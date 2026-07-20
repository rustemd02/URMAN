---
task_id: MM-52
title: Старый ПК и DedOS как отдельный модуль
status: complete
kind: migration
depends_on: [MM-45]
blocks: [MM-54]
parallel_group: migration-leaves
owner_scope:
  - content/old_pc/**
  - content/modules/urman-oldpc/**
  - src/os/**
  - src/scenes/ComputerScene.ts
  - src/runtime/modules/oldpc/**
  - tests/runtime/modules/oldpc/**
  - tests/content/urman-oldpc/**
forbidden_scope:
  - src/systems/SaveSystem.ts
  - src/game/Game.ts
  - src/game/GameState.ts
  - src/game/SceneManager.ts
  - src/main.ts
  - content/campaigns/**
deliverables:
  - Portable urman.oldpc content module
  - Registry-driven DedOS apps
  - Old PC UI state через RuntimeContext
verification:
  - npm run content:check -- --module urman.oldpc
  - npm run test:content
  - npm run test:runtime
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-52 — Old PC и DedOS migration

## Цель

Сохранить сильную data-driven часть старого ПК, но убрать собственный parser/state/progression owner. ПК становится подключаемым content/runtime module.

`MM-52` — единственный owner `src/scenes/ComputerScene.ts`; широкая scene-маска `MM-51` явно исключает этот файл.

## Content

Перевести существующие Markdown/frontmatter documents в schema `MM-20`, сохранив body, reliability, canon status, unlocks и search terms. Создать `urman.oldpc` manifest с явным списком файлов, logical text/asset refs и dependency на нужные knowledge/character IDs.

`src/os/data/oldPcContent.ts` перестаёт парсить raw modules и получает normalized records из `ContentRegistry`. Старый regex/parser удаляется после зелёного parity test; permanent fallback запрещён.

## Runtime

- DedOS apps регистрируются через app registry descriptors, без switch/implicit globals.
- Old PC hub читает documents через registry и отправляет open/save/search commands.
- Progression, knowledge и evidence живут только в kernel state.
- Локальный UI state допустим только как namespaced capability snapshot; прямой `localStorage` удаляется в `MM-70` после общего cutover.
- `ComputerScene` становится adapter к old PC capability и не меняет `GameState` напрямую.

## Проверки

- Все 10 текущих документов компилируются и сохраняют search/unlock поведение.
- Opening/saving document выдаёт shared knowledge/evidence через commands.
- Missing dependency или orphan unlock падает в content preflight.
- Snapshot восстанавливает активный документ, search query и saved clue без второго progression owner.
- Runtime не использует `window.URMAN`.

Handoff указывает старые storage/parser paths, которые должен удалить `MM-70`, и registration paths для `MM-54`.

## Handoff

```text
Task: MM-52
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: content/old_pc/**; content/modules/urman-oldpc/**; src/os/**; src/scenes/ComputerScene.ts; src/runtime/modules/oldpc/**; tests/content/urman-oldpc/**; tests/runtime/modules/oldpc/**
Behavior / contracts delivered: Ten full Markdown documents moved to one `urman.oldpc` owner with closed metadata, logical assets/texts and exact capability schemas; ContentRegistry-only search catalog; descriptor-driven DedOS; injected ComputerScene adapter; RuntimeContext commands; open atomically carries canonical knowledge/presentation refs; save is non-gating bookmark snapshot.
Verification commands: npm run content:check -- --module urman.oldpc; npm run test:content; npm run test:runtime; npm run test:architecture; npm run content:types:check; npm run build; git diff --check
Verification results: PASS — content 66/66; runtime 90/90; architecture 2/2; module preflight, generated types, build and diff check pass. Independent QUALITY PASS; final SPEC PASS after canonical ID repair.
Retired owners / remaining callers: Removed raw oldPcContent parser and old authored bodies. MM-54 must create/start OldPcCapabilitySession through CapabilityHost and inject it into ComputerScene/DedOS. MM-70 owns SaveSystem old-PC keys and legacy validator scripts validate-old-pc-content/validate-clue-graph.
Risks or drift: Missing injection UI is development guard only, never a production fallback. Canonical IDs are exactly urman.oldpc:app/archive_search and urman.oldpc:asset/ui_old_pc_desktop_base; aliases prohibited.
Required next task: MM-53
```

## Capability-presentation amendment receipt — 2026-07-18

`createOldPcCapabilityDescriptor` теперь явно проксирует `render` и `subscribe` session через generic optional host port. MM-54 после exact create/start получает только `CapabilityHost.presentation(capabilityInstanceId)` и передаёт adapter-у этот port; ни `OldPcCapabilitySession`, ни `RuntimeContext`, ни raw kernel state не становятся UI dependency. Render/update models cloned/frozen host-ом, input идёт через host `handle`, subscription disposable и terminal после stop/dispose.

Evidence: dedicated old-PC host-port regression подтверждает immutable model, section update и cleanup/terminal behavior; общий targeted capability/old-PC suite — `17/17`. Это handoff amendment, не новый старый-PC state/progression owner. Production wiring остаётся исключительно MM-54; независимый MM-03 re-freeze ещё обязателен до cutover.

## Legacy chat retirement receipt — 2026-07-18

Обнаруженный параллельный DedOS chat не имел portable content owner и содержал неканоничные, не прошедшие сценарную и языковую проверку narrative data. Удалены `src/data/chat_data.ts`, `src/os/data/ChatProvider.ts`, `src/os/apps/chat.ts` и неимпортируемый `src/os/data/chats/master_db.json`; также удалены точный launcher `urman.oldpc:launcher/chat`, descriptor `urman.oldpc:app/chat` и их OS imports.

Это delete-first retirement внутреннего кода, не перенос контента и не изменение Chapter 1: архивный app `urman.oldpc:app/archive_search` остаётся единственным narrative old-PC owner. Compatibility alias или пустой chat placeholder не оставлены. Regression test доказывает отсутствие файлов, launcher/descriptor и source references. MM-54 поэтому удаляет оставшийся `src/data/**` closure без chat consumer.

## Legacy web-corpus retirement amendment — 2026-07-18

Финальный MM-80 review обнаружил отдельный неучтённый legacy owner: `src/os/data/web_content.json` содержал старые HTML/JSON-тексты Татарвики, стихов и форума. Bounded scan не выявил ни импорта, ни Vite-glob, ни runtime lookup; файл не является пользовательскими данными, snapshot или внешним контрактом. Поэтому он удалён по `delete-first`, а не переведён в portable content без канонической проверки.

`urman.oldpc` compiled module и `urman.oldpc:app/archive_search` остаются единственными current narrative owners для production Old PC. Fallback, alias и placeholder не добавлялись. MM-55 закрепляет absence exact path через `RetiredOwnerPresent`; его временная-tree regression возвращает тот же файл и ожидает точный diagnostic.

Это amendment receipt к уже завершённой MM-52, не изменение статуса и не каноническое решение.
