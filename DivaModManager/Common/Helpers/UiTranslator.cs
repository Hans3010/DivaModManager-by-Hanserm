using DivaModManager.Common.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace DivaModManager.Common.Helpers
{
    /// <summary>
    /// Translates the fixed texts on screen (buttons, tabs, check boxes, tooltips, etc.) from English.
    /// Each element is translated on its first layout (SizeChanged), so it also works in other windows and inside DataTemplates.
    /// (A Loaded class handler isn't used because it only reaches elements that have their own Loaded handler)
    /// Bound values (mod names, etc.) and controls whose text the code compares aren't translated.
    /// </summary>
    public static class UiTranslator
    {
        // Combo boxes the code compares by their displayed text, so they aren't translated
        private static readonly HashSet<string> ExcludedComboBoxes = new() { "GBTypeBox" };

        private static Dictionary<string, string> current;

        // Elements already translated (later SizeChanged events do nothing)
        private static readonly ConditionalWeakTable<FrameworkElement, object> translated = new();

        private static readonly MethodInfo UpdateSelectionBoxItem =
            typeof(ComboBox).GetMethod("UpdateSelectionBoxItem", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool IsActive => current != null;

        /// <summary>Turns translation on for the configured language (called once when the app starts)</summary>
        public static void Initialize()
        {
            // Languages other than English and Japanese use the translations in Languages\<code>\ui.toml
            var pack = LanguagePacks.Get(Global.ConfigToml?.Language);
            if (pack == null || pack.Ui.Count == 0) return;
            current = pack.Ui;
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnSizeChanged), true);
        }

        /// <summary>For strings set from code. Returns the text unchanged if there's no translation</summary>
        public static string T(string english)
        {
            if (current == null || english == null) return english;
            if (current.TryGetValue(english, out var t) && !string.IsNullOrEmpty(t)) return t;
            var trimmed = english.Trim();
            if (trimmed.Length != english.Length && current.TryGetValue(trimmed, out t))
                return english.Replace(trimmed, t);
            return english;
        }

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement element && !translated.TryGetValue(element, out _))
            {
                translated.Add(element, null);
                try { Translate(element); }
                catch (Exception) { /* a failed translation must not break the screen */ }
            }
        }

        private static bool IsLiteral(DependencyObject obj, DependencyProperty property) =>
            BindingOperations.GetBindingExpressionBase(obj, property) == null;

        private static void Translate(FrameworkElement element)
        {
            // Column header texts are used by the code, so they aren't translated
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
                case ComboBox comboBox:
                    TranslateComboBox(comboBox);
                    break;
                case ComboBoxItem comboItem:
                    TranslateComboBoxItem(comboItem, ItemsControl.ItemsControlFromItemContainer(comboItem));
                    break;
                case ContentControl content when content.Content is string text && IsLiteral(content, ContentControl.ContentProperty):
                    content.Content = T(text);
                    break;
                case TextBlock textBlock when !(textBlock.TemplatedParent is ContentPresenter):
                    TranslateTextBlock(textBlock);
                    break;
            }
        }

        /// <summary>Translates the XAML items first and refreshes the shown item, so closed combo boxes show the translation too</summary>
        private static void TranslateComboBox(ComboBox comboBox)
        {
            if (ExcludedComboBoxes.Contains(comboBox.Name)) return;
            var changed = false;
            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Content is string before)
                {
                    // ComboBoxItems in Items are the items themselves, created in XAML (or code)
                    TranslateComboBoxItem(comboItem, comboBox, declared: true);
                    changed |= !ReferenceEquals(before, comboItem.Content);
                }
            }
            if (changed && comboBox.SelectedItem != null) UpdateSelectionBoxItem?.Invoke(comboBox, null);
        }

        private static void TranslateComboBoxItem(ComboBoxItem item, ItemsControl owner, bool declared = false)
        {
            if (item.Content is not string text || !IsLiteral(item, ContentControl.ContentProperty)) return;
            if (owner == null || ExcludedComboBoxes.Contains(owner.Name)) return;
            // Items generated by code (the data itself) aren't translated. Only items written in XAML are
            if (!declared && !ReferenceEquals(owner.ItemContainerGenerator.ItemFromContainer(item), item)) return;
            var t = T(text);
            if (t != text) item.Content = t;
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

        /// <summary>Text built in code (conflict window, etc.)</summary>
        public static string Format(string english, params object[] args) => string.Format(T(english), args);
    }
}
