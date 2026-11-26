using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// Rebuilds the chunk geometry of an NHL 24 (PS4) mesh with new vertex and triangle counts per section, for porting
/// NHL 27 arena pieces into an NHL 24 level (PLAN §35). The RES keeps its length: only fields change.
/// - LOD: +0x54 index format (33 = 16-bit), +0x58 index buffer bytes, +0x5C vertex buffer bytes (padded to 16); the
///   chunk is the vertex buffer then the index buffer.
/// - Section (0xE0): +0x20 triangles, +0x24 first index, +0x28 vertex byte offset, +0x2C vertex count, +0xC0/+0xD0 box
///   (float4 min/max, w 0). Elements are separate streams in declaration order; indices are section-local.
/// - Header: box at 0/16; composite part count u32 at +0x98, part boxes (2 × float4 each) at the u64 at +0xA4. Every part
///   box is grown to cover the new geometry so nothing is culled by the old pieces.
/// Each host element is filled from the NHL 27 source's element of the same usage: same format and size are copied
/// raw (0x34 tangent frame: the encodings match, checked on structures/arizonageneric in both games); positions and
/// UVs are converted; part index (0x02), lightmap UV (0x2A) and anything the source lacks keep the host section's first
/// vertex. Mirrored placements get their winding flipped. Tangent frames are not rotated (the encoding isn't decoded),
/// so rotated pieces keep their unrotated shading normals.
/// </summary>
public static class MeshRebuild24
{
    /// <summary>"+z", "-z", "+x" or "-x": pieces on the other side take their lightmap point from the mirrored position on
    /// this side (when one side's points land on dark texels).</summary>
    public static string LightmapMirror;

    /// <summary>Let a LOD switch to 32-bit indices (format 46) when a section needs over 65,535 vertices.</summary>
    public static bool AllowWide = true;

    /// <param name="Uv">optional [scale u, scale v, offset u, offset v] for the first UV (e.g. into part of an atlas)</param>
    public sealed record Piece(MeshRaw.Section Source, float[] Transform, float[] Uv = null);

    public sealed class Result
    {
        public byte[] Res;
        public Dictionary<Guid, byte[]> Chunks = new();
        public List<string> Notes = new();
    }

    /// <param name="pieces">host section index (in LOD 0) → pieces; a listed empty list blanks the section; unlisted
    /// sections keep their geometry.</param>
    public static Result Build(byte[] res, Func<Guid, byte[]> chunk, Dictionary<int, List<Piece>> pieces, ISet<string> flatUv = null,
        ISet<string> doubleSided = null, IDictionary<string, float> ringUv = null, IDictionary<string, float[]> flatAt = null)
    {
        var host = new MeshRaw(res, chunk, ps4: true);
        var r = new Result { Res = (byte[])res.Clone() };
        var b = r.Res;
        float[] all = { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
        string lod0Mat(int i) => host.Sections.First(s => s.Lod == 0 && s.Index == i).Material;
        for (int li = 0; li < host.LodOffsets.Count; li++)
        {
            int l = host.LodOffsets[li];
            if (U32(b, l + 0x70) != uint.MaxValue) { r.Notes.Add($"LOD{li}: inline geometry, left alone"); continue; }
            var id = new Guid(b.AsSpan(l + 0x60, 16));
            var secs = host.Sections.Where(s => s.Lod == li).OrderBy(s => s.Index).ToList();
            // LOD n takes LOD 0's pieces by material name
            var map = new Dictionary<int, List<Piece>>();
            foreach (var (si, list) in pieces)
            {
                var s = li == 0 ? secs.FirstOrDefault(x => x.Index == si) : secs.FirstOrDefault(x => x.Material == lod0Mat(si));
                if (s != null) map[s.Index] = list;
            }
            _near = null;
            var vb = new MemoryStream();
            var ib = new List<int>();
            var counts = new List<(int Verts, int Tris, int First, int Offset, float[] Box, float Density)>();
            foreach (var s in secs)
            {
                var (streams, idx, n, box, dens) = map.TryGetValue(s.Index, out var list) ? Fill(s, list, flatUv?.Contains(s.Material) == true, doubleSided?.Contains(s.Material) == true,
                    ringUv != null && ringUv.TryGetValue(s.Material, out float ru) ? ru : 0,
                    flatAt != null && flatAt.TryGetValue(s.Material, out var fa) ? fa : null) : Keep(s);
                while (vb.Length % 16 != 0) vb.WriteByte(0);
                int offset = (int)vb.Length;
                foreach (var st in streams) vb.Write(st);
                counts.Add((n, idx.Length / 3, ib.Count, offset, box, dens));
                ib.AddRange(idx);
                if (box != null) for (int k = 0; k < 3; k++) { all[k] = Math.Min(all[k], box[k]); all[k + 3] = Math.Max(all[k + 3], box[k + 3]); }
            }
            while (vb.Length % 16 != 0) vb.WriteByte(0);
            bool wide = U32(b, l + 0x54) != 33;
            if (!wide && counts.Any(c => c.Verts > 65535))
            {
                if (!AllowWide) throw new InvalidDataException($"LOD{li}: a section has over 65,535 vertices and the LOD has 16-bit indices");
                W32(b, l + 0x54, 46); // 32-bit, as NHL 27's roof_middle_01_mesh uses
                wide = true;
                r.Notes.Add($"LOD{li}: switched to 32-bit indices (format 46)");
            }
            int width = wide ? 4 : 2;
            var data = new byte[vb.Length + ib.Count * width + 15 & ~15];
            vb.ToArray().CopyTo(data, 0);
            for (int i = 0; i < ib.Count; i++)
                if (wide) BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan((int)vb.Length + i * 4), ib[i]);
                else BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((int)vb.Length + i * 2), (ushort)ib[i]);
            W32(b, l + 0x58, (uint)(ib.Count * width));
            W32(b, l + 0x5C, (uint)vb.Length);
            for (int i = 0; i < secs.Count; i++)
            {
                int sp = secs[i].Record;
                var c = counts[i];
                W32(b, sp + 0x20, (uint)c.Tris);
                W32(b, sp + 0x24, (uint)c.First);
                W32(b, sp + 0x28, (uint)c.Offset);
                W32(b, sp + 0x2C, (uint)c.Verts);
                if (c.Box != null && map.ContainsKey(secs[i].Index) && c.Verts > 0) PutBox(b, sp + 0xC0, c.Box);
                // +0x34: the section's texture density (UV units per metre), which texture streaming picks mips from
                if (c.Density > 0 && map.ContainsKey(secs[i].Index) && c.Verts > 0)
                {
                    float old = BitConverter.ToSingle(b, sp + 0x34);
                    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(sp + 0x34), c.Density);
                    if (li == 0) r.Notes.Add($"  {secs[i].Material}: texture density {old:0.###} -> {c.Density:0.###} uv/m");
                }
            }
            r.Chunks[id] = data;
            r.Notes.Add($"LOD{li}: {counts.Sum(c => c.Verts):N0} vertices, {counts.Sum(c => c.Tris):N0} triangles, chunk {data.Length:N0} bytes");
        }
        if (all[0] <= all[3])
        {
            float[] Grow(float[] old) => new[] { Math.Min(old[0], all[0]), Math.Min(old[1], all[1]), Math.Min(old[2], all[2]), Math.Max(old[3], all[3]), Math.Max(old[4], all[4]), Math.Max(old[5], all[5]) };
            PutBox(b, 0, Grow(GetBox(b, 0)));
            int parts = (int)U32(b, 0x98);
            long at = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0xA4));
            if (parts > 0 && parts < 4096 && at > 0 && at + parts * 32 <= b.Length)
            {
                for (int p = 0; p < parts; p++) PutBox(b, (int)at + p * 32, Grow(GetBox(b, (int)at + p * 32)));
                r.Notes.Add($"{parts} part boxes grown");
            }
        }
        return r;
    }

    private static (List<byte[]> Streams, int[] Indices, int Count, float[] Box, float Density) Keep(MeshRaw.Section s) =>
        (s.Elements.Select(e => e.Data).ToList(), s.Indices, s.VertexCount, null, 0);

    private static (List<byte[]>, int[], int, float[], float) Fill(MeshRaw.Section host, List<Piece> list, bool flat = false, bool twoSided = false, float ring = 0, float[] flatUv = null)
    {
        _near = null;
        int n = list.Sum(p => p.Source.VertexCount);
        var streams = host.Elements.Select(e => new byte[e.Size * n]).ToList();
        var idx = new List<int>();
        float[] box = { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
        int baseV = 0;
        double uvArea = 0, worldArea = 0;
        foreach (var p in list)
        {
            var src = p.Source;
            var t = p.Transform ?? ArenaPlacement.Identity;
            bool mirror = Det(t) < 0;
            var sp = src.Get(1);
            var pos = new float[src.VertexCount * 3];
            for (int v = 0; v < src.VertexCount && sp != null; v++)
            {
                var q = MeshRaw.Floats(sp, v, 3);
                pos[v * 3] = q[0] * t[0] + q[1] * t[3] + q[2] * t[6] + t[9];
                pos[v * 3 + 1] = q[0] * t[1] + q[1] * t[4] + q[2] * t[7] + t[10];
                pos[v * 3 + 2] = q[0] * t[2] + q[1] * t[5] + q[2] * t[8] + t[11];
            }
            // one lightmap point per piece: the old host vertex nearest the piece's centre (per-vertex lookups banded)
            byte[] lmPiece = null;
            // only the host section's own old vertices: the lightmap element's last u32 identifies the section's chart, and
            // another section's entry renders flat white
            _near ??= NearIndex.Build(new List<MeshRaw.Section> { host });
            if (_near != null && src.VertexCount > 0)
            {
                double cx = 0, cy = 0, cz = 0;
                for (int v = 0; v < src.VertexCount; v++) { cx += pos[v * 3]; cy += pos[v * 3 + 1]; cz += pos[v * 3 + 2]; }
                var face = new double[6];
                for (int i = 0; i + 2 < src.Indices.Length; i += 3)
                {
                    int a = src.Indices[i], c = src.Indices[i + 1], e = src.Indices[i + 2];
                    if (Math.Max(a, Math.Max(c, e)) >= src.VertexCount) continue;
                    if (mirror) (c, e) = (e, c);
                    double ax = pos[c * 3] - pos[a * 3], ay = pos[c * 3 + 1] - pos[a * 3 + 1], az = pos[c * 3 + 2] - pos[a * 3 + 2];
                    double bx = pos[e * 3] - pos[a * 3], by = pos[e * 3 + 1] - pos[a * 3 + 1], bz = pos[e * 3 + 2] - pos[a * 3 + 2];
                    double[] nf = { ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx };
                    int k = Math.Abs(nf[0]) >= Math.Abs(nf[1]) && Math.Abs(nf[0]) >= Math.Abs(nf[2]) ? 0 : Math.Abs(nf[1]) >= Math.Abs(nf[2]) ? 1 : 2;
                    face[k * 2 + (nf[k] < 0 ? 1 : 0)] += Math.Sqrt(nf[0] * nf[0] + nf[1] * nf[1] + nf[2] * nf[2]);
                }
                int pieceAxis = face.Max() > 0 ? Array.IndexOf(face, face.Max()) : -1;
                float fx = (float)(cx / src.VertexCount), fy = (float)(cy / src.VertexCount), fz = (float)(cz / src.VertexCount);
                if (LightmapMirror is { Length: 2 } lmm)
                {
                    int sign = lmm[0] == '-' ? -1 : 1;
                    if (lmm[1] == 'z' && fz * sign < 0) { fz = -fz; if (pieceAxis >= 4) pieceAxis ^= 1; }
                    if (lmm[1] == 'x' && fx * sign < 0) { fx = -fx; if (pieceAxis is 0 or 1) pieceAxis ^= 1; }
                }
                lmPiece = _near.Find(fx, fy, fz, pieceAxis);
                if (Environment.GetEnvironmentVariable("NHL_LMDEBUG") == host.Material && lmPiece != null)
                    Console.WriteLine($"      lm {host.Material} {src.Material} axis={pieceAxis} c=({cx / src.VertexCount:0.0},{cy / src.VertexCount:0.0},{cz / src.VertexCount:0.0}) {Convert.ToHexString(lmPiece)}");
            }
            // ring: u from the distance around the ring (vertices by angle about the origin, cumulative xz path), at `ring` u per metre
            float[] ringU = null;
            if (ring > 0 && src.VertexCount > 1)
            {
                var order = Enumerable.Range(0, src.VertexCount).OrderBy(v => MathF.Atan2(pos[v * 3], -pos[v * 3 + 2])).ToArray(); // seam behind the +z end
                ringU = new float[src.VertexCount];
                float acc = 0;
                for (int k = 1; k < order.Length; k++)
                {
                    int a = order[k - 1], c = order[k];
                    acc += MathF.Sqrt((pos[a * 3] - pos[c * 3]) * (pos[a * 3] - pos[c * 3]) + (pos[a * 3 + 2] - pos[c * 3 + 2]) * (pos[a * 3 + 2] - pos[c * 3 + 2]));
                    ringU[c] = acc * ring;
                }
            }
            for (int ei = 0; ei < host.Elements.Count; ei++)
            {
                var he = host.Elements[ei];
                var se = src.Get(he.Usage);
                var dst = streams[ei];
                for (int v = 0; v < src.VertexCount; v++)
                {
                    var d = dst.AsSpan((baseV + v) * he.Size, he.Size);
                    // default: the host section's first vertex
                    if (host.VertexCount > 0) he.Data.AsSpan(0, he.Size).CopyTo(d); else d.Clear();
                    switch (he.Usage)
                    {
                        case 1:
                            float x = pos[v * 3], y = pos[v * 3 + 1], z = pos[v * 3 + 2];
                            Put(d, he.Format, he.Size, new[] { x, y, z });
                            box[0] = Math.Min(box[0], x); box[1] = Math.Min(box[1], y); box[2] = Math.Min(box[2], z);
                            box[3] = Math.Max(box[3], x); box[4] = Math.Max(box[4], y); box[5] = Math.Max(box[5], z);
                            break;
                        case 0x21 or 0x22 or 0x23 when flat:
                            if (flatUv != null) Put(d, he.Format, he.Size, flatUv); // a chosen texel
                            break; // strip/atlas host texture: every vertex keeps the host's first UV (one clean colour)
                        case 0x21 or 0x22 or 0x23:
                            var uv = se ?? src.Get(0x21);
                            if (uv != null)
                            {
                                var f2 = MeshRaw.Floats(uv, v, 2);
                                if (ringU != null) f2[0] = ringU[v];
                                if (p.Uv != null) { f2[0] = f2[0] * p.Uv[0] + p.Uv[2]; f2[1] = f2[1] * p.Uv[1] + p.Uv[3]; }
                                Put(d, he.Format, he.Size, f2);
                            }
                            break;
                        case 0x2A when lmPiece != null && he.Size == lmPiece.Length:
                            // lightmap UV: the nearest old host vertex's (in world space), so the bowl picks up the
                            // level's baked light and shadow around where it sits
                            lmPiece.CopyTo(d);
                            break;
                        case 2 or 0x2A:
                            break; // host part index / lightmap UV
                        default:
                            if (se != null && se.Format == he.Format && se.Size == he.Size)
                                se.Data.AsSpan(v * se.Size, se.Size).CopyTo(d);
                            break;
                    }
                }
            }
            var uvs = flat ? null : src.Get(0x21);
            for (int i = 0; i + 2 < src.Indices.Length; i += 3)
            {
                int a = src.Indices[i], c = src.Indices[i + 1], e = src.Indices[i + 2];
                if (uvs != null && Math.Max(a, Math.Max(c, e)) < src.VertexCount)
                {
                    float U(int v) => (ringU != null ? ringU[v] : MeshRaw.Floats(uvs, v, 2)[0]) * (p.Uv?[0] ?? 1);
                    float V(int v) => MeshRaw.Floats(uvs, v, 2)[1] * (p.Uv?[1] ?? 1);
                    double ua = U(c) - U(a), va = V(c) - V(a), ub = U(e) - U(a), vb2 = V(e) - V(a);
                    double ax = pos[c * 3] - pos[a * 3], ay = pos[c * 3 + 1] - pos[a * 3 + 1], az = pos[c * 3 + 2] - pos[a * 3 + 2];
                    double bx = pos[e * 3] - pos[a * 3], by = pos[e * 3 + 1] - pos[a * 3 + 1], bz = pos[e * 3 + 2] - pos[a * 3 + 2];
                    double cxp = ay * bz - az * by, cyp = az * bx - ax * bz, czp = ax * by - ay * bx;
                    uvArea += Math.Abs(ua * vb2 - va * ub) / 2;
                    worldArea += Math.Sqrt(cxp * cxp + cyp * cyp + czp * czp) / 2;
                }
                if (mirror) (c, e) = (e, c);
                idx.Add(baseV + a); idx.Add(baseV + c); idx.Add(baseV + e);
                if (twoSided) { idx.Add(baseV + a); idx.Add(baseV + e); idx.Add(baseV + c); } // back face (NHL 27 draws these two-sided)
            }
            baseV += src.VertexCount;
        }
        return (streams, idx.ToArray(), n, n > 0 ? box : null, worldArea > 0 && uvArea > 0 ? (float)Math.Sqrt(uvArea / worldArea) : 0);
    }

    [ThreadStatic] private static NearIndex _near;

    /// <summary>The host LOD's old vertices (world position → raw lightmap UV element) on a 2 m grid, for nearest lookups.</summary>
    private sealed class NearIndex
    {
        public int Size;
        private readonly List<(float X, float Y, float Z, byte[] Lm, int Axis)> _pts = new();
        private readonly Dictionary<(int, int, int), List<int>> _grid = new();
        private const float Cell = 2f;

        public static NearIndex Build(List<MeshRaw.Section> secs)
        {
            var r = new NearIndex();
            foreach (var s in secs)
            {
                var p = s.Get(1); var lm = s.Get(0x2A); var tf = s.Get(0x34);
                if (tf != null && tf.Size != 4) tf = null;
                if (p == null || lm == null || s.VertexCount == 0) continue;
                if (r.Size == 0) r.Size = lm.Size; else if (lm.Size != r.Size) continue;
                for (int v = 0; v < s.VertexCount; v++)
                {
                    var q = MeshRaw.Floats(p, v, 3);
                    var key = ((int)MathF.Floor(q[0] / Cell), (int)MathF.Floor(q[1] / Cell), (int)MathF.Floor(q[2] / Cell));
                    (r._grid.TryGetValue(key, out var l) ? l : r._grid[key] = new()).Add(r._pts.Count);
                    int axis = tf == null ? -1 : (int)((BinaryPrimitives.ReadUInt32LittleEndian(tf.Data.AsSpan(v * 4)) >> 24) & 7);
                    r._pts.Add((q[0], q[1], q[2], lm.Data.AsSpan(v * lm.Size, lm.Size).ToArray(), axis));
                }
            }
            return r._pts.Count > 0 ? r : null;
        }

        /// <param name="axis">the piece's main facing (+x 0, -x 1, +y 2, -y 3, +z 4, -z 5) or -1: old vertices facing
        /// another way count as 4 m further (so a wall doesn't take a texel from the seats' undersides)</param>
        public byte[] Find(float x, float y, float z, int axis = -1)
        {
            int cx = (int)MathF.Floor(x / Cell), cy = (int)MathF.Floor(y / Cell), cz = (int)MathF.Floor(z / Cell);
            int best = -1; float bd = float.MaxValue;
            for (int rad = 0; rad < 5 && (best < 0 || (rad - 1) * Cell < MathF.Sqrt(bd)); rad++)
                for (int i = -rad; i <= rad; i++)
                    for (int j = -rad; j <= rad; j++)
                        for (int k = -rad; k <= rad; k++)
                        {
                            if (Math.Max(Math.Abs(i), Math.Max(Math.Abs(j), Math.Abs(k))) != rad) continue;
                            if (!_grid.TryGetValue((cx + i, cy + j, cz + k), out var l)) continue;
                            foreach (int pi in l)
                            {
                                var p = _pts[pi];
                                float dd = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) + (p.Z - z) * (p.Z - z);
                                if (axis >= 0 && p.Axis != axis) dd += 16;
                                if (dd < bd) { bd = dd; best = pi; }
                            }
                        }
            if (best < 0) // far from the old bowl: brute force over a sample
            {
                int step = Math.Max(1, _pts.Count / 4000);
                for (int pi = 0; pi < _pts.Count; pi += step)
                {
                    var p = _pts[pi];
                    float dd = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) + (p.Z - z) * (p.Z - z);
                    if (dd < bd) { bd = dd; best = pi; }
                }
            }
            return _pts[best].Lm;
        }
    }

    private static float Det(float[] t) =>
        t[0] * (t[4] * t[8] - t[5] * t[7]) - t[1] * (t[3] * t[8] - t[5] * t[6]) + t[2] * (t[3] * t[7] - t[4] * t[6]);

    private static void Put(Span<byte> d, int format, int size, float[] v)
    {
        for (int c = 0; c < v.Length; c++)
            switch (format)
            {
                case 2 or 3 or 4 when c * 4 + 4 <= size: BinaryPrimitives.WriteSingleLittleEndian(d[(c * 4)..], v[c]); break;
                case 6 or 8 when c * 2 + 2 <= size: BinaryPrimitives.WriteHalfLittleEndian(d[(c * 2)..], (Half)v[c]); break;
            }
    }

    private static float[] GetBox(byte[] b, int at) => new[]
    {
        BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at)), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at + 4)), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at + 8)),
        BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at + 16)), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at + 20)), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at + 24)),
    };

    private static void PutBox(byte[] b, int at, float[] box)
    {
        for (int k = 0; k < 3; k++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at + k * 4), box[k]);
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at + 16 + k * 4), box[k + 3]);
        }
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at + 12), 0);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at + 28), 0);
    }

    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
    private static void W32(byte[] b, int p, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p), v);
}
