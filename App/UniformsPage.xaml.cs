using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A team in the Uniforms list, with its kits.</summary>
public sealed class TeamRow
{
    public string League { get; init; }
    public string Team { get; init; }
    public List<Kit> Kits { get; init; }
    public string Display => UniformNames.Pretty(Team);
    public string Meta => $"{League.ToUpperInvariant()}  ·  {Kits.Count} kit{(Kits.Count == 1 ? "" : "s")}";
}

/// <summary>A kit tab over the 3D view.</summary>
/// <summary>One colour zone of the helmet or gloves: its database colour and the value in its box.</summary>
public sealed class GearZone : Observable
{
    private readonly Action<GearZone> _changed;
    private int _original;
    private string _text;
    private bool _selected;

    public GearZone(string piece, int zone, string label, double share, int original, Action<GearZone> changed)
    {
        Piece = piece;
        Zone = zone;
        Label = label;
        Share = share;
        _original = original;
        _text = Hex(original);
        _changed = changed;
    }

    public string Piece { get; }
    public int Zone { get; }
    public string Label { get; }
    public double Share { get; }
    public string Meta => $"zone {Zone}  ·  {(Share < 0.01 ? "under 1%" : Share.ToString("P0"))} of the {Piece}" + (IsChanged ? $"  ·  was {Hex(_original)}" : "");

    public static string Hex(int c) => "#" + (c & 0xFFFFFF).ToString("X6");

    /// <summary>The box's colour, or null when it isn't a hex colour.</summary>
    public int? Value
    {
        get
        {
            string t = (_text ?? "").Trim().TrimStart('#');
            return t.Length == 6 && int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v) ? v : null;
        }
    }

    public bool HasError => Value == null;
    public bool IsChanged => Value != _original;
    public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }

    public Brush Swatch => Value is int c ? new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)) : Brushes.Transparent;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            Raise(nameof(Text)); Raise(nameof(Swatch)); Raise(nameof(IsChanged)); Raise(nameof(HasError)); Raise(nameof(Meta));
            _changed?.Invoke(this);
        }
    }

    public void Reset() => Text = Hex(_original);

    /// <summary>The box's value is now the database's.</summary>
    public void Saved()
    {
        if (Value is int v) _original = v;
        Raise(nameof(IsChanged)); Raise(nameof(Meta));
    }
}

public sealed class KitTab
{
    public Kit Kit { get; init; }
    public bool IsSelected { get; init; }
    public string Label => $"{UniformNames.Pretty(Kit.Name)}  ·  {UniformNames.Pretty(Kit.Brand)}";
    public override string ToString() => Label;
}

/// <summary>A texture of the shown kit.</summary>
public sealed class KitTexRow : Observable
{
    private BitmapSource _thumb;

    public KitTexture Texture { get; init; }
    public string ModText { get; init; }
    public bool HasMod => ModText != null;
    public string Label => UniformNames.Pretty(Texture.Slot.Replace("_" + Texture.Map, "").Replace('_', ' '));
    public string Map => Texture.Map;
    public Brush MapBrush => (Brush)Application.Current.FindResource(Map switch { "color" => "Accent", "normal" => "Violet", "coeff" => "Patch", _ => "Muted" });
    public BitmapSource Thumb { get => _thumb; set => Set(ref _thumb, value); }
}

public static class UniformNames
{
    public static string Pretty(string s) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace('_', ' '));
}

/// <summary>
/// Team uniforms: pick a team and a kit and it is drawn in 3D on the game's jersey, pants and socks meshes. Each of the
/// kit's textures can be replaced from here; a replacement is written straight into the game files (through the mod
/// list, which keeps the original for Restore), then the kit is read from the game again.
/// </summary>
public partial class UniformsPage : UserControl
{
    private ModService _mods;
    private TextureService _textures;
    private List<TeamRow> _teams = new();
    private TeamRow _team;
    private Kit _kit;
    private int _version;
    private bool _suppress, _busy;
    /// <summary>The garment textures' size: the bases' full 2048, so stamped logos keep about their own 512 pixels.</summary>
    private const int TextureSize = 2048;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _letterTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer _gearTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private List<(string Garment, MeshPart Part, ImageSource Tex)> _shown;
    private Kit _gearKit;
    private string _gearTeam;
    private KitEquipment.GearRow _helmetRow, _gloveRow;
    private List<(int Zone, double Share)> _helmetUsed, _gloveUsed;
    private List<int> _teamColors = new();

    public event Action<string> StatusChanged;

    public UniformsPage()
    {
        InitializeComponent();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyFilter(); };
        _letterTimer.Tick += async (_, _) => { _letterTimer.Stop(); if (_kit != null) await ShowKit(_kit); };
        _gearTimer.Tick += async (_, _) => { _gearTimer.Stop(); await RedrawGear(); };
        _suppress = true;
        LetterName.Text = "PLAYER";
        LetterNumber.Text = "27";
        LetterCaptain.SelectedIndex = 0;
        _suppress = false;
    }

    public async void SetMods(ModService mods)
    {
        _mods = mods;
        _textures = new TextureService(mods.Index);
        Summary.Text = "Finding the kits…";
        var kits = await Task.Run(() => Kits.Find(mods.Index));
        _teams = kits.GroupBy(k => (k.League, k.Team)).Select(g => new TeamRow { League = g.Key.League, Team = g.Key.Team, Kits = g.ToList() })
                     .OrderBy(t => t.League == "nhl" ? 0 : 1).ThenBy(t => t.League).ThenBy(t => t.Team).ToList();
        var leagues = new List<string> { "All leagues" };
        leagues.AddRange(_teams.Select(t => t.League).Distinct().OrderBy(l => l == "nhl" ? "" : l));
        _suppress = true;
        LeagueFilter.ItemsSource = leagues;
        LeagueFilter.SelectedItem = leagues.Contains("nhl") ? "nhl" : leagues[0];
        _suppress = false;
        Summary.Text = $"{kits.Count:N0} kits of {_teams.Count:N0} teams in {leagues.Count - 1} leagues  ·  drawn with the game's own models  ·  replacements are written into the game";
        ApplyFilter();
    }

    public void SetSearch(string text) { Search.Text = text; ApplyFilter(); }

    public async Task SelectTeam(int n, int kit = 0)
    {
        if (TeamList.Items.Count <= n) return;
        TeamList.SelectedIndex = n;
        if (TeamList.SelectedItem is TeamRow t && kit > 0 && kit < t.Kits.Count) await ShowKit(t.Kits[kit]);
    }

    public void Turn(double yaw) => Viewer.Face(yaw);

    /// <summary>Sets the previewed name, number and captain's letter (scripted runs).</summary>
    public void SetLettering(string name, string number, string captain)
    {
        _suppress = true;
        if (name != null) LetterName.Text = name;
        if (number != null) LetterNumber.Text = number;
        if (captain != null) LetterCaptain.SelectedIndex = captain.ToUpperInvariant() switch { "C" => 1, "A" => 2, _ => 0 };
        _suppress = false;
    }

    /// <summary>The name and number drawn on the jersey, from the boxes under the model.</summary>
    private KitLettering Lettering() => new(LetterName.Text.Trim(), new string(LetterNumber.Text.Where(char.IsDigit).ToArray()),
        LetterCaptain.SelectedIndex switch { 1 => "C", 2 => "A", _ => null });

    private void OnLetteringChanged(object sender, RoutedEventArgs e)
    {
        if (_suppress || _kit == null) return;
        _letterTimer.Stop();
        _letterTimer.Start();
    }

    public void Zoom(double factor, double up) => Viewer.Zoom(factor, up);

    /// <summary>Replaces a slot of the shown kit (scripted runs).</summary>
    public Task ReplaceSlot(string slot, string file) => _kit?.Slot(slot) is { } t ? ReplaceWith(t, file) : Task.CompletedTask;
    public Task ExportSlot(string slot, string file) => _kit?.Slot(slot) is { } t ? ExportTo(t, file) : Task.CompletedTask;

    private void ApplyFilter()
    {
        string league = LeagueFilter.SelectedItem as string;
        var terms = Search.Text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = _teams.Where(t => (league == null || league == "All leagues" || t.League == league)
                                     && terms.All(q => t.Team.Contains(q) || t.League.Contains(q))).ToList();
        TeamList.ItemsSource = rows;
        TeamCount.Text = $"{rows.Count:N0}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { _searchTimer.Stop(); _searchTimer.Start(); }

    private void OnLeagueChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppress) ApplyFilter();
    }

    private async void OnTeamSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TeamList.SelectedItem is not TeamRow t) return;
        _team = t;
        _ = LoadSlots(t);
        await ShowKit(t.Kits.FirstOrDefault(k => k.Name == "home" && k.Brand == "adidas") ?? t.Kits.FirstOrDefault(k => k.Name == "home") ?? t.Kits[0]);
    }

    private async void OnKitPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || KitPicker.SelectedItem is not KitTab tab || tab.Kit == _kit) return;
        await ShowKit(tab.Kit);
    }

    /// <summary>Reads the kit's meshes and textures from the game now and draws it.</summary>
    private async Task ShowKit(Kit kit)
    {
        int version = ++_version;
        _kit = kit;
        EndLegacy();
        if ((AddKitPicker.ItemsSource as IEnumerable<SlotKitItem>)?.FirstOrDefault(k => k.Kit == kit) is { } shownKit) AddKitPicker.SelectedItem = shownKit;
        _suppress = true;
        var tabs = _team?.Kits.Select(k => new KitTab { Kit = k, IsSelected = k == kit }).ToList();
        KitPicker.ItemsSource = tabs;
        KitPicker.SelectedItem = tabs?.FirstOrDefault(t => t.IsSelected);
        _suppress = false;
        TeamTitle.Text = UniformNames.Pretty(kit.Team);
        KitTitle.Text = $"{kit.League.ToUpperInvariant()}  ·  {UniformNames.Pretty(kit.Brand)}  ·  {UniformNames.Pretty(kit.Name)}  ·  {_team?.Kits.Count} kits";
        ViewEmpty.Visibility = Visibility.Collapsed;
        ViewBusy.Visibility = Visibility.Visible;
        ViewBusyText.Text = "Loading the kit from the game…";
        await RefreshTextures();
        try
        {
            await LoadGear(kit);
            if (version != _version) return;
            var parts = await KitScene.BuildAsync(_mods.Index, _textures, kit, Lettering(), GearTexture, TextureSize, () => version != _version);
            if (parts == null || version != _version) return;
            _shown = parts;
            Viewer.Show(parts.Select(p => (p.Part, p.Tex)));
            ViewBusy.Visibility = Visibility.Collapsed;
            StatusChanged?.Invoke($"{kit}: {parts.Count} parts, {parts.Sum(p => p.Part.TriangleCount):N0} triangles" +
                                  (_gearTeam != null ? $"  ·  helmet and gloves in {_gearTeam} colours (from the database)" : "  ·  no team in the database for helmet and glove colours"));
        }
        catch (Exception ex)
        {
            ViewBusyText.Text = "Couldn't draw this kit: " + ex.Message;
        }
    }

    /// <summary>The kit's texture list, with what the mod list says about each.</summary>
    private async Task RefreshTextures()
    {
        if (_kit == null) return;
        var states = await _mods.StatesAsync();
        // the branding arrays hold the same images as the numbered branding textures (layer N = branding_..._N); they are
        // kept in step when one of those is replaced, so they aren't listed
        var rows = _kit.Textures.Where(t => !t.Slot.EndsWith("_array")).Select(t =>
        {
            var e = _mods.Find("texture", t.Name);
            string mod = e == null ? null : states.GetValueOrDefault(e) switch { ModState.Applied => "replaced in game", ModState.NotApplied => "not applied", _ => "changed" };
            return new KitTexRow { Texture = t, ModText = mod };
        }).ToList();
        TexList.ItemsSource = rows;
        TexCount.Text = $"{rows.Count}";
        foreach (var r in rows)
        {
            var kit = _kit;
            _ = Task.Run(async () =>
            {
                var bmp = await _textures.ThumbnailAsync(r.Texture.Name, 96);
                await Dispatcher.InvokeAsync(() => { if (_kit == kit) r.Thumb = bmp; });
            });
        }
    }

    // ================================================================ helmet and glove colours

    private static bool IsGear(string garment) => KitScene.IsGear(garment);

    /// <summary>
    /// Reads the kit's helmet and glove rows and its team's colours from the game's database. Unsaved edits for the same
    /// kit are kept (a redraw after a lettering change); a new kit starts clean.
    /// </summary>
    private async Task LoadGear(Kit kit)
    {
        var index = _mods.Index;
        var read = await Task.Run(() =>
        {
            try
            {
                var db = new Legacy(index).OpenDb(KitEquipment.Database);
                var g = KitEquipment.Gear(db, kit);
                var colors = new List<int>();
                if (g is { } gg)
                {
                    var t = db.Table("stockteamcolorstable");
                    var f = t.Fields.ToDictionary(x => x.Name);
                    for (int r = 0; r < t.WrittenRecords; r++)
                        if (!db.IsInvalid(t, r) && (int)db.Get(t, r, f["team"]) == gg.TeamIndex) colors.Add((int)db.Get(t, r, f["color"]));
                }
                _helmetUsed ??= KitEquipment.UsedZones(index, "helmet");
                _gloveUsed ??= KitEquipment.UsedZones(index, "gloves");
                return (Gear: g, Colors: colors);
            }
            catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException) { return (null, new List<int>()); }
        });
        bool keepEdits = _gearKit == kit && GearZones.Any(z => z.IsChanged);
        _gearKit = kit;
        _gearTeam = read.Gear?.Team;
        _teamColors = read.Colors;
        _helmetRow = read.Gear?.Helmet;
        _gloveRow = read.Gear?.Gloves;
        if (!keepEdits)
        {
            _selectedZone = null;
            string[] helmetNames = { "Shell", "Trim", "Maker's logo" };
            GearZone Make(string piece, (int Zone, double Share) u, KitEquipment.GearRow row) =>
                new(piece, u.Zone, piece == "helmet" && u.Zone <= 3 ? helmetNames[u.Zone - 1] : $"Zone {u.Zone}", u.Share, row.Zones[u.Zone - 1], OnZoneChanged);
            HelmetZones.ItemsSource = _helmetRow == null ? null : _helmetUsed.Select(u => Make("helmet", u, _helmetRow)).ToList();
            GloveZones.ItemsSource = _gloveRow == null ? null : _gloveUsed.OrderByDescending(u => u.Share).Select(u => Make("gloves", u, _gloveRow)).ToList();
        }
        TeamColors.ItemsSource = read.Colors.Concat(new[] { 0xE7E7E7, 0x202020 }).Distinct()
            .Select(c => new { Hex = GearZone.Hex(c), Brush = (Brush)new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)) }).ToList();
        GearSource.Text = _gearTeam == null
            ? "This kit's team isn't in the database, so it has no helmet or glove colours."
            : $"{_gearTeam}, from nhl_game.db's stockteamequipmenttable." +
              (_helmetRow != null ? $" Helmet: row {_helmetRow.Row}, used by {_helmetRow.Scope(kit)}." : "") +
              (_gloveRow != null ? $" Gloves: row {_gloveRow.Row}, used by {_gloveRow.Scope(kit)}." : "");
        HelmetHeader.Visibility = _helmetRow != null ? Visibility.Visible : Visibility.Collapsed;
        GloveHeader.Visibility = _gloveRow != null ? Visibility.Visible : Visibility.Collapsed;
        GearButtons();
    }

    private IEnumerable<GearZone> GearZones =>
        (HelmetZones.ItemsSource as IEnumerable<GearZone> ?? Enumerable.Empty<GearZone>()).Concat(GloveZones.ItemsSource as IEnumerable<GearZone> ?? Enumerable.Empty<GearZone>());

    /// <summary>A piece's 15 zone colours with the boxes' values on top (a box that isn't a colour keeps the database's).</summary>
    private int[] Zones(string piece)
    {
        var row = piece == "helmet" ? _helmetRow : _gloveRow;
        if (row == null) return null;
        var z = (int[])row.Zones.Clone();
        foreach (var g in GearZones.Where(g => g.Piece == piece && g.Value is not null)) z[g.Zone - 1] = g.Value.Value;
        return z;
    }

    /// <summary>The helmet (with the kit's decals and the previewed number) or a glove, in the zone boxes' colours.</summary>
    private Task<ImageSource> GearTexture(string garment) =>
        KitScene.GearAsync(_mods.Index, _gearKit ?? _kit, garment, Zones("helmet"), Zones("gloves"), Lettering().Number);

    /// <summary>Redraws only the helmet and gloves (after a colour edit), keeping the view.</summary>
    private async Task RedrawGear()
    {
        if (_shown == null) return;
        var tex = new Dictionary<string, ImageSource>();
        foreach (var g in new[] { "helmet", "glove_left", "glove_right" }) tex[g] = await GearTexture(g);
        _shown = _shown.Select(p => IsGear(p.Garment) ? (p.Garment, p.Part, tex[p.Garment]) : p).ToList();
        Viewer.Show(_shown.Select(p => (p.Part, p.Tex)));
    }

    private void OnZoneChanged(GearZone z)
    {
        GearButtons();
        _gearTimer.Stop();
        _gearTimer.Start();
    }

    private GearZone _selectedZone;

    private void Select(GearZone z)
    {
        foreach (var g in GearZones) g.IsSelected = g == z;
        _selectedZone = z;
    }

    private void OnZoneClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GearZone z) Select(z);
    }

    private void OnZoneFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GearZone z) Select(z);
    }

    private void OnTeamColor(object sender, RoutedEventArgs e)
    {
        if (_selectedZone != null && (sender as FrameworkElement)?.Tag is string hex) _selectedZone.Text = hex;
    }

    private void GearButtons()
    {
        int changed = GearZones.Count(z => z.IsChanged && !z.HasError);
        bool errors = GearZones.Any(z => z.HasError);
        GearSave.IsEnabled = !_busy && changed > 0 && !errors;
        GearDiscard.IsEnabled = !_busy && GearZones.Any(z => z.IsChanged);
        GearStatus.Text = errors ? "A colour isn't a hex value (like #1A2B3C)." :
            changed == 0 ? "Pick a zone, then type a colour or click a team colour. The model shows it right away." :
            $"{changed} zone colour(s) changed, not saved yet.";
    }

    private void OnKitTab(object sender, RoutedEventArgs e)
    {
        if (GearPanel == null || SlotsPanel == null || TabSlots == null) return;
        bool gear = TabGear.IsChecked == true, slots = TabSlots.IsChecked == true;
        GearPanel.Visibility = gear ? Visibility.Visible : Visibility.Collapsed;
        SlotsPanel.Visibility = slots ? Visibility.Visible : Visibility.Collapsed;
        TexList.Visibility = gear || slots ? Visibility.Collapsed : Visibility.Visible;
        TexCount.Visibility = gear || slots ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnGearDiscard(object sender, RoutedEventArgs e)
    {
        foreach (var z in GearZones) z.Reset();
        GearButtons();
        await RedrawGear();
    }

    private async void OnGearSave(object sender, RoutedEventArgs e) => await SaveGearAsync();

    /// <summary>Writes the changed zone colours into the kit's helmet and glove rows of the game's database, then redraws from the game.</summary>
    public async Task SaveGearAsync()
    {
        var changed = GearZones.Where(z => z.IsChanged && z.Value is not null).ToList();
        if (changed.Count == 0 || _busy || _gearKit == null) return;
        int helmetRow = _helmetRow?.Row ?? -1, gloveRow = _gloveRow?.Row ?? -1;
        var kit = _gearKit;
        string team = _gearTeam;
        _busy = true;
        GearButtons();
        try
        {
            await _mods.SaveDbAsync(KitEquipment.Database, db =>
            {
                var t = db.Table("stockteamequipmenttable");
                foreach (var z in changed)
                    db.Set(t, z.Piece == "helmet" ? helmetRow : gloveRow, t.Fields.First(f => f.Name == "zone" + z.Zone), z.Value.Value);
                return $"Uniforms: {team} " + string.Join(", ", changed.Select(z => $"{z.Piece} {z.Label.ToLowerInvariant()} = {GearZone.Hex(z.Value.Value)}"));
            }, "Uniforms page");
            foreach (var z in changed) z.Saved();
            StatusChanged?.Invoke($"{changed.Count} helmet/glove colour(s) written into the game (nhl_game.db). The Mods page can restore the original.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't save: " + ex.Message);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't save to the game", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        _gearKit = null;
        await ShowKit(kit);
    }

    /// <summary>Sets a zone's colour box (scripted runs): "helmet:1=#FF0000".</summary>
    public void SetGearZone(string spec)
    {
        var m = System.Text.RegularExpressions.Regex.Match(spec ?? "", @"^(helmet|gloves):(\d+)=(.+)$");
        if (m.Success && GearZones.FirstOrDefault(z => z.Piece == m.Groups[1].Value && z.Zone == int.Parse(m.Groups[2].Value)) is { } z)
        {
            Select(z);
            z.Text = m.Groups[3].Value;
        }
    }

    public void ShowGearTab() => TabGear.IsChecked = true;

    private void OnFront(object sender, RoutedEventArgs e) => Viewer.Face(0);
    private void OnSide(object sender, RoutedEventArgs e) => Viewer.Face(90);
    private void OnBack(object sender, RoutedEventArgs e) => Viewer.Face(180);

    private async void OnReplace(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not KitTexRow row) return;
        var dlg = new OpenFileDialog { Title = $"Replace {row.Label} ({row.Map})", Filter = ImageLoader.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await ReplaceWith(row.Texture, dlg.FileName);
    }

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not KitTexRow row || _kit == null) return;
        string folder = System.IO.Path.Combine(KitPaths.Export, "uniforms", _kit.League, _kit.Team);
        System.IO.Directory.CreateDirectory(folder);
        var dlg = new SaveFileDialog
        {
            Title = $"Export {row.Label} ({row.Map})",
            Filter = "PNG image|*.png|DDS, as stored in the game (BC compressed)|*.dds",
            InitialDirectory = folder,
            FileName = $"{_kit.Team}_{_kit.Name}_{row.Texture.Slot}.png",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await ExportTo(row.Texture, dlg.FileName);
    }

    /// <summary>Writes a kit texture, read from the game now at its largest stored size, as PNG or (by the extension) DDS.</summary>
    public async Task ExportTo(KitTexture texture, string file)
    {
        try
        {
            var index = _mods.Index;
            bool dds = file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
            var (w, h) = await Task.Run(() =>
            {
                var t = Texture.Load(index, texture.Name);
                if (t.Rgba == null) throw new System.IO.InvalidDataException(t.Problem ?? "it couldn't be decoded");
                System.IO.File.WriteAllBytes(file, dds ? t.ToDds() : t.ToPng());
                return (t.Width, t.Height);
            });
            StatusChanged?.Invoke($"Exported {texture.Slot} ({w}×{h}) to {file}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Can't export {texture.Slot}: {ex.Message}");
        }
    }

    /// <summary>Encodes an image for a kit texture and writes it into the game files, then redraws the kit from the game.</summary>
    public Task<bool> ReplaceWith(KitTexture texture, string file) => ReplaceAll(new[] { (texture, file) }, null);

    /// <summary>Writes several kit textures into the game files in one go, then redraws the kit from the game.</summary>
    private async Task<bool> ReplaceAll(IReadOnlyList<(KitTexture Texture, string File)> items, string summary)
    {
        if (_busy || _mods == null) return false;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        _busy = true;
        string error = null;
        var done = new List<string>();
        try
        {
            foreach (var (texture, file) in items)
            {
                done.Add(await WriteTexture(texture, file));
                StatusChanged?.Invoke(done[^1]);
            }
            _textures.ClearThumbs();
        }
        catch (Exception ex)
        {
            error = "Can't replace: " + ex.Message;
            StatusChanged?.Invoke(error);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't replace this texture", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        await ShowKit(_kit);
        StatusChanged?.Invoke(error ?? summary ?? string.Join("  ·  ", done));
        return error == null;
    }

    /// <summary>Encodes one image for a kit texture and writes it into the game; returns what was done.</summary>
    private Task<string> WriteTexture(KitTexture texture, string file) => KitWriter.WriteTextureAsync(_mods, _kit, texture, file, t => StatusChanged?.Invoke(t));

    /// <summary>Forgets unsaved slot edits of a team and reads its slots again when it's shown (after the studio added one).</summary>
    public void SlotsChangedElsewhere(int teamId)
    {
        _slotDrafts.Remove(teamId);
        _slotCounts.Remove(teamId);
        if (_team != null && _slotTeam?.TeamId == teamId) _ = LoadSlots(_team);
    }
}
