using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// Reads the top level of a .dds file as RGBA8, so a DDS made in Paint.NET, GIMP, texconv or NVIDIA's tools can be used
/// as a replacement image (it is then encoded into the game texture's own format and mips, like a PNG). Supported:
/// DXT1/BC1, DXT3/BC2, DXT5/BC3, ATI2/BC5, BC4, BC7 (DX10 header) and uncompressed 32-bit RGBA/BGRA.
/// </summary>
public static class DdsReader
{
    public static bool IsDds(string file) => Path.GetExtension(file).Equals(".dds", StringComparison.OrdinalIgnoreCase);

    public static (byte[] Rgba, int W, int H) Load(string file) => Read(File.ReadAllBytes(file));

    public static (byte[] Rgba, int W, int H) Read(byte[] d)
    {
        if (d.Length < 128 || BinaryPrimitives.ReadUInt32LittleEndian(d) != 0x20534444) throw new InvalidDataException("Not a DDS file.");
        int U(int o) => (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o));
        int h = U(12), w = U(16);
        int pfFlags = U(80), fourCC = U(84), bits = U(88);
        int rMask = U(92), gMask = U(96), bMask = U(100), aMask = U(104);
        int data = 128;
        string kind;
        bool bgra = false;
        if ((pfFlags & 4) != 0)
        {
            string cc = System.Text.Encoding.ASCII.GetString(d, 84, 4);
            if (cc == "DX10")
            {
                int dxgi = U(128);
                data = 148;
                kind = dxgi switch
                {
                    70 or 71 or 72 => "BC1", 73 or 74 or 75 => "BC2", 76 or 77 or 78 => "BC3", 79 or 80 or 81 => "BC4", 82 or 83 or 84 => "BC5",
                    97 or 98 or 99 => "BC7", 27 or 28 or 29 => "RGBA", 87 or 88 or 90 or 91 => "BGRA",
                    _ => throw new NotSupportedException($"This DDS uses DXGI format {dxgi}, which the kit can't read. Save it as BC7, BC3/DXT5, BC1/DXT1 or 32-bit RGBA."),
                };
                if (kind == "BGRA") { kind = "RGBA"; bgra = true; }
            }
            else kind = cc switch
            {
                "DXT1" => "BC1", "DXT3" or "DXT2" => "BC2", "DXT5" or "DXT4" => "BC3", "ATI1" or "BC4U" => "BC4", "ATI2" or "BC5U" => "BC5",
                _ => throw new NotSupportedException($"This DDS is {cc}, which the kit can't read. Save it as BC7, BC3/DXT5, BC1/DXT1 or 32-bit RGBA."),
            };
        }
        else if (bits == 32)
        {
            kind = "RGBA";
            bgra = rMask == 0x00FF0000;
            _ = gMask; _ = bMask; _ = aMask;
        }
        else throw new NotSupportedException("Only 32-bit uncompressed DDS files can be read (or BC1/BC3/BC4/BC5/BC7).");

        if (kind == "RGBA")
        {
            var px = d.AsSpan(data, w * h * 4).ToArray();
            if (bgra) for (int i = 0; i < px.Length; i += 4) (px[i], px[i + 2]) = (px[i + 2], px[i]);
            if ((pfFlags & 1) == 0 && !(bits == 32 && aMask != 0)) for (int i = 3; i < px.Length; i += 4) px[i] = 255;
            return (px, w, h);
        }
        var format = kind switch
        {
            "BC1" => TextureFormat.Of(0x36), "BC3" => TextureFormat.Of(0x3C), "BC5" => TextureFormat.Of(0x3F), "BC7" => TextureFormat.Of(0x42),
            _ => null,
        };
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        if (kind == "BC4")
        {
            // BC4: a single channel, decoded as the red half of a BC5 block pair
            var pair = new byte[bw * bh * 16];
            for (int i = 0; i < bw * bh; i++) d.AsSpan(data + i * 8, 8).CopyTo(pair.AsSpan(i * 16));
            var two = BcDecoder.Decode(pair, w, h, TextureFormat.Of(0x3F));
            for (int i = 0; i < two.Length; i += 4) { two[i + 1] = two[i + 2] = two[i]; two[i + 3] = 255; }
            return (two, w, h);
        }
        if (kind == "BC2")
        {
            // BC2: 8 bytes of 4-bit alpha (one nibble per pixel, low nibble first) then a BC1 colour block
            if (data + bw * bh * 16 > d.Length) throw new InvalidDataException("The DDS file is shorter than its header says.");
            var colour = new byte[bw * bh * 8];
            for (int i = 0; i < bw * bh; i++) d.AsSpan(data + i * 16 + 8, 8).CopyTo(colour.AsSpan(i * 8));
            var px = BcDecoder.Decode(colour, w, h, TextureFormat.Of(0x36));
            for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                int blk = data + (by * bw + bx) * 16;
                for (int k = 0; k < 16; k++)
                {
                    int x = bx * 4 + k % 4, y = by * 4 + k / 4;
                    if (x >= w || y >= h) continue;
                    int nib = (d[blk + k / 2] >> (k % 2 * 4)) & 15;
                    px[(y * w + x) * 4 + 3] = (byte)(nib * 17);
                }
            }
            return (px, w, h);
        }
        int size = bw * bh * format.BlockBytes;
        if (data + size > d.Length) throw new InvalidDataException("The DDS file is shorter than its header says.");
        return (BcDecoder.Decode(d.AsSpan(data, size), w, h, format), w, h);
    }
}
