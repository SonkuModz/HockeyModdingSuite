using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>Where one sync stream stands. Off is gray (not run, or its box is unticked), Running gold, Done green, Failed red.</summary>
public enum StageState { Off, Running, Done, Failed }

/// <summary>One sync stream's status chip above the list (NHL rosters, AHL rosters, draft picks, missing players' bios).</summary>
public sealed class SyncStage : Observable
{
    private StageState _state;
    private string _detail = "";

    public SyncStage(string label) => Label = label;

    public string Label { get; }
    public StageState State { get => _state; private set => Set(ref _state, value); }
    /// <summary>Short text after the label (the chip has a fixed width): "12/32 · MIN", "748 players", "off", the error.</summary>
    public string Detail { get => _detail; private set => Set(ref _detail, value); }

    public void Update(StageState state, string detail = "")
    {
        State = state;
        Detail = detail;
    }
}

/// <summary>One difference in the sync list, with its tick box.</summary>
public sealed class SyncRow : Observable
{
    private readonly Action _changed;
    private bool _selected;

    public SyncRow(SyncChange c, Action changed)
    {
        Change = c;
        _selected = c.Selected && (c.CanApply || c.CanCreate);
        _changed = changed;
    }

    /// <summary>A draft pick owner change from CapWages: same row, ticked and applied like a player move.</summary>
    public SyncRow(PickSyncChange p, Action changed)
    {
        Pick = p;
        _selected = p.Selected && p.CanApply;
        _changed = changed;
    }

    /// <summary>A contract change (or a team's retained-salary totals) from CapWages.</summary>
    public SyncRow(ContractChange c, Action changed)
    {
        Contract = c;
        _selected = c.Selected && c.CanApply;
        _changed = changed;
    }

    /// <summary>A team's lines from DailyFaceoff.</summary>
    public SyncRow(LineChange l, Action changed)
    {
        Line = l;
        _selected = l.Selected && l.CanApply;
        _changed = changed;
    }

    public SyncChange Change { get; }
    public PickSyncChange Pick { get; }
    public ContractChange Contract { get; }
    public LineChange Line { get; }
    public bool IsPick => Pick != null;
    public bool IsContract => Contract != null;
    public bool IsLines => Line != null;
    /// <summary>A roster row (trade, call-up, info, left, not in game), as opposed to a pick or contract row.</summary>
    public bool IsPlayer => Change != null;

    public string Name => IsLines ? $"{Line.TeamName} lines" : IsPick ? $"{Pick.Year + 1900} {PickSync.Ordinal(Pick.Round)} round pick ({Pick.OriginalAbbr})" : IsContract ? Contract.Name : Change.Name;
    public string Meta => IsLines ? $"{Line.TeamAbbr}  ·  lines  ·  DailyFaceoff" : IsPick ? "Draft pick  ·  CapWages" :
        IsContract ? (Contract.Kind == ContractChangeKind.TeamRetention ? $"{Contract.TeamAbbr}  ·  team retained salary  ·  CapWages" : $"{Contract.Position}  ·  {Contract.TeamAbbr}  ·  CapWages") :
        $"{Change.Position}" + (Change.Ahl is { } h ? $"  ·  AHL id {h.Id}" + (h.Born is { } b ? $"  ·  born {b:yyyy-MM-dd}" : "") : Change.Api is { } a ? $"  ·  NHL id {a.Id}" + (a.Born is { } b2 ? $"  ·  born {b2:yyyy-MM-dd}" : "") : $"  ·  game id {Change.GameId}");
    public string Description => IsLines ? Line.Description : IsPick ? Pick.Description : IsContract ? Contract.Description : Change.Description;
    public bool CanApply => IsLines ? Line.CanApply : IsPick ? Pick.CanApply : IsContract ? Contract.CanApply : Change.CanApply || Change.CanCreate;
    public string Note => IsLines ? Line.Source : IsPick ? "" :
        IsContract ? (Contract.NameOnly ? "Matched by name only: the birth dates differ. Check it's the same player." : Contract.Source) :
        Change.NameOnly ? "Matched by name only: the birth dates differ. Check it's the same player." :
        Change.Kind == SyncKind.Join && Change.NeedsNewRow ? "In the game as a free agent with no roster row — Sync will create one." :
        Change.Kind == SyncKind.Left ? "Unticked: he may be injured, sent down or gone. Tick it to send him to the AHL affiliate." :
        Change.Kind == SyncKind.Missing ? MissingNote() : "";

    /// <summary>A Missing row's note: his bio and that Sync will create him, or why he can't be created.</summary>
    private string MissingNote()
    {
        if (Change.Bio == null) return "Not in the game's database. His bio is needed to create him.";
        string problem = PlayerCreator.Problem(Change);
        return Change.Bio.Text + (problem == null
            ? "  ·  Sync will create him, with ratings and gear copied from a similar player and a generic face."
            : $"  ·  Can't be created: {problem}.");
    }
    public string KindText => IsLines ? "LINES" : IsPick ? "PICK" : IsContract ? (Contract.Kind == ContractChangeKind.TeamRetention ? "RETAINED" : "CONTRACT") : Change.Kind switch
    {
        SyncKind.Move => "TRADE", SyncKind.Join => "CALL-UP", SyncKind.Update => "INFO", SyncKind.Left => "LEFT NHL",
        _ => Change.CanCreate ? "CREATE" : "NOT IN GAME",
    };
    public Brush KindBrush => new SolidColorBrush(IsLines ? (Line.CanApply ? Color.FromRgb(0x9A, 0xD0, 0x5E) : Color.FromRgb(0xE0, 0x6C, 0x6C)) : IsPick ? Color.FromRgb(0xB7, 0x8C, 0xFF) :
        IsContract ? (Contract.Kind == ContractChangeKind.TeamRetention ? Color.FromRgb(0xE8, 0x9E, 0xC9) : Color.FromRgb(0x4F, 0xC9, 0xC4)) : Change.Kind switch
    {
        SyncKind.Move => Color.FromRgb(0xF2, 0xA6, 0x3B), SyncKind.Join => Color.FromRgb(0x5B, 0xC2, 0x8C), SyncKind.Update => Color.FromRgb(0x7C, 0xA7, 0xF5),
        SyncKind.Left => Color.FromRgb(0xB8, 0xBE, 0xC9),
        _ => Change.CanCreate ? Color.FromRgb(0xE6, 0xC1, 0x5A) : Color.FromRgb(0xE0, 0x6C, 0x6C),
    });
    /// <summary>Everything the search box matches on for a pick or contract row.</summary>
    public string SearchText => IsContract ? $"{Name} {Contract.TeamAbbr} {Description}" : IsLines ? $"{Name} {Line.TeamAbbr} {Description}" : $"{Name} {Description}";

    /// <summary>Called when the Missing player's bio arrives: the row shows it, and a player who can be created is ticked.</summary>
    public void BioLoaded()
    {
        foreach (var p in new[] { nameof(Note), nameof(KindText), nameof(KindBrush), nameof(CanApply) }) Raise(p);
        if (Change.CanCreate && !_selected) { _selected = true; Raise(nameof(Selected)); _changed?.Invoke(); }
    }

    public bool Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value && CanApply)) _changed?.Invoke(); }
    }
}

/// <summary>
/// The Roster page's NHL sync: fetches every team's current roster from the NHL API (and, when ticked, every AHL
/// team's roster from the AHL feed) and every team's contracts and draft picks from CapWages, compares them with the
/// game's (RosterSync, ContractSync, PickSync), lists the differences to tick, and writes the ticked ones into
/// nhl_game.db through the database's mod entry. Applying reads the database from the game again and matches the
/// players, contracts and picks afresh, so nothing stale is written.
/// </summary>
public partial class RosterSyncView : UserControl
{
    private ModService _mods;
    private List<NhlPlayer> _api;
    /// <summary>Every AHL team's roster as fetched (team + players), or null when the AHL box is unticked or the fetch failed.</summary>
    private List<(AhlTeam Team, List<AhlPlayer> Players)> _ahl;
    /// <summary>Feed teams with no matching team in the game, from the last AHL compare (skipped, not failed).</summary>
    private List<string> _ahlSkipped = new();
    /// <summary>The last AHL error (fetch or compare), shown in the status line; null when the AHL part is fine.</summary>
    private string _ahlError;
    private List<SyncRow> _rows = new();
    /// <summary>Every NHL team's CapWages page, parsed (contracts and picks), or null when neither box is ticked or the fetch failed.</summary>
    private List<CapTeam> _cap;
    /// <summary>Why the CapWages fetch failed; null when it's fine. Picks and contracts are then not compared at all.</summary>
    private string _capError;
    private List<OwnedPick> _picks;
    private PickCompareResult _pickResult;
    private List<SyncRow> _pickRows = new();
    private List<SyncRow> _contractRows = new();
    /// <summary>Every NHL team's DailyFaceoff lines as fetched (a team that failed has its error), or null when the lines box is unticked.</summary>
    private List<(string Abbr, LineTeam Team, string Error)> _lines;
    private List<SyncRow> _lineRows = new();
    /// <summary>" Draft picks from CapWages: 1120 picks (2027–2031); game first draft year 2026, offset 0." — "" when no pick compare has run; appended to the Fetch status line so the user can tell what the picks were compared against.</summary>
    private string _pickSource = "";
    private bool _busy;

    public event Action<string> StatusChanged;
    /// <summary>Raised after changes were written (the roster list reloads).</summary>
    public event Action Applied;

    /// <summary>Missing players' bios from this fetch, by source and id ("NHL:8485414", "AHL:11419"). Every compare
    /// (after Apply, ticking sizes or AHL) builds new rows, so they take their bios from here instead of losing them;
    /// a new Fetch starts it empty.</summary>
    private readonly Dictionary<string, PlayerBio> _bios = new();

    private static string BioKey(SyncChange c) => c.Api != null ? $"NHL:{c.Api.Id}" : c.Ahl != null ? $"AHL:{c.Ahl.Id}" : null;

    private readonly SyncStage _nhlStage = new("NHL rosters");
    private readonly SyncStage _ahlStage = new("AHL rosters");
    private readonly SyncStage _picksStage = new("Draft picks");
    private readonly SyncStage _contractsStage = new("Contracts");
    private readonly SyncStage _biosStage = new("Missing bios");
    private readonly SyncStage _linesStage = new("Lines");

    public RosterSyncView()
    {
        InitializeComponent();
        StageList.ItemsSource = new[] { _nhlStage, _ahlStage, _contractsStage, _picksStage, _linesStage, _biosStage };
    }

    /// <summary>The AHL chip after a fetch or compare: off when unticked, red when the fetch or compare failed, else green with the counts.</summary>
    private void ShowAhlStage()
    {
        if (AhlBox.IsChecked != true) _ahlStage.Update(StageState.Off, "off");
        else if (_ahlError != null) _ahlStage.Update(StageState.Failed, _ahlError);
        else if (_ahl != null) _ahlStage.Update(StageState.Done, $"{_ahl.Sum(t => t.Players.Count)} players");
    }

    /// <summary>The picks chip after the CapWages compare: off when unticked, red when CapWages failed, else green with how many owners differ.</summary>
    private void ShowPicksStage()
    {
        if (PicksBox.IsChecked != true) _picksStage.Update(StageState.Off, "off");
        else if (_capError != null) _picksStage.Update(StageState.Failed, _capError);
        else if (_pickResult != null) _picksStage.Update(StageState.Done, $"{_pickRows.Count} to change");
    }

    /// <summary>The contracts chip after the CapWages compare: off when unticked, red when CapWages failed, else green with how many differ.</summary>
    private void ShowContractsStage()
    {
        if (ContractsBox.IsChecked != true) _contractsStage.Update(StageState.Off, "off");
        else if (_capError != null) _contractsStage.Update(StageState.Failed, _capError);
        else if (_cap != null) _contractsStage.Update(StageState.Done, $"{_contractRows.Count} to change");
    }

    /// <summary>
    /// The CapWages stream: all 32 team pages, then the pick and contract compares for whichever boxes are ticked. Both
    /// chips show the fetch's progress, since one fetch feeds both. Never throws: a failed fetch turns both chips red and
    /// clears their rows (all or nothing — half a league is never compared), and the roster results survive.
    /// </summary>
    private async Task FetchCapWagesAsync(bool picks, bool contracts)
    {
        if (!picks && !contracts) return;
        void Report(string s)
        {
            if (picks) _picksStage.Update(StageState.Running, s);
            if (contracts) _contractsStage.Update(StageState.Running, s);
        }
        Report("starting…");
        _capError = null;
        try
        {
            int done = 0;
            var progress = new Progress<string>(t => Report($"{++done}/{CapWagesApi.Teams.Length} · {t}"));
            var sink = (IProgress<string>)progress;
            _cap = await Task.Run(() => CapWagesApi.AllTeamsAsync(t => sink.Report(t)));
        }
        catch (Exception ex)
        {
            _cap = null;
            _capError = ex.Message;
            ClearPicks();
            _contractRows = new List<SyncRow>();
            ApplyFilter();
            ShowPicksStage();
            ShowContractsStage();
            return;
        }
        if (picks) { Report("comparing…"); await ComparePicksAsync(); ShowPicksStage(); }
        if (contracts) { await CompareContractsAsync(); ShowContractsStage(); }
    }

    /// <summary>The lines chip: off when unticked, red when no team could be read, else green with how many teams differ (and how many pages failed).</summary>
    private void ShowLinesStage()
    {
        if (LinesBox.IsChecked != true) { _linesStage.Update(StageState.Off, "off"); return; }
        if (_lines == null) return;
        int failed = _lines.Count(t => t.Error != null);
        if (failed == _lines.Count) _linesStage.Update(StageState.Failed, _lines.FirstOrDefault().Error ?? "no teams");
        else _linesStage.Update(StageState.Done, $"{_lineRows.Count(r => r.Line.CanApply)} to change" + (failed > 0 ? $", {failed} failed" : ""));
    }

    /// <summary>
    /// The DailyFaceoff stream: every team's line-combinations page, then the compare. A team whose page fails is listed
    /// with its error and the others go on; it never throws, so the other streams are never taken down.
    /// </summary>
    private async Task FetchLinesAsync()
    {
        _linesStage.Update(StageState.Running, "starting…");
        try
        {
            int done = 0;
            var progress = new Progress<string>(t => _linesStage.Update(StageState.Running, $"{++done}/32 · {t}"));
            var sink = (IProgress<string>)progress;
            _lines = await Task.Run(() => DailyFaceoffApi.AllTeamsAsync(t => sink.Report(t)));
            await CompareLinesAsync();
        }
        catch (Exception ex)
        {
            _lines = null;
            _lineRows = new List<SyncRow>();
            ApplyFilter();
            _linesStage.Update(StageState.Failed, ex.Message);
            return;
        }
        ShowLinesStage();
    }

    /// <summary>Compares DailyFaceoff's lines with the game's as they are now (players not on their team yet are listed; Apply checks again after the roster changes).</summary>
    private async Task CompareLinesAsync()
    {
        if (_lines == null || LinesBox.IsChecked != true) return;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var lines = _lines;
        var changes = await Task.Run(() => LineSync.Compare(db, lines));
        _lineRows = changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
    }

    /// <summary>Ticking/unticking the lines box after a fetch: read DailyFaceoff (only if it hasn't been yet) and compare, or drop the line rows.</summary>
    private async void OnLines(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _busy || (_api == null && _cap == null && _lines == null)) return;
        if (LinesBox.IsChecked == true)
        {
            if (_lines == null) await FetchLinesAsync();
            else { await CompareLinesAsync(); ShowLinesStage(); }
        }
        else { _lineRows = new List<SyncRow>(); ApplyFilter(); ShowLinesStage(); }
    }

    private void ClearPicks()
    {
        _picks = null;
        _pickResult = null;
        _pickRows = new List<SyncRow>();
        _pickSource = "";
    }

    public void SetMods(ModService mods) => _mods = mods;

    private async void OnFetch(object sender, RoutedEventArgs e) => await FetchAsync();

    /// <summary>Fetches the NHL's rosters and compares them with the game's database as it is now. The NHL, AHL and CapWages fetches are three independent streams that run at the same time — CapWages feeds both the draft picks and the contracts — and the player compare runs once the NHL and AHL fetches are both done, since it dedupes across both.</summary>
    public async Task FetchAsync()
    {
        if (_mods == null || _busy) return;
        _busy = true;
        SetButtons();
        FetchButton.IsEnabled = false;
        try
        {
            bool includeAhl = AhlBox.IsChecked == true;
            bool includePicks = PicksBox.IsChecked == true;
            bool includeContracts = ContractsBox.IsChecked == true;
            bool includeLines = LinesBox.IsChecked == true;

            // Each stream reports on its own chip, so the streams never overwrite each other; the
            // text line below the chips gets the summary once everything is done.
            FetchStatus.Text = "Fetching…";
            _nhlStage.Update(StageState.Running, "starting…");
            _ahlStage.Update(includeAhl ? StageState.Running : StageState.Off, includeAhl ? "starting…" : "off");
            _picksStage.Update(includePicks ? StageState.Running : StageState.Off, includePicks ? "starting…" : "off");
            _contractsStage.Update(includeContracts ? StageState.Running : StageState.Off, includeContracts ? "starting…" : "off");
            _biosStage.Update(StageState.Off, "waiting");
            _linesStage.Update(includeLines ? StageState.Running : StageState.Off, includeLines ? "starting…" : "off");

            // NHL stream: fetch only. The compare needs the AHL fetch too (one shared matched
            // set), so it runs below, once both player fetches are done.
            int done = 0;
            var progress = new Progress<string>(t => _nhlStage.Update(StageState.Running, $"{++done}/{NhlApi.Teams.Length} · {t}"));
            var sink = (IProgress<string>)progress;
            Task<List<NhlPlayer>> nhlTask = Task.Run(() => NhlApi.AllRostersAsync(t => sink.Report(t)));

            // AHL stream: fetch only, and only when ticked.
            Task<List<(AhlTeam Team, List<AhlPlayer> Players)>> ahlTask = null;
            if (includeAhl)
            {
                int ahlDone = 0;
                var ahlProgress = new Progress<string>(t => _ahlStage.Update(StageState.Running, $"{++ahlDone}/32 · {t}"));
                var ahlSink = (IProgress<string>)ahlProgress;
                ahlTask = Task.Run(() => AhlApi.AllRostersAsync(t => ahlSink.Report(t)));
            }

            // CapWages stream: every team's page, then picks and contracts compared on their own. It
            // never throws, so it cannot take the player streams down with it. An unticked part's rows
            // from an earlier fetch go away.
            _cap = null;
            _capError = null;
            if (!includePicks) ClearPicks();
            if (!includeContracts) _contractRows = new List<SyncRow>();
            ApplyFilter();
            Task capTask = FetchCapWagesAsync(includePicks, includeContracts);

            // DailyFaceoff stream: every team's lines, compared on their own; never throws either.
            _lines = null;
            if (!includeLines) _lineRows = new List<SyncRow>();
            Task linesTask = includeLines ? FetchLinesAsync() : Task.CompletedTask;

            // Await the NHL fetch. A failure means no player list at all, but the CapWages stream
            // still runs to completion on its own.
            string nhlError = null;
            try { _api = await nhlTask; _nhlStage.Update(StageState.Done, $"{_api.Count} players"); }
            catch (Exception ex) { _api = null; nhlError = ex.Message; _nhlStage.Update(StageState.Failed, ex.Message); }

            // Await the AHL fetch (only when it ran). A failure is reported in the status line
            // while the NHL rows are still shown.
            _ahl = null;
            _ahlError = null;
            _ahlSkipped = new List<string>();
            if (ahlTask != null)
            {
                try { _ahl = await ahlTask; }
                catch (Exception ex) { _ahl = null; _ahlError = ex.Message; }
            }

            // The combined player compare: AHL first, NHL second, one shared matched set. The
            // CapWages stream may still be running on its own chips.
            _bios.Clear();
            if (_api != null) await CompareAsync();
            // After the compare, which can fail the AHL side on its own.
            ShowAhlStage();

            // Full bios for the players not in the game (the create-player pipeline needs them). Only
            // known once the compare has run, since it decides who is Missing.
            int bioFailed = 0;
            if (_api != null) bioFailed = await LookUpBiosAsync();
            else _biosStage.Update(StageState.Off, "skipped");

            // Wait for the CapWages and DailyFaceoff streams to finish (no-ops when unticked).
            await capTask;
            await linesTask;

            FetchStatus.Text = _api == null
                ? $"Couldn't fetch the NHL rosters: {nhlError}. " + CapCounts().TrimStart() + _pickSource
                : $"{_api.Count} players on NHL rosters{AhlSummary()}, fetched {DateTime.Now:HH:mm}. " + Counts() + (_ahlError == null ? "" : $" AHL failed: {_ahlError}.")
                  + (bioFailed == 0 ? "" : $" Bios couldn't be fetched for {bioFailed} missing player{(bioFailed == 1 ? "" : "s")}.")
                  + _pickSource;
            FilterBar.IsEnabled = true;
        }
        catch (Exception ex)
        {
            FetchStatus.Text = "Couldn't fetch the NHL rosters: " + ex.Message;
            // Whatever was still running stopped with it.
            foreach (var s in new[] { _nhlStage, _ahlStage, _contractsStage, _picksStage, _linesStage, _biosStage })
                if (s.State == StageState.Running) s.Update(StageState.Failed, ex.Message);
        }
        finally
        {
            _busy = false;
            FetchButton.IsEnabled = true;
            FetchLabel.Text = _api == null ? "Fetch NHL rosters" : "Fetch again";
            SetButtons();
        }
    }

    /// <summary>
    /// Looks up the full bio of each player who is not in the game (MissingPlayers.BioAsync): NHL roster players from
    /// the NHL API's player page, AHL roster players from the AHL feed's player view. The two run side by side, one
    /// player at a time each with a short pause (both hosts turn away bursts), and each row's note shows the bio as it
    /// arrives. A failed lookup leaves that row without a bio. Players whose bio is already known (from _bios) are not
    /// looked up again. Progress and the outcome go on the bios chip. Returns the number of failures.
    /// </summary>
    private async Task<int> LookUpBiosAsync()
    {
        var missing = _rows.Where(r => r.Change.Kind == SyncKind.Missing && r.Change.Bio == null).ToList();
        var nhl = missing.Where(r => r.Change.Api != null).ToList();
        var ahl = missing.Where(r => r.Change.Ahl is { Id: > 0 }).ToList();
        int total = nhl.Count + ahl.Count, done = 0, failed = 0;
        void report(string s) => _biosStage.Update(StageState.Running, s);
        if (total > 0) report($"0/{total}");

        async Task Run(List<SyncRow> rows)
        {
            foreach (var r in rows)
            {
                try
                {
                    r.Change.Bio = await MissingPlayers.BioAsync(r.Change);
                    if (r.Change.Bio != null && BioKey(r.Change) is { } key) _bios[key] = r.Change.Bio;
                    r.BioLoaded();
                }
                catch (Exception) { failed++; }
                report($"{++done}/{total}");
                await Task.Delay(150);
            }
        }

        await Task.WhenAll(Run(nhl), Run(ahl));
        int all = _rows.Count(r => r.Change.Kind == SyncKind.Missing), withBio = _rows.Count(r => r.Change.Kind == SyncKind.Missing && r.Change.Bio != null);
        if (failed > 0) _biosStage.Update(StageState.Failed, $"{failed} of {all} failed");
        else _biosStage.Update(StageState.Done, all == 0 ? "no players missing" : $"{withBio} players");
        return failed;
    }

    /// <summary>Compares the fetched rosters with a database read from the game now.</summary>
    private async Task CompareAsync()
    {
        var db = await _mods.OpenDbAsync(Roster.Database);
        bool sizes = SizesBox.IsChecked == true;
        var api = _api;
        var ahl = _ahl;
        // One combined compare (AHL first, NHL second, one shared matched set — see CompareCombined):
        // a player on both an NHL roster and an AHL roster gets exactly one change, the AHL one, so the
        // list can no longer show the NHL and AHL changes for the same player as inverses of each other.
        // An AHL compare failure must not wipe the NHL rows: fall back to the NHL compare alone, remember
        // the error for the status line, and try again on the next fetch. Teams with no game match are
        // skipped by CompareAllAhl (their names come back in _ahlSkipped) and do not fail the compare.
        List<SyncChange> changes;
        if (ahl != null)
        {
            try { var (combined, skipped) = await Task.Run(() => RosterSync.CompareCombined(db, api, ahl, sizes)); changes = combined; _ahlSkipped = skipped; _ahlError = null; }
            catch (Exception ex) { _ahlError = ex.Message; changes = await Task.Run(() => RosterSync.Compare(db, api, sizes)); }
        }
        else
        {
            changes = await Task.Run(() => RosterSync.Compare(db, api, sizes));
        }
        foreach (var c in changes)
            if (c.Kind == SyncKind.Missing && BioKey(c) is { } key && _bios.TryGetValue(key, out var bio)) { c.Bio = bio; c.Selected = c.CanCreate; }
        _rows = changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
    }

    /// <summary>Compares CapWages' pick owners with the game's draft picks as they are now. Real years map onto the game's through the offset (game first draft year − real completed draft), so it works for NHL 24 and NHL 27.</summary>
    private async Task ComparePicksAsync()
    {
        if (_cap == null || PicksBox.IsChecked != true) return;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var cap = _cap;
        var owners = await Task.Run(() => PickOwners.FromCapWages(cap, db));
        _picks = owners.Picks;
        _pickResult = await Task.Run(() => PickSync.Compare(db, owners.Picks));
        _pickRows = _pickResult.Changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
        string range = owners.Picks.Count == 0 ? "" : $" ({owners.Picks.Min(p => p.DraftYear) - owners.Offset}–{owners.Picks.Max(p => p.DraftYear) - owners.Offset})";
        _pickSource = $" Draft picks from CapWages: {owners.Picks.Count} picks{range}; game first draft year {(owners.GameFirstYear < 0 ? "unknown" : owners.GameFirstYear.ToString())}, offset {owners.Offset}"
            + (owners.Conflicts > 0 ? $"; {owners.Conflicts} picks listed by two teams (first kept)" : "") + ".";
    }

    /// <summary>Compares CapWages' contracts (and every team's retained-salary totals) with the game's as they are now.</summary>
    private async Task CompareContractsAsync()
    {
        if (_cap == null || ContractsBox.IsChecked != true) return;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var cap = _cap;
        var changes = await Task.Run(() => ContractSync.Compare(db, cap));
        _contractRows = changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
    }

    private string Counts()
    {
        int N(SyncKind k) => _rows.Count(r => !r.Change.IsAhl && r.Change.Kind == k);
        int A(SyncKind k) => _rows.Count(r => r.Change.IsAhl && r.Change.Kind == k);
        string s = $"{N(SyncKind.Move)} trades, {N(SyncKind.Join)} call-ups/signings, {N(SyncKind.Update)} info changes, " +
               $"{N(SyncKind.Left)} no longer on an NHL roster, {N(SyncKind.Missing)} not in the game.";
        if (_rows.Any(r => r.Change.IsAhl))
            s += $" AHL: {A(SyncKind.Join)} call-ups, {A(SyncKind.Move)} moves, {A(SyncKind.Update)} info changes, {A(SyncKind.Missing)} not in the game.";
        return s + CapCounts() + LinesCounts();
    }

    /// <summary>The DailyFaceoff part of the status line; "" when the lines box is unticked or nothing was read.</summary>
    private string LinesCounts()
    {
        if (_lines == null || LinesBox.IsChecked != true) return "";
        int failed = _lines.Count(t => t.Error != null), waiting = _lineRows.Count(r => r.Line.NotOnTeam.Count > 0);
        return $" {_lineRows.Count(r => r.Line.CanApply)} teams' lines differ from DailyFaceoff" + (waiting > 0 ? $" ({waiting} wait on roster changes)" : "")
            + (failed > 0 ? $"; {failed} team page{(failed == 1 ? "" : "s")} couldn't be read" : "") + ".";
    }

    /// <summary>The CapWages part of the status line: contracts and pick owners that differ, or why CapWages couldn't be compared; "" when neither box is ticked.</summary>
    private string CapCounts()
    {
        if (_capError != null) return $" CapWages couldn't be read, so contracts and picks were not compared: {_capError}";
        string s = "";
        if (_cap != null && ContractsBox.IsChecked == true)
        {
            int players = _contractRows.Count(r => r.Contract.Kind == ContractChangeKind.Contract);
            int teams = _contractRows.Count - players;
            s += $" {players} contract{(players == 1 ? "" : "s")} and {teams} team{(teams == 1 ? "" : "s")}' retained salary differ from CapWages.";
        }
        if (_pickResult != null)
        {
            s += $" {_pickRows.Count} draft pick owner{(_pickRows.Count == 1 ? "" : "s")} differ.";
            var notes = new List<string>();
            if (_pickResult.NoGameRow > 0) notes.Add($"{_pickResult.NoGameRow} CapWages picks with no game row");
            if (_pickResult.NoSourcePick > 0) notes.Add($"{_pickResult.NoSourcePick} game picks CapWages doesn't list");
            if (_pickResult.SkippedUnmapped > 0) notes.Add($"{_pickResult.SkippedUnmapped} unmapped");
            if (notes.Count > 0) s += " (" + string.Join(", ", notes) + ")";
        }
        return s;
    }

    /// <summary>", N players on M AHL teams" for the status line, plus any skipped teams; "" when the AHL box is unticked.</summary>
    private string AhlSummary() => _ahl == null ? "" : $", {_ahl.Sum(t => t.Players.Count)} on {_ahl.Count} AHL teams" +
        (_ahlSkipped.Count == 0 ? "" : $" (teams not in the game: {string.Join(", ", _ahlSkipped)})");

    private SyncKind? Kind => KindMove.IsChecked == true ? SyncKind.Move : KindJoin.IsChecked == true ? SyncKind.Join :
        KindUpdate.IsChecked == true ? SyncKind.Update : KindLeft.IsChecked == true ? SyncKind.Left : KindMissing.IsChecked == true ? SyncKind.Missing : null;

    private IEnumerable<SyncRow> Shown => ChangeList.ItemsSource as IEnumerable<SyncRow> ?? Enumerable.Empty<SyncRow>();

    private void ApplyFilter()
    {
        if (ChangeList == null) return;
        var kind = Kind;
        bool picksOnly = KindPicks.IsChecked == true, contractsOnly = KindContracts.IsChecked == true, linesOnly = KindLines.IsChecked == true;
        string q = SyncSearch.Text.Trim();
        var rows = _rows.Concat(_contractRows).Concat(_pickRows).Concat(_lineRows).Where(r =>
            (picksOnly ? r.IsPick : contractsOnly ? r.IsContract : linesOnly ? r.IsLines : kind == null || r.IsPlayer && r.Change.Kind == kind) &&
            (q.Length == 0 || (!r.IsPlayer ? r.SearchText.Contains(q, StringComparison.OrdinalIgnoreCase)
                : r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || (r.Change.Api?.Team ?? r.Change.Ahl?.TeamName ?? "").Equals(q, StringComparison.OrdinalIgnoreCase)))).ToList();
        ChangeList.ItemsSource = rows;
        EmptyText.Text = _api == null && _pickResult == null && _cap == null ? "Press Fetch NHL rosters to compare the game with today's NHL." :
            _api == null ? "No contract or draft pick differences from CapWages." : "Nothing here: the game already matches.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SetButtons();
    }

    private void OnFilter(object sender, RoutedEventArgs e) => ApplyFilter();

    private async void OnSizes(object sender, RoutedEventArgs e)
    {
        if (_api == null || _busy) return;
        await CompareAsync();
    }

    /// <summary>Ticking/unticking the AHL box after a fetch: fetch all 32 AHL teams (not all 32 NHL teams again), or drop the AHL rows.</summary>
    private async void OnAhl(object sender, RoutedEventArgs e)
    {
        if (_api == null || _busy) return;
        if (AhlBox.IsChecked == true && _ahl == null)
        {
            try
            {
                int ahlDone = 0;
                _ahlStage.Update(StageState.Running, "starting…");
                var ahlProgress = new Progress<string>(t => _ahlStage.Update(StageState.Running, $"{++ahlDone}/32 · {t}"));
                var ahlSink = (IProgress<string>)ahlProgress;
                _ahl = await Task.Run(() => AhlApi.AllRostersAsync(t => ahlSink.Report(t))); _ahlError = null;
            }
            catch (Exception ex) { _ahl = null; _ahlError = ex.Message; ShowAhlStage(); FetchStatus.Text = "Couldn't fetch the AHL rosters: " + ex.Message; return; }
        }
        else if (AhlBox.IsChecked != true) { _ahl = null; _ahlError = null; _ahlSkipped = new List<string>(); }
        await CompareAsync();
        ShowAhlStage();
        // Newly listed AHL players not in the game need their bios too (ones already fetched come from _bios).
        int bioFailed = await LookUpBiosAsync();
        FetchStatus.Text = $"{_api.Count} players on NHL rosters{AhlSummary()}. " + Counts() +
            (_ahlError == null ? "" : $" AHL: {_ahlError}") +
            (bioFailed == 0 ? "" : $" Bios couldn't be fetched for {bioFailed} missing player{(bioFailed == 1 ? "" : "s")}.");
    }

    /// <summary>Ticking/unticking the draft picks box after a fetch: compare the picks (fetching CapWages first only if it
    /// hasn't been read yet — not the rosters again), or drop the pick rows.</summary>
    private async void OnPicks(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _busy || (_api == null && _cap == null)) return;
        if (PicksBox.IsChecked == true)
        {
            if (_cap == null) await FetchCapWagesAsync(picks: true, contracts: false);
            else { await ComparePicksAsync(); ShowPicksStage(); }
        }
        else { ClearPicks(); ApplyFilter(); ShowPicksStage(); }
        ShowCapSummary();
    }

    /// <summary>Ticking/unticking the contracts box after a fetch: compare the contracts (fetching CapWages first only if it
    /// hasn't been read yet), or drop the contract rows.</summary>
    private async void OnContracts(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _busy || (_api == null && _cap == null)) return;
        if (ContractsBox.IsChecked == true)
        {
            if (_cap == null) await FetchCapWagesAsync(picks: false, contracts: true);
            else { await CompareContractsAsync(); ShowContractsStage(); }
        }
        else { _contractRows = new List<SyncRow>(); ApplyFilter(); ShowContractsStage(); }
        ShowCapSummary();
    }

    /// <summary>The status line after a picks/contracts box changed.</summary>
    private void ShowCapSummary() => FetchStatus.Text = _api == null
        ? CapCounts().TrimStart() + _pickSource
        : $"{_api.Count} players on NHL rosters{AhlSummary()}. " + Counts() + (_ahlError == null ? "" : $" AHL: {_ahlError}") + _pickSource;

    private void OnTickAll(object sender, RoutedEventArgs e) { foreach (var r in Shown) r.Selected = true; }
    private void OnTickNone(object sender, RoutedEventArgs e) { foreach (var r in Shown) r.Selected = false; }

    private void SetButtons()
    {
        int n = _rows.Count(r => r.Selected) + _contractRows.Count(r => r.Selected) + _pickRows.Count(r => r.Selected) + _lineRows.Count(r => r.Selected);
        int total = _rows.Count + _contractRows.Count + _pickRows.Count + _lineRows.Count;
        ApplyButton.IsEnabled = !_busy && n > 0;
        TickAll.IsEnabled = TickNone.IsEnabled = !_busy && Shown.Any();
        ApplyLabel.Text = n > 0 ? $"Apply {n} to game" : "Apply to game";
        ApplyStatus.Text = _api == null && _cap == null ? "" : $"{n} of {total} ticked. Players who change team get their line spots cleared; the game puts them in its own lines."
            + (_rows.Any(r => r.Change.CanCreate) ? " Create rows add the player with ratings and gear copied from a similar player, a generic face and (with Contracts ticked) his CapWages deal." : "")
            + (_contractRows.Count > 0 ? " Contract rows write the deal, extension, clauses and retention; an unchanged deal keeps the game's clauses." : "")
            + (_pickRows.Count > 0 ? " Pick rows only change the pick's owner." : "")
            + (_lineRows.Count > 0 ? " Lines rows set the team's lines, special teams and goalies from DailyFaceoff after the roster changes; a team whose lineup has a player not on it in the game is skipped." : "");
    }

    private async void OnApply(object sender, RoutedEventArgs e) => await ApplyAsync();

    /// <summary>Writes the ticked changes into the game: a fresh read of the database, matched again, then the same players' changes applied.</summary>
    public async Task ApplyAsync()
    {
        // IsAhl is part of the key: an NHL change and an AHL change for the same player/kind/team must not be confused.
        var picked = _rows.Where(r => r.Selected && r.Change.Kind != SyncKind.Missing).Select(r => (r.Change.GameId, r.Change.Kind, r.Change.ToTeam, r.Change.IsAhl)).ToHashSet();
        // Missing players to create, by source id (every Missing row has game id -1, so the key above can't tell them apart),
        // with the bios looked up for them: the fresh compare's rows have none.
        var toCreate = _rows.Where(r => r.Selected && r.Change.CanCreate && BioKey(r.Change) != null)
            .GroupBy(r => BioKey(r.Change)).ToDictionary(g => g.Key, g => g.First().Change.Bio);
        bool contractsOn = ContractsBox.IsChecked == true;
        var pickedPicks = _pickRows.Where(r => r.Selected).Select(r => (r.Pick.Year, r.Pick.Round, r.Pick.OriginalTeam)).ToHashSet();
        var pickedContracts = _contractRows.Where(r => r.Selected).Select(r => r.Contract.Key).ToHashSet();
        var pickedLines = _lineRows.Where(r => r.Selected).Select(r => r.Line.TeamAbbr).ToHashSet();
        if (picked.Count == 0 && toCreate.Count == 0 && pickedPicks.Count == 0 && pickedContracts.Count == 0 && pickedLines.Count == 0 || _busy) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _busy = true;
        SetButtons();
        var api = _api;
        var ahl = _ahl;
        var picks = _picks;
        var cap = _cap;
        bool sizes = SizesBox.IsChecked == true;
        int totalPicked = picked.Count + toCreate.Count + pickedPicks.Count + pickedContracts.Count + pickedLines.Count;
        var lines = _lines;
        StatusChanged?.Invoke($"Writing {totalPicked} roster change(s) into the game…");
        string summary = null;
        try
        {
            await _mods.SaveDbAsync(Roster.Database, db =>
            {
                // Re-compare fresh with the same combined compare the list uses (AHL first, NHL second,
                // one shared matched set — see CompareCombined): the fresh list can no longer contain two
                // changes for the same player that would overwrite each other in Apply. Safety net: if a
                // duplicate somehow remains, keep one change per player, preferring the AHL one.
                // No player fetch has run: there is nothing to re-compare for players, so the fresh player
                // list is empty — the contract and pick rows are re-compared below from CapWages' data.
                List<SyncChange> fresh = api == null ? new List<SyncChange>() : ahl != null
                    ? RosterSync.CompareCombined(db, api, ahl, sizes).Changes
                    : RosterSync.Compare(db, api, sizes);
                var missing = fresh.Where(c => c.Kind == SyncKind.Missing && BioKey(c) is { } k && toCreate.ContainsKey(k)).ToList();
                foreach (var c in missing) c.Bio = toCreate[BioKey(c)];
                fresh = fresh.Where(c => c.Kind != SyncKind.Missing && picked.Contains((c.GameId, c.Kind, c.ToTeam, c.IsAhl))).ToList();
                var deduped = new List<SyncChange>(fresh.Count);
                var byGame = new Dictionary<int, int>(); // GameId -> index in deduped (GameId -1 rows are never deduped)
                foreach (var c in fresh)
                {
                    if (c.GameId < 0) { deduped.Add(c); continue; }
                    if (!byGame.TryGetValue(c.GameId, out int at)) { byGame[c.GameId] = deduped.Count; deduped.Add(c); }
                    else if (c.IsAhl && !deduped[at].IsAhl) deduped[at] = c;
                }
                fresh = deduped;
                var summaries = new List<string>();
                if (fresh.Count > 0) summaries.Add(RosterSync.Apply(db, fresh));
                // New players after the moves (so a team's 40-player limit counts the moves), then their contracts below.
                var created = new HashSet<int>();
                if (missing.Count > 0)
                {
                    var made = PlayerCreator.Create(db, missing, contractsOn ? cap : null);
                    created = made.CreatedIds;
                    if (made.Summary.Length > 0) summaries.Add(made.Summary);
                    if (made.Skipped.Count > 0)
                        summaries.Add("not created: " + string.Join(", ", made.Skipped.Take(5).Select(s => $"{s.Name} ({s.Reason})")) + (made.Skipped.Count > 5 ? ", …" : ""));
                }
                // Contracts after the roster moves (a traded player's retention depends on his new team), re-compared
                // fresh against the database being saved: the ticked ones, and every new player's (he starts unsigned).
                if (cap != null && (pickedContracts.Count > 0 || contractsOn && created.Count > 0))
                {
                    var freshContracts = ContractSync.Compare(db, cap).Where(c => pickedContracts.Contains(c.Key) ||
                        contractsOn && c.Kind == ContractChangeKind.Contract && created.Contains(c.GameId) && !c.NameOnly).ToList();
                    if (freshContracts.Count > 0) summaries.Add(ContractSync.Apply(db, freshContracts));
                }
                // Lines last among the roster parts: the moves and new players are in, so the lineups can find them.
                // A team whose lineup still has a player not on it is skipped and named in the summary.
                if (lines != null && pickedLines.Count > 0) summaries.Add(LineSync.Apply(db, lines, pickedLines));
                if (picks != null)
                {
                    var freshPicks = PickSync.Compare(db, picks).Changes.Where(c => pickedPicks.Contains((c.Year, c.Round, c.OriginalTeam))).ToList();
                    if (freshPicks.Count > 0) summaries.Add(PickSync.Apply(db, freshPicks));
                }
                if (summaries.Count == 0) return null;
                summary = string.Join("; ", summaries);
                return summary + $" ({DateTime.Now:yyyy-MM-dd})";
            }, "Roster sync");
            if (summary != null)
            {
                StatusChanged?.Invoke($"{summary}. The Mods page can restore the original.");
                Applied?.Invoke();
            }
            else
            {
                // SaveDbAsync wrote nothing (the fresh re-compare matched none of the ticked rows): say
                // so, instead of claiming the changes were written when they were not.
                StatusChanged?.Invoke("Nothing was written: the ticked rows no longer matched on a fresh compare. Fetch again and check the list.");
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't apply: " + ex.Message);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't write the roster changes", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        if (api != null) await CompareAsync();
        await CompareContractsAsync();
        await ComparePicksAsync();
        await CompareLinesAsync();
        ShowContractsStage();
        ShowPicksStage();
        ShowLinesStage();
        FetchStatus.Text = _api == null
            ? "After applying: " + CapCounts().TrimStart() + _pickSource
            : $"{_api.Count} players on NHL rosters{AhlSummary()}. After applying: " + Counts() +
              (_ahlError == null ? "" : $" AHL: {_ahlError}") + _pickSource;
    }

    /// <summary>Shows one kind of change (scripted runs): move, join, update, left, missing, contracts, picks.</summary>
    public void ShowKind(string kind)
    {
        (kind switch { "move" => KindMove, "join" => KindJoin, "update" => KindUpdate, "left" => KindLeft, "missing" => KindMissing, "contracts" => KindContracts, "picks" => KindPicks, "lines" => KindLines, _ => KindAll }).IsChecked = true;
    }
}
