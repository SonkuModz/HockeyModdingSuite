# UI scripts and the debug menu

The front end is driven by editable legacy files: `.nav` and `.menu` (JSON) and `.lua` sources under
`nhl/ui/features/...`. Edited copies go in a mod list entry's `Legacy` list like a database. Exported originals
are in `test\ui`.

## Debug menus (confirmed in game)

- NHL 24 uses the **new** main menu (`features/mainmenu/vvms/mainmenu.lua`, with the FOR YOU/ONLINE/OFFLINE/MORE
  tabs), not `homepage.lua` (the legacy flow).
- `mainmenu.lua` line 182, `o.IsUiDebugMenuOptionEnabled = true`, adds **UI DEBUG MENU** under FOR YOU. It fires
  `evt_debug_menu_new` (`debugmenuhub.vvm`). A second added entry, "UI Debug Menu (Classic)" (`evt_debug_menu`,
  the `debug.menu` list), also works, but its dark text is hard to read. Edited copy:
  `mods\debugmenu\mainmenu.lua`.
- `homepage.lua` line 219 (a "HACKED TILE" shown only when `DEBUG_MENU_SHOWN`) is in `mods\debugmenu` too, for the
  legacy flow.

## What doesn't work

- The `sys_dbg_autodebugmenu` / DebugDisplay tweakables are never read in retail.
- The Frostbite engine menu (`-Game.DebugMenuOnLThumb true` in `commandline.txt`) boots, but L3 does nothing.
  The in-gameplay debug menu service is stripped. Remove the `commandline.txt` afterwards (see
  [EMULATOR.md](EMULATOR.md)).
- shadPS4 doesn't pass the keyboard to the game.

## Writing UI files

The legacy writer rule applies: the index chunk holds a `[u64 raw size][guid]` record per data chunk. A stale size
crashes the game on the first loading screen. `LegacyWriter` updates them (see
[DATABASE_EDITING.md](DATABASE_EDITING.md)).
