using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A table in the list, with how many of its values are edited and not saved yet.</summary>
public sealed class DbTableRow : INotifyPropertyChanged
{
    public T3Table Table { get; init; }
    public string Name => Table.Name;
    public string Meta => $"{Table.WrittenRecords:N0} rows  ·  {Table.Fields.Count} fields";
    private int _edits;
    public int Edits { get => _edits; set { _edits = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditText))); } }
    public string EditText => _edits > 0 ? $"{_edits} edited" : "";
    public event PropertyChangedEventHandler PropertyChanged;
}

/// <summary>
/// Every table of the game's databases as a grid. The database is read from the game on every open, reload and save.
/// Edits go into a working copy as they're made (so a value that doesn't fit is refused right there), and Save writes
/// them into the game through the database's one mod entry, the same one the Roster page adds to.
/// </summary>
public partial class DatabasePage : UserControl
{
    private ModService _mods;
    private T3Db _db;
    private string _dbName;
    private T3Table _table;
    private DataTable _rows;
    private List<DbTableRow> _tableRows = new();
    private readonly Dictionary<(string Table, int Record, string Field), object> _edits = new();
    private bool _busy, _suppress;
    private string _editOld;
    private readonly DispatcherTimer _rowTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public event Action<string> StatusChanged;

    public DatabasePage()
    {
        InitializeComponent();
        _rowTimer.Tick += (_, _) => { _rowTimer.Stop(); ApplyRowFilter(); };
    }

    public void SetMods(ModService mods) => _mods = mods;

    /// <summary>The page was opened: lists the databases (once) and reads the picked one fresh from the game.</summary>
    public async Task Shown()
    {
        if (_mods == null || _busy) return;
        if (DbPicker.Items.Count == 0)
        {
            var names = (await _mods.LegacyNamesAsync()).Where(n => n.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.StartsWith("gamemodes/database") ? 0 : n.StartsWith("gamemodes") ? 1 : 2).ThenBy(n => n).ToList();
            _suppress = true;
            DbPicker.ItemsSource = names;
            DbPicker.SelectedIndex = names.Count > 0 ? 0 : -1;
            _suppress = false;
        }
        if (_edits.Count == 0) await LoadAsync();
    }

    /// <summary>Picks a database and table and filters the rows (scripted runs).</summary>
    public async Task Show(string db, string table, string find)
    {
        if (db != null && DbPicker.ItemsSource is List<string> names && names.FirstOrDefault(n => n.Contains(db, StringComparison.OrdinalIgnoreCase)) is { } n)
        {
            _suppress = true;
            DbPicker.SelectedItem = n;
            _suppress = false;
            await LoadAsync();
        }
        if (table != null) TableList.SelectedItem = _tableRows.FirstOrDefault(t => t.Name.Equals(table, StringComparison.OrdinalIgnoreCase));
        if (find != null) { RowSearch.Text = find; ApplyRowFilter(); }
    }

    private async Task LoadAsync()
    {
        if (DbPicker.SelectedItem is not string name) return;
        _busy = true;
        SetButtons();
        RowsBusy.Visibility = Visibility.Visible;
        string keepTable = _table?.Name;
        try
        {
            _db = await _mods.OpenDbAsync(name);
            _dbName = name;
            _edits.Clear();
            var (ok, total) = _db.CheckCrcs();
            var state = _mods.Find(DbEditor.Kind, name) is { } e ? (await _mods.StatesAsync()).GetValueOrDefault(e) : (ModState?)null;
            Summary.Text = $"{name}  ·  {_db.Tables.Count} tables  ·  CRCs {ok}/{total} match" +
                           (state == ModState.Applied ? "  ·  has your saved edits (Mods page restores the original)" : "");
            _tableRows = _db.Tables.OrderBy(t => t.Name).Select(t => new DbTableRow { Table = t }).ToList();
            ApplyTableFilter();
            TableList.SelectedItem = _tableRows.FirstOrDefault(t => t.Name == keepTable) ?? _tableRows.FirstOrDefault(t => t.Table.WrittenRecords > 0);
        }
        catch (Exception ex)
        {
            Summary.Text = "Couldn't read this database: " + ex.Message;
        }
        finally
        {
            _busy = false;
            RowsBusy.Visibility = Visibility.Collapsed;
            SetButtons();
        }
    }

    private void ApplyTableFilter()
    {
        string q = TableSearch.Text.Trim();
        var rows = _tableRows.Where(t => q.Length == 0 || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        TableList.ItemsSource = rows;
        TableCount.Text = $"{rows.Count}";
    }

    private void OnTableSearch(object sender, TextChangedEventArgs e) => ApplyTableFilter();

    private void OnTablePicked(object sender, SelectionChangedEventArgs e)
    {
        if (TableList.SelectedItem is DbTableRow row) ShowTable(row.Table);
    }

    /// <summary>The table's rows as a grid (every value as text; the "#" column is the record number).</summary>
    private void ShowTable(T3Table t)
    {
        _table = t;
        // text first (names, cities…), then the numbers in the table's own order
        var fields = t.Fields.Where(f => f.Type is not (T3FieldType.Integer or T3FieldType.Float)).Concat(t.Fields.Where(f => f.Type is T3FieldType.Integer or T3FieldType.Float)).ToList();
        _rows = new DataTable();
        _rows.Columns.Add("#", typeof(int));
        foreach (var f in fields) _rows.Columns.Add(f.Name, typeof(string));
        _rows.BeginLoadData();
        int shown = 0;
        for (int i = 0; i < t.WrittenRecords; i++)
        {
            if (_db.IsInvalid(t, i)) continue;
            var values = new object[fields.Count + 1];
            values[0] = i;
            for (int k = 0; k < fields.Count; k++) values[k + 1] = Text(_db.Get(t, i, fields[k]));
            _rows.Rows.Add(values);
            shown++;
        }
        _rows.EndLoadData();

        Rows.Columns.Clear();
        Rows.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding("[#]"), IsReadOnly = true, Foreground = (System.Windows.Media.Brush)FindResource("Muted") });
        foreach (var f in fields)
            Rows.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock { Text = f.Name, ToolTip = Describe(f) },
                Binding = new Binding($"[{f.Name}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            });
        Rows.ItemsSource = _rows.DefaultView;
        TableTitle.Text = t.Name;
        TableMeta.Text = $"{shown:N0} rows  ·  {t.Fields.Count} fields  ·  {t.Records:N0} slots" + (t.HasCompressedStrings ? "  ·  compressed text" : "") +
                         "  ·  double-click a cell to edit";
        ApplyRowFilter();
    }

    private static string Describe(T3Field f) => f.Type switch
    {
        T3FieldType.Integer => $"{f.Name}: whole number, {f.RangeLow} to {f.RangeLow + (1L << f.Depth) - 1}",
        T3FieldType.Float => $"{f.Name}: decimal number",
        T3FieldType.String => $"{f.Name}: text, up to {f.Depth / 8 - 1} bytes",
        _ => $"{f.Name}: compressed text (a new value must compress to no more than the old one)",
    };

    private static string Text(object v) => v switch
    {
        float x => x.ToString("0.######", CultureInfo.InvariantCulture),
        null => "",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture),
    };

    private void OnRowSearch(object sender, TextChangedEventArgs e) { _rowTimer.Stop(); _rowTimer.Start(); }

    /// <summary>Shows the rows with the search text in any of their values.</summary>
    private void ApplyRowFilter()
    {
        if (_rows == null) return;
        string q = RowSearch.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("*", "[*]").Replace("%", "[%]");
        try
        {
            _rows.DefaultView.RowFilter = q.Length == 0 ? "" :
                string.Join(" OR ", _rows.Columns.Cast<DataColumn>().Select(c => c.ColumnName == "#" ? $"CONVERT([#], 'System.String') LIKE '%{q}%'" : $"[{c.ColumnName}] LIKE '%{q}%'"));
        }
        catch (Exception ex) when (ex is EvaluateException or SyntaxErrorException) { _rows.DefaultView.RowFilter = ""; }
        StatusChanged?.Invoke(q.Length == 0 ? "" : $"{_rows.DefaultView.Count:N0} of {_rows.Rows.Count:N0} rows have \"{RowSearch.Text.Trim()}\"");
    }

    private void OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (_busy) { e.Cancel = true; return; }
        _editOld = (e.Row.Item as DataRowView)?[ColumnName(e.Column)] as string;
    }

    private static string ColumnName(DataGridColumn c) => c.Header is TextBlock tb ? tb.Text : c.Header as string;

    /// <summary>Checks the new value against the field (range, width, compressed size) by writing it into the working copy.</summary>
    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.EditingElement is not TextBox box || e.Row.Item is not DataRowView view) return;
        var field = _table.Fields.FirstOrDefault(f => f.Name == ColumnName(e.Column));
        if (field == null) return;
        int record = (int)view["#"];
        string text = box.Text.Trim();
        if (text == (_editOld ?? "")) return;
        try
        {
            object value = field.Type switch
            {
                T3FieldType.Integer => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                T3FieldType.Float => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                _ => text,
            };
            _db.Set(_table, record, field, value);
            _edits[(_table.Name, record, field.Name)] = value;
            box.Text = Text(_db.Get(_table, record, field));
            CountEdits();
            StatusChanged?.Invoke($"{_table.Name} row {record}: {field.Name} {_editOld} → {box.Text} (not saved yet)");
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or NotSupportedException)
        {
            e.Cancel = true;
            box.Text = _editOld;
            StatusChanged?.Invoke($"Can't set {field.Name}: {(ex is FormatException ? Describe(field) : ex.Message)}");
        }
    }

    private void CountEdits()
    {
        foreach (var r in _tableRows) r.Edits = _edits.Keys.Count(k => k.Table == r.Name);
        SetButtons();
    }

    private void SetButtons()
    {
        SaveButton.IsEnabled = DiscardButton.IsEnabled = !_busy && _edits.Count > 0;
        ReloadButton.IsEnabled = DbPicker.IsEnabled = !_busy;
        SaveButton.ToolTip = _edits.Count > 0 ? $"Write {_edits.Count} edited value(s) into {_dbName}" : null;
    }

    private async void OnDbPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (_edits.Count > 0 && !Confirm($"Drop {_edits.Count} unsaved edit(s) to {_dbName}?"))
        {
            _suppress = true;
            DbPicker.SelectedItem = _dbName;
            _suppress = false;
            return;
        }
        await LoadAsync();
    }

    private async void OnReload(object sender, RoutedEventArgs e)
    {
        if (_edits.Count > 0 && !Confirm($"Drop {_edits.Count} unsaved edit(s)?")) return;
        await LoadAsync();
    }

    private async void OnDiscard(object sender, RoutedEventArgs e) => await LoadAsync();

    private bool Confirm(string text) => App.Args.ContainsKey("shot") ||
        MessageBox.Show(Window.GetWindow(this), text, "HMS", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveAsync();

    /// <summary>Writes the edits into the game (into a fresh read of the database), then reads it back.</summary>
    public async Task SaveAsync()
    {
        if (_edits.Count == 0 || _busy) return;
        Rows.CommitEdit(DataGridEditingUnit.Row, true);
        _busy = true;
        SetButtons();
        var edits = _edits.ToList();
        StatusChanged?.Invoke($"Writing {edits.Count} value(s) into {_dbName}…");
        try
        {
            await _mods.SaveDbAsync(_dbName, db =>
            {
                foreach (var ((table, record, field), value) in edits)
                {
                    var t = db.Table(table);
                    db.Set(t, record, t.Fields.First(f => f.Name == field), value);
                }
                var parts = edits.Select(x => $"{x.Key.Table}[{x.Key.Record}].{x.Key.Field} = {Text(x.Value)}").ToList();
                return "Database: " + (parts.Count <= 6 ? string.Join(", ", parts) : string.Join(", ", parts.Take(6)) + $" and {parts.Count - 6} more");
            });
            _edits.Clear();
            StatusChanged?.Invoke($"{edits.Count} value(s) written into the game ({_dbName}). The Mods page can restore the original.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Can't save: " + ex.Message);
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't save to the game", MessageBoxButton.OK, MessageBoxImage.Warning);
            _busy = false;
            SetButtons();
            return;
        }
        _busy = false;
        await LoadAsync();
    }
}
