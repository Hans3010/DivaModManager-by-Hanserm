using DivaModManager.Common.Helpers;
using System.Windows;
using System.Windows.Input;

namespace DivaModManager.Common.MessageWindow
{
    /// <summary>
    /// Interaction logic for DmmInfoWindowOK.xaml
    /// </summary>
    public partial class DmmInfoWindowOK : Window
    {
        public bool OK = false;
        public bool IsCancel = true;
        private string linkUrl;

        /// <summary>2つ目のボタンでリンク（パッチノートなど）を開けるようにする</summary>
        public void ShowLinkButton(string text, string url)
        {
            linkUrl = url;
            Button_2.Content = text;
            Button_2.Visibility = Visibility.Visible;
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(linkUrl)) ProcessHelper.TryStartProcess(linkUrl);
        }

        public DmmInfoWindowOK(string strInfo, string strText, string title, bool ok = false)
        {
            InitializeComponent();

            InfoText.Text = strInfo;
            if (string.IsNullOrEmpty(InfoText.Text)) InfoText.Visibility = Visibility.Collapsed;
            OK = ok;
            Title = title;

            Activate();
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            OK = true;
            IsCancel = false;
            Close();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OK = false;
                IsCancel = true;
                Close();
            }
        }
    }
}
