# Photographed people reference — W01 v1 — 2026-10-26

- Artifact: `photographed_people_reference_v1.png` (1536 × 1024, PNG with alpha).
- Purpose: one isolated visual reference for a possible static 3D people group; concept only, not a runtime asset or art acceptance.
- Generation: built-in `image_gen.imagegen`, one call and one result. No Meshy submission, Meshy API, Meshy credits, or runtime integration.
- Generation ID: `exec-5b8d32fd-d2de-4390-b7bb-63cfd1a070f8`.
- Input image: `game/assets/textures/photobook/p-forest-front-v2.png`, project-owned image used only for grouping/material direction. SHA-256: `c4b64f07534aca8a5e021b55f9e695686cd0180379d4227ebad19d169149c60e`.
- Output SHA-256: `1a46730a0ece12c210e0424818802378e695ed2adc76c38077a612352c1432d3`.
- Alpha readback: corners are fully transparent; the figure interiors are near-opaque with soft partial-alpha edges. The image remains a cutout; do not flatten its alpha.
- Existing source authority: `assets/source/blender/photoworlds/W01/pforest_tableau.py` already stages three anonymous `Resident_Idle_frame18` figures in `PForest_PhotoOnly_FictionalGroup`; `pforest-tableau-v1.manifest.json` marks them `photo_only`. That authored tableau remains the placement/height source. This concept does not assign identities, alter scene positions, or establish runtime people.
- Current one-figure geometry source: `PForest_Photo_Figure_1_BakedIdle` is already present in `assets/source/blender/photoworlds/W01/pforest-tableau-v1.blend` as an anonymous, photo-only blue-coat figure with the Resident idle pose baked at frame 18. The rejected extraction recipe `prepare_human_trace.py` and its manifest are preserved in the local `URMAN-W01-rejected-baked-recipe-2026-10-26.zip` archive recorded in PW-046, rather than shipped in Git. An export candidate (`96903b0960dd69792d17f4bcd61c48d681dcd6433a29b78edcdc4e3bdd3552b4`) was rejected by root style review: the source reads as a helmet-like cap, bare hands, turquoise robe-like coat, and generic Resident pose rather than the P-FOREST blue winter coat. The verified GLB was removed from `game/assets`; this is not a ready runtime asset. Keep this preview/provenance only as source history until cleanup. The generated trio image remains only a future family/Meshy concept reference and is not used as geometry or texture input.
- Rights and story limit: the input is a project-generated illustration of fictional anonymous adults. The output is another anonymous concept reference; no known NPC or real-person identity is intended. Human-art acceptance and any later asset rights review remain open.
- Review status: trio concept was approved as a future family style reference; it is not itself a runtime model or Meshy geometry.

## Exact prompt used

```text
Use case: stylized-concept
Asset type: isolated character-group reference for a static game tableau, before any 3D modeling
Primary request: create one clean full-body reference image of exactly three anonymous fictional adults in practical winter clothing, standing separately as a small group. Follow Image 1's three-person grouping and relative heights only; do not recreate its forest.
Input images: Image 1 (project-owned P-FOREST illustration reference): use only the composition of the three people and its restrained painterly material character. Its people are fictional and anonymous. Do not copy or imply any named game character.
Scene/backdrop: genuinely transparent background. No visible ground, platform, snow, trees, branches, scenery, props, lettering, frame, or watermark.
Subject: exactly two adult men and one adult woman. Left: man in a dark blue winter coat and ushanka. Center: man in a brown sheepskin coat and ushanka. Right: woman in a plum winter coat and purple headscarf. Modest everyday civilian clothes, gloves, warm trousers and felt boots; no uniform, insignia, weapon, bag, or culturally specific religious symbol. Three distinct anonymous faces, not resembling known game NPCs or real people.
Style/medium: the same quiet, tactile painterly material realism as Image 1 and the accepted URMAN house frame: matte wool, sheepskin, cloth and worn felt, broad coherent brushwork, believable proportions. Avoid glossy cartoon dolls, plastic toy surfaces, photorealism, or a different visual style.
Composition/framing: wide horizontal 3:2 lineup. All three figures fully visible from head to boot, with both legs and both boots clearly separated on each person. Their bodies face mostly toward the viewer like the reference; left man's head turns toward image-left, center man's and woman's heads turn toward image-right. All gaze past the camera, never into the lens. Arms rest naturally down at their sides with gloved hands visible. Keep generous transparent gaps between every silhouette, arm, leg and boot so the figures do not touch or merge.
Lighting/mood: soft, neutral winter daylight with even gentle fill; no dramatic or strongly baked cast shadows.
Constraints: preserve exactly three separate standing silhouettes and the left-man / center-man / right-woman order. Keep the head-turn directions and civilian clothing described above. No named-character identities or story facts.
Avoid: extra people, touching/overlapping bodies, hidden limbs, hidden boots, shared coat shapes, cropped feet, ground plane, base, snow, forest, trees, branches, background gradients, dramatic rim light, glossy fabric, exaggerated cartoon proportions, hyperreal skin, text, logos, watermark.
```

## Single blue-coat reference — W01 v1 — 2026-10-26

- Artifact: `photographed_bluecoat_single_reference_v1.png` (1024 × 1536, RGBA PNG).
- Purpose: isolate the leftmost anonymous man from the approved trio concept for one static human-trace Meshy input; the trio remains the future family concept.
- Generation: one built-in `image_gen.imagegen` edit call against the local trio reference. No real-person source or named game character was used.
- Generation ID: `exec-4656abc4-63bd-4a96-ad91-d3c2e491c8f1`.
- Input: `photographed_people_reference_v1.png`; SHA-256 `1a46730a0ece12c210e0424818802378e695ed2adc76c38077a612352c1432d3`.
- Output SHA-256: `89d4155d2db871495e72d68ea0c01c978a04bc1495865202fa50cabeeccb6a76`.
- Alpha readback: RGBA, corner alpha is 0, alpha range 0–254; the subject is isolated with soft edges. Static visual review confirms one full-body figure, averted head, both lowered gloves and separated felt boots; no other people remain.
- Current status: reference self-QC passed for a single-view 3D request. Root reviewed the raw Meshy geometry and approved it for LOD preparation only; runtime and art acceptance remain open.
- Meshy image-to-3D: `meshy-7.1`, standard, 4K PBR; task `01a17788-e73a-75b6-aa85-8bed6809626e`, `SUCCEEDED`, actual 30 credits.
- Near triangle remesh: task `01a1778c-1405-740a-8b75-875a92399427`, target 25,000 faces, `SUCCEEDED`, actual 5 credits; downloaded GLB SHA-256 `93d3b5a4d04d09ce42b41480c99a41e7faa7f613103cd9f04a3ce88c27873dfd`.
- Mid triangle remesh: task `01a1778c-4d77-72d4-b5c8-e64ae8f5d871`, target 8,000 faces, `SUCCEEDED`, actual 5 credits; downloaded GLB SHA-256 `888d0cb0ae4e808054970c0b2fbe8594b75b23b875340b4ecf5b209845d99849`.
- Total actual Meshy cost for this chain: 40 credits. No additional paid tasks were submitted.
- Prepared candidate: `game/assets/photoworlds/forest_entry/pforest-human-trace-meshy-v1.glb`, SHA-256 `f22539b5248f4615f80cb65e4e8a93ed410da6ad2021f4c47edd5fc9c5e650e5`, 12,353,688 bytes; one shared PBR material, front +Z in glTF, 25,927-triangle LOD0 and 8,309-triangle LOD1, common height 1.78 m. The LODs occupy separate tiles in the same 2048×1024 atlas; their original UV islands remain intact except for tile scale/offset.
- The raw task GLBs, sanitized receipts and rendered review previews are archived locally outside the repository as `URMAN-W01-bluecoat-meshy-2026-10-26.zip`; the reproducible export script and source manifests remain with the selected image inputs.

### Exact edit prompt

```text
Edit Image 1 and isolate ONLY the leftmost adult man in the dark blue winter coat. Preserve this same anonymous fictional figure and his original face, body proportions, clothing design, worn matte painterly materials, relaxed standing pose, and averted gaze as closely as possible. Keep the dark fur ushanka with warm fur edging, the blue coat with its warm fur collar/cuffs, visible brown winter gloves on both lowered hands, dark trousers, and both separate felt boots. His head stays turned toward image-left, looking past the camera, never into the lens. Show one complete figure from hat to both boot soles, centered with clear gaps between arms, legs, and boots. Remove the center man and the woman completely; do not leave partial people, shadows, or silhouettes. Use a genuinely transparent background with clean alpha edges. Keep the restrained tactile painterly realism, soft neutral winter light, and quiet color palette of Image 1. Do not redesign him as a new person or named character. No helmet, no bare hands, no robe, no additional people, no ground, platform, snow, trees, props, text, logo, watermark, dramatic baked shadow, glossy plastic, cartoon look, or photorealism.
```

- Root deferred the Meshy candidate because campaign clarity remains the priority; the static figure is not runtime-integrated or art-accepted. The candidate GLB, raw/remesh GLBs, review renders and sanitized CLI receipts are preserved in the verified local archive `/Users/unterlantas/.codex/local_asset_archive/URMAN-W01-bluecoat-meshy-2026-10-26.zip` (SHA-256 `ff5a9ed51c3aad3114acddd12695b21c9787300ce86691ea69609f50103972d1`). The original `pforest-tableau-v1.blend` remains unchanged in the repository; the archive also contains a byte-verified copy.
