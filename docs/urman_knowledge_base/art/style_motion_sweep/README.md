# Godot style camera-sweep evidence

Capture date: 2026-09-14 rerun on the assembled shipped world (Act1ConnectedWorld)  
Engine: Godot 4.7.1 .NET, Forward+, Metal 4.0, Apple M4 Pro  
Command: `./eng/capture-style-motion-sweep.sh`  
Status: **production evidence only; art lock remains OPEN**

This test-only pass renders each mandatory Act 1 zone at nine camera states:
three rows (`near`, `mid`, `far`) and three columns (FOV `65°`, `75°`, `90°`).
Each cell is a real 640 × 360 SubViewport readback; the contact sheets are
1 920 × 1 080 RGBA PNGs. The capture is a spatial readability sweep, not a
temporal movement or head-bob test. The exact camera positions and targets are
recorded in `style_motion_sweep_manifest.json`.

## The sweep photographs the assembled world, not the raw zone scene

Until 2026-09-14 this harness instantiated only the `style_benchmark_*` zone
scene. That scene is the gameplay base, but it is not what the player sees:
`Act1ConnectedWorld` hides its `Ground`, `Road`, benchmark `Pine`/`Birch`,
`BoundaryThread`, `DistantWindow`/`DistantWarmWindow` and other stand-ins, and
mounts the shared winter heightfield, authored parcels and exterior atmosphere
in their place (`Act1ConnectedWorld.ApplyLogicalZonePresentationSuppressions`).
The old sheets therefore showed benchmark ground and a floating warm window that
no player can reach, and read as a grey-olive summer greybox next to a shipped
snow village.

The harness now builds the world the same way `Main` does — `new
Act1ConnectedWorld()` per capture scene, `SetActiveLogicalZone(zone_id)`, camera
placed at `zone origin + the same authored offset` — so every cell is the real
winter Act 1 look. The camera grid, scene ownership checks and fail-closed
imported-module assertions are unchanged; `styleImportedModules` is now read
from the zone instance inside the connected world. The manifest records
`assembled_world`, `zone_id` and `world_origin` per scene, and `captured_at_utc`
is the real capture time (it was a hardcoded `2026-08-12` literal that made a
fresh run claim an August date).

## Contact sheets

| Scene | Preview | SHA-256 |
|---|---|---|
| Day street | `godot_day_street_motion_sweep_1080p.png` | `3a2a1bf26245bac005c6fa265473cf7d4228dbc978f5aeee541fce1b95eeb4a5` |
| House / old PC | `godot_house_old_pc_motion_sweep_1080p.png` | `687c12d607e295b59ccb3f6a6c872f3d4b9337e7aa662c90313cd805f2a2c894` |
| Kara-Urman edge | `godot_kara_urman_edge_motion_sweep_1080p.png` | `5411bde5573440be67ace0dd6f22cf09960d1af2b112949dac0c9b40a9b29ad6` |

![Day street camera sweep](godot_day_street_motion_sweep_1080p.png)

![House and old PC camera sweep](godot_house_old_pc_motion_sweep_1080p.png)

![Kara-Urman edge camera sweep](godot_kara_urman_edge_motion_sweep_1080p.png)

The manifest itself is `15cec7b3f9f046d4123ee07e092a381dd0141adeb3de787e7351f5c93e27e603`.

Every sheet below this line that predates 2026-09-14 was captured from the raw
zone scene and is retained as history only. The frames above are the current
receipt.

## Reading the grid

- Columns, left to right: FOV `65°`, `75°`, `90°`.
- Rows, top to bottom: `near`, `mid`, `far`.
- The middle cell is the existing 75° style-frame composition. It is a
  comparison anchor, not a new runtime camera default.
- Day and Kara rows fail closed unless their zone instance inside the connected
  world reports the expected project-original imported module
  (`HouseA_project_original` and `PineA_project_original`).

## Evidence and review

- Build completed with zero warnings and zero errors.
- All 27 cells rendered through Metal Forward+; all three sheets are 1 920 ×
  1 080, non-interlaced RGBA PNGs.
- Capture logs contain no `SCRIPT ERROR`, Godot runtime error, ObjectDB leak,
  RID leak or resource-leak diagnostic.
- Day street now shows a continuous authored crown/rut relief and near/mid
  puddle silhouettes, but the road, houses, fences and trees still read as a
  provisional greybox family. The isolated wetness candidate now passes
  scene-owner contact (5/5 clusters, 15/15 patches within ±0.010 m) after
  bounded benchmark-origin corrections; its roughness/specular review and
  broad albedo response remain open.
- The house preserves the old PC as the focal object in the near and mid rows;
  the far row loses document/CRT detail. The lamp is still warmer/stronger than
  the style bible target, and the fabric/hero-PC geometry is not production
  quality.
- Kara-Urman keeps foreground/midground separation in the near and mid rows,
  while fog compresses the far row into blue-gray masses. Canopy tiers are
  readable, but the branch/crown family remains provisional and is not a final
  cultural or forest-art review.

## Gates still open

This sweep does **not** prove temporal motion comfort, head-bob comfort,
Windows/M1 performance, global v2 texture activation, production road/forest/PC
geometry, final fog/light calibration, cultural review, or art-lock acceptance.
The stone/fabric owner gate is closed only for the bounded presentation anchors;
all other items remain separate production gates. The baseline
`style_frames/README.md` remains the authoritative art-lock verdict.

## Superseding lighting/fog calibration receipt — 2026-08-14

The same 27-cell harness was rerun after the bounded `StyleBenchmarkZone`
ambient/fog/key-light calibration. It remains a presentation-only candidate;
runtime materials, shader, collision and narrative state are unchanged.

| Scene | Current contact sheet | SHA-256 |
|---|---|---|
| Day street | `godot_day_street_motion_sweep_1080p.png` | `022647535c839bb568378b5b3015d8d01af63d7bc8576435680056399cb1ca0d` |
| House / old PC | `godot_house_old_pc_motion_sweep_1080p.png` | `39d810d103d372c09404333212bb04668bd7ea6da191f036f07f13593a665767` |
| Kara-Urman edge | `godot_kara_urman_edge_motion_sweep_1080p.png` | `1276650c0bf6f2bdf9d266cbe0a62ff891727575f4c3c6cacf536579dd1f6af2` |

Manifest SHA-256: `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`.
All 27 cells are non-empty and leak-free on Metal/Forward+. The sweep still
does not close visual style acceptance, authored geometry, cultural review or
release-hardware gates.

## Semantic material-owner split rerun — 2026-08-14

After the imported wood/cloth owner calibration, the same 27-cell
near/mid/far × FOV 65°/75°/90° sweep completed on Metal/Forward+ without
errors, leaks or blank cells:

| Scene | Current contact sheet SHA-256 |
|---|---|
| Day street | `6b76871d171ac322c59fd5f6c90cd098c6cbad136ffd67e319e46fed2f528d71` |
| House / old PC | `db75b808ac74d9ca9ca268398253c36d177e06c1fef1d83131a11e1f2c82feb5` |
| Kara-Urman edge | `9fde939a23e1b81d9838813bb2b9a4727894817b777ebd2ac1abbd42823f13fb` |

Manifest SHA-256: `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`.
This is still traversal evidence, not acceptance of end-grain orientation,
20–30 m repetition, authored geometry or final art lock.

## PineA collision-isolation rerun — 2026-08-14

After the imported-physics ownership fix, the same 27-cell Metal/Forward+
capture was rerun. The adapter removes imported `CollisionObject3D` and
`CollisionShape3D` descendants before the kit enters the scene tree; the
original Pine keeps only its controlled layer-2 proxy and the two presentation
variants have no physics descendants. All 27 cells remained non-empty and the
manifest stayed byte-identical.

| Scene | Current contact sheet SHA-256 |
|---|---|
| Day street | `6b76871d171ac322c59fd5f6c90cd098c6cbad136ffd67e319e46fed2f528d71` |
| House / old PC | `db75b808ac74d9ca9ca268398253c36d177e06c1fef1d83131a11e1f2c82feb5` |
| Kara-Urman edge | `ee60b51327633c7dd627848cf49ce33412121f20f383d784b556c8489510a1b2` |

This closes only the imported layer-1 collider regression. It does not accept
the repeated PineA family, greybox geometry, fog/light balance, motion comfort,
release-hardware performance or the visual art lock.
