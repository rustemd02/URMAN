# Параллельная работа над УРМАНОМ — дорожки (с 2026-10-01)

Два исполнителя работают одновременно и не должны править одно и то же:

- **W — мир и раскладка** (Claude Code, автор ведёт сам): деревня, участки, площадь, овраг и мосты,
  дороги, снег, адреса и таблички, жители и их расстановка.
- **M — механики и мини-игры**, **V — вождение**, **T — текстуры и материалы (ImageGen)** — друг
  через GPT/Codex. Можно брать одну дорожку или несколько, но каждая правит **только свои файлы**.

Правило одно: **файл принадлежит одной дорожке**. Чужой файл не правится — вместо этого строка
в таблице запросов внизу. Общие файлы (раздел 3) меняются только по объявлению и только
дописыванием в конец.

## 1. Что сейчас делает W (чтобы M/V/T не опирались на то, что уедет)

План раскладки v3 (`docs/production/village_relayout_2026-10-01/plan.json`, `scheme.png`):

| Этап | Что меняется | Статус |
|---|---|---|
| 1–2 | Заречье (35 дворов, полиция, пекарня, молокозавод, лесничество); овраг с подвесным мостом вместо реки; снег и тропинки | готово, `e617148` |
| 3 | Центр: ДК, школа, мечеть, почта вокруг площади «Мәйдан»; сельсовет и магазин через главную улицу, магазин на дороге | готово (этап 3+4 одним коммитом) |
| 4 | Старые дома открытой части → новые участки «забор к забору», дом Тамары на главной улице, ФАП на Дәү урам у моста | готово: 44 участка + 5 перенесённых домов, улицы в `act1_open_part.world.v1.json`; ФАП и дом Тамары остались на месте (см. decision_log 2026-10-01) |
| 5 | Дом бабая и әби переезжает прямо на главную улицу вместе со двором | после 4 |

Следствие для M/V/T: **не зашивать мировые координаты** во дворе бабая, у ДК/школы/мечети/магазина
и в старой открытой части. Механику привязывать к узлу-якорю (имя узла или meta) или к своей
отдельной сцене, а точку в мире запрашивать у W строкой в таблице.

## 2. Владение файлами

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

- `game/scripts/Act1ConnectedWorld.MechanicsLane.cs` — единственная точка, где M добавляет свои
  объекты в мир (метод `BuildMechanicsLane()` уже вызывается после транспорта).
- `YardMechanism*.cs`, `YardTool*.cs`, `YardUseTarget*.cs`, `CarryCoordinator*.cs`, `CarryableProp*.cs`,
  `OldPc*.cs` (компьютер, тетрис, чат, поиск), `ShopUi.cs`, `ShopCatalog.cs`, `game/content/urman.shop.v1.json`,
  `BathIgnitionChoiceUi.cs`.
- Любые **новые** файлы мини-игр: `game/scripts/Minigame*.cs`, `game/scenes/minigames/**`,
  `game/tests/Minigame*` + сцены тестов.
- Тесты: `act1_carry_interaction_smoke_test`, `act1_yard_mechanisms_smoke_test`, `old_pc_tetris_smoke_test`,
  `Act1NotebookShopSmokeTest`.
- Новая мини-игра (например, карточная) — это **Proposal**, не канон: сначала одна запись в конце
  `docs/urman_knowledge_base/decision_log.md` (что, где в деревне, зачем в истории, связь с татарским
  языком), и только после «да» автора — код. Акт I, EX00–EX14 и 60 активных минут не заменяются
  мини-игрой; Тамара вне объёма.

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
- Договор с W: публичные методы `PainterlyMaterialLibrary.ForColor(color, surface)` и `ForPath(color)`
  и имена поверхностей (`snow_ground`, `snow_trampled`, `stone`, `wood`, …) не переименовываются и не
  удаляются; шейдерный флаг `soft_path_edges` (мягкие края троп) сохраняется. Менять внешний вид —
  можно и нужно, менять контракт — только через таблицу.
- Политика ассетов из `AGENTS.md`: семейство объектов получает подходящую фактуру на настоящих UV;
  библиотека и варианты вместо сотен уникальных повторов.

## 3. Общие файлы — только по объявлению, только дописывать

| Файл | Правило |
|---|---|
| `game/project.godot` | M/V могут **добавить** input action; объявить в таблице до коммита; существующие не трогать |
| `eng/verify-godot.sh` | список тестов — только добавлять строки своего теста в конец |
| `game/scripts/RuntimeBridge.cs`, `QuestRuntimeCoordinator.cs`, `CompiledCampaignRepository.cs`, `Main.cs`, `Act1DemoRoot.cs`, `FirstPersonController*.cs`, сохранения (`UserSettingsStore.cs`, `RuntimeBridge.LoadRecovery.cs`) | не править; нужна новая логика — **новый** partial-файл `RuntimeBridge.<Фича>.cs` / `FirstPersonController.<Фича>.cs` + строка в таблице |
| `content/campaigns/**`, `content/modules/**`, `game/content/urman.chapter1.compiled.v1.json` | правится источник в `content/`, compiled JSON пересобирается; при конфликте compiled JSON **не мерджить руками**, а пересобрать |
| `docs/urman_knowledge_base/decision_log.md`, `open_questions.md`, `backlog.md`, `mindmap.md` | только новые записи в конец |
| `AGENTS.md`, `canon.md`, `00_codex_context.md` | не трогать без автора |
| `graphify-out/**` | в ветках дорожек не коммитить; после слияния `graphify update .` делает тот, кто сливает |

## 4. Git

- `main` — интеграция. W коммитит в `main` маленькими шагами, перед каждым push — `git pull --rebase`.
- Друг работает в ветке своей дорожки: `lane/mechanics`, `lane/driving`, `lane/textures`.
  Каждый день и перед слиянием: `git fetch origin && git rebase origin/main`.
- Слияние в `main` (fast-forward или PR) — только если:
  1. `. eng/dotnet-env.sh; .tools/dotnet/dotnet build game/Urman.Game.csproj -v q --nologo` → `0 Error(s)`;
  2. свои тесты дорожки проходят (`URMAN_SMOKE_NATIVE=1 sh eng/run-smoke-guarded.sh res://tests/<сцена>.tscn`);
  3. `git diff --stat origin/main...HEAD` показывает только файлы своей дорожки плюс объявленные общие.
- Конфликт в чужом файле: берётся версия владельца, своя правка уходит запросом в таблицу.
- Удаление или переименование файла чужой дорожки — никогда.

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

## 6. Таблица запросов между дорожками (только дописывать)

| Дата | От → кому | Файл / место | Что нужно | Статус |
|---|---|---|---|---|
| 2026-10-01 | W → T | `far_bank_signs.md` | вывески полиции, пекарни, молокозавода, лесничества (ImageGen, паспорт готов) | открыто |
| 2026-10-01 | V → W | дороги у Нивы бабая и ФАПа | Дать опорные точки проезжих маршрутов после перекладки: старый reverse Нивы остановился у X=1,87/Z=−2,83, ветка мотоцикла к ФАПу — у X=22,24/Z=−24,57 с «Здесь нет подходящей дороги». V обновит свои маршруты проверки по ответу. | открыто |
| 2026-10-01 | W → V | ответ на запрос выше | Дороги после этапов 3–4 — только из кода/данных, без ручных точек: главная `AgentBAct1Layout.MainRoadAxis` (прямая x≈0 от z 150 до −53,5), улица ФАП `FapBranchAxis` (прямая z −24 от главной до ворот ФАП x 28) + `BridgeApproachAxis` до моста, поперечные улицы — `Act1ConnectedWorld.OpenPartPlot().Roads`, проезд вокруг сквера — `PlazaDriveAxis`. Точка X=22,24/Z=−24,57 теперь лежит на улице ФАП; X=1,87/Z=−2,83 — на главной. | открыто (ждёт проверки V) |

## 7. Промпт для друга (скопировать целиком в GPT/Codex)

```text
Ты работаешь над игрой УРМАН (Godot 4.7 .NET/C#, репозиторий URMAN). Параллельно в этом же
репозитории работает другой агент (Claude) — он ведёт мир и раскладку деревни. Чтобы не было
коллизий, ты работаешь строго в своей дорожке.

Сначала прочитай:
1. AGENTS.md (канон, ограничения; Тамара, Studio и следующие акты вне объёма);
2. docs/production/parallel_lanes_2026-10-01.md — там владение файлами, общие файлы, git-порядок,
   таблица запросов. Этот документ главнее любых старых «поручений» в AGENTS.md о раскладке деревни:
   раскладку, дороги, снег, адреса, участки и Act1ConnectedWorld.*.cs ведёт другой агент.

Моя дорожка: <M — механики и мини-игры | V — вождение | T — текстуры/ImageGen> (оставь нужное).
Задача на этот заход: <что сделать, критерий готовности>.

Правила:
- Правь только файлы своей дорожки из раздела 2. Нужна правка в чужом файле — не трогай его,
  допиши строку в таблицу раздела 6 (дата, от кого, файл, что нужно) и продолжай без неё.
- Общие файлы из раздела 3 — только дописывать и только с объявлением в таблице.
- Не зашивай мировые координаты во дворе бабая, у ДК/школы/мечети/магазина и в старой открытой части:
  эти места переезжают (раздел 1). Привязывайся к узлу-якорю или своей сцене; точку запроси у W.
- Механики в мире добавляй только через Act1ConnectedWorld.MechanicsLane.cs (BuildMechanicsLane()).
- Новая мини-игра — сначала Proposal одной записью в конец decision_log.md, код после «да» автора.
- Работай в ветке lane/<имя>; ежедневно и перед слиянием `git fetch origin && git rebase origin/main`.
- Перед слиянием: сборка без ошибок, тесты дорожки проходят, `git diff --stat origin/main...HEAD`
  содержит только твои файлы. graphify-out не коммить.
- Не выдавай технический прогон за художественную приёмку или плейтест; честно пиши, что проверено.

В конце захода отчитайся: что сделано, какие файлы изменены, какие тесты запускались и их итог,
какие запросы оставлены в таблице.
```
