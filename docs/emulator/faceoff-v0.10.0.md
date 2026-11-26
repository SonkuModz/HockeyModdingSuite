# Faceoff v0.10.0 (KytyPS5-nhl27-enhanced)

A build of duy64/KytyPS5-nhl (release a917829, branch `opt/nhl27-fixes`) for NHL 27 (PPSA34063, tested with
game version 01.003.001), with the Faceoff launcher. It holds menus at 30 fps and play at 60 fps.

This is a test build. No game files are included: you need your own dump of the game.

## New in v0.10.0

The project is now called **Faceoff**, and releases are numbered from v0.10.0 (this one is the former RC6).

- **Smoother play.** Memory faults are handled in larger batches, unmapped GPU-written memory is dropped instead of
  copied back, and the page-tracking locks give up the CPU instead of spinning. Played with a controller before
  release; not yet measured against RC5 on the same match.
- **Fixes from KytyPS5**, each with its original author: texture views past the last mip end cleanly instead of
  closing the emulator, a depth image keeps its own stencil record, and freed memory is unmapped from the GPU one run
  at a time (nmzik); a depth target keeps its HTile clear state (TheCruZ).
- **Settings on by default** (in `builds/rc6/cfgGlobal.json`, remove a line to turn one off): `KYTY_FAULT_COALESCE_PAGES`,
  `KYTY_UNMAP_DISCARD`, `KYTY_STENCIL_OWN_PLANE`, `KYTY_HTILE_OWNER`, `KYTY_SPIN_YIELD`.
- **Diagnostics**, only with `KYTY_PERF_STATS=1`: draw-preparation timing (`NHL27DRAWPROF`, duy64), read-fault
  breakdown (`NHL27RDSPLIT`).
- **New Faceoff look**, shared with the other Puck Peak apps: app dock at the top left, Play at the top right, check
  marks for game, emulator and shader cache, and a status bar with download progress.
- **Updating from RC5:** the auto-update installs the new emulator build and keeps your saves and shader cache. The
  launcher itself is now numbered 0.10.0, which RC5's launcher reads as older, so take `Faceoff.exe` from the zip once
  by hand.

Known: shadows can look blocky in places (seen in earlier builds too), and a cut from one cutscene to the next can
stutter.

## New in RC5

- **Built on RC3 again.** Testers found RC4 slower than RC3, with the overlay's texture transfers far higher. Two of
  RC4's on-ice fixes re-upload whole textures much more often than needed. RC5 starts from RC3 and leaves them out.
  RC4 stays available in Faceoff for anyone who prefers its glitch fixes.
- **Newer fixes from KytyPS5 and duy64**, each with its original author:
  - four texture fixes by nmzik (cube textures, views past the last mip, storage-texture LOD, depth textures with
    metadata);
  - a faster internal lock (Artyom Zabroda) and fewer repeated GPU interrupts (superkeka);
  - a shader math fix by duy64 (clamped reciprocal), for stability.
- **Long-session crash fix.** When the GPU runs out of dedicated buffer allocations the game keeps running and the log
  says `Buffer: dedicated allocations exhausted`.
- **Better crash reports.** `play_log.txt` now names your GPU and driver, the shader being compiled, and the window
  setup steps.
- **Faceoff 1.2.0:**
  - **Pick your build:** RC3, RC4 or RC5 on the Play tab. A build you don't have is downloaded from GitHub when you
    pick it, and checked against its known SHA-256.
  - **Auto-update:** Faceoff looks for a new release when it starts and offers to update. Saves, shader cache and
    settings stay where they are.
  - All builds share one set of saves (`_SaveData`, `_DownloadData`, `_Trophies`) and one shader cache folder.
  - The trophy pop-up setting now works.
  - After the game closes or crashes, **Open log folder** shows `play_log.txt` in Explorer for your bug report.
  - 1440p is the default resolution.
- The RC5 speed-ups were played at 1440p before release; the multi-draw change and the out-of-video-memory recovery
  have only passed build checks: see `TESTING.md`.

## New in RC3

- **Be a Pro crash fixed.** Opening Weekly Growth or Traits crashed the game (`DEVICE_LOST`). 5 of 5 opens crashed
  before the fix, 0 of 13 after it. Cause: a depth target reused at the same address with a wider width kept the
  old, narrower image. It was not an NVIDIA driver bug.
- **Be a Pro images fixed.** The hub headline picture and the player, agent, coach and GM portraits came out as
  coloured noise. The game reads those pictures from memory right after the GPU draws them, and the emulator now
  copies them back in time.

## How to use

1. Check the requirements below.
2. **Coming from RC3, RC4 or RC5:** extract v0.10.0 into your old folder and let it overwrite. On the first start Faceoff moves
   the old build into `buildsc3` or `buildsc4`, so your saves, shader cache and game folder stay. **New install:**
   extract it anywhere you can write to, with at least 2 GB free. It should not be inside `Program Files`.
3. Double-click `Faceoff.exe`, the launcher: pick the game folder and the build, change resolution, display and render
   scale, then press Play. `Play-NHL27.cmd` still works as a plain fallback; it starts the build Faceoff last picked.
4. The first start asks for your game folder, the one that holds `eboot.bin`. It remembers the choice in
   `game_path.txt`. Delete that file to pick another folder.
5. Close the game window when you're done.

Don't start `kyty_emulator.exe` directly: it would run without the NHL 27 settings.

To keep saves from another Kyty install, copy `_SaveData`, `_DownloadData` and `_Trophies` into this folder.

**First start:** the first arena load stutters for 30 s or longer while shaders compile. That happens once per
folder. If your profile shows "Sign in required", press OK.

**Controls:** plug in a controller before you start. Keyboard: J = Cross, L = Circle, I = Triangle, K = Square,
arrow keys = D-pad, WASD = left stick, Enter = Options, Q / E = L1 / R1.

**Resolution and display:** set them in Faceoff. Fullscreen and resolutions other than 2560x1440 are untested here.

## Requirements

- Windows 10 or 11, 64-bit, and your own dump of NHL 27.
- A CPU with AVX2 (Intel Haswell / AMD Zen or newer). Without AVX2 the game won't start.
- The latest Microsoft Visual C++ Redistributable (x64). If the emulator won't start and a window names `MSVCP140.dll`
  or `VCRUNTIME140.dll`, install it from Microsoft.
- About 14 GB of free RAM and 10 GB of video memory at 2560x1440, measured in a match.
- A current GPU driver. Tested only on an RTX 5080 with driver 621.01, a Ryzen 7 9800X3D and 32 GB of RAM.
  Expect lower numbers on slower hardware. Other GPUs have not been tried.

## Known issues

- **Sudden fps drops.** Roughly 1 match in 4 in scripted tests drops to 5-10 fps, for a few seconds up to more than a
  minute. There are at least two causes and neither is fixed.
- **Rare crashes.** Open reports: a crash inside the NVIDIA driver while compiling a shader on a fresh cache (RC4), a
  crash while creating a Be a Pro player, and a silent exit right after the window opens. If you hit one, keep
  `play_log.txt`, and for a silent exit also the Windows Event Viewer entry (Windows Logs > Application,
  `kyty_emulator.exe`).
- **Crash when simming between shifts.** A tester on NVIDIA reports a crash in Be a Pro when 2 or more goals are scored
  during a sim to the next shift, on an earlier build. It could not be reproduced here: 5 sims ran clean, but none had 2
  or more goals, so it may still be there. If you hit it, please send `play_log.txt`, your GPU and your driver version.
- **Long sessions.** The longest test was 55 minutes (two Be a Pro games, mostly simmed). Three matches in a row, saving
  and reloading a career, and a full Be a Pro season have not been tested.
- **Stutter.** Entering play can stall for 70-220 ms, and heavy 30 fps cut-scenes (arena intro, some replays) can dip
  to 20-25 fps for a few seconds. Play has 1% lows around 50 fps.
- **Starting again right after closing** can fail with `could not reserve 13824 MB for guest direct memory`. Wait a
  few seconds and try again.
- **ONES mode** needs readback on, according to upstream: add `--readback-linear-images true` to `extra_args` in
  `builds\<build>\cfgGlobal.json`. Not tested with this build.
- **Online modes**, PSN and EA servers don't work. That's not a goal of this build.
- The upstream known bugs (jersey cloth, some skin tones, facial hair, bright pixels, logos) are unchanged.

## Reporting a problem

- Take a screenshot and note the scene and the rough time.
- After a crash or freeze keep `play_log.txt`. The next start moves it to `play_log_previous.txt`, and the start
  after that replaces it.

## Changes since a917829

- Pacing: the game's own flip rate at a 60 Hz vblank is the default, so menus and cut-scenes run at 30 fps and play at
  60 fps. `KYTY_FORCE_FLIP_RATE=0` with `KYTY_MENU_VBLANK_HZ=240` / `KYTY_PLAY_VBLANK_HZ=240` brings back the old
  behaviour.
- At the 30 Hz flip rate, a frame that misses its vblank shows on the next vblank (50 ms) instead of the next even one
  (66 ms). `KYTY_LATE_FLIP=0` restores the old behaviour.
- Built with PGO and AVX2 (clang-cl 19.1.5 from VS 2022 Build Tools). Build line: `tools/pgo/README.md`.
- Frame-pacing log lines: `NHL27FRAMETIME` every 5 s, `NHL27HITCH` for each frame from 40 ms.
- `cfgGlobal.json`: `KYTY_PERF_STATS` is `0`. `KYTY_READBACK_STILLS=0` turns the Be a Pro image fix off.
- Text and overlay keys in the scripted input file, used for automated tests.
- `tools/nhl27`: the start script and settings shipped in this folder.

## Measured

Ryzen 7 9800X3D, RTX 5080, scripted 15-minute CPU vs CPU Play Now match.

| | a917829 defaults (1 run) | pacing fix (2 runs) | pacing fix + PGO/AVX2 (3 runs) |
|---|---|---|---|
| Play fps (median / p10) | 67 / 54 (uncapped) | 60 / 59-60 | 60 / 60 |
| Play mean 1% low (fps) | - | 44.9-46.9 | 48.7-49.8 |
| Play 5 s windows with no missed frame | - | 58-71% | 75-87% |
| Play frames over 50 ms (15 min) | - | 28-31 | 9-23 |
| Menus / 30 Hz scenes fps (median / p10) | 40 / - (uncapped) | 30 / 29 | 30 / 28 |

These numbers are from RC2. The RC3 fixes were not measured separately. On the RC3 code, a 55-minute Be a Pro run (two
games) had no crash, with play at a median of 60 fps and a 1% low of 52.

## Source

GPL-2.0. Source code: `KytyPS5-nhl27-enhanced-rc5-src.zip`, shipped next to this build (tag `nhl27-enhanced-rc5`).

## Credits

duy64 (KytyPS5-nhl, the RC4 on-ice glitch fixes and the RC5 shader fix), nmzik, Artyom Zabroda, superkeka, pengui07,
pargi, the KytyPS5 authors (GPL-2.0, see `licenses/`).
AnyPS5 (boykopovar) was used as a reference only. No code was copied from it.

## AI involvement

The changes, measurements and these notes were produced with an AI coding assistant (Claude) driving the build,
scripted test runs and log analysis. Human testing: the Be a Pro crash and the image bug were found by hand on RC2.
RC3 itself has been through scripted runs only. RC4 adds duy64's fixes unchanged; the AI ported and built them. RC5's
commits were picked, ported and built by the AI, and Faceoff 1.2.0 was written by it.
