using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DivaModManager.Features.Download
{
    /// <summary>
    /// Lets a download continue after the manager was closed or crashed.
    /// While a file downloads, a marker "{file}.dmmpartial" next to it remembers its URL. If the same file is
    /// downloaded again from the same URL, the existing data is kept and only the missing part is requested
    /// (HttpClientExtensions.DownloadAsync resumes with a Range header, or starts over if the server can't).
    /// </summary>
    public static class ResumableDownload
    {
        public const string MARKER_EXTENSION = ".dmmpartial";

        private class Marker
        {
            public string Url { get; set; }
            public DateTime Started { get; set; }
        }

        private static string MarkerPath(string filePath) => filePath + MARKER_EXTENSION;

        /// <summary>
        /// Opens the file to download into. Keeps the data of an unfinished download of the same URL;
        /// otherwise starts an empty file. Returns the stream and how many bytes are already there.
        /// </summary>
        public static (FileStream Stream, long Existing) Open(string filePath, string url)
        {
            var marker = ReadMarker(filePath);
            var resume = marker != null && marker.Url == url && File.Exists(filePath) && new FileInfo(filePath).Length > 0;
            if (!resume)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                WriteMarker(filePath, new Marker { Url = url, Started = DateTime.Now });
            }
            var stream = new FileStream(filePath, resume ? FileMode.Open : FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            if (resume)
            {
                stream.Seek(0, SeekOrigin.End);
                Logger.WriteLine($"Resuming the unfinished download of {Path.GetFileName(filePath)} from {FileHelper.GetDirectorySizeView10(stream.Length)}.", LoggerType.Info);
            }
            return (stream, resume ? stream.Length : 0);
        }

        /// <summary>The download finished: the file is complete, so the marker is removed</summary>
        public static void Complete(string filePath) => DeleteMarker(filePath);

        /// <summary>The user cancelled: remove the partial file and its marker</summary>
        public static void Discard(string filePath)
        {
            DeleteMarker(filePath);
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch (Exception) { }
        }

        /// <summary>Unfinished downloads in the Downloads folder (file name and size so far)</summary>
        public static List<(string FileName, long Size)> FindUnfinished()
        {
            var result = new List<(string, long)>();
            if (!Directory.Exists(Global.downloadBaseLocation)) return result;
            foreach (var marker in Directory.GetFiles(Global.downloadBaseLocation, "*" + MARKER_EXTENSION))
            {
                var file = marker[..^MARKER_EXTENSION.Length];
                if (File.Exists(file)) result.Add((Path.GetFileName(file), new FileInfo(file).Length));
                else DeleteMarker(file);
            }
            return result;
        }

        private static Marker ReadMarker(string filePath)
        {
            try
            {
                var path = MarkerPath(filePath);
                return File.Exists(path) ? JsonSerializer.Deserialize<Marker>(File.ReadAllText(path)) : null;
            }
            catch (Exception) { return null; }
        }

        private static void WriteMarker(string filePath, Marker marker)
        {
            try { File.WriteAllText(MarkerPath(filePath), JsonSerializer.Serialize(marker)); }
            catch (Exception) { }
        }

        private static void DeleteMarker(string filePath)
        {
            try { if (File.Exists(MarkerPath(filePath))) File.Delete(MarkerPath(filePath)); }
            catch (Exception) { }
        }
    }
}
