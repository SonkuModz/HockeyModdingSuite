using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// Writes a whole legacy file (a UI SWF, a layout, a lua script) into the game files through the mod list. One mod entry
/// ("legacy", the file's path) per file; saving again replaces it. Chunks keep their size, so:
/// - a file the same size or smaller is written where it is, and its index entry gets the new size;
/// - a bigger one is written over a debug-only file of the same chunk (nhl/ui/features/debug/…, never loaded by the
///   retail game) that is big enough, and its index entry is pointed there. The debug file's entry is left as it is.
/// Restoring the entry puts both chunks back. The entry's source is a copy of the written file (mods/sources), so a mod pack
/// can carry it.
/// </summary>
public static class LegacyEditor
{
    public const string Kind = "legacy";

    /// <summary>The collector that has <paramref name="name"/> (the main one first), or null.</summary>
    public static string CollectorOf(AssetIndex index, string name)
    {
        foreach (var c in Legacy.Collectors(index).OrderBy(c => c == Legacy.Main ? 0 : 1))
        {
            try { if (new Legacy(index, c).Find(name) != null) return c; }
            catch (InvalidDataException) { }
        }
        return null;
    }

    public static ModEntry Save(AssetIndex index, ModStore store, string game, string collector, string name, byte[] data,
        string summary, Action<string> log = null)
    {
        // the original layout, under any earlier save of this file
        var existing = store.Find(Kind, name);
        if (existing != null && store.State(existing, game) is ModState.Applied or ModState.Partial)
            store.Restore(existing, game, log);

        List<PatchData> patches;
        var notes = new List<string>();
        try
        {
            var legacy = new Legacy(index, collector);
            var f = legacy.Find(name) ?? throw new FileNotFoundException($"No legacy file {name} in {collector}.");
            if (legacy.Ps4) patches = PlanPs4(index, legacy, f, data, notes, log);
            else {
            var idx = legacy.ReadChunk(legacy.IndexChunk);
            var (start, end) = Legacy.StoredRange(f.Offset, f.Size);
            if (I(idx, f.Entry + 8) != start || I(idx, f.Entry + 12) != end)
                throw new InvalidDataException($"{name}'s index entry doesn't use 256 KB stored blocks; not writing it.");

            int offset = f.Offset;
            if (data.Length > f.Size)
            {
                var spare = Spare(legacy, f, data.Length)
                    ?? throw new InvalidDataException($"{name} grew by {data.Length - f.Size:N0} bytes and its chunk has no free debug file of {data.Length:N0} bytes to move it into.");
                offset = spare.Offset;
                notes.Add($"moved into the space of {spare.Name} (debug only, {spare.Size:N0} bytes)");
            }
            var raw = legacy.ReadChunk(f.ChunkId);
            data.CopyTo(raw, offset);

            (start, end) = Legacy.StoredRange(offset, data.Length);
            BinaryPrimitives.WriteInt32LittleEndian(idx.AsSpan(f.Entry + 8), start);
            BinaryPrimitives.WriteInt32LittleEndian(idx.AsSpan(f.Entry + 12), end);
            BinaryPrimitives.WriteInt32LittleEndian(idx.AsSpan(f.Entry + 16), offset);
            BinaryPrimitives.WriteInt32LittleEndian(idx.AsSpan(f.Entry + 20), data.Length);

            var tocs = new TocCache(index);
            var locations = LocationIndex.Build(index, tocs);
            var dataPlan = ChunkPatcher.Plan(index, f.ChunkId, raw, locations, tocs, log);
            patches = new List<PatchData>(dataPlan.Patches);
            notes.AddRange(dataPlan.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
            if (offset != f.Offset || data.Length != f.Size)
            {
                var indexPlan = ChunkPatcher.Plan(index, legacy.IndexChunk, idx, locations, tocs, log);
                patches.AddRange(indexPlan.Patches);
                notes.AddRange(indexPlan.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
            }
            }
            if (patches.Count == 0) throw new InvalidDataException($"{name}: nothing could be written (see the notes).");
        }
        catch
        {
            if (existing != null) store.Apply(existing, game, log);
            throw;
        }
        string sources = Path.Combine(store.Dir, "sources");
        Directory.CreateDirectory(sources);
        string copy = Path.Combine(sources, $"legacy_{Legacy.Fnv1(name):x16}{Path.GetExtension(name)}");
        File.WriteAllBytes(copy, data);
        var entry = store.Add(Kind, name, copy, summary, patches, notes);
        store.Apply(entry, game, log);
        return entry;
    }

    /// <summary>
    /// NHL 24: its data chunks are Kraken-compressed and an index entry's stored range is compressed bytes, so the file
    /// can't be written in place. The whole data chunk is rebuilt (the file where it was, or at the end of the chunk when
    /// it grew), stored uncompressed in 256 KB blocks, and added at the end of its CAS file (<see cref="ChunkGrower"/>; the
    /// game doesn't check NHL 24's TOC signatures). Every entry of that chunk gets its stored range in the new blocks, and
    /// the index chunk is stored the same way. Restore cuts both back off.
    /// </summary>
    public static List<PatchData> PlanPs4(AssetIndex index, Legacy legacy, LegacyFile f, byte[] data, List<string> notes, Action<string> log) =>
        PlanPs4(index, legacy, new[] { (f, data) }, notes, log);

    /// <summary>Several files of one collector at once (one mod entry; their chunks and the index chunk are added once).</summary>
    public static List<PatchData> PlanPs4(AssetIndex index, Legacy legacy, IReadOnlyList<(LegacyFile File, byte[] Data)> files, List<string> notes, Action<string> log)
    {
        var idx = legacy.ReadChunk(legacy.IndexChunk);
        long L(int p) => BinaryPrimitives.ReadInt64LittleEndian(idx.AsSpan(p));
        void W(int p, long v) => BinaryPrimitives.WriteInt64LittleEndian(idx.AsSpan(p), v);
        var tocs = new TocCache(index);
        var locations = LocationIndex.Build(index, tocs);
        var ends = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var patches = new List<PatchData>();
        foreach (var group in files.GroupBy(x => x.File.ChunkId))
        {
            var raw = legacy.ReadChunk(group.Key);
            // where each edited file goes: in place, or at the end of the chunk when it grew
            var placed = new Dictionary<int, (long Offset, long Size)>();
            foreach (var (f, data) in group)
            {
                int offset = f.Offset;
                if (data.Length > f.Size)
                {
                    offset = (raw.Length + 15) & ~15;
                    Array.Resize(ref raw, offset + data.Length);
                    notes.Add($"{f.Name} grew by {data.Length - f.Size:N0} bytes: moved to the end of its data chunk");
                }
                else if (data.Length < f.Size) Array.Clear(raw, offset + data.Length, f.Size - data.Length);
                data.CopyTo(raw, offset);
                placed[f.Entry] = (offset, data.Length);
            }
            // every entry in this data chunk: its range of the new stored blocks
            int moved = 0;
            foreach (var x in legacy.Files().Where(x => x.ChunkId == group.Key).GroupBy(x => x.Entry).Select(g => g.First()))
            {
                var (o, n) = placed.TryGetValue(x.Entry, out var at) ? at : (L(x.Entry + 24), L(x.Entry + 32));
                if (o < 0 || o + n > raw.Length) throw new InvalidDataException($"{x.Name}'s index entry points past its data chunk; not writing.");
                var (start, end) = Legacy.StoredRange((int)o, (int)n);
                W(x.Entry + 8, start);
                W(x.Entry + 16, end);
                W(x.Entry + 24, o);
                W(x.Entry + 32, n);
                moved++;
            }
            notes.Add($"data chunk stored uncompressed ({raw.Length:N0} bytes) at the end of its CAS file; {moved} index entr{(moved == 1 ? "y" : "ies")} updated");
            patches.AddRange(ChunkGrower.Plan(index, group.Key, FrostbiteBlocks.Store(raw), locations, tocs, ends).Patches);
        }
        patches.AddRange(ChunkGrower.Plan(index, legacy.IndexChunk, FrostbiteBlocks.Store(idx), locations, tocs, ends).Patches);
        return patches;
    }

    /// <summary>The smallest debug-only file of <paramref name="f"/>'s chunk that holds <paramref name="size"/> bytes and that no other file shares.</summary>
    public static LegacyFile Spare(Legacy legacy, LegacyFile f, int size)
    {
        var same = legacy.Files().Where(x => x.ChunkId == f.ChunkId).ToList();
        bool Shared(LegacyFile c) => same.Any(x => x.Entry != c.Entry && x.Offset < c.Offset + c.Size && c.Offset < x.Offset + x.Size);
        return same.Where(c => c.Size >= size && c.Name.Contains("/debug/", StringComparison.OrdinalIgnoreCase) && !Shared(c))
                   .OrderBy(c => c.Size).FirstOrDefault();
    }

    private static int I(byte[] b, int p) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
}
