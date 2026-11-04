using NHL27Kit.Core;

namespace NHL27Kit.Cli;

/// <summary>
/// nhl27kit &lt;command&gt; [--game DIR] ...
///   info                      layout, superbundles, install chunks and TOC sizes
///   index                     build (or load) the asset index and print counts by EBX type
///   list [ebx|res|chunk] [--filter TEXT] [--type TYPE] [--limit N]
///   export [ebx|res|chunk] NAME|GUID [--out DIR]   write an asset's decompressed bytes
/// The game folder defaults to "NHL 27 dump/NHL" next to the kit.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Usage();
            return 0;
        }
        var opts = Options.Parse(args.Skip(1).ToArray());
        string game = opts.Get("game") ?? KitPaths.FindGame();
        if (game == null || !File.Exists(Path.Combine(game, "Data", "layout.toc")))
        {
            Console.Error.WriteLine($"No game folder (with Data/layout.toc) found{(game != null ? " at " + game : "")}. Pass --game DIR.");
            return 2;
        }
        _profile = GameProfile.Detect(game);
        try
        {
            return args[0] switch
            {
                "info" => Info(game),
                "index" => Index(game, opts),
                "list" => List(game, opts),
                "export" => Export(game, opts),
                "texture" => TextureCmd(game, opts),
                "legacy-jersey" => LegacyJerseyCmd(opts),
                "copies" => CopiesCmd(game, opts),
                "dbtest" => DbTestCmd(game, opts),
                "mesh" => MeshCmd(game, opts),
                "mesh-blend" => MeshBlendCmd(game, opts),
                "idle" => IdleCmd(game, opts),
                "kits" => KitsCmd(game, opts),
                "kit-stamp" => KitStampCmd(game, opts),
                "jersey-studio" => JerseyStudioCmd(game, opts),
                "faceoff" => FaceoffCmd(opts),
                "kit-gear" => KitGearCmd(game, opts),
                "kit-helmet" => KitHelmetCmd(game, opts),
                "own-kit" => OwnKitCmd(game, opts),
                "jersey-template" => JerseyTemplateCmd(game, opts),
                "collar" => CollarCmd(game, opts),
                "tunables" => TunablesCmd(game, opts),
                "bap" => BeAProCmd(opts),
                "arena" => ArenaCmd(game, opts),
                "ebx-probe" => EbxProbeCmd(game, opts),
                "roster-sync" => RosterSyncCmd(game, opts),
                "bc-test" => BcTestCmd(opts),
                "retexture" => RetextureCmd(game, opts),
                "mesh-replace" => MeshReplaceCmd(game, opts),
                "logo-ads" => LogoAdsCmd(game, opts),
                "logo-ads-all" => LogoAdsAllCmd(game, opts),
                "logo-ads-named" => LogoAdsNamedCmd(game, opts),
                "unused-jerseys" => UnusedJerseysCmd(game, opts),
                "texture-grow" => TextureGrowCmd(game, opts),
                "venue-build" => VenueBuildCmd(game, opts),
                "roster-mirror" => RosterMirrorCmd(game, opts),
                "roster-repair" => RosterRepairCmd(game, opts),
                "db" => DbCmd(game, opts),
                "legacy" => LegacyCmd(game, opts),
                "ebx-refs" => EbxRefsCmd(game, opts),
                "ebx-imports" => EbxImportsCmd(game, opts),
                "ebx-grep" => EbxGrepCmd(game, opts),
                "legacy-set" => LegacySetCmd(game, opts),
                "scorebug" => ScorebugCmd(game, opts),
                "db-set" => DbSetCmd(game, opts),
                "kyty-releases" => KytyReleasesCmd(opts),
                "mods" => ModsCmd(game, opts),
                "apply-sound" => ApplySoundCmd(game, opts),
                "restore" => RestoreCmd(game, opts),
                "apply" => ApplyCmd(game, opts),
                "sharers" => SharersCmd(game, opts),
                "jerseys" => JerseysCmd(game, opts),
                "a9test" => A9TestCmd(game, opts),
                "codecs" => CodecsCmd(game, opts),
                "replace-sound" => ReplaceSoundCmd(game, opts),
                "replace-texture" => ReplaceTextureCmd(game, opts),
                "roundtrip" => RoundtripCmd(game, opts),
                "sound" => SoundCmd(game, opts),
                "movies" => MoviesCmd(game, opts),
                "nhl24pc" => Nhl24PcCmd(opts),
                "ps4-sigtest" => Ps4SigTestCmd(game, opts),
                "movie-replace" => MovieReplaceCmd(game, opts),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(Environment.GetEnvironmentVariable("NHL27KIT_DEBUG") != null ? $"error: {e}" : $"error: {e.Message}");
            return 1;
        }
    }

    private static void Usage() => Console.WriteLine(
        """
        nhl27kit - NHL 27 (PS5) data explorer

          nhl27kit info   [--game DIR]
          nhl27kit index  [--game DIR] [--rebuild]
          nhl27kit list   [ebx|res|chunk] [--filter TEXT] [--type TYPE] [--id GUID] [--limit N]
          nhl27kit export [ebx|res|chunk] NAME|GUID [--out DIR]
          nhl27kit texture NAME|--filter TEXT [--limit N] [--out DIR]   decode textures to PNG + DDS

        The game folder defaults to "NHL 27 dump/NHL" next to the kit. The game folder is only read.
        """);

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command '{cmd}'.");
        Usage();
        return 2;
    }

    private static int Info(string game)
    {
        var layout = new GameLayout(game);
        Console.WriteLine($"game: {layout.GameDir}");
        Console.WriteLine($"install chunks ({layout.Catalogs.Count}):");
        for (int i = 0; i < layout.Catalogs.Count; i++)
        {
            string dir = Path.Combine(layout.DataDir, layout.Catalogs[i].Replace('/', Path.DirectorySeparatorChar));
            int cas = Directory.Exists(dir) ? Directory.GetFiles(dir, "cas_*.cas").Length : 0;
            Console.WriteLine($"  [{i,2}] {layout.Catalogs[i]}  ({cas} cas)");
        }
        Console.WriteLine($"superbundles ({layout.SuperBundles.Count}):");
        foreach (string sb in layout.SuperBundles)
        {
            string data = layout.TocPath(sb);
            Console.WriteLine($"  {sb}");
            foreach (string path in new[] { data }.Concat(layout.LiveTocPaths(sb)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                    continue;
                var toc = new SuperBundleToc(path, File.ReadAllBytes(path), layout.TocCatalogs);
                var refs = toc.Chunks.Select(c => c.Ref).Concat(toc.Bundles.SelectMany(b => b.Entries)).ToList();
                int patched = refs.Count(r => r.Patch), unknown = refs.Count(r => r.Catalog < 0);
                Console.WriteLine($"      {Path.GetRelativePath(layout.GameDir, path)}: {toc.Bundles.Count} bundles, {toc.Chunks.Count} chunks" +
                                  $" ({patched} refs into Patch{(unknown > 0 ? $", {unknown} to unknown install chunks" : "")})");
            }
        }
        return 0;
    }

    /// <summary>The game --game names (NHL 27 or NHL 24): which index cache and mod list the commands use.</summary>
    private static GameProfile _profile;

    private static AssetIndex OpenIndex(string game, bool rebuild)
    {
        string cache = KitPaths.IndexFileFor(_profile);
        Directory.CreateDirectory(Path.GetDirectoryName(cache));
        if (rebuild && File.Exists(cache))
            File.Delete(cache);
        return AssetIndex.Open(game, File.ReadAllBytes, cache, Console.WriteLine);
    }

    private static int Index(string game, Options opts)
    {
        var index = OpenIndex(game, opts.Has("rebuild"));
        Console.WriteLine($"{index.BundleList.Count} bundles, {index.Ebx.Count} ebx, {index.Res.Count} res, {index.Chunks.Count} chunks");
        Console.WriteLine("ebx by type:");
        foreach (var g in index.Ebx.Values.GroupBy(e => e.Type ?? "").OrderByDescending(g => g.Count()).Take(40))
            Console.WriteLine($"  {g.Count(),7}  {(g.Key == "" ? "(unknown)" : g.Key)}");
        return 0;
    }

    private static int List(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string kind = opts.Positional.FirstOrDefault() ?? "ebx";
        string filter = opts.Get("filter");
        string type = opts.Get("type");
        // --id GUID: the EBX partition with this id (how ANT assets reference each other)
        Guid? id = Guid.TryParse(opts.Get("id"), out var g) ? g : null;
        int limit = int.TryParse(opts.Get("limit"), out int n) ? n : 200;
        bool Match(string s) => filter == null || (s ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);

        IEnumerable<string> rows = kind switch
        {
            "ebx" => index.Ebx.Values
                .Where(e => Match(e.Name) && (type == null || string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase)) && (id == null || e.Id == id))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => $"{e.Name}\t{e.Type}\t{e.OriginalSize}" + (opts.Has("show-id") ? $"\t{e.Id}" : "")),
            "res" => index.Res.Values.Where(e => Match(e.Name))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => $"{e.Name}\t0x{e.ResType:X8}\t{e.OriginalSize}"),
            "chunk" => index.Chunks.Values.Where(e => Match(e.Id.ToString()))
                .Select(e => $"{e.Id}\t{e.OriginalSize}\t{(e.InChunkTable ? "table" : "bundle")}"),
            _ => throw new ArgumentException($"list takes ebx, res or chunk, not '{kind}'"),
        };
        int count = 0;
        foreach (string row in rows.Take(limit))
        {
            Console.WriteLine(row);
            count++;
        }
        Console.Error.WriteLine($"{count} shown (--limit {limit})");
        return 0;
    }

    /// <summary>Decodes one texture (by name) or the first --limit textures matching --filter to PNG and DDS.</summary>
    /// <summary>legacy-jersey IN.dds OUT.png [--size N]: converts a legacy-template jersey texture to NHL 27's jersey layout.</summary>
    private static int LegacyJerseyCmd(Options opts)
    {
        string file = opts.Positional.ElementAtOrDefault(0) ?? throw new ArgumentException("legacy-jersey needs an input DDS");
        string outFile = opts.Positional.ElementAtOrDefault(1) ?? Path.ChangeExtension(file, null) + " (NHL 27).png";
        int size = int.TryParse(opts.Get("size"), out int n) ? n : 2048;
        var (rgba, w, h) = DdsReader.Load(file);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = LegacyJersey.Convert(rgba, w, h, size);
        File.WriteAllBytes(outFile, ImageFiles.Png(result, size, size));
        Console.WriteLine($"{Path.GetFileName(file)} {w}x{h} -> {outFile} {size}x{size} ({sw.ElapsedMilliseconds} ms)");
        return 0;
    }

    private static int TextureCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string outDir = opts.Get("out") ?? Path.Combine(KitPaths.Export, "textures");
        string filter = opts.Get("filter");
        int limit = int.TryParse(opts.Get("limit"), out int n) ? n : 10;
        var names = filter == null
            ? new List<string> { opts.Positional.FirstOrDefault() ?? throw new ArgumentException("texture needs a name or --filter") }
            : index.Ebx.Values.Where(e => e.Type == "TextureAsset" && e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
        int ok = 0;
        foreach (string name in names)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var t = Texture.Load(index, name, decode: false);
                t.Layer = int.TryParse(opts.Get("layer"), out int layerN) ? layerN : 0;
                var h = t.Header;
                Console.WriteLine($"{name}");
                Console.WriteLine($"  {h.Type} d={h.Depth} s={h.SliceCount} {h.Width}x{h.Height} {h.Format.Name} mips={h.MipCount} first={h.FirstMip} u32/33={h.Unknown32}/{h.Unknown33} flags={h.Flags} " +
                                  $"chunk={t.Data.Length:N0} (header says {h.ChunkSize:N0}) mipSizes=[{string.Join(",", h.MipSizes.Take(Math.Max(1, (int)h.MipCount)))}] " +
                                  $"offsets={h.MipOffsets[0]},{h.MipOffsets[1]}");
                t.DecodeLargest();
                if (int.TryParse(opts.Get("mip"), out int wantMip))
                    t.DecodeMip(wantMip);
                if (t.Problem != null)
                {
                    Console.WriteLine("  ! " + t.Problem);
                    continue;
                }
                string file = Path.Combine(outDir, name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file + ".png", t.ToPng());
                File.WriteAllBytes(file + ".dds", t.ToDds());
                Console.WriteLine($"  mip {t.Mip}: {t.Width}x{t.Height} -> {file}.png ({sw.ElapsedMilliseconds} ms)");
                ok++;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{name}\n  ! {e.Message}");
            }
        }
        Console.Error.WriteLine($"{ok} of {names.Count} decoded");
        return 0;
    }

    /// <summary>Lists NewWaveAssets matching --filter with the chunks they use and each chunk's SPS header.</summary>
    /// <summary>Encodes a replacement for a movie and plans it (nothing is written to the game).</summary>
    private static int MovieReplaceCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("movie-replace NAME --video FILE [--fit fill|fit|stretch] [--mute] [--out FILE.webm]");
        string video = opts.Get("video") ?? throw new ArgumentException("--video FILE");
        var m = Movies.List(index).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.Short.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no movie {name}");
        var t = MovieReplacement.Target(index, m);
        Console.WriteLine($"{m.Name}: {t.Width}x{t.Height} @ {t.Fps} fps, {t.Frames} frames ({t.Seconds:0.00} s), {t.Size:N0} B, {t.Keyframes} key{(t.Audio ? $", {t.AudioCodec} {t.AudioChannels}ch {t.AudioRate} Hz" : "")}");
        var fit = Enum.TryParse<MovieFit>(opts.Get("fit") ?? "fill", true, out var f) ? f : MovieFit.Fill;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string last = null;
        var webm = MovieReplacement.EncodeAsync(t, video, fit, !opts.Has("mute"), s => { if (!s.Contains("frame ")) Console.WriteLine("  " + s); last = s; }).GetAwaiter().GetResult();
        Console.WriteLine($"encoded in {sw.Elapsed.TotalSeconds:0} s: {webm.Length:N0} B");
        string outFile = opts.Get("out");
        if (outFile != null) File.WriteAllBytes(outFile, webm);
        var tocs = new TocCache(index);
        var plan = MovieReplacement.Plan(index, m, webm, LocationIndex.Build(index, tocs), tocs);
        var check = Movies.ReadTable(plan.Ebx, Movies.FrameTable(plan.Ebx, Movies.Frames(webm)));
        Console.WriteLine($"plan: {plan.Chunk.Patches.Count} chunk patches ({plan.Chunk.Copies} copies), {plan.EbxPlan.Patches.Count} ebx patches; table {check.Count} frames, ebx {plan.Ebx.Length} B");
        foreach (var n in plan.Notes) Console.WriteLine("  note: " + n);
        return 0;
    }

    private static int MoviesCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Get("filter") ?? opts.Positional.FirstOrDefault() ?? "";
        int limit = int.TryParse(opts.Get("limit"), out int n) ? n : int.MaxValue;
        string outDir = opts.Get("out");
        var movies = Movies.List(index).Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(limit).ToList();
        var stats = new Dictionary<string, int>();
        long total = 0;
        foreach (var m in movies)
        {
            byte[] d;
            try { d = Movies.Read(index, m); }
            catch (Exception e) { Console.WriteLine($"{m.Name}	unreadable: {e.Message}"); continue; }
            total += d.Length;
            var w = Movies.Probe(d);
            string kind = w == null ? $"not webm ({Convert.ToHexString(d.AsSpan(0, Math.Min(8, d.Length)))})"
                : $"{w.Codec} {w.Width}x{w.Height}{(w.Alpha ? " alpha" : "")}{(w.Audio != null ? $" +{w.Audio} {w.AudioChannels}ch" : "")}";
            stats[kind.Split(' ')[0] + (w?.Alpha == true ? " alpha" : "") + (w?.Audio != null ? " +audio" : "")] =
                stats.GetValueOrDefault(kind.Split(' ')[0] + (w?.Alpha == true ? " alpha" : "") + (w?.Audio != null ? " +audio" : "")) + 1;
            string table = "";
            if (w != null && opts.Has("check"))
            {
                var frames = Movies.Frames(d);
                var ebx = index.Read(m.Entry);
                int at = Movies.FrameTable(ebx, frames);
                if (at < 0) table = $"	no table ({frames.Count} frames)";
                else
                {
                    var t = Movies.ReadTable(ebx, at);
                    int bad = t.Zip(frames).Count(x => x.First.Offset != x.Second.Offset || x.First.Size != x.Second.Size || Math.Abs(x.First.Seconds - x.Second.Seconds) > 0.002);
                    table = $"	table @0x{at:x} {t.Count} frames, {bad} differ, {frames.Count(f => f.Key)} key";
                    stats[bad == 0 ? "table ok" : "table differs"] = stats.GetValueOrDefault(bad == 0 ? "table ok" : "table differs") + 1;
                }
                if (at < 0) stats["no table"] = stats.GetValueOrDefault("no table") + 1;
            }
            Console.WriteLine($"{m.Name}	{kind}	{w?.Seconds:0.0}s	{d.Length / 1048576.0:0.0} MB	{m.ChunkId}{(m.Subtitles > 0 ? $"	{m.Subtitles} subtitle" : "")}{table}");
            if (outDir != null)
            {
                string file = Path.Combine(outDir, m.Name.Replace('/', Path.DirectorySeparatorChar) + ".webm");
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, d);
            }
        }
        Console.WriteLine($"{movies.Count} movies, {total / 1048576.0:0} MB");
        foreach (var (k, v) in stats.OrderByDescending(x => x.Value)) Console.WriteLine($"  {v,5}  {k}");
        return 0;
    }

    private static int SoundCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Get("filter") ?? opts.Positional.FirstOrDefault() ?? "";
        int limit = int.TryParse(opts.Get("limit"), out int n) ? n : 10;
        var waves = index.Ebx.Values.Where(e => e.Type == "NewWaveAsset" && e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
        foreach (var w in waves)
        {
            var ebx = index.Read(w);
            var ids = Audio.ChunkIds(index, ebx);
            Console.WriteLine($"{w.Name}  ({ebx.Length} B ebx, {ids.Count} chunks)");
            foreach (var id in ids)
            {
                var c = index.GetChunk(id);
                byte[] d;
                try { d = index.Read(c); }
                catch (Exception e) { Console.WriteLine($"  {id}: unreadable ({e.Message})"); continue; }
                var h = SpsHeader.Read(d);
                if (opts.Has("wav") && h != null)
                {
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var snd = Sound.Decode(d);
                        string file = Path.Combine(opts.Get("out") ?? Path.Combine(KitPaths.Export, "sounds"), w.Name.Replace('/', Path.DirectorySeparatorChar) + $"_{ids.IndexOf(id)}.wav");
                        Directory.CreateDirectory(Path.GetDirectoryName(file));
                        File.WriteAllBytes(file, snd.ToWav(stereo: opts.Has("stereo")));
                        Console.WriteLine($"  -> {snd.Channels}ch {snd.Seconds:0.00}s in {sw.ElapsedMilliseconds} ms: {file}");
                    }
                    catch (Exception e) { Console.WriteLine($"  ! {e}"); }
                }
                Console.WriteLine($"  {id}: {d.Length:N0} B  " + (h == null ? "not SPS: " + Convert.ToHexString(d.AsSpan(0, Math.Min(16, d.Length))) :
                    $"{h.CodecName} (0x{h.CodecByte:X2}) {h.Channels}ch {h.SampleRate} Hz {h.Seconds:0.00}s hdr={h.HeaderSize}  first: {Convert.ToHexString(d.AsSpan(0, Math.Min(24, d.Length)))}"));
            }
        }
        Console.Error.WriteLine($"{waves.Count} sounds; {index.Ebx.Values.Count(e => e.Type == "NewWaveAsset"):N0} NewWaveAssets in all");
        return 0;
    }

    /// <summary>Shows every stored copy of a texture's chunk and checks the bundle copies against the full one.</summary>
    private static int CopiesCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("copies needs a texture name");
        var res = index.GetRes(name) ?? throw new ArgumentException($"no RES named {name}");
        var h = TextureHeader.Parse(index.Read(res));
        Console.WriteLine($"{name}: {h.Width}x{h.Height} {h.Format.Name} mips={h.MipCount} first={h.FirstMip} chunk={h.ChunkId} size={h.ChunkSize:N0} mipOffsets={h.MipOffsets[0]},{h.MipOffsets[1]}");
        var tocs = new TocCache(index);
        var copies = ChunkCopies.Find(index, h.ChunkId, tocs);
        byte[] full = null;
        foreach (var c in copies.Where(c => !c.InBundle && c.Live))
        {
            full = FrostbiteBlocks.Decode(index.ReadStored(c.Ref));
            break;
        }
        foreach (var c in copies)
        {
            byte[] stored = index.ReadStored(c.Ref);
            var raw = FrostbiteBlocks.Decode(stored);
            string where = Path.GetRelativePath(index.Layout.GameDir, index.Layout.CasPath(c.Ref.Catalog, c.Ref.Cas, c.Ref.Patch));
            string rel = "";
            if (full != null && c.InBundle)
            {
                int at = raw.Length <= full.Length ? full.Length - raw.Length : -1;
                bool tail = at >= 0 && full.AsSpan(at).SequenceEqual(raw);
                bool atLogical = c.LogicalOffset + raw.Length <= full.Length && full.AsSpan((int)c.LogicalOffset, raw.Length).SequenceEqual(raw);
                rel = $" tailOfFull={tail} atLogicalOffset={atLogical}";
            }
            string sha = c.Sha1 == null ? "" : $" sha1(stored)={(ChunkCopies.Sha1Of(stored).AsSpan().SequenceEqual(c.Sha1) ? "match" : "NO MATCH")} sha1(raw)={(ChunkCopies.Sha1Of(raw).AsSpan().SequenceEqual(c.Sha1) ? "match" : "no")}";
            if (c.Sha1 != null && full != null)
            {
                var fullStored = index.ReadStored(copies.First(x => !x.InBundle && x.Live).Ref);
                var cands = new Dictionary<string, byte[]>
                {
                    ["stored"] = stored, ["raw"] = raw, ["fullStored"] = fullStored, ["fullRaw"] = full,
                    ["raw[:logical]"] = raw.AsSpan(0, Math.Min(raw.Length, (int)c.LogicalSize)).ToArray(),
                    ["stored[:logical]"] = stored.AsSpan(0, Math.Min(stored.Length, (int)c.LogicalSize)).ToArray(),
                    ["fullRaw[:logical]"] = full.AsSpan(0, Math.Min(full.Length, (int)c.LogicalSize)).ToArray(),
                    ["fullStored[:logical]"] = fullStored.AsSpan(0, Math.Min(fullStored.Length, (int)c.LogicalSize)).ToArray(),
                    ["raw[:logical] framed"] = FrostbiteBlocks.Store(raw.AsSpan(0, Math.Min(raw.Length, (int)c.LogicalSize)).ToArray()),
                };
                foreach (var (k, v) in cands)
                    if (ChunkCopies.Sha1Of(v).AsSpan().SequenceEqual(c.Sha1))
                        Console.WriteLine($"    SHA1 = sha1({k})");
                Console.WriteLine($"    record sha1 {Convert.ToHexString(c.Sha1)} at 0x{c.Sha1Pos:X}");
            }
            Console.WriteLine($"  {(c.Live ? "live" : "dead")} {c.Kind,-6} {Path.GetFileName(c.Toc)} {(c.InBundle ? c.BundleName : "")}\n      {where} @0x{c.Ref.Offset:X} stored={stored.Length:N0} raw={raw.Length:N0} logical=({c.LogicalOffset},{c.LogicalSize}){rel}{sha}");
        }
        return 0;
    }

    /// <summary>Decodes textures, re-encodes them, and measures the loss (PSNR), and checks the new chunk has the original's size and framing.</summary>
    private static int RoundtripCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Get("filter") ?? opts.Positional.FirstOrDefault() ?? "";
        int limit = int.TryParse(opts.Get("limit"), out int n) ? n : 6;
        var names = index.Ebx.Values.Where(e => e.Type == "TextureAsset" && e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) && index.GetRes(e.Name) != null)
            .Select(e => e.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
        foreach (string name in names)
        {
            try
            {
                var t = Texture.Load(index, name, decode: false);
                var h = t.Header;
                string why = TextureBuilder.Unsupported(h);
                if (why != null) { Console.WriteLine($"{name}: skipped ({why})"); continue; }
                t.DecodeMip(0);
                if (t.Rgba == null || t.Mip != 0) { Console.WriteLine($"{name}: skipped (mip 0 not in the chunk)"); continue; }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var raw = TextureBuilder.BuildChunk(h, t.Rgba);
                long ms = sw.ElapsedMilliseconds;
                var stored = index.ReadStored(t.Chunk.Ref);
                var framed = Framing.Reframe(stored, raw);
                var t2 = new Texture { Name = name, Header = h, Chunk = t.Chunk, Data = raw };
                t2.DecodeMip(0);
                double se = 0; long cnt = 0;
                int channels = h.Format.Name.StartsWith("BC5") ? 2 : 4;
                for (int i = 0; i < t.Rgba.Length; i += 4)
                    for (int c = 0; c < channels; c++) { double d = t.Rgba[i + c] - t2.Rgba[i + c]; se += d * d; cnt++; }
                double psnr = se == 0 ? 99 : 10 * Math.Log10(255.0 * 255.0 / (se / cnt));
                string small = "";
                // the smallest stored mips decode as well as the large ones
                int last = TextureBuilder.StoredMips(h) - 1;
                var a = new Texture { Name = name, Header = h, Chunk = t.Chunk, Data = t.Data }; a.DecodeMip(last);
                var b = new Texture { Name = name, Header = h, Chunk = t.Chunk, Data = raw }; b.DecodeMip(last);
                double se2 = 0; for (int i = 0; i < a.Rgba.Length; i++) { double d = a.Rgba[i] - b.Rgba[i]; se2 += d * d; }
                small = $" mip{last} {a.Width}x{a.Height} mse {se2 / a.Rgba.Length:0.0}";
                Console.WriteLine($"{name}\n  {h.Width}x{h.Height} {h.Format.Name}: {psnr:0.0} dB, built in {ms} ms, size {(raw.Length == t.Data.Length ? "same" : "DIFFERENT")}, framing {(framed.Length == stored.Length ? "same" : "DIFFERENT")}{small}");
                if (opts.Has("save"))
                {
                    string dir = Path.Combine(KitPaths.Export, "roundtrip");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, name.Substring(name.LastIndexOf('/') + 1) + "_orig.png"), ImageFiles.Png(t.Rgba, t.Width, t.Height));
                    File.WriteAllBytes(Path.Combine(dir, name.Substring(name.LastIndexOf('/') + 1) + "_new.png"), ImageFiles.Png(t2.Rgba, t2.Width, t2.Height));
                }
            }
            catch (Exception e) { Console.WriteLine($"{name}\n  ! {e.Message}"); }
        }
        return 0;
    }

    /// <summary>
    /// replace-texture NAME [--pattern checker|invert|tint] [--target DIR] [--keep]: plans a replacement with a test image
    /// and, with --target, copies the touched archives there, applies it to the copy, checks it, restores, checks again.
    /// The game folder itself is never written.
    /// </summary>
    private static int ReplaceTextureCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("replace-texture needs a texture name");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tocs = new TocCache(index);
        Console.WriteLine("reading every TOC to see which data is shared...");
        var loc = LocationIndex.Build(index, tocs);
        Console.WriteLine($"  done in {sw.Elapsed.TotalSeconds:0.0}s");

        var orig = Texture.Load(index, name, decode: false);
        var h = orig.Header;
        orig.DecodeMip(0);
        var img = new byte[h.Width * h.Height * 4];
        string pattern = opts.Get("pattern") ?? "checker";
        for (int y = 0; y < h.Height; y++)
            for (int x = 0; x < h.Width; x++)
            {
                int i = (y * h.Width + x) * 4;
                switch (pattern)
                {
                    case "invert": for (int c = 0; c < 3; c++) img[i + c] = (byte)(255 - orig.Rgba[i + c]); img[i + 3] = orig.Rgba[i + 3]; break;
                    case "tint": img[i] = (byte)Math.Min(255, orig.Rgba[i] + 90); img[i + 1] = (byte)(orig.Rgba[i + 1] / 2); img[i + 2] = (byte)(orig.Rgba[i + 2] / 2); img[i + 3] = orig.Rgba[i + 3]; break;
                    default:
                        bool on = ((x / 64) + (y / 64)) % 2 == 0;
                        img[i] = (byte)(on ? 255 : 20); img[i + 1] = (byte)(on ? 0 : 200); img[i + 2] = (byte)(on ? 255 : 40); img[i + 3] = 255;
                        break;
                }
            }
        sw.Restart();
        TextureReplacementPlan plan;
        try { plan = Replacements.PlanTexture(index, name, img, loc, tocs); }
        catch (Exception e) { Console.WriteLine("cannot replace: " + e.Message); return 1; }
        Console.WriteLine($"planned in {sw.ElapsedMilliseconds} ms: {string.Join("; ", plan.Chunk.Notes)}");
        foreach (var p in plan.Chunk.Patches)
            Console.WriteLine($"  {p.Note,-12} {p.New.Length,10:N0} bytes  {p.File} @ 0x{p.Offset:X}");

        string target = opts.Get("target");
        if (target == null) { Console.WriteLine("(no --target: nothing written)"); return 0; }

        // work on copies of the touched archives
        foreach (var rel in plan.Chunk.Patches.Select(p => p.File).Distinct())
        {
            string src = Path.Combine(index.Layout.GameDir, rel.Replace('/', Path.DirectorySeparatorChar)), dst = Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(src).Length) { Console.WriteLine($"copying {rel} ({new FileInfo(src).Length / 1048576} MB)..."); File.Copy(src, dst, true); }
        }
        var store = new ModStore(Path.Combine(target, "_mods"));
        foreach (var old in store.Entries.ToList()) store.Remove(old);
        var entry = store.Add("texture", name, "test pattern " + pattern, "test", plan.Chunk.Patches);
        Console.WriteLine($"state before: {store.State(entry, target)}");
        store.Apply(entry, target, Console.WriteLine);
        Console.WriteLine($"state after apply: {store.State(entry, target)}");
        // read the texture back from the modified copy of the archive: the chunk at its table location
        var fullRef = orig.Chunk.Ref;
        string chunkFile = Path.Combine(target, Path.GetRelativePath(index.Layout.GameDir, index.Layout.CasPath(fullRef.Catalog, fullRef.Cas, fullRef.Patch)));
        byte[] stored;
        using (var fs = new FileStream(chunkFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        { fs.Position = fullRef.Offset; stored = new byte[fullRef.Size]; fs.ReadExactly(stored); }
        var back = new Texture { Name = name, Header = h, Chunk = null, Data = FrostbiteBlocks.Decode(stored) };
        back.DecodeMip(0);
        double se = 0; for (int i = 0; i < back.Rgba.Length; i += 4) for (int c = 0; c < 3; c++) { double d = back.Rgba[i + c] - img[i + c]; se += d * d; }
        Console.WriteLine($"read back from the modified copy: {back.Width}x{back.Height}, {10 * Math.Log10(255.0 * 255.0 / (se / (back.Rgba.Length / 4 * 3) + 1e-9)):0.0} dB against the replacement image");
        if (opts.Has("keep")) { Console.WriteLine("kept applied (--keep)"); return 0; }
        store.Restore(entry, target, Console.WriteLine);
        Console.WriteLine($"state after restore: {store.State(entry, target)}");
        using (var fs = new FileStream(chunkFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        { fs.Position = fullRef.Offset; var again = new byte[fullRef.Size]; fs.ReadExactly(again); Console.WriteLine("restored bytes identical to the original: " + again.AsSpan().SequenceEqual(index.ReadStored(fullRef))); }
        return 0;
    }

    /// <summary>
    /// replace-sound NAME --wav FILE [--variation N] [--target DIR]: plans replacing one variation of a sound and, with
    /// --target, applies it to copies of the touched archives, reads it back, restores and checks. Writes the decoded result
    /// to export/replaced.wav to listen to.
    /// </summary>
    private static int ReplaceSoundCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("replace-sound needs a sound name");
        string wav = opts.Get("wav") ?? throw new ArgumentException("replace-sound needs --wav FILE");
        int variation = int.TryParse(opts.Get("variation"), out int v) ? v : 1;
        var wave = index.GetEbx(name) ?? throw new ArgumentException($"no sound named {name}");
        var vars = Audio.Variations(index, wave);
        if (variation < 1 || variation > vars.Count) throw new ArgumentException($"{name} has {vars.Count} variation(s)");
        var stream = vars[variation - 1];
        var header = stream.Header;
        Console.WriteLine($"{name} #{variation} of {vars.Count}: {header?.CodecName} {header?.Channels}ch {header?.Seconds:0.00}s at {stream.Offset:N0} in chunk {stream.ChunkId}");
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var (pcm, ch, rate) = WavReader.Read(File.ReadAllBytes(wav));
        Console.WriteLine($"input: {ch}ch {rate} Hz {pcm.Length / ch / (double)rate:0.00}s");
        SoundReplacementPlan plan;
        try { plan = Replacements.PlanSound(index, name, stream, pcm, ch, rate, loc, tocs); }
        catch (Exception e) { Console.WriteLine("cannot replace: " + e.Message); return 1; }
        Console.WriteLine($"stream {plan.Bytes:N0} of {plan.Capacity:N0} bytes at {plan.BitratePerChannel / 1000} kbps/ch; decoded back: {plan.Result.Channels}ch {plan.Result.Seconds:0.00}s");
        foreach (var w in plan.Warnings) Console.WriteLine("  note: " + w);
        Console.WriteLine($"  {string.Join("; ", plan.Chunk.Notes)}");
        foreach (var p in plan.Chunk.Patches) Console.WriteLine($"  {p.Note,-12} {p.New.Length,10:N0} bytes  {p.File} @ 0x{p.Offset:X}");
        Directory.CreateDirectory(KitPaths.Export);
        File.WriteAllBytes(Path.Combine(KitPaths.Export, "replaced.wav"), plan.Result.ToWav(stereo: true));

        string target = opts.Get("target");
        if (target == null) { Console.WriteLine("(no --target: nothing written)"); return 0; }
        foreach (var rel in plan.Chunk.Patches.Select(p => p.File).Distinct())
        {
            string src = Path.Combine(index.Layout.GameDir, rel.Replace('/', Path.DirectorySeparatorChar)), dst = Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(src).Length) { Console.WriteLine($"copying {rel} ({new FileInfo(src).Length / 1048576} MB)..."); File.Copy(src, dst, true); }
        }
        var store = new ModStore(Path.Combine(target, "_mods"));
        foreach (var old in store.Entries.ToList()) store.Remove(old);
        var entry = store.Add("sound", name, wav, "test", plan.Chunk.Patches);
        store.Apply(entry, target, Console.WriteLine);
        Console.WriteLine($"state after apply: {store.State(entry, target)}");
        // read the whole chunk back from the modified copy and walk its streams
        var full = stream.Chunk.Ref;
        byte[] stored;
        using (var fs = new FileStream(Path.Combine(target, Path.GetRelativePath(index.Layout.GameDir, index.Layout.CasPath(full.Catalog, full.Cas, full.Patch))), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        { fs.Position = full.Offset; stored = new byte[full.Size]; fs.ReadExactly(stored); }
        var rawBack = FrostbiteBlocks.Decode(stored);
        var streamsBack = Audio.Streams(rawBack);
        var back = Sound.Decode(rawBack.AsSpan(stream.Offset, stream.Length).ToArray());
        Console.WriteLine($"read back from the modified copy: {back.Header.CodecName} {back.Channels}ch {back.Seconds:0.00}s; the chunk still has {streamsBack.Count} streams (was {Audio.Streams(index.Read(stream.Chunk)).Count})");
        store.Restore(entry, target, Console.WriteLine);
        Console.WriteLine($"state after restore: {store.State(entry, target)}");
        return 0;
    }

    /// <summary>codecs: the codec and channel count of the first stream of every NewWaveAsset chunk, counted per sound folder.</summary>
    private static int CodecsCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int n = 0;
        foreach (var w in index.Ebx.Values.Where(e => e.Type == "NewWaveAsset"))
        {
            var parts = w.Name.Split('/');
            string folder = parts.Length > 2 ? parts[1] + "/" + parts[2] : w.Name;
            foreach (var id in Audio.ChunkIds(index, index.Read(w)))
            {
                var c = index.GetChunk(id);
                // the first stored block's start is the stream's 'H' header (data is stored uncompressed)
                var head = index.ReadStored(c.Ref.Catalog, c.Ref.Cas, c.Ref.Patch, c.Ref.Offset, Math.Min(c.Ref.Size, 40u));
                var h = head.Length >= 20 ? SpsHeader.Read(head.AsSpan(8)) : null;
                string key = $"{folder,-28} {(h == null ? "not SPS" : $"{h.CodecName} {h.Channels}ch")}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
            n++;
        }
        foreach (var (k, v) in counts) Console.WriteLine($"{v,6}  {k}");
        Console.Error.WriteLine($"{n} sounds");
        return 0;
    }

    /// <summary>a9test NAME [--variation N] [--gain G]: decodes an ATRAC9 stream, re-encodes it with its own configs, decodes that, and compares.</summary>
    private static int A9TestCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("a9test needs a sound name");
        int variation = int.TryParse(opts.Get("variation"), out int v) ? v : 1;
        if (double.TryParse(opts.Get("gain"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double gain))
            VGAudio.Codecs.Atrac9.Atrac9Encoder.SpectrumGain = gain;
        if (int.TryParse(opts.Get("slope"), out int slope)) VGAudio.Codecs.Atrac9.Atrac9Encoder.GradientSlope = slope;
        if (int.TryParse(opts.Get("bands"), out int bands)) VGAudio.Codecs.Atrac9.Atrac9Encoder.BandCountOverride = bands;
        var wave = index.GetEbx(name) ?? throw new ArgumentException($"no sound named {name}");
        var stream = Audio.Variations(index, wave)[variation - 1];
        var raw = index.Read(stream.Chunk).AsSpan(stream.Offset, stream.Length).ToArray();
        var h = stream.Header;
        var orig = Sound.Decode(raw);
        var configs = SpsAtrac9Writer.ReadConfigs(raw, h);
        var cfg0 = new VGAudio.Codecs.Atrac9.Atrac9Config(configs[0]);
        int gpb = SpsAtrac9Writer.GroupsPerBlock(raw, h, cfg0.SuperframeSamples);
        Console.WriteLine($"{name} #{variation}: {h.Channels}ch {h.Seconds:0.00}s, {configs.Count} stream(s), {cfg0.FrameBytes} B/frame, {cfg0.SuperframeSamples} samples/superframe, {gpb} per block, {raw.Length:N0} bytes");
        {
            var test = Enumerable.Range(0, cfg0.ChannelCount).Select(c => Enumerable.Range(0, orig.Frames / cfg0.SuperframeSamples * cfg0.SuperframeSamples).Select(i => orig.Pcm[i * orig.Channels + c]).ToArray()).ToArray();
            Console.WriteLine("self test: " + VGAudio.Codecs.Atrac9.Atrac9Encoder.SelfTest(configs[0], test));
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        uint flags = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(8)) & 0xF0000000u;
        var sps = SpsAtrac9Writer.Encode(orig.Pcm, orig.Channels, configs, raw.AsSpan(0, 8).ToArray(), flags, gpb, raw.Length + 100000);
        Console.WriteLine($"encoded in {sw.ElapsedMilliseconds} ms: {sps.Length:N0} bytes (original {raw.Length:N0})");
        var back = Sound.Decode(sps);
        // find the delay and gain that line the result up with the original
        int ch = orig.Channels, n = Math.Min(orig.Frames, back.Frames);
        int bestLag = 0; double bestCorr = double.MinValue;
        for (int lag = 0; lag <= 1024; lag++)
        {
            double c = 0;
            for (int i = 0; i + lag < n; i += 7) c += (double)orig.Pcm[i * ch] * back.Pcm[(i + lag) * ch];
            if (c > bestCorr) { bestCorr = c; bestLag = lag; }
        }
        double num = 0, den = 0, sig = 0, err = 0;
        for (int i = 0; i + bestLag < n; i++)
            for (int c = 0; c < ch; c++) { double a = orig.Pcm[i * ch + c], b = back.Pcm[(i + bestLag) * ch + c]; num += a * b; den += b * b; }
        double g2 = den > 0 ? num / den : 1;
        for (int i = 0; i + bestLag < n; i++)
            for (int c = 0; c < ch; c++) { double a = orig.Pcm[i * ch + c], b = back.Pcm[(i + bestLag) * ch + c] * g2; sig += a * a; err += (a - b) * (a - b); }
        Console.WriteLine($"lag {bestLag} samples, best gain {g2:0.0000} (so SpectrumGain should be x{g2:0.0000}), SNR after gain {10 * Math.Log10(sig / Math.Max(err, 1e-9)):0.0} dB");
        Directory.CreateDirectory(KitPaths.Export);
        File.WriteAllBytes(Path.Combine(KitPaths.Export, "a9_original.wav"), orig.ToWav(stereo: true));
        File.WriteAllBytes(Path.Combine(KitPaths.Export, "a9_reencoded.wav"), back.ToWav(stereo: true));
        return 0;
    }

    private static ModStore Mods() => new ModStore(KitPaths.ModsFor(_profile));

    private static void RefuseWhileRunning()
    {
        if (Emulators.Running() is { } emu)
            throw new InvalidOperationException($"{emu} is running. Close the game before changing its files.");
        if (_profile?.Kind == GameKind.Nhl24)
            throw new InvalidOperationException("NHL 24 can be viewed and exported for now; changing its files isn't ready yet.");
    }

    /// <summary>mods: the mod list (the app's Mods page) and each entry's state in the game.</summary>
    private static int ModsCmd(string game, Options opts)
    {
        var store = Mods();
        if (store.Entries.Count == 0) { Console.WriteLine("no mods"); return 0; }
        foreach (var e in store.Entries)
            Console.WriteLine($"{store.State(e, game),-11} {e.Kind,-8} {e.Name}  ({e.Summary}; {e.Patches.Count} range(s); from {Path.GetFileName(e.Source)})");
        return 0;
    }

    /// <summary>apply-sound NAME --file AUDIO [--variation N]: replaces one variation of a sound in the game files (through the mod list).</summary>
    private static int ApplySoundCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("apply-sound needs a sound name");
        string file = opts.Get("file") ?? throw new ArgumentException("apply-sound needs --file AUDIO");
        int variation = int.TryParse(opts.Get("variation"), out int v) ? v : 1;
        var wave = index.GetEbx(name) ?? throw new ArgumentException($"no sound named {name}");
        var stream = Audio.Variations(index, wave)[variation - 1];
        string key = $"{name}#{variation}";
        var store = Mods();
        var existing = store.Find("sound", key);
        if (existing != null && store.State(existing, game) is ModState.Applied or ModState.Partial) { store.Restore(existing, game, Console.WriteLine); }
        var (pcm, ch, rate) = ReadAudio(file);
        var tocs = new TocCache(index);
        var plan = Replacements.PlanSound(index, name, stream, pcm, ch, rate, LocationIndex.Build(index, tocs), tocs);
        var h = plan.Original;
        string summary = $"{h.CodecName} {h.Channels} ch â†’ {plan.Codec} {plan.BitratePerChannel / 1000} kbps/ch, {plan.Result.Seconds:0.0} s in {plan.Bytes:N0} of {plan.Capacity:N0} bytes";
        var entry = store.Add("sound", key, Path.GetFullPath(file), summary, plan.Chunk.Patches, plan.Warnings);
        Console.WriteLine(summary);
        foreach (var w in plan.Warnings) Console.WriteLine("  note: " + w);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        // read it back from the game files
        var back = Audio.Decode(index, stream);
        Console.WriteLine($"read back from the game: {back.Header.CodecName} {back.Channels} ch {back.Seconds:0.00} s");
        return 0;
    }

    /// <summary>restore NAME: puts the original bytes of a mod back (NAME as the mods command lists it).</summary>
    private static int RestoreCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("restore needs a mod name");
        var store = Mods();
        var e = store.Entries.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no mod named {name}");
        store.Restore(e, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(e, game)}");
        return 0;
    }

    /// <summary>
    /// jerseys TEAM [--test]: a team's uniform slots (TEAM is an exhibitionteams index or abbreviation) with the kit each
    /// shows. --test adds a slot, moves one and removes one in memory, then undoes it and checks the table comes back byte
    /// for byte; nothing is written.
    /// </summary>
    private static int JerseysCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var db = new Legacy(index).OpenDb(JerseySlots.Database);
        var kits = Kits.Find(index);
        var teams = JerseySlots.Teams(db, kits);
        string arg = opts.Positional.FirstOrDefault() ?? "TOR";
        var et = db.Table("exhibitionteams");
        var ef = et.Fields.ToDictionary(x => x.Name);
        int teamId = int.TryParse(arg, out int n) ? n : Enumerable.Range(0, et.WrittenRecords)
            .Where(r => string.Equals((string)db.Get(et, r, ef["abbrname"]), arg, StringComparison.OrdinalIgnoreCase))
            .Select(r => (int)db.Get(et, r, ef["index"])).DefaultIfEmpty(-1).First();
        string KitOf(JerseySlot s)
        {
            var folder = teams.FirstOrDefault(x => x.Value.ArtId == s.ArtId).Key;
            var kit = kits.FirstOrDefault(k => (k.League, k.Team) == folder && JerseySlots.Address(k) == (s.Style, s.Variant));
            return kit?.ToString() ?? $"art {s.ArtId} {JerseySlots.Brands.ElementAtOrDefault(s.Style)} {s.Variant} (no kit folder)";
        }
        var slots = JerseySlots.Read(db, teamId);
        Console.WriteLine($"team {teamId}: {slots.Count} slots ({db.Table(JerseySlots.TableName).WrittenRecords} of {db.Table(JerseySlots.TableName).Records} records used)");
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            Console.WriteLine($"  {i,2}. #{s.JerseyIndex,-3} {JerseySlots.LabelName(s.Label),-8} {(s.IsDefault ? "default" : ""),-8} {(s.IsLight ? "light" : "dark"),-6} {KitOf(s)}");
        }
        if (opts.Has("tail"))
        {
            var tt = db.Table(JerseySlots.TableName);
            int recStart = tt.RecordsStart;
            foreach (int r in new[] { tt.WrittenRecords - 2, tt.WrittenRecords - 1, tt.WrittenRecords, tt.WrittenRecords + 1, tt.WrittenRecords + 2, tt.WrittenRecords + 300, tt.Records - 1 })
            {
                var b = db.Data.AsSpan(recStart + r * tt.RecordSize, tt.RecordSize).ToArray();
                var tmp = new T3Db(db.Data);
                Console.WriteLine($"  [{r}] {Convert.ToHexString(b)}  " + string.Join(" ", tt.Fields.Where(f => f.Name is "teamid" or "jerseyindex" or "teamartid" or "style" or "variant" or "orderid").Select(f => $"{f.Name}={Bits(b, f)}")));
            }
            static long Bits(byte[] b, T3Field f) { long v = 0; for (int i = 0; i < f.Depth; i++) { int bit = f.BitOffset + i; if ((b[bit >> 3] >> (bit & 7) & 1) != 0) v |= 1L << i; } return v + f.RangeLow; }
            return 0;
        }
        if (!opts.Has("test")) return 0;

        var t = db.Table(JerseySlots.TableName);
        var original = db.Data.ToArray();
        var othersBefore = Enumerable.Range(0, 1024).Where(x => x != teamId).ToDictionary(x => x, x => JerseySlots.Read(db, x).Select(s => s with { Row = -1 }).ToList());
        // exact: add a slot then take it out again, move one and move it back (the unused records hold leftover junk that a
        // removed slot leaves zeroed, so they and the CRC over them are left out of the comparison)
        int junkFrom = t.RecordsStart + t.WrittenRecords * t.RecordSize, junkTo = t.CrcRecordsPos + 4;
        bool Same() => db.Data.AsSpan(0, junkFrom).SequenceEqual(original.AsSpan(0, junkFrom)) && db.Data.AsSpan(junkTo).SequenceEqual(original.AsSpan(junkTo));
        {
            var bos0 = teams.Values.First(x => x.Name == "Boston Bruins");
            JerseySlots.Write(db, teamId, slots.Append(JerseySlots.NewSlot(db, bos0.ArtId, 2, 0)).ToList());
            int added = t.WrittenRecords;
            JerseySlots.Write(db, teamId, JerseySlots.Read(db, teamId).Where(x => slots.Any(o => o.JerseyIndex == x.JerseyIndex)).OrderBy(x => slots.FindIndex(o => o.JerseyIndex == x.JerseyIndex)).ToList());
            for (int i = 0, shown = 0; !Same() && i < original.Length && shown < 12; i++)
                if (original[i] != db.Data[i]) { shown++; int rel = i - t.RecordsStart; Console.WriteLine($"    byte {i}: table record {(rel >= 0 ? rel / t.RecordSize : -1)} +{(rel >= 0 ? rel % t.RecordSize : rel)} was {original[i]:X2} now {db.Data[i]:X2}  (records CRC at {t.CrcRecordsPos}, header CRC at {t.CrcHeaderPos}, start {t.Start})"); }
            Console.WriteLine($"add then remove: {added} records with the new slot, back to {t.WrittenRecords}; identical to the original: {Same()}");
            var moved = slots.Skip(1).Append(slots[0]).ToList();
            JerseySlots.Write(db, teamId, moved);
            JerseySlots.Write(db, teamId, slots);
            Console.WriteLine($"move then move back: identical to the original: {Same()}");
        }
        // add Boston's adidas home, move the last slot to the top, remove the second
        var bos = teams.Values.First(x => x.Name == "Boston Bruins");
        var edited = slots.ToList();
        edited.Add(JerseySlots.NewSlot(db, bos.ArtId, 2, 0));
        var last = edited[^2];
        edited.Remove(last);
        edited.Insert(0, last);
        var gone = edited[1];
        edited.RemoveAt(1);
        Console.WriteLine("write: " + JerseySlots.Write(db, teamId, edited) + $"  (CRCs {db.CheckCrcs().Ok}/{db.CheckCrcs().Total}, {t.WrittenRecords} records)");
        var after = JerseySlots.Read(db, teamId);
        for (int i = 0; i < after.Count; i++)
            Console.WriteLine($"  {i,2}. #{after[i].JerseyIndex,-3} {JerseySlots.LabelName(after[i].Label),-8} {KitOf(after[i])}");
        // other teams untouched
        bool othersSame = true;
        foreach (var (tid, list) in othersBefore)
            if (!list.SequenceEqual(JerseySlots.Read(db, tid).Select(s => s with { Row = -1 }))) { othersSame = false; Console.WriteLine($"  team {tid} changed!"); }
        Console.WriteLine("other teams unchanged: " + othersSame);
        // undo: remove the new one, put the old one back, original order (the old one comes back with a new slot number)
        var now = JerseySlots.Read(db, teamId);
        var undo = new List<JerseySlot>();
        foreach (var s in slots)
            undo.Add(s.Row == gone.Row ? JerseySlots.NewSlot(db, s.ArtId, s.Style, s.Variant) with { Label = s.Label, IsDefault = s.IsDefault, IsLight = s.IsLight }
                : now.First(x => x.JerseyIndex == s.JerseyIndex));
        Console.WriteLine("undo: " + JerseySlots.Write(db, teamId, undo) + $"  (CRCs {db.CheckCrcs().Ok}/{db.CheckCrcs().Total}, {t.WrittenRecords} records)");
        int diff = 0, first = -1;
        for (int i = 0; i < original.Length; i++) if (original[i] != db.Data[i]) { diff++; if (first < 0) first = i; }
        Console.WriteLine($"bytes different from the original: {diff}" + (diff > 0 ? $" (first at {first}; the removed slot came back as #{JerseySlots.Read(db, teamId).Max(x => x.JerseyIndex)}, so its number differs)" : ""));
        return 0;
    }
    /// <summary>sharers TEXTURE: the other TOC entries that point at the bytes of a texture's chunk (and the textures whose chunk that is).</summary>
    private static int SharersCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("sharers needs a texture name");
        var header = TextureHeader.Parse(index.Read(index.GetRes(name) ?? throw new ArgumentException($"no texture {name}")));
        var tocs = new TocCache(index);
        var mine = ChunkCopies.Find(index, header.ChunkId, tocs).Select(c => LocationIndex.Key(c.Ref)).ToHashSet();
        var byChunk = new Dictionary<Guid, string>();
        foreach (var (n, r) in index.Res)
            if (n.Contains("teamlogos", StringComparison.OrdinalIgnoreCase) || n.Contains("centerlogo", StringComparison.OrdinalIgnoreCase) || opts.Has("all"))
                try { byChunk[TextureHeader.Parse(index.Read(r)).ChunkId] = n; } catch { }
        foreach (string path in index.Layout.AllTocPaths())
        {
            var toc = tocs.Get(path);
            foreach (var c in toc.Chunks)
                if (mine.Contains(LocationIndex.Key(c.Ref)) && c.Id != header.ChunkId)
                    Console.WriteLine($"  chunk {c.Id} in {Path.GetFileName(path)} -> {byChunk.GetValueOrDefault(c.Id, "?")}");
            foreach (var b in toc.Bundles)
                for (int i = 0; i < b.Entries.Count; i++)
                    if (mine.Contains(LocationIndex.Key(b.Entries[i])))
                        Console.WriteLine($"  bundle {b.Name} entry {i} in {Path.GetFileName(path)}");
        }
        return 0;
    }

    /// <summary>apply NAME: writes a mod's recorded edit into the game again (NAME as the mods command lists it).</summary>
    private static int ApplyCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("apply needs a mod name");
        var store = Mods();
        var e = store.Entries.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no mod named {name}");
        store.Apply(e, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(e, game)}");
        return 0;
    }

    /// <summary>Audio for the CLI: WAV directly, anything else through ffmpeg (on PATH or from winget).</summary>
    private static (short[] Pcm, int Channels, int Rate) ReadAudio(string file)
    {
        if (Path.GetExtension(file).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return WavReader.Read(File.ReadAllBytes(file));
        string ffmpeg = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(d => Path.Combine(d.Trim(), "ffmpeg.exe")).FirstOrDefault(File.Exists);
        if (ffmpeg == null)
        {
            string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
            ffmpeg = Directory.Exists(winget) ? Directory.GetDirectories(winget, "*FFmpeg*").SelectMany(d => Directory.EnumerateFiles(d, "ffmpeg.exe", SearchOption.AllDirectories)).FirstOrDefault() : null;
        }
        if (ffmpeg == null) throw new NotSupportedException("Only WAV files can be read without ffmpeg.");
        string tmp = Path.Combine(Path.GetTempPath(), "NHL27Kit", Guid.NewGuid().ToString("N") + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(tmp));
        var psi = new System.Diagnostics.ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (string a in new[] { "-y", "-v", "error", "-i", file, "-ar", "48000", "-c:a", "pcm_s16le", tmp }) psi.ArgumentList.Add(a);
        using (var p = System.Diagnostics.Process.Start(psi)) { string err = p.StandardError.ReadToEnd(); p.WaitForExit(); if (p.ExitCode != 0) throw new InvalidDataException("ffmpeg: " + err); }
        try { return WavReader.Read(File.ReadAllBytes(tmp)); } finally { File.Delete(tmp); }
    }

    /// <summary>dbtest DB META [--table NAME] [--rows N]: parses a t3db file, checks its CRCs and prints tables or rows.</summary>
    private static int DbTestCmd(string game, Options opts)
    {
        string dbFile = opts.Positional.ElementAtOrDefault(0) ?? throw new ArgumentException("dbtest needs a .db file");
        string meta = opts.Positional.ElementAtOrDefault(1);
        var db = new T3Db(File.ReadAllBytes(dbFile), meta != null ? File.ReadAllText(meta) : null);
        var (ok, total) = db.CheckCrcs();
        Console.WriteLine($"{db.Tables.Count} tables, CRCs {ok}/{total} match");
        string tname = opts.Get("table");
        if (tname == null)
        {
            foreach (var t in db.Tables.OrderBy(t => t.Name))
                Console.WriteLine($"  {t.Name,-40} {t.ShortName} {t.WrittenRecords,6} rows ({t.Records} max) {t.Fields.Count,3} fields{(t.HasCompressedStrings ? " (compressed strings)" : "")}");
            return 0;
        }
        var table = db.Table(tname) ?? throw new ArgumentException($"no table {tname}");
        int rows = int.TryParse(opts.Get("rows"), out int r) ? r : 5;
        Console.WriteLine(string.Join(" | ", table.Fields.Select(f => $"{f.Name}:{f.Type}/{f.Depth}")));
        int shown = 0;
        for (int i = 0; i < table.Records && shown < rows; i++)
        {
            if (db.IsInvalid(table, i)) continue;
            Console.WriteLine($"  [{i}] " + string.Join(" | ", table.Fields.Select(f => $"{f.Name}={db.Get(table, i, f)}")));
            shown++;
        }
        return 0;
    }

    /// <summary>scorebug [--class FILTER] [--set ID=VALUE ...] [--sharp] [--dry FILE]: the score clock's style values, read now from the game; --set writes edits.</summary>
    private static int ScorebugCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var sb = Scorebug.Load(index);
        var sets = opts.All("set").Select(a => a.Split('=', 2)).ToDictionary(a => a[0], a => a[1]);
        bool sharp = opts.Has("sharp");
        foreach (var id in sets.Keys)
            if (!sb.Values.Any(v => v.Id == id)) throw new ArgumentException($"no value {id}");
        if (opts.Get("dry") is { } dry)
        {
            File.WriteAllBytes(dry, sb.Build(sets, sharp));
            Console.WriteLine($"built (not written to the game) -> {dry}");
            return 0;
        }
        if (sets.Count > 0 || sharp)
        {
            RefuseWhileRunning();
            var entry = Scorebug.Save(index, Mods(), game, sb, sets, Console.WriteLine, sharp);
            Console.WriteLine($"{entry.Summary}\nstate: {Mods().State(entry, game)}");
            sb = Scorebug.Load(index);
            foreach (var id in sets.Keys) Console.WriteLine($"read back: {id} = {sb.Values.First(v => v.Id == id).Display}");
            return 0;
        }
        string filter = opts.Get("class");
        foreach (var c in sb.Classes.Where(c => filter == null || c.Short.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(c.Short);
            foreach (var v in c.Values) Console.WriteLine($"  {v.Group}.{v.Key,-14} {v.Kind,-10} {v.Display}");
        }
        Console.WriteLine($"aliases: {sb.Aliases.Count}; logo path {sb.Find(Scorebug.LogoStyle, "logoProps", "path")?.Display} -> {sb.AliasTexture(sb.Find(Scorebug.LogoStyle, "logoProps", "path")?.Display)}; sharp badge: {sb.HasSharpBadge}");
        foreach (var m in new[] { "_logoMount", "_awayTeamMount", "_homeTeamMount", "_statsMount", "_timeMount" })
            Console.WriteLine($"mount {m}: {sb.Mount(m)}");
        return 0;
    }

    /// <summary>legacy-set NAME --file FILE [--collector C] [--note TEXT]: writes a whole legacy file into the game (through the mod list).</summary>
    private static int LegacySetCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("legacy-set needs a file name");
        string file = opts.Get("file") ?? throw new ArgumentException("legacy-set needs --file");
        string collector = opts.Get("collector") ?? LegacyEditor.CollectorOf(index, name) ?? throw new ArgumentException($"no collector has {name}");
        var data = File.ReadAllBytes(file);
        var entry = LegacyEditor.Save(index, Mods(), game, collector, name, data, opts.Get("note") ?? $"{Path.GetFileName(file)} ({data.Length:N0} bytes)", Console.WriteLine);
        foreach (var n in entry.Warnings) Console.WriteLine($"  note: {n}");
        Console.WriteLine($"state: {Mods().State(entry, game)}");
        var back = new Legacy(index, collector);
        var f = back.Find(name);
        bool same = back.Read(f).AsSpan().SequenceEqual(data);
        Console.WriteLine($"read back from the game: {name} at {f.Offset:N0}, {f.Size:N0} bytes, {(same ? "matches" : "DIFFERS")}");
        return same ? 0 : 1;
    }

    /// <summary>ebx-imports FILTER [--type T] [--match TEXT]: for every EBX whose name contains FILTER, the EBX it imports (only those containing TEXT).</summary>
    private static int EbxImportsCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "", type = opts.Get("type"), match = opts.Get("match") ?? "";
        var byId = new Dictionary<Guid, string>();
        foreach (var e in index.Ebx.Values) byId.TryAdd(e.Id, e.Name);
        foreach (var e in index.Ebx.Values.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) && (type == null || e.Type == type)).OrderBy(e => e.Name))
        {
            EbxFile f;
            try { f = EbxFile.Parse(index.Read(e)); } catch { continue; }
            if (f == null) continue;
            var names = f.Imports.Select(i => i.File).Distinct().Select(g => byId.GetValueOrDefault(g, g.ToString())).Where(n => n.Contains(match, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"{e.Name}	{string.Join(" | ", names)}");
        }
        return 0;
    }

    /// <summary>ebx-grep TEXT [--scan PREFIX] [--res]: the EBX (or RES) under PREFIX whose bytes contain TEXT (case-insensitive ASCII).</summary>
    private static int EbxGrepCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string text = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("ebx-grep needs text");
        string scan = opts.Get("scan") ?? "";
        var needle = System.Text.Encoding.ASCII.GetBytes(text.ToLowerInvariant());
        var entries = opts.Has("res")
            ? index.Res.Values.Where(e => e.Name.StartsWith(scan, StringComparison.OrdinalIgnoreCase)).Select(e => (e.Name, (Func<byte[]>)(() => index.Read(e))))
            : index.Ebx.Values.Where(e => e.Name.StartsWith(scan, StringComparison.OrdinalIgnoreCase) && e.Type != "TextureAsset").Select(e => (e.Name, (Func<byte[]>)(() => index.Read(e))));
        int hits = 0;
        Parallel.ForEach(entries, entry =>
        {
            byte[] b;
            try { b = entry.Item2(); } catch { return; }
            if (b == null) return;
            var low = new byte[b.Length];
            for (int i = 0; i < b.Length; i++) low[i] = b[i] is >= (byte)'A' and <= (byte)'Z' ? (byte)(b[i] + 32) : b[i];
            int at = low.AsSpan().IndexOf(needle);
            if (at < 0) return;
            int from = Math.Max(0, at - 60), to = Math.Min(b.Length, at + needle.Length + 80);
            string ctx = new string(b[from..to].Select(c => c is >= 32 and < 127 ? (char)c : '.').ToArray());
            lock (needle) { hits++; Console.WriteLine($"{entry.Name}\t@{at}\t{ctx}"); }
        });
        Console.Error.WriteLine($"{hits} hits");
        return 0;
    }

    /// <summary>ebx-refs NAME [--scan PREFIX]: what an EBX imports, and which EBX under PREFIX import it.</summary>
    private static int EbxRefsCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("ebx-refs needs an EBX name");
        var byId = new Dictionary<Guid, string>();
        foreach (var e in index.Ebx.Values) byId.TryAdd(e.Id, e.Name);
        var target = index.GetEbx(name) ?? throw new ArgumentException($"no ebx {name}");
        var file = EbxFile.Parse(index.Read(target));
        Console.WriteLine($"{name} ({target.Type}) imports:");
        foreach (var g in file.Imports.Select(i => i.File).Distinct())
            Console.WriteLine($"  {(byId.TryGetValue(g, out var n) ? n + "  (" + index.GetEbx(n)?.Type + ")" : g.ToString())}");
        foreach (int b in target.Bundles.Distinct())
        {
            var mates = index.Ebx.Values.Where(e => e.Bundles.Contains(b) && e.Type is not ("TextureAsset" or "BundleRefTableBlueprintBundle" or "BundleRefTableBlueprint")).Select(e => $"{e.Name} ({e.Type})").ToList();
            Console.WriteLine($"in bundle {index.BundleList[b].Name}: {mates.Count} non-texture ebx");
            foreach (var m in mates.Take(int.TryParse(opts.Get("mates"), out int mm) ? mm : 15)) Console.WriteLine($"    {m}");
        }
        string scan = opts.Get("scan");
        if (scan == null) return 0;
        if (scan == "*") scan = "";
        var skip = (opts.Get("skip") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"imported by (under {scan}):");
        foreach (var e in index.Ebx.Values.Where(e => e.Name.StartsWith(scan, StringComparison.OrdinalIgnoreCase)
                     && !skip.Any(k => e.Name.StartsWith(k, StringComparison.OrdinalIgnoreCase)) && e.Type is not ("TextureAsset" or "NewWaveAsset")))
        {
            EbxFile f;
            try { f = EbxFile.Parse(index.Read(e)); } catch { continue; }
            if (f != null && f.Imports.Any(i => i.File == target.Id))
                Console.WriteLine($"  {e.Name}  ({e.Type})");
        }
        return 0;
    }

    /// <summary>legacy [NAME [--out FILE]] [--filter TEXT] [--collector NAME|list]: one legacy file's contents, or every legacy file (databases, UI data, scripts) with its size.</summary>
    private static int LegacyCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string collector = opts.Get("collector") ?? Legacy.Main;
        if (collector == "list")
        {
            foreach (var c in Legacy.Collectors(index))
            {
                try { var l = new Legacy(index, c); Console.WriteLine($"{c}\t{l.Names.Count} files\tindex chunk {l.IndexChunk}"); }
                catch (Exception ex) { Console.WriteLine($"{c}\t({ex.Message})"); }
            }
            return 0;
        }
        var legacy = new Legacy(index, collector);
        if (opts.Positional.FirstOrDefault() is { } one)
        {
            var bytes = legacy.Read(legacy.Find(one) ?? throw new ArgumentException($"no legacy file {one}"));
            if (opts.Get("out") is { } outFile) { File.WriteAllBytes(outFile, bytes); Console.WriteLine($"{bytes.Length} bytes -> {outFile}"); }
            else Console.WriteLine(System.Text.Encoding.UTF8.GetString(bytes));
            return 0;
        }
        string filter = opts.Get("filter");
        if (opts.Get("out") is { } dir)
        {
            int count = 0;
            var chunks = new Dictionary<Guid, byte[]>();
            foreach (var n in legacy.Names.Where(n => filter == null || n.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                var f = legacy.Find(n);
                if (f == null) continue;
                if (!chunks.TryGetValue(f.ChunkId, out var chunk)) chunks[f.ChunkId] = chunk = legacy.ReadChunk(f.ChunkId);
                string dst = Path.Combine(dir, n.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.WriteAllBytes(dst, chunk.AsSpan(f.Offset, f.Size).ToArray());
                count++;
            }
            Console.WriteLine($"{count} files -> {dir}");
            return 0;
        }
        foreach (var n in legacy.Names.Where(n => filter == null || n.Contains(filter, StringComparison.OrdinalIgnoreCase)).OrderBy(n => n))
            Console.WriteLine(opts.Get("where") != null && legacy.Find(n) is { } lf ? $"{n}\t{lf.Size}\t{lf.ChunkId}\t{lf.Offset}" : $"{n}\t{legacy.Find(n)?.Size}");
        return 0;
    }

    /// <summary>db [NAME] [--table T] [--rows N] [--find TEXT]: the game's databases, read now from its files.</summary>
    private static int DbCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var legacy = new Legacy(index);
        string name = opts.Positional.FirstOrDefault();
        if (name == null)
        {
            foreach (var n in legacy.Names.Where(n => n.EndsWith(".db", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n))
                Console.WriteLine($"  {n,-60} {legacy.Find(n)?.Size,12:N0} bytes");
            return 0;
        }
        // --file DB: a database from disk (a save's league database), named by the game database's meta
        var db = opts.Get("file") is { } file
            ? new T3Db(File.ReadAllBytes(file), System.Text.Encoding.UTF8.GetString(legacy.Read(name[..^3] + "-meta.xml")).TrimStart('﻿'))
            : legacy.OpenDb(name);
        var (ok, total) = db.CheckCrcs();
        Console.WriteLine($"{name}: {db.Tables.Count} tables, CRCs {ok}/{total} match" + (db.Source != null ? $" (chunk {db.Source.ChunkId} at {db.Source.Offset:N0})" : ""));
        if (opts.Has("crcs"))
            foreach (var c in db.CrcReport())
                Console.WriteLine($"  {(c.Ok ? "ok " : "BAD")} {c.What,-40} bytes {c.From:N0}-{c.CrcPos:N0}");
        if (opts.Has("shared"))
            foreach (var t in db.Tables.Where(t => t.HasCompressedStrings))
            {
                // strings stored once and pointed at by more than one field
                var offs = new Dictionary<int, int>();
                foreach (var f in t.Fields.Where(f => f.IsCompressed))
                    for (int rec = 0; rec < t.WrittenRecords; rec++)
                        if (BitConverter.ToInt32(db.Data, t.RecordsStart + rec * t.RecordSize + f.BitOffset / 8) is var o && o != -1) offs[o] = offs.GetValueOrDefault(o) + 1;
                int shared = offs.Count(x => x.Value > 1);
                if (shared > 0 || opts.Has("all")) Console.WriteLine($"  {t.Name,-40} {offs.Count,7} strings, {shared} shared by {offs.Where(x => x.Value > 1).Sum(x => x.Value)} values");
            }
        string tname = opts.Get("table");
        if (tname == null)
        {
            foreach (var t in db.Tables.OrderBy(t => t.Name))
                Console.WriteLine($"  {t.Name,-40} {t.WrittenRecords,6} rows {t.Fields.Count,4} fields  (room for {t.Records}, {Enumerable.Range(0, t.WrittenRecords).Count(i => db.IsInvalid(t, i))} empty, {t.RecordSize} bytes each)");
            return 0;
        }
        var table = db.Table(tname) ?? throw new ArgumentException($"no table {tname}");
        if (opts.Has("fields"))
            Console.WriteLine("  " + string.Join(" | ", table.Fields.Select(f => $"{f.Name}:{f.Type}/{f.Depth}" + (f.Type == T3FieldType.Integer ? $" [{f.RangeLow}..]" : ""))));
        int rows = int.TryParse(opts.Get("rows"), out int r) ? r : 5;
        string find = opts.Get("find");
        int shown = 0;
        for (int i = 0; i < table.WrittenRecords && shown < rows; i++)
        {
            if (db.IsInvalid(table, i)) continue;
            string line = string.Join(" | ", table.Fields.Select(f => $"{f.Name}={db.Get(table, i, f)}"));
            if (find != null && !line.Contains(find, StringComparison.OrdinalIgnoreCase)) continue;
            Console.WriteLine($"  [{i}] {line}");
            shown++;
        }
        return 0;
    }

    /// <summary>
    /// ps4-sigtest NAME [--write]: NHL 24's TOC signature test (PLAN §28 step 3). The BC3 texture's colours are inverted
    /// (each colour block's two endpoints flipped), stored uncompressed so it no longer fits, and added at the end of its
    /// CAS file; its TOC entries are repointed and the signature is left as it was. With --write it goes into the game
    /// as a mod entry in mods-nhl24 (its old bytes kept).
    /// </summary>
    private static int Ps4SigTestCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        if (index.Layout.IsPs5) throw new ArgumentException("ps4-sigtest is for NHL 24 (PS4)");
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("ps4-sigtest needs a texture name");
        var res = index.GetRes(name) ?? throw new ArgumentException($"no texture {name}");
        var h = TextureHeader.Parse(index.Read(res), ps4: true);
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var copies = ChunkCopies.Find(index, h.ChunkId, tocs);
        foreach (var c in copies)
            Console.WriteLine($"  {(c.Live ? "live" : "old "),-4} {c.Kind,-6} {Path.GetFileName(c.Toc),-24} {(c.InBundle ? c.BundleName : "chunk table"),-50} cas {c.Ref.Catalog}/{c.Ref.Cas} at 0x{c.Ref.Offset:X} size {c.Ref.Size:N0}");
        byte[] stored = index.ReadStored(copies.First(c => c.Live).Ref);
        ChunkPlan plan;
        Texture preview;
        if (h.Format.Name == "BC3" && h.MipCount == 1 && h.FirstMip == 0)
        {
            byte[] raw = FrostbiteBlocks.Decode(stored);
            // BC3: 8 bytes alpha, then the colour block's two RGB565 endpoints; flipping them inverts the colours
            for (int i = 0; i + 16 <= raw.Length; i += 16)
                for (int e = 8; e < 12; e += 2)
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(i + e),
                        (ushort)~System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(i + e)));
            byte[] newStored = FrostbiteBlocks.Store(raw);
            plan = ChunkGrower.Plan(index, h.ChunkId, newStored, loc, tocs);
            Console.WriteLine($"{name}: {stored.Length:N0} stored bytes -> {newStored.Length:N0} (uncompressed, colours inverted)");
            preview = new Texture { Name = name, Header = h, Data = raw };
            preview.DecodeMip(0);
        }
        else
        {
            // any other texture: the picture's colours inverted and the whole chain encoded again (NHL 24's replace path)
            var t = Texture.Load(index, name);
            var rgba = (byte[])t.Rgba.Clone();
            for (int i = 0; i < rgba.Length; i += 4)
                for (int k = 0; k < 3; k++) rgba[i + k] = (byte)(255 - rgba[i + k]);
            var tp = Replacements.PlanTexture(index, name, rgba, loc, tocs, Console.WriteLine);
            plan = tp.Chunk;
            preview = tp.Result;
            Console.WriteLine($"{name}: {h.Format.Name} {h.Width}x{h.Height}, {TextureBuilder.StoredMips(h)} mips encoded again; mip offsets {h.MipOffsets[0]},{h.MipOffsets[1]} -> {tp.Header.MipOffsets[0]},{tp.Header.MipOffsets[1]}");
        }
        foreach (var p in plan.Patches)
            Console.WriteLine($"    {p.Note,-22} {p.New.Length,10:N0} bytes at 0x{p.Offset:X} in {p.File}{(p.Append ? " (appended)" : "")}");
        foreach (var n in plan.Notes) Console.WriteLine($"    note: {n}");
        var tocFiles = plan.Patches.Where(p => p.File.EndsWith(".toc")).Select(p => p.File).Distinct().ToList();
        Console.WriteLine($"  signed TOCs changed, signature left as it is: {string.Join(", ", tocFiles)}");
        string outDir = opts.Get("out") ?? Path.Combine(KitPaths.Export, "ps4-sigtest");
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, Path.GetFileName(name) + "_inverted.png"), preview.ToPng());
        Console.WriteLine($"  preview: {outDir}");
        if (!opts.Has("write")) return 0;

        if (Emulators.Running() is { } emu) throw new InvalidOperationException($"{emu} is running. Close the game before changing its files.");
        if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
            throw new InvalidOperationException("The kit is open. Close it before changing the game's files.");
        if (Directory.Exists(game.TrimEnd('\\', '/') + "-mods")) throw new InvalidOperationException("A -mods overlay folder is next to the game; the emulator would load it over the game.");
        var store = Mods();
        if (store.Find("texture", name) is { } had && store.State(had, game) != ModState.NotApplied)
            throw new InvalidOperationException($"{name} already has a mod entry in the game ({had.Summary}).");
        var entry = store.Add("texture", name, "ps4-sigtest", "Signature test: colours inverted, stored uncompressed and added at the end of its CAS file; TOC signature left as it was",
            plan.Patches, plan.Notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>nhl24pc --install DIR [--relink DUMP]: where NHL24 PC's game link points, and repoints it.</summary>
    private static int Nhl24PcCmd(Options opts)
    {
        string install = opts.Get("install") ?? throw new ArgumentException("--install DIR");
        Console.WriteLine($"{Nhl24Pc.GameLink(install)} -> {Nhl24Pc.LinkTarget(install) ?? "(not a link)"}");
        if (opts.Get("relink") is { } dump)
        {
            string old = Nhl24Pc.Relink(install, dump);
            Console.WriteLine($"relinked (was {old ?? "nothing"}): now -> {Nhl24Pc.LinkTarget(install)}");
        }
        return 0;
    }

    /// <summary>kyty-releases [--install latest|latest:OWNER|TAG] [--dir DIR]: lists or installs KytyPS5 builds from the NHL forks.</summary>
    private static int KytyReleasesCmd(Options opts)
    {
        string dir = opts.Get("dir") ?? KytyReleases.BuildsDir;
        var (releases, errors) = KytyReleases.ListAllAsync().GetAwaiter().GetResult();
        foreach (string e in errors)
            Console.WriteLine("could not list " + e);
        foreach (var r in releases)
            Console.WriteLine($"{r.Owner,-10} {r.Tag,-30} {r.Published:yyyy-MM-dd HH:mm}  {r.Size / 1048576.0,6:0.0} MB  {(KytyReleases.Installed(r, dir) != null ? "installed" : "")}");
        if (opts.Get("install") is { } tag)
        {
            // latest, latest:OWNER, or a tag
            var r = tag == "latest" ? releases.First()
                : tag.StartsWith("latest:") ? releases.First(x => x.Owner.Equals(tag[7..], StringComparison.OrdinalIgnoreCase))
                : releases.First(x => x.Tag == tag);
            int last = -1;
            string exe = KytyReleases.InstallAsync(r, new Progress<(long Done, long Total)>(p =>
            {
                int pct = (int)(p.Done * 100 / Math.Max(1, p.Total));
                if (pct / 10 != last) { last = pct / 10; Console.WriteLine($"  {pct}%"); }
            }), dir).GetAwaiter().GetResult();
            Console.WriteLine($"installed: {exe} ({new FileInfo(exe).Length:N0} bytes)");
        }
        return 0;
    }

    /// <summary>db-set NAME --table T --row R --field F --value V: changes one value in the game's database.</summary>
    private static int DbSetCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("db-set needs a database name");
        string tname = opts.Get("table") ?? throw new ArgumentException("db-set needs --table");
        int row = int.Parse(opts.Get("row") ?? throw new ArgumentException("db-set needs --row"));
        string fname = opts.Get("field") ?? throw new ArgumentException("db-set needs --field");
        string value = opts.Get("value") ?? throw new ArgumentException("db-set needs --value");
        var entry = DbEditor.Save(index, Mods(), game, name, db =>
        {
            var t = db.Table(tname) ?? throw new ArgumentException($"no table {tname}");
            var f = t.Fields.FirstOrDefault(x => x.Name.Equals(fname, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no field {fname}");
            object old = db.Get(t, row, f);
            db.Set(t, row, f, f.Type == T3FieldType.Integer ? int.Parse(value) : f.Type == T3FieldType.Float ? float.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : value);
            return $"{t.Name}[{row}].{f.Name}: {old} â†’ {value}";
        }, Console.WriteLine);
        Console.WriteLine($"{entry.Summary}\nstate: {Mods().State(entry, game)}");
        // read it back from the game files
        var back = new Legacy(index).OpenDb(name);
        var bt = back.Table(tname);
        var bf = bt.Fields.First(x => x.Name.Equals(fname, StringComparison.OrdinalIgnoreCase));
        var (ok, total) = back.CheckCrcs();
        Console.WriteLine($"read back from the game: {bt.Name}[{row}].{bf.Name} = {back.Get(bt, row, bf)} (CRCs {ok}/{total})");
        return 0;
    }

    /// <summary>mesh-blend NAME: per chunk section, the distinct blend-index values (usage 0x02) its vertices use.</summary>
    private static int MeshBlendCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("mesh-blend needs a mesh name");
        foreach (var s in MeshWriter.Sections(index.Read(index.GetRes(name))))
        {
            var chunk = index.Read(index.GetChunk(s.Chunk));
            int at = s.VertexOffset;
            var counts = new SortedDictionary<string, int>();
            foreach (var (usage, format, size) in s.Elements)
            {
                if (usage == 2)
                    for (int v = 0; v < s.VertexCount; v++)
                    {
                        string k = Convert.ToHexString(chunk, at + v * size, size);
                        counts[k] = counts.GetValueOrDefault(k) + 1;
                    }
                at += size * s.VertexCount;
            }
            Console.WriteLine($"LOD{s.Lod} s{s.Index} {s.Material,-28} {counts.Count} values: {string.Join(" ", counts.OrderByDescending(x => x.Value).Take(8).Select(x => $"{x.Key}x{x.Value}"))}");
        }
        return 0;
    }

    /// <summary>mesh NAME [--obj FILE] [--lod N] [--verts TEXT]: reads a MeshSet and prints its LODs and parts (and the vertices of parts named TEXT); writes an OBJ.</summary>
    /// <summary>
    /// The menu idle on the player skeleton: idle [--t SECONDS] [--grip 0..1] [--obj FILE] [--mesh NAME]... poses the default kit
    /// meshes (or the named ones) at that time and writes them as one OBJ.
    /// </summary>
    private static int IdleCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var idle = PlayerIdle.Load(index);
        if (float.TryParse(opts.Get("grip"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var grip)) idle.GripAmount = grip;
        Console.WriteLine($"skeleton {idle.Skeleton.Count} bones; clip {idle.Animation.Frames} frames ({idle.Duration:0.0} s), {idle.Animation.Quats} rotations, {idle.Animation.Vectors} vectors, {idle.Animation.Floats} floats");
        double t = double.TryParse(opts.Get("t"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tt) ? tt : 0;
        var skin = idle.Skin(t);
        var hp = System.Numerics.Vector3.Transform(idle.Skeleton.Model[idle.Skeleton.IndexOf("Head")].Translation, skin[idle.Skeleton.IndexOf("Head")]);
        Console.WriteLine(FormattableString.Invariant($"head at {hp.X:0.000}, {hp.Y:0.000}, {hp.Z:0.000}"));
        if (opts.Get("obj") is not { } obj) return 0;
        var names = opts.All("mesh").ToList();
        if (names.Count == 0)
            names = new[] { "jersey/adidas/components/player_jersey_adidas_notuck_large_mesh", "pant/common/components/player_pant_common_looselong_mesh",
                "sock/common/components/player_sock_common_notuck_mesh" }.Select(n => "content/character/skater/player/" + n)
                .Append(KitEquipment.HelmetMesh).Append(KitEquipment.GloveMesh("left")).Append(KitEquipment.GloveMesh("right")).Append(KitEquipment.HeadMesh).Concat(KitEquipment.StickMeshes(opts.Get("hand") ?? "left")).ToList();
        var sb = new System.Text.StringBuilder();
        int baseVertex = 1;
        foreach (var name in names)
            foreach (var part in Kits.LoadMesh(index, name).Lods[0].Where(p => p.Uvs != null))
            {
                var pos = PoseSkin.Positions(part, skin, idle.Skeleton.IndexOf("Head"));
                sb.Append("o ").AppendLine(part.Material);
                for (int v = 0; v < part.VertexCount; v++)
                    sb.Append(FormattableString.Invariant($"v {pos[v * 3]} {pos[v * 3 + 1]} {pos[v * 3 + 2]}\n"));
                for (int i = 0; i < part.Indices.Length; i += 3)
                    sb.Append($"f {part.Indices[i] + baseVertex} {part.Indices[i + 1] + baseVertex} {part.Indices[i + 2] + baseVertex}\n");
                baseVertex += part.VertexCount;
                Console.WriteLine($"  {name.Split('/').Last()} {part.Material}: {part.VertexCount} verts, {part.Influences} influences");
            }
        File.WriteAllText(obj, sb.ToString());
        Console.WriteLine($"wrote {obj}");
        return 0;
    }

    private static int MeshCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("mesh needs a mesh name");
        var res = index.GetRes(name) ?? throw new ArgumentException($"no RES named {name}");
        var mesh = new MeshSet(index.Read(res), id => index.GetChunk(id) is { } c ? index.Read(c) : null);
        Console.WriteLine($"{mesh.Name}  bounds {string.Join(",", mesh.Bounds.Min.Select(v => v.ToString("0.00")))} .. {string.Join(",", mesh.Bounds.Max.Select(v => v.ToString("0.00")))}  meta {Convert.ToHexString(res.ResMeta ?? Array.Empty<byte>())}");
        for (int l = 0; l < mesh.Lods.Count; l++)
            foreach (var p in mesh.Lods[l].Where(p => p.Uvs != null))
            {
                float uMin = p.Uvs.Where((_, i) => i % 2 == 0).Min(), uMax = p.Uvs.Where((_, i) => i % 2 == 0).Max();
                float vMin = p.Uvs.Where((_, i) => i % 2 == 1).Min(), vMax = p.Uvs.Where((_, i) => i % 2 == 1).Max();
                Console.WriteLine($"  lod{l} {p.Material,-20} {p.VertexCount,6} verts {p.TriangleCount,6} tris  max index {p.Indices.Max()}  uv {uMin:0.00}..{uMax:0.00} x {vMin:0.00}..{vMax:0.00}  [{p.Layout}]" +
                    (p.Uvs2 != null ? $"  uv2 {p.Uvs2.Where((_, i) => i % 2 == 0).Min():0.00}..{p.Uvs2.Where((_, i) => i % 2 == 0).Max():0.00} x {p.Uvs2.Where((_, i) => i % 2 == 1).Min():0.00}..{p.Uvs2.Where((_, i) => i % 2 == 1).Max():0.00}" : ""));
            }
        // --verts TEXT: each vertex (position, UV, UV2) of the LOD 0 parts whose material has TEXT
        if (opts.Get("verts") is { } verts)
            foreach (var p in mesh.Lods[0].Where(p => (p.Material ?? "").Contains(verts, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine(p.Material);
                for (int v = 0; v < p.VertexCount; v++)
                    Console.WriteLine(FormattableString.Invariant($"  ({p.Positions[v * 3]:0.###}, {p.Positions[v * 3 + 1]:0.###}, {p.Positions[v * 3 + 2]:0.###})  uv {p.Uvs?[v * 2]:0.####},{p.Uvs?[v * 2 + 1]:0.####}  uv2 {p.Uvs2?[v * 2]:0.###},{p.Uvs2?[v * 2 + 1]:0.###}"));
            }
        if (opts.Get("obj") is { } obj)
        {
            File.WriteAllText(obj, mesh.ToObj(int.TryParse(opts.Get("lod"), out int lod) ? lod : 0));
            Console.WriteLine($"wrote {obj}");
        }
        return 0;
    }

    /// <summary>kits [FILTER]: the uniform kits (league/team/brand/kit) and, for one match, its texture slots.</summary>
    private static int KitsCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "";
        var kits = Kits.Find(index).Where(k => k.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"{kits.Count} kits");
        foreach (var k in kits.Take(40)) Console.WriteLine($"  {k}  ({k.Textures.Count} textures)");
        if (kits.Count == 1)
            foreach (var t in kits[0].Textures) Console.WriteLine($"    {t.Slot,-34} {t.Garment,-10} {t.Map,-7} {t.Name}");
        return 0;
    }

    /// <summary>
    /// kit-stamp [FILTER] [--garment jersey|pant] [--size N] [--out DIR] [--limit N]: builds each matching kit's garment
    /// from its base and stamp (as the game does) and compares it with the pre-baked …_color texture. With --out, writes
    /// the composite and the baked texture as PNGs.
    /// </summary>
    /// <summary>
    /// jersey-studio KIT [--template N] [--colors #RRGGBB,…] [--move ID:TX,TY] [--out FILE.png] [--plan]: draws a kit's
    /// jersey with a create-a-team design and moved hotspots, checks the stamp rewrite, and (--plan) plans writing the stamp.
    /// Nothing is written into the game.
    /// </summary>
    private static int JerseyStudioCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "nhl/toronto/adidas/home";
        var kit = Kits.Find(index).First(k => k.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) && k.IsStamped("jersey"));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var templates = JerseyStudio.Templates(index);
        Console.WriteLine($"{kit}: {templates.Count} designs ({string.Join(", ", templates.GroupBy(t => t.Brand).Select(g => $"{g.Count()} {g.Key}"))})");
        var src = JerseyStudio.LoadSource(index, kit, 2048);
        Console.WriteLine($"  source read in {sw.ElapsedMilliseconds} ms: {src.Spots.Count} hotspots, logos on layers {string.Join(",", src.Logos.Keys)}");
        var regions = new long[8];
        for (int i = 3; i < src.Base.Length; i += 4) regions[Math.Min(7, JerseyStudio.Region(src.Base[i]))]++;
        Console.WriteLine("  fabric regions: " + string.Join("  ", regions.Select((c, r) => (c, r)).Where(x => x.c > 0).Select(x => $"{x.r}:{100.0 * x.c / (2048 * 2048):0.0}%")));

        if (opts.Has("pose"))
        {
            var jm = Kits.LoadMesh(index, Kits.Meshes(index, kit).First(m => m.Garment == "jersey").Mesh);
            var map = SurfaceMap.Build(jm.Lods[0], 512);
            Console.WriteLine($"  jersey bounds {string.Join(",", map.Bounds.Select(b => b.ToString("0.00")))}");
            for (double bx = 0; bx < 0.6; bx += 0.04)
            {
                var ys = Enumerable.Range(0, 512 * 512).Where(i => map.Covered(i) && Math.Abs(map.Position[i * 3]) >= bx && Math.Abs(map.Position[i * 3]) < bx + 0.04).Select(i => map.Position[i * 3 + 1]).ToList();
                if (ys.Count > 0) Console.WriteLine($"   |x| {bx:0.00}: {ys.Count,6} texels  y {ys.Min():0.00}..{ys.Max():0.00}");
            }
        }
        var d = new StudioDesign { Spots = src.Spots.ToList() };
        var colors = (opts.Get("colors") ?? "#00205B,#FFFFFF,#7F8C99").Split(',').Select(c => Convert.ToInt32(c.TrimStart('#'), 16)).ToList();
        if (int.TryParse(opts.Get("template"), out int tn))
        {
            var t = templates.First(x => x.Number == tn && x.Brand == (opts.Get("brand") ?? "adidas"));
            sw.Restart();
            d.Pattern = JerseyStudio.LoadPattern(index, t, 2048);
            Console.WriteLine($"  {t.Label}: {t.Masks.Count} masks, zones {string.Join(" ", d.Pattern.Zones.Select(z => $"{z.Zone}:{z.Share:P1}"))} ({sw.ElapsedMilliseconds} ms)");
            for (int i = 0; i < d.Pattern.Zones.Count; i++) d.ZoneColors[d.Pattern.Zones[i].Zone] = colors[i % colors.Count];
        }
        foreach (var mv in opts.All("move"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(mv, @"^(\d+):([\d.]+),([\d.]+)(?:,([\d.]+))?$");
            int id = int.Parse(m.Groups[1].Value);
            int at = d.Spots.FindIndex(s => s.Id == id);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            d.Spots[at] = d.Spots[at] with { Enabled = true, Tx = double.Parse(m.Groups[2].Value, ci), Ty = double.Parse(m.Groups[3].Value, ci) };
            if (m.Groups[4].Success) { double k = double.Parse(m.Groups[4].Value, ci); d.Spots[at] = d.Spots[at] with { Sx = d.Spots[at].Sx * k, Sy = d.Spots[at].Sy * k }; }
        }
        // --line body|sleeves:FROM,WIDTH,#RRGGBB[,#EDGE,EDGEWIDTH] (cm)
        foreach (var ln in opts.All("line"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(ln, @"^(body|sleeves):([\d.]+),([\d.]+),#?(\w{6})(?:,#?(\w{6}),([\d.]+))?$");
            if (!m.Success) throw new ArgumentException($"--line {ln}: body|sleeves:FROM,WIDTH,#RRGGBB[,#EDGE,EDGEWIDTH]");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            d.Lines.Add(new StudioLine(m.Groups[1].Value == "body" ? LinePlace.Body : LinePlace.Sleeves, double.Parse(m.Groups[2].Value, ci), double.Parse(m.Groups[3].Value, ci),
                Convert.ToInt32(m.Groups[4].Value, 16), m.Groups[5].Success ? Convert.ToInt32(m.Groups[5].Value, 16) : null, m.Groups[6].Success ? double.Parse(m.Groups[6].Value, ci) : 0));
        }
        if (src.Shape is { } shape)
            Console.WriteLine($"  shape: hem y {shape.Hem:0.000}, arm ({shape.DirX:0.00},{shape.DirY:0.00}), armpit |x| {shape.PitX:0.000} y {shape.PitY:0.000}, cuff {shape.Cuff * 100:0.0} cm past it");
        if (opts.Has("free"))
        {
            var db = new Legacy(index).OpenDb(JerseySlots.Database);
            var kits = Kits.Find(index);
            var teams = JerseySlots.Teams(db, kits);
            var used = JerseySlots.Used(db);
            var free = kits.Where(k => teams.ContainsKey((k.League, k.Team)) && JerseySlots.Address(k) is { } a && !used.Contains((teams[(k.League, k.Team)].ArtId, a.Style, a.Variant))).ToList();
            Console.WriteLine($"  {free.Count} of {kits.Count} kit folders are in no team's menu; {free.Count(k => k.IsStamped("jersey"))} of them stamped");
            foreach (var g in free.GroupBy(k => k.League).OrderByDescending(g => g.Count())) Console.WriteLine($"    {g.Key}: {g.Count()}  e.g. {string.Join(", ", g.Take(3))}");
        }
        sw.Restart();
        var painted = JerseyStudio.PaintBase(src, d);
        var drawn = JerseyStudio.Draw(src, d, painted, new KitLettering("PLAYER", "27", "C"));
        Console.WriteLine($"  painted and drawn in {sw.ElapsedMilliseconds} ms");
        if (opts.Get("out") is { } outFile) { File.WriteAllBytes(outFile, ImageFiles.Png(drawn, 2048, 2048)); Console.WriteLine($"  -> {outFile}"); }

        // the stamp: unchanged hotspots give the same bytes; edited ones the same length and read back as set
        var same = KitStamp.Write(src.StampEbx, "jersey", src.Spots);
        Console.WriteLine($"  stamp round trip: {(same.AsSpan().SequenceEqual(src.StampEbx) ? "identical" : "DIFFERENT")}");
        var edited = KitStamp.Write(src.StampEbx, "jersey", d.Spots);
        var back = KitStamp.Parse(edited, "jersey");
        bool ok = edited.Length == src.StampEbx.Length && back.Zip(d.Spots).All(p => p.First.Enabled == p.Second.Enabled
            && Math.Abs(p.First.Tx - p.Second.Tx) < 1e-4 && Math.Abs(p.First.Ty - p.Second.Ty) < 1e-4 && Math.Abs(p.First.Sx - p.Second.Sx) < 1e-4);
        Console.WriteLine($"  edited stamp: {edited.Length} bytes (was {src.StampEbx.Length}), reads back {(ok ? "as set" : "WRONG")}");
        if (opts.Has("plan"))
        {
            var tocs = new TocCache(index);
            var plan = ResPatcher.PlanEbx(index, kit.Stamps["jersey"], edited, LocationIndex.Build(index, tocs), tocs);
            foreach (var p in plan.Patches) Console.WriteLine($"  patch {p.File} 0x{p.Offset:X} {p.Old.Length} bytes ({p.Note})");
            foreach (var n in plan.Notes) Console.WriteLine($"  note: {n}");
        }
        return 0;
    }

    /// <summary>
    /// faceoff [NAME] [--league nhl] [--pages N]: lists TheFaceoff.net rinks; with a name, finds the first match's top-down
    /// layout and downloads it to export/faceoff/downloads. Writes nothing into the game.
    /// </summary>
    private static int FaceoffCmd(Options opts)
    {
        var (key, label) = FaceoffNet.Leagues.First(l => l.Key == (opts.Get("league") ?? "nhl"));
        var league = new FaceoffLeague { Key = key, Label = label };
        int pages = int.TryParse(opts.Get("pages"), out int pg) ? pg : 1;
        string want = opts.Positional.FirstOrDefault();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < pages && !league.Done; i++) FaceoffNet.MoreAsync(league).GetAwaiter().GetResult();
        Console.WriteLine($"{league.Label}: {league.Rinks.Count} rinks from {league.Pages} page(s), pager {league.Pager}, more: {!league.Done} ({sw.ElapsedMilliseconds} ms)");
        if (want == null) { foreach (var r in league.Rinks) Console.WriteLine($"  {r.Name}  {r.Url}"); return 0; }
        var rink = league.Rinks.FirstOrDefault(r => r.Name.Contains(want, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no rink like {want}");
        var (title, layout) = FaceoffNet.RinkAsync(rink.Url).GetAwaiter().GetResult();
        Console.WriteLine($"  {title}: layout {layout ?? "none"}");
        if (layout == null) return 1;
        string file = FaceoffNet.DownloadAsync(layout, Path.Combine(KitPaths.Export, "faceoff", "downloads")).GetAwaiter().GetResult();
        Console.WriteLine($"  downloaded {new FileInfo(file).Length:N0} bytes -> {file} (convert it in the app: Teams, Logos, From TheFaceoff.net)");
        return 0;
    }

    private static int KitStampCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "", garment = opts.Get("garment") ?? "jersey", outDir = opts.Get("out");
        int size = int.TryParse(opts.Get("size"), out int sz) ? sz : 256, limit = int.TryParse(opts.Get("limit"), out int lim) ? lim : int.MaxValue;
        var kits = Kits.Find(index).Where(k => k.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) && k.IsStamped(garment)).Take(limit).ToList();
        Console.WriteLine($"{kits.Count} stamped kits");
        foreach (var kit in kits)
        {
            try
            {
                if (opts.Has("spots"))
                    foreach (var (s, layer) in KitStamp.Brands(KitStamp.Parse(index.Read(index.GetEbx(kit.Stamps[garment])), garment)))
                        Console.WriteLine($"    {(s.Enabled ? "on " : "off")} {s.Name,-8} layer {layer}  r={s.R} s={s.Sx}x{s.Sy} t=({s.Tx},{s.Ty})");
                var lettering = opts.Get("name") != null || opts.Get("number") != null ? new KitLettering(opts.Get("name"), opts.Get("number"), opts.Get("captain")) : null;
                var c = Kits.Compose(index, kit, garment, size, lettering).Value;
                string diff = "no baked texture";
                var bakedSlot = kit.Slot($"{garment}_color");
                Texture baked = null;
                if (bakedSlot != null)
                {
                    baked = Texture.Load(index, bakedSlot.Name, decode: false);
                    baked.DecodeMip(baked.MipForSize(size));
                    if (baked.Width == c.Width && baked.Height == c.Height)
                    {
                        long sum = 0; int far = 0, n = c.Width * c.Height;
                        for (int i = 0; i < n; i++)
                        {
                            int d = 0;
                            for (int k = 0; k < 3; k++) d += Math.Abs(c.Rgba[i * 4 + k] - baked.Rgba[i * 4 + k]);
                            sum += d;
                            if (d > 90) far++;
                        }
                        diff = $"mean diff {sum / (3.0 * n):0.00}, {100.0 * far / n:0.00}% pixels differ";
                    }
                    else diff = $"baked is {baked.Width}x{baked.Height}";
                }
                Console.WriteLine($"  {kit}  {c.Width}x{c.Height}  {diff}");
                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);
                    string stem = Path.Combine(outDir, kit.ToString().Replace('/', '_') + "_" + garment);
                    File.WriteAllBytes(stem + "_stamped.png", ImageFiles.Png(c.Rgba, c.Width, c.Height));
                    if (baked?.Rgba != null) File.WriteAllBytes(stem + "_baked.png", ImageFiles.Png(baked.Rgba, baked.Width, baked.Height));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"  {kit}  ! {e.Message}");
            }
        }
        return 0;
    }

    /// <summary>
    /// kit-helmet [FILTER] --out DIR: the helmet's second UV set (u red, v green) drawn in its texture space, and the
    /// kit's helmet logo stamped through it over the team-coloured helmet. Writes nothing into the game.
    /// </summary>
    /// <summary>collar [KIT]: the collar's pieces in the jersey texture, each texture island of them with its size and where it is on the model.</summary>
    private static int CollarCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "nhl/anaheim/adidas/home";
        var kit = Kits.Find(index).First(k => k.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) && k.IsStamped("jersey"));
        var src = JerseyStudio.LoadSource(index, kit, 2048);
        var map = src.Collar;
        var pos = src.Shape.Map.Position;
        int size = map.Size, n = size * size;
        var seen = new bool[n];
        var queue = new Queue<int>();
        Console.WriteLine($"{kit}: hem {src.Shape.Hem:0.000}");
        for (int start = 0; start < n; start++)
        {
            int region = JerseyStudio.Region(src.Base[start * 4 + 3]);
            if (seen[start] || float.IsNaN(pos[start * 3]) || (map.Part[start] == CollarPart.None && region < 5)) continue;
            var part = map.Part[start];
            double sy = 0, sz = 0, sx = 0, minY = 9, maxY = -9;
            int count = 0, minU = size, maxU = 0, minV = size, maxV = 0;
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % size, y = i / size;
                count++;
                sx += Math.Abs(pos[i * 3]); sy += pos[i * 3 + 1]; sz += pos[i * 3 + 2];
                minY = Math.Min(minY, pos[i * 3 + 1]); maxY = Math.Max(maxY, pos[i * 3 + 1]);
                minU = Math.Min(minU, x); maxU = Math.Max(maxU, x); minV = Math.Min(minV, y); maxV = Math.Max(maxV, y);
                foreach (int j in new[] { x > 0 ? i - 1 : -1, x < size - 1 ? i + 1 : -1, i - size, i + size })
                    if (j >= 0 && j < n && !seen[j] && !float.IsNaN(pos[j * 3]) && map.Part[j] == part && JerseyStudio.Region(src.Base[j * 4 + 3]) == region)
                    { seen[j] = true; queue.Enqueue(j); }
            }
            if (count < 20) continue;
            if (opts.Has("hole") && map.Hole != null)
            {
                var hist = new SortedDictionary<int, (int N, double Y)>();
                for (int i = 0; i < n; i++)
                    if (map.Part[i] == part && JerseyStudio.Region(src.Base[i * 4 + 3]) == region && map.Hole[i] < float.MaxValue)
                    {
                        int bin = (int)(map.Hole[i] / 5) * 5;
                        hist.TryGetValue(bin, out var h);
                        hist[bin] = (h.N + 1, h.Y + pos[i * 3 + 1]);
                    }
                Console.WriteLine($"  {part} hole distance: " + string.Join("  ", hist.Select(kv => FormattableString.Invariant($"{kv.Key}:{kv.Value.N}@{kv.Value.Y / kv.Value.N:0.000}"))));
            }
            Console.WriteLine(FormattableString.Invariant($"{part,-10} r{region}  {count,7} texels  uv {minU}-{maxU} x {minV}-{maxV}  y {minY:0.000}..{maxY:0.000}  mean |x| {sx / count:0.000} z {sz / count:+0.000;-0.000}"));
        }
        return 0;
    }

    /// <summary>jersey-template [KIT] --out FILE.dds [--size N]: the jersey texture guide (parts, 10 cm lines, logo spots), as DDS and PNG.</summary>
    private static int JerseyTemplateCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string filter = opts.Positional.FirstOrDefault() ?? "nhl/anaheim/adidas/home", file = opts.Get("out") ?? "jersey_template.dds";
        int size = int.TryParse(opts.Get("size"), out int sz) ? sz : 2048;
        var kit = Kits.Find(index).First(k => k.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) && k.IsStamped("jersey"));
        if (opts.Has("measure"))
        {
            var sh = JerseyStudio.LoadSource(index, kit, 512).Shape;
            var pp = sh.Map.Position;
            double backTop = 0, frontTop = 0;
            for (int i = 0; i < pp.Length / 3; i++)
            {
                if (float.IsNaN(pp[i * 3]) || Math.Abs(pp[i * 3]) > 0.03) continue;
                double cm = (pp[i * 3 + 1] - sh.Hem) * 100;
                if (pp[i * 3 + 2] < 0) backTop = Math.Max(backTop, cm); else frontTop = Math.Max(frontTop, cm);
            }
            Console.WriteLine(FormattableString.Invariant($"{kit}: hem {sh.Hem:0.000} m; centre back hem to collar {backTop:0.0} cm, front {frontTop:0.0} cm; armpit to cuff {sh.Cuff * 100:0.0} cm, shoulder to cuff {(sh.Cuff + sh.Shoulder) * 100:0.0} cm; armpit ({sh.PitX:0.000}, {sh.PitY:0.000}), arm dir ({sh.DirX:0.00}, {sh.DirY:0.00})"));
            return 0;
        }
        var img = JerseyTemplate.Build(index, kit, size);
        File.WriteAllBytes(file, JerseyTemplate.Dds(img, size, size));
        File.WriteAllBytes(Path.ChangeExtension(file, ".png"), ImageFiles.Png(img, size, size));
        Console.WriteLine($"{kit}: {file} ({size}x{size}) and its PNG");
        return 0;
    }

    /// <summary>ebx-probe NAME [--match TEXT] [--floats N]: each import reference in an EBX, with the floats around it (for finding placement transforms).</summary>
    private static int EbxProbeCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("ebx-probe needs an EBX name");
        string match = opts.Get("match") ?? "";
        int nf = int.TryParse(opts.Get("floats"), out int k) ? k : 24;
        var byId = new Dictionary<Guid, string>();
        foreach (var e in index.Ebx.Values) byId.TryAdd(e.Id, e.Name);
        var f = EbxFile.Parse(index.Read(index.GetEbx(name)));
        var d = f.Data;
        Console.WriteLine($"{f.Imports.Length} imports, {f.ImportOffsets.Length} import offsets, {f.Instances.Length} instance counts, payload to {f.DataEnd}");
        int shown = 0;
        foreach (uint off in f.ImportOffsets)
        {
            int at = EbxFile.Payload + (int)off;
            ulong raw = BitConverter.ToUInt64(d, at);
            int idx = (int)(raw & 0x7FFFFFFF);
            string target = idx < f.Imports.Length ? byId.GetValueOrDefault(f.Imports[idx].File, "?") : $"raw {raw:X}";
            if (!target.Contains(match, StringComparison.OrdinalIgnoreCase)) continue;
            Console.WriteLine($"@{off} raw {raw:X16} -> {target}");
            var sb = new System.Text.StringBuilder("   ");
            for (int i = -nf; i < nf; i++)
            {
                int q = at + i * 4;
                if (q < 0 || q + 4 > d.Length) continue;
                float v = BitConverter.ToSingle(d, q);
                sb.Append(i == 0 ? " |" : " ").Append(Math.Abs(v) < 1e5 && (Math.Abs(v) > 1e-4 || v == 0) ? v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : $"#{BitConverter.ToUInt32(d, q):X}");
            }
            Console.WriteLine(sb);
            if (opts.Has("near"))
                foreach (uint o2 in f.ImportOffsets.Where(o2 => o2 != off && Math.Abs((long)o2 - off) <= 200))
                {
                    int i2 = BitConverter.ToInt32(d, EbxFile.Payload + (int)o2);
                    Console.WriteLine($"     import at {((long)o2 - off) / 8.0:+0.#;-0.#} slots -> {(i2 < f.Imports.Length ? byId.GetValueOrDefault(f.Imports[i2].File, "?") : "?")}");
                }
            if (opts.Has("arrays"))
                for (int s = -20; s < 20; s++)
                {
                    int fp = at + s * 8;
                    if (fp < EbxFile.Payload || fp + 8 > f.DataEnd) continue;
                    long rel = BitConverter.ToInt32(d, fp);
                    if (s == 0 || BitConverter.ToInt32(d, fp + 4) != 0) continue;
                    if (rel == 0 || Math.Abs(rel) > d.Length) continue;
                    long pa = fp + rel;
                    if (pa < 36 || pa + 64 > d.Length) continue;
                    uint count = BitConverter.ToUInt32(d, (int)pa - 4);
                    if (count == 0 || count > 100000) continue;
                    float F(int o) => BitConverter.ToSingle(d, (int)pa + o);
                    double r0 = F(0) * F(0) + F(4) * F(4) + F(8) * F(8), r1 = F(16) * F(16) + F(20) * F(20) + F(24) * F(24);
                    double r2 = F(32) * F(32) + F(36) * F(36) + F(40) * F(40), dot = F(0) * F(16) + F(4) * F(20) + F(8) * F(24);
                    bool ortho = r0 > 1e-4 && Math.Abs(r0 - r1) < 0.02 * r0 && Math.Abs(r0 - r2) < 0.02 * r0 && Math.Abs(dot) < 0.02 * r0;
                    if (opts.Has("dump")) Console.WriteLine("        " + string.Join(" ", Enumerable.Range(0, (int)Math.Min(count * 4, 24)).Select(j => F(j * 4).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
                    Console.WriteLine(FormattableString.Invariant($"     slot {s,3}: -> {pa} count {count}{(ortho ? $"  TRANSFORMS first t=({F(48):0.##},{F(52):0.##},{F(56):0.##}) r=({F(0):0.##},{F(4):0.##},{F(8):0.##})" : "")}"));
                }
            if (++shown >= (int.TryParse(opts.Get("limit"), out int l) ? l : 20)) break;
        }
        return 0;
    }

    /// <summary>arena [LEVEL] [--obj FILE] [--v]: the arena levels, or what the kit draws of one (placements, sections, textures).</summary>
    private static int ArenaCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var levels = ArenaScene.Levels(index);
        string pick = opts.Positional.FirstOrDefault();
        if (pick == null)
        {
            foreach (var l in levels) Console.WriteLine($"{l.Level}\t{l.Venue}\t{l.Id}");
            Console.Error.WriteLine($"{levels.Count} arenas");
            return 0;
        }
        var arena = levels.FirstOrDefault(l => l.Level.Equals(pick, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no arena {pick}");
        var names = ArenaScene.Names(index);
        var allPresets = ArenaScene.Presets(index);
        var presets = new Dictionary<string, string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var placements = ArenaScene.Placements(index, arena, names);
        Console.WriteLine($"{ArenaScene.Layers(index, arena).Count} layers, {placements.Count} placements of {placements.Select(p => p.Mesh).Distinct().Count()} meshes ({sw.ElapsedMilliseconds} ms)");
        var pieces = new Dictionary<string, List<ArenaPiece>>(StringComparer.OrdinalIgnoreCase);
        long tris = 0;
        int flat = 0;
        var textures = new HashSet<string>();
        using var obj = opts.Get("obj") is { } o ? new StreamWriter(o) : null;
        int baseVertex = 1;
        foreach (var g in placements.GroupBy(p => p.Mesh))
        {
            List<ArenaPiece> list;
            try { list = ArenaScene.Load(index, arena, g.Key, names, allPresets, presets); }
            catch (Exception ex) { Console.WriteLine($"{g.Key}: {ex.Message}"); continue; }
            int n = g.Count();
            if (opts.Has("v")) Console.WriteLine($"{g.Key} x{n}: {list.Count} sections, {list.Sum(p => p.Part.TriangleCount):N0} triangles");
            tris += (long)n * list.Sum(p => p.Part.TriangleCount);
            foreach (var p in list) { if (p.Texture == null) flat++; else textures.Add(p.Texture); }
            if (obj == null) continue;
            foreach (var pl in g)
                foreach (var p in list)
                {
                    var ps = p.Part.Positions; var t = pl.Transform;
                    obj.WriteLine($"g {Path.GetFileName(g.Key)}_{p.Part.Material}");
                    for (int v = 0; v < p.Part.VertexCount; v++)
                    {
                        float x = ps[v * 3], y = ps[v * 3 + 1], z = ps[v * 3 + 2];
                        obj.WriteLine(FormattableString.Invariant($"v {x * t[0] + y * t[3] + z * t[6] + t[9]} {x * t[1] + y * t[4] + z * t[7] + t[10]} {x * t[2] + y * t[5] + z * t[8] + t[11]}"));
                    }
                    for (int k = 0; k < p.Part.Indices.Length; k += 3)
                        obj.WriteLine($"f {p.Part.Indices[k] + baseVertex} {p.Part.Indices[k + 1] + baseVertex} {p.Part.Indices[k + 2] + baseVertex}");
                    baseVertex += p.Part.VertexCount;
                }
        }
        Console.WriteLine($"{arena.Level}: {tris:N0} triangles, {textures.Count} textures, {flat} sections without one ({sw.Elapsed.TotalSeconds:0.0} s)");
        return 0;
    }

    /// <summary>tunables --out FILE.csv: every tuning value the eboot registers (default, range, group, label), read from eboot.bin.</summary>
    private static int TunablesCmd(string game, Options opts)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Tunables.Read(Path.Combine(game, "eboot.bin"));
        Tunables.WriteCsv(rows, opts.Get("out") ?? "tunables.csv");
        Console.WriteLine($"{rows.Count} tunables ({rows.Count(t => t.Default == null)} with no default read) in {sw.Elapsed.TotalSeconds:0.0}s");
        return 0;
    }

    /// <summary>
    /// bap SLOTDIR [--set "Overall=80;Speed=90"] [--xfactor SLOT:ABILITY:TIER] [--out DIR]: a Be A Pro save's pro, ratings
    /// and X-Factors; with edits, writes the rebuilt DATA and INDEX to --out (never into the slot).
    /// </summary>
    private static int BeAProCmd(Options opts)
    {
        var save = BeAProSave.Open(opts.Positional.First());
        Console.WriteLine($"{save.Slot.Title}: {save.Slot.Detail}  ·  player {save.PlayerId}  ·  record {(save.HasRecord ? "found" : "not found")}");
        foreach (var r in BeAProSave.SkaterRatings) Console.Write($"{r.Name}={save.Rating(r)}  ");
        Console.WriteLine();
        for (int i = 0; i < 3; i++) Console.Write($"xfactor{i + 1}={save.XFactor(i)}  ");
        Console.WriteLine();
        Console.WriteLine("career values: " + string.Join(" ", save.CareerValues().Select(v => $"#{v.Id}={v.Value}")));
        Console.WriteLine("activities: " + string.Join(" ", Enumerable.Range(0, 6).Select(i => $"{BeAProSave.Activities[i]}={save.Activity(i)}")));
        foreach (var kv in (opts.Get("set") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (k, v) = (kv.Split('=')[0].Trim(), int.Parse(kv.Split('=')[1]));
            save.SetRating(BeAProSave.SkaterRatings.First(r => r.Name.Equals(k, StringComparison.OrdinalIgnoreCase)), v);
        }
        if (opts.Get("xfactor") is { } xf && xf.Split(':') is { Length: 3 } p) save.SetXFactor(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
        if (opts.Get("out") is { } outDir)
        {
            var (data, index) = save.Build();
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "DATA"), data);
            File.WriteAllBytes(Path.Combine(outDir, "INDEX"), index);
            Console.WriteLine($"wrote {outDir} ({data.Length:N0} bytes)");
        }
        return 0;
    }

    /// <summary>own-kit --out DIR [--font N] [--pant N] [--sock N] [--colors #MAIN,#SECOND,#THIRD]: paints the create-a-team parts, writes nothing.</summary>
    private static int OwnKitCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string outDir = opts.Get("out") ?? KitPaths.Export;
        Directory.CreateDirectory(outDir);
        var palette = (opts.Get("colors") ?? "#0B2D6B,#FFFFFF,#C8102E").Split(',').Select(c => Convert.ToInt32(c.TrimStart('#'), 16)).ToList();
        int Pick(string k) => int.TryParse(opts.Get(k), out int n) ? n : 0;
        foreach (var garment in new[] { "font", "pant", "sock" })
        {
            var parts = OwnKit.Parts(index, garment);
            Console.WriteLine($"{garment}: {parts.Count} designs ({string.Join(" ", parts.Select(p => $"{p.Number}:{p.Masks.Count}{(p.Color == null ? "-" : "")}"))})");
            var t = parts.FirstOrDefault(p => p.Number == Pick(garment)) ?? parts[0];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var pat = garment == "font" ? OwnKit.LoadFont(index, t, 1024) : JerseyStudio.LoadPattern(index, t, 1024);
            byte[] img;
            if (garment == "font")
            {
                var outl = OwnKit.Outlines(pat);
                byte[] probeN = null, probeC = null;
                if (opts.Has("probe"))
                {
                    probeN = JerseyStudio.Resize(Kits.Load(index, OwnKit.Map(index, t, "normal"), 1024, 0) is var tn ? tn.Rgba : null, tn.Width, tn.Height, pat.Size);
                    probeC = JerseyStudio.Resize(Kits.Load(index, OwnKit.Map(index, t, "coeff"), 1024, 0) is var tc ? tc.Rgba : null, tc.Width, tc.Height, pat.Size);
                }
                if (opts.Has("probe"))
                    for (int x = 0; x < 160; x += 2)
                    {
                        int i = (int.TryParse(opts.Get("proberow"), out int pr) ? pr : 300) * pat.Size + x;
                        Console.WriteLine($"    x{x}: a{pat.Detail[i * 4 + 3]} " + string.Join(" ", pat.Masks.Select(m => $"{m[i * 4]},{m[i * 4 + 1]},{m[i * 4 + 2]}"))
                            + "  n " + string.Join(",", Enumerable.Range(0, 4).Select(c => probeN[i * 4 + c])) + "  c " + string.Join(",", Enumerable.Range(0, 4).Select(c => probeC[i * 4 + c])));
                    }
                Console.WriteLine($"  {t.Label}: zones {string.Join(" ", pat.Zones.Select(z => $"{z.Zone}{(outl.Contains(z.Zone) ? "o" : "f")}{OwnKit.OutlineShares(pat).GetValueOrDefault(z.Zone):0.00}/{z.Share:P1}"))}, normal {OwnKit.Map(index, t, "normal") != null}");
                img = OwnKit.PaintFont(pat, outl, palette[1], palette[2]);
                var th = OwnKit.FontThumb(img, pat.Size);
                File.WriteAllBytes(Path.Combine(outDir, "own_font_thumb.png"), ImageFiles.Png(th.Rgba, th.Width, th.Height));
            }
            else
            {
                Console.WriteLine($"  {t.Label}: zones {string.Join(" ", pat.Zones.Select(z => $"{z.Zone}:{z.Share:P0}"))}, normal {OwnKit.Map(index, t, "normal") != null}");
                img = OwnKit.PaintPart(pat, palette, 0);
            }
            File.WriteAllBytes(Path.Combine(outDir, $"own_{garment}.png"), ImageFiles.Png(img, pat.Size, pat.Size));
            Console.WriteLine($"  painted in {sw.ElapsedMilliseconds} ms");
        }
        var carrier = OwnKit.Carrier(Kits.Find(index));
        Console.WriteLine($"carrier: {carrier}");
        return 0;
    }

    private static int KitHelmetCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string outDir = opts.Get("out") ?? KitPaths.Export;
        Directory.CreateDirectory(outDir);
        var kit = Kits.Find(index).First(k => k.ToString().Contains(opts.Positional.FirstOrDefault() ?? "nhl/toronto/adidas/home", StringComparison.OrdinalIgnoreCase));
        var mesh = Kits.LoadMesh(index, KitEquipment.HelmetMesh);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var map = SurfaceMap.Build(mesh.Lods[0], 1024);
        Console.WriteLine($"{kit}: helmet map in {sw.ElapsedMilliseconds} ms, bounds {string.Join(",", map.Bounds.Select(b => b.ToString("0.00")))}");
        var dbg = new byte[1024 * 1024 * 4];
        for (int i = 0; i < 1024 * 1024; i++)
        {
            if (!map.Covered(i)) continue;
            dbg[i * 4] = (byte)Math.Clamp(map.Uv2[i * 2] * 255, 0, 255);
            dbg[i * 4 + 1] = (byte)Math.Clamp(map.Uv2[i * 2 + 1] * 255, 0, 255);
            dbg[i * 4 + 2] = (byte)(map.Position[i * 3] > 0 ? 255 : 0);
            dbg[i * 4 + 3] = 255;
        }
        File.WriteAllBytes(Path.Combine(outDir, "helmet_uv2.png"), ImageFiles.Png(dbg, 1024, 1024));
        // the decal islands: texels whose second UV is inside the unit square, grouped by where on the helmet they are
        var islands = Enumerable.Range(0, 1024 * 1024).Where(i => map.Covered(i) && map.Uv2[i * 2] is >= 0 and <= 1 && map.Uv2[i * 2 + 1] is >= 0 and <= 1)
            .GroupBy(i => (X: Math.Sign(Math.Round(map.Position[i * 3] / 0.05)), Z: Math.Sign(Math.Round(map.Position[i * 3 + 2] / 0.05))));
        foreach (var g in islands)
        {
            var l = g.ToList();
            Console.WriteLine($"  island x{g.Key.X} z{g.Key.Z}: {l.Count} texels, uv1 x {l.Min(i => i % 1024)}..{l.Max(i => i % 1024)} y {l.Min(i => i / 1024)}..{l.Max(i => i / 1024)}, " +
                $"uv2 {l.Min(i => map.Uv2[i * 2]):0.00}..{l.Max(i => map.Uv2[i * 2]):0.00} x {l.Min(i => map.Uv2[i * 2 + 1]):0.00}..{l.Max(i => map.Uv2[i * 2 + 1]):0.00}, " +
                $"pos x {l.Average(i => map.Position[i * 3]):0.00} y {l.Average(i => map.Position[i * 3 + 1]):0.00} z {l.Average(i => map.Position[i * 3 + 2]):0.00}");
        }
        for (int y = 560; y <= 700; y += 20)
            Console.WriteLine("   " + string.Join(" ", Enumerable.Range(0, 11).Select(k => 70 + k * 19).Select(x =>
                { int i = y * 1024 + x; return map.Covered(i) ? $"{map.Uv2[i * 2],5:0.00},{map.Uv2[i * 2 + 1],4:0.00}" : "    --    "; })));
        var db = new Legacy(index).OpenDb(KitEquipment.Database);
        var zones = KitEquipment.Colors(db, kit)?.Helmet ?? new int[15];
        var h = KitEquipment.Helmet(index, zones, 1024);
        foreach (var slot in kit.Textures.Where(t => t.Slot.StartsWith("helmetlogo_") && t.Map == "color"))
        {
            var logo = Texture.Load(index, slot.Name);
            int inside = Enumerable.Range(0, 1024 * 1024).Count(i => map.Covered(i) && map.Uv2[i * 2] is >= 0 and < 1 && map.Uv2[i * 2 + 1] is >= 0 and < 1);
            var alphas = Enumerable.Range(0, logo.Width * logo.Height).Select(i => logo.Rgba[i * 4 + 3]).ToList();
            Console.WriteLine($"  {slot.Slot}: {logo.Width}x{logo.Height}, {inside} texels in the decal square, alpha {alphas.Min()}..{alphas.Max()} mean {alphas.Average(a => a):0}");
            var before = (byte[])h.Rgba.Clone();
            map.Decal(h.Rgba, logo.Rgba, logo.Width, logo.Height, (0, 0, 1, double.Parse(opts.Get("v1") ?? "1", System.Globalization.CultureInfo.InvariantCulture)));
            Console.WriteLine($"  decal changed {Enumerable.Range(0, before.Length / 4).Count(i => before[i * 4] != h.Rgba[i * 4])} texels (helmet texture {h.Width}x{h.Height})");
        }
        File.WriteAllBytes(Path.Combine(outDir, "helmet_logo.png"), ImageFiles.Png(h.Rgba, h.Width, h.Height));
        var dressed = KitEquipment.HelmetDressed(index, kit, zones, 1024, opts.Get("number") ?? "27");
        File.WriteAllBytes(Path.Combine(outDir, "helmet_dressed.png"), ImageFiles.Png(dressed.Rgba, dressed.Width, dressed.Height));
        Console.WriteLine($"  -> {outDir}");
        return 0;
    }

    /// <summary>kit-gear [FILTER] [--out DIR]: each matching kit's team and its helmet and glove zone colours from the database.</summary>
    private static int KitGearCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var db = new Legacy(index).OpenDb(KitEquipment.Database);
        string filter = opts.Positional.FirstOrDefault() ?? "", outDir = opts.Get("out");
        foreach (var kit in Kits.Find(index).Where(k => k.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(int.TryParse(opts.Get("limit"), out int l) ? l : 40))
        {
            var c = KitEquipment.Colors(db, kit);
            if (c is not { } v) { Console.WriteLine($"  {kit}  no team"); continue; }
            string Z(int[] z) => z == null ? "-" : string.Join(" ", z.Take(12).Select(x => x.ToString("X6")));
            Console.WriteLine($"  {kit}  -> {v.Team}  (variant {KitEquipment.Variant(kit.Name)})");
            Console.WriteLine($"    helmet {Z(v.Helmet)}");
            Console.WriteLine($"    gloves {Z(v.Gloves)}");
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                string stem = Path.Combine(outDir, kit.ToString().Replace('/', '_'));
                if (v.Helmet != null) { var h = KitEquipment.Helmet(index, v.Helmet, 512); File.WriteAllBytes(stem + "_helmet.png", ImageFiles.Png(h.Rgba, h.Width, h.Height)); }
                if (v.Gloves != null) { var g = KitEquipment.Gloves(index, "right", v.Gloves, 512); File.WriteAllBytes(stem + "_glove.png", ImageFiles.Png(g.Rgba, g.Width, g.Height)); }
            }
        }
        return 0;
    }

    /// <summary>
    /// venue-build --venue outdoorcanal --obj FILE --map "group=material;group2=material2" [--keep mat,mat] [--scale 0.01]
    /// [--rotate 90] [--skip-mesh m,m] [--dry OUT.obj]: rebuilds a venue's scenery from an OBJ. Every structures/VENUE mesh is read; each OBJ
    /// group is packed into the LOD0 sections of its NHL material (split across them as needed); every other section
    /// (other materials, shadow sections, lower LODs) is blanked unless its material is kept. All changed chunks go into
    /// one mod entry ("venue:VENUE"), replacing earlier mesh entries of that venue.
    /// </summary>
    private static int VenueBuildCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string venue = opts.Get("venue") ?? throw new ArgumentException("venue-build needs --venue");
        string obj = opts.Get("obj") ?? throw new ArgumentException("venue-build needs --obj");
        float scale = float.Parse(opts.Get("scale") ?? "0.01", System.Globalization.CultureInfo.InvariantCulture);
        float rotate = float.Parse(opts.Get("rotate") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        var map = (opts.Get("map") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=')).ToDictionary(x => x[0].Trim(), x => x[1].Trim());
        var keep = (opts.Get("keep") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        // meshes the level doesn't show in a game (e.g. placed only by a layer that isn't loaded): nothing goes into them
        var skipMeshes = (opts.Get("skip-mesh") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string prefix = $"content/worlds/components/structures/{venue}/";
        var meshes = index.Ebx.Values.Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.Type is "RigidMeshAsset" or "CompositeMeshAsset" or "MeshAsset")
            .Select(e => e.Name).Where(n => index.GetRes(n) != null).OrderBy(n => n).ToList();
        var all = meshes.SelectMany(m => MeshWriter.Sections(index.Read(index.GetRes(m))).Select(sec => (Mesh: m, Sec: sec))).ToList();
        Console.WriteLine($"{meshes.Count} meshes, {all.Count} chunk sections");

        var chunks = new Dictionary<Guid, byte[]>();
        byte[] ChunkOf(Guid id) => chunks.TryGetValue(id, out var c) ? c : chunks[id] = index.Read(index.GetChunk(id) ?? throw new InvalidDataException($"no chunk {id}"));
        var used = new HashSet<(string, int, int)>();
        // per mesh and per section: the bounds of the geometry written into it (the header boxes are fitted to them)
        var bounds = new Dictionary<string, (float[] Min, float[] Max)>();
        var sectionBounds = new Dictionary<(string, MeshWriter.Section), (float[] Min, float[] Max)>();
        foreach (var (group, material) in map)
        {
            var g = NewGeometry.FromObj(obj, new HashSet<string> { group }, scale, rotate);
            var targets = all.Where(x => x.Sec.Lod == 0 && x.Sec.Material == material && !skipMeshes.Contains(Path.GetFileName(x.Mesh))).OrderByDescending(x => x.Sec.VertexCount).ToList();
            if (targets.Count == 0) throw new ArgumentException($"no LOD0 section uses {material}");
            var pieces = g.Split(targets.Select(x => (x.Sec.VertexCount, x.Sec.TriangleCount)).ToList());
            for (int i = 0; i < targets.Count; i++)
            {
                MeshWriter.Write(ChunkOf(targets[i].Sec.Chunk), targets[i].Sec, pieces[i]);
                used.Add((targets[i].Mesh, targets[i].Sec.Lod, targets[i].Sec.Index));
                if (pieces[i].VertexCount == 0) continue;
                if (!bounds.TryGetValue(targets[i].Mesh, out var bb)) bounds[targets[i].Mesh] = bb = (new[] { float.MaxValue, float.MaxValue, float.MaxValue }, new[] { float.MinValue, float.MinValue, float.MinValue });
                var sb = sectionBounds[(targets[i].Mesh, targets[i].Sec)] = (new[] { float.MaxValue, float.MaxValue, float.MaxValue }, new[] { float.MinValue, float.MinValue, float.MinValue });
                for (int v = 0; v < pieces[i].VertexCount; v++)
                    for (int c = 0; c < 3; c++)
                    {
                        float p = pieces[i].Positions[v * 3 + c];
                        bb.Min[c] = Math.Min(bb.Min[c], p); bb.Max[c] = Math.Max(bb.Max[c], p);
                        sb.Min[c] = Math.Min(sb.Min[c], p); sb.Max[c] = Math.Max(sb.Max[c], p);
                    }
            }
            Console.WriteLine($"  {group,-26} {g.VertexCount,6} verts {g.TriangleCount,6} tris -> {material} ({pieces.Count(p => p.TriangleCount > 0)} of {targets.Count} sections)");
        }
        int blanked = 0;
        foreach (var (mesh, sec) in all)
        {
            if (used.Contains((mesh, sec.Lod, sec.Index)) || (keep.Contains(sec.Material) && sec.Lod == 0) || keep.Contains(sec.Material.Replace("_Shadow", ""))) continue;
            MeshWriter.Blank(ChunkOf(sec.Chunk), sec);
            blanked++;
        }
        Console.WriteLine($"blanked {blanked} sections; {chunks.Count} chunks changed");

        if (opts.Get("dry") is { } dry)
        {
            // every LOD0 section as it now reads, in one OBJ
            using var w = new StreamWriter(dry);
            int vbase = 0;
            foreach (var m in meshes)
            {
                var res = index.Read(index.GetRes(m));
                var set = new MeshSet(res, id => chunks.TryGetValue(id, out var c) ? c : index.GetChunk(id) is { } e ? index.Read(e) : null);
                if (set.Lods.Count == 0) continue;
                foreach (var part in set.Lods[0])
                {
                    if (part.Indices.All(i => i == 0)) continue;
                    w.WriteLine($"g {Path.GetFileName(m)}_{part.Material}");
                    for (int i = 0; i < part.VertexCount; i++) w.WriteLine(FormattableString.Invariant($"v {part.Positions[i * 3]} {part.Positions[i * 3 + 1]} {part.Positions[i * 3 + 2]}"));
                    for (int t = 0; t < part.Indices.Length; t += 3)
                        if (!(part.Indices[t] == part.Indices[t + 1] && part.Indices[t] == part.Indices[t + 2]))
                            w.WriteLine($"f {part.Indices[t] + 1 + vbase} {part.Indices[t + 1] + 1 + vbase} {part.Indices[t + 2] + 1 + vbase}");
                    vbase += part.VertexCount;
                }
            }
            Console.WriteLine($"wrote {dry}");
            return 0;
        }

        RefuseWhileRunning();
        var store = Mods();
        foreach (var old in store.Entries.Where(e => e.Kind == "mesh" && (e.Name == "venue:" + venue || e.Name.StartsWith(prefix))).ToList())
        {
            store.Restore(old, game, Console.WriteLine);
            store.Remove(old);
        }
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var patches = new List<PatchData>();
        var notes = new List<string>();
        foreach (var (id, bytes) in chunks)
        {
            var plan = ChunkPatcher.Plan(index, id, bytes, loc, tocs);
            patches.AddRange(plan.Patches);
            notes.AddRange(plan.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
        }
        // the headers: the mesh box grown over the new geometry and each written section's box fitted to its piece
        // (the game culls by both, so geometry outside the old boxes would vanish)
        foreach (var (mesh, bb) in bounds)
        {
            var grown = MeshWriter.GrowBounds(index.Read(index.GetRes(mesh)), bb.Min, bb.Max);
            foreach (var ((m, sec), sb) in sectionBounds)
                if (m == mesh) MeshWriter.SetSectionBounds(grown, sec, sb.Min, sb.Max);
            var plan = ResPatcher.Plan(index, mesh, grown, loc, tocs);
            patches.AddRange(plan.Patches);
            notes.AddRange(plan.Notes);
            Console.WriteLine($"  {Path.GetFileName(mesh)}: mesh and section bounds fitted ({plan.Patches.Count} patches)");
        }
        var entry = store.Add("mesh", "venue:" + venue, obj, $"Venue {venue} rebuilt from {Path.GetFileName(obj)}: {map.Count} parts, {blanked} sections blanked", patches, notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>
    /// mesh-replace NAME --section N --obj FILE [--groups a,b] [--scale 0.01] [--offset x,y,z] [--blank-lods] [--blank-shadow]
    /// [--dry OUT.obj]: puts OBJ groups into LOD0 section N of a mesh in place (MeshWriter). --blank-lods empties the same
    /// material in the other LODs, --blank-shadow the shadow-only sections. --dry writes the result as an OBJ instead of
    /// patching the game.
    /// </summary>
    private static int MeshReplaceCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("mesh-replace needs a mesh name");
        var res = index.GetRes(name) ?? throw new ArgumentException($"no mesh {name}");
        var resBytes = index.Read(res);
        var sections = MeshWriter.Sections(resBytes);
        foreach (var x in sections)
            Console.WriteLine($"  LOD{x.Lod} s{x.Index} {x.Material,-30} {x.VertexCount,6} verts {x.TriangleCount,6} tris  [{string.Join(" ", x.Elements.Select(e => $"{e.Usage:x}/{e.Format}/{e.Size}"))}]");
        int si = int.Parse(opts.Get("section") ?? "0");
        var target = sections.First(x => x.Lod == sections.Min(y => y.Lod) && x.Index == si);
        float scale = float.Parse(opts.Get("scale") ?? "0.01", System.Globalization.CultureInfo.InvariantCulture);
        var groups = (opts.Get("groups") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var g = NewGeometry.FromObj(opts.Get("obj") ?? throw new ArgumentException("mesh-replace needs --obj"), groups, scale);
        if (opts.Get("offset") is { } off)
        {
            var o = off.Split(',').Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            for (int i = 0; i < g.Positions.Count; i++) g.Positions[i] += o[i % 3];
        }
        Console.WriteLine($"new geometry: {g.VertexCount} vertices, {g.TriangleCount} triangles into LOD{target.Lod} s{target.Index} {target.Material}");

        var chunks = new Dictionary<Guid, byte[]>();
        byte[] ChunkOf(Guid id) => chunks.TryGetValue(id, out var c) ? c : chunks[id] = index.Read(index.GetChunk(id) ?? throw new InvalidDataException($"no chunk {id}"));
        MeshWriter.Write(ChunkOf(target.Chunk), target, g);
        foreach (var x in sections.Where(x => x != target))
        {
            bool same = opts.Has("blank-lods") && x.Material == target.Material && x.Lod != target.Lod;
            bool shadow = opts.Has("blank-shadow") && x.Material.EndsWith("_Shadow", StringComparison.OrdinalIgnoreCase);
            if (!same && !shadow) continue;
            MeshWriter.Blank(ChunkOf(x.Chunk), x);
            Console.WriteLine($"  blanked LOD{x.Lod} s{x.Index} {x.Material}");
        }
        if (opts.Get("dry") is { } dry)
        {
            var back = new MeshSet(resBytes, id => chunks.TryGetValue(id, out var c) ? c : index.GetChunk(id) is { } e ? index.Read(e) : null);
            File.WriteAllText(dry, back.ToObj(target.Lod));
            Console.WriteLine($"read back and wrote {dry}");
            return 0;
        }
        RefuseWhileRunning();
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var store = Mods();
        if (store.Find("mesh", name) is { } old) { store.Restore(old, game, Console.WriteLine); store.Remove(old); }
        var patches = new List<PatchData>();
        var notes = new List<string>();
        foreach (var (id, bytes) in chunks)
        {
            var plan = ChunkPatcher.Plan(index, id, bytes, loc, tocs);
            patches.AddRange(plan.Patches);
            notes.AddRange(plan.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
        }
        var entry = store.Add("mesh", name, opts.Get("obj"), $"Mesh: {g.VertexCount} vertices, {g.TriangleCount} triangles into {target.Material}" + (groups.Count > 0 ? $" ({string.Join(", ", groups)})" : ""), patches, notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>
    /// logo-ads [ID] [--layout FILE.rgba --layout-size W,H] [--size N --like REF] [--out DIR] [--write] [--dump]: the
    /// centre-ice ads drawn from the logo's texture
    /// (LogoAds), as a dry run. Lays out the ice meshes and logo ID (default 27, Toronto) in memory with the game's own four
    /// ads (or a team's, cut from a TheFaceoff.net layout as raw RGBA), and draws centre ice from above the game's way
    /// (logo_ads_stock.png) and the new way (logo_ads_new.png), with the new texture (logo_ads_texture.png). With the
    /// game's ads the two drawings should match. Writes nothing into the game.
    /// </summary>
    private static int LogoAdsCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string id = new string((opts.Positional.FirstOrDefault() ?? "27").Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        string outDir = opts.Get("out") ?? Path.Combine(KitPaths.Export, "logo-ads");
        Directory.CreateDirectory(outDir);

        var chunks = new Dictionary<Guid, byte[]>();
        byte[] ChunkOf(Guid g) => chunks.TryGetValue(g, out var c) ? c : chunks[g] = index.Read(index.GetChunk(g) ?? throw new InvalidDataException($"no chunk {g}"));
        // only the NHL ice: the other ice meshes don't map the logo with a plain 0..1 UV2 (LogoAds)
        var nhlBefore = index.Read(index.GetRes(LogoAds.NhlIce) ?? throw new InvalidDataException("no NHL ice mesh"));
        if (opts.Has("dump"))
        {
            // every element of the logo, centre-ad and centre-line sections, vertex by vertex (float or half)
            var dumped = opts.Get("mesh") is { } other ? index.Read(index.GetRes(other) ?? throw new ArgumentException($"no mesh {other}")) : nhlBefore;
            foreach (var s in MeshWriter.Sections(dumped).Where(s => s.Material.Contains("centerlogo") || s.Material.Contains("centerad") || s.Material == "ice_centerline_mat"))
            {
                var c = ChunkOf(s.Chunk);
                Console.WriteLine($"LOD{s.Lod} s{s.Index} {s.Material}");
                for (int v = 0; v < s.VertexCount; v++)
                {
                    var parts = new List<string>();
                    int at = s.VertexOffset;
                    foreach (var (usage, format, esize) in s.Elements)
                    {
                        int p = at + v * esize;
                        string val = format switch
                        {
                            3 => string.Join(",", Enumerable.Range(0, esize / 4).Select(k => System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(c.AsSpan(p + k * 4)).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))),
                            6 => string.Join(",", Enumerable.Range(0, 2).Select(k => ((float)System.Buffers.Binary.BinaryPrimitives.ReadHalfLittleEndian(c.AsSpan(p + k * 2))).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))),
                            _ => Convert.ToHexString(c, p, esize),
                        };
                        parts.Add($"{usage:x}:{val}");
                        at += esize * s.VertexCount;
                    }
                    Console.WriteLine("  " + string.Join("  ", parts));
                }
            }
            return 0;
        }
        var nhlAfter = (byte[])nhlBefore.Clone();
        var stockChunks = new Dictionary<Guid, byte[]>();
        foreach (var s in MeshWriter.Sections(nhlBefore)) stockChunks.TryAdd(s.Chunk, (byte[])ChunkOf(s.Chunk).Clone());
        Console.WriteLine(Path.GetFileName(LogoAds.NhlIce));
        foreach (var c in LogoAds.PatchMesh(nhlAfter, ChunkOf, true))
            Console.WriteLine(FormattableString.Invariant($"  LOD{c.Lod} s{c.Index,-2} {c.Material,-26} {c.Change,-44} UV2 {c.Before.U0:0.000},{c.Before.V0:0.000}..{c.Before.U1:0.000},{c.Before.V1:0.000} -> {c.After.U0:0.000},{c.After.V0:0.000}..{c.After.U1:0.000},{c.After.V1:0.000}"));
        Console.WriteLine($"{chunks.Count} chunk(s) would change");

        (byte[], int, int) Rgba(string name) { var t = Texture.Load(index, name); return (t.Rgba, t.Width, t.Height); }
        string logoName = ArenaScene.CenterLogo(index, int.Parse(id));
        var (logo, lw, lh) = Rgba(logoName);
        Console.WriteLine($"logo {logoName} {lw}×{lh}");
        if (lw != lh) throw new InvalidDataException("the logo isn't square");
        var stockLogo = (logo, lw, lh);
        int size = int.TryParse(opts.Get("size"), out int sz) ? sz : lw;
        var gameAds = IceAds.GameCenterAds(index, -1).Select(a => IceAds.Texture(index, a) is { } n ? Rgba(n) : (null, 0, 0)).ToList();
        var ads = gameAds;
        if (opts.Get("layout") is { } layout)
        {
            // a TheFaceoff.net top-down layout as raw RGBA (--layout-size W,H): the team's own logo and ads cut from it
            var wh = (opts.Get("layout-size") ?? "5000,2236").Split(',').Select(int.Parse).ToArray();
            var pic = File.ReadAllBytes(layout);
            var g = CenterIce.Measure(pic, wh[0], wh[1]);
            Console.WriteLine($"layout {Path.GetFileName(layout)}: centre {g.Cx:0},{g.Cy:0}, {g.PixelsPerFoot:0.00} px/ft{(g.Measured ? "" : " (template)")}");
            ads = CenterIce.CenterAds.Select(s => (CenterIce.Hotspot(pic, wh[0], wh[1], g, new CenterIceLook(), s), CenterIce.AdWidth, CenterIce.AdHeight)).ToList();
            (logo, lw, lh) = (CenterIce.Logo(pic, wh[0], wh[1], g, new CenterIceLook(), size), size, size);
        }
        var composed = LogoAds.Compose(logo, lw, ads, size);
        File.WriteAllBytes(Path.Combine(outDir, "logo_ads_texture.png"), ImageFiles.Png(composed, size, size));

        // centre ice from above, 20 m square: right is -z, down is +x (the broadcast side at the bottom)
        byte[] Draw(byte[] res, Func<Guid, byte[]> chunk, Func<string, (byte[] Img, int W, int H)?> textureOf)
        {
            const int n = 1024;
            const double half = 10;
            var img = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++) { img[i * 4] = 0xE9; img[i * 4 + 1] = 0xEE; img[i * 4 + 2] = 0xF2; img[i * 4 + 3] = 255; }
            var set = new MeshSet(res, chunk);
            foreach (var part in set.Lods[0])
            {
                if (textureOf(part.Material) is not { } tex || part.Uvs3 == null) continue;
                for (int t = 0; t + 2 < part.Indices.Length; t += 3)
                {
                    int a = part.Indices[t], b = part.Indices[t + 1], c = part.Indices[t + 2];
                    if (a == b && b == c) continue;
                    double Px(int v) => (-part.Positions[v * 3 + 2] + half) / (2 * half) * n;
                    double Py(int v) => (part.Positions[v * 3] + half) / (2 * half) * n;
                    double ax = Px(a), ay = Py(a), bx = Px(b), by = Py(b), cx = Px(c), cy = Py(c);
                    double area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
                    if (Math.Abs(area) < 1e-9) continue;
                    int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), x1 = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                    int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), y1 = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                    for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        double px = x + 0.5, py = y + 0.5;
                        double wa = ((bx - px) * (cy - py) - (cx - px) * (by - py)) / area, wb = ((cx - px) * (ay - py) - (ax - px) * (cy - py)) / area, wc = 1 - wa - wb;
                        if (wa < 0 || wb < 0 || wc < 0) continue;
                        double u = wa * part.Uvs3[a * 2] + wb * part.Uvs3[b * 2] + wc * part.Uvs3[c * 2];
                        double v = wa * part.Uvs3[a * 2 + 1] + wb * part.Uvs3[b * 2 + 1] + wc * part.Uvs3[c * 2 + 1];
                        var (src, w, h) = tex;
                        int sx = Math.Clamp((int)(u * w), 0, w - 1), sy = Math.Clamp((int)(v * h), 0, h - 1), so = (sy * w + sx) * 4, o = (y * n + x) * 4;
                        double al = src[so + 3] / 255.0;
                        for (int k = 0; k < 3; k++) img[o + k] = (byte)Math.Round(src[so + k] * al + img[o + k] * (1 - al));
                    }
                }
            }
            return img;
        }
        var adOf = CenterIce.CenterAds.Select((s, i) => (s.Material, i)).ToDictionary(p => p.Material, p => p.i, StringComparer.OrdinalIgnoreCase);
        var stock = Draw(nhlBefore, g => stockChunks.TryGetValue(g, out var c) ? c : index.GetChunk(g) is { } e ? index.Read(e) : null,
            m => m.Equals(LogoAds.LogoMaterial, StringComparison.OrdinalIgnoreCase) ? stockLogo : adOf.TryGetValue(m, out int i) && gameAds[i].Item1 != null ? gameAds[i] : null);
        // the new way: the material each section now has decides its texture
        var newSections = MeshWriter.Sections(nhlAfter);
        var logoMat = newSections.First(s => s.Lod == 0 && s.Material.Equals(LogoAds.LogoMaterial, StringComparison.OrdinalIgnoreCase));
        var drawsLogo = newSections.Where(s => s.Lod == 0 && nhlAfter[s.At + 0x1C] == nhlAfter[logoMat.At + 0x1C]).Select(s => s.Material).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($"drawn with the logo's material: {string.Join(", ", drawsLogo)}");
        var fresh = Draw(nhlAfter, g => chunks.TryGetValue(g, out var c) ? c : index.GetChunk(g) is { } e ? index.Read(e) : null,
            m => drawsLogo.Contains(m) ? (composed, size, size) : null);
        File.WriteAllBytes(Path.Combine(outDir, "logo_ads_stock.png"), ImageFiles.Png(stock, 1024, 1024));
        File.WriteAllBytes(Path.Combine(outDir, "logo_ads_new.png"), ImageFiles.Png(fresh, 1024, 1024));
        long diff = 0;
        for (int i = 0; i < stock.Length; i++) diff += Math.Abs(stock[i] - fresh[i]);
        Console.WriteLine(FormattableString.Invariant($"mean difference stock vs new: {diff / (double)stock.Length:0.00} of 255 per channel"));
        Console.WriteLine($"wrote {outDir}");
        if (!opts.Has("write")) return 0;

        // into the game: the NHL ice (mesh entry "logo-ads") and this logo (texture entry), each undone by Restore.
        // Every other arena's logo on the NHL ice shows cropped until it's laid out too, or the mesh entry is restored.
        RefuseWhileRunning();
        if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
            throw new InvalidOperationException("The kit is open. Close it before changing the game's files.");
        var store = Mods();
        if (store.Find("texture", logoName) is { } had)
        {
            if (store.State(had, game) != ModState.NotApplied)
                throw new InvalidOperationException($"{logoName} already has a mod entry in the game ({had.Summary}). Restore it first.");
            store.Remove(had);
        }
        if (store.Find("mesh", "logo-ads") is { } oldMesh) { store.Restore(oldMesh, game, Console.WriteLine); store.Remove(oldMesh); }
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var patches = new List<PatchData>();
        var notes = new List<string>();
        foreach (var (cid, bytes) in chunks)
        {
            var plan = ChunkPatcher.Plan(index, cid, bytes, loc, tocs);
            patches.AddRange(plan.Patches);
            notes.AddRange(plan.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
        }
        var resPlan = ResPatcher.Plan(index, LogoAds.NhlIce, nhlAfter, loc, tocs);
        patches.AddRange(resPlan.Patches);
        notes.AddRange(resPlan.Notes);
        var meshEntry = store.Add("mesh", "logo-ads", LogoAds.NhlIce, $"NHL ice: centre-ice ads drawn from the logo (logo UV2/UV3 × {LogoAds.LogoFrac}, 4 ad sections on the logo's material)", patches, notes);
        store.Apply(meshEntry, game, Console.WriteLine);
        Console.WriteLine($"mesh state: {store.State(meshEntry, game)}");

        // the logo: in place at its own size, else grown (--like: a game texture of the new size)
        var tex = size == stockLogo.lw ? Replacements.PlanTexture(index, logoName, composed, loc, tocs)
            : Replacements.PlanTextureResize(index, logoName, composed, opts.Get("like") ?? throw new ArgumentException($"a {size}² logo needs --like (a game texture of that size)"), loc, tocs);
        var hd = tex.Header;
        var texEntry = store.Add("texture", logoName, opts.Get("layout") ?? "game ads", $"Logo laid out with centre-ice ads, {hd.Format.Name} {hd.Width}×{hd.Height}" + (size != stockLogo.lw ? $" (was {stockLogo.lw}², added at the end of its CAS file)" : ""),
            tex.Chunk.Patches.Concat(tex.Res?.Patches ?? new()).ToList(), tex.Chunk.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
        store.Apply(texEntry, game, Console.WriteLine);
        Console.WriteLine($"logo state: {store.State(texEntry, game)}");
        return 0;
    }

    /// <summary>
    /// logo-ads-all: every centre logo laid out for the ads (LogoAds), PLAN §22.
    /// --teams DIR: the NHL teams' logo ids (by level, matched to the team by city as on the Arenas page) and each
    ///   team's newest TheFaceoff.net layout, downloaded; writes DIR/teams.tsv (id, level, team, layout file).
    /// --layouts DIR [--like REF] [--limit N] [--preview DIR] [--write]: plans every ice_centerlogo_N_color. Logo N with
    ///   DIR/N.rgba (+ N.size "W,H"): the team's logo and ads cut from it at 2048² (grown, REF's header). The rest: the
    ///   game's logo with the game's four ads, in place at its own size. The other ice meshes get their logo UV3 scaled.
    ///   Logos with a mod entry already are left alone. --nhl FILE (teams.tsv): those logos get an entry each, the rest
    ///   share one. --ids A,B: only these. --write applies mesh "logo-ads:other-ice", texture "logo-ads:logos" and the
    ///   NHL logos' own entries.
    ///   --replace: only the NHL logos with a layout in DIR, each replacing its own entry (restored first, with --write);
    ///   the other ice and the shared logos entry are left as they are.
    /// </summary>
    private static int LogoAdsAllCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string Squash(string x) => new string((x ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (opts.Get("teams") is { } teamsDir)
        {
            Directory.CreateDirectory(teamsDir);
            var db = new Legacy(index).OpenDb(Roster.Database);
            var teams = TeamEditor.ListTeams(db).Where(t => t.Index < 32).ToList();
            var lines = new List<string>();
            foreach (var l in ArenaScene.Levels(index))
            {
                var team = teams.FirstOrDefault(t => Squash(t.City) == l.Venue.ToLowerInvariant())
                    ?? teams.FirstOrDefault(t => Squash(t.City).StartsWith(l.Venue, StringComparison.OrdinalIgnoreCase));
                if (team == null) continue;
                string file = "";
                try
                {
                    var rink = IceAds.FindRinkAsync(team.Name, Console.WriteLine).GetAwaiter().GetResult();
                    if (rink != null)
                    {
                        var (title, layout) = FaceoffNet.RinkAsync(rink.Url).GetAwaiter().GetResult();
                        if (layout != null) file = FaceoffNet.DownloadAsync(layout, Path.Combine(KitPaths.Export, "faceoff", "downloads")).GetAwaiter().GetResult();
                        Console.WriteLine($"{l.Id,5} {l.Level,-24} {team.Name,-24} {title} -> {Path.GetFileName(file)}");
                    }
                    else Console.WriteLine($"{l.Id,5} {l.Level,-24} {team.Name,-24} no rink on TheFaceoff.net");
                }
                catch (Exception ex) { Console.WriteLine($"{l.Id,5} {l.Level,-24} {team.Name,-24} failed: {ex.Message}"); }
                lines.Add($"{l.Id}\t{l.Level}\t{team.Name}\t{file}");
            }
            File.WriteAllLines(Path.Combine(teamsDir, "teams.tsv"), lines);
            Console.WriteLine($"{lines.Count} levels of NHL teams -> {Path.Combine(teamsDir, "teams.tsv")}");
            return 0;
        }

        string layouts = opts.Get("layouts") ?? throw new ArgumentException("logo-ads-all needs --teams DIR or --layouts DIR");
        string like = opts.Get("like") ?? "content/worlds/rink/textures/common/ice_clean_coeff";
        int limit = int.TryParse(opts.Get("limit"), out int lim) ? lim : int.MaxValue;
        string preview = opts.Get("preview");
        if (preview != null) Directory.CreateDirectory(preview);
        bool write = opts.Has("write"), replace = opts.Has("replace");
        if (write)
        {
            RefuseWhileRunning();
            if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
                throw new InvalidOperationException("The kit is open. Close it before changing the game's files.");
        }
        var store = Mods();
        foreach (var (kind, name) in replace ? Array.Empty<(string, string)>() : new[] { ("mesh", "logo-ads:other-ice"), ("texture", "logo-ads:logos") })
            if (store.Find(kind, name) is { } had)
            {
                if (store.State(had, game) != ModState.NotApplied) throw new InvalidOperationException($"{name} is already in the game. Restore it first.");
                if (write) store.Remove(had);
            }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        Console.WriteLine($"locations read in {sw.Elapsed.TotalSeconds:0} s");

        // the other ice meshes: their logo's UV3 scaled (the NHL ice is the "logo-ads" entry)
        var meshChunks = new Dictionary<Guid, byte[]>();
        byte[] MeshChunk(Guid g) => meshChunks.TryGetValue(g, out var c) ? c : meshChunks[g] = index.Read(index.GetChunk(g) ?? throw new InvalidDataException($"no chunk {g}"));
        foreach (var mesh in LogoAds.Meshes.Where(m => m != LogoAds.NhlIce && !replace))
        {
            if (index.GetRes(mesh) is not { } r) continue;
            foreach (var c in LogoAds.PatchMesh(index.Read(r), MeshChunk, false))
                Console.WriteLine($"  {Path.GetFileName(mesh)} LOD{c.Lod} s{c.Index} {c.Material}: {c.Change}");
        }
        var meshPatches = new List<PatchData>();
        foreach (var (cid, bytes) in meshChunks)
            meshPatches.AddRange(ChunkPatcher.Plan(index, cid, bytes, loc, tocs).Patches);
        Console.WriteLine($"other ice: {meshChunks.Count} chunk(s), {meshPatches.Count} range(s)");

        (byte[], int, int) Rgba(string n) { var t = Texture.Load(index, n); return (t.Rgba, t.Width, t.Height); }
        var gameAds = IceAds.GameCenterAds(index, -1).Select(a => IceAds.Texture(index, a) is { } n ? Rgba(n) : (null, 0, 0)).ToList();
        var logoName = new System.Text.RegularExpressions.Regex(@"^content/worlds/rink/textures/centerlogo/ice_centerlogo_(\d+)_color$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var logos = index.Res.Keys.Select(n => (Name: n, M: logoName.Match(n))).Where(x => x.M.Success)
            .Select(x => (x.Name, Id: int.Parse(x.M.Groups[1].Value))).ToList();
        bool Team(int id) => File.Exists(Path.Combine(layouts, $"{id}.rgba"));
        // the NHL teams' logos (logo-ads-all --teams): each gets its own entry, so a team's ads can come later alone
        var nhl = opts.Get("nhl") is { } tsv ? File.ReadAllLines(tsv).Select(l => int.Parse(l.Split('\t')[0])).ToHashSet() : new HashSet<int>();
        var only = opts.Get("ids") is { } ids ? ids.Split(',').Select(int.Parse).ToHashSet() : null;
        var nhlPlans = new List<(string Name, string Summary, string Source, List<PatchData> Patches)>();
        logos = logos.Where(x => only == null || only.Contains(x.Id)).Where(x => !replace || Team(x.Id) && nhl.Contains(x.Id))
            .OrderBy(x => Team(x.Id) ? 0 : 1).ThenBy(x => x.Id).Take(limit).ToList();
        Console.WriteLine($"{logos.Count} logos, {logos.Count(x => Team(x.Id))} with a team layout");
        if (replace && write)
        {
            // the logos' own entries come out first, so their bytes are the game's again and free to grow
            foreach (var (name, id) in logos)
                if (store.Entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } own)
                {
                    if (store.State(own, game) != ModState.NotApplied) store.Restore(own, game, null);
                    store.Remove(own);
                    Console.WriteLine($"  {id,5}: its entry restored, to be replaced");
                }
            index = OpenIndex(game, false);
            tocs = new TocCache(index);
            loc = LocationIndex.Build(index, tocs);
        }

        ModEntry entry = write && !replace ? store.Begin("texture", "logo-ads:logos", layouts, "") : null;
        var ends = new Dictionary<string, long>();
        var done = new HashSet<Guid>();
        int grown = 0, inPlace = 0, skipped = 0, ranges = 0;
        long added = 0;
        foreach (var (name, id) in logos)
        {
            try
            {
                if (store.Entries.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) && !(replace && !write)) { Console.WriteLine($"  {id,5}: has its own mod entry, left alone"); skipped++; continue; }
                var h = TextureHeader.Parse(index.Read(index.GetRes(name)));
                if (done.Contains(h.ChunkId)) { Console.WriteLine($"  {id,5}: same stored bytes as one already done"); skipped++; continue; }
                TextureReplacementPlan plan = null;
                byte[] composed;
                int size;
                string how;
                if (Team(id))
                {
                    var wh = File.ReadAllText(Path.Combine(layouts, $"{id}.size")).Trim().Split(',').Select(int.Parse).ToArray();
                    var pic = File.ReadAllBytes(Path.Combine(layouts, $"{id}.rgba"));
                    var g = CenterIce.Measure(pic, wh[0], wh[1]);
                    var ads = CenterIce.CenterAds.Select(s => (CenterIce.Hotspot(pic, wh[0], wh[1], g, new CenterIceLook(), s), CenterIce.AdWidth, CenterIce.AdHeight)).ToList();
                    size = 2048;
                    composed = LogoAds.Compose(CenterIce.Logo(pic, wh[0], wh[1], g, new CenterIceLook(), size), size, ads, size);
                    try { plan = Replacements.PlanTextureResize(index, name, composed, like, loc, tocs, ends: ends); how = "team, 2048, added"; grown++; }
                    catch (InvalidDataException ex)
                    {
                        // it can't be grown (e.g. its bytes are shared): the team's at the logo's own size, in place
                        Console.WriteLine($"  {id,5}: not grown ({ex.Message})");
                        size = h.Width;
                        composed = LogoAds.Compose(CenterIce.Logo(pic, wh[0], wh[1], g, new CenterIceLook(), size), size, ads, size);
                        how = $"team, {size}, in place";
                        inPlace++;
                    }
                }
                else
                {
                    var (logo, lw, lh) = Rgba(name);
                    if (lw != lh) { Console.WriteLine($"  {id,5}: not square ({lw}x{lh}), left alone"); skipped++; continue; }
                    size = lw;
                    composed = LogoAds.Compose(logo, lw, gameAds, size);
                    how = $"game ads, {size}, in place";
                    inPlace++;
                }
                // in place: shared bytes are looked for (slow) only when the logo turns out to share them
                if (plan == null)
                {
                    try { plan = Replacements.PlanTexture(index, name, composed, loc, tocs); }
                    catch (InvalidDataException ex) when (ex.Message.Contains("also used by")) { plan = Replacements.PlanTexture(index, name, composed, loc, tocs, takeShared: true); }
                }
                done.Add(h.ChunkId);
                foreach (var sh in plan.Chunk.SharedChunks) done.Add(sh);
                var patches = plan.Chunk.Patches.Concat(plan.Res?.Patches ?? new()).ToList();
                added += patches.Where(p => p.Append).Sum(p => (long)p.New.Length);
                ranges += patches.Count;
                if (nhl.Contains(id)) nhlPlans.Add((name, $"Logo laid out with centre-ice ads ({how})", Team(id) ? Path.Combine(layouts, $"{id}.rgba") : "game ads", patches));
                else if (entry != null) store.AddPatches(entry, patches);
                if (preview != null && (Team(id) || id % 50 == 0)) File.WriteAllBytes(Path.Combine(preview, $"{id}.png"), ImageFiles.Png(composed, size, size));
                Console.WriteLine($"  {id,5}: {how}{(plan.Chunk.SharedChunks.Count > 0 ? $" (+{plan.Chunk.SharedChunks.Count} sharing its bytes)" : "")}  [{sw.Elapsed.TotalSeconds:0} s]");
            }
            catch (Exception ex) { Console.WriteLine($"  {id,5}: failed, left alone: {ex.Message}"); skipped++; }
        }
        Console.WriteLine($"{grown} grown, {inPlace} in place, {skipped} left alone; {ranges:N0} ranges; {added / 1048576.0:0.0} MB added to {ends.Count} CAS file(s) in {sw.Elapsed.TotalMinutes:0.0} min");
        if (!write) return 0;
        if (replace)
        {
            RefuseWhileRunning();
            foreach (var (name, summary, source, patches) in nhlPlans)
            {
                var e = store.Add("texture", name, source, summary, patches);
                store.Apply(e, game, null);
                Console.WriteLine($"  {Path.GetFileName(name)}: {store.State(e, game)}");
            }
            return 0;
        }

        // planning took a while: the game and the kit must still be closed
        RefuseWhileRunning();
        if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
            throw new InvalidOperationException("The kit was opened while planning. Nothing was written; close it and run again.");
        var meshEntry = store.Add("mesh", "logo-ads:other-ice", "intl, arcade, outdoor and threes ice", $"Other ice: logo UV3 × {LogoAds.LogoFrac} for the laid-out logos", meshPatches);
        store.Apply(meshEntry, game, null);
        Console.WriteLine($"other ice: {store.State(meshEntry, game)}");
        entry.Summary = $"Every centre logo laid out for the centre-ice ads: {grown} NHL logos at 2048 with their own ads, {inPlace} with the game's ads in place";
        store.Commit(entry);
        store.Apply(entry, game, null);
        Console.WriteLine($"logos: {store.State(entry, game)}");
        // the NHL teams' own entries, in the order planned (their added data was planned one after another)
        foreach (var (name, summary, source, patches) in nhlPlans)
        {
            var e = store.Add("texture", name, source, summary, patches);
            store.Apply(e, game, null);
            Console.WriteLine($"  {Path.GetFileName(name)}: {store.State(e, game)}");
        }
        return 0;
    }

    /// <summary>
    /// unused-jerseys [--thumbs DIR]: every kit folder with jersey textures that no team's menu (nhl_game.db
    /// stockteamjerseys) shows: free folders a slot could point at, folders of no database team, and folders a slot
    /// can't name (another brand, a name that isn't a number). Folders whose jersey is the same stored texture as a
    /// menu's are marked as copies. --thumbs writes each one's jersey texture at 256 as PNG (league_team_brand_name.png).
    /// </summary>
    private static int UnusedJerseysCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var db = new Legacy(index).OpenDb(JerseySlots.Database);
        var kits = Kits.Find(index);
        string thumbs = opts.Get("thumbs");
        if (thumbs != null) Directory.CreateDirectory(thumbs);
        var rows = JerseySlots.Unused(index, db, kits);
        int all = kits.Count(k => k.Diffuse("jersey") != null);
        Console.WriteLine($"{all} jersey folders, {all - rows.Count} in a menu, {rows.Count} not:");
        foreach (var g in rows.GroupBy(r => r.Why.Split(',')[0]).OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key}: {g.Count()} ({g.Count(r => r.CopyOf != null)} copies)");
        foreach (var r in rows)
        {
            var k = r.Kit;
            Console.WriteLine($"{k,-48} {r.Why}");
            if (thumbs == null) continue;
            try
            {
                var t = Kits.Load(index, k.Diffuse("jersey").Name, 256, 0);
                File.WriteAllBytes(Path.Combine(thumbs, $"{k.League}_{k.Team}_{k.Brand}_{k.Name}.png"), ImageFiles.Png(t.Rgba, t.Width, t.Height));
            }
            catch (Exception ex) { Console.WriteLine($"  (no picture: {ex.Message})"); }
        }
        return 0;
    }

    /// <summary>
    /// logo-ads-named [--preview DIR] [--write]: the centre logos with a name instead of a number (vast, arcade_*, event_*,
    /// hut*, eashl_*…), laid out like the rest (the game's logo and its four ads, in place), as entry
    /// "logo-ads:named-logos". Without it the ice shows them blown up and off centre, with slices of them in the ad spots.
    /// Logos whose bytes are a numbered logo's are already laid out, and left alone.
    /// </summary>
    private static int LogoAdsNamedCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string preview = opts.Get("preview");
        if (preview != null) Directory.CreateDirectory(preview);
        bool write = opts.Has("write");
        if (write)
        {
            RefuseWhileRunning();
            if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
                throw new InvalidOperationException("The kit is open. Close it before changing the game's files.");
        }
        var store = Mods();
        const string EntryName = "logo-ads:named-logos";
        if (store.Find("texture", EntryName) is { } had)
        {
            if (store.State(had, game) != ModState.NotApplied) throw new InvalidOperationException($"{EntryName} is already in the game. Restore it first.");
            if (write) store.Remove(had);
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);

        var logoName = new System.Text.RegularExpressions.Regex(@"^content/worlds/rink/textures/centerlogo/ice_centerlogo_(.+)_color$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var all = index.Res.Keys.Select(n => (Name: n, M: logoName.Match(n))).Where(x => x.M.Success).Select(x => (x.Name, Id: x.M.Groups[1].Value)).ToList();
        bool Numbered(string id) => id.All(char.IsDigit);
        var numberedBytes = all.Where(x => Numbered(x.Id)).Select(x => TextureHeader.Parse(index.Read(index.GetRes(x.Name))).ChunkId).ToHashSet();
        var named = all.Where(x => !Numbered(x.Id)).OrderBy(x => x.Id).ToList();
        Console.WriteLine($"{named.Count} named logos");

        (byte[], int, int) Rgba(string n) { var t = Texture.Load(index, n); return (t.Rgba, t.Width, t.Height); }
        var gameAds = IceAds.GameCenterAds(index, -1).Select(a => IceAds.Texture(index, a) is { } n ? Rgba(n) : (null, 0, 0)).ToList();
        ModEntry entry = write ? store.Begin("texture", EntryName, "game logos and ads", "") : null;
        var done = new HashSet<Guid>();
        int laid = 0, skipped = 0, ranges = 0;
        foreach (var (name, id) in named)
        {
            try
            {
                if (store.Entries.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { Console.WriteLine($"  {id}: has its own mod entry, left alone"); skipped++; continue; }
                var h = TextureHeader.Parse(index.Read(index.GetRes(name)));
                if (numberedBytes.Contains(h.ChunkId)) { Console.WriteLine($"  {id}: same bytes as a numbered logo (laid out already)"); skipped++; continue; }
                if (done.Contains(h.ChunkId)) { Console.WriteLine($"  {id}: same bytes as one already done"); skipped++; continue; }
                var (logo, lw, lh) = Rgba(name);
                if (lw != lh) { Console.WriteLine($"  {id}: not square ({lw}x{lh}), left alone"); skipped++; continue; }
                var composed = LogoAds.Compose(logo, lw, gameAds, lw);
                TextureReplacementPlan plan;
                try { plan = Replacements.PlanTexture(index, name, composed, loc, tocs); }
                catch (InvalidDataException ex) when (ex.Message.Contains("also used by")) { plan = Replacements.PlanTexture(index, name, composed, loc, tocs, takeShared: true); }
                done.Add(h.ChunkId);
                foreach (var sh in plan.Chunk.SharedChunks) done.Add(sh);
                var patches = plan.Chunk.Patches.Concat(plan.Res?.Patches ?? new()).ToList();
                ranges += patches.Count;
                if (entry != null) store.AddPatches(entry, patches);
                if (preview != null) File.WriteAllBytes(Path.Combine(preview, $"{id}.png"), ImageFiles.Png(composed, lw, lw));
                laid++;
                Console.WriteLine($"  {id}: game ads, {lw}, in place{(plan.Chunk.SharedChunks.Count > 0 ? $" (+{plan.Chunk.SharedChunks.Count} sharing its bytes)" : "")}  [{sw.Elapsed.TotalSeconds:0} s]");
            }
            catch (Exception ex) { Console.WriteLine($"  {id}: failed, left alone: {ex.Message}"); skipped++; }
        }
        Console.WriteLine($"{laid} laid out, {skipped} left alone; {ranges:N0} ranges in {sw.Elapsed.TotalMinutes:0.0} min");
        if (!write) return 0;
        RefuseWhileRunning();
        if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
            throw new InvalidOperationException("The kit was opened while planning. Nothing was written; close it and run again.");
        entry.Summary = $"The named centre logos (vast, arcade, events, HUT…) laid out for the centre-ice ads: {laid} with the game's ads in place";
        store.Commit(entry);
        store.Apply(entry, game, null);
        Console.WriteLine($"named logos: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>
    /// texture-grow NAME --like REF [--layout FILE.rgba --layout-size W,H] [--out DIR] [--write]: replaces a texture with
    /// one at REF's size (a texture the game has at that size), Replacements.PlanTextureResize. The image is a centre
    /// logo cut from a TheFaceoff.net layout (raw RGBA) at that size, else the texture as it is, scaled up. Prints the
    /// plan and writes the result as PNG; --write puts it in the game as a texture mod entry.
    /// </summary>
    private static int TextureGrowCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("texture-grow needs a texture name");
        string like = opts.Get("like") ?? throw new ArgumentException("texture-grow needs --like REF");
        string outDir = opts.Get("out") ?? Path.Combine(KitPaths.Export, "texture-grow");
        Directory.CreateDirectory(outDir);
        var res = index.GetRes(name) ?? throw new ArgumentException($"no texture {name}");
        var h = TextureHeader.Parse(index.Read(res));
        var rr = index.GetRes(like) ?? throw new ArgumentException($"no texture {like}");
        var rh = TextureHeader.Parse(index.Read(rr));
        string Line(TextureHeader x) => $"{x.Width}x{x.Height} {x.Format.Name} pool={x.PoolId} flags={x.Flags} mips={x.MipCount} first={x.FirstMip} u32/33={x.Unknown32}/{x.Unknown33} size={x.ChunkSize:N0} offsets={x.MipOffsets[0]},{x.MipOffsets[1]}";
        Console.WriteLine($"{name}\n  now  {Line(h)}  meta={Convert.ToHexString(res.ResMeta)}");
        Console.WriteLine($"{like}\n  like {Line(rh)}  meta={Convert.ToHexString(rr.ResMeta)}");

        byte[] rgba;
        if (opts.Get("layout") is { } layout)
        {
            if (rh.Width != rh.Height) throw new ArgumentException("a centre logo is square");
            var wh = (opts.Get("layout-size") ?? "5000,2236").Split(',').Select(int.Parse).ToArray();
            var pic = File.ReadAllBytes(layout);
            rgba = CenterIce.Logo(pic, wh[0], wh[1], CenterIce.Measure(pic, wh[0], wh[1]), new CenterIceLook(), rh.Width);
        }
        else
        {
            var t = Texture.Load(index, name);
            rgba = MipChain.Resize(t.Rgba, t.Width, t.Height, rh.Width, rh.Height, TextureBuilder.IsSrgb(h));
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var plan = Replacements.PlanTextureResize(index, name, rgba, like, loc, tocs);
        Console.WriteLine($"  new  {Line(plan.Header)}  (planned in {sw.Elapsed.TotalSeconds:0.0} s)");
        var patches = plan.Chunk.Patches.Concat(plan.Res.Patches).ToList();
        foreach (var p in patches)
            Console.WriteLine($"    {p.Note,-22} {p.New.Length,10:N0} bytes at 0x{p.Offset:X} in {p.File}{(p.Append ? " (appended)" : "")}");
        foreach (var n in plan.Chunk.Notes.Concat(plan.Res.Notes)) Console.WriteLine($"    note: {n}");
        File.WriteAllBytes(Path.Combine(outDir, Path.GetFileName(name) + $"_{plan.Header.Width}.png"), plan.Result.ToPng());
        Console.WriteLine($"  wrote {outDir}");
        if (!opts.Has("write")) return 0;

        RefuseWhileRunning();
        if (new[] { "HMS", "NHL27Kit" }.SelectMany(System.Diagnostics.Process.GetProcessesByName).Any(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero))
            throw new InvalidOperationException("The kit is open. Close it before changing the game's files.");
        var store = Mods();
        if (store.Find("texture", name) is { } had)
        {
            if (store.State(had, game) != ModState.NotApplied)
                throw new InvalidOperationException($"{name} already has a mod entry in the game ({had.Summary}). Restore it first.");
            store.Remove(had);
        }
        var entry = store.Add("texture", name, opts.Get("layout") ?? "scaled up", $"{plan.Header.Format.Name} {plan.Header.Width}×{plan.Header.Height} (was {h.Width}×{h.Height}), added at the end of its CAS file",
            patches, plan.Chunk.Notes.Concat(plan.Res.Notes));
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>retexture NAME --file F.dds [--source ORIGINAL]: replaces a texture in the game (restoring its earlier mod entry
    /// first), as the Textures page does. The image is read as DDS.</summary>
    private static int RetextureCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        var index = OpenIndex(game, false);
        string name = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("retexture needs a texture name");
        string file = opts.Get("file") ?? throw new ArgumentException("retexture needs --file");
        var store = Mods();
        if (store.Find("texture", name) is { } old) { store.Restore(old, game, Console.WriteLine); store.Remove(old); }
        var header = TextureHeader.Parse(index.Read(index.GetRes(name) ?? throw new ArgumentException($"no texture {name}")));
        var (rgba, w, h) = DdsReader.Load(file);
        if (w != header.Width || h != header.Height) rgba = MipChain.Resize(rgba, w, h, header.Width, header.Height, TextureBuilder.IsSrgb(header));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tocs = new TocCache(index);
        var plan = Replacements.PlanTexture(index, name, rgba, LocationIndex.Build(index, tocs), tocs);
        Console.WriteLine($"encoded and planned in {sw.Elapsed.TotalSeconds:0.0} s");
        var hd = plan.Header;
        var entry = store.Add("texture", name, opts.Get("source") ?? file, $"{hd.Format.Name} {hd.Width}×{hd.Height}, {TextureBuilder.StoredMips(hd)} stored mips",
            plan.Chunk.Patches, plan.Chunk.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>bc-test IN.rgba W H OUT.rgba [--mips N]: encodes raw RGBA8 as BC7 sRGB (the replace path: mips by Halve, then
    /// BcEncoder) and writes the decoded mip levels one after another, with the encode time.</summary>
    private static int BcTestCmd(Options opts)
    {
        var a = opts.Positional;
        byte[] rgba; int w, h;
        if (DdsReader.IsDds(a[0])) { (rgba, w, h) = DdsReader.Load(a[0]); if (opts.Has("dds-only")) { File.WriteAllBytes(a[3], rgba); Console.WriteLine($"DDS {w}x{h}"); return 0; } }
        else { rgba = File.ReadAllBytes(a[0]); w = int.Parse(a[1]); h = int.Parse(a[2]); }
        int mips = int.TryParse(opts.Get("mips"), out int m) ? m : 4;
        var fmt = TextureFormat.Of(0x43);
        using var outFile = File.Create(a[3]);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < mips; i++)
        {
            if (i > 0) (rgba, w, h) = MipChain.Halve(rgba, w, h, true);
            var blocks = BcEncoder.Encode(rgba, w, h, fmt);
            var back = BcDecoder.Decode(blocks, w, h, fmt);
            outFile.Write(back);
        }
        Console.WriteLine($"encoded {mips} levels in {sw.ElapsedMilliseconds} ms");
        return 0;
    }

    /// <summary>roster-repair [--dry 1]: gives moved players keys in their team, fills empty line slots, sorts the roster tables.</summary>
    private static int RosterRepairCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        if (opts.Has("dry"))
        {
            var db = new Legacy(index).OpenDb(Roster.Database);
            Console.WriteLine(RosterSync.Repair(db) ?? "nothing to repair");
            var (ok, total) = db.CheckCrcs();
            Console.WriteLine($"in memory: CRCs {ok}/{total}; a second repair: {RosterSync.Repair(db) ?? "nothing to repair"}");
            return 0;
        }
        RefuseWhileRunning();
        var entry = DbEditor.Save(index, Mods(), game, Roster.Database, RosterSync.Repair, Console.WriteLine, "Roster sync");
        Console.WriteLine(entry == null ? "nothing to repair" : $"state: {Mods().State(entry, game)}");
        return 0;
    }

    /// <summary>roster-mirror: copies the exhibition roster table's team/jersey changes into the online copy the game plays with.</summary>
    private static int RosterMirrorCmd(string game, Options opts)
    {
        RefuseWhileRunning();
        var index = OpenIndex(game, false);
        var entry = DbEditor.Save(index, Mods(), game, Roster.Database, RosterSync.Mirror, Console.WriteLine, "Roster sync");
        Console.WriteLine(entry == null ? "nothing to copy" : $"{entry.Summary}\nstate: {Mods().State(entry, game)}");
        return 0;
    }

    /// <summary>roster-sync [--team TOR] [--all]: compares the game's rosters with the NHL API's current ones (read only).</summary>
    private static int RosterSyncCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var db = new Legacy(index).OpenDb(Roster.Database);
        string team = opts.Get("team");
        var api = (team != null ? NhlApi.RosterAsync(team.ToUpperInvariant()) : NhlApi.AllRostersAsync()).GetAwaiter().GetResult();
        var changes = RosterSync.Compare(db, api, opts.Has("sizes"));
        if (team != null) changes = changes.Where(c => c.Api?.Team == team.ToUpperInvariant() || c.FromTeamName != null && c.Kind == SyncKind.Left && c.FromTeam == RosterSync.TeamIndex(db)[team.ToUpperInvariant()]).ToList();
        Console.WriteLine($"{api.Count} players on NHL rosters; {changes.Count} differences");
        foreach (var g in changes.GroupBy(c => c.Kind))
        {
            Console.WriteLine($"{g.Key}: {g.Count()}");
            foreach (var c in g.Take(opts.Has("all") ? 10000 : 12))
                Console.WriteLine($"  {(c.Selected ? "[x]" : "[ ]")} {c.Name,-26} {c.Position,-2} {c.Description}{(c.NameOnly ? "  (name match only)" : "")}");
        }
        if (opts.Has("dry"))
        {
            // applies the ticked changes to the database in memory only, then compares again
            Console.WriteLine(RosterSync.Apply(db, changes.Where(c => c.Selected)));
            var (ok, total) = db.CheckCrcs();
            var again = RosterSync.Compare(db, api, opts.Has("sizes"));
            Console.WriteLine($"in memory: CRCs {ok}/{total}; differences left: " + string.Join(", ", again.GroupBy(c => c.Kind).Select(g => $"{g.Key} {g.Count()}")));
        }
        return 0;
    }

    private static int Export(string game, Options opts)
    {
        // an EBX and its RES often share a name: a leading kind picks one (default: ebx, then res)
        var args = opts.Positional;
        string kind = args.Count > 1 && args[0] is "ebx" or "res" or "chunk" ? args[0] : null;
        string name = (kind != null ? args.Skip(1) : args).FirstOrDefault() ?? throw new ArgumentException("export needs an asset name or chunk guid");
        string outDir = opts.Get("out") ?? KitPaths.Export;
        var index = OpenIndex(game, false);
        AssetEntry entry = kind switch
        {
            "ebx" => index.GetEbx(name),
            "res" => index.GetRes(name),
            "chunk" => Guid.TryParse(name, out var cid) ? index.GetChunk(cid) : null,
            _ => Guid.TryParse(name, out var id) ? index.GetChunk(id) : (AssetEntry)index.GetEbx(name) ?? index.GetRes(name),
        };
        if (entry == null)
            throw new ArgumentException($"no {kind ?? "ebx, res or chunk"} named '{name}'");
        string ext = entry switch { EbxEntry => ".ebx", ResEntry => ".res", _ => ".chunk" };
        string file = Path.Combine(outDir, (entry is ChunkEntry c ? c.Id.ToString() : entry.Name).Replace('/', Path.DirectorySeparatorChar) + ext);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        byte[] data = index.Read(entry);
        File.WriteAllBytes(file, data);
        Console.WriteLine($"{data.Length} bytes -> {file}");
        return 0;
    }
}

/// <summary>--key value / --flag options and positional arguments.</summary>
internal sealed class Options
{
    private readonly Dictionary<string, string> _named = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Key, string Value)> _all = new();
    public List<string> Positional { get; } = new();

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                string key = args[i][2..];
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
                o._named[key] = hasValue ? args[++i] : "";
                o._all.Add((key, o._named[key]));
            }
            else
            {
                o.Positional.Add(args[i]);
            }
        }
        return o;
    }

    public string Get(string key) => _named.TryGetValue(key, out var v) && v != "" ? v : null;
    public bool Has(string key) => _named.ContainsKey(key);
    /// <summary>Every value of an option given more than once (--set a=1 --set b=2).</summary>
    public IEnumerable<string> All(string key) => _all.Where(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && a.Value != "").Select(a => a.Value);
}
