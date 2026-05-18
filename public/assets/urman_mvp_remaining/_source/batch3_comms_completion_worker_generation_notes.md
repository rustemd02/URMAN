# Batch 3 Comms Completion Worker Notes

## Scope

Generated missing bitmap bases for URMAN asset inventory rows #37 and #38.

Output folder only:

`public/assets/urman_mvp_remaining/_incoming/batch3_comms_completion_worker/`

The shared root manifest and project docs were not edited.

## References Read

- `AGENTS.md`
- `docs/urman_knowledge_base/asset_inventory_50.md` rows #37 and #38
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/asset_pack_first10/controlled_animation.md`
- `public/assets/urman_mvp_remaining/_incoming/batch3_old_pc_phone_worker/generation_notes.md`
- `public/assets/urman_mvp_remaining/_incoming/batch3_old_pc_phone_worker/manifest_draft.json`

## Generation Method

- Mode: built-in `image_gen` through the `imagegen` skill.
- Each requested asset was generated as a separate prompt.
- Raw imagegen outputs were copied from `$CODEX_HOME/generated_images/019e3b0e-5dd7-7103-be8e-433c810082bc/` into this batch folder.
- Generated 16:9 images were normalized from `1672x941` to `2048x1152`.
- All final PNG bases are RGB / non-transparent.

## Shared Prompt Anchor

All prompts used the locked URMAN anchor:

Ink-wash storybook game asset for URMAN, hand-drawn black ink line, muted watercolor wash, visible paper grain, grounded contemporary Tatar village details, ordinary first with subtle wrongness, no hyperrealism, no glossy 3D render, no pixel art, no neon, no gore, no obvious monster, no full creature reveal.

All prompts also required blank editable text placeholders, no real app branding, and animation-safe regions.

## Asset Prompt Summaries

- `ui_yalkyn_group_chat_base.png`: restrained old PC messenger group chat window with rural contact list, central group chat pane, blank chat rows, muted grey/turquoise Win98-like UI, CRT glow/scanline/cursor/window shimmer slots.
- `ui_yalkyn_dm_alsu_base.png`: old PC direct-message window with Alsu implied by layout only, cautious/helpful pacing through blank bubble rhythm, CRT glow/scanline/cursor/window shimmer slots.
- `ui_yalkyn_unread_danger_message.png`: old PC messenger unread warning state with contact list, subtle unread badge, blank notification toast/modal, low pressure, no heavy glitch, CRT glow/scanline/unread pulse/window shimmer slots.
- `ui_phone_mother_chat_base.png`: contemporary phone mother chat during bus/car arrival context, ordinary check-in layout, blank chat bubbles, no real app branding, phone screen glow slot only.
- `ui_phone_ticket_purchase_base.png`: contemporary phone ticket/trip purchase context, generic route/date/payment/button placeholders, no real logos, phone screen glow slot only.
- `ui_phone_translation_popup_base.png`: phone translation popup / Tatar word helper, clean centered editable popup, no baked readable vocabulary, phone screen glow slot only.

## QA Notes

- `contact_sheet.jpg` was created for visual review.
- Dimensions checked with `file`: all six PNG bases are `2048 x 1152`.
- The old PC assets match the neighboring batch direction: dusty CRT frame, muted grey/turquoise UI, domestic rather than meme-like.
- Phone assets are contemporary but softened into paper/ink wash, with bus/car or village approach context.
- Important text regions are blank placeholder bars/bubbles/cards and are captured in `editable_layers_draft.json`.
- Tatar letters are supported via editable metadata only; none were intentionally baked into the bitmap.
- Controlled animation is metadata-only in `animation_layers_draft.json`; no GIF/video files were generated.

## Partial Coverage / Risks

- Like the neighboring batch, generated UI chrome may include tiny pseudo-icons or micro-marks. They are not intended as story text. All meaningful names, timestamps, labels, messages, route fields, and vocabulary must be rendered by editable overlays.
- The generated old PC frames include the monitor bezel as part of the base. If the controller wants pure screen-only UI, crop masks may be needed during integration.
- The phone translation popup also covers part of a blurred background phone UI; use the popup editable regions as canonical and treat background lines as optional ambience.

## Files

- `ui_yalkyn_group_chat_base.png`
- `ui_yalkyn_dm_alsu_base.png`
- `ui_yalkyn_unread_danger_message.png`
- `ui_phone_mother_chat_base.png`
- `ui_phone_ticket_purchase_base.png`
- `ui_phone_translation_popup_base.png`
- `contact_sheet.jpg`
- `manifest_draft.json`
- `animation_layers_draft.json`
- `editable_layers_draft.json`
- `generation_notes.md`
