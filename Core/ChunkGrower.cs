using System.Buffers.Binary;
using System.Security.Cryptography;

namespace NHL27Kit.Core;

/// <summary>
/// Gives a chunk new data of another size, which <see cref="ChunkPatcher"/> can't (it writes in place). The new stored
/// bytes are added at the end of the CAS file that holds the chunk now, and everything that locates the chunk is
/// pointed at them:
/// - each TOC entry of the chunk (its chunk-table entry and each bundle's entry): big-endian offset and size;
/// - each bundle record that lists it: the chunk's logical size and the SHA1 of its stored bytes.
/// The old bytes stay where they were, unused. Restoring the mod puts the TOC and record values back and cuts the CAS
/// file back to its old length. Every live copy must be the same stored bytes at one place used by nothing else.
/// </summary>
public static class ChunkGrower
{
    /// <param name="ends">For planning several before any is written: each CAS file's planned length (relative path ->
    /// bytes), read and updated here. Without it the file's length now is used.</param>
    public static ChunkPlan Plan(AssetIndex index, Guid chunkId, byte[] newStored, LocationIndex locations, TocCache tocs, IDictionary<string, long> ends = null)
    {
        var layout = index.Layout;
        var copies = ChunkCopies.Find(index, chunkId, tocs);
        var live = copies.Where(c => c.Live).ToList();
        if (live.Count == 0) throw new InvalidDataException($"Chunk {chunkId} has no live stored copy.");
        var at = live[0].Ref;
        if (live.Any(c => c.Ref.Catalog != at.Catalog || c.Ref.Cas != at.Cas || c.Ref.Patch != at.Patch || c.Ref.Offset != at.Offset))
            throw new InvalidDataException("The chunk's live copies are stored in more than one place; growing that isn't handled.");
        int ours = copies.Count(c => LocationIndex.Key(c.Ref) == LocationIndex.Key(at));
        if (locations.Count(at) > ours)
            throw new InvalidDataException($"The chunk's bytes are also used by {locations.Count(at) - ours} other entr{(locations.Count(at) - ours == 1 ? "y" : "ies")}; growing it would leave them pointing at old data.");
        if (live.Any(c => c.LogicalOffset != 0))
            throw new InvalidDataException("A bundle lists only part of the chunk (logical offset isn't 0); growing that isn't handled.");
        uint newRawSize = (uint)FrostbiteBlocks.Parse(newStored).Sum(b => (long)b.RawSize);

        string cas = layout.CasPath(at.Catalog, at.Cas, at.Patch);
        string Rel(string path) => Path.GetRelativePath(layout.GameDir, path).Replace('\\', '/');
        long length = ends != null && ends.TryGetValue(Rel(cas), out long planned) ? planned : new FileInfo(cas).Length;
        long offset = (length + 15) & ~15L;
        if (offset + newStored.Length > uint.MaxValue) throw new InvalidDataException($"{Rel(cas)} would pass 4 GB, past what a TOC offset holds.");

        var plan = new ChunkPlan { Copies = copies.Count };
        var data = new byte[offset - length + newStored.Length];
        newStored.CopyTo(data, offset - length);
        plan.Patches.Add(new PatchData(Rel(cas), length, Array.Empty<byte>(), data, "appended chunk", Append: true));
        if (ends != null) ends[Rel(cas)] = length + data.Length;

        // the TOC entries: offset and size, big-endian
        var done = new HashSet<(string, long)>();
        void U32(string file, long pos, uint value, bool bigEndian, string note)
        {
            if (!done.Add((Rel(file), pos))) return;
            var old = new byte[4];
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { fs.Position = pos; fs.ReadExactly(old); }
            var neu = new byte[4];
            if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(neu, value); else BinaryPrimitives.WriteUInt32LittleEndian(neu, value);
            plan.Patches.Add(new PatchData(Rel(file), pos, old, neu, note));
        }
        byte[] oldSha = null;
        foreach (var c in live)
        {
            U32(c.Toc, c.Ref.OffsetPos, (uint)offset, true, $"toc {c.Kind} offset");
            U32(c.Toc, c.Ref.SizePos, (uint)newStored.Length, true, $"toc {c.Kind} size");
            if (!c.InBundle) continue;
            // the record: the chunk's logical size and the SHA1 of its stored bytes
            var r0 = c.Record;
            var rec = BundleRecord.Read(index.ReadStored(r0), r0.Offset);
            var item = rec.Chunks.First(x => x.Id == chunkId);
            string recFile = layout.CasPath(r0.Catalog, r0.Cas, r0.Patch);
            oldSha ??= SHA1.HashData(index.ReadStored(at));
            if (!item.Sha1.AsSpan().SequenceEqual(oldSha))
                throw new InvalidDataException($"The record of {c.BundleName} holds a SHA1 that isn't the stored chunk's; nothing was planned.");
            U32(recFile, item.SizePos, newRawSize, false, "record logical size");
            if (done.Add((Rel(recFile), item.Sha1Pos)))
                plan.Patches.Add(new PatchData(Rel(recFile), item.Sha1Pos, item.Sha1, SHA1.HashData(newStored), "record SHA1"));
        }
        plan.Notes.Add($"{newStored.Length:N0} stored bytes added to {Rel(cas)} at 0x{offset:X} (was {length:N0} bytes long); {live.Count} live entr{(live.Count == 1 ? "y" : "ies")} pointed there");
        return plan;
    }
}
