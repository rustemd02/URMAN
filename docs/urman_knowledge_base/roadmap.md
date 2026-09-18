# Roadmap to Full Godot Release

## Current delivery slice — Act 1 atmospheric demo (2026-08-15)

The immediate product target is a playable first-person demo, not a complete
five-act port. `game/scenes/act1_demo.tscn` is the default launch and must carry
the player from arrival through the house/old PC, village, FAP, zirat and
Kara-Urman edge across 16 authored beats, ending on the «НЕ ОТВЕЧАЙ»
cliffhanger. The demo shares the Godot runtime/content/save contracts but keeps
Acts 2–5, the 6–8-hour full playthrough, release-class host gates and web
retirement out of its acceptance boundary. Those remain deferred long-term
milestones, not removed content.

## Deferred full-migration path — retained north star

The numbered path below is not the current execution order. It resumes only
after a separate goal reopens Acts 2–5 and full release work. The current
execution order for this repository is the Act 1 demo queue in
`execution_backlog.json` and `next_10_actions.md`; the manual-playtest handoff
is `../archive/act1_demo_handoff.md`.

## Historical active path — 2026-08-10

1. **Baseline and knowledge lock** — сохранить green web parity oracle; принять Godot/C#, Painterly Low-Poly 3D, пятиактную дугу и retirement boundary.
2. **C# foundation** — создать `Urman.Core`, `Urman.Content`, `Urman.ContentCli`, `Urman.Godot`, architecture tests и `SaveGameV3` contract.
3. **Chapter 1 parity** — перенести compiler/runtime/capabilities и пройти arrival-to-«Не отвечай» без web bridge.
4. **Godot vertical slice** — first-person controller, compact zones, interactions, old PC, journal, dialogue, audio и три style benchmarks.
5. **Acts 2–5 locks and production** — сначала narrative/clue/location/language/threat gate каждого акта, затем его final scenes and assets.
6. **Release gate** — full 6–8-hour playthrough, keyboard/mouse/gamepad parity, cultural review, performance and macOS/Windows exports.
7. **Cutover** — удалить production TypeScript/Vite/Three.js/browser persistence и route fallback; browser localStorage не трогать.

Current production note: the Acts 2–5 foundation now includes an accepted `narrative_lock_acts_2_5.md` package (beat sheets, clue graphs, zones, NPCs, documents, language keys, world variants and threat beats), six walkable zone captures, selective environment `.glb` dressing for the house, Soviet old-PC/table and forest boundary, plus `HouseA_`/`PineA_` imported modules in the three style benchmark compositions. The project-original environment kit now has a bounded HouseA facade pass and a 49-mesh deterministic LOD1 contract including the WellA/WoodpileA/GateA anchors; the nine-prefix character `.glb` kit still has 143 face/clothing-detail LOD pairs, nine Blender-authored `Idle`/`Tension` armatures, Godot `AnimationPlayer` playback/switching, Godot LOD ranges and a collision QA smoke for all 12 zones. Layer-1/layer-2 boundaries are smoke-tested. The SaveGameV3-backed accessibility contract and Godot settings fan-out are also wired and covered by smoke; the deterministic four-stem ambience manifest and zone director now pass Godot headless import/switching smoke, while real playback is reserved for desktop runs. A focused CRT/document/house-prop and imported-module benchmark pass has fresh reproducible frames but remains rejected as final art lock. External motion-comfort/readability and cultural review remain open. This is still a production-progress gate; complete environment family, facial expression polish, authored voice/ambience mix, mesh-collision acceptance and release hardware evidence remain before step 6.

The 2026-08-14 material calibration adds semantic imported owners for facade,
fence, furniture and bark scales, plus an explicit cloth descriptor for the
character kit. This is a bounded presentation integration backed by fresh
Godot smoke, spatial/temporal captures and M4 benchmark; it does not activate
the v6 candidates globally or close the style/art gate.

The current Act 1 prop slice also places the project-original `WellA_` and
`WoodpileA_` modules in the day-street benchmark via presentation-only
adapters. Their hidden helper boxes retain local collision ownership; this
improves the demo composition without expanding Acts 2–5 production or
changing runtime/save/narrative ownership.

Execution note: `docs/urman_knowledge_base/execution_backlog.json` is the resumable queue for an orchestrator, and `docs/archive_aegis/work/2026-08-10-godot-full-migration/10-intent.md` is its synchronized goal frame. `set_goal` remains the durable outcome contract; the queue only schedules bounded work and cannot override canon, decisions or release gates.

Current gate ledger: `release_gate_matrix.md` separates closed technical
foundations from open art, audio, cultural, accessibility, release-hardware and
full-playthrough gates. Fresh desktop package hashes and current macOS host
smoke are evidence only; web retirement remains absence-gated and browser data
is not deleted.

2026-08-14 bounded continuation: `ASSET-006` adds the host-independent asset
registry preflight and typed Blender host status. The current
`StyleBenchmarkZone` ambient/fog/light calibration has fresh static, spatial
and temporal Godot evidence, but the three style benchmarks remain visual
production candidates rather than an art lock. The next owner slice is authored
geometry/material-owner review after release-host Blender revalidation, not a
global texture fallback.

Разделы ниже сохраняют исторический MVP roadmap. Они полезны как критерии качества Chapter 1, но не являются текущим engine execution order.

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

Current route navigation and old PC prototypes satisfy parts of Phase 1, but they are still separate loops. Phase 1 is not complete until shared knowledge state connects old PC clues, journal, dialogue reactions, vocabulary re-read, pressure and route/cliffhanger progression. Use `../archive/mvp_completion_handoff.md` for the exact P0 sequence and `playtest_plan.md` for smoke/targeted tests.

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
