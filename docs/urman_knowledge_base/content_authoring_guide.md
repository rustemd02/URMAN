# Руководство по модульному авторингу для ЛЛМ

Этот документ — рабочая инструкция для ЛЛМ, сценариста или разработчика, который меняет УРМАН после модульной миграции. Цель: менять кампанию данными, не возвращая сюжетный код в ядро.

## Что можно менять без правки ядра

- персонажа в роли кампании;
- текст, диалог, сцену, квест, документ, clue, vocabulary и logical asset ref;
- состав и порядок модулей новой кампании;
- доступность существующего контента через typed conditions/effects;
- обычный side quest на уже существующих механиках.

Новая уникальная механика требует отдельного capability provider, но не изменения `RuntimeKernel`. Нельзя добавлять `if (questId === ...)`, `switch` по сценам, raw `/assets/`, `localStorage` или `window.URMAN` в generic runtime.

## Сначала прочитай

До любого изменения прочитай в таком порядке:

1. `../../AGENTS.md`.
2. `README.md`, `canon.md`, `chapter1_mvp_campaign.md` и `mvp_scope.md`.
3. `decision_log.md`, `open_questions.md`, `weak_points.md`.
4. `technical_architecture.md` и `../modular_migration/01_ARCHITECTURE_CONTRACT.md`.
5. Ближайший действующий аналог в `../../content/modules/**` и его `module.json`.

Если новое содержание меняет канон, путь Айдара, роль персонажа, татарский языковой слой, ассеты или MVP-приоритет, обнови относящийся к этому файл KB и `mindmap.md` в том же изменении. Не принимай непроверенную деталь за факт: используй `Hypothesis` или `Proposal` и внеси противоречие в `open_questions.md`.

## Модель данных

```text
content/modules/<moduleId>/
  module.json                  # manifest: файлы, provides, dependencies, assets
  definitions.json             # JSON-массив typed definitions
  documents/<id>.md            # optional Markdown document + frontmatter
  logical/<asset>.ref          # logical mapping, не browser path

content/campaigns/<campaignId>/campaign.json
  # entrypoint, module selection, role bindings, capability requirements,
  # narrativeOrder и narrative invariants
```

Каждый ID неизменяем и имеет форму `moduleId:kind/localId`, например `urman.chapter1:quest/quest_language_reread`. Дубли, aliases и неявные overrides запрещены. Зависимости модулей и capabilities используют точную версию; semver solver отсутствует намеренно.

Полный формат полей — в `../../content/schemas/**`. Не угадывай допустимый opcode или поле: скопируй ближайший рабочий record и измени минимально.

## Короткое дерево решений

| Нужно сделать | Где менять | Нужен код ядра? |
|---|---|---:|
| Переписать реплику | `TextDefinition` и/или `DialogueDefinition` в module source | Нет |
| Поменять актёра роли для эксперимента | `roleBindings` в dev campaign | Нет |
| Добавить документ старого ПК | Markdown + `module.json` модуля `urman.oldpc` | Нет |
| Добавить обычный side quest | новый или существующий module + campaign selection | Нет |
| Поменять путь игрока | scene interactions, conditions/effects и campaign invariants | Нет |
| Добавить новый вид механики | отдельный capability package/descriptor/config | Ядро — нет; provider — да |
| Изменить правила «Не отвечай» или канон | Сначала решение в KB | Не начинай без решения |

## Надёжный authoring flow

1. Сформулируй игровой результат в одной фразе: что игрок узнаёт, делает или получает.
2. Найди существующие definitions того же kind и повтори их структуру.
3. Выбери module owner. Не клади side quest в `urman.core`: core не должен становиться складом сюжета.
4. Добавь records и все referenced texts/assets/documents в один module. Допиши каждый новый ID в `provides` и каждый файл в `sourceFiles` соответствующего `module.json`.
5. Если content должен входить в прохождение, явно подключи module с точной версией в `campaign.json`.
6. Поставь only typed conditions/effects. Один пользовательский action должен оставаться одной атомарной kernel transaction.
7. Выполни проверки из раздела «Проверка».
8. Для изменения канона, языка, ассетов или MVP обнови KB и только потом проси review.

## Пример: обычный side quest только данными

Ниже — образец **не для немедленного добавления в production Chapter 1**, а для копирования структуры. «Тихая сверка» использует уже существующие механики knowledge и quest; он не создаёт новый provider, UI или scene renderer.

### 1. Создай отдельный модуль

`content/modules/urman.side-quiet-check/module.json`:

```json
{
  "schemaVersion": 1,
  "moduleId": "urman.side-quiet-check",
  "exactVersion": "1.0.0",
  "sourceFiles": ["definitions.json"],
  "assets": [],
  "dependencies": [
    { "moduleId": "urman.chapter1", "exactVersion": "1.0.0" }
  ],
  "provides": [
    "urman.side-quiet-check:text/quest-title",
    "urman.side-quiet-check:text/objective-title",
    "urman.side-quiet-check:quest/quiet-check"
  ],
  "requires": []
}
```

### 2. Добавь два текста и один quest record

`content/modules/urman.side-quiet-check/definitions.json`:

```json
[
  {
    "schemaVersion": 1,
    "id": "urman.side-quiet-check:text/quest-title",
    "value": {
      "default": "Compare two records quietly",
      "translations": { "ru": "Тихо сопоставить две записи" }
    },
    "purpose": "ui"
  },
  {
    "schemaVersion": 1,
    "id": "urman.side-quiet-check:text/objective-title",
    "value": {
      "default": "Keep the contradiction in view without confronting anyone.",
      "translations": { "ru": "Удержать противоречие в поле зрения, не устраивая конфронтацию." }
    },
    "purpose": "ui"
  },
  {
    "schemaVersion": 1,
    "id": "urman.side-quiet-check:quest/quiet-check",
    "titleTextId": "urman.side-quiet-check:text/quest-title",
    "stages": [
      {
        "id": "compare-existing-clues",
        "objectives": [
          {
            "id": "notice-second-record",
            "titleTextId": "urman.side-quiet-check:text/objective-title",
            "optional": true,
            "startConditions": [
              {
                "op": "knowledge.status",
                "knowledgeId": "urman.chapter1:knowledge/clue_marat_official_death_version",
                "status": "confirmed"
              }
            ],
            "completionConditions": [
              {
                "op": "knowledge.status",
                "knowledgeId": "urman.chapter1:knowledge/contradiction_marat_official_vs_internal",
                "status": "confirmed"
              }
            ],
            "completionEffects": [],
            "failureEffects": []
          }
        ],
        "composition": {
          "mode": "all",
          "objectiveIds": ["notice-second-record"]
        }
      }
    ],
    "outcomes": { "success": [], "optional": [], "failure": [] },
    "retryPolicy": { "mode": "none", "maximumAttempts": 1 },
    "checkpointPolicy": { "mode": "objective" },
    "cancelPolicy": { "allowed": true, "effects": [] }
  }
]
```

Это пример наблюдающего side quest: он использует уже существующие clues, не меняет канон и не вводит новый progression effect. Чтобы сделать его содержательнее, добавь новые typed knowledge/text/document records и связи с существующей сценой или документом — не обработчик в `Game`.

### 3. Подключи module только в экспериментальную кампанию

Скопируй действующий campaign manifest в новый dev campaign и добавь точную зависимость:

```json
{
  "moduleId": "urman.side-quiet-check",
  "exactVersion": "1.0.0"
}
```

в `modules`. Не добавляй side quest в production campaign без сценарного решения и review. Новый manifest получает новый SHA-256 fingerprint, поэтому тестировать его нужно новым run: hot-swap активного сохранения запрещён.

`narrativeOrder` — это авторский порядок и основа invariant-проверок. Он **не** запускает quest и не заменяет `startConditions`/`completionConditions`. Если нужно изменить фактический порядок доступности, меняй typed conditions/effects, а не только массив порядка.

## Частые операции

### Сменить NPC или диалог

- У диалога используются `participantRoles`, а concrete characters задаёт `campaign.json.roleBindings`.
- Для эксперимента создай dev campaign, замени одну binding-пару и проверь, что новый персонаж подходит роли и не меняет канон молча.
- Для новой реплики добавь `TextDefinition`, затем dialogue node с `textId`, `conditions`, `effects` и `choices`.
- Если interaction сцены открывает другой диалог, меняй `targetDialogueId` в scene definition. Не добавляй hardcoded scene switch в renderer или kernel.

### Поменять или добавить asset

- Добавь `AssetDefinition` с logical `file` ref, укажи ID в `assets` и `provides` manifest.
- Для недекоративного изображения добавь `altTextId`; для аудио — caption, transcript и equivalent non-audio cue.
- Не ставь `/assets/...` и не привязывайся к Vite path в content. Путь разрешает host resolver.

### Добавить старый ПК-документ

- Используй `urman.oldpc` и существующий Markdown/frontmatter pattern.
- Укажи путь Markdown-файла в `module.json.sourceFiles`, а document ID — в `provides`.
- `knowledgeRefs`, `accessConditions`, `openEffects` и `oldPc` metadata должны ссылаться на существующие canonical IDs или records, созданные в том же authoring change.
- Не возвращай raw parser, отдельный JSON corpus или chat placeholder.

### Добавить новую capability

Это уже не data-only change, но ядро менять не нужно. Создай isolated provider с exact protocol/version, schema/config definition и descriptor catalog entry. Он обязан пройти lifecycle `register → validate → create → restore → start → handle → snapshot → stop/dispose`, не оставив subscriptions, timers или scheduled jobs. Затем добавь `capabilityRequirements` в campaign и отдельные lifecycle/snapshot/cleanup tests.

## Правила Chapter 1, которые нельзя сломать

- Кырлай — деревня; Кара-Урман — запретное лесное урочище, не alias деревни.
- Мифологические существа — часть старого порядка, не мобы и не generic horror.
- Тимур хәзрәт — моральная и культурная опора; ислам нельзя подавать карикатурно или как «антифольклор».
- Айдар не становится избранным магом.
- До финала допустима только тревожная гипотеза `clue_voice_answer_is_dangerous_hint`.
- `clue_do_not_answer_rule` подтверждается только после вмешательства Рината у кромки Кара-Урмана. Не раскрывай это правилом в раннем диалоге, документе, journal, asset text или side quest.

## Проверка

Для content-only изменения запусти минимум:

```bash
npm run content:check -- --module urman.your-module
npm run content:check
npm run test:content
git diff --check
```

Для campaign/Content Lab проверки дополнительно:

```bash
npm run dev
# Открой URL, который напечатает Vite, и добавь ?lab=1.
# Создай новый isolated run с нужной campaign/module/quest/scene/preset/seed.
npm run test:e2e
```

Для нового provider или изменения generic runtime дополнительно:

```bash
npm run test:runtime
npm run test:architecture
npm run build
```

Если `content:check` сообщает duplicate ID, unknown opcode, missing asset/provider, broken reference, unreachable invariant или premature reveal, не обходи ошибку fallback-ом. Исправь data graph или вернись к сценарному решению.

## Готовый prompt для другой ЛЛМ

```text
Ты работаешь только с modular content УРМАН. Сначала прочитай AGENTS.md,
docs/urman_knowledge_base/README.md, canon.md, chapter1_mvp_campaign.md,
decision_log.md, open_questions.md, weak_points.md и этот guide.

Задача: <одна конкретная смена контента>.

Ограничения:
- Сначала найди ближайший рабочий аналог в content/modules/**.
- Используй только JSON/Markdown data, если механика уже существует.
- Не меняй RuntimeKernel, generic renderer, main, storage, schema или capability provider
  без отдельной причины и явного объяснения.
- ID имеют формат moduleId:kind/localId; aliases и overrides запрещены.
- Не раскрывай clue_do_not_answer_rule до финального действия Рината.
- При каноническом противоречии остановись и запиши вопрос, не придумывай решение.

Сделай минимальное изменение, обнови manifest/campaign/KB только когда это нужно,
запусти npm run content:check и релевантные тесты. В финале перечисли changed
paths, результат проверок, влияние на канон и оставшиеся риски.
```

## Граница ответственности

Это руководство объясняет действующую web implementation v1. Portable граница — JSON/Markdown, schemas, IDs и capability protocols; Unity importer, пользовательские моды, live content swap, ECS и произвольный scripting framework не входят в задачу. Если нужен один из них, сначала создай отдельное архитектурное решение.

## Journal actions in Act I (2026-09-11)

An existing scene interaction may carry optional `journalAction: {sourceIds: [idA, idB], resultTextId}`. Exactly two distinct document/knowledge sources are required; scene/dialogue/document transition targets cannot coexist with it. `CompiledCampaignRepository` checks source and localized-text closure on load. JournalUi projects choices; RuntimeBridge validates the selected pair and dispatches the same `content.apply` command. The kernel checks source presence against the transaction's journal before applying effects. Physical interaction dispatch rejects journal actions. No second narrative state or save format is introduced.

Keep observation and conclusion separate. Record key document sources in their `openEffects` and physical observations with `journal.record`. A wrong hypothesis has authored feedback and no destructive effects; the successful choice changes existing knowledge/vocabulary state. Do not also confirm that conclusion in `onEnter` or a document's `knowledgeRefs`.

### Physical world interactions (Act I, 2026-09-11)
An optional scene interaction may declare `worldLocations` with one or more
existing Act I logical locations. It is available by the player's location
and its authored conditions, independent of the active investigation scene.
Such an action cannot target another scene or be a journal comparison and
never executes its source scene's `onExit`. Successful effects force the
existing checkpoint; the kernel remains the sole discovery-state owner.
Outdoor walks do not automatically switch logical locations, so an optional
exterior find normally allows all three exterior IDs. The old-PC power action
now declares `house_old_pc` instead of a runtime ID special case.

`targetJournalEntryId` selects a recorded entry in the existing journal. The
action must contain a matching `journal.record`, with both entry/source IDs
known to the journal projection. It excludes the other targets and
`journalAction`. Physical targets pass it to `JournalUi.Open`; they only open
that entry after a successful commit. A physical reveal need not open a panel.
Visible geometry and any removable collision must explicitly project the
saved knowledge on initial construction, load and new game; declaring
`worldLocations` alone does not create a secret or a passage.


### Conditional dialogue entry (2026-09-13)

A dialogue may declare ordered `entryRoutes: [{conditions, nodeId}]`.
Each route has at least one existing content condition and targets a node in
that dialogue. RuntimeBridge chooses the first matching route from current
shared state **before** DialogueUi applies any node effects. No match (or no
routes) uses the existing `startNodeId`. The selected node still runs its
normal conditions/effects; a denied node does not try other entry routes.
The compiler and compiled repository reject missing target nodes.

Mansur uses existing `pc_access_granted` for his repeat entry; the original
start node grants access only after entry selection. No visited-state store,
UI-specific NPC IDs or save schema change is introduced. Content changes
still change the campaign fingerprint and can invalidate older saves.
