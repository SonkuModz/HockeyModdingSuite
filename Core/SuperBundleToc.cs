using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// A data location in a CAS archive, and where a TOC stores it (big-endian u32 offset and size). Catalog is the
/// install chunk's index in <see cref="GameLayout.Catalogs"/>, or -1 when the TOC names a chunk the layout lacks.
/// </summary>
public readonly record struct TocRef(int Catalog, int Cas, bool Patch, uint Offset, uint Size, int OffsetPos, int SizePos);

/// <summary>A bundle listed in a superbundle TOC: its name, and the CAS locations of its record (entry 0) and assets.</summary>
public sealed class TocBundle
{
    public int Index { get; init; }
    public string Name { get; init; }
    /// <summary>Entry 0 is the bundle record; entries 1..n are its assets (ebx, then res, then chunks).</summary>
    public List<TocRef> Entries { get; } = new();
}

/// <summary>An entry of a TOC's chunk table.</summary>
public readonly record struct TocChunk(Guid Id, TocRef Ref);

/// <summary>
/// A superbundle TOC (Data/Ps5/*.toc, Patch/Ps5/*.toc). Layout (all big-endian, offsets from the TOC's start: 0 for
/// unsigned TOCs (PS5 Data), 556 after a signature header (PS5 Patch, PS4)): a 15-int header, bundle references and
/// entries (name offset, size, offset), the chunk table (flags, reversed guids with list indices, entries), Huffman-
/// compressed bundle names, and per bundle a block listing its record and assets with their CAS locations.
///
/// A location group names the CAS file. PS4: 4 bytes [unk][patch][catalog index][cas]; a chunk entry is 12 bytes and
/// a bundle entry flag of 1 starts a group. PS5: 8 bytes [unk][patch][install chunk persistentIndex, u32][unk][cas]; a
/// chunk entry is 16 bytes and flag bit 0x80 starts a group.
/// </summary>
public sealed class SuperBundleToc
{
    public string Path { get; }
    public bool Ps5 { get; }
    public List<TocBundle> Bundles { get; } = new();
    public List<TocChunk> Chunks { get; } = new();

    /// <param name="catalogOf">PS5: install chunk persistentIndex -> catalog index (-1 if unknown).</param>
    public SuperBundleToc(string path, byte[] bytes, Func<int, int> catalogOf = null)
    {
        Path = path;
        var b = bytes;
        int Base = DbObject.DataStart(b);
        int I(int p) => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(p));
        if (b.Length < Base + 60 || I(Base) != 0x3C)
            return;
        int bundleRefOffset = I(Base), bundleOffset = I(Base + 4), bundleCount = I(Base + 8);
        int chunkGuidOffset = I(Base + 16), chunkCount = I(Base + 20), chunkEntryOffset = I(Base + 24);
        int nameOffset = I(Base + 32);
        int flags = I(Base + 44), stringCount = I(Base + 48), stringSize = I(Base + 52), stringOffset = I(Base + 56);
        if (bundleCount == 0 && chunkCount == 0)
            return;

        // the chunk entries run up to the names (header int 8); their stride tells the format apart
        int stride = chunkCount > 0 ? (nameOffset - chunkEntryOffset) / chunkCount : 0;
        Ps5 = stride >= 16 || (chunkCount == 0 && catalogOf != null);
        int groupSize = Ps5 ? 8 : 4;

        // reads a location group at q: (catalog, cas, patch)
        (int Catalog, int Cas, bool Patch) Group(int q) => Ps5
            ? (catalogOf?.Invoke(I(q + 2)) ?? -1, b[q + 7], b[q + 1] != 0)
            : (b[q + 2], b[q + 3], b[q + 1] != 0);

        // bundles: name offset (bit index into the compressed names), size, offset of the bundle's block
        var nameOffsets = new int[bundleCount];
        var blockOffsets = new long[bundleCount];
        int p = Base + bundleOffset;
        for (int i = 0; i < bundleCount; i++, p += 16)
        {
            nameOffsets[i] = I(p);
            blockOffsets[i] = BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(p + 8));
        }
        string[] names = new string[bundleCount];
        if ((flags & 4) != 0 && stringCount > 0)
        {
            var data = new int[stringCount];
            for (int i = 0; i < stringCount; i++) data[i] = I(Base + nameOffset + 4 * i);
            var table = new int[stringSize];
            for (int i = 0; i < stringSize; i++) table[i] = I(Base + stringOffset + 4 * i);
            for (int i = 0; i < bundleCount; i++) names[i] = Huffman(table, data, nameOffsets[i]);
        }

        // chunk table: guid i carries (entry index * ints per entry) in its low 24 bits
        if (chunkCount > 0)
        {
            int intsPerEntry = stride / 4;
            var guids = new Guid[chunkCount];
            p = Base + chunkGuidOffset;
            Span<byte> g = stackalloc byte[16];
            for (int i = 0; i < chunkCount; i++, p += 20)
            {
                b.AsSpan(p, 16).CopyTo(g);
                g.Reverse();
                int index = (I(p + 16) & 0xFFFFFF) / intsPerEntry;
                if (index < chunkCount)
                    guids[index] = new Guid(g);
            }
            p = Base + chunkEntryOffset;
            for (int k = 0; k < chunkCount; k++, p += stride)
            {
                var (catalog, cas, patch) = Group(p);
                int q = p + groupSize;
                var r = new TocRef(catalog, cas, patch, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(q)),
                                   BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(q + 4)), q, q + 4);
                Chunks.Add(new TocChunk(guids[k], r));
            }
        }

        // each bundle's block: [unk][unk][flags offset][entry count][entries offset][header size]...; per entry a
        // flag byte says whether it starts a new location group; then offset and size
        for (int i = 0; i < bundleCount; i++)
        {
            var bundle = new TocBundle { Index = i, Name = names[i] ?? $"bundle{i}" };
            int start = Base + (int)blockOffsets[i];
            if (blockOffsets[i] < 0 || start + 32 > b.Length)
            {
                Bundles.Add(bundle);
                continue;
            }
            int flagsOffset = I(start + 8), entryCount = I(start + 12), entriesOffset = I(start + 16);
            int q = start + entriesOffset;
            (int Catalog, int Cas, bool Patch) group = (0, 0, false);
            for (int j = 0; j < entryCount; j++)
            {
                byte f = b[start + flagsOffset + j];
                if (Ps5 ? (f & 0x80) != 0 : f == 1)
                {
                    group = Group(q);
                    q += groupSize;
                }
                bundle.Entries.Add(new TocRef(group.Catalog, group.Cas, group.Patch, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(q)),
                                              BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(q + 4)), q, q + 4));
                q += 8;
            }
            Bundles.Add(bundle);
        }
    }

    private static string Huffman(int[] table, int[] data, int bit)
    {
        var sb = new StringBuilder();
        while (true)
        {
            int node = table.Length / 2 - 1;
            do
            {
                int v = (data[bit / 32] >> (bit % 32)) & 1;
                node = table[node * 2 + v];
                bit++;
            }
            while (node >= 0);
            char c = (char)(-1 - node);
            if (c == '\0')
                return sb.ToString();
            sb.Append(c);
        }
    }
}
