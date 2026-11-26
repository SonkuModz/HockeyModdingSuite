using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// NHL 24's content/common/configs/bundlereftable/player_team_brt (a BundleRefTableResource): which bundle holds each
/// team-kit asset. A kit whose assets aren't in it isn't found, and the game draws the template kit
/// (content/character/skater/league/common/template) instead, so every new kit folder needs its entries here.
///
/// Layout (a relocatable block: every pointer is an absolute offset in the resource, listed in a fixup table at the end;
/// the RES meta is [u32 fixup table offset][u32 fixup table bytes]):
/// - header: u64 pointers [suffix string "_player_team_brt"][keys][bundles][assets][nodes][empty string], then u32
///   [key count][bundle count][asset count][0][type hash][0][1][0];
/// - a string pool from 0x50;
/// - bundles, 24 bytes: [name][folder or the empty string][superbundle node] (bundle = folder/name + suffix);
/// - assets, 16 bytes: [short name][folder]; an asset's bundle is the one its keys name (EA's part of the fixup table also lists each bundle's assets right after it; a part another tool appended may not);
/// - keys, 16 bytes, sorted: [u64 FNV-1 64 of an asset's short name or of its full path][u32 bundle][u32 asset];
/// - superbundle nodes, 16 bytes: [name][parent];
/// - the fixup table: u32 offsets of the pointers (each bundle's three, then its assets' two each, ..., the header's last).
/// A copy adds the new bundles at the end of the bundle table, their assets at the end of the asset table, new strings at
/// the end of the pool, and keys merged in order; everything after the pool moves and the pointers into it follow.
/// </summary>
public sealed class BundleRefTable
{
    public const string Name = "content/common/configs/bundlereftable/player_team_brt";

    private byte[] _b;
    private uint _relocStart;
    private ulong[] _q = new ulong[6];
    private uint[] _u = new uint[8];
    private int _nk, _nb, _na, _nodes;
    private int _poolEnd;   // past the last string (before the padding to the bundle table)

    public int Bundles => _nb;
    public int Assets => _na;
    public int Keys => _nk;
    public int TableBytes => _b.Length;
    public uint RelocStart => _relocStart;

    private long Q(int i) => (long)_q[i];
    private ulong U64(long at) => BinaryPrimitives.ReadUInt64LittleEndian(_b.AsSpan((int)at));
    private uint U32(long at) => BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan((int)at));
    public string Str(long at) { int s = (int)at, e = Array.IndexOf(_b, (byte)0, s); return Encoding.Latin1.GetString(_b, s, e - s); }

    public static BundleRefTable Read(byte[] raw, uint relocStart)
    {
        var t = new BundleRefTable { _b = raw, _relocStart = relocStart };
        for (int i = 0; i < 6; i++) t._q[i] = t.U64(8 * i);
        for (int i = 0; i < 8; i++) t._u[i] = t.U32(0x30 + 4 * i);
        t._nk = (int)t._u[0]; t._nb = (int)t._u[1]; t._na = (int)t._u[2];
        long keys = t.Q(1), bundles = t.Q(2), assets = t.Q(3), nodes = t.Q(4);
        if (!(0x50 < bundles && bundles + 24L * t._nb <= assets && assets + 16L * t._na == keys && keys + 16L * t._nk == nodes && nodes < relocStart && relocStart <= raw.Length))
            throw new InvalidDataException("player_team_brt isn't laid out as expected.");
        if ((relocStart - nodes) % 16 != 0 || (raw.Length - relocStart) % 4 != 0) throw new InvalidDataException("player_team_brt's node or fixup table has an odd size.");
        t._nodes = (int)((relocStart - nodes) / 16);
        int end = (int)bundles;
        while (end > 0x50 && raw[end - 1] == 0) end--;
        t._poolEnd = end + 1;   // keep the last string's NUL
        return t;
    }

    public (long Name, long Folder, long Node) Bundle(int i) { long at = Q(2) + 24L * i; return ((long)U64(at), (long)U64(at + 8), (long)U64(at + 16)); }
    public (long Short, long Folder) Asset(int i) { long at = Q(3) + 16L * i; return ((long)U64(at), (long)U64(at + 8)); }
    public (ulong Hash, int Bundle, int Asset) Key(int i) { long at = Q(1) + 16L * i; return (U64(at), (int)U32(at + 8), (int)U32(at + 12)); }
    public string BundleName(int i) { var (n, f, _) = Bundle(i); string s = Str(n), d = Str(f); return d.Length > 0 ? d + "/" + s : s; }
    public string AssetName(int i) { var (s, f) = Asset(i); return Str(f) + "/" + Str(s); }

    public static ulong Fnv64(string s)
    {
        ulong h = 0xcbf29ce484222325;
        foreach (byte c in Encoding.Latin1.GetBytes(s)) { h *= 0x100000001b3; h ^= c; }
        return h;
    }

    private uint[] Relocs()
    {
        int n = (_b.Length - (int)_relocStart) / 4;
        var r = new uint[n];
        for (int i = 0; i < n; i++) r[i] = U32(_relocStart + 4L * i);
        return r;
    }

    /// <summary>Each asset's bundle, as the fixup table orders them (an asset's pointers follow its bundle's).</summary>
    public int[] AssetBundles()
    {
        var of = Enumerable.Repeat(-1, _na).ToArray();
        long b0 = Q(2), a0 = Q(3);
        int cur = -1;
        foreach (uint x in Relocs())
        {
            if (x >= b0 && x < b0 + 24L * _nb && (x - b0) % 24 == 0) cur = (int)((x - b0) / 24);
            else if (x >= a0 && x < a0 + 16L * _na && (x - a0) % 16 == 0) of[(x - a0) / 16] = cur;
        }
        // where a key names an asset's bundle, that's the one (a table extended by another tool may list all its bundles'
        // pointers before all its assets', so the order above doesn't hold there)
        for (int i = 0; i < _nk; i++) { var (_, b, a) = Key(i); of[a] = b; }
        return of;
    }

    /// <summary>Problems: keys out of order, keys whose hash isn't their asset's short name or path, pointers out of place.</summary>
    public List<string> Check()
    {
        var p = new List<string>();
        for (int i = 1; i < _nk; i++) if (Key(i).Hash < Key(i - 1).Hash) { p.Add($"key {i} out of order"); break; }
        int badHash = 0, badIdx = 0;
        for (int i = 0; i < _nk; i++)
        {
            var (h, bi, ai) = Key(i);
            if (bi >= _nb || ai >= _na) { badIdx++; continue; }
            var (s, f) = Asset(ai);
            string sh = Str(s);
            if (h != Fnv64(sh) && h != Fnv64(Str(f) + "/" + sh)) badHash++;
        }
        if (badHash > 0) p.Add($"{badHash} keys don't hash their asset's name");
        if (badIdx > 0) p.Add($"{badIdx} keys point past the tables");
        int badPtr = 0;
        foreach (uint x in Relocs())
        {
            if (x + 8 > _relocStart) { badPtr++; continue; }
            ulong v = U64(x);
            if (v >= (ulong)_relocStart) badPtr++;
        }
        if (badPtr > 0) p.Add($"{badPtr} pointers out of place");
        return p;
    }

    /// <summary>A folder copied as another: <paramref name="Rename"/> maps a donor name (bundle, asset or folder) to the copy's.</summary>
    public sealed record Copy(string FromFolder, string ToFolder, Func<string, string> Rename);

    /// <summary>
    /// The table with each copy's entries added (a copy of the donor folder's live bundles, their assets and keys,
    /// renamed). No copies: the same bytes. <paramref name="relocStart"/> is the new RES meta's first value.
    /// </summary>
    public byte[] With(IReadOnlyList<Copy> copies, out uint relocStart, List<string> log = null)
    {
        var assetBundle = AssetBundles();
        var newStrings = new List<string>();
        var stringAt = new Dictionary<string, long>(StringComparer.Ordinal);
        long poolLen = _poolEnd;
        long StrOf(string s)
        {
            if (stringAt.TryGetValue(s, out long at)) return at;
            at = poolLen; poolLen += Encoding.Latin1.GetByteCount(s) + 1;
            newStrings.Add(s);
            return stringAt[s] = at;
        }
        var addBundles = new List<(long Name, long Folder, long Node)>();   // node: old address
        var addAssets = new List<(long Short, long Folder, int Bundle)>();  // bundle: new index
        var addKeys = new List<(ulong Hash, int Bundle, int Asset)>();
        var existingKeys = new HashSet<ulong>();
        for (int i = 0; i < _nk; i++) existingKeys.Add(Key(i).Hash);
        foreach (var c in copies)
        {
            string from = c.FromFolder.TrimEnd('/') + "/", to = c.ToFolder.TrimEnd('/');
            // the donor's live bundles: those its keys point at (an earlier, superseded copy of a folder can linger)
            var keyed = new List<(ulong Hash, int Bundle, int Asset)>();
            for (int i = 0; i < _nk; i++)
            {
                var k = Key(i);
                if (AssetName(k.Asset).StartsWith(from, StringComparison.Ordinal)) keyed.Add(k);
            }
            if (keyed.Count == 0) throw new InvalidDataException($"player_team_brt has no keys for {c.FromFolder}.");
            var donorBundles = keyed.Select(k => assetBundle[k.Asset]).Concat(keyed.Select(k => k.Bundle))
                .Where(b => b >= 0 && BundleName(b).StartsWith(from, StringComparison.Ordinal)).Distinct().OrderBy(b => b).ToList();
            // the donor's bundles that hold no keyed asset (none expected) are copied too, if they're the only copy of their name
            var names = donorBundles.Select(BundleName).ToHashSet(StringComparer.Ordinal);
            for (int b = 0; b < _nb; b++)
            {
                string n = BundleName(b);
                if (n.StartsWith(from, StringComparison.Ordinal) && !names.Contains(n)) { donorBundles.Add(b); names.Add(n); }
            }
            donorBundles.Sort();
            long emptyStr = Q(5);
            var bundleMap = new Dictionary<int, int>();
            var assetMap = new Dictionary<int, int>();
            foreach (int b in donorBundles)
            {
                var (_, _, node) = Bundle(b);
                string nn = c.Rename(BundleName(b));
                if (!nn.StartsWith(to + "/", StringComparison.Ordinal)) throw new InvalidDataException($"{BundleName(b)} renames to {nn}, outside {to}.");
                bundleMap[b] = _nb + addBundles.Count;
                addBundles.Add((StrOf(nn), emptyStr, node));
                for (int a = 0; a < _na; a++)
                {
                    if (assetBundle[a] != b) continue;
                    var (s, f) = Asset(a);
                    string folder = Str(f), sh = Str(s);
                    string nf = folder.StartsWith(from, StringComparison.Ordinal) || folder + "/" == from ? c.Rename(folder) : folder;
                    string ns = c.Rename(sh);
                    assetMap[a] = _na + addAssets.Count;
                    addAssets.Add((StrOf(ns), StrOf(nf), bundleMap[b]));
                }
            }
            int keysBefore = addKeys.Count;
            foreach (var (h, kb, ka) in keyed)
            {
                if (!assetMap.TryGetValue(ka, out int newA)) continue;
                var (s, f) = Asset(ka);
                string sh = Str(s), full = Str(f) + "/" + sh;
                string nsh = c.Rename(sh), nfull = c.Rename(Str(f)) + "/" + nsh;
                ulong nh = h == Fnv64(sh) ? Fnv64(nsh) : h == Fnv64(full) ? Fnv64(nfull) : throw new InvalidDataException($"A key of {full} hashes neither its name nor its path.");
                if (!existingKeys.Add(nh)) throw new InvalidDataException($"player_team_brt already has a key with {nsh}'s hash.");
                addKeys.Add((nh, bundleMap.TryGetValue(kb, out int newB) ? newB : kb, newA));
            }
            log?.Add($"player_team_brt: {c.ToFolder}: {bundleMap.Count} bundles, {assetMap.Count} assets, {addKeys.Count - keysBefore} keys (from {c.FromFolder})");
        }

        // the new layout
        long poolPadded = (poolLen + 15) & ~15L;
        long oldBundles = Q(2), oldAssets = Q(3), oldKeys = Q(1), oldNodes = Q(4);
        long nb = _nb + addBundles.Count, na = _na + addAssets.Count, nk = _nk + addKeys.Count;
        long newBundles = copies.Count == 0 ? oldBundles : poolPadded;
        long assetsGap = oldAssets - (oldBundles + 24L * _nb);
        long newAssets = copies.Count == 0 ? oldAssets : (newBundles + 24L * nb + 15) & ~15L;
        long newKeys = newAssets + 16L * na, newNodes = newKeys + 16L * nk;
        long newReloc = newNodes + 16L * _nodes;
        long Map(long addr)
        {
            if (addr < oldBundles) return addr;   // header, pool
            if (addr < oldAssets) return newBundles + (addr - oldBundles);
            if (addr < oldKeys) return newAssets + (addr - oldAssets);
            if (addr < oldNodes) return newKeys + (addr - oldKeys);
            if (addr < _relocStart) return newNodes + (addr - oldNodes);
            throw new InvalidDataException($"player_team_brt: a pointer to 0x{addr:X}, past the tables.");
        }
        var relocs = Relocs();
        int headerRelocs = relocs.Reverse().TakeWhile(x => x < 0x30).Count();
        int newRelocCount = relocs.Length + addBundles.Count * 3 + addAssets.Count * 2;
        var o = new byte[newReloc + 4L * newRelocCount];
        // header and pool
        _b.AsSpan(0, _poolEnd).CopyTo(o);
        long w = _poolEnd;
        foreach (var s in newStrings) { var sb = Encoding.Latin1.GetBytes(s); sb.CopyTo(o, w); w += sb.Length + 1; }
        // tables, moved as blocks
        _b.AsSpan((int)oldBundles, 24 * _nb).CopyTo(o.AsSpan((int)newBundles));
        _b.AsSpan((int)oldAssets, 16 * _na).CopyTo(o.AsSpan((int)newAssets));
        _b.AsSpan((int)oldNodes, 16 * _nodes).CopyTo(o.AsSpan((int)newNodes));
        void P64(long at, long v) => BinaryPrimitives.WriteUInt64LittleEndian(o.AsSpan((int)at), (ulong)v);
        // the old pointers, moved and repointed
        foreach (uint x in relocs) P64(Map(x), Map((long)U64(x)));
        if (copies.Count == 0 && assetsGap > 0) _b.AsSpan((int)(oldBundles + 24L * _nb), (int)assetsGap).CopyTo(o.AsSpan((int)(newBundles + 24L * _nb)));
        // new bundles and assets
        var newRelocs = new List<uint>();
        for (int i = 0; i < addBundles.Count; i++)
        {
            long at = newBundles + 24L * (_nb + i);
            P64(at, addBundles[i].Name); P64(at + 8, addBundles[i].Folder); P64(at + 16, Map(addBundles[i].Node));
            newRelocs.Add((uint)at); newRelocs.Add((uint)(at + 8)); newRelocs.Add((uint)(at + 16));
            for (int j = 0; j < addAssets.Count; j++)
            {
                if (addAssets[j].Bundle != _nb + i) continue;
                long aat = newAssets + 16L * (_na + j);
                P64(aat, addAssets[j].Short); P64(aat + 8, addAssets[j].Folder);
                newRelocs.Add((uint)aat); newRelocs.Add((uint)(aat + 8));
            }
        }
        // keys: old and new, by hash
        var keys = Enumerable.Range(0, _nk).Select(Key).Concat(addKeys).OrderBy(k => k.Hash).ToList();
        if (copies.Count == 0) keys = Enumerable.Range(0, _nk).Select(Key).ToList();   // as they are (the order is the file's)
        for (int i = 0; i < keys.Count; i++)
        {
            long at = newKeys + 16L * i;
            BinaryPrimitives.WriteUInt64LittleEndian(o.AsSpan((int)at), keys[i].Hash);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan((int)at + 8), (uint)keys[i].Bundle);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan((int)at + 12), (uint)keys[i].Asset);
        }
        // counts
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x30), (uint)nk);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x34), (uint)nb);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x38), (uint)na);
        // the fixup table: the old entries (moved), the new ones before the header's
        var all = relocs.Take(relocs.Length - headerRelocs).Select(x => (uint)Map(x)).Concat(newRelocs).Concat(relocs.Skip(relocs.Length - headerRelocs)).ToList();
        for (int i = 0; i < all.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan((int)(newReloc + 4L * i)), all[i]);
        relocStart = (uint)newReloc;
        return o;
    }
}
