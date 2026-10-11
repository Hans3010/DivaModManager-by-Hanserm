using DivaModManager.Common.Converters;
using System;

namespace DivaModManager.Features.Download
{
    public class DownloadProgress
    {
        public DownloadProgress(float percentage, long downloadedBytes, long totalBytes, string fileName,
            double bytesPerSecond = 0, string status = null)
        {
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            Percentage = percentage;
            FileName = fileName;
            BytesPerSecond = bytesPerSecond;
            Status = status;
        }

        public float Percentage { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public string FileName { get; set; }
        public double BytesPerSecond { get; set; }
        /// <summary>
        /// 一時停止中・再接続中などの状態（通常ダウンロード中は null）
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// プログレスウィンドウ用の表示文字列（例: "12.5% (1.2 GB of 7.5 GB) - 8.4 MB/s, 13m left"）
        /// </summary>
        public string ToDisplayText()
        {
            var text = $"{Math.Round(Percentage * 100, 2)}% " +
                $"({StringConverters.FormatSize(DownloadedBytes)} of {StringConverters.FormatSize(TotalBytes)})";
            if (!string.IsNullOrEmpty(Status))
                return $"{text} - {Status}";
            if (BytesPerSecond > 0)
            {
                text += $" - {StringConverters.FormatSize((long)BytesPerSecond)}/s";
                if (TotalBytes > DownloadedBytes)
                    text += $", {FormatEta(TimeSpan.FromSeconds((TotalBytes - DownloadedBytes) / BytesPerSecond))} left";
            }
            return text;
        }

        private static string FormatEta(TimeSpan eta)
        {
            if (eta.TotalHours >= 1) return $"{(int)eta.TotalHours}h {eta.Minutes}m";
            if (eta.TotalMinutes >= 1) return $"{eta.Minutes}m {eta.Seconds}s";
            return $"{Math.Max(1, eta.Seconds)}s";
        }
    }
}
