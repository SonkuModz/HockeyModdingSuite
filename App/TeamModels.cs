using System.Globalization;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A team or arena in a Teams page picker (Value is the team's or arena's index).</summary>
public sealed record PickItem(string Label, int Value)
{
    public override string ToString() => Label;
}

/// <summary>A team in the Teams page list.</summary>
public sealed class TeamListRow
{
    public TeamSummary Team { get; init; }
    public string Display => string.IsNullOrWhiteSpace(Team.Name) ? $"Team {Team.Index}" : Team.Name;
    public string Meta => $"{Team.League}  ·  {Team.Abbr}  ·  #{Team.Index}" + (Team.Active ? "" : "  ·  inactive");
}

/// <summary>A titled group of values on the Teams page.</summary>
public sealed class TeamSection
{
    public string Title { get; init; }
    public string Caption { get; init; }
    public List<TeamValueRow> Rows { get; init; }
}

/// <summary>One editable value of the team (or its arena or palette), bound to a field of a database row.</summary>
public sealed class TeamValueRow : Observable
{
    private readonly Action<TeamValueRow> _changed;
    private object _original;
    private string _text;
    private bool _bool;
    private PickItem _pick;

    public TeamValueRow(TeamField field, int row, object original, int maxBytes, List<PickItem> picks, Action<TeamValueRow> changed)
    {
        Field = field;
        Row = row;
        MaxBytes = maxBytes;
        Picks = picks;
        _changed = changed;
        Load(original);
    }

    public TeamField Field { get; }
    /// <summary>The row of <see cref="TeamField.Table"/> the value is in.</summary>
    public int Row { get; }
    public int MaxBytes { get; }
    public List<PickItem> Picks { get; }

    public string Label => Field.Label;
    public string Hint => Field.Hint;
    public bool HasHint => !string.IsNullOrEmpty(Field.Hint);
    public bool IsText => Field.Kind is TeamFieldKind.Text or TeamFieldKind.Number;
    public bool IsColor => Field.Kind == TeamFieldKind.Color;
    public bool IsToggle => Field.Kind == TeamFieldKind.Toggle;
    public bool IsPick => Field.Kind is TeamFieldKind.Team or TeamFieldKind.Arena;

    public string Text { get => _text; set { if (Set(ref _text, value)) Edited(); } }
    public bool BoolValue { get => _bool; set { if (Set(ref _bool, value)) Edited(); } }
    public PickItem PickValue { get => _pick; set { if (Set(ref _pick, value)) Edited(); } }

    /// <summary>The value as the database stores it (int or string), or null when the box can't be read.</summary>
    public object Value => Field.Kind switch
    {
        TeamFieldKind.Text => _text ?? "",
        TeamFieldKind.Number => int.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : null,
        TeamFieldKind.Color => TeamEditor.ParseHex(_text),
        TeamFieldKind.Toggle => _bool ? 1 : 0,
        _ => _pick?.Value,
    };

    public string Error =>
        Value == null ? (IsColor ? "Not a hex colour, like #1A2B3C" : "Not a whole number")
        : Field.Kind == TeamFieldKind.Text && System.Text.Encoding.UTF8.GetByteCount(_text ?? "") > MaxBytes ? $"At most {MaxBytes} bytes"
        : null;

    public bool HasError => Error != null;
    public bool IsChanged => Value != null && !Equals(Value, _original);
    public string Was => IsChanged ? "was " + Show(_original) : null;
    public bool ShowWas => IsChanged;

    public Brush Swatch => IsColor && Value is int c ? new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)) : Brushes.Transparent;

    private string Show(object v) => Field.Kind switch
    {
        TeamFieldKind.Color => TeamEditor.Hex((int)v),
        TeamFieldKind.Toggle => (int)v == 1 ? "on" : "off",
        TeamFieldKind.Team or TeamFieldKind.Arena => Picks.FirstOrDefault(p => p.Value == (int)v)?.Label ?? v.ToString(),
        _ => $"\"{v}\"",
    };

    /// <summary>Puts an unsaved value back into the box (the team was shown again before saving).</summary>
    public void Restore(object v)
    {
        object keep = _original;
        Load(v);
        _original = keep;
    }

    private void Load(object v)
    {
        _original = v;
        switch (Field.Kind)
        {
            case TeamFieldKind.Color: _text = TeamEditor.Hex((int)v); break;
            case TeamFieldKind.Toggle: _bool = (int)v == 1; break;
            case TeamFieldKind.Team or TeamFieldKind.Arena:
                _pick = Picks.FirstOrDefault(p => p.Value == (int)v) ?? new PickItem($"#{v}", (int)v);
                if (!Picks.Contains(_pick)) Picks.Insert(0, _pick);
                break;
            default: _text = Convert.ToString(v, CultureInfo.InvariantCulture); break;
        }
    }

    public void Reset() => Edited(() => Load(_original));

    private void Edited(Action change = null)
    {
        change?.Invoke();
        foreach (string p in new[] { nameof(Text), nameof(BoolValue), nameof(PickValue), nameof(Swatch), nameof(IsChanged), nameof(HasError), nameof(Error), nameof(Was), nameof(ShowWas) })
            Raise(p);
        _changed?.Invoke(this);
    }
}

/// <summary>One logo texture on the Teams page, with its thumbnail (loaded from the game when shown).</summary>
public sealed class TeamLogoRow : Observable
{
    private System.Windows.Media.Imaging.BitmapSource _thumb;
    private string _size = "";
    private bool _busy;

    public TeamLogo Logo { get; init; }
    public string Label => Logo.Label;
    public string Tip => Logo.Name;
    public System.Windows.Media.Imaging.BitmapSource Thumb { get => _thumb; set => Set(ref _thumb, value); }
    public string Size { get => _size; set => Set(ref _size, value); }
    public bool Busy { get => _busy; set => Set(ref _busy, value); }
}

/// <summary>A group of logo styles (Logo, Rink, On backgrounds…).</summary>
public sealed class TeamLogoGroup
{
    public string Title { get; init; }
    public List<TeamLogoRow> Logos { get; init; }
}
