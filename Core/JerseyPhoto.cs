namespace NHL27Kit.Core;

/// <summary>A band of one colour along a line drawn on a photo: <see cref="From"/> cm from the line's start, <see cref="Width"/> cm wide.</summary>
public sealed record PhotoRun(double From, double Width, int Color);

/// <summary>
/// Reads a jersey from a photo: the colours along a line drawn up the body (hem → armpit) or a sleeve (cuff → shoulder),
/// split into bands and scaled to the model's centimetres; and a crest cut out of a box round it. What's read is turned
/// into a studio design: flat fabric colours and lines, with the crest painted into the color map.
/// </summary>
public static class JerseyPhoto
{
    /// <summary>Colours closer than this (CIE76 ΔE) are one colour: lighting on cloth moves a colour about this much.</summary>
    public const double Same = 16;

    /// <summary>
    /// The colour bands along the line from (x0,y0) to (x1,y1), taken as <paramref name="cm"/> long. Each sample is the
    /// median across a short strip at right angles to the line (so a crease or a speck doesn't count); bands thinner than
    /// <paramref name="minCm"/> go to their neighbour.
    /// </summary>
    public static List<PhotoRun> Runs(byte[] rgba, int w, int h, double x0, double y0, double x1, double y1, double cm, double minCm = 0.5)
    {
        double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int n = Math.Max(2, (int)Math.Round(len));
        if (len < 4 || cm <= 0) return new List<PhotoRun>();
        double ux = (x1 - x0) / len, uy = (y1 - y0) / len;
        int half = Math.Max(2, (int)(len / 120));
        var lab = new (double L, double A, double B)[n];
        var strip = new List<(double, double, double)>();
        for (int t = 0; t < n; t++)
        {
            double cx = x0 + (x1 - x0) * t / (n - 1), cy = y0 + (y1 - y0) * t / (n - 1);
            strip.Clear();
            for (int k = -half; k <= half; k++)
            {
                int px = (int)Math.Round(cx - uy * k), py = (int)Math.Round(cy + ux * k);
                if (px < 0 || py < 0 || px >= w || py >= h) continue;
                int i = (py * w + px) * 4;
                strip.Add(Lab(rgba[i], rgba[i + 1], rgba[i + 2]));
            }
            lab[t] = strip.Count > 0 ? Median(strip) : (0, 0, 0);
        }
        // a little smoothing along the line too
        int win = Math.Max(1, n / 300);
        var smooth = new (double, double, double)[n];
        for (int t = 0; t < n; t++)
        {
            strip.Clear();
            for (int k = Math.Max(0, t - win); k <= Math.Min(n - 1, t + win); k++) strip.Add(lab[k]);
            smooth[t] = Median(strip);
        }
        double perSample = cm / (n - 1);
        int minSamples = Math.Max(2, (int)Math.Ceiling(minCm / perSample));

        // grow bands: a new one starts when enough samples in a row differ from the current band's colour
        var bands = new List<(int Start, int End)>();
        int start = 0, pending = -1;
        var mean = smooth[0];
        int count = 1;
        for (int t = 1; t < n; t++)
        {
            if (Delta(smooth[t], mean) > Same)
            {
                if (pending < 0) pending = t;
                if (t - pending + 1 >= minSamples)
                {
                    bands.Add((start, pending));
                    start = pending;
                    mean = Mean(smooth, pending, t + 1);
                    count = t + 1 - pending;
                    pending = -1;
                }
            }
            else
            {
                pending = -1;
                count++;
                mean = (mean.Item1 + (smooth[t].Item1 - mean.Item1) / count, mean.Item2 + (smooth[t].Item2 - mean.Item2) / count, mean.Item3 + (smooth[t].Item3 - mean.Item3) / count);
            }
        }
        bands.Add((start, n));

        // each band's colour: the median of its middle (edges blur into the next band)
        var cols = bands.Select(b =>
        {
            int trim = (b.End - b.Start) / 6;
            strip.Clear();
            for (int t = b.Start + trim; t < b.End - trim; t++) strip.Add(smooth[t]);
            if (strip.Count == 0) strip.Add(smooth[b.Start]);
            return Median(strip);
        }).ToList();
        // neighbours that came out the same colour join
        for (int i = bands.Count - 1; i > 0; i--)
            if (Delta(cols[i], cols[i - 1]) <= Same)
            {
                bands[i - 1] = (bands[i - 1].Start, bands[i].End);
                bands.RemoveAt(i);
                cols.RemoveAt(i);
            }
        return bands.Select((b, i) => new PhotoRun(Math.Round(b.Start * perSample, 1), Math.Round((b.End - b.Start) * perSample, 1), Rgb(cols[i]))).ToList();
    }

    /// <summary>The median colour round a point (a square <paramref name="r"/> pixels each way).</summary>
    public static int Sample(byte[] rgba, int w, int h, double x, double y, int r = 4)
    {
        var list = new List<(double, double, double)>();
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int px = (int)x + dx, py = (int)y + dy;
                if (px < 0 || py < 0 || px >= w || py >= h) continue;
                int i = (py * w + px) * 4;
                list.Add(Lab(rgba[i], rgba[i + 1], rgba[i + 2]));
            }
        return list.Count > 0 ? Rgb(Median(list)) : 0;
    }

    /// <summary>
    /// Gives colours that are the same (within <see cref="Same"/>) one value across every list (weighted by width), so
    /// a stripe read on the body and on a sleeve comes out one colour.
    /// </summary>
    public static List<List<PhotoRun>> Unify(IReadOnlyList<List<PhotoRun>> sets, IList<int> picks)
    {
        var groups = new List<(double L, double A, double B, double Weight)>();
        int Group((double, double, double) c, double weight)
        {
            for (int g = 0; g < groups.Count; g++)
                if (Delta(c, (groups[g].L, groups[g].A, groups[g].B)) <= Same)
                {
                    var x = groups[g];
                    double tw = x.Weight + weight;
                    groups[g] = (x.L + (c.Item1 - x.L) * weight / tw, x.A + (c.Item2 - x.A) * weight / tw, x.B + (c.Item3 - x.B) * weight / tw, tw);
                    return g;
                }
            groups.Add((c.Item1, c.Item2, c.Item3, weight));
            return groups.Count - 1;
        }
        var ids = sets.Select(s => s.Select(r => Group(Lab(r.Color), r.Width)).ToList()).ToList();
        var pickIds = picks.Select(c => Group(Lab(c), 1)).ToList();
        int Col(int g) => Rgb((groups[g].L, groups[g].A, groups[g].B));
        for (int i = 0; i < picks.Count; i++) picks[i] = Col(pickIds[i]);
        return sets.Select((s, k) => s.Select((r, i) => r with { Color = Col(ids[k][i]) }).ToList()).ToList();
    }

    /// <summary>The colour covering the most of the bands.</summary>
    public static int Dominant(IEnumerable<PhotoRun> runs) =>
        runs.GroupBy(r => r.Color).OrderByDescending(g => g.Sum(r => r.Width)).Select(g => g.Key).FirstOrDefault();

    /// <summary>
    /// The bands that aren't the base colour, as studio lines. A band with a thinner band of one colour on both sides
    /// becomes one line with that edge.
    /// </summary>
    public static List<StudioLine> Lines(IReadOnlyList<PhotoRun> runs, int baseColor, LinePlace place)
    {
        var lines = new List<StudioLine>();
        for (int i = 0; i < runs.Count; i++)
        {
            var r = runs[i];
            if (r.Color == baseColor) continue;
            if (i + 2 < runs.Count && runs[i + 2].Color == r.Color && runs[i + 1].Color != baseColor && runs[i + 1].Color != r.Color
                && r.Width < runs[i + 1].Width && runs[i + 2].Width < runs[i + 1].Width
                && Math.Max(r.Width, runs[i + 2].Width) <= 1.8 * Math.Min(r.Width, runs[i + 2].Width))
            {
                var mid = runs[i + 1];
                lines.Add(new StudioLine(place, mid.From, mid.Width, mid.Color, r.Color, Math.Round((r.Width + runs[i + 2].Width) / 2, 1)));
                i += 2;
                continue;
            }
            lines.Add(new StudioLine(place, r.From, r.Width, r.Color));
        }
        return lines;
    }

    /// <summary>
    /// The crest cut out of the box (x, y, bw, bh): inside the ellipse that fills the box when <paramref name="round"/>,
    /// else everything but the cloth round it (the colour at the box's border, flooded in from the border). RGBA, bw × bh.
    /// </summary>
    public static byte[] Crest(byte[] rgba, int w, int h, int x, int y, int bw, int bh, bool round)
    {
        x = Math.Clamp(x, 0, w - 1); y = Math.Clamp(y, 0, h - 1);
        bw = Math.Clamp(bw, 1, w - x); bh = Math.Clamp(bh, 1, h - y);
        var o = new byte[bw * bh * 4];
        for (int j = 0; j < bh; j++) Array.Copy(rgba, ((y + j) * w + x) * 4, o, j * bw * 4, bw * 4);
        if (round)
        {
            double rx = bw / 2.0, ry = bh / 2.0;
            for (int j = 0; j < bh; j++)
                for (int i = 0; i < bw; i++)
                {
                    double dx = (i + 0.5 - rx) / rx, dy = (j + 0.5 - ry) / ry;
                    // about a pixel of soft edge
                    double d = (1 - Math.Sqrt(dx * dx + dy * dy)) * Math.Min(rx, ry);
                    o[(j * bw + i) * 4 + 3] = (byte)Math.Round(255 * Math.Clamp(d + 0.5, 0, 1));
                }
            return o;
        }
        var border = new List<(double, double, double)>();
        for (int i = 0; i < bw; i++) { border.Add(LabAt(o, bw, i, 0)); border.Add(LabAt(o, bw, i, bh - 1)); }
        for (int j = 0; j < bh; j++) { border.Add(LabAt(o, bw, 0, j)); border.Add(LabAt(o, bw, bw - 1, j)); }
        var cloth = Median(border);
        var gone = new bool[bw * bh];
        var queue = new Queue<int>();
        void Try(int i, int j)
        {
            if (i < 0 || j < 0 || i >= bw || j >= bh) return;
            int k = j * bw + i;
            if (gone[k] || Delta(LabAt(o, bw, i, j), cloth) > Same * 1.5) return;
            gone[k] = true;
            queue.Enqueue(k);
        }
        for (int i = 0; i < bw; i++) { Try(i, 0); Try(i, bh - 1); }
        for (int j = 0; j < bh; j++) { Try(0, j); Try(bw - 1, j); }
        while (queue.Count > 0)
        {
            int k = queue.Dequeue(), i = k % bw, j = k / bw;
            Try(i - 1, j); Try(i + 1, j); Try(i, j - 1); Try(i, j + 1);
        }
        // a pixel next to the cloth is half see-through, so the cut isn't jagged
        for (int j = 0; j < bh; j++)
            for (int i = 0; i < bw; i++)
            {
                int k = j * bw + i;
                if (gone[k]) { o[k * 4 + 3] = 0; continue; }
                bool edge = (i > 0 && gone[k - 1]) || (i < bw - 1 && gone[k + 1]) || (j > 0 && gone[k - bw]) || (j < bh - 1 && gone[k + bw]);
                if (edge) o[k * 4 + 3] = 128;
            }
        return o;
    }

    // ================================================================ colour

    private static (double, double, double) LabAt(byte[] rgba, int w, int x, int y)
    {
        int i = (y * w + x) * 4;
        return Lab(rgba[i], rgba[i + 1], rgba[i + 2]);
    }

    public static (double L, double A, double B) Lab(int rgb) => Lab((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    public static (double L, double A, double B) Lab(byte r, byte g, byte b)
    {
        static double Lin(byte c) { double v = c / 255.0; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        double lr = Lin(r), lg = Lin(g), lb = Lin(b);
        double x = (0.4124 * lr + 0.3576 * lg + 0.1805 * lb) / 0.95047;
        double y = 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
        double z = (0.0193 * lr + 0.1192 * lg + 0.9505 * lb) / 1.08883;
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16 / 116.0;
        double fx = F(x), fy = F(y), fz = F(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    public static int Rgb((double L, double A, double B) lab)
    {
        double fy = (lab.L + 16) / 116, fx = fy + lab.A / 500, fz = fy - lab.B / 200;
        static double F(double t) => t * t * t > 0.008856 ? t * t * t : (t - 16 / 116.0) / 7.787;
        double x = F(fx) * 0.95047, y = F(fy), z = F(fz) * 1.08883;
        double lr = 3.2406 * x - 1.5372 * y - 0.4986 * z, lg = -0.9689 * x + 1.8758 * y + 0.0415 * z, lb = 0.0557 * x - 0.2040 * y + 1.0570 * z;
        static int G(double v) { v = Math.Clamp(v, 0, 1); return (int)Math.Round(255 * (v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055)); }
        return G(lr) << 16 | G(lg) << 8 | G(lb);
    }

    public static double Delta((double, double, double) a, (double, double, double) b)
    {
        double dl = a.Item1 - b.Item1, da = a.Item2 - b.Item2, db = a.Item3 - b.Item3;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    private static (double, double, double) Median(List<(double, double, double)> list)
    {
        static double M(IEnumerable<double> v) { var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
        return (M(list.Select(c => c.Item1)), M(list.Select(c => c.Item2)), M(list.Select(c => c.Item3)));
    }

    private static (double, double, double) Mean((double, double, double)[] a, int from, int to)
    {
        double l = 0, x = 0, y = 0;
        for (int i = from; i < to; i++) { l += a[i].Item1; x += a[i].Item2; y += a[i].Item3; }
        int n = Math.Max(1, to - from);
        return (l / n, x / n, y / n);
    }
}
