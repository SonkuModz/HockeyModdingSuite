using System.Buffers.Binary;
using System.IO.Compression;

namespace NHL27Kit.Core;

/// <summary>Block-compression decoders: BC1, BC3, BC4, BC5 and BC7 to RGBA8.</summary>
public static class BcDecoder
{
    /// <summary>Decodes rows of blocks (already linear) into width*height*4 RGBA bytes.</summary>
    public static byte[] Decode(ReadOnlySpan<byte> blocks, int width, int height, TextureFormat format)
    {
        int bw = (width + 3) / 4, bh = (height + 3) / 4;
        var rgba = new byte[width * height * 4];
        Span<byte> px = stackalloc byte[64];
        for (int by = 0; by < bh; by++)
        {
            for (int bx = 0; bx < bw; bx++)
            {
                var block = blocks.Slice((by * bw + bx) * format.BlockBytes, format.BlockBytes);
                switch (format.Name[..3])
                {
                    case "BC1": Bc1(block, px, opaqueOnly: false); break;
                    case "BC4": Bc4Gray(block, px); break;
                    case "BC3": Bc3(block, px); break;
                    case "BC5": Bc5(block, px); break;
                    default: Bc7(block, px); break;
                }
                for (int y = 0; y < 4; y++)
                {
                    int iy = by * 4 + y;
                    if (iy >= height) break;
                    for (int x = 0; x < 4; x++)
                    {
                        int ix = bx * 4 + x;
                        if (ix >= width) break;
                        px.Slice((y * 4 + x) * 4, 4).CopyTo(rgba.AsSpan((iy * width + ix) * 4));
                    }
                }
            }
        }
        return rgba;
    }

    private static void Bc1(ReadOnlySpan<byte> b, Span<byte> px, bool opaqueOnly)
    {
        ushort c0 = BinaryPrimitives.ReadUInt16LittleEndian(b), c1 = BinaryPrimitives.ReadUInt16LittleEndian(b[2..]);
        Span<byte> pal = stackalloc byte[16];
        Rgb565(c0, pal[..4]);
        Rgb565(c1, pal[4..8]);
        if (c0 > c1 || opaqueOnly)
        {
            for (int c = 0; c < 3; c++)
            {
                pal[8 + c] = (byte)((2 * pal[c] + pal[4 + c] + 1) / 3);
                pal[12 + c] = (byte)((pal[c] + 2 * pal[4 + c] + 1) / 3);
            }
            pal[11] = pal[15] = 255;
        }
        else
        {
            for (int c = 0; c < 3; c++)
                pal[8 + c] = (byte)((pal[c] + pal[4 + c]) / 2);
            pal[11] = 255;
            pal[12] = pal[13] = pal[14] = pal[15] = 0;
        }
        uint idx = BinaryPrimitives.ReadUInt32LittleEndian(b[4..]);
        for (int i = 0; i < 16; i++)
            pal.Slice((int)((idx >> (2 * i)) & 3) * 4, 4).CopyTo(px[(i * 4)..]);
    }

    private static void Rgb565(ushort c, Span<byte> o)
    {
        int r = (c >> 11) & 31, g = (c >> 5) & 63, bl = c & 31;
        o[0] = (byte)((r << 3) | (r >> 2));
        o[1] = (byte)((g << 2) | (g >> 4));
        o[2] = (byte)((bl << 3) | (bl >> 2));
        o[3] = 255;
    }

    /// <summary>One BC4 block (8 bytes) into channel <paramref name="channel"/> of the 16 pixels.</summary>
    private static void Bc4(ReadOnlySpan<byte> b, Span<byte> px, int channel)
    {
        Span<byte> a = stackalloc byte[8];
        a[0] = b[0];
        a[1] = b[1];
        if (a[0] > a[1])
            for (int i = 1; i < 7; i++) a[i + 1] = (byte)(((7 - i) * a[0] + i * a[1] + 3) / 7);
        else
        {
            for (int i = 1; i < 5; i++) a[i + 1] = (byte)(((5 - i) * a[0] + i * a[1] + 2) / 5);
            a[6] = 0;
            a[7] = 255;
        }
        ulong bits = 0;
        for (int i = 0; i < 6; i++) bits |= (ulong)b[2 + i] << (8 * i);
        for (int i = 0; i < 16; i++)
            px[i * 4 + channel] = a[(int)((bits >> (3 * i)) & 7)];
    }

    /// <summary>BC4: one channel, shown as grey.</summary>
    private static void Bc4Gray(ReadOnlySpan<byte> b, Span<byte> px)
    {
        Bc4(b, px, 0);
        for (int i = 0; i < 16; i++)
        {
            px[i * 4 + 1] = px[i * 4 + 2] = px[i * 4];
            px[i * 4 + 3] = 255;
        }
    }

    private static void Bc3(ReadOnlySpan<byte> b, Span<byte> px)
    {
        Bc1(b[8..], px, opaqueOnly: true);
        Bc4(b, px, 3);
    }

    /// <summary>BC5: red and green; blue rebuilt as a normal map's Z so normal maps preview as they shade.</summary>
    private static void Bc5(ReadOnlySpan<byte> b, Span<byte> px)
    {
        Bc4(b, px, 0);
        Bc4(b[8..], px, 1);
        for (int i = 0; i < 16; i++)
        {
            double x = px[i * 4] / 127.5 - 1, y = px[i * 4 + 1] / 127.5 - 1;
            double z = Math.Sqrt(Math.Max(0, 1 - x * x - y * y));
            px[i * 4 + 2] = (byte)Math.Round((z + 1) * 127.5);
            px[i * 4 + 3] = 255;
        }
    }

    // ---------------------------------------------------------------- BC7

    // per mode: subsets, partition bits, rotation bits, index-selection bit, colour bits, alpha bits,
    // endpoint p-bits (one per endpoint), shared p-bits (one per subset), index bits, second index bits
    private static readonly int[,] Modes =
    {
        { 3, 4, 0, 0, 4, 0, 1, 0, 3, 0 },
        { 2, 6, 0, 0, 6, 0, 0, 1, 3, 0 },
        { 3, 6, 0, 0, 5, 0, 0, 0, 2, 0 },
        { 2, 6, 0, 0, 7, 0, 1, 0, 2, 0 },
        { 1, 0, 2, 1, 5, 6, 0, 0, 2, 3 },
        { 1, 0, 2, 0, 7, 8, 0, 0, 2, 2 },
        { 1, 0, 0, 0, 7, 7, 1, 0, 4, 0 },
        { 2, 6, 0, 0, 5, 5, 1, 0, 2, 0 },
    };

    private static readonly byte[][] Weights =
    {
        null, null,
        new byte[] { 0, 21, 43, 64 },
        new byte[] { 0, 9, 18, 27, 37, 46, 55, 64 },
        new byte[] { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 },
    };

    private ref struct Bits
    {
        private readonly ReadOnlySpan<byte> _b;
        private int _pos;
        public Bits(ReadOnlySpan<byte> b, int pos) { _b = b; _pos = pos; }
        public int Read(int n)
        {
            int v = 0;
            for (int i = 0; i < n; i++, _pos++)
                v |= ((_b[_pos >> 3] >> (_pos & 7)) & 1) << i;
            return v;
        }
    }

    private static void Bc7(ReadOnlySpan<byte> b, Span<byte> px)
    {
        int mode = 0;
        while (mode < 8 && (b[0] & (1 << mode)) == 0) mode++;
        if (mode == 8)
        {
            px.Clear();
            return;
        }
        int ns = Modes[mode, 0], pb = Modes[mode, 1], rb = Modes[mode, 2], isb = Modes[mode, 3], cb = Modes[mode, 4], ab = Modes[mode, 5];
        int epb = Modes[mode, 6], spb = Modes[mode, 7], ib = Modes[mode, 8], ib2 = Modes[mode, 9];
        var r = new Bits(b, mode + 1);
        int partition = r.Read(pb), rotation = r.Read(rb), indexSel = r.Read(isb);

        // endpoints[subset*2 + end, channel]
        Span<int> ep = stackalloc int[6 * 4];
        for (int c = 0; c < 3; c++)
            for (int e = 0; e < ns * 2; e++)
                ep[e * 4 + c] = r.Read(cb);
        for (int e = 0; e < ns * 2; e++)
            ep[e * 4 + 3] = ab > 0 ? r.Read(ab) : 255;
        int colorBits = cb, alphaBits = ab;
        if (epb != 0 || spb != 0)
        {
            Span<int> p = stackalloc int[6];
            if (epb != 0) for (int e = 0; e < ns * 2; e++) p[e] = r.Read(1);
            else for (int s = 0; s < ns; s++) p[2 * s] = p[2 * s + 1] = r.Read(1);
            for (int e = 0; e < ns * 2; e++)
            {
                for (int c = 0; c < 3; c++) ep[e * 4 + c] = (ep[e * 4 + c] << 1) | p[e];
                if (ab > 0) ep[e * 4 + 3] = (ep[e * 4 + 3] << 1) | p[e];
            }
            colorBits++;
            if (ab > 0) alphaBits++;
        }
        for (int e = 0; e < ns * 2; e++)
        {
            for (int c = 0; c < 3; c++) ep[e * 4 + c] = Expand(ep[e * 4 + c], colorBits);
            if (ab > 0) ep[e * 4 + 3] = Expand(ep[e * 4 + 3], alphaBits);
        }

        // which subset each pixel is in, and the anchor pixels whose index has one bit less
        Span<int> subset = stackalloc int[16];
        int anchor1 = 0, anchor2 = 0;
        if (ns == 2)
        {
            for (int i = 0; i < 16; i++) subset[i] = Bc7Tables.Partition2[partition * 16 + i];
            anchor1 = Bc7Tables.Fixup2[partition];
        }
        else if (ns == 3)
        {
            for (int i = 0; i < 16; i++) subset[i] = Bc7Tables.Partition3[partition * 16 + i];
            anchor1 = Bc7Tables.Fixup3[partition * 2];
            anchor2 = Bc7Tables.Fixup3[partition * 2 + 1];
        }
        bool IsAnchor(int i) => i == 0 || (ns >= 2 && i == anchor1) || (ns == 3 && i == anchor2);

        Span<int> idx1 = stackalloc int[16];
        Span<int> idx2 = stackalloc int[16];
        for (int i = 0; i < 16; i++) idx1[i] = r.Read(IsAnchor(i) ? ib - 1 : ib);
        if (ib2 > 0)
            for (int i = 0; i < 16; i++) idx2[i] = r.Read(i == 0 ? ib2 - 1 : ib2);

        for (int i = 0; i < 16; i++)
        {
            int s = subset[i];
            int e0 = s * 2 * 4, e1 = (s * 2 + 1) * 4;
            int ci, cbits, ai, abits;
            if (ib2 == 0) { ci = ai = idx1[i]; cbits = abits = ib; }
            else if (indexSel == 0) { ci = idx1[i]; cbits = ib; ai = idx2[i]; abits = ib2; }
            else { ci = idx2[i]; cbits = ib2; ai = idx1[i]; abits = ib; }
            int wc = Weights[cbits][ci], wa = Weights[abits][ai];
            int o = i * 4;
            for (int c = 0; c < 3; c++)
                px[o + c] = (byte)(((64 - wc) * ep[e0 + c] + wc * ep[e1 + c] + 32) >> 6);
            px[o + 3] = (byte)(((64 - wa) * ep[e0 + 3] + wa * ep[e1 + 3] + 32) >> 6);
            switch (rotation)
            {
                case 1: (px[o], px[o + 3]) = (px[o + 3], px[o]); break;
                case 2: (px[o + 1], px[o + 3]) = (px[o + 3], px[o + 1]); break;
                case 3: (px[o + 2], px[o + 3]) = (px[o + 3], px[o + 2]); break;
            }
        }
    }

    private static int Expand(int v, int bits)
    {
        v <<= 8 - bits;
        return v | (v >> bits);
    }
}

/// <summary>Writes DDS (block data) and PNG (RGBA) files.</summary>
public static class ImageFiles
{
    /// <summary>A DDS of linear block data: DX10 header for BC7, FourCC for the rest. Mips are listed largest first.</summary>
    public static byte[] Dds(int width, int height, TextureFormat format, IReadOnlyList<byte[]> mips)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        bool dx10 = format.FourCC == null;
        w.Write(0x20534444u); // "DDS "
        w.Write(124u);
        w.Write(0x1u | 0x2u | 0x4u | 0x1000u | 0x80000u | (mips.Count > 1 ? 0x20000u : 0u)); // caps|height|width|pixelformat|linearsize|mipcount
        w.Write((uint)height);
        w.Write((uint)width);
        w.Write((uint)mips[0].Length);
        w.Write(0u);
        w.Write((uint)mips.Count);
        for (int i = 0; i < 11; i++) w.Write(0u);
        w.Write(32u);
        w.Write(0x4u); // DDPF_FOURCC
        w.Write(System.Text.Encoding.ASCII.GetBytes(dx10 ? "DX10" : format.FourCC));
        for (int i = 0; i < 5; i++) w.Write(0u);
        w.Write(0x1000u | (mips.Count > 1 ? 0x400008u : 0u)); // texture | complex+mipmap
        for (int i = 0; i < 4; i++) w.Write(0u);
        if (dx10)
        {
            w.Write((uint)format.DxgiFormat);
            w.Write(3u); // TEXTURE2D
            w.Write(0u);
            w.Write(1u);
            w.Write(0u);
        }
        foreach (var m in mips) w.Write(m);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>An RGBA8 PNG.</summary>
    public static byte[] Png(byte[] rgba, int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(ms, "IHDR", ihdr);
        using (var raw = new MemoryStream())
        {
            using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
                for (int y = 0; y < height; y++)
                {
                    z.WriteByte(0);
                    z.Write(rgba, y * width * 4, width * 4);
                }
            Chunk(ms, "IDAT", raw.ToArray());
        }
        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> n = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(n, data.Length);
        s.Write(n);
        var td = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, td);
        data.CopyTo(td, 4);
        s.Write(td);
        BinaryPrimitives.WriteUInt32BigEndian(n, Crc(td));
        s.Write(n);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte x in d) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}

/// <summary>A texture read from the game: its header, the data chunk, and its largest stored mip decoded.</summary>
public sealed class Texture
{
    public string Name { get; init; }
    public TextureHeader Header { get; init; }
    public ChunkEntry Chunk { get; init; }
    /// <summary>The decompressed data chunk: the mips, smallest first, the largest at the end.</summary>
    public byte[] Data { get; init; }
    /// <summary>The layer of an array <see cref="DecodeMip"/> reads (layers are stored one after another).</summary>
    public int Layer { get; set; }
    /// <summary>The mip level <see cref="Rgba"/> holds (0 = full size).</summary>
    public int Mip { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    /// <summary>The mip's linear (de-swizzled) block data, for DDS.</summary>
    public byte[] Blocks { get; private set; }
    public byte[] Rgba { get; private set; }
    public string Problem { get; private set; }

    /// <summary>
    /// Reads the texture <paramref name="name"/> (its RES header and the chunk the header names) and decodes the largest
    /// mip the chunk holds. The mips are stored smallest first, so the largest that fits is at the end of the data.
    /// </summary>
    public static Texture Load(AssetIndex index, string name, bool decode = true)
    {
        var res = index.GetRes(name) ?? throw new InvalidDataException($"No RES named {name}.");
        var header = TextureHeader.Parse(index.Read(res), ps4: !index.Layout.IsPs5);
        // A streamed texture's bundle lists only part of its chunk (the small mips); the whole chunk, every mip, is in a
        // TOC chunk table under the same id. Take the copy whose size matches the header, else the largest.
        var candidates = (index.TableChunks.GetValueOrDefault(header.ChunkId) ?? new List<ChunkEntry>()).ToList();
        if (index.GetChunk(header.ChunkId) is { } listed && !candidates.Contains(listed))
            candidates.Add(listed);
        if (candidates.Count == 0)
            throw new InvalidDataException($"The texture's chunk {header.ChunkId} is in no TOC or bundle.");
        ChunkEntry chunk = null;
        byte[] data = null;
        foreach (var c in candidates)
        {
            byte[] d;
            try { d = index.Read(c); }
            catch { continue; }
            if (data == null || d.Length > data.Length)
                (chunk, data) = (c, d);
            if (d.Length == header.ChunkSize)
                break;
        }
        if (data == null)
            throw new InvalidDataException($"No copy of the texture's chunk {header.ChunkId} could be read.");
        var tex = new Texture { Name = name, Header = header, Chunk = chunk, Data = data };
        if (decode)
            tex.DecodeLargest();
        return tex;
    }

    public int MipCount => Math.Max(1, (int)Header.MipCount);

    /// <summary>Entries the chunk stores: the header's mip sizes, up to the first empty one.</summary>
    private int Stored => TextureBuilder.StoredMips(Header);

    /// <summary>The first mip of the shared tail tile, or -1 when the chunk has none.</summary>
    public int TailStart
    {
        get
        {
            int last = Stored - 1;
            // a texture of one mip is tiled plainly, whatever its shape (the 4x40 score clock gradient, 444x32 lights)
            if (last < 0 || MipCount <= 1) return -1;
            var (w, h) = Header.MipSize(last);
            return MipTail.IsTailMip(w, h, Header.Format.BlockBytes) ? last : -1;
        }
    }

    /// <summary>Stored bytes of mip <paramref name="mip"/> (a tail mip: the shared tail tile).</summary>
    public int MipBytes(int mip)
    {
        if (Header.Ps4)
        {
            if (mip < 15 && Header.MipSizes[mip] != 0)
                return (int)Header.MipSizes[mip];
            var (pw, ph) = Header.MipSize(mip);
            return Ps4Tiling.TiledSize((pw + 3) / 4, (ph + 3) / 4, Header.Format.BlockBytes);
        }
        int entry = TailStart >= 0 ? Math.Min(mip, TailStart) : mip;
        if (entry < 15 && Header.MipSizes[entry] != 0)
            return (int)Header.MipSizes[entry];
        var (w, h) = Header.MipSize(mip);
        return Ps5Swizzle.TiledSize((w + 3) / 4, (h + 3) / 4, Header.Format.BlockBytes);
    }

    /// <summary>Where mip <paramref name="mip"/> starts in <see cref="Data"/> (mips are stored smallest first, mip 0 last); negative when the chunk does not hold it.</summary>
    public long MipStart(int mip)
    {
        if (Header.Ps4)
        {
            // largest first, mip by mip, each mip's layers one after another; a chunk without the large mips (the
            // bundle copy of a texture with FirstMip > 0) is the full chain's tail
            int layers = Header.Layers;
            long full = 0, start = 0;
            for (int i = 0; i < MipCount; i++)
            {
                if (i == mip) start = full + (long)Math.Clamp(Layer, 0, layers - 1) * MipBytes(i);
                full += (long)MipBytes(i) * layers;
            }
            long at = start - Math.Max(0, full - Data.Length);
            return at >= 0 && at + MipBytes(mip) <= Data.Length ? at : -1;
        }
        int entry = TailStart >= 0 ? Math.Min(mip, TailStart) : mip;
        long end = Data.Length;
        // an array's layers each take the header's chunk size, in order
        if (Header.Layers > 1 && Data.Length >= (long)Header.Layers * Header.ChunkSize)
            end = (long)(Math.Clamp(Layer, 0, Header.Layers - 1) + 1) * Header.ChunkSize;
        for (int i = 0; i < entry; i++) end -= MipBytes(i);
        return end - MipBytes(entry);
    }

    /// <summary>The largest mip the chunk holds, or -1.</summary>
    public int FirstResidentMip => Enumerable.Range(0, MipCount).FirstOrDefault(m => MipStart(m) >= 0, -1);

    /// <summary>The smallest resident mip still at least <paramref name="minSize"/> pixels on its longer side (for thumbnails).</summary>
    public int MipForSize(int minSize)
    {
        int best = FirstResidentMip;
        if (best < 0) return -1;
        for (int m = best + 1; m < MipCount; m++)
        {
            var (w, h) = Header.MipSize(m);
            if (Math.Max(w, h) < minSize || MipStart(m) < 0) break;
            best = m;
        }
        return best;
    }

    public void DecodeLargest() => DecodeMip(FirstResidentMip);

    /// <summary>Decodes mip <paramref name="mip"/> into <see cref="Rgba"/> (and its linear blocks into <see cref="Blocks"/>).</summary>
    public void DecodeMip(int mip)
    {
        var f = Header.Format;
        Problem = null;
        if (!f.Known)
        {
            Problem = $"Pixel format {f.Name} has no decoder yet.";
            return;
        }
        if (mip < 0 || mip >= MipCount || MipStart(mip) < 0)
        {
            Problem = $"The chunk holds {Data.Length:N0} bytes, too few for {(mip < 0 ? "any mip" : $"mip {mip}")} of a {Header.Width}x{Header.Height} {f.Name} texture.";
            return;
        }
        var (w, h) = Header.MipSize(mip);
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        Mip = mip;
        Width = w;
        Height = h;
        var stored = Data.AsSpan((int)MipStart(mip), MipBytes(mip));
        if (Header.Ps4)
        {
            Blocks = (Header.Flags & TextureFlags.Swizzle) != 0
                ? Ps4Tiling.Untile(stored, bw, bh, f.BlockBytes)
                : stored[..Math.Min(stored.Length, bw * bh * f.BlockBytes)].ToArray();
            Rgba = BcDecoder.Decode(Blocks, w, h, f);
            return;
        }
        int tail = TailStart;
        Blocks = tail >= 0 && mip >= tail ? MipTail.Unpack(stored, mip - tail, bw, bh, f.BlockBytes) : Ps5Swizzle.Deswizzle(stored, bw, bh, f.BlockBytes);
        Rgba = BcDecoder.Decode(Blocks, w, h, f);
    }

    public byte[] ToDds() => ImageFiles.Dds(Width, Height, Header.Format, new[] { Blocks });
    public byte[] ToPng() => ImageFiles.Png(Rgba, Width, Height);
}
