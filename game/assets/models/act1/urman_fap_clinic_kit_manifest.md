# URMAN Act I FAP Clinic Kit

Status: **authored geometry-only presentation pass v4; runtime, first-person,
medical/local-context and final art review remain open**

This kit is a specific modest rural medical-point landmark for the connected
Act I world. It replaces the isolated test-room/generic-box read with a full
Painterly Low-Poly exterior silhouette: a compact gabled clinic facade with a
covered entry porch, windows and service door; a restrained service yard with
shed, fence and gate; a bench and two blank boards; a secondary rain awning;
an uneven wet path with puddles; and a birch/shrub edge mass. Its single
`FapInteriorSet` component preserves the accepted five compact clinic
silhouettes and adds four restrained wall/utility cues: an examination cot,
folding privacy screen, wall medicine cabinet, enamel instrument trolley/basin
stand, waiting bench, wall radiator and pipes, open supply shelf with varied
blank containers, blank examination chart, and coat hook rail.

## Deliverables

- Source: `assets/source/blender/act1/urman_fap_clinic_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_fap_clinic_kit.glb`
- Root: `URMAN_FapClinicKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Front convention: local **−Y faces the entry/approach front**.
- Source meshes: **255** presentation meshes; **8,438** source triangles.
- Materials: **25** basic muted wet-weather node materials.
- Source images: **0**; image-texture nodes: **0**.

The kit is authored as independent presentation geometry. It contains no
collision, navigation, interaction, camera, light, physics, runtime script or
image-texture owner. Every visible mesh is an LOD0 presentation mesh with a
direct component-root parent.

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
| `FapPathPuddleCluster` | `(0.00, -4.50, 0.00)` | 11 | 576 | Uneven path core/fork, five shallow puddle surfaces and four edge stones |
| `FapBirchShrubMass` | `(9.25, 3.40, 0.00)` | 22 | 668 | Three pale birch trunks with bark marks, branches, faceted crowns and low shrubs |
| `FapInteriorSet` | `(0.00, 0.00, 0.00)` | 75 | 2,052 | Nine restrained interior silhouettes: accepted cot, folding privacy screen, medicine cabinet, enamel trolley/basin and waiting bench, plus radiator/pipes, open supply shelf and blank containers, blank examination chart, and coat hook rail |

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
only. It preserves all 50 accepted baseline meshes and adds 25 small
chamfered/tapered/faceted meshes using muted sage, enamel, dusty blue and dark
timber materials. The source preview coordinates keep the cot and screen to the
left/rear, cabinet and supply shelf on the rear wall, radiator/chart on the
side wall, hooks on the front wall, trolley to the right and bench at the
near-right edge, leaving a clear center path to the Naila and document targets.
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
SOURCE mesh_count: 255
SOURCE mesh_triangles: 8438
SOURCE material_count: 25
SOURCE image_count: 0
SOURCE image_texture_nodes: []
SOURCE collision_like_names: []
SOURCE physics_like_types: []
SOURCE all_mesh_names_have_lod0: True
```

Key source AABBs (metres, source scene coordinates including preview-board root offsets):

```text
FapFacade_Main meshes=42 triangles=1150 aabb_min=(-4.8479, -3.7100, 0.0000) aabb_max=(4.8460, 3.6100, 4.7500)
FapEntryPorch meshes=18 triangles=488 aabb_min=(-2.0200, -5.5700, 0.0100) aabb_max=(2.0200, -2.9800, 3.2000)
FapServiceShed meshes=17 triangles=456 aabb_min=(5.1986, -0.1050, 0.0000) aabb_max=(8.9697, 2.9600, 3.2434)
FapInteriorSet meshes=75 triangles=2052 aabb_min=(-5.6600, -5.5400, 0.0200) aabb_max=(5.4400, 5.6400, 2.6400)
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
GLB mesh_count: 255
GLB mesh_triangles: 8438
GLB material_count: 25
GLB image_count: 0
GLB image_texture_nodes: []
GLB collision_like_names: []
GLB physics_like_types: []
GLB all_mesh_names_have_lod0: True
```

Key imported GLB AABBs match source:

```text
FapFacade_Main (-4.8479, -3.7100, 0.0000)..(4.8460, 3.6100, 4.7500)
FapEntryPorch (-2.0200, -5.5700, 0.0100)..(2.0200, -2.9800, 3.2000)
FapServiceShed (5.1986, -0.1050, 0.0000)..(8.9697, 2.9600, 3.2434)
FapInteriorSet (-5.6600, -5.5400, 0.0200)..(5.4400, 5.6400, 2.6400)
```

The local −Y entry approach remains open: the porch rail gap is **2.7500 m**,
the centered door-frame clear width is **1.2600 m**, and the service shed's
nearest geometry is **5.1986 m** from the facade centerline. No key mesh has
zero area; all unchanged component children compare equal at the
mesh/name/transform/material snapshot level against the pre-pass source.

Artifact sizes and SHA-256:

```text
2882303 bytes  assets/source/blender/act1/urman_fap_clinic_kit.blend
592532 bytes   game/assets/models/act1/urman_fap_clinic_kit.glb
2e65174895e461a1fdf50dcb604eaaf669c084625371d0f6cb8b2bb106f5e3fe  assets/source/blender/act1/urman_fap_clinic_kit.blend
73aa52f7204a2423e22081eaf719700abd9d10c8edf1f5e9126ea52e4603e854  game/assets/models/act1/urman_fap_clinic_kit.glb
```

Generator idempotence (two consecutive runs):

```text
GLB SHA run 1: 73aa52f7204a2423e22081eaf719700abd9d10c8edf1f5e9126ea52e4603e854
GLB SHA run 2: 73aa52f7204a2423e22081eaf719700abd9d10c8edf1f5e9126ea52e4603e854
equal: True
```

The connected-world `BuildFapClinic` presentation pass extracts only the
`FapInteriorSet` direct root from this GLB. It preserves the imported ancestor
basis, places the component at the FAP interior source origin, suppresses only
legacy replacement furniture visuals, and leaves the existing floor/walls,
collision, Naila, document targets and RuntimeBridge ownership unchanged.

Godot import/runtime, connected-world placement, collision/navigation,
first-person visual review, target-hardware performance and medical/local
cultural approval are intentionally not claimed by this asset-only pass.
