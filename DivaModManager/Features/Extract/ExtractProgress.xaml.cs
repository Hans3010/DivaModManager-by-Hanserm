using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Shell;

#nullable enable

namespace DivaModManager.Features.Extract
{
    /// <summary>
    /// Interaction logic for ProgressBox.xaml
    /// </summary>
    public partial class ExtractProgress : Window
    {
        public ExtractProgressInfo extractinfo = new();
        private CancellationTokenSource cancellationTokenSource;
        public bool finished = false;

        /// <summary>
        /// Cancelled when the user closes the window (= cancels the extraction)
        /// </summary>
        public CancellationToken CancelToken => cancellationTokenSource.Token;

        public ExtractProgress(double start, double end)
        {
            InitializeComponent();
            cancellationTokenSource = new();
            // It used to replace the XAML progress bar with a new, hidden instance (progressBar = new()), so no progress was shown
            extractinfo.ProgressValue = start;
            extractinfo.ProgressMaxValue = end;
            progressBar.Minimum = start;
            progressBar.Maximum = end;
            Title = "Extract Progress";
        }

        /// <summary>
        /// Updates the progress (call from the UI thread)
        /// </summary>
        public void SetProgress(double percent, string fileName)
        {
            percent = Math.Clamp(percent, 0, 100);
            extractinfo.ProgressValue = percent;
            progressBar.IsIndeterminate = false;
            progressBar.Value = percent;
            progressTitle.Text = $"Extracting {fileName}...";
            taskBarItem.ProgressState = TaskbarItemProgressState.Normal;
            taskBarItem.ProgressValue = percent / 100;
        }

        /// <summary>
        /// Shows an animation while the percentage isn't known
        /// </summary>
        public void SetIndeterminate(string fileName)
        {
            progressBar.IsIndeterminate = true;
            progressTitle.Text = $"Extracting {fileName}...";
            progressText.Text = "Preparing...";
            taskBarItem.ProgressState = TaskbarItemProgressState.Indeterminate;
        }

        /// <summary>
        /// Call when finished (closes without counting as cancelled)
        /// </summary>
        public void CloseFinished()
        {
            finished = true;
            Close();
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            progressText.Text = $"{Math.Round(progressBar.Value)} %";
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!finished)
                cancellationTokenSource.Cancel();
        }
    }

    public class ExtractProgressInfo : INotifyPropertyChanged
    {
        private double _progressMaxValue;
        public double ProgressMaxValue
        {
            get { return _progressMaxValue; }
            set
            {
                _progressMaxValue = value;
                OnPropertyChanged(nameof(ProgressMaxValue));
            }
        }
        private double _progressValue;
        public double ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get { return _isProcessing; }
            set
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
