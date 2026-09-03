# Act I OpenCode Execution Ledger

Run: 2026-09-03, single external OpenCode implementer, canonical checkout
` /Users/unterlantas/Documents/GitHub/URMAN`, branch `main`.

- Base commit: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`)
- Current commit (ledger init): `fc58c408326c0fff420641cdd82e712bb51b4f6c`
- Branch: `main`
- Tracker: `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (root, untracked input)
- Current task/slice: BASE-003 inspection-first (next)

## Completed

- BASE-001 + BASE-002 preflight: PASS 2026-09-03.
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
  - Commit: `task(BASE-001): preflight BASE-002` (this ledger + baseline doc +
    fixture/test fix only).

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

- Immediate: BASE-003 inspection-first, then MAP-001, BASE-005.

## External gates

- None entered yet. Human/cultural/audio/platform gates will be marked BLOCKED_EXTERNAL with evidence packages as encountered.

## Resume point

- If interrupted now: resume at BASE-003 inspection-first
  (`technical_architecture.md`, `decision_log.md`, `game/scripts/Main.cs`
  boundary vs `Act1DemoRoot` → `main.tscn` → `Act1ConnectedWorld` chain).
