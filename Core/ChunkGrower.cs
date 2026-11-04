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
/// file back to its old length. Live copies may be stored in several places: each CAS file gets the new bytes once.
/// A bundle may list only the end of the chunk (NHL 24's streamed textures keep their top mip in the chunk table only):
/// those copies take <c>tail</c>, and their records keep the SHA1 of the whole stored chunk. Other chunks that store the
/// same bytes keep their image.
/// </summary>
public static class ChunkGrower
{
    /// <param name="ends">For planning several before any is written: each CAS file's planned length (relative path ->
    /// bytes), read and updated here. Without it the file's length now is used.</param>
    /// <param name="tail">The new stored bytes of the copies that hold only the chunk's end, and where in the chunk they start.</param>
    public static ChunkPlan Plan(AssetIndex index, Guid chunkId, byte[] newStored, LocationIndex locations, TocCache tocs, IDictionary<string, long> ends = null, (byte[] Stored, uint LogicalOffset)? tail = null)
    {
        var layout = index.Layout;
        var copies = ChunkCopies.Find(index, chunkId, tocs);
        var live = copies.Where(c => c.Live).ToList();
        if (live.Count == 0) throw new InvalidDataException($"Chunk {chunkId} has no live stored copy.");
        // other entries at the same place: copies of this chunk the index doesn't list (another superbundle's bundle)
        // move with it. Other chunks that happen to store the same bytes keep them: the old bytes aren't touched.
        var places = live.Select(c => LocationIndex.Key(c.Ref)).ToHashSet();
        if (live.Any(c => locations.Count(c.Ref) > copies.Count(o => LocationIndex.Key(o.Ref) == LocationIndex.Key(c.Ref))))
            live.AddRange(ChunkCopies.FindAt(index, tocs, places, copies, chunkId));
        foreach (var c in live.Where(c => c.LogicalOffset != 0))
        {
            if (tail == null)
                throw new InvalidDataException("A bundle lists only part of the chunk (logical offset isn't 0); growing that isn't handled.");
            if (c.LogicalOffset != tail.Value.LogicalOffset)
                throw new InvalidDataException($"{c.BundleName} lists the chunk from byte {c.LogicalOffset:N0}, but its end starts at byte {tail.Value.LogicalOffset:N0}: this layout isn't understood.");
        }
        uint RawSize(byte[] stored) => (uint)FrostbiteBlocks.Parse(stored).Sum(b => (long)b.RawSize);
        string Rel(string path) => Path.GetRelativePath(layout.GameDir, path).Replace('\\', '/');
        ends ??= new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var plan = new ChunkPlan { Copies = copies.Count };
        // the new bytes, once per CAS file and per kind (whole chunk, or its end)
        var added = new Dictionary<(string File, bool Tail), uint>();
        uint AddedAt(string cas, bool isTail, byte[] stored)
        {
            string rel = Rel(cas);
            if (added.TryGetValue((rel, isTail), out uint at)) return at;
            long length = ends.TryGetValue(rel, out long planned) ? planned : new FileInfo(cas).Length;
            long offset = (length + 15) & ~15L;
            if (offset + stored.Length > uint.MaxValue) throw new InvalidDataException($"{rel} would pass 4 GB, past what a TOC offset holds.");
            var data = new byte[offset - length + stored.Length];
            stored.CopyTo(data, offset - length);
            plan.Patches.Add(new PatchData(rel, length, Array.Empty<byte>(), data, "appended chunk", Append: true));
            ends[rel] = length + data.Length;
            plan.Notes.Add($"{stored.Length:N0} stored bytes added to {rel} at 0x{offset:X} (was {length:N0} bytes long)");
            return added[(rel, isTail)] = (uint)offset;
        }

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
        var oldShas = new Dictionary<long, byte[]>();
        byte[] OldSha(TocRef at)
        {
            long place = LocationIndex.Key(at);
            if (!oldShas.TryGetValue(place, out var sha)) oldShas[place] = sha = SHA1.HashData(index.ReadStored(at));
            return sha;
        }
        // a record's SHA1 is of the whole stored chunk, also when its bundle holds only the end
        var whole = live.FirstOrDefault(c => c.LogicalOffset == 0);
        foreach (var c in live)
        {
            bool isTail = c.LogicalOffset != 0;
            byte[] stored = isTail ? tail.Value.Stored : newStored;
            uint offset = AddedAt(layout.CasPath(c.Ref.Catalog, c.Ref.Cas, c.Ref.Patch), isTail, stored);
            U32(c.Toc, c.Ref.OffsetPos, offset, true, $"toc {c.Kind} offset");
            U32(c.Toc, c.Ref.SizePos, (uint)stored.Length, true, $"toc {c.Kind} size");
            if (!c.InBundle) continue;
            // the record: the chunk's logical size and the SHA1 of its stored bytes
            var r0 = c.Record;
            var rec = BundleRecord.Read(index.ReadStored(r0), r0.Offset);
            var item = rec.Chunks.First(x => x.Id == chunkId);
            string recFile = layout.CasPath(r0.Catalog, r0.Cas, r0.Patch);
            if (isTail && whole == null) throw new InvalidDataException($"{c.BundleName} holds only the end of a chunk nothing holds whole.");
            byte[] oldSha = OldSha(isTail ? whole.Ref : c.Ref);
            if (!item.Sha1.AsSpan().SequenceEqual(oldSha))
                throw new InvalidDataException($"The record of {c.BundleName} holds a SHA1 that isn't the stored chunk's; nothing was planned.");
            U32(recFile, item.SizePos, RawSize(stored), false, "record logical size");
            if (done.Add((Rel(recFile), item.Sha1Pos)))
                plan.Patches.Add(new PatchData(Rel(recFile), item.Sha1Pos, item.Sha1, SHA1.HashData(newStored), "record SHA1"));
        }
        plan.Notes.Add($"{live.Count} live entr{(live.Count == 1 ? "y" : "ies")} pointed at the new data");
        return plan;
    }
}
