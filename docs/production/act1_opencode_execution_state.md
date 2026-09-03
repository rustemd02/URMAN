# Act I OpenCode Execution Ledger

Run: 2026-09-03, single external OpenCode implementer, canonical checkout
` /Users/unterlantas/Documents/GitHub/URMAN`, branch `main`.

- Base commit: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`)
- Current commit (ledger init): `fc58c408326c0fff420641cdd82e712bb51b4f6c`
- Branch: `main`
- Tracker: `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (root, untracked input)
- Current task/slice: MAP-001 committed; next BASE-005 capture diagnosis.

## Completed

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

- Immediate: BASE-005 (causal capture 44/25 diagnosis + fix + 44/44 rerun).

## External gates

- None entered yet. Human/cultural/audio/platform gates will be marked BLOCKED_EXTERNAL with evidence packages as encountered.

## Resume point

- If interrupted now: resume at BASE-005 duplicate-capture diagnosis
  (`game/tests/Act1FullRouteCoreWorldCapture.cs`,
  `eng/capture-act1-full-route-core-world.sh`): reproduce duplicate groups with
  per-frame camera transform/zone/hash logging, fix settle/readback/camera
  application cause, then single full 44/44 rerun to
  `<empty-dir-outside-repo>`.
