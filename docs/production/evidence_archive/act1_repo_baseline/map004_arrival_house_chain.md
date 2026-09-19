# MAP-004 Inspection Receipt — Arrival → House Readable Chain

- Date: 2026-09-03
- Commit: `3e27772` (branch `main`)
- Method: canonical-owner inspection of the authored route data and smoke
  assertions; no code edit.

## The chain (single readable entrance → gate → path → door line)

`game/scripts/experiments/agent_b_act1/AgentBAct1Layout.cs` owns the route
chain data used by both the composition and the physical walkthrough:

- Arrival spawn `(0, 0.05, 9)` between the arrival fence/gate landmarks
  (`ArrivalGateWest`/`ArrivalGateEast` at z≈16–17 flank the entry;
  `Act1ConnectedWorld.cs:6490-6491`).
- `HousePathAxis` turns off the main street at `(-1.2,-8)` → `(-6,-5.5)` →
  `(-12,-2.5)` → `(-19,0)` → `(-24,1.2)` (dedicated approach path, not a
  diagonal test line).
- `WalkChain` walks that axis and passes "through the open yard gate (gap
  z 1.75–3.45)" at `(-25.2, 2.6)` → yard anchor `(-24.4, 1.7)` → door
  approach `(-25.9, 0.9)` in front of the veranda, then mirrors the same
  nodes back out (bidirectional readability is authored into the chain).
- The yard gate is a real composition object: `BabaiYardOpenGate`
  (`AddVisualGate`, 2.2 m wide × 1.35 m high,
  `Act1ConnectedWorld.cs:4765`) sits in the fence line between
  `BabaiYardWestBoundary`/`BabaiYardEastBoundary` (visual-only boundaries;
  the shared ground owns traversal).
- Door anchor contract: `HouseExteriorSpawn = (-25.9, 0.05, 0.9)`; the
  physical walkthrough asserts the `arrival-enter-house` interaction target
  sits exactly there (±0.01 m,
  `Act1FirstPersonWalkthroughSmokeTest.cs:75-79`) and approaches at the
  authored `InteractionStandOff` ray distance (ray distance unchanged by this
  slice).

## Mechanical evidence

- `act1-first-person-walkthrough: PASS mode=physical-characterbody-walk …
  distance=135,49m final-zone=kara_urman_night` (verify run on `e50c64a`,
  `evidence/act1_repo_baseline/map002-verify-godot-PASS.txt`): the chain is
  physically walkable arrival → yard gate → door with real movement and the
  production ray, including the HouseDoor-at-anchor assertion.
- Collision clearance: chain nodes lie over the shared traversal ground and
  the `arrival-to-house-yard` traversal strip (MAP-003 receipt); boundaries
  and gates are visual-only per `Act1VisualOnlyPolicy`.

## Open (human, stays with this task)

- First-time wayfinding is not proven: the tracker requires an observed
  first-person route recording (NEW VERIFICATION REQUIRED). Marker/teleport
  fallback is forbidden by the tracker reopen condition; no marker was added
  in this slice.
