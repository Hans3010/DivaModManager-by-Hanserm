using DivaModManager.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DivaModManager.Features.Conflict
{
    public enum ConflictType
    {
        File,
        Song,
        Module,
        ModuleCostume,
        CustomizeItem,
    }

    public class ModConflict
    {
        public ConflictType Type { get; set; }
        /// <summary>重複しているもの（ファイルパス、pv_xxx、モジュールIDなど）</summary>
        public string Key { get; set; }
        /// <summary>曲名・モジュール名など（分かる場合）</summary>
        public string Detail { get; set; }
        /// <summary>重複している Mod（優先度順。先頭が実際に使われる）</summary>
        public List<string> Mods { get; set; } = new();

        public string TypeText => UiTranslator.T(Type switch
        {
            ConflictType.File => "File",
            ConflictType.Song => "Song ID",
            ConflictType.Module => "Module ID",
            ConflictType.ModuleCostume => "Costume slot",
            ConflictType.CustomizeItem => "Item ID",
            _ => Type.ToString(),
        });
        public string Winner => Mods.FirstOrDefault();
        public string Overridden => string.Join(", ", Mods.Skip(1));
        public string Element => string.IsNullOrEmpty(Detail) ? Key : $"{Key}  ({Detail})";
    }

    /// <summary>
    /// 有効な Mod 同士の競合を検出する。
    /// DML は同じパスのファイルが複数の Mod にある場合、優先度が一番高い Mod のものだけを読み込む。
    /// mod_pv_db.txt などの "mod_" で始まるデータベースは結合されるが、同じ ID の曲・モジュールは上書きされる。
    /// </summary>
    public static class ModConflictScanner
    {
        private const string MODULE_TABLE = "gm_module_id.bin";
        private const string ITEM_TABLE = "gm_customize_item_id.bin";

        // Mod の DLL が各 Mod フォルダから個別に読み込むファイル（DML の上書きではないので全部使われる）
        // rom/nc_db.toml: New Classics が "%s/rom/nc_db.toml" として Mod ごとに読み込む
        private static readonly HashSet<string> PerModFiles = new(StringComparer.OrdinalIgnoreCase) { "rom/nc_db.toml" };

        /// <param name="mods">有効な Mod（名前とフォルダ）を優先度の高い順に渡す</param>
        public static List<ModConflict> Scan(IReadOnlyList<(string Name, string Path)> mods)
        {
            var result = new List<ModConflict>();
            var files = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var displayPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var songs = new Dictionary<int, (List<string> Mods, string Name)>();
            var modules = new Dictionary<int, (List<string> Mods, string Name)>();
            var costumes = new Dictionary<string, (List<string> Mods, string Name)>(StringComparer.OrdinalIgnoreCase);
            var items = new Dictionary<int, (List<string> Mods, string Name)>();

            // ThumbnailSplitter があれば、パックごとのサムネイル（spr_sel_pvtmb.farc）の重複は自動で分けられる
            var hasSplitter = mods.Any(m => Health.ModHealthScanner.IsThumbnailSplitter(m.Name, m.Path));

            foreach (var (name, path) in mods)
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;

                foreach (var file in EnumerateRomFiles(path))
                {
                    var relative = Path.GetRelativePath(path, file).Replace('\\', '/');
                    var fileName = Path.GetFileName(relative);
                    // "mod_" で始まるファイルは DML が結合するので競合しない（中身の ID は別途確認する）
                    if (fileName.StartsWith("mod_", StringComparison.OrdinalIgnoreCase)) continue;
                    // ほかの Mod の DLL が Mod フォルダごとに読み込むファイルも競合しない
                    if (PerModFiles.Contains(relative)) continue;
                    // バックアップ（.bak。Fix thumbnails などで作られる）はゲームが読み込まないので競合しない
                    if (relative.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;
                    if (hasSplitter && relative.Equals("rom/2d/spr_sel_pvtmb.farc", StringComparison.OrdinalIgnoreCase)) continue;
                    AddUnique(files, relative, name);
                    displayPath.TryAdd(relative, relative);
                }

                var pvDb = Path.Combine(path, "rom", "mod_pv_db.txt");
                if (File.Exists(pvDb))
                {
                    foreach (var (id, songName) in ReadSongs(pvDb))
                    {
                        if (!songs.TryGetValue(id, out var s)) { s = (new List<string>(), songName); songs[id] = s; }
                        if (!s.Mods.Contains(name)) s.Mods.Add(name);
                    }
                }

                foreach (var table in ReadTables(path))
                {
                    var isModule = table.FileName.Equals(MODULE_TABLE, StringComparison.OrdinalIgnoreCase);
                    foreach (var entry in ParseTable(table.Text, isModule ? "module" : "cstm_item"))
                    {
                        if (!int.TryParse(entry.GetValueOrDefault("id"), out var id)) continue;
                        var entryName = entry.GetValueOrDefault("name") ?? string.Empty;
                        var target = isModule ? modules : items;
                        if (!target.TryGetValue(id, out var m)) { m = (new List<string>(), entryName); target[id] = m; }
                        if (!m.Mods.Contains(name)) m.Mods.Add(name);

                        if (isModule && entry.TryGetValue("chara", out var chara) && entry.TryGetValue("cos", out var cos))
                        {
                            var key = $"{chara} {cos}";
                            if (!costumes.TryGetValue(key, out var c)) { c = (new List<string>(), entryName); costumes[key] = c; }
                            if (!c.Mods.Contains(name)) c.Mods.Add(name);
                        }
                    }
                }
            }

            foreach (var (path, list) in files.Where(x => x.Value.Count > 1).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                result.Add(new ModConflict { Type = ConflictType.File, Key = displayPath[path], Mods = list });
            foreach (var (id, s) in songs.Where(x => x.Value.Mods.Count > 1).OrderBy(x => x.Key))
                result.Add(new ModConflict { Type = ConflictType.Song, Key = $"pv_{id:000}", Detail = s.Name, Mods = s.Mods });
            foreach (var (id, m) in modules.Where(x => x.Value.Mods.Count > 1).OrderBy(x => x.Key))
                result.Add(new ModConflict { Type = ConflictType.Module, Key = $"module id {id}", Detail = m.Name, Mods = m.Mods });
            foreach (var (key, c) in costumes.Where(x => x.Value.Mods.Count > 1).OrderBy(x => x.Key))
                result.Add(new ModConflict { Type = ConflictType.ModuleCostume, Key = key, Detail = c.Name, Mods = c.Mods });
            foreach (var (id, i) in items.Where(x => x.Value.Mods.Count > 1).OrderBy(x => x.Key))
                result.Add(new ModConflict { Type = ConflictType.CustomizeItem, Key = $"item id {id}", Detail = i.Name, Mods = i.Mods });

            return result;
        }

        private static void AddUnique(Dictionary<string, List<string>> dic, string key, string mod)
        {
            if (!dic.TryGetValue(key, out var list)) { list = new List<string>(); dic[key] = list; }
            if (!list.Contains(mod)) list.Add(mod);
        }

        /// <summary>DML が読み込む rom 系フォルダ（rom, rom_steam, rom_steam_en など）内のファイル</summary>
        private static IEnumerable<string> EnumerateRomFiles(string modPath)
        {
            IEnumerable<string> romDirs;
            try { romDirs = Directory.EnumerateDirectories(modPath).Where(d => Path.GetFileName(d).StartsWith("rom", StringComparison.OrdinalIgnoreCase)).ToList(); }
            catch (Exception) { yield break; }
            foreach (var dir in romDirs)
            {
                IEnumerable<string> found;
                try { found = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList(); }
                catch (Exception) { continue; }
                foreach (var f in found) yield return f;
            }
        }

        private static IEnumerable<(int Id, string Name)> ReadSongs(string pvDbPath)
        {
            var names = new Dictionary<int, string>();
            string[] lines;
            try { lines = File.ReadAllLines(pvDbPath, Encoding.UTF8); }
            catch (Exception) { yield break; }
            foreach (var line in lines)
            {
                if (!line.StartsWith("pv_")) continue;
                var dot = line.IndexOf('.');
                if (dot < 4 || !int.TryParse(line.AsSpan(3, dot - 3), out var id)) continue;
                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var key = line.Substring(dot + 1, eq - dot - 1);
                if (!names.ContainsKey(id)) names[id] = string.Empty;
                if (key == "song_name_en" || (key == "song_name" && names[id].Length == 0))
                    names[id] = line.Substring(eq + 1);
            }
            foreach (var (id, name) in names) yield return (id, name);
        }

        /// <summary>
        /// Mod 内のモジュール・カスタマイズアイテムのテーブルを読み込む。
        /// 単体ファイル（rom\...\gm_module_id.bin）と FARC（rom\mod_gm_module_tbl.farc など）の両方に対応する
        /// </summary>
        public static IEnumerable<(string FileName, string Source, string Text)> ReadTables(string modPath)
        {
            foreach (var file in EnumerateRomFiles(modPath))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals(MODULE_TABLE, StringComparison.OrdinalIgnoreCase) || fileName.Equals(ITEM_TABLE, StringComparison.OrdinalIgnoreCase))
                {
                    string text;
                    try { text = File.ReadAllText(file, Encoding.UTF8); }
                    catch (Exception) { continue; }
                    yield return (fileName, Path.GetRelativePath(modPath, file), text);
                }
                else if (fileName.EndsWith(".farc", StringComparison.OrdinalIgnoreCase)
                    && (fileName.Contains("gm_module_tbl", StringComparison.OrdinalIgnoreCase) || fileName.Contains("gm_customize_item_tbl", StringComparison.OrdinalIgnoreCase)))
                {
                    var entries = FarcReader.Read(file, n => n.Equals(MODULE_TABLE, StringComparison.OrdinalIgnoreCase) || n.Equals(ITEM_TABLE, StringComparison.OrdinalIgnoreCase));
                    foreach (var entry in entries)
                        yield return (entry.Name, $"{Path.GetRelativePath(modPath, file)}/{entry.Name}", Encoding.UTF8.GetString(entry.Data));
                }
            }
        }

        /// <summary>"module.0.id=997" 形式のテーブルを番号ごとの辞書にする</summary>
        private static IEnumerable<Dictionary<string, string>> ParseTable(string text, string prefix)
        {
            var entries = new SortedDictionary<int, Dictionary<string, string>>();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (!line.StartsWith(prefix + ".")) continue;
                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var keyParts = line.Substring(0, eq).Split('.');
                if (keyParts.Length != 3 || !int.TryParse(keyParts[1], out var n)) continue;
                if (!entries.TryGetValue(n, out var entry)) { entry = new Dictionary<string, string>(); entries[n] = entry; }
                entry[keyParts[2]] = line.Substring(eq + 1);
            }
            return entries.Values;
        }
    }
}
