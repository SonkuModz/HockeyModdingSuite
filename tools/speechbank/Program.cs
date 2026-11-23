using System.Buffers.Binary;
using System.Security.Cryptography;
using NHL27Kit.Core;
// read-only: decodes a wave's SBle bank DSET columns in both games and tests them against identical takes and metadata ids
AssetIndex Open(string g) => AssetIndex.Open(g, File.ReadAllBytes, KitPaths.IndexFileFor(GameProfile.Detect(g)), _ => { });
var ix = new[] { Open(args[0]), Open(args[1]) }; string[] tag = { "27", "24" };
var waves = args.Skip(2).ToList();
if (waves.Count > 2 && waves[0] == "--exportwave") {
    // read-only: every take of a wave as 16 kHz mono WAV plus a JSON list (args: 27 24 --exportwave OUTDIR G|WAVE ...)
    string outDir = waves[1]; Directory.CreateDirectory(outDir);
    var list = new List<string>();
    foreach (var spec in waves.Skip(2))
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
        var kw = Keywords(ix[g]);
        long[] vid = Array.Empty<long>();
        try { vid = Bank(ix[g].Read(ix[g].GetRes(w)))[0xa29af127].F.First(f => f.Hash == 0xf5f914d9).V; } catch { }
        var vs = Audio.Variations(ix[g], ix[g].GetEbx(w));
        string leaf = parts[1].Replace('/', '_');
        for (int i = 0; i < vs.Count; i++)
        {
            if (vs[i].Header == null) continue;
            var snd = Audio.Decode(ix[g], vs[i]);
            int c = snd.Channels; var mono = new List<short>();
            for (int f = 0; f + 2 < snd.Frames; f += 3) { int sum = 0; for (int k = 0; k < c; k++) sum += snd.Pcm[f * c + k]; mono.Add((short)Math.Clamp(sum, -32768, 32767)); }
            string file = $"{tag[g]}_{leaf}_{i + 1}.wav";
            using (var ms = new MemoryStream()) using (var bw = new BinaryWriter(ms))
            {
                bw.Write("RIFF"u8); bw.Write(36 + mono.Count * 2); bw.Write("WAVEfmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(16000); bw.Write(32000); bw.Write((short)2); bw.Write((short)16);
                bw.Write("data"u8); bw.Write(mono.Count * 2); foreach (var x in mono) bw.Write(x); bw.Flush(); File.WriteAllBytes(Path.Combine(outDir, file), ms.ToArray());
            }
            string kt = i < vid.Length && kw.TryGetValue((uint)vid[i], out var ks) ? string.Join("+", ks) : "";
            list.Add($"{{\"game\":\"{tag[g]}\",\"wave\":\"{parts[1]}\",\"take\":{i + 1},\"tag\":\"{kt}\",\"sec\":{snd.Seconds:0.00},\"file\":\"{file}\"}}");
        }
        Console.WriteLine($"NHL {tag[g]} {parts[1]}: {vs.Count} takes, bank rows {vid.Length}");
    }
    File.WriteAllText(Path.Combine(outDir, "takes.json"), "[\n" + string.Join(",\n", list) + "\n]");
    return;
}
if (waves.Count > 1 && waves[0] == "--find") {
    // read-only: every soundwave whose name contains the text, per game, with its take count and distinct keywords
    for (int g = 0; g < 2; g++)
    {
        var kw = Keywords(ix[g]);
        foreach (var e in ix[g].Ebx.Values.Where(x => x.Name.StartsWith("sound/speech/") && x.Name.Contains(waves[1])).OrderBy(x => x.Name))
        {
            int n = -1; string kws = "";
            try { n = Audio.Variations(ix[g], ix[g].GetEbx(e.Name)).Count; } catch { }
            try { var vid = Bank(ix[g].Read(ix[g].GetRes(e.Name)))[0xa29af127].F.First(f => f.Hash == 0xf5f914d9).V;
                  var ks = vid.Select(v => kw.TryGetValue((uint)v, out var l) ? string.Join("+", l) : "-").GroupBy(x => x).OrderByDescending(x => x.Count()).Take(12);
                  kws = string.Join(" ", ks.Select(x => $"{x.Key}x{x.Count()}")); } catch { }
            Console.WriteLine($"NHL {tag[g]} {e.Name}: {n} takes  {kws}");
        }
    }
    return;
}
if (waves.Count > 2 && waves[0] == "--align") {
    // read-only: slides keyword-ordered take lengths of wave A (game GA) against wave B (game GB) and prints the best offsets
    // (args: 27 24 --align GA|WAVEA GB|WAVEB)
    Dictionary<long, double> Durs(string spec)
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
        var kw = Keywords(ix[g]); var a = Bank(ix[g].Read(ix[g].GetRes(w)))[0xa29af127]; var vid = a.F.First(f => f.Hash == 0xf5f914d9).V;
        var vs = Audio.Variations(ix[g], ix[g].GetEbx(w)); var d = new Dictionary<long, List<double>>();
        for (int r = 0; r < Math.Min(a.N, vs.Count); r++) if (kw.TryGetValue((uint)vid[r], out var ks) && ks.Count == 1) { if (!d.TryGetValue(ks[0], out var l)) d[ks[0]] = l = new(); l.Add(vs[r].Header.Seconds); }
        return d.ToDictionary(x => x.Key, x => x.Value.Average());
    }
    var A = Durs(waves[1]); var B = Durs(waves[2]);
    var res = new List<(long Off, int N, double R)>();
    for (long off = -20000; off <= 20000; off++)
    {
        var pts = A.Where(x => B.ContainsKey(x.Key + off)).Select(x => (X: x.Value, Y: B[x.Key + off])).ToList();
        if (pts.Count < 8) continue;
        double mx = pts.Average(q => q.X), my = pts.Average(q => q.Y);
        double den = Math.Sqrt(pts.Sum(q => (q.X - mx) * (q.X - mx)) * pts.Sum(q => (q.Y - my) * (q.Y - my)));
        if (den == 0) continue;
        res.Add((off, pts.Count, pts.Sum(q => (q.X - mx) * (q.Y - my)) / den));
    }
    foreach (var r in res.OrderByDescending(r => r.R).Take(6)) Console.WriteLine($"  offset {r.Off,6}: n {r.N,4}, r = {r.R:0.000}");
    return;
}
if (waves.Count > 0 && waves[0] == "--kwfile") {
    // read-only: every array in the keywords EBX (relative pointer fields from 0x20), count and first bytes
    for (int g = 0; g < 2; g++)
    {
        var b = ix[g].Read(ix[g].GetEbx("sound/speech/maincommentary/database/maincommentarykeywords"));
        Console.WriteLine($"NHL {tag[g]} keywords EBX {b.Length} B; header " + Convert.ToHexString(b.AsSpan(0, Math.Min(0xC0, b.Length))));
        for (int fo = 0x10; fo < 0xC0; fo += 4)
        {
            int rel = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(fo)); int at = fo + rel;
            if (rel <= 0 || at >= b.Length || at < 4) continue; uint n = U(b, at - 4);
            if (n == 0 || n > 1000000 || at + (long)n > b.Length) continue;
            Console.WriteLine($"  field 0x{fo:X}: -> 0x{at:X}, count {n}, first {Convert.ToHexString(b.AsSpan(at, (int)Math.Min(48, b.Length - at)))}");
        }
    }
    return;
}
if (waves.Count > 2 && waves[0] == "--rows") {
    // read-only: keyword names along bank rows A..B, with take length (args: 27 24 --rows PLAYERNAMES G|WAVE|A|B ...)
    var nm = new Dictionary<uint, string>();
    foreach (var line in File.ReadLines(waves[1])) { var m = System.Text.RegularExpressions.Regex.Match(line, @"surnameid=(\d+) \| name=([^|]+?) \|.*isfirstname=0"); if (m.Success) nm.TryAdd(uint.Parse(m.Groups[1].Value), m.Groups[2].Value); }
    foreach (var spec in waves.Skip(2))
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
        var kw = Keywords(ix[g]); var a = Bank(ix[g].Read(ix[g].GetRes(w)))[0xa29af127]; var vid = a.F.First(f => f.Hash == 0xf5f914d9).V;
        var vs = Audio.Variations(ix[g], ix[g].GetEbx(w));
        Console.WriteLine($"NHL {tag[g]} {parts[1]}:");
        Console.WriteLine("  " + string.Join("  ", Enumerable.Range(int.Parse(parts[2]), int.Parse(parts[3]) - int.Parse(parts[2]) + 1).Select(r => $"{r}:{(kw.TryGetValue((uint)vid[r], out var ks) ? string.Join("+", ks.Select(k => nm.GetValueOrDefault(k, k.ToString()))) : "-")}({vs[r].Header?.Seconds:0.00})")));
    }
    return;
}
if (waves.Count > 1 && waves[0] == "--xdur") {
    // read-only: same keyword in both games' wave: are the take lengths related? (args: 27 24 --xdur WAVE)
    string w = "sound/speech/maincommentary/soundwaves/" + waves[1];
    var per = new Dictionary<uint, List<double>>[2];
    for (int g = 0; g < 2; g++)
    {
        var kw = Keywords(ix[g]); var a = Bank(ix[g].Read(ix[g].GetRes(w)))[0xa29af127]; var vid = a.F.First(f => f.Hash == 0xf5f914d9).V;
        var vs = Audio.Variations(ix[g], ix[g].GetEbx(w)); per[g] = new();
        for (int r = 0; r < Math.Min(a.N, vs.Count); r++) if (kw.TryGetValue((uint)vid[r], out var ks) && ks.Count == 1) { if (!per[g].TryGetValue(ks[0], out var l)) per[g][ks[0]] = l = new(); l.Add(vs[r].Header.Seconds); }
    }
    var pts = per[0].Where(x => per[1].ContainsKey(x.Key)).Select(x => (X: x.Value.Average(), Y: per[1][x.Key].Average())).ToList();
    double mx = pts.Average(q => q.X), my = pts.Average(q => q.Y);
    double rr = pts.Sum(q => (q.X - mx) * (q.Y - my)) / Math.Sqrt(pts.Sum(q => (q.X - mx) * (q.X - mx)) * pts.Sum(q => (q.Y - my) * (q.Y - my)));
    // within one game: keywords with 2+ takes, first vs second take
    for (int g = 0; g < 2; g++)
    {
        var pp = per[g].Values.Where(l => l.Count >= 2).Select(l => (X: l[0], Y: l[1])).ToList();
        if (pp.Count < 3) { Console.WriteLine($"NHL {tag[g]}: keywords {per[g].Count}, none with 2+ takes"); continue; }
        double ax = pp.Average(q => q.X), ay = pp.Average(q => q.Y);
        double r2 = pp.Sum(q => (q.X - ax) * (q.Y - ay)) / Math.Sqrt(pp.Sum(q => (q.X - ax) * (q.X - ax)) * pp.Sum(q => (q.Y - ay) * (q.Y - ay)));
        Console.WriteLine($"NHL {tag[g]}: keywords with 2+ takes {pp.Count}, take 1 vs take 2 length r = {r2:0.000}");
    }
    Console.WriteLine($"keywords 27 {per[0].Count}, 24 {per[1].Count}; same keyword across games: n {pts.Count}, r = {rr:0.000}");
    Console.WriteLine("  keywords: 27 " + string.Join(",", per[0].Keys.OrderBy(x => x).Take(60)) + " | 24 " + string.Join(",", per[1].Keys.OrderBy(x => x).Take(60)));
    return;
}
if (waves.Count > 2 && waves[0] == "--lencheck") {
    // read-only: correlation between surname length and take length under row / perm mappings (args: 27 24 --lencheck PLAYERNAMES G|WAVE ...)
    var nm = new Dictionary<uint, string>();
    string col = Environment.GetEnvironmentVariable("KWCOL") ?? "surnameid";
    foreach (var line in File.ReadLines(waves[1])) { var m = System.Text.RegularExpressions.Regex.Match(line, @"(?<![a-z])" + col + @"=(\d+)(?: |$)"); var mn = System.Text.RegularExpressions.Regex.Match(line, @"name=([^|]+?) \|"); if (m.Success && mn.Success && (Environment.GetEnvironmentVariable("KWALL") != null || line.Contains("isfirstname=0"))) nm.TryAdd(uint.Parse(m.Groups[1].Value), mn.Groups[1].Value); }
    Console.WriteLine($"key column {col}: {nm.Count} names");
    double R(List<(double X, double Y)> p) { if (p.Count < 3) return double.NaN; double mx = p.Average(q => q.X), my = p.Average(q => q.Y); double sxy = p.Sum(q => (q.X - mx) * (q.Y - my)), sx = Math.Sqrt(p.Sum(q => (q.X - mx) * (q.X - mx))), sy = Math.Sqrt(p.Sum(q => (q.Y - my) * (q.Y - my))); return sxy / (sx * sy); }
    foreach (var spec in waves.Skip(2))
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
        var kw = Keywords(ix[g]); var a = Bank(ix[g].Read(ix[g].GetRes(w)))[0xa29af127];
        var vid = a.F.First(f => f.Hash == 0xf5f914d9).V; var perm = a.F.FirstOrDefault(f => f.Hash == 0x65610234).V;
        var vs = Audio.Variations(ix[g], ix[g].GetEbx(w));
        var gaps = perm == null ? null : Enumerable.Range(0, (int)perm.Max() + 1).Where(x => !perm.Contains(x)).ToList();
        var maps = new Dictionary<string, Func<int, long>> { ["row"] = r => r };
        var b11 = Bank(ix[g].Read(ix[g].GetRes(w)))[0x11d5b185]; var bv = b11.F.First(f => f.Hash == 0xf5f914d9).V;
        var pos11 = new Dictionary<long, int>(); for (int i = 0; i < b11.N; i++) pos11.TryAdd(bv[i], i);
        maps["pos-in-11d5b185"] = r => pos11.TryGetValue(vid[r], out var q) ? q : -1;
        var rank = vid.Select((v, i) => (v, i)).OrderBy(x => x.v).Select((x, k) => (x.i, k)).ToDictionary(x => x.i, x => x.k);
        maps["vid-rank"] = r => rank[r];
        if (perm != null) { maps["perm"] = r => perm[r]; maps["perm-compact"] = r => perm[r] - gaps.Count(x => x < perm[r]); }
        foreach (var (mn, f) in maps)
        {
            var pts = new List<(double, double)>();
            for (int r = 0; r < a.N; r++) { long s = f(r); if (s < 0 || s >= vs.Count || vs[(int)s].Header == null) continue; if (!kw.TryGetValue((uint)vid[r], out var ks) || ks.Count != 1 || !nm.TryGetValue(ks[0], out var n)) continue; pts.Add((n.Length, vs[(int)s].Header.Seconds)); }
            Console.WriteLine($"NHL {tag[g]} {parts[1]} {mn}: n {pts.Count}, r = {R(pts):0.000}");
            if (mn == "row") { var ex = new List<string>(); for (int r = 0; r < a.N && ex.Count < 400; r++) if (kw.TryGetValue((uint)vid[r], out var ks) && ks.Count == 1 && nm.TryGetValue(ks[0], out var n2) && (n2.Length >= 14 || n2.Length <= 3)) ex.Add($"{n2}={vs[r].Header?.Seconds:0.00}"); Console.WriteLine("  longest/shortest names: " + string.Join(" ", ex.Take(30))); }
        }
        if (gaps != null) Console.WriteLine($"  perm gaps: {string.Join(",", gaps)}");
    }
    return;
}
if (waves.Count > 1 && waves[0] == "--vidscan") {
    // read-only: are VariationIds unique across 27's (or 24's) waves? (args: 27 24 --vidscan G vid...)
    int g = int.Parse(waves[1]); var want = waves.Skip(2).Select(long.Parse).ToHashSet();
    long rows = 0; var seen = new Dictionary<long, int>(); int wv = 0;
    foreach (var e in ix[g].Ebx.Values.Where(x => x.Name.StartsWith("sound/speech/maincommentary/soundwaves/")))
    {
        Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)> b;
        try { var r = ix[g].GetRes(e.Name); if (r == null) continue; b = Bank(ix[g].Read(r)); } catch { continue; }
        if (!b.TryGetValue(0xa29af127, out var a)) continue; wv++;
        var vid = a.F.First(f => f.Hash == 0xf5f914d9).V;
        for (int i = 0; i < a.N; i++) { rows++; seen[vid[i]] = seen.GetValueOrDefault(vid[i]) + 1; if (want.Contains(vid[i])) Console.WriteLine($"  vid {vid[i]} in {e.Name.Replace("sound/speech/maincommentary/soundwaves/", "")} row {i}"); }
    }
    Console.WriteLine($"NHL {tag[g]}: waves {wv}, rows {rows}, distinct vids {seen.Count}, vids used more than once {seen.Count(x => x.Value > 1)}");
    return;
}
if (waves.Count > 1 && waves[0] == "--dump") {
    // read-only: every column of every DSET for rows A..B of a wave (args: 27 24 --dump G|WAVE|A|B)
    var parts = waves[1].Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
    int A = int.Parse(parts[2]), B = int.Parse(parts[3]);
    var bank = Bank(ix[g].Read(ix[g].GetRes(w)));
    foreach (var (id, t) in bank)
    {
        if (t.N < 2) continue;
        Console.WriteLine($"DSET {id:x8} rows {t.N}: " + string.Join(" ", t.F.Select(f => $"{f.Hash:x8}")));
        for (int r = A; r <= Math.Min(B, t.N - 1); r++) Console.WriteLine($"  {r}: " + string.Join(" ", t.F.Select(f => f.V[r].ToString())));
    }
    var a = bank[0xa29af127];
    foreach (var f in a.F) { var hits = Enumerable.Range(0, a.N).Where(r => f.V[r] == 721 || f.V[r] == 8938).Take(5); Console.WriteLine($"  a29af127.{f.Hash:x8} == 721 or 8938 at rows {string.Join(",", hits)}"); }
    var sorted = a.F.First(f => f.Hash == 0xf5f914d9).V.Select((v, i) => (v, i)).OrderBy(x => x.v).ToList();
    Console.WriteLine($"  vid rank of row 721: {sorted.FindIndex(x => x.i == 721)}; row with vid rank 721: {sorted[721].i} (vid {sorted[721].v})");
    var perm = a.F.FirstOrDefault(f => f.Hash == 0x65610234).V;
    if (perm != null) { var ps = perm.Select((v, i) => (v, i)).OrderBy(x => x.v).ToList(); Console.WriteLine($"  perm rank of row 721: {ps.FindIndex(x => x.i == 721)}; row whose perm rank is 721: {ps[721].i}"); }
    return;
}
if (waves.Count > 0 && waves[0] == "--mapall") {
    // read-only: --map over every shared wave whose takes carry keywords; totals how 27 rows relate to streams
    var kw27 = Keywords(ix[0]); var kw24 = Keywords(ix[1]);
    var c = new Dictionary<(int, Guid), byte[]>();
    string H(int g, SoundStream st) { if (!c.TryGetValue((g, st.ChunkId), out var r)) c[(g, st.ChunkId)] = r = ix[g].Read(st.Chunk); return Convert.ToHexString(SHA1.HashData(r.AsSpan(st.Offset, st.Length))); }
    int P = 0, ER = 0, EP = 0, W = 0;
    foreach (var e27 in ix[0].Ebx.Values.Where(x => x.Name.StartsWith("sound/speech/maincommentary/soundwaves/")).OrderBy(x => x.Name))
    {
        var e24 = ix[1].GetEbx(e27.Name); if (e24 == null) continue;
        Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)> b27, b24;
        try { b27 = Bank(ix[0].Read(ix[0].GetRes(e27.Name))); b24 = Bank(ix[1].Read(ix[1].GetRes(e24.Name))); } catch { continue; }
        if (!b27.TryGetValue(0xa29af127, out var a27) || !b24.TryGetValue(0xa29af127, out var a24)) continue;
        var vid27 = a27.F.First(f => f.Hash == 0xf5f914d9).V; var vid24 = a24.F.First(f => f.Hash == 0xf5f914d9).V;
        var perm = a27.F.FirstOrDefault(f => f.Hash == 0x65610234).V;
        if (perm == null || vid24.Length == 0 || !kw24.ContainsKey((uint)vid24[0])) continue;
        c.Clear();
        List<SoundStream> v27, v24; try { v27 = Audio.Variations(ix[0], e27); v24 = Audio.Variations(ix[1], e24); } catch { continue; }
        var h24 = new Dictionary<string, int>(); for (int i = 0; i < v24.Count; i++) if (v24[i].Header != null) h24.TryAdd(H(1, v24[i]), i);
        int p = 0, er = 0, ep = 0;
        for (int st = 0; st < v27.Count; st++)
        {
            if (v27[st].Header == null || !h24.TryGetValue(H(0, v27[st]), out int s24) || s24 >= vid24.Length || !kw24.TryGetValue((uint)vid24[s24], out var k24)) continue;
            var rows = Enumerable.Range(0, a27.N).Where(r => kw27.TryGetValue((uint)vid27[r], out var k) && k.SequenceEqual(k24)).ToList();
            if (rows.Count == 0) continue;
            p++; if (rows.Contains(st)) er++; if (rows.Any(r => perm[r] == st)) ep++;
        }
        if (p == 0) continue;
        W++; P += p; ER += er; EP += ep;
        Console.WriteLine($"{e27.Name.Replace("sound/speech/maincommentary/soundwaves/", "")}: pairs {p}, row==stream {er}, perm==stream {ep}, perm==row {Enumerable.Range(0, a27.N).Count(i => perm[i] == i)}/{a27.N}");
        if (W >= 40) break;
    }
    Console.WriteLine($"TOTAL waves {W} pairs {P}: row==stream {ER}, perm[row]==stream {EP}");
    return;
}
if (waves.Count > 2 && waves[0] == "--map") {
    // read-only: uses byte-identical takes (27 stream s27 == 24 stream s24) to learn how 27's bank rows map to streams.
    // 24's surname per stream = keyword of vid[row = stream]; 27 rows holding that surname are compared with s27 (args: 27 24 --map WAVE27 WAVE24)
    string w27 = "sound/speech/maincommentary/soundwaves/" + waves[1], w24 = "sound/speech/maincommentary/soundwaves/" + waves[2];
    var kw27 = Keywords(ix[0]); var kw24 = Keywords(ix[1]);
    var b27 = Bank(ix[0].Read(ix[0].GetRes(w27))); var b24 = Bank(ix[1].Read(ix[1].GetRes(w24)));
    var a27 = b27[0xa29af127]; var vid27 = a27.F.First(f => f.Hash == 0xf5f914d9).V; var perm = a27.F.FirstOrDefault(f => f.Hash == 0x65610234).V;
    var vid24 = b24[0xa29af127].F.First(f => f.Hash == 0xf5f914d9).V;
    var v27 = Audio.Variations(ix[0], ix[0].GetEbx(w27)); var v24 = Audio.Variations(ix[1], ix[1].GetEbx(w24));
    var c = new Dictionary<(int, Guid), byte[]>();
    string H(int g, SoundStream st) { if (!c.TryGetValue((g, st.ChunkId), out var r)) c[(g, st.ChunkId)] = r = ix[g].Read(st.Chunk); return Convert.ToHexString(SHA1.HashData(r.AsSpan(st.Offset, st.Length))); }
    var h24 = new Dictionary<string, int>(); for (int i = 0; i < v24.Count; i++) h24.TryAdd(H(1, v24[i]), i);
    int pairs = 0, eqRow = 0, eqPerm = 0, shown = 0;
    for (int s = 0; s < v27.Count; s++)
    {
        if (!h24.TryGetValue(H(0, v27[s]), out int s24) || s24 >= vid24.Length || !kw24.TryGetValue((uint)vid24[s24], out var k24)) continue;
        pairs++;
        var rows = Enumerable.Range(0, a27.N).Where(r => kw27.TryGetValue((uint)vid27[r], out var k) && k.SequenceEqual(k24)).ToList();
        if (rows.Contains(s)) eqRow++;
        if (perm != null && rows.Any(r => perm[r] == s)) eqPerm++;
        if (shown++ < 12) Console.WriteLine($"  27 stream {s} = 24 stream {s24} (kw {string.Join(",", k24)}): 27 rows with that kw {string.Join(",", rows.Take(6))}" + (perm != null ? $" perm {string.Join(",", rows.Take(6).Select(r => perm[r]))}" : ""));
    }
    Console.WriteLine($"identical pairs with keywords {pairs}: 27 row == stream {eqRow}, perm[row] == stream {eqPerm}");
    return;
}
if (waves.Count > 1 && waves[0] == "--perm") {
    // read-only: in 27's banks, a29af127.65610234 per row; rows whose keyword is one of the given surname ids (args: 27 24 --perm WAVE ids...)
    string w = "sound/speech/maincommentary/soundwaves/" + waves[1];
    var bank = Bank(ix[0].Read(ix[0].GetRes(w))); var a = bank[0xa29af127];
    var vid = a.F.First(f => f.Hash == 0xf5f914d9).V; var perm = a.F.First(f => f.Hash == 0x65610234).V;
    var kw = Keywords(ix[0]); var ids = waves.Skip(2).Select(uint.Parse).ToHashSet();
    Console.WriteLine($"rows {a.N}, perm distinct {perm.Distinct().Count()}, perm==row {Enumerable.Range(0, a.N).Count(i => perm[i] == i)}, max {perm.Max()}");
    for (int i = 0; i < a.N; i++) if (kw.TryGetValue((uint)vid[i], out var ks) && ks.Any(ids.Contains)) Console.WriteLine($"  row {i} (take #{i + 1}) perm {perm[i]} (take #{perm[i] + 1}) vid {vid[i]} kw {string.Join(",", ks)}");
    for (int i = 0; i < a.N; i++) if (perm[i] == 721) Console.WriteLine($"  perm 721 is row {i}, kw {(kw.TryGetValue((uint)vid[i], out var k2) ? string.Join(",", k2) : "-")}");
    return;
}
if (waves.Count > 1 && waves[0] == "--kw") {
    // read-only: the keyword entries for takes (args: 27 24 --kw G|WAVE|TAKE ...) plus the raw KeywordData around them
    foreach (var spec in waves.Skip(1))
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
        var bank = Bank(ix[g].Read(ix[g].GetRes(w))); var vid = bank[0xa29af127].F.First(f => f.Hash == 0xf5f914d9).V;
        var b = ix[g].Read(ix[g].GetEbx("sound/speech/maincommentary/database/maincommentarykeywords"));
        foreach (var tk in parts[2].Split(','))
        {
            int t = int.Parse(tk) - 1; uint v = (uint)vid[t];
            for (int fo = 0x40; fo < 0x90; fo += 8) { int rel = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(fo)); int at = fo + rel; if (rel <= 0 || at >= b.Length || at < 4) continue;
                uint n = U(b, at - 4); if (n < 100 || n > 300000 || at + (long)n * 16 > b.Length) continue;
                for (int i = 0; i < n; i++) { int q = at + i * 16; if (U(b, q + 8) != v) continue; int d = q + BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(q)); uint c = U(b, d - 4);
                    Console.WriteLine($"NHL {tag[g]} {parts[1]} #{t + 1} vid {v}: entry {i} raw {Convert.ToHexString(b.AsSpan(q, 16))} count {c} data {Convert.ToHexString(b.AsSpan(d, (int)Math.Min(c * 8, 64)))}"); }
                break; }
        }
    }
    return;
}
if (waves.Count > 1 && waves[0] == "--vcheck") {
    // read-only: is bank row = take? compares a29af127 (row, e4660a62 take, f5f914d9 vid) with 11d5b185 (6ac4e4ea take, f5f914d9 vid)
    foreach (var spec in waves.Skip(1))
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1];
        var bank = Bank(ix[g].Read(ix[g].GetRes(w)));
        foreach (var (id, t) in bank) Console.WriteLine($"  {id:x8} rows {t.N}: " + string.Join(" ", t.F.Select(f => $"{f.Hash:x8}[{f.How}]={string.Join(",", f.V.Take(6))}")));
        var a = bank[0xa29af127]; var take = a.F.First(f => f.Hash == 0xe4660a62).V; var vid = a.F.First(f => f.Hash == 0xf5f914d9).V;
        int off = Enumerable.Range(0, a.N).Count(i => take[i] != i);
        var b = bank[0x11d5b185]; var bt = b.F.First(f => f.Hash == 0x6ac4e4ea).V; var bv = b.F.First(f => f.Hash == 0xf5f914d9).V;
        var byVid = Enumerable.Range(0, a.N).ToDictionary(i => vid[i], i => i);
        int agreeRow = Enumerable.Range(0, b.N).Count(i => byVid.TryGetValue(bv[i], out var r) && r == bt[i]);
        int agreeTake = Enumerable.Range(0, b.N).Count(i => byVid.TryGetValue(bv[i], out var r) && take[r] == bt[i]);
        Console.WriteLine($"NHL {tag[g]} {parts[1]}: rows where e4660a62 != row {off}/{a.N}; 11d5b185 pairs agreeing with row {agreeRow}/{b.N}, with e4660a62 {agreeTake}/{b.N}");
        Console.WriteLine("  first rows (row:take:vid) " + string.Join(" ", Enumerable.Range(0, 12).Select(i => $"{i}:{take[i]}:{vid[i]}")));
    }
    return;
}
if (waves.Count > 1 && waves[0] == "--take") {
    // read-only: checks bank row = stream order for a wave and exports takes (args: 27 24 --take OUTDIR G|WAVE|N|FILE ...; G 0 = 27, 1 = 24)
    string outDir = waves[1]; Directory.CreateDirectory(outDir);
    foreach (var spec in waves.Skip(2))
    {
        var parts = spec.Split('|'); int g = int.Parse(parts[0]); string w = "sound/speech/maincommentary/soundwaves/" + parts[1]; int n = int.Parse(parts[2]);
        var e = ix[g].GetEbx(w); var vs = Audio.Variations(ix[g], e);
        byte[] rb = ix[g].Read(ix[g].GetRes(w)); var bank = Bank(rb);
        int rows = bank[0xa29af127].N;
        // the offset column as u32 per row
        int o = (int)U(rb, (int)U(rb, 0x18)); uint col = 0;
        foreach (var (dp, _) in Enumerable.Range(0, 8).Select(k => ((int)U(rb, (int)U(rb, 0x18) + k * 8), 0)))
        {
            if (dp <= 0 || dp + 0x48 > rb.Length || U(rb, dp) != 0x44534554 || U(rb, dp + 8) != 0x3fe2afd5) continue;
            int nf = (int)(U(rb, dp + 0x3C) & 0xFFFF);
            for (int k = 0; k < nf; k++) if (U(rb, dp + 0x48 + k * 0x18) == 0xe8e591dd) col = U(rb, dp + 0x48 + k * 0x18 + 0x10);
        }
        int ok = 0, chk = 0;
        for (int i = 0; i < Math.Min(rows, vs.Count); i++) { if (col == 0 || col + i * 4 + 4 > rb.Length) break; chk++; if ((U(rb, (int)col + i * 4) & 0xFFFF) == ((vs[i].Offset + 3) & 0xFFFF)) ok++; }
        var s = vs[n - 1];
        Console.WriteLine($"NHL {tag[g]} {parts[1]} #{n}: bank rows {rows}, streams {vs.Count}, chunks {vs.Select(v => v.ChunkId).Distinct().Count()}, no-header {vs.Count(v => v.Header == null)}, offset check {ok}/{chk}, this take {(col > 0 ? (U(rb, (int)col + (n - 1) * 4) & 0xFFFF) : 0)} vs {(s.Offset + 3) & 0xFFFF}, {s.Header?.Seconds:0.00}s {s.Length}B");
        File.WriteAllBytes(Path.Combine(outDir, parts[3]), Audio.Decode(ix[g], s).ToWav());
    }
    return;
}
if (waves.Count > 1 && waves[0] == "--who") {
    // read-only: per surname wave in both games, the takes (and lengths) of the given names (args: 27 24 --who PLAYERNAMES24.txt names...)
    var nm = new Dictionary<uint, string>();
    foreach (var line in File.ReadLines(waves[1]))
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"surnameid=(\d+) \| name=([^|]+?) \|.*isfirstname=0");
        if (m.Success && uint.Parse(m.Groups[1].Value) > 0 && waves.Skip(2).Contains(m.Groups[2].Value, StringComparer.OrdinalIgnoreCase)) nm.TryAdd(uint.Parse(m.Groups[1].Value), m.Groups[2].Value);
    }
    for (int g = 0; g < 2; g++)
    {
        var kw = Keywords(ix[g]);
        foreach (var e in ix[g].Ebx.Values.Where(x => x.Name.StartsWith("sound/speech/maincommentary/soundwaves/") && x.Name.Contains("surname")).OrderBy(x => x.Name))
        {
            Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)> bank;
            try { bank = Bank(ix[g].Read(ix[g].GetRes(e.Name))); } catch { continue; }
            if (!bank.TryGetValue(0xa29af127, out var t)) continue;
            var vids = t.F.First(f => f.Hash == 0xf5f914d9).V; var vs = Audio.Variations(ix[g], e);
            var hits = new List<string>();
            for (int i = 0; i < Math.Min(vs.Count, vids.Length); i++)
                if (kw.TryGetValue((uint)vids[i], out var ks)) foreach (var k in ks) if (nm.TryGetValue(k, out var n)) hits.Add($"{n} #{i + 1} {vs[i].Header?.Seconds:0.00}s");
            Console.WriteLine($"NHL {tag[g]} {e.Name.Replace("sound/speech/maincommentary/soundwaves/", "")} ({vs.Count}): {string.Join(", ", hits)}");
        }
    }
    return;
}
if (waves.Count > 1 && waves[0] == "--pilot") {
    // read-only: picks surnames both games have in pa_player_surname_pxspos, where 24 has one take and 27 a different,
    // not longer recording; exports both as WAV (args: 27 24 --pilot OUTDIR PLAYERNAMES24.txt [names...])
    string outDir = waves[1]; Directory.CreateDirectory(outDir);
    var names = new Dictionary<uint, string>();
    foreach (var line in File.ReadLines(waves[2]))
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"surnameid=(\d+) \| name=([^|]+?) \|.*isfirstname=0");
        if (m.Success && uint.Parse(m.Groups[1].Value) > 0) names.TryAdd(uint.Parse(m.Groups[1].Value), m.Groups[2].Value);
    }
    var wanted = waves.Skip(3).ToList();
    var takesBy = new Dictionary<uint, List<(string Wave, int Take, SoundStream S)>>[2];
    for (int g = 0; g < 2; g++)
    {
        var kw = Keywords(ix[g]); takesBy[g] = new();
        foreach (var e in ix[g].Ebx.Values.Where(x => x.Name.StartsWith("sound/speech/maincommentary/soundwaves/") && x.Name.EndsWith("/pa_player_surname_pxspos")))
        {
            Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)> bank;
            try { bank = Bank(ix[g].Read(ix[g].GetRes(e.Name))); } catch (Exception ex) { Console.WriteLine($"NHL {tag[g]} {e.Name}: bank not readable ({ex.Message})"); continue; }
            if (!bank.ContainsKey(0xa29af127)) { Console.WriteLine($"NHL {tag[g]} {e.Name}: no a29af127 table; tables " + string.Join(" ", bank.Select(kv => $"{kv.Key:x8}[{kv.Value.N}]:" + string.Join("/", kv.Value.F.Select(f => $"{f.Hash:x8}={string.Join(",", f.V.Take(4))}"))))); continue; }
            var vids = bank[0xa29af127].F.First(f => f.Hash == 0xf5f914d9).V;
            var offs = bank[0x3fe2afd5].F.First(f => f.Hash == 0xe8e591dd).V;
            var vs = Audio.Variations(ix[g], e);
            int offOk = Enumerable.Range(0, Math.Min(vs.Count, offs.Length)).Count(i => 2 * i < offs.Length && ((vs[i].Offset + 3) & 0xFFFF) == (offs[2 * i] & 0xFFFF));
            Console.WriteLine($"NHL {tag[g]} {e.Name}: {vs.Count} takes, bank rows {vids.Length}, offsets matching take order {offOk}");
            Console.WriteLine("  stream offsets " + string.Join(",", vs.Take(8).Select(v => $"{v.Offset}+{v.Length}")) + " | bank " + string.Join(",", offs.Take(8)) + " | e4660a62 " + string.Join(",", bank[0xa29af127].F.First(f => f.Hash == 0xe4660a62).V.Take(8)) + $" | chunks {vs.Select(v => v.ChunkId).Distinct().Count()}");
            for (int i = 0; i < Math.Min(vs.Count, vids.Length); i++)
                if (kw.TryGetValue((uint)vids[i], out var ks))
                    foreach (var k in ks) { if (!takesBy[g].TryGetValue(k, out var l)) takesBy[g][k] = l = new(); l.Add((e.Name, i, vs[i])); }
        }
    }
    var cache = new Dictionary<(int, Guid), byte[]>();
    string Hash(int g, SoundStream s) { if (!cache.TryGetValue((g, s.ChunkId), out var r)) cache[(g, s.ChunkId)] = r = ix[g].Read(s.Chunk); return Convert.ToHexString(SHA1.HashData(r.AsSpan(s.Offset, s.Length))); }
    // every 24 take of the surname gets a 27 take (different audio, not longer); 27's takes are used in turn
    var picks = new List<(uint Id, string Name, List<((string Wave, int Take, SoundStream S) T24, (string Wave, int Take, SoundStream S) T27)> Map)>();
    foreach (var (id, l24) in takesBy[1].OrderBy(x => x.Key))
    {
        if (l24.Count > 6 || !takesBy[0].TryGetValue(id, out var l27) || !names.TryGetValue(id, out var nm)) continue;
        if (wanted.Count > 0 && !wanted.Contains(nm, StringComparer.OrdinalIgnoreCase)) continue;
        var h24 = l24.Select(t => Hash(1, t.S)).ToHashSet();
        var pool = l27.Where(t => !h24.Contains(Hash(0, t.S))).ToList();
        if (pool.Count == 0) continue;
        var map = new List<((string, int, SoundStream), (string, int, SoundStream))>(); int k = 0;
        foreach (var t24 in l24)
        {
            var fit = pool.Where(t => t.S.Header.Seconds <= t24.S.Header.Seconds + 0.05).ToList();
            if (fit.Count == 0) { map = null; break; }
            map.Add((t24, fit[k++ % fit.Count]));
        }
        if (map != null) picks.Add((id, nm, map));
    }
    foreach (var w in wanted)
        foreach (var id in names.Where(x => x.Value.Equals(w, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key))
            Console.WriteLine($"WHY {w} {id}: 24 " + string.Join(" ", takesBy[1].GetValueOrDefault(id, new()).Select(t => $"{t.S.Header.Seconds:0.00}")) + " | 27 " + string.Join(" ", takesBy[0].GetValueOrDefault(id, new()).Select(t => $"{t.S.Header.Seconds:0.00}")));
    Console.WriteLine($"candidates: {picks.Count}");
    foreach (var p in picks.Take(wanted.Count > 0 ? 100 : 5000))
    {
        Console.WriteLine($"NAME {p.Name}	{p.Id}	24 takes {p.Map.Count}, 27 takes {takesBy[0][p.Id].Count}");
        int n = 0;
        foreach (var (t24, t27) in p.Map)
        {
            n++;
            Console.WriteLine($"TAKE	{p.Name}	{t24.Wave}	{t24.Take + 1}	{t24.S.Header.Seconds:0.00}	{p.Name}_{n}_27.wav	27 #{t27.Take + 1} {t27.S.Header.Seconds:0.00}s");
            if (wanted.Count == 0) continue;
            File.WriteAllBytes(Path.Combine(outDir, $"{p.Name}_{n}_24_before.wav"), Audio.Decode(ix[1], t24.S).ToWav());
            File.WriteAllBytes(Path.Combine(outDir, $"{p.Name}_{n}_27.wav"), Audio.Decode(ix[0], t27.S).ToWav());
        }
    }
    return;
}
Dictionary<uint, List<uint>> Keywords(AssetIndex a)
{
    var b = a.Read(a.GetEbx("sound/speech/maincommentary/database/maincommentarykeywords")); var r = new Dictionary<uint, List<uint>>();
    for (int fo = 0x40; fo < 0x90; fo += 8) { int v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(fo)); int t = fo + v; if (v <= 0 || t >= b.Length || t < 4) continue;
        uint n = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(t - 4)); if (n < 100 || n > 300000 || t + (long)n * 16 > b.Length) continue;
        for (int i = 0; i < n; i++) { int q = t + i * 16; int d = q + BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(q)); uint vid = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(q + 8));
            uint c = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(d - 4)); var l = new List<uint>(); for (int k = 0; k < c; k++) l.Add(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(d + k * 8))); r[vid] = l; }
        break; }
    return r;
}
if (waves.Count > 0 && waves[0] == "--names") {
    // keywords per VariationId (maincommentary), per game
    var kw = new Dictionary<uint, List<uint>>[2];
    for (int g = 0; g < 2; g++) {
        var b = ix[g].Read(ix[g].GetEbx("sound/speech/maincommentary/database/maincommentarykeywords")); kw[g] = new();
        for (int fo = 0x40; fo < 0x90; fo += 8) { int v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(fo)); int t = fo + v; if (v <= 0 || t >= b.Length || t < 4) continue;
            uint n = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(t - 4)); if (n < 100 || n > 300000 || t + (long)n * 16 > b.Length) continue;
            for (int i = 0; i < n; i++) { int q = t + i * 16; int d = q + BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(q)); uint vid = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(q + 8));
                uint c = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(d - 4)); var l = new List<uint>(); for (int k = 0; k < c; k++) l.Add(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(d + k * 8))); kw[g][vid] = l; }
            break; }
        Console.WriteLine($"keyword entries NHL {tag[g]}: {kw[g].Count}");
    }
    // surname waves: per leaf name, the surnames (keywords) each game has takes for
    var per = new Dictionary<string, HashSet<uint>[]>();
    for (int g = 0; g < 2; g++)
        foreach (var e in ix[g].Ebx.Values.Where(x => x.Name.StartsWith("sound/speech/maincommentary/soundwaves/") && x.Name.Contains("surname")))
        {
            var res = ix[g].GetRes(e.Name); if (res == null) continue;
            Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)> bank; try { bank = Bank(ix[g].Read(res)); } catch { Console.WriteLine($"  NHL {tag[g]} {e.Name}: bank not readable"); continue; }
            if (!bank.TryGetValue(0xa29af127, out var t)) continue;
            var vf = t.F.FirstOrDefault(f => f.Hash == 0xf5f914d9); if (vf.V == null) continue;
            string leaf = e.Name[(e.Name.LastIndexOf('/') + 1)..];
            if (!per.TryGetValue(leaf, out var sets)) per[leaf] = sets = new[] { new HashSet<uint>(), new HashSet<uint>() };
            int takes = 0;
            foreach (var vid in vf.V) if (kw[g].TryGetValue((uint)vid, out var ks)) { foreach (var k in ks) sets[g].Add(k); takes++; }
            Console.WriteLine($"  NHL {tag[g]} {e.Name}: {t.N} takes, {takes} with keywords");
        }
    var all27 = new HashSet<uint>(); var all24 = new HashSet<uint>();
    foreach (var (leaf, sets) in per.OrderBy(x => x.Key))
    {
        all27.UnionWith(sets[0]); all24.UnionWith(sets[1]);
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, $"kw27_{leaf}.txt"), sets[0].Select(x => x.ToString())); File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, $"kw24_{leaf}.txt"), sets[1].Select(x => x.ToString()));
        Console.WriteLine($"{leaf}: surnames 27 {sets[0].Count}, 24 {sets[1].Count}, in 27 not 24 {sets[0].Count(x => !sets[1].Contains(x))}, in 24 not 27 {sets[1].Count(x => !sets[0].Contains(x))}");
    }
    var newOnes = all27.Where(x => !all24.Contains(x)).OrderBy(x => x).ToList();
    Console.WriteLine($"ALL surname waves: 27 {all27.Count}, 24 {all24.Count}, surnames only 27 has audio for: {newOnes.Count}");
    File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "new_surnames.txt"), newOnes.Select(x => x.ToString()));
    return;
}
uint U(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
// metadata VariationId sets
var meta = new HashSet<uint>[2];
for (int g = 0; g < 2; g++)
{
    var b = ix[g].Read(ix[g].GetEbx("sound/speech/maincommentary/database/maincommentarysentenceplayersamplemetadata"));
    meta[g] = new();
    for (int fo = 0x40; fo < 0x80; fo += 8)
    {
        int v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(fo)); int t = fo + v;
        if (v <= 0 || t >= b.Length || t < 4) continue; uint n = U(b, t - 4);
        if (n < 1000 || n > 200000 || t + (long)n * 16 > b.Length) continue;
        for (int i = 0; i < n; i++) meta[g].Add(U(b, t + i * 16 + 8));
    }
    Console.WriteLine($"metadata ids NHL {tag[g]}: {meta[g].Count}");
}
// generic DSET decode: returns table id -> (count, fields: hash -> values[])
Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)> Bank(byte[] b)
{
    var res = new Dictionary<uint, (int, List<(uint, uint, uint, long[], string)>)>();
    int tab = (int)U(b, 0x18);
    for (int p = tab; p + 8 <= b.Length; p += 8)
    {
        int o = (int)U(b, p); if (o <= 0 || o + 0x40 > b.Length || U(b, o) != 0x44534554) break;
        uint id = U(b, o + 8); int n = (int)U(b, o + 0x38); int nf = (int)(U(b, o + 0x3C) & 0xFFFF);
        var fields = new List<(uint, uint, uint, long[], string)>();
        for (int k = 0; k < nf; k++)
        {
            int d = o + 0x48 + k * 0x18; if (d + 0x18 > b.Length) break;
            uint h = U(b, d), type = U(b, d + 4), bs = U(b, d + 8), col = U(b, d + 0x10);
            int width = (int)((type >> 8) & 0xFF), implicitSeq = (int)((type >> 16) & 0xFF);
            var v = new long[n]; string how;
            if (col == 0 || col >= b.Length) { for (int i = 0; i < n; i++) v[i] = bs + (implicitSeq == 1 ? i : 0); how = implicitSeq == 1 ? "base+i" : "const"; }
            else { for (int i = 0; i < n; i++) { int q = (int)col + i * width; v[i] = q + width > b.Length ? -1 : bs + (width == 1 ? b[q] : width == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(q)) : width == 4 ? (long)U(b, q) : -1); } how = $"col@{col:X} w{width}"; }
            fields.Add((h, type, bs, v, how));
        }
        res[id] = (n, fields);
    }
    return res;
}
foreach (var w in waves)
{
    Console.WriteLine($"\n######## {w}");
    var takes = new List<string>[2]; var banks = new Dictionary<uint, (int N, List<(uint Hash, uint Type, uint Base, long[] V, string How)> F)>[2];
    for (int g = 0; g < 2; g++)
    {
        var e = ix[g].GetEbx(w); if (e == null) { Console.WriteLine($"  not in {tag[g]}"); continue; }
        var vs = Audio.Variations(ix[g], e).Where(s => s.Header != null).ToList();
        var raws = new Dictionary<Guid, byte[]>();
        takes[g] = vs.Select(s => { if (!raws.TryGetValue(s.ChunkId, out var r)) raws[s.ChunkId] = r = ix[g].Read(s.Chunk); return Convert.ToHexString(SHA1.HashData(r.AsSpan(s.Offset, s.Length))); }).ToList();
        banks[g] = Bank(ix[g].Read(ix[g].GetRes(w)));
        Console.WriteLine($"  NHL {tag[g]}: {vs.Count} takes");
        foreach (var (id, t) in banks[g])
        {
            Console.WriteLine($"    DSET {id:x8} rows {t.N}");
            foreach (var f in t.F)
            {
                var vals = f.V; int inMeta = vals.Count(x => x >= 0 && meta[g].Contains((uint)x));
                Console.WriteLine($"      {f.Hash:x8} type {f.Type:x8} base {f.Base} {f.How}: min {vals.DefaultIfEmpty().Min()} max {vals.DefaultIfEmpty().Max()} distinct {vals.Distinct().Count()} in-metadata {inMeta} first {string.Join(",", vals.Take(8))}");
            }
        }
    }
    if (takes[0] == null || takes[1] == null) continue;
    // identical-take pairs: compare per-take columns
    var pos24 = takes[1].Select((h, i) => (h, i)).GroupBy(x => x.h).ToDictionary(x => x.Key, x => x.First().i);
    var pairs = takes[0].Select((h, i) => (I27: i, I24: pos24.GetValueOrDefault(h, -1))).Where(p => p.I24 >= 0).ToList();
    Console.WriteLine($"  identical takes: {pairs.Count} (same position {pairs.Count(p => p.I27 == p.I24)})");
    foreach (var (id, t27) in banks[0])
    {
        if (!banks[1].TryGetValue(id, out var t24) || t27.N != takes[0].Count || t24.N != takes[1].Count) continue;
        foreach (var f in t27.F)
        {
            var f24 = t24.F.FirstOrDefault(x => x.Hash == f.Hash); if (f24.V == null) continue;
            int eq = pairs.Count(p => f.V[p.I27] == f24.V[p.I24]);
            Console.WriteLine($"    {id:x8}.{f.Hash:x8}: equal across identical pairs {eq}/{pairs.Count}   e.g. {string.Join(" ", pairs.Take(5).Select(p => $"{f.V[p.I27]}/{f24.V[p.I24]}"))}");
        }
    }
}
