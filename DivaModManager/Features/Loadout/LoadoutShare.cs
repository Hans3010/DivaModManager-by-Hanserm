using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DivaModManager.Features.Loadout
{
    /// <summary>
    /// Shares a loadout as a small JSON file: the mod order (first = highest priority), whether each mod is enabled
    /// and the page it was downloaded from, so whoever imports it can download the mods they don't have.
    /// Rust4Diva modpacks ({"name", "mods": [{"name", "enabled", "path"}]}) can be imported too.
    /// </summary>
    public static class LoadoutShare
    {
        public const string FILE_FILTER = "Loadout (*.json)|*.json|All files (*.*)|*.*";
        public const int FORMAT_VERSION = 1;

        public class SharedLoadout
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
            [JsonPropertyName("app")]
            public string App { get; set; }
            [JsonPropertyName("format")]
            public int Format { get; set; } = FORMAT_VERSION;
            [JsonPropertyName("mods")]
            public List<SharedMod> Mods { get; set; } = new();
        }

        public class SharedMod
        {
            /// <summary>Name in config.toml (or the folder name if it has none)</summary>
            [JsonPropertyName("name")]
            public string Name { get; set; }
            /// <summary>Folder in the mods folder</summary>
            [JsonPropertyName("folder")]
            public string Folder { get; set; }
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }
            /// <summary>GameBanana / DivaModArchive page (null for mods installed by hand)</summary>
            [JsonPropertyName("page")]
            public string Page { get; set; }
            /// <summary>Only in Rust4Diva modpacks: absolute path of the mod folder</summary>
            [JsonPropertyName("path")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Path { get; set; }

            [JsonIgnore]
            public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : FolderName ?? "?";

            /// <summary>Folder name, also taken from the path of Rust4Diva modpacks</summary>
            [JsonIgnore]
            public string FolderName
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(Folder)) return Folder;
                    if (string.IsNullOrWhiteSpace(Path)) return null;
                    var name = System.IO.Path.GetFileName(Path.TrimEnd('/', '\\'));
                    // Rust4Diva paths may come from Linux ("/home/.../mods/Foo")
                    var slash = name.LastIndexOfAny(new[] { '/', '\\' });
                    return slash >= 0 ? name[(slash + 1)..] : name;
                }
            }

            /// <summary>True if the mod can be downloaded with the one-click installer</summary>
            [JsonIgnore]
            public bool CanDownload => PageKey(Page) != null;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // Keep Japanese mod names readable in the file
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        #region Export

        public static SharedLoadout Create(string loadoutName, IEnumerable<Mod> mods) => new()
        {
            Name = loadoutName,
            App = "DivaModManager by Hanserm",
            Mods = mods.Select(m => new SharedMod
            {
                Name = string.IsNullOrWhiteSpace(m.ConfigTomlModName) ? m.name : m.ConfigTomlModName,
                Folder = m.name,
                Enabled = m.enabled,
                Page = m.homepage?.ToString(),
            }).ToList(),
        };

        public static void Save(SharedLoadout loadout, string path) =>
            File.WriteAllText(path, JsonSerializer.Serialize(loadout, JsonOptions), new UTF8Encoding(false));

        /// <summary>File name for a loadout (characters Windows doesn't allow are replaced)</summary>
        public static string FileNameFor(string loadoutName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((loadoutName ?? "Loadout").Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return $"{(string.IsNullOrEmpty(name) ? "Loadout" : name)}.loadout.json";
        }

        #endregion

        #region Import

        /// <summary>Reads a shared loadout. Throws if the file isn't one</summary>
        public static SharedLoadout Load(string path)
        {
            var loadout = JsonSerializer.Deserialize<SharedLoadout>(File.ReadAllText(path), JsonOptions);
            if (loadout?.Mods == null) throw new InvalidDataException("The file has no \"mods\" list.");
            loadout.Mods = loadout.Mods.Where(m => m != null && (m.FolderName != null || !string.IsNullOrWhiteSpace(m.Name))).ToList();
            if (loadout.Mods.Count == 0) throw new InvalidDataException("The file has no mods.");
            if (string.IsNullOrWhiteSpace(loadout.Name)) loadout.Name = Path.GetFileNameWithoutExtension(path).Replace(".loadout", "");
            return loadout;
        }

        /// <summary>
        /// Builds a new loadout from a shared one: installed mods in the shared order and state, then the installed mods
        /// the file doesn't mention (disabled). Returns the mods of the file that aren't installed
        /// </summary>
        public static ObservableCollection<Mod> Build(SharedLoadout shared, IReadOnlyList<Mod> installed, out List<SharedMod> missing)
        {
            var result = new ObservableCollection<Mod>();
            var used = new HashSet<Mod>();
            missing = new();
            foreach (var entry in shared.Mods)
            {
                var mod = Find(entry, installed, used);
                if (mod == null)
                {
                    missing.Add(entry);
                    continue;
                }
                used.Add(mod);
                var copy = mod.Clone();
                copy.enabled = entry.Enabled;
                result.Add(copy);
            }
            foreach (var mod in installed.Where(m => !used.Contains(m)))
            {
                var copy = mod.Clone();
                copy.enabled = false;
                result.Add(copy);
            }
            return result;
        }

        /// <summary>
        /// Puts the mods of a loadout back in the shared order and state (used after downloading the missing mods,
        /// which are added at the top or bottom). Mods the file doesn't mention keep their order after them.
        /// Returns how many of the shared mods are in the loadout
        /// </summary>
        public static int ApplyOrder(SharedLoadout shared, ObservableCollection<Mod> loadout)
        {
            var used = new HashSet<Mod>();
            var ordered = new List<Mod>();
            foreach (var entry in shared.Mods)
            {
                var mod = Find(entry, loadout.ToList(), used);
                if (mod == null) continue;
                used.Add(mod);
                if (mod.enabled != entry.Enabled) mod.enabled = entry.Enabled;
                ordered.Add(mod);
            }
            ordered.AddRange(loadout.Where(m => !used.Contains(m)));
            for (var i = 0; i < ordered.Count; i++)
            {
                var index = loadout.IndexOf(ordered[i]);
                if (index != i) loadout.Move(index, i);
            }
            return used.Count;
        }

        /// <summary>
        /// Installed mod for an entry of the file: same folder, else same download page (several folders can come
        /// from one page, so the config.toml name decides), else same config.toml name
        /// </summary>
        private static Mod Find(SharedMod entry, IReadOnlyList<Mod> installed, HashSet<Mod> used)
        {
            var candidates = installed.Where(m => !used.Contains(m)).ToList();
            var folder = entry.FolderName;
            if (folder != null)
            {
                var byFolder = candidates.FirstOrDefault(m => string.Equals(m.name, folder, StringComparison.OrdinalIgnoreCase));
                if (byFolder != null) return byFolder;
            }
            var key = PageKey(entry.Page);
            if (key != null)
            {
                var byPage = candidates.Where(m => PageKey(m.homepage?.ToString()) == key).ToList();
                if (byPage.Count == 1) return byPage[0];
                var byPageAndName = byPage.FirstOrDefault(m => SameName(m, entry.Name));
                if (byPageAndName != null) return byPageAndName;
                // Several folders from the same page with other names: don't guess
                if (byPage.Count > 1) return null;
            }
            return string.IsNullOrWhiteSpace(entry.Name) ? null : candidates.FirstOrDefault(m => SameName(m, entry.Name));
        }

        private static bool SameName(Mod mod, string name) =>
            !string.IsNullOrWhiteSpace(name)
            && (string.Equals(mod.ConfigTomlModName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mod.name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>"gb:123" / "dma:45" for a GameBanana / DivaModArchive mod page, null otherwise</summary>
        public static string PageKey(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var gb = Regex.Match(url, @"gamebanana\.com/mods/(\d+)", RegexOptions.IgnoreCase);
            if (gb.Success) return "gb:" + gb.Groups[1].Value;
            var dma = Regex.Match(url, @"divamodarchive\.com/posts?/(\d+)", RegexOptions.IgnoreCase);
            if (dma.Success) return "dma:" + dma.Groups[1].Value;
            return null;
        }

        /// <summary>Name not used by another loadout ("Name", "Name (2)", ...)</summary>
        public static string UniqueName(string name, ICollection<string> existing)
        {
            name = string.IsNullOrWhiteSpace(name) ? "Imported" : name.Trim();
            if (!existing.Contains(name)) return name;
            for (var i = 2; ; i++)
            {
                var candidate = $"{name} ({i})";
                if (!existing.Contains(candidate)) return candidate;
            }
        }

        #endregion
    }
}
