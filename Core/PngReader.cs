using System.Buffers.Binary;
using System.IO.Compression;

namespace NHL27Kit.Core;

/// <summary>
/// Reads a PNG into RGBA8 pixels without a UI toolkit (the core runs under the command line too): 8-bit grey, grey +
/// alpha, RGB, RGBA and palette images, and 16-bit ones (cut to 8 bits); not interlaced ones.
/// </summary>
public static class PngReader
{
    public static (byte[] Rgba, int Width, int Height) Load(string file) => Read(File.ReadAllBytes(file), file);

    public static (byte[] Rgba, int Width, int Height) Read(byte[] png, string name = "image")
    {
        if (png.Length < 8 || BinaryPrimitives.ReadUInt64BigEndian(png) != 0x89504E470D0A1A0A) throw new InvalidDataException($"{name} isn't a PNG.");
        int w = 0, h = 0, depth = 0, type = 0, interlace = 0;
        byte[] palette = null, trns = null;
        var idat = new MemoryStream();
        for (int p = 8; p + 8 <= png.Length;)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p));
            string kind = System.Text.Encoding.ASCII.GetString(png, p + 4, 4);
            var data = png.AsSpan(p + 8, len);
            switch (kind)
            {
                case "IHDR":
                    w = BinaryPrimitives.ReadInt32BigEndian(data);
                    h = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    depth = data[8]; type = data[9]; interlace = data[12];
                    break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": trns = data.ToArray(); break;
                case "IDAT": idat.Write(data); break;
            }
            if (kind == "IEND") break;
            p += 12 + len;
        }
        if (w <= 0 || h <= 0) throw new InvalidDataException($"{name} has no image header.");
        if (interlace != 0) throw new NotSupportedException($"{name} is interlaced; save it without interlacing.");
        int channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new NotSupportedException($"{name}: PNG colour type {type}.") };
        if (!(depth == 8 || depth == 16 || (type == 3 && depth is 1 or 2 or 4)))
            throw new NotSupportedException($"{name}: {depth}-bit PNGs of colour type {type} aren't read.");

        int bitsPerPixel = channels * depth;
        int stride = (w * bitsPerPixel + 7) / 8, bpp = Math.Max(1, bitsPerPixel / 8);
        var raw = new byte[stride * h];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        {
            var prev = new byte[stride];
            var line = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                int filter = z.ReadByte();
                z.ReadExactly(line);
                for (int x = 0; x < stride; x++)
                {
                    int a = x >= bpp ? line[x - bpp] : 0, b = prev[x], c = x >= bpp ? prev[x - bpp] : 0;
                    line[x] += filter switch
                    {
                        0 => 0,
                        1 => (byte)a,
                        2 => (byte)b,
                        3 => (byte)((a + b) / 2),
                        4 => Paeth(a, b, c),
                        _ => throw new InvalidDataException($"{name}: bad PNG filter {filter}."),
                    };
                }
                line.CopyTo(raw, y * stride);
                (prev, line) = (line, prev);
            }
        }

        var rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 4, row = y * stride;
                byte S(int ch) => depth == 16 ? raw[row + (x * channels + ch) * 2] : raw[row + x * channels + ch];
                switch (type)
                {
                    case 0: rgba[o] = rgba[o + 1] = rgba[o + 2] = S(0); rgba[o + 3] = 255; break;
                    case 4: rgba[o] = rgba[o + 1] = rgba[o + 2] = S(0); rgba[o + 3] = S(1); break;
                    case 2: rgba[o] = S(0); rgba[o + 1] = S(1); rgba[o + 2] = S(2); rgba[o + 3] = 255; break;
                    case 6: rgba[o] = S(0); rgba[o + 1] = S(1); rgba[o + 2] = S(2); rgba[o + 3] = S(3); break;
                    case 3:
                        int i = depth == 8 ? raw[row + x] : (raw[row + x * depth / 8] >> (8 - depth - x * depth % 8)) & ((1 << depth) - 1);
                        if (palette != null && i * 3 + 2 < palette.Length) { rgba[o] = palette[i * 3]; rgba[o + 1] = palette[i * 3 + 1]; rgba[o + 2] = palette[i * 3 + 2]; }
                        rgba[o + 3] = trns != null && i < trns.Length ? trns[i] : (byte)255;
                        break;
                }
            }
        return (rgba, w, h);
    }

    private static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }
}
