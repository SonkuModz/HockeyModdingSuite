using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>Which game a folder holds, and so which of the kit's readers, files and emulator go with it.</summary>
public enum GameKind { Unknown, Nhl27, Nhl24 }

/// <summary>
/// A game folder's identity, from its sce_sys: param.json (PS5) or param.sfo (PS4). NHL 27 is PPSA34063 on PS5 (run on
/// KytyPS5); NHL 24 is CUSA37933 (US) or CUSA37934 (EU) on PS4 (run on nhl24pc). Each game keeps its own index cache
/// and mod list (<see cref="KitPaths"/>).
/// </summary>
public sealed record GameProfile(GameKind Kind, string Title, string TitleId, string ContentId, string Version, string Platform)
{
    /// <summary>Short name for menus: "NHL 27", "NHL 24", or the title.</summary>
    public string Name => Kind switch { GameKind.Nhl27 => "NHL 27", GameKind.Nhl24 => "NHL 24", _ => Title ?? "Unknown game" };

    /// <summary>Folder-safe key for the game's cache and mod list.</summary>
    public string Key => Kind switch { GameKind.Nhl27 => "nhl27", GameKind.Nhl24 => "nhl24", _ => (TitleId ?? "game").ToLowerInvariant() };

    public bool IsPs4 => Platform == "PS4";

    /// <summary>"NHL 27  ·  PS5  ·  v01.003.001"</summary>
    public string Badge => $"{Name}  ·  {Platform}" + (string.IsNullOrEmpty(Version) ? "" : $"  ·  v{Version}");

    public static readonly GameProfile None = new(GameKind.Unknown, null, null, null, null, null);

    private static readonly string[] Nhl24Ids = { "CUSA37933", "CUSA37934" };

    /// <summary>The game in <paramref name="dir"/>; <see cref="None"/> when there's no game there.</summary>
    public static GameProfile Detect(string dir)
    {
        if (!KitPaths.IsGame(dir)) return None;
        string sys = Path.Combine(dir, "sce_sys");
        try
        {
            string json = Path.Combine(sys, "param.json");
            if (File.Exists(json))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                var r = doc.RootElement;
                string S(string k) => r.TryGetProperty(k, out var v) ? v.ToString() : null;
                string title = r.TryGetProperty("localizedParameters", out var lp) && lp.TryGetProperty("en-US", out var en) && en.TryGetProperty("titleName", out var tn)
                    ? tn.GetString() : null;
                string id = S("titleId");
                return new GameProfile(Guess(id, title, "PS5"), title, id, S("contentId"), S("contentVersion"), "PS5");
            }
            string sfo = Path.Combine(sys, "param.sfo");
            if (File.Exists(sfo))
            {
                var p = ReadSfo(File.ReadAllBytes(sfo));
                string id = p.GetValueOrDefault("TITLE_ID"), title = p.GetValueOrDefault("TITLE");
                return new GameProfile(Guess(id, title, "PS4"), title, id, p.GetValueOrDefault("CONTENT_ID"), p.GetValueOrDefault("APP_VER"), "PS4");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException) { }
        // a game folder without sce_sys: the data's platform folder tells PS5 (Ps5) from PS4 (Gen4b)
        bool ps5 = Directory.Exists(Path.Combine(dir, "Data", "Ps5"));
        return new GameProfile(ps5 ? GameKind.Nhl27 : Directory.Exists(Path.Combine(dir, "Data", "Gen4b")) ? GameKind.Nhl24 : GameKind.Unknown,
            null, null, null, null, ps5 ? "PS5" : "PS4");
    }

    private static GameKind Guess(string titleId, string title, string platform)
    {
        if (titleId == "PPSA34063") return GameKind.Nhl27;
        if (Nhl24Ids.Contains(titleId)) return GameKind.Nhl24;
        // another region's id: go by the title
        string t = (title ?? "").Replace("®", "");
        return t.Contains("27") && platform == "PS5" ? GameKind.Nhl27 : t.Contains("24") && platform == "PS4" ? GameKind.Nhl24 : GameKind.Unknown;
    }

    /// <summary>
    /// A PS4 param.sfo's values. Layout (little endian): "\0PSF", u32 version, u32 key table offset, u32 data table offset,
    /// u32 count; per entry u16 key offset, u16 format (0x0204 UTF-8, 0x0404 u32), u32 length, u32 max length, u32 data offset.
    /// </summary>
    public static Dictionary<string, string> ReadSfo(byte[] d)
    {
        if (d.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(d) != 0x46535000) throw new InvalidDataException("Not a param.sfo");
        int keys = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(8)), data = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(12));
        int n = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(16));
        var map = new Dictionary<string, string>();
        for (int i = 0; i < n; i++)
        {
            int e = 20 + i * 16;
            int ko = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(e)), fmt = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(e + 2));
            int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(e + 4)), off = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(e + 12));
            int k = keys + ko, end = Array.IndexOf(d, (byte)0, k);
            string key = Encoding.ASCII.GetString(d, k, end - k);
            var v = d.AsSpan(data + off, len);
            map[key] = fmt == 0x0404 ? BinaryPrimitives.ReadUInt32LittleEndian(v).ToString() : Encoding.UTF8.GetString(v).TrimEnd('\0');
        }
        return map;
    }
}

/// <summary>The emulators that read the game's files while they run: nothing may be written then.</summary>
public static class Emulators
{
    private static readonly (string Process, string Name)[] Known = { ("kyty_emulator", "KytyPS5"), ("nhl24_new", "NHL24 PC"), ("nhl24", "NHL24 PC"), ("shadPS4", "shadPS4") };

    /// <summary>The name of a running emulator, or null.</summary>
    public static string Running()
    {
        foreach (var (process, name) in Known)
        {
            var found = System.Diagnostics.Process.GetProcessesByName(process);
            bool any = found.Length > 0;
            foreach (var p in found) p.Dispose();
            if (any) return name;
        }
        return null;
    }
}
