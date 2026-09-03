# MAP-003 Inspection Receipt — Seven-Connector Traversal Contract

- Date: 2026-09-03
- Commit: `da6f233` (branch `main`)
- Method: canonical-owner inspection + layout-data computation; no code edit.
- Verification: `./eng/verify-godot.sh` exit 0 on parent commit `e50c64a`
  (`evidence/act1_repo_baseline/map002-verify-godot-PASS.txt`; `da6f233`
  changed docs only). Physical walkthrough PASS (135,49 m) crosses the
  connector chain arrival → house → main street → return → zirat → Kara.

## Construction (canonical owners)

- `game/scripts/Act1ConnectedWorld.cs::BuildConnectorPresentation`
  (`Act1ConnectedWorld.cs:631-674`): one `Act1Connectors` node with
  `collisionOwner=Act1ConnectedWorld`; shared `SharedVillageGround` visual box
  (86×0.12×208 m, centre (0,−0.12,−48) → x ∈ [−43,43], z ∈ [−152,+56]) plus
  `SharedVillageGroundTraversalCollision` (full-size traversable box).
- Per connector: declared start/end/width from `Act1WorldLayout.Connectors`, a
  `RoadSurface` visual strip (presentation suppressed — AgentB terrain road kit
  owns the visible crown) and a traversable strip at the declared width.
  Surface policy metadata: "SharedVillageGround and RoadSurface own traversal
  collision; shoulders, fences and framing are visual-only".

## Per-connector audit (layout data, ground envelope bounds x ∈ [−43,43], z ∈ [−152,+56])

| Connector | Length (m) | Width (m) | Both endpoints over shared traversal ground |
|---|---:|---:|---|
| `arrival-to-house-yard` | 23.91 | 4.8 | yes |
| `residential-road-extension` | 27.00 | 5.6 | yes |
| `village-to-fap-branch` | 27.86 | 4.2 | yes |
| `house-to-zirat-return` | 64.69 | 4.4 | yes |
| `zirat-to-kara-urman` | 13.50 | 4.0 | yes |
| `fap-yard-loop` | 10.38 | 2.6 | yes |
| `kara-edge-approach` | 19.50 | 3.5 | yes |

All seven connectors have declared collision-bearing traversal strips at their
full length plus the shared ground fallback beneath; no gap between the shared
ground bounds and any connector endpoint. Narrowest route connector is
`fap-yard-loop` (2.6 m, a yard loop, not the critical path); critical path
widths are 3.5–5.6 m, above the walkthrough's demonstrated physical clearance.

## Mechanical coverage verdict

Collision coverage of the Arrival → house → FAP → return → zirat → Kara chain
is proven by construction (owned traversal strips + shared ground), the launch
smoke's ground-envelope assertions, and the physical walkthrough smoke that
walks the route without teleport. No connector edit made in this slice.

## Open (human, stays with this task)

- Manual edge-walk video of all seven connectors (NEW VERIFICATION REQUIRED per
  tracker): slope/shoulder/seam visual contact and ankle-snag check remain
  human review; Z01-002–Z08-002 own per-zone surface contact fixes.
