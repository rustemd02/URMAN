# URMAN Act I Village Exterior Kit

Status: **authored presentation-only production candidate v5; full-volume exterior pass with visible joinery and three optional parcel variants; not yet integrated**

This kit replaces raw modular primitives with one reusable, geometry-authored
Painterly Low-Poly exterior family for Act I village composition. The v5 pass
retains the grounded rear/side dwelling construction and three genuinely
different full-volume house/outbuilding/yard parcel variants from the v4 pass,
then adds restrained front-house joinery, an offset shed lean-to silhouette,
and visible gate ironwork/footing. The parcel yards also carry a sparse
contact-composition pass: irregular moss, low shrubs, fallen/leaning branches
and edge sedge stay near authored boundaries while leaving the central approach
readable. It is a
neutral asset handoff, not a connected-world placement, collision, navigation,
interaction, runtime, or first-person art-lock pass.

## Deliverables

- Source: `assets/source/blender/act1/urman_village_exterior_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_village_exterior_kit.glb`
- Root: `URMAN_VillageExteriorKit` with **9 direct roots**: 6 canonical
  components and 3 optional parcel variants.
- Generated snapshot: `.blend` 4,647,489 bytes, SHA-256
  `04f3701d072230a04670be85ae0e3cf1b9bcb85fd7c090bca8753f65575bfde6`;
  `.glb` 1,033,800 bytes, SHA-256
  `f61cff303a0f3253d0c7c0346dbaba5d5110b9261ed5667edb34c90ee0040e8e`.
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Component front convention: local **−Y** faces the facade/yard approach;
  local +X is the component's horizontal axis.
- Mesh policy: 709 authored LOD0 meshes total, 20,558 source triangles
  (2026-09-07 window-trim pass: the transom bar and tapered headboard crown
  were retired from every window — they read as a timber lattice at street
  distance; jambs/rails/mullion/sill remain), no transform
  instances, physics nodes, `-col` meshes, navigation, interaction, or runtime
  scripts.
- Material policy: 17 exported basic materials with muted wet-weather values;
  no image textures or texture files are used or required.
- Source generator: `assets/source/blender/act1/urman_village_exterior_kit.py`

## Published component roots

The six canonical component roots below remain direct children of
`URMAN_VillageExteriorKit`. Each local origin is a ground anchor at local
`(0, 0, 0)`. The non-zero root translation shown below is only a neutral
  source/GLB preview-board offset; a later composition task may place the named
  root independently without rebuilding its local geometry. The v5 artifact also
publishes three optional parcel roots as additional direct children; they are
listed immediately after the canonical table.

| Root | Preview-board offset (m) | Local pivot | Intended reuse | Representative child meshes |
|---|---:|---|---|---|
| `DwellingFacade_TimberPlaster` | `(-9.20, 0.80, 0.00)` | ground anchor; front −Y | recurring village dwelling frontage, Babay/әби yard facade, near/mid street house parcel | `DwellingFacade_Wall_LOD0`, `DwellingFacade_Roof_LOD0`, `DwellingFacade_GableTie_LOD0`, `DwellingFacade_GableVentInset_LOD0`, `DwellingFacade_WindowShutter_00_LOD0`, `DwellingFacade_EaveBack_LOD0`, `DwellingFacade_FoundationRear_LOD0`, `DwellingFacade_ChimneyStack_LOD0`, `DwellingFacade_PorchDeck_LOD0`, `DwellingFacade_WindowWarmInset_LOD0` |
| `OutbuildingShed_Low` | `(-1.00, 0.25, 0.00)` | ground anchor; front −Y | yard shed, firewood/utility outbuilding, low parcel silhouette behind a fence | `OutbuildingShed_Wall_LOD0`, `OutbuildingShed_BackWall_LOD0`, `OutbuildingShed_SideWallLeft_LOD0`, `OutbuildingShed_Roof_LOD0`, `OutbuildingShed_LeanToWall_LOD0`, `OutbuildingShed_LeanToRoof_LOD0`, `OutbuildingShed_FoundationPlinth_LOD0` |
| `FenceSegment_RoughPicket` | `(5.00, -0.55, 0.00)` | ground anchor at segment centre | repeated but independently placed village boundary runs; vary rotation and spacing at composition time | `FenceSegment_Picket_00_LOD0` … `FenceSegment_Picket_06_LOD0`, `FenceSegment_RailUpper_LOD0`, `FenceSegment_RailMiddle_Crooked_LOD0`, `FenceSegment_FoundationStoneLeft_LOD0` |
| `Gate_CrookedTimber` | `(10.50, -0.55, 0.00)` | threshold centre; ground anchor | plain yard threshold or side-road opening; keep as a physical landmark, not a quest marker | `Gate_PostLeft_LOD0`, `Gate_PostRight_LOD0`, `Gate_Header_Crooked_LOD0`, `Gate_Picket_00_LOD0` … `Gate_Picket_05_LOD0`, `Gate_HingeRight_LOD0`, `Gate_LatchPlate_LOD0`, `Gate_PostFootRight_LOD0`, `Gate_ThresholdStoneRear_LOD0` |
| `Woodpile_StackedLogs` | `(14.70, 0.70, 0.00)` | stack footprint centre | modest yard dressing and scale cue beside shed/fence; reusable without turning into a hero prop | `Woodpile_Log_00_LOD0` … `Woodpile_Log_05_LOD0`, `Woodpile_SupportLeft_LOD0`, `Woodpile_SupportRight_LOD0` |
| `Well_YardLandmark` | `(18.20, -0.35, 0.00)` | well ring centre; ground anchor | quiet yard landmark and wayfinding anchor; place sparingly near a house parcel | `Well_YardLandmark_StoneRing_LOD0`, `Well_YardLandmark_WaterInset_LOD0`, `Well_YardLandmark_Roof_LOD0`, `Well_YardLandmark_PostLeft_LOD0`, `Well_YardLandmark_PostRight_LOD0`, `Well_YardLandmark_Bucket_LOD0` |

### Optional parcel roots (retained v4 variant family)

These are full-volume, presentation-only composition candidates rather than
replacement names for the six canonical roots. Each bundles a distinct
dwelling, outbuilding, and yard boundary under one direct root and keeps the
same 1-metre scale and local −Y approach convention.

| Root | Preview-board offset (m) | Meshes | Source-world bounds (m) | Distinguishing read |
|---|---:|---:|---|---|
| `VillageParcel_VariantA_TimberGable` | `(28.00, 0.00, 0.00)` | 113 | `(23.34, -3.92, 0.00)` … `(32.69, 2.19, 4.34)` | timber/gable dwelling, low shed, open rail yard with sparse moss/shrubs |
| `VillageParcel_VariantB_PlasterAnnex` | `(41.00, 0.00, 0.00)` | 130 | `(36.30, -3.85, -0.00)` … `(45.63, 2.57, 4.20)` | compact plaster dwelling, annex/banya outbuilding, tall plank boundary with edge growth |
| `VillageParcel_VariantC_BanyaYard` | `(54.00, 0.00, 0.00)` | 108 | `(49.27, -3.48, 0.00)` … `(58.03, 2.33, 4.34)` | raised-veranda dwelling, utility shed, mixed-repair fence with restrained contact dressing |

Each optional yard adds the same authored composition contract without creating
another direct root: 3 irregular moss patches, 2 low shrubs built from 3
faceted clumps each, 2 restrained branches and 4 sparse three-blade sedge
clusters (23 meshes per parcel). These are presentation-only edge cues with
`VARIANT_COMPOSITION_PASS`; the central approach lane remains intentionally
clear. No generic conifer/cone markers or invented religious/ethnic signage are
used.

## Dwelling facade contract

`DwellingFacade_TimberPlaster` is a full-depth exterior frontage rather than a
single wall card. Its v5 pass keeps the existing local ground anchor and
doorway approach while using a slightly tapered, chamfered plaster volume, an
offset-ridge faceted gable roof with broad front/rear eaves and restrained rake
trim, and a deeper offset porch with deck, step, posts and lean-to roof. It
retains dark timber posts and diagonal braces, a recessed plank door with
handle, front warm/cool windows, and shallow plaster course geometry. The
rear/side foundation plinths, rear timber frame and dim rear window make the
back read as a built volume; a weathered chimney stack/cap provides a distinct
roofline cue. The v5 joinery pass adds a visible gable tie, shadowed attic vent
and two slightly misaligned hinged shutters so the near facade reads as built
and maintained by hand. The warm inset is a basic material surface, not
emissive lighting or a runtime light source. The dwelling contains 60 mesh
children / 1,606 triangles (v4: 56 / 1,494; delta +4 meshes / +112 triangles).

## Yard volume contract

The existing component roots remain independently reusable and keep their
accepted preview-board anchors. The v5 pass adds only local presentation
geometry: `OutbuildingShed_Low` now has a foundation plinth, full rear wall,
both side walls, rear/side roof eaves, a rear batten, and a small offset
utility lean-to with its own roof and corner post (24 meshes / 792 triangles;
v4: 21 / 708); `FenceSegment_RoughPicket` keeps its two foundation stones and
crooked middle rail (15 / 500); and `Gate_CrookedTimber` adds uneven post caps,
a rear brace, a second threshold stone, and visible right hinge, latch plate
and footing stone (20 / 704; v4: 17 / 620). Existing names are preserved, all
new children use the existing basic material library, and no root placement or
collision is introduced.

## Well landmark contract

`Well_YardLandmark` keeps its local ground anchor, front −Y convention and
existing required child names. Its v1 production candidate replaces the
simple ring/roof study with two staggered, irregularly faceted masonry courses,
subtle timber rim rails, two leaning timber posts, a crooked support header, a
small pitched roof with front/back eaves and ridge cap, and an offset axle with
crank handle. A small suspended bucket, bail, and narrow rope provide a
restrained near/mid-distance use cue without adding interaction or physics.
The component uses only the existing basic stone, wood, roof, metal, bark and
dark-water materials; it has no inscription, decorative claim, image texture,
collision, navigation, interaction, or runtime ownership.

## Integration boundary

The later `Act1ConnectedWorld` composition task owns placement, duplication,
rotation, visibility ranges, route continuity, floor/collision ownership,
navigation, interaction targets, scene dressing, material routing and
Godot-side visual review. This asset pass does **not** alter scenes, code,
`RuntimeBridge`, tests, existing kits, registries, or the connected world.

The pinned Blender export and source/GLB structural inspection confirm the nine
direct roots, 494 meshes, 13,776 triangles, 16 materials, zero degenerate
polygons, and no cameras/lights. The GLB hash is stable across a deterministic
rerun; the recorded `.blend` hash is a snapshot because Blender may rewrite
source-container bytes on save. Current C# integration risk:
`Act1ConnectedWorld` resolves and attaches only the six canonical component
names, so it currently ignores the three optional parcel roots. They are not
runtime-placed or collision-owned until a later composition task explicitly
consumes them. No visible game/capture run was performed. Godot 4.7.1 .NET
import, in-engine placement, first-person traversal, target-hardware
performance, cultural review and final art lock remain unverified.
