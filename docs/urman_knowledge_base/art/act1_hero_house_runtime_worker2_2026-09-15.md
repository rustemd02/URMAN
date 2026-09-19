# HeroHouse 8×7 — runtime integration, worker-2

Date: 2026-09-15. Checkout: main at
`04f039ee95e6c1320d52d61fff3089fad834393c`, with retained uncommitted work.
This is a change/evidence record, not an execution queue or acceptance decision.

## Implemented geometry

`game/scripts/StyleBenchmarkInteriorFactory.cs` now owns the room-local contract
paired with `HERO_HOUSE_CONTRACT` in the existing exterior source. The clear room
is 8×7 m; the outside footprint is 8.4×7.4 m, wall thickness is 0.20 m and the
clear ceiling is 2.60 m. Room origin relative to the facade is
`(1.658, 0.246, -2.634)`. The existing exterior entry position is preserved.

The room is mounted through the facade's actual transform. Furniture is moved
as rigid groups, without scaling; its source transforms are retained for the
diagnostic's dimension comparison. The separately authored PC and character
sizes are retained. The old 12×10 shell and per-member furniture scaling in
`StyleBenchmarkZone.BuildHouseOldPc` are removed.

Seven real wall openings, their glazing and the exterior openings share the
same centres: three front, two rear, one on either side. A closed physical door
prevents walking through the leaf while its route interaction is unavailable.
The stove/flue sits under the existing exterior chimney. The ceiling and room
floor fit below the eaves and above the original threshold datum respectively.

The PC table has its four original legs and a supported lamp. The high cupboard
is lowered into the available ceiling height. A shallow shelf supports the
photograph, leaving room for its existing reverse-side animation. The sewing tin
and radio are seated on the actual tilted chest lid using triangle intersections
and a rigid whole-prop transform. Warm water is placed on the actual flat stove
top, with the bowl's 6 mm bottom offset accounted for.

## Exterior collision and ground contact

`Act1ConnectedWorld.AuthoredKitCollision.cs` now derives contact from the real
triangles of pierced house walls instead of filling window/door openings with a
wall box or opening broad gaps beside unrelated interaction targets. The former
blanket exception for all side/rear `DwellingFacade_` members is removed; it also
made neighbouring non-enterable houses walk-through. Closed leaves and glazing
receive separate contacts.

Exterior authored-kit contacts are disabled while a separate interior is active
and restored on return. This matters especially for the independently authored
FAP room. The prime owns the logical-zone hook and applies it through the same
active-zone path.

`Act1ConnectedWorld.RoadsideGrounding.cs` adds terrain-scribed perimeter plinth
supports under level foundations. The building, threshold and people remain in
place. The new visible support and its contact use the same geometry. Existing
zirat/roadside grounding work is preserved.

The rear-corner minaret discovery now resolves the current HeroHouse corner
members and measures its outward direction from the actual room centre.

## Static evidence completed

Current exterior GLB SHA-256:
`082a2f90497c4e8016600b03d1b05b5a68c57552a1d8981605f7107aee0fd2fc`.

Compared with the prior-worker backup at
`/tmp/urman-worker2-hero-source-before-20260915/urman_village_exterior_kit.glb`:

| Measurement | Result |
| --- | ---: |
| Original nodes | 871 |
| Current nodes | 1,016 |
| Original nodes preserved | 871 |
| Original nodes missing or changed | 0 |
| Added HeroHouse nodes | 145 |

The comparison covered node transforms/extras, named hierarchy, material names
and actual accessor bytes, including indices and vertex attributes. Added
HeroHouse children were excluded only when comparing the shared parent's child
list. No Blender export was repeated in this worker's runtime-integration pass.

The GLB contains one HeroHouse chimney stack and one cap. Its horizontal stack
centre `(-1.492, -3.084)` agrees with the room origin plus stove position.
`git diff --check` passed on the modified tracked runtime/test files.

The first shared C# build found an unavailable property name on
`ConcavePolygonShape3D`; it was corrected from `BackfaceCollisionEnabled` to the
installed binding's `BackfaceCollision`. The next shared build passed with zero
errors and zero warnings, as reported by the prime.

## First runtime evidence and corrective slice

The existing `res://tests/act1_boundary_architecture_smoke_test.tscn` now checks
the seven window alignments, room mounting and spawn transform, unchanged
furniture dimensions, chimney alignment, floor settling, ordinary movement to
PC/people/photo/stove and back, closed-door and side-wall pushes, and exterior
collision restoration. It retains checks for neighbouring facades, FAP, shed,
mosque and yard supports. New foundation samples are compared with real terrain
physics, not only the analytic height function.

`res://tests/act1_roadside_geometry_smoke_test.tscn` remains the existing test for
the zirat stones, road shoulders, ditch and physical crossing in both directions.

Capture environments, each requiring its own existing empty absolute directory
outside the checkout and a real renderer:
`URMAN_BOUNDARY_ARCHITECTURE_OUTPUT` and `URMAN_ROADSIDE_OUTPUT`.

The first real Metal run returned exit 1: 474 recorded checks and 52.92 m of
ordinary movement. Full evidence is under the prime's approved path
`docs/production/act1_takeover_geometry_evidence_2026-09-15/first_run/`.

All seven window centres matched. All 92 tested furniture members retained
their dimensions. Chimney horizontal error was 0.00000024 m. Closed-door and
side-wall pushes stopped the ordinary controller. Entry, return, stove and
photograph approaches were reachable, but the centre/PC/Rinat route had four
stalls. The hidden outside terrain still collided with the player above the
room's floor; the corrective slice now disables and restores only explicitly
tagged exterior terrain/architecture owners alongside the exterior kit.

The first run also found the mosque's closed door lacked collision and the
previous wall proxies had been shortened around their midpoint, leaving a
0.5 m gap at the bottom. The corrective slice rebuilds those contacts from the
full visible walls and adds the closed door and entrance step.

Foundation rays originally stopped on other objects before reaching the actual
terrain. The correction excludes those other body IDs from this diagnostic's
ray only; it preserves the original 5 mm maximum-gap condition. It does not
disable scene objects to obtain a result. Controller wall contacts now accept
an intervening plinth/leaf/corner only after an independent ray confirms the
slide point is within 55 mm of that same building's visible source member.
Stalls now record the exact collider, source mesh and contact normal.

The remaining FAP approach and MainStreet return stalls require the next
run's collider evidence. This document does not claim the corrective slice
has passed its runtime checks yet.

The entry and reverse screenshots were actually inspected. The room has the
expected roof/furniture scale and supported table/stove/chest composition;
plain grey glazing and weak curtain/sill presentation remain an art limitation.
No art, human, audio, accessibility or 60-active-minute acceptance gate is closed.

## Integration ownership and remaining review

The prime integrates HeroHouse component selection, real room/world spawn,
logical-zone collision switching, Rinat, photograph/tin discoveries and the
warm-water anchor in shared files. The authored source/GLB, global registry,
compiled packs and shared queue have not been regenerated or updated here.

One additional collision-ownership issue was found while reading the FAP:
`Act1ConnectedWorld.FapExploration.cs` already adds a source-linked service-shed
body. The corrective builder now detects this existing source owner and does
not add a second service-shed body.

## Approved subsequent small-space source preparation

The prime subsequently assigned three optional functional spaces: a modest
yard loft, a low passage beneath its deck, and a service pocket in the FAP.
Their content IDs were confirmed through the prime as
`discover-shed-loft-roofline`, `discover-underdeck-rattle` and
`discover-fap-service-cabinet`, under the existing interaction/knowledge prefixes.

The Blender source now contains an independent `HeroYardShed_Loft` component.
Its 4.4×4.4 m footprint, 1.46 m loft, 1.22 m clear low passage and fixed seven-rung
ladder are metre-scaled. This source is prepared, not exported or mounted yet.
The bounded `--component-only hero-yard-shed` branch preserves existing
components instead of reauthoring the 1,016 previously exported nodes.
Python AST parsing and `git diff --check` passed without launching Blender.
The new traversal/runtime spaces remain pending the released post-correction
implementation slice; their existence is not asserted from source alone.
