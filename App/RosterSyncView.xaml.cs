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

    public SyncChange Change { get; }
    public string Name => Change.Name;
    public string Meta => $"{Change.Position}" + (Change.Ahl is { } h ? $"  ·  AHL id {h.Id}" + (h.Born is { } b ? $"  ·  born {b:yyyy-MM-dd}" : "") : Change.Api is { } a ? $"  ·  NHL id {a.Id}" + (a.Born is { } b2 ? $"  ·  born {b2:yyyy-MM-dd}" : "") : $"  ·  game id {Change.GameId}");
    public string Description => Change.Description;
    public bool CanApply => Change.CanApply;
    public string Note => Change.NameOnly ? "Matched by name only: the birth dates differ. Check it's the same player." :
        Change.Kind == SyncKind.Join && Change.NeedsNewRow ? "In the game as a free agent with no roster row — Sync will create one." :
        Change.Kind == SyncKind.Left ? "Unticked: he may be injured, sent down or gone. Tick it to send him to the AHL affiliate." :
        Change.Kind == SyncKind.Missing ? "Not in the game's database, so it can't be applied yet." : "";
    public string KindText => Change.Kind switch
    {
        SyncKind.Move => "TRADE", SyncKind.Join => "CALL-UP", SyncKind.Update => "NUMBER", SyncKind.Left => "LEFT NHL", _ => "NOT IN GAME",
    };
    public Brush KindBrush => new SolidColorBrush(Change.Kind switch
    {
        SyncKind.Move => Color.FromRgb(0xF2, 0xA6, 0x3B), SyncKind.Join => Color.FromRgb(0x5B, 0xC2, 0x8C), SyncKind.Update => Color.FromRgb(0x7C, 0xA7, 0xF5),
        SyncKind.Left => Color.FromRgb(0xB8, 0xBE, 0xC9), _ => Color.FromRgb(0xE0, 0x6C, 0x6C),
    });

    public bool Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value && CanApply)) _changed?.Invoke(); }
    }
}

/// <summary>
/// The Roster page's NHL sync: fetches every team's current roster from the NHL API, compares it with the game's
/// (RosterSync), lists the differences to tick, and writes the ticked ones into nhl_game.db through the database's mod
/// entry. Applying reads the database from the game again and matches the players afresh, so nothing stale is written.
/// </summary>
public partial class RosterSyncView : UserControl
{
    private ModService _mods;
    private List<NhlPlayer> _api;
    /// <summary>Iowa Wild's AHL roster as fetched, or null when the AHL box is unticked or the fetch failed.</summary>
    private List<AhlPlayer> _ahl;
    /// <summary>The last AHL error (fetch or compare), shown in the status line; null when the AHL part is fine.</summary>
    private string _ahlError;
    private List<SyncRow> _rows = new();
    private bool _busy;

    public event Action<string> StatusChanged;
    /// <summary>Raised after changes were written (the roster list reloads).</summary>
    public event Action Applied;

    public RosterSyncView() => InitializeComponent();

    public void SetMods(ModService mods) => _mods = mods;

    private async void OnFetch(object sender, RoutedEventArgs e) => await FetchAsync();

    /// <summary>Fetches the NHL's rosters and compares them with the game's database as it is now.</summary>
    public async Task FetchAsync()
    {
        if (_mods == null || _busy) return;
        _busy = true;
        SetButtons();
        FetchButton.IsEnabled = false;
        try
        {
            int done = 0;
            var progress = new Progress<string>(t => FetchStatus.Text = $"Fetching NHL rosters… {++done}/{NhlApi.Teams.Length} ({t})");
            var sink = (IProgress<string>)progress;
            _api = await Task.Run(() => NhlApi.AllRostersAsync(t => sink.Report(t)));
            _ahl = null;
            _ahlError = null;
            if (AhlBox.IsChecked == true)
            {
                // The AHL fetch must not wipe the NHL results: it has its own try/catch, and a failure
                // is reported in the status line while the NHL rows are still shown.
                try
                {
                    FetchStatus.Text = "Fetching Iowa Wild's AHL roster…";
                    _ahl = await Task.Run(() => AhlApi.RosterAsync());
                }
                catch (Exception ex) { _ahl = null; _ahlError = ex.Message; }
            }
            FetchStatus.Text = "Comparing with the game…";
            await CompareAsync();
            FetchStatus.Text = $"{_api.Count} players on NHL rosters" + (_ahl == null ? "" : $", {_ahl.Count} on Iowa Wild") +
                $", fetched {DateTime.Now:HH:mm}. " + Counts() + (_ahlError == null ? "" : $" AHL failed: {_ahlError}.");
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
            FetchLabel.Text = _api == null ? "Fetch NHL rosters" : "Fetch again";
            SetButtons();
        }
    }

    /// <summary>Compares the fetched rosters with a database read from the game now.</summary>
    private async Task CompareAsync()
    {
        var db = await _mods.OpenDbAsync(Roster.Database);
        bool sizes = SizesBox.IsChecked == true;
        var api = _api;
        var changes = await Task.Run(() => RosterSync.Compare(db, api, sizes));
        if (_ahl != null)
        {
            // An AHL compare failure (e.g. Iowa Wild not in this database) must not wipe the NHL rows:
            // keep them, remember the error for the status line, and try again on the next fetch.
            var ahl = _ahl;
            try { changes.AddRange(await Task.Run(() => RosterSync.CompareAhl(db, ahl, RosterSync.ResolveIowaTeamIndex(db), sizes))); _ahlError = null; }
            catch (Exception ex) { _ahlError = ex.Message; }
        }
        _rows = changes.Select(c => new SyncRow(c, SetButtons)).ToList();
        ApplyFilter();
    }

    private string Counts()
    {
        int N(SyncKind k) => _rows.Count(r => !r.Change.IsAhl && r.Change.Kind == k);
        int A(SyncKind k) => _rows.Count(r => r.Change.IsAhl && r.Change.Kind == k);
        string s = $"{N(SyncKind.Move)} trades, {N(SyncKind.Join)} call-ups/signings, {N(SyncKind.Update)} number changes, " +
               $"{N(SyncKind.Left)} no longer on an NHL roster, {N(SyncKind.Missing)} not in the game.";
        if (_rows.Any(r => r.Change.IsAhl))
            s += $" Iowa Wild: {A(SyncKind.Join)} call-ups, {A(SyncKind.Move)} moves, {A(SyncKind.Update)} number changes, {A(SyncKind.Missing)} not in the game.";
        return s;
    }

    private SyncKind? Kind => KindMove.IsChecked == true ? SyncKind.Move : KindJoin.IsChecked == true ? SyncKind.Join :
        KindUpdate.IsChecked == true ? SyncKind.Update : KindLeft.IsChecked == true ? SyncKind.Left : KindMissing.IsChecked == true ? SyncKind.Missing : null;

    private IEnumerable<SyncRow> Shown => ChangeList.ItemsSource as IEnumerable<SyncRow> ?? Enumerable.Empty<SyncRow>();

    private void ApplyFilter()
    {
        if (ChangeList == null) return;
        var kind = Kind;
        string q = SyncSearch.Text.Trim();
        var rows = _rows.Where(r => (kind == null || r.Change.Kind == kind) &&
            (q.Length == 0 || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
             || (r.Change.Api?.Team ?? r.Change.Ahl?.TeamName ?? "").Equals(q, StringComparison.OrdinalIgnoreCase))).ToList();
        ChangeList.ItemsSource = rows;
        EmptyText.Text = _api == null ? "Press Fetch NHL rosters to compare the game with today's NHL." : "Nothing here: the game already matches.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SetButtons();
    }

    private void OnFilter(object sender, RoutedEventArgs e) => ApplyFilter();

    private async void OnSizes(object sender, RoutedEventArgs e)
    {
        if (_api == null || _busy) return;
        await CompareAsync();
    }

    /// <summary>Ticking/unticking the AHL box after a fetch: fetch Iowa only (not all 32 NHL teams again), or drop the AHL rows.</summary>
    private async void OnAhl(object sender, RoutedEventArgs e)
    {
        if (_api == null || _busy) return;
        if (AhlBox.IsChecked == true && _ahl == null)
        {
            try { _ahl = await Task.Run(() => AhlApi.RosterAsync()); _ahlError = null; }
            catch (Exception ex) { _ahl = null; _ahlError = ex.Message; FetchStatus.Text = "Couldn't fetch the AHL roster: " + ex.Message; return; }
        }
        else if (AhlBox.IsChecked != true) { _ahl = null; _ahlError = null; }
        await CompareAsync();
        FetchStatus.Text = $"{_api.Count} players on NHL rosters" + (_ahl == null ? "" : $", {_ahl.Count} on Iowa Wild") + ". " + Counts() +
            (_ahlError == null ? "" : $" AHL: {_ahlError}");
    }

    private void OnTickAll(object sender, RoutedEventArgs e) { foreach (var r in Shown) r.Selected = true; }
    private void OnTickNone(object sender, RoutedEventArgs e) { foreach (var r in Shown) r.Selected = false; }

    private void SetButtons()
    {
        int n = _rows.Count(r => r.Selected);
        ApplyButton.IsEnabled = !_busy && n > 0;
        TickAll.IsEnabled = TickNone.IsEnabled = !_busy && Shown.Any();
        ApplyLabel.Text = n > 0 ? $"Apply {n} to game" : "Apply to game";
        ApplyStatus.Text = _api == null ? "" : $"{n} of {_rows.Count} ticked. Players who change team get their line spots cleared; the game puts them in its own lines.";
    }

    private async void OnApply(object sender, RoutedEventArgs e) => await ApplyAsync();

    /// <summary>Writes the ticked changes into the game: a fresh read of the database, matched again, then the same players' changes applied.</summary>
    public async Task ApplyAsync()
    {
        // IsAhl is part of the key: an NHL change and an AHL change for the same player/kind/team must not be confused.
        var picked = _rows.Where(r => r.Selected).Select(r => (r.Change.GameId, r.Change.Kind, r.Change.ToTeam, r.Change.IsAhl)).ToHashSet();
        if (picked.Count == 0 || _busy) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _busy = true;
        SetButtons();
        var api = _api;
        var ahl = _ahl;
        bool sizes = SizesBox.IsChecked == true;
        StatusChanged?.Invoke($"Writing {picked.Count} roster change(s) into the game…");
        try
        {
            await _mods.SaveDbAsync(Roster.Database, db =>
            {
                // Re-compare BOTH lists fresh: ApplyAsync used to re-run only the NHL compare, so ticked AHL rows
                // were silently dropped (the fresh list had no match for them and nothing was written for them).
                var fresh = RosterSync.Compare(db, api, sizes).Where(c => picked.Contains((c.GameId, c.Kind, c.ToTeam, c.IsAhl))).ToList();
                if (ahl != null)
                    fresh.AddRange(RosterSync.CompareAhl(db, ahl, RosterSync.ResolveIowaTeamIndex(db), sizes)
                        .Where(c => picked.Contains((c.GameId, c.Kind, c.ToTeam, c.IsAhl))));
                return fresh.Count == 0 ? null : RosterSync.Apply(db, fresh) + $" ({DateTime.Now:yyyy-MM-dd})";
            }, "Roster sync");
            StatusChanged?.Invoke($"{picked.Count} roster change(s) written into the game. The Mods page can restore the original.");
            Applied?.Invoke();
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
        await CompareAsync();
        FetchStatus.Text = $"{_api.Count} players on NHL rosters" + (_ahl == null ? "" : $", {_ahl.Count} on Iowa Wild") + ". After applying: " + Counts() +
            (_ahlError == null ? "" : $" AHL: {_ahlError}");
    }

    /// <summary>Shows one kind of change (scripted runs): move, join, update, left, missing.</summary>
    public void ShowKind(string kind)
    {
        (kind switch { "move" => KindMove, "join" => KindJoin, "update" => KindUpdate, "left" => KindLeft, "missing" => KindMissing, _ => KindAll }).IsChecked = true;
    }
}
