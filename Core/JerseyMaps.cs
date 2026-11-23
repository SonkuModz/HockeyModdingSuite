namespace NHL27Kit.Core;

/// <summary>A design's normal and shine maps, RGBA and square, in the jersey's layout.</summary>
public sealed record JerseyMapSet(byte[] BaseNormal, byte[] Normal, byte[] BaseCoeff, byte[] Coeff, int Size);

/// <summary>
/// Normal and shine maps for a new design. A kit's own maps have its old stripes and logos in them as relief, so they
/// don't fit another design. The garment itself is what the game's kits have in common: at each texel the median of many
/// kits leaves the folds, panel seams, vents and knit and loses what's particular to any one kit. On that go the design's
/// own relief: a raised seam at each colour edge, and the print as raised twill with a stitched outline, more matte than
/// the cloth (PLAN §26).
/// </summary>
public static class JerseyMaps
{
    /// <summary>The garment with no design on it.</summary>
    public sealed record Neutral(byte[] BaseNormal, byte[] Knit, byte[] BaseCoeff, byte[] Coeff, int Size, int Kits);

    private const int KitsUsed = 15;
    // A coeff's green: 54 on cloth, lower on twill.
    private const int ClothFrom = 50, TwillShine = 38;

    public static Neutral LoadNeutral(AssetIndex index, int size = 2048)
    {
        var all = Kits.Find(index)
            .Where(k => k.IsStamped("jersey") && new[] { "jersey_base_normal", "jersey_normal", "jersey_base_coeff", "jersey_coeff" }.All(s => k.Slot(s) != null))
            .GroupBy(k => k.Team).Select(g => g.First()).ToList();
        if (all.Count == 0) throw new InvalidDataException("no kit has normal and shine maps to start from");
        // spread across the teams
        var picked = Enumerable.Range(0, Math.Min(KitsUsed, all.Count)).Select(i => all[i * all.Count / Math.Min(KitsUsed, all.Count)]).ToList();
        var loaded = new (byte[] BaseN, byte[] N, byte[] BaseC, byte[] C)?[picked.Count];
        byte[] Read(Kit k, string slot)
        {
            var t = Kits.Load(index, k.Slot(slot).Name, size, 0);
            return t.Rgba != null && t.Width == size && t.Height == size ? t.Rgba : null;
        }
        Parallel.For(0, picked.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            try
            {
                var (bn, n, bc, c) = (Read(picked[i], "jersey_base_normal"), Read(picked[i], "jersey_normal"), Read(picked[i], "jersey_base_coeff"), Read(picked[i], "jersey_coeff"));
                if (bn != null && n != null && bc != null && c != null) loaded[i] = (bn, n, bc, c);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
        });
        var sets = loaded.Where(l => l != null).Select(l => l.Value).ToList();
        if (sets.Count == 0) throw new InvalidDataException("no kit's normal and shine maps could be read");

        int texels = size * size, k = sets.Count;
        var baseN = new byte[texels * 4];
        var knit = new byte[texels * 4];
        var baseC = new byte[texels * 4];
        var coeff = new byte[texels * 4];
        Parallel.For(0, texels, () => new byte[k], (i, _, v) =>
        {
            int o = i * 4;
            byte Mid(Func<(byte[] BaseN, byte[] N, byte[] BaseC, byte[] C), byte> at, Func<(byte[] BaseN, byte[] N, byte[] BaseC, byte[] C), bool> use = null)
            {
                int n = 0;
                foreach (var s in sets) if (use == null || use(s)) v[n++] = at(s);
                if (n == 0) { foreach (var s in sets) v[n++] = at(s); }
                Array.Sort(v, 0, n);
                return v[n / 2];
            }
            baseN[o] = Mid(s => s.BaseN[o]);
            baseN[o + 1] = Mid(s => s.BaseN[o + 1]);
            baseN[o + 2] = 255;
            baseN[o + 3] = 255;
            baseC[o] = Mid(s => s.BaseC[o]);
            baseC[o + 1] = Mid(s => s.BaseC[o + 1]);
            baseC[o + 2] = Mid(s => s.BaseC[o + 2]);
            baseC[o + 3] = Mid(s => s.BaseC[o + 3]);
            // knit and shine from the kits with cloth here, not a logo
            bool Cloth((byte[] BaseN, byte[] N, byte[] BaseC, byte[] C) s) => s.C[o + 1] >= ClothFrom;
            knit[o] = Mid(s => (byte)Math.Clamp(s.N[o] - s.BaseN[o] + 128, 0, 255), Cloth);
            knit[o + 1] = Mid(s => (byte)Math.Clamp(s.N[o + 1] - s.BaseN[o + 1] + 128, 0, 255), Cloth);
            coeff[o] = Mid(s => s.C[o], Cloth);
            coeff[o + 1] = Mid(s => s.C[o + 1], Cloth);
            coeff[o + 2] = Mid(s => s.C[o + 2], Cloth);
            coeff[o + 3] = 255;
            return v;
        }, _ => { });
        return new Neutral(baseN, knit, baseC, coeff, size, k);
    }

    /// <summary>The maps for a design: its art and the mask of what's printed on it (255 = print).</summary>
    public static JerseyMapSet Generate(Neutral g, JerseyShape shape, byte[] art, byte[] print)
    {
        int size = g.Size, texels = size * size;
        if (shape.Map.Size != size) throw new ArgumentException("the model's map isn't the maps' size");
        var pos = shape.Map.Position;
        bool Valid(int i) => !float.IsNaN(pos[i * 3]);

        // where the colour changes, on the cloth and in the print; and the print's own outline
        var clothEdge = new float[texels];
        var printEdge = new float[texels];
        var outline = new float[texels];
        var mask = new float[texels];
        Parallel.For(0, size - 1, y =>
        {
            for (int x = 0; x < size - 1; x++)
            {
                int i = y * size + x;
                if (!Valid(i)) continue;
                mask[i] = print[i] / 255f;
                foreach (int j in new[] { i + 1, i + size })
                {
                    if (!Valid(j)) continue;
                    if ((print[i] > 127) != (print[j] > 127)) outline[i] = 1;
                    int d = Math.Abs(art[i * 4] - art[j * 4]) + Math.Abs(art[i * 4 + 1] - art[j * 4 + 1]) + Math.Abs(art[i * 4 + 2] - art[j * 4 + 2]);
                    if (d <= EdgeStep) continue;
                    if (print[i] > 127 || print[j] > 127) printEdge[i] = 1; else clothEdge[i] = 1;
                }
            }
        });
        var seams = Blur(clothEdge, size, 1.1f);
        var stitched = Blur(printEdge, size, 0.9f);
        var groove = Blur(outline, size, 1.0f);
        var plateau = Blur(mask, size, 1.1f);
        var height = new float[texels];
        Parallel.For(0, texels, i => height[i] = plateau[i] * Twill + seams[i] * Seam + stitched[i] * Stitch - groove[i] * Groove);

        var baseN = (byte[])g.BaseNormal.Clone();
        var normal = new byte[texels * 4];
        var baseC = (byte[])g.BaseCoeff.Clone();
        var coeff = (byte[])g.Coeff.Clone();
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x, o = i * 4;
                float dx = 0, dy = 0;
                if (Valid(i))
                {
                    float At(int xx, int yy)
                    {
                        if (xx < 0 || yy < 0 || xx >= size || yy >= size) return height[i];
                        int j = yy * size + xx;
                        return Valid(j) ? height[j] : height[i];
                    }
                    dx = (At(x + 1, y) - At(x - 1, y)) * Strength * SlopeX;
                    dy = (At(x, y + 1) - At(x, y - 1)) * Strength * SlopeY;
                }
                baseN[o] = Clamp(g.BaseNormal[o] + dx);
                baseN[o + 1] = Clamp(g.BaseNormal[o + 1] + dy);
                // twill has no knit
                float k = 1 - plateau[i];
                normal[o] = Clamp(baseN[o] + (g.Knit[o] - 128) * k);
                normal[o + 1] = Clamp(baseN[o + 1] + (g.Knit[o + 1] - 128) * k);
                normal[o + 2] = 255;
                normal[o + 3] = 255;
                coeff[o + 1] = Clamp(g.Coeff[o + 1] + (TwillShine - g.Coeff[o + 1]) * plateau[i]);
            }
        });
        return new JerseyMapSet(baseN, normal, baseC, coeff, size);
    }

    private const int EdgeStep = 48;
    private const float Twill = 1f, Seam = 0.55f, Stitch = 0.3f, Groove = 0.3f, Strength = 45f;
    // A height rising to the right leans the normal left; rising downwards leans it up. Measured on real kits' logos
    // (full normal minus base, across the logo's edge): Detroit, Pittsburgh, Chicago and Colorado all agree.
    private const float SlopeX = -1f, SlopeY = 1f;

    private static byte Clamp(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);

    private static float[] Blur(float[] src, int size, float sigma)
    {
        int r = (int)MathF.Ceiling(sigma * 2.5f);
        var kernel = new float[r * 2 + 1];
        float sum = 0;
        for (int i = -r; i <= r; i++) sum += kernel[i + r] = MathF.Exp(-i * i / (2 * sigma * sigma));
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
        var a = new float[src.Length];
        var b = new float[src.Length];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                float t = 0;
                for (int k = -r; k <= r; k++) t += kernel[k + r] * src[y * size + Math.Clamp(x + k, 0, size - 1)];
                a[y * size + x] = t;
            }
        });
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                float t = 0;
                for (int k = -r; k <= r; k++) t += kernel[k + r] * a[Math.Clamp(y + k, 0, size - 1) * size + x];
                b[y * size + x] = t;
            }
        });
        return b;
    }
}
