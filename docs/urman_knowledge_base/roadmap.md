# Roadmap to MVP

## Phase 0 — Knowledge Base / Preproduction

### Goals

- Зафиксировать canon, MVP, weak points, open questions.
- Свести сюжет, gameplay, татарский язык, ассеты и техническую архитектуру в одну систему.
- Выделить минимальный вертикальный срез.

### Deliverables

- `AGENTS.md`
- `docs/urman_knowledge_base/`
- Mermaid mindmap
- MVP scope
- asset inventory
- technical architecture
- backlog
- next 10 actions

### Risks

- Документация устареет, если её не обновлять вместе с задачами.
- Красивый canon может скрыть нерешённый gameplay.

### Criteria

- Все required docs созданы.
- Открытые вопросы и слабые места явно зафиксированы.
- Первый MVP loop описан достаточно, чтобы прототипировать.

## Phase 1 — Prototype

### Goals

- Проверить minute-to-minute gameplay.
- Доказать dialogue key system.
- Доказать первый татарский language unlock.
- Проверить old PC / archive как главный документальный хаб MVP.

### Deliverables

- 15-минутный greybox loop.
- 1 локационный маршрут: дом → улица / кладбище → архив / ПК → Ринат.
- 3–5 NPC или placeholder speakers.
- 8–12 clues.
- 10+ prototype documents / records across old PC and journal graph.
- Old PC shell implemented with search, «Татарвики», saved messages, gated/corrupted fragment, document-mark clues and local clue saving.
- 5–7 татарских слов.
- Черновой journal / clue graph.

### Risks

- Игроку может быть скучно читать.
- UI может стать сложнее самой игры.
- Язык может не ощущаться нужным.

### Criteria

- Игрок самостоятельно понимает, какой clue применить.
- Хотя бы один NPC меняет реакцию из-за ключа.
- Хотя бы одно татарское слово открывает новый смысл.
- Старый ПК открывает или переосмысляет один критичный clue по Марату.
- Есть первый мистический след без полного показа существа.

### 2026-05-18 Audit Update

Current route navigation and old PC prototypes satisfy parts of Phase 1, but they are still separate loops. Phase 1 is not complete until shared knowledge state connects old PC clues, journal, dialogue reactions, vocabulary re-read, pressure and route/cliffhanger progression. Use `mvp_completion_handoff.md` for the exact P0 sequence and `playtest_plan.md` for smoke/targeted tests.

## Phase 2 — Vertical Slice

### Goals

- Собрать полноценный MVP slice с атмосферой, UI и сценами.
- Утвердить визуальный и звуковой стиль.
- Свести narrative, gameplay и technical data.

### Deliverables

- Полируемая первая глава.
- Дом бабая и әби.
- Главная улица / кладбище / медпункт / мечеть / старый ПК как документальный хаб.
- Алсу, Тимур, Ринат, Наиля, Разиля.
- 20–30 clues.
- 10–15 документов.
- Working vocabulary unlocks.
- First cliffhanger scene.

### Risks

- Ассеты расползутся.
- Слишком много NPC.
- Документы начнут заменять действие.

### Criteria

- Вертикальный срез можно пройти от приезда до клиффхэнгера.
- Игрок понимает Марата как эмоциональный центр.
- Игрок понимает, что татарский язык полезен.
- Игрок получает доказательство старой системы, но не полное объяснение.

### Vertical Slice Gate

Do not call Phase 2 complete until a fresh browser can play from menu to cliffhanger without direct URL jumps, and at least one old PC clue changes journal state, NPC dialogue and vocabulary/re-read state.

## Phase 3 — MVP Content

### Goals

- Довести контент MVP до цельного опыта.
- Убрать лишние ветки.
- Подготовить demo / grant build / playtest build.

### Deliverables

- Финальный текст MVP.
- Финальные MVP-документы.
- Полный journal graph.
- MVP asset pack.
- Audio pass.
- UI polish.
- Playtest сценарии.

### Risks

- Желание добавить Тукая, Баранова, полную романтику и все существа.
- Культурные ошибки без проверки.
- Непонятный финальный cliffhanger.

### Criteria

- Контент не требует знания полной bible.
- Все clues используются.
- Все татарские слова в MVP проверены.
- Концовка MVP ясно меняет жанровую рамку.

## Phase 4 — Polish

### Goals

- Отполировать читаемость, темп, звук, UI и культурную точность.
- Подготовить MVP к внешнему показу.

### Deliverables

- Bugfix pass.
- Copyediting pass.
- Татарский language review.
- Cultural / religious review.
- Performance / save-load check.
- Updated knowledge base.

### Risks

- Полировка начнёт добавлять scope.
- Исправления текста сломают clues.

### Criteria

- MVP проходит без блокеров.
- Journal не содержит broken links.
- Игроки понимают цель, Марата, язык и cliffhanger.
- Knowledge base отражает фактический build.
