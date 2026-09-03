# «УРМАН» — handoff атмосферной демки первого акта

Дата сборки: 2026-08-24
Статус: **production connected-world integration; visual 360° gate OPEN; не готовая демка, не release и не art lock**

## Что проверять

Это production-progress first-person сборка первого акта на Godot 4.7.1 .NET с
Painterly Low-Poly 3D-подачей. `Act1ConnectedWorld` удерживает пять логических
зон в одном persistent root, а `AgentBExteriorWorld` добавляет authored
внешний слой, terrain/road collision, foliage и дождь. Физический walkthrough
проходит весь маршрут, но визуальная приемка ещё не закрыта: старые style
frames не заменяют full-route 360° capture, near/mid/far review и human
wayfinding. Поэтому этот handoff по-прежнему не заявляет готовую демку.

Последняя production composition correction закрывает конкретный duplicate-
owner риск ФАП: `FapClinicAuthoredKitPresentation` владеет внешним фасадом,
двором и ориентиром, а одноимённое `Fap_*` семейство внутри Agent B
village-buildings kit скрывается до построения архитектурной коллизии. Локальная
сцена ФАП по-прежнему владеет waiting room, desk/document targets и narrative
handoff через `RuntimeBridge`; на authoring board добавлен один физически
привязанный label `ФАП`, а не floating quest marker. Это структурная гарантия,
не визуальный art acceptance: FAP forward/back/side кадры, wayfinding
comprehension и культурный review ещё нужно пройти вручную.

Отдельно закрыт contamination-риск foliage-kit: `AgentB_FoliageKit` теперь
используется только как скрываемая source-библиотека, а в мире остаётся один
детерминированный `AgentB_PlantedFoliage` layer. В план добавлен отдельный
low-contact pass для мокрых обочин, дворов и перехода к Кара-Урману; это не
новая gameplay-система и не замена authored foliage review. Повторяемость,
плотность, силуэты и human 360° review всё ещё требуют ручной проверки.

Атмосферный owner также маршрутизирован явно: exterior logical zones используют
один `AgentBEnvironment`/`AgentBSun`, а house/FAP включают только локальный
`WorldEnvironment`. Smoke проверяет отсутствие второго active environment и не
подменяет этим ручной review тёплого дома, exterior-дождя или ночной кромки;
rain emitter также не работает внутри house/FAP. Для Kara добавлены три слабых
cool bounce cue (`KaraThresholdBounce`, `KaraRootBounce`, `KaraGestureBounce`):
они включаются только в `kara_urman_night`, не создают gameplay-владельца и
проверяются dedicated smoke через перемещение test-owned camera. Это исправляет
структурный value-floor риск, но не заменяет human review ночной читаемости,
тумана, дождя и финальной композиции.

Standalone-вариант `AgentBAct1World` теперь считается историческим
экспериментом, а не вторым runnable-маршрутом: его сцена, probes и windowed
launcher удалены. Production-внешний слой — только
`Act1ConnectedWorld/Act1CoreWorldGreybox/AgentBExteriorWorld`; старый отчёт и
receipt оставлены как provenance, не как доказательство готовности.
Слой помечен в runtime как `variantStatus=production-canonical`, а smoke
проверяет этот контракт и canonical entrypoint `res://scenes/act1_demo.tscn`.

Запуск по умолчанию: `game/scenes/act1_demo.tscn`. Для исходного проекта
используйте `./eng/run-act1-demo.sh`: wrapper подключает закреплённое .NET
окружение перед запуском Godot. Прямой запуск бинарника Godot без
`eng/dotnet-env.sh` может завершиться ошибкой `hostfxr/coreclr` и не является
валидным FPS-тестом. Веб-рантайм, browser
localStorage, акты 2–5 и полный 6–8-часовой маршрут в эту демку намеренно не
входят.

## Управление

| Действие | Клавиатура и мышь | Геймпад |
|---|---|---|
| Идти | `WASD` | левый стик |
| Смотреть | мышь | правый стик |
| Начать / осмотреть / подтвердить | `E` или левая кнопка мыши | `A` |
| Журнал | `J` | `Y` |
| Меню и настройки | `Esc` | `Start` |

Первая карточка управления остаётся на экране до подтверждения игроком и
автоматически показывает последнюю обнаруженную схему ввода. В настройках
можно переназначить клавиши/кнопки и оси, FOV (65–90°), чувствительность,
head-bob, reduced motion, high contrast, text scale, subtitles и graphics
preset. Карточка также формулирует первую цель: добраться до дома и проверить
старый компьютер; это presentation-only подсказка и не отдельный quest-state
owner. Для ручного плейтеста обязательно проверить и клавиатуру/мышь, и
геймпад; текущий smoke подтверждает wiring, но не заменяет наблюдение за
реальным игроком.

После сохранения первой записи журнал дополнительно показывает блок `ТЕКУЩАЯ
ЦЕЛЬ`. Это read-only проекция активной цели из общего runtime quest-state:
журнал не создаёт, не завершает и не подменяет квест. Если активной цели нет,
блок остаётся нейтральным (`—`); это не GPS-маркер и не новый сюжетный owner.
После успешного сохранения старый ПК/документ также показывает короткий статус
`Откройте журнал [J]` или `Откройте журнал [Y]` для последнего устройства.

После первого входа в деревню журнал также показывает блок `ТАТАРСКИЕ СЛОВА`.
Первое технически доступное слово — `урман` с объяснением «граница старых
правил». Это read-only проекция уже записанного vocabulary-state: журнал не
угадывает слова, не выдаёт награду и не создаёт отдельный языковой прогресс.
Культурная и языковая редактура формулировок остаётся обязательным открытым
гейтом перед публичным плейтестом.

## Маршрут ручного прохождения

1. На въезде подтвердить карточку управления и осмотреть дом.
2. В доме открыть старый ПК, прочитать официальный документ и сохранить
   важную запись в журнал.
3. Выйти на дневную улицу, найти указатель `ФАП` и пройти в клинику.
4. В комнате ожидания осмотреть стол документов и прочитать извещение о
   смерти Марата.
5. Вернуться в дом, найти физический ориентир Рината (стул, пальто и радио)
   и пройти диалог. После него проверить состояние `alerted` через обычный
   журнал/документы, а не через debug-команду.
6. Последовательно прочитать сохранённое сообщение, источник Татарвики о
   границе, его повторное прочтение и зарисовку кромки Кара-Урмана.
7. Выйти через зират на ночную кромку леса и дождаться затемнения с
   `НЕ ОТВЕЧАЙ` / `Конец демо`.

После каждого перехода между компактными зонами камера получает
destination-facing yaw: дом, ФАП и зират смотрят в сторону следующего шага, а
возврат из леса — обратно к деревне. Это не GPS и не новый quest-state owner;
это только защита от ситуации, когда игрок после двери продолжает смотреть на
предыдущую локацию.

На входе в лес две логические audio-ссылки идут в короткой presentation-очереди:
сначала текст следа Марата, затем предупреждение Рината. Это сохраняет оба
субтитровых/описательных cue даже до появления authored voice-файлов; финальная
карточка появляется только после их показа. Это не меняет beat state и не
является заменой финального voice/mix pass.

Ручной сессии не следует телепортировать игрока, вызывать
`RuntimeBridge.DispatchInteractionAsync` или переключать зоны из debug-кода.
Все переходы должны выполняться камерой, лучом взаимодействия и mapped
input, как в `act1_first_person_corridor_smoke_test.tscn`.

## Техническая evidence

- `. ./eng/dotnet-env.sh && ./eng/verify-dotnet.sh` — build без warnings/errors,
  Core 38/38 и Content 12/12.
- `./eng/verify-godot.sh` — exit 0: scene/import, input/ray, old PC, journal
  (включая отображение `урман` и его смысла), persistence, 16-beat Chapter 1,
  physical corridor, 93 m physical
  first-person walkthrough и регрессии; после
  cleanup shared `Shape3D` нет RID leak.
- `./eng/verify-desktop-artifacts.sh --check-receipt` — PASS, 203 файлов в
  Windows payload и receipt `build/desktop-artifact-receipt.json`.
- `./eng/verify-macos-host.sh` — PASS на текущем Mac: universal app,
  embedded PCK/.NET, Forward+ и first-person bootstrap.
- `./eng/run-act1-demo-safe.sh` — диагностический запуск для слабого или
  программного GPU: Mobile renderer + low painterly material profile.
- `./eng/run-act1-demo.sh` — обычный запуск исходной демки с Forward+ и
  закреплённым .NET окружением; дополнительные аргументы передаются Godot.
- `./eng/benchmark-act1-demo-package.sh` — probe опубликованного macOS ZIP
  через главный `act1_demo.tscn`; флаг `--urman-safe-mode` сравнивает safe
  ветку без scene-path override. На последней пересборке 2026-08-15 реальные
  Metal-прогоны дали `119.82 FPS` (medium Forward+) и `120.04 FPS`
  (safe Forward Mobile/low) на локальном M4 Pro. Оба результата выше
  внутреннего floor 30 FPS; значения предыдущих запусков сохраняются как
  исторический диапазон, потому что pacing окна зависит от фоновой нагрузки.
- Расширенный source probe отдельно печатает первые 20 warm-up кадров: medium
  `12.993 ms` average / `64.829 ms` max, safe `10.873 ms` average /
  `48.195 ms` max. После прогрева источник держит около 120 FPS; это не похоже
  на постоянный 1 FPS loop на текущем хосте.
- При подтверждённо медленном старте демо после нескольких устойчиво тяжёлых
  кадров автоматически включает session-only low-профиль (без изменения
  сюжета, сохранений или состояния кампании). Для диагностики без такого
  fallback можно запустить `--print-fps --no-auto-performance-fallback`;
  встроенный вывод Godot печатает live frame-time/FPS примерно раз в секунду.

Проверка vocabulary UI: `. ./eng/dotnet-env.sh && ./.tools/godot/Godot_mono.app/Contents/MacOS/Godot --headless --path game res://tests/journal_flow_smoke_test.tscn`
завершается с `journal-flow-smoke` и проверяет, что после первого состояния
словаря журнал содержит `урман` и «граница старых правил».
## Acceptance audit playable-границы Акта 1 — 2026-08-15

| Требование текущей цели | Evidence | Статус |
|---|---|---|
| Godot 4.7.1 .NET и default Act 1 entrypoint | Godot 4.7.1 export, `act1_demo.tscn`, `Act1DemoLaunchSmokeTest` | PASS |
| Фиксированное первое лицо и compact-zone traversal | first-person interaction smoke, corridor smoke, physical walkthrough `93.05 m` | PASS |
| Единая карта Акта I и непрерывный обзор | текущий `Main.SwitchZone` выгружает `ZoneHost`; unified connected-world capture ещё не создан | OPEN — главный текущий blocker |
| Приезд Айдара и safe zone дома | `arrival` → `house`, `ChapterOneFlowSmokeTest` | PASS |
| Расследование Марата через старый ПК/архив | official notice → internal register → saved message, `oldpc-flow-smoke` | PASS |
| Первый татарский слой | Gulsina/Alsu vocabulary effects, `DialogueFlowSmokeTest`, reread gate на `tt_javap` | PASS (technical; language review OPEN) |
| Мистический след и «Не отвечай» | boundary article, edge sketch, ordered Marat/Rinat cues и final card | PASS (presentation; authored voice OPEN) |
| Acts II–V не входят в demo route | `Act1DemoRoot` starts at `village_day` и не подключает full-game entrypoint | PASS |
| Атмосферный Painterly Low-Poly presentation | три benchmark-кадра существуют только как isolated scene evidence | OPEN — не считать готовой демкой до connected map и human review |
| Стабильность на target hardware | local M4 package `119.82/120.04 FPS` | OPEN: M1/Windows и внешний 1 FPS report |
| Наблюдаемая понятность для первого игрока | automated smoke не заменяет human playtest | OPEN |

Последний полный `./eng/verify-godot.sh` прошёл без build warnings/errors;
transition diagnostics: `17.2 ms` в дом, `14.8 ms` к Кара-Урману и `14.9 ms`
обратно в деревню. Таблица намеренно разделяет техническую playable-проверку,
художественный lock, культурную проверку и release hardware gates.

- Startup graphics contract: `Act1DemoLaunchSmokeTest` проверяет, что
  заявленный `medium` preset применяется до первого кадра (0.90 3D scale,
  2× MSAA); отдельный full-entrypoint probe фиксирует frame-time только как
  локальное диагностическое evidence.
- Доступность физических интеракций и проверка финального клиффхэнгера больше
  не сериализуют kernel state в каждом кадре: обе presentation-проверки
  получают coalesced main-thread уведомление после изменения runtime-state;
  source-tree Forward+ / Mobile probes после первой оптимизации дали
  119.97 / 119.99 FPS на локальном M4 Pro.
- Финальный audio presentation smoke дополнительно проверяет порядок `Marat →
  Rinat`: `AudioCueUi` не перезаписывает первый cue вторым в том же кадре,
  а `НЕ ОТВЕЧАЙ` ждёт завершения короткой очереди. Logical audio refs,
  authored voice recording, mix и культурная listening-проверка остаются OPEN.
- `res://tests/act1_first_person_walkthrough_smoke_test.tscn` проходит
  arrival → house → old PC → street → ФАП → house → зират → Кара-Урман
  без записи позиции игрока: движение идёт через production input actions и
  `MoveAndSlide`, а камера — через test-only look setter. Это технический
  traversability gate, не наблюдаемый human playtest.
- `Main` задаёт destination-facing spawn yaw для Act 1 transitions; corridor
  smoke проверяет этот контракт, а физический walkthrough подтверждает маршрут
  на 93.05 м. Human first-time wayfinding и cultural review остаются OPEN.
- `zone_flow_smoke_test.tscn` дополнительно записывает текущую стоимость
  синхронных переходов: 29.9 ms в дом, 16.8 ms к Кара-Урману и 25.8 ms обратно
  в деревню на текущем хосте. Это не закрывает target-host traversal, но не
  подтверждает постоянный 1 FPS из-за загрузчика компактных зон.
- Подробный corridor receipt: `art/first_person_corridor/README.md`.

Актуальные desktop artifacts:

- macOS ZIP — SHA-256
  `e38176747d9eecaf3fc25207d34a138197200e18dfc90c984279f9cfd58b7751`,
  161,721,643 bytes;
- Windows ZIP — SHA-256
  `b93fcc10ca090dca5ba3e780aa403a3e3e533226c2ed4a84d138379a3d8ea56c`,
  96,888,584 bytes;
- Windows EXE — SHA-256
  `ca2ccde016413ea88a77146676716643657bd0f343f39ecff8805e063582440e`,
  127,508,992 bytes.

## Known issues и честные границы

- У Рината пока нет финальной authored-модели: в доме стоит
  presentation-only cue из стула, пальто и радио. Это намеренный staging
  placeholder, а не новый канонический персонажный ассет.
- Дневная улица, дом и кромка Кара-Урмана — production candidates: лесная
  повторяемость, greybox-геометрия, туман, wetness и near/mid/far readability
  ещё требуют художественной проверки.
- Четыре ambience stems и caption fallback технически маршрутизируются, но
  authored voice, финальный mix, субтитровая редактура и non-audio cues не
  приняты.
- Технические input/accessibility smoke проходят, но нет наблюдаемого
  first-time playtest, внешней accessibility-сессии, культурного/языкового и
  религиозного review.
- Windows host execution, M1/средний Windows performance, signing и
  notarization остаются открытыми.

## Последний bounded presentation-срез — OldPc в доме акта 1

Дом теперь использует project-original `OldPc_` GLB как presentation-only
модуль `GeneratedOldPcAct1`. Процедурные коробки CRT/клавиатуры/башни удалены,
но стол, документы, мышь, лампа и единственный `InteractionTarget OldPc`
сохранены. Адаптер удаляет импортированные physics descendants до добавления
модуля в сцену; gameplay collision остаётся у layer-1 interaction target.

`SceneSmokeTest` и `StyleFrameCapture` требуют exact `OldPc_` 8/8 LOD,
положительное metadata sanitation и ноль collision descendants. Свежий
Metal/Forward+ house frame —
`godot_house_old_pc_1080p.png`, SHA-256
`bcb0a1d92c694161d84ef50051de578a54b6ab4c200435662d4160fea83428cb`.
Это production-progress candidate, не art lock: композиция, authored detail,
культурный review и наблюдаемая читаемость остаются открытыми.

## Последний bounded presentation-срез — детерминированная палитра хвои

`StyleBenchmarkZone.MakePine` теперь выбирает один из пяти приглушённых
хвойных оттенков по стабильной world-space фазе, когда зона не задаёт цвет
явно. Это разделяет value-планы переднего, среднего и дальнего леса без новых
мешей, коллизий, shader-веток или сюжетного состояния; цветовые владельцы
остаются в `PainterlyMaterialLibrary`.

Свежие real Metal/Forward+ кадры после среза:

- day street — `4c99a5483d8d9025503e320bb12dbd6b117a1e446fa78e22b6fa619de533bccb`;
- house / old PC — `115b617d91a0ea47bf65b3f46b200117f24ca5e921610845d68e124325af6903`;
- Kara-Urman edge — `ff3f37535d62f31bea4c2add821817a58fb1f3e901e105dbb1722f77e59e6826`.

Полный Godot smoke и физический walkthrough проходят; package probe после
пересборки держит `120.15 FPS` Forward+ и `120.07 FPS` Mobile/low на локальном
M4 Pro. Срез улучшает читаемость леса, но не закрывает art lock: повторяемость
одной процедурной crown-family, authored canopy/ground density, motion,
cultural review и target-host performance остаются OPEN.

## Fresh package receipt после journal vocabulary projection — 2026-08-15

Свежий `./eng/export-desktop-debug.sh` пересобрал обе платформы после
новой read-only проекции татарского словаря в журнале. `./eng/verify-desktop-artifacts.sh
--check-receipt` и `./eng/verify-macos-host.sh` прошли; receipt содержит 203
Windows payload-файла и привязан к текущим размерам/хэшам.

- macOS ZIP — `e38176747d9eecaf3fc25207d34a138197200e18dfc90c984279f9cfd58b7751`
  (161,721,643 bytes);
- Windows ZIP — `b93fcc10ca090dca5ba3e780aa403a3e3e533226c2ed4a84d138379a3d8ea56c`
  (96,888,584 bytes);
- Windows EXE — `ca2ccde016413ea88a77146676716643657bd0f343f39ecff8805e063582440e`
  (127,508,992 bytes);
- receipt — `567e45835cbdbcf7c1ffbeafbf0933d65ce82a19fe367014c3d4e309a30c47be`.

Пакетный probe: `119.82 FPS` medium Forward+ и `120.04 FPS` Mobile/low на
локальном Apple M4 Pro. Это не закрывает Windows/M1 и не является
подтверждением или исправлением пользовательского 1 FPS без сведений о
целевом OS/GPU и команде запуска.

## Out of scope текущей демки

До выполнения connected-world gate этот файл не является обещанием готовой
играбельной демки. Benchmark-кадры, текстуры-кандидаты, package receipt и
локальный FPS — только технические/референсные evidence. Следующий production
срез должен сначала закрыть общую greybox-карту и свежий 360° first-person
проход; полировка материалов и release-технические вопросы отложены.

Acts 2–5, полный 6–8-часовой перенос, production art lock, release acceptance,
web-retirement и удаление browser saves. Подготовленные данные и архитектура
этих направлений сохраняются как deferred north-star work и не должны
попадать в ручной сценарий этого playtest.
