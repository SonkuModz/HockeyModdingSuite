# NHL 27 Be A Pro saves

What a Be A Pro save holds, worked out by reading `BeAPro0` (a career at the World Juniors stage, no games played yet).
The kit's Be A Pro page and `nhl27kit bap` edit ratings and X-Factors (`Core/BeAProSave.cs`).

## Where

KytyPS5 keeps saves unencrypted in `%LOCALAPPDATA%\KytyPS5\run\_SaveData\PPSA34063\<slot>\`:

- `BeAPro0`: the career. `BeAPro12`: the game's backup copy of it (`"IsBackup":true`). `Profile0`: profile settings.
- Each slot has `DATA`, `INDEX` (320 bytes), `NAME` and `sce_sys/param.bin`, `sce_sys/blocks.bin`.

## DATA and INDEX

`INDEX` lists the files packed into `DATA`, each as a 0x50-byte entry: a name (`BeAPro0`, `__SaveLoadFileInfo`,
`__SaveLoadMetaData`, `__SaveloadVer`), a count, and the start and end offsets in `DATA`. Each packed file starts with
u64 length and an 8-byte save id that's the same in every file of one save and differs between the two copies (not a
checksum).

- `__SaveLoadMetaData`: JSON (position, team, injured, first and last name, season `20260103`, `IsBackup`).
- `__SaveloadVer`: `c@1.031`.
- The career (`BeAPro0`): after the 16-byte header, the byte `01` and an LZ4 frame (block-independent, content
  checksum). Its contents are a stream of sections, each either `01` + an LZ4 frame or raw bytes.

| Stream offset | Kind | Size | What |
|---|---|---|---|
| 0 | LZ4 | 216 KB | Career state: 64 numbered settings (u32 id, i32 value), then the career's people (coach, agent, media), conversations and story events |
| 72,640 | raw | 180 B | |
| 72,820 | LZ4 | 5.4 MB | Player records (below) |
| 1,126,837 | raw | 160 KB | |
| 1,287,393 | LZ4 | 2 MB | League database: a t3db with the 32 `beapro` save-group tables of `nhl_game` (named by `nhl_game-meta.xml`, CRCs good; `nhl27kit db gamemodes/database/t3db/nhl_game.db --file FILE`) |
| 1,726,753 | raw | 258 KB | `CompleteFeedbackBlob`: coaching feedback grades by skill (the part that differs between the two copies) |
| 1,985,272 | LZ4 | 216 KB | Not identified |
| 2,018,335 | LZ4 | 68 KB | Ten-byte records, likely tournament standings |
| 2,018,853 | LZ4 | 54 KB | Mostly empty |
| 2,019,149 | raw | 12.5 KB | The pro's career block (below) |

## Player records

u32 total length, u32 count (4,997), then records of 964 bytes (skaters) or 896 (goalies; layout not mapped).
Offsets from the record start (hometown, 40 chars; first name at +40, last name at +65):

| Offset | What |
|---|---|
| +116 | u32 game_id (-1 for a created player such as the pro) |
| +140 | u32 player index (4996 for the pro) |
| +446 … +455 | aggression, agility, durability, endurance, fighting (frequency), **overall**, passing, poise, (potential?), speed |
| +466, +469 | growth letter, growth tier |
| +498, +499 | trait0, trait1 |
| +538 … +555 | acceleration, balance, body checking, defensive awareness, deking, discipline, faceoffs, fighting skill, hand-eye, offensive awareness, puck control, shot blocking, slap accuracy, slap power, stick checking, strength, wrist accuracy, wrist power |
| +556 … +573 | the same 18 again, a little lower for the pro (base vs boosted, not confirmed which) |
| +651 … +656 | category ratings: defense, physical, puck, sense, shoot, skate |
| +744, +748, +752 | i32 X-Factor ability ids (-1 none; names TXT_XFACTOR_N) |
| +756, +760, +764 | their tiers (1 Specialist, 2 All-Star, 3 Elite) |

Found by matching about 500 skaters against `exhibitionskaterai` (exact for fighting skill and trait0; ±2 for the rest,
since ratings move during a career).

The pro ("The goat", id 4996): overall 73, X-Factor Quick Release (Specialist), team 140 (USA), #27, second line.

## The pro's career block

Starts with u32 4996. Fifty-odd numbered 192-byte slots (empty: likely per-game or per-season lines), a copy of the
ratings, two runs of six i32 — 42, 38, 42, 22, 24, 90 and 7, 4, 6, 5, 4, 3 — which fit the six energy activities'
current values and levels (Management, Teammates, Brand, Health, Skill Training, X-Factor Training), then a 523-entry
(u32 key, i32 value) table: create-a-player choices (1–~470, sliders at 50), then money-sized values (479: 76,900,000,
480: 104,000,000, 482: 850,000, 483: 20,800,000, 484: 1,025,000) and 491: 27 (jersey).

## What the game tracks (names from service-defs and the eboot)

`gamemodes/service-defs/data/bap/*.json` describe the screens: `AvailableTraitPoints`, `AvailableSpecializationPoints`,
`AvailableEnergyPoints` (0–1024), `MaxEnergyPoints`, `EnergyPoints` (-2048–2048), health points, purchased traits (up
to 1,600) and specializations (400), perks (with costs and counters) and `CurrentFunds`. `gFEGameModesBeAPro_InitialFunds`
defaults to 0.

## Not mapped yet

Personal stats (none yet in this career), trait and specialization points, energy points and funds. Candidates in the
career-state settings: #99 = 1150, #69 = 50000, #241 = 85, #177 = 28. A save from before and after one game, one
trait purchase or one energy spend would settle them.

## Writing

The end of the career stream holds a block table: 15 entries of (u32 block id, u32 offset, u32 stored size, u32 raw
size), then a u16 count. Raw size 0 means the block is stored plain. Otherwise the block is `00` + raw bytes (under 512
bytes) or `01` + an LZ4 frame (64 KB independent blocks, content checksum). The loader (eboot 0x38FCCA0, table parser
0x38FC230) only bounds-checks and finds blocks by id, with no hash, so a rebuilt stream loads. The career file is then
u64 length, save id, `01` + LZ4 of the stream, and u32 stream length. INDEX offsets are rebuilt to match.

| Block id | What |
|---|---|
| 0x1B9A22BB | Career state |
| 0xA086F262 | Player records |
| 0x423A2DAC | League t3db |
| 0xC0F3C086 | The pro's career block (plain): ratings at 6188 (overall, then 25 alphabetical), activities at 6272 and 6328 |
| 0x5858B61E | Create-a-player key/value table |

A rating is written to the record's current value and to the pro block. The base copies (+556, +617, or +489… for the
first list) move by the same amount.
