using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>A waveform of bars: played bars in the accent colour, the rest dim. Click or drag to seek.</summary>
public sealed class WaveformView : FrameworkElement
{
    private float[] _peaks;
    private double _progress;

    public event Action<double> Seek;

    public float[] Peaks
    {
        set { _peaks = value; InvalidateVisual(); }
    }

    public double Progress
    {
        set
        {
            if (Math.Abs(value - _progress) < 0.0005) return;
            _progress = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        var played = (Brush)TryFindResource("Accent") ?? Brushes.DodgerBlue;
        var rest = (Brush)TryFindResource("LineStrong") ?? Brushes.Gray;
        const double bar = 3, gap = 2;
        if (_peaks == null || _peaks.Length == 0 || w < bar)
        {
            dc.DrawRectangle(rest, null, new Rect(0, h / 2 - 0.5, w, 1));
            return;
        }
        int n = Math.Max(1, (int)(w / (bar + gap)));
        double x = (w - n * (bar + gap) + gap) / 2;
        for (int i = 0; i < n; i++)
        {
            int from = (int)((long)i * _peaks.Length / n), to = Math.Max(from + 1, (int)((long)(i + 1) * _peaks.Length / n));
            float peak = 0;
            for (int k = from; k < to && k < _peaks.Length; k++)
                peak = Math.Max(peak, _peaks[k]);
            // a gentle curve so quiet parts still show
            double bh = Math.Max(3, Math.Pow(peak, 0.65) * (h - 4));
            bool isPlayed = (i + 0.5) / n <= _progress;
            dc.DrawRoundedRectangle(isPlayed ? played : rest, null, new Rect(x + i * (bar + gap), (h - bh) / 2, bar, bh), 1.5, 1.5);
        }
    }

    private void Raise(MouseEventArgs e)
    {
        if (ActualWidth > 0)
            Seek?.Invoke(Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_peaks == null) return;
        CaptureMouse();
        Raise(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured) Raise(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
    }
}

/// <summary>
/// Plays one decoded sound: a waveform with the play position, play/pause, time and volume. The sound is written as a
/// stereo WAV in the temp folder and played with WPF's MediaPlayer.
/// </summary>
public partial class SoundPlayerView : UserControl
{
    public static string TempDir { get; } = Path.Combine(Path.GetTempPath(), "NHL27Kit", "play");

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private Sound _sound;
    private string _file;
    private bool _playing;
    private double _duration;

    public SoundPlayerView()
    {
        InitializeComponent();
        _player.Volume = Volume.Value;
        _tick.Tick += (_, _) => Update();
        _player.MediaEnded += (_, _) => Stop();
        _player.MediaFailed += (_, e) => Clear("Playback failed: " + (e.ErrorException?.Message ?? "unknown error"));
        Wave.Seek += OnSeek;
    }

    public bool IsPlaying => _playing;

    /// <summary>Removes leftover files from earlier runs.</summary>
    public static void CleanTemp()
    {
        try { if (Directory.Exists(TempDir)) Directory.Delete(TempDir, recursive: true); }
        catch { /* a file may still be in use */ }
    }

    /// <summary>Empties the player and shows a note where the waveform would be.</summary>
    public void Clear(string note = "")
    {
        Stop();
        Release();
        _sound = null;
        Wave.Peaks = null;
        WaveNote.Text = note;
        PlayBtn.IsEnabled = false;
        TimeText.Text = "0:00 / 0:00";
        InfoText.Text = "";
    }

    public async Task SetSoundAsync(Sound sound, string info = null)
    {
        Stop();
        Release();
        _sound = sound;
        _duration = sound.Seconds;
        WaveNote.Text = "";
        PlayBtn.IsEnabled = false;
        var (peaks, wav) = await Task.Run(() => (sound.Peaks(480), sound.ToWav(stereo: true)));
        if (_sound != sound) return;
        string file = Path.Combine(TempDir, Guid.NewGuid().ToString("N") + ".wav");
        Directory.CreateDirectory(TempDir);
        await File.WriteAllBytesAsync(file, wav);
        if (_sound != sound)
        {
            try { File.Delete(file); } catch { }
            return;
        }
        _file = file;
        Wave.Peaks = peaks;
        Wave.Progress = 0;
        _player.Open(new Uri(file));
        PlayBtn.IsEnabled = true;
        InfoText.Text = info ?? $"{sound.Channels} ch · {sound.SampleRate / 1000.0:0.#} kHz" + (sound.Channels > 2 ? " · played as a stereo mix" : "");
        Update();
    }

    public void Toggle()
    {
        if (_sound == null) return;
        if (_playing)
        {
            _player.Pause();
            _playing = false;
            _tick.Stop();
        }
        else
        {
            if (_player.Position.TotalSeconds >= _duration - 0.05)
                _player.Position = TimeSpan.Zero;
            _player.Play();
            _playing = true;
            _tick.Start();
        }
        PlayBtn.Content = _playing ? "" : "";
    }

    /// <summary>Stops playback and rewinds.</summary>
    public void Stop()
    {
        _tick.Stop();
        _playing = false;
        PlayBtn.Content = "";
        try { _player.Stop(); } catch { /* nothing open */ }
        Wave.Progress = 0;
        TimeText.Text = $"0:00 / {Fmt.Clock(_duration)}";
    }

    private void Release()
    {
        _player.Close();
        if (_file == null) return;
        try { File.Delete(_file); } catch { /* removed on the next start */ }
        _file = null;
    }

    private void Update()
    {
        double pos = Math.Min(_duration, _player.Position.TotalSeconds);
        Wave.Progress = _duration > 0 ? pos / _duration : 0;
        TimeText.Text = $"{Fmt.Clock(pos)} / {Fmt.Clock(_duration)}";
    }

    private void OnSeek(double fraction)
    {
        if (_sound == null) return;
        _player.Position = TimeSpan.FromSeconds(fraction * _duration);
        Update();
    }

    private void OnToggle(object sender, RoutedEventArgs e) => Toggle();

    private void OnVolume(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player != null) _player.Volume = e.NewValue;
    }
}
