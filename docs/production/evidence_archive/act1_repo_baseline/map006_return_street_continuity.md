# MAP-006 Verification Receipt — FAP Egress → Return Street → Zirat Continuity

- Date: 2026-09-03
- Commit: `455702b` (branch `main`)
- Method: existing evidence re-audit + canonical-owner inspection; no code edit.

## Former blocker removed: "все три текущих capture вида одинаковы"

The tracker's stated blocker for this zone was that the three requested views
(forward/back/depth) captured identically. That was part of the stale
environmental 44/25 capture failure, disproven on the current build by the
BASE-005 diagnostic runs (`evidence/act1_repo_baseline/base005-capture-diag-run2-PASS-44of44.txt`):

```
frame=23 id=connective_street_return_forward cam_pos=(-15.40,1.75,-22.00) fwd=(0.423,-0.013,-0.906) sha256=de1028a37734 same_as_prev=false
frame=24 id=connective_street_return_back    cam_pos=(-7.00,1.75,-41.50)  fwd=(-0.596,-0.011,0.803) sha256=729a522230f5 same_as_prev=false
frame=25 id=connective_street_return_depth   cam_pos=(-3.00,1.75,-50.00)  fwd=(0.174,-0.009,-0.985) sha256=177b0ef8c482 same_as_prev=false
```

Three distinct camera transforms → three distinct PNG SHA-256 values; the
44/44 uniqueness gate passed on both runs. No duplicate-frame recurrence
(`same_as_prev=false` throughout).

## Persistent-world continuity anchors (one map, no scene swap)

- Egress: FAP yard gate `FapYardGate` + `FapYardLoop` connector connect the
  clinic back to the branch road; the return street is the same persistent
  `village_day` exterior (connected mode never reloads zones).
- Return street: `house-to-zirat-return` connector (64.69 m, width 4.4 m,
  MAP-003 receipt) carries the route south; return-street parcels/fences
  (`ReturnWestFieldBoundary`, `ReturnEastFieldBoundary`,
  `Act1ConnectedWorld.cs:745-746`) frame both sides; wet road kit owns the
  surface rhythm (ReturnStreet crown + shoulders + ditches attachments).
- Depth toward zirat: `returnTransitionAnchor` =
  `zirat_road` origin + (0, 0.035, -7.0) with the authored
  `RoadCrown_ApproachWorn` segment (ART-002) and sedge framing; distant
  village framing keeps a horizon beyond the return street
  (`BuildVillageFraming`, compositionPolicy "route center remains open").
- Physical proof: the walkthrough crosses this exact chain in one connected
  world (PASS 135,49 m; no scene transition between FAP and zirat beats).

## Open (human, stays with this task)

- Flat-corridor / empty-horizon judgement and direction-keeping toward the
  zirat remain human review (supplemental pitched capture + manual walkthrough
  per tracker); fog-as-concealment is a reopen condition and is not used.
