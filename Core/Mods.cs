using System.Security.Cryptography;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>One byte range of a game file a mod changes: the bytes before and after, kept in the mod's folder.</summary>
public sealed class RangePatch
{
    /// <summary>Path below the game folder, with forward slashes.</summary>
    public string File { get; set; }
    public long Offset { get; set; }
    public int Length { get; set; }
    public string OldSha1 { get; set; }
    public string NewSha1 { get; set; }
    public string Note { get; set; }
    /// <summary>
    /// The range is added at the end of the file (Offset = its length before). Restoring cuts the file back when the range
    /// is at the end; otherwise it stays, unused, until what was added after it is restored too (<see cref="ModStore"/>).
    /// </summary>
    public bool Append { get; set; }
    /// <summary>
    /// A whole file the mod adds (a loose file the game looks for, none there before): its old bytes are "no file".
    /// Applying writes it (making its folders); restoring deletes it while it holds what was written.
    /// </summary>
    public bool NewFile { get; set; }
    /// <summary>For an added file: the folder (below the game folder, like File) whose empty subfolders a restore removes.</summary>
    public string KeepDir { get; set; }

    // A range too big to keep a copy of (a mod package's data, gigabytes) reads its new bytes from the package instead:
    // Source is the package file, the range's bytes are the deflate stream at SourceOffset (SourceLength compressed
    // bytes) once SourceSkip decompressed bytes are passed. Only added ranges use it; their old bytes are empty.
    public string Source { get; set; }
    public long SourceOffset { get; set; }
    public long SourceLength { get; set; }
    public long SourceSkip { get; set; }
}

/// <summary>An added range whose new bytes stay in a package (<see cref="RangePatch.Source"/>), with their SHA1.</summary>
public sealed record SourcedPatch(string File, long Offset, int Length, string NewSha1, string Note, string Source, long SourceOffset, long SourceLength, long SourceSkip);

/// <summary>A replacement of one asset: what it replaces, and the ranges it writes.</summary>
public sealed class ModEntry
{
    public string Id { get; set; }
    /// <summary>"texture" or "sound".</summary>
    public string Kind { get; set; }
    /// <summary>The asset's name (the key: one entry per asset).</summary>
    public string Name { get; set; }
    public string Source { get; set; }
    public string Summary { get; set; }
    public List<string> Warnings { get; set; } = new();
    public DateTime Created { get; set; } = DateTime.Now;
    public List<RangePatch> Patches { get; set; } = new();
}

public enum ModState { NotApplied, Applied, Partial, Changed, Missing }

/// <summary>
/// The mod list and what it does to the game folder. A mod is a set of byte ranges: applying writes each range's new
/// bytes, restoring writes its old bytes back, and every step first checks the range holds what it expects (by SHA1), so
/// nothing is overwritten that the kit did not put there or read from there.
/// Layout: &lt;dir&gt;\mods.json, and per mod &lt;dir&gt;\&lt;id&gt;\&lt;n&gt;.old and .new.
/// </summary>
public sealed class ModStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Dir { get; }
    public List<ModEntry> Entries { get; private set; } = new();

    public ModStore(string dir)
    {
        Dir = dir;
        Load();
    }

    private string ListFile => Path.Combine(Dir, "mods.json");
    private string LeftoverFile => Path.Combine(Dir, "leftovers.json");

    /// <summary>
    /// Data that removed mods added at the end of game files and that may still be there (a restore leaves added data
    /// that isn't at the end of its file): trimmed like the list's own once it reaches the end.
    /// </summary>
    private List<RangePatch> _leftovers = new();

    private void Load()
    {
        try
        {
            Entries = File.Exists(ListFile) ? JsonSerializer.Deserialize<List<ModEntry>>(File.ReadAllText(ListFile)) ?? new() : new();
            _leftovers = File.Exists(LeftoverFile) ? JsonSerializer.Deserialize<List<RangePatch>>(File.ReadAllText(LeftoverFile)) ?? new() : new();
        }
        catch (Exception e)
        {
            throw new InvalidDataException($"The mod list {ListFile} can't be read: {e.Message}. Fix or move it; the kit won't replace it.");
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(ListFile + ".tmp", JsonSerializer.Serialize(Entries, Json));
        File.Move(ListFile + ".tmp", ListFile, overwrite: true);
        if (_leftovers.Count > 0 || File.Exists(LeftoverFile))
        {
            File.WriteAllText(LeftoverFile + ".tmp", JsonSerializer.Serialize(_leftovers, Json));
            File.Move(LeftoverFile + ".tmp", LeftoverFile, overwrite: true);
        }
    }

    public ModEntry Find(string kind, string name) =>
        Entries.FirstOrDefault(e => e.Kind == kind && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    private string Old(ModEntry e, int n) => Path.Combine(Dir, e.Id, $"{n}.old");
    private string New(ModEntry e, int n) => Path.Combine(Dir, e.Id, $"{n}.new");

    /// <summary>Adds a mod, replacing any entry for the same asset (the caller restores that one first).</summary>
    public ModEntry Add(string kind, string name, string source, string summary, IReadOnlyList<PatchData> patches, IEnumerable<string> warnings = null)
    {
        var existing = Find(kind, name);
        if (existing != null) Remove(existing);
        var e = Begin(kind, name, source, summary, warnings);
        AddPatches(e, patches);
        Commit(e);
        return e;
    }

    /// <summary>
    /// Starts a mod whose ranges are added in parts (<see cref="AddPatches"/>, then <see cref="Commit"/>), for one too
    /// big to hold in memory at once. It isn't in the list until it's committed.
    /// </summary>
    public ModEntry Begin(string kind, string name, string source, string summary, IEnumerable<string> warnings = null)
    {
        var e = new ModEntry { Id = Guid.NewGuid().ToString("N")[..12], Kind = kind, Name = name, Source = source, Summary = summary, Warnings = warnings?.ToList() ?? new() };
        Directory.CreateDirectory(Path.Combine(Dir, e.Id));
        return e;
    }

    /// <summary>Keeps more ranges of a begun mod (their old and new bytes go to its folder).</summary>
    public void AddPatches(ModEntry e, IEnumerable<PatchData> patches)
    {
        foreach (var p in patches)
        {
            int i = e.Patches.Count;
            File.WriteAllBytes(Old(e, i), p.Old);
            File.WriteAllBytes(New(e, i), p.New);
            e.Patches.Add(new RangePatch { File = p.File, Offset = p.Offset, Length = p.New.Length, OldSha1 = Hex(SHA1.HashData(p.Old)), NewSha1 = Hex(SHA1.HashData(p.New)), Note = p.Note, Append = p.Append, NewFile = p.NewFile, KeepDir = p.KeepDir });
        }
    }

    /// <summary>Keeps added ranges of a begun mod whose new bytes stay in their package (nothing is copied).</summary>
    public void AddSourcedPatches(ModEntry e, IEnumerable<SourcedPatch> patches)
    {
        string empty = Hex(SHA1.HashData(Array.Empty<byte>()));
        foreach (var p in patches)
            e.Patches.Add(new RangePatch
            {
                File = p.File, Offset = p.Offset, Length = p.Length, OldSha1 = empty, NewSha1 = p.NewSha1, Note = p.Note, Append = true,
                Source = p.Source, SourceOffset = p.SourceOffset, SourceLength = p.SourceLength, SourceSkip = p.SourceSkip,
            });
    }

    /// <summary>Puts a begun mod in the list, replacing any entry for the same asset (the caller restores that one first).</summary>
    public void Commit(ModEntry e)
    {
        if (Find(e.Kind, e.Name) is { } existing && existing != e) Remove(existing);
        if (!Entries.Contains(e)) Entries.Add(e);
        Save();
    }

    /// <summary>Forgets a mod. It must not be applied: its backups go with it.</summary>
    public void Remove(ModEntry e)
    {
        Entries.Remove(e);
        _leftovers.AddRange(e.Patches.Where(p => p.Append));
        Save();
        try { Directory.Delete(Path.Combine(Dir, e.Id), recursive: true); } catch { /* leftovers are harmless */ }
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b);

    private static string Path_(string game, RangePatch p) => Path.Combine(game, p.File.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// The SHA1 a range's place holds now, or null when the file is too short. An appended range reads as its old
    /// (empty) bytes while the file ends where it began.
    /// </summary>
    private static string Current(string file, RangePatch p)
    {
        // an added file: "no file" reads as its old (empty) bytes; a file of another length as something else
        if (p.NewFile)
        {
            if (!File.Exists(file)) return Hex(SHA1.HashData(Array.Empty<byte>()));
            long len = new FileInfo(file).Length;
            return len == p.Length ? HashRange(file, 0, p.Length) : $"length {len}";
        }
        // an earlier range of the same mod may be added before it, so a file that ends at or before it hasn't got it
        if (p.Append && new FileInfo(file).Length <= p.Offset) return Hex(SHA1.HashData(Array.Empty<byte>()));
        return HashRange(file, p.Offset, p.Length);
    }

    /// <summary>
    /// The SHA1 of a file range, or null when the file is too short. Read through one pooled buffer: checking every
    /// mod's state reads well over 100 MB, which as one array per range would leave the large-object heap in holes.
    /// </summary>
    private static string HashRange(string file, long offset, int length)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.SequentialScan);
        if (offset + length > fs.Length) return null;
        fs.Position = offset;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buf = System.Buffers.ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            for (int left = length; left > 0;)
            {
                int n = fs.Read(buf, 0, Math.Min(buf.Length, left));
                if (n <= 0) return null;
                sha.AppendData(buf, 0, n);
                left -= n;
            }
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buf); }
        return Hex(sha.GetHashAndReset());
    }

    /// <summary>What the game folder holds for this mod: all old, all new, a mix, or something else.</summary>
    public ModState State(ModEntry e, string game)
    {
        int old = 0, neu = 0, other = 0;
        // added data says nothing on its own (it may stay after a restore); the ranges that point at it decide
        bool appendsCount = e.Patches.All(p => p.Append);
        foreach (var p in e.Patches)
        {
            if (p.Append && !appendsCount) continue;
            string file = Path_(game, p);
            if (!p.NewFile && !File.Exists(file)) return ModState.Missing;
            string h = Current(file, p);
            if (h == null) return ModState.Missing;
            // a range whose old and new bytes are the same (a size that didn't change) says nothing either way
            if (p.OldSha1 == p.NewSha1 && h == p.NewSha1) continue;
            if (h == p.NewSha1) neu++;
            else if (h == p.OldSha1) old++;
            else other++;
        }
        if (other > 0) return ModState.Changed;
        if (old == 0) return ModState.Applied;
        return neu == 0 ? ModState.NotApplied : ModState.Partial;
    }

    /// <summary>Writes the mod's new bytes. Every range must hold its old or new bytes.</summary>
    public void Apply(ModEntry e, string game, Action<string> log = null) => Write(e, game, toNew: true, log);

    /// <summary>Writes the mod's old bytes back.</summary>
    public void Restore(ModEntry e, string game, Action<string> log = null) => Write(e, game, toNew: false, log);

    private void Write(ModEntry e, string game, bool toNew, Action<string> log)
    {
        string verb = toNew ? "apply" : "restore";
        if (toNew) ResumeSourced(e, game, log);
        if (toNew) MoveAppended(e, game, log);
        // check everything before writing anything
        for (int i = 0; i < e.Patches.Count; i++)
        {
            var p = e.Patches[i];
            string file = Path_(game, p);
            if (!p.NewFile && !File.Exists(file)) throw new FileNotFoundException($"Can't {verb} {e.Name}: {p.File} is not in the game folder.");
            // added data needs no check to restore: nothing points at it afterwards, and TrimAdded cuts only what it recognises
            if (p.Append && !toNew) continue;
            string h = Current(file, p) ?? throw new InvalidDataException($"Can't {verb} {e.Name}: {p.File} is shorter than the patch expects.");
            if (h != p.OldSha1 && h != p.NewSha1)
                throw new InvalidDataException($"Can't {verb} {e.Name}: {p.File} at 0x{p.Offset:X} holds neither the original nor the replacement. The game files changed since this mod was made; nothing was written.");
        }
        int written = 0;
        // data read from a package goes in first, so the TOC ranges that point at it come after it
        if (toNew) written += WriteSourced(e, game, log);
        // restoring goes backwards, so appended ranges are cut off last-added first
        foreach (int i in toNew ? Enumerable.Range(0, e.Patches.Count) : Enumerable.Range(0, e.Patches.Count).Reverse())
        {
            var p = e.Patches[i];
            if (p.Source != null) continue;
            string file = Path_(game, p);
            string want = toNew ? p.NewSha1 : p.OldSha1;
            if (Current(file, p) == want) continue;
            // added data comes off after the ranges pointing at it are back (TrimAdded)
            if (p.Append && !toNew) continue;
            var data = File.ReadAllBytes(toNew ? New(e, i) : Old(e, i));
            if (Hex(SHA1.HashData(data)) != want) throw new InvalidDataException($"The stored {(toNew ? "new" : "old")} bytes of {e.Name} (patch {i}) are damaged.");
            if (p.NewFile)
            {
                if (toNew)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllBytes(file + ".tmp", data);
                    File.Move(file + ".tmp", file, overwrite: true);
                }
                else
                {
                    File.Delete(file);
                    if (p.KeepDir != null) DeleteEmptyFolders(Path.GetDirectoryName(file), Path.Combine(game, p.KeepDir.Replace('/', Path.DirectorySeparatorChar)));
                }
                if (Current(file, p) != want) throw new IOException($"{p.File} was not {(toNew ? "written" : "deleted")}.");
                written++;
                log?.Invoke($"{p.Note ?? "file"}: {(toNew ? "added" : "deleted")} {p.File}");
                continue;
            }
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Position = p.Offset;
                fs.Write(data);
                fs.Flush(true);
            }
            if (Current(file, p) != want)
                throw new IOException($"{p.File} did not hold what was written at 0x{p.Offset:X}.");
            written++;
            log?.Invoke($"{p.Note ?? "range"}: {p.Length:N0} bytes at 0x{p.Offset:X} in {p.File}");
        }
        if (!toNew)
            foreach (var f in e.Patches.Where(p => p.Append).Select(p => p.File).Distinct(StringComparer.OrdinalIgnoreCase))
                written += TrimAdded(game, f, log);
        log?.Invoke($"{e.Name}: {written} range(s) written, {e.Patches.Count - written} already as wanted");
    }

    /// <summary>
    /// Deletes <paramref name="dir"/> and its parents while they're empty, stopping below <paramref name="keep"/> (a
    /// loose file's folders that the mod made; a folder with anything else in it stays, and so does keep itself).
    /// </summary>
    private static void DeleteEmptyFolders(string dir, string keep)
    {
        string stop = Path.GetFullPath(keep).TrimEnd(Path.DirectorySeparatorChar);
        for (string d = Path.GetFullPath(dir); d != null && d.Length > stop.Length + 1 && d.StartsWith(stop + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); d = Path.GetDirectoryName(d))
        {
            try
            {
                if (!Directory.Exists(d) || Directory.EnumerateFileSystemEntries(d).Any()) return;
                Directory.Delete(d);
            }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
        }
    }

    /// <summary>
    /// An apply of package data that stopped partway (a crash, power loss) leaves a file ending inside one of its added
    /// ranges: the file is cut back to where that range starts, so the apply carries on from there. Only when every range
    /// before it in that file already holds its new bytes and the partial bytes are the start of that range's own bytes
    /// (read from the package), so the data cut can only be this mod's. Anything else is left for the checks to refuse.
    /// </summary>
    private void ResumeSourced(ModEntry e, string game, Action<string> log)
    {
        foreach (var group in e.Patches.Where(p => p.Source != null).GroupBy(p => p.File, StringComparer.OrdinalIgnoreCase))
        {
            string file = Path_(game, group.First());
            if (!File.Exists(file)) continue;
            long length = new FileInfo(file).Length;
            var ranges = group.OrderBy(p => p.Offset).ToList();
            var cut = ranges.FirstOrDefault(p => length > p.Offset && length < p.Offset + p.Length);
            if (cut == null || !File.Exists(cut.Source)) continue;
            if (ranges.TakeWhile(p => p != cut).Any(p => Current(file, p) != p.NewSha1)) continue;
            int partial = (int)(length - cut.Offset);
            var have = new byte[partial];
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { fs.Position = cut.Offset; ReadFull(fs, have, partial); }
            var ours = new byte[partial];
            using (var pkg = new FileStream(cut.Source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var inflate = new System.IO.Compression.DeflateStream(new SubStream(pkg, cut.SourceOffset, cut.SourceLength), System.IO.Compression.CompressionMode.Decompress))
            {
                Skip(inflate, cut.SourceSkip);
                ReadFull(inflate, ours, partial);
            }
            if (!have.AsSpan().SequenceEqual(ours)) continue;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { fs.SetLength(cut.Offset); fs.Flush(true); }
            log?.Invoke($"{cut.File}: an earlier apply stopped partway; cut back to 0x{cut.Offset:X} to carry on");
        }
    }

    /// <summary>
    /// Writes the ranges whose new bytes stay in a package: each package stream is read once from its start, and the
    /// ranges along it are written in order (those already in place are passed over, so a stopped apply carries on).
    /// </summary>
    private int WriteSourced(ModEntry e, string game, Action<string> log)
    {
        int written = 0;
        foreach (var stream in e.Patches.Where(p => p.Source != null).GroupBy(p => (p.Source, p.SourceOffset, p.SourceLength)))
        {
            var ranges = stream.OrderBy(p => p.SourceSkip).ToList();
            var todo = ranges.Where(p => Current(Path_(game, p), p) != p.NewSha1).ToList();
            if (todo.Count == 0) continue;
            if (!File.Exists(stream.Key.Source))
                throw new FileNotFoundException($"Can't apply {e.Name}: its package {stream.Key.Source} isn't there any more. Put it back, or remove the mod and import the package again.");
            using var pkg = new FileStream(stream.Key.Source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
            using var inflate = new System.IO.Compression.DeflateStream(new SubStream(pkg, stream.Key.SourceOffset, stream.Key.SourceLength), System.IO.Compression.CompressionMode.Decompress);
            long at = 0, last = todo.Max(p => p.SourceSkip);
            byte[] buf = null;
            foreach (var p in ranges)
            {
                if (p.SourceSkip > last) break;
                if (p.SourceSkip < at) throw new InvalidDataException($"The package ranges of {e.Name} overlap.");
                Skip(inflate, p.SourceSkip - at);
                at = p.SourceSkip;
                bool needed = todo.Contains(p);
                if (buf == null || buf.Length < p.Length) buf = new byte[p.Length];
                ReadFull(inflate, buf, p.Length);
                at += p.Length;
                if (!needed) continue;
                if (Hex(SHA1.HashData(buf.AsSpan(0, p.Length))) != p.NewSha1)
                    throw new InvalidDataException($"The package {stream.Key.Source} doesn't hold what {e.Name} expects at {p.File} 0x{p.Offset:X} (it changed since it was imported).");
                string file = Path_(game, p);
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                {
                    if (fs.Length != p.Offset) throw new IOException($"{p.File} is 0x{fs.Length:X} bytes long; {e.Name}'s data has to go at 0x{p.Offset:X}. Nothing more was written.");
                    fs.Position = p.Offset;
                    fs.Write(buf, 0, p.Length);
                    fs.Flush(true);
                }
                if (Current(file, p) != p.NewSha1) throw new IOException($"{p.File} did not hold what was written at 0x{p.Offset:X}.");
                written++;
                log?.Invoke($"{p.Note ?? "range"}: {p.Length:N0} bytes at 0x{p.Offset:X} in {p.File}");
            }
        }
        return written;
    }

    private static void Skip(Stream s, long count)
    {
        if (count <= 0) return;
        var buf = System.Buffers.ArrayPool<byte>.Shared.Rent(1 << 20);
        try
        {
            while (count > 0)
            {
                int n = s.Read(buf, 0, (int)Math.Min(buf.Length, count));
                if (n <= 0) throw new EndOfStreamException("The package stream ended early.");
                count -= n;
            }
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buf); }
    }

    private static void ReadFull(Stream s, byte[] buf, int count)
    {
        for (int got = 0; got < count;)
        {
            int n = s.Read(buf, got, count - got);
            if (n <= 0) throw new EndOfStreamException("The package stream ended early.");
            got += n;
        }
    }

    /// <summary>
    /// A mod not in the game whose added data's place now holds something else (restoring it cut the file, then another
    /// mod added data there): its data goes at the file's end instead, and the TOC offsets it writes follow. Nothing
    /// changes unless every offset can follow.
    /// </summary>
    private void MoveAppended(ModEntry e, string game, Action<string> log)
    {
        var moves = new List<(int Index, byte[] Data, long At)>();
        var offsets = new Dictionary<uint, uint>();
        foreach (var group in e.Patches.Select((p, i) => (P: p, I: i)).Where(x => x.P.Append).GroupBy(x => x.P.File, StringComparer.OrdinalIgnoreCase))
        {
            string file = Path_(game, group.First().P);
            if (!File.Exists(file)) continue;
            long end = new FileInfo(file).Length;
            var pending = group.Where(x => Current(file, x.P) != x.P.NewSha1).ToList();
            if (pending.All(x => end <= x.P.Offset)) continue;
            // a package's data can't move: its TOCs come whole from the package, with the offsets it was made for
            if (e.Kind == CimsInstaller.Kind || pending.Any(x => x.P.Source != null))
                throw new InvalidDataException($"Can't apply {e.Name}: {group.First().P.File} has had data added where this package's data has to go. Restore the mods applied after it was removed first; nothing was written.");
            foreach (var (p, i) in pending)
            {
                var data = File.ReadAllBytes(New(e, i));
                if (Hex(SHA1.HashData(data)) != p.NewSha1) throw new InvalidDataException($"The stored new bytes of {e.Name} (patch {i}) are damaged.");
                long oldAt = (p.Offset + 15) & ~15L, newAt = (end + 15) & ~15L;
                int pad = (int)(oldAt - p.Offset);
                var moved = new byte[newAt - end + data.Length - pad];
                data.AsSpan(pad).CopyTo(moved.AsSpan((int)(newAt - end)));
                if (newAt + data.Length > uint.MaxValue) throw new InvalidDataException($"{p.File} would pass 4 GB, past what a TOC offset holds.");
                if (offsets.ContainsKey((uint)oldAt)) throw new InvalidDataException($"Can't apply {e.Name}: two of its added ranges start at 0x{oldAt:X}, so they can't be moved.");
                offsets[(uint)oldAt] = (uint)newAt;
                moves.Add((i, moved, end));
                end += moved.Length;
            }
        }
        if (moves.Count == 0) return;

        // the TOC offsets that point at the added data, all still as the game shipped them
        var pointers = new List<(int Index, byte[] Data)>();
        var followed = new HashSet<uint>();
        for (int i = 0; i < e.Patches.Count; i++)
        {
            var p = e.Patches[i];
            if (p.Append || p.Length != 4 || p.Note == null || !p.Note.EndsWith(" offset", StringComparison.Ordinal)) continue;
            var cur = File.ReadAllBytes(New(e, i));
            uint at = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cur);
            if (!offsets.TryGetValue(at, out uint to)) continue;
            string file = Path_(game, p);
            if (!File.Exists(file) || Current(file, p) != p.OldSha1)
                throw new InvalidDataException($"Can't apply {e.Name}: {p.File} at 0x{p.Offset:X} isn't as the game shipped it, so this mod's added data can't be moved; nothing was written.");
            var neu = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(neu, to);
            pointers.Add((i, neu));
            followed.Add(at);
        }
        if (followed.Count != offsets.Count)
            throw new InvalidDataException($"Can't apply {e.Name}: its added data's place in the CAS file is taken, and this mod doesn't record every TOC offset that points at it. Restore the mods added after it first; nothing was written.");

        foreach (var (i, data, at) in moves)
        {
            var p = e.Patches[i];
            log?.Invoke($"{p.Note ?? "range"}: its place at 0x{p.Offset:X} in {p.File} is taken, moved to 0x{at:X}");
            File.WriteAllBytes(New(e, i), data);
            p.Offset = at;
            p.Length = data.Length;
            p.NewSha1 = Hex(SHA1.HashData(data));
        }
        foreach (var (i, data) in pointers)
        {
            File.WriteAllBytes(New(e, i), data);
            e.Patches[i].NewSha1 = Hex(SHA1.HashData(data));
        }
        Save();
    }

    /// <summary>
    /// Cuts data the mod list added off the end of <paramref name="rel"/> while the data at the end belongs to a mod
    /// that isn't applied (nothing points at it any more), so restoring every mod gives back the original file even
    /// when they were restored out of order. Returns how many ranges were cut.
    /// </summary>
    private int TrimAdded(string game, string rel, Action<string> log)
    {
        string file = Path.Combine(game, rel.Replace('/', Path.DirectorySeparatorChar));
        int cut = 0;
        while (true)
        {
            long length = new FileInfo(file).Length;
            bool Here(RangePatch p) => p.Append && string.Equals(p.File, rel, StringComparison.OrdinalIgnoreCase);
            // leftovers that were cut, or whose place a listed mod uses now, are forgotten
            int dropped = _leftovers.RemoveAll(p => Here(p) && (p.Offset >= length || Entries.Any(x => x.Patches.Any(q => Here(q) && q.Offset == p.Offset))));
            if (dropped > 0) Save();
            var last = Entries.SelectMany(x => x.Patches.Select(p => (Entry: x, Patch: p)))
                .Concat(_leftovers.Select(p => (Entry: (ModEntry)null, Patch: p)))
                .FirstOrDefault(t => Here(t.Patch) && t.Patch.Offset + t.Patch.Length == length && t.Patch.Offset < length);
            if (last.Patch == null) break;
            if (last.Entry == null)
            {
                if (Current(file, last.Patch) != last.Patch.NewSha1) break;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { fs.SetLength(last.Patch.Offset); fs.Flush(true); }
                _leftovers.Remove(last.Patch);
                Save();
                log?.Invoke($"{last.Patch.Note ?? "range"}: cut {rel} back to 0x{last.Patch.Offset:X} (left by an earlier save)");
                cut++;
                continue;
            }
            // only when nothing of its mod points at it, and it's the data the mod added
            if (last.Entry.Patches.Any(p => !p.Append && File.Exists(Path_(game, p)) && Current(Path_(game, p), p) is { } h && h != p.OldSha1)) break;
            if (Current(file, last.Patch) != last.Patch.NewSha1) break;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { fs.SetLength(last.Patch.Offset); fs.Flush(true); }
            if (new FileInfo(file).Length != last.Patch.Offset) throw new IOException($"{rel} was not cut back to 0x{last.Patch.Offset:X}.");
            log?.Invoke($"{last.Patch.Note ?? "range"}: cut {rel} back to 0x{last.Patch.Offset:X} ({last.Entry.Name})");
            cut++;
        }
        return cut;
    }
}
