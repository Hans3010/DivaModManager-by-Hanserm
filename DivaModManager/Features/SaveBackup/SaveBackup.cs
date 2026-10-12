using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DivaModManager.Features.Saves
{
    /// <summary>
    /// Mega Mix+ のセーブデータ（%APPDATA%\SEGA\Project DIVA MEGA39's）を zip で保存・復元する。
    /// ゲームを起動するたびに、前回のバックアップから変わっていれば新しく保存する。
    /// </summary>
    public static class SaveBackup
    {
        public const int KEEP = 15;

        public static string SaveFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SEGA", "Project DIVA MEGA39's");
        public static string BackupFolder => Path.Combine(Global.assemblyLocation, "Backups", "Saves");

        public class Entry
        {
            public string Path { get; init; }
            public DateTime Date { get; init; }
            public string Display => Date.ToString("yyyy-MM-dd HH:mm:ss");
            public override string ToString() => Display;
        }

        public static List<Entry> List()
        {
            if (!Directory.Exists(BackupFolder)) return new List<Entry>();
            return Directory.GetFiles(BackupFolder, "save_*.zip")
                .Select(f => new Entry { Path = f, Date = File.GetLastWriteTime(f) })
                .OrderByDescending(e => e.Date)
                .ToList();
        }

        /// <summary>セーブデータが前回のバックアップより新しければ保存する。保存したら true</summary>
        public static bool BackupIfChanged()
        {
            try
            {
                if (!Directory.Exists(SaveFolder)) return false;
                var files = Directory.GetFiles(SaveFolder, "*", SearchOption.AllDirectories);
                if (files.Length == 0) return false;
                var newest = files.Max(File.GetLastWriteTime);
                var last = List().FirstOrDefault();
                if (last != null && last.Date >= newest) return false;
                Create("save");
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Couldn't back up the save data: {ex.Message}", LoggerType.Warning);
                return false;
            }
        }

        public static string Create(string prefix)
        {
            Directory.CreateDirectory(BackupFolder);
            var zip = Path.Combine(BackupFolder, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(SaveFolder, zip, CompressionLevel.Optimal, includeBaseDirectory: false);
            Logger.WriteLine($"Save data backed up to {zip}", LoggerType.Info);
            Prune();
            return zip;
        }

        /// <summary>
        /// バックアップを復元する。先に今のセーブデータもバックアップしておく（restore_ で始まる名前）。
        /// ゲームが起動していないことを呼び出し側で確認すること
        /// </summary>
        public static void Restore(Entry entry)
        {
            if (Directory.Exists(SaveFolder) && Directory.EnumerateFiles(SaveFolder, "*", SearchOption.AllDirectories).Any())
                Create("save_before_restore");
            Directory.CreateDirectory(SaveFolder);
            ZipFile.ExtractToDirectory(entry.Path, SaveFolder, overwriteFiles: true);
            Logger.WriteLine($"Save data restored from {entry.Path}", LoggerType.Info);
        }

        /// <summary>古いバックアップを消して KEEP 個だけ残す</summary>
        private static void Prune()
        {
            foreach (var old in List().Skip(KEEP))
            {
                try { File.Delete(old.Path); }
                catch (Exception) { }
            }
        }
    }
}
