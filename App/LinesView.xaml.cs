using System.Windows;
using System.Windows.Controls;
using NHL27Kit.Core;
using LinePlayer = NHL27Kit.Core.RosterLines.LinePlayer;

namespace NHL27Kit.App;

/// <summary>One line spot (l1lw, pp_1c…) and the player in it.</summary>
public sealed class LineSlot : Observable
{
    private readonly Action<LineSlot, LinePlayer> _picked;
    private LinePlayer _selected;

    public LineSlot(string slot, string label, IReadOnlyList<LinePlayer> choices, LinePlayer holder, Action<LineSlot, LinePlayer> picked)
    {
        Slot = slot;
        Label = label;
        Choices = choices;
        _selected = holder;
        Original = holder;
        _picked = picked;
    }

    public string Slot { get; }
    public string Label { get; }
    public IReadOnlyList<LinePlayer> Choices { get; }
    public LinePlayer Original { get; private set; }
    public double Width => 190;
    public bool IsChanged => Selected?.PlayerIndex != Original?.PlayerIndex;

    /// <summary>The picker's player; a pick by the user goes through the view (which may swap).</summary>
    public LinePlayer Selected
    {
        get => _selected;
        set { if (value != null && value != _selected) _picked(this, value); }
    }

    /// <summary>Sets the player without asking the view (swaps, reloads).</summary>
    public void Put(LinePlayer p)
    {
        _selected = p;
        Raise(nameof(Selected));
        Raise(nameof(IsChanged));
    }

    public void Saved() { Original = _selected; Raise(nameof(IsChanged)); }
}

public sealed class LineUnit
{
    public string Label { get; init; }
    public List<LineSlot> Slots { get; init; }
}

public sealed class LineCategory
{
    public string Title { get; init; }
    public List<LineUnit> Units { get; init; }
}

/// <summary>
/// The Roster page's line editor: every unit of a team (lines and D pairs, PP, PP 4-on-3, PK, 4-on-4, 3-on-3, shootout,
/// goalies) with a player picker per spot. A player is in at most one unit of a kind, so picking someone who is already
/// in another unit of that kind swaps the two. Save writes the team's slots into both roster tables (players without a
/// spot are scratched); Auto-build fills everything as the NHL sync does. The database is read from the game each time.
/// </summary>
public partial class LinesView : UserControl
{
    private static readonly LinePlayer Nobody = new(-1, "(empty)", -1, 0, true, false);

    private ModService _mods;
    private int _team = -1;
    private string _teamName;
    private List<LineSlot> _slots = new();
    private List<LinePlayer> _players = new();
    private bool _busy, _suppress;

    public event Action<string> StatusChanged;

    public LinesView() => InitializeComponent();

    public void SetMods(ModService mods) => _mods = mods;

    /// <summary>Lists the teams (NHL first) and shows the picked one, read from the game now.</summary>
    public async Task ShownAsync(int selectTeam = -1)
    {
        if (_mods == null) return;
        if (TeamPicker.Items.Count == 0)
        {
            var db = await _mods.OpenDbAsync(Roster.Database);
            var roster = await Task.Run(() => new Roster(db));
            var nhl = await Task.Run(() => RosterSync.TeamIndex(db).Values.ToHashSet());
            var teams = roster.Teams.Values.Where(t => !string.IsNullOrEmpty(t.Name))
                .OrderBy(t => nhl.Contains(t.Index) ? 0 : 1).ThenBy(t => t.Name)
                .Select(t => new ComboBoxItem { Content = $"{t.Name}  ({t.Abbr})", Tag = t.Index }).ToList();
            _suppress = true;
            TeamPicker.ItemsSource = teams;
            _suppress = false;
        }
        var items = (List<ComboBoxItem>)TeamPicker.ItemsSource;
        var pick = items.FirstOrDefault(i => (int)i.Tag == (selectTeam >= 0 ? selectTeam : _team)) ?? items.FirstOrDefault();
        if (pick != null && TeamPicker.SelectedItem != pick) TeamPicker.SelectedItem = pick;
        else if (pick != null) await LoadAsync((int)pick.Tag);
    }

    private async void OnTeamPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || TeamPicker.SelectedItem is not ComboBoxItem item) return;
        if (_slots.Any(s => s.IsChanged) && !App.Args.ContainsKey("shot") &&
            MessageBox.Show(Window.GetWindow(this), $"Drop the unsaved line changes for {_teamName}?", "HMS", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
        {
            _suppress = true;
            TeamPicker.SelectedItem = ((List<ComboBoxItem>)TeamPicker.ItemsSource).FirstOrDefault(i => (int)i.Tag == _team);
            _suppress = false;
            return;
        }
        await LoadAsync((int)item.Tag);
    }

    /// <summary>Reads the team's lines from the game and shows them (or, with <paramref name="built"/>, a built set over them).</summary>
    private async Task LoadAsync(int team, Dictionary<string, int> built = null)
    {
        _team = team;
        _teamName = (TeamPicker.SelectedItem as ComboBoxItem)?.Content as string;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var (players, slots, names) = await Task.Run(() =>
        {
            var (p, s) = RosterLines.ReadTeam(db, team);
            return (p, s, RosterLines.SlotNames(db));
        });
        _players = players;
        var choices = new[] { Nobody }.Concat(players).ToList();
        LinePlayer Of(int pi) => pi < 0 ? Nobody : players.FirstOrDefault(p => p.PlayerIndex == pi) ?? Nobody;
        _slots = names.Select(n => new LineSlot(n, SlotLabel(n), choices, Of(slots[n]), OnPicked)).ToList();
        if (built != null)
            foreach (var s in _slots) s.Put(Of(built.GetValueOrDefault(s.Slot, -1)));
        Categories.ItemsSource = _slots.GroupBy(s => RosterLines.Category(s.Slot))
            .Select(c => new LineCategory
            {
                Title = CategoryTitle(c.Key),
                Units = c.GroupBy(s => RosterLines.Unit(s.Slot)).Select(u => new LineUnit { Label = UnitLabel(u.Key), Slots = u.ToList() }).ToList(),
            }).ToList();
        AutoButton.IsEnabled = players.Count > 0;
        Update(built != null ? "Auto-built from positions and overall ratings. Save to put it in the game." : $"{players.Count} players, read from the game at {DateTime.Now:HH:mm:ss}.");
    }

    /// <summary>A pick: the player leaves any other unit of the same kind, and whoever was in this spot takes his place there.</summary>
    private void OnPicked(LineSlot slot, LinePlayer p)
    {
        var old = slot.Selected;
        string cat = RosterLines.Category(slot.Slot);
        if (p != Nobody)
            foreach (var other in _slots.Where(s => s != slot && s.Selected == p && RosterLines.Category(s.Slot) == cat).ToList())
                other.Put(old ?? Nobody);
        slot.Put(p);
        Update(null);
    }

    private void Update(string idle)
    {
        int changed = _slots.Count(s => s.IsChanged);
        int empty = _slots.Count(s => s.Selected == Nobody);
        SaveButton.IsEnabled = !_busy && changed > 0;
        DiscardButton.IsEnabled = !_busy && changed > 0;
        Status.Text = changed > 0 ? $"{changed} spot(s) changed, not saved yet." + (empty > 0 ? $" {empty} spot(s) are empty: the game wants every spot filled." : "")
            : idle ?? Status.Text;
        var inLines = _slots.Select(s => s.Selected.PlayerIndex).ToHashSet();
        var scratched = _players.Where(p => !inLines.Contains(p.PlayerIndex)).Select(p => $"{p.Name} ({p.PositionName} {p.Overall})").ToList();
        Scratched.Text = scratched.Count == 0 ? "" : "SCRATCHED (in no unit):  " + string.Join(",  ", scratched);
    }

    private async void OnDiscard(object sender, RoutedEventArgs e) => await LoadAsync(_team);

    private async void OnAuto(object sender, RoutedEventArgs e)
    {
        if (_team < 0) return;
        // built in a database read just now and shown; nothing is written until Save
        var db = await _mods.OpenDbAsync(Roster.Database);
        int team = _team;
        var built = await Task.Run(() => { RosterLines.BuildTeam(db, team); return RosterLines.ReadTeam(db, team).Slots; });
        await LoadAsync(team, built);
        foreach (var s in _slots) s.Put(s.Selected);
        Update(null);
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveAsync();

    /// <summary>Writes the team's lines into the game (both roster tables) and reads them back.</summary>
    public async Task SaveAsync()
    {
        if (_busy || _team < 0 || !_slots.Any(s => s.IsChanged)) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var map = _slots.ToDictionary(s => s.Slot, s => s.Selected.PlayerIndex);
        int team = _team;
        string name = _teamName;
        _busy = true;
        Update(null);
        try
        {
            await _mods.SaveDbAsync(Roster.Database, db => { RosterLines.WriteTeam(db, team, map); return $"Lines: {name}"; }, "Lines editor");
            StatusChanged?.Invoke($"{name}'s lines written into the game. The Mods page can restore the original.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't save the lines: " + ex.Message);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't save the lines", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        await LoadAsync(team);
    }

    /// <summary>Puts a player in a spot (scripted runs): "l1lw=McKenna".</summary>
    public void Pick(string spec)
    {
        var parts = spec.Split('=', 2);
        if (parts.Length == 2 && _slots.FirstOrDefault(s => s.Slot == parts[0]) is { } slot &&
            _players.FirstOrDefault(p => p.Name.Contains(parts[1], StringComparison.OrdinalIgnoreCase)) is { } p)
            OnPicked(slot, p);
    }

    public Task AutoBuildAsync() { OnAuto(null, null); return Task.Delay(2500); }

    private static string CategoryTitle(string cat) => cat switch
    {
        "g" => "GOALIES", "l" => "EVEN STRENGTH  ·  LINES AND D PAIRS", "pp" => "POWER PLAY", "pp4" => "POWER PLAY  ·  4 ON 3",
        "pk4" => "PENALTY KILL  ·  4 SKATERS", "pk3" => "PENALTY KILL  ·  3 SKATERS", "ev4" => "4 ON 4", "ev3" => "3 ON 3 (REGULATION)",
        "three" => "3 ON 3 OVERTIME", "s" => "SHOOTOUT", _ => cat.ToUpperInvariant(),
    };

    private static string UnitLabel(string unit) => unit switch
    {
        "g" => "Goalies", "s" => "Shooters", "three" => "Overtime unit",
        _ when unit.StartsWith("l") => $"Line {unit[1..]}",
        _ => $"Unit {unit[^1..]}",
    };

    private static string SlotLabel(string slot)
    {
        if (slot is "g1") return "STARTER";
        if (slot is "g2") return "BACKUP";
        if (System.Text.RegularExpressions.Regex.IsMatch(slot, @"^s\d$")) return $"SHOOTER {slot[1..]}";
        if (slot.StartsWith("three_")) return slot[6..] switch { "c" => "C", "w" => "W", "d" => "D", "s1" => "SKATER", "s2" => "SKATER", "g1" => "G", "g2" => "BACKUP G", var x => x.ToUpperInvariant() };
        return System.Text.RegularExpressions.Regex.Match(slot, @"(lw|rw|c|ld|rd)$").Value.ToUpperInvariant();
    }
}
