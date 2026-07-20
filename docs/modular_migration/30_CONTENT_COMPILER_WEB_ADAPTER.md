---
task_id: MM-30
title: Единый content compiler и Vite adapter
status: complete
kind: implementation
depends_on: [MM-20]
blocks: [MM-31]
parallel_group: null
owner_scope:
  - scripts/content/**
  - src/content/generated/**
  - src/content/web/**
  - tests/content/compiler/**
  - package.json
  - vite.config.ts
forbidden_scope:
  - src/os/data/oldPcContent.ts
  - src/game/**
  - src/scenes/**
  - content/modules/**
deliverables:
  - Детерминированный CompiledContentPack
  - ContentCompilationResult как единственный diagnostics owner
  - Общий CLI validator/compiler
  - Детерминированная генерация content-types.ts и drift check
  - Базовые Node test scripts до старта MM-31
  - Vite virtual content catalog
verification:
  - npm run content:check
  - npm run content:types:check
  - npm run test:content
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-30 — Content compiler и web adapter

## Цель

Сделать один semantic compiler для CLI, тестов и Vite. Старые regex-парсеры пока не удаляются и не изменяются: их перенос принадлежит `MM-52` и cutover-пакетам.

## Реализация

Создать:

- `scripts/content/compile-content.mjs` — библиотечный entrypoint без process exit, возвращающий `ContentCompilationResult`;
- `scripts/content/content-cli.mjs` — `check` и `compile` команды, которые отображают diagnostics из result;
- `scripts/content/diagnostics.mjs` — constructors/codes для diagnostics; массивом diagnostics владеет только `ContentCompilationResult`;
- `scripts/content/generate-content-types.mjs` — deterministic generator с write/check режимами без сторонней библиотеки;
- `src/content/generated/content-types.ts` — генерируемые из JSON Schema типы, не редактируемые вручную;
- `src/content/web/viteContentPlugin.ts` — virtual module `virtual:urman-content-catalog`;
- compiler tests и valid/invalid fixtures.

Manifest обязан явно перечислять files; compiler не использует glob как production-authority. Сборка нормализует Markdown/frontmatter и JSON, сортирует registries детерминированно, разрешает cross-file/cross-module refs и вычисляет fingerprints из canonical JSON.

### Вход compiler и workspace check

`compileContent({ campaignPath, moduleManifestPaths })` всегда получает явный campaign manifest и точный список module manifest paths. Без campaign selection он возвращает fatal diagnostic `MissingCampaignSelection` и не создаёт пустой `CompiledContentPack`. Vite plugin принимает те же явные paths/options; `MM-30` реализует и тестирует adapter, но production wiring принадлежит только `MM-54`.

Без непустого explicit `moduleManifestPaths` compiler возвращает fatal `MissingModuleSelection`. ContentId references извлекаются schema-derived traversal-ом, а не неполным списком имён полей; root definition `id`, invariant `id`, provider `protocolId` и claim `resourceId` имеют отдельную declaration/key semantics.

Bare `content:check` — прозрачный dev-only workspace audit, а не production loader: он проверяет schemas/types и может обнаруживать только стандартные authoring manifests `content/modules/**/module.json` и `content/campaigns/**/campaign.json`. Нулевой workspace разрешён до content migration и печатает точный результат `checked 0 modules, 0 campaigns`; pack при этом не создаётся. `--module <id-or-path>` использует discovery только для выбора authoring manifest; неизвестный ID падает с точной diagnostic. После выбора module/campaign их `sourceFiles` остаются единственным authority — compiler не glob-ит authored payload. Production runtime и Vite catalog никогда не используют discovery.

При success compiler возвращает `{ ok: true, pack, diagnostics }`; при любой integrity error — `{ ok: false, diagnostics }` без pack. `CompiledContentPack` не дублирует diagnostics. CLI/tests читают result целиком, а Vite adapter публикует только `result.pack` после `ok === true`.

## Generated types

`content:types` читает schemas в стабильном lexical order, нормализует JSON и генерирует byte-for-byte deterministic `src/content/generated/content-types.ts` с LF и фиксированным header. `content:types:check` генерирует результат в памяти, сравнивает с tracked file, ничего не пишет и завершает процесс ненулевым code при drift. Schema/compiler changes не принимаются без зелёного drift check.

Type generator сохраняет sibling object-shape при `allOf`: ограничивающая ветка (например `not` для mutually exclusive interaction target) не может превратить generated object type в `unknown`.

## Semantic preflight

Останавливать компиляцию при:

- duplicate ID или module ID/version;
- undeclared cross-module reference;
- missing file, asset, role binding, entrypoint или capability;
- unknown condition/effect opcode;
- dependency cycle и unreachable required entry;
- несовместимой exact version;
- нарушении narrative invariant, включая reveal timing;
- конфликте эксклюзивного resource claim, известном на этапе композиции.
- `UnknownDialogueTarget` с точным JSON pointer, если `targetDialogueId` не разрешается в dialogue definition; `ConflictingInteractionTarget`, если interaction указывает одновременно scene и dialogue target.

Никаких warning-and-continue для ошибок целостности.

## Browser path

Vite plugin вызывает тот же compiler во время dev/build и отдаёт immutable `CompiledContentPack` через virtual module. Browser не fetch-ит произвольные Markdown-файлы и не знает расположение authoring tree. Production bundle не содержит filesystem parser.

## Package scripts

Добавить стабильные команды:

```text
content:check     node scripts/content/content-cli.mjs check
content:compile   node scripts/content/content-cli.mjs compile
content:types     node scripts/content/generate-content-types.mjs
content:types:check node scripts/content/generate-content-types.mjs --check
test:content      node --test 'tests/content/**/*.test.mjs'
test:runtime      node --test 'tests/runtime/**/*.test.mjs'
test:architecture node --test 'tests/architecture/**/*.test.mjs'
test:unit         npm run test:content && npm run test:runtime
```

`MM-30` — единственный owner этих `package.json` scripts и создаёт их до передачи `MM-31`. Используется только встроенный `node --test` с quoted glob по `*.test.mjs`; category directory не является runner contract. Новые test dependencies не устанавливаются. `test:unit` — umbrella для content/runtime unit tests, а `test:architecture` остаётся отдельным gate. Существующий `validate:content` временно вызывает старые проверки и новый `content:check`; окончательное удаление дубликатов принадлежит `MM-70`.

## Completed handoff

Task: MM-30
Status: complete
Worktree / branch: shared workspace, `codex/modular-migration`
Changed paths: `scripts/content/**`, `src/content/generated/**`, `src/content/web/**`, `tests/content/compiler/**`, `package.json`, `vite.config.ts`; bounded MM-20 schema amendment paths recorded separately.
Behavior / contracts delivered: explicit semantic compiler; fatal `ContentCompilationResult`; deterministic immutable registries/graph/SHA-256 fingerprints; schema-derived reference traversal; typed narrative/capability/resource preflight; transparent dev workspace audit; deterministic generated types; explicit Vite virtual catalog adapter.
Verification commands: `npm run content:check`; `npm run content:types:check`; `npm run test:content`; `npm run validate:content`; `npm run build`; `git diff --check`.
Verification results: workspace audit `checked 0 modules, 0 campaigns`; generated types current; 57/57 content tests; legacy+new validation and production build pass. Final reviews: `SPEC PASS`, `QUALITY PASS`, no P0/P1/P2.
Retired owners / remaining callers: no legacy owner retired in MM-30; old validators remain intentionally chained until MM-70. Vite adapter is not production-wired until MM-54.
Risks or drift: Markdown frontmatter parser intentionally supports flat keys with JSON arrays/objects; production content must follow this portable subset. Dev discovery is audit/ID-selection only and never a production loader.
Required next task: MM-31.
