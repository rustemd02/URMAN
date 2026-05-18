# Batch 2 Locations Completion Worker Notes

Status: generated draft, 2026-05-18.

Scope was limited to:

`public/assets/urman_mvp_remaining/_incoming/batch2_locations_completion_worker/`

No shared `asset_manifest.json`, first10 files or remaining root PNG files were edited.

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
- `public/assets/urman_mvp_remaining/loc_kitchen_first_dinner_warm.png`
- `public/assets/urman_mvp_remaining/loc_kitchen_first_dinner_tense_silence.png`
- `public/assets/urman_mvp_remaining/loc_fap_cabinet_document_desk_day.png`
- `public/assets/urman_mvp_remaining/loc_zirat_general_day.png`
- `public/assets/urman_mvp_remaining/loc_marat_grave_closeup_mossy.png`
- `public/assets/urman_mvp_remaining/loc_marat_grave_closeup_cleaned_readable_base.png`
- `public/assets/urman_mvp_remaining/route_forest_approach_evening.png`
- `public/assets/urman_mvp_first10/route_kara_urman_forest_edge_pressure.png`

## Generation Method

Used the built-in `imagegen` skill/tool, one prompt per requested asset. Prompts enforced:

- ink-wash storybook / muted watercolor / paper grain;
- grounded contemporary Tatar village details;
- ordinary first with subtle wrongness;
- important text blank/editable;
- no monster reveal, no glowing eyes, no gore;
- for Kara-Urman: branches may almost form attention/figure, but no full creature.

Generated sources were copied from:

`/Users/unterlantas/.codex/generated_images/019e3ada-ef5d-74a1-a994-0565a1d330be/`

Final PNGs were resized with `sips` from 1672x941 to 2048x1152. The source aspect ratio was already effectively 16:9; the resize is a tiny normalization for the requested production dimensions.

## Files

- `loc_kitchen_first_dinner_night_talk.png` - #19 night talk kitchen variant
- `loc_fap_waiting_room_day.png` - #27 FAP waiting room day
- `loc_fap_pressure_document_desk.png` - #27 FAP pressure/document desk
- `loc_zirat_general_late_evening.png` - #28 zirat late evening
- `loc_zirat_general_wind_pressure.png` - #28 zirat wind/pressure
- `loc_marat_grave_closeup_journal_crop.png` - #29 Marat grave journal crop
- `route_forest_approach_pressure_silence.png` - #31/#23 forest approach pressure/silence
- `route_kara_urman_edge_voice_moment.png` - #31/#23 Kara-Urman edge voice moment
- `manifest_draft.json`
- `animation_layers_draft.json`
- `editable_layers_draft.json`
- `contact_sheet.jpg`

## Visual QA

Checked generated PNGs directly before drafting metadata:

- Dimensions: all eight final PNGs are 2048x1152.
- Style: aligned with existing remaining locations: handmade ink line, muted watercolor, visible paper grain.
- Tone: subtle domestic/folk horror, no gore, no blood, no skull props, no jump-scare framing.
- Creature policy: no full creature reveal and no glowing eyes.
- Text policy: important text areas are blank or illegible texture; use `editable_layers_draft.json` for runtime text.
- Route screens: forward path/sign landmarks remain readable without floating quest UI.

## Disputed / Review Notes

- `route_kara_urman_edge_voice_moment.png`: the upper-right branch shape is intentionally the strongest "almost figure" in this batch. It has no face, eyes or full body, so it follows the prompt, but it should be reviewed for subtlety before production lock.
- `loc_fap_waiting_room_day.png` and `loc_fap_pressure_document_desk.png`: medical cross symbols are baked as non-text visual signs. If the art direction later wants zero symbolic marks in base bitmaps, cover or repaint them during integration.
- Grave/date/name text must not be accepted from bitmap texture. The grave crop is a blank inscription base only.
