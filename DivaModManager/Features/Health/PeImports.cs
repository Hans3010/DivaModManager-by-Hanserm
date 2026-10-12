using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DivaModManager.Features.Health
{
    /// <summary>
    /// Gets the names of the DLLs a DLL (PE file) needs to load, from its import table
    /// </summary>
    public static class PeImports
    {
        public static List<string> Read(string path)
        {
            var data = File.ReadAllBytes(path);
            var result = new List<string>();
            if (data.Length < 0x40 || data[0] != 'M' || data[1] != 'Z') return result;

            int pe = BitConverter.ToInt32(data, 0x3C);
            if (pe <= 0 || pe + 24 > data.Length || BitConverter.ToUInt32(data, pe) != 0x00004550) return result;
            int sectionCount = BitConverter.ToUInt16(data, pe + 6);
            int optionalSize = BitConverter.ToUInt16(data, pe + 20);
            int optional = pe + 24;
            ushort magic = BitConverter.ToUInt16(data, optional);
            // Data directory offset: 96 for PE32, 112 for PE32+ (64-bit). Imports are the second entry (1)
            int dataDirs = optional + (magic == 0x20B ? 112 : 96);
            if (dataDirs + 16 > data.Length) return result;
            uint importRva = BitConverter.ToUInt32(data, dataDirs + 8);
            if (importRva == 0) return result;

            int sections = optional + optionalSize;
            int RvaToOffset(uint rva)
            {
                for (int i = 0; i < sectionCount; i++)
                {
                    int s = sections + i * 40;
                    if (s + 40 > data.Length) break;
                    uint va = BitConverter.ToUInt32(data, s + 12);
                    uint size = Math.Max(BitConverter.ToUInt32(data, s + 8), BitConverter.ToUInt32(data, s + 16));
                    uint raw = BitConverter.ToUInt32(data, s + 20);
                    if (rva >= va && rva < va + size) return (int)(rva - va + raw);
                }
                return -1;
            }

            int desc = RvaToOffset(importRva);
            if (desc < 0) return result;
            // IMAGE_IMPORT_DESCRIPTOR is 20 bytes. The list ends with one that is all zeros
            for (; desc + 20 <= data.Length; desc += 20)
            {
                uint nameRva = BitConverter.ToUInt32(data, desc + 12);
                if (nameRva == 0 && BitConverter.ToUInt32(data, desc) == 0) break;
                int nameOffset = RvaToOffset(nameRva);
                if (nameOffset < 0) continue;
                int end = Array.IndexOf(data, (byte)0, nameOffset);
                if (end <= nameOffset) continue;
                result.Add(Encoding.ASCII.GetString(data, nameOffset, end - nameOffset));
            }
            return result;
        }
    }
}
