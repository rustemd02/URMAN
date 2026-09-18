---
task_id: MM-41
title: Asset, audio и text resolvers
status: complete
kind: implementation
depends_on: [MM-31]
blocks: [MM-42]
parallel_group: foundation-services
owner_scope:
  - src/runtime/resolvers/**
  - src/runtime/media/**
  - tests/runtime/resolvers/**
forbidden_scope:
  - src/game/**
  - src/scenes/**
  - src/os/**
  - content/modules/**
  - public/assets/**
deliverables:
  - Единственные runtime-владельцы AssetRef и TextRef
  - Audio/caption/accessibility contract
  - Проверка manifest closure без миграции кампании
verification:
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-41 — Asset, audio и text resolvers

## Цель

Создать generic resolvers поверх `CompiledContentPack`, не переписывая assets или сцены текущей кампании. Campaign manifests принадлежат `MM-50`, old PC — `MM-52`.

## Контракты

- `AssetResolver.resolve(AssetRef, variant?)` возвращает host URL и metadata либо typed missing-asset error.
- `TextResolver.resolve(TextRef, locale, variables)` возвращает безопасный text model, не HTML.
- `AudioResolver` возвращает audio resource, captions, transcript и non-audio cue.
- `AssetRef` и `TextRef` — logical content IDs, не файловые пути.
- Variant selection детерминирован и зависит только от явного state/seed.

Resolver объединяет manifests в порядке resolved campaign, но duplicate logical ID всегда ошибка. Implicit last-write-wins запрещён.

## Язык и доступность

Text record поддерживает русскую основу, татарские варианты и токены vocabulary без превращения татарского в вымышленный код. Audio record требует captions/transcript для сюжетной информации. Значимые признаки не могут зависеть только от звука или цвета.

## Проверки

- missing asset/text/audio диагностируется с module ID и source pointer;
- duplicate ID не перекрывается;
- renderer test получает только resolved model и не знает `/assets/`;
- captions и non-audio cue дают тот же outcome key;
- resolver не импортирует campaign data напрямую.

## Handoff

```text
Task: MM-41
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: src/runtime/resolvers/resolver-errors.mjs; src/runtime/resolvers/resolver-catalog.mjs; src/runtime/resolvers/content-resolvers.mjs; src/runtime/resolvers/content-resolvers.d.mts; src/runtime/media/audio-resolver.mjs; src/runtime/media/audio-resolver.d.mts; tests/runtime/resolvers/content-resolvers.test.mjs
Behavior / contracts delivered: Campaign-ordered resolver catalog rejects duplicate logical IDs; AssetResolver maps logical AssetRef to host-owned URL and deterministic variants; TextResolver returns immutable structured localized/vocabulary models; AudioResolver provides captions, transcript and equivalent non-audio cue; manifest closure validates generic asset/text/accessibility links with module/source diagnostics.
Verification commands: npm run test:content; npm run test:runtime; npm run test:architecture; npm run content:types:check; npm run build; git diff --check
Verification results: PASS — content 58/58; runtime 59/59; architecture 2/2; generated types, production build and diff check pass. Independent SPEC PASS and QUALITY PASS.
Retired owners / remaining callers: No legacy owner retired and no production composition changed. MM-54 must supply the production resolveFileUrl host adapter; the identity default is foundation/test-only.
Risks or drift: No campaign data, raw /assets path, renderer, scene, browser, persistence or legacy import entered the generic runtime boundary.
Required next task: MM-42
```
