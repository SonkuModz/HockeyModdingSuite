namespace NHL27Kit.Core;

/// <summary>
/// Where each texel of a garment's texture sits on its model: the mesh's triangles drawn in texture space (first UV
/// set), each texel holding the interpolated 3D position and, when the mesh has one, its second UV set. Lines painted
/// in 3D (a stripe round the body at a height) and decals mapped with the second UVs (the helmet's) are drawn with it.
/// </summary>
public sealed class SurfaceMap
{
    public int Size { get; }
    /// <summary>x, y, z per texel (NaN where no triangle covers it).</summary>
    public float[] Position { get; }
    /// <summary>u, v of the second UV set per texel (null when the mesh has none; NaN where uncovered).</summary>
    public float[] Uv2 { get; }
    /// <summary>The model's bounds: min x, y, z then max x, y, z.</summary>
    public float[] Bounds { get; } = { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };

    private SurfaceMap(int size, bool uv2)
    {
        Size = size;
        Position = new float[size * size * 3];
        Array.Fill(Position, float.NaN);
        if (uv2) { Uv2 = new float[size * size * 2]; Array.Fill(Uv2, float.NaN); }
    }

    public bool Covered(int texel) => !float.IsNaN(Position[texel * 3]);

    /// <summary>Draws the parts' triangles (those with UVs) into a size×size map. The texture's top row is V = 0.</summary>
    public static SurfaceMap Build(IEnumerable<MeshPart> parts, int size)
    {
        var list = parts.Where(p => p.Uvs != null).ToList();
        var map = new SurfaceMap(size, list.Any(p => p.Uvs2 != null));
        foreach (var p in list)
            for (int v = 0; v < p.VertexCount; v++)
                for (int c = 0; c < 3; c++)
                {
                    map.Bounds[c] = Math.Min(map.Bounds[c], p.Positions[v * 3 + c]);
                    map.Bounds[c + 3] = Math.Max(map.Bounds[c + 3], p.Positions[v * 3 + c]);
                }
        foreach (var p in list)
        {
            for (int t = 0; t < p.TriangleCount; t++)
            {
                int a = p.Indices[t * 3], b = p.Indices[t * 3 + 1], c = p.Indices[t * 3 + 2];
                double ax = p.Uvs[a * 2] * size, ay = p.Uvs[a * 2 + 1] * size;
                double bx = p.Uvs[b * 2] * size, by = p.Uvs[b * 2 + 1] * size;
                double cx = p.Uvs[c * 2] * size, cy = p.Uvs[c * 2 + 1] * size;
                double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-9) continue;
                // a texel's centre inside the triangle, with half a texel of slack so neighbouring triangles leave no gaps
                int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)) - 1)), x1 = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)) - 1)), y1 = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                double slack = 0.75 / Math.Sqrt(Math.Abs(area));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double wa = ((bx - px) * (cy - py) - (by - py) * (cx - px)) / area;
                    double wb = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) / area;
                    double wc = 1 - wa - wb;
                    if (wa < -slack || wb < -slack || wc < -slack) continue;
                    int o = y * size + x;
                    // a texel already filled from inside its own triangle keeps it
                    bool inside = wa >= 0 && wb >= 0 && wc >= 0;
                    if (!inside && map.Covered(o)) continue;
                    for (int k = 0; k < 3; k++)
                        map.Position[o * 3 + k] = (float)(wa * p.Positions[a * 3 + k] + wb * p.Positions[b * 3 + k] + wc * p.Positions[c * 3 + k]);
                    if (p.Uvs2 != null)
                        for (int k = 0; k < 2; k++)
                            map.Uv2[o * 2 + k] = (float)(wa * p.Uvs2[a * 2 + k] + wb * p.Uvs2[b * 2 + k] + wc * p.Uvs2[c * 2 + k]);
                }
            }
        }
        return map;
    }

    /// <summary>
    /// Stamps an image onto <paramref name="rgba"/> (size×size, the map's size) through the second UV set: a texel whose
    /// second UV falls in <paramref name="rect"/> (u0, v0, u1, v1) takes the image there, blended by its alpha.
    /// </summary>
    public void Decal(byte[] rgba, byte[] img, int iw, int ih, (double U0, double V0, double U1, double V1) rect, bool flipU = false)
    {
        if (Uv2 == null) return;
        int n = Size * Size;
        Parallel.For(0, Size, y =>
        {
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                float u = Uv2[i * 2], v = Uv2[i * 2 + 1];
                if (float.IsNaN(u)) continue;
                double s = (u - rect.U0) / (rect.U1 - rect.U0), t = (v - rect.V0) / (rect.V1 - rect.V0);
                if (s < 0 || s >= 1 || t < 0 || t >= 1) continue;
                if (flipU) s = 1 - s;
                double fx = s * iw - 0.5, fy = t * ih - 0.5;
                int sx = Math.Clamp((int)Math.Floor(fx), 0, iw - 1), sy = Math.Clamp((int)Math.Floor(fy), 0, ih - 1);
                int sx1 = Math.Min(iw - 1, sx + 1), sy1 = Math.Min(ih - 1, sy + 1);
                double ax = Math.Clamp(fx - sx, 0, 1), ay = Math.Clamp(fy - sy, 0, 1);
                double r = 0, g = 0, b = 0, a = 0;
                void Add(int px, int py, double w)
                {
                    int o = (py * iw + px) * 4;
                    double al = img[o + 3] / 255.0 * w;
                    r += img[o] * al; g += img[o + 1] * al; b += img[o + 2] * al; a += al;
                }
                Add(sx, sy, (1 - ax) * (1 - ay)); Add(sx1, sy, ax * (1 - ay)); Add(sx, sy1, (1 - ax) * ay); Add(sx1, sy1, ax * ay);
                if (a <= 0.001) continue;
                int d = i * 4;
                rgba[d] = (byte)Math.Round(r / a * a + rgba[d] * (1 - a));
                rgba[d + 1] = (byte)Math.Round(g / a * a + rgba[d + 1] * (1 - a));
                rgba[d + 2] = (byte)Math.Round(b / a * a + rgba[d + 2] * (1 - a));
            }
        });
    }
}
