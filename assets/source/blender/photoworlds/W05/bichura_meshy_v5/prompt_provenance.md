# Bichura v5 — Meshy technical candidate

Status: original Meshy 7.1 result and locally simplified geometry reviewed as inputs for rigging. Deformation, runtime, artistic and cultural acceptance are pending.

- ImageGen source: Codex generation UUID `01a1afac-1270-7080-a3a0-c814d0538e60`, result `exec-5bec648a-3a9e-40a6-a887-4d6f39091ea6.png`
- Workspace copy: imagegen-reference-v5.png
- SHA-256: 5ee62a51cce868470418696de60adc1c0cad1741ce01e26520110c7838ae1565
- Reference: accepted URMAN gameplay frame evidence/ACT1-DEPTH.12/native_uv_v3_fdc5f5ef62fc4554ad107b9d9bef31ef/frames/babai_shape_front_high.png, for broad material response, lighting, and muted warm/cool balance only.
- Generation: built-in ImageGen, one generation call.
- Meshy task: `01a1afaf-e7c9-74b6-94fd-88d40e943dfe` (`meshy-7.1`, standard geometry, textured image-to-3D; SUCCEEDED).
- Credits: API balance before submission was 3660; task reports 30 consumed credits.
- Raw GLB: `bichura-v5-raw.glb`, 27745676 bytes, SHA-256 `f25b926c382527e2cd456a5f8688387783a0103d7b8b0f02d1d620b24bd89643`. Both GLBs and the Blender recipe are in the local asset archive under `URMAN-W05-concepts/bichura-meshy-v5`.
- Static GLB inspection: 1 mesh, 1 primitive, 258477 vertices, 462382 triangles, 1 material with base-color and normal textures; no skin or rig.
- Raw POSITION bounds are X[-0.72867, 0.72844], Y[-0.95200, 0.95088], Z[-0.45151, 0.44720] (about 1.457 × 1.903 × 0.899 scene units before node transforms).
- Local candidate: Blender 4.5.12 LTS, factory startup, background Workbench views at 640 × 640. Exact-position UV-seam vertex pairs were welded at 1e-6 before Collapse Decimate; loop UVs remain. The mesh was scaled uniformly to 0.75 m Blender-Z height and placed with its feet at Z=0. A previous non-welded 16k probe visibly split along surfaces and was replaced. No rig/remesh/retexture task was submitted.
- Candidate GLB: `bichura-v5-optimized.glb`, 14499792 bytes; SHA-256 `0c16bcfd4a3dae0e35937c73de5424c5269848e393317080cdeae5c8ef958fa2`. It contains 1 mesh, 14955 vertices, 16000 triangles, UV coordinates, 1 material, 3 packed images/textures, and no skin/rig. Blender dimensions were about 0.574 × 0.354 × 0.751 m. The exported GLB has 0 degenerate triangles, 0 boundary edges and 0 non-manifold edges after exact XYZ welding of UV-seam duplicates.
- Neutral review used front and side three-quarter views at 640 × 640. The welded silhouette is continuous, with visible hand and leg gaps. The next check covers separate digits, deformation and the cup grip.

Exact prompt:

Use case: stylized-concept character sheet for a new original 3D game character, Bichura, an uncanny but benevolent household presence. Intended as a 3D model-generation reference, pending artistic review.

Reference image role: use the attached accepted URMAN gameplay image ONLY for broad hand-painted surface response, tactile stylized 3D materials, muted warm timber / cool grey-green balance, and soft dimensional light. Do not reproduce its person, pose, clothing, house, snow, trees, objects, composition, culture-specific details or story.

Character: an unmistakably original adult nonhuman being, about 0.75 m tall, with grounded weight and an unfamiliar domestic silhouette. Low, gently slanted head merges into a compact sloped upper back without a visible neck; head is modest in size, face narrow with deeply recessed half-lidded eyes, plain asymmetric brow and cheek planes, small blunt nose and closed mouth, alert yet restrained expression. No broad human smile. Ears, if visible, are very small and close against the head. Broad low shoulders, compact torso, sturdy short legs, disproportionately long separate forearms reaching near knees. Both anatomically plausible hands are visible, each with five distinct fingers and a clear thumb, fingers lightly separated and naturally relaxed for rigging and small-object contact. Natural matte clay-greige skin with subtle broad hand-painted variation. One very simple unadorned grey-green household linen wrap at the hips, preserving the readable shoulders, elbows, wrists, knees and fingers. No historical or regional costume claim.

One full-body figure, feet in frame, neutral relaxed A-pose, elbows slightly away from body, palms turned inward, three-quarter front view with face and both hands visible, orthographic or low-perspective character-sheet framing, centered against a plain neutral warm-grey studio background with a soft contact shadow. Render as a premium tactile stylized 3D game asset compatible with the attached game's broad material treatment, clear large forms, restrained surface detail, readable joints, balanced silhouette. This is not a final model or approved design.

Avoid: a human-looking man, child proportions, big head, pointed or prominent ears, wide glossy eyeballs or heavy eye sockets, bald fantasy elf, house-elf or goblin, mascot, cute pet, Pixar toy, BathSpirit, Shurale, gnome, troll, mythological costume, folk or religious symbols, invented ethnic ornament, cap, vest, uniform, boots, jewelry, wood or bark skin, fur, horns, antlers, tail, claws, beard, glowing parts, magic particles, action pose, props, held object, room, dramatic cinematic set, text, labels, watermark, multiple views, extra figures.
