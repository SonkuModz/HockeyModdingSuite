**HMS 1.4.0**: centre ice from a rink picture you download yourself, cleaner TheFaceoff.net logos, and AHL sync and more player editing in the Roster.

## What's new

### 🧊 Centre ice tab (Teams)
- The team editor now has **Team** and **Centre ice** tabs.
- **Open TheFaceoff.net**, go to the rink's page and click the whole-rink picture. It opens on Google Drive: press **Download** there.
- **Choose picture…** or drop the file on the card. The centre logo and centre line are made from it, with a preview and sliders, ready to write.
- The tab doesn't contact the site, so it keeps working when TheFaceoff.net refuses HMS's own requests (the 403 error).
- A picture that's too small or isn't a whole rink is turned away with the reason. A small copy gets a warning that the logo will be soft.

### 🎨 Cleaner centre-ice logos
- TheFaceoff.net draws its logos under frosted ice, which washes them out. A new **Remove tint** slider takes that off and matches the colours of the game's own logos. It's on by default.

### 🏒 Team logos and lines fixed
- Vegas, Seattle, Utah and teams outside the NHL showed the wrong logos in Teams, because HMS numbered logos by team number. It now uses the numbers the game uses.
- Centre ice now writes to the right logo and line. Teams that share a logo or line are named, and a line other clubs use is only written if you ask for it.

### 📋 Roster (thanks to nkerling10)
- **AHL sync** for all 32 teams, pulled from theahl.com. A player on both an NHL and an AHL roster goes with the AHL one ([#3](https://github.com/SonkuModz/HockeyModdingSuite/pull/3)).
- Free agents in the base roster now get assigned to their real teams during NHL sync ([#2](https://github.com/SonkuModz/HockeyModdingSuite/pull/2)).
- Edit positions, draft info and contracts on the player screen ([#5](https://github.com/SonkuModz/HockeyModdingSuite/pull/5)).
- A team reaching 40 players no longer stops the whole sync: those players stay where they are and the summary names them.

### 🛠️ NHL 24 fixes
- Fixes for errors reported in [#4](https://github.com/SonkuModz/HockeyModdingSuite/issues/4): adding or moving a jersey slot, replacing streamed textures, and re-applying or restoring mods.
- NHL 27: replacing a shared texture asks whether to change all its users.

## Heads up
⚠️ Not checked in game yet: the new tint removal and the new logo and line numbering. The Centre ice tab's **Write**, Choose picture and drag-and-drop haven't been run either; its conversion has been tested. **Back up your game folder.**
⚠️ Centre logos written with an older version keep their old washed-out look until you write them again.
⚠️ TheFaceoff.net sometimes refuses HMS's requests, so the **Centre ice from TheFaceoff.net…** window may not load. Use the new tab when that happens.
⚠️ The notes from earlier versions still apply (NHL 27 Scorebug saving is off, and not every NHL 24 write is confirmed in game).

## Setup
1. Unzip anywhere and run `HMS.exe`. You don't need to install .NET.
2. Copy `oo2core_9_win64.dll` from your **FMT** folder next to the exe. It's required and can't be shipped with HMS. Copy `opus.dll` too for commentary audio.
3. Pick your game on the start screen.
4. Close the emulator before applying mods.

*Made by Sonku. A fan project, not affiliated with EA Sports or the NHL. Centre-ice graphics are from TheFaceoff.net: personal use only, all credit to them.*
