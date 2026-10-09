# Technical Architecture

Target stack, accepted 2026-08-10: **Godot 4.7.1 .NET, C# и .NET 10 LTS**. Production target — ходибельный 3D-мир с постоянной камерой от первого лица для macOS и Windows. Browser и mobile не входят в релиз.

Engine-neutral остаются narrative kernel, portable content, compiler contracts и save data. Они не зависят от Godot nodes, scene paths, physics implementation или rendering API. Godot — единственный production owner представления, input, audio и world scenes.

## Действующий delivery contract — PhotoWorlds, 08.10.2026

Релизная цель — полная кампания «За краем снимка» по
`../tasktracker/07_full_game_integration_2026-10-08.md`, а не только прежний demo.
Наличие `act1_demo.tscn` в текущем project entrypoint описывает фактический старый запуск,
не ограничивает новый объём и не доказывает, что переключение уже выполнено.

Исходные владельцы нового графа — `content/campaigns/urman.fullgame/campaign.json` и
`content/modules/urman-fullgame/`; результат компиляции потребляет существующий
`CompiledCampaignRepository`. `narrativeOrder` служит индексом для инструмента симуляции;
исполняемый переход принадлежит interaction/dialogue, его conditions/effects и target scene.
Манифест выбирает активные quests новой арки: прежние определения могут сохраняться для
истории и совместимых потребителей, но не должны выдавать отменённые обязательные цели.

Версионируемое состояние PhotoWorlds остаётся внутри существующего kernel snapshot
под `namespaceId=photoworlds-v1`; namespace кампании и schema каталога — разные версии.
Snapshot schema 2 хранит campaign ID/exactVersion, каталог, передачу книги, отдельные
PhotoId/PageId acquisition и mount, чтение оборота, контекст, provenance facts/external
evidence, фактический текст подписи, пролог и эпилог. `visits.active` остаётся null до
реального перехода; visit record хранит семантический WorldId/safe-node и value-only
ReturnTicket. Пока runtime PhotoWorldCatalog не поставлен (PW-007), валидатор принимает
для текущего fullgame только авторские WorldId W01–W05; неизвестные ключи отклоняются.
Per-world state резервирует nullable UInt32 seed, safe-node, action, observation и delta maps;
активный/возвращающийся visit обязан ссылаться на свой сохранённый seed и safe-node.
ReturnTicket сверяет campaign ID/exactVersion со snapshot и хранит logical clock в форме
`{tick: nonnegative Int64}` по `LogicalClockSnapshot`. Persisted transition ограничен
сочетаниями `enter: preparing/loading/destination-ready/photo-active`, `return:
returning/village-ready`, либо соответствующей фазой `failed` с failureCode; lifecycle
visit должен совпадать с фазой, target обязателен и совпадает с visit или origin билета;
visit ID не может одновременно находиться в active и history. Составляющие
внешнего подтверждения проверяются точно по одному разу, source IDs не повторяются.
Узлы Godot и придуманные координаты в save не попадают. Завершение глав, словарь и
one-shot command occurrences остаются у действующих owners `quests`, `vocabulary` и
RuntimeSnapshot occurrence ledger; PhotoWorlds не дублирует их.

Единственная текущая миграция — schema 1 → 2. Она сохраняет каталог и все реальные
book/photo/fact/evidence/caption/prologue/epilogue значения, добавляет campaign identity,
пустую историю visits, null active visit/transition и пустой world map. Она не выводит
из отсутствующих данных посещение, действие, наблюдение, завершение, seed или safe spawn.
Неизвестная nested schema отклоняется до flush/project live owners; kernel restore повторно
проверяет schema 2. Мигрированный snapshot остаётся в памяти до обычного следующего save.

Новая кампания использует `photoworlds-v1`-подкаталог через прежний `AtomicSaveGameStore`
и `RuntimeBridge`; legacy quick/checkpoint primary/backup остаются в исходном каталоге.
Это явная граница несовместимости, не автоматическая миграция старых pact-флагов.
SaveGameV3 schema, RuntimeSnapshot schema, PhotoWorlds schema и campaign fingerprint
не взаимозаменяемы. Graph safe-node remaps и unknown-version recovery принадлежат PW-025;
prepare/ReturnTicket и физическое восстановление внутри мира — PW-020/PW-024 и остаются
отдельными гейтами. Текущий контракт сам по себе не доказывает готовность этих потоков.

Снимок SaveGameV3 берётся после штатного flush владельцев физического состояния: `RuntimeBridge`
сначала сохраняет транспорт, двери/объекты мира и фактическую позу сопровождающего NPC, затем
вызывает `RuntimeKernel.CaptureSnapshot()`. В частности, `AlsuStreetWalkPresentation` пишет
реальную позицию, yaw, checkpoint, pendingCheckpoint и пройденный участок в существующий
`world.props` через команду ядра. Поэтому runtime state, прочитанный до `SaveSlotAsync`, может
отличаться от сохранённого вследствие обычных owner flush; проверка дискового снимка сравнивает
его с live state после flush. В текущем prologue smoke единственное различие с начальным state —
новая запись фактической позы сопровождающего в `world.props`. Вложенный `photoworlds` сохраняет
исходный нарративный срез, а load восстанавливает именно декодированный снимок и затем проецирует сохранённую позу.
Существующий guarded `FullGameFlowSmokeTest` проверил эту границу на Windows: job
`2ef4f2bd8a14484db7c0cb3239397f10` завершён с PASS; receipt и точный source snapshot
сохранены в `evidence/PW005/native_2ef4f2bd8a14484db7c0cb3239397f10/`. Это не подтверждает физическое восстановление посещения PhotoWorld или работу
его safe-node.

Семантический сценарий проверяет переходы и источники состояния; физические якоря,
проходимость, пять произведённых миров и художественная приёмка требуют соответствующих
PW-карточек и свежего REL-003. Декларация `entryAnchorId` не является сохранённым `in_range`.

## Историческая граница поставки — Act 1 demo

Текущий delivery target — не полный five-act migration, а отдельная
проверяемая Godot-сборка первого акта. `res://scenes/act1_demo.tscn` оборачивает
существующий first-person composition root, задаёт старт `village_day@arrival`,
показывает короткую presentation-заставку и после
`urman.chapter1:beat/cliffhanger-hard-cut` выводит финальный overlay. Runtime
kernel, save/load, capabilities, content IDs и narrative state остаются общими;
`Act1DemoRoot` не пишет сюжетное состояние и не создаёт второй runtime owner.

В launch path демо входят только пять компактных зон первого акта:
`village_day`, `house_old_pc`, `fap_clinic`, `zirat_road` и
`kara_urman_night`. Full-game zones Acts 2–5, их presentation dressing,
полный playthrough и web retirement остаются долгосрочными deferred задачами и
проверяются отдельными сценами, но не должны случайно попасть в demo entrypoint.

## Target C# / Godot ownership

- `Urman.Core` — чистое C#-ядро: commands, events, state, kernel, clue graph, quests, vocabulary, capabilities, deterministic clock/RNG.
- `Urman.Content` — portable models, JSON Schema, Markdown, compiler, reference closure, logical asset/text/audio resolvers и narrative invariants.
- `Urman.Godot` — composition root, world director, first-person controller, interactions, UI, audio, animation и save coordinator.
- `Urman.ContentCli` — единственный Content Lab: `validate`, `compile`, `inspect`, `simulate`, `report`.
- `content/**` остаётся source of truth. C# compiler обязан сохранить namespaced IDs, deterministic fingerprints и закрытые capability contracts текущего pack.
- TypeScript/Vite/Three.js runtime остаётся read-only parity oracle до финального cutover. Production bridge, dual owner и permanent fallback запрещены.

Quest-owned capability sessions синхронизируются по `quests.*.activeCapabilities` только после committed kernel transaction. `QuestCapabilitySessionOrchestrator` создаёт и запускает недостающие provider sessions, восстанавливает их из `SaveGameV3`, откатывает созданные в текущем reconciliation сессии при provider-сбое и не удаляет устаревшую сессию, пока runtime claim с тем же owner ID не освобождён. Постоянные presentation capabilities, например старый ПК, остаются отдельными composition-owned sessions и не попадают под quest cleanup.

## Исторические web-модульные заметки — 2026-07-18 (только parity oracle)

Ниже сохранены записи модульной миграции, сделанные до принятия Godot/C# target. Они описывают прежний browser composition и `GameSnapshotV2` как исторические доказательства; эти owners не являются текущими production boundaries и не должны восстанавливаться. Текущая архитектура — разделы `Target C# / Godot ownership`, `3D World Presentation` и `Save / Load`.

Миграция MM-00–MM-80 (цепочка удалена при чистке 2026-09-18; см. git-историю) завершила историческую архитектурную подготовку web-oracle к достройке MVP, но не объявляла MVP готовым. В текущем target Production Chapter 1 и full-game campaign — заменяемые C# compiled campaigns, а не встроенный сценарий.

- Portable source of truth — JSON/Markdown в `content/modules/**` и campaign manifests; compiler создаёт immutable `CompiledContentPack` и блокирует duplicate ID, неизвестные opcode, недостижимые обязательные точки, missing assets/providers и раннее раскрытие улики.
- `RuntimeKernel` — единственный writer progression state и occurrence ledger. Обычный квест/персонаж/диалог/asset меняется данными; уникальная механика приходит capability provider с exact version.
- `CampaignManifest` фиксирует ordered modules, role bindings, capability requirements и entrypoint. `GameSnapshotV2` хранит campaign lock/fingerprint; несовпадение не делает partial load и требует явный reset.
- `main` — единственный browser composition root. Он собирает `RuntimeBootstrap` и named persistence gateway; `Game` получает только presentation facade, без kernel, registry, capability host, raw storage или story IDs.
- Gateway — единственная production storage boundary. Он один раз удаляет четыре pre-MVP v1 keys, сохраняет username/preferences и Content Lab namespace, а current V2 save удаляет только после exact typed reset proposal.
- `Content Lab` и legacy MainMap остаются development-only. Old PC/DedOS — отдельный capability module; production не подключает legacy paths без явной campaign registration.
- Практический authoring flow и data-only side quest example закреплены в `content_authoring_guide.md`; это entrypoint для следующей ЛЛМ, а schema/contract authority остаётся в `content/schemas/**`.

Не входят в v1: live hot-swap активного run, пользовательские моды, универсальный scripting framework, ECS и Unity importer. После reload presentation открывает campaign entrypoint без повторного entry effect; точное восстановление экранной позиции потребует отдельного расширения snapshot contract.

## Исторические planning notes

Разделы ниже сохраняют engine-neutral product ideas. Они не являются списком действующих runtime owners: legacy `GameState`, `SceneManager`, `RouteNavigationScene`, `SaveSystem`, `src/data/**` и regex validators удалены и не должны восстанавливаться.

## Content Layout Proposal

```text
/data
  /characters
  /locations
  /routes
  /dialogues
  /clues
  /documents
  /vocabulary
  /timeline
  /quests
  /lore
  /assets
```

## Data-driven Quests

Квесты должны описывать не «иди в точку X», а условия знания:

- required clues;
- required vocabulary;
- required location access;
- NPC state;
- time phase;
- unlocked topics;
- consequences.

MVP quest example: `quest_marat_first_contradiction`.

## Dialogue System

Диалоги должны поддерживать:

- topics;
- NPC states;
- required knowledge keys;
- escalating reaction levels;
- effects: unlock clue, unlock topic, update suspicion, notify council;
- language fragments with partial translation.

Ответ NPC не должен быть единственным линейным узлом. Один topic может иметь несколько ответов в зависимости от того, что игрок уже знает.

## Knowledge Key System

Knowledge key — общий формат для фактов, слов, документов, фото, предметов и противоречий.

Поля:

- `id`
- `title`
- `type`
- `source`
- `tags`
- `reveals`
- `dangerLevel`
- `usableInDialogue`
- `relatedCharacters`
- `relatedLocations`

## Inventory / Evidence System

Inventory в УРМАНЕ — не только предметы. Это evidence inventory:

- physical items;
- documents;
- photos;
- names;
- dates;
- татарские words;
- contradictions;
- testimony.

Система должна позволять применить evidence в диалоге, архивном поиске и journal graph.

## Document Viewer

Document viewer должен поддерживать:

- body text;
- metadata: year, source, author, location;
- tags;
- highlighted terms;
- unknown татарские words;
- re-read after vocabulary unlock;
- links to clue graph.

Документы не должны быть статичными страницами лора. Они должны открывать действия.

## Old PC Interface

Accepted MVP direction: старый ПК бабая — основной document hub / документальный хаб расследования и отдельная оболочка поверх тех же data-driven сущностей, что journal, documents, clues and vocabulary.

Product lock: `old_pc.md`.

Core surfaces:

- folders;
- archive search;
- «Татарвики»;
- Marat documents;
- local documents;
- saved messages;
- possibly corrupted files;
- document marks and registry contradictions;
- search by keywords;
- vocabulary-sensitive re-read.

Data requirements:

- PC documents should use the shared document model, not a separate lore-only format.
- Search index should connect documents, vocabulary, clues, dates, characters and locations.
- PC entries should be able to unlock `KnowledgeKey` ids and update the journal graph.
- Открытие документа подтверждает связанные knowledge keys; отдельное действие «В журнал» добавляет ссылку в `runtime.journal`. Повторное сохранение идемпотентно.
- Татарские terms discovered elsewhere should be usable as PC search terms.
- Some files can be gated by story flags, passwords, damaged text or recovered fragments, but MVP should avoid complex freeform hacking.
- Technical metadata must not be the main clue type. Use document marks such as stamps, registry dates, case numbers, crossed-out lines and repeated classifications.
- PC shell should be unnamed Win98-like UI, not a canon-branded OS.

Он должен быть дешёвым production hub: много сюжета через UI вместо дорогих катсцен. Full programmability / Turing-complete behavior is explicitly post-MVP.

## Old PC Authoring Model

Source of truth for old PC content — portable Markdown/JSON definitions модуля `urman.oldpc` под `content/modules/**`. Runtime получает их только из compiled content pack; прямого Vite raw import или отдельного parser нет. Другой engine сможет читать ту же portable границу, но Unity importer не входит в v1.

Required authoring fields:

- `id`
- `type`
- `title`
- `pcSection`
- `canonStatus`
- `reliability`
- `searchTerms`
- `reveals`
- `requires`
- `unlocks`
- `relatedCharacters`
- `relatedLocations`
- `dangerLevel`
- `mvp`

Allowed reliability values:

- `official_lie`
- `partial_truth`
- `personal_memory`
- `village_record`
- `pact_record`
- `folklore_mask`
- `corrupted`
- `unverified`

Allowed canon statuses:

- `canon`
- `soft_canon`
- `hypothesis`
- `proposal`
- `in_world_lie`

Semantic compiler (`npm run content:check`) проверяет IDs, schema, typed refs, reachability, capability requirements и narrative timing. Отдельный regex validator `validate-old-pc-content` удалён; новый validator нельзя возвращать «на всякий случай».

## Messenger Interface

«Ялкын»:

- direct messages;
- group chats;
- timestamps;
- search;
- message clues;
- partially understood татарские fragments;
- attachments: photo, audio, document.

Для MVP можно реализовать read-only версию.

## Татарский Language-learning System

Система должна поддерживать:

- vocabulary entries;
- known / guessed / confirmed states;
- contextual hints;
- unlock effects;
- partial translation states;
- old document re-render after unlock;
- words as dialogue keys.

Accepted content direction, 2026-05-23: vocabulary entries describe real татарские words used inside русско-татарская mixed speech. Dialogue data should support Russian lines with татарские inserted tokens, increasing татарский density by story phase without requiring complex grammar. At least one NPC may be configured as татароязычный / mostly татароязычный, but core progression must not depend on fully understanding long татарский lines.

Useful authoring fields for vocabulary / dialogue:

- `densityStage`: 1 | 2 | 3 | 4;
- `firstContext`: dialogue, route sign, document, old PC search, NPC-only line;
- `repeatContexts`: source ids where the word appears again;
- `applicationTargets`: dialogue topics, search terms, re-read targets or route hints unlocked by the word;
- статус слова — монотонная лестница `unknown → guessed → confirmed`: запись, понижающая статус, игнорируется ядром (`ContentRuleEngine.SetStatus`), поэтому подсказка сцены не отменяет уже понятое слово; у `knowledge` семантика прежняя (см. `decision_log.md` 2026-09-14);
- `requiresConsultantReview`: boolean.

Не делать language system отдельным «учебником». Она должна быть связана с evidence.

## Character State

Для NPC:

- trust;
- fear;
- suspicion;
- knows player has clue X;
- available topics;
- last reaction;
- council notified flag.

Состояние нужно для того, чтобы сильные ключи меняли не только одну реплику, но и поведение деревни.

## Village State

Village state:

- day phase;
- investigation pressure;
- council alert level;
- forest presence;
- safe zone availability;
- route restrictions;
- village rumour state.

Это позволит делать ощущение, что Кырлай реагирует на расследование.

For MVP, village state should stay lightweight:

- `pressureLevel`: 0–3;
- concrete flags such as `rinat_alerted`, `council_notified`, `night_route_blocked`;
- ambience state ids for location changes;
- dialogue state effects tied to dangerous knowledge keys.

Do not build a full village simulation for MVP. Pressure is scripted dramaturgy around the detective loop.

## 3D World Presentation

Accepted MVP direction, 2026-08-10: compact walkable 3D locations with a continuous first-person camera. World presentation consumes portable narrative state but does not own clues, quests, dialogue progression or save truth.

Engine-neutral presentation responsibilities:

- load a compact location and its authored spawn point;
- provide player movement, look, collision and interaction focus;
- map world interactables to portable scene, dialogue, clue, document or capability IDs;
- apply time, pressure, ambience and visibility variants from runtime state;
- expose location transitions through typed campaign data rather than hardcoded story branches;
- keep journal navigation as a supporting view, not the primary overworld.

`ResolverCatalog` объединяет registries только в явном порядке модулей кампании и запрещает duplicate logical IDs. `AssetResolver` детерминированно выбирает вариант по explicit ID либо canonical seed/state и передаёт logical file host-адаптеру. `TextResolver` возвращает структурированные локализованные сегменты, включая vocabulary tokens, без HTML. `AudioResolver` возвращает resource metadata, captions, transcript и равнозначный non-audio cue; сам playback остаётся ответственностью Godot.

The Godot audio presentation boundary is explicit: `AmbientAudioDirector` owns only the continuous physical-zone bed. It loads `game/assets/audio/ambient_manifest.json`, maps a logical `WorldLocationId` to one imported WAV stem and exposes the current stem as presentation metadata; it never writes narrative state or competes with `AudioResolver`. Normal desktop runs attach and loop an `AudioStreamPlayer`, while headless verification validates stream import and zone switching without starting native playback, avoiding false-positive audio-server leaks. The current four stems are deterministic technical placeholders; authored field ambience, Marat/Rinat voice, final mix, captions and cultural review remain release-owned content work.

Godot implementation: каждая крупная локация — отдельная `PackedScene`; `WorldDirector` загружает компактную зону и spawn point по логическим IDs. Камера имеет высоту около 1,7 м, FOV 75° с диапазоном 65–90° и минимальный head bob. Основной renderer — Forward+ с baked lighting, LOD, occlusion culling, ограниченными динамическими тенями и локальным volumetric fog. Для слабого/программного GPU существует только явный presentation-only `--urman-safe-mode` через Mobile renderer и low painterly material branch; это не gameplay fallback и не второй runtime owner. Seamless open world, combat controller, complex NPC schedules и general-purpose pathfinding не требуются.

Chapter 1 physical mapping использует пять зон: `village_day`, `house_old_pc`, `fap_clinic`, `zirat_road` и `kara_urman_night`. Несколько evidence scenes переиспользуют дом и ФАП, но остаются отдельными portable scene IDs. `InteractionTarget` отображается и участвует в raycast только когда его authored source scene активна и условия перехода выполнены; смена `PackedScene` не изменяет narrative state сама по себе. Сквозной headless smoke проходит все 16 точек `narrativeOrder` и проверяет финальные clue/beat в едином kernel state.

Full-game presentation foundation добавляет campaign `urman.fullgame`: 12 compact logical zones (`fullgame_act2_house` … `fullgame_act5_epilogue`) materialize через отдельные authored `PackedScene` wrappers в `scenes/zones/fullgame/`, каждый из которых задаёт один `ZoneId` и использует общий `FullGameZone` adapter. Compiled scene interactions становятся физическими `InteractionTarget` nodes; `FullGameInteractionLayout` ставит их рядом с authored prop anchors (док, архивный стол, книга пакта, boundary marker), а не в общем grid. `FullGameZoneDressing` — отдельный presentation-only owner, который выбирает модульный river/archive/pact/boundary и другие zone-specific наборы по `ZoneId`; он не читает и не меняет narrative state. `GeneratedModularKitDressing` селективно инстанцирует проектный `urman_modular_kit.glb` в доме Акта 2, советском архиве Акта 3 и лесной границе Акта 5, выравнивает kit по authored anchor и задаёт LOD0 `0–24m` / LOD1 `18–72m` с self-fade. `FullGameNpcDressing` теперь размещает project-original `urman_character_kit.glb` через `GeneratedCharacterKitDressing`: девять именованных character prefixes, ground anchors, deterministic face landmarks/layered clothing, nine Blender-authored `Idle`/`Tension` armatures и LOD0 `0–18m` / LOD1 `14–48m`; слой остаётся presentation-only, без physics bodies, interaction ownership или narrative state. Godot `AnimationPlayer` запускает matching `Idle` и позволяет presentation-only переключение на `Tension`; facial expression polish и authored audio остаются отдельными production gates. Blender `-col`-объекты не становятся физикой автоматически: активными остаются layer-1 ground/zone colliders и provisional layer-2 proxy colliders, а не импортированный render mesh. Отдельный `CollisionQaSmokeTest` уже проверяет queryable floor всех 12 зон и изоляцию kit proxies; качество финальных mesh-colliders остаётся production gate. Этот слой всё ещё является production-progress pass, а не финальным art/level production. Full-game smoke проверяет authored scene path, physical dressing, authored interaction layout, environment and character GLB selection, NPC contract, animation playback и visibility ranges для каждой пройденной зоны, каждый transition/dialogue и совпадение target zone с compiled target, поэтому отсутствующий ID не может незаметно стать generic `world.interact`.

Physical documents use the same compiled document registry as the old PC. An interaction may declare one `targetDocumentId`; `RuntimeBridge.OpenDocumentAsync` evaluates access conditions and applies `openEffects` plus the presentation-only `document.open` effect in the kernel. `DocumentUi` renders the Markdown body and uses the existing `journal.record` command when the player explicitly presses «В журнал»; repeated records are idempotent. Four full-game documents (Baranov 1967, Tukay 1913, Kazan 1552 and the pact ledger) are now placed beside their authored props and covered by the full-game flow smoke.

Godot journal — read-only projection того же `runtime.journal`, которое изменяет kernel. `CompiledCampaignRepository` разрешает `entryId/sourceId` через compiled registries документов и knowledge keys; `JournalUi` и `DocumentUi` не хранят сюжетное состояние и не содержат захардкоженных записей. Контроллер игрока отвечает только за движение и modal lock. Старый ПК, физические документы, журнал, диалоги, vocabulary и quests читают одно runtime state.

`FirstPersonController` определяет последнее устройство по реальным Godot input events и меняет подсказку взаимодействия между `[E]` и `[A]`. FOV, чувствительность, выбранное устройство и остальные поля `GameSettingsSnapshot` больше не заменяются константами при save/load. `SettingsUi` применяет FOV, чувствительность, лёгкий head bob и `low/medium/high` render scale + MSAA. Motion blur не включён в renderer-профиле. Accessibility-контракт `AccessibilitySettingsSnapshot` хранит reduced motion, high contrast, text scale, subtitles и audio descriptions; `AccessibilityPresentation` доставляет его всем UI targets через одну Godot group. Reduced motion отключает head bob, text scale и high contrast применяются к UI-панелям, а `AudioCueUi` выбирает captions или равнозначный non-audio cue по настройкам. Это закрывает технический presentation slice; внешняя проверка читаемости, укачивания и культурно корректных текстовых описаний остаётся playtest/release gate.

Historical prototype, superseded as production direction on 2026-08-10:

- `src/scenes/RouteNavigationScene.ts` is the first runtime implementation of this model.
- `public/assets/urman_route_map/route_graph.json` is the source of truth for route nodes, exits and route state transitions.
- `public/assets/urman_route_map/asset_manifest.json` maps route asset IDs to PNG files.
- `public/assets/urman_route_map/animation_layers.json` and `editable_layers.json` drive runtime overlays / editable text regions.
- `SceneManager` maps `village` to `RouteNavigationScene`; the old procedural/isometric map is preserved only as `villageGreybox`.
- `GameState` stored route node progress and journal sketch state for that prototype session.

These files may remain as provenance and composition reference. They must not be restored as a parallel presentation owner or used as a fallback for the first-person 3D target.

## Save / Load

Target contract: `SaveGameV3` хранится атомарно под `user://` через temporary file и replace. Он не импортирует `GameSnapshotV2`, не читает и не удаляет browser localStorage.

Детерминированные отложенные действия принадлежат `Urman.Core.Determinism.DeterministicScheduler`: очередь сортируется по logical tick и `jobId`, поддерживает owner-scoped cancel/claim/ack/release и входит в `SaveGameV3`. Временные claim leases не сохраняются: после загрузки незавершённая работа снова доступна для детерминированной обработки. `CustodyStore` и `EvidenceProvenance` остаются чистыми runtime primitives: первый атомарно планирует claim/transfer/consume через kernel resource claims, второй строит проверяемые цепочки происхождения без циклов и самоссылок.

Save payload должен хранить:

- schema version;
- campaign ID, exact version и fingerprint;
- runtime snapshot и capability snapshots;
- snapshot deterministic scheduler без временных leases;
- collected clues;
- vocabulary state;
- quest state;
- document read state;
- NPC states;
- village state;
- journal graph;
- current time phase;
- location access;
- `WorldLocationId`, `SpawnPointId`, player transform;
- settings, portable keyboard/gamepad button/axis input bindings, `AccessibilitySettingsSnapshot` и playtime.

Повреждённая запись не должна уничтожать последнюю рабочую: save coordinator сохраняет предыдущий валидный файл как recovery candidate и выдаёт typed error вместо partial load.

`InputBindingSnapshot` хранит logical action, physical keyboard keycode и optional gamepad button или axis/sign pair. `Urman.Core` проверяет уникальность и форму списка, а `Urman.Godot.InputBindingService` один преобразует его в Godot `InputMap`. Переназначение клавиши, кнопки или аналоговой оси очищает только прежние события той же action; mouse bindings и остальные actions не затрагиваются. SaveGameV3 ещё не выпускался, поэтому контракт обновлён на месте без чтения старых development-only V3 файлов и без compatibility branch.

`AccessibilitySettingsSnapshot` по умолчанию сохраняет включённые subtitles и audio descriptions, отключённые reduced motion/high contrast и масштаб текста `1.0`. `SaveGameV3Codec` ограничивает масштаб диапазоном `0.8–1.6` и отклоняет нечисловые значения до записи; загрузка не создаёт второй settings store и не меняет browser localStorage.

Персистентность доступности проверяется в обоих путях: слот сохранения — `PlayerSettingsSmokeTest`, а холодный запуск через хранилище приложения `UserSettingsStore` — `Act1UserSettingsSmokeTest`, где теперь проверяется не только FOV, но и `Accessibility` (reduced motion, high contrast, масштаб текста 1,25): настройки должны не только записаться, но и сняться — иначе чувствительному к укачиванию игроку пришлось бы выставлять их заново при каждом запуске. Раньше в этом тесте покрывался только FOV.

## Localization

Даже если основной язык проекта — русский, система должна нормально держать:

- русский текст;
- татарские слова;
- переводы;
- partial translation;
- glyph support: ә, ө, ү, җ, ң, һ.

Татарский должен быть не вторичным «локализационным языком», а gameplay data.

## Asset Registry

Реестр ассетов должен связывать:

- asset id;
- type;
- priority;
- used by quest / document / location / character;
- MVP status;
- placeholder status;
- source / license.

Для каждого локального производственного файла реестр также обязан хранить
`sourceSha256` и `derivedSha256` (если файл существует в репозитории),
generator/source-of-truth и технические ограничения ассета. Скрипт
`eng/verify-asset-registry.sh` выполняет host-independent preflight: проверяет
уникальность IDs, лицензирование, безопасные repo-relative пути, наличие файлов
и совпадение SHA-256. `eng/verify-assets.sh` запускает этот preflight до Blender;
падение Blender при старте классифицируется как `HOST_TOOLCHAIN_BLOCKED` и не
получает fallback или право объявить свежий asset PASS.

Это нужно, чтобы контролировать главный риск MVP — расползание ассетов.

## Content Pipeline

Accepted target:

1. Писать narrative data в существующих Markdown / JSON modules.
2. `Urman.ContentCli validate` проверяет schemas, IDs, references, capabilities и narrative invariants.
3. `compile` создаёт immutable `CompiledCampaign` и deterministic `CampaignFingerprint`.
4. `inspect` показывает modules, bindings, clue/reveal graph и asset closure.
5. `simulate` прогоняет deterministic command scenarios без Godot.
6. `report` формирует production audit: orphan clues, missing sources/uses/assets, unchecked татарские строки и story-gate violations.

Asset provenance проверяется отдельным host-independent шагом до запуска
Blender; Blender/GLB verification остаётся вторым, более узким gate для
генераторов и импортируемой геометрии. Временная проблема рендер-хоста не
скрывает расхождение source/derived hashes, а успешный preflight не подменяет
проверку Blender.

## Narrative Flags

Флаги должны быть именованы по смыслу:

- `marat_grave_date_found`
- `rinat_notified_council`
- `tt_urman_confirmed`
- `old_pc_archive_unlocked`
- `first_mystical_trace_seen`

Избегать флагов вида `quest1_done`, потому что они быстро ломают логику.

## Clue Graph

Clue graph — центральная структура расследования:

- nodes: character, clue, document, location, vocabulary, event;
- edges: reveals, contradicts, confirms, mentioned_by, found_at, unlocks;
- tags: MVP, language, danger, Marat, pact.

Graph нужен и для журнала игрока, и для внутренней проверки сценария.

## Engine Compatibility Boundary

Portable contracts используют JSON/Markdown, валидируемые IDs и ссылки. Godot-specific scene paths, nodes, resources и input actions живут только в `Urman.Godot` и его registries. `Urman.Core`, `Urman.Content` и сохранения не ссылаются на Godot assemblies.

## Самостоятельная сборка Акта I — 2026-09-11

`eng/export-desktop-release.sh <пустой внешний каталог>` создаёт unsigned
`macos/URMAN.zip` с `URMAN.app`, `windows/URMAN.exe` с .NET-зависимостями и
`URMAN-windows-x86_64.zip`. Существующие проверки состава PCK и native-архивов
работают на фактически экспортированных файлах; ошибка удаляет только новый
комплект этого запуска. Проверка PCK берёт обязательные текстуры из
`PainterlyMaterialLibrary`: старый release-фильтр ошибочно исключал используемые
v2–v5 материалы, из-за чего обычное окно оставалось без мира, хотя headless
bootstrap проходил. Headless не доказывает загрузку GPU-ресурсов.

Существующий `Act1DemoRoot` выполняет пакетный probe в точке приезда: 12 секунд
прогрева, 60 секунд кадров, среднее/p95/p99/max и доля кадров дольше 33,3 мс.
Меню, headless, короткий диагностический запуск и незавершённая сборка мира
не получают PASS. Это статический замер одной точки, а не проверка плавности
всего маршрута. Windows host launch и подпись/notarization остаются отдельными
неподтверждёнными требованиями.

## Меню, профиль и завершение Акта I — 2026-09-11

«Продолжить» проверяет quick и checkpoint через AtomicSaveGameStore, выбирает
последний исправный payload по времени файла, включая резервную копию, и
показывает место/дату. Несовместимые и повреждённые файлы остаются на месте.
Загрузка возвращает прямо в сохранённую сцену; вступление показывается только
для новой игры. Неудачная загрузка оставляет меню с сообщением.

Текущий профиль настроек остаётся главным при загрузке сюжета: старый слот
не меняет размер текста, чувствительность, FOV и другие настройки игрока.
Settings в SaveGameV3 сохраняются для совместимости формата.

Новая игра при наличии исправного сохранения требует второго нажатия и
объясняет замену будущих автосохранений; ручной слот не удаляется. Титры и
лицензии открываются из прокручиваемого меню, Esc возвращает фокус. Файл
`game/content/credits.ru.txt` включён в export presets и обязательный состав PCK.
Финальная карточка «Конец Акта I» возвращает в меню кнопкой или Esc; новый
сеанс сбрасывает состояние финала. Tween привязан к экрану и прекращается
при его удалении.

Проверка Continue начинается после подключения bridge. Быстрые клавиши записи
и загрузки заблокированы в модальных экранах, чтобы меню не перезаписывало
ручной слот начальным сеансом; явные кнопки паузы сохраняют прежние API.

Checkpoint guard сбрасывается в общем ReplaceKernel; завершение старой записи
не устанавливает guard новому сеансу. На время чтения слота очередь реплик
приостанавливается, а повторные load/save/new-game не принимаются. Успешное
чтение заменяет очередь; неудачное возвращает прежнюю паузу без потери реплик.
При равном времени исправных payload предпочтение получает checkpoint.

### 2026-09-11 — диалог при увеличенном тексте

DialogueUi теперь увеличивает высоту существующей панели вместе с масштабом
шрифта и сохраняет нижний отступ. На 1.6 длинная реплика Мансура и три ответа
помещаются одновременно; прежние 324 px обрезали реплику над ответами.
Существующий Act1UiReadabilitySmokeTest дополнительно проверяет границы
интерактивных дочерних контролов вне ScrollContainer. Узкая матрица 720p/1080p,
шрифты 1.0/1.6 прошла; оба крупных диалоговых кадра просмотрены главным агентом.
Evidence: `/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/ui_readability_round4.log`
и `ui_acceptance/readability_v4`. Это приёмка изменённого диалога, не всей игры.

2026-09-11: после последней сборки MainMenu smoke повторён на настоящем
Metal renderer в 1280×720 и 1920×1080. Проверены fresh/Continue, настройки,
возврат из титров и загрузка; сохранены normal/1.6 кадры меню и титров.
Крупный текст прокручивается внутри панели, кнопки и видимый фокус сохранены.
Просмотрены кадры в `ui_acceptance/menu_720` и `ui_acceptance/menu_1080`;
логи `menu_720_round4.log` и `menu_1080_round4.log` во внешнем каталоге.
Профиль пользователя восстановлен byte-for-byte. Художественная доводка
фоновой сцены и обычное полное прохождение остаются отдельными пунктами.

### 2026-09-12 — выбор поверхности performance probe

Существующий Act1DemoRoot принимает --urman-perf-sample=zone@spawn только при включённом performance probe. Пара проверяется таблицей Act1WorldLayout до входа Main в дерево; обычный запуск и menu diagnostic сохраняют arrival. Receipt и stall diagnostics записывают выбранную пару. Короткими native диагностическими запусками проверены village_day@arrival, house_old_pc@entry, fap_clinic@waiting_room, kara_urman_night@village_path и отклонение unknown@bad; настройки/сохранения восстановлены byte-for-byte. Эти прогоны проверяют выбор зоны и валидацию, не FPS acceptance: полный packaged 12s warmup/60s sample ещё нужен. Steady sample не заменяет измерения движения, переходов или обычное прохождение.

### 2026-09-12 — parent-local anchor импортированных modular assets

GeneratedModularKitDressing принимает anchor в системе координат родителя. AlignAnchor ошибочно вычитал мировую позицию reference mesh из локальной позиции instance. В connected доме с origin (-28,0,0) это размещало весь OldPc_ GLB на X=0, на 28 м от стола; локальные декоративные экран и вентиляционные полосы оставались в доме. Runtime probe подтвердил расхождение всех восьми LOD0 деталей.

Теперь referenceMesh.GlobalPosition преобразуется через parent.ToLocal перед вычислением смещения. Проверены callers StyleBenchmarkZone, FullGameZoneDressing и AddAuthoredPine: они используют parent-local anchor (у forest сейчас identity transform). Явный rebase room-local HouseInterior сохранён. Существующий generated_modular_kit_contract_smoke_test обновлён минимально: parent смещён на -28 м и проверяется фактическая позиция reference относительно anchor. C# build и этот smoke прошли; native house/Кара-Урман кадры просмотрены. Корпус, клавиатура и системный блок теперь видны на столе; масштаб ПК остаётся отдельной художественной корректировкой. Диагностические GD.Print удалены. Доказательства: внешний crt_position_probe_capture.log, crt_anchor_fix_capture.log/frames, modular_local_anchor_smoke.log.

### 2026-09-12 — смещения PCK и native boot splash

`read_pck` в `eng/verify-act1-release-package.sh` теперь прибавляет базу данных из заголовка к относительному смещению каждого ресурса PCK v4. Нулевое смещение означает первый ресурс, а не отсутствующие данные. В R18 прежний reader начинал чтение PNG на 112 байт раньше; проверка текстовых маркеров могла пропустить эту ошибку. Семантика подтверждена владельцем формата: [Godot PackedSourcePCK](https://github.com/godotengine/godot/blob/master/core/io/file_access_pack.cpp). Это исправление проверки пакета, не runtime-рендера.

Существующий gate дополнен проверкой исходного PNG для заставки: одной импортированной `.ctex`, достаточной для меню, недостаточно при раннем старте движка. Обе сборки R18 содержат PNG 2 518 008 байт, побайтно совпадающий с исходником. Исправленный gate прошёл на обоих реальных PCK (284 записи), headless bootstrap достиг arrival, userdata восстановлены. Evidence: внешний `URMAN_ActI_Finish_20260911/boot_branding_r18`. Headless Windows PCK на Mac не подтверждает запуск Windows EXE; обычное появление заставки на разблокированном Mac остаётся открытым.

### 2026-09-12 — явная ошибка первого запуска

`Act1DemoRoot` перехватывает сбой собственной инициализации, записывает диагностику в журнал и завершает приложение с кодом 1. При обычном запуске перед выходом показывается штатный `OS.Alert` с коротким русским сообщением; headless не ждёт диалога. Сырые exception-тексты остаются в журнале. Порядок успешного синхронного запуска не менялся.

Исключения дочерних `_Ready` Godot перехватывает на native-границе, поэтому после `AddChild` проверяются завершённая сборка connected world, существующий session owner и игрок; после создания меню — его основная кнопка. `Act1ConnectedWorld` отдельно хранит факт начала сборки (защита от повторного входа и повторной сборки после сбоя) и `IsBuilt`, который становится true только в конце успешной сборки. Простое перемещение старого флага в конец вызывало бы рекурсию через `SetActiveLogicalZone`.

Диагностика до исправления: временно недоступная main-сцена оставляла исключение в журнале и код 0 после `--quit-after 2`. После исправления по отдельности временно подменены пути main-сцены, зоны, compiled campaign и иллюстрации меню: все четыре случая дали контролируемую ошибку старта и код 1. Исходники после каждой серии восстановлены, финальный C# build без ошибок/предупреждений; userdata восстановлены побайтно. Существующий `act1_demo_launch_smoke_test` прошёл обычный путь меню → новая игра → keyboard/gamepad intro → Act I. Это headless-проверка поведения; native-диалог ещё нужно увидеть после разблокировки Mac. Evidence: внешний `URMAN_ActI_Finish_20260911/startup_failure_r20`.


### 2026-09-12 — видимый текст списка при клавиатурном фокусе

`ItemList` рисует focus-плашку поверх строк. Непрозрачные `JournalFocus`
и `OldPcFocus` почти полностью закрывали список улик и полностью — список
архива. Это подтверждено native 720p кадрами при text scale 1,6.

Действующий `AccessibilityPresentation.ApplyToControl` для ItemList клонирует
непрозрачный StyleBoxFlat фокуса и отключает DrawCenter. Исходный общий ресурс
не меняется: hover кнопок и заливка LineEdit остаются прежними. Повторное
применение пропускает уже исправленный контур. В проекте два ItemList —
журнал и старый ПК; отдельные обработчики и новые темы не добавлены.

Проверены существующий JournalFlow (источники, неверное/верное сопоставление,
запись и повторное открытие), native 720p/1,6 и 1080p/1,0 для обоих списков,
а также поиск ПК с фокусом. Luna подтвердила A/B и отсутствие изменения
общего hover-ресурса. Временные capture-вставки восстановлены побайтно,
C# build 0/0 и userdata восстановлены. Новый набор тестов не добавлялся.
Evidence: внешние `journal_scroll_audit_r23`, `oldpc_focus_before`,
`itemlist_focus_v2` в `URMAN_ActI_Finish_20260911`.

Исходная гипотеза о потере прокрутки не подтвердилась: реальная справка
сохраняет scroll value 362 после закрытия/открытия. Поведение прокрутки
не менялось. Общая доступность и обычное прохождение остаются открытыми.

### Что попадает в поставляемый PCK (проверено 2026-09-14)

Вопрос возник из правила «не выпускать новый пакет ради каждой строки»: нужно было
знать, устаревает ли кандидат от правки тестов. Проверено прямо по артефакту —
поиск по PCK упакованного r45 (проверено повторно; PCK между r42 и r45 не менялся):

- `scripts/FirstPersonController` — 3 вхождения (игровые скрипты в пакете есть);
- `act1_first_person_corridor_smoke_test`, `tests/act1_demo_launch_smoke_test`,
  `SceneSmokeTest` — **0 вхождений** (тестовые сцены и скрипты в пакет не попадают).

Следствие для правила дрейфа: правка только тестов не устаревает кандидата, потому
что сам артефакт не меняется. Правки игровых скриптов, контента и ассетов —
устаревают, и тогда нужен новый экспорт и перенаправление документов.


## Windows test station — 2026-10-02

Прямое поручение автора: игровые запуски перенести с ноутбука на выделенный Windows-ПК.
Минимальная схема — штатный Remote и чат в основном Windows-checkout; собственного
сервера/MCP/Cloud-очереди не добавляем. Подготовлены `eng/setup-windows-station.ps1` и
`eng/run-windows-check.ps1`; закреплённые версии берутся из существующих global.json/toolchain.json.
Guard расширен на Windows: APPDATA, byte-range lock, остановка дерева процесса, прежние
rename/backup/recovery/fingerprint. Windows ACL отдельно не сравниваются. Станция использует
локальный override, отключающий импорт авторского .blend при наличии готового GLB.
Порядок: одна сборка/импорт/сцена под lock, лог и receipt; настоящий GPU-прогон пока not-run.
Подробности: [Windows-станция](../production/WINDOWS_TEST_STATION_RU.md).

### 2026-10-03 — неизменяемая проекция состояния при частых чтениях

`RuntimeKernel.SelectState` переиспользует JSON-снимок между успешными commit.
Инициализация/сброс защищены тем же `_dispatchLock`; effects меняют отдельный
draft, а rejected dispatch не сбрасывает снимок. Старый выданный JsonElement
сохраняет прежнее содержимое. CaptureSnapshot, restore, ledger и save-схема
не менялись. Это устраняет полную сериализацию при каждом кадровом чтении
bridge/opening, сохраняя единственного владельца состояния в RuntimeKernel.
Пять существующих KernelTests и отдельный CPU-микрозамер прошли; текущая
игровая производительность/M1 не измерены. Остальной аудит и ограничения:
[performance_audit_2026-10-03](../production/performance_audit_2026-10-03.md).

### 2026-10-03 — runtime стоимость скрытых character rigs / horse contacts

Character adapter оставляет selected Rig/Anchor до AddChild; per-instance
AnimationLibrary с копиями выбранных клипов удаляет чужие rest tracks,
не меняя shared imported animations. Idle/Tension/Talk/Walk и добавляемая
позднее retargeted библиотека остаются. World mesh census 70 430 → 62 913.
Horse fresh support повторно используется только при неизменившейся подошве
в текущем PreparePose; pending Rebase / swing / изменённый floor получают
прежнюю проекцию. Нулевая траектория пропускает sweep, но endpoint остаётся.
Native High M4Pro 1080p Debug после правок 23.83 FPS; измеримого общего
ускорения нет. Renderer CPU ~16.7 мс, ~34 311 shadow draws; GPU timer недоступен.
Следующий этап требует передачи W/common. Полная верификация и ограничения:
[perf audit](../production/performance_audit_2026-10-03.md).

Perf verification 03.10: текущая C#-сборка PASS, KernelTests 5 PASS.
NPC presentation: первый borderline sole-gap FAIL; original adapter baseline
PASS; повтор текущей правки PASS (4 turns/36 samples/10 families), threshold
не менялся. Idle-фаза не фиксируется: первый failure не объявлен предсуществующим.
Horse native route INVALID после 1.314 м, travel gate «нет подходящей дороги»;
движение/obstacles/save recovery не приняты. Guard во всех запусках подтвердил
восстановление userdata. Native High после правок 23.83 FPS: цель открыта.

### 2026-10-03 — полный доступ и продолжение performance-аудита

Автор отменил ownership/W-common ограничения; protected M4 Pro прогоны
разрешены. SelectWorldProps возвращает fragment неизменяемого Kernel snapshot
без второго parse/serialize. RuralPropGeometry владеет exact-size BoxMesh cache;
callers не меняют shared resources. ShelterPublicMaterials получает cached
полную копию материала без трёх snow flags через прежний Materials owner.
Сопоставимый fixed High arrival: 24.74 → 26.32 FPS, draw calls 41 378 → 36 809;
MSAA4/scale1/1080p, фактуры/геометрия/коллизии сохранены. Цель M1 открыта.

Address graph publication происходит после search physics budget. Longer native
прогон обнаружил до 2 490 segments и 131 мс rebuild; cut spatial index сохраняет
точные predicates/первый anchor/минимум creation index. Intersection append
устраняет heap iterators и endpoint arrays; диагностика включает phase timing.
Exhaustive mode отключает candidate indices и сохраняет тот же narrow phase.
Существующий AddressRegistrySmokeTest после обоих этапов прошёл (84 checks),
новые тесты не созданы. Steady/native стоимость новой версии ещё измеряется.

Static opaque shader без VERTEX writes исследуется отдельно только с
URMAN_STATIC_MATERIAL_PILOT=1. Общий fragment и varying расчёты сохранены;
snow/trample/wind материалы исключены, source two-sided treatment сохранён.
Default пока не включён. Triplanar zero-axis эксперимент отклонён и удалён;
его контроль получил movement input и не годится для сравнения.
Доказательства и ограничения — [perf audit](../production/performance_audit_2026-10-03.md).

### 2026-10-03 — прекращение игровых запусков по просьбе автора

Автор попросил продолжать только статически, чтобы ноутбук оставался доступным.
Игровые/native/headless запуски остановлены; последний guard подтвердил
восстановление userdata. Новые benchmark/smoke не разрешены до нового поручения.
Продолжаются статический аудит, точечные оптимизации и C#-компиляция.
Непроверенный static-material pilot удалён; первоначальный shader/culling
восстановлен. Новые horse disposal/identity правки ещё не измерены в игре.

Статический этап: собственные ray/shape results и exclusion arrays в horse
ground/endpoints/sweeps и AddressWalkProbe освобождаются в текущем scope.
В установленном GodotSharp 4.7.1 Array<T> не IDisposable; отдельный untyped
using-owner освобождает тот же underlying Array (подтверждено actual DLL IL).
Borrowed scene nodes/space/resources не освобождаются. Nearest сохраняет
distance/ordinal ordering одним проходом; лишний startup graph Rebuild удалён,
публикации новых проверенных путей сохранены. C# build PASS 0 warnings/errors,
11.05 с; git diff --check чистый. Последние игровые эффекты not-run; M1 цель
открыта, поздний O(N²) graph rebuild остаётся риском.

### 2026-10-03 — второй статический performance-этап

По просьбе автора три субагента параллельно проверили renderer/fog/shadows,
CPU/physics/audio и address graph, затем провели перекрёстный review. Игровых
запусков нет. ForColor выбирает RigidPainterlyShader для 30 явных семейств с
immutable zero deformation flags: единый VertexDeformation block удалён,
fragment/varyings/uniforms сохранены. Это разрешает existing Godot shared
shadow path; TwoSided/Cutout/unknown/snow/wind сохраняют прежние shaders.
Потенциальная точность imported ShadowMesh и GLSL/pixels/FPS ещё не проверены.
Road pair index объединяет padded spatial и near-parallel direction
кандидаты, сохраняя numeric predicate/i-j order/exhaustive fallback и bounded
index memory. Плотные наборы всё ещё дороги. Vehicle audio использует один
span-buffer вместо per-sample calls; exact transform guards не инвалидируют
неизменные деревья/коллайдеры. Snow binding memo зависит от exact texture
reference/origin/extent/cacheCount, сбрасывается при ClearCache.
Метель fog=.032 не согласована с реальным culling: camera Far160, High
shadows120. Самовольное уменьшение дистанций/числа частиц не внедрено.
Текущая C#-сборка PASS 0 warnings/errors, 10.90 с; diff-check чистый, Godot не
запускался. Подробные proof/unknowns — production/performance_audit_2026-10-03.md.

### 2026-10-03 — третий статический performance-этап

Пять read-only аудитов (rendering, CPU, physics/NPC/vehicles, world/algorithms,
weather/particles) и четыре исполнителя на непересекающихся файлах; игровых,
импортных, headless- и тестовых запусков нет.

Проверено по исходникам Godot 4.7-stable, а не по памяти: счётчик
`ViewportRenderInfoType.Shadow` включает теневые проходы всех типов источников
(`render_forward_clustered.cpp:2850`); `VisibilityRange` учитывается и для
теневых кастеров по расстоянию до камеры (`renderer_scene_cull.cpp:2924`), т.е.
LOD деревьев уже ограничивает их вклад в тени; `ArrayMesh.ShadowMesh` — это
позиционный прокси с теми же треугольниками и той же LOD-цепочкой
(`importer_mesh.cpp:955-1046`), поэтому он снижает только вершинную полосу
теневого прохода. Ни один способ сократить число теневых кастеров без изменения
вида не найден: `CastShadow` у ground-cover, 13 интерьерных омни и оконных
спотов убирают видимую тень при солнце 16° и в интерьерах.

Внедрено без изменения вида: кэш `SettlementRegistry.StableId`; топологическая
фаза `SettlementRoadGraph.RebuildCore` без per-segment LINQ (эквивалентность
проверена внешним harness на 400 000 враждебных наборов); `NodeAt` одним
проходом; индекс кандидатов дорожного графа больше не отключается целиком при
превышении бюджета ячеек (сегмент дедемитируется и сравнивается со всеми,
кандидаты — надмножество, порядок пар сохранён); кадр лошади в `VolumeOverlaps`
считается один раз вместо ~18 полных `PreparePose` за такт; `SettlePose`
переиспользует уже доказанную в том же такте опору ноги; собственные результаты
запросов освобождаются детерминированно; одна оценка контекста на такт в
`AddressAccessVerifier`; аллокации и групповые поиски в per-frame путях
транспорта, мира, звука и facility-тика (facility больше не переписывает
константные `CollisionLayer`/`Visible` и трансформ стоящей двери; `door.Target`
Prompt/ray-слой намеренно не мемоизируются). Удерживаемые статические кэши
частиц получили тестовый сброс по существующему паттерну.

Переписывание маски следов на `GetData`/`SetData` **опровергнуто** как побитово
идентичное: `Math::make_half_float` усекает мантиссу и обнуляет денормали, а
`System.Half` округляет к ближайшему чётному. Читается как кандидат с точным
путём (дословный порт RGBAH-энкодера), не внедрялось. Цель M1 открыта; игровой
эффект и FPS не измерены.

### 2026-10-03 — четвёртый статический performance-этап

Разбор исходников Godot 4.7-stable уточнил условие дешёвого теневого пути:
`SceneShaderForwardClustered::ShaderData::uses_shared_shadow_material`
(`scene_shader_forward_clustered.h:297-300`) требует непрозрачный back-culled
материал, не пишущий VERTEX, без alpha clip/discard и `world_vertex_coords`.
Общий shadow material (и позиционный `ArrayMesh.ShadowMesh` вместе с ним) доступен
только такому материалу; полный painterly-шейдер пишет VERTEX всегда. Отдельный
флаг `is_animated()` управляет лишь пометкой теней позиционных источников
(`make_shadow_dirty`), и у rigid-материалов он становится false — это узкий
остаточный риск: idle-анимация скелета на месте в радиусе omni/оконного спота
может не обновлять силуэт своей тени до следующего изменения трансформа.

Поэтому выбор `RigidPainterlyShader` в `PainterlyMaterialLibrary.ForColor` больше
не ограничен списком 30 семейств, а определяется флагами, при которых блок
деформации доказуемо инертен: `trample_ground_surface == false` (guard самой
`snow_trample_at`), `has_snow_micro == false`, `wind_sway == 0`. Эти три флага
пишутся только в самой фабрике, а `has_snow_micro` позже только опускается
(`WithoutSnow`), так что инертный при создании материал остаётся инертным.
Cutout/two-sided варианты используют другие шейдеры и не затрагиваются;
snow-поверхности и семейства с ненулевым sway сохраняют полный шейдер.
Вырезание блока побайтово точное по построению: `ShaderSource` собирается как
конкатенация литералов с `VertexDeformation` между ними. Видимый результат не
меняется; эффект на теневой проход не измерен (игра не запускалась). Census
`URMAN_PERF_RENDER_DIAGNOSTICS=1` теперь пишет `sharedShadowPathSurfaces` и
`otherShaderMaterialSurfaces`, чтобы следующий замер проверил охват этого перехода.

Отдельный вывод этапа: отношение shadow/visible ≈ 4.85 устроено геометрией
каскадов при солнце 16° (бокс дальнего каскада ≈ 184 × 210 × 60 м), поэтому
дальнейшее сокращение теневого прохода возможно только решением автора
(дистанция/число каскадов, высота солнца, тени целых классов геометрии или
LOD-упрощение рантайм-мешей), а не визуально-нейтральной оптимизацией.

### 2026-10-03 — первый живой замер (M4 Pro, High, 1080p)

Автор разрешил игровые запуски; пять прогонов через `eng/protected_run.py` (копия
и восстановление userdata с проверкой fingerprint), 1920×1080, High, scale 1.0,
MSAA 4×, без VSync, прогрев 12 с, замер 15 с, 100 % фокуса, `village_day@arrival`.
Хост — M4 Pro, не M1, то есть это измерение эффекта правок, а не приёмка целевого
устройства.

Итог: кадр ограничен **объёмом directional-теней** и **physics**, а не пикселями.
Базовая High — 26.23 FPS (38.13 мс, p95 59.5, 97 % кадров длиннее 33.3 мс).
`URMAN_PERF_SHADOW_DISTANCE=45` (тот же билд, только дальность) — 40.47 FPS
(+54 %), 8.06 % длинных кадров, shadow draws 30 079 → 14 284, renderer CPU
16.16 → 8.42 мс. `URMAN_PERF_SHADOW_SPLITS=2` — 33.95 FPS (+29 %).
`URMAN_PERF_SCALE=0.70` — 25.93 FPS, то есть заполнение/разрешение/MSAA на этой
сцене не рычаг. physics 13–14 мс не меняется ни от теней, ни от scale; кадр
складывается как physics + renderer на главном потоке. Фоновая проверка адресов
активна всё окно замера (pending 29–36) и расходует 2-мс бюджет каждого
физического такта. Правка выбора rigid-шейдера по флагам измерена как нейтральная
(58 568 против 58 573 поверхностей на общем теневом пути из 95 279; FPS в пределах
разброса), временный переключатель этого A/B удалён.

Отдельный замер с прогревом 60 с (очередь проверки адресов опустела,
`completed=true`) показывает установившийся режим: **31.93 FPS**, avg 31.32 мс,
p95 33.54, p99 45.64, длинных кадров 5.43 % против 96.97 % при 12-секундном
прогреве, physics 10.91 против 13.14 мс. То есть фоновая проверка адресов не фон,
а дестабилизатор темпа на первой минуте, и **все замеры с коротким прогревом
(включая прежние baseline 23.83/24.02 FPS) измеряли первую минуту, а не
установившийся режим**. Установившийся кадр 31.32 мс ≈ renderer 16.71 + physics
10.91 + ~3.7, и доминирует теневой проход. Полная таблица установившихся режимов
(прогрев 60 с, отличается ровно одна ручка теней): 4 каскада/120 м — 31.93 FPS;
2 каскада — 40.81; 45 м — 48.68; **2 каскада + 45 м — 57.09 FPS, p95 19.1 мс,
длинных кадров 0.35 %** при неизменных разрешении, MSAA, scale, геометрии,
текстурах и материалах. То есть целевая полоса 50–60 FPS достигается на этом
хосте только комбинацией двух теневых настроек, а решение о них — авторское
(меняется распределение и дальность тени, не качество картинки в целом). При этой
комбинации physics (11.35 мс) становится больше половины кадра 17.51 мс, поэтому
следующий рычаг — физика (лошадь, сервер, бюджет фоновой проверки адресов).
Managed-профиль показал, что это **Debug**-сборка: главный поток во время сборки
мира тратит ~33 % на `godotsharp_method_bind_ptrcall`, ~14 % на хеширование в
`DisposablesTracker`, ~17 % на создание/уничтожение managed-строк и ~10 % на
`godotsharp_instance_from_id`, а поток финализатора занят почти на 100 % на
`Array.Finalize`/`Dictionary.Finalize`/`StringName.Finalize`. Значит абсолютные
числа — нижние оценки: перед срезом качества нужен замер Release-сборки и приёмка
на M1.
Подробности, таблица прогонов и команда воспроизведения —
production/performance_audit_2026-10-03.md.

### 2026-10-03 — стоимость одной пересборки адресного графа снижена (статически)

Живой профиль показал, что рывки первой минуты делает именно публикация адресного
графа: 30 пересборок за 27 с, медиана 9.6 мс, все 26 сопоставленных попали в кадры
>33.3 мс. Внутри пересборки при 1 184 сегментах узкая фаза — 6.6 мс на 22 328
протестированных пар (296 нс на пару), то есть стоимость чтения полей разбросанных
объектов `Segment`, а не арифметики. Внедрено: входы предикатов узкой фазы
(`SegmentBounds` 40 байт + приращения концов) упакованы в массивы (объекты
`Segment` в горячем цикле не разыменовываются, только для принятых пар), словари
ячеек переведены с кортежного ключа на `long`-биекцию `((long)x<<32)|(uint)z`, а
группировка — с `SelectMany().OrderBy()` на заранее выделенный список с полным
компаратором (ключ по `CompareOrdinal`, затем позиция), что даёт ту же
перестановку, что стабильная сортировка LINQ. Доказательства: сборка 0/0,
харнесс на 20 000 случайных последовательностей (перестановки совпали, 0 коллизий
`CellKey`), независимое ревью diff. Ожидаемый эффект ≤1–2 мс из ~10 мс на
пересборку; живой замер не выполнялся — игровые запуски в текущем аудите
приостановлены поручением автора.

### 2026-10-03 — статический этап после замеров (запуски приостановлены)

По `AGENTS.md` игровые запуски в текущем аудите приостановлены, поэтому дальше —
код и статический разбор. Скан всех `_Process`/`_PhysicsProcess` тел на аллокации
коллекций, обходы дерева, неосвобождённые физические запросы, LINQ и создание
физических тел не нашёл ничего, кроме одного уже оптимизированного места, — per-tick
C#-работа закрыта, и оставшиеся ~11 мс physics это серверный шаг движка, цепочка
лошади (свежие проверки обязательны) и 2-мс бюджет проверки адресов.

Исправлено: четыре предиката модальности (`InteractionTarget`, `BathIgnitionChoiceUi`,
`RuntimeBridge.HasActiveMainMenu`, fallback в `VehicleFleet`) вызывали
`GetNodesInGroup("main_menu").OfType<MainMenuUi>().Any(...)` — нативный `Array<Node>`
плюс LINQ на каждый вызов и без освобождения массива, в том числе в покадровых
подсказках. Теперь один общий `MainMenuUi.AnyUndismissed(SceneTree)`: общий случай
решает `GetFirstNodeInGroup` без аллокации, полный перебор остаётся fallback'ом и
освобождает владеющую обёртку, решения не меняются.

Census живых прогонов разобран по владельцам: 21 402 поверхности кадра видимости →
3 850 ключей (mesh, material, mirror), переиспользование 5.56; внешний мир 14 237 →
259 (инстансы работают), крупнейшая оставшаяся группа —
`Act1AuthoredExteriorKitPresentation` 876 → 870, и это **импортированная сцена**
(`assetSource = VillageExteriorKitScenePath`), поэтому интернирование примитивов и
шаринг материалов из кода её не исправят — только рантайм-слияние геометрии по
(фасад, материал), которое меняет локальный culling и уже измерялось отрицательно
на другом контенте; это кандидат под замер. Владельцы с переиспользованием 1.00 —
содержательно уникальные (85 табличек адресов с разными номерами, памятные
таблички, четыре транспорта) и не схлопываются без изменения картинки.

Незакоммиченный рефакторинг `SettlementRoadGraph.cs` от параллельного исполнителя
(упакованные массивы узкой фазы, длинные ключи ячеек, группировка без LINQ) был
проверен чтением на эквивалентность и закоммичен отдельно, чтобы работа в общем
checkout не потерялась; ожидаемый эффект — меньше измеренных 9.6 мс медианы
пересборки, но он не измерен.

### 2026-10-03 — две линейные стоимости на публикации адреса убраны (статически)

`AttachVerifiedAddressAccessPaths` вызывается по разу на каждый проверенный адрес
(источник рывков первой минуты), и внутри него: (1) `RefreshVerifiedWinterFootpaths`
пересчитывал строку revision для **всех** уже проверенных маршрутов на каждом
attach, хотя сверка с метаданными меша шла после вычисления — теперь revision
мемоизируется по ссылке на массив маршрута (`CommitAddressAccess` кладёт свежий
массив из `Compact`, массивы не мутируются), запись мемо снимается вместе с
устаревшим мешем; (2) `NodeAt` линейно сканировал все узлы на каждый вызов (по
разу на маршрут при каждой публикации и на старте поиска каждого адреса) — теперь
есть индекс узлов по метровым ячейкам, а допуск `.001` гарантирует, что подходящий
узел лежит в 3×3 окрестности, поэтому тест `DistanceXZ<.001` и тай-брейк по
наименьшему id сохранены. Индекс строится в конце `RebuildCore` после заполнения
`_nodes`, который больше нигде не меняется. Сборка 0/0; независимое read-only
ревью; живой замер не выполнялся — игровые запуски приостановлены поручением
автора.

### 2026-10-03 — меньше мусора в сборке мира (фасады адресов)

Managed-профиль сборки мира: ~17 % на создание/уничтожение строк, ~5 % на
`StringName`, ~10 % на `godotsharp_instance_from_id`, поток финализатора почти
100 % занят `Array.Finalize`/`Dictionary.Finalize`/`StringName.Finalize`. В
`AddressFacadeMount` (выбор места таблички адреса) рекурсивный обход потомков
больше не оставляет владельческие массивы `GetChildren()` финализатору, а шесть
ключей метаданных в двух горячих предикатах стали статическими `StringName` вместо
построения на каждый вызов `HasMeta(string)`. Значения и решения не меняются; это
стоимость сборки мира, а не кадра. Остальные ~130 мест `GetChildren()`/
`FindChildren()` — отдельный механический проход, не в этом коммите.

### 2026-10-03 — независимая проверка правок второго исполнителя

Три коммита параллельного исполнителя (`1ef6a246` — мемо проверки стойки внутри
такта, `509b8e5c` — освобождение массива обхода фасадов и кэш `StringName`,
`ae282bdc` — предикат главного меню без аллокации) проверены read-only: **APPROVE
по всем трём**, обязательных исправлений нет. Проверено по коду: мемо ключуется
кадром, точной позой и всеми входами запроса, а не входящие в ключ `CollisionMask`
(её переключает `FirstPersonController.Movement.cs`), массив исключений и
`IsInsideTree()` безопасны только потому, что драйвер аудита не возвращается к
движку внутри такта; `AnyUndismissed` даёт то же множество, что прежний LINQ-предикат
(и в установившемся режиме не выделяет массив, так как `Dismiss()` вызывает
`QueueFree()`). Оба инварианта закреплены комментариями в коде.

### 2026-10-03 — освобождение результатов физических запросов в горячих путях

Профиль сборки мира показывал поток финализатора занятым почти на 100 % на
`Dictionary.Finalize`/`Array.Finalize`. Скан всех `_Process`/`_PhysicsProcess` тел
подтвердил, что прямых запросов там нет, но по проекту нашлось ~20 мест, где
созданные на каждый вызов объекты Godot оставались финализатору, в том числе на
постоянной частоте: по лучу на каждый метр шага расписания
(`AuthoredWorldDirector.Schedules.FirstObstacle`), группы опор и проверка выхода из
машины, `SpinClear`, валидация парковки, `RinatPresencePresentation.GroundAt`,
луч опоры на каждый шаг в `FootstepAudioController`. Внедрено освобождение: `using`
для `PhysicsRayQueryParameters3D`/`PhysicsShapeQueryParameters3D`/`Dictionary` и
`using var owner = (global::Godot.Collections.Array)typed;` для `Array<Rid>` (в
GodotSharp 4.7.1 `Array<T>` не `IDisposable`, освобождает неутипизированный
псевдоним). Нативная сторона держит собственную ссылку на массив исключений, все
чтения результата происходят до конца `using`. Значения и порядок запросов не
менялись; на установившийся кадр напрямую не влияет, убирает поток мусора в
финализатор.

### 2026-10-03 — владение массивами `SurfaceGetArrays` в сборке мира

`Mesh.SurfaceGetArrays(s)` каждый раз создаёт принадлежащий вызывающему `Array` и
копирует в него всю поверхность; 39 мест оставляли эту копию финализатору, почти
все — в сборке мира (лес/кит, геометрия деревни, модели транспорта). Внедрено
`using var arrays = …SurfaceGetArrays(…)`, для `.Duplicate(true)` — владение и
источником, и копией, а для чтений внутри проекций — помощники
`SurfaceVertices`/`SurfaceBytes`/`RidgeSurface`, возвращающие уже скопированные
managed-данные (в `RidgeSurface` заодно убрано двойное чтение меша для вершин и
индексов). Безопасность подтверждена исходником Godot 4.7-stable
(`scene/resources/mesh.cpp`): `add_surface_from_arrays` принимает `const Array &`,
только читает его и сохраняет скопированные данные, а `surface_get_arrays`
возвращает новый массив. Значения, порядок вызовов и решения не менялись.

### 2026-10-03 — класс владения физическими запросами закрыт по проекту

Второй проход тем же приёмом перевёл на владение ещё тринадцать файлов: бани,
мечеть (включая исключения и результат `IntersectShape` внутри цикла по шагам
двери), загрузку сохранения в мечети (пять мест), обувь, общественные здания
(объект параметров не освобождался вообще), магазин, лестницу, портативный фонарь,
контакт колеса, отладочную сетку и все места `CarryCoordinator`. Отдельная находка —
межметодное владение: `CarryCoordinator.Trace(...)` возвращает `Dictionary`, и все
восемь вызовов его не освобождали (часть — покадровые пути прицеливания), теперь
освобождают. Повторный скан по проекту даёт ноль мест, где созданный на вызов
физический объект остаётся финализатору; остаются только два корректно владеемых
кэшированных поля (`AlsuStreetWalkPresentation._groundRay` и
`FirstPersonController._stanceProbeQuery`), которые освобождаются в `_ExitTree`.
Первый проход независимо проверен (APPROVE 8/8); второй помечен как проверенный
только автором — делегирование ревью в сессии недоступно (лимит глубины субагентов),
ждёт внешнего ревью. Сборка 0/0.
Независимое ревью (APPROVE 8/8) доказало семантику по исходникам Godot/GodotSharp:
`mesh_surface_get_arrays` возвращает свежесозданный `Array` (владелец — вызывающий),
`add_surface_from_arrays` принимает `const Array &` и сохраняет уже скопированные
данные, `Marshaling.cs` копирует packed-массивы в managed (`new Vector3[size]` +
`Buffer.MemoryCopy`), `Duplicate` даёт независимый массив, а `using` внутри
`yield`-итератора освобождает на каждой итерации (проверено харнессом). Ревью нашло
один пропущенный сайт (`Act1ConnectedWorld.cs:9134`), он исправлен; итоговый скан —
40 вызовов `SurfaceGetArrays`, 0 без освобождения.


## Windows test station — 2026-10-05

Development stays in the current checkout. Automatic game checks use eng/remote-check.py, an exact SHA256 source snapshot, private Tailscale HTTPS, and one interactive Windows worker reusing run-windows-check.ps1 and protected_run.py. ContentCli refreshes both campaign packs before the fresh C# build and guarded import. Receipts bind results to the snapshot, generated content and DLL; local play keeps the existing launchers/save contract. Setup and connection authority: docs/production/WINDOWS_TEST_STATION_RU.md.

Capture checks/disposes the saved Image and uses the existing GodotSmokeCleanup before quit. VehicleImmersionDetails builds sibling presentation nodes on its existing first physics tick instead of the parent-blocked _Ready; Windows guard retries transient PermissionError renames for at most five seconds. Both changes address failures observed during station acceptance; normal local play remains unchanged.

Тестовая очистка освобождает также detached library scene AnimationCatalog (ual1_standard.glb): диагностика orphan nodes обнаружила её сохранение после capture. Освобождение выполняет существующий GodotSmokeCleanup после закрытия сцены; обычная игра сохраняет кэш.


## Windows baseline: восстановление исполнения материалов — 2026-10-08

Защищённый native capture PW-001 (`ae59d933fe394be597135bf7998c94a3`, snapshot
`c1e132b9152848cf495eb5a4084028ceac804d9e95aea1b0b72f2f763bb3c2f2`)
дошёл до кадров, но завершился FAIL: ошибки компиляции шейдеров оставили мир
серым. Это диагностический срез, а не подтверждение художественной готовности.

В существующем `PainterlyMaterialLibrary` исправлены sampler hint
`hint_default_white` и передача authored UV из `fragment()` в helper явным
аргументом. Все производные варианты используют тот же источник шейдера.
`VillageWindowMaterials` передаёт frost normal через `NORMAL_MAP` в tangent
space и `NORMAL_MAP_DEPTH=ice`; смешение tangent normal с view-space `NORMAL`
и несуществующий `NORMAL_ROUGHNESS` удалены. Авторский диапазон roughness
0.42–0.83 сохранён. Контракт пространств сверён с
[Godot 4.7 spatial shader reference](https://docs.godotengine.org/en/4.7/tutorials/shaders/shader_reference/spatial_shader.html).
До нового native receipt и просмотра кадров эти правки не считаются проверенным
восстановлением материалов; C# compilation не компилирует shader source.


Следующий native срез `d624619b297f48d0bd2f06bb59b96d8f`
(`a39d980ced05b3ab859122249a8b84ff99589c3d8a6d834330732dd466796a95`)
не содержит прежних shader parse/metadata/curve ошибок, но не дал ни одного
кадра: буфер instance uniforms исчерпан, журнал переполнен повторными ошибками.
Зират измерен 1.03 м, mosque mount найден; визуальная приёмка ещё не выполнена.
На Windows подтверждён Vulkan Forward+, GTX 970.

Godot 4.7 резервирует 16 uniform slots на instance, даже если shader объявляет
только три instance-параметра. Default 65 536 slots допускает около 4 096
таких мешей. Для текущего плотного мира установлен явный project budget
1 048 576 slots (65 536 instances до расхода глобальных параметров, 16 MiB
GPU storage buffer). Это сохраняет общие материалы и существующие
индивидуальные pigment/shelter/support параметры, а не клонирует материал
на каждую доску и не отключает проверки. Число actual mesh candidates
фиксируется диагностикой следующего capture; отсутствие overflow и кадры
нужны для подтверждения бюджета, будущие фотомиры этим ещё не проверены.
Источники: [ProjectSettings 4.7](https://docs.godotengine.org/en/4.7/classes/class_projectsettings.html#class-projectsettings-property-rendering-limits-global-shader-variables-buffer-size),
[renderer RD allocation](https://github.com/godotengine/godot/blob/4.7-stable/servers/rendering/renderer_rd/storage_rd/material_storage.cpp#L1887).

Чистый native baseline `441d83b0a1714e7d815e8c96673e6fd8`
(`64ed22b96b7e4ad87d361edbe6a7931c674f078e5efcb5e4ff5cdef6dc8964e3`)
подтвердил восстановление shader compilation и отсутствие overflow: native exit 0,
пустой engine-errors.log, три кадра с материалами, userdata restored/verified.
Census: 63 109 мешей, 54 781 shader-instance candidates, budget 1 048 576 slots;
запас текущего среза — примерно 10.7 тысяч candidate meshes, не гарантия будущих
фотомиров. Root принял воспроизводимую базу PW-001, не общий арт/performance PASS.
Табличка мечети на этом снимке не видна; её subsequent ADDR fix проверяется отдельно.
Точный base-plus-runtime patch и ограничения — `../../evidence/PW-001/`.

## PW-032: привязка слоёв pigment к поверхности — 09.10.2026

В существующем `PainterlyMaterialLibrary` один `pigment_position` теперь выбирается
в общем fragment scope: авторский UV при `bound_uv_pigment`, иначе уже вычисленные
локальные координаты при `local_wood_texture`, иначе world. Static `local_floor_texture` сохраняет
мировой pigment/cell grid: локальная привязка albedo не означает перенос 6-метрового
распределения оттенка на каждый отдельный столб/доску.
Мазок, macro stain, cell tint и finish grain используют этот выбор. Albedo binding,
материальный cache, UV/offset/scale владельцы и world-bound поверхности сохраняют
свои существующие пути. Для движущейся ткани сохраняется `ForMovingCloth`, для
доски — `ForLocalWoodPiece`; новых флагов или параллельного material owner нет.

Причина: albedo уже следовал UV/local, но pigment локальной доски и общий finish
noise следовали world position. Это могло менять рисунок при переносе/повороте.
Координаты назначаются до vertex wind deformation, UV сохраняется на mesh. Свет,
направление normal и контакт с землёй имеют отдельную физическую семантику и не
переводятся искусственно в UV. Это лишь ограниченный source slice PW-032:
GPU compile/rendering подтверждены native job `6fc0fe2b4aac4c4f847bad25eea78bd3`.
Фактическое движение всех потребителей, AS-12/16, wind/normals/shadows и
художественная приёмка остаются открытыми.

### PW-005: flush физического владельца перед сериализацией

Native diagnostic `02fedfa24c9344ee85e89ad87702f9a0` выявил раннюю запись Алсу:
SaveSlot сохранял Y=0 до проверки опоры, затем physics подтвердил Y=.09397566.
При загрузке actual-pose это нарушало существующий допуск опоры .04 м. Теперь
`FlushActualPoseAsync` после ReadRuntimeState ждёт существующий
`CompleteLoadedPhysicalProjectionAsync`, перепроверяет session/живого владельца
и только затем сериализует позу. Внутренние зоны сохраняют прежний exact-pose
путь самого projection method. Поддержка/коллизия и ошибки не ослаблены.
Существующий smoke допускает нормализацию только предварительно неготовой позы,
требует готового post-save владельца и точного saved↔after совпадения; для уже
готовой позы также сохраняет before↔saved равенство. Causal archive и ограничение
временного окна — `../../evidence/PW005/save-readiness-cause-02fedfa.json`.

### PhotoWorld typed command slice — 2026-10-09

`PhotoWorldCommandHandler` maps authored PhotoWorld narrative effects to typed `pw1.*` commands through one canonical operation table. Mixed effects remain one `pw1.interaction.apply` transaction. The existing ContentApply planner, PhotoWorldState reducer and RuntimeKernel occurrence ledger own mutation; Bridge captures and validates session identity across asynchronous dispatch/reconciliation. Windows semantic fullflow and duplicate-occurrence rejection passed (`evidence/PW-006/receipt.json`). Visit/world physical command producers remain unfinished: compiled logical anchor IDs and last spawn pose do not prove a physical safe origin, range or readiness. The runtime catalog/anchor owners must supply real bindings before successful prepare/commit; no synthetic ticket is accepted as physical proof.

### Rinat cloth UV source refresh — 2026-10-09

The canonical v2 character Blender source lacked metric UV metadata for Rinat clothing; skirt/hat/sash also lacked UV maps. `generate_character_kit_v2.py --refresh-cloth-uv Rinat --export` opens the complete existing source, refreshes only the allowlisted character cloth LOD pairs through the existing metric rest-surface helper, and preserves the full kit. Numeric skin/hair material suffixes stay outside the cloth route; mixed cloth/skin meshes and generation flags are rejected. Source audit preserves all other meshes, rigs, images/materials and 36 clips. Protected native fb2a4303 confirms all 14 staging Rinat cloth surfaces use metric `uv-local` and UV-bound pigment. Root accepted the bounded source/material result in Idle front/profile stills; full walk/talk, quantitative motion threshold, other cast and whole art acceptance remain open (`evidence/PW-032/rinat-cloth-uv-refresh-2026-10-09.json`).
