using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using NHL27Kit.Core;
using Path = System.IO.Path;

namespace NHL27Kit.App;

/// <summary>What the photo converter read: the cloth colours, the lines, and the crest cut out (RGBA).</summary>
public sealed record PhotoJersey(int Body, int? Collar, int? Yoke, List<StudioLine> Lines, byte[] Crest, int CrestWidth, int CrestHeight);

/// <summary>
/// Jersey from a photo: draw a line up the body (hem → armpit) and one along a sleeve (cuff → top of the shoulder), box
/// the crest, click the collar and the shoulders. The line's colours are split into bands (<see cref="JerseyPhoto"/>) and
/// measured against the model (the same two lengths, in cm), so the stripes land where the photo has them.
/// </summary>
public partial class PhotoJerseyWindow : Window
{
    private readonly double _bodyCm, _sleeveCm;
    private byte[] _rgba;
    private int _w, _h;
    private string _file;
    private string _tool = "body";
    private Point _start;
    private Shape _drawing;
    private readonly Dictionary<string, Shape> _marks = new();

    private List<PhotoRun> _body, _sleeve;
    private string _bodyFrom, _sleeveFrom, _crestFrom;
    private int? _collar, _yoke;
    // the crest's photo and box, to cut it again when Round changes
    private (byte[] Rgba, int W, int H, Int32Rect Box)? _crestSource;
    private byte[] _crest;
    private int _crestW, _crestH;

    public PhotoJersey Result { get; private set; }

    /// <param name="bodyCm">The model's hem to armpit height.</param>
    /// <param name="sleeveCm">The model's shoulder to cuff length.</param>
    public PhotoJerseyWindow(double bodyCm, double sleeveCm)
    {
        InitializeComponent();
        _bodyCm = bodyCm;
        _sleeveCm = sleeveCm;
        Hint();
        Summarize();
    }

    // ================================================================ photo

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "A photo of the jersey", Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tif;*.tiff;*.dds|All files|*.*" };
        if (dlg.ShowDialog(this) == true) Open(dlg.FileName);
    }

    public void Open(string file)
    {
        try
        {
            var (rgba, w, h) = ImageLoader.Load(file);
            _rgba = rgba; _w = w; _h = h; _file = file;
            var bgra = (byte[])rgba.Clone();
            for (int i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
            bmp.Freeze();
            Photo.Source = bmp;
            Photo.Width = Marks.Width = w;
            Photo.Height = Marks.Height = h;
            Marks.Children.Clear();
            _marks.Clear();
            PhotoEmpty.Visibility = Visibility.Collapsed;
            PhotoName.Text = Path.GetFileName(file) + $"  ·  {w} × {h}";
        }
        catch (Exception ex)
        {
            Status.Text = $"Can't open {Path.GetFileName(file)}: {ex.Message}";
        }
    }

    // ================================================================ marking

    private void OnTool(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Uid: { Length: > 0 } t }) _tool = t;
        if (IsLoaded) Hint();
    }

    private void Hint()
    {
        ToolHint.Text = _tool switch
        {
            "body" => "Drag from the bottom of the hem straight up to the height of the armpit, beside the crest (not through it). The hem to armpit is " + Cm(_bodyCm) + " on the game's model.",
            "sleeve" => "Drag along a sleeve from the cuff's edge up to the top of the shoulder. The shoulder to cuff is " + Cm(_sleeveCm) + " on the game's model.",
            "crest" => "Drag a box round the crest. The cloth round it is cut away (or switch Round on for a round crest).",
            "collar" => "Click the collar: the neck band and the V under it get its colour.",
            _ => "Click the shoulders (the yoke) if they're a colour of their own.",
        };
    }

    private static string Cm(double cm) => cm.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " cm";

    private double Stroke => Math.Max(_w, _h) / 300.0;

    private void OnMarkDown(object sender, MouseButtonEventArgs e)
    {
        if (_rgba == null) return;
        _start = e.GetPosition(Marks);
        if (_tool is "collar" or "yoke") return;
        Remove(_tool);
        _drawing = _tool == "crest"
            ? (RoundCrest.IsChecked == true ? new Ellipse() : new Rectangle())
            : new Line { X1 = _start.X, Y1 = _start.Y, X2 = _start.X, Y2 = _start.Y };
        _drawing.Stroke = MarkBrush(_tool);
        _drawing.StrokeThickness = Stroke;
        if (_drawing is not Line) { Canvas.SetLeft(_drawing, _start.X); Canvas.SetTop(_drawing, _start.Y); }
        Marks.Children.Add(_drawing);
        Marks.CaptureMouse();
    }

    private void OnMarkMove(object sender, MouseEventArgs e)
    {
        if (_drawing == null) return;
        var p = e.GetPosition(Marks);
        if (_drawing is Line l) { l.X2 = p.X; l.Y2 = p.Y; return; }
        Canvas.SetLeft(_drawing, Math.Min(p.X, _start.X));
        Canvas.SetTop(_drawing, Math.Min(p.Y, _start.Y));
        _drawing.Width = Math.Abs(p.X - _start.X);
        _drawing.Height = Math.Abs(p.Y - _start.Y);
    }

    private void OnMarkUp(object sender, MouseButtonEventArgs e)
    {
        if (_rgba == null) return;
        var p = e.GetPosition(Marks);
        Marks.ReleaseMouseCapture();
        if (_drawing != null) Marks.Children.Remove(_drawing);
        _drawing = null;
        Mark(_tool, _tool is "collar" or "yoke" ? new[] { p.X, p.Y } : new[] { _start.X, _start.Y, p.X, p.Y });
    }

    /// <summary>
    /// Marks the shown photo: "body" / "sleeve" a line (x0, y0, x1, y1: hem or cuff first), "crest" a box (two corners),
    /// "collar" / "yoke" a point. Also how scripted runs mark it.
    /// </summary>
    public void Mark(string tool, double[] v)
    {
        if (_rgba == null) return;
        string from = Path.GetFileName(_file);
        Remove(tool);
        Shape shape;
        switch (tool)
        {
            case "body":
            case "sleeve":
                var runs = JerseyPhoto.Runs(_rgba, _w, _h, v[0], v[1], v[2], v[3], tool == "body" ? _bodyCm : _sleeveCm);
                if (runs.Count == 0) { Status.Text = "Drag a longer line."; return; }
                if (tool == "body") { _body = runs; _bodyFrom = from; } else { _sleeve = runs; _sleeveFrom = from; }
                shape = new Line { X1 = v[0], Y1 = v[1], X2 = v[2], Y2 = v[3] };
                break;
            case "crest":
                int x = (int)Math.Min(v[0], v[2]), y = (int)Math.Min(v[1], v[3]);
                int bw = (int)Math.Abs(v[2] - v[0]), bh = (int)Math.Abs(v[3] - v[1]);
                if (bw < 8 || bh < 8) { Status.Text = "Drag a box round the whole crest."; return; }
                _crestSource = (_rgba, _w, _h, new Int32Rect(x, y, bw, bh));
                _crestFrom = from;
                CutCrest();
                shape = RoundCrest.IsChecked == true ? new Ellipse { Width = bw, Height = bh } : new Rectangle { Width = bw, Height = bh };
                Canvas.SetLeft(shape, x);
                Canvas.SetTop(shape, y);
                break;
            default:
                int c = JerseyPhoto.Sample(_rgba, _w, _h, v[0], v[1], Math.Max(2, (int)(Math.Max(_w, _h) / 250)));
                if (tool == "collar") _collar = c; else _yoke = c;
                double r = Stroke * 4;
                shape = new Ellipse { Width = r * 2, Height = r * 2 };
                Canvas.SetLeft(shape, v[0] - r);
                Canvas.SetTop(shape, v[1] - r);
                break;
        }
        shape.Stroke = MarkBrush(tool);
        shape.StrokeThickness = Stroke;
        _marks[tool] = shape;
        Marks.Children.Add(shape);
        Summarize();
    }

    private void Remove(string tool)
    {
        if (_marks.Remove(tool, out var old)) Marks.Children.Remove(old);
    }

    private Brush MarkBrush(string tool) => (Brush)FindResource(tool switch { "body" => "Accent", "sleeve" => "Violet", "crest" => "Patch", _ => "Good" });

    private void OnRound(object sender, RoutedEventArgs e)
    {
        if (_crestSource is not { } s) return;
        CutCrest();
        if (_marks.ContainsKey("crest") && _file != null)
            Mark("crest", new double[] { s.Box.X, s.Box.Y, s.Box.X + s.Box.Width, s.Box.Y + s.Box.Height });
        else Summarize();
    }

    private void CutCrest()
    {
        if (_crestSource is not { } s) return;
        _crest = JerseyPhoto.Crest(s.Rgba, s.W, s.H, s.Box.X, s.Box.Y, s.Box.Width, s.Box.Height, RoundCrest.IsChecked == true);
        _crestW = s.Box.Width;
        _crestH = s.Box.Height;
    }

    // ================================================================ what's been read

    /// <summary>Puts what's been read together (one colour for the same colour everywhere) and lists it.</summary>
    private void Summarize()
    {
        Results.Children.Clear();
        Result = null;
        var picks = new List<int>();
        if (_collar is int cc) picks.Add(cc);
        if (_yoke is int yc) picks.Add(yc);
        var sets = JerseyPhoto.Unify(new[] { _body ?? new List<PhotoRun>(), _sleeve ?? new List<PhotoRun>() }, picks);
        var body = sets[0];
        var sleeve = sets[1];
        int? collar = _collar != null ? picks[0] : null, yoke = _yoke != null ? picks[^1] : null;

        if (body.Count == 0 && sleeve.Count == 0)
        {
            Results.Children.Add(Note("Nothing yet: mark the body stripes first (the body's colour comes from them)."));
        }
        int baseColor = JerseyPhoto.Dominant(body.Count > 0 ? body : sleeve);
        var lines = new List<StudioLine>();
        if (body.Count + sleeve.Count > 0)
        {
            Results.Children.Add(Swatch(baseColor, "Body", _bodyFrom ?? _sleeveFrom));
            if (body.Count > 0)
            {
                var bl = JerseyPhoto.Lines(body, baseColor, LinePlace.Body);
                lines.AddRange(bl);
                Results.Children.Add(Note($"Body: {bl.Count} line(s), from {_bodyFrom}"));
                foreach (var l in bl) Results.Children.Add(LineItem(l));
            }
            if (sleeve.Count > 0)
            {
                int sleeveBase = JerseyPhoto.Dominant(sleeve);
                var sl = new List<StudioLine>();
                // sleeves of their own colour: one line the whole length under the stripes
                if (sleeveBase != baseColor) sl.Add(new StudioLine(LinePlace.Sleeves, 0, Math.Ceiling(_sleeveCm + 2), sleeveBase));
                sl.AddRange(JerseyPhoto.Lines(sleeve, sleeveBase, LinePlace.Sleeves));
                lines.AddRange(sl);
                Results.Children.Add(Note($"Sleeves: {sl.Count} line(s), from {_sleeveFrom}"));
                foreach (var l in sl) Results.Children.Add(LineItem(l));
            }
        }
        if (collar is int c1) Results.Children.Add(Swatch(c1, "Collar (neck band and V)", null));
        if (yoke is int c2) Results.Children.Add(Swatch(c2, "Shoulders", null));
        if (_crest != null)
        {
            Results.Children.Add(Note($"Crest from {_crestFrom}, painted into the color map"));
            var bgra = (byte[])_crest.Clone();
            for (int i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
            CrestImage.Source = BitmapSource.Create(_crestW, _crestH, 96, 96, PixelFormats.Bgra32, null, bgra, _crestW * 4);
            CrestFrame.Visibility = Visibility.Visible;
        }
        else CrestFrame.Visibility = Visibility.Collapsed;

        bool ready = body.Count + sleeve.Count > 0 || _crest != null;
        ApplyButton.IsEnabled = ready;
        if (ready) Result = new PhotoJersey(baseColor, collar, yoke, lines, _crest, _crestW, _crestH);
        Status.Text = ready ? "Every line and colour can be changed in the studio afterwards." : "";
    }

    private TextBlock Note(string text) => new() { Text = text, Style = (Style)FindResource("Caption"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 2) };

    private FrameworkElement Swatch(int c, string label, string from)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        row.Children.Add(Chip(c, 18));
        row.Children.Add(new TextBlock { Text = $"{label}  ·  {GearZone.Hex(c)}" + (from != null ? $"  ·  {from}" : ""), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        return row;
    }

    private FrameworkElement LineItem(StudioLine l)
    {
        var row = new DockPanel { Margin = new Thickness(10, 2, 0, 2) };
        row.Children.Add(Chip(l.Color, 14));
        if (l.Edge is int edge) { var e = Chip(edge, 14); e.Margin = new Thickness(3, 0, 0, 0); row.Children.Add(e); }
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string text = string.Format(ci, "{0:0.0} cm up, {1:0.0} wide", l.From, l.Width) + (l.Edge != null ? string.Format(ci, ", {0:0.0} edges", l.EdgeWidth) : "");
        row.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("Caption"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        return row;
    }

    private Border Chip(int c, double size) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(3),
        Background = new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)),
        BorderBrush = (Brush)FindResource("LineStrong"), BorderThickness = new Thickness(1),
    };

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (Result == null) return;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
