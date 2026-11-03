namespace NHL27Kit.Core;

/// <summary>The collar's pieces, as the jersey texture has them.</summary>
public enum CollarPart { None, NeckFront, NeckBack, Insert, BackPatch, Front, Sides, RingFront, RingBack }

/// <summary>
/// A stripe along the collar (the neck band and the collar's front): <see cref="From"/> and <see cref="Width"/> in %
/// of its height, from the edge sewn to the jersey (0) to the edge at the neck (100).
/// </summary>
public sealed record CollarStripe(double From, double Width, int Color);

/// <summary>
/// The collar of the jersey texture: which collar piece each texel is (the collar's front, whose two sides meet in the
/// V; the V insert under it; the neck band, split front and back; the patch at the back of the neck; the collar's ends)
/// and, on the collar's front and the neck band, how far across it the texel is (0 at the edge sewn to the jersey, 1 at
/// the edge by the neck).
/// </summary>
public sealed class CollarMap
{
    public int Size { get; init; }
    public CollarPart[] Part { get; init; }
    /// <summary>0 at the jersey's edge … 1 at the neck's on the collar's front and the neck band; NaN elsewhere.</summary>
    public float[] Up { get; init; }
    /// <summary>Texels (steps) from the neck opening in the texture, on the collar's pieces round it; MaxValue elsewhere.</summary>
    public float[] Hole { get; init; }

    public static readonly (CollarPart Part, string Name, string Hint)[] Parts =
    {
        (CollarPart.Front, "Collar, front", "its two sides, meeting in the V"),
        (CollarPart.RingFront, "Collar ring, front", "the band round the neck opening, at the front"),
        (CollarPart.RingBack, "Collar ring, back", "the band round the neck opening, at the back"),
        (CollarPart.Insert, "V insert", "under the collar at the front, where the NHL shield sits"),
        (CollarPart.NeckFront, "Neck band, front", "the band round the neck, inside the collar"),
        (CollarPart.NeckBack, "Neck band, back", "the band round the back of the neck"),
        (CollarPart.BackPatch, "Back of the neck", "the panel under the collar at the back"),
        (CollarPart.Sides, "Collar ends", "small pieces where the collar's ends meet"),
    };

    public static CollarMap Build(StudioSource src)
    {
        int size = src.Size, n = size * size;
        var part = new CollarPart[n];
        var up = new float[n];
        Array.Fill(up, float.NaN);
        var map = src.Shape?.Map;
        if (map == null || map.Size != size) return new CollarMap { Size = size, Part = part, Up = up };
        var pos = map.Position;
        var neck = src.NeckBand;
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(pos[i * 3])) continue;
            bool front = pos[i * 3 + 2] >= 0;
            if (neck[i]) { part[i] = front ? CollarPart.NeckFront : CollarPart.NeckBack; continue; }
            part[i] = JerseyStudio.Region(src.Base[i * 4 + 3]) switch
            {
                5 => CollarPart.Sides,
                6 => front ? CollarPart.Insert : CollarPart.BackPatch,
                7 => CollarPart.Front,
                _ => CollarPart.None,
            };
        }

        // up the band: the distance to its two edges (the hole's and the outside's) in the texture
        var outside = new bool[n];
        var queue = new Queue<int>();
        void Seed(int i) { if (!neck[i] && !outside[i]) { outside[i] = true; queue.Enqueue(i); } }
        for (int k = 0; k < size; k++) { Seed(k); Seed((size - 1) * size + k); Seed(k * size); Seed(k * size + size - 1); }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % size;
            if (x > 0) Seed(i - 1);
            if (x < size - 1) Seed(i + 1);
            if (i >= size) Seed(i - size);
            if (i < n - size) Seed(i + size);
        }
        var dIn = Distance(neck, i => !neck[i] && !outside[i], size);
        var dOut = Distance(neck, i => outside[i], size);
        double yIn = 0, yOut = 0;
        int cIn = 0, cOut = 0;
        for (int i = 0; i < n; i++)
        {
            if (!neck[i] || float.IsNaN(pos[i * 3])) continue;
            float t = dIn[i] / Math.Max(1f, dIn[i] + dOut[i]);
            up[i] = t;
            if (t < 0.2) { yIn += pos[i * 3 + 1]; cIn++; }
            else if (t > 0.8) { yOut += pos[i * 3 + 1]; cOut++; }
        }
        // the hole's edge is the band's top when it's the higher one on the model; else the other way up
        if (cIn > 0 && cOut > 0 && yIn / cIn < yOut / cOut)
            for (int i = 0; i < n; i++) if (!float.IsNaN(up[i])) up[i] = 1 - up[i];

        // the collar's front: from where it meets the jersey's fabric (body, yoke, sides) to its other edges
        var wings = new bool[n];
        for (int i = 0; i < n; i++) wings[i] = part[i] == CollarPart.Front;
        bool Fabric(int i) => !float.IsNaN(pos[i * 3]) && part[i] == CollarPart.None && !neck[i];
        var dJersey = Distance(wings, Fabric, size);
        var dNeck = Distance(wings, i => !wings[i] && !Fabric(i), size);
        for (int i = 0; i < n; i++)
            if (wings[i] && dJersey[i] < float.MaxValue && dNeck[i] < float.MaxValue)
                up[i] = dJersey[i] / Math.Max(1f, dJersey[i] + dNeck[i]);
        // the neck opening: the gap in the texture that the collar's pieces close round (not reached from the sheet's edge)
        var covered = new bool[n];
        for (int i = 0; i < n; i++) covered[i] = !float.IsNaN(pos[i * 3]);
        var open = new bool[n];
        queue.Clear();
        void Open(int i) { if (!covered[i] && !open[i]) { open[i] = true; queue.Enqueue(i); } }
        for (int k = 0; k < size; k++) { Open(k); Open((size - 1) * size + k); Open(k * size); Open(k * size + size - 1); }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % size;
            if (x > 0) Open(i - 1);
            if (x < size - 1) Open(i + 1);
            if (i >= size) Open(i - size);
            if (i < n - size) Open(i + size);
        }
        var collar = new bool[n];
        for (int i = 0; i < n; i++) collar[i] = part[i] is CollarPart.Insert or CollarPart.BackPatch or CollarPart.Front or CollarPart.Sides;
        var hole = Distance(collar, i => !covered[i] && !open[i], size);

        // the ring round the opening: the insert and the back panel are one piece each with it in the texture. Going out
        // from the opening the ring gets longer (it's round it), then where it ends the count per step falls to the
        // panel's straight width; that's the ring's edge
        foreach (var (whole, ring) in new[] { (CollarPart.Insert, CollarPart.RingFront), (CollarPart.BackPatch, CollarPart.RingBack) })
        {
            const int Bin = 5;
            var count = new int[size / Bin + 2];
            for (int i = 0; i < n; i++)
                if (part[i] == whole && hole[i] < float.MaxValue) count[Math.Min(count.Length - 1, (int)(hole[i] / Bin))]++;
            int most = 0, edge = 0;
            for (int b = 0; b < count.Length; b++)
            {
                most = Math.Max(most, count[b]);
                if (most > 0 && count[b] < most * 0.75) { edge = b * Bin; break; }
            }
            if (edge == 0) continue;
            for (int i = 0; i < n; i++)
                if (part[i] == whole && hole[i] < edge)
                {
                    part[i] = ring;
                    up[i] = 1 - (hole[i] - 1) / edge;
                }

            // what's left: the insert (or panel) itself, the biggest piece, and the ring's wider ends out over the
            // shoulders, cut off from it; those are the ring's
            var pieces = new List<(List<int> Texels, double X)>();
            var seen = new bool[n];
            for (int start = 0; start < n; start++)
            {
                if (seen[start] || part[start] != whole) continue;
                var texels = new List<int>();
                double x = 0;
                seen[start] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), ix = i % size;
                    texels.Add(i);
                    x += Math.Abs(pos[i * 3]);
                    foreach (int j in new[] { ix > 0 ? i - 1 : -1, ix < size - 1 ? i + 1 : -1, i - size, i + size })
                        if (j >= 0 && j < n && !seen[j] && part[j] == whole) { seen[j] = true; queue.Enqueue(j); }
                }
                pieces.Add((texels, x / texels.Count));
            }
            if (pieces.Count < 2) continue;
            var main = pieces.MaxBy(t => t.Texels.Count);
            double reach = main.Texels.Max(i => Math.Abs(pos[i * 3]));
            foreach (var piece in pieces)
                if (piece.Texels != main.Texels && piece.X > reach)
                    foreach (int i in piece.Texels) { part[i] = ring; up[i] = 0; }
        }
        return new CollarMap { Size = size, Part = part, Up = up, Hole = hole };
    }

    /// <summary>Steps (4-neighbour) from the texels <paramref name="from"/> into the island.</summary>
    private static float[] Distance(bool[] island, Func<int, bool> from, int size)
    {
        int n = size * size;
        var d = new float[n];
        Array.Fill(d, float.MaxValue);
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            if (!island[i]) continue;
            int x = i % size;
            if ((x > 0 && from(i - 1)) || (x < size - 1 && from(i + 1)) || (i >= size && from(i - size)) || (i < n - size && from(i + size)))
            {
                d[i] = 1;
                queue.Enqueue(i);
            }
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % size;
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < size - 1 ? i + 1 : -1, i - size, i + size })
                if (j >= 0 && j < n && island[j] && d[j] > d[i] + 1) { d[j] = d[i] + 1; queue.Enqueue(j); }
        }
        return d;
    }

    /// <summary>Fills the collar pieces given a colour, then the stripes across the neck band.</summary>
    public void Paint(byte[] rgba, IReadOnlyDictionary<CollarPart, int> colors, IReadOnlyList<CollarStripe> stripes)
    {
        if (colors.Count == 0 && stripes.Count == 0) return;
        int n = Size * Size;
        for (int i = 0; i < n; i++)
        {
            var p = Part[i];
            if (p == CollarPart.None) continue;
            if (colors.TryGetValue(p, out int c)) Set(rgba, i, c, 1);
            if (float.IsNaN(Up[i])) continue;
            double u = Up[i] * 100;
            foreach (var s in stripes)
            {
                // about a texel of soft edge either side (the band is ~60 texels high at 2048)
                const double Soft = 1.5;
                double a = Math.Clamp((u - s.From) / Soft + 0.5, 0, 1) * Math.Clamp((s.From + s.Width - u) / Soft + 0.5, 0, 1);
                if (a > 0) Set(rgba, i, s.Color, a);
            }
        }
    }

    private static void Set(byte[] rgba, int i, int c, double a)
    {
        int o = i * 4;
        rgba[o] = (byte)Math.Round((c >> 16 & 255) * a + rgba[o] * (1 - a));
        rgba[o + 1] = (byte)Math.Round((c >> 8 & 255) * a + rgba[o + 1] * (1 - a));
        rgba[o + 2] = (byte)Math.Round((c & 255) * a + rgba[o + 2] * (1 - a));
    }
}
