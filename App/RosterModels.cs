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
        Search = $"{p.Name} {p.TeamAbbr} {p.PositionName} {(p.Jersey >= 0 ? "#" + p.Jersey : "")}".ToLowerInvariant();
    }

    public RosterPlayer Player { get; }
    public string Search { get; }
    public string Name => Player.Name;
    public string Meta => $"{Player.TeamAbbr}  ·  {Player.PositionName}{(Player.Jersey >= 0 ? "  ·  #" + Player.Jersey : "")}";
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
    };

    public static string Label(string field)
    {
        if (Labels.TryGetValue(field, out var l)) return l;
        string s = field.StartsWith("c_") ? field[2..] : field;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace('_', ' '));
    }
}

/// <summary>One editable value of a player: a field of one of his rows, shown with an offset (height and weight).</summary>
public sealed class FieldEdit : Observable
{
    private string _text;

    public FieldEdit(T3Db db, T3Table table, int row, T3Field field, int offset = 0)
    {
        Table = table.Name;
        Row = row;
        Field = field.Name;
        Type = field.Type;
        Offset = offset;
        Label = RosterVisuals.Label(field.Name);
        var v = db.Get(table, row, field);
        Original = field.Type == T3FieldType.Integer ? ((int)v + offset).ToString(CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture);
        _text = Original;
        if (field.Type == T3FieldType.Integer)
        {
            Min = field.RangeLow + offset;
            Max = (int)Math.Min(int.MaxValue, field.RangeLow + (1L << field.Depth) - 1) + offset;
            if (field.RangeHigh > field.RangeLow) Max = Math.Min(Max, field.RangeHigh + offset);
        }
        else MaxBytes = field.Type == T3FieldType.String ? field.Depth / 8 - 1 : 0;
    }

    public string Table { get; }
    public int Row { get; }
    public string Field { get; }
    public T3FieldType Type { get; }
    public int Offset { get; }
    public string Label { get; }
    public string Original { get; }
    public int Min { get; }
    public int Max { get; }
    public int MaxBytes { get; }
    public bool IsRating { get; init; }
    public bool IsNumber => Type == T3FieldType.Integer;
    public double BoxWidth => IsNumber ? 70 : 170;
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
                return !int.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? "Not a number"
                    : v < Min || v > Max ? $"{Min}–{Max}" : null;
            return System.Text.Encoding.UTF8.GetByteCount(_text ?? "") > MaxBytes ? $"Max {MaxBytes} bytes" : null;
        }
    }

    public bool HasError => Error != null;

    /// <summary>The value as stored (display value minus the offset).</summary>
    public object Value => IsNumber ? int.Parse(_text, CultureInfo.InvariantCulture) - Offset : _text;

    /// <summary>A 0-1 bar for ratings (0-99).</summary>
    public double Bar => int.TryParse(_text, out int v) ? Math.Clamp(v / 99.0, 0, 1) * 120 : 0;
    public Brush BarBrush => RosterVisuals.RatingBrush(int.TryParse(_text, out int v) ? v : 0);
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
