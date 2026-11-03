using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// The app's side of mods: the mod list (in the kit folder's mods\), planning replacements off the UI thread, and
/// applying or restoring them in the game folder. One operation at a time; nothing is written while KytyPS5 runs.
/// </summary>
public sealed class ModService
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly TocCache _tocs;
    private LocationIndex _locations;

    public ModService(AssetIndex index)
    {
        Index = index;
        _tocs = new TocCache(index);
        Store = new ModStore(Path.Combine(KitPaths.Root, "mods"));
    }

    public AssetIndex Index { get; }
    public ModStore Store { get; }
    public string Game => Index.Layout.GameDir;

    /// <summary>Raised (on the UI thread's caller) after the list or the game changes.</summary>
    public event Action Changed;

    public static bool GameRunning => Process.GetProcessesByName("kyty_emulator").Length > 0;

    public ModEntry Find(string kind, string name) => Store.Find(kind, name);

    public ModState State(ModEntry e) => Store.State(e, Game);

    private async Task<T> Run<T>(Func<T> work)
    {
        await _gate.WaitAsync();
        try { return await Task.Run(work); }
        finally { _gate.Release(); }
    }

    private LocationIndex Locations() => _locations ??= LocationIndex.Build(Index, _tocs);

    /// <summary>Restores an asset's earlier replacement if it is in the game, so a new one starts from the original.</summary>
    public async Task ResetAsync(string kind, string name)
    {
        var e = Find(kind, name);
        if (e == null) return;
        var state = await Run(() => State(e));
        if (state is ModState.Applied or ModState.Partial)
            await RestoreAsync(e);
    }

    public Task<TextureReplacementPlan> PlanTextureAsync(string name, string imageFile, Action<string> note, bool takeShared = false) => Run(() =>
    {
        var res = Index.GetRes(name) ?? throw new InvalidDataException($"No texture named {name}.");
        var header = TextureHeader.Parse(Index.Read(res));
        string why = TextureBuilder.Unsupported(header);
        if (why != null) throw new NotSupportedException(why);
        var (rgba, w, h) = ImageLoader.Load(imageFile);
        if (w != header.Width || h != header.Height)
        {
            note?.Invoke($"resized from {w} x {h} to {header.Width} x {header.Height}");
            rgba = MipChain.Resize(rgba, w, h, header.Width, header.Height, TextureBuilder.IsSrgb(header));
        }
        return Replacements.PlanTexture(Index, name, rgba, Locations(), _tocs, takeShared: takeShared);
    });

    /// <summary>
    /// Replaces one layer of a texture array in the game. An array has one mod entry for all its replaced layers: the new
    /// layer is added to the array as the game has it now (earlier layers included), the earlier entry is restored, and
    /// original → new is recorded and applied as one entry, so Restore undoes every layer.
    /// </summary>
    public async Task<TextureReplacementPlan> ReplaceArrayLayerAsync(string name, string imageFile, int layer, Action<string> note)
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        var current = await Run(() => Texture.Load(Index, name, decode: false).Data);
        var existing = Find("texture", name);
        string history = existing != null && State(existing) == ModState.Applied ? existing.Summary : null;
        await ResetAsync("texture", name);
        try
        {
            var plan = await Run(() =>
            {
                var res = Index.GetRes(name) ?? throw new InvalidDataException($"No texture named {name}.");
                var header = TextureHeader.Parse(Index.Read(res));
                var (rgba, w, h) = ImageLoader.Load(imageFile);
                if (w != header.Width || h != header.Height)
                {
                    note?.Invoke($"resized from {w} x {h} to {header.Width} x {header.Height}");
                    rgba = MipChain.Resize(rgba, w, h, header.Width, header.Height, TextureBuilder.IsSrgb(header));
                }
                return Replacements.PlanTexture(Index, name, rgba, Locations(), _tocs, layer: layer, arrayData: current);
            });
            var hd = plan.Header;
            string summary = (history != null ? history + "; " : $"{hd.Format.Name} {hd.Width}×{hd.Height} array of {hd.Layers}: ") + $"layer {layer} from {Path.GetFileName(imageFile)}";
            var e = Queue("texture", name, imageFile, summary, plan.Chunk.Patches, new List<string>());
            await ApplyAsync(e);
            return plan;
        }
        catch
        {
            // put the earlier layers back
            if (existing != null && Store.Entries.Contains(existing)) { try { await ApplyAsync(existing); } catch { } }
            throw;
        }
    }

    public Task<SoundReplacementPlan> PlanSoundAsync(string name, SoundStream stream, string audioFile) => Run(() =>
    {
        var (pcm, ch, rate) = AudioLoader.Load(audioFile);
        return Replacements.PlanSound(Index, name, stream, pcm, ch, rate, Locations(), _tocs);
    });

    public ModEntry Queue(string kind, string name, string source, string summary, IReadOnlyList<PatchData> patches, IEnumerable<string> warnings)
    {
        var e = Store.Add(kind, name, source, summary, patches, warnings);
        Changed?.Invoke();
        return e;
    }

    public void Remove(ModEntry e)
    {
        Store.Remove(e);
        Changed?.Invoke();
    }

    public async Task ApplyAsync(ModEntry e)
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        try { await Run(() => { Store.Apply(e, Game); return 0; }); }
        finally { Changed?.Invoke(); }
    }

    public async Task RestoreAsync(ModEntry e)
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        try { await Run(() => { Store.Restore(e, Game); return 0; }); }
        finally { Changed?.Invoke(); }
    }

    public const string EbxKind = "ebx";

    /// <summary>
    /// Rewrites an EBX in place (same length; a kit stamp's hotspots) as one mod entry. An earlier save of it is restored
    /// first, so the entry always goes from the original; bytes equal to the original just drop the entry.
    /// </summary>
    public async Task<ModEntry> SaveEbxAsync(string name, byte[] data, string summary)
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        try
        {
            return await Run(() =>
            {
                var existing = Find(EbxKind, name);
                if (existing != null && State(existing) is ModState.Applied or ModState.Partial) Store.Restore(existing, Game);
                ChunkPlan plan;
                try { plan = ResPatcher.PlanEbx(Index, name, data, Locations(), _tocs); }
                catch
                {
                    if (existing != null) Store.Apply(existing, Game);
                    throw;
                }
                if (plan.Patches.Count == 0)
                {
                    if (existing != null) Store.Remove(existing);
                    return null;
                }
                string sources = Path.Combine(Store.Dir, "sources");
                Directory.CreateDirectory(sources);
                string copy = Path.Combine(sources, $"ebx_{Legacy.Fnv1(name):x16}.ebx");
                File.WriteAllBytes(copy, data);
                var e = Store.Add(EbxKind, name, copy, summary, plan.Patches, plan.Notes);
                Store.Apply(e, Game);
                return e;
            });
        }
        finally { Changed?.Invoke(); }
    }

    public const string MovieKind = "movie";

    public Task<MovieTarget> MovieTargetAsync(Movie m) => Run(() => MovieReplacement.Target(Index, m));

    /// <summary>Plans a ready WebM (encoded to fit) over a movie, and keeps a copy of it as the mod's source.</summary>
    public Task<MoviePlan> PlanMovieAsync(Movie m, byte[] webm) => Run(() =>
    {
        var plan = MovieReplacement.Plan(Index, m, webm, Locations(), _tocs);
        string sources = Path.Combine(Store.Dir, "sources");
        Directory.CreateDirectory(sources);
        plan.Source = Path.Combine(sources, $"movie_{Legacy.Fnv1(m.Name):x16}.webm");
        File.WriteAllBytes(plan.Source, webm);
        return plan;
    });

    /// <summary>The score clock as the game has it right now (decompiled from tin_nhl.swf; never cached).</summary>
    public Task<Scorebug> LoadScorebugAsync() => Run(() => Scorebug.Load(Index));

    /// <summary>Writes score clock edits into the game (recompiles tin_nhl.swf; one mod entry).</summary>
    public async Task SaveScorebugAsync(Scorebug current, IReadOnlyDictionary<string, string> edits, bool sharpBadge = false)
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        try { await Run(() => Scorebug.Save(Index, Store, Game, current, edits, sharpBadge: sharpBadge)); }
        finally { Changed?.Invoke(); }
    }

    /// <summary>A database as the game has it right now (never cached: edits made anywhere show up).</summary>
    public Task<T3Db> OpenDbAsync(string name) => Run(() => new Legacy(Index).OpenDb(name));

    /// <summary>The legacy file names (databases among them), read from the game's index chunk.</summary>
    public Task<List<string>> LegacyNamesAsync() => Run(() => new Legacy(Index).Names);

    /// <summary>Edits a database in the game: <paramref name="edit"/> gets it as the game has it now and returns what it changed.</summary>
    public async Task<ModEntry> SaveDbAsync(string name, Func<T3Db, string> edit, string source = "Database page")
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        try { return await Run(() => DbEditor.Save(Index, Store, Game, name, edit, source: source)); }
        finally { Changed?.Invoke(); }
    }

    /// <summary>Writes a pack of these mods (a database's current bytes are read from the game).</summary>
    public Task<List<string>> ExportPackAsync(string file, string title, IReadOnlyList<ModEntry> entries) => Run(() =>
        ModPack.Export(file, title, entries, e =>
        {
            if (Store.State(e, Game) != ModState.Applied) throw new InvalidOperationException("its edits are not in the game");
            var legacy = new Legacy(Index);
            return legacy.Read(e.Name);
        }));

    /// <summary>
    /// Imports a pack: every item is rebuilt against this game and written into it (through the mod list). Returns one
    /// line per item, saying what happened.
    /// </summary>
    public async Task<List<string>> ImportPackAsync(string file, Action<string> progress)
    {
        if (GameRunning) throw new InvalidOperationException("KytyPS5 is running. Close the game before changing its files.");
        var manifest = await Run(() => ModPack.Open(file, Path.Combine(Store.Dir, "sources")));
        var results = new List<string>();
        int n = 0;
        foreach (var item in manifest.Items)
        {
            n++;
            progress?.Invoke($"{n}/{manifest.Items.Count}: {item.Name}");
            try
            {
                switch (item.Kind)
                {
                    case "texture":
                    {
                        await ResetAsync("texture", item.Name);
                        var notes = new List<string>();
                        var plan = await PlanTextureAsync(item.Name, item.File, x => notes.Add(x));
                        var h = plan.Header;
                        var e = Queue("texture", item.Name, item.File, $"{h.Format.Name} {h.Width}×{h.Height}, {TextureBuilder.StoredMips(h)} stored mips (from a pack)", plan.Chunk.Patches, notes);
                        await ApplyAsync(e);
                        break;
                    }
                    case "sound":
                    {
                        await ResetAsync("sound", item.Name);
                        int hash = item.Name.LastIndexOf('#');
                        string wave = hash < 0 ? item.Name : item.Name[..hash];
                        int variation = hash < 0 ? 1 : int.Parse(item.Name[(hash + 1)..]);
                        var ebx = Index.GetEbx(wave) ?? throw new InvalidDataException($"The game has no sound {wave}.");
                        var stream = await Run(() => Audio.Variations(Index, ebx)[variation - 1]);
                        var plan = await PlanSoundAsync(wave, stream, item.File);
                        var o = plan.Original;
                        var e = Queue("sound", item.Name, item.File, $"{o.CodecName} {o.Channels} ch → {plan.Codec} {plan.BitratePerChannel / 1000} kbps/ch, {plan.Result.Seconds:0.0} s (from a pack)", plan.Chunk.Patches, plan.Warnings);
                        await ApplyAsync(e);
                        break;
                    }
                    case DbEditor.Kind:
                    {
                        var bytes = await File.ReadAllBytesAsync(item.File);
                        await SaveDbAsync(item.Name, db =>
                        {
                            if (db.Data.Length != bytes.Length) throw new InvalidDataException($"the pack's {item.Name} is {bytes.Length:N0} bytes, this game's {db.Data.Length:N0}");
                            if (db.Data.AsSpan().SequenceEqual(bytes)) return null;
                            bytes.CopyTo(db.Data, 0);
                            return $"from pack {manifest.Title ?? Path.GetFileName(file)}: {item.Summary}";
                        }, Path.GetFileName(file));
                        break;
                    }
                    case LegacyEditor.Kind:
                    {
                        var bytes = await File.ReadAllBytesAsync(item.File);
                        await Run(() =>
                        {
                            string collector = LegacyEditor.CollectorOf(Index, item.Name) ?? throw new InvalidDataException($"The game has no {item.Name}.");
                            return LegacyEditor.Save(Index, Store, Game, collector, item.Name, bytes, $"from pack {manifest.Title ?? Path.GetFileName(file)}: {item.Summary}");
                        });
                        break;
                    }
                    case MovieKind:
                    {
                        await ResetAsync(MovieKind, item.Name);
                        var movie = await Run(() => Movies.Get(Index, item.Name)) ?? throw new InvalidDataException($"The game has no movie {item.Name}.");
                        var plan = await PlanMovieAsync(movie, await File.ReadAllBytesAsync(item.File));
                        var e = Queue(MovieKind, item.Name, plan.Source, $"{item.Summary} (from a pack)", plan.Patches, plan.Notes);
                        await ApplyAsync(e);
                        break;
                    }
                    case EbxKind:
                        await SaveEbxAsync(item.Name, await File.ReadAllBytesAsync(item.File), $"from pack {manifest.Title ?? Path.GetFileName(file)}: {item.Summary}");
                        break;
                    default:
                        throw new NotSupportedException($"unknown kind {item.Kind}");
                }
                results.Add($"✓ {item.Name}");
            }
            catch (Exception ex)
            {
                results.Add($"✗ {item.Name}: {ex.Message}");
            }
        }
        Changed?.Invoke();
        return results;
    }

    public Task<Dictionary<ModEntry, ModState>> StatesAsync() =>
        Run(() => Store.Entries.ToDictionary(e => e, e => { try { return Store.State(e, Game); } catch { return ModState.Missing; } }));
}

/// <summary>Reads images (PNG, JPG, BMP, TIFF, GIF) as RGBA8 through WPF's decoders.</summary>
public static class ImageLoader
{
    public const string Filter = "Images|*.png;*.dds;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|DDS|*.dds|All files|*.*";

    public static (byte[] Rgba, int W, int H) Load(string file)
    {
        // a DDS (BC1/BC3/BC5/BC7 or 32-bit) is read by the kit; it's then encoded into the game texture's format like any image
        if (DdsReader.IsDds(file)) return DdsReader.Load(file);
        var decoder = BitmapDecoder.Create(new Uri(file), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        var bgra = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        var px = new byte[w * h * 4];
        bgra.CopyPixels(px, w * 4, 0);
        for (int i = 0; i < px.Length; i += 4) (px[i], px[i + 2]) = (px[i + 2], px[i]);
        return (px, w, h);
    }
}

/// <summary>Reads audio: WAV directly, anything else (MP3, OGG, FLAC, M4A...) through ffmpeg when it is installed.</summary>
public static class AudioLoader
{
    public const string Filter = "Audio|*.wav;*.mp3;*.ogg;*.flac;*.m4a;*.aac;*.wma;*.opus|WAV|*.wav|All files|*.*";

    public static (short[] Pcm, int Channels, int Rate) Load(string file)
    {
        if (Path.GetExtension(file).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            try { return WavReader.Read(File.ReadAllBytes(file)); }
            catch (NotSupportedException) when (FindFfmpeg() != null) { /* an unusual WAV: let ffmpeg convert it */ }
        }
        string ffmpeg = FindFfmpeg() ?? throw new NotSupportedException("Only WAV files can be read. Install ffmpeg to use MP3, OGG, FLAC and others.");
        string tmp = Path.Combine(Path.GetTempPath(), "NHL27Kit", Guid.NewGuid().ToString("N") + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(tmp));
        try
        {
            var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (string a in new[] { "-y", "-v", "error", "-i", file, "-ar", "48000", "-c:a", "pcm_s16le", tmp }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0 || !File.Exists(tmp)) throw new InvalidDataException("ffmpeg could not read the file: " + err.Trim());
            return WavReader.Read(File.ReadAllBytes(tmp));
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    public static string FindFfmpeg() => Ffmpeg.Find();
}
