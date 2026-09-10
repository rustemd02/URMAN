# URMAN Act I Wet Village Road Kit

Status: **authored presentation candidate; continuous route-spine pass 2026-09-05; not art lock and not demo ready**

This is a bounded Painterly Low-Poly geometry kit for the Act I village road.
It uses an authored uneven crown, wheel-worn ruts, irregular puddles, muddy
shoulders, asymmetrical ditches, discrete yard/drainage berms, a humble
culvert, low side-band foliage, and a short broken fence cue. The readability
pass rebuilds the active road relief roots, removes continuous linear rut
ribbons, bakes every mesh child transform into its geometry, and applies the
legacy extractor's inverse axis bake. The 2026-09-03 variant-module pass adds
two authored non-crown road segments — `RoadCrown_BranchWet` (narrower,
muddier FAP-branch strip) and `RoadCrown_ApproachWorn` (worn, patchy
zirat-approach strip) — so Arrival, MainStreet, the FAP branch, the return
street and the zirat transition no longer read as copies of one procedural
strip. The four shoulder/ditch roots now carry sparse, asymmetric low-poly
contact masses that transition toward yards and break the flat horizon while
staying within at most 0.23 m local vertical relief after the bake. Mesh names,
materials, preview-root placements, route and collision
ownership remain stable; runtime ownership stays with `Act1ConnectedWorld`.

## Deliverables

- Source: `assets/source/blender/act1/urman_wet_village_road_kit.blend`
- Derived GLB: `game/assets/models/act1/urman_wet_village_road_kit.glb`
- Root: `URMAN_WetVillageRoadKit`
- Units: **1 Blender unit = 1 metre**.
- Authoring travel axis: local ±Y before the export bake.
- Runtime travel axis: Godot local ±Z after the existing `+90° X` extractor correction.
- Source meshes: **165** presentation meshes; **2,920** source triangles.
- Materials: **19** muted Principled node materials; no raster textures.
- Source images: **0**; image-texture nodes: **0**.

## Published direct components

Every row is an empty direct child of `URMAN_WetVillageRoadKit`. Preview
translations are neutral board offsets and are removed by the existing
component extraction helper. Mesh children are directly parented to the named
component root and are LOD0 presentation geometry.

| Direct root | Preview offset (m) | Meshes | Triangles | Geometry role |
|---|---:|---:|---:|---|
| `RoadCrown_SunkenWet` | `(0.00, 0.00, 0.00)` | 5 | 326 | uneven crowned road surface, sparse edge clods and worn patches |
| `RoadRuts_PuddleNear` | `(0.00, -6.20, 0.00)` | 5 | 80 | broken shallow mud depressions and two irregular near puddles |
| `RoadRuts_PuddleFar` | `(0.00, 6.50, 0.00)` | 5 | 80 | broken shallow mud depressions and two offset far puddles |
| `RoadCrown_BranchWet` | `(0.00, -21.50, 0.00)` | 4 | 178 | narrower muddier FAP-branch strip with off-centre crown, one puddle and a worn patch |
| `RoadCrown_ApproachWorn` | `(0.00, 21.50, 0.00)` | 6 | 192 | worn patchy zirat-approach strip with one clod, puddle and two worn patches |
| `MuddyShoulder_Left` | `(-3.25, 0.00, 0.00)` | 8 | 188 | left wet shoulder, clay breaks, pockets and two yard berm masses |
| `MuddyShoulder_Right` | `(3.25, 0.00, 0.00)` | 8 | 188 | independent right wet shoulder variation and two yard berm masses |
| `RoadsideDitch_Left` | `(-4.75, 0.00, 0.00)` | 8 | 232 | left uneven drainage channel, three water pockets and two bank masses |
| `RoadsideDitch_Right` | `(4.75, 0.00, 0.00)` | 8 | 232 | independent right drainage channel, three water pockets and two bank masses |
| `CulvertStoneCrossing` | `(0.00, 3.60, 0.00)` | 13 | 248 | humble cap stones and wing stones across the ditch cue |
| `GrassSedgeMass_Left` | `(-6.15, -2.10, 0.00)` | 18 | 180 | low left side-band sedge and faceted grass |
| `GrassSedgeMass_Right` | `(6.15, 2.30, 0.00)` | 18 | 180 | low right side-band sedge and faceted grass |
| `FernShrubBreak_Left` | `(-6.55, -9.60, 0.00)` | 25 | 242 | low left fern/shrub break with open road-side edge |
| `FernShrubBreak_Right` | `(6.45, 9.70, 0.00)` | 25 | 242 | low right fern/shrub break with open road-side edge |
| `RoadFenceBreak_Low` | `(-5.65, 8.20, 0.00)` | 9 | 132 | short low broken fence, gap, posts and moss cue |

## Geometry and material policy

- Geometry is primary: faceted plane changes, a crowned/rutted lane, broken
  mud depressions/puddles, slumped shoulders, shallow drainage, irregular
  yard/drainage berms, and clod/stone contact breakup avoid a flat rectangular
  road slab or paired rails.
- Continuous linear rut ribbons are omitted from the active pass. The remaining
  mud breaks and puddles retain the irregular wheel-worn cue.
- The source mesh bake is `+90° X` after each child object transform is baked
  into its vertices. The existing runtime extraction correction remains
  `Basis.FromEuler(+90° X)`, so the two axis conversions cancel for the
  published road geometry.
- Root bases and all mesh-child local transforms are neutral. The four-sided
  preview translations remain on component empties only and are explicitly
  rebased by the existing runtime extractor.
- Existing muted Principled materials remain authoritative. No textures,
  images, external downloads, or new dependencies are introduced.
- The new terrain contact masses are low-poly faceted solids with irregular
  eight-sided footprints and sloped crests; they are presentation-only,
  asymmetrically placed, and do not widen or move the gameplay route envelope.
- The kit contains no text, signs, religious or ethnic markers, fantasy/horror
  iconography, neon, camera, light, collision/navigation/interaction/physics
  object, runtime script, or collision-suffixed mesh.
- The host scene remains authoritative for route floor, collision, navigation,
  interaction, camera, runtime state, visibility, and wet-weather effects.

## Verification evidence

Pinned generator and GLB import/readback: **Blender 4.5.12 LTS**. Two
background runs rebuilt/baked the same authored route, shoulder, and ditch
geometry; the consecutive rerun produced the same GLB SHA. No demo, visible
window, audio, or full capture was launched.

Generator/source validation:

```text
root: URMAN_WetVillageRoadKit (EMPTY)
direct components: 15; mesh children: 165; source triangles: 2920
images: 0; cameras: 0; lights: 0; collision/physics objects: 0
axis bake: legacy-godot-extractor-compensation-v1
mesh child transforms after bake: 165 neutral
linear rut ribbons: 0
```

Active road source profile before axis bake (legacy Blender coordinates):

```text
RoadCrown_SunkenWet: meshes=5 triangles=326 z=0.029489..0.210696
RoadRuts_PuddleNear: meshes=5 triangles=80 z=0.069772..0.174238
RoadRuts_PuddleFar: meshes=5 triangles=80 z=0.072531..0.174877
RoadCrown_BranchWet: meshes=4 triangles=178 z=0.077493..0.200641
RoadCrown_ApproachWorn: meshes=6 triangles=192 z=0.076644..0.216175
MuddyShoulder_Left: meshes=8 triangles=188 z=0.092057..0.322617
MuddyShoulder_Right: meshes=8 triangles=188 z=0.096695..0.277833
RoadsideDitch_Left: meshes=8 triangles=232 z=0.106953..0.317548
RoadsideDitch_Right: meshes=8 triangles=232 z=0.114355..0.336287
ROAD_AABB_LOCAL: x=-3.345472..3.318040 y=-14.000000..14.000000 z=0.029489..0.216175
ROAD_MAX_VERTICAL_RELIEF: 0.186685 m
```

The authored source profile uses `rut_depth=0.082 m` and `crown=0.115 m` on
the main lane, with independent edge wobble and asymmetric edge drop. The
validated source crown/rut AABB now has `0.186685 m` total vertical relief;
the shoulders and ditches additionally peak at `0.277833..0.336287 m` source
height while the direct roots remain within `0.230560 m` runtime-facing
vertical relief. These are mesh-derived bounds, not material/texture claims.

The following AABBs were read from a clean pinned-Blender headless import of
the published GLB. `raw_aabb` is the imported legacy basis; `extractor_aabb` is
the same component after emulating the existing runtime `+90° X` correction
with preview translation removed. The latter is the runtime-facing contract:

| Component | Raw Godot size (X,Y,Z) | Extractor/runtime size (X,Y,Z) |
|---|---:|---:|
| `RoadCrown_SunkenWet` | `(6.663512, 0.181207, 28.000000)` | `(6.663512, 28.000000, 0.181207)` |
| `RoadRuts_PuddleNear` | `(3.370775, 0.104466, 5.106085)` | `(3.370775, 5.106085, 0.104466)` |
| `RoadRuts_PuddleFar` | `(3.253010, 0.102346, 5.788174)` | `(3.253010, 5.788174, 0.102346)` |
| `RoadCrown_BranchWet` | `(4.790205, 0.123148, 14.000000)` | `(4.790205, 14.000000, 0.123148)` |
| `RoadCrown_ApproachWorn` | `(5.894377, 0.139531, 14.000000)` | `(5.894377, 14.000000, 0.139531)` |
| `MuddyShoulder_Left` | `(3.430936, 0.230560, 27.200000)` | `(3.430936, 27.200000, 0.230560)` |
| `MuddyShoulder_Right` | `(2.712350, 0.181138, 27.200000)` | `(2.712350, 27.200000, 0.181138)` |
| `RoadsideDitch_Left` | `(3.629156, 0.210595, 26.900000)` | `(3.629156, 26.900000, 0.210595)` |
| `RoadsideDitch_Right` | `(3.856123, 0.221932, 26.900000)` | `(3.856123, 26.900000, 0.221932)` |

The clean pinned-Blender GLB import reported the expected authored root,
exactly 15 direct component roots, 165 mesh objects / 2920 triangles, neutral
mesh-child transforms, and no cameras or lights. Runtime road widths are
3.253–6.664 m where the crown/rut corridor is present; runtime longitudinal
extents are 5.106–28.000 m. The rebuilt active road's extracted local vertical
relief is at most 0.186685 m; shoulder/ditch components read
0.181138–0.230560 m, all below the 0.38 m generator gate.

Generator idempotence (derived GLB):

```text
run 1 GLB SHA-256: 5baae8899afd74f61f9ce9489ac8c4e9ee3a3db60f7455e7af1e706291cf13bf
run 2 GLB SHA-256: 5baae8899afd74f61f9ce9489ac8c4e9ee3a3db60f7455e7af1e706291cf13bf
GLB SHA MATCH: True
```

The source `.blend` is regenerated and saved on each run; Blender's container
metadata can vary between saves, so the derived GLB is the byte-for-byte
idempotence artifact.

Artifact sizes and SHA-256:

```text
1913540 bytes  assets/source/blender/act1/urman_wet_village_road_kit.blend
344068 bytes   game/assets/models/act1/urman_wet_village_road_kit.glb
4ca5dc26da3237173ea114a27c7854f60037dc91915129541b95c468268646f8  assets/source/blender/act1/urman_wet_village_road_kit.blend
5baae8899afd74f61f9ce9489ac8c4e9ee3a3db60f7455e7af1e706291cf13bf  game/assets/models/act1/urman_wet_village_road_kit.glb
```

## Open gates

- **C# integration:** open; no C# or scene changes were made. Parent acceptance
  must re-enable the active wet-road builder and inspect the integrated frame.
- **First-person/360°:** open; forward, reverse, side, near/mid/far, and moving
  views still require the connected-world composition pass.
- **Human visual:** open; silhouette, repetition, material response, and
  shoulder/ditch readability still require human review in the actual scene.
- **Performance:** open; target-host import, memory, draw-call, and frame cost
  are not claimed.
