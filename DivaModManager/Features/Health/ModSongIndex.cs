using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DivaModManager.Features.Health
{
    /// <summary>
    /// 各 Mod の mod_pv_db.txt に入っている曲（ID と曲名）。曲名・ID で Mod を探すのに使う
    /// </summary>
    public static class ModSongIndex
    {
        public record Song(int Id, string Name, string NameJp);

        private static readonly Dictionary<string, (DateTime Time, List<Song> Songs)> cache = new(StringComparer.OrdinalIgnoreCase);

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
            try
            {
                foreach (var line in File.ReadLines(pvDb, Encoding.UTF8))
                {
                    if (!line.StartsWith("pv_")) continue;
                    var dot = line.IndexOf('.');
                    var eq = line.IndexOf('=');
                    if (dot < 4 || eq < dot || !int.TryParse(line.AsSpan(3, dot - 3), out var id)) continue;
                    var key = line.Substring(dot + 1, eq - dot - 1);
                    if (key != "song_name" && key != "song_name_en") continue;
                    names.TryGetValue(id, out var n);
                    if (key == "song_name") n.Jp = line.Substring(eq + 1).Trim();
                    else n.En = line.Substring(eq + 1).Trim();
                    names[id] = n;
                }
            }
            catch (Exception) { }
            var songs = names.Select(x => new Song(x.Key, string.IsNullOrEmpty(x.Value.En) ? x.Value.Jp ?? "" : x.Value.En, x.Value.Jp ?? "")).ToList();
            lock (cache) cache[modDir] = (time, songs);
            return songs;
        }

        /// <summary>
        /// 曲名（英語・日本語）の一部、または曲 ID（"270" / "pv_270"）で探す。
        /// 返り値: Mod のフォルダ → 一致した曲
        /// </summary>
        public static Dictionary<string, List<Song>> Search(IEnumerable<string> modDirs, string query)
        {
            var result = new Dictionary<string, List<Song>>(StringComparer.OrdinalIgnoreCase);
            query = (query ?? "").Trim();
            if (query.Length == 0) return result;
            var idText = query.StartsWith("pv_", StringComparison.OrdinalIgnoreCase) ? query[3..] : query;
            var isId = int.TryParse(idText, out var id);
            foreach (var dir in modDirs.Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var matches = GetSongs(dir).Where(s =>
                    (isId && s.Id == id)
                    || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || s.NameJp.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count > 0) result[dir] = matches;
            }
            return result;
        }
    }
}
