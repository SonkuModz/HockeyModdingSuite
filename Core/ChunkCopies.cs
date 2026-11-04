using System.Security.Cryptography;

namespace NHL27Kit.Core;

/// <summary>
/// One stored copy of a chunk: its place in a TOC chunk table, or in a bundle. The same chunk id is often stored several
/// times (a chunk table entry holding every mip, each bundle that loads the texture holding its own copy).
/// </summary>
public sealed record ChunkCopy(string Kind, string Toc, TocRef Ref, int BundleId, string BundleName, long Sha1Pos, byte[] Sha1,
                               uint LogicalOffset, uint LogicalSize, bool Live, TocRef Record = default)
{
    public bool InBundle => BundleId >= 0;
}

/// <summary>Parsed superbundle TOCs, kept for the lifetime of one planning run.</summary>
public sealed class TocCache
{
    private readonly AssetIndex _index;
    private readonly Dictionary<string, SuperBundleToc> _tocs = new(StringComparer.OrdinalIgnoreCase);

    public TocCache(AssetIndex index) => _index = index;

    public SuperBundleToc Get(string path)
    {
        if (!_tocs.TryGetValue(path, out var toc))
            _tocs[path] = toc = new SuperBundleToc(path, File.ReadAllBytes(path), _index.Layout.TocCatalogs);
        return toc;
    }
}

public static class ChunkCopies
{
    /// <summary>A TOC's path below Data or Patch, which names the superbundle it belongs to.</summary>
    public static string SuperbundleKey(AssetIndex index, string toc)
    {
        string rel = Path.GetRelativePath(index.Layout.GameDir, toc).Replace('\\', '/');
        int slash = rel.IndexOf('/');
        return slash < 0 ? rel : rel[(slash + 1)..];
    }

    /// <summary>
    /// Every stored copy of a chunk. A copy in a Patch TOC or bundle replaces the same one in Data, so only the last copy
    /// per superbundle (and per bundle name) is <see cref="ChunkCopy.Live"/>; the rest are still listed.
    /// </summary>
    public static List<ChunkCopy> Find(AssetIndex index, Guid id, TocCache tocs)
    {
        var copies = new List<ChunkCopy>();

        // chunk table entries, one per TOC that has the chunk
        if (index.TableChunks.TryGetValue(id, out var table))
        {
            var lastBySb = table.GroupBy(t => SuperbundleKey(index, t.Toc)).ToDictionary(g => g.Key, g => g.Last());
            foreach (var t in table)
                copies.Add(new ChunkCopy("table", t.Toc, t.Ref, -1, null, -1, null, 0, t.Ref.Size, ReferenceEquals(lastBySb[SuperbundleKey(index, t.Toc)], t)));
        }

        // bundle entries: read each listing bundle's record to find the chunk's position in it
        if (index.GetChunk(id) is { InChunkTable: false } listed)
        {
            var lastByName = listed.Bundles.GroupBy(b => (SuperbundleKey(index, index.BundleList[b].Toc), index.BundleList[b].Name)).ToDictionary(g => g.Key, g => g.Max());
            foreach (int b in listed.Bundles)
            {
                var info = index.BundleList[b];
                var toc = tocs.Get(info.Toc);
                var tb = toc.Bundles[info.BundleIndex];
                var rec = BundleRecord.Read(index.ReadStored(tb.Entries[0].Catalog, tb.Entries[0].Cas, tb.Entries[0].Patch, tb.Entries[0].Offset, tb.Entries[0].Size), tb.Entries[0].Offset);
                int at = rec.Chunks.FindIndex(c => c.Id == id);
                if (at < 0) continue;
                var item = rec.Chunks[at];
                var entry = tb.Entries[1 + rec.Ebx.Count + rec.Res.Count + at];
                bool live = lastByName[(SuperbundleKey(index, info.Toc), info.Name)] == b;
                copies.Add(new ChunkCopy("bundle", info.Toc, entry, b, info.Name, item.Sha1Pos, item.Sha1, item.LogicalOffset, item.Size, live, tb.Entries[0]));
            }
        }
        return copies;
    }

    /// <summary>
    /// Bundle copies found by place rather than through the index: every bundle entry in the TOCs that starts at one of
    /// <paramref name="places"/> (LocationIndex keys) and is a chunk of its bundle. The index lists a chunk once, so a
    /// bundle in another superbundle that carries the same chunk can be missing from <see cref="Find"/>. Copies already
    /// in <paramref name="known"/> are skipped; the ones found count as live (they are checked like any live copy).
    /// </summary>
    /// <param name="only">Only copies of this chunk.</param>
    public static List<ChunkCopy> FindAt(AssetIndex index, TocCache tocs, IReadOnlySet<long> places, IEnumerable<ChunkCopy> known, Guid? only = null)
    {
        var have = known.Where(c => c.InBundle).Select(c => (c.Toc, c.BundleName, LocationIndex.Key(c.Ref))).ToHashSet();
        var found = new List<ChunkCopy>();
        foreach (string path in index.Layout.AllTocPaths())
        {
            var toc = tocs.Get(path);
            foreach (var tb in toc.Bundles)
                for (int i = 1; i < tb.Entries.Count; i++)
                {
                    long key = LocationIndex.Key(tb.Entries[i]);
                    if (!places.Contains(key) || have.Contains((path, tb.Name, key))) continue;
                    var r0 = tb.Entries[0];
                    var rec = BundleRecord.Read(index.ReadStored(r0.Catalog, r0.Cas, r0.Patch, r0.Offset, r0.Size), r0.Offset);
                    int at = i - 1 - rec.Ebx.Count - rec.Res.Count;
                    if (at < 0 || at >= rec.Chunks.Count) continue;
                    var item = rec.Chunks[at];
                    if (only != null && item.Id != only) continue;
                    found.Add(new ChunkCopy("bundle", path, tb.Entries[i], int.MaxValue, tb.Name, item.Sha1Pos, item.Sha1, item.LogicalOffset, item.Size, true, r0));
                    have.Add((path, tb.Name, key));
                }
        }
        return found;
    }

    /// <summary>The bytes a bundle record's SHA1 for a stored chunk covers, as the game's own files show it: the stored (framed) bytes.</summary>
    public static byte[] Sha1Of(ReadOnlySpan<byte> stored) => SHA1.HashData(stored);
}
