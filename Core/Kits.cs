namespace NHL27Kit.Core;

/// <summary>One texture of a kit: its slot (jersey_color, pant_normal, branding_jersey_0_color…) and asset name.</summary>
public sealed record KitTexture(string Slot, string Name)
{
    /// <summary>The garment the slot belongs to: jersey, pant, sock, font, helmetlogo or branding.</summary>
    public string Garment => Slot.StartsWith("branding_") ? "branding" : Slot.Split('_')[0];
    /// <summary>The map kind: color, normal, coeff or alpha.</summary>
    public string Map => Slot[(Slot.LastIndexOf('_') + 1)..];
}

/// <summary>A team's uniform: content/character/skater/league/[league]/[team]/[brand]/[kit]/ and its textures.</summary>
public sealed class Kit
{
    public string League { get; init; }
    public string Team { get; init; }
    public string Brand { get; init; }
    public string Name { get; init; }
    public string Folder { get; init; }
    public List<KitTexture> Textures { get; } = new();
    /// <summary>The stamp asset of each garment that has one (jersey, pant): where its branding logos go.</summary>
    public Dictionary<string, string> Stamps { get; } = new(StringComparer.OrdinalIgnoreCase);

    public KitTexture Slot(string slot) => Textures.FirstOrDefault(t => t.Slot == slot);

    /// <summary>The texture drawn on a garment in the 3D view: its graphics layer, else its base.</summary>
    public KitTexture Diffuse(string garment) => Slot($"{garment}_color") ?? Slot($"{garment}_base_color");

    /// <summary>True when the game builds this garment from its base plus stamped branding (what the 3D view then draws).</summary>
    public bool IsStamped(string garment) => Stamps.ContainsKey(garment) && Slot($"{garment}_base_color") != null
        && Textures.Any(t => t.Slot.StartsWith($"branding_{garment}_") && (t.Map == "color" || t.Slot == $"branding_{garment}_color_array"));

    public override string ToString() => $"{League}/{Team}/{Brand}/{Name}";
}

/// <summary>The lettering to preview on a jersey: the name on the back, the number (back, arms, front) and C or A.</summary>
public sealed record KitLettering(string Name, string Number, string Captain);

/// <summary>Every kit in the game, from the texture names (nothing else to read).</summary>
public static class Kits
{
    public const string Root = "content/character/skater/league/";

    public static List<Kit> Find(AssetIndex index)
    {
        var kits = new Dictionary<string, Kit>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, e) in index.Ebx)
        {
            if (e.Type is not ("TextureAsset" or "TextureArrayAsset" or "RawFileDataAsset") || !name.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) continue;
            var parts = name[Root.Length..].Split('/');
            if (parts.Length != 5) continue;
            string folder = Root + string.Join('/', parts.Take(4));
            if (!kits.TryGetValue(folder, out var kit))
                kits[folder] = kit = new Kit { League = parts[0], Team = parts[1], Brand = parts[2], Name = parts[3], Folder = folder };
            // jersey_adidas_toronto_home_color → jersey_color
            string file = parts[4], tag = $"_{kit.Brand}_{kit.Team}_{kit.Name}";
            string slot = file.Replace(tag, "", StringComparison.OrdinalIgnoreCase);
            if (e.Type == "RawFileDataAsset")
            {
                // stamp_jersey_adidas_toronto_home → jersey
                if (slot.StartsWith("stamp_")) kit.Stamps[slot["stamp_".Length..]] = name;
                continue;
            }
            kit.Textures.Add(new KitTexture(slot, name));
        }
        foreach (var k in kits.Values) k.Textures.Sort((a, b) => Order(a).CompareTo(Order(b)));
        return kits.Values.Where(k => k.Diffuse("jersey") != null).OrderBy(k => k.League).ThenBy(k => k.Team).ThenBy(k => KitOrder(k.Name)).ThenBy(k => k.Name).ToList();
    }

    private static string Order(KitTexture t) =>
        (t.Garment switch { "jersey" => "0", "pant" => "1", "sock" => "2", "font" => "3", "helmetlogo" => "4", _ => "5" })
        + (t.Map switch { "color" => "0", "normal" => "1", "coeff" => "2", _ => "3" }) + t.Slot;

    private static int KitOrder(string kit) => kit switch { "home" => 0, "away" => 1, "third" => 2, _ => 3 };

    /// <summary>The meshes a kit is shown on: the brand's jersey (adidas when the brand has none), common pants and socks, a helmet and gloves.</summary>
    public static List<(string Garment, string Mesh)> Meshes(AssetIndex index, Kit kit)
    {
        // NHL 24 names each LOD of a jersey (..._large_lod0_mesh); NHL 27 has one mesh with every LOD
        string Jersey(string brand)
        {
            string name = $"content/character/skater/player/jersey/{brand}/components/player_jersey_{brand}_notuck_large";
            return index.GetRes(name + "_mesh") != null ? name + "_mesh" : index.GetRes(name + "_lod0_mesh") != null ? name + "_lod0_mesh" : null;
        }
        var list = new List<(string, string)>
        {
            ("jersey", Jersey(kit.Brand) ?? Jersey("adidas")),
            ("pant", "content/character/skater/player/pant/common/components/player_pant_common_looselong_mesh"),
            ("sock", "content/character/skater/player/sock/common/components/player_sock_common_notuck_mesh"),
        };
        // the helmet and gloves, coloured from the database (KitEquipment)
        list.Add(("helmet", KitEquipment.HelmetMesh));
        list.Add(("glove_left", KitEquipment.GloveMesh("left")));
        list.Add(("glove_right", KitEquipment.GloveMesh("right")));
        return list.Where(m => m.Item2 != null && index.GetRes(m.Item2) != null).ToList();
    }

    /// <summary>
    /// A garment as the game draws it: its base with each enabled brand hotspot of its stamp stamped on, read from the
    /// game now. Logos come from the branding array (the copy the game draws, and the one a replacement writes), else
    /// the numbered standalone texture. Null when the garment isn't stamped.
    /// </summary>
    public static (byte[] Rgba, int Width, int Height)? Compose(AssetIndex index, Kit kit, string garment, int size, KitLettering text = null)
    {
        if (!kit.IsStamped(garment)) return null;
        var spots = KitStamp.Parse(index.Read(index.GetEbx(kit.Stamps[garment])), garment);
        var b = Load(index, kit.Slot($"{garment}_base_color").Name, size, 0);
        var array = kit.Slot($"branding_{garment}_color_array");
        var logos = new List<(StampHotspot, byte[], int, int)>();
        foreach (var (spot, layer) in KitStamp.Brands(spots))
        {
            if (!spot.Enabled) continue;
            // a logo needs about its share of the texture's pixels, at least 128
            int want = Math.Max(128, (int)(Math.Max(spot.Sx, spot.Sy) * b.Width * 1.5));
            Texture t = null;
            try { if (array != null) t = Load(index, array.Name, want, layer); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { }
            if (t?.Rgba == null && kit.Slot($"branding_{garment}_{layer}_color") is { } one) t = Load(index, one.Name, want, 0);
            // pant stamps turn the other way from jersey stamps: 0 is upright on the leg (the Ottawa home pants' r=0 logo,
            // most NHL pants), where a jersey's upright crest is 180. The baked pant textures follow the jersey rule.
            if (t?.Rgba != null) logos.Add((garment == "pant" ? spot with { R = spot.R + 180 } : spot, t.Rgba, t.Width, t.Height));
        }
        // the name and numbers, from the kit's font texture, at its font hotspots
        if (text != null && kit.Slot($"font_color") is { } fontSlot)
        {
            var font = Load(index, fontSlot.Name, 2048, 0);
            if (font.Rgba != null && font.Width == font.Height) logos.AddRange(Lettering(spots, text, font.Rgba, font.Width, b.Width, b.Height));
        }
        return (KitStamp.Compose(b.Rgba, b.Width, b.Height, logos), b.Width, b.Height);
    }

    /// <summary>The name, numbers and captain's letter drawn from a kit's font texture, ready for <see cref="KitStamp.Compose"/>.</summary>
    public static List<(StampHotspot, byte[], int, int)> Lettering(IEnumerable<StampHotspot> spots, KitLettering text, byte[] font, int fontSize, int width, int height)
    {
        var logos = new List<(StampHotspot, byte[], int, int)>();
        foreach (var spot in spots.Where(s => s.Type == "font" && s.Enabled))
        {
            string value = spot.Name switch
            {
                "name" => text.Name,
                "number_back" or "number_front" or "number_arm_left" or "number_arm_right" => text.Number,
                "captain" => text.Captain,
                _ => null,
            };
            if (KitStamp.Text(spot, value, font, fontSize, width, height) is { } img)
                // the chest faces the other way from the back in the texture, and every kit's chest spots say
                // r=0 like the back's, so they're turned round to read upright
                logos.Add((spot.Name is "captain" or "number_front" ? spot with { R = spot.R + 180 } : spot, img.Rgba, img.Width, img.Height));
        }
        return logos;
    }

    public static Texture Load(AssetIndex index, string name, int size, int layer)
    {
        var t = Texture.Load(index, name, decode: false);
        t.Layer = layer;
        t.DecodeMip(t.MipForSize(size));
        return t;
    }

    public static MeshSet LoadMesh(AssetIndex index, string name)
    {
        var res = index.GetRes(name) ?? throw new InvalidDataException($"No mesh {name}.");
        return new MeshSet(index.Read(res), id => index.GetChunk(id) is { } c ? index.Read(c) : null, ps4: !index.Layout.IsPs5, meta: res.ResMeta);
    }
}
