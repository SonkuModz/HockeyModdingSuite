using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NHL27Kit.App;

/// <summary>
/// A 3D view of an arena: drag to orbit, right-drag to pan, scroll to zoom, double-click for the broadcast view.
/// Frostbite is Y-up in metres; the rink is centred on the origin with its length along Z.
/// </summary>
public sealed class ArenaViewer : Border
{
    public enum View { Broadcast, End, Overhead, Ice, Bowl }

    private readonly Viewport3D _view = new() { ClipToBounds = true };
    private readonly PerspectiveCamera _camera = new() { FieldOfView = 50, NearPlaneDistance = 0.3, FarPlaneDistance = 4000 };
    private readonly ModelVisual3D _scene = new();
    private Point3D _target;
    private double _yaw, _pitch, _distance;
    private Point? _drag;
    private bool _panning;

    public ArenaViewer()
    {
        _view.Camera = _camera;
        RenderOptions.SetBitmapScalingMode(_view, BitmapScalingMode.HighQuality);
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(130, 130, 136)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(170, 168, 160), new Vector3D(0.2, -1, 0.35)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(70, 74, 84), new Vector3D(-0.4, -0.3, -0.8)));
        _view.Children.Add(new ModelVisual3D { Content = lights });
        _view.Children.Add(_scene);
        Child = _view;
        Background = new SolidColorBrush(Color.FromRgb(12, 14, 18));
        Cursor = Cursors.SizeAll;
        Go(View.Broadcast);
    }

    /// <summary>Shows a built scene (frozen, so it can be made off the UI thread).</summary>
    public void Show(Model3D scene) => _scene.Content = scene;

    public void Clear() => _scene.Content = null;

    /// <summary>Moves the camera to a preset.</summary>
    public void Go(View view)
    {
        (_target, _yaw, _pitch, _distance) = view switch
        {
            // inside the bowl, under the roof (about 26 m up)
            View.End => (new Point3D(0, 0, 0), 0, 17, 50),
            View.Overhead => (new Point3D(0, 0, 0), 90, 88, 85),
            View.Ice => (new Point3D(0, 1.2, 8), 90, 4, 18),
            View.Bowl => (new Point3D(0, 4, 0), 40, 28, 46),
            _ => (new Point3D(0, 0, 0), 90, 26, 34),
        };
        UpdateCamera();
    }

    /// <summary>A camera set by numbers (scripted shots): yaw and pitch in degrees, distance in metres.</summary>
    public void Look(double yaw, double pitch, double distance, double height = double.NaN)
    {
        _yaw = yaw;
        _pitch = Math.Clamp(pitch, -10, 89);
        _distance = Math.Clamp(distance, 2, 800);
        if (!double.IsNaN(height)) _target = new Point3D(_target.X, height, _target.Z);
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        double yaw = _yaw * Math.PI / 180, pitch = _pitch * Math.PI / 180;
        var dir = new Vector3D(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
        _camera.Position = _target + dir * _distance;
        _camera.LookDirection = -dir;
        // straight down needs another up than +Y
        _camera.UpDirection = _pitch > 85 ? new Vector3D(-Math.Sin(yaw), 0, -Math.Cos(yaw)) : new Vector3D(0, 1, 0);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { Go(View.Broadcast); return; }
        Begin(e, false);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) => Begin(e, true);

    private void Begin(MouseButtonEventArgs e, bool pan)
    {
        _drag = e.GetPosition(this);
        _panning = pan;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag is not { } start || (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)) return;
        var p = e.GetPosition(this);
        double dx = p.X - start.X, dy = p.Y - start.Y;
        if (_panning)
        {
            // along the ground, at a speed that matches the distance
            double yaw = _yaw * Math.PI / 180, k = _distance * 0.0015;
            var right = new Vector3D(Math.Cos(yaw), 0, -Math.Sin(yaw));
            var forward = new Vector3D(-Math.Sin(yaw), 0, -Math.Cos(yaw));
            _target += right * (-dx * k) + forward * (dy * k);
        }
        else
        {
            _yaw -= dx * 0.35;
            _pitch = Math.Clamp(_pitch + dy * 0.25, -10, 89);
        }
        _drag = p;
        UpdateCamera();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => End();
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) { End(); e.Handled = true; }

    private void End()
    {
        _drag = null;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _distance = Math.Clamp(_distance * (e.Delta > 0 ? 0.87 : 1.15), 2, 800);
        UpdateCamera();
    }
}
