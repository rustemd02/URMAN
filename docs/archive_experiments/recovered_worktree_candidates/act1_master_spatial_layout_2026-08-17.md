# Act I master spatial layout — 2026-08-17

Status: **PARTIAL / authored-geometry production candidate**. This is the
canonical spatial contract for a later runtime-composition task. It is not an
integrated demo, not a replacement for `Act1ConnectedWorld`, and not an art
lock.

## Scope and production boundary

This slice creates one world-space Blender master asset for the full Act I
route: arrival, the village road and connective streets, Babai's yard and
house exterior, the ФАП approach, the return street, the quiet zirat approach,
the Kara transition and the Kara-Urman forest edge. The geometry is authored
as solid low-poly relief, crowned roads, depressed ditches, raised banks, plot
rise/fall, horizon blockers and simple landmark anchors. The asset is
presentation-only: it contains no gameplay collision and no route state.

Painterly Low-Poly is carried by faceted geometry and flat authored semantic
materials. No new raster textures were added. Far families are separated
low-profile terrain/tree-line or parcel silhouettes with front/back slopes and
a route-centre breathing gap; they do not form a slab, arch or near-camera
occluder. The zirat is intentionally quiet and non-spectacular; the Kara edge
reads as a threshold and horizon change, not as a generic monster set-piece.

The existing `urman_forest_edge_kit.blend` / `.glb` is unchanged. It remains a
separate authored kit to be instanced by a later composition pass around the
Kara edge.

## Artifact evidence

Source and derived paths:

- `assets/source/blender/urman_act1_master_terrain.blend` — 1,658,684 bytes,
  SHA-256 `6634d721b69abbaf55e247776c722d238a166364cb10b41502c1aa0a09837436`.
- `game/assets/generated/urman_act1_master_terrain.glb` — 408,564 bytes,
  SHA-256 `fccc29f946bec22e4ee394c2e8ebeffc3b75591ef4e53c9e02cccb3790770d51`.

The source was saved and exported with pinned Blender
`4.5.12 LTS`. A Blender reopen check found 97 objects, 80 mesh objects, all
10 zone roots and no collision-like names; the maximum Far-family mesh
bounding-box height is 1.45 m. A stdlib GLB structure check found
91 JSON nodes: one asset root, 10 zone empties and 80 independent mesh nodes;
there are 22 flat materials and no collision-like node names. The pinned
Godot `4.7.1.stable.mono.official.a13da4feb` import completed, and a direct
PackedScene probe passed with 92 instantiated nodes, 80 `MeshInstance3D`
nodes, all 10 zone roots and `collision_like=0`.

The corrective neutral Blender preview boards are stored outside the
repository. They use perspective cameras at player eye height `Y=1.65 m`;
each board hides unrelated zone roots so cross-zone overlap cannot disguise the
spatial read:

- `/private/tmp/urman_act1_master_terrain_previews/urman_act1_master_arrival_village_relation.png`
  — 1,422,729 bytes, SHA-256
  `d0340caee44e9feaa2b97955fae496a8bca93eb9b41c57b1c7f989a735f005d1`, grade
  **3.2/5, partial**. `ArrivalKyrla` + `VillageRoad` show the crowned route,
  ditch/bank rhythm, near plot relief and entry anchors; low separated far
  silhouettes frame the horizon without spanning the road.
- `/private/tmp/urman_act1_master_terrain_previews/urman_act1_master_zirat_kara_transition.png`
  — 1,423,406 bytes, SHA-256
  `d9e0a6658165b549d3e65d1f766baf7d1c36f50812badae831228002442c6d49`, grade
  **3.1/5, partial**. Only `ZiratApproach` + `KaraTransition` are visible; the
  quiet ground, route, plain anchor and boundary shift remain readable, with
  separated low horizon pieces and no cross-zone Kara-edge overlap.
- `/private/tmp/urman_act1_master_terrain_previews/urman_act1_master_kara_edge_composed_bend.png`
  — 1,435,847 bytes, SHA-256
  `b91493e2070f9d57d892090eecf8eb483bd8ab4d1bb57123c49b83164ad37a19`, grade
  **3.0/5, partial**. Only `KaraForestEdge` is visible; the bend, threshold
  anchor, road-side breathing room and sideward continuation read. The left
  bank remains a strong framing element and needs first-person runtime review.

Preview camera contracts are explicit: Arrival `(2.8,1.65,28)` →
`(-1,1.55,-8)`, Zirat/Kara `(2.6,1.65,-82)` → `(-1,0.45,-101)`, and Kara bend
`(2.8,1.65,-108)` → `(-5,0.55,-122)`. These are inspection views, not runtime
camera or scene ownership.

These grades are evidence of authored spatial progress only. They do not close
first-person comprehension, runtime seams, target-hardware performance,
cultural review or art lock.

## World-space contract

The asset uses Blender/Godot world units as metres: **1 Blender unit = 1 m**.
The canonical axes are `+Y` up, `+X` across the village/road, and `-Z` forward
from the arrival side toward the zirat and Kara-Urman. The exported asset root
is `URMAN_Act1_MasterTerrain` at `(0, 0, 0)`. Each zone empty is a child of
that root; the mesh families below are children of their zone empty and retain
their listed local coordinates. A later runtime composition should instance
the root at identity under `Act1ConnectedWorld` and preserve the authored
child transforms.

The coordinates intentionally match the current logical Act I world layout:

| Logical contract | World position / relationship |
| --- | --- |
| `HouseOrigin` | `(-28, 0, 0)` |
| `FapOrigin` | `(28, 0, -30)` |
| `ZiratOrigin` | `(0, 0, -70)` |
| `KaraUrmanOrigin` | `(0, 0, -115)` |
| Arrival → house-yard connector | `(-1.8, 0.025, 9.5)` → `(-24.4, 0.025, 1.7)`, width 4.8 m |
| Residential road extension | `(0, 0.025, 8)` → `(0, 0.025, -19)`, width 5.6 m |
| Village → ФАП branch | `(3.8, -8.2)` → `(25.4, -25.8)`, width 4.2 m |
| House → zirat return | `(-28, 4.82)` → `(0, -53.5)`, width 4.4 m |
| Zirat → Kara | `(0, -89.5)` → `(0, -103)`, width 4.0 m |

The master is not a collision or navigation authority. Existing floor/path and
connector collision owners remain authoritative until a separate composition
task decides which authored surfaces are visible and which procedural visual
duplicates are disabled.

## Zone placement and intended camera views

The table gives the exact zone root, world placement, authored route centerline
and intended visual read. Coordinates in a centerline column are local to the
zone root unless marked `world`.

| Zone root | Root origin | Authored route / visual placement | Intended view |
| --- | --- | --- | --- |
| `ArrivalKyrla` | `(0, 0, 0)` | Crowned arrival road local `(0,34)` → `(0.1,27)` → `(-0.2,19.5)` → `(0,12)` → `(0,9.5)`, width 5.4 m; entry anchor local `(2.8,14.6)` | Eye-height arrival looking toward the village road, plus a neutral elevated 3/4 relation board. Keep both road shoulders and the far village silhouette in frame. |
| `VillageRoad` | `(0, 0, 0)` | Residential crown local `(0,9.5)` → `(0.1,3)` → `(-0.55,-4.8)` → `(0.3,-12.5)` → `(0,-19)`, width 5.6 m; crossroad anchor local `(-4.6,-3)` | First-person 180/360° village spine; read house-line continuation, ditch rhythm and the FAP branch without a straight procedural corridor. |
| `BabaiYard` | `(-28, 0, 0)` | Approach crown local `(26.2,9.5)` → `(20,7.5)` → `(12.2,4.7)` → `(3.6,1.7)`, width 4.8 m; porch wayfinding anchor local `(3.8,4.82)` | Turn from the village arrival connector into the yard. The yard rise, banks and porch-facing anchor should frame the house rather than become an isolated test plot. |
| `HouseExterior` | `(-28, 0, 0)` | House lot centered at local `(0,0)`; threshold local `(0,4.82)`; side-bank and foundation relief occupy the near layer | Close first-person porch/side-yard read, with enough peripheral bank and distant house mass to carry the 360° village continuation. |
| `StreetConnective` | `(0, 0, -8)` | Crossroad local `(0,10)` → `(0.8,5)` → `(3.8,-0.2)` → `(7.5,-5)`, width 5.0 m; branch start is world `(3.8,-8.2)` | Elevated and eye-height views of the junction where village road, Babai side and ФАП branch separate. The crossroad pedestal is a spatial anchor, not a quest marker. |
| `FapApproach` | `(28, 0, -30)` | Branch crown local `(-24.2,21.8)` → `(-18,18)` → `(-10,12)` → `(-4,8)` → `(-2.6,4.2)`, width 4.2 m; end is world `(25.4,-25.8)` | Oblique approach to the ФАП/service yard with village horizon retained behind the player-facing bend. No floating UI or medical stereotype is implied by the geometry. |
| `ReturnStreet` | `(0, 0, -36)` | Return crown local `(-28,40.8)` → `(-23,32)` → `(-16,20)` → `(-8,8)` → `(0,-17.5)`, width 4.4 m; start is world `(-28,4.8)`, end world `(0,-53.5)` | A return-facing first-person line from the house side toward the zirat. Banks and distant village/zirat blockers should preserve orientation without turning the return into a new scene. |
| `ZiratApproach` | `(0, 0, -70)` | Quiet crown local `(0,16.5)` → `(0,9.5)` → `(0.2,2)` → `(0,-8)` → `(0,-19.5)`, width 3.9 m; quiet anchor local `(4.2,6.2)` | Low-intensity, respectful view along the approach. The quiet ground, sparse anchor and tree line are the read; no religious caricature, spectacle or monster staging. |
| `KaraTransition` | `(0, 0, -97)` | Boundary crown local `(0,7.5)` → `(0,3.2)` → `(-0.8,-2)` → `(-2,-8)`, width 3.7 m; boundary anchor local `(-4,-5)` = world `(-4,-102)` | Eye-height threshold view from the zirat side into the changing ground and forest horizon. Preserve a calm transition and let the later forest kit supply authored edge mass. |
| `KaraForestEdge` | `(0, 0, -115)` | Composed bend crown local `(0,12)` → `(0,8)` → `(-0.7,3.5)` → `(-2.4,-1.5)` → `(-5,-6.5)` → `(-8,-12)`, width 3.5 m; bend anchor local `(-4.3,-4.4)` = world `(-4.3,-119.4)` | First-person composed bend with left/right horizon continuation and a plain threshold post. The existing forest-edge kit is later placed around the banks; this terrain alone is not the final forest view. |

All ten roots support a near/mid/far visual continuation through independently
named families. `Near` names are the road, ground-relief and landmark reading;
`Mid` names are banks, ditches and plot/yard rise; `Far` names are solid
faceted silhouette blockers or horizon lines. `LOD0`/`LOD1` in this asset are
authored family variants and review labels, not a completed runtime LOD policy.

## Exact GLB node inventory

The exported root is `URMAN_Act1_MasterTerrain`. Its exact zone children are
`ArrivalKyrla`, `VillageRoad`, `BabaiYard`, `HouseExterior`,
`StreetConnective`, `FapApproach`, `ReturnStreet`, `ZiratApproach`,
`KaraTransition` and `KaraForestEdge`. The 80 independent mesh nodes are:

### `ArrivalKyrla`

- `ArrivalKyrla_BanksAndDitches_Mid_LOD0`
- `ArrivalKyrla_DistantVillageSilhouette_Far_LOD0`
- `ArrivalKyrla_DistantVillageSilhouette_Far_LOD1`
- `ArrivalKyrla_LandmarkPedestal_Entry_Near_LOD0`
- `ArrivalKyrla_PlotRiseFall_Mid_LOD0`
- `ArrivalKyrla_RoadCrown_Near_LOD0`
- `ArrivalKyrla_TerrainRelief_East_Near_LOD0`
- `ArrivalKyrla_TerrainRelief_Near_LOD0`
- `ArrivalKyrla_WayfindingPost_Entry_Near_LOD0`

### `VillageRoad`

- `VillageRoad_BanksAndDitches_Mid_LOD0`
- `VillageRoad_DistantForestLine_Far_LOD0`
- `VillageRoad_DistantHouseLine_East_Far_LOD0`
- `VillageRoad_DistantHouseLine_West_Far_LOD0`
- `VillageRoad_LandmarkPedestal_Crossroad_Near_LOD0`
- `VillageRoad_RoadCrown_Near_LOD0`
- `VillageRoad_TerrainRelief_East_Near_LOD0`
- `VillageRoad_TerrainRelief_West_Near_LOD0`
- `VillageRoad_WayfindingPost_Crossroad_Near_LOD0`

### `BabaiYard`

- `BabaiYard_ApproachRoadCrown_Near_LOD0`
- `BabaiYard_DistantParcelBlocker_Far_LOD0`
- `BabaiYard_DistantParcelBlocker_Far_LOD1`
- `BabaiYard_PlotRiseFall_Mid_LOD0`
- `BabaiYard_PorchWayfindingAnchor_Near_LOD0`
- `BabaiYard_YardBanksAndDitches_Mid_LOD0`
- `BabaiYard_YardRelief_Near_LOD0`

### `HouseExterior`

- `HouseExterior_DistantHouseBlocker_Far_LOD0`
- `HouseExterior_DistantHouseBlocker_Far_LOD1`
- `HouseExterior_FoundationCornerAnchor_Near_LOD0`
- `HouseExterior_FoundationRelief_Near_LOD0`
- `HouseExterior_HouseLotRise_Mid_LOD0`
- `HouseExterior_PorchThresholdAnchor_Near_LOD0`
- `HouseExterior_SideBank_Mid_LOD0`

### `StreetConnective`

- `StreetConnective_BanksAndDitches_Mid_LOD0`
- `StreetConnective_CrossroadCrown_Near_LOD0`
- `StreetConnective_CrossroadRelief_East_Near_LOD0`
- `StreetConnective_CrossroadRelief_Near_LOD0`
- `StreetConnective_DistantFapHorizon_Far_LOD0`
- `StreetConnective_DistantVillageHorizon_Far_LOD0`
- `StreetConnective_LandmarkPedestal_Crossroad_Near_LOD0`
- `StreetConnective_WayfindingPost_FapBranch_Near_LOD0`

### `FapApproach`

- `FapApproach_BanksAndDitches_Mid_LOD0`
- `FapApproach_DistantClinicBlocker_Far_LOD0`
- `FapApproach_DistantVillageHorizon_Far_LOD1`
- `FapApproach_LandmarkPedestal_Fap_Near_LOD0`
- `FapApproach_PlotRiseFall_Mid_LOD0`
- `FapApproach_RoadCrown_Near_LOD0`
- `FapApproach_ServiceYardRelief_Near_LOD0`
- `FapApproach_WayfindingPost_Fap_Near_LOD0`

### `ReturnStreet`

- `ReturnStreet_BanksAndDitches_Mid_LOD0`
- `ReturnStreet_DistantVillageBlocker_Far_LOD0`
- `ReturnStreet_DistantZiratTreeLine_Far_LOD0`
- `ReturnStreet_LandmarkPedestal_Return_Near_LOD0`
- `ReturnStreet_RoadCrown_Near_LOD0`
- `ReturnStreet_TerrainRelief_East_Near_LOD0`
- `ReturnStreet_TerrainRelief_West_Near_LOD0`

### `ZiratApproach`

- `ZiratApproach_BanksAndDitches_Mid_LOD0`
- `ZiratApproach_KaraDistantHorizon_Far_LOD1`
- `ZiratApproach_PlainWayfindingPost_Near_LOD0`
- `ZiratApproach_QuietGroundRelief_East_Near_LOD0`
- `ZiratApproach_QuietGroundRelief_Near_LOD0`
- `ZiratApproach_QuietLandmarkPedestal_Near_LOD0`
- `ZiratApproach_QuietTreeLine_Far_LOD0`
- `ZiratApproach_RoadCrown_Near_LOD0`

### `KaraTransition`

- `KaraTransition_BanksAndDitches_Mid_LOD0`
- `KaraTransition_BoundaryLandmarkPedestal_Near_LOD0`
- `KaraTransition_BoundaryPost_Near_LOD0`
- `KaraTransition_ForestHorizon_Far_LOD0`
- `KaraTransition_ForestHorizon_Far_LOD1`
- `KaraTransition_GroundShift_East_Near_LOD0`
- `KaraTransition_GroundShift_Near_LOD0`
- `KaraTransition_RoadCrown_Near_LOD0`

### `KaraForestEdge`

- `KaraForestEdge_BanksAndDitches_Mid_LOD0`
- `KaraForestEdge_BendGroundRelief_Near_LOD0`
- `KaraForestEdge_BendLandmarkPedestal_Near_LOD0`
- `KaraForestEdge_ComposedBendRoadCrown_Near_LOD0`
- `KaraForestEdge_ComposedHorizon_Back_Far_LOD1`
- `KaraForestEdge_ComposedHorizon_Left_Far_LOD0`
- `KaraForestEdge_ComposedHorizon_Right_Far_LOD0`
- `KaraForestEdge_PlainThresholdPost_Near_LOD0`
- `KaraForestEdge_SideBank_East_Near_LOD0`

Every mesh is an independent presentation geometry node with `collision=none`
and `presentationOnly=true` metadata in the Blender source. No node is named
with `collision`, `collider`, `StaticBody3D`, `CollisionShape3D`, `physics` or
`-col` semantics.

## Logical seams and technical boundaries

These are composition seams, not scene or collision boundaries. No runtime
world transition should be added at them by this slice.

| Seam | Exact position / overlap | Later meaning |
| --- | --- | --- |
| Arrival ↔ VillageRoad | world `z=9.5`, arrival endpoint = village start | Blend the entry road into the residential spine; keep both ditches continuous. |
| VillageRoad ↔ StreetConnective | branch junction world `(3.8, -8.2)`; StreetConnective local `(3.8,-0.2)` | Preserve the crossroad as one spatial junction; do not create a duplicated road cap. |
| Arrival ↔ BabaiYard | connector from `(-1.8, 9.5)` to `(-24.4, 1.7)` | Later composition aligns the yard approach to the existing connector path. |
| BabaiYard ↔ HouseExterior | house-side porch/yard band around world `(-28, 4.82)` and yard endpoint `(-24.4, 1.7)` | Place the house kit and yard rise as one readable parcel; avoid a floating house on a flat patch. |
| StreetConnective ↔ FapApproach | world `(3.8, -8.2)` → `(25.4, -25.8)` | Keep branch width and horizon relationship coherent; ФАП content remains a later kit/placement task. |
| HouseExterior ↔ ReturnStreet | world `(-28, 4.82)`; ReturnStreet starts at `(-28,4.8)` | Use a small tolerance band for the 0.02 m authoring rounding; no collider change. |
| ReturnStreet ↔ ZiratApproach | world `(0, -53.5)` | Fade only if the existing runtime composition requires it; visually this is one return line. |
| ZiratApproach ↔ KaraTransition | world `(0, -89.5)` | Start the quiet ground-to-forest terrain shift without a spectacle beat. |
| KaraTransition ↔ KaraForestEdge | authored overlap band world `z=-103` to `z=-105`; Kara edge starts at `(0,-103)` | Later forest-kit placement owns the edge mass; trim or hide duplicate horizon faces at composition time. |
| KaraForestEdge end | logical bend/horizon around world `(-8,-127)` | Review the final bend in first person; this is not a hard world limit. |

The visual master intentionally does not declare a shared navmesh, floor
collider or physics material. The host scene must continue to own those
systems. Any future seam cleanup must be done by changing the composition
selection/visibility or by authoring a follow-up mesh revision, not by adding
hidden collision to this asset.

## Required later runtime placement

The next task that wires this asset into runtime should:

1. Load `game/assets/generated/urman_act1_master_terrain.glb` as a
   presentation root beneath `Act1ConnectedWorld` at identity, preserving the
   world-space coordinates above. If the host uses per-zone instancing, use the
   ten exact zone roots without renaming them.
2. Keep `SharedVillageGround`, `RoadSurface`, zone floor ownership, connector
   paths and existing gameplay collisions authoritative. Do not generate
   `StaticBody3D`, `CollisionShape3D`, navigation or interaction owners from
   this GLB.
3. Compose the unchanged
   `game/assets/generated/urman_forest_edge_kit.glb` around
   `KaraTransition`/`KaraForestEdge` banks. Its source/derived family names and
   material routing remain the forest kit's responsibility.
4. Decide the seam overlap and visibility range for each near/mid/far family,
   then disable only the procedural visual duplicates that would z-fight or
   flatten the authored relief. Do not remove route logic or collision while
   doing that.
5. Route the authored faces through the existing Painterly Low-Poly semantic
   material policy and review the result at eye height, including backward and
   side looks. The neutral Blender boards are not a substitute for this.
6. Validate the complete route in the current first-person corridor and
   destination-facing camera contracts. This slice makes no runtime scene,
   script, collision or gameplay change.

## Remaining content gaps and acceptance gates

- First-person Godot review of all ten zones at near/mid/far distances and
  180/360 degrees is still open.
- The corrective boards now meet the minimum 3/5 eye-height composition bar,
  but the Kara left bank is still a strong side frame and all three boards need
  in-engine 180/360 review before any integration claim.
- Babai's house exterior, ФАП service-yard content and the existing forest-edge
  family are not embedded in this GLB; they must be composed later without
  turning the route into isolated test scenes.
- Runtime seam ownership, procedural-duplicate suppression, material routing,
  target hardware performance and memory budget are unverified.
- The anchor/post vocabulary is deliberately plain. Wayfinding placement and
  cultural review remain open; no religious stereotype or generic monster
  spectacle is accepted as a shortcut.
- Gameplay collision/navmesh/interaction is intentionally absent and must be
  supplied by the host scene under its existing ownership.
- No demo-ready, release-ready or art-lock claim is made by this document.
