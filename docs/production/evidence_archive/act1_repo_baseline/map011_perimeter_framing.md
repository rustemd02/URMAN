# MAP-011 Inspection Receipt — World Boundaries & Perimeter Framing

- Date: 2026-09-03
- Commit: `1581899` (branch `main`)
- Method: canonical-owner inspection of the framing layer; no code edit.

## Perimeter composition inventory (authored framing, visual-only)

`BuildVillageFraming` (`Act1ConnectedWorld.cs:681-700`) composes the boundary
layer out of seven authored scopes: `BuildAuthoredOutdoorBackbone` (terrain
banks/breams), arrival/main-street framing, house exterior, FAP exterior,
zirat exterior, Kara edge framing, village landmark kit and the village
infrastructure layer (utility poles + sagging cables, six broken field-boundary
fence runs flanking Arrival/MainStreet/Return, `Act1ConnectedWorld.cs:741-746`).
The file carries ~306 visual fence/tree/faceted-mass builder calls; side
parcels and far masses exist per zone (e.g. `KaraLateralForestShelfWest/East`,
`KaraDistantEdgeWindow*`, `ZiratDistantVillageMass`, village-edge parcel
group). Boundary policy metadata: shoulders/fences/framing are visual-only;
the shared ground owns traversal — no invisible-wall masking.

- Suppression sanity: `ConnectedWorldPresentationSuppressions` removes
  duplicate legacy primitives with recorded reasons (e.g. legacy Kara
  approach dressing that occluded the authored forest edge; duplicate zirat
  closure fences crowding the route) — suppression list is explicit and
  reason-tagged, not a fog wall.

## Evidence status

- Lateral/forward/back/depth frames across all anchors are distinct and
  unique-hash proven (BASE-005 diagnostic runs; 44/44 twice).
- Up/down pitched views are NOT part of the 44-frame core contract —
  CAPTURE-004 owns the supplemental directional set (open).

## Open (human, stays with this task)

- Manual perimeter walk, pitched supplemental capture and the "no naked
  horizon / no skybox edge / no test geometry" judgement remain NEW
  VERIFICATION REQUIRED with this task and CAPTURE-004/006.
