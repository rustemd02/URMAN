# Zirat / Forest Route Views Worker Notes

Generated on 2026-05-18 for incoming integration only.

## Scope

- Write scope respected: `public/assets/urman_route_map/_incoming/zirat_forest_views_worker/`.
- Root route map manifests/docs were read but not edited.
- Generated three opaque route PNGs and support drafts:
  - `route_zirat_road_day.png`
  - `route_zirat_road_pressure.png`
  - `route_forest_approach_back_evening.png`
  - `contact_sheet.jpg`
  - `manifest_draft.json`
  - `animation_layers_draft.json`
  - `editable_layers_draft.json`

## Source References Read

- `AGENTS.md`
- `docs/urman_knowledge_base/village_route_art_spec.md`
- `docs/urman_knowledge_base/route_navigation_product_lock.md`
- `docs/urman_knowledge_base/route_navigation_graph.md`
- `public/assets/urman_route_map/route_graph.json`
- `public/assets/urman_route_map/asset_manifest.json`

Visual references inspected:

- `route_zirat_road_evening.png`
- `route_forest_approach_evening.png`
- `route_forest_approach_pressure_silence.png`
- `route_kara_urman_forest_edge_pressure.png`

## QA Notes

- All three route PNGs were resized to `2048x1152`.
- All three are non-transparent PNGs.
- Composition stays walking-height / in-world route navigation, not top-down map.
- Route affordance is explicit:
  - zirat day: road forward to zirat/cemetery hint;
  - zirat pressure: same road logic, narrower and more occluded;
  - forest back evening: path forward leads out toward village/zirat road for retreat.
- No creature reveal, no glowing eyes, no direct monster silhouette.
- Text is not final. Sign regions are intended for runtime/editable overlays after proofing.

## Known Integration Caveats

- `route_zirat_road_pressure.png` includes faint unreadable weathered marks on the sign face. Treat these as texture/noise, not baked final text.
- `route_forest_approach_back_evening.png` uses a back/oblique sign board primarily as a landmark; controller may decide whether it receives any runtime text.
- The generated day baseline is brighter than the existing evening zirat reference by design; pressure/evening variants should carry the heavier tone.

## Final Prompt Summaries

`route_zirat_road_day`: day baseline road to zirat, dry grass, denser but ordinary trees, blank sign placeholder for `зират`, distant cemetery hint, clear forward/back route affordance.

`route_zirat_road_pressure`: pressure variant of same route logic, road narrowed by still branches/dry grass, sign harder to read, no monster or glowing eyes.

`route_forest_approach_back_evening`: back-facing view from forest approach toward village/zirat road, forest frames rear/edges, village path visible ahead, supports retreat.
