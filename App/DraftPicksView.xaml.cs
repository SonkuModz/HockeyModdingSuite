using System.Windows.Controls;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>How a chip in a picks-grid cell relates to the selected team.</summary>
public enum DraftPickChipKind { Own, TradedAway, Acquired, Missing }

/// <summary>One chip inside a grid cell: a single pick, shown as its team's abbreviation.</summary>
public sealed class DraftPickChip
{
    public DraftPickChip(string abbr, DraftPickChipKind kind, string tip)
    {
        Abbr = abbr;
        Kind = kind;
        Tip = tip;
    }

    public string Abbr { get; }
    public DraftPickChipKind Kind { get; }
    public string Tip { get; }
}

/// <summary>
/// One cell of the picks grid: a round of a draft year. It holds a chip for the team's original pick
/// (white if still held, red if it now belongs to another team) plus a green chip for every pick the
/// team acquired from another team in that year and round.
/// </summary>
public sealed class DraftPickCell
{
    public DraftPickCell(int round, List<DraftPickChip> chips)
    {
        Round = round;
        Chips = chips;
    }

    public int Round { get; }
    public List<DraftPickChip> Chips { get; }
    public string RoundLabel => $"R{Round}";
    public bool IsEmpty => Chips.Count == 0;
}

/// <summary>One row of the picks grid: a draft year and its seven rounds.</summary>
public sealed class DraftPickYear
{
    public DraftPickYear(int year, List<DraftPickCell> cells, bool isPast)
    {
        Year = year;
        Cells = cells;
        IsPast = isPast;
    }

    public int Year { get; }
    public int DisplayYear => Year + 1900;
    public List<DraftPickCell> Cells { get; }
    public bool IsPast { get; }
    public string Title => IsPast ? $"{DisplayYear}  ·  completed draft" : DisplayYear.ToString();
    public double RowOpacity => IsPast ? 0.55 : 1;
}

/// <summary>
/// The Roster page's draft picks tab (read-only): a team's picks, one row per draft year, with a chip
/// per pick in each round — its own originals and everything it acquired from other teams. The
/// database is read from the game each time a team is picked; nothing is written.
/// </summary>
public partial class DraftPicksView : UserControl
{
    private ModService _mods;
    private int _team = -1;
    private bool _suppress;

    public event Action<string> StatusChanged;

    public DraftPicksView() => InitializeComponent();

    public void SetMods(ModService mods) => _mods = mods;

    /// <summary>Lists the NHL teams and shows the picked one's picks, read from the game now.</summary>
    public async Task ShownAsync(int selectTeam = -1)
    {
        if (_mods == null) return;
        if (TeamPicker.Items.Count == 0)
        {
            var db = await _mods.OpenDbAsync(Roster.Database);
            var roster = await Task.Run(() => new Roster(db));
            var nhl = await Task.Run(() => RosterSync.TeamIndex(db).Values.ToHashSet());
            var teams = roster.Teams.Values.Where(t => !string.IsNullOrEmpty(t.Name) && (nhl.Count == 0 || nhl.Contains(t.Index)))
                .OrderBy(t => t.Name)
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
        await LoadAsync((int)item.Tag);
    }

    /// <summary>Reads the team's picks from the game and shows them, one chip per pick.</summary>
    private async Task LoadAsync(int team)
    {
        _team = team;
        var db = await _mods.OpenDbAsync(Roster.Database);
        var (picks, roster) = await Task.Run(() => (new DraftPicks(db), new Roster(db)));
        string Abbr(int idx) => idx < 0 ? "—" : roster.Teams.TryGetValue(idx, out var t) ? t.Abbr : $"#{idx}";

        var originals = picks.OriginalsOf(team).ToList();
        var owned = picks.OwnedBy(team).ToList();
        var rows = picks.Years.Select(y =>
        {
            var cells = Enumerable.Range(1, 7).Select(round =>
            {
                var chips = new List<DraftPickChip>();
                var original = originals.FirstOrDefault(p => p.Year == y && p.Round == round);
                if (original != null)
                {
                    if (original.Owner == team)
                        chips.Add(new DraftPickChip(Abbr(team), DraftPickChipKind.Own, "Own pick — still held."));
                    else if (original.Owner >= 0)
                        chips.Add(new DraftPickChip(Abbr(original.Owner), DraftPickChipKind.TradedAway,
                            $"Traded away — now owned by {Abbr(original.Owner)}."));
                    else
                        chips.Add(new DraftPickChip("—", DraftPickChipKind.Missing, "No owner in the game for this pick."));
                }
                foreach (var a in owned.Where(p => p.Year == y && p.Round == round && p.OriginalTeam != team && p.OriginalTeam >= 0)
                                        .OrderBy(p => Abbr(p.OriginalTeam)))
                    chips.Add(new DraftPickChip(Abbr(a.OriginalTeam), DraftPickChipKind.Acquired,
                        $"Acquired — originally {Abbr(a.OriginalTeam)}'s pick."));
                return new DraftPickCell(round, chips);
            }).ToList();
            return new DraftPickYear(y, cells, y == picks.FirstYear);
        }).ToList();
        Years.ItemsSource = rows;

        Status.Text = $"{originals.Count} original picks  ·  {owned.Count} held now  ·  read from the game at {DateTime.Now:HH:mm:ss}.";
    }
}
