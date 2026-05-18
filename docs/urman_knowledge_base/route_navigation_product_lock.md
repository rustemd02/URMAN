# Route Navigation Product Lock

Status: Defaults applied for route-map production pass, 2026-05-18.

Purpose: зафиксировать продуктовые решения перед генерацией следующего набора картинок для интерактивной карты Кырлая. Это не top-down карта, а in-world route navigation: игрок стоит внутри деревни, видит один route segment, делает шаг вперёд, поворачивается на 90 градусов и осматривает объекты.

## Canon References

- `docs/urman_knowledge_base/village_route_art_spec.md` — главный art/spec документ для ходибельной карты.
- `docs/urman_knowledge_base/gameplay.md` — controls: step forward, turn left/right на 90 градусов, back, inspect.
- `docs/urman_knowledge_base/technical_architecture.md` — route node fields and reusable movement transitions.
- `docs/urman_knowledge_base/decision_log.md` — accepted decision: in-world discrete route navigation, not a full top-down map.
- `docs/urman_knowledge_base/asset_inventory_50.md` — row #23 Village route navigation kit.

## Existing Route Asset Audit

Already generated in `public/assets/urman_mvp_remaining/`:

- Main street: `route_main_street_empty_evening`, `route_main_street_entry_evening`, `route_main_street_entry_pressure_watchers`, `route_main_street_watchers_pressure`.
- Mansur turn: `route_mansur_house_turn_day`, `route_mansur_house_turn_evening`.
- Crossroad: `route_village_crossroad_day`, `route_village_crossroad_pressure`.
- Selsmag/FAP lane: `route_selsmag_fap_lane_day`, `route_selsmag_fap_lane_evening`, `route_selsmag_fap_lane_pressure`.
- Mosque sign: `route_mosque_sign_day`, `route_mosque_sign_evening`.
- Zirat/forest/Kara-Urman: `route_zirat_road_evening`, `route_forest_approach_evening`, `route_forest_approach_pressure_silence`, `route_forest_approach_voice_lock`, `route_kara_urman_edge_voice_moment`, `route_kara_urman_edge_rinat_interruption`, `route_kara_urman_rinat_interruption`.
- Stretch route nodes: `route_bridge_river_turn_day`, `route_bridge_river_turn_dusk_pressure`, `route_alsu_meeting_spot_day`, `route_admin_archive_corner_day`.
- Route restrictions/support: `route_unsafe_path_blocked_evening`, `ui_journal_route_sketch_base`.
- Transitions/overlays: `transition_turn_90_ink_smear`, `overlay_ink_smear_transition`.

Implication: do not regenerate the route pack from scratch unless the current visual direction is rejected. The better production move is to build a route graph around existing images, then generate missing facing views and transition/support images.

## Recommended Product Defaults

1. Scope: MVP village route graph plus stretch branches already supported by assets.
   - Core spine: arrival/main street -> Mansur house -> crossroad -> FAP/selsmag -> mosque -> zirat -> forest approach -> Kara-Urman edge.
   - Stretch branches: river/bridge, Alsu meeting spot, admin/archive corner.

2. Topology: small looped village, not a purely linear corridor.
   - The player should be able to return through known routes and understand Кырлай as a place.
   - Keep scale hidden: no full overworld map, only in-world views and incomplete journal sketch.

3. Asset strategy: reuse existing route PNGs and generate only missing facing views / transition passes.
   - Existing assets become route node backgrounds.
   - New generation should focus on missing `forward/back/left/right` view pairs and route-state alternates.

4. State strategy: baseline day graph first, then evening/pressure overlays and selected alternate scenes.
   - Do not create day/evening/pressure for every single node unless the route graph proves it is needed.
   - Pressure should be mostly overlay/state-driven, with a few painted variants for key moments.

5. Text strategy: diegetic sign text remains editable until Tatar proofing.
   - Use editable regions for `мәчет`, `зират`, `ФАП`, `сельмаг`, `Кара-Урман`, old place names and notice-board text.
   - Avoid baked readable final text in generated route PNGs.

6. Deliverables for the next production pass:
   - `docs/urman_knowledge_base/route_navigation_graph.md`
   - `public/assets/urman_route_map/route_graph.json`
   - `public/assets/urman_route_map/asset_manifest.json`
   - `public/assets/urman_route_map/animation_layers.json`
   - `public/assets/urman_route_map/editable_layers.json`
   - generated missing route PNGs and contact sheets.

## Product Questions To Answer Before Image Generation

1. Scope: should the first interactive map include only the MVP spine, or include stretch branches to river/Alsu/admin from the start?

2. Topology: should Кырлай feel like a small looped place with alternate returns, or like a mostly linear directed route for the vertical slice?

3. Existing assets: should we preserve and reuse the current route PNGs, or intentionally regenerate a new route pack from scratch?

4. State depth: should subagents generate missing `day/evening/pressure` variants broadly, or only the missing baseline/facing views needed for playable navigation?

5. Journal map: should the journal sketch be a simple discovered-route support view, or should it include clue pins, unsafe route marks and old place names immediately?

6. Naming/output: should the interactive map live in a new pack `public/assets/urman_route_map/`, or be folded into `public/assets/urman_mvp_remaining/`?

## Current Recommendation

Use existing assets, create a new `public/assets/urman_route_map/` integration pack, generate only missing facing views and transition/support passes, and keep the journal map incomplete but clue-aware. This preserves the work already done while moving toward a playable route graph.

## Applied Defaults

Because no contrary product answers were provided before production started, the current route-map pass used the recommendation above as a temporary product lock:

- reuse current first10 / remaining route PNGs;
- create a separate `public/assets/urman_route_map/` pack;
- include MVP spine plus existing river / Alsu / admin stretch branches;
- model Кырлай as a small looped route graph with state transitions;
- generate missing facing views and transition/support passes rather than a new route pack from scratch;
- keep journal map incomplete but clue-aware;
- keep all important diegetic sign and journal text editable until Tatar/Russian proofing.
