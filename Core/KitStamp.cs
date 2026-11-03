using System.Text;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// One hotspot of a kit's stamp: where the game puts a branding logo (or a name/number) on a garment's texture.
/// Tx/Ty is the centre in UV with V up, Sx/Sy the size as a fraction of the texture, R the rotation in degrees.
/// </summary>
public sealed record StampHotspot(int Id, string Type, string Name, bool Enabled, double R, double Sx, double Sy, double Tx, double Ty)
{
    /// <summary>Font hotspots: the text's scale and offset inside the box (fsx/fsy, ftx/fty), arch (fcurve) and slant (fshear).</summary>
    public double Fsx { get; init; } = 1;
    public double Fsy { get; init; } = 1;
    public double Ftx { get; init; }
    public double Fty { get; init; }
    public double Fcurve { get; init; }
    public double Fshear { get; init; }
}

/// <summary>
/// A kit's stamp (stamp_jersey_…, stamp_pant_…): a RawFileDataAsset holding a &lt;hotspots&gt; XML. The game draws the
/// garment's base texture (…_base_color) and stamps each enabled "brand" hotspot's logo (layer N of the branding array)
/// onto it; the kit's …_color texture is the same thing pre-baked.
/// </summary>
public static class KitStamp
{
    /// <summary>The hotspots of one garment (jersey, pant) in a stamp asset's bytes; empty if it has none.</summary>
    public static List<StampHotspot> Parse(byte[] ebx, string garment)
    {
        var list = new List<StampHotspot>();
        string text = Encoding.Latin1.GetString(ebx);
        var block = Block(text, garment);
        if (!block.Success) return list;
        foreach (Match m in Regex.Matches(block.Groups[1].Value, @"<hotspot\s([^>]*)/>"))
        {
            var a = Regex.Matches(m.Groups[1].Value, @"(\w+)=""([^""]*)""").GroupBy(x => x.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
            double D(string key) => a.TryGetValue(key, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            list.Add(new StampHotspot((int)D("id"), a.GetValueOrDefault("type", ""), a.GetValueOrDefault("name", ""), D("enable") != 0,
                D("r"), D("sx"), D("sy"), D("tx"), D("ty"))
            {
                Fsx = a.ContainsKey("fsx") ? D("fsx") : 1, Fsy = a.ContainsKey("fsy") ? D("fsy") : 1,
                Ftx = D("ftx"), Fty = D("fty"), Fcurve = D("fcurve"), Fshear = D("fshear"),
            });
        }
        return list;
    }

    /// <summary>
    /// The stamp asset's bytes with one garment's hotspots set to <paramref name="spots"/> (matched by id): enable, r, sx,
    /// sy, tx, ty and the font values. The XML is rewritten in place at the same length, so the asset keeps its size:
    /// values that change keep 4 decimals, and the indentation in front of the hotspots takes up any difference.
    /// </summary>
    public static byte[] Write(byte[] ebx, string garment, IEnumerable<StampHotspot> spots)
    {
        var byId = spots.ToDictionary(s => s.Id);
        string text = Encoding.Latin1.GetString(ebx);
        var block = Block(text, garment);
        if (!block.Success) throw new InvalidDataException($"The stamp has no <{garment}> hotspots.");
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string body = Regex.Replace(block.Groups[1].Value, @"<hotspot\s([^>]*)/>", m =>
        {
            string attrs = m.Groups[1].Value;
            var id = Regex.Match(attrs, @"\bid=""(\d+)""");
            if (!id.Success || !byId.TryGetValue(int.Parse(id.Groups[1].Value), out var s)) return m.Value;
            string Set(string a, string key, double value)
            {
                return Regex.Replace(a, $@"\b{key}=""([^""]*)""", v =>
                {
                    bool same = double.TryParse(v.Groups[1].Value, System.Globalization.NumberStyles.Float, ci, out double old) && Math.Abs(old - value) < 0.00005;
                    if (same) return v.Value;
                    string f = key == "enable" ? (value != 0 ? "1.0" : "0.0") : value.ToString("0.0000", ci);
                    return $@"{key}=""{f}""";
                });
            }
            attrs = Set(attrs, "enable", s.Enabled ? 1 : 0);
            attrs = Set(attrs, "r", ((s.R % 360) + 360) % 360);
            attrs = Set(attrs, "sx", s.Sx);
            attrs = Set(attrs, "sy", s.Sy);
            attrs = Set(attrs, "tx", s.Tx);
            attrs = Set(attrs, "ty", s.Ty);
            if (s.Type == "font")
            {
                attrs = Set(attrs, "fsx", s.Fsx);
                attrs = Set(attrs, "fsy", s.Fsy);
                attrs = Set(attrs, "ftx", s.Ftx);
                attrs = Set(attrs, "fty", s.Fty);
                attrs = Set(attrs, "fcurve", s.Fcurve);
                attrs = Set(attrs, "fshear", s.Fshear);
            }
            return "<hotspot " + attrs + "/>";
        });
        int diff = body.Length - block.Groups[1].Length;
        // even out the length in the runs of spaces that indent the hotspots (at least one stays)
        var sb = new StringBuilder(body);
        while (diff != 0)
        {
            bool changed = false;
            for (int i = sb.Length - 1; i > 0 && diff != 0; i--)
            {
                if (sb[i] != '<' || sb[i - 1] != ' ') continue;
                if (diff > 0 && i >= 2 && sb[i - 2] == ' ') { sb.Remove(i - 1, 1); diff--; changed = true; }
                else if (diff < 0) { sb.Insert(i, ' '); diff++; changed = true; }
            }
            if (!changed) throw new InvalidDataException("The edited hotspots don't fit in the stamp's space.");
        }
        string result = text[..block.Groups[1].Index] + sb + text[(block.Groups[1].Index + block.Groups[1].Length)..];
        var bytes = Encoding.Latin1.GetBytes(result);
        if (bytes.Length != ebx.Length) throw new InvalidDataException("The stamp changed size.");
        return bytes;
    }

    /// <summary>
    /// The brand hotspots with the branding layer each one draws: its name when it's a number, else its place among
    /// the brand hotspots ("sponsor" is layer 1 on NHL kits).
    /// </summary>
    public static List<(StampHotspot Spot, int Layer)> Brands(IEnumerable<StampHotspot> spots) =>
        spots.Where(s => s.Type == "brand").Select((s, i) => (s, int.TryParse(s.Name.StartsWith("branding") ? s.Name[8..] : s.Name, out int n) ? n : i)).ToList();

    /// <summary>
    /// A garment's &lt;hotspots&gt; block. The pant stamps keep theirs in a &lt;jersey&gt; block too (one hotspot named
    /// "branding0"), so a stamp with a single block gives that one for any garment.
    /// </summary>
    private static Match Block(string text, string garment)
    {
        var block = Regex.Match(text, $@"<{garment}>(.*?)</{garment}>", RegexOptions.Singleline);
        if (block.Success) return block;
        var tags = Regex.Matches(text, @"<(\w+)>\s*<hotspot\s").Select(m => m.Groups[1].Value).Distinct().ToList();
        return tags.Count == 1 ? Regex.Match(text, $@"<{tags[0]}>(.*?)</{tags[0]}>", RegexOptions.Singleline) : block;
    }

    /// <summary>
    /// Stamps logos onto a copy of the base (RGBA, its alpha is a material mask and is ignored; the result is opaque).
    /// A logo is turned R+180 degrees, centred on (Tx, 1-Ty) and sized Sx×Sy of the texture: checked against the game's
    /// pre-baked jerseys.
    /// </summary>
    public static byte[] Compose(byte[] baseRgba, int width, int height, IEnumerable<(StampHotspot Spot, byte[] Rgba, int Width, int Height)> logos)
    {
        var dst = (byte[])baseRgba.Clone();
        for (int i = 3; i < dst.Length; i += 4) dst[i] = 255;
        foreach (var (s, src, sw, sh) in logos)
        {
            if (!s.Enabled || src == null || s.Sx <= 0 || s.Sy <= 0) continue;
            double lw = s.Sx * width, lh = s.Sy * height;
            double cx = s.Tx * width, cy = (1 - s.Ty) * height;
            double a = (s.R + 180) * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
            double half = Math.Sqrt(lw * lw + lh * lh) / 2 + 1;
            int x0 = Math.Max(0, (int)(cx - half)), x1 = Math.Min(width - 1, (int)(cx + half));
            int y0 = Math.Max(0, (int)(cy - half)), y1 = Math.Min(height - 1, (int)(cy + half));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                // the pixel back into the logo: undo the (visually counter-clockwise) turn, then scale to 0..1
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                double u = (dx * cos - dy * sin) / lw + 0.5, v = (dx * sin + dy * cos) / lh + 0.5;
                if (u < 0 || u >= 1 || v < 0 || v >= 1) continue;
                Sample(src, sw, sh, u * sw - 0.5, v * sh - 0.5, out double r, out double g, out double b, out double al);
                if (al <= 0) continue;
                int o = (y * width + x) * 4;
                dst[o] = (byte)Math.Round(r * al + dst[o] * (1 - al));
                dst[o + 1] = (byte)Math.Round(g * al + dst[o + 1] * (1 - al));
                dst[o + 2] = (byte)Math.Round(b * al + dst[o + 2] * (1 - al));
            }
        }
        return dst;
    }

    /// <summary>
    /// Draws text for a font hotspot (name, number_back, number_arm_*, number_front, captain) from the kit's font texture,
    /// as an image the size of the hotspot's box on a <paramref name="width"/>×<paramref name="height"/> garment texture,
    /// ready for <see cref="Compose"/>. The text is fitted into the box keeping its shape, scaled by fsx/fsy. Null when
    /// the hotspot has nothing to draw.
    /// </summary>
    public static (byte[] Rgba, int Width, int Height)? Text(StampHotspot s, string text, byte[] font, int fontSize, int width, int height)
    {
        var glyphs = KitFont.Glyphs(s.Name, text);
        if (glyphs.Count == 0 || s.Sx <= 0 || s.Sy <= 0) return null;
        int bw = Math.Max(1, (int)Math.Ceiling(s.Sx * width)), bh = Math.Max(1, (int)Math.Ceiling(s.Sy * height));
        double k = fontSize / 2048.0;
        double tw = glyphs.Sum(g => g.W) * k, th = glyphs.Max(g => g.H) * k;
        double scale = Math.Min(bw / tw, bh / th);
        double sx = scale * (s.Fsx > 0 ? s.Fsx : 1), sy = scale * (s.Fsy > 0 ? s.Fsy : 1);
        double left = (bw - tw * sx) / 2 + s.Ftx * bw, top = (bh - th * sy) / 2 + s.Fty * bh;
        var img = new byte[bw * bh * 4];
        double x = left;
        foreach (var g in glyphs)
        {
            double gw = g.W * k * sx, gh = g.H * k * sy, gy = top + (th * sy - gh) / 2;
            int x0 = Math.Max(0, (int)x), x1 = Math.Min(bw - 1, (int)Math.Ceiling(x + gw));
            int y0 = Math.Max(0, (int)gy), y1 = Math.Min(bh - 1, (int)Math.Ceiling(gy + gh));
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                double u = (px + 0.5 - x) / gw, v = (py + 0.5 - gy) / gh;
                if (u < 0 || u >= 1 || v < 0 || v >= 1) continue;
                Sample(font, fontSize, fontSize, (g.X + u * g.W) * k - 0.5, (g.Y + v * g.H) * k - 0.5, out double r, out double gg, out double b, out double al);
                int o = (py * bw + px) * 4;
                if (al * 255 <= img[o + 3]) continue;
                img[o] = (byte)r; img[o + 1] = (byte)gg; img[o + 2] = (byte)b; img[o + 3] = (byte)Math.Round(al * 255);
            }
            x += gw;
        }
        return (img, bw, bh);
    }

    /// <summary>Bilinear sample (premultiplied while filtering, so transparent texels don't bleed their colour).</summary>
    private static void Sample(byte[] p, int w, int h, double fx, double fy, out double r, out double g, out double b, out double a)
    {
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
        double tx = fx - x0, ty = fy - y0;
        r = g = b = a = 0;
        for (int j = 0; j < 2; j++)
        for (int i = 0; i < 2; i++)
        {
            double wgt = (i == 0 ? 1 - tx : tx) * (j == 0 ? 1 - ty : ty);
            if (wgt == 0) continue;
            int x = Math.Clamp(x0 + i, 0, w - 1), y = Math.Clamp(y0 + j, 0, h - 1), o = (y * w + x) * 4;
            double al = p[o + 3] / 255.0 * wgt;
            r += p[o] * al; g += p[o + 1] * al; b += p[o + 2] * al; a += al;
        }
        if (a > 0) { r /= a; g /= a; b /= a; }
    }
}

/// <summary>
/// Where the characters sit in a kit's font texture (font_…_color, 2048×2048; every kit uses the same layout):
/// letters in two rows of 128-pixel cells (64 for I, space, apostrophe and period; _ marks an empty cell), back numbers in 320×512 cells,
/// C and A for the captain's letter, and two rows of 192-pixel digit cells for the arms and front.
/// </summary>
public static class KitFont
{
    public readonly record struct Glyph(int X, int Y, int W, int H);

    private static readonly Dictionary<char, Glyph> Letters = BuildLetters();

    private static Dictionary<char, Glyph> BuildLetters()
    {
        var map = new Dictionary<char, Glyph>();
        void Row(int y, params (char C, int W)[] cells)
        {
            int x = 0;
            foreach (var (c, w) in cells)
            {
                if (c != '_') map[c] = new Glyph(x, y, w, 192);
                x += w;
            }
        }
        Row(0, ('A', 128), ('B', 128), ('C', 128), ('D', 128), ('E', 128), ('F', 128), ('G', 128), ('H', 128), ('I', 64), (' ', 64),
            ('J', 128), ('K', 128), ('L', 128), ('\'', 64), ('M', 128), ('.', 64), ('N', 128), ('O', 128));
        Row(192, ('P', 128), ('Q', 128), ('R', 128), ('S', 128), ('T', 128), ('U', 128), ('V', 128), ('_', 64), ('W', 128), ('_', 64),
            ('X', 128), ('Y', 128), ('Z', 128), ('_', 64), ('-', 64));
        return map;
    }

    /// <summary>The glyphs a font hotspot draws for this text (letters for the name, the right digit set for numbers).</summary>
    public static List<Glyph> Glyphs(string hotspot, string text)
    {
        var list = new List<Glyph>();
        if (string.IsNullOrEmpty(text)) return list;
        if (hotspot == "name")
        {
            foreach (char c in text.ToUpperInvariant())
                if (Letters.TryGetValue(c == '’' ? '\'' : c, out var g)) list.Add(g);
        }
        else if (hotspot == "number_back")
        {
            foreach (char c in text)
                if (c is >= '0' and <= '9') { int d = c - '0'; list.Add(new Glyph(d % 5 * 320, d < 5 ? 448 : 960, 320, 512)); }
        }
        else if (hotspot is "number_front" or "number_arm_left" or "number_arm_right" or "helmet")
        {
            foreach (char c in text)
                if (c is >= '0' and <= '9') list.Add(new Glyph((c - '0') * 192, 1472, 192, 320));
        }
        else if (hotspot == "captain")
        {
            char c = char.ToUpperInvariant(text[0]);
            if (c == 'C') list.Add(new Glyph(1664, 320, 384, 448));
            else if (c == 'A') list.Add(new Glyph(1664, 768, 384, 448));
        }
        return list;
    }
}
