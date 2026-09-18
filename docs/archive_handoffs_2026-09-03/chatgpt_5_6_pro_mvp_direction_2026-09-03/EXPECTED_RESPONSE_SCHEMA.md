# Контракт ответа GPT-5.6 Pro

Ответ Pro должен быть на русском и следовать этому порядку. Не заменяй tracker общими рекомендациями. Каждый крупный вывод должен иметь inline-ссылку на путь внутри bundle (например, `CURRENT_STATE.md`, `game/scripts/RuntimeBridge.cs` или `visual_evidence/current_act1_360_failed_gate/act1_full_route_core_world_receipt.json`).

## Обязательные разделы

Используй эти headings без переименования и не пропускай ни один:

1. `VERDICT`
2. `MVP LOCK`
3. `CRITICAL PATH`
4. `ZONE PLAN`
5. `SYSTEM PLAN`
6. `TASK TRACKER`
7. `FIRST WAVE`
8. `STOP DOING`
9. `RELEASE GATES`
10. `RISKS/QUESTIONS`
11. `DIRECTIVE TO CODEX`

### VERDICT

- точный статус текущего продукта;
- ровно пять причин, по которым он не shippable MVP;
- отдельные границы technical traversal, file-integrity, visual acceptance, human playtest и release readiness;
- явное признание текущего 44-frame/25-unique failed capture gate.

### MVP LOCK

- player-visible start и end Act I;
- target experience и эмоциональная/визуальная планка;
- mandatory systems/content;
- explicit out-of-scope (как минимум Acts II–V, combat, open world, full pact reveal, premature packaging/optimization);
- условия, после которых слово «готов» допустимо.

### CRITICAL PATH

Покажи dependency graph (Mermaid, ASCII или компактная таблица) от current dirty greybox до playable/beautiful MVP. У каждого узла должны быть owner, prerequisite и evidence artifact. Отдельно покажи блокирующую зависимость capture/ownership и порядок human/cultural/platform gates.

### ZONE PLAN

Опиши ровно восемь direct visual zones: `Arrival`, `MainStreet`, `BabaiEbiYard`, `HouseExteriorApproach`, `ConnectiveStreetReturn`, `FapExterior`, `ZiratMemoryField`, `KaraForestEdge`. Для каждой требуются:

- near/mid/far composition и distinctive authored read;
- forward/back/lateral/360 acceptance и route/wayfinding;
- exact current owner и файлы;
- текущий evidence gap, включая duplicate capture;
- cheapest relevant verification command/artifact;
- культурная/религиозная safety note, если зона её затрагивает.

`house_interior` и `fap_interior` должны быть показаны отдельно как interior checkpoints: не увеличивай ими direct-zone count и не скрывай их hash/coverage status.

### SYSTEM PLAN

Для narrative/dramaturgy, interactions, old PC/documents/journal, татарского языка, audio/voice, save/persistence, onboarding/settings, accessibility/motion comfort и performance/platform укажи:

- minimum MVP contract;
- current source-of-truth file/module;
- missing evidence;
- release gate;
- сохранение `RuntimeBridge` как sole narrative/progression/save writer.

### TASK TRACKER

Это должен быть **полный исчерпывающий tracker всего remaining Act I scope** от current state до playable, beautiful, tested MVP и release-candidate. Не ограничивай таблицу 5–10 строками и не выдавай только ближайшие действия. Количество строк определяется реальными независимыми ownership/acceptance slices: столько, сколько нужно для закрытия всех gaps и gates, без календарных оценок и без искусственного объединения разных owners.

Таблица должна быть конкретной и иметь **точно** такую строку заголовков:

| ID | Priority | Outcome | Exact ownership | Depends on | Acceptance evidence | Verification command/artifact | Stop/rollback condition | Release gate |
|---|---|---|---|---|---|---|---|---|

Правила строк:

- `ID` стабилен, уникален и пригоден для follow-up;
- `Priority` только `P0`, `P1` или `P2`;
- `Outcome` — наблюдаемый результат, не «улучшить»;
- `Exact ownership` — точный файл, класс, data block или asset-manifest scope; один ответственный Luna slice на строку;
- `Depends on` — стабильные IDs или `—`;
- `Acceptance evidence` — конкретный frame/receipt/route/state/text/human sign-off;
- `Verification command/artifact` — самая дешёвая релевантная команда или артефакт, без фиктивных команд;
- `Stop/rollback condition` — измеримый сигнал остановки или возврата, включая ownership/canon conflict;
- `Release gate` — `MVP`, `PUBLIC`, `CULTURAL`, `ACCESSIBILITY`, `PLATFORM`, `PLAYTEST`, `POST-MVP` или явная комбинация.

Не делай одну строку «переделать всю игру», не объединяй несовместимых owners и не добавляй Sol implementation/review. Каждая task-slice должна быть исполнима Luna-only без скрытой параллельной работы. Tracker обязан покрывать как минимум: все 8 direct visual zones, `house_interior`, `fap_interior`, capture/evidence repair, canonical ownership, narrative/content, physical interactions, old PC/documents/journal, татарский vocabulary, audio/voice/silence, art/material/light/foliage/terrain, save/persistence, onboarding/settings, accessibility/motion comfort, QA, human playtest, cultural/language/religious review, performance/platform и packaging/provenance/public release-candidate. Если item уже закрыт, не создавай фиктивную задачу: укажи evidence и статус закрытия.

После основной таблицы, но внутри `TASK TRACKER`, добавь подзаголовок `COVERAGE MATRIX / COMPLETENESS CHECK` и таблицу минимум с колонками `Coverage item | Source gap/gate | Tracker IDs | Exact owner | Exit evidence | Status`. В matrix должны присутствовать все items из `GAPS_AND_RELEASE_GATES.md`, включая 8 зон, два interior checkpoint-а, P0 duplicate capture, canonical presentation ownership, narrative/interaction/audio, cultural/language/religious, accessibility/motion, performance/platform, save/persistence, onboarding/settings, QA/human playtest, packaging/provenance и release-candidate. Разверни каждую отдельную строку и каждый отдельный bullet-gate из `GAPS_AND_RELEASE_GATES.md`, включая polish и public-demo bullets; одной агрегированной строкой на раздел недостаточно. Добавь итог:

`Completeness check: unmapped gaps = 0; out-of-scope items are explicitly cited.`

Ни один установленный gap не может исчезнуть между `GAPS_AND_RELEASE_GATES.md` и tracker. `OUT OF SCOPE` допустим только для явно отложенных Acts II–V/post-MVP items с цитатой authoritative source; Act I MVP/release-candidate gates нельзя пометить так для удобства.

### FIRST WAVE

Выбери **5–10 ID из полного tracker-а**, перечисли их в строгом порядке и объясни каждую dependency. Это только первая исполняемая волна, а не размер tracker-а: остальные tracker rows должны остаться видимыми и иметь полный dependency path до MVP/release-candidate. Ownership первой волны не пересекается. Включи только Act I critical path; capture failure и canonical-owner decision должны быть P0, если tracker считает их блокирующими.

### STOP DOING

Коротко перечисли остановленные практики и evidence-based причину: benchmark churn, duplicate worlds/owners, stale experiment revival, premature optimization/packaging и review без acceptance artifact.

### RELEASE GATES

Таблица должна раздельно показать: внутренний MVP, public demo, human playtest, cultural/language/religious, accessibility/motion, performance/platform, save/recovery, onboarding/settings, packaging/provenance. Укажи `Blocker`, `Polish` или `Post-MVP`, owner, evidence и exact exit condition. Technical smoke PASS нельзя переписать в visual/release PASS.

### RISKS/QUESTIONS

Сначала перечисли риски с mitigation/stop condition, затем только вопросы, без которых нельзя принять конкретное решение. Вопрос не должен дублировать факт, который уже есть в bundle.

### DIRECTIVE TO CODEX

Дай одну страницу действий родительскому архитектору: locked decisions, IDs первой волны, порядок, accepted evidence и stop condition. Директива должна быть копируемым следующим execution plan, не календарным планом и не запросом на Sol.

## Запрещённые подмены

- Нельзя называть текущий demo готовым из-за build/smoke/receipt.
- Нельзя считать 44 файла 44 уникальными видами: в текущем receipt 25 уникальных SHA-256.
- Нельзя выдавать `docs/experiments/agent_b_act1_world_report.md`, target PNG или старый handoff за current production proof.
- Нельзя разрешать канонические противоречия молча.
- Нельзя предлагать ground-up rewrite без доказательства архитектурной невозможности.
- Нельзя оценивать календарный срок или притворяться, что human/cultural/platform review уже выполнен.
