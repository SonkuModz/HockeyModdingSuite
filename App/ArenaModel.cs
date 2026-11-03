using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>What was drawn of an arena.</summary>
public sealed record ArenaStats(int Meshes, int Placed, int Sections, long Triangles, int Textures, int Flat, List<string> Problems);

/// <summary>
/// Builds the WPF scene of an arena from <see cref="ArenaScene"/>: textures decoded at up to 512 px, glass drawn last and
/// see-through, materials without a texture coloured by their name, and the ice covered with NHL markings drawn by the
/// kit around the arena's own centre-ice logo.
/// </summary>
public static class ArenaModel
{
    private const int TextureSize = 512;
    // the NHL rink: 200 × 85 ft, corner radius 28 ft; in the game 60.96 × 25.98 m, length along Z
    private const double LengthFt = 200, WidthFt = 85, CornerFt = 28, PxPerFt = 10;
    private const double LengthM = 60.96, WidthM = 25.98;

    /// <summary>The placements, each mesh's pieces and the textures, read off the UI thread.</summary>
    public sealed class Loaded
    {
        public List<ArenaPlacement> Placements { get; set; } = new();
        public Dictionary<string, List<ArenaPiece>> Pieces { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Each mesh's bounds in its own space: min x, y, z then max x, y, z, in metres.</summary>
        public Dictionary<string, float[]> Bounds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, BitmapSource> Textures { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Problems { get; } = new();
        public string CenterLogo => Pieces.Values.SelectMany(l => l).FirstOrDefault(p => p.Kind == ArenaPieceKind.CenterLogo)?.Texture;
    }

    public static Loaded Load(AssetIndex index, ArenaLevel arena, Action<string> progress)
    {
        var result = new Loaded();
        var names = ArenaScene.Names(index);
        var presets = ArenaScene.Presets(index);
        var cache = new Dictionary<string, string>();
        progress?.Invoke("Reading the level…");
        result.Placements = ArenaScene.Placements(index, arena, names);
        var meshes = result.Placements.Select(p => p.Mesh).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        for (int i = 0; i < meshes.Count; i++)
        {
            progress?.Invoke($"Reading meshes… {i + 1} of {meshes.Count}");
            try
            {
                var pieces = ArenaScene.Load(index, arena, meshes[i], names, presets, cache);
                result.Pieces[meshes[i]] = pieces;
                result.Bounds[meshes[i]] = Bounds(pieces);
            }
            catch (Exception ex) { result.Problems.Add($"{Short(meshes[i])}: {ex.Message}"); }
        }
        var wanted = result.Pieces.Values.SelectMany(l => l).Where(p => p.Texture != null).Select(p => p.Texture).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int done = 0;
        Parallel.ForEach(wanted, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) }, name =>
        {
            BitmapSource bmp = null;
            try
            {
                var t = Kits.Load(index, name, TextureSize, 0);
                if (t.Rgba != null) bmp = ToBitmap(t.Rgba, t.Width, t.Height);
            }
            catch { /* drawn flat */ }
            lock (result)
            {
                if (bmp != null) result.Textures[name] = bmp;
                progress?.Invoke($"Decoding textures… {++done} of {wanted.Count}");
            }
        });
        return result;
    }

    private static float[] Bounds(List<ArenaPiece> pieces)
    {
        var b = new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
        foreach (var p in pieces)
            for (int v = 0; v < p.Part.Positions.Length; v++)
            {
                b[v % 3] = Math.Min(b[v % 3], p.Part.Positions[v]);
                b[3 + v % 3] = Math.Max(b[3 + v % 3], p.Part.Positions[v]);
            }
        return b;
    }

    /// <summary>
    /// A misread placement: a copy of a mesh the size of the bowl, or anything but the rink itself with its vertices over the
    /// ice below the scoreboard (nothing in an arena stands there; a bowl's box covers the ice, so the vertices are counted).
    /// </summary>
    private static bool Misplaced(string mesh, float[] t, float[] b, List<ArenaPiece> pieces)
    {
        if (b == null || b[0] > b[3]) return false;
        if (Math.Max(b[3] - b[0], Math.Max(b[4] - b[1], b[5] - b[2])) > 40 && !IsIdentity(t)) return true;
        // the rink and the jumbotron (Montreal's hangs down to 11.7 m) belong over the ice
        if (Short(mesh).StartsWith("rink_", StringComparison.OrdinalIgnoreCase) || ArenaScene.IsScoreboard(mesh)) return false;
        int over = 0, all = 0;
        foreach (var p in pieces)
        {
            var ps = p.Part.Positions;
            for (int v = 0; v + 2 < ps.Length; v += 3)
            {
                double x = ps[v], y = ps[v + 1], z = ps[v + 2];
                double wx = x * t[0] + y * t[3] + z * t[6] + t[9], wy = x * t[1] + y * t[4] + z * t[7] + t[10], wz = x * t[2] + y * t[5] + z * t[8] + t[11];
                all++;
                if (Math.Abs(wx) < WidthM / 2 - 1 && Math.Abs(wz) < LengthM / 2 - 2 && wy > -0.5 && wy < 12) over++;
            }
        }
        return over > 8 && over * 50 > all;
    }

    /// <summary>A mesh's sections as models, made once and shared by all its placements.</summary>
    private sealed record Made(List<GeometryModel3D> Solid, List<GeometryModel3D> Glass, int Triangles, int Flat);

    /// <summary>The scene, frozen. <paramref name="rink"/> is the ice image from <see cref="DrawRink"/>.</summary>
    public static (Model3DGroup Scene, ArenaStats Stats) Build(Loaded loaded, ImageSource rink, bool glass, bool ice, bool roof, bool props, bool jumbotron)
    {
        var group = new Model3DGroup();
        var see = new List<Model3D>();
        long tris = 0;
        int sections = 0, flat = 0, placed = 0;
        var rinkBrush = rink == null ? null : Frozen(new ImageBrush(rink) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) });
        var flats = new Dictionary<Color, Material>();
        var made = new Dictionary<string, Made>(StringComparer.OrdinalIgnoreCase);
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pl in loaded.Placements)
        {
            if (!loaded.Pieces.TryGetValue(pl.Mesh, out var pieces) || pieces.Count == 0) continue;
            if (!roof && IsRoof(pl.Mesh) || !props && IsProp(pl.Mesh) || !jumbotron && ArenaScene.IsScoreboard(pl.Mesh)) continue;
            if (Misplaced(pl.Mesh, pl.Transform, loaded.Bounds.GetValueOrDefault(pl.Mesh), pieces)) continue;
            if (!made.TryGetValue(pl.Mesh, out var m)) made[pl.Mesh] = m = Make(pieces, loaded, rinkBrush, flats, glass, ice);
            if (m.Solid.Count + m.Glass.Count == 0) continue;
            var t = pl.Transform;
            var transform = IsIdentity(t) ? null : Frozen(new MatrixTransform3D(new Matrix3D(t[0], t[1], t[2], 0, t[3], t[4], t[5], 0, t[6], t[7], t[8], 0, t[9], t[10], t[11], 1)));
            Model3D Wrap(List<GeometryModel3D> models)
            {
                if (transform == null && models.Count == 1) return models[0];
                var g = new Model3DGroup { Transform = transform ?? Transform3D.Identity };
                foreach (var x in models) g.Children.Add(x);
                g.Freeze();
                return g;
            }
            if (m.Solid.Count > 0) group.Children.Add(Wrap(m.Solid));
            if (m.Glass.Count > 0) see.Add(Wrap(m.Glass));
            tris += m.Triangles;
            placed++;
            if (shown.Add(pl.Mesh)) { sections += m.Solid.Count + m.Glass.Count; flat += m.Flat; }
        }
        // see-through last, so what's behind the glass is already drawn
        foreach (var x in see) group.Children.Add(x);
        group.Freeze();
        return (group, new ArenaStats(shown.Count, placed, sections, tris, loaded.Textures.Count, flat, loaded.Problems));
    }

    private static Made Make(List<ArenaPiece> pieces, Loaded loaded, Brush rinkBrush, Dictionary<Color, Material> flats, bool glass, bool ice)
    {
        var solid = new List<GeometryModel3D>();
        var see = new List<GeometryModel3D>();
        int tris = 0, flat = 0;
        foreach (var p in pieces)
        {
            bool isIce = p.Kind is ArenaPieceKind.Ice or ArenaPieceKind.CenterLogo;
            if (isIce && !ice || p.Kind == ArenaPieceKind.Glass && !glass) continue;
            // every ice section (ads, lines, the centre logo) lies on the ice and gets the drawn rink; _ZOnly is a depth-only copy
            if (isIce && (p.Part.Material ?? "").EndsWith("_ZOnly", StringComparison.OrdinalIgnoreCase)) continue;
            var part = p.Part;
            var mesh = new MeshGeometry3D();
            var pos = new Point3DCollection(part.VertexCount);
            var nrm = new Vector3DCollection(part.VertexCount);
            var uv = new PointCollection(part.VertexCount);
            for (int v = 0; v < part.VertexCount; v++)
            {
                double x = part.Positions[v * 3], y = part.Positions[v * 3 + 1], z = part.Positions[v * 3 + 2];
                pos.Add(new Point3D(x, y, z));
                nrm.Add(new Vector3D(part.Normals[v * 3], part.Normals[v * 3 + 1], part.Normals[v * 3 + 2]));
                if (isIce) uv.Add(new Point((x + WidthM / 2) / WidthM, (z + LengthM / 2) / LengthM));
                else if (part.Uvs != null) uv.Add(new Point(part.Uvs[v * 2], part.Uvs[v * 2 + 1]));
            }
            var ind = new Int32Collection(part.Indices.Length);
            foreach (int i in part.Indices) ind.Add(i);
            mesh.Positions = pos;
            mesh.Normals = nrm;
            if (uv.Count > 0) mesh.TextureCoordinates = uv;
            mesh.TriangleIndices = ind;
            mesh.Freeze();

            Material material;
            if (isIce && rinkBrush != null) material = Frozen(new DiffuseMaterial(rinkBrush));
            else if (p.Kind == ArenaPieceKind.Screen) material = Screen(part, loaded);
            else if (p.Kind == ArenaPieceKind.Glass)
                material = Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(Color.FromArgb(46, 190, 214, 230)))));
            else if (p.Texture != null && part.Uvs != null && loaded.Textures.TryGetValue(p.Texture, out var bmp))
                material = Frozen(new DiffuseMaterial(Frozen(new ImageBrush(bmp) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1), TileMode = TileMode.Tile })));
            else
            {
                var c = ColorOf(part.Material);
                if (!flats.TryGetValue(c, out material)) flats[c] = material = Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(c))));
                flat++;
            }
            // two-sided: from inside the bowl the walls face away from the camera
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            (p.Kind == ArenaPieceKind.Glass ? see : solid).Add(model);
            tris += part.TriangleCount;
        }
        return new Made(solid, see, tris, flat);
    }

    /// <summary>
    /// A jumbotron screen. The game plays video on it, so the kit shows the arena's centre logo on a dark panel, drawn at
    /// the screen's own width and height so it isn't stretched.
    /// </summary>
    private static Material Screen(MeshPart part, Loaded loaded)
    {
        // the screen's size in metres from how far a step in u and in v goes on each triangle (round screens too)
        double w = 1, h = 1;
        if (part.Uvs != null)
        {
            var ps = part.Positions; var uv = part.Uvs;
            double su = 0, sv = 0, n = 0, u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            for (int k = 0; k + 2 < part.Indices.Length; k += 3)
            {
                int i = part.Indices[k], j = part.Indices[k + 1], l = part.Indices[k + 2];
                double du1 = uv[j * 2] - uv[i * 2], dv1 = uv[j * 2 + 1] - uv[i * 2 + 1], du2 = uv[l * 2] - uv[i * 2], dv2 = uv[l * 2 + 1] - uv[i * 2 + 1];
                double det = du1 * dv2 - du2 * dv1;
                if (Math.Abs(det) < 1e-9) continue;
                double lu = 0, lv = 0;
                for (int c = 0; c < 3; c++)
                {
                    double e1 = ps[j * 3 + c] - ps[i * 3 + c], e2 = ps[l * 3 + c] - ps[i * 3 + c];
                    double pu = (e1 * dv2 - e2 * dv1) / det, pv = (e2 * du1 - e1 * du2) / det;
                    lu += pu * pu; lv += pv * pv;
                }
                su += Math.Sqrt(lu); sv += Math.Sqrt(lv); n++;
                foreach (int x in new[] { i, j, l })
                {
                    u0 = Math.Min(u0, uv[x * 2]); u1 = Math.Max(u1, uv[x * 2]);
                    v0 = Math.Min(v0, uv[x * 2 + 1]); v1 = Math.Max(v1, uv[x * 2 + 1]);
                }
            }
            if (n > 0)
            {
                w = Math.Max(su / n * (u1 - u0), 0.1);
                h = Math.Max(sv / n * (v1 - v0), 0.1);
            }
        }
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Frozen(new LinearGradientBrush(Color.FromRgb(16, 22, 34), Color.FromRgb(6, 8, 12), 90)), null, new RectangleGeometry(new Rect(0, 0, w, h))));
        var logo = loaded.CenterLogo is { } name ? loaded.Textures.GetValueOrDefault(name) : null;
        if (logo != null)
        {
            double size = Math.Min(w, h) * 0.8;
            drawing.Children.Add(new ImageDrawing(logo, new Rect((w - size) / 2, (h - size) / 2, size, size)));
        }
        drawing.Freeze();
        var brush = Frozen(new DrawingBrush(drawing) { Stretch = Stretch.Fill, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) });
        // lit from within
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(brush));
        material.Children.Add(new EmissiveMaterial(brush));
        material.Freeze();
        return material;
    }

    private static bool IsIdentity(float[] t) => t.Select((v, i) => Math.Abs(v - ArenaPlacement.Identity[i])).Max() < 1e-3;

    /// <summary>Props: the shared objects (ladders, tables, lights), not the venue's structures or the rink.</summary>
    public static bool IsProp(string mesh) => mesh.StartsWith("content/worlds/components/objects/", StringComparison.OrdinalIgnoreCase);

    /// <summary>The centre-ice ads (CenterAd1..4) and where they came from.</summary>
    public sealed record Ads(BitmapSource[] Images, string Source);

    /// <summary>The game's centre-ice ads for an arena: its four hotspot ids' textures.</summary>
    public static Ads GameAds(AssetIndex index, int arenaId)
    {
        var ids = IceAds.GameCenterAds(index, arenaId);
        var images = ids.Select(id =>
        {
            try { return IceAds.Texture(index, id) is { } name && Kits.Load(index, name, TextureSize, 0) is { Rgba: not null } t ? ToBitmap(t.Rgba, t.Width, t.Height) : null; }
            catch { return null; }
        }).ToArray();
        return new Ads(images, $"the game's (hotspots {string.Join(", ", ids)})");
    }

    /// <summary>
    /// An NHL team's own centre-ice ads, cut from its current rink on TheFaceoff.net (the layout is kept in
    /// export/faceoff/downloads after the first download). Null when the site has no rink for the team.
    /// </summary>
    public static async Task<Ads> TeamAds(string team, Action<string> progress)
    {
        var rink = await IceAds.FindRinkAsync(team, progress);
        if (rink == null) return null;
        progress?.Invoke($"Reading {rink.Name} from TheFaceoff.net…");
        var (title, layout) = await FaceoffNet.RinkAsync(rink.Url);
        if (layout == null) return null;
        progress?.Invoke($"Downloading {rink.Name}'s layout…");
        string file = await FaceoffNet.DownloadAsync(layout, System.IO.Path.Combine(KitPaths.Export, "faceoff", "downloads"));
        return await Task.Run(() =>
        {
            var (src, w, h) = ImageLoader.Load(file);
            var g = CenterIce.Measure(src, w, h);
            var look = new CenterIceLook();
            var images = CenterIce.CenterAds.Select(s => ToBitmap(CenterIce.Hotspot(src, w, h, g, look, s), CenterIce.AdWidth, CenterIce.AdHeight)).ToArray();
            return new Ads(images, $"{(string.IsNullOrWhiteSpace(title) ? rink.Name : title)} from TheFaceoff.net");
        });
    }

    /// <summary>
    /// The ice, 85 × 200 ft at <see cref="PxPerFt"/>: white ice, the centre-ice logo and ads, then the NHL lines, circles
    /// and creases at their regulation places. Length runs down the image. Drawn on the UI thread.
    /// </summary>
    public static BitmapSource DrawRink(BitmapSource logo, BitmapSource[] ads = null)
    {
        int w = (int)(WidthFt * PxPerFt), h = (int)(LengthFt * PxPerFt);
        var red = new SolidColorBrush(Color.FromRgb(200, 30, 45));
        var blue = new SolidColorBrush(Color.FromRgb(20, 60, 170));
        var crease = new SolidColorBrush(Color.FromRgb(120, 190, 235));
        var dv = new DrawingVisual();
        using (var g = dv.RenderOpen())
        {
            double F(double ft) => ft * PxPerFt;
            double cx = w / 2.0, cy = h / 2.0;
            g.DrawRectangle(new SolidColorBrush(Color.FromRgb(238, 242, 246)), null, new Rect(0, 0, w, h));
            if (logo != null)
            {
                double span = F(CenterIce.LogoSpanFeet);
                // upright from the broadcast side (the camera on +X looking across the ice)
                g.PushTransform(new RotateTransform(-90, cx, cy));
                g.DrawImage(logo, new Rect(cx - span / 2, cy - span / 2, span, span));
                g.Pop();
            }
            // the ads in their slots, mapped as the game maps them: texel (u, v) at Origin + u·U + v·V
            for (int i = 0; i < CenterIce.CenterAds.Length && ads != null && i < ads.Length; i++)
            {
                if (ads[i] == null) continue;
                var s = CenterIce.CenterAds[i];
                double kx = w / WidthM, kz = h / LengthM;
                g.PushTransform(new MatrixTransform(s.Ux * kx, s.Uz * kz, s.Vx * kx, s.Vz * kz, (s.Ox + WidthM / 2) * kx, (s.Oz + LengthM / 2) * kz));
                g.DrawImage(ads[i], new Rect(0, 0, 1, 1));
                g.Pop();
            }
            // lines across the ice (y = distance from the centre line along the length)
            void Across(double y, double widthFt, Brush b) => g.DrawRectangle(b, null, new Rect(0, cy + F(y) - F(widthFt) / 2, w, F(widthFt)));
            Across(0, 1, red);
            foreach (int s in new[] { -1, 1 })
            {
                Across(s * 25.5, 1, blue);
                Across(s * 89, 2 / 12.0, red);
                // goal crease: a 6 ft half circle in front of the goal line
                var c = new Point(cx, cy + F(s * 89));
                var half = new PathGeometry(new[] { new PathFigure(new Point(cx - F(6), c.Y), new PathSegment[]
                    { new ArcSegment(new Point(cx + F(6), c.Y), new Size(F(6), F(6)), 0, false, s > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise, true) }, true) });
                g.DrawGeometry(crease, new Pen(red, F(2 / 12.0)), half);
                // trapezoid behind the net
                var pen = new Pen(red, F(2 / 12.0));
                g.DrawLine(pen, new Point(cx - F(11), c.Y), new Point(cx - F(14), cy + F(s * 100)));
                g.DrawLine(pen, new Point(cx + F(11), c.Y), new Point(cx + F(14), cy + F(s * 100)));
                foreach (int side in new[] { -1, 1 })
                {
                    // end-zone circles and dots, and the neutral-zone dots
                    var dot = new Point(cx + F(side * 22), cy + F(s * 69));
                    g.DrawEllipse(null, new Pen(red, F(2 / 12.0)), dot, F(15), F(15));
                    g.DrawEllipse(red, null, dot, F(1), F(1));
                    g.DrawEllipse(red, null, new Point(cx + F(side * 22), cy + F(s * 20)), F(1), F(1));
                }
            }
            g.DrawEllipse(null, new Pen(blue, F(2 / 12.0)), new Point(cx, cy), F(15), F(15));
            g.DrawEllipse(blue, null, new Point(cx, cy), F(0.5), F(0.5));
        }
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>A colour for a material with no texture, from the words in its name.</summary>
    public static Color ColorOf(string material)
    {
        string m = (material ?? "").ToLowerInvariant();
        (string Word, Color C)[] words =
        {
            ("lightblue", Color.FromRgb(140, 180, 220)), ("lightgrey", Color.FromRgb(190, 192, 196)), ("blue", Color.FromRgb(30, 64, 150)),
            ("red", Color.FromRgb(170, 30, 40)), ("black", Color.FromRgb(28, 28, 30)), ("white", Color.FromRgb(232, 232, 232)),
            ("yellow", Color.FromRgb(220, 190, 40)), ("green", Color.FromRgb(40, 120, 60)), ("orange", Color.FromRgb(220, 110, 30)),
            ("gold", Color.FromRgb(190, 150, 60)), ("teflon", Color.FromRgb(236, 236, 232)), ("boards", Color.FromRgb(236, 236, 232)),
            ("carpet", Color.FromRgb(60, 50, 50)), ("concrete", Color.FromRgb(150, 150, 146)), ("grey", Color.FromRgb(140, 142, 146)),
        };
        foreach (var (word, c) in words) if (m.Contains(word)) return c;
        return Color.FromRgb(128, 130, 136);
    }

    public static BitmapSource ToBitmap(byte[] rgba, int width, int height)
    {
        var bgra = new byte[rgba.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i];
            bgra[i + 3] = rgba[i + 3];
        }
        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
        bmp.Freeze();
        return bmp;
    }

    public static bool IsRoof(string mesh) => Short(mesh).StartsWith("roof", StringComparison.OrdinalIgnoreCase);

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public static string Short(string name) => name[(name.LastIndexOf('/') + 1)..];
}
