using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NHL27Kit.App;

/// <summary>
/// KytyPS5's settings for one game, layered the way its launcher layers them (each layer's "emu" object; "env" merges
/// key by key):
///   build:   cfgGlobal.json, then cfg&lt;TITLE&gt;.json, next to kyty_emulator.exe
///   global:  %LOCALAPPDATA%\KytyPS5\configs\kyty.json
///   profile: %LOCALAPPDATA%\KytyPS5\configs\games\&lt;TITLE&gt;.json   (the only layer the kit writes)
/// A profile value equal to the layers below is dropped, so the profile holds only what differs, like the launcher's.
/// </summary>
public sealed class KytySetup
{
    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KytyPS5");
    public static string ConfigsDir => Path.Combine(DataDir, "configs");

    public string TitleId { get; }
    public string Emulator { get; }
    public string BuildName => NHL27Kit.Core.KytyReleases.BuildName(Emulator);
    public string RunDir { get; }
    public string ProfileFile => Path.Combine(ConfigsDir, "games", TitleId + ".json");

    private readonly JsonObject _build = new(), _global = new();
    private JsonObject _profileDoc, _profile;

    private KytySetup(string titleId, string emulator)
    {
        TitleId = titleId;
        Emulator = emulator;
        var kyty = ReadJson(Path.Combine(ConfigsDir, "kyty.json"));
        string runDir = kyty?["app"]?["run_dir"]?.GetValue<string>();
        RunDir = string.IsNullOrWhiteSpace(runDir) ? Path.Combine(DataDir, "run") : runDir;
        if (emulator != null)
        {
            string dir = Path.GetDirectoryName(emulator);
            Merge(_build, ReadJson(Path.Combine(dir, "cfgGlobal.json"))?["emu"] as JsonObject);
            Merge(_build, ReadJson(Path.Combine(dir, $"cfg{titleId}.json"))?["emu"] as JsonObject);
        }
        Merge(_global, kyty?["emu"] as JsonObject);
        LoadProfile();
    }

    /// <summary>
    /// The settings for <paramref name="titleId"/>. The emulator is <paramref name="emulator"/> when given, else the
    /// launcher's own choice when that file exists, else the newest KytyPS5 build next to the kit.
    /// </summary>
    public static KytySetup Load(string titleId, string emulator)
    {
        if (string.IsNullOrWhiteSpace(emulator) || !File.Exists(emulator))
            emulator = FindEmulator();
        return new KytySetup(titleId, emulator);
    }

    public static string FindEmulator()
    {
        string chosen = ReadJson(Path.Combine(ConfigsDir, "kyty.json"))?["app"]?["emulator"]?.GetValue<string>();
        if (File.Exists(chosen))
            return chosen;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var builds = NHL27Kit.Core.KytyReleases.BuildFolders(dir.FullName).Select(NHL27Kit.Core.KytyReleases.FindEmulator).Where(p => p != null).ToList();
            if (builds.Count > 0)
                return builds[0];
        }
        return null;
    }

    public void LoadProfile()
    {
        _profileDoc = ReadJson(ProfileFile) ?? new JsonObject { ["version"] = 1 };
        if (_profileDoc["emu"] is not JsonObject emu)
            _profileDoc["emu"] = emu = new JsonObject();
        _profile = emu;
    }

    // ---------------------------------------------------------------- values

    public enum Layer { Default, Build, Global, Profile }

    /// <summary>The value in effect and the layer it comes from (env keys when <paramref name="env"/>).</summary>
    public (JsonNode Value, Layer From) Get(string key, bool env = false)
    {
        if (Find(_profile, key, env) is { } p) return (p, Layer.Profile);
        if (Find(_global, key, env) is { } g) return (g, Layer.Global);
        if (Find(_build, key, env) is { } b) return (b, Layer.Build);
        return (null, Layer.Default);
    }

    /// <summary>What the layers below the profile give (null if none).</summary>
    public JsonNode Inherited(string key, bool env = false) => Find(_global, key, env) ?? Find(_build, key, env);

    /// <summary>Sets a profile value; a value equal to the inherited one (or <paramref name="fallback"/>) is removed.</summary>
    public void Set(string key, JsonNode value, bool env = false, JsonNode fallback = null)
    {
        var target = env ? EnvOf(_profile, create: true) : _profile;
        var below = Inherited(key, env) ?? fallback;
        if (value == null || (below != null && JsonNode.DeepEquals(below, value)))
            target.Remove(key);
        else
            target[key] = value.DeepClone();
        if (env && target.Count == 0)
            _profile.Remove("env");
    }

    public void Reset(string key, bool env = false)
    {
        var target = env ? EnvOf(_profile, create: false) : _profile;
        target?.Remove(key);
    }

    /// <summary>Every env key any layer sets, sorted.</summary>
    public IEnumerable<string> EnvKeys() =>
        new[] { _build, _global, _profile }.SelectMany(l => EnvOf(l, false)?.Select(p => p.Key) ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);

    public int ProfileCount => _profile.Count(p => p.Key != "env") + (EnvOf(_profile, false)?.Count ?? 0);

    /// <summary>Writes the profile (keeping any keys the kit does not know). The first save keeps a .bak of the original.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfileFile));
        if (File.Exists(ProfileFile) && !File.Exists(ProfileFile + ".bak"))
            File.Copy(ProfileFile, ProfileFile + ".bak");
        string text = _profileDoc.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ProfileFile + ".tmp", text + "\n", new UTF8Encoding(false));
        File.Move(ProfileFile + ".tmp", ProfileFile, overwrite: true);
    }

    // ---------------------------------------------------------------- command line

    /// <summary>
    /// The emulator's arguments and extra environment for the settings in effect, as kyty_emulator.exe --help documents
    /// them. Unset values are left to the emulator's own defaults.
    /// </summary>
    public (List<string> Args, Dictionary<string, string> Env) Command(string gameDir)
    {
        var args = new List<string> { "--game", gameDir };
        string S(string k) => Get(k).Value is JsonValue v ? v.ToString() : null;
        bool? B(string k) => Get(k).Value is JsonValue v && v.TryGetValue(out bool b) ? b : null;
        void Flag(string k, string flag) { if (B(k) == true) args.Add(flag); }
        void Opt(string k, string flag) { if (!string.IsNullOrWhiteSpace(S(k))) { args.Add(flag); args.Add(S(k)); } }

        if (S("screen_resolution") is { } res && res.StartsWith('R') && res[1..].Split('X') is [var w, var h])
            args.AddRange(new[] { "--screen-width", w, "--screen-height", h });
        if (S("present_mode") is { } pm)
            args.AddRange(new[] { "--present-mode", PresentModeName(pm) });
        Flag("fullscreen_enabled", "--fullscreen");
        Flag("hdr_enabled", "--hdr");
        Flag("hide_cursor_enabled", "--hide-cursor");
        Flag("perf_overlay_enabled", "--perf-overlay");
        Flag("skip_notice_screen", "--skip-notice-screen");
        Flag("network_connected", "--network-connected");
        Flag("amd_cpu_enabled", "--amd-cpu");
        Flag("readback_linear_images", "--readback-linear-images");
        Flag("readback_tiled_images", "--readback-tiled-images");
        Flag("tessellation_enabled", "--tessellation");
        Flag("sync_raw_image_buffers", "--sync-raw-image-buffers");
        Flag("vulkan_validation_enabled", "--vulkan-validation");
        Flag("shader_validation_enabled", "--shader-validation");
        Flag("command_buffer_dump_enabled", "--command-buffer-dump");
        Flag("profiler_enabled", "--profile");
        if (B("trophy_enabled") == false) args.Add("--no-trophy-notifications");
        if (B("red_zone_protection_enabled") == false) args.Add("--no-redzone");
        Opt("user_name", "--user-name");
        Opt("user_id", "--user-id");
        Opt("console_language", "--console-language");
        Opt("vblank_frequency", "--vblank-frequency");
        Opt("gpu_index", "--gpu");
        Opt("controller_color", "--controller-color");
        Opt("controller_vibration_intensity", "--controller-vibration");
        Opt("controller_speaker_volume", "--controller-volume");
        Opt("controller_keyboard_player", "--keyboard-player");
        Opt("trophy_duration_seconds", "--trophy-duration");
        Opt("audio_input_device", "--mic");
        Opt("shader_optimization_type", "--shader-optimization-type");
        Opt("shader_log_direction", "--shader-log-direction");
        Opt("shader_log_folder", "--shader-log-folder");
        Opt("printf_direction", "--printf-direction");
        Opt("printf_output_file", "--printf-output-file");
        Opt("command_buffer_dump_folder", "--command-buffer-dump-folder");
        // extra_args last, so it can override the flags above (e.g. --redzone)
        if (S("extra_args") is { } extra)
            args.AddRange(SplitArgs(extra));

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string k in EnvKeys())
            if (Get(k, env: true).Value is JsonValue v)
                env[k] = v.ToString();
        return (args, env);
    }

    /// <summary>present_mode is a name or Vulkan's VkPresentModeKHR number (0 Immediate, 1 Mailbox, 2 Fifo).</summary>
    public static string PresentModeName(string value) => value switch
    {
        "0" => "Immediate",
        "1" => "Mailbox",
        "2" => "Fifo",
        _ => value,
    };

    public static string Quote(string a) => a.Length == 0 || a.Any(c => c is ' ' or '"' or '\t') ? "\"" + a.Replace("\"", "\\\"") + "\"" : a;

    private static IEnumerable<string> SplitArgs(string s)
    {
        var cur = new StringBuilder();
        bool quoted = false;
        foreach (char c in s)
        {
            if (c == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); }
                continue;
            }
            cur.Append(c);
        }
        if (cur.Length > 0) yield return cur.ToString();
    }

    // ---------------------------------------------------------------- playtime (shared with the launcher)

    public static (long Seconds, DateTime? LastPlayed) Playtime(string titleId)
    {
        var game = ReadJson(Path.Combine(DataDir, "playtime.json"))?["games"]?[titleId.ToLowerInvariant()];
        long seconds = game?["seconds"]?.GetValue<long>() ?? 0;
        long last = game?["last_played"]?.GetValue<long>() ?? 0;
        return (seconds, last > 0 ? DateTimeOffset.FromUnixTimeSeconds(last).LocalDateTime : null);
    }

    public static void AddPlaytime(string titleId, TimeSpan played)
    {
        string file = Path.Combine(DataDir, "playtime.json");
        var doc = ReadJson(file) ?? new JsonObject { ["version"] = 1 };
        if (doc["games"] is not JsonObject games) doc["games"] = games = new JsonObject();
        string key = titleId.ToLowerInvariant();
        if (games[key] is not JsonObject g) games[key] = g = new JsonObject();
        g["seconds"] = (g["seconds"]?.GetValue<long>() ?? 0) + (long)played.TotalSeconds;
        g["last_played"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(file, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    // ---------------------------------------------------------------- json helpers

    private static JsonNode Find(JsonObject layer, string key, bool env) => env ? EnvOf(layer, false)?[key] : layer[key];

    private static JsonObject EnvOf(JsonObject layer, bool create)
    {
        if (layer["env"] is JsonObject e) return e;
        if (!create) return null;
        layer["env"] = e = new JsonObject();
        return e;
    }

    private static void Merge(JsonObject into, JsonObject from)
    {
        if (from == null) return;
        foreach (var (k, v) in from)
        {
            if (k == "env" && v is JsonObject env)
            {
                var target = EnvOf(into, true);
                foreach (var (ek, ev) in env) target[ek] = ev?.DeepClone();
            }
            else
            {
                into[k] = v?.DeepClone();
            }
        }
    }

    public static JsonObject ReadJson(string file)
    {
        try
        {
            return File.Exists(file)
                ? JsonNode.Parse(File.ReadAllText(file), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>The running emulator: started with the profile's command line, output captured to a log and a short tail.</summary>
public sealed class KytyRun
{
    private readonly Process _process;
    private readonly StreamWriter _log;
    private readonly LinkedList<string> _tail = new();
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public event Action Exited;
    public string LogFile { get; }
    public TimeSpan Elapsed => _clock.Elapsed;
    public bool Running => !_process.HasExited;
    public int ExitCode => _process.HasExited ? _process.ExitCode : 0;

    public KytyRun(string emulator, IEnumerable<string> args, IReadOnlyDictionary<string, string> env, string runDir, string logFile)
    {
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(Path.GetDirectoryName(logFile));
        LogFile = logFile;
        _log = new StreamWriter(logFile, false, new UTF8Encoding(false)) { AutoFlush = false };
        var psi = new ProcessStartInfo(emulator)
        {
            WorkingDirectory = runDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => Line(e.Data);
        _process.ErrorDataReceived += (_, e) => Line(e.Data);
        _process.Exited += (_, _) =>
        {
            _clock.Stop();
            lock (_gate) _log.Flush();
            Exited?.Invoke();
        };
        _log.WriteLine($"> {emulator} {string.Join(' ', args.Select(KytySetup.Quote))}");
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void Line(string line)
    {
        if (line == null) return;
        lock (_gate)
        {
            _log.WriteLine(line);
            // the emulator prints a perf line per frame; keep the tail short
            _tail.AddLast(line);
            if (_tail.Count > 300) _tail.RemoveFirst();
        }
    }

    public string Tail(int lines)
    {
        lock (_gate)
        {
            _log.Flush();
            return string.Join("\n", _tail.Skip(Math.Max(0, _tail.Count - lines)));
        }
    }

    public void Stop()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch { /* already gone */ }
    }
}
