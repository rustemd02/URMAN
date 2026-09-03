# Act I complete-village landmark kit — 2026-08-17

Status: **PARTIAL / authored-geometry production candidate; art lock OPEN.**

This asset-only pass establishes a reusable Painterly Low-Poly landmark kit for
the complete Act I village route: arrival, village street, Babai/әби yard,
house parcels, FAP, return street, zirat boundary and the Kara approach. It
does not integrate the kit into the runtime route, change collisions,
navigation, interaction, scene composition or registries.

## Published contract

- Blender source:
  `assets/source/blender/urman_act1_village_landmark_kit.blend`
  (8,221,538 bytes; SHA-256
  `5df27a1bec9e71bd4968c3ba702ae6f5cabbaccb7b1632393acbda7529dd644e`).
- Derived GLB:
  `game/assets/generated/urman_act1_village_landmark_kit.glb`
  (3,186,644 bytes; SHA-256
  `bd5016db2b0e9d9187b6c200dd1564c2333396b0d9b70971b89cb4539f074daf`).
- GLB root: `URMAN_Act1_VillageLandmarkKit`.
- Independent groups:
  `Arrival`, `VillageStreet`, `BabaiYard`, `HouseExterior`,
  `ConnectiveStreet`, `FapExterior`, `ReturnStreet`, `ZiratBoundary`,
  `KaraApproach`.
- Presentation-only contract: no collision, navigation or interaction nodes;
  route floor/path ownership stays with the existing world.

The kit contains three distinct full-volume wooden house families with doors,
windows, foundations, eaves and solid roofs; varied fence and gate families;
the Babai yard shed/outbuilding, well, woodpile, bench and barrel; roadside
parcel supports; a modest FAP exterior with a converted mesh `ФАП` sign; and a
quiet zirat boundary with a low fence, plain threshold markers and planting.
The Kara approach adds varied tree silhouettes, roots, boulders and a plain
side landmark. There are no raster textures, religious spectacle elements,
generic fantasy language or monster/prop staging.

## Corrective preview evidence

The four required neutral Blender previews are outside the repository at
1600×900, using perspective cameras with exact eye height `Z=1.65 m`:

- `/private/tmp/urman_act1_village_landmark_kit_arrival_village_street.png`
  — visible uneven road and wet tracks with near/mid/far houses, fences and
  vegetation; the street reads as a connected continuation.
- `/private/tmp/urman_act1_village_landmark_kit_babai_yard_side_back.png`
  — corrected outdoor yard-side view with visible ground, fence, tree and
  distant parcel roofs. **Partial:** the near house still dominates the left
  half and this is not yet a clean side/back reveal; composition needs a later
  pass.
- `/private/tmp/urman_act1_village_landmark_kit_fap_return_street.png`
  — corrected outdoor camera and front orientation: visible road/wet tracks,
  fence, bench, tree and mid/far village continuation tie the FAP to the
  return street.
- `/private/tmp/urman_act1_village_landmark_kit_zirat_kara_threshold.png`
  — corrected outdoor threshold view with visible ground/road, low quiet
  boundary, plain markers, trees and a readable Kara transition; no wall,
  roof, cemetery spectacle or inside-geometry framing.

The preview files are evidence only, not a runtime traversal capture. Overall
status remains **PARTIAL** because Babai composition is still too near-heavy,
the previews are not Godot traversal proof, and cultural review, route
composition, target-hardware performance and final art lock remain open.

## Verification

Pinned Blender 4.5.12 export and reopen succeeded:

```text
source reopen: 866 nodes, 799 meshes, 67 empties, 28 materials,
               0 collision-like nodes
GLB reopen:    866 nodes, 799 meshes, 67 empties, 28 materials,
               0 collision-like nodes
groups:        all 9 required names present
```

Pinned Godot 4.7.1 .NET import and load probe succeeded:

```text
GLB load: PASS
effective root: URMAN_Act1_VillageLandmarkKit
nodes: 866
meshes: 799
materials: 28
groups: all 9 required names present
collision-like nodes: 0
contract: PASS
```

Godot's file wrapper is named `urman_act1_village_landmark_kit`; the effective
instanced child root retains the required exact root name. The export and
verification used the source generator and temporary verification scripts
outside the repository; no generator or QA image was added to the owned file
set.

## Next owner and gaps

A later composition task should place these groups along the existing connected
Act I route, preserve visible near/mid/far parcel rhythm, tune the Babai camera
and spacing, and assign runtime collision/navigation only through existing
owners. It should also review the FAP sign and zirat threshold in context with
the project's cultural reviewer. This asset pass makes no demo, release,
runtime-art-lock or performance claim.
