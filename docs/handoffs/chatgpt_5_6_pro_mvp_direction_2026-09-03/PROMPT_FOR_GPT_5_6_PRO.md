Ты — GPT-5.6 Pro, независимый production advisor для проекта УРМАН. Твоя задача — вернуть родительскому архитектору решительное, проверяемое направление, которое может исполняться только Luna-задачами. Это не просьба похвалить демо и не просьба написать код.

Сначала прочитай **весь bundle**, включая `SOURCE_MANIFEST.md`, все семь handoff-документов, каждый указанный source-файл и все 44 PNG с receipt. Не отвечай после чтения только README или краткого summary. Для каждого крупного вывода укажи путь(и) внутри bundle; особенно цитируй `CURRENT_STATE.md`, `ARCHITECTURE_AND_OWNERS.md`, `GAPS_AND_RELEASE_GATES.md`, реальный код и конкретный receipt/PNG. Если источник не подтверждает вывод, напиши `не подтверждено`, а не заполняй пробел правдоподобной догадкой.

## Рамка доверия

Различай четыре класса фактов:

1. canonical source: `AGENTS.md`, разделы 1–17 `URMAN_Codex_Context.md` и рабочие canon/decision/open-question docs;
2. current working-tree implementation: `game/`, `content/`, `eng/` и tests, которые могут быть dirty и не равны release artifact;
3. historical benchmark/target и experiment: старые handoff/route docs, target PNG, `docs/experiments/agent_b_act1_world_report.md`; эксперимент Agent B не является вторым каноническим runtime;
4. current runtime capture: `visual_evidence/current_act1_360_failed_gate/` и receipt.

Критически учти текущий evidence contract: 44 PNG, receipt hashes совпадают с 44 файлами, real root viewport=true, subviewport=false, capture_process_count=1, 8 direct visual zones, 10/10 waypoints, 242.49586 m; но только 25 уникальных SHA-256. 16 файлов сериализовали один view (`connective_street_return` — 3, `fap_exterior` — 5, `fap_interior` — 4, четыре направления `zirat`), все 5 `kara_approach` сериализовали один view. Wrapper завершился с code 1 по uniqueness gate. Это **текущий P0 capture-gate failure**, а не разрешение блокировать задачу и не разрешение подменять кадры старым benchmark. Не объявляй поздние зоны прошедшими 360° review. Отдельно различай file-integrity PASS, technical traversal PASS и visual/human acceptance.

Сохраняй татарский культурный код и религиозную аккуратность: мифологические существа — старый порядок, территория и правила, не мобы; Кырлай, Айдар, Марат, Тимур хәзрәт, зират, язык и исламская рамка не должны быть generic horror-декорацией. Сохраняй `RuntimeBridge` единственным владельцем narrative/progression/save state. `Act1WorldLayout` отвечает за deterministic placement, `Act1ConnectedWorld` — за presentation envelope; не придумывай второй state/world/audio owner. Названия и противоречия (Айдар/Айрат, Кырлай/Кара-Урман, Гөлсинә/Миннигуль, два Марата) не разрешай молча.

Фокус — только Act I: приезд, дом бабая и әби, социальное напряжение, Alsu, расследование Марата, old PC/архив, первый татарский слой, первый мистический след и cliffhanger `Не отвечай`. Не расширяй план на Acts II–V, полный pact reveal, combat, open world, полный KFU intro, premature packaging или преждевременную optimization. Ground-up rewrite допустим только если ты докажешь по source-коду, что текущая архитектура не может поддержать MVP; по умолчанию предпочитай удалить/заменить duplicate presentation owners и закончить одну canonical connected map.

Не оценивай календарное время. Не выдумывай скрытые ассеты, platform numbers, playtest outcomes, voice quality или фактический human approval. Не добавляй Sol implementation/review tasks: родительский Sol-чат координирует, а исполнение остаётся Luna-only.

## Верни ответ строго по следующим разделам

### VERDICT

Дай точный executive verdict о текущем продукте и ровно пять главных причин, почему он не является shippable MVP. Отдели технически доказанное от визуального, human и release evidence. Прямо скажи, что означает duplicate capture failure и почему текущий demo нельзя назвать готовым.

### MVP LOCK

Зафиксируй player-visible start/end, целевое ощущение, обязательные systems/content и явный out-of-scope. Опиши минимальный Act I, который можно назвать playable **и** красивым после gates; не принимай «все systems существуют в коде» за acceptance.

### CRITICAL PATH

Построй dependency graph от dirty greybox/current evidence к playable/beautiful MVP. Укажи блокирующие зависимости и порядок: capture root cause/ownership lock, canonical world/art, route/narrative/interaction/audio, language/cultural/accessibility, human playtest и только затем platform/public gates. Если порядок следует изменить, докажи это ссылками на source/evidence.

### ZONE PLAN

Дай конкретный план для всех восьми direct visual zones: `Arrival`, `MainStreet`, `BabaiEbiYard`, `HouseExteriorApproach`, `ConnectiveStreetReturn`, `FapExterior`, `ZiratMemoryField`, `KaraForestEdge`. Для каждой укажи near/mid/far, forward/back/lateral/360 acceptance, route/wayfinding, distinctive authored read, культурные ограничения, exact current owner, evidence gap и самый дешёвый verification artifact. Не скрывай `house_interior` и `fap_interior`: покажи их как interior checkpoints с отдельным acceptance и учти их duplicate/unique статус по receipt.

### SYSTEM PLAN

Свяжи с реальными владельцами текущего маршрута narrative/dramaturgy, physical interactions, old PC/documents/journal, татарский vocabulary, audio/voice/silence, save/persistence, onboarding/settings, accessibility/motion comfort и performance/platform. Для каждой системы назови минимальный MVP contract, текущий source of truth, missing proof и release gate. Укажи, где `RuntimeBridge` должен остаться sole writer.

### TASK TRACKER

Составь **полный и исчерпывающий** ranked tracker от текущего dirty/greybox состояния до завершённого играбельного, красивого, оттестированного Act I MVP и release-candidate. Это не список ближайших идей и не только первая волна: tracker обязан охватить весь remaining scope, который реально нужен для закрытия всех eight visual zones и interior checkpoints, narrative/content, interactions, old PC/documents/journal, татарского vocabulary, audio/voice, canonical ownership, art/material/light/foliage/terrain, capture/evidence, save/persistence, onboarding/settings, accessibility/motion comfort, QA, human playtest, cultural/language/religious review, performance/platform и packaging/provenance/public release gates. Размер tracker не фиксирован: создай столько независимых task-slices, сколько требуется evidence и ownership, не объединяй несовместимые работы в одну строку и не сокращай tracker ради компактности.

Каждая строка должна иметь ровно одну ответственную Luna-задачу, непересекающегося владельца, точный файл/модуль, dependency, observable acceptance evidence, дешёвую команду или artifact verification, stop/rollback condition и release gate. Используй **точно** эти колонки:

`ID | Priority | Outcome | Exact ownership | Depends on | Acceptance evidence | Verification command/artifact | Stop/rollback condition | Release gate`

Не пиши «улучшить визуал» или «сделать красиво» без конкретного owner, view/route или измеримого результата. Не создавай параллельный world, state store, audio owner или capture harness без доказанной необходимости. Если существующий gap уже закрыт доказательством, явно отметь это в tracker/coverage matrix и приведи evidence, а не дублируй задачу.

В конце `TASK TRACKER` добавь обязательную `COVERAGE MATRIX / COMPLETENESS CHECK`. Сопоставь **каждый** item из `GAPS_AND_RELEASE_GATES.md` и каждый установленный риск/gate с одним или несколькими tracker ID: все 8 direct visual zones (`Arrival`, `MainStreet`, `BabaiEbiYard`, `HouseExteriorApproach`, `ConnectiveStreetReturn`, `FapExterior`, `ZiratMemoryField`, `KaraForestEdge`), `house_interior`, `fap_interior`, capture failure, canonical ownership, narrative/content, interactions, audio, cultural/language/religious review, accessibility/motion, performance/platform, save/persistence, onboarding/settings, QA/human playtest, packaging/provenance и public release-candidate gate. Разверни в matrix каждую отдельную строку и каждый отдельный bullet-gate из `GAPS_AND_RELEASE_GATES.md`, а не только крупные заголовки. Для каждой строки matrix укажи source gap, tracker IDs, owner/gate и доказательство закрытия. Matrix должна иметь итоговую проверку `unmapped gaps = 0`; нельзя заменить её фразой «всё покрыто». Если item сознательно не входит в Act I release-candidate, пометь его `OUT OF SCOPE` и процитируй authoritative source.

### FIRST WAVE

После полного tracker выбери из него первую волну из **5–10 конкретных ID** и дай явный порядок исполнения. FIRST WAVE — только выделенное начало полного tracker-а, а не его замена и не ограничение общего числа задач. Ownership slices не должны пересекаться, а acceptance одной задачи должен быть входом только для явно зависящих задач. В волне не должно быть Sol implementation/review tasks, календарных оценок или задач из Acts II–V. Включи P0 capture failure и ownership decision, если они действительно блокируют дальнейшее визуальное решение.

### STOP DOING

Дай короткий запретительный список: benchmark churn, повторная генерация одинаковых world layers, duplicate presentation owners, возврат retired Agent B/web path, преждевременная optimization/packaging, manual review без валидного capture и endless review без acceptance artifact. Оставь только то, что следует из evidence; если запрет не нужен, объясни почему.

### RELEASE GATES

Раздели gates на: внутренний playable/beautiful Act I MVP; public demo; human playtest; cultural/language/religious review; accessibility/motion; performance/platform; save/recovery; onboarding/settings; packaging/provenance. Для каждого укажи blocker/polish/post-MVP, owner, evidence и exact exit condition. Не превращай старые technical PASS в release PASS.

### RISKS/QUESTIONS

Перечисли риски и только genuinely blocking questions. Для каждого вопроса скажи, какой конкретный выбор или artifact разблокирует работу. Не задавай вопросы, на которые можно ответить чтением bundle; сначала сам прочитай source.

### DIRECTIVE TO CODEX

Заверши одностраничной директивой родительскому архитектору: что считать locked, что делать первым, какие Luna slices создать, какой evidence принять и где остановиться. Директива должна быть готова для вставки в следующий Codex execution plan и не должна содержать Sol implementation/review work.

Ответ напиши по-русски. Цитируй bundle-пути рядом с каждым major conclusion. Если evidence недостаточно, это должно уменьшать силу утверждения, а не превращаться в оптимистичный verdict.
