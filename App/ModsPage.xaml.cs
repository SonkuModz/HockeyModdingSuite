using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>One mod in the list, with its state in the game.</summary>
public sealed class ModRow
{
    public ModRow(ModEntry e, ModState state)
    {
        Entry = e;
        State = state;
        string name = e.Kind == "sound" && e.Name.Contains('#') ? e.Name[..e.Name.LastIndexOf('#')] : e.Name;
        string take = e.Kind == "sound" && e.Name.Contains('#') ? "  ·  variation " + e.Name[(e.Name.LastIndexOf('#') + 1)..] : "";
        int slash = name.LastIndexOf('/');
        Leaf = (slash < 0 ? name : name[(slash + 1)..]) + take;
        Folder = slash < 0 ? "" : name[..slash];
        Glyph = e.Kind == "sound" ? "" : e.Kind == LegacyEditor.Kind ? "" : e.Kind == DbEditor.Kind ? "" : "";
        Tint = (Brush)Application.Current.FindResource(e.Kind == "sound" ? "Violet" : e.Kind == DbEditor.Kind ? "Good" : e.Kind == LegacyEditor.Kind ? "Patch" : "Accent");
        long bytes = e.Patches.Sum(p => (long)p.Length);
        Detail = $"{e.Summary}  ·  {e.Patches.Count} range{(e.Patches.Count == 1 ? "" : "s")}, {Visuals.Size(bytes)}  ·  from {System.IO.Path.GetFileName(e.Source)}  ·  {e.Created:MMM d, HH:mm}";
        WarningText = string.Join("\n", e.Warnings.Select(w => "• " + w));
    }

    public ModEntry Entry { get; }
    public ModState State { get; }
    public string Leaf { get; }
    public string Folder { get; }
    public string Glyph { get; }
    public Brush Tint { get; }
    public string Detail { get; }
    public string WarningText { get; }
    public bool HasWarnings => WarningText.Length > 0;
    public bool CanApply => State is ModState.NotApplied or ModState.Partial;
    public bool CanRestore => State is ModState.Applied or ModState.Partial;
    public bool CanRemove => State is ModState.NotApplied or ModState.Missing or ModState.Changed;

    public string StateText => State switch
    {
        ModState.NotApplied => "Not applied",
        ModState.Applied => "In the game",
        ModState.Partial => "Partly applied",
        ModState.Changed => "Game files changed",
        _ => "Files missing",
    };

    public Brush StateBrush => (Brush)Application.Current.FindResource(State switch
    {
        ModState.Applied => "Good",
        ModState.NotApplied => "Muted",
        ModState.Partial => "Patch",
        _ => "Danger",
    });
}

public partial class ModsPage : UserControl
{
    private ModService _mods;
    private bool _busy;

    public event Action<string> StatusChanged;

    public ModsPage() => InitializeComponent();

    public void SetMods(ModService mods)
    {
        _mods = mods;
        _mods.Changed += () => Dispatcher.BeginInvoke(new Action(() => _ = Refresh()));
        _ = Refresh();
    }

    public async Task Refresh()
    {
        if (_mods == null) return;
        var states = await _mods.StatesAsync();
        var rows = _mods.Store.Entries.OrderByDescending(e => e.Created).Select(e => new ModRow(e, states.GetValueOrDefault(e, ModState.Missing))).ToList();
        List.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        int applied = rows.Count(r => r.State == ModState.Applied), pending = rows.Count(r => r.CanApply);
        Summary.Text = rows.Count == 0 ? "Replacements you have made, and whether they are in the game."
            : $"{rows.Count} replacement{(rows.Count == 1 ? "" : "s")}  ·  {applied} in the game  ·  {pending} not applied";
        ApplyAllLabel.Text = pending > 0 ? $"Apply all ({pending})" : "Apply all";
        ApplyAllButton.IsEnabled = pending > 0 && !_busy;
        RestoreAllButton.IsEnabled = rows.Any(r => r.CanRestore) && !_busy;
        ExportButton.IsEnabled = rows.Count > 0 && !_busy;
        ImportButton.IsEnabled = !_busy;
    }

    private static ModRow RowOf(object sender) => (sender as FrameworkElement)?.DataContext as ModRow;

    private bool CheckGame()
    {
        if (!ModService.GameRunning) return true;
        MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "NHL27Kit", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private async Task Do(string what, Func<Task> work)
    {
        if (_busy || !CheckGame()) return;
        _busy = true;
        ApplyAllButton.IsEnabled = RestoreAllButton.IsEnabled = false;
        try
        {
            StatusChanged?.Invoke(what + "…");
            await work();
            StatusChanged?.Invoke(what + ": done");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(what + " failed: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), ex.Message, "NHL27Kit", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
            await Refresh();
        }
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        var row = RowOf(sender);
        if (row != null) await Do($"Applying {row.Leaf}", () => _mods.ApplyAsync(row.Entry));
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var row = RowOf(sender);
        if (row != null) await Do($"Restoring {row.Leaf}", () => _mods.RestoreAsync(row.Entry));
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        var row = RowOf(sender);
        if (row == null) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Remove {row.Leaf} from the list? Its saved original bytes are deleted with it.", "NHL27Kit",
                            MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _mods.Remove(row.Entry);
    }

    private async void OnExportPack(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Export mod pack", Filter = ModPack.Filter, FileName = "my-mods" + ModPack.Extension };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await ExportPack(dlg.FileName);
    }

    public async Task ExportPack(string file)
    {
        var entries = _mods.Store.Entries.ToList();
        if (entries.Count == 0) return;
        await Do("Exporting the pack", async () =>
        {
            var notes = await _mods.ExportPackAsync(file, System.IO.Path.GetFileNameWithoutExtension(file), entries);
            StatusChanged?.Invoke($"Saved {System.IO.Path.GetFileName(file)}: {entries.Count - notes.Count} replacement(s)" + (notes.Count > 0 ? $", {notes.Count} left out" : ""));
            if (notes.Count > 0 && !App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), "Left out of the pack:\n\n" + string.Join("\n", notes), "NHL27Kit", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async void OnImportPack(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Import mod pack", Filter = ModPack.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Write the replacements in {System.IO.Path.GetFileName(dlg.FileName)} into the game files?\n\nThe originals stay in the kit's mods folder, and Restore puts them back.",
                            "NHL27Kit", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        await ImportPack(dlg.FileName);
    }

    public async Task ImportPack(string file)
    {
        await Do("Importing the pack", async () =>
        {
            var results = await _mods.ImportPackAsync(file, line => Dispatcher.BeginInvoke(() => StatusChanged?.Invoke("Importing " + line)));
            int ok = results.Count(r => r.StartsWith("✓"));
            StatusChanged?.Invoke($"{System.IO.Path.GetFileName(file)}: {ok} of {results.Count} written into the game");
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), string.Join("\n", results), $"Imported {ok} of {results.Count}", MessageBoxButton.OK,
                                ok == results.Count ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void OnApplyAll(object sender, RoutedEventArgs e)
    {
        var rows = (List.ItemsSource as List<ModRow>)?.Where(r => r.CanApply).ToList() ?? new();
        if (rows.Count == 0) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Write {rows.Count} replacement{(rows.Count == 1 ? "" : "s")} into the game files?\n\nThe originals stay in the kit's mods folder, and Restore puts them back.",
                            "NHL27Kit", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        await Do($"Applying {rows.Count} replacement(s)", async () => { foreach (var r in rows) await _mods.ApplyAsync(r.Entry); });
    }

    private async void OnRestoreAll(object sender, RoutedEventArgs e)
    {
        var rows = (List.ItemsSource as List<ModRow>)?.Where(r => r.CanRestore).ToList() ?? new();
        if (rows.Count == 0) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Put the originals of {rows.Count} replacement{(rows.Count == 1 ? "" : "s")} back into the game files?",
                            "NHL27Kit", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        await Do($"Restoring {rows.Count} replacement(s)", async () => { foreach (var r in rows) await _mods.RestoreAsync(r.Entry); });
    }
}
