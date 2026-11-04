using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;

namespace NHL27Kit.App;

public partial class MainWindow : Window
{
    public static readonly RoutedCommand FocusSearch = new();

    /// <summary>Most rows the list shows at once; narrowing the folder, type or search shows the rest.</summary>
    private const int RowCap = 5000;

    private readonly Settings _settings = Settings.Load();
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private AssetIndex _index;
    private Catalog _ebx, _res, _chunks;
    private AssetKind _kind = AssetKind.Ebx;
    private FolderNode _folder;
    private CancellationTokenSource _filterCts, _previewCts;
    private bool _suppress;
    private string _title, _version, _titleId;
    /// <summary>The game in the game folder (NHL 27 or NHL 24): which pages show, and its own cache and mods.</summary>
    private GameProfile _profile = GameProfile.None;

    public MainWindow()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); Refresh(); };
    }

    private string Game => GameDir.Text.Trim().TrimEnd('\\', '/');

    // ================================================================ startup

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // --game DIR opens another game for this run without changing the saved choice (scripted shots)
        GameDir.Text = App.Args.TryGetValue("game", out var argGame) ? argGame
            : KitPaths.IsGame(_settings.GameDir) ? _settings.GameDir : KitPaths.FindGame() ?? _settings.GameDir ?? "";
        AskGame.IsChecked = _settings.AskForGame;
        // --picker shows the game picker (a scripted shot of it ends there)
        if (App.Args.ContainsKey("picker") || (argGame == null && !App.Args.ContainsKey("shot") && _settings.AskForGame && KnownGames().Count > 1))
        {
            var pick = PickGameAsync();
            if (App.Args.ContainsKey("shot"))
            {
                await Shot();
                return;
            }
            OpenPicked(await pick);
        }
        ShowGameInfo();
        PlayPage.StateChanged += SetState;
        PlayPage.StatusChanged += text => Status.Text = text;
        PlayPage.Init(_settings, Game, _titleId, _title, _version);
        Play24Page.StateChanged += SetState;
        Play24Page.StatusChanged += text => Status.Text = text;
        Play24Page.Init(_settings, Game, _titleId, _title, _version);
        TexturePage.StatusChanged += text => Status.Text = text;
        TexturePage.ShowInExplorer += ShowAssetInExplorer;
        SoundPage.StatusChanged += text => Status.Text = text;
        SoundPage.ShowInExplorer += ShowAssetInExplorer;
        ModsPage.StatusChanged += text => Status.Text = text;
        RosterPage.StatusChanged += text => Status.Text = text;
        UniformsPage.StatusChanged += text => Status.Text = text;
        StudioPage.StatusChanged += text => Status.Text = text;
        StudioPage.SlotsChanged += UniformsPage.SlotsChangedElsewhere;
        UnusedPage.StatusChanged += text => Status.Text = text;
        UnusedPage.SlotsChanged += UniformsPage.SlotsChangedElsewhere;
        ScorebugPage.StatusChanged += text => Status.Text = text;
        Scorebug24Page.StatusChanged += text => Status.Text = text;
        DatabasePage.StatusChanged += text => Status.Text = text;
        TuningPage.StatusChanged += text => Status.Text = text;
        TuningPage.GameDir = Game;
        BeAProPage.StatusChanged += text => Status.Text = text;
        ArenasPage.StatusChanged += text => Status.Text = text;
        BeAProPage.Init(_settings, _titleId);
        TeamsPage.StatusChanged += text => Status.Text = text;
        TexturePage.GoToMods += () => NavMods.IsChecked = true;
        SoundPage.GoToMods += () => NavMods.IsChecked = true;
        VideoPage.StatusChanged += text => Status.Text = text;
        VideoPage.ShowInExplorer += ShowAssetInExplorer;
        VideoPage.GoToMods += () => NavMods.IsChecked = true;
        if (App.Args.TryGetValue("page", out var page))
            (page switch { "settings" => NavSettings, "play" => NavPlay, "textures" => NavTextures, "sounds" => NavSounds, "videos" => NavVideos, "mods" => NavMods, "roster" => NavRoster, "uniforms" => NavUniforms, "studio" => NavStudio, "unused" => NavUnused, "scorebug" => NavScorebug, "database" => NavDatabase, "tuning" => NavTuning, "beapro" => NavBeAPro, "arenas" => NavArenas, "teams" => NavTeams, _ => NavExplorer }).IsChecked = true;
        if (App.Args.TryGetValue("scroll", out var scroll) && double.TryParse(scroll, out double offset))
        {
            PlayPage.UpdateLayout();
            PlayPage.Scroller.ScrollToVerticalOffset(offset);
        }
        if (App.Args.ContainsKey("no-index"))
        {
            HideSplash();
            Status.Text = "Loading the asset index…";
            await Shot();
            return;
        }
        await LoadIndex(rebuild: false);
        HideSplash();
        await ApplyArgs();
        await Shot();
    }

    private readonly System.Diagnostics.Stopwatch _sinceStart = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Fades the startup screen away, after it's been up at least a moment so it doesn't just flash.</summary>
    private async void HideSplash()
    {
        if (App.Args.ContainsKey("splash")) return;
        int left = 1100 - (int)_sinceStart.ElapsedMilliseconds;
        if (left > 0 && !App.Args.ContainsKey("shot")) await Task.Delay(left);
        if (App.Args.ContainsKey("shot")) { Splash.Visibility = Visibility.Collapsed; return; }
        Motion.Leave(Splash);
    }

    private void ShowGameInfo()
    {
        var props = new List<Property>();
        bool ok = KitPaths.IsGame(Game);
        _profile = ok ? GameProfile.Detect(Game) : GameProfile.None;
        GameCheck.Text = !ok ? "No Data\\layout.toc in this folder."
            : _profile.Kind == GameKind.Unknown ? "✓  Found Data\\layout.toc, but it isn't NHL 27 or NHL 24: pages may not read it."
            : $"✓  {_profile.Name} ({_profile.Platform}) found";
        GameCheck.Foreground = (Brush)FindResource(ok ? "Good" : "Danger");
        if (ok)
        {
            string title = _profile.Title ?? _profile.Name;
            props.Add(new Property("Game", $"{_profile.Name}  ·  {_profile.Platform}"));
            props.Add(new Property("Title", title));
            props.Add(new Property("Title id", _profile.TitleId ?? "—", true));
            props.Add(new Property("Content id", _profile.ContentId ?? "—", true));
            props.Add(new Property("Version", _profile.Version ?? "—", true));
            (_title, _version, _titleId) = (title, _profile.Version, _profile.TitleId);
            GameBadge.Text = _profile.Badge;
            SplashGame.Text = $"{_profile.Name}  ·  {_profile.Platform}";
            string patchDir = Path.Combine(Game, "Patch");
            bool patched = Directory.Exists(patchDir) && Directory.EnumerateFileSystemEntries(patchDir).Any();
            props.Add(new Property("Patch", patched ? "Installed (Patch\\ overrides Data\\)" : "None"));
        }
        else
            GameBadge.Text = "No game";
        ApplyProfile();
        GameProps.ItemsSource = props;
        bool oodle = File.Exists(Path.Combine(AppContext.BaseDirectory, "oo2core_9_win64.dll"));
        KitProps.ItemsSource = new List<Property>
        {
            new("Kit folder", KitPaths.Root, true),
            new("Index cache", KitPaths.IndexFileFor(_profile), true),
            new("Mod list", KitPaths.ModsFor(_profile), true),
            new("Exports", KitPaths.Export, true),
            new("Oodle", oodle ? "oo2core_9_win64.dll found" : "Missing: copy oo2core_9_win64.dll next to HMS.exe"),
        };
    }

    private async Task LoadIndex(bool rebuild)
    {
        if (!KitPaths.IsGame(Game))
        {
            SetState("No game folder", "Danger");
            ShowEmpty("", "Choose the game folder", "Open Settings and point the kit at the extracted NHL 27 folder (the one with eboot.bin, Data and Patch).");
            Progress.Visibility = Visibility.Collapsed;
            return;
        }
        SetState("Loading index", "Patch");
        RebuildButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        ShowEmpty("", rebuild ? "Rebuilding the index" : "Indexing NHL 27",
                  "Reading 372k bundles across 203 CAS archives. The first run takes about a minute; after that it loads from cache.");
        string game = Game;
        var sw = Stopwatch.StartNew();
        try
        {
            var (index, ebx, res, chunks) = await Task.Run(() =>
            {
                string cache = KitPaths.IndexFileFor(GameProfile.Detect(game));
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                if (rebuild && File.Exists(cache))
                    File.Delete(cache);
                var ix = AssetIndex.Open(game, File.ReadAllBytes, cache, line => Dispatcher.BeginInvoke(() => Status.Text = line.Trim()));
                Dispatcher.BeginInvoke(() => Status.Text = "Building folders…");
                return (ix, Catalog.Build(ix, AssetKind.Ebx), Catalog.Build(ix, AssetKind.Res), Catalog.Build(ix, AssetKind.Chunk));
            });
            _index = index;
            (_ebx, _res, _chunks) = (ebx, res, chunks);
            KindEbx.Tag = Short(ebx.Count);
            KindRes.Tag = Short(res.Count);
            KindChunk.Tag = Short(chunks.Count);
            Summary.Text = $"{index.BundleList.Count:N0} bundles  ·  {ebx.Count:N0} EBX  ·  {res.Count:N0} RES  ·  {chunks.Count:N0} chunks  ·  {ebx.Patched + res.Patched + chunks.Patched:N0} patched";
            StatusRight.Text = $"Index ready in {sw.Elapsed.TotalSeconds:0.0}s";
            Status.Text = game;
            SetState("Index ready", "Good");
            ShowKind();
            TexturePage.SetIndex(index);
            SoundPage.SetIndex(index);
            VideoPage.SetIndex(index);
            try
            {
                var mods = new ModService(index);
                TexturePage.SetMods(mods);
                SoundPage.SetMods(mods);
                VideoPage.SetMods(mods);
                ModsPage.SetMods(mods);
                RosterPage.SetMods(mods);
                UniformsPage.SetMods(mods);
                StudioPage.SetMods(mods);
                UnusedPage.SetMods(mods);
                ScorebugPage.SetMods(mods);
                Scorebug24Page.SetMods(mods);
                DatabasePage.SetMods(mods);
                if (NavDatabase.IsChecked == true) _ = DatabasePage.Shown();
                TeamsPage.SetMods(mods);
                BeAProPage.SetMods(mods);
                ArenasPage.SetMods(mods);
                if (NavArenas.IsChecked == true) _ = ArenasPage.Shown();
                if (NavTeams.IsChecked == true) _ = TeamsPage.Shown();
            }
            catch (Exception ex)
            {
                Status.Text = "Mods are off: " + ex.Message;
            }
        }
        catch (Exception ex)
        {
            SetState("Index failed", "Danger");
            ShowEmpty("", "Could not read the game", ex.Message);
            Status.Text = ex.Message;
        }
        finally
        {
            Progress.Visibility = Visibility.Collapsed;
            RebuildButton.IsEnabled = true;
        }
    }

    private static string Short(int n) => n >= 1_000_000 ? $"{n / 1e6:0.##}M" : n >= 1000 ? $"{n / 1e3:0.#}k" : n.ToString();

    private void SetState(string text, string brush)
    {
        StatusChipText.Text = text;
        StatusDot.Fill = (Brush)FindResource(brush);
    }

    private void ShowEmpty(string glyph, string title, string text)
    {
        EmptyGlyph.Text = glyph;
        EmptyTitle.Text = title;
        EmptyText.Text = text;
        EmptyState.Visibility = Visibility.Visible;
    }

    // ================================================================ kind, folders, filters

    private Catalog Current => _kind switch { AssetKind.Res => _res, AssetKind.Chunk => _chunks, _ => _ebx };

    private void OnKindChanged(object sender, RoutedEventArgs e)
    {
        _kind = sender == KindRes ? AssetKind.Res : sender == KindChunk ? AssetKind.Chunk : AssetKind.Ebx;
        if (_index != null)
            ShowKind();
    }

    private void ShowKind()
    {
        var cat = Current;
        _suppress = true;
        TypeFilter.ItemsSource = cat.Types;
        TypeFilter.SelectedIndex = 0;
        Folders.ItemsSource = cat.Root.Children.Count > 0 ? cat.Root.Children : new List<FolderNode> { cat.Root };
        _suppress = false;
        _folder = cat.Root;
        Refresh();
    }

    private void OnFolderSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _folder = e.NewValue as FolderNode ?? Current?.Root;
        if (!_suppress)
            Refresh();
    }

    private void OnCollapseAll(object sender, RoutedEventArgs e)
    {
        if (Current == null)
            return;
        void Collapse(FolderNode n)
        {
            if (!n.IsExpanded) return;
            n.IsExpanded = false;
            foreach (var c in n.Children) Collapse(c);
        }
        foreach (var c in Current.Root.Children) Collapse(c);
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppress) Refresh();
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppress && _index != null) Refresh();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void OnFocusSearch(object sender, ExecutedRoutedEventArgs e)
    {
        NavExplorer.IsChecked = true;
        Search.Focus();
        Search.SelectAll();
    }

    /// <summary>Filters the current folder's assets (and the folders below it) off the UI thread.</summary>
    private async void Refresh()
    {
        if (_index == null)
            return;
        _filterCts?.Cancel();
        var cts = _filterCts = new CancellationTokenSource();
        var folder = _folder ?? Current.Root;
        string type = (TypeFilter.SelectedItem as TypeChoice)?.Type;
        bool patchedOnly = PatchedOnly.IsChecked == true;
        string[] terms = Search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        ListTitle.Text = folder.Path.Length == 0 ? $"ALL {KindLabel(_kind)}" : folder.Path.ToUpperInvariant();
        (List<AssetRow> rows, int total) result;
        try
        {
            result = await Task.Run(() =>
            {
                var rows = new List<AssetRow>(Math.Min(RowCap, folder.Total));
                int total = 0;
                foreach (var r in folder.AllItems())
                {
                    if (cts.IsCancellationRequested) break;
                    if (type != null && r.TypeText != type) continue;
                    if (patchedOnly && !r.IsPatched) continue;
                    if (terms.Length > 0 && !terms.All(t => r.Name.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                    if (rows.Count < RowCap) rows.Add(r);
                    total++;
                }
                return (rows, total);
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cts.IsCancellationRequested)
            return;
        Assets.ItemsSource = result.rows;
        ListCount.Text = result.total > result.rows.Count ? $"{result.rows.Count:N0} of {result.total:N0} shown" : $"{result.total:N0} items";
        if (result.total == 0)
            ShowEmpty("", "No matches", "Try fewer search words, another type, or a folder higher up.");
        else
            EmptyState.Visibility = Visibility.Collapsed;
    }

    /// <summary>Lets long asset names wrap after '_' and '/' instead of mid-word.</summary>
    private static string Breakable(string s) => s.Replace("_", "_​").Replace("/", "/​");

    private static string KindLabel(AssetKind k) => k switch { AssetKind.Res => "RES", AssetKind.Chunk => "CHUNKS", _ => "EBX" };

    // ================================================================ details

    private AssetRow Selected => Assets.SelectedItem as AssetRow;

    private async void OnAssetSelected(object sender, SelectionChangedEventArgs e)
    {
        var row = Selected;
        DetailsPane.Visibility = row == null ? Visibility.Collapsed : Visibility.Visible;
        DetailsEmpty.Visibility = row == null ? Visibility.Visible : Visibility.Collapsed;
        if (row == null)
            return;

        DetailGlyph.Text = row.Glyph;
        DetailGlyph.Foreground = row.Tint;
        DetailType.Text = row.TypeText;
        DetailKind.Text = row.Kind switch { AssetKind.Res => "Resource (RES)", AssetKind.Chunk => "Data chunk", _ => "Asset (EBX)" } + (row.IsPatched ? "  ·  from the 01.003 patch" : "");
        DetailName.Text = Breakable(row.Leaf);
        DetailPath.Text = row.Folder.Length > 0 ? Breakable(row.Folder) : "—";
        ExportLabel.Text = row.Kind == AssetKind.Ebx && _index.GetRes(row.Name) != null ? "Export EBX + RES" : "Export";

        var en = row.Entry;
        var props = new List<Property>();
        switch (en)
        {
            case EbxEntry x:
                props.Add(new("Partition", x.Id.ToString(), true));
                break;
            case ResEntry x:
                props.Add(new("RES type", $"{ResTypes.Name(x.ResType)}  (0x{x.ResType:X8})"));
                props.Add(new("RID", $"0x{x.ResRid:X16}", true));
                break;
            case ChunkEntry x:
                props.Add(new("Chunk id", x.Id.ToString(), true));
                props.Add(new("Listed in", x.InChunkTable ? "TOC chunk table" : "bundle"));
                break;
        }
        props.Add(new("Size", $"{Visuals.Size(en.OriginalSize)}  ({en.OriginalSize:N0} bytes)"));
        props.Add(new("Stored", $"{Visuals.Size(en.Ref.Size)} compressed"));
        props.Add(new("Superbundle", Path.GetRelativePath(_index.Layout.GameDir, en.Toc)));
        string cas;
        try { cas = Path.GetRelativePath(_index.Layout.GameDir, _index.Layout.CasPath(en.Ref.Catalog, en.Ref.Cas, en.Ref.Patch)); }
        catch { cas = $"install chunk {en.Ref.Catalog}, cas {en.Ref.Cas}"; }
        props.Add(new("Archive", cas));
        props.Add(new("Offset", $"0x{en.Ref.Offset:X8}", true));
        if (en.Bundles.Count > 0)
        {
            var names = en.Bundles.Take(3).Select(b => Breakable(_index.BundleList[b].Name)).ToList();
            props.Add(new($"Bundles ({en.Bundles.Count:N0})", string.Join("\n", names) + (en.Bundles.Count > 3 ? $"\n+ {en.Bundles.Count - 3:N0} more" : "")));
        }
        if (en.Sha1 != null)
            props.Add(new("SHA1", Convert.ToHexString(en.Sha1).ToLowerInvariant(), true));
        DetailProps.ItemsSource = props;
        ShowTexturePreview(row);
        ShowSoundPreview(row);

        // the data preview decompresses the asset off the UI thread
        _previewCts?.Cancel();
        var cts = _previewCts = new CancellationTokenSource();
        Hex.Text = "Decompressing…";
        HexInfo.Text = "";
        if (en.OriginalSize > 96 * 1024 * 1024)
        {
            Hex.Text = "Too large to preview here. Export it instead.";
            return;
        }
        try
        {
            byte[] data = await Task.Run(() => _index.Read(en), cts.Token);
            if (cts.IsCancellationRequested) return;
            Hex.Text = Visuals.Hex(data, 512);
            HexInfo.Text = $"{Math.Min(512, data.Length)} of {data.Length:N0} bytes" + (EbxFile.IsEbx(data) ? "  ·  RIFF EBX" : "");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
                Hex.Text = "Could not read: " + ex.Message;
        }
    }

    // ================================================================ texture preview

    private TextureService _textures;
    private string _previewing;

    /// <summary>The texture a row stands for: a TextureAsset EBX or a Texture RES (null for anything else).</summary>
    private string TextureNameOf(AssetRow row) =>
        row.Kind == AssetKind.Ebx && row.Type == "TextureAsset" && _index.GetRes(row.Name) != null ? row.Name
        : row.Kind == AssetKind.Res && row.Entry is ResEntry r && ResTypes.Name(r.ResType) == "Texture" ? row.Name
        : null;

    private async void ShowTexturePreview(AssetRow row)
    {
        string name = TextureNameOf(row);
        TexPreviewCard.Visibility = name == null ? Visibility.Collapsed : Visibility.Visible;
        _previewing = name;
        if (name == null) return;
        _textures ??= new TextureService(_index);
        TexPreview.Source = null;
        TexPreviewNote.Text = "Decoding…";
        TexPreviewTitle.Text = "TEXTURE";
        try
        {
            var t = await _textures.LoadAsync(name, -1);
            if (_previewing != name) return;
            if (t.Problem != null)
            {
                TexPreviewNote.Text = t.Problem;
                return;
            }
            // a big texture previews from a smaller mip
            int mip = t.MipForSize(512);
            if (mip != t.Mip) t.DecodeMip(mip);
            TexPreview.Source = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, Channel.Rgba);
            TexPreviewNote.Text = "";
            TexPreviewTitle.Text = $"TEXTURE  ·  {t.Header.Width}×{t.Header.Height} {t.Header.Format.Name.ToUpperInvariant()}";
        }
        catch (Exception ex)
        {
            if (_previewing == name) TexPreviewNote.Text = ex.Message;
        }
    }

    // ================================================================ sound preview

    private SoundService _sounds;
    private string _soundPreviewing;

    private async void ShowSoundPreview(AssetRow row)
    {
        bool isWave = row.Kind == AssetKind.Ebx && row.Type == "NewWaveAsset";
        SoundPreviewCard.Visibility = isWave ? Visibility.Visible : Visibility.Collapsed;
        ExplorerPlayer.Stop();
        _soundPreviewing = isWave ? row.Name : null;
        if (!isWave) return;
        _sounds ??= new SoundService(_index);
        SoundPreviewTitle.Text = "SOUND";
        ExplorerPlayer.Clear("Reading\u2026");
        try
        {
            var vars = await _sounds.VariationsAsync(row.Name);
            if (_soundPreviewing != row.Name) return;
            if (vars.Count == 0 || vars[0].Header == null)
            {
                ExplorerPlayer.Clear("No audio of its own. It may play through a sibling sound (like one ending in _licensed) or a sample bank.");
                return;
            }
            SoundPreviewTitle.Text = $"SOUND  \u00b7  {vars[0].Header.CodecName.ToUpperInvariant()}" + (vars.Count > 1 ? $"  \u00b7  {vars.Count} VARIATIONS" : "");
            ExplorerPlayer.Clear("Decoding\u2026");
            var sound = await _sounds.DecodeAsync(vars[0]);
            if (_soundPreviewing != row.Name) return;
            await ExplorerPlayer.SetSoundAsync(sound);
        }
        catch (Exception ex)
        {
            if (_soundPreviewing == row.Name) ExplorerPlayer.Clear(ex.Message);
        }
    }

    private void OnOpenInSounds(object sender, RoutedEventArgs e)
    {
        if (_soundPreviewing == null) return;
        string name = _soundPreviewing;
        NavSounds.IsChecked = true;
        SoundPage.Show(name);
    }

    private void OnOpenInTextures(object sender, RoutedEventArgs e)
    {
        if (_previewing == null) return;
        NavTextures.IsChecked = true;
        TexturePage.Show(_previewing);
    }

    private void ShowAssetInExplorer(string name)
    {
        NavExplorer.IsChecked = true;
        KindEbx.IsChecked = true;
        int slash = name.LastIndexOf('/');
        RevealFolder(slash < 0 ? "" : name[..slash]);
        Search.Text = slash < 0 ? name : name[(slash + 1)..];
    }

    private void OnAssetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Selected == null || Selected.Kind == AssetKind.Chunk) return;
        // reveal the asset's folder in the tree
        Search.Text = "";
        RevealFolder(Selected.Folder);
    }

    private void RevealFolder(string path)
    {
        var node = Current.Root;
        foreach (string part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = node.Children.FirstOrDefault(c => c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (next == null) break;
            node.IsExpanded = true;
            node = next;
        }
        node.IsExpanded = true;
        node.IsSelected = true;
    }

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        var row = Selected;
        if (row == null) return;
        ExportButton.IsEnabled = false;
        try
        {
            var files = await Task.Run(() =>
            {
                var written = new List<string> { Write(row.Entry, row.Kind) };
                if (row.Kind == AssetKind.Ebx && _index.GetRes(row.Name) is { } res)
                    written.Add(Write(res, AssetKind.Res));
                return written;
            });
            Status.Text = $"Exported {string.Join(", ", files.Select(Path.GetFileName))} to {Path.GetDirectoryName(files[0])}";
            SetState("Exported", "Good");
        }
        catch (Exception ex)
        {
            Status.Text = "Export failed: " + ex.Message;
            SetState("Export failed", "Danger");
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }

    private string Write(AssetEntry entry, AssetKind kind)
    {
        string name = entry is ChunkEntry c ? "chunks/" + c.Id : entry.Name;
        string ext = kind switch { AssetKind.Res => ".res", AssetKind.Chunk => ".chunk", _ => ".ebx" };
        string file = Path.Combine(KitPaths.Export, name.Replace('/', Path.DirectorySeparatorChar) + ext);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllBytes(file, _index.Read(entry));
        return file;
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        Clipboard.SetText(Selected.Name);
        Status.Text = "Copied " + Selected.Name;
    }

    private void OnOpenExports(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(KitPaths.Export);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{KitPaths.Export}\"") { UseShellExecute = true });
    }

    private void OnOpenKit(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{KitPaths.Root}\"") { UseShellExecute = true });

    private async void OnRebuild(object sender, RoutedEventArgs e)
    {
        NavExplorer.IsChecked = true;
        await LoadIndex(rebuild: true);
    }

    // ================================================================ settings and navigation

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (ExplorerPage == null) return;
        ShowPage(sender);
        // the rail scrolls when the window is short: keep the picked page's button in sight
        (sender as FrameworkElement)?.BringIntoView();
        // the page that's now showing glides in
        Motion.Enter(new FrameworkElement[] { ExplorerPage, TexturePage, SoundPage, VideoPage, ModsPage, RosterPage, UniformsPage, StudioPage, UnusedPage, ScorebugPage, Scorebug24Page,
            DatabasePage, TuningPage, BeAProPage, ArenasPage, TeamsPage, PlayPage, Play24Page, SettingsPage }.FirstOrDefault(p => p.Visibility == Visibility.Visible));
    }

    private void ShowPage(object sender)
    {
        ExplorerPage.Visibility = sender == NavExplorer ? Visibility.Visible : Visibility.Collapsed;
        TexturePage.Visibility = sender == NavTextures ? Visibility.Visible : Visibility.Collapsed;
        SoundPage.Visibility = sender == NavSounds ? Visibility.Visible : Visibility.Collapsed;
        VideoPage.Visibility = sender == NavVideos ? Visibility.Visible : Visibility.Collapsed;
        ModsPage.Visibility = sender == NavMods ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavMods) _ = ModsPage.Refresh();
        RosterPage.Visibility = sender == NavRoster ? Visibility.Visible : Visibility.Collapsed;
        UniformsPage.Visibility = sender == NavUniforms ? Visibility.Visible : Visibility.Collapsed;
        StudioPage.Visibility = sender == NavStudio ? Visibility.Visible : Visibility.Collapsed;
        UnusedPage.Visibility = sender == NavUnused ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavUnused) _ = UnusedPage.Shown();
        bool nhl24 = _profile?.Kind == GameKind.Nhl24;
        ScorebugPage.Visibility = sender == NavScorebug && !nhl24 ? Visibility.Visible : Visibility.Collapsed;
        Scorebug24Page.Visibility = sender == NavScorebug && nhl24 ? Visibility.Visible : Visibility.Collapsed;
        DatabasePage.Visibility = sender == NavDatabase ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavDatabase) _ = DatabasePage.Shown();
        TuningPage.Visibility = sender == NavTuning ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavTuning) _ = TuningPage.Shown();
        BeAProPage.Visibility = sender == NavBeAPro ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavBeAPro) _ = BeAProPage.Shown();
        ArenasPage.Visibility = sender == NavArenas ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavArenas) _ = ArenasPage.Shown();
        TeamsPage.Visibility = sender == NavTeams ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavTeams) _ = TeamsPage.Shown();
        if (sender == NavRoster) _ = RosterPage.Shown();
        if (sender == NavScorebug) _ = nhl24 ? Scorebug24Page.Shown() : ScorebugPage.Shown();
        // leaving a page stops what it was playing
        if (sender != NavSounds) SoundPage.StopPlayback();
        if (sender != NavVideos) VideoPage.StopPlayback();
        if (sender != NavExplorer) ExplorerPlayer.Stop();
        // NHL 24 plays on NHL24 PC, NHL 27 on KytyPS5
        bool ps4 = _profile?.Kind == GameKind.Nhl24;
        PlayPage.Visibility = sender == NavPlay && !ps4 ? Visibility.Visible : Visibility.Collapsed;
        Play24Page.Visibility = sender == NavPlay && ps4 ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = sender == NavSettings ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================================================================ games (NHL 27, NHL 24)

    /// <summary>A game folder for the switcher.</summary>
    public sealed record GameChoice(string Dir, GameProfile Profile)
    {
        public string Label => $"{Profile.Name}  ·  {Profile.Platform}  ·  {Dir}";
        public override string ToString() => Label;
    }

    /// <summary>The game folders the kit knows: the one open, ones picked before, and dumps found next to the kit.</summary>
    private List<GameChoice> KnownGames() =>
        new[] { Game }.Concat(_settings.Games).Concat(KitPaths.FindGames(_settings.Games.Prepend(Game)))
            .Where(KitPaths.IsGame).Select(d => Path.GetFullPath(d).TrimEnd('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(d => new GameChoice(d, GameProfile.Detect(d)))
            .OrderBy(g => g.Profile.Kind == GameKind.Nhl27 ? 0 : g.Profile.Kind == GameKind.Nhl24 ? 1 : 2).ToList();

    private void FillGamePicker()
    {
        var games = KnownGames();
        _suppress = true;
        GamePicker.ItemsSource = games;
        GamePicker.SelectedItem = games.FirstOrDefault(g => g.Dir.Equals(Game, StringComparison.OrdinalIgnoreCase));
        _suppress = false;
    }

    private void OnGamePicked(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || GamePicker.SelectedItem is not GameChoice g || g.Dir.Equals(Game, StringComparison.OrdinalIgnoreCase)) return;
        SwitchGame(g.Dir);
    }

    /// <summary>A game on the startup picker.</summary>
    public sealed record GameCard(string Dir, GameProfile Profile, ImageSource Icon)
    {
        public string Name => Profile.Name;
        public string Short => Profile.Kind switch { GameKind.Nhl27 => "27", GameKind.Nhl24 => "24", _ => "?" };
        public string Platform => Profile.Platform;
        public string Detail => string.IsNullOrEmpty(Profile.Version) ? Profile.TitleId ?? "" : $"{Profile.TitleId}  ·  v{Profile.Version}";
        public string Folder => Dir;
    }

    private TaskCompletionSource<string> _pick;

    /// <summary>Shows the game cards on the startup screen; the task ends with the folder picked.</summary>
    private Task<string> PickGameAsync()
    {
        GameCards.ItemsSource = KnownGames().Select(g => new GameCard(g.Dir, g.Profile, Cover(g.Dir))).ToList();
        SplashGame.Text = "";
        SplashLoading.Visibility = Visibility.Collapsed;
        GamePick.Visibility = Visibility.Visible;
        _pick = new TaskCompletionSource<string>();
        return _pick.Task;
    }

    /// <summary>The game's cover (sce_sys/icon0.png), or null.</summary>
    private static ImageSource Cover(string dir)
    {
        string file = Path.Combine(dir, "sce_sys", "icon0.png");
        if (!File.Exists(file)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 412;
            bmp.UriSource = new Uri(file);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException) { return null; }
    }

    private void OnGameCard(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GameCard card) _pick?.TrySetResult(card.Dir);
    }

    private void OnPickBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose the extracted game folder (NHL 27 or NHL 24)" };
        if (dlg.ShowDialog(this) != true) return;
        if (KitPaths.IsGame(dlg.FolderName)) _pick?.TrySetResult(dlg.FolderName);
        else MessageBox.Show(this, "That folder has no Data\\layout.toc. Pick the extracted game folder, the one with eboot.bin and Data.", "HMS",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Opens the game picked at startup and remembers it (and "don't ask again").</summary>
    private void OpenPicked(string dir)
    {
        GameDir.Text = dir;
        _settings.GameDir = Game;
        if (!_settings.Games.Contains(Game, StringComparer.OrdinalIgnoreCase)) _settings.Games.Add(Game);
        if (PickRemember.IsChecked == true) _settings.AskForGame = false;
        _settings.Save();
        AskGame.IsChecked = _settings.AskForGame;
        GamePick.Visibility = Visibility.Collapsed;
        SplashLoading.Visibility = Visibility.Visible;
    }

    private void OnAskGame(object sender, RoutedEventArgs e)
    {
        if (_settings.AskForGame == (AskGame.IsChecked == true)) return;
        _settings.AskForGame = AskGame.IsChecked == true;
        _settings.Save();
    }

    /// <summary>The title bar's game: a menu of the known games, and Browse.</summary>
    private void OnGameSwitch(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = GameSwitch, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var g in KnownGames())
        {
            var item = new MenuItem
            {
                Header = $"{g.Profile.Name}  ·  {g.Profile.Platform}",
                ToolTip = g.Dir,
                IsCheckable = true,
                IsChecked = g.Dir.Equals(Game, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) => { if (!g.Dir.Equals(Game, StringComparison.OrdinalIgnoreCase)) SwitchGame(g.Dir); };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var browse = new MenuItem { Header = "Another game folder…" };
        browse.Click += OnBrowseGame;
        menu.Items.Add(browse);
        menu.IsOpen = true;
    }

    /// <summary>Opens another game folder: its own index, mods and pages.</summary>
    private void SwitchGame(string dir)
    {
        if (PlayPage.IsRunning)
        {
            MessageBox.Show(this, "The game is running. Close it before switching games.", "HMS", MessageBoxButton.OK, MessageBoxImage.Information);
            FillGamePicker();
            return;
        }
        GameDir.Text = dir;
        OnGameDirChanged(null, null);
    }

    /// <summary>
    /// Shows the pages that work for the open game. NHL 24 (PS4) starts with the ones that read any Frostbite game
    /// (PLAN §28); the rest come as each is made to read and write NHL 24.
    /// </summary>
    private void ApplyProfile()
    {
        if (NavExplorer == null) return;
        bool ps4 = _profile.Kind == GameKind.Nhl24;
        var all = new[] { NavPlay, NavExplorer, NavTextures, NavSounds, NavVideos, NavRoster, NavDatabase, NavTuning, NavTeams, NavArenas,
            NavBeAPro, NavUniforms, NavStudio, NavUnused, NavScorebug, NavMods, NavSettings };
        var nhl24 = new[] { NavPlay, NavExplorer, NavTextures, NavSounds, NavVideos, NavRoster, NavDatabase, NavTuning, NavTeams, NavScorebug, NavMods, NavSettings };
        foreach (var nav in all)
            nav.Visibility = !ps4 || nhl24.Contains(nav) ? Visibility.Visible : Visibility.Collapsed;
        ComingSoonArenas.Visibility = ps4 ? Visibility.Collapsed : Visibility.Visible;
        // the page that's open may have gone
        if (all.FirstOrDefault(n => n.IsChecked == true) is { Visibility: Visibility.Collapsed })
            NavExplorer.IsChecked = true;
        // Play and Scorebug are for both games, but each game has its own page
        else if (NavPlay.IsChecked == true)
            ShowPage(NavPlay);
        else if (NavScorebug.IsChecked == true)
            ShowPage(NavScorebug);
        FillGamePicker();
    }

    private void OnBrowseGame(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose the extracted game folder (NHL 27 or NHL 24)", InitialDirectory = KitPaths.IsGame(Game) ? Game : null };
        if (dlg.ShowDialog(this) == true)
        {
            GameDir.Text = dlg.FolderName;
            OnGameDirChanged(null, null);
        }
    }

    private async void OnGameDirChanged(object sender, RoutedEventArgs e)
    {
        if (_settings.GameDir == Game) return;
        _settings.GameDir = Game;
        if (KitPaths.IsGame(Game) && !_settings.Games.Contains(Game, StringComparer.OrdinalIgnoreCase)) _settings.Games.Add(Game);
        _settings.Save();
        TuningPage.GameDir = Game;
        ShowGameInfo();
        PlayPage.Init(_settings, Game, _titleId, _title, _version);
        Play24Page.Init(_settings, Game, _titleId, _title, _version);
        BeAProPage.Init(_settings, _titleId);
        if (KitPaths.IsGame(Game))
            await LoadIndex(rebuild: false);
    }

    // ================================================================ window chrome

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!PlayPage.IsRunning)
            return;
        if (MessageBox.Show(this, "NHL 27 is still running. Close the kit and stop the game?", "HMS", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            e.Cancel = true;
        else
            PlayPage.Shutdown();
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        // a maximized chrome-less window overhangs the screen by its resize border
        Root.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(1);
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    // ================================================================ previews (--shot) and scripted state

    private async Task ApplyArgs()
    {
        var a = App.Args;
        if (a.TryGetValue("kind", out var kind))
            (kind switch { "res" => KindRes, "chunk" => KindChunk, _ => KindEbx }).IsChecked = true;
        if (a.TryGetValue("folder", out var folder))
            RevealFolder(folder);
        if (a.TryGetValue("type", out var type) && TypeFilter.ItemsSource is List<TypeChoice> types)
            TypeFilter.SelectedItem = types.FirstOrDefault(t => t.Type == type) ?? TypeFilter.SelectedItem;
        if (a.TryGetValue("search", out var search))
            Search.Text = search;
        if (a.ContainsKey("patched"))
            PatchedOnly.IsChecked = true;
        if (a.TryGetValue("ssearch", out var ssearch))
            SoundPage.SetSearch(ssearch);
        if (a.TryGetValue("sselect", out var ssel) && int.TryParse(ssel, out int sn))
        {
            await Task.Delay(800);
            SoundPage.SelectIndex(sn);
            await Task.Delay(3500);
            if (a.ContainsKey("splay"))
            {
                SoundPage.PlayNow();
                await Task.Delay(2500);
            }
            if (a.TryGetValue("sreplace", out var sfile))
            {
                await SoundPage.ReplaceWith(sfile);
                await Task.Delay(1500);
            }
        }
        if (a.TryGetValue("vsearch", out var vsearch))
            VideoPage.SetSearch(vsearch);
        if (a.TryGetValue("vcat", out var vcat))
            VideoPage.SetCategory(vcat);
        if (a.TryGetValue("vselect", out var vsel) && int.TryParse(vsel, out int vn))
        {
            for (int i = 0; i < 100 && VideoPage.ListCount.Text.Length == 0; i++) await Task.Delay(200);
            await Task.Delay(600);
            VideoPage.SelectIndex(vn);
            await Task.Delay(int.TryParse(a.GetValueOrDefault("vwait"), out int vw) ? vw : 5000);
            if (a.TryGetValue("vmp4", out var vmp4)) await VideoPage.SaveMp4(vmp4);
            if (a.ContainsKey("shot")) await VideoPage.SnapshotForShot();
        }
        if (a.TryGetValue("pexport", out var pexport))
        {
            await Task.Delay(2500);
            await ModsPage.ExportPack(pexport);
        }
        if (a.TryGetValue("pimport", out var pimport))
        {
            await Task.Delay(2500);
            await ModsPage.ImportPack(pimport);
            await Task.Delay(500);
        }
        if (a.TryGetValue("usearch", out var usearch))
        {
            await Task.Delay(2500);
            UniformsPage.SetSearch(usearch);
            await Task.Delay(400);
        }
        if (a.TryGetValue("uselect", out var usel) && int.TryParse(usel, out int un))
        {
            int ukit = a.TryGetValue("ukit", out var uk) && int.TryParse(uk, out int ukn) ? ukn : 0;
            if (a.ContainsKey("uname") || a.ContainsKey("unumber") || a.ContainsKey("ucaptain"))
                UniformsPage.SetLettering(a.GetValueOrDefault("uname"), a.GetValueOrDefault("unumber"), a.GetValueOrDefault("ucaptain"));
            await UniformsPage.SelectTeam(un, ukit);
            await Task.Delay(4000);
            if (a.TryGetValue("uexport", out var uefile) && a.TryGetValue("uslot", out var ueslot))
                await UniformsPage.ExportSlot(ueslot, uefile);
            if (a.TryGetValue("ureplace", out var ufile) && a.TryGetValue("uslot", out var uslot))
            {
                await UniformsPage.ReplaceSlot(uslot, ufile);
                await Task.Delay(1500);
            }
            if (a.TryGetValue("uturn", out var ut) && double.TryParse(ut, out double yaw)) { UniformsPage.Turn(yaw); await Task.Delay(500); }
            if (a.ContainsKey("unoidle")) { UniformsPage.Viewer.SetIdle(false); await Task.Delay(300); }
            if (a.ContainsKey("unolight")) { UniformsPage.Viewer.SetLighting(false); await Task.Delay(300); }
            if (a.TryGetValue("uwait", out var uw) && int.TryParse(uw, out int uwait)) await Task.Delay(uwait);
            if (a.ContainsKey("ugeartab")) { UniformsPage.ShowGearTab(); await Task.Delay(300); }
            if (a.TryGetValue("ugear", out var ugear))
            {
                foreach (var spec in ugear.Split(';', StringSplitOptions.RemoveEmptyEntries)) UniformsPage.SetGearZone(spec);
                await Task.Delay(2500);
            }
            if (a.ContainsKey("ugearsave")) { await UniformsPage.SaveGearAsync(); await Task.Delay(1500); }
            if (a.ContainsKey("uslots")) { UniformsPage.ShowSlotsTab(); await Task.Delay(1500); }
            if (a.TryGetValue("uslotadd", out var uadd)) { await UniformsPage.AddSlot(uadd); await Task.Delay(1500); }
            if (a.TryGetValue("uslotmove", out var umove))
            {
                foreach (var spec in umove.Split(';', StringSplitOptions.RemoveEmptyEntries)) UniformsPage.MoveSlot(spec);
                await Task.Delay(800);
            }
            if (a.TryGetValue("uslotname", out var uname)) { UniformsPage.RenameSlot(uname); await Task.Delay(500); }
            if (a.TryGetValue("uslotremove", out var urem) && int.TryParse(urem, out int uremn)) { UniformsPage.RemoveSlot(uremn); await Task.Delay(500); }
            if (a.ContainsKey("uslotsave")) { await UniformsPage.SaveSlotsAsync(); await Task.Delay(2500); }
            if (a.TryGetValue("ulegacy", out var ulegacy)) { await UniformsPage.ConvertLegacy(ulegacy); await Task.Delay(1500); }
            if (a.ContainsKey("ulegacywrite")) { await UniformsPage.WriteLegacy(!a.ContainsKey("ulegacykeeplogos")); await Task.Delay(2500); }
            if (a.TryGetValue("uzoom", out var uz) && double.TryParse(uz, System.Globalization.CultureInfo.InvariantCulture, out double zf))
            {
                double up = a.TryGetValue("uup", out var uu) && double.TryParse(uu, System.Globalization.CultureInfo.InvariantCulture, out double u) ? u : 0;
                UniformsPage.Zoom(zf, up);
                await Task.Delay(500);
            }
        }
        if (a.TryGetValue("page", out var sPage) && sPage == "studio")
        {
            await Task.Delay(2500);
            if (a.ContainsKey("uname") || a.ContainsKey("unumber") || a.ContainsKey("ucaptain"))
                StudioPage.SetLettering(a.GetValueOrDefault("uname"), a.GetValueOrDefault("unumber"), a.GetValueOrDefault("ucaptain"));
            if (a.TryGetValue("jselect", out var jsel) && int.TryParse(jsel, out int jsn))
            {
                if (a.TryGetValue("jsearch", out var jsearch)) { StudioPage.SetSearch(jsearch); await Task.Delay(400); }
                await StudioPage.SelectTeam(jsn, a.TryGetValue("jkit", out var sk) && int.TryParse(sk, out int skn) ? skn : 0);
            }
            else
            {
                int? N(string key) => int.TryParse(a.GetValueOrDefault(key), out int v) ? v : null;
                await StudioPage.SetOwn(a.GetValueOrDefault("jcolors"), N("jfont"), N("jpant"), N("jsock"), a.GetValueOrDefault("jletter"));
            }
            await Task.Delay(5000);
            if (a.TryGetValue("jtemplate", out var stpl)) { await StudioPage.StudioUseTemplate(stpl); await Task.Delay(1500); }
            if (a.TryGetValue("jimage", out var simg)) { await StudioPage.UseImage(simg, a.ContainsKey("jlegacy")); await Task.Delay(1500); }
            if (a.TryGetValue("jphoto", out var sphoto)) { await StudioPage.StudioPhoto(sphoto, a.ContainsKey("jphotoshow")); await Task.Delay(1500); }
            foreach (var spec in a.GetValueOrDefault("jcolor", "").Split(';', StringSplitOptions.RemoveEmptyEntries)) await StudioPage.StudioColor(spec);
            if (a.TryGetValue("jcollar", out var jcollar)) await StudioPage.StudioCollar(jcollar);
            foreach (var spec in a.GetValueOrDefault("jline", "").Split(';', StringSplitOptions.RemoveEmptyEntries)) await StudioPage.StudioLine(spec);
            foreach (var spec in a.GetValueOrDefault("jlogo", "").Split(';', StringSplitOptions.RemoveEmptyEntries)) await StudioPage.StudioAddLogo(spec);
            foreach (var spec in a.GetValueOrDefault("jspot", "").Split(';', StringSplitOptions.RemoveEmptyEntries)) await StudioPage.StudioSpot(spec);
            if (a.TryGetValue("jpick", out var jpick))
            {
                var pk = jpick.Split(',');
                var pci = System.Globalization.CultureInfo.InvariantCulture;
                await StudioPage.StudioPickLine(int.Parse(pk[0]), double.Parse(pk[1], pci), double.Parse(pk[2], pci));
            }
            if (a.TryGetValue("jstrip", out var jstrip)) { int at = jstrip.IndexOf(':'); await StudioPage.StudioExportStrip(int.Parse(jstrip[..at]), jstrip[(at + 1)..]); }
            if (a.TryGetValue("jlinespng", out var jlp)) { await Task.Delay(1500); await StudioPage.StudioExportLines(jlp); }
            if (a.TryGetValue("jslot", out var sslot)) await StudioPage.StudioSlotTarget(sslot);
            await Task.Delay(2500);
            if (a.TryGetValue("jtemplatefile", out var jtf)) await StudioPage.SaveTemplate(jtf);
            if (a.ContainsKey("unoidle")) { StudioPage.Viewer.SetIdle(false); await Task.Delay(300); }
            if (a.TryGetValue("jturn", out var st) && double.TryParse(st, out double syaw)) { StudioPage.Turn(syaw); await Task.Delay(500); }
            if (a.TryGetValue("jzoom", out var sz) && double.TryParse(sz, System.Globalization.CultureInfo.InvariantCulture, out double szf))
            {
                StudioPage.Zoom(szf, a.TryGetValue("jup", out var su) && double.TryParse(su, System.Globalization.CultureInfo.InvariantCulture, out double sup) ? sup : 0);
                await Task.Delay(500);
            }
            if (a.TryGetValue("jtab", out var jtab)) { StudioPage.ShowTab(jtab); await Task.Delay(400); }
            if (a.TryGetValue("jscroll", out var sscr) && double.TryParse(sscr, out double ssy)) { StudioPage.StudioScroll(ssy); await Task.Delay(400); }
            if (a.ContainsKey("jwrite")) { await StudioPage.WriteStudio(); await Task.Delay(3000); }
            if (a.ContainsKey("jaddteam")) { await StudioPage.AddToTeam(true); await Task.Delay(3000); }
        }
        if (a.TryGetValue("page", out var dbPage) && dbPage == "database")
        {
            await Task.Delay(3000);
            await DatabasePage.Show(a.GetValueOrDefault("dbname"), a.GetValueOrDefault("dbtable"), a.GetValueOrDefault("dbfind"));
            await Task.Delay(1500);
        }
        if (a.TryGetValue("page", out var tuPage) && tuPage == "tuning")
        {
            await TuningPage.Shown();
            TuningPage.Show(a.GetValueOrDefault("tusearch"), a.GetValueOrDefault("tugroup"), a.ContainsKey("turatings"));
            if (a.TryGetValue("tucsv", out var tucsv)) TuningPage.SaveCsv(tucsv);
            await Task.Delay(800);
        }
        if (a.TryGetValue("page", out var arPage) && arPage == "arenas")
        {
            await Task.Delay(2500);
            await ArenasPage.Shown();
            ArenasPage.SetLayers(!a.ContainsKey("anoglass"), !a.ContainsKey("anoice"), !a.ContainsKey("anoroof"), !a.ContainsKey("anoprops"), !a.ContainsKey("anojumbo"), !a.ContainsKey("agameads"));
            if (a.TryGetValue("arena", out var arena)) await ArenasPage.Show(arena);
            if (a.TryGetValue("aview", out var av) && Enum.TryParse<ArenaViewer.View>(av, true, out var view)) ArenasPage.Go(view);
            if (a.TryGetValue("alook", out var al))
            {
                var look = al.Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                ArenasPage.Look(look[0], look[1], look[2], look.Length > 3 ? look[3] : double.NaN);
            }
            await Task.Delay(1500);
        }
        if (a.TryGetValue("page", out var pPage) && pPage == "play" && _profile.Kind == GameKind.Nhl24)
        {
            await Task.Delay(800);
            if (a.TryGetValue("p24set", out var pset)) Play24Page.SetValues(pset);
            if (a.ContainsKey("p24save")) { Play24Page.Save(); await Task.Delay(800); }
            if (a.TryGetValue("p24scroll", out var psc) && double.TryParse(psc, out double psy)) { Play24Page.Scroller.ScrollToVerticalOffset(psy); await Task.Delay(400); }
        }
        if (a.TryGetValue("page", out var bPage) && bPage == "beapro")
        {
            if (a.TryGetValue("bapdir", out var bdir)) BeAProPage.SavesDirOverride = bdir;
            await Task.Delay(2500);
            await BeAProPage.Show(a.GetValueOrDefault("bapslot"));
            if (a.TryGetValue("bapset", out var bset)) BeAProPage.SetValues(bset);
            if (a.ContainsKey("bapadvanced")) BeAProPage.ShowAdvanced();
            if (a.ContainsKey("bapsave")) { await BeAProPage.SaveAsync(); await Task.Delay(1500); }
            await Task.Delay(800);
            if (a.TryGetValue("bapscroll", out var bsc) && double.TryParse(bsc, out double bsy)) { BeAProPage.ScrollTo(bsy); await Task.Delay(400); }
        }
        if (a.TryGetValue("page", out var tPage) && tPage == "teams")
        {
            await Task.Delay(3000);
            await TeamsPage.Show(a.GetValueOrDefault("tsearch"), a.GetValueOrDefault("tteam"));
            await Task.Delay(800);
            if (a.TryGetValue("tset", out var tset))
            {
                foreach (var spec in tset.Split(';', StringSplitOptions.RemoveEmptyEntries)) TeamsPage.SetValue(spec);
                await Task.Delay(500);
            }
            if (a.ContainsKey("tsave")) { await TeamsPage.SaveAsync(); await Task.Delay(1500); }
            if (a.TryGetValue("tlogoexport", out var tle)) await TeamsPage.ExportLogos(tle);
            if (a.TryGetValue("tfaceoff", out var tfo) && TeamsPage.OpenFaceoff() is { } fw)
            {
                await Task.Delay(1500);
                if (a.TryGetValue("tfosat", out var tfs) && double.TryParse(tfs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fsat))
                    fw.SetLook(fsat, 0.06);
                if (a.TryGetValue("tfosearch", out var tfq)) fw.Search.Text = tfq;
                if (tfo.StartsWith("http")) await fw.OpenRink(tfo); else await fw.PickRink(tfo);
                await Task.Delay(1500);
                if (a.ContainsKey("tfodark")) { fw.IceToggle.IsChecked = true; fw.SetLook(fw.SatSlider.Value, fw.CutSlider.Value + 0.0001); await Task.Delay(1200); }
                if (a.ContainsKey("tfowrite")) { await fw.Write(); await Task.Delay(1500); }
            }
            if (a.TryGetValue("tlogo", out var tlw) && a.TryGetValue("tlogofile", out var tlf)) { await TeamsPage.ReplaceLogos(tlw, tlf); await Task.Delay(1500); }
            await Task.Delay(1500);
            if (a.TryGetValue("tscroll", out var tsc) && double.TryParse(tsc, out double tsv)) { TeamsPage.ScrollTo(tsv); await Task.Delay(400); }
        }
        if (a.TryGetValue("page", out var uPage) && uPage == "unused")
        {
            await Task.Delay(4000);
            if (a.TryGetValue("nreason", out var ur)) { UnusedPage.SetReason(ur); await Task.Delay(300); }
            if (a.TryGetValue("nsearch", out var us)) { UnusedPage.SetSearch(us); await Task.Delay(600); }
            if (a.TryGetValue("nkit", out var uk)) { await UnusedPage.SelectKit(uk); await Task.Delay(6000); }
            if (a.TryGetValue("nturn", out var ut) && double.TryParse(ut, out double uyaw)) { UnusedPage.Turn(uyaw); await Task.Delay(500); }
            // thumbnails
            await Task.Delay(4000);
        }
        if (a.TryGetValue("page", out var sb24Page) && sb24Page == "scorebug" && _profile.Kind == GameKind.Nhl24)
        {
            // FFDec reads the movie's settings code first
            await Task.Delay(9000);
            if (a.TryGetValue("sb24group", out var sbg)) { Scorebug24Page.SelectGroup(sbg); await Task.Delay(400); }
            if (a.TryGetValue("sb24set", out var sbs)) { Scorebug24Page.SetValues(sbs); await Task.Delay(600); }
            if (a.ContainsKey("sb24save")) { await Scorebug24Page.SaveAsync(); await Task.Delay(1500); }
        }
        else if (a.TryGetValue("page", out var sbPage) && sbPage == "scorebug")
        {
            // the page decompiles the score clock with FFDec first
            await Task.Delay(12000);
            if (a.ContainsKey("sbsharp")) { ScorebugPage.UseSharpBadge(); await Task.Delay(800); }
            if (a.ContainsKey("sbsave")) { await ScorebugPage.SaveAsync(); await Task.Delay(1500); }
        }
        if (a.ContainsKey("rtext"))
        {
            RosterPage.SetTextMode();
            await Task.Delay(2500);
        }
        if (a.TryGetValue("rlines", out var rlines) && int.TryParse(rlines, out int lteam))
        {
            await Task.Delay(2500);
            await RosterPage.LinesAsync(lteam, a.GetValueOrDefault("rlinespick"), a.ContainsKey("rlinesauto"), a.ContainsKey("rlinessave"));
            await Task.Delay(1200);
        }
        if (a.TryGetValue("rsync", out var rsync))
        {
            await Task.Delay(2500);
            await RosterPage.SyncAsync(rsync);
            await Task.Delay(800);
            if (a.ContainsKey("rsyncapply")) { await RosterPage.SyncApplyAsync(); await Task.Delay(800); }
        }
        if (a.TryGetValue("rsearch", out var rsearch))
        {
            RosterPage.SetSearch(rsearch);
            await Task.Delay(600);
        }
        if (a.TryGetValue("rselect", out var rsel) && int.TryParse(rsel, out int rn))
        {
            await Task.Delay(2500);
            RosterPage.SelectIndex(rn);
            await Task.Delay(3000);
        }
        if (a.TryGetValue("tsearch", out var tsearch))
            TexturePage.SetSearch(tsearch);
        if (a.TryGetValue("tselect", out var tsel) && int.TryParse(tsel, out int tn))
        {
            await Task.Delay(800);
            TexturePage.SelectIndex(tn);
            await Task.Delay(2500);
            if (a.TryGetValue("tlayer", out var tl) && int.TryParse(tl, out int tln))
            {
                await TexturePage.SelectLayer(tln);
                await Task.Delay(1500);
            }
            if (a.TryGetValue("treplace", out var tfile))
            {
                await TexturePage.ReplaceWith(tfile);
                await Task.Delay(1500);
            }
        }
        await Task.Delay(600);
        Refresh();
        await Task.Delay(900);
        if (a.TryGetValue("select", out var sel) && int.TryParse(sel, out int n) && Assets.Items.Count > n)
        {
            Assets.SelectedIndex = n;
            await Task.Delay(900);
        }
    }

    private async Task Shot()
    {
        if (!App.Args.TryGetValue("shot", out var file) || string.IsNullOrEmpty(file))
            return;
        // --wait MS: for pages that keep loading after the scripted steps (the score clock's decompile)
        await Task.Delay(App.Args.TryGetValue("wait", out var w) && int.TryParse(w, out int ms) ? ms : 500);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        // a dialog open on top (TheFaceoff.net converter) is what's on screen
        FrameworkElement target = OwnedWindows.OfType<Window>().LastOrDefault(w => w.IsVisible)?.Content as FrameworkElement ?? Root;
        var bmp = new RenderTargetBitmap((int)(target.ActualWidth * scale), (int)(target.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(target);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
        using (var fs = File.Create(file))
            enc.Save(fs);
        Application.Current.Shutdown();
    }
}

/// <summary>One kind of asset (EBX, RES or chunks) arranged for the explorer: rows, folder tree, type counts.</summary>
public sealed class Catalog
{
    public FolderNode Root { get; } = new("All", "", 0);
    public List<TypeChoice> Types { get; private set; }
    public int Count { get; private set; }
    public int Patched { get; private set; }

    public static Catalog Build(AssetIndex index, AssetKind kind)
    {
        var cat = new Catalog();
        IEnumerable<AssetRow> rows = kind switch
        {
            AssetKind.Ebx => index.Ebx.Values.Select(e => new AssetRow(kind, e, e.Name, e.Type)),
            AssetKind.Res => index.Res.Values.Select(e => new AssetRow(kind, e, e.Name, ResTypes.Name(e.ResType))),
            _ => index.Chunks.Values.Select(e => new AssetRow(kind, e, e.Id.ToString(), e.InChunkTable ? "Chunk (TOC table)" : "Chunk (bundle)")),
        };
        var types = new Dictionary<string, int>();
        foreach (var r in rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            var node = cat.Root;
            if (kind != AssetKind.Chunk)
                foreach (string part in r.Folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    node = node.Child(part);
            node.Items.Add(r);
            types[r.TypeText] = types.GetValueOrDefault(r.TypeText) + 1;
            if (r.IsPatched) cat.Patched++;
        }
        cat.Count = cat.Root.Count();
        cat.Types = new List<TypeChoice> { new(null, cat.Count) };
        cat.Types.AddRange(types.OrderByDescending(t => t.Value).Select(t => new TypeChoice(t.Key, t.Value)));
        return cat;
    }
}
