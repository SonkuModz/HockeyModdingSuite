using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>One movie (MovieTexture2Asset) in the Videos list.</summary>
public sealed class VideoRow
{
    public VideoRow(Movie movie)
    {
        Movie = movie;
        Name = movie.Name;
        Leaf = movie.Short;
        Folder = movie.Folder;
        Category = CategoryOf(Name);
        (Glyph, Tint) = Look(Category);
        SizeText = movie.Size >= 1 << 20 ? $"{movie.Size / 1048576.0:0.0} MB" : $"{movie.Size / 1024.0:0} KB";
    }

    public Movie Movie { get; }
    public string Name { get; }
    public string Leaf { get; }
    public string Folder { get; }
    public string Category { get; }
    public string Glyph { get; }
    public Brush Tint { get; }
    public string SizeText { get; }

    public static string CategoryOf(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("trainingcamp") || n.Contains("training_camp")) return "Training";
        if (n.Contains("/arena_graphics/") || n.StartsWith("content/worlds/")) return "Arena";
        if (n.Contains("/transitions/") || n.Contains("/wipes/") || n.Contains("transitionwipe")) return "Transitions";
        if (n.Contains("/hypestage/") || n.Contains("/mainmenu/") || n.Contains("/selectsides/") || n.Contains("/bootflow/") ||
            n.Contains("/movie/ui/") || n.Contains("/cfm/") || n.StartsWith("ui/")) return "Menus";
        return "Modes";
    }

    private static Brush B(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
    private static readonly Brush Blue = B("#4C9DFF"), Pink = B("#F472B6"), Teal = B("#34D3C0"), Amber = B("#F5A524"), Violet = B("#A78BFA");

    public static (string Glyph, Brush Tint) Look(string category) => category switch
    {
        "Menus" => ("", Blue),
        "Arena" => ("", Amber),
        "Transitions" => ("", Pink),
        "Training" => ("", Teal),
        _ => ("", Violet),
    };
}

/// <summary>
/// Every movie in the game (menus, hype stages, arena scoreboards, ice projections, transitions, Training Camp), with a
/// player (WebView2 plays WebM itself; EA's older VP6 movies are converted with ffmpeg first), WebM/MP4 export, and
/// replacement: a clip is encoded to the movie's size, frame rate, frame count and bytes, and queued on the Mods page.
/// </summary>
public partial class VideoPage : UserControl
{
    private const string Host = "nhl27kit.video";
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "NHL27Kit", "video");
    private AssetIndex _index;
    private ModService _mods;
    private List<VideoRow> _all = new();
    private VideoRow _selected;
    private string _category;
    private byte[] _data;
    private string _kind;
    private string _playing, _replacementFile;
    private bool _showingReplacement;
    private Task _webReady;
    private CancellationTokenSource _loadCts, _encodeCts, _filterCts;

    public event Action<string> StatusChanged;
    public event Action<string> ShowInExplorer;
    public event Action GoToMods;

    public VideoPage()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); _ = Refresh(); };
        try { if (Directory.Exists(_temp)) Directory.Delete(_temp, true); } catch { /* a file may still be in use */ }
    }

    public void SetIndex(AssetIndex index)
    {
        _index = index;
        _ = LoadList();
    }

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _mods.Changed += () => Dispatcher.BeginInvoke(new Action(() => _ = UpdateBanner()));
    }

    private async Task LoadList()
    {
        Summary.Text = "Reading the movies…";
        var movies = await Task.Run(() => Movies.List(_index));
        _all = movies.Select(m => new VideoRow(m)).ToList();
        int Count(string c) => _all.Count(r => r.Category == c);
        Summary.Text = $"{_all.Count:N0} videos  ·  {Count("Menus")} menus  ·  {Count("Arena")} arena  ·  {Count("Transitions")} transitions  ·  {Count("Modes")} modes  ·  {Count("Training")} training  ·  " +
                       $"{_all.Sum(r => r.Movie.Size) / 1073741824.0:0.0} GB  ·  WebM (VP8) and EA VP6";
        await Refresh();
    }

    /// <summary>Stops the player (leaving the page).</summary>
    public void StopPlayback()
    {
        if (Web.CoreWebView2 != null) _ = Web.CoreWebView2.ExecuteScriptAsync("var v=document.querySelector('video'); if (v) v.pause();");
    }

    // ================================================================ list

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void OnCategoryChanged(object sender, RoutedEventArgs e)
    {
        string c = (string)((RadioButton)sender).Content;
        _category = c == "All" ? null : c;
        _ = Refresh();
    }

    public void SetSearch(string text)
    {
        Search.Text = text;
        _searchDelay.Stop();
        _ = Refresh();
    }

    public void SetCategory(string category)
    {
        _category = category;
        _ = Refresh();
    }

    private async Task Refresh()
    {
        if (_all.Count == 0) return;
        _filterCts?.Cancel();
        var cts = _filterCts = new CancellationTokenSource();
        string[] terms = Search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string category = _category;
        List<VideoRow> rows;
        try
        {
            rows = await Task.Run(() => _all.Where(r => (category == null || r.Category == category) &&
                                                         terms.All(t => r.Name.Contains(t, StringComparison.OrdinalIgnoreCase))).ToList(), cts.Token);
        }
        catch (OperationCanceledException) { return; }
        if (cts.IsCancellationRequested) return;
        List.ItemsSource = rows;
        ListTitle.Text = (category ?? "ALL").ToUpperInvariant() + " VIDEOS" + (terms.Length > 0 ? $"  ·  “{Search.Text.Trim()}”" : "");
        ListCount.Text = rows.Count == 1 ? "1 video" : $"{rows.Count:N0} videos";
        ListEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListEmptyTitle.Text = "No videos match";
        ListEmptyText.Text = "Try fewer words or another kind.";
    }

    public void SelectIndex(int n)
    {
        if (n >= 0 && n < List.Items.Count) { List.SelectedIndex = n; List.ScrollIntoView(List.SelectedItem); }
    }

    // ================================================================ selection and player

    private async void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not VideoRow row) return;
        _selected = row;
        _data = null;
        _kind = null;
        _replacementFile = null;
        _showingReplacement = false;
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        InspectorEmpty.Visibility = Visibility.Collapsed;
        Inspector.Visibility = Visibility.Visible;
        VideoName.Text = row.Leaf;
        VideoPath.Text = row.Name;
        Props.ItemsSource = null;
        SaveRawButton.IsEnabled = SaveMp4Button.IsEnabled = false;
        ReplaceInfo.Text = "";
        CompareButton.Visibility = Visibility.Collapsed;
        _ = UpdateBanner();
        ShowMessage("Reading the movie…");
        try
        {
            var m = row.Movie;
            var (data, info, frames, ebx) = await Task.Run(() =>
            {
                var d = Movies.Read(_index, m);
                var w = Movies.Probe(d);
                var f = w != null ? Movies.Frames(d) : null;
                return (d, w, f, _index.Read(m.Entry));
            }, cts.Token);
            if (cts.IsCancellationRequested) return;
            _data = data;
            _kind = Movies.Kind(data);
            SaveRawText.Text = _kind == "vp6" ? "VP6" : info?.DocType == "matroska" ? "MKV" : "WebM";
            SaveRawButton.IsEnabled = true;
            SaveMp4Button.IsEnabled = true;
            ShowProps(m, data, info, frames, ebx);
            await PlayAsync(data, _kind, info, row.Leaf, cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested) ShowMessage("Can't read this movie: " + ex.Message);
        }
    }

    private void ShowProps(Movie m, byte[] data, WebmInfo info, List<MovieFrame> frames, byte[] ebx)
    {
        var props = new List<Property> { new("Asset", m.Entry.Type) };
        if (info != null)
        {
            props.Add(new("Video", $"{info.Codec}  ·  {info.Width}×{info.Height}{(info.Alpha ? "  ·  alpha" : "")}"));
            if (frames is { Count: > 1 })
            {
                double fps = MovieReplacement.FrameRate(frames);
                props.Add(new("Frames", $"{frames.Count:N0} at {fps:0.###} fps  ·  {frames.Count(f => f.Key)} key frames"));
            }
            props.Add(new("Length", Fmt.Seconds(info.Seconds)));
            props.Add(new("Sound", info.Audio != null ? $"{info.Audio}  ·  {info.AudioChannels} ch  ·  {info.AudioRate / 1000:0.#} kHz" : "None"));
            int at = frames != null ? Movies.FrameTable(ebx, frames) : -1;
            props.Add(new("Frame table", at >= 0 ? $"at 0x{at:X} in the asset, matches the file" : "not found"));
        }
        else props.Add(new("Video", _kind == "vp6" ? "EA VP6 (MVhd), the older format" : "unknown format"));
        props.Add(new("Size", $"{data.Length:N0} bytes ({data.Length / 1048576.0:0.0} MB)"));
        if (m.Subtitles > 0) props.Add(new("Subtitles", $"{m.Subtitles} track{(m.Subtitles == 1 ? "" : "s")} (ST00)"));
        props.Add(new("Chunk", m.ChunkId.ToString(), true));
        props.Add(new("Stored in", $"{(m.Chunk.Ref.Patch ? "Patch" : "Data")}  ·  {m.Chunk.Toc}"));
        Props.ItemsSource = props;
        bool replaceable = info?.VideoCodec == "V_VP8" && HasTable(frames, ebx);
        ReplaceButton.IsEnabled = replaceable && _mods != null;
        ReplaceInfo.Text = replaceable
            ? $"A replacement is encoded to {info.Width}×{info.Height}, {frames.Count:N0} frames and {data.Length / 1048576.0:0.0} MB{(info.Audio != null ? ", with sound" : "")}. A shorter clip holds its last frame; a longer one is cut."
            : info == null ? "EA VP6 movies can be watched and exported; replacing them isn't supported yet." : $"Only VP8 movies can be replaced; this one is {info.Codec}.";
        static bool HasTable(List<MovieFrame> f, byte[] e) => f != null && Movies.FrameTable(e, f) >= 0;
    }

    private Task EnsureWeb() => _webReady ??= InitWeb();

    private async Task InitWeb()
    {
        Directory.CreateDirectory(_temp);
        var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(KitPaths.Cache, "webview2"),
            new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
        await Web.EnsureCoreWebView2Async(env);
        Web.CoreWebView2.SetVirtualHostNameToFolderMapping(Host, _temp, CoreWebView2HostResourceAccessKind.Allow);
        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
        File.WriteAllText(Path.Combine(_temp, "player.html"), """
            <!doctype html><html><head><meta charset="utf-8"><style>
            html,body{margin:0;height:100%;background:#07090d;overflow:hidden}
            video{width:100%;height:100%;object-fit:contain;display:block;background:#07090d}
            </style></head><body><video id="v" controls autoplay loop playsinline></video><script>
            const q=new URLSearchParams(location.search), v=document.getElementById('v');
            v.src=q.get('f'); v.play().catch(()=>{});
            </script></body></html>
            """);
    }

    /// <summary>Plays a movie's bytes: WebM as it is, VP6 converted to MP4 with ffmpeg.</summary>
    private async Task PlayAsync(byte[] data, string kind, WebmInfo info, string label, CancellationToken ct)
    {
        try { await EnsureWeb(); }
        catch (Exception ex)
        {
            ShowMessage("The player needs the WebView2 runtime (part of Microsoft Edge): " + ex.Message);
            return;
        }
        string file;
        if (kind == "webm" && info?.VideoCodec is "V_VP8" or "V_VP9")
        {
            file = $"{Guid.NewGuid():N}.webm";
            await File.WriteAllBytesAsync(Path.Combine(_temp, file), data, ct);
        }
        else
        {
            if (Ffmpeg.Find() == null)
            {
                ShowMessage("This movie is in EA's older VP6 format, which needs ffmpeg to play.\nPut ffmpeg.exe in a ThirdParty\\ffmpeg folder next to HMS.exe, or install it (winget install Gyan.FFmpeg).");
                return;
            }
            ShowMessage("Converting EA VP6 for the player…");
            string src = Path.Combine(_temp, $"{Guid.NewGuid():N}.{(kind == "vp6" ? "vp6" : "mkv")}");
            file = $"{Guid.NewGuid():N}.mp4";
            await File.WriteAllBytesAsync(src, data, ct);
            await Ffmpeg.RunAsync(new[] { "-y", "-v", "error", "-i", src, "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart", Path.Combine(_temp, file) }, ct);
            TryDelete(src);
        }
        if (ct.IsCancellationRequested) return;
        string old = _playing;
        _playing = file;
        Web.CoreWebView2.Navigate($"https://{Host}/player.html?f={Uri.EscapeDataString(file)}");
        ShowWeb();
        PlayerCaption.Text = label;
        if (old != null && old != _replacementFile) TryDelete(Path.Combine(_temp, old));
    }

    private void ShowMessage(string text)
    {
        PlayerMessageText.Text = text;
        PlayerMessage.Visibility = Visibility.Visible;
        Web.Visibility = Visibility.Hidden;
        Snapshot.Visibility = Visibility.Collapsed;
        PlayerCaption.Text = "";
    }

    private void ShowWeb()
    {
        PlayerMessage.Visibility = Visibility.Collapsed;
        Snapshot.Visibility = Visibility.Collapsed;
        Web.Visibility = Visibility.Visible;
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch { /* still open in the player */ }
    }

    /// <summary>
    /// For --shot: the WebView is its own window, which a WPF render can't see, so its picture is put in its place.
    /// </summary>
    public async Task SnapshotForShot()
    {
        if (Web.CoreWebView2 == null || Web.Visibility != Visibility.Visible) return;
        using var ms = new MemoryStream();
        await Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
        ms.Position = 0;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        Snapshot.Source = bmp;
        Snapshot.Visibility = Visibility.Visible;
        Web.Visibility = Visibility.Hidden;
    }

    // ================================================================ export

    private void OnSaveRaw(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _data == null) return;
        string ext = SaveRawText.Text.ToLowerInvariant();
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = _selected.Leaf + "." + ext, Filter = $"{SaveRawText.Text}|*.{ext}|All files|*.*", InitialDirectory = ExportDir() };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        File.WriteAllBytes(dlg.FileName, _data);
        StatusChanged?.Invoke($"Saved {Path.GetFileName(dlg.FileName)} ({_data.Length / 1048576.0:0.0} MB).");
    }

    private async void OnSaveMp4(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _data == null) return;
        if (Ffmpeg.Find() == null)
        {
            MessageBox.Show(Window.GetWindow(this), "Saving as MP4 needs ffmpeg. Put ffmpeg.exe in a ThirdParty\\ffmpeg folder next to HMS.exe, or install it (winget install Gyan.FFmpeg).", "HMS");
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = _selected.Leaf + ".mp4", Filter = "MP4|*.mp4", InitialDirectory = ExportDir() };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await SaveMp4(dlg.FileName);
    }

    public async Task SaveMp4(string file)
    {
        if (_data == null) return;
        SaveMp4Button.IsEnabled = false;
        StatusChanged?.Invoke($"Converting {_selected.Leaf} to MP4…");
        string src = Path.Combine(_temp, $"{Guid.NewGuid():N}.{(_kind == "vp6" ? "vp6" : "webm")}");
        try
        {
            Directory.CreateDirectory(_temp);
            await File.WriteAllBytesAsync(src, _data);
            await Ffmpeg.RunAsync(new[] { "-y", "-v", "error", "-i", src, "-c:v", "libx264", "-preset", "medium", "-crf", "16", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", file });
            StatusChanged?.Invoke($"Saved {Path.GetFileName(file)}.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't convert: " + ex.Message);
        }
        finally
        {
            TryDelete(src);
            SaveMp4Button.IsEnabled = true;
        }
    }

    private static string ExportDir()
    {
        string dir = Path.Combine(KitPaths.Export, "videos");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void OnShowInExplorer(object sender, RoutedEventArgs e)
    {
        if (_selected != null) ShowInExplorer?.Invoke(_selected.Name);
    }

    // ================================================================ replace

    public const string ClipFilter = "Videos and pictures|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.m4v;*.wmv;*.gif;*.png;*.jpg;*.jpeg;*.bmp|All files|*.*";

    private async void OnReplace(object sender, RoutedEventArgs e)
    {
        if (_encodeCts != null) { _encodeCts.Cancel(); return; }
        if (_selected == null || _mods == null) return;
        if (Ffmpeg.Find() == null)
        {
            MessageBox.Show(Window.GetWindow(this), "Replacing a video needs ffmpeg to encode it. Put ffmpeg.exe in a ThirdParty\\ffmpeg folder next to HMS.exe, or install it (winget install Gyan.FFmpeg).", "HMS");
            return;
        }
        var picker = new Microsoft.Win32.OpenFileDialog { Title = $"Replace {_selected.Leaf}", Filter = ClipFilter };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        await ReplaceWith(picker.FileName);
    }

    /// <summary>Encodes a clip for the selected movie and queues it on the Mods page (the dialog's choice, or a scripted one).</summary>
    public async Task ReplaceWith(string file)
    {
        if (_selected == null || _mods == null) return;
        var row = _selected;
        var existing = _mods.Find(ModService.MovieKind, row.Name);
        if (existing != null && (await _mods.StatesAsync()).GetValueOrDefault(existing) is ModState.Applied or ModState.Partial)
        {
            if (MessageBox.Show(Window.GetWindow(this), "This video's current replacement is in the game. Put the original back first and use the new clip instead?",
                                "HMS", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            try { await _mods.ResetAsync(ModService.MovieKind, row.Name); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "HMS", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        }
        StopPlayback();
        var cts = _encodeCts = new CancellationTokenSource();
        ReplaceText.Text = "Cancel";
        ReplaceGlyph.Text = "";
        var fit = (MovieFit)Math.Max(0, FitPicker.SelectedIndex);
        bool keepAudio = KeepAudio.IsChecked == true;
        try
        {
            var target = await _mods.MovieTargetAsync(row.Movie);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var webm = await MovieReplacement.EncodeAsync(target, file, fit, keepAudio,
                s => Dispatcher.BeginInvoke(new Action(() => { ReplaceInfo.Text = s; StatusChanged?.Invoke($"{row.Leaf}: {s}"); })), cts.Token);
            ReplaceInfo.Text = "Planning the write…";
            var plan = await _mods.PlanMovieAsync(row.Movie, webm);
            string summary = $"VP8 {target.Width}×{target.Height}, {target.Frames:N0} frames at {target.Fps:0.###} fps{(target.Audio ? " with sound" : "")}, {webm.Length / 1048576.0:0.0} MB from {Path.GetFileName(file)}";
            _mods.Queue(ModService.MovieKind, row.Name, plan.Source, summary, plan.Patches, plan.Notes);
            ReplaceInfo.Text = $"Encoded in {sw.Elapsed.TotalSeconds:0} s. The replacement is queued: apply it on the Mods page.";
            StatusChanged?.Invoke($"{row.Leaf}: replacement ready. Apply it on the Mods page.");
            if (_selected == row)
            {
                _replacementFile = $"{Guid.NewGuid():N}.webm";
                await File.WriteAllBytesAsync(Path.Combine(_temp, _replacementFile), webm);
                _showingReplacement = true;
                Play(_replacementFile, "Replacement  ·  " + Path.GetFileName(file));
            }
        }
        catch (OperationCanceledException)
        {
            ReplaceInfo.Text = "Cancelled.";
            StatusChanged?.Invoke("Video replacement cancelled.");
        }
        catch (Exception ex)
        {
            ReplaceInfo.Text = ex.Message;
            StatusChanged?.Invoke("Can't replace: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't replace this video", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _encodeCts = null;
            ReplaceText.Text = "Replace…";
            ReplaceGlyph.Text = "";
            await UpdateBanner();
        }
    }

    private void Play(string file, string label)
    {
        if (Web.CoreWebView2 == null) return;
        _playing = file;
        Web.CoreWebView2.Navigate($"https://{Host}/player.html?f={Uri.EscapeDataString(file)}");
        ShowWeb();
        PlayerCaption.Text = label;
    }

    private async void OnCompare(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _data == null || _replacementFile == null) return;
        _showingReplacement = !_showingReplacement;
        if (_showingReplacement) Play(_replacementFile, "Replacement");
        else await PlayAsync(_data, _kind, Movies.Probe(_data), _selected.Leaf, CancellationToken.None);
        await UpdateBanner();
    }

    private void OnGoMods(object sender, RoutedEventArgs e) => GoToMods?.Invoke();

    private async Task UpdateBanner()
    {
        CompareButton.Visibility = _replacementFile != null ? Visibility.Visible : Visibility.Collapsed;
        CompareButton.Content = _showingReplacement ? "Play original" : "Play replacement";
        if (_selected == null || _mods == null) { ModBanner.Visibility = Visibility.Collapsed; return; }
        var entry = _mods.Find(ModService.MovieKind, _selected.Name);
        if (entry == null) { ModBanner.Visibility = Visibility.Collapsed; return; }
        var state = (await _mods.StatesAsync()).GetValueOrDefault(entry, ModState.Missing);
        ModBanner.Visibility = Visibility.Visible;
        ModBannerText.Text = state switch
        {
            ModState.Applied => $"Replaced in the game: {entry.Summary}.",
            ModState.NotApplied => "Replacement ready: apply it on the Mods page.",
            ModState.Partial => "Replacement partly applied: finish or restore it on the Mods page.",
            _ => "The game files changed under this replacement: see the Mods page.",
        };
    }
}
