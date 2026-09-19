# УРМАН — Act I master layout contract

Дата: 2026-08-17
Статус: **AUTHORITATIVE SPATIAL PRODUCTION CONTRACT / PARTIAL IMPLEMENTATION**
Рендер-цель: **Painterly Low-Poly 3D, first-person, Godot**

## Status and scope

Этот документ фиксирует один производственный master layout всей территории
Акта I. Он отвечает за пространственную связность, world-space anchors,
направления взгляда, front/back continuity, 360° envelope и границы владельцев.

Он **не** является:

- свидетельством готового демо или ready-to-show сборки;
- art lock, asset lock или доказательством production-ready текущих kits;
- заменой scene/runtime/content contract;
- разрешением добавлять новый narrative state, второй collision owner или
  отдельный transition-only мир.

Контрактная цель: игрок должен ощущать одну компактную территорию Кырлая — от
въезда до кромки Кара-Урмана. Логические переходы и состояние расследования
могут оставаться data-driven и принадлежать `RuntimeBridge`, но триггер сам по
себе не считается пространственной связью. Если при развороте видны пустой
горизонт, backside отдельной сцены, greybox-граница или повторяемый ряд
конусов, layout gate не пройден.

### Authority and confidence

Приоритет источников: `../00_codex_context.md`, knowledge base и утверждённая
`art/act1_visual_reference_bible_2026-08-17.md`; фактические координаты и
владельцы ниже сверены с primary `game/scripts/Act1WorldLayout.cs`,
`Act1ConnectedWorld.cs`, `Main.cs`, `StyleBenchmarkZone.cs`, zone scenes и
capture/smoke harnesses.

| Маркер | Значение |
|---|---|
| **A — source verified** | Точная координата, scene path, spawn или owner есть в primary source. Это не visual acceptance. |
| **B — technical composition** | Код строит/инстанцирует связь или метаданные, но нет полного human first-person visual proof. |
| **C — presentation candidate** | GLB/procedural/preview/import evidence есть, но нет принятого runtime traversal, collision, cultural или art gate. |
| **OPEN** | Несогласованность, overlap или критерий, который нельзя безопасно додумать. |

Godot coordinate convention: player root использует `Y=0.05 m`, eye примерно
`+1.70 m`; `yaw=0°` смотрит вдоль `-Z`, `yaw=180°` — обратно к `+Z`.
Координаты ниже — world X/Z layout, а не утверждение о реальных географических
сторонах света.

## 1. Coordinate-and-route overview

В primary сейчас пять логических zone scenes и восемь производственных
визуальных зон. `ReturnStreet` — обратное направление/композиционный слой
связующей улицы, а не девятая смысловая зона.

| # | Production zone / logical source | Current anchor / spawn (world) | Entry → exit continuity | Player-facing direction | Intended visual landmark | Actual owner / source confidence |
|---:|---|---|---|---|---|---|
| 1 | **Arrival / въезд** — `village_day` | `village_day@arrival = (0, .05, 9), 0°` **A** | Начало маршрута → `arrival-to-house-yard`: `(-1.8, .025, 9.5)` → `(-24.4, .025, 1.7)`; параллельно `residential-road-extension`: `(0, .025, 8)` → `(0, .025, -19)` | Вперёд `-Z`, к первым домам и улице; назад должен читаться выход из Кырлая | Земляная дорога, первые ограды, дальние крыши и продолжение поселения | `Act1WorldLayout` anchors + `Act1ConnectedWorld` road/framing **A/B**; текущий визуальный состав всё ещё procedural + candidate **C** |
| 2 | **Main street / главная улица** — `village_day` | `village_day@from_house = (-2.2, .05, 2.4), 0°` **A**; route target `RoadToFap` около `(3.8, .75, -8.2)` | Вход с yard/house return → residential street → FAP branch `village-to-fap-branch`: `(3.8, .025, -8.2)` → `(25.4, .025, -25.8)` | Базово `-Z`; FAP должен читаться как боковой следующий landmark, а не как floating marker | Фасады, калитки, окна, указатель/перекрёсток и физическая табличка `ФАП` в глубине | `StyleBenchmarkZone` owns local targets; `Act1ConnectedWorld` owns composition/connector **A/B**; current street remains greybox **C/OPEN** |
| 3 | **Babai–Ebi yard & house / двор и дом** — `house_old_pc` | origin `(-28, 0, 0)`; `house_old_pc@entry = (-28, .05, 3.8), 0°` **A** | Arrival apron enters the yard; `HouseExit` returns logically to `village_day@from_house`; physical backbone also has `house-to-zirat-return`: `(-28, .025, 4.82)` → `(0, .025, -53.5)` | Из entry вглубь дома `-Z`, к комнате и Old PC; из yard назад — к main street | Полный объём дома, калитка, дорожка, поленница/хозяйственная глубина, окно и старый ПК | Local room/interactions: `StyleBenchmarkZone`; yard/exterior/framing: `Act1ConnectedWorld`; `HouseA`/village kit are presentation candidates **A/B/C**, not production-ready |
| 4 | **Connective street / связующая улица** — composition layer | Нет отдельного runtime spawn. Group anchors: `ConnectiveStreet = (0, 0, -20)`, `ReturnStreet = (0, 0, -46)`, yaw `180°` **B** | Main street / FAP branch → alternating residential parcels → return toward zirat; keep both previous and next landmark visible | Основное движение `-Z`; обратный кадр обязан возвращать узнаваемую улицу/yard | Чередование фасадов, заборов, сараев, мокрых плеч дороги и просветов в деревню | `Act1ConnectedWorld` shared ground/connector/framing + imported village groups **A/B/C**; no independent narrative owner |
| 5 | **FAP / медпункт** — `fap_clinic` | origin `(28, 0, -30)`; `fap_clinic@waiting_room = (28, .05, -25.2), 0°` **A** | Branch arrives at `(25.4, .025, -25.8)`; FAP loop `(23, .025, -25.6)` → `(33, .025, -28.4)`; `OfficialRecordToInternalRegister` returns logically to `house_old_pc@entry` | Interior entry faces `-Z` toward desk/document; exterior approach must face the FAP volume from branch | Скромный полный объём, porch/step, bench, readable `ФАП` sign, yard and return-road depth | Local FAP interior/targets: `StyleBenchmarkZone`; sole exterior landmark owner: `Act1ConnectedWorld/Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation`; Agent B `Fap_*` family is explicitly suppressed **A/B**; first-person visual acceptance remains **OPEN** |
| 6 | **Return street / обратная дорога** — `ReturnStreet` layer | No independent logical spawn. Primary route seam is `house-to-zirat-return` ending at `zirat_road@village_side = (0, .05, -53.5), 0°` **A** | FAP evidence return → house logical return → southbound village/return road → zirat village side | `-Z` toward zirat; reverse view must show the lived-in village, not a scene backside | Fences, side yards, damp road shoulders, distant houses and a clear change from village to memory boundary | Connector/framing: `Act1ConnectedWorld`; authored `ReturnStreet` candidate group **A/B/C**; exact human route comprehension **OPEN** |
| 7 | **Zirat / зират** — `zirat_road` | origin `(0, 0, -70)`; `zirat_road@village_side = (0, .05, -53.5), 0°` **A** | Entry from return road; local exit target `ZiratRoadToForest` at world `(0, .75, -89.5)` → `kara_urman_night@village_path` `(0, .05, -103)` | `-Z` toward forest threshold; `+Z` must preserve village memory and route return | Низкая уважительная граница, простые markers, трава/посадки, road and visible village behind | Local road/floor/interaction: `StyleBenchmarkZone`; connected composition and `ZiratBoundary`: `Act1ConnectedWorld`; old `ZiratFence`/greybox enclosure are presentation-only suppressions **B/C/OPEN** |
| 8 | **Kara-Urman forest edge / кромка** — `kara_urman_night` | origin `(0, 0, -115)`; `kara_urman_night@village_path = (0, .05, -103), 0°` **A** | `zirat-to-kara`: `(0, .025, -89.5)` → `(0, .025, -103)`; `kara-edge-approach`: `(0, .025, -103)` → `(0, .025, -122.5)`; optional return uses `(1.8, .05, -12.5), 180°` local village spawn | Вперёд `-Z` к порогу; назад `+Z` к зирату/деревне; never a black void | Broken banks/roots, mixed forest masses, side landmark, village value plane and a sound-first threshold | Local path/interaction: `StyleBenchmarkZone`; connected framing: `Act1ConnectedWorld`; `PineA` and procedural forest are current candidates; `act1_forest_edge_kit` is not integrated **A/B/C/OPEN** |

### Source-vs-actual drift that this contract makes explicit

- `Act1ConnectedWorld` does build all five primary scenes once and retains a
  persistent root in Act 1 demo mode. This proves an implementation seam, not a
  cohesive art result.
- The shared traversal box is `86 × 0.24 × 208 m` at `(0, -.12, -48)`, with
  seven connector strips. Its ground range now covers `z=-152…56`, including
  the reverse-arrival framing band around `z=42–53`; the first-person seam is
  still subject to the full visual capture and human review.
- The production FAP presentation now has one declared exterior owner:
  `FapClinicAuthoredKitPresentation`. The local benchmark scene keeps the
  interior floor, desk and interaction targets; the Agent B buildings kit's
  `Fap_*` meshes are hidden before architecture-collision generation. The
  legacy `BuildFapExterior`/landmark builders remain source/rollback code and
  are not materialized by the connected-world build path. This closes the
  duplicate-owner seam structurally; first-person visual acceptance remains
  separate and **OPEN**.
- `act1_village_landmark_kit_2026-08-17.md` describes the GLB as asset-only,
  while current primary `Act1ConnectedWorld` now loads and re-anchors its nine
  groups. The code is the actual integration evidence; the kit remains partial
  and the document/source drift is **OPEN** for later synchronization.
- The forest-edge kit is still not loaded by primary; current Kara framing uses
  procedural masses and selected `PineA` presentation instances. Importability
  of `urman_forest_edge_kit.glb` is not runtime composition evidence.

## 2. Compact master plan

Top-down orientation below uses `+Z` as the arrival/back direction and `-Z` as
the forest/forward direction. It is a compact loop/branch, not a map UI.

```text
                               +Z / village arrival

       west parcel             MAIN ROUTE                 east parcel
          [houses]       [Arrival  (0, 9)]       [houses / horizon]
                             │
                             │  residential-road-extension
                   [Babai–Ebi yard / house]
                  (-28,0) ───┘       │
                             [VillageStreet  z≈5]
                                  │
                     [ConnectiveStreet  z≈-20]
                                  │
              FAP branch  ────────┘────── [FAP (28,-30)]
             `(3.8,-8.2) → (25.4,-25.8)`     ↺ yard loop
                                  │
                       [ReturnStreet  z≈-46]
                                  │
                         [Zirat village side]
                       (0,-53.5) / origin (0,-70)
                                  │
                 [quiet zirat boundary → forest road]
                                  │
                  [Kara path (0,-103) / origin (0,-115)]
                                  │
                       [Kara threshold to -122.5]
                               -Z / forest
```

The diagonal `house-to-zirat-return` connector is a spatial backbone seam, not
a new quest or direct narrative shortcut. The authored route remains:

```text
arrival → house/old PC → main street → FAP document → house/re-read
→ zirat road → Kara-Urman cliffhanger
```

The physical world must make that sequence legible even where the logical
interaction target updates `RuntimeBridge` and applies a mapped spawn. A
transition fade may protect narrative pacing; it must not hide a missing yard,
road, boundary or reverse view.

### Layout rules and no-go blockers

- The persistent root is one `Act1ConnectedWorld`; local interiors can be
  visibility-gated, but the exterior territory and connector ground do not get
  unloaded per logical beat.
- Every seam has a **near seam belt** (ground/shoulders and doorway or gate), a
  **mid continuity band** (fence/yard/side volume), and a **far closure**
  (houses, tree line, zirat/forest threshold). A skybox-only seam is a fail.
- Front and back views are equally authored. A house needs side/back logic; a
  fence needs an end, gate or proscribed continuation; the forest needs a
  village-facing value plane.
- Route center stays open and readable. Parcel framing may constrain the view,
  but must not block protected walkable path, camera ray, doorway, document,
  FAP sign or zirat entry.
- No flat road slab, naked horizon, repeated wall of foliage, row of identical
  cones, floating facade, collisionless-looking step or vegetation mask may be
  accepted as a layout solution.
- Foliage source kits are extraction-only: template roots must be hidden after
  deterministic placement; visible planted instances must carry the authored
  ground contact and cannot be used to conceal missing parcel geometry.
- No generic horror dressing: no blood, skulls, glowing eyes, decorative runes,
  Halloween props, or “cursed” mosque/ziрат. The first read is an ordinary,
  specific татарская деревня; wrongness comes later through context, silence,
  sound, shadow and rule.
- Do not literally borrow `Микулай`/Kryashen material. Only broad observations
  such as wet rural scale, quiet weather and human-sized composition are usable;
  religion, ethnicity, costume, faces, characters, plot and visual markers are
  not transferable.

## 3. Per-zone 360° envelope

The following is the authored envelope contract, not a claim that current
greybox passes. Every zone must be reviewed from the six directions **forward,
backward, left, right, up, down**. Near/mid/far means authored geometry and
value separation, not three fog distances.

| Zone | Forward | Backward | Left / right | Up / down | Near / mid / far requirement | Geometry required before foliage/materials |
|---|---|---|---|---|---|---|
| **Arrival** | Road intent, first gate/house and at least two depth layers | Exit/approach remains a village road, not edge-of-map | Low fences, parcel openings, side roofs and obochina on both sides | Wires/sky/crowns at scale; below-camera ruts, wet shoulder and contact | **Near:** road relief, tracks, fence bases. **Mid:** first full-volume houses/gates. **Far:** settlement continuation and closed horizon | Continuously walkable road and shoulders; complete parcel bounds; house backs, roof volumes and terrain beyond arrival. |
| **Main street** | Main road and a readable FAP branch/landmark | House/yard and arrival memory remain identifiable | Alternating fences, windows, yards, shed/woodpile and controlled gaps | Roofs, poles/cables only where authored; ground must show crown, ditch and puddle seating | **Near:** gates, road edge, physical sign/props. **Mid:** two-sided house parcels. **Far:** next street/FAP silhouette and village depth | Road branch junction, full side/back volumes, fence ends/gates, sign support and non-blocking collider plan. |
| **Babai–Ebi yard** | Gate, yard path, house entrance and safe-zone warmth | Street, gate and arrival connection remain visible | House side, shed/woodpile/well/barrel/bench and boundary on both sides | Eave, tree/canopy, window/sky; threshold, steps, mud and planted roots below | **Near:** threshold/yard contact and one precise household prop cluster. **Mid:** full house + outbuilding/yard boundary. **Far:** street facades and distant parcel roofs | Full house volume including side/back, yard grade, fence/gate openings, porch/steps and physical path to street. |
| **House / Old PC** | Room depth, table and readable Old PC/document focal point | Door/window and actual yard relation; no black wall-only back | Walls, shelf, furniture, domestic archive cues and clear side clearance | Ceiling/beams/lamp; floorboards/rug/shadows and furniture contact | **Near:** hero PC, desk, documents, interaction clearance. **Mid:** room furniture and domestic wall depth. **Far:** door/window/yard glimpse and believable room closure | Floor, full room envelope, doorway/doorframe, collision/interaction clearance and Old PC anchor; UI cannot substitute for geometry. |
| **Connective / return street** | Next landmark, curve/branch or zirat direction; no endless corridor | Recognizable previous street/yard/FAP relation | Parcel alternation, open yards, fences and vegetation with gaps | Sparse wires/crowns; relief, drainage, wet/dry transitions underfoot | **Near:** shoulders/ditch/fence contact. **Mid:** varied parcels and outbuildings. **Far:** zirat boundary or village mass plus return cue | Continuous road/terrain seam, full parcel edges, side/back architecture and landmark sightlines before any flora. |
| **FAP** | Exterior entry/sign or interior desk, depending on checkpoint | Street/return direction and branch exit remain understandable | Clinic boundary, bench/yard, side building and open service edge | Roof/eaves/tree; step, mud, puddle and grass contact | **Near:** porch, step, sign, desk/document clearance. **Mid:** complete clinic and yard. **Far:** return street and village depth, not a blank field | One agreed clinic volume, exterior/interior seam and ground level; remove duplicate presentation facade only after ownership review. |
| **Zirat** | Quiet path, low boundary and forest direction without dungeon framing | Village visible as memory/social context; return path clear | Respectful fence/markers, planting, side fields and controlled openings | Sky/branches without gothic theatre; dry/wet soil, grass, roots and marker bases | **Near:** path, low markers and respectful boundary. **Mid:** planted enclosure and road edge. **Far:** village behind and Kara value plane ahead | Road/entry/exit, low boundary, marker placement, reverse village silhouette and cultural review before vegetation/material pass. |
| **Kara-Urman edge** | Path bends/enters a readable threshold with a non-empty forest window | Zirat/village remains visible or audible and visually anchored | Asymmetric banks, roots, mixed trunks and side landmark; no straight tree wall | Canopy/branch may form one almost-human gesture; ground shows roots/moss/wetness and safe path | **Near:** banks, roots, logs/boulders and route contact. **Mid:** varied mixed forest masses/side landmark. **Far:** two value-plane horizon masses and village-side closure | Bent/graded path, left/right banks, horizon closure, side landmark and camera-safe occluders. No full creature or monster reveal. |

## 4. Authored build layers and cross-zone continuity

Layers are sequential gates. A later layer cannot be used to conceal a failed
earlier layer.

| Layer | What is authored | Owner boundary | Gate before next layer |
|---:|---|---|---|
| 0 | Coordinate/topology: origins, entry/exit seams, route order, camera height, world extents | `Act1WorldLayout` + this contract; `RuntimeBridge` is not spatial art owner | All anchors resolve; no unexplained overlap, off-ground reverse mass or missing seam |
| 1 | Terrain/road: shared ground, road crown/ruts, shoulders, ditches, thresholds, graded banks and safe walkable path | Existing local floors/road relief plus connected traversal surfaces; only the declared collision owner may change | Player can walk the complete physical backbone; camera ray and interaction clearances survive |
| 2 | House and boundary volumes: full house sides/backs, FAP volume, fences/gates, yard, zirat low boundary, Kara banks | `StyleBenchmarkZone` owns local room/target geometry; connected composition owns shared presentation framing; candidate GLBs remain presentation-only until accepted | Forward/back/side silhouettes and doorway/yard transitions read without foliage/material tricks |
| 3 | Village/depth silhouettes: near/mid/far houses, sheds, roofs, village-facing forest plane, zirat-to-village depth | `Act1ConnectedWorld` composition owner with one approved authored family per role | No empty horizon, no repeated wall, no accidental backside, no visual overlap at FAP/yard seams |
| 4 | Vegetation: grass, sedge, ferns, shrubs, young firs, moss, branches, fallen wood, mixed forest masses | Presentation-only asset owner; no hidden collision/navigation/interaction descendants | Every plant has a ground reason and role; path, marker, target and horizon remain readable |
| 5 | Materials, weather and light: Painterly albedo/roughness, wet/dry contact, rain, fog, warm house island, value separation | Existing material/light helpers; no new global shader/material owner from a layout task | Day/night continuity, near/mid/far value, rain contact and target-host budget are evidenced |
| 6 | Capture and human review: first-person 360°, motion, wayfinding, culture, language, accessibility, performance | Test-only harness + human reviewers; never an asset preview alone | Hard checklist is all PASS; otherwise status remains `PARTIAL/OPEN` |

Cross-zone rules:

1. Every visible route entrance has a destination landmark in the forward view
   and a memory landmark in the reverse view.
2. The same road/soil vocabulary continues across seams; a connector cannot
   change width, height or material abruptly at a logical transition.
3. A house/FAP/zirat boundary is a volume with a ground contact and side/back
   logic, not a front-facing card. Interiors must leave a believable door,
   yard/road relation even when their logical scene is active.
4. Village density tapers: lived-in parcels near the road, quieter fields and
   low planting toward zirat, then asymmetrical forest masses. Do not use fog as
   a hard curtain between zones.
5. The one active zone environment/light presentation may change ambience, but
   it must not expose a black sky, doubled directional light or a visible
   “scene swap” seam.
   In production, exterior logical zones route to `AgentBEnvironment` and
   `AgentBSun`; house/FAP route to their local interior environment. There must
   be exactly one active `WorldEnvironment` owner in either mode.
6. `ReturnStreet` is the same village continuing in the reverse direction. It
   must reuse rhythm and landmarks without becoming a second map or a ninth
   content owner.

## 5. Technical integration boundaries

### Protected owners

| Protected surface | Must remain authoritative |
|---|---|
| `RuntimeBridge` | Narrative state, clue/knowledge/vocabulary state, dialogue effects, save/load, current logical zone/spawn and the cliffhanger. No presentation layer dispatches state directly. |
| Compiled Chapter 1 interactions | Existing `InteractionTarget` nodes and data IDs, including old PC, FAP desk, document chain, Rinat and zirat/Kara targets. Do not replace them with a decorative marker or generic fallback. |
| `Act1WorldLayout` | Origins, declared local spawns, world-spawn mapping and route connector coordinates once this contract is accepted. Changes require a new layout decision, not an asset pass. |
| `Main` / `FirstPersonController` | Connected-mode composition, destination-facing spawn application, player physics and camera. A camera checkpoint may set a test look; production spawn ownership stays here. |
| Local zone floor/path and interaction collision | The five current scenes own their authored local floors, room/path relief and target bodies. Do not delete, merge or silently re-layer them. |
| `Act1ConnectedWorld` traversal surfaces | `SharedVillageGroundTraversalCollision` and declared `RoadTraversalCollision` strips own shared connector traversal. Shoulders, fences, distant framing and candidate GLBs do not gain collision implicitly. |

### Presentation-only candidates and safe visual suppressions

Presentation-only candidates include the village landmark GLB groups, forest-edge
kit, `HouseA`/`PineA`/OldPc modules attached through `AttachPresentationOnly`,
procedural facades/sheds/trees/fences, FAP facade/sign, zirat markers and
non-interactive weather/lighting nodes. They may be replaced or re-composed only
inside their declared spatial slice and only after the geometry/ownership gate.

The current primary code already suppresses presentation-only benchmark artifacts
when composing the connected world. These suppressions are safe **only while the
replacement landmark is present and the protected target remains intact**:

- village benchmark `VillageSignPost/Board/Arrow/Text`, selected benchmark
  trees, pole and cables;
- `zirat_road` `ZiratFence` and the connected `ZiratGreyboxEnclosure` when the
  authored quiet boundary is the visible candidate;
- Kara benchmark `DistantWindow`, boundary posts/thread/ribbons/marker and
  deferred `Act2Continuation` presentation;
- inactive interior roots and their local environment/light presentation while
  exterior framing remains visible;
- duplicate decorative props that have no `CollisionObject3D`, navigation,
  interaction or runtime metadata owner.

Every suppression needs a capture check for wayfinding, reverse view and
interaction clearance. A hidden interaction target is not a hidden owner: its
compiled ID and `RuntimeBridge` path remain protected.

### No-go integration boundaries / OPEN seams

- Never attach imported `StaticBody3D`, `CollisionShape3D`, navigation or
  interaction descendants from a presentation GLB without a separately accepted
  owner. Current candidate contracts explicitly have no collision.
- Never solve a future FAP regression by adding another facade, hidden proxy or
  scene-local transition. The production contract already has one exterior
  visual owner (`FapClinicAuthoredKitPresentation`) and one local interior/
  interaction owner (`StyleBenchmarkZone`); keep the route ground/targets
  unchanged.
- Never call a material, fog, foliage or transition fade a fix for missing
  geometry, off-ground placement or a 180° backside.
- **OPEN:** whether every logical transition can be presented as a continuous
  physical walk while preserving the current evidence interaction chain. The
  93.05 m walkthrough is technical traversability evidence, not proof of
  first-time route comprehension.
- **OPEN:** visual capture and human review of the reverse-arrival extent
  described above; the geometry envelope itself now covers the declared range.
- **OPEN:** mesh-level collision, navigation and target-host performance for
  future authored geometry.

## 6. Visual checkpoint itinerary

These are **future first-person capture staging coordinates**, not current
evidence and not an art-lock claim. Each listed checkpoint must be captured from
the production player camera at 1280×720 or 1920×1080 on the chosen review
profile, with the player root at `Y=.05` and the eye at `Y≈1.70`.

At every row capture the primary direction plus yaw `0° / 90° / 180° / 270°`
from the same root position, then a controlled up/down look (pitch about
`±15°`). The target values below define the primary forward read; they are not
substitute cameras. Near/mid/far positions must repeat the same direction set
at approximately 3 m / 12 m / 25 m along the local route where the geometry
allows it.

| Capture ID / zone | Exact root position | Primary look target / direction | Required visual question |
|---|---|---|---|
| `arrival` | `(0, .05, 9)` | target `(0, 1.45, -3)`; yaw `0°` toward main road | Does the village continue forward and behind, with road contact and first homes rather than field/skybox? |
| `main_street` | `(-2.2, .05, 2.4)` | target `(0, 1.45, -12)`; primary `-Z`, with FAP branch at the right-hand side | Do street parcels, house/yard memory and the FAP landmark form one readable social road? |
| `babai_yard` | `(-24.4, .05, 1.7)` | target `(-28, 1.45, -1.2)`; toward house volume/gate | Does the yard have ground, gate, side/back house mass and a readable street return? |
| `house_old_pc` | `(-28, .05, 3.8)` | target `(-28, 1.45, -3.2)`; yaw `0°`, toward room/Old PC | Does the interior read as a real home with door/yard relation, not an isolated benchmark room? |
| `connective_street` | `(0, .05, -20)` | target `(0, 1.45, -38)`; yaw `0°` toward return street | Is there meaningful near/mid/far village depth after the main street, including a reverse memory cue? |
| `fap` | exterior staging `(25.4, .05, -25.8)`; interior staging `(28, .05, -25.2)` | exterior target `(28, 1.45, -30)`; interior target `(28, 1.45, -34.2)` | Is one FAP volume legible from the branch, and does the desk/official-document interior connect to it without overlap? |
| `return_street` | `(0, .05, -46)` | target `(0, 1.45, -53.5)`; yaw `0°`; reverse target `(0, 1.45, -35)` at `180°` | Does the route taper naturally from village to zirat, and does the reverse view preserve the village? |
| `zirat` | forward staging `(-.9, .05, -54.1)`; reverse staging `(1.15, .05, -57.3)` | forward target `(0, 1.45, -73)`; reverse target `(.2, 1.45, -41.5)` | Is the zirat quiet and respectful, with entry/return/forest logic and no decorative dungeon read? |
| `kara_edge` | `(0, .05, -105.8)`; existing side checks also use `(.8,.05,-107.2)`, `(-2.15,.05,-108.6)`, `(2.05,.05,-109.4)` | forward target `(0, 1.45, -119.5)`; reverse target `(.3, 1.5, -97.5)` | Do banks, mixed masses, horizon and village-facing back view create a threshold without a full creature or black void? |

The current `Act1VisualReviewCapture` harness covers only six root-viewport
frames: Kara forward/back/left/right and zirat forward/back, each in a separate
Godot process at 1280×720. It does **not** cover arrival, main street, yard,
house, connective street, FAP or return street; it also does not provide the
required all-zone 360°/near-mid/far motion itinerary. Current six-view evidence
is therefore technically useful but inadequate for master-layout acceptance.
The corridor smoke and 93.05 m physical walkthrough prove routing/collision and
state handoff only; they do not replace human visual review, wayfinding or
comfort review.

## 7. Production slice order and non-overlapping Luna ownership proposal

Later work must be split by spatial slice and owner. One task may consume a
previous slice's published geometry, but must not reopen its collision,
interaction or runtime owner without a new decision.

| Slice | Sole spatial ownership | Allowed work | Explicit non-overlap |
|---:|---|---|---|
| 0. Layout/seam gate | Master coordinate/topology owner | Resolve shared ground extent, seams, connector widths, capture points and no-go blockers | No new assets, materials, narrative IDs or collision owner |
| 1. Terrain/road backbone | Route terrain owner | Road relief, shoulders, ditches, graded thresholds, cross-zone ground continuity | Does not author houses, foliage, weather or interactions; preserves protected traversal bodies |
| 2. Arrival/main/connective/return architecture | Village street owner | `Arrival`, `VillageStreet`, `ConnectiveStreet`, `ReturnStreet`: full-volume houses, fences, gates, sheds and reverse silhouettes | Does not edit Babai interior, FAP volume, zirat boundary or Kara forest kit |
| 3. Babai–Ebi yard and house | Home/Old PC owner | `BabaiYard`, `HouseExterior`, house interior composition, domestic props and hero-PC staging | Does not own road seams, FAP/zirat/Kara or RuntimeBridge/interaction IDs |
| 4. FAP | FAP owner | One agreed FAP exterior + local waiting room/desk relationship, sign and yard | Does not duplicate village framing or add a second FAP collision/transition owner |
| 5. Zirat | Zirat owner | Respectful boundary, markers, planting, reverse village/forward forest composition | Does not author Kara threshold creature language, religious spectacle or road owner |
| 6. Kara approach | Forest-edge owner | Banks, roots, mixed tree masses, horizon, side landmark and restrained Level 3 trace | Does not replace all PineA globally, reveal a creature, edit zirat or add Act 2 route state |
| 7. Weather/light continuity | Presentation atmosphere owner | Rain, fog, value planes, day/pressure/night tuning and ambience handoff | Does not use post-processing to hide geometry or create a new global material owner |
| 8. Capture/review gate | Test/evidence owner | Eight-zone root-viewport captures, 360°/near-mid/far sweep, motion/comfort and review receipts | Test-only; no production geometry, state, collision or asset integration edits |

All implementation, documentation, tests and captures for these later slices
remain Luna-only. A slice is not complete on import smoke or a Blender preview;
it hands off exact world anchors, owner metadata, known OPEN gates and first-
person evidence.

## 8. Hard master-layout acceptance checklist

This checklist gates **any** asset integration into the Act I map. `OPEN` or
`FAIL` means `PARTIAL`; no asset may be called integrated/accepted for the
layout until the relevant line is `PASS`.

### Spatial contract

- [ ] All five primary logical scenes resolve to the eight production zones in
  the coordinate table; no hidden ninth `ReturnStreet` world is introduced.
- [ ] Origins, world spawns, connector endpoints and yaw are exact or have a
  recorded layout decision; no unexplained local/world offset remains.
- [ ] The shared ground and route extents cover every authored near/mid/far
  staging point, including reverse-arrival framing; no geometry floats outside
  the terrain seam.
- [ ] Player can traverse the master backbone with protected collision and
  without seeing a scene unload, trigger-only void, hard skybox edge or
  unplanned fade seam.
- [ ] Every zone passes forward/back/left/right/up/down review; reverse views
  preserve memory of the previous zone and forward views show the next intent.

### Geometry before dressing

- [ ] Road/terrain has useful relief, shoulders, drainage, thresholds and
  planted contact; it is not a uniform slab.
- [ ] Houses, FAP, fences, yards, zirat boundary and Kara banks are full
  volumes with sides/backs, ends and ground contact.
- [ ] Near/mid/far layers differ by silhouette, value, density and detail; no
  repeated module reads as wallpaper inside 20–30 m.
- [ ] Foliage is added only after geometry passes and never masks an empty
  horizon, missing collider, target, facade backside or unfinished seam.
- [ ] FAP overlap and any candidate GLB/procedural duplicate have one declared
  visual owner per role; decorative suppression leaves protected targets intact.

### Ownership and runtime safety

- [ ] `RuntimeBridge` remains sole narrative/vocabulary/save owner.
- [ ] Existing compiled interaction IDs and `InteractionTarget` bodies remain
  the only interactive targets for the route.
- [ ] Protected local floors/paths, connected traversal surfaces and spawn
  transforms are not deleted, silently re-layered or replaced by presentation
  assets.
- [ ] Presentation candidates contain no unreviewed collision, navigation or
  interaction descendants; any exception has an explicit owner decision.
- [ ] Connected world instance identity remains stable across logical route
  transitions; no benchmark scene is accepted as an isolated substitute.

### Evidence and visual/cultural gates

- [ ] Eight-zone capture itinerary exists in the production harness with exact
  player positions, targets, renderer, resolution and receipt hashes.
- [ ] Each zone has 360° plus up/down checks and near/mid/far motion evidence;
  still Blender/GLB previews and current Kara/zirat six-view receipt alone do
  not count.
- [ ] First-time wayfinding is observed without test teleports or direct target
  hints; journal/signs support orientation but do not become GPS markers.
- [ ] Татарские labels/words, zirat markers, FAP sign and local household forms
  pass language, cultural and religious human review.
- [ ] No literal `Микулай`/Kryashen borrowing, generic monster language,
  “cursed mosque” framing or full creature reveal appears before the locked
  cliffhanger.
- [ ] Motion comfort/accessibility, authored audio/captions, M1/Windows frame
  time and final human playtest are recorded as separate gates.
- [ ] No claim says “ready demo”, “final art lock” or “production-ready kit”
  while any master-layout gate remains `OPEN`.

## 9. Open gates

The master layout is not accepted for public/demo readiness until these gates
are closed by the appropriate human evidence:

1. **Cultural, religious and local context:** review of village proportions,
   household details, zirat markers, FAP/mosque context and the respectful
   relationship between Islam, татарская culture and folklore.
2. **Tatar language:** native-speaker review of `ФАП`, `зират`, any village
   signage, Old PC documents, vocabulary and dialogue/caption wording,
   including `ә, ө, ү, җ, ң, һ`.
3. **Voice/audio:** authored Marat voice, Rinat interruption, rain/forest/
   village ambience, captions and non-audio cues; current logical refs and
   caption fallback are not a final recording or mix.
4. **Motion comfort/accessibility:** observed first-person motion, FOV,
   head-bob/reduced-motion, gamepad parity, readable 1080p text and
   audio-description/non-audio support.
5. **M1/Windows performance:** release-shaped exported builds on Apple M1 and
   representative Windows hardware. Local M4 Pro/Metal smoke and package
   structure do not close this gate.
6. **Final human playtest:** first-time route comprehension, wayfinding,
   detective-loop pacing, zirat/forest tone, language usefulness and the
   audio-first cliffhanger. The deterministic corridor/walkthrough is not a
   substitute.

Additional production gates remain open: authored house/street/FAP/zirat/Kara
geometry, mesh-level collision review, 20–30 m repetition, near/mid/far
traversal readability, final lighting/weather, asset provenance/LOD review and
Painterly Low-Poly art lock.

## 10. Contract summary

The Act I map is one compact world with five logical scene owners and eight
visual production zones. The current primary code proves that a connected-world
composition owner and world-space anchors exist; it does not prove that the
map is visually cohesive, culturally accepted, comfortable, performant on
target hardware or ready for public demonstration. The contract remains
**PARTIAL / OPEN** until the hard checklist and human gates above pass.
