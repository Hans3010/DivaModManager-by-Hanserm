using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DivaModManager.Features.Setup
{
    public static class Setup
    {
        // called by SetupGame, 
        // MM+とDMLファイルの存在を確認する。DMLは起動時に毎回バージョンチェックする
        public static bool Generic(string exe, string defaultPath)
        {
            // Get install path from registry
            try
            {
                if (OperatingSystem.IsWindows() && !Global.IsWine)
                {
                    var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1761390");
                    if (!string.IsNullOrEmpty(key?.GetValue("InstallLocation") as string))
                        defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}DivaMegaMix.exe";
                    // InstallLocation が空のことがあるので、Steam のライブラリフォルダからも探す
                    if (!File.Exists(defaultPath))
                        defaultPath = FindInSteamLibraries(exe) ?? defaultPath;
                }
                else
                {
                    // Linux (Proton/Wine) 環境: Steam の一般的なパスを確認
                    var linuxPaths = new[]
                    {
                        Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "/home", ".steam", "steam", "steamapps", "common", "Hatsune Miku Project DIVA Mega Mix Plus", "DivaMegaMix.exe"),
                        Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "/home", ".local", "share", "Steam", "steamapps", "common", "Hatsune Miku Project DIVA Mega Mix Plus", "DivaMegaMix.exe"),
                        "/mnt/c/Program Files (x86)/Steam/steamapps/common/Hatsune Miku Project DIVA Mega Mix Plus/DivaMegaMix.exe",
                    };
                    foreach (var path in linuxPaths)
                    {
                        if (File.Exists(path))
                        {
                            defaultPath = path;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Couldn't find install path in registry ({e.Message})", LoggerType.Error);
            }

            string configExePath = Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher;

            // 1番目：Config.json
            if (File.Exists(configExePath))
            {
                defaultPath = configExePath;
            }
            // 2番目：レジストリ
            else if (!File.Exists(defaultPath))
            {
                // 3番目：ユーザーファイル選択
                var resultWindow = WindowHelper.DMMWindowOpenAsync(77);
                OpenFileDialog dialog = new()
                {
                    DefaultExt = ".exe",
                    Filter = $"Executable Files ({exe})|{exe}",
                    Title = $"{exe}を選択してください / Select {exe}",
                    Multiselect = false,
                    InitialDirectory = Global.assemblyLocation
                };
                dialog.ShowDialog();
                if (!string.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals(exe, StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!string.IsNullOrEmpty(dialog.FileName))
                {
                    Logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                {
                    // ファイル選択しなかったら終了
                    return false;
                }
            }

            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher = defaultPath;
            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder = Path.Combine(Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher), "mods");
            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].DMLConfig = Path.Combine(Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher), "config.toml");
            Directory.CreateDirectory(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder);

            if (!Directory.Exists(Global.downloadBaseLocation))
                Directory.CreateDirectory(Global.downloadBaseLocation);
            if (!File.Exists(Global.temporaryWarningFilePath))
                File.Create(Global.temporaryWarningFilePath);

            // Check for DML update
            if (!Global.ConfigJson.CurrentConfig.FirstOpen)
            {
                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModLoaderVersion = null;
                ConfigJson.UpdateConfig();
            }
            return true;
        }

        /// <summary>Steam の libraryfolders.vdf に書かれた各ライブラリの common フォルダからゲームを探す</summary>
        private static string FindInSteamLibraries(string exe)
        {
            try
            {
                var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
                if (string.IsNullOrEmpty(steamPath)) return null;
                var libraries = new List<string> { steamPath };
                var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), @"""path""\s+""([^""]+)"""))
                        libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
                foreach (var library in libraries)
                {
                    var path = Path.Combine(library, "steamapps", "common", "Hatsune Miku Project DIVA Mega Mix Plus", exe);
                    if (File.Exists(path))
                    {
                        Logger.WriteLine($"Found {exe} in Steam library: {path}", LoggerType.Debug);
                        return Path.GetFullPath(path);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Couldn't search Steam libraries ({e.Message})", LoggerType.Warning);
            }
            return null;
        }
    }
}
