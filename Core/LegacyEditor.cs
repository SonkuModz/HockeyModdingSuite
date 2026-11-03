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
        if (!index.Layout.IsPs5)
            throw new NotSupportedException("Saving NHL 24's legacy files (its databases) isn't ready yet: its index entries and compressed data chunks differ from NHL 27's.");
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
