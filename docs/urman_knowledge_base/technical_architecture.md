# Technical Architecture

Target stack, accepted 2026-08-10: **Godot 4.7.1 .NET, C# и .NET 10 LTS**. Production target — ходибельный 3D-мир с постоянной камерой от первого лица для macOS и Windows. Browser и mobile не входят в релиз.

Engine-neutral остаются narrative kernel, portable content, compiler contracts и save data. Они не зависят от Godot nodes, scene paths, physics implementation или rendering API. Godot — единственный production owner представления, input, audio и world scenes.

## Current delivery boundary — Act 1 demo

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

Миграция `docs/modular_migration/MM-00`–`MM-80` завершила историческую архитектурную подготовку web-oracle к достройке MVP, но не объявляла MVP готовым. В текущем target Production Chapter 1 и full-game campaign — заменяемые C# compiled campaigns, а не встроенный сценарий.

- Portable source of truth — JSON/Markdown в `content/modules/**` и campaign manifests; compiler создаёт immutable `CompiledContentPack` и блокирует duplicate ID, неизвестные opcode, недостижимые обязательные точки, missing assets/providers и раннее раскрытие улики.
- `RuntimeKernel` — единственный writer progression state и occurrence ledger. Обычный квест/персонаж/диалог/asset меняется данными; уникальная механика приходит capability provider с exact version.
- `CampaignManifest` фиксирует ordered modules, role bindings, capability requirements и entrypoint. `GameSnapshotV2` хранит campaign lock/fingerprint; несовпадение не делает partial load и требует явный reset.
- `main` — единственный browser composition root. Он собирает `RuntimeBootstrap` и named persistence gateway; `Game` получает только presentation facade, без kernel, registry, capability host, raw storage или story IDs.
- Gateway — единственная production storage boundary. Он один раз удаляет четыре pre-MVP v1 keys, сохраняет username/preferences и Content Lab namespace, а current V2 save удаляет только после exact typed reset proposal.
- `Content Lab` и legacy MainMap остаются development-only. Old PC/DedOS — отдельный capability module; production не подключает legacy paths без явной campaign registration.
- Практический authoring flow и data-only side quest example закреплены в `content_authoring_guide.md`; это entrypoint для следующей ЛЛМ, а schema/contract authority остаётся в `content/schemas/**` и `docs/modular_migration/**`.

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
