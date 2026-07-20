---
task_id: MM-70
title: Retirement старых persistence и compatibility paths
status: complete
kind: retirement
depends_on: [MM-60]
blocks: [MM-80]
parallel_group: null
owner_scope:
  - src/systems/SaveSystem.ts
  - src/systems/VocabularySystem.ts
  - src/os/apps/oldPcHub.ts
  - src/os/apps/notepad.ts
  - src/ui/NotebookUI.ts
  - scripts/validate-old-pc-content.mjs
  - scripts/validate-route-graph.mjs
  - scripts/validate-clue-graph.mjs
  - scripts/validate-content.mjs
  - src/runtime/persistence/**
  - tests/integration/persistence-cutover/**
  - package.json
forbidden_scope:
  - content/modules/**
  - src/runtime/kernel/**
deliverables:
  - Единственный production persistence owner
  - Одноразовый clean reset pre-MVP keys
  - Удаление regex validators, globals и compatibility carriers
verification:
  - npm run content:check
  - npm run test:unit
  - npm run test:architecture
  - npm run test:e2e
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-70 — Persistence и legacy retirement

`MM-54` может уже удалить перечисленные legacy UI/system файлы, если это необходимо для атомарного удаления `GameState` и compilation closure. В таком случае MM-70 не восстанавливает их: он проверяет отсутствие старого пути и завершает reset/storage/validator retirement.

## Цель

После перехода smoke/tests на новый runtime удалить временно оставленные storage, validator и global paths. В конце пакета у gameplay один snapshot owner и один semantic content compiler.

## Clean reset

При первом запуске новой версии удалить только:

- `urman.mvp.save.v1`;
- `urman.oldPcHub.state.v1`;
- `game.notepad.files.v1`;
- `game.notepad.activeFile.v1`;
- старые capability/test namespaces, найденные аудитом `MM-10`.

Сохранить `urman_username` и явно классифицированные user preferences. Показать одноразовое понятное уведомление о сбросе pre-MVP-прогресса. После завершения reset записать versioned marker, чтобы уведомление не повторялось.

## Coordinated MM-54P amendment

MM-70 сохраняет единоличное владение gateway. Он добавляет только три public operation, нужные уже существующему composition root: browser factory с injected global, strict запись готового `GameSnapshotV2` и удаление current V2 save после explicit reset confirmation. `Game`/`main` не получают raw storage API и не могут читать, писать или очищать storage напрямую. Для первого запуска `applyLegacyReset()` по-прежнему удаляет только четыре audited v1 keys; deletion `urman.mvp.save.v2` разрешена только для уже показанного typed reset proposal после несовместимого V2 load.

## Retirement

- Все gameplay/UI persistence проходит через `GameSnapshotV2` codec и storage gateway.
- Old PC/notepad local UI state хранится в namespaced capability snapshot либо session-only state.
- Удалить прямые `localStorage` calls вне gateway.
- Удалить production `window.URMAN` и старый smoke bypass.
- Удалить regex/TS-source validators после переноса их проверок в semantic compiler.
- Удалить compatibility adapters, старые arrays/allowlists и duplicate state owners, оставшиеся в матрице `MM-10`.

Не оставлять старый validator «на всякий случай»: если новый compiler не покрывает проверку, сначала добавить semantic test, затем удалить старый путь.

## Acceptance

- Fingerprint mismatch отказывает в load и предлагает reset без частичного state.
- Reset очищает все gameplay namespaces и сохраняет username/preferences.
- Повторный запуск не показывает notice второй раз.
- Static gate не находит raw storage/global bypass/старые parser callers.
- Default campaign и isolated Lab saves не пересекаются.

## Handoff

```text
Task: MM-70
Status: implementation complete; acceptance blocked on composition-root amendment
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: src/runtime/persistence/production-persistence-gateway.{mjs,d.mts}; tests/integration/persistence-cutover/production-persistence-gateway.test.mjs; package.json; deleted scripts/{validate-old-pc-content,validate-route-graph,validate-clue-graph,validate-content}.mjs; docs/modular_migration/70_LEGACY_PERSISTENCE_RETIREMENT.md
Behavior / contracts delivered: createProductionPersistenceGateway({ storage, saveKey? }) is the sole browser-storage boundary and accepts only an injected browser-like port; it never discovers a browser global, creates a second store or calls clear(). save() serializes only encodeGameSnapshotV2 output at urman.mvp.save.v2. load() strictly JSON-parses and decodeGameSnapshotV2-validates the snapshot/campaign lock before returning ready; malformed, incompatible or CampaignMismatch data returns the typed reset-required result with an explicit reset proposal and no restoration call. restore() stages through restoreGameSnapshotV2 only after that successful decode. applyLegacyReset() is opt-in and deletes exactly urman.mvp.save.v1, urman.oldPcHub.state.v1, game.notepad.files.v1 and game.notepad.activeFile.v1; MM-10 audit found no retired capability/test namespace to add. It writes urman.persistence.reset.v2=completed only after every removeItem succeeds, returns the one-time Russian notice only on success and preserves urman_username, urman.content-lab.v1, urman.mvp.save.v2 and unknown origin keys. Existing old-PC/notepad state remains session-only; deleted system/UI source paths remain absent. The four legacy regex/TS-source validator scripts and their package callers were retired without an alias; semantic content compiler/tests remain under content:check and test:unit.
Verification commands: node --test tests/integration/persistence-cutover/production-persistence-gateway.test.mjs; npm run content:check; npm run test:unit; npm run test:architecture; node scripts/check-runtime-architecture.mjs; npm run test:e2e; npm run build; git diff --check
Verification results: PASS — focused persistence cutover 5/5 (strict V2 save/load/restore, malformed JSON and fingerprint mismatch without restore, reset success/failure marker ordering and static retirement audit); content audit 3 modules/3 campaigns; content 68/68; runtime 102/102; architecture 2/2; static gate 88 production source files; local Vite+Chrome CDP e2e 8/8; production build 55 modules; diff check clean. The required browser run used approved local-loopback permission only. No command touched actual browser/user storage: reset tests use injected in-memory storage.
Retired owners / remaining callers: SaveSystem.ts, VocabularySystem.ts, NotebookUI.ts, the old regex validators and validate:* package scripts remain deleted. No production raw localStorage/window.URMAN caller is reintroduced in the scoped source paths. content:check remains the semantic compiler entrypoint. No compatibility adapter, v1 reader, migration fallback or broad reset was retained.
Risks or drift: src/game/Game.ts already has snapshot()/restore() but has no injected storage seam, and MM-70 owner scope forbids changing Game/composition root. The new gateway is therefore the sole tested persistence boundary but is not yet instantiated by production UI; a later explicitly-scoped composition change must inject the browser storage port and surface the reset proposal/notice. This task deliberately did not add a top-level storage side effect or a second state owner.
Required next task: explicitly scoped MM-54 composition-root amendment that injects this gateway into production boot and renders the one-time reset proposal/notice. MM-80 cannot start until that P1 is closed.
```

## Independent acceptance — 2026-07-18

Result: `NOT PASS` — one open P1, no P0.

`createProductionPersistenceGateway` and its focused integration tests satisfy the storage/reset contract in isolation, but no production caller creates it. `Game`/`main` remain the MM-54-owned composition root, so first production launch cannot perform the reset, show its notice, load a V2 snapshot or surface a fingerprint mismatch. MM-70 must not bypass that boundary with a top-level side effect. An explicitly scoped MM-54 amendment is required before this task and the whole migration can be accepted.

## Coordinated MM-54P/MM-70 implementation handoff — 2026-07-18

Status: implementation complete; independent SPEC/QUALITY acceptance pending. This supersedes the production-boot P1 above; the historical review result remains as the reason for the amendment.

`createProductionPersistenceGateway` now accepts the closed `RuntimePersistencePort`, not pack/descriptors/handlers or an orphan run. It persists through `capture()`, strictly validates through `decode()` and asks the port to perform the sole live-run replacement through `restore()`. Its injected browser factory is the only production `localStorage` adapter. `discardCurrentV2Save(proposal)` rejects before a typed mismatch proposal exists, accepts only the exact pending proposal, consumes it on success and retains it after a storage failure for a retry. A successful discard removes only `urman.mvp.save.v2`; it cannot run the v1 reset, remove the marker, username or the Content Lab namespace.

The composition root applies `applyLegacyReset()` once on boot, surfaces its notice, handles missing/valid/rejected V2 outcomes, and writes V2 after the initial entry plus committed player actions. Browser tests prove v1 one-time removal, V2 restore without entry replay, autosave, typed mismatch confirmation and preservation of marker/username/Lab after confirmed V2 discard.

Verification: `node --test tests/integration/persistence-cutover/production-persistence-gateway.test.mjs` 8/8; `npm run content:check` 3 modules/3 campaigns; `npm run test:unit` content 68/68 and runtime 102/102; `npm run test:architecture` 2/2; static gate 88 files; real Vite+Chrome `npm run test:e2e` 10/10; production build 56 modules; `git diff --check` clean. The suite includes both a failing-storage regression that proves a committed transition remains committed with an aria-live error and a failed-discard retry regression that preserves proposal eligibility. The loopback-only E2E was approved; no development command touched an existing user browser profile.

Historical next task at this handoff: independent review of the coordinated MM-54P/MM-55P/MM-70 amendment. It later passed; MM-80 is complete (see `00_ORCHESTRATOR_README.md`).

## Final independent acceptance — 2026-07-18

Result: `QUALITY PASS`; no open P0/P1/P2.

MM-70 is accepted with its coordinated MM-54P/MM-55P amendment. The production gateway is the sole gameplay-storage boundary; it receives a closed bootstrap persistence port, never owns a live run and has no v1 reader, broad clear or fallback. The four audited v1 keys reset once with a notice, while current V2 deletion requires the exact pending typed proposal and preserves the reset marker, username, preferences and Content Lab namespace. Legacy validators and their package callers remain retired.
