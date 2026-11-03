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
/// Plays NHL 24 on NHL24 PC (a shadPS4 build for NHL 24): Play opens the package's launcher (launcher.exe, or
/// tools\launcher.ps1 in older packages), which starts nhl24.exe. The emulator settings are its shadPS4 config,
/// user\config.json, edited here. The page also shows its play time, log and the game folder it runs.
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

    /// <summary>
    /// The NHL24 PC folder: the one chosen, else the newest one next to the game folder (up to three folders above it),
    /// else one in a Games folder on any drive.
    /// </summary>
    private string FindInstall()
    {
        if (App.Args.TryGetValue("p24pc", out var arg)) return IsInstall(arg) ? arg : null;
        if (IsInstall(_settings?.Nhl24Pc)) return _settings.Nhl24Pc;
        var near = new List<string>();
        var dir = KitPaths.IsGame(_gameDir) ? new DirectoryInfo(_gameDir).Parent : null;
        for (int up = 0; dir?.Parent != null && up < 3; dir = dir.Parent, up++)
        {
            try { near.AddRange(Directory.GetDirectories(dir.FullName).Where(IsInstall)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        if (near.Count > 0) return near.OrderByDescending(d => File.GetLastWriteTimeUtc(Exe(d))).First();
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

    /// <summary>The emulator: nhl24_new.exe in older packages, nhl24.exe in newer ones.</summary>
    private static string Exe(string install) =>
        new[] { "nhl24_new.exe", "nhl24.exe" }.Select(x => Path.Combine(install, x)).FirstOrDefault(File.Exists);

    private string User(string name) => Path.Combine(_install ?? "", "user", name);

    private void ShowInstall()
    {
        _install = FindInstall();
        InstallPath.Text = _install ?? "NHL24 PC not found";
        InstallInfo.Text = _install == null ? "Choose the NHL24 PC folder, the one with nhl24.exe."
            : Launcher() != null ? "Play opens its launcher; press Play there to start the game with the settings below."
            : "No launcher here: Play starts the emulator straight away.";
        ShowPlaytime();
        LoadConfig();
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

    // ================================================================ emulator settings (user\config.json)

    private string ConfigFile => User("config.json");
    private JsonObject _config;
    private List<SettingSection> _sections;
    private bool _dirty;

    private static Choice C(string label, string value) => new(label, value);

    private static readonly List<Choice> Sizes = new() { C("1280 × 720", "1280x720"), C("1600 × 900", "1600x900"), C("1920 × 1080", "1920x1080"),
        C("2560 × 1440", "2560x1440"), C("3840 × 2160", "3840x2160") };

    /// <summary>
    /// The settings NHL24 PC's launcher shows, as keys of its shadPS4 config ("GPU.fsr_enabled"). Sizes are two keys each
    /// ("GPU.window_*" is window_width and window_height); the display mode also sets GPU.full_screen.
    /// </summary>
    private void BuildSections()
    {
        _sections = new List<SettingSection>
        {
            new()
            {
                Title = "Display", Glyph = "", Caption = "window and presentation",
                Rows = new()
                {
                    new() { Key = "GPU.full_screen_mode", Label = "Display mode", Kind = RowKind.Choice, Type = RowType.String, Fallback = "Windowed",
                            Choices = new() { C("Windowed", "Windowed"), C("Borderless fullscreen", "Fullscreen (Borderless)"), C("Fullscreen", "Fullscreen") } },
                    new() { Key = "GPU.window_*", Label = "Window size", Hint = "When windowed", Kind = RowKind.Choice, Type = RowType.String, Fallback = "1280x720", Choices = Sizes },
                    new() { Key = "GPU.internal_screen_*", Label = "Resolution", Hint = "The size the game renders and goes fullscreen at", Kind = RowKind.Choice, Type = RowType.String,
                            Fallback = "1920x1080", Choices = Sizes },
                    new() { Key = "GPU.present_mode", Label = "Present mode", Hint = "Mailbox: lowest latency without tearing. Fifo: vsync. Immediate: may tear", Kind = RowKind.Choice,
                            Type = RowType.String, Fallback = "Mailbox", Choices = new() { C("Mailbox", "Mailbox"), C("Fifo (vsync)", "Fifo"), C("Immediate", "Immediate") } },
                    new() { Key = "GPU.vblank_frequency", Label = "Frame rate", Hint = "The emulated display's refresh rate", Kind = RowKind.Choice, Type = RowType.Int, Fallback = "60",
                            Choices = new() { C("60 FPS", "60"), C("120 FPS", "120"), C("144 FPS", "144") } },
                    new() { Key = "GPU.hdr_allowed", Label = "HDR", Hint = "When the display supports it", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "General.show_fps_counter", Label = "FPS counter", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                },
            },
            new()
            {
                Title = "Image", Glyph = "", Caption = "upscaling and sharpening",
                Rows = new()
                {
                    new() { Key = "GPU.fsr_enabled", Label = "FSR upscaling", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                    new() { Key = "GPU.rcas_enabled", Label = "Sharpening", Hint = "FSR's RCAS pass", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                    new() { Key = "GPU.rcas_attenuation", Label = "Sharpening strength", Hint = "Lower is sharper", Kind = RowKind.Slider, Type = RowType.Int, Min = 0, Max = 3000,
                            Fallback = "250", Format = v => (v / 1000).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) },
                },
            },
            new()
            {
                Title = "Game", Glyph = "", Caption = "console and system",
                Rows = new()
                {
                    new() { Key = "General.neo_mode", Label = "PS4 Pro mode", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "General.redzone_patches", Label = "Red zone protection", Hint = "Turn on if the game crashes while a match loads (0xc0000005)", Kind = RowKind.Toggle,
                            Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "General.console_language", Label = "Console language", Kind = RowKind.Choice, Type = RowType.Int, Fallback = "1", Choices = ConsoleLanguages.Choices },
                    new() { Key = "General.volume_slider", Label = "Volume", Kind = RowKind.Slider, Type = RowType.Int, Min = 0, Max = 100, Fallback = "100", Format = v => $"{v:0}%" },
                    new() { Key = "General.show_splash", Label = "Splash screen", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "Network.connected_to_network", Label = "Network", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                },
            },
            new()
            {
                Title = "Controls", Glyph = "", Caption = "gamepad, mouse and keyboard",
                Rows = new()
                {
                    new() { Key = "Input.motion_controls_enabled", Label = "Motion controls", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                    new() { Key = "Input.background_controller_input", Label = "Gamepad in the background", Hint = "Keep reading the gamepad when the window isn't focused",
                            Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "Input.cursor_hide_timeout", Label = "Hide cursor after", Kind = RowKind.Slider, Type = RowType.Int, Min = 0, Max = 30, Fallback = "5", Format = v => $"{v:0} s" },
                },
            },
            new()
            {
                Title = "Advanced", Glyph = "", Caption = "memory, caches and logging",
                Rows = new()
                {
                    new() { Key = "General.extra_dmem_in_mbytes", Label = "Extra direct memory", Hint = "On top of the PS4's own", Kind = RowKind.Slider, Type = RowType.Int,
                            Min = 0, Max = 8000, Fallback = "0", Format = v => $"{v:0} MB" },
                    new() { Key = "Vulkan.pipeline_cache_enabled", Label = "Pipeline cache", Hint = "Fewer hitches after the first play", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                    new() { Key = "Log.enable", Label = "Log", Hint = "user\\log\\shadps4.log, shown under Console", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                },
            },
        };
        foreach (var row in AllRows)
            row.Changed += _ => SetDirty(true);
        Sections.ItemsSource = _sections;
    }

    private IEnumerable<SettingRow> AllRows => _sections.SelectMany(x => x.Rows);

    private static JsonNode At(JsonObject config, string key)
    {
        string[] parts = key.Split('.');
        return (config?[parts[0]] as JsonObject)?[parts[1]];
    }

    private static string Raw(JsonNode n) => n is JsonValue v ? v.ToString() : null;

    private void LoadConfig()
    {
        if (_sections == null) BuildSections();
        _config = null;
        try { _config = File.Exists(ConfigFile) ? JsonNode.Parse(File.ReadAllText(ConfigFile))?.AsObject() : null; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException) { }
        foreach (var row in AllRows)
        {
            string raw = row.Key.EndsWith(".window_*") || row.Key.EndsWith(".internal_screen_*")
                ? Raw(At(_config, row.Key.Replace("*", "width"))) is { } w && Raw(At(_config, row.Key.Replace("*", "height"))) is { } h ? $"{w}x{h}" : null
                : Raw(At(_config, row.Key));
            row.Load(raw);
        }
        bool ok = _config != null;
        Sections.IsEnabled = ok;
        SetDirty(false);
        ConfigCaption.Text = _install == null ? "Choose the NHL24 PC folder first."
            : ok ? $"{ConfigFile}  ·  the launcher saves over it when you change settings there"
            : $"No {ConfigFile} yet: start the launcher once to make it.";
    }

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        SaveButton.IsEnabled = dirty;
        RevertButton.IsEnabled = dirty;
        ConfigTitle.Text = "Emulator settings" + (dirty ? "  ·  unsaved changes" : "");
    }

    private void OnRevert(object sender, RoutedEventArgs e) => LoadConfig();

    /// <summary>Changes rows without saving (scripted runs): "GPU.vblank_frequency=120;General.neo_mode=true".</summary>
    public void SetValues(string spec)
    {
        foreach (string pair in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = pair.Split('=', 2);
            if (AllRows.FirstOrDefault(x => x.Key.Equals(kv[0].Trim(), StringComparison.OrdinalIgnoreCase)) is not { } row) continue;
            string v = kv[1].Trim();
            switch (row.Kind)
            {
                case RowKind.Toggle: row.BoolValue = v is "1" or "true" or "on"; break;
                case RowKind.Choice: row.ChoiceValue = row.Choices.FirstOrDefault(c => c.Value.Equals(v, StringComparison.OrdinalIgnoreCase)) ?? row.ChoiceValue; break;
                case RowKind.Slider: row.NumberValue = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture); break;
                default: row.TextValue = v; break;
            }
        }
    }

    public bool Save() => SaveConfig();

    private void OnSave(object sender, RoutedEventArgs e) => SaveConfig();

    /// <summary>Writes the rows into config.json, keeping every other key as it was (a copy of the first original is kept as config.json.hms-backup).</summary>
    private bool SaveConfig()
    {
        if (_config == null) return false;
        if (Emulators.Running() is { } emu)
        {
            StatusChanged?.Invoke($"{emu} is running: close the game before changing its settings.");
            return false;
        }
        foreach (var row in AllRows)
        {
            string[] parts = row.Key.Split('.');
            if (_config[parts[0]] is not JsonObject section) _config[parts[0]] = section = new JsonObject();
            if (parts[1].EndsWith("*"))
            {
                string[] wh = (row.ChoiceValue?.Value ?? row.Fallback).Split('x');
                section[parts[1].Replace("*", "width")] = long.Parse(wh[0]);
                section[parts[1].Replace("*", "height")] = long.Parse(wh[1]);
            }
            else
                section[parts[1]] = row.ToNode();
        }
        // fullscreen follows the display mode
        if (_config["GPU"] is JsonObject gpu)
            gpu["full_screen"] = Raw(gpu["full_screen_mode"]) is { } mode && mode != "Windowed";
        try
        {
            string backup = ConfigFile + ".hms-backup";
            if (!File.Exists(backup)) File.Copy(ConfigFile, backup);
            File.WriteAllText(ConfigFile, _config.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
            SetDirty(false);
            StatusChanged?.Invoke("Saved " + ConfigFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusChanged?.Invoke("Couldn't save the settings: " + ex.Message);
            return false;
        }
    }

    /// <summary>The folder of the eboot.bin NHL24 PC last started (user\game_path.txt), when it's still there.</summary>
    private string LastStarted()
    {
        try
        {
            string eboot = File.Exists(User("game_path.txt")) ? File.ReadAllText(User("game_path.txt")).Trim() : null;
            return !string.IsNullOrEmpty(eboot) && File.Exists(eboot) ? Path.GetDirectoryName(eboot) : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { return null; }
    }

    /// <summary>
    /// The game folder NHL24 PC runs: the one it last started (newer launchers pick any folder), else game-UPDATE when
    /// there is one, else game (often a link to the extracted dump). Mods only reach the game when that's the folder HMS
    /// writes to.
    /// </summary>
    private void ShowRunsGame()
    {
        RunsWarning.Visibility = Visibility.Collapsed;
        FixLinkButton.Visibility = Visibility.Collapsed;
        if (_install == null)
        {
            RunsText.Text = "–";
            return;
        }
        string last = LastStarted();
        string runs = last ?? new[] { "game-UPDATE", "game" }.Select(d => Path.Combine(_install, d)).FirstOrDefault(d => File.Exists(Path.Combine(d, "eboot.bin")));
        if (runs == null)
        {
            // game is usually a link to the extracted dump: say where it points when that's gone
            string game = Nhl24Pc.GameLink(_install);
            string target = Nhl24Pc.LinkTarget(_install);
            CanFixLink();
            if (target != null)
            {
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
            RunsWarning.Text = $"That isn't the folder HMS has open ({_gameDir}), so changes made here won't show in this game."
                               + (last != null ? " Pick the NHL 24 folder in NHL24 PC's launcher." : "");
            RunsWarning.Visibility = Visibility.Visible;
            // relinking game only helps when that's what runs
            if (last == null && !runs.EndsWith("game-UPDATE", StringComparison.OrdinalIgnoreCase)) CanFixLink();
        }
    }

    /// <summary>Offers to relink install\game when HMS has an NHL 24 dump open and game is a link (or missing).</summary>
    private void CanFixLink()
    {
        string game = Nhl24Pc.GameLink(_install);
        bool fixable = File.Exists(Path.Combine(_gameDir ?? "", "eboot.bin")) && GameProfile.Detect(_gameDir).Kind == GameKind.Nhl24
                       && (Nhl24Pc.LinkTarget(_install) != null || !Directory.Exists(game));
        FixLinkButton.Visibility = fixable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFixLink(object sender, RoutedEventArgs e)
    {
        string game = Nhl24Pc.GameLink(_install), old = Nhl24Pc.LinkTarget(_install);
        string ask = $"Point NHL24 PC's game link at the NHL 24 folder HMS has open?\n\n{game}\n→ {_gameDir}\n\n"
                     + (old != null ? $"It points at {old} now. Only the link changes: no game files are moved or deleted.\n\n" : "")
                     + "From then on, NHL24 PC's launcher writes its resolution and FPS patches into that folder's eboot.bin when you play.";
        if (MessageBox.Show(Window.GetWindow(this), ask, "HMS", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            Nhl24Pc.Relink(_install, _gameDir);
            StatusChanged?.Invoke($"NHL24 PC now plays {_gameDir}" + (old != null ? $" (it was {old})" : ""));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            MessageBox.Show(Window.GetWindow(this), "Couldn't change the link: " + ex.Message, "HMS", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        ShowRunsGame();
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
        var dlg = new OpenFolderDialog { Title = "Choose the NHL24 PC folder (the one with nhl24.exe)", InitialDirectory = _install };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!IsInstall(dlg.FolderName))
        {
            StatusChanged?.Invoke("No nhl24.exe in " + dlg.FolderName);
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
            // the launcher reads the same config, so unsaved changes are saved first
            if (_dirty && !SaveConfig()) return;
            string launcher = Launcher(), exe = Exe(_install);
            var start = launcher == null ? new ProcessStartInfo(exe)
                : launcher.EndsWith(".ps1") ? new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{launcher}\"") { CreateNoWindow = true }
                : new ProcessStartInfo(launcher);
            start.WorkingDirectory = _install;
            start.UseShellExecute = false;
            Process.Start(start)?.Dispose();
            StatusChanged?.Invoke(launcher != null ? "Opened the NHL24 PC launcher: press Play there to start the game" : $"Started {Path.GetFileName(exe)}");
            _tick.Start();
        }
        catch (Exception ex)
        {
            PlayProblem.Text = "Could not start NHL24 PC: " + ex.Message;
            PlayProblem.Visibility = Visibility.Visible;
        }
    }

    /// <summary>The package's launcher: tools\launcher.ps1 (what older packages' shortcut runs), else launcher.exe, or null.</summary>
    private string Launcher() =>
        new[] { Path.Combine(_install, "tools", "launcher.ps1"), Path.Combine(_install, "launcher.exe") }.FirstOrDefault(File.Exists);

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
