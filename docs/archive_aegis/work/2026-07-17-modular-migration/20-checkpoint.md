# Todo Checkpoint Draft — старт оркестрации

Date: 2026-07-17

## Current todo

- [x] Создать и проверить task package MM-00–MM-80.
- [x] Создать goal и выделенную branch.
- [x] Подтвердить исходный content/build baseline.
- [x] Выполнить MM-01–MM-03 и заморозить архитектурный контракт.
- [x] Выполнить MM-10 authority/ID/retirement baseline.
- [x] Выполнить MM-20–MM-45 последовательно по dependency graph.
- [x] Выполнить MM-50–MM-53 свежими implementer-субагентами без параллельных edits.
- [x] Выполнить MM-54 composition-root cutover и закрыть зависимые re-freeze.
- [x] Выполнить MM-55 whole-pack closure.
- [x] Выполнить MM-60 Content Lab, stress fixtures и E2E.
- [x] Получить независимую приёмку реализованного MM-54P/MM-55P/MM-70 amendment.
- [x] Выполнить MM-80 и независимый финальный review.

## Active slice

Модульная миграция завершена. Final architecture review вернул `PASS` без P0/P1/P2; после него удалён и absence-gated unreachable `src/os/data/web_content.json`, а historical MVP handoff явно отделён от действующего compiled-content пути. Полный финальный run зелёный; игра остаётся не MVP release, а архитектурно готовой к его достройке.

## Evidence refs

- `npm run validate:content` — pass, 2026-07-17.
- `npm run build` — pass, 72 modules, 2026-07-17.
- `git branch --show-current` — `codex/modular-migration`.
- `docs/modular_migration/` — 22 files, dependency/frontmatter check pass.
- MM-01 `status: frozen`, MM-03 `status: passed`; 12/12 fixture lifecycle rows записаны.
- MM-03 spec re-review — PASS, P0/P1 0 после закрытия пяти P1.
- MM-03 quality re-review — PASS, P0/P1 0 после закрытия execution-contract findings.
- `git diff --check` и task-doc whitespace scans — pass.
- MM-10 spec regression — PASS; final quality review — PASS; открытых P0/P1 нет.
- Повторный pre-MM-20 baseline: `npm run validate:content` pass; `npm run build` pass, 72 modules; `git diff --check` pass.
- MM-20: 17 portable schemas, validator и 23 focused regressions; final bounded `SPEC PASS` и `QUALITY PASS`, P0/P1/P2 нет.
- MM-20 amendment: 26/26 schema tests; `AMENDMENT SPEC PASS` + `AMENDMENT QUALITY PASS`.
- MM-30: 57/57 content tests; final `SPEC PASS` + `QUALITY PASS`; explicit compiler/Vite boundary and deterministic generated types accepted.
- MM-31/MM-40 bounded amendment: decision log records immutable `RuntimeContext.query` claim projection; all 12 MM-03 fixture paths re-verified `MM-03 REFREEZE PASS`; final quality PASS. Query is read-only and makes capability teardown prove owner release without a second state owner.
- MM-31: final `SPEC PASS` + `QUALITY PASS`; 58 content tests, generated-type drift check, 54 runtime tests, 2 architecture tests, build (72 modules) and diff check passed. JSON-only snapshots preserve ledger/claims/event sequence; invalid input fallback is side-effect free; sequence exhaustion rejects before reducer commit; runtime descriptors accept only own data fields.
- MM-40: final `SPEC PASS` + `QUALITY PASS`; generic quest lifecycle, capability host and deterministic world primitives accepted. Cleanup is claim-query-proven, re-entry-safe and child-complete; scheduler uses lease/ack; custody conflicts before effect preflight.
- MM-41: final `SPEC PASS` + `QUALITY PASS`; campaign-ordered logical resolver catalog, deterministic AssetRef variants, structured localized TextRef/vocabulary model and accessible AudioResolver accepted. Full verification: content 58/58, runtime 59/59, architecture 2/2, generated type drift, build and diff check pass. Production URL host adapter remains exclusively for MM-54.
- MM-42: final `SPEC PASS` + `QUALITY PASS`; strict JSON-only GameSnapshotV2, exact CampaignLock/fingerprint, typed errors and staged capability restore accepted. Initial P1 for silent unknown RNG member dropping was repaired and regression-covered. Full verification: content 58/58, runtime 65/65, architecture 2/2, generated type drift, build and diff check pass.
- MM-45: final `SPEC PASS` + `QUALITY PASS`; neutral synthetic two-campaign pack proves role/text/asset swap, quest reorder, deterministic runtime/snapshot/restore and cleanup. Five deterministic negative preflight cases retain module/path/pointer. Full verification: canary 5/5, content 58/58, runtime 65/65, architecture 2/2, build and diff check pass.
- MM-50: final `SPEC PASS` + `QUALITY PASS`; portable canonical Chapter 1 data and narrative guards accepted. Core is intentionally empty until two real consumers exist; full old-PC bodies remain MM-52-owned. Reviewer-incidental FAP desk change was reconciled against MM-10 row 197, fully re-verified and independently re-reviewed. Full verification: Chapter data 5/5, content 63/63, runtime 65/65, architecture 2/2, generated type drift, build and diff check pass.
- MM-51: final `SPEC PASS` + `QUALITY PASS`; generic active scene/UI providers accepted. Initial reviews repaired atomic target-entry, real audio/config, async dispose race, exact capability definition/provider preflight and terminal post-dispose APIs. Full verification: focused 19/19, runtime 84/84, content 63/63, architecture 2/2, build and diff check pass.
- MM-20 old-PC amendment: bounded `MM-03 OLDPC REFREEZE PASS`; 17 schemas, 45 focused schema/compiler/architecture checks, content 66/66, generated-type drift and diff check pass. The closed optional `DocumentDefinition.oldPc` contract is the sole portable metadata seam for MM-52.
- MM-52: final `SPEC PASS` + `QUALITY PASS`; data-driven old PC/DedOS capability, full Markdown bodies and injected ComputerScene seam accepted. Full verification: content 66/66, runtime 90/90, architecture 2/2, build and diff check pass. The intentionally red legacy validation scripts/storage retirement remain explicitly MM-70-owned.
- MM-53: final `SPEC PASS` + `QUALITY PASS`; dev-only MainMap descriptor and injected bindings accepted. Two P1 lifecycle findings were repaired: dynamic action listeners and exactly-once terminal cleanup after a failed `scene.init()`. Full verification: focused 6/6, runtime 96/96, architecture 2/2, build and diff check pass.
- Capability-presentation amendment: independent `QUALITY PASS`; no P0/P1/P2. Focused runtime 102/102, architecture 2/2 and diff check pass; the bounded port is fully frozen.
- Scene-to-dialogue amendment: independent `MM-03 REFREEZE SPEC PASS` + `QUALITY PASS`; exact compiler diagnostics, type generation through `allOf`, atomic start-node commit, no replay and forest-only final rule verified.
- MM-54: final root verification PASS — content preflight 3 modules/3 campaigns, generated types current, content 68/68, runtime 102/102, architecture 2/2, integration 4/4, production build (55 modules) and diff check. Production seed is campaign-fingerprint-derived; deterministic session scopes distinguish a changed re-entry but preserve post-restore duplicate filtering. `graphify update .` remains host-blocked only at watch rebuild (`Operation not permitted`).
- MM-55: final `SPEC PASS` + `QUALITY PASS`; the first review found and repaired three scanner P1 plus a dotted-scene quality P1. Final static gate checked 86 production source files; closure integration 3/3, content preflight 3/3, runtime 102/102, architecture 2/2, production build (55 modules) and diff check passed.
- MM-55 Content-Lab amendment: `src/lab/**` is the only added dev-only implementation path. Static gate still rejects Lab/MainMap imports from every production source; checker 86 files and closure 4/4 passed.
- MM-60: final `SPEC PASS` + `QUALITY PASS`; no open P0/P1/P2. Review repairs removed fake post-compile fingerprints, made all variants separately compiled immutable packs, hardened Chrome/CDP diagnostics/teardown/focus, made inspection selectors explicit, retained failed cleanup runs, and aligned all 12 exact config/event/outcome contracts. Final verification: content 3 modules/3 campaigns; content 68/68; runtime 102/102; architecture 2/2; static gate 86; closure 4/4; real Vite+Chrome E2E 8/8 with approved loopback; production build 55 modules; diff check pass. `src/lab/**` and test harness remain absent from production bundle.

## Blocked on

Нет функционального blocker для миграции. Commit/push authority по-прежнему отсутствует. `graphify update .` выполняет extraction, но host watch rebuild не имеет разрешения (`Operation not permitted`); это не блокирует code/content verification.

## ResumeStateHint

При возобновлении читать `10-intent.md`, этот checkpoint, goal state, `00_ORCHESTRATOR_README.md` и handoff последнего принятого task. Не запускать downstream task, пока spec и quality review его dependency не закрыты.

## DriftCheckDraft

- Intent: aligned
- Scope: aligned; MM-54 cutover retired only owners assigned by MM-10/54
- Compatibility: exact pre-MVP reset and V2 rejection вызываются из production boot через injected storage port; legacy reader/fallback отсутствует
- New fallback/owner: none; kernel is sole state writer and scene/dialogue handoff is one atomic command
- Retirement track: MM-70 code/validator retirement и MM-54P boot wiring приняты independent review
- Decision: complete

## Next

Начать отдельный product slice: authoring и playtest текущей Chapter 1 campaign по portable contracts. Не возрождать legacy runtime owners.
