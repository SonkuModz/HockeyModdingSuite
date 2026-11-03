using System.Buffers.Binary;
using System.Text;
using Iced.Intel;
using Decoder = Iced.Intel.Decoder;

namespace NHL27Kit.Core;

public enum TunableType { Bool, Int, Float }

/// <summary>One gameplay tuning value compiled into the eboot: its default and range, group path and readable label.</summary>
public sealed record Tunable(string Name, TunableType Type, double? Default, double? Min, double? Max, string Group, string Label, long Site)
{
    public string TypeText => Type switch { TunableType.Bool => "bool", TunableType.Int => "int", _ => "float" };
    public string DefaultText => Show(Default);
    public string MinText => Show(Min);
    public string MaxText => Show(Max);
    public string SiteText => $"0x{Site:X}";
    /// <summary>Names a rating or attribute (gBackskatingRatingModifierForward, gAIJumpOverAttribMinChance …).</summary>
    public bool IsRating => Has(Name) || Has(Label);

    private static bool Has(string s) => s != null && (s.Contains("Rating", StringComparison.OrdinalIgnoreCase) || s.Contains("Attrib", StringComparison.OrdinalIgnoreCase));

    private string Show(double? v) => v is not double d ? "" : Type switch
    {
        TunableType.Bool => d != 0 ? "true" : "false",
        TunableType.Int => ((long)d).ToString(),
        _ => ((float)d).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// Reads the tuning values the eboot registers at start-up. Each is a static object built by a constructor called as
/// ctor(this, group, name, label, values…): the group, name and label are RIP-relative leas into read-only data, a bool's
/// default is in r8b, an int's in r8d/r9d/[rsp] (default, min, max), a float's in xmm0/xmm1/xmm2. The constructors are
/// found by what they store as the type code ([this+0x20] = 1 bool, 2 int, 4 float), so a game update that moves them
/// still reads. Read only; see docs/eboot/README.md.
/// </summary>
public static class Tunables
{
    private sealed class Segment { public long Va, Size, Offset; }

    public static List<Tunable> Read(string eboot)
    {
        byte[] b = File.ReadAllBytes(eboot);
        var segs = Segments(b);
        var code = segs.FirstOrDefault(s => s.Va == 0) ?? throw new InvalidDataException("eboot.bin has no code segment at address 0 (is it decrypted?)");

        long Off(long va)
        {
            foreach (var s in segs)
                if (va >= s.Va && va < s.Va + s.Size) return s.Offset + va - s.Va;
            return -1;
        }
        string CStr(long va)
        {
            long o = Off(va);
            if (o < 0) return null;
            int e = Array.IndexOf(b, (byte)0, (int)o, (int)Math.Min(512, b.Length - o));
            return e < 0 ? null : Encoding.Latin1.GetString(b, (int)o, e - (int)o);
        }
        float? F32(long va)
        {
            long o = Off(va);
            return o < 0 ? null : BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan((int)o, 4));
        }

        // every "\0gName\0" string in read-only data
        var names = new Dictionary<long, string>();
        foreach (var s in segs.Where(s => s.Va != 0))
        {
            int end = (int)(s.Offset + s.Size);
            for (int i = (int)s.Offset + 1; i < end - 2; i++)
            {
                if (b[i] != (byte)'g' || b[i - 1] != 0 || b[i + 1] < 'A' || b[i + 1] > 'Z') continue;
                int j = i + 1;
                while (j < end && (b[j] is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'_')) j++;
                if (j < end && b[j] == 0 && j - i > 1) names.TryAdd(s.Va + i - s.Offset, Encoding.ASCII.GetString(b, i, j - i));
                i = j;
            }
        }

        // every REX.W lea r, [rip+disp32] in the code that points at one
        var sites = new List<(long Site, string Name)>();
        int c0 = (int)code.Offset, c1 = (int)(code.Offset + code.Size) - 7;
        for (int i = c0; i < c1; i++)
        {
            if (b[i + 1] != 0x8D || (b[i] != 0x48 && b[i] != 0x4C) || (b[i + 2] & 0xC7) != 0x05) continue;
            long site = i - c0;
            long target = site + 7 + BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(i + 3, 4));
            if (names.TryGetValue(target, out var n)) sites.Add((site, n));
        }

        var kinds = new Dictionary<ulong, TunableType?>();
        TunableType? Kind(ulong target)
        {
            if (kinds.TryGetValue(target, out var k)) return k;
            k = null;
            long o = Off((long)target);
            if (o >= 0 && target < (ulong)code.Size)
            {
                var d = Decoder.Create(64, new ByteArrayCodeReader(b, (int)o, (int)Math.Min(0x100, b.Length - o)), target);
                for (int n = 0; n < 60 && d.IP < target + 0x100; n++)
                {
                    var x = d.Decode();
                    if (x.Mnemonic == Mnemonic.Ret) break;
                    if (x.Mnemonic == Mnemonic.Mov && x.Op0Kind == OpKind.Memory && x.MemoryDisplacement64 == 0x20 && x.MemorySize == MemorySize.UInt32 && x.Op1Kind == OpKind.Immediate32)
                    {
                        k = x.Immediate32 switch { 1 => TunableType.Bool, 2 => TunableType.Int, 4 => TunableType.Float, _ => null };
                        break;
                    }
                }
            }
            kinds[target] = k;
            return k;
        }

        var rows = new List<Tunable>();
        var seen = new HashSet<(string, long)>();
        foreach (var (site, name) in sites)
        {
            if (!seen.Add((name, site))) continue;
            var list = Window(b, c0, code.Size, site);
            if (list == null) continue;
            int k = list.FindIndex(x => (long)x.IP == site);
            int c = list.FindIndex(k, x => x.Mnemonic == Mnemonic.Call);
            if (c < 0 || list[c].Op0Kind != OpKind.NearBranch64) continue;
            if (Kind(list[c].NearBranch64) is not TunableType kind) continue;
            int s0 = list.FindLastIndex(k, x => x.Mnemonic == Mnemonic.Call) + 1;

            var ptr = new Dictionary<Register, long>();
            var imm = new Dictionary<Register, long>();
            var xmm = new Dictionary<Register, float?>();
            long? stack = null;
            for (int j = s0; j < c; j++)
            {
                var x = list[j];
                Register r0 = x.Op0Kind == OpKind.Register ? x.Op0Register.GetFullRegister() : Register.None;
                bool rip = x.Op1Kind == OpKind.Memory && x.IsIPRelativeMemoryOperand;
                switch (x.Mnemonic)
                {
                    case Mnemonic.Lea when rip:
                        ptr[r0] = (long)x.IPRelativeMemoryAddress;
                        imm.Remove(r0);
                        break;
                    case Mnemonic.Mov when r0 != Register.None && x.Op1Kind == OpKind.Register:
                        var r1 = x.Op1Register.GetFullRegister();
                        if (ptr.TryGetValue(r1, out var p)) ptr[r0] = p; else ptr.Remove(r0);
                        if (imm.TryGetValue(r1, out var v)) imm[r0] = v; else imm.Remove(r0);
                        break;
                    case Mnemonic.Mov when IsImm(x.Op1Kind):
                        long value = x.Op1Kind == OpKind.Immediate32 ? (int)x.Immediate32 : (long)x.GetImmediate(1);
                        if (r0 != Register.None) { imm[r0] = value; ptr.Remove(r0); }
                        else if (x.Op0Kind == OpKind.Memory && x.MemoryBase == Register.RSP && x.MemoryIndex == Register.None && x.MemoryDisplacement64 == 0) stack = value;
                        break;
                    // the last push before the call is the first stack argument (an int's max)
                    case Mnemonic.Push when IsImm(x.Op0Kind):
                        stack = x.Op0Kind == OpKind.Immediate8to64 || x.Op0Kind == OpKind.Immediate32to64 ? (long)x.GetImmediate(0) : (int)x.GetImmediate(0);
                        break;
                    case Mnemonic.Push when r0 != Register.None:
                        stack = imm.TryGetValue(r0, out var pv) ? pv : null;
                        break;
                    case Mnemonic.Xor when r0 != Register.None && x.Op1Kind == OpKind.Register && x.Op1Register.GetFullRegister() == r0:
                        imm[r0] = 0;
                        ptr.Remove(r0);
                        break;
                    case Mnemonic.Vmovss when x.Op0Kind == OpKind.Register && rip && x.OpCount == 2:
                        xmm[x.Op0Register] = F32((long)x.IPRelativeMemoryAddress);
                        break;
                    case Mnemonic.Vxorps or Mnemonic.Vxorpd when x.OpCount == 3 && x.Op1Kind == OpKind.Register && x.Op2Kind == OpKind.Register && x.Op1Register == x.Op2Register:
                        xmm[Xmm(x.Op0Register)] = 0f;
                        break;
                    case Mnemonic.Vmovaps or Mnemonic.Vmovss when x.OpCount == 2 && x.Op0Kind == OpKind.Register && x.Op1Kind == OpKind.Register:
                        xmm[Xmm(x.Op0Register)] = xmm.GetValueOrDefault(Xmm(x.Op1Register));
                        break;
                }
            }
            double? I(Register r) => imm.TryGetValue(r, out var v) ? v : null;
            double? X(Register r) => xmm.GetValueOrDefault(r) is float f ? double.Parse(f.ToString("G6", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture) : null;
            string group = ptr.TryGetValue(Register.RSI, out var g) ? CStr(g) : null;
            string label = ptr.TryGetValue(Register.RCX, out var l) ? CStr(l) : null;
            if (label == name) label = null;
            rows.Add(kind switch
            {
                TunableType.Float => new Tunable(name, kind, X(Register.XMM0), X(Register.XMM1), X(Register.XMM2), group, label, site),
                TunableType.Int => new Tunable(name, kind, I(Register.R8), I(Register.R9), stack, group, label, site),
                _ => new Tunable(name, kind, I(Register.R8) is double d ? (d != 0 ? 1 : 0) : null, null, null, group, label, site),
            });
        }

        // a group path kept in a register (rsi from r13) carries over from the registration before it in the same function
        rows.Sort((a, z) => a.Site.CompareTo(z.Site));
        (string Group, long Site)? last = null;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Group != null) last = (rows[i].Group, rows[i].Site);
            else if (last is { } p && rows[i].Site - p.Site < 0x2000) { rows[i] = rows[i] with { Group = p.Group }; last = (p.Group, rows[i].Site); }
        }
        rows.Sort((a, z) => string.CompareOrdinal(a.Name, z.Name));
        return rows;
    }

    /// <summary>Writes the list as CSV (the same columns as docs/eboot/tunables.csv).</summary>
    public static void WriteCsv(IEnumerable<Tunable> rows, string file)
    {
        static string Q(string s) => s == null ? "" : s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        var sb = new StringBuilder("name,type,default,min,max,group,label,site\n");
        foreach (var t in rows)
            sb.Append(string.Join(",", Q(t.Name), t.TypeText, t.DefaultText, t.MinText, t.MaxText, Q(t.Group), Q(t.Label), t.SiteText.ToLowerInvariant())).Append('\n');
        File.WriteAllText(file, sb.ToString());
    }

    private static bool IsImm(OpKind k) => k is OpKind.Immediate8 or OpKind.Immediate16 or OpKind.Immediate32 or OpKind.Immediate64
        or OpKind.Immediate8to16 or OpKind.Immediate8to32 or OpKind.Immediate8to64 or OpKind.Immediate32to64;

    private static Register Xmm(Register r) => r is >= Register.YMM0 and <= Register.YMM31 ? Register.XMM0 + (r - Register.YMM0) : r;

    /// <summary>The instructions around a site: decodes from the furthest start before it whose decoding lands on it.</summary>
    private static List<Instruction> Window(byte[] b, int codeOffset, long codeSize, long site)
    {
        for (int back = 0x50; back > 0; back--)
        {
            long start = site - back;
            if (start < 0) continue;
            int len = (int)Math.Min(back + 0x50, codeSize - start);
            var d = Decoder.Create(64, new ByteArrayCodeReader(b, codeOffset + (int)start, len), (ulong)start);
            var list = new List<Instruction>();
            bool hit = false;
            while (d.IP < (ulong)(start + len))
            {
                var x = d.Decode();
                if (x.IsInvalid) break;
                list.Add(x);
                if ((long)x.IP == site) hit = true;
            }
            if (hit) return list;
        }
        return null;
    }

    /// <summary>The loaded segments: the SELF's segment table gives each program header's place in the file.</summary>
    private static List<Segment> Segments(byte[] b)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(b) != 0xEEF51454) throw new InvalidDataException("eboot.bin isn't a decrypted PS5 SELF");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x18));
        int elf = 0x20 + count * 0x20;
        if (BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(elf)) != 0x464C457F) throw new InvalidDataException("No ELF header after the SELF segment table");
        long phOff = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(elf + 0x20));
        int phNum = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(elf + 0x38));
        var list = new List<Segment>();
        for (int i = 0; i < count; i++)
        {
            long flags = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x20 + i * 0x20));
            long off = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x28 + i * 0x20));
            long packed = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x30 + i * 0x20));
            long size = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x38 + i * 0x20));
            int ph = (int)(flags >> 20) & 0xFFF;
            if ((flags & 0x800) == 0 || ph >= phNum) continue;
            int p = elf + (int)phOff + ph * 56;
            if (BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p)) != 1) continue;
            long va = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p + 0x10));
            long fileSize = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p + 0x20));
            if (packed != size) throw new InvalidDataException("eboot.bin has compressed segments");
            list.Add(new Segment { Va = va, Size = Math.Min(size, fileSize), Offset = off });
        }
        return list;
    }
}
