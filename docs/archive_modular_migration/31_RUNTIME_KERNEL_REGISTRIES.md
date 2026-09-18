---
task_id: MM-31
title: Runtime kernel, registries и transactional context
status: complete
kind: implementation
depends_on: [MM-30]
blocks: [MM-40, MM-41]
parallel_group: null
owner_scope:
  - src/runtime/kernel/**
  - src/runtime/registries/**
  - src/runtime/contracts/**
  - tests/runtime/kernel/**
  - tests/architecture/**
forbidden_scope:
  - src/game/Game.ts
  - src/game/GameState.ts
  - src/game/SceneManager.ts
  - src/main.ts
  - src/scenes/**
  - src/os/**
deliverables:
  - Immutable RuntimeContext
  - Единственный transactional state writer
  - Typed registries и ordered event bus
verification:
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-31 — Runtime kernel и registries

## Цель

Создать новое ядро рядом с текущим runtime, не подключая его к production composition root. Cutover делает только `MM-54`.

## Контракты

Создать узкие owner-файлы для:

- `RuntimeKernel`, `RuntimeContext`, `Command`, `CommandResult`, `DomainEvent`;
- `ContentRegistry`, `SceneRegistry`, `ConditionRegistry`, `EffectRegistry`, `CapabilityRegistry`;
- `StateStore` с immutable selectors и reducer transaction;
- ordered event bus с disposable subscriptions;
- typed errors `PreflightError`, `ConflictError`, `UnknownCommandError`, `DuplicateOccurrence`, `OccurrenceConflict`.

Не добавлять эти контракты в общий `src/types/game.types.ts`.

## Транзакция

`dispatch` получает command со стабильным `actionOccurrenceId`. Kernel:

1. вычисляет fingerprint команды и проверяет occurrence ledger: тот же ID + тот же fingerprint возвращает `DuplicateOccurrence` со ссылкой на сохранённый result без commit/events; тот же ID + другой fingerprint возвращает `OccurrenceConflict`;
2. получает immutable snapshot для preconditions;
3. резервирует resource claims;
4. вычисляет полный effect batch;
5. применяет batch одним reducer commit;
6. записывает occurrence ID, command fingerprint и result, затем выпускает ordered events;
7. при ошибке освобождает claims и не меняет state.

Subscriber не может мутировать store. События содержат sequence number и transaction ID.

## Registries

Registry строится только из `CompiledContentPack` и зарегистрированных runtime descriptors. Duplicate registration, missing provider и exact-version mismatch — startup errors. Scene creation идёт через `SceneRegistry`; switch по строковым scene ID и narrative allowlist в generic owner запрещены.

## Architecture tests

Добавить проверки, что `src/runtime/kernel/**` и `src/runtime/registries/**`:

- не импортируют `src/data`, `src/scenes`, `src/os`, DOM или конкретную campaign;
- не содержат `char_`, `clue_`, `quest_`, `/assets/`, `localStorage` и `window.URMAN`;
- не экспортируют mutable store;
- атомарно отклоняют конфликт; same-fingerprint duplicate возвращает saved-result reference без новых events, different-fingerprint duplicate отклоняется как `OccurrenceConflict`.

Пакет не меняет поведение текущей игры; доказательство — исходный smoke/build остаётся зелёным.

## Handoff

```text
Task: MM-31
Status: complete
Worktree / branch: /Users/unterlantas/Documents/GitHub/URMAN @ codex/modular-migration
Changed paths: src/runtime/contracts/**, src/runtime/kernel/**, src/runtime/registries/**,
  tests/runtime/kernel/**, tests/architecture/**
Behavior / contracts delivered: RuntimeKernel is the sole transactional state writer; immutable
  RuntimeContext exposes select, query, dispatch, subscription and capability lookup. `query`
  returns only an immutable state/claim projection, so a capability can prove a committed owner
  release without accessing a mutable store. Ordered event delivery, occurrence ledger, resource
  claims, JSON-only kernel snapshots and strict descriptor registries are available beside legacy
  runtime. Snapshot restore preserves state, claims, ledger and safe event sequence without replaying
  events. Invalid input/output and sequence exhaustion reject before commit.
Verification commands: npm run test:content; npm run content:types:check; npm run test:runtime;
  npm run test:architecture; npm run build; git diff --check; graphify update .
Verification results: PASS — 58 content tests, generated-type drift check, 54 runtime tests,
  2 architecture tests and production build (72 modules). diff check passed. graphify static
  update was attempted; its watch rebuild reported host "Operation not permitted" after extraction.
Reviews: final SPEC PASS, MM-03 re-freeze PASS and final QUALITY PASS after regression fixes for
  JSON persistence, event-sequence overflow, own-data runtime descriptors and verified claim release.
Retired owners / remaining callers: none retired; this is additive and production composition
  remains owned exclusively by MM-54.
Risks or drift: no campaign/scene/legacy import or narrative ID was added. Capability/world services
  are delivered in MM-40; resolvers and V2 persistence remain MM-41/MM-42 work.
Required next task: MM-41, then MM-42 after MM-40/MM-41 handoff.
```
