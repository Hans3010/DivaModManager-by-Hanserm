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
        /// <summary>英語の種類名（表示は SeverityText / KindText で翻訳）</summary>
        public string Kind { get; set; }
        public string Detail { get; set; }

        public string SeverityText => UiTranslator.T(Severity switch
        {
            HealthSeverity.Error => "Error",
            HealthSeverity.Warning => "Warning",
            _ => "Info",
        });
        public string KindText => UiTranslator.T(Kind);
    }

    /// <summary>
    /// 有効な Mod のうち、ゲームで問題になりそうなものを探す。
    /// - Mod の DLL が見つからない、または DLL が必要とするライブラリが見つからない（DivaModLoader の "Failed to load"）
    /// - mod_pv_db の曲が使う譜面・音声・動画ファイルがどの Mod にもない
    /// - 歌詞が 150 行を超えるのに OpenLyricLimit がない
    /// </summary>
    public static class ModHealthScanner
    {
        private const int LYRIC_LIMIT = 150;

        /// <summary>ゲームのフォルダ（DivaMegaMix.exe のあるフォルダ）</summary>
        public static string GameDir
        {
            get
            {
                var launcher = Global.ConfigJson?.CurrentConfig?.Launcher;
                return string.IsNullOrEmpty(launcher) ? null : Path.GetDirectoryName(launcher);
            }
        }

        public static List<HealthIssue> Scan(IReadOnlyList<(string Name, string Path)> mods, string gameDir)
        {
            var issues = new List<HealthIssue>();
            var valid = mods.Where(m => !string.IsNullOrEmpty(m.Path) && Directory.Exists(m.Path)).ToList();

            // すべての有効な Mod のファイル（"rom/..." の相対パス）。DML はこれらをゲームのファイルとして読み込む
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

            foreach (var (name, path) in valid)
            {
                var pvDb = Path.Combine(path, "rom", "mod_pv_db.txt");
                if (File.Exists(pvDb))
                    CheckSongs(name, pvDb, gameDir, modFiles, hasLyricLimitPatch, issues);
            }
            return issues;
        }

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
                    issues.Add(new HealthIssue { Severity = HealthSeverity.Error, Mod = mod, Kind = "Missing DLL", Detail = UiTranslator.Format("{0} is listed in config.toml but doesn't exist.", dll) });
                    continue;
                }
                List<string> imports;
                try { imports = PeImports.Read(dllPath); }
                catch (Exception) { continue; }

                var searchDirs = new List<string> { Path.GetDirectoryName(dllPath), modPath, gameDir, Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) };
                // PATH は見ない: ゲームは Steam から起動するので、DMM と同じ PATH になるとは限らない
                // （MSYS2 などの PATH にだけあるライブラリは、ゲームでは見つからず "Failed to load" になる）
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

        #region 曲

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
                if (FileExists(file, gameDir, modFiles)) continue;

                if (!missingBySong.TryGetValue(id, out var m)) { m = (new(), new(), new()); missingBySong[id] = m; }
                if (rest.Contains("script_file_name")) m.Charts.Add(Path.GetFileName(file));
                else if (rest.Contains("movie_file_name")) m.Movies.Add(Path.GetFileName(file));
                else if (rest.Contains("song_file_name")) m.Audio.Add(Path.GetFileName(file));
            }

            foreach (var (id, m) in missingBySong.OrderBy(x => int.TryParse(x.Key, out var n) ? n : 0))
            {
                var song = $"pv_{id} {SongName(id)}".Trim();
                if (m.Charts.Count > 0 || m.Audio.Count > 0)
                {
                    issues.Add(new HealthIssue
                    {
                        Severity = HealthSeverity.Error,
                        Mod = mod,
                        Kind = "Missing song files",
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
                        Detail = UiTranslator.Format("pv_{0} {1} has {2} lyric lines, but the game only shows {3}. Install OpenLyricLimit (included in Spanish Lyrics) to see them all.", id, SongName(id), lines, LYRIC_LIMIT),
                    });
                }
            }
        }

        /// <summary>
        /// 使われていない難易度・別音源（length より後ろの番号）は無視する。
        /// 例: difficulty.hard.length=0 なら difficulty.hard.0.script_file_name は読み込まれない
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
            // 公式の曲などはゲーム本体のアーカイブにあるので、ここで確認できるのはフォルダにあるファイルだけ
            if (!string.IsNullOrEmpty(gameDir) && SafeExists(gameDir, file)) return true;
            // PC 版は pv_db に .mp4 と書かれていても同じ名前の .usm を読み込む
            if (file.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) && modFiles.Contains(file[..^4] + ".usm")) return true;
            // ゲーム本体の曲（ID が公式の範囲）のファイルは本体にあるので確認しない
            if (IsOfficialAsset(file)) return true;
            return false;
        }

        /// <summary>公式曲のファイル（pv_001～pv_999 など、本体のアーカイブにあるもの）</summary>
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
