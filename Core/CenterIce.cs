namespace NHL27Kit.Core;

/// <summary>Where a top-down rink picture puts the rink: its centre (the red line's middle) and its scale.</summary>
public readonly record struct RinkGeometry(double Cx, double Cy, double PixelsPerFoot, bool Measured);

/// <summary>An ad slot on the ice: where texel (u, v) of its hotspot lies, Origin + u·U + v·V (x, z in metres).</summary>
public readonly record struct IceAdSlot(string Material, double Ox, double Oz, double Ux, double Uz, double Vx, double Vz);

/// <summary>
/// How a centre-ice conversion looks: colour saturation (1 = as drawn), how much of the ice is cut away, and how much of
/// the site's frosted-ice tint is taken off the paint (1 = all of it, 0 = the picture's colours as they are).
/// </summary>
public sealed record CenterIceLook(double Saturation = 1, double CutOut = 0.06, double IceTint = 1);

/// <summary>
/// Converts a top-down rink layout (TheFaceoff.net's 5000×2236 pictures: white ice, 200×85 ft) into the game's centre ice:
/// - ice_centerlogo_N_color: 1024², the logo on transparent ice. The game's logos hold the 15 ft centre circle at a
///   380.8 px radius, so the square spans 40.3 ft around the centre faceoff dot.
/// - ice_centerline_N_color: 64×4096, the red line's 85 ft along the strip, its 1 ft across x 4..59 (the rest transparent),
///   centre dot in the middle.
/// The scale comes from the blue lines (25 ft each side of centre); when they aren't found, the site's template is used.
/// White ice turns transparent (colour-to-alpha against white: paint over white ice comes out as itself).
/// The site draws its paint under a frosted-ice veil: black paint is about 90, not 0, and colours are washed out. That is
/// a levels shift, so it is undone with levels (TintBlack..TintWhite stretched to full range), which also clips the ice's
/// pale scratches to white; a small edge-keeping blur takes off the grain the stretch brings up. The paint then goes in
/// the range the game's own logos use (PaintLow..PaintHigh: its Boston has black at 35,31,32 and gold at 224,147,0).
/// </summary>
public static class CenterIce
{
    public const int LogoSize = 1024, LineWidth = 64, LineLength = 4096;
    public const double LogoSpanFeet = 15 * 1024 / 380.8, RinkWidthFeet = 85;
    private const int LineLeft = 4, LineRight = 60;
    private const double TintBlack = 85, TintWhite = 238, PaintLow = 19, PaintHigh = 228;

    /// <summary>Finds the rink in a top-down picture (RGBA).</summary>
    public static RinkGeometry Measure(byte[] rgba, int w, int h)
    {
        // blue lines: columns where strong blue covers most of the height
        var count = new int[w];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int o = (y * w + x) * 4;
            if (rgba[o + 2] > 150 && rgba[o] < 110 && rgba[o + 1] < 150) count[x]++;
        }
        double left = 0, right = 0;
        int nl = 0, nr = 0;
        for (int x = 0; x < w; x++)
        {
            if (count[x] <= h * 0.5) continue;
            if (x < w / 2) { left += x; nl++; } else { right += x; nr++; }
        }
        if (nl > 0 && nr > 0)
        {
            left /= nl; right /= nr;
            double ppf = (right - left) / 50.0;
            // the template draws the rink 5000/2236 wide, centred: the centre row is the middle of the picture
            if (ppf > w / 400.0 && ppf < w / 100.0) return new RinkGeometry((left + right) / 2, h / 2.0, ppf, true);
        }
        // TheFaceoff.net's template: blue lines at 1882 and 3118 of 5000
        double k = w / 5000.0;
        return new RinkGeometry(2500 * k, h / 2.0, 24.72 * k, false);
    }

    /// <summary>The centre logo: RGBA, <paramref name="n"/>² (the game's are LogoSize²), over the same span of ice.</summary>
    public static byte[] Logo(byte[] rgba, int w, int h, RinkGeometry g, CenterIceLook look, int n = LogoSize)
    {
        var v = new float[n * n * 3];
        double span = LogoSpanFeet * g.PixelsPerFoot;
        Parallel.For(0, n, y =>
        {
            for (int x = 0; x < n; x++)
                SampleClean(rgba, w, h, g.Cx + ((x + 0.5) / n - 0.5) * span - 0.5, g.Cy + ((y + 0.5) / n - 0.5) * span - 0.5, look, v, (y * n + x) * 3);
        });
        return Unwhite(Smooth(v, n, n, look), n, n, look);
    }

    public const int AdWidth = 512, AdHeight = 256;

    /// <summary>
    /// The NHL ice's four centre-ice ad slots (rink_ice_nhl_mesh's ice_centeradone..four_mat, the game's CenterAd1..4), as
    /// the game maps a hotspot onto them with UV3 (usage 0x23; the decals' UV, not UV2): texel (u, v) lies at
    /// Origin + u·U + v·V, in metres (x across the ice, +x the broadcast side; z along it). Each is 15.9 ft along the ice
    /// by 8 ft across, 4 to 20 ft from the red line and 24 to 32 ft from the middle. All four face the same way: u along
    /// +z, v towards -x.
    /// </summary>
    public static readonly IceAdSlot[] CenterAds =
    {
        new("ice_centeradone_mat", 9.733, -6.083, 0, 4.85, -2.425, 0),
        new("ice_centeradtwo_mat", 9.733, 1.233, 0, 4.85, -2.425, 0),
        new("ice_centeradthree_mat", -7.308, -6.083, 0, 4.85, -2.425, 0),
        new("ice_centeradfour_mat", -7.308, 1.233, 0, 4.85, -2.425, 0),
    };

    /// <summary>
    /// One centre-ice ad cut from a top-down layout for <paramref name="slot"/>: RGBA, AdWidth×AdHeight, white ice made
    /// transparent as for the logo. The layout lies on the ice as the logo cut from it does: its right is +z and its down
    /// is −x (the logo's UV3 runs u along +z, v towards −x).
    /// </summary>
    public static byte[] Hotspot(byte[] rgba, int w, int h, RinkGeometry g, CenterIceLook look, IceAdSlot slot)
    {
        var values = new float[AdWidth * AdHeight * 3];
        double k = g.PixelsPerFoot / 0.3048;   // pixels per metre
        Parallel.For(0, AdHeight, y =>
        {
            double v = (y + 0.5) / AdHeight;
            for (int x = 0; x < AdWidth; x++)
            {
                double u = (x + 0.5) / AdWidth;
                double wx = slot.Ox + u * slot.Ux + v * slot.Vx, wz = slot.Oz + u * slot.Uz + v * slot.Vz;
                SampleClean(rgba, w, h, g.Cx + wz * k - 0.5, g.Cy - wx * k - 0.5, look, values, (y * AdWidth + x) * 3);
            }
        });
        return Unwhite(Smooth(values, AdWidth, AdHeight, look), AdWidth, AdHeight, look);
    }

    /// <summary>Samples the layout with the ice's tint taken off (values 0..255, ice near 255).</summary>
    private static void SampleClean(byte[] rgba, int w, int h, double sx, double sy, CenterIceLook look, float[] v, int o)
    {
        Sample(rgba, w, h, sx, sy, out double r, out double g, out double b);
        v[o] = (float)Untint(r, look); v[o + 1] = (float)Untint(g, look); v[o + 2] = (float)Untint(b, look);
    }

    private static double Untint(double c, CenterIceLook look)
    {
        double black = TintBlack * look.IceTint, white = 255 - (255 - TintWhite) * look.IceTint;
        return Math.Clamp((c - black) / (white - black) * 255, 0, 255);
    }

    /// <summary>A value with the tint off, in the range the game's paint uses.</summary>
    private static double Paint(double c, CenterIceLook look)
    {
        double low = PaintLow * look.IceTint, high = 255 - (255 - PaintHigh) * look.IceTint;
        return low + c * (high - low) / 255;
    }

    /// <summary>An edge-keeping blur: takes off the grain that stretching the colours brings up, keeps the paint's edges.</summary>
    private static float[] Smooth(float[] v, int w, int h, CenterIceLook look)
    {
        if (look.IceTint <= 0) return v;
        const int radius = 3;
        double sigma = 22 * look.IceTint, near = 1 / (2 * sigma * sigma);
        var space = new float[(2 * radius + 1) * (2 * radius + 1)];
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                space[(dy + radius) * (2 * radius + 1) + dx + radius] = (float)Math.Exp(-(dx * dx + dy * dy) / (2 * 1.8 * 1.8));
        var dst = new float[v.Length];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 3;
                float r0 = v[o], g0 = v[o + 1], b0 = v[o + 2], sr = 0, sg = 0, sb = 0, sum = 0;
                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(h - 1, y + radius); yy++)
                    for (int xx = Math.Max(0, x - radius); xx <= Math.Min(w - 1, x + radius); xx++)
                    {
                        int p = (yy * w + xx) * 3;
                        float dr = v[p] - r0, dg = v[p + 1] - g0, db = v[p + 2] - b0;
                        float weight = space[(yy - y + radius) * (2 * radius + 1) + xx - x + radius] * MathF.Exp(-(float)near * (dr * dr + dg * dg + db * db));
                        sr += v[p] * weight; sg += v[p + 1] * weight; sb += v[p + 2] * weight; sum += weight;
                    }
                dst[o] = sr / sum; dst[o + 1] = sg / sum; dst[o + 2] = sb / sum;
            }
        });
        return dst;
    }

    /// <summary>Clean values to RGBA with white ice turned transparent (colour-to-alpha against white).</summary>
    private static byte[] Unwhite(float[] v, int w, int h, CenterIceLook look)
    {
        var dst = new byte[w * h * 4];
        double ramp = 0.30 - 0.10 * look.IceTint;
        Parallel.For(0, w * h, i =>
        {
            double r = v[i * 3], g = v[i * 3 + 1], b = v[i * 3 + 2];
            double d = Math.Max(255 - r, Math.Max(255 - g, 255 - b)) / 255.0;
            double a = Math.Clamp((d - look.CutOut) / ramp, 0, 1);
            if (a <= 0) return;
            r = Paint(Math.Max(0, 255 - (255 - r) / a), look);
            g = Paint(Math.Max(0, 255 - (255 - g) / a), look);
            b = Paint(Math.Max(0, 255 - (255 - b) / a), look);
            Saturate(ref r, ref g, ref b, look.Saturation);
            int o = i * 4;
            dst[o] = Byte(r); dst[o + 1] = Byte(g); dst[o + 2] = Byte(b); dst[o + 3] = Byte(a * 255);
        });
        return dst;
    }

    /// <summary>The centre line: RGBA, LineWidth×LineLength (opaque across the line, as the game's are).</summary>
    public static byte[] Line(byte[] rgba, int w, int h, RinkGeometry g, CenterIceLook look)
    {
        var dst = new byte[LineWidth * LineLength * 4];
        Parallel.For(0, LineLength, y =>
        {
            double sy = g.Cy + ((y + 0.5) / LineLength - 0.5) * RinkWidthFeet * g.PixelsPerFoot - 0.5;
            for (int x = LineLeft; x < LineRight; x++)
            {
                double sx = g.Cx + ((x + 0.5 - LineLeft) / (LineRight - LineLeft) - 0.5) * g.PixelsPerFoot - 0.5;
                Sample(rgba, w, h, sx, sy, out double r, out double gg, out double b);
                r = Paint(Untint(r, look), look); gg = Paint(Untint(gg, look), look); b = Paint(Untint(b, look), look);
                Saturate(ref r, ref gg, ref b, look.Saturation);
                int o = (y * LineWidth + x) * 4;
                dst[o] = Byte(r); dst[o + 1] = Byte(gg); dst[o + 2] = Byte(b); dst[o + 3] = 255;
            }
        });
        return dst;
    }

    /// <summary>
    /// A picture of centre ice as the game puts it together (the logo on ice, the line strip over it), size², for previews.
    /// </summary>
    public static byte[] Preview(byte[] logo, byte[] line, int size, int ice = 0xE9EEF2)
    {
        var dst = new byte[size * size * 4];
        double linePx = LineLength * LogoSpanFeet / RinkWidthFeet;   // the strip's pixels across the logo's span
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                double r = ice >> 16 & 255, g = ice >> 8 & 255, b = ice & 255;
                Over(logo, LogoSize, LogoSize, (x + 0.5) / size * LogoSize - 0.5, (y + 0.5) / size * LogoSize - 0.5, ref r, ref g, ref b);
                // the strip is 1 ft (plus margins) wide: LineWidth/56 ft, centred
                double feet = ((x + 0.5) / size - 0.5) * LogoSpanFeet, across = feet * (LineRight - LineLeft) + LineWidth / 2.0;
                if (across >= 0 && across < LineWidth)
                    Over(line, LineWidth, LineLength, across - 0.5, LineLength / 2.0 + ((y + 0.5) / size - 0.5) * linePx - 0.5, ref r, ref g, ref b);
                int o = (y * size + x) * 4;
                dst[o] = Byte(r); dst[o + 1] = Byte(g); dst[o + 2] = Byte(b); dst[o + 3] = 255;
            }
        });
        return dst;
    }

    private static void Over(byte[] img, int w, int h, double x, double y, ref double r, ref double g, ref double b)
    {
        int xi = Math.Clamp((int)Math.Round(x), 0, w - 1), yi = Math.Clamp((int)Math.Round(y), 0, h - 1), o = (yi * w + xi) * 4;
        double a = img[o + 3] / 255.0;
        if (a <= 0) return;
        r = img[o] * a + r * (1 - a); g = img[o + 1] * a + g * (1 - a); b = img[o + 2] * a + b * (1 - a);
    }

    private static void Saturate(ref double r, ref double g, ref double b, double s)
    {
        if (Math.Abs(s - 1) < 1e-6) return;
        double l = 0.299 * r + 0.587 * g + 0.114 * b;
        r = l + (r - l) * s; g = l + (g - l) * s; b = l + (b - l) * s;
    }

    private static byte Byte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    private static void Sample(byte[] p, int w, int h, double x, double y, out double r, out double g, out double b)
    {
        x = Math.Clamp(x, 0, w - 1); y = Math.Clamp(y, 0, h - 1);
        int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
        double fx = x - x0, fy = y - y0;
        int a = (y0 * w + x0) * 4, bb = (y0 * w + x1) * 4, c = (y1 * w + x0) * 4, d = (y1 * w + x1) * 4;
        double L(int k) => (p[a + k] * (1 - fx) + p[bb + k] * fx) * (1 - fy) + (p[c + k] * (1 - fx) + p[d + k] * fx) * fy;
        r = L(0); g = L(1); b = L(2);
    }
}
