# NHL 24 commentary: how lines are picked (research, 2026-11-22)

Goal: port NHL 27's commentary audio into NHL 24 (NHL24 PC 0.9.3b, v1.60 with the Center Ice Mod installed; NHL 27
from `NHL 27 dump/NHL`). Everything here is read-only research. The sentence → phrase → wave chain below comes from
field names in the eboot, not yet shown on data.

## The two games' audio

- Same speech system in both (Frostbite sentence player): `sound/speech/<system>/` with `database/` (SentenceDatabase,
  SentenceKeywords, SentencePickups, SentenceSampleMetadata), `context/` (ContextSystem), `wc/` (SentencePlayerWaveCollection
  per bank: `eng_inxp`, `eng_lcu_01..10`, `eng_bank`, `eng_cgc`, `silence`) and `soundwaves/` (NewWaveAsset per line).
  Systems: maincommentary, announcer, threes, gauntlet, fe_bap, fe_woc.
- Same codec: EA SPS + Opus multistream, every take readable in both.
- NHL 27 has *less*: 71,607 takes / 55.4 h (6,475 waves) vs NHL 24's 118,801 takes / 165.2 h (9,155 waves).
- 30,493 of 27's takes are byte-identical to a 24 take. In `eng_inxp` shared waves: 25,374 identical at the same take
  number, 4,915 identical at another take number, 12,138 different at the same take number, 3,931 extra in 27.
- Differing takes are different audio, not re-encodes (300 decoded pairs: 0 same recording; 275 different length).
- 1,321 waves exist only in 27 (14,466 takes, 10.6 h): new milestone / penalty-explained / recap lines, Be a Pro
  conversations and cutscenes, announcer team intros, new surname waves.
- WAV pairs: `export/commentary-compare.zip`.

## How a line is chosen (NHL 24 eboot type info)

The eboot builds its reflection data at start-up (RIP-relative `lea name` + `mov reg, packed` per field; field record
0x18 bytes: name pointer, packed u64 = name hash | flags << 32 | offset << 48, type pointer). Tool:
`tools/typeinfo` (`--fields Class…`, `--range A Z`). Chain:

- **SentenceDatabase** → `SentenceEvent` (EventIndex, Sentences, EventParameters, SoundMappingParameterIndex) →
  `Sentence` (SentenceId, Probability, Phrases) → `Phrase` (NewWave, SelectionGroup, Parameters/ParamMappings,
  PlaybackMessage). `ParamMappingDescription` maps a game parameter (ParameterName, SelectionParameterHash) onto the wave.
  SentencePlayer options include `OverrideVariationIdWithSampleId`.
- **NewWaveAsset** (EBX ~520 B): points at one audio chunk (all takes back to back, SPS streams with plain 12-byte
  headers, no id) and has selection fields (DefaultSelectionBehavior, SelectionParameterCount, ControlParameterCount).
  Its class layout differs between 24 and 27 (27's EBX is 24 bytes longer; type signature differs): 27 EBX can't be
  copied, only rebuilt.
- **The wave's RES** (type 0xB2C465F6, e.g. 78,880 B for the 24 surname wave) is an EA **SBle** bank (u16 at 0x0A =
  number of DSET tables: 5 in both 24 banks, 4 in 27's silence bank, 5 in 27's surname bank) with DSET (`TESD`) tables:
  - `11d5b185`: one row per take (6 for a 6-take silence wave; 2,835 for 24's `pa_player_surname_pxspos`, 3,121 for 27's),
    fields `eae4c46a`, `d914f9f5`: the selection keys. Columns are bit-packed / indexed (u16 pairs), not decoded yet.
  - `a29af127`: per take, 7 fields in 24, **9 in 27** (format change).
  - `3fe2afd5`: per take, 3 fields. `a28d4a0d`: one row, the chunk size. `131a919f`: 24 only. `775b5543`: 27 surname only.
- **SentenceSampleMetadata**: `ExtendedMetadataEntries` = {Data, VariationId u32, EntryCount u32}; 69,134 entries in 24
  (ids 1–110,557 plus 1,078 at 1,000,011+), 26,826 in 27 (ids 1–31,707). Unique per entry; fewer entries than takes.

## Player names

- `nhl_game.db` `playernames` (both games): audioid, surnameid, name, bank (0–8), index, isfirstname, filter, availability.
- Surname ids are the same list in both games: 14,716 identical id + name (2 differ only by accent encoding), 1 dropped.
  27's `playernames` has **1,421 more surname rows** (ids 14,933–16,353, all bank 8: e.g. Michkov 16073, Celebrini
  16075). These are database rows, not proven recordings: 27's `pa_player_surname_pxspos` has only 286 more takes than
  24's, and 24 also carries surname takes in `eng_lcu_01`. Which ids have a take in 27 and none in 24 isn't counted yet.
- Only three surname waves share a name across the games (`pa_player_surname_pxsend`, `pa_player_surname_pxspos`,
  `pm_pxto_player_surname`); nothing in 24's sentence database calls 27's `pm_player_surname_level_1..4`.

## SBle DSET columns (decoded)

Bank header: u32 at 0x18 = offset of a list of (u32 DSET offset, u32) pairs. DSET (`TESD`): u32 id at +8, row count at
+0x38, field count = low u16 at +0x3C, field descriptors from +0x48, 0x18 bytes each: {u32 name hash, u32 type, u32 base,
u32 ?, u32 column offset (from the bank start), u32 ?}. Type byte 1 = stored width (1/2/3/4 bytes), byte 2 = 1 means
"base + row number" (no column). Value = base + stored value; a column offset of 0 means a constant (or base + row).
Floats (type xx000205) are stored as base bits + u16. Checked on the 6-take silence bank (all columns equal across the
6 identical takes in 24 and 27) and the surname banks.

- `a29af127` (per take, in take order): `f5f914d9` = the take's **VariationId** (global; e.g. 23–28 for the silence wave,
  87,277+ for 24's surname takes, 310+ for 27's), `e4660a62` = take number.
- `11d5b185` (per take, sorted by a key that isn't stored): `6ac4e4ea` = take number, `f5f914d9` = VariationId.
- `3fe2afd5`: `e8e591dd` = where the take starts in the chunk (3, 527, 1071… = stream offsets + 3; 16-bit, wraps),
  `6cfcce5b` = a float per take (~0.9; gain?).
- `a28d4a0d`: `dc19107b` = the chunk's byte size.

## WRONG: keyword ≠ playernames.surnameid (found 2026-11-22)

By ear, the 27 take tagged 8384 (= Ovechkin's surnameid) says "Allen", and the length test shows no link between surname
length and take length under this join in either game ("Li" → 1.05 s, "Lilliestrom-Karlsson" → 0.39 s). What holds: in 27,
two takes with the same keyword have matching lengths (r = 0.80, 751 pairs), so a keyword is one spoken word; and 24/27 share
the keyword space for `ca_city_name_cxspos` (offset 0, r = 0.82, 30 pairs). The keyword → game id join is unknown (no Allen
record holds 8384 in any column). Everything below that names players (the table, the 1,105 candidates, Pilot A/A2) rests
on the wrong join. All 8 pilot entries were restored the same day.

City intros ("…we are in Los Angeles"): `bm_intro_*` lines plus `cm_city_name_cxspos` (play-by-play) / `ca_city_name_*`.
27's `cm` and `ca` use keywords 26–103 (same space, r = 0.62); 24's `cm` uses 2373–2419 and others, which align with nothing.

## How a surname picks a take (UNVERIFIED, see above)

surname id → **SentenceKeywords** (`maincommentarykeywords`: KeywordData {Keywords[] of (u32 value, u32 1), VariationId};
38,047 entries in 24, 16,400 in 27) → the VariationId → the wave's bank (`a29af127`: take number; `3fe2afd5`: offset) →
the audio. Checked: keyword values of 24/27 surname takes are `playernames.surnameid` (1423 Buskey, 5474 Kielly, 8389
Owen, 8906 Poddyakon, 4118 Hamilton, 1480 Calder). **SentenceSampleMetadata** only holds the speaker per take
(`speaker` = `MNar` / `CAId`), not the selection.

## Surnames with audio (per wave, by keyword)

| wave | 27 surnames | 24 surnames (incl. eng_lcu_01) | in 27 only |
|---|---|---|---|
| pa_player_surname_pxspos | 2,092 | 4,813 | 947 (9 are new DB names) |
| pa_player_surname_pxsend | 207 | 946 | 85 |
| pm_pxto_player_surname | 736 | 2,829 | 334 |
| pm_player_surname_level_1..4 (27 only) | ~1,897 each | – | 932 new DB names (ids ≥ 14,933) are here |
| pm_pxby_player_surname_level_1 (27 only) | 909 | – | – |

24 calls its own `pm_player_surname_pxspos/pxsend`, `pm_pxby_player_surname`, `pm_pxto_player_l2_surname`… which 27
doesn't have. So 27's new names (Michkov, Celebrini…) are recorded in waves 24 never plays: bringing them over means
putting 27's `level_N` takes into 24's closest wave (an inference to check by ear: level = intensity?).

## How NHL 24 adds takes later (update banks)

`wc/eng_lcu_01` holds 258 waves; `wc/eng_lcu_02..10` are empty slots. An update-bank wave (e.g.
`soundwaves/eng_lcu_01/pa_player_surname_pxspos`, 1,978 takes) has the **same identity GUID** as the base wave (EBX +0x80,
`18d7860a…`) and its own chunk and bank; its takes have keywords in the same `maincommentarykeywords`. So the game merges
a wave's takes across banks. Ported takes can be **added to an `eng_lcu_01` wave** (grow its chunk and bank, add keyword
entries) instead of rewriting the base banks.

## Port plan

1. **Pilot A, no structure change:** for ~5 surnames both games have, replace 24's take with 27's recording of the same
   surname (same keyword), re-encoded to fit the take's bytes (HMS's sound replacement). Proves 27's voice/names play in
   24 and sound right next to 24's lines.
2. **Pilot B, new takes:** add ~10 surnames 24 has no take for (from the "in 27 only" lists) to an `eng_lcu_01` wave:
   append audio to its chunk, add bank rows (SBle writer: needs the column encoding above), add keyword entries
   (grow `maincommentarykeywords`). Proves the add path.
3. **Names, full:** the ~1,360 surnames in shared waves, then 27's new DB names via `level_N` → 24's waves, with rows added
   to 24's `playernames` (same id space) so roster players get their surnameid.
4. **Lines (later):** re-recorded shared lines and 27-only lines need sentence-database entries; 27's SentenceDatabase /
   NewWaveAsset layouts differ from 24's, so they have to be rebuilt per entry.

### Pilot A (applied 2026-11-22)

Wave `pa_player_surname_pxspos`. The bank's take order is the chunk's stream order: `3fe2afd5.e8e591dd` (read as u16 at
stride 2) holds every take's offset + 3 at even rows (1,561/1,561 checked in 27, 1,418 in 24 base, 989 in 24
`eng_lcu_01`). 1,105 surnames have ≤ 6 takes in 24 and a different, not longer 27 take; stars (McDavid, Crosby,
Matthews…) have no take in this wave at all. Applied, one Mods entry each (`apply-sound`, in place, 48 kbps/ch Opus, no file
grows): Byfield (base #385), Hintz (`eng_lcu_01` #698), Kotkaniemi (base #2319), Nylander (`eng_lcu_01` #1924), Ovechkin
(base #1556). Read back from the game: 0.99 correlation with 27's take, 0.05–0.20 with 24's old one. Not yet heard in a
game. WAVs: `export/commentary-pilotA.zip`. Tool: `tools/speechbank --pilot OUTDIR PLAYERNAMES.txt [names]`.
The voice is on the second channel of these 2-channel (uncoupled) streams; the first is silent.

Not heard in game: `pa_player_surname_pxspos` seems to be a rarely called wave; name calls come mostly from the `pm_` waves
(`pm_pxto_player_l2_surname` 6,120 takes, `pm_pxto_player_surname` 6,330, `pm_player_surname_pxspos` 5,587,
`pm_pxby_player_surname` 4,488). 27 has these five names in `pm_` waves only as `pm_pxby_player_surname_level_1` (Byfield,
Kotkaniemi, Hintz, Ovechkin) and `pm_pxto_player_surname` (Byfield, Kotkaniemi); its `pm_player_surname_level_1..4` hold none.
Applied 2026-11-22 (Pilot A2): Hintz "by" (24 `pm_pxby_player_surname` #2425 ← 27 `pm_pxby_player_surname_level_1` #839),
Kotkaniemi "to" (24 `pm_pxto_player_l2_surname` #5798 ← 27 `pm_pxto_player_surname` #557), Byfield "to" (24
`pm_pxto_player_l2_surname` #594 ← 27 #379). Read back: 0.98–1.00 with 27, < 0.08 with 24's old takes.

The bank's offset column, read as u32 per row, holds the stream's start offset (low 16 bits; the decoder's base adds 3).

**Stacked on the Center Ice Mod:** the Center Ice Mod ships its own copies of these `pm_` chunks (13 / 25 more streams than
the bank has rows), appended to `nhl_installpackage_02/cas_03.cas` (0x4001CEF8–0x689DF0BE). The three A2 entries patch
inside that added data. Restore them before restoring the Center Ice Mod; the other way round, its trim stops at the range
that holds them (0x6001CEF8) and ~600 MB of unreferenced data stays at the end of cas_03 (harmless, nothing points at it).

Before any further write: CAS headroom (base `nhl_installpackage_05/cas_05.cas` is 3.69 GB with the Center Ice Mod; 4 GB is the
limit) and stacking on the Center Ice Mod entry (a cimsrt case with an ordinary mod on top, restore in reverse order).

## City calls: labelled by ear (2026-11-22)

`eng_inxp/cm_city_name_cxsend` (play-by-play, city ends the sentence). The user labelled the takes by ear on the City call labels page.
- NHL 27 keyword = city: 27 Dallas, 28 Columbus, 29 Los Angeles, 30 St. Louis, 31 Vegas, 32 Grand Rapids, 33 Syracuse, 34 Texas. Second takes use 525+ (525 Columbus, 527 Dallas, 535 Vegas, 538 Los Angeles, 549 St. Louis).
- NHL 24 tags all 193 takes as keyword 27, but only takes 1-140 are Dallas. 141+ are other cities: 141/156 Columbus, 142/165 Los Angeles, 143/171 St. Louis, 144/164 Vegas, 158 Dallas.
- So the wave holds only a few cities in both games; the real per-team set is likely elsewhere (`cxspos`, or the analyst `ca_` waves, which share keyword numbers across 24 and 27).

Applied: NHL 24 #142 <- NHL 27 #2, NHL 24 #165 <- NHL 27 #26 (mod entries `.../cm_city_name_cxsend#142`, `#165`, in cas_07). Read back: 0.99 similarity to the NHL 27 source.

## Team roll call swapped (2026-11-22, Whisper-paired)

`eng_inxp/cm_city_name_cxspos` holds one take per NHL team in both games, in the same order: NHL 24 #143-169 and NHL 27 #1-28, plus Vancouver at NHL 24 #141-142 and NHL 27 #21. faster-whisper (small model) transcribed all 1,244 takes and got 23 of 23 of the user's ear labels right on the end-of-sentence wave. The user confirmed the LA pair (24 #154 <- 27 #7) by ear first. All 28 were applied as separate entries; the pairs are in export/commentary-teams/pairs.txt and the transcripts are in export/whisper-cities/. Arizona (24 #163) was left alone, since NHL 27 has Utah instead. Several NHL 27 takes are longer than their slot and were fit at 32-40 kbps/ch.

## "to <player>" surnames swapped (2026-11-23, Whisper-paired)

faster-whisper (small, GPU) transcribed all 13,241 takes: NHL 27 `pm_pxto_player_surname` (739) and NHL 24 `pm_pxto_player_surname` plus `pm_pxto_player_l2_surname` (eng_inxp and eng_lcu_01). Each transcript was matched to a roster surname from playernames (24 and 27). 207 surnames matched exactly in both games, which covers 523 NHL 24 takes. Each of those takes now plays an NHL 27 take of the same spoken name, one mod entry per take. The user confirmed the Crosby pair (24 #674 <- 27 #125) by ear first. Lists and logs are in export/commentary-pxto/.
These takes sit inside the Center Ice Mod's rewritten block of cas_03 (as Pilot A did): restore these entries before restoring the Center Ice Mod. Not yet done: near-miss transcripts (fuzzy matches) and the 55 NHL 27-only surnames, which have no NHL 24 slot.

## Play-by-play calls swapped in bulk (2026-11-23)

The user ear-checked a pilot first: 165 takes of `bm_save_generic`, `bm_save_glove_flashy` and `bm_save_pad_oz_slot`, one entry per take. Then 228 more `bm_` waves shared by both games were paired. Waves tied to scores, periods, names, teams, replays or stitching were left out, and so were the analyst waves (`ba_`, `bma_`). Pairing rules:
- the same wave
- a matching pronoun: a he/she slot takes the same pronoun or a neutral take; a neutral slot takes only a neutral take
- the NHL 27 take is 50-110% of the slot's length
- no team, number or period in either transcript

Result: 2,628 pairs. 755 slots had no NHL 27 take short enough and 294 were specific, so those stay NHL 24.
These were applied with the new `apply-sounds --list` batch mode (one mod entry per wave, "WAVE [batch]"): 2,592 takes in 223 waves. 36 takes in 5 waves were skipped because their chunk is shared with other assets (bm_goal_gen among them). None of the batch ranges fall inside the Center Ice Mod's ranges. Files are in export/commentary-pbp/.
