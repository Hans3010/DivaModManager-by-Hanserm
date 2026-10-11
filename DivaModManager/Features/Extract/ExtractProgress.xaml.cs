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
        /// ユーザーがウィンドウを閉じた（＝解凍をキャンセルした）ときにキャンセルされる
        /// </summary>
        public CancellationToken CancelToken => cancellationTokenSource.Token;

        public ExtractProgress(double start, double end)
        {
            InitializeComponent();
            cancellationTokenSource = new();
            // 以前は progressBar = new() で XAML のプログレスバーを未表示の新規インスタンスに置き換えていたため、進捗が表示されなかった
            extractinfo.ProgressValue = start;
            extractinfo.ProgressMaxValue = end;
            progressBar.Minimum = start;
            progressBar.Maximum = end;
            Title = "Extract Progress";
        }

        /// <summary>
        /// 進捗を更新する（UIスレッドから呼び出すこと）
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
        /// 進捗率が取得できない間はアニメーション表示にする
        /// </summary>
        public void SetIndeterminate(string fileName)
        {
            progressBar.IsIndeterminate = true;
            progressTitle.Text = $"Extracting {fileName}...";
            progressText.Text = "Preparing...";
            taskBarItem.ProgressState = TaskbarItemProgressState.Indeterminate;
        }

        /// <summary>
        /// 処理完了時に呼び出す（キャンセル扱いにせずに閉じる）
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
