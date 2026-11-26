# NHL 24 gameplay deep dive: how gameplay is defined and how to change it (2026-11-24)

Read-only research on the 1.60 game (`NHL24-PC-v0.9.3b\game-UPDATE`). Nothing was written to the game.
Companion files: `gameplay-levers.md` (scalar tunables), `tunables-nhl24-1.60.csv`, `curves-nhl24-1.60.csv`,
`settings-1.60\` (the settings CSVs/JSON), `tools\eboot-curves\` (the probe and the profile-save decoder).

## What College Football 27 does (the reference)

`C:\Users\sonku\Downloads\reference` holds Frostbite EBX exports from CFB27's `football/Gameplay/AttribSys/data/...`:
one asset per system (`passing`, `tackle`, `blocking`, `fatiguemanager`, `injury`, `gameplay_tuning`, ...), each with a
`_gamestyle_simulation` twin that overrides it for the Simulation style, and `gameplay_sliders` = 42 `FloatCurve`s
(points X→Y) that turn a menu slider position into a multiplier. Gameplay = data assets + slider curves.

NHL 24 has **no AttribSys EBX** (checked all 518,814 EBX names/types). Its gameplay system is EA's older NHL code
("tweakables"), compiled into `eboot.bin`. But it has the same three layers, just stored differently:

| Layer | CFB27 | NHL 24 |
|---|---|---|
| Tuning values | AttribSys EBX fields | 22,933 scalar tweakables (bool/int/float) built by code; `gameplay-levers.md` |
| Slider → effect curves | `gameplay_sliders` FloatCurves | **807 float curves (type 12) + 897 x→y tables (type 14) + 463 type 13 + small types**, points stored as plain floats in the eboot's **initialized data segment** |
| Game styles / slider defaults | `_gamestyle_simulation` assets | `gamemodes/setting_csv/*.csv` (legacy files): settings, contexts (presets A–D, Simulation etc.), defaults |
| User's sliders | save | profile save `Settings.SettingGroupList` (SettingId → SettingValue) |
| Live updates | — | "AI tuner sets" downloaded from EA (`TunerBlob`, `AITunerSets`, `gFEGameModesLoad_EnableAITunerSet`); none present offline |

## 1. Curves and tables: the real lever (new)

A tweakable object (BSS, built by a static initializer) is `+0x10 name, +0x18 label, +0x20 type code, ...`. Type codes:
1 bool (2,605), 2 int (5,031), 4 float (15,306) — the ones HMS read so far — and the rest, found now:

- **12 = float curve (807)**: `+0x38 count`, `+0x40 pointer to count floats`, `+0x48 two floats` (an editor
  min/max like the scalars' ranges; not used when the curve is evaluated, and some stock points lie outside it,
  e.g. `gInjuryRateSlider` 10000 vs 0–6000), `+0x50` set at run time to the instance the evaluator reads.
  738 decoded with their points (69 are filled by other code). Points sit in the RW data segment
  (VA 0xAA00000, file offset 0xA75CB20, 0x1A8FD8 bytes): **file offset = VA − 0xAA00000 + 0xA75CB20**.
  Every array is distinct (no sharing, no overlaps) and referenced once in code, by its initializer (scan of
  REX.W rip-relative `lea`/`mov`), so changing one changes only that curve.
- **How a curve is read (checked in code, e.g. 0x2EF1DBF for `gSkatingSpeedSlider`)**: the slider's setting
  value v (0–100, from the game settings) gives `pos = v × (count − 1) / (steps − 1)`; the result is the linear
  interpolation of `points[⌊pos⌋]` and `points[⌊pos⌋+1]`, read live through `[obj+0x50] → [+0x40]` each time.
  So a 7-point slider at 50 reads point 3, at 55 reads 30% of the way from point 3 to point 4.
- **14 = x→y table (897)**: `+0x38 pointer` to (x, y) float pairs in the same data segment (e.g.
  `gGoalieTetherLeftStickMagToChallengeFactor` = (0.05,0.95) (0.1,1) ...). Count not stored inline yet (to decode).
- 13 (463, int/enum lists), 15 (27, `gSweetSpot_*` x→y with domain), 11 (26, enums), 5/7/9/10 (colours, debug).

The 84 menu-slider curves are all type 12, e.g. (points for slider steps low→high; domain):

| Curve | Points | Editor min–max | File offset |
|---|---|---|---|
| `gShotAccuracySlider` (shot error) | 4 2 1.3 0.8 0.6 0.3 0.01 | 0–4 | A7B8520 |
| `gShotPowerSlider` | 0.7 0.84 0.92 1.08 1.16 1.3 1.5 | 0–2 | A7B8500 |
| `gSlapShotAccuracySlider` | 0.25 0.5 0.75 1 1.2 1.4 2 | 0–2 | A7B8400 |
| `gOneTimerAccuracySlider` | 0.75 0.8 0.85 0.9 0.93 0.97 1 | 0–2 | A7B8460 |
| `gPassAccuracySlider` (error) | 40 40 30 20 10 5 0 | 0–90 | A7B8B80 |
| `gPassInterceptSliderEffect` | 0.01 0.1 0.2 0.3 0.35 0.4 0.9 | 0–1 | A7B9040 |
| `gPuckControlSlider` | 20 2 0.6 0.4 0.35 0.3 0 | 0–30 | A7B89C0 |
| `gSkatingSpeedSlider` | 0.85 0.92 0.98 1.07 1.1 1.15 1.2 | 0–2 | A7B8740 |
| `gAccelerationSlider` | 0.5 0.6 0.7 0.85 1.033 1.2 1.5 | 0–2 | A7B8720 |
| `gAgilitySlider` | 0.85 0.93 0.98 1 1.02 1.05 1.1 | 0–2 | A7B8760 |
| `gFatigueEffctSlider` | 0 0.15 0.25 0.5 0.75 1 1.5 | 0–2 | A7B8700 |
| `gEnergyIncRateBenchSlider` | 0.001 0.025 0.04 0.045 0.065 0.075 0.125 | 0–0.1 | A7B8480 |
| `gHitPowerSlider` | 1.15 1.2 1.25 1.3 1.35 1.4 1.45 | 0–2 | A7B92A0 |
| `gHitPowerSliderForPhysics` | 0.5 0.9 0.95 1 1.05 1.1 1.5 | 0–5 | A7B9280 |
| `gPokeEffectSlider` | 0 0.6875 0.75 0.8125 0.875 0.9375 1.5 | 0–2 | A7B8E30 |
| `gGoalieErrorDelayFramesScaleSlider` | 2 1.5 1.2 1 0.9 0.8 0.5 | 0–10 | A7B8C70 |
| `gFlinchingSavesZErrorScaleSlider` | 2 1.5 1.2 1 0.9 0.7 0.5 | 0–10 | A7B86C0 |
| `gGoalieScreenabilitySlider` | 0 0.5 0.8 0.9 1 1.18 2 | 0–2 | A7B8C40 |
| `gPenaltySliderScale` | 0 0.5 0.75 1 1.5 | 0–3 | A7AE590 |
| `gTrippingSliderEffect` (11 pts) | 1 0 -0.2 … -0.9 -1.5 | −2–2 | A7B9380 |
| `gSlashingSliderEffect` (11) | 1 0 -0.4 … -1.7 -2 | −2–2 | A7B93E0 |
| `gHighStickingSliderEffect` (11) | 1 0 -0.4 … -1.2 -1.5 | −2–2 | A7B93B0 |
| `gInjuryRateSlider` | 10000 4000 1000 450 110 55 1 | 0–6000 | A7B8FC0 |

Beyond sliders, the 738 decoded curves cover goalie behaviour (50+), max-speed/turn limits (`gMax…`, 57), defence,
passing, fighting, preemptive checks, mimic AI, etc. Full list: `curves-nhl24-1.60.csv` (name, type, count,
pointer VA, file offset, points, the +0x48 pair, init site).

**Why this is the safer route**: it changes data, not instructions. What broke the game on 2026-11-04 is unknown;
that change was ~438 *code* patches (repointed loads, constants written into nop padding). A curve change is
4-byte float writes into a data table that only its own initializer points at; no instruction or shared constant
changes. Not yet proven in game.

Two ways to apply:
1. **eboot.bin data bytes** through ModStore (one entry; Restore puts the stock floats back). File offsets above.
   eboot.bin has no mod entry today. The only earlier on-disk eboot change on this setup was the match that broke.
2. **shadPS4 memory patches** in `user\patches\nhl24.xml`: `Address = 0x400000 + VA` (checked against the
   CIMS salary-cap lines; this route is proven to load on 1.60). But the CIMS "emulator patches" entry owns the
   whole file (0–1,863 replaced, 1,863–10,674 appended), so adding lines would make that entry read changed;
   it needs its own handling first.

Prepared proof (not applied): `gSkatingSpeedSlider` all 7 points 1.5 (stock 0.85…1.2), 28 bytes at file offset
0xA7B8740, old `9a99593f1f856b3f48e17a3fc3f5883fcdcc8c3f3333933f9a99993f`, new `0000c03f`×7; read back in memory,
the neighbouring curves unchanged. Pass: in Play Now at the default speed slider, skaters are clearly faster
(≈ +40%); after Restore, back to normal.

## 2. Settings data (slider defaults, game styles) — legacy CSVs, writable with legacy-set

`gamemodes/setting_csv/`: `setting.csv` (≈540 settings: every slider, e.g. 36 SHOT_ACCURACY_HUMAN, 151
SHOT_ACCURACY_CPU, 288/289 PLAYER_SPEED, 327–340 per-penalty sliders, 353–358 holding/hooking/interference),
`range.csv` (GAMEPLAY_SETTINGS_RANGE 0–6 = the 7 curve points), `context.csv` (contexts: `_DEFAULT_`,
DEFAULT_GAME_STYLE, DEFAULT_GAME_STYLE_PRESET_A–D, high-level presets 1–4, Be A Pro, 3v3, …),
`contextsetting.csv` (12,479 rows: per context and setting, enabled / default / visible), `choice.csv`.
- Game style choices: Arcade, Traditional, Half-Sim, **Simulation (hidden, is_visible 0)**, Custom; presets
  Arcade/Default/Simulation/**Hardcore (hidden)**. In 1.60 the four style contexts hold identical slider defaults.
- **Unverified**: the profile save's slider values (0–100: puck control 33, pass interceptions 80, shot accuracy
  55, …) don't match `contextsetting.csv`'s defaults (3, 4, …), so the effective starting values come from
  somewhere else (code or a defaults step). Whether editing these CSVs changes slider defaults, or un-hiding the
  Simulation style gives a different set, is untested.

## 3. Profile save (what the user set)

`user\home\1000\savedata\CUSA37933\Profile0\DATA`: records `[u64 len][8-byte hash][body]`; the main body is
`0x01` + an LZ4 frame holding JSON (537 KB). `Settings.SettingGroupList[].SettingList[] = {SettingId,
SettingValue}` with `Name: TXT_UNSAVED_PRESET`; `AITunerSets: {CRCValue: "0"}` (no tuner set). Decoder:
`tools\eboot-curves\profile_save_decode.py`. Writing it back needs the record hash checked (not done).

## 4. AI tuner sets (EA's live tuning) — not used offline

The game downloads tuner sets (`OnlineContentManager::mTunerLocalStorage`, `TunerBlob`, `TunerCRC`,
`TXT_TUNER_CHOICE_DESCRIPTION_CURRENT/LAST/LATEST`, `gEnableTunableDownload`) and applies them at match load
(`gFEGameModesLoad_EnableAITunerSet`). Offline there are none. If the blob format is decoded, a local tuner set
would override scalar tweakables without patching code — the most complete route, still research.

## Recommended path

1. Proof in game (needs the user's OK, one reversible ModStore entry): set every point of `gSkatingSpeedSlider`
   to 1.5 in eboot.bin data → players should be visibly faster at any speed slider; Restore → back.
2. Then a `curves` reader and `curve-set NAME=v1,v2,…` writer in HMS (plan, read back, one ModStore entry), plus a
   Curves view on the Tuning page.
3. A Simulation setup: curve edits for shooting/passing/goalies/penalties/fatigue, a few curves at a time, with
   the user's slider values (0–100) chosen to sit on the intended points.

## 5. Coverage: what is decoded and what isn't (2026-11-24)

| Source | What it is | State |
|---|---|---|
| Eboot scalars (bool/int/float) | 22,933 tweakables, 15 197 AI, 992 Physics, 193 Animation | read; 537 with no default read |
| Eboot curves (type 12) | 807 slider/response curves | 738 with points read; 69 filled elsewhere |
| Eboot x→y tables (type 14) | 897 (goalie motion, tether, …) | found; point count not decoded |
| Eboot type 13 / 15 / 11 | 463 / 27 / 26 | partly read (13: 278) |
| `gamedata/realai/*.def` + `.set` | "RealAI" decision sets: inputs (e.g. hitter pos/vel, puck-carrier dist/angle, aggressiveness) → actions (bodycheck, hip check, …); puck carrier 0.8 MB, 1-on-0 1.7 MB, off-puck 256 KB, hitting, active sticks, defensive actions | found; binary format not decoded |
| `gamedata/nn/` (39 files) | fdeep neural-net AI models (puck carrier, loose puck, defence) + manifests | found (JSON/XML); not analysed |
| `gamedata/playsystem/allplays.pbk` | 4.2 MB text playbook (play sets) | found; not analysed |
| `gamedata/physics/*.xml` | collision spheres per bone (player, goalie, ref) | found (plain XML) |
| `gamemodes/setting_csv/`, `strategies_csv/` | settings, game styles, strategy sliders | read; effect of edits unverified |
| ANT animation data (`animations/antanimations`, 984 EBX of an unnamed type; `BranchTunerAsset` etc. in the eboot) | animation state graphs / blend trees | not analysed |
| AI tuner sets (online) | EA's live tuning blobs | code found; no data offline |

The eboot read found 25,240 objects against the ~25,500 estimated in §28, so a few hundred may still be missed.
