---
name: utah-arena-port
description: "Utah NHL 27 arena in NHL 24 (level arizonageneric_22) — what's confirmed, the tricks that worked, what's open (PLAN §35)"
metadata:
  node_type: memory
  type: project
  originSessionId: d892aae3-bd95-4112-9475-ce6e24ee1ff2
  modified: 2026-11-26T06:14:34.020Z
---

Utah's NHL 27 arena is ported into NHL 24 level `arizonageneric_22` (PLAN.md §35, tools/arena-utah/*.json, CLI `arena-port`).

Confirmed in game by 2026-11-26:
- the bowl (identity placement)
- the crowd
- the jumbotron mesh
- the mirrored +x suites (NHL 27 has none on its camera side)
- white surfaces fixed: glass dropped, metal moved to cloth_canvas_black
- Delta Center / UTAH MAMMOTH screen art on the jumbotron

Not yet checked in game:
- hanging lamps raised to 39.5 m
- corner `lightmapMirror +z`

As of 2026-11-26, Utah's home arena points at Rice-Eccles (arenaid 904 = arena index of level 904; [[riceeccles-winter-classic]]). Level 22 only shows in game once exhibitionteams[22].arenaid is set back to 22.

Still open: end-wall branding (needs an NHL 27 reference shot from the user).

Lessons:
- **Per-level textures:** a level's MeshVariationDatabase binds textures per mesh material by import. `arena-port mvdb-tex` repoints one import to a texture only that level uses (here the Arizona scoreboard state sheets, which sit in level 22's bundles), then `retexture` that sheet. Shared textures share one chunk, so never retexture them.
- **Placements:** level layers keep cooked copies of prefab transforms. Edit the layer (e.g. `environment_common`), not the prefab. `arena-port lift` does this.
- **Startup failure:** "Insufficient system resources" at startup means low free memory, not a mod problem.

**Why:** each of these cost an in-game round with the user, who gets frustrated by repeated misses ("still floating").
**How to apply:** verify the asset the game actually reads, offline, before asking for another screenshot. Related: [[nhl27kit-project]], [[nhl24-new-kit-folders]].
