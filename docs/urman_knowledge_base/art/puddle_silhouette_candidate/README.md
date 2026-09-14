# Puddle silhouette candidate v1

Status: **RETIRED as a shipped-look question — diagnostic numbers only; the faceted mesh was never needed because the authored wet kit ships (see below). Art-lock quality of the shipped wet read is still human.**

Each 1 920 × 1 080 contact sheet compares the existing flat `CylinderMesh` puddle proxy with a test-only irregular shallow `ArrayMesh`. The source view is on the left, the candidate view is on the right; the upper row is the fixed first-person overview and the lower row is a close diagnostic view. The material reference and source `roughness_value=0.90` are identical on both sides.

- Exact relief-collider contact: **PASS** (±0.005 m, source and candidate)
- Candidate mesh operations: **15/15** in-memory `Mesh` replacements; no colliders added
- Source scenes, shader, PainterlyMaterialLibrary, roughness, saves and narrative state: **unchanged**
- Diagnostic PNGs: **3/3**

## Superseded as shipped-look evidence — 2026-09-14

Measured, not assumed. `PainterlyEnvironmentDetails.AddPuddleCluster` is called
from one place only, `StyleBenchmarkZone` (six clusters: PuddleNear, PuddleMiddle,
PuddleFar, BoundaryWetPatch, ZiratWetPatch and the day-street road set). Every
cluster carries the meta `puddleGeometry = low-poly-overlap-proxy`, and
`Act1ConnectedWorld.ApplyLogicalZonePresentationSuppressions` hides every child
with that meta in `village_day`, `zirat_road` and `kara_urman_night` — the only
zones where they exist. So the shipped Act I contains **no** low-poly cylinder
puddle at all, and the `OPEN-roughness-review` marker at
`PainterlyEnvironmentDetails.cs:65` is attached to geometry the player never sees.

What ships instead is the authored wet village road kit
(`urman_wet_village_road_kit.glb`, 15 named components including
`RoadRuts_PuddleNear`, `RoadRuts_PuddleFar`, `RoadsideDitch_Left/Right`,
`RoadCrown_SunkenWet`, `MuddyShoulder_Left/Right`), mounted across Arrival,
MainStreet, ConnectiveStreetReturn and ZiratMemoryField by
`Act1ConnectedWorld.BuildAct1WetVillageRoadKit` and graded in
`RebindWetVillageRoadMaterials` to explicit painterly responses:

| Kit family | Surface response | Roughness | Specular | Wet grade |
|---|---|---|---|---|
| `Puddle_MutedGlint`, `Puddle_ShallowBlueGreen`, `Ditch_StillWater` | `water` | 0.38 | 0.45 | 0.98 |
| `MuddyShoulder_*`, `Ditch_DampGreenBrown` | `wet_ground` | 0.56 | 0.32 | 0.94 |
| `WetRoad_RutDark`, `WetRoad_MutedOchre`, `WetRoad_WornLight` | `earth` | 0.78 | 0.16 | 0.70 |

`snow_road` / `snow_ground` (roughness 0.68 / 0.90, wet grade 0) own the snow
blanket over the same corridor, so in winter the wet read is the dark glint in
the ruts, the damp shoulder break and the ditch line — visible in
`../style_frames/godot_day_street_1080p.png` and in the route frames.

Consequence: the question this candidate exists to answer ("select the faceted
puddle mesh instead of the cylinder plate") no longer needs answering — a
different authored solution shipped, and the cylinder proxy is hidden. The
contact-sheet numbers above stay valid as diagnostics of a retired proxy. What
stays human is whether the shipped wet read is *convincing* at player height and
in motion.

The candidate intentionally has no bottom face or vertical cylindrical side wall. It remains a bounded silhouette experiment: visual assessment must still decide whether it reads as damp, avoids a plate/rim silhouette, survives traversal, and stays compatible with the Painterly Low-Poly 3D style bible.

## Verification

Run `./eng/capture-puddle-silhouette-candidate.sh`. The wrapper builds the C# project, runs the real Metal/Forward+ capture, pins benchmark-scene hashes, validates exact owner/contact samples, confirms material/collision isolation and decodes the output PNG dimensions/SHA-256 values.
