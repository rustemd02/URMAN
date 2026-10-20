# Existing W02 summer willow reference

Source: `game/scripts/photoworlds/W02Controller.cs`, `BuildSummerBank()`, currently lines 910–940. Runtime nodes are `W02World/OpeningWillows-1` and `W02World/OpeningWillows1`; two instances, four mesh nodes each. Their roots are `(74.4, TerrainHeight(74.4,114),114)` and `(81.6,TerrainHeight(81.6,114),114)`. This reference extracts one unchanged local tree to origin; it does not reproduce the bank terrain or world lighting.

The recipe writes three neutral views and a small Blender source scene. It follows the repository's `act1/neighbor_log_house_a_meshy_v1/render_existing.py` camera, Cycles and neutral-lighting pattern.

| Part | Current Godot definition |
|---|---|
| Trunk | CylinderMesh: bottom radius .24 m, top radius .14 m, height 3.2 m, six radial segments, default four height rings, both caps; position `(0,1.6,0)` |
| Crown0 | SphereMesh: radius 1.1 m, height 2.2 m, seven radial segments, four rings; position `(-.75,3.1,-.4)` |
| Crown1 | Same sphere; position `(0,3.6,0)` |
| Crown2 | Same sphere; position `(.75,3.1,.4)` |
| All crowns | Local scale `(1.15,.85,1.15)` in Godot XYZ |
| Palette | Trunk `#5b5044`/`wood_cut`; Crown0/2 `#516440`, Crown1 `#5c7046`, all `leaf_birch` |

Godot XYZ maps to Blender `(X,-Z,Y)`, preserving handedness, dimensions and all local transforms. This uses explicit primitive vertices and triangles, not Blender UV-sphere/cone defaults: Godot sphere `rings=4` means six latitude rows, including duplicated poles. Cylinder default rings were checked against the upstream header. Godot clockwise triangles are reversed for Blender's counter-clockwise fronts.

Primitive definitions checked on 20.10.2026 against [Godot primitive_meshes.cpp](https://github.com/godotengine/godot/blob/master/scene/resources/3d/primitive_meshes.cpp) and [primitive_meshes.h](https://github.com/godotengine/godot/blob/master/scene/resources/3d/primitive_meshes.h). Godot is MIT-licensed; its algorithm is re-expressed here for this reference. The checked upstream master source is not a pinned hash of the local 4.7 engine; this is a source-definition reconstruction, not a bit-for-bit engine mesh capture. Double-versus-float rounding may differ.

Expected current budget per tree: trunk 72 indexed triangles; three crowns 70 each, including 14 degenerate pole triangles per crown. Total **282 indexed / 240 nondegenerate triangles**, 202 primitive vertices, four mesh surfaces. Two runtime trees total **564 indexed / 480 nondegenerate triangles**, eight mesh surfaces. The constructor declares no LOD or trunk collision. These counts concern only the two R08 willows, not the separate `HorizonTrunks`/`HorizonCrowns` field dressing or R07 bench. The renderer records its produced count and source SHA in `source_metrics.txt`; no replacement performance budget is approved by this reference.

Material limitation: `Snowless()` calls the live `PainterlyMaterialLibrary` and disables snow; that Godot world-space texture, relief, wind, grading and BRDF stack is not ported into Blender. Render materials display the three authored base palette colors with a neutral diffuse finish only. These images are exact-form references within the numerical/version limits above, **not exact runtime appearance** or evidence of gameplay/art acceptance.

For the later generated candidate, the accepted material-family target remains `evidence/ACT1-DEPTH.12/native_uv_v3_fdc5f5ef62fc4554ad107b9d9bef31ef/frames/babai_shape_front_high.png`: coherent, convincing wood and scene-scale material detail. This is a summer snowless willow; do not substitute the generated winter pine. The current three round clumps are the starting example, not a requirement to preserve their artificial anatomy in the improved reference. ImageGen, upload, Meshy generation, credits and runtime replacement are outside this source step.
