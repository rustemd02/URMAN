# URMAN Route Navigation Graph

Status: generated and validated, 2026-05-18.

The route map follows the accepted in-world discrete navigation direction: one walking-height route screen at a time, fixed step-forward movement, 90-degree left/right turns, inspectable diegetic signs, and an incomplete journal sketch as support rather than a full top-down overworld.

## Product Defaults

- Scope: MVP spine plus existing stretch branches to river/Alsu/admin.
- Topology: small looped village with route restrictions, not a purely linear corridor.
- Asset strategy: reuse existing first10/remaining route PNGs; generate missing facing views and transition/support passes.
- State strategy: playable baseline graph first; evening/pressure variants only where dramaturgically useful.
- Text strategy: diegetic signs and journal labels remain editable runtime text until Tatar proofing.

## Data Files

- `public/assets/urman_route_map/route_graph.json` — node/facing/action graph.
- `public/assets/urman_route_map/asset_manifest.json` — copied route assets plus generated route-map gap assets.
- `public/assets/urman_route_map/animation_layers.json` — controlled route animation slots.
- `public/assets/urman_route_map/editable_layers.json` — editable sign/journal regions.

## Generated Gap Assets

| Asset ID | Purpose | Priority |
|---|---|---|
| `route_mansur_house_back_to_street_day` | Back/facing view from Mansur turn toward main street | P0 |
| `route_selsmag_fap_lane_back_day` | Back/facing view from FAP/selsmag lane toward crossroad | P0 |
| `route_mosque_sign_back_day` | Back/facing view from mosque sign path toward crossroad | P0 |
| `route_zirat_road_day` | Day baseline for zirat road before evening/pressure | P0 |
| `route_zirat_road_pressure` | Pressure variant for zirat route | P0 |
| `route_forest_approach_back_evening` | Back/facing view from forest approach toward village/zirat road | P0 |
| `transition_step_forward_occlusion` | Reusable 500-800 ms step-forward occlusion pass | P0 |
| `transition_forest_pressure_edge` | Special darker transition to Kara-Urman boundary | P0 |
| `journal_route_sketch_update` | Simple ink draw-on update overlay for newly discovered route lines | P0 |
| `ui_journal_route_sketch_pressure` | Journal sketch with unsafe route marks and old place names, still incomplete | P1 |

All assets in this table are generated and integrated into `public/assets/urman_route_map/`.

## Validation Checkpoint

- 41 PNG files in `public/assets/urman_route_map/`.
- 41 manifest entries in `asset_manifest.json`.
- 41 controlled animation entries in `animation_layers.json`.
- 34 editable text-region entries in `editable_layers.json`.
- 32 graph nodes in `route_graph.json`.
- 10 route-node state transitions in `route_graph.json`.
- 1 support-view transition for the journal sketch state.
- 8 explicit external location targets for interiors / scene handoff: Mansur house, FAP, mosque, river bank and MVP cliffhanger.
- All graph nodes are reachable from `arrival_vehicle_dusk` when ordinary exits and state transitions are considered.
- No pending `missing_required_assets` remain.
- The generated gap list is preserved as `generated_gap_assets` for production traceability.
- `navigation_contract` defines `turnLeft = -90`, `turnRight = 90`, `forward = 0` and reusable transition IDs.
- The route graph keeps journal sketches as support views; it does not introduce a full top-down overworld map.

## QA Rules

- Every playable turn must target a discrete `facing` node.
- `turn_90_default` hides node swap; it must not simulate true 3D rotation of one flat image.
- `step_forward_default` ends on a clean route node, never an arbitrary in-between camera position.
- Journal sketch is incomplete and clue-aware, not a full village map.
- Pressure states must imply social/forest pressure without showing a creature.

## Runtime Integration Checkpoint

Status: first playable runtime integration implemented, 2026-05-18.

- `src/scenes/RouteNavigationScene.ts` loads `route_graph.json`, `asset_manifest.json`, `animation_layers.json` and `editable_layers.json` at runtime.
- `village` now opens the route-navigation scene; the old isometric greybox remains reachable as `villageGreybox`.
- `GameState` stores current route node, discovered route nodes, journal route updates and pressure-journal state.
- Implemented controls: forward, back, 90-degree left/right turns, inspect, journal and external scene/location handoff modals.
- Implemented visual layer support: static route PNG base, controlled runtime animation slot overlays, editable-region hover preview and transition overlays.
- Smoke-tested in Vite: quick play opens route scene, forward movement reaches main street, right turn reaches Mansur turn, journal modal opens, house handoff modal opens, console errors 0 during checked interactions.
- Production build check: `npm run build` passed on 2026-05-18.

Remaining runtime work: replace placeholder `HouseScene` / FAP / river handoff content with real node scenes, add save/load persistence for route state, tune mobile layout and run an actual playtest for orientation clarity.
