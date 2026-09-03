# Full-game dressing captures

Capture date: 2026-08-14  
Engine: Godot 4.7.1 .NET, Metal 4.0, Forward+, 1920×1080  
Command: `./eng/capture-fullgame-frames.sh`

These seven frames prove that the authored Acts 2–5 zone wrappers now receive
distinct walkable presentation dressing in Godot. The house, Soviet archive and
forest-boundary frames also exercise `GeneratedModularKitDressing`: the project-original
Blender `.glb` is loaded selectively, with LOD0/LOD1 visibility ranges applied
in-engine. The rebuilt 49-mesh kit includes WellA/WoodpileA yard dressing,
the OldPc DriveSlot/LabelPlate hero-detail pair and the GateA threshold
candidate; imported `-col` render meshes stay hidden;
`CollisionQaSmokeTest`
now proves queryable layer-1 floors and isolated layer-2 kit proxy colliders in
all 12 zones. They remain provisional production colliders until authored mesh
collision is reviewed. The frames also include deterministic project-original
character-kit silhouettes where the zone composition calls for them, plus the shared authored
road-relief path contract and bounded wet-patch candidates where those zones expose a path. They are production-progress
evidence, not a final art-lock promise. The geometry is still a modular
greybox/progress pass and needs further hero props, facial expression polish, authored sound, mesh-level
collision review and cultural presentation review.

| Frame | SHA-256 |
|---|---|
| `godot_act2_house_1080p.png` | `ea0305d78097a6d85d446d2cbc9827d9c477664e116682e1a5ac4ea3b611cb1d` |
| `godot_act2_river_1080p.png` | `2acce022b6ed63c17ed8109547ec180d2e291b6db90d0c8e1f1cadd86595606a` |
| `godot_act3_archive_1080p.png` | `533b30598f3e205d6765982691a89e2c489eccd4fe2e4b9a65f8431efa81569a` |
| `godot_act3_soviet_1080p.png` | `57ad5be29707070b14448cb45134a22d85438fc39d0ef4437c38c7c27b211598` |
| `godot_act4_pact_1080p.png` | `61d80e2bba5e63fd46a61703649d9595b1dd08a158dd0971ad4ba0eb42839858` |
| `godot_act5_boundary_1080p.png` | `e90047ff7c4dafa43f365b25424e718cd833483d71714e6c5d757abf956b4179` |
| `godot_act5_epilogue_1080p.png` | `b2801097aa1dfb858820e280371334aee58981a1f9bb07a179f5fe751ae3fc83` |

The 2026-08-14 recapture includes the bounded imported HouseA facade pass in
the Act 2 house and the OldPc tower/panel/power-button plus DriveSlot/LabelPlate
detail pass in the Soviet zone. The day-road and path wrappers now show deterministic low-poly
crown/rut relief; the wet patches are still presentation-only and carry an open
roughness/specular gate. The frames remain production-progress evidence, not a
final hero-asset or art-lock acceptance.

The 2026-08-14 recapture adds a seventh frame for the canonical Act 5 epilogue.
That zone now exercises the same project-original `HouseA_` presentation kit as
the Act 2 house, with the `act5-epilogue-house` variant and `wood_facade` owner
asserted by `FullGameFlowSmokeTest`; a distant procedural house remains only as
background dressing. This is a bounded presentation integration, not proof of
final epilogue geometry, authored mesh collision, cultural review or art lock.

The post-rebuild 2026-08-14 recapture additionally exercises `WellA_` and
`WoodpileA_` in the Act 2/epilogue family and `GateA_` in the Act 5 boundary
family. The full-game smoke now checks exact LOD0/LOD1 family counts and
presentation-only metadata. The frames still show provisional greybox-level
lighting/forest density; they are not an art-lock claim and do not establish
new gameplay collision.
