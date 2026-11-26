---
name: nhl27kit-project
description: "HMS (Hockey Modding Suite, formerly NHL27Kit) is the user's NHL 27 PS5 + NHL 24 PS4 modding tool; NHL24Kit in C:\\dev is reference only"
metadata:
  node_type: memory
  type: project
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-11-11T23:49:01.083Z
---

The user's modding tool lives in `C:\Users\sonku\Downloads\NHL 27 Modding\NHL27Kit` (GitHub SonkuModz/HockeyModdingSuite,
private). Started 2026-10-26 for NHL 27 (PS5, PPSA34063, KytyPS5). On 2026-11-03 it gained NHL 24 (PS4, CUSA37933,
nhl24pc; dump `NHL 24 Dump`) and was rebranded **HMS — Hockey Modding Suite**, navy and gold, logos in
`refs\Hockey Modding Suite Branding`. The exe is now `HMS.exe`; code, folders, settings (%APPDATA%\NHL27Kit) and
cache keep NHL27Kit names on purpose.

**Why:** the user wants one suite for both games, rewritten from scratch; NHL24Kit (FMT-based) was slow.
**How to apply:** use NHL24Kit only to learn what's where, never copy its approach. NHL 24 writes are unlocked: the
TOC signature test passed 2026-11-03 (NHL 24 doesn't check TOC signatures), and every HMS write works on NHL 24
(PLAN.md §28). Overview of the NHL 24 files: docs/nhl24/nhl24-deep-dive.md. "Kit running" checks must cover HMS.exe
as well as NHL27Kit.exe.
