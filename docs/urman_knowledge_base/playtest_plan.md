# MVP Playtest Plan

Status: active Godot first-person test strategy; legacy route checks below are historical subsystem evidence only.

Authority note, 2026-08-10: Godot 4.7.1 .NET, Painterly Low-Poly 3D and keyboard/mouse/gamepad are accepted. Route-node scripts, browser telemetry and direct QA URLs do not validate the target and will retire at final cutover.

Purpose: define how to test УРМАН from prototype pieces to a real MVP. Технический arrival-to-cliffhanger path уже проходит в Godot через пять зон и единое состояние. Не запускать внешний full-slice playtest до art lock, authored audio/presentation pass, accessibility review и записи наблюдаемой сборки; до этого тестировать управление, расследование и comprehension отдельными сессиями.

## Current playtest target — Act 1 demo

The current build under test is the dedicated `res://scenes/act1_demo.tscn`
entrypoint. A first-time playtest should cover only the five-zone Chapter 1
route and its 16 authored beats, ending at the `НЕ ОТВЕЧАЙ` fade-to-black.
Acts 2–5, the full 6–8-hour arc and web retirement are not required to call
this demo playable; they remain separate long-term gates. The demo still needs
observed pacing/comprehension, authored voice/ambience, cultural review and
release-shaped performance evidence before a human-facing acceptance claim.
The manual session checklist and current package hashes are in
`docs/urman_knowledge_base/act1_demo_handoff.md`.

## Current Testability

- [TECH] the six-view connected Act I visual-review harness (./eng/capture-act1-visual-review.sh <output-dir>) is mandatory evidence before visual acceptance: it records six distinct 1280×720 root-viewport PNGs from six separate Godot processes for Kara-Urman forward/back/left/right and zirat forward/back. It does not replace a human first-person walkthrough.

Можно тестировать сейчас на Godot-сборке:

- first-person movement and scale in five compact zones;
- authored arrival-to-cliffhanger progression without web bridge;
- old-PC document chain, dialogue effects, татарский re-read and shared clue/quest state;
- SaveGameV3 roundtrip with zone and player transform;
- keyboard/mouse and basic gamepad bindings, key/button/axis remapping, modal settings UI, FOV/sensitivity/head-bob/graphics application, reduced-motion/high-contrast/text-scale/subtitles/audio-description settings, сохранение настроек и автоматическая смена `[E]`/`[A]` по последнему устройству как internal smoke evidence.
- the Act 1 opening card now mirrors the detected keyboard/gamepad device (WASD/mouse/E/J or left/right stick/A/Y), states the first presentation goal (enter the house and meet бабай and әби), remains modal until the player confirms with the mapped interact action, and does not auto-dismiss; this is still internal discovery evidence, not an observed controller-parity session.
- the journal renders `ТЕКУЩАЯ ЦЕЛЬ` from the active runtime quest as a read-only projection; before both official notice and internal register are found it asks what happened to Marat, and offers their comparison only when the existing JournalActions readiness gate allows it. This should be checked for discoverability and wording during first-time playtest; it is not a GPS marker, a new quest owner or proof that the route is self-explanatory.
- the successful old-PC/physical-document save status now names the same journal shortcut (`[J]` or `[Y]`) that the opening card exposes; verify that this reduces hesitation without making the UI feel like a quest tracker.
- compiled `urman.fullgame` entrypoint, 46 authored beats, 12 provisional zones and physical `InteractionTarget` coverage through the canonical tragic epilogue as internal smoke evidence; `narrative_lock_acts_2_5.md` now fixes the production beat/clue/zone/language/threat package before external playtest.
- zone-specific Acts 2–5 dressing, selective Blender `.glb` house/old-PC variants with explicit LOD ranges and six reproducible representative renders (`./eng/capture-fullgame-frames.sh`) as production-progress evidence; these renders are not final art acceptance.
- project-original character-kit GLB prefixes in the family, river, mosque, council, archive and pact zones, with explicit LOD ranges, face landmarks/layered clothing, Blender-authored `Idle`/`Tension` clips, Godot `AnimationPlayer` playback/switching and `CollisionQaSmokeTest` coverage for queryable layer-1 floors and isolated layer-2 kit proxies across all 12 full-game zones; expression polish and final collision remain production gates.
- deterministic four-stem ambience manifest (`village_day`, `house_old_pc`, `kara_urman_night`, `water_edge`) with zone switching and a presentation-only 0.65-second two-player crossfade covered by `AmbientAudioSmokeTest`; headless checks validate resources without starting playback, while desktop mix, voice and cultural listening review remain open.
- local Apple M4 Pro performance baseline for the three mandatory style scenes (`./eng/benchmark-godot.sh`); this proves the 30 FPS low-preset floor only on the current host, not M1/Windows release performance.
- six non-destructive Painterly texture families with versioned v2/v3/v4/v5/v6 albedo candidates, deterministic PNG/seam checks and test-only in-memory material swaps for the three style scenes (`./eng/capture-texture-candidate-frames.sh`); focused v3↔v5 and v5↔v6 earth/wood A/B harnesses (`./eng/capture-texture-ab-diagnostic.sh`) isolate relief-only versus relief+wetness and record 120/120 contact samples, while the v5/v6 motion wrappers cover 27 near/mid/far × FOV 65°/75°/90° cells per scene. This proves candidate rendering and technical isolation only, not runtime activation, visual earth separation, 20–30 m repetition, temporal comfort or final art lock.
- a test-only Godot camera sweep for the three mandatory style scenes (`./eng/capture-style-motion-sweep.sh`); 27 real Metal/Forward+ cells cover near/mid/far positions × FOV 65°/75°/90° and provide spatial readability evidence. The 2026-08-12 recapture includes the bounded stone/fabric presentation anchors. It is not a temporal movement/head-bob comfort test, global v2 activation proof or final art lock.
- a test-only Godot temporal sweep (`./eng/capture-style-temporal-sweep.sh`); 216 real Metal/Forward+ frame samples cover the three mandatory scenes × FOV 65°/75°/90° × head-bob/reduced-motion modes. This measures the current camera path and proves the reduced-motion vertical component can be removed, but it remains technical evidence rather than an observed motion-comfort or first-time usability review.
- a first-person interaction smoke (`res://tests/first_person_interaction_smoke_test.tscn`, included by `./eng/verify-godot.sh`); the production camera ray hits the authored HouseDoor, mapped gamepad `A` enters the house, the same ray hits the old-PC target, and keyboard `E` opens the modal archive UI. `Act1DemoLaunchSmokeTest` additionally checks opening-card device discovery and mapped gamepad dismissal. These close only local input/ray wiring checks; they do not replace first-time usability, full gamepad parity or external playtest.
- a full first-person corridor smoke (`res://tests/act1_first_person_corridor_smoke_test.tscn`, included by `./eng/verify-godot.sh`); the real demo entrypoint uses physical ray/input targets for the house, old PC, FAP desk, Rinat dialogue, saved message, Татарвики boundary source/reread, edge sketch, zirat and Kara-Urman cliffhanger. Evidence targets open the shared `DocumentUi`, while the Rinat dialogue commits the shared `alerted` state. This closes the internal detective-loop handoff only; observed pacing, human wayfinding, controller parity and cultural review remain open.
- a bounded physical walkthrough smoke (`res://tests/act1_first_person_walkthrough_smoke_test.tscn`, included by `./eng/verify-godot.sh`); it covers exactly 135.49 m of actual `CharacterBody3D` movement through the same Act 1 zones and uses the production camera ray for the dialogue/document gates. It proves the expected unavailable-before/available-after order: Mansur grants old-PC access; the official Marat notice alone leaves the house exit locked; Gulsina/әби's `ярамый` warning unlocks that exit; Alsu confirms the conflicting accounts and unlocks the FAP route; Naila grants medical-record desk access. It never teleports the player or dispatches narrative commands. This is a traversability and gate-order regression, not a first-time player session or art/performance acceptance.
- MAP-002 metric separation (2026-09-03): the route has two deliberately different distance numbers that must never be merged or compared as one measurement. `135,49 м` is the physical gameplay walk — real `CharacterBody3D` movement with ray/input gates, printed by `act1-first-person-walkthrough` with `mode=physical-characterbody-walk`. `242,50 м` is the presentation waypoint audit — straight-line world-space distances between capture-harness waypoints, recorded in the capture receipt's `traversal` block with `mode="world-space presentation waypoint audit; no narrative transition or save mutation"` and printed with `mode=presentation-waypoint-audit`. The waypoint audit is camera-evidence bookkeeping only; it must never be cited as gameplay completion or player route length, and human route validation comes only from observed playtests.
- the journal-flow smoke keeps its assertions on the shared runtime projection; its stale objective/vocabulary oracle has been corrected. `JournalUi` remains a read-only presentation projection and does not own progression.
- the day-street signpost now has a physical `ФАП` label on the board, covered by the scene/style smoke and by the 27-cell near/mid/far sweep. It is a diegetic landmark candidate, not a quest marker; first-time wayfinding still needs observation.

Пока нельзя честно считать внешним full-MVP playtest:

- chapter presentation до art lock и живых NPC/документальных ассетов;
- audio-first cliffhanger до authored voice/ambience playback, captions, non-audio cue integration и cultural listening review; technical stems and zone routing alone не считаются финальным звуком;
- вкладки/граф журнала, сохранение и восстановление remapping клавиш, кнопок и аналоговых осей, а также внешняя accessibility-проверка на реальных пользователях;
- производительность трёх 1080p benchmark scenes на M1/Windows release-class hardware;
- first-time comprehension без прямого наблюдаемого playtest.
- production-grade Acts 2–5 presentation: current full-game zones are navigation/content adapters, not final art-complete locations.

## Playtest Matrix

| Track | Goal | Testers | Build scope | Main risk | Pass condition |
|---|---|---:|---|---|---|
| Internal smoke | Check that the spine does not break technically | 1 dev/QA | `intro`, `village`, `house`, `computer`, `forest` | Blockers, missing assets, impossible transitions | 0 blockers, validators pass |
| First-person movement | Test movement, look, scale and comfort | 6 players | representative 3D village greybox | Motion sickness, poor scale, empty walking | 80% reach three landmarks without help; no severe discomfort reports |
| Old PC investigation | Test search/document loop | 6 players | `?scene=computer` or house-to-PC | PC becomes reading without action | 70% find official doc and contradictory register |
| Narrative comprehension | Test Айдар, Марат, false versions | 5 players | intro + route + old PC | Марат feels like lore, not emotional hook | 80% explain the Marat contradiction |
| Language mechanic | Test татарский as gameplay | 5 players + language reviewer | intro + old PC + journal/vocab | language feels decorative | 60% use a татарский word as search/key |
| Cliffhanger | Test genre shift | 5 players | final route and forest scene | reads as generic monster lure | 80% say "old system/rules", not only "monster" |
| Cultural review | Check татарский, islam, folklore | 1-2 consultants | all text and scenes | cultural/religious caricature | 0 blocker/serious issues |
| First-time full slice | Test complete MVP | 5-8 players | menu to cliffhanger | player does not know what to do | 70% finish with <= 2 neutral hints |
| Gamepad parity | Verify all release actions and prompts | 3 players | Chapter 1 plus one threat beat | Mouse-only interaction or unreadable focus | 100% critical path without keyboard/mouse |
| Performance | Hold visual budget | dev/QA | three benchmark scenes | Fog/light/foliage exceed budget | 1080p/60 target; M1 low preset >= 30 FPS |
| Full game | Verify the one-ending 6–8-hour arc | 5 first-time players | Acts 1–5 and epilogue | Mid-game lore overload or unclear tragic causality | 80% explain why Айдар destroys the pact and its price |

## Instrumentation Needed Before Serious Tests

Implement in the Godot telemetry/debug layer before external or first-time full-slice tests:

- anonymous `session_id`, build hash, timestamp, viewport, platform and input device;
- `scene_enter`, `scene_exit`, duration per scene;
- `world_location_enter`, `spawn_used`, `interaction_focused`, `interaction_committed`, `world_variant_changed`;
- `journal_open`, `external_handoff_open`, `stuck_hint_used`;
- `oldpc_open`, `search_query`, `item_open`, `locked_item_seen`, `clue_saved`, `term_clicked`, `reset_session`;
- `clue_acquired`, `contradiction_seen`, `vocabulary_unlocked`, `dialogue_key_used`, `pressure_level_changed`;
- observer hotkeys or panel: `confused`, `bored`, `stuck`, `aha`, `cultural_question`, `bug`;
- export one JSON per session and optional CSV summary;
- QA reset controls: clear route state, clear old PC state, jump to route node, jump to old PC section.

## Automated Checks

Current Godot/C# commands:

```bash
./eng/verify-dotnet.sh
./eng/verify-godot.sh
./eng/capture-fullgame-frames.sh
./eng/benchmark-godot.sh
./eng/capture-texture-candidate-frames.sh
npm run content:check
node --test tests/integration/content-closure/whole-pack-closure.test.mjs
git status --short
```

The browser commands and direct QA URLs below are historical parity-oracle checks only; they do not validate the Godot release target and will be removed at final cutover.

Recommended package scripts to add:

```bash
npm run validate:old-pc
npm run validate:route-graph
npm run validate:clue-graph
npm run validate:content
npm run test:smoke
```

Direct QA URLs after `npm run dev`:

```text
http://localhost:5173/
http://localhost:5173/?scene=village
http://localhost:5173/?scene=house
http://localhost:5173/?scene=computer
http://localhost:5173/?scene=forest
http://localhost:5173/?scene=zirat
```

## Script A: Internal Smoke

Duration: 20-30 minutes.

Setup:

1. Start a fresh browser profile or clear `localStorage`.
2. Run `npm run dev`.
3. Keep devtools console visible.

Steps:

1. Open `/`.
2. Click `Начать игру`; enter default or custom name.
3. Confirm chapter card appears and then phone intro starts.
4. Click 2-3 татарские chat messages and confirm translations are readable.
5. Buy ticket and confirm transition to `village`.
6. In route scene, move from arrival toward main street.
7. Find the house route; enter house handoff.
8. In house, click `Сесть за компьютер`.
9. In old PC, open `Архивный поиск`.
10. Search `Марат`.
11. Open `Справка о смерти Марата Н.`.
12. Save clue.
13. Search `реестр`; open `Строка реестра: дело Марата`.
14. Search or click `урман`; open relevant Татарвики / Кара-Урман material.
15. Use the desktop icon `Отойти от компьютера` or route return path.
16. Route toward zirat/forest approach.
17. Trigger pressure / Rinat interruption if the route supports it.
18. Open forest scene.

Record:

- console errors;
- broken images;
- missing audio;
- actions that appear clickable but do nothing;
- route dead ends;
- unreadable text;
- moments where direct URL jumps were required;
- localStorage state that fails to reset.

Fail immediately if:

- black screen;
- unhandled exception;
- route graph fails to load;
- old PC cannot open;
- validator fails;
- cliffhanger handoff is impossible after it is claimed implemented.

## Script B: Route Orientation

Duration: 15-20 minutes.

Start at:

```text
http://localhost:5173/?scene=village
```

Task for tester:

1. Find the house of Мансур.
2. Return to the main route.
3. Find the crossroad.
4. Find FAP/selsmag.
5. Find the mosque sign.
6. Find the route toward zirat.
7. Reach forest approach or Кара-Урман edge.
8. Open journal sketch at least once.

Observer records:

- wrong turns;
- time to first house;
- whether signs or journal were used;
- whether turns feel spatially logical;
- whether route labels feel like diegetic signs or quest markers;
- places where player gets stuck longer than 3 minutes.

Pass:

- 80% of testers find the required locations;
- no tester gets stuck twice for more than 3 minutes;
- journal helps orientation but does not feel like GPS.

## Script C: Old PC Investigation

Duration: 20-30 minutes.

Start at:

```text
http://localhost:5173/?scene=computer
```

Task for tester:

1. Find the official document about Марат.
2. Save it as a clue.
3. Find one document that contradicts it.
4. Unlock or identify one gated/corrupted file.
5. Use one suggested татарский term as a search term.
6. Explain what changed in your understanding of Марат.

Observer records:

- first search query;
- dead searches;
- first useful clue;
- whether locked file requirements are understandable;
- whether suggested terms feel helpful or too hand-holdy;
- documents skipped because too long or too dry;
- whether player understands "official lie" without the UI spelling it out.

Pass:

- 70% find official doc and contradictory register;
- median time to first useful clue is <= 7 minutes;
- at least 60% use a term such as `урман`, `Шүрәле`, `граница`, `не отвечай`.

## Script D: Narrative Comprehension

Duration: 45-60 minutes.

Rules:

- Do not explain canon.
- Ask player to think aloud.
- Observer may only ask: "what are you trying to check?" and "what do you think this means?"
- Use neutral hints only after 3 minutes stuck.

Run:

1. Start from `/`.
2. Play through intro, route, house and old PC.
3. If route to forest is implemented, continue to cliffhanger.
4. Ask post-test questions.

Post-test questions:

- Who is Айдар?
- Why did he come back?
- Who is Марат?
- What are the versions of what happened to Марат?
- Which source feels official but suspicious?
- Which source contradicts it?
- What does `урман` mean in the game after the old PC?
- Why might "do not answer" matter?
- What does Ринат know that Айдар does not?
- What do you think the village is hiding?

Pass:

- 80% explain Айдар's return and Марат's emotional role.
- 75% identify at least two connected clues.
- 80% understand that the village hides a system, not only one crime.

Fail signals:

- "I guess there is a monster in the forest" is the whole takeaway.
- Марат is remembered only as a name in documents.
- Татарский is remembered only as flavor.
- Player thinks Кара-Урман is the village.

## Script E: Language Mechanic

Duration: 20 minutes after old PC or dedicated build.

Task:

1. Identify all татарские words the player noticed.
2. Ask which word helped them act.
3. Ask them to use a word as a search term or dialogue key.
4. Show a re-read target after a word is confirmed.
5. Ask what changed in meaning.

Target words:

- `урман`
- `тавыш`
- `җавап`
- `ярамый`
- `зират`
- `шүрәле`

Pass:

- 60% use one word as a tool.
- 80% say the word changed search, interpretation or dialogue.
- Language reviewer marks no blocker errors.

Fail:

- Player says "language is cool but not needed".
- Words unlock only dictionary entries, not action.
- The player feels mocked for not knowing татарский.

## Script F: Cliffhanger

Duration: 10-15 minutes.

Prerequisite:

- route final path and forest/cliffhanger scene implemented.
- audio fallback implemented if authored voice is missing.

Steps:

1. Start from a state where final route clue is unlocked.
2. Route to forest approach.
3. Trigger Kara-Urman edge.
4. Let the voice call Айдар.
5. Pause long enough that player feels the temptation to answer.
6. Rинат interrupts with «Не отвечай».
7. Hard cut.

Post-test:

- What changed about the genre?
- Why was answering dangerous?
- Did Rинат feel like he knew a practical rule?
- Did you expect a monster reveal?
- Did the cut happen too early, too late or at the right moment?

Pass:

- 80% understand "rules/system" without exposition.
- 80% say Rинат's line is practical, not random.
- No one needs a full creature reveal to understand the turn.

Fail:

- Scene reads as jump scare.
- Scene reads as random ghost voice.
- Rинат reads as exposition delivery.

## Cultural And Language Review

Review before any external demo.

Reviewers:

- татарский language consultant;
- culturally knowledgeable татарский reviewer;
- religious/cultural reviewer for Тимур хәзрәт and mosque scenes if possible.

Review scope:

- intro татарский messages;
- route signs: `мәчет`, `зират`, `ФАП`, `сельмаг`, `Кара-Урман`;
- old PC Татарвики;
- all vocabulary cards;
- all Тимур/ mosque text;
- Шүрәле, Су Анасы, Бичура framing;
- audio lines, especially Марат voice and Ринат's «Не отвечай».

Severity:

- Blocker: must fix before any public build.
- Serious: fix before external demo.
- Minor: fix before polished MVP.
- Style: note for later polish.

Blocker examples:

- wrong татарский meaning that changes gameplay;
- mosque/islam framed as hostile or decorative superstition;
- folklore reduced to enemy mobs;
- generic "tribal horror" aesthetics;
- historical Тукай line stated as crude hard fact.

## Success Metrics

| Area | Success | Failure threshold |
|---|---|---|
| Technical | 0 smoke blockers, validators pass | Any blocker or repeated missing asset |
| Completion | 70% finish full slice with <= 2 hints | <50% finish |
| Detective loop | 75% identify 2 connected clues | Players read docs but infer nothing |
| Old PC | 70% find official + contradictory files | Search feels random |
| Татарский | 60% use a word as tool | Language described as cosmetic |
| Route | 80% find required locations | Journal/signs do not improve orientation |
| Narrative | 80% explain Айдар/Марат | Марат is not emotional hook |
| Cliffhanger | 80% understand old system/rules | Reads as generic monster reveal |
| Culture | 0 blocker/serious review issues | Any unresolved serious issue |

## MVP Release Gate

Before saying "MVP is ready for external playtest":

- `npm run build` passes.
- old PC validator passes.
- clue graph validator exists and passes.
- route graph validator exists and passes.
- smoke Script A passes with 0 blockers.
- Route Script B meets pass threshold.
- Old PC Script C meets pass threshold.
- Narrative Script D has at least one internal pass.
- Cultural review has no unresolved blocker.

Комплект сеанса — `eng/run-m10-playtest-session.sh <папка-вне-репо>`: он запускает
кандидата под защитой userdata, снимает кадры каждые 20 с и выдаёт бланк ответов,
сам в игру не играет и ответов не выдумывает. Кандидат он выбирает сам: явный
`URMAN_M10_CANDIDATE=<папка>` имеет приоритет, иначе берётся **самый свежий**
распакованный `native_*_candidate` из каталога доказательств. Раньше в скрипте был
жёстко прописан r34, который к этому моменту удалён: комплект упал бы на первом же
запуске у человека, который пришёл проводить сеанс. Теперь такого расхождения быть
не может, и при отсутствии кандидата скрипт печатает, что именно экспортировать.

## Попытка интерактивного прохода 2026-09-14 — заблокирована средой

Нативный кандидат `/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/native_20260914_r41_candidate` (r40 не содержит покачивания NPC; кандидаты r39 и старше удалены как устаревшие — использовать только r41)
был запущен под защитой userdata. Приложение действительно работает: главное меню
показано живьём (окно 1280×720, фон зимней деревни, пункты «Новая игра»,
«Продолжить», «Настройки», «Об игре и титры», «Выход»), затем процесс корректно
завершён, userdata восстановлены побайтово.

Провести сам проход обычным вводом **не удалось**, и это ограничение среды, а не
игры:

- координатный ввод перехватывает прозрачное системное окно Notification Center
  (bounds 0,0–1728,1117), поэтому каждый клик по игре отклоняется как чужой пиксель;
- AX-дерево игрового окна не отдаётся инструменту (капча захвата превышает 8 с),
  поэтому нажать кнопку как элемент нельзя;
- программный ввод через AppleScript недоступен оболочке (System Events отвечает
  таймаутом −1712), а синтетическое нажатие клавиши в игровое окно не доходит —
  меню не реагирует.

**Ответы в чек-листе не заполнялись.** Выдуманные «дословные ответы» нового игрока
были бы подлогом, поэтому здесь только констатация блокера. Скриптовый маршрут тоже
не заменяет проход: он заранее знает ответы и ведёт камеру к центру interaction
proxy. Что доступно и подтверждено: скриптовая проверка обычной точки входа
(`act1_demo_launch_smoke_test`: dedicated entrypoint → first-person arrival →
dynamic keyboard/gamepad intro → Chapter 1 campaign) — это структурная проверка, не
плейтест.

## Форма для человеческого сеанса M10 (заполнить вручную)

Протокол и обоснование — `docs/production/act1_m10_handoff_package_2026-09-14.md`,
раздел 8. Кандидат — `native_20260914_r41_candidate`, чистая установка, без подсказок.

| # | Вопрос | Ответ дословно | Время / место |
|---|---|---|---|
| 1 | Куда ты приехал и к кому? | | |
| 2 | Что ты нашёл и почему тебе это важно? | | |
| 3 | Что ты решил сделать и почему он не сделал это сам? | | |
| 4 | Что теперь кажется странным? | | |
| 5 | Что ты хочешь проверить дальше и где? | | |
| 6 | Покажи, как открыл журнал и что искал | | |
| 7 | Что произошло в финале и что тебе запретили? | | |
| 8 | Ты вернулся в меню — продолжил бы играть? | | |

Дополнительно: длительность сеанса, места затора (с временем), что перечитывал, что
хотел сделать, но игра не позволила; отдельно — произношение татарских слов.
Результат вписать в таблицу выше и продублировать в `open_questions.md`, если
всплывёт противоречие канона.
