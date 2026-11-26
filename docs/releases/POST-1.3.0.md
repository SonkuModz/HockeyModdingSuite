**HMS 1.3.0**: NHL 24 can now be **edited**, not just viewed, and it can **play with NHL 27's gameplay tuning**.

## What's new

### 🎯 Match NHL 27 tuning (NHL 24)
- New **Match NHL 27** button on the Tuning page when NHL 24 is open.
- It sets NHL 24's gameplay values (AI, physics, animation) to NHL 27's wherever both games have the same value: **438 of 476** values that differ.
- Before writing, HMS reads the changed eboot back: every changed value must read as NHL 27's and every other value as it was. If anything doesn't match, nothing is written.
- HMS finds your NHL 27 folder by itself (or asks for it). Undo it with **Restore** on the Mods page.

### ✏️ NHL 24 editing
- NHL 24 doesn't check its signed index files (tested in game), so replacing and saving now work for NHL 24.
- 🖼️ **Textures**: replace any texture (confirmed in game).
- 🔊 **Sounds**, 🎬 **Videos**, **Database**, **Roster** and **Teams** edits save into the game.
- 🕒 **Scorebug**: edit NHL 24's ESPN+ score clock (colours, text, sizes) with a live preview, and save it into the game.
- Restoring an older change no longer gets stuck behind a newer one.

### 📈 Tuning reads NHL 24
- All **22,807** gameplay values in NHL 24's eboot, searchable, with defaults and ranges, and CSV export.

### 👕 Uniforms and Unused jerseys on NHL 24
- Kits draw on NHL 24's own jersey, pants and socks models.
- **Unused** lists the jerseys no team's menu shows (467 on NHL 24, 153 can be added).

### 🎮 Play
- Play for NHL 24 starts `nhl24.exe` straight away.

## Heads up
⚠️ Only texture replacement is confirmed in game so far on NHL 24. The other NHL 24 writes save and read back correctly but aren't all tested in game. **Back up your NHL 24 folder.**
⚠️ Match NHL 27 uses NHL 27's values on NHL 24's engine, so some things may feel different. Play offline when testing: the online tuner can override values.
⚠️ Not on NHL 24 yet: Arenas, Be A Pro and Jersey Studio. NHL 24 kits show without a helmet, gloves or the idle pose.
⚠️ The NHL 27 notes from 1.1.0 and 1.2.0 still apply (NHL 27 Scorebug saving is off).

## Setup
1. Unzip anywhere and run `HMS.exe`. You don't need to install .NET.
2. Copy `oo2core_9_win64.dll` from your **FMT** folder next to the exe. It's required and can't be shipped with HMS. Copy `opus.dll` too for commentary audio.
3. Pick your game on the start screen.
4. Close the emulator before applying mods.

*Made by Sonku. A fan project, not affiliated with EA Sports or the NHL. Centre-ice graphics are from TheFaceoff.net: personal use only, all credit to them.*
