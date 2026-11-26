**NHL27Kit 1.1.0** brings six new pages, a 3D player that moves, and a new hockey look.

## What's new

### New pages
- 🎬 **Videos**: every video in the game (menus, arena intros, transitions, training). Play them, save them as WebM or MP4, or replace one with your own clip.
- 🏟️ **Arenas**: every arena in 3D as the game builds it, with the ice, boards, glass, ads, banners and jumbotron. Fly around or jump to the broadcast, end, bowl and overhead views.
- 🎛️ **Tuning**: the gameplay values built into the game (AI, skating, shooting, checking, goalies and more), searchable, with defaults and ranges. Export them as CSV.
- ⭐ **Be A Pro**: open your Be A Pro saves and edit your player's ratings and X-Factors. Every save is backed up before it's changed.
- 👕 **Unused jerseys**: the game has hundreds of jerseys that no team's menu shows, such as old alternates, All-Star sets and AHL/ECHL thirds. Browse them all, see each one on the player, and add any of them to a team's menu in one click.
- 🎨 **Jersey Studio** is now its own page.

### Jersey Studio
- Design **your own** jersey from the create-a-team templates, or start from a team's.
- Colour the **collar**: the front, back and the ring around the neck.
- Add **stripes** and your own **logos** (PNG or DDS).
- **Add to a team's menu**: write your design into a free jersey folder and add it to any team's jersey list.
- **From a photo…**: turn a picture of a real jersey into a colour map. Mark the crest and stripes, and the kit lays them out on the template.
- A `.dds` **template** that shows which part of the image is which part of the jersey.

### Uniforms and the 3D view
- The player stands in the game's **idle animation**, holding a stick.
- Jerseys show their **fabric and seams** (normal maps), the way they look in game.
- Fixed pants textures that were drawn backwards.

### Look and feel
- A new **hockey theme**: rink markings, a scoreboard status bar, a puck slider, goal-red buttons.
- A **startup screen** while the game is read.
- Pages and Studio tabs **glide in** when you switch.

## Heads up
⚠️ **Not all checked in game yet:** Jersey Studio's *Add to a team's menu* and *From a photo*, the Unused page's *Add to menu*, and Be A Pro editing. **Back up your game folder and your saves first**, and tell me how they go.
⚠️ Scorebug saving is still switched off: a changed score clock crashed the game when a match loaded. The page is preview only.

## Setup
1. Unzip anywhere and run `NHL27Kit.exe`. You don't need to install .NET.
2. Copy `oo2core_9_win64.dll` from your **FMT** folder next to the exe. It's required and can't be shipped with the kit. Copy `opus.dll` too if you want commentary audio.
3. In Settings, pick your NHL 27 dump folder (the one with `eboot.bin`, `Data` and `Patch`).
4. Close the emulator before applying mods.

Updating from 1.0.0? Just replace the exe: your settings and mods stay where they are.

*Made by Sonku. A fan project, not affiliated with EA Sports or the NHL. Centre-ice graphics are from TheFaceoff.net: personal use only, all credit to them.*
