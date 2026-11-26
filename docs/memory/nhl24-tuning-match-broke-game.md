---
name: nhl24-tuning-match-broke-game
description: "NHL 24 \"Match NHL 27\" tuning broke the game per the user; restored 2026-11-04, don't re-apply or re-run it"
metadata:
  node_type: memory
  type: project
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-11-05T04:02:10.467Z
---

On 2026-11-04 the user said the NHL 24 tuning values copied from NHL 27 (Tuning page's Match NHL 27 / `tunables-match`) were breaking the game, and asked for the originals back. The `tuning eboot.bin` entry in `%LOCALAPPDATA%\NHL27Kit\mods-nhl24` was restored with the clibuild CLI: all 478 ranges hold the original bytes, and the entry is kept, not applied.

**Why:** The user saw it break in game; which values caused it isn't known.

**How to apply:** Don't re-apply the entry or re-run the match without being asked. To change that list, use the CLI in `$TEMP/claude/clibuild`, since the repo's CLI reads the repo's `mods-nhl24` instead. Related: [[nhl24-real-life-calibration]], [[nhl27kit-project]].
