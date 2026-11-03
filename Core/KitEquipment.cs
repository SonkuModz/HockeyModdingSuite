namespace NHL27Kit.Core;

/// <summary>
/// A kit's helmet and gloves as the game colours them. The colours are in nhl_game.db's stockteamequipmenttable: rows of
/// 15 zone colours per team, equipment kind, era and jersey variant (kind 7 is the helmet, kind 4 the gloves; checked
/// against Chicago's black helmet with its white maker's logo and red-trimmed gloves). The models' recolour masks say
/// which zone each pixel takes: the helmet's recolor1 red/green/blue are zones 1-3 (shell, trim, logo), the gloves'
/// recolor1-4 red/green/blue are zones 1-12.
/// </summary>
public static class KitEquipment
{
    public const string Database = "gamemodes/database/t3db/nhl_game.db";
    public const int HelmetKind = 7, GloveKind = 4;

    private const string Player = "content/character/skater/player/";
    public const string HelmetMesh = Player + "helmet/ccm/tacks/components/player_helmet_ccm_tacks_910_mesh";
    private const string HelmetTextures = Player + "helmet/ccm/tacks/textures/player_helmet_ccm_tacks_910_";
    private const string Glove = Player + "glove/ccm/tacks/xf/";

    public static string GloveMesh(string side) => $"{Glove}components/player_glove_ccm_tacks_xf_{side}_mesh";

    /// <summary>The default head the 3D view dresses: one of the game's generic faces, with brown eyes.</summary>
    private const string Head = "content/character/skater/shared/head/generic/male_60169/";
    public const string HeadMesh = Head + "components/head_male_60169_mesh";
    public const string HeadColor = Head + "textures/head_male_60169_color";
    public const string HeadNormal = Head + "textures/head_male_60169_normal";
    public const string EyeColor = "content/character/common/textures/eye_common_brown_color";

    /// <summary>The stick the idle holds: shaft and blade meshes (skinned to the skeleton's Stick bones).</summary>
    public const string StickColor = Player + "stick/bauer/adv/one/textures/player_stick_bauer_adv_one_base_color";
    public static string[] StickMeshes(string hand) =>
        new[] { "shaft", "blade" }.Select(p => $"{Player}stick/common/components/player_stick_{hand}_{p}_mesh").ToArray();

    /// <summary>The kit's helmet and glove zone colours (0xRRGGBB, 15 each), or null when its team isn't in the database.</summary>
    public static (int[] Helmet, int[] Gloves, string Team)? Colors(T3Db db, Kit kit) =>
        Gear(db, kit) is { } g ? (g.Helmet?.Zones, g.Gloves?.Zones, g.Team) : null;

    /// <summary>One piece's colours and the stockteamequipmenttable row they come from (era/variant -1: shared by more kits).</summary>
    public sealed record GearRow(int Row, int Era, int Variant, int[] Zones)
    {
        /// <summary>Which of the team's kits the row colours, in words.</summary>
        public string Scope(Kit kit) => Variant != -1 ? $"only this kit ({kit.Brand} {kit.Name})"
            : Era != -1 ? $"every {kit.Brand} kit of the team without its own row" : "every kit of the team without its own row";
    }

    /// <summary>The kit's helmet and glove rows, or null when its team isn't in the database.</summary>
    public static (GearRow Helmet, GearRow Gloves, string Team, int TeamIndex)? Gear(T3Db db, Kit kit)
    {
        int team = TeamIndex(db, kit);
        if (team < 0) return null;
        var table = db.Table("stockteamequipmenttable");
        int era = kit.Brand switch { "nineties" => 0, "rbk" => 1, "adidas" => 2, _ => -1 };
        int variant = Variant(kit.Name);
        GearRow Pick(int kind)
        {
            // the most specific row: this era and jersey variant, then the era, then the team's default
            (int Score, GearRow Row) best = (-1, null);
            var f = table.Fields.ToDictionary(x => x.Name);
            for (int r = 0; r < table.WrittenRecords; r++)
            {
                if (db.IsInvalid(table, r)) continue;
                int I(string n) => (int)db.Get(table, r, f[n]);
                if (I("team") != team || I("equipment") != kind || I("brand") != -1) continue;
                int e = I("era"), v = I("variant");
                if ((e != -1 && e != era) || (v != -1 && v != variant)) continue;
                int score = (e == era && era != -1 ? 2 : 0) + (v == variant && variant != -1 ? 4 : 0);
                if (score > best.Score) best = (score, new GearRow(r, e, v, Enumerable.Range(1, 15).Select(z => I("zone" + z)).ToArray()));
            }
            return best.Row;
        }
        var helmet = Pick(HelmetKind);
        var gloves = Pick(GloveKind);
        if (helmet == null && gloves == null) return null;
        var teams = db.Table("exhibitionteams");
        string name = Convert.ToString(db.Get(teams, team, teams.Fields.First(x => x.Name == "teamname")));
        return (helmet, gloves, name, team);
    }

    /// <summary>
    /// The zones (1-based) a piece's recolour masks actually use, with the share of the texture each covers; "helmet" or
    /// "gloves" (the right glove).
    /// </summary>
    public static List<(int Zone, double Share)> UsedZones(AssetIndex index, string piece)
    {
        var masks = piece == "helmet" ? new[] { HelmetTextures + "recolor1" }
            : Enumerable.Range(1, 4).Select(i => $"{Glove}textures/player_glove_ccm_tacks_xf_right_recolor{i}").ToArray();
        var list = new List<(int, double)>();
        for (int m = 0; m < masks.Length; m++)
        {
            Texture t;
            try { t = Load(index, masks[m], 128); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { continue; }
            int n = t.Width * t.Height;
            for (int c = 0; c < 3; c++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++) sum += t.Rgba[i * 4 + c];
                double share = sum / 255.0 / n;
                if (share > 0.002) list.Add((m * 3 + c + 1, share));
            }
        }
        return list;
    }

    /// <summary>home 0, away 1, then the number the kit folder spells out (two … twentythree); -1 when it isn't one.</summary>
    public static int Variant(string kit)
    {
        string[] ones = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
            "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        if (kit == "home") return 0;
        if (kit == "away") return 1;
        int i = Array.IndexOf(ones, kit);
        if (i >= 0) return i;
        if (kit.StartsWith("twenty")) { int j = Array.IndexOf(ones, kit[6..]); return kit.Length == 6 ? 20 : j is > 0 and < 10 ? 20 + j : -1; }
        if (kit.StartsWith("thirty")) { int j = Array.IndexOf(ones, kit[6..]); return kit.Length == 6 ? 30 : j is > 0 and < 10 ? 30 + j : -1; }
        return -1;
    }

    /// <summary>
    /// The exhibitionteams row of a kit's team folder (toronto, nyr, torontomarlies…): the team in the kit's league whose
    /// city, abbreviation, team name or nickname best matches the folder name. -1 when nothing matches.
    /// </summary>
    public static int TeamIndex(T3Db db, Kit kit)
    {
        var t = db.Table("exhibitionteams");
        var f = t.Fields.ToDictionary(x => x.Name);
        string folder = Norm(kit.Team);
        int league = Array.IndexOf(new[] { "nhl", "ahl" }, kit.League);
        (int Score, int Row) best = (0, -1);
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            if (league >= 0 && (int)db.Get(t, r, f["league"]) != league) continue;
            string city = Norm((string)db.Get(t, r, f["cityname"])), name = Norm((string)db.Get(t, r, f["teamname"]));
            string nick = Norm((string)db.Get(t, r, f["nickname"])), abbr = Norm((string)db.Get(t, r, f["artabbr"]));
            int score = folder == city || folder == abbr ? 5 : folder == name || folder == nick ? 5
                : name.Length > 0 && (name.Contains(folder) || folder.Contains(name)) ? 3
                : city.Length > 2 && folder.StartsWith(city) ? 2 : 0;
            // a folder that names a team ("torontomarlies") beats the plain city it starts with
            if (score == 2 && name.Length > 0 && folder.Length > city.Length && name.Contains(folder[city.Length..])) score = 4;
            if (score > best.Score) best = (score, r);
        }
        return best.Row;
    }

    private static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>The helmet's colour texture with its zones tinted: (rgba, w, h).</summary>
    public static (byte[] Rgba, int Width, int Height) Helmet(AssetIndex index, int[] zones, int size) =>
        Tint(index, HelmetTextures + "color", new[] { HelmetTextures + "recolor1" }, zones, size);

    /// <summary>
    /// The helmet as the game dresses it for a kit: the league's helmet art (…_910_nhl_color, …_ahl_color, else the
    /// plain one) tinted in the zone colours, then the decals the helmet maps with its second UV set:
    /// - the kit's helmetlogo_0 (the sponsor, 2:1) fills the side band, UV2 0..1 × 0..1/3, on both sides;
    /// - helmetlogo_2 (a square team logo on some kits) is put in the middle of that band (not checked in game);
    /// - the number, from the kit's font (the arm digits), in the front and back box, UV2 0.10..0.40 × 0.72..0.95,
    ///   scaled by the stamp's "helmet" hotspot (fsx, 0.54 on NHL kits).
    /// </summary>
    public static (byte[] Rgba, int Width, int Height) HelmetDressed(AssetIndex index, Kit kit, int[] zones, int size, string number)
    {
        string league = $"{HelmetTextures}{kit?.League}_color";
        string art = kit != null && index.GetEbx(league) != null ? league : HelmetTextures + "color";
        var h = Tint(index, art, new[] { HelmetTextures + "recolor1" }, zones, size);
        if (kit == null) return h;
        // the league's decals (the NHL shield on the back) are in the art, kept from the tint where its decal mask is set
        string maskName = art.Replace("_color", "_decalmask");
        if (art != HelmetTextures + "color" && index.GetEbx(maskName) != null)
        {
            try
            {
                var plain = Load(index, art, size);
                var mask = Load(index, maskName, size);
                for (int i = 0; i < h.Width * h.Height; i++)
                {
                    int x = i % h.Width * mask.Width / h.Width, y = i / h.Width * mask.Height / h.Height;
                    int px = i % h.Width * plain.Width / h.Width, py = i / h.Width * plain.Height / h.Height;
                    double a = mask.Rgba[(y * mask.Width + x) * 4] / 255.0;
                    if (a <= 0) continue;
                    for (int c = 0; c < 3; c++)
                        h.Rgba[i * 4 + c] = (byte)(plain.Rgba[(py * plain.Width + px) * 4 + c] * a + h.Rgba[i * 4 + c] * (1 - a));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
        }
        var map = HelmetMap(index, h.Width);
        if (map?.Uv2 == null || h.Width != h.Height) return h;
        foreach (var (layer, rect) in new[] { (0, (0.0, 0.0, 1.0, 1 / 3.0)), (2, (1 / 3.0, 0.0, 2 / 3.0, 1 / 3.0)) })
        {
            if (kit.Slot($"helmetlogo_{layer}_color") is not { } slot) continue;
            try
            {
                var logo = Load(index, slot.Name, size);
                map.Decal(h.Rgba, logo.Rgba, logo.Width, logo.Height, rect);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
        }
        if (!string.IsNullOrEmpty(number) && kit.Slot("font_color") is { } fontSlot)
        {
            try
            {
                var font = Load(index, fontSlot.Name, 2048);
                double fsx = 0.54;
                if (kit.Stamps.TryGetValue("jersey", out var stampName) && index.GetEbx(stampName) is { } stamp
                    && KitStamp.Parse(index.Read(stamp), "jersey").FirstOrDefault(x => x.Name == "helmet") is { } spot)
                {
                    if (!spot.Enabled) return h;
                    if (spot.Fsx > 0) fsx = spot.Fsx;
                }
                // the box is 0.30 × 0.23 of the second UV square; drawn on a 512-pixel square, so the digits keep their shape
                var box = new StampHotspot(0, "font", "helmet", true, 0, 0.30, 0.23, 0.5, 0.5) { Fsx = fsx / 0.54, Fsy = fsx / 0.54 };
                if (font.Width == font.Height && KitStamp.Text(box, number, font.Rgba, font.Width, 512, 512) is { } text)
                    map.Decal(h.Rgba, text.Rgba, text.Width, text.Height, (0.10, 0.72, 0.40, 0.95));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
        }
        return h;
    }

    private static readonly Dictionary<int, SurfaceMap> HelmetMaps = new();

    /// <summary>The helmet model's texels at one size (read once).</summary>
    private static SurfaceMap HelmetMap(AssetIndex index, int size)
    {
        lock (HelmetMaps)
        {
            if (HelmetMaps.TryGetValue(size, out var map)) return map;
            try { map = SurfaceMap.Build(Kits.LoadMesh(index, HelmetMesh).Lods[0], size); }
            catch (InvalidDataException) { map = null; }
            return HelmetMaps[size] = map;
        }
    }

    /// <summary>A glove's (left or right) colour texture with its zones tinted.</summary>
    public static (byte[] Rgba, int Width, int Height) Gloves(AssetIndex index, string side, int[] zones, int size)
    {
        string t = $"{Glove}textures/player_glove_ccm_tacks_xf_{side}_";
        return Tint(index, t + "base_color", Enumerable.Range(1, 4).Select(i => t + "recolor" + i).ToArray(), zones, size);
    }

    /// <summary>
    /// Each recolour mask's red, green and blue are the next three zones; a masked pixel takes its zone's colour (lit by
    /// the 3D view), the rest keeps the base texture.
    /// </summary>
    private static (byte[] Rgba, int Width, int Height) Tint(AssetIndex index, string baseName, string[] masks, int[] zones, int size)
    {
        var b = Load(index, baseName, size);
        var outp = (byte[])b.Rgba.Clone();
        int n = b.Width * b.Height;
        for (int m = 0; m < masks.Length; m++)
        {
            Texture mask;
            try { mask = Load(index, masks[m], size); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { continue; }
            if (mask.Rgba == null) continue;
            for (int c = 0; c < 3; c++)
            {
                int zone = m * 3 + c;
                if (zone >= zones.Length) break;
                int col = zones[zone];
                byte zr = (byte)(col >> 16), zg = (byte)(col >> 8), zb = (byte)col;
                for (int i = 0; i < n; i++)
                {
                    // the mask may be stored at another size than the base
                    int x = i % b.Width * mask.Width / b.Width, y = i / b.Width * mask.Height / b.Height;
                    double a = mask.Rgba[(y * mask.Width + x) * 4 + c] / 255.0;
                    if (a <= 0) continue;
                    int o = i * 4;
                    outp[o] = (byte)(zr * a + outp[o] * (1 - a));
                    outp[o + 1] = (byte)(zg * a + outp[o + 1] * (1 - a));
                    outp[o + 2] = (byte)(zb * a + outp[o + 2] * (1 - a));
                }
            }
        }
        for (int i = 3; i < outp.Length; i += 4) outp[i] = 255;
        return (outp, b.Width, b.Height);
    }

    private static Texture Load(AssetIndex index, string name, int size)
    {
        var t = Texture.Load(index, name, decode: false);
        t.DecodeMip(t.MipForSize(size));
        if (t.Rgba == null) throw new InvalidDataException($"{name} couldn't be decoded.");
        return t;
    }
}
