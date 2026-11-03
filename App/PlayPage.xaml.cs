using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// Launches NHL 27 on KytyPS5 with the game's Kyty profile, and edits that profile (the same file the Kyty launcher
/// reads, so both stay in step).
/// </summary>
public partial class PlayPage : UserControl
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private Settings _settings;
    private string _gameDir, _titleId = "PPSA34063";
    private KytySetup _setup;
    private List<SettingSection> _sections;
    private List<SettingRow> _envRows;
    private KytyRun _run;
    private bool _dirty;
    private List<KytyBuildRow> _builds = new();
    private List<string> _sourceErrors = new();
    private bool _downloading, _showAllBuilds;
    /// <summary>Builds shown before "Show all".</summary>
    private const int BuildsShown = 6;

    /// <summary>Raised with a short state ("Running · 12:03") and a brush key for the title bar.</summary>
    public event Action<string, string> StateChanged;
    public event Action<string> StatusChanged;

    public PlayPage()
    {
        InitializeComponent();
        _tick.Tick += (_, _) => OnTick();
    }

    public bool IsRunning => _run?.Running == true;

    public void Init(Settings settings, string gameDir, string titleId, string title, string version)
    {
        _settings = settings;
        _gameDir = gameDir;
        _titleId = titleId ?? _titleId;
        HeroTitle.Text = string.IsNullOrEmpty(title) ? "NHL 27" : title;
        HeroMeta.Text = $"{_titleId}  ·  v{version}";
        HeroArt.ImageSource = Art(Path.Combine(gameDir ?? "", "sce_sys", "pic1.png"), 1800) ?? Art(Path.Combine(gameDir ?? "", "sce_sys", "pic0.png"), 1800);
        IconArt.ImageSource = Art(Path.Combine(gameDir ?? "", "sce_sys", "icon0.png"), 256);
        ShowPlaytime();
        LoadSetup();
        _ = LoadReleasesAsync();
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

    private void ShowPlaytime()
    {
        var (seconds, last) = KytySetup.Playtime(_titleId);
        var t = TimeSpan.FromSeconds(seconds);
        string played = seconds == 0 ? "Not played yet" : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m played" : $"{t.Minutes}m played";
        Playtime.Text = last is { } d ? $"{played}  ·  last played {d:MMM d, h:mm tt}" : played;
    }

    // ================================================================ profile

    private void LoadSetup()
    {
        _setup = KytySetup.Load(_titleId, _settings?.Emulator);
        EmulatorPath.Text = _setup.Emulator ?? "kyty_emulator.exe not found";
        BuildInfo.Text = _setup.Emulator == null
            ? "Choose kyty_emulator.exe from a KytyPS5 build."
            : $"Build {_setup.BuildName}. Settings layer: build cfg files → Kyty global → this game's profile.";
        BuildSections();
        Reload();
        UpdateBuildRows();
    }

    private static Choice C(string label, string value) => new(label, value);

    private void BuildSections()
    {
        _sections = new List<SettingSection>
        {
            new()
            {
                Title = "Display", Glyph = "", Caption = "window and presentation",
                Rows = new()
                {
                    new() { Key = "screen_resolution", Label = "Resolution", Hint = "Window size, and the size fullscreen renders at", Kind = RowKind.Choice, Type = RowType.String, Fallback = "R1280X720",
                            Choices = new() { C("1280 × 720", "R1280X720"), C("1920 × 1080", "R1920X1080"), C("2560 × 1440", "R2560X1440"), C("3840 × 2160", "R3840X2160") } },
                    new() { Key = "present_mode", Label = "Present mode", Hint = "Mailbox: lowest latency without tearing. Fifo: vsync. Immediate: may tear", Kind = RowKind.Choice, Type = RowType.Int, Fallback = "1",
                            Choices = new() { C("Mailbox", "1"), C("Fifo (vsync)", "2"), C("Immediate", "0") } },
                    new() { Key = "fullscreen_enabled", Label = "Fullscreen", Hint = "Borderless desktop fullscreen", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "hdr_enabled", Label = "HDR10 output", Hint = "When the display supports it", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "hide_cursor_enabled", Label = "Hide cursor", Hint = "After 2 seconds without movement", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "perf_overlay_enabled", Label = "Performance overlay", Hint = "F2 toggles it in game, Shift+F2 for detail", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                },
            },
            new()
            {
                Title = "NHL 27 rendering", Glyph = "", Caption = "this build's NHL switches",
                Rows = new()
                {
                    new() { Key = "KYTY_NHL_RES_SCALE", Env = true, Label = "Render scale", Hint = "Internal resolution before FSR 3 upscales it", Kind = RowKind.Slider, Min = 0.5, Max = 1.0, Step = 0.0001,
                            Fallback = "1", Format = v => $"{v * 100:0}%" },
                    new() { Key = "KYTY_FSR3_HDR", Env = true, Label = "FSR 3 in HDR", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_FSR3_NHL27_SHADOW", Env = true, Label = "FSR 3 shadow fix", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_FSR3_NHL27_HISTORY", Env = true, Label = "FSR 3 history fix", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_FSR3_NHL27_PRESENT", Env = true, Label = "FSR 3 present fix", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_FSR1", Env = true, Label = "FSR 1 upscaling", Hint = "Instead of FSR 3", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_OUTPUT_1080P", Env = true, Label = "Force 1080p output", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_SHADER_PREGEN", Env = true, Label = "Pre-build shaders", Hint = "Fewer hitches in game, longer first start", Kind = RowKind.Toggle, Fallback = "0" },
                    new() { Key = "KYTY_PERF_STATS", Env = true, Label = "Performance stats in the log", Kind = RowKind.Toggle, Fallback = "0" },
                },
            },
            new()
            {
                Title = "System", Glyph = "", Caption = "console and account",
                Rows = new()
                {
                    new() { Key = "user_name", Label = "User name", Hint = "Local user, 1–16 characters", Kind = RowKind.Text, Type = RowType.String, Fallback = "Kyty" },
                    new() { Key = "console_language", Label = "Console language", Kind = RowKind.Choice, Type = RowType.Int, Fallback = "1",
                            Choices = ConsoleLanguages.Choices },
                    new() { Key = "skip_notice_screen", Label = "Skip intro logos", Hint = "Startup logos and notices", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "network_connected", Label = "Network", Hint = "Let the game use the local network", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "trophy_enabled", Label = "Trophy notifications", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                    new() { Key = "red_zone_protection_enabled", Label = "Red zone protection", Hint = "NHL needs it: off crashes on loading", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "true" },
                },
            },
            new()
            {
                Title = "Controller", Glyph = "", Caption = "DualSense",
                Rows = new()
                {
                    new() { Key = "controller_vibration_intensity", Label = "Vibration", Kind = RowKind.Slider, Type = RowType.Int, Min = 0, Max = 100, Fallback = "100", Format = v => $"{v:0}%" },
                    new() { Key = "controller_speaker_volume", Label = "Speaker volume", Kind = RowKind.Slider, Type = RowType.Int, Min = 0, Max = 100, Fallback = "50", Format = v => $"{v:0}%" },
                    new() { Key = "controller_keyboard_player", Label = "Keyboard player", Hint = "0 shares player 1 with the gamepad", Kind = RowKind.Choice, Type = RowType.Int, Fallback = "0",
                            Choices = new() { C("Shared with player 1", "0"), C("Player 1", "1"), C("Player 2", "2"), C("Player 3", "3"), C("Player 4", "4") } },
                },
            },
            new()
            {
                Title = "Advanced", Glyph = "", Caption = "accuracy and debugging",
                Rows = new()
                {
                    new() { Key = "readback_linear_images", Label = "Read back linear images", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "readback_tiled_images", Label = "Read back tiled images", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "sync_raw_image_buffers", Label = "Sync raw image buffers", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "tessellation_enabled", Label = "Tessellation", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "amd_cpu_enabled", Label = "AMD CPU patches", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "vulkan_validation_enabled", Label = "Vulkan validation", Hint = "Very slow", Kind = RowKind.Toggle, Type = RowType.Bool, Fallback = "false" },
                    new() { Key = "extra_args", Label = "Extra arguments", Hint = "Passed to kyty_emulator.exe after everything else", Kind = RowKind.Text, Type = RowType.String, Fallback = "" },
                },
            },
        };
        var covered = _sections.SelectMany(s => s.Rows).Where(r => r.Env).Select(r => r.Key).ToHashSet();
        _envRows = _setup.EnvKeys().Where(k => !covered.Contains(k)).Select(k =>
        {
            string v = _setup.Get(k, env: true).Value?.ToString();
            bool flag = v is "0" or "1";
            return new SettingRow { Key = k, Label = k, Env = true, Mono = true, Kind = flag ? RowKind.Toggle : RowKind.Text, Fallback = flag ? "0" : "" };
        }).ToList();
        foreach (var r in _sections.SelectMany(s => s.Rows).Concat(_envRows))
            r.Changed += OnRowChanged;
        Sections.ItemsSource = _sections;
        EnvRows.ItemsSource = EnvToggle.IsChecked == true ? _envRows : null;
        EnvTitle.Text = $"Environment variables  ({_setup.EnvKeys().Count()})";
    }

    private IEnumerable<SettingRow> AllRows => _sections.SelectMany(s => s.Rows).Concat(_envRows);

    private void Reload()
    {
        foreach (var r in AllRows)
            r.Load(_setup);
        RefreshProfileInfo();
    }

    private void OnRowChanged(SettingRow row)
    {
        _setup.Set(row.Key, row.ToNode(), row.Env, row.FallbackNode);
        row.Load(_setup);
        SetDirty(true);
    }

    private void OnResetRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SettingRow row) return;
        _setup.Reset(row.Key, row.Env);
        row.Load(_setup);
        SetDirty(true);
    }

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        SaveButton.IsEnabled = dirty;
        RevertButton.IsEnabled = dirty;
        RefreshProfileInfo();
    }

    private void RefreshProfileInfo()
    {
        int n = _setup.ProfileCount;
        ProfileTitle.Text = $"{_titleId} profile" + (_dirty ? "  ·  unsaved changes" : "");
        ProfileCaption.Text = $"{n} setting{(n == 1 ? "" : "s")} differ from the build and global defaults  ·  {_setup.ProfileFile}";
        UpdateCommand();
    }

    private void UpdateCommand()
    {
        if (_setup.Emulator == null)
        {
            CommandText.Text = "No emulator chosen.";
            return;
        }
        var (args, env) = _setup.Command(_gameDir ?? "<game>");
        CommandText.Text = $"{Path.GetFileName(_setup.Emulator)} {string.Join(' ', args.Select(KytySetup.Quote))}\n\n+ {env.Count} environment variables\n  in {_setup.RunDir}";
    }

    private void OnSave(object sender, RoutedEventArgs e) => SaveProfile();

    private bool SaveProfile()
    {
        try
        {
            _setup.Save();
            SetDirty(false);
            StatusChanged?.Invoke("Saved " + _setup.ProfileFile);
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Could not save the profile: " + ex.Message);
            return false;
        }
    }

    private void OnRevert(object sender, RoutedEventArgs e)
    {
        _setup.LoadProfile();
        Reload();
        SetDirty(false);
    }

    private void OnEnvToggle(object sender, RoutedEventArgs e)
    {
        EnvRows.ItemsSource = EnvToggle.IsChecked == true ? _envRows : null;
        EnvRows.Visibility = EnvToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBrowseEmulator(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Choose kyty_emulator.exe", Filter = "KytyPS5 emulator|kyty_emulator.exe|Programs|*.exe" };
        if (_setup.Emulator != null) dlg.InitialDirectory = Path.GetDirectoryName(_setup.Emulator);
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        _settings.Emulator = dlg.FileName;
        _settings.Save();
        LoadSetup();
    }

    // ================================================================ launching

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        PlayProblem.Visibility = Visibility.Collapsed;
        string problem = _setup.Emulator == null ? "Choose kyty_emulator.exe first (the … button next to Emulator)."
            : !File.Exists(Path.Combine(_gameDir ?? "", "eboot.bin")) ? $"No eboot.bin in {_gameDir}. Set the game folder in Settings."
            : null;
        if (problem != null)
        {
            PlayProblem.Text = problem;
            PlayProblem.Visibility = Visibility.Visible;
            return;
        }
        // the launcher reads the same profile, so it is saved before playing
        if (_dirty && !SaveProfile())
            return;
        try
        {
            var (args, env) = _setup.Command(_gameDir);
            _run = new KytyRun(_setup.Emulator, args, env, _setup.RunDir, Path.Combine(KitPaths.Root, "logs", "kyty-last-run.log"));
            _run.Exited += () => Dispatcher.BeginInvoke(OnExited);
            ConsoleText.Text = "";
            ShowRunning(true);
            _tick.Start();
            StatusChanged?.Invoke($"Started {Path.GetFileName(_setup.Emulator)} with {_titleId}");
        }
        catch (Exception ex)
        {
            PlayProblem.Text = "Could not start the emulator: " + ex.Message;
            PlayProblem.Visibility = Visibility.Visible;
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), "Stop NHL 27? Anything not saved in game is lost.", "HMS",
                            MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            _run?.Stop();
    }

    private void OnTick()
    {
        if (_run == null) return;
        var t = _run.Elapsed;
        string time = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        RunningText.Text = $"Running  ·  {time}";
        StateChanged?.Invoke($"Playing  ·  {time}", "Good");
        bool atBottom = ConsoleScroll.VerticalOffset >= ConsoleScroll.ScrollableHeight - 4;
        ConsoleText.Text = _run.Tail(120);
        ConsoleText.Foreground = (System.Windows.Media.Brush)FindResource("TextDim");
        if (atBottom) ConsoleScroll.ScrollToEnd();
    }

    private void OnExited()
    {
        _tick.Stop();
        OnTick();
        ShowRunning(false);
        var played = _run.Elapsed;
        try { KytySetup.AddPlaytime(_titleId, played); } catch { /* playtime is a nicety */ }
        ShowPlaytime();
        string how = _run.ExitCode == 0 ? "closed" : $"exited with code {_run.ExitCode}";
        StateChanged?.Invoke("Index ready", "Good");
        StatusChanged?.Invoke($"NHL 27 {how} after {played:h\\:mm\\:ss}. Log: {_run.LogFile}");
        if (_run.ExitCode != 0 && ExitReason(_run.Tail(60)) is { } reason)
        {
            PlayProblem.Text = reason;
            PlayProblem.Visibility = Visibility.Visible;
        }
    }

    /// <summary>A plain explanation for a start-up failure the log names, or null.</summary>
    private static string ExitReason(string log)
    {
        var m = System.Text.RegularExpressions.Regex.Match(log ?? "", @"could not reserve (\d+) MB for guest direct memory");
        if (!m.Success) return null;
        double need = int.Parse(m.Groups[1].Value) / 1024.0;
        var s = new MemoryStatus { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MemoryStatus>() };
        string free = GlobalMemoryStatusEx(ref s) ? $" Right now {s.AvailPageFile / (double)(1L << 30):0.#} GB is free." : "";
        return $"Windows couldn't set aside the {need:0.#} GB of memory the emulator needs at start.{free} " +
               "Close big programs (browsers, Discord) and try again, or make the Windows paging file larger " +
               "(System → Advanced system settings → Performance → Virtual memory).";
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    private void ShowRunning(bool running)
    {
        PlayButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        StopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        RunningBadge.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Ends the game with the kit (called when the window closes).</summary>
    public void Shutdown() => _run?.Stop();


    // ================================================================ builds (KytyPS5 NHL fork releases)

    private async Task LoadReleasesAsync()
    {
        RefreshReleasesButton.IsEnabled = false;
        ReleasesCaption.Text = $"Checking {string.Join(" and ", KytyReleases.Sources)} on GitHub…";
        try
        {
            var (releases, errors) = await KytyReleases.ListAllAsync();
            if (_downloading) return;
            _sourceErrors = errors;
            _builds = releases.Select(r => new KytyBuildRow { Release = r }).ToList();
            UpdateBuildRows();
            var fresh = _builds.Where(b => b.Status == "New").Select(b => $"{b.Release.Owner} {b.Release.Commit}").ToList();
            if (fresh.Count > 0)
                StatusChanged?.Invoke($"New KytyPS5 build{(fresh.Count > 1 ? "s" : "")} out ({string.Join(", ", fresh)}): download under Builds on the Play page");
        }
        catch (Exception ex)
        {
            ReleasesCaption.Text = $"Couldn't reach GitHub: {ex.Message}";
        }
        finally
        {
            RefreshReleasesButton.IsEnabled = true;
        }
    }

    /// <summary>The newest builds and the one in use; the rest wait behind "Show all".</summary>
    private void ShowBuildRows()
    {
        var shown = _showAllBuilds ? _builds : _builds.Where((b, i) => i < BuildsShown || b.Status == "In use" || b.Busy).ToList();
        ReleaseRows.ItemsSource = shown;
        ShowAllBuildsButton.Content = _showAllBuilds ? "Show fewer" : $"Show all {_builds.Count} builds";
        ShowAllBuildsButton.Visibility = shown.Count < _builds.Count || _showAllBuilds ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Marks each release installed, in use or new (the newest of each fork), from what is on disk and the emulator chosen.</summary>
    private void UpdateBuildRows()
    {
        if (_builds.Count == 0 || _downloading) return;
        string inUse = _setup?.BuildName;
        var newestOfFork = _builds.GroupBy(b => b.Release.Repo).Select(g => g.First()).ToHashSet();
        foreach (var row in _builds)
        {
            var r = row.Release;
            bool used = string.Equals(inUse, r.FolderName, StringComparison.OrdinalIgnoreCase);
            row.Installed = used ? _setup.Emulator : KytyReleases.Installed(r);
            row.Status = used ? "In use" : row.Installed != null ? "Installed" : newestOfFork.Contains(row) ? "New" : null;
            row.Action = used ? null : row.Installed != null ? "Use" : "Download";
            row.Detail = $"{r.Owner}  ·  {r.Commit}  ·  {r.Size / 1048576.0:0.0} MB" + (r.Prerelease ? "  ·  pre-release" : "");
            row.CanAct = true;
        }
        var current = _builds.FirstOrDefault(b => b.Status == "In use");
        string caption = current == null ? "Downloads go next to the kit, beside your other builds."
            : current == _builds[0] ? $"You're on the newest build ({current.Release.Owner})."
            : $"You're on {current.Release.Owner}'s build from {current.Release.Published:MMM d}; newer ones are above it.";
        ReleasesCaption.Text = string.Join("\n", _sourceErrors.Select(e => "Couldn't read " + e).Prepend(caption));
        ShowBuildRows();
    }

    private void OnShowAllBuilds(object sender, RoutedEventArgs e)
    {
        _showAllBuilds = !_showAllBuilds;
        ShowBuildRows();
    }

    private void OnRefreshReleases(object sender, RoutedEventArgs e) => _ = LoadReleasesAsync();

    /// <summary>A menu of the forks' release pages.</summary>
    private void OnOpenReleasesPage(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (string repo in KytyReleases.Sources)
        {
            var item = new MenuItem { Header = repo };
            item.Click += (_, _) => Process.Start(new ProcessStartInfo(KytyReleases.ReleasesPage(repo)) { UseShellExecute = true });
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private async void OnReleaseAction(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not KytyBuildRow row) return;
        if (row.Installed != null)
        {
            UseBuild(row.Installed);
            return;
        }
        _downloading = true;
        foreach (var b in _builds) b.CanAct = false;
        RefreshReleasesButton.IsEnabled = false;
        row.Busy = true;
        row.Progress = 0;
        var r = row.Release;
        try
        {
            var progress = new Progress<(long Done, long Total)>(p =>
            {
                row.Progress = p.Done * 100.0 / Math.Max(1, p.Total);
                row.Detail = $"{r.Commit}  ·  {p.Done / 1048576.0:0.0} of {p.Total / 1048576.0:0.0} MB";
            });
            StatusChanged?.Invoke($"Downloading {r.AssetName}…");
            string exe = await KytyReleases.InstallAsync(r, progress);
            _downloading = false;
            StatusChanged?.Invoke($"Installed {r.FolderName}");
            UseBuild(exe);
        }
        catch (Exception ex)
        {
            _downloading = false;
            UpdateBuildRows();
            row.Detail = "Download failed: " + ex.Message;
            StatusChanged?.Invoke($"Couldn't install {r.Tag}: {ex.Message}");
        }
        finally
        {
            _downloading = false;
            row.Busy = false;
            RefreshReleasesButton.IsEnabled = true;
        }
    }

    /// <summary>Makes <paramref name="exe"/> the emulator Play launches (the profile is kept: it is per game, not per build).</summary>
    private void UseBuild(string exe)
    {
        if (_dirty && !SaveProfile()) return;
        _settings.Emulator = exe;
        _settings.Save();
        LoadSetup();
        StatusChanged?.Invoke($"Playing on {_setup.BuildName} from now on");
    }

    /// <summary>kyty_launcher.exe next to the emulator, else in the newest build that has one (newer releases ship without it).</summary>
    private string FindLauncher()
    {
        var dirs = new List<string>();
        if (_setup.Emulator != null) dirs.Add(Path.GetDirectoryName(_setup.Emulator));
        if (KytySetup.ReadJson(Path.Combine(KytySetup.ConfigsDir, "kyty.json"))?["app"]?["emulator"]?.GetValue<string>() is { } chosen)
            dirs.Add(Path.GetDirectoryName(chosen));
        if (Directory.Exists(KytyReleases.BuildsDir))
            dirs.AddRange(Directory.GetDirectories(KytyReleases.BuildsDir, "KytyPS5-*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase));
        return dirs.Select(d => Path.Combine(d, "kyty_launcher.exe")).FirstOrDefault(File.Exists);
    }

    // ================================================================ links

    private void OnOpenLauncher(object sender, RoutedEventArgs e)
    {
        string launcher = FindLauncher();
        if (launcher == null)
        {
            StatusChanged?.Invoke("No kyty_launcher.exe in any KytyPS5 build.");
            return;
        }
        Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = Path.GetDirectoryName(launcher), UseShellExecute = true });
    }

    private void OnCopyCommand(object sender, RoutedEventArgs e)
    {
        if (_setup.Emulator == null) return;
        var (args, env) = _setup.Command(_gameDir);
        // a cmd.exe line: the env with set, then the emulator
        var lines = env.Select(kv => $"set \"{kv.Key}={kv.Value}\"").ToList();
        lines.Add($"cd /d \"{_setup.RunDir}\"");
        lines.Add($"\"{_setup.Emulator}\" {string.Join(' ', args.Select(KytySetup.Quote))}");
        Clipboard.SetText(string.Join("\r\n", lines));
        StatusChanged?.Invoke("Copied the launch command (as cmd lines)");
    }

    private void OnOpenRunDir(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_setup.RunDir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_setup.RunDir}\"") { UseShellExecute = true });
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        string log = _run?.LogFile ?? Path.Combine(KitPaths.Root, "logs", "kyty-last-run.log");
        if (File.Exists(log))
            Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
        else
            StatusChanged?.Invoke("No log yet: play once first.");
    }
}
