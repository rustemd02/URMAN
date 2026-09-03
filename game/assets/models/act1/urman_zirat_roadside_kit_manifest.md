# URMAN Act I Zīrat Roadside Kit

Status: **authored presentation-only geometry candidate; not runtime acceptance,
cultural approval, or final art lock**

This kit replaces primitive-only road/boundary reads with one reusable
Painterly Low-Poly geometry family for the Act I zīrat approach: wet road
shoulders, an authored roadside ditch and culvert stone cluster, a restrained
low fence with an open gate, a non-inscribed low marker grouping, path edges,
birch/shrub framing, and a quiet distant village mass. It is deliberately
neutral and must receive cultural, religious, and local-context review before
final release or narrative placement.

## Deliverables

- Source: `assets/source/blender/act1/urman_zirat_roadside_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_zirat_roadside_kit.glb`
- Manifest: `game/assets/models/act1/urman_zirat_roadside_kit_manifest.md`
- Root: `URMAN_ZiratRoadsideKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Front convention: local **−Y faces the road-facing approach** for the root
  and every published component root.
- Source meshes: **234** presentation meshes; **7,160** source triangles.
- Materials: **24** basic muted wet-weather node materials.
- Source images: **0**; source/imported image-texture nodes: **0**.

## Published direct roots

Every name below is an empty ground pivot and a direct child of
`URMAN_ZiratRoadsideKit`. All authored mesh children are directly parented to
one of these roots, so later Act1ConnectedWorld composition can independently
extract, rebase, rotate, and place each component.

| Direct root | Preview arrangement offset (m) | Meshes | Triangles | Geometry role |
|---|---:|---:|---:|---|
| `WetRoadShoulder_Left` | `(-2.75, 0.00, 0.00)` | 21 | 748 | raised wet road crown, damp mud shoulder, edge clods and grass |
| `WetRoadShoulder_Right` | `(2.75, 0.00, 0.00)` | 21 | 748 | asymmetric counterpart shoulder with subdued wet sheen |
| `RoadsideDitch` | `(3.55, 0.00, 0.00)` | 22 | 552 | eroded ditch profile, shallow water shadow, bank clods and grass |
| `CulvertStoneCluster` | `(3.55, 0.00, 0.00)` | 11 | 436 | low stone wings, dark culvert mouth and mossy stones |
| `ZiratBoundaryFence` | `(0.00, 8.15, 0.00)` | 27 | 444 | quiet weathered picket enclosure with a deliberate gate gap |
| `ZiratOpenGate` | `(0.00, 8.15, 0.00)` | 16 | 304 | two posts and two swung-open low timber gate leaves |
| `ZiratMarkerGroup_Low` | `(0.00, 8.15, 0.00)` | 25 | 684 | five low, plain, non-inscribed stone markers with earth mounds |
| `ZiratMarkerGroup_Far` | `(0.00, 8.15, 0.00)` | 8 | 328 | four smaller, quieter non-inscribed far markers |
| `ZiratPathEdge` | `(0.00, 8.15, 0.00)` | 14 | 676 | relief path core, low edge banks, stones and wet patches |
| `ZiratBirchShrubMass` | `(5.85, 8.15, 0.00)` | 31 | 1,160 | pale birch trunks, broad low-poly crowns and understory |
| `ZiratDistantVillageMass` | `(0.00, 8.15, 0.00)` | 38 | 1,080 | low far houses, muted roofs, tree masses and distant fence line |

The offsets describe a neutral source arrangement that reads as a road
approach toward a small enclosure. They are not runtime world coordinates;
later composition owns placement, duplication, visibility ranges, relief
alignment, walkability and route continuity.

## Geometry and material boundary

The kit uses authored low-poly relief, extruded silhouettes, faceted stones,
low timber segments, tapered branches, simple roof volumes and muted basic
materials. Wetness is communicated through geometry breakup and restrained
roughness/value differences, not raster textures or painted puddle cards.

The source and GLB contain no collision, navigation, interaction, physics,
camera, light, text, calligraphy, religious symbols, image textures, external
assets, or runtime scripts. Mesh names use presentation-only `LOD0` suffixes
and contain no collision-like owner aliases.

## Cultural gate

`ZiratMarkerGroup_Low` and `ZiratMarkerGroup_Far` are intentionally neutral:
plain, low, non-inscribed marker slabs with no crosses, crescents, calligraphy,
dates, names, ornamental religious shapes, or implied grave text. This is a
geometry restraint, not a claim that the depiction is locally correct. Cultural
and religious review with an appropriate local/contextual reviewer is required
before final release, narrative use, or art lock.

## Verification evidence

Source reopen from the saved Blender file:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --factory-startup /Users/unterlantas/Documents/GitHub/URMAN/assets/source/blender/act1/urman_zirat_roadside_kit.blend --python /private/tmp/urman_zirat_roadside_kit_verify.py -- source
```

Observed result:

```text
SOURCE root: URMAN_ZiratRoadsideKit
SOURCE component_count: 11
SOURCE missing_names: []
SOURCE wrong_component_parents: []
SOURCE mesh_count: 234
SOURCE image_count: 0
SOURCE image_texture_nodes: []
SOURCE collision_like_names: []
SOURCE physics_like_types: []
```

Fresh GLB import from an empty Blender scene:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --factory-startup --python /private/tmp/urman_zirat_roadside_kit_verify.py -- glb /Users/unterlantas/Documents/GitHub/URMAN/game/assets/models/act1/urman_zirat_roadside_kit.glb
```

Observed result:

```text
GLB root: URMAN_ZiratRoadsideKit
GLB component_count: 11
GLB missing_names: []
GLB wrong_component_parents: []
GLB mesh_count: 234
GLB image_count: 0
GLB image_texture_nodes: []
GLB collision_like_names: []
GLB physics_like_types: []
```

Source/derived artifact sizes and SHA-256:

```text
assets/source/blender/act1/urman_zirat_roadside_kit.blend|2246409 bytes
game/assets/models/act1/urman_zirat_roadside_kit.glb|522960 bytes
f2b86cad69a8b2a4f64d8f0208b40512699b3e5ccd2d21632dac7a8d7f7e5bf1  assets/source/blender/act1/urman_zirat_roadside_kit.blend
f2f87e87446041689133a4c25c4de23eab575c5cbe0adc8ac7e100a6c6963a3d  game/assets/models/act1/urman_zirat_roadside_kit.glb
```

The temporary generator and verifier live outside the repository under
`/private/tmp`; no generator source is part of this deliverable. Godot import,
runtime placement, first-person traversal, target-hardware performance,
wayfinding, cultural approval, and final art lock remain unverified.
