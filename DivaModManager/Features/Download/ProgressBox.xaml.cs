using DivaModManager.Features.Download;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Shell;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for ProgressBox.xaml
    /// </summary>
    public partial class ProgressBox : Window
    {
        private CancellationTokenSource cancellationTokenSource;
        private readonly PauseTokenSource pauseTokenSource = new();
        public bool finished = false;
        public ProgressBox(CancellationTokenSource cancellationTokenSource)
        {
            InitializeComponent();
            this.cancellationTokenSource = cancellationTokenSource;
        }

        /// <summary>
        /// Shows the pause button and returns the PauseToken passed to the download
        /// </summary>
        public PauseToken EnablePause()
        {
            if (PauseButton.Visibility != Visibility.Visible)
            {
                PauseButton.Visibility = Visibility.Visible;
                Height += 40;
            }
            return pauseTokenSource.Token;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (pauseTokenSource.IsPaused)
            {
                pauseTokenSource.Resume();
                PauseButton.Content = Common.Helpers.UiTranslator.T("Pause");
                taskBarItem.ProgressState = TaskbarItemProgressState.Normal;
            }
            else
            {
                pauseTokenSource.Pause();
                PauseButton.Content = Common.Helpers.UiTranslator.T("Resume");
                taskBarItem.ProgressState = TaskbarItemProgressState.Paused;
            }
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }
    }
}
