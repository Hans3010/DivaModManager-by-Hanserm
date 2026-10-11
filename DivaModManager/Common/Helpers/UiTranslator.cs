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
            if (Global.ConfigToml?.Language != ConfigTomlDmm.Lang.ES.ToString()) return;
            current = Spanish;
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded), true);
        }

        /// <summary>コードから設定する文字列用。翻訳がなければそのまま返す</summary>
        public static string T(string english)
        {
            if (current == null || english == null) return english;
            if (current.TryGetValue(english, out var t)) return t;
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

        private static readonly Dictionary<string, string> Spanish = new()
        {
            // Pestañas y barra principal
            ["Mods"] = "Mods",
            ["Option"] = "Opciones",
            ["Module"] = "Módulos",
            ["Song"] = "Canciones",
            ["Loadouts"] = "Loadouts",
            ["Setup"] = "Configurar",
            ["Launch"] = "Jugar",
            ["Executable"] = "Ejecutable",
            ["Steam"] = "Steam",
            ["Console: Off"] = "Consola: No",
            ["Console: On"] = "Consola: Sí",
            ["Core Update"] = "Actualizar",
            ["Updates to DivaModLoader and DivaModManager by Hanserm."] = "Busca actualizaciones de DivaModLoader y DivaModManager by Hanserm.",
            ["Show the DivaModLoader console window when the game starts (useful to see mod logs and errors)."] = "Muestra la consola de DivaModLoader al abrir el juego (sirve para ver los registros y errores de los mods).",
            ["Search..."] = "Buscar...",
            ["Name & Note"] = "Nombre y nota",
            ["Name"] = "Nombre",
            ["Note"] = "Nota",
            ["Enabled"] = "Activados",
            ["True"] = "Sí",
            ["False"] = "No",
            ["Search Target Column"] = "Columna donde buscar",
            ["Enabled Filter"] = "Filtro de activados",
            ["Category Filter"] = "Filtro de categoría",
            ["ScreenShot"] = "Captura",
            ["Conflicts"] = "Conflictos",
            ["Find enabled mods that overwrite each other (same files, song IDs or module IDs)."] = "Busca mods activados que se pisan entre sí (mismos archivos, IDs de canciones o de módulos).",
            ["Drop files to install mod"] = "Suelta archivos aquí para instalar el mod",

            // Menú contextual de la lista de mods
            ["Configure Mod"] = "Configurar mod",
            ["Open Mod Folder"] = "Abrir carpeta del mod",
            ["Open HomePage"] = "Abrir página web",
            ["Fetch Metadata"] = "Obtener información",
            ["Check Content"] = "Revisar contenido",
            ["Clean Update Mod"] = "Actualizar mod (limpio)",
            ["Rename Mod Folder"] = "Renombrar carpeta del mod",
            ["Delete Mod"] = "Borrar mod",
            ["Move To Top"] = "Mover arriba de todo",
            ["Move To Bottom"] = "Mover abajo de todo",
            ["Open DMM Folder"] = "Abrir carpeta de DMM",
            ["Open MM+ Folder"] = "Abrir carpeta del juego",
            ["Open Version Info"] = "Ver información de versión",
            ["Setting Files Open"] = "Abrir archivos de configuración",

            // Navegadores de mods
            ["More Info"] = "Más info",
            ["Download"] = "Descargar",
            ["Cache Use"] = "Usar caché",
            ["Clear Cache"] = "Borrar caché",
            [" Favorites"] = " Favoritos",
            ["Favorites"] = "Favoritos",
            ["Show only your favorite mods"] = "Mostrar solo tus mods favoritos",
            ["✔ Installed"] = "✔ Instalado",
            ["Add to / remove from favorites"] = "Agregar a / quitar de favoritos",
            ["Go to gamebanana.com"] = "Ir a gamebanana.com",
            ["Go to divamodarchive.com"] = "Ir a divamodarchive.com",
            ["Go to GitHub"] = "Ir a GitHub",
            ["Go to GitHub Sponsors"] = "Apoyar en GitHub Sponsors",
            ["Join the Discord!"] = "¡Únete al Discord!",
            ["Page 1"] = "Página 1",
            ["Alt. Downloads"] = "Descargas alternativas",
            ["Multiple downloads were found, choose one:"] = "Se encontraron varias descargas, elige una:",
            ["(Checking file size...)"] = "(Revisando tamaño...)",
            ["Would you like to install?"] = "¿Quieres instalarlo?",
            ["Would you like to update?"] = "¿Quieres actualizarlo?",
            ["Skip Update"] = "Omitir actualización",
            ["Changelog"] = "Cambios",
            ["Preview"] = "Vista previa",
            ["Caution"] = "Atención",

            // Pestaña Módulos
            ["Mod Filter"] = "Filtro de mod",
            ["Type Filter"] = "Filtro de tipo",
            ["Chara Filter"] = "Filtro de personaje",
            ["Key Filter"] = "Filtro de clave",
            ["Conflict Filter"] = "Filtro de conflictos",

            // Pestaña Opciones
            ["One-Click Install"] = "Instalación con un clic",
            ["Settings BackUp"] = "Respaldar configuración",

            // Ventanas
            ["Yes"] = "Sí",
            ["No"] = "No",
            ["OK"] = "OK",
            ["Cancel"] = "Cancelar",
            [" Cancel"] = " Cancelar",
            ["Close"] = "Cerrar",
            [" Close"] = " Cerrar",
            ["Retry"] = "Reintentar",
            ["Confirm"] = "Confirmar",
            ["Browse"] = "Examinar",
            [" Create"] = " Crear",
            [" Save"] = " Guardar",
            ["Don't show this message again"] = "No volver a mostrar este mensaje",
            ["Pause"] = "Pausa",
            ["Resume"] = "Continuar",
            ["Mod Conflicts"] = "Conflictos entre mods",
            ["Scan again"] = "Revisar de nuevo",
            ["Fix thumbnails"] = "Arreglar miniaturas",
            ["Move the overridden pack's song thumbnails to their own sprite set so both packs show their thumbnails. Backups are kept."] = "Mueve las miniaturas del pack que pierde a su propio set, para que los dos packs muestren sus miniaturas. Se guardan respaldos.",
            ["Type"] = "Tipo",
            ["Conflict"] = "Conflicto",
            ["Winner (used)"] = "Gana (se usa)",
            ["Overridden"] = "Queda tapado",
            ["When two enabled mods contain the same file, song ID or module ID, only the mod higher in the list is used (Winner). To choose a different winner, move that mod up in the mod list. Song packs that share thumbnail files, or module mods that share an ID, show missing images or costumes in game."] =
                "Cuando dos mods activados tienen el mismo archivo, ID de canción o ID de módulo, solo se usa el que está más arriba en la lista (Gana). Para elegir otro, súbelo en la lista de mods. Los packs que comparten archivos de miniaturas, o los mods de módulos con el mismo ID, hacen que falten imágenes o trajes en el juego.",
        };

        /// <summary>コードで組み立てる文言（競合ウィンドウなど）</summary>
        public static string Format(string english, params object[] args) => string.Format(T(english), args);
    }
}
