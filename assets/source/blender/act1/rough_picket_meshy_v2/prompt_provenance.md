# Rough-picket bay reference — 2026-10-19

- Artifact: `reference.png` (1672 × 941; PNG, landscape 16:9; SHA-256: `5d4e83fb30f1e6e3ce1c16f8749a4cc4db9996fcbad23395ba57eb90429c0bfa`).
- Purpose: one asset-level concept reference for a future metric rough-picket bay. The image is not an in-engine frame, exact metric proof, art acceptance, or a runtime asset.
- Generation: built-in `image_gen`, one call and one output. No Meshy credits, Blender, external service, geometry modeling, or additional variants.
- Reference inputs: `assets/source/blender/act1/rough_picket_meshy_v1/input-rough-picket-v1.png` for fence-family/material cues only; `evidence/ACT1-VILLAGE-COMPOSITION/yard-cleanup-2026-10-10/final/frames/C1_street_high.png` for current YardFences palette and in-world context; `evidence/ACT1-DEPTH.12/native_uv_v3_fdc5f5ef62fc4554ad107b9d9bef31ef/frames/babai_shape_front_high.png` for accepted material and winter-light style. Meshy proportions were explicitly excluded.
- Metric guidance: 2.35 m post centers, 1.18 m board tops, 1.29 m posts, 13 pickets at 95 × 28 mm with about 86 mm visible gaps, and two 45 × 75 mm rails at 0.27 m / 0.95 m. These are visual targets in the prompt; the bitmap does not verify dimensions.
- Assessment: the output reads as one straight grounded bay with thirteen separate pickets and two rails. Its wood detail and blurred forest background are more naturalistic than the accepted gameplay frame, so use it for layout/construction reference only; do not accept it as final game art.

## Prompt used

Create ONE landscape 16:9 asset-level ART REFERENCE for a single rough-picket fence bay in URMAN's painterly 3D winter village. It is concept art for later image-to-3D modeling, not an in-game screenshot and not proof of exact metric geometry. Treat the listed measurements as silhouette guidance only.

Reference roles:
- Image 1, the isolated rough-picket source overview: use only the identity of this timber fence family and its muted warm, aged wood/ochre material direction. Do not copy its incorrect board-to-gap proportions, oversized/irregular look, transparent/red edge artifacts, or its exact texture.
- Image 2, the current gameplay street frame: use the existing YardFences dark warm-brown timber palette and the game's winter daylight context. Do not bring its street, signs, people, buildings, or extra fence runs into this asset image.
- Image 3, the accepted babai house frame: match its tactile painterly 3D game-art language, softly shaped but believable wood, matte surfaces, restrained surface grain, pale snow, cool blue-gray winter daylight, and soft grounded shadows. Do not turn this into photorealism, glossy PBR, a showroom, or generic cinematic AAA art.

Subject and construction:
Show one straight complete fence span only, in a near-frontal view with a very slight three-quarter angle (enough to reveal real board, rail, and post thickness without strong perspective distortion). Keep the entire bay uncropped and large in frame.
Use exactly two modestly stout square end posts, about 120 mm square, standing firmly in the snow, with their tops about 1.29 m above the ground. Between their centers the span is about 2.35 m. Show exactly thirteen narrow, separate, pointed picket boards; each board is about 95 mm wide and 28 mm thick, with board crowns about 1.18 m above the ground. Their repeated clear gaps should read visually nearly as wide as the boards (about 86 mm); preserve clean, even daylight between every board.
Show exactly two substantial horizontal bearing rails, each about 45 by 75 mm, at about 0.27 m and 0.95 m above ground. The rails visibly bridge and attach to both posts and support the backs of the pickets. Make every member meet another convincingly: no floating parts, accidental crossings, or hidden supports. Keep the wood lightly weathered and softly aged; subtle faded warm-brown/ochre paint may remain, but avoid decorative trim and conspicuous hardware.
Ground the post feet directly into a small natural snow surface with readable contact and soft contact shadow; keep snow off the boards and joints so the construction remains fully legible. Use a quiet pale blue-gray winter background with soft depth, no competing objects. Show form, spacing, and joinery clearly enough that a modeler can reconstruct the bay.

Exclude text, labels, logo, UI, characters, monsters, buildings, gates, hinges, extra fence spans, diagonal braces, extra horizontal members, extra pickets, ornament, oversized bolts or plates, floating snow particles, glossy reflections, photographic realism, high-detail neural noise, and showroom staging.

## Assessment limitation

A single generated bitmap cannot prove member dimensions, gaps, joinery or runtime performance. The actual candidate still needs a 3D metric check and a native-game frame before acceptance.
