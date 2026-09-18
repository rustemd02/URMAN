---
task_id: MM-53
title: Изоляция legacy MainMap
status: complete
kind: migration
depends_on: [MM-45]
blocks: [MM-54]
parallel_group: migration-leaves
owner_scope:
  - src/MainMap/**
  - src/runtime/modules/legacy-mainmap/**
  - tests/runtime/modules/legacy-mainmap/**
forbidden_scope:
  - src/game/**
  - src/scenes/**
  - src/os/**
  - src/main.ts
  - content/campaigns/**
deliverables:
  - Dev-only MainMap runtime module
  - Явные imports и disposable lifecycle
  - Caller/retirement handoff
verification:
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# Handoff

```text
Task: MM-53
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: src/MainMap/**; src/runtime/modules/legacy-mainmap/legacy-mainmap-module.mjs; tests/runtime/modules/legacy-mainmap/**
Behavior / contracts delivered: MainMap receives all bindings/layout/actions through explicit dev config, emits opaque typed requests and owns no production navigation/state. `legacy-mainmap-module.mjs` is an explicit dev-only descriptor with no automatic registration. World/action/listener/RAF/interval/canvas resources dispose deterministically.
Verification commands: focused legacy-mainmap test; npm run test:runtime; npm run test:architecture; npm run build; git diff --check
Verification results: PASS — focused 6/6; runtime 96/96; architecture 2/2; production build and diff check pass. Independent SPEC and QUALITY review passed. Review repairs cover dynamic action-listener disposal, failure-atomic rejection of malformed config before any DOM/listener/timer/RAF/canvas acquisition, and terminal exactly-once scene cleanup when `scene.init()` throws.
Retired owners / remaining callers: Production callers remain only for MM-54 atomic removal: src/game/SceneManager.ts:8,55–56,69 and src/game/Game.ts:52. Dev-only descriptor has no production auto-registration.
Risks or drift: No broader legacy cleanup. Source cleanup/config contract remains MainMap-only; no campaign or composition mutation.
Required next task: MM-54.
```
# MM-53 — MainMap legacy isolation

## Цель

Сохранить greybox MainMap для тестирования, не позволяя ему влиять на production campaign или владеть общими персонажами, квестами и переходами.

## Изменения

- Обернуть MainMap в dev-only runtime module с явным descriptor и lifecycle.
- Все входные residents, actions, labels и scene requests передавать через config/role bindings.
- Удалить внутренние предположения вроде `residentId === 'babay'` и hardcoded `dialogue_babay`.
- Освобождать Three.js resources, listeners, animation frame и DOM при dispose.
- Не регистрировать модуль в production: регистрацию или удаление старого `villageGreybox` выполняет `MM-54`.

## Граница

Пакет не превращается в «remaining legacy cleanup». Пустые systems, global state, scene switch и storage принадлежат `MM-54`/`MM-70`.

## Проверки

- Модуль создаётся только при явной dev registration.
- Два разных role binding набора не требуют изменения generator code.
- Dispose освобождает renderer/resources и прекращает animation loop.
- Production dependency graph не импортирует `src/MainMap/**` после cutover; доказательство передаётся `MM-54`.
