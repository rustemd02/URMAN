# Journal Pressure Worker Notes

## Output

- `ui_journal_route_sketch_pressure.png`
- `contact_sheet.jpg`
- `manifest_draft.json`
- `animation_layers_draft.json`
- `editable_layers_draft.json`

## Source And Method

- Built-in `image_gen` mode via the project imagegen skill.
- Visual reference: `public/assets/urman_route_map/ui_journal_route_sketch_base.png`.
- Generated file was copied into this worker scope, then resized to the required `2048x1152`.
- Original generated file remains under `/Users/unterlantas/.codex/generated_images/019e3b78-6f35-77c3-abb4-3ff8865edf7d/`.

## Prompt Summary

Open notebook/journal support view, same composition family as the base asset, with incomplete hand-drawn route sketch, unsafe crossed-out paths, clue pins, old place-name blank placeholders, and warning note regions. Style was constrained to ordinary detective journal paper, pencil/ink, muted watercolor/paper grain. Explicitly excluded full GPS map, complete village reveal, readable final text, monster symbols, glowing horror marks, blood, fantasy map-poster treatment, compass rose, and magical sigils.

## QA Notes

- Size: `2048x1152`.
- Alpha: none; RGB PNG.
- Baked readable labels: no intentional readable route/place/warning text; visible text areas are placeholder strokes.
- Canon/product fit: support view only, incomplete and clue-aware, no direct creature reveal.
- Minor caveat: the right book-edge page texture includes tiny incidental paper marks inherited by generation; they are not intended as route labels and should not be used as final text.

## Suggested Integration

- Register as `ui_journal_route_sketch_pressure` in the controller-owned route map manifest.
- Use editable regions from `editable_layers_draft.json` for runtime labels after Tatar/Russian proofing.
- Use optional reveal regions from `animation_layers_draft.json` only as subtle ink/paper reveals; avoid glow/pulse effects that would push the journal into supernatural UI.
