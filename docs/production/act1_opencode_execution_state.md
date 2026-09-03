# Act I OpenCode Execution Ledger

Run: 2026-09-03, single external OpenCode implementer, canonical checkout
` /Users/unterlantas/Documents/GitHub/URMAN`, branch `main`.

- Base commit: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`)
- Current commit (ledger init): `fc58c408326c0fff420641cdd82e712bb51b4f6c`
- Branch: `main`
- Tracker: `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (root, untracked input)
- Current task/slice: BASE-007 committed; next ART-002 wet-road authored rework.

## Completed

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

- Immediate: ART-002 (wet-road authored rework), then ART-005 (zirat kit), then
  ART-006 (Kara kit).

## External gates

- None entered yet. Human/cultural/audio/platform gates will be marked BLOCKED_EXTERNAL with evidence packages as encountered.

## Resume point

- If interrupted now: resume at ART-002 (`assets/source/blender/act1/
  urman_wet_village_road_kit.blend` + `.py`, rebuilt GLB + manifest +
  registry hash update; verify via `./eng/rebuild-assets.sh &&
  ./eng/verify-assets.sh`).
