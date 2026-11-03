# NHL 27 eboot: attributes and tuning

What the game's executable (`NHL 27 dump/NHL/eboot.bin`, 281 MB) says about player attributes, and the gameplay tuning
values compiled into it. Read only; nothing here changes the game.

## The file

A decrypted PS5 SELF (magic `54 14 F5 EE`), 12 segments, none compressed, ELF header at 0x1A0. Loaded segments
(virtual address → file offset):

| Segment | VA | Size | File offset | What |
|---|---|---|---|---|
| ph0 | 0x0 | 0xBFAD8CC | 0x608F0 | code |
| ph1 | 0xBFB0000 | 0x2395DC4 | 0xC01FE80 | read-only data (strings, float constants) |
| ph3 | 0xE348000 | 0x8EFAD8 | 0xE3BA3D0 | data |
| ph7 | 0xEC38000 | 0x5B76B0 (mem 0x1F69E90) | 0xECACC70 | data + bss |

`tools/elf.py` prints this; `tools/strings.py` dumps every printable string (853k).

## Attributes

The game has 26 skater and 20 goalie ratings (overall included). Three names for each, all in the same order:

- eboot enums `SKATER_ATTRIBUTE_*` / `GOALIE_ATTRIBUTE_*` and the short `ATTRIBUTE_*` set;
- the database columns in `exhibitionskaterai` / `exhibitiongoalieai` (`c_speed`, `c_handeye`, `c_gsh` = glove high,
  `c_gsl`, `c_ssh` = stick high, `c_ssl`, `c_5hole`, …);
- `ratings_creation_zone.csv` (game file `gamemodes/creation_zone_csv/playerratings.csv`): RatingId 0–45 ↔ IsGoalie ↔
  AiAttribute (the legacy AI attribute index the eboot's `PlayerRatings::mAttributeIdTo{Skater,Goalie}LegacyAIAttrId`
  maps to) ↔ identifier (`SKATERAI_HANDEYE` …).
- `ratings_kernel_entity.csv` (`gamemodes/kernel_entity_csv/ratings.csv`): the same with display names (Glove High,
  Puck Play, Rebound Ctrl …).

Goalie columns: overall, aggression, agility, positioning (angles), breakaway, durability, endurance, five hole, glove
high, glove low, passing, poise, poke check, puck play frequency, rebound control, recovery, speed, stick high, stick low,
vision. Skaters: overall, acceleration, aggression, agility, balance, body checking, defensive awareness, deking,
discipline, durability, endurance, faceoffs, fighting skill, hand-eye, offensive awareness, passing, poise, puck control,
shot blocking, slap shot accuracy and power, speed, stick checking, strength, wrist shot accuracy and power.

Other eboot systems that read attributes (by their names): X-Factor boosts (`PlayersAttributeBoostMap`), injuries
(`AffectedAttributesList`, `AI/Injuries/AttributeMod`), morale (`PlayerRatings::ModByMoraleInfoGet`), dynamic ratings
(`DYNAMIC_RATINGS_MODS`), progression (`PlayerAttributeProgress`, `AffectedAttributeMaxXP`), scouting, World of Chel
loadouts (`weightModsByWeightClassAndAttribute`) and card-house training boosts.

## Tuning variables ("tunables")

The eboot registers **24,412** gameplay tuning values at start-up (`tunables.csv`: 16,201 float, 5,401 int, 2,810 bool),
each with a type, default, minimum, maximum, a group path (`AI/Goalie/HIKSaves/Abort`, `Physics/Prototype/Balance/…`)
and often a readable label. 318 name a rating or attribute outright; `attribute_tunables.csv` (from the first pass, which
read 14,171) sorts 641 by the attribute they touch, for example:

| Name | Default | Range | What |
|---|---|---|---|
| gBackskatingRatingModifierForward | 0.85 | 0–1 | forwards skate backwards at 85% of their rating |
| gBackskatingRatingModifierDefense | 0.95 | 0–1 | defencemen at 95% |
| gAbortSavePositioningRatingWeight | 0.15 | 0–1 | goalie abort-save weight from positioning |
| gGoalieButterflyPushMaxSpeedRating | 0.95 | 0–1 | butterfly push speed at the best rating |
| gDekeRatingEffect | 1.0 | 0–1 | how much the deke rating matters |
| gFightRatingSkillWeight | 0.7 | 0–2 | fighting skill's weight in a fight |
| gArcadeModeUniformPlayerRatingValue | 99 | 35–99 | every player's rating in arcade mode |
| gMaxPoiseEffectPlayoffGame | 4.0 | 0–50 | most a playoff game moves poise |

`player_type_defaults.csv`: the EASHL player-type tuning (15 `EASHL_TUNING_*` groups: Grinder, Playmaker, Sniper,
Dangler, Butterfly, Standup …), mostly placeholder 70s (range 35–99) in the eboot; the real values probably come with
the online tuner.

### How they're registered

Each is a static object built by one of a few constructors, called as
`ctor(this, group, name, label, …values)`; `this` goes on a global linked list (`[this+0x28]` = previous head):

| Constructor | Type code (`[this+0x20]`) | Values |
|---|---|---|
| 0x13139E0 | 1 bool | `r8b` default |
| 0x1313AE0 | 2 int | `r8d` default, `r9d` min, max pushed on the stack |
| 0x1313C00 | 4 float | `xmm0` default, `xmm1` min, `xmm2` max (stored at +0x38/+0x3C/+0x40) |
| 0x13146B0 | 0xE | pointer + two floats |
| 0x13143A0 | 0xC | int, pointer, float |
| 0x1314540 | 0xD | int, pointer, ints |

The kit reads them itself (`Core/Tunables.cs`, the **Tuning** page and `nhl27kit tunables --out FILE.csv`): it takes the
segments from the SELF header, finds every `\0g…\0` name in read-only data and every RIP-relative `lea` that points
at one, decodes back to the call before (Iced) and reads the arguments. The constructors are recognised by the type code
they store, not by address. 14 defaults are computed at start-up and aren't read; the three rare types aren't decoded.
`tools/lea.py` + `tools/tunables.py` are the first Python pass (needs numpy and capstone).

The game also has an online "tuner" (`gamemodes/service-defs/data/game_setting/tuner.json`: versions, backup, latest),
so these defaults may be overridden by a downloaded tuning set at run time.

## Changing them (not done)

The float defaults are loaded from shared constant-pool entries (one `0.95` serves many variables), so changing one
variable means patching its `vmovss` to point at a new constant, or the value after construction, not the constant.
Patching the eboot also needs the kit to write a modified eboot, which it doesn't do yet.
