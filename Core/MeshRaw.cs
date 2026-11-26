using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// A mesh's sections with their vertex elements as raw byte streams, for copying geometry between NHL 27 (PS5) and
/// NHL 24 (PS4) meshes without decoding every element. Locates the buffers the same way as <see cref="MeshSet"/>.
/// </summary>
public sealed class MeshRaw
{
    public sealed class Element
    {
        public int Usage, Format, Size;
        public byte[] Data; // Size bytes per vertex
    }

    public sealed class Section
    {
        public int Lod, Index, Record;        // Record: byte offset of the section record in the RES
        public string Material;
        public int MaterialIndex;              // byte +0x1C
        public int VertexCount, TriangleCount;
        public List<Element> Elements = new();
        public int[] Indices;                  // section-local
        public Element Get(int usage) => Elements.FirstOrDefault(e => e.Usage == usage);
    }

    public readonly bool Ps4;
    public readonly int Base, SectionSize, DeclAt, ChunkAt, InlineAt;
    public readonly List<int> LodOffsets = new();     // absolute
    public readonly List<Section> Sections = new();

    public MeshRaw(byte[] b, Func<Guid, byte[]> chunk, bool ps4, byte[] meta = null)
    {
        Ps4 = ps4;
        (Base, ChunkAt, InlineAt, SectionSize, DeclAt) = ps4 ? (0, 0x60, 0x70, 0xE0, 0x4C) : (16, 0x70, 0x80, 0x170, 0x70);
        for (int i = 0; i < 6; i++)
        {
            long o = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(Base + 0x20 + 8 * i));
            if (o > 0 && o < b.Length) LodOffsets.Add(Base + (int)o);
        }
        var spans = LodOffsets.Select(l => (Inline: U32(b, l + InlineAt), Index: U32(b, l + 0x58), Vertex: U32(b, l + 0x5C))).ToList();
        long total = spans.Where(s => s.Inline != uint.MaxValue).Select(s => (long)s.Inline + ((s.Index + s.Vertex + 15) & ~15)).DefaultIfEmpty(0).Max();
        int inlineStart = (int)(b.Length - total);
        if (ps4 && meta is { Length: >= 4 } && U32(meta, 0) is var start && start + total <= b.Length) inlineStart = (int)start;
        for (int li = 0; li < LodOffsets.Count; li++)
        {
            int l = LodOffsets[li];
            int count = (int)U32(b, l + 8);
            int at = Base + (int)BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(l + 12));
            bool wide = U32(b, l + 0x54) != 33;
            byte[] geo; int vtx;
            if (spans[li].Inline == uint.MaxValue)
            {
                geo = chunk?.Invoke(new Guid(b.AsSpan(l + ChunkAt, 16)));
                vtx = 0;
                if (geo == null) continue;
            }
            else { geo = b; vtx = inlineStart + (int)spans[li].Inline; }
            int idx = vtx + (int)spans[li].Vertex;
            for (int s = 0; s < count; s++)
            {
                int sp = at + s * SectionSize;
                var sec = new Section
                {
                    Lod = li, Index = s, Record = sp, MaterialIndex = b[sp + 0x1C],
                    TriangleCount = (int)U32(b, sp + 0x20), VertexCount = (int)U32(b, sp + 0x2C),
                };
                long mo = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(sp + 8));
                sec.Material = mo > 0 && mo < b.Length ? Str(b, Base + (int)mo) : "";
                int first = (int)U32(b, sp + 0x24), vbytes = (int)U32(b, sp + 0x28);
                int p = vtx + vbytes;
                for (int e = 0; e < 16; e++)
                {
                    int usage = b[sp + DeclAt + e * 4], format = b[sp + DeclAt + e * 4 + 1];
                    int size = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(sp + DeclAt + 64 + e * 2));
                    if (usage == 0 && size == 0) break;
                    var el = new Element { Usage = usage, Format = format, Size = size, Data = new byte[size * sec.VertexCount] };
                    if (p + el.Data.Length <= geo.Length) Buffer.BlockCopy(geo, p, el.Data, 0, el.Data.Length);
                    sec.Elements.Add(el);
                    p += el.Data.Length;
                }
                sec.Indices = new int[sec.TriangleCount * 3];
                for (int i = 0; i < sec.Indices.Length; i++)
                {
                    int ip = idx + (first + i) * (wide ? 4 : 2);
                    if (ip + (wide ? 4 : 2) > geo.Length) break;
                    sec.Indices[i] = wide ? (int)U32(geo, ip) : BinaryPrimitives.ReadUInt16LittleEndian(geo.AsSpan(ip));
                }
                Sections.Add(sec);
            }
        }
    }

    public static float[] Floats(Element e, int v, int n)
    {
        var r = new float[n];
        for (int c = 0; c < n; c++)
            r[c] = e.Format switch
            {
                2 or 3 or 4 => c * 4 + 4 <= e.Size ? BinaryPrimitives.ReadSingleLittleEndian(e.Data.AsSpan(v * e.Size + c * 4)) : 0,
                6 or 8 => c * 2 + 2 <= e.Size ? (float)BinaryPrimitives.ReadHalfLittleEndian(e.Data.AsSpan(v * e.Size + c * 2)) : 0,
                _ => 0,
            };
        return r;
    }

    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
    private static string Str(byte[] b, int p)
    {
        int e = Array.IndexOf(b, (byte)0, p);
        return Encoding.UTF8.GetString(b, p, (e < 0 ? b.Length : e) - p);
    }
}
