using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A jersey in the Unused jerseys grid.</summary>
public sealed class UnusedRow : INotifyPropertyChanged
{
    public UnusedRow(JerseySlots.UnusedKit u) => Unused = u;

    public JerseySlots.UnusedKit Unused { get; }
    public Kit Kit => Unused.Kit;
    public string Path => Unused.Kit.ToString();
    public string LeagueText => Kit.League.ToUpperInvariant();
    public string TeamText => Unused.Team?.Name ?? UniformNames.Pretty(Kit.Team);
    public string KitText => $"{UniformNames.Pretty(Kit.Brand)}  ·  {UniformNames.Pretty(Kit.Name)}";
    public bool CanAdd => Unused.Reason == JerseySlots.UnusedReason.Free;
    public string Badge => Unused.Reason switch
    {
        JerseySlots.UnusedReason.Free => Unused.CopyOf != null ? "COPY" : "CAN ADD",
        JerseySlots.UnusedReason.NoTeam => "NO TEAM",
        _ => "NO SLOT",
    };
    public Brush BadgeBrush => (Brush)Application.Current.FindResource(CanAdd ? "Good" : "Faint");

    /// <summary>How light the jersey is (0-765, the body's mean R+G+B), from its thumbnail; -1 before it's read.</summary>
    public double Lightness { get; set; } = -1;

    private BitmapSource _thumb;
    public BitmapSource Thumb { get => _thumb; set { _thumb = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumb))); } }

    public event PropertyChangedEventHandler PropertyChanged;
}

/// <summary>
/// Jerseys the game has textures for that no team's jersey menu shows (nhl_game.db stockteamjerseys): ones whose team
/// is in the database (a slot can show them; Add to menu puts one in), ones with no database team, and ones a slot
/// can't name. The menus are read from the game each time the page opens and after Reload; Add to menu writes the new
/// slot through the mod list, so Restore on the Mods page undoes it.
/// </summary>
public partial class UnusedPage : UserControl
{
    private const int ThumbSize = 128, TextureSize = 2048;
    private ModService _mods;
    private TextureService _textures;
    private List<UnusedRow> _rows = new();
    private Dictionary<(string League, string Team), JerseySlots.KitTeam> _teams;
    private T3Db _db;
    private bool _busy, _suppress;
    private int _version, _thumbsVersion;
    private readonly DispatcherTimer _searchTimer;

    public event Action<string> StatusChanged;
    /// <summary>A team's menu changed (its team id): the Uniforms page reads its slots again.</summary>
    public event Action<int> SlotsChanged;

    public UnusedPage()
    {
        InitializeComponent();
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyFilter(); };
        AddLabel.ItemsSource = Enumerable.Range(0, 3).Select(i => new PickItem(JerseySlots.LabelName(i), i)).ToList();
        AddLabel.SelectedIndex = 2;
    }

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _textures = new TextureService(mods.Index);
        if (IsVisible) _ = ReloadAsync();
    }

    /// <summary>Called when the page is shown: always reads the menus again.</summary>
    public Task Shown() => _mods == null || _busy ? Task.CompletedTask : ReloadAsync();

    private void OnReload(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    /// <summary>Reads the kit folders and nhl_game.db's jersey menus from the game and lists every jersey no menu shows.</summary>
    public async Task ReloadAsync()
    {
        if (_mods == null || _busy) return;
        _busy = true;
        ReloadButton.IsEnabled = false;
        GridEmpty.Text = "Reading the jersey menus from the game…";
        GridEmpty.Visibility = Visibility.Visible;
        var keep = (Tiles.SelectedItem as UnusedRow)?.Path;
        try
        {
            var index = _mods.Index;
            var db = await _mods.OpenDbAsync(JerseySlots.Database);
            var (unused, teams, total) = await Task.Run(() =>
            {
                var kits = Kits.Find(index);
                return (JerseySlots.Unused(index, db, kits), JerseySlots.Teams(db, kits), kits.Count(k => k.Diffuse("jersey") != null));
            });
            _db = db;
            _teams = teams;
            _rows = unused.Select(u => new UnusedRow(u)).ToList();
            int free = unused.Count(u => u.Reason == JerseySlots.UnusedReason.Free);
            Summary.Text = $"{total:N0} jersey folders in the game, {total - unused.Count:N0} in a team's menu. {unused.Count:N0} aren't: " +
                           $"{free:N0} can go in a menu, {unused.Count - free:N0} have no team in the database.";

            // the teams a slot can be added to, NHL first
            var picks = teams.GroupBy(x => x.Value.TeamId).Select(g => g.OrderBy(x => x.Key.Team.Length).First())
                .Select(x => new StudioTeamItem(x.Value, x.Key.League, $"{x.Value.Name}  ·  {x.Key.League.ToUpperInvariant()}"))
                .OrderBy(t => t.League == "nhl" ? 0 : 1).ThenBy(t => t.League).ThenBy(t => t.Team.Name).ToList();
            _suppress = true;
            AddTeam.ItemsSource = picks;
            var leagues = new[] { "All leagues" }.Concat(_rows.Select(r => r.Kit.League).Distinct().OrderBy(l => l == "nhl" ? 0 : 1).ThenBy(l => l)).ToList();
            string league = LeagueFilter.SelectedItem as string;
            LeagueFilter.ItemsSource = leagues;
            LeagueFilter.SelectedItem = leagues.Contains(league) ? league : leagues[0];
            _suppress = false;
            ApplyFilter();
            if (Tiles.ItemsSource is List<UnusedRow> shown && shown.FirstOrDefault(r => r.Path == keep) is { } again) Tiles.SelectedItem = again;
            StatusChanged?.Invoke($"Unused jerseys: {unused.Count:N0} jersey folders no menu shows ({free:N0} can be added).");
        }
        catch (Exception ex)
        {
            GridEmpty.Text = "Can't read the jersey menus: " + ex.Message;
            StatusChanged?.Invoke("Unused jerseys: " + ex.Message);
        }
        finally
        {
            _busy = false;
            ReloadButton.IsEnabled = true;
            AddButtons();
        }
    }

    // ================================================================ the grid

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { _searchTimer.Stop(); _searchTimer.Start(); }

    private void OnReasonChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppress && _rows != null && Tiles != null) ApplyFilter();
    }

    private void OnLeagueChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppress) ApplyFilter();
    }

    public void SetSearch(string text) => Search.Text = text;

    public void SetReason(string which) => (which switch { "noteam" => ShowNoTeam, "all" => ShowAll, _ => ShowFree }).IsChecked = true;

    private void ApplyFilter()
    {
        if (Tiles == null) return;
        string league = LeagueFilter.SelectedItem as string;
        var terms = Search.Text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = _rows.Where(r => (ShowAll.IsChecked == true || (ShowFree.IsChecked == true) == r.CanAdd)
                                    && (league == null || league == "All leagues" || r.Kit.League == league)
                                    && terms.All(q => r.Path.Contains(q, StringComparison.OrdinalIgnoreCase) || r.TeamText.Contains(q, StringComparison.OrdinalIgnoreCase)))
            // NHL first, then by league and team
            .OrderBy(r => r.Kit.League == "nhl" ? 0 : 1).ThenBy(r => r.Kit.League).ThenBy(r => r.TeamText).ThenBy(r => r.Kit.Brand).ThenBy(r => r.Kit.Name).ToList();
        var keep = Tiles.SelectedItem;
        Tiles.ItemsSource = rows;
        if (keep != null && rows.Contains(keep)) Tiles.SelectedItem = keep;
        Count.Text = $"{rows.Count:N0}";
        GridEmpty.Text = _rows.Count == 0 ? GridEmpty.Text : "No jerseys match.";
        GridEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _ = LoadThumbs(rows);
    }

    /// <summary>Draws the shown jerseys' thumbnails, in the grid's order (a new filter starts over).</summary>
    private async Task LoadThumbs(List<UnusedRow> rows)
    {
        int version = ++_thumbsVersion;
        foreach (var r in rows.Where(r => r.Thumb == null))
        {
            if (version != _thumbsVersion) return;
            try
            {
                var t = await _textures.LoadSizedAsync(r.Kit.Diffuse("jersey").Name, ThumbSize);
                if (t.Rgba == null) continue;
                r.Lightness = Lightness(t.Rgba);
                r.Thumb = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, Channel.Rgb);
                if (Tiles.SelectedItem == r) AddLight.IsChecked = r.Lightness > 480;
            }
            catch { /* left blank */ }
        }
    }

    /// <summary>The mean R+G+B of the sheet's middle, where the body is.</summary>
    private static double Lightness(byte[] rgba)
    {
        int w = (int)Math.Sqrt(rgba.Length / 4);
        long sum = 0, n = 0;
        for (int y = w / 3; y < w * 2 / 3; y++)
            for (int x = w / 4; x < w * 3 / 4; x++)
            {
                int i = (y * w + x) * 4;
                sum += rgba[i] + rgba[i + 1] + rgba[i + 2];
                n++;
            }
        return n > 0 ? sum / (double)n : -1;
    }

    // ================================================================ the jersey on the player

    private async void OnPicked(object sender, SelectionChangedEventArgs e)
    {
        if (Tiles.SelectedItem is UnusedRow r) await ShowKit(r);
    }

    private async Task ShowKit(UnusedRow r)
    {
        int version = ++_version;
        var kit = r.Kit;
        KitTeam.Text = r.TeamText;
        KitPath.Text = $"{kit.League.ToUpperInvariant()}  ·  {r.KitText}  ·  {kit}";
        KitWhy.Text = r.Unused.Reason switch
        {
            JerseySlots.UnusedReason.Free => "No menu shows it." + (r.Unused.CopyOf != null ? $" Same texture as {r.Unused.CopyOf}, which a menu shows." : ""),
            JerseySlots.UnusedReason.NoTeam => $"No team in the database is named \"{kit.Team}\", so no slot can point at this folder.",
            _ => $"A slot names kits by maker (nineties, rbk, adidas) and number; \"{kit.Brand}/{kit.Name}\" isn't one.",
        };
        _suppress = true;
        var picks = AddTeam.ItemsSource as List<StudioTeamItem>;
        AddTeam.SelectedItem = picks?.FirstOrDefault(t => t.Team.TeamId == r.Unused.Team?.TeamId) ?? AddTeam.SelectedItem ?? picks?.FirstOrDefault();
        AddLight.IsChecked = r.Lightness > 480;
        _suppress = false;
        AddButtons();

        ViewBusy.Visibility = Visibility.Visible;
        ViewBusyText.Text = "Loading the kit from the game…";
        try
        {
            var index = _mods.Index;
            var db = _db;
            var gear = await Task.Run(() => KitEquipment.Gear(db, kit));
            Task<ImageSource> Gear(string garment) => KitScene.GearAsync(index, kit, garment, gear?.Helmet?.Zones, gear?.Gloves?.Zones, "");
            var parts = await KitScene.BuildAsync(index, _textures, kit, null, Gear, TextureSize, () => version != _version);
            if (parts == null || version != _version) return;
            Viewer.Show(parts.Select(p => (p.Part, p.Tex)));
            ViewBusy.Visibility = Visibility.Collapsed;
            StatusChanged?.Invoke($"{kit}: {r.Unused.Why}");
        }
        catch (Exception ex)
        {
            if (version == _version) ViewBusyText.Text = "Couldn't draw this kit: " + ex.Message;
        }
    }

    private void OnFront(object sender, RoutedEventArgs e) => Viewer.Face(0);
    private void OnSide(object sender, RoutedEventArgs e) => Viewer.Face(90);
    private void OnBack(object sender, RoutedEventArgs e) => Viewer.Face(180);

    public void Turn(double yaw) => Viewer.Face(yaw);

    /// <summary>Picks a jersey by its folder (nhl/boston/adidas/twelve), showing every jersey first.</summary>
    public async Task SelectKit(string path)
    {
        ShowAll.IsChecked = true;
        Search.Text = "";
        ApplyFilter();
        if ((Tiles.ItemsSource as List<UnusedRow>)?.FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) is not { } row) return;
        Tiles.SelectedItem = row;
        Tiles.ScrollIntoView(row);
        await Task.Delay(100);
    }

    // ================================================================ into a team's menu

    private void OnAddChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppress) AddButtons();
    }

    private void AddButtons()
    {
        if (AddButton == null) return;
        var r = Tiles.SelectedItem as UnusedRow;
        var team = AddTeam.SelectedItem as StudioTeamItem;
        AddButton.IsEnabled = !_busy && r?.CanAdd == true && team != null && _db != null;
        AddStatus.Text = r == null ? "Pick a jersey to see it on the player."
            : !r.CanAdd ? "This one can't go in a menu: " + r.Unused.Why + "."
            : team == null ? ""
            : team.Team.TeamId == r.Unused.Team.TeamId ? $"Adds a slot to {team.Team.Name}'s jersey menu that shows {r.Kit}."
            : $"Adds a slot to {team.Team.Name}'s menu that shows {r.Unused.Team.Name}'s {r.KitText.ToLowerInvariant()} jersey.";
    }

    private async void OnAdd(object sender, RoutedEventArgs e) => await AddAsync(App.Args.ContainsKey("shot"));

    /// <summary>Adds the picked jersey to the picked team's menu: a new stockteamjerseys row, first or last.</summary>
    public async Task AddAsync(bool noAsk = false)
    {
        if (_busy || Tiles.SelectedItem is not UnusedRow r || !r.CanAdd || AddTeam.SelectedItem is not StudioTeamItem team) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "NHL27Kit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var label = AddLabel.SelectedItem as PickItem ?? new PickItem("Third", 2);
        bool light = AddLight.IsChecked == true, top = AddTop.IsChecked == true;
        if (!noAsk && MessageBox.Show(Window.GetWindow(this),
                $"Add {r.Kit} to {team.Team.Name}'s jersey menu as \"{label.Label}\"{(top ? ", first in the menu" : "")}?\n\n" +
                "Only the menu changes: the jersey's textures are the game's own. Restore on the Mods page undoes it.",
                "Add to a team's menu", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _busy = true;
        AddButtons();
        try
        {
            var address = JerseySlots.Address(r.Kit).Value;
            int artId = r.Unused.Team.ArtId, teamId = team.Team.TeamId;
            string summary = null;
            await _mods.SaveDbAsync(JerseySlots.Database, db =>
            {
                var slots = JerseySlots.Read(db, teamId);
                var slot = JerseySlots.NewSlot(db, artId, address.Style, address.Variant) with { Label = label.Value, IsLight = light, IsDefault = false };
                if (top) slots.Insert(0, slot); else slots.Add(slot);
                summary = JerseySlots.Write(db, teamId, slots);
                return $"Unused jerseys: {team.Team.Name} jersey menu, {r.Kit} added as {label.Label} ({summary})";
            }, "Unused jerseys");
            SlotsChanged?.Invoke(teamId);
            StatusChanged?.Invoke($"Unused jerseys: {r.Kit} is in {team.Team.Name}'s jersey menu as {label.Label} ({summary}). Restore on the Mods page undoes it.");
        }
        catch (Exception ex)
        {
            AddStatus.Text = "Can't add it: " + ex.Message;
            if (!noAsk) MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't add the jersey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally
        {
            _busy = false;
        }
        await ReloadAsync();
    }
}
