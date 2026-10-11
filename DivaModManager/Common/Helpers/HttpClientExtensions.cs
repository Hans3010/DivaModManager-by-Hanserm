
using DivaModManager.Features.Debug;
using DivaModManager.Features.Download;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Common.Helpers
{
    public static class HttpClientExtensions
    {
        public static long GetDirectorySize(this DirectoryInfo directoryInfo, bool recursive = true)
        {
            var startDirectorySize = default(long);
            if (directoryInfo == null || !directoryInfo.Exists)
                return startDirectorySize; //Return 0 while Directory does not exist.

            //Add size of files in the Current Directory to main size.
            foreach (var fileInfo in directoryInfo.GetFiles())
                Interlocked.Add(ref startDirectorySize, fileInfo.Length);

            if (recursive) //Loop on Sub Direcotries in the Current Directory and Calculate it's files size.
                Parallel.ForEach(directoryInfo.GetDirectories(), (subDirectory) =>
            Interlocked.Add(ref startDirectorySize, subDirectory.GetDirectorySize(recursive)));

            return startDirectorySize;  //Return full Size of this Directory.
        }
        private const int DownloadBufferSize = 81920;
        private const int MaxDownloadRetries = 5;
        // この秒数データが届かなければ接続が止まったとみなして再接続する
        private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

        // Download function Core
        // 以前の実装の問題点:
        //  - HTTPエラー(404/503等)のレスポンス本文をそのままModファイルとして保存していた
        //  - 本文の読み込み中にはタイムアウトが無く、接続が止まると永遠に終わらなかった
        //  - 途中で切断されても再試行せず、サイズ不一致の警告後に壊れたファイルを展開していた
        // 現在は Range ヘッダで途中から再開し、失敗時は例外を投げて展開させない
        public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, string fileName,
            IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default, PauseToken pause = default)
        {
            var total = new StrongBox<long?>();
            var retry = 0;
            while (true)
            {
                await pause.WaitWhilePausedAsync(cancellationToken);
                var lengthBeforeAttempt = destination.Length;
                try
                {
                    await DownloadAttemptAsync(client, requestUri, destination, fileName, progress, cancellationToken, pause, total);
                    break;
                }
                catch (Exception ex) when (IsRetryable(ex, cancellationToken)
                    && (retry < MaxDownloadRetries || destination.Length > lengthBeforeAttempt))
                {
                    // 大きいファイルでは何度も切断されることがあるため、進捗があれば回数をリセットする
                    if (destination.Length > lengthBeforeAttempt)
                        retry = 0;
                    retry++;
                    Logger.WriteLine($"Download of {fileName} interrupted at {destination.Length} bytes ({ex.Message}). Retrying {retry}/{MaxDownloadRetries}...", LoggerType.Warning);
                    progress?.Report(new DownloadProgress(
                        total.Value > 0 ? (float)destination.Length / total.Value.Value : 0, destination.Length, total.Value ?? 0, fileName,
                        status: $"Connection lost, retrying ({retry}/{MaxDownloadRetries})..."));
                    await Task.Delay(TimeSpan.FromSeconds(2 * retry), cancellationToken);
                }
            }

            var totalLength = total.Value;
            if (totalLength.HasValue && totalLength.Value != destination.Length)
            {
                throw new IOException($"The downloaded file is incomplete. Server size: {totalLength}, downloaded size: {destination.Length}.");
            }
            if (!totalLength.HasValue)
            {
                Logger.WriteLine($"Could not retrieve the ContentLength of {fileName}. Skip size validation after download.", LoggerType.Warning);
            }
            progress?.Report(new DownloadProgress(1, destination.Length, destination.Length, fileName));
        }

        /// <summary>
        /// 1回分のダウンロード。destination に既にデータがあれば Range で続きから取得する
        /// </summary>
        /// <param name="total">ファイル全体のサイズ（不明なら null）。失敗時も判明したサイズを保持する</param>
        private static async Task DownloadAttemptAsync(HttpClient client, string requestUri, Stream destination, string fileName,
            IProgress<DownloadProgress> progress, CancellationToken cancellationToken, PauseToken pause, StrongBox<long?> total)
        {
            var resumeFrom = destination.Length;
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (resumeFrom > 0)
                request.Headers.Range = new RangeHeaderValue(resumeFrom, null);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (resumeFrom > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && total.Value == resumeFrom)
                return; // 既に全て取得済み
            response.EnsureSuccessStatusCode();

            long? totalLength;
            if (resumeFrom > 0 && response.StatusCode == HttpStatusCode.PartialContent)
            {
                totalLength = response.Content.Headers.ContentRange?.Length
                    ?? (response.Content.Headers.ContentLength + resumeFrom);
                destination.Seek(resumeFrom, SeekOrigin.Begin);
            }
            else
            {
                // サーバーが Range に未対応の場合は最初からやり直す
                if (resumeFrom > 0)
                {
                    Logger.WriteLine($"Server does not support resuming {fileName}. Restarting download from the beginning.", LoggerType.Warning);
                    destination.SetLength(0);
                    destination.Seek(0, SeekOrigin.Begin);
                }
                totalLength = response.Content.Headers.ContentLength;
            }
            total.Value = totalLength;

            using var download = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[DownloadBufferSize];
            var reportWatch = Stopwatch.StartNew();
            var speedWatch = Stopwatch.StartNew();
            long speedBytes = 0;
            double bytesPerSecond = 0;
            while (true)
            {
                if (pause.IsPaused)
                {
                    progress?.Report(new DownloadProgress(
                        totalLength > 0 ? (float)destination.Length / totalLength.Value : 0, destination.Length, totalLength ?? 0, fileName, status: "Paused"));
                    // 長時間の一時停止中にサーバーが接続を切った場合は、再試行処理で続きから再開される
                    await pause.WaitWhilePausedAsync(cancellationToken);
                    speedWatch.Restart();
                    speedBytes = 0;
                }

                int bytesRead;
                using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    stallCts.CancelAfter(StallTimeout);
                    try
                    {
                        bytesRead = await download.ReadAsync(buffer.AsMemory(0, buffer.Length), stallCts.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"No data received for {StallTimeout.TotalSeconds} seconds.");
                    }
                }
                if (bytesRead == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                speedBytes += bytesRead;

                if (progress != null && reportWatch.Elapsed >= ProgressInterval)
                {
                    reportWatch.Restart();
                    if (speedWatch.Elapsed.TotalSeconds >= 1)
                    {
                        var current = speedBytes / speedWatch.Elapsed.TotalSeconds;
                        bytesPerSecond = bytesPerSecond == 0 ? current : (bytesPerSecond * 0.7) + (current * 0.3);
                        speedWatch.Restart();
                        speedBytes = 0;
                    }
                    if (totalLength > 0)
                        progress.Report(new DownloadProgress((float)destination.Length / totalLength.Value, destination.Length, totalLength.Value, fileName, bytesPerSecond));
                }
            }

            // 接続が途中で閉じられた場合は再試行させる
            if (totalLength.HasValue && destination.Length < totalLength.Value)
                throw new IOException($"Connection closed before the download finished ({destination.Length} of {totalLength} bytes).");
        }

        private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;
            if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
            {
                // 404/403 等は再試行しても無駄なので即座に失敗させる
                var code = (int)httpEx.StatusCode.Value;
                return code == 408 || code == 429 || code >= 500;
            }
            return ex is HttpRequestException || ex is IOException || ex is TimeoutException
                || ex is TaskCanceledException; // HttpClient.Timeout（ヘッダ受信待ち）
        }
    }
    public static class StreamExtensions
    {
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long> progress = null, CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.CanRead)
                throw new ArgumentException("Has to be readable", nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new ArgumentException("Has to be writable", nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
    }
}
