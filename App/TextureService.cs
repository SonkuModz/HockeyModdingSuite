using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NHL27Kit.Core;

namespace NHL27Kit.App;

public enum Channel { Rgba, Rgb, R, G, B, A }

/// <summary>Decodes textures off the UI thread: full textures on demand, and small thumbnails with a bounded cache.</summary>
public sealed class TextureService
{
    private readonly AssetIndex _index;
    private readonly SemaphoreSlim _gate = new(Math.Max(2, Environment.ProcessorCount / 2));
    private readonly ConcurrentDictionary<string, BitmapSource> _thumbs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _thumbOrder = new();
    private const int ThumbCap = 600;

    public TextureService(AssetIndex index) => _index = index;

    public AssetIndex Index => _index;

    /// <summary>Forgets every thumbnail (after the game's data changed).</summary>
    public void ClearThumbs() => _thumbs.Clear();

    /// <summary>Loads a texture and decodes <paramref name="mip"/> (-1: the largest it holds).</summary>
    public async Task<Texture> LoadAsync(string name, int mip = -1, CancellationToken ct = default, int layer = 0)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var t = Texture.Load(_index, name, decode: false);
                t.Layer = layer;
                t.DecodeMip(mip < 0 ? t.FirstResidentMip : mip);
                return t;
            }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Loads a texture from the game now (never cached) and decodes the smallest mip at least <paramref name="size"/> pixels.</summary>
    public async Task<Texture> LoadSizedAsync(string name, int size)
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var t = Texture.Load(_index, name, decode: false);
                t.DecodeMip(t.MipForSize(size));
                return t;
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>A thumbnail at least <paramref name="size"/> pixels on its longer side (decoded from a small mip), or null.</summary>
    public async Task<BitmapSource> ThumbnailAsync(string name, int size = 256)
    {
        if (_thumbs.TryGetValue(name, out var cached))
            return cached;
        await _gate.WaitAsync();
        try
        {
            var bmp = await Task.Run(() =>
            {
                var t = Texture.Load(_index, name, decode: false);
                t.DecodeMip(t.MipForSize(size));
                // data maps (normal, coeff) keep values in alpha that are not transparency: show their colour
                return t.Rgba == null ? null : ToBitmap(t.Rgba, t.Width, t.Height, Role(name) == "Color" ? Channel.Rgba : Channel.Rgb);
            });
            if (bmp != null)
            {
                _thumbs[name] = bmp;
                _thumbOrder.Enqueue(name);
                while (_thumbs.Count > ThumbCap && _thumbOrder.TryDequeue(out var old))
                    _thumbs.TryRemove(old, out _);
            }
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>RGBA pixels as a frozen BGRA bitmap, showing one channel (as grey) or all of them.</summary>
    public static BitmapSource ToBitmap(byte[] rgba, int width, int height, Channel channel)
    {
        var bgra = new byte[rgba.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            byte r = rgba[i], g = rgba[i + 1], b = rgba[i + 2], a = rgba[i + 3];
            switch (channel)
            {
                case Channel.Rgba: bgra[i] = b; bgra[i + 1] = g; bgra[i + 2] = r; bgra[i + 3] = a; break;
                case Channel.Rgb: bgra[i] = b; bgra[i + 1] = g; bgra[i + 2] = r; bgra[i + 3] = 255; break;
                default:
                    byte v = channel switch { Channel.R => r, Channel.G => g, Channel.B => b, _ => a };
                    bgra[i] = bgra[i + 1] = bgra[i + 2] = v;
                    bgra[i + 3] = 255;
                    break;
            }
        }
        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>The usual role of a texture, from its name's suffix.</summary>
    public static string Role(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.EndsWith("_color") || n.EndsWith("_colour") || n.EndsWith("_albedo") || n.EndsWith("_diffuse") || n.Contains("_color_")) return "Color";
        if (n.EndsWith("_normal") || n.Contains("_normal_") || n.EndsWith("_nrm")) return "Normal";
        if (n.EndsWith("_coeff") || n.Contains("_coeff_")) return "Coeff";
        return "Other";
    }
}

/// <summary>One texture in the gallery; its thumbnail loads the first time a row shows it.</summary>
public sealed class TexTile : Observable
{
    private BitmapSource _thumb;
    private bool _requested, _selected, _failed;

    public TexTile(string name, TextureService service)
    {
        Name = name;
        Service = service;
        int slash = name.LastIndexOf('/');
        Leaf = slash < 0 ? name : name[(slash + 1)..];
        Folder = slash < 0 ? "" : name[..slash];
        Role = TextureService.Role(name);
    }

    public string Name { get; }
    public string Leaf { get; }
    public string Folder { get; }
    public string Role { get; }
    public TextureService Service { get; }
    public Brush RoleBrush => (Brush)Application.Current.FindResource(Role switch { "Color" => "Accent", "Normal" => "Violet", "Coeff" => "Patch", _ => "Muted" });

    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public bool Failed { get => _failed; private set => Set(ref _failed, value); }
    public bool Loading => _thumb == null && !_failed;

    /// <summary>Drops the thumbnail so the next look at the tile reads the texture from the game again.</summary>
    public void Reload()
    {
        _thumb = null;
        _requested = false;
        _failed = false;
        Raise(nameof(Thumb));
        Raise(nameof(Loading));
    }

    public BitmapSource Thumb
    {
        get
        {
            if (!_requested)
            {
                _requested = true;
                _ = LoadThumb();
            }
            return _thumb;
        }
    }

    private async Task LoadThumb()
    {
        var bmp = await Service.ThumbnailAsync(Name);
        _thumb = bmp;
        Failed = bmp == null;
        Raise(nameof(Thumb));
        Raise(nameof(Loading));
    }
}

public sealed class TexRow
{
    public List<TexTile> Tiles { get; init; }
}
