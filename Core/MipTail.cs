namespace NHL27Kit.Core;

/// <summary>
/// The mip tail: mips smaller than one tile don't get tiles of their own. They share one 4096-byte tile, each at a fixed
/// 256-byte slot, tiled by the normal pattern from the slot's start (found by matching a PS5 chunk's tail blocks to the
/// downsampled larger mips, and checked on BC1 and BC3 textures: BC1 holds 256+64+16+4+3 blocks, BC3 64+16+4+3).
/// A mip is in the tail when its block grid is smaller than a tile in either direction; the header's last mip size
/// (4096) is the tail tile, which comes first in the chunk, as the smallest data does.
/// </summary>
public static class MipTail
{
    public const int TileBytes = 4096;

    /// <summary>Slot (in 256-byte units) of the t-th tail mip, largest first.</summary>
    private static readonly int[] SlotUnits = { 8, 6, 5, 4, 3, 2, 1 };

    private static readonly Dictionary<int, Dictionary<(int, int), int>> Inverse = new();

    private static Dictionary<(int, int), int> InverseLut(int blockBytes)
    {
        lock (Inverse)
        {
            if (!Inverse.TryGetValue(blockBytes, out var inv))
            {
                inv = new Dictionary<(int, int), int>();
                var (tw, th) = Ps5Swizzle.Tile(blockBytes);
                // index -> (x, y) comes from deswizzling a ramp
                var probe = new byte[tw * th * blockBytes];
                for (int i = 0; i < tw * th; i++) { probe[i * blockBytes] = (byte)i; probe[i * blockBytes + 1] = (byte)(i >> 8); }
                var linear = Ps5Swizzle.Deswizzle(probe, tw, th, blockBytes);
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                        inv[(x, y)] = linear[(y * tw + x) * blockBytes] | (linear[(y * tw + x) * blockBytes + 1] << 8);
                Inverse[blockBytes] = inv;
            }
            return inv;
        }
    }

    /// <summary>True when a mip of this size is in the tail of the format's tiling.</summary>
    public static bool IsTailMip(int width, int height, int blockBytes)
    {
        var (tw, th) = Ps5Swizzle.Tile(blockBytes);
        return (width + 3) / 4 < tw || (height + 3) / 4 < th;
    }

    private static int SlotBlock(int t, int blockBytes)
    {
        if (t >= SlotUnits.Length) throw new NotSupportedException($"The mip tail holds {t + 1} mips, more than are understood.");
        return SlotUnits[t] * 256 / blockBytes;
    }

    /// <summary>Packs linear mips (largest first) into one tail tile.</summary>
    public static byte[] Pack(IReadOnlyList<(byte[] Blocks, int Bw, int Bh)> mips, int blockBytes)
    {
        var tile = new byte[TileBytes];
        var inv = InverseLut(blockBytes);
        for (int t = 0; t < mips.Count; t++)
        {
            var (blocks, bw, bh) = mips[t];
            int baseSlot = SlotBlock(t, blockBytes);
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    if (!inv.TryGetValue((x, y), out int index)) throw new NotSupportedException($"Tail mip {t}: block ({x},{y}) is outside a tile.");
                    int slot = baseSlot + index;
                    if ((slot + 1) * blockBytes > TileBytes) throw new NotSupportedException($"Tail mip {t} does not fit its slot.");
                    Buffer.BlockCopy(blocks, (y * bw + x) * blockBytes, tile, slot * blockBytes, blockBytes);
                }
        }
        return tile;
    }

    /// <summary>The linear blocks of the t-th tail mip, from a tail tile.</summary>
    public static byte[] Unpack(ReadOnlySpan<byte> tile, int t, int bw, int bh, int blockBytes)
    {
        var inv = InverseLut(blockBytes);
        int baseSlot = SlotBlock(t, blockBytes);
        var blocks = new byte[bw * bh * blockBytes];
        for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                int slot = baseSlot + inv[(x, y)];
                if ((slot + 1) * blockBytes <= tile.Length)
                    tile.Slice(slot * blockBytes, blockBytes).CopyTo(blocks.AsSpan((y * bw + x) * blockBytes));
            }
        return blocks;
    }
}
