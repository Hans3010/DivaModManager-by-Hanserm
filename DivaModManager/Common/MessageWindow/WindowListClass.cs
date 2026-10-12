using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Tomlyn;

namespace DivaModManager.Common.MessageWindow
{
    public static class WindowList
    {
        public static string WINDOW_LIST_NAME { get; set; } = "Message.toml";
        public static string WINDOW_LIST_PATH = $"{Global.assemblyLocation}{WINDOW_LIST_NAME}";
        public static string WINDOW_LIST_RESOURCE_PATH = $"DivaModManager.Resources.Message.toml";

        public static Dictionary<string, string> WINDOW_LIST_NO_58 = new()
        {
            { "https://gamebanana.com/mods/434457", DMLUpdater.MODULE_NAME },   // Eden Module ID Patch
            { "https://gamebanana.com/mods/432449", DMLUpdater.MODULE_NAME },   // Eden Song ID Patch
            { "https://gamebanana.com/mods/397695", DMLUpdater.MODULE_NAME },   // Eden Song Limit Patch
            { "https://gamebanana.com/mods/427167", "Eden Core" },              // Eden Project LITE Version
            { "https://gamebanana.com/mods/438186", Global.GAME_NAME },         // Future Tone Customization - Dynamic Version
        };
        public static List<string> WINDOW_LIST_NO_59 = new()
        {
            "https://gamebanana.com/mods/405848",       // Eden Project [v5.6]
            "https://gamebanana.com/mods/397695",       // Eden Song Limit Patch
        };


        public static Dictionary<string, WindowInfo> WindowDictionary { get; set; } = new();

        public static bool InitWindowList([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Global.IsMainWindowLoaded:{Global.IsMainWindowLoaded}, Global.IsModGridLoaded:{Global.IsModGridLoaded}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            Dictionary<string, WindowInfo> windowListToml = new();
            bool ret = false;

            try
            {
                var content = string.Empty;
                ret = LoadWindowMessageTomlFromResource();
            }
            catch (Exception ex)
            {
                ret = false;
                Logger.WriteLine(string.Join(" ", $"Initialization was performed because {WINDOW_LIST_PATH} could not be read.", $"ex.Message:{ex.Message}", $"ex.StackTrace:{ex.StackTrace}"), LoggerType.Error, param: ParamInfo);
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End.", $"WindowList.Count:{WindowDictionary.Count()} Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        public static bool LoadWindowMessageTomlFromResource([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            var ret = false;

            var assenbly = Assembly.GetExecutingAssembly();
            using Stream stream = assenbly.GetManifestResourceStream(WINDOW_LIST_RESOURCE_PATH);
            if (stream == null)
            {
                // Message.tomlが見つからない場合はアプリケーションを再度ダウンロードしてもらう
                MessageBox.Show("Message.tomlの読み込みに失敗しました。\nアプリケーションを再度ダウンロードしてください。\n\nFailed to load Message.toml\nPlease download the application again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                using StreamReader reader = new(stream);
                var content = reader.ReadToEnd();

                WindowDictionary = Toml.ToModel<Dictionary<string, WindowInfo>>(content);
                ret = true;
            }

            Logger.WriteLine(string.Join(" ", MeInfo, $"End. Return:{ret}"), LoggerType.Debug, param: ParamInfo);
            return ret;
        }

        // "none"かreplaceが指定されていない{0}はブランクとして扱う
        public static Regex regex = new(@"^(none|\{\d+\}$)");

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="replace"></param>
        /// <returns></returns>
        public static string ReplaceMessage(string message, List<string> replace)
        {
            var text = message;
            if (replace == null || replace.Count == 0)
            {
                text = regex.Replace(text, "");
                return text;
            }

            int numMax = 0;
            var matches = Regex.Matches(text, "{\\d+}");

            var numList = new List<int>();
            foreach (var match in matches)
            {
                numList.Add(int.Parse(match.ToString().Replace("{", "").Replace("}", "")));
            }
            if (numList.Count > 0)
                numMax = numList.ToList().Max(x => x) + 1;
            else
                // 置き換える {0} がなくても "none"（空欄の印）は消す
                return regex.Replace(message, "");
            for (var n = 0; n < numMax; n++)
            {
                var rep = "{" + n + "}";
                text = text.Replace(rep, replace[n]);
            }
            return text;
        }

        #region ダミーデータ
        public static string WindowListDummyData()
        {
            Dictionary<string, WindowInfo> dataList = new();
            string key = 1.ToString("MESSAGE-0000");
            var data = new WindowInfo()
            {
                ID = 1,
                Info_JP = "info_jp",
                Info_EN = "info_en",
                Context_JP = "Config.tomlがない",
                Context_EN = "No Config.toml",
                WindowTitle_JP = "No Config.toml_JP",
                WindowTitle_EN = "No Config.toml_EN",
            };
            dataList.Add(key, data);

            data = new()
            {
                ID = 2,
                Info_JP = "info_jp",
                Info_EN = "info_en",
                Context_JP = "Context_jp",
                Context_EN = "Context_en",
                WindowTitle_JP = "Multi Config.toml",
                WindowTitle_EN = "Multi Config.toml",
            };
            key = 2.ToString("MESSAGE-0000");
            dataList.Add(key, data);

            data = new()
            {
                ID = 3,
                Info_JP = "info_jp",
                Info_EN = "info_en",
                Context_JP = "Context_jp",
                Context_EN = "Context_en",
                WindowTitle_JP = "Nest Directory JP",
                WindowTitle_EN = "Nest Directory EN",
            };
            key = 3.ToString("MESSAGE-0000");
            dataList.Add(key, data);

            var ret = Toml.FromModel(dataList);

            return ret;
        }
        #endregion ダミーデータ

        public static WindowInfo MessageWindowNo(int ID)
        {
            var key = ID.ToString("WINDOW-0000");
            return WindowDictionary[key].DeepCopy();    // windowListはstaticなので、コピーして別インスタンスにする
        }

        public static WindowInfo MessageWindowNo(int ID, List<string> replaceList)
        {
            var key = ID.ToString("WINDOW-0000");
            var info = WindowDictionary[key].DeepCopy();    // windowListはstaticなので、コピーして別インスタンスにする
            info.replaceList = replaceList;

            return info;
        }

        public static bool Save()
        {
            var ret = false;
            try
            {
                string tomlString = Toml.FromModel(WindowDictionary);
                ret = FileHelper.TryWriteAllText(WINDOW_LIST_PATH, tomlString);
            }
            catch (Exception ex)
            {
                string MeInfo = Logger.GetMeInfo(new StackFrame());
                Logger.WriteLine(string.Join(" ", $"Unexpected error loading {WINDOW_LIST_PATH}: {ex.Message}. Using default {WINDOW_LIST_PATH} in {AssemblyName.GetAssemblyName}."), LoggerType.Error);
            }
            return ret;
        }

        public static string MessageString(int ID, [CallerMemberName] string caller = "")
        {
            var key = ID.ToString("WINDOW-0000");
            var info = WindowDictionary[key].DeepCopy();    // windowListはstaticなので、コピーして別インスタンスにする

            return info.Info();
        }
    }
    public class WindowInfo
    {
        public enum TYPE
        {
            NONE = 0,
            INFORMATION,
            WARNING,
            ERROR,
            ATTENTION,
        }
        public enum MESSAGE_WINDOW
        {
            None = 0,
            OK,
            OKCheck,
            YesNo,
            YesNoCheck,
            YesNoOpen,
            Choice,
            Metadata,
            MessageBox,
            InfoOK,
        }
        [DataMember(Name = "id")]
        public int ID { get; set; }
        [IgnoreDataMember]
        private TYPE _Type { get; set; }
        [DataMember(Name = "type")]
        public TYPE Type
        {
            get { return _Type; }
            set { _Type = Enum.Parse<TYPE>(value.ToString().ToUpper().AsSpan(), true); }
        }
        [IgnoreDataMember]
        public List<string> replaceList { get; set; } = new();
        [DataMember(Name = "info_jp")]
        public string Info_JP { get; set; }
        [DataMember(Name = "info_en")]
        public string Info_EN { get; set; }
        /// <summary>設定言語の文言を選ぶ。英語・日本語以外は Languages フォルダの messages.toml、ない場合は英語</summary>
        private string Pick(string field, string jp, string en)
        {
            var lang = Global.ConfigToml?.Language;
            if (lang == ConfigTomlDmm.Lang.JP.ToString()) return jp;
            if (!LanguagePacks.IsBuiltIn(lang)) return LanguagePacks.Message(lang, ID, field) ?? en;
            return en;
        }
        public string Info() => WindowList.ReplaceMessage(Pick("info", Info_JP, Info_EN), replaceList);
        [DataMember(Name = "context_jp")]
        public string Context_JP { get; set; }
        [DataMember(Name = "context_en")]
        public string Context_EN { get; set; }
        public string Context() => WindowList.ReplaceMessage(Pick("context", Context_JP, Context_EN), replaceList);

        // MessageWindow
        [DataMember(Name = "window_type")]
        public string WindowType { get; set; }
        [DataMember(Name = "window_title_jp")]
        public string WindowTitle_JP { get; set; }
        [DataMember(Name = "window_title_en")]
        public string WindowTitle_EN { get; set; }
        public string WindowTitle() => WindowList.ReplaceMessage(Pick("window_title", WindowTitle_JP, WindowTitle_EN), replaceList);
        // button_1
        [DataMember(Name = "button_1_jp")]
        public string Button_1_JP { get; set; }
        [DataMember(Name = "button_1_en")]
        public string Button_1_EN { get; set; }
        public string Button_1() => Pick("button_1", Button_1_JP, Button_1_EN);
        // button_2
        [DataMember(Name = "button_2_jp")]
        public string Button_2_JP { get; set; }
        [DataMember(Name = "button_2_en")]
        public string Button_2_EN { get; set; }
        public string Button_2() => Pick("button_2", Button_2_JP, Button_2_EN);
        // button_3
        [DataMember(Name = "button_3_jp")]
        public string Button_3_JP { get; set; }
        [DataMember(Name = "button_3_en")]
        public string Button_3_EN { get; set; }
        public string Button_3() => Pick("button_3", Button_3_JP, Button_3_EN);
        // check_1
        [DataMember(Name = "check_1_jp")]
        public string Check_1_JP { get; set; }
        [DataMember(Name = "check_1_en")]
        public string Check_1_EN { get; set; }
        public string Check_1() => WindowList.ReplaceMessage(Pick("check_1", Check_1_JP, Check_1_EN), replaceList);

        public string WindowId() { return $"WINDOW-{ID.ToString("0000")}"; }

        public override string ToString()
        {
            return $"[{WindowId()}]\n{Info()}\n{Context()}\n\n";
        }
    }
}
