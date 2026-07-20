# Backlog

## MVP Completion Audit — P0 Integration

- [ ] Task: Выполнить `mvp_completion_handoff.md` как главный P0-план до заявления "MVP готов".
  Type: Production / Integration
  Priority: Critical
  Depends on: current old PC and route prototypes
  Output: playable arrival-to-cliffhanger vertical slice with shared evidence state, journal, dialogue key, vocabulary re-read, pressure and save/load.
  Notes: Не начинать с новых ассетов или новой лорной ветки. Главный blocker: old PC, journal, vocabulary, dialogue, pressure and save/load живут раздельно.

- [ ] Task: Создать shared knowledge source of truth.
  Type: Tech / Gameplay
  Priority: Critical
  Depends on: `technical_architecture.md`, `mvp_completion_handoff.md`
  Output: `KnowledgeKey`, `VocabularyEntry`, dialogue, quest and village pressure data that old PC, journal, dialogue, route and save/load can all consume.
  Notes: Начать с TS data modules: `src/data/knowledge_keys.ts`, `src/data/vocabulary_data.ts`, `src/data/dialogue_data.ts`, `src/data/quests.ts`, plus shared state in `GameState`.

- [ ] Task: Связать old PC clues с общим journal/dialogue graph.
  Type: Tech / Gameplay
  Priority: Critical
  Depends on: shared knowledge source of truth
  Output: opening/saving old PC documents adds shared keys, vocabulary, contradictions and pressure effects.
  Notes: Сейчас old PC loop локальный. MVP начинается только когда `clue_marat_official_death_version` changes journal and Rinat dialogue.

- [ ] Task: Реализовать investigation journal вместо vocabulary-only notebook.
  Type: UI / Gameplay
  Priority: Critical
  Depends on: shared knowledge keys
  Output: journal tabs for clues, contradictions, Marat timeline, vocabulary and route sketch support.
  Notes: `NotebookUI` currently shows only татарский vocabulary.

- [ ] Task: Реализовать first dialogue-key prototype with Ринат.
  Type: Gameplay / UI
  Priority: Critical
  Depends on: shared knowledge keys, journal
  Output: `DialogueSystem`, `DialogueUI`, `dialogue_data.ts` and a Rinat topic where clues change reaction level.
  Notes: NPC must lie differently, fear, or partially admit; no exposition dump.

- [ ] Task: Закрыть runtime canon/style drift.
  Type: Narrative / Tech
  Priority: High
  Depends on: canon.md, decision_log.md
  Output: active runtime text no longer treats Кара-Урман as village, Алсу 1926 as plain truth, or Шүрәле as generic monster.
  Notes: Check `characters.ts`, `chat_data.ts`, `MainMenuScene.ts`, `IntroScene.ts`, `HUD.ts`.

- [ ] Task: Replace MVP scene stubs with existing ink-wash assets.
  Type: Tech / Assets / Narrative
  Priority: High
  Depends on: `public/assets/urman_mvp_remaining/`
  Output: real house, mosque and forest/cliffhanger scenes using existing visual pack.
  Notes: `HouseScene` and `ForestScene` are stubs; `MosqueScene` uses an Al-Aqsa 3D placeholder that does not fit MVP.

- [ ] Task: Add route and clue graph validators.
  Type: Tech / QA
  Priority: High
  Depends on: shared data files
  Output: `scripts/validate-clue-graph.mjs`, `scripts/validate-route-graph.mjs`, package scripts and release gate.
  Notes: Keep old PC validator; expand cross-reference validation instead of replacing the authoring model.

- [ ] Task: Run internal smoke and targeted playtests from `playtest_plan.md`.
  Type: QA / Production
  Priority: High
  Depends on: first connected loop
  Output: recorded route, old PC, narrative, language and cliffhanger findings with pass/fail thresholds.
  Notes: Full external first-time MVP playtest is premature until shared journal/dialogue graph and final cliffhanger exist.

## Narrative

- [x] Task: Написать beat sheet первого дня Айдара в Кырлае.
  Type: Narrative
  Priority: High
  Depends on: MVP scope
  Output: 10–15 beat sequence from arrival to first night.
  Notes: Completed 2026-05-23 as `chapter1_mvp_campaign.md`: 40–60-minute chapter spine from road / home to «Не отвечай», with scene cards, clue graph and red-team fixes.

- [x] Task: Выбрать конкретный cliffhanger MVP.
  Type: Narrative
  Priority: High
  Depends on: gameplay prototype
  Output: accepted working scene.
  Notes: Completed 2026-05-23 in `chapter1_mvp_campaign.md`. Keep final as audio-first: voice of Marat, Aidar almost answers, Rinat says «Не отвечай», hard cut; no full creature reveal or explanatory monologue.

- [ ] Task: Написать пакет документов о Марате.
  Type: Narrative
  Priority: High
  Depends on: clue graph schema
  Output: могила, медсправка, сообщение, дневник, архивная запись.
  Notes: Все документы должны давать playable clues.

- [ ] Task: Решить статус линии вырубки.
  Type: Narrative
  Priority: High
  Depends on: MVP focus
  Output: accepted/rejected/proposed decision.
  Notes: Не позволить ей вытеснить Марата.

## Gameplay

- [ ] Task: Прототипировать 15-минутный investigation loop.
  Type: Gameplay
  Priority: High
  Depends on: first clues and NPCs
  Output: playable or paper prototype.
  Notes: Static-node hybrid + route navigation: дом → маршрутная улица с 90-degree turn → кладбище → архив/ПК → Ринат → audio-first след.

- [ ] Task: Прототипировать village route navigation.
  Type: Gameplay
  Priority: High
  Depends on: accepted ink-wash route style
  Output: 3–4 connected route segments with forward, left/right turn, inspect and journal sketch update.
  Notes: Content side is ready in `public/assets/urman_route_map/`: 41 PNG, 32 route nodes, 10 route-node state transitions, 1 journal support transition, controlled animation and editable text metadata. Remaining work is runtime integration / playability test: consume `route_graph.json`, render turn/step transitions, hotspots and journal updates.

- [ ] Task: Описать reaction levels для NPC.
  Type: Gameplay
  Priority: High
  Depends on: dialogue key system
  Output: rules for lie / deflect / partial truth / fear / notify.
  Notes: Начать с Рината.

- [ ] Task: Спроектировать village pressure.
  Type: Gameplay
  Priority: Medium
  Depends on: suspicion rules
  Output: simple MVP meter or flags.
  Notes: Scripted flags + 0–3 pressure level. Деревня должна реагировать, но не душить игрока.

## Tech

- [x] Epic: Выполнить модульную миграцию по `docs/modular_migration/`.
  Type: Tech / Architecture / Production
  Priority: Critical
  Depends on: architecture freeze `MM-03`, current green content/build baseline
  Output: portable campaign modules, transactional runtime kernel, capability providers, Content Lab и удалённые legacy owners.
  Notes: Принято 2026-07-18 после final verification. Chapter 1 остаётся заменяемым production baseline; архитектурная готовность не означает MVP release.

- [x] Task: Заморозить portable content, runtime и snapshot contracts.
  Type: Tech / Architecture
  Priority: Critical
  Depends on: `docs/modular_migration/01_ARCHITECTURE_CONTRACT.md`, stress matrix
  Output: frozen ID/version, `CompiledContentPack`, `RuntimeContext`, capability lifecycle и `GameSnapshotV2` contracts.
  Notes: Кодовая миграция не начинается, пока 12 stress fixtures не пройдут review gate `MM-03`; capability teardown допускается только после immutable kernel claim query подтверждает owner release.

- [x] Task: Перенести campaign, active runtime, old PC/DedOS и MainMap отдельными leaf-пакетами.
  Type: Tech / Migration
  Priority: Critical
  Depends on: foundation canary `MM-45`
  Output: четыре изолированных handoff для единого composition-root cutover `MM-54`.
  Notes: Параллельная работа разрешена только в отдельных worktree и без совместного владения `Game`, `GameState`, `SceneManager` или `main`.

- [x] Task: Закрыть migration gates и удалить legacy owners.
  Type: Tech / QA / Retirement
  Priority: Critical
  Depends on: composition root `MM-54`
  Output: whole-pack closure, Content Lab, clean save reset, static architecture checks и итоговый KB handoff.
  Notes: Permanent adapters, duplicate state owners, raw gameplay `localStorage`, production `window.URMAN` и regex validators не остаются.

- [ ] Task: Создать data schema для characters, clues, documents, dialogues, vocabulary.
  Type: Tech
  Priority: High
  Depends on: technical_architecture.md
  Output: JSON schema or TypeScript types.
  Notes: Engine-neutral.

- [ ] Task: Создать clue graph validator.
  Type: Tech
  Priority: High
  Depends on: data schema
  Output: script/check for broken links and orphan clues.
  Notes: Проверять source, reveals, unlocks.

- [x] Task: Сделать prototype old PC document hub.
  Type: Tech
  Priority: High
  Depends on: first Marat documents, document data
  Output: старый ПК с поиском, 10 валидируемыми content files, «Татарвики», сохранёнными сообщениями, gated/corrupted fragments, подсказанными терминами and local clue saving.
  Notes: Implemented 2026-05-18 in `src/os/apps/oldPcHub.ts`, `src/os/data/oldPcContent.ts`, `content/old_pc/` and `HouseScene`. Remaining work: connect saved clues to full journal/dialogue key graph and route navigation runtime.

- [x] Task: Интегрировать route navigation graph в runtime.
  Type: Tech / Gameplay
  Priority: High
  Depends on: `public/assets/urman_route_map/route_graph.json`
  Output: playable node navigation with forward, back, 90-degree left/right turns, inspect hotspots, state transitions and journal sketch updates.
  Notes: First playable runtime integration exists in `src/scenes/RouteNavigationScene.ts`; `village` now opens it and old `MainMapScene` remains as `villageGreybox`. Build passed. Remaining follow-up: replace placeholder external handoffs with real location scenes and playtest orientation clarity.

- [ ] Task: Провести playtest route navigation на ориентацию.
  Type: Gameplay / UX
  Priority: High
  Depends on: runtime route navigation scene
  Output: notes on whether the player understands forward/back/90-degree turns, diegetic signs, journal sketch updates and external handoffs.
  Notes: Проверить особенно main street -> Mansur turn -> crossroad -> FAP/mosque/zirat and whether the journal helps without becoming a full top-down map.

- [x] Task: Создать old PC content validator.
  Type: Tech / Narrative Tools
  Priority: High
  Depends on: `content/old_pc/README.md`, old PC authoring schema
  Output: проверка required fields, unique IDs, broken links, invalid reliability/canon statuses, orphan clues and search terms.
  Notes: Implemented as `scripts/validate-old-pc-content.mjs`. Current validator covers required fields, unique IDs, allowed statuses, basic arrays, body presence and `requires` links. Orphan clues/search terms should move to the shared clue graph validator.

- [ ] Task: Интегрировать saved clues из ПК в journal/dialogue key graph.
  Type: Tech / Gameplay
  Priority: High
  Depends on: old PC prototype, journal UI, clue graph schema
  Output: документы ПК создают reusable knowledge keys, меняют журнал и дают проверяемые dialogue topics.
  Notes: Сейчас ПК сохраняет улики локально внутри archive hub. Это хорошо для MVP-прототипа, но не заменяет общую систему evidence inventory.

- [ ] Task: Сделать save/load model.
  Type: Tech
  Priority: Medium
  Depends on: state model
  Output: spec + minimal implementation.
  Notes: Clues, vocabulary, NPC state, village state.

## UI

- [ ] Task: Спроектировать journal UI.
  Type: UI
  Priority: High
  Depends on: clue graph
  Output: wireframe / prototype.
  Notes: Факты, противоречия, timeline, словарь.

- [ ] Task: Спроектировать dialogue key picker.
  Type: UI
  Priority: High
  Depends on: knowledge key data
  Output: simple UI for applying keys.
  Notes: Не перегружать списками.

- [ ] Task: Спроектировать vocabulary UI.
  Type: UI
  Priority: High
  Depends on: language prototype
  Output: known / guessed / confirmed word states.
  Notes: Словарь должен вести обратно к уликам.

## Assets

- [ ] Task: Утвердить MVP asset floor.
  Type: Assets
  Priority: High
  Depends on: design_style.md
  Output: locked minimum asset list.
  Notes: Отдельно отметить placeholders.

- [x] Task: Довести remaining MVP visual asset pack до полного покрытия inventory.
  Type: Assets
  Priority: High
  Depends on: `asset_inventory_50.md`, first10 asset pack
  Output: `public/assets/urman_mvp_remaining/` + manifest/animation/editable metadata.
  Notes: Visual coverage generated and validated: 179 PNG, 179 manifest entries, 179 animation entries, 92 editable entries. #50 remains partial only for authored audio/voice production.

- [ ] Task: Добавлять animation slots ко всем новым локациям, UI и документам.
  Type: Assets / Tech
  Priority: High
  Depends on: controlled animation spec
  Output: overlay-slot metadata for each generated asset.
  Notes: Не делать baked GIF/video по умолчанию. Для каждого ассета отмечать зоны под light pulse, CRT glow, birds/dust, grass/branch drift, document highlight and pressure overlay where relevant. First10 and remaining visual pack have `animation_layers.json`.

- [ ] Task: Сделать key art / style frame для дома, улицы и старого ПК.
  Type: Assets
  Priority: High
  Depends on: accepted ink-wash storybook direction
  Output: 3 style frames.
  Notes: Делать в стиле тушь + приглушённая акварель: дом бабая и әби, old PC, кромка Кара-Урмана, route segment главной улицы. Проверить воспроизводимость, не уходить в гиперреализм. First generated batch exists in `public/assets/urman_mvp_first10/`: Айдар portrait set, main street route screen, old PC frame, Marat medical record template, Kara-Urman forest edge pressure screen. Дом бабая и әби still needs a polished style frame.

- [ ] Task: Подготовить document asset templates.
  Type: Assets
  Priority: Medium
  Depends on: document viewer
  Output: медсправка, архивная карточка, чат, «Татарвики».
  Notes: Должно быть читаемо.

## Audio

- [ ] Task: Спроектировать audio identity MVP.
  Type: Audio
  Priority: Medium
  Depends on: locations
  Output: list of ambience states.
  Notes: Дом, улица, лес, вода, мечеть, old PC.

- [ ] Task: Сделать audio-first cliffhanger concept.
  Type: Audio
  Priority: Medium
  Depends on: cliffhanger decision
  Output: sound beat script.
  Notes: Кромка Кара-Урмана, исчезающий village ambience, голос Марата, пауза перед ответом, короткое «Не отвечай» от Рината.

## Language Learning

- [ ] Task: Выбрать первые 5–7 татарских слов.
  Type: Language Learning
  Priority: High
  Depends on: MVP scenes
  Output: word list with context and unlocks.
  Notes: Проверить носителем.

- [ ] Task: Прототипировать vocabulary unlock.
  Type: Language Learning
  Priority: High
  Depends on: document viewer
  Output: one re-readable document and one dialogue key.
  Notes: Это must-have для pitch.

- [ ] Task: Найти татарского language consultant.
  Type: Research
  Priority: High
  Depends on: production planning
  Output: review process.
  Notes: Особенно для UI и религиозного/фольклорного словаря.

## Research

- [ ] Task: Проверить культурную рамку Шурале, Су Анасы, Бичуры.
  Type: Research
  Priority: High
  Depends on: mythology draft
  Output: notes and corrections.
  Notes: Не опираться только на общие интернет-образы.

- [ ] Task: Проверить религиозную рамку Тимура.
  Type: Research
  Priority: High
  Depends on: сцены Тимура
  Output: consultant notes.
  Notes: Избежать карикатуры.

## Production

- [ ] Task: Настроить правило обновления knowledge base.
  Type: Production
  Priority: High
  Depends on: AGENTS.md
  Output: PR/checklist rule.
  Notes: Любая крупная задача обновляет docs.

- [ ] Task: Провести первый internal playtest paper prototype.
  Type: Production
  Priority: Medium
  Depends on: prototype loop
  Output: notes on confusion, pacing, language.
  Notes: Проверить, понимает ли игрок, что делать.
