using System.Buffers.Binary;
using System.Globalization;

namespace NHL27Kit.Core;

/// <summary>Triangles to put into a mesh section: positions (metres, the game's axes), UVs (top-left origin) and indices.</summary>
public sealed class NewGeometry
{
    public List<float> Positions { get; } = new();
    public List<float> Uvs { get; } = new();
    public List<int> Indices { get; } = new();
    public int VertexCount => Positions.Count / 3;
    public int TriangleCount => Indices.Count / 3;

    /// <summary>
    /// Cuts the geometry into pieces that fit sections of the given sizes (vertices, triangles), in order. Triangles are
    /// taken in order along the longest axis so each piece is one region of the model. Returns one piece per capacity
    /// (empty when nothing is left), and throws when the capacities run out first.
    /// </summary>
    public List<NewGeometry> Split(IReadOnlyList<(int Vertices, int Triangles)> capacities)
    {
        int tris = TriangleCount;
        float[] min = { float.MaxValue, float.MaxValue, float.MaxValue }, max = { float.MinValue, float.MinValue, float.MinValue };
        for (int i = 0; i < VertexCount; i++) for (int c = 0; c < 3; c++) { min[c] = Math.Min(min[c], Positions[i * 3 + c]); max[c] = Math.Max(max[c], Positions[i * 3 + c]); }
        int axis = Enumerable.Range(0, 3).OrderByDescending(c => max[c] - min[c]).First();
        var order = Enumerable.Range(0, tris).OrderBy(t => Positions[Indices[t * 3] * 3 + axis] + Positions[Indices[t * 3 + 1] * 3 + axis] + Positions[Indices[t * 3 + 2] * 3 + axis]).ToList();
        var pieces = new List<NewGeometry>();
        int next = 0;
        foreach (var (cv, ct) in capacities)
        {
            var piece = new NewGeometry();
            var map = new Dictionary<int, int>();
            while (next < tris && piece.TriangleCount < ct)
            {
                int t = order[next];
                int newVerts = Enumerable.Range(0, 3).Count(k => !map.ContainsKey(Indices[t * 3 + k]));
                if (piece.VertexCount + newVerts > cv) break;
                for (int k = 0; k < 3; k++)
                {
                    int old = Indices[t * 3 + k];
                    if (!map.TryGetValue(old, out int ni))
                    {
                        ni = map[old] = piece.VertexCount;
                        for (int c = 0; c < 3; c++) piece.Positions.Add(Positions[old * 3 + c]);
                        piece.Uvs.Add(Uvs[old * 2]); piece.Uvs.Add(Uvs[old * 2 + 1]);
                    }
                    piece.Indices.Add(ni);
                }
                next++;
            }
            pieces.Add(piece);
        }
        if (next < tris) throw new InvalidDataException($"{tris - next} of {tris} triangles don't fit the sections given.");
        return pieces;
    }

    /// <summary>
    /// The faces of some groups of a Wavefront OBJ (all of them when <paramref name="groups"/> is empty), triangulated,
    /// one vertex per distinct position/UV pair, scaled by <paramref name="scale"/> and with V flipped (OBJ's origin is
    /// bottom-left).
    /// </summary>
    public static NewGeometry FromObj(string file, ISet<string> groups, float scale) => FromObj(file, groups, scale, 0);

    /// <param name="rotateY">Degrees to turn the model about the vertical axis (counter-clockwise seen from above), after scaling.</param>
    public static NewGeometry FromObj(string file, ISet<string> groups, float scale, float rotateY)
    {
        double ang = rotateY * Math.PI / 180;
        float cos = (float)Math.Cos(ang), sin = (float)Math.Sin(ang);
        var g = new NewGeometry();
        var v = new List<float>();
        var vt = new List<float>();
        var map = new Dictionary<(int, int), int>();
        string group = null;
        foreach (var line in File.ReadLines(file))
        {
            if (line.StartsWith("v ")) { var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); for (int i = 1; i <= 3; i++) v.Add(float.Parse(p[i], CultureInfo.InvariantCulture)); }
            else if (line.StartsWith("vt")) { var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); vt.Add(float.Parse(p[1], CultureInfo.InvariantCulture)); vt.Add(p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : 0); }
            else if (line.StartsWith("g ") || line.StartsWith("o ")) group = line[2..].Trim();
            else if (line.StartsWith("f ") && (groups.Count == 0 || groups.Contains(group)))
            {
                var corners = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(c =>
                {
                    var a = c.Split('/');
                    int pi = int.Parse(a[0]) - 1, ti = a.Length > 1 && a[1].Length > 0 ? int.Parse(a[1]) - 1 : -1;
                    if (!map.TryGetValue((pi, ti), out int k))
                    {
                        k = map[(pi, ti)] = g.VertexCount;
                        float x = v[pi * 3] * scale, y = v[pi * 3 + 1] * scale, z = v[pi * 3 + 2] * scale;
                        g.Positions.Add(x * cos + z * sin); g.Positions.Add(y); g.Positions.Add(-x * sin + z * cos);
                        g.Uvs.Add(ti >= 0 ? vt[ti * 2] : 0); g.Uvs.Add(ti >= 0 ? 1 - vt[ti * 2 + 1] : 0);
                    }
                    return k;
                }).ToList();
                for (int i = 1; i + 1 < corners.Count; i++) { g.Indices.Add(corners[0]); g.Indices.Add(corners[i]); g.Indices.Add(corners[i + 1]); }
            }
        }
        return g;
    }
}

/// <summary>
/// Writes new geometry into an existing section of a MeshSet whose LOD geometry lives in a chunk, in place: the section
/// keeps its vertex count, triangle count, vertex format and buffer offsets, so nothing but the chunk's bytes changes.
/// The new vertices and triangles fill the section from the start; the rest become copies of the first new vertex and
/// degenerate (invisible) triangles. Elements the new geometry has no data for (blend indices, colours, the tangent
/// frame) keep the section's first original value; a float3 second UV (usage 0x2A, the lightmap UV) takes the UV.
/// Other sections can be blanked (every triangle made degenerate), e.g. the same material in the lower LODs and the
/// shadow-only section, so the old shape doesn't show at a distance or cast shadows.
/// </summary>
public static class MeshWriter
{
    private const int Base = MeshSet.Base;

    public sealed record Section(int Lod, int Index, string Material, int VertexCount, int TriangleCount, int FirstIndex, int VertexOffset,
                                 List<(int Usage, int Format, int Size)> Elements, Guid Chunk, bool WideIndices, int VertexBufferBytes, int At);

    /// <summary>Every section of every chunk-backed LOD.</summary>
    public static List<Section> Sections(byte[] res)
    {
        var list = new List<Section>();
        for (int li = 0; li < 6; li++)
        {
            long o = BinaryPrimitives.ReadInt64LittleEndian(res.AsSpan(Base + 0x20 + 8 * li));
            if (o <= 0 || o >= res.Length) continue;
            int l = Base + (int)o;
            if (U32(res, l + 0x80) != uint.MaxValue) continue; // inline geometry: not written here
            var chunk = new Guid(res.AsSpan(l + 0x70, 16));
            int sections = (int)U32(res, l + 8);
            int at = Base + (int)BinaryPrimitives.ReadInt64LittleEndian(res.AsSpan(l + 12));
            bool wide = U32(res, l + 0x54) != 33;
            int vbytes = (int)U32(res, l + 0x5C);
            for (int s = 0; s < sections; s++)
            {
                int sp = at + s * 0x170;
                long mo = BinaryPrimitives.ReadInt64LittleEndian(res.AsSpan(sp + 8));
                string mat = mo > 0 && mo < res.Length ? Str(res, Base + (int)mo) : "";
                var elements = new List<(int, int, int)>();
                for (int e = 0; e < 16; e++)
                {
                    int usage = res[sp + 0x70 + e * 4], format = res[sp + 0x71 + e * 4];
                    int size = BinaryPrimitives.ReadUInt16LittleEndian(res.AsSpan(sp + 0xB0 + e * 2));
                    if (usage == 0 && size == 0) break;
                    elements.Add((usage, format, size));
                }
                list.Add(new Section(li, s, mat, (int)U32(res, sp + 0x2C), (int)U32(res, sp + 0x20), (int)U32(res, sp + 0x24),
                    (int)U32(res, sp + 0x28), elements, chunk, wide, vbytes, sp));
            }
        }
        return list;
    }

    /// <summary>Writes <paramref name="g"/> into section <paramref name="s"/> of the chunk bytes (changed in place).</summary>
    public static void Write(byte[] chunk, Section s, NewGeometry g)
    {
        int n = s.VertexCount;
        if (g.VertexCount > n) throw new InvalidDataException($"{g.VertexCount} vertices don't fit: the section has {n}.");
        if (g.TriangleCount > s.TriangleCount) throw new InvalidDataException($"{g.TriangleCount} triangles don't fit: the section has {s.TriangleCount}.");
        if (g.VertexCount == 0) { Blank(chunk, s); return; }
        int at = s.VertexOffset;
        foreach (var (usage, format, size) in s.Elements)
        {
            var first = chunk.AsSpan(at, size).ToArray();
            for (int v = 0; v < n; v++)
            {
                int src = v < g.VertexCount ? v : 0;
                var dst = chunk.AsSpan(at + v * size, size);
                if (usage == 1 && format == 3) for (int c = 0; c < 3; c++) BinaryPrimitives.WriteSingleLittleEndian(dst[(c * 4)..], g.Positions[src * 3 + c]);
                else if (usage == 0x21 && format == 6) for (int c = 0; c < 2; c++) BinaryPrimitives.WriteHalfLittleEndian(dst[(c * 2)..], (Half)g.Uvs[src * 2 + c]);
                else if (usage == 0x21 && format == 3 && size >= 8) for (int c = 0; c < 2; c++) BinaryPrimitives.WriteSingleLittleEndian(dst[(c * 4)..], g.Uvs[src * 2 + c]);
                else if (usage == 0x2A && format == 3 && size == 12)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(dst, Math.Clamp(g.Uvs[src * 2], 0, 1));
                    BinaryPrimitives.WriteSingleLittleEndian(dst[4..], Math.Clamp(g.Uvs[src * 2 + 1], 0, 1));
                    first.AsSpan(8, 4).CopyTo(dst[8..]);
                }
                else first.CopyTo(dst);
            }
            at += size * n;
        }
        int width = s.WideIndices ? 4 : 2, ib = s.VertexBufferBytes + s.FirstIndex * width;
        for (int i = 0; i < s.TriangleCount * 3; i++)
        {
            int value = i < g.Indices.Count ? g.Indices[i] : 0;
            if (s.WideIndices) BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(ib + i * 4), value);
            else BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(ib + i * 2), (ushort)value);
        }
    }

    /// <summary>Makes every triangle of a section degenerate (all indices 0): it draws nothing.</summary>
    public static void Blank(byte[] chunk, Section s)
    {
        int width = s.WideIndices ? 4 : 2, ib = s.VertexBufferBytes + s.FirstIndex * width;
        chunk.AsSpan(ib, s.TriangleCount * 3 * width).Clear();
    }

    /// <summary>
    /// The mesh header with its bounding box (two float4 at the start of the resource) grown to also hold
    /// <paramref name="min"/>..<paramref name="max"/>. The game culls a mesh by this box, so geometry outside it would
    /// vanish whenever the old box is off screen.
    /// </summary>
    public static byte[] GrowBounds(byte[] res, float[] min, float[] max)
    {
        var r = (byte[])res.Clone();
        for (int c = 0; c < 3; c++)
        {
            float lo = BinaryPrimitives.ReadSingleLittleEndian(r.AsSpan(Base + c * 4)), hi = BinaryPrimitives.ReadSingleLittleEndian(r.AsSpan(Base + 16 + c * 4));
            BinaryPrimitives.WriteSingleLittleEndian(r.AsSpan(Base + c * 4), Math.Min(lo, min[c]));
            BinaryPrimitives.WriteSingleLittleEndian(r.AsSpan(Base + 16 + c * 4), Math.Max(hi, max[c]));
        }
        return r;
    }

    /// <summary>
    /// The mesh header with section <paramref name="s"/>'s own bounding box (two float4 at +0x150 of its record) set to
    /// <paramref name="min"/>..<paramref name="max"/>. The game also culls each section by its box, so a section holding
    /// new geometry would vanish whenever its old box is small or off screen.
    /// </summary>
    public static void SetSectionBounds(byte[] res, Section s, float[] min, float[] max)
    {
        for (int c = 0; c < 3; c++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(res.AsSpan(s.At + 0x150 + c * 4), min[c]);
            BinaryPrimitives.WriteSingleLittleEndian(res.AsSpan(s.At + 0x160 + c * 4), max[c]);
        }
    }

    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));

    private static string Str(byte[] b, int p)
    {
        int e = Array.IndexOf(b, (byte)0, p);
        return System.Text.Encoding.ASCII.GetString(b, p, (e < 0 ? b.Length : e) - p);
    }
}
