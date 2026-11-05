using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>One difference in the sync list, with its tick box.</summary>
public sealed class SyncRow : Observable
{
    private readonly Action _changed;
    private bool _selected;

    public SyncRow(SyncChange c, Action changed)
    {
        Change = c;
        _selected = c.Selected && c.CanApply;
        _changed = changed;
    }

    /// <summary>A draft pick owner change from the bundled draft-picks.json file: same row, ticked and applied like a player move.</summary>
    public SyncRow(PickSyncChange p, Action changed)
    {
        Pick = p;
        _selected = p.Selected && p.CanApply;
        _changed = changed;
    }

    public SyncChange Change { get; }
    public PickSyncChange Pick { get; }
    public bool IsPick => Pick != null;

    public string Name => IsPick ? $"{Pick.Year + 1900} {PickSync.Ordinal(Pick.Round)} round pick ({Pick.OriginalAbbr})" : Change.Name;
    public string Meta => IsPick ? "Draft pick  ·  draft-picks.json" :
        $"{Change.Position}" + (Change.Ahl is { } h ? $"  ·  AHL id {h.Id}" + (h.Born is { } b ? $"  ·  born {b:yyyy-MM-dd}" : "") : Change.Api is { } a ? $"  ·  NHL id {a.Id}" + (a.Born is { } b2 ? $"  ·  born {b2:yyyy-MM-dd}" : "") : $"  ·  game id {Change.GameId}");
    public string Description => IsPick ? Pick.Description : Change.Description;
    public bool CanApply => IsPick ? Pick.CanApply : Change.CanApply;
    public string Note => IsPick ? "" : Change.NameOnly ? "Matched by name only: the birth dates differ. Check it's the same player." :
        Change.Kind == SyncKind.Join && Change.NeedsNewRow ? "In the game as a free agent with no roster row — Sync will create one." :
        Change.Kind == SyncKind.Left ? "Unticked: he may be injured, sent down or gone. Tick it to send him to the AHL affiliate." :
        Change.Kind == SyncKind.Missing ? "Not in the game's database, so it can't be applied yet." : "";
    public string KindText => IsPick ? "PICK" : Change.Kind switch
    {
        SyncKind.Move => "TRADE", SyncKind.Join => "CALL-UP", SyncKind.Update => "NUMBER", SyncKind.Left => "LEFT NHL", _ => "NOT IN GAME",
    };
    public Brush KindBrush => new SolidColorBrush(IsPick ? Color.FromRgb(0xB7, 0x8C, 0xFF) : Change.Kind switch
    {
        SyncKind.Move => Color.FromRgb(0xF2, 0xA6, 0x3B), SyncKind.Join => Color.FromRgb(0x5B, 0xC2, 0x8C), SyncKind.Update => Color.FromRgb(0x7C, 0xA7, 0xF5),
        SyncKind.Left => Color.FromRgb(0xB8, 0xBE, 0xC9), _ => Color.FromRgb(0xE0, 0x6C, 0x6C),
    });
    /// <summary>Everything the search box matches on for a pick row.</summary>
    public string SearchText => $"{Name} {Description}";

    public bool Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value && CanApply)) _changed?.Invoke(); }
    }
}

/// <summary>
/// The Roster page's NHL sync: fetches every team's current roster from the NHL API (and, when ticked, every AHL
/// team's roster from the AHL feed) and the draft picks from the bundled draft-picks.json file next to the exe,
/// compares them with the game's (RosterSync, PickSync), lists the differences to tick, and writes the ticked ones
/// into nhl_game.db through the database's mod entry. Applying reads the database from the game again and matches
/// the players and picks afresh, so nothing stale is written.
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
    private List<PuckPediaPick> _picks;
    private PickSnapshot.SnapshotFile _snapshot;
    private PickCompareResult _pickResult;
    private List<SyncRow> _pickRows = new();
    /// <summary>The reason the snapshot file could not be used (missing or unreadable); null when it loaded fine. Shown in the status line instead of pick rows.</summary>
    private string _pickError;
    /// <summary>"Draft picks: bundled file (2026-11-04), 896 picks; game first draft year 2026, offset 0." — "" when no pick fetch has run; appended to the Fetch status line so the user can tell what the picks were compared against.</summary>
    private string _pickSource = "";
    private bool _busy;

    public event Action<string> StatusChanged;
    /// <summary>Raised after changes were written (the roster list reloads).</summary>
    public event Action Applied;

    public RosterSyncView() => InitializeComponent();

    public void SetMods(ModService mods) => _mods = mods;

    private async void OnFetch(object sender, RoutedEventArgs e) => await FetchAsync();

    /// <summary>Fetches the NHL's rosters and compares them with the game's database as it is now. The NHL, AHL and draft-pick fetches are three independent streams that run at the same time — the picks stream reads the bundled draft-picks.json file next to the exe (no network, no waiting) while the player streams fetch from their hosts — and the player compare runs once the NHL and AHL fetches are both done, since it dedupes across both.</summary>
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

            // Shared progress: each stream updates only its own line, and the status text is
            // recomposed from all three, so the streams never overwrite each other.
            string nhlState = "NHL rosters…", ahlState = includeAhl ? "AHL rosters…" : null, pickState = includePicks ? "draft picks…" : null;
            void RefreshFetchStatus()
            {
                var parts = new List<string>();
                if (nhlState != null) parts.Add(nhlState);
                if (ahlState != null) parts.Add(ahlState);
                if (pickState != null) parts.Add(pickState);
                FetchStatus.Text = parts.Count == 0 ? "Fetching…" : "Fetching " + string.Join("  ·  ", parts);
            }
            RefreshFetchStatus();

            // NHL stream: fetch only. The compare needs the AHL fetch too (one shared matched
            // set), so it runs below, once both player fetches are done.
            int done = 0;
            var progress = new Progress<string>(t => { nhlState = $"NHL rosters… {++done}/{NhlApi.Teams.Length} ({t})"; RefreshFetchStatus(); });
            var sink = (IProgress<string>)progress;
            Task<List<NhlPlayer>> nhlTask = Task.Run(() => NhlApi.AllRostersAsync(t => sink.Report(t)));

            // AHL stream: fetch only, and only when ticked.
            Task<List<(AhlTeam Team, List<AhlPlayer> Players)>> ahlTask = null;
            if (includeAhl)
            {
                int ahlDone = 0;
                var ahlProgress = new Progress<string>(t => { ahlState = $"AHL rosters… {++ahlDone}/32 ({t})"; RefreshFetchStatus(); });
                var ahlSink = (IProgress<string>)ahlProgress;
                ahlTask = Task.Run(() => AhlApi.AllRostersAsync(t => ahlSink.Report(t)));
            }

            // Picks stream: read the bundled draft-picks.json next to the exe and compare, on its
            // own. It never throws — a missing or unreadable file just clears the pick rows — so it
            // cannot take the player streams down with it.
            Task picksTask = Task.CompletedTask;
            if (includePicks)
            {
                picksTask = FetchPicksAsync(() => { pickState = "draft picks done"; RefreshFetchStatus(); });
            }
            else
            {
                // Unticked: the picks are not read, and any pick rows from an earlier fetch go away.
                _picks = null;
                _snapshot = null;
                _pickResult = null;
                _pickRows = new List<SyncRow>();
                _pickError = null;
                _pickSource = "";
                ApplyFilter();
            }

            // Await the NHL fetch. A failure means no player list at all, but the picks stream
            // still runs to completion on its own.
            string nhlError = null;
            try { _api = await nhlTask; nhlState = "NHL rosters done"; }
            catch (Exception ex) { _api = null; nhlError = ex.Message; nhlState = "NHL rosters failed"; }
            RefreshFetchStatus();

            // Await the AHL fetch (only when it ran). A failure is reported in the status line
            // while the NHL rows are still shown.
            _ahl = null;
            _ahlError = null;
            _ahlSkipped = new List<string>();
            if (ahlTask != null)
            {
                try { _ahl = await ahlTask; ahlState = "AHL rosters done"; }
                catch (Exception ex) { _ahl = null; _ahlError = ex.Message; ahlState = "AHL rosters failed"; }
                RefreshFetchStatus();
            }

            // The combined player compare: AHL first, NHL second, one shared matched set. The
            // picks stream may still be fetching; its progress keeps updating the line.
            if (_api != null) await CompareAsync();

            // Wait for the picks stream to finish (no-op when unticked).
            await picksTask;

            FetchStatus.Text = _api == null
                ? $"Couldn't fetch the NHL rosters: {nhlError}. " + PickCounts().TrimStart() + _pickSource
                : $"{_api.Count} players on NHL rosters{AhlSummary()}, fetched {DateTime.Now:HH:mm}. " + Counts() + (_ahlError == null ? "" : $" AHL failed: {_ahlError}.")
                  + _pickSource;
            FilterBar.IsEnabled = true;
        }
        catch (Exception ex)
        {
            FetchStatus.Text = "Couldn't fetch the NHL rosters: " + ex.Message;
        }
        finally
        {
            _busy = false;
            FetchButton.IsEnabled = true;
            FetchLabel.Text = _api == null ? "Fetch NHL rosters & picks" : "Fetch again";
            SetButtons();
        }
    }

    /// <summary>One picks stream: read the bundled draft-picks.json next to the exe, then compare. Never throws: a missing or unreadable file just clears the pick rows and records the reason, so the player results survive.</summary>
    private async Task FetchPicksAsync(Action onDone = null)
    {
        _pickSource = "";
        _pickError = null;
        var (file, error) = PickSnapshot.Load();
        if (file == null)
        {
            _picks = null;
            _snapshot = null;
            _pickResult = null;
            _pickRows = new List<SyncRow>();
            _pickError = error;
            ApplyFilter();
            onDone?.Invoke();
            return;
        }
        _snapshot = file;
        await ComparePicksAsync();
        onDone?.Invoke();
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
        _rows = changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
    }

    /// <summary>Compares the snapshot file's picks with the game's draft picks as they are now. The file's real years are mapped onto the game's years through the offset (game first draft year − snapshot year), so the same file works for NHL 24 and NHL 27.</summary>
    private async Task ComparePicksAsync()
    {
        if (_snapshot == null) return;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var file = _snapshot;
        var mapped = await Task.Run(() => PickSnapshot.ToPicks(file, db));
        _picks = mapped.Picks;
        _pickResult = await Task.Run(() => PickSync.Compare(db, _picks));
        _pickRows = _pickResult.Changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
        _pickSource = mapped.GameFirstYear < 0
            ? $" Draft picks: bundled file ({file.AsOf}), {file.Picks.Count} picks; game first draft year unknown, offset 0."
            : $" Draft picks: bundled file ({file.AsOf}), {file.Picks.Count} picks; game first draft year {mapped.GameFirstYear}, offset {mapped.Offset}.";
    }

    private string Counts()
    {
        int N(SyncKind k) => _rows.Count(r => !r.Change.IsAhl && r.Change.Kind == k);
        int A(SyncKind k) => _rows.Count(r => r.Change.IsAhl && r.Change.Kind == k);
        string s = $"{N(SyncKind.Move)} trades, {N(SyncKind.Join)} call-ups/signings, {N(SyncKind.Update)} number changes, " +
               $"{N(SyncKind.Left)} no longer on an NHL roster, {N(SyncKind.Missing)} not in the game.";
        if (_rows.Any(r => r.Change.IsAhl))
            s += $" AHL: {A(SyncKind.Join)} call-ups, {A(SyncKind.Move)} moves, {A(SyncKind.Update)} number changes, {A(SyncKind.Missing)} not in the game.";
        if (_pickResult == null) return s;
        return s + PickCounts();
    }

    /// <summary>The picks part of the status line: "3 draft pick owners differ from the bundled file. (…)", or the not-compared line when the snapshot file could not be used; "" when no pick compare has run.</summary>
    private string PickCounts()
    {
        if (_pickResult == null) return _pickError == null ? "" : $" Draft picks were not compared: {_pickError}";
        string s = $" {_pickRows.Count} draft pick owner{(_pickRows.Count == 1 ? "" : "s")} differ from the bundled file.";
        var notes = new List<string>();
        if (_pickResult.NoGameRow > 0) notes.Add($"{_pickResult.NoGameRow} file picks with no game row");
        if (_pickResult.NoPuckPediaPick > 0) notes.Add($"{_pickResult.NoPuckPediaPick} game picks with no file match");
        if (_pickResult.SkippedUnmapped > 0) notes.Add($"{_pickResult.SkippedUnmapped} unmapped");
        if (notes.Count > 0) s += " (" + string.Join(", ", notes) + ")";
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
        bool picksOnly = KindPicks.IsChecked == true;
        string q = SyncSearch.Text.Trim();
        var rows = _rows.Concat(_pickRows).Where(r =>
            (picksOnly ? r.IsPick : kind == null || !r.IsPick && r.Change.Kind == kind) &&
            (q.Length == 0 || (r.IsPick ? r.SearchText.Contains(q, StringComparison.OrdinalIgnoreCase)
                : r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || (r.Change.Api?.Team ?? r.Change.Ahl?.TeamName ?? "").Equals(q, StringComparison.OrdinalIgnoreCase)))).ToList();
        ChangeList.ItemsSource = rows;
        EmptyText.Text = _api == null && _pickResult == null ? "Press Fetch NHL rosters & picks to compare the game with today's NHL." :
            _api == null ? "No draft pick differences in the bundled file." : "Nothing here: the game already matches.";
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
                var ahlProgress = new Progress<string>(t => FetchStatus.Text = $"Fetching AHL rosters… {++ahlDone}/32 ({t})");
                var ahlSink = (IProgress<string>)ahlProgress;
                _ahl = await Task.Run(() => AhlApi.AllRostersAsync(t => ahlSink.Report(t))); _ahlError = null;
            }
            catch (Exception ex) { _ahl = null; _ahlError = ex.Message; FetchStatus.Text = "Couldn't fetch the AHL rosters: " + ex.Message; return; }
        }
        else if (AhlBox.IsChecked != true) { _ahl = null; _ahlError = null; _ahlSkipped = new List<string>(); }
        await CompareAsync();
        FetchStatus.Text = $"{_api.Count} players on NHL rosters{AhlSummary()}. " + Counts() +
            (_ahlError == null ? "" : $" AHL: {_ahlError}");
    }

    /// <summary>Ticking/unticking the draft picks box after a fetch: read the bundled file (not the rosters again), or drop the pick rows.</summary>
    private async void OnPicks(object sender, RoutedEventArgs e)
    {
        if (_mods == null || _busy) return;
        if (PicksBox.IsChecked == true && _picks == null)
        {
            await FetchPicksAsync();
            FetchStatus.Text = _api == null
                ? PickCounts().TrimStart() + _pickSource
                : $"{_api.Count} players on NHL rosters{AhlSummary()}. " + Counts() + (_ahlError == null ? "" : $" AHL: {_ahlError}") + _pickSource;
        }
        else if (PicksBox.IsChecked != true)
        {
            _picks = null;
            _snapshot = null;
            _pickResult = null;
            _pickRows = new List<SyncRow>();
            _pickError = null;
            _pickSource = "";
            ApplyFilter();
            if (_api != null)
                FetchStatus.Text = $"{_api.Count} players on NHL rosters{AhlSummary()}. " + Counts() + (_ahlError == null ? "" : $" AHL: {_ahlError}") + _pickSource;
        }
    }

    private void OnTickAll(object sender, RoutedEventArgs e) { foreach (var r in Shown) r.Selected = true; }
    private void OnTickNone(object sender, RoutedEventArgs e) { foreach (var r in Shown) r.Selected = false; }

    private void SetButtons()
    {
        int n = _rows.Count(r => r.Selected) + _pickRows.Count(r => r.Selected);
        int total = _rows.Count + _pickRows.Count;
        ApplyButton.IsEnabled = !_busy && n > 0;
        TickAll.IsEnabled = TickNone.IsEnabled = !_busy && Shown.Any();
        ApplyLabel.Text = n > 0 ? $"Apply {n} to game" : "Apply to game";
        ApplyStatus.Text = _api == null ? "" : $"{n} of {total} ticked. Players who change team get their line spots cleared; the game puts them in its own lines."
            + (_pickRows.Count > 0 ? " Pick rows only change the pick's owner." : "");
    }

    private async void OnApply(object sender, RoutedEventArgs e) => await ApplyAsync();

    /// <summary>Writes the ticked changes into the game: a fresh read of the database, matched again, then the same players' changes applied.</summary>
    public async Task ApplyAsync()
    {
        // IsAhl is part of the key: an NHL change and an AHL change for the same player/kind/team must not be confused.
        var picked = _rows.Where(r => r.Selected).Select(r => (r.Change.GameId, r.Change.Kind, r.Change.ToTeam, r.Change.IsAhl)).ToHashSet();
        var pickedPicks = _pickRows.Where(r => r.Selected).Select(r => (r.Pick.Year, r.Pick.Round, r.Pick.OriginalTeam)).ToHashSet();
        if (picked.Count == 0 && pickedPicks.Count == 0 || _busy) return;
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
        bool sizes = SizesBox.IsChecked == true;
        int totalPicked = picked.Count + pickedPicks.Count;
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
                // No player fetch has run (the picks come from the bundled file, not a fetch): there is
                // nothing to re-compare for players, so the fresh player list is empty — the pick rows are
                // re-compared below from _picks, which is what Apply needs here.
                List<SyncChange> fresh = api == null ? new List<SyncChange>() : ahl != null
                    ? RosterSync.CompareCombined(db, api, ahl, sizes).Changes
                    : RosterSync.Compare(db, api, sizes);
                fresh = fresh.Where(c => picked.Contains((c.GameId, c.Kind, c.ToTeam, c.IsAhl))).ToList();
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
        await ComparePicksAsync();
        FetchStatus.Text = _api == null
            ? "After applying: " + PickCounts().TrimStart() + _pickSource
            : $"{_api.Count} players on NHL rosters{AhlSummary()}. After applying: " + Counts() +
              (_ahlError == null ? "" : $" AHL: {_ahlError}") + _pickSource;
    }

    /// <summary>Shows one kind of change (scripted runs): move, join, update, left, missing, picks.</summary>
    public void ShowKind(string kind)
    {
        (kind switch { "move" => KindMove, "join" => KindJoin, "update" => KindUpdate, "left" => KindLeft, "missing" => KindMissing, "picks" => KindPicks, _ => KindAll }).IsChecked = true;
    }
}
