using NHL27Kit.Core;

namespace NHL27Kit.Cli;

public static partial class Program
{
    /// <summary>loc-test [DB]: writes a language database again with no changes and checks it reads back the same.</summary>
    private static int LocTestCmd(string game, Options opts)
    {
        var legacy = new Legacy(OpenIndex(game, false));
        string name = opts.Positional.FirstOrDefault() ?? LocDb.English;
        var db = legacy.OpenDb(name);
        var rows = LocDb.ReadAll(db);
        var bytes = LocDb.Write(db, rows);
        Console.WriteLine($"{name}: {rows.Count:N0} rows, {db.Data.Length:N0} bytes; written again {bytes.Length:N0} bytes, {(bytes.AsSpan().SequenceEqual(db.Data) ? "byte-identical" : "different bytes")}");
        var back = new T3Db(bytes, System.Text.Encoding.UTF8.GetString(legacy.Read(name[..^3] + "-meta.xml")).TrimStart('﻿'));
        var (ok, total) = back.CheckCrcs();
        var rows2 = LocDb.ReadAll(back);
        int same = rows.OrderBy(r => r.Hash).ThenBy(r => r.Id, StringComparer.Ordinal).Zip(rows2).Count(x => x.First == x.Second);
        Console.WriteLine($"  read back: CRCs {ok}/{total}, {same:N0}/{rows.Count:N0} rows the same, hashes ok {rows2.Count(r => LocDb.Hash(r.Id) == r.Hash):N0}");
        return 0;
    }

    /// <summary>
    /// league PACK.json [--conf bigten,he] [--name NCAA] [--id 29] [--apply] [--verify] [--restore]: NHL 24 custom
    /// leagues. Without a flag, plans from the game's files (nothing written) and lists the teams; --apply writes it
    /// in as one mod entry (restoring an earlier one first); --verify reads the game again and checks it; --restore
    /// takes it out.
    /// </summary>
    private static int LeagueCmd(string game, Options opts)
    {
        string path = opts.Positional.FirstOrDefault() ?? Path.Combine(KitPaths.Root, "leagues", "ncaa", "ncaa_leagues.json");
        var pack = LeaguePack.Load(path);
        var o = new LeagueOptions
        {
            Conferences = (opts.Get("conf") ?? string.Join(",", pack.Leagues.Select(l => l.Key))).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            LeagueName = opts.Get("name") ?? "NCAA",
            LeagueId = int.TryParse(opts.Get("id"), out int id) ? id : 29,
        };
        var store = Mods();
        if (opts.Has("restore"))
        {
            var e = store.Find(CustomLeague.Kind, LeagueInstaller.EntryName(o)) ?? throw new ArgumentException($"No \"{LeagueInstaller.EntryName(o)}\" entry.");
            if (Emulators.Running() is { } run) throw new InvalidOperationException($"{run} is running.");
            store.Restore(e, game, Console.WriteLine);
            Console.WriteLine($"restored: {store.State(e, game)}");
            return 0;
        }
        var index = OpenIndex(game, false);
        bool write = opts.Has("apply");
        LeagueResult r = null;
        if (write || !opts.Has("verify"))
        {
            r = LeagueInstaller.Install(index, store, game, pack, Path.GetFullPath(path), o, write, Console.WriteLine);
            Console.WriteLine(r.Summary);
            foreach (var p in r.Teams)
                Console.WriteLine($"  {p.Slot,4}{(p.Added ? "+" : " ")} {p.ArtAbbr,-8} {p.Team.FullName,-34} {p.Team.Players.Count,2} players  template {p.TemplateName} ({p.TemplateIndex}), art {p.ArtId}");
            foreach (var l in r.Log) Console.WriteLine("  " + l);
            Console.WriteLine($"  loc rows taken over: {string.Join(", ", r.TakenOverKeys.Take(12))}{(r.TakenOverKeys.Count > 12 ? ", ..." : "")}");
            if (write) Console.WriteLine($"written: entry {r.Entry.Id}, {r.Entry.Patches.Count} ranges, {store.State(r.Entry, game)}");
            else
            {
                Console.WriteLine("checks on the edited files (not written):");
                foreach (var c in LeagueInstaller.Verify(r.Game, r.Initial, r.Loc, r.Teams, o)) Console.WriteLine($"  {(c.Ok ? "ok " : "BAD")} {c.What}");
            }
        }
        if (write || opts.Has("verify"))
        {
            var plans = r?.Teams;
            if (plans == null)
            {
                // the teams as installed, found by their art abbreviations
                var legacy = new Legacy(index);
                var (g, ie, _) = CustomLeague.Open(legacy);
                var tt = g.Table(Roster.TeamsTable);
                var f = tt.Fields.ToDictionary(x => x.Name);
                var rows = Enumerable.Range(0, tt.WrittenRecords).Where(x => !g.IsInvalid(tt, x))
                    .Select(x => (Index: (int)g.Get(tt, x, f["index"]), Abbr: (string)g.Get(tt, x, f["artabbr"]), Art: (int)g.Get(tt, x, f["artid"]))).ToList();
                var teams = o.Conferences.Select(k => pack.Conference(k)).SelectMany(c => c.Teams.Where(t => t.Players.Count > 0).Select(t => (c, t))).ToList();
                plans = teams.Select(x =>
                {
                    var row = rows.First(b => b.Abbr == CustomLeague.ArtAbbr(x.t));
                    return new LeagueTeamPlan(x.c, x.t, row.Index, -1, "", CustomLeague.ArtAbbr(x.t), 0, false, row.Art >= LeagueLogos.FirstArtId && row.Art < 4000 ? row.Art : -1);
                }).ToList();
            }
            int bad = 0;
            foreach (var c in LeagueInstaller.Verify(index, plans, o, game))
            {
                Console.WriteLine($"  {(c.Ok ? "ok " : "BAD")} {c.What}");
                if (!c.Ok) bad++;
            }
            Console.WriteLine(bad == 0 ? "verify: all checks pass" : $"verify: {bad} problem(s)");
            return bad == 0 ? 0 : 1;
        }
        return 0;
    }
    /// <summary>league-probe: team tables of both roster databases (sizes, key fields, sort orders, free team indices).</summary>
    private static int LeagueProbeCmd(string game, Options opts)
    {
        var legacy = new Legacy(OpenIndex(game, false));
        var (g, ie, _) = CustomLeague.Open(legacy);
        foreach (var (db, name) in new[] { (g, "game"), (ie, "initial") })
        {
            Console.WriteLine($"== {name}: {db.Tables.Count} tables");
            foreach (var t in db.Tables.OrderBy(t => t.Name))
            {
                var keys = t.Fields.Where(f => f.Name is "index" or "team" or "teamid" or "key" or "arenaid" or "leagueid").ToList();
                if (keys.Count == 0) continue;
                var parts = new List<string>();
                foreach (var f in keys)
                {
                    var vals = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r)).Select(r => (int)db.Get(t, r, f)).ToList();
                    bool sorted = vals.Zip(vals.Skip(1)).All(x => x.First <= x.Second);
                    parts.Add($"{f.Name}/{f.Depth}[{f.RangeLow}] {(vals.Count > 0 ? vals.Min() : 0)}..{(vals.Count > 0 ? vals.Max() : 0)}{(sorted ? " sorted" : "")}");
                }
                Console.WriteLine($"  {t.Name,-34} {t.WrittenRecords,6}/{t.Records,-6} {string.Join("; ", parts)}");
            }
        }
        var used = new HashSet<int>();
        foreach (var db in new[] { g, ie })
            foreach (var tn in new[] { Roster.TeamsTable, "customteams", "exhibitionteamstrategies", "customonlineteamstrategies", "exhibitionarena", "stockteamjerseys" })
                if (db.Table(tn) is { } t)
                {
                    var f = t.Fields.FirstOrDefault(x => x.Name is "index" or "teamid");
                    if (f != null) for (int r = 0; r < t.WrittenRecords; r++) if (!db.IsInvalid(t, r)) used.Add((int)db.Get(t, r, f));
                }
        var free = Enumerable.Range(0, 1024).Where(i => !used.Contains(i)).ToList();
        Console.WriteLine($"free team indices ({free.Count}): {string.Join(",", free)}");
        var (g2, ie2, loc2) = CustomLeague.Open(legacy);
        var spare = CustomLeague.SpareKeys(loc2, new[] { g2, ie2 }.Concat(CustomLeague.OpenOthers(legacy)));
        var texts = LocDb.ReadAll(loc2).GroupBy(r => r.Id).ToDictionary(x => x.Key, x => x.First().Text);
        var abbrs = spare.Select(k => System.Text.RegularExpressions.Regex.Replace(k, "^(NHLTeamName_Abbr3_|NHLTeamName_|NHLCityName_|TXT_NICKNAME_ALT_|TXT_NICKNAME_|TEAMLINE1_|TEAMLINE2_|X_XLA_TEAM_)", "")).Distinct().ToList();
        Console.WriteLine($"spare keys: {spare.Count}, {abbrs.Count} abbreviations");
        foreach (var a in abbrs)
        {
            var ks = spare.Where(k => k.EndsWith("_" + a)).ToList();
            Console.WriteLine($"  {a,-10} {ks.Count} keys  e.g. {string.Join(" / ", ks.Take(3).Select(k => texts.GetValueOrDefault(k)))}");
        }
        return 0;
    }
    /// <summary>
    /// league-logo PNG [--out DIR] [--stock NAME.gnf]: builds every logo style's GNF of one PNG and decodes each back to a
    /// PNG (and a contact sheet), to check the GNF writer; --stock decodes one of the game's kettle GNFs the same way.
    /// </summary>
    private static int LeagueLogoCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string outDir = opts.Get("out") ?? Path.Combine(KitPaths.Root, "export", "league-logos");
        Directory.CreateDirectory(outDir);
        static (byte[], int, int) DecodeGnf(byte[] g)
        {
            uint w2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(g.AsSpan(0x18));
            int w = (int)(w2 & 0x3FFF) + 1, h = (int)((w2 >> 14) & 0x3FFF) + 1;
            int bw = (w + 3) / 4, bh = (h + 3) / 4;
            var blocks = Ps4Tiling.Untile(g.AsSpan(0x100), bw, bh, 16);
            return (BcDecoder.Decode(blocks, w, h, TextureFormat.Of(0x43)), w, h);
        }
        if (opts.Get("stock") is { } stock)
        {
            var legacy = new Legacy(index, "globals/chunkfilecollectoruikettle_swfassets");
            var g = legacy.Read(legacy.Find(stock) ?? throw new ArgumentException($"no {stock}"));
            var (px, w, h) = DecodeGnf(g);
            string f = Path.Combine(outDir, "stock_" + Path.GetFileNameWithoutExtension(stock) + ".png");
            File.WriteAllBytes(f, ImageFiles.Png(px, w, h));
            Console.WriteLine($"{stock}: {w}x{h} -> {f}");
            return 0;
        }
        string png = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("league-logo needs a PNG");
        var (rgba, sw, sh) = PngReader.Load(png);
        var header = LeagueLogos.TemplateHeader(index);
        var sheetW = 6 * 260; var sheetH = 4 * 260;
        var sheet = new byte[sheetW * sheetH * 4];
        for (int i = 0; i < sheet.Length; i += 4) { sheet[i] = 70; sheet[i + 1] = 74; sheet[i + 2] = 80; sheet[i + 3] = 255; }
        int n = 0;
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        foreach (var st in LeagueLogos.Styles)
        {
            var gnf = LeagueLogos.Gnf(header, LeagueLogos.Draw(rgba, sw, sh, st.W, st.H, st.Look), st.W, st.H);
            var (px, w, h) = DecodeGnf(gnf);
            if (w != st.W || h != st.H) throw new InvalidDataException($"{st.Style}: header says {w}x{h}");
            File.WriteAllBytes(Path.Combine(outDir, st.Style.Replace('/', '_') + ".png"), ImageFiles.Png(px, w, h));
            // into the sheet, shrunk to fit 256
            int cx = n % 6 * 260 + 2, cy = n / 6 * 260 + 2; n++;
            double k = Math.Max(1.0, Math.Max(w, h) / 256.0);
            for (int y = 0; y < (int)(h / k); y++)
                for (int x = 0; x < (int)(w / k); x++)
                {
                    int si = ((int)(y * k) * w + (int)(x * k)) * 4, di = ((cy + y) * sheetW + cx + x) * 4;
                    int a = px[si + 3];
                    for (int c = 0; c < 3; c++) sheet[di + c] = (byte)((px[si + c] * a + sheet[di + c] * (255 - a)) / 255);
                }
            Console.WriteLine($"  {st.Style,-22} {w}x{h} {gnf.Length,9:N0} bytes");
        }
        string sheetFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(png) + "_sheet.png");
        File.WriteAllBytes(sheetFile, ImageFiles.Png(sheet, sheetW, sheetH));
        Console.WriteLine($"{LeagueLogos.Styles.Length} styles in {sw2.ElapsedMilliseconds} ms -> {sheetFile}");
        return 0;
    }
    /// <summary>
    /// league-tex [--id 29] [--png FILE] [--apply] [--restore] [--verify]: the league's own logo textures, added as new
    /// bundles to the patch contentlaunchsb TOC. Without flags: plan and check, nothing written.
    /// </summary>
    private static int LeagueTexCmd(string game, Options opts)
    {
        int id = int.TryParse(opts.Get("id"), out int n) ? n : 29;
        string png = opts.Get("png") ?? Path.Combine(KitPaths.Root, "leagues", "ncaa", "logos", "leagues", "ncaa.png");
        string entryName = $"league {id} logo";
        const string kind = "texture";
        var store = Mods();
        string tocPath = Path.Combine(game, "Patch", "Gen4b", "contentlaunchsb.toc");
        string outDir = opts.Get("out") ?? Path.Combine(KitPaths.Root, "previews", "league-tex");
        if (opts.Has("restore"))
        {
            var e = store.Find(kind, entryName) ?? throw new ArgumentException($"No \"{entryName}\" entry.");
            if (Emulators.Running() is { } run) throw new InvalidOperationException($"{run} is running.");
            store.Restore(e, game, Console.WriteLine);
            Console.WriteLine($"restored: {store.State(e, game)}");
            return 0;
        }
        var index = OpenIndex(game, false);
        int bad = 0;
        void Check(bool ok, string what) { Console.WriteLine($"  {(ok ? "ok " : "BAD")} {what}"); if (!ok) bad++; }
        var tocBytes = File.ReadAllBytes(tocPath);
        var existing = store.Find(kind, entryName);
        if (existing != null && store.State(existing, game) != ModState.NotApplied && !opts.Has("verify"))
        {
            Console.WriteLine($"\"{entryName}\" is {store.State(existing, game)}; restore it first to rebuild.");
            return 1;
        }

        if (!opts.Has("verify"))
        {
            Console.WriteLine("checks on the TOC as it is:");
            var t0 = TocAdder.Toc.Read(tocBytes);
            Check(t0.Check().Count == 0, $"all {t0.N:N0} bundle names and {t0.M:N0} chunk guids found through the hash maps");
            var p0 = new SuperBundleToc(tocPath, tocBytes, index.Layout.TocCatalogs);
            var rebuilt = TocAdder.Plan("x", tocBytes, Array.Empty<AddedBundle>());
            var p1 = new SuperBundleToc(tocPath, rebuilt.After, index.Layout.TocCatalogs);
            Check(TocAdder.Toc.Read(rebuilt.After).Check().Count == 0, "rebuilt with nothing added: every name and guid found through the new maps");
            Check(SameToc(p0, p1, out string why), "rebuilt with nothing added: same bundles, entries and chunks " + why);

            // a copy of l20 named l24, given l24's own ids, must come out as l24's own files
            var l20 = LeagueLogos.LeagueTexture("size256", 20);
            var l24 = LeagueLogos.LeagueTexture("size256", 24);
            var real = index.BundleList.First(b => b.Name == AddedTextures.BundleOf(l24));
            var srcB = index.BundleList.First(b => b.Name == AddedTextures.BundleOf(l20));
            var realRec = BundleRecord.Read(index.ReadStored(real.Record), 0);
            var srcRec = BundleRecord.Read(index.ReadStored(srcB.Record), 0);
            var baseToc = new SuperBundleToc(real.Toc, File.ReadAllBytes(real.Toc));
            var realRaw = Enumerable.Range(1, 5).Select(j => FrostbiteBlocks.Decode(index.ReadStored(baseToc.Bundles[real.BundleIndex].Entries[j]))).ToList();
            var srcRaw = Enumerable.Range(1, 5).Select(j => FrostbiteBlocks.Decode(index.ReadStored(baseToc.Bundles[srcB.BundleIndex].Entries[j]))).ToList();
            var map = new Dictionary<Guid, Guid>();
            for (int j = 0; j < 3; j++)
            {
                string want = srcRec.Ebx[j].Name.Replace("/l20", "/l24");
                int k = realRec.Ebx.FindIndex(x => x.Name == want);
                map[EbxFile.Parse(srcRaw[j]).FileGuid] = EbxFile.Parse(realRaw[k]).FileGuid;
                map[new Guid(srcRaw[j].AsSpan(0x20, 16))] = new Guid(realRaw[k].AsSpan(0x20, 16));
            }
            var tocs = new Dictionary<string, SuperBundleToc>(StringComparer.OrdinalIgnoreCase) { [real.Toc] = baseToc };
            var test = AddedTextures.Clone(index, new TextureClone(l20, l24, null), tocs, g => map.TryGetValue(g, out var x) ? x : g, realRec.Res[0].ResRid, realRec.Chunks[0].Id);
            var testRec = BundleRecord.Read(test.Record, 0);
            bool ebxSame = true;
            for (int j = 0; j < 3; j++)
            {
                int k = realRec.Ebx.FindIndex(x => x.Name == testRec.Ebx[j].Name);
                ebxSame &= k >= 0 && test.Raw[j].AsSpan().SequenceEqual(realRaw[k]) && testRec.Ebx[j].Size == realRec.Ebx[k].Size;
            }
            Check(ebxSame, "l20 copied as l24: its three ebx files are byte for byte l24's");
            Check(test.Raw[3].AsSpan().SequenceEqual(realRaw[3]), "l20 copied as l24: its texture header is byte for byte l24's");
            Check(testRec.Res[0].Name == realRec.Res[0].Name && testRec.Res[0].ResRid == realRec.Res[0].ResRid && testRec.Res[0].ResMeta.AsSpan().SequenceEqual(realRec.Res[0].ResMeta) &&
                  testRec.Chunks[0].Id == realRec.Chunks[0].Id && testRec.Chunks[0].Size == realRec.Chunks[0].Size, "l20 copied as l24: the record's res and chunk are l24's");
            var realRecBytes = index.ReadStored(real.Record);
            int metaOff = BitConverter.ToInt32(realRecBytes, 28) + 4;
            Check(test.Record.Length == realRecBytes.Length && test.Record.AsSpan(metaOff).SequenceEqual(realRecBytes.AsSpan(metaOff)), "l20 copied as l24: the record's chunk meta is l24's");

            Console.WriteLine($"building the league {id} logo from {png}");
            var clones = LeagueLogos.LeagueClones(index, id, png);
            Directory.CreateDirectory(outDir);
            foreach (var c in clones)
            {
                var h = TextureHeader.Parse(index.Read(index.Res[c.From]), ps4: true);
                File.WriteAllBytes(Path.Combine(outDir, c.To.Replace('/', '_') + ".png"), ImageFiles.Png(c.Rgba, h.Width, h.Height));
            }
            var plan = AddedTextures.Build(index, tocPath, clones, Console.WriteLine);
            plan.Notes.ForEach(x => Console.WriteLine("  " + x));
            Console.WriteLine("checks on the planned TOC:");
            var after = plan.TocAfter;
            var ta = TocAdder.Toc.Read(after);
            Check(ta.Check().Count == 0, $"all {ta.N:N0} bundle names and {ta.M:N0} chunk guids found through the hash maps");
            var pa = new SuperBundleToc(tocPath, after, index.Layout.TocCatalogs);
            var byName = pa.Bundles.ToDictionary(b => b.Name);
            Check(p0.Bundles.All(b => byName.TryGetValue(b.Name, out var x) && x.Entries.SequenceEqual(b.Entries)), $"every old bundle ({p0.Bundles.Count:N0}) is there with the same entries");
            var chunksA = pa.Chunks.ToDictionary(c => c.Id);
            Check(p0.Chunks.All(c => chunksA.TryGetValue(c.Id, out var x) && x.Ref == c.Ref), $"every old chunk ({p0.Chunks.Count:N0}) is there at the same place");
            foreach (var c in plan.Copies)
                Check(byName.TryGetValue(c.Bundle, out var nb) && nb.Entries.Count == 6 && nb.Entries.All(e => e.Patch) && chunksA.ContainsKey(c.ChunkId) &&
                      nb.Entries.Skip(1).Select(e => e.Size).SequenceEqual(c.Stored.Select(s => (uint)s.Length)) && chunksA[c.ChunkId].Ref.Offset == nb.Entries[5].Offset,
                      $"{c.Bundle}: listed, 6 entries in the patch CAS, its chunk in the chunk table");
            // no other mod may patch what this rewrites or copies
            int clashes = 0;
            foreach (var e in store.Entries.Where(e => e.Kind != kind || e.Name != entryName))
                foreach (var p in e.Patches.Where(p => p.File.Equals(plan.TocRel, StringComparison.OrdinalIgnoreCase)))
                    foreach (var o in plan.Owned)
                        if (p.Offset < o.End && p.Offset + p.Length > o.Start) { clashes++; Check(false, $"{e.Name} patches 0x{p.Offset:X} in the TOC's {o.What}"); }
            Check(clashes == 0, "no other mod patches the TOC ranges this rewrites or copies");
            Console.WriteLine($"  {plan.Patches.Count} ranges: {string.Join(", ", plan.Patches.Select(p => $"{p.Note} {p.New.Length:N0}"))}");
            if (bad > 0) { Console.WriteLine($"{bad} check(s) failed; nothing written."); return 1; }
            if (!opts.Has("apply")) { Console.WriteLine("not written (pass --apply)"); return 0; }
            if (Emulators.Running() is { } run) throw new InvalidOperationException($"{run} is running.");
            if (new[] { "HMS", "NHL27Kit", "nhl24", "nhl24_new", "shadPS4", "kyty_emulator" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId))
                throw new InvalidOperationException("The game, an emulator or HMS is running.");
            if (existing != null) store.Remove(existing);
            var entry = store.Add(kind, entryName, png, $"League {id}'s logo: {clones.Count} new textures in the patch contentlaunchsb TOC", plan.Patches);
            store.Apply(entry, game, Console.WriteLine);
            Console.WriteLine($"written: entry {entry.Id}, {store.State(entry, game)}");
            index = OpenIndex(game, false);
        }

        Console.WriteLine("checks on the game files:");
        Check(TocAdder.Toc.Read(File.ReadAllBytes(tocPath)).Check().Count == 0, "the TOC's hash maps find every name and guid");
        Directory.CreateDirectory(outDir);
        foreach (var (variant, _) in LeagueLogos.LeagueVariants)
        {
            string name = LeagueLogos.LeagueTexture(variant, id);
            try
            {
                var t = Texture.Load(index, name);
                File.WriteAllBytes(Path.Combine(outDir, "game_" + name.Replace('/', '_') + ".png"), t.ToPng());
                Check(t.Width > 0, $"{name}: read back from the game, {t.Width}x{t.Height}");
            }
            catch (Exception ex) { Check(false, $"{name}: {ex.Message}"); }
        }
        return bad > 0 ? 1 : 0;
    }

    private static bool SameToc(SuperBundleToc a, SuperBundleToc b, out string why)
    {
        why = "";
        var bn = b.Bundles.ToDictionary(x => x.Name);
        foreach (var x in a.Bundles)
            if (!bn.TryGetValue(x.Name, out var y) || !x.Entries.SequenceEqual(y.Entries)) { why = $"({x.Name} differs)"; return false; }
        var bc = b.Chunks.ToDictionary(x => x.Id);
        foreach (var c in a.Chunks)
            if (!bc.TryGetValue(c.Id, out var d) || d.Ref != c.Ref) { why = $"(chunk {c.Id} differs)"; return false; }
        if (a.Bundles.Count != b.Bundles.Count || a.Chunks.Count != b.Chunks.Count) { why = "(counts differ)"; return false; }
        return true;
    }

    /// <summary>bundle-dump NAME --out DIR: a bundle's record and each asset (stored and decoded) as files, plus its TOC entries.</summary>
    private static int BundleDumpCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("bundle-dump NAME");
        string dir = opts.Get("out") ?? "dump";
        Directory.CreateDirectory(dir);
        var b = index.BundleList.First(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var toc = new SuperBundleToc(b.Toc, File.ReadAllBytes(b.Toc));
        var tb = toc.Bundles[b.BundleIndex];
        var lines = new List<string> { $"{b.Name} toc {b.Toc} #{b.BundleIndex}" };
        int k = 0;
        foreach (var r in tb.Entries)
        {
            var stored = index.ReadStored(r);
            File.WriteAllBytes(Path.Combine(dir, $"e{k}.stored"), stored);
            if (k > 0) File.WriteAllBytes(Path.Combine(dir, $"e{k}.raw"), FrostbiteBlocks.Decode(stored));
            lines.Add($"e{k} cat {r.Catalog} cas {r.Cas} patch {r.Patch} off 0x{r.Offset:X} size {r.Size} at {r.OffsetPos}  {index.Layout.CasPath(r.Catalog, r.Cas, r.Patch)}");
            k++;
        }
        foreach (var c in BundleRecord.Read(index.ReadStored(b.Record), b.Record.Offset).Chunks)
            lines.Add($"chunk {c.Id} in table: {index.TableChunks.ContainsKey(c.Id)}");
        File.WriteAllLines(Path.Combine(dir, "entries.txt"), lines);
        lines.ForEach(Console.WriteLine);
        return 0;
    }

    /// <summary>bundle-probe FILTER: bundles whose name contains FILTER, with their TOC, record and assets.</summary>
    private static int BundleProbeCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "";
        int limit = int.TryParse(opts.Get("limit"), out int n) ? n : 20;
        if (opts.Get("names") is { } tocFile)
        {
            // the bundle names of one TOC, in its order, one per line
            var toc = new SuperBundleToc(tocFile, File.ReadAllBytes(tocFile));
            File.WriteAllLines(opts.Get("out") ?? "names.txt", toc.Bundles.Select(x => x.Name));
            Console.WriteLine($"{toc.Bundles.Count} names");
            return 0;
        }
        if (opts.Has("study"))
        {
            // patch-only bundles of a TOC, and whether single-texture bundles' chunks are in a chunk table
            string pt = Path.Combine(game, "Patch", "Gen4b", "contentlaunchsb.toc"), bt = Path.Combine(game, "..", "game", "Data", "Gen4b", "contentlaunchsb.toc");
            var baseNames = new SuperBundleToc(bt, File.ReadAllBytes(bt)).Bundles.Select(x => x.Name).ToHashSet();
            var patch = new SuperBundleToc(pt, File.ReadAllBytes(pt));
            var only = patch.Bundles.Where(x => !baseNames.Contains(x.Name)).ToList();
            Console.WriteLine($"patch bundles {patch.Bundles.Count}, patch-only {only.Count}");
            foreach (var x in only.Take(limit)) Console.WriteLine($"  only #{x.Index} {x.Name} entries {x.Entries.Count}");
            var patchTable = patch.Chunks.Select(c => c.Id).ToHashSet();
            int inAny = 0, inPatch = 0, none = 0, tex = 0;
            var byName = index.BundleList.Where(x => string.Equals(x.Toc, pt, StringComparison.OrdinalIgnoreCase) || x.Toc.EndsWith("contentlaunchsb.toc", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var x in byName.Where(x => x.Name.EndsWith("_brt")))
            {
                var rec = BundleRecord.Read(index.ReadStored(x.Record), x.Record.Offset);
                if (rec.Chunks.Count != 1 || rec.Res.Count != 1) continue;
                tex++;
                var id = rec.Chunks[0].Id;
                bool any = index.TableChunks.ContainsKey(id);
                if (any) inAny++; else { none++; if (none <= 5) Console.WriteLine($"  not in a table: {x.Name} ({Path.GetFileName(Path.GetDirectoryName(x.Toc))}/{Path.GetFileName(x.Toc)})"); }
                if (patchTable.Contains(id)) inPatch++;
            }
            Console.WriteLine($"single-texture brt bundles {tex}: chunk in some table {inAny}, in the patch table {inPatch}, in none {none}");
            foreach (var x in only.Where(x => x.Name.EndsWith("_brt")).Take(5))
            {
                var bi = index.BundleList.FirstOrDefault(y => y.Name == x.Name);
                if (bi == null) continue;
                var rec = BundleRecord.Read(index.ReadStored(bi.Record), bi.Record.Offset);
                Console.WriteLine($"  only-brt {x.Name}: ebx {rec.Ebx.Count} res {rec.Res.Count} chunks {rec.Chunks.Count} in table {rec.Chunks.Select(c => index.TableChunks.ContainsKey(c.Id) + (patchTable.Contains(c.Id) ? "(patch)" : "")).FirstOrDefault()}");
            }
            return 0;
        }
        var hits = index.BundleList.Where(b => b.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"{hits.Count} bundles match");
        foreach (var b in hits.Take(limit))
        {
            Console.WriteLine($"{b.Name}  toc {Path.GetFileName(b.Toc)} #{b.BundleIndex} record {b.Record.Size} bytes cat {b.Record.Catalog} cas {b.Record.Cas} patch {b.Record.Patch}");
            if (opts.Has("assets"))
            {
                var rec = BundleRecord.Read(index.ReadStored(b.Record), b.Record.Offset);
                foreach (var e in rec.Ebx) Console.WriteLine($"   ebx   {e.Name} {e.Size}");
                foreach (var e in rec.Res) Console.WriteLine($"   res   {e.Name} {e.Size} type {e.ResType:X8} rid {e.ResRid:X}");
                foreach (var e in rec.Chunks) Console.WriteLine($"   chunk {e.Id} {e.Size} logical {e.LogicalOffset}");
            }
        }
        var tocs = hits.GroupBy(b => b.Toc).Select(g => $"{Path.GetFileName(g.Key)} x{g.Count()}");
        Console.WriteLine("tocs: " + string.Join(", ", tocs));
        return 0;
    }
}
