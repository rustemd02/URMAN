---
task_id: MM-42
title: GameSnapshotV2 и campaign-locked persistence
status: complete
kind: implementation
depends_on: [MM-40, MM-41]
blocks: [MM-45]
parallel_group: null
owner_scope:
  - src/runtime/persistence/**
  - tests/runtime/persistence/**
forbidden_scope:
  - src/systems/SaveSystem.ts
  - src/game/GameState.ts
  - src/os/**
  - src/scenes/**
deliverables:
  - Versioned GameSnapshotV2 codec
  - Campaign fingerprint lock
  - Typed incompatible-save behavior
verification:
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-42 — Snapshot codec

## Цель

Создать новый persistence contract рядом с Save v1, не подключая его к production до `MM-54` и не очищая старые keys до `MM-70`.

## `GameSnapshotV2`

Snapshot хранит только JSON:

- snapshot schema version и saved sequence;
- `CampaignLock`: campaign ID/version, exact modules/capabilities, module fingerprints и общий SHA-256 fingerprint;
- immutable domain state и committed action/trigger occurrences с command fingerprints/results;
- logical clock, owner-scoped RNG streams и scheduler queue;
- quest/objective/checkpoint state;
- item instances, condition, custody и active claims;
- evidence provenance и generated content bindings;
- capability snapshots, keyed по stable `capabilityInstanceId` и namespaced по protocol ID, exact protocol version и state schema version.

## Codec policy

- Decode сначала валидирует JSON shape, затем campaign lock и capability compatibility.
- Fingerprint mismatch возвращает typed `CampaignMismatch`; state не применяется.
- Unknown schema version возвращает typed `UnsupportedSnapshotVersion`.
- Silent field dropping, guessed defaults и best-effort load запрещены.
- Для pre-MVP v1 нет migration adapter; clean reset делает `MM-70`.

## Детерминизм

Restore выполняется до capability `start`. Scheduler восстанавливает stable job IDs и fire ledger, поэтому просроченная задача исполняется максимум один раз. RNG продолжает stream с сохранённой позиции. Runtime instance bindings не генерируются повторно.

## Проверки

- encode/decode roundtrip даёт canonical-equivalent state;
- save/restore в середине capability сохраняет outcome;
- scheduled job после restore fires once;
- fingerprint mismatch и unknown capability state version не мутируют store;
- две параллельные sessions одного protocol восстанавливаются по разным `capabilityInstanceId` без коллизии;
- provenance, custody и occurrence ledger переживают roundtrip.

## Handoff

```text
Task: MM-42
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: src/runtime/persistence/campaign-lock.mjs; src/runtime/persistence/campaign-lock.d.mts; src/runtime/persistence/snapshot-errors.mjs; src/runtime/persistence/snapshot-errors.d.mts; src/runtime/persistence/game-snapshot-v2.mjs; src/runtime/persistence/game-snapshot-v2.d.mts; tests/runtime/persistence/game-snapshot-v2.test.mjs
Behavior / contracts delivered: Strict JSON-only GameSnapshotV2 codec; exact campaign lock and fingerprint comparison; typed snapshot errors; full nested shape validation with no silent field dropping; staged RuntimeKernel/world/CapabilityHost restore; capability compatibility and restore-before-start lifecycle; rollback cleanup of an abandoned stage; persisted scheduler/RNG/occurrence/claim/domain state.
Verification commands: npm run test:content; npm run content:types:check; npm run test:runtime; npm run test:architecture; npm run build; git diff --check
Verification results: PASS — content 58/58; runtime 65/65; architecture 2/2; generated types, production build and diff check pass. Independent QUALITY PASS; initial strict-decode P1 repaired and repeat independent SPEC PASS.
Retired owners / remaining callers: No legacy persistence owner retired. Save v1 and production wiring remain unchanged; MM-54 supplies capability registry and host descriptors to the production restore boundary, and MM-70 retires old saves.
Risks or drift: New codec is additive and has no scene, OS, Save v1 or composition-root import. Every unknown persisted member is rejected rather than dropped.
Required next task: MM-45
```
