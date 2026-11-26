using System.Security.Cryptography;
using System.Text.Json;
using NHL27Kit.Core;

// cims-view GAME PACKAGE OVERLAY [--extract] : builds a read-only view of the game as PACKAGE would leave a stock game
// (its TOCs in OVERLAY, its added CAS data extracted there) and saves the package's legacy databases to OVERLAY\out.
// Stock TOC bytes come from the game's TOCs with every kit mod's ranges put back to their old bytes (checked by SHA-256).
// Nothing in the game or the mod list is written.
if (args[0] == "--diff")
{
    // --diff A.db A-meta.xml B.db B-meta.xml : rows per table, and how many rows differ (same row number)
    var a = new T3Db(File.ReadAllBytes(args[1]), File.ReadAllText(args[2]).TrimStart('﻿'));
    var b = new T3Db(File.ReadAllBytes(args[3]), File.ReadAllText(args[4]).TrimStart('﻿'));
    foreach (var ta in a.Tables)
    {
        var tb = b.Table(ta.Name);
        if (tb == null) { Console.WriteLine($"{ta.Name,-40} only in A"); continue; }
        int n = Math.Min(ta.WrittenRecords, tb.WrittenRecords), diff = 0;
        bool sameLayout = ta.RecordSize == tb.RecordSize && ta.Fields.Count == tb.Fields.Count;
        for (int r = 0; r < n; r++)
        {
            bool d = sameLayout ? !a.RecordBytes(ta, r).AsSpan().SequenceEqual(b.RecordBytes(tb, r))
                : ta.Fields.Any(f => tb.Fields.FirstOrDefault(g => g.Name == f.Name) is { } g && !Equals(a.Get(ta, r, f)?.ToString(), b.Get(tb, r, g)?.ToString()));
            if (d) diff++;
        }
        if (diff > 0 || ta.WrittenRecords != tb.WrittenRecords || ta.Records != tb.Records || !sameLayout)
            Console.WriteLine($"{ta.Name,-40} A {ta.WrittenRecords}/{ta.Records}  B {tb.WrittenRecords}/{tb.Records}  differ {diff}{(sameLayout ? "" : "  (layout differs)")}");
    }
    foreach (var tb in b.Tables.Where(t => a.Table(t.Name) == null)) Console.WriteLine($"{tb.Name,-40} only in B");
    return;
}
if (args[0] == "--newvalues")
{
    // --newvalues A.db A-meta B.db B-meta TABLE FIELD... : values of each field that B has and A doesn't (with a player count)
    var a = new T3Db(File.ReadAllBytes(args[1]), File.ReadAllText(args[2]).TrimStart('﻿'));
    var b = new T3Db(File.ReadAllBytes(args[3]), File.ReadAllText(args[4]).TrimStart('﻿'));
    var ta = a.Table(args[5]); var tb = b.Table(args[5]);
    foreach (var fname in args.Skip(6))
    {
        var fa = ta.Fields.First(f => f.Name == fname); var fb = tb.Fields.First(f => f.Name == fname);
        var have = Enumerable.Range(0, ta.WrittenRecords).Where(r => !a.IsInvalid(ta, r)).Select(r => a.Get(ta, r, fa)?.ToString()).ToHashSet();
        var neu = Enumerable.Range(0, tb.WrittenRecords).Where(r => !b.IsInvalid(tb, r)).Select(r => b.Get(tb, r, fb)?.ToString()).Where(v => !have.Contains(v)).GroupBy(v => v).ToList();
        Console.WriteLine($"{fname}: {neu.Count} new value(s), {neu.Sum(g => g.Count())} row(s){(neu.Count > 0 ? ": " + string.Join(", ", neu.Take(30).Select(g => $"{g.Key} x{g.Count()}")) : "")}");
    }
    return;
}
if (args[0] == "--facheck")
{
    // --facheck OLD.db OLD-meta NOW.db NOW-meta : where the old database's free agents are now
    var a = new T3Db(File.ReadAllBytes(args[1]), File.ReadAllText(args[2]).TrimStart('﻿'));
    var b = new T3Db(File.ReadAllBytes(args[3]), File.ReadAllText(args[4]).TrimStart('﻿'));
    IEnumerable<int> Rows(T3Db d, T3Table t) => Enumerable.Range(0, t.WrittenRecords).Where(r => !d.IsInvalid(t, r));
    int I(T3Db d, T3Table t, int r, string f) => (int)d.Get(t, r, t.Fields.First(x => x.Name == f));
    Dictionary<int, int> IdOf(T3Db d) { var t = d.Table("exhibitionplayers"); return Rows(d, t).GroupBy(r => I(d, t, r, "index")).ToDictionary(g => g.Key, g => I(d, t, g.First(), "playerid")); }
    HashSet<int> Fa(T3Db d) { var t = d.Table("exhibitionfreeagents"); var id = IdOf(d); return Rows(d, t).Select(r => id.GetValueOrDefault(I(d, t, r, "playerindex"), -1)).ToHashSet(); }
    Dictionary<int, int> TeamOf(T3Db d) { var t = d.Table("exhibitionrostertable"); var id = IdOf(d); return Rows(d, t).GroupBy(r => id.GetValueOrDefault(I(d, t, r, "playerindex"), -1)).ToDictionary(g => g.Key, g => I(d, t, g.First(), "team")); }
    Dictionary<int, string> Names(T3Db d) { var t = d.Table("exhibitionplayerbiotable"); return Rows(d, t).GroupBy(r => I(d, t, r, "game_id")).ToDictionary(g => g.Key, g => $"{d.Get(t, g.First(), t.Fields.First(x => x.Name == "firstname"))} {d.Get(t, g.First(), t.Fields.First(x => x.Name == "lastname"))}"); }
    var oldFa = Fa(a); var nowFa = Fa(b); var nowIds = IdOf(b).Values.ToHashSet(); var nowTeam = TeamOf(b); var oldNames = Names(a); var nowBio = Names(b);
    Console.WriteLine($"old free agents {oldFa.Count}, now {nowFa.Count}; old ones still free agents {oldFa.Count(nowFa.Contains)}, on a roster now {oldFa.Count(nowTeam.ContainsKey)}, gone from the players table {oldFa.Count(x => !nowIds.Contains(x))}, still with a bio {oldFa.Count(nowBio.ContainsKey)}");
    foreach (var x in oldFa.Where(nowFa.Contains).Take(40)) Console.WriteLine($"   still FA: {x} {oldNames.GetValueOrDefault(x)}");
    foreach (var x in oldFa.Where(x => !nowFa.Contains(x) && nowIds.Contains(x) && !nowTeam.ContainsKey(x)).Take(40)) Console.WriteLine($"   in players, no roster, not FA: {x} {oldNames.GetValueOrDefault(x)}");
    return;
}
if (args[0] == "--dump")
{
    // --dump GAME OUTDIR : the game's legacy databases as they are now
    var gi = AssetIndex.Open(KitPaths.Canonical(args[1]), File.ReadAllBytes, null, null);
    var gl = new Legacy(gi);
    foreach (var lf in gl.Files().Where(x => x.Name.EndsWith(".db") || x.Name.EndsWith("-meta.xml")))
    {
        string dst = Path.Combine(args[2], lf.Name.Replace('/', '\\'));
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.WriteAllBytes(dst, gl.Read(lf));
    }
    Console.WriteLine(string.Join("\n", gl.Names.Where(n => n.EndsWith(".db"))));
    return;
}
string game = KitPaths.Canonical(args[0]), pkgPath = args[1], overlay = Path.GetFullPath(args[2]);
bool extract = args.Contains("--extract");
var pkg = CimsPackage.Open(pkgPath);
var store = new ModStore(KitPaths.ModsFor(GameProfile.Detect(game)));
string realRoot = Path.GetDirectoryName(KitPaths.BaseOf(game));
Console.WriteLine($"{pkg.Title}: {pkg.Files.Count} files; game root {realRoot}; mod list {store.Dir} ({store.Entries.Count} entries)");

static string Hex(byte[] b) => Convert.ToHexString(b);

// the stock bytes of a game file: kit mods' ranges put back, last entry first
byte[] Stock(string full)
{
    var b = File.ReadAllBytes(full);
    string rel = Path.GetRelativePath(game, full).Replace('\\', '/');
    int reverted = 0;
    for (int ei = store.Entries.Count - 1; ei >= 0; ei--)
    {
        var e = store.Entries[ei];
        for (int i = e.Patches.Count - 1; i >= 0; i--)
        {
            var p = e.Patches[i];
            if (!string.Equals(p.File, rel, StringComparison.OrdinalIgnoreCase) || p.Append || p.NewFile || p.Source != null) continue;
            if (p.Offset + p.Length > b.Length) continue;
            if (Hex(SHA1.HashData(b.AsSpan((int)p.Offset, p.Length))) != p.NewSha1) continue;
            var old = File.ReadAllBytes(Path.Combine(store.Dir, e.Id, $"{i}.old"));
            if (old.Length != p.Length) { Console.WriteLine($"   {e.Name} patch {i}: old bytes are another length, skipped"); continue; }
            old.CopyTo(b, (int)p.Offset);
            reverted++;
        }
    }
    if (reverted > 0) Console.WriteLine($"   {rel}: {reverted} kit range(s) put back");
    return b;
}

string Real(string packagePath) => Path.Combine(realRoot, packagePath);
string Over(string packagePath) => Path.Combine(overlay, packagePath);
var byPath = pkg.Files.ToDictionary(f => f.Path.Replace('/', '\\'), StringComparer.OrdinalIgnoreCase);

// 1. TOCs: every TOC of the game, stock, with the package's ranges over the ones it changes
foreach (var dir in new[] { @"game\Data", @"game-UPDATE\Patch" })
{
    foreach (var real in Directory.EnumerateFiles(Real(dir), "*.toc", SearchOption.AllDirectories))
    {
        string rel = Path.GetRelativePath(realRoot, real);
        Directory.CreateDirectory(Path.GetDirectoryName(Over(rel)));
        if (!byPath.TryGetValue(rel, out var f))
        {
            File.WriteAllBytes(Over(rel), Stock(real));
            continue;
        }
        byte[] mod;
        if (f.Ranges.Count == 1 && f.Ranges[0][0] == 0 && f.Ranges[0][1] == f.ModSize) mod = pkg.ReadBlob(f);
        else
        {
            var stock = Stock(real);
            string sh = Hex(SHA256.HashData(stock));
            Console.WriteLine($"   {rel}: stock {(sh == f.StockSha256.ToUpperInvariant() ? "matches" : "DIFFERS")}");
            var blob = pkg.ReadBlob(f);
            mod = new byte[f.ModSize];
            Array.Copy(stock, mod, Math.Min(stock.Length, mod.Length));
            int at = 0;
            foreach (var r in f.Ranges) { Array.Copy(blob, at, mod, r[0], r[1]); at += (int)r[1]; }
        }
        Console.WriteLine($"   {rel}: package copy {(Hex(SHA256.HashData(mod)) == f.ModSha256.ToUpperInvariant() ? "ok" : "HASH DIFFERS")}");
        File.WriteAllBytes(Over(rel), mod);
    }
    foreach (var name in new[] { "layout.toc" }) File.Copy(Path.Combine(Real(dir), name), Path.Combine(Over(dir), name), true);
}

// 2. CAS data: a whole file or a "put"/"add" file is kept whole; added data alone otherwise
string AddFile(CimsFile f) => Over(f.Path.Replace('/', '\\')) + ".added";
bool Whole(CimsFile f) => f.StockSize == 0 || f.Ranges[0][0] == 0;
foreach (var f in pkg.Files.Where(f => f.Path.EndsWith(".cas", StringComparison.OrdinalIgnoreCase)))
{
    string dst = AddFile(f);
    long want = Whole(f) ? f.ModSize : f.ModSize - f.StockSize;
    if (File.Exists(dst) && new FileInfo(dst).Length == want) continue;
    if (!extract) throw new InvalidOperationException($"{f.Path} isn't extracted yet: run with --extract");
    Directory.CreateDirectory(Path.GetDirectoryName(dst));
    Console.WriteLine($"   extracting {f.Path} ({want / 1048576.0:0} MB)");
    using var z = pkg.OpenBlob(f);
    using (var o = new FileStream(dst + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        z.CopyTo(o, 1 << 22);
    if (new FileInfo(dst + ".tmp").Length != want) throw new InvalidDataException($"{f.Path}: extracted {new FileInfo(dst + ".tmp").Length}, expected {want}");
    File.Move(dst + ".tmp", dst, true);
}

// 3. the view: a CAS path below the overlay reads the stock file plus the package's added data
GameLayout.ReadRedirect = path =>
{
    string rel = Path.GetRelativePath(overlay, path);
    if (byPath.TryGetValue(rel, out var f))
    {
        var add = new FileStream(AddFile(f), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Whole(f)) return add;
        return new ConcatStream(new FileStream(Real(rel), FileMode.Open, FileAccess.Read, FileShare.ReadWrite), f.StockSize, add);
    }
    return new FileStream(Real(rel), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
};
var index = AssetIndex.Open(Path.Combine(overlay, "game-UPDATE"), File.ReadAllBytes, null, Console.WriteLine);
var leg = new Legacy(index);
string outDir = Path.Combine(overlay, "out");
Directory.CreateDirectory(outDir);
foreach (var lf in leg.Files().Where(x => x.Name.EndsWith(".db") || x.Name.EndsWith("-meta.xml")))
{
    string dst = Path.Combine(outDir, lf.Name.Replace('/', '\\'));
    Directory.CreateDirectory(Path.GetDirectoryName(dst));
    File.WriteAllBytes(dst, leg.Read(lf));
}
foreach (var name in new[] { "gamemodes/database/t3db/nhl_game.db" }.Concat(leg.Names.Where(n => n.StartsWith("gamemodes/roster/") && n.EndsWith(".db"))))
{
    try
    {
        var db = leg.OpenDb(name);
        var (ok, total) = db.CheckCrcs();
        Console.WriteLine($"{name}: {db.Tables.Count} tables, CRCs {ok}/{total}");
    }
    catch (Exception ex) { Console.WriteLine($"{name}: {ex.Message}"); }
}

/// <summary>A read-only stream: the first <c>len</c> bytes of one stream, then another.</summary>
sealed class ConcatStream : Stream
{
    private readonly Stream _a, _b; private readonly long _len; private long _pos;
    public ConcatStream(Stream a, long len, Stream b) { _a = a; _len = len; _b = b; }
    public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
    public override long Length => _len + _b.Length;
    public override long Position { get => _pos; set => _pos = value; }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_pos < _len)
        {
            _a.Position = _pos;
            int n = _a.Read(buffer, offset, (int)Math.Min(count, _len - _pos));
            _pos += n; return n;
        }
        _b.Position = _pos - _len;
        int m = _b.Read(buffer, offset, count);
        _pos += m; return m;
    }
    public override long Seek(long o, SeekOrigin s) => _pos = s switch { SeekOrigin.Begin => o, SeekOrigin.Current => _pos + o, _ => Length + o };
    public override void Flush() { }
    public override void SetLength(long v) => throw new NotSupportedException();
    public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    protected override void Dispose(bool d) { if (d) { _a.Dispose(); _b.Dispose(); } base.Dispose(d); }
}
