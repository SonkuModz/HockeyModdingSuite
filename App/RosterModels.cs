using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A player in the roster list.</summary>
public sealed class PlayerRow
{
    public PlayerRow(RosterPlayer p)
    {
        Player = p;
        Search = $"{p.Name} {p.TeamAbbr} {p.PositionDisplay} {(p.Jersey >= 0 ? "#" + p.Jersey : "")}".ToLowerInvariant();
    }

    public RosterPlayer Player { get; }
    public string Search { get; }
    public string Name => Player.Name;
    public string Meta => $"{Player.TeamAbbr}  ·  {Player.PositionDisplay}{(Player.Jersey >= 0 ? "  ·  #" + Player.Jersey : "")}";
    public string Overall => Player.Overall > 0 ? Player.Overall.ToString() : "–";
    public Brush OverallBrush => RosterVisuals.RatingBrush(Player.Overall);
}

public static class RosterVisuals
{
    public static Brush RatingBrush(int v) => (Brush)Application.Current.FindResource(v >= 85 ? "Good" : v >= 75 ? "Accent" : v >= 65 ? "Patch" : "Muted");

    /// <summary>A team colour (0xRRGGBB) as a brush, or the accent for none.</summary>
    public static Color TeamColor(int rgb) => rgb <= 0 ? (Color)Application.Current.FindResource("AccentColor")
        : Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skill_overall"] = "Overall", ["c_offawareness"] = "Off. awareness", ["c_defawareness"] = "Def. awareness",
        ["c_wristshotpower"] = "Wrist shot power", ["c_wristaccuracy"] = "Wrist shot acc.", ["c_slapshotpower"] = "Slap shot power",
        ["c_slapaccuracy"] = "Slap shot acc.", ["c_puckcontrol"] = "Puck control", ["c_handeye"] = "Hand-eye", ["c_stickchecking"] = "Stick checking",
        ["c_bodychecking"] = "Body checking", ["c_shotblocking"] = "Shot blocking", ["c_fightingskill"] = "Fighting skill",
        ["c_gsh"] = "Glove high", ["c_gsl"] = "Glove low", ["c_ssh"] = "Stick high", ["c_ssl"] = "Stick low", ["c_5hole"] = "Five hole",
        ["c_reboundcontrol"] = "Rebound control", ["c_shotrecover"] = "Shot recovery", ["c_pokecheck"] = "Poke check",
        ["c_puckplayfrequency"] = "Puck play freq.", ["c_breakaway"] = "Breakaway", ["c_paddledown"] = "Paddle down",
        ["firstname"] = "First name", ["lastname"] = "Last name", ["birthplace"] = "Birthplace", ["nickname"] = "Nickname",
        ["jersey"] = "Jersey", ["height"] = "Height (in)", ["weight"] = "Weight (lb)",
        ["positionprimary"] = "Primary position", ["positionsecondary"] = "Secondary position", ["position"] = "Position",
        ["draftyear"] = "Draft year", ["draftround"] = "Draft round", ["draftposition"] = "Draft pick", ["draftteam"] = "Drafted by",
        ["contractdollars"] = "Salary ($)", ["contractlength"] = "Years remaining",
        ["contractis2way"] = "Two-way contract", ["contractismultiyear"] = "Multi-year deal", ["contractneedsentrylevel"] = "Entry-level deal",
        ["extensionlength"] = "Extension years", ["extensiondollars"] = "Extension salary ($)", ["extensionis2way"] = "Extension two-way",
    };

    public static string Label(string field)
    {
        if (Labels.TryGetValue(field, out var l)) return l;
        // trade protection is one clause + team list per season: "Year 3 clause", "Year 3 teams"
        if (field.StartsWith("contractclausemntcnumteams", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(field["contractclausemntcnumteams".Length..], out int m)) return $"Year {m + 1} teams";
        if (field.StartsWith("contractclause", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(field["contractclause".Length..], out int c)) return $"Year {c + 1} clause";
        string s = field.StartsWith("c_") ? field[2..] : field;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace('_', ' '));
    }
}

/// <summary>
/// One editable value of a player: a field of one of his rows, shown with an offset (height and weight) and,
/// where the game stores a unit, a scale (salary and extension dollars are stored in $5,000 units and shown in
/// dollars). Min/Max are in display units; minValue/maxValue pin them to a known domain (e.g. draft round 0-7)
/// instead of whatever the field's own range says.
/// </summary>
public sealed class FieldEdit : Observable
{
    private string _text;

    public FieldEdit(T3Db db, T3Table table, int row, T3Field field, int offset = 0, int scale = 1, int? minValue = null, int? maxValue = null)
    {
        Table = table.Name;
        Row = row;
        Field = field.Name;
        Type = field.Type;
        Offset = offset;
        Scale = scale;
        Label = RosterVisuals.Label(field.Name);
        var v = db.Get(table, row, field);
        Original = field.Type == T3FieldType.Integer ? ((int)v * scale + offset).ToString(CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture);
        _text = Original;
        if (field.Type == T3FieldType.Integer)
        {
            long storedMax = field.RangeLow + (1L << field.Depth) - 1;
            if (field.RangeHigh > field.RangeLow) storedMax = Math.Min(storedMax, field.RangeHigh);
            Min = minValue ?? (int)Math.Clamp((long)field.RangeLow * scale + offset, int.MinValue, int.MaxValue);
            Max = maxValue ?? (int)Math.Clamp(storedMax * scale + offset, int.MinValue, int.MaxValue);
        }
        else MaxBytes = field.Type == T3FieldType.String ? field.Depth / 8 - 1 : 0;
    }

    public string Table { get; }
    public int Row { get; }
    public string Field { get; }
    public T3FieldType Type { get; }
    public int Offset { get; }
    /// <summary>Display units per stored unit (1 except for dollar fields, where it is 5000).</summary>
    public int Scale { get; }
    public string Label { get; }
    public string Original { get; }
    public int Min { get; }
    public int Max { get; }
    public int MaxBytes { get; }
    public bool IsRating { get; init; }
    public bool IsNumber => Type == T3FieldType.Integer;
    public double BoxWidth => IsNumber ? (Scale > 1 ? 110 : 70) : 170;
    public HorizontalAlignment TextAlign => IsNumber ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public double CardWidth => IsNumber ? 236 : 330;

    public string Text
    {
        get => _text;
        set
        {
            if (!Set(ref _text, value)) return;
            Raise(nameof(IsChanged));
            Raise(nameof(Error));
            Raise(nameof(HasError));
            Raise(nameof(Bar));
            Raise(nameof(BarBrush));
        }
    }

    public bool IsChanged => _text != Original;

    public string Error
    {
        get
        {
            if (IsNumber)
            {
                if (!int.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return "Not a number";
                if (v < Min || v > Max) return $"{Min}–{Max}";
                if (Scale > 1 && (v - Offset) % Scale != 0) return $"Multiple of {Scale:N0}";
                return null;
            }
            return System.Text.Encoding.UTF8.GetByteCount(_text ?? "") > MaxBytes ? $"Max {MaxBytes} bytes" : null;
        }
    }

    public bool HasError => Error != null;

    /// <summary>The value as stored (display value minus the offset, divided by the scale).</summary>
    public object Value => IsNumber ? (int.Parse(_text, CultureInfo.InvariantCulture) - Offset) / Scale : _text;

    /// <summary>A 0-1 bar for ratings (0-99).</summary>
    public double Bar => int.TryParse(_text, out int v) ? Math.Clamp(v / 99.0, 0, 1) * 120 : 0;
    public Brush BarBrush => RosterVisuals.RatingBrush(int.TryParse(_text, out int v) ? v : 0);
}

/// <summary>
/// A value shown on the profile but never edited: no database field behind it, so it never counts as a change
/// and never gets written. Shown in a faded (disabled) box, like a disabled control elsewhere in the app.
/// </summary>
public sealed class ReadOnlyField
{
    public ReadOnlyField(string label, string text)
    {
        Label = label;
        Text = text;
    }

    public string Label { get; }
    public string Text { get; }
    /// <summary>Wide enough for the longest team name ("Columbus Blue Jackets") and a "26.32% — $1.84M" share.</summary>
    public double CardWidth => 360;
    public double BoxWidth => 200;
}

/// <summary>A position on the primary/secondary scale: 0=C, 1=LW, 2=RW, 3=LD, 4=RD, 5=G; -1 is "None" (secondary only).</summary>
public sealed record PositionChoice(int Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// A player's positions, edited through the three bio fields (positionprimary, positionsecondary, position) so a
/// change saves, discards and reads back like any other value on the page. The legacy <c>position</c> field is kept
/// in sync with the primary (LD/RD collapse to D, G to 4) because the line builder and the sync read it.
/// The offered positions stay inside the player's group — forwards C/LW/RW, defence LD/RD, goalie G only — so a
/// skater can never be turned into a goalie (or the reverse) here; that would desync the position from the ratings row.
/// </summary>
public sealed class PositionEdit : Observable
{
    private static readonly List<PositionChoice> Forwards = new() { new(0, "C"), new(1, "LW"), new(2, "RW") };
    private static readonly List<PositionChoice> Defence = new() { new(3, "LD"), new(4, "RD") };
    private static readonly List<PositionChoice> Goalies = new() { new(5, "G") };

    public PositionEdit(FieldEdit primary, FieldEdit secondary, FieldEdit legacy)
    {
        PrimaryField = primary;
        SecondaryField = secondary;
        LegacyField = legacy;
        int pv = PrimaryValue;
        PrimaryChoices = (pv == 5 ? Goalies : pv is 3 or 4 ? Defence : Forwards).ToList();
        // a stored value outside the offered set (inconsistent data) is still shown, so the card never lies
        if (PrimaryChoices.All(c => c.Value != pv)) PrimaryChoices.Add(new PositionChoice(pv, Roster.PositionNameFull(pv)));
        foreach (var f in new[] { primary, secondary, legacy })
            f.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FieldEdit.Text)) Changed(); };
    }

    public FieldEdit PrimaryField { get; }
    public FieldEdit SecondaryField { get; }
    public FieldEdit LegacyField { get; }
    public List<PositionChoice> PrimaryChoices { get; }

    /// <summary>The secondary positions: the other spots in the same group, plus None (and the stored secondary
    /// itself, if it sits outside the group, so inconsistent data is shown rather than hidden).</summary>
    public List<PositionChoice> SecondaryChoices
    {
        get
        {
            var list = new List<PositionChoice> { new(-1, "None") };
            list.AddRange(PrimaryChoices.Where(c => c.Value != PrimaryValue));
            int sv = SecondaryValue;
            if (sv >= 0 && list.All(c => c.Value != sv)) list.Add(new PositionChoice(sv, Roster.PositionNameFull(sv)));
            return list;
        }
    }

    public int PrimaryValue => int.TryParse(PrimaryField.Text, out int v) ? v : 0;
    public int SecondaryValue => int.TryParse(SecondaryField.Text, out int v) ? v : -1;
    /// <summary>"C/LW", "RD", "G".</summary>
    public string Display => Roster.PositionDisplay(PrimaryValue, SecondaryValue);

    public PositionChoice Primary
    {
        get => PrimaryChoices.FirstOrDefault(c => c.Value == PrimaryValue) ?? PrimaryChoices[0];
        set
        {
            if (value == null || value.Value == PrimaryValue) return;
            PrimaryField.Text = value.Value.ToString(CultureInfo.InvariantCulture);
            // the legacy field follows the primary, so lines and sync keep reading the right spot
            LegacyField.Text = Roster.CollapsedPosition(value.Value).ToString(CultureInfo.InvariantCulture);
            if (SecondaryValue == value.Value) SecondaryField.Text = "-1";
            Changed();
        }
    }

    public PositionChoice Secondary
    {
        get => SecondaryChoices.FirstOrDefault(c => c.Value == SecondaryValue) ?? SecondaryChoices[0];
        set
        {
            if (value == null || value.Value == SecondaryValue) return;
            SecondaryField.Text = value.Value.ToString(CultureInfo.InvariantCulture);
            Changed();
        }
    }

    public bool IsChanged => PrimaryField.IsChanged || SecondaryField.IsChanged || LegacyField.IsChanged;
    public bool HasError => PrimaryField.HasError || SecondaryField.HasError || LegacyField.HasError;
    public string Error => PrimaryField.Error ?? SecondaryField.Error ?? LegacyField.Error;

    private void Changed()
    {
        foreach (var n in new[] { nameof(Primary), nameof(Secondary), nameof(SecondaryChoices), nameof(Display), nameof(IsChanged), nameof(HasError), nameof(Error) })
            Raise(n);
    }
}

/// <summary>
/// A team picker for one bio field (the drafting team): a ComboBox over the page's team list, backed by a FieldEdit
/// so the value saves and discards like any other field. The list always starts with "Undrafted" (-1); a stored team
/// the list doesn't know is shown as "#index" rather than dropped.
/// </summary>
public sealed class PickEdit : Observable
{
    public PickEdit(FieldEdit field, List<PickItem> picks)
    {
        Field = field;
        Picks = picks;
        field.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FieldEdit.Text)) Changed(); };
    }

    public FieldEdit Field { get; }
    public List<PickItem> Picks { get; }
    public string Label => Field.Label;

    public PickItem Selected
    {
        get => Picks.FirstOrDefault(p => p.Value == Value) ?? Picks[0];
        set
        {
            if (value == null || value.Value == Value) return;
            Field.Text = value.Value.ToString(CultureInfo.InvariantCulture);
            Changed();
        }
    }

    public int Value => int.TryParse(Field.Text, out int v) ? v : -1;
    public bool IsChanged => Field.IsChanged;
    public bool HasError => Field.HasError;
    public string Error => Field.Error;

    private void Changed()
    {
        foreach (var n in new[] { nameof(Selected), nameof(IsChanged), nameof(HasError), nameof(Error) }) Raise(n);
    }
}

/// <summary>A 0/1 bio flag (two-way, multi-year, entry-level) as a checkbox, backed by a FieldEdit.</summary>
public sealed class ToggleEdit : Observable
{
    public ToggleEdit(FieldEdit field)
    {
        Field = field;
        field.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FieldEdit.Text)) Changed(); };
    }

    public FieldEdit Field { get; }
    public string Label => Field.Label;

    public bool BoolValue
    {
        get => Field.Text == "1";
        set
        {
            string t = value ? "1" : "0";
            if (Field.Text != t) Field.Text = t;
            Changed();
        }
    }

    public bool IsChanged => Field.IsChanged;
    public bool HasError => Field.HasError;
    public string Error => Field.Error;

    private void Changed()
    {
        foreach (var n in new[] { nameof(BoolValue), nameof(IsChanged), nameof(HasError), nameof(Error) }) Raise(n);
    }
}

/// <summary>A choice for one trade-protection year: the clause type the game stores (0-4).</summary>
public sealed record ClauseChoice(int Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// One year of trade protection: a clause type (contractclauseN) plus the size of the no-trade list for that year
/// (contractclausemntcnumteamsN). The slots run from the current season through the contract, then the extension.
/// Clause values (ContractSync.ClauseValue; decoded by matching the shipped data against CapWages): 0 = none,
/// 1 = modified no-trade (M-NTC, with a team list), 2 = no-move with a modified no-trade list ("M-NTC, NMC"),
/// 3 = full no-trade (NTC), 4 = full no-move (NMC). A team list only means anything with a clause: picking None
/// clears the list to 0.
/// </summary>
public sealed class ClauseEdit : Observable
{
    public static readonly List<ClauseChoice> Choices = new()
    {
        new(0, "None"), new(1, "No-trade (modified list)"), new(2, "No-move + no-trade list"), new(3, "No-trade (full)"), new(4, "No-move (full)"),
    };

    public ClauseEdit(int year, FieldEdit clause, FieldEdit teams)
    {
        Year = year;
        ClauseField = clause;
        TeamsField = teams;
        foreach (var f in new[] { clause, teams })
            f.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FieldEdit.Text)) Changed(); };
    }

    public int Year { get; }
    public FieldEdit ClauseField { get; }
    public FieldEdit TeamsField { get; }
    public string YearLabel => $"Year {Year}";

    public int ClauseValue => int.TryParse(ClauseField.Text, out int v) ? v : 0;
    public int TeamsValue => int.TryParse(TeamsField.Text, out int v) ? v : 0;
    /// <summary>The team list only applies with a clause; with None it is forced to 0.</summary>
    public bool TeamsEnabled => ClauseValue != 0;

    public ClauseChoice Selected
    {
        get => Choices.FirstOrDefault(c => c.Value == ClauseValue) ?? Choices[0];
        set
        {
            if (value == null || value.Value == ClauseValue) return;
            ClauseField.Text = value.Value.ToString(CultureInfo.InvariantCulture);
            if (value.Value == 0) TeamsField.Text = "0";
            Changed();
        }
    }

    public bool IsChanged => ClauseField.IsChanged || TeamsField.IsChanged;
    public bool HasError => ClauseField.HasError || TeamsField.HasError || CrossError != null;
    /// <summary>A team list with no clause is inconsistent data: the list would never be read.</summary>
    public string CrossError => !ClauseField.HasError && !TeamsField.HasError && ClauseValue == 0 && TeamsValue != 0
        ? "No clause this year — set the teams to 0." : null;
    public string Error => ClauseField.Error ?? TeamsField.Error ?? CrossError;

    private void Changed()
    {
        foreach (var n in new[] { nameof(Selected), nameof(TeamsEnabled), nameof(IsChanged), nameof(HasError), nameof(Error), nameof(CrossError) })
            Raise(n);
    }
}

/// <summary>A localized string in the Text list.</summary>
public sealed class LocRow
{
    public string Table { get; init; }
    public int Row { get; init; }
    public string Id { get; init; }
    public string Text { get; init; }
    public string Preview => Text.Length > 140 ? Text[..140].Replace('\n', ' ') + "…" : Text.Replace('\n', ' ');
}

/// <summary>
/// One of a player's three X-Factor slots, edited through its two fields (xfactorNability, xfactorNtier) so a change
/// saves, discards and reads back like any other value on the page.
/// </summary>
public sealed class XFactorSlot : Observable
{
    private ImageSource _icon;

    public XFactorSlot(int slot, FieldEdit ability, FieldEdit tier, IReadOnlyList<XFactorAbility> abilities, string[] tierNames, bool goalie)
    {
        Slot = slot;
        AbilityField = ability;
        TierField = tier;
        TierNames = tierNames;
        // the abilities that fit this player (goalie or skater), "None" first
        Choices = new[] { new XFactorAbility(-1, "None", "") }.Concat(abilities.Where(a => a.IsGoalie == goalie || a.Id == AbilityId)).ToList();
        ability.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FieldEdit.Text)) Changed(); };
        tier.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FieldEdit.Text)) Changed(); };
    }

    public int Slot { get; }
    public FieldEdit AbilityField { get; }
    public FieldEdit TierField { get; }
    public string[] TierNames { get; }
    public List<XFactorAbility> Choices { get; }

    public int AbilityId => int.TryParse(AbilityField.Text, out int v) ? v : -1;
    public int Tier => int.TryParse(TierField.Text, out int v) ? Math.Clamp(v, 1, 3) : 1;
    public bool IsEmpty => AbilityId < 0;
    public bool IsChanged => AbilityField.IsChanged || TierField.IsChanged;

    public XFactorAbility Selected
    {
        get => Choices.FirstOrDefault(c => c.Id == AbilityId) ?? Choices[0];
        set
        {
            if (value == null) return;
            AbilityField.Text = value.Id.ToString(CultureInfo.InvariantCulture);
            // a new ability starts at Specialist
            if (value.Id >= 0 && int.TryParse(TierField.Text, out int t) && t < 1) TierField.Text = "1";
        }
    }

    /// <summary>0-2 for the tier picker (Specialist, All-Star, Elite).</summary>
    public int TierIndex
    {
        get => Tier - 1;
        set { if (value >= 0) TierField.Text = (value + 1).ToString(CultureInfo.InvariantCulture); }
    }

    public string Name => IsEmpty ? "Empty slot" : Selected.Name;
    public string Short => IsEmpty ? "No X-Factor in this slot." : Selected.Short;
    public string TierName => IsEmpty ? "" : TierNames[Tier - 1];
    public Brush TierBrush => new SolidColorBrush(Tier switch { 3 => Color.FromRgb(0xE8, 0x3A, 0x22), 2 => Color.FromRgb(0x5B, 0x8C, 0xF0), _ => Color.FromRgb(0xD8, 0xB0, 0x5A) });
    public ImageSource Icon { get => _icon; set => Set(ref _icon, value); }
    public string IconTexture => IsEmpty ? null : XFactors.Icon(AbilityId, Tier);

    /// <summary>Raised after the ability or tier changes (the page loads the new icon).</summary>
    public event Action<XFactorSlot> IconNeeded;

    private void Changed()
    {
        foreach (var n in new[] { nameof(AbilityId), nameof(Tier), nameof(IsEmpty), nameof(IsChanged), nameof(Selected), nameof(TierIndex),
                                  nameof(Name), nameof(Short), nameof(TierName), nameof(TierBrush), nameof(IconTexture) })
            Raise(n);
        IconNeeded?.Invoke(this);
    }
}
