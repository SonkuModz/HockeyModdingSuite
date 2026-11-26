---
name: nhl27kit-no-db-cache
description: "NHL27Kit must read DBs/rosters fresh from the game files every time, never from cached copies; roster editor shows in-game portraits"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-10-27T12:51:50.128Z
---

Never use cached copies of any database or roster in NHL27Kit. Read them fresh from the game files on every open, reload, save and selection, so edits made anywhere stick and show up. Selecting a player in the roster editor must show the player's in-game portrait (ui/textures/common/playerportraits/p<artid>).

**Why:** the user asked for this on 2026-10-27. They want what the kit shows to always match the game, whoever changed it.

**How to apply:** go through `Legacy.OpenDb` / `ModService.OpenDbAsync` each time. Don't keep extracted DB files in %TEMP% or the kit's folder as a source. Related: [[nhl27kit-project]], [[nhl27kit-autonomous-run]].
