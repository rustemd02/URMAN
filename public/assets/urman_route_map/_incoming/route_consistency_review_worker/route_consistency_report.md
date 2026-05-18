# Route Consistency Report

Worker: `route_consistency_review_worker`  
Date: 2026-05-18  
Scope reviewed: `route_graph.json`, `asset_manifest.json`, route art/product docs.

## Summary

The asset pack is internally consistent at the `backgroundAssetId` level: every current route node background exists in `asset_manifest.assets`. The main remaining issue is not missing PNG references, but graph playability: several copied route nodes are currently orphaned or bypassed, and the listed missing back/facing views are not yet modeled as discrete graph nodes.

## Findings

### P0: Core spine does not currently reach forest/Kara-Urman through route nodes

`zirat_road_forward_evening.exits.forward` points to `zirat_general_late_evening`, which is not a route-map node in this graph. As a result, `forest_approach_evening`, `forest_approach_pressure`, `forest_approach_voice_lock`, `kara_urman_edge_pressure`, `kara_urman_edge_voice_moment`, and `kara_urman_edge_rinat_interruption` are not reachable from the playable route graph unless another runtime system injects the transition.

Recommendation: make `zirat_road_forward_evening` continue to `forest_approach_evening`, or explicitly add/import a `zirat_general_late_evening` route node that then exits to `forest_approach_evening`.

### P0: Stretch branch nodes exist but are not connected

The graph includes these playable-looking nodes, but no current route node exits into them:

- `bridge_river_turn_day`
- `alsu_meeting_spot_day`
- `admin_archive_corner_day`
- `unsafe_path_blocked_evening`

Each only has a back exit, so the assets are copied but unreachable as route navigation. This conflicts with the product default of "MVP spine plus existing stretch branches" unless the controller intentionally keeps them disabled.

Recommendation: either mark them disabled/optional in metadata, or add discrete crossroad/side-facing nodes and exits for river/admin/unsafe branches.

### P0: Required back/facing views are listed as missing, but not represented as target nodes

The manifest correctly lists these P0 missing assets:

- `route_mansur_house_back_to_street_day`
- `route_selsmag_fap_lane_back_day`
- `route_mosque_sign_back_day`
- `route_forest_approach_back_evening`

However, current `back` exits generally jump directly to the previous forward node. That is acceptable for a greybox reverse transition only if the player is not expected to stand in the back-facing view. It does not yet satisfy the art spec's minimum facing set for important places that require `back` views.

Recommendation: after these assets are generated, add explicit back-facing nodes or mark the `back` action as a reverse transition that does not enter a new facing node.

### P1: Rinat/Kara-Urman interruption asset IDs are confusing

There are no exact duplicate asset IDs, but there are two highly similar interruption assets:

- `route_kara_urman_edge_rinat_interruption`
- `route_kara_urman_rinat_interruption`

Only `route_kara_urman_edge_rinat_interruption` is referenced by the graph. The second asset appears to be an alternate and should be renamed or annotated before final integration.

Recommendation: keep one canonical graph asset, and label the other as `alternate`, `unused`, or a more precise state such as `route_kara_urman_edge_rinat_interruption_alt_admin_gap`.

### P1: Main-street pressure alternates need status labels

The manifest includes both:

- `route_main_street_entry_pressure_watchers`
- `route_main_street_watchers_pressure`

Only `route_main_street_entry_pressure_watchers` is referenced by the graph. This is probably fine, but the unreferenced alternate should be marked as alternate/unused to avoid future accidental selection.

## Review Questions

### Does every node `backgroundAssetId` exist or appear in `missing_required_assets`?

Yes. Every current node `backgroundAssetId` exists in `asset_manifest.assets`. None of the current node backgrounds rely on `missing_required_assets`.

Transition assets are different: `transition_step_forward_occlusion`, `transition_forest_pressure_edge`, and `journal_route_sketch_update` are referenced by transitions and correctly listed as missing.

### Are 90-degree turns represented as discrete target nodes rather than fake rotation?

Mostly yes. Existing turn actions target discrete nodes:

- `main_entry_forward_day.turnRight -> mansur_turn_forward_day`
- `village_crossroad_forward_day.turnLeft -> selsmag_fap_lane_day`
- `village_crossroad_forward_day.turnRight -> mosque_sign_day`
- pressure crossroad turn exits also target discrete nodes.

Risk: future stretch branches should not be attached as fake rotations from the crossroad image. They need their own discrete facing nodes or an explicit disabled/locked status.

### Are journal map and route graph keeping top-down map as support only?

Yes. The docs and manifest keep `ui_journal_route_sketch_base` as a support layer. The route graph is in-world node navigation, not a top-down overworld. Missing journal update/pressure assets are support overlays, not primary navigation.

### Are there obvious duplicate Rinat/Kara-Urman interruption assets that should be disambiguated?

Yes. `route_kara_urman_edge_rinat_interruption` and `route_kara_urman_rinat_interruption` should be disambiguated before integration. The first is referenced; the second is currently unreferenced alternate material.

### What additional images are truly needed beyond current `missing_required_assets`?

No additional images are strictly required to cover existing node backgrounds. If the stretch branches are meant to be playable from the start, additional discrete facing/connector views may be needed for crossroad-to-river/admin/unsafe transitions, because current crossroad actions already occupy forward/left/right/back.

Concrete candidates if stretch branches stay in scope:

- a crossroad/side-facing view toward river/bridge, or an alternate crossroad node that exposes `bridge_river_turn_day`;
- a crossroad/side-facing view toward administration/archive, or an alternate notice-board/admin-facing node;
- a pressure/blocked side view that naturally leads to `unsafe_path_blocked_evening`.

If stretch branches are intentionally disabled for the first integration, no new image requests are needed beyond the current `missing_required_assets` list.

