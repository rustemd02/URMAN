# MAP-007 Verification Receipt — Village → Zirat Transition

- Date: 2026-09-03
- Commit: `b4b92fb` (branch `main`)
- Method: canonical-owner inspection + existing capture evidence; no code edit.

## Spatial pause, boundary, return sightline (composition anchors)

- **Narrowing road + boundary**: `ZiratAuthoredBoundaryFence` is placed
  laterally at `origin + (6.7, 0, -15.0)`, yaw 90°, explicitly out of the
  first-person road window ("the gate is an open presentation cue, never a
  blocker", `Act1ConnectedWorld.cs:2127-2131`); `ZiratOpenGate` continues the
  authored fence line. Shoulders with muted road-band material override and
  the roadside ditch/culvert give the road a narrower, quieter read.
- **Return sightline**: village-side edge is composed from authored parcels
  (`zirat_road@village-edge-authored-parcels` group with west facade/fences
  and east shed, `Act1ConnectedWorld.cs:2914-2947`), and the transition crown
  from the village side is the authored `RoadCrown_ApproachWorn` variant
  (ART-002) — the player can look back along a real road to the village.
- **Non-inscribed restraint**: `ZiratMarkerGroup_Low/Far` are plain
  non-inscribed slabs (kit manifest cultural gate); the composition adds no
  invented epitaphs/symbols — religious review is CULTURE-003
  (BLOCKED_EXTERNAL), unchanged by this slice.
- **Duplicate-direction blocker removed with evidence**: diag-run2 zirat
  frames are distinct — forward `e43c4ee1…`, back `8e88b00c…`,
  left `898b5466…`, right `a2c0a8df…`, depth `b6d72e76…`, all
  `same_as_prev=false` (`evidence/act1_repo_baseline/base005-capture-diag-run2-PASS-44of44.txt`).
  The tracker's "четыре направления duplicated" was part of the stale 44/25
  environmental failure.
- Physical passage: walkthrough crosses return street → zirat → Kara
  (`zirat_road@village_side` spawn, PASS 135,49 m).

## Open (human, stays with this task)

- Religious/local framing review: CULTURE-003 (BLOCKED_EXTERNAL; specialist
  sign-off required before final acceptance).
- Flat-horizon / grade judgement: human review + supplemental pitched capture
  (Z07-004 owns zone-level boundaries).
