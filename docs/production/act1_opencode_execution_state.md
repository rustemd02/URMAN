# Act I OpenCode Execution Ledger

Run: 2026-09-03, single external OpenCode implementer, canonical checkout
` /Users/unterlantas/Documents/GitHub/URMAN`, branch `main`.

- Base commit: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`)
- Current commit (ledger init): `fc58c408326c0fff420641cdd82e712bb51b4f6c`
- Branch: `main`
- Tracker: `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (root, untracked input)
- Current task/slice: BASE-008 committed; next BASE-006 backlog repoint.

## Completed

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

- Immediate (WAVE 2): BASE-006 (execution_backlog.json repoint), MAP-002
  (metric labels), BASE-004 (package manifest), then MAP-003+.

## External gates

- ART-005 remainder: 360° in-engine review + specialist religious/local
  sign-off — BLOCKED_EXTERNAL (needs human reviewer; evidence package
  prepared in `game/assets/models/act1/urman_zirat_roadside_kit_manifest.md`
  and `evidence/act1_repo_baseline/art005-export-reproducible-PASS.txt`).
- ART-006 remainder: human 360/lateral-density art review — BLOCKED_EXTERNAL.

## Resume point

- If interrupted now: resume at BASE-006 (`docs/urman_knowledge_base/
  execution_backlog.json` + `backlog.md`: mark old full tracker superseded,
  point active queue at the repo-grounded tracker; JSON parse check).
