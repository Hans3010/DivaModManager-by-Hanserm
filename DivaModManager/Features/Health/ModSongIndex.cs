using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DivaModManager.Features.Health
{
    /// <summary>
    /// Songs in each mod's mod_pv_db.txt (ID, name, difficulties). Used to find mods by song name or ID and to build the song list
    /// </summary>
    public static class ModSongIndex
    {
        /// <param name="Difficulties">Playable difficulties, such as "E N H EX EXEX"</param>
        public record Song(int Id, string Name, string NameJp, string Difficulties);

        private static readonly Dictionary<string, (DateTime Time, List<Song> Songs)> cache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly (string Key, string Label)[] DifficultyOrder =
        {
            ("easy", "E"), ("normal", "N"), ("hard", "H"), ("extreme", "EX"),
        };

        public static List<Song> GetSongs(string modDir)
        {
            var pvDb = Path.Combine(modDir ?? "", "rom", "mod_pv_db.txt");
            if (!File.Exists(pvDb)) return new List<Song>();
            var time = File.GetLastWriteTimeUtc(pvDb);
            lock (cache)
            {
                if (cache.TryGetValue(modDir, out var c) && c.Time == time) return c.Songs;
            }
            var names = new SortedDictionary<int, (string Jp, string En)>();
            var lengths = new Dictionary<(int Id, string Diff), int>();
            try
            {
                foreach (var line in File.ReadLines(pvDb, Encoding.UTF8))
                {
                    if (!line.StartsWith("pv_")) continue;
                    var dot = line.IndexOf('.');
                    var eq = line.IndexOf('=');
                    if (dot < 4 || eq < dot || !int.TryParse(line.AsSpan(3, dot - 3), out var id)) continue;
                    var key = line.Substring(dot + 1, eq - dot - 1);
                    var value = line.Substring(eq + 1).Trim();
                    if (key == "song_name" || key == "song_name_en")
                    {
                        names.TryGetValue(id, out var n);
                        if (key == "song_name") n.Jp = value; else n.En = value;
                        names[id] = n;
                    }
                    else if (key.StartsWith("difficulty.") && key.EndsWith(".length") && int.TryParse(value, out var len))
                    {
                        lengths[(id, key.Split('.')[1])] = len;
                    }
                }
            }
            catch (Exception) { }

            string Difficulties(int id)
            {
                var list = new List<string>();
                foreach (var (key, label) in DifficultyOrder)
                {
                    lengths.TryGetValue((id, key), out var len);
                    if (len > 0) list.Add(label);
                    // The second extreme is EXTRA EXTREME
                    if (key == "extreme" && len > 1) list.Add("EXEX");
                }
                return string.Join(" ", list);
            }

            var songs = names.Select(x => new Song(x.Key, string.IsNullOrEmpty(x.Value.En) ? x.Value.Jp ?? "" : x.Value.En, x.Value.Jp ?? "", Difficulties(x.Key))).ToList();
            lock (cache) cache[modDir] = (time, songs);
            return songs;
        }

        /// <summary>
        /// Searches by part of the song name (English or Japanese) or by song ID ("270" / "pv_270").
        /// Returns: mod folder → matching songs
        /// </summary>
        public static Dictionary<string, List<Song>> Search(IEnumerable<string> modDirs, string query)
        {
            var result = new Dictionary<string, List<Song>>(StringComparer.OrdinalIgnoreCase);
            query = (query ?? "").Trim();
            if (query.Length == 0) return result;
            foreach (var dir in modDirs.Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var matches = GetSongs(dir).Where(s => Matches(s, query)).ToList();
                if (matches.Count > 0) result[dir] = matches;
            }
            return result;
        }

        public static bool Matches(Song s, string query)
        {
            query = (query ?? "").Trim();
            if (query.Length == 0) return true;
            var idText = query.StartsWith("pv_", StringComparison.OrdinalIgnoreCase) ? query[3..] : query;
            return (int.TryParse(idText, out var id) && s.Id == id)
                || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.NameJp.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
