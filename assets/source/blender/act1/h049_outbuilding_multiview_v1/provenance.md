# H049 — four canonical geometry views

Four separate PNG inputs were rendered from the frozen existing shed GLB, using the existing hero studio helper. This stage made no new ImageGen/Meshy request, UV atlas, material graft, model export, runtime change or credit spend.

Source: `assets/source/blender/act1/h049_outbuilding_reference_v1/h049-outbuilding-source.glb`, 42,880 bytes, SHA256 `67fc359b6536ae801bfd84428b00780e918491d8ed11ec27365956bef1600ea3`. Project-original rights inherited from its source provenance. The source remains at its original path; no GLB or .blend was copied here.

The extracted component remains 21 mesh nodes / 524 triangles / 27 material surfaces, with separate original glass and no UV. Source object transforms, vertex positions/normals and material slots were checked unchanged in memory, and the original file SHA still matches after rendering.

Bounds in Blender Z-up metres: min `[-2.0699999, -1.4449999, 0.02]`, max `[2.0699999, 1.4307481, 3.1199999]`; dimensions approximately 4.14 × 2.8757482 × 3.10 m. All four use source scale1, orthographic scale6.2099998, the same target, upward view ratio0.15, 1280×960 resolution, studio lights/background and 24 Cycles samples. Runtime parcel scale, compensation/clamp, world ground/light/material overrides and collision are not reproduced.

Personally inspected all four actual renders:

- `front.png`: actual gable facade; one door offset right.
- `back.png`: no rear door/window; existing vertical repair batten remains.
- `left.png`: one four-pane window, no side door.
- `right.png`: wall face has no door/window.

Door/window components from an adjacent face may appear edge-on at the silhouettes; they are not extra openings. Existing black patches at source window joints and simple source wall geometry were not repaired or hidden. Semantic mapping is confirmed by source geometry and actual images, not inferred from axis filenames.

Future geometry input order is front/back/left/right, as four separate files. Accepted material reference remains separately at `assets/source/blender/act1/h049_outbuilding_reference_v1/imagegen-reference-v1.png`, SHA256 `4511d043c56f36f87a2a3cfa9b49baf5bfeaeb03e33124dd947c8a3ad31335d9`; it is not copied here or included as a fifth geometry view. No collage or four new ImageGen variations.

`input-receipt.json` records exact paths, hashes, bounds, camera parameters, source guards and semantic review. `render-inputs.raw.log` is the single actual Blender run. Recipe AST, receipt JSON and the four PNG SHA/size checks pass; Blender exited0. No new model/texture copies or intermediate exports were created. Four-view input frozen for root review; geometry generation, metric fit and native acceptance remain unproven.
