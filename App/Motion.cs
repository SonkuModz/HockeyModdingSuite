using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NHL27Kit.App;

/// <summary>The kit's small animations: pages and tabs gliding in, the startup screen fading away.</summary>
public static class Motion
{
    private static readonly IEasingFunction Glide = new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>Fades <paramref name="el"/> in while it slides the last few pixels into place.</summary>
    public static void Enter(FrameworkElement el, double dx = 0, double dy = 14, int ms = 260)
    {
        if (el == null) return;
        if (el.RenderTransform is not TranslateTransform move)
            el.RenderTransform = move = new TranslateTransform();
        var time = TimeSpan.FromMilliseconds(ms);
        el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, time) { EasingFunction = Glide });
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, time) { EasingFunction = Glide });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, time) { EasingFunction = Glide });
    }

    /// <summary>Fades <paramref name="el"/> out (scaling up a touch), then collapses it.</summary>
    public static void Leave(FrameworkElement el, int ms = 450)
    {
        if (el == null || el.Visibility != Visibility.Visible) return;
        var time = TimeSpan.FromMilliseconds(ms);
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform();
        el.RenderTransform = scale;
        var fade = new DoubleAnimation(1, 0, time) { EasingFunction = Glide };
        fade.Completed += (_, _) => el.Visibility = Visibility.Collapsed;
        el.IsHitTestVisible = false;
        el.BeginAnimation(UIElement.OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.04, time) { EasingFunction = Glide });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.04, time) { EasingFunction = Glide });
    }
}
