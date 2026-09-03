# Weak Points

## Connected Act I map remains the primary visual gate

Актуальное состояние: production `Act1ConnectedWorld` теперь создаёт пять
логических зон в одном persistent root и монтирует `AgentBExteriorWorld` с
пятью authored GLB-kit, terrain/road collision, foliage и rain/fog/light.
Физический walkthrough до клиффхэнгера проходит, но 360° first-person кадры,
near/mid/far review, human wayfinding и отсутствие повторов ещё не приняты.

Историческая формулировка blocker-а: текущий `act1_demo.tscn` технически проходит маршрут, но
`Main.SwitchZone` уничтожает текущий `ZoneHost` и загружает одну benchmark-сцену
за раз. Поэтому три красивых кадра и физический walkthrough не доказывают
цельный мир: при развороте игрок может увидеть пустоту, greybox-границу или
повторяющуюся процедурную корону вместо продолжения Кырлая.

Следующий gate: сохранить общий world/runtime owner, провести ограниченный
full-route root-viewport capture для восьми визуальных зон и проверить руками
направления вперёд/назад/вбок/вверх/вниз, near/mid/far, wayfinding и повторяемость.
Текстуры, package/FPS и benchmark receipt остаются вторичными до этого review.

Точечный 360° pass 2026-08-25 добавил обратные оконно-дверные и timber cues к
authored dwelling facades через существующий presentation-only helper. Это
убирает доказанную пустую заднюю плоскость соседнего `MainStreetWestNeighborFacade`
в Babai-кадре, не меняя collision, interactions или `RuntimeBridge`; свежий
headless receipt подтверждает 36/36 root-viewport кадров и 10/10 waypoint.
Пасс всё ещё не является visual acceptance: road/terrain flatness, near/mid/far
density, Kara night readability, cultural review и human 360° проход остаются
OPEN.

В точечном road-пассе 2026-08-25 скрыты только presentation-меши legacy
`RoadSurface` коннекторов и core outer-bank envelope: видимый мокрый crown,
ruts, puddles и terrain остаются за Agent B, а traversal strips/collision
сохраняются. Headless receipt после каждого варианта подтверждает 36/36 кадров,
10/10 waypoint и отсутствие runtime errors. Это улучшает читаемость мокрой
дороги, но оставшиеся плоские участки, общий near/mid/far density и human visual
review по-прежнему OPEN.

Периферийный authored-parcel pass 2026-08-25 добавил пять дальних фасадных
кластеров из существующего `urman_village_exterior_kit.glb` вокруг въезда,
главной улицы и возвратной дороги. Они остаются presentation-only и не входят
в collision, interactions или `RuntimeBridge`; receipt сохранил 36/36 кадров и
10/10 waypoint. Это закрывает только несколько дальних silhouette gaps:
боковые поля, плотность near/mid/far, повторяемость prefab-семейств и human
360° review всё ещё OPEN. Шесть дальних foliage entries добавлены в тот же
deterministic plan; clearance guard сохраняет дорогу свободной, но authored
canopy и material variation ещё не приняты.

## FAP exterior now has a single presentation owner

Проверенный production-риск был конкретным: `agentb_village_buildings_kit.glb`
содержит собственное семейство `Fap_*`, а connected-world слой отдельно
подключает `urman_fap_clinic_kit.glb` на том же участке. Это давало два
конкурирующих фасада, ограды и входа, даже когда каждый kit по отдельности
выглядел корректно.

Минимальное исправление: `AgentBAct1ExteriorLayer` скрывает `Fap_*` до
генерации архитектурной коллизии и помечает семейство suppression metadata;
`FapClinicAuthoredKitPresentation` остаётся единственным exterior visual
owner; его authored wayfinding board получает один физически привязанный
diegetic label `ФАП`, чтобы suppression не убирал ориентир вместе с дублем.
Интерьер, desk/document targets и `RuntimeBridge` не затронуты.
`Act1DemoLaunchSmokeTest` теперь fail-closed проверяет этот контракт и сам
label. Это закрывает только структурный duplicate-owner риск; forward/back/
side кадры, wayfinding comprehension, локальный медицинский контекст и human
art review всё ещё OPEN.

## Foliage template source is hidden after extraction

Проверенный production-риск был рядом с тем же presentation-layer:
`agentb_foliage_kit.glb` является библиотекой исходных семейств, но до
исправления оставался видимым после создания `AgentB_PlantedFoliage`. Это
оставляло у начала мира плотную доску шаблонов и одновременно показывало
исходные и посаженные варианты.

`AgentBAct1ExteriorLayer` теперь скрывает только source root после сбора
шаблонов, помечает `templateSourceHidden` и сохраняет deterministic copies в
`AgentB_PlantedFoliage`. План дополнен низким contact-scale pass по мокрым
обочинам, дворам, ФАПу, переходу зирата и кромке Кара; он использует
разнообразные authored sedge/fern/grass/moss/branch/stump variants и не входит
в дорожный проход. Smoke проверяет, что source root невидим, planted layer
непуст, обработал все записи плана и не допускает посадку ближе 0,25 м к
road envelope. Это закрывает template-board
contamination и устраняет известный spatial density недобор, но не закрывает
360° повторяемость, authored foliage review, near/mid/far motion readability
или культурную проверку.

## Act I atmosphere now has one routed environment owner

До этой коррекции `AgentBEnvironment` добавлялся в persistent core и оставался
активным вместе с `WorldEnvironment` текущей logical zone. Это создавало
конкурирующие глобальные environment/light owners: внешний Agent B sun мог
складываться с локальным directional light, а интерьер не гарантировал свой
тёплый фон.

`Act1ConnectedWorld` теперь маршрутизирует ровно одного владельца: на
`village_day`, `zirat_road` и `kara_urman_night` активен Agent B exterior
environment/sun, а локальные directional suns выключены; в `house_old_pc` и
`fap_clinic` Agent B environment/sun выключены и остаётся локальная среда
интерьера; дождь Agent B также выключается внутри. Smoke проверяет один
активный `WorldEnvironment` и rain-emitter в обоих режимах.
Для ночной кромки добавлены три слабых холодных `OmniLight3D` cue в том же
`AgentBExteriorWorld`: они подсвечивают порог, корневой кластер и жестовую
ветвь только при `kara_urman_night`, без теней, коллизии, навигации или
интеракций. Dedicated smoke проверяет их runtime-включение через test-owned
camera probe; это только value-separation correction. Эта коррекция закрывает
ownership-конфликт, но не является визуальной приемкой света, ночной
читаемости, дождя, тёплого окна или art lock.

## Standalone Agent B world variant is retired

Production теперь имеет один исполняемый внешний владелец:
`Act1ConnectedWorld/Act1CoreWorldGreybox/AgentBExteriorWorld`, помеченный
`variantStatus=production-canonical`. Старый `AgentBAct1World` и его
windowed/probe harness удалены; исторический capture-report оставлен только
для provenance. Это снижает риск случайно проверять не тот мир и не меняет
маршрут, collision, interaction, `RuntimeBridge` или save ownership. Smoke
проверяет canonical marker, а визуальный 360° gate остаётся OPEN.

## Capture harness liveness is now bounded

Предыдущий full-route capture мог оставаться живым без PNG до ручного убийства:
test-only код напрямую ждал `RenderingServer.FramePostDraw`, а shell-wrapper не
имел watchdog. В `Act1FullRouteCoreWorldCapture` ожидание заменено коротким
settle-window по `ProcessFrame`; `eng/capture-act1-full-route-core-world.sh`
дополнительно ограничивает production capture таймаутом
`URMAN_CAPTURE_TIMEOUT_SECONDS` (по умолчанию 300 секунд). Это только QA
инфраструктура и не является доказательством визуальной приемки; сам capture и
human review остаются обязательным следующим gate.

## MVP-прототипы не связаны в одну игру

Слабое место: старый route navigation уже не соответствует принятому first-person 3D target, а внешний minute-to-minute detective loop всё ещё не доказан. Базовый разрыв общей world presentation закрыт частично: physical full-game documents теперь открываются в Godot и могут записываться в тот же journal, что и old PC, но vocabulary re-read, dialogue topics и полноценный плейтест ещё требуют production-прохода.

Путь решения: сохранить shared knowledge source of truth и довести цепочку `old PC/physical document -> journal -> dialogue reaction -> vocabulary re-read -> pressure -> 3D world/cliffhanger`. Сейчас Godot smoke доказывает первые два звена для четырёх full-game документов; следующим шагом остаются re-read, authored dialogue consequences и external first-time playtest. Старый route prototype использовать только как provenance и reference.

## Визуальный пакет опережает runtime

Слабое место: `public/assets/urman_mvp_remaining/` визуально покрывает большую часть старого 2D-формата, но не является готовым набором для ходибельного 3D. Runtime использует HTML-заглушки, 3D-placeholder мечети, старую рамку ПК и частичные handoff previews.

Путь решения: разделить пакет на reusable references/UI/textures и то, что нужно заменить 3D-моделями. До массовой генерации собрать один in-engine environment kit и один hero prop; ink-wash документы, портреты и old PC UI использовать там, где 2D остаётся частью принятого формата.

## Acts 2–5 имеют presentation pass, но не production lock

Слабое место: `FullGameZoneDressing` теперь даёт каждой зоне реальный модульный визуальный контекст, `GeneratedModularKitDressing` подключает environment `.glb` с проверяемыми LOD-диапазонами и provisional layer-2 proxy colliders, а три style benchmark кадра дополнительно упражняют project-original `HouseA_`/`PineA_` через тот же адаптер. Последний Blender pass добавил HouseA foundation/door/frame/window-trim/porch/step/eave cues и довёл environment kit до 32 детерминированных LOD1 mesh без расширения gameplay-collision owner. `FullGameNpcDressing` размещает project-original character `.glb` с девятью prefix-вариантами, лицевыми landmarks, layered clothing, детерминированными LOD и Blender-authored `Idle`/`Tension` armatures. Godot `AnimationPlayer` импортирует и переключает оба клипа presentation-only; четырёхстемный `AmbientAudioDirector` и `AmbientAudioSmokeTest` добавляют только технический zone-routing/import contract; семь Godot capture-кадров, три style capture и отдельный `CollisionQaSmokeTest` подтверждают wiring, queryable floors всех 12 зон и изоляцию layer-2 proxy colliders. Окружение и персонажи всё ещё production-progress: полный authored module family, финальная expression-постановка, authored ambience/voice/mix, mesh-level collision acceptance, release-hardware LOD/performance evidence и cultural presentation review остаются открыты.

Путь решения: заменять dressing по актам на Blender-ассеты только после beat-sheet/content lock; каждый акт принимать отдельной проходимой сборкой с frame-time, collision, audio-caption и культурной проверкой. Текущий smoke-контракт закрывает только базовую геометрию пола и изоляцию proxy; следующий collision pass должен проверить реальные authored mesh-colliders перед заменой procedural colliders.

Bounded epilogue progress, 2026-08-14: канонический Act 5 epilogue теперь
получает тот же project-original `HouseA_` модуль, что и Act 2, через отдельный
`act5-epilogue-house` presentation variant. Это улучшает проверяемую композицию
седьмым full-game capture и не добавляет второй gameplay-owner. Риск не снят:
один HouseA не заменяет полное семейство деревенских модулей, authored mesh
collision, hero-пропорции или культурную проверку.

## Локальный performance baseline не заменяет release hardware

Слабое место: три 1080p benchmark-сцены и отдельный full-entrypoint probe Act 1
измерены на Apple M4 Pro и держат внутренний 30 FPS low-preset floor, но это
ещё не доказательство 60 FPS на среднем Windows-PC, 30 FPS на Apple M1 или
стабильности экспортированных сборок. Локальный probe полного
`act1_demo.tscn` также не воспроизводит жалобу «около 1 FPS». Исправлена
несогласованность старта: заявленный `medium` теперь реально применяется
до первого кадра (0.90 scale / 2× MSAA); release-hardware reproduction всё ещё
нужен.

Путь решения: повторить тот же benchmark на Apple M1 и реальном Windows-PC, сохранить raw frame-time и проверить low/medium/high пресеты до финального cutover.

Для жалобы на единичный FPS добавлен явный диагностический путь
`eng/run-act1-demo-safe.sh`: Mobile renderer, 0.75 3D scale, без MSAA и без
трёх triplanar texture reads в painterly fragment shader. Он не меняет зоны,
сюжет, сохранения или collision owners. Если safe launch исправляет FPS, нужно
отдельно принять renderer/material budget для целевого GPU; не выдавать этот
локальный rescue path за закрытие M1/Windows performance gate.

## Плейтест полного MVP пока преждевременен

Слабое место: полный путь «приезд → клиффхэнгер» ещё нельзя честно тестировать как цельную главу. Можно тестировать old PC search loop и narrative comprehension, а старую route orientation — только как исторический прототип, не как final external MVP experience.

Путь решения: использовать Godot first-person matrix из `playtest_plan.md`: сначала movement/input/performance smoke, old PC, language and narrative tests. Full first-time MVP playtest возможен только после 3D-house scene, shared journal/dialogue graph и final cliffhanger path.

## Gameplay core loop не доказан

Слабое место: detective loop принят как главное действие игрока, но пока не доказано прототипом, что это интересно каждую минуту.

Путь решения: сделать 15-минутный playable prototype с одним расследовательским циклом: противоречие → clue → NPC / архив → новый ключ → новая реакция NPC → татарский unlock → мистический след.

## Формат игры выбран, но не доказан

Слабое место: ходибельный 3D от первого лица принят как направление, но ещё не доказано, что маленькая команда сможет удержать качество окружения, атмосферу и темп без расползания локаций.

Путь решения: сделать 15-минутный greybox: дом → участок улицы → кладбище или архив/ПК → Ринат → audio-first след. Проверить масштаб, управление, интерактивность, скорость сборки модульного окружения и стоимость одной законченной минуты. Для текущей демо-сборки непрерывность ambience усилена двухплеерным 0.65-секундным crossfade; authored voice, полевой ambience mix и культурное listening review остаются отдельными gates.

## Ассеты не зафиксированы как минимум

Слабое место: список персонажей, локаций, документов и UI быстро разрастается.

Путь решения: утвердить MVP asset floor: минимум портретов, минимум локаций, минимум документов, один old PC UI, один journal UI, один dialogue key UI.

## Визуальный стиль принят, но не доказан production-тестом

Слабое место: Painterly Low-Poly 3D принят, но ещё не доказано, что его производственные бюджеты дают нужное качество в движении. Красивый одиночный concept render может скрыть чрезмерную стоимость ассетов или нестабильный frame-time.

Путь решения: собрать три ходибельных Godot benchmark scenes — улица, дом/ПК и ночная кромка леса. Проверить силуэты, материалы, свет, туман, поворот камеры, collision, производственную повторяемость, frame-time и отсутствие generic horror. Шесть versioned v2 texture candidates (`*_v2_albedo.png`), шесть focused v3 siblings (`*_v3_albedo.png`), focused v4 earth/wood pair (`*_v4_albedo.png`) и focused v5 earth/wood rework (`*_v5_albedo.png`) проходят формат/seam gate; все остаются test-only preview ownership и не включены глобально. Отдельный v3↔v5 Metal/Forward+ A/B даёт 9 листов и 120/120 изолированных contact samples; отдельный v5 motion sweep даёт 27 near/mid/far × FOV 65°/75°/90° ячеек на каждой обязательной сцене. Earth relief-only/relief+wetness и wood facade/house/forest comparison остаются OPEN. Отдельные camera sweeps подтверждают spatial projection; temporal sweep добавляет 216 кадров для FOV 65°/75°/90° с head bob и reduced motion. Это техническое evidence, не внешний comfort review. Независимый аудит держит v4 в HOLD/REWORK; v5 — production OPEN: earth всё ещё требует проверки pebble repetition/wetness и отсутствия двойного relief, wood — проверки ориентации и shared owner/scale; greybox geometry, wet roughness и fog/light calibration всё ещё не приняты.

### Лужи имеют отдельный contact/wetness gate

Слабое место: текущие low-poly puddle silhouettes визуально существуют, а
первый общий physics-world аудит давал ложные совпадения с relief другой сцены.
После изоляции сцен и bounded-коррекции `PuddleFar`/`BoundaryWetPatch` все
15 патчей принадлежат ожидаемому relief и укладываются в ±0.010 м. Снижение
roughness до «мокрого» значения всё ещё может скрыть реальные проблемы
specular/clip/z-fight, поэтому визуальный material gate остаётся отдельным.

Путь решения: использовать отдельный test-only Godot harness, который поочерёдно
инстанцирует benchmark-сцены, требует instance-owner collider, клонирует только
presentation `ShaderMaterial` в памяти, проверяет 5 кластеров/15 патчей и
отсутствие изменения исходного `roughness=0.90`. Contact gate теперь PASS;
wetness roughness/specular остаётся OPEN и не становится runtime owner.

Матрица v2 теперь измеряет raw production origins до любых test-only действий.
Отдельный узкий geometry slice посадил только восемь превышавших 5 мм патчей
на их собственные relief-cells; все 15 origin samples проходят ±0.005 м с
exact owner и candidate-local alignment больше не требуется. Матрица всё ещё
сравнивает `roughness_value` в rows `0.40/0.50/0.60` (45 клонов, 9 кадров).
По кадрам wetness остаётся слабой/плоской; не выбирать roughness и не менять
runtime до отдельного visual review.

## Полная игра шире подробно написанного канона — narrative lock закрыт

Слабое место: релиз на 6–8 часов и трагический финал приняты, но production мог начать диктовать сюжет до подробной фиксации актов 2–5.

Решение: 2026-08-11 принят `narrative_lock_acts_2_5.md` с beat sheet, clue graph, зонами, NPC, документами, language keys, world variants и threat beats для каждого акта. Это снимает narrative ambiguity для производства, но не закрывает внешний татарский/культурный review, art/audio, mesh-collision или release gates. Greybox и final art по-прежнему принимаются раздельно.

## Татарский язык требует точной механики

Слабое место: «изучение татарского» легко станет декоративным словарём или скучным уроком.

Путь решения: первый language prototype должен менять расследование. Игрок выучил слово → старый документ стал понятнее → NPC дал другую реакцию.

## MVP может расползтись

Слабое место: полный лор включает 1552, Тукая, Баранова, Марата, пакт, Алсу, Тимура, несколько существ и концовки.

Путь решения: MVP держать вокруг Марата. 1552, Тукай и Баранов — только короткие намёки или архивные teasers.

## Сюжет большой, но вертикальный срез должен быть узким

Слабое место: хочется раскрыть весь мир сразу.

Путь решения: MVP раскрывает только смену рамки: «деревня лжёт» → «деревня живёт в системе сосуществования». Не раскрывать правила пакта полностью.

## Риск спойлернуть финальное «Не отвечай»

Слабое место: если Гөлсинә, Ринат, Татарвики или journal заранее прямо объяснят правило «не отвечать голосу Марата у кромки», финальный клиффхэнгер станет не откровением, а выполнением инструкции.

Путь решения: до финала использовать только тревожную гипотезу `clue_voice_answer_is_dangerous_hint`: голос / ответ / граница опасно связаны, но правило не подтверждено. Ключ `clue_do_not_answer_rule` становится confirmed только после действия Рината в сцене у кромки Кара-Урмана. Детальный lock: `chapter1_mvp_campaign.md`.

## Риск перегрузить игрока лором

Слабое место: архивы, термины, существа и история могут задушить темп.

Путь решения: каждый документ должен иметь gameplay use. Документ без clue, ключа или эмоционального удара не входит в MVP.

## Старый ПК может съесть MVP

Слабое место: ПК бабая теперь сюжетно важный документальный хаб, и его легко разрастить до самостоятельной тьюринг-полной ОС раньше, чем доказан базовый detective loop.

Путь решения: для MVP делать не «компьютер внутри игры», а плотный old PC / archive loop: поиск, документы, «Татарвики», сохранённое сообщение, gated/corrupted fragment и re-read после vocabulary/search unlock. Прототип 2026-05-18 уже покрывает этот loop на 10 content files. Следующий риск не в самой оболочке, а в интеграции улик ПК с journal/dialogue graph. Полную программируемость держать как post-MVP ambition. Техническая метадата не должна быть главным clue; использовать документальные отметки и противоречия.

## Слишком много персонажей

Слабое место: список NPC широкий: семья, Алсу, Тимур, Ринат, Наиля, Разиля, Гаяз, Ленар, лесник, Самат, Мурат, бабушки, абыйлар, сельсовет.

Путь решения: MVP делит персонажей на active NPC и ambient/document NPC. Не всем нужны портреты и сцены.

## Хоррор, детектив и education могут конфликтовать

Слабое место: если татарский mechanic будет слишком учебным, он сломает хоррор; если хоррор будет слишком прямым, он сломает детектив.

Путь решения: язык должен быть инструментом расследования, а хоррор — следствием понимания, а не отдельным режимом.

## Религиозный слой требует аккуратности

Слабое место: Тимур хәзрәт и исламская рамка легко могут стать карикатурой.

Путь решения: писать Тимура как цельного человека, а не функцию экспозиции; консультироваться; не противопоставлять ислам и татарскую культуру примитивно.

## Непонятно, насколько игра интерфейсная

Слабое место: первое лицо усиливает деревню как место, но баланс всё ещё может съехать: слишком много документов остановит исследование, слишком много пустой ходьбы раздует производство и размоет детективный цикл.

Путь решения: компактные ходибельные зоны + глубокие интерфейсные расследования. Интерфейсы как реальные предметы использовать выборочно: старый ПК, бумага, фото, журнал.

## Риск укачивания и пустого 3D

Слабое место: медленное первое лицо легко испортить сильным head bob, инерцией, узким FOV или длинными пустыми маршрутами.

Путь решения: минимальный head bob, настраиваемый FOV, предсказуемая скорость, короткие насыщенные маршруты и частые смысловые ориентиры. Проверить управление на сером блок-ауте до производства красивых ассетов.

## Конфликт имён Марата

Слабое место: центральный Марат — друг детства, но ранний список содержит Марата-лесника.

Путь решения: переименовать лесника или объединить функцию с другим персонажем до сценарного прототипа.

## Бабай как действующий глава в 80 лет

Слабое место: может быть неправдоподобно, если он формально всё ещё глава администрации в 2026.

Путь решения: Мансур уже зафиксирован как бабай и фактический держатель семейного доступа к ПК. Открытый вопрос теперь не имя бабая, а статус Гаяза: нужен ли он как отдельный официальный глава/ambient NPC или его функцию нужно удалить из MVP.

## Линия Тукая культурно рискованна

Слабое место: прямое утверждение, что деревня / существа повлияли на смерть Тукая, может звучать грубо.

Путь решения: держать как спорный архивный документ, локальную интерпретацию или фан-теорию внутри мира, не как подтверждённый факт.

## Runtime несколько источников истины до cutover — закрыто 2026-07-18

Бывший риск: `GameState`, `SceneManager`, TypeScript arrays, custom old-PC parser, localStorage owners и `window.URMAN` одновременно несли content, progression, navigation и persistence.

Решение: production теперь использует compiled content pack, `RuntimeKernel`, capability providers и named persistence gateway; old owners/validators удалены, а static gate проверяет 88 production source files. Legacy MainMap/Content Lab изолированы как dev-only. Permanent fallback и второй owner не допускаются.

Исторический риск `GameSnapshotV2` не хранить текущую presentation-position закрыт в target: `SaveGameV3` теперь хранит текущую зону, spawn point и portable player transform. Восстановление Godot-зоны и settings проверено интеграционным smoke; повреждённый primary recovery покрыт atomic-store tests.

## Accessibility contract — технически закрыт, пользовательская проверка открыта

Слабое место: доступность легко принять за наличие нескольких флажков, хотя качество зависит от того, действительно ли reduced motion, контраст, размер текста, субтитры и текстовые описания действуют на всех presentation owners и не расходятся с сохранением.

Путь решения: `AccessibilitySettingsSnapshot` живёт внутри unreleased `SaveGameV3`; `AccessibilityPresentation` применяет его через одну Godot target-group к first-person head bob, dialogue/journal/document/old-PC/settings panels и `AudioCueUi`. C# codec и Godot smoke проверяют roundtrip, диапазон text scale и reduced-motion override. Остались внешние проверки укачивания, читаемости на 1080p/M1/Windows и культурной корректности non-audio descriptions.

## Current runtime спойлерит «Не отвечай» раньше accepted финала

Слабое место: текущие `dialogue_data.ts`, `knowledge_keys.ts` and `quests.ts` могут unlock/require `clue_do_not_answer_rule` до действия Рината, хотя accepted Chapter 1 разрешает только hypothesis `clue_voice_answer_is_dangerous_hint` до cliffhanger.

Путь решения: MM-50 не копирует текущие unlock edges. Narrative invariant test должен доказывать отсутствие/неподтверждённость `clue_do_not_answer_rule` до финального outcome и confirmation только после реплики Рината.

## Character IDs и records не замкнуты

Слабое место: legacy array использует `babay`/`abi`, другие data use `char_babay`/`char_abi`/`char_gulsina`, а обязательные Айдар, Марат, Ринат, Наиля and Тимур records отсутствуют. `rushania` нельзя молча принять за Наилю.

Путь решения: применить one-time mapping MM-10 без runtime aliases; MM-50 создаёт пять missing records из accepted authority, а ambient legacy characters подключает только явно через campaign manifest.

## V6 texture candidates remain evidence, not acceptance

Слабое место: v6 earth и wood выглядят спокойнее на swatch, но в текущем
greybox/освещении разница земли с v5 едва заметна; это может означать как
правильную подчинённость фактуры геометрии, так и отсутствие полезного
материального breakup.

Путь решения: держать v6 test-only, проверять earth relief-only против
relief+wetness и wood на фасаде/заборе/мебели/end-grain при нейтральном и
тёплом свете. A/B и 27-cell motion receipts уже технически чистые, но
temporal comfort, 20–30 m repetition, geometry/fog/light, cultural review и
art lock остаются открыты.

## Pinned Blender verifier зависит от host-контекста

Слабое место: в managed/sandboxed запуске pinned Blender 4.5.12 LTS завершается `SIGSEGV (139)` внутри
`gpu::supports_barycentric_whitelist -> MTLBackend::metal_is_supported` ещё до
загрузки `.blend` и verifier script. Это воспроизводится для environment,
character и даже `--factory-startup --python-expr`; проблема не доказывает
дефект исходных мешей или текстур. На elevated real-driver запуске оба
верификатора сейчас проходят, поэтому статус зависит от host и не должен
маскироваться одним зелёным/красным результатом.

Путь решения: всегда сначала запускать `eng/verify-asset-registry.sh`, который
проверяет 36 derived и 10 явных source hashes независимо от Blender; затем
классифицировать sandbox SIGSEGV как `HOST_TOOLCHAIN_BLOCKED` и повторить
environment/character verification на release host или после обновления
pinned Blender с полным regression pass. Доказательства и точные команды:
`art/blender_asset_verifier_host_blocker.md`.

## Lighting/fog calibration — технически проверено, визуально открыто

Слабое место: даже после bounded ambient/fog/key-light calibration в
`StyleBenchmarkZone.cs` day street остаётся серо-оливковым greybox, дом слишком
тёплый/пустой, а Kara-Urman сжимается туманом в синюю массу. Это нельзя
исправить дальнейшим усилением albedo: нужны authored geometry, material
owners и культурная level-art проверка.

Путь решения: считать новые static/27-cell/216-sample кадры production
candidate evidence, но держать GODOT-003 и art lock OPEN; следующий дорогой
срез — authored village/forest/hero-PC geometry и owner-specific material
orientation, а не ещё одна универсальная texture версия.

## Imported GLB collision descendants require an adapter boundary

Слабое место: project-original environment GLB содержит собственные
`StaticBody3D`/`CollisionShape3D` descendants. Если оставить их внутри
presentation instance, они добавляют неожиданные layer-1 physics owners рядом
с authored walkable floor и controlled layer-2 proxy.

Путь решения: `GeneratedModularKitDressing` синхронно удаляет эти imported
descendants до `AddChild`. Обычный `Attach` затем создаёт ровно один
контролируемый layer-2 proxy, а `AttachPresentationOnly` — ноль physics nodes;
`SceneSmokeTest` считает фактическое дерево, а не только metadata. Raw GLB и
его registry не меняются. Это закрывает ownership regression, но не визуальный
арт-гейт: PineA family всё ещё повторяется, а release-host/motion/cultural
review остаются OPEN.

## Temporal evidence must preserve the scene it measures

Слабое место: ранняя версия temporal harness освобождала целые
`CollisionObject3D`. В benchmark-сценах MeshInstance3D часто является его
потомком, поэтому формально «непустые» кадры были разреженной копией сцены и не
могли подтверждать комфорт или читаемость.

Путь решения: текущий render-only clone сохраняет визуальных потомков,
обнуляет collision layer/mask и освобождает только `CollisionShape3D` после
снимка исходного дерева. Receipt фиксирует 806/109/826 visual meshes,
286/22/695 удалённых shapes и ноль активных physics-query owners. Старые кадры
сохранены как superseded evidence; 216 новых образцов — лишь техническое
доказательство. Внешний comfort review и art lock остаются OPEN.

## Lighting/fog calibration is an integrated bounded baseline, not an art lock

Слабое место: даже после интеграции bounded calibration в Chapter 1
`StyleBenchmarkZone` day street остаётся серо-оливковым greybox, дом тёплым и
пустым, а Kara-Urman всё ещё требует authored canopy/ground geometry. Усиление
текстур не решает иерархию света и authored geometry.

Путь решения: значения ambient/fog/key/fill были сначала проверены через
`StyleCalibrationCandidateCapture`, затем перенесены только в
`StyleBenchmarkZone.cs` как презентационный baseline для демо первого акта.
Production material owners, shader, GLB, collision, saves и narrative state не
меняются. Свежий Metal/Forward+ capture технически PASS, но visual/cultural/art
acceptance, near/mid/far repetition, temporal comfort и полноценная authored
geometry остаются OPEN.

## Owner-specific wood/cloth calibration — технически подключено, визуально открыто

Слабое место: один world-scale для imported wood surfaces превращал фасад,
забор, мебель и кору в одинаковую регулярную полосу; character kit запрашивал
`cloth`, которого не было среди texture descriptors и потому терял albedo.

Путь решения: `PainterlyMaterialLibrary` теперь разделяет только semantic
owners (`wood_facade`, `wood_fence`, `wood_furniture`, `wood_bark`) с тем же
source albedo и разными world scales, а `cloth` явно указывает на bounded
`old_fabric_v2` descriptor. Shader, geometry, collision, saves и narrative
state не меняются. Следующие gates — Godot near/mid/far traversal, end-grain
orientation, 20–30 m repetition, clothing readability, cultural review и art
lock; это не финальное утверждение качества текстур.

## Authored yard/threshold modules remain presentation candidates

Слабое место: `WellA_`, `WoodpileA_` и `GateA_` добавляют культурно читаемые
силуэты, но пока являются только декоративными импортированными мешами. У них
нет отдельной gameplay collision authority; GateA также может визуально
перекрыть marker выбора или проявить повтор при дальнем проходе.

Путь решения: держать zone floor/path и существующие layer-2 proxy colliders
единственными физическими владельцами, проверять GateA в PineA-композиции, а
well/woodpile — в HouseA-композиции. Статические Act 2/Act 5 captures и
runtime-backed marker-clearance smoke уже записаны; остаются near/mid/far
screen-space traversal, 20–30 m repetition review, authored family coverage и
release-host performance. Не подключать GateA к HouseA-эпилогу и не лечить
возможные floating/occlusion проблемы новой текстурой.

## Puddle silhouette remains a diagnostic, not a selected production mesh

Слабое место: исходный низкий `CylinderMesh` читается как отдельная толстая
пластина с вертикальным бортом. Новый in-memory фасетный `ArrayMesh` убирает
этот борт и сохраняет exact-owner контакт ≤5 мм, но при общей source
roughness `0.90` почти исчезает в обзорном кадре; это не доказывает ни сырость,
ни читаемость в движении.

Путь решения: сравнивать source/candidate contact sheets из
`art/puddle_silhouette_candidate/` вместе с управляемым wetness review, а не
подменять проблему повышением albedo или глобальным снижением roughness. До
отдельного level-art/traversal решения не активировать candidate mesh в
`PainterlyEnvironmentDetails`; сохранить силуэт, wetness, temporal comfort и
art lock OPEN.

## GLB family contract does not replace mesh-collision review

Слабое место: даже project-original `.glb` может пройти registry hash и всё
же получить лишний import-helper или визуальный mesh вне опубликованных LOD
пар. Prefix-only выборка уже проявила такой случай: importer оставил у
`HouseA` не-LOD дочерний render helper, который раньше становился видимым.

Путь решения: `GeneratedModularKitContractSmokeTest` instantiates all nine
families presentation-only and fail-closes on exact `_`-terminated prefixes and
pairs `12/12, 6/6, 1/1, 2/2, 5/5, 6/6, 7/7, 4/4, 4/4`, one-to-one LOD names,
visibility ranges, exact semantic `ShaderMaterial` owner sets, imported-physics
removal metadata and any physics descendant. `GeneratedModularKitDressing` now accepts
only explicit `_LOD0`/`_LOD1` render meshes for published visibility, so the
shortened HouseA/OldPc helpers stay hidden. The current superseding modular
source is a 49-mesh contract with `OldPc_ 8/8`; the exact DriveSlot/LabelPlate
LOD pairs are asserted in Blender and Godot. FenceA is explicitly
presentation-only in the asset registry because the GLB has no `FenceA-col`.
This proves import/presentation hygiene, not player-facing mesh collision:
authored collision, Blender release-host verification, traversal, visual review
and art lock remain OPEN.

## OldPc hero details are a candidate, not a finished hero prop

Слабое место: два новых authored детали делают силуэт старого ПК понятнее на
интеракционной дистанции, но общий Soviet frame остаётся дальним, а shared
material и low-poly формы могут потерять слот/табличку при движении или на
средней дистанции.

Путь решения: `OldPcHeroDetailCapture` проверяет четыре exact LOD-имени и даёт
два Metal/Forward+ close кадра без изменения proxy/physics/runtime. Новый
`OldPcHeroDetailMotionSweep` добавляет 18 near/mid × FOV 65/75/90 × head-bob
кадров в одном isolated world; все четыре детали и non-empty readback gate
проходят. Это закрывает только техническую проекцию и устойчивость захвата:
нужны стандартный Soviet recapture, observed traversal/comfort,
repetition review, cultural/level-art review и M1/Windows performance. Не
объявлять art lock и не включать отдельный shader/texture owner только ради
усиления этих деталей.

## Act 1 demo is the current product gate, not the full-game gate

Слабое место: после большого Godot migration backlog легко принять наличие
12 full-game зон и 46-beat campaign за обязательный объём ближайшей сборки.
Это размывает проверку атмосферы, темпа и понятности первого часа.

Путь решения: текущий launch path зафиксирован на
`game/scenes/act1_demo.tscn`. Приёмка демо проверяет пять компактных зон,
16 авторских Chapter 1 beats, общий state old PC/journal/dialogue/vocabulary,
первое лицо и финальный fade-to-black «НЕ ОТВЕЧАЙ». Акты 2–5, полный
6–8-часовой playthrough, release-host gates и web retirement — deferred scope,
а не незаметно урезанная или уже пройденная часть демо. Открытые demo gates:
авторский voice/ambience mix, культурный review, observed first-time playtest,
M1/Windows performance, near/mid/far readability и art lock.

## Act 1 day-street props are presentation candidates, not collision owners

Слабое место: дневная улица получила project-original `WellA_` и `WoodpileA_`
вместо двух самых очевидных процедурных greybox-пропов, но это пока одна
вариация каждого семейства. Дальний road dressing, повторяемость заборов и
деревьев, а также культурная правдоподобность всей улицы ещё не проверены
наблюдаемым прохождением.

Путь решения: оставлять `AttachPresentationOnly` единственным visual owner;
скрытые helper-boxes зоны и layer-1 дороги остаются физическими владельцами.
Сценовые smoke-проверки должны продолжать требовать exact
`WellA_project_original|WoodpileA_project_original` metadata и ноль импортированных
physics descendants. Следующий осмысленный шаг — near/mid/far проход демо и
level-art review, а не активация новой текстуры или добавление collision к
декоративным модулям.

Diegetic wayfinding pass, 2026-08-14: existing signpost now carries a muted
`ФАП` label on the board and is covered by scene/style smoke. This is a useful
near/mid landmark, but the 27-cell sweep is still a technical projection; it
does not prove that a first-time player notices the sign, understands the
route, or finds the FAP without help. Keep observed wayfinding and cultural
review open, and do not replace world landmarks with floating quest markers.

## Act 1 physical evidence corridor is technical proof, not observed playtest

The dedicated corridor smoke now proves the real first-person handoff from the
arrival door through old PC, FAP documents, Rinat dialogue, saved message,
Татарвики reread, edge sketch, zirat and the Kara-Urman cliffhanger. Evidence
targets open the shared `DocumentUi`; the Rinat interaction commits the same
runtime `alerted` state used by the authored route. This removes the previous
silent scene-only evidence gap in the internal Godot path.

The route still has human-facing risks: the Rinat cue is an empty chair/coat/
radio presentation aid rather than a final authored character, the player must
discover the evidence targets without test teleporting, and the current smoke
does not measure pacing, controller comfort, document comprehension or cultural
read. Keep observed first-time playthrough, authored voice, language/cultural
review, release-host performance and art lock OPEN.

The new bounded physical walkthrough now drives the same route through
`CharacterBody3D.MoveAndSlide` for about 93 m and reaches the Kara-Urman
cliffhanger without writing a player transform. It closes the previous
physics/spawn regression blind spot, but it is still deterministic QA: it does
not prove that a first-time player understands the FAP landmark, reads the
documents, tolerates the camera motion or accepts the atmosphere.

## Packaged Act 1 performance is now directly measurable

`Act1DemoRoot` exposes a diagnostic-only `--urman-perf-probe` path because the
exported Godot binary cannot accept a replacement scene path. The new
`./eng/benchmark-act1-demo-package.sh` wrapper measures the published macOS ZIP
itself and supports `--urman-safe-mode`; both fresh package runs are around
120 FPS in the real Metal window on the local M4 Pro (medium Forward+, safe
Forward Mobile). This removes the editor-vs-package evidence gap,
but does not close M1/Windows performance, Windows host execution or the user
reported hardware-specific 1 FPS until that machine is reproduced.

## Interaction availability no longer polls the kernel every frame

Слабое место: старый presentation path вызывал полную сериализацию
`RuntimeKernel.SelectState()` из каждого `InteractionTarget._Process`, хотя
нарративное состояние меняется только после команд. Для слабого CPU это было
лишней работой в render/game loop и могло усиливать аппаратную просадку FPS.

Путь решения: `RuntimeBridge` теперь ставит coalesced main-thread notification
после committed state/zone changes, а `InteractionTarget` подписывается на неё,
обновляя collision layer и видимость только при изменении состояния. Прямой
kernel owner, команды, сохранения и narrative IDs не менялись. Source-tree
реальный probe после изменения: 119.97 FPS Forward+ medium и 119.99 FPS
Forward Mobile low на M4 Pro. Это закрывает только лишний polling-риск; 1 FPS
на пользовательском OS/GPU всё ещё требует target-host замера.

После этого тем же уведомлением переведён финальный detector `Act1DemoRoot`:
он больше не сериализует состояние каждый кадр ради проверки клиффхэнгера, а
проверяет правило только после изменения runtime-state и оставляет в кадре
только короткий таймер затемнения. Это уменьшает ещё один CPU-усилитель, но не
является доказательством производительности на машине пользователя.

## Act 1 OldPc is now an imported presentation candidate

Слабое место: процедурный CRT в доме был читаемым greybox-заменителем, но не
соответствовал уже проверенному project-original `OldPc_` modular kit и не
давал убедительного сюжетного якоря для первого лица.

Путь решения: дом акта 1 теперь подключает `GeneratedOldPcAct1` через
`AttachPresentationOnly`, удаляя дублирующие procedural CRT/keyboard/tower
pieces. `SceneSmokeTest` и `StyleFrameCapture` требуют exact 8/8 LOD,
положительное удаление импортированных physics nodes, ноль physics descendants
у модуля и сохранённый layer-1 `OldPc` interaction target. Это bounded
presentation progress, а не art lock: близкая читаемость, motion/comfort,
наблюдаемое прохождение, культурная проверка и target-host FPS остаются OPEN.

## Kara-Urman night frame is readable but still not art-locked

The previous night calibration compressed the path and tree trunks into a
single blue-black mass in the fixed first-person frame. The demo benchmark now
uses a small cold ambient/fog adjustment (`0.64` ambient, `0.0034` fog density,
`0.075` height density, `1.10` MoonFill) that preserves the restrained night
mood while restoring a value floor for the walkable path. The current Metal
capture and full Godot smoke pass are clean; this is still only a benchmark
presentation change. Human motion/wayfinding review, forest-family geometry,
cultural review and release-host performance remain open, and the values are
not a global full-game lighting owner.

## Act 1 slow-startup guard is bounded, not a platform-performance pass

The reported single-digit-FPS path now has a session-only presentation rescue:
after a short warm-up the entrypoint requires several consistently slow frames
and switches only the existing low material/render-scale profile. It does not
change zones, kernel state, narrative data or SaveGameV3. `--print-fps
--no-auto-performance-fallback` prints live FPS/frame-time/preset/scale/MSAA
for target-host diagnosis. The fresh package remains healthy on the local M4
Pro (`120.04 FPS` medium Forward+, `119.86 FPS` safe Mobile/low), so Windows,
M1-class hardware, editor-vs-package launch and any remaining ~1 FPS report
stay open until reproduced with the diagnostic command.

## Direct Godot launch can be mistaken for an FPS failure

Слабое место: запуск `.tools/godot/Godot_mono.app` напрямую из shell без
`eng/dotnet-env.sh` не является валидным тестом демки. На чистом окружении
Godot воспроизводимо сообщает об отсутствующих `hostfxr/coreclr` зависимостях;
это launch-path error, который нельзя смешивать с измерением FPS.

Путь решения: добавлен канонический `./eng/run-act1-demo.sh`, который подключает
закреплённый .NET runtime и запускает `game/scenes/act1_demo.tscn`; для слабого
GPU остаётся `./eng/run-act1-demo-safe.sh` с Mobile/low. На текущем M4 Pro эти
два wrapper-а дают 120.00 и 120.06 FPS соответственно. Если safe wrapper на
целевой машине всё ещё даёт около 1 FPS, остаются открытыми OS/GPU/driver,
display scaling и editor-versus-package gates; до их фиксации нельзя менять
сюжетный runtime или объявлять performance gate закрытым.

## Act 1 zone transitions now face the next landmark

Слабое место: doorway transitions preserved the player's previous yaw. После
выхода из дома или ФАПа это могло оставить игрока смотрящим назад, а на зирате
лес Кара-Урмана оказывался за спиной без объяснимого физического ориентира.
Старый corridor smoke этого не ловил, потому что перед каждой интеракцией сам
принудительно наводил камеру.

Путь решения: `Main` теперь применяет destination-facing spawn yaw только для
компактных переходов Act 1 (дом, улица, ФАП, зират, кромка леса); возврат из
леса смотрит обратно в деревню. `FirstPersonController.ApplyZoneSpawn` меняет
только presentation transform и сбрасывает наклон/скорость, не меняя kernel,
сохранения, quest state или interaction IDs. Corridor smoke проверяет
объявленный yaw после каждого перехода, а физический walkthrough проходит
93.05 м до клиффхэнгера. Human first-time wayfinding, input drift, cultural
review и art lock остаются открытыми.

## Act 1 journal now exposes the active objective without owning quest state

Слабое место: после закрытия старого ПК игрок видел сохранённую улику, но не
получал в журнале явного следующего шага. В мире уже были diegetic-ориентиры,
однако это оставляло риск, что первый игрок примет отсутствие подсказки за
сломанный маршрут.

Путь решения: `RuntimeBridge.ActiveObjectives()` читает уже вычисленную активную
цель из kernel state, а `JournalUi` показывает её в блоке `ТЕКУЩАЯ ЦЕЛЬ`. Ни
команды, ни сохранение, ни narrative IDs не меняются; журнал остаётся только
presentation projection. Smoke проверяет текст цели после сохранения официальной
справки. Остаются открытыми наблюдаемая понятность маршрута, формулировка для
первого игрока и локализационный review.

После этого сохранение также подтверждается коротким контекстным статусом с
кнопкой журнала (`[J]`/`[Y]`). Это снижает риск, что игрок не заметит новую
проекцию, но не закрывает observed first-time comprehension.

## Act 1 audio-first cliffhanger required ordered presentation

Слабое место: вход в Кара-Урман отправляет два audio events подряд. При
логических audio-ref без физических записей один label мог сразу заменить
текст голоса Марата предупреждением Рината, поэтому технический smoke видел
только последний cue.

Путь решения: `AudioCueUi` теперь держит короткую presentation-очередь и
показывает `Marat → Rinat`; `Act1DemoRoot` ждёт её перед финальной карточкой.
Это закрывает только потерю текста в одном кадре. Authored voice, финальный
ambience/mix, редактура субтитров, timing по реальному голосу и культурная
listening-проверка остаются production-gates.

## Act 1 pine palette improves value separation but is not authored canopy

Слабое место: одинаковый хвойный цвет усиливал повторяемость процедурных
ярусов на кромке Кара-Урмана и сжимал ближний, средний и дальний планы в одну
массу. Это presentation-проблема первого акта, а не повод добавлять шум,
пикселизацию или новый material owner.

Путь решения: `StyleBenchmarkZone` получил детерминированную приглушённую
палитру из пяти холодных хвойных оттенков, выбираемую из мировых координат
ствола. Геометрия, коллизии, shader, texture registry, сохранения, narrative
state и акты II–V не менялись. Реальные Forward+ style frames подтверждают
более читаемое разделение value planes, но forest family всё ещё procedural.

Остаются открытыми authored canopy/ветви, near-mid-far motion sweep,
проверка на M1 и Windows и human art/cultural review. Не усиливать это
текстурным шумом и не объявлять art lock по одному still-кадру.

## Act 1 exterior world is integrated but visual 360 review is not yet captured on the new layer

Слабое место: `AgentBExteriorWorld` теперь даёт production-маршруту
авторский террейн, архитектуру, зират и кромку Кара-Урмана, а физический
walkthrough проходит весь путь (91,76 м, cliffhanger completed). Однако
45-кадровый first-person visual review нового слоя в связке с интерьерными
зонами ещё не снят: существующие кадры — либо изолированный эксперимент
Agent B, либо старый greybox.

Путь решения: следующий capture pass должен снять forward/back/left/right/
depth по восьми зонам уже из `act1_demo.tscn` (реальная камера игрока,
Forward+, без окна на рабочем столе пользователя) и прогнать структурные
QA-гейты no_empty_horizon / no_degenerate_frames на новом слое.

Остаются открытыми культурное ревью меток зирата, ночная экспозиция Кара по
человеческой оценке, M1/Windows performance acceptance и финальная
оптимизация. Наличие зелёных smoke/walkthrough не объявляется art lock или
release readiness.

## Act I road grade and lateral silhouettes are a bounded visual pass (2026-08-25)

Слабое место: в первом connected-world capture дорожные `WetRoad_WornLight`,
`WetRoad_MutedOchre` и грязевые плечи давали слишком контрастные светлые полосы,
из-за чего мокрая дорога читалась как набор модульных плит. Несколько боковых
секторов также теряли дальний силуэт деревни.

Путь решения: `Act1ConnectedWorld` теперь перевязывает только дорожные,
плечевые и дренажные материалы существующего wet-road kit на приглушённый
Painterly earth-профиль; коллизия, route owners и RuntimeBridge не меняются.
В `DistantPerimeterParcels` добавлены четыре малых presentation-only силуэта
из уже закэшированного authored house family и существующие заборы/деревья для
Babai yard, MainStreet, FAP и Zirat lateral views.

Headless Metal capture через `./eng/capture-act1-full-route-core-world.sh`
подтвердил 36/36 PNG, 8 visual zones, forward/back + lateral + near/mid/far,
10/10 traversal waypoints и 242.50 м маршрута; build: 0 warnings / 0 errors,
runtime leak gate: PASS в принятом запуске. Ручной просмотр показывает более
цельную мокрую дорогу и немного закрытые боковые горизонты, но не закрывает
greybox/simple-module, empty-field, authored canopy, lighting и first-impression
gates. Это bounded presentation pass, не art lock и не доказательство готовой
демки.

## Act I ground planes and lateral field edges received a bounded relief pass (2026-08-25)

Слабое место: после material grade часть ближних дорожных плеч, берм и parcel
fields всё ещё читалась как длинные BoxMesh-плиты, а fixed right-turn кадры
MainStreet и Zirat доходили до плоского дальнего поля.

Путь решения: `Act1ConnectedWorld.AddVisualLandformSegment` теперь строит
неглубокую 5×7 low-poly crown surface без вертикальных коробчатых стенок;
`AddVisualParcelPatch` использует неровный presentation-only perimeter вместо
плоского блока. В `DistantPerimeterParcels` добавлены три дальних parcel
anchors из существующего authored village kit для восточного края улицы,
дальнего двора бабая и восточной границы зирата. `AddVillageFacade` получил
сдержанную заднюю оконную обвязку для 360°-проверки. Маршрут, collision,
navigation, interactions, camera, сохранения и RuntimeBridge не менялись.

Новый headless Metal capture подтвердил 36/36 кадров, 8 visual zones,
forward/back + lateral + near/mid/far, 10/10 waypoint и 242.50 м; manual
review подтверждает более естественный relief дороги и несколько закрытых
дальних секторов. MainStreet right field, Babai hero facade, generic
low-poly silhouettes, lighting depth, cultural review и first-impression
quality остаются открытыми. Это bounded visual pass, не art lock и не готовая
демка.

## Act I MainStreet и Zirat получили адресное закрытие боковых полей (2026-08-25)

Слабое место: после relief-пасса правый поворот MainStreet всё ещё смотрел в
пустой дальний участок, а восточная сторона зирата читалась как плоский
горизонт без связи с деревней.

Путь решения: в `DistantPerimeterParcels` добавлены два sparse
presentation-only parcel anchors из существующего `urman_village_exterior_kit`
с фасадом, сараем/забором и разными деревьями; основной MainStreet anchor
подвинут ближе к реальному лучу камеры и получил небольшой mixed-tree edge.
Маршрут, collision, navigation, interactions, camera, сохранения и
RuntimeBridge не менялись.

Headless Metal capture `sideclosures6` подтвердил 36/36 PNG, 8 зон,
forward/back + lateral + near/mid/far, 10/10 waypoint и 242.50 м; manual
review показывает читаемую дальнюю связку фасадов/деревьев в MainStreet и
Zirat. Пустое небо, generic low-poly foliage, простой rear hero facade,
свет, культурная проверка и first-impression gate остаются открытыми. Это
ограниченный composition pass, не art lock и не готовая демка.

## Act I hero house получила локальный rear-window light cue (2026-08-25)

Слабое место: при развороте во двор бабая/әби фасад оставался читаемым как
простая тёмная масса, без тёплого следа недавней жизни.

Путь решения: только placement `BabaiApproachDwellingFacade` получил один
низкоэнергетический `OmniLight3D` внутри presentation-only rear dressing;
дальние копии фасадов остались material-only. Это не новый global light owner:
маршрут, коллизия, navigation, interactions, сохранения и RuntimeBridge не
менялись.

Новый headless Metal capture `hero-warmth` прошёл 36/36 кадров, waypoint и
runtime gates; manual review подтверждает локальный тёплый акцент, но простой
hero silhouette, daylight contrast, authored materials, культурная проверка и
first-impression gate остаются открытыми. Это локальный lighting pass, не art
lock и не готовая демка.
