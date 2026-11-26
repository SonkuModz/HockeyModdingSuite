# IHN (Ice Hockey Network) broadcast package for NHL 24

The user's own broadcast package: every in-game broadcast graphic restyled, then the menus last.

- **Game:** NHL 24 (shadPS4).
- **Name:** IHN, Ice Hockey Network. The corner mark reads `IHN`.
- **Colours:** navy and gold (the HMS palette under a different name). Navy about #13294B (deep #0B1A33), gold about
  #C9A54C (light #E8CB7A, dark #9C7A2E), white text. Team boxes keep their team colours.
- **Style:** angled and sporty: slanted edges, stripes, energetic wipes.
- **Fonts available without adding any:** the score clock movie embeds `Stratum1-Bold` (condensed, sporty) and
  `NN Rektorat STD Heavy` (wide, heavy). Both suit the look.
- The user wants a **visual concept for each piece before it's built**, and a screenshot from the game once it's in.

## Inventory: what draws the broadcast UI (read from the game 2026-11-24)

The overlays are named in `nhl/ui/definition.overlays` (uicommon_nhluidata). Each has a `.nav`
(`features/overlays/X.nav`), a `.vvm` (`features/overlays/vvms/X/`) that names a `.layout` (JSON), a `.lua` view model
and a `.dd`. A layout's `"type"` asset names resolve through `nhl/ui/definition.kettle.assetpaths`
(uikettle_nhluidata): `swf_assets/...gfx` movies (uikettle_launch_swfassets, with their `.gnf` images) or
`img://fb/ui/textures/...` textures (TextureAssets). Text and data widgets are AS3 classes in `nhl/ui/lib/nhluidata.swf`
and `nhl/ui/lib/external/tin_nhl.swf`.

| Graphic | Overlay / layout | What draws it | Text in the movie? | How it can be changed |
|---|---|---|---|---|
| Score clock (bar, PP/EN tab, 4-on-4, shootout, final, goal banner) | GameClock → `scoreclockoverlay.layout`, type `score_clock` | `overlays/scoreclock/scoreclock.gfx` + twin `score_clock.gfx` (AS3, 2 fonts, 33 gnf) | yes, all in code | **Proven:** whole-movie swap (`scorebug24 --movie`). Sportsnet is applied now. |
| Lower thirds (player info, player stats, team info, team stats, goalie bio) | PlayerInfo, PlayerStats, TeamInfo, TeamStats, GoalieBioInfo → all `lowerthird/lowerthirdoverlay.layout`, types `lower_thirds` + `bg_glitch` | `overlays/lowerthirds/lowerthirdsoverlay.gfx` (animated panels, no text, 27 gnf) + `overlays/common/bgglitchanimation.gfx` + textures `lower_third_*` (`ui/textures/common/widgets/lowerthird/*`) | no: text comes from layout widgets | movie swap (to test), textures, layout JSON |
| Goal scorer lower third | PlayerInfoGoal (layer 2) | the lower-third path above (to confirm) | no | as above |
| End of period / intermission panel | EndOfPeriod → `endofperiod.layout`, type `end_of_period` | `overlays/endofperiod/endofperiod.gfx` (91 gnf images) | no | movie and image swap; text via layout |
| Team intro | TeamIntro → `teamintro.layout`, type `team_intro_swf` | `overlays/teamintro/team_intro.gfx` (50 gnf) | no | movie and image swap |
| Player intro banners | PlayerIntro → `playerintro.layout` (PlayerBanner1-4) | layout widgets (AS3) + textures | widgets | layout JSON, textures |
| Clear player intros | — | `overlays/clearplayerintros/clear_player_intros.gfx` (1 font) | yes | movie swap |
| Game intro logo | GameIntro → `intrologooverlay.layout` (MoviePlayer) | a video | — | `movie-replace` |
| City intro sponsor | CityIntroSponsor → `cityintrosponsor.layout` | `nhl/data/common/movie/cityintro/nbc_ad.mkv` + image ad | — | `movie-replace`, texture |
| Title card (player of the game) | TitleCard → `titlecard.layout` (PotpgWdgt) | widgets | widgets | layout, textures |
| Star comparison | StarComparison → `starcomparison.layout` | widgets (BrandedHeader, BrandedCupLogo, Portrait) | widgets | layout, textures |
| Upcoming games / team announcement | UpcomingGames, TeamAnnouncement → layouts | `upcominggamesbackground.gfx`, `teamannouncementbackground.gfx` + widgets | widgets | movie, layout |
| Broadcast bug (corner logo) | BroadcastBug → `broadcastbug.layout` | widgets (`ones_logo`, banners) — mostly modes other than 5v5 | widgets | layout, textures |
| Instant replay HUD | InstantReplay (layer 5) → `instantreplay.layout` | button callouts and camera text (widgets) | widgets | layout |
| Replay of the game / POTG / end-of-game replay | — | `replay/replayofthegame.gfx`, `replay/potg_background.gfx`, `replay/endofgame/replay{oneteam,threestars,vsteams}.gfx` | no | movie swap |
| Highlight frames | — | `highlight/highlight.gfx`, `highlight_flare.gfx`, `highlightrectangle.gfx` | no | movie swap |
| Sim to next shift | SimToNextShift | `overlays/simtonextshift/simtonextshift.gfx` | no | movie swap |

Not counted (other modes): hut, threes, proam, gauntlet, rush, beapro, guest in the booth, fighting, strategies,
fatigue, energy, coach feedback.

**To locate:**
- **The in-play replay wipe/transition** (the logo stinger going into and out of a replay). It is not one of the gfx
  above. Look in the movies (`movies`), EBX (`ebx-grep replay`), and `overlaydata/`.
- **The regular-mode goal banner outside the score clock.** Only hut/proam/threes have their own `goalscored*`
  overlays; in 5v5 the goal animation is inside the score clock (Sportsnet's is), plus PlayerInfoGoal.

**Contract to keep:** before replacing any movie, record what the game calls on it (its document class's public
methods and frame labels, and the layout's calls). The score clock's is in the Sportsnet source (same API as the
ESPN+ clock).

## How proven each write path is (NHL 24)

1. **Whole score clock swap:** proven (Sportsnet, in game).
2. **`.layout`/`.lua` edits:** proven only by the debug-menu mod (`mods\debugmenu`).
3. **Textures:** the existing texture tooling.
4. **Other gfx movies:** the same legacy writer, but never tried on a movie other than the score clock. The first test
   of phase 2.
5. **`tin_nhl.swf`/`nhluidata.swf` code:** unproven on 24, and crashed matches on 27. Last resort only.

## Phases

1. **Scorebug (IHN score clock).** Fork `tools/scorebug-sportsnet` into `tools/scorebug-ihn` (the Sportsnet source
   stays as it is, and stays installable from the Scorebug page). Same API and settings block. Navy and gold angled
   bar, IHN wordmark drawn as vector paths, PP/EN tab, 4-on-4, shootout, final, goal banner animation. Concept first,
   then Ruffle preview, then in game (installing it restores the Sportsnet entry first; Sportsnet can be put back from
   the Scorebug page).
2. **Lower thirds** (player info, goal scorer, stats, goalie bio). First test a movie swap of
   `lowerthirdsoverlay.gfx` with the game's own movie re-saved (proves the path), then restyle it, the glitch movie and
   the `lower_third_*` textures.
3. **Intros:** team intro movie and images, player intro banners, game intro video, city intro sponsor.
4. **End of period / intermission** panel.
5. **Replays and transitions:** locate the replay wipe; restyle the replay-of-the-game, POTG, end-of-game replay and
   highlight movies; the instant replay HUD.
6. **Stat overlays:** title card, star comparison, upcoming games, team announcement, broadcast bug.
7. **Menus (last):** the front end (`refs/UI_SCRIPTS.md`: `.nav`, `.menu`, `.lua` under `nhl/ui/features`, and
   `definition.styles`), restyled in IHN navy and gold.

Each phase: a concept for the user, then build, then a screenshot from the game.
