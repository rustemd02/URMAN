---
task_id: MM-01
title: Архитектурный контракт модульной игры
status: frozen
kind: design-contract
depends_on: [MM-00]
blocks: [MM-02]
parallel_group: null
owner_scope:
  - docs/modular_migration/01_ARCHITECTURE_CONTRACT.md
forbidden_scope:
  - src/**
  - content/**
  - public/**
deliverables:
  - Проверяемая граница content, kernel и capability
  - Публичные контракты для последующих пакетов
verification:
  - git diff --check
  - Review every MM-02 fixture against the contract before MM-03 freeze
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-01 — Архитектурный контракт

## Цель

Зафиксировать минимальную архитектуру, позволяющую менять сценарные арки, квесты, персонажей, диалоги и ассеты без переписывания ядра. Это не универсальный движок и не система загружаемых модов.

## Три владельца

### Portable content

`content/**` хранит JSON и Markdown с JSON Schema. Данные не импортируют TypeScript, DOM, Vite и веб-пути. Manifest каждого модуля явно перечисляет файлы и зависимости; runtime-glob как источник истины запрещён.

Основные контракты:

- `ContentModuleManifest` — ID, точная версия, schema version, явные файлы, assets, dependencies, provides/requires.
- `CampaignManifest` — точный список и порядок модулей, entrypoint, role bindings, capability requirements, `narrativeOrder` и закрытые narrative invariants.
- `CompiledContentPack` — нормализованный неизменяемый pack с registries, resolved metadata и fingerprints; diagnostics в него не входят.
- `QuestDefinition`, `DialogueDefinition`, `SceneDefinition`, `CharacterDefinition`, `KnowledgeDefinition`, `VocabularyDefinition`, `DocumentDefinition`, `AssetDefinition`, `TextDefinition`.
- закрытые AST `Condition` и `Effect`; произвольные выражения, `eval` и код внутри контента запрещены.

Portable-граница — данные, схемы, ID и capability protocol. Веб-реализации renderer/provider остаются TypeScript-специфичными. Unity importer и нейтральный бинарный bundle в v1 не создаются.

Scene interaction может указывать ровно один typed target: `targetSceneId` или `targetDialogueId`. Для dialogue target generic scene provider получает start node из `ContentRegistry` и передаёт его `conditions`/`effects` как `targetEntry` в ту же атомарную scene transaction; после commit host получает только `{ fromSceneId, targetDialogueId, entryAlreadyCommitted: true }`. Generic dialogue session с этим флагом рендерит start node, но не повторяет его entry command. Это protocol seam без campaign-ID ветки.

Компилятор возвращает закрытый `ContentCompilationResult`, который единолично владеет diagnostics: либо `{ ok: true, pack: CompiledContentPack, diagnostics }`, либо `{ ok: false, diagnostics }`. `CompiledContentPack` не содержит второго diagnostics field. Diagnostic с severity `error` запрещает создание pack; warning не может скрывать ошибку целостности. Каждый diagnostic имеет стабильный code, module ID, source path и JSON pointer.

Browser discovery имеет один путь: Vite adapter вызывает тот же semantic compiler и публикует готовый pack через `virtual:urman-content-catalog`. Runtime не сканирует authoring tree, не fetch-ит Markdown/JSON и не содержит filesystem parser. Отдельного глобального loader/service locator нет: bootstrap получает `CompiledContentPack` из host adapter, затем строит `ContentRegistry`, `SceneRegistry`, `ConditionRegistry`, `EffectRegistry` и `CapabilityRegistry`. Нормативный registry seam — `ContentRegistry.fromPack(pack)`; остальные registry получают только pack и явно зарегистрированные host descriptors.

### Runtime kernel

`RuntimeKernel` — единственный writer состояния. `RuntimeContext` не отдаёт mutable `GameState`; он предоставляет:

```text
select(query) -> immutable result
query(readModelSelector) -> immutable { state, claims }
dispatch(command with actionOccurrenceId) -> accepted | typed rejection
subscribe(eventType, ordered handler) -> disposable subscription
capabilities.require(id, exactVersion) -> provider handle
```

`query` — отдельный read-only seam для системной проверки kernel-owned claims. Он передаёт selector только immutable snapshot `{ state, claims }`; occurrence ledger, mutable store, capability sessions и writer API не выдаются. `select` остаётся коротким state-only selector API. Ни query, ни select не дают capability права обходить transaction/reducer.

Одно действие игрока проходит одну serial transaction: preflight → проверка условий и resource claims → применение всех эффектов → ordered events. Ошибка даёт zero partial effects. Ledger хранит occurrence ID, fingerprint команды и результат. Повтор того же `actionOccurrenceId` с тем же fingerprint возвращает typed `DuplicateOccurrence` со ссылкой на сохранённый результат без нового commit/events; повтор с другим fingerprint даёт typed `OccurrenceConflict`. Оба пути идемпотентны, ledger переживает snapshot/restore.

Ядро включает только сервисы, доказанные стресс-матрицей: logical game clock, owner-scoped seeded RNG, scheduler со стабильными job ID и fire ledger, атомарный item/custody store, evidence provenance и resource claims. Wall clock и `Date.now()` не управляют authored progression. Claims имеют stable resource ID, owner instance ID, mode и lifecycle scope; конфликт отклоняется до commit. Scheduler job и RNG stream принадлежат owner instance, сохраняют позицию/ledger и освобождаются только через kernel transaction.

### Quest orchestration

`QuestDefinition` отделён от `QuestInstanceState`. Quest lifecycle поддерживает stages, параллельные objectives, `all/any/threshold`, optional/failure outcomes, retry, checkpoint и cancel. Quest/order в campaign не является identity: перестановка независимых квестов не меняет ID, references или RNG streams.

Каждая objective с механикой указывает exact capability protocol, валидируемый config и outcome schema. Checkpoint сохраняет quest state, generated bindings и snapshot её capability instances. Cancel/retry сначала выполняет одну owner-scoped cleanup transaction, затем `stop/dispose`; временные claims/jobs/effects не остаются. Постоянный authored effect применяется только через kernel outcome transaction. Capability не может менять store напрямую.

Role slots используются в dialogue/scene/quest definitions вместо concrete character ID там, где разрешён swap. Campaign role binding разрешается при компиляции, а resolved/generated binding сохраняется в run и `GameSnapshotV2`. Procedural выбор разрешён только из authored pool по явным constraints и seeded stream; он не создаёт новый канон и хранит source/provenance.

`CampaignManifest.narrativeOrder` — обязательный уникальный authorial порядок typed IDs. Он не заменяет runtime event log, но делает compile-time проверяемыми два закрытых invariant: `required-reachable(targetId)` и `reveal-not-before(subjectId, afterId)`. Для reveal-invariant оба ID обязаны присутствовать в order, а `subjectId` стоит строго после `afterId`. Runtime event-order proof принадлежит canary и campaign tests, не compiler schema.

### Capability providers

Уникальная механика реализует `CapabilityManifest`, schema конфигурации и host-specific provider. Жизненный цикл:

```text
register descriptor → validate campaign/config → create session
→ optional restore snapshot → start → handle commands/events → snapshot → stop → dispose
```

Каждая session получает стабильный `capabilityInstanceId`, независимый от protocol ID и порядка запуска. Состояние capability — JSON, keyed по `capabilityInstanceId` и namespaced по protocol ID, exact protocol version и state schema version. Это позволяет параллельно запускать несколько objectives одного protocol без коллизий. Restore вызывается не более одного раза до `start`; `stop` и `dispose` идемпотентны.

Все timers, listeners, subscriptions, claims и scheduled jobs имеют owner `capabilityInstanceId` и очищаются при `stop/dispose/reset`. Provider получает только `RuntimeContext` и отправляет commands; прямой write в kernel state запрещён. Missing/incompatible capability или invalid config останавливает preflight до начала игры. Доступная альтернатива обязана выдавать тот же typed outcome ID и те же authored state effects; различаться могут только input/presentation и внутреннее состояние provider.

### Optional capability presentation port

Web-specific presentation не расширяет `RuntimeContext` и не открывает provider session. Только started session, созданная через exact descriptor и явно имеющая собственные callable `render` и `subscribe`, может быть получена через `CapabilityHost.presentation(capabilityInstanceId)`. Возвращаемый immutable port содержит ровно `render()`, `handle(JsonValue)` и `subscribe(listener)`:

- `render` возвращает только cloned/frozen JSON model; raw provider state, session, runtime context, kernel state и writer API отсутствуют;
- `handle` проходит через обычный host input boundary и получает тот же typed JSON outcome, что `CapabilityHost.handle`;
- `subscribe` передаёт listener только cloned/frozen JSON models и возвращает disposable subscription; ошибка listener не меняет provider lifecycle;
- создание port или subscription до `start`, после `stop/dispose`, либо у session без обеих explicit methods завершается typed boundary error; уже выданные port/subscription terminal после `stop/dispose`, а host очищает subscription вместе с owner cleanup.

Этот port optional и content-neutral: он не добавляет renderer в kernel, нового state owner, capability protocol/version, snapshot member или fallback. Old-PC — первый web-specific consumer; другие capability могут не реализовывать presentation совсем.

## Идентификаторы и зависимости

- Формат ID: `moduleId:kind/localId`, например `urman.chapter1:character/rinat`.
- ID неизменяем после публикации модуля и не зависит от его позиции в кампании.
- Модуль и capability требуют точную версию. Диапазоны версий и semver solver не входят в v1.
- Cross-module reference разрешён только при явной dependency.
- Duplicate ID, implicit override и legacy alias запрещены.
- Patch/override-механизм не входит в v1: вариант контента оформляется отдельным модулем или campaign manifest.

Static host code подключает новую custom mechanic только новым capability package, schema/catalog registration и content config. Это не live/remote loading: descriptor catalog собирается host build и валидируется против exact requirements до создания run. Kernel, общий renderer и providers других mechanics не меняются.

Capability manifest явно перечисляет static `resourceClaims` как `{ resourceId, mode: exclusive | shared }`. Compiler рассматривает claims только выбранных requirements/providers и останавливает композицию, если один resource запрошен несколькими selected providers и хотя бы один claim exclusive. Resource ID — authored key; отдельная `ResourceDefinition` в v1 не создаётся.

## Кампания и сохранение

При старте нового run компилятор разрешает кампанию и создаёт `CampaignLock`: ID кампании, точные версии модулей и capability, fingerprints модулей и общий SHA-256 `campaignFingerprint`. Lock живёт весь run.

Content Lab может менять кампанию только через новый изолированный run. Hot-swap внутри активного сохранения запрещён. При несовпадении fingerprint загрузка возвращает typed incompatibility и предлагает явный reset; silent best-effort запрещён.

`GameSnapshotV2` хранит campaign lock, logical clock, RNG streams с позициями, scheduler queue и fire ledger, action/trigger occurrences с command fingerprints/results, quest/objective/checkpoint state, item instances и custody, active claims/provenance, generated bindings и capability snapshots, keyed по `capabilityInstanceId` и namespaced по protocol/version/state schema.

Load сначала валидирует JSON/schema version, затем `CampaignLock`, pack/capability fingerprints и compatibility всех capability snapshots. Любая ошибка возвращает typed load error до mutation. Restore идёт в staging state; только полностью успешный restore атомарно становится active run.

## Assets, text и язык

Контент ссылается на `AssetRef` и `TextRef`, а не на `/assets/...` или пользовательский текст внутри renderer. `AssetResolver` и `TextResolver` — единственные владельцы URL, локализации, татарских вариантов, captions и доступных альтернатив.

Markdown document source компилируется вместе с нормализованным non-empty `bodyMarkdown` в portable `DocumentDefinition`; browser никогда не fetch-ит authoring Markdown. JSON authoring может явно содержать или не содержать `bodyMarkdown`.

## Error boundary

- Compile/preflight: `DuplicateId`, `UnresolvedReference`, `UnknownOpcode`, `MissingCapability`, `IncompatibleCapabilityVersion`, `InvalidCapabilityConfig`, `MissingAsset`, `DependencyCycle`, `ResourceClaimConflict`.
- Dispatch: typed condition/command rejection, `ResourceConflict`, `DuplicateOccurrence`, `OccurrenceConflict`; все дают zero partial effects.
- Load: `UnsupportedSnapshotVersion`, `CampaignMismatch`, `CapabilitySnapshotIncompatible`, `SnapshotIntegrityError`; ни одна ошибка не применяет staged state.

Diagnostic/error code — стабильная машинная identity; русское сообщение может меняться. Silent fallback, warning-and-continue для integrity errors и автоматический alias запрещены.

## Non-goals v1

- live hot-swap и remote/downloadable mods;
- полный ECS, generic scripting engine и Turing-complete DSL;
- semver solver и implicit content overrides;
- Unity importer или общий renderer;
- реализация всех механик из стресс-матрицы;
- сохранение pre-MVP save v1 через постоянный compatibility layer.

## Критерий freeze

После `MM-02` владелец `MM-03` проверяет этот контракт на все 12 стресс-квестов. После статуса `Frozen` менять публичный seam можно только отдельной записью решения и повторным прогоном `MM-03` со всеми зависимыми пакетами.
