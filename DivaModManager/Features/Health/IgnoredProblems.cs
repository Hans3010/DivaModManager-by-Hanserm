using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DivaModManager.Features.Health
{
    /// <summary>
    /// 「無視」にした問題（ignored-problems.json）。Mod の作者側の問題など、ユーザーが直せないものを黙らせる
    /// </summary>
    public static class IgnoredProblems
    {
        private static string FilePath => Path.Combine(Global.assemblyLocation, "ignored-problems.json");
        private static HashSet<string> keys;
        private static readonly object sync = new();

        private static void Load()
        {
            if (keys != null) return;
            try
            {
                keys = File.Exists(FilePath)
                    ? new HashSet<string>(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new(), StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
            }
            catch (Exception) { keys = new HashSet<string>(StringComparer.Ordinal); }
        }

        public static bool Contains(string key)
        {
            lock (sync) { Load(); return keys.Contains(key); }
        }

        public static void Set(string key, bool ignored)
        {
            lock (sync)
            {
                Load();
                if (ignored) keys.Add(key); else keys.Remove(key);
                try { File.WriteAllText(FilePath, JsonSerializer.Serialize(keys.OrderBy(k => k).ToList(), new JsonSerializerOptions { WriteIndented = true })); }
                catch (Exception) { }
            }
        }
    }
}
