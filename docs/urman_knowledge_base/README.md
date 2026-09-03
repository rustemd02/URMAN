# URMAN Knowledge Base

Эта папка — рабочая база знаний проекта **УРМАН**. Она нужна не для декоративной документации, а чтобы будущие Codex-сессии, сценаристы, геймдизайнеры и разработчики быстро понимали канон, MVP, риски и следующие шаги.

## Как пользоваться

Перед работой читать:

1. `../../AGENTS.md`
2. этот `README.md`
3. релевантный файл базы знаний
4. `decision_log.md`
5. `open_questions.md`
6. `weak_points.md`

Главный сырой источник: `../../URMAN_Codex_Context.md`.

## Файлы

- `mindmap.md` — центральная карта проекта.
- `project_brief.md` — короткий high-level бриф.
- `canon.md` — hard canon, soft canon, гипотезы и противоречия.
- `mvp_scope.md` — вертикальный срез MVP.
- `mvp_completion_handoff.md` — главный handoff-документ: что именно не хватает до полноценного MVP и в каком порядке это добивать.
- `chapter1_mvp_campaign.md` — сценарный lock 40–60-минутной первой главы / MVP vertical slice: акты, сцены, clue graph, NPC reactions, татарский язык, fear escalation и финал «Не отвечай».
- `gameplay.md` — core loop, расследование, диалоги, язык, интерфейсы.
- `old_pc.md` — продуктовый и сценарный lock старого ПК бабая.
- `narrative.md` — сюжетная архитектура.
- `narrative_lock_acts_2_5.md` — production lock актов 2–5: beat sheets, clue graph, зоны, NPC, документы, языковые ключи, world variants, threat beats и handoff-gates.
- `characters.md` — персонажи, роли, знания, тайны и ассеты.
- `village_lore.md` — Кырлай как место и социальная система.
- `mythology.md` — существа, пакт и правила сосуществования.
- `language_learning.md` — татарский язык как игровая механика.
- `design_style.md` — визуальный стиль, арт-дизайн, концепция спрайтов и ассетов.
- `village_route_art_spec.md` — что конкретно рисовать для ходибельной маршрутной карты деревни.
- `assets.md` — инвентаризация ассетов.
- `asset_inventory_50.md` — 50 ключевых production-ассетов MVP с вариациями.
- `technical_architecture.md` — engine-neutral архитектура систем.
- `content_authoring_guide.md` — пошаговое руководство для ЛЛМ: как менять кампанию, квесты, диалоги, роли, документы и ассеты через portable modules.
- `open_questions.md` — нерешённые вопросы.
- `weak_points.md` — честные слабые места и пути решения.
- `roadmap.md` — путь до MVP.
- `playtest_plan.md` — матрица плейтестов, ручные сценарии, метрики и release gate для MVP.
- `backlog.md` — человекочитаемый индекс задач и исторические audit-записи.
- `execution_backlog.json` — машиночитаемая очередь для оркестратора: вехи, зависимости, владельцы, критерии готовности, проверки и evidence.
- `decision_log.md` — решения.
- `glossary.md` — словарь терминов.
- `next_10_actions.md` — 10 ближайших действий до MVP.

## Как обновлять mindmap

`mindmap.md` обновляется вместе с любым решением, которое меняет:

- MVP;
- персонажей;
- сюжетную структуру;
- лор Кырлая;
- татарский language-learning слой;
- ассеты;
- технические системы;
- риски или открытые вопросы.

Новые ветки добавлять коротко: один узел — одна сущность, система, риск или линия. Если ветка требует производства, связывать её с задачей в `backlog.md`. Если ветка не решена, помечать `[OPEN]`; если критична для MVP — `[MVP]`; если требует ассетов — `[ASSET]`.

Пример:

```mermaid
mindmap
  root((УРМАН))
    Gameplay [OPEN][RISK]
      Core Loop [MVP][OPEN]
      Dialogue Keys [MVP][TECH]
      Татарский язык [LANG][RISK]
```

## Правило фактов

- `Hard Canon` нельзя менять без записи в `decision_log.md`.
- `Soft Canon` можно уточнять, если не ломается суть.
- `Hypotheses` и `Proposal` нельзя выдавать за установленную правду.
- Любое противоречие записывать в `open_questions.md`.

## Текущее состояние

Проект имеет завершённую modular architecture: portable campaign modules, transactional kernel, capability providers, Content Lab и retired legacy owners. Это готовность достраивать MVP, а не заявление «MVP готов». Главный product blocker — довести и проверить целостный опыт игрока, ассеты, звук, UX и плейтестовый release gate. Сначала смотреть `chapter1_mvp_campaign.md`, затем `mvp_completion_handoff.md` и `playtest_plan.md`; старые runtime paths в historical sections не восстанавливать.

## Текущий продуктовый объём — connected greybox Акта I (2026-08-15)

Активная цель больше не считает набор изолированных benchmark-сцен
атмосферной демкой. Сначала нужно собрать одну компактную территорию в
`game/scenes/act1_demo.tscn`: въезд в Кырлай, главная дорога, двор бабая и
әби с домом/старым ПК, связная улица с домами и заборами, ФАП, обратная дорога,
зират и постепенный подход к кромке Кара-Урмана. При развороте игрок должен
видеть продолжение деревни, а не пустой фон или границу отдельной сцены.

После connected-greybox gate эта территория станет основой first-person
прохода через 16 авторских beats, расследование Марата, документы/журнал,
татарский re-read и постановочное «НЕ ОТВЕЧАЙ» / «Конец демо». До свежего
360°/near-mid/far прохода и визуального review демо не называть готовой или
«вау»-сборкой.

Акты 2–5, полный 6–8-часовой playthrough, производство полного набора ассетов,
release-class Windows/M1 acceptance и web retirement сейчас не являются
критериями демо и остаются долгосрочным deferred scope. Их исходники, narrative
lock и browser saves не удаляются и не становятся частью launch path.

## Execution backlog

`execution_backlog.json` — единственная очередь исполняемых задач для текущей
Godot-миграции. Её текущий scope — `urman-act1-connected-greybox` и
`game/scenes/act1_demo.tscn`; полный migration intent в
`docs/aegis/work/2026-08-10-godot-full-migration/10-intent.md` сохранён как
long-term north star. `backlog.md`, roadmap и Aegis evidence остаются её
человочитаемыми индексами и доказательствами. Оркестратор не должен создавать
альтернативный task owner или менять канон прямо из очереди: для сюжетных и
архитектурных решений он обязан ссылаться на `decision_log.md`,
`open_questions.md` и соответствующий authority-документ.

Для выполнения задачи оркестратор выбирает только `ready`-элемент, проверяет `depends_on`, захватывает `active_owner`, выполняет `verify`, добавляет evidence и только затем ставит `completed`. `blocked`-элементы не исполняются автоматически; `REL-004` намеренно остаётся последним absence-gated cutover.
