using System.Security.Cryptography;
using NHL27Kit.Core;

namespace NHL27Kit.Cli;

public static partial class Program
{
    /// <summary>
    /// kit-alts --kit TEAM:FROM:TO[:light|dark[:NHL27WORD]] ... [--game27 DIR] [--apply] [--restore]: NHL 24. Adds kits as new kit folders: each a copy of the
    /// team's kit FROM (24/25, the Center Ice Mod's) as variant TO, with a jersey slot and equipment rows copied from
    /// FROM's. Without --apply it plans and checks (nothing written). One mod entry, "NHL 27 alternates".
    /// </summary>
    private static int KitAltsCmd(string game, Options opts)
    {
        const string entryName = "NHL 27 alternates";
        var store = Mods();
        var existing = store.Find(KitAlternates.Kind, entryName);
        if (opts.Has("restore"))
        {
            if (existing == null) { Console.WriteLine("no entry"); return 0; }
            RefuseRunning();
            store.Restore(existing, game, Console.WriteLine);
            Console.WriteLine($"restored: {store.State(existing, game)}");
            return 0;
        }
        var index = OpenIndex(game, false);
        if (index.Layout.IsPs5) throw new ArgumentException("kit-alts is for NHL 24.");
        var legacy = new Legacy(index);
        var db = legacy.OpenDb(JerseySlots.Database);
        var teams = JerseySlots.Teams(db, Kits.Find(index));
        var kits = new List<AltKit>();
        var word27Of = new Dictionary<AltKit, string>(ReferenceEqualityComparer.Instance);   // by reference: a kit record changes (its NHL 27 colours) after it is added
        var copies = new List<KitCopy>();
        var converts = new List<KitConvert27>();
        AssetIndex i27 = null;
        T3Db db27 = null;
        if (opts.Get("game27") is { } g27)
        {
            g27 = KitPaths.Canonical(g27) ?? g27;
            i27 = AssetIndex.Open(g27, File.ReadAllBytes, KitPaths.IndexFileFor(GameProfile.Detect(g27)), _ => { });
            if (!i27.Layout.IsPs5) throw new ArgumentException("--game27 should be NHL 27 (PS5).");
        }
        // NHL 24 has only the adidas fabric (weave); NHL 27's kits use its Fanatics one: a copy of the adidas folder with NHL
        // 27's Fanatics pixels, and the converted kits' shader presets pointed at it (the same weave variant, the other brand)
        const string weaveDir = "content/character/skater/league/shared/textile/weave/";
        KitCopy weave = null;
        KitConvert27 wcv = null;
        var weaveSwap = new Dictionary<Guid, Guid>();   // an adidas weave texture's partition and first guid -> its copy's
        int weaveSwapped = 0;
        byte[] SwapWeave(byte[] b)
        {
            if (weaveSwap.Count == 0) return null;
            var o = (byte[])b.Clone();
            int n = 0;
            foreach (var (from, to) in weaveSwap)
            {
                var f = from.ToByteArray(); var t = to.ToByteArray();
                for (int at = o.AsSpan().IndexOf(f); at >= 0; at = o.AsSpan().IndexOf(f)) { t.CopyTo(o, at); n++; }
            }
            if (n == 0) return null;
            weaveSwapped++;
            return o;
        }
        foreach (var spec in opts.All("kit"))
        {
            var p = spec.Split(':');
            var kt = teams.FirstOrDefault(x => x.Key.League == "nhl" && x.Key.Team == p[0]).Value ?? throw new ArgumentException($"no NHL kit team {p[0]}");
            int from = int.Parse(p[1]), to = int.Parse(p[2]);
            kits.Add(new AltKit { TeamId = kt.TeamId, ArtId = kt.ArtId, Team = p[0], FromVariant = from, ToVariant = to, Light = p.Length > 3 && p[3] == "light", What = spec });
            KitConvert27 cv = null;
            string mode = p.Length > 5 ? p[5] : "full";
            if (mode is not ("full" or "pixels" or "stamps" or "store")) throw new ArgumentException($"{spec}: the 6th field is pixels, stamps or store.");
            if (p.Length > 4)
            {
                if (i27 == null) throw new ArgumentException($"{spec}: an NHL 27 kit needs --game27 DIR.");
                cv = new KitConvert27(i27, index, p[0], p[4]);
                converts.Add(cv);
                word27Of[kits.Last()] = p[4];
                db27 ??= new Legacy(i27).OpenDb(JerseySlots.Database);
                var z27 = KitEquipment27.Read(db27, kt.ArtId, Array.IndexOf(KitAlternates.Words, p[4]));
                if (z27.Count > 0 && !opts.Has("donor-colours")) kits.Last().Zones27 = z27;
                else Console.WriteLine($"  {spec}: NHL 27 has no equipment colours for it; the donor's stay");
            }
            copies.Add(new KitCopy
            {
                Team = p[0], FromWord = KitAlternates.Words[from], ToWord = KitAlternates.Words[to],
                // a 6th field limits the conversion (checks): pixels (NHL 27 textures, the donor's stamps), stamps (the reverse),
                // store (all of it, textures stored uncompressed)
                Pixels = cv == null || mode == "stamps" ? null : (n, h) => cv.Pixels(copies.Last(x => x.ToWord == KitAlternates.Words[to] && x.Team == p[0]), n, h),
                Ebx = cv == null ? null : (n, b) =>
                {
                    var stamp = mode == "pixels" ? null : cv.Stamp(copies.Last(x => x.ToWord == KitAlternates.Words[to] && x.Team == p[0]), n, b);
                    return SwapWeave(stamp ?? b) ?? stamp;
                },
                Uncompressed = mode == "store",
                // its shader presets' bundles carry the adidas weave: listed as the Fanatics copy's instead
                SharedRenames = cv == null || opts.Has("adidas-weave") ? null : new[] { ("weave/adidas/", "weave/fanatc/"), ("_adidas_default", "_fanatc_default") },
            });
        }
        if (kits.Count == 0) throw new ArgumentException("kit-alts needs --kit TEAM:FROM:TO");
        if (existing != null && store.State(existing, game) != ModState.NotApplied)
            throw new InvalidOperationException($"\"{entryName}\" is in the game: --restore it first.");

        var all = new List<KitCopy>(copies);
        if (converts.Count > 0 && !opts.Has("adidas-weave"))
        {
            if (index.GetEbx(weaveDir + "fanatc/weave_jersey_fanatc_default_normal_array") != null) throw new InvalidOperationException("NHL 24 already has the Fanatics weave folder.");
            wcv = KitConvert27.Weave(i27, index, "fanatc", "fanatics");
            weave = new KitCopy { League = "shared", Team = "textile", Brand = "weave", FromWord = "adidas", ToWord = "fanatc", FileFrom = "_adidas_", FileTo = "_fanatc_", Pixels = (n, h) => wcv.Pixels(weave, n, h) };
            all.Insert(0, weave);   // built first: the kits' bundles list its files
            foreach (var e in index.Ebx.Values.Where(e => e.Name.StartsWith(weaveDir + "adidas/") && !e.Name.Contains("_player_team_brt")).ToList())
            {
                string nn = weave.Rename(e.Name);
                if (i27.GetEbx(wcv.Name27(weave, nn)) == null) { Console.WriteLine($"  NHL 27 has no {wcv.Name27(weave, nn)}; {Path.GetFileName(e.Name)} stays adidas"); continue; }
                var raw = index.Read(e);
                var x = EbxFile.Parse(raw);
                var g0 = new Guid(raw.AsSpan(0x20, 16));
                weaveSwap[x.FileGuid] = KitClone.CopyGuid(nn, x.FileGuid);
                weaveSwap[g0] = KitClone.CopyGuid(nn, g0);
            }
        }
        string toc = Path.GetFullPath(Path.Combine(index.Layout.GameDir, "..", "game", "Data", "Gen4b", "contentsb.toc"));
        if (!File.Exists(toc)) toc = Path.Combine(index.Layout.GameDir, "Data", "Gen4b", "contentsb.toc");
        var plan = KitClone.Build(index, all, toc, null, Console.WriteLine);
        if (wcv != null)
        {
            Console.WriteLine($"NHL 27 Fanatics weave ({weaveSwap.Count / 2} textures; {weaveSwapped} kit file(s) pointed at it):");
            wcv.Log.ForEach(x => Console.WriteLine("  " + x));
        }
        foreach (var cv in converts)
        {
            Console.WriteLine($"NHL 27 {cv.Team} {cv.Word27}:");
            cv.Log.ForEach(x => Console.WriteLine("  " + x));
        }
        // the game's table of which bundle holds each kit asset: the new folders' entries in each stored copy
        var grow = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [plan.CasRel] = plan.CasLength + plan.Data.Length };
        if (plan.PatchCasRel != null) grow[plan.PatchCasRel] = plan.PatchCasLength + plan.PatchData.Length;
        var brtCopies = all.Select(c => new BundleRefTable.Copy(c.Folder(c.FromWord), c.Folder(c.ToWord), s => c.Rename(s + "/")[..^1])).ToList();
        var brtLog = new List<string>();
        var brt = ResGrower.Plan(index, BundleRefTable.Name, (raw, meta) =>
        {
            uint rs = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(meta);
            if (rs + System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(meta.AsSpan(4)) != raw.Length) throw new InvalidDataException("player_team_brt: its meta doesn't add up to its size.");
            var t = BundleRefTable.Read(raw, rs);
            var p0 = t.Check();
            if (p0.Count > 0) throw new InvalidDataException("player_team_brt as it is: " + string.Join("; ", p0));
            var same = t.With(Array.Empty<BundleRefTable.Copy>(), out uint rs0);
            if (rs0 != rs || !same.AsSpan().SequenceEqual(raw)) throw new InvalidDataException("player_team_brt doesn't rebuild to the same bytes.");
            var nb = t.With(brtCopies, out uint nrs, brtLog);
            var nm = (byte[])meta.Clone();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(nm, nrs);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(nm.AsSpan(4), (uint)(nb.Length - nrs));
            return (nb, nm);
        }, grow);
        brtLog.Distinct().ToList().ForEach(x => Console.WriteLine("  " + x));
        brt.Notes.ForEach(x => Console.WriteLine("  " + x));
        plan.Patches.AddRange(brt.Patches);
        var log = new List<string>();
        KitAlternates.AddRows(db, kits, log);
        log.ForEach(x => Console.WriteLine("  " + x));

        // menu names: VINTAGE_2_<art>_<variant> strings in each language database, the text NHL 27 gives the kit (its own
        // language's, else English) or --name TEAM:VARIANT=Text. The tables are full, so new keys take over the rows of kit
        // names no jersey slot shows.
        var nameOf = new Dictionary<string, (string En, Dictionary<string, string> ByLang)>(StringComparer.OrdinalIgnoreCase);   // key -> texts
        var overrides = opts.All("name").Select(x => x.Split('=', 2)).ToDictionary(x => x[0].ToLowerInvariant(), x => x[1]);
        var leg27 = i27 != null ? new Legacy(i27) : null;
        // English only unless asked (writing every language database crashed the game at start-up, 2026-11-24)
        var langs = opts.Has("all-languages")
            ? legacy.Names.Where(n => n.StartsWith("loc/") && n.EndsWith(".db") && !n.Contains("-meta")).OrderBy(n => n != LocDb.English).ThenBy(n => n).ToList()
            : new List<string> { LocDb.English };
        var names27 = new Dictionary<string, Dictionary<string, KitNameRef>>();
        Dictionary<string, KitNameRef> Names27(string lang)
        {
            if (names27.TryGetValue(lang, out var m)) return m;
            try { m = leg27?.Find(lang) != null ? KitNames.Read(leg27.OpenDb(lang)) : null; } catch { m = null; }
            return names27[lang] = m;
        }
        foreach (var k in kits)
        {
            string key = KitNames.Key(KitAlternates.Adidas, k.ArtId, k.ToVariant);
            string en = overrides.GetValueOrDefault($"{k.Team}:{k.ToVariant}");
            var byLang = new Dictionary<string, string>();
            if (word27Of.TryGetValue(k, out var w27))
            {
                string key27 = KitNames.Key(KitAlternates.Adidas, k.ArtId, Array.IndexOf(KitAlternates.Words, w27));
                foreach (var lang in langs)
                    if (Names27(lang)?.GetValueOrDefault(key27) is { } r) byLang[lang] = r.Text;
                en ??= byLang.GetValueOrDefault(LocDb.English);
                if (en == null) Console.WriteLine($"  NHL 27 has no name for {key27}");
            }
            if (en == null) continue;
            if (overrides.ContainsKey($"{k.Team}:{k.ToVariant}")) byLang.Clear();
            nameOf[key] = (en, byLang);
        }
        if (opts.Get("loc-rows") is { } cap) LocDb.RowCap = Math.Min(65535, int.Parse(cap));   // rows a language table may grow to
        var locFiles = new List<(LegacyFile File, byte[] Data, string Lang, List<string> Taken)>();
        if (opts.Has("loc-unchanged"))
        {
            // a check: the English database written back as it is, through the same path
            nameOf.Clear();
            var ldb = legacy.OpenDb(LocDb.English);
            locFiles.Add((ldb.Source, (byte[])ldb.Data.Clone(), LocDb.English, new List<string>()));
            Console.WriteLine("  loc/eng_us.db written back unchanged (check)");
        }
        if (nameOf.Count > 0)
        {
            var slotKits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var js = db.Tables.First(x => x.Name == "stockteamjerseys");
            var jf = js.Fields.ToDictionary(f => f.Name);
            for (int r = 0; r < js.WrittenRecords; r++)
                if (!db.IsInvalid(js, r)) slotKits.Add(KitNames.Key(Convert.ToInt32(db.Get(js, r, jf["style"])), Convert.ToInt32(db.Get(js, r, jf["teamartid"])), Convert.ToInt32(db.Get(js, r, jf["variant"]))));
            foreach (var lang in langs)
            {
                var ldb = legacy.OpenDb(lang);
                var have = KitNames.Read(ldb);
                if (have.Count == 0) continue;   // a language with no kit names (rus_ru)
                // spare: kit names no slot shows (art 903's, the legends', kept), oldest first
                var ids = LocDb.ReadAll(ldb).Select(r => r.Id).Where(i => i.StartsWith("vintage_", StringComparison.OrdinalIgnoreCase)).ToList();
                var spare = ids.Where(i => !slotKits.Contains(i) && !nameOf.ContainsKey(i) && !i.ToLowerInvariant().Contains("_903_")).OrderBy(i => i.ToLowerInvariant()).ToList();
                // a key already there (any case) keeps its spelling
                var texts = nameOf.ToDictionary(kv => ids.FirstOrDefault(i => i.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) ?? "Vintage" + kv.Key[7..], kv => kv.Value.ByLang.GetValueOrDefault(lang) ?? kv.Value.En);
                var (bytes, addedKeys, taken) = LocDb.Set(ldb, texts, spare);
                locFiles.Add((ldb.Source, bytes, lang, taken));
                Console.WriteLine($"  {lang}: {texts.Count} kit name(s): {string.Join(", ", texts.Select(t => $"{t.Key} \"{t.Value}\""))}; {addedKeys.Count} new row(s), {taken.Count} taken over ({string.Join(", ", taken)})");
            }
        }

        int bad = 0;
        void Check(bool ok, string what) { if (!ok) bad++; Console.WriteLine($"  {(ok ? "ok " : "BAD")} {what}"); }
        Console.WriteLine("checks:");
        var before = new SuperBundleToc(toc, File.ReadAllBytes(toc), index.Layout.TocCatalogs);
        var after = new SuperBundleToc(toc, plan.TocAfter, index.Layout.TocCatalogs);
        Check(TocAdder.Toc.Read(plan.TocAfter).Check().Count == 0, $"the TOC's hash maps find all {after.Bundles.Count:N0} bundles and {after.Chunks.Count:N0} chunks");
        var byName = after.Bundles.ToDictionary(b => b.Name);
        Check(before.Bundles.All(b => byName.TryGetValue(b.Name, out var x) && x.Entries.SequenceEqual(b.Entries)), $"every old bundle ({before.Bundles.Count:N0}) has the same entries");
        var chunksAfter = after.Chunks.ToDictionary(c => c.Id);
        Check(before.Chunks.All(c => chunksAfter.TryGetValue(c.Id, out var x) && x.Ref == c.Ref), $"every old chunk ({before.Chunks.Count:N0}) is where it was");
        int entriesOk = 0, entriesBad = 0;
        foreach (var nb in plan.Bundles)
        {
            if (!byName.TryGetValue(nb.Name, out var tb) || tb.Entries.Count != nb.Entries.Count) { entriesBad++; Console.WriteLine($"    {nb.Name}: not listed as planned"); continue; }
            bool ok = true;
            for (int j = 0; j < nb.Entries.Count; j++)
                ok &= tb.Entries[j].Offset == nb.Entries[j].Offset && tb.Entries[j].Size == nb.Entries[j].Size;
            // each asset's SHA1 in the record is its stored bytes' (new ones) or the record's own (shared ones)
            var rec = nb.Parsed;
            var items = rec.Ebx.Concat(rec.Res).Concat(rec.Chunks).ToList();
            for (int j = 0; j < items.Count; j++)
                if (nb.Entries[j + 1].Stored is { } st) ok &= SHA1.HashData(st).AsSpan().SequenceEqual(items[j].Sha1);
            // new ebx read back from their stored bytes
            var copy = all.First(c => nb.Name.Contains("/" + c.ToWord + "/"));
            for (int j = 0; j < rec.Ebx.Count; j++)
                if (nb.Entries[j + 1].Stored is { } st) ok &= plan.Raw.TryGetValue(rec.Ebx[j].Name, out var want) && FrostbiteBlocks.Decode(st).AsSpan().SequenceEqual(want) && (rec.Ebx[j].Name.Contains("/" + copy.ToWord + "/") || copy.SharedRenames != null);
            if (ok) entriesOk++; else { entriesBad++; Console.WriteLine($"    {nb.Name}: entries, SHA1s or ebx not as planned"); }
        }
        Check(entriesBad == 0, $"{entriesOk} new bundles listed with their entries, SHA1s and ebx as planned");
        if (plan.PatchTocAfter != null)
        {
            string ptoc = Path.Combine(index.Layout.GameDir, plan.PatchTocRel);
            var pBefore = new SuperBundleToc(ptoc, File.ReadAllBytes(ptoc), index.Layout.TocCatalogs);
            var pAfter = new SuperBundleToc(ptoc, plan.PatchTocAfter, index.Layout.TocCatalogs);
            Check(TocAdder.Toc.Read(plan.PatchTocAfter).Check().Count == 0, $"the patch TOC's hash maps find all {pAfter.Bundles.Count:N0} bundles and {pAfter.Chunks.Count:N0} chunks");
            var pByName = pAfter.Bundles.ToDictionary(b => b.Name);
            Check(pBefore.Bundles.All(b => pByName.TryGetValue(b.Name, out var x) && x.Entries.SequenceEqual(b.Entries)), $"every old patch bundle ({pBefore.Bundles.Count:N0}) has the same entries");
            var pChunks = pAfter.Chunks.ToDictionary(c => c.Id);
            Check(pBefore.Chunks.All(c => pChunks.TryGetValue(c.Id, out var x) && x.Ref == c.Ref), $"every old patch chunk ({pBefore.Chunks.Count:N0}) is where it was");
            var newByName = plan.Bundles.ToDictionary(b => b.Name);
            bool PatchRootOk(string n)
            {
                if (!pByName.TryGetValue(n, out var pb) || !byName.TryGetValue(n, out var bb)) return false;
                var pe = newByName[n].PatchEntries;
                if (pe == null || pb.Entries.Count != bb.Entries.Count || pe.Count != pb.Entries.Count) return false;
                for (int j = 0; j < pe.Count; j++)
                {
                    var (x, y) = (pb.Entries[j], bb.Entries[j]);
                    if (x.Offset != pe[j].Offset || x.Size != pe[j].Size) return false;
                    // files the copy wrote: in the patch CAS; the rest (chunks, files it shares) where the base copy has them
                    if (pe[j].Stored != null && j < 1 + newByName[n].Parsed.Ebx.Count + newByName[n].Parsed.Res.Count) { if (!x.Patch || x.Catalog != y.Catalog) return false; }
                    else if ((x.Catalog, x.Cas, x.Patch, x.Offset, x.Size) != (y.Catalog, y.Cas, y.Patch, y.Offset, y.Size)) return false;
                }
                return !pb.Entries[0].Patch ? false : true;
            }
            Check(plan.PatchBundles.All(PatchRootOk),
                $"{plan.PatchBundles.Count} root bundle(s) in the patch TOC, record and own files in {plan.PatchCasRel}, chunks as in the base: {string.Join(", ", plan.PatchBundles.Select(n => n[(n.LastIndexOf('/') + 1)..]))}");
        }
        else Check(false, "no root bundle listed in the patch TOC (the game finds kits there)");
        if (wcv != null)
        {
            // every converted kit bundle that carried an adidas weave texture now lists the Fanatics one, with nothing adidas left
            var carriers = plan.Bundles.Where(b => copies.Any(c => c.SharedRenames != null && b.Name.Contains("/" + c.ToWord + "/"))).ToList();
            int fan = carriers.Count(b => b.Parsed.Ebx.Concat(b.Parsed.Res).Any(x => x.Name.Contains("weave/fanatc/")));
            int adi = carriers.Count(b => b.Parsed.Ebx.Concat(b.Parsed.Res).Any(x => x.Name.Contains("weave/adidas/")));
            Check(fan > 0 && adi == 0, $"{fan} converted kit bundle(s) carry the Fanatics weave, {adi} still an adidas one");
        }
        if (wcv != null) Check(weaveSwapped >= converts.Count, $"{weaveSwapped} converted kit file(s) use the Fanatics weave (at least one a kit)");
        int missing = plan.ChunkStored.Keys.Count(c => !chunksAfter.ContainsKey(c));
        Check(missing == 0, $"{plan.ChunkStored.Count} new chunks in the chunk table");
        foreach (var (place, oldRaw, newRaw, newMeta, nb) in brt.Copies)
        {
            uint rs = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(newMeta);
            
            var t = BundleRefTable.Read(newRaw, rs);
            var probs = t.Check();
            Check(probs.Count == 0 && rs + System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(newMeta.AsSpan(4)) == newRaw.Length, $"player_team_brt at {place} ({nb} bundles): rebuilt table reads back, keys sorted and hashed{(probs.Count > 0 ? ": " + string.Join("; ", probs) : "")}");
            var keys = Enumerable.Range(0, t.Keys).Select(t.Key).ToList();
            foreach (var c in all)
            {
                string from = c.Folder(c.FromWord) + "/", to = c.Folder(c.ToWord) + "/";
                int want = keys.Count(k => t.AssetName(k.Asset).StartsWith(from) && t.BundleName(k.Bundle).StartsWith(from));
                var mine = keys.Where(k => t.AssetName(k.Asset).StartsWith(to)).ToList();
                bool ok = mine.Count == want && want > 0 && mine.All(k => t.BundleName(k.Bundle).StartsWith(to) && (k.Hash == BundleRefTable.Fnv64(t.AssetName(k.Asset)) || k.Hash == BundleRefTable.Fnv64(t.AssetName(k.Asset)[(t.AssetName(k.Asset).LastIndexOf('/') + 1)..])));
                // every new bundle the table names is one the TOC lists
                var named = mine.Select(k => "gen4b/" + t.BundleName(k.Bundle) + "_player_team_brt").Distinct().ToList();
                ok &= named.All(byName.ContainsKey);
                Check(ok, $"  {c.Folder(c.ToWord)}: {mine.Count} keys (the donor has {want}) point at {named.Count} of its bundles, all in the TOC");
            }
        }
        foreach (var (file, bytes, lang, taken) in locFiles)
        {
            var ndb = new T3Db(bytes, System.Text.Encoding.UTF8.GetString(legacy.Read(lang[..^3] + "-meta.xml")).TrimStart('﻿'));
            var (lo, la) = ndb.CheckCrcs();
            var rows = LocDb.ReadAll(ndb);
            var got = KitNames.Read(ndb);
            bool sorted = rows.Zip(rows.Skip(1)).All(x => x.First.Hash <= x.Second.Hash) && rows.Where(r => r.Id.StartsWith("vintage_", StringComparison.OrdinalIgnoreCase)).All(r => r.Hash == LocDb.Hash(r.Id));
            bool named = nameOf.All(kv => got.TryGetValue(kv.Key.ToLowerInvariant(), out var r) && r.Text == (kv.Value.ByLang.GetValueOrDefault(lang) ?? kv.Value.En));
            Check(lo == la && sorted && named && rows.Count <= 65535, $"{lang}: CRCs {lo}/{la}, {rows.Count:N0} rows sorted by their key hashes, {nameOf.Count} kit name(s) read back{(sorted ? "" : " [order/hash: " + string.Join(", ", rows.Where(r => r.Hash != LocDb.Hash(r.Id)).Take(3).Select(r => r.Id)) + $" unsorted {rows.Zip(rows.Skip(1)).Count(x => x.First.Hash > x.Second.Hash)}]")}{(named ? "" : " [names: " + string.Join(", ", nameOf.Select(kv => got.TryGetValue(kv.Key.ToLowerInvariant(), out var r) ? r.Text : "none")) + "]")}");
        }
        var (crcOk, crcAll) = db.CheckCrcs();
        Check(crcOk == crcAll, $"nhl_game.db CRCs {crcOk}/{crcAll}");
        foreach (var k in kits)
            Check(JerseySlots.Read(db, k.TeamId).Any(s => s.Style == 2 && s.Variant == k.ToVariant), $"team {k.TeamId} ({k.Team}) has a slot for {KitAlternates.Words[k.ToVariant]}");
        plan.Notes.ForEach(n => Console.WriteLine("  " + n));
        if (bad > 0) { Console.WriteLine($"{bad} check(s) failed; nothing written."); return 1; }
        if (!opts.Has("apply")) { Console.WriteLine("not written (pass --apply)"); return 0; }

        RefuseRunning();
        var notes = new List<string>();
        var patches = new List<PatchData>(plan.Patches);
        var legacyFiles = new List<(LegacyFile, byte[])> { (db.Source, db.Data) };
        legacyFiles.AddRange(locFiles.Select(f => (f.File, f.Data)));
        patches.AddRange(LegacyEditor.PlanPs4(index, legacy, legacyFiles, notes, Console.WriteLine, grow));
        foreach (var g in patches.Where(x => x.Append).GroupBy(x => x.File, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  appends to {g.Key}: {string.Join(", ", g.Select(x => $"0x{x.Offset:X} +{x.New.Length:N0} ({x.Note})"))}");
        // appends to one file never overlap (each part of the change starts where the one before it ends)
        foreach (var g in patches.Where(x => x.Append).GroupBy(x => x.File, StringComparer.OrdinalIgnoreCase))
        {
            var spans = g.Select(x => (Start: x.Offset, End: x.Offset + x.New.Length)).OrderBy(x => x.Start).ToList();
            if (spans.Zip(spans.Skip(1)).Any(x => x.Second.Start < x.First.End))
                throw new InvalidDataException($"Appends to {g.Key} overlap: {string.Join(", ", spans.Select(x => $"0x{x.Start:X}-0x{x.End:X}"))}; nothing written.");
        }
        if (existing != null) store.Remove(existing);
        var entry = store.Add(KitAlternates.Kind, entryName, string.Join(" ", opts.All("kit")),
            $"{kits.Count} kit(s) added as new kit folders: {string.Join(", ", kits.Select(k => $"{k.Team} {KitAlternates.Words[k.ToVariant]}"))}", patches);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"written: entry {entry.Id}, {store.State(entry, game)}");

        // read back through a rebuilt index
        index = OpenIndex(game, true);
        legacy = new Legacy(index);
        var db2 = legacy.OpenDb(JerseySlots.Database);
        foreach (var (c, k) in copies.Zip(kits))
        {
            int n = KitClone.FolderBundles(index, c.Folder(c.ToWord)).Count, want = KitClone.FolderBundles(index, c.Folder(c.FromWord)).Count;
            Check(n == want, $"{c.Folder(c.ToWord)}: {n} of {want} bundles in the game");
            string tex = $"{c.Folder(c.ToWord)}/jersey_adidas_{c.Team}_{c.ToWord}_base_color";
            try { var t = Texture.Load(index, tex); Check(t.Width > 0 && t.Problem == null, $"{tex}: {t.Width}x{t.Height} read back"); }
            catch (Exception e) { Check(false, $"{tex}: {e.Message}"); }
            Check(JerseySlots.Read(db2, k.TeamId).Any(s => s.Style == 2 && s.Variant == k.ToVariant), $"team {k.TeamId} slot {KitAlternates.Words[k.ToVariant]} read back");
        }
        return bad > 0 ? 1 : 0;

        static void RefuseRunning()
        {
            if (Emulators.Running() is { } run) throw new InvalidOperationException($"{run} is running.");
            if (new[] { "HMS", "NHL27Kit", "nhl24", "nhl24_new", "shadPS4", "kyty_emulator" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId))
                throw new InvalidOperationException("The game, an emulator or HMS is running.");
        }
    }
}
