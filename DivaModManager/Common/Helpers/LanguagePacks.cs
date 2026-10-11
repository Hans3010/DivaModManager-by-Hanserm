using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tomlyn;
using Tomlyn.Model;

namespace DivaModManager.Common.Helpers
{
    /// <summary>
    /// 追加の表示言語（Languages フォルダ）を読み込む。英語と日本語は組み込み。
    /// Languages\&lt;code&gt;\ に language.toml / ui.toml / messages.toml を置くと、言語の選択肢に追加される。
    /// "_" で始まるフォルダ（_template など）は読み込まない。詳しくは TRANSLATING.md を参照。
    /// </summary>
    public static class LanguagePacks
    {
        public const string FOLDER_NAME = "Languages";

        public class Pack
        {
            public string Code { get; init; }
            public string Name { get; init; }
            public string Author { get; init; }
            public Dictionary<string, string> Ui { get; init; } = new();
            public Dictionary<string, Dictionary<string, string>> Messages { get; init; } = new();
        }

        private static readonly Dictionary<string, Pack> packs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>選択肢: 組み込みの英語・日本語と、Languages フォルダの言語</summary>
        public static IReadOnlyList<(string Code, string Name)> Available =>
            new List<(string, string)> { ("EN", "English"), ("JP", "日本語 (Japanese)") }
                .Concat(packs.Values.OrderBy(p => p.Name).Select(p => (p.Code, p.Name)))
                .ToList();

        public static bool IsBuiltIn(string code) =>
            string.IsNullOrEmpty(code) || code.Equals("EN", StringComparison.OrdinalIgnoreCase) || code.Equals("JP", StringComparison.OrdinalIgnoreCase);

        public static Pack Get(string code) => code != null && packs.TryGetValue(code, out var pack) ? pack : null;

        /// <summary>メッセージウィンドウの文言（ない場合は null → 英語を使う）</summary>
        public static string Message(string code, int windowId, string field)
        {
            var pack = Get(code);
            if (pack == null) return null;
            return pack.Messages.TryGetValue(windowId.ToString("WINDOW-0000"), out var fields)
                && fields.TryGetValue(field, out var text) && !string.IsNullOrEmpty(text) ? text : null;
        }

        public static void Load()
        {
            packs.Clear();
            var root = Path.Combine(Global.assemblyLocation, FOLDER_NAME);
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var code = Path.GetFileName(dir);
                if (code.StartsWith("_") || IsBuiltIn(code)) continue;
                try
                {
                    var info = ReadTable(Path.Combine(dir, "language.toml"));
                    var pack = new Pack
                    {
                        Code = code.ToUpperInvariant(),
                        Name = info != null && info.TryGetValue("name", out var n) ? n?.ToString() : code,
                        Author = info != null && info.TryGetValue("author", out var a) ? a?.ToString() : "",
                        Ui = (ReadTable(Path.Combine(dir, "ui.toml")) ?? new TomlTable())
                            .Where(x => x.Value is string)
                            .ToDictionary(x => x.Key, x => (string)x.Value),
                        Messages = (ReadTable(Path.Combine(dir, "messages.toml")) ?? new TomlTable())
                            .Where(x => x.Value is TomlTable)
                            .ToDictionary(x => x.Key, x => ((TomlTable)x.Value).ToDictionary(y => y.Key, y => y.Value?.ToString())),
                    };
                    packs[pack.Code] = pack;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Couldn't load language \"{code}\": {ex.Message}", LoggerType.Warning);
                }
            }
        }

        private static TomlTable ReadTable(string path)
        {
            if (!File.Exists(path)) return null;
            return Toml.ToModel(File.ReadAllText(path));
        }
    }
}
