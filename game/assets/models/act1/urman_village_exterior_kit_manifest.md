# URMAN Act I Village Exterior Kit

Status: **authored presentation-only production candidate; dwelling facade v2 and well landmark v1; not yet integrated**

This kit replaces raw modular primitives with one reusable, geometry-authored
Painterly Low-Poly exterior family for Act I village composition. It is a
neutral asset handoff, not a connected-world placement, collision, navigation,
interaction, runtime, or first-person art-lock pass.

## Deliverables

- Source: `assets/source/blender/act1/urman_village_exterior_kit.blend`
- Derived artifact: `game/assets/models/act1/urman_village_exterior_kit.glb`
- Root: `URMAN_VillageExteriorKit`
- Scale: **1 Blender unit = 1 metre**; metric scene; +Z is up.
- Component front convention: local **−Y** faces the facade/yard approach;
  local +X is the component's horizontal axis.
- Mesh policy: authored meshes only, LOD0 nodes, no transform instances,
  physics nodes, `-col` meshes, navigation, interaction, or runtime scripts.
- Material policy: 17 exported basic materials with muted wet-weather values;
  no image textures or texture files are used or required.
- Source generator: `assets/source/blender/act1/urman_village_exterior_kit.py`

## Published component roots

Every component root is a direct child of `URMAN_VillageExteriorKit`. Its
local origin is a ground anchor at local `(0, 0, 0)`. The non-zero root
translation shown below is only a neutral source/GLB preview-board offset; a
later composition task may place the named root independently without
rebuilding its local geometry.

| Root | Preview-board offset (m) | Local pivot | Intended reuse | Representative child meshes |
|---|---:|---|---|---|
| `DwellingFacade_TimberPlaster` | `(-9.20, 0.80, 0.00)` | ground anchor; front −Y | recurring village dwelling frontage, Babay/әби yard facade, near/mid street house parcel | `DwellingFacade_Wall_LOD0`, `DwellingFacade_Roof_LOD0`, `DwellingFacade_GableRakeLeft_LOD0`, `DwellingFacade_GableRakeRight_LOD0`, `DwellingFacade_PorchDeck_LOD0`, `DwellingFacade_PorchRoof_LOD0`, `DwellingFacade_DoorPanel_LOD0`, `DwellingFacade_WindowWarmInset_LOD0`, `DwellingFacade_WindowCoolInset_LOD0` |
| `OutbuildingShed_Low` | `(-1.00, 0.25, 0.00)` | ground anchor; front −Y | yard shed, firewood/utility outbuilding, low parcel silhouette behind a fence | `OutbuildingShed_Wall_LOD0`, `OutbuildingShed_Roof_LOD0`, `OutbuildingShed_Door_LOD0`, `OutbuildingShed_FrontBatten_00_LOD0` |
| `FenceSegment_RoughPicket` | `(5.00, -0.55, 0.00)` | ground anchor at segment centre | repeated but independently placed village boundary runs; vary rotation and spacing at composition time | `FenceSegment_Picket_00_LOD0` … `FenceSegment_Picket_06_LOD0`, `FenceSegment_RailUpper_LOD0`, `FenceSegment_RailLower_LOD0` |
| `Gate_CrookedTimber` | `(10.50, -0.55, 0.00)` | threshold centre; ground anchor | plain yard threshold or side-road opening; keep as a physical landmark, not a quest marker | `Gate_PostLeft_LOD0`, `Gate_PostRight_LOD0`, `Gate_Header_Crooked_LOD0`, `Gate_Picket_00_LOD0` … `Gate_Picket_05_LOD0`, `Gate_ThresholdStone_LOD0` |
| `Woodpile_StackedLogs` | `(14.70, 0.70, 0.00)` | stack footprint centre | modest yard dressing and scale cue beside shed/fence; reusable without turning into a hero prop | `Woodpile_Log_00_LOD0` … `Woodpile_Log_05_LOD0`, `Woodpile_SupportLeft_LOD0`, `Woodpile_SupportRight_LOD0` |
| `Well_YardLandmark` | `(18.20, -0.35, 0.00)` | well ring centre; ground anchor | quiet yard landmark and wayfinding anchor; place sparingly near a house parcel | `Well_YardLandmark_StoneRing_LOD0`, `Well_YardLandmark_WaterInset_LOD0`, `Well_YardLandmark_Roof_LOD0`, `Well_YardLandmark_PostLeft_LOD0`, `Well_YardLandmark_PostRight_LOD0`, `Well_YardLandmark_Bucket_LOD0` |

## Dwelling facade contract

`DwellingFacade_TimberPlaster` is a full-depth exterior frontage rather than a
single wall card. Its v2 production pass keeps the existing local ground anchor
and doorway approach while using a slightly tapered, chamfered plaster volume,
an offset-ridge faceted gable roof with broad eaves and restrained rake trim,
and a deeper offset porch with deck, step, posts and lean-to roof. It retains
dark timber posts and diagonal braces, a recessed plank door with handle, one
warm window inset with frame/mullions, one dim cool window, and shallow plaster
course geometry. The warm inset is a basic material surface, not emissive
lighting or a runtime light source.

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

Blender/GLB structural inspection confirms the named roots and geometry are
present. Godot 4.7.1 .NET import, in-engine placement, first-person traversal,
target-hardware performance, cultural review and final art lock remain
unverified.
