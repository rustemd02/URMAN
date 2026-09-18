---
task_id: MM-45
title: Foundation canary до миграции кампании
status: complete
kind: integration-gate
depends_on: [MM-42]
blocks: [MM-50, MM-51, MM-52, MM-53]
parallel_group: null
owner_scope:
  - tests/fixtures/content/canary/**
  - tests/integration/foundation-canary.test.ts
  - tests/helpers/mock-capabilities/**
forbidden_scope:
  - content/modules/urman/**
  - src/game/**
  - src/scenes/**
  - src/os/**
deliverables:
  - Synthetic pack через полный foundation pipeline
  - Доказательство swap и deterministic restore
  - Negative preflight suite
verification:
  - npm run content:check
  - npm run test:content
  - npm run test:runtime
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-45 — Foundation canary

## Цель

Проверить foundation на вымышленном нейтральном контенте до переноса УРМАН. Если canary не проходит, пакеты `MM-50`–`MM-53` не стартуют.

## Synthetic pack

Создать два маленьких module manifest и две campaign variants. Они должны содержать:

- двух взаимозаменяемых персонажей через один role slot;
- два независимых квеста, порядок которых меняется между campaigns;
- диалог, сцену, logical asset/text refs и один mock capability;
- один scheduled effect, item transfer и evidence provenance;
- deterministic seed и snapshot в середине objective.

## Проверяемый путь

```text
source JSON/Markdown → schemas → semantic compiler → CompiledContentPack
→ registries → quest/capability transaction → resolvers → GameSnapshotV2
→ restore → dispose
```

## Acceptance

- Обе campaigns компилируются без изменения kernel или generic renderer.
- Role swap меняет NPC/text/assets только через bindings.
- Перестановка независимых квестов не ломает references.
- Same seed/action log до и после snapshot даёт одинаковый state/event log.
- Duplicate ID, missing provider, missing asset, unknown opcode и unreachable entry дают deterministic diagnostics с module/path/pointer.
- Dispose оставляет zero active timers, listeners, jobs и claims.
- В fixture и mock provider нет ID текущей кампании УРМАН.

## Handoff

```text
Task: MM-45
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: tests/fixtures/content/canary/**; tests/helpers/mock-capabilities/canary-signal-provider.mjs; tests/integration/foundation-canary.test.mjs
Behavior / contracts delivered: Neutral two-module/two-campaign synthetic pack; campaign role bindings swap NPC text/assets; independent quests reorder; compiler/registries/resolvers/quest/capability path; scheduled mock provider, custody and evidence provenance; deterministic V2 restore and lifecycle cleanup; deterministic negative preflight diagnostics.
Verification commands: npm run content:check; npm run test:content; npm run test:runtime; npm run test:architecture; node --test tests/integration/foundation-canary.test.mjs; npm run build; git diff --check
Verification results: PASS — content check 0/0 explicit workspace audit; content 58/58; runtime 65/65; architecture 2/2; canary 5/5; production build and diff check pass. Independent SPEC PASS and QUALITY PASS.
Retired owners / remaining callers: No owner retired. Canary is test-only and leaves production composition untouched.
Risks or drift: Fixture/mock provider remain free of URMAN identifiers; no campaign/legacy/scene/OS/asset-tree/composition mutation. The integration test's compiler diagnostic identity is asserted separately and is not fixture content.
Required next task: MM-50; then MM-51, MM-52 and MM-53 in isolated ownership order before MM-54.
```
