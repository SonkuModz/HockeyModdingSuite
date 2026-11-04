using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A section of the NHL 24 score clock's settings.</summary>
public sealed class Scorebug24Group : INotifyPropertyChanged
{
    public string Label { get; init; }
    public string Meta { get; init; }
    public List<Scorebug24Field> Fields { get; init; }
    private int _changes;
    public int Changes { get => _changes; set { _changes = value; On(nameof(HasChanges)); On(nameof(ChangeText)); } }
    public bool HasChanges => Changes > 0;
    public string ChangeText => $"{Changes} edited";
    public event PropertyChangedEventHandler PropertyChanged;
    private void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>One setting: a colour, number, text, or a switch.</summary>
public sealed class Scorebug24Field : INotifyPropertyChanged
{
    private readonly Action<Scorebug24Field> _changed;
    private string _text;

    public Scorebug24Field(Scorebug24Setting s, string current, Action<Scorebug24Field> changed)
    {
        Setting = s;
        Original = current;
        _text = current;
        _changed = changed;
        Validate();
    }

    public Scorebug24Setting Setting { get; }
    public string Key => Setting.Key;
    public string Original { get; private set; }
    public string Label => Setting.Label;
    public string Tip => Setting.Key;
    public string DefaultText => Setting.Default == "" ? "default: none" : $"default: {Setting.Default}";
    public bool IsColor => Setting.Kind == Scorebug24Kind.Colour;
    public bool IsFlag => Setting.Kind == Scorebug24Kind.Flag;
    public bool HasBox => !IsFlag;
    public double BoxWidth => IsColor ? 84 : Setting.Kind == Scorebug24Kind.Text ? (Key == "teamColors" ? 130 : 110) : 64;
    public string Error { get; private set; }
    public bool HasError => Error != null;
    public bool IsChanged => !string.Equals(_text?.Trim(), Original, StringComparison.OrdinalIgnoreCase);

    public bool On
    {
        get => _text is "true" or "1";
        set => Text = value ? "true" : "false";
    }

    public Brush Swatch
    {
        get
        {
            try { uint c = Scorebug24.ParseColour(_text); return new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)); }
            catch (Exception e) when (e is FormatException or OverflowException) { return Brushes.Transparent; }
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
            Raise(nameof(Text)); Raise(nameof(On)); Raise(nameof(Swatch)); Raise(nameof(IsChanged)); Raise(nameof(Error)); Raise(nameof(HasError));
            _changed?.Invoke(this);
        }
    }

    /// <summary>After a save: what's in the game now.</summary>
    public void Saved()
    {
        Original = _text?.Trim();
        Raise(nameof(IsChanged));
    }

    private void Validate()
    {
        Error = null;
        string t = _text?.Trim() ?? "";
        switch (Setting.Kind)
        {
            case Scorebug24Kind.Colour:
                try { Scorebug24.ParseColour(t); } catch (Exception e) when (e is FormatException or OverflowException or ArgumentException) { Error = "Use #RRGGBB"; }
                break;
            case Scorebug24Kind.Number:
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) Error = "Not a number";
                break;
            case Scorebug24Kind.Text:
                if (t.Contains(';') || (Key != "teamColors" && t.Contains('='))) Error = "No ; or =";
                else if (t.Any(c => c > 126)) Error = "Plain letters only";
                break;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// NHL 24's Scorebug page: the settings of a score clock made with NHL24Kit (read from the movie in the game: its
/// settings string and the defaults its code uses), a preview drawn from them, and Save (one mod entry for both movies).
/// </summary>
public partial class Scorebug24Page : UserControl
{
    private ModService _mods;
    private Scorebug24 _sb;
    private List<Scorebug24Group> _groups = new();
    private bool _busy, _loaded;
    private BitmapSource[] _logos;

    // sample teams for the preview: away WPG, home BOS (their logos come from the game)
    private static readonly (string Abbr, uint Pri, uint Sec, string Logo)[] Teams =
    {
        ("WPG", 0x041E42, 0x004C97, "ui/textures/common/teamlogos/large/t1"),
        ("BOS", 0xFFB81C, 0x111111, "ui/textures/common/teamlogos/large/t2"),
    };

    public event Action<string> StatusChanged;

    public Scorebug24Page() => InitializeComponent();

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _sb = null;
        _loaded = false;
        _logos = null;
        if (IsVisible) _ = Shown();
    }

    public async Task Shown()
    {
        if (_mods == null || _loaded || _busy) return;
        await ReloadAsync(null);
    }

    private async Task ReloadAsync(string message)
    {
        _busy = true;
        SetButtons();
        PreviewBusy.Visibility = Visibility.Visible;
        PreviewBusyText.Text = "Reading the score clock from the game…";
        Bug.Children.Clear();
        try
        {
            var index = _mods.Index;
            _sb = await Task.Run(() => Scorebug24.Load(index));
            _logos ??= await Task.Run(() => Teams.Select(t =>
            {
                try
                {
                    var tex = Texture.Load(index, t.Logo);
                    return (BitmapSource)ArenaModel.ToBitmap(tex.Rgba, tex.Width, tex.Height);
                }
                catch (Exception) { return null; }
            }).Select(b => { b?.Freeze(); return b; }).ToArray());
            _loaded = true;
            BuildGroups();
            Summary.Text = $"{Scorebug24.File}  ·  {(_sb.Style != null ? _sb.Style + " score clock made with NHL24Kit" : "made with NHL24Kit")}  ·  {_sb.Settings.Count} settings, read from the game";
            AboutText.Text = "This score clock reads its settings each time a match starts, so a change needs no rebuild of the movie. Saving writes scoreclock.gfx and score_clock.gfx as one entry on the Mods page.";
            PreviewBusy.Visibility = Visibility.Collapsed;
            Draw();
            Status(message ?? "");
        }
        catch (Exception ex)
        {
            _sb = null;
            _groups.Clear();
            GroupList.ItemsSource = null;
            Fields.ItemsSource = null;
            PreviewBusyText.Text = ex.Message;
            Summary.Text = ex is NotSupportedException ? "Nothing to edit in this score clock." : "The score clock couldn't be read.";
            Status("");
        }
        finally
        {
            _busy = false;
            SetButtons();
        }
    }

    private static readonly string[] Order = { "Colours", "Labels", "Show and hide", "Position and size", "Text sizes", "Timing" };

    private void BuildGroups()
    {
        string selected = (GroupList.SelectedItem as Scorebug24Group)?.Label;
        _groups = _sb.Settings.GroupBy(s => s.Group).OrderBy(g => Array.IndexOf(Order, g.Key) is int i && i >= 0 ? i : 99)
            .Select(g => new Scorebug24Group
            {
                Label = g.Key,
                Meta = $"{g.Count()} setting{(g.Count() == 1 ? "" : "s")}",
                Fields = g.Select(s => new Scorebug24Field(s, _sb.Value(s), OnFieldChanged)).ToList(),
            }).ToList();
        GroupList.ItemsSource = _groups;
        GroupList.SelectedItem = _groups.FirstOrDefault(g => g.Label == selected) ?? _groups.FirstOrDefault();
    }

    private void OnGroupSelected(object sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedItem is not Scorebug24Group g) { Fields.ItemsSource = null; return; }
        Fields.ItemsSource = g.Fields;
        GroupTitle.Text = g.Label.ToUpperInvariant();
        FieldCount.Text = g.Meta;
    }

    /// <summary>Shows one section (scripted runs: --sb24group Colours).</summary>
    public void SelectGroup(string label) => GroupList.SelectedItem = _groups.FirstOrDefault(g => g.Label.Equals(label, StringComparison.OrdinalIgnoreCase)) ?? GroupList.SelectedItem;

    /// <summary>Sets values as if typed (scripted runs: --sb24set "cClock=#7A0019;lPowerPlay=PP").</summary>
    public void SetValues(string spec)
    {
        foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && AllFields.FirstOrDefault(f => f.Key == kv[0].Trim()) is { } f) f.Text = kv[1].Trim();
        }
    }

    private IEnumerable<Scorebug24Field> AllFields => _groups.SelectMany(g => g.Fields);

    private void OnFieldChanged(Scorebug24Field f)
    {
        foreach (var g in _groups) g.Changes = g.Fields.Count(x => x.IsChanged);
        SetButtons();
        Draw();
        int n = AllFields.Count(x => x.IsChanged);
        Status(n == 0 ? "No unsaved changes." : $"{n} setting(s) changed, not saved yet.");
    }

    private void SetButtons()
    {
        bool changed = AllFields.Any(f => f.IsChanged), errors = AllFields.Any(f => f.HasError);
        SaveButton.IsEnabled = !_busy && _sb != null && changed && !errors;
        DiscardButton.IsEnabled = !_busy && changed;
        ReloadButton.IsEnabled = !_busy;
        OriginalButton.IsEnabled = !_busy && _mods?.Find(LegacyEditor.Kind, Scorebug24.File) is { } e && _mods.State(e) is ModState.Applied or ModState.Partial;
    }

    private void Status(string text)
    {
        State.Text = text;
        StatusChanged?.Invoke(text);
    }

    private void OnReload(object sender, RoutedEventArgs e) => _ = ReloadAsync("Read again from the game.");

    private void OnDiscard(object sender, RoutedEventArgs e)
    {
        foreach (var f in AllFields.Where(f => f.IsChanged).ToList()) f.Text = f.Original;
        Status("Changes discarded.");
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveAsync();

    public async Task SaveAsync()
    {
        if (_busy || _sb == null) return;
        var values = AllFields.Where(f => f.IsChanged).ToDictionary(f => f.Key, f => f.Text.Trim());
        if (values.Count == 0) return;
        _busy = true;
        SetButtons();
        Status("Saving to the game…");
        try
        {
            await _mods.SaveScorebug24Async(_sb, values);
            foreach (var f in AllFields) f.Saved();
            foreach (var g in _groups) g.Changes = 0;
            Status($"Saved {values.Count} setting(s) to the game. Restore the entry on the Mods page to undo.");
        }
        catch (Exception ex)
        {
            Status("Couldn't save: " + ex.Message);
        }
        finally
        {
            _busy = false;
            SetButtons();
        }
    }

    private async void OnOriginal(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!App.Args.ContainsKey("shot") &&
            MessageBox.Show(Window.GetWindow(this), "Put the score clock back as it was before HMS changed it? This restores the score clock entry on the Mods page.",
                "Scorebug", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        _busy = true;
        SetButtons();
        try
        {
            if (_mods.Find(LegacyEditor.Kind, Scorebug24.File) is { } entry && _mods.State(entry) is ModState.Applied or ModState.Partial)
                await _mods.RestoreAsync(entry);
            _busy = false;
            await ReloadAsync("The score clock is back as it was.");
        }
        catch (Exception ex)
        {
            _busy = false;
            SetButtons();
            Status("Couldn't restore: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- preview

    private string V(string key) => AllFields.FirstOrDefault(f => f.Key == key) is { } f ? f.Text?.Trim() : _sb?.Settings.FirstOrDefault(s => s.Key == key)?.Default;

    private double N(string key, double d) => double.TryParse(V(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : d;

    private uint C(string key, uint d)
    {
        try { return V(key) is { Length: > 0 } s ? Scorebug24.ParseColour(s) : d; }
        catch (Exception e) when (e is FormatException or OverflowException) { return d; }
    }

    private bool F(string key, bool d) => V(key) is { Length: > 0 } s ? s is "true" or "1" : d;

    private static Color Rgb(uint c) => Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);

    private static double Lum(uint c) => (0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF)) / 255;

    private static uint Mix(uint a, uint b, double t)
    {
        uint Ch(int s) => (uint)((a >> s & 0xFF) * (1 - t) + (b >> s & 0xFF) * t);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    /// <summary>
    /// Draws the bug as the movie's code does (league box, clock box, two team rows with the gradient, the line between
    /// them), plus the power play bar and the shots box under and beside it, at the settings' sizes and colours.
    /// </summary>
    private void Draw()
    {
        Bug.Children.Clear();
        if (_sb == null) return;
        double rowH = N("rowH", 50), leagueW = N("leagueW", 76), clockW = N("clockW", 100), teamW = N("teamW", 232), shotsW = N("shotsW", 78);
        double barH = N("barH", 40), logoSize = N("logoSize", 96), scoreCx = teamW - N("scoreInset", 36), scale = Math.Clamp(N("scale", 1), 0.3, 3);
        double h = rowH * 2, teamX = leagueW + clockW, teamEnd = teamX + teamW;
        double gradA = Math.Clamp(N("gradA", 70), 0, 255), gradB = Math.Clamp(N("gradB", 175), gradA, 255);
        uint cText = C("cText", 0xFFFFFF), cClockText = C("cClockText", 0xEDEDED);
        bool darken = F("darkenLight", true);
        uint Row(uint c) => !darken ? c : Lum(c) > 0.62 ? Mix(c, 0, Math.Min(0.55, (Lum(c) - 0.62) * 1.6 + 0.25)) : c;
        var overrides = ParseTeamColours(V("teamColors"));
        (uint Pri, uint Sec) TeamCol(int i) => overrides.TryGetValue(Teams[i].Abbr, out var o) ? o : (Teams[i].Pri, Teams[i].Sec);

        var root = new Canvas { Width = teamEnd + shotsW + 8, Height = h + barH + 8 };
        void Rect(double x, double y, double w, double hh, Brush b) { var r = new Rectangle { Width = Math.Max(0, w), Height = Math.Max(0, hh), Fill = b }; Canvas.SetLeft(r, x); Canvas.SetTop(r, y); root.Children.Add(r); }
        TextBlock Text(string s, double size, uint c, bool heavy = false) => new()
        {
            Text = s, FontSize = Math.Max(1, size), Foreground = new SolidColorBrush(Rgb(c)), FontFamily = new FontFamily("Bahnschrift"),
            FontWeight = heavy ? FontWeights.Black : FontWeights.Bold,
        };
        void Place(TextBlock t, double cx, double cy, bool centreX = true)
        {
            t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(t, centreX ? cx - t.DesiredSize.Width / 2 : cx);
            Canvas.SetTop(t, cy - t.DesiredSize.Height / 2);
            root.Children.Add(t);
        }
        Brush B(uint c) => new SolidColorBrush(Rgb(c));

        // shots box (beside the rows, shown for a moment in the game)
        for (int i = 0; i < 2; i++)
        {
            Rect(teamEnd, i * rowH, shotsW, rowH, B(Row(TeamCol(i).Pri)));
            Place(Text(i == 0 ? "12" : "9", N("sShots", 32), cText), teamEnd + shotsW / 2, i * rowH + rowH / 2);
        }
        Rect(teamEnd, 9, 2, rowH - 18, new SolidColorBrush(Color.FromArgb(230, Rgb(cText).R, Rgb(cText).G, Rgb(cText).B)));
        Rect(teamEnd, rowH + 9, 2, rowH - 18, new SolidColorBrush(Color.FromArgb(230, Rgb(cText).R, Rgb(cText).G, Rgb(cText).B)));
        var shotsLabel = Text("SHOTS", N("sShotsLabel", 14), cText);
        Place(shotsLabel, teamEnd + shotsW / 2, h + 10);

        // power play bar (slides out under the bug)
        bool ppTeam = F("ppTeamColour", true);
        uint barCol = ppTeam ? Row(TeamCol(1).Pri) : C("cBar", 0x111111);
        Rect(0, h, teamEnd, barH, B(barCol));
        if (F("ppLogo", true) && _logos?[1] is { } ppLogo)
        {
            double s = N("ppLogoSize", 72);
            var img = new Image { Source = ppLogo, Width = s, Height = s, Opacity = Math.Clamp(N("ppLogoAlpha", 0.28), 0, 1), Clip = null };
            var holder = new Canvas { Width = teamEnd, Height = barH, ClipToBounds = true };
            Canvas.SetLeft(img, teamEnd / 2 - s / 2);
            Canvas.SetTop(img, barH / 2 - s * 0.6);
            holder.Children.Add(img);
            Canvas.SetTop(holder, h);
            root.Children.Add(holder);
        }
        Place(Text($"{V("lPowerPlay") ?? "POWERPLAY"} 1:33", N("sBar", 27), cText), teamEnd / 2, h + barH / 2);

        // league box
        Rect(0, 0, leagueW, h, B(C("cLeague", 0x131313)));
        if (F("showShield", true)) Place(Text("NHL", leagueW * 0.3, cText), leagueW / 2, h * 0.32);
        if (F("showEspn", true)) Place(Text(_sb.Style == "ESPN+" ? "ESPN+" : "TV", leagueW * 0.2, cText), leagueW / 2, h * 0.78);

        // clock box
        Rect(leagueW, 0, clockW, h, B(C("cClock", 0x3A3A3A)));
        Rect(leagueW + 11, rowH - 1, clockW - 22, 2, B(C("cDivider", 0x8E8E8E)));
        var period = new TextBlock { FontFamily = new FontFamily("Bahnschrift"), FontWeight = FontWeights.Bold, Foreground = B(cClockText) };
        period.Inlines.Add(new System.Windows.Documents.Run("1") { FontSize = Math.Max(1, N("sPeriod", 29)) });
        period.Inlines.Add(new System.Windows.Documents.Run("ST") { FontSize = Math.Max(1, N("sPeriodSuffix", 21)) });
        Place(period, leagueW + clockW / 2, rowH / 2);
        Place(Text("20:00", N("sClock", 32), cClockText), leagueW + clockW / 2, rowH + rowH / 2);

        // team rows
        bool gradient = F("rowGradient", true), highlight = F("ppHighlightName", true);
        for (int i = 0; i < 2; i++)
        {
            var (pri, sec) = TeamCol(i);
            pri = Row(pri);
            sec = Row(sec);
            if (Math.Abs(Lum(sec) - Lum(pri)) < 0.03 && sec == pri) sec = Mix(pri, 0, 0.35);
            Brush bg = gradient
                ? new LinearGradientBrush(new GradientStopCollection
                {
                    new(Rgb(sec), 0), new(Rgb(sec), gradA / 255), new(Rgb(pri), gradB / 255), new(Rgb(pri), 1),
                }, 0)
                : B(pri);
            var row = new Canvas { Width = teamW, Height = rowH, ClipToBounds = true, Background = bg };
            if (_logos?[i] is { } logo)
            {
                var img = new Image { Source = logo, Width = logoSize, Height = logoSize };
                Canvas.SetLeft(img, -2);
                Canvas.SetTop(img, Math.Round(rowH / 2 - logoSize * 0.45));
                row.Children.Add(img);
            }
            Canvas.SetLeft(row, teamX);
            Canvas.SetTop(row, i * rowH);
            root.Children.Add(row);
            // the home team has the power play in the preview
            Place(Text(Teams[i].Abbr, N("sAbbr", 33), highlight && i == 1 ? C("cHighlight", 0xF5CF2D) : cText), teamX + teamW * 0.585, i * rowH + rowH / 2);
            Place(Text(i == 0 ? "2" : "3", N("sScore", 50), cText, heavy: true), teamX + scoreCx, i * rowH + rowH / 2);
        }
        if (F("showRowLine", true)) Rect(teamX, rowH - 1, teamW, 2, B(C("cRowLine", 0xDCDCDC)));

        root.LayoutTransform = new ScaleTransform(scale, scale);
        Bug.Width = root.Width * scale;
        Bug.Height = root.Height * scale;
        Bug.Children.Add(root);

        // where it sits on screen (the bug pins itself to the top left)
        Canvas.SetLeft(ScreenBug, N("x", 56));
        Canvas.SetTop(ScreenBug, N("y", 40));
        ScreenBug.Width = teamEnd * scale;
        ScreenBug.Height = h * scale;
        PreviewNote.Text = AllFields.Any(f => f.IsChanged) ? "Preview of unsaved changes · fonts are stand-ins" : "Preview from the game's settings · fonts are stand-ins · sample teams WPG at BOS";
    }

    private static Dictionary<string, (uint, uint)> ParseTeamColours(string s)
    {
        var map = new Dictionary<string, (uint, uint)>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (s ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(':');
            if (f.Length < 2 || !uint.TryParse(f[1].Trim(), NumberStyles.HexNumber, null, out uint pri)) continue;
            uint sec = f.Length >= 3 && uint.TryParse(f[2].Trim(), NumberStyles.HexNumber, null, out uint x) ? x : pri;
            map[f[0].Trim()] = (pri, sec);
        }
        return map;
    }
}
