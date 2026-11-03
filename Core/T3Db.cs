using System.Buffers.Binary;
using System.Text;
using System.Xml.Linq;

namespace NHL27Kit.Core;

public enum T3FieldType { String = 0, Integer = 3, Float = 4, ShortCompressedString = 13, LongCompressedString = 14 }

/// <summary>A field of a t3db table: its storage (type, bit offset and depth) and, from the meta XML, its name and range.</summary>
public sealed class T3Field
{
    public string ShortName { get; init; }
    public string Name { get; set; }
    public T3FieldType Type { get; init; }
    public int BitOffset { get; init; }
    public int Depth { get; init; }
    public int RangeLow { get; set; }
    public int RangeHigh { get; set; }
    public bool Key { get; set; }

    public bool IsCompressed => Type is T3FieldType.ShortCompressedString or T3FieldType.LongCompressedString;
    public override string ToString() => Name;
}

/// <summary>
/// A table of a t3db database. Records are kept as raw bytes; values are read and written in place at each field's bit
/// offset (integers least-significant bit first, as offsets from the field's low end), so an edit never moves anything.
/// </summary>
public sealed class T3Table
{
    public string ShortName { get; init; }
    public string Name { get; set; }
    public List<T3Field> Fields { get; } = new();
    public int RecordSize { get; init; }
    public int Records { get; init; }
    public int WrittenRecords { get; internal set; }
    public int CancelledRecords { get; init; }

    /// <summary>Byte offsets in the file: the table's start, its records, its compressed strings area, its records CRC.</summary>
    public int Start { get; init; }
    public int RecordsStart { get; init; }
    public int StringsStart { get; init; }
    public int StringsLength { get; init; }
    public int CrcRecordsPos { get; internal set; }
    public int CrcHeaderPos { get; init; }

    internal T3Huffman Huffman { get; set; }
    public bool HasCompressedStrings => Huffman != null;

    public override string ToString() => Name;
}

/// <summary>
/// EA's t3db databases (NHL 27: gamemodes/database/t3db/nhl_game.db, loc/*.db), read and edited in place. Layout (all
/// little endian): "DB\0\x08" signature, u32 0, u32 file size, u32 0, u32 table count, CRC; per table a 4-char short
/// name and u32 offset; CRC; then the tables. A table: u32, u32 record size, u32 bit count, u32 compressed strings length,
/// u16 records, u16 written, u16 cancelled, u16, u8 field count, 3 pad, u32, CRC; fields (u32 type, u32 bit offset,
/// 4-char name, u32 depth); the records; for compressed strings a Huffman tree then the strings; CRC. Each CRC covers the
/// bytes since the previous one. The meta XML names tables and fields and gives integer ranges.
/// </summary>
public sealed class T3Db
{
    public byte[] Data { get; }
    public List<T3Table> Tables { get; } = new();
    public int CrcHeaderPos { get; }
    public int CrcNamesPos { get; }
    /// <summary>Where the database was read from, when it came from the game.</summary>
    public LegacyFile Source { get; init; }

    public T3Db(byte[] data, string metaXml = null)
    {
        Data = data;
        if (data.Length < 24 || data[0] != 'D' || data[1] != 'B' || data[2] != 0 || data[3] != 8)
            throw new InvalidDataException("Not a t3db database (no DB\\0\\x08 signature).");
        int n = I32(16);
        CrcHeaderPos = 20;
        int p = 24;
        var names = new List<(string, int)>();
        for (int i = 0; i < n; i++, p += 8)
            names.Add((Encoding.ASCII.GetString(data, p, 4), I32(p + 4)));
        CrcNamesPos = p;
        int baseOff = p + 4;
        foreach (var (shortName, off) in names)
            Tables.Add(ReadTable(shortName, baseOff + off));
        // a table's records CRC is the last 4 bytes before the next table (or the file's end): the padding after
        // compressed strings isn't always to 4 bytes (loc/ger_de.db), so it's taken from where the next table starts
        var byStart = Tables.OrderBy(t => t.Start).ToList();
        int fileSize = I32(8);
        for (int i = 0; i < byStart.Count; i++)
        {
            int end = i + 1 < byStart.Count ? byStart[i + 1].Start : fileSize;
            if (end - 4 >= byStart[i].RecordsStart && end <= data.Length) byStart[i].CrcRecordsPos = end - 4;
        }
        if (metaXml != null) ApplyMeta(metaXml);
    }

    private int I32(int p) => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(p));
    private ushort U16(int p) => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(p));

    private T3Table ReadTable(string shortName, int start)
    {
        int recordSize = I32(start + 4);
        int stringsLength = I32(start + 12);
        int records = U16(start + 16), written = U16(start + 18), cancelled = U16(start + 20);
        int fieldCount = Data[start + 24];
        int crcHeader = start + 32;
        int p = crcHeader + 4;
        var fields = new List<T3Field>();
        for (int i = 0; i < fieldCount; i++, p += 16)
            fields.Add(new T3Field
            {
                Type = (T3FieldType)I32(p),
                BitOffset = I32(p + 4),
                ShortName = Encoding.ASCII.GetString(Data, p + 8, 4),
                Depth = I32(p + 12),
            });
        foreach (var f in fields) f.Name = f.ShortName;
        fields.Sort((a, b) => a.BitOffset.CompareTo(b.BitOffset));
        int recordsStart = p;
        int recordsEnd = recordsStart + records * recordSize;
        int stringsStart = recordsEnd, crcRecords = recordsEnd;
        T3Huffman huff = null;
        if (fields.Any(f => f.IsCompressed) && records > 0)
        {
            // the Huffman tree sits before the strings; its size is the smallest string offset any record uses
            int treeBytes = int.MaxValue;
            for (int r = 0; r < written; r++)
                foreach (var f in fields.Where(f => f.IsCompressed))
                {
                    int o = I32(recordsStart + r * recordSize + f.BitOffset / 8);
                    if (o != -1 && o < treeBytes) treeBytes = o;
                }
            if (treeBytes == int.MaxValue) treeBytes = 0;
            huff = new T3Huffman(Data, recordsEnd, treeBytes / 4);
            stringsStart = recordsEnd;
            crcRecords = (recordsEnd + stringsLength + 3) & ~3;
        }
        var table = new T3Table
        {
            ShortName = shortName, Name = shortName, RecordSize = recordSize, Records = records, WrittenRecords = written, CancelledRecords = cancelled,
            Start = start, CrcHeaderPos = crcHeader, RecordsStart = recordsStart, StringsStart = stringsStart, StringsLength = stringsLength, CrcRecordsPos = crcRecords,
            Huffman = huff,
        };
        table.Fields.AddRange(fields);
        return table;
    }

    private void ApplyMeta(string xml)
    {
        var doc = XDocument.Parse(xml);
        var tables = doc.Descendants("table").ToDictionary(t => (string)t.Attribute("shortname") ?? "", t => t);
        foreach (var table in Tables)
        {
            if (!tables.TryGetValue(table.ShortName, out var t)) continue;
            table.Name = (string)t.Attribute("name") ?? table.ShortName;
            var fields = t.Descendants("field").ToDictionary(f => (string)f.Attribute("shortname") ?? "", f => f);
            foreach (var f in table.Fields)
            {
                if (!fields.TryGetValue(f.ShortName, out var x)) continue;
                f.Name = (string)x.Attribute("name") ?? f.ShortName;
                if (int.TryParse((string)x.Attribute("rangelow"), out int lo)) f.RangeLow = lo;
                if (int.TryParse((string)x.Attribute("rangehigh"), out int hi)) f.RangeHigh = hi;
                f.Key = string.Equals((string)x.Attribute("key"), "True", StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public T3Table Table(string name) => Tables.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || t.ShortName == name);

    private int RecordPos(T3Table t, int record) => t.RecordsStart + record * t.RecordSize;

    /// <summary>A deleted or empty record (its last byte's top bit).</summary>
    public bool IsInvalid(T3Table t, int record) => record >= t.WrittenRecords || (Data[RecordPos(t, record) + t.RecordSize - 1] & 0x80) != 0;

    public object Get(T3Table t, int record, T3Field f)
    {
        int rp = RecordPos(t, record);
        switch (f.Type)
        {
            case T3FieldType.Integer:
                return (int)(ReadBits(rp, f.BitOffset, f.Depth) + f.RangeLow);
            case T3FieldType.Float:
                return BitConverter.ToSingle(Data, rp + f.BitOffset / 8);
            case T3FieldType.String:
            {
                int len = f.Depth / 8, s = rp + f.BitOffset / 8;
                int e = Array.IndexOf(Data, (byte)0, s, len);
                return Encoding.UTF8.GetString(Data, s, (e < 0 ? s + len : e) - s);
            }
            default:
            {
                int off = I32(rp + f.BitOffset / 8);
                if (off == -1 || t.Huffman == null) return "";
                int p = t.StringsStart + off;
                int n = f.Type == T3FieldType.ShortCompressedString ? Data[p] : BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(p));
                p += f.Type == T3FieldType.ShortCompressedString ? 1 : 2;
                return t.Huffman.Read(Data, p, n);
            }
        }
    }

    /// <summary>
    /// Changes a value in place and recomputes the table's records CRC. Integers must be in the field's range; a fixed
    /// string must fit its width; a compressed string must encode to no more bytes than the one it replaces (nothing moves).
    /// </summary>
    public void Set(T3Table t, int record, T3Field f, object value) => SetCore(t, record, f, value);

    /// <summary>
    /// Bytes a text takes in a string field and the bytes the field has for it (strings are edited in place, so the new
    /// text must fit what the old one used). Need is -1 when the text has a character the table can't encode.
    /// </summary>
    public (int Need, int Have) Budget(T3Table t, int record, T3Field f, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        if (f.Type == T3FieldType.String) return (bytes.Length + 1, f.Depth / 8);
        if (!f.IsCompressed) return (0, 0);
        int off = I32(RecordPos(t, record) + f.BitOffset / 8);
        if (off == -1 || t.Huffman == null) return (bytes.Length, 0);
        var enc = t.Huffman.Encode(bytes);
        return (enc?.Length ?? -1, StringUsers(t, off) > 1 ? 0 : Room(t, f, off));
    }

    private readonly Dictionary<T3Table, (int[] Offsets, Dictionary<int, int> Users)> _strings = new();

    /// <summary>The compressed-string offsets every written record of a table uses (sorted), and how many fields point at each.</summary>
    private (int[] Offsets, Dictionary<int, int> Users) Strings(T3Table t)
    {
        if (_strings.TryGetValue(t, out var s)) return s;
        var users = new Dictionary<int, int>();
        foreach (var f in t.Fields.Where(f => f.IsCompressed))
            for (int r = 0; r < t.WrittenRecords; r++)
            {
                int o = I32(RecordPos(t, r) + f.BitOffset / 8);
                if (o != -1) users[o] = users.GetValueOrDefault(o) + 1;
            }
        var offsets = users.Keys.ToArray();
        Array.Sort(offsets);
        return _strings[t] = (offsets, users);
    }

    /// <summary>How many string fields of the table point at the string stored at <paramref name="offset"/> (more than one: stored once, shared).</summary>
    public int StringUsers(T3Table t, int offset) => Strings(t).Users.GetValueOrDefault(offset);

    /// <summary>
    /// The bytes a compressed string at <paramref name="offset"/> can take: up to where the next string starts (or the
    /// strings area ends), so a name made shorter keeps its space for a later, longer one. Never less than what it uses.
    /// </summary>
    private int Room(T3Table t, T3Field f, int offset)
    {
        int head = f.Type == T3FieldType.ShortCompressedString ? 1 : 2;
        int p = t.StringsStart + offset;
        int n = head == 1 ? Data[p] : BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(p));
        int used = t.Huffman.EncodedLength(Data, p + head, n);
        var offsets = Strings(t).Offsets;
        int i = Array.BinarySearch(offsets, offset);
        int next = i >= 0 && i + 1 < offsets.Length ? offsets[i + 1] : t.StringsLength;
        return Math.Max(used, next - offset - head);
    }

    private void SetCore(T3Table t, int record, T3Field f, object value)
    {
        int rp = RecordPos(t, record);
        switch (f.Type)
        {
            case T3FieldType.Integer:
            {
                int v = Convert.ToInt32(value);
                long raw = (long)v - f.RangeLow;
                if (raw < 0 || raw >= (1L << f.Depth)) throw new ArgumentOutOfRangeException(nameof(value), $"{f.Name} takes {f.RangeLow} to {f.RangeLow + (1L << f.Depth) - 1}.");
                WriteBits(rp, f.BitOffset, f.Depth, raw);
                break;
            }
            case T3FieldType.Float:
                BinaryPrimitives.WriteSingleLittleEndian(Data.AsSpan(rp + f.BitOffset / 8), Convert.ToSingle(value));
                break;
            case T3FieldType.String:
            {
                var bytes = Encoding.UTF8.GetBytes((string)value ?? "");
                int len = f.Depth / 8;
                if (bytes.Length >= len) throw new ArgumentException($"{f.Name} holds at most {len - 1} bytes.");
                Array.Clear(Data, rp + f.BitOffset / 8, len);
                bytes.CopyTo(Data, rp + f.BitOffset / 8);
                break;
            }
            default:
            {
                int off = I32(rp + f.BitOffset / 8);
                if (off == -1 || t.Huffman == null) throw new NotSupportedException("This string is empty in the game; it can't be given text in place.");
                int p = t.StringsStart + off;
                bool shortLen = f.Type == T3FieldType.ShortCompressedString;
                int oldN = shortLen ? Data[p] : BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(p));
                int head = shortLen ? 1 : 2;
                int oldBytes = t.Huffman.EncodedLength(Data, p + head, oldN);
                if (StringUsers(t, off) > 1)
                    throw new NotSupportedException($"This text is stored once for {StringUsers(t, off)} values; changing it would change them all.");
                int room = Room(t, f, off);
                var text = Encoding.UTF8.GetBytes((string)value ?? "");
                var enc = t.Huffman.Encode(text) ?? throw new ArgumentException("The text uses characters this table's strings never use.");
                if (enc.Length > room) throw new ArgumentException($"The new text needs {enc.Length} bytes compressed; there's room for {room}. Shorten it.");
                if (shortLen) { if (text.Length > 255) throw new ArgumentException("Too long."); Data[p] = (byte)text.Length; }
                else BinaryPrimitives.WriteUInt16BigEndian(Data.AsSpan(p), (ushort)text.Length);
                Array.Clear(Data, p + head, oldBytes);
                enc.CopyTo(Data, p + head);
                break;
            }
        }
        if (_batch != null) _batch.Add(t);
        else FixCrc(t);
    }

    private HashSet<T3Table> _batch;

    /// <summary>
    /// Many edits at once: until the returned object is disposed, Set doesn't recompute the table's records CRC after
    /// each value (it covers the whole table); each changed table's CRC is recomputed once at the end.
    /// </summary>
    public IDisposable Batch()
    {
        if (_batch != null) return new BatchEnd(null);
        _batch = new HashSet<T3Table>();
        return new BatchEnd(this);
    }

    private sealed class BatchEnd(T3Db db) : IDisposable
    {
        public void Dispose()
        {
            if (db?._batch == null) return;
            foreach (var t in db._batch) db.FixCrc(t);
            db._batch = null;
        }
    }

    private long ReadBits(int recordPos, int bitOffset, int depth)
    {
        long v = 0;
        for (int i = 0; i < depth; i++)
        {
            int bit = bitOffset + i;
            if ((Data[recordPos + (bit >> 3)] >> (bit & 7) & 1) != 0) v |= 1L << i;
        }
        return v;
    }

    private void WriteBits(int recordPos, int bitOffset, int depth, long value)
    {
        for (int i = 0; i < depth; i++)
        {
            int bit = bitOffset + i, b = recordPos + (bit >> 3);
            if (((value >> i) & 1) != 0) Data[b] |= (byte)(1 << (bit & 7));
            else Data[b] &= (byte)~(1 << (bit & 7));
        }
    }

    // ---------------------------------------------------------------- CRCs

    /// <summary>EA's database CRC: CRC-32 (0x04C11DB7) most significant bit first, starting at -1, no final xor.</summary>
    public static int Crc(ReadOnlySpan<byte> bytes)
    {
        int crc = -1;
        foreach (byte b in bytes)
        {
            crc ^= b << 24;
            for (int k = 0; k < 8; k++) crc = crc < 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
        }
        return crc;
    }

    /// <summary>The span each CRC covers: from the end of the previous CRC (or the signature) to the CRC.</summary>
    private IEnumerable<(int From, int CrcPos)> CrcSpans()
    {
        yield return (0, CrcHeaderPos);
        yield return (CrcHeaderPos + 4, CrcNamesPos);
        int prev = CrcNamesPos + 4;
        foreach (var t in Tables.OrderBy(t => t.Start))
        {
            yield return (prev, t.CrcHeaderPos);
            yield return (t.CrcHeaderPos + 4, t.CrcRecordsPos);
            prev = t.CrcRecordsPos + 4;
        }
    }

    /// <summary>Each CRC of the file, what it covers and whether it matches its bytes.</summary>
    public List<(string What, int From, int CrcPos, bool Ok)> CrcReport()
    {
        var list = new List<(string, int, int, bool)>();
        foreach (var (from, pos) in CrcSpans())
        {
            var t = Tables.FirstOrDefault(x => x.CrcHeaderPos == pos || x.CrcRecordsPos == pos);
            string what = t == null ? (pos == CrcHeaderPos ? "file header" : "table names") : $"{t.Name} {(pos == t.CrcHeaderPos ? "header" : "records")}";
            list.Add((what, from, pos, Crc(Data.AsSpan(from, pos - from)) == I32(pos)));
        }
        return list;
    }

    /// <summary>How many of the file's CRCs match its bytes (all of them, for a file this reader understands).</summary>
    public (int Ok, int Total) CheckCrcs()
    {
        int ok = 0, total = 0;
        foreach (var (from, pos) in CrcSpans())
        {
            total++;
            if (Crc(Data.AsSpan(from, pos - from)) == I32(pos)) ok++;
        }
        return (ok, total);
    }

    /// <summary>
    /// Puts a table's written records in a new order (<paramref name="order"/>[i] is the record that goes to place i; every
    /// written record exactly once). Records are moved whole, so the table's size and every other table stay as they are;
    /// the records CRC is recomputed. Used to keep a table sorted by its key after keys change.
    /// </summary>
    public void Reorder(T3Table t, IReadOnlyList<int> order)
    {
        if (order.Count != t.WrittenRecords || order.Distinct().Count() != order.Count || order.Any(i => i < 0 || i >= t.WrittenRecords))
            throw new ArgumentException("The new order must name every written record once.");
        var copy = Data.AsSpan(t.RecordsStart, t.WrittenRecords * t.RecordSize).ToArray();
        for (int i = 0; i < order.Count; i++)
            copy.AsSpan(order[i] * t.RecordSize, t.RecordSize).CopyTo(Data.AsSpan(t.RecordsStart + i * t.RecordSize));
        FixCrc(t);
    }

    /// <summary>
    /// Changes how many of a table's records are in use (the header's written count), within the room the table already
    /// has (<see cref="T3Table.Records"/>), so nothing moves. Records past the new count are zeroed, as unused ones are in
    /// the game's files; records it newly takes in keep their bytes for the caller to fill. Both CRCs are recomputed.
    /// </summary>
    public void SetWritten(T3Table t, int count)
    {
        if (count < 0 || count > t.Records) throw new ArgumentOutOfRangeException(nameof(count), $"{t.Name} has room for {t.Records} records.");
        if (count < t.WrittenRecords)
            Array.Clear(Data, RecordPos(t, count), (t.WrittenRecords - count) * t.RecordSize);
        BinaryPrimitives.WriteUInt16LittleEndian(Data.AsSpan(t.Start + 18), (ushort)count);
        t.WrittenRecords = count;
        var (from, pos) = CrcSpans().First(s => s.CrcPos == t.CrcHeaderPos);
        BinaryPrimitives.WriteInt32LittleEndian(Data.AsSpan(pos), Crc(Data.AsSpan(from, pos - from)));
        FixCrc(t);
    }

    /// <summary>A copy of a record's bytes.</summary>
    public byte[] RecordBytes(T3Table t, int record) => Data.AsSpan(RecordPos(t, record), t.RecordSize).ToArray();

    /// <summary>Overwrites a record (within the written count) with bytes from <see cref="RecordBytes"/>.</summary>
    public void WriteRecord(T3Table t, int record, byte[] bytes)
    {
        if (bytes.Length != t.RecordSize || record < 0 || record >= t.WrittenRecords) throw new ArgumentException("Not a record of this table.");
        bytes.CopyTo(Data.AsSpan(RecordPos(t, record)));
        if (_batch != null) _batch.Add(t);
        else FixCrc(t);
    }

    private void FixCrc(T3Table t)
    {
        int from = t.CrcHeaderPos + 4;
        BinaryPrimitives.WriteInt32LittleEndian(Data.AsSpan(t.CrcRecordsPos), Crc(Data.AsSpan(from, t.CrcRecordsPos - from)));
    }
}

/// <summary>A table's Huffman tree for compressed strings: nodes of [child0, leaf0, child1, leaf1]; child 0 means a leaf.</summary>
internal sealed class T3Huffman
{
    private readonly byte[,] _child, _leaf;
    private readonly ushort[] _code = new ushort[256];
    private readonly byte[] _bits = new byte[256];
    private readonly int _nodes;

    public T3Huffman(byte[] data, int pos, int nodes)
    {
        _nodes = nodes;
        _child = new byte[nodes, 2];
        _leaf = new byte[nodes, 2];
        for (int i = 0; i < nodes; i++, pos += 4)
        {
            _child[i, 0] = data[pos]; _leaf[i, 0] = data[pos + 1];
            _child[i, 1] = data[pos + 2]; _leaf[i, 1] = data[pos + 3];
        }
        var code = new ushort[Math.Max(1, nodes)];
        var depth = new byte[Math.Max(1, nodes)];
        for (int i = 0; i < nodes; i++)
            for (int side = 0; side < 2; side++)
            {
                ushort c = (ushort)(code[i] * 2 + side);
                byte d = (byte)(depth[i] + 1);
                byte child = _child[i, side];
                if (child != 0) { code[child] = c; depth[child] = d; }
                else { _code[_leaf[i, side]] = c; _bits[_leaf[i, side]] = d; }
            }
    }

    public string Read(byte[] data, int pos, int length)
    {
        if (length <= 0) return "";
        if (_nodes == 0) return Encoding.UTF8.GetString(data, pos, length);
        var outBytes = new byte[length];
        int n = 0, node = 0;
        while (n < length)
        {
            byte b = data[pos++];
            for (int bit = 7; bit >= 0 && n < length; bit--)
            {
                int side = (b >> bit) & 1;
                int child = _child[node, side];
                if (child == 0) { outBytes[n++] = _leaf[node, side]; node = 0; }
                else node = child;
            }
        }
        return Encoding.UTF8.GetString(outBytes);
    }

    /// <summary>Bytes the encoded form of a stored string of <paramref name="length"/> characters takes.</summary>
    public int EncodedLength(byte[] data, int pos, int length)
    {
        if (_nodes == 0) return length;
        int n = 0, node = 0, bytes = 0;
        while (n < length)
        {
            byte b = data[pos + bytes++];
            for (int bit = 7; bit >= 0 && n < length; bit--)
            {
                int child = _child[node, (b >> bit) & 1];
                if (child == 0) { n++; node = 0; }
                else node = child;
            }
        }
        return bytes;
    }

    /// <summary>The encoded bytes, or null if a character has no code in this tree.</summary>
    public byte[] Encode(byte[] text)
    {
        if (_nodes == 0) return text;
        var outBytes = new List<byte>();
        int cur = 0, used = 0;
        foreach (byte c in text)
        {
            if (_bits[c] == 0) return null;
            for (int i = _bits[c] - 1; i >= 0; i--)
            {
                cur = (cur << 1) | ((_code[c] >> i) & 1);
                if (++used == 8) { outBytes.Add((byte)cur); cur = 0; used = 0; }
            }
        }
        if (used > 0) outBytes.Add((byte)(cur << (8 - used)));
        return outBytes.ToArray();
    }
}
