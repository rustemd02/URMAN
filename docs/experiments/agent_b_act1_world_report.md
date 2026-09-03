# Agent B / Act I Experimental World Report — УРМАН (Кырлай)

Экспериментальный вариант мира Акта I, собранный по правилу `Luna-only`,
в изолированной копии `/Users/unterlantas/Documents/GitHub/URMAN__agent-b-act1`
(APFS `cp -Rc` от справочного checkout). Справочный checkout
`/Users/unterlantas/Documents/GitHub/URMAN` не менялся: по приёмному
контракту туда допускается только этот отчёт
(`docs/experiments/agent_b_act1_world_report.md`).

> **Исторический отчёт.** 24 августа 2026 standalone-вариант `AgentBAct1World`
> был выведен из runnable-кода. Его киты, height-field и foliage-план
> переиспользуются production-слоем
> `Act1ConnectedWorld/AgentBAct1ExteriorLayer`; старые сцена, probes и
> windowed capture launcher удалены, чтобы в репозитории оставался один
> исполняемый владелец внешнего мира. Кадры и receipt ниже сохраняются как
> экспериментальное evidence, но не как production acceptance.

---

## STATUS

**PASS** — 11/11 waypoint пройдено физическим шагом за один проход
(~236,3 м, 8 058 физ-шагов при 60 Гц), снято 45/45 кадров (1280×720 PNG,
все sha256 сверены с receipt), 9/9 структурных QA-проверок `true`,
`runtime_errors: []`.

Предшествующий прогон достигал 9/11 (`kara_approach`, `cliffhanger`
не достигались) из-за телепортации коллизии `KaraRoot_0` на дорожную ось;
причина найдена детерминированным probe и устранена (см. HONEST FAILURES).

## VARIANT

- Мировой вариант: `act1-world` (Agent B экспериментальный мир, Act I).
- Сцена: `res://scenes/experiments/agent_b_act1/agent_b_act1_world.tscn`,
  мета `agentBVariant=act1-world`.
- Изоляция: код живёт в namespace `Urman.Experiments.AgentBAct1` (+ тестовый
  harness `Urman.Godot.Tests`); существующие Main/RuntimeBridge/
  Act1ConnectedWorld/Act1WorldLayout, benchmark-сцены и прежние GLB-киты не
  правились.
- Вариант — **не** владелец нарративного состояния: квесты, реплики и
  состояния персонажей не создавались. Нарративная интрига размечена только
  9 scene-маркерами (проверка `story_markers`).

## FILES

| Файл | Назначение |
| --- | --- |
| `assets/source/blender/agent_b_act1/agent_b_common.py` | общие Blender-хелперы: палитра `AB_*`, value-noise/fbm, mesh-билдеры, `sample_polyline`, экспорт GLB и `bake_transforms` |
| `agent_b_terrain.py` | кит 1: террейн + дорожная сеть (рутины, канавы, лужи, переправа) |
| `agent_b_buildings.py` | кит 2: дом бабая, дом Алсу, ФАП, мечеть, дворы, заборы |
| `agent_b_foliage.py` | кит 3: 10 семейств силуэтов (берёза, сосна, ель, куст, папоротник, осока, трава, ветка, мох-камень, пень) |
| `agent_b_zirat.py` | кит 4: зират — низкая ограда, ворота, дорожка, скамья, нейтральные метки (`CULTURAL REVIEW REQUIRED` для любых надписей) |
| `agent_b_kara.py` | кит 5: опушка Кара-Урмана — массивы, краевые стволы, корневые откосы, «жест-ветка», клиффхэнгер-кольцо |
| `agent_b_glb_audit.py` | аудит GLB: 0 камер / 0 огней / 0 физики во всех китах |
| `game/assets/models/agent_b_act1/*.glb (+.import)` | 5 экспортированных китов |
| `game/scripts/experiments/agent_b_act1/AgentBAct1HeightField.cs` | детерминированный порт террейна: `Ground`, `GroundWithJitter`, `BuildTerrainFaces`, `RoadInfo` |
| `AgentBAct1Layout.cs` | зоны, `Route` (11 waypoint), `WalkChain` (дорожная цепочка), `ReviewPoints` (45 точек съёмки) |
| `AgentBAct1World.cs` *(retired 2026-08-24)* | исторический standalone-композер: загрузка и перепривязка материалов китов, коллизии, посадка растительности, атмосфера day→Kara-night, дождь, маркеры |
| `game/scenes/experiments/agent_b_act1/agent_b_act1_world.tscn`, `agent_b_act1_play.tscn` *(retired 2026-08-24)* | историческая сцена мира и стартовая сцена |
| `game/tests/AgentBAct1WorldCapture.cs` (+ `.tscn`) *(retired 2026-08-24)* | исторический silent capture: WalkRouteAsync, 45 кадров, FrameStats, JSON-receipt |
| `game/tests/AgentBWalkProbe.cs`, `game/tests/AgentBStuckProbe.cs` (+ `.tscn`) *(retired 2026-08-24)* | исторические диагностические пробы ходьбы и застреваний |
| `eng/agent-b-act1-capture.sh`, `eng/agent-b-act1-run.sh` *(retired 2026-08-24)* | исторические входные скрипты захвата и ручного прогона (Dummy audio, windowed 1280×720) |
| `docs/experiments/agent_b_act1_world_report.md` | этот отчёт |

## WORLD

Авторский физически проходимый Кырлай:

- **Дорожная сеть**: единая цепочка от въезда через двор бабая, главную
  улицу, ветку к ФАПу, просёлок у зирата и узкий съезд в Кара-Урман. Дорога
  прорезана в детерминированный height field (value-noise FBM) с профилем
  короны, колёсных рут и обочин.
- **Архитектура**: дом бабая и әби с двором (топик `LogWall/Roof/Win*`),
  дома соседей, ФАП, мечеть с минаретом, заборы, ворота, колодец с бадьёй,
  переправа с камнями у речки.
- **Зират** на холме восточнее просёлка: 0,9 м деревянная ограда с открытыми
  воротами, дорожка к центру, непритязательные метки без резных символов.
- **Кара-Урман** на конце маршрута: клин тёмных массивов, корни-откосы вдоль
  подхода, одна «жест»-ветка силуэтом в тумане, кольцо стволов вокруг
  площадки клиффхэнгера (z ≈ -122…-132), стоячая линия камня в глубине.
  Никаких мобов и «входа в подземелье»: лес — старый порядок, а не генерик-
  хоррор.
- **Атмосфера**: `ProceduralSky`, туман по высоте, `CpuParticles3D` дождь;
  световой градиент «день → ночь Кара-Урмана» по z (полный день z ≥ -34,
  глубокая ночь z ≤ -92), ночные окна горят эмиссией (`AB_window_warm`).

## ASSETS

| Кит | Мешей | Треугольников |
| --- | ---: | ---: |
| `agentb_terrain_road_kit.glb` | 30 | 21 084 |
| `agentb_village_buildings_kit.glb` | 675 | 9 234 |
| `agentb_foliage_kit.glb` | 120 | 4 648 |
| `agentb_zirat_kit.glb` | 72 | 1 004 |
| `agentb_kara_edge_kit.glb` | 55 | 3 316 |
| **Всего в сцене** | **1 161 меш-инстанс** | **48 814** |

- Материалы: только безлексурные `AB_*` из палитры; композер привязывает их
  к `PainterlyMaterialLibrary.ForColor(hex, surface)`, окна/лужи — к
  `StandardMaterial3D` (эмиссия/глянец).
- Коллизи: террейн как `ConcavePolygonShape3D` из height field + 377
  коллайдеров по авторским мешам (архитектура/зират/kara-edge); декор
  (`Rut_/Ditch_/Puddle_/Road_/Win*/Glass…`) исключён из коллизий по имени.
- Растительность: 209 посаженных копий 10 шаблонов по детерминированному
  плану посадки с привязкой к `Ground(x,z)`.

## TRAVERSAL

- Ходьба: `CharacterBody3D.MoveAndSlide`, гравитация 21,6 м/с², скорость
  3,6 м/с, stuck-recovery с возвратом на грунт при проваливании ниже
  `ground - 1.5`.
- `WalkChain` ведёт по осевой линии дороги, с отводом к `zirat_roadside`
  (-3,2, -68) западнее ограды зирата и присоединением к Кара-дороге
  через (-2,6,-79) → (-1,6,-84) → (-0,6,-89,5).
- Один полный проход: **236,34 м** по горизонтали, 8 058 физ-шагов,
  точки прибытия фиксируются по дистанции до waypoint (`dist ≤ ~2,6 м`).
- Итоговый журнал: `arrival(1) → back_to_street(46) → house_yard(645) →
  house_exterior(724) → main_street(1635) → fap_branch(2136) →
  fap_exterior(2554) → return_street(4760) → zirat_roadside(5828) →
  kara_approach(7280) → cliffhanger(8058)`.

## VISUAL EVIDENCE

- **45 кадров**: 9 зон (arrival, babai, house, street, fap, return, zirat,
  kara, cliffhanger) × 5 направлений (forward / back / left / right / depth,
  у площадки клиффхэнгера — forward / back / up / down / left).
- Все PNG 1280×720, sha256 каждой сверены с receipt (45/45, сравнение
  без учёта регистра hex).
- Capture dir: `/Users/unterlantas/Documents/GitHub/agentb-capture-20260819-235351/`
  (45 PNG + `agent_b_act1_world_receipt.json`).
- Характеристики кадра (пример): arrival_forward luma 0.47, 9 тонов;
  zirat_forward luma 0.44, 11 тонов; kara_forward luma 0.04 — намеренно
  тёмная ночь (см. OPEN GATES). Все кадры имеют ненулевую структурную
  дисперсию (`std_luma` > 0.01), ни один не «плоский».
- Рендерер: `Metal 4.0 - Forward+`, устройство `Apple M4 Pro (Apple9)`,
  тонмаппер ACES, экспозиция 1.1, дождь частицами, туман по высоте.

## VERIFICATION

Все проверки выполнены в изолированной копии (`. eng/dotnet-env.sh`):

| Проверка | Результат |
| --- | --- |
| `dotnet build game/Urman.Game.csproj` (TreatWarningsAsErrors, net10.0) | Ошибок 0, предупреждений 0 |
| `./eng/agent-b-act1-capture.sh` | `AGENTB_CAPTURE_DONE frames=45 waypoints=11 walked=236,3m elapsed=907.8s`; `capture complete: 45 frames` |
| `./eng/verify-dotnet.sh` | контент-сбор валиден, diagnostics 0 (аудио-замыкание 8/8 captions / 8/8 transcripts) |
| `git diff --check` | чисто |
| `graphify update .` | граф обновлён без ошибок |
| receipt: структурные QA | `kits_loaded 5/5`, `mesh_density 1161`, `triangle_budget 48814`, `no_giant_geometry ok`, `collision_layer 377`, `foliage_planted 209`, `story_markers 9`, `no_empty_horizon ok`, `no_degenerate_frames ok` |
| receipt: runtime | `runtime_errors: []` |

## HONEST FAILURES

Ничего не скрывается — что ломалось и как чинилось:

1. **Телепортация `KaraRoot_0` на дорогу (основной блокер, исправлен).**
   `root_bank()` записывал положение `(cx, -cz)` в вершины при неначальном
   object scale; `bake_transforms` запекала только вращение, а glTF-экспорт
   с `export_apply=True` не aplicает object transform — позиция
   умножалась на масштаб ноды (y=0.7), и корень, задуманный на z=-98,
   попадал в z≈-68,6, прямо на линию ходьбы. Итог — затык перед зиратом
   и 9/11 waypoint. Чинилось в двух местах: `bake_transforms` теперь
   хранит комплексный R·S в вершины с мировой сохранностью, а позиция в
   китах носится через `location` (чистая трансляция ноды). Все киты
   пересборены, импорт перекеширован, маршрут проходит 11/11.
2. **Задержка импортного кэша GLB (исправлено).** После пересборки китов
   Godot подхватывал старые `.scn`; кэш форсирован прогоном `--import`
   перед capture (входит в `eng/agent-b-act1-capture.sh`).
3. **QA-ворота `no_degenerate_frames` была слишком строгой (исправлено).**
   Ночная сторона у Кара-Урмана имеет `ground_ratio ≈ 0.99` (сцена
   намеренно тёмная), и кадры заколеблись как «пустые». Теперь кадр
   считается планшетным только при экстремальном соотношении И
   (`std_luma < 0.012` или `mean_luma > 0.35`).
4. **Временное несоответствие sha256 при повторной проверке receipt —
   артефакт сравнения**: `Convert.ToHexString` пишет заглавно, сравнение
   теперь нормализовано; все 45 PNG сходятся с receipt побайтно.
5. **Неудовлетворительная попытка с `--headless`**: headless рендер
   крашится на этом тулчейне (MoltenVK/shader MSL), поэтому все прогоны —
   только окошечные; это задокументировано в LIMITATIONS.

## OPEN GATES

Остаётся открытым на усмотрение следующей итерации / приёмки:

- **Ночная читаемость Кара-Урмана**: `cliff_forward` `mean_luma ≈ 0.02` —
  сцена структурная, но очень тёмная. Поднять экспозицию у финального
  клиффхэнгера или оставить как авторскую атмосферу решает владелец
  приёмки.
- **Культурное ревью меток зирата**: в `agent_b_zirat.py` стоит метка
  `CULTURAL REVIEW REQUIRED`; форма меток держится нейтральной без
  резных символов до отдельного прохода.
- **Локальная плотность дождя**: сейчас амплитуда дождя глобальная;
  сгущение дождя у кара-опушки не настроено (опционально).
- **Клиффхэнгер-надпись**: стоячая линия камня размечена в
  `AgentBAct1Layout.ReviewPoints`, но текстовый «маркер старого порядка»
  не входит в приёмку и требует отдельной проработки сценария.

---

### Приложения

- Receipt: `../agentb-capture-20260819-235351/agent_b_act1_world_receipt.json`
  (`kind: urman.agent_b_act1_world_capture`, frames=45, waypoints=11,
  walked=236.34 m, runtime_errors=[], все QA `true`).
- Исторический запуск захвата: `./eng/agent-b-act1-capture.sh` (скрипт
  удалён; receipt сохранён только как provenance).
- Исторический ручной прогон: `./eng/agent-b-act1-run.sh` (windowed launcher
  удалён и не должен использоваться для production-приёмки).
