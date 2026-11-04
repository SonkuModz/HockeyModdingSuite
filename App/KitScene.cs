using System.IO;
using System.Windows.Media;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// A kit dressed on the game's models, read from the game now: the Uniforms page and the Jersey studio draw with it.
/// Stamped garments (jersey, pants) are their base with the stamp's logos (and the jersey's lettering); the helmet and
/// gloves are tinted from the database's zone colours, the helmet with its decals (sponsor, league shield, number).
/// The player is finished with a default head (one of the game's generic faces) and a stick, which the idle holds.
/// </summary>
public static class KitScene
{
    public static bool IsGear(string garment) => garment is "helmet" or "glove_left" or "glove_right";

    /// <summary>Every part of the kit's meshes with its texture. <paramref name="gear"/> draws the helmet and gloves.</summary>
    public static async Task<List<(string Garment, MeshPart Part, ImageSource Tex)>> BuildAsync(AssetIndex index, TextureService textures, Kit kit,
        KitLettering text, Func<string, Task<ImageSource>> gear, int size, Func<bool> stale = null)
    {
        var meshes = await Task.Run(() => Kits.Meshes(index, kit).Select(m => (m.Garment, Mesh: Kits.LoadMesh(index, m.Mesh))).ToList());
        if (KitViewer.Rig == null)
        {
            try { KitViewer.Rig = await Task.Run(() => PlayerIdle.Load(index)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { /* drawn in the bind pose */ }
        }
        var parts = new List<(string Garment, MeshPart Part, ImageSource Tex)>();
        foreach (var (garment, mesh) in meshes)
        {
            if (stale?.Invoke() == true) return null;
            ImageSource tex = null;
            if (IsGear(garment)) tex = gear != null ? await gear(garment) : null;
            else
            {
                if (kit.IsStamped(garment))
                {
                    // as the game draws it: the base with the branding logos stamped on (so replaced logos show)
                    try
                    {
                        var lettering = garment == "jersey" ? text : null;
                        var c = await Task.Run(() => Kits.Compose(index, kit, garment, size, lettering));
                        if (c is { } img) tex = TextureService.ToBitmap(img.Rgba, img.Width, img.Height, Channel.Rgb);
                    }
                    catch { /* falls back to the baked texture */ }
                }
                if (tex == null && kit.Diffuse(garment) is { } diffuse)
                {
                    try
                    {
                        var t = await textures.LoadSizedAsync(diffuse.Name, size);
                        if (t.Rgba != null) tex = TextureService.ToBitmap(t.Rgba, t.Width, t.Height, Channel.Rgb);
                    }
                    catch { /* drawn grey */ }
                }
            }
            var normal = IsGear(garment) ? null : await NormalAsync(textures, NormalSlot(kit, garment), size);
            foreach (var part in mesh.Lods[0].Where(p => p.Uvs != null))
            {
                if (normal is { } n) KitViewer.SetNormalMap(part, n.Rgba, n.Width, n.Height);
                parts.Add((garment, part, tex));
            }
        }
        if (stale?.Invoke() == true) return null;
        parts.AddRange(await ExtrasAsync(index, textures, size));
        return parts;
    }

    /// <summary>
    /// The normal map drawn into a garment's lit texture. A stamped garment takes its base map: the full one has the
    /// kit's own logos embossed, which would show through a replaced logo.
    /// </summary>
    private static string NormalSlot(Kit kit, string garment) =>
        (kit.IsStamped(garment) ? kit.Slot($"{garment}_base_normal") : kit.Slot($"{garment}_normal") ?? kit.Slot($"{garment}_base_normal"))?.Name;

    private static async Task<(byte[] Rgba, int Width, int Height)?> NormalAsync(TextureService textures, string name, int size)
    {
        if (name == null) return null;
        try
        {
            var t = await textures.LoadSizedAsync(name, size);
            return t.Rgba != null ? (t.Rgba, t.Width, t.Height) : null;
        }
        catch { return null; }
    }

    private static readonly (string Garment, string Mesh, Dictionary<string, string> Textures)[] Extras =
    {
        ("head", KitEquipment.HeadMesh, new() { ["mat_head"] = KitEquipment.HeadColor, ["mat_eyes"] = KitEquipment.EyeColor }),
        ("stick", KitEquipment.StickMeshes("left")[0], new() { [""] = KitEquipment.StickColor }),
        ("stick", KitEquipment.StickMeshes("left")[1], new() { [""] = KitEquipment.StickColor }),
    };

    /// <summary>The default head and the stick, each part with its texture (by material; "" for any).</summary>
    private static async Task<List<(string Garment, MeshPart Part, ImageSource Tex)>> ExtrasAsync(AssetIndex index, TextureService textures, int size)
    {
        var parts = new List<(string, MeshPart, ImageSource)>();
        var loaded = new Dictionary<string, ImageSource>();
        foreach (var (garment, name, map) in Extras)
        {
            MeshSet mesh;
            try { mesh = await Task.Run(() => Kits.LoadMesh(index, name)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { continue; }
            foreach (var part in mesh.Lods[0].Where(p => p.Uvs != null))
            {
                if (!map.TryGetValue(part.Material, out var texName) && !map.TryGetValue("", out texName)) texName = null;
                if (texName != null && !loaded.ContainsKey(texName))
                {
                    try
                    {
                        var t = await textures.LoadSizedAsync(texName, Math.Min(size, 1024));
                        loaded[texName] = t.Rgba != null ? TextureService.ToBitmap(t.Rgba, t.Width, t.Height, Channel.Rgb) : null;
                    }
                    catch { loaded[texName] = null; }
                }
                if (part.Material == "mat_head" && await NormalAsync(textures, KitEquipment.HeadNormal, Math.Min(size, 1024)) is { } n)
                    KitViewer.SetNormalMap(part, n.Rgba, n.Width, n.Height);
                parts.Add((garment, part, texName != null ? loaded[texName] : null));
            }
        }
        return parts;
    }

    /// <summary>The helmet (dressed for the kit, with <paramref name="number"/>) or a glove in these zone colours; null without them.</summary>
    public static async Task<ImageSource> GearAsync(AssetIndex index, Kit kit, string garment, int[] helmet, int[] gloves, string number)
    {
        var zones = garment == "helmet" ? helmet : gloves;
        if (zones == null) return null;
        try
        {
            var g = await Task.Run(() => garment == "helmet" ? KitEquipment.HelmetDressed(index, kit, zones, 1024, number)
                : KitEquipment.Gloves(index, garment == "glove_left" ? "left" : "right", zones, 1024));
            return TextureService.ToBitmap(g.Rgba, g.Width, g.Height, Channel.Rgb);
        }
        catch { return null; }
    }
}

/// <summary>Writes images over a kit's textures in the game (each its own mod entry, which Restore undoes).</summary>
public static class KitWriter
{
    /// <summary>The game stores an image once for several textures: asks whether to change them all (Restore puts them all back).</summary>
    public static bool AskToTakeShared(System.Windows.Window owner, string why) =>
        System.Windows.MessageBox.Show(owner, why + "\n\nReplace it anyway? Every texture using this stored image changes too. Restore on the Mods page puts them all back.",
            "This image is shared", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

    /// <summary>
    /// Encodes one image for a kit texture and writes it into the game; returns what was done. A numbered branding
    /// texture's layer of the branding array (the copy the game draws) is written first.
    /// </summary>
    public static async Task<string> WriteTextureAsync(ModService mods, Kit kit, KitTexture texture, string file, Action<string> status)
    {
        string label = $"{UniformNames.Pretty(kit.Team)} {kit.Name} {texture.Slot}";
        status?.Invoke($"Encoding {System.IO.Path.GetFileName(file)} for {label}…");
        var m = System.Text.RegularExpressions.Regex.Match(texture.Slot, @"^branding_(\w+?)_(\d+)_(color|normal|coeff)$");
        var array = m.Success ? kit.Slot($"branding_{m.Groups[1].Value}_{m.Groups[3].Value}_array") : null;
        if (array != null)
        {
            status?.Invoke($"Writing layer {m.Groups[2].Value} of the {m.Groups[1].Value} branding array…");
            await mods.ReplaceArrayLayerAsync(array.Name, file, int.Parse(m.Groups[2].Value), null);
        }
        try
        {
            await mods.ResetAsync("texture", texture.Name);
            var notes = new List<string>();
            TextureReplacementPlan plan;
            try { plan = await mods.PlanTextureAsync(texture.Name, file, n => notes.Add(n)); }
            catch (System.IO.InvalidDataException ex) when (array == null && mods.Index.Layout.IsPs5 && ex.Message.Contains("also used by"))
            {
                if (!AskToTakeShared(System.Windows.Application.Current.MainWindow, ex.Message))
                    return $"{label} not replaced: other textures use the same stored image.";
                notes.Clear();
                plan = await mods.PlanTextureAsync(texture.Name, file, n => notes.Add(n), takeShared: true);
            }
            var h = plan.Header;
            notes.AddRange(plan.Chunk.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
            var entry = mods.Queue("texture", texture.Name, file, $"{h.Format.Name} {h.Width}×{h.Height}, {TextureBuilder.StoredMips(h)} stored mips", plan.Chunk.Patches, notes);
            status?.Invoke($"Writing {label} into the game…");
            await mods.ApplyAsync(entry);
            return $"{label} replaced in the game" + (array != null ? $" (and layer {m.Groups[2].Value} of its array)" : "");
        }
        catch (System.IO.InvalidDataException ex) when (array != null && ex.Message.Contains("also used by"))
        {
            // the standalone copy is stored once for several kits: changing it would change them all
            return $"{label}: layer {m.Groups[2].Value} of the branding array replaced in the game. The standalone copy is shared with other kits, so it was left alone.";
        }
    }
}
