# Batch 3 Documents Worker Notes

Date: 2026-05-18

Scope: P0 document/evidence bitmap bases for asset inventory #46, #47, #48, #49 plus missing #45 variants.

Output directory: `public/assets/urman_mvp_remaining/_incoming/batch3_documents_worker/`

## Source Context Read

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/asset_pack_first10/animation_layers.json`
- `docs/urman_knowledge_base/asset_pack_first10/editable_layers.json`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `public/assets/urman_mvp_first10/doc_marat_medical_record_bg.png`

## Generation Mode

- Used built-in `image_gen` via the `imagegen` skill.
- Copied final selected outputs from Codex generated image storage into the worker output directory.
- Resized outputs locally with PIL to approved runtime dimensions.
- Did not edit the shared manifest, shared metadata, or knowledge-base files.

## Runtime Text Policy

All important document content is intentionally absent from the bitmap bases. The images provide paper, scan artifacts, tables, UI frames, redaction marks and blank fields only.

Editable overlay text should support:

- Cyrillic
- Latin, if needed for file paths or old PC metadata
- Tatar letters: `ә ө ү җ ң һ`

## Style Notes

- Kept documents mundane: village bureaucracy, old PC evidence hub, notebook pages, archive folder.
- Avoided gore, blood, horror props, skull imagery, obvious monsters, glowing eyes and full creature reveal.
- Kept pact-folder images as ordinary accounting paperwork. The disturbing meaning should come from data-driven overlays and clue graph behavior, not from baked art.
- Kept messenger states restrained: old PC/archived log rather than modern phone UI or cyberpunk glitch.

## Size Normalization

- `doc_marat_medical_record_redacted_bg.png`: 1463x2048
- `doc_marat_medical_record_metadata_card.png`: 1024x1024
- `doc_cemetery_registry_photo_bg.png`: 1536x2048
- `doc_cemetery_registry_line_crop_bg.png`: 2048x1152
- `doc_marat_diary_early_note_bg.png`: 1463x2048
- `doc_marat_diary_angry_note_bg.png`: 1463x2048
- `doc_marat_diary_forest_route_note_bg.png`: 1463x2048
- `doc_marat_saved_messages_base.png`: 2048x1152
- `doc_marat_saved_messages_warning_state.png`: 2048x1152
- `doc_pact_folder_household_list_bg.png`: 1536x2048
- `doc_pact_folder_incident_log_bg.png`: 1463x2048
- `doc_pact_folder_compensation_note_bg.png`: 1463x2048

## QA Notes

- `file` check confirmed PNG output and normalized dimensions.
- Draft overlay rectangles are approximate normalized regions intended for first integration pass.
- Base images may contain abstract strokes, dashes, stamp ghosts and UI glyph-like marks; they should not be treated as canonical text.
- If a strict OCR pass later finds accidental readable text, fix by painting it out or regenerating the affected base before promoting to shared manifest.
