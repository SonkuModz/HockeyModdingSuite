using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Media;

namespace NHL27Kit.App;

public enum RowKind { Toggle, Choice, Slider, Text }

/// <summary>How a setting is stored: env values are always strings ("1"/"0" for switches).</summary>
public enum RowType { Bool, Int, Number, String }

public sealed record Choice(string Label, string Value)
{
    // the combo box's closed state shows the item itself
    public override string ToString() => Label;
}

/// <summary>One emulator setting on the Play page, bound to a key of the Kyty profile.</summary>
public sealed class SettingRow : Observable
{
    private bool _bool;
    private Choice _choice;
    private double _number;
    private string _text, _source;
    private bool _overridden, _loading;

    public string Key { get; init; }
    public string Label { get; init; }
    public string Hint { get; init; }
    public bool Env { get; init; }
    public RowKind Kind { get; init; }
    public RowType Type { get; init; }
    /// <summary>The emulator's own default, used when no layer sets the key.</summary>
    public string Fallback { get; init; }
    public List<Choice> Choices { get; init; }
    public double Min { get; init; }
    public double Max { get; init; } = 100;
    public double Step { get; init; } = 1;
    public Func<double, string> Format { get; init; } = v => v.ToString("0", CultureInfo.InvariantCulture);
    public bool Mono { get; init; }

    public bool IsToggle => Kind == RowKind.Toggle;
    public bool IsChoice => Kind == RowKind.Choice;
    public bool IsSlider => Kind == RowKind.Slider;
    public bool IsText => Kind == RowKind.Text;
    public bool HasHint => !string.IsNullOrEmpty(Hint);
    public FontFamily LabelFont => (FontFamily)System.Windows.Application.Current.FindResource(Mono ? "MonoFont" : "UiFont");

    public event Action<SettingRow> Changed;

    public bool BoolValue { get => _bool; set { if (Set(ref _bool, value)) Edited(); } }
    public Choice ChoiceValue { get => _choice; set { if (Set(ref _choice, value)) Edited(); } }
    public double NumberValue
    {
        get => _number;
        set
        {
            value = Math.Round(value / Step) * Step;
            if (Set(ref _number, value)) { OnNumberText(); Edited(); }
        }
    }
    public string NumberText => Format(_number);
    public string TextValue { get => _text; set { if (Set(ref _text, value)) Edited(); } }
    public string Source { get => _source; private set { if (Set(ref _source, value)) OnSourceBrush(); } }
    public bool Overridden { get => _overridden; private set => Set(ref _overridden, value); }
    public Brush SourceBrush => (Brush)System.Windows.Application.Current.FindResource(Source switch
    {
        "Profile" => "Accent",
        "Global" => "Violet",
        "Build" => "TextDim",
        _ => "Faint",
    });

    private void OnNumberText() => Raise(nameof(NumberText));
    private void OnSourceBrush() => Raise(nameof(SourceBrush));

    private void Edited()
    {
        if (!_loading) Changed?.Invoke(this);
    }

    /// <summary>Shows the value in effect and where it comes from.</summary>
    public void Load(KytySetup setup)
    {
        var (node, from) = setup.Get(Key, Env);
        Show(node is JsonValue v ? v.ToString() : Fallback);
        Source = from == KytySetup.Layer.Default ? "Default" : from.ToString();
        Overridden = from == KytySetup.Layer.Profile;
    }

    /// <summary>Shows a value from a single file (no layers to say it came from).</summary>
    public void Load(string raw)
    {
        Show(raw ?? Fallback);
        Source = "";
        Overridden = false;
    }

    private void Show(string raw)
    {
        _loading = true;
        switch (Kind)
        {
            case RowKind.Toggle:
                BoolValue = raw is "1" or "true" or "True";
                break;
            case RowKind.Choice:
                ChoiceValue = Choices.FirstOrDefault(c => string.Equals(c.Value, raw, StringComparison.OrdinalIgnoreCase))
                              ?? (raw == null ? Choices.FirstOrDefault() : new Choice(raw, raw));
                break;
            case RowKind.Slider:
                NumberValue = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : Min;
                break;
            default:
                TextValue = raw ?? "";
                break;
        }
        _loading = false;
    }

    /// <summary>The UI's value as the profile stores it.</summary>
    public JsonNode ToNode()
    {
        string s = Kind switch
        {
            RowKind.Toggle => Env ? (BoolValue ? "1" : "0") : null,
            RowKind.Choice => ChoiceValue?.Value,
            RowKind.Slider => Format == null ? _number.ToString(CultureInfo.InvariantCulture) : _number.ToString("0.####", CultureInfo.InvariantCulture),
            _ => TextValue,
        };
        if (Env)
            return JsonValue.Create(s ?? "");
        return Type switch
        {
            RowType.Bool => JsonValue.Create(BoolValue),
            RowType.Int => long.TryParse(s, out long l) ? JsonValue.Create(l) : JsonValue.Create(s),
            RowType.Number => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? JsonValue.Create(d) : JsonValue.Create(s),
            _ => JsonValue.Create(s ?? ""),
        };
    }

    public JsonNode FallbackNode => Fallback == null ? null : Env ? JsonValue.Create(Fallback) : Type switch
    {
        RowType.Bool => JsonValue.Create(Fallback == "true"),
        RowType.Int => JsonValue.Create(long.Parse(Fallback)),
        RowType.Number => JsonValue.Create(double.Parse(Fallback, CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(Fallback),
    };
}

/// <summary>The PS4/PS5 system languages, in console_language order.</summary>
public static class ConsoleLanguages
{
    public static readonly string[] Names = { "Japanese", "English (US)", "French", "Spanish", "German", "Italian", "Dutch", "Portuguese", "Russian", "Korean",
        "Chinese (Traditional)", "Chinese (Simplified)", "Finnish", "Swedish", "Danish", "Norwegian", "Polish", "Portuguese (Brazil)",
        "English (UK)", "Turkish", "Spanish (Latin America)", "Arabic", "French (Canada)", "Czech", "Hungarian", "Greek", "Romanian", "Thai",
        "Vietnamese", "Indonesian" };

    public static List<Choice> Choices => Names.Select((l, i) => new Choice(l, i.ToString(CultureInfo.InvariantCulture))).ToList();
}

public sealed class SettingSection
{
    public string Title { get; init; }
    public string Glyph { get; init; }
    public string Caption { get; init; }
    public List<SettingRow> Rows { get; init; }
}

/// <summary>One KytyPS5 release in the Play page's Builds list.</summary>
public sealed class KytyBuildRow : Observable
{
    private string _status, _action, _detail;
    private bool _canAct = true, _busy;
    private double _progress;

    public NHL27Kit.Core.KytyRelease Release { get; init; }
    public string Title => Release.Published.ToString("MMM d, yyyy  ·  h:mm tt", CultureInfo.CurrentCulture);
    /// <summary>The installed kyty_emulator.exe, or null.</summary>
    public string Installed { get; set; }

    public string Detail { get => _detail; set => Set(ref _detail, value); }
    /// <summary>"In use", "Installed", "New" or empty.</summary>
    public string Status { get => _status; set { if (Set(ref _status, value)) { Raise(nameof(HasStatus)); Raise(nameof(StatusBrush)); } } }
    public bool HasStatus => !string.IsNullOrEmpty(_status);
    public Brush StatusBrush => (Brush)System.Windows.Application.Current.FindResource(_status switch
    {
        "In use" => "Good",
        "New" => "Accent",
        _ => "TextDim",
    });
    /// <summary>"Download", "Use" or null (the build in use).</summary>
    public string Action { get => _action; set { if (Set(ref _action, value)) Raise(nameof(HasAction)); } }
    public bool HasAction => _action != null;
    public bool CanAct { get => _canAct; set => Set(ref _canAct, value); }
    public bool Busy { get => _busy; set => Set(ref _busy, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
}
