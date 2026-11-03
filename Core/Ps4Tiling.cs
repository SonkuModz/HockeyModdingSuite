namespace NHL27Kit.Core;

/// <summary>
/// PS4 texture tiling (a header with the <see cref="TextureFlags.Swizzle"/> flag): elements (4x4 BC blocks, or pixels
/// for uncompressed formats) in 8x8-element tiles, tiles in rows, the 64 elements of a tile in Morton order (x from
/// index bits 0, 2, 4; y from bits 1, 3, 5). Every mip is tiled on its own and padded to whole tiles, which is why a
/// 512x512 BC3 chain takes 354,304 bytes, not 349,524.
/// </summary>
public static class Ps4Tiling
{
    private static readonly (int X, int Y)[] Morton = Enumerable.Range(0, 64)
        .Select(i => ((i & 1) | ((i >> 1) & 2) | ((i >> 2) & 4), ((i >> 1) & 1) | ((i >> 2) & 2) | ((i >> 3) & 4))).ToArray();

    /// <summary>Bytes a mip of <paramref name="w"/> x <paramref name="h"/> elements takes when tiled (whole tiles).</summary>
    public static int TiledSize(int w, int h, int elementBytes) => (w + 7) / 8 * ((h + 7) / 8) * 64 * elementBytes;

    /// <summary>Reorders tiled elements into rows (cropping the padding of the last tiles).</summary>
    public static byte[] Untile(ReadOnlySpan<byte> tiled, int w, int h, int elementBytes)
    {
        var linear = new byte[w * h * elementBytes];
        int tilesW = (w + 7) / 8, tilesH = (h + 7) / 8;
        for (int ty = 0, src = 0; ty < tilesH; ty++)
            for (int tx = 0; tx < tilesW; tx++)
                for (int i = 0; i < 64; i++, src += elementBytes)
                {
                    int x = tx * 8 + Morton[i].X, y = ty * 8 + Morton[i].Y;
                    if (x < w && y < h && src + elementBytes <= tiled.Length)
                        tiled.Slice(src, elementBytes).CopyTo(linear.AsSpan((y * w + x) * elementBytes));
                }
        return linear;
    }

    /// <summary>Reorders rows of elements into tiles (the padding left zero).</summary>
    public static byte[] Tile(ReadOnlySpan<byte> linear, int w, int h, int elementBytes)
    {
        var tiled = new byte[TiledSize(w, h, elementBytes)];
        int tilesW = (w + 7) / 8, tilesH = (h + 7) / 8;
        for (int ty = 0, dst = 0; ty < tilesH; ty++)
            for (int tx = 0; tx < tilesW; tx++)
                for (int i = 0; i < 64; i++, dst += elementBytes)
                {
                    int x = tx * 8 + Morton[i].X, y = ty * 8 + Morton[i].Y;
                    if (x < w && y < h)
                        linear.Slice((y * w + x) * elementBytes, elementBytes).CopyTo(tiled.AsSpan(dst));
                }
        return tiled;
    }
}
