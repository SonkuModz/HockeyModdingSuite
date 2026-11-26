# NHL 19 roster save: edited save hangs on "CHECKING"

> **Solved (2026-11-15).** The roster record must end with a u32 of the unpacked size (TDB + 18-byte trailer), after the LZ4
> frame. Saves 2 and 3 below were built without it. That alone made the edited save hang on CHECKING, and an unedited repack
> crash after the title screen. With the u32 added and CRCValue recomputed, the edited save loads, and the St. Louis clock
> shows the edited colour. The score clock reads `overlayprimarycolor`. The rest of this file is kept as it was written.

The game is NHL 19 (CUSA11126) on shadPS4 0.21.1 (Overtime Roster boost build), with the Puck Peak / Overtime roster. Profile0 has `ActiveRosterType` Custom, so the game boots `RosterCustom0`.

- **Symptom:** after a small edit to the roster's team table, the game loads the roster and then sits on "CHECKING" forever.
- **Control:** the original, unedited save gets past CHECKING to the homepage.
- **To test:** copy a `RosterCustom0` folder into `shadPS4/home/1000/savedata/CUSA11126/` and press a button at the title screen. Saves only load after that press.

## What's in here

| Path | What it is | Result in game |
|---|---|---|
| `saves/1-original/RosterCustom0` | The untouched Overtime roster save | Passes CHECKING |
| `saves/2-edited-stale-crc/RosterCustom0` | The edits below, with the original `CRCValue` kept in the JSON record | Hangs on CHECKING |
| `saves/3-edited-new-crc/RosterCustom0` | The same edits, with `CRCValue` recomputed | Still hangs on CHECKING |
| `decoded/original.tdb` | The TDB decompressed from save 1, trailer trimmed | — |
| `decoded/edited.tdb` | The edited TDB packed into saves 2 and 3 | — |
| `scripts/rostercrc.py` | Computes `CRCValue` from a TDB; gives exactly the stored value for both real saves (RosterCustom0 and RosterOnline0) | — |
| `scripts/repack_save.py` | Packs a TDB back into a save folder (LZ4 frame, trailer, INDEX) | — |
| `scripts/nhl_game-meta.xml` | Table metadata the checksum script needs (savegroups and table order) | — |

## The edits (St. Louis Blues row, table `exhibitionteams`)

The six colour fields were set to loud test colours, so it's obvious which one the game shows. Bytes are stored little-endian.

| Offset in TDB | Field | Before | After |
|---|---|---|---|
| 0x9231a | feprimarycolor | 00529B | FFFF00 |
| 0x9238b | pressecondarycolor | 152546 | 00FFFF |
| 0x923a7 | overlaysecondarycolor | D6B033 | FF00FF |
| 0x923c9 (only 0x923ca changed) | presprimarycolor | 00FFB2 | 00FF00 |
| 0x923e1 | overlayprimarycolor | 0C284D | 00FF00 |
| 0x9243f | fesecondarycolor | FDBA30 | FF8000 |
| 0xcb390 | 4 bytes Blueline's TDB writer recomputed after the edit (believed to be the table's own CRC; not confirmed) | 97f6d40d | 88bd0e59 |

No other bytes differ between `original.tdb` and `edited.tdb`.

## Save container (as far as we've decoded it)

- **DATA** is a series of records, each `[u64 length][8-byte tag][payload]`:
  1. `{"IsBackup":false}`
  2. A 0x48-byte version block containing "G@1.60"
  3. JSON with FileCreateDate, FileDisplayName and CRCValue
  4. `0x01`, then an LZ4 frame: 64 KB independent blocks, content checksum on, no content size
- **The LZ4 frame decompresses to** the TDB (its size is the u32 at +8) followed by an 18-byte trailer: `d92eb758 00000000`, then a u64 TDB size, then `0100`.
- **INDEX:**
  - +0x28 = 16 + the length of the last record
  - +0x30 = that record's offset
  - +0x38 = the DATA file size

## CRCValue (cracked; matches both real saves)

- **Format:** 54 characters, nine u32 values, each as 6 base-62 digits (`0-9A-Za-z`, most significant first).
- **CRC:** CRC-32/MPEG-2 (poly 04C11DB7, init FFFFFFFF, not reflected, no final xor).
- **Which tables count:** a table counts only if the u32 AllocationType at its slot+4 has bit 0x2 and it has at least one record.
- **Per-table contribution:** the CRC of each live record (skip a record whose last byte has 0x80 set), sorted ascending, each fed into the running CRC as a little-endian u32.
- **Value 0:** all tables whose savegroups include `rosterupdate`, in meta file order.
- **Values 1–8:** tables VoEQ, mHuy, ubPc, ZBac, eGlu, dSvy, qiwI and xjgw, each on its own. A table that contributes nothing gives FFFFFFFF.

## Open question

Save 3 has a `CRCValue` that the same algorithm verifies on the real saves, and the TDB checksum Blueline knows about is updated. It still hangs. So something else is being checked. Candidates:

- A checksum somewhere else in the save or in Profile0
- A server-side or Overtime-side check
- Something the re-compression changes (our LZ4 output isn't byte-identical to the original, although it decompresses to the same data)

A useful control would be save 1's TDB repacked with no edits at all. That run was inconclusive, because nobody pressed a button at the title screen.
