# ledger_TREES — VIS-025, VIS-026, VIS-075, VIS-082, VIS-083, VIS-084

Дата: 2026-10-08. Исполнитель: пакет визуального reset, дорожка «деревья/лес».
Срез репозитория на момент правок: рабочее дерево после итерации 03
(`docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07.md`).
Игра, Blender, станция и git-операции **не запускались** (правила поручения).
Статус всех шести карточек: `CODE_DONE_VERIFY_PENDING` (правки визуальные).

Лесной контракт: геометрически настоящий, атмосферно нечеловеческий; база
blue/green/dirty teal; постоянный красный запрещён; cozy-лес и лес-организм
отклонены; ветвление 1/2/редко 3 порядок; силуэты различимы, не radial sticks;
редкие старые деревья получают длинные тревожные ветви (H3-1); плотность
страшная, но без стены повторов и без «больше деревьев»
(`URMAN_FINAL_STYLE_RECIPE_RU.md` §3.3, §4.6, §8; `REFERENCES_RU.md` H2/H3-1/H4-2/H4-3).

## 0. Корневые причины, найденные в коде (прочитано, не предположено)

| # | Наблюдение | Доказательство |
|---|---|---|
| 1 | План посадки был слеп к породе: `Birch_*` → ровно два силуэта, `Spruce_*`/`Pine_*` → один `WinterSpruce_1`, `Shrub_*` → `WinterBirdCherry_1`. Лиственные Rowan/Willow/Maple/Linden_2 в Act I не сажались ни разу. | `AgentBAct1ExteriorLayer.cs:1820-1827` (BuildDensifiedPlan), `:1407-1417` (PlantFoliage) |
| 2 | Стена леса — регулярная решётка: 9 рядов кольца с шагом 4.0/3.4 м, row 0…8, каждый ряд прямоугольник; внутри ряда 52-74 % — высокие ели четырёх почти одинаковых вариантов. | `AgentBAct1ExteriorLayer.cs:2125-2148` (beltRows/beltInset/beltStep), `:2403-2482` (EmitBelt) |
| 3 | «Плоские ярусы» R025 — это **два** разных меша: прототип `PineA` из `urman_modular_kit.glb` (четыре конуса-яруса, живёт в Act I как `KaraEdgePineA_*`) и процедурная ель `WinterSpruce_*` (ярусная лестница с общим шагом азимута `level*1.17 + branch*tau/3`). | `tools/blender/generate_modular_environment.py:1173-1205` (tiers), `Act1ConnectedWorld.cs:9288-9290` + `:9875-9893` (AddAuthoredPine), `agent_b_foliage.py` (былой угол ярусов) |
| 4 | Кора не читается по породе: у всех не-берёзовых лиственных один `wood_bark`, у хвойных `bark_pine`, то есть разница только в цвете, не в семействе. | `AgentBAct1ExteriorLayer.cs:1714-1734` (RegionalFoliageMaterial) |
| 5 | authored-записи плана в полосе `y <= -86` с летними префиксами молча скрывались (`karaSuppression`), поэтому сектор Кара-Урмана был «пустым», а не разобранным. | `AgentBAct1ExteriorLayer.cs:1498-1501` |
| 6 | `rowan_berries` и `wattle` ссылаются на несуществующие PNG; рябиновое berry-семейство реально используется. | `material_consumers_RU.md` §1, §5.4, `:262-263` |

## 1. VIS-025 — силуэт одной близкой ели: `CODE_DONE_VERIFY_PENDING`

Файлы: `assets/source/blender/agent_b_act1/agent_b_foliage.py`,
`assets/source/blender/urman_winter_pine_v2.py` (новый),
HANDOFF-4 (`PineA`), HANDOFF-5 (роутинг авторских елей).

1. Источник меша установлен (см. §0.3): R025 = `PineA_Crown` (прототип) **и**
   `WinterSpruce_*` (производная библиотека). Правки покрыли оба.
2. `winter_spruce_variant`: добавлен `_spruce_mass_plan(index, level_count, tier)` —
   крона собирается в 3–5 нерегулярных масс: неровные вертикальные extents,
   фиксированный зазор между массами (просвет, где ствол читается), собственный
   азимутный base у каждой массы, золотой шаг внутри массы, доминантная ветвь
   массы выбирается сидом, а не всегда является нижней, последний ярус массы
   укорачивается. Снежная шапка теперь садится на доминанту верхних масс.
   **Число выпускаемых ветвей не изменилось** → бюджет треугольников и
   `_assert_winter_variant` (near 2000–6000, light 600–1500, far 100–400) не
   сдвинуты; far-ярус получает 45 % модуляции, чтобы дальний силуэт остался
   простой елью, а не шумом (шаг 3 карточки).
3. `urman_winter_pine_v2.py` — детерминированный генератор hero-ели, который
   пересаживает связные «острова» needle-карт (`AB_needles`, 462 тр-ка,
   сварка по позиции, UV/материалы не тронуты) в 3–5 масс с просветами,
   heading/reach/droop/lift на массу. Контракты сохранены: имена
   `WinterPine_LOD0/1/2`, слоты `AB_bark`/`AB_needles`, embedded `Leaf_Pine_C`,
   base z = 0, height = 1, bark-only decimation, collision none. Пишет
   `urman_winter_pine_v2_manifest.json` с census (массы, extent, headings,
   reach,Triangles) и `glb_sha256`.
4. Цена: измеряется по `AGENTB_WINTER_VARIANT … triangles=` и census
   манифеста; сравнение baseline → after заносятся в этот ledger после
   ближайшего станционного прогона. Число деревьев не увеличено.

Команда (не выполнялась):

```
blender --background --python assets/source/blender/urman_winter_pine_v2.py -- --root . --seed 20261008
AGENTB_OUT=. blender --background --python assets/source/blender/agent_b_act1/agent_b_foliage.py
```

Проверка кадра: `P16 forest close`, `P16b medium`, рамка R025; в каноне
сравнения — `C5_forest_edge`.

## 2. VIS-026 — разрыв стены леса в одном обязательном виде: `CODE_DONE_VERIFY_PENDING`

Файлы: `game/scripts/experiments/agent_b_act1/AgentBFoliagePlan.cs` (правится
здесь), HANDOFF-1/2/3 — `AgentBAct1ExteriorLayer.cs` (чужой файл, точная правка).

Сектор: **южная кромка, ось Кара-Урмана, 47 м** — от порога z = −92 до
ориентиров z = −119 по улице, куда смотрит `C5_forest_edge`
(`Ground@0,1.7,-40 > Ground@0,2.0,-80`) и P06/P16b.

Авторская композиция в `AgentBFoliagePlan.Entries` (те же корни, что были в
плане; экземпляров не добавлено):

- порог: `(-4.5,-92) WinterSpruce_3` (13.5 м) слева, `(4.6,-94) WinterLinden_1`
  (6.8 м, широкая) справа — деревня «отпускает»;
- ближняя группа неравных высот: `(-5.2,-99) WinterBirch_1`;
- **главный ориентир**: `(5.0,-100) WinterSpruce_5` (24.5 м) — выше всего
  вокруг, читается как точка, по которой рулят;
- **2 просвета**: между z = −100…−112 слева и z = −101…−112 справа не стоит
  ни одного rooted-ствола; там остались только низкие метки (`Fern_2`,
  `GrassTuft_1`, `MossStone_0`, `Stump_0`);
- причина, по которой сектор вообще стал читаемым: летние префиксы `Birch_`/
  `Spruce_` в полосе `y <= -86` скрывались фильтром `karaSuppression`
  (`:1498-1501`); явные `Winter*`-имена под него не попадают, поэтому
  композиция теперь существует, а не прячется.

Просветы в кольце задаются данными из этого файла, а не правкой решётки:

- `AgentBFoliagePlan.IsForestWindow(Vector2 point, int row)` — row 3…6,
  южная полоса `y ∈ [−150, −132]`, два окна x ∈ [−10,−2] и x ∈ [8,16];
- `AgentBFoliagePlan.WindowUnderstory(Vector2 point)` — детерминированный
  низкий ярус (Fern/Sedge/Shrub/MossStone/GrassTuft) вместо ствола; не
  потребляет общий RNG-поток, поэтому ни один другой корень не смещается.

Запреты соблюдены: row 0–2 (линния, закрывающая sky line) и row 7–8 (ряд, к
которому можно подойти, и на котором висит `BuildForestBoundaryCollision` по
`_forestBoundarySegments`) **не трогаются** → вид с обратной стороны не
становится провалом карты, маршрут остаётся закрытым физически. Fog density не
увеличена, случайный re-scatter не выполнялся.

## 3. VIS-075 — редкое семейство тревожных ветвей (H3-1): `CODE_DONE_VERIFY_PENDING`

Файлы: `assets/source/blender/agent_b_act1/agent_b_foliage.py` (новая функция + регистрация),
`AgentBFoliagePlan.cs` (размещение), HANDOFF-1 (`VariantKey`),
пересборка `agentb_foliage_kit.glb`.

- `winter_old_branch_variant(index, height, tier)` — 5 авторских силуэтов
  (12.6 / 15.4 / 11.2 / 16.8 / 13.8 м), near/light/far из одного habit-сида.
  Строго по H3-1 и против «леса-организма»: тяжёлый настоящий ствол с корневым
  вздутием, taper и сломанным лидером; 3–5 длинных сучья разной высоты и
  разного азимута с реальным провисанием и переломом; один низкий «вытянутый»
  сук как тревожный beat; 2-й порядок только на старой половине сучья (2/1
  побега), 3-й порядок — **одна** кисть на всё дерево; снег только на верхних
  поверхностях двух-четырёх верхних сучьев, нижний тревожный сук остаётся
  голым. Никаких корней-рук, лиц, гигантских масштабов, scale x3.
- Материал: новый слот `AB_bark_old` (палитра `3d3a36`, холодная), surface в
  рантайме — существующий `wood_bark` (HANDOFF-3), чтобы не требовать карту
  до времени.
- Бюджет: семейство объявляет свой коридор в `_assert_winter_variant`
  (near 1100–6000, light 360–1500, far 70–400) — лыбое старое дерево
  закономерно легче хвойной кроны; заполнители не добавлялись. Оценка по
  формуле tubes: near ≈ 1.7–1.9 k тр-ков.
- Размещение: 4 акцента, только лесной регистр
  `(-18.5,-112) (19,-121) (-52,-30) (-47,12)`, в `HeroEntries`, **не** в
  уютном ядре деревни. Потолок карточки (~10 % ближних лесных hero-посадок)
  соблюдается с запасом, а VIS-026 в этом же секторе снимает больше
  высоких стволов, чем добавляется.
- Активация только после экспорта кита: `HeroFamilyGeometryAvailable = false`,
  потому что композитор бросает `Missing winter foliage geometry` на неизвестный
  вариант (`:1316-1317`). Порядок: HANDOFF-1 → экспорт кита → flag true →
  прогоны. Без этого шага правка безвредна (ничего не удалено, поведение
  прежнее).

## 4. VIS-082 — стандарт коры и ветвления: `CODE_DONE_VERIFY_PENDING`

Файлы: `agent_b_foliage.py`, `agent_b_common.py` (палитра),
`AgentBKitMaterials.cs` (реестр AB_), HANDOFF-3 (сортировка коры в рантайме).

- Ветвление: `_winter_habit_records` получил таблицу habit (phase, first, span,
  crowd, fork) по породам + сдвиг на номер варианта; азимуты primaries —
  золотой шаг 2.39996 от собственный фазы породы, плюс guard
  `_avoid_repeated_angle(..., minimum=0.55)`: два primary одного ствола не
  сходятся ближе 0.55 rad → «повторяющегося Y-branch» больше нет (критерий
  карточки).
- 3-й порядок стал локально редким: бюджет твигов на primary **численно
  сохранён** (`per_secondary × secondary_count`), но концентрируется на одной
  seed-выбранной вторичной ветви (`bearing_index`), остальные оканчиваются
  голым spur. Это то, что раньше читалось как feather duster.
- Ствол: taper + корневое вздутие `1 + 0.42(1−t)²` и лёгкое утолщение к
  середине, вилка co-dominant (`habit["fork"]`) — ветвь сохраняет 1.55×
  калипера и поднимается к apex, то есть развилка, а не «пять одинаковых
  рук».
- Кора:species-слоты `AB_bark_linden / AB_bark_rowan / AB_bark_willow /
  AB_bark_pine / AB_bark_old / AB_needles_spruce` — палитра, whitelist
  `_assert_winter_variant`, реестр `AgentBKitMaterials.Map`. Все цвета холодные
  серо-бурые; красного baseline нет (стиль-рецепт §8).
- Снег: `_append_snow_cap` теперь допускается только на ветвях, у которых
  средняя станция не ниже точки крепления → свисые ивы/черёмувы остаются
  без белой шапки («snow accents только сверху»).
- 3 отличимых силуэта на hero distance: Birch_1/2/**3**, Linden_1/2/**3**,
  Rowan_1/2/**3**, Willow_1/2/**3** + Maple_1 + OldBranch_1…5.

## 5. VIS-083 — пересобрать лиственные семейства: `CODE_DONE_VERIFY_PENDING`

Файлы: `agent_b_foliage.py`, `AgentBFoliagePlan.cs`.

- 4 породные семьи (birch/linden/rowan/willow) доведены до 3 силуэтных
  вариантов каждая; каждый вариант имеет полную near/light/far тройку с одним
  rooted habit → LOD-переход без исчезновения кроны (VIS-027).
- Разведение по месту в плане: tidy street yard — `WinterBirch_2`,
  `WinterRowan_1`, `WinterMaple_1`; old yard — `WinterLinden_1/2`,
  `WinterWillow_1` у мокрой канавы, `WinterLinden_3` у двоя бабаев;
  forest edge/порог — `WinterBirch_1`, `WinterLinden_1`, `WinterSpruce_3/5`,
  редкие `WinterOldBranch_*` (AS-06 только как акцент).
- Улица перестаёт быть хвойным парком и одновременно «одинаковой стеной»
  леса: ни одна residential-строка не просит ель.
- Не делалось: копирование одного mesh с random scale (правки дают разные
  habit, а не множители).

## 6. VIS-084 — ограничить хвойные: `CODE_DONE_VERIFY_PENDING`

Файлы: `AgentBFoliagePlan.cs`, `urman_winter_pine_v2.py`, HANDOFF-5.

Инвентарь хвойных в плановых корнях после правки:

| Регистр | До | После |
|---|---|---|
| Жилые ряды/улица/дворы (|x| < 58, −124 < z < 46, z > −86) | 2 запроса ели (оба переписывались композитором) | 0 запросов хвойных; порода названа явно |
| Порог Кара (z ≤ −86) | `Spruce_1`, `Spruce_2` (скрыты фильтром) | `WinterSpruce_3` + landmark `WinterSpruce_5` |
| Дальние пояса | решётка кольца 9 рядов | тот же пояс, +2 окна просвета (HANDOFF-2), share лиственных выше |

Массовых хвойных рядов на жилых улицах: 0. Отдельные экземпляры объяснимы
(порог леса, ориентир). Внешний хвойный лес не удалялся.
Шаг 3 карточки («довести snow/bough silhouette существующего pine asset или
заменить производимым вариантом») закрыт `urman_winter_pine_v2.py` с сохранением
внешних имён; альтернатива — routed на `WinterSpruce_*` — в HANDOFF-5.

## 7. HANDOFF (чужие файлы; точная правка, не выполнялась)

Номера строк — срез 08.10.2026; применять по символу, не по номеру.

**HANDOFF-1 (блокирующий для VIS-075)** — `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs`,
`private static string? VariantKey(string nodeName)`, массив `families`:
добавить `"WinterFarOldBranch"`, `"WinterLightOldBranch"`, `"WinterOldBranch"`
в те же группы (Far/Light/near). Без этого `WinterOldBranch_*` бросает
`Missing winter foliage geometry`.

**HANDOFF-2** — тот же файл, `EmitBelt(...)`: сразу после
`InsideMosqueKeepOut` и речного check поставить

```csharp
        if (AgentBFoliagePlan.IsForestWindow(position, row))
        {
            // VIS-026: authored просвет. Средние ряды кольца здесь не ставят
            // вертикальный ствол; низкий ярус остаётся, чтобы пол не выглядел
            // вытоптанной поляной. Ряды 0-2 и 7-8 вне проверки.
            planned.Add((position, AgentBFoliagePlan.WindowUnderstory(position)));
            return;
        }
```

и в `else`-ветке diversity (доля лиственных у дальних рядов) заменить набор
`{ WinterBirch_1, WinterBirch_2, WinterLinden_1, WinterMaple_1, WinterSpruce_1 }`
на `{ WinterBirch_1, WinterBirch_2, WinterLinden_1, WinterLinden_2,
WinterRowan_2, WinterMaple_1, WinterWillow_2 }` c порогами 0.85/0.90/0.93/0.95/
0.97/1.00 (ель из этого коридора убрать). `WinterOldBranch_*` в EmitBelt
**не** добавлять: семейство размещается только авторски.

**HANDOFF-3** — тот же файл, `RegionalFoliageMaterial`: различить кору по
породе, используя только уже существующие painterly-семейства (карты заказаны
в `asset_requests/TREES.md`):

```csharp
        var old = variant.Contains("OldBranch", StringComparison.Ordinal);
        var linden = variant.Contains("Linden", StringComparison.Ordinal) || old;
        var willow = variant.Contains("Willow", StringComparison.Ordinal);
        var rowan = variant.Contains("Rowan", StringComparison.Ordinal);
        "bark" => PainterlyMaterialLibrary.ForColor(
            birch ? region == "kara" ? "90978c" : "c9c2ad"
                : conifer ? region == "kara" ? "504c43" : "685e50"
                : old ? "4a4a45"
                : willow ? "494b47"
                : rowan ? "6b6257"
                : linden ? region == "kara" ? "55574f" : "6d6a5e"
                : region == "kara" ? "575953" : "9b9487",
            birch ? "bark_birch_winter" : pine ? "bark_pine" : "wood_bark",
            sheltered: region == "kara"),
```

**HANDOFF-4** — `tools/blender/generate_modular_environment.py`, `create_pine`:
корона из 4 равных конусов (`tiers` 1.42/1.18/0.92/0.60 радиуса) заменить на
3–5 нерегулярные массы: для каждой массы (низ→верх) невыровненные
`radius1`, `depth`, z-центр с зазором, смещение центра по XY, yaw и
неравномерное число вершин (6/7), плюс один «сломанный» короткий ярус у вершины;
сохранить имена `PineA_Trunk_LOD0` / `PineA_Crown_LOD0`, material slots,
`tag(..., 1200, ...)` бюджет и точку основания. Причина: R025 — этот прототип
живёт в Act I (`Act1ConnectedWorld.cs:9288-9290`).

**HANDOFF-5** — `game/scripts/Act1ConnectedWorld.cs`:
`AddAuthoredPine`/три `KaraEdgePineA_*` перевести на
`AddVisualTree(..., VegetationStyle.Conifer)` (то есть на производные
`WinterSpruce_*`/`WinterOldBranch_*`) либо оставить только после HANDOFF-4;
в `BuildKaraForestEdge` список `KaraUrmanEdgeTree` (12 точек) разбить на
группы 2–3 и добавить два лиственных проёма, не меняя число экземпляров.

**Наблюдение для приёмки (не блокирует):** `game/tests/AddressRemediationGeometryProof.cs:394-397`
ожидает имена `PlantedStem_Winter{Linden_1_Plant33,Birch_2_Plant14,Birch_2_Plant15,Maple_1_Plant70}`.
Номера Plant смещаются вместе с планом: тест вернёт `found=false, accepted=false`
(падения нет), но строки нужно перенаблюдать после первого прогона. Также
`Act1FacilitiesSmokeTest.cs:268` ищет `WinterBirch_1_Plant` у бани — корень
`(-10.5,-36)` сохранён как `WinterBirch_1`, проверка жива.

## 8. Что осталось открытым

1. Экспорт `agentb_foliage_kit.glb`, `urman_winter_pine.glb` и
   `urman_modular_kit.glb` (Blender 4.5, команды выше) — не выполнялся.
2. Станционные пары before/after: `C5_forest_edge` (+ R024/R043, R025),
   P16/P16b, P21/P23 для VIS-075, tree lineup и foliage lineup, distance sweep,
   top-down census для VIS-084; замер цены (triangles, draw calls, overdraw,
   FPS) — по `URMAN_VISUAL_RESET_EXECUTION_2026-10-07.md`.
3. Художественная приёмка автора: hero-силуэт ели (VIS-025 прямо требует),
   соответствие леса H2/H3-1/H4-2/H4-3, «различимы ли три силуэта» на lineup.
4. Текстуры коры/хвои/ветвей — заявки в
   `docs/urman_knowledge_base/art/asset_requests/TREES.md` (AS-05/06/07).
