# Batch 3 Old PC / Messenger / Phone Worker Notes

## Scope

Generated bitmap bases for URMAN asset inventory refs #33, #34, #37 and #38.

Output folder only:

`public/assets/urman_mvp_remaining/_incoming/batch3_old_pc_phone_worker/`

The shared `public/assets/urman_mvp_remaining/asset_manifest.json` was not edited.

## References Read

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `public/assets/urman_mvp_first10/ui_old_pc_desktop_base.png`
- existing `loc_mansur_pc_room_*` images in `public/assets/urman_mvp_remaining/`

## Generation Method

- Mode: built-in `image_gen` through the `imagegen` skill.
- Each requested asset was generated as a separate image prompt.
- Raw imagegen output was copied from `$CODEX_HOME/generated_images/...` into this batch folder.
- Generated 16:9 images were normalized to `2048x1152`.
- `ui_old_pc_crt_close_crop.png` was normalized to `1536x1536`.

## Style Constraints Applied

- Ink-wash storybook, hand-drawn black ink line, muted watercolor wash, visible paper grain.
- Old PC UI: Win98-like but not meme-like, muted CRT, faded grey plastic, grey/turquoise UI.
- No cyberpunk, no neon, no glossy 3D render, no pixel art, no gore, no obvious monster.
- Important story/UI text left blank for editable overlays.

## QA Notes

- `contact_sheet.jpg` was created for visual review.
- File dimensions checked with `file`.
- Most UI text regions are blank rectangles, bubbles, or non-content placeholder bars.
- A few tiny generated toolbar/key micro-marks remain in old PC UI chrome and keyboard areas. They are not intended as story text; all meaningful text should be rendered by editable overlays listed in `editable_layers_draft.json`.
- No shared manifest or root knowledge-base files were changed.

## Files

- `ui_old_pc_crt_off.png`
- `ui_old_pc_crt_boot.png`
- `ui_old_pc_crt_power_off.png`
- `ui_old_pc_crt_close_crop.png`
- `ui_old_pc_folder_view_base.png`
- `ui_old_pc_active_window_base.png`
- `ui_old_pc_error_corrupt_file_base.png`
- `ui_yalkyn_messenger_contacts_base.png`
- `ui_yalkyn_messenger_saved_marat_log_base.png`
- `ui_phone_aidar_intro_chat_base.png`
- `manifest_draft.json`
- `animation_layers_draft.json`
- `editable_layers_draft.json`
- `generation_notes.md`
- `contact_sheet.jpg`
