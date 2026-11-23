using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>One file a .cimspkg changes, as its manifest lists it (paths are below the NHL24 PC folder).</summary>
public sealed class CimsFile
{
    public string Path { get; set; }
    /// <summary>"patch" (ranges written over the stock file, which may grow) or "put" (a whole file).</summary>
    public string Action { get; set; }
    public long StockSize { get; set; }
    public string StockSha256 { get; set; }
    public long ModSize { get; set; }
    public string ModSha256 { get; set; }
    /// <summary>The raw-deflate stream holding the ranges' new bytes, one after another.</summary>
    public long BlobOffset { get; set; }
    public long BlobLength { get; set; }
    /// <summary>[offset, length] pairs in the modded file.</summary>
    public List<long[]> Ranges { get; set; } = new();
}

/// <summary>
/// A CIMS mod package (the Center Ice Mod's .cimspkg): "CIMSPKG1", then each file's raw-deflate stream, then the JSON
/// manifest, then its length (u64 little endian) and "CIMSPKG1" again. The manifest gives each file's stock and modded
/// size and SHA-256, so a package can be checked against a game before anything is written.
/// </summary>
public sealed class CimsPackage
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CIMSPKG1");

    public string FilePath { get; private init; }
    public string Name { get; private init; }
    public string Version { get; private init; }
    public string Requires { get; private init; }
    public string Notes { get; private init; }
    public List<CimsFile> Files { get; private init; }

    /// <summary>"Center Ice Mod 2026-11-15": the name the mod list knows it by.</summary>
    public string Title => string.IsNullOrEmpty(Version) ? Name : $"{Name} {Version}";

    /// <summary>The bytes the package adds to the game's files.</summary>
    public long Growth => Files.Where(f => f.Action == "patch").Sum(f => Math.Max(0, f.ModSize - f.StockSize));

    public static CimsPackage Open(string path)
    {
        using var fs = File.OpenRead(path);
        var head = new byte[8];
        if (fs.Length < 32 || fs.Read(head) != 8 || !head.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException($"{System.IO.Path.GetFileName(path)} isn't a CIMS package.");
        var tail = new byte[16];
        fs.Position = fs.Length - 16;
        fs.ReadExactly(tail);
        if (!tail.AsSpan(8).SequenceEqual(Magic)) throw new InvalidDataException($"{System.IO.Path.GetFileName(path)} is cut short (no end marker).");
        long length = BinaryPrimitives.ReadInt64LittleEndian(tail);
        if (length <= 0 || length > fs.Length - 24) throw new InvalidDataException("The package's manifest length is wrong.");
        var json = new byte[length];
        fs.Position = fs.Length - 16 - length;
        fs.ReadExactly(json);
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (r.TryGetProperty("Format", out var f) && f.GetInt32() != 1) throw new InvalidDataException($"Package format {f} isn't supported.");
        var files = r.GetProperty("Files").Deserialize<List<CimsFile>>() ?? new();
        foreach (var x in files)
            if (x.BlobOffset < 8 || x.BlobOffset + x.BlobLength > fs.Length - 16 - length) throw new InvalidDataException($"{x.Path}: its data lies outside the package.");
        return new CimsPackage { FilePath = System.IO.Path.GetFullPath(path), Name = S("Name") ?? "Mod package", Version = S("Version"), Requires = S("Requires"), Notes = S("Notes"), Files = files };
    }

    /// <summary>The decompressed new bytes of a file's ranges (for small files; big ones are streamed).</summary>
    public byte[] ReadBlob(CimsFile f)
    {
        using var fs = File.OpenRead(FilePath);
        using var z = new DeflateStream(new SubStream(fs, f.BlobOffset, f.BlobLength), CompressionMode.Decompress);
        var ms = new MemoryStream();
        z.CopyTo(ms);
        return ms.ToArray();
    }

    public Stream OpenBlob(CimsFile f)
    {
        var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        return new DeflateStream(new SubStream(fs, f.BlobOffset, f.BlobLength, ownsBase: true), CompressionMode.Decompress);
    }
}

/// <summary>A read-only window of another stream.</summary>
public sealed class SubStream : Stream
{
    private readonly Stream _base;
    private readonly long _start, _length;
    private readonly bool _owns;
    private long _pos;

    public SubStream(Stream baseStream, long start, long length, bool ownsBase = false)
    {
        _base = baseStream; _start = start; _length = length; _owns = ownsBase;
    }

    public override bool CanRead => true;
    public override bool CanSeek => _base.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _pos; set => _pos = Math.Clamp(value, 0, _length); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        long left = _length - _pos;
        if (left <= 0) return 0;
        if (buffer.Length > left) buffer = buffer[..(int)left];
        if (_base.Position != _start + _pos) _base.Position = _start + _pos;
        int n = _base.Read(buffer);
        _pos += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _pos + offset, _ => _length + offset };
        return _pos;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && _owns) _base.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Puts a CIMS package into an NHL 24 game (NHL24 PC's game + game-UPDATE pair) as kit mods, so the Mods page shows it
/// and Restore takes it out again. The data entry holds every game-file change: the TOC ranges keep their old and new
/// bytes in the mod's folder (tens of MB); the data added at the end of the CAS files stays in the package and is
/// streamed from it when applied (gigabytes, which the kit doesn't copy). A second entry holds the emulator patch file
/// the package puts in user\patches (its own entry, since the launcher may rewrite that file).
/// Nothing is planned unless every file the package changes is exactly the stock file it was made for (by SHA-256), and
/// a plan checks that the stock bytes plus the package's give exactly the modded file it lists, before anything is kept.
/// </summary>
public static class CimsInstaller
{
    public const string Kind = "package";
    /// <summary>The size of the added ranges the data is cut into (a range is read into memory to check and write it).</summary>
    public const int DefaultChunk = 64 << 20;

    public enum FileState { Stock, Modded, Other, Missing }

    /// <summary>One file's place in the game: its full path and the path the mod list knows it by.</summary>
    public sealed record Target(CimsFile File, string Full, string Rel);

    /// <summary>What the package will do, checked against the game; <see cref="Install"/> keeps it.</summary>
    public sealed class Plan
    {
        public CimsPackage Package { get; init; }
        public string GameDir { get; init; }
        public List<PatchData> Patches { get; } = new();
        public List<SourcedPatch> Sourced { get; } = new();
        public PatchData[] EmulatorPatch { get; set; }
        public string EmulatorNote { get; set; }
        public long Growth { get; set; }
        public List<string> Lines { get; } = new();
    }

    /// <summary>
    /// Where a package path goes: "game\..." in the base game (the Data), "game-UPDATE\..." in the update folder (the
    /// game the kit opened), anything else (user\patches) in the NHL24 PC folder above them.
    /// </summary>
    public static string FullPath(string gameDir, string packagePath)
    {
        string game = KitPaths.Canonical(gameDir);
        string baseDir = KitPaths.BaseOf(game) ?? game;
        string install = Path.GetDirectoryName(baseDir);
        var parts = packagePath.Replace('/', '\\').Split('\\', 2);
        string rest = parts.Length > 1 ? parts[1] : "";
        string first = parts[0];
        string full = first.Equals(Path.GetFileName(baseDir), StringComparison.OrdinalIgnoreCase) && !first.EndsWith("-UPDATE", StringComparison.OrdinalIgnoreCase) ? Path.Combine(baseDir, rest)
            : first.EndsWith("-UPDATE", StringComparison.OrdinalIgnoreCase) ? Path.Combine(game, rest)
            : Path.Combine(install, packagePath);
        full = Path.GetFullPath(full);
        if (!full.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{packagePath} points outside the NHL24 PC folder.");
        return full;
    }

    public static List<Target> Targets(CimsPackage p, string gameDir)
    {
        string game = KitPaths.Canonical(gameDir);
        return p.Files.Select(f =>
        {
            string full = FullPath(game, f.Path);
            return new Target(f, full, Path.GetRelativePath(game, full).Replace('\\', '/'));
        }).ToList();
    }

    /// <summary>The package's shape: a CAS file only grows (one range from its stock end); other ranges stay in the file.</summary>
    public static void Validate(CimsPackage p)
    {
        foreach (var f in p.Files)
        {
            if (f.Action == "put") { if (f.Ranges.Count != 1 || f.Ranges[0][0] != 0 || f.Ranges[0][1] != f.ModSize) throw new InvalidDataException($"{f.Path}: a whole file should be one range."); continue; }
            if (f.Action != "patch") throw new InvalidDataException($"{f.Path}: action \"{f.Action}\" isn't supported.");
            if (f.StockSha256 == null) throw new InvalidDataException($"{f.Path}: the manifest has no stock hash.");
            long end = 0;
            foreach (var r in f.Ranges)
            {
                if (r.Length != 2 || r[0] < end || r[1] <= 0 || r[0] + r[1] > f.ModSize) throw new InvalidDataException($"{f.Path}: its ranges are out of order or outside the file.");
                if (r[0] > f.StockSize) throw new InvalidDataException($"{f.Path}: a range starts past the stock file's end.");
                end = r[0] + r[1];
            }
            if (f.ModSize > f.StockSize && end != f.ModSize) throw new InvalidDataException($"{f.Path}: it grows, but its last range doesn't reach its new end.");
            if (f.ModSize < f.StockSize) throw new InvalidDataException($"{f.Path}: files that shrink aren't supported.");
            // a CAS file grows: one range from its stock end, or the whole file (whose stock part MakePlan checks is unchanged)
            if (f.Path.EndsWith(".cas", StringComparison.OrdinalIgnoreCase) && !IsAppend(f) && !IsWholeGrown(f))
                throw new InvalidDataException($"{f.Path}: a CAS file is expected to only have data added at its end.");
        }
    }

    /// <summary>Each file's state in the game, by size and then SHA-256 (reads every file: tens of GB, a minute or so).</summary>
    public static List<(Target T, FileState State)> Check(CimsPackage p, string gameDir, Action<string> progress = null)
    {
        var list = new List<(Target, FileState)>();
        foreach (var t in Targets(p, gameDir))
        {
            var f = t.File;
            FileState s;
            if (!File.Exists(t.Full)) s = f.Action == "put" ? FileState.Stock : FileState.Missing;
            else
            {
                long len = new FileInfo(t.Full).Length;
                if (f.Action == "put") s = len == f.ModSize && Sha256(t.Full) == f.ModSha256.ToUpperInvariant() ? FileState.Modded : FileState.Other;
                else if (len != f.StockSize && len != f.ModSize) s = FileState.Other;
                else
                {
                    progress?.Invoke($"Checking {t.Rel}");
                    string h = Sha256(t.Full);
                    s = h == f.StockSha256.ToUpperInvariant() ? FileState.Stock : h == f.ModSha256.ToUpperInvariant() ? FileState.Modded : FileState.Other;
                }
            }
            list.Add((t, s));
        }
        return list;
    }

    /// <summary>
    /// Plans the package against the game: every file it patches must be stock. Reads each stock file and the whole
    /// package once, and checks that stock + package bytes give each modded file's SHA-256 (the dry run: nothing is
    /// written, to the game or the mod list).
    /// </summary>
    public static Plan MakePlan(CimsPackage p, string gameDir, Action<string> log = null, int chunk = DefaultChunk)
    {
        Validate(p);
        string game = KitPaths.Canonical(gameDir);
        var plan = new Plan { Package = p, GameDir = game, Growth = p.Growth };
        foreach (var t in Targets(p, game))
        {
            var f = t.File;
            if (f.Action == "put") { PlanPut(plan, t); continue; }
            if (!File.Exists(t.Full)) throw new FileNotFoundException($"{f.Path} isn't in the game ({t.Full}).");
            long len = new FileInfo(t.Full).Length;
            if (len != f.StockSize)
                throw new InvalidDataException(len == f.ModSize
                    ? $"{f.Path} already has this package's changes (installed with its own installer?). Uninstall it there first, so the kit starts from the stock files."
                    : $"{f.Path} isn't the stock file this package was made for ({len:N0} bytes, the package expects {f.StockSize:N0}). The game is another version, or has other mods in it.");
            log?.Invoke($"Checking {t.Rel}");
            using var stockHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            bool whole = IsWholeGrown(f) && f.Path.EndsWith(".cas", StringComparison.OrdinalIgnoreCase);
            if ((IsAppend(f) || whole) && f.StockSize > 0 && (whole || f.ModSize - f.StockSize > chunk))
            {
                // big added data: hash the stock file, then stream the package's bytes on into the modded file's hash. A
                // whole-file range starts with the stock part again: it must be the stock bytes, and only the rest is used
                using var modHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long skip = whole ? f.StockSize : 0;
                var buf = new byte[chunk];
                using var z = p.OpenBlob(f);
                if (whole) SameAsStock(t, z, stockHash, modHash, buf);
                else HashFile(t.Full, stockHash, modHash);
                if (Hex(stockHash.GetHashAndReset()) != f.StockSha256.ToUpperInvariant())
                    throw new InvalidDataException($"{f.Path} isn't the stock file this package was made for (its SHA-256 differs). The game is another version, or has other mods in it.");
                long added = f.ModSize - f.StockSize, done = 0;
                {
                    while (done < added)
                    {
                        int n = (int)Math.Min(chunk, added - done);
                        ReadFull(z, buf, n);
                        modHash.AppendData(buf, 0, n);
                        plan.Sourced.Add(new SourcedPatch(t.Rel, f.StockSize + done, n, Hex(SHA1.HashData(buf.AsSpan(0, n))), $"{p.Title}: {Path.GetFileName(f.Path)} data",
                            p.FilePath, f.BlobOffset, f.BlobLength, skip + done));
                        done += n;
                    }
                }
                if (Hex(modHash.GetHashAndReset()) != f.ModSha256.ToUpperInvariant())
                    throw new InvalidDataException($"{f.Path}: the stock file plus the package's data don't give the modded file the package lists. The package may be damaged.");
                continue;
            }
            // a TOC (or any small file): build the modded file in memory, keep its changed ranges
            var stock = File.ReadAllBytes(t.Full);
            if (Hex(SHA256.HashData(stock)) != f.StockSha256.ToUpperInvariant())
                throw new InvalidDataException($"{f.Path} isn't the stock file this package was made for (its SHA-256 differs). The game is another version, or has other mods in it.");
            var blob = p.ReadBlob(f);
            if (blob.LongLength != f.Ranges.Sum(r => r[1])) throw new InvalidDataException($"{f.Path}: the package's data doesn't match its ranges.");
            var mod = new byte[f.ModSize];
            Array.Copy(stock, mod, Math.Min(stock.Length, mod.Length));
            int at = 0;
            foreach (var r in f.Ranges) { Array.Copy(blob, at, mod, r[0], r[1]); at += (int)r[1]; }
            if (Hex(SHA256.HashData(mod)) != f.ModSha256.ToUpperInvariant())
                throw new InvalidDataException($"{f.Path}: the stock file plus the package's data don't give the modded file the package lists. The package may be damaged.");
            foreach (var r in f.Ranges)
            {
                long a = r[0], b = r[0] + r[1];
                // the part over the stock file, only where bytes really change (a whole-file range keeps only its differences)
                foreach (var (s, e) in Differences(stock, mod, a, Math.Min(b, f.StockSize)))
                    plan.Patches.Add(new PatchData(t.Rel, s, stock[(int)s..(int)e], mod[(int)s..(int)e], $"{p.Title}: {Path.GetFileName(f.Path)}"));
                if (b > f.StockSize)
                    plan.Patches.Add(new PatchData(t.Rel, f.StockSize, Array.Empty<byte>(), mod[(int)f.StockSize..(int)b], $"{p.Title}: {Path.GetFileName(f.Path)} grown", Append: true));
            }
        }
        plan.Lines.Add($"{p.Files.Count(f => f.Action == "patch")} game files match the stock files the package was made for, and the package's data gives each modded file exactly.");
        plan.Lines.Add($"{plan.Patches.Count} TOC ranges ({plan.Patches.Sum(x => (long)x.New.Length) / 1048576.0:0.0} MB kept in the mod list), {plan.Sourced.Count} data ranges ({plan.Sourced.Sum(x => (long)x.Length) / 1073741824.0:0.00} GB read from the package when applied).");
        return plan;
    }

    private static bool IsAppend(CimsFile f) => f.Ranges.Count == 1 && f.Ranges[0][0] == f.StockSize;
    private static bool IsWholeGrown(CimsFile f) => f.Ranges.Count == 1 && f.Ranges[0][0] == 0 && f.Ranges[0][1] == f.ModSize && f.ModSize > f.StockSize;

    // a whole-file range's stock part: reads the stock file and the package's first StockSize bytes side by side, hashing
    // both into the stock and modded hashes; any byte that differs means the package rewrites stock data, which isn't supported
    private static void SameAsStock(Target t, Stream z, IncrementalHash stockHash, IncrementalHash modHash, byte[] buf)
    {
        var mine = new byte[buf.Length];
        using var fs = new FileStream(t.Full, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        long left = t.File.StockSize;
        while (left > 0)
        {
            int n = (int)Math.Min(buf.Length, left);
            ReadFull(fs, mine, n);
            ReadFull(z, buf, n);
            if (!mine.AsSpan(0, n).SequenceEqual(buf.AsSpan(0, n)))
                throw new InvalidDataException($"{t.File.Path}: the package changes this CAS file's stock data, not only adds to it. That isn't supported.");
            stockHash.AppendData(mine, 0, n);
            modHash.AppendData(buf, 0, n);
            left -= n;
        }
    }

    // a changed byte span within [from, to): runs of differences, joined when less than 4 KB apart
    private static IEnumerable<(long, long)> Differences(byte[] a, byte[] b, long from, long to)
    {
        long i = from;
        while (i < to)
        {
            while (i < to && a[i] == b[i]) i++;
            if (i >= to) yield break;
            long start = i, lastDiff = i;
            for (; i < to; i++)
            {
                if (a[i] != b[i]) lastDiff = i;
                else if (i - lastDiff > 4096) break;
            }
            yield return (start, lastDiff + 1);
        }
    }

    private static void PlanPut(Plan plan, Target t)
    {
        var f = t.File;
        var neu = plan.Package.ReadBlob(f);
        if (neu.Length != f.ModSize || Hex(SHA256.HashData(neu)) != f.ModSha256.ToUpperInvariant()) throw new InvalidDataException($"{f.Path}: the package's copy is damaged.");
        var cur = File.Exists(t.Full) ? File.ReadAllBytes(t.Full) : null;
        if (cur != null && cur.AsSpan().SequenceEqual(neu)) { plan.EmulatorNote = $"{f.Path} already holds the package's copy."; return; }
        if (cur != null && cur.Length > neu.Length) { plan.EmulatorNote = $"{f.Path} is longer than the package's copy, so it's left alone (put the package's copy in by hand if you want it)."; return; }
        int keep = cur?.Length ?? 0;
        var list = new List<PatchData>();
        if (keep > 0) list.Add(new PatchData(t.Rel, 0, cur, neu[..keep], $"{plan.Package.Title}: {Path.GetFileName(f.Path)}"));
        if (neu.Length > keep) list.Add(new PatchData(t.Rel, keep, Array.Empty<byte>(), neu[keep..], $"{plan.Package.Title}: {Path.GetFileName(f.Path)}", Append: true));
        plan.EmulatorPatch = list.ToArray();
        plan.EmulatorNote = cur == null ? $"{f.Path} is added (it isn't there now)." : $"{f.Path} is replaced by the package's copy ({cur.Length:N0} → {neu.Length:N0} bytes); Restore puts the current one back.";
    }

    /// <summary>The free space the install needs on the game's drive: the added data, the TOC copies and a margin.</summary>
    public static long SpaceNeeded(Plan plan) => plan.Growth + plan.Patches.Sum(x => (long)x.Old.Length + x.New.Length) + (1L << 30);

    /// <summary>
    /// Keeps the plan as kit mods (the data, and the emulator patch file when there is one) and applies them. Refuses
    /// while an emulator runs or the drive lacks room. Returns the entries.
    /// </summary>
    public static List<ModEntry> Install(Plan plan, ModStore store, Action<string> log = null)
    {
        if (Emulators.Running() is { } emu) throw new InvalidOperationException($"{emu} is running. Close the game before changing its files.");
        var drive = new DriveInfo(Path.GetPathRoot(plan.GameDir));
        long need = SpaceNeeded(plan);
        if (drive.AvailableFreeSpace < need)
            throw new IOException($"Not enough free space on {drive.Name}: the package needs {need / 1073741824.0:0.0} GB and there's {drive.AvailableFreeSpace / 1073741824.0:0.0} GB. Nothing was written.");
        var p = plan.Package;
        var entries = new List<ModEntry>();
        if (store.Find(Kind, p.Title) is { } old && store.State(old, plan.GameDir) != ModState.NotApplied)
            throw new InvalidOperationException($"{p.Title} is already in the mod list and applied. Restore it first to install it again.");
        var data = store.Begin(Kind, p.Title, p.FilePath, $"{p.Name} ({p.Version}): {p.Files.Count(f => f.Action == "patch")} game files, {plan.Growth / 1073741824.0:0.0} GB added; the added data is read from the package when applied");
        store.AddPatches(data, plan.Patches);
        store.AddSourcedPatches(data, plan.Sourced);
        store.Commit(data);
        entries.Add(data);
        if (plan.EmulatorPatch is { Length: > 0 })
        {
            // the mod list writes into files that are there: a missing patch file starts empty (Restore leaves it empty)
            string file = Path.Combine(plan.GameDir, plan.EmulatorPatch[0].File.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, Array.Empty<byte>()); }
            var e = store.Begin(Kind, p.Title + " (emulator patches)", p.FilePath, plan.EmulatorNote);
            store.AddPatches(e, plan.EmulatorPatch);
            store.Commit(e);
            entries.Add(e);
        }
        foreach (var e in entries)
        {
            log?.Invoke($"Applying {e.Name}");
            store.Apply(e, plan.GameDir, log);
        }
        return entries;
    }

    private static void HashFile(string file, params IncrementalHash[] hashes)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        var buf = System.Buffers.ArrayPool<byte>.Shared.Rent(4 << 20);
        try
        {
            int n;
            while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                foreach (var h in hashes) h.AppendData(buf, 0, n);
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buf); }
    }

    private static string Sha256(string file)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        HashFile(file, h);
        return Hex(h.GetHashAndReset());
    }

    private static void ReadFull(Stream s, byte[] buf, int count)
    {
        for (int got = 0; got < count;)
        {
            int n = s.Read(buf, got, count - got);
            if (n <= 0) throw new EndOfStreamException("The package's data ended early.");
            got += n;
        }
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b);
}
