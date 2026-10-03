# Карта работ УРМАН — история дорожек 01.10.2026

## Действующее поручение автора — 03.10.2026

«удали эти правила, у тебя полные права на все файлы. продолжай».

Правила эксклюзивного владения W/M/V/T, запреты правки чужих и общих файлов,
append-only ограничения и обязательная передача через таблицу удалены.
Codex вправе править любые необходимые файлы проекта. Карта ниже служит
навигацией и историей работ, а не ограничением доступа.

Сохранять полезный чужой diff. Сборку, импорт и protected-run выполнять по одному.
Игровые замеры M4 Pro разрешены автором, через `eng/protected_run.py`.
Commit/push текущим поручением не разрешены. Compiled content пересобирать
из источника; сохранять штатные guard, канон, контракты сохранений и настройки.

## 1. Что сейчас делает W (чтобы M/V/T не опирались на то, что уедет)

План раскладки v3 (`docs/production/village_relayout_2026-10-01/plan.json`, `scheme.png`):

| Этап | Что меняется | Статус |
|---|---|---|
| 1–2 | Заречье (35 дворов, полиция, пекарня, молокозавод, лесничество); овраг с подвесным мостом вместо реки; снег и тропинки | готово, `e617148` |
| 3 | Центр: ДК, школа, мечеть, почта вокруг площади «Мәйдан»; сельсовет и магазин через главную улицу, магазин на дороге | готово (этап 3+4 одним коммитом) |
| 4 | Старые дома открытой части → новые участки «забор к забору», дом Тамары на главной улице, ФАП на Дәү урам у моста | готово: 44 участка + 5 перенесённых домов, улицы в `act1_open_part.world.v1.json`; ФАП и дом Тамары остались на месте (см. decision_log 2026-10-01) |
| 5 | Дом бабая и әби переезжает прямо на главную улицу вместе со двором | после 4 |

## 2. Историческая карта областей кода (не ограничивает права)

### W — мир и раскладка (Claude)

- `game/scripts/Act1ConnectedWorld.cs` и **все** `Act1ConnectedWorld.*.cs`, кроме
  `Act1ConnectedWorld.Vehicles.cs` (V) и `Act1ConnectedWorld.MechanicsLane.cs` (M).
- `AuthoredWorldDirector*.cs`, `AuthoredWorldPlot.cs`, `KitPlacementTakeover.cs`, `Act1WorldLayout.cs`,
  `Settlement*.cs`, `Address*.cs`, `AuthoredAddressText.cs`, `DebugVillageMinimap.cs`, `DebugWorldGrid.cs`,
  `SnowTrampleField.cs`, `WinterParticleSurfaces.cs`, `PainterlyEnvironmentDetails.cs`, `PrologueApproachRoad.cs`,
  `game/scripts/experiments/agent_b_act1/**`.
- Данные мира: `game/content/world/**`, `game/content/urman.settlement.addresses.v1.json`,
  `content/modules/urman-chapter1/address-navigation.json`.
- `tools/world/**`, `docs/production/village_relayout_*/**`, `docs/urman_knowledge_base/village_lore.md`.
- Тесты мира: `AddressWorldSmokeTest*`, `Act1TopDownCapture`, `Act1SuspensionBridgeSmokeTest`,
  `Act1FirstPersonWalkthroughSmokeTest` (в нём сейчас падает `view-arrival-message` — разбирает W).

### M — механики и мини-игры (друг)

- `game/scripts/Act1ConnectedWorld.MechanicsLane.cs` — существующая точка интеграции, где размещаются
  объекты в мире (метод `BuildMechanicsLane()` уже вызывается после транспорта).
- `YardMechanism*.cs`, `YardTool*.cs`, `YardUseTarget*.cs`, `CarryCoordinator*.cs`, `CarryableProp*.cs`,
  `OldPc*.cs` (компьютер, тетрис, чат, поиск), `ShopUi.cs`, `ShopCatalog.cs`, `game/content/urman.shop.v1.json`,
  `BathIgnitionChoiceUi.cs`.
- Любые **новые** файлы мини-игр: `game/scripts/Minigame*.cs`, `game/scenes/minigames/**`,
  `game/tests/Minigame*` + сцены тестов.
- Тесты: `act1_carry_interaction_smoke_test`, `act1_yard_mechanisms_smoke_test`, `old_pc_tetris_smoke_test`,
  `Act1NotebookShopSmokeTest`.

### V — вождение (друг)

- `Vehicle*.cs` (контроллер, колёса, коллизии, визуал, звук, радио, лошадь), `Act1VehiclePerformanceRoute.cs`,
  `Act1ConnectedWorld.Vehicles.cs`, `Act1DemoRoot.PrologueDriver.cs`, `Act1DemoRoot.PrologueNivaRide.cs`,
  `Act1DemoRoot.PrologueRideRadio.cs`, `Act1DemoRoot.DevRideCapture.cs`, `game/content/vehicles/**`.
- Исключение: **маршрут** поездки на Ниве идёт по дорогам W. Если W меняет дороги, W сам правит
  точки маршрута в `PrologueNivaRide.cs` (отдельным маленьким коммитом с пометкой в таблице).
  V меняет физику, управление, камеру, звук — не точки маршрута.
- Тесты: `vehicle_smoke_test`, `vehicle_radio_dial_smoke_test`, `act1_ride_route_check`
  (обязан печатать `ride-route: PASS`), `niva_art_capture`.

### T — текстуры и материалы (друг, ImageGen)

- `game/assets/textures/**`, новые файлы в `game/assets/generated/**`,
  `game/scripts/PainterlyMaterialLibrary.cs`, `RuralPropMaterials.cs`, `CivicSurfaceLibrary.cs`,
  `ClinicSurfacePresentation.cs`, тесты `texture_candidate_*`.
- Реестр и паспорта: `docs/urman_knowledge_base/art/texture_runtime_inventory_2026-09-22.md`,
  книга промптов `urman_imagegen_texture_prompt_book_2026-09-22.md`, паспорт табличек Заречья
  `docs/production/village_relayout_2026-10-01/far_bank_signs.md`.

- Политика ассетов из `AGENTS.md`: семейство объектов получает подходящую фактуру на настоящих UV;
  библиотека и варианты вместо сотен уникальных повторов.

## 5. Как запускать (обе машины)

- Сборка: `. eng/dotnet-env.sh && .tools/dotnet/dotnet build game/Urman.Game.csproj -v q --nologo`.
- Игра: `. eng/dotnet-env.sh && .tools/godot/Godot_mono.app/Contents/MacOS/Godot --path game`.
- Смоук: `URMAN_SMOKE_NATIVE=1 sh eng/run-smoke-guarded.sh res://tests/<сцена>.tscn` (страж сохранений;
  одновременно только один защищённый прогон на машине; не пересобирать DLL, пока он идёт).
- Кадры для проверки внешнего вида: `URMAN_VIEW_CAPTURE=<папка> URMAN_VIEW_POINTS="имя:x,y,z>tx,ty,tz;…"
  python3 eng/protected_run.py --clean --timeout 300 <godot> --always-on-top --path game res://scenes/act1_demo.tscn`
  (`--always-on-top` — иначе захват может зависнуть, когда окно перекрыто).
- Отладочное меню «Отладка: локации»: файл `debug-zones.enabled` в
  `~/Library/Application Support/Godot/app_userdata/URMAN/`.

## 6. Исторические запросы по раскладке

| Дата | От → кому | Файл / место | Что нужно | Статус |
|---|---|---|---|---|
| 2026-10-02 | W → V (сделано W) | `game/content/vehicles/act1_vehicles.v1.json` | стоянка Нивы бабая (−1.65, 1) → (−1.3, 17), западная обочина севернее двора (ворота, путь Алсу и перекрёсток свободны); мотоцикл (1.4, −23) → (2.2, −18): стоял в устье прямой улицы ФАП: двор бабая переехал на улицу, калитка на z ≈ 1.2; конец маршрута приезда в `PrologueNivaRide.cs` тоже | сделано, проверить |
| 2026-10-01 | W → T | `far_bank_signs.md` | вывески полиции, пекарни, молокозавода, лесничества (ImageGen, паспорт готов) | открыто |
| 2026-10-01 | V → W | дороги у Нивы бабая и ФАПа | Дать опорные точки проезжих маршрутов после перекладки: старый reverse Нивы остановился у X=1,87/Z=−2,83, ветка мотоцикла к ФАПу — у X=22,24/Z=−24,57 с «Здесь нет подходящей дороги». V обновит свои маршруты проверки по ответу. | открыто |
| 2026-10-01 | W → V | ответ на запрос выше | Дороги после этапов 3–4 — только из кода/данных, без ручных точек: главная `AgentBAct1Layout.MainRoadAxis` (прямая x≈0 от z 150 до −53,5), улица ФАП `FapBranchAxis` (прямая z −24 от главной до ворот ФАП x 28) + `BridgeApproachAxis` до моста, поперечные улицы — `Act1ConnectedWorld.OpenPartPlot().Roads`, проезд вокруг сквера — `PlazaDriveAxis`. Точка X=22,24/Z=−24,57 теперь лежит на улице ФАП; X=1,87/Z=−2,83 — на главной. | открыто (ждёт проверки V) |

## 2026-10-03 — продолжение performance-аудита

Автор дал полные права на все файлы; ожидание передачи W/общих файлов снято.
Уже внесённые perf-правки: RuntimeKernel snapshot cache, selected NPC rig/clip
branches, same-tick stationary horse contact reuse. Native High M4Pro 1080p
Debug пока 23.83 FPS; целевые M1 50–60 не достигнуты. Root ведёт рендеринг
и последовательные build/native runs; независимые помощники исследуют
batching/material reuse и owner-level runtime projection. О результатах —
`performance_audit_2026-10-03.md`.
