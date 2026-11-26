---
name: ihn-broadcast-package
description: "IHN (Ice Hockey Network) custom broadcast package for NHL 24 — navy/gold, angled, team logos; concept before each build"
metadata:
  node_type: memory
  type: project
  originSessionId: f6ede1d4-45c3-4b02-bc14-c53fe8a06c96
  modified: 2026-11-25T04:31:12.894Z
---

Started 2026-11-24. The user's own broadcast package for NHL 24 (shadPS4): **IHN, Ice Hockey Network**. Navy #13294B and gold #C9A54C (the HMS colours under a different name, so don't use the HMS logo), an angled, sporty style, and **team logos** in the team boxes, the PP tab and the goal banner. Use the game's own team logos (ui/textures/common/teamlogos/large/tN), never the centre-ice logos. The user then shared their own reference image (NHL27Kit/previews/ihn/user-reference.png: centred bug, bevelled gunmetal frame, glowing team panels, raised clock, SOG row, PENALTY TIME, POWER PLAY tab). That is the direction now, over concepts A-J and G1-G6. The user picked **R1 Gunmetal** (2026-11-25), first top centre, then moved it to the **top left** after seeing it in game ("top center doesnt look good"). It is built in tools/scorebug-ihn and saved as App/Assets/scoreclock-ihn.swf (installed with `scorebug24 --movie ... --name IHN`). Logos are fitted and centred in their slots. The goal animation has **4 unique styles that take turns** (Burst, Slash, Spotlight, Strobe; cfg goalStyle 0 = rotate, 1-4 = fixed) with 128 px logos, because the user asked for the goal animations to differ. Phases: scorebug, lower thirds, intros, end of period, replays, stat overlays, and menus LAST. The inventory and plan are in NHL27Kit/docs/nhl24/broadcast-package-ihn.md and PLAN §33. The scorebug source is tools/scorebug-ihn (a fork of the Sportsnet clock; BASE = App/Assets/scoreclock-sportsnet.swf, and the build reproduces it byte for byte).


**Score clock size limit:** the movie must compress to at most 162,761 bytes, the size of its slot (the last file in its legacy chunk). A bigger one gets moved, and the game then shows no score clock at all, while the tool's read-back says "Not a Scaleform". tools/scorebug-ihn/build.py empties only EA's decorative animation sprites (strip.py DROP list), giving about 144 KB, and prints the size. Don't empty the other linked symbols: with all of them emptied, shootout mode crashed while Sportsnet (intact) worked. Check "read back: same movie" after every install. In game, loader.ImageLoader resets its clip's scale to 1 whenever it redraws (after goals, etc.), so size logos on a wrapper Sprite you own around the clip, never on the clip itself (the textures are 256x256). No pixel reads (getColorBoundsRect) on game textures; a build with them crashed the game, cause unconfirmed.

**Why:** the user asked for visual concepts as the work goes, and screenshots from the game.

**How to apply:** for each piece, send a concept PNG (previews/ihn/) for an OK before building, then a game screenshot. See [[nhl24-sportsnet-scorebug]], [[nhl27kit-project]].

**Replay wipe (Phase 3, 2026-11-25):** texture markers showed nothing in any wipe (restored). The wipe the user sees most is the 3D Play Now flat "EA SPORTS" wipe; its logo is meshes (medallion/easports/alt under content/worlds/wipes/wipe_flat_logos/meshes/unified). The user's own IHN logo SVG (tools/wipe-ihn/ihn-logo.svg; shield, stick, IHN, HOCKEY NETWORK) went in as a one-colour cut-out via new CLI `mesh24` (3 `mesh` entries) plus a navy background guess via `ebx-floats` (1 `ebx` entry). Confirmed in game by the user 2026-11-25 ("looks good"). Next phase: the pre-game intro screen. Details in PLAN §33.

**Pre-game intro (Phase 4, 2026-11-25):** the user picked concept B Broadcast Band; built as a code-drawn team_intro.gfx (tools/intro-ihn) and installed as "Team intro: IHN" via new CLI `ui24`; confirmed in game by the user 2026-11-25 ("Looks good"). Next phase: end of period. team_intro.gfx has zero free room (16,815 B max). The intro and the score clock share one UI index chunk: the clock entry reads "Changed" after the intro install (still in game); restore the intro first, then the clock. Details in PLAN §33.

**End of period (Phase 5, 2026-11-25):** the user picked A Score Band; built in tools/eop-ihn (shares drawing code with the intro via make_tpl.py) and installed as "End of period: IHN"; confirmed in game by the user 2026-11-25 ("Looks good"). Next: stat overlays, menus last. Restore order across the three UI entries: End of period → Team intro → Score clock (they share the UI collector's index chunk; EOP also shares the clock's data chunk).

**Line on the ice (2026-11-25):** horizontal navy/gold bar via faceoffstrategy.layout + linefatiguehome.layout x (tools/linefatigue-ihn), confirmed in game by the user 2026-11-25 ("looks good"); home not mirrored (one widget). Next: line change menu (linechangedisplay.layout) in the same colours. CLI commands default to NHL 27: always pass --game for NHL 24. Details in PLAN §33.
**Line change menu (2026-11-25):** navy/gold recolour of linechangedisplay.layout (tools/linechange-ihn), confirmed in game by the user 2026-11-25. Stat overlays: user keeps EA's as they are (2026-11-25, no intent to edit). Only menus left, and only if the user asks. Restore it before the Line on the ice entries.
**Live shots feed (2026-11-25):** game sends no shot totals to the scorebug; lua VM (over unused widgettester.lua) + hidden label + movie pollFeed, installed, waiting for user check; status text shows in middle box. Never reinstall a Changed entry under the same name (ModStore.Add drops the old one); use ui24 --entry. PLAN §33.

2026-11-25: the user asked to drop the shots feed, hide SOG, and show the power play time in the team's box. The feed entries were restored (that also fixed the away line panel the feed tests broke). The new scorebug "scoreclock.gfx (pp box)" (App/Assets/scoreclock-ihn-pp.swf) is installed and waiting for the user's in-game check. Don't retry a shots feed unless the user asks.

2026-11-25: the title screen is the user's own clip 1125, confirmed in game. Movie replacement needs every track's table rewritten: EA movies carry an "eaa" alpha track and a sound table with a fixed packet count. Build with tools/movie-ihn/build_title.py, then install with `movie-replace --webm --apply`. Main menu is next.

Main menu (2026-11-25): user chose a top horizontal bar (concept D); confirmed "looks good" in game. Built by
NHL27Kit/tools/menu-ihn/build.py; background is the user's 1123.mp4 on movie nhl24_main_menu_s6. Lessons: a
HORIZONTAL CollectionView needs checkItemWidths; parent TabsViewSections clips at its width.
Pause menu (2026-11-25): user picked jumbotron concept D after asking for varied, non-EA layouts (10 drawn);
End-of-period/game Home (hubhomesection3stars) also jumbotron, confirmed.
Home page confirmed. tools/pause-ihn/build.py. Game Stats / Manage Teams / Coach Feedback tabs still EA style.
