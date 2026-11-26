---
name: nhl27kit-autonomous-run
description: "User checks NHL27Kit progress remotely, wants screenshots; plan in NHL27Kit/PLAN.md; write changes straight into the game files (no test copies); can't test in game while away"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-10-27T18:02:35.609Z
---

The user leaves NHL27Kit work running while away and checks in by remote control. They want screenshots of new
features sent as they land (SendUserFile, status proactive), and a written plan (NHL27Kit/PLAN.md, started
2026-10-27). They dropped batch export from the plan.

On 2026-10-27 they said: write everything into the game's files, no copies. So replacements are applied to the real
game folder, always through the kit's ModStore (kit folder mods\), which keeps the original bytes so Restore works.

While away they can't play the game, so they can't confirm anything in game ("like I said I'm not at home so I cant
currently check", 2026-10-27).

**Why:** they follow along from their phone, and want to test results in game, not on copies.
**How to apply:** after each feature, render it with NHL27Kit.exe --shot and send it. Apply to the real game via
ModStore (never raw writes without a backup). Don't use the CLI --target copy flow for testing. Don't stop and ask
them to check in game: keep building, leave test mods applied, and keep a list of what still needs an in-game check
in PLAN.md. Related: [[nhl27kit-project]].
