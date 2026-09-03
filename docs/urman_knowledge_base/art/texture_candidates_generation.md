# Painterly texture candidates — ImageGen v2 manifest

Статус: production-candidate pass, 2026-08-11. Эти six textures are non-destructive siblings of the existing `*_albedo.png` files; they are not art-lock acceptance or runtime activation. Per-file provenance is now mirrored in `assets/asset_registry.json`; the v1 material mapping remains active.

## Generation and normalization

- Mode: built-in `image_gen` (the installed ImageGen skill), one call per material. No CLI/API fallback and no hand-authored noise.
- The built-in output for this pass was a 1 254 × 1 254 RGB PNG. Each selected source was copied into `game/assets/textures/painterly/` and normalized with `sips -z 1024 1024` to the required final 1 024 × 1 024 RGB PNG. No existing texture was overwritten.
- Prompts below are the exact prompts sent to ImageGen. `ImageGen source` is the original generated file before the workspace copy/resize. The raw first old-fabric attempt was rejected for overly photographic macro weave and is not part of the deliverables; the second prompt/path below is the selected candidate.

## Candidate index

| Final file | ImageGen source | Final size / format | SHA-256 | Selection reason |
| --- | --- | --- | --- | --- |
| `game/assets/textures/painterly/weathered_wood_boards_v2_albedo.png` | `/Users/unterlantas/.codex/generated_images/019ff211-14bc-7272-986b-504f2e36fd4e/exec-5eea9e6e-cbc7-4f8f-82db-e300281cab05.png` | 1024×1024 RGB PNG | `03499969fcb1cb172a3d6b4b11bb710c65daa159e4c9cb24de11f4a1c3c1c9bf` | Horizontal broad planks, quiet knots and faded ochre/moss breakup read clearly as the existing village wood language while remaining ordinary rather than horror-coded. |
| `game/assets/textures/painterly/aged_plaster_v2_albedo.png` | `/Users/unterlantas/.codex/generated_images/019ff211-14bc-7272-986b-504f2e36fd4e/exec-43925dfb-9ee4-464b-a44b-e9003e7c2e2f.png` | 1024×1024 RGB PNG | `a88975b10aa0d7070f868408366309c2b4100fd5eb1defd9fb08fe3281083416` | Pale limewash, restrained hairline cracks and warm underlayers fit the domestic paper/ochre palette without turning the wall into a focal object. |
| `game/assets/textures/painterly/damp_earth_v2_albedo.png` | `/Users/unterlantas/.codex/generated_images/019ff211-14bc-7272-986b-504f2e36fd4e/exec-7d5db022-fd5e-4b41-8626-a5dc8344d827.png` | 1024×1024 RGB PNG | `02f356c591d52fdde31992f6064fbb3b10ec6928e856bfbb0315a687c24aeb56` | Broad muddy masses, shallow rut direction, small slate pebbles and muted moss give readable first-person ground variation without a composed road scene. |
| `game/assets/textures/painterly/pine_foliage_v2_albedo.png` | `/Users/unterlantas/.codex/generated_images/019ff211-14bc-7272-986b-504f2e36fd4e/exec-90d203f0-2eba-4a94-8485-9821088448a9.png` | 1024×1024 RGB PNG | `3838784bdcf4d3fd31f6bf6bff93ef5906452cebe3df1d6b86075b42888e8ff6` | Varied hand-painted needle clusters and deep blue-green gaps match the conifer silhouette language and avoid a single recognizable branch or object. |
| `game/assets/textures/painterly/mossy_stone_v2_albedo.png` | `/Users/unterlantas/.codex/generated_images/019ff211-14bc-7272-986b-504f2e36fd4e/exec-26a528ae-7f93-44e4-9be8-456774bf3d16.png` | 1024×1024 RGB PNG | `38b0e69f32a4e3e859921229cab0ea7bf12426dac9b8e150683b7607b5277b15` | Closely packed low-poly rounded stones with quiet moss in joints provide a reusable boundary/foundation surface; no hero rock or decorative symbol. |
| `game/assets/textures/painterly/old_fabric_v2_albedo.png` | `/Users/unterlantas/.codex/generated_images/019ff211-14bc-7272-986b-504f2e36fd4e/exec-73d1fdfc-907b-4f1a-8eee-98efecf0439a.png` | 1024×1024 RGB PNG | `f1c646785cddb876510f81be2400c6c2da01764b1fceb5d2bb8e92f0186d9132` | Selected second pass uses broad grouped brush bands and faded blue-gray/cream wear; the dense macro-weave first attempt was discarded to keep medium-frequency first-person readability. |

## Exact prompts

### `weathered_wood_boards_v2_albedo.png`

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable material swatch of weathered wooden boards for an old rural house in Kырлай, Татарстан. Show only the surface, viewed straight-on orthographic and evenly framed edge-to-edge. Boards run horizontally in irregular broad planks with subtle seams, softened chipped edges, a few quiet knots and hand-painted grain. It must tile perfectly on all four edges with no visible seam or mirrored repetition.
Scene/backdrop: flat material study, no scene or context.
Subject: old exterior wood board surface only.
Style/medium: painterly low-poly 3D production texture, hand-painted albedo, restrained ink-wash and watercolor breakup, readable medium-frequency variation, not a rendered object.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no floor plane, no border.
Lighting/mood: neutral diffuse studio light; matte albedo with no cast shadows, no specular highlights and no dramatic vignette.
Color palette: muted gray-brown, weathered umber, faded ochre, old-lacquer taupe, tiny moss-green accents; grounded in the project's paper-and-wet-village palette.
Materials/textures: broad plank direction, sparse knots, dry peeling paint and softened grain; detail readable at first-person distance without micro-noise.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; preserve ordinary village material before any wrongness.
Avoid: objects, furniture, nails as focal objects, signs, ornaments, people, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, cartoon/fantasy, neon, horror symbols, blood, skulls, black voids, glossy plastic, visible repetition, non-tileable edges.
```

### `aged_plaster_v2_albedo.png`

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable material swatch of aged plaster on an old rural house wall in Кырлай, Татарстан. Show only the surface, viewed straight-on orthographic and evenly framed edge-to-edge. A pale limewashed plaster field with soft hand-troweled patches, faint hairline cracks, rubbed corners implied only as subtle tonal variation, and a few warm underlayers; keep the texture quiet and usable across a wall. It must tile perfectly on all four edges with no visible seam or mirrored repetition.
Scene/backdrop: flat material study, no scene or context.
Subject: aged plaster wall surface only.
Style/medium: painterly low-poly 3D production texture, hand-painted albedo, restrained ink-wash and watercolor breakup, readable medium-frequency variation, not a rendered object.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no floor plane, no border.
Lighting/mood: neutral diffuse studio light; matte albedo with no cast shadows, no specular highlights and no dramatic vignette.
Color palette: faded cream, warm gray, desaturated straw-ochre, pale clay and tiny muted olive traces; old paper and domestic village tones.
Materials/textures: chalky plaster, subtle trowel strokes, sparse fine cracks and uneven wash, no high-frequency photographic grit; keep variation readable at first-person distance.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary household material, not spooky.
Avoid: objects, bricks, wallpaper patterns, trim, windows, doors, signs, ornaments, people, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, cartoon/fantasy, neon, horror symbols, blood, skulls, glossy paint, deep shadows, visible repetition, non-tileable edges.
```

### `damp_earth_v2_albedo.png`

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable material swatch of damp earth and compacted muddy soil from a rural village road in Кырлай, Татарстан. Show only the surface, viewed straight-on orthographic and evenly framed edge-to-edge. Broad painterly soil masses with shallow soft ruts, scattered small rounded pebbles, muted wet patches and sparse mossy traces; medium-frequency variation that reads as ground at first-person scale without individual objects. It must tile perfectly on all four edges with no visible seam or mirrored repetition.
Scene/backdrop: flat material study, no scene or context.
Subject: damp earth road surface only.
Style/medium: painterly low-poly 3D production texture, hand-painted albedo, restrained ink-wash and watercolor breakup, readable medium-frequency variation, not a rendered landscape.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no road vanishing point, no border.
Lighting/mood: neutral diffuse studio light; matte albedo with subtle broad wet-value variation but no cast shadows, no specular glare and no dramatic vignette.
Color palette: muted umber and gray-brown, cool slate pebbles, desaturated olive moss, tiny blue-gray damp notes; grounded, autumnal and rural.
Materials/textures: compacted clay, soft granular earth, a few painted pebble shapes and shallow moisture stains; avoid fussy photographic grit.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary village road material, no horror cues.
Avoid: tire tracks as a single composition, puddles with hard reflections, large rocks, plants, leaves, branches, objects, signs, footprints, people, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, cartoon/fantasy, neon, horror symbols, blood, skulls, glossy wet mirror, visible repetition, non-tileable edges.
```

### `pine_foliage_v2_albedo.png`

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable material swatch of dense pine foliage for low-poly conifer branches at the Kara-Urman forest edge near Кырлай, Татарстан. Show only overlapping clusters of short, hand-painted needle sprays and soft branch masses viewed straight-on orthographic, filling the frame edge-to-edge. Use varied cluster scale and direction so it reads as a reusable foliage material rather than a single branch illustration. It must tile perfectly on all four edges with no visible seam or mirrored repetition.
Scene/backdrop: flat material study, no scene or context.
Subject: pine needle foliage surface only.
Style/medium: painterly low-poly 3D production texture, hand-painted albedo, restrained ink-wash and watercolor breakup, readable medium-frequency variation, simplified faceted brush shapes rather than photographic needles.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no branch silhouette, no border.
Lighting/mood: neutral diffuse light; matte foliage with broad value grouping and gentle painterly variation, no cast shadows, no glossy highlights.
Color palette: muted spruce green, deep pine green, blue-green shadow, subdued moss and a few desaturated olive tips; natural and quiet, never neon.
Materials/textures: layered brush strokes suggesting needle tufts, soft dark gaps between clusters, sparse dry-tip variation; enough contrast for first-person readability without fine noise.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary forest material before any wrongness.
Avoid: a single recognizable branch, pinecones, berries, flowers, trunks, insects, animals, objects, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, cartoon/fantasy, neon, horror symbols, blood, skulls, glowing eyes, visible repetition, non-tileable edges.
```

### `mossy_stone_v2_albedo.png`

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable material swatch of mossy stone for low boundary walls, old foundations and path stones in Кырлай, Татарстан. Show only an even field of closely packed irregular rounded stones, viewed straight-on orthographic and framed edge-to-edge. Each stone is low-poly readable with softly painted planes, shallow joints and restrained damp moss in crevices; no single hero rock. It must tile perfectly on all four edges with no visible seam or mirrored repetition.
Scene/backdrop: flat material study, no scene or context.
Subject: mossy stone surface only.
Style/medium: painterly low-poly 3D production texture, hand-painted albedo, restrained ink-wash and watercolor breakup, readable medium-frequency variation, simplified faceted planes instead of photorealistic geology.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no wall silhouette, no border.
Lighting/mood: neutral diffuse studio light; matte stone with gentle broad value variation and soft crevice darkening, no cast shadows, no glossy highlights.
Color palette: cool gray, slate blue-gray, damp brown-gray, desaturated moss olive and muted lichen green; subdued wet-village colors.
Materials/textures: rounded fieldstone shapes, shallow mortar gaps, painterly chips and moss flecks; enough separation at first-person distance without high-frequency grit.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary old stone before any wrongness.
Avoid: one large boulder, masonry wall silhouette, bricks, decorative patterns, gravel pile, plants, flowers, roots, leaves, objects, signs, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, cartoon/fantasy, neon, horror symbols, blood, skulls, glowing marks, deep black gaps, visible repetition, non-tileable edges.
```

### `old_fabric_v2_albedo.png`

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable painterly albedo swatch of a well-used plain woven household fabric from a rural Tatar village. Only the flat material fills the frame in a straight-on orthographic view. Make the weave readable through broad grouped brush marks and softly broken warp/weft bands, not through a dense photographic macro grid. Include faded dye, rubbed nap and a few quiet watercolor wear patches, with varied broad areas so the material reads at first-person distance. The four edges must tile perfectly with no visible seam or mirrored repeat.
Scene/backdrop: flat material study, no scene or context.
Subject: aged plain textile surface only.
Style/medium: painterly low-poly 3D production albedo, hand-painted ink-wash and watercolor breakup, stylized brush texture, medium-frequency detail.
Composition/framing: full-frame orthographic material swatch, no perspective, no garment or furniture silhouette, no border.
Lighting/mood: neutral diffuse matte lighting, soft broad value shifts, no cast shadow and no glossy highlight.
Color palette: muted blue-gray, faded warm gray, dusty cream, restrained ochre-rust and tiny olive traces; quiet old-house palette.
Materials/textures: broad soft warp/weft strokes, worn nap, faded patches and slight thread-direction drift; no tiny regular checker pattern.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; ordinary domestic material; no decorative cultural motif required.
Avoid: photorealistic macro weave, dense regular grid, plaid, stripes, embroidery, lace, fringe, buttons, seams, clothing folds, furniture, objects, plants, people, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, cartoon/fantasy, neon, horror symbols, blood, skulls, shiny satin, visible repetition, non-tileable edges.
```

## Production candidate v3 (not active at runtime)

### `pine_foliage_v3_albedo.png`

- **Final file:** `game/assets/textures/painterly/pine_foliage_v3_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-434bb17a-07d8-455f-b18d-2676474ee651.png`
- **Generation mode:** built-in `image_gen`, one new source image, 2026-08-13; no input/reference image and no CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG. Existing v1/v2 textures were not overwritten.
- **SHA-256 (source):** `8b796fb04a410a8379c25ca6fa173d07c8c3a7cfef564c9346e807cb4d5dba2a`
- **SHA-256 (final):** `05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830`
- **Selection note:** broad layered needle clusters and deep blue-green gaps retain three-to-four quiet value families under night grading. It is a candidate for the Kara-Urman edge only; forest silhouette, fog separation, triplanar repetition and cultural review still require in-engine evidence.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a first-person Godot triplanar village environment.
Create one square seamless tileable hand-painted albedo texture of muted pine foliage / conifer needles for the edge of Kara-Urman forest in Kyrlay, Tatarstan. Painterly Low-Poly 3D, not a photograph. Show 4–6 broad irregular layered needle clusters and sparse flat branch masses, organized into 3–4 large desaturated value families so the material remains readable at 128x128 and through blue-gray night fog. Keep the tile calm and evenly distributed, no diagonal landscape composition, no focal branch, no border, no obvious grid or mirrored repetition. Subject is foliage surface only on a flat orthographic swatch, no tree, no forest scene, no props. Neutral diffuse matte albedo with no embedded shadows, ambient occlusion, highlights, gloss, or vignette. Palette: deep blue-green, muted spruce, charcoal teal, restrained gray-olive; low saturation, natural and damp. Use broad faceted brush planes and watercolor/dry-brush breakup, with restrained medium-frequency variation and no photographic micro-needles. Clean seamless edges on all four sides. Avoid pixel art, photorealism, cartoon/fantasy, heavy outlines, VHS, chromatic aberration, neon, bright greens, red, blood, horror symbols, text, watermark, logo, labels, noise, dense microdetail, hard cast shadows, glossy specular, visible seams, or non-tileable edges. Output: a single square seamless tileable albedo swatch only.
```

### `aged_plaster_v3_albedo.png`

- **Final file:** `game/assets/textures/painterly/aged_plaster_v3_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019ff634-61da-7170-8090-ee3ff73b0b5e/exec-19919973-1019-46dd-854b-235c5a9bf070.png`
- **Generation mode:** built-in `image_gen`, one new source image, 2026-08-12; no input/reference image and no CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized non-destructively with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG.
- **SHA-256 (final):** `bb5f6c13d1947ef5616e9dac49ff37b41e6ff323901cc0b44b783212ac21454e`
- **Selection note:** broad, chalky limewash and quiet trowel breakup read as a painterly domestic wall at first-person scale. Sparse warm clay/olive age marks differentiate it from v2 without adding props, symbols, deep shadows, horror cues or photo-like grit. Seamlessness still requires in-engine tiling review before any material activation.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square seamless tileable painterly albedo material swatch of aged limewashed plaster on an old rural house wall in Кырлай, Татарстан. Show only the flat wall surface, straight-on orthographic and evenly framed edge-to-edge. A quiet faded cream plaster field with broad hand-troweled washes, softly broken limewash, a few sparse hairline cracks, restrained rubbed patches, tiny traces of pale clay and muted olive aging. Favor medium-scale hand-painted watercolor and ink-wash breakup that reads at first-person distance, with no photographic grit. The four edges must tile perfectly with no visible seam, border, or mirrored repeat.
Scene/backdrop: flat material study, no scene or context.
Subject: aged plaster wall surface only.
Style/medium: painterly low-poly 3D production texture; hand-painted albedo; restrained ink-wash and watercolor breakup; readable medium-frequency variation, not a rendered object.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no floor plane, no border.
Lighting/mood: neutral diffuse studio light; matte albedo with no cast shadows, no specular highlights, no dramatic vignette.
Color palette: faded cream, warm gray, pale straw-ochre, muted clay and tiny restrained olive traces; ordinary domestic village tones, compatible with the project's paper-and-wet-village palette.
Materials/textures: chalky plaster, broad trowel strokes, subtle uneven limewash and sparse fine cracks; deliberately quiet across a wall.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary household material before any wrongness.
Avoid: objects, bricks, wallpaper patterns, trim, windows, doors, signs, ornaments, people, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, dense micro-noise, cartoon/fantasy, neon, horror symbols, blood, skulls, glossy paint, deep shadows, visible repetition, non-tileable edges.
```

### `old_fabric_v3_albedo.png`

- **Final file:** `game/assets/textures/painterly/old_fabric_v3_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019ff634-61da-7170-8090-ee3ff73b0b5e/exec-ac6ccfc9-a408-40bc-9b31-f22b19f4c606.png`
- **Generation mode:** built-in `image_gen`, one new source image, 2026-08-13; no input/reference image and no CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized non-destructively with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG. Existing v1/v2 textures were not overwritten.
- **SHA-256 (source):** `436b805c0ae136b7861ce6bf0063333f2a86992408ec7da3c7e2323bc8c70cef`
- **SHA-256 (final):** `c19f22b7b5f2e9b4f8f5460e407c964d0ca9395022b4f2ef93d38722a0e3da82`
- **Selection note:** broad, broken blue-gray/cream woven brush bands and sparse old-ochre wear replace v2's dense macro-grid with calm medium-scale fabric variation. It remains a non-active production candidate; edge continuity, 2×2 repetition and triplanar readability require in-engine review before any material activation.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create one square seamless tileable hand-painted albedo texture of well-used plain woven household fabric for rugs, curtains and cloth in an old rural home in Кырлай, Татарстан. The entire frame is only flat fabric surface, viewed straight-on in orthographic projection. Make the weave read through 5–7 broad irregular grouped woven brush bands with soft broken warp/weft direction and muted faded-dye patches, not through a dense macro textile grid. Organize the material in 3–4 calm desaturated value families; leave broad quiet areas so it remains legible at first-person distance and at 128x128. The four edges must tile perfectly without a visible seam, grid, border, focal stripe, or mirrored repeat.
Scene/backdrop: flat material study, no scene or context.
Subject: aged plain household fabric surface only.
Style/medium: Painterly Low-Poly 3D production albedo; hand-painted ink-wash, watercolor and dry-brush breakup; restrained medium-frequency detail, not a photograph and not a rendered cloth object.
Composition/framing: full-frame orthographic material swatch, no perspective, no garment or furniture silhouette, no folds, no border.
Lighting/mood: neutral diffuse matte albedo with soft broad value shifts; no embedded ambient occlusion, cast shadow, specular highlight, gloss or vignette.
Color palette: muted blue-gray, faded warm gray, dusty cream and restrained old ochre; subdued domestic village palette.
Materials/textures: wide grouped woven brush marks, rubbed nap and a few quiet watercolor wear patches; no tiny regular checker pattern.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary domestic material before any wrongness.
Avoid: photorealistic macro weave, dense regular grid, pixel art, plaid, stripes, embroidery, decorative motifs, lace, fringe, buttons, seams, clothing folds, furniture, objects, plants, people, symbols, text, watermark, logos, outlines, borders, UV test labels, cartoon/fantasy, neon, horror symbols, blood, skulls, shiny satin, deep shadows, visible repetition, non-tileable edges.
```

### `damp_earth_v3_albedo.png`

- **Final file:** `game/assets/textures/painterly/damp_earth_v3_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-a943a587-754f-4bdf-8881-59af51cf9a6e.png`
- **Generation mode:** built-in `image_gen`, one new source image, 2026-08-12; no input/reference image and no CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG copied to a temporary workspace path and resized with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG. No existing texture was overwritten.
- **SHA-256 (source):** `4f665583807eaae536d0a4c966a240764ef01b3fd42642082e2b1ef3717882d9`
- **SHA-256 (final):** `cd031be6ff3fbb2750a05abe6b667d23bba2ad3705b6f5a14755d21a33b77b20`
- **Selection note:** this pass replaces the v2 material's photographic granular/diagonal read with broad compacted-clay value masses, two quiet broken rut families and sparse flat pebble marks. It remains a candidate only: no runtime owner switch, and near/mid/far traversal plus 20–30 m repetition review are still required.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create a square 1024x1024 seamless tileable material swatch of damp compacted earth for a rural village road in Кырлай, Татарстан. Show only the flat surface, viewed straight-on orthographic and evenly framed edge-to-edge. Replace photographic granular mud with 6–10 broad hand-painted clay and watercolor masses and two gently broken lengthwise rut bands. Add broad muted pooling and a very sparse handful of small flat slate pebble silhouettes. Preserve strong medium-scale value grouping when reduced to 128x128 and after warm in-engine grading; the tile must read as damp earth from a first-person camera rather than as a composed landscape.
Scene/backdrop: flat material study, no scene or context.
Subject: compacted damp earth road surface only.
Style/medium: painterly low-poly 3D production texture, hand-painted albedo, restrained ink-wash and watercolor breakup, readable medium-frequency variation, not a rendered object.
Composition/framing: full-frame orthographic material swatch; no perspective, no horizon, no vanishing-point road composition, no border.
Lighting/mood: neutral diffuse studio light; matte albedo with broad value variation, no embedded ambient occlusion, cast shadows, specular reflections or dramatic vignette.
Color palette: muted umber, cool gray-brown, taupe, slate blue-gray and restrained olive; grounded, damp and autumnal, never bright.
Materials/textures: broad irregular clay masses, two subtle broken rut families, watercolor pooling and sparse flat pebble marks; no photographic micro-grit and no high-frequency shimmer.
Text (verbatim): ""
Constraints: seamless tileable albedo only; exactly square 1024x1024 PNG output; consistent lighting; ordinary village road material before any wrongness.
Avoid: diagonal terrain composition, a single continuous tire track, deep puddles, hard reflections, embedded AO, large rocks, plants, leaves, branches, objects, signs, footprints, people, symbols, text, watermark, logos, outlines, borders, grid, UV test labels, photorealism, cartoon/fantasy, neon, horror symbols, blood, skulls, glossy wet mirror, visible repetition, non-tileable edges.
```

### `weathered_wood_boards_v3_albedo.png`

- **Final file:** `game/assets/textures/painterly/weathered_wood_boards_v3_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019ffb03-5c54-7630-b47f-6910b0ac2d8f/exec-1122c0bd-e98e-446c-b8b3-5cf56979e715.png`
- **Generation mode:** built-in `image_gen`, one new source image, 2026-08-13; no input/reference image and no CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized non-destructively with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG. Existing v1/v2 textures were not overwritten.
- **SHA-256 (source):** `d1a4915598a414c6064149bc661fb526534cfdbd5eecaa784dce2c66af4e5933`
- **SHA-256 (final):** `65372413ff490da9dce831fd5f4022d6f804265b42dd9895ad4b13a6fa196093`
- **Selection note:** selected because six broad irregular rows, three quiet knots and 2–3 large gray-brown/taupe value families replace the v1/v2 photographic grain and flaking with calm painterly low-poly readability. The deterministic pixel gate passes, including opposite-edge seam mean `0.0081/0.0653` and seam max `0.1333/0.1412` (vertical/horizontal). It remains a production candidate only: a repeated 2×2 swatch and near/mid/far in-engine review may still reveal broad brush-mass repetition or a horizontal tonal step, so no runtime activation or art lock is implied.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create one square seamless tileable hand-painted albedo texture of weathered wooden boards for an old rural house in Кырлай, Татарстан. The material must read immediately as Painterly Low-Poly 3D, not as a photograph. Show exactly 6 broad, slightly irregular horizontal plank rows spanning the full tile. Organize all color and wear into only 2–3 large value families with broad calm brush masses. Keep the plank seams narrow, soft, low-contrast and shallow. Include no more than 3 small quiet knots across the entire tile. The four edges must tile cleanly without a visible seam, border, offset break or mirrored repetition.
Scene/backdrop: flat material study only; no scene, props or context.
Subject: weathered exterior wooden board surface only.
Style/medium: Painterly Low-Poly 3D production texture; simplified hand-painted albedo; broad faceted watercolor and dry-brush shapes; deliberately restrained medium-frequency detail.
Composition/framing: full-frame straight-on orthographic surface swatch, evenly framed edge-to-edge; no perspective, horizon, floor plane or border.
Lighting/mood: neutral diffuse matte albedo; ordinary village material before any wrongness; no embedded shadows, ambient occlusion, highlights or vignette.
Color palette: muted gray-brown, old-lacquer taupe, desaturated umber and restrained faded ochre; at most a tiny quiet olive-gray trace; no bright colors.
Materials/textures: broad plank-scale wear shapes and a few simplified grain sweeps; softened painted edges; preserve clarity when reduced to 128x128.
Text (verbatim): ""
Constraints: seamless tileable square albedo only; exactly 6 broad irregular horizontal board rows; 2–3 large value families; low-contrast seams; maximum 3 quiet knots; clean consistent lighting; no focal feature.
Avoid: photorealism, photographic wood grain, microdetail, micro-noise, fine splinters, chips, peeling flakes, dense scratches, sharp cracks, high-contrast grooves, deep seams, many knots, nails, screws, objects, furniture, plants, signs, ornaments, symbols, text, watermark, logos, outlines, ink contours, borders, grids, UV labels, VHS, scanlines, pixel art, neon, saturated colors, horror red, blood, skulls, glossy varnish, cast shadows, visible repetition, non-tileable edges.
```

### `mossy_stone_v3_albedo.png`

- **Final file:** `game/assets/textures/painterly/mossy_stone_v3_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019ffb03-5c54-7630-b47f-6910b0ac2d8f/exec-0d6c28cf-228f-4308-89cf-693b417e2d93.png`
- **Generation mode:** built-in `image_gen`, one new source image, 2026-08-13; no input/reference image and no CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized non-destructively with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG. Existing v2 and other v3 textures were not overwritten.
- **SHA-256 (source):** `f33ac699bacb58e18f2df353bf4ecdcefb4b2678841000b169ff892972579d20`
- **SHA-256 (final):** `4444dc8585aada9dfd85601b8b32c96b0af3aa385d1d96e8ae88d24dfed66024`
- **Selection note:** about 20 broad rounded stones, simplified faceted brush planes and three-to-four quiet gray/taupe value families replace the v2 candidate's dense small-stone field and photographic surface breakup. Muted moss remains subordinate to the stone masses and there is no hero rock, regular masonry row, fantasy mark or horror cue. The deterministic pixel gate passes, including opposite-edge seam mean `0.0158/0.0166` and seam max `0.1725/0.2314` (vertical/horizontal). It remains a production candidate only: repeated 2×2 and in-engine near/mid/far review may still reveal recognizable large-stone repetition, edge shape discontinuities or moss paths joining into a visible network, so no runtime activation or art lock is implied.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create one square seamless tileable hand-painted albedo texture of old mossy fieldstone for low boundary walls and house foundations in Кырлай, Татарстан. It must read immediately as Painterly Low-Poly 3D, never as photographic geology. Fill the tile edge-to-edge with about 16–22 broad irregular rounded stones, using varied large and medium sizes and an organic interlocking layout. Simplify every stone into 3–5 softly painted faceted planes. Organize the complete tile into only 3–4 large quiet value families. Keep joints shallow, narrow, low-contrast and irregular. Add sparse muted moss only in a few short disconnected joint sections, never as a continuous green network. No single hero stone, repeated motif, row pattern or grid. The four edges must tile cleanly without a visible seam, border, offset break or mirrored repetition.
Scene/backdrop: flat material study only; no wall silhouette, landscape, props or context.
Subject: old rounded fieldstone surface only.
Style/medium: Painterly Low-Poly 3D production texture; simplified hand-painted albedo; broad faceted watercolor and dry-brush shapes; restrained medium-frequency detail.
Composition/framing: full-frame straight-on orthographic surface swatch, evenly framed edge-to-edge; no perspective, horizon, floor plane or border.
Lighting/mood: neutral diffuse matte albedo; ordinary damp village material before any wrongness; no embedded cast shadows, ambient occlusion, highlights, gloss or vignette.
Color palette: cool gray, muted slate blue-gray, damp brown-gray, subdued taupe and sparse desaturated moss olive; calm low saturation, no bright colors.
Materials/textures: broad rounded stone silhouettes, gently broken painted planes and a few quiet moss touches in joints; preserve clear low-poly value grouping when reduced to 128x128.
Text (verbatim): ""
Constraints: seamless tileable square albedo only; about 16–22 broad stones; 3–4 large value families; shallow low-contrast joints; sparse disconnected muted moss; consistent neutral lighting; no focal feature.
Avoid: photorealism, photographic geology, realistic rock scans, microdetail, micro-noise, grain, gravel, pebble field, hundreds of small stones, dense speckles, sharp cracks, deep black crevices, continuous moss web, bright green moss, regular rows, masonry grid, hexagonal pattern, tiled mosaic, bricks, mortar grid, one large boulder, plants, flowers, roots, leaves, objects, signs, ornaments, symbols, text, watermark, logos, outlines, ink contours, borders, grids, UV labels, VHS, scanlines, pixel art, cartoon fantasy, magical runes, glowing marks, neon, saturated colors, horror symbols, blood, skulls, bones, cast shadows, glossy wet reflections, visible repetition, non-tileable edges.
```

## Production candidate v4 (focused, not active at runtime)

V4 is a two-material comparison pass prompted by the art review. It targets
the road's damp-earth value grouping and the shared wood owner used by houses,
fences and furniture. Existing v1, v2 and v3 files were not overwritten; v4 is
substituted only by the dedicated test harness.

### `damp_earth_v4_albedo.png`

- **Final file:** `game/assets/textures/painterly/damp_earth_v4_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-ac87dae7-c807-4393-a908-03e7f43d49c1.png`
- **Generation mode:** built-in ImageGen, one new source image, 2026-08-14; no reference image or CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG.
- **SHA-256 (source):** `df02a3242328bf4d223b692067c2fd95bfd681bbbf40bd902b274b509386f163`
- **SHA-256 (final):** `f1ed53e8b40f94fc45a2e8e43816e5c77e1717da0be09a60dcc68045da1bad31`
- **Selection note:** broad irregular clay masses, broken lengthwise damp bands and sparse flat pebbles survive first-person reduction better than a photographic-grit treatment. In-engine review still must judge repetition and wetness/roughness separately.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create one square seamless tileable hand-painted albedo texture of damp compacted earth for a rural Tatar village road near Kyrlay, Tatarstan. This is a focused v4 alternative to an existing v3 candidate: simplify the surface into 6–10 very broad irregular clay-and-watercolor value masses and two gently broken lengthwise rut families that remain unmistakable after reduction to 128x128. Keep the pattern evenly distributed for a reusable tile, never a composed road scene.
Scene/backdrop: flat material study only, straight-on orthographic, edge-to-edge surface swatch.
Subject: compacted damp earth road material only; sparse flat pebble silhouettes and quiet darker moisture pooling are allowed.
Style/medium: Painterly Low-Poly 3D, hand-painted albedo, broad faceted brush planes, restrained watercolor/ink-wash breakup; no photographic grit.
Composition/framing: uniform orthographic material tile, no diagonal or vanishing-point composition, no focal rut, no horizon, no border.
Lighting/mood: neutral matte diffuse studio light; broad value grouping; no embedded AO, cast shadows, specular reflections, gloss or vignette.
Color palette: muted umber, gray-brown, taupe, slate blue-gray and restrained olive; damp autumnal rural tones, low saturation.
Materials/textures: large clay masses, broken rut bands, sparse small flat pebbles, soft broad damp stains; no high-frequency shimmer.
Constraints: seamless on all four edges, exactly square 1024x1024 source, reusable triplanar albedo, medium-scale grouping must survive warm in-engine grading and first-person distance.
Avoid: photorealism, pixel art, cartoon/fantasy, heavy outlines, VHS, chromatic aberration, neon, bright colors, horror symbols, blood, skulls, plants, leaves, branches, objects, signs, footprints, people, text, watermark, logos, borders, grid, UV labels, hard reflections, large rocks, dense microdetail, visible seams, non-tileable edges.
```

### `weathered_wood_boards_v4_albedo.png`

- **Final file:** `game/assets/textures/painterly/weathered_wood_boards_v4_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-b0d67670-e26b-4823-8ea7-7aadb05ee064.png`
- **Generation mode:** built-in ImageGen, one new source image, 2026-08-14; no reference image or CLI/API fallback.
- **Normalization:** source `1254×1254` RGB PNG resized with `sips -z 1024 1024 -s format png` to final `1024×1024` RGB PNG.
- **SHA-256 (source):** `ae2523b334e9daec292cbb0b7a418dc9a7c1bb5371be1929c106260a325c3964`
- **SHA-256 (final):** `5203ad6e37e5a3328b8aef2dc8c1aa00a5c3e83896be7286663c8e640c7af25a`
- **Selection note:** broad gray-umber boards and calm paint washes are readable at first-person distance with low saturation. Dark horizontal seams may overstate board courses on some meshes; a mesh/material-variety review remains required.

Exact prompt sent to ImageGen:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a first-person Godot triplanar village environment.
Primary request: Create one square seamless tileable hand-painted albedo texture of weathered wooden boards for old village houses, fences and furniture in Kyrlay, Tatarstan. This focused v4 alternative must simplify the source into 5–7 broad irregular board courses with low-contrast seams, 1–2 large wash/age patches per plank and no more than three quiet knots in the entire tile. It must read as aged boards at 128x128 and not become uniform brown striping under triplanar mapping.
Scene/backdrop: flat material study only, straight-on orthographic, edge-to-edge material swatch.
Subject: weathered painted wood board surface only; no assembled wall or object silhouette.
Style/medium: Painterly Low-Poly 3D, chunky faceted brush planes, hand-painted watercolor and dry-brush albedo, restrained medium-frequency variation, non-photographic.
Composition/framing: evenly distributed reusable tile, no perspective, no diagonal composition, no border, no focal plank.
Lighting/mood: neutral matte diffuse studio light; subdued value grouping; no embedded shadows/AO, gloss, cast shadows, specular highlights or vignette.
Color palette: gray-umber, taupe, muted brown, restrained ochre and tiny desaturated moss notes; old damp rural wood, never bright.
Materials/textures: broad board courses, quiet grain suggestions, sparse knots, rubbed paint washes and faceted brush planes; seams are low contrast and not geometry.
Constraints: seamless on all four edges, exactly square 1024x1024 source, reusable triplanar albedo, readable in first-person near/mid/far review without dense microtexture.
Avoid: photorealism, pixel art, cartoon/fantasy, heavy outlines, VHS, chromatic aberration, neon, horror symbols, blood, skulls, dense splinter grain, cracked-paint microflakes, photographic relief, regular striping, objects, nails, signs, text, watermark, logos, borders, grid, UV labels, visible seams, non-tileable edges.
```

## v4 Godot evidence (2026-08-14)

The two v4 candidates passed the deterministic image gate and were substituted
only in memory on Godot 4.7.1 .NET / Forward+ / Metal. Still captures cover
the three mandatory scenes plus a two-swatch sheet; the motion harness renders
near/mid/far × FOV 65/75/90 (27 cells). Runtime mappings, saves, collisions and
narrative state remain unchanged.

| Evidence | Absolute path | SHA-256 |
|---|---|---|
| Day still | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v4/godot_day_street_texture_candidates_1080p.png` | `9267027b0aa57a5542a38a3ae9b51c1621cf81a2eccea66a541f26abd7598cd8` |
| House still | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v4/godot_house_old_pc_texture_candidates_1080p.png` | `7aedd9aa737c556ba7a697676f3557a5f5a2ac166026d9e8d6cd72b335d60858` |
| Kara still | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v4/godot_kara_urman_edge_texture_candidates_1080p.png` | `a1b489112c600e53dd50d4bbfbb0a12ba9ac7b44a883c8ec799ab1737e1a772e` |
| Swatches | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v4/godot_material_swatches_texture_candidates_1080p.png` | `869ca5a91984a25e6822cab8eb30e403ed065e8503e2a50db7b1071686a7e31b` |
| Motion day | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v4/godot_day_street_texture_v4_motion_sweep_1080p.png` | `b4fef995712deccb851a5be4c3a95deb840ad7a453fdc0f3b8c92bb6b8d6b329` |
| Motion house | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v4/godot_house_old_pc_texture_v4_motion_sweep_1080p.png` | `0a8917359651e469834f0347e14c75aca2cceb2b6ce505dbdf70a8c66090337c` |
| Motion Kara | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v4/godot_kara_urman_edge_texture_v4_motion_sweep_1080p.png` | `068a9e214de2c301e3d3bb5171d9042d990a3f7d106dad3f4dc6ea3e05074a97` |
| Motion manifest | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v4/texture_candidate_motion_sweep_manifest.json` | `27c7a6713ea02f38b8ac074957c59d1f40f4909818d08649b010cf990ea41b43` |

V4 verdict: image gate PASS and still/spatial capture PASS, but both candidates
are **HOLD/REWORK** rather than replacement-ready. The damp-earth tile has
stronger standalone value grouping, yet the in-engine delta is small (day v3→v4
RGB MAD approximately `0.47/0.43/0.27`) and its continuous dark rut bands and
stone marks duplicate authored road-relief/rut/puddle geometry. The next gate is
a relief-only versus relief+wetness A/B along/across the road plus a 20–30 m
repeat review. The wood palette is calmer, but dark continuous seams and knots
turn into regular triplanar striping and can substitute for mesh board seams;
v3 remains safer until material owners/scales are separated or the albedo is
reworked. Runtime v1 remains active and no art lock is declared.

## Production candidate v5 rework (2026-08-14)

V5 is a non-destructive rework prompted by the v4 Godot review. It removes the
painted rut bands/volume-like stones from earth and the long dark board
seams/concentric knots from wood. Both files are still test-only; v1 runtime
mappings remain active and no art lock is implied.

### `damp_earth_v5_albedo.png`

- **Final file:** `game/assets/textures/painterly/damp_earth_v5_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-f0e43bb0-3791-44af-b2c2-30f8ab4cab16.png`
- **Generation mode:** built-in `image_gen` with `damp_earth_v4_albedo.png` as a palette/brush reference only; 2026-08-14.
- **Normalization:** source `1 254×1 254` RGB PNG resized with `sips -z 1024 1024 -s format png`.
- **SHA-256 (source):** `c6d20920891a74beab82e971c45cef1f408504237f977e14feb009cddbbabd51`
- **SHA-256 (final):** `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108`
- **Selection note:** broad gray-brown/olive damp masses survive reduction without a continuous rut composition. Sparse painted pebble marks remain an OPEN repetition/relief-duplication risk.

Exact prompt:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a Godot Painterly Low-Poly 3D first-person horror game
Input image: damp_earth_v4_albedo.png is a palette and brush reference only
Primary request: create a new square seamless orthographic top-down damp earth albedo texture, a v5 rework for a compact Tatar village road
Style/medium: Painterly Low-Poly 3D, hand-painted watercolor and gouache brush planes, restrained stylized material, not photoreal
Composition/framing: evenly distributed tile, no horizon, no vanishing point, no diagonal terrain composition, no directional perspective
Lighting/mood: neutral diffuse material study, no cast shadows, no ambient occlusion, no specular highlights, no embedded lighting
Color palette: muted gray-brown clay, desaturated olive, cool damp gray, very restrained blue-gray accents; natural and subdued
Materials/textures: 6–10 broad irregular clay and wetness masses with soft broken edges and medium-scale value grouping; sparse tiny flat pebble marks only as painted color, no 3D stones; broad watercolor pooling; enough breakup to survive reduction to 128x128
Critical rework constraints: remove all continuous lengthwise rut bands, road grooves, aligned tire tracks, embedded stones with volume, hard dark channels and strong directional repetition; geometry and puddle meshes provide ruts and relief in Godot
Seamless requirement: tileable on all four edges with no visible edge seam or corner discontinuity
Avoid: photographic mud, micro-grit, sharp pebbles, realistic rock shading, baked AO, baked relief, mirror/glare, saturated green or blue, horror noise, pixelation, cartoon/fantasy, heavy outlines, VHS, chromatic aberration, text, watermark
```

### `weathered_wood_boards_v5_albedo.png`

- **Final file:** `game/assets/textures/painterly/weathered_wood_boards_v5_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-787c41b8-bfd9-4691-9117-2c898e9b29fb.png`
- **Generation mode:** built-in `image_gen` with `weathered_wood_boards_v4_albedo.png` as a palette/brush reference only; 2026-08-14.
- **Normalization:** source `1 254×1 254` RGB PNG resized with `sips -z 1024 1024 -s format png`.
- **SHA-256 (source):** `1d6812ec67c7f35d1cf6a1cd13bcbad863f060e11e5e8d144ecfeebce7e9238c`
- **SHA-256 (final):** `3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70`
- **Selection note:** cooler gray-umber wash and lower-contrast board breaks reduce the strongest v4 striping, but horizontal bands, knots, orientation and shared-owner scale remain OPEN.

Exact prompt:

```text
Use case: stylized-concept
Asset type: production-candidate seamless albedo texture for a Godot Painterly Low-Poly 3D first-person horror game
Input image: weathered_wood_boards_v4_albedo.png is a palette and brush reference only
Primary request: create a new square seamless orthographic weathered wood boards albedo texture, a v5 rework for old houses and fences in a muted Tatar village
Style/medium: Painterly Low-Poly 3D, hand-painted dry-brush planes, restrained stylized material, not photoreal
Composition/framing: tileable horizontal boards, evenly distributed across the tile; board courses may vary gently in height and drift, but no single full-width dark line
Lighting/mood: neutral diffuse material study, no cast shadows, no ambient occlusion, no specular highlights, no embedded lighting
Color palette: cool gray-umber, taupe, faded clay brown, sparse desaturated olive wear; muted natural palette
Materials/textures: 5–7 broad irregular board courses, broad wash and age patches on each board, sparse soft grain grouped into chunky brush planes; maximum 1–2 quiet small knots per tile, irregularly placed
Critical rework constraints: low-contrast board boundaries that do not form continuous dark stripes; remove long uninterrupted seams, concentric high-contrast knot rings, dense splinters, microflakes, photographic relief and plank-end perspective; mesh provides actual board seams and thickness in Godot
Seamless requirement: tileable on all four edges with no visible edge seam or corner discontinuity; no obvious 2x2 repeat when reduced
Avoid: photoreal wood, sharp grain noise, dramatic knots, built-in normal/height/AO, deep cracks, glossy varnish, bright orange/yellow, saturated green, pixelation, cartoon/fantasy, heavy outlines, VHS, chromatic aberration, text, watermark
```

V5 standalone image gate is PASS 2/2. The clean v3↔v5 Metal/Forward+ A/B
receipt is recorded under the superseding non-overwriting
`art/texture_candidate_ab_diagnostic_v5/` directory (the earlier v4 directory
is preserved); it has 9 unique sheets, 120/120 isolated relief/contact samples
and status OPEN. The
A/B is diagnostic only: earth relief-only versus relief+wetness and wood
facade/house/forest differences still require human near/mid/far, repetition,
lighting and material-owner review before any promotion.

## Production candidate v6 rework (2026-08-14)

V6 is a narrower, non-destructive rework after the v5 motion review. It targets
the two surfaces whose albedo must stay subordinate to authored geometry. The
selected files below are candidates only; v1 runtime mappings, shader, saves,
collisions and narrative state remain unchanged.

### `damp_earth_v6_albedo.png`

- **Final file:** `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v6_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-a99da43a-9a61-4b0b-a01d-16b9a77bbd52.png`
- **Generation mode:** built-in `image_gen`; new orthographic seamless tile; 2026-08-14.
- **SHA-256 (source):** `01c6c701ab20adc420d812f56b16c4f811b093a17244b52b3764b9a0f724a77f`
- **SHA-256 (final):** `9ef03566bf89c800dc6317028ce1d9dfb6304ed8de70ea35710fa1b22c544e54`
- **Normalization:** source 1 254×1 254 RGB PNG resized to 1 024×1 024 and embedded with the macOS sRGB IEC61966-2.1 profile.
- **Selection note:** broad clay/gray-olive watercolor masses with no painted ruts, tire tracks, stones or embedded AO. It is deliberately quieter than v5 and is intended to let the authored road-relief and wetness geometry carry the foreground read.

Exact prompt:

```text
Use case: stylized-concept
Asset type: seamless game albedo texture for Godot Painterly Low-Poly 3D
Primary request: Create a square, seamless orthographic tile of damp compacted earth for a first-person indie mystery-horror game set in a rainy Tatar village.
Style/medium: hand-painted painterly low-poly material study; broad brush planes with restrained, readable value grouping; not a photo.
Composition/framing: perfectly top-down orthographic square tile, no perspective, no horizon, no object framing, no border, edges designed to tile seamlessly on all sides.
Lighting/mood: diffuse overcast daylight, flat albedo reference only; no cast shadows, no ambient occlusion, no specular highlights, no baked lighting.
Color palette: muted clay brown, cool gray-brown, desaturated olive, small subdued blue-gray damp stains; natural and quiet.
Materials/textures: 6–10 broad irregular watercolor-like soil masses and soft pooling stains; subtle brush breakup that remains legible after reduction to 128×128; no geometric relief.
Constraints: texture must support authored low-poly road geometry and separate wetness/puddle materials; broad stains only, non-directional, no strong repeated motif.
Avoid: painted tire tracks, continuous ruts, vanishing-point composition, embedded stones or pebbles, strong occlusion, glossy wet patches, photographic granular micro-grit, checkerboard tiling, pixel art, cartoon/fantasy colors, horror gore, outlines, VHS, chromatic aberration, text, watermark.
```

### `weathered_wood_boards_v6_albedo.png`

- **Final file:** `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v6_albedo.png`
- **ImageGen source:** `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-3f6bb9ec-82e6-4160-9198-5fc15e077469.png`
- **Generation mode:** built-in `image_gen`; abstract painted material study; 2026-08-14.
- **SHA-256 (source):** `a9e6f9c6689137be6f87cffa8bfbabe586a12688d2be74fe9fa9cf279c7f3dd4`
- **SHA-256 (final):** `9b1703f44f00a226ccfe79cace0a8231148125d81c3c91db6e9951ab3dfcd55e`
- **Normalization:** source 1 254×1 254 RGB PNG resized to 1 024×1 024 and embedded with the macOS sRGB IEC61966-2.1 profile.
- **Selection note:** broad muted wash fields with no recognizable knot, annual ring, plank border or continuous dark seam. This is intentionally less literal than v5 so mesh boards, thickness and end-grain stay authored geometry.

Exact prompt:

```text
Use case: stylized-concept
Asset type: seamless game albedo texture for Godot Painterly Low-Poly 3D
Primary request: Create a square seamless color tile for aged village wood, designed as an abstract hand-painted material rather than a realistic wood photograph.
Style/medium: painterly low-poly, chunky faceted brush planes, simplified graphic material study with visible brush blocks; deliberately non-photoreal.
Composition/framing: perfectly top-down orthographic square tile, no perspective, no horizon, no object framing, no border, seamless on all four edges.
Lighting/mood: flat diffuse overcast albedo only, no shadows, no AO, no specular, no lighting gradients.
Color palette: desaturated gray-brown, umber, taupe, faint moss-olive and restrained ochre; subdued rural palette.
Materials/textures: 8–12 broad irregular painted wash shapes with a very soft short-grain suggestion; material should read as old painted wood only through color grouping, not through depicted plank edges or realistic rings. Leave broad quiet areas for triplanar projection.
Constraints: make it intentionally low-frequency and stylized; at 128×128 it must read as a few irregular weathered value families, not as stripes. No directional board courses, no continuous horizontal or vertical grain, no knot, no rings, no cracks, no splinters, no bark, no nails, no end-grain. Mesh geometry supplies boards and seams.
Avoid: photorealistic wood, realistic annual rings, recognizable knot, long parallel bands, dense fibers, high-frequency microdetail, photographic relief, embedded shadows, glossy varnish, bright orange, pixel art, cartoon/fantasy colors, heavy outlines, VHS, chromatic aberration, text, watermark.
```

An earlier wood attempt from the same session was rejected before normalization:
source `/Users/unterlantas/.codex/generated_images/019fec14-3af4-7091-9920-ac08ba294cd5/exec-a397c522-97e1-4cf4-98c4-47b5f3c81579.png`, SHA-256
`3c96d0fc28b45b701b4b2547f196151a3b7e5dc67fd9ecb5450bd56760049602`.
It still looked photographic, with a visible knot and long grain bands, so it is
not a repository candidate.

The v6 pair passes the deterministic 1 024² RGB/sRGB image gate (2/2 when
selected explicitly; 18/18 for the complete v2–v6 candidate set). Godot
material-owner, A/B, near/mid/far, temporal and human art-direction review are
still required before promotion; no art lock is declared.
