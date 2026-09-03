# Painterly texture candidate verification

- Status: **PASS for image/material-owner gate; v4 visual review HOLD/REWORK; art lock remains OPEN**
- Summary: v2 baseline 6/6 PASS; v3 six-file pass; focused v4 pair PASS; focused v5 pair PASS; focused v6 pair PASS; current discovered set 18/18 PASS.
- Gate: 1 024 × 1 024, 8-bit RGB/RGBA, non-indexed/non-gray, opposite-edge seam mean ≤ 0.12 and max ≤ 0.4, clipping fraction ≤ 0.02, HSV saturation mean ≤ 0.68 and high-saturation fraction ≤ 0.08.
- Texel-density check: each known filename is mapped to the existing `PainterlyMaterialLibrary` triplanar scale; this gate does not edit the shader.

| File | Status | Size/mode | Seam mean V/H | Seam max V/H | Clip low/high | Sat mean/high | Scale | Texels/world | SHA-256 |
|---|---|---|---:|---:|---:|---:|---|---:|---|
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/aged_plaster_v2_albedo.png` | **PASS** | 1024×1024 RGB | 0.0283/0.0353 | 0.3098/0.2471 | 0.0000/0.0000 | 0.1979/0.0000 | 2.6×1.8 | 2662.4×1843.2 | `a88975b10aa0d7070f868408366309c2b4100fd5eb1defd9fb08fe3281083416` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v2_albedo.png` | **PASS** | 1024×1024 RGB | 0.0270/0.0262 | 0.1843/0.1059 | 0.0000/0.0000 | 0.3503/0.0000 | 2.0×5.0 | 2048.0×5120.0 | `02f356c591d52fdde31992f6064fbb3b10ec6928e856bfbb0315a687c24aeb56` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/mossy_stone_v2_albedo.png` | **PASS** | 1024×1024 RGB | 0.0276/0.0334 | 0.1412/0.2078 | 0.0000/0.0000 | 0.1394/0.0000 | 2.4×2.4 | 2457.6×2457.6 | `38b0e69f32a4e3e859921229cab0ea7bf12426dac9b8e150683b7607b5277b15` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/old_fabric_v2_albedo.png` | **PASS** | 1024×1024 RGB | 0.0444/0.0566 | 0.1961/0.3020 | 0.0000/0.0000 | 0.1743/0.0000 | 3.0×3.0 | 3072.0×3072.0 | `f1c646785cddb876510f81be2400c6c2da01764b1fceb5d2bb8e92f0186d9132` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/pine_foliage_v2_albedo.png` | **PASS** | 1024×1024 RGB | 0.0353/0.0403 | 0.2784/0.2824 | 0.0000/0.0000 | 0.4090/0.0000 | 2.2×2.2 | 2252.8×2252.8 | `3838784bdcf4d3fd31f6bf6bff93ef5906452cebe3df1d6b86075b42888e8ff6` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v2_albedo.png` | **PASS** | 1024×1024 RGB | 0.0373/0.0599 | 0.2471/0.2863 | 0.0000/0.0000 | 0.2787/0.0000 | 3.2×3.2 | 3276.8×3276.8 | `03499969fcb1cb172a3d6b4b11bb710c65daa159e4c9cb24de11f4a1c3c1c9bf` |

## Shader decision

**No shader change in this pass.** The four existing surface names retain their
v1 triplanar mappings. `stone` and `fabric` now have explicit presentation
owners using the v2 candidates and documented scales (2.4×2.4 and 3.0×3.0); no
fallback to wood, plaster or foliage is allowed. They are attached only to the
non-interactive well/forest marker and house rug presentation anchors. The
candidate helper still clones presentation materials in memory for comparison;
the source scenes now also prove the two new runtime surface owners.

### Imported owner split (2026-08-14)

The Blender modular kit now uses four additional semantic descriptors without
changing the shader or replacing the source wood albedo:

| Owner | Source | World scale | Imported meshes |
|---|---|---:|---|
| `wood_facade` | `weathered_wood_boards_albedo.png` | 1.8 × 1.8 | HouseA roof, doors, porch, eaves, frames and trim |
| `wood_fence` | `weathered_wood_boards_albedo.png` | 2.2 × 2.2 | FenceA posts and rail |
| `wood_furniture` | `weathered_wood_boards_albedo.png` | 2.7 × 2.7 | TableA meshes |
| `wood_bark` | `weathered_wood_boards_albedo.png` | 1.35 × 1.35 | PineA trunk |
| `cloth` | `old_fabric_v2_albedo.png` | 3.0 × 3.0 | Generated character clothing metadata owner |

This is an integration candidate, not visual acceptance. Captures must still
check end-grain orientation, shared-owner repetition, character clothing
readability and near/mid/far traversal before any art-lock decision.

## Open handoff

The image/material-owner gate for the v2 baseline passes 6/6. The wrapper
`./eng/capture-texture-candidate-frames.sh` reruns the complete mapping gate
and performs the real Forward+/Metal scene capture. Near/mid/far temporal
readability, geometry, fog/light calibration, cultural review and final art lock
remain open.

## v3 focused candidate gate (2026-08-13)

The technical decoder also discovers `_v3_albedo.png` siblings. These remain
test-only candidates; the shader and active runtime mapping are unchanged.

| File | Dimensions / mode | Seam mean V/H | Seam max V/H | Clipping | Saturation mean/high | Existing scale | SHA-256 | Gate |
|---|---|---:|---:|---:|---:|---|---|---|
| `weathered_wood_boards_v3_albedo.png` | 1 024×1 024 RGB | 0.0081/0.0653 | 0.1333/0.1412 | 0/0 | 0.3259/0 | wood 3.2×3.2 | `65372413ff490da9dce831fd5f4022d6f804265b42dd9895ad4b13a6fa196093` | PASS |
| `aged_plaster_v3_albedo.png` | 1 024×1 024 RGB | 0.0247/0.0225 | 0.2314/0.1882 | 0/0 | 0.1701/0 | plaster 2.6×1.8 | `bb5f6c13d1947ef5616e9dac49ff37b41e6ff323901cc0b44b783212ac21454e` | PASS |
| `damp_earth_v3_albedo.png` | 1 024×1 024 RGB | 0.0122/0.0115 | 0.0902/0.0824 | 0/0 | 0.2540/0 | earth 2.0×5.0 | `cd031be6ff3fbb2750a05abe6b667d23bba2ad3705b6f5a14755d21a33b77b20` | PASS |
| `pine_foliage_v3_albedo.png` | 1 024×1 024 RGB | 0.0119/0.0110 | 0.0941/0.0706 | 0/0 | 0.2726/0 | foliage 2.2×2.2 | `05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830` | PASS |
| `mossy_stone_v3_albedo.png` | 1 024×1 024 RGB | 0.0158/0.0166 | 0.1725/0.2314 | 0/0 | 0.1268/0 | stone 2.4×2.4 | `4444dc8585aada9dfd85601b8b32c96b0af3aa385d1d96e8ae88d24dfed66024` | PASS |
| `old_fabric_v3_albedo.png` | 1 024×1 024 RGB | 0.0466/0.0462 | 0.2824/0.2235 | 0/0 | 0.1578/0 | fabric 3.0×3.0 | `c19f22b7b5f2e9b4f8f5460e407c964d0ca9395022b4f2ef93d38722a0e3da82` | PASS |

```text
./eng/verify-painterly-textures.sh
Status: PASS; 18/18 discovered v2/v3/v4/v5/v6 candidates pass.
```

## v6 focused rework gate (2026-08-14)

The v6 earth/wood pair is a test-only candidate set. Both files are 1 024 ×
1 024 non-interlaced RGB PNGs and pass the same deterministic seam, clipping
and saturation thresholds. Runtime owners remain the baseline v1 descriptors;
the imported semantic owner split below changes scale only, not the source
albedo or shader.

| File | Dimensions / mode | Seam mean V/H | Seam max V/H | Clipping | Saturation mean/high | Baseline scale | SHA-256 | Gate |
|---|---|---:|---:|---:|---:|---|---|---|
| `damp_earth_v6_albedo.png` | 1 024×1 024 RGB | 0.0133/0.0124 | 0.1020/0.0667 | 0/0 | 0.3208/0 | earth 2.0×5.0 | `9ef03566bf89c800dc6317028ce1d9dfb6304ed8de70ea35710fa1b22c544e54` | PASS |
| `weathered_wood_boards_v6_albedo.png` | 1 024×1 024 RGB | 0.0063/0.0070 | 0.0353/0.0667 | 0/0 | 0.2951/0 | wood 3.2×3.2 | `9b1703f44f00a226ccfe79cace0a8231148125d81c3c91db6e9951ab3dfcd55e` | PASS |

The independent v6 A/B, motion and temporal captures remain technical
evidence only. Earth still needs relief-only versus relief-plus-wetness review;
wood still needs end-grain, shared-owner repetition and near/mid/far review.

## v5 focused rework gate (2026-08-14)

The v5 pair is test-only and does not change the shader or active runtime
mapping. Both files are 1 024×1 024 non-interlaced RGB PNGs and resolve to the
existing earth/wood scales.

| File | Dimensions / mode | Seam mean V/H | Seam max V/H | Clipping | Saturation mean/high | Existing scale | SHA-256 | Gate |
|---|---|---:|---:|---:|---:|---|---|---|
| `damp_earth_v5_albedo.png` | 1 024×1 024 RGB | 0.0083/0.0088 | 0.0588/0.0510 | 0/0 | 0.2045/0 | earth 2.0×5.0 | `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108` | PASS |
| `weathered_wood_boards_v5_albedo.png` | 1 024×1 024 RGB | 0.0147/0.0205 | 0.0784/0.0784 | 0/0 | 0.2516/0 | wood 3.2×3.2 | `3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70` | PASS |

The deterministic gate is not artistic acceptance. Earth still needs a
relief-only/relief-plus-wetness read and pebble-repeat review; wood still needs
facade/fence/furniture/end-grain orientation and shared-owner review.

## v4 focused candidate gate (2026-08-14)

The decoder now accepts the additional `_v4_albedo.png` suffix without
changing the shader or runtime owner. The focused earth/wood pair is technically
clean and mapped to the existing scales:

| File | Dimensions / mode | Seam mean V/H | Seam max V/H | Clipping | Saturation mean/high | Existing scale | SHA-256 | Gate |
|---|---:|---:|---:|---:|---:|---|---|---|
| `weathered_wood_boards_v4_albedo.png` | 1 024×1 024 RGB | 0.0157/0.0249 | 0.0902/0.0745 | 0/0 | 0.2373/0 | wood 3.2×3.2 | `5203ad6e37e5a3328b8aef2dc8c1aa00a5c3e83896be7286663c8e640c7af25a` | PASS |
| `damp_earth_v4_albedo.png` | 1 024×1 024 RGB | 0.0078/0.0095 | 0.0471/0.0431 | 0/0 | 0.1888/0 | earth 2.0×5.0 | `f1ed53e8b40f94fc45a2e8e43816e5c77e1717da0be09a60dcc68045da1bad31` | PASS |

V4 is still test-only and remains HOLD/REWORK after the independent visual
audit: earth's painted rut/stone marks may duplicate authored road relief, and
wood's seams/knots may become regular triplanar striping across the shared owner.
The wrapper is `eng/capture-texture-v4-frames.sh`; the
near/mid/far/FOV harness is `eng/capture-texture-v4-motion-sweep.sh`. Both
require a real Godot renderer and fail closed on import, script or RID/resource
leak diagnostics. V4 does not replace v1/v2/v3 files or activate a runtime
mapping.

The 6/6 focused v3 still capture is recorded under
`art/texture_candidate_frames/v3/README.md`. No shader change or active
material-owner registration was made; near/mid/far readability and 20–30 m
tiling remain visual production gates.

### Wetness candidate handoff (2026-08-14)

The damp-earth and puddle question is intentionally separated from the image
gate. `eng/capture-wetness-candidate.sh` instantiates the day, house, Kara-Urman
and Zirat benchmark scenes, verifies exactly five clusters/15 existing patches
and source puddle roughness `0.90`, then checks contact against the authored
layer-1 relief. Only a passing contact gate would clone the existing shader
material in memory at roughness `0.50` and render three candidate frames.

The real Metal/Forward+ run is **OPEN as an acceptance decision, PASS as a
contact/isolation receipt**: each benchmark scene is instantiated one at a time,
the expected relief collider is required by instance identity, and all five
clusters/15 patches fall within ±0.010 m after bounded `PuddleFar` and
`BoundaryWetPatch` origin corrections. Candidate clone count is `15` at
roughness `0.50` in memory only, and three 1 920×1 080 PNGs are written. This
does not change the shader, runtime material owner, collisions, saves or
narrative; see `art/wetness_candidate/wetness_candidate_manifest.json` for
per-patch measurements. Wetness roughness/specular acceptance remains OPEN.

### Pine foliage v3 candidate (2026-08-13)

`pine_foliage_v3_albedo.png` is a separate night-forest candidate, not a
replacement of the v2 file or the active foliage owner. The image gate passes
at 1 024 × 1 024 RGB with seam mean V/H `0.0119/0.0110`, seam max V/H
`0.0941/0.0706`, clipping `0/0`, saturation mean/high `0.2726/0`; it uses the
existing foliage scale `2.2 × 2.2` (2 252.8 texels/world). Final SHA-256:
`05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830`.
The candidate is currently image-gate PASS / Godot motion-sweep PASS as a
test-only substitution. No shader or runtime material mapping changed; final
Kara-Urman acceptance remains open because the canopy geometry and night-fog
read are still provisional.

## v3 spatial motion-sweep receipt (2026-08-13)

`./eng/capture-texture-v3-motion-sweep.sh` passed on Godot 4.7.1 Mono,
Forward+/Metal. It rendered 27 real cells (three scenes × near/mid/far × FOV
65/75/90) into three 1 920 × 1 080 sheets. The test-only harness substitutes
all six v3 basenames and checks candidate-file existence before loading. The
render-only clone disables collision shapes but retains StaticBody3D-owned
presentation meshes, so road relief remains visible while Shape3D RIDs are
released before teardown.

| Scene | Capture SHA-256 | Replacements across 9 cells |
|---|---|---:|
| `day_street` | `eb2e5b7be7e002e7c04023d84857200e47594aba68b6106be8eefc2cf1c972fa` | 6 057 |
| `house_old_pc` | `e7faa6af1b4a5aa87bdecad82892eed57323f99fb6db94a57bf01a73df9fed4c` | 288 |
| `kara_urman_edge` | `dbe732bd3d212d0d5d0c5585cc9a1290b61284a31555cc1f3698c1e4bb2a967f` | 4 707 |

Manifest SHA-256: `d9aebe7bb1d04e169ccbc934da223fbf832bd6197324978f717b8118cfed8ee9`.
This confirms clean six-candidate spatial capture and projection coverage only; active
runtime ownership, temporal comfort, authored geometry, lighting/fog and art
lock remain open.

## Authored module material-owner addendum — 2026-08-14

The rebuilt Blender yard/threshold modules use the existing Painterly shader
contract with semantic owners only: `WellA_Rim` resolves `stone`, wooden WellA
parts resolve `wood_prop` at world scale `2.4 × 2.4`, WoodpileA resolves
`wood_bark`, GateA boards resolve `wood_fence`, and `GateA_Ribbon` resolves
`cloth`. `WellA_Water` remains a color-only ShaderMaterial. The new
`wood_prop` owner uses the existing v1 weathered-wood albedo; no v2–v6
candidate activation or shader change was made.

This is technical routing evidence, not texture/art acceptance: in-frame
near/mid/far readability, end-grain orientation, repetition, wetness and
cultural review remain open. The module receipt is recorded in
`art/authored_module_candidates_2026-08-14.md`.
