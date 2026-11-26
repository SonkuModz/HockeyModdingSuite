**HMS 1.5.0** adds a Sportsnet score clock for NHL 24, NHL24 PC 0.9.3b support, Center Ice Mod packages, Faceoff builds on the Play page, and about half the idle memory.

## What's new

### 📺 Sportsnet score clock (NHL 24)
- On the **Scorebug** page, **Install Sportsnet clock** puts a Sportsnet-style score clock in the game. Restore on the Mods page brings your old clock back.
- **Main bar:** SN box, team colour boxes with the score, a shots row, and a clock box with the period. **FINAL** fills the clock box at the end.
- **Penalties:**
  - A **power play** or **empty net** tab hangs under the team on it.
  - The team's **logo tile** slides in next to the clock.
  - **4-on-4** gets its own box.
- **Info panels** slide out in the team's colour.
- **Shootout** markers show ✔ for a goal, ✖ for a miss and an empty ring for a shot not taken, with **SHOOTOUT - ROUND n** underneath.
- **Goal animation**, in order:
  1. A blue wipe with light streaks.
  2. The team colour band with the embossed team name and the logo zooming past.
  3. The spaced-out team name.
  4. **GOAL** built letter by letter, with **POWER PLAY** above it on a power-play goal.
- Its colours, sizes, labels and timings can be edited on the same page, like the ESPN+ clock's.

### 🎮 NHL24 PC 0.9.3b
- NHL24 PC 0.9.3b puts the base game in `game` and the 1.60 update in `game-UPDATE`. Pick either folder and HMS opens them together as the updated game.

### 🧊 Center Ice Mod packages (NHL 24)
- **Mods > Import pack…** now takes `.cimspkg` files.
- HMS checks the package against your game first. This takes a few minutes and writes nothing.
- It then shows what the package changes and the disk space it needs, and installs it as mod-list entries you can Restore.
- Keep the `.cimspkg` file where it is: its data is read from it when the mod is applied.

### ▶️ Play
- The Builds list now includes **Iksperial's Faceoff builds** (NHL 27). The newest emulator by date is the default.
- A Faceoff build uses only its own config, and your Play page edits are saved with that build.

### ⚡ Lighter
- Idle memory is roughly halved, from about 2.8 GB to about 1.4 GB on NHL 27.

### 👕 Jersey Studio
- **Shop shots:** flat front and back pictures of your design.
- Writing a design now also writes the jersey's fabric maps, so old stripes and logos don't show through.

## Heads up
⚠️ **Confirmed in game:** the Sportsnet clock's bar, power play tab and logo tile, and its goal animation.
⚠️ **Not seen in game yet:** the Sportsnet clock's 4-on-4 box, info panel, FINAL and shootout markers. They have only been checked in a preview.
⚠️ **Sportsnet shots row:** it shows the last shot totals NHL 24 sent, so it's blank until the game first sends them.
⚠️ **Scorebug page:** reading the score clock needs **FFDec** (`ffdec-cli.exe`) in a `ThirdParty\ffdec` folder next to HMS.exe. HMS can't ship it.
⚠️ **Not checked in game yet:** Jersey Studio's shop shots and the new fabric maps.
⚠️ The notes from earlier versions still apply. NHL 27 Scorebug saving is still off. **Back up your game folder.**

## Setup
1. Unzip anywhere and run `HMS.exe`. You don't need to install .NET.
2. Copy `oo2core_9_win64.dll` from your **FMT** folder next to the exe. It's required and can't be shipped with HMS. Copy `opus.dll` too for commentary audio.
3. Pick your game on the start screen.
4. Close the emulator before applying mods.
