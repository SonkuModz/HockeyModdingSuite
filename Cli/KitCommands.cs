using System.Buffers.Binary;
using System.Security.Cryptography;
using NHL27Kit.Core;

namespace NHL27Kit.Cli;

public static partial class Program
{
    /// <summary>
    /// kit-clone-test [TEAM]: NHL 24. Copies TEAM's (boston) Center Ice Mod kit twentyfour as twentyfive, given the real
    /// twentyfive's ids, and compares the copy with the real one (ebx, headers, records; not pixels). Writes nothing.
    /// </summary>
    private static int KitCloneTestCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string team = opts.Positional.FirstOrDefault() ?? "boston";
        string from = opts.Get("from") ?? "twentyfour", to = opts.Get("to") ?? "twentyfive";
        var copy = new KitCopy { Team = team, FromWord = from, ToWord = to };
        var real = KitClone.FolderBundles(index, copy.Folder(to));
        var srcB = KitClone.FolderBundles(index, copy.Folder(from));
        Console.WriteLine($"{copy.Folder(from)}: {srcB.Count} bundles; {copy.Folder(to)}: {real.Count}");
        // the real sibling's ids, by name
        var ids = new KitClone.Ids();
        var tocs = new Dictionary<string, SuperBundleToc>(StringComparer.OrdinalIgnoreCase);
        SuperBundleToc T(string p) => tocs.TryGetValue(p, out var t) ? t : tocs[p] = new SuperBundleToc(p, File.ReadAllBytes(p), index.Layout.TocCatalogs);
        var realRaw = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var realRes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var srcRes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var realRec = new Dictionary<string, (byte[] Bytes, BundleRecord Rec)>(StringComparer.OrdinalIgnoreCase);
        var srcRaw = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        void Load(BundleInfo b, Dictionary<string, byte[]> into, Dictionary<string, byte[]> intoRes, Dictionary<string, (byte[], BundleRecord)> recs)
        {
            var tb = T(b.Toc).Bundles[b.BundleIndex];
            var rb = index.ReadStored(tb.Entries[0]);
            var rec = BundleRecord.Read(rb, 0);
            recs?.Add(b.Name, (rb, rec));
            int k = 1;
            foreach (var e in rec.Ebx)
            {
                var r = tb.Entries[k++];
                if (!into.ContainsKey(e.Name)) into[e.Name] = FrostbiteBlocks.Decode(index.ReadStored(r));
            }
            foreach (var e in rec.Res)
            {
                var r = tb.Entries[k++];
                if (!intoRes.ContainsKey(e.Name)) intoRes[e.Name] = FrostbiteBlocks.Decode(index.ReadStored(r));
            }
        }
        foreach (var b in real) Load(b, realRaw, realRes, realRec);
        foreach (var b in srcB) Load(b, srcRaw, srcRes, null);
        foreach (var (name, raw) in srcRaw.Where(x => x.Key.StartsWith(copy.Folder(from) + "/")))
        {
            string nn = copy.Rename(name);
            if (!realRaw.TryGetValue(nn, out var rr)) { Console.WriteLine($"  real has no {nn}"); continue; }
            ids.Guids[EbxFile.Parse(raw).FileGuid] = EbxFile.Parse(rr).FileGuid;
            ids.Guids[new Guid(raw.AsSpan(0x20, 16))] = new Guid(rr.AsSpan(0x20, 16));
        }
        foreach (var (name, rr) in realRes) ids.Chunks[name] = TextureHeader.Parse(rr, ps4: true).ChunkId;
        foreach (var (_, (_, rec)) in realRec)
            foreach (var r in rec.Res) ids.Rids[r.Name] = r.ResRid;

        string toc = Path.Combine(index.Layout.GameDir, "..", "game", "Data", "Gen4b", "contentsb.toc");
        toc = Path.GetFullPath(toc);
        // a copy into a folder that exists: the check only builds it in memory (the "already has" refusal is for real copies)
        var plan = BuildForTest(index, copy, toc, ids);
        int bad = 0, okEbx = 0, okRes = 0, okRec = 0;
        foreach (var (name, raw) in plan.Raw)
        {
            if (!realRaw.TryGetValue(name, out var rr)) { bad++; Console.WriteLine($"  BAD {name}: not in the real folder"); continue; }
            if (raw.AsSpan().SequenceEqual(rr)) okEbx++;
            else { bad++; Diff(name, raw, rr); }
        }
        foreach (var (name, raw) in plan.RawRes)
        {
            if (!realRes.TryGetValue(name, out var rr)) { bad++; Console.WriteLine($"  BAD {name}: no real header"); continue; }
            {
                // the header's first 8 bytes say where mips 1 and 2 start in the stored chunk: the copy's storage, not the real one's
                if (raw.Length == rr.Length && raw.AsSpan(8).SequenceEqual(rr.AsSpan(8))) okRes++;
                else { bad++; Diff(name, raw, rr); }
            }
        }
        foreach (var nb in plan.Bundles)
        {
            if (!realRec.TryGetValue(nb.Name, out var rr)) { bad++; Console.WriteLine($"  BAD {nb.Name}: no real bundle"); continue; }
            var a = nb.Record; var b = rr.Bytes;
            int total = BinaryPrimitives.ReadInt32LittleEndian(a.AsSpan(8));
            int shaEnd = 36 + 20 * total;
            // the record without its SHA1s (stored bytes differ) and its res metas (the headers' first 16 bytes)
            var ra = nb.Parsed; var rb2 = rr.Rec;
            bool same = a.Length == b.Length && a.AsSpan(0, 36).SequenceEqual(b.AsSpan(0, 36));
            int metasAt = shaEnd + 8 * ra.Ebx.Count + 8 * ra.Res.Count + 4 * ra.Res.Count;
            for (int i = shaEnd; same && i < a.Length; i++)
            {
                bool inMeta = i >= metasAt && i < metasAt + 16 * ra.Res.Count && (i - metasAt) % 16 < 8;
                if (!inMeta && a[i] != b[i]) { same = false; Console.WriteLine($"  record {nb.Name[(nb.Name.LastIndexOf('/') + 1)..]}: byte {i} is {a[i]:X2}, real {b[i]:X2}"); }
            }
            if (same) okRec++; else bad++;
        }
        Console.WriteLine($"ebx byte for byte: {okEbx}; headers (but mip offsets): {okRes}; records (but SHA1s, mip offsets): {okRec}; differences: {bad}");
        Console.WriteLine(string.Join("\n", plan.Notes));
        return bad == 0 ? 0 : 1;

        static void Diff(string name, byte[] a, byte[] b)
        {
            Console.WriteLine($"  BAD {name}: {a.Length} vs {b.Length} bytes");
            int shown = 0;
            for (int i = 0; i < Math.Min(a.Length, b.Length) && shown < 6; i++)
                if (a[i] != b[i]) { Console.WriteLine($"     byte {i}: {a[i]:X2} real {b[i]:X2}"); shown++; }
        }
    }

    // the test copies into a folder the game has: the clone refuses that, so the test's copy goes under a scratch name
    // of the same length and is renamed back in memory
    private static KitClone.Plan BuildForTest(AssetIndex index, KitCopy copy, string toc, KitClone.Ids ids)
    {
        return KitClone.Build(index, new[] { copy }, toc, ids, Console.WriteLine, testInto: true);
    }
}
