# Batch 4 Support Locations Worker Notes

Status: generated draft, 2026-05-18.

Scope was limited to:

`public/assets/urman_mvp_remaining/_incoming/batch4_support_locations_worker/`

No shared `asset_manifest.json`, root PNG files or other incoming worker folders were edited.

## Inputs Read

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/village_route_art_spec.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- Existing route/location PNGs under `public/assets/urman_mvp_remaining/`, including the Mansur yard route, selsmag/FAP lane, mosque sign route and FAP waiting room references.

## Generation Method

Used the built-in `imagegen` skill/tool, one prompt per requested asset. Prompts enforced:

- ink-wash storybook / muted watercolor / paper grain;
- grounded contemporary Tatar village details;
- ordinary first with subtle wrongness;
- important text blank/editable;
- no monster reveal, no glowing eyes, no gore;
- mosque/Timur scene: calm safe light, not sinister;
- river scene: false Su-Anasy hint only through still water/ripple, no creature/woman/face/hand reveal.

Generated sources were copied from:

`/Users/unterlantas/.codex/generated_images/019e3b06-2749-73a3-987f-fd7017fa6780/`

Final PNGs were resized with `sips` to 2048x1152. The generated source aspect ratio was already 16:9-like; resize was used for production dimension normalization.

## Files

- `loc_yard_shed_niva_evening.png` - #21 yard / shed / old Niva evening
- `loc_selsmag_counter_interior.png` - #24 selsmag counter interior
- `loc_mosque_tea_office_timur_calm.png` - #26 Timur hazrat tea office, calm safe light
- `loc_river_bank_day.png` - #30 river bank day with indirect water stillness only
- `loc_admin_archive_corner_day.png` - #32 administration archive corner day
- `loc_arrival_road_to_kyrlay_dusk_alt_if_needed.png` - #17 arrival road alt, generated because no `loc_arrival*` target was found under `public/assets/urman_mvp_remaining` at worker start
- `manifest_draft.json`
- `animation_layers_draft.json`
- `editable_layers_draft.json`
- `contact_sheet.jpg`

## Visual QA

Checked generated PNGs directly before drafting metadata:

- Dimensions: all six final PNGs are 2048x1152.
- Style: aligned with existing remaining locations: handmade ink line, muted watercolor, visible paper grain.
- Tone: slow-burn village unease, no blood, gore, skull props, jump-scare framing, glowing eyes or monster reveal.
- Mosque/Timur safety: `loc_mosque_tea_office_timur_calm.png` reads as calm, bright and humane, not cursed or anti-Islamic.
- River false lead: `loc_river_bank_day.png` contains only still water/ripple ambiguity; no woman, creature, face or hand.
- Text policy: signs, boards and labels are blank or illegible texture. Use `editable_layers_draft.json` for runtime text.
- Route/location affordances: yard, river and arrival frames keep clear path/inspection regions; interiors keep readable interactable zones.

## Review Notes

- `loc_selsmag_counter_interior.png`: shelves contain generated product-label texture. Treat as noncanonical noise; production prices/product names should be editable overlays only.
- `loc_admin_archive_corner_day.png`: binder/drawer marks are texture only. Archive labels should come from runtime text overlays.
- `loc_arrival_road_to_kyrlay_dusk_alt_if_needed.png`: keep only if integration still lacks a stronger #17 root arrival frame.
