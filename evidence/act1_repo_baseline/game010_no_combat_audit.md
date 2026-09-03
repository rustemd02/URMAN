# GAME-010 Audit — No-Combat Invariant (P0, KEEP)

- Date: 2026-09-03
- Commit: `2365d0c` (branch `main`)
- Method: tracker-ordered sweep on the Act I surface (fullgame excluded by
  `--glob '!**/fullgame*'`):

```
rg -n -i 'attack|weapon|damage|health|combat' game/scripts game/project.godot \
   content/campaigns/urman.chapter1 src-dotnet --glob '!**/fullgame*'
```

- **Result: zero matches** across the Godot scripts, the input map (no
  attack/weapon actions bound), the Chapter 1 campaign content, and the
  portable Core runtime.
- Threat model stays route/sound/rules-shaped: no damage/health loop, no
  weapon verbs, no combat-shaped UI or input; the Kara escalation is
  environmental (MAP-008 receipt) and the finale is a dialogue/hard-cut rule
  (MAP-009 receipt).
- Manual classification: no placeholder combat verbs lurk under other names —
  interaction IDs are investigation/route actions (`route-to-fap`,
  `zirat-road-to-forest`, old-PC search/open/save, dialogue effects), verified
  in STATE-001's caller audit.

## Verdict

No-combat invariant holds on `2365d0c`. Reopen condition per tracker: any
player-facing combat verb, even as a placeholder.
