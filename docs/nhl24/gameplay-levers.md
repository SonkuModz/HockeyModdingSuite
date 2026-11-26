# NHL 24 gameplay levers (1.60, NHL24 PC 0.9.3b) — 2026-11-24

What can change gameplay today, where it lives, and how safe it is. Everything below was read from
`NHL24-PC-v0.9.3b\game-UPDATE` (1.60 eboot, nhl_game.db from the 1.60 Patch). Nothing was written.

Full tunables dump: `tunables-nhl24-1.60.csv` (22,933 values: 15,304 float, 5,027 int, 2,602 bool; 537 with no
default read). Columns: name, type, default, min, max, group, label, site (code offset of the registration).

## 1. In-game slider menu (Settings > Gameplay sliders) — no modding

The menu sliders (shot accuracy, pass speed, penalties, hit power, fatigue, goalie reaction, …) are the game's own
profile settings, saved in the user's save data. Changing them needs nothing from HMS and can't break the files.

Behind them the eboot has slider *response curves*: 96 `g…Slider…` objects, of which **84 are a curve/table type
HMS doesn't decode** (only bool/int/float are read), e.g. `gShotAccuracySlider`, `gShotAccuracySliderGlobalScale`,
`gShotPowerSlider`, `gSkatingSpeedSlider`, `gPassAccuracySlider`, `gPassAccuracySliderCPU`, `gHitPowerSlider`,
`gPenaltySliderScale`, `gInjuryRateSlider`, `gFatigueEffctSlider`, `gGoalieScreenEffectSlider`,
`gGoalieErrorDelayFramesScaleSlider`, `gPuckControlSlider`, `gPokeEffectSlider`, the per-penalty
`g…SliderEffect` (tripping, slashing, hooking via the stick-check flags, high-sticking, elbowing, cross-check,
charging). **Update (same day): decoded.** They are type-12 float curves whose points are plain floats in the
eboot's data segment, read live by the game; see `gameplay-deep-dive.md` and `curves-nhl24-1.60.csv`. Changing
them is a data write (no code), not yet tested in game.

There are **no slider tables in `nhl_game.db`** (119 tables: rosters, AI ratings, strategies, schedules, league
state). The only data-file "sliders" are team strategy sliders (below).

## 2. Database (`gamemodes/database/t3db/nhl_game.db`; Play Now also reads `gamemodes/roster/initial_experience.db`)

Writable now with `db-set` (one field per call, through ModStore, CRCs rechecked, read back):
- `exhibitionskaterai` (9,678 rows, 103 fields) / `exhibitiongoalieai` (1,156 rows, 50 fields): per-player AI
  ratings and tendencies. Ratings-wide realism work (e.g. compressing inflated ratings) lives here; needs a bulk
  script rather than one `db-set` per value.
- `exhibitionteamstrategies`: per-team tactic biases (forecheck, PP/PK setups, forward/defence bias 0–10). Ranges
  from `strategies_csv/strategysliders.csv` (e.g. forward/defence bias 0–10, trap/forecheck 0–6).
- `exhibitionplayers` / roster tables: the ratings themselves.

## 3. Eboot tunables (scalar values compiled into `eboot.bin`)

Patched by `TunablePatcher` (ints: immediate; bools: imm8; floats: the `vmovss` load repointed at bytes already
holding the wanted float, else a 4-byte slot in nop padding). Applied as one ModStore entry on `eboot.bin`, so Restore
puts the original bytes back.

Caveats:
- **Only confirmed in game on the 1.00 eboot** (wrist shot power). Never tested on this 1.60 eboot.
- The 2026-11-04 "Match NHL 27" (438 values, `tunables-match`) **broke the game**; the cause is unknown (values or
  the patcher's padding slots/min-max edits). Never re-run it. Any new batch: ≤10 values, inside [min, max], min/max
  untouched, boot a match before adding more.
- A default of 0 is often a zeroed register (`vxorps`), which the patcher can't change (it skips those); the plan
  step says so per value.
- Some rows read with the default outside their range (`gAITuneAIHittingTunePenInjuryModifier` 1.56 vs max 1.25,
  `gPenaltyChancePenaltyShotBonus` 3 vs max 1): possibly misread, don't touch.
- `user\patches\nhl24.xml` (emulator MemoryPatcher, 100 lines: salary cap 2026-27, synchronous replay compression)
  patches this image in memory; new eboot ranges must not overlap those addresses.
- No CLI writes a hand-picked set yet: needs a small `tunables-set NAME=VALUE …` over `TunablePatcher.Make` (plan,
  read back, then `--write` as its own mod entry). Not `tunables-match --groups`.
- The store is the repo's `mods-nhl24` (what HMS's Mods page uses; the kitclone/claudecli2 CLI builds write there).
  Not `%LOCALAPPDATA%\NHL27Kit\mods-nhl24` (old 1.00 store with the restored match entry). It has no eboot entry today.

### Levers that matter most for realism (current values)

| Area | Tunable | Now | Range | Note |
|---|---|---|---|---|
| Shooting | `gGlobalShotErrorScale` | 0.8 | 0–5 | global shot error; higher = fewer pinpoint shots |
| Shooting | `gGlobalShotErrorScaleDefensiveZone` | 0.35 | 0–5 | |
| Shooting | `gRatingsShotAccuracyMaxPenalty` / `gRatingsSlapShotAccuracyMaxPenalty` | 0.6 / 0.7 | 0–1 | how much low ratings hurt accuracy |
| Shooting | `gFatigueShotAccuracyMaxPenalty` / `gFatigueShotPowerMaxPenalty` | **0 / 0** | 0–1 | tiredness doesn't affect shots at all |
| Shooting | `gBackhandShotAccuracyMultiplier` / `gBackhandShotPowerMultiplier` | 0.85 / 0.5 | 0–3 | |
| Shooting | `gOneTimerQualityShotPowerMaxPenalty` | 0.5 | 0–1 | |
| Penalties | `gPenaltyChanceCharging` / `gPenaltyChanceHitIntoBoards` / `gPenaltyChanceHipCheckInterference` | **0 / 0 / 0** | 0–1 | only the knockdown variants (1.0) are called |
| Penalties | `gPenaltyChanceHook/Hold/Slash/Trip(poke)/HighStick/CrossChecking/Elbowing` | 1 | 0–1 | already max |
| Penalties | `gPenaltyChanceDive` | 0.75 | 0–1 | |
| Penalties | `gDisallowPenaltyForTicksAfterPenalty` / `…OtherPlayer` | 75 / 35 | 0–100 | cooldown between calls |
| Penalties | `gDoubleMinorChanceOnHighStick` | 0.2 | 0–1 | |
| Penalties | `gNeverCallGameMisconducts` | false | | |
| Fatigue | `gEnergyDecRateFromAccel_General` | 1.7 | 0–10 | energy cost of accelerating |
| Fatigue | `gBenchEnergyIncScale_General` | 0.22 | 0–10 | bench recovery |
| Fatigue | `gFatigueMultPK_General` / `gFatigueMultPP_General` | 1.2 / 0.5 | 0–5 | |
| Fatigue | `gDecEnergyForHitScale_1Player_Hittee` / `_Hitter` | 0.75 / 0.05 | 0–1 | |
| Skating | `gSpeedBoostForTopSpeed` | 1.2 | 1–2.5 | speed-burst top speed |
| Skating | `gAccelImprovementTuner` / `gAgilityImprovementTuner` | 0.28 / 0.4 | 0–1 | difficulty skating help |
| Goalies | `gEnableGoalieReboundControl` | false | | "Enable Puck Rebound Damping" |
| Goalies | `gGoalieApplySkillLevelModifierToRatingsCPU` / `…Human` | true / false | | difficulty scales CPU goalie ratings |
| Injuries | `gHitInjuryChanceMultiplier` | 1 | 0–10000 | |
| Injuries | `gMinTempInjurySeconds` / `gMaxTempInjurySeconds` | 180 / 480 | 0–3000 | |
| Rules | `gAIRules_Hybrid_XDistfromPuck` / `…ZDistfromGoalline` | 22 / 20 | 0–100 | hybrid icing |

Goalies: 3,029 tunables (`AI/Goalie/...`), mostly rebound and positioning geometry; no single global save scale was
found among the readable values (the screen/error curves are slider types, section 1).

## Real-life targets (reference, calibration paused since 2026-11-03)

`nhl_baseline_2025-26.json`, per team-game: 3.08 goals, 27.8 shots on goal, 11.1 sh%, 88.9 sv% (high-danger 81.1,
mid 88.4, long 96.8), 20.4 hits, 14.2 blocks, 14.9 missed shots, 3.68 penalties (9.0 PIM), PP 21.1%.
