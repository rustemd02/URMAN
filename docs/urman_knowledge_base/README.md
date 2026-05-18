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
- `gameplay.md` — core loop, расследование, диалоги, язык, интерфейсы.
- `old_pc.md` — продуктовый и сценарный lock старого ПК бабая.
- `narrative.md` — сюжетная архитектура.
- `characters.md` — персонажи, роли, знания, тайны и ассеты.
- `village_lore.md` — Кырлай как место и социальная система.
- `mythology.md` — существа, пакт и правила сосуществования.
- `language_learning.md` — татарский язык как игровая механика.
- `design_style.md` — визуальный стиль, арт-дизайн, концепция спрайтов и ассетов.
- `village_route_art_spec.md` — что конкретно рисовать для ходибельной маршрутной карты деревни.
- `assets.md` — инвентаризация ассетов.
- `asset_inventory_50.md` — 50 ключевых production-ассетов MVP с вариациями.
- `technical_architecture.md` — engine-neutral архитектура систем.
- `open_questions.md` — нерешённые вопросы.
- `weak_points.md` — честные слабые места и пути решения.
- `roadmap.md` — путь до MVP.
- `playtest_plan.md` — матрица плейтестов, ручные сценарии, метрики и release gate для MVP.
- `backlog.md` — задачи.
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

Проект уже имеет сильный сюжетный и лорный фундамент, working route-navigation prototype, old PC document hub and a visually broad MVP asset pack. Главный текущий blocker до полноценного MVP — не нехватка лора или картинок, а отсутствие общей сцепки `old PC clue -> shared knowledge key -> journal -> dialogue reaction -> vocabulary re-read -> pressure -> cliffhanger`. Сначала смотреть `mvp_completion_handoff.md`, затем `playtest_plan.md`.
