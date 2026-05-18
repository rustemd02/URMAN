# Transition Kit Worker Notes

Scope: `public/assets/urman_route_map/_incoming/transition_kit_worker/`

Generated with the built-in image generation path using flat `#ff00ff` chroma-key sources, then converted to alpha PNGs with:

```bash
python3 /Users/unterlantas/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py
```

Final PNGs were resized to `2048x1152` with alpha preserved.

## Delivered Assets

- `transition_step_forward_occlusion.png` - reusable 500-800 ms forward-step foreground occlusion. Dark blurred fence/grass/coat-edge forms, transparent center, no UI text.
- `transition_forest_pressure_edge.png` - rare darker Kara-Urman boundary paper-wash/branch-pressure overlay. No creature, eyes, person silhouette, or symbol reveal.
- `journal_route_sketch_update.png` - sparse journal route-line update overlay. Short ink route lines, node dots, pin/cross marks only; not a full map.
- `contact_sheet.jpg` - checkerboard QA contact sheet for quick alpha/readability review.
- `manifest_draft.json`, `animation_layers_draft.json`, `editable_layers_draft.json` - draft metadata for controller integration.

## QA Notes

- All final PNGs are `2048x1152`, RGBA.
- All final PNGs have alpha transparency.
- No root route-map manifests or docs were edited.
- Rejected generation outputs included a full journal map and route-screen-like forest frames; those were not copied into this worker folder.

## Source Prompts

### transition_step_forward_occlusion

Reusable foreground occlusion pass for a 500-800 ms step-forward movement. Blurred nearby fence slats, grass stems, roadside weeds, and a coat-edge crossing the frame. Transparent/chroma-key background, no UI text, no arrows, no creature, no eyes, no full map, no fake 3D camera rotation.

### transition_forest_pressure_edge

Special darker paper-wash transition toward Kara-Urman boundary. Heavy irregular dark edge wash, feathered ink blooms, dry-brush streaks, and narrow branch-like pressure marks. Transparent/chroma-key background, no creature, no eyes, no face, no silhouette person, no glowing symbols.

### journal_route_sketch_update

Transparent journal ink-line draw-on update overlay. A few short route strokes, small node dots, two or three simple pin/cross marks, and partial dashed lines. Mostly empty canvas, no readable words, no labels, no full village map.
