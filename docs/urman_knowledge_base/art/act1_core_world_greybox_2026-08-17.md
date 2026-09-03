# Act I core-world greybox — 2026-08-17

**Status: PARTIAL**

This pass establishes one connected first-person presentation envelope for the
Act I route. It is a functional greybox and evidence harness, not an art lock,
demo-ready build, release candidate, or performance pass.

## Scope and owner

The exact owned layer is `Act1ConnectedWorld/Act1CoreWorldGreybox`, created by
`game/scripts/Act1ConnectedWorld.cs`. It is tagged `presentationOnly=true` and
`visualOnly=true` for its visual zones, with explicit no-navigation and
no-interaction policies. The mounted `AgentBExteriorWorld` is the one named
exception: it owns the two explicitly tagged terrain/architecture traversal
bodies; no other visual child may add collision. The layer has eight direct
visual zones:

1. `Arrival`
2. `MainStreet`
3. `BabaiEbiYard`
4. `HouseExteriorApproach`
5. `ConnectiveStreetReturn`
6. `FapExterior`
7. `ZiratMemoryField`
8. `KaraForestEdge`

The layer uses neutral procedural boxes, faceted masses, low banks, shoulders,
fences, gates, trees, roof volumes, and distant silhouettes. It does not add
assets, textures, navigation, interaction names, state metadata, or narrative
code. The Agent B exception adds only its declared terrain/architecture
traversal collision and unified exterior atmosphere; route floors, path and
connector collision, spawns, interaction targets, player, and `RuntimeBridge`
remain authoritative.

## Route envelope by zone

The measurements below are world-space greybox envelopes around the existing
anchors; they describe presentation coverage, not new traversal geometry.

| Zone | Front / forward envelope | Back / return envelope | Greybox contents |
|---|---|---|---|
| Arrival | Road from approximately `(0, 20)` toward `(0, -1.5)`, width `5.6`; west/east parcel shoulders and near houses | Village-facing houses and tree silhouettes continue toward the arrival horizon around `z=45..53` | Two near building masses, two horizon volumes, fences, trees, wet shoulders, village horizon mass |
| Main street | Road from `z=7.5` to `z=-19`, width `5.6` | Back-facing house/barn volumes and parcel gates continue toward the arrival side | Four built volumes, four fences, two gates, sheds, near/far vegetation and distant village mass |
| Babai / Ebi yard | Existing `house_old_pc` placement plus the `arrival-to-house-yard` approach; parcel envelope extends roughly `±11.5` across the approach | Barn, rear-house, tool shed, tree masses and a distant street mass close the yard behind the approach | Two large volumes, four boundary runs, open gate, shed, woodpile, vegetation |
| House exterior / approach | Existing house anchor with the greybox house volume offset `-3.4m` along the authored approach; threshold remains at the existing doorway side | Side outbuilding, back fence, approach fences and street-memory mass frame the return | Full house/roof/porch volume, threshold, window cue, outbuilding, fences and birch |
| Connective street & return | Existing `house-to-zirat-return` connector start/end, with banks and shoulders following the authored route | Built farms, parcel fences, sheds, trees and a low horizon mass continue toward the zirat road | Road envelope, asymmetric banks, four farm volumes, fences, sheds, distinct vegetation silhouettes |
| FAP exterior | Existing `village-to-fap-branch` anchor and branch-facing service approach; clinic mass stays outside the entry corridor | Clinic rear/service barn, service boundary and return-village mass close the opposite view | Clinic exterior/roof/porch, service step, barn, boundaries, gate, vegetation and village mass |
| Zirat memory field | Road envelope from approximately `z=-53.5` to `z=-89.5`, width `4.2`; entry header at `z=-55` | Village memory houses at the near end and forest/memory silhouettes at the far end | Outer enclosure runs at approximately `x=±11.5`, entry runs/header, memory stones, banks, houses and forest masses |
| Kara forest edge / cliffhanger | Existing path approach from approximately `z=-103` to `z=-122.5`, width `3.5`; asymmetric banks and root clusters flank but do not close the path | Village-memory masses and mixed near/mid/far forest silhouettes preserve the return view | Root banks, side landmark fence/post, mixed tree masses, distant forest window masses and one crooked branch |

The route remains one connected composition around the existing anchors. The
envelope is deliberately simple and value-grouped: road, wet/earth shoulder,
built volume, foliage mass, and distance are visually separable without
introducing final painterly materials.

## Visual suppression record

Source scenes remain loaded. The connected-world owner already used narrow
presentation-only visibility suppression for benchmark artifacts that conflict
with the shared composition:

- `zirat_road`: `ZiratFence`.
- `village_day`: `VillageSignPost`, `VillageSignBoard`, `VillageSignArrow`,
  `VillageSignText`, `Pine`, `Birch`, `UtilityPole`, `UtilityCable`.
- `kara_urman_night`: `DistantWindow`, `DistantWarmWindow`, boundary marker,
  post, thread, ribbon, and `Act2Continuation` presentation nodes.
- Imported village landmark framing: `ZiratGreyboxEnclosure`, replaced by the
  authored visual enclosure while leaving route and interaction owners intact.

The one targeted correction from the required read-only visual-mass audit was
also kept narrow: `kara-urman-edge/GeneratedModularKit` is hidden as a
presentation root because its benchmark anchoring occluded the shared branch
envelope when the exterior zones coexisted. The authored Kara scene remains
loaded; the core layer supplies replacement forest-edge silhouettes. This is a
visual composition suppression only, not deletion or gameplay ownership change.

The FAP duplicate-presentation seam is structurally **CLOSED** in the current
connected-world build: the dedicated `FapClinicAuthoredKitPresentation` owns
the exterior landmark, carries one diegetic `ФАП` label on its authored
wayfinding board, while the Agent B village-buildings `Fap_*` family is
suppressed before architecture collision is generated. The local FAP scene
still owns the waiting-room/desk/interactions. This closes only the duplicate
owner; first-person composition, wayfinding comprehension, medical/local-
context review and art lock remain **OPEN**. The rejected Kara–zirat candidate
is out of scope and was not integrated.

The foliage source seam is also structurally **CLOSED**: `AgentB_FoliageKit`
is hidden after template extraction, while deterministic instances remain under
`AgentB_PlantedFoliage`. This prevents the source-library template board from
leaking into the playable world. The planting plan now also carries a bounded
low-contact pass along wet shoulders, yard edges, FAP service parcels, the
zirat transition and the Kara threshold; these are authored sedge/fern/grass/
moss/branch/stump variants outside the route envelope, not a new tree wall.
It does not accept the foliage family as final art; repeated silhouettes,
density, ground contact and 360° review remain **OPEN**.

The atmosphere-owner seam is structurally **CLOSED**: exterior logical zones
route to the single `AgentBEnvironment`/`AgentBSun` owner, while house and FAP
interiors route to their local `WorldEnvironment`; local exterior directional
lights are disabled when Agent B owns the outdoor sky; its rain emitter is
disabled in house/FAP. This prevents stacked global environments without
accepting the resulting lighting as final art. The Kara edge now also has
three presentation-only, low-energy cool bounce cues under that same owner;
they are enabled only for the night exterior and carry no collision,
navigation, interaction or narrative state. This is a bounded value-separation
correction, not a replacement for authored night lighting.
Night readability, warm-window balance, rain/fog composition and human art
review remain **OPEN**.

## Collision and ownership safety

`Act1FullRouteCoreWorldCapture` audits the layer recursively and requires zero
`CollisionObject3D`, `CollisionShape3D`, navigation, or `InteractionTarget`
nodes. The production route, connector collision definitions, spawns/yaws,
player, interaction targets, `RuntimeBridge`, narrative state, and saves were
not modified by this pass. The traversal receipt is a world-space presentation
waypoint audit; it does not simulate player movement or narrative transitions.

## Capture coverage

Owned capture sources:

- `game/tests/Act1FullRouteCoreWorldCapture.cs`
- `game/tests/act1_full_route_core_world_capture.tscn`
- `eng/capture-act1-full-route-core-world.sh`

The harness launches the production `res://scenes/main.tscn`, uses the real
first-person camera and root viewport, and declares 36 frame specs across all
eight visual zones. Coverage includes forward/back views for every zone,
left/right views for Arrival, Main street, Babai/Ebi yard, FAP, zirat, and Kara,
near/mid/far evidence per zone, and a 10-waypoint arrival-to-Kara cliffhanger
receipt.

The single post-correction full capture wrote:

```text
/private/tmp/urman-act1-full-route-core-final.za9Os3/
/private/tmp/urman-act1-full-route-core-final.za9Os3/act1_full_route_core_world_receipt.json
```

The Godot harness itself reported 36 frames, 8 zones, 10 traversal waypoints,
and zero forbidden layer nodes. The external artifact gate correctly rejected
that run because the root viewport image readback was stale: 35 PNGs were byte-
identical to the first frame. That receipt is historical evidence from an
earlier harness revision; a later production run showed that waiting directly
for `RenderingServer.FramePostDraw` could leave the process alive without a PNG.
The harness now uses a bounded `ProcessFrame` settle window and the shell wrapper
adds a 300-second watchdog. No second full capture was run after that correction,
so final image coverage remains an evidence gap rather than a passing gate.

## Remaining greybox to replace later

- Procedural boxes, roof slabs, faceted foliage, banks, fences, and horizon
  masses need authored village architecture, terrain, and forest-edge assets.
- Existing benchmark/imported dressing still needs near-field occlusion review
  in some yard and house views; FAP ownership is now single-owner, but its
  first-person composition review is still open.
- The Kara edge has readable silhouettes but remains a coarse repeated-mass
  approximation, not the final cliffhanger composition.
- No final wet-ground response, rain, lighting, material, audio, narrative, or
  performance treatment was added.
- The route receipt is not a substitute for physical player traversal evidence.

## Open art gates

- Complete the fresh root-viewport capture with the bounded settle/watchdog
  harness and review every generated image.
- Review the single-owner FAP exterior from forward/back/side directions without
  hiding route floors, entry, targets, or runtime state.
- Replace greybox masses with authored assets and validate the 180/360 first-
  person envelope at each checkpoint.
- Run the remaining Act I art acceptance, performance, and production-readiness
  gates. This document intentionally remains **PARTIAL**.
