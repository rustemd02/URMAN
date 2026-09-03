# MAP-009 Verification Receipt — Final Cliffhanger Trigger Contract (P0)

- Date: 2026-09-03
- Commit: `0748308` (branch `main`)
- Method: canonical-owner inspection of content contract + runtime wiring +
  existing smoke/simulation evidence; no code edit.

## Trigger placement and approach gating (before commit)

- The last approach interaction `zirat-road-to-forest`
  (`definitions.json:1303-1311`, physical target `ZiratRoadToForest` at
  `StyleBenchmarkZone.cs:896-904`) is condition-gated on
  `knowledge/clue_marat_last_route_near_zirat == confirmed` — the zirat
  roadside clue must be inspected first; the unavailable target gives no
  silent passage to the forest.
- `scene/forest` entry requires **three** conditions
  (`definitions.json:1329-1335`): the zirat clue confirmed, Rinat
  `alerted`, and beat `rinat-visible-before-edge` completed. Sequence breaks
  (skipping Rinat or the clue) cannot enter the final scene.

## Terminal state is single, data-owned, and non-retriggerable

- The forest scene has `interactions: []`; the terminal effects fire in
  `onEnter` only (`definitions.json:1336-1341`):
  `clue_do_not_answer_rule = confirmed` and
  `beat/cliffhanger-hard-cut = completed`. There is no repeated interaction
  target to re-fire.
- Both effects are state assignments (`knowledge.set-status`,
  `beat.set-state`), not appends — re-entry or replay yields the same state,
  no duplicate progress events.
- Presentation is read-only: `Act1DemoRoot.EvaluateEndingState`
  (`Act1DemoRoot.cs:210-244`) shows the «НЕ ОТВЕЧАЙ» card only when the shared
  beat state reads `completed`, guarded by `_endingShown` (once); the wrapper
  never writes narrative state (RuntimeBridge sole owner).

## Early-reveal impossibility

- Campaign invariant `do-not-answer-after-rinat`
  (`campaign.json:51-57`, `kind: reveal-not-before`, severity `error`):
  `clue_do_not_answer_rule` may not be confirmed before
  `beat/rinat_do_not_answer`. Enforced by the content validator/simulator —
  BASE-002 evidence: `simulate urman.chapter1` 16/16 beats, invariants pass
  (`evidence/act1_repo_baseline/verify-dotnet-PASS.txt`).
- Companion invariant `cliffhanger-reachable` (required-reachable) proves the
  ending is not just blocked but actually reachable from arrival.
- Runtime proof: `act1-first-person-walkthrough: PASS … final-zone=
  kara_urman_night cliffhanger=completed` (exit 0 on every recent verify run)
  walks the real chain and asserts the single completed terminal beat.

## Backtracking and spawn-after-load

- Backtracking before commit: the zirat clue and Rinat beats persist in the
  shared state; re-approach after backtracking re-evaluates the same entry
  conditions (idempotent). Exhaustive interruption/save-load matrices around
  the final trigger are owned by GAME-009 / SAVE-002 / STATE-005 (open, P0
  hardening wave) — this receipt covers placement/approach/idempotency.
- Spawn-after-load at the kara zone (`kara_urman_night@village_path`) is part
  of the MAP-010 spawn matrix.

## Verdict

No early reveal, no re-triggerable final target, single terminal state,
reveal-not-before invariant simulated green. No code edit required; reopen
conditions: any change to scene/forest entry conditions, final effects, or the
demo ending guard.
