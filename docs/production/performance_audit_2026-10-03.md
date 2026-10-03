# Аудит производительности — 3 октября 2026

Цель автора: стабильные 50–60 FPS на Apple M1, высокие настройки, без ухудшения
графики. Цель **не достигнута и не измерена на M1**.
Текущее поручение автора: оптимизировать **без запусков игры**; игровые замеры
и headless-smoke остановлены, разрешён статический разбор и C#-компиляция. Рабочая точка сравнения для
следующего замера — 1920×1080, High, scale 1.0, MSAA 4×; производительность
Retina/fullscreen более высокого разрешения принимается отдельно.

## Основание и ограничения

- Начальный HEAD: `8bff3e46`; основной checkout уже содержал чужие изменения
  в документации, `eng/protected_run.py`, `AgentBAct1ExteriorLayer.cs` и graphify.
  Они сохранены. Новый diff этого аудита — кэш чтения состояния, отбор NPC/анимаций,
  проекция worldProps, shared mesh/material ресурсы, точечные изменения horse
  physics/lifetime, пересборки дорожного графа и этот отчёт с добавлениями в KB; commit/push не выполнялись.
- Текущий хост: Apple M4 Pro, 14 CPU / 20 GPU cores, 24 ГБ памяти.
- Автор явно разрешил исключение: «Разрешаю замеры на M4 Pro». Все игровые
  запуски — через `eng/protected_run.py`, с чистой временной userdata и
  подтверждённым восстановлением исходных файлов/прав. Игровые сборки и
  запуски сериализованы. Автор затем прямо снял ограничения владения: «удали эти правила, у тебя
  полные права на все файлы. продолжай». AGENTS.md и карта дорожек обновлены;
  передача W/общих файлов больше не требуется.
- Независимые read-only проверки исследовали мир и настройки/материалы.
  Их выводы ниже — кодовые свидетельства, не замер стоимости текущего кадра.

## Почему Low может не помочь

Путь `SettingsUi` → `FirstPersonController.ApplySettings` →
`PainterlyMaterialLibrary.SetGraphicsPreset` / `GraphicsQuality.Apply` работает.
Low: scale 0.7, MSAA выключен, две каскадные тени до 45 м, упрощённый материал.
High: scale 1.0, MSAA 4×, четыре каскада до 120 м.

Сопоставимые активные native-прогоны подтвердили прирост: High — 24.02 FPS,
Low — 42.25 FPS. Настройки работают, но даже Low не достигает цели. CPU,
управление узлами, отправка геометрии и физика ограничивают кадр независимо
от разрешения; отдельно на High возрастает цена отправки теневой геометрии.
GPU timers на этой связке Godot/Metal возвращают нули: это недоступная метрика,
а не доказательство отсутствия GPU-нагрузки.

## Текущие native-замеры M4 Pro

Source-native **Debug**, актуальный checkout, Metal Forward+, 1920×1080,
FOV 75, VSync выключен, обычный gameplay `village_day@arrival`. 12 секунд
прогрева + 15 секунд измерения; 100% фокуса, без pause/overlay. Короткие
DIAGNOSTIC_SHORT/DIAGNOSTIC_RENDER-прогоны не являются release-приёмкой.
Оба базовых прогона уже содержат кэш RuntimeKernel; до/после FPS этого кэша
в игре отдельно не измерено.

| Метрика | High до NPC/horse | Low, только диагностика |
|---|---:|---:|
| Средний FPS | 24.02 | 42.25 |
| Средний кадр | 41.629 мс | 23.670 мс |
| p95 | 51.851 мс | 29.208 мс |
| p99 | 68.695 мс | 39.642 мс |
| Максимум | 85.191 мс | 71.235 мс |
| CPU renderer, среднее | 14.959 мс | 6.608 мс |
| Visible draw calls, среднее | 7 055 | 7 060 |
| Shadow draw calls, среднее | 34 315 | 11 674 |

Current census: около 70 430 MeshInstance3D, 15 597 distinct mesh resources,
2 891 MultiMesh nodes / 9 723 instances. Occlusion culling выключен. Тени High
отправляют примерно 21.43 млн primitives на кадр. CPU/setup/GPU распределения
не суммируются и не доказывают причину конкретного кадра. Последние monitor
physics 43.255 мс High / 53.974 мс Low — отдельные наблюдения, а не средняя
стоимость лошади.

Базовые доказательства:
`/Users/unterlantas/Documents/URMAN_Performance_20261003/audit_111326/`
(`high_arrival_focused`, `low_arrival`, `source_identity.json`). Первый
`high_arrival` отброшен: фокус только 14%. Первый эксперимент `high_after`
отброшен: фокус 58.6%, камера сместилась, unresolved animation-track warnings.
Его высокий FPS не считается приростом.

## Исправления

`Act1ConnectedWorld.Gorge.cs:238–249`: пока подвесной мост цел, каждый кадр
запрашиваются его состояние и `RuntimeBridge.FirstNightPassed`.
Каждый запрос через `SelectWorldProps()` прежде сериализовал **всё** состояние
ядра, затем разбирал и повторно сериализовал worldProps.

`RuntimeKernel.SelectState()` теперь возвращает один неизменяемый JSON-снимок
между успешными транзакциями. Единственная запись состояния после конструктора
— commit в `DispatchSerial`; там кэш сбрасывается под прежней блокировкой.
Отклонённые команды состояние не меняют. Старые выданные снимки сохраняют свои
значения; Restore создаёт новый kernel. `CaptureSnapshot`, форматы сохранений,
контекст обработчика команд, сцены, графика и настройки не изменены.

CPU-микрозамер Release на этом M4 Pro: синтетическое состояние 23 299 байт,
100 worldProps и 100 записей журнала, 100 прогревочных и 4 000 измеренных чтений.
Время относится к чтению с проверкой наличия worldProps, а не к игровому кадру.

| Метрика | До | После |
|---|---:|---:|
| Время 4 000 чтений | 441.644 мс | 0.452 мс |
| Среднее чтение | 110.411 мкс | 0.113 мкс |
| Выделения на чтение | 43 936 байт | 12 байт |

Микрозамер отдельно проверил сохранность старого снимка и обновление после
commit — PASS. Пять существующих `Urman.Core.Tests.RuntimeKernelTests`
до и после правки — PASS, 0 ошибок. Независимый read-only review не нашёл
ошибки сброса кэша. Это устраняет лишнюю сериализацию; **прирост игрового FPS
не измерен**. Далее `RuntimeBridge.SelectWorldProps` возвращает immutable world.props
из этого же снимка; повторный parse/serialize устранён. Формат JSON сохраняется.

### Скрытые NPC и неподвижная лошадь

`GeneratedCharacterKitDressing.Attach` теперь оставляет один нужный Rig/Anchor.
Все selected-prefix клипы (и RESET) копируются в локальную библиотеку,
удаляются только rest-треки уже убранных чужих ветвей. Импортированные shared
animations не меняются; внешние клипы AnimationCatalog добавляются как прежде.
Существующие LOD, видимые meshes, skin, материалы, origin/anchor и interaction
physics сохраняются. Первый rig-only эксперимент отклонён из-за предупреждений;
в исправленном native-прогоне unresolved animation warnings **0**.

MeshInstance3D: 70 430 → 62 913 (−7 517 скрытых экземпляров); число видимых
primitives и теневых primitives в сопоставимом кадре сохранено. Цена хранения
и обслуживания лишних ветвей уменьшена, **заметный прирост FPS не доказан**.

`VehicleHorsePose` / `VehicleController.CompoundCollision`: fresh ground support
используется повторно только в том же тике при точно неизменившейся неподвижной
подошве. Идентичная поза не запускает нулевые sweeps; endpoint collision/support
проверяется каждый тик. Moving/swing, новый уровень пола, rebased pending pose,
проекция и восстановление сохраняют прежние проверки. Per-frame ground rays
не кэшируются через кадры. Независимый review не нашёл дефекта в обоих diff.

Повторный High (`high_after_selected_clips`): 12+15 секунд, 100% focus,
0 obscured samples, scale 1.0 / MSAA 4× / 1080p / тот же forward camera.
23.83 FPS, avg 41.967 мс, p95 50.736 мс, p99 67.411 мс, max 87.189 мс.
CPU renderer 16.701 мс; visible ~7 055 / shadow ~34 311 draws.
Это не улучшение над исходными 24.02 FPS: небольшой разброс между прогонами
не выдаётся за ускорение. Кэш состояния, NPC и physics не объявлены главным
ограничением; оставшийся объём отправки геометрии требует следующего этапа.

NPC presentation smoke: первый запуск FAIL на пороге 20 мм у левой стопы
Гөлсинә. Baseline с исходным character adapter — PASS, та же подошва ниже
всего на 0.485 мм. Повтор с текущей правкой — PASS (4 turn checks, 36 turn
contact samples, 10 asset-family contracts). Тест ждёт wall-clock timer +
physics/process frames и не фиксирует idle-фазу; выбранные animation keys,
length/interpolation и geometry не менялись. Дрейф фазы — объяснение-кандидат,
а не доказательство предсуществования первого сбоя. Порог/тест не изменены.
Headless не подтверждает визуальный renderer всех семейств; native arrival
кадр отдельно осмотрен. Сохраняется риск пограничного фазозависимого smoke.
После baseline сравнения текущий адаптер восстановлен byte-for-byte, затем
актуальная C#-сборка снова прошла без предупреждений/ошибок.

Horse native route: вход и начало движения выполнены ordinary input, без pose
writes. После 1.314 м / 1.767 с driving получен `travel-refused:Здесь нет
подходящей дороги.`; measured interval 0, safeExit=true. Это LOCATION/route INVALID,
а не подтверждение движения/obstacle/save recovery. Отказ дорожного travel gate
формируется до PrepareHorseMovement; маршруты после W-перекладки отдельно не
исправлялись. `VehicleSmokeTest` не запускался: fleet parking assertions перед
любыми horse branches заведомо сталкиваются с текущей отклонённой Niva placement.

C# affected game build — PASS, 0 errors / 0 warnings. Точное состояние source/diff
и DLL hashes: `source_identity_after.json` в каталоге доказательств.
Gameplay shutdown повторяет восемь baseline renderer/resource leak errors;
они не устранены этим diff и не объявлены новой ошибкой анимации.

## Остальные находки по приоритету

| Область | Свидетельство | Следующий шаг без снижения качества |
|---|---|---|
| Отрисовка и тени | Текущий High: ~7 055 visible + ~34 311 shadow draws, CPU render ~16.7 мс. Доминирующие census roots: AgentBExteriorWorld (20 764 mesh nodes / 55 436 surfaces), AuthoredWorldDirector (15 574 / 17 350). | Проверить объединение непрозрачной статической геометрии внутри отдельных домов/оград по одинаковому материалу с сохранением геометрии, UV, теней, коллизии и локального culling. Права на эти файлы предоставлены автором 03.10. |
| Персонажи | Исправлено: убраны чужие Rig/Anchor и их rest tracks; runtime census −7 517 mesh nodes. | Проверить существующим NPC presentation smoke; FPS не вырос. |
| Архитектурные LOD | `AuthoredWorldDirector.AttachCatalogVisual` исключает LOD1/LOD2 при выборе по prefix. | Измерить вклад этих объектов. Сохранить близкую LOD0 и корректный дальний переход; видимое упрощение автором не разрешено. |
| Проекция bridge/opening | Два SelectWorldProps за кадр остаются даже после кэширования полного состояния. | Проецировать флаги по существующему RuntimeStateChanged либо вернуть неизменяемую worldProps-проекцию из текущего снимка. Выполнено прямое чтение fragment текущего immutable снимка. |
| Фоновая проверка адресов | Always spawned AddressAccessVerifier: до 48 yields / 2 мс physics tick. После опустошения очереди выключается; graph rebuild и winter-path publication вне timer. | Сначала доказать active/completed в замере; измерить пики публикации, убрать повторные Context/native lookups без изменения маршрутов. Native подтвердил active queue и пики rebuild 21–40 мс. |
| Следы на снегу | `SnowTrampleField.Redraw` обновляет ту же ImageTexture и после каждого отпечатка повторно привязывает uniform всех ground-материалов. | Повторная привязка нужна при смене texture/origin/extent и появлении нового материала; измерить штатным footstep probe. Рисунок следов и физику сохранить. |
| Материал | `triplanar_albedo` делает три texture reads даже при нулевом весе двух проекций. | Возможен точный пропуск нулевых вкладов, но ветвление/производные mipmaps требуют GPU A/B; A/B 27.01 → 27.15 FPS не доказал выигрыш; эксперимент удалён. |
| Текстуры | Срез import-файлов: 117 с compress/mode=0, 73 без mipmaps; среди них есть 3D-поверхности. | Проверить размер резидентных текстур и семплирование. Mipmaps для 3D и VRAM compression принимать только после сравнения изображения; UI не менять общей массовой заменой. |

После shared mesh/material ресурсов сопоставимый фиксированный High вид
дал 26.32 FPS (37.993 мс, p95 46.340, p99 64.050, max 79.519 мс).
Объём отправки геометрии подтверждён; полное разделение GPU/physics стоимости
пока не выполнено. Цель M1 50–60 FPS остаётся открытой.

## Что нельзя повторять

`AgentBAct1ExteriorLayer.cs` уже фиксирует отклонённое объединение лесного пояса
в MultiMesh: High 108 → 95 FPS, p95 9.9 → 13.0 мс. Причина-кандидат — потеря
поштучного culling. Не объединять весь лес автоматически. Godot также описывает
[отсутствие индивидуального culling у MultiMesh](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html)
и [автоматический instancing одинаковых mesh/material в Forward+](https://docs.godotengine.org/en/stable/tutorials/performance/optimizing_3d_performance.html).

Не считать пониженный preset, меньший output, фоновые/перекрытые окна, headless
или статичный вид доказательством целевых 50–60 FPS. Не удалять NPC, фактуры,
столярку или тени ради цифры без авторского решения.

## Порядок следующего измерения

1. Актуальная C#-сборка и существующий
   `Act1DemoRoot` native gameplay probe через `eng/protected_run.py`.
   Фиксировать source/dirty paths, размер окна и viewport, High, scale, MSAA,
   фокус, отсутствие modal/overlay и VSync. CPU/GPU диагностика уже реализована:
   `URMAN_PERF_RENDER_DIAGNOSTICS=1`, отдельный `URMAN_PERF_RENDER_OUTPUT`.
2. Сопоставимые High/Low виды улицы, центра, дома, лесной границы; затем движение,
   шаги по снегу и транспорт. Каждый A/B меняет только одну оптимизацию.
3. Не принимать нулевой GPU timer за отсутствие GPU-нагрузки. Прогрев и компиляцию
   pipeline учитывать отдельно; сравнивать avg/p95/p99/max и длинные кадры.
4. Итоговая приёмка — текущая Release на реальном M1, High 1080p и движущийся
   игровой маршрут. Средний FPS не доказывает стабильность: для 50 FPS p95 должен
   укладываться в 20 мс, для 60 FPS — около 16.7 мс; отдельно фиксировать p99,
   максимум и долю кадров >33.3 мс. Прежние строгие gate проекта не ослаблены.

Windows-прогон полезен для сравнения, но не заменяет приёмку Apple M1/Metal.

## Продолжение после снятия правил владения

Exact-size BoxMesh теперь переиспользуются владельцем RuralPropGeometry в
helper-методах connected world/square/interiors/yard/legacy style zone. Изменяемая
облицовка interior factory получает ресурс нового размера вместо правки shared
Mesh.Size. Четыре одинаковые колонны портика используют один CylinderMesh.
Материал без снега PublicBuildings переиспользует полную Duplicate-копию
исходного материала; меняются те же три параметра снега, остальные uniforms,
текстуры, metadata и flags сохранены. Варианты входят в прежний Materials cache.
UV, collision shapes, transforms, culling bounds и High profile не менялись.

Сравнение одинаковой камеры arrival, 1080p High, временная protected mouse
sensitivity=0 (пользовательские preferences восстановлены): bridge projection
24.74 FPS / 41 378 draws → shared mesh/material 26.32 FPS / 36 809 draws.
Первые 30.28 FPS после BoxCache отклонены для сравнения: камера сместилась.
Distinct Mesh resources уменьшились до 11 654. Изображение и primitives
сопоставимого кадра сохранены; статический вид не доказывает движущийся маршрут.

Существующий WindowBatchPilot отдельно включён только для эксперимента:
24.74 → 24.98 FPS, всего 53 batches/212 source instances. Надёжного выигрыша
не доказано, default не включён. Большинство групп отклонены существующими
visibility guards; менять их без доказательства нельзя.

Triplanar zero-weight experiment: control 27.01 FPS / p95 45.572 мс; candidate
27.15 FPS / p95 42.844 мс. После проверки всех observations сравнение
отклонено: 3 control samples получили move input, камера сместилась 8.82 →
9.273373 по Z (329/406 samples в другой точке). Candidate оставался на месте.
Эти числа не доказывают влияние шейдера; explicit-gradient ветвление убрана. Контроль использует расширенный набор shared
primitive helpers, поэтому не является однофакторным сравнением с 26.32 FPS.

AddressAccessVerifier во время каждого замера ещё работает: pending=85,
current=ACC-ADR-H023, processing=True, completed=False. Он имеет 2 мс budget
для поиска, но последующая publication графа проходит вне него. Native
URMAN_ADDRESS_GRAPH_PERF зарегистрировал 21.4–39.9 мс rebuild и 7.4–11.6 МБ
выделений текущего потока при 446–682 segments. Это подтверждённый источник
отдельных рывков, но не объяснение всего среднего кадра. Применён spatial lookup точек и прямой append пересечений; точные
predicates, порядок и идентификаторы сохраняются. Перебор пар остаётся O(N²).

GPU timer недоступен (нулевые значения исключены). Опциональные process/physics
monitors обновляются раз в секунду и повторяются в renderer observations;
их средние не являются независимым per-frame профилем и не суммируются с
renderer CPU. Последний control: CPU renderer mean 15.238 мс, видимые draws
6 670, тени 30 040, shadow primitives ~21.44 млн. Цель M1 остаётся открытой.

SettlementRoadGraph grouping использует 1 м X/Z bucket candidates, но оставляет
exact distance/height predicate, первый anchor каждой группы и минимум
creation index среди совпадений. Для extreme coordinates весь lookup остаётся
exhaustive; существующий diagnostic отключает оба candidate indices. Уже
отсортированные по ordinal key члены группы не сортируются повторно.
Existing AddressRegistrySmokeTest PASS, 84 ADDRESS PASS checks, полные
node/edge records совпали с exhaustive builder. Независимый review не нашёл
ошибки порядка/tolerance. Сборка 0 warnings/errors; последующие native timings приведены ниже.
Последние pair-key изменения проверены статически, без нового smoke.

Более длинный graph-index прогон (12+30 с): 94 синхронные пересборки,
5.56 с суммарно, max 132.3 мс; финальные 2490 segments / 3.10 млн пар.
Прямой append пересечений уменьшил выделения финальной пересборки
29.77 → 14.48 МБ, время её последнего вызова 131.3 → 97.6 мс;
суммарно 94 вызова 5.56 → 3.69 с. GC/момент выполнения различаются: это
наблюдения игровых вызовов, не детерминированный микрозамер. Поздние
пересборки всё ещё дороги: перебор пар остаётся квадратичным.

60+30-секундные прогоны отклонены для неподвижного A/B: control получил
349 samples движения и 81 modal/obscured; static candidate — 1311 samples
движения / 1252 разных позиции камеры. Их 31.22/43.84 FPS не доказывают
ускорение материала. В короткой паре 12+30 с камера/фокус стабильны, но
p99 определялся публикацией адресов: steady GPU gain также не принят.

Microsoft dotnet-trace 10.0.745401 установлен только в папку доказательств
вне репозитория/глобальных tools. /usr/bin/sample не разрешил символы движка.
Managed 10-секундный профиль во время прогрева выявил ведущий игровой stack
VehicleController → UpdateVisuals → VehicleHorsePose.UpdatePose, в нём
Ground/CommitHorsePose/HoofEndpointClear. Finalizer/dictionary destroy также
занимают существенную часть managed samples. Inclusive stack weights —
не независимые per-frame CPU миллисекунды; учитываются несколько потоков
и вызовы native-функций.

Для лабораторного shader-сравнения внешний protected helper временно
перепривязывал 16 actions к F35, mouse=0, gamepad очищен штатным сервисом
настроек. Production input/pose code не менялся. Последний процесс завершился
до performanceReport/render.json (exit0), FPS не принят; guard подтвердил
восстановление userdata. Все игровые процессы завершены.

## Статические изменения после остановки запусков

- Непроверенный static-material pilot удалён целиком; исходные shader и
  PreserveSourceCulling восстановлены. Cached WithoutSnow сохранён.
- VehicleHorsePose: owned Dictionary результатов всех central/edge rays и
  локальные RID arrays освобождаются в текущем scope. В точной текущей позе
  используется фактический transform лошади вместо identity inverse/multiply.
  Другие позы сохраняют старое преобразование. Свежие rays не пропускаются.
- HoofEndpointClear: один новый base exclusion на вызов, отдельные копии для
  каждой проверки, owned hit arrays/dictionaries освобождаются. Коллизии,
  support, dynamic floor и сами query predicates не изменены.
- HoofSegmentClear/LowerLegSegmentClear: owned initial/identified arrays,
  hit/rest dictionaries и RID exclusions освобождаются на всех выходах.
  Порядок запросов и скопированные значения контактов сохраняются.
- AddressWalkProbe: освобождаются Floor results, group-result array,
  диагностические hits и собственный exclusion array при Dispose. Actor nodes
  и заимствованный world/space не освобождаются.
- SettlementRoadGraph: вместо сортировки двух ключей — CompareOrdinal с той
  же последовательностью concatenation. Committed key берётся прямым проходом
  уже отсортированной группы. SHA/ID формат и порядок рёбер сохраняются.
- Nearest: один проход по рёбрам вместо LINQ query pipeline; сохранены порядок
  фильтров, минимальная дистанция, ordinal ID при равенстве и обработка NaN.
- BuildAddressRegistry: убрана повторная startup-пересборка после импорта
  адресов/constraints. Финальный граф уже строит ImportAddressRoads; дороги
  и улицы после него не меняются. Публикация новых verified paths сохранена.

Новые lifetime/identity изменения прошли независимый статический review;
игровой эффект и движение лошади после них **не проверены** по поручению автора.
Текущая affected C#-компиляция: PASS, 0 предупреждений/ошибок, 11.05 с;
`dotnet build game/Urman.Game.csproj --no-restore --disable-build-servers
-v q --nologo -m:1 -nodeReuse:false` через репозиторный dotnet-env. Godot при
этом не запускался; после компиляции процессов Godot нет. `git diff --check`
чистый. Новые тесты/смоуки/benchmark не выполнялись.

Первая компиляция lifetime-правок выявила 17 CS1674/CS1061: в GodotSharp
4.7.1 Array<T> не IDisposable. Исправление использует отдельный using-owner
для `(Godot.Collections.Array)typed`. Независимое чтение IL установленной DLL
подтвердило: explicit cast возвращает тот же `_underlyingArray`, без копии;
у него есть Dispose. Typed reads завершаются до освобождения owner. Словари
поддерживают Dispose напрямую. Заимствованные scene nodes/resources не тронуты.

Статический аудит оставшегося O(N²): простая сетка XZ-границ не эквивалентна
MayIntersect, который допускает раздельные ill-conditioned nonparallel пары.
Безопасный будущий индекс должен объединять spatial и near-parallel direction
кандидаты с прежним порядком i/j и exhaustive-path для extreme imports.
На срезе первого статического этапа такой индекс ещё не внедрён; поздние
rebuild остаются риском рывков. Его следующая реализация описана ниже.

## Второй статический этап — наиболее сильные кандидаты

Автор повторно поручил продолжать без игры, с максимально доступным числом
субагентов. Три субагента параллельно разобрали rendering/fog/shadows,
CPU/physics/audio и алгоритмы адресного графа; затем выполнили перекрёстные
read-only review. Игровых/импортных/headless запусков нет. Приведённые ниже
выигрыши — устранённые операции и механизмы, **не измеренный FPS**.

### Метель и реальная дальность

Текущая gameplay-камера имеет Far=160 в first_person_player.tscn; High
оставляет четыре каскада теней до 120 м. Village fog density=0.032,
TuneConnectedAct1Atmosphere меняет fog, SetOpeningBlizzard — параметры частиц.
Последний LOD деревьев не ограничен VisibilityRangeEnd; ground cover — 85 м.
По этим источникам метель уменьшает читаемость дальних объектов, но отдельного
согласованного fog-distance culling нет. Первоначальное впечатление автора
о сниженной дальности не подтверждает сокращение обработки сцены.

Fog не даёт точного нулевого вклада в дальние пиксели. Высокая крона, свет
окон, длинные directional shadows и вид наружу из интерьера не позволяют
объявить общий короткий cutoff сохранением графики. Far/тени/плотность/число
частиц в этом этапе не уменьшены. Occlusion culling с настоящими occluders
остаётся отдельным кандидатом; корректные окна/двери и CPU-цена не проверены.

### Внедрено

1. **Неподвижная непрозрачная геометрия и проход теней.** У 30 явно заданных
   семейств дерева, коры, стен, камня и металла wind_sway=0,
   has_snow_micro=false, trample_ground_surface=false неизменны. Прежний общий
   shader всё равно записывал VERTEX += 0. Новый RigidPainterlyShader удаляет
   только один общий VertexDeformation block; uniforms, helpers, четыре
   vertex-varyings и весь fragment сохранены. Оригинальный ShaderSource
   восстановлен byte-identical статическим чтением raw literals. Неизвестные,
   snow/wind/cutout семейства сохраняют оригинал; PreserveSourceCulling для
   обоих собственных shaders сохраняет прежний TwoSided вариант. WithoutSnow
   не расширяет eligibility. Godot [отмечает запись VERTEX](https://github.com/godotengine/godot/blob/4.7-stable/servers/rendering/renderer_rd/forward_clustered/scene_shader_forward_clustered.cpp)
   и [исключает её из shared shadow path](https://github.com/godotengine/godot/blob/4.7-stable/servers/rendering/renderer_rd/forward_clustered/scene_shader_forward_clustered.h).
   Это наиболее прямой кандидат против прежних 30–34 тыс. shadow draws.
   Новые draw counts/GLSL compilation/pixels не измерены. Включение уже
   существующего ShadowMesh может выявить различия точности импортированной
   сжатой геометрии; визуальная приёмка пока not-run.
2. **Кандидаты дорожного графа.** Padded AABB cells 8 м объединены с ±
   max-component-normalized direction cells 1/16 и соседями 3×3. Старый
   near-parallel numerical predicate сохранён как RetainDisjointPair;
   spatial-кандидаты добавляются первыми, direction-only пары проверяются
   до добавления/сортировки. Это не допускает сортировку всех строго
   параллельных разнесённых сегментов. Все прежние MayIntersect=true пары
   покрываются UNION; кандидаты обрабатываются в исходном порядке i/j.
   Extreme/nonfinite/nonpositive norm или >65 536 spatial memberships
   сохраняют прежний whole-build scan; diagnostic exhaustive не меняется.
   Счётчики учитывают пропущенные индексом false pairs. Dense/ill-conditioned
   наборы остаются дорогими: direction scans могут быть O(N²), а сортировка
   плотных candidate lists — до O(N² log N). Новый runtime-equivalence smoke
   не запускался; старые 84 checks не объявляются проверкой нового индекса.
3. **Механический звук.** Вместо отдельного PushFrame на каждый sample —
   один PushBuffer(_buffer.AsSpan(0,count)). При работающем двигателе это
   устраняет примерно 22 050 managed/native вызовов в секунду, заменяя их
   одним bounded batch на process tick. Sample generator/count/order,
   пауза и запуск/остановка сохранены; новый managed массив не создаётся.
   Native marshaling может копировать span, zero-allocation не заявляется.
   Local GodotSharp 4.7.1 подтверждает overload; single-producer и native
   synchronous ring-buffer write проверены по исходнику.
4. **Трансформации транспорта.** UpdateVisuals и ApplySteeringCollision
   присваивают прежний desired Transform только при exact != фактическому
   текущему Transform. У неподвижных машин прежние одинаковые записи
   инвалидировали visual tree/shape-owner transforms каждый physics tick.
   Новый cache/epsilon не введён; движение родителя распространяется штатно.
   Поза лошади, свежие ground/endpoint/sweep проверки остаются каждый тик.
5. **Привязка следов в снегу.** SetSnowTrample пропускает повторный проход
   по всем материалам только при той же ссылке Texture, точных origin/extent
   и прежнем Materials.Count. ImageTexture.Update обновляет содержимое без
   новой привязки; новый material или сдвиг окна выполняет прежний проход.
   ClearCache сбрасывает memo, выход/null отключает старые consumers. Внешних
   writers этих uniform и same-count cache replacements в коде не найдено.

Полный payload-cache измельчённых снежных треугольников отложен: требует
дополнительных массивов/лимита памяти, не устраняет mesh upload и пока не
подтверждён профилем ходьбы. Уменьшение качества снеговых следов не внедрено.
Повторные физические лучи персонажей остаются кандидатом по lifetime,
но не объявляются главным источником кадра без evidence.

Дополнительный разбор частиц: обычная метель — один CPU emitter 4000/5200
частиц, который движок отправляет через native MultiMesh, а не отдельными
scene nodes. Скрытая система уже выходит до симуляции. Два снегопада в
лесном прологе имеют разные плотность/движение; удаление любого меняет вид и
не внедрено. Повторные четыре shelter uniforms за кадр — меньший будущий
кандидат, не объявленный основной причиной. Переход на GPU particles и
сокращение количества снега не выполнялись.

Статическое перекрёстное review перечисленных изменений прошло. Общая текущая
C#-компиляция: PASS, 0 предупреждений/ошибок, 10.90 с, прежний узкий build
game/Urman.Game.csproj без restore/build servers, -m:1. git diff --check чистый;
процессов Godot после проверки нет. Это **не** GLSL/runtime/pixel/FPS проверка.
Новые тесты не созданы и не запускались. Цель M1 остаётся открытой.

## Третий статический этап — проверка по исходникам движка и устранение работы

Автор снова поручил продолжать без игры, с максимальным числом субагентов.
Пять read-only аудитов (rendering, CPU, physics/NPC/vehicles, world/algorithms,
weather/particles) прошли параллельно, затем четыре исполнителя правили
непересекающиеся файлы, затем независимые review проверили эти правки. Игровых,
импортных, headless- и тестовых запусков нет; C#-сборки выполнялись только по
одной за раз (никогда одновременно), через репозиторный toolchain, а финальная —
после остановки всех исполнителей.

### Что проверено по исходникам Godot 4.7-stable (а не по памяти)

- **Счётчик `ViewportRenderInfoType.Shadow` включает теневые проходы всех типов
  источников**, не только directional: каждый `_render_shadow_append`
  (`servers/rendering/renderer_rd/forward_clustered/render_forward_clustered.cpp:2850`)
  пишет в `info[TYPE_SHADOW]`. Поэтому 34 315 shadow draws нельзя заранее
  приписать только четырём каскадам солнца.
- **`VisibilityRange` учитывается и для теневых кастеров**, причём по расстоянию
  до камеры: `VIS_CHECK` включает `VIS_RANGE_CHECK`, вычисляемый от
  `cull_data.cam_transform.origin` (`servers/rendering/renderer_scene_cull.cpp:2924`),
  а ветка directional-каскадов проверяет `IN_FRUSTUM(...) && VIS_CHECK` (там же,
  ~3242). Значит существующая LOD-цепочка деревьев уже ограничивает их вклад в
  теневой проход; отдельного «LOD протекает в тени» дефекта нет.
- **`ArrayMesh.ShadowMesh` — это позиционный прокси с теми же треугольниками и
  той же LOD-цепочкой**, а не упрощение: `ImporterMesh::create_shadow_mesh`
  (`scene/resources/3d/importer_mesh.cpp:955-1046`) дедуплицирует вершины,
  переиндексирует те же индексы и копирует `lods` без изменения геометрии.
  Импортированные `.glb` (у которых `.import` требует
  `create_shadow_meshes=true`) его имеют; ArrayMesh, собранный в рантайме, — нет.
  Это объясняет, почему `shadowMeshRid` ненулевой у всех 140 оконных
  меш-ресурсов census, и даёт будущий кандидат (см. ниже), но **не** уменьшает
  число draw calls или примитивов.

### Внедрено в этом этапе

6. **Кэш стабильных идентификаторов адресного графа.** Каждая публикация графа
   заново выводила `SHA256` для каждого узла и ребра
   (`SettlementRegistry.StableId`, вызывается из `SettlementRoadGraph`).
   Идентификатор — чистая функция от `(prefix, sourceKey)`; добавлен
   ограниченный кэш (65 536 записей) с полной очисткой при достижении предела.
   Формат ID, порядок и потребители не изменились.
7. **Топологическая фаза `RebuildCore` без LINQ на каждый сегмент.** Прежний
   конвейер `OrderBy(T).ThenBy(Key).GroupBy(NodeId).Select(First).OrderBy(T).ToArray()`
   выполнялся для каждого из ~2 490 сегментов каждой из 94 пересборок. Заменён
   на сортировку на месте общим компаратором `(T, ordinal Key, insertion order)`
   и упорядоченную дедупликацию по `NodeId` в переиспользуемых буферах.
   Эквивалентность доказана по частям: последний `OrderBy(T)` был no-op (стабильная
   сортировка уже упорядоченной по `(T,Key)` последовательности по одному `T`);
   `GroupBy(...).First()` — первое вхождение `NodeId`; `List.Sort` нестабилен,
   поэтому полный порядок задан третьим ключом — позицией вставки. Случай «две
   реза одного сегмента с одинаковыми `T` и `Key`, но разными `NodeId`» достижим
   (коллинеарное перекрытие с концами в разных группах) и именно он требовал
   третьего ключа. Независимая проверка: отдельный одноразовый harness вне
   репозитория на 400 000 рандомизированных враждебных наборов дал побитово
   одинаковую последовательность элементов для старого и нового кода.
8. **`SettlementRoadGraph.NodeAt`** — один проход с выбором ординально
   минимального `Id` вместо `Where(...).OrderBy(Id, Ordinal).FirstOrDefault()`;
   вызывается в цикле по всем подтверждённым входам на каждую привязку.
9. **Индекс кандидатов больше не отключается целиком.** Прежний
   `if(memberships>65536){pairCells=null;break;}` выключал индекс для **всего**
   графа, если суммарный «вес» ячеек превышал бюджет, и тогда каждая пересборка
   перебирала все пары. Теперь сегмент, не помещающийся в бюджет, покидает
   индекс (`unindexed`) и сравнивается со всеми остальными в том же цикле
   кандидатов; бюджет памяти прежний (65 536). Множество кандидатов —
   надмножество прежнего, порядок обхода пар (i возрастает, j возрастает)
   сохранён, поэтому `Rebuild` и `RebuildExhaustiveForDiagnostics` по-прежнему
   дают один граф. Статическая оценка по авторским данным (110 сегментов,
   329 membership, максимум 12 ячеек на сегмент) показывает, что порог,
   вероятно, и не достигался: это страховка, а не заявленный выигрыш. В
   диагностический лог добавлено поле `unindexed_segments`; семантика
   `pair_index` изменилась — при дедемоции он остаётся `1` (индекс работает для
   остальных сегментов), а не `0`, как раньше при полном отключении.
10. **Кадр лошади в `VolumeOverlaps` считается один раз.** Для 9 fixed- и
    steering-томов `hoofIndex`/`lowerLegIndex` всегда `-1`, поэтому обе ветви
    прежнего тернарника давали ровно `excluded`, а `HorseFrameForPose(pose)`
    вызывался и выбрасывался. Худший случай `CanRotate` (повёрнутый базис, не
    совпадающий с принятой позой) давал ~18 полных `PreparePose` = ~1 200
    `IntersectRay` за physics-тик. `PreparePose` внутри тика только читает
    `_legs`/`_previous`/`_phase`/`_replants`/`_stepsStarted`/`_initialized` и мир
    (состояние пишет только `PublishPose`, из `VolumeOverlaps` не вызываемый),
    `DirectSpaceState` внутри тика неизменен, имена томов не пересекаются ⇒ один
    вызов возвращает то же, что возвращали все прежние. Порядок `IntersectShape`
    по томам не изменён. Кэширования между вызовами и тиками нет.
11. **`SettlePose` не переопрашивает ногу, уже доказанную в этом же тике.**
    Новый параметр `reuseFreshSupport` пропускает только луч `Ground` для ноги с
    `!Moving && SupportAtSole`, выполняя ровно те же записи состояния
    (`Start=End=Sole`, `EndNormal=Normal`, `Swing=0`, `Moving=false`, `Solve`) —
    поэтому публикуемое состояние побитово прежнее. `SupportAtSole` выставлен в
    `PreparePose` из луча с `wanted == leg.Sole`, т.е. тот же луч уже вернул
    попадание точно в `leg.Sole`. Флаг передаётся только из
    `SupportedHorseFrame` вместе с `freshlyPrepared`; для планов после
    `RebasePose` (другой `HorsePose.Basis` влияет на corner-проверку) остаётся
    полный `Ground`.
12. **Освобождение собственных результатов запросов и коллекций.** `using` на
    результат `IntersectShape` в `VolumeOverlaps` и `CanFitAt`, на
    `IntersectRay` в `FirstPersonController.Steps/Body` и `SnowTrampleField`, на
    `SurfaceGetArrays` там же; прямые проверки двух RID вместо
    `new[]{a,b}.Distinct()`. Заимствованные узлы, ресурсы, world/space не
    освобождаются. В GodotSharp 4.7.1 `Array<T>` не `IDisposable` — освобождается
    underlying untyped `Array` через `(Godot.Collections.Array)typed`, что уже
    принято в проекте. Проба стойки `CanFitAt` дополнительно переиспользует
    `CapsuleShape3D`, query и exclusion-массив по ключу
    `(radius, height)` вместо создания нативных объектов на каждый вызов
    (проба шага вызывается тысячи раз); они освобождаются в существующем
    `_ExitTree`. Удерживаемые статические кэши частиц получили тестовый сброс по
    уже принятому паттерну (`WinterParticleSurfaces.ClearCacheForHeadlessTests`
    + одна строка в `GodotSmokeCleanup`).
13. **Аллокации в горячем пути транспорта.** `CopyLegs` вместо
    `Select(...).ToArray()`, статический буфер долей шага, индексный проход по
    колёсам вместо `foreach` по `IReadOnlyList` и LINQ `Contains`, статический
    список действий вместо `new[]{...}`, гварды точным `!=` для `lamp.Visible`,
    `tuning.Text`, `_head.RotationDegrees`, трансформов томов и костей.
14. **`VehicleFleet._PhysicsProcess`** — `GetNodesInGroup("main_menu").OfType().Any()`
    каждый тик (нативный `Array<Node>` + LINQ) заменён быстрым путём по
    `GetFirstNodeInGroup` с полным перечислением как фолбэком (окно, когда
    `Dismiss()`-нутый узел ещё кадр в дереве, а второй уже создан), кэш
    `player_controller`/`pause_menu`/`settings_ui` с `IsInstanceValid` +
    `IsInsideTree`, запись `_hud.Visible` только при изменении; семантика
    `Suspended` не изменена.
15. **Верификатор адресов: одна полная оценка контекста на тик.**
    `Same(Context(), _context)` внутри цикла до 48 проб (каждая с
    `CapturePlayTimeGroups`) заменён на `Same(Refresh(tick), _context)`, где
    `Refresh` перечитывает только `GetTree().Paused` и `_presentationRevision`;
    `Ready` стал вычисляемым свойством с той же формулой. Внутри цикла нет
    `await`/`yield`/`CallDeferred`, единственный писатель
    `_presentationRevision` — `Act1ConnectedWorld.SetActiveLogicalZone`, поэтому
    предикат тот же; постикловый чек оставлен полным, ни один выход не ослаблен.
16. **Per-frame гигиена мира/звука.** Кэш игрока в `InteriorPresentation` и
    `YardMechanism`, переиспользуемый снимок ключей в `UpdateConversationFacing`,
    порядок предикатов в `WatchSuspensionBridge` (`FirstNightPassed` до чтения
    `SelectWorldProps`), отсутствие per-frame `Name.ToString()`/`SetMeta`/
    `SpeedScale` без изменения значения в `Act1ConnectedWorld._Process`,
    `AudioCueUi` кэшируется в `VehicleMechanicalAudio`, `int[3]` в
    `RinatFootPlacementModifier` заменён явными записями, LINQ по 127 объектам в
    `AuthoredWorldDirector._PhysicsProcess` заменён флагом, shelter-uniforms и
    `SetMeta` в `AgentBAct1ExteriorLayer.UpdateSnowPresentation` пишутся только
    при изменении, facility-тик больше не переписывает константные
    `CollisionLayer`/`Visible` и трансформ стоящей двери (`door.Target.CollisionLayer`
    и `Prompt` намеренно не мемоизируются: подсказка зависит от живого ray-слоя),
    `SetWindMotion` получил memo, `WinterParticleSurfaces.Snow` переиспользует
    `Shader`/`ShaderMaterial`/`QuadMesh` по параметрам.
17. **Два оставшихся per-frame пересчёта в presentation-путях.** Фигура водителя
    телеги (`CartDriver`) резолвится один раз при сборке визуала (а не
    `GetNodeOrNull("CartDriver")` каждый физический такт), и её `Visible` пишется
    только при изменении; строка часов старого ПК форматируется только при смене
    минуты — текст есть чистая функция от `(int)clock.TotalMinutes`, а
    `Label::set_text` (Godot 4.7-stable) и сам игнорирует одинаковую строку, так
    что изображение не меняется. Механический скан всех
    `_Process`/`_PhysicsProcess` по `game/scripts/**` на аллокации/LINQ/
    `GetNodesInGroup`: после этого этапа в кадровых путях не осталось ни одного
    LINQ-конвейера, ни одной групповой выборки без гейта и ни одной строковой
    интерполяции за кадр — всё остальное суть конструкторы value-типов
    (`Vector3`/`Basis`/`Quaternion`/`Transform3D`), а два найденных исключения
    (часы ПК и `SingleOrDefault` в `Act1VehiclePerformanceRoute`) исправлены и
    существуют только в perf-харнессе соответственно.

### Единственное найденное изменение поведения

Независимый review hygiene-блока нашёл ровно одно место, где правка меняет не
только стоимость: `RinatPresencePresentation` пересчитывает presence-предикат по
событию `RuntimeBridge.RuntimeStateChanged`, а оно поднимается через
`CallDeferred`. Все писатели читаемых полей (`npc[...rinat].alerted`,
`knowledge[...route_kara_urman_edge_hint].status`) — контентные эффекты, и каждый
dispatch-путь bridge вызывает `QueueRuntimeStateChanged`, поэтому инвалидация
корректна, но переключение `Visible`/`CollisionLayer` этой презентационной фигуры
может отстать от коммита на ≤1 кадр (прежнее чтение каждый кадр могло увидеть
коммит в том же кадре, если `_Process` Рината шёл позже мутации). Решение принято
осознанно и зафиксировано здесь: порог сюжетный, задержка в один кадр не
наблюдаема, флаг остаётся presentation-only.

### Проверки этого этапа

- Независимые review прошли по трём блокам: адресный граф + lifetime
  (`APPROVE` по всем шести пунктам, обязательных исправлений нет), транспорт
  (`APPROVE` по всем семи пунктам, обязательных исправлений нет, включая проверку
  по исходнику GodotSharp, что `Array<T>[i]` возвращает независимую нативную
  копию словаря и освобождение внешнего массива не инвалидирует элементы
  результата) и hygiene-блок (`APPROVE` по восьми пунктам; единственная находка —
  счётчик `_pendingStates` мог быть меньше числа объектов с непустым
  `PendingState` в случае откатившегося состояния; исправлено одной строкой,
  применения при этом не терялись). Независимость review частичная: они
  запускались как форки с унаследованным контекстом этого запуска, что отмечено
  в их собственных вердиктах.
- Эквивалентность переписанной топологической фазы проверена внешним
  одноразовым harness (400 000 случаев) — это анализ, а не тест репозитория.
- Узкая C#-компиляция `game/Urman.Game.csproj` (`--no-restore`,
  `--disable-build-servers`, `-m:1`) выполнялась по одной за раз; итоговая — после
  остановки всех исполнителей: PASS, 0 предупреждений/ошибок, 12.34 с. Godot при
  этом не запускался.
- `git diff --check` чистый.

### Остаточный риск четвёртого этапа (из независимого review)

У материалов, ставших rigid, `material_is_animated` (в движке — `is_animated()`,
`scene_shader_forward_clustered.cpp:248`) становится `false`. Этот флаг читается
только при обновлении теней **позиционных** источников: `_render_scene` вызывает
`shadow_atlas_update_light(...)`, и лишь затем `_light_instance_update_shadow(...)`,
возвращаемое значение которого при анимированном материале делает
`light->make_shadow_dirty()` (`renderer_scene_cull.cpp:3628-3640`, а также
`:2430-2647`). Тени directional-солнца пересобираются независимо, а перемещение
узла по-прежнему помечает свет грязным, поэтому солнце, статика и любой движущийся
узел не затронуты.

Практическая ширина этого риска ещё меньше, чем в первой оценке: флаг взводится,
если **хотя бы один** кастер в наборе источника имеет анимированный материал, а
снег/лёд/трава/растительность остались на полном шейдере (у них живые
микрорельеф, следы или качание) и лежат в радиусе любой около-земной лампы или
оконного спота. Поэтому позиционные тени в обычной сцене продолжают
перерисовываться каждый кадр, и одновременно с этим «выигрыш» от прекращения
принудительных перерисовок не материализуется: основной эффект этапа — не
частота обновления позиционных теней, а переход ставших rigid поверхностей на
дешёвый общий теневой материал в самом теневом проходе. Остаётся сценарий без
единого анимированного кастера в наборе источника (например, источник, чей набор
состоит только из ставших rigid поверхностей): там idle-анимация скелета на месте
может не обновлять силуэт тени до следующего изменения трансформа или видимости.
Видимая геометрия анимируется как обычно; TAA в проекте не включается, поэтому
motion-векторы визуально не влияют. Отдельный визуальный кадр с idle-NPC в радиусе
позиционной лампы **не снимался**; это остаётся проверкой для следующего запуска.

Также по замечанию review добавлена дешёвая защита от тихого отказа оптимизации:
статический конструктор `PainterlyMaterialLibrary` сравнивает код rigid-шейдера с
полным и печатает предупреждение, если вырезание блока перестало срабатывать
(изображение при этом не менялось бы — терялась бы только оптимизация).

### Пятый блок — избыточные запросы внутри шага пробы

`AddressWalkProbe.TryAdvance` (обычный 8-см шаг) делал на плоском грунте три
проверки стойки (`Clear`) и два свипа; третья проверка выполнялась для позы
`feet = advanced + _downObstacle.GetTravel()`. При нулевом вертикальном ходе свипа
(капсула уже стоит точно на опоре — обычный случай ровного грунта) эта поза
поразрядно равна `advanced`, для которой проверка `Clear(advanced)` уже прошла в
этом же тике, а `BodyTestMotion` между ними — только запрос и мир не меняет.
Значит третий запрос доказуемо повторял уже полученный положительный ответ.
То же в `TrySupport`: `landed = raised + _landing.GetTravel()` против уже
пройденной `Clear(raised)`. Оба места теперь пропускают только точное совпадение
поз (поразрядное сравнение компонент, не `IsEqualApprox`), поэтому на одну
ULP отличная поза по-прежнему запрашивается. Сообщения об отказах и вся
диагностика в пропускаемых ветках были недостижимы и раньше (ветка требует
провала запроса, который в этом же тике уже прошёл), поэтому результат пробы,
поддержка, маршруты и публикация `access/*` не меняются.

Это сокращает стоимость одной пробы примерно на 20 % на ровном грунте, то есть
сокращает окно, в течение которого фоновая проверка расходует свой 2-мс бюджет,
но не сам бюджет: пока проверка идёт, цена кадра остаётся прежней (см. строку про
бюджет в таблице кандидатов ниже). Компиляция PASS 0/0; игра не запускалась.

### Остаточные допущения (не проверены запуском)

- Пропуск нулевого sweep для идентичной позы копыта (`HoofSegmentClear`/
  `LowerLegSegmentClear`) опирается на то, что `CastMotion` с нулевой длиной даёт
  safe fraction 1 — это уже заложено в исходную строку
  `if (initial.Count == 0 && fractions[0] >= 1) continue;`. Дефекта review не
  нашёл, но это единственное место, где вывод опирается на поведение движка, а не
  на чистый C#-аргумент.
- Память индекса дорожного графа ограничена прежним бюджетом 65 536; при
  превышении деградация локальная, а не полное отключение индекса.
- `pair_index=1` при дедемоции и новое поле `unindexed_segments` ещё не
  наблюдались в живом логе (запусков не было).
- **Не проверено:** GLSL/шейдеры, изображение, звук, реальный FPS, поведение
  лошади в движении, визуальная приемка, а также существующие смоуки
  (`Act1FacilitiesSmokeTest`, `AddressRegistrySmokeTest`, NPC presentation) — они
  не запускались. Игра не запускалась, замеров нет.

### Кандидаты, не внедрённые в этом этапе (с риском)

| Кандидат | Почему не внедрён |
|---|---|
| Теневой проход (34 315 draws / 21.4 млн primitives на High) | Проверено по исходникам: `VisibilityRange` в тенях уже учитывается, `ShadowMesh` не уменьшает примитивы, а все найденные способы сократить число кастеров (трава/кустарник ground-cover, 13 интерьерных омни с `ShadowEnabled=true`, оконные споты) убирают видимую тень при солнце 16° и в интерьерах. Это изменение вида, а не оптимизация. |
| `ShadowMesh` для пересобранных в рантайме мешей (террейн, дороги) | Восстановление позиционного прокси доказуемо идентично (те же треугольники), **но** рендер подставляет теневой меш только при `uses_shared_shadow_material()` — для непрозрачного back-culled материала, не пишущего VERTEX/POSITION и не использующего alpha clip/discard/`world_vertex_coords` (`scene_shader_forward_clustered.h:297-300`, подстановка `sdcache->surface_shadow` — `render_forward_clustered.cpp:4223-4255`, отрисовка — там же `:363`). Крупные снеговые и растительные поверхности пишут VERTEX (микрорельеф/следы/качание) и потому недоступны по построению, а импортированные `.glb` его уже имеют. После перехода на выбор Rigid-шейдера по флагам (коммит `270064ee`) под условие попадают и пересобранные в рантайме дороги/земля (`AB_road_*`/`AB_earth*` → surface `earth`), но суммарно это ≈90 тыс. треугольников (≈1.7 % теневых примитивов при четырёх каскадах) плюс процедурная мелочь — выигрыш неизмерим в этом этапе, прокси не внедрялся. Прокси устранил бы только ширину вершинной выборки и дубли вершин, но не число примитивов, а в depth-only проходе с мелкими треугольниками, скорее всего, доминирует setup на примитив; в GodotSharp генератора нет (есть только `ArrayMesh.ShadowMesh`), поэтому прокси пришлось бы повторять по `ImporterMesh::create_shadow_mesh`. Нужен GPU A/B; не внедрено. |
| Двухсплитовый directional-режим на medium | Меняет распределение резкости дальней тени; нужен A/B. |
| Occlusion culling | В репозитории нет ни одного `OccluderInstance3D` и нет запечённых окклюдеров; включение — редакторная работа, и счётчик теневого прохода оно не уменьшает. |
| Внешний мир в render set из интерьеров (`Far=160`) | Окно из дома — среженный кадр (туман настраивается под него); урезание изменит вид. |
| Объединение геометрии внутри домов/оград | Сокращает draw calls, но меняет локальный culling; объединение лесного пояса в MultiMesh уже измерялось и было откатано (108 → 95 FPS). |
| Полный upload маски следов на каждый отпечаток (8 МиБ) | Переписывание на `GetData`/`SetData` **опровергнуто** как побитово идентичное: `Math::make_half_float` (`core/math/math_funcs.h:747-793`) усекает мантиссу и обнуляет денормали (теряя знак), а .NET `System.Half` округляет к ближайшему чётному и сохраняет денормали; на реальных диапазонах маски расхождение 59.8 %. Точным был бы только дословный порт `make_half_float`/`half_to_float` (обе функции прочитаны в `core/math/math_funcs.h:702-793`) и RGBAH-ветвей `_set_color_at_ofs`/`_get_color_at_ofs` (`core/io/image.cpp:3511-3516,3635-3640`; раскладка `ofs = y*width+x`, 4×`uint16` на пиксель) целочисленной арифметикой плюс `GetData`/`SetData`. Уточнённая оценка выигрыша: сам interop в GodotSharp стоит десятки наносекунд на вызов, то есть ~200 тыс. пар `GetPixel`+`SetPixel` на rebuild — это порядка 8 мс, тогда как остальное в спайке — управляемая математика (2–3 `Mathf.Pow` + `Sin`/`SmoothStep` на пиксель) и `RefineGround`. Зеркало убирает только interop-часть, поэтому в этом этапе не внедрялось: сначала нужно прочитать уже существующие меты `snowTrampleRedrawMs`/`snowTrampleMeshMs`/`snowTrampleUpdateMs` на следующем замере. |
| Двойное применение зоны на старте | `Main.SwitchZone` → `SetActiveLogicalZone("village_day")` → `Build()`, а `Build()` в конце сам вызывает `SetActiveLogicalZone("village_day")`, поэтому полный проход зоны выполняется дважды. **Проверка перед внедрением показала, что второй проход может быть несущим:** после внутреннего применения зоны `Build()` ещё выполняет `FinalizeStandaloneZiratFenceContacts`, `FinalizeFacilityContacts`, `FinalizePublicBuildingContacts`, `FinalizeArrivalBusStop` и `BuildZiratFamily`, а `BuildZiratFamily` сам вызывает `ApplyInteractionRouting` (`Act1ConnectedWorld.ZiratFamily.cs:69`), то есть поздняя регистрация целей — реальный паттерн. Чтобы убрать второй проход без риска, нужен аудит всех писателей `_interactionsByZone`. Разово 5–40 мс на старте; не внедрено. |
| Генерация LOD для рантайм-мешей | `ImporterMesh.GenerateLods` доступен из C# и снизил бы примитивы в обоих проходах на дистанции, но это видимое упрощение моделей, которое автор не разрешал. Не внедрено. |
| Интернирование одинаковых примитивов (`CylinderMesh`/`PrismMesh`/`SphereMesh`/… по точным параметрам) | По образцу уже принятого `BoxMesh`-кэша: одинаковые параметры ⇒ одинаковая геометрия ⇒ общий ресурс ⇒ автоматический instancing. Механический скан нашёл всего 7 мест, где примитив создаётся в цикле (по 2–4 экземпляра) плюс 22 одинаковых `SphereMesh` следов; ожидаемый выигрыш — единицы сотен draw calls из ~41 тыс. Риск: ключ обязан включать ВСЕ свойства класса (пропуск `Rings`/`Subdivide*`/`Orientation`/`CenterOffset`/`FlipFaces`/`AddUv2` дал бы другую геометрию). Не внедрено. |
| Тени ground-cover (`MultiMesh`, 1 864 узла / 5 877 инстансов) | Трава/кустарник/ферны при солнце 16° дают тени длиной до ~0.8 м на плоском снегу рядом с игроком; отключение — видимое изменение, а не оптимизация. Не внедрено. |
| Бюджет фоновой проверки адресов (2 мс на physics-тик) | `AddressAccessVerifier` расходует до 2 мс каждого физического такта, пока не опустеет очередь (в замерах очередь была активна всю 42-секундную сессию: pending=85). При 26 FPS физика делает ~2.3 такта на кадр, то есть бюджет стоит ~4.6 мс кадра. Уменьшение бюджета вдвое уменьшило бы это до ~2.3 мс, но удвоило бы окно проверки, а `access/*`-дороги публикуются по мере её продвижения и участвуют в travel gate — это решение автора о поведении, не оптимизация. Что внедрено вместо этого: устранены только доказуемо избыточные запросы внутри одной пробы (см. ниже). |
| `RuntimeBridge.CapturePlayTimeBlocks` каждый кадр | Кэширование узлов меню/игрока в центральном bridge рискует контрактом паузы; не внедрялось. |
| `ApplyAtmosphere` пишет ~20 свойств env/sky/sun, которые в том же синхронном переключении зоны перезаписывает `TuneConnectedAct1Atmosphere` | Мёртвая работа порядка 20 записей на смену зоны; на кадр не влияет. Записано как кандидат, значение тумана 0.032 авторитетно именно потому, что `Tune` идёт вторым. |


## Четвёртый статический этап — расширение непишущего VERTEX пути

Этап начат после того, как разбор исходников Godot показал, **от чего именно**
зависит дешёвый теневой путь. `SceneShaderForwardClustered::ShaderData::
uses_shared_shadow_material` (`scene_shader_forward_clustered.h:297-300`) требует
непрозрачный back-culled материал, **не пишущий VERTEX**, без alpha clip/discard и
без `world_vertex_coords` (cull-режим берётся из `render_mode` —
`scene_shader_forward_clustered.cpp:115,197`). Только на этом пути движок берёт
позиционный `ArrayMesh.ShadowMesh` (`render_forward_clustered.cpp:4223-4255`,
отрисовка там же `:363`). Полный painterly-шейдер пишет VERTEX всегда (блок
деформации), поэтому он никогда не попадал на этот путь. Отдельный флаг
`is_animated()` (`scene_shader_forward_clustered.cpp:248`) — это другое: он
управляет тем, помечается ли тень **позиционного** источника грязной каждый кадр
(`renderer_scene_cull.cpp:2430-2647` → `make_shadow_dirty()`), и он тоже
становится false у новых rigid-материалов (см. остаточные риски).

Прежний выбор `RigidPainterlyShader` (шейдер без блока деформации) был ограничен
списком из 30 явно названных семейств. Теперь вариант выбирается по флагам,
которые и делают блок доказуемо инертным:

- `trample_ground_surface == false` ⇒ `snow_trample_at` возвращает `vec4(0.0)` по
  собственному guard'у шейдера, значит `pressed_height == 0`;
- `has_snow_micro == false` ⇒ мертва ветка микрорельефа;
- `wind_sway == 0` ⇒ мертва ветка ветра (`wind_enabled && wind_sway > 0.0`).

При этих трёх условиях блок не меняет ни `VERTEX`, ни `world_position`
(`world_normal` он не трогает вообще), поэтому удаление блока не может изменить
видимый результат — тот же аргумент, что был принят для 30 семейств, но
применённый ко всем материалам, где флаги это позволяют.

Неизменяемость флагов проверена по всему репозиторию: `wind_sway` и
`trample_ground_surface` пишутся только в самой фабрике `ForColor`, а
`has_snow_micro` там же ставится в `true` только для snow-поверхностей и позже
только **опускается** в `false` (`WithoutSnow`). Ни один код не поднимает эти
флаги на уже созданном материале, поэтому материал, инертный при создании,
остаётся инертным.

Что осталось исключённым: cutout (альфа-сканирование) и two-sided/sheer варианты
используют другие шейдеры и никогда не совпадают с `PainterlyShader` в момент
проверки, поэтому их прозрачность и culling не затрагиваются; snow-поверхности
сохраняют исходный вариант (их микрорельеф и следы живые, а headless-прогон без
загрузки микротекстуры не должен менять выбранный вариант); семейства с
`wind_sway > 0` (`foliage`, `leaf_birch`, `grass_tuft`) остаются на полном
шейдере.

Вырезание блока остаётся побайтово точным по построению: `ShaderSource`
собирается как `"""…""" + "\n" + VertexDeformation + "\n" + """…"""`, поэтому
`ShaderSource.Replace(VertexDeformation, string.Empty, StringComparison.Ordinal)`
удаляет ровно эту вставку (на её месте остаётся пустая строка GLSL, что нейтрально).

Дополнительно под `RigidPainterlyShader` попадают, среди прочих, `earth`, `metal`,
`roof`, `fabric`/`cloth*`, `grass`, `bark_birch`, `wet_ground`, `wet_road`,
`wood_cut`, `wood_carved`, `painted`, `rowan_berries`, `hay_bundle` — то есть
заметная доля непрозрачной геометрии деревни, которая раньше шла в теневой проход
собственным шейдером. Ожидаемый эффект: меньше вариантов шейдера в теневом проходе
и переход на общий shadow material (а для импортированной геометрии — на
позиционный `ShadowMesh`). **Эффект не измерен**: ни draw counts, ни время кадра,
ни изображение не снимались, игра не запускалась.

Что этот этап **не** делает: не меняет террейн/дороги/земляные банки (их материал
содержит живой микрорельеф или следы), не трогает два снегопада, дальность теней,
каскады, `Far`, туман, MSAA, разрешение и число объектов.
