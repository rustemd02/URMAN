# Backlog

**Актуальный разбор требований — 28 сентября 2026:** [полный пакет](../tasktracker/review_2026-09-28/README.md),
[покрытие каждой претензии](../tasktracker/review_2026-09-28/coverage.md), единственная очередь — `execution_backlog.json`.
Нынешний этап документальный, дальнейший игровой запуск остаётся на авторской паузе. Предыдущие срезы ниже — история;
совместимые требования Акта I, EX00–EX14 и 60 активных минут сохраняются. Тамару не возобновлять.

Current production baseline, 2026-08-10: Godot 4.7.1 .NET, C#/.NET 10, Painterly Low-Poly 3D and walkable first-person compact zones. Tasks that extend the old discrete route runtime, replace 3D scenes with static ink-wash screens or validate fixed 90-degree navigation are historical and must not be executed.

Execution queue: `execution_backlog.json` is the machine-readable source for orchestration. This file remains the human-readable index and historical audit; do not create a second task owner in an external tracker. 2026-09-03: the active task queue for the Act I repo-grounded run is `../production/act1_repo_grounded_production_tracker.md`; `../archive_tz_mvp_full_tracker_2026-09-03.md` is retired as authority and kept as provenance only (its verification commands are fictional and must never be executed or cited as evidence). Current focus: `GODOT-005` (dedicated Act 1 demo entrypoint) alongside the still-open `GODOT-003` style gate; `ASSET-006` (host-independent provenance preflight) is completed; `NARR-001` (Acts 2–5 narrative lock) completed 2026-08-11, while `NARR-002` remains deferred from the current demo and blocked on external cultural review.

**2026-10-04 — фактическая сверка.** `docs/production/act1_repo_grounded_production_tracker.md` в репозитории отсутствует: ни создания, ни удаления в истории git нет. Этот документ и launch-промпт ниже — исторический срез, а не действующее поручение (см. `docs/tasktracker/06_small_model_execution_2026-09-22.md`: «не запускать новую реализацию из исторического launch prompt»). Живая очередь и статусы — только в `docs/urman_knowledge_base/execution_backlog.json`.

## Current scope: connected greybox of Act 1 — 2026-08-15

- [x] Dedicated default entrypoint: `game/scenes/act1_demo.tscn`.
- [x] First-person arrival overlay and compact route through `village_day`,
  `house_old_pc`, `fap_clinic`, `zirat_road` and `kara_urman_night`.
- [x] Shared runtime state carries the 16 authored beats, old-PC documents,
  journal, dialogue keys, vocabulary re-read and ordered final audio cues.
- [x] Physical first-person corridor: camera ray + mapped input now traverses
  arrival → old PC → ФАП evidence → Rinat dialogue → saved message / Татарвики
  reread / edge sketch → zirat → Kara-Urman, with shared `DocumentUi` handoffs.
- [x] Technical cliffhanger presentation: `НЕ ОТВЕЧАЙ` / `Конец демо`.
- [x] Manual-playtest handoff: `../archive/act1_demo_handoff.md` records controls, route,
  current artifacts, known issues and the explicit Acts 2–5 boundary.
- [ ] Unified Act I territory: keep arrival, road, yard/house, village street,
  FAP, return road, zirat and Kara-Urman approach in one connected world; the
  current one-zone `Main.SwitchZone` path is only a technical prototype.
- [ ] First-person continuity evidence: fresh 360°/near-mid/far frames and a
  short physical pass proving that the benchmark scenes no longer present as
  isolated rooms or empty backsides.
- [x] Bounded day-street presentation pass: project-original `WellA_` and
  `WoodpileA_` modules are visible through `AttachPresentationOnly`; hidden
  helper boxes remain the collision owners and smoke checks require zero
  imported physics descendants.
- [ ] Human-facing gates after the map: authored voice/ambience mix, cultural
  review, observed first-time playtest, authored geometry, near/mid/far
  readability, M1/Windows performance and visual art lock.

Acts 2–5 production, the 6–8-hour full-game acceptance path and web-runtime
retirement are deferred long-term work. They remain documented and testable in
separate full-game scenes, but are deliberately absent from the demo launch
path; no browser saves or legacy source are removed.

## Deferred Full Godot Migration (north star; not current delivery)

- [x] Task: Capture web golden baseline and durable migration work record.
  Output: accepted migration plan, TaskStartSnapshot, content/runtime/architecture/E2E/build evidence.
- [x] Task: Lock engine, visual style, full-game arc and retirement boundaries in the knowledge base.
  Output: Godot/C# ownership, Painterly Low-Poly budgets, five acts and one tragic ending.
- [x] Task: Install and pin Godot 4.7.1 .NET, .NET 10 and Blender 4.5 LTS.
  Output: reproducible macOS bootstrap and recorded versions.
- [x] Task: Create `Urman.Core`, `Urman.Content`, `Urman.ContentCli` and `Urman.Godot` owners.
  Output: buildable solution with architecture tests blocking Godot dependencies from Core/Content.
- [x] Task: Reach compiler and runtime golden parity for Chapter 1.
  Output: matching IDs, diagnostics, fingerprint, commands/events, snapshot outcomes and reveal timing.
- [ ] Task: Build Chapter 1 Godot vertical slice and three art benchmarks.
  Output: playable arrival-to-cliffhanger slice with accepted in-engine captures.
Status: authored arrival-to-cliffhanger path passes through five walkable Godot zones; shared journal, settings UI, portable keyboard/gamepad button/axis remapping, dynamic keyboard/gamepad hints and the SaveGameV3-backed accessibility contract are integrated and smoke-tested. The deterministic four-stem ambience manifest, zone-aware `AmbientAudioDirector` and headless resource/switching smoke are now integrated; final playback mix, voice recording and captions still require production review. The final forest entry now presents Marat's logical cue before Rinat's warning instead of overwriting the first subtitle in the same frame; the closing card waits for that presentation-only queue. The latest Godot evidence exercises the imported project-original `HouseA`/`PineA` modules, the 27-cell near/mid/far × FOV spatial sweep, the visual-preserving 216-sample temporal/head-bob sweep, the isolated lighting/fog A/B promoted to a bounded Chapter 1 benchmark baseline, and the authored road-relief mesh/collision pass; technical evidence is recorded, while observed comfort, in-engine art acceptance, external accessibility review and authored audio production remain open.
- [ ] Task: Lock and produce Acts 2–5.
  Output: complete 6–8-hour playable game and consultant-reviewed language/cultural content.
  Status: narrative package is locked in `narrative_lock_acts_2_5.md`; compiled 46-beat campaign, 12 walkable zones, one authored Godot `PackedScene` per zone, zone-specific `FullGameZoneDressing`, selective environment `.glb` dressing (including `HouseA_`/`PineA_` in the three style captures), a project-original nine-prefix character `.glb` kit with 143 deterministic LOD meshes, face landmarks/layered clothing, nine Blender-authored `Idle`/`Tension` clips, Godot `AnimationPlayer` playback/switching, the authored road-relief path and four physical document interactions feeding the shared journal are now smoke-tested. The four-stem ambience manifest and zone director are technically wired but explicitly not final authored sound. A dedicated collision QA smoke proves queryable floors, authored path cells and layer-2 proxy isolation across all 12 zones; facial expression polish, authored voice/ambience mix, final mesh-dressing review, LOD/performance on release hardware and cultural lock remain open.
- [ ] Task: Run desktop/full-playthrough gate and retire web production owners.
  Output: macOS/Windows evidence and absence-gated TypeScript/Vite/Three.js runtime retirement.
Status: current 2026-08-15 debug packages are structurally verified (macOS ZIP `e3817674…58b7751`, Windows ZIP `b93fcc10…d8ea56c`, Windows PE32+ `ca2ccde0…82440e`) and the elevated current-macOS host smoke passes with the Act 1 demo default launch. The startup `medium` graphics profile is now applied before the first frame (0.90 scale / 2× MSAA), with full Godot smoke, the read-only journal objective and first Tatar vocabulary projection, the `[J]/[Y]` save-status hint, ordered Marat/Rinat cue presentation, destination-facing Act 1 zone spawns and local entrypoint/package performance evidence (`119.82 FPS` medium / `120.04 FPS` safe on the latest serialized M4 Pro package probe). The canonical source launch is now `./eng/run-act1-demo.sh`, which sources the pinned .NET environment; `./eng/run-act1-demo-safe.sh` remains the Mobile/low diagnostic path. The hardened exporter uses fresh staging, lock serialization, clean Windows ZIP metadata and an atomic artifact receipt. Windows-host execution, M1/Windows performance, authored audio, cultural/accessibility review, full playthrough and final web absence gate remain open. `./eng/check-web-retirement.sh --report` is available and currently finds 13 expected legacy/runtime-persistence findings; `--assert-absent` remains intentionally failing until acceptance and never deletes browser data. The bounded status is recorded in `release_gate_matrix.md`.

## Historical MVP Completion Audit — P0 Integration

Status: superseded execution list. Preserve as audit evidence only; do not execute its legacy file/owner instructions.

- [ ] Task: Выполнить `../archive/mvp_completion_handoff.md` как главный P0-план до заявления "MVP готов".
  Type: Production / Integration
  Priority: Critical
  Depends on: current old PC and route prototypes
  Output: playable arrival-to-cliffhanger vertical slice with shared evidence state, journal, dialogue key, vocabulary re-read, pressure and save/load.
  Notes: Не начинать с новых ассетов или новой лорной ветки. Главный blocker: old PC, journal, vocabulary, dialogue, pressure and save/load живут раздельно.

- [ ] Task: Создать shared knowledge source of truth.
  Type: Tech / Gameplay
  Priority: Critical
  Depends on: `technical_architecture.md`, `../archive/mvp_completion_handoff.md`
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
  Notes: First-person 3D: дом → ходибельный участок улицы → кладбище → архив/ПК → Ринат → audio-first след.

- [ ] Task: Прототипировать first-person 3D village greybox.
  Type: Gameplay
  Priority: High
  Depends on: engine decision, first-person controller baseline
  Output: one compact walkable zone with movement, look, collision, three landmarks, one interactable and one location transition.
  Notes: Start with grey geometry. Old route PNGs/graph may inform composition and landmarks but must not become a fallback runtime.

- [x] Task: Выбрать движок и утвердить first-person technical slice.
  Type: Tech / Architecture
  Priority: High
  Depends on: accepted walkable first-person 3D presentation
  Output: Godot 4.7.1 .NET/C# decision plus controller/world/presentation ownership contract.
  Notes: Accepted 2026-08-10; see `technical_architecture.md` and `decision_log.md`.

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

- [x] Epic: модульная миграция выполнена (цепочка MM-00–MM-80, удалена при чистке 2026-09-18; см. git-историю).
  Type: Tech / Architecture / Production
  Priority: Critical
  Depends on: architecture freeze `MM-03`, current green content/build baseline
  Output: portable campaign modules, transactional runtime kernel, capability providers, Content Lab и удалённые legacy owners.
  Notes: Принято 2026-07-18 после final verification. Chapter 1 остаётся заменяемым production baseline; архитектурная готовность не означает MVP release.

- [x] Task: Заморозить portable content, runtime и snapshot contracts.
  Type: Tech / Architecture
  Priority: Critical
  Depends on: stress matrix
  Output: frozen ID/version, `CompiledContentPack`, `RuntimeContext`, capability lifecycle и historical `GameSnapshotV2` contracts; current desktop persistence is `SaveGameV3`.
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
  Notes: Historical prototype completed in 2026-05; superseded as production direction on 2026-08-10. Preserve only as provenance/reference and do not extend it as fallback.

- [ ] Task: Провести playtest first-person greybox на ориентацию и комфорт.
  Type: Gameplay / UX
  Priority: High
  Depends on: first-person 3D village greybox
  Output: notes on movement, look, FOV, motion comfort, landmark readability, interaction reach and natural boundaries.
  Notes: Проверить путь main street → Mansur house → crossroad → FAP/mosque/zirat и помогает ли журнал без превращения в full top-down map.

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
  Notes: Full-game physical documents now use the shared `RuntimeBridge`/`runtime.journal` path. The old-PC capability still keeps its explicit save action and requires a separate pass to expose every saved clue as a dialogue topic without expanding the archive hub.

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
  Depends on: first-person 3D presentation; proposed polished stylized low-poly direction
  Output: one in-engine walkable style frame plus old PC hero prop/UI test.
  Notes: Сравнить primitive PS1, polished stylized low-poly и painterly high-detail low-poly на одной сцене; рекомендация — средний вариант. Старые ink-wash изображения использовать как палитру, texture/reference source и UI material, а не как готовый 3D-мир.

- [x] Task: Создать и проверить Painterly Low-Poly texture candidates.
  Type: Assets / QA
  Priority: High
  Depends on: three in-engine style benchmarks
  Output: six non-destructive versioned `*_v2`/`*_v3`/`*_v4`/`*_v5_albedo.png` siblings, exact ImageGen prompts/provenance, deterministic seam/format gates and Godot candidate captures.
  Notes: v2 and focused v3 candidates pass their image gates and test-only scene sweeps. v4 earth/wood is explicitly HOLD/REWORK because drawn ruts and dark board seams duplicate authored relief and shared-owner striping. v5 reworks only damp earth and weathered wood; both pass the technical 1024² RGB/sRGB gate, a clean v3↔v5 Metal/Forward+ A/B receipt (9 sheets, 120/120 isolated relief/contact samples) and a separate 27-cell near/mid/far × FOV 65°/75°/90° sweep, but the earth relief-only cells do not yet show a reliable rendered delta and both remain production OPEN. Weathered wood, aged plaster, damp earth and pine foliage retain the v1 runtime owners; stone/fabric keep bounded presentation owners. Runtime v1 files, shader, narrative state and saves were not changed; visual material separation, near/far motion readability, 20–30 m tiling, geometry, lighting, cultural review and art lock remain open. See `docs/urman_knowledge_base/art/texture_candidates_{generation,technical,qa}.md`, `docs/urman_knowledge_base/art/texture_candidates_v5_technical.md`, `docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v5/`, `docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v5/` and `assets/asset_registry.json`.

- [x] Task: Проверить host-independent asset provenance.
  Type: Build / Assets / QA
  Priority: High
  Depends on: modular environment and character kits
  Output: `eng/verify-asset-registry.sh` preflight plus typed Blender host status.
  Notes: 33 derived files and 7 explicit local sources hash-match; duplicate, missing and hash-mismatch fixtures fail closed. `verify-assets.sh` now reports sandbox Blender SIGSEGV as `HOST_TOOLCHAIN_BLOCKED` without fallback; elevated real-driver verification passes, while release-host revalidation remains open.

- [ ] Task: Подготовить document asset templates.
  Type: Assets
  Priority: Medium
  Depends on: document viewer
  Output: медсправка, архивная карточка, чат, «Татарвики».
  Notes: Должно быть читаемо.

## Audio

Production boundary evidence, 2026-08-11: `Urman.ContentCli report --root .` now distinguishes the technical four-stem ambient pass from authored voice. It reports 8/8 campaign-level caption and transcript closures and 4/4 physical ambient files, while keeping the 8 logical voice references and final mix explicitly `OPEN`; this does not mark the authored audio tasks complete.

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

Texture candidate addendum, 2026-08-14: v6 damp-earth and weathered-wood
siblings now pass the deterministic image gate (2/2 explicitly; 18/18 across
v2–v6), have an isolated v5↔v6 Godot A/B receipt (9 sheets, 120/120 contact)
and a dedicated 27-cell motion receipt per mandatory scene. They remain
production OPEN: earth relief-only separation is subtle, wood owner/orientation
needs review, and runtime v1 mappings remain active. See
`docs/urman_knowledge_base/art/texture_candidates_v6_technical.md`,
`art/texture_candidate_ab_diagnostic_v6/` and
`art/texture_candidate_motion_sweep_v6/`.

Material owner calibration addendum, 2026-08-14: imported HouseA/FenceA/
TableA/PineA meshes now use semantic `wood_facade`, `wood_fence`,
`wood_furniture` and `wood_bark` descriptors with separate scales, while the
character kit's `cloth` metadata owner resolves `old_fabric_v2` explicitly.
Full Godot smoke, static/spatial/temporal captures and the local M4 benchmark
pass; this remains a bounded presentation integration and does not close
end-grain/orientation, clothing readability, cultural review or art lock.

PineA collision-owner hardening addendum, 2026-08-14: the style Kara-Urman
benchmark now removes imported GLB collision descendants before instantiation
enters the scene tree. `SceneSmokeTest` proves one layer-2 proxy for the
original Pine and zero physics descendants for both presentation variants.
Fresh style, 27-cell spatial, 216-sample temporal and M4 benchmark reruns pass
on the local Metal/Forward+ host. This is still presentation evidence: one
PineA family, procedural-cone repetition, greybox geometry, external comfort,
M1/Windows performance and art lock remain open.

Wetness matrix v2 addendum, 2026-08-14: a production geometry micro-slice seats
only the eight measured outlier patches on their own road/path relief cells;
all 15 raw production origins now pass the strict 5 mm exact-owner gate without
candidate-local alignment. The isolated Metal/Forward+ harness still compares
roughness `0.40/0.50/0.60` with 45 in-memory clones and nine PNGs. Day and
Zirat still read as flat plates and Kara wetness is barely legible, so this is
a technical comparison receipt only; no runtime roughness or art lock is
accepted.

Authored module task addendum, 2026-08-14:

- [ ] Task: Подключить Blender-authored WellA, WoodpileA и GateA к village
  dressing.
  Type: Assets / Godot / QA
  Priority: High
  Depends on: modular environment kit, host-independent registry, HouseA
  epilogue slice
  Output: 49-mesh LOD1 kit (including OldPc DriveSlot/LabelPlate), registry
  records, Act 2 yard and Act 5 threshold presentation, fresh full-game and
  OldPc close captures.
  Notes: decorative only; no new collision authority; GateA не использовать
  в HouseA-эпилоге. Near/mid/far traversal, repetition, marker readability,
  cultural review and release-host performance remain production gates.

  Contract follow-up: `GeneratedModularKitContractSmokeTest` is included in
  the Godot verifier for all nine exact published families. `FenceA` is
  presentation-only in the registry because the GLB has no `FenceA-col`.

Epilogue presentation addendum, 2026-08-14: the canonical Act 5 epilogue now
uses the project-original `HouseA_` module under `act5-epilogue-house`; the
full-game smoke asserts the `wood_facade` owner and the fresh capture set has
seven frames. This is a bounded scene-wiring slice, not completion of ASSET-004
or production art lock.

Temporal evidence repair and calibration addendum, 2026-08-14: the temporal
render-only clone now preserves visual descendants under `CollisionObject3D`,
zeros collision layers/masks and removes only `CollisionShape3D` nodes. The
previous sparse receipt is retained under `superseded_pre_visual_sanitization/`
and is not a GODOT-003 gate. A separate `StyleCalibrationCandidateCapture`
clones `WorldEnvironment` and overrides only presentation lights/fog for six
Metal/Forward+ A/B frames; both receipts are technical candidates and leave
visual, comfort and art-lock review open.

Act 1 performance addendum, 2026-08-15: the dedicated demo entrypoint now
has a fail-soft startup guard. Several consistently heavy post-warm-up frames
switch only the current session to the existing low material/render-scale
profile; `--print-fps --no-auto-performance-fallback` records target-host
diagnostics without the rescue. Fresh package probes remain healthy on the
local M4 Pro (`119.94 FPS` medium Forward+, `120.05 FPS` safe Mobile/low), so
M1/Windows and editor-vs-package reproduction remain open.

Fresh export refresh, 2026-08-15: after the ordered cliffhanger cue
presentation and AudioCueUi cleanup, current package hashes are macOS
`a66f1d0cea0af2cc41a9f48ab63df1c434daafffcc4f6defe23a0a12b50b2ead`, Windows
ZIP `122bbc6163ae2ca3f17a3532f8fd0aff15d8f1ff3347e52e7d2eb7002f424c6b`, and
Windows EXE `4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`;
read-only desktop receipt verification passes with manifest
`d8014f498aa698d5d85bf02db6c3ca7d332bb2744260aad06810691583ec82a7`. These
are structural package evidence only; target-host performance remains open.


## 2026-10-02 — визуальное ревью W

Новая реализация и свежая проверка уютной деревни ведутся в существующих ACT1-VILLAGE-LAYOUT/COMPOSITION/MOSQUE; новая очередь не создаётся. Полная карта причин/изменений — `../production/village_relayout_2026-10-02/audit.md`. Художественная приёмка и слышимый азан открыты.


### 04.10.2026 — авторское поручение: жилые окна и бытовой звук

Реализованы shared жилые окна и 23 proximity-события с постоянными пулами
света/голосов и ограниченным аудиокэшем. Сборка/целевые проверки и внешние
ограничения — [бытовой слой](../production/village_life_2026-10-04.md).
Художественную приёмку/прослушивание и общий RAM/FPS не закрывать по бюджету кэша.


### 04.10.2026 — дворы, ночь и деревянная мечеть

В существующих ACT1-VILLAGE-COMPOSITION / ACT1-NPC.5 / ACT1-MOSQUE записано новое авторское поручение и его локальная реализация. Пять дополнительных фотоисточников, очистка дворов, расписание присутствия и физика мечети — [отчёт](../production/village_repair_2026-10-04/report.md). Общую художественную/культурную приёмку и полный часовой опыт по узким PASS не закрывать.


2026-10-04: прямое дополнение автора к ACT1-PUBLIC — существующий far-police, приёмная/коридор/пустой изолятор, сидящий фоновый дежурный, неподвижные Жигули «УЧАСТКОВЫЙ». Текущий результат и проверки — `docs/production/police_post_2026-10-04/report.md`; публикация и весь Акт I не принимаются этой локальной задачей.
