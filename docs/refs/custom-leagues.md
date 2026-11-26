# Adding a custom league to NHL 24 (PS4, CUSA37933, 01.00)

This is how the USHL was added as a playable league: it shows in Play Now, its teams and lines open, and games load with it. The same steps work for any other league.

**Status, 2026-10-16:**

- Working in game: the league, its 16 teams and rosters, lines, games, team names, arena names, team colours.
- Built but not yet tested in game: logos.
- Not started: generated jerseys.

Tools:

- `C:\dev\NHL24Kit\mods\ushl\UshlBuilder` builds the rosters.
- `C:\dev\NHL24Kit\mods\ushl\LocAdd` adds text strings.
- `C:\dev\NHL24Kit\mods\ushl\make_logos.py` makes the logo textures.
- `C:\dev\NHL24Kit\mods\ushl\deploy_logos.sh` copies the logos into the game folders.

---

## 1. Which files to edit

Play Now reads two roster databases, both legacy files in t3db format (a FIFA-style database plus a meta XML). Edit **both**:

| File | What it holds |
|---|---|
| `gamemodes/database/t3db/nhl_game.db` | The full database (119 tables): leagues, teams, players, stats, schedules. |
| `gamemodes/roster/initial_experience.db` | A newer roster (about 9,000 player bios against 8,134). It has no `stockleagues` and no `stockteamcolorstable`. |
| `loc/eng_us.db` | Every on-screen string, team names included. |

Both roster files use `nhl_game-meta.xml`. The Worker writes them back with `LegacyWriter` through a plan's `"legacy"` entries (see `test/plan_ushl.json`).

How to test whether the game reads an edit: change an *existing* row and look for it in game. Moving the Brandon Wheat Kings to the AHL in place showed up straight away. A team-name edit does **not** work as a test, because names come from the loc database (section 6).

---

## 2. The league

- `stockleagues` already defines leagues 0–32. Leagues 28 (CHL), **29 (TXT_USA_LEAGUE)**, 30 (Russia) and 31 (rest of world) have no teams. The USHL uses **29**.
- A league appears in Play Now as soon as it has at least one valid team. The game builds the league list from the teams it can load, not from `stockleagues`.
- Team group fields: `leaguegroup = conferencegroup = divisiongroup = 24` (one table, like the European leagues). Group 24 is unused. The team-by-league service drops teams whose group is above 32 unless the group is on a built-in allow-list.
- League name: set the loc string `TXT_USA_LEAGUE` to "USHL".
- The debug tweakable "Enable leagues outside of modes..." isn't needed for Play Now. It only affects which leagues appear in some modes.

---

## 3. Teams: take over existing rows, never add new ones

**New rows added to `exhibitionteams` never appear in game.** That held sorted or unsorted, and in a stock league or league 29.

The fix is to take over unused existing rows. There are exactly 16 "Aces" placeholder rows at **883–898** (league 17, `teamtype` 1, inactive, no players). They're EASHL custom-team slots that offline modes never use. Each comes with its own:

- arena row (same index);
- strategy row (same index);
- 3 jersey rows;
- 39 equipment rows.

For each USHL team, copy a template team (a WHL team) into the slot, then:

- set the team row's `index` (slot), `league`, the group fields, `teamname`, `cityname`, `nickname`, `abbrname`, `artabbr`, `artid`, `arenaid` (slot), rivals set to -1, and colours;
- **overwrite in place** the slot's own strategy, arena, jersey and equipment rows with the template's. Rows added to `stockteamjerseys` are ignored too, which crashed the lines view because the jersey list came back empty.
  - Jerseys: each template jersey replaces the slot jersey with the same `jerseyindex`.
  - Equipment: matched by `equipment` + `variant`.
  - Template rows beyond the slot's count are skipped. For example, Red Deer's 4th jersey has nowhere to go.

Afterwards each table keeps exactly the stock row count; only player rows are added.

---

## 4. Players and rosters

Added rows **do** work for players: `exhibitionplayers`, `exhibitionplayerbiotable`, `exhibitionskaterai`, `exhibitiongoalieai`, the equipment tables and `exhibitionrostertable`.

Rules, each one learned from a crash:

1. **Player IDs: use 27019–56499.** IDs above the stock exhibition range (56818–59999) already belong to custom players in `nhl_game.db` (custom bios, goalies, resort extras). Reusing them made the game pick the wrong player as a team's top player, and Play Now crashed at eboot `0x3935081`. The builder uses 40000+ for both `playerid` and the `exhibitionplayers` index.
2. **Roster keys encode the team: `key = team * 40 + slot`** (at most 40 players per team). The game finds a team's roster by that key range. Every stock row follows the rule. Wrong keys made the roster come back empty, and the lines view crashed at eboot `0x3ce1809`. This is probably also why added team rows looked "ignored": their rosters weren't found.
3. **Keep every table sorted the stock way**, because the game binary-searches them:
   - teams, strategies and arenas by `index`;
   - players by `index`;
   - bios, AI and equipment by `game_id`;
   - roster by `key` (which is team order);
   - jerseys and team equipment by team.
4. Names: generated from `playernames.csv`, using real surnames that have commentary audio (`surnameid`).

To read a crash address: shadPS4 (the nhl24pc build) loads the eboot at the address shown in the log line `Loading module eboot.bin to 0x...` (it varies between runs, e.g. 0x5220000). Subtract that from the exception address to get the image address, then disassemble `test/eboot/image.bin` (scratchpad `disx.py`).

---

## 5. Writing the databases

- `FifaTable.ConvertToDataTable()` returns every slot (live records first, then free ones). New rows go into free slots.
- After `ConvertFromDataTable`, put `m_NMaxRecords` and `m_NValidRecords` back with reflection, so the file keeps its layout and size. Restore the counts of tables you didn't touch as well.
- The records CRC is written as 0, and the game accepts that.

---

## 6. Team names (loc strings)

On-screen names come from `loc/eng_us.db`, table `LanguageStrings`, keyed by the team's **`artabbr`**:

| Key | Text |
|---|---|
| `NHLTeamName_<artabbr>` | Full name ("Chicago Steel") |
| `NHLCityName_<artabbr>` | City |
| `TXT_NICKNAME_<artabbr>`, `TXT_NICKNAME_ALT_<artabbr>` | Nickname |
| `TEAMLINE1_<artabbr>`, `TEAMLINE2_<artabbr>` | Two-line name (city, nickname) |
| `NHLTeamName_Abbr3_<artabbr>`, `X_XLA_TEAM_<artabbr>` | 3-letter abbreviation |

The database's `teamname` and `abbrname` aren't shown in game. Give each team a unique `artabbr`; the USHL uses `"USHL" + abbr`, e.g. `USHLCHS`. Otherwise the team shows its template team's name.

**The key hash (`hashid`):** a reflected CRC-32 (polynomial 0xEDB88320, init 0, no final XOR) over every byte of the string ID with bit 5 cleared (`b & 0xDF`; the game's "uppercase", which also changes digits), then XOR 0x80000000. This matches 65,251 of the 65,253 stock rows; the two misses have non-ASCII IDs.

```python
def loc_hash(s):
    c = 0
    for b in s.encode():
        c = TABLE[(c ^ (b & 0xDF)) & 0xFF] ^ (c >> 8)   # standard CRC-32 table
    h = c ^ 0x80000000
    return h - (1 << 32) if h >= 1 << 31 else h   # stored as a signed 32-bit int
```

`LanguageStrings` is full (valid = max) and sorted by signed `hashid`. `LocAdd` grows the table and sorts again; the file gets larger, which `LegacyWriter` handles.

How the hash was found: two keys that differ only in their last character (`NHLTeamName_0` and `NHLTeamName_1`) have hashes that XOR to `0x77073096`, which is entry 1 of the CRC-32 table. Solving the rest showed a constant `0x80000000` and the `& 0xDF` uppercasing.

---

## 7. Team colours

These are set in the team row: every column ending in `primarycolor`, `secondarycolor` or `tertiarycolor`. That covers the `pres*`, `fe*` and `overlay*` columns used by menus, team cards and the scorebug. The USHL uses the official palette in the order given: primary, secondary, tertiary.

---

## 8. Logos: new textures, nothing reused (built, not yet tested)

Rule: don't overwrite any existing texture. That includes other leagues' logos, unused Champions League logos and jerseys.

Each logo variant is its own small bundle (`ui/textures/common/teamlogos/<variant>/t<artid>`, 18 variants). Adding bundles would mean rebuilding the table of contents' perfect-hash index and its Huffman-compressed name table. Instead, the game has a fallback (`NHL::CustomLogo`, eboot about `0x80519f0`):

1. Try `fb://ui/textures/common/teamlogos/<variant>/t<artid>` (the built-in texture).
2. **If that doesn't exist**, load `kettle_assets/teamlogos/<variant>/t<artid>.gnf`, a loose live-content file (EA's way to ship logos for new teams). The kettle downloader writes these to `/tempdata/nhl/ui/kettle_assets/...`, and `/tempdata` is `/download0/temp/`.

So the USHL teams get **art IDs 2901–2916**: no built-in logo or team uses 2000–4000. `make_logos.py` generates every variant in the stock sizes and styles:

| Variant | Size | Style |
|---|---|---|
| extralargemono | 1024 | mono |
| large, largemono, largestylized/left, largestylized/right | 512 | plain / mono / 3D left / 3D right |
| medium, mediumglossy, mediumstylized/left, mediumstylized/right, hutcardmedium | 256 | plain / glossy / 3D / plain |
| small, smallglossy, smallstylized/left, smallstylized/right | 128 | plain / glossy / 3D |
| extrasmall, extrasmallmono, hutcardsmalllogo | 80 | plain / mono |
| hutcardbw | 332×464 | mono, cropped |

The files are BC7 GNF textures, built from a stock GNF header (`test/espn/build/score_clock_i1.gnf`) patched for each size:

- word 2 = (width - 1) | (height - 1) << 14;
- word 4 pitch = width rounded up to 32, minus 1, at bit 13;
- word 7 = data size (whole 8×8-block tiles × 16 bytes);
- byte 12 = file size.

The Worker's `png-gnf` then encodes and tiles them.

Which folder the game reads isn't confirmed yet, so `deploy_logos.sh` copies them to every candidate (`user/download/CUSA37933/temp/nhl/ui/kettle_assets`, `user/download/CUSA37933/nhl/ui/kettle_assets`, `user/temp/CUSA37933/nhl/ui/kettle_assets`, `user/download/CUSA37933/temp/kettle_assets`). After an in-game test, shadPS4's log of failed opens shows the real path.

---

## 9. Still to do

- Confirm the logo folder in game, then remove the extra copies.
- Simple generated jerseys in team colours. These must also be new textures; the same fallback, or new assets, still needs to be investigated.
- Helmet and glove colours still come from the slot's or template's equipment rows.
- Packaging: the USHL entry in the 24Kit mod list holds the three databases. The loose logo files are copied separately.

---

## Things that looked like answers but weren't

- **"The game ignores the edited database":** it doesn't. Team names just come from loc, so name edits never show.
- **"The league is filtered out in the eboot":** league 29 passes every check in `GetFilledLeaguesList`. The real problem was the teams.
- **"Equipment duplicates cause the crash":** stock teams have duplicate equipment identities too. The Play Now crash was the player-ID collision.
- **Rebuilding the table of contents to add logo bundles:** possible, but unnecessary thanks to the kettle fallback.
