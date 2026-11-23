namespace NHL27Kit.Core;

/// <summary>A flat product shot of a jersey (RGBA, transparent round it), lined up with the template by its outline.</summary>
public sealed class FlatShot
{
    public byte[] Rgba { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    /// <summary>The outline's left and top, for lining the shot up with the template.</summary>
    public int Left { get; init; }
    public int Top { get; init; }

    /// <summary>Reads the outline; null when the image has no transparent surround to find it from.</summary>
    public static FlatShot From(byte[] rgba, int w, int h)
    {
        int left = w, top = h, right = -1, bottom = -1;
        long opaque = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (rgba[(y * w + x) * 4 + 3] > 128)
                {
                    opaque++;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
        if (right < 0 || opaque > (long)w * h * 95 / 100) return null;
        return new FlatShot { Rgba = rgba, Width = w, Height = h, Left = left, Top = top };
    }
}

/// <summary>What the flat shots gave: the art for the whole jersey, the colours found and the collar read off them.</summary>
public sealed record FlatResult(byte[] Art, IReadOnlyList<int> Palette, JerseyFlat.CollarRead Collar, byte[] CleanFront, byte[] CleanBack, byte[] PrintMask);

/// <summary>
/// Puts flat product shots of a jersey (a front and a back on the same template garment, as the league's shop shows them)
/// onto the model: the shading of the render comes off, colours move into the range the game's own jerseys use, and each
/// texel of the model takes its colour from the place in the shots that is the same place on the garment. So panels,
/// curved piping, the crest, patches and laces all come across, not only straight stripes.
/// </summary>
public static class JerseyFlat
{
    // ================================================================ the template

    // Measured on the template at 900 px across the 3240 px shot; offsets below are from the outline's corner on the
    // reference shots. Rows are (y, left, centre, right) of the body as it can be seen (not behind the sleeves).
    private const double Scale = 3240 / 900.0;
    private const int FrontLeft = 427, FrontTop = 86, BackLeft = 509, BackTop = 90;

    private sealed record View(
        (double Y, double L, double C, double R)[] Body,
        double HemY, double TopY,
        (double X, double R)[] Ring,
        (double X, double Y, double Half)[] ScreenLeft,
        (double X, double Y, double Half)[] ScreenRight,
        int RefLeft, int RefTop);

    private static readonly View Front = new(
        new (double, double, double, double)[]
        {
            (20, 300, 445, 590), (60, 300, 430, 590), (120, 222, 412, 655), (200, 222, 407, 598), (300, 235, 406, 605),
            (412, 258, 405, 615), (500, 258, 404, 637), (640, 252, 403, 645), (780, 248, 402, 650), (880, 248, 400, 655),
        },
        875, 25,
        new (double, double)[]
        {
            (248, -42), (260, -36), (290, -23), (320, -16), (350, -10), (380, -7), (410, -4), (440, -1), (470, 0), (500, 0),
            (530, -2), (560, -5), (590, -12), (620, -21), (655, -38),
        },
        new (double, double, double)[] { (175, 765, 47), (183, 722, 59), (199, 497, 59), (204, 403, 54), (214, 250, 54), (222, 118, 14) },
        new (double, double, double)[] { (707, 778, 59), (706, 738, 63), (704, 497, 68), (690, 401, 75), (670, 250, 58), (660, 110, 14) },
        FrontLeft, FrontTop);

    private static readonly View Back = new(
        new (double, double, double, double)[]
        {
            (20, 340, 450, 540), (50, 340, 440, 540), (120, 215, 412, 650), (200, 225, 410, 590), (390, 247, 400, 607),
            (500, 250, 392, 615), (640, 250, 375, 630), (780, 252, 362, 640), (880, 250, 350, 640),
        },
        876, 28,
        new (double, double)[]
        {
            (250, -50), (260, -42), (290, -20), (320, -13), (350, -7), (380, -4), (410, -1), (440, 0), (470, 0), (500, -3),
            (530, -9), (560, -20), (590, -42), (620, -71), (640, -92),
        },
        new (double, double, double)[] { (204, 746, 46), (200, 700, 52), (196, 491, 54), (198, 391, 49), (205, 250, 50), (218, 120, 14) },
        new (double, double, double)[] { (719, 775, 59), (712, 730, 62), (683, 494, 68), (676, 398, 69), (660, 250, 62), (650, 110, 14) },
        BackLeft, BackTop);

    // ================================================================ cleaning the shots

    /// <summary>
    /// The cloth colours of a jersey's two shots, largest first, and the shots with the render's shading taken off and the
    /// cloth flattened onto those colours (a crest's fine detail, which isn't near any of them, stays).
    /// </summary>
    public static (byte[] Front, byte[] Back, List<int> Palette) Clean(FlatShot front, FlatShot back)
    {
        const int Down = 4;
        var fs = Small(front, Down);
        var bs = Small(back, Down);
        int fn = fs.Lab.Length;
        var lab = new (double L, double A, double B)[fn + bs.Lab.Length];
        var solid = new bool[lab.Length];
        fs.Lab.CopyTo(lab, 0); bs.Lab.CopyTo(lab, fn);
        fs.Solid.CopyTo(solid, 0); bs.Solid.CopyTo(solid, fn);
        var (centers, large, main, label) = Cluster(lab, solid, new[] { (fs.W, fs.H, 0), (bs.W, bs.H, fn) });
        var f = CleanOne(front, fs, label, 0, centers, large, main, Down, Front);
        var b = CleanOne(back, bs, label, fn, centers, large, main, Down, Back);
        return (f, b, centers.Select(JerseyPhoto.Rgb).ToList());
    }

    private static (int W, int H, (double L, double A, double B)[] Lab, bool[] Solid) Small(FlatShot shot, int down)
    {
        int w = shot.Width / down, h = shot.Height / down;
        var lab = new (double L, double A, double B)[w * h];
        var solid = new bool[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < down; j++)
                    for (int i = 0; i < down; i++)
                    {
                        int o = ((y * down + j) * shot.Width + x * down + i) * 4;
                        r += shot.Rgba[o]; g += shot.Rgba[o + 1]; b += shot.Rgba[o + 2]; a += shot.Rgba[o + 3];
                    }
                double n = down * down;
                if (a / n > 250)
                {
                    solid[y * w + x] = true;
                    lab[y * w + x] = JerseyPhoto.Lab((byte)(r / n), (byte)(g / n), (byte)(b / n));
                }
            }
        });
        return (w, h, lab, solid);
    }

    private static byte[] CleanOne(FlatShot shot, (int W, int H, (double L, double A, double B)[] Lab, bool[] Solid) small, int[] labels, int offset,
        List<(double L, double A, double B)> centers, bool[] large, bool[] main, int down, View view)
    {
        int w = small.W, h = small.H;
        var lab = small.Lab;
        // how much lighter or darker than its colour each piece of cloth is: only where it's plainly cloth
        var ratio = new double[w * h];
        var weight = new double[w * h];
        Parallel.For(2, h - 2, y =>
        {
            for (int x = 2; x < w - 2; x++)
            {
                int i = y * w + x, l = labels[offset + i];
                if (l < 0 || !large[l] || centers[l].L < 30) continue;
                bool same = true;
                for (int dy = -2; dy <= 2 && same; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (labels[offset + i + dy * w + dx] != l) { same = false; break; }
                if (!same) continue;
                ratio[i] = lab[i].L / centers[l].L;
                weight[i] = 1;
            }
        });
        var shade = BlurRatio(ratio, weight, w, h, 10);

        var edges = new double[w * h];
        Parallel.For(2, h - 2, y =>
        {
            for (int x = 2; x < w - 2; x++)
            {
                int i = y * w + x;
                if (!small.Solid[i - 2] || !small.Solid[i + 2] || !small.Solid[i - 2 * w] || !small.Solid[i + 2 * w]) continue;
                edges[i] = JerseyPhoto.Delta(lab[i - 2], lab[i + 2]) + JerseyPhoto.Delta(lab[i - 2 * w], lab[i + 2 * w]);
            }
        });
        // full size: a small median takes off the weave
        var dst = new byte[shot.Rgba.Length];
        int W = shot.Width, H = shot.Height;
        var med = new byte[shot.Rgba.Length];
        Parallel.For(0, H, y =>
        {
            var r = new byte[9]; var g = new byte[9]; var b = new byte[9];
            for (int x = 0; x < W; x++)
            {
                int o = (y * W + x) * 4;
                med[o + 3] = shot.Rgba[o + 3];
                if (shot.Rgba[o + 3] == 0) continue;
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int q = (Math.Clamp(y + dy, 0, H - 1) * W + Math.Clamp(x + dx, 0, W - 1)) * 4;
                        r[k] = shot.Rgba[q]; g[k] = shot.Rgba[q + 1]; b[k] = shot.Rgba[q + 2]; k++;
                    }
                Array.Sort(r); Array.Sort(g); Array.Sort(b);
                med[o] = r[4]; med[o + 1] = g[4]; med[o + 2] = b[4];
            }
        });
        // then the shading comes off, and cloth goes onto its colour; an edge (of a letter, of a stripe) keeps its smoothing
        Parallel.For(0, H, y =>
        {
            for (int x = 0; x < W; x++)
            {
                int o = (y * W + x) * 4;
                dst[o + 3] = med[o + 3];
                if (med[o + 3] == 0) continue;
                var c = JerseyPhoto.Lab(med[o], med[o + 1], med[o + 2]);
                double s = Bilinear(shade, w, h, (x + 0.5) / down - 0.5, (y + 0.5) / down - 0.5);
                // chroma follows the light less than brightness does: only half the correction, or a shadow goes neon
                double sc = Math.Sqrt(s);
                c = (c.L / s, c.A / sc, c.B / sc);
                bool edge = Bilinear(edges, w, h, (x + 0.5) / down - 0.5, (y + 0.5) / down - 0.5) > 16;
                double tx = (x - shot.Left + view.RefLeft) / Scale, ty = (y - shot.Top + view.RefTop) / Scale;
                bool onPrint = ReferenceEquals(view, Front) && tx >= 235 && tx <= 640 && ty >= ZoneTop - 5 && ty <= ZoneBottom + 60;
                var (near, t) = edge && onPrint ? (c, 0.0) : Snap(c, centers, main, onPrint ? 28 : 48);
                var f = (L: c.L + (near.L - c.L) * t, A: c.A + (near.A - c.A) * t, B: c.B + (near.B - c.B) * t);
                int rgb = JerseyPhoto.Rgb(f);
                dst[o] = (byte)(rgb >> 16); dst[o + 1] = (byte)(rgb >> 8); dst[o + 2] = (byte)rgb;
            }
        });
        return dst;
    }

    /// <summary>
    /// The cloth colour a shading-corrected colour belongs to and how fully (1 = it's that colour). On the colour when close,
    /// fading out so a crest's own shades aren't cut into steps. The render's sheen and folds keep a cloth's hue and move its
    /// brightness, so the nearest colour of that hue takes it as well.
    /// </summary>
    private static ((double L, double A, double B) Near, double T) Snap((double L, double A, double B) c, List<(double L, double A, double B)> centers, bool[] main, double reach)
    {
        double best = double.MaxValue;
        (double L, double A, double B) near = c;
        foreach (var p in centers)
        {
            double d = JerseyPhoto.Delta(c, p);
            if (d < best) { best = d; near = p; }
        }
        double full = near.L < 32 ? 16 : 9, none = near.L < 32 ? 24 : 15;
        double t = Math.Clamp((none - best) / (none - full), 0, 1);
        if (t >= 1) return (near, 1);
        double cc = Math.Sqrt(c.A * c.A + c.B * c.B), closest = double.MaxValue;
        (double L, double A, double B)? hue = null;
        for (int i = 0; i < centers.Count; i++)
        {
            var p = centers[i];
            double pc = Math.Sqrt(p.A * p.A + p.B * p.B), dl = Math.Abs(p.L - c.L);
            if (dl >= (main[i] ? reach : 10) || dl >= closest) continue;
            bool same = pc < 12 ? cc < 12 : cc >= pc * 0.4 && cc <= pc * 1.6 && (c.A * p.A + c.B * p.B) / Math.Max(1, cc * pc) >= 0.96;
            if (same) { closest = dl; hue = p; }
        }
        return hue is { } h ? (h, 1) : (near, t);
    }

    /// <summary>
    /// k-means on colour (brightness counting half, as lighting moves it most), near centres merged, then colours that are
    /// only the thin edges between two others dropped. Largest first; the label of each pixel, -1 for none; which are big
    /// enough to judge shading by.
    /// </summary>
    private static (List<(double L, double A, double B)> Centers, bool[] Large, bool[] Main, int[] Label) Cluster((double L, double A, double B)[] lab, bool[] solid, (int W, int H, int Offset)[] images)
    {
        const int K = 16;
        var idx = Enumerable.Range(0, lab.Length).Where(i => solid[i]).ToArray();
        static double D((double L, double A, double B) a, (double L, double A, double B) b)
        {
            double dl = (a.L - b.L) * 0.5, da = a.A - b.A, db = a.B - b.B;
            return dl * dl + da * da + db * db;
        }
        // seeds as far from each other as can be, so a small colour still gets one
        var centers = new List<(double L, double A, double B)> { lab[idx[idx.Length / 2]] };
        var nearest = idx.Select(i => D(lab[i], centers[0])).ToArray();
        while (centers.Count < K)
        {
            int far = 0;
            for (int j = 1; j < idx.Length; j++) if (nearest[j] > nearest[far]) far = j;
            if (nearest[far] < 25) break;
            var c = lab[idx[far]];
            centers.Add(c);
            for (int j = 0; j < idx.Length; j++) nearest[j] = Math.Min(nearest[j], D(lab[idx[j]], c));
        }
        var label = new int[lab.Length];
        Array.Fill(label, -1);
        for (int round = 0; round < 10; round++)
        {
            var sum = new (double L, double A, double B, long N)[centers.Count];
            foreach (int i in idx)
            {
                int best = 0;
                double bd = double.MaxValue;
                for (int c = 0; c < centers.Count; c++) { double d = D(lab[i], centers[c]); if (d < bd) { bd = d; best = c; } }
                label[i] = best;
                sum[best] = (sum[best].L + lab[i].L, sum[best].A + lab[i].A, sum[best].B + lab[i].B, sum[best].N + 1);
            }
            for (int c = 0; c < centers.Count; c++)
                if (sum[c].N > 0) centers[c] = (sum[c].L / sum[c].N, sum[c].A / sum[c].N, sum[c].B / sum[c].N);
        }
        // merge what's one colour under the lighting: closest pair first, so a ramp of shades can't chain into one colour
        var counts = new long[centers.Count];
        foreach (int i in idx) counts[label[i]]++;
        var parent = Enumerable.Range(0, centers.Count).ToArray();
        var live = Enumerable.Range(0, centers.Count).Where(c => counts[c] > 0).ToList();
        var mean = centers.ToArray();
        var weight = counts.ToArray();
        while (live.Count > 1)
        {
            int pa = -1, pb = -1;
            double close = double.MaxValue;
            foreach (int a in live)
                foreach (int b in live)
                    if (a < b && D(mean[a], mean[b]) < close) { close = D(mean[a], mean[b]); pa = a; pb = b; }
            if (Math.Sqrt(close) >= 10) break;
            double n = weight[pa] + weight[pb];
            mean[pa] = ((mean[pa].L * weight[pa] + mean[pb].L * weight[pb]) / n, (mean[pa].A * weight[pa] + mean[pb].A * weight[pb]) / n, (mean[pa].B * weight[pa] + mean[pb].B * weight[pb]) / n);
            weight[pa] += weight[pb];
            parent[pb] = pa;
            live.Remove(pb);
        }
        int Root(int a) { while (parent[a] != a) a = parent[a]; return a; }
        for (int c = 0; c < centers.Count; c++) parent[c] = Root(c);
        for (int i = 0; i < label.Length; i++) if (label[i] >= 0) label[i] = parent[label[i]];

        // how much of each colour has room around it: an edge between two colours has none, a shadow has little
        var inner = new long[centers.Count];
        foreach (var (w, h, off) in images)
            for (int y = 2; y < h - 2; y++)
                for (int x = 2; x < w - 2; x++)
                {
                    int i = off + y * w + x, l = label[i];
                    if (l < 0) continue;
                    bool same = true;
                    for (int dy = -2; dy <= 2 && same; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            if (label[i + dy * w + dx] != l) { same = false; break; }
                    if (same) inner[l]++;
                }
        var totals = new long[centers.Count];
        foreach (int i in idx) totals[label[i]]++;
        long all = idx.Length;
        var kept = Enumerable.Range(0, centers.Count)
            .Where(c => totals[c] >= all * 0.002 && inner[c] >= totals[c] * 0.2)
            .OrderByDescending(c => totals[c]).ToList();
        var result = new List<(double L, double A, double B)>();
        foreach (int c in kept)
        {
            double l = 0, a = 0, b = 0;
            foreach (int i in idx) if (label[i] == c) { l += lab[i].L; a += lab[i].A; b += lab[i].B; }
            result.Add((l / totals[c], a / totals[c], b / totals[c]));
        }
        var large = kept.Select(c => totals[c] >= all * 0.015).ToArray();
        // a shadow or a sheen isn't a colour of its own. A printed colour meets its neighbour in a sharp edge; light fades.
        double Softness(int qc, int pc, double dl)
        {
            double sum = 0;
            long n = 0;
            const int Reach = 3;
            foreach (var (w, h, off) in images)
                for (int y = Reach; y < h - Reach; y += 2)
                    for (int x = Reach; x < w - Reach; x += 2)
                    {
                        int i = off + y * w + x;
                        if (label[i] != qc) continue;
                        if (label[i - Reach] == pc || label[i + Reach] == pc)
                        {
                            sum += Math.Abs(lab[i + Reach].L - lab[i - Reach].L);
                            n++;
                        }
                        if (label[i - Reach * w] == pc || label[i + Reach * w] == pc)
                        {
                            sum += Math.Abs(lab[i + Reach * w].L - lab[i - Reach * w].L);
                            n++;
                        }
                    }
            // no border with it at all says nothing: it's its own colour
            return n < 8 ? double.MaxValue : sum / n / Math.Max(1, dl);
        }
        var into = new int[result.Count];
        Array.Fill(into, -1);
        for (int i = 1; i < result.Count; i++)
        {
            var q = result[i];
            double qc = Math.Sqrt(q.A * q.A + q.B * q.B);
            for (int j = 0; j < i; j++)
            {
                var p = result[j];
                double pc = Math.Sqrt(p.A * p.A + p.B * p.B), dl = Math.Abs(p.L - q.L);
                if (into[j] >= 0 || totals[kept[j]] < totals[kept[i]] * 2 || dl > 60 || dl < 1) continue;
                bool neutral = pc < 12 && qc < 12;
                bool sameHue = neutral || pc >= 12 && qc >= 4 && (q.A * p.A + q.B * p.B) / (qc * pc) > 0.94;
                if (!sameHue) continue;
                if (!neutral && q.L < p.L && totals[kept[i]] < all * 0.03) { into[i] = j; break; }
                if (Softness(kept[i], kept[j], dl) < (neutral ? 0.45 : 0.8)) { into[i] = j; break; }
            }
        }
        bool Band(int c)
        {
            const int Run = 70, Rows = 4;
            foreach (var (w, h, off) in images)
            {
                int rows = 0;
                for (int y = 0; y < h; y++)
                {
                    int run = 0, longest = 0;
                    for (int x = 0; x < w; x++)
                    {
                        run = label[off + y * w + x] == c ? run + 1 : 0;
                        if (run > longest) longest = run;
                    }
                    if (longest >= Run) rows++;
                }
                if (rows >= Rows) return true;
            }
            return false;
        }
        // Where this colour meets a lighter one of the same hue: does it step there, or fade?
        bool Fades(int c, int lighter)
        {
            int sharp = 0, soft = 0;
            double step = 0;
            foreach (var (w, h, off) in images)
            {
                for (int y = 2; y < h - 2; y++)
                    for (int x = 2; x < w - 2; x++)
                    {
                        int at = off + y * w + x;
                        if (label[at] != c) continue;
                        foreach (int d in new[] { 1, -1, w, -w })
                        {
                            if (label[at + d] != lighter) continue;
                            double across = Math.Abs(lab[at + d].L - lab[at - d].L), whole = Math.Abs(lab[at + 2 * d].L - lab[at - 2 * d].L);
                            step += whole;
                            if (across >= whole * 0.75 && whole >= 8) sharp++; else soft++;
                        }
                    }
            }
            return sharp + soft >= 40 && sharp < (sharp + soft) * 0.35;
        }
        bool Shading(int i)
        {
            var q = result[i];
            double qc = Math.Sqrt(q.A * q.A + q.B * q.B);
            for (int j = 0; j < result.Count; j++)
            {
                if (j == i || into[j] != -1 || result[j].L <= q.L) continue;
                var p = result[j];
                double pc = Math.Sqrt(p.A * p.A + p.B * p.B);
                bool same = pc < 12 ? qc < 12 : qc >= 4 && (q.A * p.A + q.B * p.B) / (qc * pc) > 0.94;
                if (same && Fades(kept[i], kept[j])) return true;
            }
            return false;
        }
        for (int i = 1; i < result.Count; i++)
        {
            if (into[i] != -1 || totals[kept[i]] >= all * 0.08) continue;
            var q = result[i];
            double qc = Math.Sqrt(q.A * q.A + q.B * q.B);
            bool shares = false;
            for (int j = 0; j < i && !shares; j++)
            {
                var p = result[j];
                double pc = Math.Sqrt(p.A * p.A + p.B * p.B);
                shares = pc < 12 ? qc < 12 : qc >= 4 && (q.A * p.A + q.B * p.B) / (qc * pc) > 0.94;
            }
            if (shares && (!Band(kept[i]) || Shading(i))) into[i] = -2;
        }
        var keep = Enumerable.Range(0, result.Count).Where(i => into[i] == -1).ToList();
        var map = new int[centers.Count];
        Array.Fill(map, -1);
        for (int k = 0; k < keep.Count; k++) map[kept[keep[k]]] = k;
        // what was only light counts as the cloth it's on, so the shading map sees it for what it is
        for (int i = 0; i < result.Count; i++) if (into[i] >= 0) map[kept[i]] = keep.IndexOf(into[i]);
        for (int i = 0; i < label.Length; i++) if (label[i] >= 0) label[i] = map[label[i]];
        return (keep.Select(k => result[k]).ToList(), keep.Select(k => large[k]).ToArray(), keep.Select(k => totals[kept[k]] >= all * 0.06).ToArray(), label);
    }

    /// <summary>The weighted average of <paramref name="v"/> in a box of the radius (twice over, nearly a Gaussian); 1 where there's nothing to go on.</summary>
    private static double[] BlurRatio(double[] v, double[] weight, int w, int h, int radius)
    {
        var a = (double[])v.Clone();
        var b = (double[])weight.Clone();
        for (int pass = 0; pass < 2; pass++)
        {
            a = BoxBlur(a, w, h, radius);
            b = BoxBlur(b, w, h, radius);
        }
        var o = new double[w * h];
        for (int i = 0; i < o.Length; i++) o[i] = b[i] > 0.02 ? Math.Clamp(a[i] / b[i], 0.45, 1.7) : 1;
        return o;
    }

    private static double[] BoxBlur(double[] v, int w, int h, int r)
    {
        var t = new double[w * h];
        Parallel.For(0, h, y =>
        {
            double sum = 0;
            for (int x = -r; x <= r; x++) sum += v[y * w + Math.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                t[y * w + x] = sum / (2 * r + 1);
                sum += v[y * w + Math.Min(x + r + 1, w - 1)] - v[y * w + Math.Max(x - r, 0)];
            }
        });
        var o = new double[w * h];
        Parallel.For(0, w, x =>
        {
            double sum = 0;
            for (int y = -r; y <= r; y++) sum += t[Math.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                o[y * w + x] = sum / (2 * r + 1);
                sum += t[Math.Min(y + r + 1, h - 1) * w + x] - t[Math.Max(y - r, 0) * w + x];
            }
        });
        return o;
    }

    private static double Bilinear(double[] v, int w, int h, double x, double y)
    {
        x = Math.Clamp(x, 0, w - 1.001); y = Math.Clamp(y, 0, h - 1.001);
        int x0 = (int)x, y0 = (int)y;
        double fx = x - x0, fy = y - y0;
        int i = y0 * w + x0;
        return (v[i] * (1 - fx) + v[i + 1] * fx) * (1 - fy) + (v[i + w] * (1 - fx) + v[i + w + 1] * fx) * fy;
    }

    // ================================================================ the collar in the shots

    // Everything of the collar in the shots (wings, tab, rim and the inside of the neck), which goes on the model's own
    // collar pieces instead of being projected, and the maker's mark on the back of the neck, in template units.
    private static readonly (double X, double Y)[] FrontCollar =
    {
        (320, 0), (320, 50), (321, 80), (333, 111), (351, 130), (380, 152), (408, 165), (451, 150), (498, 127), (554, 92), (573, 58), (565, 0),
    };
    private static readonly (double X, double Y)[] BackCollar = { (330, 0), (330, 57), (400, 64), (480, 60), (552, 52), (552, 0) };
    private static readonly (double X, double Y)[] BackHemTag = { (570, 790), (622, 790), (622, 845), (570, 845) };
    private static readonly (double X, double Y)[] BackMark = { (380, 68), (447, 68), (447, 122), (380, 122) };

    /// <summary>A copy of a cleaned shot with these pieces gone: each takes the colour of the nearest cloth round it.</summary>
    private static byte[] WithoutPieces(byte[] rgba, FlatShot shot, View v, params (double X, double Y)[][] pieces)
    {
        int w = shot.Width, h = shot.Height;
        var mask = new bool[w * h];
        double ox = shot.Left - v.RefLeft, oy = shot.Top - v.RefTop;
        int minY = h, maxY = 0;
        foreach (var poly in pieces)
        {
            var pts = poly.Select(p => (X: p.X * Scale + ox, Y: p.Y * Scale + oy)).ToArray();
            int y0 = Math.Max(0, (int)pts.Min(p => p.Y)), y1 = Math.Min(h - 1, (int)pts.Max(p => p.Y));
            minY = Math.Min(minY, y0); maxY = Math.Max(maxY, y1);
            for (int y = y0; y <= y1; y++)
            {
                var xs = new List<double>();
                for (int k = 0; k < pts.Length; k++)
                {
                    var a = pts[k]; var b = pts[(k + 1) % pts.Length];
                    if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y)) xs.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int x = Math.Max(0, (int)xs[k]); x <= Math.Min(w - 1, (int)xs[k + 1]); x++) mask[y * w + x] = true;
            }
        }
        return Fill(rgba, w, h, mask, minY, maxY);
    }

    /// <summary>A copy with the masked pixels (between those heights) in the colour of the nearest cloth: two sweeps, plenty for flat cloth over these distances.</summary>
    private static byte[] Fill(byte[] rgba, int w, int h, bool[] mask, int minY, int maxY)
    {
        var o = (byte[])rgba.Clone();
        int top = Math.Max(0, minY - 4), bottom = Math.Min(h - 1, maxY + 4);
        var from = new int[w * h];
        var dist = new double[w * h];
        for (int y = top; y <= bottom; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool cloth = !mask[i] && rgba[i * 4 + 3] > 200;
                from[i] = cloth ? i : -1;
                dist[i] = cloth ? 0 : double.MaxValue;
            }
        void Try(int i, int j, double step)
        {
            if (from[j] >= 0 && dist[j] + step < dist[i]) { dist[i] = dist[j] + step; from[i] = from[j]; }
        }
        for (int y = top; y <= bottom; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                Try(i, i - 1, 1);
                if (y > top) { Try(i, i - w, 1); Try(i, i - w - 1, 1.41); Try(i, i - w + 1, 1.41); }
            }
        for (int y = bottom; y >= top; y--)
            for (int x = w - 2; x >= 1; x--)
            {
                int i = y * w + x;
                Try(i, i + 1, 1);
                if (y < bottom) { Try(i, i + w, 1); Try(i, i + w + 1, 1.41); Try(i, i + w - 1, 1.41); }
            }
        for (int y = top; y <= bottom; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!mask[i] || from[i] < 0) continue;
                int q = from[i] * 4;
                o[i * 4] = rgba[q]; o[i * 4 + 1] = rgba[q + 1]; o[i * 4 + 2] = rgba[q + 2]; o[i * 4 + 3] = 255;
            }
        return o;
    }

    // Where shoulder patches sit in the shots, in template units: (left, top, right, bottom).
    private static readonly (double X0, double Y0, double X1, double Y1)[] FrontShoulders = { (185, 20, 335, 175), (525, 20, 700, 175) };
    private static readonly (double X0, double Y0, double X1, double Y1)[] BackShoulders = { (190, 20, 340, 150), (540, 20, 690, 150) };

    /// <summary>
    /// A copy of a cleaned shot without its shoulder patches, which are left to be added by hand: whatever stands out from
    /// the cloth in those places and is whole inside them goes. Something that runs out of them (a panel, a line of piping,
    /// the peaks on a sleeve) is the jersey's design and stays.
    /// </summary>
    private static byte[] WithoutPatches(byte[] rgba, FlatShot shot, View v, (double X0, double Y0, double X1, double Y1)[] places)
    {
        int w = shot.Width, h = shot.Height;
        var mask = new bool[w * h];
        int ox = shot.Left - v.RefLeft, oy = shot.Top - v.RefTop;
        int minY = h, maxY = 0;
        foreach (var (X0, Y0, X1, Y1) in places)
        {
            int x0 = Math.Max(1, (int)(X0 * Scale) + ox), y0 = Math.Max(1, (int)(Y0 * Scale) + oy);
            int x1 = Math.Min(w - 2, (int)(X1 * Scale) + ox), y1 = Math.Min(h - 2, (int)(Y1 * Scale) + oy);
            var r = new List<byte>(); var g = new List<byte>(); var b = new List<byte>();
            for (int y = y0; y <= y1; y += 4)
                for (int x = x0; x <= x1; x += 4)
                {
                    int o = (y * w + x) * 4;
                    if (rgba[o + 3] < 200) continue;
                    r.Add(rgba[o]); g.Add(rgba[o + 1]); b.Add(rgba[o + 2]);
                }
            if (r.Count == 0) continue;
            r.Sort(); g.Sort(); b.Sort();
            int mr = r[r.Count / 2], mg = g[g.Count / 2], mb = b[b.Count / 2];
            bool Odd(int i)
            {
                int o = i * 4;
                const int Rim = 12;
                if (i - Rim * w < 0 || i + Rim * w >= w * h) return false;
                if (rgba[(i - Rim * w) * 4 + 3] < 200 || rgba[(i + Rim * w) * 4 + 3] < 200 || rgba[(i - Rim) * 4 + 3] < 200 || rgba[(i + Rim) * 4 + 3] < 200) return false;
                return rgba[o + 3] >= 200 && Math.Abs(rgba[o] - mr) + Math.Abs(rgba[o + 1] - mg) + Math.Abs(rgba[o + 2] - mb) > 45;
            }
            var seen = new bool[w * h];
            var queue = new Queue<int>();
            var piece = new List<int>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int start = y * w + x;
                    if (seen[start] || !Odd(start)) continue;
                    piece.Clear();
                    bool whole = true;
                    seen[start] = true;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int i = queue.Dequeue(), ix = i % w, iy = i / w;
                        piece.Add(i);
                        if (ix <= x0 || ix >= x1 || iy <= y0 || iy >= y1) { whole = false; continue; }
                        foreach (int j in new[] { i - 1, i + 1, i - w, i + w })
                            if (!seen[j] && Odd(j)) { seen[j] = true; queue.Enqueue(j); }
                    }
                    if (!whole) continue;
                    foreach (int i in piece) mask[i] = true;
                }
            // a patch's soft edge and thin border go with it
            const int Grow = 16;
            var grown = (bool[])mask.Clone();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!mask[y * w + x]) continue;
                    for (int dy = -Grow; dy <= Grow; dy += 2)
                        for (int dx = -Grow; dx <= Grow; dx += 2)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h) grown[ny * w + nx] = true;
                        }
                }
            mask = grown;
            minY = Math.Min(minY, y0 - Grow);
            maxY = Math.Max(maxY, y1 + Grow);
        }
        return minY > maxY ? rgba : Fill(rgba, w, h, mask, minY, maxY);
    }

    /// <summary>The colours of the collar's parts, read at the places the template has them.</summary>
    public sealed record CollarRead(int Wing, int Tab, int Rim, int InsideBack, int BackBand, int BackRim);

    public static CollarRead ReadCollar(FlatShot front, byte[] f, FlatShot back, byte[] b)
    {
        int At(FlatShot s, byte[] rgba, View v, params (double X, double Y)[] points)
        {
            var r = new List<byte>(); var g = new List<byte>(); var bl = new List<byte>();
            foreach (var (X, Y) in points)
            {
                int cx = (int)(X * Scale + s.Left - v.RefLeft), cy = (int)(Y * Scale + s.Top - v.RefTop);
                for (int dy = -4; dy <= 4; dy += 2)
                    for (int dx = -4; dx <= 4; dx += 2)
                    {
                        int o = (Math.Clamp(cy + dy, 0, s.Height - 1) * s.Width + Math.Clamp(cx + dx, 0, s.Width - 1)) * 4;
                        if (rgba[o + 3] < 200) continue;
                        r.Add(rgba[o]); g.Add(rgba[o + 1]); bl.Add(rgba[o + 2]);
                    }
            }
            if (r.Count == 0) return 0x808080;
            r.Sort(); g.Sort(); bl.Sort();
            return r[r.Count / 2] << 16 | g[g.Count / 2] << 8 | bl[bl.Count / 2];
        }
        return new CollarRead(
            At(front, f, Front, (340, 85), (538, 72), (478, 112), (365, 123)),
            At(front, f, Front, (387, 128), (431, 128), (408, 155)),
            At(front, f, Front, (354, 60), (535, 47)),
            At(front, f, Front, (395, 38), (510, 38)),
            At(back, b, Back, (440, 41), (380, 47), (500, 42)),
            At(back, b, Back, (440, 29), (400, 29), (490, 29)));
    }

    /// <summary>Paints the read colours on the model's collar pieces; the rim's piping is the part of the ring by the neck.</summary>
    private static void PaintCollar(byte[] art, CollarMap map, CollarRead c, JerseyShape shape)
    {
        const double Piping = 0.62;
        void Set(int i, int rgb) { int o = i * 4; art[o] = (byte)(rgb >> 16); art[o + 1] = (byte)(rgb >> 8); art[o + 2] = (byte)rgb; }
        for (int i = 0; i < map.Part.Length; i++)
        {
            if (map.Part[i] == CollarPart.None || shape.Map.Position[i * 3 + 1] < shape.PitY) continue;
            switch (map.Part[i])
            {
                case CollarPart.Front: case CollarPart.Sides: Set(i, c.Wing); break;
                case CollarPart.Insert: Set(i, c.Tab); break;
                case CollarPart.RingFront: Set(i, map.Up[i] >= Piping ? c.Rim : c.Wing); break;
                case CollarPart.RingBack: Set(i, map.Up[i] >= Piping ? c.BackRim : c.BackBand); break;
                case CollarPart.NeckFront: Set(i, c.Rim); break;
                case CollarPart.NeckBack: Set(i, c.InsideBack); break;
            }
        }
    }

    // ================================================================ onto the model

    /// <summary>The whole conversion: the art for the model (size×size RGBA, the base's alpha kept), and what was read.</summary>
    public static FlatResult Convert(StudioSource src, FlatShot front, FlatShot back)
    {
        var (fc, bc, palette) = Clean(front, back);
        var collar = ReadCollar(front, fc, back, bc);
        var fm = WithoutPatches(WithoutPieces(fc, front, Front, FrontCollar), front, Front, FrontShoulders);
        var bm = WithoutPatches(WithoutPieces(bc, back, Back, BackCollar, BackMark, BackHemTag), back, Back, BackShoulders);
        var art = Project(src.Shape, src.Base, front, back, Soften(fm, front), Soften(bm, back), src.Collar, out var printMask);
        PaintBackMark(src.Shape, art, src.Collar, back, bc);
        PaintCollar(art, src.Collar, collar, src.Shape);
        return new FlatResult(art, palette, collar, fc, bc, printMask);
    }

    /// <summary>
    /// Lettering colours for a jersey's palette (its biggest colour first): the fill stands out most from the cloth, the
    /// outline is whatever stands apart from both.
    /// </summary>
    public static (int Fill, int Outline) LetterColors(IReadOnlyList<int> palette)
    {
        static double Apart(int a, int b)
        {
            int dr = (a >> 16 & 255) - (b >> 16 & 255), dg = (a >> 8 & 255) - (b >> 8 & 255), db = (a & 255) - (b & 255);
            return Math.Sqrt(0.3 * dr * dr + 0.59 * dg * dg + 0.11 * db * db);
        }
        int body = palette[0];
        if (palette.Count < 2) return (Apart(body, 0xFFFFFF) > Apart(body, 0) ? 0xFFFFFF : 0x101010, body);
        int fill = palette.Skip(1).MaxBy(c => Apart(c, body));
        int outline = palette.Count > 2 ? palette.Skip(1).Where(c => c != fill).MaxBy(c => Math.Min(Apart(c, body), Apart(c, fill))) : body;
        return (fill, outline);
    }

    /// <summary>
    /// A kit's font sheet (RGBA, glyphs in the alpha) in new colours: the sheet's main colour becomes the fill and its others
    /// the outline, with soft edges between them kept.
    /// </summary>
    public static byte[] RecolorFont(byte[] font, int fill, int outline)
    {
        var pts = new List<(double R, double G, double B)>();
        for (int i = 0; i < font.Length; i += 4 * 7)
            if (font[i + 3] > 200) pts.Add((font[i], font[i + 1], font[i + 2]));
        var o = (byte[])font.Clone();
        if (pts.Count == 0) return o;
        // up to three colours: start from the extremes of brightness and the middle
        var ordered = pts.OrderBy(p => p.R + p.G + p.B).ToList();
        var c = new[] { ordered[0], ordered[ordered.Count / 2], ordered[^1] };
        var n = new int[3];
        static double D((double R, double G, double B) a, (double R, double G, double B) b) => (a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B);
        for (int it = 0; it < 12; it++)
        {
            var sum = new (double R, double G, double B)[3];
            Array.Clear(n);
            foreach (var p in pts)
            {
                int k = D(p, c[0]) <= D(p, c[1]) ? 0 : 1;
                if (D(p, c[2]) < D(p, c[k])) k = 2;
                n[k]++;
                sum[k] = (sum[k].R + p.R, sum[k].G + p.G, sum[k].B + p.B);
            }
            for (int k = 0; k < 3; k++)
                if (n[k] > 0) c[k] = (sum[k].R / n[k], sum[k].G / n[k], sum[k].B / n[k]);
        }
        int main = Array.IndexOf(n, n.Max());
        double[] to(int rgb) => new double[] { rgb >> 16 & 255, rgb >> 8 & 255, rgb & 255 };
        double[] fillC = to(fill), outC = to(outline);
        Parallel.For(0, font.Length / 4, j =>
        {
            int i = j * 4;
            if (font[i + 3] == 0) return;
            var p = ((double)font[i], (double)font[i + 1], (double)font[i + 2]);
            double main_w = 0, other_w = 0;
            for (int k = 0; k < 3; k++)
            {
                if (n[k] == 0) continue;
                double w = 1 / Math.Pow(D(p, c[k]) + 1, 2);
                if (k == main) main_w += w; else other_w += w;
            }
            double t = main_w / (main_w + other_w);
            for (int ch = 0; ch < 3; ch++) o[i + ch] = (byte)Math.Round(fillC[ch] * t + outC[ch] * (1 - t));
        });
        return o;
    }

    /// <summary>The mark on the back of the neck goes in the middle of the model's panel there, in proportion.</summary>
    private static void PaintBackMark(JerseyShape shape, byte[] art, CollarMap map, FlatShot back, byte[] rgba)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        var pos = shape.Map.Position;
        for (int i = 0; i < map.Part.Length; i++)
            if (map.Part[i] == CollarPart.BackPatch && pos[i * 3 + 1] > shape.PitY)
            {
                minX = Math.Min(minX, pos[i * 3]); maxX = Math.Max(maxX, pos[i * 3]);
                minY = Math.Min(minY, pos[i * 3 + 1]); maxY = Math.Max(maxY, pos[i * 3 + 1]);
            }
        if (maxX <= minX) return;
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        // the mark, its surround's colour across the rest of the panel, sized by its height on the panel
        double perUnit = (maxY - minY) * 0.8 / (BackMark[2].Y - BackMark[0].Y);
        double mx = (BackMark[0].X + BackMark[1].X) / 2, my = (BackMark[0].Y + BackMark[2].Y) / 2;
        for (int i = 0; i < map.Part.Length; i++)
        {
            if (map.Part[i] != CollarPart.BackPatch || pos[i * 3 + 1] <= shape.PitY) continue;
            // seen from behind: the model's +x is on the left of the shot
            double u = mx - (pos[i * 3] - cx) / perUnit, v = my - (pos[i * 3 + 1] - cy) / perUnit;
            u = Math.Clamp(u, BackMark[0].X, BackMark[1].X);
            v = Math.Clamp(v, BackMark[0].Y, BackMark[2].Y);
            var col = SampleSolid(rgba, back.Width, back.Height, u * Scale + back.Left - BackLeft, v * Scale + back.Top - BackTop);
            int o = i * 4;
            art[o] = col.R; art[o + 1] = col.G; art[o + 2] = col.B;
        }
    }

    // The crest's part of the shots (top of the laces to under a big crest): kept in proportion on a short-bodied model.
    private const double ZoneTop = 230, ZoneBottom = 560, TabY = 160, BodyHalf = 190;
    // The print on the chest is seen straight on and keeps its shape: this much of the size the model's width would give
    // it, both ways. The model is short for its width, so all of that size would leave nothing for the hem.
    private const double PrintSize = 0.8;

    /// <summary>
    /// The print on the chest: where its middle is across the shot (it isn't always on the shot's middle line) and the cloth
    /// colour round it. The middle is NaN when there's no print.
    /// </summary>
    private static (double Left, double Right, (byte R, byte G, byte B) Cloth) FindPrint(View view, FlatShot shot, byte[] rgba)
    {
        int ox = shot.Left - view.RefLeft, oy = shot.Top - view.RefTop;
        var samples = new List<(double X, double Y, (byte R, byte G, byte B) C)>();
        for (double y = ZoneTop; y <= ZoneBottom; y += 2)
        {
            var (l, _, r) = Row(view, y);
            for (double x = l + 8; x <= r - 8; x += 1)
                samples.Add((x, y, SampleSolid(rgba, shot.Width, shot.Height, x * Scale + ox, y * Scale + oy)));
        }
        byte Mid(Func<(byte R, byte G, byte B), byte> f) => samples.Select(c => f(c.C)).OrderBy(v => v).ElementAt(samples.Count / 2);
        var cloth = (R: Mid(c => c.R), G: Mid(c => c.G), B: Mid(c => c.B));
        var hits = new Dictionary<int, int>();
        foreach (var (x, _, c) in samples)
            if (Math.Abs(c.R - cloth.R) + Math.Abs(c.G - cloth.G) + Math.Abs(c.B - cloth.B) > 60)
                hits[(int)x] = hits.GetValueOrDefault((int)x) + 1;
        // the print is what's joined up across the middle of the chest; creases by the sleeves further out aren't part of it
        var (_, mid, _) = Row(view, (ZoneTop + ZoneBottom) / 2);
        var columns = hits.Where(kv => kv.Value >= 6).Select(kv => kv.Key).ToHashSet();
        const int Gap = 10;
        int Reach(int from, int step)
        {
            int last = from, x = from;
            while (x - last is <= Gap and >= -Gap && x > 0 && x < 900)
            {
                if (columns.Contains(x)) last = x;
                x += step;
            }
            return last;
        }
        // start from what's marked nearest the middle line
        if (columns.Count < 20) return (double.NaN, double.NaN, cloth);
        int start = columns.MinBy(x => Math.Abs(x - mid));
        if (Math.Abs(start - mid) > 80) return (double.NaN, double.NaN, cloth);
        return (Reach(start, -1), Reach(start, 1), cloth);
    }

    /// <summary>How far down the print runs: past the usual zone there are rows with marks in its width and clear cloth at the sides.</summary>
    private static double PrintEnd(FlatShot shot, byte[] rgba, double left, double right)
    {
        if (double.IsNaN(left)) return ZoneBottom;
        const double Most = ZoneBottom + 110;
        double ox = shot.Left - Front.RefLeft, oy = shot.Top - Front.RefTop, end = ZoneBottom;
        int misses = 0;
        for (double y = ZoneBottom; y < Most && misses < 3; y += 1)
        {
            var (l, _, r) = Row(Front, y);
            var row = new List<(byte, byte, byte)>();
            for (double x = l + 30; x <= r - 30; x += 3) row.Add(SampleSolid(rgba, shot.Width, shot.Height, x * Scale + ox, y * Scale + oy));
            int m = Across(row).Color;
            (byte R, byte G, byte B) cloth = ((byte)(m >> 16), (byte)(m >> 8), (byte)m);
            bool Off(double x)
            {
                var c = SampleSolid(rgba, shot.Width, shot.Height, x * Scale + ox, y * Scale + oy);
                return Math.Abs(c.R - cloth.R) + Math.Abs(c.G - cloth.G) + Math.Abs(c.B - cloth.B) > 60;
            }
            int inside = 0, all = 0, outside = 0, sides = 0;
            for (double x = left; x <= right; x += 2) { all++; if (Off(x)) inside++; }
            for (double x = l + 20; x < left - 10; x += 4) { sides++; if (Off(x)) outside++; }
            for (double x = right + 10; x <= r - 20; x += 4) { sides++; if (Off(x)) outside++; }
            bool mark = inside >= 2 && inside < all * 0.8 && outside <= sides * 0.1;
            if (mark) { end = y; misses = 0; } else misses++;
        }
        return end;
    }

    /// <summary>A little softening, since the shot is about twice as fine as the texture: no jagged edges from sampling it.</summary>
    private static byte[] Soften(byte[] rgba, FlatShot shot)
    {
        int w = shot.Width, h = shot.Height;
        var o = (byte[])rgba.Clone();
        Parallel.For(1, h - 1, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                int i = (y * w + x) * 4;
                if (rgba[i + 3] < 200) continue;
                int r = 0, g = 0, b = 0, n = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int q = i + (dy * w + dx) * 4;
                        if (rgba[q + 3] < 200) continue;
                        r += rgba[q]; g += rgba[q + 1]; b += rgba[q + 2]; n++;
                    }
                o[i] = (byte)(r / n); o[i + 1] = (byte)(g / n); o[i + 2] = (byte)(b / n);
            }
        });
        return o;
    }

    private static double RingAt(View v, double x)
    {
        var t = v.Ring;
        if (x <= t[0].X) return t[0].R;
        for (int k = 1; k < t.Length; k++)
            if (x <= t[k].X) return t[k - 1].R + (t[k].R - t[k - 1].R) * (x - t[k - 1].X) / (t[k].X - t[k - 1].X);
        return t[^1].R;
    }

    // Lines round the body follow the ring all the way up from the hem to here, and gently stop being bent above it.
    private static double RingFade(double y) => Math.Clamp((y - ZoneBottom) / (700 - ZoneBottom), 0, 1);

    /// <summary>The median colour of samples, and whether most of them are it.</summary>
    private static (int Color, bool Flat) Across(List<(byte R, byte G, byte B)> samples)
    {
        var r = samples.Select(c => c.R).OrderBy(c => c).ToList();
        var g = samples.Select(c => c.G).OrderBy(c => c).ToList();
        var b = samples.Select(c => c.B).OrderBy(c => c).ToList();
        int mr = r[r.Count / 2], mg = g[g.Count / 2], mb = b[b.Count / 2], same = 0;
        foreach (var c in samples)
            if (Math.Abs(c.R - mr) + Math.Abs(c.G - mg) + Math.Abs(c.B - mb) < 40) same++;
        return (mr << 16 | mg << 8 | mb, same >= samples.Count * 0.65);
    }

    /// <summary>Keeps a table's colours only where it's stripes: when nearly all of the range from..to is, the whole range is (the odd row at a stripe's edge included); else none of it.</summary>
    private static int[] Stripes((int Color, bool Flat)[] t, int from, int to)
    {
        var o = new int[t.Length];
        Array.Fill(o, -1);
        int flat = 0;
        for (int k = from; k < to; k++) if (t[k].Flat) flat++;
        if (flat < (to - from) * 0.7) return o;
        for (int k = from; k < to; k++) o[k] = t[k].Color;
        return o;
    }

    private const int RowsPerUnit = 2;

    /// <summary>For each height of the shot below the print, the colour of its row across the middle of the body, if that part of the shot is stripes.</summary>
    private static int[] FlatRows(View v, FlatShot shot, byte[] rgba)
    {
        var t = new (int, bool)[(int)(v.HemY + 2) * RowsPerUnit];
        double ox = shot.Left - v.RefLeft, oy = shot.Top - v.RefTop;
        Parallel.For(0, t.Length, k =>
        {
            double y = (double)k / RowsPerUnit;
            if (y < ZoneBottom) return;
            var (l, _, r) = Row(v, y);
            double fade = RingFade(y);
            var samples = new List<(byte, byte, byte)>();
            for (double x = l + 30; x <= r - 30; x += 5)
                samples.Add(SampleSolid(rgba, shot.Width, shot.Height, x * Scale + ox, (y + RingAt(v, x) * fade) * Scale + oy));
            t[k] = Across(samples);
        });
        return Stripes(t, (int)(ZoneBottom * RowsPerUnit), t.Length);
    }

    private const int SleeveSteps = 1000;
    // Stripes on a sleeve stop before the shoulder, which has its own shapes.
    private const double SleeveStripes = 0.62;

    /// <summary>For each step along a sleeve from the cuff, the colour across it, if that part of the sleeve is stripes.</summary>
    private static int[] FlatSleeve(SleevePath path, FlatShot shot, byte[] rgba, View v)
    {
        var t = new (int, bool)[SleeveSteps + 1];
        double ox = shot.Left - v.RefLeft, oy = shot.Top - v.RefTop;
        int to = (int)(SleeveSteps * SleeveStripes);
        Parallel.For(0, to, k =>
        {
            var (ax, ay, nx, ny, half) = Axis(path, (double)k / SleeveSteps);
            var samples = new List<(byte, byte, byte)>();
            for (double u = -0.6; u <= 0.6; u += 0.1)
                samples.Add(SampleSolid(rgba, shot.Width, shot.Height, ax + nx * u * half + ox, ay + ny * u * half + oy));
            t[k] = Across(samples);
        });
        return Stripes(t, 0, to);
    }

    private static bool Lerp(int[] table, double at, out (byte R, byte G, byte B) color)
    {
        color = default;
        int k = (int)Math.Floor(at);
        if (k < 0 || k + 1 >= table.Length || table[k] < 0 || table[k + 1] < 0) return false;
        double f = at - k;
        byte Mix(int sh) => (byte)Math.Round((table[k] >> sh & 255) * (1 - f) + (table[k + 1] >> sh & 255) * f);
        color = (Mix(16), Mix(8), Mix(0));
        return true;
    }

    /// <summary>
    /// Art from cleaned shots: each texel of the model takes the colour of the same place on the garment. Heights go from
    /// the hem to the collar's tab, which pin the shots' heights to the model's. What's a plain stripe in the shots is drawn
    /// as one, dead straight.
    /// </summary>
    public static byte[] Project(JerseyShape shape, byte[] start, FlatShot front, FlatShot back, byte[] frontRgba, byte[] backRgba, CollarMap collar, out byte[] printMask)
    {
        int size = shape.Map.Size;
        var art = (byte[])start.Clone();
        var mask = new byte[size * size];
        var pos = shape.Map.Position;
        // the tab's point: the collar's piece at the front, high on the chest (some kits have stray texels of it lower down)
        double tab = double.MaxValue;
        for (int i = 0; i < collar.Part.Length; i++)
            if (collar.Part[i] == CollarPart.Insert && pos[i * 3 + 1] > shape.PitY && pos[i * 3 + 2] > 0 && Math.Abs(pos[i * 3]) < 0.1)
                tab = Math.Min(tab, pos[i * 3 + 1]);
        tab = tab == double.MaxValue ? (shape.Map.Bounds[4] - shape.Hem) * 0.85 : tab - shape.Hem;
        var frontS = Sleeves(Front, shape);
        var backS = Sleeves(Back, shape);
        // the body's half width at each height, as it's seen straight on
        var halfWidth = new double[shape.BodyMid.Length];
        for (int i = 0; i < size * size; i++)
        {
            double ax = Math.Abs(pos[i * 3]);
            if (double.IsNaN(ax) || shape.OnSleeve(pos[i * 3], pos[i * 3 + 1])) continue;
            int b = Math.Clamp((int)((pos[i * 3 + 1] - shape.Hem) * 100), 0, halfWidth.Length - 1);
            if (ax > halfWidth[b]) halfWidth[b] = ax;
        }
        halfWidth = Smooth(halfWidth, shape.PitX);
        // metres per template unit in the print, then how much of the height the print's part needs to match it
        var (printLeft, printRight, printCloth) = FindPrint(Front, front, frontRgba);
        double printMiddle = (printLeft + printRight) / 2;
        double zoneEnd = Math.Max(ZoneBottom, PrintEnd(front, frontRgba, printLeft, printRight) + 3);
        double print = shape.PitX / BodyHalf * PrintSize, zone = (zoneEnd - ZoneTop) * print / tab, top = (ZoneTop - TabY) * print * 0.8 / tab;
        bool warp = zone + top < 0.8;
        double zoneHi = warp ? 1 - top : 0, zoneLo = warp ? zoneHi - zone : 0;
        double perMetre = 1 / print;
        double HeightOf(View view, double h)
        {
            if (h >= 1) return TabY + (view.TopY - TabY) * (h - 1) / 0.25;
            if (warp && h >= zoneHi) return ZoneTop + (TabY - ZoneTop) * (h - zoneHi) / (1 - zoneHi);
            if (warp && h >= zoneLo) return zoneEnd + (ZoneTop - zoneEnd) * (h - zoneLo) / (zoneHi - zoneLo);
            if (warp) return view.HemY + (zoneEnd - view.HemY) * h / zoneLo;
            return view.HemY + (TabY - view.HemY) * h;
        }
        double armpitS = shape.Cuff / (shape.Cuff + shape.Shoulder);
        var (_, _, backCloth) = FindPrint(Back, back, backRgba);
        var frontRows = FlatRows(Front, front, frontRgba);
        var backRows = FlatRows(Back, back, backRgba);
        var fl = FlatSleeve(frontS.Left, front, frontRgba, Front);
        var fr = FlatSleeve(frontS.Right, front, frontRgba, Front);
        var bl = FlatSleeve(backS.Left, back, backRgba, Back);
        var br = FlatSleeve(backS.Right, back, backRgba, Back);
        Parallel.For(0, size * size, i =>
        {
            float px = pos[i * 3];
            if (float.IsNaN(px)) return;
            double x = px, y = pos[i * 3 + 1], z = pos[i * 3 + 2];
            double h = (y - shape.Hem) / tab;
            double sx, sy;
            bool useFront, printable = false;
            int o = i * 4;
            if (shape.OnSleeve(x, y))
            {
                // a sleeve and the shoulder's cap: along it from the cuff, round it from the front
                double along = shape.Along(x, y);
                double s = Math.Clamp((shape.Cuff - along) / (shape.Cuff + shape.Shoulder), 0, 1);
                double q = shape.Around(x, y, z, true);
                // a stripe goes right round the arm, in one place: the front shot's, when the back agrees it's a stripe there
                var flatF = x > 0 ? fr : fl;
                var flatB = x > 0 ? bl : br;
                if (Lerp(flatF, s * SleeveSteps, out var band) && Lerp(flatB, s * SleeveSteps, out _))
                {
                    art[o] = band.R; art[o + 1] = band.G; art[o + 2] = band.B;
                    return;
                }
                useFront = Math.Abs(q) <= Math.PI / 2;
                var view = useFront ? frontS : backS;
                // the shots look from the front: the model's +x is on the right in the front one and the left in the back one
                bool right = (x > 0) == useFront;
                var (ax, ay, nx, ny, half) = Axis(right ? view.Right : view.Left, s);
                // not into the crease where the sleeve meets the body
                double t = Math.Max(Math.Sin(q), -0.55);
                double side = right ? 1 : -1;
                sx = ax + side * nx * t * half;
                sy = ay + side * ny * t * half;
                // towards the shoulder it goes over to where the body's mapping arrives at the same place, so they meet
                double w = Math.Clamp((s - SleeveStripes) / Math.Max(1e-6, armpitS - SleeveStripes), 0, 1);
                if (w > 0)
                {
                    var v0 = useFront ? Front : Back;
                    double y0 = HeightOf(v0, h);
                    var (ol, or) = Outer(v0, y0);
                    sx += ((right ? or : ol) * Scale - sx) * w;
                    sy += (y0 * Scale - sy) * w;
                }
            }
            else
            {
                int b = Math.Clamp((int)((y - shape.Hem) * 100), 0, shape.BodyMid.Length - 1);
                useFront = z - shape.BodyMid[b] >= 0;
                var view = useFront ? Front : Back;
                double y0 = HeightOf(view, h);
                // a plain row of the shot is a straight line on the model
                double printed = printMiddle + (useFront ? x : -x) * perMetre;
                bool tail = useFront && y0 <= zoneEnd && printed >= printLeft - 8 && printed <= printRight + 8;
                if (!tail && y0 >= ZoneBottom && backRows[(int)(y0 * RowsPerUnit)] >= 0 && Lerp(frontRows, y0 * RowsPerUnit, out var line))
                {
                    art[o] = line.R; art[o + 1] = line.G; art[o + 2] = line.B;
                    return;
                }
                var (l, c, r) = Row(view, y0);
                var (ol, or) = Outer(view, y0);
                // The shots look from the front, so on the back one the model's +x is on the left.
                double mx = useFront ? x : -x;
                double side = Math.Max(0.05, halfWidth[b]);
                double ax = Math.Min(Math.Abs(mx), side), u;
                bool plainRows = y0 >= FrontOuter[^1].Y && y0 <= zoneEnd;
                if (!useFront && plainRows)
                {
                    art[o] = backCloth.R; art[o + 1] = backCloth.G; art[o + 2] = backCloth.B;
                    return;
                }
                if (!useFront)
                {
                    // nothing printed: evenly across
                    double f = mx / side;
                    u = (ol + or) / 2 + Math.Clamp(f, -1, 1) * (or - ol) / 2;
                }
                else
                {
                    // straight on: the middle of the print at the shot's middle line, the same scale either side of it as
                    // far as the body shows in the shot, then on out to the edge
                    // the print goes on the model's middle line; above and below it the laces and the rest stay on the shot's
                    if (!double.IsNaN(printMiddle))
                    {
                        double near = y0 < ZoneTop ? 1 - (ZoneTop - y0) / 40 : y0 > zoneEnd ? 1 - (y0 - zoneEnd) / 40 : 1;
                        c += (printMiddle - c) * Math.Clamp(near, 0, 1);
                    }
                    double room = mx >= 0 ? r - c : c - l, outer = mx >= 0 ? or - c : c - ol;
                    double full = Math.Min(room * 0.97, side * perMetre), knee = full / perMetre;
                    if (ax <= knee) u = c + Math.Sign(mx) * ax * perMetre;
                    else if (plainRows)
                    {
                        // beside the print, below the shoulder: plain cloth
                        art[o] = printCloth.R; art[o + 1] = printCloth.G; art[o + 2] = printCloth.B;
                        return;
                    }
                    else u = c + Math.Sign(mx) * (full + (outer - full) * (ax - knee) / Math.Max(1e-6, side - knee));
                }
                if (useFront && plainRows && (double.IsNaN(printMiddle) || u < printLeft - 8 || u > printRight + 8))
                {
                    art[o] = printCloth.R; art[o + 1] = printCloth.G; art[o + 2] = printCloth.B;
                    return;
                }
                printable = useFront && y0 >= TabY && y0 <= zoneEnd;
                sx = u * Scale;
                sy = (y0 + RingAt(view, u) * RingFade(y0)) * Scale;
            }
            var shot = useFront ? front : back;
            var rgba = useFront ? frontRgba : backRgba;
            var refV = useFront ? Front : Back;
            sx += shot.Left - refV.RefLeft;
            sy += shot.Top - refV.RefTop;
            var col = SampleSolid(rgba, shot.Width, shot.Height, sx, sy);
            art[o] = col.R; art[o + 1] = col.G; art[o + 2] = col.B;
            if (printable && Math.Abs(col.R - printCloth.R) + Math.Abs(col.G - printCloth.G) + Math.Abs(col.B - printCloth.B) > 40) mask[i] = 255;
        });
        printMask = mask;
        return art;
    }

    /// <summary>A measure per centimetre without its jumps: the median of its neighbours; where there's none, the default.</summary>
    private static double[] Smooth(double[] v, double missing)
    {
        const int R = 4;
        var o = new double[v.Length];
        var t = new List<double>();
        for (int i = 0; i < v.Length; i++)
        {
            t.Clear();
            for (int k = Math.Max(0, i - R); k <= Math.Min(v.Length - 1, i + R); k++) if (v[k] > 0) t.Add(v[k]);
            if (t.Count == 0) { o[i] = missing; continue; }
            t.Sort();
            o[i] = t[t.Count / 2];
        }
        return o;
    }

    private static (double L, double C, double R) Row(View v, double y)
    {
        var t = v.Body;
        if (y <= t[0].Y) return (t[0].L, t[0].C, t[0].R);
        for (int k = 1; k < t.Length; k++)
            if (y <= t[k].Y)
            {
                double f = (y - t[k - 1].Y) / (t[k].Y - t[k - 1].Y);
                return (t[k - 1].L + (t[k].L - t[k - 1].L) * f, t[k - 1].C + (t[k].C - t[k - 1].C) * f, t[k - 1].R + (t[k].R - t[k - 1].R) * f);
            }
        var e = t[^1];
        return (e.L, e.C, e.R);
    }

    // Above the armpit the model's body goes on over the shoulder to the arm, so it reaches past the body in the shots to the
    // outside of the sleeve: (y, left, right).
    private static readonly (double Y, double L, double R)[] FrontOuter =
        { (60, 300, 590), (130, 222, 680), (200, 170, 715), (300, 155, 742), (345, 205, 690), (375, 250, 618) };
    private static readonly (double Y, double L, double R)[] BackOuter =
        { (50, 340, 540), (130, 205, 660), (200, 178, 700), (300, 155, 745), (340, 205, 690), (372, 245, 610) };

    private static (double L, double R) Outer(View v, double y)
    {
        var t = ReferenceEquals(v, Front) ? FrontOuter : BackOuter;
        if (y >= t[^1].Y) { var (l, _, r) = Row(v, y); return (l, r); }
        if (y <= t[0].Y) return (t[0].L, t[0].R);
        for (int k = 1; k < t.Length; k++)
            if (y <= t[k].Y)
            {
                double f = (y - t[k - 1].Y) / (t[k].Y - t[k - 1].Y);
                return (t[k - 1].L + (t[k].L - t[k - 1].L) * f, t[k - 1].R + (t[k].R - t[k - 1].R) * f);
            }
        return (t[^1].L, t[^1].R);
    }

    private sealed record SleevePath((double D, double X, double Y, double Half)[] Points);

    private static (SleevePath Left, SleevePath Right) Sleeves(View v, JerseyShape shape) => (Path(v.ScreenLeft), Path(v.ScreenRight));

    private static SleevePath Path((double X, double Y, double Half)[] p)
    {
        var o = new (double, double, double, double)[p.Length];
        double d = 0;
        for (int i = 0; i < p.Length; i++)
        {
            if (i > 0) d += Math.Sqrt(Math.Pow(p[i].X - p[i - 1].X, 2) + Math.Pow(p[i].Y - p[i - 1].Y, 2));
            o[i] = (d, p[i].X * Scale, p[i].Y * Scale, p[i].Half * Scale);
        }
        for (int i = 0; i < o.Length; i++) o[i].Item1 /= d;
        return new SleevePath(o);
    }

    /// <summary>The sleeve's middle at <paramref name="s"/> from the cuff (0) to the shoulder (1), the outward normal for a right-hand sleeve, and its half width.</summary>
    private static (double X, double Y, double Nx, double Ny, double Half) Axis(SleevePath p, double s)
    {
        var t = p.Points;
        int k = 1;
        while (k < t.Length - 1 && t[k].D < s) k++;
        double f = (s - t[k - 1].D) / Math.Max(1e-9, t[k].D - t[k - 1].D);
        double ax = t[k].X - t[k - 1].X, ay = t[k].Y - t[k - 1].Y, len = Math.Sqrt(ax * ax + ay * ay);
        // going up the sleeve (ay < 0): turning clockwise on the screen points right
        double nx = -ay / len, ny = ax / len;
        return (t[k - 1].X + ax * f, t[k - 1].Y + ay * f, nx, ny, t[k - 1].Half + (t[k].Half - t[k - 1].Half) * f);
    }

    /// <summary>The shot's colour at a point, bilinear; off the jersey, the nearest of it going in towards the shot's middle.</summary>
    private static (byte R, byte G, byte B) SampleSolid(byte[] rgba, int w, int h, double x, double y)
    {
        double cx = w / 2.0, cy = h / 2.0;
        for (int step = 0; step < 24; step++)
        {
            int x0 = Math.Clamp((int)Math.Floor(x), 0, w - 2), y0 = Math.Clamp((int)Math.Floor(y), 0, h - 2);
            int o = (y0 * w + x0) * 4;
            if (rgba[o + 3] > 200 && rgba[o + 7] > 200 && rgba[o + w * 4 + 3] > 200 && rgba[o + w * 4 + 7] > 200)
            {
                double fx = Math.Clamp(x - x0, 0, 1), fy = Math.Clamp(y - y0, 0, 1);
                byte L(int c) => (byte)Math.Round(
                    (rgba[o + c] * (1 - fx) + rgba[o + 4 + c] * fx) * (1 - fy) + (rgba[o + w * 4 + c] * (1 - fx) + rgba[o + w * 4 + 4 + c] * fx) * fy);
                return (L(0), L(1), L(2));
            }
            double dx = cx - x, dy = cy - y, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            x += dx / len * 8;
            y += dy / len * 8;
        }
        return (128, 128, 128);
    }

    // ================================================================ seeing the result without the game's lighting

    /// <summary>The art on the model seen straight on from the front or back (A-pose, no lighting), for checking.</summary>
    public static byte[] Look(JerseyShape shape, byte[] art, bool fromBack, int size, out int outSize)
    {
        outSize = size;
        var o = new byte[size * size * 4];
        var depth = new double[size * size];
        Array.Fill(depth, double.NegativeInfinity);
        var b = shape.Map.Bounds;
        double scale = (size - 20) / Math.Max(b[3] - b[0], b[4] - b[1]);
        var pos = shape.Map.Position;
        int n = shape.Map.Size * shape.Map.Size;
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(pos[i * 3])) continue;
            double x = pos[i * 3], y = pos[i * 3 + 1], z = pos[i * 3 + 2];
            double d = fromBack ? -z : z;
            int sx = (int)(size / 2 + (fromBack ? -x : x) * scale), sy = (int)(size - 10 - (y - b[1]) * scale);
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    int px = sx + dx, py = sy + dy;
                    if (px < 0 || py < 0 || px >= size || py >= size) continue;
                    int k = py * size + px;
                    if (d <= depth[k]) continue;
                    depth[k] = d;
                    o[k * 4] = art[i * 4]; o[k * 4 + 1] = art[i * 4 + 1]; o[k * 4 + 2] = art[i * 4 + 2]; o[k * 4 + 3] = 255;
                }
        }
        return o;
    }
}
