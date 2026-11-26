---
name: nhl24-real-life-calibration
description: NHL 24 tuning-to-real-life (NHL EDGE) calibration is paused until the user has time; how to resume
metadata:
  node_type: memory
  type: project
  originSessionId: 6bf7147d-9574-47ad-ade1-943c219d317f
  modified: 2026-11-04T04:31:23.135Z
---

On 2026-11-03 the user paused calibrating NHL 24 gameplay tuning to real NHL stats (NHL EDGE via nhl-api-py and api.nhle.com/stats) "until I have the time". Don't restart it unprompted.

**Why:** It needs the user to sim CPU vs CPU games and send box scores; NHL 24 computes real speeds in code from ratings, so outcomes (shots, save % by danger, hits, penalties) are matched instead of mph.

**How to apply:** When they come back to it, start from PLAN.md §28 ("Real-life calibration") and docs/nhl24/nhl_baseline_2025-26.json; ask for period length and difficulty with the screenshots. Related: [[nhl27kit-project]].
