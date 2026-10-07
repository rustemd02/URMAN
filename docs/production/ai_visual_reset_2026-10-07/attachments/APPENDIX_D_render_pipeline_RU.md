# B. Карта рендер-пайплайна УРМАН (Акт I, Godot 4 + C#)

Дата: 2026-10-07. Тип: техническая карта для внешней модели и Codex.
Источник: только файлы репозитория `/Users/unterlantas/Documents/GitHub/URMAN`.
Правило: то, что не найдено в репозитории, помечено «не найдено». Номера строк —
навигация по срезу, они не стабильные идентификаторы (искать по имени класса/метода).

Игровой проект: `game/` — Godot 4.7 + C#, точка входа `game/scenes/act1_demo.tscn`.
Веб-прототип в корне (`src/`, `public/`, `index.html`, `package.json`, `vite.config.ts`):
это **legacy-прототип студии** — Three.js/vite TypeScript-приложение (изометрическая
`src/MainMap/`, `src/os/`, виртуальная ОС ПК), **игрой не используется**: документально
признан «ЛИШНЯЯ/УСТАРЕВШАЯ реализация (legacy web)»
(`docs/production/urman_ttz_compliance_audit_2026-09-15.md:100`), никакой код в `game/`
его не грузит; Godot не может исполнять TypeScript (проверено поиском ссылок).

---

## 1. `game/project.godot` — рендер-настройки уровня проекта

Файл: `game/project.godot` (172 строки). Секция `[rendering]` содержит **ровно одну**
строку (строки 170–172).

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Главная сцена | `game/project.godot:14` | `run/main_scene="res://scenes/act1_demo.tscn"` — стартует демо Акта I | можно указать `full_game.tscn` |
| Рендер-бэкенд | не найдено в `project.godot` | ключ `rendering/renderer/rendering_method` отсутствует → действует дефолт движка **Forward+**; документально подтверждено: кадры сняты «из Godot/Forward+/Metal» (`docs/urman_knowledge_base/design_style.md:352`) | добавить `rendering/renderer/rendering_method="forward_plus"\|"mobile"` |
| Тени (глобально) | не найдено | в `project.godot` нет `rendering/lights_and_shadows/*`; всё теневое — в рантайме (`GraphicsQuality`) | можно вынести в проект, но текущий владелец — код |
| GI: SDFGI | не найдено | `Sdfgi` в `game/` не встречается ни в `.cs`, ни в `.tscn`, ни в `.godot` | — |
| GI: VoxelGI | не найдено | `VoxelGi` не встречается | — |
| GI: LightmapGI | не найдено | `LightmapGi`/`LightmapProbe` не встречается; в `.glb.import` стоит `meshes/light_baking=1`, но ни одной `LightmapGI`-ноды нет | — |
| SSAO | не найдено в проекте | SSAO живёт в `Environment` (см. §5, §9) | — |
| SSIL | не найдено | `SsilEnabled` не встречается | — |
| Туман (volumetric fog) | не найдено | объёмный туман нигде не включён; используется **depth fog** (`FogModeEnum.Depth`); документально: «объёмный туман… не добавлены» (`docs/urman_knowledge_base/design_style.md:705`) | — |
| Tonemap (проект) | не найдено в проекте | Tonemap задаётся на каждом `Environment` (AgX/Aces/Filmic) — см. §5, §9 | — |
| Antialiasing (проект) | не найдено в проекте | MSAA/FXAA задаются вьюпортом в `GraphicsQuality.Apply` | — |
| Физические материалы | не найдено | `PhysicsMaterial` / `PhysicsMaterialOverride` в `game/` не используются; трение авто считается вручную (`game/scripts/VehicleMechanics.cs:374-375`) | — |
| Качество текстур (импорт) | `game/project.godot:172` | единственная строка: `textures/vram_compression/import_etc2_astc=true` | можно менять VRAM-компрессию |
| Occlusion culling | не найдено | `occlusion_culling/*` в `project.godot` нет; `IgnoreOcclusionCulling` только читается в пилоте оконных групп (`game/scripts/Act1ConnectedWorld.WindowBatching.cs:75,176`) | добавить OccluderInstance3D + `occlusion_culling/use_occlusion_culling` |
| LOD (проект) | не найдено в проекте | LOD задаётся рантаймом: `viewport.MeshLodThreshold` (`game/scripts/GraphicsQuality.cs:44`) и `VisibilityRange*` на мешах | — |
| Виртуальный шрифт UI | `game/project.godot:34` | `gui/theme/custom_font="res://assets/fonts/PT_Sans-Web-Regular.ttf"` — единственная тема в проекте | можно подменить шрифт |
| Разрешение | `game/project.godot:22-26` | 1920×1080 viewport, окно 1280×720, `stretch/mode="canvas_items"` | окно/масштаб |
| Boot splash | `game/project.godot:16-17` | фон `Color(0.008,0.012,0.011,1)` и PNG `res://assets/textures/ui/act1_menu_winter_v1.png` | арт заставки |

**Вывод по §1:** «картинка» задана почти целиком в C#-коде в рантайме, а не в
`project.godot`. Проектный файл — тонкий.

---

## 2. Сцены: что где лежит, где Environment/свет/камера

### 2.1 Корневые сцены

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| `main.tscn` | `game/scenes/main.tscn:15-42` | `Node3D "Main"` + `RuntimeBridge` + `ZoneHost` + инстанс игрока + 5 UI-слоёв + аудио-директора. **Нет** WorldEnvironment, **нет** DirectionalLight3D, **нет** Camera3D | состав UI/нод |
| `act1_demo.tscn` | `game/scenes/act1_demo.tscn:1-6` | 6 строк: только `Node "Act1Demo"` со скриптом `res://scripts/Act1DemoRoot.cs`. Всё остальное строится кодом | — |
| `full_game.tscn` | `game/scenes/full_game.tscn:14-43` | `FullGameMain` (`Main.cs`) с `InitialZoneId="village_day"`, `InitialZoneId`/`CampaignResourcePath="res://content/urman.fullgame.compiled.v1.json"` | стартовая зона кампании |
| Камера | `game/scenes/player/first_person_player.tscn:17-24` | `CharacterBody3D` → `Head` (y=1.7) → `Camera3D` (`fov=75`, `near=0.05`, `far=160`), плюс `InteractionRay` (2.7 м) и `CanvasLayer "Hud"` (прицел + подсказка, :30-65) | FOV, near/far, позиция прицела |
| Слои отображения зон | `game/scripts/Main.cs:8-27` | карта `ZoneId → .tscn`; 17 зон (5 Акт I + 12 fullgame) | добавить/переставить зону |
| Сборка connected-мира | `game/scripts/Main.cs:77-79` | `EnableAct1ConnectedWorld` → создаёт `new Act1ConnectedWorld` | включение/выключение |

### 2.2 `scenes/zones/` — это заглушки-скрипты

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| `style_benchmark_day_street.tscn` | `game/scenes/zones/style_benchmark_day_street.tscn` (7 строк) | только `Node3D` + `StyleBenchmarkZone.cs` с `ZoneKind` | `ZoneKind` |
| `style_benchmark_house_pc.tscn` | тот же каталог | `ZoneKind = 2`-варианты интерьера дома/ПК | то же |
| `style_benchmark_kara_urman_night.tscn` | `game/scenes/zones/style_benchmark_kara_urman_night.tscn:1-7` | `ZoneKind = 2` (KaraUrmanNight) | то же |
| `chapter1_fap_clinic.tscn`, `chapter1_zirat_road.tscn` | `game/scenes/zones/` | такие же 7-строчные обёртки | то же |
| `fullgame_zone.tscn` + `zones/fullgame/*.tscn` | `game/scenes/zones/fullgame_zone.tscn:1-6`, `zones/fullgame/act2..act5*.tscn` | 18 файлов по 6–7 строк; вся геометрия — `FullGameZone.cs` / `FullGameZoneDressing.cs` | скрипты зон |
| Реальные позиции зон | `game/scripts/Act1WorldLayout.cs:20-26,38-41` | `ZonePlacement(ZoneId, …, Origin, Interior)`; `HouseOrigin=(-28,0,0)`, `FapOrigin=(28,0,-30)`, `ZiratOrigin=(0,0,-70)`, `KaraUrmanOrigin=(0,0,-115)` | мировые смещения зон |
| Система «одна зона Акта I» | `game/scripts/Act1ConnectedWorld.cs:1125-1210` | `BuildAct1CoreWorldGreybox()` создаёт `Act1CoreWorldGreybox` и 8 core-визуальных зон (`Arrival`, `MainStreet`, `BabaiEbiYard`, `HouseExteriorApproach`, `ConnectiveStreetReturn`, `FapExterior`, `ZiratMemoryField`, `KaraForestEdge`) | состав ядра |

### 2.3 Где именно Environment / свет / камера

**В `.tscn`-файлах игры нет ни одного `WorldEnvironment`, `DirectionalLight3D`,
`OmniLight3D`, `SpotLight3D` и `Camera3D`, кроме камеры игрока**
(проверено `grep` по всему `game/scenes/`). Всё создаётся в C#.

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Единственный наружный `WorldEnvironment` | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:2538-2589`, нода `AgentBEnvironment` (`:2586`) | создаёт `Sun` (`AgentBSun`) + `Sky` (`ProceduralSkyMaterial`) + `Environment`; монтируется в `Act1CoreWorldGreybox/AgentBExteriorWorld` | весь наружный свет/небо/туман |
| Монтирование слоя | `game/scripts/Act1ConnectedWorld.cs:1671-1679` | `BuildAgentBExteriorWorld()`: `new AgentBAct1ExteriorLayer{Name="AgentBExteriorWorld"}` → `core.AddChild` → `layer.Build()` | — |
| Внутренние `WorldEnvironment` зон | `game/scripts/StyleBenchmarkZone.cs:97-166` (создание `:125-166`, `AddChild` на `:166`) | по зоне: `Sky`+`ProceduralSkyMaterial` (`:104-124`), `Environment` (`:125-164`), `GraphicsQuality.ConfigureEnvironment` (`:165`) | интерьерные профили |
| Солнце зон-бенчмарков | `game/scripts/StyleBenchmarkZone.cs:170-185` | `new DirectionalLight3D{Name="MainDirectionalLight", LightEnergy=…, ShadowEnabled=true}` | энергия/углы бенчмарк-зон |
| Полный список Environment | `game/scripts/Act1ConnectedWorld.cs:384-390` | `FindDescendants<WorldEnvironment>(zone)` → `_environmentsByZone`; при переключении зоны ресурс либо отдаётся, либо обнуляется (`:591-602`) | селектор активного профиля |
| Полный список светов | `game/scripts/Act1ConnectedWorld.cs:391-395` | `FindDescendants<Light3D>(zone)` → `_lightsByZone`; включается только свет активной зоны, а наружное солнце никогда не дублируется (`:604-626`) | правила включения |
| FullGame зоны | `game/scripts/FullGameZone.cs:41-140` | свой `WorldEnvironment` (`:49-75`), `KeyLight` (`:77-83`), `LocalStoryLight` (`:132-137`) | свет последующих актов |
| Камера | `game/scenes/player/first_person_player.tscn:20` | одна `Camera3D` на `Head/Camera3D`; код берёт её через `GetNode<Camera3D>("Head/Camera3D")` (`game/scripts/Act1DemoRoot.cs:964,1022`) | FOV/позиция |

---

## 3. Как строится мир в рантайме

Основной владелец — `Act1ConnectedWorld` — это **90 partial-файлов, 30 759 строк**
(`game/scripts/Act1ConnectedWorld*.cs`). Точка входа сборки: `Build()`
(`game/scripts/Act1ConnectedWorld.cs:~340-535`), список шагов — `ProfileWorldBuildStep(...)`
(`:407-469`).

### 3.1 Источники мешей

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| 5 kit-ов Agent B | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:15-24` | `agentb_terrain_road_kit.glb`, `agentb_village_buildings_kit.glb`, `agentb_foliage_kit.glb`, `agentb_zirat_kit.glb`, `agentb_kara_edge_kit.glb` из `res://assets/models/agent_b_act1/` | сами `.glb` и список |
| Инстанцирование + перекраска | `game/scripts/experiments/agent_b_act1/AgentBKitMaterials.cs:101-142` | `InstantiateKit()` грузит `PackedScene`, `RebindMaterials()` подменяет каждый материал с именем `AB_*` (`:119-142`); кит обязан быть без коллизий (`:110-114`) | карта имён → материалы |
| Импортированные киты Акта I | `game/scripts/Act1ConnectedWorld.cs:15-128,3773` | `urman_village_exterior_kit.glb`, `urman_fap_clinic_kit.glb`, `urman_zirat_roadside_kit.glb`, `urman_kara_forest_edge_kit.glb`, `urman_wet_village_road_kit.glb` | `.blend`-источники в `assets/source/blender/` |
| Сборка из отдельных компонентов | `game/scripts/Act1ConnectedWorld.cs:65-77,1671-1674` | `VillageExteriorKitComponentNames` — имена компонентов кита (`DwellingFacade_TimberPlaster`, `OutbuildingShed_Low`, `FenceSegment_RoughPicket`, `Gate_CrookedTimber`, `Woodpile_StackedLogs`, `Well_YardLandmark`, `VillageParcel_VariantA/B/C`) | список компонентов |
| Generic участки из данных | `game/scripts/AuthoredWorldDirector.cs:23-24,73-104,213-341` | читает `res://content/world/*.world.v1.json` (7 файлов) + `catalog.v1.json`; `BuildVisual` (`:213`), `BuildScatter` (`:294`), `BuildCollision` (`:342`) | JSON-плоты и каталог |
| Процедурные примитивы (заглушки/достройка) | `docs/urman_knowledge_base/art/primitive_surface_audit_2026-09-29.json` → 461 callsite | 71 `CylinderMesh`, 54 `BoxMesh`, 35 `SphereMesh`, 15 `QuadMesh`, 6 `TorusMesh`, 4 `CapsuleMesh`, 3 `PrismMesh`; топ-файлы: `Act1ConnectedWorld.SquareInteriors.Club.cs` (59), `…School.cs` (49), `…SovkhozSquare.cs` (41), `…SquareInteriors.cs` (40), `PrologueDeepForest.cs` (28) | заменить примитивы на ассеты |
| Дома (генерируются данными) | `game/scripts/Act1ConnectedWorld.AuthoredWorld.cs:15-38` | `AuthoredWorldDirector.Build(...)`; дополнительные generic-плоты: open_part A17/B17/C10 и far_bank A11/B12/C12 = **79 parcel placements** (`docs/production/performance_handoff_2026-10-07.md:323-330`) | JSON-плоты |
| Подавление дублей/блокбаутов | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:853-1274` | 10 методов `Suppress*` (FAP, дом бабая, A7/A5/far house, веранды, колодец, крылья Кара) | списки подавления |
| Скрытие core-блокбаутов | `game/scripts/Act1ConnectedWorld.cs:1640-1662` | `HideCorePresentationNode(...)` ×20, `coreBlockoutBuildingSuppressionCount=20` | список вытесненных примитивов |

### 3.2 Дороги, заборы, снег

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Дороги | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:360-473` (`ConformRoadPresentation`) | дорожное полотно берётся из heightfield `AgentBAct1HeightField.RoadInfo(x,z)`, не из отдельного меша | профиль дороги в `AgentBAct1HeightField.cs` |
| Подавление дублирующего декора дороги | `game/scripts/Act1ConnectedWorld.cs:1681-1733` | `SuppressAgentBOverlappingRoadDecor`: скрывает `MudStrip_*`, `Rut_*`, `Ditch_*` (обязательные имена проверяются, `:1686-1703`) | список скрытых семейств |
| Снежные валы вдоль улицы | `game/scripts/Act1ConnectedWorld.cs:8420-8520+` (`AddMainStreetSnowBanks`) | проход по z от 16 до −84 с шагом 1 м, поиск кромки дороги по x шагом 0.25 м; непрерывные валы высотой `.3+.08*sin(z*.31+side)` самплируются каждые ~4 точки и публикуются как `AddVisualLandformSurface(..., "e8edf0", "snow_ground")`; вызов — `:467` | высота/шаг/цвет валов |
| Снег под постройками | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:474-604` | `ExcludeOccupiedRoomTerrain` / `RecutOccupiedRoomTerrain` / `ClipGroundFootprint` — вырезает снег под домом ФАП и домом бабушки | геометрия выреза |
| Снегопад | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:2609-2636` (`BuildSnow`) | один `CpuParticles3D "AgentBSnow"`: `Amount=4000`, `Lifetime=3.0`, `Gravity=(0,-0.32,0)`, `Randomness=.55`, `ScaleAmount 0.7–1.5`, quad `WinterParticleSurfaces.Snow(.026f,.50f,roomExclusion:true)` | число/скорость частиц |
| Метель | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:2595-2607` | `SetOpeningBlizzard(bool)`: направление/скорости 11–16 м/с, `Preprocess` | интенсивность шторма |
| Снежный след игрока | `PainterlyMaterialLibrary.SetSnowTrample` (`game/scripts/PainterlyMaterialLibrary.cs:501-530`) + uniform `trample_map` в шейдере | маска следов пишется в runtime `ImageTexture` и раздаётся материалам с `trample_ground_surface=true` | разрешение/радиус маски |
| Тропы | `PainterlyMaterialLibrary.ForPath` (`:696-706`) | материал `snow_trampled` + `soft_path_edges=true`, тональность подтянута к `.85,.88,.92` | тон/мягкость края |
| Окна-эмиссия | `game/scripts/VillageWindowMaterials.cs:32-51` | на материал — `lamp_energy` 0.72 днём / 1.20 ночью | цвет/энергия |
| Зимние ветки/ёлки (переиспользуемые) | `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:82-88` | `FoliageMesh()` грузит `res://assets/models/act1/urman_winter_pine.glb` (хвойные) либо `urman_winter_dead_tree.glb` (лиственные) | сами `.glb` |

### 3.3 Деревья и растительность — где генерируются

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Авторский список посадок | `game/scripts/experiments/agent_b_act1/AgentBFoliagePlan.cs:29-147` | 59+ записей `(Vector2 position, string variant)`: `Birch_*`, `Spruce_*`, `Shrub_*`, `Fern_*`, `Sedge_*`, `GrassTuft_*`, `Stump_0`, `MossStone_0` | позиции/породы |
| Процедурное размножение | `AgentBAct1ExteriorLayer.cs:1794-1861` (`BuildDensifiedPlan`) | на каждую авторскую точку 2–4 «спутника» (`RandomNumberGenerator{Seed=20260910}`), затем сетка 2.2 м вдоль дорог (`:1873+`) | seed, число сателлитов, шаг |
| Лесное кольцо | `AgentBAct1ExteriorLayer.cs:2108-2143` | **9 рядов** (`beltRows=9`) вокруг прямоугольника `ForestRingInnerMin/Max` (`:2362-2366`), шаг 4.0 м для рядов 0–3 и 3.4 м дальше | глубина/плотность кольца |
| Состав кольца | `AgentBAct1ExteriorLayer.cs:2400-2479` (`EmitBelt`) | на точку 3 растения: дерево (`WinterSpruce_3..6`, `WinterBirch_1/2`, `WinterLinden_1`, `WinterMaple_1`) + 2 подлеска (`Fern_1`, `Sedge_1`, `Shrub_1`, `GrassTuft_1`, `WinterSpruce_1/2`) | пропорции пород |
| Сборка меша варианта | `AgentBAct1ExteriorLayer.cs:1301-1365` (`Geometry`) | все части шаблона сливаются через `SurfaceTool` по группам `bark/snow/foliage/berries/stone`, затем кэшируются по варианту | группировка поверхностей |
| Региональная перекраска | `AgentBAct1ExteriorLayer.cs:1711-1732` (`RegionalFoliageMaterial`) + `:8-15` палитра | 5 цветов хвои (`26372f`, `2f4438`, `354b3f`, `3a4b3d`, `30483f`) по регионам `village/zirat/kara` | палитра |
| Батчинг напочвенного покрова | `AgentBAct1ExteriorLayer.cs:1610-1625` | `MultiMeshInstance3D` по ячейке **12 м** × вариант × регион × LOD | размер ячейки |
| Коллизии стволов | `AgentBAct1ExteriorLayer.cs:1564-1608` | `ConcavePolygonShape3D` из bark-поверхностей только для видимых крупных деревьев внутри кольца | порог высоты `3.5f` |
| Блокировка под крышей | `AgentBAct1ExteriorLayer.cs:34-78` | `BuildingRoofBounds` / `UnderBuildingRoof` / `ReconcileBuildingFoliage` — убирает посадки под крышами | — |
| Метаданные для диагностики | `AgentBAct1ExteriorLayer.cs:1632-1645` | `plannedFoliageEntryCount`, `plantedFoliageNodeCount`, `createdPlantedStemCollisionCount`, `minimumFoliageRoadClearance` и др. | — |

**Отдельные деревья Кара-Урмана:** `game/scripts/Act1ConnectedWorld.cs:6474,6508,6548,6575,6618-6624`
(`AddAuthoredWinterTree(..., "WinterPine")`), источник — `urman_winter_pine.glb`.

---

## 4. Материалы и шейдеры

**.gdshader-файлов в репозитории — 0; `.tres`/`.res`-материалов — 0.** Все шейдеры
объявлены как C#-строки (`raw string literal`) и собираются в `Shader` в рантайме.
Это ключевой факт для художника: правка шейдера = правка `.cs`.

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| **Главный «painterly» шейдер** | `game/scripts/PainterlyMaterialLibrary.cs:31-348` (`ShaderSource`) | `shader_type spatial; render_mode diffuse_burley, specular_schlick_ggx`; vertex-блок вынесен в `VertexDeformation` (`:7-29`) | весь вид поверхности |
| — triplanar | `PainterlyMaterialLibrary.cs:110-118` (`triplanar_albedo`) | `blend = pow(abs(normal), 4)` нормируется, 3 выборки альбедо (X/Y/Z) | степень blend, scale |
| — macro-variation | `PainterlyMaterialLibrary.cs:131-139` (`cell_tint`) + `:241-256` (`macro_pigment`, `macro_stain`) | клеточный тинт по `floor(p.xz/6)` и крупный шум | `cell_jitter`, размер клетки 6 м |
| — detail-текстура | `PainterlyMaterialLibrary.cs:178-181` | по UV `albedo_texture` добавляется как detail к `base_color` (два режима яркости) | `texture_strength` |
| — snow blanket | `PainterlyMaterialLibrary.cs:186-191`, `:284-293` | `snow_cover = smoothstep(0.30..0.75, world_normal.y) * snow_coverage`; смешивает albedo→`snow_color` и roughness→0.92 | `snow_coverage` по семействам (`:842-858`) |
| — snow micro / рельеф | `PainterlyMaterialLibrary.cs:8-17` (`VertexDeformation`) | `snow_micro_response.r` даёт `±0.004` вертикального смещения + `snow_micro_normal` | `snow_relief_scale` (`:973-976`) |
| — искры снега | `PainterlyMaterialLibrary.cs:306-316` | view-dependent micro-glints: `SPECULAR += grain*0.08`, roughness −`grain*0.04` | `snow_sparkle` (`:826-840`) |
| — trample (следы) | `PainterlyMaterialLibrary.cs:18-21`, `:142-150`, `:318-343` | `snow_trample_at(world_position)` двигает VERTEX и темнит/сплющивает альбедо | маска и extent |
| — ветер (листва) | `PainterlyMaterialLibrary.cs:22-28` | `gust` из `sin(TIME*1.6 + x*0.55 + z*0.4)`, амплитуда `wind_sway * max(VERTEX.y,0)` | `wind_sway` (`:928-935`), глобально `SetWindMotion` (`:532-547`) |
| — edge frost | `PainterlyMaterialLibrary.cs:161-176` | ветка `edge_frost=true` для `frost_window`: `ALBEDO=mix(base_color, frost.rgb, frost.a)`, roughness 0.30…0.86 | — |
| — cut wood end | `PainterlyMaterialLibrary.cs:228-239` | годовые кольца на торцах дерева (процедурно, `sin(length(grain)*33)`), с `fwidth`-затуханием | `cut_wood_end` |
| — прозрачность листвы | `PainterlyMaterialLibrary.cs:345` | `BACKLIGHT = ALBEDO * leaf_transmission` | `leaf_transmission=0.45` для `foliage`/`leaf_birch` (`:927`) |
| Вариант «rigid» | `PainterlyMaterialLibrary.cs:355-369`, выбор `:1010-1015` | если VERTEX-блок гарантированно нулевой (`wind_sway==0`, нет snow micro, нет trample) — шейдер без деформации: это условие попадания в общий shadow material движка | условие выбора |
| Вариант two-sided | `PainterlyMaterialLibrary.cs:370-377` | `cull_disabled` + `FRONT_FACING`-коррекция нормали для снега | — |
| Вариант cutout | `PainterlyMaterialLibrary.cs:378-384` | `ALPHA = texture(cutout_texture, UV).a`, `ALPHA_SCISSOR_THRESHOLD = 0.2` | — |
| Таблица текстур-альбедо | `PainterlyMaterialLibrary.cs:386-458` (`SurfaceTextures`, 34 записи) | семейство → `(PNG-путь, Vector2 scale)`; пример `wood_facade` → `urman_w01_v01_basecolor.png` | пути и повтор |
| Зимние альбедо | `PainterlyMaterialLibrary.cs:464-476` (`WinterTextures`, 10 записей) | подключаются, только если PNG существует (`:947-958`) | пути |
| Фабрика материалов | `PainterlyMaterialLibrary.cs:708-1019` (`ForColor(htmlColor, surface, sheltered)`) | кэш `surface:color:sheltered`; на семейство задаются `brush_scale` (`:755-776`), `variation` (`:777-796`), `texture_strength` (`:797-822`), `snow_sparkle` (`:826-840`), `snow_coverage` (`:842-858`), `cell_jitter` (`:859-876`), `roughness/specular/wet_grade` (`:880-926`) | любая ручка поверхности |
| Производные фабрики | `PainterlyMaterialLibrary.cs:591-706` | `ForCutout`, `PreserveSourceCulling`, `ForMovingCloth`, `ForLocalWoodPiece`, `WithoutSnow`, `Sheltered`, `ForPath` | — |
| Глобальные переключатели материалов | `PainterlyMaterialLibrary.cs:491-567` | `SetSnowTrample`, `SetWindMotion`, `SetGraphicsPreset` (`low_quality` — одна выборка вместо трипланара) | — |
| **Шейдер окон** | `game/scripts/VillageWindowMaterials.cs:8-27` | `frost_map` + `room_color`; `EMISSION = room_color*folds*lamp_energy*(1-ice*0.7)`; без blend-режима → **окно непрозрачно** (подтверждено комментарием `game/scripts/WindowSilhouettes.cs:12-14`) | энергия/цвет/мороз |
| Палитра AB_* → painterly | `game/scripts/experiments/agent_b_act1/AgentBKitMaterials.cs:14-55` | 40 записей вида `["AB_log_wall"] = ("7a674e","log_wall",0f)` | весь вид китов Agent B |
| Материалы окна тёплого/холодного | `AgentBKitMaterials.cs:47-48` | `AB_window_warm` → `StandardMaterial3D` c `EmissionEnergyMultiplier=3.0`; `AB_window_cold` — эмиссии нет | — |
| Материалы персонажей | `game/scripts/GeneratedCharacterKitDressing.cs:252-362` (процедурные), `:364-407` (human-v2) | лицо/глаза — `StandardMaterial3D` с `DiffuseMode=Toon`, `SpecularMode=Disabled`, `Roughness=1`, `Metallic=0` (`:274-279`, `:390-396`); одежда — `PainterlyMaterialLibrary.ForColor` | палитры `:254-266`, `:290-308` |
| Parallax occlusion mapping | не найдено | uniform-ов parallax/height-map в шейдере нет | — |
| Height/mask-блендинг | частично | есть `snow_cover` через `world_normal.y` (`:188`) и `cell_tint` — но **масочного бленда двух текстур по маске нет** | можно добавить |
| ORM/MRA-пакеты | не найдено | используется только один `sampler2D albedo_texture` + процедурные roughness/specular | — |
| Vertex-color blending | не найдено (есть только uniform `vertex_pigment` для `terrain`/`wet_road`/`snow_road`, `:47`, `:749`) | — | — |
| Именование surface-семейств | `PainterlyMaterialLibrary.cs:719-741` | `finishSurface`-алиасы: `wood_painted_blue/green/trim/wood_log_uv → wood_facade`; `stone_foundation → stone`; `cloth_* → cloth` | — |

---

## 5. Свет: солнце, небо, туман, ночь, окна, фары

### 5.1 Авторские данные освещения — главный файл правок

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Данные профилей | `game/content/world/atmosphere.v1.json:8-169` | 3 профиля: `village-winter-frost` (зоны `village_day`, `house_old_pc`, `fap_clinic`), `zirat-winter-muted` (`zirat_road`), `kara-winter-night-edge` (`kara_urman_night`) | **все параметры света/неба/тумана** |
| Загрузчик | `game/scripts/AtmosphereProfiles.cs:20,29-35,37-55` | `Path="res://content/world/atmosphere.v1.json"`; отсутствие профиля — ошибка, не дефолт (`:34`) | схема записи |
| Применение профиля | `game/scripts/Act1ConnectedWorld.cs:1751-1825` (`TuneConnectedAct1Atmosphere`) | выбирает профиль по зоне (`:1772`), пишет в `Environment` (`:1774-1797`), в `ProceduralSkyMaterial` (`:1800-1809`), в `AgentBSun` (`:1811-1820`) | логика выбора |
| Диагностический дамп | `game/scripts/AtmosphereDump.cs:5-20` | при `URMAN_ATMOSPHERE_DUMP=<file>` пишет фактически применённые значения (вызов `Act1ConnectedWorld.cs:1824`) | — |

### 5.2 Конкретные значения

| Параметр | village-winter-frost | zirat-winter-muted | kara-winter-night-edge |
|---|---|---|---|
| ambient energy / color / skyContribution | 0.85 / `7d90b1` / 0 | 0.36 / `7d90b1` / 0 | 0.28 / `72899f` / 0 |
| fog color / density / height / heightDensity | `4b5f76` / 0.03 / 1.0 / 0.008 | `44566b` / 0.032 / 1.0 / 0.008 | `354955` / 0.026 / 1.0 / 0.008 |
| fog aerial / skyAffect / sunScatter | 0.65 / 0.6 / 0.025 | 0.55 / 0.6 / 0.025 | 0.55 / 0.6 / 0.025 |
| exposure | 1.0 | 1.0 | 0.92 |
| SSAO intensity / radius | 0.75 / 0.4 | 0.75 / 0.4 | 0.75 / 0.4 |
| sky top / horizon / groundHorizon / groundBottom | `263346` / `556b80` / `485a6d` / `263346` | `263346` / `4b6074` / `3f5062` / `263346` | `1b2836` / `3c4c60` / `2c3a4a` / `141d28` |
| sunAngleMax / sunCurve | 0 / 0.12 | 0 / 0.12 | 0 / 0.12 |
| sky cover RGBA | `.29,.38,.5,.86` | `.29,.38,.5,.86` | `.24,.32,.41,.75` |
| sun color / energy / shadowOpacity / rotation (X,Y) | `c6d4e2` / 0.48 / 0.18 / (−16, 18) | `c6d4e2` / 0.55 / 0.45 / (−16, 18) | `9fb6d4` / 0.22 / 0.52 / (−52, −28) |

Источник значений: `game/content/world/atmosphere.v1.json:20-61,73-114,126-167`.
Исходный «заводской» `Environment` создаётся в
`AgentBAct1ExteriorLayer.cs:2538-2589`: `LightColor=c7d3d1`, `LightEnergy=1.04`,
`ShadowOpacity=.30`, `RotationDegrees=(-48,32,0)`, `TonemapMode=Aces`,
`TonemapExposure=.98`, `FogDensity=.0034`, `FogAerial=.54`.

### 5.3 Остальной свет

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Небо | `AgentBAct1ExteriorLayer.cs:2550-2567` | `ProceduralSkyMaterial` + процедурная «облачная плёнка» `CreatePainterlySkyCover()` (`:2690-2732`, цилиндрическая выборка, fBm-полосы) | цвета/cover |
| Тени солнца | `game/scripts/GraphicsQuality.cs:138-144` (`ConfigureSun`) | `low`: `Parallel2Splits`, 45 м; `medium`: `Parallel4Splits`, 80 м; `high`: 4 сплита, 120 м | разделение — только через пресет |
| Тени: атласы и фильтр | `game/scripts/GraphicsQuality.cs:30-60` | `low`: .7 scale, MSAA off, LOD 4.0, atlas 2048/1024, `Hard`; `medium`: .9, MSAA2X, LOD 1.5, 4096/2048, `SoftLow`; `high`: 1.0, MSAA4X, LOD 1.0, 4096/4096, `SoftMedium` | — |
| Ночной режим | `Act1ConnectedWorld.cs:652-669` | `isKaraNight` → профиль `kara-winter-night-edge`, `AuthoredWorld.SetAmbientNight(true)` (`:668`), `UpdateOrdinaryNpcNightPresence` (`:669`) | порог ночи |
| Окна (эмиссия) | `game/scripts/VillageWindowMaterials.cs:32-51` | `lamp_energy` 0.72 → 1.20 ночью (`SetNight`, `:45-51`); используется `frost_window_v1_albedo.png` | энергия/цвет |
| Свет из окон (spill) | `game/scripts/VillageHouseholdDirector.cs:10,95-102,185-199` | пул **6** `SpotLight3D` (`SpillBudget=6`), `LightColor=ffd3a1`, `LightEnergy=.75` (ночью `1.25`), `SpotRange=4.9`, `SpotAngle=65°`, `SpotAttenuation=1.6`, тени вкл., `LightSize=.12`, `ShadowBias=.035`; выбор ближайших окон на 4 Гц | бюджет/угол/энергия |
| Силуэты в окнах | `game/scripts/WindowSilhouettes.cs:38-63` | пул 10 (ночь) / 6 (день) quads, 5 общих `ArrayMesh` (32 треугольника), без теней и текстур, `Transparency`-fade | бюджеты/дистанции |
| Свечение окон в китах | `AgentBKitMaterials.cs:47-48,75-90` | `AB_window_warm` → `EmissionEnabled`, `EmissionEnergyMultiplier=3.0` | — |
| Фары автомобиля | `game/scripts/VehicleVisualFactory.cs:241-248` | `SpotLight3D "HeadlightN"`, `LightColor=(1,.84,.57)`, `LightEnergy=2.1`, `SpotRange=26`, `SpotAngle=29`, `RotationDegrees=(-4,0,0)` (пара: x=±0.60, y=1.01, z=−2.04; `:80`) | дальность/угол |
| Дальний свет | `game/scripts/VehicleVisualFactory.Cabin.cs:99-103,311-322` | `HighBeamEnergyFactor=1.55`, `HighBeamRangeFactor=2`, `HighBeamAngleDegrees=27`, цвет `(1,.9,.72)` | — |
| Кара-ночные акценты | `AgentBAct1ExteriorLayer.cs:2638-2681` | 3 `OmniLight3D` с `baseEnergy` .070/.055/.045 и `OmniRange` 16/14/13, штатно `LightEnergy=0` и `Visible=false` | энергии |
| Портативный фонарь | `game/scripts/PortableLight.cs:9-16` | реальный `OmniLight3D` на переносимом предмете, `Reach=3.4` — только проверка освещённости детали | радиус |
| Свечи/лампы интерьеров | `game/scripts/StyleBenchmarkZone.cs:552-605` (дом/ПК): `f0d5ae` 1.70, `92b0d3` .68, `dfc7a6` 1.0, `78a59a` (CRT) .78, `c2a375` .72; `:921-959` (ФАП): `a4b1b1` .42, `7899a2` .44, `d5ad78` 1.42, `cda47c` .90 | — |
| Всего мест создания источников | `grep OmniLight3D\|SpotLight3D` → **24 файла, 59 вхождений** | крупнейшие: `StyleBenchmarkZone.cs`, `Act1ConnectedWorld*.cs`, `FullGameZone.cs`, `VehicleVisualFactory*.cs` | — |

---

## 6. Персонажи: модели, скелет, клипы, морфы, руки

### 6.1 Источники моделей

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Кит персонажей v1 | `game/assets/generated/urman_character_kit.glb` (4.58 МБ) | 633 glTF nodes, 542 meshes, 9 skins, 18 animations | — |
| Кит персонажей v2 («human») | `game/assets/generated/urman_character_kit_v2.glb` (32.81 МБ) | 799 nodes, 196 meshes, 9 skins, 36 animations | — |
| Blender-источники | `assets/source/blender/urman_character_kit.blend`, `…_kit_v2.blend` | исходники кита | форма/UV/скелет |
| Головы | `assets/source/blender/characters/tamara_aged_head.blend`, `urman_cc0_head_templates.blend` + `Quaternius_CC0_License.txt` | CC0-производные голов | лица |
| Анимационная библиотека | `game/assets/animations/ual1_standard.glb` (7.27 МБ) | Quaternius **Universal Animation Library, CC0** (`AnimationCatalog.cs:22`) | сама библиотека |
| Выбор кита по префиксу | `game/scripts/GeneratedCharacterKitDressing.cs:12,20-24` | `HumanPrefixes = {Mansur, Gulsina, Naila, TimurHazrat, Alsu, Rinat, Resident, Tamara, PhoneGuy}` → v2; остальные → v1 | список префиксов |
| Инстанс и обрезка чужих ригов | `GeneratedCharacterKitDressing.cs:47-112` | `packed.Instantiate<Node3D>()` **целого** кита, затем `Free()` всех `_Rig`/`_Anchor` кроме нужного префикса (`:69-76`) и обрезка animation-библиотек (`:77-103`) | можно оптимизировать (см. §10, G11) |
| Импортные настройки персонажей | `game/assets/generated/urman_character_kit_v2.glb.import:19-32` | `meshes/generate_lods=true`, `meshes/create_shadow_meshes=true`, `skins/use_named_skins=true`, `animation/fps=30`, `animation/import=true`, `materials/extract=0` | LOD/shadow/animation import |
| Скелет | `game/scripts/AnimationCatalog.cs:14-17,205-215` | общий скелет кита — **65 костей**, имя схемы `urman-ue65`; путь скелета выводится из первой track-пути клипа `{prefix}_Idle` (`:205-215`) | скелетный контракт |
| Выбор мешей | `GeneratedCharacterKitDressing.cs:114-150` | берутся только `{prefix}_*` с `_LOD0`/`_LOD1`; обязательны оба LOD, иначе — исключение (`:146-150`) | имена мешей |
| LOD-дистанции персонажей | `GeneratedCharacterKitDressing.cs:32-37` | LOD0 0–18 м (fade-margin 2), LOD1 14–48 м (margin 2/4); `VisibilityRangeFadeMode=Self` (`:231-250`) | дистанции |

### 6.2 Клипы и как они применяются

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Данные каталога движений | `game/content/animations/catalog.v1.json:8-505` | **29 движений** (`urman.anim:*`), категории: Ожидание, Реакция на опасность, Ходьба и бег, Разговор и жесты, Подбор и перенос, Сидение, Работа и взаимодействие, Эмоции | добавить/сменить движения |
| Тип источника | `catalog.v1.json` поля `source.kit` \| `source.library`+`clip` \| `source.procedural` | `kit` — клип самого кита (`Idle`, `Tension`, `Walk`, `Talk`); `library ual` — клип Quaternius UAL; `procedural: "turn"` — без клипа (`:167-181`) | — |
| Загрузчик каталога | `game/scripts/AnimationCatalog.cs:21,69-72,235-241` | `CatalogPath="res://content/animations/catalog.v1.json"`, парсинг `entities` | — |
| Проигрывание kit-клипа | `AnimationCatalog.cs:100-107` | `player.Play($"{prefix}_{clip}", blend, speed)` + установка `LoopMode` | — |
| Ретаргет библиотечного клипа | `AnimationCatalog.cs:109-169` | клип дублируется, для каждой track: если кости нет в скелете — track удаляется, если кость `root` + `Position3D` — тоже удаляется (один владелец позиции); >8 отсутствующих костей → отказ (`:155-158`); иначе путь переуказывается на `{skeletonPath}:{bone}` (`:152`) | фильтрация костей |
| Кэш игрока кита | `AnimationCatalog.cs:29-67` | `ConditionalWeakTable<Node3D, KitPlayer>`, валидация `HasAnimation($"{prefix}_Idle")` | — |
| Совместимость без проигрывания | `AnimationCatalog.cs:171-184` (`Compatibility`) | считает отсутствующие кости клипа | — |
| Клипы кита v1 | `GeneratedCharacterKitDressing.cs:170-179` | `Idle` играется автоматически при `Attach` (`:179`); для v2 принудительно `LoopMode=Linear` для `Idle/Tension/Talk/Walk` (`:175-177`) | — |
| Смена клипа | `GeneratedCharacterKitDressing.cs:210-229` (`PlayClip`) | `player.Play($"{prefix}_{clipSuffix}", blend)` + метаданные | — |
| Анимация трупов/мелких NPC | `game/scripts/TamaraFenceQuest.cs:80,691-692,1426`, `PolicePostPresentation.cs:33` | поиск `AnimationPlayer` через `FindChildren` | — |
| Морфы / BlendShapes | **не найдено** | ни `SetMorph`, ни `Morph`-API; наоборот, код **требует** `GetBlendShapeCount()==0` (`game/scripts/Act1ConnectedWorld.WindowBatching.cs:209`, `…MosqueClothing.cs:32,73`, `…MosqueImamDress.cs:78`, `…PublicFenceJunction.cs:135`) | — |
| Лицевая анимация / lipsync / viseme | **не найдено** | в коде нет; в метаданных NPC прямо: «expression/audio polish open» (`game/scripts/FullGameNpcDressing.cs:27,49`) | — |
| Лицо как геометрия | `GeneratedCharacterKitDressing.cs:269-289` | лицо/глаза — отдельные меши `{prefix}_Head_*`, `{prefix}_FaceEyes_*` с CC0-альбедо; `Mansur` дополнительно получает `assets/textures/characters/mansur_age_v1_albedo.png` (`:282-285`) | текстуры лиц |

### 6.3 Руки / кисти / вид от первого лица

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| Нижнее тело игрока | `game/scripts/FirstPersonController.Body.cs:34-86` (`InitializeVisibleBody`) | `GeneratedCharacterKitDressing.Attach(this, "aidar-first-person", "CouncilWitness", …)`; поворот 180°, `GroundSolesOnAnchor`, удаление всех `AnimationPlayer` (`:47-51`) | источник тела |
| Какие меши остаются | `FirstPersonController.Body.cs:53-56` | только `CouncilWitness_TrouserLeft/Right_LOD0`, `_BootLeft/Right_LOD0`, `_Body_LOD0`, `_CoatHem_LOD0`; остальные `Visible=false` + `QueueFree` (`:60-68`) | список мешей |
| Тени тела не отбрасываются | `FirstPersonController.Body.cs:66` | `mesh.CastShadow = ShadowCastingSetting.Off` | — |
| Добавленные кости колен/стоп | `FirstPersonController.Body.cs:98-140` | `AddBodyBone("AidarKnee"+side, thigh, …)` и `"AidarAnkle"+side`; пересадка skin ботинка на новую кость (`:129-133`), `TailorBodyTrousers` (`:138`) | рига |
| Кисти рук | `GeneratedCharacterKitDressing.cs:320-326` | кисть определяется по подстроке `"Hand"` в имени меша и получает цвет кожи; отдельной модели кисти/виевмодели нет | цвет кожи |
| Виewmodel оружия/рук в кадре | **не найдено** | в кадре только ноги/полы пальто; рук от первого лица в отдельной ноде нет | можно добавить |
| Скелет тела игрока | `FirstPersonController.Body.cs:58-59,74-79` | `Skeleton3D` берётся с ботинка, `ResetBonePoses()`, кости `Spine`, `Leg.L/R` | — |
| Походка/шаг | `FirstPersonController.Body.cs:15-27,139-140` | процедурная: `_bodyGait`, `_bodyStride`, `BodyLeg(Thigh,Knee,Ankle,…,Phase)` — ноги гнутся кодом, не клипом | параметры шага |

---

## 7. UI: темы, шрифты, панели, документы, журнал, старый ПК

| Что | Где (путь:строка) | Как работает | Что можно менять |
|---|---|---|---|
| **Единственный владелец темы** | `game/scripts/UrmanUiTheme.cs:17-489` | цвета, типографика, форма плашек, моторика экранов; экраны берут `Theme` отсюда | **весь вид UI** |
| Палитры | `UrmanUiTheme.cs:38-76` | `Normal`: ink `0e1413f5`, text `eee7d8`, accent `c42b32` (калина), focus `f2e9d6`; `HighContrast`: чёрный/белый, accent `d4000b` | цвета |
| Размеры | `UrmanUiTheme.cs:81-91` | Body 18, Small 15, Button 20, Heading 30, Title 64, Speaker 22, Subtitle 22, WorldPrompt 24 | размеры |
| Сетка отступов | `UrmanUiTheme.cs:94-101` | Xs 4, S 8, M 16, L 24, Xl 40 | — |
| Скос плашек | `UrmanUiTheme.cs:104,192-217` | `PlateSkew = -0.16f`, `PlateBox(...)` — жёсткая скошенная плашка с offset-тенью | силуэт UI |
| Обычная тема | `UrmanUiTheme.cs:128-130,218+` (`Build`) | `For(bool highContrast)` — кэш `_normal`/`_highContrast` | — |
| Тема «тетрадь» (документы) | `UrmanUiTheme.cs:138-185` | `NotebookInk=34291c`, `NotebookCover=4a3121`, `NotebookPaper=e7dcc1`; своя обработка `Button`/скроллов/`LineEdit` | вид бумаги |
| Анимация открытия | `UrmanUiTheme.cs:106,470+` (`PlayOpen`) | `MotionSeconds=0.14`; уважает `ReducedMotion` | — |
| **Тема в проекте** | `game/project.godot:34` | только `gui/theme/custom_font` — единый фолбэк-шрифт | — |
| Файлов `.theme`/`Theme.tres` | **не найдено** | тем-ресурсов нет; всё строится кодом | — |
| Шрифты (5 TTF) | `game/assets/fonts/`: `PT_Sans-Web-Regular.ttf`, `PT_Sans-Web-Bold.ttf`, `PT_Sans-Web-Italic.ttf`, `PT_Sans-Narrow-Web-Bold.ttf`, `PT_Sans-Narrow-Web-Regular.ttf` + `OFL.txt` | лицензия OFL | подмена шрифта |
| Загрузка шрифтов | `UrmanUiTheme.cs:19,117-121,187-189` | `FontDirectory="res://assets/fonts"`; `BodyFont`, `BodyBoldFont`, `ItalicFont`, `DisplayFont` (Narrow Bold), `DisplayRegularFont`; отсутствие файла → исключение | — |
| **Кириллица и татарские буквы** | проверено чтением `cmap` из TTF | у **всех 5** шрифтов 717 глифов, 210 в кириллическом блоке, и **все 12** татарских букв (ә ө ү җ ң һ + заглавные) присутствуют — пропусков нет | — |
| Импортные настройки шрифтов | `game/assets/fonts/*.ttf.import` | `antialiasing=1`, `multichannel_signed_distance_field=false`, `msdf_size=48`, `allow_system_fallback=true`, `hinting=3`, `subpixel_positioning=4`, `oversampling=0.0`, `fallbacks=[]` | MSDF/фолбэки |
| Тема ПК (Windows XP Luna) | `game/scripts/OldPcXpChrome.cs:1-120` | `LunaTop=3b8cf5`, `LunaMid=1f5fd8`, `StartGreen=3c9a3c`, окно `ece9d8`, меню `d3e5fa`, татарский тюльпан `TulipRed=c8323c`; всё рисуется кодом | «скин» ПК |
| Обои ПК | `game/scripts/OldPcWallpaper.cs:1-110` | `_Draw`: летняя деревня (сруб, синие наличники, берёзы, минарет) вместо XP-холма; комментарий: «Code-drawn; not a replacement for a world asset» | — |
| Иконки ПК | `game/scripts/OldPcDesktopIcon.cs:1-86` | 16 примитивных `_Draw`-глифов (`Box`, `DrawLine`) по полю `Kind` | — |
| Экраны UI (7 сцен) | `game/scenes/ui/`: `main_menu_ui.tscn` (6 строк), `audio_cue_ui.tscn` (27), `dialogue_ui.tscn` (72), `document_ui.tscn` (105), `journal_ui.tscn` (121), `old_pc_ui.tscn` (181), `settings_ui.tscn` (223) | в основном `CanvasLayer` + `Control`; наполняются скриптами | вёрстка/иерархия |
| Документы: бумага | `game/scenes/ui/document_ui.tscn:4-17` | `StyleBoxTexture` на `res://assets/ui/document_notebook_sheet.png` с `texture_margin` 94/25/25/25 и `content_margin` 112/40/48/36 | подложка бумаги |
| Документы: логика | `game/scripts/DocumentUi.cs:41-98,122` | `_notebookSheet` берётся из темы панели (`:41`); `_documentView.Theme = UrmanUiTheme.Notebook` (`:64`); `ink = NotebookInk` (или чёрный в HC) для title/body/status (`:81-98`); `UiFoley.Play("paper_open")` (`:122`) | чернила/бумага |
| Журнал (тетрадь) | `game/scripts/JournalUi.cs:15,65,77,136-165` | `RichTextLabel Body`; `BuildNotebookUi()` (`:77`); `NotebookProjection()` (`:164`); `RefreshNotebookPages()` (`:165`); звук `paper_open` | страницы/проекция |
| Журнал: карточки | `JournalUi.cs:284-460` | `AddThemeStyleboxOverride("panel", UrmanUiTheme.PlateBox(...))` (`:435-437`), отдельные цвета записей/цитат (`:444-448`) | — |
| Журнал: доп. части | `game/scripts/JournalUi.Notebook.cs`, `JournalUi.Vocabulary.cs` | проекция тетради и словарь татарских слов | — |
| Старый ПК: главный скрипт | `game/scripts/OldPcUi.cs:80-87` | `_computer.Theme ??= new Theme()`; цвета `a5c7ad`, `b7c9b0`, `b9c8b1`, `94b19a`, `aebcac`, выделение `d0b46d` | «зелёный люминофор» |
| ПК: приложения/окна | `game/scripts/OldPcUi.Apps.cs` (784), `OldPcUi.Desktop.cs` (658), `OldPcUi.Search.cs`, `OldPcUi.Social.cs`, `OldPcUi.Chat.cs`, `OldPcUi.Hints.cs`, `OldPcUi.Tetris.cs`, `OldPcTetris.cs`, `OldPcWindowTitleBar.cs` | оконная модель, панель задач, часы, поиск по архиву, соцсеть, чат, подсказки, «Тетрис» | функциональность ПК |
| Соцсеть/страницы ПК | `OldPcUi.Social.cs:27,144,176-195` | `RichTextLabel` c `line_separation=4`, `DesktopStyle(...)` плашки, `DesktopFocus(...)` | — |
| Доступность UI | `game/scripts/AccessibilityPresentation.cs` | применяет `UrmanUiTheme.For(settings)` ко всем экранам | — |
| Иконки-ассеты | `game/assets/ui/` = 2 файла (384 КБ): `document_notebook_sheet.png`, `urman_app_icon_v1.png`; `game/assets/textures/ui/` — `act1_menu_winter_v1.png` и др. | PNG | арт UI |
| Проверка `_Draw`-владельцев | `grep _Draw\|DrawString\|DrawTexture\|QueueRedraw` | владельцы код-рисунка: `OldPcWallpaper.cs`, `OldPcDesktopIcon.cs`, `OldPcTetris.cs`, `SettlementMapControl.cs`, `DebugSoundPanel.cs` | — |

---

## 8. Ассеты `game/assets` — сводка

### 8.1 По каталогам

| Каталог | Файлов | Размер | Доминирующие форматы | Роль |
|---|---:|---:|---|---|
| `game/assets/audio/` | 245 | 116 МБ | `.ogg`, `.wav`, `.mp3` | звук (вне визуала) |
| `game/assets/textures/` | 241 | 221 МБ | `.png` (170), `.jpg` (13) + `.import` | все 3D-фактуры |
| `game/assets/models/` | 56 | 47 МБ | `.glb` (15) + `.import` | киты/пропсы |
| `game/assets/generated/` | 52 | 63 МБ | `.glb` (4) + `.png` (20) | кит персонажей, Niva, киты из Blender-скриптов |
| `game/assets/third_party/` | 43 | 13 МБ | `.glb` (7) + `.jpg`/`.png` | Office/Police set (см. `ATTRIBUTION.txt`, `OFFICESET_LICENSE.txt`) |
| `game/assets/images/` | 10 | 9.6 МБ | `.png` | иллюстрации/документы |
| `game/assets/fonts/` | 11 | 2.2 МБ | `.ttf` (5) + `.import` | UI-шрифты |
| `game/assets/animations/` | 2 | 7.3 МБ | `ual1_standard.glb` | библиотека анимаций |
| `game/assets/ui/` | 2 | 384 КБ | `.png` | лист тетради, иконка |

Всего изображений в `game/assets` — **183**: 170 PNG + 13 JPG.

### 8.2 Текстуры

| Метрика | Значение |
|---|---|
| Всего изображений | 183 |
| Разрешения (топ) | 1024×1024 — **73**; 1254×1254 — **40**; 512×512 — 27; 256×256 — 8; 2172×724 — 5; 1774×887 — 4; 1536×1024 — 4; 2048×2048 — 2; 4096×4096 — 2; 3072×2048 — 2 |
| Уникальных разрешений | 25 |
| Изображений ≤64×64 | **0** |
| По каталогам | `textures/` 117 · `third_party/` 31 · `generated/` 20 · `models/` 9 · `images/` 5 · `ui/` 1 |

**Заглушки — это не текстуры.** Ни одного однопиксельного/пустого PNG не найдено.
Явные заглушки/примитивы — **процедурная геометрия**: 461 callsite с `BoxMesh`,
`CylinderMesh`, `SphereMesh`, `QuadMesh`, `TorusMesh`, `CapsuleMesh`, `PrismMesh`
(см. §3.1). Единственная известная заглушка-текстура по смыслу — процедурная
маска снежинки `WinterParticleSurfaces.Snow` (`game/scripts/WinterParticleSurfaces.cs:20-38`,
генерируется `Image.CreateEmpty(64,64)` в рантайме, с комментарием «rendering
resources, not generated artwork»).

### 8.3 Модели

| Метрика | Значение |
|---|---|
| `.glb` в `game/assets` | **30** |
| `.blend` в `game/assets` | 1 (`props/milk_bottle/karaurman_milk_bottle_v1.blend`) |
| `.fbx` / `.obj` / `.res` / `.gltf` в `game/assets` | **0** |
| Суммарный размер | 92.6 МБ |
| Крупнейшие | `generated/urman_character_kit_v2.glb` 32.81 МБ · `animations/ual1_standard.glb` 7.27 · `models/props/chayan_magazine_animated_v1.glb` 6.18 · `generated/urman_character_kit.glb` 4.58 · `third_party/police/vaz2106_static.glb` 4.52 · `generated/urman_niva.glb` 4.27 · `props/chakchak/karaurman_chakchak_box_v1.glb` 4.23 · `agent_b_act1/agentb_foliage_kit.glb` 3.47 · `generated/urman_act1_village_landmark_kit.glb` 3.04 · `models/act1/urman_village_exterior_kit.glb` 2.87 |
| Blender-источники (`assets/source/blender/`, вне `game/`) | **20** `.blend`: 5 act1-китов, 5 agent_b-китов, `urman_character_kit(_v2)`, `tamara_aged_head`, `urman_cc0_head_templates`, `urman_modular_kit`, `urman_niva`, `urman_winter_pine`, `urman_winter_dead_tree`, `urman_forest_edge_kit`, `urman_act1_village_landmark_kit` |
| GLB, реально загружаемые кодом | 13 (полный список — в §3.1 и §6.1) |

### 8.4 Паспорта примитивных поверхностей (только сводка)

`docs/urman_knowledge_base/art/primitive_surface_audit_2026-09-29.json`
(176 490 байт, ключи `kind`, `date`, `scope`, `limitations`, `callsites`):

| Метрика | Значение |
|---|---|
| `kind` | «source inventory, not execution queue» |
| `scope` | «top-level Act I runtime candidates and Agent B dependencies; excludes Studio/Tamara/FullGame, collision-only shapes» |
| `callsites` | **461** |
| `primitive_types` по частоте | `CylinderMesh` 71 · `BoxMesh` 54 · `SphereMesh` 35 · `QuadMesh` 15 · `TorusMesh` 6 · `CapsuleMesh` 4 · `PrismMesh` 3 |
| `review_status` | все 461: «source-only; runtime visibility/material to verify» |
| Топ-источники | `Act1ConnectedWorld.SquareInteriors.Club.cs` 59 · `…School.cs` 49 · `…SovkhozSquare.cs` 41 · `…SquareInteriors.cs` 40 · `PrologueDeepForest.cs` 28 · `CarryableProp.Geometry.cs` 24 · `VehicleVisualFactory.RoadVehicles.cs` 24 |

`docs/urman_knowledge_base/art/primitive_surface_requests_2026-09-29.json`
(67 351 байт, ключи `kind`, `requests`, `civic_handmade_batch_2026_09_30`):

| Метрика | Значение |
|---|---|
| `kind` | «asset request passports; execution stays in execution_backlog.json» |
| `requests` | **28** паспортов (`TX29-01`…`TX29-28`) |
| `priority` | `0` — 16, `1` — 12 |
| `generation_status` | `generated_sources_inspected_selected_integration_candidate` 8 · `reuse_scoped_pending_runtime` 5 · `reuse_scoped_and_missing_generation_blocked` 5 · `current civic batch generated and bound; other consumers remain open` 5 · `conditional_receiver_uv_review_required` 3 · `missing_generation_blocked` 1 · `procedural_vfx_owner_no_bitmap_job` 1 |
| `runtime_status` | `not-run` 14 · `current native review pending` 5 · `current receiver capture pending…` 4 · `receiver capture pending…` 4 · `current-run verification pending` 1 |
| `execution_queue` | все 28 → `docs/urman_knowledge_base/execution_backlog.json` (ACT1-TEXTURE.00/.02/.03/.07/.08/.11/.12) |
| Схема записи | `id, family, priority, consumer, geometry_required, uv_contract, format_size, physical_response, reference, generation_status, geometry_status, runtime_status, verification, execution_queue, source_callsite_inventory, output, provenance` |
| `civic_handmade_batch_2026_09_30` | `manifest: docs/production/visual_rework_2026-09-30/imagegen_manifest.json`, `geometry: …/interiors_receipt.json`, `scope`: «17 source PNG, correction versions retained; selected subset; not all 28 families accepted» |

Семейства запросов (по 28 паспортам): жилые фасады/срубы; ограды/ворота/калитки;
наличники/окна/двери/крыльца; крыши/водостоки/дымоходы; штукатурка общественных
зданий; кирпич/бетон; дворы/дорожки/снег/дрова; лавки; школьная мебель; фанерный
стул; зелёная доска; секционные батареи.

---

## 9. Постобработка: что реально включено и что запрещено

### 9.1 Реально включено в текущем рендере

| Эффект | Где (путь:строка) | Состояние |
|---|---|---|
| Tonemap (наружный Акт I) | `Act1ConnectedWorld.cs:1785-1786` | **AgX**, `exposure` из профиля (1.0 / 1.0 / 0.92) |
| Tonemap (наружный, «заводской») | `AgentBAct1ExteriorLayer.cs:2574-2575` | **Aces**, exposure 0.98 (до применения профиля) |
| Tonemap (бенчмарк-зоны) | `StyleBenchmarkZone.cs:162-163` | **Filmic** (AgX для интерьера дома), exposure 0.96–1.04 |
| Tonemap (FullGame) | `FullGameZone.cs:73` | **Filmic** |
| Depth fog | `Act1ConnectedWorld.cs:1778-1784`; интерьер окна `:630-651` | **включён**; в доме `FogMode=Depth`, `FogDensity=.93`, `FogDepthBegin=12`, `FogDepthEnd=max(24, 2.4/outdoor.FogDensity)` |
| SSAO | `Act1ConnectedWorld.cs:1791-1793`; `StyleBenchmarkZone.cs:149-151` | **включён** (intensity 0.75 / radius 0.4 в деревне; 0.55/0.30 в бенчмарке); пресет `low` выключает (`GraphicsQuality.cs:129-136`) |
| MSAA | `GraphicsQuality.cs:35-42` | medium — `Msaa2X`, high — `Msaa4X`, low — выкл. |
| Screen-space AA | `GraphicsQuality.cs:43` | FXAA **только** на `low`, иначе Disabled |
| 3D scale / FSR | `GraphicsQuality.cs:35-41` | medium 0.9 (Bilinear), low 0.7 (**FSR**), high 1.0 |
| **Glow** | `Act1ConnectedWorld.cs:1790` | **выключен**: `environment.GlowEnabled = false` |
| **Adjustments** | `Act1ConnectedWorld.cs:1794-1797` | **выключены**: `AdjustmentEnabled=false`, brightness/saturation/contrast = 1 |
| Volumetric fog | не найдено | не включён нигде |
| SSIL | не найдено | не включён |
| SDFGI / VoxelGI / LightmapGI | не найдено | не используются |
| DOF / Motion blur / Sharpen | не найдено | не используются |
| Screen-space reflections | не найдено | не используются |

### 9.2 Что явно запрещено документами проекта

| Запрет | Где зафиксировано |
|---|---|
| Glow и adjustments отключены; контрольный захват без них | `docs/urman_knowledge_base/decision_log.md:3068-3073` («Фаза 8: свет без глобального Glow, 2026-09-11») |
| Не добавлять постфильтр, glow или новый lighting owner | `docs/urman_knowledge_base/design_style.md:356` |
| Дом: «новый менеджер погоды/профилей, объёмный туман, glow и отражения не добавлены» | `docs/urman_knowledge_base/design_style.md:705` |
| Постобработка не должна маскировать геометрию или создавать нового глобального владельца материала | `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md:333` |
| Ink-smear и бумажное затемнение — только редкая сюжетная постобработка, не маскировка переходов | `docs/urman_knowledge_base/design_style.md:194` |
| Снег: не чистый белый; пересвет в чистый белый запрещён | `docs/urman_knowledge_base/design_style.md:240-242` |
| Небо: «слабый галообразный ореол; никакой "полярной" синевы» | `docs/urman_knowledge_base/design_style.md:249` |
| Не использовать кровь, черепа, glowing eyes, Halloween-эстетику, скримеры как базовый язык | `docs/urman_knowledge_base/design_style.md:325` |
| Не менять ради FPS: тени травы, 4 каскада→2, 120 м→45 м, High MSAA/scale, туман, дальность | `docs/production/performance_handoff_2026-10-07.md:217-230` |

---

## 10. Производительность: известные лимиты (геометрия / материалы / батчинг)

Источник: `docs/production/performance_handoff_2026-10-07.md` (разделы 2.1, 3 (G01),
13 (G11), 15 (G13), 17). Документ подчёркивает: текущий аудит **не запускал** игру,
FPS не измерен (строки 15–18), цифры — исторические, не текущие.

### 10.1 Цель и исторические замеры

| Что | Значение | Где |
|---|---|---|
| Цель автора | стабильные 50–60 FPS на базовом Mac M1 при High без ухудшения картинки | `performance_handoff_2026-10-07.md:9-11` |
| Исторический замер (M4 Pro / High / 1080p, после прогрева) | кадр ≈ **31,32 мс**; CPU renderer **16,71 мс**; physics **10,91 мс** | `:98-100` |
| Бюджет 50–60 FPS | 20–16,7 мс на кадр | `:109` |
| Оговорка | сумму 16,71 + 10,91 нельзя считать неперекрывающимися частями одного кадра | `:118-126` |
| Ограничитель MaxFps / TimeScale / лишние SubViewport | **не найдены** в обычных `game/scripts` | `:128-130` |
| Draw calls (другой документ, исторический) | 10 406 draw calls в кадре `phase07/snowed_obstacles1080`, avg 13,946 / p95 14,893 / max 26,287 мс | `docs/urman_knowledge_base/decision_log.md:3067` |

### 10.2 Геометрия и ключи отрисовки

| Что | Значение | Где |
|---|---|---|
| Исторический census | **21 402** поверхности-кандидата, **3 850** различных ключей `mesh/material/mirror` | `performance_handoff_2026-10-07.md:186-190` |
| Лес | 14 237 поверхностей на **259** ключей | `:190` |
| `AuthoredWorldDirector` | 3 238 поверхностей / **985** ключей | `:190` |
| Импортированный exterior kit | 876 поверхностей / **870** ключей | `:190` |
| Где читать код | `GraphicsQuality.cs` (`Apply`/`ConfigureSun`), `Act1DemoRoot.RenderDiagnostics.cs`, `AgentBAct1ExteriorLayer.cs` (лесные партии), `Act1ConnectedWorld.cs` (`BuildAct1AuthoredExteriorKit`) | `:173-181` |
| Проблема | объект может стоить несколько отправок: основной проход + каскады теней + локальные источники; низкая непрозрачность тени не удешевляет её расчёт | `:187-197` |
| Что **не** делать | не выключать тени травы/кустов; не менять 4 каскада→2 и 120 м→45 м; не менять High MSAA/scale, туман, дальность; не собирать весь лес в один MultiMesh (был регресс из-за отсечения); не применять ShadowMesh вслепую к shader-deformed снегу/веткам | `:217-241` |
| Что уже батчится | лес: части шаблона уже сливаются по `bark/snow/foliage/berries/stone` через `SurfaceTool` и кэшируются по варианту (`AgentBAct1ExteriorLayer.cs:1334-1364`); напочвенный покров уже в `MultiMesh` по ячейке **12 м** × вариант × регион × LOD (`:1610-1624`) | `:232-241` |
| Готовый, но выключенный пилот | `game/scripts/Act1ConnectedWorld.WindowBatching.cs:11-14`, `BatchPaintedWindowSurrounds` выходит, если `URMAN_WINDOW_BATCH_PILOT != "1"` | `:243-247` |

### 10.3 Материалы и текстурный импорт

| Что | Значение | Где |
|---|---|---|
| Статический разбор `urman_village_exterior_kit.glb` | **1476** mesh entries, **886** geometry+material сигнатур, **590** избыточных entries (старое число 733 = все члены групп повторов) | `performance_handoff_2026-10-07.md:243-256` |
| Повторы по компонентам | VariantA 185→115 · VariantB 201→128 · VariantC 179→113 | `:262-268` |
| Импорт уже включён | `array_mesh/deduplicate_surfaces=true`, `mesh_library/use_node_names_as_mesh_names=false`, `_subresources={}` — одни GLB entries **не доказывают** раздельные Mesh RID | `:270-276` |
| 590 | это число копий внутри файла, **не** число видимых draws и **не** обещанный выигрыш | `:327-332` |
| Triplanar | `PainterlyMaterialLibrary.triplanar_albedo` делает 3 выборки; прежний точный zero-weight эксперимент с explicit gradients **не дал** выигрыша (27,01 → 27,15 FPS) и удалён | `:1770-1780` |
| Snow micro detail | нельзя вырезать блок целиком при `detail == 0`: `response.g` всё ещё участвует в roughness — изменит BRDF дальнего снега | `:1781-1784` |
| Текстурный импорт (G13) | **117** `.import` с `compress/mode=0`, из них **73** с `mipmaps/generate=false`, **44** с `true`; эти 117 PNG = **173 526 242** пикселя в исходном разрешении | `:1550-1562` |
| Доказанные связи 3D-consumers без mipmaps | `realism_20260930/herringbone_oak_v1_basecolor.png` (1254×1254, `CivicSurfaceLibrary.Parquet/Floor`) · `civic/mosque_prayer_carpet_v2_albedo.png` (1254², `MosqueCarpets`) · `painterly/urman_t07_v01_basecolor.png` (1254², `cloth_towel`) · `realism_20260929/birch_plywood_varnished_v1_basecolor.png` (1254², `RuralPropMaterials.Surface("plywood")`) · `realism_20260929/painted_steel_enamel_v1_basecolor.png` (1254², `RuralPropMaterials.Surface("steel")`) · `painterly/frost_window_v1_albedo.png` (1024×1536, окна) | `:1570-1582` |
| Что уже с mipmaps | `painterly/urman_w01_v01_basecolor.png`, `urman_w03_v01_basecolor.png` — `mipmaps/generate=true` | `:1600-1603` |
| Крупнейшие без mipmaps | magazine atlas 4096×4096, chakchak atlas 3072×2048 — но прямой consumer этих PNG в `game/scripts` не найден | `:1626-1632` |

### 10.4 Батчинг: границы и исторический результат

| Что | Значение | Где |
|---|---|---|
| Граница culling | **одно окно**, не весь лес и не квартал | `performance_handoff_2026-10-07.md:414-419` |
| Исторический лесной batch | 108 → **95** FPS, p95 9,9 → **13** мс (регресс); точный размер прежних ячеек не зафиксирован | `:416-419` |
| Оконный пакет (пакет B) | 79 parcel placements (open_part A17/B17/C10, far_bank A11/B12/C12); у каждого VariantA/B/C Dwelling **9 окон** по **4** Jamb/Rail на окно → верхняя граница **2844 → 711** активных render instances | `:323-331` |
| Условие пилота окна | сохранять: 4 непрозрачных неподвижных элементов на окно, glass/backing/physical owners остаются | `Act1ConnectedWorld.WindowBatching.cs:9-10,35-37` |
| Что нельзя | объединять объекты только по похожему цвету; считать alpha=0 скрытием без затрат; объединять весь лес | `:238-241` |
| Уникальный ключ дедупликации | все attributes, точные accessor bytes (componentType/type/count), layout/normalized, indices, primitive order/mode/material, weights, morph targets, extensions/extras | `:283-288` |
| Пакеты A и B проверять раздельно | дедупликация меняет mesh RID, от которых зависит ключ batching cache; выигрыши не складываются арифметически | `:406-411` |

### 10.5 Персонажи и прочие подтверждённые расходы

| Что | Значение | Где |
|---|---|---|
| Полный кит на каждого NPC | `Attach` инстансирует **весь** комплект, затем `Free()` чужих `_Rig`/`_Anchor`; обычные generic-плоты дают **33** вызова этой ветки, все с human-префиксами | `performance_handoff_2026-10-07.md:1349-1382` |
| GLB-структура персонажей | v2: 799 nodes / 196 meshes / 9 skins / 36 animations; v1: 633 / 542 / 9 / 18; у каждого файла 9 пар root Rig/Anchor; v2 имеет 4 исходных `Resident_*` по 195 каналов | `:1369-1376` |
| Предупреждение | число glTF nodes/channels ≠ числу Godot Nodes/tracks; не умножать 799 на 33 и не объявлять draw calls | `:1377-1383` |
| Снегопад | один `CpuParticles3D` с 4000 частицами, не 4000 scene nodes | `:1786-1790` |
| Матрица запрещённых ложных сокращений | 12 пунктов (снизить shadow distance/cascades; спрятать комнаты; объединить весь лес; один MultiMesh на все meshes; уменьшить physics tick; кэш raycast по позиции; убрать проверку копыт; уменьшить следы/resolution/subdivision; System.Half для маски; PhysicsDirectSpaceState в Task.Run; CPU-only микропроверку за замер; Windows за доказательство M1) | `:1744-1760` |

---

## Точки вмешательства для художника/программиста

Упорядоченный список: сверху — максимальный визуальный эффект при минимальном риске.

### A. Свет, небо, туман, экспозиция (без кода)

1. `game/content/world/atmosphere.v1.json` — **главный файл**: 3 профиля, все параметры солнца, неба, тумана, ambient, SSAO, экспозиции. Правка не требует C#.
2. `game/scripts/AtmosphereProfiles.cs` — схема/загрузчик профилей (`:7-12` запись, `:37-55` парсинг). Править, только если меняется **набор** полей.
3. `game/scripts/Act1ConnectedWorld.cs:1751-1825` (`TuneConnectedAct1Atmosphere`) — выбор профиля по зоне и порядок применения; здесь же `GlowEnabled=false` (`:1790`) и отключение adjustments (`:1794-1797`).
4. `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:2538-2589` (`BuildEnvironment`) — базовый `Environment`/`Sky`/`AgentBSun` до применения профиля; `:2690-2732` — процедурная облачная плёнка неба.
5. `game/scripts/GraphicsQuality.cs:30-60` и `:129-144` — пресеты (render scale, MSAA, FXAA, LOD threshold, атласы теней, каскады, дальность теней, SSAO). **Единственный владелец этих решений.**
6. `game/scripts/StyleBenchmarkZone.cs:97-185` — интерьерные/бенчмарк-профили и локальные источники (`:552-605`, `:921-959`).
7. `game/scripts/FullGameZone.cs:41-140` — свет последующих актов.

### B. Материалы и шейдеры

8. `game/scripts/PainterlyMaterialLibrary.cs:31-348` — **сам шейдер** (трипланар, snow blanket, micro-рельеф, trample, ветер, frost, cut wood, backlight). Любая новая фича (parallax, маска-бленд, второй альбедо-слой) — здесь.
9. `game/scripts/PainterlyMaterialLibrary.cs:708-1019` (`ForColor`) — таблицы ручек по семействам: `brush_scale`, `variation`, `texture_strength`, `snow_sparkle`, `snow_coverage`, `cell_jitter`, roughness/specular/wet.
10. `game/scripts/PainterlyMaterialLibrary.cs:386-476` — `SurfaceTextures` (34) и `WinterTextures` (10): пути альбедо и масштаб UV.
11. `game/scripts/experiments/agent_b_act1/AgentBKitMaterials.cs:14-55` — карта `AB_*` → цвет/surface/эмиссия для всех китов Agent B.
12. `game/scripts/VillageWindowMaterials.cs:8-27,32-51` — шейдер и энергия окон.
13. `game/scripts/PainterlyMaterialLibrary.cs:355-384` — варианты шейдера (rigid/two-sided/cutout); трогать согласованно с `:1010-1015`.

### C. Геометрия мира, дома, заборы, дороги, снег

14. `game/scripts/Act1ConnectedWorld.cs` — **главный строитель** (8 420–8 520 — снежные валы; 1 125–1 210 — ядро; 1 640–1 662 — подавление блокбаутов; 1 681–1 733 — подавление декора дороги).
15. `game/scripts/Act1ConnectedWorld.AuthoredWorld.cs:15-38` — монтирование generic-плотов и регрейд их материалов.
16. `game/scripts/AuthoredWorldDirector.cs:73-341` — исполнение `content/world/*.world.v1.json` (визуал, скаттер, коллизии).
17. `game/content/world/*.world.v1.json` (7 плотов) и `catalog.v1.json` — **данные домов, пропсов, скаттера**; правятся без C#.
18. `game/scripts/Act1WorldLayout.cs:20-190` — мировые позиции логических зон и точек спавна.
19. `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:120-176` (сборка 5 китов), `:853-1274` (подавления дублей), `:360-473` (дороги), `:474-604` (вырез снега под домом), `:2609-2636` (снегопад), `:2638-2681` (ночные акценты).
20. `game/scripts/Act1ConnectedWorld.WindowBatching.cs:11-14` — пилот оконных групп (`URMAN_WINDOW_BATCH_PILOT=1`).
21. Blender-источники китов: `assets/source/blender/act1/*.blend` (+ `.py`-генераторы), `assets/source/blender/agent_b_act1/*.blend` — правка формы/UV с последующим экспортом GLB.
22. Примитивы-заглушки: `game/scripts/Act1ConnectedWorld.SquareInteriors*.cs`, `…SovkhozSquare.cs`, `PrologueDeepForest.cs`, `CarryableProp.Geometry.cs`, `VehicleVisualFactory.RoadVehicles.cs` (по паспортам `primitive_surface_requests_2026-09-29.json`).

### D. Растительность

23. `game/scripts/experiments/agent_b_act1/AgentBFoliagePlan.cs:29-147` — авторский список посадок (позиция + порода).
24. `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs:1794-1861` — процедурное размножение и seed; `:2108-2143` — 9-рядное лесное кольцо; `:2400-2479` — состав кольца; `:1301-1365` — сборка мешей вариантов; `:1711-1732` — региональные палитры.
25. `game/assets/models/act1/urman_winter_pine.glb`, `urman_winter_dead_tree.glb`, `game/assets/models/agent_b_act1/agentb_foliage_kit.glb` — сами деревья.

### E. Персонажи

26. `game/scripts/GeneratedCharacterKitDressing.cs` — выбор кита (`:20-24`), инстанс и обрезка ригов (`:47-112`), выбор мешей/LOD (`:114-150`), процедурные материалы (`:252-362`) и human-материалы (`:364-407`).
27. `game/content/animations/catalog.v1.json` — **29 движений**, источник (kit / UAL / procedural), loop, speed.
28. `game/scripts/AnimationCatalog.cs:74-169` — проигрывание и ретаргет клипов UAL на 65-костный скелет; `:205-215` — путь скелета.
29. `game/scripts/FirstPersonController.Body.cs:34-140` — тело игрока от первого лица, добавленные кости колен/стоп, список видимых мешей.
30. `assets/source/blender/urman_character_kit.blend`, `urman_character_kit_v2.blend`, `characters/tamara_aged_head.blend`, `characters/urman_cc0_head_templates.blend` — исходники формы/лиц; `game/assets/animations/ual1_standard.glb` — клипы.

### F. UI

31. `game/scripts/UrmanUiTheme.cs` — **единственный владелец** цвета, типографики, плашек и моторики UI; «тетрадь» документов `:138-185`.
32. `game/assets/fonts/*.ttf` + `game/project.godot:34` — шрифты; все 5 покрывают кириллицу и все татарские буквы.
33. `game/scripts/DocumentUi.cs`, `JournalUi.cs` + `JournalUi.Notebook.cs`/`JournalUi.Vocabulary.cs` — документы и журнал.
34. `game/scripts/OldPcUi*.cs` (`OldPcUi.cs`, `.Apps`, `.Desktop`, `.Search`, `.Social`, `.Chat`, `.Hints`, `.Tetris`), `OldPcXpChrome.cs`, `OldPcWallpaper.cs`, `OldPcDesktopIcon.cs`, `OldPcWindowTitleBar.cs` — старый ПК.
35. `game/scenes/ui/*.tscn` (7 файлов) — иерархия и вёрстка экранов; `game/assets/ui/document_notebook_sheet.png` — подложка бумаги.

### G. Производительность (с согласия автора; ограничения §10)

36. `game/scripts/GraphicsQuality.cs` — пресеты и диагностические probe-override (`--urman-perf-probe`, `:68-126`).
37. `game/scripts/Act1DemoRoot.RenderDiagnostics.cs` — существующий сбор render-census (`:40-43,159-162,181-260`): единственный согласованный измеритель.
38. `game/scripts/Act1ConnectedWorld.WindowBatching.cs` — готовый пилот оконных групп; изучать **до** создания второго batching-хелпера.
39. `assets/source/blender/act1/urman_village_exterior_kit.py` (после `bpy.ops.export_scene.gltf`, ~строка 3370) — место нормализации mesh references по плану пакета A.
40. Пилоты текстурного импорта (G13): `.import`-файлы конкретных consumer'ов из `§10.3` — по одному параметру за этап.
