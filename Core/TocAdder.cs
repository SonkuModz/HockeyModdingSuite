using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>A bundle to add to a TOC: its name, its block (record and asset locations) and its chunks' chunk-table entries.</summary>
public sealed record AddedBundle(string Name, byte[] Block, IReadOnlyList<(Guid Id, byte[] Entry)> Chunks);

/// <summary>
/// Adds bundles to an NHL 24 (PS4) superbundle TOC with Huffman-coded names (header of 15 ints after the signature; all
/// big-endian, offsets from the end of the signature):
///   [0] bundle hash map (n ints), [1] bundle entries (16 bytes: name bit offset, 0x40000000 | block size, block offset
///   i64), [2] n, [3] chunk hash map (m ints), [4] chunk guids (20 bytes: reversed guid, entry int offset), [5] m,
///   [6] chunk entries (12 bytes: location group, offset, size), [8] name data (ints), [10] chunk entry ints, [11] flags,
///   [12] name data ints, [13] tree ints, [14] tree; then each bundle's block.
/// Both hash maps are minimal perfect hashes: slot = H(key) % n; a negative value v there is the index -1-v, otherwise
/// the index is H(key, seed v) % n. H is FNV-1 over signed bytes, mod 0x01000193. A bundle's key is its name; a chunk's
/// is its guid's bytes.
///
/// Nothing that other mods patch in place moves: the chunk entries and the bundle blocks stay where they are. The rest
/// is rewritten or copied to the end of the file: the two hash maps are rebuilt in place (each grows into the table after
/// it, which moves to the end), the guid table, the names (with the new ones after them), the tree, the new blocks and
/// the bundle entries go at the end, and the new chunk entries go right after the old ones (where the names were).
///
/// A TOC rebuilt with plain names (the Center Ice Mod's contentsb: a 12-int header, flags without bit 4) is handled the
/// same way: its names are NUL-terminated strings and a bundle entry's name offset is a byte offset into them, so the
/// copy at the end is the old block of names with the new ones after it, and there is no tree.
/// </summary>
public static class TocAdder
{
    private const uint Prime = 0x01000193, Basis = 0x811C9DC5;

    public static uint Hash(ReadOnlySpan<byte> key, uint seed = Basis)
    {
        uint x = seed;
        foreach (byte b in key) x = (x * Prime) ^ (uint)(sbyte)b;
        return x % Prime;
    }

    public static int Lookup(int[] table, ReadOnlySpan<byte> key)
    {
        uint n = (uint)table.Length;
        int v = table[Hash(key) % n];
        return v < 0 ? -1 - v : (int)(Hash(key, (uint)v) % n);
    }

    /// <summary>A minimal perfect hash of the keys: the table, and the index each key gets.</summary>
    public static (int[] Table, int[] IndexOf) BuildMap(IReadOnlyList<byte[]> keys)
    {
        int n = keys.Count;
        var table = new int[n];
        var index = new int[n];
        var used = new bool[n];
        var buckets = new List<int>[n];
        for (int i = 0; i < n; i++) (buckets[Hash(keys[i]) % (uint)n] ??= new()).Add(i);
        var slots = new List<int>();
        foreach (int b in Enumerable.Range(0, n).Where(b => buckets[b]?.Count > 1).OrderByDescending(b => buckets[b].Count))
        {
            for (uint d = 1; ; d++)
            {
                if (d > 10_000_000) throw new InvalidDataException("No seed places a bucket of the bundle hash map.");
                slots.Clear();
                bool ok = true;
                foreach (int k in buckets[b])
                {
                    int s = (int)(Hash(keys[k], d) % (uint)n);
                    if (used[s] || slots.Contains(s)) { ok = false; break; }
                    slots.Add(s);
                }
                if (!ok) continue;
                for (int j = 0; j < slots.Count; j++) { used[slots[j]] = true; index[buckets[b][j]] = slots[j]; }
                table[b] = (int)d;
                break;
            }
        }
        int free = 0;
        foreach (int b in Enumerable.Range(0, n).Where(b => buckets[b]?.Count == 1))
        {
            while (used[free]) free++;
            used[free] = true;
            index[buckets[b][0]] = free;
            table[b] = -1 - free;
        }
        return (table, index);
    }

    /// <summary>A parsed TOC: its header, names, and raw tables.</summary>
    public sealed class Toc
    {
        public byte[] Bytes;
        public int Base;
        public int[] H = new int[15];
        public int[] BundleMap, ChunkMap, NameData, Tree;
        public string[] Names;
        public int[] NameBits, SizeFields;
        public long[] Blocks;
        public byte[][] GuidRecords; // 20 bytes each
        /// <summary>Plain names (12-int header): NameBits are byte offsets into <see cref="PlainNames"/>.</summary>
        public bool Plain;
        public byte[] PlainNames;
        public int N => H[2];
        public int M => H[5];

        public int I(int rel) => BinaryPrimitives.ReadInt32BigEndian(Bytes.AsSpan(Base + rel));

        public static Toc Read(byte[] b)
        {
            var t = new Toc { Bytes = b, Base = DbObject.DataStart(b) };
            int header = t.I(0);
            if (header is not (0x3C or 0x30)) throw new InvalidDataException("Not a superbundle TOC this understands.");
            for (int i = 0; i < header / 4; i++) t.H[i] = t.I(4 * i);
            t.Plain = header == 0x30;
            if (t.Plain ? (t.H[11] & 4) != 0 : (t.H[11] & 4) == 0) throw new InvalidDataException("The TOC's name flag doesn't match its header.");
            int n = t.N, m = t.M;
            int[] Ints(int rel, int count) { var a = new int[count]; for (int i = 0; i < count; i++) a[i] = t.I(rel + 4 * i); return a; }
            t.BundleMap = Ints(t.H[0], n);
            t.ChunkMap = Ints(t.H[3], m);
            if (!t.Plain)
            {
                t.NameData = Ints(t.H[8], t.H[12]);
                t.Tree = Ints(t.H[14], t.H[13]);
            }
            t.NameBits = new int[n]; t.SizeFields = new int[n]; t.Blocks = new long[n]; t.Names = new string[n];
            int namesEnd = 0;
            for (int i = 0; i < n; i++)
            {
                int p = t.H[1] + 16 * i;
                t.NameBits[i] = t.I(p);
                t.SizeFields[i] = t.I(p + 4);
                t.Blocks[i] = BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(t.Base + p + 8));
                if (t.Plain)
                {
                    int s = t.Base + t.H[8] + t.NameBits[i], e = Array.IndexOf(b, (byte)0, s);
                    if (t.NameBits[i] < 0 || e < 0) throw new InvalidDataException($"Bundle {i}'s name isn't in the TOC.");
                    t.Names[i] = Encoding.UTF8.GetString(b, s, e - s);
                    namesEnd = Math.Max(namesEnd, e + 1 - t.Base - t.H[8]);
                }
                else t.Names[i] = Decode(t.Tree, t.NameData, t.NameBits[i]);
            }
            if (t.Plain) t.PlainNames = b[(t.Base + t.H[8])..(t.Base + t.H[8] + namesEnd)];
            t.GuidRecords = new byte[m][];
            for (int i = 0; i < m; i++) t.GuidRecords[i] = b.AsSpan(t.Base + t.H[4] + 20 * i, 20).ToArray();
            return t;
        }

        public static byte[] ChunkKey(ReadOnlySpan<byte> record) { var k = record[..16].ToArray(); Array.Reverse(k); return k; }

        /// <summary>Problems with the TOC as it is: every name and guid must be found through its hash map.</summary>
        public List<string> Check()
        {
            var bad = new List<string>();
            for (int i = 0; i < N; i++)
                if (Lookup(BundleMap, Encoding.UTF8.GetBytes(Names[i])) != i) { bad.Add($"bundle {i} {Names[i]} isn't found by its hash"); if (bad.Count > 5) return bad; }
            for (int i = 0; i < M; i++)
                if (Lookup(ChunkMap, ChunkKey(GuidRecords[i])) != i) { bad.Add($"chunk guid {i} isn't found by its hash"); if (bad.Count > 5) return bad; }
            return bad;
        }
    }

    public static string Decode(int[] tree, int[] data, int bit)
    {
        var sb = new StringBuilder();
        while (true)
        {
            int node = tree.Length / 2 - 1;
            do
            {
                int v = (data[bit / 32] >> (bit % 32)) & 1;
                node = tree[node * 2 + v];
                bit++;
            }
            while (node >= 0);
            char c = (char)(-1 - node);
            if (c == '\0') return sb.ToString();
            sb.Append(c);
        }
    }

    /// <summary>Each character's bits (root first) in the tree.</summary>
    private static Dictionary<char, bool[]> Codes(int[] tree)
    {
        var codes = new Dictionary<char, bool[]>();
        void Walk(int node, List<bool> path)
        {
            for (int v = 0; v < 2; v++)
            {
                int next = tree[node * 2 + v];
                path.Add(v == 1);
                if (next < 0) codes[(char)(-1 - next)] = path.ToArray();
                else Walk(next, path);
                path.RemoveAt(path.Count - 1);
            }
        }
        Walk(tree.Length / 2 - 1, new List<bool>());
        return codes;
    }

    public sealed class Result
    {
        public List<PatchData> Patches { get; } = new();
        public List<string> Notes { get; } = new();
        /// <summary>The whole TOC as it is after the patches (for checks before anything is written).</summary>
        public byte[] After { get; set; }
    }

    /// <param name="rel">The TOC's path relative to the game folder, for the patches.</param>
    public static Result Plan(string rel, byte[] bytes, IReadOnlyList<AddedBundle> add)
    {
        var t = Toc.Read(bytes);
        var r = new Result();
        int n = t.N, m = t.M, k = add.Count, kc = add.Sum(a => a.Chunks.Count);
        var h = t.H;
        // the layout this relies on
        if (!(h[0] < h[1] && h[1] < h[3] && h[3] < h[4] && h[4] < h[6] && h[6] < h[8] && (t.Plain || h[8] < h[14])))
            throw new InvalidDataException("The TOC's tables aren't in the order this expects.");
        // names right after the chunk entries (a rebuilt TOC may pad them to 16 bytes), and the tree right after the names
        int pad = h[8] - (h[6] + m * 12);
        if (h[10] != m * 3 || pad < 0 || pad >= 16 || (!t.Plain && pad != 0) || (!t.Plain && h[8] + h[12] * 4 != h[14]))
            throw new InvalidDataException("The TOC's chunk entries aren't right before its names, or its names right before the tree.");
        int nameRoom = (t.Plain ? t.PlainNames.Length : h[12] * 4) + pad;
        if (h[0] + (n + k) * 4 > h[3] || h[3] + (m + kc) * 4 > h[6] || kc * 12 > nameRoom)
            throw new InvalidDataException("The hash maps don't fit where they are.");
        if (t.SizeFields.Any(s => (s & 0x7F000000) != 0x40000000))
            throw new InvalidDataException("A bundle entry's size field isn't 0x40000000 | size.");
        int guidTop = t.GuidRecords.Length > 0 ? t.GuidRecords[0][16] : 1;
        if (t.GuidRecords.Any(g => g[16] != guidTop)) throw new InvalidDataException("The chunk guids' flag bytes differ.");
        var bad = t.Check();
        if (bad.Count > 0) throw new InvalidDataException("The TOC's hash maps don't find its own entries: " + string.Join("; ", bad));
        var names = new HashSet<string>(t.Names, StringComparer.Ordinal);
        foreach (var a in add)
            if (!names.Add(a.Name)) throw new InvalidDataException($"The TOC already has a bundle {a.Name}.");
        var guids = new HashSet<Guid>(t.GuidRecords.Select(g => new Guid(Toc.ChunkKey(g))));
        foreach (var c in add.SelectMany(a => a.Chunks))
            if (!guids.Add(c.Id)) throw new InvalidDataException($"The TOC already has a chunk {c.Id}.");

        // names: the old data, then the new names' bits (plain: the old strings, then the new ones)
        var newBits = new int[k];
        int[] data = null;
        byte[] plainNames = null;
        if (t.Plain)
        {
            var ms = new MemoryStream();
            ms.Write(t.PlainNames);
            for (int i = 0; i < k; i++)
            {
                newBits[i] = (int)ms.Length;
                ms.Write(Encoding.UTF8.GetBytes(add[i].Name));
                ms.WriteByte(0);
            }
            plainNames = ms.ToArray();
        }
        else
        {
            var codes = Codes(t.Tree);
            var bits = new List<bool>();
            int start = h[12] * 32;
            for (int i = 0; i < k; i++)
            {
                newBits[i] = start + bits.Count;
                foreach (char c in add[i].Name + "\0")
                    bits.AddRange(codes.TryGetValue(c, out var code) ? code : throw new InvalidDataException($"The TOC's name tree has no '{c}'."));
            }
            data = new int[h[12] + (bits.Count + 31) / 32];
            t.NameData.CopyTo(data, 0);
            for (int i = 0; i < bits.Count; i++)
                if (bits[i]) data[h[12] + i / 32] |= 1 << (i % 32);
        }

        // the end of the file: guids, names, tree, blocks, bundle entries
        long fileEnd = bytes.Length;
        var tail = new MemoryStream();
        long At() => fileEnd + tail.Length - t.Base;
        void Align(int a) { while ((fileEnd + tail.Length) % a != 0) tail.WriteByte(0); }
        void Int(int v) { Span<byte> s = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(s, v); tail.Write(s); }
        void Long(long v) { Span<byte> s = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(s, v); tail.Write(s); }
        Align(16);

        // chunk map and guid table
        var chunkKeys = t.GuidRecords.Select(g => Toc.ChunkKey(g)).Concat(add.SelectMany(a => a.Chunks).Select(c => c.Id.ToByteArray())).ToList();
        var chunkRecords = t.GuidRecords.Concat(add.SelectMany(a => a.Chunks).Select((c, j) =>
        {
            var g = new byte[20];
            var key = c.Id.ToByteArray();
            Array.Reverse(key);
            key.CopyTo(g, 0);
            BinaryPrimitives.WriteInt32BigEndian(g.AsSpan(16), (guidTop << 24) | ((m + j) * 3));
            return g;
        })).ToList();
        var (chunkMap, chunkIndex) = BuildMap(chunkKeys);
        var guidTable = new byte[(m + kc) * 20];
        for (int i = 0; i < m + kc; i++) chunkRecords[i].CopyTo(guidTable, chunkIndex[i] * 20);
        int guidAt = (int)At();
        tail.Write(guidTable);
        Align(16);
        int nameAt = (int)At();
        int treeAt = 0;
        if (t.Plain) tail.Write(plainNames);
        else
        {
            foreach (int v in data) Int(v);
            Align(16);
            treeAt = (int)At();
            foreach (int v in t.Tree) Int(v);
        }
        Align(16);
        var blockAt = new long[k];
        for (int i = 0; i < k; i++)
        {
            blockAt[i] = At();
            tail.Write(add[i].Block);
            Align(4);
        }
        Align(16);
        var bundleKeys = t.Names.Concat(add.Select(a => a.Name)).Select(s => Encoding.UTF8.GetBytes(s)).ToList();
        var (bundleMap, bundleIndex) = BuildMap(bundleKeys);
        var entries = new byte[(n + k) * 16];
        for (int i = 0; i < n + k; i++)
        {
            var e = entries.AsSpan(bundleIndex[i] * 16, 16);
            bool old = i < n;
            BinaryPrimitives.WriteInt32BigEndian(e, old ? t.NameBits[i] : newBits[i - n]);
            BinaryPrimitives.WriteInt32BigEndian(e[4..], old ? t.SizeFields[i] : 0x40000000 | add[i - n].Block.Length);
            BinaryPrimitives.WriteInt64BigEndian(e[8..], old ? t.Blocks[i] : blockAt[i - n]);
        }
        int entriesAt = (int)At();
        tail.Write(entries);
        if (fileEnd + tail.Length > int.MaxValue) throw new InvalidDataException("The TOC would pass 2 GB.");

        // in place: header and bundle map; chunk map; new chunk entries after the old ones
        var nh = (int[])h.Clone();
        nh[1] = entriesAt; nh[2] = n + k; nh[4] = guidAt; nh[5] = m + kc; nh[8] = nameAt; nh[10] = (m + kc) * 3;
        if (!t.Plain) { nh[12] = data.Length; nh[14] = treeAt; }
        var head = new byte[h[0] + (n + k) * 4];
        for (int i = 0; i < h[0] / 4; i++) BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(4 * i), nh[i]);
        for (int i = 0; i < n + k; i++) BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(h[0] + 4 * i), bundleMap[i]);
        var cmap = new byte[(m + kc) * 4];
        for (int i = 0; i < m + kc; i++) BinaryPrimitives.WriteInt32BigEndian(cmap.AsSpan(4 * i), chunkMap[i]);
        var centries = new byte[kc * 12];
        int ce = 0;
        foreach (var c in add.SelectMany(a => a.Chunks))
        {
            if (c.Entry.Length != 12) throw new ArgumentException("A chunk entry is 12 bytes.");
            c.Entry.CopyTo(centries, ce);
            ce += 12;
        }
        var after = new byte[fileEnd + tail.Length];
        bytes.CopyTo(after, 0);
        tail.ToArray().CopyTo(after, fileEnd);
        void InPlace(int relOffset, byte[] neu, string note)
        {
            int at = t.Base + relOffset;
            r.Patches.Add(new PatchData(rel, at, bytes[at..(at + neu.Length)], neu, note));
            neu.CopyTo(after, at);
        }
        InPlace(0, head, "toc header and bundle hash map");
        InPlace(h[3], cmap, "toc chunk hash map");
        if (kc > 0) InPlace(h[6] + m * 12, centries, "toc added chunk entries");
        r.Patches.Add(new PatchData(rel, fileEnd, Array.Empty<byte>(), tail.ToArray(), "toc moved tables and added bundles", Append: true));
        r.After = after;
        r.Notes.Add($"{rel}: {k} bundle(s) and {kc} chunk(s) added to {n:N0} and {m:N0}; {tail.Length:N0} bytes added at the end");
        return r;
    }
}
