namespace NHL27Kit.Core;

/// <summary>What replacing a texture would do: the byte ranges, and how the result looks once decoded back.</summary>
public sealed class TextureReplacementPlan
{
    public string Name { get; init; }
    public TextureHeader Header { get; init; }
    public ChunkPlan Chunk { get; init; }
    /// <summary>A resize's header rewrite (null when the size stays).</summary>
    public ChunkPlan Res { get; init; }
    /// <summary>The new chunk read back with the decoder (so a preview shows what the game will draw).</summary>
    public Texture Result { get; init; }
}

public static partial class Replacements
{
    /// <summary>
    /// Plans replacing the texture <paramref name="name"/> with <paramref name="rgba"/>, an image already at the texture's
    /// size. Reads the game; writes nothing.
    /// </summary>
    public static TextureReplacementPlan PlanTexture(AssetIndex index, string name, byte[] rgba, LocationIndex locations, TocCache tocs, Action<string> log = null, int layer = 0, byte[] arrayData = null, bool takeShared = false)
    {
        var res = index.GetRes(name) ?? throw new InvalidDataException($"No texture named {name}.");
        var header = TextureHeader.Parse(index.Read(res), ps4: !index.Layout.IsPs5);
        string why = TextureBuilder.Unsupported(header);
        if (why != null) throw new NotSupportedException(why);
        if (header.Ps4) return PlanTexturePs4(index, name, header, rgba, locations, tocs, log);
        byte[] raw = TextureBuilder.BuildChunk(header, rgba, log);
        if (header.Layers > 1)
        {
            // an array: the new layer goes in place of layer N, the others are kept as the game has them
            if (layer < 0 || layer >= header.Layers) throw new ArgumentOutOfRangeException(nameof(layer), $"The array has layers 0 to {header.Layers - 1}.");
            // the other layers: as given (the game's array before an earlier replacement was undone), else as the game has them
            var current = arrayData ?? Texture.Load(index, name, decode: false).Data;
            if (current.Length != (long)header.Layers * header.ChunkSize)
                throw new InvalidDataException($"The array's chunk is {current.Length:N0} bytes, not {header.Layers} layers of {header.ChunkSize:N0}: its layout isn't understood.");
            var full = (byte[])current.Clone();
            raw.CopyTo(full, layer * header.ChunkSize);
            raw = full;
        }
        var chunk = ChunkPatcher.Plan(index, header.ChunkId, raw, locations, tocs, log, takeShared);
        var result = new Texture { Name = name, Header = header, Chunk = null, Data = raw, Layer = layer };
        result.DecodeMip(0);
        return new TextureReplacementPlan { Name = name, Header = header, Chunk = chunk, Result = result };
    }

    /// <summary>
    /// NHL 24: the new chunk is stored uncompressed (mip 0, mip 1 and the rest each starting a block, as the game's own
    /// chunks do), added at the end of its CAS file (<see cref="ChunkGrower"/>), and the header's two mip offsets (where
    /// mip 1 and mip 2 start in the stored chunk) are rewritten in every RES copy. The TOC entries change, so the TOC's
    /// signature no longer matches. The header patches follow the chunk's in <see cref="TextureReplacementPlan.Chunk"/>,
    /// so callers apply one list.
    /// </summary>
    private static TextureReplacementPlan PlanTexturePs4(AssetIndex index, string name, TextureHeader header, byte[] rgba, LocationIndex locations, TocCache tocs, Action<string> log)
    {
        byte[] raw = TextureBuilder.BuildChunk(header, rgba, log);
        int n = TextureBuilder.StoredMips(header);
        // the parts that start a block: mip 0, mip 1, then the rest together
        var cuts = new List<int> { 0 };
        if (n > 1) cuts.Add((int)header.MipSizes[0]);
        if (n > 2) cuts.Add((int)(header.MipSizes[0] + header.MipSizes[1]));
        cuts.Add(raw.Length);
        using var ms = new MemoryStream();
        var starts = new List<long>();
        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            starts.Add(ms.Length);
            ms.Write(FrostbiteBlocks.Store(raw[cuts[k]..cuts[k + 1]]));
        }
        byte[] stored = ms.ToArray();
        var res = index.Read(index.GetRes(name));
        var newRes = (byte[])res.Clone();
        for (int k = 0; k < 2; k++)
            BitConverter.TryWriteBytes(newRes.AsSpan(k * 4, 4), (uint)(k + 1 < starts.Count ? starts[k + 1] : 0));
        // the chunk and any header copy that moves share the CAS files' planned ends
        var ends = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var chunk = ChunkGrower.Plan(index, header.ChunkId, stored, locations, tocs, ends);
        if (!newRes.AsSpan().SequenceEqual(res))
        {
            var resPlan = ResPatcher.Plan(index, name, newRes, locations, tocs, grow: ends);
            chunk.Patches.AddRange(resPlan.Patches);
            chunk.Notes.AddRange(resPlan.Notes);
        }
        var nh = TextureHeader.Parse(newRes, ps4: true);
        var result = new Texture { Name = name, Header = nh, Chunk = null, Data = raw };
        result.DecodeMip(0);
        return new TextureReplacementPlan { Name = name, Header = nh, Chunk = chunk, Result = result };
    }

    /// <summary>
    /// Plans replacing the texture <paramref name="name"/> with <paramref name="rgba"/> at another size. The game's own
    /// header layout for that size is taken from <paramref name="like"/>, a texture the game has at that size, type and
    /// block size: its mip offsets, size, mip count, the two small fields at +32/+33, mip sizes and chunk size. The rest
    /// (format, flags, chunk id, name hash, first mip) stays this texture's. The bigger chunk is added at the end of its
    /// CAS file (<see cref="ChunkGrower"/>) and the header is rewritten in place. <see cref="TextureReplacementPlan.Res"/>
    /// holds the header's patches.
    /// </summary>
    public static TextureReplacementPlan PlanTextureResize(AssetIndex index, string name, byte[] rgba, string like, LocationIndex locations, TocCache tocs, Action<string> log = null, IDictionary<string, long> ends = null)
    {
        var resEntry = index.GetRes(name) ?? throw new InvalidDataException($"No texture named {name}.");
        var oldRes = index.Read(resEntry);
        var header = TextureHeader.Parse(oldRes);
        var refRes = index.Read(index.GetRes(like) ?? throw new InvalidDataException($"No texture named {like}."));
        var rh = TextureHeader.Parse(refRes);
        if (header.Type != TextureType.TT_2d || rh.Type != TextureType.TT_2d) throw new NotSupportedException("Only plain 2D textures are resized.");
        if (rh.Format.BlockBytes != header.Format.BlockBytes) throw new InvalidDataException($"{like} is {rh.Format.Name}, whose blocks aren't the size of {header.Format.Name}'s.");
        if (rgba.Length != rh.Width * rh.Height * 4) throw new ArgumentException($"The image must be {rh.Width} x {rh.Height}, as {like} is.");
        var res = (byte[])oldRes.Clone();
        Array.Copy(refRes, 0, res, 0, 8);       // mip offsets
        Array.Copy(refRes, 22, res, 22, 4);     // width, height
        res[30] = refRes[30];                   // mip count
        Array.Copy(refRes, 32, res, 32, 2);     // the two small fields (they follow the size)
        Array.Copy(refRes, 56, res, 56, 64);    // mip sizes, chunk size
        var nh = TextureHeader.Parse(res);
        string why = TextureBuilder.Unsupported(nh);
        if (why != null) throw new NotSupportedException(why);
        byte[] raw = TextureBuilder.BuildChunk(nh, rgba, log);
        if (raw.Length != nh.ChunkSize) throw new InvalidDataException($"The new chunk is {raw.Length:N0} bytes, the header says {nh.ChunkSize:N0}.");
        byte[] stored = FrostbiteBlocks.Store(raw);
        // the header's mip offsets: where the first stored block at or after mip 0 (and mip 1) starts. Stored smallest
        // first, so mip k begins after the smaller ones.
        var starts = FrostbiteBlocks.BlockStarts(stored);
        for (int k = 0; k < 2; k++)
        {
            long mipStart = 0;
            for (int i = TextureBuilder.StoredMips(nh) - 1; i > k; i--) mipStart += nh.MipSizes[i];
            var block = starts.FirstOrDefault(s => s.Raw >= mipStart);
            if (block.Compressed != nh.MipOffsets[k])
                throw new InvalidDataException($"Mip offset {k} is {nh.MipOffsets[k]} in {like}'s header, but the new chunk's block for mip {k} starts at {block.Compressed}: this layout isn't understood.");
        }
        var chunk = ChunkGrower.Plan(index, header.ChunkId, stored, locations, tocs, ends);
        var resPlan = ResPatcher.Plan(index, name, res, locations, tocs);
        var result = new Texture { Name = name, Header = nh, Chunk = null, Data = raw };
        result.DecodeMip(0);
        return new TextureReplacementPlan { Name = name, Header = nh, Chunk = chunk, Res = resPlan, Result = result };
    }
}
