using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;
using Path = System.IO.Path;

namespace NHL27Kit.App;

/// <summary>
/// A top-down rink picture turned into the game's centre logo and centre line (<see cref="CenterIce"/>): the preview, the
/// look sliders, Save PNGs and Write. Write hands the two PNGs to the Teams page, which writes them into the team's
/// textures. The Teams page's Centre ice tab feeds it a picture the user downloaded; the TheFaceoff.net window one it
/// downloaded from the site.
/// </summary>
public partial class CenterIcePanel : UserControl
{
    private TeamSummary _team;
    private bool _hasLogo, _hasLine, _ready;
    private Func<string, string, Task<string>> _write;
    private readonly DispatcherTimer _lookTimer = new() { Interval = TimeSpan.FromMilliseconds(110) };
    private byte[] _src, _logo, _line;
    private int _w, _h, _pick, _look;
    private RinkGeometry _geo;
    private string _title;

    public static readonly string Folder = Path.Combine(KitPaths.Export, "faceoff");

    public CenterIcePanel()
    {
        InitializeComponent();
        _lookTimer.Tick += async (_, _) => { _lookTimer.Stop(); await Convert(); };
        UpdateLookText();
        _ready = true;
    }

    public bool HasResult => _logo != null;

    /// <summary>Keeps the preview to this height where the page scrolls.</summary>
    public double PreviewMaxHeight { get => PreviewBox.MaxHeight; set => PreviewBox.MaxHeight = value; }

    /// <summary>The team this panel writes into: says what will be written, and clears any picture.</summary>
    public void SetTarget(TeamSummary team, bool hasLogo, bool hasLine, int logoNumber, List<TeamSummary> artSharers, List<TeamSummary> lineSharers, Func<string, string, Task<string>> write, string prompt)
    {
        _team = team;
        _hasLogo = hasLogo;
        _hasLine = hasLine;
        _write = write;
        WriteText.Text = $"Write into {team.Abbr}";
        // a line is often shared across teams, so one that other clubs use is only written when asked for
        var otherClubs = lineSharers.Where(t => t.ArtId != team.ArtId).ToList();
        LineCheck.Visibility = hasLine ? Visibility.Visible : Visibility.Collapsed;
        LineCheck.IsChecked = otherClubs.Count == 0;
        TargetInfo.Text = !hasLogo ? $"{team.Abbr} has no centre ice texture, so there's nothing to write." :
                          $"Writes {team.Abbr}'s centre logo (ice_centerlogo_{logoNumber}, 1024²)" +
                          (hasLine ? $" and, when ticked, its centre line (ice_centerline_{team.CenterLine}, 64×4096)" : "; this team has no centre line texture, so only the logo") +
                          ". Each is its own mod entry; the Mods page restores them." +
                          (artSharers.Count > 0 ? $" The logo also shows for {TeamLogos.Names(artSharers)}." : "") +
                          (hasLine && otherClubs.Count > 0 ? $" The line also changes {TeamLogos.Names(otherClubs)}, so it's off unless you tick it." : "");
        Begin(prompt);
        RinkTitle.Text = "No picture yet";
        RinkInfo.Text = "";
        WriteStatus.Text = "";
    }

    /// <summary>Clears the picture for a new one; the number it returns goes with that picture (a newer one wins).</summary>
    public int Begin(string message)
    {
        int pick = ++_pick;
        _src = null;
        _logo = _line = null;
        WriteButton.IsEnabled = SaveButton.IsEnabled = false;
        PreviewImage.Source = null;
        LineImage.Source = null;
        RinkImage.Source = null;
        RinkMarks.Children.Clear();
        RinkInfo.ClearValue(TextBlock.ForegroundProperty);
        Say(message);
        return pick;
    }

    public bool IsCurrent(int pick) => pick == _pick;

    /// <summary>A message where the preview goes.</summary>
    public void Say(string message)
    {
        PreviewEmpty.Visibility = Visibility.Visible;
        PreviewEmptyText.Text = message;
    }

    /// <summary>A picture that can't be used: says why at the top, where it can't be missed.</summary>
    private void Reject(string why)
    {
        RinkInfo.Text = why;
        RinkInfo.Foreground = (Brush)FindResource("Danger");
        Say("Choose another picture");
    }

    /// <summary>The heading above the preview (a null title leaves it as it is).</summary>
    public void ShowName(string title, string info)
    {
        if (title != null) RinkTitle.Text = _title = title;
        RinkInfo.Text = info;
    }

    /// <summary>Reads a rink picture and converts it. A picture that can't be a whole rink is turned away with the reason.</summary>
    public async Task<bool> OpenPicture(int pick, string file, string title, string source)
    {
        ShowName(title, source);
        byte[] src;
        int w, h;
        try { (src, w, h) = await Task.Run(() => ImageLoader.Load(file)); }
        catch (Exception ex)
        {
            if (pick == _pick) Reject($"Can't read {Path.GetFileName(file)}: {ex.Message} Use a JPG or PNG: the Download button on the rink's Google Drive page gives one.");
            return false;
        }
        if (pick != _pick) return false;
        if (w < CenterIce.LeastWidth)
        {
            Reject($"{Path.GetFileName(file)} is only {w}×{h}, too small to make a logo from. Press Download on the rink's Google Drive page for the 5000×2236 picture.");
            return false;
        }
        if (!CenterIce.IsRinkShape(w, h))
        {
            Reject($"{Path.GetFileName(file)} is {w}×{h}, which isn't a whole rink. Use the top-down picture of the whole rink, about 5000×2236.");
            return false;
        }
        var geo = await Task.Run(() => CenterIce.Measure(src, w, h));
        if (pick != _pick) return false;
        _src = src; _w = w; _h = h; _geo = geo;
        RinkInfo.Text = $"{w}×{h} layout, {geo.PixelsPerFoot:0.0} px/ft " +
                        (geo.Measured ? "(measured from the blue lines)" : "(blue lines not found: the site's usual template)") + $"  ·  {source}" +
                        (w < CenterIce.FullWidth * 4 / 5 ? $"\nThis copy is small, so the logo will be soft. The full picture is {CenterIce.FullWidth} wide: press Download on the rink's Google Drive page." : "");
        RinkImage.Source = Small(src, w, h, 560);
        MarkRink();
        await Convert();
        return true;
    }

    /// <summary>A downscaled copy for the whole-rink picture.</summary>
    private static BitmapSource Small(byte[] rgba, int w, int h, int width)
    {
        int sw = width, sh = (int)Math.Round((double)h * width / w);
        var dst = new byte[sw * sh * 4];
        for (int y = 0; y < sh; y++)
        for (int x = 0; x < sw; x++)
            Array.Copy(rgba, ((y * h / sh) * w + x * w / sw) * 4, dst, (y * sw + x) * 4, 4);
        return TextureService.ToBitmap(dst, sw, sh, Channel.Rgb);
    }

    /// <summary>Draws the square the logo is cut from and the line's span on the whole-rink picture.</summary>
    private void MarkRink()
    {
        RinkMarks.Children.Clear();
        if (_src == null) return;
        RinkMarks.UpdateLayout();
        double bw = 280, bh = 126, scale = Math.Min(bw / _w, bh / _h), ox = (bw - _w * scale) / 2, oy = (bh - _h * scale) / 2;
        double span = CenterIce.LogoSpanFeet * _geo.PixelsPerFoot * scale;
        var accent = (Brush)FindResource("Accent");
        var square = new Rectangle { Width = span, Height = span, Stroke = accent, StrokeThickness = 1.5 };
        Canvas.SetLeft(square, ox + _geo.Cx * scale - span / 2);
        Canvas.SetTop(square, oy + _geo.Cy * scale - span / 2);
        RinkMarks.Children.Add(square);
    }

    // ================================================================ converting

    private void OnLook(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        UpdateLookText();
        _lookTimer.Stop();
        _lookTimer.Start();
    }

    private void OnDefaults(object sender, RoutedEventArgs e)
    {
        var d = new CenterIceLook();
        SatSlider.Value = d.Saturation;
        CutSlider.Value = d.CutOut;
        TintSlider.Value = d.IceTint;
    }

    private void UpdateLookText()
    {
        SatText.Text = $"{SatSlider.Value * 100:0}%";
        CutText.Text = $"{CutSlider.Value:0.00}";
        TintText.Text = $"{TintSlider.Value * 100:0}%";
    }

    public void SetLook(double saturation, double cut)
    {
        SatSlider.Value = saturation;
        CutSlider.Value = cut;
    }

    private CenterIceLook Look() => new(SatSlider.Value, CutSlider.Value, TintSlider.Value);

    /// <summary>Converts the layout with the sliders' look and shows it (the centre as the game puts it together, and the line).</summary>
    private async Task Convert()
    {
        if (_src == null) return;
        int look = ++_look;
        var (src, w, h, g, l, dark) = (_src, _w, _h, _geo, Look(), IceToggle.IsChecked == true);
        var r = await Task.Run(() =>
        {
            var logo = CenterIce.Logo(src, w, h, g, l);
            var line = CenterIce.Line(src, w, h, g, l);
            var preview = CenterIce.Preview(logo, line, 768, dark ? 0x101318 : 0xE9EEF2);
            // the line on its side: 85 ft across, its 64 pixels down
            var turned = new byte[CenterIce.LineLength / 4 * CenterIce.LineWidth * 4];
            int tw = CenterIce.LineLength / 4;
            for (int y = 0; y < CenterIce.LineWidth; y++)
            for (int x = 0; x < tw; x++)
                Array.Copy(line, ((x * 4) * CenterIce.LineWidth + y) * 4, turned, (y * tw + x) * 4, 4);
            return (logo, line, Preview: TextureService.ToBitmap(preview, 768, 768, Channel.Rgb), Line: TextureService.ToBitmap(turned, tw, CenterIce.LineWidth, Channel.Rgba));
        });
        if (look != _look || src != _src) return;
        _logo = r.logo;
        _line = r.line;
        PreviewImage.Source = r.Preview;
        LineImage.Source = r.Line;
        PreviewEmpty.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = true;
        WriteButton.IsEnabled = _hasLogo;
    }

    // ================================================================ saving and writing

    private (string Logo, string Line) SavePngs(string folder)
    {
        Directory.CreateDirectory(folder);
        string stem = Path.Combine(folder, $"{_team.Abbr}_" + string.Concat((_title ?? "rink").Split(Path.GetInvalidFileNameChars())).Replace(' ', '_'));
        string logo = stem + "_centerlogo.png", line = stem + "_centerline.png";
        File.WriteAllBytes(logo, ImageFiles.Png(_logo, CenterIce.LogoSize, CenterIce.LogoSize));
        File.WriteAllBytes(line, ImageFiles.Png(_line, CenterIce.LineWidth, CenterIce.LineLength));
        return (logo, line);
    }

    private void OnSavePngs(object sender, RoutedEventArgs e)
    {
        if (_logo == null) return;
        var dlg = new OpenFolderDialog { Title = "Save the centre logo and line PNGs into", InitialDirectory = Folder };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        var (logo, line) = SavePngs(dlg.FolderName);
        WriteStatus.Text = $"Saved {Path.GetFileName(logo)} and {Path.GetFileName(line)}.";
    }

    private async void OnWrite(object sender, RoutedEventArgs e) => await Write();

    /// <summary>Saves the PNGs (export/faceoff) and has the Teams page write them into the team's centre ice.</summary>
    public async Task Write()
    {
        if (_logo == null || !_hasLogo) return;
        var (logo, line) = SavePngs(Folder);
        WriteButton.IsEnabled = false;
        WriteStatus.Text = $"Writing into {_team.Abbr}'s centre ice…";
        try { WriteStatus.Text = await _write(logo, _hasLine && LineCheck.IsChecked == true ? line : null); }
        catch (Exception ex) { WriteStatus.Text = "Can't write: " + ex.Message; }
        finally { WriteButton.IsEnabled = true; }
    }
}
