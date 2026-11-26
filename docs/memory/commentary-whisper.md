---
name: commentary-whisper
description: NHL 27 → NHL 24 commentary swaps paired by Whisper transcripts; paused by the user on 2026-11-23; state and quirks
metadata:
  node_type: memory
  type: project
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-11-23T07:22:49.455Z
---

NHL 24 commentary swaps are paired by what the takes *say* (faster-whisper small, on the GPU), not by keyword IDs. Keyword→surname/city joins proved wrong (the Ovechkin take said "Allen").

**Paused by the user on 2026-11-23** ("lets hold off on this for now"). Don't resume unless they ask.

Applied in NHL 24, all restorable:
- 30 city takes
- 523 "to <player>" surnames, inside the Center Ice Mod's cas_03 block: restore these before the CIM
- 165 pilot save takes
- 2,592 `bm_` play-by-play takes as 223 "WAVE [batch]" entries, outside the CIM ranges

User-confirmed in-game: LA, New York, Crosby, and the save calls.

Left to do:
- analyst `ba_`/`bma_` waves
- shared-chunk waves (`bm_goal_gen` and 4 others)
- 755 slots with no NHL 27 take short enough
- fuzzy-matched surnames

Notes and files:
- Notes: NHL27Kit/docs/nhl24/commentary-research.md
- Files: export/commentary-*, export/whisper-cities
- New CLI command `apply-sounds --list` (built in Cli/bin/claudecli2, not committed)

Quirks: run with `python -P`, not `-I` (faster-whisper is in the user site-packages). Pass numpy arrays to Whisper, since its av decoding is broken. One ear-confirmed clip per new kind of swap before bulk (see [[nhl27kit-project]]).
