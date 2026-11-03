using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// A create-a-team jersey design (league/custom/jersey/[brand]/custom_N): a dark detail layer (…_color) and up to five
/// recolor masks, each of whose R, G and B channels is one colour zone. Every NHL 27 skater jersey shares one UV layout,
/// so a design fits any kit's jersey.
/// </summary>
public sealed record StudioTemplate(string Brand, int Number, string Color, IReadOnlyList<string> Masks, string Garment = "jersey")
{
    public string Label => Garment == "jersey" ? $"{(Brand == "rbk" ? "Reebok" : char.ToUpperInvariant(Brand[0]) + Brand[1..])} design {Number}"
        : $"{(Garment == "font" ? "Lettering" : Garment == "pant" ? "Pants" : "Socks")} {Number}";
    /// <summary>The design's folder in league/custom.</summary>
    public string Folder => Masks[0][..Masks[0].LastIndexOf('/')];
}

/// <summary>A template's images at one size: zone z is channel z % 3 of mask z / 3.</summary>
public sealed class StudioPattern
{
    public StudioTemplate Template { get; init; }
    public int Size { get; init; }
    public byte[] Detail { get; init; }
    public List<byte[]> Masks { get; init; }
    /// <summary>The zones that are drawn somewhere, with their share of the sheet, largest first.</summary>
    public List<(int Zone, double Share)> Zones { get; init; }
}

/// <summary>What a kit's jersey is drawn from, read from the game when the studio opens.</summary>
public sealed class StudioSource
{
    public Kit Kit { get; init; }
    public int Size { get; init; }
    /// <summary>jersey_base_color as RGBA; its alpha is the fabric map (see <see cref="JerseyStudio.Region"/>).</summary>
    public byte[] Base { get; set; }
    public byte[] StampEbx { get; init; }
    public List<StampHotspot> Spots { get; init; }
    /// <summary>The logo of each branding layer the stamp uses, as the game has it.</summary>
    public Dictionary<int, (byte[] Rgba, int Width, int Height)> Logos { get; init; }
    public byte[] Font { get; set; }
    public int FontSize { get; set; }
    /// <summary>A jersey of your own: no team's art or logos (Kit only lends the models and the lettering spots).</summary>
    public bool Own { get; init; }
    /// <summary>The jersey model in texture space (for lines); null when the mesh can't be read.</summary>
    public JerseyShape Shape { get; init; }

    private bool[] _neckBand;
    /// <summary>
    /// The neck band's texels: a piece of its own in the texture (not joined to the body), at the top of the model. It's
    /// in the body's fabric region, so it has no region of its own to colour.
    /// </summary>
    public bool[] NeckBand => _neckBand ??= JerseyStudio.FindNeckBand(this);

    private CollarMap _collar;
    /// <summary>The collar's pieces (read from the base's alpha and the model once, when first asked).</summary>
    public CollarMap Collar => _collar ??= CollarMap.Build(this);
}

/// <summary>Where a line goes: round the body (measured up from the hem) or round both sleeves (measured from the cuff).</summary>
public enum LinePlace { Body, Sleeves }

/// <summary>
/// A stripe painted round the jersey: <see cref="From"/> cm from the hem or cuff to its near edge, <see cref="Width"/> cm
/// wide, with an optional edge (trim) of <see cref="EdgeWidth"/> cm on both sides. With an <see cref="Image"/> the
/// stripe is that image wrapped round instead (its height is the width; colour and edge aren't used).
/// </summary>
public sealed record StudioLine(LinePlace Place, double From, double Width, int Color, int? Edge = null, double EdgeWidth = 0, StudioStripe Image = null);

/// <summary>
/// An image wrapped round the body or the sleeves as a stripe (RGBA). Its left edge is the front centre. Mirrored
/// (both sides the same), it runs from the front centre round one side to the back centre and the other side is its
/// mirror. Otherwise it goes once round with the front centre in its middle. It's repeated <see cref="Repeat"/> times
/// along that.
/// </summary>
public sealed record StudioStripe(byte[] Rgba, int Width, int Height, string File, int Repeat = 1, bool Mirror = true)
{
    /// <summary>The template's guide colour: pixels exactly this are see-through when a strip is loaded.</summary>
    public const int Guide = 0xFF00FF;

    /// <summary>A strip from an image, with the template's guides (pure magenta) made see-through.</summary>
    public static StudioStripe From(byte[] rgba, int w, int h, string file, int repeat = 1, bool mirror = true)
    {
        var o = (byte[])rgba.Clone();
        for (int i = 0; i < o.Length; i += 4)
            if (o[i] == 255 && o[i + 1] == 0 && o[i + 2] == 255) o[i + 3] = 0;
        return new StudioStripe(o, w, h, file, Math.Max(1, repeat), mirror);
    }
}

/// <summary>A logo added to the jersey (not one of the stamp's): painted into the base where it's placed.</summary>
public sealed record StudioDecal(byte[] Rgba, int Width, int Height, string File, StampHotspot Place);

/// <summary>
/// The jersey model seen from its texture: each texel's place on the body, the hem, and the arms (A-pose: the sleeves
/// slope down from the shoulders). Measured from the mesh, so it fits each maker's cut.
/// </summary>
public sealed class JerseyShape
{
    public SurfaceMap Map { get; init; }
    public double Hem { get; init; }
    /// <summary>The arm's direction (outwards and down, for x &gt; 0; mirrored for the other arm).</summary>
    public double DirX { get; init; }
    public double DirY { get; init; }
    /// <summary>The armpit (|x|, y): the plane through it across the arm parts sleeves from the body.</summary>
    public double PitX { get; init; }
    public double PitY { get; init; }
    /// <summary>How far the cuff is past the armpit plane.</summary>
    public double Cuff { get; init; }
    /// <summary>The body's middle (z) in each 1 cm from the hem: images wrap round it.</summary>
    public double[] BodyMid { get; init; }
    /// <summary>The arm's middle in each 1 cm from the armpit plane: across it (up the arm's side) and in z.</summary>
    public double[] ArmMidQ { get; init; }
    public double[] ArmMidZ { get; init; }

    /// <summary>Across the arm, square to <see cref="Along"/> in the arm's plane (towards the top of the arm).</summary>
    public double Across(double x, double y) => -DirY * (Math.Abs(x) - PitX) + DirX * (y - PitY);

    /// <summary>
    /// The angle round the body (or the arm) at a point, from its front centre: −π…π, positive to the model's +x side
    /// (the screen's right seen from the front). On an arm it's the same for both (x is taken as |x|).
    /// </summary>
    public double Around(double x, double y, double z, bool sleeve)
    {
        if (!sleeve)
        {
            int b = Math.Clamp((int)((y - Hem) * 100), 0, BodyMid.Length - 1);
            return Math.Atan2(x, z - BodyMid[b]);
        }
        int a = Math.Clamp((int)(Along(x, y) * 100), 0, ArmMidQ.Length - 1);
        return Math.Atan2(Across(x, y) - ArmMidQ[a], z - ArmMidZ[a]);
    }

    /// <summary>How far round the body (or a sleeve) it is at <paramref name="cm"/> from the hem (or the cuff), in cm.</summary>
    public double Girth(LinePlace place, double cm)
    {
        var p = Map.Position;
        int n = Map.Size * Map.Size;
        const int Bins = 120;
        var radii = new List<double>[Bins];
        for (int i = 0; i < Bins; i++) radii[i] = new List<double>();
        for (int i = 0; i < n; i++)
        {
            float x = p[i * 3];
            if (float.IsNaN(x)) continue;
            double y = p[i * 3 + 1], z = p[i * 3 + 2];
            bool sleeve = OnSleeve(x, y);
            if (sleeve != (place == LinePlace.Sleeves)) continue;
            double d = sleeve ? (Cuff - Along(x, y)) * 100 : (y - Hem) * 100;
            if (Math.Abs(d - cm) > 1) continue;
            double th = Around(x, y, z, sleeve), r;
            if (!sleeve) r = Math.Sqrt(x * x + Math.Pow(z - BodyMid[Math.Clamp((int)((y - Hem) * 100), 0, BodyMid.Length - 1)], 2));
            else
            {
                int a = Math.Clamp((int)(Along(x, y) * 100), 0, ArmMidQ.Length - 1);
                r = Math.Sqrt(Math.Pow(Across(x, y) - ArmMidQ[a], 2) + Math.Pow(z - ArmMidZ[a], 2));
            }
            radii[Math.Clamp((int)((th + Math.PI) / (2 * Math.PI) * Bins), 0, Bins - 1)].Add(r);
        }
        double sum = 0, last = 0;
        for (int b = 0; b < Bins; b++)
        {
            if (radii[b].Count > 0) { radii[b].Sort(); last = radii[b][radii[b].Count / 2]; }
            sum += last * 2 * Math.PI / Bins;
        }
        return sum * 100;
    }
    /// <summary>
    /// How far the shoulder's corner (where the arm's straight top edge meets the shoulder) is behind the armpit plane:
    /// shoulder to cuff is <see cref="Cuff"/> + this, the sleeve's length as a photo of a jersey shows it.
    /// </summary>
    public double Shoulder { get; init; }

    /// <summary>How far along the arm a point is past the armpit plane (negative on the body).</summary>
    public double Along(double x, double y) => DirX * (Math.Abs(x) - PitX) + DirY * (y - PitY);

    /// <summary>Whether a point is on a sleeve: past the armpit plane and outside the body (the hem's corners are past the plane too).</summary>
    public bool OnSleeve(double x, double y) => Along(x, y) > 0 && Math.Abs(x) > PitX;

    public static JerseyShape Measure(MeshSet mesh, int size)
    {
        var map = SurfaceMap.Build(mesh.Lods[0], size);
        var p = map.Position;
        int n = size * size;
        double hem = map.Bounds[1], maxX = map.Bounds[3];
        // the body's width near the hem, and a straight fit of height against |x| out along the sleeves
        double sx = 0, sy = 0, sxx = 0, sxy = 0, torso = 0;
        long k = 0;
        for (int i = 0; i < n; i++)
        {
            float x = p[i * 3];
            if (float.IsNaN(x)) continue;
            double ax = Math.Abs(x), y = p[i * 3 + 1];
            if (y < hem + 0.12) torso = Math.Max(torso, ax);
            if (ax < maxX * 0.55) continue;
            sx += ax; sy += y; sxx += ax * ax; sxy += ax * y; k++;
        }
        double slope = k > 1 ? (k * sxy - sx * sy) / (k * sxx - sx * sx) : 0;
        double len = Math.Sqrt(1 + slope * slope);
        // the armpit: the lowest point just outside the body (the hem flares as wide as the arms start)
        double pitY = double.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float x = p[i * 3];
            if (float.IsNaN(x)) continue;
            double ax = Math.Abs(x);
            if (ax >= torso + 0.015 && ax < torso + 0.045) pitY = Math.Min(pitY, p[i * 3 + 1]);
        }
        if (pitY == double.MaxValue) pitY = (hem + map.Bounds[4]) / 2;
        var shape = new JerseyShape { Map = map, Hem = hem, DirX = 1 / len, DirY = slope / len, PitX = torso, PitY = pitY };
        // the top edge of the jersey seen from the front, in 1 cm steps of |x|
        double cuff = 0;
        int bins = (int)(maxX * 100) + 1;
        var top = new double[bins];
        Array.Fill(top, double.NaN);
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(p[i * 3])) continue;
            cuff = Math.Max(cuff, shape.Along(p[i * 3], p[i * 3 + 1]));
            int b = Math.Min(bins - 1, (int)(Math.Abs(p[i * 3]) * 100));
            if (double.IsNaN(top[b]) || p[i * 3 + 1] > top[b]) top[b] = p[i * 3 + 1];
        }
        // the shoulder's corner: going in from the cuff, where the top edge leaves the arm's straight top line
        double tx = 0, ty = 0, txx = 0, txy = 0;
        int tk = 0;
        for (int b = (int)(bins * 0.6); b < bins - 2; b++)
            if (!double.IsNaN(top[b])) { double x = b / 100.0; tx += x; ty += top[b]; txx += x * x; txy += x * top[b]; tk++; }
        double corner = torso, cornerY = pitY;
        if (tk > 2)
        {
            double ts = (tk * txy - tx * ty) / (tk * txx - tx * tx), t0 = (ty - ts * tx) / tk;
            for (int b = (int)(bins * 0.6); b >= 0; b--)
            {
                if (double.IsNaN(top[b])) continue;
                double x = b / 100.0;
                if (top[b] < t0 + ts * x - 0.015 || top[b] > t0 + ts * x + 0.015) break;
                corner = x; cornerY = top[b];
            }
        }
        // the middle of the body at each height and of the arm along it, for wrapping images round them
        int bodyBins = (int)((map.Bounds[4] - hem) * 100) + 1, armBins = (int)(cuff * 100) + 1;
        var bz = new double[bodyBins]; var bc = new int[bodyBins];
        var aq = new double[armBins]; var az = new double[armBins]; var ac = new int[armBins];
        for (int i = 0; i < n; i++)
        {
            float x = p[i * 3];
            if (float.IsNaN(x)) continue;
            double y = p[i * 3 + 1], z = p[i * 3 + 2];
            if (shape.OnSleeve(x, y))
            {
                int a = Math.Clamp((int)(shape.Along(x, y) * 100), 0, armBins - 1);
                aq[a] += shape.Across(x, y); az[a] += z; ac[a]++;
            }
            else
            {
                int b = Math.Clamp((int)((y - hem) * 100), 0, bodyBins - 1);
                bz[b] += z; bc[b]++;
            }
        }
        Fill(bz, bc); Fill(aq, ac); Fill(az, ac);
        return new JerseyShape
        {
            Map = map, Hem = hem, DirX = shape.DirX, DirY = shape.DirY, PitX = torso, PitY = pitY, Cuff = cuff,
            Shoulder = Math.Max(0, -shape.Along(corner, cornerY)), BodyMid = bz, ArmMidQ = aq, ArmMidZ = az,
        };
    }

    /// <summary>Sums into means; an empty bin takes the nearest filled one below (or above, at the start).</summary>
    private static void Fill(double[] sum, int[] count)
    {
        int first = Array.FindIndex(count, c => c > 0);
        for (int i = 0; i < sum.Length; i++)
        {
            if (count[i] > 0) sum[i] /= count[i];
            else sum[i] = i > 0 && i > first ? sum[i - 1] : 0;
        }
        for (int i = 0; i < first; i++) sum[i] = sum[first];
    }

    /// <summary>
    /// Paints lines over <paramref name="rgba"/> (the map's size), each with its edges, soft over about a texel.
    /// <paramref name="layer"/> builds up the alpha too (for the lines on their own over a see-through sheet).
    /// </summary>
    public void Paint(byte[] rgba, IReadOnlyList<StudioLine> lines, bool layer = false)
    {
        if (lines.Count == 0) return;
        int size = Map.Size;
        var p = Map.Position;
        // about how many cm a texel covers on the cloth, for a soft edge
        double soft = Math.Max(Map.Bounds[4] - Map.Bounds[1], 0.5) / size * 160;
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float px = p[i * 3];
                if (float.IsNaN(px)) continue;
                double py = p[i * 3 + 1];
                double along = Along(px, py);
                bool sleeve = OnSleeve(px, py);
                double around = double.NaN;
                foreach (var line in lines)
                {
                    // the distance (cm) from the hem or the cuff
                    double d;
                    if (line.Place == LinePlace.Body) { if (sleeve) continue; d = (py - Hem) * 100; }
                    else { if (!sleeve) continue; d = (Cuff - along) * 100; }
                    double a0 = line.From, a1 = line.From + line.Width, e = line.Edge != null ? Math.Max(0, line.EdgeWidth) : 0;
                    if (line.Image is { } img)
                    {
                        if (d < a0 || d > a1 || line.Width <= 0) continue;
                        if (double.IsNaN(around)) around = Around(px, py, p[i * 3 + 2], sleeve);
                        // u: round from the front centre; v: the image's top is the line's far edge
                        double u = img.Mirror ? Math.Abs(around) / Math.PI : 0.5 + around / (2 * Math.PI);
                        u *= img.Repeat;
                        u -= Math.Floor(u);
                        double v = 1 - (d - a0) / line.Width;
                        var (c, a) = Sample(img, u * img.Width - 0.5, v * img.Height - 0.5);
                        Blend(rgba, i, c, a, layer);
                        continue;
                    }
                    double Cover(double lo, double hi) => Math.Clamp((d - lo) / soft + 0.5, 0, 1) * Math.Clamp((hi - d) / soft + 0.5, 0, 1);
                    if (e > 0) Blend(rgba, i, line.Edge.Value, Cover(a0 - e, a1 + e), layer);
                    Blend(rgba, i, line.Color, Cover(a0, a1), layer);
                }
            }
        });
    }

    private static void Blend(byte[] rgba, int i, int c, double a, bool layer = false)
    {
        if (a <= 0) return;
        int o = i * 4;
        rgba[o] = (byte)Math.Round((c >> 16 & 255) * a + rgba[o] * (1 - a));
        rgba[o + 1] = (byte)Math.Round((c >> 8 & 255) * a + rgba[o + 1] * (1 - a));
        rgba[o + 2] = (byte)Math.Round((c & 255) * a + rgba[o + 2] * (1 - a));
        if (layer) rgba[o + 3] = (byte)Math.Round(255 * a + rgba[o + 3] * (1 - a));
    }

    /// <summary>The strip's colour and coverage at pixel (x, y), bilinear, wrapping across and clamped down.</summary>
    private static (int Color, double Alpha) Sample(StudioStripe s, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double r = 0, g = 0, b = 0, a = 0;
        for (int k = 0; k < 4; k++)
        {
            int xi = ((x0 + (k & 1)) % s.Width + s.Width) % s.Width, yi = Math.Clamp(y0 + (k >> 1), 0, s.Height - 1);
            double w = ((k & 1) == 1 ? fx : 1 - fx) * ((k >> 1) == 1 ? fy : 1 - fy);
            int o = (yi * s.Width + xi) * 4;
            double pa = s.Rgba[o + 3] / 255.0 * w;
            r += s.Rgba[o] * pa; g += s.Rgba[o + 1] * pa; b += s.Rgba[o + 2] * pa; a += pa;
        }
        if (a <= 0) return (0, 0);
        return ((int)Math.Round(r / a) << 16 | (int)Math.Round(g / a) << 8 | (int)Math.Round(b / a), a);
    }

    /// <summary>The lines on their own, over a see-through sheet the map's size (RGBA).</summary>
    public byte[] LinesLayer(IReadOnlyList<StudioLine> lines)
    {
        var rgba = new byte[Map.Size * Map.Size * 4];
        Paint(rgba, lines, true);
        return rgba;
    }

    /// <summary>
    /// A guide to paint a strip on, at <paramref name="pxPerCm"/>: as wide as the way round at the line's middle (half of
    /// it when mirrored), as tall as the line. The guides (front, sides, back, and the underarm on a sleeve, plus a tick
    /// every 5 cm) are pure magenta, which is see-through when the strip is loaded, so they can be left in.
    /// </summary>
    public (byte[] Rgba, int Width, int Height, double Girth) StripTemplate(LinePlace place, double from, double height, bool mirror, double pxPerCm = 10)
    {
        double girth = Girth(place, from + height / 2);
        double span = mirror ? girth / 2 : girth;
        int w = Math.Clamp((int)Math.Round(span * pxPerCm), 16, 8192), h = Math.Clamp((int)Math.Round(height * pxPerCm), 4, 4096);
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < rgba.Length; i += 4) { rgba[i] = rgba[i + 1] = rgba[i + 2] = 238; rgba[i + 3] = 255; }
        void Column(double u, int thick)
        {
            int x = Math.Clamp((int)Math.Round(u * (w - 1)), 0, w - 1);
            for (int t = 0; t < thick; t++)
                for (int y = 0; y < h; y++)
                {
                    int xx = Math.Clamp(x + t - thick / 2, 0, w - 1), o = (y * w + xx) * 4;
                    rgba[o] = 255; rgba[o + 1] = 0; rgba[o + 2] = 255;
                }
        }
        // the quarter marks: front, side, back (and the other side when it goes all the way round)
        for (int q = 0; q <= (mirror ? 2 : 4); q++) Column(mirror ? q / 2.0 : q / 4.0, 3);
        // a tick every 5 cm along the top and bottom
        for (double cm = 5; cm < span; cm += 5)
        {
            int x = (int)Math.Round(cm * pxPerCm);
            if (x >= w) break;
            for (int y = 0; y < Math.Min(h, (int)(pxPerCm * 1.5)); y++)
                foreach (int yy in new[] { y, h - 1 - y })
                {
                    int o = (yy * w + x) * 4;
                    rgba[o] = 255; rgba[o + 1] = 0; rgba[o + 2] = 255;
                }
        }
        return (rgba, w, h, girth);
    }
}

/// <summary>A jersey design: what's painted under the logos, and where the logos and lettering go.</summary>
public sealed class StudioDesign
{
    /// <summary>The design painted over the jersey; null keeps the kit's own art.</summary>
    public StudioPattern Pattern { get; set; }
    /// <summary>Colour of each pattern zone (RGB), null when not set (the zone is left out).</summary>
    public Dictionary<int, int> ZoneColors { get; } = new();
    /// <summary>Flat fills of fabric regions (1 body … 5 collar sides), on top of the art.</summary>
    public Dictionary<int, int> RegionColors { get; } = new();
    /// <summary>A whole jersey image (RGBA, studio size) used as the art instead of the kit's.</summary>
    public byte[] Image { get; set; }
    /// <summary>The hotspots as edited (same ids as the kit's stamp).</summary>
    public List<StampHotspot> Spots { get; set; }
    /// <summary>Replaced logos by branding layer, and the image files they came from.</summary>
    public Dictionary<int, (byte[] Rgba, int Width, int Height)> Logos { get; } = new();
    public Dictionary<int, string> LogoFiles { get; } = new();
    /// <summary>Stripes round the body and sleeves, painted over the art in order.</summary>
    public List<StudioLine> Lines { get; set; } = new();
    /// <summary>Logos added anywhere on the jersey, painted into the base (under the stamped ones).</summary>
    public List<StudioDecal> Decals { get; set; } = new();
    /// <summary>Colours of the collar's pieces, and stripes across the neck band (painted over the region fills).</summary>
    public Dictionary<CollarPart, int> CollarColors { get; } = new();
    public List<CollarStripe> CollarStripes { get; set; } = new();
}

public static class JerseyStudio
{
    /// <summary>
    /// The fabric regions of the base color's alpha (k/7): what each is, as it shows on the model. 2 is the small
    /// gussets under the arms, 3 the yoke round the neck and over the shoulders, 4 runs down the sides; 5, 6 and 7 are
    /// collar pieces (see <see cref="CollarMap"/>). 8 isn't in the alpha: it's the neck band, found by its shape
    /// (<see cref="StudioSource.NeckBand"/>).
    /// </summary>
    public static readonly string[] RegionNames = { "", "Body", "Underarm gussets", "Shoulder yoke", "Side panels", "Collar ends", "V insert", "Collar front", "Neck band" };
    public const int NeckBandRegion = 8;

    /// <summary>The neck band piece: a texture island under a twentieth of the jersey whose top is at the model's top.</summary>
    public static bool[] FindNeckBand(StudioSource src)
    {
        int size = src.Size, n = size * size;
        var mask = new bool[n];
        var map = src.Shape?.Map;
        if (map == null || map.Size != size) return mask;
        var pos = map.Position;
        var seen = new bool[n];
        var queue = new Queue<int>();
        var piece = new List<int>();
        for (int start = 0; start < n; start++)
        {
            if (seen[start] || float.IsNaN(pos[start * 3])) continue;
            piece.Clear();
            float top = float.MinValue;
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                piece.Add(i);
                top = Math.Max(top, pos[i * 3 + 1]);
                int x = i % size;
                foreach (int j in new[] { x > 0 ? i - 1 : -1, x < size - 1 ? i + 1 : -1, i - size, i + size })
                    if (j >= 0 && j < n && !seen[j] && !float.IsNaN(pos[j * 3])) { seen[j] = true; queue.Enqueue(j); }
            }
            if (piece.Count < n / 20 && top > map.Bounds[4] - 0.05)
                foreach (int i in piece) mask[i] = true;
        }
        return mask;
    }

    public static int Region(byte alpha) => (int)Math.Round(alpha * 7 / 255.0);

    private static readonly Regex MaskName = new(@"league/custom/jersey/(\w+)/custom_(\d+)/jersey_\1_custom_\2_recolor(\d)$", RegexOptions.IgnoreCase);

    /// <summary>Every create-a-team jersey design in the game, adidas first.</summary>
    public static List<StudioTemplate> Templates(AssetIndex index)
    {
        var found = new Dictionary<(string, int), SortedDictionary<int, string>>();
        foreach (var (name, e) in index.Ebx)
        {
            if (e.Type != "TextureAsset" || !name.Contains("/custom/jersey/", StringComparison.OrdinalIgnoreCase)) continue;
            var m = MaskName.Match(name);
            if (!m.Success) continue;
            var key = (m.Groups[1].Value.ToLowerInvariant(), int.Parse(m.Groups[2].Value));
            if (!found.TryGetValue(key, out var masks)) found[key] = masks = new();
            masks[int.Parse(m.Groups[3].Value)] = name;
        }
        return found.Select(f =>
            {
                string folder = f.Value.Values.First()[..f.Value.Values.First().LastIndexOf('/')];
                string color = $"{folder}/jersey_{f.Key.Item1}_custom_{f.Key.Item2}_color";
                return new StudioTemplate(f.Key.Item1, f.Key.Item2, index.GetEbx(color) != null ? color : null, f.Value.Values.ToList());
            })
            .OrderBy(t => t.Brand == "adidas" ? 0 : 1).ThenBy(t => t.Brand).ThenBy(t => t.Number).ToList();
    }

    /// <summary>Reads a design's detail layer and masks from the game at <paramref name="size"/>.</summary>
    public static StudioPattern LoadPattern(AssetIndex index, StudioTemplate t, int size)
    {
        byte[] Read(string name)
        {
            var tex = Kits.Load(index, name, size, 0);
            if (tex.Rgba == null) throw new InvalidDataException($"{name} can't be decoded.");
            return Resize(tex.Rgba, tex.Width, tex.Height, size);
        }
        var masks = t.Masks.Select(Read).ToList();
        var detail = t.Color != null ? Read(t.Color) : new byte[size * size * 4];
        var count = new long[masks.Count * 3];
        int n = size * size;
        for (int m = 0; m < masks.Count; m++)
        for (int i = 0; i < n; i++)
        for (int c = 0; c < 3; c++)
            if (masks[m][i * 4 + c] > 127) count[m * 3 + c]++;
        var zones = count.Select((k, z) => (Zone: z, Share: k / (double)n)).Where(z => z.Share > 0.0005).OrderByDescending(z => z.Share).ToList();
        return new StudioPattern { Template = t, Size = size, Detail = detail, Masks = masks, Zones = zones };
    }

    /// <summary>
    /// A small map of a design for a picker: each pixel's zone (-1 for none) over the front of the jersey and its
    /// sleeves, and the zones by share (largest first), so it can be coloured for any team without reading it again.
    /// </summary>
    public static (sbyte[] Map, int Size, List<int> Order) ThumbZones(AssetIndex index, StudioTemplate t, int size = 64)
    {
        var p = LoadPattern(index, t, 256);
        var map = new sbyte[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // the front and sleeves: the lower part of the sheet (upright there)
            int sx = (int)(256 * (0.04 + 0.92 * (x + 0.5) / size)), sy = Math.Min(255, (int)(256 * (0.34 + 0.66 * (y + 0.5) / size)));
            int best = -1, bestV = 127;
            foreach (var (zone, _) in p.Zones)
            {
                int v = p.Masks[zone / 3][(sy * 256 + sx) * 4 + zone % 3];
                if (v > bestV) { bestV = v; best = zone; }
            }
            map[y * size + x] = (sbyte)best;
        }
        return (map, size, p.Zones.Select(z => z.Zone).ToList());
    }

    /// <summary>Reads what the studio draws a kit's jersey from: its base, stamp, logos and font.</summary>
    public static StudioSource LoadSource(AssetIndex index, Kit kit, int size)
    {
        if (!kit.IsStamped("jersey")) throw new InvalidDataException($"{kit} has no stamped jersey (base color, stamp and branding logos).");
        byte[] ebx = index.Read(index.GetEbx(kit.Stamps["jersey"]));
        var spots = KitStamp.Parse(ebx, "jersey");
        var b = Kits.Load(index, kit.Slot("jersey_base_color").Name, size, 0);
        if (b.Rgba == null) throw new InvalidDataException("The jersey's base color can't be decoded.");
        var array = kit.Slot("branding_jersey_color_array");
        var logos = new Dictionary<int, (byte[], int, int)>();
        foreach (var (_, layer) in KitStamp.Brands(spots))
        {
            if (logos.ContainsKey(layer)) continue;
            Texture t = null;
            try { if (array != null) t = Kits.Load(index, array.Name, 512, layer); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
            if (t?.Rgba == null && kit.Slot($"branding_jersey_{layer}_color") is { } one) t = Kits.Load(index, one.Name, 512, 0);
            if (t?.Rgba != null) logos[layer] = (t.Rgba, t.Width, t.Height);
        }
        byte[] font = null;
        int fontSize = 0;
        if (kit.Slot("font_color") is { } f)
        {
            var ft = Kits.Load(index, f.Name, 2048, 0);
            if (ft.Rgba != null && ft.Width == ft.Height) { font = ft.Rgba; fontSize = ft.Width; }
        }
        JerseyShape shape = null;
        try
        {
            string mesh = Kits.Meshes(index, kit).FirstOrDefault(m => m.Garment == "jersey").Mesh;
            if (mesh != null) shape = JerseyShape.Measure(Kits.LoadMesh(index, mesh), size);
        }
        catch (InvalidDataException) { /* no lines without the model */ }
        return new StudioSource
        {
            Kit = kit, Size = size, Base = Resize(b.Rgba, b.Width, b.Height, size), StampEbx = ebx, Spots = spots,
            Logos = logos, Font = font, FontSize = fontSize, Shape = shape,
        };
    }

    /// <summary>
    /// The base color a design paints: the art (the kit's, an image, or a pattern in its zone colours over the kit's
    /// art), then the region fills. The kit's alpha (the fabric map the game's shader reads) is kept.
    /// </summary>
    public static byte[] PaintBase(StudioSource src, StudioDesign d)
    {
        int size = src.Size;
        var art = d.Image != null ? (byte[])d.Image.Clone() : (byte[])src.Base.Clone();
        if (d.Pattern != null) art = Paint(art, size, d.Pattern, d.ZoneColors);
        int n = size * size;
        var neck = d.RegionColors.ContainsKey(NeckBandRegion) ? src.NeckBand : null;
        for (int i = 0; i < n; i++)
        {
            byte a = src.Base[i * 4 + 3];
            if (d.RegionColors.Count > 0 && d.RegionColors.TryGetValue(neck != null && neck[i] ? NeckBandRegion : Region(a), out int c))
            {
                art[i * 4] = (byte)(c >> 16); art[i * 4 + 1] = (byte)(c >> 8); art[i * 4 + 2] = (byte)c;
            }
        }
        if ((d.CollarColors.Count > 0 || d.CollarStripes.Count > 0) && src.Shape?.Map.Size == size) src.Collar.Paint(art, d.CollarColors, d.CollarStripes);
        if (d.Lines.Count > 0 && src.Shape?.Map.Size == size) src.Shape.Paint(art, d.Lines);
        if (d.Decals.Count > 0) art = KitStamp.Compose(art, size, size, d.Decals.Select(x => (x.Place, x.Rgba, x.Width, x.Height)));
        for (int i = 0; i < n; i++) art[i * 4 + 3] = src.Base[i * 4 + 3];
        return art;
    }

    /// <summary>Paints a pattern's zones over <paramref name="under"/> (each zone blends in by its mask); a zone with no colour is skipped.</summary>
    public static byte[] Paint(byte[] under, int size, StudioPattern p, IReadOnlyDictionary<int, int> colors)
    {
        var dst = (byte[])under.Clone();
        var masks = p.Masks.Select(m => p.Size == size ? m : Resize(m, p.Size, p.Size, size)).ToList();
        var zones = colors.Where(z => z.Key / 3 < masks.Count).ToList();
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                int o = (y * size + x) * 4;
                double r = dst[o], g = dst[o + 1], b = dst[o + 2];
                foreach (var (zone, c) in zones)
                {
                    double w = masks[zone / 3][o + zone % 3] / 255.0;
                    if (w <= 0) continue;
                    r = r * (1 - w) + (c >> 16 & 255) * w;
                    g = g * (1 - w) + (c >> 8 & 255) * w;
                    b = b * (1 - w) + (c & 255) * w;
                }
                dst[o] = (byte)Math.Round(r);
                dst[o + 1] = (byte)Math.Round(g);
                dst[o + 2] = (byte)Math.Round(b);
            }
        });
        return dst;
    }

    /// <summary>The jersey as the game draws it with this design: the painted base, the logos and (when given) the lettering.</summary>
    public static byte[] Draw(StudioSource src, StudioDesign d, byte[] painted, KitLettering text)
    {
        var spots = d.Spots ?? src.Spots;
        var logos = new List<(StampHotspot, byte[], int, int)>();
        foreach (var (spot, layer) in KitStamp.Brands(spots))
        {
            if (!spot.Enabled) continue;
            if (d.Logos.TryGetValue(layer, out var l) || src.Logos.TryGetValue(layer, out l)) logos.Add((spot, l.Rgba, l.Width, l.Height));
        }
        if (text != null && src.Font != null) logos.AddRange(Kits.Lettering(spots, text, src.Font, src.FontSize, src.Size, src.Size));
        return KitStamp.Compose(painted, src.Size, src.Size, logos);
    }

    /// <summary>
    /// The design for writing into another kit folder (whose stamp has its own logos): the painted base with this
    /// design's stamped logos painted in (alpha = the fabric map), and that kit's stamp spots: every logo switched off,
    /// the lettering spots placed as this design has them (by name).
    /// </summary>
    public static (byte[] Base, List<StampHotspot> Spots) ForKit(StudioSource src, StudioDesign d, byte[] painted, IReadOnlyList<StampHotspot> target)
    {
        var baked = Draw(src, d, painted, null);
        for (int i = 3; i < baked.Length; i += 4) baked[i] = painted[i];
        var mine = (d.Spots ?? src.Spots).Where(s => s.Type == "font").GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.First());
        var spots = target.Select(t => t.Type == "brand" ? t with { Enabled = false }
            : mine.TryGetValue(t.Name, out var m) ? t with { Enabled = m.Enabled, R = m.R, Sx = m.Sx, Sy = m.Sy, Tx = m.Tx, Ty = m.Ty, Fsx = m.Fsx, Fsy = m.Fsy, Ftx = m.Ftx, Fty = m.Fty }
            : t).ToList();
        return (baked, spots);
    }

    /// <summary>Nearest/box resize of a square-ish RGBA image to size×size.</summary>
    public static byte[] Resize(byte[] rgba, int w, int h, int size)
    {
        if (w == size && h == size) return rgba;
        var dst = new byte[size * size * 4];
        Parallel.For(0, size, y =>
        {
            int sy = Math.Min(h - 1, (int)((y + 0.5) * h / size));
            for (int x = 0; x < size; x++)
            {
                int sx = Math.Min(w - 1, (int)((x + 0.5) * w / size));
                Array.Copy(rgba, (sy * w + sx) * 4, dst, (y * size + x) * 4, 4);
            }
        });
        return dst;
    }
}
