using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>An arena level, content/worlds/stadiums/&lt;Venue&gt;_&lt;Id&gt; (toronto_27): Id is the arena id (exhibitionarena).</summary>
public sealed record ArenaLevel(string Level, string Venue, int Id)
{
    public string Path => $"{ArenaScene.Stadiums}{Level}";
    public string Structures => $"{ArenaScene.StructuresRoot}{Venue}/";
}

/// <summary>
/// A mesh placed in the level: a LinearTransform as 12 floats, rows right, up, forward and translation
/// (world = x·right + y·up + z·forward + translation).
/// </summary>
public sealed record ArenaPlacement(string Mesh, float[] Transform)
{
    public static readonly float[] Identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
}

/// <summary>One section of one mesh, with the colour texture its material uses (null: drawn flat).</summary>
public sealed record ArenaPiece(string Mesh, MeshPart Part, string Texture, ArenaPieceKind Kind);

public enum ArenaPieceKind { Solid, Glass, Ice, CenterLogo, Screen }

/// <summary>
/// What the kit draws of an arena: every mesh the level's scenery layers place (stands, roof, boards, glass, banners,
/// lights, props), the venue's own root meshes the layers don't place (at the origin, where the level puts them), and
/// the NHL ice with the arena's centre-ice logo.
/// Placements: in a level EBX, a placed ObjectBlueprint or SpatialPrefabBlueprint is an import reference, and a
/// few 8-byte slots before it a relative pointer (i32, upper half 0) leads to an array (u32 count before it) of 64-byte
/// LinearTransforms, one per instance. Blueprints lead to their mesh, and prefabs are read the same way with their
/// transforms composed with the instance's.
/// Each mesh section's material is a SurfaceShaderPreset the mesh imports, named like the section; the preset imports
/// its textures, and the one ending _color is drawn.
/// </summary>
public static class ArenaScene
{
    public const string Stadiums = "content/worlds/stadiums/", StructuresRoot = "content/worlds/components/structures/",
        ScoreboardRoot = "content/worlds/scoreboard/";

    /// <summary>The jumbotron and its parts (content/worlds/scoreboard/VENUE/).</summary>
    public static bool IsScoreboard(string mesh) => mesh.StartsWith(ScoreboardRoot, StringComparison.OrdinalIgnoreCase);
    public const string Ice = "content/worlds/rink/ice/components/rink_ice_nhl_mesh";
    private static readonly string[] MeshTypes = { "RigidMeshAsset", "CompositeMeshAsset", "SkinnedMeshAsset", "MeshAsset" };
    private static readonly string[] ColorEnds = { "_color", "_basecolor", "_albedo", "_diffuse", "_cs" };

    /// <summary>Every arena level that has structure meshes of its own.</summary>
    public static List<ArenaLevel> Levels(AssetIndex index)
    {
        var venues = index.Ebx.Values.Where(e => e.Name.StartsWith(StructuresRoot, StringComparison.OrdinalIgnoreCase) && MeshTypes.Contains(e.Type))
            .Select(e => e.Name[StructuresRoot.Length..].Split('/')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = new List<ArenaLevel>();
        var rx = new Regex(@"^content/worlds/stadiums/(([a-z0-9_]+?)_(\d+))/\1$", RegexOptions.IgnoreCase);
        foreach (var e in index.Ebx.Values)
            if (rx.Match(e.Name) is { Success: true } m && venues.Contains(m.Groups[2].Value))
                list.Add(new ArenaLevel(m.Groups[1].Value, m.Groups[2].Value, int.Parse(m.Groups[3].Value)));
        return list.OrderBy(a => a.Venue, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList();
    }

    // layers that hold no scenery (lights, effects, the crowd, cameras, game modes other than 5-on-5)
    private static readonly string[] SkipLayers = { "lighting", "lighing", "effects", "crowd", "nis", "autotest", "locators", "schematic", "state",
        "presentation", "threes", "occluders", "modes", "replay", "networkregistry", "meshvariationdb", "description" };

    /// <summary>The level's EBX that place scenery: its SubWorldData and LayerData, less lighting, effects, crowd and the like.</summary>
    public static List<string> Layers(AssetIndex index, ArenaLevel arena) =>
        index.Ebx.Values.Where(e => e.Name.StartsWith(arena.Path + "/", StringComparison.OrdinalIgnoreCase) && e.Type is "SubWorldData" or "LayerData" or "DetachedSubWorldData"
                && !SkipLayers.Any(w => e.Name[(arena.Path.Length + 1)..].Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Everything drawn for an arena: the meshes the level places, the venue's root meshes it doesn't (at the origin),
    /// and the ice at the origin if the venue has NHL boards.
    /// </summary>
    public static List<ArenaPlacement> Placements(AssetIndex index, ArenaLevel arena, Dictionary<Guid, string> names)
    {
        var list = new List<ArenaPlacement>();
        var seen = new HashSet<string>();
        var prefabs = new Dictionary<string, List<ArenaPlacement>>(StringComparer.OrdinalIgnoreCase);
        void Add(ArenaPlacement p)
        {
            if (index.GetRes(p.Mesh) == null) return;
            string key = p.Mesh + "|" + string.Join(",", p.Transform.Select(v => MathF.Round(v, 2)));
            if (seen.Add(key)) list.Add(p);
        }
        foreach (var layer in Layers(index, arena))
            foreach (var p in Placed(index, layer, names, prefabs, 0)) Add(p);
        // the scoreboard is loaded by the game's state logic, not a layer: its prefab, modelled where it hangs
        if (!list.Any(p => IsScoreboard(p.Mesh)))
        {
            string folder = $"{ScoreboardRoot}{arena.Venue}/scoreboard_{arena.Venue}";
            string prefab = folder + "_prefab";
            if (index.GetEbx(prefab) != null)
            {
                foreach (var p in Placed(index, prefab, names, prefabs, 0)) Add(p);
                // its parts (body, screens, LED rings) are modelled where they hang, so any the prefab's records miss go in as they are
                var f = EbxFile.Parse(index.Read(index.GetEbx(prefab)));
                foreach (var g in f?.Imports.Select(i => i.File).Distinct() ?? Enumerable.Empty<Guid>())
                    if (names.TryGetValue(g, out var n) && index.GetEbx(n)?.Type == "ObjectBlueprint")
                        foreach (var p in BlueprintMesh(index, n, names))
                            if (!list.Any(x => x.Mesh.Equals(p.Mesh, StringComparison.OrdinalIgnoreCase))) Add(p);
            }
            if (!list.Any(p => p.Mesh.Equals(folder + "_mesh", StringComparison.OrdinalIgnoreCase)) && index.GetEbx(folder + "_mesh") != null)
                Add(new ArenaPlacement(folder + "_mesh", ArenaPlacement.Identity));
        }
        var placed = list.Select(p => p.Mesh).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var e in index.Ebx.Values.Where(e => e.Name.StartsWith(arena.Structures, StringComparison.OrdinalIgnoreCase) && MeshTypes.Contains(e.Type)
                     && !e.Name[arena.Structures.Length..].Contains('/') && !placed.Contains(e.Name)).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Add(new ArenaPlacement(e.Name, ArenaPlacement.Identity));
        if (index.Ebx.Values.Any(e => e.Name.StartsWith(arena.Structures, StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith("/rink_boards_mesh", StringComparison.OrdinalIgnoreCase))
            && !list.Any(p => p.Mesh.StartsWith("content/worlds/rink/ice/", StringComparison.OrdinalIgnoreCase)))
            Add(new ArenaPlacement(Ice, ArenaPlacement.Identity));
        return list;
    }

    /// <summary>The meshes an EBX places, in its own space; prefabs and blueprints are followed (cached in <paramref name="prefabs"/>).</summary>
    private static List<ArenaPlacement> Placed(AssetIndex index, string name, Dictionary<Guid, string> names, Dictionary<string, List<ArenaPlacement>> prefabs, int depth)
    {
        var list = new List<ArenaPlacement>();
        EbxFile f;
        try { f = EbxFile.Parse(index.Read(index.GetEbx(name))); } catch { return list; }
        if (f == null) return list;
        var d = f.Data;
        foreach (uint off in f.ImportOffsets)
        {
            int at = EbxFile.Payload + (int)off;
            if (at + 8 > f.DataEnd) continue;
            int idx = BitConverter.ToInt32(d, at);
            if (idx < 0 || idx >= f.Imports.Length || !names.TryGetValue(f.Imports[idx].File, out var target)) continue;
            string type = index.GetEbx(target)?.Type;
            // bare mesh references sit in other records (variations, lists) next to unrelated transform arrays: only
            // blueprints and prefabs are placed
            if (type is not ("ObjectBlueprint" or "SpatialPrefabBlueprint")) continue;
            var transforms = TransformsBefore(d, at, f.DataEnd);
            if (transforms == null) continue;
            IEnumerable<ArenaPlacement> inner;
            if (depth >= 4) continue;
            {
                if (!prefabs.TryGetValue(target, out var cached))
                {
                    prefabs[target] = cached = new List<ArenaPlacement>();   // a prefab that places itself stops here
                    cached.AddRange(type == "ObjectBlueprint" ? BlueprintMesh(index, target, names) : Placed(index, target, names, prefabs, depth + 1));
                }
                inner = cached;
            }
            foreach (var t in transforms)
                foreach (var p in inner) list.Add(p with { Transform = Compose(p.Transform, t) });
        }
        return list;
    }

    /// <summary>An ObjectBlueprint's mesh, at its origin.</summary>
    private static IEnumerable<ArenaPlacement> BlueprintMesh(AssetIndex index, string blueprint, Dictionary<Guid, string> names)
    {
        EbxFile f;
        try { f = EbxFile.Parse(index.Read(index.GetEbx(blueprint))); } catch { yield break; }
        if (f == null) yield break;
        foreach (var g in f.Imports.Select(i => i.File).Distinct())
            if (names.TryGetValue(g, out var n) && MeshTypes.Contains(index.GetEbx(n)?.Type))
            {
                yield return new ArenaPlacement(n, ArenaPlacement.Identity);
                yield break;
            }
    }

    // where the transforms pointer sits before the import reference, in 8-byte slots: 13 in a static model group's member,
    // 7 in a reference to an object or prefab
    private static readonly int[] Slots = { 7, 13 };

    /// <summary>The instance transforms an import reference at <paramref name="at"/> owns, or null.</summary>
    private static List<float[]> TransformsBefore(byte[] d, int at, int end)
    {
        foreach (int s in Slots)
        {
            int fp = at - s * 8;
            if (fp < EbxFile.Payload) break;
            int rel = BitConverter.ToInt32(d, fp);
            if (rel == 0 || BitConverter.ToInt32(d, fp + 4) != 0) continue;
            long pa = (long)fp + rel;
            if (pa < EbxFile.Payload + 4 || pa + 64 > end || (pa & 15) != 0) continue;
            uint count = BitConverter.ToUInt32(d, (int)pa - 4);
            if (count == 0 || count > 20000 || pa + count * 64L > end) continue;
            var list = new List<float[]>((int)count);
            for (int i = 0; i < count; i++)
            {
                var t = ReadTransform(d, (int)pa + i * 64);
                if (t == null) { list = null; break; }
                list.Add(t);
            }
            if (list != null) return list;
        }
        return null;
    }

    /// <summary>A LinearTransform (four float4 rows, the fourth component 0) whose 3×3 is a rotation with a uniform scale, or null.</summary>
    private static float[] ReadTransform(byte[] d, int p)
    {
        var t = new float[12];
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 3; c++) t[r * 3 + c] = BitConverter.ToSingle(d, p + r * 16 + c * 4);
            if (BitConverter.ToSingle(d, p + r * 16 + 12) != 0) return null;
        }
        if (t.Any(v => !float.IsFinite(v))) return null;
        double L(int r) => t[r * 3] * t[r * 3] + t[r * 3 + 1] * t[r * 3 + 1] + t[r * 3 + 2] * t[r * 3 + 2];
        double D(int a, int b) => t[a * 3] * t[b * 3] + t[a * 3 + 1] * t[b * 3 + 1] + t[a * 3 + 2] * t[b * 3 + 2];
        double l0 = L(0);
        if (l0 < 1e-6 || l0 > 1e6 || Math.Abs(L(1) - l0) > 0.03 * l0 || Math.Abs(L(2) - l0) > 0.03 * l0) return null;
        if (Math.Abs(D(0, 1)) > 0.03 * l0 || Math.Abs(D(0, 2)) > 0.03 * l0 || Math.Abs(D(1, 2)) > 0.03 * l0) return null;
        if (Math.Abs(t[9]) > 5000 || Math.Abs(t[10]) > 5000 || Math.Abs(t[11]) > 5000) return null;
        return t;
    }

    /// <summary><paramref name="inner"/> then <paramref name="outer"/>: rows times the outer 3×3, translation carried through.</summary>
    public static float[] Compose(float[] inner, float[] outer)
    {
        var r = new float[12];
        for (int row = 0; row < 4; row++)
            for (int c = 0; c < 3; c++)
            {
                float v = inner[row * 3] * outer[c] + inner[row * 3 + 1] * outer[3 + c] + inner[row * 3 + 2] * outer[6 + c];
                r[row * 3 + c] = row == 3 ? v + outer[9 + c] : v;
            }
        return r;
    }

    /// <summary>EBX names by file id, for following imports.</summary>
    public static Dictionary<Guid, string> Names(AssetIndex index)
    {
        var byId = new Dictionary<Guid, string>();
        foreach (var e in index.Ebx.Values) byId.TryAdd(e.Id, e.Name);
        return byId;
    }

    /// <summary>Every material preset by its short name, for materials a mesh doesn't import itself.</summary>
    public static Dictionary<string, string> Presets(AssetIndex index)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in index.Ebx.Values)
            if (e.Type == "SurfaceShaderPreset" && e.Name.StartsWith("content/worlds/", StringComparison.OrdinalIgnoreCase))
                map.TryAdd(e.Name[(e.Name.LastIndexOf('/') + 1)..], e.Name);
        return map;
    }

    /// <summary>
    /// A mesh's full-detail sections with their colour textures. <paramref name="allPresets"/> is <see cref="Presets"/>;
    /// <paramref name="textures"/> caches preset → texture.
    /// </summary>
    public static List<ArenaPiece> Load(AssetIndex index, ArenaLevel arena, string mesh, Dictionary<Guid, string> names, Dictionary<string, string> allPresets,
        Dictionary<string, string> textures)
    {
        var set = Kits.LoadMesh(index, mesh);
        if (set.Lods.Count == 0) return new();
        var ebx = EbxFile.Parse(index.Read(index.GetEbx(mesh)));
        // the materials this mesh imports, by short name
        var imported = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in ebx?.Imports.Select(i => i.File).Distinct() ?? Enumerable.Empty<Guid>())
            if (names.TryGetValue(g, out var n)) imported.TryAdd(n[(n.LastIndexOf('/') + 1)..], n);

        var list = new List<ArenaPiece>();
        foreach (var part in set.Lods[0])
        {
            string material = part.Material ?? "";
            if (material.Contains("shadow", StringComparison.OrdinalIgnoreCase) || part.Indices.Length == 0) continue;
            var kind = material.StartsWith("ice_centerlogo", StringComparison.OrdinalIgnoreCase) ? ArenaPieceKind.CenterLogo
                : material.StartsWith("ice_", StringComparison.OrdinalIgnoreCase) ? ArenaPieceKind.Ice
                : material.Contains("glass", StringComparison.OrdinalIgnoreCase) ? ArenaPieceKind.Glass
                // the jumbotron's video screens: filled by the game at run time
                : material.StartsWith("scoreboard_screen", StringComparison.OrdinalIgnoreCase) ? ArenaPieceKind.Screen : ArenaPieceKind.Solid;
            string texture = kind == ArenaPieceKind.CenterLogo ? CenterLogo(index, arena.Id) : null;
            string bare = material.EndsWith("_mat", StringComparison.OrdinalIgnoreCase) ? material[..^4] : material;
            if (texture == null && kind is ArenaPieceKind.Solid or ArenaPieceKind.Glass
                && (imported.GetValueOrDefault(material) ?? allPresets.GetValueOrDefault(material) ?? allPresets.GetValueOrDefault(bare)) is { } preset)
            {
                if (!textures.TryGetValue(preset, out texture))
                    textures[preset] = texture = ColorTexture(index, preset, names);
            }
            list.Add(new ArenaPiece(mesh, part, texture, kind));
        }
        return list;
    }

    /// <summary>The arena's centre-ice logo (ice_centerlogo_ID_color), or the default one.</summary>
    public static string CenterLogo(AssetIndex index, int id)
    {
        string name = $"content/worlds/rink/textures/centerlogo/ice_centerlogo_{id}_color";
        return index.GetEbx(name) != null ? name : index.GetEbx("content/worlds/rink/textures/centerlogo/ice_centerlogo_0_color")?.Name;
    }

    /// <summary>The colour texture a material preset imports, or null.</summary>
    public static string ColorTexture(AssetIndex index, string preset, Dictionary<Guid, string> names)
    {
        var e = index.GetEbx(preset);
        if (e == null) return null;
        EbxFile f;
        try { f = EbxFile.Parse(index.Read(e)); } catch { return null; }
        if (f == null) return null;
        var textures = f.Imports.Select(i => i.File).Distinct().Select(g => names.GetValueOrDefault(g))
            .Where(n => n != null && index.GetEbx(n)?.Type == "TextureAsset").ToList();
        string found = null;
        foreach (var end in ColorEnds)
            if (textures.FirstOrDefault(t => t.EndsWith(end, StringComparison.OrdinalIgnoreCase)) is { } t) { found = t; break; }
        found ??= textures.FirstOrDefault(t => !t.EndsWith("_normal", StringComparison.OrdinalIgnoreCase) && !t.EndsWith("_coeff", StringComparison.OrdinalIgnoreCase));
        return Variant(index, preset, found) ?? found;
    }

    /// <summary>
    /// A numbered copy of a material (banner_stlouis_roof_rectangle_02_mat) imports its parent's texture, but the game
    /// shows its own numbered one (banner_stlouis_roof_02_color: the 2019 Cup banner).
    /// </summary>
    private static string Variant(AssetIndex index, string preset, string texture)
    {
        if (texture == null) return null;
        var m = Regex.Match(preset, @"_(\d{2})(_mat)?$");
        if (!m.Success) return null;
        string number = "_" + m.Groups[1].Value;
        foreach (var end in ColorEnds)
            if (texture.EndsWith(end, StringComparison.OrdinalIgnoreCase) && !texture[..^end.Length].EndsWith(number))
            {
                string name = texture[..^end.Length] + number + end;
                if (index.GetEbx(name)?.Type == "TextureAsset") return name;
            }
        return null;
    }
}
