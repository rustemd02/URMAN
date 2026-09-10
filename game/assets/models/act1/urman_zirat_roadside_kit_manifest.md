# URMAN Act I Zīrat Roadside Kit

Status: **authored presentation-only geometry candidate; not runtime acceptance,
cultural approval, or final art lock**

This kit replaces primitive-only road/boundary reads with one reusable
Painterly Low-Poly geometry family for the Act I zīrat approach: wet road
shoulders, an authored roadside ditch and culvert stone cluster, a restrained
low fence with an open gate, a non-inscribed low marker grouping, path edges,
birch/shrub framing, and a quiet distant village mass. The Wave 17 cemetery
edge pass keeps the linked road/ditch and open-gate arrangement, then adds
uneven boundary berms, plain companion markers, and anchored birch windbreak
trunks/canopies so the approach reads as a coherent rural edge rather than
isolated stones. It is deliberately neutral and must receive cultural,
religious, and local-context review before final release or narrative
placement.

## Deliverables

- Source: `assets/source/blender/act1/urman_zirat_roadside_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_zirat_roadside_kit.glb`
- Manifest: `game/assets/models/act1/urman_zirat_roadside_kit_manifest.md`
- Root: `URMAN_ZiratRoadsideKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Front convention: local **−Y faces the road-facing approach** for the root
  and every published component root.
- Source meshes: **168** presentation meshes; **3,308** source triangles.
- Materials: **21** basic muted wet-weather node materials.
- Source images: **0**; source/imported image-texture nodes: **0**.

## Published direct roots

Every name below is an empty ground pivot and a direct child of
`URMAN_ZiratRoadsideKit`. All authored mesh children are directly parented to
one of these roots, so later Act1ConnectedWorld composition can independently
extract, rebase, rotate, and place each component.

| Direct root | Preview arrangement offset (m) | Meshes | Triangles | Geometry role |
|---|---:|---:|---:|---|
| `WetRoadShoulder_Left` | `(-2.75, 0.00, 0.00)` | 11 | 236 | uneven wet shoulder relief, road-band hook, edge clods and short grass |
| `WetRoadShoulder_Right` | `(2.75, 0.00, 0.00)` | 11 | 236 | asymmetric counterpart shoulder with subdued wet sheen |
| `RoadsideDitch` | `(3.55, 0.00, 0.00)` | 12 | 218 | eroded ditch relief, shallow water shadow, bank clods and sedge |
| `CulvertStoneCluster` | `(3.55, 0.00, 0.00)` | 8 | 160 | low stone wings, dark culvert mouth and mossy stones tied to the ditch |
| `ZiratBoundaryFence` | `(0.00, 8.15, 0.00)` | 24 | 496 | quiet weathered picket enclosure, uneven berms, broken braces and restrained peripheral deadwood |
| `ZiratOpenGate` | `(0.00, 8.15, 0.00)` | 14 | 280 | two posts and two swung-open low timber gate leaves |
| `ZiratMarkerGroup_Low` | `(0.00, 8.15, 0.00)` | 17 | 340 | five plain markers plus two companions arranged in three irregular clusters with respectful gaps |
| `ZiratMarkerGroup_Far` | `(0.00, 8.15, 0.00)` | 14 | 280 | four smaller markers plus two companions aligned to the same three-cluster language |
| `ZiratPathEdge` | `(0.00, 8.15, 0.00)` | 8 | 134 | four asymmetric low-poly grade masses, clear curved central path and edge grass |
| `ZiratBirchShrubMass` | `(5.85, 8.15, 0.00)` | 23 | 508 | three birch/shrub masses plus two anchored windbreak trunks with varied crowns and contacts |
| `ZiratDistantVillageMass` | `(0.00, 8.15, 0.00)` | 26 | 420 | low far houses, muted roofs, varied horizon foliage and distant fence line |

The offsets describe a neutral source arrangement that reads as a road
approach toward a small enclosure. They are not runtime world coordinates;
later composition owns placement, duplication, visibility ranges, relief
alignment, walkability and route continuity.

## Geometry and material boundary

The kit uses authored low-poly relief, extruded silhouettes, faceted stones,
low timber segments, tapered branches, simple roof volumes and muted basic
materials. Wetness is communicated through geometry breakup and restrained
roughness/value differences, not raster textures or painted puddle cards.

## Wave 3 base pass (superseded) — 2026-09-04

Pass ID: `wave3-zirat-road-edge-v1`. The in-repository exporter creates this
pass idempotently and validates every declared object before export. It adds
19 presentation meshes: six inward wet shoulder aprons, three roadside reed
beds, two broken boundary-fence braces, three quiet marker leaf-litter
contacts, three birch-edge understory contacts and two low distant-village
ground-closure lobes. The additions contain no inscriptions, names, dates,
calligraphy or religious symbols, and keep the central road and open gate
presentation unobstructed. This is a geometry candidate pass; it is not proof
of first-person 360-degree quality or cultural correctness.

This historical composition is not the accepted source state: its flat path,
repeated marker read and open horizon were replaced by the bounded restrained
threshold pass below. Only the useful edge-contact meshes are retained as source
detail.

## Wave 5 restrained threshold pass (superseded) — 2026-09-05

Pass ID: `wave5-zirat-restrained-threshold-v1`. The in-repository exporter keeps
the 11 direct roots and the retained Wave 3 edge contacts, then replaces the
old path-core/bank/stone composition with four asymmetric low-poly grade
masses and three quiet central path traces. It replaces the former marker
layout with five near and four far plain markers grouped across three
irregular left/right/back clusters, preserving clear respectful gaps and a
central passage. It also replaces the symbolic tree read with three varied
mid-distance birch/shrub masses and six varied far canopy masses, adds three
far ground banks, and places sedge/deadwood only along the ditch, path edge,
and fence perimeter. The exporter declares and validates 72 authored-pass
objects and keeps the pass idempotent. No inscriptions, text, calligraphy,
crosses, crescents, or other religious symbols were added.

This historical pass remains an authored presentation candidate, not proof of runtime
walkability, first-person 360-degree quality, target-hardware performance,
local cultural correctness, or final art lock.

The source and GLB contain no collision, navigation, interaction, physics,
camera, light, text, calligraphy, religious symbols, image textures, external
assets, or runtime scripts. Mesh names use presentation-only `LOD0` suffixes
and contain no collision-like owner aliases.

## Wave 15 linked-masses pass — 2026-09-05

Pass ID: `wave15-zirat-linked-masses-v2`. The in-repository exporter clears the
previous generated mesh children and builds one source-authored composition
under the same 11 direct roots. Road shoulders and the ditch now use uneven
relief ribbons and linked low contact masses; the culvert mouth sits in that
drainage profile. The boundary is an old two-run fence with rails and a clear
open entry, while the gate leaves are swung outward instead of reading as a
flat plate. Low and far markers remain plain, non-inscribed stone silhouettes
in three irregular clusters with an open approach. Birch/shrub contacts and
full far wall/roof village silhouettes supply near/mid/far separation without
forming a repeated row or skyline wall. The pass contains **154** presentation
meshes and **2,964** source triangles; the two exact connected-world material
hooks `WetRoadShoulder_Left_RoadBand_LOD0` and
`WetRoadShoulder_Right_RoadBand_LOD0` are preserved.

All beams, rails, branches, posts, and trunks are project-original faceted
meshes; no repeated cone or block primitives are used. No inscriptions, text,
calligraphy, crosses, crescents, or other religious symbols were added. This
remains a presentation candidate rather than proof of runtime walkability,
first-person 360-degree quality, target-hardware performance, local cultural
correctness, or final art lock.

## Wave 17 cemetery-edge pass — 2026-09-05

Pass ID: `wave17-zirat-cemetery-edge-v1`. The in-repository exporter rebuilds
the same 11 direct roots idempotently and adds two low boundary berms, four
plain companion markers, and eight windbreak meshes (two tapered three-ring
birch trunks, two branches, two irregular anchored canopies and two low
contacts). The companions stay in the existing three neutral marker clusters;
the central road and path clearance are unchanged. No text, inscription,
calligraphy, cross, crescent, ornamental religious shape, external asset or
texture was added. The pass contains **168** presentation meshes and **3,308**
source triangles and remains a candidate for first-person and local cultural
review.

## Cultural gate

`ZiratMarkerGroup_Low` and `ZiratMarkerGroup_Far` are intentionally neutral:
plain, low, non-inscribed marker slabs with no crosses, crescents, calligraphy,
dates, names, ornamental religious shapes, or implied grave text. This is a
geometry restraint, not a claim that the depiction is locally correct. Cultural
and religious review with an appropriate local/contextual reviewer is required
before final release, narrative use, or art lock.

## Verification evidence

Documented reproducible export (ART-005): the runtime GLB is produced from the
authored `.blend` by the in-repo exporter, which validates the component
contract and exports with fixed settings:

```text
.tools/blender/Blender.app/Contents/MacOS/Blender --background \
  --python tools/blender/export_zirat_roadside_kit.py -- --root <repo>
```

2026-09-05 result from pinned Blender 4.5.12 LTS: `component_count=11
mesh_count=168 triangle_count=3308 material_count=21`; Wave 17 rebuilt the
saved source and exported the derived GLB successfully. A subsequent
unchanged-source export returned the same counts and byte-stable GLB SHA-256.
Marker and boundary geometry remains neutral and requires cultural/religious
review before final acceptance.

Source reopen from the saved Blender file:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --factory-startup /Users/unterlantas/Documents/GitHub/URMAN/assets/source/blender/act1/urman_zirat_roadside_kit.blend --python /private/tmp/urman_zirat_roadside_kit_verify.py -- source
```

Observed result:

```text
source root=URMAN_ZiratRoadsideKit
source direct=('CulvertStoneCluster', 'RoadsideDitch', 'WetRoadShoulder_Left',
 'WetRoadShoulder_Right', 'ZiratBirchShrubMass', 'ZiratBoundaryFence',
 'ZiratDistantVillageMass', 'ZiratMarkerGroup_Far', 'ZiratMarkerGroup_Low',
 'ZiratOpenGate', 'ZiratPathEdge')
source components=11 meshes=168 triangles=3308 materials=21
source images=0 cameras=0 lights=0
source degenerate_polygons=0
source roots=11 missing=[] unexpected=[]
```

Fresh GLB import from an empty Blender scene:

```text
/Users/unterlantas/Documents/GitHub/URMAN/.tools/blender/Blender.app/Contents/MacOS/Blender --background --factory-startup --python /private/tmp/zirat_wave4_verify.py -- glb /Users/unterlantas/Documents/GitHub/URMAN/game/assets/models/act1/urman_zirat_roadside_kit.glb
```

Observed result:

```text
glb root=URMAN_ZiratRoadsideKit
glb direct=('CulvertStoneCluster', 'RoadsideDitch', 'WetRoadShoulder_Left',
 'WetRoadShoulder_Right', 'ZiratBirchShrubMass', 'ZiratBoundaryFence',
 'ZiratDistantVillageMass', 'ZiratMarkerGroup_Far', 'ZiratMarkerGroup_Low',
 'ZiratOpenGate', 'ZiratPathEdge')
glb components=11 meshes=168 triangles=3308 materials=21
glb images=0 cameras=0 lights=0
glb degenerate_polygons=0
glb roots=11 missing=[] unexpected=[]
```

Source/derived artifact sizes and SHA-256:

```text
assets/source/blender/act1/urman_zirat_roadside_kit.blend|2072855 bytes
game/assets/models/act1/urman_zirat_roadside_kit.glb|266444 bytes
f1aeb41d3e3dca60d6d05464df1afd5f129e0e384d80f4255e746fc61ff42c9e  assets/source/blender/act1/urman_zirat_roadside_kit.blend
b47d411076c1acfe7d087dc93fb592b2e237aecb8cfc7c3e40da662d1cf32d87  game/assets/models/act1/urman_zirat_roadside_kit.glb
```

The exporter and its authored-pass declarations are part of this deliverable;
temporary verification scripts remain outside the repository under
`/private/tmp`. Godot import, runtime placement, first-person traversal,
target-hardware performance, wayfinding, cultural approval, and final art lock
remain unverified for this pass.
