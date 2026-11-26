using System.Buffers.Binary;
using System.Security.Cryptography;

namespace NHL27Kit.Core;

/// <summary>
/// Gives a RES new contents of another size (NHL 24): each stored copy of it (bundles can list different copies, such as
/// the base game's and the patch's) is changed on its own by a function of its old bytes and RES meta, stored
/// uncompressed at the end of its CAS file, and every bundle listing that copy is repointed: its TOC entry's offset and
/// size, and in its record the RES's size, meta and SHA1.
/// </summary>
public static class ResGrower
{
    public sealed class Result
    {
        public List<PatchData> Patches { get; } = new();
        public List<string> Notes { get; } = new();
        /// <summary>Each stored copy: its place, old and new raw bytes and meta, and the bundles listing it.</summary>
        public List<(string Place, byte[] OldRaw, byte[] NewRaw, byte[] NewMeta, int Bundles)> Copies { get; } = new();
    }

    /// <param name="change">(old raw bytes, old 16-byte meta) -> (new raw bytes, new meta).</param>
    /// <param name="grow">Each CAS file's planned length (relative path -> length), shared with other appends in the same change.</param>
    public static Result Plan(AssetIndex index, string name, Func<byte[], byte[], (byte[] Raw, byte[] Meta)> change, IDictionary<string, long> grow) =>
        Plan(index, name, false, change, grow);

    /// <summary>The same for an EBX (no meta): every stored copy becomes <paramref name="change"/>(old raw bytes).</summary>
    public static Result PlanEbx(AssetIndex index, string name, Func<byte[], byte[]> change, IDictionary<string, long> grow) =>
        Plan(index, name, true, (raw, _) => (change(raw), null), grow);

    private static Result Plan(AssetIndex index, string name, bool isEbx, Func<byte[], byte[], (byte[] Raw, byte[] Meta)> change, IDictionary<string, long> grow)
    {
        var layout = index.Layout;
        AssetEntry res = isEbx ? index.GetEbx(name) ?? throw new InvalidDataException($"No EBX named {name}.")
            : index.GetRes(name) ?? throw new InvalidDataException($"No RES named {name}.");
        string Rel(string p) => Path.GetRelativePath(layout.GameDir, p).Replace('\\', '/');
        string CasRel(TocRef r) => Rel(layout.CasPath(r.Catalog, r.Cas, r.Patch));
        var tocs = new Dictionary<string, (byte[] Bytes, SuperBundleToc Toc)>(StringComparer.OrdinalIgnoreCase);
        (byte[] Bytes, SuperBundleToc Toc) Toc(string p)
        {
            if (!tocs.TryGetValue(p, out var t)) { var b = File.ReadAllBytes(p); tocs[p] = t = (b, new SuperBundleToc(p, b, layout.TocCatalogs)); }
            return t;
        }
        var r = new Result();
        // each listing bundle: its TOC, entry, record and where the RES's fields are in it
        var listings = new List<(BundleInfo Info, TocRef Entry, TocRef Head, BundleRecord.Item Item, long MetaPos)>();
        foreach (int b in res.Bundles.Distinct())
        {
            var info = index.BundleList[b];
            var tb = Toc(info.Toc).Toc.Bundles[info.BundleIndex];
            var head = tb.Entries[0];
            var recBytes = index.ReadStored(head);
            var rec = BundleRecord.Read(recBytes, head.Offset);
            var items = isEbx ? rec.Ebx : rec.Res;
            int at = items.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            int U(int p) => BinaryPrimitives.ReadInt32LittleEndian(recBytes.AsSpan(p));
            int total = U(8), ebx = U(12), nres = U(16);
            if (isEbx) { listings.Add((info, tb.Entries[1 + at], head, items[at], -1)); continue; }
            long metaPos = head.Offset + 36 + 20L * total + 8L * ebx + 8L * nres + 4L * nres + 16L * at;
            if (!recBytes.AsSpan((int)(metaPos - head.Offset), 16).SequenceEqual(rec.Res[at].ResMeta)) throw new InvalidDataException($"{info.Name}: the RES meta isn't where expected.");
            listings.Add((info, tb.Entries[1 + ebx + at], head, rec.Res[at], metaPos));
        }
        if (listings.Count == 0) throw new InvalidDataException($"No bundle lists {name}.");

        foreach (var place in listings.GroupBy(l => (l.Entry.Catalog, l.Entry.Cas, l.Entry.Patch, l.Entry.Offset, l.Entry.Size)))
        {
            var first = place.First();
            var oldStored = index.ReadStored(first.Entry);
            var oldRaw = FrostbiteBlocks.Decode(oldStored);
            var metas = place.Select(l => Convert.ToHexString(l.Item.ResMeta ?? Array.Empty<byte>())).Distinct().ToList();
            if (metas.Count > 1) throw new InvalidDataException($"{name} at {CasRel(first.Entry)} 0x{first.Entry.Offset:X}: its listing records disagree on its meta.");
            if (place.Any(l => l.Item.Size != oldRaw.Length)) throw new InvalidDataException($"{name} at {CasRel(first.Entry)} 0x{first.Entry.Offset:X}: a record's size isn't the stored copy's.");
            var (newRaw, newMeta) = change(oldRaw, first.Item.ResMeta);
            if (!isEbx && newMeta.Length != 16) throw new ArgumentException("A RES meta is 16 bytes.");
            var newStored = FrostbiteBlocks.Store(newRaw);
            string cas = CasRel(first.Entry);
            long length = grow.TryGetValue(cas, out long planned) ? planned : new FileInfo(layout.CasPath(first.Entry.Catalog, first.Entry.Cas, first.Entry.Patch)).Length;
            long offset = (length + 15) & ~15L;
            if (offset + newStored.Length > uint.MaxValue) throw new InvalidDataException($"{cas} would pass 4 GB.");
            var data = new byte[offset - length + newStored.Length];
            newStored.CopyTo(data, offset - length);
            r.Patches.Add(new PatchData(cas, length, Array.Empty<byte>(), data, $"appended {Path.GetFileName(name)}", Append: true));
            grow[cas] = length + data.Length;
            var sha = SHA1.HashData(newStored);
            foreach (var l in place)
            {
                var (tocBytes, _) = Toc(l.Info.Toc);
                string tocRel = Rel(l.Info.Toc);
                void Be(int pos, uint v, string note)
                {
                    var neu = new byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(neu, v);
                    r.Patches.Add(new PatchData(tocRel, pos, tocBytes[pos..(pos + 4)], neu, note));
                }
                Be(l.Entry.OffsetPos, (uint)offset, isEbx ? "toc ebx offset" : "toc res offset");
                Be(l.Entry.SizePos, (uint)newStored.Length, isEbx ? "toc ebx size" : "toc res size");
                string recRel = CasRel(l.Head);
                var size = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)newRaw.Length);
                var oldSize = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(oldSize, l.Item.Size);
                r.Patches.Add(new PatchData(recRel, l.Item.SizePos, oldSize, size, isEbx ? "record ebx size" : "record res size"));
                if (!isEbx) r.Patches.Add(new PatchData(recRel, l.MetaPos, l.Item.ResMeta, newMeta, "record res meta"));
                if (l.Item.Sha1 != null && l.Item.Sha1.AsSpan().SequenceEqual(SHA1.HashData(oldStored)))
                    r.Patches.Add(new PatchData(recRel, l.Item.Sha1Pos, l.Item.Sha1, sha, "record SHA1"));
                else r.Notes.Add($"the record of {l.Info.Name} holds no SHA1 of {name}'s stored bytes; left alone");
            }
            r.Copies.Add(($"{cas} 0x{first.Entry.Offset:X}", oldRaw, newRaw, newMeta, place.Count()));
            r.Notes.Add($"{name} at {cas} 0x{first.Entry.Offset:X} ({oldRaw.Length:N0} bytes, {place.Count()} bundles): {newRaw.Length:N0} bytes at 0x{offset:X}");
        }
        return r;
    }
}
