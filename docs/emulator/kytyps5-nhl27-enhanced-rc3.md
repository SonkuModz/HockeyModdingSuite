# KytyPS5-nhl27-enhanced RC3

A build of duy64/KytyPS5-nhl (release a917829, branch `opt/nhl27-fixes`) for NHL 27 (PPSA34063, tested with
game version 01.003.001). It holds menus at 30 fps and play at 60 fps, and fixes two Be a Pro bugs.

This is a test build. No game files are included: you need your own dump of the game.

## New in RC3

- **Be a Pro crash fixed.** Opening Weekly Growth or Traits crashed the game (`DEVICE_LOST`). 5 of 5 opens crashed
  before the fix, 0 of 13 after it. Cause: a depth target reused at the same address with a wider width kept the
  old, narrower image. It was not an NVIDIA driver bug.
- **Be a Pro images fixed.** The hub headline picture and the player, agent, coach and GM portraits came out as
  coloured noise. The game reads those pictures from memory right after the GPU draws them, and the emulator now
  copies them back in time.

## How to use

1. Check the requirements below.
2. Extract this folder anywhere you can write to, with at least 2 GB free. It should not be inside `Program Files`.
3. Double-click `Play-NHL27.cmd`.
4. The first start asks for your game folder, the one that holds `eboot.bin`. It remembers the choice in
   `game_path.txt`. Delete that file to pick another folder.
5. Keep the black window open while you play. Close the game window when you're done.

Don't start `kyty_emulator.exe` directly: it would run without the NHL 27 settings.

To keep saves from another Kyty install, copy `_SaveData`, `_DownloadData` and `_Trophies` into this folder.

**First start:** the first arena load stutters for 30 s or longer while shaders compile. That happens once per
folder. If your profile shows "Sign in required", press OK.

**Controls:** plug in a controller before you start. Keyboard: J = Cross, L = Circle, I = Triangle, K = Square,
arrow keys = D-pad, WASD = left stick, Enter = Options, Q / E = L1 / R1.

**Resolution:** set `screen_resolution` in `cfgGlobal.json` to `R1280X720`, `R1920X1080`, `R2560X1440` (default) or
`R3840X2160`. Set `fullscreen_enabled` to `true` for fullscreen. Fullscreen and other resolutions are untested here.

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
- **Rare crashes.** One GPU device loss happened in a test build during this work, cause unknown. Tell us if you hit
  one and keep `play_log.txt`.
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
  `cfgGlobal.json`. Not tested with this build.
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

GPL-2.0. Source code: `KytyPS5-nhl27-enhanced-rc3-src.zip`, shipped next to this build (git branch `nhl27-enhanced`).

## Credits

duy64 (KytyPS5-nhl), pengui07, pargi, the KytyPS5 authors (GPL-2.0, see `licenses/`).
AnyPS5 (boykopovar) was used as a reference only. No code was copied from it.

## AI involvement

The changes, measurements and these notes were produced with an AI coding assistant (Claude) driving the build,
scripted test runs and log analysis. Human testing: the Be a Pro crash and the image bug were found by hand on RC2.
RC3 itself has been through scripted runs only.
