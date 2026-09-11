# URMAN Act I FAP Clinic Kit

Status: **authored geometry-only Wave25 presentation pass; runtime, first-person,
medical/local-context and final art review remain open**

This kit is a specific modest rural medical-point landmark for the connected
Act I world. It replaces the isolated test-room/generic-box read with a full
Painterly Low-Poly exterior silhouette: a compact gabled clinic facade with a
covered entry porch, windows and service door; a restrained service yard with
shed, fence and gate; a bench and two blank boards; a secondary rain awning;
an uneven wet path with explicitly triangulated puddles; and a birch/shrub edge
mass. Its single `FapInteriorSet` component now carries the authored 12 m
presentation shell and compact clinic silhouettes: an examination cot, folding
privacy screen, wall medicine cabinet, enamel instrument trolley/basin stand,
waiting bench, wall radiator and pipes, open supply shelf with varied blank
containers, blank examination chart, coat hook rail, a quiet wash unit, an
attendant stool, a blank records pinboard, a reception counter, a tall storage
 cabinet and a peripheral partial-height zoning partition. The Wave25
 composition pass rotates the examination cot/privacy screen into a stronger
 mid-room silhouette, aligns the reception counter with the Naila-to-records
 diagonal, moves the full-height storage anchor onto the left wall, and
 grounds the former entry ceiling beam as a front-wall reveal header. The
 second-pass details publish 28 authored LOD0/LOD1 pairs;
the interior composition leaves a clear center aisle and separates the larger
masses to the walls/periphery.

## Deliverables

- Source: `assets/source/blender/act1/urman_fap_clinic_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_fap_clinic_kit.glb`
- Root: `URMAN_FapClinicKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Front convention: local **−Y faces the entry/approach front**.
- Source meshes: **405** presentation meshes (377 LOD0 + 28 LOD1); **12,110** source triangles.
- Materials: **25** basic muted wet-weather node materials.
- Source images: **0**; image-texture nodes: **0**.

The kit is authored as independent presentation geometry. It contains no
collision, navigation, interaction, camera, light, physics, runtime script or
image-texture owner. Every published mesh is an LOD0/LOD1 presentation mesh
with a direct component-root parent; the 28 LOD1 siblings are deterministic
decimated companions of the new detail cluster.

## Published direct roots

Every name below is an empty ground-pivot root and a direct child of
`URMAN_FapClinicKit`. Each component may be extracted, rebased, rotated and
placed independently by a later `Act1ConnectedWorld` composition pass.

| Direct root | Preview-board offset (m) | Meshes | Triangles | Geometry role |
|---|---:|---:|---:|---|
| `FapFacade_Main` | `(0.00, 0.00, 0.00)` | 42 | 1,150 | Full one-storey gabled clinic volume, grounded foundation, deep roof edges, door, service door, muntined windows, eaves and plain gable vent |
| `FapEntryPorch` | `(0.00, -3.45, 0.00)` | 18 | 488 | Covered entry deck, three low steps, posts, rails, deep rain canopy and braces |
| `FapWayfindingBoard` | `(-7.00, -1.60, 0.00)` | 10 | 440 | Tall approach landmark with framed blank neutral face and capped posts |
| `FapServiceShed` | `(7.10, 1.50, 0.00)` | 17 | 456 | Compact asymmetrical utility shed with door, battens, vent slats and stone foundation |
| `FapFenceRun` | `(7.10, -1.20, 0.00)` | 22 | 968 | Uneven low timber boundary with gate gap and short service-yard return |
| `FapGate` | `(7.10, -1.20, 0.00)` | 15 | 660 | Double timber service-yard gate with posts, caps, pickets, braces and header |
| `FapBench` | `(-3.25, -3.15, 0.00)` | 7 | 308 | Plain waiting bench with back, arms, legs and lower brace |
| `FapNoticeBoard` | `(-4.65, -2.95, 0.00)` | 9 | 396 | Broad framed blank notice face on posts with simple cap and foot |
| `FapRainAwning` | `(4.92, 0.85, 0.00)` | 7 | 276 | Secondary metal rain canopy with fascia, gutter, downpipe, braces and drain foot |
| `FapPathPuddleCluster` | `(0.00, -4.50, 0.00)` | 11 | 384 | Uneven path core/fork, five shallow explicitly triangulated puddle solids and four edge stones |
| `FapBirchShrubMass` | `(9.25, 3.40, 0.00)` | 22 | 668 | Three pale birch trunks with bark marks, branches, faceted crowns and low shrubs |
| `FapInteriorSet` | `(0.00, 0.00, 0.00)` | 197 LOD0 + 28 LOD1 | 5,468 + 448 LOD1 | Authored 12 m shell with floor, ceiling, four wall volumes, recessed entry threshold, lower wall band, side-window recess/trim silhouettes and ceiling practical, plus restrained cot, folding privacy screen, medicine cabinet, enamel trolley/basin, waiting bench, radiator/pipes, supply shelf, blank examination chart, coat hook rail, wash unit, attendant stool, blank records pinboard, records desk, reception counter, tall storage cabinet and partial-height zoning partition |

Preview-board offsets are neutral source arrangement only; they are not
runtime world coordinates. The local origin of each root is a ground anchor.

## Geometry and material boundaries

The architectural read comes from layered volume and silhouette rather than a
single wall card: a raised foundation, colored plaster/plinth breaks, dark
timber corners and courses, a faceted overhanging gable roof, front porch
depth, side service access, and a modest utility enclosure. The path/puddles,
bench, boards, gate and birch mass give the landmark near/mid/far dressing
without adding a second world or a gameplay owner.

`FapInteriorSet` is one direct scene-level component and remains presentation
only. It preserves all 50 accepted baseline meshes, adds 25 small
chamfered/tapered/faceted utility/detail meshes, adds 31 authored shell meshes,
adds 13 LOD0 meshes for the reception counter, tall storage cabinet and
partial-height zoning partition, adds the existing shell/joinery and full-
volume records-desk meshes, and adds 28 authored second-pass LOD0/LOD1 pairs
using muted sage, enamel, dusty blue and dark timber materials. The Wave25
source arrangement establishes one entry-to-Naila-to-records diagonal: the
reception counter sits near the Naila sightline, the records desk stays on its
existing rear interaction axis, and the examination cot/privacy screen turn
into the left mid-room. The full-height storage anchor now lands on the left
wall; the radiator/chart stay on the side wall, the expanded hook rail and
grounded header frame the front entry, and the trolley/wash unit remain on the
right periphery. The center aisle and all existing runtime interaction
coordinates remain open.
No collision, navigation, interaction, narrative, text, logo,
cross, religious/ethnic/diagnostic symbol or modern hospital technology is
included.

The two board faces are deliberately blank and neutral. They contain no
lettering, language, religious/cultural mark, medical symbol or real signage;
the board geometry is a wayfinding/notice placeholder for a later reviewed
content pass.

All materials are Blender Principled node materials with flat color and
roughness values. No raster images, external assets, downloaded content,
lights, cameras, collision-like mesh names, physics nodes or runtime data are
present.

## Verification evidence

Pinned Blender 4.5.12 LTS source reopen:

```text
SOURCE root: URMAN_FapClinicKit
SOURCE root_type: EMPTY
SOURCE component_count: 12
SOURCE direct_child_count: 12
SOURCE missing_names: []
SOURCE wrong_component_parents: []
SOURCE mesh_parent_issues: []
SOURCE mesh_count: 405 (377 LOD0 + 28 LOD1)
SOURCE mesh_triangles: 12110 (6194 exterior + 5468 interior LOD0 + 448 interior LOD1)
SOURCE material_count: 25
SOURCE image_count: 0
SOURCE image_texture_nodes: []
SOURCE collision_like_names: []
SOURCE physics_like_types: []
SOURCE all_published_mesh_names_have_lod0_or_lod1: True
SOURCE polygon_arity_bad: []
SOURCE degenerate_triangles: 0 (minimum area threshold 1e-10)
SOURCE interior_detail_lod_pair_count: 28
```

Key source AABBs (metres, source scene coordinates including preview-board root offsets):

```text
FapFacade_Main meshes=42 triangles=1150 aabb_min=(-4.8479, -3.7100, 0.0000) aabb_max=(4.8460, 3.6100, 4.7500)
FapEntryPorch meshes=18 triangles=488 aabb_min=(-2.0200, -5.5700, 0.0100) aabb_max=(2.0200, -2.9800, 3.2000)
FapServiceShed meshes=17 triangles=456 aabb_min=(5.1986, -0.1050, 0.0000) aabb_max=(8.9697, 2.9600, 3.2434)
FapPathPuddleCluster meshes=11 triangles=384 aabb_min=(-1.7984, -11.8984, 0.0000) aabb_max=(1.6704, -4.0000, 0.3200)
FapInteriorSet meshes=197 LOD0 + 28 LOD1 triangles=5468 LOD0 + 448 LOD1 aabb_min=(-5.8100, -5.8100, 0.0000) aabb_max=(5.8100, 5.8100, 3.4100)
```

Fresh empty-scene Blender GLB import:

```text
GLB root: URMAN_FapClinicKit
GLB root_type: EMPTY
GLB component_count: 12
GLB direct_child_count: 12
GLB missing_names: []
GLB wrong_component_parents: []
GLB mesh_parent_issues: []
GLB mesh_count: 405 (377 LOD0 + 28 LOD1)
GLB mesh_triangles: 12110
GLB material_count: 25
GLB image_count: 0
GLB image_texture_nodes: []
GLB collision_like_names: []
GLB physics_like_types: []
GLB all_published_mesh_names_have_lod0_or_lod1: True
GLB polygon_arity_bad: []
GLB degenerate_triangles: 0 (minimum area threshold 1e-10)
GLB interior_detail_lod_pair_count: 28
```

Key imported GLB AABBs match source:

```text
FapFacade_Main (-4.8479, -3.7100, 0.0000)..(4.8460, 3.6100, 4.7500)
FapEntryPorch (-2.0200, -5.5700, 0.0100)..(2.0200, -2.9800, 3.2000)
FapServiceShed (5.1986, -0.1050, 0.0000)..(8.9697, 2.9600, 3.2434)
FapPathPuddleCluster (-1.7984, -11.8984, 0.0000)..(1.6704, -4.0000, 0.3200)
FapInteriorSet (-5.8100, -5.8100, 0.0000)..(5.8100, 5.8100, 3.4100) (197 LOD0 + 28 LOD1)
```

The local −Y entry approach remains open: the porch rail gap is **2.7500 m**,
the centered door-frame clear width is **1.2600 m**, and the service shed's
nearest geometry is **5.1986 m** from the facade centerline. The source and
fresh GLB checks above report no n-gons, degenerate triangles, non-LOD mesh
names or direct-root/parent contract errors.

Current artifact sizes and SHA-256 (2026-09-12):

```text
4132798 bytes  38b8f46bd7e727426407b54bfca0e5da67ce4c497f707ed0f538955689442c9e  assets/source/blender/act1/urman_fap_clinic_kit.blend
857336 bytes  6357dcf203b5fe516df1d3eb8be8a450713aacc2e3d68347342fe0b4e1ae24b3  game/assets/models/act1/urman_fap_clinic_kit.glb
```

The reception counter now has a 6 cm top. Trolley legs extend to wheel tops
at z=.24 m, preserving their former upper contact at z=1.21 m. Existing
component names, mesh count, footprint and collision ownership are unchanged.
The generator validation reports 405 meshes and 12,110 triangles. These hashes
identify this generation; no repeated-export determinism claim is made here.

The connected-world `BuildFapClinic` presentation pass extracts only the
`FapInteriorSet` direct root from this GLB. It preserves the imported ancestor
basis, places the component at the FAP interior source origin, applies a
first-person 0–14 m LOD0 / 10–28 m LOD1 self-fade to the 28 detail pairs,
suppresses only legacy replacement furniture visuals, and leaves the existing
floor/walls, collision, Naila, document targets and RuntimeBridge ownership
unchanged.

Godot import/runtime, connected-world placement, collision/navigation,
first-person visual review, target-hardware performance and medical/local
cultural approval are intentionally not claimed by this asset-only pass.
