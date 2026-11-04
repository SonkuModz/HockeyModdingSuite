using System.Buffers.Binary;
using System.Text;
using Iced.Intel;
using Decoder = Iced.Intel.Decoder;

namespace NHL27Kit.Core;

public enum TunableType { Bool, Int, Float }

/// <summary>One gameplay tuning value compiled into the eboot: its default and range, group path and readable label.</summary>
/// <summary>What the bytes a PS4 value comes from hold.</summary>
public enum ValueKind
{
    /// <summary>A byte immediate (a bool).</summary>
    Imm8,
    /// <summary>A 4-byte immediate that is the value (any upper half is left as it is, or zeroed).</summary>
    Imm32,
    /// <summary>A 4-byte immediate sign-extended into the next field (the minimum).</summary>
    Imm32Sx,
    /// <summary>An 8-byte immediate: the value, then the minimum.</summary>
    Imm64,
    /// <summary>A 4-byte load from read-only data (rip-relative displacement).</summary>
    Load4,
    /// <summary>An 8-byte load: the value, then the minimum.</summary>
    Load8,
}

/// <summary>
/// Where a PS4 value is set (<see cref="Field"/> 0x38 the value, 0x40 the maximum): <see cref="Va"/> is the address of
/// the immediate, or of a load's displacement, whose instruction ends at <see cref="End"/>; <see cref="Const"/> is
/// what a load reads.
/// </summary>
public sealed record ValueSource(int Field, ValueKind Kind, long Va, long End, long Const);

public sealed record Tunable(string Name, TunableType Type, double? Default, double? Min, double? Max, string Group, string Label, long Site)
{
    /// <summary>NHL 24 only: the instructions that set the value and maximum (for patching them).</summary>
    public IReadOnlyList<ValueSource> Sources { get; init; }

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
    internal sealed class Segment { public long Va, Size, Offset; public uint Flags; public bool Writable => (Flags & 2) != 0; }

    public static List<Tunable> Read(string eboot) => Read(File.ReadAllBytes(eboot));

    /// <summary>Reads the values from an eboot.bin's bytes.</summary>
    public static List<Tunable> Read(byte[] b)
    {
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

        // every "\0gName\0" string in read-only data (on PS4 that is part of the code segment)
        var names = new Dictionary<long, string>();
        bool ps4 = BinaryPrimitives.ReadUInt32LittleEndian(b) == 0x1D3D154F;
        foreach (var s in segs.Where(s => s.Va != 0 || ps4))
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

        var rows = ps4 ? ReadPs4(b, c0, code.Size, sites, CStr, F32) : new List<Tunable>();
        var seen = new HashSet<(string, long)>();
        foreach (var (site, name) in ps4 ? Enumerable.Empty<(long, string)>() : sites)
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

    /// <summary>
    /// NHL 24 (PS4) builds each value inline: the object is static data at a fixed address A, filled in place and linked
    /// into the list: [A+8] group, [A+0x10] name (the lea rcx this starts from), [A+0x18] label, [A+0x20] type code.
    /// After the link come the values: a float's default and minimum as the low 8 bytes of an xmm loaded from read-only
    /// data (vmovlps [A+0x38]) and its maximum as an immediate at [A+0x40]; an int's default and minimum as a qword
    /// immediate at [A+0x38] and its maximum at [A+0x40]; a bool's default as a byte at [A+0x38].
    /// </summary>
    private static List<Tunable> ReadPs4(byte[] b, int c0, long codeSize, List<(long Site, string Name)> sites, Func<long, string> cstr, Func<long, float?> f32)
    {
        var rows = new List<Tunable>();
        foreach (var (site, name) in sites.OrderBy(x => x.Site))
        {
            // registers holding a string pointer before the site (the group and label are often loaded earlier)
            var ptr = new Dictionary<Register, long>();
            var before = Window(b, c0, codeSize, site);
            if (before != null)
                foreach (var x in before.TakeWhile(x => (long)x.IP < site))
                {
                    if (x.Mnemonic == Mnemonic.Lea && x.Op1Kind == OpKind.Memory && x.IsIPRelativeMemoryOperand) ptr[x.Op0Register.GetFullRegister()] = (long)x.IPRelativeMemoryAddress;
                    else if (x.Op0Kind == OpKind.Register) ptr.Remove(x.Op0Register.GetFullRegister());
                }
            int len = (int)Math.Min(0x300, codeSize - site);
            var d = Decoder.Create(64, new ByteArrayCodeReader(b, c0 + (int)site, len), (ulong)site);
            // the object: where the name is stored (from the register the site's lea loads, usually rcx), minus 0x10
            ulong a = 0;
            Register nameReg = Register.None;
            {
                var pre = Decoder.Create(64, new ByteArrayCodeReader(b, c0 + (int)site, Math.Min(0x60, len)), (ulong)site);
                while (pre.IP < (ulong)site + 0x60 && a == 0)
                {
                    var x = pre.Decode();
                    if (x.IsInvalid) break;
                    if (nameReg == Register.None) { nameReg = x.Op0Register.GetFullRegister(); continue; }
                    if (x.Mnemonic == Mnemonic.Mov && x.Op0Kind == OpKind.Memory && x.IsIPRelativeMemoryOperand && x.Op1Kind == OpKind.Register && x.Op1Register == nameReg)
                        a = x.IPRelativeMemoryAddress - 0x10;
                }
            }
            if (a == 0) continue;
            int heads = 0;
            TunableType? kind = null;
            long? group = null, label = null;
            bool sameLabel = false;
            var xmm = new Dictionary<Register, (long Addr, int Size, long Va, long End)>();
            var imm = new Dictionary<Register, (long Value, long Va, int Size, bool Sx)>();
            var sources = new List<ValueSource>();
            double? value = null, min = null, max = null;
            while (d.IP < (ulong)(site + len))
            {
                var x = d.Decode();
                if (x.IsInvalid) break;
                var co = d.GetConstantOffsets(x);
                bool rip = x.Op0Kind == OpKind.Memory && x.IsIPRelativeMemoryOperand;
                ulong m = rip ? x.IPRelativeMemoryAddress : 0;
                Register r1 = x.OpCount > 1 && x.Op1Kind == OpKind.Register ? x.Op1Register : Register.None;
                if (x.Mnemonic == Mnemonic.Lea && x.Op1Kind == OpKind.Memory && x.IsIPRelativeMemoryOperand)
                {
                    ptr[x.Op0Register.GetFullRegister()] = (long)x.IPRelativeMemoryAddress;
                    continue;
                }
                if (x.Mnemonic == Mnemonic.Mov && x.Op0Kind == OpKind.Register && x.Op1Kind is OpKind.Immediate64 or OpKind.Immediate32 or OpKind.Immediate32to64)
                {
                    imm[x.Op0Register.GetFullRegister()] = ((long)x.GetImmediate(1), (long)x.IP + co.ImmediateOffset, co.ImmediateSize,
                        x.Op1Kind == OpKind.Immediate32to64);
                    continue;
                }
                // an xmm loaded from read-only data, or zeroed
                if (x.Mnemonic is Mnemonic.Vmovss or Mnemonic.Vmovsd or Mnemonic.Vmovq && x.Op0Kind == OpKind.Register && x.OpCount == 2 && x.Op1Kind == OpKind.Memory && x.IsIPRelativeMemoryOperand)
                {
                    xmm[x.Op0Register] = ((long)x.IPRelativeMemoryAddress, x.Mnemonic == Mnemonic.Vmovss ? 4 : 8, (long)x.IP + co.DisplacementOffset, (long)x.NextIP);
                    continue;
                }
                if (x.Mnemonic is Mnemonic.Vxorps or Mnemonic.Vxorpd && x.OpCount == 3 && x.Op1Kind == OpKind.Register && x.Op2Kind == OpKind.Register && x.Op1Register == x.Op2Register)
                {
                    xmm[x.Op0Register] = (0, 16, 0, 0);
                    continue;
                }
                // the head of the list (a store of rax outside the object): the first links this object, the second is the next one's
                if (rip && x.Mnemonic == Mnemonic.Mov && r1 == Register.RAX && (m < a || m >= a + 0x48) && heads++ == 1) break;
                if (!rip || m < a || m >= a + 0x48) continue;
                long off = (long)(m - a);
                if (x.Mnemonic == Mnemonic.Mov && r1 != Register.None)
                {
                    var full = r1.GetFullRegister();
                    if (off == 8) group = ptr.TryGetValue(full, out var g) ? g : null;
                    else if (off == 0x18) { sameLabel = full == nameReg; label = ptr.TryGetValue(full, out var l) ? l : null; }
                    else if (off is 0x38 or 0x40 && imm.TryGetValue(full, out var q))
                    {
                        if (off == 0x38) { value = (int)q.Value; min = (int)(q.Value >> 32); }
                        bool qw = x.MemorySize is MemorySize.UInt64 or MemorySize.Int64;
                        ValueKind? k = q.Size == 8 ? (qw ? ValueKind.Imm64 : ValueKind.Imm32) : q.Size == 4 ? (qw && q.Sx ? ValueKind.Imm32Sx : ValueKind.Imm32) : null;
                        if (k is ValueKind vk) sources.Add(new ValueSource((int)off, vk, q.Va, 0, 0));
                    }
                    continue;
                }
                if (x.Mnemonic == Mnemonic.Mov && x.Op1Kind is OpKind.Immediate8 or OpKind.Immediate32 or OpKind.Immediate32to64)
                {
                    long v = x.Op1Kind == OpKind.Immediate8 ? x.Immediate8 : (int)x.Immediate32;
                    bool qword = x.MemorySize == MemorySize.UInt64 || x.MemorySize == MemorySize.Int64;
                    if (off is 0x38 or 0x40 && co.ImmediateSize is 1 or 4)
                    {
                        var k = co.ImmediateSize == 1 ? ValueKind.Imm8 : qword ? ValueKind.Imm32Sx : ValueKind.Imm32;
                        // a byte immediate sign-extended into a wider store isn't a bool's byte
                        if (k != ValueKind.Imm8 || x.MemorySize == MemorySize.UInt8)
                            sources.Add(new ValueSource((int)off, k, (long)x.IP + co.ImmediateOffset, 0, 0));
                    }
                    if (off == 0x20)
                    {
                        kind = v switch { 1 => TunableType.Bool, 2 => TunableType.Int, 4 => TunableType.Float, _ => null };
                        if (kind == null) break;
                    }
                    else if (kind == TunableType.Bool && off == 0x38) value = v != 0 ? 1 : 0;
                    else if (kind == TunableType.Int && off == 0x38) { value = v; if (qword) min = v < 0 ? -1 : 0; }
                    else if (kind == TunableType.Int && off == 0x3C) min = v;
                    else if (kind == TunableType.Int && off == 0x40) max = v;
                    else if (kind == TunableType.Float && off is 0x38 or 0x3C or 0x40)
                    {
                        double f = Round(BitConverter.Int32BitsToSingle((int)v));
                        if (off == 0x38) { value = f; if (qword) min = 0; }
                        else if (off == 0x3C) min = f;
                        else max = f;
                    }
                    continue;
                }
                // float values stored from an xmm register
                if (x.Mnemonic is Mnemonic.Vmovlps or Mnemonic.Vmovss or Mnemonic.Vmovq or Mnemonic.Vmovsd && x.OpCount == 2 && r1 != Register.None)
                {
                    bool known = xmm.TryGetValue(r1, out var src);
                    // the value is the first 4 bytes the load reads; an 8-byte load stored whole also carries the minimum
                    // (a 4-byte load stored as 8 bytes leaves the minimum 0)
                    if (known && src.Va != 0 && off is 0x38 or 0x40)
                        sources.Add(new ValueSource((int)off, src.Size == 8 && x.MemorySize != MemorySize.Float32 ? ValueKind.Load8 : ValueKind.Load4, src.Va, src.End, src.Addr));
                    float? F(int at) => !known ? null : src.Addr == 0 || at >= src.Size ? 0f : f32(src.Addr + at);
                    if (off == 0x38)
                    {
                        value = F(0) is float v0 ? Round(v0) : null;
                        if (x.MemorySize != MemorySize.Float32) min = F(4) is float v1 ? Round(v1) : null;
                    }
                    else if (off == 0x3C) min = F(0) is float v1 ? Round(v1) : null;
                    else if (off == 0x40) max = F(0) is float v2 ? Round(v2) : null;
                }
            }
            if (kind == null) continue;
            string groupText = group is long gp ? cstr(gp) : null;
            string labelText = sameLabel || label == null ? null : cstr(label.Value);
            if (labelText == name) labelText = null;
            rows.Add(new Tunable(name, kind.Value, value, kind == TunableType.Bool ? null : min, kind == TunableType.Bool ? null : max, groupText, labelText, site) { Sources = sources });
        }
        return rows;

        static double Round(float f) => double.Parse(f.ToString("G6", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
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
    internal static List<Segment> Segments(byte[] b)
    {
        // PS5 and PS4 (NHL 24) SELFs share the layout: a 0x20 header, 0x20-byte segment entries, then the ELF
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(b);
        if (magic != 0xEEF51454 && magic != 0x1D3D154F) throw new InvalidDataException("eboot.bin isn't a decrypted PS5 or PS4 SELF");
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
            list.Add(new Segment { Va = va, Size = Math.Min(size, fileSize), Offset = off, Flags = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p + 4)) });
        }
        return list;
    }
}
