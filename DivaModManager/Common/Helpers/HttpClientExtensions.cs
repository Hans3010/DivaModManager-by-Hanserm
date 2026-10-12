
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
        // If no data arrives for this many seconds, the connection is considered stalled and is reopened
        private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

        // Download function Core
        // Problems with the previous implementation:
        //  - It saved the body of HTTP error responses (404/503, etc.) as the mod file
        //  - Reading the body had no timeout, so a stalled connection never finished
        //  - It didn't retry after a disconnect, and extracted the broken file after a size mismatch warning
        // Now it resumes with a Range header and throws on failure so nothing gets extracted
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
                    // Big files can disconnect many times, so the retry count is reset whenever there's progress
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
        /// One download attempt. If destination already has data, the rest is requested with Range
        /// </summary>
        /// <param name="total">Size of the whole file (null if unknown). Keeps the size it found even when the attempt fails</param>
        private static async Task DownloadAttemptAsync(HttpClient client, string requestUri, Stream destination, string fileName,
            IProgress<DownloadProgress> progress, CancellationToken cancellationToken, PauseToken pause, StrongBox<long?> total)
        {
            var resumeFrom = destination.Length;
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (resumeFrom > 0)
                request.Headers.Range = new RangeHeaderValue(resumeFrom, null);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (resumeFrom > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && total.Value == resumeFrom)
                return; // Already fully downloaded
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
                // If the server doesn't support Range, start over from the beginning
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
                    // If the server closes the connection during a long pause, the retry resumes where it stopped
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

            // Retry if the connection is closed partway through
            if (totalLength.HasValue && destination.Length < totalLength.Value)
                throw new IOException($"Connection closed before the download finished ({destination.Length} of {totalLength} bytes).");
        }

        private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;
            if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
            {
                // 404/403 and similar won't succeed on retry, so fail right away
                var code = (int)httpEx.StatusCode.Value;
                return code == 408 || code == 429 || code >= 500;
            }
            return ex is HttpRequestException || ex is IOException || ex is TimeoutException
                || ex is TaskCanceledException; // HttpClient.Timeout (waiting for the headers)
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
