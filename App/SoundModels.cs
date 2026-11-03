using System.IO;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>One sound (NewWaveAsset) in the Sounds list.</summary>
public sealed class SoundRow
{
    public SoundRow(EbxEntry entry)
    {
        Entry = entry;
        Name = entry.Name;
        int slash = Name.LastIndexOf('/');
        Leaf = slash < 0 ? Name : Name[(slash + 1)..];
        Folder = slash < 0 ? "" : Name[..slash];
        Category = SoundService.Category(Name);
        (Glyph, Tint) = SoundService.Look(Category);
    }

    public EbxEntry Entry { get; }
    public string Name { get; }
    public string Leaf { get; }
    public string Folder { get; }
    public string Category { get; }
    public string Glyph { get; }
    public Brush Tint { get; }
    public bool IsPatched => Entry.Ref.Patch;
}

/// <summary>One audio chunk of a sound: a take, a layer or a language, depending on the sound.</summary>
public sealed record SoundVariation(int Number, SoundStream Stream)
{
    public SpsHeader Header => Stream.Header;
    public Guid Id => Stream.ChunkId;
    public int Size => Stream.Length;

    public string Label => Header == null
        ? $"#{Number}  ·  not audio"
        : $"#{Number}  ·  {Fmt.Seconds(Header.Seconds)}" + (Header.Channels > 1 ? $"  ·  {Header.Channels} ch" : "");
}

public static class Fmt
{
    public static string Seconds(double s) => s < 10 ? $"{s:0.00} s" : s < 100 ? $"{s:0.0} s" : $"{s:0} s";

    public static string Clock(double s)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, s));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}

/// <summary>Reads and decodes sounds off the UI thread.</summary>
public sealed class SoundService
{
    private readonly AssetIndex _index;
    private readonly SemaphoreSlim _gate = new(3);

    public SoundService(AssetIndex index) => _index = index;

    public AssetIndex Index => _index;

    /// <summary>The sound's top-level kind, from the folder under sound/.</summary>
    public static string Category(string name)
    {
        var parts = name.Split('/');
        return (parts.Length > 1 ? parts[1] : "") switch
        {
            "speech" => "Speech",
            "music_fb" => "Music",
            "sfx_fb" => "SFX",
            "crowds_fb" => "Crowds",
            _ => "Other",
        };
    }

    private static Brush B(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static readonly Brush Blue = B("#4C9DFF"), Pink = B("#F472B6"), Teal = B("#34D3C0"), Amber = B("#F5A524"), Grey = B("#7D8798");

    public static (string Glyph, Brush Tint) Look(string category) => category switch
    {
        "Speech" => ("", Blue),
        "Music" => ("", Pink),
        "SFX" => ("", Teal),
        "Crowds" => ("", Amber),
        _ => ("", Grey),
    };

    /// <summary>Each audio chunk the sound lists, with its header.</summary>
    public async Task<List<SoundVariation>> VariationsAsync(string name, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var wave = _index.GetEbx(name) ?? throw new InvalidDataException($"No sound named {name}.");
                var list = new List<SoundVariation>();
                foreach (var s in Audio.Variations(_index, wave))
                    list.Add(new SoundVariation(s.Number, s));
                return list;
            }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Sound> DecodeAsync(SoundVariation v, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Audio.Decode(_index, v.Stream), ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}
