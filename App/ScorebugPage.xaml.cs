using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>One part of the score clock in the list: a style class.</summary>
public sealed class ScorebugPart : INotifyPropertyChanged
{
    public string Class { get; init; }
    public string Label { get; init; }
    public string Meta { get; init; }
    private int _changes;
    public int Changes { get => _changes; set { _changes = value; On(nameof(HasChanges)); On(nameof(ChangeText)); } }
    public bool HasChanges => Changes > 0;
    public string ChangeText => $"{Changes} edited";
    public event PropertyChangedEventHandler PropertyChanged;
    private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>An editable value of a part (colour, number, text or on/off).</summary>
public sealed class ScorebugField : INotifyPropertyChanged
{
    private readonly Action<ScorebugField> _changed;
    private string _text;

    public ScorebugField(ScorebugValue v, string original, string current, Action<ScorebugField> changed)
    {
        Value = v;
        Original = original;
        _text = current;
        _changed = changed;
        Validate();
    }

    public ScorebugValue Value { get; }
    public string Id => Value.Id;
    public string Original { get; }
    public string Label => ScorebugPage.Friendly(Value.Key);
    public string Group => ScorebugPage.FriendlyGroup(Value.Group);
    public bool IsColor => Value.Kind == ScorebugValueKind.Color;
    public double BoxWidth => IsColor ? 84 : Value.Kind == ScorebugValueKind.Text ? 96 : 64;
    public string Error { get; private set; }
    public bool HasError => Error != null;
    public bool IsChanged => !string.Equals(_text?.Trim(), Original, StringComparison.OrdinalIgnoreCase);

    public Brush Swatch
    {
        get
        {
            try { uint c = Scorebug.ParseColor(_text); return new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)); }
            catch (FormatException) { return Brushes.Transparent; }
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            Validate();
            On(nameof(Text)); On(nameof(Swatch)); On(nameof(IsChanged)); On(nameof(Error)); On(nameof(HasError));
            _changed?.Invoke(this);
        }
    }

    private void Validate()
    {
        Error = null;
        try { Value.Literal(_text ?? ""); }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            Error = Value.Kind switch { ScorebugValueKind.Color => "Use #RRGGBB", ScorebugValueKind.Bool => "true or false", _ => "Not a number" };
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void On([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// The score clock (nhl/ui/lib/external/tin_nhl.swf): its parts' colours, sizes and positions, with a preview. The page
/// reads the score clock from the game every time it opens, on Reload and after a save; Save to game recompiles the
/// changed style classes and writes the SWF into the game through the mod list (one entry, so the Mods page and
/// "Game's own" undo it).
/// </summary>
public partial class ScorebugPage : UserControl
{
    private ModService _mods;
    private TextureService _textures;
    private Scorebug _sb;
    private readonly Dictionary<string, string> _edits = new();
    private Dictionary<string, ImageSource> _tex = new();
    private List<ScorebugPart> _parts = new();
    private bool _busy, _loaded, _sharpBadge;

    /// <summary>
    /// Off: a changed score clock crashed the game when a match loaded (2026-10-27, PLAN §6). The page still previews
    /// edits, and "Game's original" still restores a saved one. Turn back on once the crash is understood.
    /// </summary>
    public const bool SavingAllowed = false;
    public const string SavingOff = "Saving is off: a changed score clock crashes the game when a match loads. Edits are preview only.";

    public event Action<string> StatusChanged;

    public ScorebugPage() => InitializeComponent();

    private static readonly (string Class, string Label, string Meta)[] Known =
    {
        ("ScoreclockTimeSubwdgtStyle", "Clock", "time and period box"),
        ("ScoreclockTeamSubwdgtStyle", "Team rows", "name and score, away and home"),
        ("ScoreclockStatsSubwdgtStyle", "Shots", "SOG column"),
        ("ScoreclockLogoSubwdgtStyle", "Logo box", "left end"),
        ("ScoreclockGoalSubwdgtStyle", "Goal", "goal flash"),
        ("ScoreclockPostGoalMessageSubwdgtStyle", "After a goal", "scorer and assists"),
        ("ScoreclockMessageSubwdgtStyle", "Messages", "power play and others"),
        ("ScoreclockMessageRendererTeamStyle", "Team messages", "under a team row"),
        ("ScoreclockMessageRendererCommonStyle", "Common messages", "under the bug"),
        ("ScoreclockMessageRendererStipulationStyle", "Game rule", "stipulation banner"),
        ("ScoreclockMessageRendererBaseStyle", "Message base", "shared by messages"),
        ("ScoreclockShootoutSubwdgtStyle", "Shootout", "rounds"),
        ("ScoreclockShootoutBoxRendererStyle", "Shootout boxes", "one per round"),
        ("ScoreclockFaceoffSubwdgtStyle", "Faceoff", "win chances"),
        ("ScoreclockFaceoffTeamSubwdgtStyle", "Faceoff team", ""),
        ("ScoreclockFaceoffVsSubwdgtStyle", "Faceoff vs", ""),
        ("ScoreclockWdgtStyle", "Whole bug", "shared settings"),
    };

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _textures = new TextureService(mods.Index);
        if (IsVisible) _ = ReloadAsync();
    }

    /// <summary>Called when the page is shown: always reads the score clock again.</summary>
    public Task Shown() => _mods == null || _busy ? Task.CompletedTask : ReloadAsync();

    private void Status(string text)
    {
        State.Text = text;
        StatusChanged?.Invoke(text);
    }

    // ---------------------------------------------------------------- reading

    private async Task ReloadAsync(string done = null)
    {
        if (_mods == null) return;
        _busy = true;
        PreviewBusy.Visibility = Visibility.Visible;
        Summary.Text = "Reading the score clock from the game…";
        SetButtons();
        try
        {
            var sb = await _mods.LoadScorebugAsync();
            var tex = new Dictionary<string, ImageSource>();
            foreach (var name in new[] { ScorebugPreview.GradientTexture, ScorebugPreview.GlowTexture, ScorebugPreview.HighlightTexture, ScorebugPreview.HiveTexture, Scorebug.RowShading }
                         .Concat(ScorebugPreview.LogoTextures(sb)).Distinct())
            {
                try
                {
                    var t = await _textures.LoadAsync(name);
                    if (t?.Rgba != null) tex[name] = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, Channel.Rgba);
                }
                catch (Exception) { }
            }
            _sb = sb;
            _tex = tex;
            _edits.Clear();
            _sharpBadge = false;
            SharpThumb.Source = SharpFrame(sb, tex);
            BuildParts();
            var legacy = _mods.Find(LegacyEditor.Kind, Scorebug.File);
            bool changed = legacy != null && _mods.State(legacy) == ModState.Applied;
            Summary.Text = $"{sb.Classes.Count} parts, {sb.Values.Count(Editable)} values, read from the game's tin_nhl.swf just now · " +
                           (changed ? "your changes are in the game" : "the game's own look");
            _loaded = true;
            Status(done ?? "Read from the game.");
        }
        catch (Exception ex)
        {
            Summary.Text = "Couldn't read the score clock: " + ex.Message;
            Status(ex.Message);
        }
        finally
        {
            _busy = false;
            PreviewBusy.Visibility = Visibility.Collapsed;
            SetButtons();
            Redraw();
        }
    }

    private static bool Editable(ScorebugValue v) =>
        v.Key is not ("style" or "name" or "onComplete") && (v.Kind != ScorebugValueKind.Expression || v.Evaluate() != null || v.Color != null);

    private void BuildParts()
    {
        string selected = (PartList.SelectedItem as ScorebugPart)?.Class ?? Known[0].Class;
        _parts = Known.Where(k => _sb.Classes.Any(c => c.Short == k.Class))
                      .Select(k => new ScorebugPart { Class = k.Class, Label = k.Label, Meta = k.Meta })
                      .Concat(_sb.Classes.Where(c => Known.All(k => k.Class != c.Short)).Select(c => new ScorebugPart { Class = c.Short, Label = Friendly(c.Short), Meta = "" }))
                      .ToList();
        PartList.ItemsSource = _parts;
        PartList.SelectedItem = _parts.FirstOrDefault(p => p.Class == selected) ?? _parts.FirstOrDefault();
        CountChanges();
    }

    private void OnPartSelected(object sender, SelectionChangedEventArgs e) => ShowFields();

    private void ShowFields()
    {
        if (PartList.SelectedItem is not ScorebugPart part || _sb == null) { Fields.ItemsSource = null; return; }
        var values = _sb.Classes.First(c => c.Short == part.Class).Values.Where(Editable).ToList();
        Fields.ItemsSource = values.Select(v => new ScorebugField(v, Shown(v), _edits.TryGetValue(v.Id, out var e) ? e : Shown(v), OnFieldChanged)).ToList();
        PartTitle.Text = part.Label.ToUpperInvariant();
        FieldCount.Text = $"{values.Count} values";
    }

    /// <summary>How a value is shown for editing: colours as #RRGGBB, expressions worked out.</summary>
    private static string Shown(ScorebugValue v) => v.Kind switch
    {
        ScorebugValueKind.Color => v.Color is { } c ? $"#{c:X6}" : v.Text,
        ScorebugValueKind.Text => v.Display,
        ScorebugValueKind.Expression => (v.Evaluate() ?? 0).ToString(CultureInfo.InvariantCulture),
        _ => v.Text,
    };

    private void OnFieldChanged(ScorebugField f)
    {
        if (f.IsChanged && !f.HasError) _edits[f.Id] = f.Text.Trim();
        else _edits.Remove(f.Id);
        CountChanges();
        SetButtons();
        Redraw();
    }

    private void CountChanges()
    {
        foreach (var p in _parts) p.Changes = _edits.Keys.Count(k => k.StartsWith(p.Class + ".", StringComparison.Ordinal));
        int n = _edits.Count + (_sharpBadge ? 1 : 0);
        Status(n == 0 ? (_loaded ? "No unsaved changes." : "") : $"{(_sharpBadge ? "Sharp badge" + (_edits.Count > 0 ? " and " : "") : "")}{(_edits.Count > 0 ? $"{_edits.Count} value(s) changed" : "")}{(SavingAllowed ? ", not saved yet." : ". Preview only: saving is off, since a changed score clock crashes matches.")}");
    }

    private void SetButtons()
    {
        bool dirty = _edits.Count > 0 || _sharpBadge;
        bool hasSharp = _sb?.HasSharpBadge == true;
        SharpButton.IsEnabled = _sb != null && !_busy && !hasSharp && !_sharpBadge && _sb.AliasTexture(Scorebug.SharpBadgeAlias) != null;
        SharpButton.Content = hasSharp ? "In the game" : _sharpBadge ? "Chosen" : "Use it";
        SaveButton.IsEnabled = SavingAllowed && dirty && !_busy;
        DiscardButton.IsEnabled = dirty && !_busy;
        ReloadButton.IsEnabled = !_busy;
        OriginalButton.IsEnabled = !_busy && _mods != null &&
            _mods.Find(LegacyEditor.Kind, Scorebug.File) != null;
    }

    private void Redraw()
    {
        if (_sb == null) return;
        Preview.Show(_sb, new Dictionary<string, string>(_edits), _tex, _sharpBadge);
        PreviewNote.Text = _edits.Count > 0 || _sharpBadge ? "Preview of unsaved changes · fonts are stand-ins" : "Preview of the game's score clock · fonts are stand-ins";
    }

    // ---------------------------------------------------------------- actions

    private async void OnReload(object sender, RoutedEventArgs e) => await ReloadAsync();

    private void OnDiscard(object sender, RoutedEventArgs e)
    {
        _edits.Clear();
        _sharpBadge = false;
        ShowFields();
        CountChanges();
        SetButtons();
        Redraw();
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveAsync();

    private void OnSharp(object sender, RoutedEventArgs e) => UseSharpBadge();

    /// <summary>Chooses the sharp badge (saved with Save to game).</summary>
    public void UseSharpBadge()
    {
        if (_sb == null || _sb.HasSharpBadge) return;
        _sharpBadge = true;
        CountChanges();
        SetButtons();
        Redraw();
    }

    /// <summary>The sheet's last frame (the sharp badge) for the card's thumbnail.</summary>
    private static ImageSource SharpFrame(Scorebug sb, IReadOnlyDictionary<string, ImageSource> tex)
    {
        if (sb.AliasTexture(Scorebug.SharpBadgeAlias) is not { } name || !tex.TryGetValue(name, out var img) || img is not System.Windows.Media.Imaging.BitmapSource bmp) return null;
        int f = bmp.PixelWidth / 3;
        var crop = new System.Windows.Media.Imaging.CroppedBitmap(bmp, new Int32Rect(2 * f, 2 * f, f, f));
        crop.Freeze();
        return crop;
    }

    public async Task SaveAsync()
    {
        if (_sb == null || _busy) return;
        if (!SavingAllowed) { Status(SavingOff); return; }
        _busy = true;
        SetButtons();
        Status("Recompiling the score clock and writing it into the game…");
        try
        {
            await _mods.SaveScorebugAsync(_sb, new Dictionary<string, string>(_edits), _sharpBadge);
            _busy = false;
            await ReloadAsync("Saved into the game and read back.");
        }
        catch (Exception ex)
        {
            _busy = false;
            SetButtons();
            Status("Not saved: " + ex.Message);
        }
    }

    private async void OnOriginal(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!App.Args.ContainsKey("shot") &&
            MessageBox.Show(Window.GetWindow(this), "Put the game's own score clock back? This restores the score clock entry on the Mods page.",
                "Scorebug", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        _busy = true;
        SetButtons();
        try
        {
            if (_mods.Find(LegacyEditor.Kind, Scorebug.File) is { } entry && _mods.State(entry) is ModState.Applied or ModState.Partial)
                await _mods.RestoreAsync(entry);
            _busy = false;
            await ReloadAsync("The game's own score clock is back.");
        }
        catch (Exception ex)
        {
            _busy = false;
            SetButtons();
            Status("Couldn't restore: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- names

    public static string FriendlyGroup(string group) => group switch
    {
        "const" => "Size",
        "bgProps" => "Background",
        "bgPropsRightSide" => "Background (logo on the right)",
        "bgGradientProps" => "Shading",
        "bgTexture" or "bgTextureProps" => "Hex texture",
        "bgGlow" => "Team glow",
        "bgHighlight" => "Highlight strip",
        "teamAbbrProps" => "Team name",
        "teamScoreProps" => "Score",
        "logoProps" => "Logo",
        "teamLogoProps" => "Team logo",
        "logoMaskProps" => "Logo mask",
        "periodLabelProps" => "Period",
        "dividerProps" => "Divider",
        "statNameProps" => "Stat name",
        "awayStatProps" => "Away stat",
        "homeStatProps" => "Home stat",
        _ => Friendly(group.EndsWith("Props") ? group[..^5] : group),
    };

    public static string Friendly(string key)
    {
        if (key.Length > 0 && key.ToUpperInvariant() == key)
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.ToLowerInvariant().Replace('_', ' '));
        var sb = new System.Text.StringBuilder();
        foreach (char ch in key.Replace("Scoreclock", "").Replace("Subwdgt", "").Replace("Style", ""))
        {
            if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
            sb.Append(sb.Length == 0 ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
        }
        return sb.ToString() switch { "X" => "X", "Y" => "Y", "Lt" => "Corner top left", "Lb" => "Corner bottom left", "Rt" => "Corner top right", "Rb" => "Corner bottom right", var s => s };
    }
}
