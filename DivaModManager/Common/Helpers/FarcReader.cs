using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DivaModManager.Common.Helpers
{
    /// <summary>
    /// Reads Project DIVA FARC archives (FArc: uncompressed / FArC: gzip-compressed)
    /// Encrypted FARC formats are not supported (mods don't normally use them)
    /// </summary>
    public static class FarcReader
    {
        public class Entry
        {
            public string Name { get; set; }
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Reads the files inside a FARC. Returns an empty list if it can't be read
        /// </summary>
        /// <param name="nameFilter">If given, only files with a matching name are extracted</param>
        public static List<Entry> Read(string path, Func<string, bool> nameFilter = null)
        {
            var entries = new List<Entry>();
            byte[] b;
            try { b = File.ReadAllBytes(path); }
            catch (Exception) { return entries; }
            if (b.Length < 12) return entries;

            var magic = Encoding.ASCII.GetString(b, 0, 4);
            var compressed = magic == "FArC";
            if (!compressed && magic != "FArc") return entries;

            var headerEnd = 8 + (long)ReadUInt32BE(b, 4);
            var p = 12; // magic + header size + alignment
            while (p < headerEnd && p < b.Length)
            {
                var start = p;
                while (p < b.Length && b[p] != 0) p++;
                if (p >= b.Length) break;
                var name = Encoding.UTF8.GetString(b, start, p - start);
                p++;
                if (name.Length == 0) break;

                var entrySize = compressed ? 12 : 8;
                if (p + entrySize > b.Length) break;
                var offset = ReadUInt32BE(b, p);
                var size = ReadUInt32BE(b, p + 4);
                var originalSize = compressed ? ReadUInt32BE(b, p + 8) : size;
                p += entrySize;

                if (nameFilter != null && !nameFilter(name)) continue;
                if ((long)offset + size > b.Length) continue;

                try
                {
                    byte[] data;
                    if (compressed && size != originalSize)
                    {
                        using var input = new MemoryStream(b, (int)offset, (int)size);
                        using var gzip = new GZipStream(input, CompressionMode.Decompress);
                        using var output = new MemoryStream((int)originalSize);
                        gzip.CopyTo(output);
                        data = output.ToArray();
                    }
                    else
                    {
                        data = new byte[size];
                        Array.Copy(b, offset, data, 0, size);
                    }
                    entries.Add(new Entry { Name = name, Data = data });
                }
                catch (InvalidDataException)
                {
                    // Skip broken entries
                }
            }
            return entries;
        }

        private static uint ReadUInt32BE(byte[] b, int offset)
        {
            return ((uint)b[offset] << 24) | ((uint)b[offset + 1] << 16) | ((uint)b[offset + 2] << 8) | b[offset + 3];
        }
    }
}
