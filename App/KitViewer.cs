using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Runtime.CompilerServices;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// A 3D view of mesh parts with their textures: drag to turn the model, scroll to zoom, double-click to reset.
/// Frostbite is Y-up with the front facing +Z, which is how the camera starts.
/// Skinned parts play the game's menu idle (<see cref="Rig"/>) while "Idle" is on, else stand in the bind pose.
/// The lights (a warm key, a cool fill and a rim from behind) turn with the camera, so whichever side is in view is
/// lit the same way; "Lighting" off draws the textures flat, exactly as they are.
/// WPF 3D shades per vertex and has no normal maps, so a part given one (<see cref="SetNormalMap"/>) is drawn lit with its
/// relief baked into the texture: creases, seams and embroidery darken by how far their normal leans from the surface,
/// with a slight highlight on the edges facing up and left.
/// </summary>
public sealed class KitViewer : Border
{
    /// <summary>The player skeleton and its idle, loaded with the first kit (it's the same for every kit).</summary>
    public static PlayerIdle Rig { get; set; }

    private readonly Viewport3D _view = new() { ClipToBounds = true };
    private readonly PerspectiveCamera _camera = new() { FieldOfView = 32, NearPlaneDistance = 0.05, FarPlaneDistance = 50 };
    private readonly Model3DGroup _models = new();
    private static readonly ConditionalWeakTable<MeshPart, NormalMap> Normals = new();
    private static readonly ConditionalWeakTable<ImageSource, Baked> Reliefs = new();
    private sealed record NormalMap(byte[] Rgba, int Width, int Height);
    private sealed record Baked(NormalMap Normal, BitmapSource Image);
    private readonly ModelVisual3D _lights = new();
    private readonly CheckBox _idleSwitch, _lightSwitch;
    private readonly List<(MeshPart Part, MeshGeometry3D Mesh, GeometryModel3D Model, Brush Brush, Brush Lit)> _parts = new();
    private Point3D _target = new(0, 1.05, 0);
    private double _yaw, _pitch = 4, _distance = 3.6, _baseDistance = 3.6;
    private Point? _drag;
    private bool _picking;

    /// <summary>
    /// Ctrl+drag on the model: called with the part and the texture point (U, V; V = 0 at the top) under the mouse.
    /// Returning false lets the drag turn the view as usual.
    /// </summary>
    public Func<MeshPart, Point, bool> Pick { get; set; }
    private readonly DateTime _start = DateTime.UtcNow;
    private TimeSpan _lastFrame;

    public KitViewer()
    {
        _view.Camera = _camera;
        // mipmapped, so the 2048 textures stay clean when the model is small and sharp when zoomed in
        RenderOptions.SetBitmapScalingMode(_view, BitmapScalingMode.HighQuality);
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(58, 62, 70)));
        // directions as seen from the camera at the front (it looks along -Z; +X is screen right)
        lights.Children.Add(new DirectionalLight(Color.FromRgb(236, 228, 214), new Vector3D(0.55, -0.55, -1)));   // key: upper left
        lights.Children.Add(new DirectionalLight(Color.FromRgb(70, 84, 108), new Vector3D(-1, -0.15, -0.5)));    // fill: right
        lights.Children.Add(new DirectionalLight(Color.FromRgb(120, 132, 156), new Vector3D(-0.2, -0.45, 1)));  // rim: behind, above
        lights.Children.Add(new DirectionalLight(Color.FromRgb(34, 36, 42), new Vector3D(0, 1, -0.2)));         // bounce from the floor
        _lights.Content = lights;
        _view.Children.Add(_lights);
        _view.Children.Add(new ModelVisual3D { Content = _models });

        _idleSwitch = Switch("Idle", "Play the game's menu idle (off: the bind pose the garments are modelled in)");
        _lightSwitch = Switch("Lighting", "Studio lighting (off: the textures drawn flat, with no shading)");
        _idleSwitch.Click += (_, _) => { Pose(); UpdateLoop(); };
        _lightSwitch.Click += (_, _) => ApplyMaterials();
        var bar = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 18, 0) };
        bar.Children.Add(_idleSwitch);
        bar.Children.Add(_lightSwitch);
        var grid = new Grid();
        grid.Children.Add(_view);
        grid.Children.Add(bar);
        Child = grid;
        Background = Brushes.Transparent;
        Cursor = Cursors.SizeAll;
        IsVisibleChanged += (_, _) => UpdateLoop();
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
        Loaded += (_, _) => UpdateLoop();
        UpdateCamera();
    }

    private CheckBox Switch(string text, string tip)
    {
        var c = new CheckBox { Content = text, IsChecked = true, ToolTip = tip, Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand };
        if (TryFindResource("Switch") is Style s) c.Style = s;
        else Loaded += (_, _) => { if (TryFindResource("Switch") is Style st) c.Style = st; };
        return c;
    }

    public bool Animating => _idleSwitch.IsChecked == true && Rig != null;

    /// <summary>Turns the idle on or off (scripted shots).</summary>
    public void SetIdle(bool on) { _idleSwitch.IsChecked = on; Pose(); UpdateLoop(); }

    /// <summary>Turns the lighting on or off (scripted shots).</summary>
    public void SetLighting(bool on) { _lightSwitch.IsChecked = on; ApplyMaterials(); }

    /// <summary>Shows these parts (each with its texture, or grey without one) and frames them.</summary>
    public void Show(IEnumerable<(MeshPart Part, ImageSource Texture)> parts)
    {
        _models.Children.Clear();
        _parts.Clear();
        double minY = double.MaxValue, maxY = double.MinValue, maxR = 0;
        foreach (var (part, tex) in parts)
        {
            var mesh = new MeshGeometry3D();
            var pos = new Point3DCollection(part.VertexCount);
            var nrm = new Vector3DCollection(part.VertexCount);
            var uv = new PointCollection(part.VertexCount);
            for (int v = 0; v < part.VertexCount; v++)
            {
                double x = part.Positions[v * 3], y = part.Positions[v * 3 + 1], z = part.Positions[v * 3 + 2];
                pos.Add(new Point3D(x, y, z));
                nrm.Add(new Vector3D(part.Normals[v * 3], part.Normals[v * 3 + 1], part.Normals[v * 3 + 2]));
                if (part.Uvs != null) uv.Add(new Point(part.Uvs[v * 2], part.Uvs[v * 2 + 1]));
                // the stick reaches out sideways: it isn't part of the framing
                if (part.Material is "mat_shaft" or "mat_blade") continue;
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                maxR = Math.Max(maxR, Math.Sqrt(x * x + z * z));
            }
            var ind = new Int32Collection(part.Indices.Length);
            foreach (int i in part.Indices) ind.Add(i);
            mesh.Positions = pos;
            mesh.Normals = nrm;
            if (part.Uvs != null) mesh.TextureCoordinates = uv;
            mesh.TriangleIndices = ind;
            // skinned parts get new positions every frame; the rest never change
            if (part.Bones == null) mesh.Freeze();

            Brush brush = tex != null && part.Uvs != null
                ? new ImageBrush(tex) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1), TileMode = TileMode.Tile }
                : new SolidColorBrush(Color.FromRgb(150, 156, 168));
            brush.Freeze();
            Brush lit = brush;
            if (brush is ImageBrush && tex is BitmapSource bmp && Normals.TryGetValue(part, out var normal))
            {
                lit = new ImageBrush(Relief(bmp, normal)) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1), TileMode = TileMode.Tile };
                lit.Freeze();
            }
            var model = new GeometryModel3D { Geometry = mesh };
            _models.Children.Add(model);
            _parts.Add((part, mesh, model, brush, lit));
        }
        ApplyMaterials();
        Pose();
        if (minY <= maxY)
        {
            double h = maxY - minY;
            double fit = Math.Max(h, maxR * 2) / 2 / Math.Tan(_camera.FieldOfView * Math.PI / 360) * 1.35;
            // the same model again (a redraw after a texture or lettering change) keeps the view
            if (Math.Abs(fit - _baseDistance) > 1e-6)
            {
                _target = new Point3D(0, (minY + maxY) / 2, 0);
                _baseDistance = _distance = fit;
            }
        }
        UpdateCamera();
        UpdateLoop();
    }

    /// <summary>Lit: the texture with a soft sheen. Unlit: the texture as emitted colour over black, so no light changes it.</summary>
    private void ApplyMaterials()
    {
        bool lit = _lightSwitch.IsChecked == true;
        foreach (var (_, _, model, brush, relief) in _parts)
        {
            var material = new MaterialGroup();
            if (lit)
            {
                material.Children.Add(new DiffuseMaterial(relief));
                material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(56, 255, 250, 240)), 24));
            }
            else
            {
                material.Children.Add(new DiffuseMaterial(Brushes.Black));
                material.Children.Add(new EmissiveMaterial(brush));
            }
            material.Freeze();
            model.Material = material;
            model.BackMaterial = lit ? new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(40, 44, 52))) : material;
        }
    }

    /// <summary>Gives a part its tangent-space normal map (RGBA, X and Y in red and green), drawn into its lit texture.</summary>
    public static void SetNormalMap(MeshPart part, byte[] rgba, int width, int height) =>
        Normals.AddOrUpdate(part, new NormalMap(rgba, width, height));

    /// <summary>The texture with the normal map's relief multiplied in (kept per texture, so a redraw doesn't bake again).</summary>
    private static BitmapSource Relief(BitmapSource tex, NormalMap n)
    {
        if (Reliefs.TryGetValue(tex, out var done) && done.Normal == n) return done.Image;
        var src = tex.Format == PixelFormats.Bgra32 ? tex : new FormatConvertedBitmap(tex, PixelFormats.Bgra32, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight;
        var px = new byte[w * h * 4];
        src.CopyPixels(px, w * 4, 0);
        const float Cavity = 0.6f, Edge = 0.6f;
        float lx = -0.3f, ly = 0.35f, lz = 1f, ll = MathF.Sqrt(lx * lx + ly * ly + lz * lz);
        lx /= ll; ly /= ll; lz /= ll;
        Parallel.For(0, h, y =>
        {
            int ny = Math.Min(n.Height - 1, (int)((long)y * n.Height / h));
            for (int x = 0; x < w; x++)
            {
                int nx = Math.Min(n.Width - 1, (int)((long)x * n.Width / w));
                int ni = (ny * n.Width + nx) * 4;
                // X and Y from red and green; Z rebuilt (the game's normal maps are BC5, two channels)
                float vx = n.Rgba[ni] / 127.5f - 1, vy = n.Rgba[ni + 1] / 127.5f - 1;
                float vz = MathF.Sqrt(MathF.Max(0, 1 - vx * vx - vy * vy));
                float shade = (1 - Cavity * (1 - vz * vz * vz)) * (1 + Edge * (vx * lx + vy * ly + vz * lz - lz));
                int i = (y * w + x) * 4;
                for (int c = 0; c < 3; c++) px[i + c] = (byte)Math.Clamp(px[i + c] * shade, 0, 255);
            }
        });
        var baked = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        baked.Freeze();
        Reliefs.AddOrUpdate(tex, new Baked(n, baked));
        return baked;
    }

    private void UpdateLoop()
    {
        CompositionTarget.Rendering -= OnFrame;
        if (Animating && IsVisible && _parts.Any(p => p.Part.Bones != null)) CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object sender, EventArgs e)
    {
        // the clip is 30 fps: skinning more often than that only costs time
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now - _lastFrame < TimeSpan.FromMilliseconds(30)) return;
        _lastFrame = now;
        Pose();
    }

    /// <summary>Skins every part to the idle at the current time, or puts them back in the bind pose.</summary>
    private void Pose()
    {
        var rig = Rig;
        bool on = _idleSwitch.IsChecked == true && rig != null;
        var skin = on ? rig.Skin((DateTime.UtcNow - _start).TotalSeconds) : null;
        int head = on ? rig.Skeleton.IndexOf("Head") : -1;
        foreach (var (part, mesh, _, _, _) in _parts)
        {
            if (part.Bones == null) continue;
            var p = on ? PoseSkin.Positions(part, skin, head) : part.Positions;
            var n = on ? PoseSkin.Normals(part, skin, head) : part.Normals;
            var pos = new Point3DCollection(part.VertexCount);
            var nrm = new Vector3DCollection(part.VertexCount);
            for (int v = 0; v < part.VertexCount; v++)
            {
                pos.Add(new Point3D(p[v * 3], p[v * 3 + 1], p[v * 3 + 2]));
                nrm.Add(new Vector3D(n[v * 3], n[v * 3 + 1], n[v * 3 + 2]));
            }
            pos.Freeze();
            nrm.Freeze();
            mesh.Positions = pos;
            mesh.Normals = nrm;
        }
    }

    /// <summary>Turns the model to a preset: 0 front, 180 back, 90 side.</summary>
    public void Face(double yaw)
    {
        _yaw = yaw;
        _pitch = 4;
        _distance = _baseDistance;
        UpdateCamera();
    }

    /// <summary>Moves in <paramref name="factor"/> times closer, looking <paramref name="up"/> above the centre (scripted shots).</summary>
    public void Zoom(double factor, double up)
    {
        _target = new Point3D(_target.X, _target.Y + up, _target.Z);
        _distance = _baseDistance / factor;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        double yaw = _yaw * Math.PI / 180, pitch = _pitch * Math.PI / 180;
        var dir = new Vector3D(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
        _camera.Position = _target + dir * _distance;
        _camera.LookDirection = -dir;
        _camera.UpDirection = new Vector3D(0, 1, 0);
        // the lights go round with the camera: pitch about X, then yaw about Y
        var turn = new Transform3DGroup();
        turn.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), -_pitch)));
        turn.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), _yaw)));
        _lights.Transform = turn;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && IsSwitch(d)) return;
        if (e.ClickCount == 2) { Face(0); return; }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Pick != null && HitUv(e.GetPosition(_view)) is { } hit && Pick(hit.Part, hit.Uv))
        {
            _picking = true;
            CaptureMouse();
            return;
        }
        _drag = e.GetPosition(this);
        CaptureMouse();
    }

    /// <summary>The nearest textured part under a point of the view, and the texture point there.</summary>
    private (MeshPart Part, Point Uv)? HitUv(Point at)
    {
        (MeshPart, Point)? found = null;
        VisualTreeHelper.HitTest(_view, null, r =>
        {
            if (r is not RayMeshGeometry3DHitTestResult m) return HitTestResultBehavior.Continue;
            int k = _parts.FindIndex(x => x.Mesh == m.MeshHit);
            var t = m.MeshHit.TextureCoordinates;
            if (k < 0 || t == null || t.Count == 0) return HitTestResultBehavior.Continue;
            Point a = t[m.VertexIndex1], b = t[m.VertexIndex2], c = t[m.VertexIndex3];
            found = (_parts[k].Part, new Point(a.X * m.VertexWeight1 + b.X * m.VertexWeight2 + c.X * m.VertexWeight3,
                                               a.Y * m.VertexWeight1 + b.Y * m.VertexWeight2 + c.Y * m.VertexWeight3));
            return HitTestResultBehavior.Stop;
        }, new PointHitTestParameters(at));
        return found;
    }

    private bool IsSwitch(DependencyObject d)
    {
        for (; d != null && d != this; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d == _idleSwitch || d == _lightSwitch) return true;
        return false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_picking)
        {
            if (e.LeftButton == MouseButtonState.Pressed && HitUv(e.GetPosition(_view)) is { } hit) Pick?.Invoke(hit.Part, hit.Uv);
            return;
        }
        if (_drag is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        _yaw -= (p.X - start.X) * 0.5;
        _pitch = Math.Clamp(_pitch + (p.Y - start.Y) * 0.3, -60, 60);
        _drag = p;
        UpdateCamera();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _drag = null;
        _picking = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _distance = Math.Clamp(_distance * (e.Delta > 0 ? 0.88 : 1.14), _baseDistance * 0.3, _baseDistance * 2.5);
        UpdateCamera();
    }
}
