# Mindmap

```mermaid
mindmap
  root((УРМАН))
    Current demo target [MVP][TECH]
      `game/scenes/act1_demo.tscn` default launch
      16 Chapter 1 beats / five local builders composed into one map [OPEN]
      Arrival → old PC → FAP → zirat → Kara-Urman edge
      Connected Act I greybox is the primary blocker [MVP][TECH][RISK]
        One world: entry → road → Babay/Äbi yard + house → village street → FAP → zirat → forest edge
        360° first-person continuity / near-mid-far review required
        Benchmark frames, texture candidates and FPS probes are supporting evidence only
      Shared journal, dialogue, vocabulary and SaveGameV3 state
      Physical first-person corridor smoke [TECH]
        Old PC → FAP documents → Rinat dialogue → Татарвики reread
        Shared DocumentUi handoff and Kara-Urman cliffhanger
      Physical MoveAndSlide walkthrough 135.49 m [TECH]
        No player-position writes; floor/spawn/collision regressions covered
      Mandatory physical dialogue gates [MVP][TECH]
        Mansur → old-PC access → official Marat notice
        Gulsina/әби `ярамый` → house exit
        Alsu contradiction → FAP route → Naila medical-record desk
        Journal stale oracle corrected; RuntimeBridge remains sole state owner
      Act 1 zone spawns face the next route landmark [MVP][TECH]
        Destination yaw for house → street → FAP → zirat → Kara-Urman
        Corridor smoke asserts the presentation contract; player look remains free
        Human wayfinding, comfort and performance hardware still [OPEN]
      Interaction availability invalidation [TECH]
        RuntimeStateChanged refreshes cached targets; no per-frame kernel JSON polling
        Act 1 ending detector uses the same notification; no frame-loop state serialization
        Target-host FPS and weak-GPU validation remain [OPEN]
      «НЕ ОТВЕЧАЙ» / «Конец демо» fade-to-black
      Acts 2–5 and web retirement deferred [OPEN]
    Full Release [NARRATIVE][TECH][ASSET]
      6–8 hours
      Five acts
      One tragic ending
        Айдар destroys the pact
        Кырлай loses protection
      Acts 2–5 authored foundation [NARRATIVE][TECH]
        46 compiled beats
        12 walkable zones with zone-specific dressing
        Narrative lock: beat sheets / clue graphs / language / threat beats [NARRATIVE]
        `narrative_lock_acts_2_5.md` accepted handoff
        Татарский and cultural review pending [LANG][RISK]
        Production art, NPCs, authored audio and cultural review pending [RISK]
      No combat
      Rare authored chases
      Russian release UI
      Keyboard mouse gamepad
    Godot Migration [TECH][RISK]
      Godot 4.7.1 .NET
      C# .NET 10 LTS
      Urman.Core engine-neutral
      Urman.Content portable compiler
      Urman.Godot presentation owner
      SaveGameV3 no V2 importer
      AccessibilitySettingsSnapshot in SaveGameV3 [TECH]
      Zone ambience manifest + AmbientAudioDirector technical pass [TECH]
        Reduced motion / head bob override
        High contrast and text scale
        Captions and non-audio cues
        External readability review pending [RISK]
        Execution orchestration [TECH]
        set_goal north star
        execution_backlog.json machine queue
        backlog.md human index
        Evidence-gated completion
        No web retirement before release gate [RISK]
        release_gate_matrix.md separates PASS foundations from OPEN release rows [TECH][RISK]
      Web runtime parity oracle
      Final delete-first cutover
    Painterly Low-Poly 3D [ASSET]
      First-person camera
      Compact walkable zones
      Stylized low-poly geometry
    Painterly materials light fog
      Authored road-relief candidate in style/full-game paths [ASSET][TECH]
      Six non-destructive v2 albedo candidates [ASSET]
        Wood/plaster/earth/foliage in-memory scene QA [TECH]
        Stone/fabric v2-only presentation owners: 2 stone anchors + 3 rug meshes [TECH]
        Focused six-material v3 siblings: image gate + test-only motion sweep [ASSET][TECH]
        v4 earth/wood HOLD/REWORK: drawn ruts, seams and shared-owner striping [RISK]
        v5 earth/wood rework: technical image gate + isolated v3↔v5 A/B receipt [ASSET][TECH]
          9 Metal/Forward+ sheets, 120/120 relief/contact samples
          Separate v5 27-cell near/mid/far × FOV sweep [TECH]
          Earth rendered relief-only delta still unproven; production OPEN [RISK]
        v6 earth/wood rework: quieter earth masses + abstract wood wash [ASSET][TECH]
          v5↔v6 A/B: 9 sheets, 120/120 contacts; v6 27-cell sweep [TECH]
          Earth delta subtle; wood owner/orientation review open [RISK]
        Near/far/20–30 m readability, geometry, wetness and art lock remain open [RISK]
        Test-only puddle roughness sweep + relief-contact gate [TECH][RISK]
        StyleBenchmarkZone ambient/fog/key-light calibration: technical PASS, visual gate open [TECH][RISK]
        Runtime v1 mapping remains active; v3 never becomes fallback [TECH]
      Blender 4.5 LTS generators
      Host-independent asset registry preflight: 36 derived + 10 explicit source hashes [TECH]
        Sandbox Metal SIGSEGV is typed HOST_TOOLCHAIN_BLOCKED; elevated verifier pass remains host-dependent [RISK]
      `.glb` selective zone dressing + explicit LOD ranges [TECH]
      Style benchmarks and canonical Act 5 epilogue exercise HouseA/PineA imported modules [ASSET][TECH]
      Epilogue HouseA is presentation-only; authored family and mesh collision remain open [RISK]
      Procedural forest crowns use deterministic tapered faceted tiers [ASSET]
      Act 1 Kara-Urman forest-edge kit — 2026-08-17 [MVP][ASSET]
        16 authored component families / 32 exact LOD0+LOD1 mesh nodes
        Left/right banks and occluder clusters close the side corridor [ASSET]
        PineMass A/B + BirchEdgeMass break the repeated PineA family [ASSET]
        DistantForestMass A/B close the empty horizon [ASSET]
        Root walls, stump, branch, logs and boulders add authored ground silhouettes [ASSET]
        SideGate_WayfindingLandmark provides a plain bend/threshold landmark [MVP][ASSET]
        All meshes collision=none; path/floor/runtime remain host owners [TECH]
        Blender/Godot import PASS; first-person traversal and art lock [OPEN][RISK]
      Procedural colliders remain authoritative pending QA [RISK]
      Godot captures gate art lock
      27-cell near/mid/far × FOV spatial sweep [TECH]
        Old PC focal read and forest depth confirmed
        Temporal motion comfort and final art review pending [RISK]
      Desktop debug package structure + current macOS host smoke PASS [TECH]
        Windows host / M1 performance / full playthrough pending [RISK]
    Core [NARRATIVE]
      Жанр
        Камерный этно-хоррор
        Мистический детектив
        Interface investigation
      Тон
        Деревенская достоверность
        Медленная мистика
        Молчание и несостыковки
        Напряжённые паузы и звук
        Неполно объяснённые правила
      Темы
        Память
        Корни
        Татарский язык [LANG]
        Долг
        Цена правды
      Уникальность
        Татарский культурный код
        Язык как ключ к памяти [LANG]
        Сосуществование вместо охоты на монстров
    MVP [MVP]
      Завязка [MVP][NARRATIVE]
        Айдар возвращается в Кырлай
        Без отдельного КФУ-интро
        Дед болеет
        Марат не отпускает
      Персонажи [MVP][ASSET]
        Айдар
        Бабай
        Әби
        Алсу
        Тимур хәзрәт
        Ринат
        Наиля
        Разиля
      Первая загадка [MVP][NARRATIVE]
        Версии смерти Марата не сходятся
        Могила
        Медпункт
        Архив
      Клиффхэнгер [MVP][NARRATIVE]
        Не просто деревенская тайна
        Реальная система сосуществования
        Кромка Кара-Урмана
        Голос Марата
        Ринат: «Не отвечай»
      Completion Handoff [MVP][TECH]
        mvp_completion_handoff.md
        P0 shared evidence chain
        playtest_plan.md
      Chapter 1 Campaign Lock [MVP][NARRATIVE]
        chapter1_mvp_campaign.md
        Road arrival to home
        Alsu route guide
        Old PC official death doc
        FAP and Rinat contradiction
        Tatarwiki re-read
        Timur moral safe zone
        Evening route past zirat
        Kara-Urman edge cliffhanger
        Hint before finale not rule
        Rinat confirms Не отвечай
      Игровой цикл [MVP][OPEN][RISK]
        Найти ключ
        Проверить у NPC
        Найти документ
        Вернуться с новым смыслом
    Narrative [NARRATIVE]
      Айдар
        Свой по крови
        Чужой по правилам
        Не избранный маг
      Марат
        Друг детства
        Трагический двойник
        Винтовка и лес
      Бабай
        Мансур
        Администратор тайны
        Сломанная преемственность
      Әби
        Гөлсинә
        Тихий архив
        Обучает Алсу
      Алсу
        Проводник
        Ложное подозрение Су Анасы
        Будущее Кырлая
      Тимур хәзрәт
        Safe zone мечети
        Исламская рамка
        Моральная опора
      Ринат
        Участковый
        Закрывает дело Марата
        Знает практические правила опасности
      Совет деревни
        Старшие семьи
        Дозированный доступ
        Контроль чужаков
    Gameplay [MVP][OPEN][RISK]
      Walkable first-person 3D [MVP][TECH][ASSET]
        Постоянная камера от первого лица
        Компактные ходибельные зоны
        Непрерывное перемещение и обзор
        World navigation [MVP][ASSET]
          Вид с высоты глаз
          Физические указатели
          Неполная схема в журнале
          Модульные 3D-локации
          Старый route graph — superseded prototype [TECH]
        Interface investigation
      Исследование [MVP]
        Дом
        Улица
        Медпункт
        Кладбище
        Кромка леса
      Диалоги через ключи [MVP][TECH]
        Факт
        Дата
        Имя
        Документ
        Татарское слово [LANG]
        Rinat first prototype [OPEN][MVP]
      Архивы [MVP][TECH]
        Поиск
        Документы
        Противоречия
      Татарский язык [MVP][LANG][RISK]
        Контекст
        Словарь
        Частичный перевод
      Мессенджеры [TECH]
        Ялкын
        Чаты жителей
      ПК бабая [MVP][TECH][ASSET]
        Главный документальный хаб [NARRATIVE]
        Runtime prototype implemented 2026-05-18 [TECH]
        Сюжетно-критичный доступ
        Доступен всегда из дома
        Безымянная Win98-like оболочка
        Mouse-driven desktop and windows
        Data-driven content loader
        Татарвики
        Архив
        Документы Марата
        Сохранённые сообщения
        Реестр домов и семей
        Нарушения и компенсации
        Кара-Урман
        Повреждённые файлы
        Gated-документы
        Local clue saving
        Journal projection through RuntimeKernel [MVP][TECH]
        Shared evidence bridge verified in Chapter 1 [MVP][TECH]
        Техническая метадата не clue
        Старые письма и бытовые папки ограниченно
        Внутренний учёт Кырлая
      Лес [NARRATIVE][ASSET]
        Запретные границы
        Шурале
        Звуки
        Правила понятны не полностью
      Safe zones [TECH]
        Дом
        Мечеть
    Language Learning [LANG][MVP][RISK]
      Татарские слова
        Урман
        Су
        Юл
        Өй
        Әби
        Бабай
        Ярамый
        Җавап
        Тавыш
        Шүрәле
      Контекст
        Русская речь с татарскими вставками
        Средняя плотность [MVP]
        Бытовые фразы
        Запреты
        Документы
      Диалоги
        Непонятые реплики
        Слова как dialogue keys
        Один татароязычный NPC [OPEN]
      Документы
        Частичные переводы
        Re-read старых улик
        Поиск по татарским словам
      Прогрессия [MVP][LANG]
        Stage 1 отдельные слова
        Stage 2 слова-запреты
        Stage 3 короткие формулы
        Stage 4 татароязычный NPC
      Механики перевода [TECH][OPEN]
        Vocabulary unlock
        Context guess
        Re-read old evidence
    Assets [ASSET][RISK]
      Modular 3D environment kits [MVP][ASSET][TECH]
      Selective object interfaces [MVP][TECH]
      Painterly Low-Poly 3D accepted [MVP][ASSET]
        In-engine captures reproducible
        Art lock pending visual acceptance [RISK]
      Ink-wash principles [MVP][ASSET]
        Тушевая линия
        Приглушённая акварель
        Бумажная фактура
        Обычность сначала
        Неправильность потом
      Asset Inventory 50 [MVP][ASSET]
      First generated asset batch [MVP][ASSET]
        Айдар portrait set
        Main street route screen — historical reference
        Old PC frame
        Medical record template
        Kara-Urman forest edge pressure screen
      Remaining 2D asset pack [MVP][ASSET][RISK]
        Historical visual coverage generated
        179 PNG assets
        Manifest-driven provenance for 50 rows
        Editable text metadata
        Controlled animation metadata
        Audio row #50 still separate [RISK]
      Controlled animation slots [MVP][ASSET][TECH]
        Static PNG base
        Overlay masks
        Window light pulse
        CRT glow
        Birds and dust
        Branch/grass drift
        Pressure overlays
      Village 3D kit [MVP][ASSET]
        Houses and fences
        Roads and paths
        Foliage and utility props
        Diegetic signs
        Collision-ready modules
      Art reference Искатель [ASSET]
      Персонажи [MVP]
      Локации [MVP]
      UI [MVP]
      Звук [MVP]
      Музыка
      Документы [MVP]
      Фото [MVP]
      Иконки
    Lore [NARRATIVE]
      Кырлай
        62 жителя
        21 домохозяйство
        Основание после 1552
      Кара-Урман
        Запретное урочище
        Старое place-name
        Не название деревни
      Пакт
        Молчание
        Границы
        Компенсация
      Шурале
        Лесной народ
        Не один монстр
      Су Анасы
        Вода
        Гребень
        Баранов 1967
      Бичура
        Домашний уклад
      Тукай
        1913 [RISK]
        Спорная архивная линия
      1967
        Баранов
        Плёнка
        Утопление
      История Марата
        Дневник
        Письма наружу
        Ложные версии смерти
    Technical [TECH]
      Modular migration complete [MVP][TECH]
        Portable JSON Markdown content
        CampaignManifest composition
        RuntimeKernel single state writer
        Immutable claim query before capability teardown
        Capability providers for unique mechanics
        Campaign fingerprint locked per run
        SaveGameV3 atomic user:// persistence [TECH]
          Runtime/capability/world/player/settings/playtime
          Portable input bindings
          No V2 importer or browser-localStorage reader
        Content Lab dev-only
        Authoring guide for LLMs [TECH]
          Data-only quests, dialogues, roles and assets
          Content check and isolated Lab runs
        Legacy owners retired after migration
        Unowned DedOS chat retired; archive remains sole narrative PC owner
        Atomic scene→dialogue handoff commits start effects once [TECH][NARRATIVE]
        Architecture ready to build MVP, not MVP release [MVP][RISK]
        Task packet docs/modular_migration
      Data model
        Characters
        Locations
        Clues
        Documents
        Vocabulary
      Shared Evidence Chain [MVP][TECH][RISK]
        Old PC clue
        Physical document target
        document.open -> shared kernel state
        Knowledge key
        Journal card
        Dialogue reaction
        Vocabulary re-read
        Pressure state
        Cliffhanger route
      Quest system [MVP]
      Dialogue system [MVP]
      Knowledge keys [MVP]
      Inventory
      Save system [MVP]
      Localization [LANG]
      Clue graph [MVP]
      Old PC authoring [MVP][TECH]
        Markdown плюс frontmatter
        content/old_pc
        10 runtime files
        Reliability status
        Canon status
        Search index
        Validator
      Full-game document reader [MVP][TECH]
        Four authored document targets
        Explicit «В журнал» action
        Idempotent journal.record
    Risks [RISK]
      Сюжет
        Слишком большой лор
        Слабый первый крючок
      Геймплей [OPEN]
        Minute-to-minute внешним playtest ещё не доказан
        Chapter 1 loop связан через RuntimeKernel [MVP]
        Full-game adapter smoke проходит, но production presentation incomplete [RISK]
      Ассеты [ASSET]
        Нет точного минимума
        Visual pack ahead of runtime
        Three benchmark frames rejected for art lock [RISK]
        Imported module pass improves evidence but does not close art lock [RISK]
        Act 1 day street now uses presentation-only WellA/WoodpileA props; helper-box collision remains authoritative [ASSET][MVP]
        Diegetic ФАП landmark improves route readability; observed wayfinding remains open [MVP][OPEN]
        Foliage-tier pass improves silhouette but final canopy family remains open [RISK]
        Acts 2–5 dressing is still modular greybox [RISK]
      Scope creep [MVP]
        Полная игра вместо среза
        Тьюринг-полный ПК до проверки MVP loop
        ПК как отдельная ОС вместо document hub
        Универсальный движок вместо минимальных capability contracts
      Cultural accuracy
        Татарский фольклор
        Ислам
      Татарский язык [LANG]
        Риск учебника
        Риск декорации
```

## Core

Core фиксирует идентичность игры: УРМАН — не «хоррор в деревне», а мистический детектив о языке, памяти, корнях и системе молчания. Любая новая механика должна усиливать эту формулу.

## MVP

MVP — вертикальный срез. Он обязан показать прибытие Айдара, тревожную деревню, первые следы Марата, ключи в диалогах, старый ПК как главный документальный хаб, татарский как инструмент понимания и клиффхэнгер о старом порядке.

## Narrative

Narrative держит эмоциональный двигатель. Марат — человеческий крючок, бабай и әби — семейный долг, Алсу — проводник в будущее Кырлая, Тимур хәзрәт — моральная и религиозная рамка.

## Gameplay

Gameplay пока остаётся рискованной зоной. Есть сильная идея ключей, старого ПК, архивов и повторного прочтения данных, но нужно прототипом доказать, что игроку интересно каждую минуту, а не только читать документы.

## Language Learning

Татарский язык должен открывать смысл, а не быть украшением. Игрок учит слова через контекст, возвращается к старым репликам и документам и видит больше, чем видел раньше.

## Assets

Ассеты — один из главных производственных рисков. MVP использует компактные ходибельные 3D-зоны от первого лица, модульные environment kits, интерфейсы, документы, портреты и звук вместо бесшовной дорогой постановки всей деревни. Painterly Low-Poly 3D принят как направление; environment `.glb` kit selectively materializes в трёх full-game зонах и теперь также упражняется в style-бенчмарках через `HouseA_` и `PineA_`, а отдельный project-original character `.glb` содержит девять prefix-вариантов и детерминированные LOD. `FullGameNpcDressing` подключает этот kit к ключевым NPC, `CollisionQaSmokeTest` закрывает базовый floor/proxy contract во всех 12 зонах; host-independent registry preflight закрывает source/derived hash boundary, но Blender verification остаётся host-dependent. Четырёхстемный ambience manifest и `AmbientAudioDirector` дают технический zone-routing pass, но финальные лица/одежда/анимация, authored voice/mix, mesh-collision acceptance, release performance и культурная проверка всё ещё открыты. Семь in-engine full-game capture-кадров и три style-кадра воспроизводимы, а art lock ещё не принят: lighting/fog calibration улучшает технические кадры, но не заменяет полную environment family, hero PC, бытовую специфику и review.

## Lore

Лор строится вокруг Кырлая, пакта и существ как старой системы сосуществования. Существа не должны становиться простыми врагами.

## Technical

Техническая архитектура должна быть data-driven и engine-neutral: квесты, диалоги, clues, документы, vocabulary, состояние деревни и clue graph.

## Risks

Риски надо не сглаживать, а превращать в задачи: gameplay prototype, asset minimum, татарский mechanic prototype, cultural review и вертикальный сценарий MVP.

## 2026-08-14 production slice

- `[ASSET]` Imported modular kit now has separate wood owners for facade,
  fence, furniture and bark scales.
- `[ASSET]` Character `cloth` resolves an explicit old-fabric albedo owner;
  clothing readability remains `[OPEN]` until traversal and cultural review.
- `[RISK]` Spatial/temporal Metal captures are technical evidence only; repeat,
  end-grain orientation, authored geometry and art lock remain open.
- `[ASSET]` Rebuilt Blender kit now includes `WellA_`, `WoodpileA_` and `GateA_`
  plus `OldPc_DriveSlot`/`OldPc_LabelPlate`, with a 49-mesh deterministic LOD1
  contract and `OldPc_ 8/8` exact pair checks.
- `[TECH]` Act 2 yard uses WellA/WoodpileA; Act 5 boundary uses GateA under the
  PineA anchor after removing the duplicate procedural threshold dressing.
- `[RISK]` These modules are decorative only: floor/path and existing proxy
  colliders remain authoritative; placement, repetition, cultural review and
  release-host performance are still open.
- `[TECH]` `GeneratedModularKitContractSmokeTest` validates all nine GLB
  families through `AttachPresentationOnly`: exact `_`-terminated prefixes,
  LOD0/LOD1 pairing, semantic-owner sets, imported-physics removal and zero
  presentation physics descendants.
- `[RISK]` `FenceA` has no authored `FenceA-col` in the project-original GLB;
  the registry records it as presentation-only and does not grant it a hidden
  gameplay collider.
- `[RISK]` OldPc close captures plus the 18-tile near/mid FOV/head-bob motion
  receipt now show the new details through a non-empty technical matrix, but
  observed traversal/comfort, standard Soviet recapture, repetition, cultural
  review and release-host performance remain open.
- `[TECH]` The temporal render-only harness preserves visual descendants,
  removes only disposable collision shapes and records 216 valid Metal/Forward+
  samples; the prior sparse receipt is explicitly superseded.
- `[ASSET]` The six-frame isolated lighting/fog/key-light A/B under
  `art/style_calibration_candidate/` now informs the bounded Chapter 1
  `StyleBenchmarkZone` presentation baseline; geometry/material ownership and
  final art acceptance remain open.
- `[OPEN]` Comfort, authored geometry/hero PC/canopy, cultural presentation,
  release-host performance and final art lock remain unresolved after these
  captures.

## 2026-08-15 Act 1 hero-PC presentation slice

- `[MVP][ASSET]` The first-act house now uses the project-original `OldPc_`
  8/8 LOD family through a presentation-only adapter at the existing PC
  interaction anchor; procedural CRT/keyboard/tower duplicates were removed.
- `[TECH]` The layer-1 `OldPc` interaction target remains the only gameplay ray
  owner; imported collision descendants are sanitized before the GLB enters
  the scene and smoke requires zero module physics.
- `[OPEN][RISK]` The new house frame strengthens the focal prop but does not
  close close-distance readability, observed pacing, cultural review, target
  hardware FPS or Painterly Low-Poly art lock.

## 2026-08-15 Act 1 slow-startup FPS guard

- `[TECH][MVP]` `Act1DemoRoot` samples a few post-warm-up frames and applies a
  session-only low material/render-scale rescue only after consistently heavy
  startup frames; it does not touch kernel, narrative, saves, zones or
  collisions.
- `[TECH]` `--print-fps --no-auto-performance-fallback` exposes live target-host
  FPS/frame-time/preset/scale/MSAA diagnostics.
- `[OPEN][RISK]` Local M4 Pro package remains ~118–119 FPS, so M1/Windows,
  editor-vs-package launch and the reported ~1 FPS still require reproduction.

## 2026-08-15 Act 1 destination-facing zone spawns

- `[MVP][TECH]` Compact Act 1 transitions now set a destination-facing yaw so
  the player sees the next physical landmark after a doorway fade; the forest
  return faces the village.
- `[TECH]` `Act1FirstPersonCorridorSmokeTest` checks the declared yaw after each
  transition without treating later mouse/gamepad look input as a failure.
- `[OPEN]` This removes a deterministic orientation trap, but observed
  first-time wayfinding, controller drift/comfort and human cultural review
  remain separate gates.

## 2026-08-17 Act 1 complete-village landmark composition

- `[MVP][ASSET][TECH]` The accepted project-original village landmark kit is
  integrated into the persistent connected world through
  `Act1ConnectedWorld` only; its nine exact authored groups cover Arrival,
  VillageStreet, BabaiYard, HouseExterior, ConnectiveStreet, FapExterior,
  ReturnStreet, ZiratBoundary and KaraApproach.
- `[TECH]` The authored linear strip is re-anchored per group to the existing
  route zones. The kit is presentation-only: no collision, navigation or
  interaction nodes; floor/path, zone targets and `RuntimeBridge` remain the
  existing owners. The old visual-only Zirat enclosure is the only duplicate
  suppressed.
- `[MVP][RISK]` Fresh root-viewport review shows a clearer connected village
  rhythm and an open Zirat/Kara route center, but Kara side views remain busy,
  Babai is still wall-heavy, and the reverse Zirat view remains only partly
  authored.
- `[OPEN][RISK]` Full-route near/mid/far composition, observed wayfinding,
  cultural review, target-hardware performance and final art lock remain open;
  this is a partial runtime composition candidate, not demo-ready art.

## 2026-08-24 Act 1 production-owner cleanup

- `[TECH][MVP]` `Act1ConnectedWorld/AgentBExteriorWorld` is the sole runnable
  Agent B exterior owner and exposes `variantStatus=production-canonical`;
  the old standalone world, probes and windowed launcher are retired.
- `[RISK]` Historical Agent B frames/receipt remain useful provenance only;
  they do not replace a new production full-route 360° first-person capture.
- `[ASSET][TECH]` The deterministic foliage plan now has a bounded low-contact
  pass for wet shoulders, yard edges, FAP service space, zirat transition and
  Kara threshold using authored non-tree variants; it stays presentation-only.
- `[TECH][ASSET]` Kara night now has three low-energy, shadowless cool bounce
  cues under the same exterior atmosphere owner; they are disabled outside the
  night edge and remain visual-only with no gameplay or narrative state.
- `[OPEN]` Authored density, near/mid/far composition, night Kara readability,
  cultural/language review, release-host performance and human playtest remain
  gates before any prod-ready or art-lock claim.
