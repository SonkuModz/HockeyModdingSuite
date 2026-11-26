# NHL27Kit: feature plan (Oct 27)

This is the order I'm working in while you're away. Each item ends in something you can see. I'll send a screenshot
when it lands, and a note if something turns out not to work.

Rules for this run:
- Everything is written straight into your game files, with no test copies. Each change goes through the Mods page's
  store, which keeps the original bytes, so Restore can undo it.
- Every new feature is checked against the real NHL 27 data before I call it done.

---

## 1. ATRAC9 encoder: music and crowd replacements that the game can play

**Why:** all 365 music sounds and all 68 crowd sounds are ATRAC9. The goal song you replaced was written as Opus,
and the music player very likely only takes ATRAC9.

**What I'll build**
- An ATRAC9 encoder in `Core/Atrac9Encoder.cs`. VGAudio's LibAtrac9 decoder source defines the bitstream, so the
  encoder writes exactly what that decoder reads:
  - a forward MDCT matching the decoder's inverse MDCT and window;
  - scale factors per quantization unit;
  - a fixed bit allocation that fills each frame's byte budget;
  - Huffman-coded spectral coefficients;
  - no band extension and no intensity stereo, to keep it simple and valid.
- **Same config as the original:** the original's ATRAC9 config (rate, channel layout, frame size) and the EA framing
  (`00` + configs, then `01` + one superframe per stream). The replacement uses the original's exact layout.
- **Exact fit:** the result is padded to the original stream length, so it fits in place.
- **Quality check:** encode, decode it back with the decoder, and measure SNR against the input. Spectrograms go in
  the update.
- **What happens to ATRAC9 sounds:** they get replaced with ATRAC9. Opus is no longer used for them, because it
  doesn't work in the music player.

**Done when:** a real goal song round-trips through encode and decode with clear audio, the original's byte size and
framing, and passes the copy-apply-restore test.

## 2. Database: rosters, players and teams

**Why:** you wanted to dive into the database next.

**What I'll build**
- **Find it:** NHL 24 kept its databases as "legacy" chunks (for example `eng_us.db`). I'll find NHL 27's equivalents
  (the roster DB and the localization DBs) through the index.
- **Decode it:** work out the format with NHL24Kit's `DbTools` as a guide, then decode the tables (names, columns,
  rows).
- **Database page:** a table list, then a grid of rows with search and column sorting.
- **Roster editing:** edit values in place (player ratings, names, numbers, team assignments), with the original
  value shown beside each change. Changes are written back into the game's DB chunk through the Mods store, so
  Restore undoes them like any other mod.
- **Localization editing:** the game's text (team and player names, menu strings) in a searchable list. Edit a
  string and it's written back into the loc chunk the same way.

**Done when:** you can open the players table, change a real player's rating or name, apply it, and see the edit
read back from the game files.

**Status: done (Roster page).** The DBs are found through `globals/chunkfilecollector` (`Core/Legacy.cs`) and decoded
by the kit's own t3db reader (`Core/T3Db.cs`); every CRC checks out (252/252 in nhl_game.db). The Roster page shows
every player with his in-game portrait, bio and ratings. It re-reads the database from the game on every open, reload,
save and selection; nothing is cached. Saving writes into the game through one mod entry per database
(`Core/DbEditor.cs`), and later edits add to the same entry, so Restore undoes them all. The Text mode edits any
language's strings. A string is edited in place and can't grow past its original compressed size. Not done yet:
moving a player to another team (that needs roster-table rows) and strings that grow.

**X-Factors: done (Roster page).** Each player's ratings row (exhibitionskaterai / exhibitiongoalieai) has three
slots, `xfactorNability` (-1 none) and `xfactorNtier`: 1 Specialist (gold), 2 All-Star (blue), 3 Elite (red). The ability
id is the game's text id (`TXT_XFACTOR_N`, `TXT_XFACTOR_SHORT_DESC_N` in loc/eng_us.db) and icon number:
`ui/textures/common/icons/playerabilities/xfactors/medium/xfactorN_T` (32 abilities, 0-31; goalies use 26-30; icon
versions 4 and 5 are other styles). 685 skaters and 56 goalies have at least one; 175 skaters have three. The player card
shows the icons beside the name and a card per slot (icon, name, tier, description) with an ability picker and a tier
picker; changes save with the rest of the player (`Core/XFactors.cs`, `XFactorSlot`).

**Database page (raw grid): done.** Pick any of the 13 databases (nhl_game.db, the roster-mode DBs, the language DBs),
then any table, and every row shows in a grid (text fields first) with a row search. Double-click a cell to edit it;
the value is checked right away against the field (number range, text width, compressed size), and Save writes all of
them into the game through the database's one mod entry, the same one the Roster page uses. Re-read from the game on
every open, reload and save. Interesting tables found: `exhibitionteams` (192 settings per team: presentation colours,
goal/power-play/win songs, horn, mascot, goalie masks, spotlights, rival, affiliate), `exhibitionarena` (arena name,
attendance, goal horn, crowd light colour), `stockteamjerseys` (per jersey: score clock colour and logo, font,
helmet/glove presets), `stockteamequipmenttable` (helmet and glove zone colours, below).

## 3. Team kit view

**What I'll build:** pick a league and team (`nhl/toronto`), then a kit (adidas home, away, alternates). The page
shows that kit's textures side by side (base colour, normal, coeff, socks, pants, numbers font), each with Replace…
right there. This is a faster way to do uniforms than searching texture names.

**Status: done (Uniforms page), with a 3D model.** Kits are found from the texture paths
(`content/character/skater/league/<league>/<team>/<brand>/<kit>/`): 1,972 kits of 509 teams in 20 leagues. Each kit is
drawn in 3D on the game's own jersey, pants and socks meshes. `Core/MeshSet.cs` reads NHL 27's MeshSet layout: LODs,
sections, per-element vertex streams, and geometry inline or in a chunk. Every texture of the kit has Replace…, which
writes straight into the game through the mod list and then redraws the kit from the game files. Numbers and names on
the back are composited by the game from the font texture, so the 3D view doesn't show them.

Jerseys and pants with a stamp (`stamp_jersey_…`, a hotspot XML) are drawn as the game builds them: the `_base_color`
with each enabled brand hotspot's logo (layer N of the branding array) stamped on, turned R+180°, centred at (tx, 1−ty),
sized sx×sy of the texture (`Core/KitStamp.cs`). This matches the baked `_color` to within resampling on most kits, and
replaced logos now show. `nhl27kit kit-stamp [FILTER]` compares the two for every kit.

**Name and number preview:** the Uniforms page has Name, No. and C/A boxes under the model. They're drawn from the kit's
own font texture (`font_…_color`, same layout in every kit: letters in 128/64-px cells, back numbers 320×512, C and A,
then 192-px digit rows; `KitFont`) at the stamp's font hotspots (name, number_back, number_arm_left/right, captain,
number_front when enabled), fitted into each box. Chest lettering is turned 180° from what its hotspot says, because every
kit says r=0 there but the chest faces the other way from the back in the texture. **Helmet and gloves:** the 3D view adds a CCM Tacks 910 helmet and Tacks XF gloves, coloured as the game colours
them (`Core/KitEquipment.cs`). `stockteamequipmenttable` holds 15 zone colours per team, equipment kind, era and jersey
variant: kind 7 is the helmet, kind 4 the gloves (checked on Chicago, Boston, Toronto and Vegas). Era is the kit brand
(nineties 0, rbk 1, adidas 2) and variant the kit folder's number (home 0, away 1, two… twentythree); the most specific
row wins. The helmet's recolor1 red/green/blue are zones 1-3 (shell, trim, maker's logo); the gloves' recolor1-4 are
zones 1-12. Kit folders are matched to `exhibitionteams` rows by city, abbreviation or team name. The "Helmet & gloves" tab beside
the kit textures edits them: each zone the models use (helmet shell/trim/maker's logo, the gloves' zones by size) has a
colour box, the team's colours are one click away, the model redraws as you type, and Save writes the zones into the
kit's rows in nhl_game.db (the one database mod entry, so Restore undoes it). The panel says which kits a row colours: a
row with era/variant -1 is shared by the team's other kits. Tested: save, read back, restore (CRCs 252/252). Not done: the team's
helmet decal (`helmetlogo_0_color` with the helmet's decalmask), per-player helmet/glove brands, and kinds 0-3 (likely
skates, sticks, pants shell).

Not done: arched names (`fcurve`,
0 on the kits checked), slant (`fshear`), the helmet number and the patch.

- **Dark helmet, gloves and socks (fixed 2026-10-29):** those three meshes have no standard normal element (usage 6).
  They keep their normals in another, packed element, so the viewer read zeros and WPF lit them with ambient light
  only. `MeshSet` now derives area-weighted smooth normals from the triangles when a mesh stores none, welding by
  position so UV seams don't crease. The winding matches the stored normals on the jersey and pants (100%). Normal
  maps play no part: the 3D view doesn't use them.

- **Export (2026-10-29):** each texture row on the Uniforms page has an **Export…** button beside Replace. It reads the
  texture from the game at that moment (so a replaced texture exports as replaced) at its largest stored size. It saves
  a PNG, or a DDS as stored (BC-compressed, e.g. BC5 for normals). Files default to `export/uniforms/<league>/<team>/`.
  Scripted test: `--uslot SLOT --uexport FILE`.

## 4. Mod packs

**What I'll build:**
- **Export:** selected mods go into one `.nhl27mod` file, a zip of the source images and sounds plus a manifest.
- **Import:** the pack is rebuilt against your game. Replacements are re-planned from the sources, so a pack keeps
  working if the game data moves.

**Status: done (Mods page: Export pack… / Import pack…).** A pack is a zip of `manifest.json` plus each mod's source:
the image, the audio, or for a roster/text edit the whole edited database. Import re-plans every item against the game
it's imported into and writes it in through the mod list. Tested round trip: a roster edit plus the STL music mod.

## 5. Texture arrays (logos)

**What I'll build:** the logo arrays (`..._color_array`, depth = layers) are refused today. I'll add a layer picker
and replace one layer while keeping the others, using the array's mip-major layout. That follows NHL 24's notes;
I'll verify it on NHL 27 first.

**Status: done.** Arrays are `TextureArrayAsset`s with header type 3 (2D array), and their chunk size field is per
layer. NHL 27 stores them **layer-major**: layer k is byte-for-byte the chunk of the standalone `_k` texture. (NHL 24's
notes said mip-major; that's wrong here.) The Textures page lists arrays with a Layer picker, and Replace writes one
layer into the game, keeping the others. Every replaced layer of an array goes into one mod entry, so Restore undoes them
all. On the Uniforms page, replacing a branding logo writes that layer of the kit's array. It writes the standalone copy
too when that's safe; it's often stored once and shared by several kits, and then it's left alone.

## 6. Scorebugs

**Question:** can NHL 24Kit's custom scorebugs (ESPN+, Prime) be done on NHL 27?

**Status: Scorebug page done. The sharp badge was saved into the game, and on 2026-10-27 Kyty crashed when loading into a match (menus fine), so it was restored to the game's own tin_nhl.swf, and the user confirmed matches load again: the changed score clock caused the crash. Whether the swf rebuild or its move into the debug file is to blame is not known yet. Don't save score clock changes again until that's found.** See
`docs/SCOREBUGS.md`. NHL 27's score clock is Scaleform AS3 in a code library: `nhl.tin.widgets.scoreclock.*` in
`nhl/ui/lib/external/tin_nhl.swf`, in UI legacy collectors the kit didn't read before. Changed files go in with the new
`LegacyEditor`. When a file grows, it moves into the space of a debug-only file in the same chunk and its index entry is
repointed, so no TOC changes.

- **Scorebug page:** the 17 parts (clock, team rows, shots, logo box, goal, messages, shootout, faceoff…) with their
  colours, sizes and positions, and a preview drawn the way the game layers them (hex texture through the background mask,
  team glow, highlight strip, shading), checked against an in-game screenshot. Save to game recompiles the changed classes
  with FFDec and writes the SWF; "Game's own" puts the original back.
- **Sharp EA SPORTS badge:** the logo box draws the match intro's 360×360 badge (the last frame of `eas_end`, clipped)
  instead of the 64×64 one, same size and place. In the game now.
- A navy gloss style was saved by mistake (a misread request) and restored the same day.
- Fixed on the way: single-mip textures that are wide or tall (444×32, 16×80, 4×40) were decoded as a mip tail. Every
  score clock texture decodes now.

**Waiting for an in-game check:** does the game load a changed `tin_nhl.swf` (is the badge sharper)?

---

If time is left: a better BC7 encoder (more modes), a texture search by colour or kind, and an export of a whole team
(textures and sounds).

## 7. Live roster sync (NHL API)

**Why:** rosters go stale as trades, signings and call-ups happen; players like Gavin McKenna need to land on their real
team. Source: the public NHL API (reference: github.com/Zmalski/NHL-API-Reference). No key is needed.

**What the API gives (checked live, 2026-10-27):**
- `api-web.nhle.com/v1/roster/{TEAM}/current` (and `/{season}`, e.g. 20262027): forwards, defensemen and goalies with
  NHL id, name, number, position, shoots, height (in), weight (lb), birth date, city and country, and a headshot URL.
- `api-web.nhle.com/v1/player/{id}/landing`: the same plus current team, draft details (year, team, round, pick) and
  stats.
- `search.d3.nhle.com/api/v1/search/player?q=NAME&active=true`: find a player by name. `api.nhle.com/stats/rest/en/players`
  works too.
- `api-web.nhle.com/v1/prospects/{TEAM}` and `/v1/draft/...`: prospects and draft picks.
- Example: Gavin McKenna is 8486067, TOR #92, LW, shoots L, 71 in, 164 lb, born 2007-12-20 in Whitehorse; 2026 draft
  1st overall by TOR.

**What the game has:**
- Players are `exhibitionplayerbiotable` (game_id, names, birth year/month/day, height, weight, handedness, position,
  jersey, team, proteam, draft fields). There's no NHL id, so players are matched by name plus birth date.
- Ratings are in `exhibitionskaterai` / `exhibitiongoalieai`, by game_id.
- Team membership is `exhibitionrostertable` (playerindex → `exhibitionplayers.index` → playerid = game_id, team, jersey,
  plus about 90 line-slot flags). A player has one row per team he's on (club, national team).
- The roster table has 8,000 free slots (10,678 of 18,678), already reserved in the file, so rows can be added in place.
- Gavin McKenna is already in the game (game_id 25827, portrait), but only on Team Canada juniors (team 342), with
  Toronto holding his rights (proteam 27) and #9.

**Plan:**
1. **Read:** fetch every NHL team's current roster from the API and match each player to the game. Sort them into: on
   the same team; on another team in the game (a trade or signing); in the game but not on any NHL roster
   (McKenna-style); and not in the game at all.
2. **Review page (Roster → Sync):** a list of the differences grouped per team, each with a tick box. For example, "Kempe
   LAK → ... (trade)" or "McKenna: add to TOR, #9 → #92". Nothing is written until you press Apply.
3. **Write:**
   - A move changes the club roster row's team.
   - An addition fills a free roster row and its players row.
   - Numbers, height and weight are updated from the API.
   - A player who left a team has his row moved or released to free agents.
   - Line slots are cleared on rows that change; whether the game rebuilds lines itself needs an in-game check.
   - Everything goes into the one nhl_game.db mod entry, so Restore undoes it all.
4. **New players not in the game at all:** these need a new bio and ratings row (copied from a similar player) and have
   no head or portrait. That's a later step; first see how many there are.

**Status: built (Roster page → NHL sync).**
- **Code:** `Core/NhlApi.cs` fetches the 32 rosters one team at a time; the API answers bursts with 429, so it retries
  and backs off, which makes a full fetch take about 1.5 minutes. `Core/RosterSync.cs` compares and applies.
- **Matching:** by normalised name (accents and punctuation removed) and birth date, then last name and birth date
  (for Teddy/Teodors), then a single name match, which is flagged.
- **First run (2026-10-27):** 803 NHL players, 783 matched. Differences: 15 trades, 92 call-ups or signings,
  49 number changes, 69 no longer on an NHL roster, 20 not in the game.
- **Applying:** ticked changes set the roster row's team (line slots cleared) and jersey, and the bio's team, proteam
  and jersey. Height and weight are opt-in. "Left NHL" rows are unticked and send the player to the club's AHL
  affiliate.
- **Tested:** applied every default-ticked change into the game. It read back with McKenna on TOR #92, and Toronto's
  only remaining differences were the unticked ones. Then restored (CRCs 252/252).
- **CLI:** `nhl27kit roster-sync [--team TOR] [--all] [--sizes] [--dry 1]`.
- **Fix (2026-10-27, after an in-game check):** the game's lineups come from `customonlinerostertable`, a copy of
  `exhibitionrostertable` (same rows by playerindex). The first version wrote only the exhibition table, so nothing
  showed in game, and it cleared `rosterstatus` with the line slots. Now:
  - both tables are written, and the comparison reads the online copy;
  - only line-slot flags are cleared;
  - a moved player is marked active and his captaincy is removed.
  `nhl27kit roster-mirror` copied the user's 17 applied moves into the online table.
- **Fix 2 (the game crashed when a moved player was put in a line):**
  - **Keys:** a roster row's key is team × 40 + a slot (0-39), and both tables are kept in key order. The sync had
    changed only `team`, so moved players kept their old team's key.
  - **Lines:** every NHL team holds each of its 92 line-slot flags exactly once, and moves left holes.
  - **Now:** a move takes a free key in the new team, the tables are re-sorted by key (`T3Db.Reorder`), and each
    affected team's empty slots are filled. Players who just arrived go first, by position, spread across them, and
    never twice in one group.
  - **Repair:** `nhl27kit roster-repair` fixed the user's game: 34 keys and 92 spots. All 32 teams were verified
    complete in both tables, CRCs 252/252.

**Unknowns to check in game:** whether the game rebuilds line slots, whether the free-agent team is the right home for
released players, and whether `proteam`/`team` in the bio must match the roster row.

**Saved rosters override the database (found 2026-10-27).** When lines or rosters are edited in game, NHL 27 writes a
custom roster save in Kyty's save folder:
- **Where:** `%LOCALAPPDATA%\KytyPS5\run\_SaveData\PPSA34063\RosterCustom0\DATA`.
- **Format:** an LZ4 frame at byte 53 (after a small header and an `{"IsBackup":false}` JSON) holding a 59-table t3db.
  It includes `exhibitionrostertable` and `customrostertable`, and the names come from nhl_game-meta.xml.
- **Effect:** the game loads it instead of nhl_game.db's rosters, so database edits made after it was saved don't show.
- **What happened:** the user's save was taken from the broken first sync. It was moved to
  `run\_SaveData_backup\RosterCustom0-broken-2026-10-27` with the game closed.
- **To do:** the sync should warn when a RosterCustom0 save exists, or repair it too, which needs its header and
  checksum worked out.

**Lines built by the kit (2026-10-27).** The game listed moved players under "NO LINES" with a warning, and editing
lines in game makes a custom roster save that overrides the database. So the kit builds the lines itself
(`Core/RosterLines.cs`):
- **Dressing:** each team dresses 20 (rosterstatus 1: 2 goalies, 6 D, 12 forwards, best overall first) and scratches
  the rest (rosterstatus 0).
- **Units:** the 92 slots are filled unit by unit, best first by position: l1-l4 with D pairs (left-shooting D on the
  left), PP1/PP2, PP 4-on-3, PK 4 and 3, 4-on-4, 3-on-3, shootout and goalies. A player is used once per category.
- **When:** every team a sync touches is rebuilt. `roster-repair` rebuilds any team whose slots aren't each held once,
  or that has a dressed player with no slot.
- **Speed:** `T3Db.Batch()` defers the records CRC to the end; one CRC per value made this take minutes, now 6 s.
- **User's game:** 9 teams rebuilt in both tables, CRCs 252/252. Toronto L1 is McKenna-Matthews-Nylander.

**Lines editor (Roster → Lines).** Pick a team; every unit is listed with a player picker per spot: lines and D pairs,
PP, PP 4-on-3, PK 4 and 3, 4-on-4, 3-on-3 (regulation), 3-on-3 overtime, shootout and goalies.
- **Swaps:** picking a player who's already in another unit of that kind swaps the two, so each spot keeps exactly one
  player.
- **Auto-build:** fills everything with the line builder, without saving.
- **Save:** writes the team's slots into both roster tables, and anyone without a spot is scratched
  (`RosterLines.ReadTeam` / `WriteTeam`).
- **Tested:** reading, swapping, and saving into the game: Marchenko to line 1 LW, verified in both tables, then swapped back (CRCs 252/252).

## 8. Texture encoding quality (2026-10-27)

**Problem:** a replaced center-ice logo (BC7 sRGB 1024, from a PNG) looked jagged and blocky in game. Three causes:
- **Encoder:** the kit's own BC7 used mode 6 only, so blocks mixing colours and transparency (logo edges) became 4×4
  stair steps with pale fringes and dark specks.
- **Black bleed:** transparent pixels were black and got mixed into edges.
- **Mips:** colour was averaged without weighting by alpha, so the small mips (which the game uses at distance) lost
  thin lines.

**Fix:**
- **BC7:** goes through BCnEncoder.Net (MIT, NuGet), which uses every mode, at Balanced quality. On the center logo,
  mip 0's visible-colour error went from 5.0 to 0.84 and alpha error from 3.3 to 0.49. That's the same as BestQuality,
  in 24 s instead of 55 s. The kit's mode-6 encoder is the fallback.
- **Transparent pixels:** take their nearest visible colour before BC7/BC3 encoding (`MipChain.BleedTransparent`).
- **Mips:** colour is averaged weighted by alpha (`MipChain.Halve`).
- **DDS sources:** DDS files (BC1/BC3/BC4/BC5/BC7 and 32-bit) can be picked as replacement images (`DdsReader`). They're
  read and then encoded into the game texture's own format and mips.
- **CLI:** `nhl27kit retexture NAME --file F.dds` replaces a texture like the Textures page. `bc-test` encodes and
  decodes for checking.

## 9. Legacy outdoor stadium → NHL 27 (started 2026-10-27)

**Source:** `refs/nhl legacy outdoor stadium` holds an OBJ, FBX and `.rx2` of `stadium_700`, plus 102 DDS textures
(`map_Kd` numbers in the MTL).
- **Size:** 152k vertices and 108k triangles in 22 groups, in centimetres (×0.01 gives metres), with y up.
- **Skybox:** `simple` is the skybox, about 1,500 m across.
- **Rink:** Legacy's own rink, boards and glass would be left out; NHL 27 shares those across venues.

**NHL 27 side:**
- **Venues:** the outdoor venues are `content/worlds/stadiums/outdoor*_9xx`. They're levels (LayerData, Enlighten
  lighting, MeshVariationDatabase) that place scenery meshes from `content/worlds/components/structures/<venue>/`.
- **Scenery sizes at full detail:** canal 715k vertices, outdoortier0 644k, farm 719k. The canal is a World of Chel
  arena.
- **Mesh layout:** LOD0 geometry is in a chunk. Each section's elements are separate streams; the 4th byte of a
  declaration entry is the stream index. Elements seen:
  - 0x01 float3: position;
  - 0x02 format 22: blend index (sub-mesh id on static meshes);
  - 0x1E UByte4: colour;
  - 0x21 half2: UV;
  - 0x2A float3: lightmap UV;
  - 0x34 format 13 (4 bytes): tangent space. Best fit is a smallest-three quaternion (10:10:10 components, 2-bit index
    of the dropped one in the top bits, X axis = normal), not confirmed.
  - `*_Shadow` sections hold position and 0x02 only.

**Built:**
- **Writer:** `Core/MeshWriter.cs` writes new geometry into an existing chunk-backed section in place: same counts,
  format and offsets. The rest is padded with degenerate triangles, other elements keep their first original value,
  and the lightmap UV takes the UV. It can blank other sections.
- **CLI:** `nhl27kit mesh-replace NAME --section N --obj F --groups a,b --scale 0.01 [--blank-lods] [--blank-shadow]
  [--dry out.obj]`.
- **Dry run:** the Legacy `concrete` and `crowd2d` groups (6,282 vertices, 5,365 triangles) went into the canal
  `structure_mesh` section 1 (`building_background_mat`, a background material, so normals don't matter). Read back
  correctly with the kit's mesh reader.

**Next:**
1. Write it into the game and look at it in game; that's the canal World of Chel arena, so getting there is still an
   open question.
2. Confirm the tangent-space encoding.
3. Map the Legacy materials and textures onto the NHL 27 materials of the sections they replace.
4. Fit the whole bowl across the venue's sections.
5. Align the Legacy rink centre with NHL 27's.
6. Lighting.

**Full stadium written (2026-10-27).**
- **Placement:** the canal level places its scenery meshes at the origin (identity transforms in
  `outdoorcanal_905/environment_common`). The rink is centred on the origin with its long side along Z (corner pieces at
  (±17.4, 0, ±32)), so the Legacy model (rink along X) is turned 90°.
- **Geometry:** `nhl27kit venue-build --venue outdoorcanal --obj stadium_700_container_0.obj --map "..." --keep
  snow_blend_pond_mat --scale 0.01 --rotate 90` packs 14 Legacy groups (about 125k vertices) into the LOD0 sections of 14
  canal materials, split across sections where needed. It blanks the other 64 sections (scenery, shadows, lower LODs);
  the canal ice is kept. That's one mod entry, `venue:outdoorcanal`.
- **Textures:** each target material's `content/worlds/textures/<type>/<name>_color` now holds the Legacy DDS
  (`retexture`), and its `_normal` a flat normal. Two normals are stored once with other textures, so they were left
  alone. These are shared world textures, so the other outdoor venues' trees, lamps and snow paths change too.
- **DDS reader:** now also reads DXT3/BC2; five of the Legacy textures use it.
- **Test:** Toronto's arena is The Canal (905), and there's no custom roster save.
- **In game (first look):** the stadium shows (upper decks, structure, towers, scoreboard), but parts of the lower bowl
  were missing or floating. Likely cause: the game culls a mesh by the bounding box in its RES header (two float4 at
  byte 16), and the Legacy pieces went into small meshes (shrubs, lamps, railings).
- **Fix:** `MeshWriter.GrowBounds` grows each used mesh's header box over its new geometry, and `Core/ResPatcher.cs`
  writes RES changes in place. Each bundle's copy keeps its stored size: uncompressed blocks are rewritten, compressed
  ones are re-encoded with Kraken and padded to their old sizes, and each bundle record's SHA1 is updated. `venue-build`
  adds these patches to the venue entry.
- **Bounds fix written:** 13 of the 14 used meshes had their header box grown; `vista_treeslow` already covered its
  geometry. Read back from the game: `vista_shrubs_mesh` is now -46..40 × -0.7..37 × -245..200. One RES is listed by two
  bundles (canal and the World of Chel menu); the stored bytes are written once and both records' SHA1s are updated.
  The venue entry is now 235 ranges.
- **Second look in game:** the bowl still had gaps, and the dry-run OBJ had the same gaps, so they weren't culling. The
  Legacy `simple` group, which was skipped as "the skybox", also holds the solid bowl: seat banks, walls and deck
  undersides (16,814 of its 19,068 triangles). `refs/nhl legacy outdoor stadium/stadium_full_bowl.obj` moves the faces of
  `simple` within 200 m of the centre into `concreteambient` (grey concrete, `light_lamp_post_mat`, now 6 of 8
  sections). The sky dome and the floor under the rink (within 35 m, below 0.3 m) stay out. `simple`'s own texture (37)
  is a field-marking image, so concrete is the better look. Build with `--obj stadium_full_bowl.obj` and the same map.
- **Diagnostic:** `nhl27kit mesh-blend NAME` lists each section's blend-index values. In the canal meshes they are one
  value per section (a part index), and the writer keeps it, so they aren't the cause.
- **Bowl written (2026-10-28):** 249 ranges. In game the bowl shows up close, but parts vanish when the camera is
  farther away.
- **Not LOD:** these canal meshes have a single LOD (offsets 1-5 in the header are 0). The level EBX
  (`environment_common`) holds only placement transforms, and the mesh EBX scale fields are the same on every mesh.
- **Cause:** each section record (0x170 bytes) has its own bounding box, two float4 at +0x150 (min) and +0x160 (max).
  It still held the original small canal piece, so the game culled each section by that box.
  `MeshWriter.SetSectionBounds` (with `Section.At`, the record's offset) fits each written section's box to its new
  piece. `venue-build` writes it with the mesh box, and the entry is now 254 ranges. Read back: the `railings_lamps_01b`
  section box went from 29..177 × 3..6.6 × 221..496 to -122..122 × -0.7..61 × 66..145.
- **Third look (2026-10-28):** the far camera keeps the bowl now, but some pieces are still missing. One cause was
  found: `vista_trees_02b_mesh` is placed only by the small `vista` layer (`ebx-refs ... --scan content/worlds`). That
  layer apparently isn't shown in a game, so the ~14k triangles packed into 02b never drew. `venue-build --skip-mesh
  vista_trees_02b_mesh` now leaves it out. The other used meshes are placed by `environment_common` at identity, and
  they all use the same `worlds_default_lodgrp`.
- **Face direction:** about 37% of the Legacy faces point away from the rink (Legacy likely drew both sides).
  `scratchpad/orient.py` flips each one toward a grid of camera points above the ice, giving
  `stadium_full_bowl_facing.obj`. A back-face-culled preview of the old build matched the in-game shot, which confirmed
  the culling convention. The sawtooth triangles at the ends are real geometry (stepped section sides), not back faces.
- **On hold (2026-10-29):** Toronto's arena is set back to 27 with `db-set` (exhibitionteams[27].arenaid). The canal
  mesh and world texture entries stay applied but go unseen unless The Canal is picked. To resume, set it to 905 again.

## 10. KytyPS5 builds from the NHL fork (2026-10-29)

- **Done.** The Play page has a **Builds** card listing the Windows builds of
  [pengui07/KytyPS5-nhl](https://github.com/pengui07/KytyPS5-nhl/releases) (GitHub API, newest first). It marks each
  build *In use*, *Installed* or *New*, and has Download/Use buttons, a progress bar, refresh, and a link to the
  releases page. The status bar says when a new build is out.
- `Core/KytyReleases.cs` downloads the zip to a `.part` file and unpacks it into a staging folder. The staging folder
  is renamed to `KytyPS5-<tag>-Windows-x64` next to the kit only once it holds `kyty_emulator.exe`, so a failed
  download leaves nothing behind. Nothing existing is overwritten. Use sets Settings.Emulator. With no emulator
  chosen, the kit plays the newest build next to it.
- The 10-28 release ships without `kyty_launcher.exe`; the "Kyty launcher" button now uses the newest build that has one.
- **Two sources (2026-10-29):** `KytyReleases.Sources` lists pengui07/KytyPS5-nhl and duy64/KytyPS5-nhl. Their builds are
  merged newest first, and each row shows the fork's owner. The newest build of each fork is marked *New*. The card
  shows the 6 newest builds plus the one in use, with "Show all" for the rest. The globe button opens a menu of both
  release pages. If one fork can't be read, the other still lists and the caption names the failure.
- 10-28 pengui07 build: the first launch hung and then failed with "could not reserve 13824 MB for guest direct memory"
  while about 8 GB of commit was free. It launched once other apps were closed, so the hang was memory, not the build.
- CLI: `kyty-releases [--install latest|latest:OWNER|TAG] [--dir DIR]`.

## 11. Team editor (2026-10-29)

Teams page (nav: Teams). Reads `gamemodes/database/t3db/nhl_game.db` fresh on open/Reload, saves through the db's one mod
entry (source "Teams page"; Mods page restores). Core: `Core/TeamEditor.cs` (field list + lookups); UI: `App/TeamsPage.*`,
`App/TeamModels.cs`.

- List: 537 teams of exhibitionteams, league filter (stockleagues names), search by name/city/abbr/index.
- Sections: Team palette (stockteamcolorstable `color`), Identity (city, team name, nickname, abbr), Menu colours (fe*),
  Broadcast colours (pres*, overlay*), Rink colours (goal posts, stanchions, spotlights, force team colours), Home arena
  (arenaid picker + exhibitionarena name, city, capacity, year opened, outdoor, crowd light), More (rivals, AHL affiliate,
  average attendance).
- Arenas can be shared; the Home arena caption names the other teams that play there.
- Edits are kept per (table,row,field) while switching teams; one Save writes them all. Text is checked against the
  field's fixed byte width; colours are #RRGGBB.
- Scripted: `--page teams --tteam TOR --tset "feprimarycolor=#FF0000;arena.arenaname=X" --tsave --tscroll N`.
- TODO: verify a real save + restore once the kit/game are closed; see which colour sets show where in game.
- **Numbering (fixed 2026-11-04):** logo textures aren't numbered by team index; 504 of 537 teams differ. Menu logos
  and centre logos use `artid`, centre lines use the `centerline` field, and `centericelogo` replaces the art number when
  `centericelogotype` is 0 or more (29 special teams). Art is shared (59 art ids, often alumni), lines too (46 of 93 lines
  have one team; 76 has 181). The notes below that say "team index" or "arenaid == index" are the earlier wrong reading:
  arenaid does equal the index, but it doesn't number these textures. Line numbering comes from the field's values (all 93
  exist as textures, against 158 of 330 art ids) and isn't yet seen on the ice.
- Logos (added same day): `Core/TeamLogos.cs` scans the index once for `ui/textures/common/teamlogos/<style>/t<team index>`
  (~34 styles: xl/large/medium/small/extrasmall, light / team-colour backgrounds, stylized, glossy, mono, hype stage, HUT
  cards) and `content/worlds/rink/textures/centerlogo/ice_centerlogo_<index>_color` (centre ice; arenaid == team index for
  all 537 teams). Logos card: per-tile Export/Replace, Export all (export/teams/ABBR), Replace logo… (the 5 plain sizes),
  Replace every style… (all menu styles, not centre ice). Each texture is its own mod entry. Locker-room
  (`content/worlds/textures/logos/*_<city>_color`) not wired yet: keyed by city id, not index.
  Scripted: `--tlogoexport DIR`, `--tlogo main|all|STYLE --tlogofile IMG`.
  Shared storage: most logos' bytes are stored once with other textures (large/t27 = playerbanners/emblem/large/
  player_banner_emblem_large_27). `ChunkPatcher.Plan(takeShared: true)` takes the chunks whose table entries point at the
  same bytes along, plus bundle copies found by place (`ChunkCopies.FindAt`; the index lists a chunk's bundles from one
  entry and missed xl/t27's contentlaunchsb `t27_arenagraphics` copy). Only the Teams page passes takeShared. Verified:
  TOR xl/large/medium/small/extrasmall replaced, emblem changed with it, Restore put both back.
  CLI: `sharers TEXTURE` (who else points at its bytes), `apply NAME` (re-apply a mod entry).
  Team save verified: TOR nickname/feprimarycolor written, read back, then written back to originals (#004E9F).

## 12. Jersey slots (2026-10-30)

**Request:** add and move a team's jersey slots, alongside the uniform editor.

**Where slots live:** nhl_game.db `stockteamjerseys`, one row per slot (2,573 used, room for 3,135, so 562 can be added
without the file growing). Rows are sorted by `teamid` (exhibitionteams' `index` field, not its row), then
`jerseyindex` (the slot's number within the team, 0-255). The kit a slot shows is `teamartid` (exhibitionteams' `artid`;
Vegas is team 30 with art 32) + `style` (brand: 0 nineties, 1 rbk, 2 adidas) + `variant` (kit folder: home 0, away 1,
two 2 … twentythree 23); `fevariant` and `variantleadingscorer` equal `variant` on every row. `orderid` is the menu
position (Toronto: 0 = adidas ten, the current home). `isdefault` marks two per team (home and away), `islightcolor` the
white jerseys, `locstring` the menu name (0 Home, 1 Away, 2 Third; 3-12 not identified, no string table found for them).
The unused records after the written ones hold leftover junk (not jersey rows); the game only reads the written count.

**Done:** Uniforms page, third tab "Jersey slots" (`App/UniformsPage.Slots.cs`, `App/JerseySlotModels.cs`,
`Core/JerseySlots.cs`). The shown team's slots in menu order with a jersey thumbnail, up/down/top, remove, a name
picker, Default and Light switches; clicking a slot draws its kit in 3D. Add a slot: search any kit in the game (another
team's too) and Add; it starts from that kit's own slot row (keeps hastie, sock/pant style…) with this team's id, score
clock colour and logo and the next free jerseyindex. Edits are kept per team until Save, which writes every edited team
into the nhl_game.db mod entry (Restore undoes it, together with the roster edits in the same entry).
`T3Db.SetWritten` changes a table's written count within its room (header CRC fixed; dropped records zeroed),
`RecordBytes`/`WriteRecord` copy records. Removing a slot gives its record back; the table is re-sorted after every write.

**Tested (in memory, nothing written):** `nhl27kit jerseys TOR --test`: add Boston's adidas home + move + remove →
CRCs 252/252, other teams unchanged; add then remove, and move then move back, give the original bytes exactly
(ignoring the junk in unused records). UI checked by screenshot (Boston home added and moved to slot 1, not saved).
Scripted: `--uslots`, `--uslotadd "boston adidas home"`, `--uslotmove "28:1"`, `--uslotsave`.

**Saved into the game (2026-10-30, kit closed):** from the page, Boston's adidas home was added to Toronto and moved
to the top, then saved: read back from the game, Toronto had 28 slots with it first as #31, 2,574 records, CRCs 252/252.
Then removed from the page (`--uslotremove 1 --uslotsave`): the whole table dump matched the one taken before, row for
row (2,573 rows), CRCs 252/252. Main Release kit rebuilt with the tab.

**Not done yet:** checking in game that the jersey menu follows `orderid`, shows added slots and another team's kit, and
what locstrings 3-12 say.

### Kit names (2026-10-30)

The jersey menu's kit names are strings of the language databases (`loc/eng_us.db`, fre_fr, ger_de, cze_cz, fin_fi,
swe_se, lng_lg; rus_ru has none), id `VINTAGE_<style>_<artid>_<variant>` (any case; Toronto rbk eight is "2016-17 Away",
adidas twentythree "2022-23 Reverse Retro"). 471 kits have one; Toronto's current adidas ten/eleven/twelve don't. Both
string tables are full, so a kit without a name can't be given one. The name belongs to the kit (all slots showing it).

**Done:** each slot card has a name box when its kit has a name, with "bytes used of room". Save renames it in every
language that has it and has room (each language its own mod entry, so Restore undoes it alone), as typed; languages
it doesn't fit are named in the status. `Core/KitNames.cs`; scripted `--uslotname "PLACE=Name"`.

**T3Db fixes found on the way:**
- A table's records CRC was put 4 bytes early when its compressed strings don't end on a 4-byte boundary (ger_de,
  fin_fi, swe_se, lng_lg showed 3-5 of 6 CRCs). It's now the 4 bytes before the next table (or the file's end): every
  database reads all CRCs ok (loc 6/6, nhl_game.db 252/252, roster dbs). Writing those languages before this would have
  put the CRC in the wrong place.
- A compressed string's room is now up to where the next string starts, not the current text's length, so a renamed
  string keeps its space (renamed back to a longer name later). A string stored once for several values is refused
  (none are in the loc files: 0 of ~157,000; nhl_game.db has no compressed strings). CLI: `db NAME --crcs`, `--shared`.

**Tested in the game files:** "2022-23 Reverse Retro" → "Leafs Retro 23" saved from the page: read back in all 7
languages, CRCs 6/6, Boston's same-text name untouched; then the original name showed 16 of 16 bytes (fits again).
Restore of the 7 entries: all 7 back to "2022-23 Reverse Retro", CRCs 6/6. Not checked in game yet.

## 13. Legacy jersey converter (2026-10-30)

Converts a jersey texture made for the older NHL (legacy) template into NHL 27's jersey layout (`Core/LegacyJersey.cs`).
Samples: `refs/nhl legacy mod uniforms` (Montreal and Winnipeg 2027 Heritage Classic, 2048² DXT1, 12 mips).

- Every NHL 27 skater jersey mesh (nineties, rbk, adidas `player_jersey_*_notuck_large_mesh`) has the same 3,160
  vertices and 5,962 faces, and their UVs differ by about 0.0008 on average. So one mapping covers every kit.
- The legacy sheet is arranged like NHL 27's: back on top, front below, collar in the middle, sleeves off the sides,
  and the collar's inside in the bottom-left square. The proportions differ. Legacy side seams sit at x 293/733 on the
  back and 255/770 on the front (NHL 27: 330/694). The legacy collar opening is taller, and its lower part is a V
  insert on the chest. The legacy sleeves are rectangles that run the other way: the shoulder is at the sheet's edge
  and the cuff is at x≈340.
- Fitted against screenshots of both jerseys in NHL Legacy, rendered on the NHL 27 mesh:
  - **Body:** a piecewise-linear vertical map. The crest area shrinks 1.2–1.3× evenly and sits mid-chest; the hem band
    keeps its proportions. Horizontally, each row scales 1.28× across the middle, then out to the legacy side seam.
  - **Sleeves:** a quad map with the shoulder and cuff swapped. Stripes land at the elbow, and Montreal's sleeve CH sits
    on the outside of the arm, as in Legacy.
  - **Collar inside:** a straight box copy.
- A thin-plate spline through the same landmarks was tried first and dropped: down the front it swung from 0.1× to 2.1×.
- **Uniforms page:** "Legacy jersey…" converts a file and previews it on the shown kit. Nothing is written until
  "Write into game", which replaces `jersey_color` and `jersey_base_color`. "Blank kit logos" (on by default) also
  replaces each `branding_jersey_N_color`, including its array layer, with a blank image, because the game stamps those
  logos over the base and the legacy art carries its own. The converted PNG is kept in
  `export/uniforms/<league>/<team>/` because the mod entry points at it. Script: `--ulegacy FILE`, `--ulegacywrite`,
  `--ulegacykeeplogos`.
- CLI: `nhl27kit legacy-jersey IN.dds [OUT.png] [--size N]`, about 0.1 s for 2048².
- **Not yet checked in-game:** the written jersey, and whether blank colour layers also hide the logos' normal and coeff
  relief.

## 14. How jerseys work in game (research, 2026-10-30)

Traced on NHL Montreal adidas home; all counts are across every kit in the game.

**Kit folder** (`content/character/skater/league/<league>/<team>/<brand>/<kit>/`):
- Baked textures: `jersey_…_color/normal/coeff`.
- Base textures: `jersey_…_base_color/normal/coeff`.
- Branding: `branding_jersey_…_N_color/normal/coeff` (N = 0–5, 512², BC3 sRGB / BC5 / BC3) and the matching `_color/normal/coeff_array`.
- `font_…_color/normal/coeff`, `helmetlogo_…_0_color`, `stamp_jersey_…` (hotspot XML).
- Shader presets: `jersey_…_hq_asp` (AppearanceShaderPreset), its code-generated twin `_hq_aesp`, the double-sided pair `_hq_doublesided_asp/aesp`, and the `_esp` presets.
- Each asset sits in its own `_player_team_brt` bundle. Nothing imports the stamp, the presets or the baked textures, so the game loads them by name at runtime.

**What the player's jersey is drawn with:** the `_hq_asp` (`player_jersey_advanced_shader`, used by every brand). Its 15 texture slots:

| Slot | What it points at |
|---|---|
| `colorTexture`, `normalTexture`, `coefficientTexture` | the kit's `_base_` maps (not the baked ones) |
| `BrandingArray`, `BrandingArrayNorm`, `BrandingArrayCoef` | the kit's branding arrays |
| `fontColorTexture`, `fontNormalTexture`, `fontCoefficientTexture` | the kit's font |
| `weaveNormalArrayTexture`, `weaveCoeffArrayTexture` | a shared fabric weave |
| `*AnimGlow`, `normalArrayTexture` | default or debug placeholders |

Values: `weaveTile` (90,90), `weaveRegions` (6,6,6,6), `snowSprayTile` (3,3) and `ArcadeTile` (2,2). In the EBX, values are 32-byte entries (vec4 then name pointer). Texture slots are 16-byte entries (name pointer, then import index×2+1).

**Base color alpha** is a fabric-region map in steps of 1/7, not transparency:

| Region | Panel | Share of Montreal Home |
|---|---|---|
| 1 | body | 87% |
| 2 | shoulder yoke | |
| 3 | underarm gussets | |
| 4 | collar | |
| 5 | collar V sides | |
| 7 | unused corner | |

The weave arrays are 7 layers of 128² BC5 (normal) and coeff, so each panel gets its own fabric texture.

**Weaves:** chosen per kit, not per brand folder:
- 290 "adidas" kits use **fanatics** weaves and 229 use adidas weaves.
- Variants: default, tie (lace-up collar), mesh, quicklite, nikegrill, dots, metal_0 (metallic accents).

**Coeff maps:** R ≈ 128 everywhere. G changes by material, so it's likely roughness. B is near 255 and looks like occlusion. A is only lit on silver shield parts, so it's likely metalness. None of this has been checked in-game.

**Stamp (hotspots):**
- Font hotspots: name, number_back, number_arm_right (r 90), number_arm_left (r 270), helmet (fsx/fsy 0.54), captain, number_front (often off), patch. They also carry fcurve, fshear, fsx/fsy and ftx/fty (lettering arc, shear, scale, offset).
- Brand hotspots draw branding layers in order:

| Hotspot | Layer | Montreal Home content |
|---|---|---|
| mainlogo | 0 | CH crest |
| collar_nhl | 1 | **LNH** shield (French, Montreal-specific) |
| back_adidas | 2 | adidas logo |
| adidas primegreen | 3 | "End Plastic Waste" tag |
| sponsor | 4 | RBC patch, r 177 |
| (none) | 5 | English NHL shield, spare |

- Each logo has color (alpha = cutout), normal (embroidery or twill relief) and coeff, so logos are raised stitched patches in-game.
- The eboot's stamp parser keys (`fcurve=`, `fshear=`, `hotspotname`, `brandindex=`) are plain strings in it.

**Baked `jersey_color/normal/coeff`:** the hq presets don't use them. The jersey shader tiers are advanced (hq), default and simple, each with a double-sided version, and only hq presets ship in kit folders. So the baked maps are most likely for the default and simple tiers (distant or low-LOD players). Not checked in-game.

**Meshes:**
- Per brand (adidas, nineties, rbk): `player_jersey_<brand>_<notuck|lefttuck|righttuck|fulltuck>_<large|small>`, cloth-simulated (ClothObjectBlueprint plus warp/cloth wrapping assets).
- A separate `player_jerseytie_<brand>_lod0/1` lace mesh.
- All share one UV layout. Meshes default to `player_jersey_default_doublesided_preset` with debug textures; the kit's preset is applied when a player is dressed.

**Brand folders across 1,598 kit presets:**

| Brand folder | Kits |
|---|---|
| nhl adidas | 343 |
| champions nineties | 268 |
| chl rbk | 184 |
| vanity adidas | 144 |
| international nineties | 108 |
| echl rbk | 99 |
| ahl rbk | 92 |
| elite_swiss nineties | 58 |
| elite_german nineties | 51 |
| elite_swedish rbk / nineties | 40 / 34 |
| ebel nineties | 34 |
| elite_czech nineties | 33 |
| elite_finnish nineties / rbk | 28 / 8 |
| pwhl nineties | 24 |
| it adidas | 24 |
| spengler nineties | 12 |
| vast adidas | 8 |
| fn adidas | 4 |
| vanity nineties | 2 |

No kit folder is named fanatics.

**Create-a-team jerseys** (`league/custom/jersey/<adidas|rbk>/custom_N/`, 54 adidas designs plus rbk):
- `jersey_<brand>_custom_N_color` (nearly black, 2048² BC1), `_normal` (seams) and `_coeff`.
- **recolor1–5** masks (2048² BC1). Each RGB channel is one colour zone the game fills with a team colour, giving up to 15 zones. On custom 7: recolor1 is body, sleeves and yoke; recolor2 stripe pairs; recolor3 trims; recolor4 collar parts.
- `hotspots_<brand>_custom_N` (font hotspots only).
- `jersey_logo_placement_*` (rects u0,v0,u1,v1 plus rotation). Common adidas: front crest 0.40–0.60 × 0.64–0.84, shoulder patches 0.36–0.42 / 0.58–0.64 × 0.47–0.53 at 270° / 90°.
- `jersey_font_placement_*`.
- Custom fonts: `league/custom/font/rbk/custom_N` with recolor1–4. Custom pants have their own logo placement.
- The executable names: `customjerseypattern`, `customjerseystripepattern`, `jerseyfittexture`, `jerseyTexUVArray`, `brandingAssetId`.

**What this means for the studio:**
- A team kit is base (colour + region alpha) + up to 6 stamped logo layers (each with relief and material) + font, on a chosen weave.
- Editing the base keeps stamped logos, numbers and fabric. Logos stay sharp and movable through the stamp hotspots, which are XML and editable.
- The create-a-team recolor masks are a ready-made "zones + team colours" system to design with.

## 15. Jersey studio (2026-10-30)

Now its own page (**Studio** in the sidebar, `App/JerseyStudioPage.*`, 2026-10-31; see §18). Designs a kit's jersey live on the 3D model.

- **Art**: the kit's own base, a whole image (`Use an image…`, Shift = legacy layout, converted), or one of the game's
  89 create-a-team designs (54 adidas, 35 rbk; `league/custom/jersey/<brand>/custom_N`). Each design's recolor1–5 masks
  give one colour zone per RGB channel; zones are dealt the team's database colours (plus white), largest first,
  `Shuffle` rotates them, and any zone takes a typed hex or a swatch. Picker thumbnails are zone maps coloured per team.
- **Fabric regions** (base alpha k/7: yoke, gussets, collar, collar sides, whole body) take flat fills; the alpha map itself
  is always kept so the game's weave/material regions stay right.
- **Logos & lettering**: every stamp hotspot (brand layers 0–5 and the font spots: name, numbers, captain, patch) on a
  340 px sheet: drag to move, scroll to size, sliders for across/down/size/turn, Shown switch (e.g. turn on the front
  number), Mirror, Reset. Brand spots can take a new image (squared, 512²) or the crest.
- **Designs on disk**: `Save…`/`Open…` a `.jersey.json` (template, zone/region colours, spots by name, logo/image files)
  to put the same design on another kit. `PNG…` exports the composited sheet.
- **Write into game**: stamp EBX (new `ebx` mod kind: `KitStamp.Write` rewrites the XML at the same length,
  `ResPatcher.PlanEbx` patches it in place plus the record SHA1), replaced logos (branding texture + array layer),
  `jersey_base_color` (painted, alpha kept) and `jersey_color` (BC1, composited without lettering). Each is its own mod
  entry; Restore undoes it. Packs carry `ebx` entries.
- Core: `JerseyStudio` (Templates, LoadPattern, ThumbZones, LoadSource, PaintBase, Paint, Draw), `Kits.Lettering`.
- CLI: `jersey-studio KIT [--template N] [--move ID:TX,TY[,SCALE]] [--out PNG] [--plan]`: draws, checks the stamp
  rewrite (round trip identical; edits same length and read back) and plans the EBX patch without writing.
- Script args: moved to the Studio page, `--page studio --jselect N …` (§18).
- **Not yet checked in game**: whether the game re-reads stamp hotspot moves at runtime (vs. only the baked textures),
  and the written result overall. Nothing was written while building it (the kit was open).

## 16. Centre ice from TheFaceoff.net (2026-10-31)

Teams → Logos card → **Centre ice from TheFaceoff.net…** opens a converter window for the shown team.

**Centre ice tab** (2026-11-04): the team editor has Team and Centre ice tabs. The tab takes a rink picture the user
downloaded (Choose picture… or drop it on the card) and shows the same conversion, because the site answers the kit 403
at times while a browser still gets through. Nothing in the tab contacts the site; Open TheFaceoff.net only opens the
league in the user's browser. On a rink's page the whole-rink picture links to a Google Drive file ("2027 Rink.jpg")
with a Download button, which gives the full picture; right-clicking the picture on the site saves a resized AVIF
(about 1035 wide). `App/CenterIcePanel` holds the preview, sliders, Save PNGs and Write, shared with the window. It
turns away a picture under 1000 wide or not the 5000×2236 shape, and warns when the copy is under 4000 wide. Changing
team clears the picture, since a picture is one team's rink. The window now writes only into the team it was opened for.
Script: `--page teams --tteam WPG --ticepicture FILE` (`-` for the empty tab).

- **Site** (`Core/FaceoffNet.cs`): a Wix site rendered on the server, so plain HTTP works. A league list
  (`/ice-nhl`, `/ice-ahl`…) has 8 rinks a page, newest first, paged with `?<pager>_page=N` (the pager id is read from
  page 1; NHL `comp-mb1jb5iy`). Each entry gives the title, a `center-ice/<guid>` link and the centre-ice close-up id.
  A rink page lists its pictures with their sizes (`"width":5000,"height":2236,"uri":"84911f_<id>~mv2.jpg"`): the first
  5000×2236 one is that season's top-down layout. `static.wixstatic.com/media/84911f_<id>` is the full original,
  `/v1/fill/w_N,h_N,…/x.jpg` a resized copy (used for thumbnails). Downloads are kept in `export/faceoff/downloads`.
- **Terms**: graphics are for personal use with credit and a link back, not for sale. The kit downloads on demand
  and never ships or packs them.
- **Converter** (`Core/CenterIce.cs`): scale from the blue lines (25 ft each side; Boston measured 24.72 px/ft, rink
  84.3 ft wide), with the template (blue lines at 1882/3118 of 5000) as fallback.
  - `ice_centerlogo_N_color` 1024² BC7: 40.3 ft square around centre (the game's logos put the 15 ft circle at a
    380.8 px radius). White ice is turned transparent with colour-to-alpha, `CutOut` (default 0.06) drops ice scratches,
    and paint is opaque from 0.20 darker (0.30 with the tint left on). The game's logos keep paint under the red line, so no gap is cut.
  - `ice_centerline_N_color` 64×4096: the 85 ft line along the strip, its 1 ft across x 4..59, opaque (as the game's).
    It keeps team patterns (Toronto's maple leaves).
  - **Ice tint** (`IceTint`, default 100%): the site draws paint under a frosted-ice veil. Ice is about 252 but black
    paint is about 91-95, and known team colours fit gain 0.59, offset 83. Scratches on solid paint do not follow an ice
    template (correlation -0.108), so it is undone with levels, not template removal: 85..238 stretched to full range,
    which also clips pale ice scratches to white. A 7×7 edge-keeping blur takes off grain, and paint is opaque from 0.20
    darker. Output goes in 19..228, the range of the game's own logos. Boston's black 95 → 34 and gold (255,185,77) →
    (228,157,30); the game's are (35,31,32) and (224,147,0). Black grain stays higher than the game's (std 10-14 against
    3). 0% gives the old conversion. The line gets the same colours, without the blur.
  - Saturation scales colour around luma (default 100%; it was 115% to make up for the tint).
- **Window**: league picker, filter, Load older (5 pages), paste a rink link (any league or year), a live 768 px
  preview (the logo on ice with the line over it, or on dark to check transparency), the whole rink with the cut square
  marked, Saturation, Ice cut-out and Remove tint sliders, the line strip, Save PNGs…, and Write into TEAM. Write goes through
  `TeamsPage.WriteCenterIceAsync`: each texture is its own mod entry. `TeamLogos` now lists `ice_centerline_N`
  ("Centre line", Rink group) too.
- CLI: `faceoff [NAME] [--league ahl] [--pages N]` lists rinks and downloads a layout (no JPEG decoder in the CLI).
- Script args: `--page teams --tteam TOR --tfaceoff "Toronto Maple Leafs 2027"|URL [--tfosat 1.4] [--tfotint 0.5] [--tfodark] [--tfowrite]`.
  A screenshot run renders an open dialog instead of the main window.
- **Not yet done**: writing into the game and checking it in game (scale and line width are matched to the
  game's own Toronto textures, not seen on the ice).

## 17. Videos (2026-10-31)

**What the game has:** 523 `MovieTexture2Asset`s (5.9 GB): menu hype stages, arena scoreboard intros (per team),
ice projections (`content/worlds/rink/movies`), transitions and wipes, mode intros (Eliminator, Threes, Ones, HUT, Be a
Pro), and Training Camp clips. Each asset names its chunks (found like sounds: GUIDs the index knows as chunks). The
biggest chunk is the video, and any others are `ST00` subtitle tracks (UTF-16 text cues).
- 419 are **WebM** (VP8, 1920×1080 mostly; 106 with Vorbis stereo sound). 4 are Matroska with VP6 (`V_MS/VFW/FOURCC`).
  100 are **EA VP6** (`MVhd`, ffmpeg's `ea` demuxer); the `_w_alpha` ones carry alpha that ffmpeg's preview drops.
- **Frame table:** at 0xD0 in the asset is a u32 count, then per video frame `float time, u32 offset, u32 size`: where
  the frame's data starts in the WebM (after the block header) and its length. Checked against all 423 WebM/MKV movies
  (`nhl27kit movies --check`): every entry matches. After the table come the width, height, frame rate (float) and
  the file name (`….webm`). The EBXX section repeats the array's count.

**Replacing (`Core/MovieReplacement.cs`):** the chunk and the table must keep their length in place, so the clip is
encoded with ffmpeg (VP8 two-pass, no alt-ref frames, Vorbis 128k when the movie has sound) to the original's size,
frame rate and exact frame count (a short clip holds its last frame; `tpad`), at a bitrate that fits the original's
bytes. The rest is filled with an EBML Void at the end of the Segment (the Segment's 8-byte size is grown to hold
it). Then the table is rewritten from the new file's frames. If it doesn't fit, later tries soften the detail and hold
pictures for 2-3 frames. Mod kind `movie`: chunk patches + EBX patches in one entry, the fitted WebM as its source (a
pack carries it, so importing needs no ffmpeg).
- Checked offline: Philadelphia's ice projection (21 MB, 361 frames), the CFM wipe (1201 frames + Vorbis) and the
  0.5 MB playoffs hub (300 frames) encode to exactly their sizes and frame counts and decode cleanly; plans are 1 chunk
  copy + 2-3 EBX patches. **Not written into the game or checked in game yet.**
- VP6 movies can be watched (converted to H.264 for the player) and exported, not replaced.

**Videos page (`App/VideoPage.*`):** search, kinds (Menus, Arena, Transitions, Modes, Training), a WebView2 player
(plays WebM itself; VP6 is converted with ffmpeg), properties (codec, size, frames/fps/key frames, sound, frame table,
subtitles, chunk), export as the original file or MP4, Replace… (shape: fill/fit/stretch, use the clip's sound) with
progress and cancel, then Play original / Play replacement. ffmpeg is found through `Core/Ffmpeg.cs`
(NHL27KIT_FFMPEG, ThirdParty\ffmpeg, PATH, winget).
- Script args: `--page videos --vsearch TEXT --vcat Arena --vselect N [--vwait MS] [--vmp4 FILE]`; a shot captures the
  WebView's picture (`SnapshotForShot`), since a WPF render can't see it.
- CLI: `movies [FILTER] [--check] [--out DIR]` (lists, checks tables, dumps files), `movie-replace NAME --video FILE
  [--fit fill|fit|stretch] [--mute] [--out FILE]` (encodes and plans, writes nothing).
- Ideas: thumbnails in the list, subtitles (ST00) view/edit, VP6 alpha, replacing VP6 movies.

## 18. Studio page, lines, added logos, add to a team; pants and helmet branding (2026-10-31)

**Studio page** (`App/JerseyStudioPage.*`, own sidebar entry): team list (kits with a stamped jersey), kit picker,
3D model with jersey, pants, socks, helmet and gloves (helmet/glove colours and team palette from nhl_game.db, read now).
Shared with Uniforms through `App/KitScene.cs` (`KitScene.BuildAsync`, `GearAsync`, `KitWriter.WriteTextureAsync`).
- **Lines** (`StudioLine`, cm): Body lines are measured up from the hem, Sleeve lines up the arm from the cuff, each with a
  colour and an optional edge (colour + width). `JerseyShape.Measure` reads the jersey mesh (A-pose): hem y 0.932, arm
  direction (0.71,-0.71), armpit |x| 0.261 y 1.304, cuff 27.5 cm past the armpit plane. Lines paint per texel through the
  UV1 surface map (`Core/SurfaceMap.cs`), so they wrap round the model. Order: art → zones → regions → lines → decals.
- **Added logos** (`StudioDecal`, PNG or DDS via ImageLoader, squared to 512²): placed like a hotspot (drag, size, turn,
  mirror, remove), painted into the base. Design files keep lines and added logos.
- **Add to a team**: writes the design into a kit folder, then adds a new `stockteamjerseys` slot (label, light, first or
  last) through `JerseySlots.NewSlot/Write` in one `SaveDbAsync(…, "Jersey studio")` entry. Free folders = in no team's menu
  (`JerseySlots.Used`; 238 of 1,972, 142 stamped). Into another folder (`JerseyStudio.ForKit`): the design's stamped logos are
  baked into the base, the target stamp's brand hotspots switched off, lettering spots placed by name, the start kit's
  font maps copied; "Pants & socks" also writes the start kit's composited pants (target pant stamp brands off) and socks.
  Every part is its own mod entry. Uniforms' Jersey slots tab re-reads the team afterwards (`SlotsChangedElsewhere`).
- Script args: `--page studio --jselect N [--jsearch T] [--jkit N] --jtemplate adidas:N|kit --jimage FILE [--jlegacy]
  --jcolor "z0=#..;r4=#.." --jline "body:FROM,WIDTH,#RGB[,#EDGE,W];sleeves:…" --jlogo "FILE|tx,ty,size[,turn]"
  --jspot "ID:TX,TY[,SCALE][,off]" --jslot "TEAM|folder" --jturn DEG --jzoom F --jup U --jscroll Y --jwrite --jaddteam`
  (`s…` collided with the Sounds page's args).

**Pants branding**: `stamp_pant_<brand>_<team>_<kit>` keeps its hotspots in a `<jersey>` block, brand hotspot named
"branding0" (`KitStamp.Block`, `Brands` strips the prefix). Composited pants match the baked ones on 343 NHL kits (mean diff < 1).
**Pant turn rule (2026-10-31):** the baked pant textures use the jersey rule (180 = upright), but in game a pant
stamp's r=0 is upright (the user saw Ottawa home's r=0 senator head upside down in the viewer; most NHL pants are r=0).
`Kits.Compose` adds 180 to pant spots, so composites now differ from the baked pants where logos are turned (by design);
kits stamped r=180 on pants (e.g. Ottawa eleven/four/ten/three) come out turned the other way, as the game draws them.

**Helmet branding** (`KitEquipment.HelmetDressed`): league art (`player_helmet_ccm_tacks_910_{nhl,ahl}_color`) tinted
by zones, the plain art put back through `_decalmask` (red) so the NHL shield stays, then decals through the helmet's
second UV set (vertex usage 0x22, half floats; `MeshPart.Uvs2`):
- UV2 side band (0,0)–(1,1/3) on both sides: helmetlogo_0 (2:1 sponsor, e.g. Toronto's "pizza pizza"). Checked in the viewer.
- Number box (0.10,0.72)–(0.40,0.95) front and back, arm-digit font glyphs, size from the stamp's "helmet" spot (fsx 0.54).
- helmetlogo_2 (square team logo, 54 kits) in the band's middle third: **a guess, not checked in game**.
- CLI: `kit-helmet [FILTER] --out DIR [--v1] [--number]` (UV2 map, logo, dressed helmet); `mesh` prints layout and UV2.
- `jersey-studio … --pose --free --line …` prints the measured shape and the free folders.

**Not written into the game or checked in game yet**: Studio writes (over a kit and add-to-team).

### 18b. "Your own" uniform (2026-10-31)

Studio's default mode builds a uniform from the game's create-a-team parts, with no team's art or logos (`Core/OwnKit.cs`):
- Parts: `content/character/skater/league/custom/{jersey/<brand>|font/rbk|pant/rbk|sock/rbk}/custom_N/` (89 designs, 43
  fonts, 20 pants, 32 socks). Each has a `_color` detail layer (fonts 0/1 only; socks none), `recolorN` masks (each RGB
  channel a zone), `_normal`/`_coeff`. Pants and socks use the common meshes, so they fit any kit folder.
- Fonts: glyph layout as team fonts (1024², `KitStamp.Text` scales by size/2048). Mask-only fonts: each mask has its own
  background channel (zones over 20% of the top border count are background); alpha = max of the rest. Outline zones:
  > 75% of inside texels within r = max(3, size/128) of both outside and another zone. Their name rows (top 384/2048)
  are a solid mask with the shape only in the normal map, so the letters come from font 0 of the brand
  (`OwnKit.LetterFont`, alpha when painting, normal/coeff rows when writing via `OwnKit.FontMap`).
- The custom jersey stamp has no turn values, so lettering spots come from a carrier kit (`OwnKit.Carrier`,
  nhl/anaheim/adidas/home); its base alpha is filled with the main colour.
- Writing: painted font_color + font normal/coeff; with outfit, pants painted at 2048 (target alpha kept), the custom pant
  normal/coeff into base and baked slots, pant logos off; socks into sock_base_color and sock_color. In game the helmet
  and gloves come from the team's equipment rows (the preview tints them in the user's colours).
- Logo spots copy an NHL kit's (`OwnKit.Role`): every adidas NHL stamp has mainlogo (layer 0, chest, r 180, ~0.2 at
  (0.5,0.27)), right/left shoulder (r 90/270, 0.08 at (0.39/0.61,0.5)), the collar NHL shield, adidas back and hem marks,
  sometimes a sponsor. Own mode keeps the carrier's spots: main and shoulders get a stand-in round crest in your colours
  (`OwnKit.Crest`, redrawn when colours change) until replaced; league/maker marks keep the game's images; sponsor off.
- Design files keep `Own` (colours, parts, lettering colours, shifts).
- Script: `--page studio --jcolors "#MAIN,#SECOND,#THIRD" --jfont N --jpant N --jsock N --jletter "#FILL,#OUTLINE"`;
  CLI `own-kit --out DIR [--font N] [--pant N] [--sock N] [--colors …] [--probe --proberow Y]`.
- Not written into the game or checked in game yet.

### 18c. Jersey template (2026-10-31)

`Core/JerseyTemplate.cs`: a guide to a kit's jersey texture to paint over and bring back with "Use an image". Studio
"Template…" (DDS or PNG; own mode uses the carrier kit), CLI `jersey-template [KIT] --out FILE.dds [--size N]`, script
`--jtemplatefile FILE`. A copy for the adidas NHL cut is in `export/jersey_template.{dds,png}`.
- Parts from the jersey model (`SurfaceMap`) and the base alpha regions: front/back by z (front = the side the crest
  spot is on), sleeves by `JerseyShape.OnSleeve` (past the armpit plane *and* outside the body: the hem's corners are past
  the plane too; the Lines feature now uses this as well), yoke, gussets, collar, collar sides, and the neck band (a
  separate piece at the top of the model, found by flood fill). Right sleeve = the side the right_shoulder spot is on.
- Seams between parts, a guide line every 10 cm up from the hem and the cuffs, every logo spot (red) and lettering spot
  (violet) as turned boxes, labels in create-a-team font 0's letters (no digits), each part's label turned like that
  part's spots so it reads upright on the model, nudged off spots; a colour key in the top-left corner when it's empty.
- DDS: uncompressed 32-bit RGBA, legacy header (opens in Paint.NET, GIMP, Photoshop; `DdsReader` reads it back).
- Checked on the model: the template as the image reads FRONT/BACK/NAME/BACK NUMBER upright, sleeves on the right sides.

## 19. Eboot: attributes and tuning (research, 2026-10-31)

Read-only findings, in `docs/eboot/README.md` with the data (`tunables.csv`, `attribute_tunables.csv`,
`player_type_defaults.csv`, the two ratings CSVs) and the Python tools that made them (`docs/eboot/tools`).
- eboot.bin is a decrypted, uncompressed PS5 SELF; code at file 0x608F0 (VA 0), rodata at 0xC01FE80 (VA 0xBFB0000).
- Attributes: 26 skater + 20 goalie ratings; eboot enums, database `c_*` columns and the game's
  `creation_zone_csv/playerratings.csv` (RatingId ↔ legacy AI attribute ↔ identifier) all line up.
- 24,412 tuning variables registered at start-up with type, default, min, max, group and label (bool/int/float
  constructors 0x13139E0/0x1313AE0/0x1313C00); 318 name a rating or attribute. An online tuner may override them.
  (The first Python pass read 14,171: its regex skipped every name stored right after another.)
- **Tuning page** (2026-11-01, done): `Core/Tunables.cs` reads eboot.bin fresh each time the page opens (~1 s, Iced
  decoder), group tree, type filter, "Ratings only", search, Export CSV. Read only. CLI `tunables --out FILE.csv`;
  shot args `--page tuning --tusearch --tugroup --turatings --tucsv`. The nav rail scrolls (wheel) now that it's taller
  than a short window.
- Next, if wanted: decode the rare types, and work out patching (per-variable `vmovss` constant or int immediate, and
  writing a modified eboot through a mod entry).

## 20. Be A Pro save editor (2026-11-01)

- Format worked out from `BeAPro0` (`docs/beapro/README.md`): INDEX/DATA, an LZ4 career stream, and a block table at the
  end (id, offset, stored, raw). The loader only bounds-checks and matches by id, with no content hash, so rebuilt saves
  load.
- `Core/BeAProSave.cs`: slots, open, ratings (current + base copies + the pro block), X-Factors, activities, career
  values, Build, and Write. Write reads the result back, copies the slot to `_SaveData_backup`, then writes.
- CLI `bap SLOTDIR [--set] [--xfactor] [--out]`. App page **Be A Pro** (nav icon E77B): careers list, rating sliders,
  X-Factor pickers, Advanced (unconfirmed) activities and career settings, Save to career (refused while KytyPS5 runs).
  Shot args `--page beapro --bapdir --bapslot --bapset --bapadvanced --bapscroll --bapsave`.
- Tested on scratchpad copies only: Overall 88, Speed 95, Deking 90 and X-Factor 2 = Second Wind (All-Star) read back,
  and the backup matched the original byte for byte. **Not tried in game yet.**
- Next: load an edited career in game; map goalie records; settle trait points, energy and funds from before/after saves.

## 21. Arena renderer (2026-11-01)

- Levels: `content/worlds/stadiums/<venue>_<id>/<venue>_<id>` (75 with structure meshes). The level number isn't the
  exhibitionarena row (seattle_33 is row 31). Venues are matched to an NHL team by city, then the team's arenaid gives
  the name. The centre logo uses the **level** id (`ice_centerlogo_<id>`): the arenaid gave Seattle the all-star logo.
- Materials: a section's material is the basename of a SurfaceShaderPreset the mesh imports. That preset's
  `_color`/`_basecolor`/`_albedo`/`_diffuse`/`_cs` TextureAsset is the texture; sections without one are coloured by
  keywords in the material name.
- Placements (`Core/ArenaScene.cs`): the level's SubWorld/Layer EBX. ObjectBlueprint and SpatialPrefabBlueprint imports
  are followed (prefabs recursively, depth 4). Their LinearTransform arrays sit 7 or 13 pointer slots before the import:
  relative i32 pointers, count at target−4, 64-byte transforms. Root venue meshes are world space at identity, and
  subfolder meshes are local space.
- Guards (`App/ArenaModel.cs`): a copy of a mesh larger than 40 m that isn't at identity, or any non-rink placement with
  its vertices over the ice below 12 m, is a misread record and isn't drawn.
- App page **Arenas** (nav E80F) and CLI `arena`/`ebx-probe`. Toronto and Montreal look right. Seattle's ice is clean,
  but much of its bowl (stands_lower/ etc. pieces) isn't placed yet.
- Jumbotrons: `content/worlds/scoreboard/<venue>/scoreboard_<venue>_prefab` is loaded by the scoreboard state logic,
  not a layer. Its parts (body, `_main`/`_under`/`_netside` screens, LED ribbons) are modelled where they hang. The
  `scoreboard_screen*` sections show the centre logo on a dark panel, fitted to each screen's size, which is measured
  through its UVs. Scoreboards are exempt from the over-ice guard (Montreal's hangs down to 11.7 m).
- Numbered materials (`banner_stlouis_roof_rectangle_02_mat`) import their parent's texture. The game shows
  `banner_stlouis_roof_02_color` (the 2019 Cup banner), so the kit picks the numbered texture when it exists.
- Next: find the remaining placement records (Seattle's stands, ×70 concourse_bg), add outdoor venues' terrain, and
  look at performance above 1M triangles (Montreal ≈1M).

## 22. Team centre-ice ads (2026-11-01)

- The NHL ice (`rink_ice_nhl_mesh`) has ad slots as their own sections: `ice_centeradone..four_mat` (the four around
  centre ice), plus faceoff-circle, goal-line, blue-line and net slots. Each maps its hotspot with UV2. The centre ones
  are 15.9 × 8 ft, 4–20 ft from the red line and 24–32 ft from the middle, with v running towards the boards (the far
  side's two read upside down from the broadcast camera). The quads are in `CenterIce.CenterAds`.
- Hotspots are a shared pool, not per arena: `ice_hotspot_1_<id>_color` (2:1, used in the centre slots) and
  `ice_hotspot_0_<id>_color` (square), ids 0–225. These are sponsors, league logos and EASHL/Cup art.
- Which id goes in which slot: the exe has gamevars `Team_Render_CenterAd1..4` (and FaceOffCircleAd, BlueLineAd,
  IcingLineAd, NetAd), declared in `gamemodes/database/noa/nhl_gm_simulation.data` with no values. The values are in
  `gamemodes/debug/scratch/arena/<name>_n1_<arenaid>.arena` (JSON, `ArenaHotSpot`). Every NHL arena there has
  CenterAd1..4 = 191 Discover, 126 NHL App, 195 NHL EDGE, 2 EA Sports. Faceoff-circle and the other slots are 0.
- The kit (Arenas page, Team ads switch, NHL only) cuts a team's four from its newest "TEAM YEAR" rink on
  TheFaceoff.net (`IceAds.FindRinkAsync` pages the NHL list; layouts are kept in export/faceoff/downloads).
  `CenterIce.Hotspot` samples each slot texel by texel in the same frame as the logo cut: layout right = −z,
  down = +x. With the switch off, the game's own four are drawn.
- In game: the four ids are shared by every NHL arena, so replacing those textures would put one team's ads in all
  arenas. Team-specific ads in game need the per-arena ids changed, and where the game reads them in a normal game is
  not known yet (the .arena files are debug scratch).
- The eboot: the ArenaHotSpot JSON is built at 0x5372xxx through a getter (CenterAd1..4 are fields 0xa–0xd), and the
  values come from a slot map at object +0x7d0 filled by the dynamic advertising / server system ("ArenaDynamicAds",
  "MixAdsWtihOnDisk"). 191/126/195/2 aren't constants in the binary. Nothing on disk sets them.
- Instead, ads from the logo (`Core/LogoAds.cs`): textures can't be resized, so the 1024² logo keeps the logo in its
  top-left 0.79 and the four ads go in the freed strips (368×184 each, two along the bottom, two turned down the
  right). The NHL ice's logo section UV2 is scaled by 0.79, and its four centre-ad sections take the logo's material
  (section byte +0x1C, 3..6 -> 9; the vertex layouts match) with UV2 moved into their box. Dry run: `logo-ads [ID]
  [--layout FILE.rgba --layout-size W,H]`. Drawn from the patched mesh with the game's ads it matches stock (0.48/255
  mean difference); with Toronto's layout its own four show.
- Toronto test written 2026-11-01 (`logo-ads 27 --layout tor.rgba --write`): mesh entry `logo-ads` (291 ranges) and
  `ice_centerlogo_27_color` (BC7 sRGB 1024²). Read back: ad sections have material 9 and their box UV2s, the logo's
  UV2 is 0–0.79, and the logo exported from the game has the new layout. Running it again refuses (already laid out).
- In game it failed, and both entries were restored the same day (read back as stock: ad materials 3–6, UV2 0–1,
  the stock drawing byte-identical to before). At Toronto the logo was not shrunk: the whole new texture covered centre
  ice at full size, so the ads showed around the logo. So the logo shader doesn't map with this UV2. It probably works
  from position or UV1 with its own scale, which also explains the odd UV2 ranges on the other ice meshes. The ad spots
  did take the logo's material (they showed cropped logo texture), so the material swap works. A next try would need
  how the logo shader places its texture, e.g. its material parameters in the ice shader or the resolver.
- Found why (`logo-ads --dump`): every ice section also has a third UV, usage 0x23 (half2), that the test left alone.
  On the logo it's the same 0..1 as UV2, but on the ad sections it's oriented differently from UV2. That fits the test:
  the logo stayed full size and each ad spot showed the whole texture. So decals are mapped with UV3. In UV3 every
  centre slot has U = +4.85 m along z and V = -2.425 m across x, from (9.733,-6.083), (9.733,1.233), (-7.308,-6.083),
  (-7.308,1.233). The kit's `CenterIce.CenterAds` quads came from UV2 and need redoing from UV3. Next try: patch UV3
  (and UV2) the same way, with a 2048 logo (§23) so the logo at 0.79 still has more pixels than stock.
- Second test written (2026-11-01): `logo-ads 27 --size 2048 --like ice_clean_coeff --layout tor.rgba --write`.
  `LogoAds.PatchMesh` now scales the logo's UV2 and UV3, and puts each ad's UV3 hotspot texel in its box (both UVs).
  `CenterIce.CenterAds` now holds the UV3 quads. TheFaceoff.net layouts lie on the ice with right = +z and down = −x
  (worked out from the 2048 logo reading correctly in game), so `CenterIce.Hotspot` samples that way. The old
  right = −z, down = +x was a 180° turn. With game ads the dry run matches stock (0.28/255). Read back: ad UV3s in
  their boxes, logo UV3 × 0.79, logo 2048² with the four ads. The 2048-only logo entry was restored first, and its CAS
  cut back to 1,071,980,544 bytes.
- In game (2026-11-01) it works: Scotiabank Arena shows the 2048 TheFaceoff.net logo and its own four centre ads
  (Canadian Tire, Rogers, Mastercard, Sport Chek), each the right way round, as on TheFaceoff.net. Next: every
  arena's logo laid out (NHL teams with their own ads, the rest with the game's four) so none show cropped, and the
  Arenas page renderer checked against the UV3 quads.
- Rollout (`logo-ads-all`, 2026-11-01):
  - `--teams DIR` maps the 32 NHL levels to logo ids (level number, team by city) and downloads each team's layout.
  - The other ice meshes (intl, arcade, outdoor fives/threes, threes) map their logo with a plain 0–1 UV3 too, so
    their logo UV3 is scaled by 0.79 (entry `logo-ads:other-ice`). Their own ad spots are left alone.
  - Every other logo: the game's logo with the game's four ads, in place at 1024 (entry `logo-ads:logos`). In-place
    keeps ~400 logos from adding ~2 GB.
  - NHL logos: an entry each, so a team's ads can be added later without redoing the rest. With a layout (`--layouts
    DIR` holding N.rgba + N.size) the team's logo and ads go in at 2048, added; without one, the game's ads in place.
  - Mod entries can now be built in parts (`ModStore.Begin/AddPatches/Commit`), and appended ranges restore last
    first. Grown chunks planned together stack in the same CAS (`ChunkGrower` ends).
  - Encoding is ~11 s per 1024 logo (BC7 with mips), so all of them takes ~80 min.
  - TheFaceoff.net answered 403 to every request after the paging for `--teams`, so the first rollout gives the NHL
    teams (except Toronto, already done) the game's ads. Retry their layouts later and redo each team's entry.
- Team ads (2026-11-02): TheFaceoff.net answers again; the kit now waits 1.5 s between requests and doesn't retry a
  403/429. `--teams` got 30 layouts (Florida's page has none; Pittsburgh's "layout" is an arena photo of the same
  shape, kept out). `--replace` restores and removes each team logo's own entry, then writes the team's 2048 logo
  (San Jose 25 and Vegas 32 share bytes with another logo: 1024 in place). The dry run looked right for 29 teams.
  Written 2026-11-02 (rerun after a stop): 27 teams at 2048 with their own ads, San Jose and Vegas at 1024; all
  entries Applied, read back. Pittsburgh (23) and Florida (12) keep the game's ads until a layout is found elsewhere.
  `logo-ads-all --layouts DIR --nhl DIR/teams.tsv --replace --write` redoes them (game and kit closed, ~16 min).

## 23. Bigger textures: adding data (2026-11-01)

- A texture can now change size (`Core/ChunkGrower.cs`, `Replacements.PlanTextureResize`, CLI `texture-grow NAME
  --like REF [--layout FILE.rgba] [--write]`). The new chunk is stored as uncompressed 256 KB blocks, as stock is, and
  added at the end of the CAS file that holds the chunk (16-byte aligned). Then each live TOC entry (chunk table and
  bundle, big-endian offset and size) and each bundle record (chunk logical size, SHA1 of the stored bytes) is pointed at
  it. The header is rewritten in place (same 192-byte RES, with its record SHA1). The old bytes stay, unused.
- The mod list has append ranges (`RangePatch.Append`): Restore cuts the file back to its old length, and refuses if
  something was added after it.
- Header for a size: copied from a game texture of that size (`--like`). 2048² BC7 (`ice_clean_coeff`): mips 12,
  u32/33 = 4/6 (1024²: 3/5), mip sizes [4194304,1048576,262144,65536,16384,4096,4096], chunk 5,595,136. The two mip
  offsets are the stored offsets of the first 256 KB block at or after mip 0 and mip 1 (mips stored smallest first);
  checked on every resize. RES meta is the same at both sizes.
- Data TOCs (Data/Ps5/*.toc) are unsigned, so they're patched directly. CAS files are about 1 GiB each; cas_09 went
  1,071,980,544 -> 1,077,575,856 bytes. Nothing found records CAS sizes.
- First write: Toronto's centre logo (`ice_centerlogo_27_color`) at 2048², cut from its TheFaceoff.net rink. Read back
  through a rebuilt index: header 2048², both entries at cas_09 0x3FE52000, SHA1s match, and the image is identical to
  the plan. In game (2026-11-01) it loads and draws: TheFaceoff.net's design at centre ice, no hang. So the game
  accepts a grown chunk at the end of a CAS file past 1 GiB, with repointed Data TOC entries and a resized header.
- Catches: every logo drawn on the NHL ice has to be laid out again or it shows cropped. The other ice meshes (intl,
  arcade, outdoor fives/threes, threes) don't map the logo with a plain 0–1 UV2 (ranges like 3.6..11.4), so they're
  left alone; a re-laid logo shown on them would look wrong. Whether the ad sections draw right with the logo's
  material needs an in-game check.

## 24. Player idle in the 3D view (2026-11-02)

- Done: the Uniforms and Studio views play `fe_home_selectidle` on the kit, with a default generic head and a
  left-handed stick (the clip's stick bones put it through both gloves; with a right-handed stick the blade points up).
  The lighting was reworked into a key, fill and rim that follow the camera, and there are Idle and Lighting switches.
- How the clip works:
  - It is DCT-compressed: 617 frames at 30 fps, 44 rotation, 17 vector and 5 float channels.
  - Channels are mapped by the order of the rig layouts. The DOF names are in `PlayerIdle`.
  - The clip stands side-on to its trajectory. Its constant turn and drift are removed on AITrajectory, so the hips
    and the stick turn together.
- Fingers: the clip only drives up to the hands. The game has separate hand rigs
  (`af/assets/common/rigs/player/lhandrig`, `rhandrig`), so the grip comes from another layer. For now
  `PlayerIdle.Grip` curls each finger joint by a fixed angle about its local Z: 50–70° for the fingers, 10–25° for the
  thumb. That was checked in close-ups: both gloves close round the shaft. The game's own hand clip (the fingerwag
  celebrations drive the hand rig) could replace it later.
- Shot args: `--unoidle`, `--unolight`, `--uwait MS`.
- Normal maps (2026-11-02): WPF 3D has no per-pixel lighting, so `KitViewer.Relief` bakes a tangent-space relief into
  the lit texture: (1 - 0.6 (1 - z^3)) x (1 + 0.6 (n.L - L.z)), with L = (-0.3, 0.35, 1). It is cached per texture.
  This covers the jersey, pant and sock (the base map when stamped) and the head. The unlit view keeps the plain texture.
  Not done: the helmet and gloves, and the coeff maps (G looks like gloss).

## 25. Jersey from a photo (2026-11-02)

- Done: **From a photo…** in the Studio (`PhotoJerseyWindow`, `Core/JerseyPhoto.cs`).
  - The marks are a body line (hem → armpit height), a sleeve line (cuff → top of the shoulder), a crest box (cut by
    flooding the cloth colour in from the box's border, or by an ellipse), and collar and shoulder clicks.
  - Each line is sampled every pixel as the median across a short perpendicular strip. Bands start where enough samples
    in a row differ by more than ΔE 16 (CIE76) from the current band; bands thinner than 0.5 cm join a neighbour.
    The same colours across body, sleeves and the clicks are merged into one (weighted by width).
  - Base colour is the colour covering most of the body line. A band with a thinner same-colour band on each side
    becomes one line with an edge.
  - Scale: the body line is the model's hem to armpit, (PitY - Hem); the sleeve line is the model's shoulder to cuff,
    `Cuff + Shoulder`. `JerseyShape.Shoulder` is the shoulder's corner: going in from the cuff, where the jersey's top
    edge leaves the arm's straight top line. On stlouis that's 38.9 cm, against 37.2 cm hem to armpit.
  - Applying fills regions 1–7 (collar colour on 5, 6 and the neck band), replaces the lines, paints the crest as an
    added logo at the crest spot, and switches off every branding layer of the stamp.
- Shot args: `--jphoto "PHOTO|body:x0,y0,x1,y1|sleeve:…|crest:x0,y0,x1,y1|round|collar:x,y|yoke:x,y"` (photos split
  by `;`), and `--jphotoshow` to leave the converter open for the shot.
- Tested on the Blues Hometown Remix product photo. Body: blue at 8.4 cm (7.3 cm wide), silver at 15.7 cm. Sleeves:
  blue, silver, black, silver, blue from 15.4 to 25 cm. All match the photo.

## 26. Jersey normal and shine maps (research, 2026-11-02)

Each kit folder has three jersey sets, all 2048², all in the same UV layout:
- `jersey_*_base_color` BC3 sRGB: the flat fabric colours. Alpha = fabric regions (k/7). The numbering is the kit's
  own: St. Louis home has the body as 1, St. Louis ten has it as 2, Boston ten has the collar front as 7. Never
  assume a number means a part.
- `jersey_*_base_normal` BC5: the garment without the knit or logos. It has the folds, the sleeve gathers, the panel
  seams, and a raised seam at every stripe edge. It's per kit: Boston ten differs from St. Louis ten at its stripes
  and its laced V. St. Louis home vs ten differ in only 8k texels.
- `jersey_*_base_coeff` BC3: R≈126, G≈127, B 236–255 (darker in the mesh vents and seams), A=0. It doesn't follow
  the baked coeff (correlation ≈0). B is probably occlusion. Not settled.
- `jersey_*_color` BC1 sRGB: the baked jersey (base colour plus crest, numbers and patches).
- `jersey_*_normal` BC5: base normal + a fine vertical knit (period ~3 px, R std 8.5, G 4.1) + logos embossed
  (raised twill with a stitched outline). The knit is the same in every kit checked (home/ten correlation 0.9996),
  so one tile can be reused.
- `jersey_*_coeff` BC1: the shine map.
  - R = reflectance, 127 everywhere.
  - G = smoothness. 54 on fabric; 28–48 on twill logos (more matte); patterned in the mesh vents.
  - B = metal, 0 (Boston has a few 16–25 spots).
  - Proof: the Bauer 4500 helmet's `_metallic_coeff` vs `_coeff` raises B 3→140 and G 132→163.
- Branding layers `branding_jersey_*_N_{color,normal,coeff}` (plus `_array`): the logos as separate layers for the
  game to stamp.

A generator for a new design:
- Colour: the Studio's art.
- Normal: the kit's base normal, plus seam ridges at the new stripe edges, plus the shared knit, plus logos embossed
  from their alpha (Sobel of a blurred mask, with an outline groove). Encode as BC5.
- Coeff: R 127, G 54 on fabric and ~38 under logos, B 0. Keep the mesh vent pattern from the original.

### Built for shop shots (2026-11-05)

`Core/JerseyMaps.cs`. Not every kit's cloth is G 54: Colorado's and Chicago's are lower (median 24 on Colorado), so "logo = G < 50"
doesn't hold for them. The garment is the per-texel median of 15 kits (one per team, spread out, about 2 s): base normal and
base coeff over all of them; knit (normal minus base) and coeff over those with G >= 50 there. That drops each kit's old
stripes, yoke lines and logos and keeps the folds, panel seams, vents and knit. The low-back rectangle stays: the shop
photos show it too. On it: a raised seam at each colour edge, the print as a plateau with a groove round it, no knit and
G 38 under the print. Relief direction measured on real logos: R falls and G rises where height increases along x / y.
Written with the design: `jersey_base_normal`, `jersey_normal`, `jersey_base_coeff`, `jersey_coeff`, recoloured `font_color`.
The 3D view shows only the base normal. Not yet checked in the game itself.

## 27. Named centre logos laid out (2026-11-02)

- In game at Washington (WPG @ WSH), centre ice showed the EA SPORTS globe blown up and off centre, with black
  triangles in the ad spots. That game used `ice_centerlogo_vast_color`. `logo-ads-all` only laid out the numbered
  logos (`ice_centerlogo_N`), so the 0.79 UV3 showed the named ones too big, and the ad spots sampled slices of the
  globe.
- `logo-ads-named [--preview DIR] [--write]` lays out the 82 named logos (vast, arcade_*, event_*, hut*, eashl_*…):
  the game's logo and its four ads, in place, as entry `logo-ads:named-logos`. 59 written, 23 sharing bytes with one
  already done. Applied 2026-11-02 and read back (vast, arcade_default and hutrivals2_fr show the layout). Not yet
  seen in game.
- In game, live play still showed EA SPORTS at centre ice; only instant replays showed the team's logo and ads. The
  game's sponsor ads (191/126/195/2) are put onto the material of each centre-ad section in live play. With the four
  ad sections on the logo's material, they land on the logo, and the last one (2, EA SPORTS, `ice_hotspot_1_2`) wins.
  Replays don't apply them.
- Shelved (2026-11-02, the user's call): all 36 centre-ice entries restored, newest first (named logos, the team
  logos, `logo-ads:logos`, `logo-ads:other-ice`, Toronto's, `logo-ads`). Read back through a rebuilt index: the ice's
  logo and ad UV3s are 0–1 again, the logos are the game's own at 1024, and cas_09 is back to 1,071,980,544 bytes.
  The entries stay in the store (Not applied).
- To come back to it: keep the ad sections on their own materials and find how the live game picks which sections
  get the sponsor ads (the dynamic-ads slot map at object +0x7d0 in the eboot), or put the ad quads in the logo's
  section itself.

## 28. NHL 24 support (plan, 2026-11-03)

The user's request: NHL 24 (PS4) in the same kit, every page. NHL24Kit (`C:\dev\NHL24Kit`) is only a reference for
what's where; everything is written fresh in NHL27Kit (24Kit was slow: FMT, a worker process, append-only writes).
Nothing has been written to the NHL 24 dump yet.

### What there is

- **The dump**: `NHL 24 Dump` (next to `NHL27Kit`). NHL 24 v1.00, US, CUSA37933 (`sce_sys/param.sfo`, no
  `param.json`). `Patch\` is empty. Data under `Data\Gen4b\`: 9 TOCs (contentlaunchsb, contentsb, globals,
  globalsfull, levels/game/game, movies, ui, worldssb), 36 CAS files in `superbundlelayout/nhl_installpackage_00`–`06`,
  34 GB. Every TOC is signed (`00 D1 CE 01`, data at 556). `eboot.bin` is a fake-signed PS4 SELF (magic 0x1D3D154F)
  with 17 bytes patched against `eboot.bin.orig`.
- **It's modded already.** 24Kit applied 2,004 textures (mostly `ui/textures/common/teamlogos/*`) and 5 legacy files
  (databases), appending to 13 CAS files and repointing and FMT re-signing 3 TOCs (contentlaunchsb, contentsb,
  globals). `__FMTBackup\` holds stock TOCs and initfs. 24Kit's state is at the dump's old place,
  `Downloads\nhl24patch-…\NHL 24 Extracted\CUSA37933-kit\`: `stock.json` (stock length of each CAS it appended to),
  `stock_toc\` (stock TOCs), `applied.json` (fingerprints and items). The kit treats the dump as it is now as the
  starting point; those files are the way back to stock if ever needed.
- **The emulator**: `NHL24-PC-v0.9.2b`, nhl24pc (github.com/HajDEO/nhl24pc), a shadPS4 fork for NHL 24 only
  (native x86, Vulkan). `launcher.exe` (.NET 4 WinForms) keeps the game folder in `game_path.txt`, installs PKGs with
  `nhl24.exe --extract-pkg`, and runs `nhl24.exe`. Settings: `user\config.json` (shadPS4's: GPU resolution, FSR,
  fullscreen, present mode, …), keys in `user\input_config\*.ini`, eboot patches in `user\patches\nhl24.xml`
  (shadPS4 MemoryPatcher; Address = 0x400000 + image offset; per AppVer). Saves and the shader cache under `user\`.
  Red-zone protection fixes a crash while a match loads. A `<game>-mods` folder is shadPS4's per-file overlay of
  `/app0`; never leave a `commandline.txt` in the game folder (the game reads it).

### What NHL27Kit already reads for PS4 (from its 24Kit beginnings)

Signed TOCs (`DbObject.DataStart`), PS4 location groups (4 bytes, `[unk][patch][catalog][cas]`, 12-byte chunk
entries, bundle flag 1 starts a group: `SuperBundleToc`), `layout.toc` catalogs, EBX (the type table in
`Core/Data/typetables.json` is NHL 24's), Oodle blocks, t3db databases with the meta XML (`T3Db`), SPS Opus decoding
(codec 12/14/15, `Audio.cs`).

### What's different (from 24Kit's notes; check each on the dump)

- **Textures**: the RES is NHL 27's minus the two u8s at +32/+33: chunk id at +32, mip sizes at +48, chunk size at
  +108, name hash at +112, texture group at +116. Swizzle flag 0x800 = PS4 8×8 Morton tiling per mip (not
  `Ps5Swizzle`). `firstMip > 0`: the chunk table's copy holds all mips, the bundle copy is its tail from
  mipOffsets[0]; read the full one. A RES can be in several bundles (menu and gameplay): patch every copy. Pixel
  format ids through the same type table.
- **Sounds**: `NewWaveAsset` EBX (9,374, 7,534 commentary); chunk ids by scanning the EBX for known chunk GUIDs (a u32
  size follows each). EA SPS + Opus, 48 kHz, 20 ms packets, first block 648 samples (preskip 312). Audio chunks stay
  **stored** (uncompressed 256 KB blocks) or they play silent. Sample banks (`hornbank`: 439 goal horns) use DSET
  tables (`.OFF`, `.SIZ`, `.SRC`). Goal songs are picked in Harmony logic, not a table (on hold in 24Kit).
- **Databases**: `gamemodes/database/t3db/nhl_game.db` (119 tables) and `gamemodes/roster/initial_experience.db`
  (Play Now reads it too), `loc/<lang>.db`. Rules confirmed in 24Kit: added rows are ignored in many tables (take
  over unused rows); `exhibitionrostertable` key = team × 40 + slot; team names come from loc by artabbr (hash:
  reflected CRC-32 of each byte & 0xDF, ^ 0x80000000); player ids 27019–56499 are free, 56818–59999 are custom
  players and crash if reused. The legacy index chunk holds `[u64 raw size][guid]` per data chunk: a stale size
  crashes the first loading screen.
- **Logos**: `ui/textures/common/teamlogos/<variant>/t<artid>`, 18 variants (80–1024 px, `hutcardbw` 332×464), BC3.
  New art ids need loose GNFs (`kettle_assets/teamlogos/<variant>/t<id>.gnf`) in the emulator's user data.
- **Portraits**: `ui/textures/common/playerportraits/p<artid>`, 512×340 BC3 cut-outs; `artid`/`hasportrait` in
  `exhibitionplayerbiotable`.
- **Gameplay values**: ~25,500 tweakables, a linked list from static initializers (head at 0xAB83918 in the image);
  object +0x18 name, +0x20 kind, value block +0x38 (value, min, max). Defaults change by patching the loading
  instruction (floats: repoint the `vmovss` displacement at existing bytes holding the wanted float, since the
  constants are shared; ints imm32; bools imm8) as `<Line>`s in `nhl24.xml`. Confirmed in game (wrist shot power).
- **Scorebug**: `nhl/ui/swf_assets/kettle/overlays/scoreclock/scoreclock.gfx` (and `score_clock.gfx`), Scaleform
  CFX; the AS3 class `ScoreClock` draws everything; overlay textures are GNFs. Not NHL 27's `tin_nhl.swf`.
- **UI scripts**: `.nav`/`.menu`/`.lua` legacy files under `nhl/ui/features/...` (the UI debug menu via
  `mainmenu.lua`).
- **Meshes**: NHL composite meshes; a composite with zero parts has no part-table offsets (Pittsburgh's and San
  Jose's lower bowls, Seattle's upper bowl). Vertex streams stream-major. Check `MeshSet` against them.

### The open question: TOC signatures

Any write that changes a TOC (a new location or size, a grown chunk, bundle-record SHA1s) touches signed bytes. 24Kit
always had FMT re-sign (an RSA signature over an HMAC-SHA1 of the TOC body with FMT's private key; not ours to
copy), and never tested whether the game checks it. NHL27Kit mostly writes into the original byte range, which
changes no TOC at all; that's the default for NHL 24. **First write test** (with the user's OK): one small change
that touches a TOC, signature left as it is, and see whether NHL 24 loads. If it's rejected, only writes that fit the
original ranges are possible (pad smaller data; refuse bigger), until another way is found.

### Steps

1. **Foundation**: a game profile (NHL 27 PS5 / NHL 24 PS4) detected from the folder (`param.json` or
   `param.sfo`, title id), a game switcher in Settings, and per-game cache (`cache/<title>/index.bin`), mod store
   and settings. NHL 27 keeps working exactly as now.
2. **Reading**: index the NHL 24 dump (time it: 24Kit's FMT index matched 482,971 EBX, 193,291 RES, 180,802 chunks),
   Explorer, Textures (PS4 header, Morton untile, firstMip), Sounds (EBX GUID scan, Opus, banks), Videos, Database
   and Roster (both roster files), Teams. Screenshots of each.
3. **The signature test** (above), with the user's OK.
4. **Writing**: textures (Morton retile, BC encode, every RES copy), databases and loc (sizes in the index chunk),
   sounds (stored blocks; banks), logos, all through the mod store so Restore undoes them.
5. **NHL 24 extras**: Play page for nhl24pc (config.json, game_path.txt, launch, patches), Tuning that edits
   (`nhl24.xml`, the kit's own block), scorebug (`scoreclock.gfx`), portraits.
6. **3D pages**: Uniforms, Studio, Unused, Arenas once PS4 meshes and kit folders are checked on the dump.

Pages that don't apply to NHL 24 (Be A Pro until its saves are looked at) hide when NHL 24 is picked.

### Done (2026-11-03)

- **Step 1**: `GameProfile` (param.json / param.sfo), `cache/nhl24/index.bin`, `mods-nhl24/`, the game switcher
  (title bar and Settings). Indexing NHL 24 takes ~9 s: 129,715 bundles, 482,971 EBX, 193,291 RES, 180,802 chunks
  (the same counts as 24Kit's FMT index). Fix: a PS4 TOC without a chunk table (ui.toc) was read as PS5; the
  reader now takes the platform from the layout (`GameLayout.TocCatalogs`).
- **Step 2 (reading)**: checked on the dump, view and export only.
  - Textures: PS4 header (`TextureHeader.Parse(res, ps4)`), `Ps4Tiling` (8x8 Morton), mips largest first from
    mip 0 (a FirstMip bundle copy is the tail), arrays mip by mip with the layer count in **Depth** (SliceCount is
    8 on the jersey logo arrays, not the layers). 890 of 909 sampled decode; the rest are BC6H (arena HDR), no
    decoder yet. Some stock 2D logos (`content/common/logo/*`) are all zeros in the data; that's the game.
  - Sounds and Videos: worked as they were (Opus codecs 14/15, VP8 WebM).
  - Legacy files: NHL 24's index has 56-byte entries `[u64 path offset][i64 stored start][i64 stored end][i64
    offset][i64 size][chunk GUID]` (3,218 files), then a 16-byte-entry table of root folders and a table of
    `[u64 size][GUID]` per shared data chunk. `Legacy` reads both layouts. nhl_game.db: 119 tables, CRCs 240/240.
  - Roster (11,675 players, portraits), Database, Teams (527 teams, logos) read through it.
  - Every write is refused for NHL 24 (`ModService.RefuseWrite`, the CLI's `RefuseWhileRunning`,
    `LegacyEditor`, `TextureBuilder.Unsupported`) until step 3.
- **Step 3 (signature test, passed)**: NHL 24 does **not** check its TOC signatures. Two textures were written with
  the signature left stale: the Bruins team logo (`ui/textures/common/teamlogos/large/t2`) and Boston's centre ice
  (`content/worlds/rink/textures/centerlogo/ice_centerlogo_2_color`, 1024x1024 BC7, 11 mips), colours inverted. Both
  showed inverted in game (user's screenshot of centre ice). Then both were restored (ice first, then t2: appends
  are cut back last-added first) and the TOC SHA1, CAS lengths and record bytes matched the before-hashes exactly.
  - How a PS4 texture is written (`Replacements.PlanTexturePs4`): every mip BC-encoded and Morton-tiled, largest
    first; stored uncompressed as three parts (mip 0, mip 1, the rest), each starting a block; appended to the CAS
    (`ChunkGrower`), chunk-table and bundle entries repointed. The RES's first 8 bytes (where mips 1 and 2 start in
    the stored chunk) are rewritten; a RES copy that no longer fits its compressed space is stored uncompressed at
    the end of its CAS and its bundle entry repointed (`ResPatcher.Plan(grow:)`).
  - `ModStore.State`: a range whose old and new bytes are equal no longer makes a restored mod read "Partial".
- **Step 4 (writing), started**: the general NHL 24 refusals are gone; textures replace from the Textures page.
  Still refused for NHL 24 until each is tried in game: sounds, videos, EBX saves, score clock
  (`ModService.RefuseNhl24`) and databases (`LegacyEditor`).
- **Step 4 (writing), since**: every write works on NHL 24.
  - PS4 legacy files (databases): the whole data chunk is rewritten uncompressed (0x40000-byte blocks, 8-byte
    headers), a grown file goes to the end of the chunk, every index entry's stored range is recomputed, and both
    chunks are appended (`LegacyEditor.PlanPs4`). EBX copies that don't fit move to the end of their CAS.
  - ModStore: append ranges are left alone on restore; `TrimAdded` cuts a file's added tail once nothing applied
    uses it (`leftovers.json` keeps removed entries' ranges). `modstore-selftest` passes.
  - Reads after a write go through the live TOC entry (`AssetIndex.LiveRef`).
  - Scorebug: NHL 24's ESPN+ clock (scoreclock.gfx / score_clock.gfx, CFX) reads its settings from a 4096-character
    `#NHL24KIT_CFG#` constant; the Scorebug page edits it with a preview (`Scorebug24`). Awaiting an in-game check,
    with a test DB edit (exhibition team 2 "HMS Test Bruins").
  - Tuning: tunables are read from the PS4 eboot (inline static objects, `Tunables.ReadPs4`): 20,745 values.
  - Uniforms: kits draw on NHL 24 (MeshSet's PS4 layout; jerseys named per LOD, `..._large_lod0_mesh`). Helmet,
    gloves and the idle pose aren't done for NHL 24 (different meshes); the user said that's enough.
  - Unused jerseys: works as on NHL 27.
  - Skipped for NHL 24 (user's call, 2026-11-03): Arenas, Be A Pro (no career saves in NHL24 PC's
    user\home\1000\savedata\CUSA37933 anyway), Jersey Studio. Their nav buttons stay hidden on NHL 24.
- **Tuning written (2026-11-03)**: the Tuning page's Match NHL 27 (NHL 24 only; `TuningMatch`, also `tunables-match`) sets NHL 24's gameplay values (AI, Physics,
  Animation; not the coaching/OTF feedback) that NHL 27 also has to NHL 27's: 438 of 476 differing values since the reader fix, one mod
  entry on eboot.bin (427 small ranges). `TunablePatcher`: immediates rewritten; float loads repointed at read-only
  bytes holding the new value, else at 4-byte slots in nop padding after a ret (23). The patched eboot is read back
  before writing (every change as wanted, every other value unchanged). Skipped 7 (zeroed registers, a sign change);
  29 differ only in their minimum. List: docs/nhl24/tuning-from-nhl27.txt. **Undone 2026-11-04**: the user reported it breaks the game, so the entry was restored (all 478 ranges back to the original bytes; the entry stays in the mod list, not applied). Which values caused it isn't known.
- **Real-life calibration (on hold, 2026-11-03, user's call)**: NHL EDGE (api-web.nhle.com/v1/edge/..., no key; see
  github.com/coreyjs/nhl-api-py) plus api.nhle.com/stats team reports. Real per-team-game baselines for 2025-26 and
  EDGE save % by danger are in docs/nhl24/nhl_baseline_2025-26.json. NHL 24 has no rating-to-mph table: speeds come
  from ratings through code, sliders (`gSkatingSpeedSlider` 7-point curve 0.85..1.2, `gShotPowerSlider`) and
  multipliers (`gSlapShotPowerScale` 1.1), so the plan is to match outcomes: the user sims CPU vs CPU games, HMS
  compares box-score stats to the baselines and adjusts shooting/goalie/penalty tuning. Next step waits on the user's
  sim results. Don't re-run tunables-match: it broke the game (see above).

## 29. NHL24 PC 0.9.3b, NHL 24 1.60 and CIMS packages (2026-11-22)

- NHL24 PC 0.9.3b installs the base game (1.00) in `game\` and the 1.60 update next to it in `game-UPDATE\` (eboot,
  sce_sys, Patch; its Data is empty). KitPaths pairs them: either folder opens the update folder as the game, its Data
  read from `game\Data` (`KitPaths.Canonical`, `DataDirOf`). Checked: every stock TOC ref resolves; nhl_game.db and EBX
  read from the 1.60 Patch; Tunables read from the 1.60 eboot.
- A TOC rebuilt by the Center Ice Mod installer has a 12-int header (0x30), flags 0 and plain NUL-terminated bundle
  names; SuperBundleToc reads both forms.
- `.cimspkg` (CIMSPKG1): magic, raw-deflate blob per file, JSON manifest (stock/mod size + SHA-256, ranges), u64
  manifest length, magic. `CimsInstaller` checks every target is stock, verifies stock + package = mod hash (dry run,
  ~2 min), then writes through ModStore: TOC ranges kept in the mod list, CAS growth as *sourced* ranges read from the
  package when applied (the package must stay where it was), emulator `user\patches\nhl24.xml` as its own entry.
  Restore gives the stock files back; a crash mid-apply resumes; another mod's data where the package's goes is refused.
- CLI: `cims PACKAGE [--plan] [--install]`; app: Mods > Import pack… takes `.cimspkg`.
- Center Ice Mod 2026-11-15: 48 game files + emulator patch, 8.6 GB added, ~10.1 GB free needed. Not installed yet.

## 30. Custom leagues: NCAA (NHL 24, 2026-11-23)

Sources: `refs\custom-leagues.md` (how the USHL went in), `refs\NCAA_Leagues.md` and `refs\NCAAHANDOFF\ncaa-export`
(7 conferences, 63 teams, 1,720 players from the Blueline NHL 19 project). The pack is copied to `leagues\ncaa`
(`ncaa_leagues.json`, logos).

Decided with the user: one conference first (Big Ten); one Play Now league "NCAA" (league 29, TXT_USA_LEAGUE) with the
conferences as conference groups; later teams beyond 16 try added team rows; ratings flat 60-70 (the pack's NHL 19
overalls).

How it goes in (`Core/CustomLeague.cs`, `Core/LeagueInstaller.cs`, `Core/LocDb.cs`):
- One mod entry, kind `league`, name "NCAA league", over nhl_game.db, initial_experience.db (opened with
  nhl_game-meta.xml) and loc/eng_us.db: all three share the legacy index chunk, so they're planned together
  (`LegacyEditor.PlanPs4`). Applying again restores the old entry first. It refuses while a db/legacy entry for those
  files is applied, and while an emulator is running.
- Teams take over the 16 Aces rows (883-898): the template team's row (the pack's `kitsCopiedFrom` when NHL 24 has
  it, else Brandon) is copied over the slot. Then the slot's strategy and arena rows, jerseys (by jerseyindex),
  equipment (by item and jersey; the template's all-jersey row as fallback) and palette. Names, colours, arena name and
  city, groups 24/24/24, art abbreviation `NC<abbr>`.
- Players: new rows in bio, skater/goalie ratings and equipment, players, and both roster tables. Ids are free in both
  databases from 40000; roster key = team × 40 + n; tables re-sorted as stock; lines from `RosterLines.Build`.
  Ratings, ratings rows and faces are a stock AHL/CHL/ECHL player's with the same position and overall. Birthplace
  codes are the pack's NHL 19 ones (they match NHL 24's: ON 9, US 14, AZ 17, MA 36, NY 47). Commentary surname from
  nhl_game.db's playernames when there is one (85 of the 182 Big Ten players have none).
- Strings: LanguageStrings holds 65,498 rows, and the u16 row count caps it at 65,535. New keys are added as rows up
  to 65,520 (as the USHL's were), then take over keys of old circuit abbreviations no team has (`NHLCityName_1T01`…).
  Numbered keys are never taken: `NHLCityName_0` is Anaheim and `NHLTeamName_21` the Flyers, a live by-number family.
  `LocDb.Write` rebuilds the table: same tree, strings deduplicated, sorted by hash. An unchanged rewrite reads back the
  same: CRCs 4/4, 65,498/65,498 rows. Big Ten: 22 rows added, 34 taken over.
- initial_experience.db gets each new player's rows copied from nhl_game.db (same layouts), so both give him the same
  ratings, face and gear. A failed re-apply puts the earlier league back.
- CLI: `league [PACK] --conf bigten[,he…] [--apply|--verify|--restore]`; `loc-test`. App: **Leagues** page
  (`--page leagues --lconf KEY --lteam N --lverify`).

Done: Big Ten applied to NHL24 PC 0.9.3b and checked off-game: 23/23 checks. A restore round trip went back to stock
(checked by mod state, row counts and loc size, not by file hash), then it was applied again. Rollback tested.

**Checked in game (2026-11-23, by the user):** "NCAA" shows in Play Now with its 7 teams, the team names and colours
look right, and a game loads. The template teams' logos show (Minnesota: Chicago Wolves, Ohio State: Cincinnati), as
expected. Team ratings come out at about OFF 52-53, DEF 50-56, GOA 56-57.
Not done yet: logos, which are still the template team's (the art id is kept; next is the kettle_assets fallback with
new art ids), generated jerseys, the other 6 conferences (more than 16 teams needs added team rows), Maryville (no
roster), and real ratings.

## 31. NHL 27 alternates in NHL 24 (2026-11-24)

44 NHL 27 kits added to NHL 24 as new kit folders and jersey slots (one mod entry, "NHL 27 alternates"; confirmed in game):
NHL 27's current alternates (menu label 2, named "2026-27 Alternate") and its 2024-25 / 2025-26 kits Center Ice doesn't have.

- `kit-alts-find --game NHL24 --game27 NHL27 [--specs FILE]` lists them per team and writes the `kit-alts` arguments: each
  kit onto a team kit of the same shade laid out like NHL 27's (base colour, branding arrays with room for NHL 27's
  layers), the Center Ice Fanatics kit first, as a free variant whose word is as long as the donor's.
- `kit-alts ... --loc-rows 65530 --apply`. Each kit gets NHL 27's textures and stamps, its name (eng_us), and NHL 27's
  helmet/pant colours (stockteamequipmenttable zones) where NHL 27 has them.
- What it took:
  - New kit folders: player_team_brt entries, patch-TOC root bundles, odd res ids.
  - Fanatics weave: textile/weave/fanatc, listed in each kit bundle. NHL 27's Fanatics arrays are NHL 24's adidas ones
    without layer 1 (holes); kits pick layers by NHL 24's order, so the copy is in NHL 24's order, with layers 1-2 (holes,
    dimple mesh) as plain knit in both normal and coeff arrays. Coeffs stay NHL 24's (NHL 27's look like chrome).
    The tie weaves' last, blotchy stitch layer (tie 6, metal_tie 7) draws on logos of kits whose donor uses a tie
    weave; NHL 27's kits use the default weave without it, so it takes the flat layer before it.
  - Menu names: eng_us.db grows in place (`LegacyEditor.PlanPs4`); moving it to its chunk's end crashed start-up.
    Names take over unused vintage rows, then add rows (65,530 of 65,535).
  - Stamps too long: numbers lose trailing zeros. Branding arrays with more layers than NHL 24's: the layers past the
    stamp's last brand hotspot switched on are left out.
- Left out: Chicago 2024-25 Classic (3 pant logos shown, NHL 24 has room for 2).

## 32. NHL 24 gameplay: how it is defined and changed (research, 2026-11-24)

Read only; nothing written. Full write-up `docs/nhl24/gameplay-deep-dive.md` (plus `gameplay-levers.md`,
`tunables-nhl24-1.60.csv`, `curves-nhl24-1.60.csv`, `settings-1.60/`, probe in `tools/eboot-curves/`).
- Reference: College Football 27 (Frostbite) keeps gameplay in AttribSys EBX with `_gamestyle_simulation` twins and
  `gameplay_sliders` FloatCurves. NHL 24 has no AttribSys EBX; the same layers live in the eboot and legacy CSVs.
- Eboot tweakable type codes beyond bool/int/float: 12 float curve (807; `+0x38` count, `+0x40` pointer, `+0x48`
  editor min/max, `+0x50` runtime instance), 14 x→y pairs (897), 13 (463), 15, 11, 5/7/9/10. Curve points are plain
  floats in the RW data segment (file offset = VA − 0xAA00000 + 0xA75CB20), each array distinct and referenced once.
- The 84 menu-slider curves are among them. Evaluation (checked in code): slider value v (0–100) → pos =
  v × (count−1)/(steps−1), linear interpolation between points, read live. So a curve edit is a data-only write.
- Settings: `gamemodes/setting_csv/` (setting, range, context, contextsetting, choice); hidden Simulation style and
  Hardcore preset. The profile save (LZ4 JSON, `Settings.SettingGroupList`) holds 0–100 slider values that don't
  match the CSV defaults: the CSV route is unverified.
- AI tuner sets (EA's live tuning blobs) exist in code; none offline; format not decoded.
- Next (waiting on the user's OK): one in-game proof, `gSkatingSpeedSlider` all points 1.5 as one eboot.bin
  ModStore entry (28 bytes at 0xA7B8740), then `curves`/`curve-set` in HMS.

### 32.1 Sustained Pressure off switch (2026-11-24, not applied)
- AddPressure is at VA 0x2C5CE30. It is the only writer of player pressure state `+0x780` (1 = pinned, 2 = adrenaline).
- Off switch: eboot file offset 0x2CAEBD4, `74` → `EB` (the `[meter+0x3C]` enabled check always returns).
  - SHA1 before: 4047f9f0…
  - SHA1 after: d552d54b…
- Waiting on the user's OK. It needs a one-byte ModStore eboot entry command first.
- Details and the issue → lever map: docs/nhl24/pressure-system-and-issues.md.
- **Applied 2026-11-24** with the user's OK: ModStore entry 4d7c573cfad1, kind `eboot`, name "Sustained Pressure off".
  - New CLI command: `eboot-bytes --name --at --old --new [--sha1] [--write]`.
  - Not yet tested in a match.
  - **Confirmed in game 2026-11-24:** the pressure meter is gone.
- Issue fixes batch 1 (5 values) applied 2026-11-24 as ModStore 8bbcebe28c34 (kind tuning-set). Not yet tested in a match. See docs/nhl24/pressure-system-and-issues.md section 5.
  - Batch 1 boots and plays (2026-11-24); effects not checked.
- Hitting fixes batch 1 (7 values) applied 2026-11-24 as ModStore 66b44c5d2f4f. Not yet tested in a match.
  - Hitting batch 1 boots and plays (2026-11-24); effects not checked.
- Skating fixes 1 (41cd61cc74f2) and Scalar route test (94531285f4da, gSkateModelMaxSpeed 0.29 -> 0.35) applied 2026-11-24. Still to do: the user checks speed in a match, then restores the test entry.
  - **Scalar route proven on 1.60 (2026-11-24):** the speed test was visibly faster. Test entry restored.
- Stumble fixes 1 (d1042c2ca44c) and Hit power -10% (849640859dd4, the first curve/data write) applied 2026-11-24. Not yet tested in a match.
  - Both reverted 2026-11-24 (hits overpowered). Cause unknown; test each alone.
- Penalty and puck fixes 1 (1f329959f9f4) applied 2026-11-24. Not yet tested in a match. Hitting on hold.
  - Penalty and puck fixes 1 plays good (2026-11-24).

## 33. IHN broadcast package (NHL 24, started 2026-11-24)

The user's own broadcast package, **IHN (Ice Hockey Network)**: navy and gold (HMS colours, different name), angled
and sporty. Every broadcast graphic first, the menus last. The inventory of what draws each graphic, how proven each
write path is, and the phases are in `docs/nhl24/broadcast-package-ihn.md`.

Phases: 1 scorebug (`tools/scorebug-ihn`, a fork of the Sportsnet clock), 2 lower thirds, 3 intros, 4 end of period,
5 replays and transitions, 6 stat overlays, 7 menus. Each one gets a concept for the user first, then a screenshot from
the game.

- **Phase 1 (scorebug):** after ten concepts (A-J, `previews/ihn/`), the user picked **G Puck** (2026-11-24): the clock in a gold-rimmed puck, with rounded team capsules and oversized logos. Six versions of G were then sent to choose from (`concept-g1-puck-plus` … `concept-g6-night-glow`, made by `previews/ihn/gen_puck.py`). Logos are NHL 24's own (`ui/textures/common/teamlogos/large/tN`: 0 ANA … 29 WSH, 32 VGK, 33 SEA), never the centre-ice art. `tools/scorebug-ihn` is forked and builds (BASE `App/Assets/scoreclock-sportsnet.swf`). **Then the user shared the look they have in mind** (`previews/ihn/user-reference.png`): centred, a bevelled gunmetal frame, glowing team-colour panels with big logos and abbreviations, a raised clock ("1st PERIOD" over the time), a SOG row with PENALTY TIME in the middle, and a POWER PLAY tab under the team. Concepts from it: `concept-r1-gunmetal` (true to the image) and `concept-r2-navy-and-gold` (`gen_ref.py`), waiting for a pick. The game feeds one comparison number per team (`showComparison`), so the reference's "12 / 18" becomes one number.
- **Phase 1 done (2026-11-25):** the user picked **R1 Gunmetal** and it is **in the game and confirmed** (regular game, goals, power play, shootout mode). Source `tools/scorebug-ihn/ScoreClock.tpl.as`, built movie `App/Assets/scoreclock-ihn.swf`, installed as "Score clock: IHN" (`scorebug24 --movie … --name IHN`). The user moved it from top centre to the **top left** after seeing it in game. Logos are fitted on a wrapper Sprite (the game's `loader.ImageLoader` resets its own clip's scale on every redraw) and start at 256-px scale, so reloads don't flash. The goal banner has 4 styles taking turns (Burst, Slash, Spotlight, Strobe; cfg `goalStyle` 0 = rotate, 1-4 = fixed) with 128 px logos. During a power play the middle box reads "PENALTY 1:36". Lessons: the movie must compress to at most **162,761 bytes** (its slot); a bigger one is moved and the game shows no clock. `build.py` runs `strip.py`, which empties only EA's decorative animation sprites (about 144 KB). Emptying every sprite crashed shootout mode, and no pixel reads are allowed on game textures. Always check that the install says "read back: same movie". Not done: the app's Scorebug page (Core `Scorebug24.Load` reports "Not a Scaleform" on some read-backs; the CLI install is fine) and an IHN entry in the app's style list. Phase 2 (lower thirds) skipped: the user likes EA's as they are (2026-11-25).
- **Phase 3 (replays/transitions), started 2026-11-25:** the replay wipes are 3D worlds under `content/worlds/wipes/`, not gfx. A marker test (7 recoloured wipe textures) showed **none** of them in game: the player/team wipes take shapes and team colours from geometry, and those markers are restored (entries left NotApplied). The wipe the user sees most is the **Play Now flat wipe** ("EA SPORTS": white disc, black letters, dot/plus/hatch patterns), whose logo is geometry: `wipe_flat_logos/meshes/unified/wipes_play_now_flat_medallion_main_mesh` (white disc = part 0 cap), `wipes_play_now_flat_easports_main_mesh` (part 1 = "EA", part 2 = "SPORTS"), `wipe_play_now_flat_alt_main_mesh` (part 0 ring + parts 1/2); each cap has a shell (extruded sides) and a position+UV twin section. UVs are a planar map of position. Colours come from the wipe's materials, so the user's SVG logo (`tools/wipe-ihn/ihn-logo.svg`) goes in as a one-colour cut-out.
  - **Built:** `Core/MeshWriter24.cs` + CLI `mesh24 NAME [--geo F.json] [--dry OUT.obj]` (PS4 layout, half positions/UVs, other elements kept; the chunk is stored uncompressed at the end of its CAS via ChunkGrower, one `mesh` ModStore entry). `tools/wipe-ihn/build_logo.py SVG ORIG_OBJ_DIR OUT` makes the geometry (own ear clipping with holes; HOCKEY NETWORK from Arial Black). CLI `ebx-floats NAME --at N [--set ..] [--expect ..]` (one `ebx` entry).
  - **In the game and confirmed by the user (2026-11-25, "looks good"):** 3 `mesh` entries "IHN wipe logo (medallion/easports/alt)" (shells blanked, twins filled); first mesh write on NHL 24: apply → restore gave identical TOC hashes and file sizes → applied again. Separate `ebx` entry "IHN wipe navy background test": the Vec4 at byte 352 of `wipe_play_now_flat_dsub_schematic` (0.05,0.05,0.05,1 → 0.02,0.06,0.22,1); a guess that it is the background colour (no type info to name the field). If the game crashes, restore the ebx entry first, then the meshes.
- **Phase 4 (pre-game team intro): in the game and confirmed by the user (2026-11-25, "Looks good"):** the user picked concept **B Broadcast Band** (`previews/ihn/intro-b-band.png`) over A Split. `team_intro.gfx` (collector `globals/chunkfilecollectoruikettle_launch_swfassets`) is driven by `nhl.overlays.teamvsteam.TeamVsTeam` (nhluidata) with `sendMessage`: `setOnHideCompleteCallback`(fn), `init`(data), `show`, `hide` (the movie must call the callback when its out-animation ends; the game then sends `evt_close_TeamIntro`), `dispose`. Source `tools/intro-ihn/TeamIntro.tpl.as` (document class `overlays.teamintro.TeamIntro`, everything drawn in code; the IHN logo comes from `gen_art.py` → `art.txt`), `build.py BASE OUT` replaces the class with FFDec and drops every EA symbol, shape and image. Text uses the game's font map names (`$Header`, `$HeaderItalic`, `$Default`, `$BodyAlternate`) with `embedFonts` false, like EA's `SWFAssetTextUtil`. Logos load with `loader.DeprecatedImageLoader` (EA's choice here) on a wrapper Sprite fitted to 160 px. Team colours: the primary brightened to the same hue; the secondary when the primary is black/grey. Records show only when the game sends them; a significance ("ELIMINATED" …) replaces the record tab. **The file has no free space after it: room 16,815 B** (EA's own size); the build is 12,065 B. New CLI `ui24 PATH [--movie F.swf --name N] [--dry]` lists a UI movie's room and writes it as one `legacy` entry, refusing anything bigger than its room. Installed as "Team intro: IHN" (built movie `App/Assets/team-intro-ihn.swf`; read back: same movie). Preview: Ruffle via `scratchpad/pv` with flashvar `nhl24kitPreview=1` (fonts fall back to `_sans`).
  - **Restore order:** the intro and the score clock share the collector's index chunk, so the intro entry was saved on top of the clock's and the clock entry now reads **Changed** (its data is untouched and still in the game). Restore the intro first, then the clock. `scorebug24 --set/--movie` restores the clock entry first, so it refuses while the intro is applied: restore the intro, change the clock, then reinstall the intro.
- **Phase 5 (end of period): in the game and confirmed by the user (2026-11-25, "Looks good"):** the user picked concept **A Score Band** (`previews/ihn/eop-a-band.png`; B was an intermission board). `endofperiod.gfx` is driven by `nhl.overlays.endofperiod.EndOfPeriodOverlay` with `sendMessage`: `SetData`(data, playSfx), `show`, `hide` (immediate: the overlay goes invisible at once, so no out-animation), `dispose`. Data: `AwayTeamCity/Name`, `HomeTeamCity/Name`, `AwayTeamScore`, `HomeTeamScore`, `PeriodString`, `AwayTeamLogoPath`, `HomeTeamLogoPath`, `Away/HomePrimaryTeamColor`, `Away/HomeSecondaryTeamColor` (no shots or other stats). Source `tools/eop-ihn`: `make_tpl.py` writes `EndOfPeriod.tpl.as` from `head.as` + the intro's shared drawing code (band halves, logo art, logo loading, colour helpers) + `tail.as`; `build.py BASE OUT` (document class `overlays.endofperiod.EndOfPeriodSWF`). Room **15,661 B**, build 11,450 B. Installed as "End of period: IHN" (built movie `App/Assets/end-of-period-ihn.swf`). It shares its **data chunk** with the score clock, so the whole 10.4 MB chunk was re-stored; afterwards the clock (both copies), intro and end-of-period movies all read back byte-identical to their built files.
- **Line on the ice (LineFatigue panel), in the game and confirmed by the user (2026-11-25, "looks good"):** the user picked the **horizontal bar** (`previews/ihn/linefatigue-horizontal.png`) in navy/gold. Only `faceoffstrategy.layout` (collector `globals/chunkfilecollectoruicommon_nhluidata`) draws it; the overlay code `nhl.overlays.linefatigue.LineFatigueOverlay` looks up nodes one level deep only (zinc `TreeNode.getNodeByName` is not recursive), so PanelBase/ProgressFooter/PosPlayersGroup and their named children stay; the nodes it resizes (PanelBackground, edges) are alpha 0 and the bar is `background_white` fills with `color`. The clip top is 994-(n*30+95) and ProgressFooter sits at n*30+49, so the footer (line name + energy bar) is screen-anchored while the rest moves 30 px per player: the bar is drawn for n=3 (screen 928..994), the footer row lands in its lower half (n=3) or upper half (n=2, penalty kill). Energy bar width = 250 - layout x - 28 (x 134 → 88 px). One widget serves both sides, so the home bar is not mirrored; `linefatiguehome.layout` x 1670 → 1221 (bar 700 wide). Source `tools/linefatigue-ihn/build.py OUTDIR SRC_DIR`. Entries "Line on the ice: IHN bar" + "(home x)" via `legacy-set --game <NHL 24 dir>` (**always pass --game: the default is NHL 27**; a first run went to NHL 27 and was restored at once). Restore the home-x entry first.
- **Line change menu, in the game and confirmed by the user (2026-11-25, "Looks good"):** `linechangedisplay.layout` (collector `globals/chunkfilecollectoruicommon_nhluidata`, class `nhl.overlays.linechange.LineChangeElement`) recoloured in place by `tools/linechange-ihn/build.py SRC OUT`: the code keeps resizing PanelBackground (206 + 38*lines), edges and bottom line and moving SelectedLineBackground/StrategyGroup, so those became navy/gold `background_white` fills; header keeps the team-colour glow, gold header lines, IHN tag, gold highlight (alpha .55, white text stays readable: the code cannot recolour one line's text), gold strategy dividers added inside StrategyGroup. `linechangestrategies.layout` is shared with other strategy panels and was not touched (strategy titles stay white). Entry "Line change menu: IHN"; restore it before the Line on the ice entries.
- **Stat overlays: skipped (2026-11-25).** The user keeps EA's stat overlays as they are and has no intent to edit them. Only the menus are left, and only if the user asks.
- **Live shots feed (SOG row), installed 2026-11-25, waiting for the user's check:** NHL 24 never sends shot totals to the score clock (`nhl.overlays.scoreclock.ScoreClockOverlay` only forwards them inside occasional comparison events, `receiveEvent` 8 → `showComparison`), so the SOG row stayed 0. Feed: a lua VM added to `scoreclock.vvm` (`"vms":[["lua","/features/debug/vvms/widgettester/widgettester.lua"]]`; absolute paths work in vvms) runs `tools/shots-feed-ihn/ihnshots.lua`, installed over the unused debug file widgettester.lua (no new legacy files can be added). It polls `nhl:game-stats:team-performance:info:get:01.00.00` with `api("DataWatch").SetupWatch` (frequency 1) and publishes `away|home` (PeriodStatsList[1].Away/HomeTeamStats.Shots) on `bnd_ihn_shots` (viewname `ihn_shots`); `scoreclockoverlay.layout` gets a hidden Label `IhnShots` bound to it; the IHN movie (`pollFeed` in ScoreClock.tpl.as) walks up its display parents to the node with `getNodeByName`, reads the label twice a second. Status texts starting with `!` (`!init`, `!watching`, `!no data`, `!bad data …`) and `FEED: NO LABEL` show in the middle box for this first test. Entries: "Score clock: IHN + shots feed" (ui24 with new `--twin` and `--entry`, stacked on top of the old "Score clock: IHN" so that one stays restorable), "Shots feed script/label/hook (IHN)". ModStore.Add replaces an entry of the same kind+name, so **never reinstall an entry that reads Changed under the same name**.
  - **Test 1 (2026-11-25): the clock disappeared** with the lua VM in `scoreclock.vvm`; restoring only that hook brought it back (so a VM there changes how that VVM loads). It then read FEED: NO LABEL: zinc elements are not DisplayObjects (`TreeNode` wraps `_display`), so walking display parents never meets `getNodeByName`; but `ElementBase` names its DisplayObject after the node. **Version 2:** the script is a second VM of `linefatigueaway.vvm` (that overlay is loaded the whole time the HUD is up; multiple `vms` are allowed), the hidden Label `IhnShots` sits in `linefatigueaway.layout`, and the movie searches the stage for a DisplayObject named IhnShots and reads its first TextField. Scoreclock label entry restored. Entries now: "Score clock: IHN + shots feed 2" (`--entry …(shots feed 2)`), "Shots feed hook/label (IHN, line panel)"; the older "shots feed" movie entry and the widgettester script entry stay (both still in the game).
  - **Test 2: FEED: NO LABEL again.** `TreeNode.set name` doesn't rename the DisplayObject (only a name given to the constructor does), so layout nodes can't be found by name. **Version 3:** every published text starts with the tag `IHNSOG` (the hidden label's default text too, width 2000, resizeType NONE) and the movie searches the stage for a TextField whose text starts with it. If this still says NO LABEL, the overlays are probably separate movie views with no shared stage. Line-panel script/hook/label entries were restored and reinstalled in order; movie entry "Score clock: IHN + shots feed 3" on top.
  - **Test 3: NO LABEL; diagnostic (feed 4) read NL S1 O2047 T126 D15:** the clock movie shares one stage with the other overlays (2,047 display objects, 126 TextFields), so the route works but no TextField held IHNSOG (a binding likely blanks the label's default text until the script publishes). **Version 5:** an unbound marker Label `IhnMark` ("IHNMARK") right before IhnShots in linefatigueaway.layout; the movie finds the marker, takes the next sibling's TextField as the feed label, and reports FEED: NO SCRIPT (label empty), FEED: MARK ONLY, or NL … (no marker: line panel parts not on stage). Errors now show their message (`ERR 1065 …`). Movie entry "Score clock: IHN + shots feed 5".
  - **Test 4 (feed 5): NL S1 O2088 T137 D15** at 20:00: the line panel's marker is not on the stage (its extra root labels aren't drawn, or the 2-VM linefatigueaway VVM doesn't load). **Feed 6:** the same marker + feed label also go into `scoreclockoverlay.layout` (labels alone there are safe); the movie checks every marker and reports `FEED: NO SCRIPT a/b` (a feed labels after b markers). If the clock's label gets the text, bindings cross VVMs; if not, the script isn't running or bindings are per-VVM.
  - **Test 5 (feed 6): FEED: WAITING at 18:47.** The movie found the score clock layout's own IhnShots label by its default text "IHNSOG" (an unresolved binding leaves default text alone), so labels in that layout are on the stage and readable. Nothing the script published ever reached it: either bindings are per-VVM, or the script in linefatigueaway.vvm never runs (that VVM's extra labels never appeared on the stage either). Waiting for the user's choice: one more test (a do-nothing lua VM in scoreclock.vvm, to see if any VM hides the clock) or stop and restore the feed entries.
  - **Restore order now (last in, first out):** End of period → Team intro → Score clock. The earlier two read **Changed** while a later one is applied.- Realistic Sim preset (NHL24 Tuner v1.0, 110 of 115 values) applied 2026-11-25 as ModStore c800c28017ad. Not yet tested in a match.
- 2026-11-25: Realistic Sim preset is now all 115 tuner values (5b858070ace3); Issue fixes 1 is 531e4b1dbe66 (3 values). Not yet tested in a match.

## 34. Center Ice 2026.11.24: rosters only (2026-11-25)

The user asked for only the new Center Ice Mod's rosters, nothing else. Its .cimspkg replaces every file the 2026-11-15
one changed (and has a new "add" action CimsInstaller doesn't take), so it isn't installed:
- `tools/cims-view GAME PACKAGE OVERLAY [--extract]`: rebuilds the package's TOCs over stock ones (the game's TOCs with
  every kit mod's ranges put back, checked by SHA-256), extracts its CAS data (~12 GB), and reads the game through
  `GameLayout.ReadRedirect` (stock file + package data). Saves its legacy DBs to OVERLAY\out. All CRCs ok. Also `--dump`,
  `--diff`, `--newvalues`.
- `cims-rosters DIR --name NAME [--apply]` (`Core/CimsRosters.cs`): the player tables (bio, skater/goalie ratings and
  equipment, players, both roster tables, free agents, resort extras, goalie gear, career stats) of nhl_game.db and
  initial_experience.db become the package's; NCAA league players (ids 40000-56499) and their roster rows are kept.
  Teams, jerseys, arenas, leagues and strings untouched.
- Applied as "Center Ice 2026.11.24 rosters" (kind rosters, 5ede75a9b929): 9,123 package bios + 1,720 league ones;
  read back from the game matches. No new head/hair/eyebrow ids. Not checked in game yet.
- Restore this entry before restoring the NCAA league or the NHL 27 alternates (they share the legacy index chunk).

### §33 update 2026-11-25: SOG hidden, power play box in the team's slot
- Live shots feed abandoned at the user's request. All 11 feed entries restored LIFO; scoreclock.vvm / scoreclockoverlay.layout /
  linefatigueaway.vvm+layout / widgettester.lua back to EA's bytes; the game movie matched App/Assets/scoreclock-ihn.swf again.
  Lesson: the 2-VM linefatigueaway.vvm broke the away line panel. Don't touch other overlays' VVMs for experiments.
- New build (tools/scorebug-ihn/ScoreClock.tpl.as, based on ScoreClock.tpl.before-feed.as; the feed version is kept as
  ScoreClock.tpl.feed-attempt.as): no SOG row. The warning/power play tab is drawn in the team's row slot (rowPts(i), team glow)
  and slides out from under the bar. The middle box shows only for popups, neutral warnings and the shootout (team boxes then
  carry the round markers). The side panel sits at TOP+4 when nothing is below the bar.
- Movie App/Assets/scoreclock-ihn-pp.swf, installed with ui24 --twin score_clock.gfx --entry "scoreclock.gfx (pp box)". Awaiting the user's in-game check.
- 2026-11-25 fixes after the user's test:
  - PP box only appeared after pause/unpause. ScoreClockOverlay sends showWarning only from _runShowPowerPlayScript, on
    penalty overlay / faceoff / done-simming events, and only if powerPlayTimeSeconds > 0. Otherwise only
    updatePowerplayTime ticks arrive, and the movie dropped those while _warnText was null. The movie now opens the box
    on a tick (side AWAY/HOME with a clock other than 0:00). Entry "scoreclock.gfx (pp box 2)".
  - Line bar off centre / at different heights for 3-man vs 2-man lines: LineFatigueOverlay moves the clip by
    30*n+95 and moves only ProgressFooter back (getNodeByName isn't recursive, so the layout can't pin the bar).
    linefatigueaway/home.layout now drop the {side}_players_count -> data binding, so the panel height stays 0 and the
    clip top sits at the panel bottom. The bar is drawn at clip y -66..0 and the footer is centred. Entries "(fixed y)" on all three
    layouts (legacy-set got --entry). Awaiting the user's check.
- **Title screen (2026-11-25):** bootflow/vvms/pressstart plays `nhl/data/common/movie/bootflow/press_start_reveal` (VP8
  1920x1080, 30 fps, 260 frames, 8.63 s, Vorbis 8ch) once and holds its last frame; PRESS prompt at layout 1448,835
  (300x52), legal 1500,1000. My own concept (`previews/ihn/title-ihn.html`, `title-concept.png`) was set aside: the user
  made their own clip (Downloads/1125/1125.mp4; it fades to black, so it was cut at frame 240 = 8.0 s →
  1125-freeze8.mp4 and the encoder holds that frame). Installed with the new `movie-replace … --apply` (CLI; same entry
  as the app's Videos page, kind `movie`) → read back same video, 260-frame table. Fixes: MovieReplacement used Vorbis
  128k, which libvorbis rejects for 8 channels ("Invalid argument"); now 32 kbps a channel (min 128k). The movie lives
  in the base Data CAS (cas_05 / cas_03, contentlaunchsb.toc). Awaiting the user's in-game check.
  - **First install failed in game** (the EA boot movie's last frame stayed up): a MovieTexture2Asset has a block table
    for EVERY track (u32 count + {float time, u32 offset, u32 size}), not only the picture. EA's title movie has 3 tracks:
    1 VP8 picture (und), 2 VP8 alpha (language "eaa", one flat level, 255 = opaque, dropping to 168 at the end),
    3 Vorbis 8ch 48k (622 packets, one per block). It also holds the sound's CodecPrivate offset/size (562/3889),
    per-track kbit/s (20219, 65, 594), the sound's length (7.977) and the file size. Our 2-track encode left tables 2 and 3
    pointing into EA's old file. Table counts are EBX arrays (count + array table at the end), so they can't change.
  - **Fix:** `MovieReplacement.RetableAll` rewrites every track's table (and the CodecPrivate pair) and refuses a file
    whose tracks or block counts differ (EA's own file round-trips with 0 patches; the broken 2-track file is refused).
    `Movies.Tracks` + `Movies.Frames(d, track)`. CLI `movie-replace --webm FILE` installs a ready WebM (padded to size);
    the read-back now compares the whole asset. `tools/movie-ihn/build_title.py` builds EA's layout: Vorbis q 9 cut to
    8.44 s gives exactly 622 packets for clip 1125. Alpha 255 flat. **Confirmed in game 2026-11-25 ("looks clean").**
  - Still to do for movies in general: EncodeAsync only makes picture + sound, so any movie with an alpha track or a
    fixed sound packet count needs this layout (make Encode copy the track layout and tune the sound to the count).

### §33 main menu: top bar test (2026-11-25)
- Concept D (previews/ihn/menu/menu-D-15.png) built by tools/menu-ihn/build.py from EA originals in tools/menu-ihn/src.
- Entries "<file> (ihn top bar)" for mainmenubackground, backgroundwidget (scrim 0.25), mainmenu (logo y, IHN tag),
  tabsviewmanager (tabs y 18, sections y 58), mainmenusection (HORIZONTAL list, scroll steps from EA's trade list,
  d-pad left/right), listmenurow (title 46/40, headers hidden, gold highlight, max width 520), tabbutton (gold tab,
  shared by every tab bar). Movie: "menu movie s6 (1123)" = user's 1123.mp4 (the date picks s6; backup
  nhl24_main_menu untouched). Untested in game at install: horizontal scroll, For You card placement, d-pad.
- In-game fixes: mainmenusection needs checkItemWidths (rows all at x 0 without it) -> entry "(ihn top bar 2)";
  TabsViewSections width 1200 clipped the bar after 4 modes -> 1920, entry "tabsviewmanager.layout (ihn top bar 2)".
  User: "looks good" (2026-11-25). Still EA magenta: For You card (HomepageDynamicButtonTile), now-playing toast.

### §33 pause menu: jumbotron test (2026-11-25)
- User picked concept D (previews/ihn/menu/pause-D.png) from ten (A..J). Built by tools/pause-ihn/build.py from EA
  originals in tools/pause-ihn/src; entries "<file> (ihn jumbotron)" for hubmanager, hubhomesectionprimaryversion,
  gamestatspanel, gamestatsitem, manageteamrow (shared with Manage Teams list), singlefilter (shared filter tabs).
- Home is hub section 0 (scroll clamps it to x 0), so section coords = screen coords; hub y 256 -> 130.
- Matchup art (native ReplayHighlights widget) hidden; no clock in the jumbotron (not in the panel's data).
- Untested at install: Group scale on the score header, horizontal option row navigation, stat tile strip.
- In-game fixes: logos native ~115 -> scale 1.1 at y -34; strip moves by marginLeft (x ignored in PanelLayout);
  neighbour page gap is paddingAroundSelected (400, with paddingBetweenItems 400); option text 23 px via the
  section's CustomRowStyles. Entries "(ihn jumbotron 2/3)". User: "looks good" (2026-11-25). Other tabs still EA.
- End of period/game Home = hubhomesection3stars (and shootout end of game): same jumbotron via jumbotron(); entries '(ihn jumbotron)'.
  User: end-of-period jumbotron "looks good now" (2026-11-25).

## 35. Utah's NHL 27 arena and jumbotron in NHL 24 (started 2026-11-25)

**What picks the arena.** NHL 24 `exhibitionteams[22]` is Utah Mammoth (Center Ice renamed Arizona), arenaid 22.
`exhibitionarena[22]` is "Delta Center" with `arenaartid` 22, and arenaartid is the level number, so the level is
`content/worlds/stadiums/arizonageneric_22`. Row 279 "Arizona Arena" also uses level 22. Only that level places
`structures/arizonageneric/*` and `scoreboard/arizonageneric/*` (`ebx-refs --scan content/worlds`), so rebuilding
those meshes changes only Utah's arena, with no DB entry.

**NHL 27 source.** `content/worlds/stadiums/utah_30` is the real Delta Center. It has 14 layers and 316 placements of
126 meshes, 1.59M triangles in total: `structures/utah/` is 1.37M triangles over 214 placements, the rink pieces are
104k, and props make up the rest. The jumbotron is `scoreboard/utah/` (the main mesh at 18.5k triangles, 4 screens,
ribbon). `utahgeneric_30` is NHL 26's generic Utah. The crowd placement is
`content/crowd/configs/worlds/stadiums/utah/crowd_utah_placement` (type 0132086d, 473 KB). In NHL 24 the crowd placement
is `structures/arizonageneric/crowd` (type 14568e26, 241 KB). NHL 27's arizonageneric placement is 241,266 bytes
against NHL 24's 241,236, so the formats look close.

**NHL 24 host.** arizonageneric_22 has 19 placements and 200k triangles. The bowl (stands, vipboxes, roof, banners)
is only about 20k triangles in about 40 sections, all at identity, with one LOD each.

**Formats (checked).** The same mesh, `structures/arizonageneric/stands_lower_mesh`, holds byte-identical vertex
streams in both games: position 01:03, part index 02:16, UV 21:06, lightmap UV + id 2A:03, and tangent frame 34:0D.
NHL 27 only adds colour 1E:0D, so NHL 27 vertex data copies into NHL 24 raw.
- **Tangent frame (34:0D, 4 bytes):** not a quaternion. Bits 24-26 are the normal's major axis (+x 0, −x 1, +y 2, −y 3,
  +z 4, −z 5; 99.5% match). Bits 0-8 hold one off-axis normal component as c·361 (511/√2). The other fields pack
  additively (borrow patterns 00/FF), and they aren't decoded yet.
- **PS4 MeshSet resize fields (RES length unchanged):**
  - LOD: +0x54 index format (33 = 16-bit), +0x58 index bytes, +0x5C vertex bytes (padded to 16). The chunk holds the
    vertex buffer, then the index buffer.
  - Section (0xE0): +0x1C material index, +0x20 tris, +0x24 first index, +0x28 vertex byte offset, +0x2C vertex count,
    +0xC0/+0xD0 box.
  - Header: box at 0/16; composite part count at +0x98, part boxes (32 bytes each) at the u64 at +0xA4.

**Built.**
- `Core/MeshRaw.cs`: sections with raw element streams, for both games.
- `Core/MeshRebuild24.cs`: rewrites a host mesh's chunk geometry with new counts per section. Source elements are
  copied raw by usage, positions are transformed, and mirrored placements get their winding flipped. Part index and
  lightmap UV keep the host section's first vertex. Section, mesh and part boxes are grown.
- CLI `arena-port elems|placements|build`:
  - `build PLAN.json --game <NHL24> --game27 <NHL27> [--dry DIR] [--apply --name N]`. The plan maps NHL 27 meshes or
    materials, at their level placements, onto host sections.
  - A write is one ModStore entry of kind `arena`: each grown chunk via ChunkGrower, each RES via ResPatcher.
- Plans live in `tools/arena-utah/`.

**Jumbotron: in the game and confirmed by the user (2026-11-25, "it works";
`previews/utah-jumbotron-ingame.png`).**
- Entry "Utah jumbotron (NHL 27)" (`tools/arena-utah/scoreboard.json`) rebuilds `scoreboard_arizonageneric_mesh`:
  - digit page → digits;
  - truss, black metal, speakers, LED body, rope and the ribbon ring → `metal_painted_01_clean_1mx1m_black_mat`;
  - the 4 screens plus Utah's data and ad screens → `scoreboard_screen_01` (the game's live screen content draws on
    them);
  - the Arizona ad strips and body are blanked.
- 18,583 triangles. Read back from the game: screens 78, digits 84, black metal 18,421.
- First resized NHL 24 mesh (new counts, grown chunk, patched RES): it loads and draws.

**Limits.**
- Materials are limited to the host's sections. NHL 27's own textures (Utah banners, arena graphics) can't be bound
  without new material/MVDB entries, and retexturing shared `worlds/textures` would change other arenas.
- Baked lighting belongs to the host: lightmap UVs are the host's constant per section.
- Rotated placements keep unrotated tangent frames (shading only).
- 16-bit indices cap a host section at 65,535 vertices.

**Bowl (entry "Utah bowl (NHL 27)", plan `tools/arena-utah/bowl.json`; in-game rounds on 2026-11-25).**
Every Utah `structures/utah/*` mesh the level places goes into the eight arizonageneric bowl meshes by material family
(stands_lower, stands_upper, railings, vipboxes, roof, roof_speakers, banners, powerring). Lessons from the test rounds:
- **Placement: `"placement": "identity"`.** ArenaScene's transform arrays are found heuristically, and for utah_30 many
  belong to other records: the first apply put 14 rotated, 0.75-scaled copies of `stands_lower_01_mesh` over the ice.
  Utah's structure meshes are modelled in place, so each goes in once at identity. `"clearBox"` (rink interior,
  y > 1.2) leaves out whole meshes modelled around the origin that need a parent transform (aisle railings), and
  `railings_05_*` is dropped by name (they sit at ice level). Roof trusses `roof_lower_01_mesh` go in once (29k tris).
- **Index format.** Sections over 65,535 vertices switch the LOD to format 46 (32-bit); NHL 24 uses 46 on 50 LODs.
- **Lightmap UV (0x2A).** Its last u32 is the section's lightmap chart id (Carolina stands_lower: 0x60, 0x74, 0x86 …
  per section). Copying it from another section renders flat white (lavender under the sky). Per-vertex nearest
  lookups also banded. Now: one entry per piece, the nearest old vertex of the same host section to the piece centre.
- **UVs.** Strip/atlas host textures (stairs, black-stripe aluminium, bare railing, roof beams, black cloth, rubber floor)
  streak under Utah's tiling UVs: `"flatUv"` keeps the host's first UV. `"ringUv"` rebuilds the power ring's u from
  distance around the ring at NHL 24's 0.172 u/m (Utah's upper ribbon spans u 0..1 once around: one stretched image);
  the seam is behind the +z end.
- **Two-sided.** NHL 27 draws walls, curtains and railings two-sided; NHL 24's host materials don't, so the concourse
  walls vanished from the rink side. `"doubleSided"` adds back faces for those host materials (+~170k triangles).
- **Suites.** In NHL 27, Utah's suites (`stands_vipboxes_02/03`) are on the −x side only; +x at that height is a
  standing-room concourse whose back walls are plaster (now grey concrete). That side is NHL 27's camera side, so the
  band above the concourse (y 13–17) is simply left empty there and shows sky in NHL 24. The plan's new `"extra"` list
  (identity placement only) puts matching meshes in again under a transform: `stands_vipboxes_02*/03*` (suites and
  their bg meshes) go in a second time mirrored across x (det < 0 flips the winding; the packed tangent frames are copied
  as they are, so the mirrored suites' shading may be off).
- **Texture density.** Section +0x34 is the section's UV units per metre (Carolina stands_lower: 0.0842/0.0846/0.0709/
  0.1304 against measured 0.083/0.085/0.071/0.13), which texture streaming chooses mips from. The rebuild now writes
  sqrt(UV area / world area) of the new geometry there (e.g. vip_ceiling_tiles 0.169 → 0.937); flat-UV sections keep
  the host's value. It is the suspect for the white "unloaded" surfaces.
- **White glass.** NHL 24's `glass_stadium_generic_color` is flat white (230) at alpha 36–98. With all of Utah's glass in
  it, two-sided, glass edge strips (`glass_*_edge_mat`, face-on as thin bands) and `glass_mirror` drew bright white
  outlines on suite fronts and windows. Those two are now dropped and the panes are single-sided (7,436 → 1,124 glass
  triangles). The +x suites (mirrored) were confirmed in game 2026-11-25.
- **Flat UV texel.** That didn't clear the white: flat-UV sections take the host's first UV, which for
  `stair_alluminum_blackstripe` (all of Utah's bare/painted metal: railings, the camera scaffold and its stairs) is the
  167-grey band, and NHL 24's arena lights push that to white. `"flatUv"` can now be an object {material: [u, v] or
  null}; the metal sits on the dark stripe (0.5, 0.9). (The OBJ dumps write v as 1 − v.)
- Still light grey in game, so all of Utah's metal now goes into stands_lower's `cloth_canvas_black_mat` (flat,
  two-sided), and all glass is dropped (the white square on the end wall was a pane). railings_mesh and vipboxes_mesh
  each keep one tiny dark metal piece (16 / 8 triangles) so no LOD chunk is empty.
- **No Utah textures yet.** Everything borrows level 22's host textures, which other arenas share. Real Utah looks need
  NHL 27 textures converted to PS4 and written into texture slots only level 22 uses (or new ones).
- Level 22 is about 830k triangles (stock 218k). Utah's two branded fabrics (`fabric_canvas_utah_01/02_color`) are
  plain grey: their look comes from material tint or another texture, so they stay black for now.

**Crowd (entry "Utah crowd (NHL 27)", `arena-port crowd`, `Core/CrowdPort.cs`).** NHL 24's
`structures/arizonageneric/crowd` and NHL 27's `crowd_*_placement` are the same EBX layout: a root with a 16-byte seat
position array and a pointer array of channels (Orientation f32, Layer u8, Attendance u8, FanProfiles u32, SeatColour
u32, Tier u8; NHL 27 adds ActivatedPairIdAndIndex). NHL 24's and NHL 27's arizonageneric files differ only in the root
type guid/signature, the first two EBXX array hashes and the instance guids. The port takes Utah's file and puts back
NHL 24's file guid, instance guids, root type, signature and array hashes, cuts the channel list to 6, overwrites the
name, wraps orientation to ±180 (same convention: +x seats face 180°), and copies Layer/Attendance/FanProfiles/
SeatColour/Tier from the nearest NHL 24 seat in a normalised bowl (the games use different value conventions). 13,486
seats; the 473 KB EBX is appended by the new `ResGrower.PlanEbx` (EBX mode: TOC offset/size, record size and SHA1).
Read back byte-identical; fans sit in Utah's stands in game.

**Corners.** One end's tunnel corners look black. In NHL 27's own model three of the four corners carry black metal
fascia and curtain on those walls (~80 / 34 m²; the +x+z corner ~22 / 14 m²), so the black follows NHL 27's
materials. A lightmap test went out with it anyway: `"lightmapMirror": "+z"` (−z pieces take their lightmap point from
the mirrored +z position) and a facing-axis preference in the lightmap lookup (old vertices facing another way count
4 m further). Not yet judged in game.

**Jumbotron textures (entry "Utah jumbotron textures (NHL 27)", `arena-port mvdb-tex`).** Materials are shared
SurfaceShaderPresets, but each level's MeshVariationDatabase binds textures per mesh material by import. Only
`arizonageneric_22/environment_replay/meshvariationdb_gen4b` lists the scoreboard mesh, and its one import of
`scoreboard_metal_chicago_color` (host section 9) now points at `scoreboard_arizona_signs_color`. That texture is
Arizona-only and already in level 22's bundles; Chicago's jumbotron is untouched (the chunk is shared, so retexturing
Chicago's metal was out). The signs sheet is now an atlas, written by `retexture`: top half = NHL 27's
`scoreboard_scoredatascreen_utah_color` (DELTA / DELTA CENTER / UTAH MAMMOTH), bottom half = Utah's signs. Utah's ad
(v 0–0.42) and data-screen (v 0.42–1) faces go into section 9 with the new rule `"uv": [1, 0.5, 0, 0]`. The other three
Arizona state sheets (clap, crowd_meter2, make_some_noise) are retextured with Utah's (same sizes). Confirmed in game 2026-11-26.

**Roof lights (entry "Utah roof lights height", `arena-port lift`).** The hanging lamps are level 22's own Arizona
fixtures at Arizona's roof height, y 17.99: a 7 × 9 grid (x −20.19…20.7, z −32.06…32.09) of baked transforms in
`arizonageneric_22/environment_common`. Editing the prefab they came from
(`structures/arizonageneric/roof_prefab_nongroupable_autogen_gen4`, nine `lights_arena_flood_12` rows) changed nothing
in game: the level layer keeps its own cooked copies, so edit the layer. `lift` moves those 63 to y 39.5, hung from
Utah's corrugated deck (39.6 m; truss chords ~35 m; the lamps hang up to 3.5 m below their origin). The rink light rig
(`lightrig_scaleable_rink_common_new`, shared, 10 spots at y 13–14 after the layer's −11 offset) is the light source
and stays put. The jumbotron's Delta Center / UTAH MAMMOTH art was confirmed in game 2026-11-26, so the MVDB texture
binding wins over the preset.

**End walls.** Not started. Utah's `fabric_canvas_utah_02` colour texture is plain grey, so NHL 27's branding there
comes from elsewhere (decal meshes or another layer); this needs an NHL 27 reference shot of the wall.

**Restore order.** The entries ("Utah jumbotron (NHL 27)", "Utah bowl (NHL 27)", "Utah crowd (NHL 27)", "Utah jumbotron
textures (NHL 27)", "Utah roof lights height" and the four `scoreboard_arizona_*_color` texture entries) touch different assets and restore in any order.

**Next.** Utah textures: NHL 24's Carolina meshes already use NHL 27-style surfacing materials
(`concrete_bare_01_clean_2mx2m_*`, `fabric_canvas_01_2mx2m_black`), which may give closer textures than the generic
arizonageneric ones; otherwise write NHL 27 textures into texture slots only level 22 uses. The user offered to point
out what each surface should look like.
