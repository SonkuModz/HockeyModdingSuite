using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// Plays NHL 24 on NHL24 PC (LEVASTAK's PS4 port): Play opens its own launcher (tools\launcher.ps1, the same thing its
/// desktop shortcut runs), which patches the eboot for the settings picked there and starts nhl24_new.exe. The page
/// only reads NHL24 PC's files: its settings, play time, log, and the game folder it runs.
/// </summary>
public partial class Play24Page : UserControl
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private Settings _settings;
    private string _gameDir, _titleId = "CUSA37933", _install;
    private DateTime? _since;

    /// <summary>The emulator's processes (the launcher starts nhl24_new.exe; older installs have nhl24.exe).</summary>
    private static readonly string[] GameProcesses = { "nhl24_new", "nhl24" };

    public event Action<string, string> StateChanged;
    public event Action<string> StatusChanged;

    public Play24Page()
    {
        InitializeComponent();
        _tick.Tick += (_, _) => OnTick();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { ShowInstall(); _tick.Start(); }
            else if (_since == null) _tick.Stop();
        };
    }

    public void Init(Settings settings, string gameDir, string titleId, string title, string version)
    {
        _settings = settings;
        _gameDir = gameDir;
        _titleId = titleId ?? _titleId;
        HeroTitle.Text = string.IsNullOrEmpty(title) ? "NHL 24" : title;
        HeroMeta.Text = $"{_titleId}  ·  v{version}";
        HeroArt.ImageSource = Art(Path.Combine(gameDir ?? "", "sce_sys", "pic1.png"), 1800) ?? Art(Path.Combine(gameDir ?? "", "sce_sys", "pic0.png"), 1800);
        IconArt.ImageSource = Art(Path.Combine(gameDir ?? "", "sce_sys", "icon0.png"), 256);
        ShowInstall();
    }

    private static BitmapImage Art(string file, int width)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(file);
            bmp.DecodePixelWidth = width;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    // ================================================================ the NHL24 PC install

    private static bool IsInstall(string dir) =>
        !string.IsNullOrEmpty(dir) && (File.Exists(Path.Combine(dir, "nhl24_new.exe")) || File.Exists(Path.Combine(dir, "nhl24.exe")));

    /// <summary>The NHL24 PC folder: the one chosen, else a Games folder on any drive that has it.</summary>
    private string FindInstall()
    {
        if (IsInstall(_settings?.Nhl24Pc)) return _settings.Nhl24Pc;
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            foreach (string games in new[] { "Games", "Emulators" }.Select(g => Path.Combine(drive.RootDirectory.FullName, g)).Where(Directory.Exists))
            {
                string[] dirs;
                try { dirs = Directory.GetDirectories(games); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                if (dirs.FirstOrDefault(IsInstall) is { } found) return found;
            }
        return null;
    }

    private string User(string name) => Path.Combine(_install ?? "", "user", name);

    private void ShowInstall()
    {
        _install = FindInstall();
        InstallPath.Text = _install ?? "NHL24 PC not found";
        bool launcher = _install != null && File.Exists(Path.Combine(_install, "tools", "launcher.ps1"));
        InstallInfo.Text = _install == null ? "Choose the NHL24 PC folder, the one with nhl24_new.exe."
            : launcher ? "Play opens its launcher, which applies the settings below and starts the game."
            : "No tools\\launcher.ps1 here: Play starts the emulator straight away.";
        ShowPlaytime();
        ShowLauncherSettings();
        ShowRunsGame();
        OnTick();
    }

    /// <summary>user\play_time.txt: a line per game, "CUSA37933 4:55:00 1792904838" (time played, last played).</summary>
    private void ShowPlaytime()
    {
        TimeSpan played = TimeSpan.Zero;
        DateTime? last = null;
        try
        {
            string line = File.Exists(User("play_time.txt"))
                ? File.ReadLines(User("play_time.txt")).FirstOrDefault(l => l.Trim().StartsWith(_titleId, StringComparison.OrdinalIgnoreCase)) : null;
            string[] parts = line?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts?.Length >= 2 && parts[1].Split(':') is { Length: 3 } hms
                && int.TryParse(hms[0], out int h) && int.TryParse(hms[1], out int m) && int.TryParse(hms[2], out int s))
                played = new TimeSpan(h, m, s);
            if (parts?.Length >= 3 && long.TryParse(parts[2], out long unix) && unix > 0)
                last = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
        }
        catch (IOException) { }
        string text = played == TimeSpan.Zero ? "Not played yet" : played.TotalHours >= 1 ? $"{(int)played.TotalHours}h {played.Minutes}m played" : $"{played.Minutes}m played";
        Playtime.Text = last is { } d ? $"{text}  ·  last played {d:MMM d, h:mm tt}" : text;
    }

    private sealed record LauncherRow(string Label, string Value);

    /// <summary>user\launcher_settings.json, as the launcher shows it.</summary>
    private void ShowLauncherSettings()
    {
        JsonObject s = null;
        try { s = File.Exists(User("launcher_settings.json")) ? JsonNode.Parse(File.ReadAllText(User("launcher_settings.json")))?.AsObject() : null; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException) { }
        if (s == null)
        {
            LauncherRows.ItemsSource = null;
            LauncherCaption.Text = _install == null ? "" : "No launcher settings yet: they're saved the first time you play from the launcher.";
            return;
        }
        string Get(string key) => s[key] is JsonValue v ? v.ToString() : null;
        string OnOff(string key) => Get(key) switch { "true" => "On", "false" => "Off", var x => x ?? "–" };
        string Res(string r) => r switch { "1080" => "1920 × 1080", "1200" => "1920 × 1200", "1440" => "2560 × 1440", "1600" => "2560 × 1600", "2160" => "3840 × 2160", _ => r ?? "–" };
        string Cap(string x) => string.IsNullOrEmpty(x) ? "–" : char.ToUpperInvariant(x[0]) + x[1..];
        LauncherRows.ItemsSource = new List<LauncherRow>
        {
            new("Resolution", Res(Get("resolution"))),
            new("Fullscreen", OnOff("fullscreen")),
            new("HDR", Cap(Get("hdr"))),
            new("VSync", OnOff("vsync")),
            new("Frame rate cap", Get("fps") is { } fps ? fps + " FPS" : "–"),
            new("Menus at full frame rate", OnOff("menu_full_fps")),
            new("PS4 Pro mode", OnOff("ps4pro")),
            new("16× anisotropic filtering", OnOff("aniso16")),
            new("Sharpness", Cap(Get("sharpness"))),
            new("FSR", OnOff("fsr")),
            new("Red zone protection", OnOff("redzone")),
        };
        LauncherCaption.Text = User("launcher_settings.json");
    }

    /// <summary>
    /// The game folder NHL24 PC runs: game-UPDATE when there is one, else game (often a link to the extracted dump).
    /// Mods only reach the game when that's the folder HMS writes to.
    /// </summary>
    private void ShowRunsGame()
    {
        RunsWarning.Visibility = Visibility.Collapsed;
        if (_install == null)
        {
            RunsText.Text = "–";
            return;
        }
        string runs = new[] { "game-UPDATE", "game" }.Select(d => Path.Combine(_install, d)).FirstOrDefault(d => File.Exists(Path.Combine(d, "eboot.bin")));
        if (runs == null)
        {
            // game is usually a link to the extracted dump: say where it points when that's gone
            string game = Path.Combine(_install, "game");
            string target = null;
            try { target = new DirectoryInfo(game).LinkTarget; } catch (IOException) { }
            if (target != null)
            {
                target = target.StartsWith(@"\??\") ? target[4..] : target;
                RunsText.Text = $"{game}\n→ {target}";
                RunsWarning.Text = "That folder doesn't exist any more (moved or renamed?), so NHL24 PC can't start the game. Point the game link at your NHL 24 folder again.";
            }
            else
            {
                RunsText.Text = $"No game in {game}";
                RunsWarning.Text = "NHL24 PC has no game to start: put the extracted NHL 24 folder (or a link to it) there as game.";
            }
            RunsWarning.Visibility = Visibility.Visible;
            return;
        }
        string real = Resolve(runs);
        RunsText.Text = real.Equals(runs, StringComparison.OrdinalIgnoreCase) ? runs : $"{runs}\n→ {real}";
        if (KitPaths.IsGame(_gameDir) && !Same(real, _gameDir))
        {
            RunsWarning.Text = $"That isn't the folder HMS has open ({_gameDir}), so changes made here won't show in this game.";
            RunsWarning.Visibility = Visibility.Visible;
        }
    }

    /// <summary>A folder with its links (junctions) followed.</summary>
    private static string Resolve(string dir)
    {
        try
        {
            var target = new DirectoryInfo(dir).ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? Path.GetFullPath(dir);
        }
        catch (IOException) { return Path.GetFullPath(dir); }
    }

    private static bool Same(string a, string b) =>
        Resolve(a).TrimEnd('\\', '/').Equals(Resolve(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private void OnBrowseInstall(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose the NHL24 PC folder (the one with nhl24_new.exe)", InitialDirectory = _install };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!IsInstall(dlg.FolderName))
        {
            StatusChanged?.Invoke("No nhl24_new.exe in " + dlg.FolderName);
            return;
        }
        _settings.Nhl24Pc = dlg.FolderName;
        _settings.Save();
        ShowInstall();
    }

    // ================================================================ playing

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        PlayProblem.Visibility = Visibility.Collapsed;
        if (_install == null)
        {
            PlayProblem.Text = "Choose the NHL24 PC folder first (the … button next to NHL24 PC).";
            PlayProblem.Visibility = Visibility.Visible;
            return;
        }
        try
        {
            string script = Path.Combine(_install, "tools", "launcher.ps1");
            string exe = new[] { "nhl24_new.exe", "nhl24.exe" }.Select(x => Path.Combine(_install, x)).First(File.Exists);
            var start = File.Exists(script)
                ? new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"") { CreateNoWindow = true }
                : new ProcessStartInfo(exe);
            start.WorkingDirectory = _install;
            start.UseShellExecute = false;
            Process.Start(start)?.Dispose();
            StatusChanged?.Invoke(File.Exists(script) ? "Opened the NHL24 PC launcher: press Play there to start the game" : $"Started {Path.GetFileName(exe)}");
            _tick.Start();
        }
        catch (Exception ex)
        {
            PlayProblem.Text = "Could not start NHL24 PC: " + ex.Message;
            PlayProblem.Visibility = Visibility.Visible;
        }
    }

    private static Process[] Running() => GameProcesses.SelectMany(Process.GetProcessesByName).ToArray();

    private void OnStop(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), "Stop NHL 24? Anything not saved in game is lost.", "HMS",
                            MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        foreach (var p in Running())
            using (p)
                try { p.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    /// <summary>Follows the game's process (started here or anywhere else) and the emulator's log.</summary>
    private void OnTick()
    {
        var found = Running();
        DateTime? started = null;
        foreach (var p in found)
            using (p)
                try { started ??= p.StartTime; } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        bool running = found.Length > 0;
        if (running)
        {
            _since = started ?? _since ?? DateTime.Now;
            var t = DateTime.Now - _since.Value;
            string time = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
            RunningText.Text = $"Running  ·  {time}";
            StateChanged?.Invoke($"Playing  ·  {time}", "Good");
        }
        else if (_since != null)
        {
            _since = null;
            StateChanged?.Invoke("Index ready", "Good");
            StatusChanged?.Invoke("NHL 24 closed");
            ShowPlaytime();
            if (!IsVisible) _tick.Stop();
        }
        PlayButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        StopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        RunningBadge.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        ShowLog();
    }

    private string LogFile => User(Path.Combine("log", "shadps4.log"));

    /// <summary>The end of the emulator's log (it's open for writing while the game runs).</summary>
    private void ShowLog()
    {
        if (_install == null || !File.Exists(LogFile)) return;
        try
        {
            using var fs = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long from = Math.Max(0, fs.Length - 24_000);
            fs.Position = from;
            var bytes = new byte[fs.Length - from];
            int n = fs.Read(bytes, 0, bytes.Length);
            var lines = Encoding.UTF8.GetString(bytes, 0, n).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            if (from > 0 && lines.Count > 0) lines.RemoveAt(0);
            string text = string.Join("\n", lines.TakeLast(120));
            if (text == ConsoleText.Text) return;
            bool atBottom = ConsoleScroll.VerticalOffset >= ConsoleScroll.ScrollableHeight - 4;
            ConsoleText.Text = text;
            ConsoleText.Foreground = (System.Windows.Media.Brush)FindResource("TextDim");
            if (atBottom) ConsoleScroll.ScrollToEnd();
        }
        catch (IOException) { }
    }

    // ================================================================ links

    private void OnOpenInstall(object sender, RoutedEventArgs e)
    {
        if (_install == null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_install}\"") { UseShellExecute = true });
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (File.Exists(LogFile))
            Process.Start(new ProcessStartInfo(LogFile) { UseShellExecute = true });
        else
            StatusChanged?.Invoke("No log yet: play once first.");
    }
}
