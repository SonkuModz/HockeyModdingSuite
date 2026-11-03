namespace NHL27Kit.Core;

/// <summary>Builds the data chunk of a replacement texture: every stored mip, encoded, tiled and laid out as the game's header says.</summary>
public static class TextureBuilder
{
    /// <summary>Why a texture can't be replaced (null if it can): arrays, cubes and volumes are not handled, nor unknown formats.</summary>
    public static string Unsupported(TextureHeader h)
    {
        if (h.Type != TextureType.TT_2d && h.Type != TextureType.TT_2dArray) return $"{h.Type} textures can't be replaced yet (only 2D textures and 2D arrays).";
        if (h.Type == TextureType.TT_2d && (h.Depth > 1 || h.SliceCount > 1)) return "This texture has several slices but isn't an array; it can't be replaced yet.";
        if (!h.Format.Known) return $"Pixel format {h.Format.Name} has no encoder.";
        if (StoredMips(h) == 0) return "The header lists no stored mips.";
        return null;
    }

    /// <summary>How many mips the chunk stores: the header's mip sizes, up to the first empty one.</summary>
    public static int StoredMips(TextureHeader h)
    {
        int n = 0;
        while (n < 15 && h.MipSizes[n] != 0) n++;
        return n;
    }

    public static bool IsSrgb(TextureHeader h) => h.Format.Name.EndsWith("sRGB");

    /// <summary>
    /// The new chunk (raw, before framing). <paramref name="rgba"/> is the image at the texture's full size
    /// (<see cref="TextureHeader.Width"/> x <see cref="TextureHeader.Height"/>). Mips are stored smallest first.
    /// </summary>
    public static byte[] BuildChunk(TextureHeader h, byte[] rgba, Action<string> log = null)
    {
        string why = Unsupported(h);
        if (why != null) throw new NotSupportedException(why);
        var f = h.Format;
        if (rgba.Length != h.Width * h.Height * 4)
            throw new ArgumentException($"The image must be {h.Width} x {h.Height}.");
        int n = StoredMips(h);
        bool srgb = IsSrgb(h);
        var (lw, lh) = h.MipSize(n - 1);
        int tail = h.MipCount > 1 && MipTail.IsTailMip(lw, lh, f.BlockBytes) ? n - 1 : -1;
        int last = tail >= 0 ? Math.Max(1, (int)h.MipCount) - 1 : n - 1;
        // normal mips are tiled one by one; the tail mips share one tile
        var stored = new byte[n][];
        var tailMips = new List<(byte[], int, int)>();
        byte[] cur = rgba;
        int w = h.Width, ht = h.Height;
        for (int i = 0; i <= last; i++)
        {
            var (mw, mh) = h.MipSize(i);
            if (i > 0) (cur, w, ht) = MipChain.Halve(cur, w, ht, srgb);
            if (w != mw || ht != mh) throw new InvalidDataException($"Mip {i} should be {mw} x {mh}, not {w} x {ht}.");
            int bw = (mw + 3) / 4, bh = (mh + 3) / 4;
            log?.Invoke($"encoding mip {i} ({mw} x {mh})");
            var blocks = BcEncoder.Encode(cur, mw, mh, f);
            if (tail >= 0 && i >= tail)
            {
                tailMips.Add((blocks, bw, bh));
                continue;
            }
            int expected = Ps5Swizzle.TiledSize(bw, bh, f.BlockBytes);
            if (expected != h.MipSizes[i])
                throw new InvalidDataException($"Mip {i} should take {h.MipSizes[i]} bytes but a tiled {mw} x {mh} {f.Name} mip takes {expected}: this layout isn't understood.");
            stored[i] = Ps5Swizzle.Swizzle(blocks, bw, bh, f.BlockBytes);
        }
        if (tail >= 0)
        {
            if (h.MipSizes[tail] != MipTail.TileBytes)
                throw new InvalidDataException($"The tail entry is {h.MipSizes[tail]} bytes, not a {MipTail.TileBytes}-byte tile.");
            stored[tail] = MipTail.Pack(tailMips, f.BlockBytes);
        }
        using var ms = new MemoryStream((int)h.ChunkSize);
        for (int i = n - 1; i >= 0; i--) ms.Write(stored[i]);
        return ms.ToArray();
    }
}

/// <summary>Stored-block framing: the 8-byte block headers a chunk's stored form carries.</summary>
public static class Framing
{
    /// <summary>
    /// The stored form of <paramref name="newRaw"/> with exactly the framing of <paramref name="oldStored"/>: its block
    /// headers are kept, only the payloads change. Throws if the old chunk has compressed blocks or the sizes differ.
    /// </summary>
    public static byte[] Reframe(byte[] oldStored, byte[] newRaw)
    {
        var blocks = FrostbiteBlocks.Parse(oldStored);
        long total = blocks.Sum(b => (long)b.RawSize);
        if (blocks.Any(b => b.Type != FrostbiteBlocks.TypeUncompressed || b.CompSize != b.RawSize))
            throw new NotSupportedException("The chunk has compressed blocks.");
        if (total != newRaw.Length)
            throw new InvalidDataException($"The new data is {newRaw.Length:N0} bytes but the chunk holds {total:N0}.");
        var result = new byte[oldStored.Length];
        int p = 0, r = 0;
        foreach (var b in blocks)
        {
            Buffer.BlockCopy(oldStored, p, result, p, 8);
            Buffer.BlockCopy(newRaw, r, result, p + 8, b.RawSize);
            p += 8 + b.CompSize;
            r += b.RawSize;
        }
        return result;
    }
}
