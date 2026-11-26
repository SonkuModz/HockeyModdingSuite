**HMS 1.2.0**: NHL27Kit is now **HMS, the Hockey Modding Suite**, and it reads **NHL 24** too.

## What's new

### NHL 24 (PS4) support
- Point HMS at an NHL 24 dump (CUSA37933) and it reads all of it: 129,715 bundles and 482,971 assets, indexed in about 9 seconds.
- 🖼️ **Textures**: PS4 textures decode, including jersey logo arrays and full-size jerseys.
- 🔊 **Sounds**: commentary, music and effects play and export as WAV.
- 🎬 **Videos**: play them and save as WebM or MP4.
- 👥 **Roster**: 11,675 players with portraits.
- 🗄️ **Database**: all 119 tables of nhl_game.db.
- 🏒 **Teams**: all 527 teams with their logos.
- Each game keeps its own index and mod list.

### Play NHL 24
- 🎮 **Play** for NHL 24 opens the **NHL24 PC** launcher, and HMS edits its emulator settings (`user\config.json`): display mode, resolution, frame rate, FSR and sharpening, PS4 Pro mode, red zone protection, controls and more. Only the settings shown change; the first save keeps a backup.
- Shows your play time, the emulator's log, and which game folder NHL24 PC runs, with a warning when it's missing or isn't the one HMS has open.
- **Point it at HMS's NHL 24 folder** links NHL24 PC's `game` folder to your NHL 24 dump, so the game plays what HMS changes. Only the link changes; a real folder is never touched.

### Pick your game
- A new start screen shows a card for each game HMS finds, with its cover. Tick **Don't ask again** to skip it.
- Switch games any time from the game name in the title bar, or in Settings.
- HMS also looks next to the games it already knows, so NHL 24 shows up even when `HMS.exe` lives in another folder.

### New look
- The **HMS** name, icon and badge, in navy and gold.

## Heads up
⚠️ **NHL 24 is view and export only for now.** Saving and replacing are switched off for NHL 24 until its signed index files are tested in game.
⚠️ The 1.1.0 notes still apply: Scorebug saving is off, and Jersey Studio's *Add to a team's menu* and *From a photo*, Unused *Add to menu* and Be A Pro editing aren't all checked in game. **Back up your game folders and saves.**

## Setup
1. Unzip anywhere and run `HMS.exe`. You don't need to install .NET.
2. Copy `oo2core_9_win64.dll` from your **FMT** folder next to the exe. It's required and can't be shipped with HMS. Copy `opus.dll` too for commentary audio.
3. Pick your game on the start screen.
4. Close the emulator before applying mods.

Updating from NHL27Kit? Just use `HMS.exe` instead: your settings, cache and mods carry over.

*Made by Sonku. A fan project, not affiliated with EA Sports or the NHL. Centre-ice graphics are from TheFaceoff.net: personal use only, all credit to them.*
