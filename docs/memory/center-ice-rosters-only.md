---
name: center-ice-rosters-only
description: "How a newer Center Ice Mod's rosters (only) go into NHL 24 without installing its package; entry 5ede75a9b929"
metadata:
  node_type: memory
  type: project
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-11-25T19:29:25.123Z
---

2026-11-25: the user wanted only the rosters from Center Ice mod 2026.11.24 ("nothing else. just the rosters"). The game keeps Center Ice 2026-11-15 installed as a package entry. The new package's databases were read with `tools/cims-view` (it rebuilds stock TOCs from the mod list's old bytes, extracts the package's CAS data, and reads through `GameLayout.ReadRedirect`). Then `cims-rosters DIR --name ... --apply` swapped in the player and roster tables and kept the NCAA league players (ids 40000-56499). Entry "Center Ice 2026.11.24 rosters" (5ede75a9b929) is applied and reads back the same. It hasn't been checked in game yet (PLAN §34).

**Why:** the user wants future Center Ice updates handled the same way unless they say otherwise; a full package install would replace the 11-15 install and other mods' bases.
**How to apply:** for the next update, run cims-view on its package, then cims-rosters with a new name. Restore the older rosters entry first. Restore this entry before the NCAA league or kit entries ([[ncaa-custom-leagues]], [[nhl24-new-kit-folders]]).
