using DivaModManager.Common.Config;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace DivaModManager.Common.Helpers
{
    /// <summary>
    /// 画面の固定テキスト（ボタン・タブ・チェックボックス・ツールチップなど）を英語から翻訳する。
    /// 全要素の Loaded イベントで翻訳するので、ほかのウィンドウや DataTemplate 内の要素にも適用される。
    /// バインディングされた値（Mod 名など）や、コードが文字列で判定しているコントロールは翻訳しない。
    /// </summary>
    public static class UiTranslator
    {
        // コードが表示文字列で判定しているので翻訳しないコンボボックス
        private static readonly HashSet<string> ExcludedComboBoxes = new() { "VisibleColumnComboBox", "GBTypeBox" };

        private static Dictionary<string, string> current;

        public static bool IsActive => current != null;

        /// <summary>設定言語に応じて翻訳を有効にする（App 起動時に1回呼ぶ）</summary>
        public static void Initialize()
        {
            // 英語・日本語以外は Languages\<code>\ui.toml の翻訳を使う
            var pack = LanguagePacks.Get(Global.ConfigToml?.Language);
            if (pack == null || pack.Ui.Count == 0) return;
            current = pack.Ui;
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded), true);
        }

        /// <summary>コードから設定する文字列用。翻訳がなければそのまま返す</summary>
        public static string T(string english)
        {
            if (current == null || english == null) return english;
            if (current.TryGetValue(english, out var t) && !string.IsNullOrEmpty(t)) return t;
            var trimmed = english.Trim();
            if (trimmed.Length != english.Length && current.TryGetValue(trimmed, out t))
                return english.Replace(trimmed, t);
            return english;
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                try { Translate(element); }
                catch (Exception) { /* 翻訳の失敗で画面を壊さない */ }
            }
        }

        private static bool IsLiteral(DependencyObject obj, DependencyProperty property) =>
            BindingOperations.GetBindingExpressionBase(obj, property) == null;

        private static void Translate(FrameworkElement element)
        {
            // 列ヘッダーの文字列はコードで判定に使われているので翻訳しない
            if (element is System.Windows.Controls.Primitives.DataGridColumnHeader) return;

            if (element.ToolTip is string tip && IsLiteral(element, FrameworkElement.ToolTipProperty))
                element.ToolTip = T(tip);

            switch (element)
            {
                case Window window when IsLiteral(window, Window.TitleProperty):
                    window.Title = T(window.Title);
                    break;
                case HeaderedContentControl headered when headered.Header is string header && IsLiteral(headered, HeaderedContentControl.HeaderProperty):
                    headered.Header = T(header);
                    break;
                case HeaderedItemsControl headeredItems when headeredItems.Header is string itemsHeader && IsLiteral(headeredItems, HeaderedItemsControl.HeaderProperty):
                    headeredItems.Header = T(itemsHeader);
                    break;
                case ComboBoxItem comboItem:
                    TranslateComboBoxItem(comboItem);
                    break;
                case ContentControl content when content.Content is string text && IsLiteral(content, ContentControl.ContentProperty):
                    content.Content = T(text);
                    break;
                case TextBlock textBlock when !(textBlock.TemplatedParent is ContentPresenter):
                    TranslateTextBlock(textBlock);
                    break;
            }
        }

        private static void TranslateComboBoxItem(ComboBoxItem item)
        {
            if (item.Content is not string text || !IsLiteral(item, ContentControl.ContentProperty)) return;
            var owner = ItemsControl.ItemsControlFromItemContainer(item);
            if (owner == null || ExcludedComboBoxes.Contains(owner.Name)) return;
            // コードで生成された項目（データそのもの）は翻訳しない。XAML で書かれた項目だけ翻訳する
            if (!ReferenceEquals(owner.ItemContainerGenerator.ItemFromContainer(item), item)) return;
            item.Content = T(text);
        }

        private static void TranslateTextBlock(TextBlock textBlock)
        {
            if (!IsLiteral(textBlock, TextBlock.TextProperty)) return;
            if (textBlock.Inlines.Count <= 1)
            {
                var text = textBlock.Text;
                var translated = T(text);
                if (!ReferenceEquals(text, translated) && text != translated) textBlock.Text = translated;
                return;
            }
            foreach (var inline in textBlock.Inlines)
            {
                if (inline is Run run && IsLiteral(run, Run.TextProperty))
                {
                    var translated = T(run.Text);
                    if (translated != run.Text) run.Text = translated;
                }
            }
        }

        /// <summary>コードで組み立てる文言（競合ウィンドウなど）</summary>
        public static string Format(string english, params object[] args) => string.Format(T(english), args);
    }
}
