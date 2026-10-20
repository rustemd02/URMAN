# H049 — existing outbuilding source and material reference

This folder contains one isolated existing project-original shed, five actual Blender views of that extracted GLB, and one built-in ImageGen material reference. It is not a captured runtime scene, a newly generated 3D model, or runtime/art acceptance. No Meshy requests, uploads or credit spend occurred in this stage.

## Existing source

- Current consumer: `urman.world:act1/open-part/h049-house` / ADR-H049, catalog `urman.catalog:urman_village_exterior_kit/villageparcel-varianta-timbergable`.
- Exact subtree: `VillageParcel_VariantA_TimberGable/VillageParcel_VariantA_TimberGable_Outbuilding`.
- Source: `game/assets/models/act1/urman_village_exterior_kit.glb`, 3,132,148 bytes; SHA256 `fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8`.
- Existing producer: `assets/source/blender/act1/urman_village_exterior_kit.py::author_variant_shed_a`, called by `author_variant_parcels`; source recipe SHA256 `dff52398d19cb2de90e4a7dc02c3a87a946b15236c0f137e68024ce95de21564` at preparation.
- Rights: Project-original per existing `assets/asset_registry.json`. The registry's historical derived SHA is not represented as the actual current input SHA.
- Preparation reuses the existing literal GLB JSON/BIN writer pattern from `aidar_meshy_v1/prepare_aidar_player_runtime.py` and the existing house studio's `create_studio`, `render_view`, and `object_bounds` functions. No shared helper was edited.
- Extracted GLB: `h049-outbuilding-source.glb`, 42,880 bytes; SHA256 `67fc359b6536ae801bfd84428b00780e918491d8ed11ec27365956bef1600ea3`. Exactly 21 mesh nodes, 524 triangles, 27 material surfaces; the glass remains separate. All selected source bufferView bytes, child TRS, material roles, positions, normals and indices are preserved; only indices/offsets are compacted and the isolated component's ancestor/preview translation removed. No Blender geometry re-export, new basis, baked runtime scale or geometry fix.
- Source bounds, Blender Z-up metres: min `[-2.0699999, -1.4449999, 0.02]`, max `[2.0699999, 1.4307481, 3.1199999]`; dimensions approximately `4.1399999 × 2.8757482 × 3.0999999` m. Render scale 1.
- H049's actual runtime plot origin, yaw, `.731` parcel scale, outbuilding compensation to `.9`, whole-parcel edge clamp, terrain, world lighting and PainterlyMaterialLibrary binding are deliberately not simulated. See the immutable `source-receipt.json` for the current entity/catalog, exact byte guards and view hashes. `prepare-source.raw.log` is the single actual preparation log.
- No UV layers exist on the 27 source surfaces. This is **not** a ready keep-original-UV retexture donor. No atlas, UV work or material graft was performed.

## Source inspection

Personally inspected all five actual imported-GLB views before ImageGen: `source-front-right.png`, `source-rear-left.png`, `source-front.png`, `source-left.png`, `source-window-opening.png`. Source shape is a small plain timber-board storage shed, not a log cabin. The source window has visible black end patches at framing joints; source geometry/materials were retained, not silently repaired. Source images are examples of existing geometry under a neutral studio, not proof of current gameplay camera visibility.

## One built-in ImageGen reference

- Tool: built-in `image_gen.imagegen`, not a CLI/API fallback; one invocation and one selected output, 2026-10-20.
- Tool output file: `/Users/unterlantas/.codex/generated_images/01a15661-354d-76f1-8270-5d14a959da37/exec-9c47318f-fc33-412e-ad29-7d7804f69e2d.png`.
- Tool output identifier: `exec-9c47318f-fc33-412e-ad29-7d7804f69e2d`; generated-images task directory `01a15661-354d-76f1-8270-5d14a959da37`.
- Selected project copy: `imagegen-reference-v1.png`, 2,134,839 bytes; SHA256 `4511d043c56f36f87a2a3cfa9b49baf5bfeaeb03e33124dd947c8a3ad31335d9`. The default tool output was left in place according to the imagegen skill.
- Input 1, geometry FRONT: `source-front.png`, SHA256 `a464960d0ee12cf89ec3687b557685cb548ef91df7d45ae570af205f05c3cde9`.
- Input 2, geometry REAR-LEFT / side window: `source-rear-left.png`, SHA256 `22e7e1e15a4a18f54e52cbe730ea1821fb7618874efcc5427cf1718916a70482`.
- Input 3, **material/color/stylization only**: author-accepted game frame `evidence/ACT1-DEPTH.12/native_uv_v3_fdc5f5ef62fc4554ad107b9d9bef31ef/frames/babai_shape_front_high.png`, SHA256 `146035efec8364cf654d773259d17a3dd538acb4cba3549e1a85204a50239adb`. Personally viewed the exact frame before the generation; its architecture and snowy surroundings are not the shed's geometry input.

Exact prompt:

```text
Use case: stylized-concept.
Asset type: one game-environment material reference for the EXISTING H049 neighbor's timber storage shed in URMAN.
Generate exactly ONE isolated complete shed, front-left three-quarter view, on a simple neutral warm-gray studio background, no text.
Input image 1 is the actual source geometry FRONT: authoritative compact single-storey shed footprint, broad gable facade, slightly asymmetric gabled roof with modest projecting eaves, plain plinth, ONE closed timber door offset to viewer-right on the FRONT. Preserve its overall proportions and door position. Image 2 is the actual REAR-LEFT geometry view: authoritative small four-pane window on the LEFT SIDE and single rear repair batten; the rear wall has NO door. The new front-left view should show the front door and left-side window together. Keep approximately 4.14m overall width, 2.88m depth, 3.10m total height; no porch, no steps, no annex, no chimney, no extra openings or floors.
Input image 3 is ONLY the author-accepted GAME MATERIAL/COLOR/STYLIZATION reference, especially its inhabited house and plain timber outbuilding. Match that restrained chromatic painterly material 3D family, solid readable shapes, quiet coarse texture and naturally worn materials. Do NOT copy its architecture, trees, people, fencing, lighting effects, landscape or snow onto this shed.
Subject/materials: an ordinary useful timber-frame and boarded storage shed in a Tatar village, not a dwelling or fantasy hut. Plain believable vertical weathered gray-brown pine boarding, subtle subdued warm aged timber grain rather than featureless smooth panels; door in slightly warmer dark timber with its existing vertical battens and a small DULL DARK IRON handle visibly distinct from wood. Use restrained timber window framing with four dim blue-gray glass panes, properly seated simple joinery and a modest sill; no decorative carving, no arbitrary black square end caps. A quiet worn muted gray-green metal roof with restrained broad seams and matte roughness, supported by the existing simple timber gable/eaves; subdued stone/concrete low plinth touching the ground. Differentiate wood, metal, glass and plinth clearly. Weathering should be quiet and credible, no heavy dirt, no rust spectacle, no distressed grunge.
Keep the actual simple roof, rectangle wall envelope, scale, front door and left window positions. No log cabin conversion: no horizontal log crowns or protruding log ends. No roof redesign, no warped boards, floating parts, unstable construction or disconnected handles.
Medium: material-rich but softly simplified 3D game asset coherent with image 3, matte surfaces, restrained brushy color variation, broad calm forms; do not turn it into photorealistic HDR/scan realism, plastic toy, clay miniature, concept-painting landscape or hyper-detailed cinematic scene.
Lighting: neutral diffuse studio daylight, mild soft shadow, easy to read the material separation. No dramatic spotlight or baked black shading.
This image is a material input only; the existing 3D source remains geometric authority. Exclude snow, rain streak overlays and environmental shadows because future material maps must not bake temporary winter weather or world lighting into the atlas.
```

Output inspection: one complete snowless shed, front door and left four-pane window, vertical timber boarding, subdued gray-green roof and visibly separate dark iron/stone/glass. Material-study candidate only: board grain is denser than the accepted distant game frame, while the image alters door batten relief/window joints and depicts boarding/seams that are not new canonical geometry. Those bitmap details must not become implicit authorization to remodel the source. Root style review remains open. No second image variant or paid request.

Verification: recipe AST parse, source receipt JSON parse, all source artifact/view SHA and size checks pass. Blender 4.5.12 LTS completed once; actual GLB re-import contains 21 mesh nodes / 524 triangles and no UV layers. Literal source-buffer guards pass. No tests/build/runtime launch. Folder frozen for root review; no temporary duplicate exports were created.
