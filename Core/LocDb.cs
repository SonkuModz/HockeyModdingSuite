using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>One on-screen string of a language database: its key hash, key and text (null: the game stores none).</summary>
public sealed record LocRow(int Hash, string Id, string Text);

/// <summary>
/// The language databases (loc/&lt;language&gt;.db): one LanguageStrings table of (hashid, stringid, sourcetext), sorted by
/// signed hashid, both strings Huffman-compressed. Edits can't be made in place when a text gets longer or a key is
/// renamed, so the table is written again: the same header, fields and Huffman tree, new records and strings. The row
/// count is a u16, so a full table can't take more rows; <see cref="Write"/> refuses more than 65,535.
/// </summary>
public static class LocDb
{
    public const string Table = "LanguageStrings";
    public const string English = "loc/eng_us.db";

    private static readonly uint[] CrcTable = BuildTable();

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    /// <summary>
    /// The game's key hash: reflected CRC-32 (init 0, no final xor) over the key's bytes with bit 5 cleared (its
    /// "uppercase"), then xor 0x80000000, stored signed. Matches every stock row with an ASCII key.
    /// </summary>
    public static int Hash(string id)
    {
        uint c = 0;
        foreach (byte b in Encoding.UTF8.GetBytes(id)) c = CrcTable[(c ^ (uint)(b & 0xDF)) & 0xFF] ^ (c >> 8);
        return unchecked((int)(c ^ 0x80000000));
    }

    private static (T3Table T, T3Field HashF, T3Field IdF, T3Field TextF) Fields(T3Db db)
    {
        var t = db.Table(Table) ?? throw new InvalidDataException("Not a language database (no LanguageStrings table).");
        T3Field F(string n) => t.Fields.FirstOrDefault(f => f.Name == n) ?? throw new InvalidDataException($"LanguageStrings has no {n} field.");
        return (t, F("hashid"), F("stringid"), F("sourcetext"));
    }

    /// <summary>Every row of the table, in file order.</summary>
    public static List<LocRow> ReadAll(T3Db db)
    {
        var (t, hf, idf, tf) = Fields(db);
        var rows = new List<LocRow>(t.WrittenRecords);
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int rp = t.RecordsStart + r * t.RecordSize;
            string text = BinaryPrimitives.ReadInt32LittleEndian(db.Data.AsSpan(rp + tf.BitOffset / 8)) == -1 ? null : (string)db.Get(t, r, tf);
            rows.Add(new LocRow((int)db.Get(t, r, hf), (string)db.Get(t, r, idf), text));
        }
        return rows;
    }

    /// <summary>
    /// The database written again with <paramref name="rows"/> (sorted here by signed hash). Everything before the
    /// table's records is kept; the records, the strings after the Huffman tree, the sizes and every CRC are new.
    /// Throws when a text has a character the table's tree can't encode, or there are more rows than a u16 holds.
    /// </summary>
    public static byte[] Write(T3Db db, IReadOnlyList<LocRow> rows)
    {
        var (t, hf, idf, tf) = Fields(db);
        if (db.Tables.Count != 1) throw new InvalidDataException("A language database has one table.");
        if (rows.Count > ushort.MaxValue) throw new InvalidOperationException($"{rows.Count:N0} strings: a language table holds at most 65,535.");
        if (hf.BitOffset % 8 != 0 || hf.Depth != 32 || idf.BitOffset % 8 != 0 || tf.BitOffset % 8 != 0)
            throw new InvalidDataException("LanguageStrings' fields aren't laid out as expected.");
        var data = db.Data;
        int I32(int p) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(p));
        int fileSize = I32(8);
        if (t.CrcRecordsPos + 4 != fileSize) throw new InvalidDataException("Something follows the strings table; not rewriting it.");

        // the Huffman tree: the bytes before the first string any record points at
        int treeBytes = int.MaxValue;
        for (int r = 0; r < t.WrittenRecords; r++)
            foreach (var f in new[] { idf, tf })
                if (I32(t.RecordsStart + r * t.RecordSize + f.BitOffset / 8) is var so && so != -1 && so < treeBytes) treeBytes = so;
        if (treeBytes == int.MaxValue) throw new InvalidDataException("The strings table has no strings.");

        var sorted = rows.OrderBy(r => r.Hash).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        for (int i = 1; i < sorted.Count; i++)
            if (sorted[i].Hash == sorted[i - 1].Hash && sorted[i].Id == sorted[i - 1].Id) throw new InvalidOperationException($"The key {sorted[i].Id} is there twice.");

        var strings = new MemoryStream();
        strings.Write(data, t.StringsStart, treeBytes);
        var placed = new Dictionary<(bool, string), int>();
        int Put(T3Field f, string text)
        {
            if (text == null) return -1;
            bool shortLen = f.Type == T3FieldType.ShortCompressedString;
            if (placed.TryGetValue((shortLen, text), out int at)) return at;
            var raw = Encoding.UTF8.GetBytes(text);
            var enc = t.Huffman.Encode(raw) ?? throw new ArgumentException($"\"{text}\" has a character the game's strings never use.");
            if (raw.Length > (shortLen ? 255 : 65535)) throw new ArgumentException($"\"{text[..Math.Min(40, text.Length)]}…\" is too long.");
            int off = (int)strings.Length;
            if (shortLen) strings.WriteByte((byte)raw.Length);
            else { strings.WriteByte((byte)(raw.Length >> 8)); strings.WriteByte((byte)raw.Length); }
            strings.Write(enc);
            return placed[(shortLen, text)] = off;
        }

        var template = db.RecordBytes(t, 0);
        template[^1] &= 0x7F;
        var records = new byte[sorted.Count * t.RecordSize];
        for (int i = 0; i < sorted.Count; i++)
        {
            var rec = records.AsSpan(i * t.RecordSize, t.RecordSize);
            template.CopyTo(rec);
            // hashid is a 32-bit integer stored as an offset from its range's low end
            BinaryPrimitives.WriteInt32LittleEndian(rec[(hf.BitOffset / 8)..], unchecked((int)((long)sorted[i].Hash - hf.RangeLow)));
            BinaryPrimitives.WriteInt32LittleEndian(rec[(idf.BitOffset / 8)..], Put(idf, sorted[i].Id));
            BinaryPrimitives.WriteInt32LittleEndian(rec[(tf.BitOffset / 8)..], Put(tf, sorted[i].Text));
        }

        int stringsLength = (int)strings.Length;
        int recordsEnd = t.RecordsStart + records.Length;
        int crcPos = (recordsEnd + stringsLength + 3) & ~3;
        var o = new byte[crcPos + 4];
        data.AsSpan(0, t.RecordsStart).CopyTo(o);
        records.CopyTo(o, t.RecordsStart);
        strings.ToArray().CopyTo(o, recordsEnd);
        BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(8), o.Length);
        BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(t.Start + 12), stringsLength);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(t.Start + 16), (ushort)sorted.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(t.Start + 18), (ushort)sorted.Count);
        void Crc(int from, int pos) => BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(pos), T3Db.Crc(o.AsSpan(from, pos - from)));
        Crc(0, db.CrcHeaderPos);
        Crc(db.CrcHeaderPos + 4, db.CrcNamesPos);
        Crc(db.CrcNamesPos + 4, t.CrcHeaderPos);
        Crc(t.CrcHeaderPos + 4, crcPos);
        return o;
    }

    /// <summary>Rows a language table is let grow to: some headroom under the u16 count's 65,535.</summary>
    public static int RowCap { get; set; } = 65520;

    /// <summary>
    /// Sets each key's text and writes the database again. Keys that aren't there are added as new rows while the table
    /// has fewer than <see cref="RowCap"/>; past that they take over the rows of <paramref name="spare"/> keys, in order.
    /// Returns the new bytes, the keys added and the keys taken over.
    /// </summary>
    public static (byte[] Data, List<string> Added, List<string> TakenOver) Set(T3Db db, IReadOnlyDictionary<string, string> texts, IEnumerable<string> spare)
    {
        var rows = ReadAll(db);
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++) byId.TryAdd(rows[i].Id, i);
        var spareRows = new Queue<int>(spare.Where(byId.ContainsKey).Select(k => byId[k]).Distinct());
        var taken = new List<string>();
        var added = new List<string>();
        foreach (var (id, text) in texts)
        {
            if (byId.TryGetValue(id, out int at)) { rows[at] = rows[at] with { Text = text }; continue; }
            if (rows.Count < RowCap)
            {
                byId[id] = rows.Count;
                rows.Add(new LocRow(Hash(id), id, text));
                added.Add(id);
                continue;
            }
            if (spareRows.Count == 0) throw new InvalidOperationException($"No spare strings left for {id}.");
            int row = spareRows.Dequeue();
            taken.Add(rows[row].Id);
            byId.Remove(rows[row].Id);
            rows[row] = new LocRow(Hash(id), id, text);
            byId[id] = row;
        }
        return (Write(db, rows), added, taken);
    }
}
