# MAP-005 Inspection Receipt — FAP Branch Sightline & Landmarks

- Date: 2026-09-03
- Commit: `455702b` (branch `main`)
- Method: canonical-owner inspection; no code edit.

## Branch read chain (interaction → signboard → road → destination)

1. **Physical branch trigger** — `RoadToFap` interaction box
   (`StyleBenchmarkZone.cs:258-266`) sits at `(3.8, 0.75, -8.2)` — exactly the
   `village-to-fap-branch` connector start — with the diegetic prompt
   «Пойти к фельдшерскому пункту» and dispatches
   `urman.chapter1:interaction/route-to-fap` → `fap_clinic@waiting_room`.
   No quest-marker UI involved.
2. **Diegetic ФАП signboard** — `FapAuthoredWayfindingBoard` (blank authored
   board meshes) carries one composed `FapWayfindingLabel` Label3D «ФАП»
   (`Act1ConnectedWorld.cs:1834-1845`); board/label are smoke-protected by
   `Act1DemoLaunchSmokeTest` (single labeled FAP owner contract). A second
   street-side landmark `FapApproachWayfinding` («ФАП») anchors the approach
   from the main street (`Act1ConnectedWorld.cs:5049`).
3. **Authored branch road surface** — since ART-002 (`25b1812`) the branch
   connector carries its own narrower authored segment
   (`WetVillageRoadFapBranchCrown` = `RoadCrown_BranchWet`) placed at the
   connector midpoint, so the branch reads as a road, not an invisible
   trigger line.
4. **Destination silhouette** — the single FAP exterior volume
   (`FapClinicAuthoredKitPresentation`, smoke-protected single owner) plus
   `FapYardGate` (`Act1ConnectedWorld.cs:6636`) give the branch a visible
   terminus; the return view looks back along the same branch road.

## Verdict

The branch is readable before interaction: named trigger at the branch head,
two diegetic «ФАП» landmarks, authored road surface, single destination
volume. Rain/fog readability of the landmarks is owned by Z02-007/Z06-007
(weather checklist) — not altered here.

## Open (human, stays with this task)

Forward/back route clips and a first-time trace without wrong turns remain
NEW VERIFICATION REQUIRED (human comprehension); rollback condition (duplicate
facade / lost landmark in rain) untouched.
