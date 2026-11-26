using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>A new UI texture copied from a stock one (same name length): <see cref="Rgba"/> is its picture at the stock size (null keeps the stock picture).</summary>
public sealed record TextureClone(string From, string To, byte[] Rgba);

/// <summary>
/// Adds new UI textures to the game as new bundles, each a copy of a stock texture's own bundle
/// (gen4b/&lt;texture&gt;_launch_ui_textures_common_brt: three ebx — the texture, its bundle-ref blueprint and blueprint
/// bundle —, the texture header res and the texture chunk). What a copy changes, found by comparing stock siblings:
/// - names (same length, so no offsets move);
/// - each ebx's partition guid and the texture ebx's own guid (instance guids are the same in every copy);
/// - the res id (in the texture ebx and the record), the chunk id (in the res and the record);
/// - the header's name hash (djb2 with xor, of the texture name) and the same hash in the record's chunk meta;
/// - the record's SHA1s (of each asset's stored bytes).
/// The data goes at the end of a patch CAS file, the bundles and their chunks into a patch TOC (<see cref="TocAdder"/>).
/// </summary>
public static class AddedTextures
{
    public const string BundleSuffix = "_launch_ui_textures_common_brt";
    public static string BundleOf(string texture) => "gen4b/" + texture + BundleSuffix;

    /// <summary>The texture header's name hash: x = x * 33 ^ c from 5381, over the name's bytes.</summary>
    public static uint NameHash(string name)
    {
        uint x = 5381;
        foreach (byte b in Encoding.UTF8.GetBytes(name)) x = (x * 33) ^ b;
        return x;
    }

    /// <summary>A guid from a seed, the same every build (so a rebuilt mod is byte-identical).</summary>
    public static Guid SeededGuid(string seed)
    {
        var h = MD5.HashData(Encoding.UTF8.GetBytes("hms-texture|" + seed));
        h[7] = (byte)((h[7] & 0x0F) | 0x40);
        h[8] = (byte)((h[8] & 0x3F) | 0x80);
        return new Guid(h);
    }

    /// <summary>A copied bundle: its record and its assets' stored bytes (record order: ebx, res, chunk), raw for checks.</summary>
    public sealed class Copy
    {
        public string Bundle;
        public string Texture;
        public byte[] Record;
        public List<byte[]> Stored = new();
        public List<byte[]> Raw = new();
        public Guid ChunkId;
        public ulong Rid;
    }

    private static int Replace(byte[] b, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, int start = 0)
    {
        int n = 0;
        for (int i = start; i + from.Length <= b.Length;)
        {
            int at = b.AsSpan(i).IndexOf(from);
            if (at < 0) break;
            to.CopyTo(b.AsSpan(i + at));
            n++;
            i += at + from.Length;
        }
        return n;
    }

    private static byte[] U64(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); return b; }
    private static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }

    /// <param name="guidFor">The new guid for an old one (checks pass the real sibling's); by default seeded from the new name.</param>
    public static Copy Clone(AssetIndex index, TextureClone c, Dictionary<string, SuperBundleToc> tocs, Func<Guid, Guid> guidFor = null,
                             ulong? rid = null, Guid? chunkId = null)
    {
        if (c.From.Length != c.To.Length) throw new ArgumentException($"{c.To} must be as long as {c.From}.");
        string from = BundleOf(c.From);
        var src = index.BundleList.FirstOrDefault(b => b.Name.Equals(from, StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidDataException($"No bundle {from}.");
        if (!tocs.TryGetValue(src.Toc, out var toc)) tocs[src.Toc] = toc = new SuperBundleToc(src.Toc, File.ReadAllBytes(src.Toc));
        var tb = toc.Bundles[src.BundleIndex];
        var record = index.ReadStored(src.Record);
        var rec = BundleRecord.Read(record, 0);
        if (rec.Ebx.Count != 3 || rec.Res.Count != 1 || rec.Chunks.Count != 1 || tb.Entries.Count != 6)
            throw new InvalidDataException($"{from} isn't a one-texture bundle (3 ebx, 1 res, 1 chunk).");
        var raw = Enumerable.Range(1, 5).Select(j => FrostbiteBlocks.Decode(index.ReadStored(tb.Entries[j]))).ToList();
        byte[] fromName = Encoding.UTF8.GetBytes(c.From), toName = Encoding.UTF8.GetBytes(c.To);
        guidFor ??= g => SeededGuid(c.To + "|" + g);

        // ebx: names, partition guids and each file's first guid
        var old = new HashSet<Guid>();
        for (int j = 0; j < 3; j++)
        {
            if (!EbxFile.IsEbx(raw[j])) throw new InvalidDataException($"{rec.Ebx[j].Name} isn't an ebx file.");
            old.Add(EbxFile.Parse(raw[j]).FileGuid);
            old.Add(new Guid(raw[j].AsSpan(0x20, 16)));
        }
        for (int j = 0; j < 3; j++)
        {
            if (Replace(raw[j], fromName, toName) == 0) throw new InvalidDataException($"{rec.Ebx[j].Name} doesn't hold its name.");
            foreach (var g in old) Replace(raw[j], g.ToByteArray(), guidFor(g).ToByteArray());
        }
        // res id: in the texture ebx and the record
        ulong oldRid = rec.Res[0].ResRid;
        // res ids are odd: the game reads an even one as a pointer to a loaded resource
        ulong newRid = rid ?? BinaryPrimitives.ReadUInt64LittleEndian(MD5.HashData(Encoding.UTF8.GetBytes("hms-rid|" + c.To))) | 1;
        if (rid == null && index.Res.Values.Any(r => r.ResRid == newRid)) throw new InvalidDataException($"The res id for {c.To} is taken.");
        if (Enumerable.Range(0, 3).Sum(j => Replace(raw[j], U64(oldRid), U64(newRid))) != 1) throw new InvalidDataException($"{c.From}'s res id isn't in its ebx once.");
        // res: chunk id and name hash
        var res = raw[3];
        var header = TextureHeader.Parse(res, ps4: true);
        Guid oldChunk = rec.Chunks[0].Id, newChunk = chunkId ?? SeededGuid("chunk|" + c.To);
        if (header.ChunkId != oldChunk) throw new InvalidDataException($"{c.From}'s header names another chunk.");
        if (header.NameHash != NameHash(c.From)) throw new InvalidDataException($"{c.From}'s header name hash isn't the one expected.");
        newChunk.ToByteArray().CopyTo(res, 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(res.AsSpan(0x70), NameHash(c.To));
        // chunk: the new picture
        if (c.Rgba != null)
        {
            var chunk = TextureBuilder.BuildChunk(header, c.Rgba);
            if (chunk.Length != raw[4].Length) throw new InvalidDataException($"The new {c.To} is {chunk.Length:N0} bytes, the stock one {raw[4].Length:N0}.");
            raw[4] = chunk;
        }
        var stored = raw.Select(r => FrostbiteBlocks.Store(r)).ToList();

        // record: names, SHA1s, res id, chunk id, chunk meta hash
        int strings = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(24)) + 4;
        if (Replace(record, fromName, toName, strings) == 0) throw new InvalidDataException("The record doesn't hold the names.");
        for (int j = 0; j < 5; j++) SHA1.HashData(stored[j]).CopyTo(record, 36 + 20 * j);
        if (Replace(record, U64(oldRid), U64(newRid)) != 1) throw new InvalidDataException("The record's res id isn't there once.");
        if (Replace(record, oldChunk.ToByteArray(), newChunk.ToByteArray()) != 1) throw new InvalidDataException("The record's chunk id isn't there once.");
        if (Replace(record, U32(NameHash(c.From)), U32(NameHash(c.To)), strings) != 1) throw new InvalidDataException("The record's chunk meta hash isn't there once.");
        return new Copy { Bundle = BundleOf(c.To), Texture = c.To, Record = record, Stored = stored, Raw = raw, ChunkId = newChunk, Rid = newRid };
    }

    public sealed class Plan
    {
        public List<PatchData> Patches { get; } = new();
        public List<string> Notes { get; } = new();
        public List<Copy> Copies { get; } = new();
        public byte[] TocAfter { get; set; }
        public string TocRel { get; set; }
        /// <summary>The TOC ranges rewritten in place or copied (relative to the file start) — no other mod may patch them.</summary>
        public List<(long Start, long End, string What)> Owned { get; } = new();
    }

    /// <summary>
    /// Plans the copies into the patch TOC <paramref name="tocPath"/>: their data at the end of the patch CAS file that a
    /// one-group, six-entry bundle of that TOC already uses (its block is the template for theirs).
    /// </summary>
    public static Plan Build(AssetIndex index, string tocPath, IReadOnlyList<TextureClone> clones, Action<string> log = null)
    {
        var layout = index.Layout;
        string Rel(string p) => Path.GetRelativePath(layout.GameDir, p).Replace('\\', '/');
        var plan = new Plan { TocRel = Rel(tocPath) };
        var tocBytes = File.ReadAllBytes(tocPath);
        var parsed = new SuperBundleToc(tocPath, tocBytes, layout.TocCatalogs);
        var raw = TocAdder.Toc.Read(tocBytes);

        // the template: a patch bundle of six entries in one group, its block 90 bytes as this layout has it
        int tmpl = -1;
        for (int i = 0; i < raw.N && tmpl < 0; i++)
        {
            var e = parsed.Bundles[i].Entries;
            if (e.Count != 6 || !e.All(x => x.Patch && x.Catalog == e[0].Catalog && x.Cas == e[0].Cas) || (raw.SizeFields[i] & 0xFFFFFF) != 90) continue;
            int s = raw.Base + (int)raw.Blocks[i];
            int I(int p) => BinaryPrimitives.ReadInt32BigEndian(tocBytes.AsSpan(s + p));
            if (I(0) == 0 && I(4) == 0 && I(8) == 84 && I(12) == 6 && I(16) == 32 && tocBytes[s + 84] == 1 && tocBytes.AsSpan(s + 85, 5).IndexOfAnyExcept((byte)0) < 0)
                tmpl = i;
        }
        if (tmpl < 0) throw new InvalidDataException("No bundle of the TOC has the block layout to copy.");
        int ts = raw.Base + (int)raw.Blocks[tmpl];
        var template = tocBytes[ts..(ts + 90)];
        var where = parsed.Bundles[tmpl].Entries[0];
        string cas = layout.CasPath(where.Catalog, where.Cas, true);
        var group = template[32..36];
        // the chunk table's group for that CAS file
        var tableChunk = parsed.Chunks.FirstOrDefault(x => x.Ref.Patch && x.Ref.Catalog == where.Catalog && x.Ref.Cas == where.Cas);
        if (tableChunk.Ref.OffsetPos == 0) throw new InvalidDataException("The TOC's chunk table has nothing in that CAS file.");
        var tableGroup = tocBytes[(tableChunk.Ref.OffsetPos - 4)..tableChunk.Ref.OffsetPos];

        long length = new FileInfo(cas).Length, start = (length + 15) & ~15L;
        var data = new MemoryStream();
        data.Write(new byte[start - length]);
        var tocs = new Dictionary<string, SuperBundleToc>(StringComparer.OrdinalIgnoreCase);
        var added = new List<AddedBundle>();
        foreach (var c in clones)
        {
            var copy = Clone(index, c, tocs);
            plan.Copies.Add(copy);
            var block = (byte[])template.Clone();
            group.CopyTo(block, 32);
            var pieces = new[] { copy.Record }.Concat(copy.Stored).ToList();
            byte[] chunkEntry = null;
            for (int j = 0; j < 6; j++)
            {
                long at = length + data.Length;
                if (at + pieces[j].Length > uint.MaxValue) throw new InvalidDataException($"{Rel(cas)} would pass 4 GB.");
                BinaryPrimitives.WriteUInt32BigEndian(block.AsSpan(36 + 8 * j), (uint)at);
                BinaryPrimitives.WriteUInt32BigEndian(block.AsSpan(40 + 8 * j), (uint)pieces[j].Length);
                if (j == 5)
                {
                    chunkEntry = new byte[12];
                    tableGroup.CopyTo(chunkEntry, 0);
                    BinaryPrimitives.WriteUInt32BigEndian(chunkEntry.AsSpan(4), (uint)at);
                    BinaryPrimitives.WriteUInt32BigEndian(chunkEntry.AsSpan(8), (uint)pieces[j].Length);
                }
                data.Write(pieces[j]);
            }
            added.Add(new AddedBundle(copy.Bundle, block, new[] { (copy.ChunkId, chunkEntry) }));
            log?.Invoke($"{copy.Texture}: copied from {c.From}");
        }
        plan.Patches.Add(new PatchData(Rel(cas), length, Array.Empty<byte>(), data.ToArray(), "added textures", Append: true));
        plan.Notes.Add($"{clones.Count} textures, {data.Length:N0} bytes added to {Rel(cas)} at 0x{start:X}");
        var toc = TocAdder.Plan(plan.TocRel, tocBytes, added);
        plan.Patches.AddRange(toc.Patches);
        plan.Notes.AddRange(toc.Notes);
        plan.TocAfter = toc.After;
        var h = raw.H;
        long B(long rel) => raw.Base + rel;
        plan.Owned.Add((0, B(h[6]), "header, hash maps, bundle entries, chunk guids"));
        plan.Owned.Add((B(h[6] + raw.M * 12), B(h[14] + h[13] * 4), "names and tree"));
        return plan;
    }
}
