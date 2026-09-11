# URMAN Act I Kara-Urman Forest-Edge Kit

Status: **authored presentation-only production candidate; not art lock and not runtime acceptance**

This kit is a reusable, geometry-authored Painterly Low-Poly forest-edge family
for the Act I Kara-Urman finale. It replaces repeated conical-pine silhouettes,
flat dark walls and proxy-only ground logs with asymmetric side masses, exposed
roots, layered near/mid/far foliage, ground breakup and two distant closure
profiles. The Wave 3 edge pass adds contact aprons, framing limbs, leaf-litter
transitions, mossy ground contacts and broken far crowns. The Wave 4 threshold
pass adds bent trunk/root families, six varied young spruces, authored
deadwood/leaf-litter transitions and broken mid/far canopy silhouettes without
building a continuous occluder wall. The Wave 15 silhouette pass replaces the
remaining stacked far crowns, low horizon blobs and rounded stone read with
sloped bank shoulders, species-specific side profiles, root plates and a
broken near/mid/far gate tied to authored trunks. The Wave 17 threshold pass
removes the remaining repeated lobe grid from banks, mixed trees, birch,
root-walls, boulder moss and low horizon, replacing it with anchored multi-ring
crowns, trunk-to-crown branches, asymmetrical bank ridges, root forks,
understory fans and far branch frames. The runtime suppression contract also
retains `ForestBank_Right_EarthMound_01` as a low irregular compatibility
profile under the right bank; Act1ConnectedWorld hides that presentation-only
target after mounting. The neutral source arrangement keeps a
central road window between the left/right banks and tree masses; later
Act1ConnectedWorld composition owns placement and traversal review.

## Deliverables

- Source: `assets/source/blender/act1/urman_kara_forest_edge_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_kara_forest_edge_kit.glb`
- Root: `URMAN_KaraForestEdgeKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Local front convention: **−Y faces the road-facing front** for the root and
  every published component root.
- Meshes: **202** presentation meshes; **5,040** source triangles.
- Materials: **16** basic muted wet-weather node materials; no image textures.
- Source images: **0**; exported/imported image-texture nodes: **0**.

## Published component roots

Each name below is a direct child of `URMAN_KaraForestEdgeKit`. Each is an
empty ground pivot with independently rebaseable child meshes. Preview-board
translations are source-only arrangement offsets; the later composition task
may zero/rebase, rotate and place each component independently.

| Component root | Source preview offset (m) | Meshes | Triangles | Role |
|---|---:|---:|---:|---|
| `ForestBank_Left` | `(-7.5, 0.0, 0.0)` | 19 | 396 | asymmetrical rising earth bank, roots, understory fan and splayed shoulders |
| `ForestBank_Right` | `(7.5, 1.0, 0.0)` | 20 | 424 | independent counter-bank with different lean/value breakup, compatibility suppression target and understory fan |
| `MixedTreeCluster_Left` | `(-8.0, 3.5, 0.0)` | 23 | 550 | mixed crooked trunks, young spruce, anchored organic crown and angled boughs |
| `MixedTreeCluster_Right` | `(8.0, 5.0, 0.0)` | 23 | 546 | offset mixed cluster with distinct birch-side crown and young spruce |
| `CrookedPineMass` | `(-5.8, 8.2, 0.0)` | 22 | 590 | bent multi-segment pine mass, anchored organic crown and one-sided profile |
| `BirchEdgeMass` | `(5.8, 9.2, 0.0)` | 17 | 380 | pale slender trunks, young spruce, anchored leaf crown and fine branches |
| `RootWall_Left` | `(-8.9, 0.8, 0.0)` | 11 | 216 | grounded root wall with root fork, litter fan and open road-side edge |
| `RootWall_Right` | `(8.9, 2.0, 0.0)` | 11 | 216 | independent grounded root wall with root fork and litter fan |
| `FallenLogCluster` | `(-2.4, -1.3, 0.0)` | 14 | 316 | three angled authored logs, cut ends and moss caps |
| `MossyBoulderCluster` | `(2.3, -0.8, 0.0)` | 9 | 354 | retained sloped stone anchors, unified by low moss sprays |
| `CrookedStump` | `(-5.0, 1.8, 0.0)` | 5 | 122 | ground landmark with cut top and broken branch |
| `DistantForestMass_Low` | `(0.0, 15.0, 0.0)` | 7 | 240 | low far-side broken ridges and anchored understory fans with central road gap |
| `DistantForestMass_Tall` | `(0.0, 18.0, 0.0)` | 21 | 690 | taller lopsided profiles, varied trunk gate and branch frames |

## Material and node boundary

Materials are flat, muted node materials named `DampEarth`, `MossyStone`,
`PineBark`, `WeatheredWood`, `CutWood`, `PineFoliage`,
`FoliageBlueGreen`, `BirchBark`, `BirchLeaves`, `Understory`, `RootDark`,
`MossGreen`, `LeafLitter`, `DistantFoliage`, `DistantBlueGreen` and
`DistantBark`. No raster/image texture datablocks or texture nodes are used.

## Wave 3 authored pass — 2026-09-04

Pass ID: `wave3-kara-forest-edge-v1`. The in-repository exporter creates this
pass idempotently and validates every declared object before export. It adds
18 presentation meshes: four asymmetric ground aprons, four root-wall contact
lobes, two framing limbs, two fallen-log leaf-litter contacts, two mossy
boulder contacts, two low far-understory lobes and two broken far-crown lobes.
The additions keep the central road aperture open and do not introduce literal
creature silhouettes, inscriptions, religious symbols or generic horror props.
This is a geometry candidate pass; it is not proof of first-person 360-degree
quality or cultural correctness.

## Wave 4 authored threshold pass — 2026-09-05

Pass ID: `wave4-kara-forest-threshold-v1`. The in-repository exporter creates
this pass idempotently and validates every declared object before export. It
adds 56 presentation meshes inside the existing 13 component roots: four
asymmetric bank rises, four bent root fingers, four low deadwood beams, four
leaf-litter/understory patches, eight irregular threshold branches, six young
spruce families (24 meshes: one bent trunk plus three uneven needle tiers
each), four mid-canopy lobes and four deep far-canopy lobes. The profiles are
deliberately varied and keep the central road aperture open; no continuous
occluder, collision, navigation, interaction or route geometry is introduced.
The pass is source-first and is not proof of runtime composition, first-person
Rinat/ending sightline, target-hardware performance or cultural correctness.

## Wave 15 silhouette pass — 2026-09-05

Pass ID: `wave15-kara-forest-edge-v1`. The in-repository exporter removes 28
presentation meshes that read as repeated low/far blobs or stacked distant
crowns, replaces 12 existing crown/stone meshes with irregular side profiles,
and adds 23 authored meshes: four splayed bank shoulders, two root plates, two
connected canopy braces, two near mixed-tree fork crowns, one pine spray, one
birch spray, three broken horizon ridges, four far gate profiles and four
slightly bent far trunks. The direct component contract is unchanged and the
central route aperture remains open by construction. This is an authored
source pass, not proof of first-person composition, reverse sightline, target
hardware performance or cultural correctness.

## Wave 17 threshold cleanup pass — 2026-09-05

Pass ID: `wave17-kara-threshold-v1`. The in-repository exporter removes 44
remaining repeated lobe meshes from the mixed-tree, birch, bank, root-wall,
boulder-moss and low-horizon groups, then adds 21 grounded meshes: two rising
bank profiles, two root forks, four species-specific multi-ring crowns, two
trunk-to-crown branches, four low understory fans, two moss sprays, two far
branch frames and one low irregular `ForestBank_Right_EarthMound_01`
compatibility profile required by the runtime suppression path. The 13 direct
component roots and all existing runtime hooks remain unchanged; the central
route aperture is preserved. The pass contains **202** presentation meshes and
**5,040** source triangles, uses only existing project materials, and remains
open to first-person 360, cultural and art-lock review.

The kit is presentation-only. It contains no collision, navigation,
interaction, physics, camera or light nodes, and no collision-like mesh names.
The host scene remains the owner of walkability, path relief, collision,
navigation, interaction, visibility ranges and runtime integration.

## Verification evidence

Documented reproducible export (ART-006): the runtime GLB is produced from the
authored `.blend` by the in-repo exporter, which validates the component
contract and exports with fixed settings:

```text
.tools/blender/Blender.app/Contents/MacOS/Blender --background \
  --python tools/blender/export_kara_forest_edge_kit.py -- --root <repo>
```

2026-09-05 result from pinned Blender 4.5.12 LTS: `component_count=13
mesh_count=202 triangle_count=4924 material_count=16`, with
`authored_pass=wave3-kara-forest-edge-v1`,
`threshold_pass=wave4-kara-forest-threshold-v1`, `threshold_mesh_count=56`,
`wave15_pass=wave15-kara-forest-edge-v1`, `wave15_mesh_count=35`,
`wave17_pass=wave17-kara-threshold-v1` and `wave17_mesh_count=21`. The
authored passes are idempotent on subsequent exports and the saved
source/derived pair was exported successfully; a second pinned export returned
the same source and GLB hashes. The triangle budget check is `4924 <= 5200`.
Threshold and silhouette changes must keep the restrained boundary motif, avoid
creature-like silhouettes, preserve the central road sightline, and pass human
360 review before acceptance.

The source reopen and fresh GLB structure checks below were run in clean,
one-shot Blender sessions with root-filtered verifier snippets; those
transient snippets are intentionally not part of the repository.

Observed result:

```text
SOURCE root: URMAN_KaraForestEdgeKit
SOURCE component_count: 13
SOURCE missing_names: []
SOURCE wrong_component_parents: []
SOURCE mesh_count: 202
SOURCE image_count: 0
SOURCE image_texture_nodes: []
SOURCE collision_like_names: []
SOURCE camera_count: 0
SOURCE light_count: 0
SOURCE triangle_count: 4924
SOURCE triangle_budget: 4924 <= 5200
SOURCE threshold_geometry_pass: wave4-kara-forest-threshold-v1 (56/56)
SOURCE wave15_geometry_pass: wave15-kara-forest-edge-v1 (35/35)
SOURCE wave17_geometry_pass: wave17-kara-threshold-v1 (21/21)
```

Observed result:

```text
GLB root: URMAN_KaraForestEdgeKit
GLB component_count: 13
GLB missing_names: []
GLB wrong_component_parents: []
GLB mesh_count: 202
GLB image_count: 0
GLB image_texture_nodes: []
GLB collision_like_names: []
GLB physics_like_types: []
GLB triangle_count: 4924
```

Read-only runtime hard-reference scan: `Act1ConnectedWorld.cs` names
`KaraForestEdge/.../ForestBank_Right/ForestBank_Right_EarthMound_01` as a
required presentation suppression target. The imported GLB contains
`ForestBank_Right_EarthMound_01` under `ForestBank_Right`; no other
Kara-descendant mesh path literals were found in that runtime suppression
section.

Source-local component AABBs (min X/Y/Z, max X/Y/Z, metres) after re-opening
the saved `.blend`:

```text
BirchEdgeMass          ( 3.476512, 6.830000, 0.030000) ( 7.687016,12.175087, 5.600000)
CrookedPineMass        (-7.680530, 6.850000,-0.017741) (-2.920000, 9.460431, 5.986000)
CrookedStump           (-6.132267, 0.801272,-0.043481) (-3.948473, 2.630486, 1.460694)
DistantForestMass_Low  (-13.716999,13.478313, 0.020000) (11.892000,17.036800, 2.402800)
DistantForestMass_Tall (-13.869019,17.078848,-0.050838) (13.710725,20.819914, 8.952400)
FallenLogCluster       (-3.925659,-3.368900,-0.102154) (-0.380373, 0.815347, 0.564479)
ForestBank_Left        (-7.181376,-4.220000,-0.135000) (-2.260000, 5.278692, 1.050000)
ForestBank_Right       ( 2.040000,-2.360000,-0.135000) ( 7.181376, 6.278692, 0.970000)
MixedTreeCluster_Left  (-8.962887, 1.530000,-0.016304) (-4.846025, 6.374177, 5.208694)
MixedTreeCluster_Right ( 4.858717, 2.610000,-0.017596) ( 9.344314, 8.164824, 5.762768)
MossyBoulderCluster    ( 0.100000,-2.262695,-0.180000) ( 5.980000, 1.300000, 1.420000)
RootWall_Left          (-8.596672,-2.334687, 0.020000) (-5.190000, 6.384712, 0.740000)
RootWall_Right         ( 5.130000,-1.134686, 0.020000) ( 8.639999, 7.584712, 0.680000)
```

Source/derived artifact sizes and SHA-256:

```text
assets/source/blender/act1/urman_kara_forest_edge_kit.blend|2243022 bytes
game/assets/models/act1/urman_kara_forest_edge_kit.glb|393744 bytes
e50fa5a6f26c1223380fae01c23bd4b775206811838f1acee4dbf215a5621c2b  assets/source/blender/act1/urman_kara_forest_edge_kit.blend
4af39ebd0fd0a4042fd8aaec878213ee7fcc5f564867473fd2ed58f9ba7231da  game/assets/models/act1/urman_kara_forest_edge_kit.glb
```

The exporter and its authored-pass declarations are part of this deliverable;
the transient verifier snippets are not retained. Godot import, runtime
placement, first-person review, target-hardware performance, cultural review
and finale readiness are not claimed by this asset-and-benchmark handoff. The
parent connected-world pass still needs to verify that the existing Kara
placements preserve a clear walkable first-person path and Rinat/ending
sightline in capture, and to measure the added mid/far foliage cost on target
hardware.

## Boulder depth pass — 2026-09-11

Four existing `MossyBoulderCluster_Stone_00..03` meshes now use shoulder rings and faceted caps instead of straight extruded plates. Names, parents, transforms and MossyStone remain intact. A separate idempotent pass follows Wave 17; it does not rerun the earlier cleanup. Source export: 202 meshes, 5,040 triangles, 16 materials, no image textures. Runtime visual acceptance remains open.
