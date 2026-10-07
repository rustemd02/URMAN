# VIS-007 — Контракт опоры и координат подвижных материалов

Дата среза: 2026-10-07, 17:00. Только статическое чтение. HEAD `612f9bc4`, дерево грязное
(параллельно правятся `PainterlyMaterialLibrary.cs` и `Act1ConnectedWorld.cs`);
номера строк — срез, навигация — по именам методов, uniform-ов и ключей.

Карточка требует: (1) support query для земли/настила/ступени, (2) разделение
rigid-local / skinned-UV / world-ground, (3) запрет новой geometry/material-ветки без
выбора режима. Ниже — фактическая карта и список нарушений для последующих VIS-009/031/092.

---

## 1. Support query: кто реально отвечает за «высота, нормаль, заглубление»

### 1.1 Земля (внешний рельеф) — один владелец

`AgentBAct1HeightField.CollisionGround(float x, float z)` —
`game/scripts/experiments/agent_b_act1/AgentBAct1HeightField.cs:549-577`.

| Свойство | Значение | Строка |
|---|---|---|
| Источник высоты | **треугольники коллайдера**, не аналитическое поле | `:555` `CollisionFacesValue` (`:128`), обход 3×3 ячеек `:558-560` |
| Сетка | `Step = 2` м, `MinX=-64`, `MaxX=150`, `MinZ=-152`, `MaxZ=232` | `:12-28` |
| Джиттер вершин | ≤ одной ячейки; `GroundWithJitter` (`:539`) и `BuildTerrainFaces` (`:579-615`) | `:544`, `:590-595` |
| Нормаль | **не отдаётся** — функция возвращает только `float Y` | подпись `:549` |
| Провал внутри поля | исключение `Terrain triangle missing at x, z` | `:573` |
| Вне поля | fallback на аналитический `Ground(x, z)` | `:576` (и `:448`) |

Нормаль опорного срезает только `VehicleSnowTracks`: 4 отсчёта `CollisionGround`
(`:166,168,177,178`) и допуск `GroundTolerance = .25f` (`:50`, проверка `:167,169`).
Единственный «нормальный» owner вне транспорта — шейдерная снежная микронормаль
(`PainterlyMaterialLibrary.cs:303-316`), она не является support query.

### 1.2 Фактические заглубления в коде (готового единообразного допуска нет)

| Offset | Кто | Строка |
|---|---|---|
| `−0.04` м | connected-мир, якоря/детали | `Act1ConnectedWorld.cs:1054`, `:2712`, `:3352`, `:3940`, `:5539`, `:7830` |
| `−0.025` м | придорожная посадка | `Act1ConnectedWorld.RoadsideGrounding.cs:71` |
| `−0.02` м | граница | `Act1ConnectedWorld.cs:3266` |
| `−0.015` м | min по точкам объекта | `Act1ConnectedWorld.RoadsideGrounding.cs:134` |
| `−0.01` м | NPC/живность | `Act1ConnectedWorld.cs:1499` (`LifeGround`) |
| `0` м | точка остановки | `Act1ConnectedWorld.ArrivalStop.cs:15` |
| `+0.05` м | дренаж | `Act1ConnectedWorld.cs:3025` |
| `+lift` | переменный | `Act1ConnectedWorld.cs:5941` |
| `+0.025` м | пух снежной пыли над опорой | `SnowTrampleField.cs:142` |
| `0.010` м | снятый зазор подошв human-kit | `GeneratedCharacterKitDressing.cs:427` (`exportedSoleClearance`) |

Предложенный в карточке допуск (щель ≤0.01 м, заглубление ≤0.02 м) **уже нарушен**
шестью вызовами `−0.04`/`−0.025` (это больше 0.02) и `+0.05`. Это не баг сам по себе —
это незарегистрированные исключения, которые карточка требует записывать по объекту.

### 1.3 Второй, независимый support query — источник расхождения

`KitPlacementTakeover.ApplyAuthored` (`game/scripts/KitPlacementTakeover.cs:83-119`)
при наличии `groundingReferenceXZ` берёт **аналитический** `AgentBAct1HeightField.Ground`
(`:94-95`), а не `CollisionGround`. То же делает `Act1ConnectedWorld.OverlapCleanup.cs:67-68`.
Разница между ними — ровно джиттер сетки (`Ground` `:448` против
`GroundWithJitter` `:539` / `CollisionGround` `:549`), то есть авторский участок
приземляется по гладкому полю, а презентация — по фактическим треугольникам.
Сводка автора: `docs/production/ai_visual_reset_2026-10-07/_research/B_render_pipeline_map_RU.md`
§3.2 этот раскол не описывает.

### 1.4 Настил, пол, ступень — другие владельцы, HeightField не трогающие

| Поверхность | Владелец опоры | Доказательство |
|---|---|---|
| Полs дома/ФАПа | `StyleBenchmarkInteriorFactory` — плоскости `Block(..., "wood_floor_painted")` | `StyleBenchmarkInteriorFactory.cs:58`, `StyleBenchmarkInteriorFactory.Rooms.cs:68,102`, `GeneratedModularKitDressing.cs:254` |
| Порог/веранда/ covered | meta `supportOwner` | `Act1ConnectedWorld.BoardCrossing.cs:184`, `Act1ConnectedWorld.PublicBuildings.cs:87`, `Act1ConnectedWorld.WinterRoads.cs:87`, `AgentBAct1ExteriorLayer.cs:357,465,519`, `AgentBAct1ExteriorLayer.KaraGradeSupports.cs:55` |
| Предмет на подносе | `supportOwner` = путь к ноде | `Act1ConnectedWorld.ShopUses.cs:63,69` |
| Ступени/лестницы | **не найдено** отдельного support query; обход — существующие коллайдеры и `RayCast3D` | `SnowTrampleField.cs:148-158` (ray вниз от камеры) — единственный лучевой запрос высоты |

`supportOwner` — уже существующая конвенция (10+ мест), но она **не** нормализована:
часть значений — имя ноды-владельца (`"AgentB_TerrainCollision"`), часть — путь
(`support.GetPath()`). `SnowTrampleField.BindSurfaces` сопоставляет строго строку
`"AgentB_TerrainCollision"` (`:242`), поэтому рельеф с путевым `supportOwner`
в деформацию снега не попадает.

### 1.5 Требования карточки vs факт

| Требование | Факт |
|---|---|
| «Не world Y=0» | выполнено: нигде нет присваивания `Y = 0` как опора; все якоря идут через `CollisionGround`/meta `supportOwner` (§1.1, §1.4) |
| owner, нормаль, высота, допустимое заглубление | высота — есть (§1.1), нормаль — **не отдаётся** (§1.1), заглубление — 10 разных значений без реестра (§1.2) |
| «Для skinned-тела local position без rest-space недостаточен» | подтверждается кодом: `vertex()` считает `world_position` из `MODEL_MATRIX × VERTEX` **до** любых смещений (`PainterlyMaterialLibrary.cs:156`), а скиннинг накладывается после (§3.2) |

---

## 2. Разделение трёх режимов (шаг 2 карточки)

| Режим | Как включается | Кто реально использует |
|---|---|---|
| **world-ground** (по умолчанию) | никаких флагов; `albedo_position = world_position`, `albedo_normal = world_normal` | `PainterlyMaterialLibrary.cs:211-212`, `:110-118` (трипланар), `:195`+`:206-209`+`:246-247`+`:256` (мазок/пятно/клеточный тинт), `:284-296` (снежное одеяло), `:268-275` (`finish_grain`) |
| **rigid-local** | `local_wood_texture` (`:801`), `local_floor_texture` (`:802`), `local_fence_rail` (`:803`); перестановка осей `:152-155`, `:154-155` | семейства `hay_bundle`, `wood_floor_painted`, `wood_fence_vertical`, `wood_fence_rail`; плюс `ForLocalWoodPiece` (`:709-727`) для переносимых досок |
| **skinned-UV** | `authored_uv_texture` (`:804`) + `bound_uv_pigment` (только `ForMovingCloth`, `:701-703`) | семейства `hay_fibers`, `cloth_table`, `cloth_curtain`, `wood_fence_uv`, `wood_log_uv` (только альбедо по UV, **без** UV-мазка), и `ForMovingCloth` |
| UV-частично | `edge_frost` → `texture(albedo_texture, UV)` (`:163-169`); `cut_wood_end` → UV сечения (`:230-238`); `soft_path_edges` → `UV.x` (`:318-329`); `CutoutShader` → `ALPHA = texture(cutout_texture, UV).a` (`:383`) | `frost_window`, `wood_cut`, `ForPath` (`:759-768`), `ForCutout` (`:654-662`) |

**Ключевой факт:** `authored_uv_texture` сам по себе переводит в UV **только альбедо**.
Мазок, пятно, клеточный тинт и снежное одеяло остаются world-space (`:195`, `:284-296`),
потому что `pigment_position` уходит в UV лишь при отдельном флаге `bound_uv_pigment`,
который ставит **только** `ForMovingCloth` (`:703`). Ни одно семейство в `ForColor`
этот флаг не получает (`:804` задаёт `authored_uv_texture`, `bound_uv_pigment` там отсутствует).

---

## 3. Подвижные поверхности: что в каком пространстве

### 3.1 Одежда и тела персонажей (skinned)

| Потребитель | Что вызывает | Пространство альбедо | Пространство мазка/тинта | Скиннинг? |
|---|---|---|---|---|
| 8 процедурных префиксов (`Mansur`, `Gulsina`, `TimurHazrat`, `CouncilElder`, `Naila`, `ArchiveClerk`, `PactKeeper`, …) | `GeneratedCharacterKitDressing.cs:360` → `ForColor(color, "cloth", sheltered:true)` | **world-трипланар** | **world** | да (`:177` LoopMode, `:179` Play idle; comment `:180-183` про exported skin) |
| 8 human-v2 префиксов (`Mansur`, `Gulsina`, `Naila`, `TimurHazrat`, `Rinat`, `Resident`, `Tamara`, `PhoneGuy`) | `GeneratedCharacterKitDressing.cs:386` → `ForColor(parts[0], "cloth", sheltered:true)` | **world-трипланар** | **world** | да |
| **Alsu** | `:385` → `ForMovingCloth` | UV | UV | да |
| **CouncilWitness** (в т.ч. игрок `Player`? см. ниже) | `:359` → `ForMovingCloth` | UV | UV | да |
| Ботинки игрока (домашние) | `FirstPersonController.Footwear.cs:46-47` → `ForMovingCloth("6d6c64")` | UV | UV | да (`FirstPersonController.Body.cs:83` «articulated coat, knee and ankle skinning») |
| Верхняя часть пальто игрока | `FirstPersonController.Body.cs:143-156` — собственная кость `AidarUpperCoat`, material из `CouncilWitness_*` | зависит от п. выше | — | да (ручной skin) |
| Одеяние Тимура хәзрәта | `Act1ConnectedWorld.MosqueImamDress.cs:53-56`, применение `:65-67`, `:85`, `:120` → `ForColor(...,"cloth")` | **world-трипланар** | **world** | да (перетекстур видимых мешей скелетного NPC) |
| Продавец Разилә | `Act1ConnectedWorld.PublicBuildings.cs:300-306` — `_Body_`, `_Sleeve`, `_Coat`, `Apron` → `ForColor(...,"cloth",sheltered:true)` | **world-трипланар** | **world** | да, анимируется `AnimationPlayer` |
| Обувные чехлы в мечети | `Act1ConnectedWorld.MosqueClothing.cs:52,56` | **world-трипланар** | **world** | нет (статичные покрытия) |
| Ковёр/ткань в мечети | `Act1ConnectedWorld.MosqueCarpets.cs:270` — `fabric` | **world-трипланар** | **world** | нет |

Итог: из 9 human-kit префиксов (`GeneratedCharacterKitDressing.cs:21`) UV-режим получает
**один** (Alsu), из процедурных — **один** (CouncilWitness). Остальная одежда — world-space
на деформирующихся mesh.

### 3.2 Почему именно это дефект (механика шейдера)

`vertex()` (`PainterlyMaterialLibrary.cs:151-159`) вычисляет
`world_position = (MODEL_MATRIX * vec4(VERTEX,1)).xyz` (`:156`), где `MODEL_MATRIX` —
трансформ **ноды**, а не костей. `fragment()` берёт из него и `triplanar_albedo`
(`:211-217` → `:110-118`), и `stroke`/`macro_stain`/`cell_tint` (`:195`, `:246-247`, `:256`).
Следовательно узор держится за точку мира, а не за ткань: на сгибе локтя или подоле
пальто картинка течёт по поверхности. Плюс `cell_tint` квантован по
`floor(p.xz/6)` (`:132`), поэтому при ходьбе NPC цветовой тинт переключается
ступенчато каждые 6 м мира.

### 3.3 Снежная слеживающая маска (trample) — мировая, правильно

| Аспект | Значение | Строка |
|---|---|---|
| Пространство | world XZ, окно 24 м вокруг игрока | `SnowTrampleField.cs:12` (`WindowExtent = 24f`), `:180` origin, `:215` `SetSnowTrample(_texture, origin, WindowExtent)` |
| Выборка | `uv = (world.xz − origin)/extent`, вне `[0,1]` — ноль | `PainterlyMaterialLibrary.cs:144-145` |
| Support в маске | канал B = `support·coverage`, A = coverage; `support = B/A` | `SnowTrampleField.cs:209`, `PainterlyMaterialLibrary.cs:147` |
| Допуск по высоте | `abs(world.y − support) < 0.12` м, иначе след не действует | `PainterlyMaterialLibrary.cs:148` |
| Вершинное смещение | `VERTEX += transpose(MODEL_NORMAL_MATRIX) * vec3(0, pressed_height, 0)` | `:18-21` |
| Нормаль следа | центральные разности по texel в world XZ | `:330-344` |
| Границы глубины | `depression ∈ [−0.05, 0]`, `raised ∈ [0, 0.02]`, иначе исключение | `SnowTrampleField.cs:204-207` |
| Глубина отпечатка | `packed ? 0.010 : 0.035` м | `:106` |
| Деформация самой земли | `GroundSurface` subdivite существующего `ArrayMesh` для `Terrain_Main`/`Road_*` c `supportOwner == "AgentB_TerrainCollision"` | `:236-243`, `:248-270` |
| Кто запрещает воде | meta `snowTrampleBlocked` для `water`/`ice` | `PainterlyMaterialLibrary.cs:1026`, читается `SnowTrampleField.cs:238` |

Это **единственная** подвижная поверхность, уже соответствующая контракту «world-ground»
по замыслу; для неё мировое пространство корректно.

### 3.4 Ветовой наклон (wind sway) — смешанное пространство

```
phase  = TIME*1.6 + world_position.x*0.55 + world_position.z*0.4      (:23)
reach  = max(VERTEX.y, 0.0)                                            (:25)
VERTEX.x += gust * wind_sway * reach;  VERTEX.z += gust * wind_sway * 0.6 * reach   (:26-27)
```

Фаза — мировая (соседние деревья дышат согласно), амплитуда — **локальная** по `VERTEX.y`,
то естьpivot'ом считается локальная плоскость `y = 0` меши. Для китов, у которых
локальное начало не у комля, наклон идёт вокруг неправильной точки. Владелец значения —
`ForColor` (`:991-998`: `foliage .05`, `leaf_birch .07`, `grass_tuft .12`, остальные 0),
глобальный вкл/выкл — `SetWindMotion` (`:543-558`), единственный вызов —
`FirstPersonController.cs:140` (`!settings.ReducedMotion`).

### 3.5 Мороз на окнах — UV, корректно

| Слой | Выборка | Строка |
|---|---|---|
| `VillageWindowMaterials` (8 общих ресурсов) | `texture(frost_map, UV)`, складки по `UV.x`, кромка по `min(UV)` | `VillageWindowMaterials.cs:16-21` |
| `edge_frost` в painterly | `texture(albedo_texture, UV)` | `PainterlyMaterialLibrary.cs:163-169` |

Единственная претензия — **mipmap**: `frost_map` объявлен без фильтра-квалификатора
(`VillageWindowMaterials.cs:11`), а `albedo_texture` — с
`filter_linear_mipmap_anisotropic` (`:48`), при этом
`frost_window_v1_albedo.png.import` имеет `mipmaps/generate=false`
(см. `material_consumers_RU.md` §7).

### 3.6 Вода / мокрое

| Что | Факт | Строка |
|---|---|---|
| Анимированной воды (UV-скролл, normal-scroll, wave vertex) | **не найдено** — `water`-семейство не имеет ни альбедо, ни TIME-членов | `PainterlyMaterialLibrary.cs:943-989` (`water` → `texture_strength 0`, `wet_grade .98`), uniform-ов `water_*` нет |
| «Мокрость» как отклик | скаляр `wet_grade` + модуляция по world-нормали | `:210`, `:252`, `:260`, `:191-192` |
| Лужи | отдельные поверхности из `PainterlyEnvironmentDetails.AddPuddleCluster`, материал `water`/`ice` | `Act1ConnectedWorld.cs:3752-3754`, `StyleBenchmarkZone.cs:1419`; посадка по `CollisionGround` |
| Следы транспорта | свой query по `CollisionGround`, допуск `GroundTolerance = .25` | `VehicleSnowTracks.cs:50,166-178` |

---

## 4. Требуемый контракт (формулировка для VIS-009/031/092)

Правило, которое следует из кода и стиля (deformable → UV/local):

| Класс поверхности | Обязательный режим | Обязательные флаги | Куда писать |
|---|---|---|---|
| **A. Skinned / деформируемая одежда, волосы, ткань на движущемся носителе** | skinned-UV | `authored_uv_texture = true` **и** `bound_uv_pigment = true`; снежное одеяло только явно (`snow_coverage = 0`, `sheltered: true`) | новая фабрика `ForDeformingSurface(hex, surface)`; `ForMovingCloth` (`:697`) остаётся её частным случаем |
| **B. Ригидная деталь с известной осью волокон (рейка, брус, доска, сено)** | rigid-local | `local_wood_texture` / `local_floor_texture` / `local_fence_rail` + `local_wood_offset`; `bound_uv_pigment` **обязателен**, иначе мазок остаётся мировым | `ForLocalWoodPiece` (`:709`) — расширить |
| **C. Поверхность, привязанная к земле (рельеф, дорога, снег, следы)** | world-ground | `trample_ground_surface`, `has_snow_micro`, `snow_material` по семейству | как сейчас |
| **D. Планарное остекление/занавеска** | UV | `edge_frost` либо `authored_uv_texture` | как сейчас |
| **E. Переносимый/двигаемый предмет** | rigid-local | как B + `cell_jitter = 0` и `ground_darken = 0`, чтобы тинт не менялся при проходе 6-метровой мировой ячейки | запрет world-`pigment_position` |

Допуск опоры (по образцу уже существующих guards):
щель ≤ 0.01 м, заглубление ≤ 0.02 м; каждый выход за них — запись с именем объекта
(по образцу `SnowTrampleField.cs:206-207`, который падает, а не молчит).

Запрет (шаг 3 карточки): новая mesh/material-ветка не принимается без явного выбора
одного из A–E. Технически это означает, что `ForColor` должен бросать исключение для
`surface`, не входящего ни в один список, — сейчас неизвестный surface молча получает
дефолты `_ =>` и мировое пространство (`:818`, `:840`, `:860`, `:905`, `:922`, `:943`).

---

## 5. Список нарушений для исправления (порядок = предлагаемая приоритетность)

1. **Мировой трипланар на skinned-одежде всех NPC, кроме Alsu и CouncilWitness.**
   `GeneratedCharacterKitDressing.cs:360` и `:386` зовут `ForColor(..., "cloth")`;
   `cloth` не попадает в `authored_uv_texture` (`PainterlyMaterialLibrary.cs:804`),
   поэтому альбедо берётся из `world_position` (`:211-217`, `:110-118`).
   Исправление: `ForDeformingSurface` (режим A) либо распространение `ForMovingCloth`.
2. **World-space мазок/пятно/клеточный тинт на той же одежде.**
   `bound_uv_pigment` ставится только в `ForMovingCloth` (`:703`); для всех остальных
   `pigment_position = world_position` (`:195`) → `stroke`/`wash` (`:199-209`),
   `macro_stain` (`:246-247`), `cell_tint` (`:131-140`, `:256`) текут по телу и
   ступенчато меняются каждые 6 м.
3. **Адресные перетекстуры на анимируемых NPC, обходящие `GeneratedCharacterKitDressing`.**
   `Act1ConnectedWorld.MosqueImamDress.cs:53-56` → применение `:65-67` (пальто, полы,
   брюки) и `Act1ConnectedWorld.PublicBuildings.cs:304,306` (`_Body_`, `_Sleeve`,
   `_Coat`, `Apron`). Эти правки попадают в режим нарушения 1 даже после починки
   фабрики — у них нет owner'а, который бы требовал UV.
4. **Переносимые доски: локальное альбедо, но мировой мазок и мировой тинт.**
   `ForLocalWoodPiece` (`:709-727`) ставит `local_wood_texture` и смещение, но не
   `bound_uv_pigment`; у `wood_prop` `cell_jitter = 0.15` (`:922-940`), у `wood` —
   `0.35`, поэтому при переносе предмет меняет оттенок на пересечении 6-метровой
   мировой ячейки (`:132`). Плюс отдельные carryables и вовсе на мировом режиме:
   `CarryableProp.Geometry.cs:126` (`"painted"`), `Act1ConnectedWorld.cs:1237-1254`
   (`"wood"`, `"metal"`).
5. **Два разных support query для одной земли.**
   Презентация — `CollisionGround` (`AgentBAct1HeightField.cs:549`), авторский plot —
   аналитический `Ground` (`KitPlacementTakeover.cs:94-95`,
   `Act1ConnectedWorld.OverlapCleanup.cs:67-68`). Расхождение = сеточный джиттер
   (`:544`, `:590-595`); отсюда же неровная посадка Kit-объектов на рельеф (кандидат R030).
   Нужно одно: либо `CollisionGround` как единственный support owner, либо явно
   задокументированная поправка.
6. **Отсутствие нормаля в support query.** `CollisionGround` возвращает `float`
   (`AgentBAct1HeightField.cs:549`), поэтому наклонные опоры (откосы, обочины,
   мостовой настил) не могут корректно поставить объект; каждый потребитель импровизирует
   (`VehicleSnowTracks.cs:166-178` — 4 отсчёта; `RoadsideGrounding.cs:134` — min по точкам).
7. **Нереестренные заглубления.** `−0.04` ×6, `−0.025`, `−0.02`, `−0.015`, `−0.01`,
   `+0.05`, `+lift` (§1.2) против предлагаемых «щель ≤0.01 / заглубление ≤0.02».
   Ни одно из них неguarded — в отличие от `SnowTrampleField.cs:206-207`.
8. **`cell_tint` привязан к мировой сетке 6 м и применяется к одежде.**
   `:131-140` + вызов `:256`; для `cloth` авторский `cell_jitter` = 0 (`:922-940`),
   поэтому сегодня дефект не виден на ткани, но включение `cell_jitter` для
   «de-cloning» одежды (естественный соблазн VIS-092) немедленно воспроизведёт
   6-метровые ступени на человеке. Запретить явно.
9. **Снежное одеяло и `snow_cover` по world-нормали без учёта локального крена.**
   `:284-296` (`smoothstep(0.30,0.72, world_normal.y)`), две стороны поправлены только
   по знаку (`:376`). Для рейки с `rollDegrees` (`Act1ConnectedWorld.cs:7973`, `:7981`,
   `:8006`) грань получает одеяло по мировой нормали, а не по фактическому уклону
   самой рейки; совмещать с `local_fence_rail` сейчас нельзя — флаг меняет только
   альбедо (`:152-155`), а не нормаль одеяла.
10. **Неизвестный surface молча получает мировой режим.**
    `ForColor` не валидирует `surface`: любые опечатки (`"woodfence"`, `"clouth"`)
    дают валидный материал в `_ =>`-значениях (`:818`, `:840`, `:860`, `:905`, `:922`, `:943`)
    и `has_albedo_texture = false` (`:1001-1021`). Это ровно то, что шаг 3 карточки
    требует запретить.
11. **`ground_darken` мёртв.** Объявлен и применяется (`:76`, `:250-251`), но всегда
    пишется `0f` (`:888`, `:704`). Контактная тень должна оставаться у SSAO/теней
    (комментарий `:886-887`), поэтому «grounding darken» нельзя использовать как
    решение R003/R030, пока ветка не удалена или не включена осознанно.
12. **`supportOwner` не нормализован.** Строковые имена против путей нод
    (§1.4); `SnowTrampleField.cs:242` сопоставляет ровно `"AgentB_TerrainCollision"`,
    поэтому часть поверхностей с другим `supportOwner` в снежную деформацию не входит.
13. **Окно: UV корректно, mipmap противоречив.** `VillageWindowMaterials.cs:11` и
    `PainterlyMaterialLibrary.cs:48` просят mipmap-фильтрацию,
    `frost_window_v1_albedo.png.import` ставит `mipmaps/generate=false`.

---

## 6. Два требуемых карточкой разбора

### 6.1 Опора стойки (R030-класс) — пример `Act1ConnectedWorld.cs:7830-7840`

| Шаг | Что делает | Пространство / владелец |
|---|---|---|
| 1 | `anchorWorld = canopy.GlobalPosition` | мир, нода |
| 2 | `anchorWorld.Y = CollisionGround(x,z) − 0.04` (`:7830`) | **world XZ → высота опоры**, допуск −0.04 (вне §4) |
| 3 | по 4 углам: `world = canopy.ToGlobal(x,0,z)`, затем снова `CollisionGround − 0.04` (`:7835`) | world; **каждая** нога считается по своей точке (корректно для наклона) |
| 4 | `bottom = canopy.ToLocal(world).Y`, `top = 2.08 + tan(5°)·x − 0.08` | local, высота стойки считается от фактической земли |
| 5 | `AddVisualBox(canopy, "Post", size, center, postColor, "wood_fence")` (`:7838-7840`) | материал **world-ground** (режим C) для объекта, который стоит на наклоне |
| Вывод | Опора корректна (по ногам), но текстура и снежное одеяло рейки/стоки смотрят в мировую сетку; при наклоне 5° (`rollDegrees`, `:7973`) граница одеяла идёт по мировой `y`, а не по нормали стойки → нарушение 9. Нормаль опоры при этом не запрашивается вообще (нарушение 6). |

### 6.2 Mapping пальто (R007-класс) — `GeneratedCharacterKitDressing.cs:349-360`

| Шаг | Что делает | Пространство |
|---|---|---|
| 1 | `surface = isHand/Head/Hair/Face/Ear/Neck ? "" : "cloth"` (`:349-355`) | тело: без surface → `ForColor(hex, "")` — чистый процедурный шейдер, **без карты** (`:1001-1021`) |
| 2 | `mesh.MaterialOverride = prefix=="CouncilWitness" && surface=="cloth" ? ForMovingCloth(color) : ForColor(color, surface, sheltered:true)` (`:358-360`) | для 8 из 9 префиксов — режим **world-ground** на skinned-меше |
| 3 | `ForColor(...,"cloth",…)` → альбедо `old_fabric_v3` по `world_position` (`:457`, `:211-217`), мазок/тинт по `world_position` (`:195`) | нарушения 1 и 2 |
| 4 | `sheltered: true` → `snow_coverage = 0`, `snow_sparkle = 0` (`:921`, `:904`) | контракт «живые персонажи не носят одеяло» выполняется (`:356-357`) |
| 5 | `authored_uv_texture` для `cloth` не ставится (`:804`) | даже если назначить ткани UV-карту, мазок останется мировым |
| Вывод | Пальто требует **одновременно** `authored_uv_texture` и `bound_uv_pigment` (режим A, §4). Существующий `ForMovingCloth` делает это верно, но применяется к двум носителям из 17 |

---

## 7. Неизвестные потребители (пометить как T, не объявлять исправленными)

| Позиция | Почему неизвестен |
|---|---|
| Материалы `enamel`, `ornament_trim`, `grass_tuft`, `terrain`, `cloth_table`, `wattle` | прямых factory-вызовов не найдено (`material_consumers_RU.md` §3, §5.3) |
| Ступени/лестницы | отдельного support query не найдено (§1.4) |
| Нормаль опоры для `CollisionGround` | API не возвращает (§1.1) |
| Вода как подвижная поверхность | анимации не найдено (§3.6) |
| `FirstPersonController.Body.cs:143-156` (`AidarUpperCoat`) | material не назначает, только кости; итоговый режим наследуется от `CouncilWitness`-пути (`GeneratedCharacterKitDressing.cs:358-359`) — требует подтверждения readback-кадром |
