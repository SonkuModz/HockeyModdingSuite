---
name: nhl24-pressure-off
description: "NHL 24 Sustained Pressure turned off by a 1-byte eboot ModStore entry, applied 2026-11-24"
metadata:
  node_type: memory
  type: project
  originSessionId: 91f0bcfd-1c23-4165-a3ca-2d7cfe82c65b
  modified: 2026-11-25T01:21:47.626Z
---

The user wanted NHL 24's Sustained Pressure system off. On 2026-11-24 it was applied as ModStore entry
4d7c573cfad1 (kind `eboot`, "Sustained Pressure off"). The patch is at eboot file offset 0x2CAEBD4, `74` → `EB`:
AddPressure (VA 0x2C5CE30) always returns, so the pinned and adrenaline states never happen.

It was written with the new CLI command `eboot-bytes`. The user confirmed in game on 2026-11-24 that the meter is gone.
Details: NHL27Kit/docs/nhl24/pressure-system-and-issues.md.

**Why:** the eboot's other gameplay patches are untested on 1.60, and [[nhl24-tuning-match-broke-game]].
**How to apply:** if NHL 24 misbehaves after this, restore this entry first.

Issue fixes batch 1 (ModStore 8bbcebe28c34, kind `tuning-set`, 5 values: glass breaks, goalie fatigue, interception
error) was written with the new CLI command `tunables-set`. It boots and plays (2026-11-24). Next
candidates are the zero-valued tunables (charging, hip-check interference, fatigue on shots), which need a code
change, not a constant swap.

Hitting fixes batch 1 (ModStore 66b44c5d2f4f, 7 values: pre-contact lunge, miss stumbles, puck knock-off) was applied
2026-11-24. It boots and plays.

Skating fixes 1 (41cd61cc74f2) and the 'Scalar route test' (94531285f4da: gSkateModelMaxSpeed 0.29 -> 0.35) were applied
2026-11-24. Visibly faster skaters would prove scalar patches work on 1.60. Restore the test entry after the check.

**Scalar route proven on 1.60 (2026-11-24):** with gSkateModelMaxSpeed at 0.35, the user saw everyone way too fast, so
tunables-set patches take effect. The test entry is restored and kept in the list, not applied.

Stumble fixes 1 (d1042c2ca44c) and Hit power -10% (849640859dd4) together made hits overpowered. Both were reverted
2026-11-24. Next time, test each alone. Don't re-apply them together.

Penalty and puck fixes 1 (1f329959f9f4: poke penalty chance 0.75, CPU hook discipline check, no incidental-stumble
interference, 30-frame no-warp after puck loss) was applied 2026-11-24. The user says it plays good. Hitting is on hold
(shoulder checks later).

On 2026-11-24 the user put everything on hold until further notice: shoulder checks, the zero-value code changes and the
table/curve writes. Don't start them unasked. The summary of changes is in NHL27Kit/docs/nhl24/gameplay-changes.md.

On 2026-11-25 the NHL24 Tuner v1.0 'Realistic Sim' preset (110 of 115 values, hitting included, the user's choice) was
applied as one entry, c800c28017ad. tunables-set gained --set-file/--skip/--max/--clamp for it. If the game misbehaves,
restore it first.

Correction 2026-11-25: the user wanted every tuner value exactly. All 115 are now set (entry 5b858070ace3), including 3
above the game's max and goalie fatigue 15. Issue fixes 1 was rewritten as 531e4b1dbe66 (glass x2 + interception).
**Why:** the user said 'change all, set the values from the tuner'.
**How to apply:** when the user names a preset or source, apply it exactly; don't keep our values over theirs.
