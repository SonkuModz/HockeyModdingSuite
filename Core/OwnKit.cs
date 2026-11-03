using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// A uniform of your own, made from the game's create-a-team parts (league/custom): jersey designs, lettering fonts,
/// pants and socks. Each part is a dark detail layer (…_color; socks have none) and recolor masks whose R, G and B are
/// colour zones, like the jerseys (<see cref="JerseyStudio"/>). Pants and socks use the common meshes every kit uses and
/// the fonts the layout every kit's font uses, so the parts fit any kit folder.
/// </summary>
public static class OwnKit
{
    private static readonly Regex MaskName = new(@"league/custom/(font|pant|sock)/(\w+)/custom_(\d+)/\1_\2_custom_\3_recolor(\d)$", RegexOptions.IgnoreCase);

    /// <summary>Every create-a-team font, pant or sock design in the game ("font", "pant", "sock"), by number.</summary>
    public static List<StudioTemplate> Parts(AssetIndex index, string garment)
    {
        var found = new Dictionary<(string, int), SortedDictionary<int, string>>();
        foreach (var (name, e) in index.Ebx)
        {
            if (e.Type != "TextureAsset" || !name.Contains($"/custom/{garment}/", StringComparison.OrdinalIgnoreCase)) continue;
            var m = MaskName.Match(name);
            if (!m.Success) continue;
            var key = (m.Groups[2].Value.ToLowerInvariant(), int.Parse(m.Groups[3].Value));
            if (!found.TryGetValue(key, out var masks)) found[key] = masks = new();
            masks[int.Parse(m.Groups[4].Value)] = name;
        }
        return found.Select(f =>
            {
                string folder = f.Value.Values.First()[..f.Value.Values.First().LastIndexOf('/')];
                string color = $"{folder}/{garment}_{f.Key.Item1}_custom_{f.Key.Item2}_color";
                return new StudioTemplate(f.Key.Item1, f.Key.Item2, index.GetEbx(color) != null ? color : null, f.Value.Values.ToList(), garment);
            })
            .OrderBy(t => t.Number).ToList();
    }

    /// <summary>A part's normal or coeff map ("normal", "coeff"), null when it has none.</summary>
    public static string Map(AssetIndex index, StudioTemplate t, string kind)
    {
        string name = $"{t.Folder}/{t.Garment}_{t.Brand}_custom_{t.Number}_{kind}";
        return index.GetEbx(name) != null ? name : null;
    }

    /// <summary>
    /// A font's outline zones: those whose glyph texels mostly touch both the outside (the glyph's alpha) and another zone.
    /// A fill touches its outline only at its rim, and a fill with no outline (the name's letters) touches no other zone.
    /// </summary>
    public static HashSet<int> Outlines(StudioPattern font) => OutlineShares(font).Where(c => c.Value > 0.75).Select(c => c.Key).ToHashSet();

    /// <summary>Each zone's share of glyph texels that touch both the outside and another zone.</summary>
    public static Dictionary<int, double> OutlineShares(StudioPattern font)
    {
        int size = font.Size, n = size * size;
        bool Inside(int i) => font.Detail[i * 4 + 3] >= 64;
        var zoneOf = new sbyte[n];
        for (int i = 0; i < n; i++)
        {
            int best = -1, bestV = 127;
            foreach (var (zone, _) in font.Zones)
            {
                int v = font.Masks[zone / 3][i * 4 + zone % 3];
                if (v > bestV) { bestV = v; best = zone; }
            }
            zoneOf[i] = Inside(i) ? (sbyte)best : (sbyte)-1;
        }
        int r = Math.Max(3, size / 128);
        var count = new Dictionary<int, (long All, long Band)>();
        for (int y = 0; y < size; y += 2)
        for (int x = 0; x < size; x += 2)
        {
            int z = zoneOf[y * size + x];
            if (z < 0) continue;
            bool edge = false, other = false;
            for (int dy = -r; dy <= r && !(edge && other); dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                int j = yy * size + xx;
                if (!Inside(j)) edge = true;
                else if (zoneOf[j] >= 0 && zoneOf[j] != z) other = true;
            }
            var c = count.GetValueOrDefault(z);
            count[z] = (c.All + 1, c.Band + (edge && other ? 1 : 0));
        }
        return count.ToDictionary(c => c.Key, c => c.Value.Band / (double)c.Value.All);
    }

    /// <summary>
    /// Loads a font design. Most have no colour texture: their masks paint the glyphs and each mask has a background
    /// zone round its glyphs (those covering much of the sheet's border), so the glyph's alpha is the other zones'
    /// strongest channel. The background zones are left out of the zones.
    /// Their name letters (the top two rows) have no shape in the masks, only relief in the normal map, so the letters
    /// are font 0's (<see cref="LetterRows"/>), which has a colour texture.
    /// </summary>
    public static StudioPattern LoadFont(AssetIndex index, StudioTemplate t, int size)
    {
        var p = JerseyStudio.LoadPattern(index, t, size);
        if (t.Color != null) return p;
        var border = new Dictionary<int, int>();
        for (int k = 0; k < size; k++)
            foreach (int i in new[] { k, (size - 1) * size + k, k * size, k * size + size - 1 })
                foreach (var (zone, _) in p.Zones)
                    if (p.Masks[zone / 3][i * 4 + zone % 3] > 127) border[zone] = border.GetValueOrDefault(zone) + 1;
        // each mask has its own background channel round its rows of glyphs
        int most = border.Count > 0 ? border.Values.Max() : 0;
        var bg = border.Where(b => b.Value > most * 0.2).Select(b => b.Key).ToHashSet();
        var zones = p.Zones.Where(z => !bg.Contains(z.Zone)).ToList();
        var detail = new byte[size * size * 4];
        for (int i = 0; i < size * size; i++)
        {
            int a = 0;
            foreach (var (zone, _) in zones) a = Math.Max(a, p.Masks[zone / 3][i * 4 + zone % 3]);
            detail[i * 4 + 3] = (byte)a;
        }
        if (LetterFont(index, t, "color") is { } letters && Kits.Load(index, letters, size, 0) is { Rgba: not null } lt)
        {
            var l = JerseyStudio.Resize(lt.Rgba, lt.Width, lt.Height, size);
            int rows = LetterRows(size);
            for (int i = 0; i < rows * size; i++) detail[i * 4 + 3] = l[i * 4 + 3];
        }
        return new StudioPattern { Template = t, Size = size, Detail = detail, Masks = p.Masks, Zones = zones };
    }

    /// <summary>How many rows of a font sheet <paramref name="size"/> square the name letters take (two rows of 192 in 2048).</summary>
    public static int LetterRows(int size) => size * 384 / 2048;

    /// <summary>The map ("color", "normal", "coeff") of the font a mask-only font takes its name letters from; null for a font with its own.</summary>
    public static string LetterFont(AssetIndex index, StudioTemplate t, string kind)
    {
        if (t.Color != null) return null;
        string name = $"{t.Folder[..t.Folder.LastIndexOf('/')]}/custom_0/font_{t.Brand}_custom_0_{kind}";
        return index.GetEbx(name) != null ? name : null;
    }

    /// <summary>A font's normal or coeff map at its full size, with the letter rows of the font its letters come from.</summary>
    public static (byte[] Rgba, int Width, int Height)? FontMap(AssetIndex index, StudioTemplate t, string kind)
    {
        if (Map(index, t, kind) is not { } name || Kits.Load(index, name, 4096, 0) is not { Rgba: not null } m) return null;
        if (LetterFont(index, t, kind) is { } letters && Kits.Load(index, letters, m.Width, 0) is { Rgba: not null } lt)
        {
            var l = lt.Width == m.Width && lt.Height == m.Height ? lt.Rgba : MipChain.Resize(lt.Rgba, lt.Width, lt.Height, m.Width, m.Height, false);
            Array.Copy(l, m.Rgba, LetterRows(m.Height) * m.Width * 4);
        }
        return (m.Rgba, m.Width, m.Height);
    }

    /// <summary>A font painted: its fill zones in <paramref name="fill"/>, its outlines in <paramref name="outline"/> (the glyphs' alpha kept).</summary>
    public static byte[] PaintFont(StudioPattern font, HashSet<int> outlines, int fill, int outline)
    {
        var img = JerseyStudio.Paint(font.Detail, font.Size, font, font.Zones.ToDictionary(z => z.Zone, z => outlines.Contains(z.Zone) ? outline : fill));
        // glyph texels no zone paints (borrowed letters) take the fill
        for (int i = 0; i < font.Size * font.Size; i++)
        {
            if (img[i * 4 + 3] == 0 || font.Zones.Any(z => font.Masks[z.Zone / 3][i * 4 + z.Zone % 3] > 127)) continue;
            img[i * 4] = (byte)(fill >> 16); img[i * 4 + 1] = (byte)(fill >> 8); img[i * 4 + 2] = (byte)fill;
        }
        return img;
    }

    /// <summary>
    /// Pants or socks painted: the big zones (a fifth of the sheet or more: the pant legs, a sock's upper and lower leg)
    /// in the main colour, the rest (stripes, panels, trims) dealt the other colours in turn; <paramref name="shift"/>
    /// moves the colours along.
    /// </summary>
    public static byte[] PaintPart(StudioPattern part, IReadOnlyList<int> palette, int shift)
    {
        var colors = new Dictionary<int, int>();
        int n = palette.Count, other = 0;
        foreach (var (zone, share) in part.Zones)
            colors[zone] = share >= 0.2 || n == 1 ? palette[shift % n] : palette[(shift + 1 + other++ % (n - 1)) % n];
        var img = JerseyStudio.Paint(part.Detail, part.Size, part, colors);
        for (int i = 3; i < img.Length; i += 4) img[i] = 255;
        return img;
    }

    /// <summary>The front band of a painted font (the back numbers 0–4), for a picker.</summary>
    public static (byte[] Rgba, int Width, int Height) FontThumb(byte[] font, int size, int width = 120, int height = 40)
    {
        var img = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sx = Math.Min(size - 1, (int)((x + 0.5) / width * 1600 / 2048 * size));
            int sy = Math.Min(size - 1, (int)((448 + (y + 0.5) / height * 512) / 2048 * size));
            int o = (sy * size + sx) * 4, d = (y * width + x) * 4;
            double a = font[o + 3] / 255.0;
            for (int c = 0; c < 3; c++) img[d + c] = (byte)(font[o + c] * a + 0x2A * (1 - a));
            img[d + 3] = 255;
        }
        return (img, width, height);
    }

    /// <summary>The whole sheet of a painted part, small, for a picker.</summary>
    public static (byte[] Rgba, int Width, int Height) Thumb(byte[] rgba, int size, int n = 56)
    {
        var img = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int sx = (int)((x + 0.5) / n * size), sy = (int)((y + 0.5) / n * size);
            Array.Copy(rgba, (sy * size + sx) * 4, img, (y * n + x) * 4, 4);
            img[(y * n + x) * 4 + 3] = 255;
        }
        return (img, n, n);
    }

    /// <summary>
    /// The kit whose models and lettering spots a jersey of your own borrows: an NHL adidas home kit with a stamped
    /// jersey (the cut most kits use), else any stamped kit.
    /// </summary>
    public static Kit Carrier(IEnumerable<Kit> kits)
    {
        var stamped = kits.Where(k => k.IsStamped("jersey") && k.Slot("jersey_base_color") != null).ToList();
        return stamped.FirstOrDefault(k => k.League == "nhl" && k.Brand == "adidas" && k.Name == "home")
               ?? stamped.FirstOrDefault(k => k.Brand == "adidas") ?? stamped.FirstOrDefault();
    }

    /// <summary>
    /// The studio source for a jersey of your own: the carrier's fabric map under a flat <paramref name="body"/> colour,
    /// its lettering spots (its logos left out), and <paramref name="font"/> (a painted font, <paramref name="fontSize"/> square).
    /// </summary>
    public static StudioSource Source(AssetIndex index, Kit carrier, int size, IReadOnlyList<int> palette, byte[] font, int fontSize)
    {
        var src = JerseyStudio.LoadSource(index, carrier, size);
        var b = (byte[])src.Base.Clone();
        Fill(b, palette[0]);
        // the logo spots as an NHL kit has them: the team's (main, shoulders) get a stand-in crest, the league's and maker's
        // marks keep their images, the sponsor is off
        var spots = new List<StampHotspot>();
        var logos = new Dictionary<int, (byte[] Rgba, int Width, int Height)>();
        var brands = KitStamp.Brands(src.Spots).ToDictionary(x => x.Spot, x => x.Layer);
        foreach (var s in src.Spots)
        {
            if (s.Type == "font") { spots.Add(s); continue; }
            if (!brands.TryGetValue(s, out int layer)) continue;
            switch (Role(s, layer))
            {
                case LogoRole.Main or LogoRole.Shoulder: spots.Add(s with { Enabled = true }); logos[layer] = (Crest(palette), 512, 512); break;
                case LogoRole.League when src.Logos.TryGetValue(layer, out var l): spots.Add(s); logos[layer] = l; break;
                default: spots.Add(s with { Enabled = false }); break;
            }
        }
        return new StudioSource
        {
            Kit = carrier, Size = size, Base = b, StampEbx = src.StampEbx, Spots = spots,
            Logos = logos, Font = font, FontSize = fontSize, Shape = src.Shape, Own = true,
        };
    }

    public enum LogoRole { Main, Shoulder, League, Sponsor }

    /// <summary>
    /// What a jersey stamp's logo spot is for. NHL kits: "mainlogo" (or "0") on the chest, right/left shoulder (turned 90/270),
    /// the NHL shield on the collar, the maker's mark on the back and hem, and sometimes a sponsor.
    /// </summary>
    public static LogoRole Role(StampHotspot s, int layer)
    {
        string n = s.Name.ToLowerInvariant();
        if (n.Contains("sponsor")) return LogoRole.Sponsor;
        if (n.Contains("main") || layer == 0) return LogoRole.Main;
        if (n.Contains("shoulder") || Math.Abs(s.R - 90) < 1 || Math.Abs(s.R - 270) < 1) return LogoRole.Shoulder;
        return LogoRole.League;
    }

    /// <summary>The stand-in crest (512²) on your uniform until you give it a logo: a round badge with a star, in your colours.</summary>
    public static byte[] Crest(IReadOnlyList<int> palette)
    {
        const int n = 512;
        int main = palette[0], second = palette.Count > 1 ? palette[1] : 0xFFFFFF, third = palette.Count > 2 ? palette[2] : second;
        var star = Enumerable.Range(0, 10).Select(i =>
        {
            double a = -Math.PI / 2 + i * Math.PI / 5, r = i % 2 == 0 ? 0.62 : 0.25;
            return (X: r * Math.Cos(a), Y: r * Math.Sin(a));
        }).ToArray();
        bool InStar(double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = star.Length - 1; i < star.Length; j = i++)
                if ((star[i].Y > y) != (star[j].Y > y) && x < (star[j].X - star[i].X) * (y - star[i].Y) / (star[j].Y - star[i].Y) + star[i].X) inside = !inside;
            return inside;
        }
        var img = new byte[n * n * 4];
        Parallel.For(0, n, py =>
        {
            for (int px = 0; px < n; px++)
            {
                double r = 0, g = 0, bl = 0, a = 0;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        double x = (px + (sx + 0.5) / 3) / n * 2 - 1, y = (py + (sy + 0.5) / 3) / n * 2 - 1, d = Math.Sqrt(x * x + y * y);
                        if (d > 0.98) continue;
                        int c = d > 0.86 ? third : d > 0.76 ? second : InStar(x / 0.76, y / 0.76) ? second : main;
                        r += c >> 16 & 255; g += c >> 8 & 255; bl += c & 255; a += 1;
                    }
                if (a == 0) continue;
                int o = (py * n + px) * 4;
                img[o] = (byte)(r / a); img[o + 1] = (byte)(g / a); img[o + 2] = (byte)(bl / a); img[o + 3] = (byte)(a / 9 * 255);
            }
        });
        return img;
    }

    /// <summary>Fills an image's colour, keeping its alpha (the fabric map).</summary>
    public static void Fill(byte[] rgba, int color)
    {
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = (byte)(color >> 16); rgba[i + 1] = (byte)(color >> 8); rgba[i + 2] = (byte)color;
        }
    }

    /// <summary>Helmet zones (15) all in the main colour.</summary>
    public static int[] HelmetZones(IReadOnlyList<int> palette) => Enumerable.Repeat(palette[0], 15).ToArray();

    /// <summary>Glove zones (15): main, second, third colour in turn.</summary>
    public static int[] GloveZones(IReadOnlyList<int> palette) => Enumerable.Range(0, 15).Select(i => palette[i % palette.Count]).ToArray();
}
