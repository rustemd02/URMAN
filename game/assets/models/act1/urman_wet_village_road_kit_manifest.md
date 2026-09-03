# URMAN Act I Wet Village Road Kit

Status: **authored presentation candidate; variant-module wet-road pass 2026-09-03; not art lock and not demo ready**

This is a bounded Painterly Low-Poly geometry kit for the Act I village road.
It uses a shallow uneven crown, broken mud depressions, restrained puddles,
muddy shoulders, asymmetrical ditches, a humble culvert, low side-band
foliage, and a short broken fence cue. The readability pass rebuilds the
active road relief roots, removes continuous linear rut ribbons, bakes every
mesh child transform into its geometry, and applies the legacy extractor's
inverse axis bake. The 2026-09-03 variant-module pass adds two authored
non-crown road segments — `RoadCrown_BranchWet` (narrower, muddier FAP-branch
strip) and `RoadCrown_ApproachWorn` (worn, patchy zirat-approach strip) — so
Arrival, MainStreet, the FAP branch, the return street and the zirat
transition no longer read as copies of one procedural strip. The four
shoulder/ditch roots are kept to at most 0.22 m local vertical relief after
that bake. Mesh counts, names, materials, and preview-root placements remain
stable; runtime route/collision ownership stays with `Act1ConnectedWorld`.

## Deliverables

- Source: `assets/source/blender/act1/urman_wet_village_road_kit.blend`
- Derived GLB: `game/assets/models/act1/urman_wet_village_road_kit.glb`
- Root: `URMAN_WetVillageRoadKit`
- Units: **1 Blender unit = 1 metre**.
- Authoring travel axis: local ±Y before the export bake.
- Runtime travel axis: Godot local ±Z after the existing `+90° X` extractor correction.
- Source meshes: **192** presentation meshes; **3,042** source triangles.
- Materials: **19** muted Principled node materials; no raster textures.
- Source images: **0**; image-texture nodes: **0**.

## Published direct components

Every row is an empty direct child of `URMAN_WetVillageRoadKit`. Preview
translations are neutral board offsets and are removed by the existing
component extraction helper. Mesh children are directly parented to the named
component root and are LOD0 presentation geometry.

| Direct root | Preview offset (m) | Meshes | Triangles | Geometry role |
|---|---:|---:|---:|---|
| `RoadCrown_SunkenWet` | `(0.00, 0.00, 0.00)` | 11 | 280 | uneven crowned road surface, broken edge clods and worn patches |
| `RoadRuts_PuddleNear` | `(0.00, -6.20, 0.00)` | 9 | 153 | broken shallow mud depressions and irregular near puddles |
| `RoadRuts_PuddleFar` | `(0.00, 6.50, 0.00)` | 9 | 153 | broken shallow mud depressions and offset far puddles |
| `RoadCrown_BranchWet` | `(0.00, -21.50, 0.00)` | 6 | 156 | narrower muddier FAP-branch strip with off-centre crown, two puddles and a worn patch |
| `RoadCrown_ApproachWorn` | `(0.00, 21.50, 0.00)` | 9 | 168 | worn patchy zirat-approach strip with clods and one restrained puddle |
| `MuddyShoulder_Left` | `(-3.25, 0.00, 0.00)` | 10 | 194 | left wet shoulder, clay breaks, clods and pockets |
| `MuddyShoulder_Right` | `(3.25, 0.00, 0.00)` | 10 | 194 | independent right wet shoulder variation |
| `RoadsideDitch_Left` | `(-4.75, 0.00, 0.00)` | 10 | 260 | left shallow uneven drainage channel and water pockets |
| `RoadsideDitch_Right` | `(4.75, 0.00, 0.00)` | 10 | 260 | independent right drainage channel and bank breakup |
| `CulvertStoneCrossing` | `(0.00, 3.60, 0.00)` | 13 | 248 | humble cap stones and wing stones across the ditch cue |
| `GrassSedgeMass_Left` | `(-6.15, -2.10, 0.00)` | 18 | 180 | low left side-band sedge and faceted grass |
| `GrassSedgeMass_Right` | `(6.15, 2.30, 0.00)` | 18 | 180 | low right side-band sedge and faceted grass |
| `FernShrubBreak_Left` | `(-6.55, -9.60, 0.00)` | 25 | 242 | low left fern/shrub break with open road-side edge |
| `FernShrubBreak_Right` | `(6.45, 9.70, 0.00)` | 25 | 242 | low right fern/shrub break with open road-side edge |
| `RoadFenceBreak_Low` | `(-5.65, 8.20, 0.00)` | 9 | 132 | short low broken fence, gap, posts and moss cue |

## Geometry and material policy

- Geometry is primary: faceted plane changes, a low uneven crown, broken mud
  depressions/puddles, broken shoulders, shallow drainage, and clod/stone
  contact breakup avoid a flat rectangular road slab or paired rails.
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
- The kit contains no text, signs, religious or ethnic markers, fantasy/horror
  iconography, neon, camera, light, collision/navigation/interaction/physics
  object, runtime script, or collision-suffixed mesh.
- The host scene remains authoritative for route floor, collision, navigation,
  interaction, camera, runtime state, visibility, and wet-weather effects.

## Verification evidence

Pinned generator: **Blender 4.5.12 LTS**. Pinned import/readback: **Godot
4.7.1.stable.mono.official.a13da4feb**. The generator's first run baked all
177 meshes; a second run rebuilt/baked only the 29 active road meshes and
produced the same GLB SHA. No demo, visible window, audio, or full capture was
launched.

Generator/source validation:

```text
root: URMAN_WetVillageRoadKit (EMPTY)
direct components: 13; mesh children: 177; source triangles: 2718
images: 0; cameras: 0; lights: 0; collision/physics objects: 0
axis bake: legacy-godot-extractor-compensation-v1
mesh child transforms after bake: 177 neutral
linear rut ribbons: 0
```

Active road source profile before axis bake (legacy Blender coordinates):

```text
RoadCrown_SunkenWet: meshes=11 triangles=280 z=0.018000..0.091879
RoadRuts_PuddleNear: meshes=9 triangles=153 z=0.023008..0.061011
RoadRuts_PuddleFar: meshes=9 triangles=153 z=0.022082..0.065340
ROAD_AABB_LOCAL: x=-3.359392..3.322560 y=-14.000000..14.000000 z=0.018000..0.091879
ROAD_MAX_VERTICAL_RELIEF: 0.073879 m
```

The following Godot AABBs were read from a clean headless import of the
published GLB. `raw_aabb` is the imported legacy basis; `extractor_aabb` is
the same component after emulating the existing runtime `+90° X` correction
with preview translation removed. The latter is the runtime-facing contract:

| Component | Raw Godot size (X,Y,Z) | Extractor/runtime size (X,Y,Z) |
|---|---:|---:|
| `RoadCrown_SunkenWet` | `(6.681952, 28.000000, 0.073879)` | `(6.681952, 0.073879, 28.000000)` |
| `RoadRuts_PuddleNear` | `(3.494151, 9.337228, 0.038003)` | `(3.494151, 0.038003, 9.337228)` |
| `RoadRuts_PuddleFar` | `(3.417549, 9.505680, 0.043258)` | `(3.417549, 0.043258, 9.505680)` |
| `MuddyShoulder_Left` | `(2.450000, 27.200001, 0.220000)` | `(2.450000, 0.220000, 27.200001)` |
| `MuddyShoulder_Right` | `(2.450000, 27.200001, 0.220000)` | `(2.450000, 0.220000, 27.200001)` |
| `RoadsideDitch_Left` | `(2.992633, 26.900000, 0.220000)` | `(2.992633, 0.220001, 26.900000)` |
| `RoadsideDitch_Right` | `(2.998052, 26.900000, 0.220000)` | `(2.998052, 0.220001, 26.900000)` |

The clean Godot probe also reported an identity imported authored-root basis,
identity bases on all 13 direct component roots, identity mesh-child local
transforms for all 177 meshes, and no cameras/lights/collision nodes. Runtime
road widths are 3.418–6.682 m where the crown/rut corridor is present; runtime
longitudinal extents are 9.337–28.000 m. The active road's extracted local
vertical relief is at most 0.073879 m; the shoulder/ditch limits are 0.220001
m, all below the 0.25 m gate.

Generator idempotence (derived GLB):

```text
run 1 GLB SHA-256: 7ede6958c5708f4d748d25cbef417dbde7961904a9192dc1f8c4d2bcb548fed3
run 2 GLB SHA-256: 7ede6958c5708f4d748d25cbef417dbde7961904a9192dc1f8c4d2bcb548fed3
GLB SHA MATCH: True
```

Artifact sizes and SHA-256:

```text
1937622 bytes  assets/source/blender/act1/urman_wet_village_road_kit.blend
332956 bytes   game/assets/models/act1/urman_wet_village_road_kit.glb
3714fc5daf998116cdde76babef2403a08768c459c697a30e8cfcb51335be620  assets/source/blender/act1/urman_wet_village_road_kit.blend
7ede6958c5708f4d748d25cbef417dbde7961904a9192dc1f8c4d2bcb548fed3  game/assets/models/act1/urman_wet_village_road_kit.glb
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
