using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// The roster (nhl_game.db) and language databases (loc/*.db). Nothing is kept between reads: the page reads the
/// database from the game files when it opens, on Reload, after every save, and again for each player or string you
/// select, so what it shows is always what the game has, whoever changed it.
/// </summary>
public partial class RosterPage : UserControl
{
    private const string DefaultLanguage = "loc/eng_us.db";

    private ModService _mods;
    private TextureService _textures;
    private List<PlayerRow> _players = new();
    private List<RosterTeam> _teams = new();
    private List<LocRow> _strings = new();
    private RosterPlayer _current;
    private List<FieldEdit> _fields = new();
    private PositionEdit _positionEdit;
    private PickEdit _draftTeamPick;
    private FieldEdit _draftTeamField, _draftRoundField, _draftPickField;
    private List<ClauseEdit> _clauses = new();
    private LocRow _currentText;
    private string _currentTextOriginal;
    private T3Db _locDb;
    private string _language = DefaultLanguage;
    private List<XFactorAbility> _xfAbilities = new();
    private string[] _tierNames = { "Specialist", "All-Star", "Elite" };
    private int _version, _textVersion;
    private bool _busy, _loaded, _suppress;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };

    public event Action<string> StatusChanged;

    public RosterPage()
    {
        InitializeComponent();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyFilter(); };
    }

    private bool TextMode => ModeText.IsChecked == true;
    private bool _syncMode;
    private bool SyncMode => _syncMode;
    private bool LinesMode => ModeLines.IsChecked == true;
    private bool PicksMode => ModePicks.IsChecked == true;

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _textures = new TextureService(mods.Index);
        SyncView.SetMods(mods);
        LinesView.SetMods(mods);
        PicksView.SetMods(mods);
        LinesView.StatusChanged += text => StatusChanged?.Invoke(text);
        SyncView.StatusChanged += text => StatusChanged?.Invoke(text);
        PicksView.StatusChanged += text => StatusChanged?.Invoke(text);
        Summary.Text = "Open the page to read the roster from the game.";
        if (IsVisible) _ = ReloadAsync();
    }

    /// <summary>Called when the page is shown: always reads the database again.</summary>
    public Task Shown() => _mods == null ? Task.CompletedTask : ReloadAsync();

    public void SetSearch(string text) => Search.Text = text;

    public void SetTextMode() => ModeText.IsChecked = true;

    /// <summary>Opens the NHL sync and fetches (scripted runs), then shows one kind of change.</summary>
    public async Task SyncAsync(string kind)
    {
        _syncMode = true;
        ModePlayers.IsChecked = false;
        ModeText.IsChecked = false;
        ModeLines.IsChecked = false;
        ModePicks.IsChecked = false;
        await ApplyModeAsync();
        await SyncView.FetchAsync();
        SyncView.ShowKind(kind);
    }

    /// <summary>Opens the line editor on a team (by index), optionally puts players in spots and saves (scripted runs).</summary>
    public async Task LinesAsync(int team, string picks, bool auto, bool save)
    {
        ModeLines.IsChecked = true;
        await Task.Delay(300);
        await LinesView.ShownAsync(team);
        await Task.Delay(1500);
        if (auto) await LinesView.AutoBuildAsync();
        foreach (var p in (picks ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)) LinesView.Pick(p);
        if (save) await LinesView.SaveAsync();
    }

    /// <summary>Applies the ticked sync changes (scripted runs).</summary>
    public Task SyncApplyAsync() => SyncView.ApplyAsync();

    public void SelectIndex(int n)
    {
        var list = TextMode ? TextList : PlayerList;
        if (list.Items.Count > n) { list.SelectedIndex = n; list.ScrollIntoView(list.SelectedItem); }
    }

    public async Task ReloadAsync()
    {
        if (_mods == null) return;
        try
        {
            if (SyncMode) return;
            if (LinesMode) { await LinesView.ShownAsync(); return; }
            if (PicksMode) { await PicksView.ShownAsync(); return; }
            if (TextMode) await LoadLanguageAsync();
            else await LoadRosterAsync();
        }
        catch (Exception ex)
        {
            Summary.Text = "Couldn't read the database: " + ex.Message;
            StatusChanged?.Invoke(Summary.Text);
        }
    }

    // ================================================================ players

    private async Task LoadRosterAsync()
    {
        Summary.Text = "Reading nhl_game.db from the game…";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var db = await _mods.OpenDbAsync(Roster.Database);
        var roster = await Task.Run(() => new Roster(db));
        // X-Factor names and tier names, from the game's English text
        try
        {
            var loc = await _mods.OpenDbAsync(DefaultLanguage);
            (_xfAbilities, _tierNames) = await Task.Run(() => (XFactors.Abilities(loc), XFactors.TierNames(loc)));
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException) { }
        _players = roster.Players.Where(p => p.RatingsTable != null && p.Name.Length > 0)
            .OrderByDescending(p => p.Overall).ThenBy(p => p.LastName).Select(p => new PlayerRow(p)).ToList();
        _teams = roster.Teams.Values.Where(t => !string.IsNullOrEmpty(t.Name)).OrderBy(t => t.Name).ToList();

        _suppress = true;
        var keep = (TeamFilter.SelectedItem as ComboBoxItem)?.Tag;
        var items = new List<ComboBoxItem> { new() { Content = "All teams", Tag = -1 } };
        items.AddRange(_teams.Select(t => new ComboBoxItem { Content = $"{t.Name}  ({t.Abbr})", Tag = t.Index }));
        TeamFilter.ItemsSource = items;
        TeamFilter.SelectedItem = items.FirstOrDefault(i => Equals(i.Tag, keep)) ?? items[0];
        _suppress = false;

        var (ok, total) = db.CheckCrcs();
        var entry = _mods.Find(DbEditor.Kind, Roster.Database);
        string edits = entry == null ? "no edits" : _mods.State(entry) == ModState.Applied ? "your edits are in the game" : "your edits are restored (not in the game)";
        Summary.Text = $"nhl_game.db  ·  read from the game at {DateTime.Now:HH:mm:ss} in {sw.ElapsedMilliseconds} ms  ·  {_players.Count:N0} players, {_teams.Count} teams  ·  checksums {ok}/{total}  ·  {edits}";
        _loaded = true;
        int keepId = _current?.GameId ?? -1;
        ApplyFilter();
        if (keepId >= 0 && PlayerList.ItemsSource is List<PlayerRow> rows && rows.FirstOrDefault(r => r.Player.GameId == keepId) is { } again)
        {
            _suppress = true;
            PlayerList.SelectedItem = again;
            _suppress = false;
            await LoadPlayerAsync(keepId);
        }
    }

    private void ApplyFilter()
    {
        if (TextMode) { ApplyTextFilter(); return; }
        string q = Search.Text.Trim().ToLowerInvariant();
        int team = (TeamFilter.SelectedItem as ComboBoxItem)?.Tag is int t ? t : -1;
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = _players.Where(r => (team < 0 || r.Player.Team == team) && terms.All(r.Search.Contains)).ToList();
        PlayerList.ItemsSource = rows;
        ListCount.Text = $"{rows.Count:N0}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnTeamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppress && _loaded) ApplyFilter();
    }

    private async void OnPlayerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || PlayerList.SelectedItem is not PlayerRow row) return;
        try { await LoadPlayerAsync(row.Player.GameId); }
        catch (Exception ex) { StatusChanged?.Invoke("Couldn't read the player: " + ex.Message); }
    }

    /// <summary>Reads the database from the game again and shows this player as it has him.</summary>
    private async Task LoadPlayerAsync(int gameId)
    {
        int version = ++_version;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var roster = await Task.Run(() => new Roster(db));
        if (version != _version) return;
        var p = roster.Find(gameId);
        if (p == null) { Editor.Visibility = Visibility.Collapsed; EditorEmpty.Visibility = Visibility.Visible; return; }
        _current = p;

        var bio = db.Table(Roster.BioTable);
        var identity = new List<FieldEdit>();
        void Add(List<FieldEdit> list, T3Table t, int row, string name, int offset = 0, bool rating = false, int scale = 1, int? minValue = null, int? maxValue = null)
        {
            if (row >= 0 && Roster.Field(t, name) is { } f) list.Add(new FieldEdit(db, t, row, f, offset, scale, minValue, maxValue) { IsRating = rating });
        }
        Add(identity, bio, p.BioRow, "firstname");
        Add(identity, bio, p.BioRow, "lastname");
        Add(identity, bio, p.BioRow, "nickname");
        Add(identity, bio, p.BioRow, "birthplace");
        Add(identity, db.Table(Roster.RosterTable), p.RosterRow, "jersey");
        Add(identity, bio, p.BioRow, "height", 54);
        Add(identity, bio, p.BioRow, "weight", 120);

        var ratings = new List<FieldEdit>();
        Add(ratings, p.RatingsTable, p.RatingsRow, "skill_overall", rating: true);
        foreach (var f in p.RatingsTable.Fields.Where(f => f.Name.StartsWith("c_") && f.Name != "c_overall" && f.Type == T3FieldType.Integer && f.Depth >= 7)
                                               .OrderBy(f => RosterVisuals.Label(f.Name)))
            Add(ratings, p.RatingsTable, p.RatingsRow, f.Name, rating: true);

        // X-Factors: three slots of ability and tier in the ratings row
        var xfFields = new List<FieldEdit>();
        var slots = new List<XFactorSlot>();
        for (int i = 1; i <= XFactors.Slots; i++)
        {
            var a = Roster.Field(p.RatingsTable, $"xfactor{i}ability");
            var t = Roster.Field(p.RatingsTable, $"xfactor{i}tier");
            if (a == null || t == null || p.RatingsRow < 0) continue;
            var af = new FieldEdit(db, p.RatingsTable, p.RatingsRow, a);
            var tf = new FieldEdit(db, p.RatingsTable, p.RatingsRow, t);
            xfFields.Add(af);
            xfFields.Add(tf);
            var slot = new XFactorSlot(i, af, tf, _xfAbilities, _tierNames, p.IsGoalie);
            slot.IconNeeded += s => _ = LoadXFactorIcon(s, version);
            slots.Add(slot);
        }
        XFactorList.ItemsSource = slots;
        XFactorBadges.ItemsSource = slots.Where(s => !s.IsEmpty).ToList();
        XFactorSection.Visibility = slots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        int have = slots.Count(s => !s.IsEmpty);
        XFactorTitle.Text = have == 0 ? "X-FACTORS  ·  NONE" : $"X-FACTORS  ·  {have}";
        foreach (var s in slots) _ = LoadXFactorIcon(s, version);

        // Position: primary + secondary (+ the legacy position field, kept in sync by the PositionEdit)
        var posFields = new List<FieldEdit>();
        Add(posFields, bio, p.BioRow, "positionprimary", minValue: 0, maxValue: 5);
        Add(posFields, bio, p.BioRow, "positionsecondary", minValue: -1, maxValue: 5);
        Add(posFields, bio, p.BioRow, "position", minValue: 0, maxValue: 4);
        _positionEdit = posFields.Count == 3 ? new PositionEdit(posFields[0], posFields[1], posFields[2]) : null;
        PositionList.ItemsSource = _positionEdit == null ? null : new[] { _positionEdit };
        PositionSection.Visibility = _positionEdit == null ? Visibility.Collapsed : Visibility.Visible;

        // Draft: year (stored as year-1900), round, pick, and the team that drafted him
        var draftFields = new List<FieldEdit>();
        Add(draftFields, bio, p.BioRow, "draftyear", 1900);
        Add(draftFields, bio, p.BioRow, "draftround", minValue: 0); // the game's older players go past round 7
        Add(draftFields, bio, p.BioRow, "draftposition", minValue: 0);
        _draftRoundField = draftFields.FirstOrDefault(f => f.Field == "draftround");
        _draftPickField = draftFields.FirstOrDefault(f => f.Field == "draftposition");
        var draftTeamFields = new List<FieldEdit>();
        Add(draftTeamFields, bio, p.BioRow, "draftteam", minValue: -1);
        _draftTeamField = draftTeamFields.FirstOrDefault();
        _draftTeamPick = null;
        if (_draftTeamField != null)
        {
            var picks = new List<PickItem> { new("Undrafted", -1) };
            picks.AddRange(_teams.Select(t => new PickItem($"{t.Name}  ({t.Abbr})", t.Index)));
            int v = int.TryParse(_draftTeamField.Text, out int tv) ? tv : -1;
            if (v != -1 && picks.All(x => x.Value != v)) picks.Insert(0, new PickItem($"#{v}", v));
            _draftTeamPick = new PickEdit(_draftTeamField, picks);
        }
        DraftFields.ItemsSource = draftFields;
        DraftTeamList.ItemsSource = _draftTeamPick == null ? null : new[] { _draftTeamPick };
        DraftSection.Visibility = draftFields.Count == 0 && _draftTeamPick == null ? Visibility.Collapsed : Visibility.Visible;

        // Contract: salary in dollars (stored in $5,000 units), years left, the type flags, and the extension
        var contractFields = new List<FieldEdit>();
        Add(contractFields, bio, p.BioRow, "contractdollars", scale: 5000, minValue: 0);
        Add(contractFields, bio, p.BioRow, "contractlength", minValue: 0);
        Add(contractFields, bio, p.BioRow, "extensionlength", minValue: 0);
        Add(contractFields, bio, p.BioRow, "extensiondollars", scale: 5000, minValue: 0);
        ContractFields.ItemsSource = contractFields;

        var toggleFields = new List<FieldEdit>();
        Add(toggleFields, bio, p.BioRow, "contractis2way", minValue: 0, maxValue: 1);
        Add(toggleFields, bio, p.BioRow, "contractismultiyear", minValue: 0, maxValue: 1);
        Add(toggleFields, bio, p.BioRow, "contractneedsentrylevel", minValue: 0, maxValue: 1);
        Add(toggleFields, bio, p.BioRow, "extensionis2way", minValue: 0, maxValue: 1);
        ContractToggles.ItemsSource = toggleFields.Select(f => new ToggleEdit(f)).ToList();

        // Trade protection: one clause type + team list per season, from this season through the contract and extension
        var clauseFields = new List<FieldEdit>();
        for (int i = 0; i < 10; i++)
        {
            Add(clauseFields, bio, p.BioRow, $"contractclause{i}", minValue: 0, maxValue: 4);
            Add(clauseFields, bio, p.BioRow, $"contractclausemntcnumteams{i}", minValue: 0, maxValue: 32);
        }
        _clauses = new List<ClauseEdit>();
        for (int i = 0; i < 10; i++)
        {
            var c = clauseFields.FirstOrDefault(f => f.Field == $"contractclause{i}");
            var m = clauseFields.FirstOrDefault(f => f.Field == $"contractclausemntcnumteams{i}");
            if (c != null && m != null) _clauses.Add(new ClauseEdit(i + 1, c, m));
        }
        ClauseList.ItemsSource = _clauses;
        ClauseSection.Visibility = _clauses.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // contract status is shown, not edited: 0 = under contract, -1 = no NHL contract
        var statusField = Roster.Field(bio, "contractstatus");
        if (statusField == null) ContractStatusText.Visibility = Visibility.Collapsed;
        else
        {
            ContractStatusText.Visibility = Visibility.Visible;
            int status = (int)db.Get(bio, p.BioRow, statusField);
            ContractStatusText.Text = status == 0 ? "Under contract" : status == -1 ? "No NHL contract" : $"Contract status {status}";
        }

        // Retained salary: shown, never edited. The bio says whether the player is retained and what his
        // current team pays; the full salary, the retained share and the retaining team are worked out from
        // the teams' yearly retained totals (RetainedSalary) and shown only when they point to exactly one
        // team — otherwise "—", never a guess. These are plain display values, not FieldEdits, so they never
        // count as changes and never get written. The totals are read only for a retained player.
        int BioInt(string name) => Roster.Field(bio, name) is { } f ? (int)db.Get(bio, p.BioRow, f) : 0;
        int retainedTrades = BioInt("retainedsalarytrades");
        int retainedLength = BioInt("contractlength");
        long retainedHit = (long)BioInt("contractdollars") * 5000;
        var retained = retainedTrades >= 1
            ? RetainedSalary.Attribute(db, retainedTrades, retainedLength, retainedHit, p.Team)
            : null;
        RetainedSection.Visibility = retainedTrades >= 1 ? Visibility.Visible : Visibility.Collapsed;
        RetainedFields.ItemsSource = retainedTrades >= 1
            ? new[]
            {
                new ReadOnlyField("Full salary", retained?.FullDollars is { } full ? RetainedMoney(full) : "—"),
                new ReadOnlyField("Retained", retained?.RetainedDollars is { } dollars && retained.Percent is { } pct
                    ? $"{RetainedPercent(pct)} — {RetainedMoney(dollars)}" : "—"),
                new ReadOnlyField("Retained by", retained?.RetainingTeam is { } retainingTeam && roster.Teams.TryGetValue(retainingTeam, out var tm) ? tm.Name : "—"),
            }
            : null;

        ContractSection.Visibility = contractFields.Count == 0 && toggleFields.Count == 0 && _clauses.Count == 0 && retainedTrades < 1
            ? Visibility.Collapsed : Visibility.Visible;

        foreach (var f in _fields) f.PropertyChanged -= OnFieldChanged;
        _fields = identity.Concat(ratings).Concat(xfFields).Concat(posFields).Concat(draftFields).Concat(draftTeamFields)
            .Concat(contractFields).Concat(toggleFields).Concat(clauseFields).ToList();
        foreach (var f in _fields) f.PropertyChanged += OnFieldChanged;
        IdentityFields.ItemsSource = identity;
        RatingFields.ItemsSource = ratings;
        RatingsTitle.Text = p.IsGoalie ? "GOALIE RATINGS" : "SKATER RATINGS";

        var team = roster.Teams.GetValueOrDefault(p.Team);
        var color = RosterVisuals.TeamColor(team?.PrimaryColor ?? 0);
        HeroBg.Background = new LinearGradientBrush(Color.FromArgb(255, (byte)(color.R * 0.85), (byte)(color.G * 0.85), (byte)(color.B * 0.85)),
                                                     Color.FromArgb(255, (byte)(color.R * 0.35), (byte)(color.G * 0.35), (byte)(color.B * 0.35)), 25);
        TeamText.Text = team?.Name ?? "Free agent";
        FirstText.Text = p.FirstName;
        LastText.Text = p.LastName.ToUpperInvariant();
        OverallText.Text = p.Overall.ToString();
        MetaText.Text = $"{p.PositionDisplay}{(p.Jersey >= 0 ? "  ·  #" + p.Jersey : "")}  ·  game id {p.GameId}";
        Editor.Visibility = Visibility.Visible;
        EditorEmpty.Visibility = Visibility.Collapsed;
        UpdateEditState($"Read from the game at {DateTime.Now:HH:mm:ss}.");

        // the portrait, decoded now from the game's texture
        Portrait.Source = null;
        NoPortrait.Visibility = Visibility.Collapsed;
        try
        {
            if (_mods.Index.GetRes(p.Portrait) == null) throw new InvalidOperationException();
            var tex = await _textures.LoadAsync(p.Portrait);
            if (version != _version) return;
            Portrait.Source = TextureService.ToBitmap(tex.Rgba, tex.Width, tex.Height, Channel.Rgba);
        }
        catch
        {
            if (version != _version) return;
            NoPortrait.Visibility = Visibility.Visible;
            NoPortraitText.Text = $"No portrait (p{p.ArtId})";
        }
    }

    /// <summary>Decodes a slot's icon (its ability at its tier) from the game's texture.</summary>
    private async Task LoadXFactorIcon(XFactorSlot slot, int version)
    {
        string name = slot.IconTexture;
        if (name == null) { slot.Icon = null; return; }
        try
        {
            var tex = await _textures.LoadAsync(name);
            if (version != _version || slot.IconTexture != name) return;
            slot.Icon = TextureService.ToBitmap(tex.Rgba, tex.Width, tex.Height, Channel.Rgba);
        }
        catch { if (version == _version) slot.Icon = null; }
        if (XFactorList.ItemsSource is List<XFactorSlot> all)
            XFactorBadges.ItemsSource = all.Where(s => !s.IsEmpty).ToList();
    }

    private void OnFieldChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FieldEdit.Text)) UpdateEditState(null);
    }

    private void UpdateEditState(string idle)
    {
        var changed = _fields.Where(f => f.IsChanged).ToList();
        string cross = CrossFieldError();
        bool errors = _fields.Any(f => f.HasError) || cross != null;
        SaveButton.IsEnabled = changed.Count > 0 && !errors && !_busy;
        DiscardButton.IsEnabled = changed.Count > 0 && !_busy;
        SaveLabel.Text = changed.Count > 0 ? $"Save {changed.Count} change{(changed.Count == 1 ? "" : "s")} to game" : "Save to game";
        EditState.Text = cross ?? (errors ? "Fix the values marked in red." : changed.Count > 0
            ? string.Join(",  ", changed.Take(4).Select(f => $"{f.Label} {f.Original} → {f.Text}")) + (changed.Count > 4 ? $"  +{changed.Count - 4}" : "")
            : idle ?? "No changes.");
    }

    /// <summary>Checks that span more than one field: draft consistency, a team list with no clause, and the
    /// skater/goalie guard. FieldEdit already covers the per-field ranges.</summary>
    private string CrossFieldError()
    {
        if (_current == null) return null;
        // only when the draft is being edited: the game's own data has players who wouldn't pass (a round but no team)
        if (_draftTeamField != null && _draftRoundField != null && _draftPickField != null
            && (_draftTeamField.IsChanged || _draftRoundField.IsChanged || _draftPickField.IsChanged)
            && !_draftTeamField.HasError && !_draftRoundField.HasError && !_draftPickField.HasError)
        {
            int team = int.Parse(_draftTeamField.Text, CultureInfo.InvariantCulture);
            int round = int.Parse(_draftRoundField.Text, CultureInfo.InvariantCulture);
            int pick = int.Parse(_draftPickField.Text, CultureInfo.InvariantCulture);
            if (team < 0 && (round != 0 || pick != 0)) return "Undrafted: set the draft round and pick to 0.";
            if (team >= 0 && (round < 1 || pick < 1)) return "Drafted: the draft round and pick must be at least 1.";
        }
        foreach (var c in _clauses)
            if (c.IsChanged && c.CrossError != null) return c.CrossError;
        if (_positionEdit != null && _positionEdit.IsChanged && (_positionEdit.PrimaryValue == 5) != _current.IsGoalie)
            return "A skater can't become a goalie (or the reverse) here: the ratings wouldn't match the position.";
        return null;
    }

    /// <summary>"$4.9M" for millions, "$615,000" below that — the retained display's money format.</summary>
    private static string RetainedMoney(long dollars) => dollars >= 1_000_000
        ? "$" + (dollars / 1_000_000.0).ToString("0.##", CultureInfo.InvariantCulture) + "M"
        : "$" + dollars.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"50%", "26.32%" — the retained display's percent format.</summary>
    private static string RetainedPercent(double pct) => pct.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private async void OnDiscard(object sender, RoutedEventArgs e)
    {
        if (_current != null) await LoadPlayerAsync(_current.GameId);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_current == null || _busy) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var changes = _fields.Where(f => f.IsChanged).ToList();
        if (changes.Count == 0 || changes.Any(f => f.HasError)) return;
        if (CrossFieldError() is { } cross)
        {
            MessageBox.Show(Window.GetWindow(this), cross, "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        int id = _current.GameId;
        string who = _current.Name;
        _busy = true;
        UpdateEditState(null);
        StatusChanged?.Invoke($"Writing {who} into the game…");
        try
        {
            await _mods.SaveDbAsync(Roster.Database, db =>
            {
                // the rows of a database read now; the player is found again by id
                var p = new Roster(db).Find(id) ?? throw new InvalidOperationException($"{who} is no longer in the database.");
                // positions are written as a set — primary, secondary and the legacy position — so the three never disagree
                var pos = changes.Where(c => c.Field is "positionprimary" or "positionsecondary" or "position").ToList();
                foreach (var c in changes.Except(pos))
                {
                    var t = db.Table(c.Table);
                    int row = c.Table == Roster.BioTable ? p.BioRow : c.Table == Roster.RosterTable ? p.RosterRow : p.RatingsRow;
                    db.Set(t, row, Roster.Field(t, c.Field), c.Value);
                    if (c.Field == "skill_overall" && Roster.Field(t, "c_overall") is { } co) db.Set(t, row, co, c.Value);
                }
                if (pos.Count > 0 && _positionEdit != null)
                {
                    var bio = db.Table(Roster.BioTable);
                    db.Set(bio, p.BioRow, Roster.Field(bio, "positionprimary"), _positionEdit.PrimaryValue);
                    db.Set(bio, p.BioRow, Roster.Field(bio, "positionsecondary"), _positionEdit.SecondaryValue);
                    db.Set(bio, p.BioRow, Roster.Field(bio, "position"), Roster.CollapsedPosition(_positionEdit.PrimaryValue));
                }
                return $"{who}: " + string.Join(", ", changes.Select(c => $"{c.Label} {c.Original} → {c.Text}"));
            });
            StatusChanged?.Invoke($"{who} saved into the game");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Save failed: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "HMS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
        }
        // read everything back from the game
        await LoadRosterAsync();
    }

    // ================================================================ text

    private async void OnModeChanged(object sender, RoutedEventArgs e)
    {
        // a tab in the bar was picked: that leaves sync mode
        _syncMode = false;
        await ApplyModeAsync();
    }

    /// <summary>Opens the NHL sync from the gold header button.</summary>
    private async void OnSyncClick(object sender, RoutedEventArgs e)
    {
        _syncMode = true;
        // no tab is active while the sync is open; picking a tab leaves it
        ModePlayers.IsChecked = false;
        ModeText.IsChecked = false;
        ModeLines.IsChecked = false;
        ModePicks.IsChecked = false;
        await ApplyModeAsync();
    }

    private async Task ApplyModeAsync()
    {
        if (PlayersView == null) return;
        PlayersView.Visibility = TextMode || SyncMode || LinesMode || PicksMode ? Visibility.Collapsed : Visibility.Visible;
        LinesView.Visibility = LinesMode ? Visibility.Visible : Visibility.Collapsed;
        TextView.Visibility = TextMode ? Visibility.Visible : Visibility.Collapsed;
        SyncView.Visibility = SyncMode ? Visibility.Visible : Visibility.Collapsed;
        PicksView.Visibility = PicksMode ? Visibility.Visible : Visibility.Collapsed;
        SearchRow.Visibility = SyncMode || LinesMode || PicksMode ? Visibility.Collapsed : Visibility.Visible;
        ReloadButton.Visibility = SyncMode ? Visibility.Collapsed : Visibility.Visible;
        SyncButton.Visibility = SyncMode ? Visibility.Collapsed : Visibility.Visible;
        if (LinesMode)
        {
            PageTitle.Text = "Lines";
            Summary.Text = "Every unit of a team: lines and D pairs, power play, penalty kill, 4-on-4, 3-on-3, shootout and goalies.";
            if (_mods != null) await LinesView.ShownAsync();
            return;
        }
        if (SyncMode)
        {
            PageTitle.Text = "NHL sync";
            Summary.Text = "Bring the game's rosters up to date with the NHL: trades, call-ups and numbers.";
            return;
        }
        if (PicksMode)
        {
            PageTitle.Text = "Draft Picks";
            Summary.Text = "Every team's future draft picks: who owns each one now, and where it came from.";
            if (_mods != null) await PicksView.ShownAsync();
            return;
        }
        TeamFilter.Visibility = TextMode ? Visibility.Collapsed : Visibility.Visible;
        LanguageFilter.Visibility = TextMode ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = TextMode ? "Text" : "Roster";
        Search.Tag = TextMode ? "Search text or string ids — e.g. Stanley Cup, TXT_BOTH" : "Search players — name, team, position, #jersey";
        if (_mods != null) await ReloadAsync();
    }

    private async Task LoadLanguageAsync()
    {
        if (LanguageFilter.ItemsSource == null)
        {
            var names = (await _mods.LegacyNamesAsync()).Where(n => n.StartsWith("loc/") && n.EndsWith(".db")).OrderBy(n => n).ToList();
            _suppress = true;
            LanguageFilter.ItemsSource = names;
            LanguageFilter.SelectedItem = names.Contains(_language) ? _language : names.FirstOrDefault();
            _suppress = false;
        }
        Summary.Text = $"Reading {_language} from the game…";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var db = await _mods.OpenDbAsync(_language);
        _strings = await Task.Run(() =>
        {
            var list = new List<LocRow>();
            foreach (var t in db.Tables)
            {
                var id = Roster.Field(t, "stringid");
                var text = Roster.Field(t, "sourcetext");
                if (id == null || text == null) continue;
                for (int r = 0; r < t.WrittenRecords; r++)
                    if (!db.IsInvalid(t, r))
                        list.Add(new LocRow { Table = t.Name, Row = r, Id = (string)db.Get(t, r, id), Text = (string)db.Get(t, r, text) });
            }
            return list;
        });
        var (ok, total) = db.CheckCrcs();
        var entry = _mods.Find(DbEditor.Kind, _language);
        string edits = entry == null ? "no edits" : _mods.State(entry) == ModState.Applied ? "your edits are in the game" : "your edits are restored (not in the game)";
        Summary.Text = $"{_language}  ·  read from the game at {DateTime.Now:HH:mm:ss} in {sw.ElapsedMilliseconds} ms  ·  {_strings.Count:N0} strings  ·  checksums {ok}/{total}  ·  {edits}";
        var keep = _currentText;
        ApplyTextFilter();
        if (keep != null && TextList.ItemsSource is List<LocRow> rows && rows.FirstOrDefault(r => r.Table == keep.Table && r.Row == keep.Row) is { } again)
        {
            _suppress = true;
            TextList.SelectedItem = again;
            _suppress = false;
            await LoadTextAsync(again);
        }
    }

    private void ApplyTextFilter()
    {
        string q = Search.Text.Trim();
        var rows = q.Length == 0 ? _strings
            : _strings.Where(r => r.Text.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        TextList.ItemsSource = rows;
        TextCount.Text = $"{rows.Count:N0}";
    }

    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || LanguageFilter.SelectedItem is not string lang) return;
        _language = lang;
        _currentText = null;
        TextEditor.Visibility = Visibility.Collapsed;
        TextEmpty.Visibility = Visibility.Visible;
        await ReloadAsync();
    }

    private async void OnTextSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || TextList.SelectedItem is not LocRow row) return;
        try { await LoadTextAsync(row); }
        catch (Exception ex) { StatusChanged?.Invoke("Couldn't read the string: " + ex.Message); }
    }

    /// <summary>Reads the language database from the game again and shows this string as it has it.</summary>
    private async Task LoadTextAsync(LocRow row)
    {
        int version = ++_textVersion;
        var db = await _mods.OpenDbAsync(_language);
        if (version != _textVersion) return;
        var t = db.Table(row.Table);
        _locDb = db;
        _currentText = row;
        _currentTextOriginal = (string)db.Get(t, row.Row, Roster.Field(t, "sourcetext"));
        TextId.Text = (string)db.Get(t, row.Row, Roster.Field(t, "stringid"));
        _suppress = true;
        TextBody.Text = _currentTextOriginal;
        _suppress = false;
        TextEditor.Visibility = Visibility.Visible;
        TextEmpty.Visibility = Visibility.Collapsed;
        UpdateBudget($"Read from the game at {DateTime.Now:HH:mm:ss}.");
    }

    private void OnTextBodyChanged(object sender, TextChangedEventArgs e)
    {
        if (!_suppress) UpdateBudget(null);
    }

    private void UpdateBudget(string idle)
    {
        if (_currentText == null || _locDb == null) return;
        var t = _locDb.Table(_currentText.Table);
        var (need, have) = _locDb.Budget(t, _currentText.Row, Roster.Field(t, "sourcetext"), TextBody.Text);
        bool changed = TextBody.Text != _currentTextOriginal;
        bool fits = need >= 0 && need <= have;
        double frac = have <= 0 ? 1 : Math.Clamp(need / (double)have, 0, 1);
        BudgetScale.ScaleX = frac;
        BudgetBar.Background = (Brush)FindResource(!fits ? "Danger" : frac > 0.9 ? "Patch" : "Accent");
        BudgetText.Text = need < 0 ? "This text uses a character that never appears in this language's strings, so it can't be encoded."
            : $"Compressed: {need} of {have} bytes. Text is edited in place, so it can be at most as long (compressed) as the original.";
        TextSave.IsEnabled = changed && fits && !_busy;
        TextRevert.IsEnabled = changed && !_busy;
        TextState.Text = !fits ? "Too long: shorten it." : changed ? "Not saved yet." : idle ?? "No changes.";
    }

    private void OnTextRevert(object sender, RoutedEventArgs e)
    {
        TextBody.Text = _currentTextOriginal;
    }

    private async void OnTextSave(object sender, RoutedEventArgs e)
    {
        if (_currentText == null || _busy) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var row = _currentText;
        string text = TextBody.Text, old = _currentTextOriginal;
        _busy = true;
        TextSave.IsEnabled = TextRevert.IsEnabled = false;
        StatusChanged?.Invoke($"Writing {row.Id} into {_language}…");
        try
        {
            await _mods.SaveDbAsync(_language, db =>
            {
                var t = db.Table(row.Table);
                db.Set(t, row.Row, Roster.Field(t, "sourcetext"), text);
                static string Short(string s) => s.Length > 40 ? s[..40] + "…" : s;
                return $"{row.Id}: \"{Short(old)}\" → \"{Short(text)}\"";
            });
            StatusChanged?.Invoke($"{row.Id} saved into the game");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Save failed: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "HMS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
        }
        await LoadLanguageAsync();
    }

    private async void OnReload(object sender, RoutedEventArgs e) => await ReloadAsync();
}
