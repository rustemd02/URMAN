---
task_id: MM-54
title: Composition root и единовременный runtime cutover
status: complete
kind: integration-and-retirement
depends_on: [MM-50, MM-51, MM-52, MM-53]
blocks: [MM-55]
parallel_group: null
owner_scope:
  - src/game/Game.ts
  - src/game/GameState.ts
  - src/game/SceneManager.ts
  - src/scenes/ChapterScene.ts
  - src/scenes/MainMenuScene.ts
  - src/scenes/ZiratMiniGame.ts
  - src/main.ts
  - src/runtime/bootstrap/**
  - content/campaigns/**
  - src/data/**
  - src/systems/QuestSystem.ts
  - src/systems/InventorySystem.ts
  - src/ui/DialogueUI.ts
  - src/scenes/ForestScene.ts
  - src/scenes/HouseScene.ts
  - src/scenes/IntroScene.ts
  - src/scenes/MosqueScene.ts
  - src/scenes/RouteNavigationScene.ts
  - src/systems/DialogueSystem.ts
  - src/systems/InvestigationSystem.ts
  - src/systems/SaveSystem.ts
  - src/systems/VocabularySystem.ts
  - src/ui/HUD.ts
  - src/ui/InventoryUI.ts
  - src/ui/NotebookUI.ts
  - src/game/EventEmitter.ts
  - tests/integration/default-campaign/**
forbidden_scope:
  - content/modules/**
  - src/os/**
  - src/MainMap/**
  - src/runtime/kernel/**
deliverables:
  - Production и lab CampaignManifest
  - Новый composition root
  - Удаление старых state/scene/data owners
verification:
  - npm run content:check
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-54 — Composition root cutover

## Цель

После интеграции четырёх leaf-пакетов один раз переключить production на compiled campaign и RuntimeKernel. Не поддерживать параллельный старый runtime.

## Amendment MM-54P — production persistence bridge

### Причина и решение

Независимая приёмка MM-70 подтвердила gateway и retirement в изоляции, но выявила P1: только этот пакет владеет production boot (`Game`/`main`), а значит gateway никогда не создавался в реальном запуске. Пользователь поручил довести весь пакет до конца; это amendment не меняет portable contract, контент, канон, kernel, capability protocol или snapshot shape.

Открывается только узкий composition seam:

- `main` создаёт `RuntimeBootstrap`, инъецирует browser host только в named gateway factory и передаёт `Game` готовые `RuntimePresentationPort` + gateway. `Game` и `main` не обращаются к storage API напрямую;
- browser-specific factory живёт у единственного persistence owner и получает browser global injection, а не ищет global сам;
- `Game` на boot запускает exact legacy reset, показывает его одноразовое уведомление и через gateway строго читает V2 snapshot до `RuntimeBootstrap.restore`;
- malformed snapshot, fingerprint mismatch и provider mismatch показывают typed reset proposal. Только явное нажатие игрока удаляет current V2 save; оно не делает best-effort restore и не затрагивает username/preferences/Lab namespace;
- после успешных player commits и стартового entry Game просит gateway записать только strict `GameSnapshotV2`. `RuntimeBootstrap` предоставляет gateway закрытый persistence port (`capture`/`decode`/`restore`) и отдельный presentation facade; Game не получает kernel/world/capability sessions, registries, persistence port или catalog providers;
- static gate разрешает browser `localStorage` только внутри named storage boundary и запрещает его во всех остальных production sources. `window.URMAN`, broad clear и fallback-owner по-прежнему запрещены.

### Владение и пределы amendment

Изменения composition-пакета ограничены `src/main.ts`, `src/game/Game.ts`, `src/runtime/bootstrap/**` и regression tests production boot. `MM-70` остаётся владельцем `src/runtime/persistence/**` и его focused tests: он добавляет только browser factory, current-V2 discard after explicit confirmation и строгое сохранение готового snapshot. `MM-55` остаётся владельцем static scanner и добавляет единственное allowlisted storage-boundary правило с regression. Нельзя менять `content/modules/**`, `src/runtime/kernel/**`, CampaignManifest, DSL, capability protocol/version либо добавлять old-save parser/alias.

### Критерии re-freeze

- Первый production boot удаляет ровно четыре retired v1 keys; marker и notice появляются только после успешного удаления, а username/preferences и Lab не меняются.
- Existing V2 save восстанавливается только при exact campaign/capability lock; ошибка остаётся typed reset proposal и не меняет live run.
- Подтверждённый reset несовместимого V2 удаляет только V2 gameplay save и возвращает к запуску новой campaign; отклонение не удаляет ничего.
- Game не получает mutable kernel/world/capability internals и не создаёт второй storage owner.
- Focused browser/integration regression доказывает reset notice, strict restore, explicit mismatch reset, save after commit и запрет direct storage access вне named boundary.

До этих проверок MM-54 и MM-70 не считаются complete, MM-80 не стартует.

### Реализация amendment — pending independent acceptance

- `RuntimeBootstrap.persistence` — закрытый `RuntimePersistencePort`: `capture()` строит strict V2, `decode()` использует private pack и capability descriptors, а только `restore()` заменяет live run после staged restore. `RuntimeBootstrap.presentation` отдельно даёт Game только campaign, presentation sessions, old-PC presentation и typed snapshot/restore.
- `main` создаёт injected browser gateway; `Game` получает только facade и gateway, на первом boot применяет exact v1 reset и one-time notice. Valid V2 открывает campaign entry c `entryAlreadyCommitted: true`, а typed rejection выводит доступный экран явного удаления только current V2 save.
- Каждый стартовый entry и каждый committed input запрашивает autosave. Ошибка storage сообщается через `aria-live` после уже совершённой kernel transaction и не отменяет её.
- Изменения не затрагивают content, campaign/snapshot shape, kernel, capability protocol/version или fallback owner.

## Composition

Создать:

- production `CampaignManifest` для текущей Chapter 1;
- dev manifests для legacy MainMap и отдельных old PC/scene модулей;
- bootstrap, который загружает virtual compiled catalog, регистрирует runtime/capability descriptors, выполняет preflight и создаёт новый run либо применяет `GameSnapshotV2`;
- один navigation/scene host поверх `SceneRegistry`.

## Retirement

По матрице `MM-10`:

- заменить mutable `GameState` на kernel store;
- удалить hardcoded switch и gameplay allowlist из `SceneManager`;
- атомарно переключить `MainMenuScene`/host с legacy `chapter1` scene token на campaign launch `urman.chapter1`, удалить этот token из `Game`/`SceneManager`, удалить все его callers и сам `src/scenes/ChapterScene.ts`; presentation берётся из generic provider, подготовленного `MM-51`, без compatibility alias;
- атомарно удалить `zirat` registration/caller из legacy `SceneManager` и `src/scenes/ZiratMiniGame.ts`; accepted zirat scene/route data из `MM-50` использует независимый generic provider `MM-51`, без переноса minigame code/behavior;
- удалить narrative arrays из `src/data/**` после перевода всех consumers;
- удалить пустые `QuestSystem`, `InventorySystem`, `DialogueUI` либо заменить их imports на реальные новые owners без оставления пустых оболочек;
- отключить старые scene registrations и `villageGreybox` из production;
- не удалять старые storage keys и временный reset notice: это делает `MM-70` после Content Lab/smoke migration.

Если старый API нужен только внутреннему caller, мигрировать caller и удалить API. Compatibility adapter допустим лишь для доказанной внешней границы; в текущем browser prototype такая граница по умолчанию отсутствует.

## Bounded cutover clarification

Инвентаризация перед cutover подтвердила, что перечисленные выше legacy scenes, systems и UI всё ещё компилируются только потому, что они импортируют удаляемые `GameState`, `SceneManager` или narrative arrays. Они не имеют production caller после переключения. `MM-54` удаляет их атомарно вместе с их владельцами; исключать их из `tsconfig` или сохранять type-only compatibility façade запрещено. Это исполнение matrix `MM-10`, а не новый fallback. `MM-70` сохраняет ответственность за storage reset, gateway и legacy validator tooling; его бывшие UI/system paths к началу MM-70 уже могут отсутствовать.

## Проверки

- Production стартует только из `CampaignManifest` и preflight.
- Две campaign variants меняют порядок/состав quests, NPC и dialogues без diff в kernel/general renderers.
- Текущий arrival-to-cliffhanger critical path сохраняется.
- Старые `GameState`/`SceneManager` owners не имеют callers.
- `rg` не находит production imports/callers `ChapterScene` и legacy scene token `chapter1`; `src/scenes/ChapterScene.ts` отсутствует, а menu/host запускает campaign ID `urman.chapter1`.
- `rg` не находит production imports/callers `ZiratMiniGame`; `src/scenes/ZiratMiniGame.ts` отсутствует, а accepted zirat content разрешается через typed scene/route definition и generic provider.
- Production не импортирует dev-only MainMap.

Handoff для `MM-55` содержит resolved module/capability list, campaign fingerprint, список удалённых owners и временно оставшиеся storage/tooling paths с владельцем `MM-70`.

## Handoff

```text
Task: MM-54
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: content/campaigns/**; vite.config.ts; src/main.ts; src/game/Game.ts; src/game/presentation-input.*; src/runtime/bootstrap/**; tests/integration/default-campaign/**; deleted src/data/**, GameState, SceneManager, EventEmitter, legacy scenes/systems/UI.
Behavior / contracts delivered: Production boot uses one explicit compiled `urman.chapter1` CampaignManifest and Vite virtual catalog; `RuntimeBootstrap` creates registries/kernel/world/old-PC host once and is the sole composition owner. The generic browser host renders scene/route/dialogue models, forwards `entryAlreadyCommitted`, maps dialogue choices to `choiceId`, and returns a terminal dialogue to its authored source scene. Scene-to-dialogue handoffs atomically include the start-node conditions/effects, so Rinat is now on the real route rather than test-only initialization. Session occurrence namespaces derive exactly from campaign fingerprint, kind/content ID and immutable state at creation: a changed handoff state gets a distinct scope; an identical restored snapshot is filtered as DuplicateOccurrence. Default RNG seed derives from the selected campaign fingerprint. Development-only old PC receives only CapabilityHost.presentation; MainMap has no production import.
Verification commands: npm run content:check; npm run content:types:check; npm run test:content; npm run test:runtime; npm run test:architecture; node --test tests/integration/default-campaign/default-campaign-cutover.test.mjs tests/integration/default-campaign/presentation-input.test.mjs; npm run build; git diff --check; graphify update .
Verification results: PASS — content preflight 3 modules/3 campaigns; generated types current; content 68/68; runtime 102/102; architecture 2/2; integration 4/4; production build and diff check pass. `graphify update .` re-extracted but host watch rebuild ended `Operation not permitted`; source verification is unaffected.
Resolved production composition: ordered modules `urman.core@1.0.0`, `urman.chapter1@1.0.0`, `urman.oldpc@1.0.0`; capability `urman.oldpc:capability/archive-hub@1.0.0`; campaign fingerprint is resolved and locked per new run/snapshot (value is content-derived, not hardcoded).
Retired owners / remaining callers: Deleted old GameState/SceneManager/EventEmitter, chapter1 token/ChapterScene/ZiratMiniGame callers, narrative src/data arrays, legacy route/scene systems and UI shells; no production MainMap or raw old-PC session import remains. Legacy old-PC/DedOS/MainMap remain separately testable only through explicit dev modules. MM-70 alone still owns pre-MVP storage-key reset and legacy validator tooling; no fallback state owner remains.
Risks or drift: No live hot swap or old-save compatibility was introduced. The game restore currently restores the locked runtime snapshot; persistent-storage UX/reset policy remains MM-70. Graphify host-watch permission error needs host remediation if graph artifacts must be refreshed.
Required next task: MM-55 whole-pack closure, then MM-60/MM-70/MM-80 gates.
```

## Independent acceptance — 2026-07-18

Result: `SPEC PASS` and `QUALITY PASS`; no open P0/P1/P2.

Root re-ran the compiled-content preflight, generated-type drift check, full content/runtime/architecture suites, default-campaign integration and production build. The review also caught and required repairs for the generic dialogue input key, state-derived session occurrence namespace and hardcoded campaign seed before acceptance. Final evidence is 3 modules/3 campaigns, content 68/68, runtime 102/102, architecture 2/2, integration 4/4, production build (55 modules) and clean diff. The static bootstrap/Game/main scan contains no story IDs, raw paths, browser global or retired scene/state owner.

## Pre-cutover legacy chat closure — 2026-07-18

В ходе bounded remediation к MM-52 удалён неимпортируемый DedOS chat: его narrative arrays, provider, renderer, `master_db.json`, launcher и exact app descriptor не имели portable module owner и не входят в production campaign. Это не создаёт fallback или replacement: единственный old-PC narrative owner — compiled `urman.oldpc` archive capability. `MM-54` не должен восстанавливать chat при удалении `src/data/**`; его closure подтверждён targeted regression check.

## MM-54P implementation handoff — 2026-07-18

```text
Task: MM-54P coordinated persistence boot amendment
Status: implementation complete; independent SPEC/QUALITY acceptance pending
Changed paths: src/main.ts; src/game/Game.ts; src/runtime/bootstrap/runtime-bootstrap.{mjs,d.mts}; tests/e2e/content-lab-and-default-campaign.test.mjs; docs modular-migration handoffs/checkpoint/evidence.
Behavior / contracts delivered: `main` is the only composition root: it creates RuntimeBootstrap, injects the browser host into the named gateway factory and passes Game a ready `RuntimePresentationPort` plus gateway. Game has no browser host, pack, bootstrap, registry, capability-host or persistence-port access. Valid storage restores once, then renders the campaign entry with entryAlreadyCommitted. Missing storage renders the menu. A typed rejected V2 snapshot renders an accessible explicit confirmation; only that exact pending proposal authorizes one current-V2 discard, which is consumed on success. Every initial entry and committed action/transition requests a V2 save, and a storage exception is surfaced only after the kernel commit through aria-live.
Verification commands: npm run content:check; npm run test:unit; npm run test:architecture; node scripts/check-runtime-architecture.mjs; node --test tests/integration/persistence-cutover/production-persistence-gateway.test.mjs; npm run test:e2e; npm run build; git diff --check.
Verification results: PASS — content 3 modules/3 campaigns; content 68/68; runtime 102/102; architecture 2/2; static 88 production source files; focused persistence 8/8; real Vite+Chrome E2E 10/10; production build 56 modules; diff check clean. The browser suite also proves a storage write failure is announced after, not rolled back with, the committed transition. The browser run required approved loopback only; the sandbox-only listener EPERM is a host restriction, not a product failure.
Risks or drift: no old-save reader, alias, broad clear, second storage owner, live hot-swap or mutable runtime export. Independent review is still required before MM-80.
Historical next task at this handoff: independent review of the coordinated MM-54P/MM-55P/MM-70 amendment; this review later passed and MM-80 is complete (see `00_ORCHESTRATOR_README.md`).
```

## MM-54P independent acceptance — 2026-07-18

Result: `QUALITY PASS`; no open P0/P1/P2.

The review required and independently verified three ownership repairs: `main` is the only compiled-pack/runtime/browser-gateway factory; `Game` receives only the closed `RuntimePresentationPort` and a ready gateway; current-V2 deletion requires the exact one-time pending reset proposal. The normal browser entrypoint proves one-time v1 retirement notice, strict V2 restore, post-commit autosave and explicit mismatch reset. No portable content, kernel, snapshot shape or capability protocol changed.
