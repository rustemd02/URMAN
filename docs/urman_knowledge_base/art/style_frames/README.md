# Godot style-frame gate

Capture date: 2026-08-15 (bounded pine-palette presentation pass after lighting/fog, puddle-origin and authored road-relief passes)  
Renderer: Godot 4.7.1 .NET, Forward+, Metal, Apple M4 Pro  
Resolution: 1 920 × 1 080  
Command: `./eng/capture-style-frames.sh`

Current verdict: **rejected as final art lock; retained as the second reproducible in-engine baseline**.

The captures prove the real first-person camera, renderer, fog, lighting, procedural painterly material, imported 1 024 px albedo textures, a more legible CRT/document focus set, deterministic domestic/forest dressing and a repeatable capture pipeline. The latest pass keeps the deterministic environment-kit contract but gives the project-original Blender kit small authored edge breaks, a bounded `HouseA` facade (foundation, door/frame, window trims, porch/step and eave), a layered low-poly pine crown, softened hero-PC/table forms and a bounded OldPc tower/panel/power-button detail pass; procedural trees now use tapered seven-sided foliage tiers with deterministic side boughs instead of repeated inflated spheres. It also places the `HouseA_` module in the day street and three scale/yaw-varied instances of the project-original `PineA_` module at the Kara-Urman edge through the same Godot LOD adapter used by full-game zones. The Chapter 1 benchmark now uses the bounded lighting/fog calibration first proven in `art/style_calibration_candidate/`; it is a presentation baseline, not a global art lock. They do not yet meet the final production lock because:

- the street now has layered fences, background silhouettes, wires, laundry, crates, hay, additional shrubs and one imported HouseA module with a bounded facade pass, but a complete authored village module family and reviewed material variation are still missing;
- the house now has a denser shelf/calendar/radio/herb/CRT presentation and a bounded imported OldPc detail pass, but it still needs the production-grade hero asset, authored documents and reviewed domestic cultural specificity;
- the forest has irregular tapered low-poly tiers, side boughs, undergrowth, fallen logs, boundary charms, restrained fireflies and three varied imported PineA instances, but it still repeats one authored family alongside the greybox crown family and needs a final branch/canopy set;
- the M4 Pro benchmark is recorded separately; representative release-hardware and motion/readability review are still open.

These PNGs must be overwritten only by the same capture command after an intentional art pass. Acceptance requires visual review plus performance evidence; ImageGen concepts or Blender-only renders cannot close the gate.

Texture-candidate note (2026-08-12): six versioned `*_v2_albedo.png` candidates
now have explicit material owners; stone/fabric are bounded to non-interactive
well/forest-marker/rug presentation anchors while the four v1 mappings remain
active elsewhere. Candidate scene captures and six material swatches live in
`../texture_candidate_frames/`; see `../texture_candidates_{generation,technical,qa}.md`.
This does not close the style-frame art lock: geometry density, fog/light
calibration, near/mid/far motion readability, cultural specificity and external
review stay open.

Focused v3 candidate note (2026-08-13): all six versioned candidates
(`weathered_wood_boards_v3`, `aged_plaster_v3`, `damp_earth_v3`,
`pine_foliage_v3`, `mossy_stone_v3` and `old_fabric_v3`) pass the numeric image
gate. The six-material set has a three-scene still capture, a six-swatch sheet
and a separate near/mid/far spatial sweep. Their frames live under
`../texture_candidate_frames/v3/` and `../texture_candidate_motion_sweep/`.
These are temporary presentation clones for comparison only; they do not
activate a runtime material, replace v1/v2 files, or close the art lock.

Camera-sweep evidence note (2026-08-12): `../style_motion_sweep/` contains
three real Metal/Forward+ 1 920 × 1 080 contact sheets. Each sheet covers
near/mid/far camera distances × FOV 65°/75°/90° (27 cells total), with exact
positions in `style_motion_sweep_manifest.json`. This is spatial readability
evidence only; it is not a temporal movement test, texture activation proof or
art-lock acceptance. The sweep confirms the old PC focal read and the
foreground/midground forest separation, while retaining the open greybox,
lighting, fog, hero-asset, cultural and release-hardware gates.

Focused v3 texture-candidate motion note (2026-08-13):
`../texture_candidate_motion_sweep/` contains a separate three-scene sweep
using all six v3 presentation clones, including the stone and fabric anchor
meshes. It repeats the same 27-cell near/mid/far × FOV grid and records clean
real-driver hashes. This is a projection/repetition check only; it does not
activate runtime materials, measure temporal head-bob comfort or close the art
lock.

Authored road-relief pass (2026-08-13): `PainterlyEnvironmentDetails.AddRoadRelief`
now replaces the flat road/path slabs in the day street, Kara-Urman edge,
Zirat road and shared Acts 2–5 `FullGameZone` path. Each surface is a
deterministic 9×28 low-poly grid with a shallow crown, two broken rut families,
and 216 matching collision cells. `RoadReliefQaSmokeTest` passes five benchmark
paths (seven relief paths total); `CollisionQaSmokeTest` also passes the same authored path contract while
traversing all 12 full-game zones. This is a production-candidate geometry
slice, not a final road/level art lock: mesh dressing, puddle/wetness, village
module variety, cultural review and near/mid/far traversal acceptance remain
open. The mesh intentionally leaves the Painterly shader unchanged.

Puddle/wetness geometry candidate (2026-08-14) adds three overlapping
low-poly patches to the day road plus bounded wet patches at the Kara-Urman
boundary and Zirat road. The patches are presentation-only, use the existing
Painterly material contract and carry an explicit `OPEN-roughness-review`
metadata gate; no shader or runtime material owner changed. `RoadReliefQaSmokeTest`
checks the three day-road clusters. A separate test-only wetness harness now
instantiates each benchmark scene one at a time, requires the ray collider to
be the scene's expected relief body, and reports 5/5 clusters and 15/15 patches
within ±0.010 m after bounded benchmark-origin corrections (`PuddleFar` y
0.102→0.092; `BoundaryWetPatch` y 0.105→0.078). It clones 15 materials at
roughness 0.50 in memory and writes three 1 920×1 080 candidate frames; final
wet specular/roughness, motion readability and cultural level-art review remain
open.

Historical frame hashes before lighting/fog integration: day `f77e80abb1d86843fe1f57724be534d65731a0abf0e1120003146f5c48301038`, house/PC `433a976d0d51832c42afcfc2acdf42e1f5314a826066829e03e82e3dbae6ff93`, forest edge `bdbf0343ddfda352ba145a766a7ab1646e3fb6a87202e2a5be5f8cf024782495`.

Focused v5 texture motion evidence (2026-08-14):
`../texture_candidate_motion_sweep_v5/` contains three test-only Metal/Forward+
contact sheets for the damp-earth and weathered-wood candidates. Each sheet
covers 27 near/mid/far × FOV 65°/75°/90° cells at 1 920 × 1 080. This is a
spatial projection/repetition check only; the v1 runtime owners remain active,
and earth relief/wetness, wood owner/orientation, temporal comfort, geometry,
lighting, cultural review and final art lock remain open.

Focused v6 texture evidence (2026-08-14):
`../texture_candidate_motion_sweep_v6/` and
`../texture_candidate_ab_diagnostic_v6/` contain the quieter earth and
abstract-wood test-only substitutions. They are technical receipts, not a
runtime material switch: the earth delta is subtle in relief-only cells and
wood still requires facade/fence/furniture/end-grain owner review.

## Lighting/fog calibration baseline — 2026-08-14

`game/scripts/StyleBenchmarkZone.cs` now uses the bounded calibration values
that were first compared in the isolated A/B receipt under
`../style_calibration_candidate/`: cooler neutral outdoor ambient/fog, lower
house lamp energy and less saturated warm window/moon fills. This is limited to
the Chapter 1 benchmark/demo presentation. Geometry,
`PainterlyMaterialLibrary`, shader uniforms, collision, narrative state and
saves were not changed. The purpose is to improve Painterly Low-Poly
readability under the fixed first-person camera, not to declare the art lock.

| Scene | Current 1 920 × 1 080 capture | SHA-256 |
|---|---|---|
| Day street | `godot_day_street_1080p.png` | `4c99a5483d8d9025503e320bb12dbd6b117a1e446fa78e22b6fa619de533bccb` |
| House / old PC | `godot_house_old_pc_1080p.png` | `115b617d91a0ea47bf65b3f46b200117f24ca5e921610845d68e124325af6903` |
| Kara-Urman edge | `godot_kara_urman_edge_1080p.png` | `ff3f37535d62f31bea4c2add821817a58fb1f3e901e105dbb1722f77e59e6826` |

The real Metal/Forward+ run is technically clean and the house/Kara washes are
less extreme, but visual acceptance remains OPEN: the day street still reads
as a grey-olive greybox, the house remains warmer and sparser than the bible,
and the Kara edge still needs authored canopy/ground geometry. These captures
are production-progress evidence only; review them together with the spatial
and temporal sweeps below.

## Deterministic pine-palette presentation pass — 2026-08-15

The existing `StyleBenchmarkZone.MakePine` owner now assigns one of five muted
world-space foliage colors (`26372f`, `2f4438`, `354b3f`, `3a4b3d`, `30483f`)
when a tree does not request an explicit color. The phase is deterministic, so
the fixed-camera captures remain reproducible while foreground, midground and
distance crowns no longer collapse into one repeated green value. No meshes,
physics nodes, materials/shader source, route state, saves or Acts 2–5 launch
paths changed; the palette only selects existing PainterlyMaterialLibrary
color owners.

Fresh real Metal/Forward+ captures after this pass:

| Scene | SHA-256 |
|---|---|
| Day street | `4c99a5483d8d9025503e320bb12dbd6b117a1e446fa78e22b6fa619de533bccb` |
| House / old PC | `115b617d91a0ea47bf65b3f46b200117f24ca5e921610845d68e124325af6903` |
| Kara-Urman edge | `ff3f37535d62f31bea4c2add821817a58fb1f3e901e105dbb1722f77e59e6826` |

Build, scene/import smoke, Act 1 corridor, physical walkthrough and the real
package probes remain clean (`120.15 FPS` medium Forward+, `120.07 FPS`
Mobile/low on the local M4 Pro). This is a bounded readability improvement,
not an art lock: repeated procedural crown geometry, authored canopy/ground
density, near/mid/far motion review, cultural review and release-host FPS stay
OPEN.

## Semantic material-owner split receipt — 2026-08-14

The imported modular kit now uses semantic wood owners (`wood_facade`,
`wood_fence`, `wood_furniture`, `wood_bark`) with calibrated world scales, and
generated NPC clothing has an explicit `cloth` albedo owner. The shader,
geometry, collision and narrative state are unchanged. A fresh Metal/Forward+
capture completed after this change:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `6728de8d1399f687c1140e171c401cf9bf7165f06c2392c096da37ca49e9d560` |
| House / old PC | `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47` |
| Kara-Urman edge | `f0a5209ef5d052f524eb4bc835ec7161ff18d9f56c375a3aa1fa0e51ae5adc8e` |

The images prove clean rendering and preserve the fixed-camera contract, but
do not close the visual art gate: the street remains greybox, the house remains
sparse/warm and the forest canopy remains provisional.

## Authored module recapture — 2026-08-14

After rebuilding the shared Blender kit to the 49-mesh LOD1 contract, the same
three style scenes were recaptured. This receipt checks import stability and
does not activate a texture candidate or claim art lock.

| Scene | Capture SHA-256 |
|---|---|
| Day street | `242673f2cfa56926b43eea698ee11b3b1eb9a10673e0be0ab321d10ee9f2ce39` |
| House / old PC | `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47` |
| Kara-Urman edge | `1e0da8a762b9528e879c3167f3eb470bd1520a3cc4c9e900058ecedda967086f` |

WellA/WoodpileA/GateA are exercised in full-game wrappers rather than this
fixed style-camera composition. The remaining greybox density, fog/light
calibration, near/mid/far motion, cultural review and release-host gates stay
open.

## PineA silhouette-variation receipt — 2026-08-14

The Kara-Urman benchmark now reuses the existing project-original `PineA_`
family at three placements without adding a Blender family or registry record.
The original `(8.6, 1.6, -14.6)` instance retains the established
`style-forest-pine` layer-2 proxy. Two additional scale/yaw variants are
presentation-only: mid-left `(-8.2, 1.44, -5.8)`, scale `0.90`, yaw `-18°`;
near-right `(8.8, 1.76, 3.0)`, scale `1.10`, yaw `27°`. Their adjusted Y
anchors keep the scaled trunk bases seated while adding no physics nodes, so
walkability and layer-1 interaction ownership remain unchanged. The exact
`styleImportedModules = PineA_project_original` contract is preserved.

Collision-isolation follow-up: the shared Godot adapter now synchronously
removes every imported `CollisionObject3D` and `CollisionShape3D` descendant
immediately after GLB instantiation and before the instance enters the scene
tree. The original Pine then receives exactly one controlled layer-2/mask-0
`KitCollisionProxy` with one shape; both presentation variants retain zero
physics descendants. `SceneSmokeTest` checks those actual node counts plus the
three-instance metadata fail-closed. This closes the imported layer-1 collider
regression only; it does not close the visual art gate.

Fresh Metal/Forward+ capture after the placement pass:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `e0ebb92243eee19e6e8f5a065b5c5dcda4f83ab083cb09c8fff932de16384d41` |
| House / old PC | `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47` |
| Kara-Urman edge | `39e28b1d0405b458c937823cf2a4afec48c498559fb5a2a30822b5832f66fe3c` |

The fixed camera shows grounded trunks and a less uniform near/mid silhouette,
but this is only a bounded reuse pass. Art acceptance remains OPEN: the three
instances still share one authored mesh family, the procedural cone family is
still prominent, and temporal motion plus representative release-hardware
review remain outstanding.

## Act 1 day-street prop integration — 2026-08-14

The Act 1 day-street benchmark now uses two project-original Blender modules in
the same fixed-camera composition: `WellA_` at `(-4.9, 0, 4.6)` and
`WoodpileA_` at `(5.0, 0, 2.3)`. Both are attached through
`GeneratedModularKitDressing.AttachPresentationOnly`; invisible helper boxes
remain the only local collision owner, while imported collision descendants are
removed before the presentation instances enter the scene tree. `SceneSmokeTest`
and `StyleFrameCapture` require the exact module metadata and zero actual
`CollisionObject3D`/`CollisionShape3D` descendants.

Fresh Metal/Forward+ capture after this bounded prop pass:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `c06331e1daa7657a0faae6874172604d69ec5822d38b87324605d251cf34512e` |
| House / old PC | `a3eb1fab7c6d3f2c89ebdf03b7570d3a363cb2a9c8edfa95adb907e5d7b220c2` |
| Kara-Urman edge | `862904f0a079e9f2c7611f80d6237938189975d2a42b710f8865f97cca2726c1` |

The well and woodpile improve cultural specificity and grounded silhouette in
the day frame, but this is still production-candidate evidence. Road/forest
greybox repetition, near/mid/far traversal, authored material variation,
cultural review, release-host performance and the Painterly Low-Poly art lock
remain OPEN. No texture candidate, shader, SaveGameV3, narrative state or
Acts 2–5 launch path was changed.

## Diegetic wayfinding landmark receipt — 2026-08-14

The day-street signpost now carries a muted, physically attached `Label3D`
reading `ФАП`. It is double-sided, depth-tested and non-billboarded, so the
landmark stays on the wooden board instead of becoming a floating quest marker.
`SceneSmokeTest` and `StyleFrameCapture` require the exact `fap` landmark
metadata. No route state, interaction ID, save field or non-diegetic HUD was
added.

Superseding Metal/Forward+ captures after the sign-depth/readability fix:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `dd6f9e1d43dea0c7de8434659633eee410bc937e7a78e43a9196b3a8772f089d` |
| House / old PC | `a3eb1fab7c6d3f2c89ebdf03b7570d3a363cb2a9c8edfa95adb907e5d7b220c2` |
| Kara-Urman edge | `862904f0a079e9f2c7611f80d6237938189975d2a42b710f8865f97cca2726c1` |

The 27-cell near/mid/far × FOV 65°/75°/90° sweep remains technical evidence;
the sign is legible in the near/mid composition but is not a substitute for
observed first-time wayfinding or cultural review. Art lock remains OPEN.

## Act 1 NPC + evidence presentation recapture — 2026-08-15

The three mandatory Godot scenes were recaptured after the bounded Act 1
presentation pass. The day street now visibly carries the project-original
Alsu guide beside the route and the existing `ФАП` sign; the house carries the
project-original Gulsina presentation plus the Rinat empty-chair/coat/radio
cue used by the physical evidence corridor. These are presentation-only
changes: interaction targets retain their collision shapes, character modules
own no physics, and the Rinat cue is explicitly not a final character asset.

Fresh Metal/Forward+ 1 920 × 1 080 hashes:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `1df98ff40e5eea009a65f5fae321414cacc1586a8ddab56fe4d8e9e04bf6dea4` |
| House / old PC | `f1499514d6a013f2c6ab6eb6e6dcd1dee4ff0cddd3161145e5b5787673280897` |
| Kara-Urman edge | `aa1937200185c8ec053f180db4fb1966a17ba93657185512416d006e398952e0` |

The frames demonstrate clean import/render and a more legible Act 1 cast, not
final art acceptance. Greybox density, authored Rinat staging/voice,
near/mid/far motion, cultural review, release-host performance and Painterly
Low-Poly art lock remain OPEN.

## Act 1 house OldPc presentation candidate — 2026-08-15

The house benchmark now presents the project-original `OldPc_` module from the
shared authored GLB through `GeneratedModularKitDressing.AttachPresentationOnly`.
The duplicate procedural CRT bezel, keyboard/key boxes, CRT base/neck/glass and
tower/button visuals were removed from this benchmark so the authored prop is
the single visible focal-PC owner. Imported collision descendants are removed
before the presentation instance enters the scene tree; the existing layer-1
`OldPc` interaction target and `urman.chapter1:interaction/oldpc-power` remain
unchanged. Scene/style smoke require 8/8 LOD meshes, positive import-sanitizing
counts and zero actual module physics descendants.

Fresh real Metal/Forward+ 1 920 × 1 080 captures after the bounded slice:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `0891fdbc18f4119028b6de5c9147f0444e1ba04b74afc334cd95d8c000dc377f` |
| House / old PC | `bcb0a1d92c694161d84ef50051de578a54b6ab4c200435662d4160fea83428cb` |
| Kara-Urman edge | `c8d73ce00425fc791f12438d7c52195f544322f03f027bcb633e545c058fe9c3` |

The house image is production-progress evidence: the authored CRT/tower reads
more clearly, but the room remains greybox. The Kara frame uses the bounded
night-readability calibration below; the Painterly Low-Poly art lock,
first-time playtest, cultural review and release-host performance remain OPEN.

## Kara-Urman night readability calibration — 2026-08-15

The first-person Kara-Urman benchmark received a narrow presentation-only
calibration after review of the previous frame: cold ambient energy `0.64`,
ambient color `7b9096`, fog color `52666d`, fog density `0.0034`, fog-height
density `0.075`, directional moon energy `0.82` and local `MoonFill` energy
`1.10`. This raises the readable value floor on the path and foreground trunks
without adding a gameplay light, changing the shader/material owners or
altering collision, narrative state or saves. It is a demo benchmark decision,
not a global art lock.

Fresh real Metal/Forward+ 1 920 × 1 080 captures after the calibration:

| Scene | Capture SHA-256 |
|---|---|
| Day street | `0891fdbc18f4119028b6de5c9147f0444e1ba04b74afc334cd95d8c000dc377f` |
| House / old PC | `bcb0a1d92c694161d84ef50051de578a54b6ab4c200435662d4160fea83428cb` |
| Kara-Urman edge | `c8d73ce00425fc791f12438d7c52195f544322f03f027bcb633e545c058fe9c3` |

Technical capture and full Godot smoke pass. Visual verdict remains OPEN:
the route is more readable, but the forest family/ground density, motion pass,
cultural review and art-lock acceptance still require human review.

## Scene basis clarification — 2026-09-14

Everything in this file was captured from the `style_benchmark_*` zone scene
alone, before the Act I connected-world dressing runs. That is why these frames
were repeatedly described here as a "grey-olive greybox": the zone scene's own
`Ground`/`Road` slabs, benchmark `Pine`/`Birch` stand-ins, `BoundaryThread` and
`DistantWindow` are all hidden at runtime by
`Act1ConnectedWorld.ApplyLogicalZonePresentationSuppressions`, and the shared
winter heightfield, authored parcels, exterior atmosphere and NPC dressing are
mounted in their place. These three PNGs remain valid as an isolated
module/scene receipt — `StyleFrameCapture` still asserts the presence,
presentation-only status and zero-physics contract of `WellA_`, `WoodpileA_` and
`OldPc_` — but they are **not** the shipped look and must not be used as the
visual style evidence for the art gate.

The 27-cell `../style_motion_sweep/` sheets now assemble the same world the game
assembles and are the current spatial-readability evidence. A future pass should
re-root `StyleFrameCapture`, `StyleTemporalComfortCapture`, the wetness candidate
and the puddle diagnostic onto the connected world the same way; until then their
READMEs state their scene basis.
