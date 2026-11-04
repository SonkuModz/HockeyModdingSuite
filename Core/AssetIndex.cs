using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>A bundle in the index: its superbundle TOC and its position there.</summary>
public sealed record BundleInfo(int Id, string Name, string Toc, int TocIndex, int BundleIndex, TocRef Record);

public abstract class AssetEntry
{
    public string Name { get; init; }
    /// <summary>The bundle the entry was found in (the first one if it is in several).</summary>
    public int Bundle { get; init; }
    /// <summary>Where the bundle's TOC says the data is (stock locations; see <see cref="AssetIndex"/>).</summary>
    public TocRef Ref { get; init; }
    public string Toc { get; init; }
    /// <summary>Absolute position of the asset's SHA1 in its bundle record (in the CAS file of the record).</summary>
    public long Sha1Pos { get; init; }
    public byte[] Sha1 { get; init; }
    public long OriginalSizePos { get; init; }
    public uint OriginalSize { get; init; }
    /// <summary>Every bundle that lists the asset (by bundle id).</summary>
    public List<int> Bundles { get; } = new();
}

public sealed class EbxEntry : AssetEntry
{
    /// <summary>The EBX file (partition) guid.</summary>
    public Guid Id { get; set; }
    public string Type { get; set; }
}

public sealed class ResEntry : AssetEntry
{
    public uint ResType { get; init; }
    public byte[] ResMeta { get; init; }
    public ulong ResRid { get; init; }
}

public sealed class ChunkEntry : AssetEntry
{
    public Guid Id { get; init; }
    public uint LogicalOffset { get; init; }
    public uint LogicalSize { get; init; }
    /// <summary>True for an entry of a TOC's chunk table (not a bundle).</summary>
    public bool InChunkTable { get; init; }
}

/// <summary>
/// Every asset of the game: EBX (with type and guid), RES and chunks, read from the superbundle TOCs and the bundle
/// records they point at. Like FMT's index it describes the STOCK data: TOCs are read from the kit's stock copies
/// when the game is modded (the caller passes them), so lookups stay valid after applies; what the game reads now is
/// found through the live TOCs (see DirectWriter). Built once and cached to disk.
/// </summary>
public sealed class AssetIndex
{
    public GameLayout Layout { get; }
    public List<BundleInfo> BundleList { get; } = new();
    public Dictionary<string, EbxEntry> Ebx { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ResEntry> Res { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Chunks by id: the bundle copy if a bundle lists the chunk, else the chunk table's.</summary>
    public Dictionary<Guid, ChunkEntry> Chunks { get; } = new();
    /// <summary>Chunk table entries by id (one per TOC that has it).</summary>
    public Dictionary<Guid, List<ChunkEntry>> TableChunks { get; } = new();

    public static IReadOnlyDictionary<string, string> TypeNames { get; }
    public static IReadOnlyDictionary<int, string> FormatNames { get; }

    static AssetIndex()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("typetables.json");
        using var doc = JsonDocument.Parse(s);
        TypeNames = doc.RootElement.GetProperty("types").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.OrdinalIgnoreCase);
        FormatNames = doc.RootElement.GetProperty("formats").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetString());
    }

    private AssetIndex(GameLayout layout) => Layout = layout;

    public EbxEntry GetEbx(string name) => Ebx.GetValueOrDefault(name);
    public ResEntry GetRes(string name) => Res.GetValueOrDefault(name);
    public ChunkEntry GetChunk(Guid id) => Chunks.GetValueOrDefault(id);

    /// <summary>
    /// Loads the cached index, or builds it. <paramref name="tocBytes"/> gives each superbundle TOC's stock bytes
    /// (path -> bytes); <paramref name="cacheFile"/> holds the index, keyed on the layout and those TOCs.
    /// </summary>
    public static AssetIndex Open(string gameDir, Func<string, byte[]> tocBytes, string cacheFile, Action<string> log)
    {
        var layout = new GameLayout(gameDir);
        var tocs = layout.AllTocPaths();
        var bytes = tocs.ToDictionary(t => t, tocBytes, StringComparer.OrdinalIgnoreCase);
        string key = CacheKey(layout, bytes);
        var index = new AssetIndex(layout);
        if (cacheFile != null && File.Exists(cacheFile) && index.LoadCache(cacheFile, key))
            return index;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        index.Build(tocs, bytes, log);
        log?.Invoke($"asset index built in {sw.Elapsed.TotalSeconds:0}s: {index.Ebx.Count} ebx, {index.Res.Count} res, {index.Chunks.Count} chunks");
        if (cacheFile != null)
            index.SaveCache(cacheFile, key);
        return index;
    }

    private static string CacheKey(GameLayout layout, Dictionary<string, byte[]> tocs)
    {
        using var sha = System.Security.Cryptography.SHA1.Create();
        var h = new StringBuilder("v2|");
        h.Append(new FileInfo(Path.Combine(layout.DataDir, "layout.toc")).Length);
        foreach (var (path, b) in tocs.OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
            h.Append('|').Append(Path.GetRelativePath(layout.GameDir, path)).Append(':').Append(Convert.ToHexString(sha.ComputeHash(b)));
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(h.ToString())));
    }

    // ---------------------------------------------------------------- building

    private void Build(List<string> tocs, Dictionary<string, byte[]> tocBytes, Action<string> log)
    {
        var ebxToType = new List<EbxEntry>();
        for (int t = 0; t < tocs.Count; t++)
        {
            var toc = new SuperBundleToc(tocs[t], tocBytes[tocs[t]], Layout.TocCatalogs);
            // a Patch TOC's bundles and chunks replace the ones Data's TOCs listed
            bool overlay = Layout.IsPatchPath(tocs[t]);
            foreach (var c in toc.Chunks)
            {
                var e = new ChunkEntry { Id = c.Id, Ref = c.Ref, Toc = tocs[t], Bundle = -1, InChunkTable = true, OriginalSize = c.Ref.Size };
                if (!TableChunks.TryGetValue(c.Id, out var list))
                    TableChunks[c.Id] = list = new List<ChunkEntry>();
                list.Add(e);
                if (overlay) Chunks[c.Id] = e;
                else Chunks.TryAdd(c.Id, e);
            }
            // bundle records live in CAS files; read each file once for all its records
            var records = new ConcurrentDictionary<int, BundleRecord>();
            Parallel.ForEach(toc.Bundles.Where(b => b.Entries.Count > 0).GroupBy(b => (b.Entries[0].Catalog, b.Entries[0].Cas, b.Entries[0].Patch)),
                new ParallelOptions { MaxDegreeOfParallelism = 8 }, group =>
                {
                    using var fs = new FileStream(Layout.CasPath(group.Key.Catalog, group.Key.Cas, group.Key.Patch), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    foreach (var b in group)
                    {
                        var r = b.Entries[0];
                        var data = new byte[r.Size];
                        fs.Position = r.Offset;
                        fs.ReadExactly(data);
                        records[b.Index] = BundleRecord.Read(data, r.Offset);
                    }
                });
            foreach (var b in toc.Bundles)
            {
                if (b.Entries.Count == 0 || !records.TryGetValue(b.Index, out var rec))
                    continue;
                int id = BundleList.Count;
                BundleList.Add(new BundleInfo(id, b.Name, tocs[t], t, b.Index, b.Entries[0]));
                int k = 1;
                foreach (var a in rec.Ebx)
                {
                    if (k >= b.Entries.Count) break;
                    var e = new EbxEntry { Name = a.Name, Bundle = id, Ref = b.Entries[k++], Toc = tocs[t], Sha1Pos = a.Sha1Pos, Sha1 = a.Sha1, OriginalSizePos = a.SizePos, OriginalSize = a.Size };
                    if (Ebx.TryGetValue(a.Name, out var had) && !overlay) had.Bundles.Add(id);
                    else { if (had != null) e.Bundles.AddRange(had.Bundles); e.Bundles.Add(id); Ebx[a.Name] = e; ebxToType.Add(e); }
                }
                foreach (var a in rec.Res)
                {
                    if (k >= b.Entries.Count) break;
                    var e = new ResEntry { Name = a.Name, Bundle = id, Ref = b.Entries[k++], Toc = tocs[t], Sha1Pos = a.Sha1Pos, Sha1 = a.Sha1, OriginalSizePos = a.SizePos, OriginalSize = a.Size, ResType = a.ResType, ResMeta = a.ResMeta, ResRid = a.ResRid };
                    if (Res.TryGetValue(a.Name, out var had) && !overlay) had.Bundles.Add(id);
                    else { if (had != null) e.Bundles.AddRange(had.Bundles); e.Bundles.Add(id); Res[a.Name] = e; }
                }
                foreach (var a in rec.Chunks)
                {
                    if (k >= b.Entries.Count) break;
                    var e = new ChunkEntry { Id = a.Id, Bundle = id, Ref = b.Entries[k++], Toc = tocs[t], Sha1Pos = a.Sha1Pos, Sha1 = a.Sha1, OriginalSizePos = a.SizePos, OriginalSize = a.Size, LogicalOffset = a.LogicalOffset, LogicalSize = a.Size };
                    if (Chunks.TryGetValue(a.Id, out var had) && !had.InChunkTable && !overlay) had.Bundles.Add(id);
                    else { if (had != null && !had.InChunkTable) e.Bundles.AddRange(had.Bundles); e.Bundles.Add(id); Chunks[a.Id] = e; }
                }
            }
            log?.Invoke($"  {Path.GetRelativePath(Layout.GameDir, tocs[t])}: {toc.Bundles.Count} bundles, {toc.Chunks.Count} table chunks");
        }

        // EBX type and guid come from the EBX data itself (EFIX): read per CAS file
        ebxToType = ebxToType.Where(e => ReferenceEquals(Ebx.GetValueOrDefault(e.Name), e)).ToList();
        Parallel.ForEach(ebxToType.GroupBy(e => (e.Ref.Catalog, e.Ref.Cas, e.Ref.Patch)), new ParallelOptions { MaxDegreeOfParallelism = 8 }, group =>
        {
            using var fs = new FileStream(Layout.CasPath(group.Key.Catalog, group.Key.Cas, group.Key.Patch), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            foreach (var e in group.OrderBy(e => e.Ref.Offset))
            {
                try
                {
                    var stored = new byte[e.Ref.Size];
                    fs.Position = e.Ref.Offset;
                    fs.ReadExactly(stored);
                    var (id, type) = EbxFile.ReadHeader(FrostbiteBlocks.Decode(stored));
                    e.Id = id;
                    e.Type = type != null && TypeNames.TryGetValue(type.Value.ToString(), out var n) ? n : type?.ToString() ?? "";
                }
                catch
                {
                    e.Type = "";
                }
            }
        });
    }

    // ---------------------------------------------------------------- cache

    private void SaveCache(string file, string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        using var w = new BinaryWriter(File.Create(file + ".tmp"));
        w.Write("NHL27Kit-index-1"); w.Write(key);
        w.Write(BundleList.Count);
        foreach (var b in BundleList) { w.Write(b.Name); w.Write(b.Toc); w.Write(b.TocIndex); w.Write(b.BundleIndex); WriteRef(w, b.Record); }
        void Base(AssetEntry e)
        {
            w.Write(e.Name ?? ""); w.Write(e.Bundle); WriteRef(w, e.Ref); w.Write(e.Toc); w.Write(e.Sha1Pos); w.Write(e.Sha1?.Length ?? 0);
            if (e.Sha1 != null) w.Write(e.Sha1);
            w.Write(e.OriginalSizePos); w.Write(e.OriginalSize);
            w.Write(e.Bundles.Count); foreach (int x in e.Bundles) w.Write(x);
        }
        w.Write(Ebx.Count);
        foreach (var e in Ebx.Values) { Base(e); w.Write(e.Id.ToByteArray()); w.Write(e.Type ?? ""); }
        w.Write(Res.Count);
        foreach (var e in Res.Values) { Base(e); w.Write(e.ResType); w.Write(e.ResMeta ?? new byte[16]); w.Write(e.ResRid); }
        var chunks = Chunks.Values.Concat(TableChunks.Values.SelectMany(l => l)).Distinct().ToList();
        w.Write(chunks.Count);
        foreach (var e in chunks) { Base(e); w.Write(e.Id.ToByteArray()); w.Write(e.LogicalOffset); w.Write(e.LogicalSize); w.Write(e.InChunkTable); w.Write(Chunks.TryGetValue(e.Id, out var main) && ReferenceEquals(main, e)); }
        w.Close();
        File.Move(file + ".tmp", file, overwrite: true);
    }

    private bool LoadCache(string file, string key)
    {
        try
        {
            using var r = new BinaryReader(File.OpenRead(file));
            if (r.ReadString() != "NHL27Kit-index-1" || r.ReadString() != key)
                return false;
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) BundleList.Add(new BundleInfo(i, r.ReadString(), r.ReadString(), r.ReadInt32(), r.ReadInt32(), ReadRef(r)));
            (string Name, int Bundle, TocRef Ref, string Toc, long Sha1Pos, byte[] Sha1, long SizePos, uint Size, List<int> Bundles) Base()
            {
                string name = r.ReadString(); int bundle = r.ReadInt32(); var rf = ReadRef(r); string toc = r.ReadString(); long sp = r.ReadInt64();
                int sl = r.ReadInt32(); var sha = sl > 0 ? r.ReadBytes(sl) : null; long zp = r.ReadInt64(); uint z = r.ReadUInt32();
                int bc = r.ReadInt32(); var bl = new List<int>(bc); for (int i = 0; i < bc; i++) bl.Add(r.ReadInt32());
                return (name, bundle, rf, string.Intern(toc), sp, sha, zp, z, bl);
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var b = Base();
                var e = new EbxEntry { Name = b.Name, Bundle = b.Bundle, Ref = b.Ref, Toc = b.Toc, Sha1Pos = b.Sha1Pos, Sha1 = b.Sha1, OriginalSizePos = b.SizePos, OriginalSize = b.Size, Id = new Guid(r.ReadBytes(16)), Type = string.Intern(r.ReadString()) };
                e.Bundles.AddRange(b.Bundles); Ebx[e.Name] = e;
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var b = Base();
                var e = new ResEntry { Name = b.Name, Bundle = b.Bundle, Ref = b.Ref, Toc = b.Toc, Sha1Pos = b.Sha1Pos, Sha1 = b.Sha1, OriginalSizePos = b.SizePos, OriginalSize = b.Size, ResType = r.ReadUInt32(), ResMeta = r.ReadBytes(16), ResRid = r.ReadUInt64() };
                e.Bundles.AddRange(b.Bundles); Res[e.Name] = e;
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var b = Base();
                var e = new ChunkEntry { Name = b.Name, Bundle = b.Bundle, Ref = b.Ref, Toc = b.Toc, Sha1Pos = b.Sha1Pos, Sha1 = b.Sha1, OriginalSizePos = b.SizePos, OriginalSize = b.Size, Id = new Guid(r.ReadBytes(16)), LogicalOffset = r.ReadUInt32(), LogicalSize = r.ReadUInt32(), InChunkTable = r.ReadBoolean() };
                bool main = r.ReadBoolean();
                e.Bundles.AddRange(b.Bundles);
                if (e.InChunkTable)
                {
                    if (!TableChunks.TryGetValue(e.Id, out var list)) TableChunks[e.Id] = list = new List<ChunkEntry>();
                    list.Add(e);
                }
                if (main) Chunks[e.Id] = e;
            }
            return true;
        }
        catch
        {
            BundleList.Clear(); Ebx.Clear(); Res.Clear(); Chunks.Clear(); TableChunks.Clear();
            return false;
        }
    }

    private static void WriteRef(BinaryWriter w, TocRef r)
    {
        w.Write((byte)r.Catalog); w.Write((byte)r.Cas); w.Write(r.Patch); w.Write(r.Offset); w.Write(r.Size); w.Write(r.OffsetPos); w.Write(r.SizePos);
    }

    private static TocRef ReadRef(BinaryReader r) =>
        new(r.ReadByte(), r.ReadByte(), r.ReadBoolean(), r.ReadUInt32(), r.ReadUInt32(), r.ReadInt32(), r.ReadInt32());

    // ---------------------------------------------------------------- reading data

    /// <summary>Reads stored (compressed) bytes from a CAS file.</summary>
    public byte[] ReadStored(TocRef r) => ReadStored(r.Catalog, r.Cas, r.Patch, r.Offset, r.Size);

    public byte[] ReadStored(int catalog, int cas, bool patch, uint offset, uint size)
    {
        using var fs = new FileStream(Layout.CasPath(catalog, cas, patch), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var b = new byte[size];
        fs.Position = offset;
        fs.ReadExactly(b);
        return b;
    }

    /// <summary>An asset's data, decompressed, from where its TOC entry points now (<see cref="LiveRef"/>).</summary>
    public byte[] Read(AssetEntry e) => FrostbiteBlocks.Decode(ReadStored(LiveRef(e)));

    /// <summary>
    /// The entry's location as its TOC has it now: a write that adds data at the end of a CAS file (NHL 24's textures and
    /// databases, a grown texture) repoints the TOC entry's big-endian offset and size, which the index (built before)
    /// doesn't know. The CAS file stays the same.
    /// </summary>
    public TocRef LiveRef(AssetEntry e)
    {
        var r = e.Ref;
        if (e.Toc == null || r.OffsetPos <= 0 || r.SizePos <= 0) return r;
        try
        {
            using var fs = new FileStream(e.Toc, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> b = stackalloc byte[4];
            fs.Position = r.OffsetPos;
            fs.ReadExactly(b);
            uint offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b);
            fs.Position = r.SizePos;
            fs.ReadExactly(b);
            uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b);
            return offset == r.Offset && size == r.Size ? r : r with { Offset = offset, Size = size };
        }
        catch (IOException) { return r; }
    }
}

/// <summary>A bundle record (stored in a CAS file): the bundle's asset list with names, sizes, SHA1s, RES metadata and chunk ids.</summary>
public sealed class BundleRecord
{
    public sealed record Item(string Name, long Sha1Pos, byte[] Sha1, long SizePos, uint Size, uint ResType = 0, byte[] ResMeta = null,
                              ulong ResRid = 0, Guid Id = default, uint LogicalOffset = 0);

    public List<Item> Ebx { get; } = new();
    public List<Item> Res { get; } = new();
    public List<Item> Chunks { get; } = new();

    /// <summary>
    /// Header: [u32 BE size][4 magic][u32 total][ebx][res][chunk count][string offset][meta offset][meta size] (LE,
    /// offsets +4), then a SHA1 per asset; ebx and res: [u32 name offset][u32 size]; res then types, 16-byte metas
    /// and u64 rids; chunks: [guid][u32 logical offset][u32 logical size].
    /// </summary>
    public static BundleRecord Read(byte[] b, long fileOffset)
    {
        var rec = new BundleRecord();
        int U(int p) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
        int total = U(8), ebx = U(12), res = U(16), chunks = U(20), strings = U(24) + 4;
        int p = 36;
        var shaPos = new long[total];
        var sha = new byte[total][];
        for (int i = 0; i < total; i++, p += 20) { shaPos[i] = fileOffset + p; sha[i] = b[p..(p + 20)]; }
        string Str(int off) { int s = strings + off, e = Array.IndexOf(b, (byte)0, s); return Encoding.UTF8.GetString(b, s, e - s); }
        for (int i = 0; i < ebx; i++, p += 8)
            rec.Ebx.Add(new Item(Str(U(p)), shaPos[i], sha[i], fileOffset + p + 4, (uint)U(p + 4)));
        var resNames = new (string, long, uint)[res];
        for (int i = 0; i < res; i++, p += 8) resNames[i] = (Str(U(p)), fileOffset + p + 4, (uint)U(p + 4));
        var types = new uint[res];
        for (int i = 0; i < res; i++, p += 4) types[i] = (uint)U(p);
        var metas = new byte[res][];
        for (int i = 0; i < res; i++, p += 16) metas[i] = b[p..(p + 16)];
        for (int i = 0; i < res; i++, p += 8)
            rec.Res.Add(new Item(resNames[i].Item1, shaPos[ebx + i], sha[ebx + i], resNames[i].Item2, resNames[i].Item3, types[i], metas[i],
                                 BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(p))));
        for (int i = 0; i < chunks; i++, p += 24)
            rec.Chunks.Add(new Item(null, shaPos[ebx + res + i], sha[ebx + res + i], fileOffset + p + 20, (uint)U(p + 20),
                                    Id: new Guid(b.AsSpan(p, 16)), LogicalOffset: (uint)U(p + 16)));
        return rec;
    }
}
