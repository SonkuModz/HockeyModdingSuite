# NHL 24 deep dive

What is in the NHL 24 dump (`NHL 24 Dump`, PS4, CUSA37933, v1.00), how it compares with NHL 27, and where HMS stands on it. Read on 2026-11-11 from the game files as they are now, so it includes the mods currently applied. Nothing was written.

## At a glance

| | NHL 24 (PS4) | NHL 27 (PS5) |
|---|---|---|
| Data size | 34 GB, 36 CAS files in 7 install packages | not counted |
| EBX assets | 482,971 | 1,154,583 |
| RES resources | 193,291 | 299,823 |
| Chunks | 180,802 | 276,142 |
| Legacy files | 10,931 | 20,618 |
| `nhl_game.db` tables | 119 | 125 |
| Teams (`exhibitionteams`) | 527 | 537 |
| Players (bio table) | 11,675 | not counted |
| Arenas | 548 | not counted |
| English strings | 65,381 | not counted |

NHL 24 has about 40% as many EBX assets as NHL 27. Most of the gap is in UI textures (200,792 against 587,298) and skater content (172,016 against 325,736).

## How the files are laid out

All the data is under `Data\Gen4b\`. There are 8 superbundles, each with a signed TOC:

| TOC | Bundles | Chunks | What's in it |
|---|---|---|---|
| contentlaunchsb | 78,819 | 84,108 (16.5 GB) | Most UI bundles (67,388), plus launch content |
| contentsb | 44,249 | 73,221 (74 GB logical) | Characters, kits, gear, most of the content |
| worldssb | 5,482 | 12,031 (4.7 GB) | Arenas, rinks, ice, boards |
| globals | 42 | 3,283 | Sound banks, music, crowds, the legacy file collectors |
| globalsfull | 767 | 1,010 | Shared content |
| levels/game/game | 1 | 6,765 | The game level |
| movies | 345 | 383 | 376 videos (VP8 WebM) |
| ui | 10 | 1 | UI globals (it has no chunk table) |

`Patch\` is empty. `eboot.bin` is a fake-signed PS4 SELF with 17 bytes patched; `eboot.bin.orig` is the original. `__FMTBackup\` holds the stock TOCs from NHL24Kit's time.

## What the assets are

**EBX by type (top):** TextureAsset 150,286; BundleRefTable blueprints 250,989; NewWaveAsset (sounds) 9,374; PartitionTrackRootData 8,103; ANTEvaluatorDataAsset 7,973; ObjectBlueprint 7,513; LayerData 5,100; SkinnedMeshAsset 4,739; MeshVariationDatabase 4,304; TextureArrayAsset 4,225; RigidMeshAsset 1,582; CompositeMeshAsset 1,534; MovieTexture2Asset 376.

**RES by type:** Texture 154,556; CompiledBytecode (shaders) 20,186; NewWaveResource 9,374; MeshSet 7,855; AssetBank (animations) 455; Enlighten lighting 310; cloth 290; Font 41.

**Where the textures are:**

| Folder | EBX | What |
|---|---|---|
| `content/character/skater/league` | 101,534 | Team kits and gear |
| `ui/textures/common/playerportraits` | 41,118 | Player portraits, `p<artid>`, 512x340 BC3 |
| `ui/textures/common/creationzone` (+ `_xl`) | 53,397 | Creation Zone previews |
| `content/character/skater/player` | 27,537 | Player heads and faces |
| `ui/textures/common/icons` | 24,744 | Icons |
| `ui/textures/common/teamlogos` | 24,726 | Team logos, 18 sizes, `t<artid>` |
| `content/character/skater/shared` | 23,079 | Shared skater parts |
| `content/character/skater/goalie` | 18,999 | Goalie gear |
| `ui/textures/common/teamjerseys` (+ `xs`, `xl`) | 20,241 | Jersey previews in the menus |
| `content/common/logo` | 14,253 | 2D logos used in game |
| `content/character/crowd` | 12,803 | The crowd (NHL 27 has no folder by this name) |
| `content/worlds/textures` | 11,948 | Arena textures |
| `ui/textures/common/ultimateteam` | 11,694 | HUT |
| `content/worlds/stadiums` | 6,305 | Arenas |
| `ui/textures/common/leaguelogos` | 1,428 | League logos |
| `ui/textures/common/coachportraits` | 705 | Coach portraits |

**Sounds:** 7,534 main commentary waves, 687 for 3s, 242 for the Gauntlet, 202 for Be A Pro, 154 announcer, about 840 sound effects, 144 music, 132 crowd. Goal horns are in a sample bank (`hornbank`, 439 horns).

## Legacy files (10,931)

These are in 6 collectors. The `kettle` collectors are where NHL 27 has `ps5`, and that's the PS4 UI.

| Folder | Files | Size | What |
|---|---|---|---|
| `nhl/ui/features` | 6,140 | 57 MB | Menus: `.nav`, `.menu`, `.lua`, `.layout`. The biggest groups are widgets 931, Ultimate Team 682, GM Connected 623, gameplay 455, overlays 375, World of CHEL 296, Be A Pro 245 and debug 227 |
| `nhl/ui/swf_assets/kettle` | 1,334 | 275 MB | Scaleform UI (`.gfx`): HUT, overlays (the scoreclock is here), eliminator, scouting, 3s, post-game |
| `gamedata/nis` | 1,382 | | Cutscene data |
| `gamemodes/service-defs` | 1,194 | 17 MB | Game mode services (`view`, `data`, `job`, `logic`) |
| `gamemodes/creation_zone_csv` | 125 | | Creation Zone lists (hair, gear, goal horns, goal calls, arena parts, cities, celebrations) |
| `camera/stadiums` | 100 | | Commentary camera positions |
| `gamemodes/service-stubs` | 67 | | Offline stand-ins for online services (HUT Rivals, seasons) |
| `gamemodes/spreadsheet` and other `_csv` folders | ~120 | | World of CHEL loadouts, 3s, strategies, settings |
| `gamedata/nn`, `realai`, `physics` | 56 | | AI and physics data |
| `config` | | | `chunkdefinition.json`, `memorybudget.cfg`, `ctldefault.xml` |

**By file type:** `.layout` 1,702, `.json` 1,476, `.cba` 1,371, `.gnf` (PS4 textures) 1,248, `.lua` 1,154, `.vvm` 993, `.lreq` 862, `.dd` 610, `.nav` 604, `.menu` 255, `.csv` 198, `.gfx` 84, `.db` 13.

## Databases (13)

- `gamemodes/database/t3db/nhl_game.db`: 13.7 MB, 119 tables. All 240 CRCs match.
- `gamemodes/roster/initial_experience.db` (39 tables), `entry_draft.db`, `expansion_draft.db`, `threes_campaign.db` (11 each). These use scrambled 4-letter table names. Play Now reads `initial_experience.db` too.
- `loc/<language>.db`: one `LanguageStrings` table, about 65,250 strings in each language. NHL 27 splits this into two tables. The Russian file has only 81 strings.

**Biggest `nhl_game.db` tables:** stockcareerseasonplayerstats 21,413; playernames 16,182; exhibitionplayers 14,783; exhibitionplayerbiotable 11,675; exhibitionrostertable 11,088; exhibitionskaterai and exhibitionskaterequipment 10,443 each; stockcareerplayoffplayerstats 9,303; stockteamequipmenttable 8,816; stockteamjerseys 2,203. All 32 `league*` tables are empty, because franchise mode fills them.

### Teams by league

| Id | League | Teams |
|---|---|---|
| 0 | NHL (32 clubs + 4 All-Star division teams) | 36 |
| 1 | AHL | 32 |
| 15 | ECHL | 28 |
| 11 / 12 / 13 | OHL / QMJHL / WHL | 20 / 18 / 22 |
| 29 | USHL (added by your own mod, not stock NHL 24) | 16 |
| 14 | Top Prospects Red and White (not in NHL 27) | 2 |
| 2 / 8 | SHL / HockeyAllsvenskan | 14 / 14 |
| 3 / 4 / 5 / 6 / 7 | Liiga / DEL / Czech / Swiss / ICE | 15 / 14 / 14 / 14 / 13 |
| 9 | Other European clubs (Aalborg, Rouen, Stavanger, Kosice, Belfast) | 5 |
| 10 / 24 / 25 / 26 | International / Men's Juniors / Men's Worlds / Women's Worlds | 24 / 10 / 16 / 10 |
| 27 | Women's national teams. In NHL 27 this id is the PWHL | 10 |
| 20 / 21 / 22 | Spengler Cup / NHL Alumni (Arizona Coyotes is still here) / All-Time | 6 / 33 / 4 |
| 17 / 18 / 19 / 23 / 16 | World of CHEL / Mascots / EASHL / Circuit / Palm Springs | 11 / 2 / 2 / 121 / 1 |

## NHL 24 against NHL 27

**Tables only NHL 24 has:** `leaguecontractoffers`, `leaguefreeagentdata`.

**Tables only NHL 27 has:** `fournationsschedule`, `pwhlschedule`, `leagueteamheadtoheadrecords`, `stockplaybookstrategies` and the coach tables (`stockcoachtable`, `stockcoachawards`, `stockcoachseasonstats`, `stockcoachplayoffstats`).

**Fields that differ** (28 tables):
- **Superstar abilities:** NHL 24 has `xsa1`–`xsa5` and `xza` in `exhibitionskaterai` and `exhibitiongoalieai` (and the custom copies). NHL 27 replaces these with `xfactor1`–`3` ability and tier fields. Goalies in NHL 24 also have `reactionbigsave` and `reactionpostgame`.
- **Gear:** NHL 27 adds neck guards (`neckguardtype`, `neckguardvariant`, `neckguardexempt`) and the goalie `skatebrand`.
- **Contracts:** NHL 27 adds contract clauses, `draftleague` and `contractevaltemplateid` to the bio table, tax rates and `coachid` to teams, and `playbookid` to strategies.
- **Jerseys:** NHL 27 adds `scoreclockcolor` and `scoreclocklogo` to `stockteamjerseys`. NHL 24's score clock doesn't read colours from the jersey table.
- **Stats:** NHL 27's `leaguegamestats` keeps per-period goals, shots and faceoffs and the three stars. NHL 24's doesn't.

The way the files are stored is the same in both: signed TOCs, CAS files, Oodle, EBX with type tables, t3db. The PS4 differences are already handled in HMS: 4-byte location groups, a texture header 2 bytes shorter, 8x8 Morton tiling (not PS5 swizzle), 56-byte legacy index entries, `kettle` collectors, and GNF loose textures.

**Engine differences that matter for modding:**
- NHL 24 animates with ANT (`ANTEvaluatorDataAsset` 7,973, `animations/antanimations` 980). NHL 27 has neither.
- NHL 24 has a separate crowd character set (`content/character/crowd`, 12,803).
- Score clock: NHL 24 uses `scoreclock.gfx` (ESPN+, Scaleform CFX). NHL 27 uses `tin_nhl.swf`.
- Tuning: NHL 24's ~20,700 tunables are compiled into `eboot.bin` as static objects, and changing one means patching code.

## HMS support for NHL 24

The TOC signature test passed on 2026-11-03: NHL 24 doesn't check TOC signatures. Two textures were written with the signature left stale, showed in game, and restored to the exact original bytes. So nothing is blocked any more, and every write HMS does works on NHL 24.

| Page | NHL 24 |
|---|---|
| Play | Yes, with NHL24 PC (its own Play page; `Play NHL 24.cmd` also works) |
| Explorer, Textures, Sounds, Videos | View, export and replace. Arena HDR textures (BC6H) don't decode yet, 19 of 909 sampled |
| Roster, Database, Teams | Read and write. Edits are written uncompressed into the legacy chunk, and sizes are updated in the index |
| Scorebug | Edits the ESPN+ clock (`Scorebug24`). It still needs checking in game |
| Tuning | Reads 20,745 values. Match NHL 27 broke the game and was undone. Don't run it again |
| Uniforms | Jerseys draw. Helmets, gloves and the idle pose don't (different meshes; you said that's enough) |
| Unused jerseys | Yes |
| Arenas, Be A Pro, Studio | Hidden on NHL 24 (your call, 2026-11-03) |
| Mods | Yes, Restore undoes them |

**What's applied to NHL 24 right now** (from its mod list):

| State | Mod |
|---|---|
| Applied | **Test** database edit: team 2 renamed "HMS Test Bruins", city "Hockeytown" |
| Applied | Score clock: clock box `#7A0019`, clock size 34, power play text "POWER PLAY" |
| Restored | The two signature-test textures (Bruins logo, Boston centre ice) |
| Restored | Tuning: Match NHL 27 |

The Bruins rename was only to test the score clock, and it's still in the game. The score clock change is also waiting on an in-game check.

## What's open

1. **Check the score clock in game**, then restore the "HMS Test Bruins" test edit.
2. **BC6H decoding** for the 19 arena HDR textures that don't show yet.
3. **Goal songs:** they're chosen in Harmony logic, not a table, so they're on hold. Goal horns (the sample bank) can be replaced.
4. **New logo art ids** need loose GNFs in the emulator's user data (`kettle_assets/teamlogos/<variant>/t<id>.gnf`). HMS doesn't make these yet.
5. **Database rules to keep in mind:** many tables ignore added rows (reuse unused rows instead). Player ids 27019–56499 are free, and 56818–59999 crash if reused. The roster key is team × 40 + slot.
6. **Not mapped yet:** what the `.cba`, `.vvm`, `.lreq` and `.dd` legacy files are; the `gamedata/nn` and `realai` AI data; the `.gnf` loose UI textures beyond logos.
7. Real-life calibration is on hold until you have sim results.
