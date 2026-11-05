using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
/// Centre ice from TheFaceoff.net: browse a league's rinks (or paste a rink's link) and download its top-down layout;
/// <see cref="CenterIcePanel"/> converts, previews and writes it.
/// </summary>
public partial class FaceoffWindow : Window
{
    private readonly Dictionary<string, FaceoffLeague> _leagues = new();
    private readonly ObservableCollection<FaceoffRow> _rows = new();
    private FaceoffLeague _league;
    private bool _loading, _ready;

    public FaceoffWindow(TeamSummary team, bool hasLogo, bool hasLine, int logoNumber, List<TeamSummary> artSharers, List<TeamSummary> lineSharers, Func<string, string, Task<string>> write)
    {
        InitializeComponent();
        Heading.Text = $"Centre ice for {team.Name}, from TheFaceoff.net";
        IcePanel.SetTarget(team, hasLogo, hasLine, logoNumber, artSharers, lineSharers, write, "Pick a rink on the left");
        RinkList.ItemsSource = _rows;
        _ready = true;
        var leagues = FaceoffNet.Leagues.Select(l => new LeagueChoice(l.Key, l.Label)).ToList();
        LeaguePicker.ItemsSource = leagues;
        LeaguePicker.SelectedIndex = Math.Max(0, leagues.FindIndex(l => l.Key == FaceoffNet.LeagueKey(team.League)));
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
            ListStatus.Text = FaceoffNet.Explain(ex);
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
            IcePanel.ShowName(null, "That isn't a thefaceoff.net link.");
            return;
        }
        RinkList.SelectedItem = null;
        await OpenRink(uri.ToString());
    }

    /// <summary>Reads a rink's page, downloads its top-down layout (kept in export/faceoff/downloads) and converts it.</summary>
    public async Task OpenRink(string url)
    {
        int pick = IcePanel.Begin("Reading the rink's page…");
        try
        {
            var (title, layout) = await FaceoffNet.RinkAsync(url);
            if (!IcePanel.IsCurrent(pick)) return;
            IcePanel.ShowName(title, url);
            if (layout == null)
            {
                IcePanel.Say("This rink's page has no top-down layout to convert.");
                return;
            }
            IcePanel.Say("Downloading the top-down layout (about 4 MB)…");
            string file = await FaceoffNet.DownloadAsync(layout, Path.Combine(CenterIcePanel.Folder, "downloads"));
            await IcePanel.OpenPicture(pick, file, title, url);
        }
        catch (Exception ex)
        {
            if (IcePanel.IsCurrent(pick)) IcePanel.Say("Can't open this rink: " + FaceoffNet.Explain(ex));
        }
    }

    /// <summary>Scripted runs: picks the first loaded rink whose name has <paramref name="name"/>.</summary>
    public async Task PickRink(string name)
    {
        for (int i = 0; i < 100 && (_league == null || _league.Pages == 0 || _loading); i++) await Task.Delay(100);
        var row = _rows.FirstOrDefault(r => r.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (row != null) { RinkList.SelectedItem = row; RinkList.ScrollIntoView(row); }
        for (int i = 0; i < 200 && !IcePanel.HasResult; i++) await Task.Delay(100);
    }
}
