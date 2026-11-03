using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// Frostbite's binary "DbObject" format (layout.toc): a type byte (low 5 bits; bit 7 = no name), a name, a value.
/// Objects become Dictionary&lt;string, object&gt;, lists List&lt;object&gt;.
/// </summary>
public static class DbObject
{
    /// <summary>Where a TOC's data starts: 556 after a signed header (0x01CED100 / 0x00D1CE00, PS4), else 0 (PS5).</summary>
    public static int DataStart(byte[] b) => b.Length > 556 && b[1] == 0xD1 && b[2] == 0xCE ? 556 : 0;

    /// <summary>Reads a TOC file, signed or not.</summary>
    public static Dictionary<string, object> ReadFile(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int p = DataStart(b);
        return Read(b, ref p, out _) as Dictionary<string, object>;
    }

    public static object Read(byte[] b, ref int p, out string name)
    {
        name = null;
        byte t = b[p++];
        int type = t & 0x1F;
        if (type == 0)
            return null;
        if ((t & 0x80) == 0)
        {
            int e = Array.IndexOf(b, (byte)0, p);
            name = Encoding.UTF8.GetString(b, p, e - p);
            p = e + 1;
        }
        switch (type)
        {
            case 1:
            case 2:
            {
                long n = Read7Bit(b, ref p);
                long end = p + n;
                var list = type == 1 ? new List<object>() : null;
                var dict = type == 2 ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) : null;
                while (p < end)
                {
                    var v = Read(b, ref p, out string k);
                    if (v == null)
                        break;
                    if (list != null) list.Add(v);
                    else dict[k] = v;
                }
                p = (int)end;
                return (object)list ?? dict;
            }
            case 6: return b[p++] == 1;
            case 7:
            {
                int n = (int)Read7Bit(b, ref p);
                string s = Encoding.UTF8.GetString(b, p, n).TrimEnd('\0');
                p += n;
                return s;
            }
            case 8: { int v = BitConverter.ToInt32(b, p); p += 4; return v; }
            case 9:
            case 10: { long v = BitConverter.ToInt64(b, p); p += 8; return v; }
            case 11: { float v = BitConverter.ToSingle(b, p); p += 4; return v; }
            case 12: { double v = BitConverter.ToDouble(b, p); p += 8; return v; }
            case 15: { var v = new Guid(b.AsSpan(p, 16)); p += 16; return v; }
            case 16: { var v = b[p..(p + 20)]; p += 20; return v; }
            case 19:
            {
                int n = (int)Read7Bit(b, ref p);
                var v = b[p..(p + n)];
                p += n;
                return v;
            }
            default:
                throw new InvalidDataException($"DbObject type {type} at {p}");
        }
    }

    private static long Read7Bit(byte[] b, ref int p)
    {
        long r = 0;
        int shift = 0;
        while (true)
        {
            byte c = b[p++];
            r |= (long)(c & 0x7F) << shift;
            if ((c & 0x80) == 0)
                return r;
            shift += 7;
        }
    }
}
