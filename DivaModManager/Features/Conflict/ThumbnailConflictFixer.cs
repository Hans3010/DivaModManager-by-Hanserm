using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DivaModManager.Features.Conflict
{
    /// <summary>
    /// Fixes song select thumbnail conflicts.
    /// Older song packs replace the whole official thumbnail sprite set (SPR_SEL_PVTMB, ID 4527, spr_sel_pvtmb.farc),
    /// so with two or more of those packs enabled, one pack's thumbnails don't show.
    /// Like newer packs (Nitori's Song Pack, etc.), the pack's thumbnails are moved to their own set
    /// (SPR_SEL_PVTMB_xxx / spr_sel_pvtmb_xxx.farc with a unique ID) so both packs work together.
    /// The original files are kept as .bak.
    /// </summary>
    public static class ThumbnailConflictFixer
    {
        public const string THUMB_FARC_RELATIVE = "rom/2d/spr_sel_pvtmb.farc";
        private const uint OFFICIAL_SET_ID = 4527;
        private const string OFFICIAL_SET_NAME = "SPR_SEL_PVTMB";
        private const string OFFICIAL_FILE_NAME = "spr_sel_pvtmb.bin";

        public static bool CanFix(ModConflict conflict) =>
            conflict.Type == ConflictType.File && conflict.Key.Equals(THUMB_FARC_RELATIVE, StringComparison.OrdinalIgnoreCase) && conflict.Mods.Count > 1;

        /// <summary>
        /// Moves the given mod's thumbnails to their own sprite set
        /// </summary>
        /// <returns>Result message</returns>
        public static string Fix(string modName, string modPath)
        {
            var dir2d = Path.Combine(modPath, "rom", "2d");
            var sprDbPath = Path.Combine(dir2d, "mod_spr_db.bin");
            var farcPath = Path.Combine(dir2d, "spr_sel_pvtmb.farc");
            if (!File.Exists(sprDbPath) || !File.Exists(farcPath))
                throw new InvalidOperationException($"{modName}: mod_spr_db.bin or spr_sel_pvtmb.farc not found.");

            var suffix = MakeSuffix(modName);
            var newSetName = $"{OFFICIAL_SET_NAME}_{suffix}";
            var newFileBase = $"spr_sel_pvtmb_{suffix.ToLowerInvariant()}";
            var newFarcPath = Path.Combine(dir2d, newFileBase + ".farc");
            if (File.Exists(newFarcPath))
                throw new InvalidOperationException($"{modName}: {Path.GetFileName(newFarcPath)} already exists.");

            // 1) mod_spr_db.bin: point the official set's entry to the new set (strings are appended at the end)
            var db = File.ReadAllBytes(sprDbPath);
            var setCount = BitConverter.ToUInt32(db, 0);
            var setsOffset = BitConverter.ToUInt32(db, 4);
            var usedIds = new HashSet<uint>();
            int target = -1;
            for (int i = 0; i < setCount; i++)
            {
                var o = (int)setsOffset + 16 * i;
                var id = BitConverter.ToUInt32(db, o);
                usedIds.Add(id);
                if (id == OFFICIAL_SET_ID && ReadString(db, (int)BitConverter.ToUInt32(db, o + 4)) == OFFICIAL_SET_NAME)
                    target = o;
            }
            if (target < 0)
                throw new InvalidOperationException($"{modName}: the official thumbnail set (SPR_SEL_PVTMB, 4527) is not in mod_spr_db.bin.");

            var newId = MakeSetId(newSetName);
            while (usedIds.Contains(newId) || newId == OFFICIAL_SET_ID) newId++;

            using (var ms = new MemoryStream())
            {
                ms.Write(db, 0, db.Length);
                while (ms.Length % 4 != 0) ms.WriteByte(0);
                var nameOffset = (uint)ms.Length;
                WriteString(ms, newSetName);
                var fileOffset = (uint)ms.Length;
                WriteString(ms, newFileBase + ".bin");
                while (ms.Length % 4 != 0) ms.WriteByte(0);
                var patched = ms.ToArray();
                BitConverter.GetBytes(newId).CopyTo(patched, target);
                BitConverter.GetBytes(nameOffset).CopyTo(patched, target + 4);
                BitConverter.GetBytes(fileOffset).CopyTo(patched, target + 8);
                db = patched;
            }

            // 2) FARC: rename the files inside and save it under the new name
            var farc = RenameFarcEntry(File.ReadAllBytes(farcPath), OFFICIAL_FILE_NAME, newFileBase + ".bin");

            // 3) Write only after making backups (the original spr_sel_pvtmb.farc becomes .bak so it isn't loaded)
            File.Copy(sprDbPath, sprDbPath + ".bak", true);
            File.WriteAllBytes(newFarcPath, farc);
            File.Move(farcPath, farcPath + ".bak", true);
            File.WriteAllBytes(sprDbPath, db);

            return $"{modName}: thumbnails moved to their own sprite set ({newSetName}, {Path.GetFileName(newFarcPath)}). Backups: mod_spr_db.bin.bak, spr_sel_pvtmb.farc.bak";
        }

        /// <summary>Restores the backups made by Fix</summary>
        public static bool Restore(string modPath)
        {
            var dir2d = Path.Combine(modPath, "rom", "2d");
            var sprDbBak = Path.Combine(dir2d, "mod_spr_db.bin.bak");
            var farcBak = Path.Combine(dir2d, "spr_sel_pvtmb.farc.bak");
            if (!File.Exists(sprDbBak) || !File.Exists(farcBak)) return false;
            foreach (var f in Directory.GetFiles(dir2d, "spr_sel_pvtmb_*.farc"))
            {
                if (RenameFarcEntryNames(File.ReadAllBytes(f)).Any(n => n.StartsWith("spr_sel_pvtmb_", StringComparison.OrdinalIgnoreCase)))
                    File.Delete(f);
            }
            File.Move(farcBak, Path.Combine(dir2d, "spr_sel_pvtmb.farc"), true);
            File.Move(sprDbBak, Path.Combine(dir2d, "mod_spr_db.bin"), true);
            return true;
        }

        private static string MakeSuffix(string modName)
        {
            // "Cabra, Eggu, Nitori Commission Pack" -> "CENCP"
            var initials = new string(modName.Split(new[] { ' ', ',', '-', '_', '\'', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0])).Where(char.IsLetterOrDigit).ToArray());
            if (initials.Length < 2) initials = new string(modName.Where(char.IsLetterOrDigit).Take(6).ToArray()).ToUpperInvariant();
            return "DMM" + (initials.Length > 0 ? initials : "MOD");
        }

        /// <summary>Makes an ID derived from the name so it doesn't collide with other mods (FNV-1a)</summary>
        private static uint MakeSetId(string name)
        {
            uint hash = 2166136261;
            foreach (var b in Encoding.UTF8.GetBytes(name)) { hash ^= b; hash *= 16777619; }
            return hash;
        }

        private static string ReadString(byte[] b, int offset)
        {
            if (offset < 0 || offset >= b.Length) return string.Empty;
            var end = offset;
            while (end < b.Length && b[end] != 0) end++;
            return Encoding.UTF8.GetString(b, offset, end - offset);
        }

        private static void WriteString(Stream s, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            s.Write(bytes, 0, bytes.Length);
            s.WriteByte(0);
        }

        private static uint ReadUInt32BE(byte[] b, int o) => ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];

        private static void WriteUInt32BE(Stream s, uint v)
        {
            s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
        }

        private static List<string> RenameFarcEntryNames(byte[] b)
        {
            var names = new List<string>();
            if (b.Length < 12) return names;
            var magic = Encoding.ASCII.GetString(b, 0, 4);
            if (magic != "FArc" && magic != "FArC") return names;
            var entrySize = magic == "FArC" ? 12 : 8;
            var end = 8 + (int)ReadUInt32BE(b, 4);
            var p = 12;
            while (p < end && p < b.Length)
            {
                var name = ReadString(b, p);
                if (name.Length == 0) break;
                names.Add(name);
                p += Encoding.UTF8.GetByteCount(name) + 1 + entrySize;
            }
            return names;
        }

        /// <summary>
        /// Rebuilds a FARC (FArc / FArC) with renamed entries. The data is copied as is
        /// </summary>
        private static byte[] RenameFarcEntry(byte[] b, string oldName, string newName)
        {
            var magic = Encoding.ASCII.GetString(b, 0, 4);
            if (magic != "FArc" && magic != "FArC")
                throw new InvalidOperationException($"Unsupported FARC format: {magic}");
            var compressed = magic == "FArC";
            var entrySize = compressed ? 12 : 8;
            var alignment = ReadUInt32BE(b, 8);
            var headerEnd = 8 + (int)ReadUInt32BE(b, 4);

            var entries = new List<(string Name, byte[] Data, uint OriginalSize)>();
            var p = 12;
            var renamed = false;
            while (p < headerEnd && p < b.Length)
            {
                var name = ReadString(b, p);
                if (name.Length == 0) break;
                p += Encoding.UTF8.GetByteCount(name) + 1;
                var offset = (int)ReadUInt32BE(b, p);
                var size = (int)ReadUInt32BE(b, p + 4);
                var originalSize = compressed ? ReadUInt32BE(b, p + 8) : (uint)size;
                p += entrySize;
                var data = new byte[size];
                Array.Copy(b, offset, data, 0, size);
                if (name.Equals(oldName, StringComparison.OrdinalIgnoreCase)) { name = newName; renamed = true; }
                entries.Add((name, data, originalSize));
            }
            if (!renamed)
                throw new InvalidOperationException($"{oldName} was not found inside the FARC file.");

            var headerSize = 4 + entries.Sum(e => Encoding.UTF8.GetByteCount(e.Name) + 1 + entrySize);
            var align = (int)Math.Max(1, alignment);
            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes(magic), 0, 4);
            WriteUInt32BE(ms, (uint)headerSize);
            WriteUInt32BE(ms, alignment);

            var offsetPositions = new List<long>();
            foreach (var e in entries)
            {
                WriteString(ms, e.Name);
                offsetPositions.Add(ms.Position);
                WriteUInt32BE(ms, 0); // offset (written later)
                WriteUInt32BE(ms, (uint)e.Data.Length);
                if (compressed) WriteUInt32BE(ms, e.OriginalSize);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                while (ms.Length % align != 0) ms.WriteByte(0);
                var dataOffset = (uint)ms.Length;
                ms.Write(entries[i].Data, 0, entries[i].Data.Length);
                var end = ms.Position;
                ms.Position = offsetPositions[i];
                WriteUInt32BE(ms, dataOffset);
                ms.Position = end;
            }
            while (ms.Length % align != 0) ms.WriteByte(0);
            return ms.ToArray();
        }
    }
}
