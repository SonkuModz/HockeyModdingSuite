using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// A legacy (pre-Frostbite) file: a byte range of a chunk, named by its path. <paramref name="Entry"/> is where its
/// 28-byte entry sits in the collector's index chunk.
/// </summary>
public sealed record LegacyFile(string Name, Guid ChunkId, int Offset, int Size, int Entry = -1);

/// <summary>
/// The legacy files NHL 27 keeps outside EBX (databases, their meta XML, lua, csv). The ChunkFileCollector EBX
/// (globals/chunkfilecollector) names an index chunk: a header of (count, offset) pairs, then 28-byte entries
/// [u64 FNV-1 64 hash of the path][i32 stored start][i32 stored end][i32 offset][i32 size][i32 chunk id index], a GUID
/// table and a pool of paths. The stored start and end are the range of the chunk's stored blocks (256 KB of data plus an
/// 8-byte header each) that holds the file, so the game can read just those. Nothing is cached: every read goes to the game files, so edits made anywhere show up.
/// </summary>
public sealed class Legacy
{
    private readonly AssetIndex _index;
    private readonly Dictionary<ulong, (int Offset, int Size, int Chunk, int Entry)> _entries = new();
    private readonly Guid[] _guids;
    public List<string> Names { get; } = new();

    public Guid IndexChunk { get; }
    public string Collector { get; }

    public const string Main = "globals/chunkfilecollector";

    /// <summary>The collectors the game has: the main one, plus the UI ones (fonts, nhluidata, swf assets).</summary>
    public static List<string> Collectors(AssetIndex index) =>
        index.Ebx.Values.Where(e => e.Type == "ChunkFileCollector").Select(e => e.Name).OrderBy(n => n).ToList();

    public Legacy(AssetIndex index, string collector = Main)
    {
        _index = index;
        Collector = collector;
        var ebx = index.GetEbx(collector) ?? throw new InvalidDataException($"The game has no {collector}.");
        var e = index.Read(ebx);
        // the collector's manifest chunk is the only chunk GUID in it
        IndexChunk = Guid.Empty;
        for (int p = 0; p + 16 <= e.Length; p++)
        {
            var g = new Guid(e.AsSpan(p, 16));
            if (g != Guid.Empty && index.Chunks.ContainsKey(g)) { IndexChunk = g; break; }
        }
        if (IndexChunk == Guid.Empty) throw new InvalidDataException("The chunk file collector names no chunk the game has.");
        var b = index.Read(index.GetChunk(IndexChunk));
        int n = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(12));
        int entries = (int)BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(16));
        int guids = (int)BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(48));
        int pool = (int)BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(56));
        int nameOffs = (int)BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(64));
        for (int k = 0; k < n; k++)
        {
            int p = entries + 28 * k;
            ulong h = BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(p));
            _entries[h] = (BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p + 16)), BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p + 20)), BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p + 24)), p);
        }
        _guids = new Guid[(nameOffs - guids) / 16];
        for (int i = 0; i < _guids.Length; i++) _guids[i] = new Guid(b.AsSpan(guids + 16 * i, 16));
        for (int i = 0; i < n && nameOffs + 4 * i + 4 <= b.Length; i++)
        {
            int s = pool + BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(nameOffs + 4 * i));
            if (s < pool || s >= b.Length) continue;
            int z = Array.IndexOf(b, (byte)0, s);
            Names.Add(Encoding.UTF8.GetString(b, s, (z < 0 ? b.Length : z) - s));
        }
    }

    public static ulong Fnv1(string s)
    {
        ulong h = 0xcbf29ce484222325;
        foreach (byte c in Encoding.UTF8.GetBytes(s)) h = (h * 0x100000001b3) ^ c;
        return h;
    }

    public LegacyFile Find(string name) =>
        _entries.TryGetValue(Fnv1(name), out var e) && e.Chunk >= 0 && e.Chunk < _guids.Length ? new LegacyFile(name, _guids[e.Chunk], e.Offset, e.Size, e.Entry) : null;

    /// <summary>Every file of this collector, with where it is.</summary>
    public IEnumerable<LegacyFile> Files() => Names.Select(Find).Where(f => f != null);

    public const int BlockSize = 0x40000, StoredBlockSize = BlockSize + 8;

    /// <summary>The stored-block range [start, end) that holds data bytes [offset, offset + size).</summary>
    public static (int Start, int End) StoredRange(int offset, int size) =>
        (offset / BlockSize * StoredBlockSize, ((offset + Math.Max(size, 1) - 1) / BlockSize + 1) * StoredBlockSize);

    /// <summary>The chunk holding a file, read now from the game files.</summary>
    public byte[] ReadChunk(Guid id) => _index.Read(_index.GetChunk(id) ?? throw new InvalidDataException($"The game has no chunk {id}."));

    public byte[] Read(LegacyFile f) => ReadChunk(f.ChunkId).AsSpan(f.Offset, f.Size).ToArray();

    public byte[] Read(string name) => Read(Find(name) ?? throw new FileNotFoundException($"No legacy file {name}."));

    /// <summary>A t3db database and its meta XML (name.db and name-meta.xml), read fresh from the game.</summary>
    public T3Db OpenDb(string name)
    {
        var f = Find(name) ?? throw new FileNotFoundException($"No legacy file {name}.");
        var chunk = ReadChunk(f.ChunkId);
        var metaFile = Find(name[..^3] + "-meta.xml");
        string meta = null;
        if (metaFile != null)
        {
            var mb = metaFile.ChunkId == f.ChunkId ? chunk.AsSpan(metaFile.Offset, metaFile.Size).ToArray() : Read(metaFile);
            meta = Encoding.UTF8.GetString(mb).TrimStart('﻿');
        }
        return new T3Db(chunk.AsSpan(f.Offset, f.Size).ToArray(), meta) { Source = f };
    }
}
