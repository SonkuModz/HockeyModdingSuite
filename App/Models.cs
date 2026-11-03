using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum AssetKind { Ebx, Res, Chunk }

/// <summary>A folder of asset paths: its own assets and subfolders, children sorted on first use.</summary>
public sealed class FolderNode : Observable
{
    private readonly Dictionary<string, FolderNode> _children = new(StringComparer.OrdinalIgnoreCase);
    private List<FolderNode> _sorted;
    private bool _expanded, _selected;

    public FolderNode(string name, string path, int depth)
    {
        Name = name;
        Path = path;
        Depth = depth;
    }

    public string Name { get; }
    /// <summary>The folder path ("" for the root), without a trailing slash.</summary>
    public string Path { get; }
    public int Depth { get; }
    public GridLength Indent => new(Math.Max(0, Depth - 1) * 14);
    public List<AssetRow> Items { get; } = new();
    /// <summary>Assets in this folder and every folder below it.</summary>
    public int Total { get; private set; }
    public string TotalText => Total.ToString("N0");
    /// <summary>Subfolders by name, with guid-named ones (unnamed assets) after the readable ones.</summary>
    public List<FolderNode> Children => _sorted ??= _children.Values
        .OrderBy(c => Guid.TryParse(c.Name, out _)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public FolderNode Child(string name)
    {
        if (!_children.TryGetValue(name, out var c))
            _children[name] = c = new FolderNode(name, Path.Length == 0 ? name : Path + "/" + name, Depth + 1);
        return c;
    }

    public int Count()
    {
        int n = Items.Count;
        foreach (var c in Children)
            n += c.Count();
        return Total = n;
    }

    public IEnumerable<AssetRow> AllItems()
    {
        foreach (var r in Items)
            yield return r;
        foreach (var c in Children)
            foreach (var r in c.AllItems())
                yield return r;
    }
}

/// <summary>One asset in the explorer list.</summary>
public sealed class AssetRow
{
    public AssetRow(AssetKind kind, AssetEntry entry, string name, string type)
    {
        Kind = kind;
        Entry = entry;
        Name = name;
        int slash = name.LastIndexOf('/');
        Leaf = slash < 0 ? name : name[(slash + 1)..];
        Folder = slash < 0 ? "" : name[..slash];
        Type = type;
        (Glyph, Tint) = Visuals.For(kind, type);
    }

    public AssetKind Kind { get; }
    public AssetEntry Entry { get; }
    public string Name { get; }
    public string Leaf { get; }
    public string Folder { get; }
    public string Type { get; }
    public string TypeText => string.IsNullOrEmpty(Type) ? "—" : Type;
    public string Glyph { get; }
    public Brush Tint { get; }
    public bool IsPatched => Entry.Ref.Patch;
    public string SizeText => Visuals.Size(Entry.OriginalSize);
}

/// <summary>A label/value line of the details pane.</summary>
public sealed record Property(string Label, string Value, bool Mono = false)
{
    public FontFamily Font => Mono ? (FontFamily)Application.Current.FindResource("MonoFont") : (FontFamily)Application.Current.FindResource("UiFont");
}

/// <summary>A choice of the type filter.</summary>
public sealed record TypeChoice(string Type, int Count)
{
    public string Label => Type == null ? "All types" : Type;
    public string CountText => Count.ToString("N0");
}

public static class Visuals
{
    private static Brush B(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static readonly Brush Blue = B("#4C9DFF"), Violet = B("#9B8CFF"), Teal = B("#34D3C0"), Amber = B("#F5A524"),
                                  Pink = B("#F472B6"), Green = B("#3CCB7F"), Grey = B("#7D8798"), Slate = B("#A3B1C6");

    /// <summary>A Segoe Fluent Icons glyph and colour for an asset.</summary>
    public static (string, Brush) For(AssetKind kind, string type)
    {
        if (kind == AssetKind.Chunk) return ("", Grey);
        string t = type ?? "";
        if (t.Contains("Texture", StringComparison.OrdinalIgnoreCase)) return ("", Blue);
        if (t.Contains("Mesh", StringComparison.OrdinalIgnoreCase)) return ("", Violet);
        if (t.Contains("Wave", StringComparison.OrdinalIgnoreCase) || t.Contains("Sound", StringComparison.OrdinalIgnoreCase) || t.Contains("Audio", StringComparison.OrdinalIgnoreCase)) return ("", Pink);
        if (t.Contains("Shader", StringComparison.OrdinalIgnoreCase) || t.Contains("Appearance", StringComparison.OrdinalIgnoreCase)) return ("", Teal);
        if (t.Contains("Blueprint", StringComparison.OrdinalIgnoreCase)) return ("", Slate);
        if (t.Contains("World", StringComparison.OrdinalIgnoreCase) || t.Contains("Layer", StringComparison.OrdinalIgnoreCase) || t.Contains("Level", StringComparison.OrdinalIgnoreCase)) return ("", Green);
        if (t.Contains("Ant", StringComparison.OrdinalIgnoreCase) || t.Contains("Track", StringComparison.OrdinalIgnoreCase) || t.Contains("Clip", StringComparison.OrdinalIgnoreCase)) return ("", Amber);
        if (kind == AssetKind.Res) return ("", Amber);
        return ("", Slate);
    }

    public static string Size(long n) => n switch
    {
        < 1024 => $"{n} B",
        < 1024 * 1024 => $"{n / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{n / (1024.0 * 1024):0.#} MB",
        _ => $"{n / (1024.0 * 1024 * 1024):0.##} GB",
    };

    public static string Hex(byte[] data, int max)
    {
        var sb = new System.Text.StringBuilder();
        int n = Math.Min(data.Length, max);
        for (int row = 0; row < n; row += 16)
        {
            sb.Append(row.ToString("X6")).Append("  ");
            for (int i = 0; i < 16; i++)
            {
                sb.Append(row + i < n ? data[row + i].ToString("X2") + " " : "   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(' ');
            for (int i = 0; i < 16 && row + i < n; i++)
            {
                byte c = data[row + i];
                sb.Append(c is >= 32 and < 127 ? (char)c : '·');
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }
}

public sealed class NonEmptyVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && s.Length > 0 || value is not (null or string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class BoolVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) ^ (parameter as string == "not") ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The window's settings, in %APPDATA%\NHL27Kit\settings.json.</summary>
public sealed class Settings
{
    public string GameDir { get; set; }
    /// <summary>Game folders picked before (NHL 27, NHL 24…), for the game switcher.</summary>
    public List<string> Games { get; set; } = new();
    /// <summary>kyty_emulator.exe to launch; empty means the launcher's choice or the newest build next to the kit.</summary>
    public string Emulator { get; set; }

    private static string File => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NHL27Kit", "settings.json");

    public static Settings Load()
    {
        try
        {
            return System.IO.File.Exists(File) ? JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(File)) ?? new Settings() : new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(File));
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // settings are a convenience; the kit works without them
        }
    }
}
