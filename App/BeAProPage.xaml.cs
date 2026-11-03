using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A value on the page that remembers what the save had, so a change shows in the patch colour.</summary>
public abstract class BapRow : Observable
{
    public Brush NameBrush => (Brush)Application.Current.FindResource(Changed ? "Patch" : "TextDim");
    public abstract bool Changed { get; }
    protected void Edited() { Raise(nameof(NameBrush)); RowChanged?.Invoke(); }
    public event Action RowChanged;
}

public sealed class BapRatingRow : BapRow
{
    private int _value;
    public BeAProRating Rating { get; init; }
    public string Name => Rating.Name;
    public int Original { get; init; }
    public int Value { get => _value; set { if (Set(ref _value, Math.Clamp(value, 0, 99))) Edited(); } }
    public override bool Changed => _value != Original;
}

public sealed class BapXFactorRow : BapRow
{
    private XFactorAbility _selected;
    private int _tier;
    public int Slot { get; init; }
    public string Label => $"X-Factor {Slot + 1}";
    public List<XFactorAbility> Choices { get; init; }
    public string[] TierNames { get; init; }
    public (int Ability, int Tier) Original { get; init; }
    public XFactorAbility Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;
            if (HasAbility && _tier == 0) TierIndex = 0;
            Raise(nameof(HasAbility));
            Edited();
        }
    }
    public bool HasAbility => _selected != null && _selected.Id >= 0;
    /// <summary>0-2 in the tier box for tiers 1-3; -1 with no ability.</summary>
    public int TierIndex { get => _tier - 1; set { if (Set(ref _tier, Math.Clamp(value + 1, 0, 3), nameof(TierIndex))) Edited(); } }
    public int Ability => HasAbility ? _selected.Id : -1;
    public int Tier => HasAbility ? Math.Max(_tier, 1) : 0;
    public override bool Changed => Ability != Original.Ability || (HasAbility && Tier != Original.Tier);
}

public sealed class BapActivityRow : BapRow
{
    private int _value, _level;
    public int Index { get; init; }
    public string Name { get; init; }
    public (int Value, int Level) Original { get; init; }
    public int Value { get => _value; set { if (Set(ref _value, value)) Edited(); } }
    public int Level { get => _level; set { if (Set(ref _level, value)) Edited(); } }
    public override bool Changed => (_value, _level) != Original;
}

public sealed class BapCareerRow : BapRow
{
    private int _value;
    public int Index { get; init; }
    public int Id { get; init; }
    public string Label => $"#{Id}";
    public int Original { get; init; }
    public int Value { get => _value; set { if (Set(ref _value, value)) Edited(); } }
    public override bool Changed => _value != Original;
}

/// <summary>
/// Be A Pro careers in KytyPS5's save folder: the pro's ratings and X-Factors, and the values not matched to the game's
/// screens yet. Saves are read fresh every time the page opens; writing copies the slot to _SaveData_backup first.
/// </summary>
public partial class BeAProPage : UserControl
{
    private const string Language = "loc/eng_us.db";
    private ModService _mods;
    private Settings _settings;
    private string _titleId;
    private BeAProSave _save;
    private List<BapRatingRow> _ratings = new();
    private List<BapXFactorRow> _xfactors = new();
    private List<BapActivityRow> _activities = new();
    private List<BapCareerRow> _career = new();
    private List<XFactorAbility> _abilities;
    private string[] _tierNames;
    private bool _busy;

    public event Action<string> StatusChanged;

    /// <summary>Where the saves are read from; the emulator's run folder unless a scripted run points elsewhere.</summary>
    public string SavesDirOverride { get; set; }

    public BeAProPage() => InitializeComponent();

    public void SetMods(ModService mods) => _mods = mods;

    public void Init(Settings settings, string titleId)
    {
        _settings = settings;
        _titleId = titleId;
    }

    private string RunDir => KytySetup.Load(TitleId, _settings?.Emulator).RunDir;
    private string TitleId => string.IsNullOrEmpty(_titleId) ? "PPSA34063" : _titleId;
    private string SavesDir => SavesDirOverride ?? BeAProSave.SavesDir(RunDir, TitleId);
    /// <summary>run\_SaveData_backup, next to the save folder.</summary>
    private string BackupDir => Path.GetFullPath(Path.Combine(SavesDir, "..", "..", "_SaveData_backup"));

    /// <summary>The page was opened: lists the careers and reads the picked one again.</summary>
    public async Task Shown()
    {
        if (_busy) return;
        if (_save != null && Dirty()) return; // keep unsaved edits while switching pages
        await LoadAsync(((BeAProSlot)SlotList.SelectedItem)?.Name);
    }

    /// <summary>Picks a career by its slot name (scripted runs).</summary>
    public async Task Show(string slot)
    {
        await LoadAsync(slot);
    }

    /// <summary>Changes values on the page without saving (scripted runs): "Overall=88;Speed=95", "xf2=Ability:Tier".</summary>
    public void SetValues(string spec)
    {
        foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            string key = kv[0].Trim();
            if (key.StartsWith("xf", StringComparison.OrdinalIgnoreCase) && int.TryParse(key[2..], out int slot) && slot is >= 1 and <= 3)
            {
                var v = kv[1].Split(':');
                var row = _xfactors.ElementAtOrDefault(slot - 1);
                if (row == null || !int.TryParse(v[0], out int id)) continue;
                row.Selected = row.Choices.FirstOrDefault(c => c.Id == id);
                if (v.Length > 1 && int.TryParse(v[1], out int tier)) row.TierIndex = tier - 1;
            }
            else if (int.TryParse(kv[1], out int value) && _ratings.FirstOrDefault(r => r.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } r)
                r.Value = value;
        }
    }

    public void ShowAdvanced() => Advanced.IsExpanded = true;
    public void ScrollTo(double y) => Scroller.ScrollToVerticalOffset(y);

    private async Task LoadAsync(string pick)
    {
        _busy = true;
        try
        {
            string dir = SavesDir;
            var slots = (await Task.Run(() => BeAProSave.Slots(dir))).OrderBy(s => s.IsBackup).ToList();
            SlotList.SelectionChanged -= OnSlotPicked;
            SlotList.ItemsSource = slots;
            SlotList.SelectionChanged += OnSlotPicked;
            SlotsEmpty.Visibility = slots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SlotsEmpty.Text = Directory.Exists(dir) ? "No Be A Pro careers in the save folder yet. Start one in the game." : $"No save folder at {dir}. Play the game in KytyPS5 once.";
            Summary.Text = $"{slots.Count} Be A Pro save{(slots.Count == 1 ? "" : "s")} in {dir}";
            if (_abilities == null && _mods != null)
            {
                try
                {
                    var loc = await _mods.OpenDbAsync(Language);
                    (_abilities, _tierNames) = await Task.Run(() => (XFactors.Abilities(loc), XFactors.TierNames(loc)));
                }
                catch { /* names are a nicety: ids are shown instead */ }
            }
            var slot = slots.FirstOrDefault(s => s.Name.Equals(pick ?? "", StringComparison.OrdinalIgnoreCase)) ?? slots.FirstOrDefault(s => !s.IsBackup) ?? slots.FirstOrDefault();
            SlotList.SelectedItem = slot;
            await OpenAsync(slot);
        }
        catch (Exception ex)
        {
            Summary.Text = "Couldn't read the saves: " + ex.Message;
        }
        finally { _busy = false; }
    }

    private async void OnSlotPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_busy) return;
        if (_save != null && Dirty() && MessageBox.Show(Window.GetWindow(this), "Drop the changes to this career?", "Be A Pro",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            SlotList.SelectionChanged -= OnSlotPicked;
            SlotList.SelectedItem = ((List<BeAProSlot>)SlotList.ItemsSource).FirstOrDefault(s => s.Dir == _save.Dir);
            SlotList.SelectionChanged += OnSlotPicked;
            return;
        }
        _busy = true;
        try { await OpenAsync(SlotList.SelectedItem as BeAProSlot); }
        finally { _busy = false; }
    }

    private async Task OpenAsync(BeAProSlot slot)
    {
        _save = null;
        Editor.Visibility = Visibility.Collapsed;
        EditorEmpty.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = false;
        if (slot == null) { EditorEmpty.Text = "Pick a career."; Detail.Text = ""; return; }
        EditorEmpty.Text = $"Reading {slot.Name}…";
        StatusChanged?.Invoke($"Reading the Be A Pro save {slot.Name}…");
        try { _save = await Task.Run(() => BeAProSave.Open(slot.Dir)); }
        catch (Exception ex)
        {
            EditorEmpty.Text = $"Couldn't read {slot.Name}: {ex.Message}";
            StatusChanged?.Invoke(EditorEmpty.Text);
            return;
        }
        Fill();
        Editor.Visibility = Visibility.Visible;
        EditorEmpty.Visibility = Visibility.Collapsed;
        StatusChanged?.Invoke($"Read {slot.Name}: {_save.Slot.Title}");
    }

    private void Fill()
    {
        var s = _save;
        ProName.Text = s.Slot.Title;
        ProDetail.Text = $"{s.Slot.Detail}  ·  player {s.PlayerId}";
        bool editable = s.HasRecord && !s.IsGoalie;
        RatingsPanel.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
        NoRecord.Visibility = editable ? Visibility.Collapsed : Visibility.Visible;
        NoRecord.Text = s.IsGoalie ? "Goalie careers: the kit hasn't mapped the goalie record yet, so ratings and X-Factors can't be edited here."
            : "The pro's player record wasn't found in this save, so ratings and X-Factors can't be edited.";

        _ratings = editable ? BeAProSave.SkaterRatings.Select(r => { int v = s.Rating(r); return new BapRatingRow { Rating = r, Original = v, Value = v }; }).ToList() : new();
        var choices = new List<XFactorAbility> { new(-1, "None", "") };
        choices.AddRange(_abilities ?? Enumerable.Range(0, XFactors.LastAbility + 1).Select(i => new XFactorAbility(i, $"X-Factor {i}", "")));
        var tiers = _tierNames ?? new[] { "Specialist", "All-Star", "Elite" };
        _xfactors = editable ? Enumerable.Range(0, 3).Select(i =>
        {
            var x = s.XFactor(i);
            var row = new BapXFactorRow { Slot = i, Choices = choices, TierNames = tiers, Original = x };
            row.Selected = choices.FirstOrDefault(c => c.Id == x.Ability) ?? choices[0];
            row.TierIndex = x.Ability >= 0 ? x.Tier - 1 : -1;
            return row;
        }).ToList() : new();
        _activities = BeAProSave.Activities.Select((n, i) => { var a = s.Activity(i); return new BapActivityRow { Index = i, Name = n, Original = a, Value = a.Value, Level = a.Level }; }).ToList();
        _career = s.CareerValues().Select((c, i) => new BapCareerRow { Index = i, Id = c.Id, Original = c.Value, Value = c.Value }).ToList();
        foreach (BapRow r in _ratings.Cast<BapRow>().Concat(_xfactors).Concat(_activities).Concat(_career)) r.RowChanged += Changed;

        Ratings.ItemsSource = _ratings;
        XFactorRows.ItemsSource = _xfactors;
        ActivityRows.ItemsSource = _activities;
        CareerRows.ItemsSource = _career;
        Changed();
    }

    private bool Dirty() => _ratings.Any(r => r.Changed) || _xfactors.Any(r => r.Changed) || _activities.Any(r => r.Changed) || _career.Any(r => r.Changed);

    private void Changed()
    {
        int n = _ratings.Count(r => r.Changed) + _xfactors.Count(r => r.Changed) + _activities.Count(r => r.Changed) + _career.Count(r => r.Changed);
        SaveButton.IsEnabled = _save != null && n > 0;
        var overall = _ratings.FirstOrDefault();
        ProOverall.Text = overall == null ? "" : overall.Value.ToString();
        Detail.Text = _save == null ? "" : n == 0 ? $"No changes. Saving copies {_save.Slot.Name} to {BackupDir} first."
            : $"{n} change{(n == 1 ? "" : "s")} not saved yet.";
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveAsync();

    public async Task SaveAsync()
    {
        if (_save == null || !Dirty()) return;
        if (ModService.GameRunning)
        {
            MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game before saving, or it will write its own copy over yours.", "Be A Pro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var s = _save;
        foreach (var r in _ratings.Where(r => r.Changed)) s.SetRating(r.Rating, r.Value);
        foreach (var x in _xfactors.Where(x => x.Changed)) s.SetXFactor(x.Slot, x.Ability, x.Tier);
        foreach (var a in _activities.Where(a => a.Changed)) s.SetActivity(a.Index, a.Value, a.Level);
        foreach (var c in _career.Where(c => c.Changed)) s.SetCareerValue(c.Index, c.Value);
        SaveButton.IsEnabled = false;
        StatusChanged?.Invoke($"Writing {s.Slot.Name}…");
        try
        {
            string backup = await Task.Run(() => s.Write(BackupDir));
            StatusChanged?.Invoke($"Saved {s.Slot.Name}. The old save is in {backup}");
            await OpenAsync((BeAProSlot)SlotList.SelectedItem);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Couldn't save: " + ex.Message);
            MessageBox.Show(Window.GetWindow(this), "Couldn't save the career:\n\n" + ex.Message, "Be A Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            await OpenAsync((BeAProSlot)SlotList.SelectedItem);
        }
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        if (_save != null && Dirty() && MessageBox.Show(Window.GetWindow(this), "Drop the changes to this career?", "Be A Pro",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        await LoadAsync(((BeAProSlot)SlotList.SelectedItem)?.Name);
    }

    private void OnOpenBackups(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(BackupDir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{BackupDir}\"") { UseShellExecute = true });
    }
}
