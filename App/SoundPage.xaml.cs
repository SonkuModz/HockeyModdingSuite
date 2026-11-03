using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>Every sound (NewWaveAsset) with a player: waveform, variations, properties and WAV export.</summary>
public partial class SoundPage : UserControl
{
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private SoundService _service;
    private List<SoundRow> _all = new();
    private SoundRow _selected;
    private List<SoundVariation> _variations = new();
    private Sound _sound;
    private string _category;
    private CancellationTokenSource _filterCts, _loadCts;

    public event Action<string> StatusChanged;
    public event Action<string> ShowInExplorer;
    public event Action GoToMods;

    private ModService _mods;
    private SoundReplacementPlan _plan;
    private bool _showingReplacement;

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _mods.Changed += () => Dispatcher.BeginInvoke(new Action(() => _ = UpdateBanner()));
    }

    private string ModKey(SoundVariation v) => $"{_selected?.Name}#{v.Number}";

    public SoundPage()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); _ = Refresh(); };
    }

    public void SetIndex(AssetIndex index)
    {
        _service = new SoundService(index);
        _all = index.Ebx.Values.Where(e => e.Type == "NewWaveAsset").Select(e => new SoundRow(e))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        int Count(string c) => _all.Count(r => r.Category == c);
        Summary.Text = $"{_all.Count:N0} sounds  ·  {Count("Speech"):N0} speech  ·  {Count("Music"):N0} music  ·  {Count("SFX"):N0} effects  ·  {Count("Crowds"):N0} crowd  ·  ATRAC9 and Opus";
        _ = Refresh();
    }

    public void StopPlayback() => Player.Stop();

    /// <summary>Starts the selected sound (for scripted checks).</summary>
    public void PlayNow()
    {
        if (!Player.IsPlaying) Player.Toggle();
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

    private async Task Refresh()
    {
        if (_service == null) return;
        _filterCts?.Cancel();
        var cts = _filterCts = new CancellationTokenSource();
        string[] terms = Search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string category = _category;
        List<SoundRow> rows;
        try
        {
            rows = await Task.Run(() => _all.Where(r =>
                    (category == null || r.Category == category) &&
                    terms.All(t => r.Name.Contains(t, StringComparison.OrdinalIgnoreCase))).ToList(), cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cts.IsCancellationRequested) return;
        List.ItemsSource = rows;
        ListTitle.Text = (category ?? "ALL").ToUpperInvariant() + " SOUNDS" + (terms.Length > 0 ? $"  ·  “{Search.Text.Trim()}”" : "");
        ListCount.Text = rows.Count == 1 ? "1 sound" : $"{rows.Count:N0} sounds";
        ListEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0)
        {
            ListEmptyGlyph.Text = "";
            ListEmptyTitle.Text = "No sounds match";
            ListEmptyText.Text = "Try fewer words or another kind.";
        }
    }

    public void SelectIndex(int n)
    {
        if (n >= 0 && n < List.Items.Count) List.SelectedIndex = n;
    }

    /// <summary>Shows a sound by name (from the Explorer).</summary>
    public async void Show(string name)
    {
        Search.Text = "";
        _category = null;
        CatAll.IsChecked = true;
        await Refresh();
        var list = List.ItemsSource as List<SoundRow>;
        int i = list?.FindIndex(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? -1;
        if (i >= 0)
        {
            List.SelectedIndex = i;
            List.ScrollIntoView(list[i]);
        }
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Player.IsPlaying || _sound == null) return;
        Player.Toggle();
    }

    // ================================================================ inspector

    private async void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        var row = List.SelectedItem as SoundRow;
        Player.Stop();
        _selected = row;
        Inspector.Visibility = row == null ? Visibility.Collapsed : Visibility.Visible;
        InspectorEmpty.Visibility = row == null ? Visibility.Visible : Visibility.Collapsed;
        _loadCts?.Cancel();
        if (row == null) return;
        var cts = _loadCts = new CancellationTokenSource();

        CatGlyph.Text = row.Glyph;
        CatGlyph.Foreground = row.Tint;
        CatText.Text = row.Category;
        CatSub.Text = row.IsPatched ? "Sound (NewWaveAsset)  ·  from the 01.003 patch" : "Sound (NewWaveAsset)";
        SoundName.Text = Breakable(row.Leaf);
        SoundPath.Text = Breakable(row.Folder);
        Variations.ItemsSource = null;
        _variations = new();
        _sound = null;
        WavButton.IsEnabled = StereoButton.IsEnabled = false;
        Props.ItemsSource = null;
        Player.Clear("Reading…");

        try
        {
            var vars = await _service.VariationsAsync(row.Name, cts.Token);
            if (cts.IsCancellationRequested) return;
            _variations = vars;
            VariationsPanel.Visibility = vars.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            VariationsTitle.Text = $"VARIATIONS  ·  {vars.Count}";
            if (vars.Count == 0)
            {
                Player.Clear("This sound has no audio of its own. Its audio may be in a sibling sound (for example one ending in _licensed) or in a sample bank.");
                ShowProps(null, null);
                return;
            }
            Variations.ItemsSource = vars;
            Variations.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested) Player.Clear(ex.Message);
        }
    }

    private async void OnVariation(object sender, SelectionChangedEventArgs e)
    {
        if (Variations.SelectedItem is not SoundVariation v || _selected == null) return;
        Player.Stop();
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        _sound = null;
        _plan = null;
        _showingReplacement = false;
        WavButton.IsEnabled = StereoButton.IsEnabled = false;
        ShowProps(v, null);
        _ = UpdateBanner();
        if (v.Header == null)
        {
            Player.Clear("This chunk is not an audio stream.");
            return;
        }
        Player.Clear("Decoding…");
        try
        {
            var sound = await _service.DecodeAsync(v, cts.Token);
            if (cts.IsCancellationRequested) return;
            _sound = sound;
            await Player.SetSoundAsync(sound);
            WavButton.IsEnabled = StereoButton.IsEnabled = true;
            ShowProps(v, sound);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested) Player.Clear(ex.Message);
        }
    }

    private void ShowProps(SoundVariation v, Sound sound)
    {
        var props = new List<Property>();
        if (v?.Header is { } h)
        {
            props.Add(new("Codec", $"{h.CodecName}  (0x{h.CodecByte:X2})"));
            props.Add(new("Channels", h.Channels switch { 1 => "1 (mono)", 2 => "2 (stereo)", _ => $"{h.Channels}  ({h.Channels / 2} stereo streams)" }));
            props.Add(new("Sample rate", $"{h.SampleRate:N0} Hz"));
            props.Add(new("Length", $"{Fmt.Seconds(h.Seconds)}  ({h.Samples:N0} samples)"));
            props.Add(new("Size", $"{Visuals.Size(v.Size)}"));
            props.Add(new("Chunk", v.Id.ToString(), true));
            props.Add(new("In chunk", $"at {v.Stream.Offset:N0}, {v.Stream.Length:N0} bytes"));
        }
        if (_selected != null)
        {
            props.Add(new("Variations", _variations.Count.ToString()));
            props.Add(new("Partition", _selected.Entry.Id.ToString(), true));
        }
        Props.ItemsSource = props;
    }

    // ================================================================ replacing

    private async Task UpdateBanner()
    {
        var v = Variations.SelectedItem as SoundVariation;
        if (_selected == null || _mods == null || v == null) { ModBanner.Visibility = Visibility.Collapsed; return; }
        ReplaceButton.IsEnabled = v.Header != null;
        var entry = _mods.Find("sound", ModKey(v));
        CompareButton.Visibility = _plan != null ? Visibility.Visible : Visibility.Collapsed;
        CompareButton.Content = _showingReplacement ? "Play original" : "Play replacement";
        if (entry == null) { ModBanner.Visibility = Visibility.Collapsed; return; }
        var state = (await _mods.StatesAsync()).GetValueOrDefault(entry, ModState.Missing);
        ModBanner.Visibility = Visibility.Visible;
        ModBannerText.Text = state switch
        {
            ModState.Applied => $"Variation {v.Number} is replaced in the game with {System.IO.Path.GetFileName(entry.Source)}.",
            ModState.NotApplied => $"Replacement ready for variation {v.Number}: apply it on the Mods page.",
            ModState.Partial => "Replacement partly applied: finish or restore it on the Mods page.",
            _ => "The game files changed under this replacement: see the Mods page.",
        };
    }

    private async void OnReplace(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _mods == null || Variations.SelectedItem is not SoundVariation v || v.Header == null) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Title = $"Replace {_selected.Leaf} (variation {v.Number})", Filter = AudioLoader.Filter };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        await ReplaceWith(picker.FileName);
    }

    /// <summary>Replaces the selected variation with an audio file (the dialog's choice, or a scripted one).</summary>
    public async Task ReplaceWith(string file)
    {
        if (_selected == null || _mods == null || Variations.SelectedItem is not SoundVariation v || v.Header == null) return;
        string name = _selected.Name, key = ModKey(v);
        var dlg = new { FileName = file };
        var existing = _mods.Find("sound", key);
        if (existing != null && (await _mods.StatesAsync()).GetValueOrDefault(existing) is ModState.Applied or ModState.Partial)
        {
            if (MessageBox.Show(Window.GetWindow(this), "This variation's current replacement is in the game. Put the original back first and use the new file instead?",
                                "NHL27Kit", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            try { await _mods.ResetAsync("sound", key); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "NHL27Kit", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        }
        Player.Stop();
        ReplaceButton.IsEnabled = false;
        StatusChanged?.Invoke($"Encoding {System.IO.Path.GetFileName(dlg.FileName)} for {_selected.Leaf}…");
        try
        {
            var plan = await _mods.PlanSoundAsync(name, v.Stream, dlg.FileName);
            var h = plan.Original;
            _mods.Queue("sound", key, dlg.FileName, $"{h.CodecName} {h.Channels} ch → {plan.Codec} {plan.BitratePerChannel / 1000} kbps/ch, {Fmt.Seconds(plan.Result.Seconds)} in {plan.Bytes:N0} of {plan.Capacity:N0} bytes",
                        plan.Chunk.Patches, plan.Warnings);
            _plan = plan;
            _showingReplacement = true;
            await Player.SetSoundAsync(plan.Result, "Replacement · " + System.IO.Path.GetFileName(dlg.FileName));
            StatusChanged?.Invoke($"{_selected.Leaf}: replacement ready. Apply it on the Mods page.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't replace: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't replace this sound", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            await UpdateBanner();
        }
    }

    private async void OnCompare(object sender, RoutedEventArgs e)
    {
        if (_plan == null) return;
        _showingReplacement = !_showingReplacement;
        if (_showingReplacement) await Player.SetSoundAsync(_plan.Result, "Replacement");
        else if (_sound != null) await Player.SetSoundAsync(_sound);
        await UpdateBanner();
    }

    private void OnGoMods(object sender, RoutedEventArgs e) => GoToMods?.Invoke();

    private static string Breakable(string s) => s.Replace("_", "_​").Replace("/", "/​");

    // ================================================================ export

    private async void OnExportWav(object sender, RoutedEventArgs e) => await Export(stereo: false);

    private async void OnExportStereo(object sender, RoutedEventArgs e) => await Export(stereo: true);

    private async Task Export(bool stereo)
    {
        if (_sound == null || _selected == null || Variations.SelectedItem is not SoundVariation v) return;
        var sound = _sound;
        string name = _selected.Name;
        try
        {
            string file = Path.Combine(KitPaths.Export, "sounds", name.Replace('/', Path.DirectorySeparatorChar))
                          + (_variations.Count > 1 ? $"_{v.Number}" : "") + (stereo && sound.Channels > 2 ? "_stereo" : "") + ".wav";
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            await Task.Run(() => File.WriteAllBytes(file, sound.ToWav(stereo)));
            StatusChanged?.Invoke($"Exported {Path.GetFileName(file)} to {Path.GetDirectoryName(file)}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Export failed: " + ex.Message);
        }
    }

    private void OnShowInExplorer(object sender, RoutedEventArgs e)
    {
        if (_selected != null) ShowInExplorer?.Invoke(_selected.Name);
    }
}
