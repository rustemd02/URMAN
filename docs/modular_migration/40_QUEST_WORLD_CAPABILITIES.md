---
task_id: MM-40
title: Quest lifecycle и минимальные world services
status: complete
kind: implementation
depends_on: [MM-31]
blocks: [MM-42]
parallel_group: foundation-services
owner_scope:
  - src/runtime/quests/**
  - src/runtime/capabilities/**
  - src/runtime/world/**
  - tests/runtime/quests/**
  - tests/runtime/capabilities/**
  - tests/runtime/world/**
forbidden_scope:
  - src/game/**
  - src/scenes/**
  - src/os/**
  - src/runtime/resolvers/**
deliverables:
  - Data-driven quest reducer
  - Capability lifecycle host
  - Deterministic minimal world primitives
verification:
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-40 — Quest, capability и world services

## Цель

Реализовать только системные примитивы, нужные для стресс-матрицы. Не строить полный world simulation, AI, crafting engine или конкретные mechanics providers.

## Quest runtime

Quest reducer читает `QuestDefinition` и хранит instance state отдельно от definition. Поддержать:

- stages и параллельные objectives;
- completion `all`, `any`, `threshold`;
- optional/failure outcomes;
- retry, checkpoint, cancel и deterministic restart;
- stable quest/objective/trigger occurrence IDs;
- идемпотентные effect batches через kernel transaction.

Quest runtime не содержит ID кампании или веток по objective kind. Он запускает capability по protocol ID и валидированному config.

## Capability host

Реализовать descriptor registration, config validation, session lifecycle `create/restore/start/handle/snapshot/stop/dispose`, namespaced JSON state и cleanup registry. Capability получает ограниченный `RuntimeContext`, owner-scoped clock/RNG/scheduler и resource claims.

Optional web-presentation seam принадлежит только `CapabilityHost`: `presentation(capabilityInstanceId)` доступен исключительно started exact session с собственными `render` и `subscribe`. Он выдаёт immutable object с `render`, typed `handle` через host и disposable `subscribe`. На boundary вход и render/update output нормализуются как frozen JSON; raw session, `RuntimeContext`, kernel state и writer наружу не передаются. Host добавляет presentation subscriptions в owner cleanup, поэтому после `stop/dispose` они disposed, а retained port не может render/handle/subscribe. Session без explicit port остаётся обычной capability и не получает неявный UI fallback.

## World primitives

- logical clock с serializable tick;
- owner-scoped seeded RNG streams с persisted position;
- scheduler со stable job ID и exactly-once fire после restore;
- `ItemInstance` с единственным custody owner и condition metadata;
- atomic claim/consume/transfer transaction;
- `EvidenceClaim` с source/provenance chain;
- resource claims для NPC, двери, локации, ambience и общего объекта.

## Проверки

- same seed + same action log = identical state и event log;
- save boundary не использует wall clock;
- два quest claim/consume один item: один commit, второй typed conflict, zero partial effects;
- cancel/dispose освобождает все claims, timers, listeners и jobs;
- неизвестный capability и incompatible version падают до gameplay;
- sample accessibility adapter возвращает тот же outcome ID.

## Handoff

```text
Task: MM-40
Status: complete
Worktree / branch: /Users/unterlantas/Documents/GitHub/URMAN @ codex/modular-migration
Changed paths: src/runtime/quests/**, src/runtime/capabilities/**, src/runtime/world/**,
  tests/runtime/quests/**, tests/runtime/capabilities/**, tests/runtime/world/**
Behavior / contracts delivered: generic quest lifecycle returns kernel transaction plans for
  parallel objectives, all/any/threshold composition, optional/failure outcomes, checkpoints,
  retry/cancel and stable occurrence IDs. CapabilityHost preflights exact descriptors/config,
  runs JSON session lifecycle and terminates only after the kernel confirms owner-claim release.
  It cleans every child despite callback failures and rejects lifecycle re-entry/orphan claim plans.
  World provides logical clock, owner RNG, lease/ack scheduler, claim-first custody and provenance.
Verification commands: npm run test:content; npm run content:types:check; npm run test:runtime;
  npm run test:architecture; npm run build; git diff --check; graphify update .
Verification results: PASS — 58 content tests, generated-type drift check, 54 runtime tests,
  2 architecture tests, production build (72 modules) and diff check. graphify update was attempted;
  host watch rebuild reported "Operation not permitted" after static extraction.
Reviews: final SPEC PASS, MM-03 re-freeze PASS and final QUALITY PASS. Review regressions cover
  cleanup ordering/proof, child cleanup, provider callback re-entry, active-only claims, scheduler
  lease release/ack, retry provider re-request and competing custody conflict.
Retired owners / remaining callers: none retired; this is additive. Integrators must dispatch a
  returned quest cleanup plan, then call CapabilityHost finalization with the committed result.
Risks or drift: no campaign IDs, mechanic-specific branches, wall clock, legacy import or production
  composition wiring was added. MM-42 owns durable V2 snapshot codec integration.
Required next task: MM-41, then MM-42 after both foundation tasks are accepted.
```
