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
    /// <summary>The range is added at the end of the file (Offset = its length before); restoring cuts the file back.</summary>
    public bool Append { get; set; }
}

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

    private void Load()
    {
        try
        {
            Entries = File.Exists(ListFile) ? JsonSerializer.Deserialize<List<ModEntry>>(File.ReadAllText(ListFile)) ?? new() : new();
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
            e.Patches.Add(new RangePatch { File = p.File, Offset = p.Offset, Length = p.New.Length, OldSha1 = Hex(SHA1.HashData(p.Old)), NewSha1 = Hex(SHA1.HashData(p.New)), Note = p.Note, Append = p.Append });
        }
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
        Save();
        try { Directory.Delete(Path.Combine(Dir, e.Id), recursive: true); } catch { /* leftovers are harmless */ }
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b);

    private static byte[] ReadRange(string file, long offset, int length)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (offset + length > fs.Length) return null;
        fs.Position = offset;
        var b = new byte[length];
        fs.ReadExactly(b);
        return b;
    }

    private static string Path_(string game, RangePatch p) => Path.Combine(game, p.File.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// The SHA1 a range's place holds now, or null when the file is too short. An appended range reads as its old
    /// (empty) bytes while the file ends where it began.
    /// </summary>
    private static string Current(string file, RangePatch p)
    {
        // an earlier range of the same mod may be added before it, so a file that ends at or before it hasn't got it
        if (p.Append && new FileInfo(file).Length <= p.Offset) return Hex(SHA1.HashData(Array.Empty<byte>()));
        var cur = ReadRange(file, p.Offset, p.Length);
        return cur == null ? null : Hex(SHA1.HashData(cur));
    }

    /// <summary>What the game folder holds for this mod: all old, all new, a mix, or something else.</summary>
    public ModState State(ModEntry e, string game)
    {
        int old = 0, neu = 0, other = 0;
        foreach (var p in e.Patches)
        {
            string file = Path_(game, p);
            if (!File.Exists(file)) return ModState.Missing;
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
        // check everything before writing anything
        for (int i = 0; i < e.Patches.Count; i++)
        {
            var p = e.Patches[i];
            string file = Path_(game, p);
            if (!File.Exists(file)) throw new FileNotFoundException($"Can't {verb} {e.Name}: {p.File} is not in the game folder.");
            string h = Current(file, p) ?? throw new InvalidDataException($"Can't {verb} {e.Name}: {p.File} is shorter than the patch expects.");
            if (h != p.OldSha1 && h != p.NewSha1)
                throw new InvalidDataException($"Can't {verb} {e.Name}: {p.File} at 0x{p.Offset:X} holds neither the original nor the replacement. The game files changed since this mod was made; nothing was written.");
            // appended ranges come off by cutting the file, so nothing may have been added after this mod's last one
            if (p.Append && !toNew && h == p.NewSha1 && new FileInfo(file).Length != e.Patches.Where(q => q.Append && q.File == p.File && Current(file, q) == q.NewSha1).Max(q => q.Offset + q.Length))
                throw new InvalidDataException($"Can't restore {e.Name}: more was added to {p.File} after its data, so it can't be cut back. Restore that first; nothing was written.");
        }
        int written = 0;
        // restoring goes backwards, so appended ranges are cut off last-added first
        foreach (int i in toNew ? Enumerable.Range(0, e.Patches.Count) : Enumerable.Range(0, e.Patches.Count).Reverse())
        {
            var p = e.Patches[i];
            string file = Path_(game, p);
            string want = toNew ? p.NewSha1 : p.OldSha1;
            if (Current(file, p) == want) continue;
            if (p.Append && !toNew)
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { fs.SetLength(p.Offset); fs.Flush(true); }
                if (new FileInfo(file).Length != p.Offset) throw new IOException($"{p.File} was not cut back to 0x{p.Offset:X}.");
                written++;
                log?.Invoke($"{p.Note ?? "range"}: cut {p.File} back to 0x{p.Offset:X}");
                continue;
            }
            var data = File.ReadAllBytes(toNew ? New(e, i) : Old(e, i));
            if (Hex(SHA1.HashData(data)) != want) throw new InvalidDataException($"The stored {(toNew ? "new" : "old")} bytes of {e.Name} (patch {i}) are damaged.");
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
        log?.Invoke($"{e.Name}: {written} range(s) written, {e.Patches.Count - written} already as wanted");
    }
}
