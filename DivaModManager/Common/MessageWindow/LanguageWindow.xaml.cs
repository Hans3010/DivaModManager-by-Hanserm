using DivaModManager.Common.Helpers;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DivaModManager.Common.MessageWindow
{
    /// <summary>
    /// Display language choice on first launch (it used to be a message window with two buttons, Japanese / English)
    /// </summary>
    public partial class LanguageWindow : Window
    {
        public string SelectedLanguage { get; private set; } = "EN";
        public bool DontShowAgain { get; private set; } = true;

        public LanguageWindow(string currentLanguage = null)
        {
            InitializeComponent();
            foreach (var (code, name) in LanguagePacks.Available)
                LanguageComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            LanguageComboBox.SelectedIndex = 0;
            var current = LanguageComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == currentLanguage);
            if (current != null) LanguageComboBox.SelectedItem = current;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "EN";
            DontShowAgain = DontShowAgainCheckBox.IsChecked == true;
            DialogResult = true;
        }
    }
}
