using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// Every TextureAsset as a thumbnail gallery, with an inspector that decodes the texture (PS5 de-swizzle, BC1-BC7) to
/// show it full size, by channel and mip, and export it as PNG or DDS.
/// </summary>
public partial class TexturePage : UserControl
{
    private const int TileWidth = 186;
    private const int Cap = 6000;

    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private TextureService _service;
    private List<string> _all = new();
    private List<TexTile> _shown = new();
    private TexTile _selected;
    private Texture _texture;
    private Channel _channel = Channel.Rgba;
    private string _role;
    private int _perRow;
    private CancellationTokenSource _filterCts, _loadCts;
    private bool _settingMips;
    private int _layer;

    public event Action<string> StatusChanged;
    /// <summary>Asks the window to show this asset in the Explorer.</summary>
    public event Action<string> ShowInExplorer;
    public event Action GoToMods;

    private ModService _mods;
    private TextureReplacementPlan _plan;
    private bool _showingReplacement;

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _mods.Changed += () => Dispatcher.BeginInvoke(new Action(() =>
        {
            _service?.ClearThumbs();
            _ = UpdateBanner();
        }));
    }

    public TexturePage()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); Refresh(); };
    }

    public void SetIndex(AssetIndex index)
    {
        _service = new TextureService(index);
        _all = index.Ebx.Values.Where(e => e.Type is "TextureAsset" or "TextureArrayAsset" && index.GetRes(e.Name) != null)
            .Select(e => e.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Summary.Text = $"{_all.Count:N0} textures  ·  decoded on the fly ({(index.Layout.IsPs5 ? "PS5" : "PS4")} tiling, BC1 / BC3 / BC4 / BC5 / BC7)";
        Refresh();
    }

    // ================================================================ gallery

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void OnRoleChanged(object sender, RoutedEventArgs e)
    {
        _role = sender == RoleAll ? null : (string)((RadioButton)sender).Content;
        Refresh();
    }

    public void SetSearch(string text)
    {
        Search.Text = text;
        _searchDelay.Stop();
        Refresh();
    }

    private async void Refresh()
    {
        if (_service == null) return;
        _filterCts?.Cancel();
        var cts = _filterCts = new CancellationTokenSource();
        string[] terms = Search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string role = _role;
        List<string> matches;
        int total;
        try
        {
            (matches, total) = await Task.Run(() =>
            {
                var list = new List<string>();
                int n = 0;
                foreach (string name in _all)
                {
                    if (cts.IsCancellationRequested) break;
                    if (terms.Length > 0 && !terms.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                    if (role != null && TextureService.Role(name) != role) continue;
                    if (list.Count < Cap) list.Add(name);
                    n++;
                }
                return (list, n);
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cts.IsCancellationRequested) return;
        _shown = matches.Select(n => new TexTile(n, _service)).ToList();
        GalleryTitle.Text = (role == null ? "ALL" : role.ToUpperInvariant()) + " TEXTURES" + (terms.Length > 0 ? $"  ·  “{Search.Text.Trim()}”" : "");
        GalleryCount.Text = total > _shown.Count ? $"{_shown.Count:N0} of {total:N0} shown" : $"{total:N0} textures";
        bool empty = total == 0;
        GalleryEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            GalleryEmptyGlyph.Text = "";
            GalleryEmptyTitle.Text = "No textures match";
            GalleryEmptyText.Text = "Try fewer words or another role.";
        }
        Layout(force: true);
    }

    private void OnGallerySized(object sender, SizeChangedEventArgs e) => Layout(force: false);

    /// <summary>Packs the tiles into rows that fill the gallery's width (rows virtualize; a wrap panel would not).</summary>
    private void Layout(bool force)
    {
        int perRow = Math.Max(1, (int)((Gallery.ActualWidth - 30) / TileWidth));
        if (!force && perRow == _perRow) return;
        _perRow = perRow;
        var rows = new List<TexRow>();
        for (int i = 0; i < _shown.Count; i += perRow)
            rows.Add(new TexRow { Tiles = _shown.GetRange(i, Math.Min(perRow, _shown.Count - i)) });
        Gallery.ItemsSource = rows;
    }

    private void OnTileClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TexTile tile)
            Select(tile);
    }

    public void SelectIndex(int n)
    {
        if (n >= 0 && n < _shown.Count) Select(_shown[n]);
    }

    /// <summary>Shows a texture by name (from the Explorer), searching for it if the gallery doesn't list it.</summary>
    public void Show(string name)
    {
        var tile = _shown.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (tile == null && _service != null)
        {
            tile = new TexTile(name, _service);
            _shown.Insert(0, tile);
            Layout(force: true);
        }
        if (tile != null) Select(tile);
    }

    // ================================================================ inspector

    private async void Select(TexTile tile)
    {
        if (_selected != null) _selected.IsSelected = false;
        _selected = tile;
        tile.IsSelected = true;
        Inspector.Visibility = Visibility.Visible;
        InspectorEmpty.Visibility = Visibility.Collapsed;
        TexName.Text = Breakable(tile.Leaf);
        TexPath.Text = Breakable(tile.Folder);
        Pills.Children.Clear();
        Props.ItemsSource = null;
        MipPicker.ItemsSource = null;
        LayerPicker.ItemsSource = null;
        LayerPicker.Visibility = Visibility.Collapsed;
        _layer = 0;
        _plan = null;
        _showingReplacement = false;
        await LoadTexture(tile.Name, -1);
        await UpdateBanner();
    }

    private async Task LoadTexture(string name, int mip)
    {
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        Big.Source = null;
        BigBusy.Visibility = Visibility.Visible;
        BigProblem.Visibility = Visibility.Collapsed;
        PngButton.IsEnabled = DdsButton.IsEnabled = false;
        Texture t;
        try
        {
            t = await _service.LoadAsync(name, mip, cts.Token, _layer);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested) return;
            BigBusy.Visibility = Visibility.Collapsed;
            BigProblem.Text = ex.Message;
            BigProblem.Visibility = Visibility.Visible;
            return;
        }
        if (cts.IsCancellationRequested) return;
        _texture = t;
        BigBusy.Visibility = Visibility.Collapsed;
        if (t.Problem != null)
        {
            BigProblem.Text = t.Problem;
            BigProblem.Visibility = Visibility.Visible;
        }
        else
        {
            Big.Source = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, _channel);
            PngButton.IsEnabled = DdsButton.IsEnabled = true;
        }
        ShowInfo(t);
    }

    private void ShowInfo(Texture t)
    {
        var h = t.Header;
        Pills.Children.Clear();
        AddPill(h.Format.Name, "Accent");
        AddPill($"{h.Width} × {h.Height}", "TextDim");
        AddPill($"{t.MipCount} mips", "TextDim");
        if (t.Chunk.Ref.Patch) AddPill("PATCH", "Patch");
        if (h.Layers > 1) AddPill($"array · {h.Layers} layers", "Violet");

        _settingMips = true;
        if (h.Layers > 1)
        {
            var layers = Enumerable.Range(0, h.Layers).Select(l => new Choice($"Layer {l}", l.ToString())).ToList();
            LayerPicker.ItemsSource = layers;
            LayerPicker.SelectedItem = layers[Math.Clamp(_layer, 0, layers.Count - 1)];
            LayerPicker.Visibility = Visibility.Visible;
        }
        var mips = Enumerable.Range(0, t.MipCount).Where(m => t.MipStart(m) >= 0)
            .Select(m => { var (w, hh) = h.MipSize(m); return new Choice($"Mip {m}  ·  {w}×{hh}", m.ToString()); }).ToList();
        MipPicker.ItemsSource = mips;
        MipPicker.SelectedItem = mips.FirstOrDefault(c => c.Value == t.Mip.ToString());
        _settingMips = false;

        int resident = t.FirstResidentMip;
        Props.ItemsSource = new List<Property>
        {
            new("Format", $"{h.Format.Name}  (0x{h.PixelFormatId:X2})"),
            new("Size", $"{h.Width} × {h.Height}" + (h.Depth > 1 ? $" × {h.Depth}" : "") + (h.SliceCount > 1 ? $", {h.SliceCount} slices" : "")),
            new("Mips", resident > 0 ? $"{t.MipCount}, from mip {resident} (larger mips are not in this chunk)" : $"{t.MipCount}, all in the chunk"),
            new("Showing", t.Rgba == null ? "—" : $"mip {t.Mip}, {t.Width} × {t.Height}"),
            new("Type", h.Type.ToString()),
            new("Flags", h.Flags.ToString()),
            new("Chunk", $"{h.ChunkId}", true),
            new("Chunk data", $"{Visuals.Size(t.Data.Length)} ({(t.Chunk.InChunkTable ? "TOC chunk table" : "bundle")}{(t.Chunk.Ref.Patch ? ", patch" : "")})"),
            new("Name hash", $"0x{h.NameHash:X8}", true),
        };
    }

    // ================================================================ replacing

    private async Task UpdateBanner()
    {
        if (_selected == null || _mods == null) { ModBanner.Visibility = Visibility.Collapsed; return; }
        ReplaceButton.IsEnabled = _texture == null || TextureBuilder.Unsupported(_texture.Header) == null;
        ReplaceButton.ToolTip = _texture != null && TextureBuilder.Unsupported(_texture.Header) is { } why ? why : "Replace this texture with an image (any size: it is scaled to fit)";
        var entry = _mods.Find("texture", _selected.Name);
        CompareButton.Visibility = _plan != null && _plan.Name == _selected.Name ? Visibility.Visible : Visibility.Collapsed;
        CompareButton.Content = _showingReplacement ? "Show original" : "Show replacement";
        if (entry == null) { ModBanner.Visibility = Visibility.Collapsed; return; }
        var state = (await _mods.StatesAsync()).GetValueOrDefault(entry, ModState.Missing);
        ModBanner.Visibility = Visibility.Visible;
        ModBannerText.Text = state switch
        {
            ModState.Applied => $"Replaced in the game with {System.IO.Path.GetFileName(entry.Source)}.",
            ModState.NotApplied => $"Replacement ready ({System.IO.Path.GetFileName(entry.Source)}): apply it on the Mods page.",
            ModState.Partial => "Replacement partly applied: finish or restore it on the Mods page.",
            _ => "The game files changed under this replacement: see the Mods page.",
        };
    }

    private async void OnReplace(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _mods == null) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Replace " + _selected.Leaf, Filter = ImageLoader.Filter };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        await ReplaceWith(picker.FileName);
    }

    /// <summary>Replaces the selected texture with an image file (the dialog's choice, or a scripted one).</summary>
    public async Task ReplaceWith(string file)
    {
        if (_selected == null || _mods == null) return;
        string name = _selected.Name;
        var dlg = new { FileName = file };
        if (_texture?.Header.Layers > 1)
        {
            await ReplaceLayer(name, file, _layer);
            return;
        }
        var existing = _mods.Find("texture", name);
        if (existing != null)
        {
            var state = (await _mods.StatesAsync()).GetValueOrDefault(existing, ModState.Missing);
            if (state is ModState.Applied or ModState.Partial)
            {
                if (MessageBox.Show(Window.GetWindow(this), "This texture's current replacement is in the game. Put the original back first and use the new image instead?",
                                    "HMS", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                try { await _mods.ResetAsync("texture", name); }
                catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "HMS", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            }
        }
        ReplaceButton.IsEnabled = false;
        BigBusy.Visibility = Visibility.Visible;
        StatusChanged?.Invoke($"Encoding {System.IO.Path.GetFileName(dlg.FileName)} for {_selected.Leaf} (the first time also checks which game data is shared)…");
        var notes = new List<string>();
        try
        {
            TextureReplacementPlan plan;
            try { plan = await _mods.PlanTextureAsync(name, dlg.FileName, n => notes.Add(n)); }
            catch (InvalidDataException ex) when (_mods.Index.Layout.IsPs5 && ex.Message.Contains("also used by"))
            {
                if (!KitWriter.AskToTakeShared(Window.GetWindow(this), ex.Message))
                {
                    StatusChanged?.Invoke("Not replaced: other textures use the same stored image.");
                    return;
                }
                notes.Clear();
                plan = await _mods.PlanTextureAsync(name, dlg.FileName, n => notes.Add(n), takeShared: true);
            }
            var h = plan.Header;
            notes.AddRange(plan.Chunk.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
            _mods.Queue("texture", name, dlg.FileName, $"{h.Format.Name} {h.Width}×{h.Height}, {TextureBuilder.StoredMips(h)} stored mips", plan.Chunk.Patches, notes);
            _plan = plan;
            _showingReplacement = true;
            if (_selected?.Name == name)
                Big.Source = TextureService.ToBitmap(plan.Result.Rgba, plan.Result.Width, plan.Result.Height, _channel);
            StatusChanged?.Invoke($"{_selected?.Leaf}: replacement ready. Apply it on the Mods page.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't replace: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't replace this texture", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            BigBusy.Visibility = Visibility.Collapsed;
            await UpdateBanner();
        }
    }

    /// <summary>Writes one layer of an array into the game (its other layers, and earlier replaced ones, are kept).</summary>
    private async Task ReplaceLayer(string name, string file, int layer)
    {
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        ReplaceButton.IsEnabled = false;
        BigBusy.Visibility = Visibility.Visible;
        StatusChanged?.Invoke($"Encoding {System.IO.Path.GetFileName(file)} for layer {layer} of {_selected.Leaf}…");
        try
        {
            await _mods.ReplaceArrayLayerAsync(name, file, layer, null);
            _service.ClearThumbs();
            _selected?.Reload();
            StatusChanged?.Invoke($"{_selected?.Leaf}: layer {layer} replaced in the game");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't replace: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't replace this layer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            BigBusy.Visibility = Visibility.Collapsed;
            // show the layer as the game now has it
            if (_selected?.Name == name) await LoadTexture(name, -1);
            await UpdateBanner();
        }
    }

    private async void OnCompare(object sender, RoutedEventArgs e)
    {
        if (_plan == null || _texture == null) return;
        _showingReplacement = !_showingReplacement;
        var t = _showingReplacement ? _plan.Result : _texture;
        if (t.Rgba != null) Big.Source = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, _channel);
        await UpdateBanner();
    }

    private void OnGoMods(object sender, RoutedEventArgs e) => GoToMods?.Invoke();

    private void AddPill(string text, string brush)
    {
        var b = (System.Windows.Media.Brush)FindResource(brush);
        Pills.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(9, 2, 9, 2),
            Margin = new Thickness(0, 0, 6, 6),
            Background = (System.Windows.Media.Brush)FindResource("Surface3"),
            Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = b },
        });
    }

    private async void OnLayerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settingMips || _selected == null || LayerPicker.SelectedItem is not Choice c) return;
        _layer = int.Parse(c.Value);
        _plan = null;
        _showingReplacement = false;
        await LoadTexture(_selected.Name, -1);
    }

    /// <summary>Shows layer N of the selected array (scripted runs).</summary>
    public async Task SelectLayer(int layer)
    {
        if (_selected == null) return;
        _layer = layer;
        await LoadTexture(_selected.Name, -1);
    }

    private async void OnMipChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settingMips || _selected == null || MipPicker.SelectedItem is not Choice c) return;
        await LoadTexture(_selected.Name, int.Parse(c.Value));
    }

    private void OnChannelChanged(object sender, RoutedEventArgs e)
    {
        _channel = ((RadioButton)sender).Content switch { "RGB" => Channel.Rgb, "R" => Channel.R, "G" => Channel.G, "B" => Channel.B, "A" => Channel.A, _ => Channel.Rgba };
        if (_texture?.Rgba != null)
            Big.Source = TextureService.ToBitmap(_texture.Rgba, _texture.Width, _texture.Height, _channel);
    }

    private static string Breakable(string s) => s.Replace("_", "_​").Replace("/", "/​");

    // ================================================================ export

    private string ExportPath(string ext)
    {
        string file = Path.Combine(KitPaths.Export, "textures", _texture.Name.Replace('/', Path.DirectorySeparatorChar))
                      + (_texture.Mip > 0 ? $"_mip{_texture.Mip}" : "") + ext;
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        return file;
    }

    private async void OnExportPng(object sender, RoutedEventArgs e) => await Export(".png", t => t.ToPng());

    private async void OnExportDds(object sender, RoutedEventArgs e) => await Export(".dds", t => t.ToDds());

    private async Task Export(string ext, Func<Texture, byte[]> encode)
    {
        if (_texture?.Rgba == null) return;
        var t = _texture;
        try
        {
            string file = ExportPath(ext);
            await Task.Run(() => File.WriteAllBytes(file, encode(t)));
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
