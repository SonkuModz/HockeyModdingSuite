namespace NHL27Kit.Core;

/// <summary>
/// PS5 texture tiling: AMD addrlib SW_4K_S on block-compressed data (as NHL27TextureViewer found it, verified
/// pixel-perfect there). 16-byte blocks (BC3/BC5/BC7) tile in 16x16 blocks with the in-tile index bits (LSB first)
/// Y0 Y1 X0 X1 Y2 X2 Y3 X3; 8-byte blocks (BC1) in 32x16 blocks with X0 Y0 Y1 X1 X2 Y2 X3 Y3 X4. A mip is always a whole
/// number of tiles, so a mip smaller than a tile is padded to one (a 32x32 BC3 mip takes 4096 bytes, as the headers say).
/// </summary>
public static class Ps5Swizzle
{
    private static readonly (int X, int Y)[] Lut16 = Lut(new[] { 'y', 'y', 'x', 'x', 'y', 'x', 'y', 'x' });
    private static readonly (int X, int Y)[] Lut8 = Lut(new[] { 'x', 'y', 'y', 'x', 'x', 'y', 'x', 'y', 'x' });

    private static (int, int)[] Lut(char[] plan)
    {
        var lut = new (int, int)[1 << plan.Length];
        for (int i = 0; i < lut.Length; i++)
        {
            int x = 0, y = 0, xb = 0, yb = 0;
            for (int bit = 0; bit < plan.Length; bit++)
            {
                int v = (i >> bit) & 1;
                if (plan[bit] == 'x') x |= v << xb++;
                else y |= v << yb++;
            }
            lut[i] = (x, y);
        }
        return lut;
    }

    public static (int W, int H) Tile(int blockBytes) => blockBytes == 8 ? (32, 16) : (16, 16);

    /// <summary>Bytes a mip of <paramref name="blocksW"/> x <paramref name="blocksH"/> blocks takes when tiled (whole tiles).</summary>
    public static int TiledSize(int blocksW, int blocksH, int blockBytes)
    {
        var (tw, th) = Tile(blockBytes);
        return ((blocksW + tw - 1) / tw) * ((blocksH + th - 1) / th) * tw * th * blockBytes;
    }

    /// <summary>Reorders tiled blocks into rows (cropping the padding of the last tiles).</summary>
    public static byte[] Deswizzle(ReadOnlySpan<byte> data, int blocksW, int blocksH, int blockBytes)
    {
        var (tw, th) = Tile(blockBytes);
        var lut = blockBytes == 8 ? Lut8 : Lut16;
        var output = new byte[blocksW * blocksH * blockBytes];
        int tilesW = (blocksW + tw - 1) / tw, tilesH = (blocksH + th - 1) / th;
        int perTile = tw * th;
        for (int t = 0; t < tilesW * tilesH; t++)
        {
            int ox = (t % tilesW) * tw, oy = (t / tilesW) * th;
            for (int k = 0; k < perTile; k++)
            {
                int x = ox + lut[k].X, y = oy + lut[k].Y;
                if (x >= blocksW || y >= blocksH) continue;
                int src = (t * perTile + k) * blockBytes;
                if (src + blockBytes > data.Length) continue;
                data.Slice(src, blockBytes).CopyTo(output.AsSpan((y * blocksW + x) * blockBytes));
            }
        }
        return output;
    }

    /// <summary>The inverse of <see cref="Deswizzle"/>: rows of blocks into tiles, the padding left zero.</summary>
    public static byte[] Swizzle(ReadOnlySpan<byte> linear, int blocksW, int blocksH, int blockBytes)
    {
        var (tw, th) = Tile(blockBytes);
        var lut = blockBytes == 8 ? Lut8 : Lut16;
        var output = new byte[TiledSize(blocksW, blocksH, blockBytes)];
        int tilesW = (blocksW + tw - 1) / tw, tilesH = (blocksH + th - 1) / th;
        int perTile = tw * th;
        for (int t = 0; t < tilesW * tilesH; t++)
        {
            int ox = (t % tilesW) * tw, oy = (t / tilesW) * th;
            for (int k = 0; k < perTile; k++)
            {
                int x = ox + lut[k].X, y = oy + lut[k].Y;
                if (x >= blocksW || y >= blocksH) continue;
                linear.Slice((y * blocksW + x) * blockBytes, blockBytes).CopyTo(output.AsSpan((t * perTile + k) * blockBytes));
            }
        }
        return output;
    }
}
