# Batch 4 Route Pressure Worker Notes

Status: DONE draft, 2026-05-18.

Scope:

- Inventory #22: `route_main_street_entry_evening`, `route_main_street_entry_pressure_watchers`
- Inventory #23: route navigation support nodes and pressure/evening variants
- Inventory #30: `route_bridge_river_turn_*`, `route_alsu_meeting_spot_day`
- Inventory #31: `route_forest_approach_voice_lock`, `route_kara_urman_edge_rinat_interruption`
- Inventory #32: `route_admin_archive_corner_day`

Read before generation:

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/village_route_art_spec.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`

Visual references checked:

- `public/assets/urman_mvp_first10/route_main_street_entry_day.png`
- `public/assets/urman_mvp_first10/route_kara_urman_forest_edge_pressure.png`
- Existing `public/assets/urman_mvp_remaining/route_*.png`
- Existing incoming route screens from `batch2_locations_completion_worker` and `batch2_route_kit_worker`

Generation approach:

- Used built-in image generation in the accepted URMAN ink-wash storybook direction.
- Prompted each base as a separate 16:9 route/navigation screen rather than one combined batch image.
- Copied generated PNGs from the Codex generated-images folder into this worker folder.
- Normalized all final bases to `2048x1152` PNG with Lanczos resize/crop.
- Created `contact_sheet.jpg` locally for review.
- Did not edit shared manifest, shared metadata, KB files or any folder outside `public/assets/urman_mvp_remaining/_incoming/batch4_route_pressure_worker/`.

Tone constraints checked:

- No full creature reveal.
- No glowing eyes.
- No top-down map.
- No quest markers.
- Text/signs are blank or intentionally illegible and should be authored as editable overlays later.
- Watchers are distant ordinary silhouettes/window presences only.
- `route_forest_approach_voice_lock.png` uses branch/shadow/silence pressure, not a monster.
- `route_bridge_river_turn_dusk_pressure.png` keeps the water line as a false-lead pressure mood, without a Су Анасы figure.
- `route_kara_urman_edge_rinat_interruption.png` includes an ordinary human Rinat-like silhouette baked into the base. If runtime needs to toggle Rinat separately, merge should either cut/regenerate a no-Rinat base or author a separate Rinat overlay.

Validation:

- `sips` dimension check passed for all eight PNG bases: `2048x1152`.
- `contact_sheet.jpg` generated and visually checked.
- `python -m json.tool` validation passed for all three draft JSON files.
