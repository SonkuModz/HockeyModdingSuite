using System.Collections.ObjectModel;
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

public sealed record LeagueChoice(string Key, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A rink in the picker, with its centre-ice picture once it's loaded.</summary>
public sealed class FaceoffRow : Observable
{
    private BitmapSource _thumb;
    public FaceoffRink Rink { get; init; }
    public string Name => Rink.Name;
    public BitmapSource Thumb { get => _thumb; set => Set(ref _thumb, value); }
}

/// <summary>
/// Centre ice from TheFaceoff.net: browse a league's rinks (or paste a rink's link), download its top-down layout,
/// and preview it converted into the game's centre logo and centre line (<see cref="CenterIce"/>), with saturation and
/// ice cut-out sliders. Write hands the two PNGs to the Teams page, which writes them into the shown team's textures.
/// </summary>
public partial class FaceoffWindow : Window
{
    private readonly TeamSummary _team;
    private readonly bool _hasLine;
    private readonly Func<string, string, Task<string>> _write;
    private readonly Dictionary<string, FaceoffLeague> _leagues = new();
    private readonly ObservableCollection<FaceoffRow> _rows = new();
    private readonly DispatcherTimer _lookTimer = new() { Interval = TimeSpan.FromMilliseconds(110) };
    private FaceoffLeague _league;
    private byte[] _src;
    private int _w, _h, _pick, _look;
    private RinkGeometry _geo;
    private string _title;
    private byte[] _logo, _line;
    private bool _loading, _ready;

    public static readonly string Folder = Path.Combine(KitPaths.Export, "faceoff");

    public FaceoffWindow(TeamSummary team, bool hasLine, int logoNumber, List<TeamSummary> artSharers, List<TeamSummary> lineSharers, Func<string, string, Task<string>> write)
    {
        InitializeComponent();
        _team = team;
        _hasLine = hasLine;
        _write = write;
        Heading.Text = $"Centre ice for {team.Name}, from TheFaceoff.net";
        WriteText.Text = $"Write into {team.Abbr}";
        // a line is often shared across teams, so one that other clubs use is only written when asked for
        var otherClubs = lineSharers.Where(t => t.ArtId != team.ArtId).ToList();
        LineCheck.Visibility = hasLine ? Visibility.Visible : Visibility.Collapsed;
        LineCheck.IsChecked = otherClubs.Count == 0;
        TargetInfo.Text = $"Writes {team.Abbr}'s centre logo (ice_centerlogo_{logoNumber}, 1024²)" +
                          (hasLine ? $" and, when ticked, its centre line (ice_centerline_{team.CenterLine}, 64×4096)" : "; this team has no centre line texture, so only the logo") +
                          ". Each is its own mod entry; the Mods page restores them." +
                          (artSharers.Count > 0 ? $" The logo also shows for {TeamLogos.Names(artSharers)}." : "") +
                          (hasLine && otherClubs.Count > 0 ? $" The line also changes {TeamLogos.Names(otherClubs)}, so it's off unless you tick it." : "");
        _lookTimer.Tick += async (_, _) => { _lookTimer.Stop(); await Convert(); };
        RinkList.ItemsSource = _rows;
        UpdateLookText();
        _ready = true;
        var leagues = FaceoffNet.Leagues.Select(l => new LeagueChoice(l.Key, l.Label)).ToList();
        LeaguePicker.ItemsSource = leagues;
        LeaguePicker.SelectedIndex = Math.Max(0, leagues.FindIndex(l => l.Key == LeagueKey(team.League)));
    }

    /// <summary>The site's league for the game's league name (the NHL when nothing fits).</summary>
    private static string LeagueKey(string league)
    {
        string l = (league ?? "").ToLowerInvariant();
        return FaceoffNet.Leagues.FirstOrDefault(x => l.Contains(x.Key)).Key ?? "nhl";
    }

    // ================================================================ the list

    private async void OnLeague(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || LeaguePicker.SelectedItem == null) return;
        string key = ((LeagueChoice)LeaguePicker.SelectedItem).Key;
        if (!_leagues.TryGetValue(key, out var league))
            _leagues[key] = league = new FaceoffLeague { Key = key, Label = FaceoffNet.Leagues.First(l => l.Key == key).Label };
        _league = league;
        Fill();
        if (league.Pages == 0) await LoadPages(3);
    }

    private async void OnMore(object sender, RoutedEventArgs e) => await LoadPages(5);

    /// <summary>Reads more pages of the league's list (8 rinks each), showing them as they come.</summary>
    public async Task LoadPages(int pages)
    {
        if (_loading || _league == null) return;
        var league = _league;
        _loading = true;
        MoreButton.IsEnabled = false;
        try
        {
            for (int i = 0; i < pages && !league.Done; i++)
            {
                ListStatus.Text = $"Reading {league.Label} rinks, page {league.Pages + 1}…";
                var added = await FaceoffNet.MoreAsync(league);
                if (league != _league) return;
                Fill();
            }
        }
        catch (Exception ex)
        {
            ListStatus.Text = "Can't read TheFaceoff.net: " + ex.Message;
            return;
        }
        finally
        {
            _loading = false;
            MoreButton.IsEnabled = !league.Done;
        }
        ListStatus.Text = $"{league.Rinks.Count} {league.Label} rinks, newest first" + (league.Done ? " (all of them)" : "");
    }

    private void OnSearch(object sender, TextChangedEventArgs e) => Fill();

    /// <summary>Shows the league's loaded rinks that match the filter, loading pictures for new rows.</summary>
    private void Fill()
    {
        if (_league == null) return;
        var terms = Search.Text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var want = _league.Rinks.Where(r => terms.All(t => r.Name.ToLowerInvariant().Contains(t))).ToList();
        var keep = _rows.Where(r => want.Contains(r.Rink)).ToDictionary(r => r.Rink);
        _rows.Clear();
        foreach (var rink in want)
        {
            var row = keep.GetValueOrDefault(rink) ?? new FaceoffRow { Rink = rink };
            _rows.Add(row);
            if (row.Thumb == null) _ = LoadThumb(row);
        }
    }

    private static readonly Dictionary<string, BitmapSource> Thumbs = new();

    private async Task LoadThumb(FaceoffRow row)
    {
        if (Thumbs.TryGetValue(row.Rink.ImageId, out var known)) { row.Thumb = known; return; }
        try
        {
            var bytes = await FaceoffNet.GetBytesAsync(row.Rink.Thumb(96));
            var bmp = await Task.Run(() =>
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.StreamSource = new MemoryStream(bytes);
                b.DecodePixelWidth = 96;
                b.EndInit();
                b.Freeze();
                return (BitmapSource)b;
            });
            Thumbs[row.Rink.ImageId] = bmp;
            row.Thumb = bmp;
        }
        catch { /* no picture */ }
    }

    // ================================================================ a rink

    private async void OnRinkPicked(object sender, SelectionChangedEventArgs e)
    {
        if (RinkList.SelectedItem is FaceoffRow row) await OpenRink(row.Rink.Url);
    }

    private async void OnOpenLink(object sender, RoutedEventArgs e)
    {
        string url = LinkBox.Text.Trim();
        if (!url.StartsWith("http")) url = "https://" + url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.EndsWith("thefaceoff.net", StringComparison.OrdinalIgnoreCase))
        {
            RinkInfo.Text = "That isn't a thefaceoff.net link.";
            return;
        }
        RinkList.SelectedItem = null;
        await OpenRink(uri.ToString());
    }

    /// <summary>Reads a rink's page, downloads its top-down layout (kept in export/faceoff/downloads) and converts it.</summary>
    public async Task OpenRink(string url)
    {
        int pick = ++_pick;
        _src = null;
        _logo = _line = null;
        WriteButton.IsEnabled = SaveButton.IsEnabled = false;
        PreviewImage.Source = null;
        LineImage.Source = null;
        RinkImage.Source = null;
        RinkMarks.Children.Clear();
        PreviewEmpty.Visibility = Visibility.Visible;
        PreviewEmptyText.Text = "Reading the rink's page…";
        try
        {
            var (title, layout) = await FaceoffNet.RinkAsync(url);
            if (pick != _pick) return;
            RinkTitle.Text = _title = title;
            if (layout == null)
            {
                PreviewEmptyText.Text = "This rink's page has no top-down layout to convert.";
                RinkInfo.Text = url;
                return;
            }
            PreviewEmptyText.Text = "Downloading the top-down layout (about 4 MB)…";
            string file = await FaceoffNet.DownloadAsync(layout, Path.Combine(Folder, "downloads"));
            var (src, w, h) = await Task.Run(() => ImageLoader.Load(file));
            if (pick != _pick) return;
            _src = src; _w = w; _h = h;
            _geo = await Task.Run(() => CenterIce.Measure(src, w, h));
            RinkInfo.Text = $"{w}×{h} layout, {_geo.PixelsPerFoot:0.0} px/ft " +
                            (_geo.Measured ? "(measured from the blue lines)" : "(blue lines not found: the site's usual template)") + $"  ·  {url}";
            RinkImage.Source = Small(src, w, h, 560);
            MarkRink();
            await Convert();
        }
        catch (Exception ex)
        {
            if (pick == _pick) PreviewEmptyText.Text = "Can't open this rink: " + ex.Message;
        }
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
        WriteButton.IsEnabled = SaveButton.IsEnabled = true;
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
        if (dlg.ShowDialog(this) != true) return;
        var (logo, line) = SavePngs(dlg.FolderName);
        WriteStatus.Text = $"Saved {Path.GetFileName(logo)} and {Path.GetFileName(line)}.";
    }

    private async void OnWrite(object sender, RoutedEventArgs e) => await Write();

    /// <summary>Saves the PNGs (export/faceoff) and has the Teams page write them into the team's centre ice.</summary>
    public async Task Write()
    {
        if (_logo == null) return;
        var (logo, line) = SavePngs(Folder);
        WriteButton.IsEnabled = false;
        WriteStatus.Text = $"Writing into {_team.Abbr}'s centre ice…";
        try { WriteStatus.Text = await _write(logo, _hasLine && LineCheck.IsChecked == true ? line : null); }
        catch (Exception ex) { WriteStatus.Text = "Can't write: " + ex.Message; }
        finally { WriteButton.IsEnabled = true; }
    }

    /// <summary>Scripted runs: picks the first loaded rink whose name has <paramref name="name"/>.</summary>
    public async Task PickRink(string name)
    {
        for (int i = 0; i < 100 && (_league == null || _league.Pages == 0 || _loading); i++) await Task.Delay(100);
        var row = _rows.FirstOrDefault(r => r.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (row != null) { RinkList.SelectedItem = row; RinkList.ScrollIntoView(row); }
        for (int i = 0; i < 200 && _logo == null; i++) await Task.Delay(100);
    }
}
