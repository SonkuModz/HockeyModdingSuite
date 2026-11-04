using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>A drawable part of a mesh LOD: one material's triangles, with positions, normals and UVs.</summary>
public sealed class MeshPart
{
    public string Material { get; init; }
    public float[] Positions { get; init; }   // x, y, z per vertex
    public float[] Normals { get; init; }     // x, y, z per vertex (derived from the triangles when the mesh stores none)
    public float[] Uvs { get; init; }         // u, v per vertex (null when the part has none)
    /// <summary>The second UV set (usage 0x22), when the part has one: helmets map their decals with it.</summary>
    public float[] Uvs2 { get; init; }
    /// <summary>The third UV set (usage 0x23), when the part has one: the ice draws its decals (logo, ads) with it.</summary>
    public float[] Uvs3 { get; init; }
    /// <summary>The vertex declaration as "usage:format" pairs (hex), for diagnostics.</summary>
    public string Layout { get; init; }
    public int[] Indices { get; init; }       // three per triangle
    /// <summary>Skinning: <see cref="Influences"/> skeleton bone indices (skeleton_player's order) and weights per vertex; null when rigid.</summary>
    public ushort[] Bones { get; init; }
    public float[] Weights { get; init; }
    public int Influences { get; init; }
    public int VertexCount => Positions.Length / 3;
    public int TriangleCount => Indices.Length / 3;
}

/// <summary>
/// NHL 27's MeshSet resource (the RES of a MeshAsset/SkinnedMeshAsset), read far enough to draw it. Layout, as found
/// in the PS5 files (offsets in the header are relative to byte 16):
/// - header: AABB (2 × float4), 6 LOD offsets (u64);
/// - LOD: u32 type, u32 max instances, u32 section count, u64 sections offset, ... u32 flags at +0x50, u32 index format
///   (33 = 16-bit, else 32-bit), u32 index bytes, u32 vertex bytes, u32 inline data offset at +0x80;
/// - section (0x170 bytes): u64 material name offset at +8, u8 stride at +0x1E, u32 triangles at +0x20, u32 first index,
///   u32 first vertex, u32 vertex count; vertex declaration at +0x70: 16 × [usage, format, 0, index] then 16 × u16 sizes;
/// - geometry is inline at the end of the resource: per LOD, the vertex buffer then the index buffer.
/// NHL 24 (PS4) has the same fields with no 16-byte prefix (offsets from byte 0), the LOD's geometry chunk at +0x60 and
/// inline offset at +0x70, and 0xE0-byte sections whose vertex declaration starts at +0x4C.
/// </summary>
public sealed class MeshSet
{
    /// <summary>Where the PS5 header's offsets count from.</summary>
    public const int Ps5Base = 16;
    private readonly int Base = Ps5Base, ChunkAt = 0x70, InlineAt = 0x80, SectionSize = 0x170, DeclAt = 0x70;
    public string Name { get; private set; }
    public List<List<MeshPart>> Lods { get; } = new();
    public (float[] Min, float[] Max) Bounds { get; private set; }

    /// <param name="chunk">Reads a chunk (for LODs whose geometry is not inline), or null to skip those LODs.</param>
    /// <param name="ps4">The NHL 24 (PS4) layout.</param>
    /// <param name="meta">The RES's meta (NHL 24: its first word is where the inline geometry starts).</param>
    public MeshSet(byte[] b, Func<Guid, byte[]> chunk = null, bool ps4 = false, byte[] meta = null)
    {
        if (ps4) (Base, ChunkAt, InlineAt, SectionSize, DeclAt) = (0, 0x60, 0x70, 0xE0, 0x4C);
        Bounds = (new[] { F(b, Base), F(b, Base + 4), F(b, Base + 8) }, new[] { F(b, Base + 16), F(b, Base + 20), F(b, Base + 24) });
        var lods = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            long o = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(Base + 0x20 + 8 * i));
            if (o > 0 && o < b.Length) lods.Add(Base + (int)o);
        }
        long nameOff = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(Base + 0x58));
        Name = nameOff > 0 && nameOff < b.Length ? Str(b, Base + (int)nameOff) : "";

        // inline geometry: the end of the resource holds exactly the inline LODs' buffers (16-byte aligned). The resource
        // meta's first word is not where it starts in the stored RES, so it isn't used. A LOD whose inline offset is -1
        // keeps its geometry in the chunk named at +0x70 (vertex buffer, then index buffer).
        var spans = lods.Select(l => (Inline: U32(b, l + InlineAt), Index: U32(b, l + 0x58), Vertex: U32(b, l + 0x5C))).ToList();
        long total = spans.Where(s => s.Inline != uint.MaxValue).Select(s => (long)s.Inline + ((s.Index + s.Vertex + 15) & ~15)).DefaultIfEmpty(0).Max();
        int inlineStart = (int)(b.Length - total);
        // NHL 24 keeps a few more bytes after the inline geometry; the meta says where it starts
        if (ps4 && meta is { Length: >= 4 } && U32(meta, 0) is var start && start + total <= b.Length) inlineStart = (int)start;

        for (int li = 0; li < lods.Count; li++)
        {
            int l = lods[li];
            var parts = new List<MeshPart>();
            int sections = (int)U32(b, l + 8);
            int sectionsAt = Base + (int)BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(l + 12));
            bool wide = U32(b, l + 0x54) != 33;
            byte[] geo = b;
            int vtx;
            if (spans[li].Inline == uint.MaxValue)
            {
                var id = new Guid(b.AsSpan(l + ChunkAt, 16));
                geo = chunk?.Invoke(id);
                if (geo == null) { Lods.Add(parts); continue; }
                vtx = 0;
            }
            else vtx = inlineStart + (int)spans[li].Inline;
            int idx = vtx + (int)spans[li].Vertex;
            for (int s = 0; s < sections; s++)
            {
                var part = ReadSection(b, sectionsAt + s * SectionSize, geo, vtx, idx, wide);
                if (part != null) parts.Add(part);
            }
            Lods.Add(parts);
        }
    }

    private MeshPart ReadSection(byte[] b, int sp, byte[] geo, int vtxBuf, int idxBuf, bool wideIndices)
    {
        long matOff = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(sp + 8));
        int stride = b[sp + 0x1E];
        int tris = (int)U32(b, sp + 0x20), firstIndex = (int)U32(b, sp + 0x24), vertexBytes = (int)U32(b, sp + 0x28), count = (int)U32(b, sp + 0x2C);
        if (stride == 0 || count == 0 || tris == 0) return null;

        // the declaration: element usage, format and size. Each element is its own stream: a section's vertices start at
        // a byte offset in the LOD's vertex buffer and hold all its positions, then all of the next element, and so on.
        int decl = sp + DeclAt, at = vtxBuf + vertexBytes;
        (int Start, int Format, int Size) pos = (-1, 0, 0), nrm = (-1, 0, 0), uv = (-1, 0, 0), uv2 = (-1, 0, 0), uv3 = (-1, 0, 0);
        // blend indices (usages 2, 3: 4 × u16 each) and weights (usages 4, 5: 4 × u8 normalized, summing to 255)
        var bones = new List<int>();
        var weights = new List<int>();
        var layout = new StringBuilder();
        for (int e = 0; e < 16; e++)
        {
            byte usage = b[decl + e * 4], format = b[decl + e * 4 + 1];
            int size = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(decl + 64 + e * 2));
            if (usage == 0 && size == 0) break;
            if (usage == 1 && pos.Start < 0) pos = (at, format, size);
            else if (usage == 6 && nrm.Start < 0) nrm = (at, format, size);
            else if (usage == 0x21 && uv.Start < 0) uv = (at, format, size);
            else if (usage == 0x22 && uv2.Start < 0) uv2 = (at, format, size);
            else if (usage == 0x23 && uv3.Start < 0) uv3 = (at, format, size);
            else if (usage is 2 or 3 && format == 0x17 && size == 8) bones.Add(at);
            else if (usage is 4 or 5 && format == 0x0D && size == 4) weights.Add(at);
            layout.Append($"{usage:X2}:{format:X2} ");
            at += size * count;
        }
        if (pos.Start < 0) return null;

        var p = new float[count * 3];
        var n = new float[count * 3];
        var t = new float[count * 2];
        var t2 = uv2.Start >= 0 ? new float[count * 2] : null;
        var t3 = uv3.Start >= 0 ? new float[count * 2] : null;
        for (int v = 0; v < count; v++)
        {
            if (t2 != null) ReadVec(geo, uv2.Start + v * uv2.Size, uv2.Format, t2, v * 2, 2);
            if (t3 != null) ReadVec(geo, uv3.Start + v * uv3.Size, uv3.Format, t3, v * 2, 2);
            ReadVec(geo, pos.Start + v * pos.Size, pos.Format, p, v * 3, 3);
            if (nrm.Start >= 0) ReadVec(geo, nrm.Start + v * nrm.Size, nrm.Format, n, v * 3, 3);
            if (uv.Start >= 0) ReadVec(geo, uv.Start + v * uv.Size, uv.Format, t, v * 2, 2);
        }
        ushort[] bi = null;
        float[] bw = null;
        int k = Math.Min(bones.Count, weights.Count) * 4;
        if (k > 0)
        {
            bi = new ushort[count * k];
            bw = new float[count * k];
            for (int v = 0; v < count; v++)
                for (int j = 0; j < k; j++)
                {
                    bi[v * k + j] = BinaryPrimitives.ReadUInt16LittleEndian(geo.AsSpan(bones[j / 4] + v * 8 + (j % 4) * 2));
                    bw[v * k + j] = geo[weights[j / 4] + v * 4 + j % 4] / 255f;
                }
        }
        var ind = new int[tris * 3];
        for (int i = 0; i < ind.Length; i++)
            ind[i] = wideIndices ? (int)U32(geo, idxBuf + (firstIndex + i) * 4) : BinaryPrimitives.ReadUInt16LittleEndian(geo.AsSpan(idxBuf + (firstIndex + i) * 2));
        // helmets, gloves and socks keep their normals in another (packed) element; derive smooth ones from the triangles
        if (nrm.Start < 0)
            SmoothNormals(p, ind, n);
        return new MeshPart
        {
            Material = matOff > 0 && matOff < b.Length ? Str(b, Base + (int)matOff) : "",
            Positions = p, Normals = n, Uvs = uv.Start >= 0 ? t : null, Uvs2 = t2, Uvs3 = t3, Indices = ind, Layout = layout.ToString().Trim(),
            Bones = bi, Weights = bw, Influences = k,
        };
    }

    /// <summary>
    /// Area-weighted vertex normals from the triangles' winding (which matches the stored normals where a mesh has them).
    /// Vertices at the same position (UV seams) share one normal, so seams don't show as creases.
    /// </summary>
    private static void SmoothNormals(float[] p, int[] ind, float[] n)
    {
        int count = p.Length / 3;
        var key = new int[count];
        var byPos = new Dictionary<(float, float, float), int>();
        for (int v = 0; v < count; v++)
        {
            var k = (MathF.Round(p[v * 3], 4), MathF.Round(p[v * 3 + 1], 4), MathF.Round(p[v * 3 + 2], 4));
            if (!byPos.TryGetValue(k, out key[v])) byPos[k] = key[v] = byPos.Count;
        }
        var acc = new double[byPos.Count * 3];
        for (int t = 0; t + 2 < ind.Length; t += 3)
        {
            int a = ind[t], b = ind[t + 1], c = ind[t + 2];
            if (a >= count || b >= count || c >= count) continue;
            double ux = p[b * 3] - p[a * 3], uy = p[b * 3 + 1] - p[a * 3 + 1], uz = p[b * 3 + 2] - p[a * 3 + 2];
            double wx = p[c * 3] - p[a * 3], wy = p[c * 3 + 1] - p[a * 3 + 1], wz = p[c * 3 + 2] - p[a * 3 + 2];
            double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
            foreach (int v in new[] { a, b, c })
            {
                acc[key[v] * 3] += nx; acc[key[v] * 3 + 1] += ny; acc[key[v] * 3 + 2] += nz;
            }
        }
        for (int v = 0; v < count; v++)
        {
            int k = key[v] * 3;
            double len = Math.Sqrt(acc[k] * acc[k] + acc[k + 1] * acc[k + 1] + acc[k + 2] * acc[k + 2]);
            if (len <= 0) continue;
            n[v * 3] = (float)(acc[k] / len); n[v * 3 + 1] = (float)(acc[k + 1] / len); n[v * 3 + 2] = (float)(acc[k + 2] / len);
        }
    }

    /// <summary>Reads <paramref name="n"/> components of a vertex element: Float2/3/4 (formats 2, 3, 4), Half2/4 (6, 8).</summary>
    private static void ReadVec(byte[] b, int at, int format, float[] dst, int di, int n)
    {
        for (int c = 0; c < n; c++)
            dst[di + c] = format switch
            {
                2 or 3 or 4 => F(b, at + c * 4),
                6 or 8 => (float)BinaryPrimitives.ReadHalfLittleEndian(b.AsSpan(at + c * 2)),
                _ => 0,
            };
    }

    private static float F(byte[] b, int p) => BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(p));
    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));

    private static string Str(byte[] b, int p)
    {
        int e = Array.IndexOf(b, (byte)0, p);
        return Encoding.UTF8.GetString(b, p, (e < 0 ? b.Length : e) - p);
    }

    /// <summary>A Wavefront OBJ of one LOD (for checking a mesh in any 3D viewer).</summary>
    public string ToObj(int lod = 0)
    {
        var sb = new StringBuilder();
        int baseVertex = 1;
        foreach (var part in Lods[lod])
        {
            sb.Append("o ").AppendLine(part.Material);
            for (int v = 0; v < part.VertexCount; v++)
            {
                sb.Append(FormattableString.Invariant($"v {part.Positions[v * 3]} {part.Positions[v * 3 + 1]} {part.Positions[v * 3 + 2]}\n"));
                if (part.Uvs != null) sb.Append(FormattableString.Invariant($"vt {part.Uvs[v * 2]} {1 - part.Uvs[v * 2 + 1]}\n"));
                sb.Append(FormattableString.Invariant($"vn {part.Normals[v * 3]} {part.Normals[v * 3 + 1]} {part.Normals[v * 3 + 2]}\n"));
            }
            for (int i = 0; i < part.Indices.Length; i += 3)
            {
                int a = part.Indices[i] + baseVertex, c = part.Indices[i + 1] + baseVertex, d = part.Indices[i + 2] + baseVertex;
                sb.Append($"f {a}/{a}/{a} {c}/{c}/{c} {d}/{d}/{d}\n");
            }
            baseVertex += part.VertexCount;
        }
        return sb.ToString();
    }
}
