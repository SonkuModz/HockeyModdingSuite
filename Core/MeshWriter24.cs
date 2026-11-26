using System.Buffers.Binary;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>
/// Writes new geometry into sections of an NHL 24 (PS4) mesh whose LOD geometry lives in a chunk, in place: each
/// section keeps its vertex count, triangle count, vertex format and buffer offsets, so only the chunk's bytes change.
/// The PS4 layout (<see cref="MeshSet"/>): offsets from byte 0, LOD chunk id at +0x60, inline offset at +0x70,
/// 0xE0-byte sections with the vertex declaration at +0x4C (16 × [usage, format, 0, index], then 16 × u16 sizes).
/// Positions (usage 1) and UVs (usage 0x21) are written as half floats (format 8: half4, w kept from the section's
/// first vertex; format 6: half2) or floats (format 3). Every other element keeps the section's first original value.
/// Unused vertices copy the first new one; unused triangles become degenerate. An empty section is blanked.
/// Used for the IHN replay wipe (the Play Now flat wipe's logo meshes, 2026-11-25).
/// </summary>
public static class MeshWriter24
{
    public sealed record Section(int Lod, int Index, string Material, int VertexCount, int TriangleCount, int FirstIndex, int VertexOffset,
                                 List<(int Usage, int Format, int Size)> Elements, Guid Chunk, bool WideIndices, int VertexBufferBytes);

    public sealed class Geometry
    {
        public List<float> Positions { get; init; } = new();
        public List<float> Uvs { get; init; } = new();
        public List<int> Indices { get; init; } = new();
        public int VertexCount => Positions.Count / 3;
        public int TriangleCount => Indices.Count / 3;
    }

    public static List<Section> Sections(byte[] res)
    {
        var list = new List<Section>();
        for (int li = 0; li < 6; li++)
        {
            long o = BinaryPrimitives.ReadInt64LittleEndian(res.AsSpan(0x20 + 8 * li));
            if (o <= 0 || o >= res.Length) continue;
            int l = (int)o;
            if (U32(res, l + 0x70) != uint.MaxValue) continue; // inline geometry: not written here
            var chunk = new Guid(res.AsSpan(l + 0x60, 16));
            int sections = (int)U32(res, l + 8);
            int at = (int)BinaryPrimitives.ReadInt64LittleEndian(res.AsSpan(l + 12));
            bool wide = U32(res, l + 0x54) != 33;
            int vbytes = (int)U32(res, l + 0x5C);
            for (int s = 0; s < sections; s++)
            {
                int sp = at + s * 0xE0;
                long mo = BinaryPrimitives.ReadInt64LittleEndian(res.AsSpan(sp + 8));
                string mat = mo > 0 && mo < res.Length ? Str(res, (int)mo) : "";
                var elements = new List<(int, int, int)>();
                for (int e = 0; e < 16; e++)
                {
                    int usage = res[sp + 0x4C + e * 4], format = res[sp + 0x4D + e * 4];
                    int size = BinaryPrimitives.ReadUInt16LittleEndian(res.AsSpan(sp + 0x4C + 64 + e * 2));
                    if (usage == 0 && size == 0) break;
                    elements.Add((usage, format, size));
                }
                list.Add(new Section(li, s, mat, (int)U32(res, sp + 0x2C), (int)U32(res, sp + 0x20), (int)U32(res, sp + 0x24),
                    (int)U32(res, sp + 0x28), elements, chunk, wide, vbytes));
            }
        }
        return list;
    }

    public static void Write(byte[] chunk, Section s, Geometry g)
    {
        int n = s.VertexCount;
        if (g.VertexCount > n) throw new InvalidDataException($"LOD{s.Lod} s{s.Index}: {g.VertexCount} vertices don't fit: the section has {n}.");
        if (g.TriangleCount > s.TriangleCount) throw new InvalidDataException($"LOD{s.Lod} s{s.Index}: {g.TriangleCount} triangles don't fit: the section has {s.TriangleCount}.");
        if (g.VertexCount == 0) { Blank(chunk, s); return; }
        if (g.Indices.Any(i => i < 0 || i >= g.VertexCount)) throw new InvalidDataException($"LOD{s.Lod} s{s.Index}: an index is out of range.");
        int at = s.VertexOffset;
        foreach (var (usage, format, size) in s.Elements)
        {
            var first = chunk.AsSpan(at, size).ToArray();
            for (int v = 0; v < n; v++)
            {
                int src = v < g.VertexCount ? v : 0;
                var dst = chunk.AsSpan(at + v * size, size);
                first.CopyTo(dst);
                if (usage == 1) Put(dst, format, size, g.Positions, src * 3, 3);
                else if (usage == 0x21 && g.Uvs.Count == g.VertexCount * 2) Put(dst, format, size, g.Uvs, src * 2, 2);
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

    private static void Put(Span<byte> dst, int format, int size, List<float> src, int si, int n)
    {
        switch (format)
        {
            case 6 or 8:
                for (int c = 0; c < n && c * 2 + 2 <= size; c++) BinaryPrimitives.WriteHalfLittleEndian(dst[(c * 2)..], (Half)src[si + c]);
                break;
            case 2 or 3 or 4:
                for (int c = 0; c < n && c * 4 + 4 <= size; c++) BinaryPrimitives.WriteSingleLittleEndian(dst[(c * 4)..], src[si + c]);
                break;
            default:
                throw new NotSupportedException($"vertex format {format} can't be written");
        }
    }

    public static void Blank(byte[] chunk, Section s)
    {
        int width = s.WideIndices ? 4 : 2, ib = s.VertexBufferBytes + s.FirstIndex * width;
        chunk.AsSpan(ib, s.TriangleCount * 3 * width).Clear();
    }

    /// <summary>
    /// Reads a geometry file: {"sections": {"LOD:INDEX": {"pos": [x,y,z,...], "uv": [u,v,...], "tri": [a,b,c,...]} or
    /// null (blank)}}. Sections not listed are left as they are.
    /// </summary>
    public static Dictionary<(int Lod, int Index), Geometry> Load(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var result = new Dictionary<(int, int), Geometry>();
        foreach (var p in doc.RootElement.GetProperty("sections").EnumerateObject())
        {
            var key = p.Name.Split(':').Select(int.Parse).ToArray();
            if (p.Value.ValueKind == JsonValueKind.Null) { result[(key[0], key[1])] = new Geometry(); continue; }
            var g = new Geometry
            {
                Positions = p.Value.GetProperty("pos").EnumerateArray().Select(x => x.GetSingle()).ToList(),
                Uvs = p.Value.TryGetProperty("uv", out var uv) ? uv.EnumerateArray().Select(x => x.GetSingle()).ToList() : new(),
                Indices = p.Value.GetProperty("tri").EnumerateArray().Select(x => x.GetInt32()).ToList(),
            };
            result[(key[0], key[1])] = g;
        }
        return result;
    }

    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));

    private static string Str(byte[] b, int p)
    {
        int e = Array.IndexOf(b, (byte)0, p);
        return System.Text.Encoding.UTF8.GetString(b, p, (e < 0 ? b.Length : e) - p);
    }
}
