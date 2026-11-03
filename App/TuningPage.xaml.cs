using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A group path in the tree (AI → Goalie → HIKSaves), with how many values sit in it and under it.</summary>
public sealed class TuningGroup : Observable
{
    private bool _expanded, _selected;
    public string Name { get; init; }
    /// <summary>The full path; null for "All values", "" for values the eboot gives no group.</summary>
    public string Path { get; init; }
    public int Depth { get; init; }
    public GridLength Indent => new(Depth * 14);
    public List<TuningGroup> Children { get; } = new();
    public int Count { get; set; }
    public string CountText => Count.ToString("N0");
    public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public bool Holds(Tunable t) => Path == null || (Path.Length == 0 ? string.IsNullOrEmpty(t.Group)
        : t.Group != null && (t.Group == Path || t.Group.StartsWith(Path + "/", StringComparison.Ordinal)));
}

/// <summary>
/// The gameplay tuning values compiled into eboot.bin (defaults and ranges), read from the game every time the page opens.
/// Read only for now: changing one means patching the eboot.
/// </summary>
public partial class TuningPage : UserControl
{
    private List<Tunable> _all = new();
    private TuningGroup _group;
    private List<Tunable> _shown = new();
    private bool _busy;
    private Task _load = Task.CompletedTask;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public event Action<string> StatusChanged;
    public string GameDir { get; set; }

    public TuningPage()
    {
        InitializeComponent();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyFilter(); };
    }

    /// <summary>The page was opened: reads eboot.bin fresh from the game.</summary>
    public Task Shown() => _busy ? _load : _load = LoadAsync();

    /// <summary>Filters the list (scripted runs): a search, a group path and the ratings switch.</summary>
    public void Show(string search, string group, bool ratings)
    {
        if (ratings) RatingsOnly.IsChecked = true;
        if (group != null && Find(Groups.ItemsSource as List<TuningGroup>, group) is { } g)
        {
            if (_group != null) _group.IsSelected = false;
            _group = g;
            ExpandTo(g.Path);
        }
        if (search != null) Search.Text = search;
        ApplyFilter();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        string eboot = GameDir == null ? null : System.IO.Path.Combine(GameDir, "eboot.bin");
        if (eboot == null || !File.Exists(eboot))
        {
            Summary.Text = "No eboot.bin in the game folder. Point Settings at the extracted NHL 27 folder.";
            return;
        }
        _busy = true;
        ReloadButton.IsEnabled = ExportButton.IsEnabled = false;
        RowsBusy.Visibility = Visibility.Visible;
        StatusChanged?.Invoke("Reading eboot.bin…");
        string keep = _group?.Path;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _all = await Task.Run(() => Tunables.Read(eboot));
            int ratings = _all.Count(t => t.IsRating);
            Summary.Text = $"{_all.Count:N0} values in eboot.bin  ·  {ratings:N0} name a rating or attribute  ·  defaults and ranges as the game ships them";
            TypeAll.Tag = _all.Count.ToString("N0");
            TypeFloat.Tag = _all.Count(t => t.Type == TunableType.Float).ToString("N0");
            TypeInt.Tag = _all.Count(t => t.Type == TunableType.Int).ToString("N0");
            TypeBool.Tag = _all.Count(t => t.Type == TunableType.Bool).ToString("N0");
            var roots = BuildTree(_all);
            Groups.ItemsSource = roots;
            GroupCount.Text = _all.Select(t => t.Group ?? "").Distinct().Count().ToString("N0");
            _group = Find(roots, keep) ?? roots[0];
            if (_group.Path is { Length: > 0 }) ExpandTo(_group.Path); else _group.IsSelected = true;
            ApplyFilter();
            StatusChanged?.Invoke($"Read {_all.Count:N0} tuning values from eboot.bin in {sw.Elapsed.TotalSeconds:0.0}s");
        }
        catch (Exception ex)
        {
            Summary.Text = "Couldn't read eboot.bin: " + ex.Message;
        }
        finally
        {
            _busy = false;
            ReloadButton.IsEnabled = true;
            ExportButton.IsEnabled = _all.Count > 0;
            RowsBusy.Visibility = Visibility.Collapsed;
        }
    }

    private static List<TuningGroup> BuildTree(List<Tunable> all)
    {
        var top = new TuningGroup { Name = "All values", Path = null, Count = all.Count };
        var nodes = new Dictionary<string, TuningGroup>();
        var roots = new List<TuningGroup>();
        foreach (var t in all)
        {
            if (string.IsNullOrEmpty(t.Group)) continue;
            string path = "";
            List<TuningGroup> level = roots;
            foreach (var part in t.Group.Split('/'))
            {
                path = path.Length == 0 ? part : path + "/" + part;
                if (!nodes.TryGetValue(path, out var n))
                {
                    nodes[path] = n = new TuningGroup { Name = part, Path = path, Depth = path.Count(c => c == '/') };
                    level.Add(n);
                }
                n.Count++;
                level = n.Children;
            }
        }
        void Sort(List<TuningGroup> l)
        {
            l.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var n in l) Sort(n.Children);
        }
        Sort(roots);
        int none = all.Count(t => string.IsNullOrEmpty(t.Group));
        if (none > 0) roots.Add(new TuningGroup { Name = "(no group)", Path = "", Count = none });
        roots.Insert(0, top);
        return roots;
    }

    private static TuningGroup Find(List<TuningGroup> level, string path)
    {
        if (level == null) return null;
        foreach (var n in level)
        {
            if (n.Path == path || (path != null && n.Path != null && n.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return n;
            if (path != null && n.Path is { Length: > 0 } p && path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase) && Find(n.Children, path) is { } hit) return hit;
        }
        return null;
    }

    /// <summary>Opens the tree down to a group and selects it.</summary>
    private void ExpandTo(string path)
    {
        foreach (var n in Chain(Groups.ItemsSource as List<TuningGroup>, path))
        {
            if (n.Path == path) n.IsSelected = true;
            else n.IsExpanded = true;
        }
    }

    private static IEnumerable<TuningGroup> Chain(List<TuningGroup> level, string path)
    {
        while (level != null)
        {
            var n = level.FirstOrDefault(g => g.Path is { Length: > 0 } p && (p == path || path.StartsWith(p + "/", StringComparison.Ordinal)));
            if (n == null) yield break;
            yield return n;
            level = n.Children;
        }
    }

    private void ApplyFilter()
    {
        string q = Search.Text.Trim();
        bool ratings = RatingsOnly.IsChecked == true;
        TunableType? type = TypeFloat.IsChecked == true ? TunableType.Float : TypeInt.IsChecked == true ? TunableType.Int : TypeBool.IsChecked == true ? TunableType.Bool : null;
        _shown = _all.Where(t => (_group == null || _group.Holds(t)) && (type == null || t.Type == type) && (!ratings || t.IsRating)
                                 && (q.Length == 0 || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || (t.Label?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)))
                     .ToList();
        Rows.ItemsSource = _shown;
        Detail.Text = $"{_shown.Count:N0} shown" + (_group?.Path is { Length: > 0 } p ? $" in {p}" : "");
    }

    private void OnGroupPicked(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TuningGroup g) return;
        _group = g;
        ApplyFilter();
    }

    private void OnFilter(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    private void OnSearch(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnRowPicked(object sender, SelectionChangedEventArgs e)
    {
        if (Rows.SelectedItem is not Tunable t) return;
        string range = t.Type == TunableType.Bool ? "" : $", range {(t.MinText.Length > 0 ? t.MinText : "?")} to {(t.MaxText.Length > 0 ? t.MaxText : "?")}";
        Detail.Text = $"{t.Name}: {t.TypeText}, default {(t.DefaultText.Length > 0 ? t.DefaultText : "computed at start-up")}{range}  ·  {(string.IsNullOrEmpty(t.Group) ? "no group" : t.Group)}  ·  registered at {t.SiteText}";
    }

    private async void OnReload(object sender, RoutedEventArgs e) => await LoadAsync();

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Title = "Export the tuning values shown", Filter = "CSV|*.csv", FileName = "tunables.csv", InitialDirectory = KitPaths.Export };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        SaveCsv(dlg.FileName);
    }

    /// <summary>Writes the values shown to a CSV file.</summary>
    public void SaveCsv(string file)
    {
        Tunables.WriteCsv(_shown, file);
        StatusChanged?.Invoke($"Saved {_shown.Count:N0} tuning values to {file}");
    }
}
