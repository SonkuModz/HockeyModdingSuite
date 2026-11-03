using System.Security.Cryptography;

namespace NHL27Kit.Core;

/// <summary>
/// A byte range to write: the bytes there now and the bytes to put there. An <paramref name="Append"/> range adds
/// bytes at the end of the file (Offset is its length, Old is empty); restoring it cuts the file back.
/// </summary>
public sealed record PatchData(string File, long Offset, byte[] Old, byte[] New, string Note, bool Append = false);

/// <summary>How many TOC entries (chunk table, bundle and bundle record entries) start at each place in the CAS archives.</summary>
public sealed class LocationIndex
{
    private readonly Dictionary<long, int> _counts = new();
    private readonly Dictionary<long, List<Guid>> _chunks = new();

    public static long Key(TocRef r) => ((r.Patch ? 1L : 0L) << 62) | ((long)(r.Catalog & 0xFF) << 40) | ((long)(r.Cas & 0xFF) << 32) | r.Offset;

    public int Count(TocRef r) => _counts.GetValueOrDefault(Key(r));

    /// <summary>The chunk ids whose chunk-table entries start at the place <paramref name="r"/> points at.</summary>
    public IReadOnlyList<Guid> ChunksAt(TocRef r) => _chunks.TryGetValue(Key(r), out var l) ? l : Array.Empty<Guid>();

    /// <summary>Reads every TOC. Takes about as long as building the asset index from scratch's TOC pass.</summary>
    public static LocationIndex Build(AssetIndex index, TocCache tocs, Action<string> log = null)
    {
        var li = new LocationIndex();
        foreach (string path in index.Layout.AllTocPaths())
        {
            var toc = tocs.Get(path);
            foreach (var c in toc.Chunks)
            {
                li.Add(c.Ref);
                if (!li._chunks.TryGetValue(Key(c.Ref), out var ids)) li._chunks[Key(c.Ref)] = ids = new List<Guid>();
                if (!ids.Contains(c.Id)) ids.Add(c.Id);
            }
            foreach (var b in toc.Bundles)
                foreach (var e in b.Entries) li.Add(e);
            log?.Invoke($"  {Path.GetRelativePath(index.Layout.GameDir, path)}");
        }
        return li;
    }

    private void Add(TocRef r) => _counts[Key(r)] = _counts.GetValueOrDefault(Key(r)) + 1;
}

public sealed class ChunkPlan
{
    public List<PatchData> Patches { get; } = new();
    public List<string> Notes { get; } = new();
    public int Copies { get; set; }
    /// <summary>Other chunks that store the very same bytes and are rewritten with this one (takeShared).</summary>
    public List<Guid> SharedChunks { get; } = new();
}

/// <summary>
/// Turns "this chunk's data is now X" into byte-range patches of the game's archives. NHL 27's chunks are stored
/// uncompressed, so a chunk of the same size is overwritten in place and no TOC changes:
/// - every stored copy is overwritten: the full chunk in the TOC chunk table, and each bundle's copy (usually the start of
///   the same bytes, sometimes its own);
/// - each bundle record holds a SHA1 of the full stored chunk, which is rewritten (NHL 24's notes: a stale one is rejected);
/// - nothing is written when another asset's entry points at the same bytes (identical data stored once), since that asset
///   would change too; with takeShared, the chunks that store those bytes are taken along (their copies and SHA1s are
///   rewritten as well), for callers where the other asset should change too (a team logo and its banner emblem).
/// </summary>
public static class ChunkPatcher
{
    public static ChunkPlan Plan(AssetIndex index, Guid chunkId, byte[] newRaw, LocationIndex locations, TocCache tocs, Action<string> log = null, bool takeShared = false)
    {
        var layout = index.Layout;
        var copies = ChunkCopies.Find(index, chunkId, tocs);
        if (copies.Count == 0) throw new InvalidDataException($"Chunk {chunkId} has no stored copy in any TOC.");
        var shared = new List<Guid>();
        if (takeShared)
        {
            // the chunks whose table entries point at our bytes, and theirs in turn
            var taken = new HashSet<Guid> { chunkId };
            for (bool more = true; more;)
            {
                more = false;
                foreach (var id in copies.SelectMany(c => locations.ChunksAt(c.Ref)).Where(id => !taken.Contains(id)).Distinct().ToList())
                {
                    taken.Add(id);
                    shared.Add(id);
                    copies.AddRange(ChunkCopies.Find(index, id, tocs));
                    more = true;
                }
            }
            // bundle copies of these bytes the index doesn't list (another superbundle's bundle with the same chunk)
            copies.AddRange(ChunkCopies.FindAt(index, tocs, copies.Select(c => LocationIndex.Key(c.Ref)).ToHashSet(), copies));
        }

        // the full chunk: the biggest live copy
        var full = copies.Where(c => c.Live).OrderByDescending(c => c.Ref.Size).FirstOrDefault() ?? copies.OrderByDescending(c => c.Ref.Size).First();
        byte[] oldFull = index.ReadStored(full.Ref);
        byte[] oldRaw = FrostbiteBlocks.Decode(oldFull);
        if (newRaw.Length != oldRaw.Length)
            throw new InvalidDataException($"The new data is {newRaw.Length:N0} bytes but the chunk holds {oldRaw.Length:N0}: only same-size replacements are written.");
        byte[] newFull = Framing.Reframe(oldFull, newRaw);
        byte[] oldSha = SHA1.HashData(oldFull), newSha = SHA1.HashData(newFull);

        var plan = new ChunkPlan { Copies = copies.Count };
        plan.SharedChunks.AddRange(shared);
        if (shared.Count > 0) plan.Notes.Add($"shared: the same bytes are stored for {shared.Count} other chunk(s), which change too");
        // per copy start: the longest copy there (its old and new stored bytes)
        var data = new Dictionary<(string, long), (byte[] Old, byte[] New, string Note)>();
        string Rel(TocRef r) => Path.GetRelativePath(layout.GameDir, layout.CasPath(r.Catalog, r.Cas, r.Patch)).Replace('\\', '/');

        // 1) the data ranges: one per distinct start, the longest copy at that start covers the shorter ones
        foreach (var c in copies)
        {
            byte[] stored;
            try { stored = index.ReadStored(c.Ref); }
            catch (Exception e) { if (c.Live) throw; plan.Notes.Add($"skipped an unreadable old copy ({e.Message})"); continue; }
            if (stored.Length > oldFull.Length || !stored.AsSpan().SequenceEqual(oldFull.AsSpan(0, stored.Length)))
            {
                if (c.Live)
                    throw new InvalidDataException($"The {c.Kind} copy in {Path.GetFileName(c.Toc)}{(c.InBundle ? " / " + c.BundleName : "")} is not the start of the full chunk, so it can't be rewritten from it.");
                plan.Notes.Add($"left an old {c.Kind} copy in {Path.GetFileName(c.Toc)} alone (it no longer matches the chunk)");
                continue;
            }
            var key = (Rel(c.Ref), (long)c.Ref.Offset);
            if (!data.TryGetValue(key, out var have) || have.New.Length < stored.Length)
                data[key] = (stored, newFull.AsSpan(0, stored.Length).ToArray(), c.InBundle ? "bundle copy" : "chunk data");
        }

        // 2) nothing else may use those bytes
        foreach (var c in copies)
        {
            var key = (Rel(c.Ref), (long)c.Ref.Offset);
            if (!data.ContainsKey(key)) continue;
            int ours = copies.Count(o => Rel(o.Ref) == key.Item1 && o.Ref.Offset == c.Ref.Offset);
            int all = locations.Count(c.Ref);
            if (all > ours)
                throw new InvalidDataException($"The data at {key.Item1} 0x{c.Ref.Offset:X} is also used by {all - ours} other asset entr{(all - ours == 1 ? "y" : "ies")} (identical data is stored once), so rewriting it would change them too. This one can't be replaced in place.");
        }

        // 3) the SHA1 in each bundle record
        var shaDone = new HashSet<(string, long)>();
        foreach (var c in copies.Where(c => c.InBundle))
        {
            if (!data.ContainsKey((Rel(c.Ref), (long)c.Ref.Offset)) && !c.Live) continue;
            if (c.Sha1 == null || !c.Sha1.AsSpan().SequenceEqual(oldSha))
            {
                if (c.Live) throw new InvalidDataException($"The record of bundle {c.BundleName} holds a SHA1 that is not the SHA1 of the stored chunk, so its meaning is not what the kit expects. Nothing was written.");
                continue;
            }
            // the record may be shared by an entry that is not ours
            var rec = TocRefOfRecord(index, tocs, c);
            int recOurs = copies.Count(o => o.InBundle && TocRefOfRecord(index, tocs, o).Offset == rec.Offset && Rel(TocRefOfRecord(index, tocs, o)) == Rel(rec));
            if (locations.Count(rec) > 1 && locations.Count(rec) > recOurs && recOurs <= 1)
                plan.Notes.Add($"the record of {c.BundleName} is shared with another bundle entry; its SHA1 is patched for both");
            string file = Rel(rec);
            if (!shaDone.Add((file, c.Sha1Pos))) continue;
            byte[] cur = ReadRange(Path.Combine(layout.GameDir, file.Replace('/', Path.DirectorySeparatorChar)), c.Sha1Pos, 20);
            if (cur == null || !cur.AsSpan().SequenceEqual(oldSha)) throw new InvalidDataException($"The SHA1 at {file} 0x{c.Sha1Pos:X} is not where the index read it. Rebuild the index.");
            plan.Patches.Add(new PatchData(file, c.Sha1Pos, cur, newSha, "record SHA1"));
        }

        // the data: only the spans that differ (spans closer than 4 KB are merged)
        var spans = new List<PatchData>();
        foreach (var ((file, start), (old, neu, note)) in data.OrderBy(d => d.Key.Item1).ThenBy(d => d.Key.Item2))
        {
            int i = 0;
            while (i < old.Length)
            {
                if (old[i] == neu[i]) { i++; continue; }
                int s = i, last = i;
                for (int j = i; j < old.Length && j - last <= 4096; j++)
                    if (old[j] != neu[j]) last = j;
                int e = last + 1;
                spans.Add(new PatchData(file, start + s, old.AsSpan(s, e - s).ToArray(), neu.AsSpan(s, e - s).ToArray(), note));
                i = e;
            }
        }
        if (spans.Count == 0) plan.Notes.Add("the new data is identical to the old");
        plan.Patches.InsertRange(0, spans);
        plan.Notes.Add($"{spans.Count} data span(s) ({spans.Sum(s => (long)s.New.Length):N0} bytes), {plan.Patches.Count - spans.Count} record SHA1(s), {copies.Count} stored copies found");
        return plan;
    }

    private static TocRef TocRefOfRecord(AssetIndex index, TocCache tocs, ChunkCopy c) => c.Record;

    private static byte[] ReadRange(string file, long offset, int length)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (offset + length > fs.Length) return null;
        fs.Position = offset;
        var b = new byte[length];
        fs.ReadExactly(b);
        return b;
    }
}
