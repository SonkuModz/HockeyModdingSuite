# NHL 26 Texture Viewer

A from-scratch Python reverse-engineering of NHL 26's Frostbite asset
archives (`.cas`/`.toc` files), built because no existing public tool
supported this current-gen, PS5-only title when this was started.
Everything here was worked out from the raw bytes of your own legally
dumped copy of the game, then cross-checked against a working FMT
(Frosty Mod Tool) profile written independently for the same build.

## NHL 27 support

NHL 27's PS5 dump uses the same container: stored (uncompressed,
unencrypted) CAS blocks, the same 16-byte chunk records and `loc`
encoding, the same RES header struct, pixel formats and PS5 tiling. Only
two things changed, and the tool handles both while still reading NHL 26:

- **Data-tree `.toc` files have no 556-byte `00 D1 CE 01` signature
  block.** The `0x3C` superbundle header sits at offset 0. Patch-tree
  tocs still carry the signature block. `toc.py` now detects which one a
  file has.
- **RES header bytes +32/+33 are now two small u8 fields** (e.g.
  `04 06 00 00`). In NHL 26 the whole u32 was always ≤16. The old
  check threw away about 99% of real NHL 27 headers.

The cache version was bumped, so old NHL 26 cache entries are never
reused for NHL 27 files. Many cas files in both games are exactly 1 GiB,
so a check on file size alone could not tell them apart.

### Texture names

Names used to be a proximity guess: "the nearest path-looking string
before this RES header". That is wrong often enough to matter, because
the bytes around a header are usually the *bundle's* other asset names
(meshes, prefabs, shaders, effect states) -- the tree filled up with
textures filed under `content/common/components/loaders/...` and named
things like `..._mesh`, `anim_time_rate_es` or
`player_root_standard_shader`.

They are now resolved exactly. Every bundle in a `.cas` is a small table
(see `build_bundle_name_index`): a 36-byte header, the SHA1 table, then
EBX records, RES records, RES types/metas/rids and chunk records, then a
string table of their real names. A texture's RES header stores, at
+120, the 32-bit FNV/djb2 hash of its own RES asset name, so the name is
looked up rather than guessed. Proximity is only a fallback when a
header's hash isn't in the file (e.g. a patched header whose bundle is
elsewhere), and such a fallback is rejected outright if the nearest name
is plainly another kind of asset (`_mesh`, `_prefab`, `_shader`, `_es`,
...). `check_names.py` reports how many names a given `.cas` resolves
exactly.

The cache version was bumped again (10) because names are part of the
cached result, so the first run after this change re-reads every `.cas`.

## Running it

Double-click `RunTextureViewer.bat`. It finds a Python 3 install (from
PATH, or a Python launcher), installs the two required packages
(`numpy`, `Pillow`), and launches the viewer. No decryption key or
external decompressor is needed -- NHL 26's PS5 dump stores every
archive block uncompressed ("stored"), which this tool relies on
directly.

If you don't have Python installed, get it from python.org (check
"Add python.exe to PATH" during setup) -- any recent Python 3 works.

## Using it

**Point it at your game.** Set "Game root" to the folder containing
`Data\Ps5\...` and `Patch\Ps5\...`. If the saved default isn't there any
more (dumps get moved), the viewer looks for an installed `*-app0` dump
under the usual places (`D:\Archives\PS5`, `D:\Games`, ...) and uses that
instead.

**Fastest way to browse everything**: click "Scan ENTIRE game (all
.cas)". First run re-reads the whole ~200GB+ archive across every install
package, Data and Patch layers both -- on a 24-core machine that is about
10-20 minutes; results are cached to disk afterwards and every future
launch is instant, even on a different machine if you copy the `cache/`
folder over (see "Sharing this" below). The "Workers" spinbox controls how
many CPU cores it uses in parallel; the default is tuned for typical
hardware, raise it if you have a lot of cores to spare.

**Rebuilding the cache without the window**: `python rebuild_cache.py`
does the same full scan from a console (`python rebuild_cache.py "<game
root>" 24` to set the root and worker count) and prints progress plus an
ETA. It writes exactly the cache the viewer reads, so use it if you'd
rather not sit through the first launch, or after any change that bumps
`CACHE_VERSION`.

**Checking names in one file**: `python check_names.py "<path to
cas_09.cas>"` prints how many texture headers were named exactly from the
bundle tables versus how many fell back to the proximity guess, with a few
example names. It takes a few seconds and writes nothing.

**Faster way to check one file**: pick a superbundle + `.cas` file and
click "Scan for textures" instead, if you just want to look at one
archive without waiting on the full game.

**Browsing results**: textures are organized as a real folder tree
matching their actual content path (`content > character > skater >
league > nhl > toronto > adidas > five > ...`), lazily expanded so it
stays fast no matter how many textures are loaded. Select a row for a
live decoded preview; select a folder and "Export selected" to export
everything nested under it in one go (e.g. select a team's folder to
get every texture for that team). Each folder level is ordered
home/away/alternates first, then jersey-set folders in numeric order
(`one`, `two`, ... `thirteen`, ... -- the game names them as words, not
digits), then the rest A-Z; leaves within a folder sort naturally by
filename (so `..._2` precedes `..._10` and a set's maps come out
grouped by kind). Only names that resolve to a real rooted path
(`content/...`, `ui/...`, `worlds/...`, a known handful)
get a place in that tree; a texture whose only recovered name is a
generic shared class label (`skater_branding_advanced_normal` and the
like -- no per-asset identity) is dropped into a single **`~ unsorted`**
folder at the bottom rather than spawning a junk top-level folder from
whatever stray bytes preceded the label.

**Searching**: type in the Search box -- it debounces briefly, filters
in memory, and only builds the (small) matching subset in the tree, so
it stays fast regardless of how many textures are loaded. Very broad
queries are capped at 2000 shown results with a status message telling
you the true count; narrow the search to see more.

**Exporting**: writes a `.dds` per texture (opens in any texture-editing
tool, Photoshop with a DDS plugin, GIMP, etc.), laid out as real nested
folders mirroring the texture's own content path. Exporting a folder
mirrors **the game's own tree exactly** -- selecting `... > nhl >
toronto` writes
`content\character\skater\league\nhl\toronto\adidas\five\...dds`, the same
path you see in the tree, so the export can be diffed against, or fed
back into, tools that expect full asset paths. Right-click a folder for
`Export folder rooted at this folder` if you'd rather get
`toronto\adidas\five\...` without the leading segments. Two textures that
end up with the same name don't overwrite each other -- duplicates get
`_2`, `_3`, etc. on the filename.

**Right-click** a row or a folder for: Export selected, Export folder
rooted at this folder (folders only), and Copy path.

## Sharing this

Give someone:
- This whole `TextureViewingScript` folder (code + launcher).
- Optionally, the `cache/` folder, if you've already run a full scan --
  it's keyed by path *relative to* the game root and validated by file
  size, so it works for them even though their game is installed
  somewhere else on their own machine. This is the difference between
  their first launch being instant vs. a 25-30 minute scan.

Don't give them:
- The game's own `Data\`/`Patch\` folders -- that's copyrighted game
  data, not something this tool produces or that should be
  redistributed. Everyone needs their own legally-dumped copy.
- The `FMT_extracted` folder if present on your machine -- that's a
  separate third-party tool (Frosty Mod Tool) used during development
  for format cross-referencing, not part of this project.

They'll need their own Python 3 install (see "Running it" above) --
`RunTextureViewer.bat` finds it automatically on a normal install.

## What's solid

- **Chunk table resolution**: every scanned texture resolves to its
  correct archive location, across both install packages and the
  Data/Patch layers. The Patch-layer `.toc` files carry chunk records
  under *two* magic values: `0x0001F895` records hold patched bytes and
  read from the Patch tree; `0x0000F895` records are back-pointers to
  untouched base content and read from the **Data** tree (their offset
  only lands inside the larger Data `.cas`). Routing by the record's own
  magic -- not just which `.toc` listed it -- is what fixed a class of
  "resolved but preview/export produces nothing" textures (verified
  25/25 on a random Patch-toc sample).
- **Decompression**: NHL 26's PS5 dump uses no compression at all
  (every block is "stored"); the reader validates each block's magic
  and just copies the bytes.
- **Pixel format table**: `0x36/0x37`=BC1, `0x3C/0x3D`=BC3,
  `0x3F`=BC5, `0x42/0x43`=BC7.
- **PS5 tiling**: AMD addrlib SW_4K_S de-swizzle, verified
  **pixel-perfect** for both the 16-bytes/block case (BC3/BC5/BC7) and
  the 8-bytes/block case (BC1) against real, named, ground-truth
  textures -- fully clean images, no residual artifacts.
- **Speed**: the scanner is numpy-vectorized, and the block decoders
  (BC1/BC3/BC5) and de-swizzle are fully array-based rather than
  per-pixel Python loops -- a full-resolution preview decode that used
  to take 8+ seconds now takes a fraction of a second.
- **BC7 preview**: a full 8-mode BC7 decoder (`frostbite/bc7_decode.py`)
  -- this is the one format where implementing it meant transcribing
  the public Direct3D/Khronos spec's own reference tables (partition
  sets, mode layout) rather than reverse-engineering anything
  NHL26-specific. Per-block Python rather than vectorized (BC7's
  mode/partition logic branches per block), so a 1024x1024 preview
  takes a few seconds rather than a fraction of one -- fine for an
  on-demand click, not for bulk scanning.
- **Names**: resolved exactly from each bundle's own RES asset table --
  the texture header's asset-name hash (at +120) is matched against the
  bundle's RES names, so a texture gets its real path instead of
  whichever mesh/prefab/shader string happens to sit nearby. Proximity
  is only the fallback when that hash isn't in the file, and it now
  refuses names that are plainly a different kind of asset.
  `check_names.py` shows the exact/resolved ratio for any `.cas`.

## What's still open

- **Only the largest resident mip level is exported**, not the full
  mip chain.
- A small number of textures (mostly "branding"/decal variants, and
  `*_array` maps like `..._coeff_array`) have a chunk id that genuinely
  isn't present in any indexed `.toc`. Most of these are now recovered
  anyway: the chunk turns out to be stored **inline right after the RES
  header** (its sibling maps in the same bundle have real `.toc` records
  at exactly `header + 164`), so an unresolved header is retried by
  scanning forward from itself for the first valid stored-block chain
  long enough to hold its top mip. Recovered rows show status "inferred"
  and a `~` on their location. What's left after that is marked
  **"no pixel data"** -- verified for several (e.g.
  `branding_jersey_adidas_pittsburgh_thirteen_1_coeff`): the GUID is in
  no `.toc`, no bundle manifest (checked every manifest around the
  header), and there's no inline chunk, so this build shipped the RES
  header with no locatable pixels anywhere the format exposes. When such
  a dead header has a resolvable same-name twin, the dead one is dropped
  rather than shown as a confusing un-previewable duplicate.
- A rare class of large single-mip BC7 textures may use a different
  tiling variant (pipe/bank-XOR) that resists the standard SW_4K_S
  pattern -- if an exported texture looks scrambled rather than just
  wrong-colored, this is likely why.
- A header whose asset-name hash isn't in the same `.cas` (typically a
  Patch-layer header whose original bundle lives in the Data layer)
  still falls back to proximity. Those rows are now named less often but
  are much less likely to be named *wrongly*: a nearby mesh/prefab/shader
  name is rejected rather than published. `check_names.py` reports the
  counts per file if a particular archive looks under-named.

## How this got cracked

The short version, if you want the full story: early attempts
misread the per-chunk record as 12 bytes (borrowed from an older
Frostbite generation's reference tooling) when it's actually 16, which
silently misaligned every record after the first and made resolution
succeed only by coincidence. A compiled FMT profile for this exact
game build (provided by another researcher) gave the real record
layout, the real "every block is stored, no compression" behavior, and
the real PS5 tile-swizzle algorithm, which is what took this from
"a handful of lucky hits" to 100% resolution and pixel-perfect images.
From there, a long series of real-world bug hunts (chunk-id dedup
silently dropping shared-texture names, one-directional name search
missing isolated references, a RES header's own self-embedded chunk id
winning proximity over the real answer, and a second chunk-record magic
value used only by Patch-layer superbundles) closed the remaining gaps
between "looks like it works" and "actually resolves and names
everything correctly."
