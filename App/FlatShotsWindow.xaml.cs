using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NHL27Kit.Core;
using Path = System.IO.Path;

namespace NHL27Kit.App;

/// <summary>Jersey from a front and a back flat shot (<see cref="JerseyFlat"/>): pick the two, and the whole design is worked out with no marking.</summary>
public partial class FlatShotsWindow : Window
{
    private readonly StudioSource _src;
    private FlatShot _front, _back;

    public FlatResult Result { get; private set; }

    public FlatShotsWindow(StudioSource src)
    {
        InitializeComponent();
        _src = src;
    }

    private void OnPickFront(object sender, RoutedEventArgs e)
    {
        if (Pick("The jersey's flat front shot") is { } file) OpenFront(file);
    }

    private void OnPickBack(object sender, RoutedEventArgs e)
    {
        if (Pick("The jersey's flat back shot") is { } file) OpenBack(file);
    }

    private string Pick(string title)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = "PNG images|*.png|All files|*.*" };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    public void OpenFront(string file) => _front = Open(file, FrontImage) ?? _front;
    public void OpenBack(string file) => _back = Open(file, BackImage) ?? _back;

    private FlatShot Open(string file, System.Windows.Controls.Image into)
    {
        FlatShot shot = null;
        try
        {
            var (rgba, w, h) = ImageLoader.Load(file);
            shot = FlatShot.From(rgba, w, h);
            if (shot == null)
                Status.Text = $"{Path.GetFileName(file)} has no transparent background, so it can't be lined up. For any other photo use From a photo.";
            else
            {
                var bgra = (byte[])rgba.Clone();
                for (int i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
                var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
                bmp.Freeze();
                into.Source = bmp;
            }
        }
        catch (Exception ex)
        {
            Status.Text = $"Can't open {Path.GetFileName(file)}: {ex.Message}";
        }
        if (shot != null) Status.Text = _front != null || _back != null ? "Ready." : "Pick the other shot too.";
        UseButton.IsEnabled = shot != null && (ReferenceEquals(into, FrontImage) ? _back != null : _front != null);
        return shot;
    }

    private async void OnUse(object sender, RoutedEventArgs e)
    {
        UseButton.IsEnabled = false;
        Status.Text = "Working it out…";
        if (await Run()) { DialogResult = true; Close(); }
        else UseButton.IsEnabled = true;
    }

    /// <summary>Converts the two shots; false, with the reason shown, when it can't.</summary>
    public async Task<bool> Run()
    {
        try
        {
            FlatShot front = _front, back = _back;
            Result = await Task.Run(() => JerseyFlat.Convert(_src, front, back));
            return true;
        }
        catch (Exception ex)
        {
            Status.Text = $"Couldn't convert them: {ex.Message}";
            return false;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
