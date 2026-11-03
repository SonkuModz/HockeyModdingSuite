using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// The Jersey slots tab: the uniforms the game's jersey menu offers the shown team (stockteamjerseys), top first. Slots
/// can be moved, given another name, made a default, removed, or added for any kit in the game (another team's too).
/// Edits are kept per team until saved; Save writes every edited team into nhl_game.db (the database mod entry).
/// </summary>
public partial class UniformsPage
{
    private Dictionary<(string League, string Team), JerseySlots.KitTeam> _kitTeams;
    private readonly Dictionary<int, ObservableCollection<SlotRow>> _slotDrafts = new();
    /// <summary>How many slots each team had in the game when its draft was read.</summary>
    private readonly Dictionary<int, int> _slotCounts = new();
    private ObservableCollection<SlotRow> _slots;
    private JerseySlots.KitTeam _slotTeam;
    private List<PickItem> _slotLabels;
    private List<SlotKitItem> _slotKits;
    private int _slotLoad;
    /// <summary>The English language database as read with the slots, and its kit names (vintage_style_art_variant).</summary>
    private T3Db _namesDb;
    private Dictionary<string, KitNameRef> _names;

    private IEnumerable<Kit> AllKits => _teams.SelectMany(t => t.Kits);

    /// <summary>Reads the team's slots from the game (unsaved edits of the team are kept) and the kits a slot can show.</summary>
    private async Task LoadSlots(TeamRow team)
    {
        if (_mods == null || team == null) return;
        int load = ++_slotLoad;
        SlotsSource.Text = "Reading the jersey slots from the game…";
        try
        {
            var db = await _mods.OpenDbAsync(JerseySlots.Database);
            var kits = AllKits.ToList();
            _kitTeams = await Task.Run(() => JerseySlots.Teams(db, kits));
            _namesDb = await _mods.OpenDbAsync(KitNames.English);
            var namesDb = _namesDb;
            _names = await Task.Run(() => KitNames.Read(namesDb));
            if (load != _slotLoad) return;
            _slotLabels ??= Enumerable.Range(0, 16).Select(i => new PickItem(JerseySlots.LabelName(i), i)).ToList();
            var kitTeam = _kitTeams.GetValueOrDefault((team.League, team.Team));
            _slotTeam = kitTeam;
            if (kitTeam == null)
            {
                _slots = null;
                SlotList.ItemsSource = null;
                SlotsSource.Text = $"{UniformNames.Pretty(team.Team)} isn't a team in the database, so it has no jersey slots.";
                SlotButtons();
                return;
            }
            if (!_slotDrafts.TryGetValue(kitTeam.TeamId, out var rows))
            {
                var slots = JerseySlots.Read(db, kitTeam.TeamId);
                _slotCounts[kitTeam.TeamId] = slots.Count;
                rows = new ObservableCollection<SlotRow>(slots.Select((s, i) => MakeSlotRow(s, i)));
            }
            else
                foreach (var r in rows) AttachName(r, r.IsNameChanged ? r.Name : null);
            _slots = rows;
            SlotList.ItemsSource = rows;
            var t = db.Table(JerseySlots.TableName);
            SlotsSource.Text = $"{kitTeam.Name}: the jersey menu, top first. {t.Records - t.WrittenRecords} more slots fit in the game's table. Click a slot to see it.";
            // the Add picker: this team's kits first, then every other kit a slot can show
            _slotKits = kits.Select(k => (Kit: k, Team: _kitTeams.GetValueOrDefault((k.League, k.Team)), Address: JerseySlots.Address(k)))
                .Where(x => x.Team != null && x.Address != null)
                .OrderBy(x => x.Team.TeamId == kitTeam.TeamId ? 0 : 1).ThenBy(x => x.Kit.League == "nhl" ? 0 : 1)
                .ThenBy(x => x.Team.Name).ThenBy(x => x.Kit.Brand).ThenBy(x => x.Address.Value.Variant)
                .Select(x => new SlotKitItem(x.Kit, x.Team, $"{x.Team.Name}  ·  {UniformNames.Pretty(x.Kit.Brand)}  ·  {UniformNames.Pretty(x.Kit.Name)}"))
                .ToList();
            FilterAddKits();
            SlotButtons();
            foreach (var r in rows.Where(r => r.Thumb == null)) _ = SlotThumb(r);
        }
        catch (Exception ex)
        {
            SlotsSource.Text = "Can't read the jersey slots: " + ex.Message;
        }
    }

    private void OnAddKitSearch(object sender, TextChangedEventArgs e) => FilterAddKits();

    /// <summary>The Add picker's kits: those whose label has every word of the search box; the shown kit is picked when it's there.</summary>
    private void FilterAddKits()
    {
        if (_slotKits == null) return;
        var words = AddKitSearch.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var list = _slotKits.Where(k => words.All(w => k.Label.ToLowerInvariant().Contains(w))).ToList();
        AddKitPicker.ItemsSource = list;
        AddKitPicker.SelectedItem = list.FirstOrDefault(k => k.Kit == _kit) ?? list.FirstOrDefault();
    }

    private SlotRow MakeSlotRow(JerseySlot s, int position)
    {
        var kit = SlotKit(s, out var kitTeam);
        var row = new SlotRow(s, kit, kitTeam != null && kitTeam.TeamId != _slotTeam?.TeamId ? kitTeam.Name : null, position, _slotLabels, _ => { Draft(); SlotButtons(); });
        AttachName(row);
        return row;
    }

    /// <summary>Gives a row its kit's English name and the room the name has, when the kit has one.</summary>
    private void AttachName(SlotRow row, string edited = null)
    {
        string key = KitNames.Key(row.Slot.Style, row.Slot.ArtId, row.Slot.Variant);
        var db = _namesDb;
        if (_names?.GetValueOrDefault(key) is { } at)
            row.SetName(key, at.Text, text => KitNames.Budget(db, at, text), edited);
        else
            row.SetName(null, null, null);
    }

    /// <summary>The kit folder a slot shows: its art team's folder of that brand and number.</summary>
    private Kit SlotKit(JerseySlot s, out JerseySlots.KitTeam team)
    {
        // a team's own folders first (several folders can share an art id)
        foreach (var (key, kt) in _kitTeams.Where(x => x.Value.ArtId == s.ArtId).OrderBy(x => x.Value.TeamId == _slotTeam?.TeamId ? 0 : 1))
        {
            var kit = AllKits.FirstOrDefault(k => (k.League, k.Team) == key && JerseySlots.Address(k) == (s.Style, s.Variant));
            if (kit != null) { team = kt; return kit; }
        }
        team = null;
        return null;
    }

    private async Task SlotThumb(SlotRow row)
    {
        if (row.Kit?.Diffuse("jersey") is not { } tex) return;
        try { row.Thumb = await _textures.ThumbnailAsync(tex.Name, 96); }
        catch { /* no picture */ }
    }

    private void SlotButtons()
    {
        if (SlotSave == null) return;
        var edited = _slotDrafts.Where(d => d.Value.Any(r => r.IsChanged) || Removed(d.Key, d.Value)).ToList();
        bool nameErrors = _slotDrafts.Values.SelectMany(v => v).Any(r => r.IsNameChanged && r.HasNameError);
        SlotSave.IsEnabled = !_busy && edited.Count > 0 && !nameErrors;
        SlotDiscard.IsEnabled = !_busy && edited.Count > 0;
        AddSlotButton.IsEnabled = !_busy && _slots != null;
        int defaults = _slots?.Count(r => r.IsDefault) ?? 0;
        SlotStatus.Text = _slots == null ? "" :
            (defaults != 2 ? $"{defaults} default slot(s): the game's teams have two, a home and an away. " : "") +
            (nameErrors ? "A kit name doesn't fit (see the red box). " : "") +
            (edited.Count == 0 ? $"{_slots.Count} slots. Use the arrows to move one, ✕ to remove it, or add any kit below."
                : $"Unsaved slot changes for {string.Join(", ", edited.Select(d => TeamName(d.Key)))}.");
    }

    /// <summary>True when the draft is missing a slot the game has (it was removed).</summary>
    private bool Removed(int teamId, IEnumerable<SlotRow> rows) => rows.Count(r => !r.IsNew) < _slotCounts.GetValueOrDefault(teamId);

    private string TeamName(int teamId) => _kitTeams?.Values.FirstOrDefault(t => t.TeamId == teamId)?.Name ?? $"team {teamId}";

    /// <summary>The shown team's slots become a draft (kept until saved or discarded) the first time one is edited.</summary>
    private ObservableCollection<SlotRow> Draft()
    {
        if (_slots == null || _slotTeam == null) return null;
        _slotDrafts[_slotTeam.TeamId] = _slots;
        return _slots;
    }

    private void Renumber()
    {
        for (int i = 0; i < _slots.Count; i++) _slots[i].Position = i;
        SlotButtons();
    }

    private void OnSlotUp(object sender, RoutedEventArgs e) => MoveSlot(sender, -1);
    private void OnSlotDown(object sender, RoutedEventArgs e) => MoveSlot(sender, +1);
    private void OnSlotTop(object sender, RoutedEventArgs e) => MoveSlot(sender, int.MinValue);

    private void MoveSlot(object sender, int by)
    {
        if ((sender as FrameworkElement)?.DataContext is not SlotRow row || Draft() is not { } rows) return;
        int from = rows.IndexOf(row);
        int to = by == int.MinValue ? 0 : Math.Clamp(from + by, 0, rows.Count - 1);
        MoveSlotTo(row, to);
    }

    /// <summary>Moves a slot to a place in the menu (0 is the top).</summary>
    public void MoveSlotTo(SlotRow row, int to)
    {
        if (Draft() is not { } rows) return;
        int from = rows.IndexOf(row);
        to = Math.Clamp(to, 0, rows.Count - 1);
        if (from < 0 || from == to) return;
        rows.Move(from, to);
        Renumber();
        SlotList.ScrollIntoView(row);
    }

    private void OnSlotRemove(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SlotRow row || Draft() is not { } rows) return;
        if (rows.Count == 1) { StatusChanged?.Invoke("A team needs at least one jersey slot."); return; }
        rows.Remove(row);
        Renumber();
        StatusChanged?.Invoke($"Removed {row.Title} from the menu (not saved yet; Discard brings it back).");
    }

    /// <summary>Removes the slot at a menu place (scripted runs, 1-based like the list).</summary>
    public void RemoveSlot(int place)
    {
        if (_slots == null || place < 1 || place > _slots.Count || Draft() is not { } rows || rows.Count == 1) return;
        rows.RemoveAt(place - 1);
        Renumber();
    }

    private void OnAddSlot(object sender, RoutedEventArgs e)
    {
        if (AddKitPicker.SelectedItem is SlotKitItem item) _ = AddSlot(item);
        else StatusChanged?.Invoke("Pick a kit to add first (type to search, e.g. boston adidas home).");
    }

    /// <summary>Adds a slot for a kit at the end of the menu (its other values come from the kit's own slot, when it has one).</summary>
    private async Task AddSlot(SlotKitItem item)
    {
        if (Draft() is not { } rows || JerseySlots.Address(item.Kit) is not { } a) return;
        var db = await _mods.OpenDbAsync(JerseySlots.Database);
        var slot = JerseySlots.NewSlot(db, item.Team.ArtId, a.Style, a.Variant);
        var row = MakeSlotRow(slot, rows.Count);
        rows.Add(row);
        Renumber();
        SlotList.ScrollIntoView(row);
        _ = SlotThumb(row);
        StatusChanged?.Invoke($"Added {item.Label} as slot {rows.Count} (not saved yet).");
    }

    /// <summary>Adds a slot for the first kit whose picker label has all the words (scripted runs: "boston adidas home").</summary>
    public Task AddSlot(string search)
    {
        AddKitSearch.Text = search ?? "";
        FilterAddKits();
        return AddKitPicker.SelectedItem is SlotKitItem item ? AddSlot(item) : Task.CompletedTask;
    }

    /// <summary>Moves the slot at a menu place to another (scripted runs, 1-based like the list: "27:1").</summary>
    public void MoveSlot(string spec)
    {
        var p = (spec ?? "").Split(':');
        if (_slots != null && p.Length == 2 && int.TryParse(p[0], out int from) && int.TryParse(p[1], out int to) && from >= 1 && from <= _slots.Count)
            MoveSlotTo(_slots[from - 1], to - 1);
    }

    private async void OnSlotClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SlotRow row || e.OriginalSource is DependencyObject d && IsInButton(d)) return;
        foreach (var r in _slots ?? new()) r.IsSelected = r == row;
        if (row.Kit == null) { StatusChanged?.Invoke("No kit folder matches this slot's art team, brand and number."); return; }
        if (row.Kit != _kit) await ShowKit(row.Kit);
    }

    private static bool IsInButton(DependencyObject d)
    {
        for (; d != null; d = d is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(d) : null)
            if (d is System.Windows.Controls.Primitives.ButtonBase or ComboBox or CheckBox or TextBox) return true;
        return false;
    }

    private async void OnSlotDiscard(object sender, RoutedEventArgs e)
    {
        _slotDrafts.Clear();
        await LoadSlots(_team);
        StatusChanged?.Invoke("Slot changes discarded.");
    }

    private async void OnSlotSave(object sender, RoutedEventArgs e) => await SaveSlotsAsync();

    /// <summary>
    /// Writes every edited team's slots into nhl_game.db and renamed kits into the language databases, then reads them
    /// back from the game.
    /// </summary>
    public async Task SaveSlotsAsync()
    {
        var drafts = _slotDrafts.Where(d => d.Value.Any(r => r.IsSlotChanged) || Removed(d.Key, d.Value))
            .Select(d => (TeamId: d.Key, Name: TeamName(d.Key), Slots: d.Value.Select(r => r.Slot).ToList())).ToList();
        // a kit shown by two slots has one name: the last edit wins
        var renames = _slotDrafts.Values.SelectMany(v => v).Where(r => r.IsNameChanged && !r.HasNameError)
            .GroupBy(r => r.NameKey).ToDictionary(g => g.Key, g => (Old: g.Last().OriginalName, New: g.Last().Name));
        if ((drafts.Count == 0 && renames.Count == 0) || _busy) return;
        if (ModService.GameRunning)
        {
            StatusChanged?.Invoke("Close KytyPS5 before saving: the game has its files open.");
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _busy = true;
        SlotButtons();
        var said = new List<string>();
        try
        {
            if (renames.Count > 0) said.Add(await SaveNamesAsync(renames));
            string summary = null;
            if (drafts.Count > 0)
            await _mods.SaveDbAsync(JerseySlots.Database, db =>
            {
                var parts = new List<string>();
                foreach (var d in drafts)
                {
                    // rows move when another team's slots are added or removed: kept slots are found again by their
                    // number, new ones start again from their kit's slot as the table is now
                    var live = JerseySlots.Read(db, d.TeamId);
                    var slots = d.Slots.Select(s => s.Row >= 0
                        ? s with { Row = live.First(l => l.JerseyIndex == s.JerseyIndex).Row }
                        : JerseySlots.NewSlot(db, s.ArtId, s.Style, s.Variant) with { Label = s.Label, IsDefault = s.IsDefault, IsLight = s.IsLight }).ToList();
                    parts.Add($"{d.Name} slots: {JerseySlots.Write(db, d.TeamId, slots)}");
                }
                summary = "Uniforms: " + string.Join("; ", parts);
                return summary;
            }, "Uniforms page");
            _slotDrafts.Clear();
            if (summary != null) said.Add(summary.Replace("Uniforms: ", "") + " (nhl_game.db)");
            StatusChanged?.Invoke(string.Join(". ", said) + ". Written into the game; the Mods page can restore the originals.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't save the slots: " + ex.Message);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't save to the game", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        await LoadSlots(_team);
    }

    /// <summary>
    /// Renames kits in every language database that has the name and room for the new one (each language is its own
    /// mod entry). The text goes into every language as typed. Returns what was done, naming languages it didn't fit.
    /// </summary>
    private async Task<string> SaveNamesAsync(Dictionary<string, (string Old, string New)> renames)
    {
        var languages = (await _mods.LegacyNamesAsync()).Where(n => n.StartsWith("loc/") && n.EndsWith(".db")).OrderBy(n => n != KitNames.English).ThenBy(n => n).ToList();
        var written = new List<string>();
        var skipped = new List<string>();
        foreach (string lang in languages)
        {
            // which names this language has and has room for, checked before anything is written
            var db = await _mods.OpenDbAsync(lang);
            var names = await Task.Run(() => KitNames.Read(db));
            var fits = renames.Where(r => names.TryGetValue(r.Key, out var at) && KitNames.Budget(db, at, r.Value.New) is var b && b.Need >= 0 && b.Need <= b.Have)
                              .ToDictionary(r => r.Key, r => r.Value);
            string code = System.IO.Path.GetFileNameWithoutExtension(lang);
            if (fits.Count < renames.Count) skipped.Add(code);
            if (fits.Count == 0) continue;
            await _mods.SaveDbAsync(lang, ldb =>
            {
                var live = KitNames.Read(ldb);
                foreach (var (key, r) in fits) KitNames.Set(ldb, live[key], r.New);
                return $"Kit names ({code}): " + string.Join(", ", fits.Select(f => $"\"{names[f.Key].Text}\" → \"{f.Value.New}\""));
            }, "Uniforms page");
            written.Add(code);
        }
        return $"{renames.Count} kit name(s) renamed in {written.Count} language(s)" +
               (skipped.Count > 0 ? $"; not in {string.Join(", ", skipped)} (no such name there, or the new one doesn't fit)" : "");
    }

    /// <summary>Types a kit name into the slot at a menu place (scripted runs: "1=My Name").</summary>
    public void RenameSlot(string spec)
    {
        int eq = (spec ?? "").IndexOf('=');
        if (_slots != null && eq > 0 && int.TryParse(spec[..eq], out int place) && place >= 1 && place <= _slots.Count && _slots[place - 1].HasName)
        {
            _slots[place - 1].Name = spec[(eq + 1)..];
            SlotList.ScrollIntoView(_slots[place - 1]);
        }
    }

    public void ShowSlotsTab() => TabSlots.IsChecked = true;
}
