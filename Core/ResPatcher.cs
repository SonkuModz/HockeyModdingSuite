using System.Security.Cryptography;

namespace NHL27Kit.Core;

/// <summary>
/// Turns "this RES now holds X" into byte-range patches, like <see cref="ChunkPatcher"/> does for chunks. A RES is a bundle
/// entry; every bundle that lists it is read to find its copy and the record SHA1 of that copy. Each copy keeps its stored
/// size: uncompressed blocks are rewritten in place, compressed (Kraken) blocks are encoded again and padded to their old
/// compressed sizes (it fails when one doesn't fit). Nothing is written when another entry shares the stored bytes.
/// Used for small header changes such as a mesh's bounding box; <see cref="PlanEbx"/> does the same for an EBX (a kit
/// stamp's hotspot XML, rewritten at the same length).
/// </summary>
public static class ResPatcher
{
    public static ChunkPlan Plan(AssetIndex index, string name, byte[] newRaw, LocationIndex locations, TocCache tocs) =>
        Plan(index, index.GetRes(name) ?? throw new InvalidDataException($"No RES named {name}."), false, newRaw, locations, tocs);

    public static ChunkPlan PlanEbx(AssetIndex index, string name, byte[] newRaw, LocationIndex locations, TocCache tocs) =>
        Plan(index, index.GetEbx(name) ?? throw new InvalidDataException($"No EBX named {name}."), true, newRaw, locations, tocs);

    private static ChunkPlan Plan(AssetIndex index, AssetEntry res, bool ebx, byte[] newRaw, LocationIndex locations, TocCache tocs)
    {
        string name = res.Name;
        // a bundle lists its EBX first, then its RES: entry 0 is the record, then one entry per asset in that order
        List<BundleRecord.Item> Items(BundleRecord r) => ebx ? r.Ebx : r.Res;
        int Entry(BundleRecord r, int at) => 1 + (ebx ? 0 : r.Ebx.Count) + at;
        var layout = index.Layout;
        string Rel(TocRef r) => Path.GetRelativePath(layout.GameDir, layout.CasPath(r.Catalog, r.Cas, r.Patch)).Replace('\\', '/');
        var plan = new ChunkPlan();
        var done = new HashSet<(string, long)>();
        // where each listing bundle keeps this RES: the same stored bytes listed by several bundles are all ours
        var ours = new Dictionary<(string, long), int>();
        foreach (int b in res.Bundles.Distinct())
        {
            var info = index.BundleList[b];
            var tb = tocs.Get(info.Toc).Bundles[info.BundleIndex];
            var head = tb.Entries[0];
            var rec = BundleRecord.Read(index.ReadStored(head.Catalog, head.Cas, head.Patch, head.Offset, head.Size), head.Offset);
            int at = Items(rec).FindIndex(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            var e = tb.Entries[Entry(rec, at)];
            var k = (Rel(e), (long)e.Offset);
            ours[k] = ours.GetValueOrDefault(k) + 1;
        }
        foreach (int b in res.Bundles.Distinct())
        {
            var info = index.BundleList[b];
            var tb = tocs.Get(info.Toc).Bundles[info.BundleIndex];
            var head = tb.Entries[0];
            var rec = BundleRecord.Read(index.ReadStored(head.Catalog, head.Cas, head.Patch, head.Offset, head.Size), head.Offset);
            int at = Items(rec).FindIndex(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            var item = Items(rec)[at];
            var entry = tb.Entries[Entry(rec, at)];
            var key = (Rel(entry), (long)entry.Offset);
            bool first = done.Add(key);

            byte[] oldStored = index.ReadStored(entry);
            byte[] oldRaw = FrostbiteBlocks.Decode(oldStored);
            if (oldRaw.Length != newRaw.Length) throw new InvalidDataException($"The new {name} is {newRaw.Length} bytes, the stored one {oldRaw.Length}.");
            if (oldRaw.AsSpan().SequenceEqual(newRaw)) continue;
            byte[] newStored;
            if (FrostbiteBlocks.IsStoredOnly(oldStored)) newStored = Framing.Reframe(oldStored, newRaw);
            else
            {
                newStored = FrostbiteBlocks.Encode(newRaw, FrostbiteBlocks.Parse(oldStored), 4, out string failure)
                    ?? throw new InvalidDataException($"The new {name} doesn't compress into its old space ({failure}).");
                if (newStored.Length != oldStored.Length) throw new InvalidDataException($"The new {name} is stored in {newStored.Length} bytes, the old one in {oldStored.Length}.");
            }
            if (locations.Count(entry) > ours.GetValueOrDefault(key))
                throw new InvalidDataException($"The stored {name} at {key.Item1} 0x{entry.Offset:X} is shared with another entry, so it can't be changed in place.");
            // the stored bytes once per place; each listing bundle's record SHA1 of them
            if (first) plan.Patches.Add(new PatchData(key.Item1, entry.Offset, oldStored, newStored, ebx ? "ebx data" : "res data"));
            if (item.Sha1 != null && item.Sha1.AsSpan().SequenceEqual(SHA1.HashData(oldStored)))
            {
                if (!plan.Patches.Any(x => x.File == Rel(head) && x.Offset == item.Sha1Pos))
                    plan.Patches.Add(new PatchData(Rel(head), item.Sha1Pos, item.Sha1, SHA1.HashData(newStored), "record SHA1"));
            }
            else plan.Notes.Add($"the record of {info.Name} holds no SHA1 of {name}'s stored bytes; left alone");
        }
        return plan;
    }
}
