# Act I connected-world greybox plan

## Goal

Replace the current Act I demo's one-zone-at-a-time presentation with one
compact, continuously loaded greybox territory. The player must be able to
look and move through a coherent Kырлай: arrival, main road, Babay/Äbi yard and
house, the populated street, FAP, the return road, zirat, and the approach to
the Kara-Urman edge. This is the next production slice, not a final art lock.

## Architecture

Keep `RuntimeBridge` as the only narrative-state owner and keep the existing
compiled Chapter 1 interaction IDs. Add a Godot presentation owner,
`Act1ConnectedWorld`, which instantiates the existing five Act I zone scenes in
one spatial layout and adds only connective greybox ground, road strips,
landmarks and distant framing. `Main` remains the composition root: in the Act
I demo mode it asks the connected-world owner to build once and changes the
logical active scene/spawn without unloading the world. Existing non-demo
smokes and full-game entrypoints retain the current one-zone `SwitchZone`
behavior.

The connected-world owner is presentation/layout only. It does not create a
second quest, vocabulary, save, collision or content compiler owner. Existing
zone `InteractionTarget`s continue to dispatch their compiled interaction; the
connected `Main` branch only maps the target zone/spawn to a world transform,
toggles the active environment/light presentation and keeps all five zones in
the scene tree.

## Tech Stack

Godot 4.7.1 .NET, C#, existing procedural Painterly Low-Poly greybox helpers,
existing project-original GLB modules, and the existing first-person player.
No new texture variant, shader, renderer, package, Windows/M1, or Acts II–V
work is part of this slice.

## Baseline / Authority Refs

- `AGENTS.md` — project rules and source-of-truth boundaries.
- `docs/urman_knowledge_base/README.md` — current Act I demo scope and deferred
  Acts II–V.
- `docs/urman_knowledge_base/design_style.md` — Painterly Low-Poly silhouettes,
  palette, fog and visual hierarchy.
- `docs/urman_knowledge_base/assets.md` and
  `docs/urman_knowledge_base/village_route_art_spec.md` — village modules,
  route landmarks and greybox asset floor.
- `docs/urman_knowledge_base/decision_log.md` and `open_questions.md` — Godot
  ownership, first-person route and unresolved art/continuity gates.
- `docs/urman_knowledge_base/weak_points.md` — explicit warning that benchmark
  scenes are not a цельный player experience.
- `game/scripts/Main.cs`, `StyleBenchmarkZone.cs`,
  `InteractionTarget.cs`, `RuntimeBridge.cs` and
  `game/tests/Act1FirstPersonWalkthroughSmokeTest.cs` — current runtime and
  route baseline.
- `game/scenes/act1_demo.tscn` and `game/scenes/main.tscn` — demo composition
  and normal one-zone composition.

## Compatibility Boundary

- `RuntimeBridge`, compiled IDs, SaveGameV3, journal, vocabulary, dialogue,
  old-PC capability and Act I story beats remain unchanged.
- `Main`'s default `EnableAct1ConnectedWorld=false` preserves existing test and
  full-game one-zone behavior.
- The connected demo may keep logical scene IDs while preserving a shared
  physical world; save/load must restore the logical zone and mapped world
  transform without importing old save formats.
- Acts II–V scenes/data remain untouched and unreachable from the demo launch
  path.

## TDD Route

- Mode: off
- Decision: skipped
- Strict authority: not applicable
- Test posture: focused post-change regression plus real Godot camera/motion
  evidence; no strict RED/GREEN requirement was requested.
- Verification: .NET build, connected-world smoke, full Act I route smoke,
  Godot scene/import smoke, fresh connected-map captures and a short real
  first-person sweep.

## Оркестрация исполнения (Luna-only)

Для всех срезов этого плана действует обязательное правило `Luna-only`:

- Реализация, правки документации, тесты, захваты, итерации и делегированное исследование выполняются в отдельных пользовательских задачах на GPT-5.6 Luna.
- Sol-субагенты не используются. Родительский Sol-чат остаётся оркестратором, архитектором и владельцем финальной приёмки; он минимизирует собственную работу и может выполнять только небольшие независимые проверки diff/test/frame, прямо требуемые acceptance-контрактом, но не вносит изменения и не запускает Sol-субагентов.
- Модель нельзя молча заменять, fallback запрещён; если GPT-5.6 Luna недоступен, работу нужно остановить и сообщить о блокере.

## Scope / Non-goals

In scope: one connected layout, world-space placements, connector ground and
roads, distant village framing, active-zone environmental presentation,
connected logical transitions, first-person 360°/near-mid-far capture, and
knowledge-base drift records.

Out of scope: new ImageGen texture generations, production material activation,
FPS optimization, package/export work, Windows/M1 evidence, final authored
models, final voice/mix, Acts II–V, web retirement and declaring art lock or
demo readiness.

## Change Necessity / Existence Check

Code change is necessary: documentation cannot remove the current root cause,
which is `Main.SwitchZone` freeing `ZoneHost` and loading exactly one benchmark
scene. The minimum boundary is a new layout owner plus a small `Main` mode
switch; no runtime-kernel or save-schema change is justified.

The new `Act1ConnectedWorld` owner is justified because no existing owner
stores spatial placement for multiple Chapter 1 zones or owns shared connector
geometry. `StyleBenchmarkZone` remains the owner of each zone's local dressing;
the new owner composes, places and frames those existing zones. No duplicate
interaction, narrative or material owner is introduced.

## Plan-Time Complexity Check

- `Main.cs` is already a composition root with zone loading and spawn mapping;
  only a narrow connected-mode branch and delegation will be added.
- `StyleBenchmarkZone.cs` is a large mixed presentation builder; it will not
  receive map-composition responsibility.
- New files `Act1WorldLayout.cs` and `Act1ConnectedWorld.cs` keep placement and
  shared dressing out of overloaded zone builders.
- Verification helpers/captures remain test-only under `game/tests` and
  `docs/urman_knowledge_base/art/act1_connected_map/`.

## Tasks

### 1. Record the goal drift and connected-map contract

Files: this plan, `docs/aegis/work/2026-08-10-godot-full-migration/10-intent.md`
or its checkpoint, `docs/urman_knowledge_base/README.md`, `decision_log.md`,
`weak_points.md`, `mindmap.md`, `act1_demo_handoff.md`, and
`release_gate_matrix.md`.

Record that the old “playable candidate / package FPS” framing is superseded
by connected-world greybox work. Mark benchmark frames, package probes, texture
candidates and smoke tests as supporting evidence only. Set the current demo
gate to OPEN until the unified map has a real first-person sweep.

Verification: inspect the new drift text with `rg`; no runtime files change in
this task.

### 2. Add the shared Act I spatial layout owner

Files: create `game/scripts/Act1WorldLayout.cs` and
`game/scripts/Act1ConnectedWorld.cs`.

Define exact offsets for `village_day`, `house_old_pc`, `fap_clinic`,
`zirat_road` and `kara_urman_night`; expose local/world spawn transforms;
instantiate all five existing PackedScenes under one world; preserve scene
metadata; retain one active `WorldEnvironment` and directional-light owner at
a time; and add connective ground/road strips, side vegetation/fences,
FAP/yard framing and distant tree/house silhouettes. All new dressing is
presentation-only and uses existing material helpers.

Verification: compile after the owner exists; add metadata for zone count,
connector count, placement IDs and visual-only policy so the next smoke can
fail closed.

### 3. Wire connected mode without changing narrative ownership

Files: `game/scripts/Main.cs`, `game/scripts/Act1DemoRoot.cs`,
`game/scripts/InteractionTarget.cs` only if the existing call boundary needs a
small delegation change, and `game/scenes/act1_demo.tscn` only if a scene flag
is preferable to the root property.

Add `EnableAct1ConnectedWorld` defaulting false. In demo mode build the connected
world once; `SwitchZone` updates the logical zone/spawn, mapped player transform,
active environment and ambience without freeing the connected world. Save/load
continues through the same `Main.SwitchZone` entry point. Keep ordinary tests
and full-game scenes on the current unloading path.

Verification: existing `verify-dotnet.sh`, focused connected-world smoke and
the existing Act I physical walkthrough. Assert the same world root/zone count
survives each logical Chapter 1 transition.

### 4. Prove first-person continuity, not only scene loading

Files: create `game/tests/Act1ConnectedWorldSmokeTest.cs` and its `.tscn`,
extend `eng/verify-godot.sh`, and create a test-only capture wrapper/script
under `eng/` plus evidence under
`docs/urman_knowledge_base/art/act1_connected_map/`.

The smoke must assert five loaded zones, connector metadata and no Act I zone
unload during transitions. The capture must record arrival, yard/house, street,
FAP, zirat and Kara-edge viewpoints plus a short 360°/near-mid-far motion
sweep. Capture metadata must identify the renderer and camera positions, but
the verdict remains OPEN until a human confirms no empty backsides, visible
greybox boundaries, repeated cone rows or broken village continuity.

Verification: focused Godot capture on the current host, `./eng/verify-godot.sh`,
`git diff --check`, `sh -n` for wrappers, and manual image review. Do not run
package/FPS/texture-production work as part of this task.

### 5. Stop at the connected-greybox gate

Update handoff and backlog with fresh map evidence and explicit remaining gates:
authored house/street families, weather/rain, fog/light composition, cultural
review, human first-time playtest and final art lock. Do not call the demo
finished merely because technical tests pass. If the map still reads as a
collection of boxes, stop with `needs-verification` and keep this goal active.

## Execution Readiness View

- Intent Lock: one visually continuous Act I territory, not benchmark-room
  switching.
- Scope Fence: map/greybox and continuity only; no texture/FPS/package/Acts II–V
  expansion.
- Baseline Lock: current five zone scenes and RuntimeBridge remain authoritative
  for local dressing and narrative state.
- Approved behavior: connected demo keeps all five zones loaded and maps
  logical transitions to world-space spawns.
- Owner constraints: `Act1ConnectedWorld` owns composition; `StyleBenchmarkZone`
  owns local zone dressing; RuntimeBridge owns story state.
- Compatibility: default Main path and full-game routes remain unchanged.
- Retirement: no old path deletion; benchmark scenes remain reusable local
  dressing/test fixtures until the connected map is accepted.
- Test obligations: build, connected smoke, route smoke, fresh camera/motion
  evidence and human visual review.
- Review gate: no art-lock or “ready to show” claim before map continuity is
  observed in motion.
- Drift rule: if connected composition requires new narrative IDs, save fields,
  shader/material owners or Acts II–V data, stop and return to plan review.

## Risks and rollback

- Existing local zone coordinates may produce overlap or hidden walls when
  composed; mitigate with explicit offsets and a capture pass before adding
  detail.
- Multiple environments/lights can flatten the mood; retain one active
  environment and record the active-zone switch as presentation-only.
- A logical transition could still accidentally unload the map; the smoke must
  compare the connected-world instance identity before/after every transition.
- Rollback is bounded: disable `EnableAct1ConnectedWorld` and the old one-zone
  path remains available; do not delete the benchmark scenes.

## Stop condition

State is `needs-verification` until the unified map has fresh first-person
360°/near-mid-far evidence and a human review confirms that the principal views
read as one village. The current scope is complete only when the connected
greybox gate is honestly recorded; final “wow” acceptance remains a later art
and playtest gate.
