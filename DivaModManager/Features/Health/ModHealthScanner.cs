using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager.Features.Health
{
    public enum HealthSeverity
    {
        Error,
        Warning,
        Info,
    }

    public class HealthIssue
    {
        public HealthSeverity Severity { get; set; }
        public string Mod { get; set; }
        /// <summary>Problem kind in English (shown translated through SeverityText / KindText)</summary>
        public string Kind { get; set; }
        public string Detail { get; set; }
        /// <summary>Page of the required mod (used by the Install button, if any)</summary>
        public string InstallUrl { get; set; }
        public string InstallName { get; set; }
        /// <summary>What the problem is about (song ID, DLL name, etc.). Used as the key in the ignore list (stays the same when the language changes)</summary>
        public string Subject { get; set; }
        /// <summary>A problem the user ignored (not counted, not asked about before launching)</summary>
        public bool Ignored { get; set; }
        public string Key => $"{Mod}|{Kind}|{Subject}";

        public string SeverityText => UiTranslator.T(Ignored ? "Ignored" : Severity switch
        {
            HealthSeverity.Error => "Error",
            HealthSeverity.Warning => "Warning",
            _ => "Info",
        });
        public string KindText => UiTranslator.T(Kind);
    }

    /// <summary>
    /// Looks for things in the enabled mods that are likely to cause problems in game.
    /// - A mod's DLL is missing, or a library the DLL needs can't be found (DivaModLoader's "Failed to load")
    /// - Charts, audio or videos used by songs in mod_pv_db aren't in any mod
    /// - Lyrics have more than 150 lines but OpenLyricLimit isn't installed
    /// </summary>
    public static class ModHealthScanner
    {
        private const int LYRIC_LIMIT = 150;

        /// <summary>The game folder (where DivaMegaMix.exe is)</summary>
        public static string GameDir
        {
            get
            {
                var launcher = Global.ConfigJson?.CurrentConfig?.Launcher;
                return string.IsNullOrEmpty(launcher) ? null : Path.GetDirectoryName(launcher);
            }
        }

        /// <param name="installed">Every installed mod (including disabled ones), used to check required mods. If null, only the enabled mods</param>
        public static List<HealthIssue> Scan(IReadOnlyList<(string Name, string Path)> mods, string gameDir, IReadOnlyList<ModRequirements.InstalledMod> installed = null)
        {
            var issues = new List<HealthIssue>();
            var valid = mods.Where(m => !string.IsNullOrEmpty(m.Path) && Directory.Exists(m.Path)).ToList();

            // Files of every enabled mod (paths relative to the mod, "rom/..."). DML loads these as game files
            var modFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, path) in valid)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        modFiles.Add(Path.GetRelativePath(path, file).Replace('\\', '/'));
                }
                catch (Exception) { }
            }

            var dlls = valid.Select(m => (m.Name, m.Path, Dlls: ReadDllList(m.Path))).ToList();
            var loadedDllNames = new HashSet<string>(dlls.SelectMany(d => d.Dlls.Select(Path.GetFileName)), StringComparer.OrdinalIgnoreCase);
            var hasLyricLimitPatch = loadedDllNames.Contains("OpenLyricLimit.dll");

            foreach (var (name, path, list) in dlls)
                CheckDlls(name, path, list, gameDir, loadedDllNames, issues);

            installed ??= dlls.Select(d => new ModRequirements.InstalledMod { Name = d.Name, Path = d.Path, Enabled = true, Homepage = ModRequirements.ReadHomepage(d.Path), DllNames = d.Dlls }).ToList();
            foreach (var (name, path) in valid)
                CheckRequirements(name, path, installed, issues);

            foreach (var (name, path) in valid)
            {
                var pvDb = Path.Combine(path, "rom", "mod_pv_db.txt");
                if (File.Exists(pvDb))
                    CheckSongs(name, pvDb, gameDir, modFiles, hasLyricLimitPatch, issues);
            }

            // ThumbnailSplitter has to be at the top (highest priority), as its author says
            var splitter = valid.FindIndex(m => IsThumbnailSplitter(m.Name, m.Path));
            if (splitter > 0)
            {
                issues.Add(new HealthIssue
                {
                    Severity = HealthSeverity.Warning,
                    Mod = valid[splitter].Name,
                    Kind = "Wrong load order",
                    Subject = "ThumbnailSplitter",
                    Detail = UiTranslator.T("ThumbnailSplitter has to be at the top of the mod list to work properly. Drag it to the first place."),
                });
            }

            foreach (var issue in issues) issue.Ignored = IgnoredProblems.Contains(issue.Key);
            return issues;
        }

        /// <summary>Whether this is ThumbnailSplitter (a mod that resolves thumbnail conflicts between packs automatically)</summary>
        public static bool IsThumbnailSplitter(string name, string path) =>
            (name ?? "").Replace(" ", "").Contains("ThumbnailSplitter", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, "ThumbnailSplitter.dll")));

        #region Required mods

        /// <summary>List of installed mods (to check required mods)</summary>
        public static List<ModRequirements.InstalledMod> DescribeInstalled(IEnumerable<(string Name, string Path, bool Enabled)> mods) =>
            mods.Where(m => !string.IsNullOrEmpty(m.Path) && Directory.Exists(m.Path))
                .Select(m => new ModRequirements.InstalledMod { Name = m.Name, Path = m.Path, Enabled = m.Enabled, Homepage = ModRequirements.ReadHomepage(m.Path), DllNames = ReadDllList(m.Path) })
                .ToList();

        private static void CheckRequirements(string mod, string modPath, IReadOnlyList<ModRequirements.InstalledMod> installed, List<HealthIssue> issues)
        {
            var requirements = new List<Requirement>(ModRequirements.GetCached(ModRequirements.ReadHomepage(modPath)));
            // Required mods that the files show
            if (File.Exists(Path.Combine(modPath, "rom", "nc_db.toml"))) requirements.Add(ModRequirements.NewClassics);
            var lyrics = Path.Combine(modPath, "lyrics");
            if (Directory.Exists(lyrics) && Directory.EnumerateFiles(lyrics, "*.toml").Any()) requirements.Add(ModRequirements.LyricsPatch);

            var others = installed.Where(m => !string.Equals(m.Path, modPath, StringComparison.OrdinalIgnoreCase)).ToList();
            var self = installed.FirstOrDefault(m => string.Equals(m.Path, modPath, StringComparison.OrdinalIgnoreCase));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var req in requirements)
            {
                if (ModRequirements.IsModLoader(req) || !seen.Add(req.Url + "|" + req.Name)) continue;
                // The mod already includes what it needs (for example OpenLyricLimit inside Spanish Lyrics)
                if (self != null && self.DllNames.Any(d => ModRequirements.MatchesDll(req, d))) continue;
                var found = ModRequirements.FindInstalled(req, others);
                if (found == null)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = req.Optional ? HealthSeverity.Info : HealthSeverity.Error,
                        Mod = mod,
                        Kind = req.Optional ? "Recommended mod" : "Missing required mod",
                        Subject = req.Url + "|" + req.Name,
                        Detail = req.Optional
                            ? UiTranslator.Format("{0} recommends {1}. Select this row and click Install to get it.", mod, req.Name)
                            : UiTranslator.Format("{0} needs {1}, which isn't installed. Select this row and click Install to get it.", mod, req.Name),
                        InstallUrl = req.Url,
                        InstallName = req.Name,
                    });
                }
                else if (!found.Enabled && !req.Optional)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Warning,
                        Mod = mod,
                        Kind = "Required mod disabled",
                        Subject = found.Name,
                        Detail = UiTranslator.Format("{0} needs {1}, which is installed but disabled. Enable it in the mod list.", mod, found.Name),
                    });
                }
            }
        }

        #endregion

        #region DLL

        private static List<string> ReadDllList(string modPath)
        {
            var result = new List<string>();
            var config = Path.Combine(modPath, "config.toml");
            if (!File.Exists(config)) return result;
            try
            {
                var model = Toml.ToModel(File.ReadAllText(config));
                if (model.TryGetValue("enabled", out var enabled) && enabled is bool b && !b) return result;
                if (model.TryGetValue("dll", out var dll))
                {
                    if (dll is TomlArray array) result.AddRange(array.OfType<string>());
                    else if (dll is string single) result.Add(single);
                }
            }
            catch (Exception) { }
            return result;
        }

        private static void CheckDlls(string mod, string modPath, List<string> dlls, string gameDir, HashSet<string> loadedDllNames, List<HealthIssue> issues)
        {
            foreach (var dll in dlls)
            {
                var dllPath = Path.GetFullPath(Path.Combine(modPath, dll));
                if (!File.Exists(dllPath))
                {
                    issues.Add(new HealthIssue { Severity = HealthSeverity.Error, Mod = mod, Kind = "Missing DLL", Subject = dll, Detail = UiTranslator.Format("{0} is listed in config.toml but doesn't exist.", dll) });
                    continue;
                }
                List<string> imports;
                try { imports = PeImports.Read(dllPath); }
                catch (Exception) { continue; }

                var searchDirs = new List<string> { Path.GetDirectoryName(dllPath), modPath, gameDir, Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) };
                // PATH isn't checked: the game is started from Steam, so it doesn't necessarily get the same PATH as DMM
                // (libraries that are only in a PATH such as MSYS2's aren't found by the game, which shows "Failed to load")
                var missing = imports
                    .Where(i => !IsApiSet(i) && !loadedDllNames.Contains(i))
                    .Where(i => !searchDirs.Any(d => SafeExists(d, i)))
                    .ToList();
                if (missing.Count > 0)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Error,
                        Mod = mod,
                        Kind = "DLL won't load",
                        Subject = Path.GetFileName(dllPath),
                        Detail = UiTranslator.Format("{0} needs {1}, which the game can't find. It will show \"Failed to load\". Reinstall the mod or get a build that includes them.", Path.GetFileName(dllPath), string.Join(", ", missing)),
                    });
                }
            }
        }

        private static bool IsApiSet(string name) =>
            name.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ext-ms-", StringComparison.OrdinalIgnoreCase);

        private static bool SafeExists(string dir, string file)
        {
            try { return !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir.Trim().Trim('"'), file)); }
            catch (Exception) { return false; }
        }

        #endregion

        #region Songs

        private static void CheckSongs(string mod, string pvDbPath, string gameDir, HashSet<string> modFiles, bool hasLyricLimitPatch, List<HealthIssue> issues)
        {
            var kv = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (var line in File.ReadLines(pvDbPath, Encoding.UTF8))
                {
                    if (line.StartsWith('#')) continue;
                    var eq = line.IndexOf('=');
                    if (eq > 0) kv[line.Substring(0, eq)] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception) { return; }

            string SongName(string id) =>
                kv.TryGetValue($"pv_{id}.song_name_en", out var en) && en.Length > 0 ? en
                : kv.TryGetValue($"pv_{id}.song_name", out var jp) ? jp : "";

            var missingBySong = new Dictionary<string, (List<string> Charts, List<string> Audio, List<string> Movies)>();
            var lyricCount = new Dictionary<string, int>();
            var presentCharts = new Dictionary<string, int>();

            foreach (var (key, value) in kv)
            {
                if (!key.StartsWith("pv_")) continue;
                var dot = key.IndexOf('.');
                if (dot < 4) continue;
                var id = key.Substring(3, dot - 3);
                var rest = key.Substring(dot + 1);

                if (rest.StartsWith("lyric.") || rest.StartsWith("lyric_en."))
                {
                    var n = rest.StartsWith("lyric.") ? 0 : 1;
                    lyricCount.TryGetValue(id + n, out var c);
                    lyricCount[id + n] = c + 1;
                    continue;
                }
                if (!rest.EndsWith("file_name") || string.IsNullOrWhiteSpace(value)) continue;
                if (!IsActiveEntry(kv, id, rest)) continue;

                var file = value.Replace('\\', '/');
                if (FileExists(file, gameDir, modFiles))
                {
                    if (rest.Contains("script_file_name")) { presentCharts.TryGetValue(id, out var n); presentCharts[id] = n + 1; }
                    continue;
                }

                if (!missingBySong.TryGetValue(id, out var m)) { m = (new(), new(), new()); missingBySong[id] = m; }
                if (rest.Contains("script_file_name")) m.Charts.Add(Path.GetFileName(file));
                else if (rest.Contains("movie_file_name")) m.Movies.Add(Path.GetFileName(file));
                else if (rest.Contains("song_file_name")) m.Audio.Add(Path.GetFileName(file));
            }

            foreach (var (id, m) in missingBySong.OrderBy(x => int.TryParse(x.Key, out var n) ? n : 0))
            {
                var song = $"pv_{id} {SongName(id)}".Trim();
                // Other difficulties' charts and the audio exist → only that difficulty can't be chosen (the song is playable)
                if (m.Audio.Count == 0 && m.Charts.Count > 0 && presentCharts.ContainsKey(id))
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Warning,
                        Mod = mod,
                        Kind = "Missing difficulty",
                        Subject = $"pv_{id}",
                        Detail = UiTranslator.Format("{0}: missing {1}. That difficulty won't load, the others work. This comes from the mod itself.", song, string.Join(", ", m.Charts.Distinct())),
                    });
                }
                else if (m.Charts.Count > 0 || m.Audio.Count > 0)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Error,
                        Mod = mod,
                        Kind = "Missing song files",
                        Subject = $"pv_{id}",
                        Detail = UiTranslator.Format("{0}: missing {1}. The song may not load or may crash.", song, string.Join(", ", m.Charts.Concat(m.Audio).Distinct())),
                    });
                }
                if (m.Movies.Count > 0)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Warning,
                        Mod = mod,
                        Kind = "Missing video",
                        Subject = $"pv_{id}",
                        Detail = UiTranslator.Format("{0}: missing {1}. The song plays without its video (some packs offer videos as a separate download).", song, string.Join(", ", m.Movies.Distinct())),
                    });
                }
            }

            if (!hasLyricLimitPatch)
            {
                foreach (var id in lyricCount.Keys.Select(k => k[..^1]).Distinct())
                {
                    lyricCount.TryGetValue(id + 0, out var jp);
                    lyricCount.TryGetValue(id + 1, out var en);
                    var lines = Math.Max(jp, en);
                    if (lines <= LYRIC_LIMIT) continue;
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Info,
                        Mod = mod,
                        Kind = "Lyrics cut off",
                        Subject = $"pv_{id}",
                        Detail = UiTranslator.Format("pv_{0} {1} has {2} lyric lines, but the game only shows {3}. Install OpenLyricLimit (included in Spanish Lyrics) to see them all.", id, SongName(id), lines, LYRIC_LIMIT),
                    });
                }
            }
        }

        /// <summary>
        /// Unused difficulties and alternate audio (numbers past their length) are ignored.
        /// Example: with difficulty.hard.length=0, difficulty.hard.0.script_file_name isn't loaded
        /// </summary>
        private static bool IsActiveEntry(Dictionary<string, string> kv, string id, string rest)
        {
            var parts = rest.Split('.');
            string lengthKey = null; int index = -1;
            if (parts.Length >= 4 && parts[0] == "difficulty" && int.TryParse(parts[2], out index))
                lengthKey = $"pv_{id}.difficulty.{parts[1]}.length";
            else if (parts.Length >= 3 && parts[0] == "another_song" && int.TryParse(parts[1], out index))
                lengthKey = $"pv_{id}.another_song.length";
            if (lengthKey == null) return true;
            return kv.TryGetValue(lengthKey, out var len) && int.TryParse(len, out var n) && index < n;
        }

        private static bool FileExists(string file, string gameDir, HashSet<string> modFiles)
        {
            if (modFiles.Contains(file)) return true;
            // Official songs and similar are in the game's own archives, so only files in folders can be checked here
            if (!string.IsNullOrEmpty(gameDir) && SafeExists(gameDir, file)) return true;
            // The PC version loads the .usm with the same name even when pv_db says .mp4
            if (file.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) && modFiles.Contains(file[..^4] + ".usm")) return true;
            // Files of the game's own songs (IDs in the official range) are in the game itself, so they aren't checked
            if (IsOfficialAsset(file)) return true;
            return false;
        }

        /// <summary>Files of official songs (pv_001 to pv_999, etc., which are in the game's archives)</summary>
        private static bool IsOfficialAsset(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith("pv_", StringComparison.OrdinalIgnoreCase)) return false;
            var digits = new string(name.Skip(3).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var id) && id < 1000;
        }

        #endregion
    }
}
