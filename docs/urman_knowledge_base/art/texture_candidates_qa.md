# Painterly texture candidate QA

Дата QA: 2026-08-13. Область проверки — только versioned albedo-кандидаты
`*_v2_albedo.png`, `*_v3_albedo.png` и test-only Godot capture helper. Этот отчёт не объявляет art
lock и не меняет shader, runtime, collisions, narrative или save. Registry
provenance was mirrored by the root integration pass after this QA run.

## Provenance / license

Текущий repository evidence: [`game/assets/textures/painterly/README.md`](../../../game/assets/textures/painterly/README.md)
и [`assets/asset_registry.json`](../../../assets/asset_registry.json) теперь
содержат отдельную versioned запись/hash для каждого `_v2` и `_v3` файла. Семейная
provenance — project-generated через встроенный OpenAI ImageGen, без
third-party source images, с `Project-generated` license; точные prompts и
исходные пути лежат в generation manifest. Это закрывает provenance/hashes для
кандидатов, но не означает финальную asset или art-lock acceptance.
PNG не содержат текстовых metadata chunks; SHA-256 — единственный content
identity, используемый этим QA.

**QA verdict: PASS for the six-file v2 image/material-owner/capture gate and
the six-file v3 image/test-capture gate; art lock remains OPEN.** All six v2
candidates now have explicit
`PainterlyMaterialLibrary` surface/scale owners and real scene coverage. The
stone and fabric owners are bounded presentation-only candidates on the well,
forest-marker and rug anchors; the v1 runtime textures remain active elsewhere.
This closes the former ownership hold without claiming final visual acceptance.

## Deterministic image gate

Checked against `./eng/verify-painterly-textures.sh` and
`game/scripts/PainterlyMaterialLibrary.cs`: 1 024 × 1 024, 8-bit RGB/RGBA,
non-interlaced PNG, opposite-edge seam, clipping, HSV safety and texel-density
thresholds. All six candidates now resolve to an explicit material/scale owner;
the deterministic discovery command returns exit code 0 and the capture helper
fails closed if any candidate has zero scene coverage.

| Candidate | Absolute path | Name / size | SHA-256 | Image gate | Material owner / texel scale | Scene coverage | Verdict |
|---|---|---|---|---|---|---|---|
| `weathered_wood_boards_v2_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v2_albedo.png` | exact / 1 024 × 1 024 RGB | `03499969fcb1cb172a3d6b4b11bb710c65daa159e4c9cb24de11f4a1c3c1c9bf` | PASS; seam mean V/H 0.0373/0.0599, max 0.2471/0.2863, clip 0/0, sat mean/high 0.2787/0 | `wood`, 3.2 × 3.2 → 3 276.8 × 3 276.8 texel/world | PASS — 246 meshes replaced in the current v2 rerun | PASS |
| `aged_plaster_v2_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/aged_plaster_v2_albedo.png` | exact / 1 024 × 1 024 RGB | `a88975b10aa0d7070f868408366309c2b4100fd5eb1defd9fb08fe3281083416` | PASS; seam mean V/H 0.0283/0.0353, max 0.3098/0.2471, clip 0/0, sat mean/high 0.1979/0 | `plaster`, 2.6 × 1.8 → 2 662.4 × 1 843.2 texel/world | PASS — 15 meshes replaced across three scenes | PASS |
| `damp_earth_v2_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v2_albedo.png` | exact / 1 024 × 1 024 RGB | `02f356c591d52fdde31992f6064fbb3b10ec6928e856bfbb0315a687c24aeb56` | PASS; seam mean V/H 0.0270/0.0262, max 0.1843/0.1059, clip 0/0, sat mean/high 0.3503/0 | `earth`, 2.0 × 5.0 → 2 048 × 5 120 texel/world | PASS — 4 meshes replaced across three scenes | PASS |
| `pine_foliage_v2_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/pine_foliage_v2_albedo.png` | exact / 1 024 × 1 024 RGB | `3838784bdcf4d3fd31f6bf6bff93ef5906452cebe3df1d6b86075b42888e8ff6` | PASS; seam mean V/H 0.0353/0.0403, max 0.2784/0.2824, clip 0/0, sat mean/high 0.4090/0 | `foliage`, 2.2 × 2.2 → 2 252.8 × 2 252.8 texel/world | PASS — 956 meshes replaced across three scenes | PASS |
| `mossy_stone_v2_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/mossy_stone_v2_albedo.png` | exact / 1 024 × 1 024 RGB | `38b0e69f32a4e3e859921229cab0ea7bf12426dac9b8e150683b7607b5277b15` | PASS; seam mean V/H 0.0276/0.0334, max 0.1412/0.2078, clip 0/0, sat mean/high 0.1394/0 | `stone`, 2.4 × 2.4 → 2 457.6 × 2 457.6 texel/world; v2-only presentation owner | PASS — 4 meshes in the current v2 rerun | PASS; art-lock-pending |
| `old_fabric_v2_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/old_fabric_v2_albedo.png` | exact / 1 024 × 1 024 RGB | `f1c646785cddb876510f81be2400c6c2da01764b1fceb5d2bb8e92f0186d9132` | PASS; seam mean V/H 0.0444/0.0566, max 0.1961/0.3020, clip 0/0, sat mean/high 0.1743/0 | `fabric`, 3.0 × 3.0 → 3 072 × 3 072 texel/world; v2-only presentation owner | PASS — 3 meshes (rug + two woven stripes) | PASS; art-lock-pending |

## Godot capture helper — v2 baseline receipt (2026-08-12)

`eng/capture-texture-candidate-frames.sh` is fail-closed on the six-file set.
`game/tests/TextureCandidateFrameCapture.cs` and
`game/tests/texture_candidate_frame_capture.tscn` are test-only: they load the
three mandatory style scenes in temporary `SubViewport`s, clone only each
presentation `ShaderMaterial`, replace its `albedo_texture` by source basename
(`*_albedo.png` → `*_v2_albedo.png`), and save:

- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_day_street_texture_candidates_1080p.png`
- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_house_old_pc_texture_candidates_1080p.png`
- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_kara_urman_edge_texture_candidates_1080p.png`
- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_material_swatches_texture_candidates_1080p.png`

The helper produced all four files at 1 920 × 1 080. SHA-256 for this QA run:

| Capture | SHA-256 | Helper result |
|---|---|---|
| `godot_day_street_texture_candidates_1080p.png` | `3089418689f4f070bb20a5ab9b3c90882ce0ccf0ea2c82ca40c6cedaf8d2d2a0` | PASS; 673 replacements |
| `godot_house_old_pc_texture_candidates_1080p.png` | `c93da911930719aeb95a4bb29bf347a1d1f875fd6d88f674c0e4235c3d28fe6c` | PASS; 32 replacements |
| `godot_kara_urman_edge_texture_candidates_1080p.png` | `25ea6e5db604a682f8ecf3b7419d7bc9ae9b562a0f6143432bff89b5d59270c1` | PASS; 523 replacements |
| `godot_material_swatches_texture_candidates_1080p.png` | `40f22254e76cf4ba3fa868ee231e7fde20356f88a322b62656393df2384f371f` | PASS; six swatches, all six scene owners mapped |

Swatch order is deterministic: top row `weathered_wood`, `aged_plaster`,
`damp_earth`; bottom row `pine_foliage`, `mossy_stone`, `old_fabric`.

## Previews

![Six painterly candidate swatches](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_material_swatches_texture_candidates_1080p.png)

Scene previews:

- [Day street candidate frame](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_day_street_texture_candidates_1080p.png)
- [House and old PC candidate frame](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_house_old_pc_texture_candidates_1080p.png)
- [Kara-Urman edge candidate frame](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/godot_kara_urman_edge_texture_candidates_1080p.png)

Coverage totals are weathered wood 246, aged plaster 15, damp earth 4, pine
foliage 956, mossy stone 4 and old fabric 3. Stone is exercised by the
non-interactive village-well and Kara-edge marker anchors; fabric is exercised
by the house rug and two woven stripe meshes. No interaction is dispatched, no
`RuntimeBridge`/save path is created, and existing collision nodes are not
edited.

## Verification / remaining risks

- **PASS:** all six v2 PNGs meet naming, dimensions, PNG mode, hash and image
  safety checks; all six map exactly to explicit `PainterlyMaterialLibrary`
  scales. All six v3 siblings pass the same image gate in the focused candidate
  section below and remain preview-only.
- **PASS:** the three benchmark scenes and the six-swatch SubViewport rendered
  without helper-level Godot `ERROR:`/`SCRIPT ERROR:` or leak diagnostics in the
  escalated Metal/Forward+ run.
- **PASS:** versioned v2/v3 provenance/hash entries are mirrored in the texture
  README and asset registry; exact ImageGen prompts/source paths remain in the
  generation manifest. External license/cultural review is still a separate
  production gate.
- **PASS:** `mossy_stone` and `old_fabric` now have explicit v2-only
  presentation owners and non-zero coverage in the mandatory scenes. They do
  not silently replace the four v1 runtime surface mappings and do not add a
  shader fallback.
- **PASS for this run:** captures were rendered on the real Metal/Forward+
  driver and the helper log was scanned for errors/leaks; headless/dummy
  rendering remains intentionally rejected. Re-run after any candidate change.

## Style-bible deviations and production gates

The candidates themselves stay within the accepted Painterly Low-Poly rules:
muted natural hues, broad hand-painted breakup, moderate faceting and no
pixel/VHS/outline/chromatic-aberration treatment. The in-engine evidence still
records these deviations from the target style and therefore keeps art lock
open:

- Day street: road, houses, fences and repeated tree family still read as
  greybox modules; albedo breakup is quieter than the authored-material target.
- House/old PC: the CRT and bounded imported tower/panel/button detail are
  readable, but the room remains sparse, the lamp is warmer/stronger than the
  bible target, and rug/fabric plus the final hero-PC geometry are not
  production-grade.
- Kara-Urman edge: the forest silhouette is readable, but night fog compresses
  value separation and the branch/canopy family remains provisional rather than
  culturally reviewed authored forest art.
- Stone/fabric: explicit owners and scene coverage are PASS only on the two
  non-interactive stone anchors and three rug meshes; this is not global v2
  runtime activation and does not replace mesh relief, folds, collision or
  roughness authoring.

Remaining production gates are temporal motion/head-bob comfort, near/mid/far
readability during traversal, authored geometry and hero props, fog/light
calibration, mesh-collision review, M1/Windows performance, external
accessibility and cultural review, authored voice/final mix, and final art lock.

This is a production-candidate QA handoff, not an art lock. Near/mid/far motion
readability, geometry fidelity, fog/light calibration, cultural review and
final art lock remain separate gates.

## v2 regression rerun (2026-08-13)

After extending the test-only harness with a version selector, the established
six-file v2 capture was rerun. The image/material gate remains **PASS 6/6** and
the real Metal/Forward+ helper completed without Godot errors, script errors or
leak diagnostics. Current presentation replacement counts are day street 673,
house/old PC 32 and Kara-Urman edge 523; candidate coverage remains wood 246,
plaster 15, earth 4, pine 956, stone 4 and fabric 3. The regenerated v2 frame
hashes are:

| Capture | SHA-256 |
|---|---|
| `godot_day_street_texture_candidates_1080p.png` | `3089418689f4f070bb20a5ab9b3c90882ce0ccf0ea2c82ca40c6cedaf8d2d2a0` |
| `godot_house_old_pc_texture_candidates_1080p.png` | `c93da911930719aeb95a4bb29bf347a1d1f875fd6d88f674c0e4235c3d28fe6c` |
| `godot_kara_urman_edge_texture_candidates_1080p.png` | `25ea6e5db604a682f8ecf3b7419d7bc9ae9b562a0f6143432bff89b5d59270c1` |
| `godot_material_swatches_texture_candidates_1080p.png` | `40f22254e76cf4ba3fa868ee231e7fde20356f88a322b62656393df2384f371f` |

This is a regression receipt, not a claim that v2 textures are active runtime
owners or visually accepted.

## v3 focused material pass (2026-08-13)

This is an additional, non-destructive candidate pass for the six materials
identified as having the highest visual risk in the read-only art review. It
does not activate any texture in `PainterlyMaterialLibrary`, replace a v1 or
v2 file, or change runtime state.

| Candidate | Absolute path | Size / mode | SHA-256 | Image gate | Existing scale | Scene coverage |
|---|---|---|---|---|---|---|
| `weathered_wood_boards_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `65372413ff490da9dce831fd5f4022d6f804265b42dd9895ad4b13a6fa196093` | PASS; seam mean V/H `0.0081/0.0653`, max `0.1333/0.1412`, clipping `0/0`, saturation mean/high `0.3259/0` | `wood`, `3.2 × 3.2` | PASS — 246 presentation meshes across the mandatory scene set |
| `aged_plaster_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/aged_plaster_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `bb5f6c13d1947ef5616e9dac49ff37b41e6ff323901cc0b44b783212ac21454e` | PASS; seam mean V/H `0.0247/0.0225`, max `0.2314/0.1882`, clipping `0/0`, saturation mean/high `0.1701/0` | `plaster`, `2.6 × 1.8` | PASS — 15 presentation meshes across each mandatory scene capture |
| `damp_earth_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `cd031be6ff3fbb2750a05abe6b667d23bba2ad3705b6f5a14755d21a33b77b20` | PASS; seam mean V/H `0.0122/0.0115`, max `0.0902/0.0824`, clipping `0/0`, saturation mean/high `0.2540/0` | `earth`, `2.0 × 5.0` | PASS — 4 presentation meshes across each mandatory scene capture |
| `pine_foliage_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/pine_foliage_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830` | PASS; seam mean V/H `0.0119/0.0110`, max `0.0941/0.0706`, clipping `0/0`, saturation mean/high `0.2726/0` | `foliage`, `2.2 × 2.2` | PASS — focused v3 motion sweep; runtime art acceptance OPEN |
| `mossy_stone_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/mossy_stone_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `4444dc8585aada9dfd85601b8b32c96b0af3aa385d1d96e8ae88d24dfed66024` | PASS; seam mean V/H `0.0158/0.0166`, max `0.1725/0.2314`, clipping `0/0`, saturation mean/high `0.1268/0` | `stone`, `2.4 × 2.4` | PASS — 4 test-only anchor meshes; runtime art acceptance OPEN |
| `old_fabric_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/old_fabric_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `c19f22b7b5f2e9b4f8f5460e407c964d0ca9395022b4f2ef93d38722a0e3da82` | PASS; seam mean V/H `0.0466/0.0462`, max `0.2824/0.2235`, clipping `0/0`, saturation mean/high `0.1578/0` | `fabric`, `3.0 × 3.0` | PASS — 3 test-only rug/stripe meshes; runtime art acceptance OPEN |

The v3 helper is deliberately separate from the established v2 capture:
`eng/capture-texture-v3-frames.sh` uses a test-only version selector and writes
only to `art/texture_candidate_frames/v3/`. The capture ran on Godot 4.7.1
Mono, Metal, Forward+, at 1 920 × 1 080. The three benchmark frames and the
swatch frame are:

- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v3/godot_day_street_texture_candidates_1080p.png` — SHA-256 `4dc4419daf0c8ee2bf8cbab0e850c93b0d626358765d5b82b7a8ce0a48b42f55`
- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v3/godot_house_old_pc_texture_candidates_1080p.png` — SHA-256 `cbe6bb374dad99702b8aefba1ba85cdb93635ead94b100514b114d43fbc084ec`
- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v3/godot_kara_urman_edge_texture_candidates_1080p.png` — SHA-256 `f0b982b83c5f74d76136054b24b564388ad49ee1a8e16e78dabfd1ae7f91d7fa`
- `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_frames/v3/godot_material_swatches_texture_candidates_1080p.png` — SHA-256 `40028e8161db2e24ce4230e234a939a4844ffa4c88c13de20a1375c95ef3c697`

### v3 visual read and remaining gates

- **Weathered wood:** the candidate removes much of the old photographic
  splinter/grain noise and reads as broad boards in the day and house frames.
  The 246 replacements make it the strongest practical candidate, but the
  repeated plank bands and one shared wood owner still require a 20–30 m sweep
  and material-variety review before activation.
- **Damp earth:** the candidate is visibly quieter and less photographic than
  v2. The authored road-relief pass now gives the day street a readable crown,
  two rut families and bounded puddle shapes, but wetness/specular roughness and
  a full production material review remain required. The separate test-only
  roughness candidate now passes isolated scene-owner contact for all five
  clusters/15 patches within ±0.010 m after bounded `PuddleFar` and
  `BoundaryWetPatch` origin corrections. It writes three 1 920×1 080 frames,
  but roughness/specular acceptance remains OPEN.
- **Aged plaster:** the candidate reads as a calm limewashed wall and is suitable
  as a background material. The house frame is still dominated by warm lighting
  and sparse geometry, so this v3 is not evidence that the domestic scene is
  production-ready.
- **Kara-Urman edge:** the still capture predates pine v3, while the subsequent
  motion sweep shows modest near/mid foliage breakup. The provisional conifer
  silhouette and night fog compression remain geometry/lighting gates, not a
  texture-pass approval.
- **Stone / fabric:** v3 now receives test-only substitutions on the existing
  well/foundation and rug/stripe anchors. Stone reads as rounded fieldstone;
  fabric resolves as broad faded weave bands. Three-dimensional silhouette,
  folds and large-surface repetition remain mesh/scene gates.
- **Tiling:** opposite-edge numeric seam checks pass, but long painterly marks
  and triplanar projection still require traversal review over 20–30 m at FOV
  65°/75°/90° and with head-bob enabled.

**v3 verdict: image gate PASS, Godot capture PASS, spatial sweep PASS, art acceptance OPEN.** The
candidate files are production candidates only. Runtime material ownership,
near/mid/far readability, geometry relief, fog/light calibration, cultural review
and final art lock remain open.

## v3 near/mid/far/FOV motion evidence (2026-08-13)

The focused v3 set was rendered through a separate test-only sweep after the
still capture. Each mandatory benchmark scene contributes nine real Metal /
Forward+ cells: near, mid and far camera positions crossed with FOV 65°, 75°
and 90° (27 cells total at 640 × 360, assembled into a 1 920 × 1 080 contact
sheet). Candidate materials are duplicated in memory; no runtime registry,
source scene, collision, save or narrative state is changed.

| Scene | Absolute capture | SHA-256 | Replacement total across 9 cells |
|---|---|---|---:|
| Day street | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/godot_day_street_texture_v3_motion_sweep_1080p.png` | `eb2e5b7be7e002e7c04023d84857200e47594aba68b6106be8eefc2cf1c972fa` | 6 057 |
| House with old PC | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/godot_house_old_pc_texture_v3_motion_sweep_1080p.png` | `e7faa6af1b4a5aa87bdecad82892eed57323f99fb6db94a57bf01a73df9fed4c` | 288 |
| Kara-Urman edge | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/godot_kara_urman_edge_texture_v3_motion_sweep_1080p.png` | `dbe732bd3d212d0d5d0c5585cc9a1290b61284a31555cc1f3698c1e4bb2a967f` | 4 707 |

Manifest: `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/texture_candidate_motion_sweep_manifest.json`  
Manifest SHA-256: `b94d55d48f15fde9b44a3997f1203e57b8e94a23d4c10b80d04f7573273044a9`

The wrapper verified all six v3 candidates, built the game project, imported
Godot resources and completed the real-driver capture with no `ERROR:`,
`SCRIPT ERROR:`, resource-leak or RID diagnostics. Visual review found broad
wood/plaster readability, a new crown/rut road silhouette and bounded puddle
shapes; the day road still lacks production wetness/specular calibration;
the house remains lighting/hero-PC limited; and Kara-Urman gains modest
near/mid foliage breakup but remains fog/forest-geometry limited. This is spatial projection and repetition evidence
only: it does not prove temporal head-bob comfort, 20–30 m no-repeat acceptance,
runtime activation or art lock.

## Pine foliage v3 candidate (2026-08-13)

The night-forest candidate `pine_foliage_v3_albedo.png` is now present beside
the existing v1/v2 files and passes the deterministic image/material gate. It
was substituted only by the focused v3 motion harness; its status is **image
PASS / test-only Godot motion PASS / runtime art acceptance OPEN**. It does not
change the foliage runtime owner, shader, collisions, saves or narrative state.

| Candidate | Absolute path | Size / mode | SHA-256 | Gate | Scale |
|---|---|---|---|---|---|
| `pine_foliage_v3_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/pine_foliage_v3_albedo.png` | 1 024 × 1 024 RGB PNG | `05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830` | PASS; seam mean V/H `0.0119/0.0110`, max `0.0941/0.0706`, clipping `0/0`, saturation mean/high `0.2726/0` | foliage 2.2 × 2.2 |

## v4 focused earth/wood pass (2026-08-14)

V4 is a production-candidate comparison, not an art lock. Two new PNGs were
checked with the deterministic image gate, then substituted in memory in the
three mandatory Godot scenes. The still helper rendered 1 920×1 080 day,
old-PC house, Kara-Urman and two-material swatch frames. The motion helper
rendered 27 Metal/Forward+ cells (near/mid/far × FOV 65/75/90) with no Godot
errors, script errors, resource leaks or RID diagnostics.

| Candidate | Absolute path | Size / mode | SHA-256 | Image gate | Godot coverage |
|---|---|---|---|---|---|
| `weathered_wood_boards_v4_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v4_albedo.png` | 1 024×1 024 RGB | `5203ad6e37e5a3328b8aef2dc8c1aa00a5c3e83896be7286663c8e640c7af25a` | PASS; seam mean/max V/H 0.0157/0.0249 and 0.0902/0.0745; sat 0.2373 | still 246 meshes; motion replacement total 1 512 |
| `damp_earth_v4_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v4_albedo.png` | 1 024×1 024 RGB | `f1ed53e8b40f94fc45a2e8e43816e5c77e1717da0be09a60dcc68045da1bad31` | PASS; seam mean/max V/H 0.0078/0.0095 and 0.0471/0.0431; sat 0.1888 | still 4 meshes; motion replacement total 1 512 |

Independent visual audit changes the production verdict: both replacements are
**HOLD/REWORK**, despite the technical image/still/motion gates passing. Damp
earth has stronger standalone broad grouping, but the in-engine improvement is
small (day v3→v4 RGB MAD approximately `0.47/0.43/0.27`) and the two continuous
dark painted rut bands plus volumetric-looking stones duplicate the authored
road-relief, rut and puddle geometry. It must not be promoted until a relief-only
versus relief+wetness A/B (along and across the road, including 20–30 m repeat)
shows no double relief or collision mismatch. Weathered wood has a calmer
palette, but its dark continuous seams and concentrated knots become regular
triplanar stripes and can read as baked mesh relief; v3 is safer until surface
owners/scales are separated or the seams are reworked. Runtime v1 stays active.

## v5 focused rework (2026-08-14)

V5 is the next non-destructive candidate pair after the v4 HOLD/REWORK review.
It removes continuous painted road ruts and volume-like stone marks from earth,
and reduces long dark seams/concentric knots in wood. Neither candidate is
active in `PainterlyMaterialLibrary`; the v1 runtime files remain the source of
truth.

| Candidate | Absolute path | Size / mode | SHA-256 | Image gate | Godot evidence | Verdict |
|---|---|---|---|---|---|---|
| `damp_earth_v5_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v5_albedo.png` | 1 024×1 024 RGB PNG | `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108` | PASS; seam mean/max V/H `0.0083/0.0088`, `0.0588/0.0510`; clipping `0/0`; sat `0.2045` | v3↔v5 A/B technical PASS; 120/120 isolated contact samples | production OPEN; human relief/repetition review required |
| `weathered_wood_boards_v5_albedo.png` | `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v5_albedo.png` | 1 024×1 024 RGB PNG | `3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70` | PASS; seam mean/max V/H `0.0147/0.0205`, `0.0784/0.0784`; clipping `0/0`; sat `0.2516` | v3↔v5 A/B technical PASS; 167/15/64 wood replacements day/house/Kara | production OPEN; orientation/shared-owner review required |

### v3↔v5 Godot A/B receipt

The versioned test-only capture at
`/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v4/`
contains nine unique Metal/Forward+ sheets: day/Kara/Zirat earth along and
across the road, plus day/house/Kara wood. Each earth sheet has columns
`v3 relief-only`, `v3 relief+wetness`, `v5 relief-only`, `v5 relief+wetness`;
each wood sheet has `v3`, `v5`. The manifest SHA-256 is
`3ced8de9eeee32ac99bef4e7f7c1633e3bb7083d21503a572260ac9051d69b63` and the
README SHA-256 is `1cf7ba9786a5a914ab7364fb684d3c98698b0287f003d80384ae3bba7c8a3c32`.
The run used Godot 4.7.1 .NET Forward+/Metal, returned `status=OPEN`,
`technical_status=PASS`, and verified `120/120` contact samples in isolated
scene worlds. It is diagnostic evidence only: the source roughness remains
0.90, wetness clone is 0.50 in memory, and no runtime/shader/collision/save or
narrative owner changed.

Representative previews:

![v3/v5 earth A/B along-road](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v4/godot_day_street_earth_v3_v5_along_ab.png)

![v3/v5 wood A/B house](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v4/godot_house_old_pc_wood_v3_v5_ab.png)

The A/B does not close the art gate. V5 remains subject to near/mid/far and
20–30 m repetition review, wetness/roughness acceptance, neutral/warm lighting
review, geometry/canopy/hero-prop quality, cultural review and final art lock.

### Superseding v3↔v5 receipt after harness manifest fix

The same capture was rerun without overwriting the prior directory, under
`/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v5/`.
Godot 4.7.1 .NET Forward+/Metal returned `status=OPEN`,
`technical_status=PASS`, 9 sheets and 120/120 isolated relief/contact samples.
The dynamic manifest now names the selected versions instead of hard-coding a
v3/v4 acceptance key. Manifest SHA-256 is
`41cc4702fbca356b7a8e2035251205fc576ebd6f822dec834c159a25f7fbc9e0`; the
README SHA remains `1cf7ba9786a5a914ab7364fb684d3c98698b0287f003d80384ae3bba7c8a3c32`.
The sheet hashes are unchanged from the v3↔v5 receipt above. This remains
technical evidence only; earth relief-only visual separation and wood
multi-owner/orientation acceptance are OPEN.

### v5 near/mid/far motion sweep

The v5 candidates were also run through the full test-only 27-cell Godot sweep
(near/mid/far × FOV 65°/75°/90°) without changing runtime owners. Metal/Forward+
captured three 1 920×1 080 contact sheets with replacement counts day `1512`,
house/old-PC `135` and Kara-Urman `603`; logs contain no Godot, ObjectDB, RID or
resource-leak diagnostics. The evidence remains spatial projection only, not
temporal comfort or visual acceptance.

- Day street: `b9c7587602da28d8a62563c99c4e36deeef434725de870745fb52052ae69c2ae`
- House/old PC: `f891abeac5ded356014390bdd14ec452735a45b2348dd5e1f23c1c84bef73a5a`
- Kara-Urman edge: `ea56366bcc7ff2bb3013e2ecb9bf66b4d3c72f7eb89802429edc1543dff92f5f`
- Manifest: `003c0be712333fa0aaee5b1a596b119768a09cd0e2cd83b8b3376d014fea4053`

Files are retained under
`/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v5/`.

### v6 rework: isolated A/B and motion sweep

V6 is a narrower rework for the same earth/wood owners. The selected v6 files
pass the image gate (2/2 explicitly; 18/18 for the complete v2–v6 set), but the
production decision remains **OPEN**. Earth removes the strongest painted
pebbles/rut-like marks; wood is an abstract wash tile without a recognizable
knot or board border. A first wood attempt was rejected as photographic and was
not copied into the repository.

The v5↔v6 isolated Metal/Forward+ A/B receipt is under
`/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v6/`:
9 sheets, 120/120 relief/contact samples, `status=OPEN`,
`technical_status=PASS`, manifest SHA
`db978e19b72451d9548bafa323448fdf2d9d9ece64a4e2360f2f5e8cba751043`.
The earth relief-only difference is subtle; wood shows a clearer house/furniture
change but still needs shared-owner/orientation review.

The dedicated v6 27-cell near/mid/far × FOV 65°/75°/90° motion receipt is under
`/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v6/`.
The real-driver sheet hashes are day
`acf47e860275f3f9a2904d709c4a7c2bdb2965eca5063293c55d4658a25d0e0f`,
house/old-PC
`3ffca14fed6a9f479dd39be7be7528024978c837be5e59718d1988485bcab25c` and
Kara-Urman
`790f591239c89934538bc1ed3d351a86b776f2242d8281df46ed313ff52a6478`;
manifest SHA `e7d52a52fd8022c7b99b842c7d0a376b87886b88ff945e761f3748a8bab4cd5b`.
This closes technical spatial capture only. Runtime activation, 20–30 m
repetition, temporal comfort, geometry, fog/light, cultural review and art lock
remain open.

### Semantic owner integration receipt (2026-08-14)

The production presentation path now distinguishes imported `wood_facade`,
`wood_fence`, `wood_furniture` and `wood_bark` scales while retaining the same
v1 weathered-wood source. Generated character `cloth` metadata resolves the
old-fabric descriptor explicitly. Full Godot smoke and the material-owner
assertions pass; fresh static, 27-cell spatial and 216-sample temporal captures
are recorded in the style-frame READMEs. This closes the previous owner/fallback
implementation gap, not the visual acceptance gate: end-grain orientation,
clothing readability, repetition, authored geometry, cultural review and art
lock remain OPEN.
