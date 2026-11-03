using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>Sample game state for the preview.</summary>
public sealed record ScorebugSample(string Away, uint AwayColor, uint AwayText, string AwayScore, string AwaySog,
                                    string Home, uint HomeColor, uint HomeText, string HomeScore, string HomeSog,
                                    string Minutes, string Seconds, string Period)
{
    public static ScorebugSample Default { get; } = new("NYR", 0x0038A8, 0xFFFFFF, "0", "0", "BOS", 0xFFB81C, 0xFFFFFF, "0", "0", "20", "00", "1ST");
}

/// <summary>
/// Draws the score clock the way ScoreclockWdgt builds it: the parts at their mounts (logo, away and home rows, shots,
/// clock), each from its style values (with any unsaved edits) and the game's own textures, layered like the AS3 code.
/// Every part tiles the hex texture (scoreclock_hive_texture, at its own size) through its background shape, which the
/// code makes the texture's mask, and a mask isn't drawn: so the clock, SOG and team rows show the texture, not their
/// background colour. Only the logo box keeps its colour (its mask is a second rectangle). Team rows then add the glow
/// tinted with the team colour, the highlight strip and their shading image; the clock, SOG and logo boxes add the shared
/// gradient. Fonts are stand-ins (WIDE and WIDE_ALT styles are stretched); it's a guide, not the game's renderer.
/// </summary>
public sealed class ScorebugPreview : FrameworkElement
{
    public const string GradientTexture = "ui/textures/common/bgs/gradient";
    public const string GlowTexture = "ui/textures/common/scoreclock/scoreclock_teamcolour_glow";
    public const string HighlightTexture = "ui/textures/common/scoreclock/scorebug_light_teamcolour";
    public const string LogoTexture = "ui/textures/common/scoreclock/scoreclock_ea_logo";
    public const string HiveTexture = "ui/textures/common/scoreclock/scoreclock_hive_texture";


    private Scorebug _sb;
    private bool _sharpBadge;
    private IReadOnlyDictionary<string, string> _edits = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, ImageSource> _tex = new Dictionary<string, ImageSource>();
    private static readonly Typeface Face = new(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Bold, FontStretches.Condensed);
    private static readonly Typeface Wide = new(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public ScorebugSample Sample { get; set; } = ScorebugSample.Default;
    public double Zoom { get; set; } = 2;

    /// <param name="sharpBadge">Draw the logo box as <see cref="Scorebug.SharpBadgeStyle"/> makes it (an unsaved choice).</param>
    public void Show(Scorebug sb, IReadOnlyDictionary<string, string> edits, IReadOnlyDictionary<string, ImageSource> textures, bool sharpBadge = false)
    {
        _sb = sb;
        _sharpBadge = sharpBadge && !sb.HasSharpBadge;
        _edits = edits;
        _tex = textures;
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ---------------------------------------------------------------- values (edits first)

    private double N(string cls, string group, string key, double fallback)
    {
        var v = _sb?.Find(cls, group, key);
        if (v == null) return fallback;
        if (_edits.TryGetValue(v.Id, out var e) && double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        if (v.Kind == ScorebugValueKind.Bool) return v.Text == "true" ? 1 : 0;
        return v.Evaluate() ?? fallback;
    }

    private double Const(string cls, string key, double fallback) => N(cls, "const", key, fallback);

    private Color C(string cls, string group, string key, uint fallback)
    {
        var v = _sb?.Find(cls, group, key);
        uint c = fallback;
        if (v != null)
        {
            if (_edits.TryGetValue(v.Id, out var e)) c = SafeColor(e, fallback);
            else if (v.Text.StartsWith("this.") && _sb.Find(cls, "const", v.Text[5..]) is { } k)
                c = _edits.TryGetValue(k.Id, out var ke) ? SafeColor(ke, fallback) : k.Color ?? fallback;
            else c = v.Color ?? fallback;
        }
        return Rgb(c);
    }

    private static uint SafeColor(string s, uint fallback) { try { return Scorebug.ParseColor(s); } catch (FormatException) { return fallback; } }
    private static Color Rgb(uint c) => Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);

    private Rect R(string cls, string group, double w, double h) =>
        new(N(cls, group, "x", 0), N(cls, group, "y", 0), N(cls, group, "width", w), N(cls, group, "height", h));

    private Point Mount(string name, double x, double y) => _sb?.Mount(name) is { } m ? new Point(m.X, m.Y) : new Point(x, y);

    // ---------------------------------------------------------------- layout

    private const string Logo = "ScoreclockLogoSubwdgtStyle", Team = "ScoreclockTeamSubwdgtStyle", Stats = "ScoreclockStatsSubwdgtStyle", Time = "ScoreclockTimeSubwdgtStyle";

    private Size BugSize()
    {
        var time = Mount("_timeMount", 323, 0);
        double w = Math.Max(time.X + Const(Time, "WIDGET_WIDTH", 100), Mount("_awayTeamMount", 80, 0).X + Const(Team, "WIDGET_WIDTH", 193));
        double h = Math.Max(Const(Time, "WIDGET_HEIGHT", 80), Mount("_homeTeamMount", 80, 40).Y + Const(Team, "WIDGET_HEIGHT", 40));
        return new Size(w, h);
    }

    protected override Size MeasureOverride(Size available)
    {
        var s = BugSize();
        return new Size(s.Width * Zoom + 2, s.Height * Zoom + 2);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_sb == null) return;
        var size = BugSize();
        double scale = Math.Min(Zoom, Math.Min(ActualWidth / size.Width, ActualHeight / size.Height));
        if (scale <= 0 || double.IsInfinity(scale) || double.IsNaN(scale)) scale = Zoom;
        dc.PushTransform(new TranslateTransform(Math.Max(0, (ActualWidth - size.Width * scale) / 2), Math.Max(0, (ActualHeight - size.Height * scale) / 2)));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var s = Sample;
        DrawTeam(dc, Mount("_awayTeamMount", 80, 0), s.Away, s.AwayColor, s.AwayText, s.AwayScore);
        DrawTeam(dc, Mount("_homeTeamMount", 80, 40), s.Home, s.HomeColor, s.HomeText, s.HomeScore);
        DrawStats(dc, Mount("_statsMount", 273, 0), s);
        DrawTime(dc, Mount("_timeMount", 323, 0), s);
        DrawLogo(dc, Mount("_logoMount", 0, 0));

        dc.Pop();
        dc.Pop();
    }

    private ImageSource Tex(string name) => _tex.TryGetValue(name, out var t) ? t : null;

    /// <summary>An image drawn in one colour (Flash's colour tint): the colour where the image is, through its alpha.</summary>
    private void Tinted(DrawingContext dc, string texture, Rect r, Color color, double alpha)
    {
        if (Tex(texture) is not { } img || r.Width <= 0 || r.Height <= 0 || alpha <= 0) return;
        dc.PushOpacityMask(new ImageBrush(img) { Stretch = Stretch.Fill });
        dc.PushOpacity(alpha);
        dc.DrawRectangle(new SolidColorBrush(color), null, r);
        dc.Pop();
        dc.Pop();
    }

    private void Image(DrawingContext dc, string texture, Rect r, double alpha)
    {
        if (Tex(texture) is not { } img || r.Width <= 0 || r.Height <= 0 || alpha <= 0) return;
        dc.PushOpacity(alpha);
        dc.DrawImage(img, r);
        dc.Pop();
    }

    /// <summary>The hex texture tiled at its own size over the part (the AS3 bitmap fill), at the part's texture alpha.</summary>
    private void Hive(DrawingContext dc, double w, double h, double alpha)
    {
        if (Tex(HiveTexture) is not { } img || alpha <= 0) return;
        var brush = new ImageBrush(img)
        {
            TileMode = TileMode.Tile, Stretch = Stretch.Fill, ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, img.Width, img.Height),
        };
        dc.PushOpacity(alpha);
        dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
        dc.Pop();
    }

    /// <summary>
    /// The background of a part whose background shape is the texture's mask: the hex texture shows, not the colour.
    /// With the texture's alpha at 0 (or no texture), the colour shows instead.
    /// </summary>
    private void MaskedBackground(DrawingContext dc, string cls, string textureGroup, double w, double h, Color color)
    {
        double alpha = N(cls, textureGroup, "alpha", 1);
        if (alpha > 0 && Tex(HiveTexture) != null) Hive(dc, w, h, alpha);
        else dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, w, h));
    }

    private static FormattedText Text(string text, double size, Color color, bool wide) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, wide ? Wide : Face, size, new SolidColorBrush(color), 1.0);

    private static double Width(FormattedText t, double stretch) => t.WidthIncludingTrailingWhitespace * stretch;

    /// <summary>Draws text with its top-left at <paramref name="at"/>, stretched sideways by <paramref name="stretch"/> (the game's wide faces).</summary>
    private static void Draw(DrawingContext dc, FormattedText t, Point at, double stretch)
    {
        dc.PushTransform(new MatrixTransform(stretch, 0, 0, 1, at.X, at.Y));
        dc.DrawText(t, new Point(0, 0));
        dc.Pop();
    }

    /// <summary>Approximate pixel sizes of MasterStyle fonts (FONT_HEADER_5 … FONT_HEADER_9).</summary>
    private double FontSize(string cls, string group, double fallback)
    {
        string style = _sb.Find(cls, group, "style")?.Text ?? "";
        var m = System.Text.RegularExpressions.Regex.Match(style, @"FONT_HEADER_(\d)");
        if (!m.Success) return fallback;
        return int.Parse(m.Groups[1].Value) switch { 5 => 30, 6 => 34, 7 => 21, 8 => 16, 9 => 12, _ => fallback };
    }

    /// <summary>How much wider than the stand-in font a style's face is: WIDE_ALT (the scores) much, WIDE some.</summary>
    private double FaceStretch(string cls, string group)
    {
        string style = _sb.Find(cls, group, "style")?.Text ?? "";
        return style.Contains("WIDE_ALT") ? 1.75 : style.Contains("WIDE") ? 1.35 : 1;
    }

    // ---------------------------------------------------------------- parts

    /// <summary>A text value (edits first), without its quotes.</summary>
    private string Str(string cls, string group, string key)
    {
        var v = _sb?.Find(cls, group, key);
        if (v == null) return null;
        return _edits.TryGetValue(v.Id, out var e) ? e : v.Display;
    }

    private void DrawLogo(DrawingContext dc, Point at)
    {
        double w = Const(Logo, "WIDGET_WIDTH", 80), h = Const(Logo, "WIDGET_HEIGHT", 80);
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        double lt = N(Logo, "bgProps", "lt", 0), lb = N(Logo, "bgProps", "lb", 0);
        var shape = LeftRounded(new Rect(0, 0, w, h), lt, lb);
        dc.PushClip(shape);
        // the logo box keeps its colour: its texture's mask is a second rectangle
        dc.DrawGeometry(new SolidColorBrush(C(Logo, "bgProps", "color", 0x222222)), null, shape);
        Hive(dc, w, h, N(Logo, "bgTexture", "alpha", 1));
        Tinted(dc, GradientTexture, R(Logo, "bgGradientProps", w, h / 2), C(Logo, "bgGradientProps", "color", 0xFFFFFF), N(Logo, "bgGradientProps", "alpha", 1));
        // the logo: whatever image logoProps.path names, clipped to logoClipProps when there is one (the sharp badge)
        Rect logo = R(Logo, "logoProps", 64, 64);
        string path = Str(Logo, "logoProps", "path");
        Rect? clip = _sb.Find(Logo, "logoClipProps", "width") != null ? R(Logo, "logoClipProps", 64, 64) : null;
        if (_sharpBadge)
        {
            logo = new Rect(-118.7, -121.1, 190, 190);
            path = Scorebug.SharpBadgeAlias;
            clip = new Rect(8, 5.6, 63.3, 63.3);
        }
        string texture = _sb.AliasTexture(path) ?? LogoTexture;
        if (clip is { } c) dc.PushClip(new RectangleGeometry(c));
        Image(dc, texture, logo, 1);
        if (clip != null) dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    /// <summary>The texture the logo box draws (its image name looked up), for the page to load.</summary>
    public static IEnumerable<string> LogoTextures(Scorebug sb)
    {
        yield return LogoTexture;
        if (sb.AliasTexture(sb.Find("ScoreclockLogoSubwdgtStyle", "logoProps", "path")?.Display) is { } t) yield return t;
        if (sb.AliasTexture(Scorebug.SharpBadgeAlias) is { } s) yield return s;
    }

    private void DrawTeam(DrawingContext dc, Point at, string abbr, uint color, uint textColor, string score)
    {
        double w = Const(Team, "WIDGET_WIDTH", 193), h = Const(Team, "WIDGET_HEIGHT", 40);
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        MaskedBackground(dc, Team, "bgTexture", w, h, Rgb(color));
        Tinted(dc, GlowTexture, R(Team, "bgGlow", w, h), Rgb(color), N(Team, "bgGlow", "alpha", 1));
        Image(dc, HighlightTexture, R(Team, "bgHighlight", w, 13), N(Team, "bgHighlight", "alpha", 0.8));
        Image(dc, Scorebug.RowShading, R(Team, "bgGradientProps", w, h), 1);

        var tc = Rgb(textColor);
        double aw = FaceStretch(Team, "teamAbbrProps"), sw = FaceStretch(Team, "teamScoreProps");
        Draw(dc, Text(abbr, FontSize(Team, "teamAbbrProps", 21), tc, aw > 1), new Point(N(Team, "teamAbbrProps", "x", 10), N(Team, "teamAbbrProps", "y", 8)), aw);
        var sc = Text(score, FontSize(Team, "teamScoreProps", 30), tc, sw > 1);
        double width = Width(sc, sw), pad = Const(Team, "TEXT_PADDING", 10);
        double sx = score.Length > 1 ? w - width - pad : w - width / 2 - Const(Team, "SINGLE_DIGIT_TEXT_OFFSET", 21) - pad;
        Draw(dc, sc, new Point(sx, N(Team, "teamScoreProps", "y", 2)), sw);
        dc.Pop();
        dc.Pop();
    }

    private void DrawStats(DrawingContext dc, Point at, ScorebugSample s)
    {
        double w = Const(Stats, "WIDGET_WIDTH", 50), h = Const(Stats, "WIDGET_HEIGHT", 80);
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        MaskedBackground(dc, Stats, "bgTextureProps", w, h, C(Stats, "bgProps", "color", 0x222222));
        Tinted(dc, GradientTexture, R(Stats, "bgGradientProps", w, h / 2), C(Stats, "bgGradientProps", "color", 0xFFFFFF), N(Stats, "bgGradientProps", "alpha", 0.1));
        Centered(dc, s.AwaySog, Stats, "awayStatProps", w, 5, 21);
        Centered(dc, "SOG", Stats, "statNameProps", w, 31, 12, Color.FromRgb(0xB8, 0xC0, 0xCC));
        Centered(dc, s.HomeSog, Stats, "homeStatProps", w, 50, 21);
        dc.Pop();
        dc.Pop();
    }

    private void DrawTime(DrawingContext dc, Point at, ScorebugSample s)
    {
        double w = Const(Time, "WIDGET_WIDTH", 100), h = Const(Time, "WIDGET_HEIGHT", 80);
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        MaskedBackground(dc, Time, "bgTextureProps", w, h, C(Time, "bgProps", "color", 0x222222));
        Tinted(dc, GradientTexture, R(Time, "bgGradientProps", w, h / 2), C(Time, "bgGradientProps", "color", 0xFFFFFF), N(Time, "bgGradientProps", "alpha", 0.1));
        dc.DrawRectangle(new SolidColorBrush(C(Time, "dividerProps", "color", 0xDBDBDB)), null, R(Time, "dividerProps", 1, 48));
        var digits = Color.FromRgb(0xE4, 0xE4, 0xE4);
        double fs = FontSize(Time, "timerMinutesLabelProps", 34);
        Draw(dc, Text(s.Minutes, fs, digits, false), new Point(N(Time, "timerMinutesLabelProps", "x", 17), N(Time, "timerMinutesLabelProps", "y", 15)), 1);
        Draw(dc, Text(":", fs, digits, false), new Point(N(Time, "timerColonLabelProps", "x", 46), N(Time, "timerColonLabelProps", "y", 12)), 1);
        Draw(dc, Text(s.Seconds, fs, digits, false), new Point(N(Time, "timerSecondsLabelProps", "x", 53), N(Time, "timerSecondsLabelProps", "y", 15)), 1);
        Centered(dc, s.Period, Time, "periodLabelProps", w, 45, 13);
        dc.Pop();
        dc.Pop();
    }

    private void Centered(DrawingContext dc, string text, string cls, string group, double w, double y, double size, Color? color = null)
    {
        double stretch = FaceStretch(cls, group);
        var t = Text(text, FontSize(cls, group, size), color ?? Colors.White, stretch > 1);
        Draw(dc, t, new Point((w - Width(t, stretch)) / 2, N(cls, group, "y", y)), stretch);
    }

    private static Geometry LeftRounded(Rect r, double lt, double lb)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r.Left + lt, r.Top), true, true);
            c.LineTo(new Point(r.Right, r.Top), true, false);
            c.LineTo(new Point(r.Right, r.Bottom), true, false);
            c.LineTo(new Point(r.Left + lb, r.Bottom), true, false);
            if (lb > 0) c.ArcTo(new Point(r.Left, r.Bottom - lb), new Size(lb, lb), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r.Left, r.Top + lt), true, false);
            if (lt > 0) c.ArcTo(new Point(r.Left + lt, r.Top), new Size(lt, lt), 0, false, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }
}
