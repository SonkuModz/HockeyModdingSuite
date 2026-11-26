---
name: nhl24-sportsnet-scorebug
description: Sportsnet score clock for NHL 24 built from the ESPN+ ScoreClock fork; installed and confirmed in-game 2026-11-23
metadata:
  type: project
---

The Sportsnet score clock (an AS3 fork of NHL24Kit's ESPN+ clock) is installed in NHL 24 as the ModStore entry "Score clock: Sportsnet". The user confirmed it in-game on 2026-11-23, including the power-play logo tile for both teams.

Notes are in NHL27Kit/docs/nhl24/sportsnet-scorebug-spec.md. The goal animation is confirmed in-game too.

Released in HMS 1.5.0 (2026-11-23, PR #14). The source and build script are in tools/scorebug-sportsnet; the shipped movie is App/Assets/scoreclock-sportsnet.swf. Install it from the app's Scorebug page or with `scorebug24 --movie`.

**Why:** follow-up tweaks will rebuild from that source instead of starting over.

**How to apply:** edit `tools/scorebug-sportsnet/ScoreClock.tpl.as`, run `build.py BASE.swf OUT.swf`, copy the result to App/Assets, preview in Ruffle, then write it with NHL 24 closed. See [[nhl27kit-project]].
