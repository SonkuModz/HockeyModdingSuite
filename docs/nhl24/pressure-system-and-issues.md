# NHL 24 (1.60): Sustained Pressure off switch + issue → lever map (2026-11-24)

Research only. Nothing has been written to the game. Code VA → eboot file offset: `VA + 0x51D80`.
Data VA → file offset: `VA − 0xAA00000 + 0xA75CB20`. shadPS4 memory address: `0x400000 + VA`.

## 1. How Sustained Pressure works (traced in the eboot)

- **Meter object** (one per team): `[+0x1C]` stage 0–4, `[+0x08 + stage*4]` points in this stage,
  `[+0x28]` "full" flag, `[+0x38]` team, `[+0x3C]` **enabled** byte, `[+0x2C]` count of full-pressure triggers.
- **AddPressure** = function at **VA 0x2C5CE30** (callers 0x2C5D5F6, 0x2C5D763, 0x2C5DB53, plus its own
  tail call at 0x2C5D3A1). Steps:
  1. Exit if `[meter+0x3C] == 0` (instruction at **0x2C5CE54**: `74 38`, je → return), or another gate fails
     (0xBA45BA8 == 0xFF, 0xBA45BB0 set, the team flag at +0x280, already full, 0xBA45A39 set, a game-state range
     check on 0xBA475AC).
  2. Add points, capped by the `gPressureMeterStepSize` int curve (5 points, data VA 0xAA53120 / file
     0xA7AFC40, values **900, 0, 0, 0, 0**). When the stage is full, the stage advances. Stages 1–4 need 0, so
     they fill at once.
  3. On reaching stage 4 it sets `[+0x28]` = full and writes the per-player **pressure state** at
     `player+0x780`: **1 = pinned** for all 20 slots of the defending team, **2 = adrenaline** for the
     attacking team. Then it fires the Full Sustained Pressure event (UI, audio).
  - These are the only code sites in the eboot that write 1 or 2 to `player+0x780` (whole code segment scanned:
    0x2C5CF6E…0x2C5D2B2). The reset function at 0x2C5CA70 writes 0 to all slots (17 callers, e.g. faceoffs and
    zone clears).
- **What the state does** (read sites):
  - Ratings, at 0x27B638A: state 1 → rating × `gPinnedRatingMultiplier` (0.8). State 2 → ×
    `gAdrenalineRatingMultiplier` (1.05), only for attribute ids 8 and 15. Clamped to 36–99.
  - Energy, at 0x28437AD: state 1 → energy loss × `gPinnedEnergyLossMultiplier` (3) and recovery ×
    `gPinnedEnergyRecoveryMultiplier` (0). State 2 → loss × `gAdrenalineEnergyLossMultiplier` (0.5).
    This is the "whole team drained to zero".
  - Goalie: `gGoalieCapabilityControlledSaveChanceFactorForPinnedPressureState` 0.33 (redirect saves under pinned).
- The `gPressureMeter…` ints are read through the object's +0x48 pointer, then `[+0x34]`:
  - MaxedTicks at 0x2C5D527
  - OutOfZone at 0x2C5D621
  - OutOfZoneDecrement at 0x2C5D659
  - StumbleFall at 0x2C5D736
  - Fall at 0x2C5D72D
  - ShotSave, Reacting and Desperation at 0x2C5DB0B, 0x2C5DB21 and 0x2C5DB18
  
  They are all in the meter update and event code at 0x2C5D5xx–0x2C5DBxx. Every point source goes through
  AddPressure. The StepSize curve is also read by:
  - 0x102FBCA… and MaxedTicks again at 0x102FCB7 (likely the UI)
  - 0x2D75453, 0x2D777F3 and 0x30E7128 (not traced)
- Other writes to `+0x780`:
  - 0x2D38298 and 0x2D6B1FC write the value back unchanged, or 4 or 5.
  - 0x2D4A516 copies `+0x784` into `+0x780` (save/restore).
  - None of them can create a 1 or 2 by itself.
  - The rest of the scan (866 hits) is other classes' fields, or immediate 0 / −1 stores.

## 2. Ways to turn it off (each one is a ModStore `eboot.bin` entry, so Restore puts the bytes back)

| Option | Bytes | Effect | Risk |
|---|---|---|---|
| **A. Off switch (recommended)** | code file offset **0x2CAEBD4**: `74` → `EB` (je → jmp) | AddPressure always returns at once. The meter never fills, Full Pressure never starts, no pinned/adrenaline state, no drain, no ice countdown | 1 byte of code. Only changes a branch that already exists. No new code, no padding slots |
| B. Data only | file 0xA7AFC40: `84030000` → `FFFFFF3F` (step 0 = 1,073,741,823) | The meter can't fill in practice | The meter UI fraction may look empty or odd. Other readers of the curve, if any, aren't known |
| C. Neutral effects | the 5 multipliers → 1.0 (energy recovery 0 → 1) | Pressure still triggers (event, visuals), but nothing happens to players | 5 float patches through TunablePatcher (repointed loads). Not confirmed on 1.60 |

Notes:
- B and C are weaker:
  - Compiled defaults may be overridden at runtime. The eboot says pinned 0.8 (−20%, the launch value), while
    the 1.2.1 patch notes say −15%; out-of-zone 90 ticks fits the patched 3 seconds.
  - B also rests on the untested data-write route, the same one the skating proof uses.
  - C leaves state readers that weren't traced, e.g. 0x266C7D0, `cmp [r12+780h],1`.
  - A removes the state at its source, so these don't matter for it.
- Applying A needs a small command that writes one `eboot.bin` byte as its own ModStore entry. The CLI has none
  today. Dry run done: the byte at 0x2CAEBD4 is `74` (context `807f3c00 7438 813d`), and the SHA1 after the
  change would be d552d54bd12123b53a0cb75189e288b942f0abe7.
- `nhl24.xml` (CIMS emulator entry) has no patch within 0x400 of either address (checked). Both A and B are
  file edits to `eboot.bin`, not xml lines.
- Eboot SHA1 before any change: 4047f9f026709ca6eaf28e0f0643ec591df2bea0 (same one as the skating proof).
- The meter visuals setting (538 `PRESSURE_METER_VISUALS`, save value 1) only changes the UI. It doesn't turn the system off.
- Not verified in game. Test after applying: play a period, stay in the offensive zone. The meter shouldn't move,
  and the defenders' energy bars shouldn't crash.

## 3. Other issues (from "NHL 24 Issues.txt") → levers found

Names are from `tunables-nhl24-1.60.csv` / `curves-nhl24-1.60.csv`. These are candidates only; none are tested.

| Issue | Levers | Current |
|---|---|---|
| Goalie fatigue / desperation | `gGoalieFatigueBaseEnergyReductionOnSaveAttemptCPU/Human` | 25 / 25 |
| | `gGoalieFatigueBoostInOffensiveZone/NeutralZone CPU/Human` | 2 / 1.5 |
| | `gGoalieEnergyRecoveryGainOnBench` | 0.5 |
| | `gAllowDesperationSavesAsLastResort` | false |
| | Other levers: 90 `…Desperation…` tunables | |
| Hip check "torpedo" | `gHipCheckOtherSpeedMul` | 2 |
| | `gHipCheckMinRelSpeedToUseOtherPlayerSpeed` | 0.1 |
| | `gHipCheckMinTargetSpeedToUseOtherPlayerSpeed` | 0.08 |
| | `gMaxHipCheckSpeedBoost` | 0 |
| | `gHipCheckLowIntentSpeedProportion` | 7 |
| | Other levers: 61 `…HipCheck…` | |
| Charging not called | `gPenaltyChanceCharging` | **0** |
| | `gMinRelSpeedForCharging` | 0.25 |
| | `gMaxTicksSinceFwdForCharging` | 4 |
| | `gMinSpeedPctForCharging` | 1 |
| Hip-check interference | `gPenaltyChanceHipCheckInterference` | **0** |
| Boards hits | `gPenaltyChanceHitIntoBoards` | **0** |
| | Other levers: 27 `…Boarding…` (mostly coach-feedback `gCF_` values) | |
| Net-front push checks | `gCapPushCheckHitMagInSlotNHL24Paranoia` | true |
| | `gHitting_PushCheck…` | |
| | `gCpuOneHandPushCheckProbability` | 0.3 |
| | Note: `gDistanceToNetForMax/MinInterferenceFeet` (40/25) is shot-lane analysis, **not** the penalty radius | |
| Interference calls | 83 `…Interference…` | |
| | e.g. `gAllowInterferenceOnPassReceiver` | false |
| | `gAllowKnockdownInterferenceOnPickups` | false |
| | Per-style curves `g…InterferenceByGameStyle` | |
| Puck magnetism / pickups | 418 pickup tunables | |
| | e.g. `gAddDefensivePickupError` | false |
| | `gAcceleratingPickupMaxSpeed` | 0.35 |
| | `gMagnetPadding` | 0.6 |
| | `gDoBlockedByPlayerMagnet` | true |
| Poke / stick lift | 53 poke and 188 stick-lift tunables (`gDSK_OldPokeError_…`, `gAllowStickLiftOnHands…`) | |
| | Sliders: `gPokeEffectSlider`, `gPuckControlSlider` | |
| Pass interceptions | `gAddDefensiveInterceptionError` | true |
| | `gAlwaysInterceptScale` | 1 |
| | Other levers: 138 `…Intercept…` | |
| Michigan / lacrosse | 106 `…Lacrosse…` | |
| | Goalie reaction tables `gGoalieLacrosseDeke…ReactionTime(Human)` (type 14) | |
| | `gGoalieLacrosseDekeChancePositioningScale` | |
| Goalie tether | `gGoalieTether…` (10 scalars) | |
| | Type-14 tables `gGoalieTether…` | |
| Glass breaks | `gGlassBreakCheckProbability` | 0.05 |
| | `gGlassBreakPuckProbability` | 0.1 |
| | `gMaxNumGlassBreaksPerGame` | 1 |
| | `gAITuneAIHittingTuneGlassBreakChance` | 0 |
| Controls (Total Control, Protect Puck mapping, vision icons) | Input/UI, not tunables | Out of scope |

The safe path stays the same: ≤10 values per batch, inside [min, max], boot a match before adding more. Never
`tunables-match`.

## 4. Applied (2026-11-24)

- Option A is in the game as ModStore entry `4d7c573cfad1`: kind `eboot`, name "Sustained Pressure off".
  - It was written with the new CLI command `eboot-bytes`; the build is in `Cli/bin/pressure`.
  - Read back: the byte is `EB`, and the eboot SHA1 is d552d54bd12123b53a0cb75189e288b942f0abe7.
- Restore it from the Mods page, or run `eboot-bytes` (same name) to re-apply it.
- Not yet tested in a match.
- **Confirmed in game 2026-11-24 by the user:** the pressure meter is gone.

## 5. Issue fixes batch 1 (applied 2026-11-24, ModStore 8bbcebe28c34, kind `tuning-set`)

| Value | Before | After |
|---|---|---|
| `gGlassBreakCheckProbability` | 0.05 | 0.01 |
| `gGlassBreakPuckProbability` | 0.1 | 0.02 |
| `gGoalieFatigueBaseEnergyReductionOnSaveAttemptCPU` | 25 | 12.5 |
| `gGoalieFatigueBaseEnergyReductionOnSaveAttemptHuman` | 25 | 12.5 |
| `gAddDefensiveInterceptionError` | true | false |

- Written with the new CLI command `tunables-set`:
  - at most 10 values, each in [min, max];
  - no constants placed in padding;
  - refuses to overlap other mods;
  - reads every value back before writing.
- Not yet tested in a match.
- Left out: the zero values (`vxorps` loads) can't be set this way: `gPenaltyChanceCharging`,
  `gPenaltyChanceHipCheckInterference`, `gFatigueShotAccuracyMaxPenalty`, `gFatigueShotPowerMaxPenalty`.
- 2026-11-24: the user says batch 1 "looks good" (boots and plays; whether each value takes effect is not checked).

## 6. Hitting fixes batch 1 (applied 2026-11-24, ModStore 66b44c5d2f4f, kind `tuning-set`)

| Value | Before | After |
|---|---|---|
| `gPreContactMotionMaxSpeedPosDelta` | 0.07 | 0.035 |
| `gPreContactMotionMaxAngleAdjust` | 0.3 | 0.15 |
| `gPreContactMotionMaxCheckSpeed` | 0.3 | 0.25 |
| `gCheckPreContactMotionCollisionRadius` | 0.4 | 0.3 |
| `gMinSpeedForTier2Miss` | 0.25 | 0.35 |
| `gMinSpeedForTier3Miss` | 0.3 | 0.4 |
| `gPlayerClassMinHitMagnitudeForKnockedOffPuck` | 0.1 | 0.05 |

- Targets complaints: one-button lunge and tracking, wild follow-throughs on misses, the puck sticking through hits.
- Not yet tested in a match.
- 2026-11-24: the user says hitting batch 1 "looks good" (boots and plays; whether each value takes effect is not checked).

## 7. Skating fixes batch 1 + scalar route test (applied 2026-11-24)

**Skating fixes 1** (ModStore 41cd61cc74f2):

| Value | Before | After |
|---|---|---|
| `gPokeTopSpeedClampMultiplier` | 0.8 | 0.9 |
| `gPokeFacingIncrementRatio` | 0.1 | 0.15 |
| `gPokeLungeFacingIncrementRatio` | 0.1 | 0.15 |
| `gPokeExtended(Sideways)Forward/LateralAccControl` (4 values) | 0.9 | 0.95 |
| `gProtectPuckMaxForwardAccelControl` | 0.5 | 0.65 |

- Targets the defensive skill stick "turning in sand" and clunky Protect Puck.

**Scalar route test** (ModStore 94531285f4da): `gSkateModelMaxSpeed` 0.29 → 0.35.

- Proves whether scalar patches take effect on 1.60:
  - if skaters are visibly faster in a match, the route works; then restore this entry;
  - if not, every scalar batch is inert on 1.60.
- Not yet tested in a match.
- **Proven 2026-11-24:** with the test entry, the user saw "everyone is way too fast". Scalar tunable patches do take
  effect on 1.60. The test entry is restored (top speed back to 0.29). It stays in the list, not applied.

## 8. Stumble fixes 1 + Hit power -10% (applied 2026-11-24)

**Stumble fixes 1** (ModStore d1042c2ca44c):

| Value | Before | After |
|---|---|---|
| `gAllowStickIncidentalBodyContact` | true | false |
| `gGPMinHitMagnitudeForHardStumble` | 0.42 | 0.6 |
| `gCoolDownBetweenStumbles` | 5 | 30 |
| `gMaxTripChanceScaleBlade` | 1 | 0.5 |
| `gMaxTripChanceScaleShaft` | 1 | 0.5 |
| `gMinTripChanceScaleShaft` | 1 | 0.5 |

- Targets stick-from-behind stumbles (the user's screenshot of a reach from behind).

**Hit power -10%** (ModStore 849640859dd4, kind `eboot`): the `gHitPowerSliderForPhysics` curve (file 0xA7B9280,
data VA 0xAA5C760) × 0.9.

- Points: 0.5 0.9 0.95 1 1.05 1.1 1.5 → 0.45 0.81 0.855 0.9 0.945 0.99 1.35.
- This is the **first data-route (curve) write**. Whether curves are read live is not yet proven in game.
- Remaining stumble tables, if needed (data route): `gStumbleNotFallingMinSlider`, `…Tough_S/Z`, `…ProtectNetZ`,
  `gStumbleFallMinSlider`.
- **Reverted 2026-11-24:** the user found hits "overpowered" (players flattened, helmets off) with both entries in, so
  both were restored and the state is NotApplied. Which one caused it is unknown:
  - the hit power curve may act inversely (a divisor);
  - or raising `gGPMinHitMagnitudeForHardStumble` may push hits past the stumble tier into falls.
  - Test each alone before retrying.

## 9. Penalty and puck fixes 1 (applied 2026-11-24, ModStore 1f329959f9f4)

| Value | Before | After |
|---|---|---|
| `gPenaltyChancePoke` | 1 | 0.75 |
| `gCPU_RepeatedHooksGetDisciplineCheck` | false | true |
| `gAllowStumbleInterferenceOnIncidental` | true | false |
| `gPickupsFramesFromRecentPuckLossForNoWarp` | 12 | 30 |

- Targets auto-trip calls from pokes, AI penalty spam, incidental interference calls and puck magnetism.
- Not yet tested in a match.
- Hitting is on hold per the user (shoulder checks later).
- 2026-11-24: the user says it "plays good" with this batch in.
