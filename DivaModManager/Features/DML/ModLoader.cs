using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager.Features.DML
{
    public static class ModLoader
    {
        public static readonly string CONFIG_TOML_NAME = "config.toml";
        private const int MAX_RETRY = 5;
        private const int RETRY_DELAY_MS = 500;
        public static string CONFIG_TOML_PATH
        {
            get { return Path.Combine(Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder), "config.toml"); }
        }

        /// <summary>
        /// Gets the console setting from config.toml (null if it can't be read)
        /// </summary>
        public static bool? ReadConsoleEnabled()
        {
            try
            {
                if (!File.Exists(CONFIG_TOML_PATH))
                    return null;
                if (!Toml.TryToModel(File.ReadAllText(CONFIG_TOML_PATH), out TomlTable config, out _))
                    return null;
                return config.TryGetValue("console", out var value) && value is bool b && b;
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Couldn't read console setting from {CONFIG_TOML_PATH} ({e.Message})", LoggerType.Warning);
                return null;
            }
        }

        /// <summary>
        /// Changes the console setting in config.toml (other settings are kept)
        /// </summary>
        public static bool WriteConsoleEnabled(bool enabled)
        {
            try
            {
                if (!File.Exists(CONFIG_TOML_PATH))
                {
                    Logger.WriteLine($"Unable to find {CONFIG_TOML_PATH}", LoggerType.Error);
                    return false;
                }
                if (!Toml.TryToModel(File.ReadAllText(CONFIG_TOML_PATH), out TomlTable config, out _))
                {
                    Logger.WriteLine($"Couldn't parse {CONFIG_TOML_PATH}", LoggerType.Error);
                    return false;
                }
                config["console"] = enabled;
                File.WriteAllText(CONFIG_TOML_PATH, Toml.FromModel(config));
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Couldn't write console setting to {CONFIG_TOML_PATH} ({e.Message})", LoggerType.Error);
                return false;
            }
        }

        /// <summary>
        /// DML読み込み用のconfig.tomlを更新
        /// </summary>
        /// <param name="callerMethodName"></param>
        /// <returns></returns>
        public static async Task<bool> BuildAsync([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            if (!File.Exists(CONFIG_TOML_PATH))
            {
                Logger.WriteLine($"BuildAsync Unable to find {CONFIG_TOML_PATH}", LoggerType.Error);
                Logger.WriteLine($"BuildAsync Unable to find {CONFIG_TOML_PATH} id:{Environment.CurrentManagedThreadId}", LoggerType.Debug);
                return false;
            }
            // The previous implementation had the retry check inverted: even when reading succeeded after a brief lock,
            // it returned false and config.toml wasn't updated (mods weren't applied)
            string configString = null;
            for (var retryCnt = 0; retryCnt < MAX_RETRY; retryCnt++)
            {
                try
                {
                    configString = File.ReadAllText(CONFIG_TOML_PATH);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(RETRY_DELAY_MS);
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"Couldn't access {CONFIG_TOML_PATH} ({e.Message})", LoggerType.Error);
                    return false;
                }
            }
            if (configString == null)
            {
                var paramStr = $"caller:{caller} retry:{MAX_RETRY} id:{Environment.CurrentManagedThreadId}";
                Logger.WriteLine($"Couldn't read {CONFIG_TOML_PATH}", LoggerType.Error, param: paramStr);
                return false;
            }
            if (!Toml.TryToModel(configString, out TomlTable config, out var diagnostics))
            {
                if (File.Exists(CONFIG_TOML_PATH))
                {
                    Logger.WriteLine($"Unexpected error loading {CONFIG_TOML_NAME}. Using default config.", LoggerType.Error);
                    var backupFilePath = FileHelper.CopyFile(CONFIG_TOML_PATH, IsOriginalFileDelete: true);
                    Logger.WriteLine($"Save the current {CONFIG_TOML_NAME} and generate a new one. Save file name:{new FileInfo(backupFilePath).Name}", LoggerType.Error);
                }
                // Create a new config if it failed to parse
                config = new()
                {
                    { "enabled", true },
                    { "console", false },
                    { "mods", "mods" },
                };
            }
            var priorityList = new List<string>();
            foreach (var mod in Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Loadouts[Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].CurrentLoadout].Where(x => x.enabled).ToList())
                priorityList.Add(mod.name);
            config["priority"] = priorityList.ToArray();
            // The previous implementation waited 500 ms after every successful write, and looped forever on repeated IOExceptions
            // (a temp file + Move isn't used because it fires MMPWatcherTOML's Renamed event)
            var isReady = false;
            var tomlText = Toml.FromModel(config);
            for (var retryCnt = 0; retryCnt < MAX_RETRY && !isReady; retryCnt++)
            {
                try
                {
                    File.WriteAllText(CONFIG_TOML_PATH, tomlText);
                    isReady = true;
                }
                catch (IOException)
                {
                    await Task.Delay(RETRY_DELAY_MS);
                }
                catch (Exception e)
                {
                    var paramStr = $"caller:{caller} retry:{retryCnt} id:{Environment.CurrentManagedThreadId}";
                    Logger.WriteLine($"Couldn't access {CONFIG_TOML_PATH} ({e.Message})", LoggerType.Error, param: paramStr);
                    break;
                }
            }
            if (!isReady)
            {
                Logger.WriteLine($"Couldn't write {CONFIG_TOML_PATH}. Mod changes may not be applied.", LoggerType.Error);
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"Result:{isReady}"), LoggerType.Debug, param: ParamInfo);
            return isReady;
        }
    }
}
