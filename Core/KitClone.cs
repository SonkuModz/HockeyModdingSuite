using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// One NHL 24 kit folder copied into a new one (<c>content/character/skater/league/&lt;league&gt;/&lt;team&gt;/&lt;brand&gt;/&lt;word&gt;</c>).
/// The new folder word must be as long as the old one, so no offset in any file moves.
/// </summary>
public sealed class KitCopy
{
    public string League { get; init; } = "nhl";
    public string Team { get; init; }
    public string Brand { get; init; } = "adidas";
    public string FromWord { get; init; }
    public string ToWord { get; init; }
    /// <summary>
    /// New pixels for one of the copy's textures: (the new texture's name, its header) -> the new chunk's raw bytes (every
    /// stored mip largest first, each mip's layers in turn, as NHL 24 lays them out), or null to keep the copied pixels.
    /// </summary>
    public Func<string, TextureHeader, byte[]> Pixels { get; init; }
    /// <summary>New contents for one of the copy's other ebx files (the stamps): (new name, copied ebx) -> new ebx of the same size, or null.</summary>
    public Func<string, byte[], byte[]> Ebx { get; init; }
    /// <summary>
    /// Shared assets the copy's bundles carry (the weave in a shader preset's bundle) to swap for another copy's in the same
    /// build: (old name part, new name part) pairs, same length. A shared asset whose renamed name another copy built (listed
    /// earlier) is listed under that name, pointing at that copy's data and ids.
    /// </summary>
    public IReadOnlyList<(string From, string To)> SharedRenames { get; init; }
    public string SharedName(string name)
    {
        if (SharedRenames == null) return null;
        string n = name;
        foreach (var (f, t) in SharedRenames) n = n.Replace(f, t);
        return n == name ? null : n;
    }
    /// <summary>New pixels stored as uncompressed blocks instead of Oodle Kraken ones (a check).</summary>
    public bool Uncompressed { get; init; }

    /// <summary>The part of the folder's file names that changes (by default _&lt;team&gt;_&lt;word&gt;; the shared weave folder's
    /// files have _&lt;word&gt;_). Same length in both.</summary>
    public string FileFrom { get; init; }
    public string FileTo { get; init; }
    public string FilePartFrom => FileFrom ?? $"_{Team}_{FromWord}";
    public string FilePartTo => FileTo ?? $"_{Team}_{ToWord}";

    public string Folder(string word) => $"content/character/skater/league/{League}/{Team}/{Brand}/{word}";
    public string Rename(string name) =>
        name.Replace($"/{FromWord}/", $"/{ToWord}/").Replace(FilePartFrom, FilePartTo);
    public override string ToString() => $"{League}/{Team}/{Brand}/{FromWord} -> {ToWord}";
}

/// <summary>
/// Copies whole NHL 24 kit folders into new ones, as the Center Ice Mod added its Fanatics kits (variants 24 and 25):
/// every bundle of the folder (&lt;asset&gt;_player_team_brt, about 31 a kit) gets a copy named for the new folder, added to a
/// superbundle TOC (<see cref="TocAdder"/>), with its data at the end of one CAS file. What a copy changes:
/// - the names (folder word and the &lt;team&gt;_&lt;word&gt; in each file name; same length, so nothing moves);
/// - each of the folder's ebx files' partition guid and first guid, wherever any of the folder's files names them
///   (the shader presets import the folder's textures by them);
/// - each texture header's res id (in its ebx and the bundle records), chunk id (in the header, the records and the
///   chunk table) and name hash (in the header and the records' chunk meta);
/// - every record's SHA1s.
/// Assets a bundle carries that aren't the folder's (the shared weave and default textures in the shader preset's
/// bundle) keep pointing at the game's own data. A texture's chunk the game keeps in a chunk table (the full chain of a
/// streamed texture, whose bundle copy is only its tail) goes in the TOC's chunk table too.
/// </summary>
public static class KitClone
{
    /// <summary>Fixed ids for a copy (a check gives a real sibling folder's): by new asset name.</summary>
    public sealed class Ids
    {
        public Dictionary<Guid, Guid> Guids { get; } = new();
        public Dictionary<string, ulong> Rids { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Guid> Chunks { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class NewBundle
    {
        public string Name;
        public string From;
        public byte[] Record;
        /// <summary>Entries in TOC order (record, ebx, res, chunks): where each one is, and its stored bytes when it is new.</summary>
        public List<(byte[] Group, uint Offset, uint Size, byte[] Stored)> Entries = new();
        public BundleRecord Parsed;
        /// <summary>The patch TOC copy's entries, when the bundle is listed there too (else null).</summary>
        public List<(byte[] Group, uint Offset, uint Size, byte[] Stored)> PatchEntries;
    }

    public sealed class Plan
    {
        public List<PatchData> Patches { get; } = new();
        public List<string> Notes { get; } = new();
        public List<NewBundle> Bundles { get; } = new();
        /// <summary>The new ebx files, decoded, by name (for checks).</summary>
        public Dictionary<string, byte[]> Raw { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The new texture headers by name (a texture's ebx and header share its name).</summary>
        public Dictionary<string, byte[]> RawRes { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The new chunks' full stored bytes by id (for checks).</summary>
        public Dictionary<Guid, byte[]> ChunkStored { get; } = new();
        /// <summary>New texture name -> chunk id.</summary>
        public Dictionary<string, Guid> TextureChunks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[] TocAfter;
        public string TocRel;
        /// <summary>The patch TOC of the same superbundle, when copies of bundles it lists were added to it too (else null).</summary>
        public byte[] PatchTocAfter;
        public string PatchTocRel;
        public List<string> PatchBundles { get; } = new();
        /// <summary>The patch CAS file the patch-listed root bundles' records and own files are stored in (as Center Ice Mod stores its kits'), else null.</summary>
        public string PatchCasRel;
        public long PatchCasLength;
        public byte[] PatchData;
        public string CasRel;
        public long CasLength;
        public byte[] Data;
        public long Bytes => Data?.Length ?? 0;
    }

    private static int Replace(byte[] b, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, int start = 0, int end = -1)
    {
        if (end < 0) end = b.Length;
        int n = 0;
        for (int i = start; i + from.Length <= end;)
        {
            int at = b.AsSpan(i, end - i).IndexOf(from);
            if (at < 0) break;
            to.CopyTo(b.AsSpan(i + at));
            n++;
            i += at + from.Length;
        }
        return n;
    }

    private static int ReplaceNames(byte[] b, KitCopy c, int start = 0, int end = -1) =>
        Replace(b, Encoding.UTF8.GetBytes($"/{c.FromWord}/"), Encoding.UTF8.GetBytes($"/{c.ToWord}/"), start, end) +
        Replace(b, Encoding.UTF8.GetBytes(c.FilePartFrom), Encoding.UTF8.GetBytes(c.FilePartTo), start, end);

    private static byte[] U64(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); return b; }
    private static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }

    /// <summary>The id a copy gives one of its ebx files' guids (partition or first instance): seeded from the new name.</summary>
    public static Guid CopyGuid(string newName, Guid old) => Seeded(newName + "|" + old);

    private static Guid Seeded(string seed)
    {
        var h = MD5.HashData(Encoding.UTF8.GetBytes("hms-kit|" + seed));
        h[7] = (byte)((h[7] & 0x0F) | 0x40);
        h[8] = (byte)((h[8] & 0x3F) | 0x80);
        return new Guid(h);
    }

    private sealed class TocData
    {
        public byte[] Bytes;
        public SuperBundleToc Parsed;
        public TocAdder.Toc Raw;
    }

    /// <summary>The live copy of each of the folder's bundles (the patch TOC's when both list it), by name.</summary>
    public static List<BundleInfo> FolderBundles(AssetIndex index, string folder)
    {
        string prefix = "gen4b/" + folder + "/";
        return index.BundleList.Where(b => b.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(b => b.Toc.Contains(Path.DirectorySeparatorChar + "Patch" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).First())
            .OrderBy(b => b.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// NHL 24's stored form of a texture chunk, as <c>Replacements.PlanTexturePs4</c> writes it: uncompressed blocks with
    /// mip 0, mip 1 and the rest (and a streamed texture's first loaded mip) each starting one. Returns the stored bytes, the
    /// header's two mip offsets, and where the tail a bundle carries starts (raw offset, stored offset), if any.
    /// </summary>
    public static (byte[] Stored, uint Mip1, uint Mip2, uint TailRaw, int TailStored) StorePs4(TextureHeader h, byte[] raw, bool compress = true)
    {
        int n = TextureBuilder.StoredMips(h);
        int layers = Math.Max(1, h.Layers);
        long M(int k) => (long)h.MipSizes[k] * layers;
        var cuts = new List<int> { 0 };
        if (n > 1) cuts.Add((int)M(0));
        if (n > 2) cuts.Add((int)(M(0) + M(1)));
        cuts.Add(raw.Length);
        int tailAt = 0;
        if (h.FirstMip > 0)
        {
            for (int k = 0; k < h.FirstMip; k++) tailAt += (int)M(k);
            if (!cuts.Contains(tailAt)) { cuts.Add(tailAt); cuts.Sort(); }
        }
        using var ms = new MemoryStream();
        var starts = new List<long>();
        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            starts.Add(ms.Length);
            // Oodle Kraken blocks, as the game stores its textures (each segment starts a block, so the header's offsets hold)
            var seg = raw[cuts[k]..cuts[k + 1]];
            ms.Write((compress ? FrostbiteBlocks.Encode(seg, null, 4, pad: false, out _) : null) ?? FrostbiteBlocks.Store(seg));
        }
        // the header's offsets: where mip 1 and mip 2 start in the stored chunk
        uint mip1 = n > 1 ? (uint)starts[cuts.IndexOf((int)M(0))] : 0;
        uint mip2 = n > 2 ? (uint)starts[cuts.IndexOf((int)(M(0) + M(1)))] : 0;
        return (ms.ToArray(), mip1, mip2, (uint)tailAt, h.FirstMip > 0 ? (int)starts[cuts.IndexOf(tailAt)] : 0);
    }

    /// <summary>NHL 24's raw chunk of an array (or a plain texture: one layer) from each layer's image at full size.</summary>
    public static byte[] BuildPs4(TextureHeader h, IReadOnlyList<byte[]> layers, Action<string> log = null)
    {
        var f = h.Format;
        if (!f.Known) throw new NotSupportedException($"No encoder for {f.Name}.");
        int count = Math.Max(1, h.Layers);
        if (layers.Count != count) throw new ArgumentException($"The texture has {count} layer(s), not {layers.Count}.");
        bool srgb = TextureBuilder.IsSrgb(h), tiled = h.Flags.HasFlag(TextureFlags.Swizzle);
        int n = TextureBuilder.StoredMips(h);
        var mips = new byte[count][][];
        for (int l = 0; l < count; l++)
        {
            if (layers[l].Length != h.Width * h.Height * 4) throw new ArgumentException($"Layer {l} must be {h.Width} x {h.Height}.");
            mips[l] = new byte[n][];
            byte[] cur = layers[l];
            int w = h.Width, ht = h.Height;
            for (int i = 0; i < n; i++)
            {
                var (mw, mh) = h.MipSize(i);
                if (i > 0) (cur, w, ht) = MipChain.Halve(cur, w, ht, srgb);
                if (w != mw || ht != mh) throw new InvalidDataException($"Mip {i} should be {mw} x {mh}, not {w} x {ht}.");
                int bw = (mw + 3) / 4, bh = (mh + 3) / 4;
                var blocks = BcEncoder.Encode(cur, mw, mh, f);
                var mip = tiled ? Ps4Tiling.Tile(blocks, bw, bh, f.BlockBytes) : blocks;
                if (mip.Length != h.MipSizes[i])
                    throw new InvalidDataException($"Mip {i} should take {h.MipSizes[i]} bytes, but a {mw} x {mh} {f.Name} mip takes {mip.Length}.");
                mips[l][i] = mip;
            }
        }
        using var ms = new MemoryStream();
        for (int i = 0; i < n; i++)
            for (int l = 0; l < count; l++) ms.Write(mips[l][i]);
        return ms.ToArray();
    }

    /// <param name="tocPath">The TOC the new bundles go into (NHL 24: the base contentsb.toc, where the Center Ice Mod put its kits).</param>
    /// <param name="ids">Fixed ids (checks); otherwise seeded from the new names, the same every build.</param>
    public static Plan Build(AssetIndex index, IReadOnlyList<KitCopy> copies, string tocPath, Ids ids = null, Action<string> log = null,
                             int? casIndex = null, bool testInto = false)
    {
        var layout = index.Layout;
        string Rel(string p) => Path.GetRelativePath(layout.GameDir, p).Replace('\\', '/');
        var plan = new Plan { TocRel = Rel(tocPath) };
        var tocs = new Dictionary<string, TocData>(StringComparer.OrdinalIgnoreCase);
        TocData Toc(string path)
        {
            if (tocs.TryGetValue(path, out var t)) return t;
            var bytes = File.ReadAllBytes(path);
            return tocs[path] = new TocData { Bytes = bytes, Parsed = new SuperBundleToc(path, bytes, layout.TocCatalogs), Raw = TocAdder.Toc.Read(bytes) };
        }
        var target = Toc(tocPath);
        var usedRids = index.Res.Values.Select(r => r.ResRid).ToHashSet();
        var usedChunks = index.Chunks.Keys.Concat(index.TableChunks.Keys).ToHashSet();
        var targetNames = target.Raw.Names.ToHashSet(StringComparer.Ordinal);

        // the sources: each copy's bundles, their records and decoded ebx and res
        var work = new List<(KitCopy Copy, BundleInfo Info, TocBundle Tb, TocData Toc, byte[] Record, BundleRecord Rec)>();
        foreach (var c in copies)
        {
            if (c.FromWord.Length != c.ToWord.Length) throw new ArgumentException($"{c}: the new folder word must be as long as the old one.");
            var bundles = FolderBundles(index, c.Folder(c.FromWord));
            if (bundles.Count == 0) throw new InvalidDataException($"{c}: no bundles in {c.Folder(c.FromWord)}.");
            if (!testInto && FolderBundles(index, c.Folder(c.ToWord)).Count > 0) throw new InvalidDataException($"{c}: the game already has {c.Folder(c.ToWord)}.");
            foreach (var b in bundles)
            {
                if (!b.Name.EndsWith("_player_team_brt", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{b.Name} isn't a kit asset's bundle.");
                var t = Toc(b.Toc);
                var tb = t.Parsed.Bundles[b.BundleIndex];
                var record = index.ReadStored(tb.Entries[0]);
                var rec = BundleRecord.Read(record, 0);
                if (rec.Ebx.Count + rec.Res.Count + rec.Chunks.Count != tb.Entries.Count - 1)
                    throw new InvalidDataException($"{b.Name}: its record lists {rec.Ebx.Count + rec.Res.Count + rec.Chunks.Count} assets, its TOC entry {tb.Entries.Count - 1}.");
                string newName = c.Rename(b.Name);
                if (newName == b.Name || newName.Length != b.Name.Length) throw new InvalidDataException($"{b.Name}: no name for its copy.");
                if (!testInto && targetNames.Contains(newName)) throw new InvalidDataException($"The TOC already has {newName}.");
                work.Add((c, b, tb, t, record, rec));
            }
        }

        bool Own(KitCopy c, string name) => name.StartsWith(c.Folder(c.FromWord) + "/", StringComparison.OrdinalIgnoreCase);

        // the CAS file the new data goes in: of the target TOC's own catalog (not a patch one), the smallest
        var src = work.Select(w => w.Tb.Entries[0]).First(r => !r.Patch);
        var candidates = target.Parsed.Chunks.Where(x => !x.Ref.Patch && x.Ref.Catalog == src.Catalog).Select(x => x.Ref.Cas).Distinct()
            .Select(cas => (Cas: cas, Path: layout.CasPath(src.Catalog, cas, false))).Where(x => File.Exists(x.Path))
            .OrderBy(x => new FileInfo(x.Path).Length).ToList();
        if (casIndex != null) candidates = candidates.Where(x => x.Cas == casIndex).ToList();
        var pick = candidates.FirstOrDefault();
        if (pick.Path == null) throw new InvalidDataException("No CAS file of the TOC's catalog to add the kits to.");
        var groupRef = target.Parsed.Chunks.First(x => !x.Ref.Patch && x.Ref.Catalog == src.Catalog && x.Ref.Cas == pick.Cas).Ref;
        byte[] group = target.Bytes[(groupRef.OffsetPos - 4)..groupRef.OffsetPos];
        plan.CasRel = Rel(pick.Path);
        long casLength = new FileInfo(pick.Path).Length;
        plan.CasLength = casLength;
        long start = (casLength + 15) & ~15L;
        var data = new MemoryStream();
        data.Write(new byte[start - casLength]);
        uint Put(byte[] stored)
        {
            long at = casLength + data.Length;
            if (at + stored.Length > uint.MaxValue - 64L * 1024 * 1024) throw new InvalidDataException($"{plan.CasRel} would pass 4 GB.");
            data.Write(stored);
            while (data.Length % 16 != 0) data.WriteByte(0);
            return (uint)at;
        }

        var added = new List<AddedBundle>();
        var patchAdded = new List<AddedBundle>();
        string patchPath = Path.Combine(layout.GameDir, "Patch", "Gen4b", Path.GetFileName(tocPath));
        var patch = File.Exists(patchPath) && !string.Equals(Path.GetFullPath(patchPath), Path.GetFullPath(tocPath), StringComparison.OrdinalIgnoreCase) ? Toc(patchPath) : null;
        var patchNames = patch?.Raw.Names.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();
        var inTable = new HashSet<Guid>();
        // the patch TOC's copies of root bundles: their record and own ebx and res stored in a patch CAS file (no patch bundle
        // keeps its record in a base CAS file); their chunks stay where the base copies have them
        MemoryStream pdata = null;
        byte[] pgroup = null;
        uint PutPatch(byte[] stored)
        {
            long at = plan.PatchCasLength + pdata.Length;
            if (at + stored.Length > uint.MaxValue - 64L * 1024 * 1024) throw new InvalidDataException($"{plan.PatchCasRel} would pass 4 GB.");
            pdata.Write(stored);
            while (pdata.Length % 16 != 0) pdata.WriteByte(0);
            return (uint)at;
        }
        // what each copy built, for later copies that list it in place of a shared asset (SharedRenames): by new name
        var builtEbx = new Dictionary<string, (uint Off, uint Size, byte[] Stored)>(StringComparer.OrdinalIgnoreCase);
        var builtRes = new Dictionary<string, (uint Off, uint Size, byte[] Stored)>(StringComparer.OrdinalIgnoreCase);
        var builtRid = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        var builtChunk = new Dictionary<Guid, (Guid New, string Owner)>();   // source chunk id -> new id, source texture
        var chunkFull = new Dictionary<Guid, (uint Off, byte[] Stored, uint TailRaw, int TailStored, bool Rebuilt)>();   // by new id
        var bundleChunkAt = new Dictionary<(Guid, uint, uint), (uint Off, byte[] Stored)>();
        // each copy on its own (two copies of one source folder each get their own ids and files)
        foreach (var workC in work.GroupBy(w => w.Copy).Select(g => g.ToList()))
        {
            // pass 1: the ids every copy changes, over all its bundles (a texture's files are in several)
            var guidMap = new Dictionary<Guid, Guid>();
            var ridMap = new Dictionary<ulong, ulong>();
            var chunkMap = new Dictionary<Guid, Guid>();
            var hashMap = new Dictionary<uint, uint>();
            var rawEbx = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);   // source name -> decoded
            var rawRes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var resHeader = new Dictionary<string, TextureHeader>(StringComparer.OrdinalIgnoreCase);
            var copyOf = new Dictionary<string, KitCopy>(StringComparer.OrdinalIgnoreCase);    // source asset -> its copy
            var chunkOwner = new Dictionary<Guid, string>();                                   // source chunk -> source texture
            foreach (var (c, info, tb, t, record, rec) in workC)
            {
                int k = 1;
                foreach (var e in rec.Ebx)
                {
                    var r = tb.Entries[k++];
                    if (!Own(c, e.Name) || rawEbx.ContainsKey(e.Name)) continue;
                    var raw = FrostbiteBlocks.Decode(index.ReadStored(r));
                    var ebx = EbxFile.Parse(raw) ?? throw new InvalidDataException($"{e.Name} isn't an ebx file.");
                    rawEbx[e.Name] = raw;
                    copyOf[e.Name] = c;
                    string nn = c.Rename(e.Name);
                    foreach (var g in new[] { ebx.FileGuid, new Guid(raw.AsSpan(0x20, 16)) })
                        if (!guidMap.ContainsKey(g))
                            guidMap[g] = ids != null && ids.Guids.TryGetValue(g, out var fixedG) ? fixedG : Seeded(nn + "|" + g);
                }
                foreach (var e in rec.Res)
                {
                    var r = tb.Entries[k++];
                    if (!Own(c, e.Name)) continue;
                    string nn = c.Rename(e.Name);
                    if (!ridMap.ContainsKey(e.ResRid))
                    {
                        // the game tells an unresolved res id from a loaded resource's pointer by its low bit: every res id is odd
                        ulong nr = ids != null && ids.Rids.TryGetValue(nn, out var fr) ? fr : BinaryPrimitives.ReadUInt64LittleEndian(MD5.HashData(Encoding.UTF8.GetBytes("hms-kit-rid|" + nn))) | 1;
                        if (ids == null && usedRids.Contains(nr)) throw new InvalidDataException($"The res id for {nn} is taken.");
                        ridMap[e.ResRid] = nr;
                    }
                    builtRid[nn] = ridMap[e.ResRid];
                    if (rawRes.ContainsKey(e.Name)) continue;
                    var raw = FrostbiteBlocks.Decode(index.ReadStored(r));
                    rawRes[e.Name] = raw;
                    copyOf[e.Name] = c;
                    var h = TextureHeader.Parse(raw, ps4: true);
                    resHeader[e.Name] = h;
                    string tex = e.Name[(e.Name.LastIndexOf('/') + 1)..];
                    if (h.NameHash != AddedTextures.NameHash(e.Name)) throw new InvalidDataException($"{e.Name}'s header name hash isn't the one expected.");
                    hashMap[h.NameHash] = AddedTextures.NameHash(nn);
                    if (!chunkMap.ContainsKey(h.ChunkId))
                    {
                        var nc = ids != null && ids.Chunks.TryGetValue(nn, out var fc) ? fc : Seeded("chunk|" + nn);
                        if (ids == null && usedChunks.Contains(nc)) throw new InvalidDataException($"The chunk id for {nn} is taken.");
                        chunkMap[h.ChunkId] = nc;
                        chunkOwner[h.ChunkId] = e.Name;
                    }
                }
            }

            // pass 2: the new files, written once each
            var ebxAt = new Dictionary<string, (uint Off, uint Size, byte[] Stored)>(StringComparer.OrdinalIgnoreCase);
            var resAt = new Dictionary<string, (uint Off, uint Size, byte[] Stored)>(StringComparer.OrdinalIgnoreCase);
            byte[] NewEbx(string name)
            {
                var c = copyOf[name];
                var b = (byte[])rawEbx[name].Clone();
                if (ReplaceNames(b, c) == 0) throw new InvalidDataException($"{name} doesn't hold its name.");
                foreach (var (o, n) in guidMap) Replace(b, o.ToByteArray(), n.ToByteArray());
                foreach (var (o, n) in ridMap) Replace(b, U64(o), U64(n));
                if (c.Ebx?.Invoke(c.Rename(name), b) is { } custom)
                {
                    if (custom.Length != b.Length) throw new InvalidDataException($"{c.Rename(name)}: the new ebx must be {b.Length} bytes.");
                    b = custom;
                }
                return b;
            }
            foreach (var (name, raw) in rawRes)
            {
                var c = copyOf[name];
                var h = resHeader[name];
                string nn = c.Rename(name);
                var res = (byte[])raw.Clone();
                var nc = chunkMap[h.ChunkId];
                nc.ToByteArray().CopyTo(res, 0x20);
                BinaryPrimitives.WriteUInt32LittleEndian(res.AsSpan(0x70), AddedTextures.NameHash(nn));
                plan.TextureChunks[nn] = nc;
                if (!chunkFull.ContainsKey(nc))
                {
                    byte[] pixels = c.Pixels?.Invoke(nn, h);
                    if (pixels != null)
                    {
                        long want = 0;
                        for (int k = 0; k < TextureBuilder.StoredMips(h); k++) want += (long)h.MipSizes[k] * Math.Max(1, h.Layers);
                        if (pixels.Length != want) throw new InvalidDataException($"{nn}: the new chunk is {pixels.Length:N0} bytes, the header wants {want:N0}.");
                        var (stored, m1, m2, tailRaw, tailStored) = StorePs4(h, pixels, !c.Uncompressed);
                        BinaryPrimitives.WriteUInt32LittleEndian(res.AsSpan(0), m1);
                        BinaryPrimitives.WriteUInt32LittleEndian(res.AsSpan(4), m2);
                        chunkFull[nc] = (Put(stored), stored, tailRaw, tailStored, true);
                    }
                    else
                    {
                        // the game's full chain: the chunk table's copy, else the bundle's
                        var tableRef = index.TableChunks.TryGetValue(h.ChunkId, out var list) && list.Count > 0 ? list[0].Ref : (TocRef?)null;
                        var full = tableRef is { } tr ? index.ReadStored(tr) : null;
                        chunkFull[nc] = full != null ? (Put(full), full, 0u, -1, false) : (0u, null, 0u, -1, false);
                    }
                }
                else if (c.Pixels != null && chunkFull[nc].Rebuilt)
                {
                    var (_, stored, _, _, _) = chunkFull[nc];
                    // the same chunk named by another header: same offsets
                    var again = StorePs4(h, FrostbiteBlocks.Decode(stored), !c.Uncompressed);
                    BinaryPrimitives.WriteUInt32LittleEndian(res.AsSpan(0), again.Mip1);
                    BinaryPrimitives.WriteUInt32LittleEndian(res.AsSpan(4), again.Mip2);
                }
                plan.RawRes[nn] = res;
                var st = FrostbiteBlocks.Store(res);
                resAt[name] = (Put(st), (uint)st.Length, st);
            }
            foreach (var name in rawEbx.Keys)
            {
                var b = NewEbx(name);
                plan.Raw[copyOf[name].Rename(name)] = b;
                var st = FrostbiteBlocks.Store(b);
                ebxAt[name] = (Put(st), (uint)st.Length, st);
            }

            foreach (var (name, at) in ebxAt) builtEbx[copyOf[name].Rename(name)] = at;
            foreach (var (name, at) in resAt)
            {
                builtRes[copyOf[name].Rename(name)] = at;
            }
            foreach (var (oldId, newId) in chunkMap) builtChunk[oldId] = (newId, chunkOwner[oldId]);

            // pass 3: each bundle's record and block
            foreach (var (c, info, tb, t, record0, rec) in workC)
            {
                var nb = new NewBundle { Name = c.Rename(info.Name), From = info.Name };
                var record = (byte[])record0.Clone();
                int strings = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(24)) + 4;
                int metaAt = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(28)) + 4, metaSize = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(32));
                int total = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(8));
                if (ReplaceNames(record, c, strings) == 0) throw new InvalidDataException($"{info.Name}: the record doesn't hold the names.");
                var entries = new List<(byte[] Group, uint Off, uint Size, byte[] Stored)>();
                byte[] GroupOf(TocRef r) => t.Bytes[(r.OffsetPos - 4)..r.OffsetPos];
                // the group a source entry is in: the last group start at or before it in its block
                var srcGroups = new List<byte[]>();
                {
                    int bi = info.BundleIndex;
                    int bs = t.Raw.Base + (int)t.Raw.Blocks[bi];
                    int flagsOff = BinaryPrimitives.ReadInt32BigEndian(t.Bytes.AsSpan(bs + 8)), count = BinaryPrimitives.ReadInt32BigEndian(t.Bytes.AsSpan(bs + 12)), entriesOff = BinaryPrimitives.ReadInt32BigEndian(t.Bytes.AsSpan(bs + 16));
                    if (count != tb.Entries.Count || entriesOff != 32) throw new InvalidDataException($"{info.Name}: its TOC block isn't laid out as expected.");
                    int q = bs + entriesOff;
                    byte[] g = null;
                    for (int j = 0; j < count; j++)
                    {
                        if (t.Bytes[bs + flagsOff + j] == 1) { g = t.Bytes[q..(q + 4)]; q += 4; }
                        srcGroups.Add(g ?? throw new InvalidDataException($"{info.Name}: its block's first entry starts no location group."));
                        q += 8;
                    }
                }
                var newSha = new byte[total][];
                int k = 1, a = 0;
                // shared assets listed as another copy's (SharedRenames): their old ids -> the other copy's
                bool shared = false;
                var sharedRids = new List<(ulong, ulong)>();
                var sharedHashes = new List<(uint, uint)>();
                var sharedChunks = new List<(Guid, Guid)>();
                var sharedMetas = new List<(int, string)>();   // res index -> new name
                foreach (var e in rec.Ebx)
                {
                    var r = tb.Entries[k];
                    if (Own(c, e.Name)) { var x = ebxAt[e.Name]; entries.Add((group, x.Off, x.Size, x.Stored)); newSha[a] = SHA1.HashData(x.Stored); }
                    else if (c.SharedName(e.Name) is { } sn && builtEbx.TryGetValue(sn, out var bx)) { entries.Add((group, bx.Off, bx.Size, bx.Stored)); newSha[a] = SHA1.HashData(bx.Stored); shared = true; }
                    else entries.Add((srcGroups[k], r.Offset, r.Size, null));
                    k++; a++;
                }
                for (int j = 0; j < rec.Res.Count; j++)
                {
                    var e = rec.Res[j];
                    var r = tb.Entries[k];
                    if (Own(c, e.Name)) { var x = resAt[e.Name]; entries.Add((group, x.Off, x.Size, x.Stored)); newSha[a] = SHA1.HashData(x.Stored); }
                    else if (c.SharedName(e.Name) is { } sn && builtRes.TryGetValue(sn, out var bx))
                    {
                        entries.Add((group, bx.Off, bx.Size, bx.Stored));
                        newSha[a] = SHA1.HashData(bx.Stored);
                        sharedRids.Add((e.ResRid, builtRid[sn]));
                        sharedHashes.Add((AddedTextures.NameHash(e.Name), AddedTextures.NameHash(sn)));
                        sharedMetas.Add((j, sn));
                        shared = true;
                    }
                    else entries.Add((srcGroups[k], r.Offset, r.Size, null));
                    k++; a++;
                }
                var chunks = new List<(Guid, byte[])>();
                foreach (var e in rec.Chunks)
                {
                    var r = tb.Entries[k];
                    string owner = chunkOwner.GetValueOrDefault(e.Id);
                    bool isShared = !chunkMap.ContainsKey(e.Id) && builtChunk.TryGetValue(e.Id, out var bc0) && c.SharedName(bc0.Owner) is { } sn0 && builtRes.ContainsKey(sn0);
                    if (isShared) { owner = builtChunk[e.Id].Owner; sharedChunks.Add((e.Id, builtChunk[e.Id].New)); shared = true; }
                    if (chunkMap.TryGetValue(e.Id, out var nc) || (isShared && (nc = builtChunk[e.Id].New) != Guid.Empty))
                    {
                        var full = chunkFull[nc];
                        uint off, size;
                        byte[] stored;
                        if (full.Rebuilt)
                        {
                            // a tail (logical offset > 0) is the stored chunk from that mip on
                            if (e.LogicalOffset == 0) { off = full.Off; stored = full.Stored; }
                            else
                            {
                                if (e.LogicalOffset != full.TailRaw) throw new InvalidDataException($"{info.Name}: its copy of {owner} starts at {e.LogicalOffset}, the new chunk's tail at {full.TailRaw}.");
                                off = full.Off + (uint)full.TailStored;
                                stored = full.Stored[full.TailStored..];
                            }
                            size = (uint)stored.Length;
                        }
                        else
                        {
                            // as the game has it: inside the copied full chain when it is a part of it, else its own copy
                            var tableRef = index.TableChunks.TryGetValue(e.Id, out var list) && list.Count > 0 ? list[0].Ref : (TocRef?)null;
                            if (full.Stored != null && tableRef is { } tr && tr.Catalog == r.Catalog && tr.Cas == r.Cas && tr.Patch == r.Patch && r.Offset >= tr.Offset && r.Offset + r.Size <= tr.Offset + tr.Size)
                            {
                                off = full.Off + (r.Offset - tr.Offset);
                                size = r.Size;
                                stored = full.Stored[(int)(r.Offset - tr.Offset)..(int)(r.Offset - tr.Offset + r.Size)];
                            }
                            else
                            {
                                var key = (nc, r.Offset, r.Size);
                                if (!bundleChunkAt.TryGetValue(key, out var bc))
                                {
                                    var s = index.ReadStored(r);
                                    bundleChunkAt[key] = bc = (Put(s), s);
                                }
                                (off, stored) = bc;
                                size = (uint)stored.Length;
                            }
                        }
                        entries.Add((group, off, size, stored));
                        newSha[a] = SHA1.HashData(stored);
                        if (full.Stored != null && inTable.Add(nc))
                        {
                            var ce = new byte[12];
                            group.CopyTo(ce, 0);
                            BinaryPrimitives.WriteUInt32BigEndian(ce.AsSpan(4), full.Off);
                            BinaryPrimitives.WriteUInt32BigEndian(ce.AsSpan(8), (uint)full.Stored.Length);
                            chunks.Add((nc, ce));
                            plan.ChunkStored[nc] = full.Stored;
                        }
                    }
                    else entries.Add((srcGroups[k], r.Offset, r.Size, null));
                    k++; a++;
                }
                // record: SHA1s, res ids, chunk ids, chunk meta name hashes
                for (int j = 0; j < total; j++)
                    if (newSha[j] != null) newSha[j].CopyTo(record, 36 + 20 * j);
                // the res ids and chunk ids come before the strings; the chunk meta after them
                int idsFrom = 36 + 20 * total;
                foreach (var (o, n) in ridMap) Replace(record, U64(o), U64(n), idsFrom, strings);
                foreach (var (o, n) in chunkMap) Replace(record, o.ToByteArray(), n.ToByteArray(), idsFrom, strings);
                foreach (var (o, n) in hashMap) Replace(record, U32(o), U32(n), metaSize > 0 ? metaAt : strings, metaSize > 0 ? metaAt + metaSize : -1);
                if (shared)
                {
                    foreach (var (o, n) in sharedRids) Replace(record, U64(o), U64(n), idsFrom, strings);
                    foreach (var (o, n) in sharedChunks) Replace(record, o.ToByteArray(), n.ToByteArray(), idsFrom, strings);
                    foreach (var (o, n) in sharedHashes) Replace(record, U32(o), U32(n), metaSize > 0 ? metaAt : strings, metaSize > 0 ? metaAt + metaSize : -1);
                    foreach (var (f, to) in c.SharedRenames) Replace(record, Encoding.UTF8.GetBytes(f), Encoding.UTF8.GetBytes(to), strings);
                }
                // a res's 16-byte meta is its header's first 16 bytes (mip offsets): kept in step with a rebuilt header
                int metasAt = idsFrom + 8 * rec.Ebx.Count + 8 * rec.Res.Count + 4 * rec.Res.Count;
                for (int j = 0; j < rec.Res.Count; j++)
                    if (Own(c, rec.Res[j].Name) && rawRes[rec.Res[j].Name].AsSpan(0, 16).SequenceEqual(record.AsSpan(metasAt + 16 * j, 16)))
                        plan.RawRes[c.Rename(rec.Res[j].Name)].AsSpan(0, 16).CopyTo(record.AsSpan(metasAt + 16 * j, 16));
                foreach (var (j, sn) in sharedMetas)
                    if (index.GetRes(rec.Res[j].Name) is { } sr && index.Read(sr).AsSpan(0, 16).SequenceEqual(record.AsSpan(metasAt + 16 * j, 16)))
                        plan.RawRes[sn].AsSpan(0, 16).CopyTo(record.AsSpan(metasAt + 16 * j, 16));
                var recStored = record;   // records are stored as they are (not block framed)
                uint recOff = Put(recStored);
                entries.Insert(0, (group, recOff, (uint)recStored.Length, recStored));
                nb.Record = record;
                nb.Entries = entries;
                nb.Parsed = BundleRecord.Read(record, 0);

                // block: the source's 32-byte header, the entries in location groups, then a flag per entry
                int bs0 = t.Raw.Base + (int)t.Raw.Blocks[info.BundleIndex];
                byte[] Block(List<(byte[] Group, uint Off, uint Size, byte[] Stored)> ents)
                {
                    var block = new MemoryStream();
                    block.Write(t.Bytes.AsSpan(bs0, 32));
                    var flags = new byte[ents.Count];
                    byte[] last = null;
                    Span<byte> two = stackalloc byte[8];
                    for (int j = 0; j < ents.Count; j++)
                    {
                        var en = ents[j];
                        if (last == null || !en.Group.AsSpan().SequenceEqual(last)) { flags[j] = 1; block.Write(en.Group); last = en.Group; }
                        BinaryPrimitives.WriteUInt32BigEndian(two, en.Off);
                        BinaryPrimitives.WriteUInt32BigEndian(two[4..], en.Size);
                        block.Write(two);
                    }
                    int flagsAt = (int)block.Length;
                    block.Write(flags);
                    var o = block.ToArray();
                    BinaryPrimitives.WriteInt32BigEndian(o.AsSpan(8), flagsAt);
                    BinaryPrimitives.WriteInt32BigEndian(o.AsSpan(12), ents.Count);
                    return o;
                }
                var bb = Block(entries);
                added.Add(new AddedBundle(nb.Name, bb, chunks));
                // the game finds a kit by its root bundle in the patch superbundle: a source the patch TOC lists gets its copy
                // listed there too (same entries, so the same data; its chunks stay in the base TOC's table)
                if (patch != null && string.Equals(Path.GetFullPath(info.Toc), Path.GetFullPath(patchPath), StringComparison.OrdinalIgnoreCase))
                {
                    if (!testInto && patchNames.Contains(nb.Name)) throw new InvalidDataException($"The patch TOC already has {nb.Name}.");
                    if (pdata == null)
                    {
                        var pr = tb.Entries[0];
                        if (!pr.Patch) throw new InvalidDataException($"{info.Name}: its patch TOC record isn't in a patch CAS file.");
                        string pcas = layout.CasPath(pr.Catalog, pr.Cas, true);
                        plan.PatchCasRel = Rel(pcas);
                        plan.PatchCasLength = new FileInfo(pcas).Length;
                        pgroup = srcGroups[0];
                        pdata = new MemoryStream();
                        pdata.Write(new byte[((plan.PatchCasLength + 15) & ~15L) - plan.PatchCasLength]);
                    }
                    // the record, ebx and res entries (those the copy wrote) go to the patch CAS file too
                    int files = 1 + rec.Ebx.Count + rec.Res.Count;
                    var pents = entries.Select((en, j) => j < files && en.Stored != null ? (pgroup, PutPatch(en.Stored), en.Size, en.Stored) : en).ToList();
                    nb.PatchEntries = pents;
                    patchAdded.Add(new AddedBundle(nb.Name, Block(pents), new List<(Guid, byte[])>()));
                    plan.PatchBundles.Add(nb.Name);
                }
                plan.Bundles.Add(nb);
            }
        }
        plan.Data = data.ToArray();
        plan.Patches.Add(new PatchData(plan.CasRel, casLength, Array.Empty<byte>(), plan.Data, "kit copies", Append: true));
        // a check that copies over a folder the game has builds the files only
        var toc = testInto ? new TocAdder.Result() : TocAdder.Plan(plan.TocRel, target.Bytes, added);
        plan.Patches.AddRange(toc.Patches);
        plan.Notes.Add($"{copies.Count} kit(s): {added.Count} bundles, {inTable.Count} chunks in the chunk table, {plan.Data.Length / 1048576.0:F1} MB added to {plan.CasRel} at 0x{start:X}");
        plan.Notes.AddRange(toc.Notes);
        plan.TocAfter = toc.After;
        if (pdata != null)
        {
            plan.PatchData = pdata.ToArray();
            plan.Patches.Add(new PatchData(plan.PatchCasRel, plan.PatchCasLength, Array.Empty<byte>(), plan.PatchData, "kit copies' patch roots", Append: true));
        }
        if (patchAdded.Count > 0 && !testInto)
        {
            plan.PatchTocRel = Rel(patchPath);
            var pt = TocAdder.Plan(plan.PatchTocRel, patch.Bytes, patchAdded);
            plan.Patches.AddRange(pt.Patches);
            plan.Notes.AddRange(pt.Notes);
            plan.PatchTocAfter = pt.After;
        }
        log?.Invoke(plan.Notes[0]);
        return plan;
    }

    /// <summary>Reads a planned bundle entry back: from the planned data when it is new, else from the game.</summary>
    public static byte[] ReadEntry(AssetIndex index, Plan plan, (byte[] Group, uint Offset, uint Size, byte[] Stored) e) => e.Stored ?? throw new InvalidOperationException("not new");
}
