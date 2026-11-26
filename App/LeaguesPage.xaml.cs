using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A conference of the league pack: tick it to put it in.</summary>
public sealed class LeagueConfRow : INotifyPropertyChanged
{
    public PackLeague League { get; init; }
    public string Name => League.Name;
    public ImageSource Logo { get; init; }
    public bool InGame { get; set; }
    public string Meta => $"{League.Teams.Count(t => t.Players.Count > 0)} teams · {League.Teams.Sum(t => t.Players.Count)} players" + (InGame ? " · in game" : "");
    private bool _picked, _selected;
    public bool Picked { get => _picked; set { _picked = value; On(nameof(Picked)); PickedChanged?.Invoke(); } }
    public bool Selected { get => _selected; set { _selected = value; On(nameof(Selected)); } }
    public event Action PickedChanged;
    public event PropertyChangedEventHandler PropertyChanged;
    private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>A team card.</summary>
public sealed class LeagueTeamRow : INotifyPropertyChanged
{
    public PackTeam Team { get; init; }
    public string Name => Team.FullName;
    public string Abbr => Team.Abbreviation;
    public string Arena => $"{Team.Arena} · {CustomLeague.ArenaCity(Team)}";
    public string Players => Team.Players.Count == 0 ? "No roster in the pack: left out" :
        $"{Team.Players.Count} players · {Team.Players.Count(p => p.Position is "C" or "LW" or "RW")} F, {Team.Players.Count(p => p.Position == "D")} D, {Team.Players.Count(p => p.Goalie)} G";
    public Color Primary { get; init; }
    public Color Secondary { get; init; }
    public ImageSource Logo { get; init; }
    public string Where { get; set; }
    public Brush WhereBrush { get; set; }
    private bool _selected;
    public bool Selected { get => _selected; set { _selected = value; On(nameof(Selected)); } }
    public event PropertyChangedEventHandler PropertyChanged;
    private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class LeaguePlayerRow
{
    public PackPlayer P { get; init; }
    public string Number => P.Number.ToString();
    public string Name => $"{P.First} {P.Last}" + (string.IsNullOrEmpty(P.Captain) ? "" : $" ({P.Captain})");
    public string Pos => P.Position;
    public string Shoots => P.Shoots;
    public string Height => P.HeightInches > 0 ? $"{P.HeightInches / 12}'{P.HeightInches % 12}\"" : "";
    public string Weight => P.WeightLbs > 0 ? P.WeightLbs.ToString() : "";
    public string Born => P.BirthYear > 0 ? P.BirthYear.ToString() : "";
    public string Overall => P.Nhl19?.Overall is > 0 and var o ? o.ToString() : "";
}

public sealed record LeagueCheckRow(string What, bool Ok)
{
    public string Icon => Ok ? "" : "";
    public Brush Brush => (Brush)Application.Current.FindResource(Ok ? "Good" : "Danger");
}

/// <summary>
/// NHL 24 custom leagues: a league pack (leagues/&lt;name&gt;/*.json, the NCAA's from the Blueline NHL 19 project), its
/// conferences, teams and rosters, and where each team goes in the game. "Add to game" puts the ticked conferences in
/// as one league (one mod entry over nhl_game.db, initial_experience.db and eng_us.db); the game's files are read
/// fresh each time.
/// </summary>
public partial class LeaguesPage : UserControl
{
    public static string DefaultPack => Path.Combine(KitPaths.Root, "leagues", "ncaa", "ncaa_leagues.json");

    private ModService _mods;
    private LeaguePack _pack;
    private string _packPath = DefaultPack;
    private List<LeagueConfRow> _confs = new();
    private Dictionary<string, int> _inGame = new(StringComparer.OrdinalIgnoreCase);
    private List<int> _freeSlots = new();
    private bool _busy, _loaded;

    public event Action<string> StatusChanged;
    public bool IsReading => _busy;

    public LeaguesPage()
    {
        InitializeComponent();
        UpdateButtons();
    }

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _loaded = false;
        if (IsVisible) _ = Shown();
    }

    public async Task Shown()
    {
        if (_loaded || _busy) return;
        await Reload();
    }

    private LeagueOptions Options() => new()
    {
        Conferences = _confs.Where(c => c.Picked).Select(c => c.League.Key).ToList(),
        LeagueName = string.IsNullOrWhiteSpace(LeagueNameBox.Text) ? "NCAA" : LeagueNameBox.Text.Trim(),
    };

    private static ImageSource Load(string file)
    {
        if (file == null || !File.Exists(file)) return null;
        try
        {
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.DecodePixelWidth = 160;
            b.UriSource = new Uri(file);
            b.EndInit();
            b.Freeze();
            return b;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or FileFormatException) { return null; }
    }

    private static Color ToColor(string hex) => TeamEditor.ParseHex(hex) is int v ? Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v) : Color.FromRgb(0x40, 0x48, 0x58);

    private async Task Reload()
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try
        {
            _pack = LeaguePack.Load(_packPath);
            PackTitle.Text = $"{_pack.Title} · {_pack.Season}";
            PackLogo.Source = Load(Path.Combine(_pack.Dir, "logos", "leagues", "ncaa.png")) ?? Load(Path.Combine(_pack.Dir, _pack.Leagues[0].Logo ?? ""));
            // the game as it is now: which pack teams it has, and the free team slots
            _inGame.Clear();
            if (_mods != null && _mods.Profile.Kind == GameKind.Nhl24)
            {
                StatusChanged?.Invoke("Reading the rosters from the game…");
                var (game, initial, _) = await _mods.OpenLeagueDbsAsync();
                var t = game.Table(Roster.TeamsTable);
                var f = t.Fields.ToDictionary(x => x.Name);
                for (int r = 0; r < t.WrittenRecords; r++)
                    if (!game.IsInvalid(t, r)) _inGame.TryAdd((string)game.Get(t, r, f["artabbr"]), (int)game.Get(t, r, f["index"]));
                _freeSlots = CustomLeague.FreeSlots(game);
            }
            var picked = _confs.Where(c => c.Picked).Select(c => c.League.Key).ToHashSet();
            _confs = _pack.Leagues.Select(l => new LeagueConfRow
            {
                League = l,
                Logo = Load(Path.Combine(_pack.Dir, l.Logo ?? "")),
                InGame = l.Teams.Any(t => _inGame.ContainsKey(CustomLeague.ArtAbbr(t))),
            }).ToList();
            foreach (var c in _confs)
            {
                c.Picked = picked.Count > 0 ? picked.Contains(c.League.Key) : c.InGame;
                c.PickedChanged += () => { UpdateSummary(); UpdateButtons(); };
            }
            ConfList.ItemsSource = _confs;
            ConfList.SelectedIndex = Math.Max(0, _confs.FindIndex(c => c.InGame));
            _loaded = true;
            UpdateSummary();
            StatusChanged?.Invoke($"League pack: {_pack.Leagues.Count} conferences, {_pack.Leagues.Sum(l => l.Teams.Count)} teams");
        }
        catch (Exception e)
        {
            Summary.Text = "Couldn't read the league pack or the game: " + e.Message;
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }

    private void UpdateSummary()
    {
        var o = Options();
        var e = _mods?.Find(CustomLeague.Kind, LeagueInstaller.EntryName(o));
        string state = e == null ? "not in the game" : _mods.State(e) switch
        {
            ModState.Applied => "in the game",
            ModState.NotApplied => "kept, not in the game",
            var s => s.ToString().ToLowerInvariant(),
        };
        int teams = _confs.Where(c => c.Picked).Sum(c => c.League.Teams.Count(t => t.Players.Count > 0));
        StateText.Text = $"Play Now league {o.LeagueId} \"{o.LeagueName}\": {state}" + (e != null ? $" ({e.Summary})" : "") +
                         $". Ticked: {teams} team{(teams == 1 ? "" : "s")} of {_freeSlots.Count + _inGame.Count(x => x.Key.StartsWith("NC"))} slots.";
        Summary.Text = _mods?.Profile.Kind == GameKind.Nhl24
            ? "Custom leagues for NHL 24 Play Now: teams take over the unused Aces slots, players get their own rows, names go in the English strings."
            : "Custom leagues are for NHL 24: open its game folder.";
        SlotsText.Text = $"{_freeSlots.Count} free team slots (the unused Aces rows {(_freeSlots.Count > 0 ? $"{_freeSlots.Min()}-{_freeSlots.Max()}" : "")}). " +
                         "More than 16 teams needs new team rows, not tried in game yet.";
    }

    private void UpdateButtons()
    {
        bool nhl24 = _mods?.Profile.Kind == GameKind.Nhl24;
        var o = _confs.Count > 0 ? Options() : null;
        var e = o == null ? null : _mods?.Find(CustomLeague.Kind, LeagueInstaller.EntryName(o));
        bool applied = e != null && _mods.State(e) is ModState.Applied or ModState.Partial;
        ApplyButton.IsEnabled = !_busy && nhl24 && o?.Conferences.Count > 0;
        RestoreButton.IsEnabled = !_busy && applied;
        VerifyButton.IsEnabled = !_busy && applied;
        ReloadButton.IsEnabled = !_busy;
    }

    private void OnConfSelected(object sender, SelectionChangedEventArgs e)
    {
        foreach (var c in _confs) c.Selected = c == ConfList.SelectedItem;
        if (ConfList.SelectedItem is not LeagueConfRow row) return;
        var good = (Brush)FindResource("Good");
        var dim = (Brush)FindResource("Muted");
        var cards = row.League.Teams.Select(t => new LeagueTeamRow
        {
            Team = t,
            Primary = ToColor(t.Colors.Primary),
            Secondary = ToColor(t.Colors.Secondary),
            Logo = Load(Path.Combine(_pack.Dir, t.Logo ?? "")),
            Where = _inGame.TryGetValue(CustomLeague.ArtAbbr(t), out int slot) ? $"In the game: team {slot}, art {CustomLeague.ArtAbbr(t)}" : t.Players.Count == 0 ? "" : "Not in the game yet",
            WhereBrush = _inGame.ContainsKey(CustomLeague.ArtAbbr(t)) ? good : dim,
        }).ToList();
        TeamCards.ItemsSource = cards;
        TeamsHeader.Text = $"{row.Name.ToUpperInvariant()} · {cards.Count} TEAMS";
        SelectTeam(cards.FirstOrDefault(c => c.Team.Players.Count > 0));
    }

    private void OnTeamClick(object sender, MouseButtonEventArgs e)
    {
        for (var d = e.OriginalSource as DependencyObject; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is FrameworkElement { DataContext: LeagueTeamRow row }) { SelectTeam(row); return; }
    }

    private void SelectTeam(LeagueTeamRow row)
    {
        if (TeamCards.ItemsSource is IEnumerable<LeagueTeamRow> all) foreach (var c in all) c.Selected = c == row;
        if (row == null) { PlayerList.ItemsSource = null; RosterTitle.Text = "ROSTER"; RosterMeta.Text = ""; return; }
        var t = row.Team;
        RosterTitle.Text = $"{t.FullName.ToUpperInvariant()} · ROSTER";
        RosterMeta.Text = $"{t.School} · kits and gear from {t.Nhl19?.KitsCopiedFrom ?? "a WHL team"} for now · ratings 60-70 as in NHL 19";
        var order = new[] { "C", "LW", "RW", "D", "G" };
        PlayerList.ItemsSource = t.Players.OrderBy(p => Array.IndexOf(order, p.Position)).ThenBy(p => p.Number).Select(p => new LeaguePlayerRow { P = p }).ToList();
    }

    /// <summary>For --shot: pick conferences, select one and a team.</summary>
    public void Pick(string keys)
    {
        var set = keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in _confs) c.Picked = set.Contains(c.League.Key);
    }

    public void Select(string conf, int team)
    {
        int i = _confs.FindIndex(c => string.Equals(c.League.Key, conf, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) ConfList.SelectedIndex = i;
        if (team >= 0 && TeamCards.ItemsSource is List<LeagueTeamRow> cards && team < cards.Count) SelectTeam(cards[team]);
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        await Reload();
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (_busy || _mods == null) return;
        var o = Options();
        _busy = true;
        UpdateButtons();
        try
        {
            StatusChanged?.Invoke($"Putting {string.Join(", ", o.Conferences)} into the game…");
            var r = await _mods.InstallLeagueAsync(_pack, _packPath, o);
            var checks = await _mods.VerifyLeagueAsync(r.Teams, o);
            ShowChecks(checks);
            StatusChanged?.Invoke($"{r.Summary}. {checks.Count(c => c.Ok)}/{checks.Count} checks pass.");
        }
        catch (Exception x)
        {
            StatusChanged?.Invoke("Not added: " + x.Message);
            MessageBox.Show(Window.GetWindow(this), x.Message, "League not added", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
            _loaded = false;
            await Reload();
        }
    }

    public async Task VerifyAsync()
    {
        if (_mods == null || _pack == null) return;
        var o = Options();
        var plans = o.Conferences.Select(k => _pack.Conference(k)).SelectMany(c => c.Teams.Where(t => t.Players.Count > 0).Select(t => (c, t)))
            .Where(x => _inGame.ContainsKey(CustomLeague.ArtAbbr(x.t)))
            .Select(x => new LeagueTeamPlan(x.c, x.t, _inGame[CustomLeague.ArtAbbr(x.t)], -1, "", CustomLeague.ArtAbbr(x.t), 0)).ToList();
        StatusChanged?.Invoke("Checking the league in the game's files…");
        var checks = await _mods.VerifyLeagueAsync(plans, o);
        ShowChecks(checks);
        StatusChanged?.Invoke($"{checks.Count(c => c.Ok)}/{checks.Count} checks pass");
    }

    private void ShowChecks(List<LeagueCheck> checks)
    {
        CheckList.ItemsSource = checks.Select(c => new LeagueCheckRow(c.What, c.Ok)).ToList();
        CheckScroller.Visibility = Visibility.Visible;
    }

    private async void OnVerify(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try { await VerifyAsync(); }
        catch (Exception x) { StatusChanged?.Invoke("Check failed: " + x.Message); }
        finally { _busy = false; UpdateButtons(); }
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var o = Options();
        if (_busy || _mods?.Find(CustomLeague.Kind, LeagueInstaller.EntryName(o)) is not { } entry) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Take the {o.LeagueName} league out of the game? nhl_game.db, initial_experience.db and eng_us.db go back as they were before it.",
                "Remove league", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _busy = true;
        UpdateButtons();
        try
        {
            await _mods.RestoreAsync(entry);
            StatusChanged?.Invoke($"{o.LeagueName} league taken out of the game");
        }
        catch (Exception x) { StatusChanged?.Invoke("Not removed: " + x.Message); }
        finally
        {
            _busy = false;
            _loaded = false;
            await Reload();
        }
    }
}
