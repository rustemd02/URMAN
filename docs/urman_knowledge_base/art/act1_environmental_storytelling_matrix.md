# Act I Environmental Storytelling Matrix (NARR-016)

Status: **authored inference matrix 2026-09-04; aligned with
`content/modules/urman-chapter1/definitions.json` beat/interaction IDs; final
readability proof stays with human review (CAPTURE-006, PLAYTEST-003)**

Rule: every final prop cluster must state its intended inference, its owner
and its false-positive risk. Mandatory progress never depends on an
unlabelled decorative prop; clues are always carried by interaction targets
(`InteractionTarget` → `RuntimeBridge`), never by decoration alone.

## Zone/room matrix

| Space | Intended inference | Prop owners (kit component) | Clue vs atmosphere | False-positive risk |
|---|---|---|---|---|
| Arrival | an inhabited, working village at dusk — ordinary first | wet-road ruts/puddles; `Well_YardLandmark`; field-boundary fence runs | atmosphere | well/fences must not read as locked "gates" (visual-only policy is metadata-documented) |
| MainStreet | people live here; the route to FAP exists before it is needed | utility poles + sagging cables; `FapApproachWayfinding` («ФАП») landmark; worn road patches | atmosphere + wayfinding | ФАП board is diegetic wayfinding, never a quest marker; no false clue props |
| BabaiEbiYard | a cared-for home of an elderly couple | `Woodpile_StackedLogs`, yard dressing, house windows (warm) | atmosphere | household props must not suggest Marat's room; no invented shrine |
| HouseExteriorApproach | threshold of someone specific | porch steps, boot/mat-scale dressing, door hardware | atmosphere | door read must not imply forced entry or a struggle |
| House interior (`scene/house`) | a family under silent stress; the old PC is the investigation hub | CRT hero detail (`prop.oldpc.crt`), calendar, radio/herbs still-life | **clue carrier**: old PC → `evidence-official-death`, `evidence-internal-register`, saved message, tatarwiki boundary | calendar/photo props must not imply dates conflicting with canonical timeline; PC is the only "computer clue" — no second screen |
| ConnectiveStreetReturn | the village edges into outskirt quiet | return-boundary fences, drainage, sparse poles | atmosphere | nothing may read as "cemetery ahead" before the zirat |
| FapExterior + FAP interior (`scene/fap_*`) | official medicine, tired but functioning | `FapClinicAuthoredKit` facade/porch/notice board; interior cot/screen/cabinet/trolley; blank notice + wayfinding boards | **clue carrier**: Naila gate → `fap-document-desk` → official record | boards stay blank — no invented medical signage/diagnoses; interior props must not imply malpractice |
| ZiratMemoryField | a real place of mourning, kept with quiet care | `ZiratBoundaryFence`/`ZiratOpenGate`, non-inscribed marker slabs, path edge | **clue carrier**: `zirat-roadside-clue` (Marat's last-route trace) — gated, one-shot | markers stay plain/non-inscribed (religious gate CULTURE-003); no invented epitaphs; nothing ornamental |
| Mosque (`scene/mosque`) | respectful presence of faith in village life | authored interior references; Тимур хәзрәт context | atmosphere | no folkloric/Islamic mixing shorthand; any change routes through CULTURE-003 |
| Zirat → Kara road | leaving the ordinary world | road envelope narrowing, asymmetric banks, root clusters | atmosphere | escalation must not use literal monster silhouettes (hard stop) |
| KaraForestEdge + `scene/forest` | a boundary of the old order; Rinat waits | Kara kit boundary objects (banks, roots, logs, stump), accent lights, central road gap | **clue carrier**: final beat — Marat's voice, Rinat's interruption (`audio-marat-voice`, `audio-rinat-interruption`) | no creature-as-prop; accent lights stay bounded (3); the road gap preserves retreat |

## Cross-cutting false-positive controls

- Blank authored boards (`FapNoticeBoard`, `FapWayfindingBoard`) + one
  diegetic «ФАП» label; no readable fake text anywhere.
- Journal/objective text comes only from the RuntimeBridge projection — props
  never carry quest semantics.
- Decorative props have no collision/interaction ownership (presentation-only
  kits, verified by the launch/capture smoke scans).
- Any new prop: add a row here in the same change; a row without an owner or
  with an unresolved false-positive risk blocks the zone readiness check.

### Winter / authenticity props (2026-09-10, decision_log)

| Prop | Owner zone | Role | Collision / interaction |
|---|---|---|---|
| `BabaiYardSweepWell`, `ArrivalSweepWell` | BabaiEbiYard, Arrival | authentic колодец-журавль with lever and counterweight; village landmark | none (visualOnly) |
| `BabaiYardWattleRun`, `ArrivalWattleRun` | BabaiEbiYard, Arrival | woven wattle (плетень) boundary | none (visualOnly) |
| `BabaiYardFrontPalisade` | BabaiEbiYard | low front-garden palisade (палисадник) | none (visualOnly) |
| `BabaiYardSled` | BabaiEbiYard | wooden sled (салазки) yard detail | none (visualOnly) |
| `DistantMinaretSilhouette` | village skyline (west) | distant minaret landmark, not enterable; cultural review open | none (presentationOnly) |
| `AgentBSnowTrample` | whole exterior | session-only packed-snow trail mask following the player | none (presentationOnly, not saved) |
| `AgentBSnow` | whole exterior | winter snowfall particles; denser at the Kara edge | none (weather owner: AgentB exterior layer) |

### Unreachable winter backdrops (2026-09-10, T3)

| Prop | Placement | Role | Collision / interaction |
|---|---|---|---|
| `BackdropNearHousesWest/East` | 70–100 m west/east of the street | distant village rows with snow roofs and chimneys | none (presentationOnly) |
| `BackdropMidWoodlandWest/East`, `BackdropMidForestNorth` | 120–170 m | winter woodland bands; the north band is dark conifer forest | none (presentationOnly) |
| `BackdropFarRidgeWest/East/North` | 230–260 m | far snow ridges dissolving into frost haze | none (presentationOnly) |
| `DistantMinaretFar` | 150 m east skyline | second minaret silhouette for depth; cultural review open | none (presentationOnly) |
| `BabaiYardHaystack` | Babai yard | winter haystack (стог сена) | none (visualOnly) |
