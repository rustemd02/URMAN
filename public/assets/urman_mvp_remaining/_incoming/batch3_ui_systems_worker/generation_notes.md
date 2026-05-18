# Batch 3 UI Systems Worker Notes

Status: generated draft, 2026-05-18.

Scope was limited to:

`public/assets/urman_mvp_remaining/_incoming/batch3_ui_systems_worker/`

No shared `asset_manifest.json` or `docs/asset_pack_remaining/*.json` files were edited.

## Inputs Read

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `public/assets/urman_mvp_first10/ui_old_pc_desktop_base.png`

## Generation Method

Used the built-in `imagegen` skill/tool, one prompt per asset. The old PC desktop base was used as style reference for CRT/Win98-like screens; journal, dialogue and vocabulary screens were directed toward paper-investigation UI.

Generated sources were copied from:

`/Users/unterlantas/.codex/generated_images/019e3ada-efaa-7d70-981d-cf033b5e130f/`

Final PNGs were resized with `sips` to the requested dimensions:

- UI screens: 2048x1152
- Icon contact sheet: 2048x1024

Raw generated PNGs are kept beside the finals as `*.raw.png`.

## Files

- `ui_archive_search_empty.png` — #35 empty archive search state
- `ui_archive_search_results.png` — #35 archive results state
- `ui_tatarwiki_article_template.png` — #36 article/re-read template
- `ui_journal_clue_graph_base.png` — #39 clue graph notebook base
- `ui_dialogue_key_picker_base.png` — #40 dialogue key picker base
- `ui_vocabulary_cards_base.png` — #41 vocabulary cards base
- `ui_document_viewer_template.png` — #42 document viewer template
- `ui_clue_type_icons_contact_sheet.png` — #43 clue icon contact sheet
- `manifest_draft.json`
- `animation_layers_draft.json`
- `editable_layers_draft.json`
- `contact_sheet.jpg`

## Text / Localization Notes

Important text is not intended to be read from the bitmap bases. Use the regions in `editable_layers_draft.json` for all clue names, dialogue, dates, document body text, archive metadata and Tatar vocabulary.

Tatar Cyrillic letters to keep in editable overlays:

`ә ө ү җ ң һ`

The generated vocabulary screen contains symbolic unknown/confirmed marks as UI icons; they should not carry linguistic meaning. If strict no-punctuation bases are required, those status marks should be repainted or covered in the integration pass.

## Animation Notes

Draft overlay slots are in `animation_layers_draft.json`:

- CRT glow
- scan noise
- cursor blink
- selected-row/result highlight
- document highlight
- re-read reveal
- key hover and dangerous-key warning
- vocabulary card confirm/unlock glow
- pressure vignette

These should stay subtle and state-driven. Do not run pressure animation constantly.

## Visual QA

Checked via `contact_sheet.jpg`:

- Style is aligned with muted ink-wash / paper grain direction.
- PC screens remain Win98-like without meme/cyberpunk treatment.
- Journal/dialogue/vocabulary screens read as paper-investigation systems.
- No full creature reveal or monster imagery.
- Main semantic text areas are blank or placeholder blocks for runtime overlays.

Dimension check passed:

- Seven UI screen finals are 2048x1152.
- Icon contact sheet final is 2048x1024.
