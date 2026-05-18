# Core Back Views Worker Notes

Generated on 2026-05-18 for the URMAN route-map integration controller.

## Deliverables

- `route_mansur_house_back_to_street_day.png`
- `route_selsmag_fap_lane_back_day.png`
- `route_mosque_sign_back_day.png`
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

## Visual References Used

- `public/assets/urman_route_map/route_mansur_house_turn_day.png`
- `public/assets/urman_route_map/route_selsmag_fap_lane_day.png`
- `public/assets/urman_route_map/route_mosque_sign_day.png`
- `public/assets/urman_route_map/route_village_crossroad_day.png`

## Generation Method

Used the built-in image generation tool with three separate prompts, then copied generated results into the assigned worker scope. The generated 16:9 images were normalized with `sips` to `2048x1152`.

## Acceptance Notes

- All three PNGs are non-transparent RGB route screens.
- All three use walking-height route composition, not top-down map composition.
- Movement is implied by discrete facing direction and route landmarks; no fake continuous 3D rotation is baked in.
- No full creature reveal, glowing eyes, blood, horror props, UI arrows, or quest markers were intentionally included.
- Diegetic text is intentionally blank/placeholder-like and should be supplied through editable runtime layers after proofing.

## Controller Caveats

- `route_mansur_house_back_to_street_day.png` does not show a prominent Niva; the Mansur yard/gate is present as the stronger edge landmark. This is acceptable for navigation, but the controller may choose to request a Niva-visible variant if continuity with `route_mansur_house_turn_day.png` is considered critical.
- `route_selsmag_fap_lane_back_day.png` keeps the FAP/shop readable at side edges while prioritizing the notice board and crossroad ahead.
- `route_mosque_sign_back_day.png` keeps the mosque calm and side/back oriented, avoiding horror coding around the mosque.
