# Backlog

## Narrative

- [ ] Task: Написать beat sheet первого дня Айдара в Кырлае.
  Type: Narrative
  Priority: High
  Depends on: MVP scope
  Output: 10–15 beat sequence from arrival to first night.
  Notes: Начать с приезда / дороги / дома, без отдельной КФУ-сцены. Держать Марата как первый крючок.

- [ ] Task: Выбрать конкретный cliffhanger MVP.
  Type: Narrative
  Priority: High
  Depends on: gameplay prototype
  Output: accepted working scene.
  Notes: Accepted: кромка Кара-Урмана → голос Марата → Ринат говорит «Не отвечай». Дальше нужен beat sheet, не новый выбор.

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
  Notes: Проверить, что вариант A работает как in-world navigation: физические указатели, скрытый масштаб деревни, reusable turn/step transitions без fake 3D rotation.

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

- [ ] Task: Сделать prototype old PC document hub.
  Type: Tech
  Priority: High
  Depends on: first Marat documents, document data
  Output: старый ПК с поиском, 3–5 документами, «Татарвики», сохранённым сообщением, metadata clues and one vocabulary re-read.
  Notes: Это центральный production shortcut и основной доступ к документам MVP. Не делать в первом прототипе полноценную ОС или свободное программирование.

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
