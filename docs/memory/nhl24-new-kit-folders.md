---
name: nhl24-new-kit-folders
description: What a new NHL 24 kit folder needs to load in game (kit-alts); the odd res id rule that caused 0xc0000005 crashes
metadata:
  node_type: memory
  type: project
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-11-24T07:55:04.972Z
---

New NHL 24 kit folders (kit-alts, KitClone) load in game as of 2026-11-24 (Boston test kits 29 = NHL 27 "fifteen" converted, 39 = plain copy).

What it took, all three needed:
- Every bundle and asset registered in `player_team_brt` (BundleRefTable + ResGrower, both stored copies); without it the game draws the adidas fallback kit.
- The root bundle `jersey_..._player_team_brt` listed in the Patch TOC with its record and own ebx/res stored in a patch CAS (5/2p), chunks in base; no stock patch bundle keeps its record in a base CAS.
- **Res ids must be odd.** The game tells an unresolved res id from a loaded pointer by bit 0; an even id is used as a pointer and crashes (0xc0000005 at eboot+0x58029C1, Rax = the res id). All ~207,800 stock ids are odd. Fixed in KitClone and AddedTextures with `| 1`.

- **Fabric (weave) lives in each model bundle.** jersey/pant/sock *_hq_asp bundles carry the shared weave (textile/weave/adidas) themselves; a separate weave folder is never loaded. NHL 27 kits use textile/weave/fanatics: added as weave/fanatc (NHL 27 normal arrays, NHL 24 coeff arrays; NHL 27 coeffs = chrome) and listed in the kit bundles in place of adidas (KitCopy.SharedRenames). Confirmed in game 2026-11-24 (Bruins Stadium Series).

- **Kit menu names** (Vintage_2_<art>_<variant> in loc/eng_us.db, taking over an unused vintage row since eng_us is at the 65,520-row cap) show in game ("2025-26 Stadium", 2026-11-24). A legacy file that grows must stay where it is (LegacyEditor.PlanPs4 grows into the free space after it); moving eng_us.db to its chunk's end crashed NHL 24 at start-up. Chunk size isn't the limit (stock chunks reach 30.6 MB).
- **Weave layer order:** NHL 24 kits pick weave layers by NHL 24's order; every NHL 27 Fanatics weave array = NHL 24's adidas array minus layer 1 (big holes). Copy 1:1 → kit bodies showed NHL 27's dimple mesh (user: "stupid dimples"). The user dislikes the small dimple mesh too (NHL 24 layer 2 / NHL 27 layer 1), so KitConvert27.SourceLayer now fills layers 1-2 with plain knit (normal: NHL 27 layer 0, k≥3 ← k-1; coeff: NHL 24's own adidas coeff, layers 1-2 ← its layer 0).
- **Tie weaves' blotchy stitch layer** (tie layer 6, metal_tie 7, after a flat layer) draws on logos of kits whose donor preset uses a tie weave (Edmonton etc.); NHL 27's Fanatics kits use the default weave without it. Copy fills it with the flat layer (KitConvert27 _twill). User reported it as the logo using "the NHL shine map" (2026-11-24); fix confirmed in game.
- **Batch applied and confirmed in game 2026-11-24** (entry "NHL 27 alternates", 44 kits, no dimples; PLAN §31): `kit-alts-find --specs FILE` picks NHL 27's label-2 alternates + 2024-25/2025-26 kits and a donor laid out like NHL 27 (base_color, enough layers); unnamed alternates get "2026-27 Alternate"; eng_us at 65,530 rows (--loc-rows 65530). Left out: Chicago 2024-25 Classic (3 pant logos, NHL 24 room for 2).
- "Insufficient system resources" from shadPS4 = too little free commit memory (needs ~13 GB free); ask the user to close apps.

**Why:** these took many crash/fallback rounds to find; the crash dump (user/log/crash_*.dmp) Rax value is how the res id was found.
**How to apply:** any tool that makes new res ids or new kit folders must keep these. Note the asset index cache can show stale ResRid after a write; read records fresh to check. Next: the batch of NHL 27 alternates, see [[nhl27kit-project]].
