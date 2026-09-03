# URMAN Act I Kara-Urman Forest-Edge Kit

Status: **authored presentation-only production candidate; not art lock and not runtime acceptance**

This kit is a reusable, geometry-authored Painterly Low-Poly forest-edge family
for the Act I Kara-Urman finale. It replaces repeated conical-pine silhouettes,
flat dark walls and proxy-only ground logs with asymmetric side masses, exposed
roots, layered near/mid/far foliage, ground breakup and two distant closure
profiles. The neutral source arrangement keeps a central road window between
the left/right banks and tree masses; later Act1ConnectedWorld composition owns
placement and traversal review.

## Deliverables

- Source: `assets/source/blender/act1/urman_kara_forest_edge_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_kara_forest_edge_kit.glb`
- Root: `URMAN_KaraForestEdgeKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Local front convention: **−Y faces the road-facing front** for the root and
  every published component root.
- Meshes: **156** presentation meshes; **3,144** source triangles.
- Materials: **16** basic muted wet-weather node materials; no image textures.
- Source images: **0**; exported/imported image-texture nodes: **0**.

## Published component roots

Each name below is a direct child of `URMAN_KaraForestEdgeKit`. Each is an
empty ground pivot with independently rebaseable child meshes. Preview-board
translations are source-only arrangement offsets; the later composition task
may zero/rebase, rotate and place each component independently.

| Component root | Source preview offset (m) | Meshes | Triangles | Role |
|---|---:|---:|---:|---|
| `ForestBank_Left` | `(-7.5, 0.0, 0.0)` | 11 | 188 | asymmetrical layered earth bank, roots and moss pockets |
| `ForestBank_Right` | `(7.5, 1.0, 0.0)` | 11 | 188 | independent counter-bank with different lean/value breakup |
| `MixedTreeCluster_Left` | `(-8.0, 3.5, 0.0)` | 11 | 238 | mixed crooked trunks, angled boughs and lobe foliage |
| `MixedTreeCluster_Right` | `(8.0, 5.0, 0.0)` | 11 | 238 | offset mixed cluster with a distinct crown profile |
| `CrookedPineMass` | `(-5.8, 8.2, 0.0)` | 13 | 286 | bent multi-segment pine mass with one-sided crown |
| `BirchEdgeMass` | `(5.8, 9.2, 0.0)` | 16 | 304 | pale slender trunks and muted broadleaf lobes |
| `RootWall_Left` | `(-8.9, 0.8, 0.0)` | 11 | 188 | low undergrowth/root wall with open road-side edge |
| `RootWall_Right` | `(8.9, 2.0, 0.0)` | 11 | 188 | independent low undergrowth/root wall |
| `FallenLogCluster` | `(-2.4, -1.3, 0.0)` | 12 | 276 | three angled authored logs, cut ends and moss caps |
| `MossyBoulderCluster` | `(2.3, -0.8, 0.0)` | 9 | 186 | faceted stone anchors, moss patches and leafy contact |
| `CrookedStump` | `(-5.0, 1.8, 0.0)` | 5 | 122 | ground landmark with cut top and broken branch |
| `DistantForestMass_Low` | `(0.0, 15.0, 0.0)` | 10 | 236 | low far-side silhouette closure with a central road gap |
| `DistantForestMass_Tall` | `(0.0, 18.0, 0.0)` | 25 | 506 | taller lopsided far profiles and layered crown breakup |

## Material and node boundary

Materials are flat, muted node materials named `DampEarth`, `MossyStone`,
`PineBark`, `WeatheredWood`, `CutWood`, `PineFoliage`,
`FoliageBlueGreen`, `BirchBark`, `BirchLeaves`, `Understory`, `RootDark`,
`MossGreen`, `LeafLitter`, `DistantFoliage`, `DistantBlueGreen` and
`DistantBark`. No raster/image texture datablocks or texture nodes are used.

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

2026-09-03 result: `component_count=13 mesh_count=156 triangle_count=3144
material_count=16`; re-exporting the unchanged source is byte-stable
(GLB SHA-256 `28f58eaa61c97010dcee7fb0111f05424aa1a8809b348299cc7e524f2d30d48a`
before and after). The exporter adds no geometry by design: threshold or
silhouette changes must keep the restrained boundary motif, avoid
creature-like silhouettes, preserve the central road sightline, and pass
human 360 review before acceptance.

Blender source reopen command:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --factory-startup /Users/unterlantas/Documents/GitHub/URMAN/assets/source/blender/act1/urman_kara_forest_edge_kit.blend --python /private/tmp/verify_urman_kara_forest_edge_source.py
```

Observed result:

```text
SOURCE root: URMAN_KaraForestEdgeKit
SOURCE component_count: 13
SOURCE missing_names: []
SOURCE wrong_component_parents: []
SOURCE mesh_count: 156
SOURCE image_count: 0
SOURCE image_texture_nodes: []
SOURCE collision_like_names: []
SOURCE camera_count: 0
SOURCE light_count: 0
```

Fresh GLB import from an empty Blender scene command:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --factory-startup --python /private/tmp/verify_urman_kara_forest_edge_glb.py
```

Observed result:

```text
GLB root: URMAN_KaraForestEdgeKit
GLB component_count: 13
GLB missing_names: []
GLB wrong_component_parents: []
GLB mesh_count: 156
GLB image_count: 0
GLB image_texture_nodes: []
GLB collision_like_names: []
GLB physics_like_types: []
```

Source/derived artifact sizes and SHA-256:

```text
assets/source/blender/act1/urman_kara_forest_edge_kit.blend|1909880 bytes
game/assets/models/act1/urman_kara_forest_edge_kit.glb|341184 bytes
4942b7abd97c9bfbf01c8d6eefb123ef0816184d114489bb649bf52267c43ddb  assets/source/blender/act1/urman_kara_forest_edge_kit.blend
28f58eaa61c97010dcee7fb0111f05424aa1a8809b348299cc7e524f2d30d48a  game/assets/models/act1/urman_kara_forest_edge_kit.glb
```

The temporary generator used for this slice is outside the repository at
`/private/tmp/urman_kara_forest_edge_kit_generator.py`; no generator source was
added to the repository. Godot import, runtime placement, first-person review,
target-hardware performance, cultural review and finale readiness are not
claimed by this asset-only handoff.
