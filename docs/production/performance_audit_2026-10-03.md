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

### Что именно должен различить следующий замер

Достаточная статистика уже собирается (`summary.shadowDraws`, `shadowPrimitives`,
`visibleDraws`, `cpuMs`, `processMs`, `physicsMs`, `snowTrample*Ms`, плюс новые
`sharedShadowPathSurfaces`/`otherShaderMaterialSurfaces`), поэтому не нужен новый
инструментарий — нужны **однофакторные** прогоны на одной и той же неподвижной
камере и одном и том же маршруте:

1. High как есть — базовая точка.
2. High с `DirectionalShadowMode = Parallel2Splits` (только теневые каскады):
   отделяет стоимость теневого прохода от остального кадра.
3. High с `Msaa3D = Disabled` (только MSAA): отделяет стоимость 4× сглаживания.
4. High с `SsaoEnabled = false`: отделяет полноэкранный SSAO.
5. High с `DirectionalShadowMaxDistance = 80`: показывает, сколько именно даёт
   текущая дистанция 120 м (и сколько потеряла бы дальняя тень).

Разница 2–5 — это диагностика, а не предложение что-то выключить: она говорит,
какой из авторских параметров держит кадр, и нужна, чтобы вообще можно было
обсуждать цель 50–60 FPS на High. Пока zero GPU timer не заменён разделением
CPU/GPU, любые выводы о «главной причине» кадра остаются недоказанными.

Windows-прогон полезен для сравнения, но не заменяет приёмку Apple M1/Metal.

### Однофакторные A/B для следующего замера (только режим probe)

Преcеты связывают несколько изменений сразу, поэтому сравнение High и Low не
отвечает на вопрос, что именно ограничивает кадр. Добавлены три
**необязательных** параметра окружения, которые действуют только когда в
командной строке уже есть `--urman-perf-probe` (то есть в существующем
performance-пробнике) и никогда не меняют сам пресет; обычный запуск их не
видит, а некорректное значение приводит к явной ошибке старта пробы, а не к
молчаливому игнорированию:

- `URMAN_PERF_SCALE=0.70` — тот же High, но меньший render scale: если кадр
  ускоряется пропорционально числу пикселей, ограничение в заполнении/пикселях
  (GPU fill/MSAA); если почти не меняется — в геометрии, draw calls или CPU.
- `URMAN_PERF_SHADOW_SPLITS=2` (или `=4`) — только число каскадов солнца при том
  же атласе, фильтре, дистанции, scale, MSAA и содержимом сцены: прямой замер
  вклада directional-теневого прохода.
- `URMAN_PERF_SHADOW_DISTANCE=45` — только дальность теней: замер вклада
  дальних каскадов, то есть объёма геометрии, попадающей в теневой проход.

Каждое значение попадает в receipt строкой `graphics_overrides=...` рядом с
`preset`/`scale`/`msaa`, поэтому доказательство замера сразу показывает, что
именно отличалось. Рекомендуемая последовательность: baseline High, затем
каждый параметр по отдельности, затем (при необходимости) их комбинации; все
прогоны — через существующий `eng/protected_run.py`, с фиксацией source/dirty
путей и одинаковыми прогревом, длительностью, разрешением, фокусом и отсутствием
modal/overlay. Ни один из этих параметров не уменьшает качество сам по себе и не
входит в обычную игру.



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
| Выбор rigid-шейдера на Low для snow-материалов без следов | При `low_quality` ветка микрорельефа мертва (её условие включает `!low_quality`), поэтому `snow_roof`/`ice` без trample могли бы тоже уйти на rigid. Но выбор варианта происходит при создании материала, то есть до применения пресета, и такая правка потребовала бы перерешать шейдер при каждом переключении пресета и восстанавливать полный при High/medium. Выигрыш только на Low и небольшой по площади — не внедрено. |
| `RuntimeBridge.CapturePlayTimeBlocks` каждый кадр | **Закрыто анализом.** Вызов покадровый (`RuntimeBridge._Process`), но работа — три `GetFirstNodeInGroup` (игрок, `pause_menu`, `settings_ui`), две навигации по родителям и `DisplayServer.WindowIsFocused()`; это микросекунды на кадр, а не миллисекунды, и `HasActiveMainMenu` теперь не выделяет массив в установившемся режиме (см. независимую проверку правок второго исполнителя). Кэширование рискует контрактом паузы и не даёт измеримого выигрыша. |
| `ApplyAtmosphere` пишет ~20 свойств env/sky/sun, которые в том же синхронном переключении зоны перезаписывает `TuneConnectedAct1Atmosphere` | Мёртвая работа порядка 20 записей на смену зоны; на кадр не влияет. Записано как кандидат, значение тумана 0.032 авторитетно именно потому, что `Tune` идёт вторым. |


## Что ещё стоит GPU-времени на High (по коду, без замера)

- **SSAO включён**: `GlowEnabled = false`, но `SsaoEnabled = true` с авторскими
  `intensity = 0.75` и `radius = 0.4` (`Act1ConnectedWorld.cs:1776-1784`,
  `content/world/atmosphere.v1.json`), а `GraphicsQuality.ConfigureEnvironment`
  выключает его только на Low. Это полноэкранный эффект на 1080p при MSAA 4× и
  часть разницы High/Low, но это авторская составляющая вида.
- **Ни SDFGI, ни SSR, ни volumetric fog в проекте не включены** (нет упоминаний в
  коде и данных), glow выключен — то есть список дорогих full-screen эффектов
  ограничен SSAO и depth-fog с aerial perspective 0.55.
- **MSAA 4× и render scale 1.0 на High** — авторские значения пресета;
  `ScreenSpaceAA` на High выключен, TAA нигде не включается.
- **Тени солнца при `shadowOpacity = 0.18`** смягчены до 18 % затемнения, при этом
  теневой проход занимает около 40 % кадра (34 315 из ~41 400 draw calls). Это не
  дефект, а следствие низкого солнца и дистанции 120 м; но именно поэтому снижение
  `DirectionalShadowMaxDistance`/числа каскадов или отключение теней у целых
  классов геометрии дало бы наибольший эффект — и именно поэтому это решение
  автора, а не оптимизация.

## Почему теневой проход нельзя сократить без решения автора

Измеренное отношение shadow/visible ≈ 4.85 объясняется геометрией, а не утечкой
LOD. При высоте солнца 16° (`content/world/atmosphere.v1.json`, `sun.rotation =
[-16, 18]`) ортогональный бокс каждого каскада должен накрывать срез фрустума
камеры, спроецированный вдоль направления света: его протяжённость «вверх» растёт
примерно как `глубина_среза / tan(16°) ≈ 3.5 × глубина`, а ширина — как ширина
среза на дальней границе. Для четвёртого каскада (60–120 м при
`DirectionalShadowMaxDistance = 120`) это бокс порядка 184 × 210 × 60 м, то есть
большой кусок деревни: отсюда ~8 600 различных ключей (mesh, material, mirror) на
каскад и ~34 тыс. теневых draw calls при 7 055 видимых. Каскады вложены, поэтому
близкие объекты честно попадают во все четыре, а `VisibilityRange` в тенях
учитывается по расстоянию до камеры (`renderer_scene_cull.cpp:2924`), то есть LOD
деревьев уже работает.

Следствие: сократить теневой проход можно только одним из решений автора —
уменьшить `DirectionalShadowMaxDistance`/число каскадов (качество дальней тени),
поднять солнце (художественный стиль), убрать тени у целых классов геометрии
(видимые тени травы/кустарника/интерьерных ламп) или разрешить LOD-упрощение
рантайм-мешей. Ни один из этих путей не является визуально-нейтральной
оптимизацией, поэтому в этом этапе они не внедрялись.

Чтобы следующий замер мог проверить эффект четвёртого этапа, census
(`URMAN_PERF_RENDER_DIAGNOSTICS=1`) теперь дополнительно пишет
`sharedShadowPathSurfaces` и `otherShaderMaterialSurfaces` — число поверхностей
всего connected world, которые попадают на общий теневой материал, и число
остальных поверхностей на ShaderMaterial. Значение считается по тождеству шейдера
через новый диагностический предикат
`PainterlyMaterialLibrary.UsesSharedShadowMaterial`; он читает только идентичность
шейдера и не влияет на то, какой материал использует меш.

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

## Разбор ключей отрисовки по владельцам (из census живых прогонов)

Census установившегося High-прогона даёт готовое объяснение, **где остались
различные ключи** (mesh, material, mirror), то есть что именно определяет число
draw calls. Всего в кадре видимости 21 402 поверхности → 3 850 различных ключей,
**среднее переиспользование 5.56**:

| Владелец | поверхностей | ключей | переиспользование |
|---|---:|---:|---:|
| AgentBExteriorWorld (лес/земля, MultiMesh) | 14 237 | 259 | **54.97** |
| AuthoredWorldDirector | 3 238 | 985 | 3.29 |
| Act1AuthoredExteriorKitPresentation | 876 | 870 | **1.01** |
| VillageForestGorge | 649 | 349 | 1.86 |
| FapExterior | 311 | 289 | 1.08 |
| ZiratMemoryField | 231 | 185 | 1.25 |
| SettlementAddressPresentation (таблички) | 120 | 120 | 1.00 |
| VillageVehicles | 100 | 100 | 1.00 |
| остальные (≤100 поверхностей каждый) | ~640 | ~600 | ~1.1 |

Что из этого следует статически:

- Крупные владельцы уже оптимизированы: внешний мир (лес, земля, снег) идёт через
  MultiMesh/инстансы и даёт 259 ключей на 14 237 поверхностей, `AuthoredWorldDirector`
  — 985 ключей на 3 238. Именно поэтому автоинстансинг уже реализован и трогать его
  нечего.
- **Крупнейший оставшийся источник — `Act1AuthoredExteriorKitPresentation` (870
  ключей на 876 поверхностей)**, и он статически оказался **импортированной сценой**
  (`BuildAct1AuthoredExteriorKit` вешает `assetSource = VillageExteriorKitScenePath`,
  `presentationOnly/visualOnly`, коллизий и навигации у него нет). Значит общий
  ресурс меша там не появляется: сцена приходит из ассета, и каждый её узел несёт
  свою пару (mesh, material), а часть — зеркальный базис (`mirror` тоже входит в
  ключ). Ни интернирование примитивов, ни шаринг материалов из игрового кода этого
  не исправят; исправить может только слияние геометрии в рантайме по
  (фасад, материал), что меняет локальный culling и уже один раз измерялось
  отрицательно на другом контенте (лесная полоса: 108 → 95 FPS). Поэтому это
  кандидат, требующий замера, а не правка «на глаз».
- Владельцы с переиспользованием 1.00 в основном **содержательно уникальны**:
  `SettlementAddressPresentation` — это 85 табличек с разными номерами (у каждой
  своя текстура, то есть своя материал), `ZiratMemoryField` — памятные таблички,
  `VillageVehicles` — 4 собранных транспорта. Их ключи нельзя схлопнуть, не меняя
  картинку.
- Отсюда вывод для дальнейших шагов: интернирование процедурных примитивов
  (`new BoxMesh/SphereMesh/CylinderMesh` на каждый экземпляр) имеет смысл только
  там, где параметры повторяются, и его величина не определена без раздельного
  счётчика «ключ отличается мешем / материалом / зеркалом». Такой раздельный
  счётчик — логичное расширение census на следующий измерительный прогон
  (сейчас запуски приостановлены), потому что без него нельзя отличить
  оптимизируемый случай от содержательно уникального.

## Статический этап после замеров: группы меню и скан per-tick колбэков

Игровые запуски приостановлены (`AGENTS.md`, поручение автора 03.10.2026), поэтому
дальше — только статический разбор, правки кода и узкая компиляция; замеры этого
раздела не перепроверялись запуском.

**Скан per-tick колбэков.** Все `_Process`/`_PhysicsProcess` тела проекта
просканированы на пять классов: аллокации коллекций, обходы дерева
(`GetChildren`/`FindChildren`/`GetNodesInGroup`/`FindDescendants`), физические
запросы без освобождения результата (`IntersectShape`/`IntersectRay`/
`BodyTestMotion`), LINQ и создание физических тел. Результат: **ни одной
аллокации, ни одного неосвобождённого запроса, ни одного LINQ** — кроме одного
места, которое уже оптимизировано параллельным исполнителем
(`VehicleFleet._PhysicsProcess`: `GetFirstNodeInGroup` в общем случае, полный
перебор только как fallback). Это подтверждает, что per-tick C#-работа закрыта:
оставшиеся ~11 мс physics — это серверный шаг движка, цепочка лошади (свежие
проверки опоры обязательны) и 2-мс бюджет проверки адресов.

**Что нашлось и исправлено.** Предикаты модальности по-прежнему вызывали
`GetTree().GetNodesInGroup("main_menu").OfType<MainMenuUi>().Any(...)` — нативный
массив `Array<Node>` плюс LINQ-итератор на каждый вызов, **без освобождения
массива**. Часть из них опрашивается покадрово: подсказка взаимодействия
(`InteractionTarget.IsPresentationCurrent`), предикат модальности бани
(`BathIgnitionChoiceUi.OtherModalOpen`), `RuntimeBridge.HasActiveMainMenu`
(в том числе из `CapturePlayTimeBlocks`), плюс fallback в `VehicleFleet`.
Неосвобождённые обёртки — ровно то, чем был занят поток финализатора в профиле
(`Array.Finalize` → `godot_array_destroy`).

Введён один общий предикат `MainMenuUi.AnyUndismissed(SceneTree)`:
- общий случай решает `GetFirstNodeInGroup` — **без аллокации массива вообще**
  (первый узел группы и первый элемент массива берутся из одного и того же списка
  группы, поэтому ответ совпадает);
- полный перебор остаётся только для кадра, когда первый узел не является живым
  неотменённым меню, и в нём нативный массив освобождается (в 4.7.1 `Array<T>` не
  `IDisposable`, поэтому освобождается владеющая нетипизированная обёртка —
  приём, уже применяемый в проекте);
- добавлены проверки `IsInstanceValid`/`IsQueuedForDeletion`, которые в двух
  вызывающих местах отсутствовали; различие достигается только на уже
  освобождённом меню, где прежняя форма падала бы при чтении состояния.
Вызывающие места (`InteractionTarget`, `BathIgnitionChoiceUi`, `RuntimeBridge`,
`VehicleFleet`) переведены на этот предикат; комментарий параллельного
исполнителя в `VehicleFleet` о причине shortcut сохраняет силу, поэтому он остался,
а его fallback теперь просто вызывает общий предикат.

**Честная оценка величины:** это гигиена, а не рычаг FPS. Один вызов — единицы
микросекунд, и он не входит в измеренные 16.7 мс рендера или 10.9 мс physics;
эффект — меньше нативных аллокаций на кадр и меньше нагрузки на финализатор.
Никакого влияния на решения, видимость или поведение предикатов нет.

**Рефакторинг графа в рабочем дереве (чужой in-flight, проверен и сохранён).**
Измеренные выше 9.6 мс медианы пересборки получены на **закоммиченном** коде. В
рабочем дереве лежал незакоммиченный рефакторинг того же файла от параллельного
исполнителя, который снимает часть этой цены: упакованные массивы `bounds`/
`deltaX`/`deltaZ` вместо чтения 40-байтовой структуры из двух разбросанных объектов
на каждую пару, длинные ключи ячеек `CellKey(x,z)=((long)x<<32)|(uint)z` вместо
`ValueTuple<int,int>` в трёх словарях, и группировка без LINQ — заранее выделенный
`List<(Cut,int)>` с полным компаратором `(Key, позиция в SelectMany)`.

Проверено чтением (не запуском): `MayIntersect`/`RetainDisjointPair` получают те же
величины в том же порядке (`ax=DeltaX_i`, `az=DeltaZ_i`, `bx=DeltaX_j`,
`bz=DeltaZ_j`, `productA=ax*bz`, `productB=az*bx` — прежние `a.DeltaX*b.DeltaZ` и
`a.DeltaZ*b.DeltaX`); `CellKey` инъективен (старшие 32 бита — `x`, младшие —
`(uint)z`), и все вставки/поиски в трёх словарях переведены на него; компаратор
`(Key, Order)` — полный порядок, поэтому результат совпадает со стабильной
`OrderBy(Key, Ordinal)`; `useBroadphase && segments.All(s=>s.Bounds.Cullable)`
заменён на эквивалентный цикл с тем же коротким замыканием. Изменение
закоммичено отдельно (авторство в сообщении коммита), потому что незакоммиченная
работа в общем checkout может быть потеряна; сборка с ним проходит 0/0.
Ожидаемый эффект — меньше 9.6 мс на пересборку, но **не измерен**: запуски
приостановлены.

**Оговорка к воспроизводимости замеров.** Checkout общий: в момент компиляции
измеренных прогонов в рабочем дереве были и незакоммиченные изменения других
исполнителей. Поэтому привязка идёт к хешу загруженной сборки: первые пять
прогонов — `Urman.Game.dll` `ff789a67…`, прогоны ручек и установившихся режимов —
`488e2171…` (различие между ними — только probe-only код гейта ручек, на игровое
поведение не влияющий), контрольный Release-прогон — Release-сборка того же кода.

## Первый живой замер после статического этапа (M4 Pro, High, 1080p)

Автор разрешил игровые запуски. Все прогоны — через репозиторный
`eng/protected_run.py` (копия userdata, восстановление с проверкой fingerprint),
по одному, командой `--path game --resolution 1920x1080 --windowed
--always-on-top --audio-driver Dummy --urman-perf-probe-mode=gameplay
--urman-perf-sample=village_day@arrival --urman-perf-request-focus
--urman-perf-no-vsync --urman-perf-warmup-seconds=12
--urman-perf-duration-seconds=15`, High, scale 1.0, MSAA 4×, чувствительность
мыши 0, 100 % фокуса, 0 obscured в каждом прогоне. **Хост — M4 Pro, а не M1:**
это не приёмка целевого устройства, а измерение эффекта правок и стоимости
подсистем на сопоставимом железе. Сборка — текущий checkout, `Urman.Game.dll`
Debug (`sha256` начала `ff789a67`), Godot из `.tools/godot/Godot_mono.app`.

| Прогон | FPS | avg | p95 | p99 | max | кадры >33.3 мс | visible draws | shadow draws | shadow primitives | renderer CPU | process | physics |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| High, базовая (4 каскада, 120 м) | **26.23** | 38.13 | 59.50 | 99.87 | 115.6 | **96.97 %** | 6 618.7 | 30 078.7 | 21 433 544 | 16.16 | 31.19 | 13.14 |
| High + `URMAN_PERF_SHADOW_DISTANCE=45` | **40.47** | 24.71 | 37.97 | 63.57 | 90.4 | **8.06 %** | 6 618.8 | 14 284.0 | 14 684 197 | 8.42 | 21.70 | 14.28 |
| High + `URMAN_PERF_SHADOW_SPLITS=2` | **33.95** | 29.46 | 41.49 | 68.61 | 80.3 | 10.57 % | 6 618.7 | 16 148.1 | 16 826 818 | 12.28 | 24.49 | 13.72 |
| High + `URMAN_PERF_SCALE=0.70` | **25.93** | 38.56 | 57.24 | 89.81 | 107.3 | 97.18 % | 6 618.7 | 30 084.1 | 21 433 544 | 16.22 | 32.32 | 14.33 |
| High, legacy-выбор шейдера (A/B) | 25.71 | 38.90 | 58.33 | 87.51 | 113.2 | 99.49 % | 6 618.7 | 30 083.9 | 21 433 544 | 16.59 | 32.08 | 14.45 |

Что из этого следует (все выводы — из этих прогонов, не из оценок):

1. **Доминирует объём directional-теней, а не пиксели.** Уменьшение дальности
   теней до 45 м при том же числе каскадов, том же атласе, фильтре, scale, MSAA и
   содержимом сцены даёт **40.47 FPS вместо 26.23 (+54 %)** и убирает почти все
   длинные кадры (96.97 % → 8.06 %); shadow draws 30 079 → 14 284, shadow
   primitives 21.43 → 14.68 млн, renderer CPU 16.16 → 8.42 мс. Переход на два
   каскада при 120 м даёт **33.95 FPS (+29 %)** и 10.57 % длинных кадров,
   shadow draws 30 079 → 16 148. То есть и дальность, и число каскадов — это
   **решения автора о качестве**, и именно они, а не CPU-правки, определяют кадр
   на High.
2. **Кадр не ограничен заполнением.** `URMAN_PERF_SCALE=0.70` (≈половина
   пикселей) не меняет ни FPS (25.93 против 26.23, в пределах разброса), ни
   renderer CPU (16.22 против 16.16), ни долю длинных кадров (97.18 % против
   96.97 %). Разрешение и MSAA не являются рычагом на этой сцене.
3. **Кадр складывается из physics + renderer, оба на главном потоке:**
   avg ≈ physics + renderer CPU + ~3–8 мс (38.13 ≈ 13.14 + 16.16 + 8.8;
   24.71 ≈ 14.28 + 8.42 + 2.0; 29.46 ≈ 13.72 + 12.28 + 3.5). physics 13–14 мс
   практически **не меняется** ни от теней, ни от scale, то есть это отдельная
   константа кадра, а не следствие рендера.
4. **Фоновая проверка адресов работает во время всего окна замера** (`pending`
   29–36, `processing=true`, `completed=false` в census каждого прогона): она
   расходует свой 2-мс бюджет каждого физического такта, а тактов на кадр
   ~1.5–2.3. Это заметная часть тех 13–14 мс physics и, в отличие от теней, не
   является ни игровым поведением, ни картинкой — но уменьшение бюджета меняет
   момент публикации `access/*`-дорог (travel gate), поэтому это тоже решение
   автора.
5. **Правка выбора rigid-шейдера измерена и практически нейтральна:** в том же
   билде режим `legacy` (прежний список 30 семейств) даёт 25.71 FPS против 26.23
   при флаговом правиле, при **побитово одинаковых** visible draws и shadow
   primitives, а census показывает 58 573 против 58 568 поверхностей на общем
   теневом пути из 95 279, то есть правило по флагам добавляет ~5 поверхностей
   (0.005 %). Вывод: расширение выбора было корректным, но на текущем контенте
   оно ничего не меняет — почти вся геометрия и раньше попадала на дешёвый путь
   через уже перечисленные семейства. Временный переключатель этого A/B удалён
   после замера; флаговое правило оставлено как более короткое и доказуемо
   эквивалентное.
6. Документированный ранее baseline 23.83 FPS измерялся на более старом билде;
   текущий baseline 26.23 FPS получен на сегодняшнем checkout, и разница между
   ними **не приписывается** ни одной конкретной правке: между замерами менялись
   и контент, и код нескольких исполнителей.

### Профиль пересборок адресного графа в живом прогоне

Тот же прогон с `URMAN_ADDRESS_GRAPH_PERF=1` (прогрев 12 с) записал 30 пересборок
за ~27 с работы. Это и есть источник рывков первой минуты: **медиана 9.6 мс,
среднее 9.56 мс, максимум 18.42 мс, суммарно 287 мс** на 30 публикаций, то есть
примерно один спайк 10 мс в секунду поверх кадра 31 мс.

| Сегментов | теоретических пар | протестировано индексом | принято | `intersection_us` | нс на пару |
|---:|---:|---:|---:|---:|---:|
| 139 | 9 591 | 170 | 152 | 1.84 мс | JIT-прогрев |
| 682 | 232 221 | 16 913 | 698 | 3.35 мс | 198 |
| 1 184 | 700 336 | 22 328 | 1 200 | 6.60 мс | 296 |

Что это значит:

- Кандидат-индекс работает как задумано: при 1 184 сегментах он отсекает 96.8 %
  пар (700 336 → 22 328 протестированных) и **`unindexed_segments=0`,
  `pair_index=1` во всех 30 пересборках**, то есть пер-сегментная дедемоция
  (страховка от превышения бюджета ячеек) на этом контенте не срабатывает. Пятая
  часть принятых пар (1 200 из 22 328) подтверждает, что direction-фильтр не
  режет лишнего.
- Оставшиеся ~296 нс на пару — это почти наверняка промахи кэша на списках
  `Segment`/`Cuts` (объекты и `List<Cut>` разбросаны по куче), а не арифметика:
  сам `MayIntersect` + `AddIntersections` — это сравнение границ и несколько
  float-операций. Дата-ориентированная переписка узкой фазы убрала бы это, но это
  глубокая переделка идентичности графа, поэтому не внедрялась.
- Фаза группировки (`group_us`) — ~3 мс при 1 184 сегментах, то есть около 30 %
  времени пересборки; она по-прежнему строит LINQ-конвейер
  `segments.SelectMany(s => s.Cuts).OrderBy(Key)` со стабильной сортировкой по
  строковым ключам. Замена на заранее выделенный список с полным компаратором
  (ключ, затем порядок вставки) доказуемо эквивалентна — тем же приёмом, который
  уже проверен на топологической фазе, — но её вклад ~1–2 мс на пересборку, и она
  остаётся кандидатом, а не внедрённой правкой.
- Аллокации упали против задокументированных ранее 7.4–11.6 МБ на пересборку:
  теперь 0.5 МБ при 139 сегментах, 2.9 МБ при 996 и 3.4 МБ при 1 184 (суммарно
  71 МБ за 30 пересборок). Это следствие уже внедрённых кэша `StableId` и
  топологии без LINQ.
- Отдельные выбросы (13.5 мс в `segment_us`, 13.6 мс в `topology_us`, 6.5 и 6.0 мс)
  не соответствуют объёму работы на этих размерах и похожи на JIT/GC-паузы
  первых вызовов, а не на стоимость фазы.

### Второй замер: тот же вид после того, как фоновая проверка адресов завершилась

Тот же билд, вид и настройки, но прогрев 60 с вместо 12 с, поэтому очередь
проверки адресов успевает опустеть (census: `pending=0 current=none job=False
processing=False completed=true`), и замер идёт уже в установившемся режиме:

| Прогон | FPS | avg | p95 | p99 | кадры >33.3 мс | visible draws | shadow draws | renderer CPU | process | physics |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| High, прогрев 12 с (проверка активна) | 26.23 | 38.13 | 59.50 | 99.87 | 96.97 % | 6 618.7 | 30 078.7 | 16.16 | 31.19 | 13.14 |
| High, прогрев 60 с (проверка завершена) | **31.93** | 31.32 | **33.54** | **45.64** | **5.43 %** | 6 618.7 | 30 086.0 | 16.71 | 33.50 | **10.91** |

Отсюда следует то, чего не было видно в статических оценках:

- Фоновая проверка адресов — не «мелкий фон», а **дестабилизатор темпа кадров на
  первой минуте сессии**: при ней 96.97 % кадров длиннее 33.3 мс, без неё —
  5.43 %; средний кадр 38.13 → 31.32 мс (FPS 26.23 → 31.93, +22 %), p99
  99.87 → 45.64 мс. Прямая цена в physics — около 2.2 мс на кадр (13.14 → 10.91),
  но хвост она портит сильнее среднего: её бюджет на такт запускает догоняющие
  физические такты (на кадр их ~1.5–2.3), то есть бюджет работает как обратная
  связь.
- **Все замеры, сделанные с 12-секундным прогревом (включая прежние baseline
  23.83/24.02 FPS в этом отчёте), измеряли первую минуту, а не установившийся
  режим.** Для сопоставимости дальше нужен прогрев не короче времени проверки
  (≈60 с на этой сцене) либо явно помеченный «startup» прогон.
- Установившийся кадр High = 31.32 мс ≈ renderer 16.71 + physics 10.91 + ~3.7,
  и доминирует теперь **renderer**, то есть теневой проход: 30 086 shadow draws
  при 4 каскадах и 120 м. Это подтверждает вывод из первых пяти прогонов.
- Пересчёт с учётом завершённой проверки: `URMAN_PERF_SHADOW_DISTANCE=45`
  (24.71 мс при активной проверке) даёт ≈22.5 мс ≈ **44 FPS** в установившемся
  режиме, `URMAN_PERF_SHADOW_SPLITS=2` — ≈27 мс ≈ 37 FPS. Даже самый сильный
  однофакторный рычаг остаётся ниже целевых 50–60 FPS.

### Полная таблица установившихся режимов и потолок теневых настроек

Все прогоны ниже — один и тот же билд, вид `village_day@arrival`, 1920×1080,
High, scale 1.0, MSAA 4×, без VSync, 100 % фокуса, замер 15 с, **прогрев 60 с**
(очередь проверки адресов пуста, `completed=true`). Отличается ровно один
параметр — ручки теней; разрешение, MSAA, scale, геометрия, текстуры и материалы
не меняются.

| Прогон | FPS | avg | p95 | p99 | кадры >33.3 мс | shadow draws | renderer CPU | physics |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 4 каскада, 120 м (текущий High) | 31.93 | 31.32 | 33.54 | 45.64 | 5.43 % | 30 086 | 16.71 | 10.91 |
| 2 каскада, 120 м | 40.81 | 24.50 | 26.83 | 36.18 | 2.12 % | 16 132 | 12.59 | 10.09 |
| 4 каскада, 45 м | 48.68 | 20.54 | 22.52 | 32.67 | 0.96 % | 14 284 | 8.58 | 11.72 |
| 2 каскада, 45 м | **57.09** | 17.51 | 19.06 | 28.95 | **0.35 %** | 9 698 | 7.24 | 11.35 |

Выводы, которые из этого следуют напрямую:

- **Целевая полоса 50–60 FPS достигается на этом хосте только комбинацией двух
  теневых настроек** (2 каскада + 45 м): 57.09 FPS, p95 19.1 мс, длинных кадров
  0.35 %. Ни одна из них по отдельности цели не даёт (40.81 и 48.68), а
  разрешение, MSAA, scale, плотность леса, размеры текстур и качество материалов
  при этом не меняются вообще — меняется только распределение и дальность тени
  (качество теней), то есть это решение автора о виде, а не деградация картинки
  «в целом».
- Renderer CPU масштабируется с числом теневых draw calls почти линейно
  (30 086 → 16.71 мс, 16 132 → 12.59, 14 284 → 8.58, 9 698 → 7.24), то есть
  именно отправка теневых вызовов, а не пиксели, определяет стоимость рендера.
- При комбинации 2+45 физика (11.35 мс) становится **больше половины кадра**
  (17.51 мс): дальше упирается в physics, а не в рендер.
- Прогоны с прогревом 12 с (в предыдущих таблицах) занижают выигрыш теневых
  ручек, потому что в них ещё работает фоновая проверка адресов; для сравнения,
  те же ручки при 12 с давали 40.47 и 33.95 FPS вместо 48.68 и 40.81.

### Повтор одной проверки стойки внутри одного физического такта (проба адресов)

Фоновая проверка адресов расходует свой 2-мс бюджет каждого такта (см. выше:
13.14 → 10.91 мс physics и 96.97 % → 5.43 % длинных кадров после её завершения),
и внутри этого бюджета каждый 8-см шаг делает четыре физических запроса. Один из
них теперь не повторяется.

`TryAdvance` начинается с `Clear(from)` — проверки стойки в стартовой позе шага, а
стартовая поза шага k+1 это ровно та `feet`, для которой шаг k уже выполнил
`Clear(feet)` (в `TryStep` — `Clear(feet)` в конце ветки подъёма). Драйвер
(`AddressAccessVerifier._PhysicsProcess`) выполняет весь цикл проб синхронно, не
возвращаясь в движок между шагами: в вызывающем коде нет `await`, `CallDeferred` и
`ToSignal`, а `PhysicsServer3D.BodyTestMotion` — запрос, не меняющий мир. Значит
внутри одного такта мир для этого запроса заморожен, и ответ на битово ту же позу
не может отличаться. Результат запоминается и переиспользуется, если совпали
**кадр физики**, **точная поза** и **все входы запроса** (`StandingBodyHeight`,
`BodyRadius`, `VehicleControlled` — от последнего `CanFitAt` переключает маску;
набор исключений `_stanceProbeExclude` неизменен, мир внутри такта заморожен).
Другая поза, другой кадр или NaN-поза (сравнение точное, NaN не совпадает) идут в
свежий запрос; `CanFitAt` не имеет побочных эффектов (переписывает поля
собственного query-объекта и читает их), поэтому пропуск вызова ничего не теряет —
ветки отказа, `LastRejection`, диагностические строки и `StepsClimbed` остаются
достижимыми ровно тогда же.

Кэш **включается только для этого драйвера** (`new AddressWalkProbe(this,
memoizeSameFrameClear: true)`), потому что смоук-тесты гоняют ту же пробу, ожидая
кадры и меняя мир между вызовами; у них поведение остаётся битово прежним
(параметр по умолчанию `false`). Экономия — один `IntersectShape` на шаг внутри
такта (около четверти работы пробы), то есть бюджет 2 мс/такт обрабатывает больше
шагов и окно проверки укорачивается; цена кадра во время окна не меняется, так как
бюджет ограничен временем.

Проверка: сборка 0 предупреждений / 0 ошибок; доказательство выше — по коду с
точными ссылками (`AddressWalkProbe.Clear`, `FirstPersonController.Stance.cs:16-17`
и `:71-86`, `AddressAccessVerifier.cs:50-53`), тестовые пути не затронуты по
построению. **Независимого ревью у этой правки нет**: в текущей сессии делегирование
субагентам недоступно (глубина 1 — предел), поэтому она помечена как проверенная
только автором и ждёт внешней проверки; предыдущие правки того же класса (пропуск
повторного запроса в `TryAdvance`/`TrySupport`) такое ревью прошли с вердиктом
APPROVE.

### Освобождение результатов физических запросов в горячих путях

Managed-профиль показал поток финализатора занятым почти на 100 % на
`Dictionary.Finalize`/`Array.Finalize` → `godot_dictionary_destroy`/
`godot_array_destroy`. Часть этого — результаты физических запросов, которые
создаются на каждый вызов и остаются финализатору. Независимый скан всех
`_Process`/`_PhysicsProcess` тел подтвердил, что **прямых** запросов там нет
(0 вызовов), но шире по проекту нашлось ~20 мест без освобождения, в том числе на
устойчивой частоте:

- `AuthoredWorldDirector.Schedules.FirstObstacle` — по одному лучу **на каждый
  метр** каждого шага расписания, и объект параметров, и возвращаемый `Dictionary`
  оставались финализатору (это самый крупный постоянный источник);
- `VehicleController`: группы опор (per-tick цикл), проверка выхода из машины
  (луч + sweep + два массива исключений), `SpinClear` (массив исключений на каждый
  вызов при повороте), `ValidatePhysicalPlacement` и `TryRestoreAuthoredParking`
  (массивы исключений и результат луча);
- `RinatPresencePresentation` — два места (`GroundAt` и проверка видимости);
- `FootstepAudioController` — луч опоры на каждый шаг (массив исключений + луч +
  результат).

Внедрено освобождение там, где объект создаётся на этом же месте и не является
кэшированным полем: `using` для `PhysicsRayQueryParameters3D`/
`PhysicsShapeQueryParameters3D`/`Dictionary` и `using var owner =
(global::Godot.Collections.Array)typed;` для `Array<Rid>` — в GodotSharp 4.7.1
типизированный `Array<T>` не реализует `IDisposable`, освобождает неутипизированный
псевдоним того же объекта. Нативная сторона хранит собственную ссылку на массив
исключений, поэтому присваивание `ray.Exclude = exclude` не зависит от нашего
`Dispose`; все чтения результата (`Count`, `position`, `normal`, `collider`)
происходят до конца области `using`, включая пути `continue`/`return`. Значения
запросов, исключения, маски и порядок вызовов не менялись — правка только
освобождает то, что и раньше становилось мусором.

Это не влияет на установившийся кадр напрямую (запросы те же), но убирает
постоянный поток нативных объектов в очередь финализатора, то есть ту нагрузку,
которая в профиле держала отдельное ядро занятым.

**Независимое read-only ревью — APPROVE по всем восьми пунктам, обязательных
исправлений нет.** Ревьюер подтвердил по XML установленного GodotSharp 4.7.1, что
у `Array<T>` нет Dispose-члена (освобождать можно только через неутипизированный
псевдоним), что `Excluded()`/`PlacementExcluded()` действительно возвращают свежий
массив на каждый вызов, что кэшированные `_rayExcludes` и `_exitShape` правильно не
освобождаются, что нативная сторона держит собственную ссылку на массив исключений
и что все чтения результатов происходят до конца области `using` (включая
`continue`/`return`), а `CastMotion` возвращает managed `float[]` и освобождать его
не нужно. Он же нашёл **четыре места, которые мой скан пропустил**, и все четыре
закрыты отдельной правкой: возвращаемый массив `VolumeOverlaps` в
`VehicleController.CanRotate` (проверка поворота, per-tick при руле) и в
`VehicleController.WheelCollision` (покадровая проверка контакта колеса),
`PlacementOverlaps` и луч пробы в `VehicleController.PlacementDiagnostics`, а также
движковый массив `source.GetChildren()` в `RinatPresencePresentation` (сборка
лампы).

**Второй проход закрыл и этот остаток — класс исчерпан по проекту.** Тем же
приёмом переведены на владение ещё тринадцать файлов: `Act1ConnectedWorld.Bathhouse.cs`,
`Act1ConnectedWorld.MosqueInterior.cs` (в том числе массив исключений и результат
`IntersectShape` внутри цикла по шагам двери), `Act1ConnectedWorld.MosqueSaveSupport.cs`
(пять мест: два массива исключений, два `IntersectRay`, результат `IntersectShape`
на старте и в конце свипа), `Act1ConnectedWorld.PlayerFootwear.cs`,
`Act1ConnectedWorld.PublicBuildings.cs` (там объект параметров не освобождался
вообще), `Act1ConnectedWorld.ShopUses.cs`, `LadderTraversal3D.cs` (четыре места),
`PortableLight.cs` (луч и массив исключений на каждую проверку видимости),
`VehicleController.WheelCollision.cs`, `DebugWorldGrid.cs` и все места
`CarryCoordinator`.

Отдельная находка второго прохода — **межметодное владение**: локальный помощник
`CarryCoordinator.Trace(...)` возвращает `Dictionary`, то есть владельцем становится
вызывающий, и **все восемь вызовов** этот словарь не освобождали
(`CarryCoordinator.cs:99,241,376,394,402`, `CarryCoordinator.Mechanisms.cs:224,230`,
`CarryCoordinator.Physics.cs:360`). Часть из них — покадровые пути прицеливания при
переносимом предмете, то есть это был постоянный поток словарей в финализатор.
Теперь каждый вызов освобождает результат (`using var`), а сам `Trace` освобождает
свой массив исключений, не затрагивая возвращаемый словарь: это разные объекты.

Повторный скан по всему проекту после двух проходов даёт **ноль** мест, где
созданный на вызов физический объект остаётся финализатору: единственные два
совпадения — корректно владеемые кэшированные поля `AlsuStreetWalkPresentation._groundRay`
(освобождается в `_ExitTree`) и `FirstPersonController._stanceProbeQuery`
(освобождается в `ReleaseStanceProbes`), которые по построению не должны
освобождаться на каждый вызов.

**Статус проверки второго прохода.** Независимое ревью первого прохода было
выполнено (APPROVE 8/8, см. выше), а для второго прохода делегирование в этой сессии
недоступно (лимит глубины субагентов), поэтому он помечен как **проверенный только
автором** и ждёт внешнего ревью — тем же порядком, каким второй исполнитель помечал
свои правки. Самоотчёт автора: сборка 0 предупреждений / 0 ошибок; механическая
сверка диффа показывает 50 добавленных освобождений, каждое — либо `using var owner =
(global::Godot.Collections.Array)typed` для типизированных массивов, либо `using var`
для `Dictionary`/параметров запроса/результата `Trace`; компилятор сам гарантирует
правило типов (применение `using` к `Array<T>` не собирается — CS1674); чтения
результатов проверены по каждому месту, включая `continue`/`return` и циклы.

### Тот же класс мусора в чтении мешей (`SurfaceGetArrays`)

`Mesh.SurfaceGetArrays(surface)` каждый раз строит **новый принадлежащий
вызывающему** `Array` и копирует в него всю поверхность (вершины, нормали,
касательные, UV, цвета, индексы). Скан нашёл **39 мест**, где этот массив
оставался финализатору — почти все в сборке мира (`AgentBAct1ExteriorLayer` — 10,
`Act1ConnectedWorld` — 7, `VehicleController.CompoundCollision`, `PublicFenceJunction`,
`VehicleVisualFactory`, `MosqueClothing`, `SnowRelief`, `BoardCrossing`,
`PublicBuildingShell`, `BabaiRelocation`, `YardMechanisms`, `PlayerFootwear`,
`Addresses`, `KaraGradeSupports`), то есть это копии целых поверхностей, а не мелкие
объекты. Там, где поверхность читается внутри проекции или LINQ, массив создавался
внутри лямбды, поэтому освободить его на месте было нельзя.

Внедрено: `using var arrays = …SurfaceGetArrays(…)` (неутипизированный
`Godot.Collections.Array` реализует `IDisposable`), для `.Duplicate(true)` —
владение и исходным массивом, и копией; для проекций добавлены маленькие
приватные помощники `SurfaceVertices(Mesh,int)`, `SurfaceBytes(Mesh,int)` и
`RidgeSurface(Mesh,Node3D)`, которые возвращают уже скопированные managed-данные и
освобождают движковый массив у себя. В `RidgeSurface` заодно устранено двойное
чтение одного и того же меша: раньше `SurfaceGetArrays(0)` вызывался отдельно для
вершин и для индексов, то есть поверхность копировалась дважды на каждый меш.

Почему это безопасно: в исходнике Godot 4.7-stable
(`scene/resources/mesh.cpp`) `ArrayMesh::add_surface_from_arrays(PrimitiveType,
const Array &p_arrays, …)` принимает массив **по константной ссылке**, только
читает его через `mesh_create_surface_data_from_arrays` в
`RenderingServerTypes::SurfaceData` и затем `add_surface(...)` сохраняет уже
**скопированные** `vertex_data`/`index_data`; сам `Array` не удерживается. А
`Mesh::surface_get_arrays` → `mesh_surface_get_arrays` возвращает новый массив, то
есть владение у вызывающего. Ни одна пара значений, порядок вызовов, маски или
ветки не менялись.

### Независимая проверка трёх правок второго исполнителя

Коммиты `1ef6a246` (мемо проверки стойки внутри такта), `509b8e5c` (обход фасадов
и `StringName`) и `ae282bdc` (предикат главного меню) в своих сообщениях прямо
указывали, что независимого ревью у них нет. Проверка выполнена read-only по коду и
диффам, **APPROVE по всем трём**, обязательных исправлений нет:

- `1ef6a246`: мемо ключуется физическим кадром, точной позой и всеми входами
  запроса; `CanStandAt` → `CanFitAt` действительно читает только `StandingBodyHeight`
  (поле `_standingHeight`), радиус капсулы, `VehicleControlled` и живую
  `CollisionMask`, а побочных эффектов у него нет. Не входящие в ключ входы
  (`CollisionMask`, которую `FirstPersonController.Movement.cs:72-79` переключает в
  0 и обратно, массив исключений и `IsInsideTree()`) безопасны ровно потому, что
  драйвер аудита не возвращается к движку внутри такта; это закреплено комментарием,
  как и запрет включать опцию драйверу, который ждёт кадры.
- `509b8e5c`: владельческий массив `GetChildren()` освобождается через неутипизированный
  псевдоним, кэшированные `StringName` не меняют значений предикатов; остальные
  ~130 мест с `GetChildren()`/`FindChildren()` сознательно отложены отдельным
  проходом.
- `ae282bdc`: новый `MainMenuUi.AnyUndismissed` возвращает ровно то же множество,
  что прежний LINQ-предикат (те же три условия, объединение по группе, пустая
  группа — false на быстром пути), и в установившемся режиме не выделяет массив,
  потому что `Dismiss()` не только ставит флаг, но и вызывает `QueueFree()`, так что
  узел уходит из группы. Инвариант «обе ветки держат одни и те же три условия»
  закреплён комментарием.

### Сколько ещё можно выиграть на draw calls: потолок посчитан статически

Разбор ключей по владельцам (census установившегося High-прогона) плюс разбор самого
ассета закрывают вопрос о том, есть ли в отрисовке скрытый резерв. Всего в кадре
видимости 21 402 поверхности → 3 850 различных ключей (mesh, material, mirror).

| Владелец | поверхностей | ключей | переиспользование |
|---|---:|---:|---:|
| AgentBExteriorWorld (лес/земля, MultiMesh) | 14 237 | 259 | 54.97 |
| AuthoredWorldDirector | 3 238 | 985 | 3.29 |
| Act1AuthoredExteriorKitPresentation (импорт) | 876 | 870 | 1.01 |
| KaraForestEdge | 893 | 164 | 5.45 |
| VillageForestGorge | 649 | 349 | 1.86 |
| FapExterior | 311 | 289 | 1.08 |
| ZiratMemoryField | 231 | 185 | 1.25 |
| SettlementAddressPresentation | 120 | 120 | 1.00 |
| VillageVehicles | 100 | 100 | 1.00 |

Самый подозрительный владелец — импортированный кит
(`res://assets/models/act1/urman_village_exterior_kit.glb`): 876 поверхностей дают
870 ключей. Разбор GLB статически (Python, без движка: glTF-JSON внутри контейнера,
хеши реальных байтовых диапазонов аксессоров вместе с их разметкой —
`componentType`/`type`/`count`/`normalized`) даёт:

- 1 476 мешей, 1 499 узлов, 1 692 примитива, **всего 21 материал**, 0 узлов с
  отрицательным масштабом (то есть зеркала ключи не раздувают);
- **733 из 1 476 мешей байт-идентичны другому мешу** (886 различных сигнатур
  геометрии+материала) — экспортёр просто не инстансировал повторы (например,
  38 копий одной и той же детали и т.п.).

То есть дело не в коде игры: каждый узел ассета несёт собственную копию меша, а
автоинстансинг в Godot требует **общего** RID меша, поэтому 733 дубликата дают
отдельные ключи. Однако потолок выигрыша здесь мал и считается напрямую:

- индексирование мешей схлопывает ключи только у **совместно видимых** дубликатов;
  из 876 видимых поверхностей кита переиспользование сейчас 1.01, то есть
  реалистично ожидать сокращение ~300–400 ключей;
- один теневой/видимый draw стоит по измеренному наклону (30 086 теневых draws →
  renderer CPU 16.71 мс, из которых видимый проход ~2.6 мс при 6 619 draws) около
  0.3–0.5 мкс, поэтому 300–400 видимых ключей ≈ 0.1–0.2 мс, а те же кастеры в
  2–3 каскадах ≈ ещё 0.2–0.4 мс;
- итог — **порядка 1–2 % FPS за ассетную операцию без возможности проверить импорт
  при приостановленных запусках**. Это ниже шума, поэтому правка не внедрялась:
  она записана как кандидат с посчитанным потолком.

Отсюда же общий вывод по отрисовке: видимый проход стоит ~2.6 мс из 16.71 мс
renderer CPU, то есть даже сокращение числа видимых ключей вдвое даёт ~1.6 % FPS;
основную цену платит **теневой** проход (4 каскада × почти весь видимый набор
кастеров), и его сокращение — это решение автора о числе каскадов и дальности
(измерено: 2+45 даёт 57.09 FPS против 31.93). Процедурные примитивы, создаваемые в
циклах с инвариантными параметрами (например планки настила в ущелье с двумя
материалами, шайбы/болты вывески, заклёпки адресных табличек), тоже относятся к
этому порядку величин: их интернирование доказуемо, но каждое даёт доли процента,
и это записано как кандидат, а не как правка.

### Снижение стоимости одной пересборки адресного графа

Живой профиль показал, что именно пересборки графа (медиана 9.6 мс, до 18.4 мс,
30 штук за 27 с) образуют рывки первой минуты: все 26 сопоставленных пересборок
попали в кадры >33.3 мс (средний такой кадр 55.7 мс против 36.8 мс в среднем).
Внутри пересборки при 1 184 сегментах узкая фаза занимала 6.6 мс на 22 328
протестированных пар (296 нс на пару) — это стоимость чтения полей объектов
`Segment`, разбросанных по куче, а не арифметики.

Внедрено (эквивалентность по значениям, без изменения решений и порядка):

- Входы обоих предикатов узкой фазы (`SegmentBounds` — 40 байт — и приращения
  концов) упакованы в три массива, заполняемые один раз из тех же
  `Segment.Bounds`/`DeltaX`/`DeltaZ`. Прежняя форма снимала 40-байтовую копию
  структуры с двух объектов на каждую пару и обращалась к объекту `b` по случайному
  адресу; теперь предикат читает непрерывный массив (~47 КБ на 1 184 сегмента,
  помещается в кэш L2), и объекты `Segment` в горячем цикле не разыменовываются
  вовсе — они нужны только для принятых пар, где вызывается `AddIntersections`.
- Словари ячеек (`spatialCells`, `directionCells`, `groupCells`) переведены с
  ключа-кортежа `(int,int)` на `long` ключ `((long)x<<32)|(uint)z` — биекция,
  то есть тот же набор ячеек и те же корзины, но без хеширования `ValueTuple` на
  ~25 тыс. обращений за пересборку.
- Группировка больше не строит
  `segments.SelectMany(s=>s.Cuts).OrderBy(c=>c.Key,StringComparer.Ordinal)`:
  последовательность собирается в заранее выделенный список, а полный компаратор
  (строковый ключ по `CompareOrdinal`, затем позиция в этой последовательности)
  даёт ту же перестановку, что стабильная сортировка LINQ, без итераторов, буфера
  и массива ключей. Условие `segments.All(s=>s.Bounds.Cullable)` заменено проходом
  по упакованному массиву.

Доказательства: сборка 0 предупреждений / 0 ошибок; отдельный C#-харнесс
(вне репозитория, удалён после прогона) сравнил перестановку нового компаратора с
`OrderBy(Key, Ordinal)` на 20 000 случайных последовательностей с намеренными
коллизиями ключей, пустыми и не-ASCII ключами — перестановки совпали полностью;
инъективность `CellKey` проверена отдельно (0 коллизий). Эквивалентность значений
в узкой фазе и полнота перевода словарей проверены независимым ревью этого diff:
APPROVE по всем семи пунктам (упакованные массивы и их заполнение, тождественность
`MayIntersect`/`RetainDisjointPair`, direction-фильтр, биекция `CellKey` и полнота
перевода всех восьми мест, порядок группировки и замену `All`), без обязательных
исправлений; из замечаний без дефекта применено одно — начальная ёмкость
`groupSequence` поднята до `segments.Count*2`, чтобы список не перерастал на
каждой пересборке. Тот же diff независимо проверил и второй исполнитель в этом
checkout (коммит `c2cf8499`, «verified read-only, build passes 0/0»).

Ожидаемый эффект — не более 1–2 мс из ~10 мс на пересборку и ~0.5 МБ меньше
мусора на каждую из 30–85 пересборок сессии, то есть уменьшение и укорочение
рывков первой минуты; на установившийся кадр это не влияет. Живой замер эффекта
(`URMAN_ADDRESS_GRAPH_PERF=1`, поля `intersection_us`/`group_us`) не выполнен:
игровые запуски в текущем аудите приостановлены поручением автора, и эта правка
проверена статически (сборка, харнесс, независимое ревью).

### Две оставшиеся стоимости на пути публикации адреса

Живой профиль показал, что каждый проверенный адрес стоит примерно 10 мс
пересборки графа плюс столько же на публикацию: все 26 сопоставленных пересборок
попали в кадры >33.3 мс, а средний такой кадр (55.7 мс) вдвое длиннее среднего
кадра окна (36.8 мс). Пересборка разобрана выше; на самой публикации
(`AttachVerifiedAddressAccessPaths` → `RefreshVerifiedWinterFootpaths`, по вызову
на каждый адрес) нашлись ещё две линейные стоимости, не связанные с графикой и
решениями автора:

- **Revision каждого маршрута пересчитывался заново на каждом attach.** Строка
  `string.Join(";", path.Select(point => point.ToString()))` строилась для **всех**
  уже проверенных маршрутов при каждой публикации, хотя мемоизация по метаданным
  меша проверялась уже после её вычисления. При ~85 маршрутах по ~150 точек это
  десятки миллионов операций и десятки мегабайт мусора за первую минуту.
  Внедрена мемоизация по ссылке на массив маршрута: revision — чистая функция
  содержимого массива, а `CommitAddressAccess` кладёт туда свежий массив из
  `Compact(...)` (`AddressAccessVerifier.cs:407+`), поэтому ссылка однозначно
  определяет содержимое; запись мемо удаляется вместе с устаревшим мешем.
- **`NodeAt` линейно сканировал все узлы на каждый вызов**, а вызывается он по
  разу на каждый уже проверенный маршрут при каждой публикации
  (`Addresses.cs:491`) и ещё раз на старте поиска каждого адреса
  (`AddressAccessVerifier.cs:234`), то есть O(адреса × маршруты × узлы) за сессию.
  Добавлен индекс узлов по метровым ячейкам: допуск `.001` гарантирует, что
  подходящий узел лежит в ячейке точки или в одной из восьми соседних, поэтому
  множество кандидатов — надмножество, а сам тест `DistanceXZ<.001` и тай-брейк по
  лексикографически наименьшему id сохранены без изменений. Индекс строится в
  конце `RebuildCore` сразу после заполнения `_nodes`, а `_nodes` нигде больше не
  меняется.

Обе правки эквивалентны по значениям и собраны с 0 предупреждений / 0 ошибок.
Независимое read-only ревью дало **APPROVE по всем восьми пунктам без обязательных
исправлений** и подтвердило три инварианта, от которых зависит корректность:
массив маршрута не мутируется на месте (писателей `_addressVerifiedPaths` ровно
два — `Remove` и присваивание свежего `Compact(...)`), узел с `DistanceXZ < .001`
не может выйти за 3×3 окрестность при сетке 1 м (включая отрицательные координаты
и границы ячеек), а `_nodes` не имеет писателей вне `RebuildCore`. По замечаниям
без дефекта эти инварианты закреплены комментариями у полей — чтобы будущая правка
не сломала мемо и индекс молча. Живой замер не выполнялся: игровые запуски в текущем аудите
приостановлены поручением автора.

### Профиль managed-кода и важная оговорка про Debug-сборку

Отдельный прогон с 12-секундным managed-сэмплированием
(`dotnet-trace --profile dotnet-sampled-thread-time`, сводка сохранена вне
репозитория как `live_final/trace/managed_top_summary.txt`; сырые `.nettrace` и
`.speedscope.json` удалены, чтобы не засорять диск) попал на пересборку сцены
после паузы по потере фокуса, поэтому профилирует **сборку мира**, а не
установившийся кадр. Этот прогон (24.14 FPS, physics 19.24 мс) в таблицы выше
не входит.

Что в нём видно на главном потоке (доли от сэмплированного CPU во время сборки
мира):

- `godotsharp_method_bind_ptrcall` — 33.3 % (нативные вызовы через interop);
- `RuntimeHelpers.GetHashCode` — 13.6 % (хеширование в `DisposablesTracker`, куда
  GodotSharp регистрирует каждый создаваемый `StringName`/`Array`/`Dictionary`);
- `godotsharp_string_new_with_utf16_chars` + `godotsharp_string_destroy` — 16.8 %
  (создание/уничтожение managed-строк из `Name.ToString()` и подобных вызовов);
- `godotsharp_instance_from_id` — 10.4 % (обратный поиск managed-объекта по id при
  маршалинге `GetParent()`/`GetChildren()` и т.п.);
- `godotsharp_array_size`, `string_name_new_from_string`, `string_name_as_string`,
  `unmanaged_get_instance_binding_managed` — ещё около 15 % на ту же маршалинг-цену;
- `AddressFacadeMount.Contains(Triangle, Vector2)` — 1.05 % и `Vector2.Subtraction`
  — 1.03 %: собственно геометрия при выборе места таблички адреса.

Отдельно: **поток финализатора во время трассы был занят почти на 100 %**
(`GC.RunFinalizers` → `Array.Finalize`/`Dictionary.Finalize`/`StringName.Finalize`
→ `godot_array_destroy`/`godot_dictionary_destroy`/`godot_string_name_destroy`),
то есть неосвобождённые обёртки Godot по-прежнему грузят финализатор, и главный
поток при этом конкурирует с ним за `DisposablesTracker`
(`Monitor.Enter_Slowpath` в профиле).

**Контрольный прогон: Release-ассембли того же кода ничего не меняет.**
Обнаруженная в профиле цена маршалинга — это код GodotSharp, а не наш; чтобы
отделить вклад конфигурации сборки managed-кода, был поставлен контрольный опыт:
тот же checkout, та же сцена и настройки, но `bin/Debug/Urman.Game.dll` заменён на
Release-сборку (`dotnet build -c Release`), после прогона возвращён обратно (sha256
Debug-ассембли `488e2171afeb2a27…` до и после совпал).

| Прогон (прогрев 60 с) | FPS | avg | p95 | длинные кадры | shadow draws | renderer CPU | physics |
|---|---:|---:|---:|---:|---:|---:|---:|
| Debug-ассембли (обычная сборка) | 31.93 | 31.32 | 33.54 | 5.43 % | 30 086 | 16.71 | 10.91 |
| Release-ассембли, тот же код | 31.41 | 31.84 | 33.08 | 4.24 % | 30 086 | 17.24 | 11.38 |

Разница в пределах разброса, то есть **оптимизация managed-кода сама по себе не
является здесь рычагом**: кадр определяется нативной работой (отправка теневых
вызовов и физический сервер), которая от конфигурации нашего ассембли не зависит.
Прежняя формулировка про «Debug занижает абсолютные числа» этим опытом **не
подтверждается** и заменена. Что осталось непроверенным — цена **редакторского/
debug-рантайма движка** (в том числе `DisposablesTracker`, который виден в профиле):
чтобы её измерить, нужен release-экспорт (`eng/export-desktop-release.sh`), а он не
выполнялся (это операция экспорта, а не запуск игры, и она создаёт крупные
артефакты). Поэтому абсолютные числа ниже следует читать как числа для запуска
`--path game` с редакторским бинарём движка, одинаковые для всех прогонов этой
таблицы; относительные сравнения внутри одной сборки корректны.

Отдельное наблюдение из профиля, не влияющее на кадр напрямую: **поток
финализатора занят почти на 100 %** (`Array.Finalize`/`Dictionary.Finalize`/
`StringName.Finalize` → `godot_*_destroy`), то есть неосвобождённые обёртки Godot
по-прежнему составляют заметную нагрузку на финализатор. Это уже вне горячих
путей, которые были закрыты ранее, и в кадр напрямую не входит; отдельного
разбора требует вопрос, есть ли у этой нагрузки измеримый эффект через GC.

**Вывод по цели (обновлён после замеров установившегося режима).** На High 1080p
установившийся кадр с текущими настройками — 31.32 мс (31.93 FPS) при 5.43 %
кадров длиннее 33.3 мс, а первая минута сессии хуже из-за фоновой проверки
адресов (38.12 мс, 96.97 %). Визуально-нейтральные правки разрыв не закрывают:
уменьшение числа пикселей не даёт ничего, а тени по отдельности дают 40.81 FPS
(2 каскада) и 48.68 FPS (45 м). **Полосу 50–60 FPS даёт только комбинация
2 каскада + 45 м — 57.09 FPS, p95 19.1 мс, длинных кадров 0.35 %** — при
неизменных разрешении, MSAA, scale, геометрии, текстурах и материалах, то есть
ценой решения автора о распределении и дальности тени. При этой комбинации
кадр становится 17.51 мс, из которых 11.35 мс — physics, поэтому следующий
рычаг — не рендер, а физика (лошадь, сервер, бюджет проверки адресов). Все
абсолютные числа получены на **Debug-сборке** и на **M4 Pro**, поэтому перед
срезом качества нужен замер Release-сборки и приёмка на целевом M1.

Воспроизведение: `eng/protected_run.py --clean -- python3 <probe.py> high
village_day@arrival 15 <outdir> [URMAN_PERF_*=...]`, где `<probe.py>` пишет
временный `settings.json` в userdata-копию и запускает Godot с перечисленными
выше аргументами; receipt — `render.json` плюс строка
`act1-demo-package-performance: ... graphics_overrides=...` в `run.log`.

### Сборка мира: меньше мусора в обходе фасадов (по профилю managed-кода)

Профиль managed-кода (см. выше) показал на главном потоке во время сборки мира
создание/уничтожение managed-строк (~17 %), построение `StringName` (~5 %) и
постоянный обратный поиск managed-объекта по id (~10 %), а поток финализатора —
почти на 100 % занятым `Array.Finalize`/`Dictionary.Finalize`/`StringName.Finalize`.
Один из самых заметных игровых владельцев в этом профиле — `AddressFacadeMount`
(выбор места таблички адреса, вызывается на каждое авторское здание при построении
реестра адресов).

Внедрено (значения и решения не меняются):

- Обход `AddressFacadeMount.Descendants` — рекурсивный по всем потомкам — больше не
  оставляет за собой владельческий массив `GetChildren()`: типизированный вид
  перебирается, а неутипизированный псевдоним того же объекта освобождает его по
  завершении перечислителя. Раньше каждый узел обхода создавал нативный `Array`,
  который освобождал только финализатор.
- Шесть ключей метаданных в двух горячих предикатах (`IsWalkThroughFoliage`,
  `IsInteriorDressing`, по три `HasMeta` на каждого предка каждой меши) вынесены в
  статические `StringName`. `HasMeta(string)` строит `StringName` на каждый вызов,
  а сами предикаты вызываются для каждого кандидата; ключи неизменны, поэтому
  значения ответов те же.

Что это даёт и чего не даёт: это стоимость **сборки мира** (один раз за сессию) —
меньше нативных аллокаций, меньше работы финализатору и меньше давления на GC на
старте; на установившийся кадр и на измеренные 31.9/57.1 FPS это не влияет.
Остальные ~130 мест с `GetChildren()`/`FindChildren()` в других файлах не
тронуты: это механическая, но широкая правка по многим файлам сразу, и её стоит
делать отдельным проходом, а не в одном коммите с этой. Сборка 0/0; независимого
ревью у этой правки нет по той же причине, что и выше (делегирование в текущей
сессии недоступно).

### Статическая атрибуция ~11 мс physics: что известно без прогона

Живой замер установившегося High даёт physics 10.91 мс на кадр (при 31.9 FPS это
около 1.9 физических тактов на кадр, то есть ~5.7 мс на такт). Managed-трассу
установившегося кадра снять нельзя (запуски приостановлены), но состав такта
частично считается по коду:

- **Одна конная повозка** (в `content/vehicles/act1_vehicles.v1.json` ровно три
  транспорта: Niva, Motorcycle, HorseCart). У парковки без водителя
  `VehicleController._PhysicsProcess` уходит в ветку `Driver is not {} player`
  (`VehicleController.cs:230-236`) и вызывает `UpdateVisuals(dt)`, где
  `HorsePose.UpdatePose` (`VehicleController.cs:543`) делает `PreparePose` и
  `CommitHorsePose` (`VehicleHorsePose.cs:97-98`). `PreparePose` проверяет опору
  каждой из 4 ног: один центральный луч плюс кольцо `SoleSegments = 16`
  (`VehicleHorsePose.cs:59`, `Ground` `:282-310`) — **68 лучей на такт** — и
  `HoofEndpointClear` даёт 4 фигурных запроса (`CompoundCollision.cs:387-411`);
  повторные `Ground` в `SupportedHorseFrame` в этой ветке не выполняются
  (`freshlyPrepared: true` и ранний выход, `CompoundCollision.cs:581-587`).
- **Фоновая проверка адресов** занимает 2 мс каждого такта, пока её очередь не
  пуста, то есть ~3.8 мс кадра в первую минуту, и ~0 после (см. выше).
- Игрок — `CharacterBody3D` (`MoveAndSlide` плюс проверки стойки/шага, часть из
  которых уже кэширована), остальные подписчики `_PhysicsProcess` (11 штук) и сам
  шаг физического сервера.

Чего здесь **нельзя** утверждать без профиля: как эти части делят 5.7 мс на такт.
Стоимость одного физического луча по BVH деревни в коде не видна, и подставлять
её «на глаз» было бы догадкой; поэтому в отчёте фиксируются только посчитанные
величины (68 лучей и 4 фигурных запроса на такт для единственной повозки, 2 мс
бюджета проверки адресов), а разделение 10.91 мс остаётся задачей следующего
разрешённого прогона (managed-трасса установившегося кадра + `URMAN_ADDRESS_GRAPH_PERF=1`).

Отсюда же следуют единственные оставшиеся рычаги физики, и все они — решения
автора, а не оптимизации:

1. **Бюджет проверки адресов как 2 мс на такт.** Ограничение задано на *такт*, а
   при 31.9 FPS тактов на кадр ~1.9, поэтому кадр платит до ~3.8 мс. Вариант,
   который сохраняет и порядок, и общее число проб, и сам бюджет: выдавать бюджет
   не более одного раза за отрисованный кадр (то есть только на первом такте
   кадра). Цена кадра в первую минуту падает примерно вдвое, окно проверки
   удлиняется примерно во столько же; результаты проб при этом зависят от момента
   их выполнения (двери/динамика), поэтому это поведенческое решение.
2. **Частота физики.** 60 тактов в секунду при кадре 31.9 мс — это 1.9 такта на
   кадр, то есть вся пер-тактовая работа умножается почти на два. Это настройка
   проекта, а не код.
3. **Кольцо опоры копыта** (`SoleSegments = 16`): 17 лучей на ногу за проверку.
   Уменьшение кольца вдвое уменьшило бы лучи вдвое, но огрубило бы проверку
   опоры — это видимое/поведенческое решение.
4. **Число подготовок позы за такт.** Три подготовки (`PreparePose` для `end`,
   подготовка в шаге подъёма и `SupportedHorseFrame` при перебазировании плана)
   отличаются друг от друга не логикой, а округлением базиса (`InterpolateWith`,
   `RebasePose` → `Solve`), поэтому сократить их без изменения точной позы нельзя:
   доказательства эквивалентности нет, кандидат остаётся с этим риском.

### Проверка кандидата «слияние геометрии» по данным самого ассета

Разбор census показал, что крупнейший оставшийся источник различных ключей —
`Act1AuthoredExteriorKitPresentation` (870 ключей на 876 поверхностей в кадре
видимости), и что владелец этот — импортированная сцена. Чтобы не гадать, данные
проверены напрямую в ассете `game/assets/models/act1/urman_village_exterior_kit.glb`
(разбор GLB-контейнера и хеширование геометрии по аксессорам, чистый статический
анализ данных, без движка):

| величина | значение |
|---|---:|
| мешей в ассете | 1 476 |
| материалов в ассете | 21 |
| узлов | 1 499 |
| треугольников всего | 44 089 |
| различных подписей (геометрия + материал) | 886 |
| различных только геометрий | 882 |
| мешей, использованных более одного раза | 733 (49.7 %) |
| самый повторяемый меш | 38 раз |

Что из этого следует:

- **Уникальность в этом владельце — это меши, а не материалы.** Материалов в
  ассете всего 21, и код всё равно перекрывает их общим набором
  (`mesh.SetSurfaceOverrideMaterial(surface, PainterlyMaterialLibrary.ForColor(color))`,
  `Act1ConnectedWorld.cs:1403`, где цвет — одно из пяти значений), то есть
  материалы уже общие. 876 размещённых поверхностей дают 870 ключей потому, что
  каждый размещённый фрагмент несёт свою геометрию (882 уникальных), а зеркальные
  постановки добавляют к ключу компонент `mirror`. «Починить» здесь нечего: шаринг
  материалов уже сделан, а автоинстансинг объединяет только совпадающие пары
  (меш, материал, зеркало).
- **Слияние геометрии по (фасад, материал) — единственный оставшийся способ, и его
  выигрыш теперь измерен сверху.** В ассете 44 089 треугольников на 1 476 мешей
  (в среднем 30 треугольников на меш), а видимых ключей этого владельца 870 из
  6 619, в теневом проходе он даёт порядка 1.7–2.6 тыс. из 30 086 вызовов (2–3
  каскада из четырёх). При измеренной стоимости 0.46 мкс на вызов это верхняя
  оценка экономии **≈1–1.2 мс из 31.3 мс кадра (3–4 % FPS)** — и только если слияние
  сведёт фрагменты к одному вызову на материал, сохранив попиксельный результат.
  Плата — потеря локального отсечения внутри сгруппированного фрагмента (тот же
  механизм уже один раз измерился отрицательно: лесная полоса 108 → 95 FPS), плюс
  необходимость группировать по видимости/LOD и не трогать cutout, two-sided и
  зеркальные поверхности.
- Вывод: кандидат **не оправдан** при текущей оценке — это заметная по объёму
  правка визуального конвейера ради 3–4 % с риском видимого поведения на границах
  отсечения, и без прогона её ценность нельзя даже подтвердить. Он остаётся в
  таблице кандидатов с этой оценкой, а не внедряется «на глаз».

## Сборка мира: обходы дерева больше не выделяют по массиву на узел

Managed-трасса сборки мира (окно 12 с, та самая, где главный поток тратил ~33 % на
`godotsharp_method_bind_ptrcall`, ~14 % на хеширование в `DisposablesTracker`,
~17 % на создание/уничтожение managed-строк и ~10 % на `godotsharp_instance_from_id`)
показала ещё одну вещь: **поток финализатора был занят почти на 100 %** на
`Array.Finalize` → `godot_array_destroy`/`godot_dictionary_destroy`, а в профиле
главного потока видны `godotsharp_array_size` и постоянные `GetChildren()`.

Причина найдена в коде: рекурсивные обходы дерева были написаны как
`foreach (var child in node.GetChildren())`, а `GetChildren()` в GodotSharp
возвращает `Godot.Collections.Array` — то есть **по одному финализируемому массиву
на каждый посещённый узел**. Обходы вида `FindDescendants<T>(this)` вызываются в
`BuildAddressRegistry` по разу на строку манифеста адресов (44–85 строк) и идут по
всему connected world, то есть дают порядок **10⁷ выделений массивов за сборку
мира**; каждое из них затем освобождает финализатор. Это и объясняет и занятость
финализатора (~10⁷ × ~1 мкс ≈ 10 с), и деградацию первой минуты, когда очередь
финализации ещё разбирается.

Внедрено (пять файлов, семь обходов): `Act1ConnectedWorld.FindDescendants<T>`
(центральный и самый горячий), `HidePresentationNode`,
`ClearExtractedSceneOwnership`, `InteractionTarget.RefreshAvailability`,
`ClinicSurfacePresentation.Descendants`, `RinatPresencePresentation.Descendants`,
`SnowTrampleField.Meshes` переведены на `GetChildCount()` + `GetChild(index)`.

Эквивалентность:

- `GetChildCount()`/`GetChild(index)` перечисляют ровно тех же детей в том же
  порядке, что и `GetChildren()`; тип-фильтр (`child is T`) и тела циклов не
  изменены, рекурсия идёт по тому же ребёнку.
- Для методов-итераторов ленивость не изменилась: `GetChildren()` тоже вызывался
  при первом шаге итератора, а не при его получении, поэтому «снимок на старте
  обхода» сохранён.
- Единственное реальное различие — «снимок против живого списка» во время
  приостановленной итерации. Проверено сканированием всех вызывающих: во время
  обхода дерево мутируют ровно два места — `BypassDiscoveries.cs:272` (добавляет
  детей в `_act1BypassCollision`, который не является потомком обходимого
  `boundary`) и `StyleBenchmarkZone.cs:225` (dev-зона), то есть ни один вызывающий
  не меняет обходимое поддерево.
- Ранний выход (`FirstOrDefault`) теперь не оставляет за собой массивов вовсе.
- Семантика «только обычные дети» сохранена: значения по умолчанию проверены
  рефлексией по установленному `GodotSharp 4.7.1` — `GetChildCount(bool
  includeInternal = False)`, `GetChild(int idx, bool includeInternal = False)`,
  `GetChildren(bool includeInternal = False)`, то есть индексный обход не может
  начать включать внутренних детей. Дополнительно проверено, что ни один метод в
  этих файлах не возвращает обход наружу (единственные такие методы — сами обходы),
  поэтому круг вызывающих, где возможна мутация во время итерации, закрыт тем же
  сканированием.

Тот же приём уже был принят в этом репозитории для обхода фасадов
(`AddressFacadeMount.Descendants`, коммит второго исполнителя), то есть это не
новая техника, а её распространение на остальные множительные обходы.

**Следующий кандидат (не внедрён):** сами обходы по всему миру внутри цикла по
строкам манифеста. Индекс по имени, построенный один раз, убрал бы ещё примерно
порядок величины посещений, но требует доказать, что за время цикла авторизованное
поддерево не переподчиняется (сейчас цикл только **добавляет** узлы под
`_addressRoot`, скрывает узлы — видимость перепроверяется в момент использования —
и отложенно освобождает таблички), а также что имена создаваемых табличек
не пересекаются с `sourceName` манифеста. Это правка в адресации, от которой зависят
сохранения и граф, поэтому без возможности прогнать адресные smoke-тесты она
записана кандидатом, а не внедрена.

Живой повторный замер не выполнялся: игровые запуски в текущем аудите
приостановлены поручением автора. Проверка независимым ревью в этой сессии
недоступна (сессия исполнителя ограничена глубиной субагентов); эквивалентность
выше проверена автором правки, а независимая перепроверка ожидается от второго
исполнителя этого checkout, который уже дважды так проверял мои правки.

## Пакет решения: что именно менять и как это проверить на M1

Ниже — не предложение «внедрить», а готовый к применению материал: измеренные числа,
точный патч и протокол проверки. Решение о качестве теней остаётся за автором; ничего
из этого в коде не применено.

### Вариант A1 — только дальность теней (визуально самый мягкий)

В `game/scripts/GraphicsQuality.cs`, `ConfigureSun`:

```csharp
sun.DirectionalShadowMaxDistance = Preset switch { "low" => 45f, "high" => 45f, _ => 80f };
```

Измерено (установившийся режим, High, 1080p, scale 1.0, MSAA 4×): **48.68 FPS**,
avg 20.54 мс, p95 22.52, длинных кадров 0.96 %, shadow draws 14 284, renderer CPU
8.58 мс. Влияние на вид: кастеры дальше 45 м перестают отбрасывать тень (солнце
16°, поэтому дальние тени и так почти не читаются), распределение резкости по
расстоянию камеры **не** меняется — это самый щадящий из трёх вариантов.

### Вариант A2 — только два каскада (резкость ближних теней падает)

```csharp
sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits;
```

Измерено: **40.81 FPS**, avg 24.50 мс, p95 26.83, длинных кадров 2.12 %, shadow draws
16 132, renderer CPU 12.59 мс. Влияние на вид: два каскада на те же 120 м означают,
что ближний каскад накрывает не ~30 м, а ~60 м, то есть **резкость теней у ног
заметно падает**. Это вариант с наибольшим визуальным риском из трёх.

### Вариант A3 — оба параметра

```csharp
sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits;
sun.DirectionalShadowMaxDistance = Preset switch { "low" => 45f, "high" => 45f, _ => 80f };
```

Измерено: **57.09 FPS**, avg 17.51 мс, p95 19.06, p99 28.95, длинных кадров 0.35 %,
shadow draws 9 698, renderer CPU 7.24 мс, physics 11.35 мс. Это единственный
измеренный вариант, попадающий в целевую полосу 50–60 FPS на M4 Pro, и единственный,
где физика становится больше половины кадра. Разрешение, MSAA, scale, геометрия,
текстуры и материалы при этом не меняются — меняется только тень.

### Протокол проверки (и приёмки на M1)

1. Прогрев **не короче 60 с**: иначе в замер попадает фоновая проверка адресов
   (измерено: 96.97 % длинных кадров против 5.43 % после её завершения, physics
   13.14 против 10.91 мс). Все числа выше получены с прогревом 60 с и замером 15 с.
2. Запуск — через `eng/protected_run.py` (копия и восстановление userdata с
   проверкой fingerprint), окно 1920×1080, `--urman-perf-probe-mode=gameplay
   --urman-perf-sample=village_day@arrival --urman-perf-request-focus
   --urman-perf-no-vsync`, 100 % фокуса. Ручки сравнения — `URMAN_PERF_SHADOW_SPLITS`
   и `URMAN_PERF_SHADOW_DISTANCE` (только при наличии probe в командной строке), их
   факт попадает в receipt строкой `graphics_overrides=...`.
3. В receipt сверять: `fps`, `p95`, `p99`, `long_frame_fraction`, `shadowDraws`,
   `shadowPrimitives`, `renderer CPU`, `physics`, `cameraPose` (должна совпадать
   между прогонами) и `focused_sample_fraction` (должна быть 100 %).
4. Приёмка цели — **на M1 и на Release-сборке** (`eng/export-desktop-release.sh`),
   а не на этом хосте и не на `--path game` с редакторским бинарём: все числа этого
   отчёта получены на M4 Pro, а контрольный опыт с Release-ассембли показал, что
   конфигурация нашего managed-кода на кадр не влияет (31.41 против 31.93 FPS), то
   есть цена редакторского/debug-рантайма движка остаётся неизмеренной.
5. Критерий цели: 50–60 FPS **и** стабильность (p95 и доля кадров >33.3 мс), а не
   только средний FPS. Визуальная приёмка (в том числе что выбранный вариант тени
   не разрушает картинку) — человеческий проход, он вне автоматизации.
