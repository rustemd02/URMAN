---
task_id: MM-00
title: Оркестрация модульной миграции УРМАН
status: complete
kind: orchestration
depends_on: []
blocks: [MM-01]
parallel_group: null
owner_scope:
  - docs/modular_migration/00_ORCHESTRATOR_README.md
forbidden_scope:
  - src/**
  - content/**
  - public/**
deliverables:
  - Актуальный статус пакетов и доказательств
  - Последовательная интеграция по графу зависимостей
verification:
  - npm run validate:content
  - npm run build
handoff: orchestrator-owned
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# Оркестратор модульной миграции УРМАН

## Цель и стоп-условие

Цель миграции — сделать кампанию заменяемой композицией данных, оставить ядро единственным владельцем состояния и вынести уникальные механики в изолированные capability-провайдеры. Работа завершена только после `MM-80`; сама миграция не означает, что игра получила статус MVP.

Остановиться со статусом `blocked`, если изменение требует нового решения по канону, движку, совместимости сохранений или публичному контракту после freeze. Не подменять решение временным fallback.

## Базовый контекст

Перед любым пакетом исполнитель читает:

1. `AGENTS.md`.
2. `docs/urman_knowledge_base/README.md`.
3. `docs/urman_knowledge_base/decision_log.md`.
4. `docs/urman_knowledge_base/open_questions.md`.
5. `docs/urman_knowledge_base/weak_points.md`.
6. Все пакеты из `depends_on` и их handoff.

Исходный проверяемый baseline перед первым кодовым пакетом:

```bash
npm run validate:content
npm run build
git status --short
```

На момент создания пакета обе npm-команды проходят, рабочее дерево чистое. Оркестратор обязан повторить baseline перед `MM-20` и не приписывать миграции уже существовавшие ошибки.

## Граф выполнения

```text
MM-00 → MM-01 → MM-02 → MM-03 → MM-10 → MM-20 → MM-30 → MM-31
                                                               ├→ MM-40 ─┐
                                                               └→ MM-41 ─┴→ MM-42 → MM-45
                                                                                         ├→ MM-50 ─┐
                                                                                         ├→ MM-51  │
                                                                                         ├→ MM-52  ├→ MM-54 → MM-55 → MM-60 → MM-70 → MM-80
                                                                                         └→ MM-53 ─┘
```

`parallel_group` обозначает только независимость зависимостей: foundation-пакеты `MM-40`/`MM-41` и migration-пакеты `MM-50`–`MM-53` могут быть готовы к старту одновременно, но не редактируют filesystem одновременно.

Текущая авторизованная модель — один shared workspace и строго последовательные edits. В каждый момент активен ровно один implementer/editor; reviewers работают read-only и не меняют дерево. Оркестратор принимает handoff, проверяет owner scope и обязательные команды, завершает editor turn и только затем запускает следующий пакет. Отдельные worktree, commit/cherry-pick/merge и «интеграция волны» не используются: полномочий на эти действия нет.

## Статусы

| Пакет | Статус | Условие старта |
|---|---|---|
| MM-01–MM-03 | complete | frozen contract и два review-stage приняты 2026-07-17 |
| MM-10 | complete | authority/ID/retirement baseline принят 2026-07-17 |
| MM-20 | complete | portable schemas и bounded amendment приняты двойным review 2026-07-17 |
| MM-30 | complete | compiler/types/Vite adapter и оба review-stage приняты 2026-07-17 |
| MM-31 | complete | kernel/registries приняты двойным review 2026-07-17 |
| MM-40, MM-41 | complete | world/capabilities и resolver owners приняты двойным review 2026-07-17 |
| MM-42 | complete | GameSnapshotV2 и campaign lock приняты двойным review 2026-07-17 |
| MM-45 | complete | foundation canary принят двойным review 2026-07-17 |
| MM-50–MM-53 | complete | campaign/active runtime/old PC/MainMap миграции приняты двойным review 2026-07-17 |
| MM-54 | complete | MM-54P production persistence bridge принят независимым QUALITY review 2026-07-18 |
| MM-55 | complete | MM-55P static boundary/factory-caller gate принят независимым QUALITY review 2026-07-18 |
| MM-60 | complete | Lab/stress/E2E приняты после SPEC/QUALITY remediation 2026-07-18 |
| MM-70 | complete | production persistence, reset и validator retirement приняты после coordinated review 2026-07-18 |
| MM-80 | complete | final architecture review PASS, full verification и KB handoff закрыты 2026-07-18 |

Только оркестратор изменяет эту таблицу. Исполнители записывают результат в handoff своего task-файла или возвращают его сообщением.

Строки с несколькими пакетами означают одновременную dependency readiness, а не parallel editing. Оркестратор выбирает их по одному в любом порядке, не нарушающем граф.

## Правила владения

- `MM-54` — единственный пакет, который меняет composition root: `Game.ts`, `GameState.ts`, `SceneManager.ts` и `main.ts`.
- `MM-50` владеет только campaign/content data; `MM-51` — активными сценами и UI-адаптерами; `MM-52` — старым ПК и DedOS; `MM-53` — `src/MainMap/**`.
- Общие контракты не добавляются в `src/types/game.types.ts`: каждый ранний пакет создаёт свой узкий owner-файл в отведённой директории.
- Нельзя держать старый и новый источник истины одновременно. Каждый перенос заканчивается классификацией `migrated and retired`, `dev-only quarantine` или `deleted`.
- Task-пакеты не дают разрешения на commit, push, merge, изменение канона или установку зависимостей. Для этого нужна отдельная команда владельца проекта.

## Контроль дрейфа

После каждого пакета оркестратор проверяет:

- задача осталась внутри `owner_scope`;
- не появился новый глобальный объект, service locator, parser или fallback;
- общие модули не знают ID УРМАН, конкретных персонажей или квестов;
- старый владелец удалён либо имеет записанную дату и пакет удаления;
- обязательные тесты пакета реально запущены;
- изменение frozen-контракта оформлено как остановка и повторный `MM-03`, а не тихая правка.

## Фальсификатор модульности

Архитектура не принята, если добавление обычного квеста на существующих механиках требует изменить `GameState`, `SceneManager`, общий UI или provider другой механики. Новая уникальная механика может добавить capability-пакет и запись в catalog, но не ветку с ID квеста в ядре.

## Итоговый handoff — 2026-07-18

`MM-00–MM-80` завершены. Data-only граница включает portable JSON/Markdown modules, кампании, сцены, квесты, диалоги, персонажей, документы и logical asset/text refs; compiled `CampaignManifest` выбирает их состав и порядок. `RuntimeKernel` остаётся единственным state/transaction owner, а уникальные механики подключаются exact-version capability providers без сюжетного кода в ядре.

Для обычного side quest добавляется определение в существующий module и при необходимости включается в campaign manifest; после этого запустить `npm run content:check`. Для новой механики добавить isolated provider и его schema/catalog/content declaration, затем покрыть lifecycle/snapshot/cleanup тестом — kernel менять не требуется. Content Lab запускается только через `npm run dev` и `?lab=1`; он создаёт отдельный run/save и не является production entrypoint.

Удалены или изолированы legacy `GameState`/`SceneManager`, старые сцены и TypeScript narrative data, raw old-PC parser/chat/web corpus, SaveSystem/v1 validators и production MainMap registration. Static closure gate запрещает их возврат, прямые asset paths, raw storage, `window.URMAN` и сюжетные IDs в generic owners.

Итоговые доказательства: `content:check` 3 modules/3 campaigns; unit 68 content + 102 runtime; architecture 2/2; persistence 8/8; E2E 10/10 with local loopback; production build 56 modules; `git diff --check` PASS; final independent architecture review PASS without P0/P1/P2. `graphify update .` выполняет extraction, но host watch rebuild остаётся недоступен из-за `Operation not permitted`. Архитектура готова к достройке MVP, но игра не объявляется MVP: текущий P2-риск — restore возвращает сюжетное состояние, но не точную presentation-position.
