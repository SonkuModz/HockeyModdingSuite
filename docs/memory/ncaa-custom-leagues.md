---
name: ncaa-custom-leagues
description: "NCAA league in NHL 24 (league 29, 62 teams, logos) confirmed in game 2026-11-23; what works and the user's choices"
metadata:
  node_type: memory
  type: project
  originSessionId: 62c775a6-a64c-4329-8876-b51f86581758
  modified: 2026-11-24T01:26:44.488Z
---

NHL 24 has a Play Now league 29 "NCAA" from HMS: one ModStore entry, kind `league` ("NCAA league"), over nhl_game.db, initial_experience.db, eng_us.db plus loose logo files. Details in NHL27Kit PLAN.md §30.

Confirmed in game on 2026-11-23:
- All 7 conferences, 62 teams (all in one group 24). 16 sit in the Aces rows, 46 in *added* team rows (indices 721-766), and the added rows DO show and work (contradicts the old custom-leagues.md warning).
- Team logos work through the kettle fallback: art ids 2901+, GNFs at `user/download/CUSA37933/nhl/ui/kettle_assets/teamlogos/<style>/t<artid>.gnf` (= /download0). /temp0 (user/temp/CUSA37933) is wiped at every start; don't use it.

The user's choices: one NCAA league with conferences (not 7 leagues); added rows over Pro-Am rows; ratings flat 60-70. On 2026-11-23 they asked for a *new* league-logo texture (l29, added to the index, not overwriting another league's), then centre-ice textures next.

League logo (2026-11-23, applied; user checked: it does NOT show in game, and they said to leave it for now): ModStore entry kind `texture` "league 29 logo", CLI `league-tex [--apply|--restore|--verify]`. 10 new bundles `gen4b/ui/textures/common/leaguelogos/<variant>/l29_launch_ui_textures_common_brt` copied from l20 (Core/AddedTextures.cs), added to Patch/Gen4b/contentlaunchsb.toc by Core/TocAdder.cs (rebuilds both perfect-hash maps, moves guids/names/tree/entries to the end; chunk entries and blocks stay put for the Center Ice Mod). League logos are picked by stockleagues.artid (league 29 had artid 23), not the league id: the league installer now sets artid = league id (LeagueOptions.LeagueArtId). Source PNG leagues/ncaa/logos/leagues/ncaa.png (from the user's refs/NCAAHANDOFF). Centre ice will need the same writer: a second TOC entry stacks on this one's header, so restore order matters.

Lessons: `league` re-apply needs all conferences (the CLI now defaults to every pack conference; once it dropped to 7 teams). The emulator log showed no probe for league logos, so the UI may draw them from somewhere other than the leaguelogos bundles (unknown).

**Why:** the loc table is near its u16 row cap, so 474 name keys take over circuit/defunct-club keys (never live-team leftovers like LAK/SJS/ARZ).
**How to apply:** build on this approach; never overwrite existing textures. Related: [[nhl27kit-project]].
