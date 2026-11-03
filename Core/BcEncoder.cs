namespace NHL27Kit.Core;

/// <summary>
/// Block-compression encoders for the formats NHL 27 uses. BC1/BC3 colour fits an axis through the block's colours and
/// BC4/BC5 fit min/max (quick, and fine for those formats). BC7 goes through BCnEncoder.Net at its best quality, which
/// tries BC7's partitioned and separate-alpha modes per block: a block mixing several colours and transparency (a logo's
/// edge) needs them. The kit's own mode-6-only BC7 is kept as a fallback. Before a BC7 or BC3 encode, fully transparent
/// pixels take the colour of the nearest visible pixel, so blocks don't pull black into a logo's edges.
/// </summary>
public static class BcEncoder
{
    /// <summary>Encodes RGBA8 pixels into rows of blocks (linear, not yet tiled).</summary>
    public static byte[] Encode(byte[] rgba, int width, int height, TextureFormat format)
    {
        int bw = (width + 3) / 4, bh = (height + 3) / 4, bb = format.BlockBytes;
        string kind = bb == 8 ? "BC1" : format.Name[..3];
        if (kind is "BC7" or "BC3") rgba = MipChain.BleedTransparent(rgba, width, height);
        if (kind == "BC7")
        {
            try { return Bc7Full(rgba, width, height, bw * bh * bb); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* the kit's own mode 6 below */ }
        }
        var output = new byte[bw * bh * bb];
        Parallel.For(0, bh, by =>
        {
            Span<byte> px = stackalloc byte[64];
            for (int bx = 0; bx < bw; bx++)
            {
                for (int y = 0; y < 4; y++)
                {
                    int sy = Math.Min(height - 1, by * 4 + y);
                    for (int x = 0; x < 4; x++)
                    {
                        int sx = Math.Min(width - 1, bx * 4 + x);
                        rgba.AsSpan((sy * width + sx) * 4, 4).CopyTo(px[((y * 4 + x) * 4)..]);
                    }
                }
                var dst = output.AsSpan((by * bw + bx) * bb, bb);
                switch (kind)
                {
                    case "BC1": Bc1(px, dst); break;
                    case "BC3": Bc4(px, 3, dst[..8]); Bc1(px, dst[8..]); break;
                    case "BC5": Bc4(px, 0, dst[..8]); Bc4(px, 1, dst[8..]); break;
                    default: Bc7Mode6(px, dst); break;
                }
            }
        });
        return output;
    }

    /// <summary>BC7 with every mode, through BCnEncoder.Net (best quality, blocks in parallel).</summary>
    private static byte[] Bc7Full(byte[] rgba, int width, int height, int expected)
    {
        var enc = new BCnEncoder.Encoder.BcEncoder();
        enc.OutputOptions.Format = BCnEncoder.Shared.CompressionFormat.Bc7;
        enc.OutputOptions.Quality = BCnEncoder.Encoder.CompressionQuality.Balanced;
        enc.OutputOptions.GenerateMipMaps = false;
        enc.Options.IsParallel = true;
        var raw = enc.EncodeToRawBytes(rgba, width, height, BCnEncoder.Encoder.PixelFormat.Rgba32, 0, out _, out _);
        if (raw.Length != expected) throw new InvalidDataException($"BC7 encoder gave {raw.Length} bytes, expected {expected}.");
        return raw;
    }

    // ---------------------------------------------------------------- BC1

    private static ushort To565(double r, double g, double b)
    {
        int R = Math.Clamp((int)Math.Round(r * 31 / 255), 0, 31), G = Math.Clamp((int)Math.Round(g * 63 / 255), 0, 63), B = Math.Clamp((int)Math.Round(b * 31 / 255), 0, 31);
        return (ushort)((R << 11) | (G << 5) | B);
    }

    private static void From565(ushort c, Span<int> o)
    {
        int r = (c >> 11) & 31, g = (c >> 5) & 63, b = c & 31;
        o[0] = (r << 3) | (r >> 2);
        o[1] = (g << 2) | (g >> 4);
        o[2] = (b << 3) | (b >> 2);
    }

    private static void Bc1(ReadOnlySpan<byte> px, Span<byte> dst)
    {
        // mean and principal axis of the 16 colours
        double mr = 0, mg = 0, mb = 0;
        for (int i = 0; i < 16; i++) { mr += px[i * 4]; mg += px[i * 4 + 1]; mb += px[i * 4 + 2]; }
        mr /= 16; mg /= 16; mb /= 16;
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        for (int i = 0; i < 16; i++)
        {
            double r = px[i * 4] - mr, g = px[i * 4 + 1] - mg, b = px[i * 4 + 2] - mb;
            xx += r * r; xy += r * g; xz += r * b; yy += g * g; yz += g * b; zz += b * b;
        }
        double ax = 0.57735, ay = 0.57735, az = 0.57735;
        for (int it = 0; it < 6; it++)
        {
            double nx = xx * ax + xy * ay + xz * az, ny = xy * ax + yy * ay + yz * az, nz = xz * ax + yz * ay + zz * az;
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-9) break;
            ax = nx / len; ay = ny / len; az = nz / len;
        }
        double tmin = double.MaxValue, tmax = double.MinValue;
        for (int i = 0; i < 16; i++)
        {
            double t = (px[i * 4] - mr) * ax + (px[i * 4 + 1] - mg) * ay + (px[i * 4 + 2] - mb) * az;
            tmin = Math.Min(tmin, t); tmax = Math.Max(tmax, t);
        }

        // candidates: the full range and slightly inset ones; the least error wins
        ushort bestC0 = 0, bestC1 = 0;
        uint bestIdx = 0;
        double bestErr = double.MaxValue;
        Span<int> pal = stackalloc int[12];
        foreach (double inset in new[] { 0.0, 1.0 / 16, 1.0 / 8 })
        {
            double lo = tmin + (tmax - tmin) * inset, hi = tmax - (tmax - tmin) * inset;
            ushort c0 = To565(mr + ax * hi, mg + ay * hi, mb + az * hi), c1 = To565(mr + ax * lo, mg + ay * lo, mb + az * lo);
            if (c0 < c1) (c0, c1) = (c1, c0);
            From565(c0, pal[..3]); From565(c1, pal[3..6]);
            for (int c = 0; c < 3; c++)
            {
                pal[6 + c] = (2 * pal[c] + pal[3 + c] + 1) / 3;
                pal[9 + c] = (pal[c] + 2 * pal[3 + c] + 1) / 3;
            }
            double err = 0;
            uint idx = 0;
            for (int i = 0; i < 16; i++)
            {
                int best = 0, bd = int.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    int dr = px[i * 4] - pal[k == 0 ? 0 : k == 1 ? 3 : k == 2 ? 6 : 9], dg = px[i * 4 + 1] - pal[(k == 0 ? 0 : k == 1 ? 3 : k == 2 ? 6 : 9) + 1],
                        db = px[i * 4 + 2] - pal[(k == 0 ? 0 : k == 1 ? 3 : k == 2 ? 6 : 9) + 2];
                    int d = dr * dr + dg * dg + db * db;
                    if (d < bd) { bd = d; best = k; }
                }
                idx |= (uint)best << (2 * i);
                err += bd;
            }
            if (err < bestErr) { bestErr = err; bestC0 = c0; bestC1 = c1; bestIdx = idx; }
            if (c0 == c1) break;
        }
        // the decoder's four-colour mode needs c0 > c1; with equal endpoints index 0 is always right
        if (bestC0 == bestC1) bestIdx = 0;
        // palette order: 0 = c0, 1 = c1, 2 = 2/3 c0 + 1/3 c1, 3 = 1/3 c0 + 2/3 c1 (the order the decoder uses)
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(dst, bestC0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(dst[2..], bestC1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dst[4..], bestIdx);
    }

    // ---------------------------------------------------------------- BC4 / BC5

    /// <summary>One channel of the 16 pixels as an 8-byte BC4 block (the 8-value mode, endpoint 0 above endpoint 1).</summary>
    private static void Bc4(ReadOnlySpan<byte> px, int channel, Span<byte> dst)
    {
        int lo = 255, hi = 0;
        for (int i = 0; i < 16; i++) { int v = px[i * 4 + channel]; lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
        Span<int> a = stackalloc int[8];
        a[0] = hi; a[1] = lo;
        if (hi == lo)
        {
            dst[0] = (byte)hi; dst[1] = (byte)lo;
            dst[2..8].Clear();
            return;
        }
        for (int i = 1; i < 7; i++) a[i + 1] = ((7 - i) * hi + i * lo + 3) / 7;
        ulong bits = 0;
        for (int i = 0; i < 16; i++)
        {
            int v = px[i * 4 + channel], best = 0, bd = int.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                int d = Math.Abs(v - a[k]);
                if (d < bd) { bd = d; best = k; }
            }
            bits |= (ulong)best << (3 * i);
        }
        dst[0] = (byte)hi; dst[1] = (byte)lo;
        for (int i = 0; i < 6; i++) dst[2 + i] = (byte)(bits >> (8 * i));
    }

    // ---------------------------------------------------------------- BC7 mode 6

    private static readonly int[] W4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

    private static void Bc7Mode6(ReadOnlySpan<byte> px, Span<byte> dst)
    {
        // axis through the colours (RGBA)
        Span<double> mean = stackalloc double[4];
        for (int i = 0; i < 16; i++) for (int c = 0; c < 4; c++) mean[c] += px[i * 4 + c];
        for (int c = 0; c < 4; c++) mean[c] /= 16;
        Span<double> cov = stackalloc double[16];
        for (int i = 0; i < 16; i++)
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                    cov[a * 4 + b] += (px[i * 4 + a] - mean[a]) * (px[i * 4 + b] - mean[b]);
        Span<double> axis = stackalloc double[4] { 0.5, 0.5, 0.5, 0.5 };
        for (int it = 0; it < 6; it++)
        {
            Span<double> n = stackalloc double[4];
            double len = 0;
            for (int a = 0; a < 4; a++) { for (int b = 0; b < 4; b++) n[a] += cov[a * 4 + b] * axis[b]; len += n[a] * n[a]; }
            len = Math.Sqrt(len);
            if (len < 1e-9) break;
            for (int a = 0; a < 4; a++) axis[a] = n[a] / len;
        }
        double tmin = double.MaxValue, tmax = double.MinValue;
        for (int i = 0; i < 16; i++)
        {
            double t = 0;
            for (int c = 0; c < 4; c++) t += (px[i * 4 + c] - mean[c]) * axis[c];
            tmin = Math.Min(tmin, t); tmax = Math.Max(tmax, t);
        }
        Span<int> v0 = stackalloc int[4], v1 = stackalloc int[4];
        for (int c = 0; c < 4; c++)
        {
            v0[c] = Math.Clamp((int)Math.Round(mean[c] + axis[c] * tmin), 0, 255);
            v1[c] = Math.Clamp((int)Math.Round(mean[c] + axis[c] * tmax), 0, 255);
        }

        // endpoints are 7 bits plus a p-bit shared by the four channels: pick each p-bit for the least rounding error
        Span<int> q0 = stackalloc int[4], q1 = stackalloc int[4], e0 = stackalloc int[4], e1 = stackalloc int[4];
        int p0 = Quantize(v0, q0, e0), p1 = Quantize(v1, q1, e1);

        // indices: nearest of 16 interpolated colours
        Span<int> idx = stackalloc int[16];
        for (int i = 0; i < 16; i++)
        {
            int best = 0, bd = int.MaxValue;
            for (int k = 0; k < 16; k++)
            {
                int d = 0;
                for (int c = 0; c < 4; c++)
                {
                    int val = ((64 - W4[k]) * e0[c] + W4[k] * e1[c] + 32) >> 6;
                    int diff = px[i * 4 + c] - val;
                    d += diff * diff;
                }
                if (d < bd) { bd = d; best = k; }
            }
            idx[i] = best;
        }
        // the first pixel's index has one bit less: its top bit must be 0, so swap the endpoints if it is not
        if (idx[0] >= 8)
        {
            for (int c = 0; c < 4; c++) { (q0[c], q1[c]) = (q1[c], q0[c]); }
            (p0, p1) = (p1, p0);
            for (int i = 0; i < 16; i++) idx[i] = 15 - idx[i];
        }

        var block = new byte[16];
        int pos = 0;
        void Put(int value, int bits)
        {
            for (int b = 0; b < bits; b++, pos++)
                if (((value >> b) & 1) != 0) block[pos >> 3] |= (byte)(1 << (pos & 7));
        }
        Put(1 << 6, 7); // mode 6: six zero bits then a one
        for (int c = 0; c < 4; c++) { Put(q0[c], 7); Put(q1[c], 7); }
        Put(p0, 1); Put(p1, 1);
        for (int i = 0; i < 16; i++) Put(idx[i], i == 0 ? 3 : 4);
        block.CopyTo(dst);
    }

    /// <summary>Splits 8-bit endpoint values into 7-bit values and one shared p-bit; <paramref name="exact"/> gets the values the decoder will see.</summary>
    private static int Quantize(ReadOnlySpan<int> v, Span<int> q, Span<int> exact)
    {
        int bestP = 0, bestErr = int.MaxValue;
        Span<int> tq = stackalloc int[4];
        for (int p = 0; p < 2; p++)
        {
            int err = 0;
            for (int c = 0; c < 4; c++)
            {
                tq[c] = Math.Clamp((int)Math.Round((v[c] - p) / 2.0), 0, 127);
                int e = (tq[c] << 1) | p;
                err += (e - v[c]) * (e - v[c]);
            }
            if (err < bestErr)
            {
                bestErr = err; bestP = p;
                for (int c = 0; c < 4; c++) q[c] = tq[c];
            }
        }
        for (int c = 0; c < 4; c++) exact[c] = (q[c] << 1) | bestP;
        return bestP;
    }
}

/// <summary>Mip chains for the replacement images.</summary>
public static class MipChain
{
    private static readonly float[] ToLinear = Enumerable.Range(0, 256).Select(i =>
    {
        double c = i / 255.0;
        return (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
    }).ToArray();

    private static byte FromLinear(float v)
    {
        double c = Math.Clamp(v, 0f, 1f);
        c = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Clamp((int)Math.Round(c * 255), 0, 255);
    }

    /// <summary>
    /// Halves an RGBA image with a 2x2 box filter. Colour is weighted by alpha (a transparent pixel's colour doesn't tint
    /// its visible neighbours) and averaged in linear light for sRGB; alpha is averaged plainly. Where all four pixels are
    /// transparent the colour is their plain average.
    /// </summary>
    public static (byte[] Rgba, int W, int H) Halve(byte[] src, int w, int h, bool srgb)
    {
        int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
        var dst = new byte[nw * nh * 4];
        Parallel.For(0, nh, y =>
        {
            Span<int> o = stackalloc int[4];
            for (int x = 0; x < nw; x++)
            {
                int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1), y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
                o[0] = (y0 * w + x0) * 4; o[1] = (y0 * w + x1) * 4; o[2] = (y1 * w + x0) * 4; o[3] = (y1 * w + x1) * 4;
                int asum = src[o[0] + 3] + src[o[1] + 3] + src[o[2] + 3] + src[o[3] + 3];
                for (int c = 0; c < 3; c++)
                {
                    float sum = 0, wsum = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        float wt = asum > 0 ? src[o[k] + 3] : 1;
                        sum += (srgb ? ToLinear[src[o[k] + c]] : src[o[k] + c] / 255f) * wt;
                        wsum += wt;
                    }
                    float v = sum / wsum;
                    dst[(y * nw + x) * 4 + c] = srgb ? FromLinear(v) : (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);
                }
                dst[(y * nw + x) * 4 + 3] = (byte)((asum + 2) / 4);
            }
        });
        return (dst, nw, nh);
    }

    /// <summary>
    /// Gives every fully transparent pixel the colour of the nearest visible one (alpha stays 0), so block compression
    /// and filtering never mix a transparent area's colour (often black) into the visible edge. Returns the input when
    /// nothing is transparent.
    /// </summary>
    public static byte[] BleedTransparent(byte[] src, int w, int h)
    {
        int n = w * h;
        bool any = false, anyVisible = false;
        for (int i = 0; i < n; i++) { if (src[i * 4 + 3] == 0) any = true; else anyVisible = true; }
        if (!any || !anyVisible) return src;
        var dst = (byte[])src.Clone();
        var done = new bool[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (src[i * 4 + 3] != 0) { done[i] = true; queue.Enqueue(i); }
        // breadth-first from the visible pixels: each transparent pixel copies the colour of the first neighbour reaching it
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % w, y = i / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int j = ny * w + nx;
                if (done[j]) continue;
                done[j] = true;
                dst[j * 4] = dst[i * 4]; dst[j * 4 + 1] = dst[i * 4 + 1]; dst[j * 4 + 2] = dst[i * 4 + 2];
                queue.Enqueue(j);
            }
        }
        return dst;
    }

    /// <summary>Bilinear (area when shrinking) resize of RGBA8 to the given size.</summary>
    public static byte[] Resize(byte[] src, int w, int h, int nw, int nh, bool srgb)
    {
        if (w == nw && h == nh) return src;
        // shrink by exact halves first (box filter), then bilinear for the remainder
        while (w >= nw * 2 && h >= nh * 2)
            (src, w, h) = Halve(src, w, h, srgb);
        if (w == nw && h == nh) return src;
        var dst = new byte[nw * nh * 4];
        Parallel.For(0, nh, y =>
        {
            double fy = Math.Max(0, (y + 0.5) * h / nh - 0.5);
            int y0 = Math.Min(h - 1, (int)fy), y1 = Math.Min(h - 1, y0 + 1);
            double ty = fy - y0;
            for (int x = 0; x < nw; x++)
            {
                double fx = Math.Max(0, (x + 0.5) * w / nw - 0.5);
                int x0 = Math.Min(w - 1, (int)fx), x1 = Math.Min(w - 1, x0 + 1);
                double tx = fx - x0;
                for (int c = 0; c < 4; c++)
                {
                    double a = src[(y0 * w + x0) * 4 + c], b = src[(y0 * w + x1) * 4 + c], d = src[(y1 * w + x0) * 4 + c], e = src[(y1 * w + x1) * 4 + c];
                    dst[(y * nw + x) * 4 + c] = (byte)Math.Clamp((int)Math.Round((a * (1 - tx) + b * tx) * (1 - ty) + (d * (1 - tx) + e * tx) * ty), 0, 255);
                }
            }
        });
        return dst;
    }
}
