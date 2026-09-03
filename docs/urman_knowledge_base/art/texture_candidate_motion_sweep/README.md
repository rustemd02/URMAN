# Painterly v3 texture candidate motion sweep

Status: **production-candidate evidence only; art lock remains open**.

This is a test-only Godot capture for the six Painterly v3 candidates:
`weathered_wood_boards_v3_albedo.png`, `aged_plaster_v3_albedo.png`,
`damp_earth_v3_albedo.png`, `pine_foliage_v3_albedo.png`,
`mossy_stone_v3_albedo.png` and `old_fabric_v3_albedo.png`. The harness duplicates presentation
`ShaderMaterial` instances in memory and renders the existing benchmark scenes
at near/mid/far camera positions and FOV 65/75/90. It does not change
`PainterlyMaterialLibrary`, gameplay state, collisions, saves, narrative data or
the source scenes.

Command and renderer:

```text
./eng/capture-texture-v3-motion-sweep.sh
Godot 4.7.1 .NET Mono / Forward+ / real Metal driver
```

The output contains 3 contact sheets, each 1 920 x 1 080 RGBA PNG. Every sheet
contains 9 real 640 x 360 cells (near/mid/far rows; FOV 65/75/90 columns), for
27 rendered cells total. The wrapper also verifies all 6 candidate PNGs, builds
the game project, imports Godot resources and fails closed on Godot errors,
script errors or resource-leak diagnostics.

| Scene | Absolute capture path | SHA-256 | Sweep replacement total |
|---|---|---|---:|
| Day street | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/godot_day_street_texture_v3_motion_sweep_1080p.png` | `4f2d73913fe3fa2c60195fb50423bd68a0ae8718c6efe3a6d25d58bd78c866ff` | 6 057 |
| House with old PC | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/godot_house_old_pc_texture_v3_motion_sweep_1080p.png` | `e7faa6af1b4a5aa87bdecad82892eed57323f99fb6db94a57bf01a73df9fed4c` | 288 |
| Kara-Urman edge | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/godot_kara_urman_edge_texture_v3_motion_sweep_1080p.png` | `ed53e8e8b2103830dd0cdb93c2b11d94c59ddba0a55b57ab8930e7392d904637` | 4 707 |

Manifest: `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/texture_candidate_motion_sweep/texture_candidate_motion_sweep_manifest.json`  
Manifest SHA-256: `d9aebe7bb1d04e169ccbc934da223fbf832bd6197324978f717b8118cfed8ee9`

## Visual read

- **Day street:** the v3 wood reads as broad muted board bands on fences and
  house facades across the distance/FOV grid. The new road relief creates a
  continuous crown/rut silhouette and bounded puddle shapes, but damp-earth
  value grouping still does not create a wet surface without roughness/specular
  review and lighting calibration. A separate isolated wetness candidate now
  passes scene-owner contact after bounded benchmark-origin corrections and
  writes day/Kara/Zirat frames; it remains test-only evidence, not material
  activation or art-lock acceptance.
- **House / old PC:** plaster is a quiet limewashed background and remains
  readable from near through far. The warm lamp, sparse room dressing and
  hero-PC proportions dominate the frame; the candidate does not close those
  geometry or lighting gates.
- **Kara-Urman edge:** pine v3 adds restrained blue-green value breakup to the
  conifer tiers, especially in near/mid cells, but night fog still compresses
  far-row separation. The repeated conifer silhouette and branch spacing remain
  geometry/lighting gates; this is not a final forest-art approval.
- **Stone / fabric:** the v3 candidates now receive test-only substitutions on
  the existing well/foundation and rug/stripe anchors. Stone reads as rounded
  low-poly fieldstone; fabric resolves as broad faded weave bands. Three-
  dimensional stone silhouette, cloth folds and large-surface repetition remain
  mesh/scene gates.

This sweep is spatial evidence for projection, scale and repetition. It is not
a temporal head-bob/comfort test, does not prove 20--30 m no-repeat acceptance,
and does not declare runtime activation or art lock. Remaining gates are
production road/forest/hero-prop geometry, material-variety ownership, roughness
and fog/light calibration, traversal review, cultural review, release-hardware
performance and final art lock.
