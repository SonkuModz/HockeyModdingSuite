using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// Changes NHL 24 (PS4) tuning defaults in eboot.bin. The game sets each value in its start-up code, so the
/// instruction that sets it is changed (NHL24Kit's notes, docs/GAMEPLAY_TWEAKS.md, and <see cref="Tunables"/>):
/// - bools and ints are immediates in the instruction: the immediate is rewritten;
/// - floats are loaded from read-only constants that other code shares, so the constant is left alone and the load's
///   displacement is pointed at read-only bytes that already hold the wanted value (an 8-byte load reads the value
///   and the minimum together). When no such bytes exist, they are put in int3 padding between functions, which is
///   never run.
/// An instruction that also sets a value not being changed (the compiler shares loads and immediates) is left alone.
/// </summary>
public static class TunablePatcher
{
    /// <summary>A value to set (and the minimum and maximum, used where they are set with it).</summary>
    public sealed record Change(string Name, double Value, double? Min = null, double? Max = null);

    public sealed record Plan(List<PatchData> Patches, List<string> Done, List<string> Skipped, byte[] Patched, HashSet<string> Changed);

    public static Plan Make(byte[] eboot, IEnumerable<Change> changes, string file = "eboot.bin")
    {
        byte[] b = eboot;
        var segs = Tunables.Segments(b);
        long Off(long va)
        {
            foreach (var s in segs)
                if (va >= s.Va && va < s.Va + s.Size) return s.Offset + va - s.Va;
            return -1;
        }
        var rows = Tunables.Read(b);
        var byName = rows.GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.ToList());
        var users = new Dictionary<long, List<(Tunable T, ValueSource S)>>();
        foreach (var r in rows)
            foreach (var s in r.Sources ?? Array.Empty<ValueSource>())
            {
                if (!users.TryGetValue(s.Va, out var l)) users[s.Va] = l = new();
                l.Add((r, s));
            }

        var skipped = new List<string>();
        // what each changed field wants its source's bytes to be (for a load: the constant it reads)
        var wants = new List<(Tunable T, ValueSource S, byte[] Bytes, string Text)>();
        foreach (var c in changes)
        {
            if (!byName.TryGetValue(c.Name, out var list)) { skipped.Add($"{c.Name}: not in this game"); continue; }
            if (list.Count > 1) { skipped.Add($"{c.Name}: set in {list.Count} places"); continue; }
            var t = list[0];
            var fields = new List<(int Field, double V)>();
            if (t.Default != c.Value) fields.Add((0x38, c.Value));
            if (c.Max is double mx && t.Max != null && t.Max != mx && t.Type != TunableType.Bool) fields.Add((0x40, mx));
            foreach (var (field, v) in fields)
            {
                string what = field == 0x38 ? "value" : "max";
                var src = (t.Sources ?? Array.Empty<ValueSource>()).Where(s => s.Field == field).ToList();
                if (src.Count != 1) { skipped.Add($"{c.Name}: its {what} isn't set by one instruction that can be changed"); continue; }
                var s = src[0];
                long o = Off(s.Va);
                byte[] bytes = Bytes(t.Type, s, v, field == 0x38 ? c.Min : null, b, o, Off(s.Const));
                if (bytes == null) { skipped.Add($"{c.Name}: its {what} {v} can't be set the way the game sets it ({s.Kind})"); continue; }
                wants.Add((t, s, bytes, $"{what} {Show(field == 0x38 ? t.Default : t.Max)} -> {Show(v)}"));
            }
        }

        // a source shared with a field that isn't changing, or wanted two ways, stays as it is
        var ok = new List<(Tunable T, ValueSource S, byte[] Bytes, string Text)>();
        foreach (var g in wants.GroupBy(w => w.S.Va))
        {
            var others = users[g.Key].Where(u => !g.Any(w => w.T == u.T && w.S.Field == u.S.Field)).Select(u => u.T.Name).Distinct().ToList();
            if (others.Count > 0)
            {
                foreach (var w in g) skipped.Add($"{w.T.Name}: the instruction also sets {string.Join(", ", others.Take(3))}{(others.Count > 3 ? $" (+{others.Count - 3})" : "")}");
                continue;
            }
            if (g.Select(w => Convert.ToHexString(w.Bytes)).Distinct().Count() > 1)
            {
                foreach (var w in g) skipped.Add($"{w.T.Name}: shares its instruction with {string.Join(", ", g.Where(x => x.T != w.T).Select(x => x.T.Name))}, which want another value");
                continue;
            }
            ok.AddRange(g);
        }

        // the bytes to write, by address
        var edits = new SortedDictionary<long, byte[]>();
        var done = new List<string>();
        var changed = new HashSet<string>();
        foreach (var w in ok.Where(w => w.S.Kind is not (ValueKind.Load4 or ValueKind.Load8)))
        {
            edits[w.S.Va] = w.Bytes;
            done.Add($"{w.T.Name}: {w.Text}");
            changed.Add(w.T.Name);
        }
        var loads = ok.Where(w => w.S.Kind is ValueKind.Load4 or ValueKind.Load8).ToList();
        // a constant found in the code must not sit on bytes that are about to change
        var reserved = new SortedDictionary<long, byte[]>(edits);
        foreach (var w in loads) reserved[w.S.Va] = new byte[4];
        var found = Find(b, segs, loads.Select(w => w.Bytes), reserved);
        var caves = new Caves(b, segs, edits);
        foreach (var w in loads)
        {
            string key = Convert.ToHexString(w.Bytes);
            string how = "existing constant";
            if (!found.TryGetValue(key, out long at))
            {
                at = caves.Put(w.Bytes);
                if (at < 0) { skipped.Add($"{w.T.Name}: no room left for its constant"); continue; }
                found[key] = at;
                edits[at] = w.Bytes;
                how = "new constant";
            }
            long disp = at - w.S.End;
            if (disp is < int.MinValue or > int.MaxValue) { skipped.Add($"{w.T.Name}: its constant is out of reach"); continue; }
            var d = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(d, (int)disp);
            edits[w.S.Va] = d;
            done.Add($"{w.T.Name}: {w.Text} ({how} at 0x{at:X})");
            changed.Add(w.T.Name);
        }

        byte[] patched = (byte[])b.Clone();
        foreach (var (va, bytes) in edits) bytes.CopyTo(patched, Off(va));

        // ranges for the mod list: edits close together are one range
        var patches = new List<PatchData>();
        long start = -1, end = -1;
        void Flush()
        {
            if (start < 0) return;
            patches.Add(new PatchData(file, start, b[(int)start..(int)end], patched[(int)start..(int)end], "tuning"));
        }
        foreach (var (va, bytes) in edits)
        {
            long o = Off(va);
            if (start >= 0 && o <= end + 16) { end = Math.Max(end, o + bytes.Length); continue; }
            Flush();
            start = o; end = o + bytes.Length;
        }
        Flush();
        return new Plan(patches, done, skipped, patched, changed);
    }

    /// <summary>What a source's bytes become for value <paramref name="v"/>; null when it can't hold it.</summary>
    private static byte[] Bytes(TunableType type, ValueSource s, double v, double? min, byte[] b, long at, long constAt)
    {
        int bits = type == TunableType.Float ? BitConverter.SingleToInt32Bits((float)v) : checked((int)v);
        if (type == TunableType.Int && v != Math.Round(v)) return null;
        switch (s.Kind)
        {
            case ValueKind.Imm8:
                return type == TunableType.Bool ? new[] { (byte)(v != 0 ? 1 : 0) } : null;
            case ValueKind.Imm32:
                return type == TunableType.Bool ? null : Le32(bits);
            case ValueKind.Imm32Sx:
                // the upper half (the minimum) follows the sign: keep it
                int old = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan((int)at));
                return type == TunableType.Bool || (old < 0) != (bits < 0) ? null : Le32(bits);
            case ValueKind.Imm64:
            {
                if (type == TunableType.Bool) return null;
                var r = new byte[8];
                BinaryPrimitives.WriteInt32LittleEndian(r, bits);
                if (min is double m) BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(4), type == TunableType.Float ? BitConverter.SingleToInt32Bits((float)m) : checked((int)m));
                else b.AsSpan((int)at + 4, 4).CopyTo(r.AsSpan(4));
                return r;
            }
            case ValueKind.Load4:
                return type == TunableType.Float ? Le32(bits) : null;
            case ValueKind.Load8:
            {
                if (type != TunableType.Float || constAt < 0) return null;
                var r = new byte[8];
                BinaryPrimitives.WriteInt32LittleEndian(r, bits);
                if (min is double m) BinaryPrimitives.WriteSingleLittleEndian(r.AsSpan(4), (float)m);
                else b.AsSpan((int)constAt + 4, 4).CopyTo(r.AsSpan(4));
                return r;
            }
        }
        return null;
    }

    private static byte[] Le32(int v)
    {
        var r = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(r, v);
        return r;
    }

    private static string Show(double? v) => v?.ToString("G6", System.Globalization.CultureInfo.InvariantCulture) ?? "?";

    /// <summary>Read-only bytes (4-byte aligned) that already hold each wanted constant, away from any edit.</summary>
    private static Dictionary<string, long> Find(byte[] b, List<Tunables.Segment> segs, IEnumerable<byte[]> wanted, SortedDictionary<long, byte[]> edits)
    {
        var w4 = new HashSet<uint>();
        var w8 = new HashSet<ulong>();
        foreach (var x in wanted)
            if (x.Length == 4) w4.Add(BinaryPrimitives.ReadUInt32LittleEndian(x));
            else w8.Add(BinaryPrimitives.ReadUInt64LittleEndian(x));
        var found = new Dictionary<string, long>();
        bool Clear(long va, int n) => !edits.Any(e => va < e.Key + e.Value.Length && e.Key < va + n);
        foreach (var s in segs.Where(s => !s.Writable))
        {
            long first = (s.Va + 3) & ~3L;
            for (long va = first; va + 8 <= s.Va + s.Size; va += 4)
            {
                int o = (int)(s.Offset + va - s.Va);
                uint v4 = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
                if (w4.Contains(v4) && Clear(va, 4)) { found.TryAdd(Convert.ToHexString(b, o, 4), va); w4.Remove(v4); }
                if (w8.Count > 0)
                {
                    ulong v8 = BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o));
                    if (w8.Contains(v8) && Clear(va, 8)) { found.TryAdd(Convert.ToHexString(b, o, 8), va); w8.Remove(v8); }
                }
                if (w4.Count == 0 && w8.Count == 0) return found;
            }
        }
        return found;
    }

    /// <summary>
    /// Room for new constants: nop padding (0x90) right after a ret that runs to a 16-byte boundary (the next function's
    /// start), so it is never run.
    /// </summary>
    private sealed class Caves
    {
        private readonly List<(long Start, long End)> _runs = new();
        private int _run;
        private long _at = -1;

        public Caves(byte[] b, List<Tunables.Segment> segs, SortedDictionary<long, byte[]> edits)
        {
            var code = segs.First(s => s.Va == 0);
            int o0 = (int)code.Offset, o1 = (int)(code.Offset + code.Size);
            for (int i = o0 + 1; i < o1; i++)
            {
                if (b[i] != 0x90 || b[i - 1] != 0xC3) continue;
                int j = i;
                while (j < o1 && b[j] == 0x90) j++;
                long start = i - o0, end = j - o0;
                if ((end & 15) == 0 && end - ((start + 3) & ~3L) >= 8) _runs.Add(((start + 3) & ~3L, end));
                i = j;
            }
        }

        public long Put(byte[] bytes)
        {
            while (_run < _runs.Count)
            {
                if (_at < 0) _at = _runs[_run].Start;
                if (_at + bytes.Length <= _runs[_run].End)
                {
                    long at = _at;
                    _at += (bytes.Length + 3) & ~3;
                    return at;
                }
                _run++;
                _at = -1;
            }
            return -1;
        }
    }
}
