# VIS-005 — Карта реальных потребителей painterly-материалов (Акт I)

Дата среза: 2026-10-07, 17:00 локального времени. Тип: статическая доказательная карта.
Движок не запускался, сборки не выполнялись, файлы проекта не изменены.

**Рабочее дерево на момент разбора было грязным и правилось параллельно**
(`PainterlyMaterialLibrary.cs`, `Act1ConnectedWorld.cs`, `AtmosphereProfiles.cs`,
`AtmosphereDump.cs`, `Act1DemoRoot.DevViewCapture.cs`; HEAD `612f9bc4`, 12 изменённых
путей). Номера строк соответствуют снятому срезу; устойчивая навигация — по имени
семейства, метода и ключа словаря. Машина-читаемая версия:
`material_consumers_2026-10-07.json` (68 семейств, по одному объекту на семейство,
каждый с проверенными callsite-адресами).

Источники: `game/scripts/PainterlyMaterialLibrary.cs` (1084 строки в срезе),
`game/scripts/experiments/agent_b_act1/AgentBKitMaterials.cs` (159),
`game/scripts/Act1ConnectedWorld.cs` (10581), `game/scripts/VillageWindowMaterials.cs` (59),
`game/scripts/GeneratedCharacterKitDressing.cs` (445), `game/assets/textures/painterly/` (74 PNG).

---

## 1. Итог

| Метрика | Значение | Доказательство |
|---|---|---|
| Семейств поверхности всего | **68** | сумма ключей двух таблиц + семейств без альбедо |
| …с альбедо-картой | 51 | `SurfaceTextures` 41 (`PainterlyMaterialLibrary.cs:386-458`) + `WinterTextures` 10 (`:464-476`) |
| …без альбедо, рисуются только процедурно | 17 | §3 |
| PNG, на которые ссылается библиотека | 44 | разбор `res://assets/textures/painterly/...` в `:386-476` и `:1043-1046` |
| PNG физически в `game/assets/textures/painterly/` | 74 | листинг каталога |
| Ссылок на несуществующий PNG | **2** | `rowan_berries_v1_albedo.png` (`:473`), `wattle_weave_v1_albedo.png` (`:474`) |
| Семейств без единого получателя | **5** | `cloth_table`, `terrain`, `grass_tuft`, `ornament_trim`, `wattle` |
| PNG на диске без привязки к семейству | 32 | §5.4 |
| PNG, разделяемых несколькими семействами | 7 карт / 17 семейств | §5.1 (VIS-038) |
| Регистров «имя материала GLB → painterly» | **5** параллельно | §6 |
| Painterly-материал в UI | **не найдено** | ни одного вызова `PainterlyMaterialLibrary.*` в коде `Control`/`CanvasLayer`; UI идёт через `UrmanUiTheme` |

Вся картинка поверхности Актов I собрана в одном шейдере-строке
(`PainterlyMaterialLibrary.cs:31-347`); `.gdshader`- и `.tres`-материалов в проекте нет.

---

## 2. Как устроена привязка «family → карта → consumer»

1. `ForColor(htmlColor, surface, sheltered)` — единственная фабрика
   (`PainterlyMaterialLibrary.cs:771`). Кэш `Materials` (`:478`), ключ
   `$"{surface}:{htmlColor}:{sheltered|exposed}"` (`:773`), компаратор `OrdinalIgnoreCase`.
2. `finishSurface` — алиасы для тюнинга, **но не для поиска карты** (`:782-791`):
   `wood_painted_blue|green|trim|wood_log_uv → wood_facade`,
   `wood_floor_painted → wood_furniture_interior`,
   `wood_fence_vertical|rail|uv → wood_fence`,
   `plaster_domestic → wall_institution` (`:787`), `stone_foundation → stone`,
   `cloth_table|cloth_curtain|cloth_towel → cloth` (`:789`).
   Карта ищется по **исходному** `surface` (`:1001-1021`), поэтому алиас меняет
   только реакцию BRDF, а не картинку, и не схлопывает material state.
3. Текстурный слот один: `uniform sampler2D albedo_texture` (`:48`),
   `has_albedo_texture` (`:49`), `texture_scale` (`:71`).
   `texture_scale` пишется из таблицы: `:1008` (`SurfaceTextures`), `:1019` (`WinterTextures`).
4. Зимние карты подключаются **условно**: `ResourceLoader.Exists(...)` (`:1012`).
   Если PNG нет — семейство молча рендерится без альбедо.
5. `SurfaceTextures` бросает исключение при отсутствии PNG (`:1005-1006`),
   `WinterTextures` — нет. Разное поведение отказа у двух таблиц одного владельца.
6. Производные фабрики (`ForCutout` `:654`, `PreserveSourceCulling` `:678`,
   `ForMovingCloth` `:697`, `ForLocalWoodPiece` `:709`, `WithoutSnow` `:729`,
   `Sheltered` `:745`, `ForPath` `:759`) **дублируют запись в тот же кэш** под
   отдельным префиксом ключа, то есть базовый материал и его вариант живут
   одновременно: `:661` (`ForCutout`), `:693` (`PreserveSourceCulling`), `:705` (`ForMovingCloth`),
   `:723` (`ForLocalWoodPiece`), `:737` (`WithoutSnow`), `:767` (`ForPath`).

---

## 3. Семейства без альбедо (17): рисуются плоским `base_color` + процедурной мазкой

Механика: `has_albedo_texture` остаётся `false`, и в `fragment()` берётся
`painted_color = base_color.rgb` (`PainterlyMaterialLibrary.cs:225-227`).

| Семейство | Настроено в шейдерных таблицах | Прямые callsite (проверено) | Где применяется |
|---|---|---|---|
| `metal` | **нет**, падает в `_ =>` (`:818,840,860,905,922,943`) | `Act1ConnectedWorld.MosqueInterior.cs:522,742`, `Act1ConnectedWorld.cs:7879`, `Act1ConnectedWorld.QuietCareDiscoveries.cs:217,223,254` (14 всего) | улица + интерьер (мечеть, ФАП, двор) |
| `bark_birch` | `brush_scale .25`, `variation .11`, `snow_sparkle .22`, `snow_coverage .30`, `cell_jitter .20`, `surfaceGrade (.92/.08/.20)` | `Act1ConnectedWorld.cs:2146,2183,3704`, `StyleBenchmarkZone.cs:1377` (10) | улица / интерьер бани |
| `leaf_birch` | + `leaf_transmission 0.45` (`:990`), `wind_sway .07` (`:991`) | `Act1ConnectedWorld.cs:2147,3705`, `StyleBenchmarkZone.cs:1378` (4) | улица |
| `grass` | `snow_coverage .72`, `snow_sparkle .35`, `cell_jitter .20`, `wet_grade .45` | `Act1ConnectedWorld.cs:2142,2178,3760` (8) | улица |
| `grass_tuft` | есть тюнинг и `wind_sway .12` | **получателей нет** | — |
| `roof` | `snow_coverage .88`, `snow_sparkle .45`, `cell_jitter .30` | `Act1ConnectedWorld.cs:2060,2087,2151` (4) | улица |
| `roof_metal` | `snow_coverage .88`, `wet_grade .65` | `Act1ConnectedWorld.cs:2058,2059,2095`, `StyleBenchmarkZone.cs:1404,1405` (9) | улица |
| `iron` | `metallic_value .65` (`:805`), `finish_grain .10` (`:806-810`) | `Act1ConnectedWorld.cs:2169`, `Act1ConnectedWorld.CulvertVerandaDiscoveries.cs:140,173` (4) | улица/интерьер |
| `enamel` | `finish_grain .035` | прямых factory-вызовов **не найдено** | интерьер |
| `water` | `texture_strength 0.0` (`:860`), `wet_grade .98`, `snowTrampleBlocked` (`:1026`) | `Act1ConnectedWorld.cs:2157,3752-3754`, `StyleBenchmarkZone.cs:1419`, `Act1ConnectedWorld.CulvertVerandaDiscoveries.cs:305` (6) | улица |
| `paper` | **нет**, `_ =>` | `Act1ConnectedWorld.ShopGoods.cs:109`, `Act1ConnectedWorld.MosqueBooks.cs:109,110` (3) | интерьер |
| `glass` | **нет**, `_ =>` | `Act1ConnectedWorld.YardMechanisms.cs:410` (1) | улица |
| `leather` | **нет**, `_ =>` | `Act1ConnectedWorld.PolicePost.Officer.cs:27` (1) | интерьер |
| `painted` | **нет**, `_ =>` | `CarryableProp.Geometry.cs:126` (1) | переносимый предмет |
| `wood_cut` | флаг `cut_wood_end` (`:797`), годовые кольца процедурно (`:230-238`) | `Act1ConnectedWorld.cs:2082,2167` (2) | улица |
| `wood_carved` | `snow_coverage .55`, `wet_grade .15` | `Act1ConnectedWorld.cs:3920` (1) | улица |
| `ornament_trim` | `brush_scale .18`, `variation .06`, `texture_strength .88` | **получателей нет** | — |

Пять семейств (`metal`, `paper`, `glass`, `leather`, `painted`) не имеют ни одной
ветки в `ForColor` и получают только дефолты `_ =>`. При этом `metal` — 14 прямых
вызовов: самый частый «железный» surface Актов I не имеет ни карты, ни настроенной
реакции, ни различимой фактуры.

---

## 4. Пять обязательных семейств пилота (шаг 1 VIS-005) — цепочки прослежены

| Цепочка | Source texture | Material | Mesh surface | Runtime owner |
|---|---|---|---|---|
| **Снег (P03, снег у крыльца)** | `res://assets/textures/painterly/urman_s01_v01_basecolor.png` (`PainterlyMaterialLibrary.cs:466`, `texture_scale` 0.5×0.5) | `ShaderMaterial` из `ForColor(hex,"snow_ground")`, ключ `snow_ground:<hex>:exposed` (`:773`); флаги `snow_material`/`trample_ground_surface`/`has_snow_micro` (`:1023-1049`) | `MeshInstance3D.MaterialOverride` / `SetSurfaceOverrideMaterial` | `RegradeAct1DaylightKitMaterials`: `Act1ConnectedWorld.cs:2122-2125` (`DampEarth`, `DampEarthDark`, `PathDirt`, `LeafLitter`), `:2132` (`AB_terrain`), `:2134-2137` (`AB_earth*`), `:2180` (`WetSheen`), `:2121` (`FapPuddleWater`→ice); запись `Act1ConnectedWorld.cs:2323` |
| **Доска / ограда (P02)** | `weathered_wood_boards_v2_albedo.png` (`wood_fence`, `:402`) и `urman_w02_v02_basecolor.png` (общая для `wood_fence_vertical/rail/uv`, `:405-407`) | 4 разных ключа кэша: `wood_fence`, `wood_fence_vertical`, `wood_fence_rail`, `wood_fence_uv`; `finishSurface` у всех `wood_fence` (`:786`) | столбы/рейки `AddVisualFenceRun`, `AddVisualBox` | `Act1ConnectedWorld.cs:7840` (`wood_fence` на столбах навеса), `:2057` (`AB_fade_paint`), `:2177` (`DistantFence`), `:3765`; `:9626` (`wood_fence_uv`), `:9651,9663` (`wood_fence_vertical`), `Act1ConnectedWorld.RearYardGate.cs:113` и `Act1ConnectedWorld.BabaiYardSideGate.cs:338` (`wood_fence_rail`); `AgentBAct1ExteriorLayer.cs:1133` |
| **Лицо** | **не painterly**: `assets/textures/characters/mansur_age_v1_albedo.png` (`GeneratedCharacterKitDressing.cs:282-284`); human-v2 — CC0-альбедо из GLB (`:388-397`) | `StandardMaterial3D`, `DiffuseMode=Toon`, `SpecularMode=Disabled`, `Roughness=1`, `Metallic=0`, `MetallicSpecular=0` (`:275-279`, `:390-395`) | `*_Head_*`, `*_FaceEyes_*` | `GeneratedCharacterKitDressing.ApplyPainterlyMaterial` (`:252`), `.ApplyHumanMaterials` (`:371`) |
| **Ткань персонажа (P05)** | `res://assets/textures/painterly/old_fabric_v3_albedo.png` (`:457`, scale 2.0×2.0) | `ForColor(hex,"cloth",sheltered:true)` (`GeneratedCharacterKitDressing.cs:360`) или `ForMovingCloth(hex)` (`PainterlyMaterialLibrary.cs:697-707`) | `*_Coat*`, `*_Sleeve*`, `*_Body_*`, `*_Apron*`, `*_Scarf*` | `GeneratedCharacterKitDressing.cs:359`, `:385`; `FirstPersonController.Footwear.cs:46-47`; `Act1ConnectedWorld.MosqueImamDress.cs:53-56`; `Act1ConnectedWorld.MosqueClothing.cs:52`; `Act1ConnectedWorld.PublicBuildings.cs:304,306` |
| **Окно** | `frost_window_v1_albedo.png` — **два разных владельца**: `VillageWindowMaterials.cs:41` (свой шейдер `:8-27`) и `WinterTextures["frost_window"]` (`PainterlyMaterialLibrary.cs:475`) | `VillageWindowMaterials.For()` — 8 общих `ShaderMaterial` (`:29`, `:32-43`); `ForColor(…,"frost_window")` — флаг `edge_frost` (`:796`) | `WindowGlass*` | `Act1ConnectedWorld.cs:6122`, `TimberHomeStyle.cs:133,148`, `VillageHouseholdDirector.cs:71`; surface `frost_window` — только `Act1ConnectedWorld.SovkhozSquare.cs:192` (и `Act1ConnectedWorld.FarBank.cs:98` через helper) |

**Важно для VIS-065/066:** из пяти семейств пилота два вообще не проходят через
painterly-шейдер так, как ожидалось: лицо — `StandardMaterial3D` + Toon, окно —
отдельным шейдером `VillageWindowMaterials`. Единая «painterly material response»
для них сейчас не достигается правкой `PainterlyMaterialLibrary`.

---

## 5. Дублирование и перекрытые ветки (VIS-038)

### 5.1 Одна PNG на несколько семейств (7 карт / 17 семейств)

| PNG | Семейства | Чем реально различаются |
|---|---|---|
| `old_fabric_v3_albedo.png` | `cloth`, `fabric`, `fabric_pattern` | `cloth` и `fabric` — **параметрически идентичны** (5.2); `fabric_pattern` отличается `texture_scale` 2.6 и `brush_scale 0.18` |
| `urman_w01_v01_basecolor.png` | `wood_facade`, `wood_log_uv` | только `authored_uv_texture` (`:804`); `finishSurface` у обоих `wood_facade` (`:782-791`) |
| `urman_w02_v02_basecolor.png` | `wood_fence_rail`, `wood_fence_uv`, `wood_fence_vertical` | только флаги проекции (`:798-804`); BRDF одинаковый |
| `weathered_wood_boards_v3_albedo.png` | `wood_furniture` (0.95), `wood_prop` (0.90) | `texture_scale` и `snow_coverage` (0 vs 0.55) |
| `damp_earth_v3_albedo.png` | `earth`, `wet_road` | `vertex_pigment` у `wet_road` (`:812`), `roughness/specular/wet` разные |
| `damp_earth_v5_albedo.png` | `terrain`, `wet_ground` | `terrain` — получателей нет (§5.3) |
| `hay_fibers_v1_albedo.png` | `hay_fibers`, `hay_bundle` | `authored_uv_texture` vs `local_wood_texture`+`upright_texture` |

### 5.2 Полностью дублирующаяся пара `cloth` / `fabric`

Совпадает всё, что пишет `ForColor`: путь карты (`:437`, `:457`), `texture_scale`
2.0×2.0, `brush_scale 0.24`, `variation 0.08`, `texture_strength 0.88`,
`snow_coverage 0.30`, `snow_sparkle 0`, `cell_jitter 0`,
`surfaceGrade (0.98 / 0.04 / 0.01)`, `wind_sway 0`, `leaf_transmission 0`,
все флаги проекции `false`. Различается только **строка ключа кэша** (`:773`):
движок держит два байтово- одинаковых `ShaderMaterial` и два material state там,
где достаточно одного. Это ровно критерий VIS-038 «0 новых уникальных материалов
для визуально одинакового класса».

### 5.3 Мёртвые ветки (не удалять до подтверждения — шаг 3 VIS-005)

| Что | Где | Состояние |
|---|---|---|
| `cloth_table` + карта `urman_t01_v02_basecolor.png` | `PainterlyMaterialLibrary.cs:416`, `authored_uv_texture` `:804`, алиас `:789` | получателей нет |
| `terrain` + карта `damp_earth_v5_albedo.png` | `:425`, `vertex_pigment` `:812`, `cell_jitter .20` | получателей нет |
| `grass_tuft` | `wind_sway .12` (`:991`), `snow_coverage .72`, `wet_grade .45` | получателей нет |
| `ornament_trim` | тюнинг `:818`, `:840`, `:860`, `:943` | получателей нет |
| `wattle` + **отсутствующий** `wattle_weave_v1_albedo.png` | `:474` | получателей нет и файла нет |
| `ground_darken` | объявлен `:76`, применяется `:250-251`, всегда пишется `0f` (`:888`, и ещё `:704`) | **вся ветка мертва** |
| `Sheltered(Material)` (`:745-757`) | разбор `parts[1]`/`parts[0]` из ключа формата `{surface}:{hex}:{exposed}` (`:773`) | порядок индексов не совпадает с форматом: `parts[0]` — surface, `parts[1]` — hex, а вызов `ForColor(parts[1], parts[0], …)` меняет их местами. Ветка не может вернуть корректный sheltered-вариант |

### 5.4 Карты на диске без привязки (32) — «уже существующие решения» (K02/K03)

Среди них ровно те, которых не хватает семействам из §3:
`bark_birch_v1/v2`, `leaf_birch_v1/v2`, `grass_verge_v1/v2`, `roof_slate_v1`,
`roof_shingle_v3`, `roof_metal_v2`, `ornament_trim_v1_albedo.png`,
`wood_carved_gate_v1_albedo.png`, `fabric_chit_v1_albedo.png`,
`wall_institution_v1_albedo.png`, `snow_road_v1_albedo.png`, `snow_fresh_v1/v2`,
а также прежние поколения: `aged_plaster`, `aged_plaster_v2`, `damp_earth`,
`damp_earth_v2/v4/v6`, `weathered_wood_boards`, `weathered_wood_boards_v5/v6`,
`mossy_stone_v2`, `old_fabric_v2`, `pine_foliage`, `pine_foliage_v3`,
`log_wall_v2`, `wallpaper_old_v2`, `ice_patch_v2`, `bark_pine_v1` (занят как
`wood_bark`), `carpet_palas_v1` (занят), `snow_micro_response/normal` (заняты).

**Вывод для заказа ассетов:** семействам `bark_birch`, `leaf_birch`, `grass`,
`roof`, `roof_metal`, `ornament_trim`, `wood_carved` карта не «нужна с нуля» —
кандидаты уже лежат в репозитории и не подключены. Новые PNG для них заказывать
нельзя до сверки с этим списком (прямое требование VIS-005 «нельзя»).

---

## 6. Пять параллельных регистров «имя материала GLB → painterly»

Один и тот же `ResourceName` перекрашивается в разных местах по-разному; все пять
пишут **один и тот же слот** `SurfaceOverrideMaterial`, читая
`mesh.Mesh.SurfaceGetMaterial(surface)?.ResourceName`
(`Act1ConnectedWorld.cs:2246-2247`, `:3779-3780`).

| # | Регистр | Файл:строка | Порядок применения |
|---|---|---|---|
| 1 | `RebindWetVillageRoadMaterials` | `Act1ConnectedWorld.cs:3736` (запись `:3786`) | из `BuildAct1WetVillageRoadKit`, вызов `:1206` |
| 2 | `AgentBKitMaterials.Map` | `AgentBKitMaterials.cs:14-55` (запись `:138`) | при `InstantiateKit` из `BuildAgentBExteriorWorld` → `Act1ConnectedWorld.cs:1212` |
| 3 | `RegradeKaraEdgeMaterials` | `Act1ConnectedWorld.cs:3693` (запись `:3727`) | внутри `BuildAct1AuthoredExteriorKit` (`:3795`) |
| 4 | `StyleBenchmarkZone.RegradeAuthoredKitMaterials` | `StyleBenchmarkZone.cs:1358`, три варианта карты `:1362`, `:1390`, `:1422`, запись `:1484` | при сборке зоны-стенда |
| 5 | **`RegradeAct1DaylightKitMaterials`** | `Act1ConnectedWorld.cs:2045`, цикл `:2228`, чтение имени `:2246`, запись `:2323` | `ApplyAct1DaylightPresentationPass`, вызов `:1222` — **последний по порядку сборки, он и есть фактический владелец пересечений** |

Доказанные конфликты одинакового ключа:

| Ключ | Регистр A | Регистр B (= побеждающий, №5) |
|---|---|---|
| `AB_log_wall` | `AgentBKitMaterials.cs:42` → `7a674e` / `log_wall` | `Act1ConnectedWorld.cs:2055` → `967d5e` / `log_wall` |
| `AB_fade_paint` | `AgentBKitMaterials.cs:43` → `8e7f66` / `wood_fence` | `Act1ConnectedWorld.cs:2057` → `92816b` / `wood_fence` |
| `AB_roof_iron` | `AgentBKitMaterials.cs:44` → `6f7268` / `roof_metal` | `Act1ConnectedWorld.cs:2058` → `76807a` / `roof_metal` |
| `AB_roof_shingle` | `AgentBKitMaterials.cs:46` → `5d5044` / `roof` | `Act1ConnectedWorld.cs:2060` → `695b4e` / `roof` |
| `AB_earth` | `AgentBKitMaterials.cs:16` → `4a4136` / **earth** | `Act1ConnectedWorld.cs:2134` → `f0f4f8` / **snow_ground** |
| `AB_road_crown` | `AgentBKitMaterials.cs:20` → `4e564f` / earth | `Act1ConnectedWorld.cs:2138` → `e3e9ee` / snow_trampled |
| `AB_water_dark` | `AgentBKitMaterials.cs:23,73-91` → `StandardMaterial3D` (roughness 0.58, metallicSpecular 0.12) | `Act1ConnectedWorld.cs:2141` → `ForColor("2c3740","ice")` (painterly, `snow_material`, `has_snow_micro`, `snowTrampleBlocked`) |
| `AB_window_warm` | `AgentBKitMaterials.cs:47,84-88` → `EmissionEnergyMultiplier 3.0` | `Act1ConnectedWorld.cs:2061-2069` → `2.0`, другой albedo/emission |
| `DampEarth` | `StyleBenchmarkZone.cs:1365` → `56544a` / earth | `Act1ConnectedWorld.cs:2122` → `f1f5f9` / snow_ground |
| `FapPathEarth` | `StyleBenchmarkZone.cs:1417` → `4f5145` / earth | `Act1ConnectedWorld.cs:2119` → `cbd2d4` / snow_trampled |
| `QuietStone` | `StyleBenchmarkZone.cs:1373` → `62675c` / stone | `Act1ConnectedWorld.cs:2149` → `7e7f73` / stone |
| `DistantFence` | `StyleBenchmarkZone.cs:1386` → `403b34` / **wood** | `Act1ConnectedWorld.cs:2177` → `4f463b` / **wood_fence** (другой `snow_coverage` 0.62 и `cell_jitter` 0.28) |
| `BirchBark` | `StyleBenchmarkZone.cs:1377` → `68705a` / `bark_birch` | `Act1ConnectedWorld.cs:2146` → `68705a` / `bark_birch` (совпадает) |

Шесть конфликтов меняют **семейство**, а не тон (`AB_earth`, `AB_road_crown`,
`DampEarth`, `FapPathEarth`, `DistantFence`, `AB_water_dark`). Именно на них
проверяется запрет VIS-038 «не объединять материалы с разным culling, deform или
alpha»: у победившего `ice` включены `snow_material`/`snowTrampleBlocked`
(`:1023-1026`), у проигравшего `StandardMaterial3D` — нет.

Отдельно: `ForColor("808080")` без surface (`AgentBKitMaterials.cs:71`) — silently
серый fallback для любого неизвестного `AB_*`-имени. Ни одно имя из GLB-китов не
сверено с `Map` автоматически; проверка существует только как исключение в
`InstantiateKit` про коллизию (`:110-114`), не про материал.

---

## 7. Mipmaps, UV-scale, anchor space — сводка фактов

| Что | Значение | Доказательство |
|---|---|---|
| Запрос фильтра в шейдере | `filter_linear_mipmap_anisotropic, repeat_enable` для `albedo_texture` | `PainterlyMaterialLibrary.cs:48` |
| То же для микрорельефа снега | `snow_micro_response`, `snow_micro_normal` | `:92`, `:93` |
| Без mipmap-запроса | `trample_map : hint_default_black, filter_linear, repeat_disable` | `:96` |
| Фактическая генерация mipmap | `mipmaps/generate=true` у 42 из 44 подключаемых PNG; **`false` у `frost_window_v1_albedo.png` и `urman_t07_v01_basecolor.png` (семейство `cloth_towel`)** | `game/assets/textures/painterly/*.import`, ключ `mipmaps/generate` |
| Размер подключаемых карт | 22 × 1254×1254 (не power-of-two), 19 × 1024×1024, 1 × 1024×1536 | `sips -g pixelWidth -g pixelHeight` |
| `texture_scale` | одна пара на семейство: от 0.25×0.40 (`carpet`, `:446`) до 2.6×2.6 (`fabric_pattern`, `:445`) и отрицательной V у `wallpaper` (1.1, −1.1, `:448`) | `:386-458`, запись `:1008`/`:1019` |
| Anchor space | §8 и `material_anchor_contract_RU.md` | — |
| Прозрачность | только `CutoutShader` (`ALPHA` + `ALPHA_SCISSOR_THRESHOLD 0.2`, `:378-384`) и `TwoSidedPainterlyShader` (`cull_disabled`, `:370-377`). Единственный потребитель cutout — `AgentBAct1ExteriorLayer.cs:103` (семейство `foliage`) | — |
| Тени: шейдер без деформации | `RigidPainterlyShader` выбирается автоматически при `wind_sway==0 && !has_snow_micro && !trample_ground_surface && !snowMaterial` (`:1073-1078`); readback — `UsesSharedShadowMaterial` (`:675-676`), потребитель `Act1DemoRoot.RenderDiagnostics.cs:256` | — |

---

## 8. Короткая таблица anchor space (детали — `material_anchor_contract_RU.md`)

| Слот шейдера | Пространство | Строка |
|---|---|---|
| `albedo_texture` (по умолчанию) | world-трипланар по `world_position`/`world_normal` | `:211-217`, `:110-118` |
| `albedo_texture` при `authored_uv_texture` | UV | `:214-215` |
| `albedo_texture` при `local_wood_texture` / `local_floor_texture` / `local_fence_rail` | object-local (перестановка `xzy`/`zyx`) | `:152-155`, `:211-212` |
| `edge_frost` (`frost_window`) | UV | `:163-169` |
| `cut_wood_end` (`wood_cut`) | UV сечения | `:230-238` |
| `stroke` / `broad_stroke` / `wash` / `macro_stain` / `cell_tint` | world (или UV при `bound_uv_pigment`) | `:195`, `:199-209`, `:246-247`, `:256`, `:131-140` |
| снежное одеяло | `world_normal.y` + `world_position.xz` | `:284-296` |
| `snow_micro_*`, `trample_map` | world_position.xz (мировое окно маски) | `:11-21`, `:142-149`, `:300-317`, `:330-344` |
| `finish_grain` | world_position | `:268-275` |
| `ground_darken` | world_position.y; всегда 0 → ветка мертва | `:250-251`, `:888` |
| `wet_grade` | скаляр без координат + `upward` по world-нормали | `:210`, `:252` |
| ветер `wind_sway` | фаза по world_position.x/z, смещение по **local** `VERTEX.y` | `:22-28` |

---

## 9. Что из этого следует для VIS-065/066 (только пробелы, без решений)

1. 17 семейств не имеют альбедо; для 7 из них готовые PNG уже лежат в репозитории (§5.4).
2. 5 семейств не имеют ни одного получателя — заказывать для них карты нельзя (§5.3).
3. 2 семейства ссылаются на отсутствующие PNG (`rowan_berries`, `wattle`);
   `rowan_berries` реально используется (`AgentBKitMaterials.cs:50`,
   `AgentBAct1ExteriorLayer.cs:1719`) и сейчас рисуется плоско, молча (§2.4).
4. `cloth` и `fabric` — одно и то же material state дважды (§5.2).
5. Новый ассет нельзя считать подключённым, если он не прошёл проверку против пяти
   регистров §6; победитель — `RegradeAct1DaylightKitMaterials` (`Act1ConnectedWorld.cs:2045`).
6. Для пилота P05 лицо и окно вообще вне painterly-шейдера (§4), а ткань —
   world-пространство (`material_anchor_contract_RU.md`).
7. `metal`/`paper`/`glass`/`leather`/`painted` не имеют ни одной настроенной ветки
   (§3) и не могут получить «painterly material response» без правки `ForColor`.
