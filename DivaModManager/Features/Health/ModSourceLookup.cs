using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DivaModManager.Features.Health
{
    /// <summary>
    /// Finds the GameBanana / DivaModArchive page of mods that were installed by hand (no mod.json).
    /// Only an exact name match counts, so a different mod with a similar name is never picked.
    /// Results are cached in cache/sources.json (found pages for good, misses for a week).
    /// </summary>
    public static class ModSourceLookup
    {
        private static readonly TimeSpan MISS_CACHE_TIME = TimeSpan.FromDays(7);
        private static string CachePath => Path.Combine(Global.assemblyLocation, "cache", "sources.json");

        private class Entry
        {
            public string Url { get; set; }
            public DateTime Time { get; set; }
        }

        private static Dictionary<string, Entry> cache;
        private static readonly object sync = new();

        private static string Normalize(string s) => Regex.Replace((s ?? "").ToLowerInvariant(), "[^0-9a-z]", "");

        private static void Load()
        {
            if (cache != null) return;
            try { cache = File.Exists(CachePath) ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(CachePath)) ?? new() : new(); }
            catch (Exception) { cache = new(); }
        }

        /// <summary>Cached page for a mod name (null if unknown or not found). Doesn't go online</summary>
        public static Uri Get(string modName)
        {
            lock (sync)
            {
                Load();
                return cache.TryGetValue(Normalize(modName), out var e) && !string.IsNullOrEmpty(e.Url) ? new Uri(e.Url) : null;
            }
        }

        /// <summary>Looks up the names that aren't cached yet. Returns how many pages were found</summary>
        public static async Task<int> RefreshAsync(IEnumerable<string> modNames)
        {
            List<string> todo;
            lock (sync)
            {
                Load();
                todo = modNames.Where(n => Normalize(n).Length >= 3)
                    .Where(n => !cache.TryGetValue(Normalize(n), out var e) || (string.IsNullOrEmpty(e.Url) && DateTime.UtcNow - e.Time > MISS_CACHE_TIME))
                    .Distinct().ToList();
            }
            if (todo.Count == 0) return 0;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DivaModManager-by-Hanserm");
            var found = 0;
            foreach (var name in todo)
            {
                string url = null;
                try { url = await FindAsync(client, name); }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Couldn't look up the page of {name}: {ex.Message}", LoggerType.Debug);
                    continue; // not cached, so it's tried again next time
                }
                if (url != null) found++;
                lock (sync) cache[Normalize(name)] = new Entry { Url = url, Time = DateTime.UtcNow };
            }
            lock (sync)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                    File.WriteAllText(CachePath, JsonSerializer.Serialize(cache));
                }
                catch (Exception) { }
            }
            return found;
        }

        private static async Task<string> FindAsync(HttpClient client, string name)
        {
            var key = Normalize(name);
            // GameBanana: mods of this game with exactly this name
            var gbUrl = $"https://gamebanana.com/apiv11/Util/Search/Results?_sModelName=Mod&_sOrder=best_match&_idGameRow={Global.GAME_ID}&_sSearchString={Uri.EscapeDataString(name)}&_nPage=1";
            using (var gb = JsonDocument.Parse(await client.GetStringAsync(gbUrl)))
            {
                if (gb.RootElement.TryGetProperty("_aRecords", out var records))
                    foreach (var r in records.EnumerateArray())
                        if (Normalize(r.GetProperty("_sName").GetString()) == key && r.TryGetProperty("_idRow", out var id))
                            return $"https://gamebanana.com/mods/{id.GetInt64()}";
            }
            // DivaModArchive
            using (var dma = JsonDocument.Parse(await client.GetStringAsync($"https://divamodarchive.com/api/v1/posts?query={Uri.EscapeDataString(name)}")))
            {
                if (dma.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var p in dma.RootElement.EnumerateArray())
                        if (Normalize(p.GetProperty("name").GetString()) == key)
                            return $"https://divamodarchive.com/posts/{p.GetProperty("id").GetInt64()}";
            }
            return null;
        }
    }
}
