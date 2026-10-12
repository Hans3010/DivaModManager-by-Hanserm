using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Features.Health
{
    /// <summary>A mod required by another mod (name and page URL)</summary>
    public record Requirement(string Name, string Url, bool Optional);

    /// <summary>
    /// Finds the mods a mod needs.
    /// - GameBanana: the page's "Requirements" and GameBanana links near "requires ..." in the description
    /// - DivaModArchive: the post's dependencies
    /// - What the files show: rom/nc_db.toml → New Classics, lyrics/*.toml → LyricsPatch
    /// Site data is cached in cache/requirements.json for a day, so the check before launching doesn't go online.
    /// </summary>
    public static class ModRequirements
    {
        private static readonly TimeSpan CACHE_TIME = TimeSpan.FromDays(1);
        private static string CachePath => Path.Combine(Global.assemblyLocation, "cache", "requirements.json");

        private class CacheEntry
        {
            public DateTime Time { get; set; }
            public List<Requirement> Requirements { get; set; } = new();
        }

        private static Dictionary<string, CacheEntry> cache;
        private static readonly object cacheLock = new();

        public static readonly Requirement NewClassics = new("New Classics", "https://divamodarchive.com/posts/169", false);
        public static readonly Requirement LyricsPatch = new("LyricsPatch", "https://gamebanana.com/mods/414986", false);

        #region mod.json

        /// <summary>The homepage in mod.json (GameBanana / DivaModArchive page)</summary>
        public static string ReadHomepage(string modPath)
        {
            try
            {
                var json = Path.Combine(modPath, "mod.json");
                if (!File.Exists(json)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                return doc.RootElement.TryGetProperty("homepage", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
            }
            catch (Exception) { return null; }
        }

        private static (string Site, int Id) ParseUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return (null, 0);
            var gb = Regex.Match(url, @"gamebanana\.com/mods/(\d+)", RegexOptions.IgnoreCase);
            if (gb.Success) return ("gb", int.Parse(gb.Groups[1].Value));
            var dma = Regex.Match(url, @"divamodarchive\.com/posts?/(\d+)", RegexOptions.IgnoreCase);
            if (dma.Success) return ("dma", int.Parse(dma.Groups[1].Value));
            return (null, 0);
        }

        #endregion

        #region Cache

        private static void LoadCache()
        {
            if (cache != null) return;
            try
            {
                cache = File.Exists(CachePath)
                    ? JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(CachePath)) ?? new()
                    : new();
            }
            catch (Exception) { cache = new(); }
        }

        private static void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                File.WriteAllText(CachePath, JsonSerializer.Serialize(cache));
            }
            catch (Exception) { }
        }

        /// <summary>Cached required mods (doesn't go online)</summary>
        public static List<Requirement> GetCached(string homepage)
        {
            var (site, id) = ParseUrl(homepage);
            if (site == null) return new List<Requirement>();
            lock (cacheLock)
            {
                LoadCache();
                return cache.TryGetValue($"{site}/{id}", out var e) ? e.Requirements : new List<Requirement>();
            }
        }

        /// <summary>Fetches and saves the page data for mods that are outdated or not cached yet</summary>
        public static async Task RefreshAsync(IEnumerable<string> homepages, CancellationToken token = default)
        {
            var targets = new List<(string Site, int Id)>();
            lock (cacheLock)
            {
                LoadCache();
                foreach (var url in homepages.Distinct())
                {
                    var (site, id) = ParseUrl(url);
                    if (site == null) continue;
                    if (cache.TryGetValue($"{site}/{id}", out var e) && DateTime.UtcNow - e.Time < CACHE_TIME) continue;
                    targets.Add((site, id));
                }
            }
            if (targets.Count == 0) return;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DivaModManager-by-Hanserm");
            var changed = false;
            foreach (var (site, id) in targets.Distinct())
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    var list = site == "gb" ? await FetchGameBananaAsync(client, id, token) : await FetchDivaModArchiveAsync(client, id, token);
                    lock (cacheLock) cache[$"{site}/{id}"] = new CacheEntry { Time = DateTime.UtcNow, Requirements = list };
                    changed = true;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Couldn't read the requirements of {site}/{id}: {ex.Message}", LoggerType.Debug);
                }
            }
            if (changed) lock (cacheLock) SaveCache();
        }

        private static bool LooksOptional(string text) =>
            Regex.IsMatch(text ?? "", @"optional|recommended|not actually required|opcional|recomendad", RegexOptions.IgnoreCase);

        private static async Task<List<Requirement>> FetchGameBananaAsync(HttpClient client, int id, CancellationToken token)
        {
            var result = new List<Requirement>();
            var json = await client.GetStringAsync($"https://gamebanana.com/apiv11/Mod/{id}?_csvProperties=_aRequirements,_sText", token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // The page's "Requirements" field: [ [name, URL, ...], ... ]
            if (root.TryGetProperty("_aRequirements", out var reqs) && reqs.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in reqs.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Array || r.GetArrayLength() < 1) continue;
                    var name = r[0].GetString() ?? "";
                    var url = r.GetArrayLength() > 1 ? r[1].GetString() ?? "" : "";
                    result.Add(new Requirement(name.Trim(), url.Trim(), LooksOptional(name)));
                }
            }

            // Links in the description, as in "Requirements: please install the ... mod"
            if (root.TryGetProperty("_sText", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                var text = textProp.GetString() ?? "";
                foreach (Match req in Regex.Matches(text, @"requir|prerequisite|needs? the|necesita", RegexOptions.IgnoreCase))
                {
                    var window = text.Substring(req.Index, Math.Min(500, text.Length - req.Index));
                    foreach (Match link in Regex.Matches(window, @"href=""(?:https?:)?//gamebanana\.com/mods/(\d+)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
                    {
                        var linkId = int.Parse(link.Groups[1].Value);
                        if (linkId == id) continue;
                        var url = $"https://gamebanana.com/mods/{linkId}";
                        if (result.Any(x => ParseUrl(x.Url) == ("gb", linkId))) continue;
                        var name = Regex.Replace(link.Groups[2].Value, "<[^>]+>", "").Trim();
                        if (name.Length < 3 || Regex.IsMatch(name, @"^(here|aqu[ií]|link|this)$", RegexOptions.IgnoreCase))
                            name = await GameBananaNameAsync(client, linkId, token) ?? url;
                        result.Add(new Requirement(System.Net.WebUtility.HtmlDecode(name), url, LooksOptional(window.Substring(0, Math.Min(window.Length, link.Index + link.Length + 40)))));
                    }
                }
            }
            return result;
        }

        private static async Task<string> GameBananaNameAsync(HttpClient client, int id, CancellationToken token)
        {
            try
            {
                using var doc = JsonDocument.Parse(await client.GetStringAsync($"https://gamebanana.com/apiv11/Mod/{id}?_csvProperties=_sName", token));
                return doc.RootElement.TryGetProperty("_sName", out var n) ? n.GetString() : null;
            }
            catch (Exception) { return null; }
        }

        private static async Task<List<Requirement>> FetchDivaModArchiveAsync(HttpClient client, int id, CancellationToken token)
        {
            var result = new List<Requirement>();
            using var doc = JsonDocument.Parse(await client.GetStringAsync($"{Global.DMA_API_URL_POSTS}{id}", token));
            if (doc.RootElement.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in deps.EnumerateArray())
                {
                    if (!d.TryGetProperty("id", out var depId)) continue;
                    var name = d.TryGetProperty("name", out var n) ? n.GetString() : $"DivaModArchive {depId}";
                    result.Add(new Requirement(name, $"https://divamodarchive.com/posts/{depId.GetInt64()}", false));
                }
            }
            return result;
        }

        #endregion

        #region Installed check

        public class InstalledMod
        {
            public string Name { get; init; }
            public string Path { get; init; }
            public bool Enabled { get; init; }
            public string Homepage { get; init; }
            public List<string> DllNames { get; init; } = new();
        }

        private static string Normalize(string s) =>
            Regex.Replace(Regex.Replace(s ?? "", @"\(.*?\)|\[.*?\]", ""), @"[^a-z0-9]", "", RegexOptions.IgnoreCase).ToLowerInvariant();

        /// <summary>Whether the required mod's name matches a DLL name (for example "OpenLyricLimit (Optional)" and OpenLyricLimit.dll)</summary>
        public static bool MatchesDll(Requirement r, string dll) =>
            Normalize(System.IO.Path.GetFileNameWithoutExtension(dll)) is { Length: >= 4 } d && d == Normalize(r.Name);

        /// <summary>DivaModLoader (it goes in the game folder, not in mods) isn't checked</summary>
        public static bool IsModLoader(Requirement r) =>
            Regex.IsMatch(r.Name + " " + r.Url, @"diva\s*mod\s*loader|DivaModLoader", RegexOptions.IgnoreCase);

        /// <summary>Finds a required mod among the installed mods (null if missing)</summary>
        public static InstalledMod FindInstalled(Requirement r, IEnumerable<InstalledMod> mods)
        {
            var (site, id) = ParseUrl(r.Url);
            var name = Normalize(r.Name);
            InstalledMod best = null;
            foreach (var m in mods)
            {
                var match = site != null && ParseUrl(m.Homepage) == (site, id);
                if (!match && name.Length >= 4)
                {
                    var modName = Normalize(m.Name);
                    match = modName.Length >= 4 && (modName.Contains(name) || name.Contains(modName))
                        || m.DllNames.Any(d => MatchesDll(r, d));
                }
                if (!match) continue;
                if (m.Enabled) return m;
                best ??= m;
            }
            return best;
        }

        #endregion
    }
}
