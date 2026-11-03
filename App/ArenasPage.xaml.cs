using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>An arena in the list: its name from exhibitionarena when the game has one, else the level's.</summary>
public sealed class ArenaRow
{
    public ArenaLevel Level { get; init; }
    public string Name { get; init; }
    public string City { get; init; }
    public string Team { get; init; }
    /// <summary>The team's arena row (exhibitionarena), or -1 for a level no NHL team plays in.</summary>
    public int ArenaId { get; init; } = -1;
    public string Title => Name ?? Level.Venue;
    public string Detail => string.Join("  ·  ", new[] { Team ?? City, Level.Level }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool Matches(string q) => string.IsNullOrEmpty(q) || Title.Contains(q, StringComparison.OrdinalIgnoreCase)
        || (City ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || (Team ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || Level.Level.Contains(q, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The arenas, drawn in 3D: the venue's structure meshes with their textures and the ice with the arena's centre logo
/// (<see cref="ArenaModel"/>). Read from the game every time an arena is picked; nothing is cached or written.
/// </summary>
public partial class ArenasPage : UserControl
{
    private ModService _mods;
    private List<ArenaRow> _all = new();
    private ArenaRow _shown;
    private ArenaModel.Loaded _loaded;
    private BitmapSource _rink;
    private ArenaModel.Ads _ads;
    /// <summary>The teams' ads already cut this session (TheFaceoff.net is slow to page through).</summary>
    private readonly Dictionary<string, ArenaModel.Ads> _teamAds = new();
    private bool _busy;
    private int _version;

    public event Action<string> StatusChanged;

    public ArenasPage() => InitializeComponent();

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _all.Clear();
    }

    /// <summary>The page was opened: lists the arenas (once per game index).</summary>
    public async Task Shown()
    {
        if (_mods == null || _all.Count > 0) return;
        var index = _mods.Index;
        var levels = await Task.Run(() => ArenaScene.Levels(index));
        Dictionary<int, ArenaSummary> arenas = new();
        List<TeamSummary> teams = new();
        try
        {
            var db = await _mods.OpenDbAsync(Roster.Database);
            arenas = TeamEditor.ListArenas(db).GroupBy(a => a.Index).ToDictionary(g => g.Key, g => g.First());
            // the 32 NHL teams; the rest are all-star, AHL and other leagues' teams
            teams = TeamEditor.ListTeams(db).Where(t => t.Index < 32).ToList();
        }
        catch { /* the level names are shown instead */ }
        _all = levels.Select(l =>
        {
            // the level's number isn't the arena row (seattle_33 is row 31): the venue is matched to its NHL team by city
            var team = teams.FirstOrDefault(t => Squash(t.City) == l.Venue.ToLowerInvariant())
                ?? teams.FirstOrDefault(t => Squash(t.City).StartsWith(l.Venue, StringComparison.OrdinalIgnoreCase));
            if (team == null || !arenas.TryGetValue(team.ArenaId, out var a)) return new ArenaRow { Level = l, Name = Readable(l.Venue) };
            // the centre-ice logo goes by the level's own number (the arena id gives Seattle the all-star logo)
            return new ArenaRow { Level = l, Name = Clean(a.Name), City = Clean(a.City), Team = team.Name, ArenaId = team.ArenaId };
        }).OrderBy(r => r.Team == null).ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList();
        ArenaCount.Text = _all.Count.ToString();
        Summary.Text = $"{_all.Count} arenas in the game, drawn in 3D with their own textures. Pick one; it's read from the game each time.";
        Filter();
    }

    /// <summary>Opens an arena by level name (toronto_27), arena name or city (scripted runs).</summary>
    public async Task Show(string arena)
    {
        await Shown();
        var row = _all.FirstOrDefault(r => r.Level.Level.Equals(arena, StringComparison.OrdinalIgnoreCase))
            ?? _all.FirstOrDefault(r => r.Matches(arena));
        if (row == null) return;
        Search.Text = "";
        ArenaList.SelectedItem = row;
        ArenaList.ScrollIntoView(row);
        await OpenAsync(row);
    }

    /// <summary>A camera preset; the overhead view looks down through the roof and the jumbotron, so it takes them off.</summary>
    public void Go(ArenaViewer.View view)
    {
        Viewer.Go(view);
        if (view == ArenaViewer.View.Overhead) RoofSwitch.IsChecked = JumboSwitch.IsChecked = false;
    }
    public void Look(double yaw, double pitch, double distance, double height) => Viewer.Look(yaw, pitch, distance, height);

    public void SetLayers(bool glass, bool ice, bool roof, bool props, bool jumbotron, bool teamAds = true)
    {
        TeamAdsSwitch.IsChecked = teamAds;
        JumboSwitch.IsChecked = jumbotron;
        PropsSwitch.IsChecked = props;
        GlassSwitch.IsChecked = glass;
        IceSwitch.IsChecked = ice;
        RoofSwitch.IsChecked = roof;
    }

    private static string Squash(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>The game's names end in a stray trademark or registered sign.</summary>
    private static string Clean(string s) => (s ?? "").Trim().TrimEnd('\u2122', '\u00AE').Trim();

    /// <summary>outdoorcanal: Outdoor canal, eashltier3: EASHL tier 3, customtoronto: Custom toronto.</summary>
    private static string Readable(string venue)
    {
        string s = Regex.Replace(venue, @"^(outdoor|custom|eashl|arena|arcade|frontend_)", "$1 ");
        s = Regex.Replace(s, @"(tier|generic)(\d*)", " $1 $2");
        s = Regex.Replace(s.Replace('_', ' '), @"(\D)(\d)", "$1 $2");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        s = s.Replace("eashl", "EASHL").Replace("ahl", "AHL").Replace("bap", "Be A Pro");
        return char.ToUpper(s[0]) + s[1..];
    }

    private void Filter()
    {
        string q = Search.Text.Trim();
        var rows = _all.Where(r => r.Matches(q)).ToList();
        ArenaList.SelectionChanged -= OnArenaPicked;
        ArenaList.ItemsSource = rows;
        if (_shown != null && rows.Contains(_shown)) ArenaList.SelectedItem = _shown;
        ArenaList.SelectionChanged += OnArenaPicked;
    }

    private void OnSearch(object sender, TextChangedEventArgs e) => Filter();

    private async void OnArenaPicked(object sender, SelectionChangedEventArgs e)
    {
        if (ArenaList.SelectedItem is ArenaRow row && row != _shown) await OpenAsync(row);
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        if (_shown != null) await OpenAsync(_shown);
    }

    private async Task OpenAsync(ArenaRow row)
    {
        if (_mods == null) return;
        int version = ++_version;
        _shown = row;
        _busy = true;
        Empty.Visibility = Visibility.Collapsed;
        Busy.Visibility = Visibility.Visible;
        BusyText.Text = $"Reading {row.Title}…";
        StatusChanged?.Invoke($"Reading {row.Title} from the game…");
        var index = _mods.Index;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var loaded = await Task.Run(() => ArenaModel.Load(index, row.Level,
                text => Dispatcher.BeginInvoke(() => { if (version == _version) BusyText.Text = text; })));
            if (version != _version) return;
            _loaded = loaded;
            _ads = await LoadAds(row, index, version);
            if (version != _version) return;
            DrawRink();
            await Rebuild();
            Viewer.Go(ArenaViewer.View.Broadcast);
            Detail.Text += $"  ·  read in {sw.Elapsed.TotalSeconds:0.0} s";
            StatusChanged?.Invoke($"Drew {row.Title}");
        }
        catch (Exception ex)
        {
            if (version != _version) return;
            Viewer.Clear();
            Detail.Text = $"Couldn't draw {row.Title}: {ex.Message}";
            StatusChanged?.Invoke(Detail.Text);
        }
        finally
        {
            if (version == _version)
            {
                _busy = false;
                Busy.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// The centre-ice ads: an NHL team's own from TheFaceoff.net when Team ads is on (the game's when the site has none
    /// or can't be read), else the game's.
    /// </summary>
    private async Task<ArenaModel.Ads> LoadAds(ArenaRow row, AssetIndex index, int version)
    {
        if (TeamAdsSwitch.IsChecked == true && row.Team != null)
        {
            if (_teamAds.TryGetValue(row.Team, out var known)) return known;
            string why;
            try
            {
                var ads = await ArenaModel.TeamAds(row.Team, text => Dispatcher.BeginInvoke(() => { if (version == _version) BusyText.Text = text; }));
                if (ads != null) return _teamAds[row.Team] = ads;
                why = $"TheFaceoff.net has no {row.Team} rink";
            }
            catch (Exception ex) { why = $"can't read {row.Team}'s rink from TheFaceoff.net: {ex.Message}"; }
            if (row.ArenaId >= 0) return await Task.Run(() => ArenaModel.GameAds(index, row.ArenaId)) with { Source = $"the game's ({why})" };
        }
        if (row.ArenaId < 0) return null;
        return await Task.Run(() => ArenaModel.GameAds(index, row.ArenaId));
    }

    private void DrawRink()
    {
        string logo = _loaded.CenterLogo;
        _rink = ArenaModel.DrawRink(logo != null ? _loaded.Textures.GetValueOrDefault(logo) : null, _ads?.Images);
    }

    private async void OnTeamAds(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _busy || _loaded == null || _shown == null) return;
        int version = _version;
        _busy = true;
        Busy.Visibility = Visibility.Visible;
        BusyText.Text = "Reading the ads…";
        try
        {
            var ads = await LoadAds(_shown, _mods.Index, version);
            if (version != _version) return;
            _ads = ads;
            DrawRink();
            await Rebuild();
        }
        finally
        {
            if (version == _version)
            {
                _busy = false;
                Busy.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async Task Rebuild()
    {
        if (_loaded == null) return;
        bool glass = GlassSwitch.IsChecked == true, ice = IceSwitch.IsChecked == true, roof = RoofSwitch.IsChecked == true, props = PropsSwitch.IsChecked == true, jumbo = JumboSwitch.IsChecked == true;
        var loaded = _loaded;
        var rink = _rink;
        BusyText.Text = "Building the scene…";
        var (scene, stats) = await Task.Run(() => ArenaModel.Build(loaded, rink, glass, ice, roof, props, jumbo));
        if (loaded != _loaded) return;
        Viewer.Show(scene);
        Detail.Text = $"{_shown.Title}: {stats.Meshes} meshes placed {stats.Placed:N0} times, {stats.Triangles:N0} triangles, {stats.Textures} textures"
            + (stats.Flat > 0 ? $", {stats.Flat} sections coloured by name" : "")
            + (stats.Problems.Count > 0 ? $", {stats.Problems.Count} meshes not read" : "")
            + (_ads != null ? $"  ·  centre-ice ads: {_ads.Source}" : "");
    }

    private async void OnLayers(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _busy || _loaded == null) return;
        await Rebuild();
    }

    private void OnView(object sender, RoutedEventArgs e)
    {
        if (Viewer == null || sender is not RadioButton { CommandParameter: string tag } || !Enum.TryParse<ArenaViewer.View>(tag, out var view)) return;
        Go(view);
    }
}
