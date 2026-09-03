# Act I OpenCode Execution Ledger

Run: 2026-09-03, single external OpenCode implementer, canonical checkout
` /Users/unterlantas/Documents/GitHub/URMAN`, branch `main`.

- Base commit: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`)
- Current commit (ledger init): `fc58c408326c0fff420641cdd82e712bb51b4f6c`
- Branch: `main`
- Tracker: `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (root, untracked input)
- Current task/slice: TEST-007 committed. Next queue: remaining automatable
  P1/P2 slices — UIUX-006/007/008 (settings navigation + user-settings store
  + input parity), AUDIO-002 (sound map doc), NARR-016 (environmental
  storytelling matrix doc), UIUX-003/004 polish. WAVE 4-7 substantive
  art/narrative slices stay REWORK/OPEN pending the CAPTURE-003/004/006
  human evidence cycle (rationale in Completed below).

## Completed

- TEST-007 (P0): static source-contract guard 2026-09-03.
  - New `tests-dotnet/Urman.Core.Tests/SourceContractTests.cs` (2 facts,
    engine-independent, runs in the dotnet suite): only `RuntimeBridge.cs`
    may construct/restore the `RuntimeKernel` (`new RuntimeKernel(` /
    `RuntimeKernel.Restore(`) and only `RuntimeBridge.cs` may own the save
    store (`new AtomicSaveGameStore(` / `user://savegames`) across all
    `game/scripts/**`. Self-check asserts the patterns ignore ordinary
    mentions. Any new caller now fails the suite — the standing regression
    guard behind the STATE-001 audit ledger.
  - Suite 43+12 green, verify-dotnet exit 0 (Godot side unchanged).
  - Evidence: `evidence/act1_repo_baseline/test007-verify-dotnet-PASS.txt`.

## Completed

- GAME-009 (P0): interruption/softlock fixtures 2026-09-03.
  - New `game/tests/Act1InterruptionSmokeTest(.cs/.tscn)` in the aggregator:
    (1) dialogue-cancel atomicity — Escape on an open Gulsina modal applies
    no state change; (2) quick save taken under the open modal + load
    afterwards reproduces the cancelled-dialogue state exactly (no
    half-applied effects); (3) repeated NPC re-talk is legal and idempotent
    (identical state, exit stays unlocked exactly once); (4) baseline load
    reverts the whole interrupted chain to a consistent replayable state.
  - Complementary one-shot semantics asserted in
    `Act1FinalStateSmokeTest`: the zirat roadside clue consumes once
    (second dispatch rejected).
  - Finding recorded (not a defect): NPC re-talk (Gulsina) is intentionally
    repeatable with idempotent effects; true one-shots are clue/evidence
    targets (zirat clue).
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/game009-verify-{godot,dotnet}-PASS.txt`.

## Completed

- STATE-005 (P0): focused final-state fixtures 2026-09-03.
  - New `game/tests/Act1FinalStateSmokeTest(.cs/.tscn)` in the aggregator;
    drives the real authored chain through RuntimeBridge to the zirat road,
    then proves the STATE-005 matrix:
    (1) forest approach locked before the zirat clue; out-of-order dispatch
    rejected with no state change and no final knowledge;
    (2) zirat clue confirms WITHOUT granting `clue_do_not_answer_rule`;
    (3) pre-forest snapshot: reveal-hidden, cliffhanger not completed;
    (4) forest entry completes the single terminal beat;
    (5) loading the pre-forest save restores the pre-reveal state exactly
    (reveal gone, beat not completed, scene zirat-road);
    (6) ending completes again after restore — once; a post-terminal repeat
    dispatch is scene-locked, rejected, and leaves the terminal state
    byte-identical.
  - Reveal-not-before ordering survives the save/load round trip (MAP-009
    contract + STATE-005 acceptance).
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/state005-verify-{godot,dotnet}-PASS.txt`.

## Completed

- UIUX-005 (P1 REWORK): pause shell + modal stack 2026-09-03.
  - New `game/scripts/PauseMenuUi.cs` (+ tscn in aggregator):
    `PauseMenuUi` owns the pause action end-to-end — opens only when the demo
    root's `CanOpenPause` allows (no menu/intro/ending), gates the player,
    stacks the settings panel on top and re-asserts the modal gate after the
    panel closes (Close() releases input — the exact trap the tracker flags),
    resumes cleanly. In-shell actions: Продолжить, Сохранить/Загрузить
    (bridge lifecycle, status feedback), Настройки, and two-step confirmations
    for Заново (`StartNewGameAsync` -> resume at arrival), В главное меню
    (returns to MainMenuUi, gameplay stays gated), Выход.
  - `SettingsUi`: pause branch removed (PauseMenuUi owns pause routing);
    ui_cancel close retained.
  - New `Act1PauseMenuSmokeTest` in the aggregator: pause gate, save/load
    resume, settings stack + pause-key return, resume, confirmed restart to
    arrival, menu return with gated input.
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux005-verify-{godot,dotnet}-PASS.txt`.

## Completed

- UIUX-001 (P1 CREATE): public main menu 2026-09-03.
  - `game/scripts/MainMenuUi.cs` + `game/scenes/ui/main_menu_ui.tscn`: pure
    presentation overlay (CanvasLayer, group `main_menu`) — title АКT I,
    Новая игра / Продолжить (hidden without a quick slot) / Настройки / Выход;
    no runtime, session or save owner inside the menu; choices reported as
    events; accessibility target (text scale + high contrast).
  - `Act1DemoRoot`: boot now gates the demo behind the menu
    (`BuildMainMenu` → player modal; New Game → `StartNewGameAsync`; Continue
    → `LoadSlotAsync("quick")` after `HasLoadableSlot`; Settings → existing
    SettingsUi; Quit). Intro card builds only after a choice
    (`ShowIntroAfterMenu`).
  - Demo-consuming smokes (launch, walkthrough, corridor, chapter-one) now
    pass the menu through the production New Game button via shared helper
    `Act1MainMenuTestSupport.StartThroughMainMenuAsync` (button press, never
    state injection).
  - New `Act1MainMenuSmokeTest` in the aggregator: menu gate, hidden Continue
    on fresh profile, Settings open/close from menu, Continue restores the
    session after a quick save (and restores any pre-existing quick slot).
  - verify-godot exit 0 (aggregator incl. new smoke; walkthrough PASS
    135,50 m), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux001-verify-{godot,dotnet}-PASS.txt`.
  - Notes: `SettingsUi.Close()` made public (programmatic close from the
    menu flow; behavior unchanged). Expected-error corridors stay out of
    Godot smokes per the aggregator's `^ERROR:` gate.

## Completed

- TEST-006 (P0): save/recovery corruption cases 2026-09-03, engine-side
  (`tests-dotnet/Urman.Core.Tests/DeterminismAndSaveTests.cs`, 3 new facts;
  suite 41+12 green, exit 0):
  - `AtomicStore_FailsSafelyWhenBothPrimaryAndBackupAreUnreadable` — both
    artifacts invalid AND never-written slot → InvalidDataException (fresh
    Continue disabled; no silent new session).
  - `AtomicStore_RecoversBackupWhenPrimaryIsTruncated` — interrupted write
    (byte-truncated primary) recovers from the one-generation backup.
    Semantics discovered and documented: the store keeps exactly ONE backup
    generation = previous primary (backup holds beat 1 after two saves).
  - `StoreRoundTrip_RestoresKernelStateAndSequenceForBothBeats` — full
    store+codec+kernel round trip: beat 2 (clue+rule, seq 2) restores state,
    event sequence and occurrence dedupe (replayed occurrence rejected, no
    duplicate commit); backup holds beat 1 (clue only, rule unknown, seq 1) —
    reveal-not-before ordering survives the round trip.
  - Kernel semantics recorded: `EventSequence` counts committed EVENTS (not
    commands). Existing coverage already included occurrence dedupe,
    snapshot-restore, claim conflicts and rollbacks (RuntimeKernelTests).
  - Godot side of New Game/Continue wiring: covered by SAVE-003's
    `Act1SaveLifecycleSmokeTest` (aggregator). Corruption matrices stay in
    xunit by design (Godot aggregator fails on any `^ERROR:` line).

## Completed

- SAVE-003 (P0): New Game/Continue lifecycle 2026-09-03 (commit `b341042`).
  - `RuntimeBridge.StartNewGameAsync` (reset via existing CreateNewSession,
    entrypoint re-applied, canonical arrival spawn, live user settings
    preserved, slots never deleted) + `HasLoadableSlot` query.
  - New `Act1SaveLifecycleSmokeTest` in the aggregator: reset, settings
    preservation, slot preservation, missing-slot gate. verify-godot exit 0
    (aggregator incl. two new tests), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/save003-verify-{godot,dotnet}-PASS.txt`.
  - Authoring note: Godot aggregator fails on ANY `^ERROR:` log line, so
    expected-error paths (corrupt load) must live in tests-dotnet, not in
    Godot smokes.

- STATE-001 (P0): repo-wide sole-writer caller audit 2026-09-03
  (commit `2365d0c`). Zero unauthorized write paths: only user:// writer is
  the bridge-owned AtomicSaveGameStore; kernel never leaks outside
  RuntimeBridge; QuestRuntimeCoordinator dispatches only kernel-mediated,
  content-gated, deterministically idempotent reconciliation commands invoked
  by the bridge. Evidence:
  `evidence/act1_repo_baseline/state001_caller_audit.md`.

- GAME-010 (P0): no-combat sweep clean 2026-09-03 (commit `8590fcc`): zero
  combat-verb matches across game/scripts, project.godot input map, chapter 1
  content and src-dotnet (fullgame excluded). Evidence:
  `evidence/act1_repo_baseline/game010_no_combat_audit.md`.

## Completed

- STATE-001 (P0): repo-wide sole-writer caller audit 2026-09-03
  (commit `2365d0c`). Zero unauthorized write paths: only user:// writer is
  the bridge-owned AtomicSaveGameStore; kernel never leaks outside
  RuntimeBridge; QuestRuntimeCoordinator dispatches only kernel-mediated,
  content-gated, deterministically idempotent reconciliation commands invoked
  by the bridge. Evidence:
  `evidence/act1_repo_baseline/state001_caller_audit.md`.

- GAME-010 (P0): no-combat sweep clean 2026-09-03 (commit `8590fcc`): zero
  combat-verb matches across game/scripts, project.godot input map, chapter 1
  content and src-dotnet (fullgame excluded). Evidence:
  `evidence/act1_repo_baseline/game010_no_combat_audit.md`.

- Queue-status note (2026-09-03): WAVE 4-5 zone tasks (Z01-Z08) and the
  substantive WAVE 6-7 slices (interior art staging, narrative text polish)
  have their automatable evidence already recorded (composition anchors,
  distinct-frame capture runs, single-owner smokes in MAP-/ART- receipts);
  their remaining slice requires in-engine authored art iteration and human
  motion/360/cultural review (CAPTURE-003/004/006, CULTURE reviews). They
  stay honestly REWORK/OPEN - no PASS is claimed from receipts.

## Completed

- WAVE 3 (ART-003/004/007/008/009/010/011/012): inspection + churn-stop
  slices 2026-09-03, one commit each:
  `7b34059` ART-003 village volumes, `86b45ad` ART-004 FAP full volume,
  `9ebbf49` ART-007 terrain owner, `77c8b30` ART-008 foliage contract,
  `533907a` ART-009 texture churn stop (decision log entry: production set =
  six runtime-referenced families; candidates provenance/out-of-package),
  `ed68c8c` ART-010 single environment owner, `55c6f1f` ART-011 suppression
  inventory, `19a74e4` ART-012 NPC presence. verify-assets exit 0 after the
  decision-log change. All human review components remain open
  (CAPTURE-006, CULTURE-002/003).

## Completed

- MAP-010 (P0): implemented table-driven spawn matrix smoke 2026-09-03
  (commit `5dbdc1e`).
  - New `game/tests/Act1SpawnMatrixSmokeTest(.cs/.tscn)`: iterates ALL 12
    declared spawns in `Act1WorldLayout.Placements` through the production
    `Main.SwitchZone` connected path; asserts exact world transform
    (placement origin + local spawn — layout positions are placement-local),
    declared yaw, `RuntimeBridge.CurrentZoneId` sync, floor within 2 m below
    the spawn (void check) and no capsule clipping at chest height (sphere
    query). Player physics frozen for deterministic assertions.
  - Added to `eng/verify-godot.sh` aggregator; full suite exit 0 with
    `act1-spawn-matrix: PASS 12/12` (`evidence/act1_repo_baseline/map010-verify-godot-PASS.txt`).
  - First authoring iteration had a real build failure (`IsEmpty` on
    `Array<Dictionary>`, `GetWorld3D` on Node) and a coordinate-convention
    finding (placement-local vs world spawn) — both fixed in-slice; no
    production code changed.

- MAP-009 (P0): final trigger contract verified 2026-09-03 (commit `998abc8`).
  - Receipt `evidence/act1_repo_baseline/map009_final_trigger_contract.md`:
    approach gating (zirat clue condition), three `scene/forest` entry
    conditions, terminal effects fire once in `onEnter` (no scene
    interactions -> non-retriggerable), state-assignment idempotency,
    reveal-not-before invariant simulated 16/16, presentation card read-only
    and once-guarded.

- MAP-008: zirat->Kara corridor inspection 2026-09-03 (commit `0748308`).
  - Authored escalation documented: envelope widths 4.2->3.5 m, darkening
    surface colors, asymmetric banks, root clusters, lateral forest shelves,
    bounded Kara value separation; kara_approach capture frames 5/5 distinct.
    Human bidirectional video remains.

- MAP-007: zirat transition inspection 2026-09-03 (commit `0549e7c`).
  - Boundary fence out of road window ("open presentation cue, never a
    blocker"), non-inscribed markers unchanged, village-edge return sightline
    authored, zirat capture frames 5/5 distinct. Religious review =
    CULTURE-003, BLOCKED_EXTERNAL.

- MAP-005 + MAP-006: inspections 2026-09-03 (commit `b4b92fb` — both receipts
  share one commit; deviation from one-task-per-commit noted, strict one-task
  commits resumed from MAP-007).
  - MAP-005: branch read chain (RoadToFap trigger at connector start, two
    diegetic ФАП landmarks, authored BranchWet road surface).
  - MAP-006: duplicate-view blocker removed with evidence — connective return
    frames have three camera transforms and three PNG SHAs in diag-run2;
    continuity anchors documented.

## Completed

- MAP-006: verification slice done 2026-09-03 (no code edit).
  - Former blocker removed with evidence: the three connective_street_return
    capture views are distinct on the current build — diag-run2 frames 23–25
    have three camera transforms and three PNG SHAs (`de1028a3…`,
    `729a5222…`, `177b0ef8…`), `same_as_prev=false`; 44/44 gate passed twice.
  - Continuity anchors documented (FapYardGate/FapYardLoop egress,
    house-to-zirat-return connector, return boundary fences,
    ApproachWorn transition segment, single persistent world).
  - Flat-corridor/empty-horizon judgement stays human.
  - Evidence: `evidence/act1_repo_baseline/map006_return_street_continuity.md`.

- MAP-005: inspection slice done 2026-09-03 (no code edit).
  - Receipt `evidence/act1_repo_baseline/map005_fap_branch_sightline.md`:
    branch read chain — `RoadToFap` trigger at connector start with diegetic
    prompt, two diegetic «ФАП» landmarks (smoke-protected board label +
    street landmark), authored `RoadCrown_BranchWet` road surface (ART-002),
    single FAP destination volume. Human route clips remain with the task.

- MAP-004: inspection slice done 2026-09-03 (no code edit).
  - Receipt `evidence/act1_repo_baseline/map004_arrival_house_chain.md`:
    arrival→yard-gate→door chain documented from `AgentBAct1Layout`
    (`HousePathAxis`, `WalkChain` with the open yard gate gap z 1.75–3.45),
    `BabaiYardOpenGate` composition anchor (`Act1ConnectedWorld.cs:4765`) and
    the walkthrough's ±0.01 m HouseDoor-at-anchor assertion with unchanged
    ray standoff. Physical walk evidence = walkthrough PASS 135,49 m.
  - Human first-time wayfinding observation remains NEW VERIFICATION REQUIRED
    with the task; no marker/teleport fallback introduced.

- MAP-003: inspection slice done 2026-09-03 (no code edit).
  - Receipt `evidence/act1_repo_baseline/map003_connector_inspection.md`:
    per-connector table (lengths 10.4–64.7 m, widths 2.6–5.6 m) computed from
    `Act1WorldLayout.Connectors`; all seven endpoints lie over the shared
    traversal ground (x ∈ [−43,43], z ∈ [−152,+56]) and each has its own
    collision-bearing traversal strip; visual crown owned by AgentB terrain
    (RoadSurface presentation suppressed by design).
  - Mechanical coverage proven by construction + launch-smoke ground-envelope
    assertions + physical walkthrough (135,49 m). Human edge-walk video of all
    seven connectors remains NEW VERIFICATION REQUIRED with this task.
  - Baseline `verify-godot` exit 0 on `e50c64a` (parent commit changed docs
    only).

- BASE-004: REWORK slice done 2026-09-03 (include/exclude decision manifest).
  - Authored `docs/production/act1_package_include_exclude.md`: grounded in the
    current debug presets (`export_filter="all_resources"`, empty filters,
    `game/export_presets.cfg:9-12,50-53`); fixes the Act I include list
    (launch chain scenes/scripts, 5 logical zone scenes, Act I kits,
    production textures, ambient manifest + Act I stems, chapter1 compiled
    pack) and the exclusion list (fullgame campaign + compiled fullgame pack,
    dev campaigns, fullgame scenes, candidate-texture provenance, retired
    candidates, test-only scenes) with an explicit no-deletion enforcement
    boundary.
  - PCK/resource-level comparison remains NEW VERIFICATION REQUIRED behind
    RELEASE-001/RELEASE-003 (no release preset exists yet); no packaging code
    changed in this slice.

- MAP-002: REWORK slice done 2026-09-03 (metric separation).
  - `act1-first-person-walkthrough` PASS line now carries
    `mode=physical-characterbody-walk`; capture PASS line carries
    `mode=presentation-waypoint-audit`; `playtest_plan.md` gained the MAP-002
    rule that 135,49 m (physical walk) and 242,50 m (waypoint audit) are
    different measurements and the audit never counts as gameplay completion.
  - `./eng/verify-godot.sh` exit 0; walkthrough PASS with new label
    (`evidence/act1_repo_baseline/map002-verify-godot-PASS.txt`).

- BASE-006: REWORK slice done 2026-09-03 (queue authority repointed).
  - `execution_backlog.json`: `authority` now names the repo-grounded tracker
    as the active queue, lists the old full tracker as superseded provenance
    (fictional commands never to be executed/cited), and adds the wave/P0> P1>
    P2 queue rule; `orchestrator_policy.queue_rule` mirrors section 0 of the
    tracker; `updated` = 2026-09-03. JSON parse verified.
  - `backlog.md` header: same repoint note; single active queue, no second
    task owner.
  - IDs are imported per executed wave only (BASE/MAP/ART wave-1+2 rows so
    far), not bulk-imported.

- BASE-008: DELETE NOW done 2026-09-03.
  - Removed both tracked host-noise files from the index (kept on disk;
    `.gitignore:16` `.DS_Store` rule keeps future files untracked):
    `.DS_Store`, `public/.DS_Store` (`git ls-files '**/.DS_Store'` now empty).
  - No other cleanup performed (no bulk hygiene, no source-asset deletion).

- ART-006 (automatable portion): documented reproducible export 2026-09-03.
  - Audit: kit already carries the authored threshold/boundary family (13
    roots: asymmetric banks, mixed tree clusters, crooked pine, birch edge,
    root walls, fallen logs, boulders, stump, two distant closure masses with
    a central road gap; 156 meshes / 3,144 tris / 16 materials). Runtime
    composition places each root once with distinct roles — no repeated-clone
    crown wall at composition level.
  - Added `tools/blender/export_kara_forest_edge_kit.py` (same pattern as
    ART-005): validates contract and exports with fixed settings.
  - Proof: byte-stable re-export — GLB SHA-256 `28f58eaa…0d48a` identical
    before/after; `./eng/verify-assets.sh` exit 0.
  - No geometry changes authored: silhouette additions risk the
    creature-as-prop hard stop and require human 360 review (Z08-004/Z08-006,
    CAPTURE-006).
  - BLOCKED_EXTERNAL remainder: human 360/lateral-density art review.

- ART-005 (automatable portion): documented reproducible export 2026-09-03.
  - Audit: the kit already carries the tracker's required restrained geometry
    (boundary fence with deliberate gate gap, swung-open low timber gate,
    non-inscribed plain marker groups, path edge, birch/shrub framing, distant
    village mass; 11 roots / 234 meshes / 7,160 tris / 24 materials; no
    images/symbols/collision).
  - Added `tools/blender/export_zirat_roadside_kit.py`: validates the
    component contract (roots/parents/counts, no images, no collision-like
    names, no camera/light/physics) and exports the GLB with fixed settings.
  - Proof: re-export of the unchanged source is byte-stable — GLB SHA-256
    `f2f87e87…963a3d` identical before/after; `./eng/verify-assets.sh` exit 0.
  - No geometry changes authored: marker/boundary additions require
    cultural/religious review first (tracker hard stop on invented
    inscriptions/symbols).
  - BLOCKED_EXTERNAL remainder: 360° in-engine review + specialist
    religious/local sign-off (CULTURE-003 scope). Evidence package:
    manifest + reproducible export receipt + verify-assets log.

- ART-002: REWORK slice done 2026-09-03 (authored variant road modules + integration).
  - Gap: MainStreet/ReturnStreet/ReturnTransition all placed literal clones of
    the single `RoadCrown_SunkenWet` strip → visible tile rhythm; FAP branch had
    no authored road segment.
  - Generator (`urman_wet_village_road_kit.py`): added two authored variant
    modules — `RoadCrown_BranchWet` (narrower 2.5 m muddier branch strip,
    off-centre crown, 2 puddles, worn patch) and `RoadCrown_ApproachWorn`
    (worn 3.0 m approach strip, 3 clods, 3 patches, 1 restrained puddle) —
    sharing the crown height contract (z 0.018–0.092, relief ≤ 0.18), existing
    19-material palette, axis bake and validation. Rebuilt .blend + GLB
    deterministically (192 meshes / 3,042 tris).
  - Integration (`Act1ConnectedWorld.cs`): component contract +2; return
    transition now uses the distinct `RoadCrown_ApproachWorn` segment instead
    of a third `SunkenWet` clone; new `WetVillageRoadFapBranchCrown`
    (`RoadCrown_BranchWet`) placed at the `village-to-fap-branch` connector
    midpoint. Presentation-only; collision/interaction owners untouched.
  - Manifest + registry updated (hashes/counts/components/status).
  - Verify: `./eng/verify-assets.sh` exit 0 (41/41 hashes),
    `./eng/verify-godot.sh` exit 0 (physical walkthrough PASS 135,49 m to
    completed cliffhanger). In-engine motion review remains part of the
    zone-level human review (Z01-002–Z08-002).
  - Evidence: `evidence/act1_repo_baseline/art002-verify-assets-PASS.txt`,
    `evidence/act1_repo_baseline/art002-verify-godot-PASS.txt`.

- BASE-007: REWORK → verified + registry completed 2026-09-03.
  - Baseline `./eng/verify-assets.sh` exit 0 before edits
    (`base007-verify-assets-baseline-PASS.txt`).
  - Gap found: 4 of 5 Act I kits missing from `assets/asset_registry.json`
    (wet road, village exterior, zirat roadside, kara forest edge; only FAP kit
    was registered). Added entries with real source/derived SHA-256, GLB-derived
    mesh/triangle/material counts cross-checked against manifests (177/2,718;
    106/3,512; 234/7,160; 156/3,144), honest generators (zirat/kara: manual
    .blend export, no generator script) and `art-lock`-open statuses.
  - Added `releaseDisposition` to all 41 assets: runtime-referenced assets =
    `include-in-act1-release` (per `PainterlyMaterialLibrary.cs` texture usage
    and `ambient_manifest.json` zone routing); v3–v6 texture candidates =
    candidate-provenance/exclude; `audio.ambient.water-edge` =
    deferred-fullgame-scope/exclude. No unknown licenses (all
    Project-original/Project-generated).
  - Post-edit `./eng/verify-assets.sh` exit 0, `assets=41 derived=41
    explicit_sources=15` (`base007-verify-assets-with-kits-PASS.txt`).

- ART-001: KEEP 2026-09-03 (docs-only; three existing art docs fixed as the
  Act I acceptance authority, no new style bible).
  - Decision recorded in `docs/urman_knowledge_base/decision_log.md`
    (2026-09-03 entry): `design_style.md`,
    `art/act1_master_layout_2026-08-17.md`,
    `art/act1_visual_reference_bible_2026-08-17.md` are the acceptance
    reference for every Act I art/zone task; target PNGs are references, not
    runtime screenshots.
  - Manual cross-check vs current runtime: docs declare Painterly Low-Poly
    full-volume walkable 3D; runtime implements it via
    `PainterlyMaterialLibrary` presets + authored Act I kits (consistent, no
    contradiction found).

- BASE-005 + CAPTURE-002: PASS 2026-09-03 (capture harness diagnostics + readback
  pose assertion; two consecutive clean 44/44 runs).
  - Diagnosis: supplied 44 files / 25 unique failure did NOT reproduce on
    current HEAD. Harness code unchanged since `fc58c40` (single-commit history);
    failure was environmental/dirty-tree in the supplied handoff run, not a
    current-code settle/readback/camera defect. Diagnostic run shows perfectly
    deterministic pacing: `drawn_frame` advances exactly 18 per spec (37 → 811),
    zero `same_as_prev` frames, all aim deviations ≤ 0.028°.
  - Causal guard added (also closes CAPTURE-002 scope): readback-time assertion
    that actual camera forward matches the requested target within 0.5° (fails
    immediately if the pose was not the rendered pose), plus per-frame log of
    camera transform/forward, active logical zone, drawn frame index, sha256
    prefix and same-as-previous flag. 44/44 uniqueness gate unchanged and
    unweakened; no delays increased.
  - Runs: run1 `/tmp/urman-base005-diag.6NkUiP` exit 0, 44 PNG / 44 unique;
    run2 `/tmp/urman-base005-diag2.Qt8gyk` exit 0, 44 PNG / 44 unique (raw PNGs
    ephemeral in /tmp; retained evidence = logs + receipt below). The
    pre-existing duplicate groups named in the tracker (connective_street_return
    ×3, fap_exterior ×5, zirat ×4, kara_approach ×5) all produced distinct
    transforms and distinct PNG SHAs in both runs.
  - Evidence: `evidence/act1_repo_baseline/base005-capture-diag-run1-PASS-44of44.txt`,
    `evidence/act1_repo_baseline/base005-capture-diag-run2-PASS-44of44.txt`,
    `evidence/act1_repo_baseline/base005-capture-receipt.json`.
  - Note: CAPTURE-003 remains open until the post-art-candidate rerun with a
    retained evidence package; CAPTURE-001 keeps the failed 44/25 receipt as
    historical FAIL evidence.

- MAP-001 verify: DONE — VERIFY 2026-09-03 (no code edit, canonical baseline recorded).
  - `./eng/verify-godot.sh` exit 0 on `5539c9b`; walkthrough smoke PASS
    (`distance=135,50m final-zone=kara_urman_night cliffhanger=completed`).
  - Canonical baseline fixed in receipt: 5 placements, 7 connectors, 8 direct
    visual zones (`Act1ConnectedWorld.cs:805-812`; capture harness asserts the
    same list at `Act1FullRouteCoreWorldCapture.cs:248-262`).
  - Evidence: `evidence/act1_repo_baseline/map001_route_baseline.md`.

- BASE-003 inspection-first: KEEP/VERIFY 2026-09-03 (no code edit).
  - Contract proven by source inspection on `66684b7`: `act1_demo.tscn` →
    `Act1DemoRoot` → one `main.tscn` instantiation with
    `EnableAct1ConnectedWorld=true`; one `Act1ConnectedWorld` under `ZoneHost`;
    connected-mode `SwitchZone` guards non-Act-I zones; fallback per-scene
    loader only reachable with flag false (tests/fullgame). Owners: 5 placements
    / 7 connectors in `Act1WorldLayout`; single labeled FAP owner with
    suppressed `Fap_*` families; `CountActiveWorldEnvironments == 1`; single
    `RuntimeBridge` + single `AmbientAudioDirector` in `main.tscn`; no
    scene-local narrative store. Assertions enforced by
    `Act1DemoLaunchSmokeTest.cs:41-109`.
  - Evidence: `evidence/act1_repo_baseline/base003_owner_inspection.md`,
    `evidence/act1_repo_baseline/base003-verify-godot-PASS.txt` (exit 0).

- BASE-001 + BASE-002 preflight: PASS 2026-09-03 (commit `66684b7`).
  - Root cause of initial `verify-dotnet` FAIL: stale frozen golden fixture
    `tests-dotnet/fixtures/content/urman.chapter1.compiled.v1.json` (fp `d89a…`)
    vs legitimate HEAD authored content (Mansur PC gate, Naila dialogues, house
    exit gates, zirat clue; fp `38fdae…`). Runtime pack
    `game/content/urman.chapter1.compiled.v1.json` already matched fresh compile.
  - Fix (test maintenance only, no content change): refreshed fixture from
    `ContentCli compile`, updated hardcoded fingerprint in
    `ContentCompilerParityTests.cs:26`.
  - `validate` exit 0 (4 modules, 4 campaigns); `simulate urman.chapter1` 16/16.
  - Evidence: `evidence/act1_repo_baseline/verify-dotnet-baseline.txt` (initial
    FAIL), `evidence/act1_repo_baseline/verify-dotnet-PASS.txt`,
    `evidence/act1_repo_baseline/verify-godot-PASS.txt`.

## Blocked / BLOCKED_EXTERNAL

- (none yet)

## Reopened

- (none)

## Commands / evidence (exit codes)

- `git rev-parse HEAD && git branch --show-current && git status --short` → HEAD `fc58c40`, branch `main`, dirty: `M .DS_Store`, `M AGENTS.md`, `?? URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (exit 0)
- verify-dotnet / verify-godot baseline runs: pending (preflight)

## Dirty state / чужие изменения (preserved, not mine)

- `M AGENTS.md` — pre-existing narrow exception clause for this exact user-authorized OpenCode Act I run (12 insertions). Preserved, excluded from task-owned commits.
- `M .DS_Store` (binary 8196 → 10244) — host noise, owned by future BASE-008. Untouched for now.
- `?? URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` — tracker input file, untracked. Left untracked.

## Next ready IDs

- Immediate: UIUX-006/007 (settings navigation + user-settings store),
  AUDIO-002 sound map doc, NARR-016 storytelling matrix doc, UIUX-003/004.

## External gates

- ART-005 remainder: specialist religious/local sign-off + 360 in-engine
  review — BLOCKED_EXTERNAL.
- ART-006 remainder: human 360/lateral-density art review — BLOCKED_EXTERNAL.
- MAP-003 remainder: manual seven-connector edge-walk video — BLOCKED_EXTERNAL.
- MAP-004 remainder: observed first-time wayfinding — BLOCKED_EXTERNAL.

## Resume point

- If interrupted now: resume at UIUX-007 — small versioned
  `UserSettingsStore` (display/input/audio/accessibility only, no narrative
  fields) + persistence smoke, per SAVE-006 boundary; verify
  `./eng/verify-dotnet.sh && ./eng/verify-godot.sh`, commit
  `task(UIUX-007): ...`. Then UIUX-006, AUDIO-002, NARR-016.
