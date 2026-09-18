---
task_id: MM-20
title: Portable JSON Schema контракты
status: complete
kind: implementation
depends_on: [MM-10]
blocks: [MM-30]
parallel_group: null
owner_scope:
  - content/schemas/**
  - scripts/validate-content-schemas.mjs
  - tests/content/schema/**
forbidden_scope:
  - src/game/**
  - src/scenes/**
  - src/os/**
  - package.json
deliverables:
  - Канонические JSON Schema для portable content
  - Зафиксированный CompiledContentPack wire contract
  - ContentCompilationResult как единственный diagnostics owner
  - Синтаксическая проверка схем без browser imports
verification:
  - node scripts/validate-content-schemas.mjs
  - node --test 'tests/content/schema/**/*.test.mjs'
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-20 — Portable content schemas

## Цель

Создать engine-neutral схемы, которые одинаково описывают исходный контент для веба и потенциального будущего importer другого движка. JSON Schema — источник истины; TypeScript-типы позже генерирует `MM-30`.

## Файлы

Создать в `content/schemas/`:

- `common.schema.json` — `ContentId`, exact version, file reference, JSON value, localized text;
- `condition-effect.schema.json` — закрытые tagged unions `Condition` и `Effect`;
- `module-manifest.schema.json` и `campaign-manifest.schema.json`;
- `character.schema.json`, `scene.schema.json`, `dialogue.schema.json`, `quest.schema.json`;
- `knowledge.schema.json`, `vocabulary.schema.json`, `document.schema.json`;
- `text.schema.json` — переносимые текстовые записи и локализованные варианты;
- `asset.schema.json`, `capability.schema.json`, `fixture.schema.json`;
- `compiled-content-pack.schema.json` — точный immutable pack contract без diagnostics для `MM-30` и `MM-31`;
- `content-compilation-result.schema.json` — закрытый success/failure result и единственный diagnostics owner.

Каждая схема получает стабильный `$id`, `schemaVersion`, `additionalProperties: false` там, где расширение не предусмотрено, и ссылки только через `$ref`.

## Обязательные типы

- ID строго соответствует `moduleId:kind/localId`.
- Manifest явно перечисляет source files, assets, exact dependencies и capability requirements.
- `CampaignManifest` содержит entrypoint, ordered modules, role bindings и invariants.
- `CampaignManifest` обязательно содержит unique `narrativeOrder`; invariant — closed union `required-reachable` или `reveal-not-before`.
- `QuestDefinition` содержит stages/objectives, composition `all/any/threshold`, optional/failure outcomes, retry/checkpoint/cancel policy и ссылку на capability config.
- `Condition` поддерживает только `all`, `any`, `not`, knowledge/vocabulary/quest/beat/NPC/inventory/pressure/time/location predicates.
- `SceneDefinition.interactions[]` разрешает ровно одну optional typed цель: `targetSceneId` или `targetDialogueId`; их совместное присутствие invalid schema. `targetDialogueId` остаётся normal `ContentId`, а его фактический kind проверяет compiler.
- `Effect` поддерживает только knowledge/vocabulary/NPC/quest/beat changes, item transaction, pressure, route/location unlock и scene/audio request.
- `CapabilityManifest` содержит protocol ID/exact version, config/state/command/event/outcome schema refs, required assets/audio, accessibility contract и lifecycle capabilities.
- `CapabilityManifest.resourceClaims` явно перечисляет static resource IDs и `exclusive/shared` mode.
- Markdown document сохраняет normalized non-empty body в optional `DocumentDefinition.bodyMarkdown`; browser pack не теряет authoring body.
- Optional closed `DocumentDefinition.oldPc` переносит только portable presentation/search metadata старого ПК: `type`, `pcSection`, `canonStatus`, `reliability`, `searchTerms`, `suggestedTerms`. Authoring `requires` нормализуется в общий `accessConditions`, а canonical outcomes — в общий `openEffects`. `sourceKind`, `inWorldSource`, `dangerLevel`, UI layout и parser provenance не переносятся; полный Markdown остаётся единственным `bodyMarkdown` владельца `urman.oldpc`.
- `CompiledContentPack` содержит normalized registries, dependency graph, module fingerprints и resolved campaign metadata, но не diagnostics.
- `ContentCompilationResult` — либо `{ ok: true, pack, diagnostics }`, либо `{ ok: false, diagnostics }`; diagnostic содержит stable code/severity, module ID, source path и JSON pointer.

## Ограничения

Схемы не содержат DOM, CSS, Vite URL, TypeScript module path, class name, renderer name или executable expression. Custom mechanic config валидируется capability schema, но её внутренний алгоритм не переносится в DSL.

## Проверка

`scripts/validate-content-schemas.mjs` должен рекурсивно читать все 17 schema JSON, проверять JSON parse, уникальность `$id`, существование локальных `$ref` и отсутствие запрещённых web-specific keys. Focused tests проверяют корректный минимальный module/campaign и отрицательные случаи ID, неизвестного opcode и лишнего свойства. Тесты запускаются через quoted glob `node --test 'tests/content/schema/**/*.test.mjs'`; category directory не используется как runner contract.

## Completed handoff

Task: MM-20
Status: complete
Worktree / branch: shared workspace, `codex/modular-migration`
Changed paths: `content/schemas/**`, `scripts/validate-content-schemas.mjs`, `tests/content/schema/**`, verification contracts in MM-20/MM-30/MM-60
Behavior / contracts delivered: 17 portable schemas; stable hierarchical `$id`; closed content/condition/effect contracts; exact versions; separate text registry; fatal `ContentCompilationResult`; zero-dependency schema bootstrap validator with schema-position traversal, canonical JSON equality, own-member safety, boolean-schema semantics and actionable union diagnostics.
Verification commands: `node scripts/validate-content-schemas.mjs`; `node --test 'tests/content/schema/**/*.test.mjs'`; `npm run build`; `git diff --check`
Verification results: 17 schemas valid; 23/23 tests pass; production build and diff check pass. Final independent reviews: `SPEC PASS`, `QUALITY PASS`, no P0/P1/P2.
Retired owners / remaining callers: none in this foundation-only packet; runtime and `package.json` were not changed.
Risks or drift: validator intentionally implements the frozen supported JSON Schema subset, not a general-purpose validator; downstream MM-30 must consume these schemas without introducing a second diagnostics owner.
Required next task: MM-30.

### Bounded amendment receipt

Date: 2026-07-17. Добавлены только `narrativeOrder`/closed narrative invariants, capability `resourceClaims` и portable `bodyMarkdown`. Generated types обновлены. Независимые `AMENDMENT SPEC PASS` и `AMENDMENT QUALITY PASS`; 17 schemas, 26/26 tests, drift/diff pass. MM-30 повторно released.

### Bounded old-PC metadata amendment receipt

Date: 2026-07-17. Добавлен закрытый optional `DocumentDefinition.oldPc` с шестью полями, полученными из действующего old-PC authoring. Он не создаёт второй parser/state owner: `accessConditions` и `openEffects` остаются общими canonical полями, а непереносимые provenance/UI metadata явно запрещены. Generated types и compiler pack retention обновлены; MM-52 разблокируется только после independent re-freeze и quality review.
