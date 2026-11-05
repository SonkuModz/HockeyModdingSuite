using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// A team's names, colours, palette, home arena and rivals, read from nhl_game.db every time the page loads and written
/// back through the database's one mod entry (the same one the Database and Roster pages add to). Edits are kept per
/// value while you move between teams, so one save can cover several teams.
/// </summary>
public partial class TeamsPage : UserControl
{
    private ModService _mods;
    private T3Db _db;
    private List<TeamSummary> _teams = new();
    private List<ArenaSummary> _arenas = new();
    private TeamSummary _team;
    private readonly Dictionary<(string Table, int Row, string Field), (object Value, string Label, string Team, bool Color)> _edits = new();
    private bool _busy, _loaded, _suppress;
    private TextureService _textures;
    private TeamLogoMap _logos;
    private List<TeamLogoRow> _logoRows = new();
    private int _logoLoad;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public event Action<string> StatusChanged;

    public TeamsPage()
    {
        InitializeComponent();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyFilter(); };
    }

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _textures = new TextureService(mods.Index);
    }

    /// <summary>The page was opened: reads the teams from the game the first time (Reload reads them again).</summary>
    public async Task Shown()
    {
        if (_mods == null || _busy || _loaded) return;
        await LoadAsync();
    }

    /// <summary>Filters the list and shows a team (scripted runs).</summary>
    public async Task Show(string search, string team)
    {
        if (!_loaded) await LoadAsync();
        if (search != null) { Search.Text = search; ApplyFilter(); }
        if (team != null)
        {
            TeamListRow Pick()
            {
                var rows = TeamList.Items.OfType<TeamListRow>().ToList();
                return rows.FirstOrDefault(r => r.Team.Abbr.Equals(team, StringComparison.OrdinalIgnoreCase))
                    ?? rows.FirstOrDefault(r => r.Team.Index.ToString() == team)
                    ?? rows.FirstOrDefault(r => r.Team.Name.Contains(team, StringComparison.OrdinalIgnoreCase));
            }
            // the shown league first (TOR is the Leafs, not a Circuit team); then the team may be in another league
            var pick = Pick();
            if (pick == null) { LeagueFilter.SelectedItem = "All leagues"; ApplyFilter(); pick = Pick(); }
            TeamList.SelectedItem = pick;
            if (TeamList.SelectedItem != null) TeamList.ScrollIntoView(TeamList.SelectedItem);
        }
    }

    /// <summary>Changes a shown value (scripted runs): FIELD=VALUE, FIELD being a table field such as feprimarycolor or arena.arenaname.</summary>
    public void SetValue(string spec)
    {
        int eq = spec.IndexOf('=');
        if (eq < 0) return;
        string key = spec[..eq].Trim(), value = spec[(eq + 1)..];
        string table = key.StartsWith("arena.") ? TeamEditor.Arenas : TeamEditor.Teams;
        if (key.StartsWith("arena.")) key = key[6..];
        if (key.StartsWith("palette")) { table = TeamEditor.Palette; }
        var row = AllRows().FirstOrDefault(r => r.Field.Table == table && (r.Field.Field == key || r.Field.Label.Equals(key, StringComparison.OrdinalIgnoreCase)));
        if (row == null) return;
        if (row.IsToggle) row.BoolValue = value is "1" or "on" or "true";
        else if (row.IsPick) row.PickValue = row.Picks.FirstOrDefault(p => p.Label.Contains(value, StringComparison.OrdinalIgnoreCase) || p.Value.ToString() == value) ?? row.PickValue;
        else row.Text = value;
    }

    /// <summary>Scrolls the editor (scripted runs).</summary>
    public void ScrollTo(double offset) => Editor.ScrollToVerticalOffset(offset);

    private IEnumerable<TeamValueRow> AllRows() => (Sections.ItemsSource as List<TeamSection>)?.SelectMany(s => s.Rows) ?? Enumerable.Empty<TeamValueRow>();

    private async Task LoadAsync()
    {
        if (_mods == null) return;
        _busy = true;
        SetButtons();
        int keep = _team?.Row ?? -1;
        try
        {
            _db = await _mods.OpenDbAsync(TeamEditor.Database);
            var index = _mods.Index;
            _logos ??= await Task.Run(() => TeamLogos.All(index));
            _teams = TeamEditor.ListTeams(_db);
            _arenas = TeamEditor.ListArenas(_db);
            _edits.Clear();
            _loaded = true;
            var state = _mods.Find(DbEditor.Kind, TeamEditor.Database) is { } e ? (await _mods.StatesAsync()).GetValueOrDefault(e) : (ModState?)null;
            Summary.Text = $"{_teams.Count} teams  ·  {_arenas.Count} arenas  ·  read from nhl_game.db" +
                           (state == ModState.Applied ? "  ·  has your saved edits (Mods page restores the original)" : "");

            _suppress = true;
            var leagues = _teams.GroupBy(t => t.League).OrderByDescending(g => g.Key == "NHL").ThenByDescending(g => g.Count()).Select(g => g.Key).ToList();
            string pickedLeague = LeagueFilter.SelectedItem as string;
            LeagueFilter.ItemsSource = new[] { "All leagues" }.Concat(leagues).ToList();
            LeagueFilter.SelectedItem = pickedLeague != null && leagues.Contains(pickedLeague) ? pickedLeague : leagues.FirstOrDefault() ?? "All leagues";
            _suppress = false;
            ApplyFilter();
            _team = null;
            var again = _teams.FirstOrDefault(t => t.Row == keep);
            if (again != null) TeamList.SelectedItem = TeamList.Items.OfType<TeamListRow>().FirstOrDefault(r => r.Team.Row == keep);
            if (TeamList.SelectedItem == null) ShowTeam(null);
        }
        catch (Exception ex)
        {
            Summary.Text = "Couldn't read the teams: " + ex.Message;
        }
        finally
        {
            _suppress = false;
            _busy = false;
            SetButtons();
        }
    }

    private void ApplyFilter()
    {
        string q = Search.Text.Trim();
        string league = LeagueFilter.SelectedItem as string;
        var rows = _teams
            .Where(t => league == null || league == "All leagues" || t.League == league)
            .Where(t => q.Length == 0 || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || t.City.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        t.Abbr.Equals(q, StringComparison.OrdinalIgnoreCase) || t.Index.ToString() == q)
            .OrderByDescending(t => t.Active).ThenBy(t => t.Name)
            .Select(t => new TeamListRow { Team = t }).ToList();
        var selected = _team;
        _suppress = true;
        TeamList.ItemsSource = rows;
        TeamList.SelectedItem = rows.FirstOrDefault(r => r.Team.Row == selected?.Row);
        _suppress = false;
        TeamCount.Text = $"{rows.Count}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { _searchTimer.Stop(); _searchTimer.Start(); }

    private void OnLeagueChanged(object sender, SelectionChangedEventArgs e) { if (!_suppress) ApplyFilter(); }

    private void OnTeamSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || TeamList.SelectedItem is not TeamListRow row) return;
        ShowTeam(row.Team);
    }

    /// <summary>Builds the team's sections from the database read at load (with the edits not saved yet put back in).</summary>
    private void ShowTeam(TeamSummary team)
    {
        _team = team;
        Empty.Visibility = team == null ? Visibility.Visible : Visibility.Collapsed;
        EditorRoot.Visibility = team == null ? Visibility.Collapsed : Visibility.Visible;
        if (team == null) { Sections.ItemsSource = null; PaletteStrip.ItemsSource = null; LogoGroups.ItemsSource = null; HeaderLogo.Source = null; return; }
        ShowLogos(team);
        // a picture is one team's rink, so it doesn't stay for the next team
        IceEditor.SetTarget(team, HasIceLogo, HasIceLine, _logos.CenterLogoNumber(team), TeamLogos.ArtSharers(_teams, team), TeamLogos.LineSharers(_teams, team),
            WriteCenterIceAsync, $"Choose the rink picture you downloaded for {team.Abbr}, or drop it here");

        var teamPicks = new[] { new PickItem("None", -1) }
            .Concat(_teams.OrderBy(t => t.Name).Select(t => new PickItem($"{t.Name} ({t.Abbr})", t.Index))).ToList();
        var arenaPicks = _arenas.OrderBy(a => a.Name).Select(a => new PickItem(string.IsNullOrWhiteSpace(a.City) ? a.Name : $"{a.Name} — {a.City}", a.Index)).ToList();
        int arenaRow = TeamEditor.RowFor(_db, team, TeamEditor.Arenas, team.ArenaId);
        var arena = _arenas.FirstOrDefault(a => a.Row == arenaRow);
        var sharing = TeamEditor.TeamsAt(_teams, team.ArenaId).Where(t => t.Row != team.Row).ToList();

        var sections = new List<TeamSection>();
        var palette = TeamEditor.PaletteRows(_db, team.Index)
            .Select(p => MakeRow(new TeamField("Team palette", TeamEditor.Palette, "color", $"Colour {p.ColorId + 1}", TeamFieldKind.Color), p.Row, p.Color, null))
            .ToList();
        if (palette.Count > 0)
            sections.Add(new TeamSection { Title = "Team palette", Caption = "The team's colours as the game lists them (stockteamcolorstable)", Rows = palette });

        foreach (var group in TeamEditor.Fields.GroupBy(f => f.Section))
        {
            var rows = new List<TeamValueRow>();
            foreach (var f in group)
            {
                int row = f.Table == TeamEditor.Arenas ? arenaRow : team.Row;
                if (row < 0) continue;
                var t = _db.Table(f.Table);
                var field = t.Fields.FirstOrDefault(x => x.Name == f.Field);
                if (field == null) continue;
                object value = _db.Get(t, row, field);
                if (value is float fl) value = (int)fl;
                var picks = f.Kind == TeamFieldKind.Team ? teamPicks.ToList() : f.Kind == TeamFieldKind.Arena ? arenaPicks.ToList() : null;
                rows.Add(MakeRow(f, row, value, picks));
            }
            if (rows.Count == 0) continue;
            sections.Add(new TeamSection { Title = group.Key, Caption = Caption(group.Key, arena, sharing), Rows = rows });
        }

        Sections.ItemsSource = sections;
        PaletteStrip.ItemsSource = palette.Count > 0 ? palette : sections.FirstOrDefault(s => s.Title == "Menu colours")?.Rows;
        TeamTitle.Text = string.IsNullOrWhiteSpace(team.Name) ? $"Team {team.Index}" : team.Name;
        TeamMeta.Text = $"{team.League}  ·  {team.Abbr}  ·  team #{team.Index}  ·  row {team.Row}" + (team.Active ? "" : "  ·  inactive") +
                        (arena != null ? $"\nHome: {arena.Name}" + (string.IsNullOrWhiteSpace(arena.City) ? "" : $", {arena.City}") : "");
    }

    /// <summary>The team's logo tiles; thumbnails are read from the game in the background (the newest team wins).</summary>
    private void ShowLogos(TeamSummary team)
    {
        var logos = _logos?.For(team) ?? new List<TeamLogo>();
        _logoRows = logos.Select(l => new TeamLogoRow { Logo = l }).ToList();
        LogoGroups.ItemsSource = _logoRows.GroupBy(r => r.Logo.Group).Select(g => new TeamLogoGroup { Title = g.Key, Logos = g.ToList() }).ToList();
        LogosCard.Visibility = _logoRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var artSharers = TeamLogos.ArtSharers(_teams, team);
        var lineSharers = TeamLogos.LineSharers(_teams, team);
        LogosCaption.Text = $"{_logoRows.Count} logo textures, art #{team.ArtId}, read from the game. Replace writes a texture into the game (the Mods page restores it)." +
                            (artSharers.Count > 0 ? $"\nThese logos also show for {TeamLogos.Names(artSharers)}." : "") +
                            (lineSharers.Count > 0 && _logoRows.Any(r => r.Logo.Style == "centerline") ? $"\nThe centre line (#{team.CenterLine}) also shows for {TeamLogos.Names(lineSharers)}." : "");
        ReplaceMainButton.IsEnabled = _logoRows.Any(r => r.Logo.Main);
        HeaderLogo.Source = null;
        _ = LoadThumbsAsync(++_logoLoad);
    }

    private async Task LoadThumbsAsync(int load)
    {
        foreach (var row in _logoRows.ToList())
        {
            if (load != _logoLoad) return;
            // logos are colour with real transparency (their names don't say _color, so decode RGBA here)
            System.Windows.Media.Imaging.BitmapSource bmp = null;
            try
            {
                var t = await _textures.LoadSizedAsync(row.Logo.Name, 128);
                if (t.Rgba != null) bmp = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, Channel.Rgba);
                row.Size = $"{t.Header.Width}×{t.Header.Height}";
            }
            catch { }
            if (load != _logoLoad) return;
            row.Thumb = bmp;
            if (row.Logo.Style == "xl" || (HeaderLogo.Source == null && row.Logo.Main)) HeaderLogo.Source = bmp;
        }
    }

    private void OnExportLogo(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TeamLogoRow row || _team == null) return;
        string folder = LogoFolder();
        var dlg = new SaveFileDialog
        {
            Title = $"Export {row.Label}",
            Filter = "PNG image|*.png|DDS, as stored in the game (BC compressed)|*.dds",
            InitialDirectory = folder,
            FileName = $"{_team.Abbr}_{row.Logo.Style.Replace('/', '_')}.png",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true) _ = ExportLogoAsync(row, dlg.FileName);
    }

    private string LogoFolder()
    {
        string folder = Path.Combine(KitPaths.Export, "teams", string.IsNullOrWhiteSpace(_team.Abbr) ? $"team{_team.Index}" : _team.Abbr);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private async Task<bool> ExportLogoAsync(TeamLogoRow row, string file)
    {
        try
        {
            var index = _mods.Index;
            bool dds = file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
            await Task.Run(() =>
            {
                var t = Texture.Load(index, row.Logo.Name);
                if (t.Rgba == null) throw new InvalidDataException(t.Problem ?? "it couldn't be decoded");
                File.WriteAllBytes(file, dds ? t.ToDds() : t.ToPng());
            });
            StatusChanged?.Invoke($"Exported {row.Label} to {file}");
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Can't export {row.Label}: {ex.Message}");
            return false;
        }
    }

    private async void OnExportLogos(object sender, RoutedEventArgs e) => await ExportLogos(null);

    /// <summary>Saves every logo of the team as PNG into <paramref name="folder"/> (default export/teams/ABBR).</summary>
    public async Task ExportLogos(string folder)
    {
        if (_team == null) return;
        folder ??= LogoFolder();
        Directory.CreateDirectory(folder);
        int n = 0;
        foreach (var row in _logoRows.ToList())
            if (await ExportLogoAsync(row, Path.Combine(folder, $"{_team.Abbr}_{row.Logo.Style.Replace('/', '_')}.png"))) n++;
        StatusChanged?.Invoke($"Exported {n} of {_logoRows.Count} logos to {folder}");
    }

    private void OnReplaceLogo(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TeamLogoRow row) return;
        if (row.Logo.Style == "centerline" && TeamLogos.LineSharers(_teams, _team).Where(t => t.ArtId != _team.ArtId).ToList() is { Count: > 0 } others &&
            !Confirm($"This centre line is also used by {others.Count} other team(s): {TeamLogos.Names(others)}. Replacing it changes theirs too. Continue?")) return;
        if (PickImage($"Replace {row.Label}") is { } file) _ = ReplaceLogosAsync(new[] { row }, file);
    }

    private void OnReplaceMain(object sender, RoutedEventArgs e)
    {
        if (PickImage("Replace the logo at every size") is { } file) _ = ReplaceLogosAsync(_logoRows.Where(r => r.Logo.Main).ToList(), file);
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        var rows = _logoRows.Where(r => r.Logo.Group != "Rink").ToList();
        if (!Confirm($"Write one image into all {rows.Count} menu logo styles of {_team?.Name}? The stylized, mono and glossy versions lose their look and show your image as it is.")) return;
        if (PickImage("Replace every logo style") is { } file) _ = ReplaceLogosAsync(rows, file);
    }

    private string PickImage(string title)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = ImageLoader.Filter };
        return dlg.ShowDialog(Window.GetWindow(this)) == true ? dlg.FileName : null;
    }

    /// <summary>Replaces logos (scripted runs): which = main, all, or a style such as xl or centerice.</summary>
    public Task ReplaceLogos(string which, string file) => ReplaceLogosAsync(which switch
    {
        "main" => _logoRows.Where(r => r.Logo.Main).ToList(),
        "all" => _logoRows.Where(r => r.Logo.Group != "Rink").ToList(),
        _ => _logoRows.Where(r => r.Logo.Style.Equals(which, StringComparison.OrdinalIgnoreCase)).ToList(),
    }, file);

    /// <summary>Encodes the image for each logo texture (resized to its size) and writes it into the game, one mod entry per texture.</summary>
    private async Task<string> ReplaceLogosAsync(IReadOnlyList<TeamLogoRow> rows, string file)
    {
        if (rows.Count == 0 || _busy || _mods == null) return "Nothing to replace (or busy).";
        if (ModService.GameRunning)
        {
            StatusChanged?.Invoke("Close KytyPS5 before replacing logos: the game has its files open.");
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return "KytyPS5 is running: close the game first.";
        }
        _busy = true;
        SetButtons();
        var failed = new List<string>();
        var shared = new List<string>();
        int done = 0, alsoChanged = 0;
        foreach (var row in rows) row.Busy = true;
        foreach (var row in rows)
        {
            StatusChanged?.Invoke($"Writing {Path.GetFileName(file)} into {_team?.Abbr} {row.Label} ({done + 1} of {rows.Count})…");
            try
            {
                await _mods.ResetAsync("texture", row.Logo.Name);
                var notes = new List<string>();
                // a logo's image is often stored once with other textures of the same team (its player-banner emblem):
                // those take the new image too
                var plan = await _mods.PlanTextureAsync(row.Logo.Name, file, n => notes.Add(n), takeShared: true);
                alsoChanged += plan.Chunk.SharedChunks.Count;
                var h = plan.Header;
                notes.AddRange(plan.Chunk.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
                var entry = _mods.Queue("texture", row.Logo.Name, file, $"{_team?.Abbr} {row.Label}: {h.Format.Name} {h.Width}×{h.Height}", plan.Chunk.Patches, notes);
                await _mods.ApplyAsync(entry);
                done++;
            }
            catch (InvalidDataException ex) when (ex.Message.Contains("also used by"))
            {
                // its bytes are stored once for several textures: rewriting them would change the others too
                shared.Add(row.Label);
            }
            catch (Exception ex)
            {
                failed.Add($"{row.Label}: {ex.Message}");
            }
            row.Busy = false;
        }
        _busy = false;
        SetButtons();
        if (_team != null) ShowLogos(_team);
        string msg = $"{done} of {rows.Count} logo(s) replaced in the game." +
                     (shared.Count > 0 ? $" Left alone ({string.Join(", ", shared)}): the game stores that image once for several textures, so it can't be changed by itself." : "") +
                     (alsoChanged > 0 ? $" {alsoChanged} other texture(s) stored the same image (such as the player-banner emblem) and changed with it." : "") +
                     (failed.Count > 0 ? " Failed: " + string.Join("; ", failed.Take(3)) : done > 0 ? " The Mods page can restore them." : "");
        StatusChanged?.Invoke(msg);
        if (failed.Count > 0 && !App.Args.ContainsKey("shot"))
            MessageBox.Show(Window.GetWindow(this), string.Join("\n", failed), "Some logos weren't replaced", MessageBoxButton.OK, MessageBoxImage.Warning);
        return msg;
    }

    private bool HasIceLogo => _logoRows.Any(r => r.Logo.Style == "centerice");
    private bool HasIceLine => _logoRows.Any(r => r.Logo.Style == "centerline");

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (Editor == null || CenterIceTab == null) return;
        bool ice = TabCenterIce.IsChecked == true;
        Editor.Visibility = ice ? Visibility.Collapsed : Visibility.Visible;
        CenterIceTab.Visibility = ice ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Shows the Centre ice tab and, when given, converts a picture (scripted runs).</summary>
    public async Task ShowCenterIce(string file)
    {
        TabCenterIce.IsChecked = true;
        if (file != null) await OpenIcePicture(file);
    }

    private void OnOpenFaceoffSite(object sender, RoutedEventArgs e)
    {
        string url = $"{FaceoffNet.Site}/ice-{FaceoffNet.LeagueKey(_team?.League)}";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { StatusChanged?.Invoke($"Can't open {url}: {ex.Message}"); }
    }

    private async void OnChoosePicture(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = $"The rink picture for {_team?.Name}",
            Filter = "Rink pictures|*.jpg;*.jpeg;*.png;*.webp;*.avif;*.bmp;*.tif;*.tiff|All files|*.*",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true) await OpenIcePicture(dlg.FileName);
    }

    private void OnPictureDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPictureDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) await OpenIcePicture(files[0]);
    }

    private Task OpenIcePicture(string file) => IceEditor.OpenPicture(IceEditor.Begin("Reading the picture…"), file, Path.GetFileNameWithoutExtension(file), Path.GetFileName(file));

    private void OnFaceoff(object sender, RoutedEventArgs e) => OpenFaceoff();

    /// <summary>Opens the TheFaceoff.net centre-ice converter for the shown team.</summary>
    public FaceoffWindow OpenFaceoff()
    {
        if (_team == null) return null;
        var team = _team;
        // the window stays open while the list moves on: it only writes into the team it was opened for
        Task<string> Write(string logo, string line) => _team?.Row == team.Row
            ? WriteCenterIceAsync(logo, line)
            : Task.FromResult($"{team.Abbr} is no longer the shown team: pick it in the list to write.");
        var w = new FaceoffWindow(team, HasIceLogo, HasIceLine, _logos.CenterLogoNumber(team), TeamLogos.ArtSharers(_teams, team), TeamLogos.LineSharers(_teams, team), Write) { Owner = Window.GetWindow(this) };
        w.Show();
        return w;
    }

    /// <summary>Writes a converted centre logo (and line, when given) into the shown team's centre-ice textures.</summary>
    public async Task<string> WriteCenterIceAsync(string logo, string line)
    {
        var lines = _logoRows.Where(r => r.Logo.Style == "centerline").ToList();
        var logos = _logoRows.Where(r => r.Logo.Style == "centerice").ToList();
        if (logos.Count == 0) return "This team has no centre ice texture.";
        string a = await ReplaceLogosAsync(logos, logo);
        string b = line != null && lines.Count > 0 ? await ReplaceLogosAsync(lines, line) : null;
        return b == null ? $"Centre logo: {a}" : $"Centre logo: {a}  Centre line: {b}";
    }

    private static string Caption(string section, ArenaSummary arena, List<TeamSummary> sharing) => section switch
    {
        "Identity" => "Names the menus, scorebug and commentary text use",
        "Menu colours" => "Front-end colours: team cards, menus and selection screens",
        "Broadcast colours" => "In-game presentation: scorebug, banners and overlays",
        "Rink colours" => "Colours the arena takes from the home team",
        "Home arena" => arena == null ? "This team has no arena row" :
            "The arena fields edit " + arena.Name + (sharing.Count == 0 ? "" :
                $" — shared with {string.Join(", ", sharing.Take(4).Select(t => t.Abbr))}{(sharing.Count > 4 ? $" and {sharing.Count - 4} more" : "")}, so they change too"),
        "More" => "Rivalries and the farm team",
        _ => null,
    };

    private TeamValueRow MakeRow(TeamField f, int row, object value, List<PickItem> picks)
    {
        var r = new TeamValueRow(f, row, value, TeamEditor.MaxBytes(_db, f.Table, f.Field), picks, Edited);
        if (_edits.TryGetValue((f.Table, row, f.Field), out var pending)) r.Restore(pending.Value);
        return r;
    }

    /// <summary>A value was edited: kept (or dropped when it's back to the game's value) until Save.</summary>
    private void Edited(TeamValueRow r)
    {
        var key = (r.Field.Table, r.Row, r.Field.Field);
        if (r.IsChanged && !r.HasError) _edits[key] = (r.Value, r.Field.Label, _team?.Abbr ?? "", r.IsColor);
        else _edits.Remove(key);
        SetButtons();
    }

    private void SetButtons()
    {
        int n = _edits.Count;
        SaveButton.IsEnabled = DiscardButton.IsEnabled = !_busy && n > 0;
        ReloadButton.IsEnabled = !_busy;
        ReplaceMainButton.IsEnabled = ReplaceAllButton.IsEnabled = !_busy;
        SaveLabel.Text = n > 0 ? $"Save {n} to game" : "Save to game";
        SaveButton.ToolTip = n > 0 ? $"Write {n} edited value(s) into nhl_game.db" : null;
    }

    private void OnResetValue(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TeamValueRow r) r.Reset();
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        if (_edits.Count > 0 && !Confirm($"Drop {_edits.Count} unsaved edit(s)?")) return;
        await LoadAsync();
    }

    private async void OnDiscard(object sender, RoutedEventArgs e) => await LoadAsync();

    private bool Confirm(string text) => App.Args.ContainsKey("shot") ||
        MessageBox.Show(Window.GetWindow(this), text, "HMS", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveAsync();

    /// <summary>Writes the edits into the game (into a fresh read of the database), then reads it back.</summary>
    public async Task SaveAsync()
    {
        if (_edits.Count == 0 || _busy) return;
        if (ModService.GameRunning)
        {
            StatusChanged?.Invoke("Close KytyPS5 before saving: the game has its files open.");
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game before changing its files.", "Can't save to the game", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _busy = true;
        SetButtons();
        var edits = _edits.ToList();
        StatusChanged?.Invoke($"Writing {edits.Count} team value(s) into nhl_game.db…");
        try
        {
            await _mods.SaveDbAsync(TeamEditor.Database, db =>
            {
                foreach (var ((table, row, field), edit) in edits)
                {
                    var t = db.Table(table);
                    db.Set(t, row, t.Fields.First(f => f.Name == field), edit.Value);
                }
                var parts = edits.Select(x => $"{x.Value.Team} {x.Value.Label} = {(x.Value.Color ? TeamEditor.Hex((int)x.Value.Value) : Show(x.Value.Value))}").ToList();
                return "Teams: " + (parts.Count <= 6 ? string.Join(", ", parts) : string.Join(", ", parts.Take(6)) + $" and {parts.Count - 6} more");
            }, "Teams page");
            _edits.Clear();
            StatusChanged?.Invoke($"{edits.Count} team value(s) written into the game. The Mods page can restore the original.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't save: " + ex.Message);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't save to the game", MessageBoxButton.OK, MessageBoxImage.Warning);
            _busy = false;
            SetButtons();
            return;
        }
        _busy = false;
        await LoadAsync();
    }

    private static string Show(object v) => v is string s ? $"\"{s}\"" : v?.ToString();
}
