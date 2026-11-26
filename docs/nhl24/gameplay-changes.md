# NHL 24 gameplay changes (2026-11-24)

What has been changed in NHL 24 (PS4 1.60, `NHL24-PC-v0.9.3b\game-UPDATE\eboot.bin`).

- Every change is its own entry in the HMS Mods page (store: `NHL27Kit/mods-nhl24`). Restore on that page, or run
  `nhl27kit restore "<name>"`, to put the original bytes back. Close the game first.
- Research and addresses: [pressure-system-and-issues.md](pressure-system-and-issues.md).
- Source complaints: `Downloads\NHL 24 Issues.txt`.

## Applied (in the game now)

### 1. Sustained Pressure off
Entry `4d7c573cfad1` (kind `eboot`). **Confirmed in game:** the meter is gone.

| What | Before | After |
|---|---|---|
| eboot file offset 0x2CAEBD4 (VA 0x2C5CE54) | `74` (je) | `EB` (jmp) |

- The pressure-meter function now always returns. The meter never fills, so Full Pressure never starts.
- No more defender rating drop (×0.8) or team stamina drain (×3 energy loss, no recovery).
- No attacker adrenaline boost and no ice countdown.

### 2. Issue fixes 1
Entry `8bbcebe28c34`. Boots and plays well.

| Value | Before | After | Fixes |
|---|---|---|---|
| `gGlassBreakCheckProbability` | 0.05 | 0.01 | Glass breaking far too often |
| `gGlassBreakPuckProbability` | 0.1 | 0.02 | Same, from pucks |
| `gGoalieFatigueBaseEnergyReductionOnSaveAttemptCPU` | 25 | 12.5 | Goalies tiring fast, then flopping and desperation dives |
| `gGoalieFatigueBaseEnergyReductionOnSaveAttemptHuman` | 25 | 12.5 | Same, for human goalies |
| `gAddDefensiveInterceptionError` | true | false | Passes going through defenders in the lane |

### 3. Hitting fixes 1
Entry `66b44c5d2f4f`. Boots and plays well.

| Value | Before | After | Fixes |
|---|---|---|---|
| `gPreContactMotionMaxSpeedPosDelta` | 0.07 | 0.035 | One-button hits lunging (speed burst before contact) |
| `gPreContactMotionMaxAngleAdjust` | 0.3 | 0.15 | Hits steering toward their target |
| `gPreContactMotionMaxCheckSpeed` | 0.3 | 0.25 | Lunge top speed |
| `gCheckPreContactMotionCollisionRadius` | 0.4 | 0.3 | How far away a check starts aiming at its target |
| `gMinSpeedForTier2Miss` | 0.25 | 0.35 | Wild follow-through when a check misses |
| `gMinSpeedForTier3Miss` | 0.3 | 0.4 | Same, for the biggest miss stumble |
| `gPlayerClassMinHitMagnitudeForKnockedOffPuck` | 0.1 | 0.05 | 2–3 bumps needed to knock the puck loose |

### 4. Skating fixes 1
Entry `41cd61cc74f2`. Boots and plays well.

| Value | Before | After | Fixes |
|---|---|---|---|
| `gPokeTopSpeedClampMultiplier` | 0.8 | 0.9 | Defensive skill stick "turning in sand" (speed cap while poking) |
| `gPokeFacingIncrementRatio` | 0.1 | 0.15 | Turn rate while poking |
| `gPokeLungeFacingIncrementRatio` | 0.1 | 0.15 | Turn rate during a lunging poke |
| `gPokeExtendedForwardAccControl` | 0.9 | 0.95 | Acceleration control with the stick out |
| `gPokeExtendedLateralAccControl` | 0.9 | 0.95 | Same |
| `gPokeExtendedSidewaysForwardAccControl` | 0.9 | 0.95 | Same, stick out to the side |
| `gPokeExtendedSidewaysLateralAccControl` | 0.9 | 0.95 | Same |
| `gProtectPuckMaxForwardAccelControl` | 0.5 | 0.65 | Clunky, slow Protect Puck |

### 5. Penalty and puck fixes 1
Entry `1f329959f9f4`. **Plays good** (the user, in game).

| Value | Before | After | Fixes |
|---|---|---|---|
| `gPenaltyChancePoke` | 1 | 0.75 | Pokes into the skates becoming automatic tripping calls |
| `gCPU_RepeatedHooksGetDisciplineCheck` | false | true | AI taking constant, unprompted penalties (discipline rating now matters) |
| `gAllowStumbleInterferenceOnIncidental` | true | false | Interference called on accidental bumps |
| `gPickupsFramesFromRecentPuckLossForNoWarp` | 12 | 30 | Puck magnetism: loose pucks snapping back to the player who lost them |

## Tried and restored (in the list, not applied)

| Entry | What | Why it's off |
|---|---|---|
| Scalar route test `94531285f4da` | `gSkateModelMaxSpeed` 0.29 → 0.35 | A test only. It proved single-value patches take effect ("everyone is way too fast"), then was restored |
| Stumble fixes 1 `d1042c2ca44c` | No stumble from incidental stick contact, half trip chance, harder contact needed for a big stumble, stumble cooldown 5 → 30 | Together with the hit power change, hits became overpowered. Both restored. Cause unknown |
| Hit power -10% `849640859dd4` | `gHitPowerSliderForPhysics` curve × 0.9 (the first table write) | Same. Test alone if retried. Never re-apply both together |

## On hold (until further notice)

- **Shoulder checks and hit power.** When resumed, change one thing at a time.
- **Values the game sets to zero** (need a code change): `gPenaltyChanceCharging`, `gPenaltyChanceHipCheckInterference`,
  `gFatigueShotAccuracyMaxPenalty`, `gFatigueShotPowerMaxPenalty`.
- **Table values** (the curve-write route): Michigan/lacrosse goalie reactions, goalie tether, the stumble threshold tables.
- **No tuning value exists:** controls (Total Control, Protect Puck mapping, vision icons), pass goals, behind-the-net
  passing, AI positioning.

## Tooling added

New commands in `NHL27Kit/Cli/Program.cs`, built to `Cli/bin/pressure/nhl27kit.exe`:

- **`eboot-bytes --name --at --old --new [--sha1] [--write]`.** Changes a few bytes of the eboot as one entry.
- **`tunables-set --name --set "gA=1;gB=0.5" [--write]`:**
  - at most 10 values, each inside its range;
  - minimum and maximum are left as they are;
  - no new bytes in code padding, only values the eboot already holds;
  - refuses to overlap other mods;
  - reads everything back before writing.

Every change was checked against the emulator's memory patches (`user\patches\nhl24.xml`). None overlap.

**Never run `tunables-match`.** It broke the game on 2026-11-04.

## 6. Realistic Sim preset (applied 2026-11-25)

Entry `c800c28017ad` (kind `tuning-set`). Not yet tested in a match.

**Source:** the community NHL24 Tuner v1.0 (`Downloads\NHL24-Tuner-v1.0\tools\tuner\presets\realistic_sim.json`,
115 values).
- That tool writes into `user\patches\nhl24.xml`. Our copy is written into `eboot.bin` through the Mods page instead,
  because the CIMS entry owns the xml.
- The user chose to apply all of it, hitting included, as one entry.

**110 values set.** Every one reuses bytes the eboot already holds; all read back correctly; nothing overlaps the
emulator patches.

**5 not set:**
- The two goalie-fatigue save values: ours from Issue fixes 1 (12.5) are kept; the preset wanted 15.
- `gShotBlockReactionOccurrenceFrequency`, `gMaxAggressionFactor`, `gStepUpBonusMaxAggression`: the preset goes past
  the game's max of 1. Capped at 1, which is already the default, so nothing changes.

**Note:** "Stumble fixes 1" (not applied) now shows *Changed*, because `gGPMinHitMagnitudeForHardStumble` is set by
the preset. Don't apply it; the store would refuse anyway.

To see each value, run `nhl27kit tunables-set` with the same file. The full list is in the preset json.

### Update 2026-11-25: full preset
The section above is superseded. "Realistic Sim preset" is now entry `5b858070ace3`.

- All **115 of 115** tuner values are set exactly as the tuner has them.
- That includes goalie fatigue 15, plus 3 values above the game's max of 1 (shot block 1.25, `gMaxAggressionFactor`
  1.3, `gStepUpBonusMaxAggression` 1.3), set at the user's request.
- A fresh read of the eboot matches the tuner file 115 of 115.
- "Issue fixes 1" was rewritten as `531e4b1dbe66`, with only the glass break chances and the interception error.
- Not yet tested in a match.
