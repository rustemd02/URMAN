# ledger_EXTERIOR — лес, LOD, отсечение, линии ветра

**Запуск:** визуальный reset 2026-10-07, пакет EXTERIOR (исполнитель — субагент).
**Карточки:** VIS-027, VIS-029, VIS-030, VIS-076, VIS-085.
**Срез кода:** рабочее дерево после итерации 03 (`docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07.md`).
**Что запускалось:** ничего игровое. Ни dotnet, ни Godot, ни станция, ни git. Только чтение
кода, правки и статические проверки (grep, баланс скобок). Все числовыеreadback-значения
в этом листе — **пороги и формулы**, а не измерения: измерения снимает первый же станционный
прогон, который закроет строки со статусом `CODE_DONE_VERIFY_PENDING`.

Границы владения, соблюдённые здесь: правились только
`AgentBAct1ExteriorLayer.cs`, `GeneratedModularKitDressing.cs`, `GraphicsQuality.cs` и новые
файлы в `game/scripts/experiments/agent_b_act1/`. `PainterlyMaterialLibrary.cs`,
`AgentBFoliagePlan.cs`, `atmosphere.v1.json`, `Act1ConnectedWorld.cs`,
`Act1DemoRoot.RenderDiagnostics.cs`, `Act1DemoRoot.PerformanceCapture.cs`, `eng/*`,
`game/tests/*` — не тронуты; всё, что в них нужно, вынесено в HANDOFF (§6) с точным кодом.
Двойная запись света не возвращена: ни один новый файл не пишет `Environment`, `Sky`,
`DirectionalLight3D` или чужие материалы. `TuneConnectedAct1Atmosphere` остаётся единственным
владельцем наружного света; новые узлы только **читают** `ExteriorAtmosphere.FogLightColor`.

---

## 1. Визуальная карта правок

| Файл | Что добавлено | Строки |
|---|---|---|
| `game/scripts/experiments/agent_b_act1/AgentBFoliageSilhouette.cs` | новый: измеритель силуэта и sway-конверта (чистое чтение мешей/материалов) | 1–192 |
| `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.FoliageBudget.cs` | новый (partial слоя): аудит parity 027, бюджет+census 029, границы отсечения 030, ритм/слои леса 085, readback-строки | 30–641 |
| `game/scripts/experiments/agent_b_act1/AgentBWindStreaks.cs` | новый: world-space wind streaks V4 (076) | 1–489 |
| `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs` | wiring: `BuildWindStreaks`, `ExteriorWindVector`, `IsForestRimRegion`, потолок LOD-множителя, far-tier shadow, аудит в `PlantFoliage`, rhythm-список и разрыв периода в `EmitBelt`, гейты streaks в presentation/shelter/`_Process` | 247, 291, 302, 311, 1575, 1654, 1722–1745, 1834–1835, 2183–2195, 2423, 2464, 2525, 2541–2555, 2715–2760, 2967–2975 |
| `game/scripts/GeneratedModularKitDressing.cs` | аудит LOD0/LOD1 пары опубликованного кита (полосы не изменены — они закреплены smoke-тестом) | 141, 247–322 |
| `game/scripts/GraphicsQuality.cs` | readback `MeshLodThreshold` + `BudgetSnapshot()`; числа пресетов не изменены | 18–46, 89 |

---

## 2. VIS-027 — силуэт на переходе LOD

**Как было (фактические дистанции, `AgentBAct1ExteriorLayer.cs:1722-1745` до правки):**
`ConfigureFoliageRange` умножал полосы на `clamp(treeHeight / 8, 1, 3)`; полосы:
LOD0 `0 → 26s (end margin 2s)`, LOD1 `24s → 64s (begin 2s, end 4s)`, LOD2 `60s → ∞ (begin 4s)`.
То есть полный LOD у ели 13–21 м (`WinterPine` нормализован в 1 м, `vertical ≈ height`,
`AgentBAct1ExteriorLayer.cs:1426-1433`) рисовался до **41,6–50,4 м**, а у кроны выше 24 м —
до **78 м** (потолок 3×). Это и есть «дальний экземпляр тянет тяжёлый вид», и это же
скрывало реальный parity: переключение происходило там, куда кадр с туманом не доходит.

**Что сделано:**

1. **Потолок множителя — единственный изменённый порог:**
   `LodRangeScaleCeiling = FoliageBudgetApplied ? 1.6f : 3f`
   (`AgentBAct1ExteriorLayer.FoliageBudget.cs:65`, применяется в `:1737`).
   Пороги после правки (scale = `clamp(treeHeight/8, 1, 1.6)`):

   | treeHeight | scale | LOD0 end (fade 2·s) | LOD1 begin/end (margins) | LOD2 begin | switch 0→1 |
   |---|---|---|---|---|---|
   | 8 м | 1.0 | 26.0 м (24–28) | 24→64 (22–26 / 60–68) | 60 (56–64) | 24–26 м |
   | 13 м | 1.6 | 41.6 м (38.4–44.8) | 38.4→102.4 (35.2–41.6) | 96 (89.6–102.4) | 38.4–41.6 м |
   | 21 м | 1.6 (было 2.6) | 41.6 м (было 54.6) | 38.4→102.4 (было 62.4–166.4) | 96 (было 156) | 38.4–41.6 м |
   | kit (`GeneratedModularKitDressing.cs:15-20`) | — | **0–24 м, margin 3 (не изменено)** | **18–72 м, margin 3 (не изменено)** | — | 18–24 м |

   Все три полосы одного растения умножаются на **один и тот же** `rangeScale`, поэтому
   crossover остаётся cross-fade: у 8-метрового дерева LOD0 гаснет на 28 м, LOD1 появляется
   на 22 м — разрыва нет; аналогично 1→2: LOD1 до 68 м, LOD2 с 56 м.
2. **Полоса проверяются по факту, а не по формуле** (`FoliageBudget.cs:439-510`): для каждого
   дерева с тремя тирами читаются `VisibilityRange*` реальных инстансов и считаются
   `lodRangeGapBandCount` (нет ни одной тиры в диапазоне) и `lodDoubleFullBandCount`
   (две полные копии одновременно). Формула: partially visible = `[begin, end]`,
   fully visible = `[begin+beginMargin, end-endMargin]`.
3. **«Обе стороны границы имеют одинаковый корень» проверяется узлом** (`:443-448`): все тиры —
   дети одного rooted `Node3D` с идентичным transform; `lodSharedRootInstanceCount`
   считает, сколько инстансов действительно имеют `Position=0, Rotation=0, Scale=1`.
   Для кита дополнительно измеряется смещение origins пары:
   `lodPairRootOffsetMaxMeters`, допуск 0.05 м (`GeneratedModularKitDressing.cs:293-295`).
4. **Паритет силуэта измеряется с вершин** (`AgentBFoliageSilhouette.cs:69-160`):
   `CrownWidth` — больший горизонтальный размах вершин выше 45 % высоты, `BaseWidth` — ниже
   12 %, `Height` — полный размах. Порог: `LodSilhouetteTolerancePercent = 2f`
   (`FoliageBudget.cs:30`), т.е. proposed ≤2 % из карточки, считаемый для **каждой соседней
   пары** (0→1 и 1→2) и публикуемый как `lodSilhouetteWorstCrownDeltaPercent`,
   `...WorstHeightDeltaPercent`, `...WorstBaseDeltaPercent`,
   `lodSilhouetteFamiliesOverTolerance` (список `region:family:crown/height/base`).
   Нарушение не ронял мир: оно репортится (обоснование в коде, `:22-30`).
5. **Alpha-overdraw дальнего тира** считается числом: `lodFarTierAlphaClippedSurfaces` и
   `lodFarTierAlphaClippedFamilies` (поверхности с `cutout_texture`, т.е. `ForCutout`
   хвои `FoliageMesh` `AgentBAct1ExteriorLayer.cs:99-104`). Поведение **не изменено**:
   превращать дальнюю крону в непрозрачную массу без пары кадров — это правка вида, а не
   бюджета; решение и точный патч — §6 HANDOFF-3.

**Как проверяется (то, что закрывает карточку):**
`act1-foliage-lod: families=… worstCrown=…% rangeGaps=… doubleFullBands=… sharedRootInstances=…`
в `game.log` любого станционного прогона; `URMAN_FOLIAGE_LOD_AUDIT=1` добавляет построчный
дамп на семью. Видеопроверка обязательна (карточка это прямо говорит): P16b near/far через
фактическую границу 0→1,walk на нормальной скорости, обе стороны, M004–M006 — т.е.
`remote-check.py capture` с чекпоинтами до/после границы + 10–15 с движение.
**Статус: `CODE_DONE_VERIFY_PENDING`.** Пороги и формулы доказаны статически; parity-числа,
видео перехода и вывод по alpha — требуют прогона.

---

## 3. VIS-029 — вклад одного тяжёлого вида

**Бюджетные действия (все под одним reversible-переключателем `URMAN_FOLIAGE_LOD_BUDGET`,
значение `legacy|off` = прежнее поведение один в один):**

| Действие | Где | Число |
|---|---|---|
| Потолок полного LOD: `rangeScale` 3.0 → 1.6 | `FoliageBudget.cs:65`, `AgentBAct1ExteriorLayer.cs:1737` | LOD0 end у высокой кроны 54,6–78 м → 41,6 м |
| Дальний тир не отбрасывает тень (`lod == 2`) | `FoliageBudget.cs:614`, `AgentBAct1ExteriorLayer.cs:1575` | тиры 0 и 1 тенят как раньше; LOD2 видим только с `60·scale ≥ 60 м`, что за пределами `low` (45 м) и внутри `medium/high` (80/120 м) закрыто туманом |
| Снимается understory у двух внешних рядов леса | `FoliageBudget.cs:610`, `AgentBAct1ExteriorLayer.cs:2541, 2554` | row 0–1 из 9; 2 записи на точку ряда; RNG-вызовы сохраняются полностью (`FoliageBudget.cs` комментарий 2536-2541), поэтому ни один корень, садовый fixture и контакт границы не сдвигаются |
| Порог объёма ячейки отсечения | `FoliageBudget.cs:40` | `CellVolumeFootprintFactor = 2.2` относительно куба 21 м (диагональ 12-м ячейки) |

**Census, который отделяет вклад foliage (meta на `AgentB_PlantedFoliage`, `FoliageBudget.cs:512-574`):**
`foliageSceneNodeCount`, `foliageTreeInstanceCount`, `foliageLodTierTreeCounts=[t0,t1,t2]`,
`foliageDistinctMeshCount`, `foliageDistinctMaterialCount`, `foliageSurfaceCount`,
`foliageAlphaSurfaceCount`, `foliageShadowCasterCount`,
`foliageOuterRowUnderstorySkipped`, `foliageBudgetMode`, `lodRangeScaleCeiling`.

**Про пресеты (прямое требование правила «не менять ради галочки»):** числа
`GraphicsQuality.Apply` (`:33-38`, `:142`) **не тронуты**. Добавлен только readback
`GraphicsQuality.BudgetSnapshot()` (`:42-46`) и `MeshLodThreshold` (`:35`): он печатает
`preset=…,lod_threshold=…,scale=…,msaa=…,shadow_filter=…,directional_shadow_distance=…,
directional_shadow_atlas=…,positional_shadow_atlas=…,ssao_gate=…,probe=…` и вшивается в
`act1-foliage-lod` и в `DescribeFoliageBudget()`. Это то, чем доказывается запрет карточки
«не сравнивать разные presets»: у пары «до/после» строка обязана быть идентичной.
Обратимость: метод можно удалить, поведение пресета не изменится.

**Как проверяется:** baseline-парный прогон того же тяжёлого вида до и после перестановки —
`P06 foliage-heavy` и `P16b` + существующий perf-probe
(`URMAN_PERF_RENDER_DIAGNOSTICS=1`, `URMAN_PERF_RENDER_OUTPUT=<абсолютный путь>`),
**с равным `BudgetSnapshot()`**; вклад foliage выделяется §6 HANDOFF-1 (ценз уже считает
`visibleSpatialCandidates` по группам владельца, но процедурная листва сейчас сваливается в
один `AgentBExteriorWorld` fallback-ключ). Критерий приёмки (0 увеличение метрик, отсутствие
скрытого роста shadow work) без этого прогона не закрывается: **замеры здесь не выполнялись.**
**Статус: `CODE_DONE_VERIFY_PENDING`.**

---

## 4. VIS-030 — границы отсечения ячейки растений

**Принцип:** MultiMesh отсеивается как группа — покарточного frustum culling не обещается
(формулировка карточки [S30]). Что действительно ломалось: вершинная анимация ветра
(`PainterlyMaterialLibrary.cs:22-28`, `VERTEX.x += gust * wind_sway * max(VERTEX.y,0)`,
`VERTEX.z` в 0.6×, `gust ∈ [-1,1]`, `wind_sway`: foliage 0.05 / leaf_birch 0.07 /
grass_tuft 0.12 / grass 0.0, `:1051-1058`) движет геометрию **за пределами** CPU AABB, и
движок про это не знает. Отсюда «исчезающая ветка» на краю экрана.

**Реализация (`FoliageBudget.cs:362-437`):**
1. Для каждой ячейки: `merged = ⋃ (instanceTransform × meshAabb)` по всем экземплярам
   (инстанс-трансформы берутся из `MultiMesh.GetInstanceTransform`, т.е. из того же массива,
   которым рисуют), плюс `plain = ⋃ instanceTransform × meshAabb` без запаса — обе величины
   репортятся, а `cullingInstancesUncovered` страхует правило слияния.
2. Запас = **измеренный** конверт sway: `wind_sway × max(localY) × instanceScale + 0.05 м`
   на snow-relief/trample (`AgentBFoliageSilhouette.cs:118-152`, `VertexReliefMargin = .05f`)
   — не произвольные 100 м и не константа.
3. Применение: `GeometryInstance3D.ExtraCullMargin = cellSway` для ячеек и
   `= max(swayX, swayZ)` для дерева каждого LOD-инстанса (`:404-411`, `:487`).
   `CustomAabb` **не** выставляется сознательно: автоматический merged AABB MultiMesh
   остаётся источником истины, а `extra_cull_margin` — документированный механизм именно
   для веремного смещения. Восстановление прежнего вида: `URMAN_FOLIAGE_BOUNDS=legacy`
   (меты и печать измерения при этом сохраняются).
4. Анти-giant-AABB: `cullingCellMaxDiagonalMeters`, `cullingCellMaxVolumeCubicMeters`,
   `cullingCellsOverFootprintLimit` (порог 2.2× куба 21 м), `cullingCellFootprintMeters=12`,
   пер-ячейечно: `cullingCellInstances`, `cullingCellAabbDiagonal`, `cullingCellAabbVolume`,
   `cullingCellSwayReserve`.
5. Occluder: `occlusionCullingActive` (viewport) и `occlusionOccluderSourceCount` — обход
   всего наружного слоя на `OccluderInstance3D`; ни один узел листвы его не создаёт, и это
   число это фиксирует, а не предположение.

**Ожидаемые величины (для сверки с прогоном):** конверт sway = 0.05 × localY × scale.
Для `WinterPine` (mesh нормализован, `localY ≈ 1`, horizontal scale 10,7–17,2 при
height 13–21 м) ⇒ 0,53–0,86 м; для kit-елей с `vertical ≤ 5` и `localY ≈ 10` ⇒ ~0,5–0,6 м;
для ground cover (`grass` → sway 0) ⇒ только 0,05 м рельефа, то есть запас у клеток
мал и измеряется, а не назначается.
**Как проверяется:** `P16b edge-left`/`edge-right` вдоль края видимости + короткое видео
(R024/R025), плюс строка `act1-foliage-lod: cells=… maxCellDiagonal=…m overFootprint=0
swayReserve=…m uncovered=0`. **Статус: `CODE_DONE_VERIFY_PENDING`**
(арифметика полос и формула конверта — `DONE` как статически доказанные).

---

## 5. VIS-076 и VIS-085

### VIS-076 — линии ветра (`AgentBWindStreaks.cs`)

| Параметр | Деревня | Кромка леса | Источник |
|---|---|---|---|
| пул / потолок видимых одновременно | 16 / **8** | 16 / **16** | `:36-38`, гейт `:290-301` |
| lifetime | 1,1–2,0 с | 0,6–1,3 с | `:41-44` (карточка: 0,6–2,0 с) |
| rest-gap (редкость) | 1,0–2,6 с | 0,7–1,9 с | `:51-53`; ожидаемое живое число ≈4–6 / ≈6–9 |
| alpha ceiling | 0,13 | 0,21 | `:56-57` |
| ширина / длина | 0,045 м / 0,9–1,9 м | 0,075 м / 1,8–3,8 м | `:59-64` |
| гейт по ветру | `speed ≥ 4,2 м/с` | | `:78`, `:272` |
| оболочка | near 7 м, far 24 м, kill 30 м | | `:80-82` |
| скорость дрейфа | 0,38× ветра | 0,55× | `:445, :447` |
| привязка направления | `ExteriorWindVector` = `_rain.Direction.Normalized() × (InitialVelocityMin+Max)/2 × Emitting` | | `AgentBAct1ExteriorLayer.cs:2742-2753` |
| тон | `ExteriorAtmosphere.FogLightColor.Lightened(0.22)` — только чтение | | `:2726-2731` (`_Process`) |
| шейдер | `spatial`, `unshaded, cull_disabled, depth_draw_never`, `instance uniform streak_alpha`, туман **не** отключён | | `:87-102` |

Принадлежность миру, а не экрану: базис ленты строится из вектора ветра и вертикали
(`ApplyPose`, `:415-430`) — camera basis не читается нигде в файле; `billboard=false`,
`screenSpace=false`, `outline=false` записаны в мету (`:141-143`); ленты имеют depth-test и
уходят за стволы; `CastShadow=Off` (воздух не тенит, `:236`).
Тест отключения: `URMAN_WIND_STREAKS=off` (или presentation off, или reduced motion) — тогда
`ClearAll()` и `Visible=false` у всех 16, кадр равен кадру без фичи; `on` используется только
capture- прогоном, обычный запуск его не выставляет (`:268-272`). Reduced motion читается
установленным в проекте способом — `GetTree().GetFirstNodeInGroup("player_controller") is
FirstPersonController { ReducedMotion: true }` (`:461-464`), тот же паттерн, что
`Act1ConnectedWorld.YardDiscoveries.cs:283` и `Main.cs:245`; ничего не записывается.
Стоимость: 1 Mesh + 1 ShaderMaterial + 16 MeshInstance3D, меты пишутся только при изменении
(`:273-288`), таймеров у узла нет — обновление идёт из уже существующего `_Process` слоя.
Как проверяется: `P01` и `P21`, 10 с движения + still, пара with/without
(`URMAN_WIND_STREAKS=0` против `=1` на одной сборке), плюс geometry-only кадр: silhouette
объектов не меняется (это и есть проверка «не outline»). Readback — меты узла и
`windStreakState` на слое (`AgentBAct1ExteriorLayer.cs:302`).
**Статус: `CODE_DONE_VERIFY_PENDING`.**

### VIS-085 — страшная плотность без стены повторов

1. **Три слоя разведены уже в данных:** near hero trees (rows 4–8 + `WinterPine`/широкие
   `WinterSpruce_4..6`), mid occluding groups (understory + `Filler`), distant fog silhouettes
   (rows 0–1, которые теперь несут только стволы, §3). Аудит считает это числом:
   `forestLayerRowsWithin15m/25m/40m` — количество **разных рядов** внутри фиксированных
   горизонтов 15/25/40 м (фиксированы намеренно, чтобы не зависеть от `atmosphere.v1.json`,
   который мне не принадлежит; VIS-113 даёт целевую дальность 15–35 м).
2. **Разрыв интервала (`BreakRowPeriod`, `FoliageBudget.cs:100-110`):** стем двигается
   **вдоль своего ряда** (`axis` выбирается по стороне прямоугольника, `edge` 0/1/4 — X,
   2/3 — Z), на `±1.05 м`, либо `±1.8 м` для 22 % точек («bay»); нормальная позиция ряда,
   а значит закрытие горизонта и все keep-out, проверенные внутри `EmitBelt`, не меняются.
   Чисто функция точки → **ни один RNG-ход не добавлен и не пропущен** (это отдельное
   требование плана: `AgentBAct1ExteriorLayer.cs:2058-2059, 2085-2088`).
   Шаг ряда остаётся 4,0 м (rows 0–3) / 3,4 м (rows 4–8) ⇒ интервал после разрыва ≈1,1–8,7 м.
   Arrival closure (`:2196`) намеренно **не** тронут: его keep-out проверяются по конкретной
   точке, а sine-stagger уже ломает период.
3. **Аудит ритма (`FoliageBudget.cs:119-259`):** по каждой паре (row, edge) считается
   гистограмма интервалов (bin 0,4 м), модальный интервал и доля интервалов в ±15 % от него —
   **дважды: по до-разрывной и по после-разрывной позиции за один прогон**
   (`forestRhythmIntervalShareBeforePercent` → `...AfterPercent`,
   `forestRhythmModalIntervalBeforeMeters` → `...AfterMeters`), т.е. A/B без двух сборок.
   Плюс в полосе 0–20 м от конверта: `forestNearBandTrunkCount`,
   `forestNearBandNearestMinMeters`, `forestNearBandNearestMedianMeters` (spatial hash 10 м);
   и по видам: `forestSpeciesLongestRun`, `forestSpeciesRunTrees` (длина серии одного вида
   вдоль ряда) — потому что «стена» это и идеальный период, и повторяющийся ствол.
   Глубина считается от внутреннего прямоугольника конверта (`RingInwardDepth`, `:268-277`).
4. **Количество деревьев не увеличено:** ни одна правка не добавляет экземпляр; `EmitBelt`
   добавляет ровно те же 3 записи, что и раньше (две из них могут сниматься бюджетом §3).
   `URMAN_FOREST_RHYTHM=grid` возвращает прежнюю метронику.
Как проверяется: `P21`/`P23` три направления + render census после разрешения; строка
`act1-forest-rhythm: mode=… intervalShare=[before%->after%] modal=[..->..]m
nearBandTrunks=… nearestMin=…m speciesRuns=… rows=[a,b,c]@15/25/40m`.
**Статус: `CODE_DONE_VERIFY_PENDING`** (плотность и «страшность» — художественная приёмка
автора; числа ритма — прогон).

---

## 6. HANDOFF (чужие файлы; здесь не правилось ничего)

**HANDOFF-1 — `game/scripts/Act1DemoRoot.RenderDiagnostics.cs` (вклад foliage в draw calls).**
Сейчас процедурная листва попадает в один `rootFallbackGroups`-ключ
`…/Act1CoreWorldGreybox/AgentBExteriorWorld`, поэтому «выделить вклад foliage» (шаг 1
VIS-029) по этому отчёту нельзя. Минимальная правка — после блока `owner == world`
(строки 229-239) добавить отдельный подсчёт по поддереву листвы:

```csharp
if (mesh.GetPath().ToString().Contains("/AgentB_PlantedFoliage/", StringComparison.Ordinal))
{
    var key = mesh is MeshInstance3D treeInstance && treeInstance.CastShadow
        != GeometryInstance3D.ShadowCastingSetting.Off ? "treesCasting" : "treesNonCasting";
    var counts = foliageFallback.GetValueOrDefault(key);
    foliageFallback[key] = (counts.MeshNodes + 1, counts.Surfaces + source.GetSurfaceCount(),
        counts.Visible + (visible ? 1 : 0), counts.Candidates + (visible && layerMatches && candidate ? 1 : 0));
}
```

с `var foliageFallback = new Dictionary<string, (int MeshNodes, int Surfaces, int Visible,
int Candidates)>(StringComparer.Ordinal);` рядом с `rootFallback` (`:208`) и полем
`foliageGroups = foliageFallback.OrderByDescending(...)` в `_rendererCensus` (`:320-325`).
То же для `MultiMeshInstance3D` уже считается (`multiMeshNodes`, `multiMeshInstances`, `:317-318`).

**HANDOFF-2 — `game/scripts/Act1DemoRoot.DevViewCapture.cs` (числа пакета в метаданных кадра).**
В `_capture-summary.json` добавить две строки, чтобы пара before/after несла бюджет в самом
кадре:

```csharp
foliageBudget = _world.AgentBExteriorLayer?.DescribeFoliageBudget(),
windStreaks = (_world.AgentBExteriorLayer?.GetNodeOrNull<Node3D>("AgentBWindStreaks")
    as Urman.Godot.AgentBWindStreaks)?.DescribeWindStreaks(),
```

(оба метода уже существуют и pubic/`public string`; доступ к слою — тот же, что у
`exteriorLayer` в `Act1ConnectedWorld.cs:654`.)

**HANDOFF-3 — дальний тир хвои и alpha-overdraw (VIS-027, критерий «дальний экземпляр не
увеличивает alpha-overdraw»).** Измерение готово (`lodFarTierAlphaClippedSurfaces`).
Кандидат правки — `AgentBAct1ExteriorLayer.cs:99-104`, внутри `FoliageMesh`, только для
`lod == 2`: заменить `PainterlyMaterialLibrary.ForCutout("455749", alpha, "foliage")` на
непрозрачный `PainterlyMaterialLibrary.ForColor("455749", "foliage", sheltered: region == "kara")`.
Это меняет вид дальней кроны (перестаёт истончаться с mipmaps — силуэт становится **стабильнее**,
но масса плотнее), поэтому нужно только после пары кадров P16b и решения автора; сейчас
сознательно не применено.

**HANDOFF-4 — полосы кита `GeneratedModularKitDressing.cs:15-20`.** Полная полоса
LOD0 (0–24, margin 3 ⇒ полностью виден до 21 м) и полоса входа LOD1 (18–72, margin 3 ⇒
полностью с 21 м) стыкуются ровно в 21 м: разрыва и полного double-draw нет — это измеряется
и подтверждается метами `lodRangeGapMeters` / `lodDoubleFullBandMeters` (§2.5). Симметричное
сужение crossover (LOD0 end 24 → 21, LOD1 begin 18 → 21, единая полоса фейда 18–24) сократило
бы double-partial-полосу с 9 м до 6 м, **но** зафиксировано контрактом:
`game/tests/GeneratedModularKitContractSmokeTest.cs:142-145, 201-215` проверяет точные числа
и мета-строки `"0-24m"` / `"18-72m"`. Правка требует одновременного изменения теста — вне
моего объёма, поэтому здесь не сделана (числа полос в этом пакете не менялись).

**HANDOFF-5 — станция.** Первая сборка после этого пакета: `dotnet build`
(новых файлов четыре,partial-классов два; GLSL `instance uniform streak_alpha` компилирует
только движок — тот же оговор, что у VIS-033 в итерации 02), затем один capture-прогон,
который закрывает числа всех пяти карточек:
`eng/remote-check.py capture` с чекпоинтами `P16b` (переход LOD, обе стороны + 10–15 с),
`P06` (foliage-heavy, before/after), `P21`/`P23` (три направления леса), `P01`+`P21`
(ветер, пара `URMAN_WIND_STREAKS=0/1`), и один perf-probe-прогон с
`URMAN_PERF_RENDER_DIAGNOSTICS=1` для VIS-029. `URMAN_FOLIAGE_LOD_AUDIT=1` включает
построчный дамп parity. Одинаковый `BudgetSnapshot()` слева и справа — условие валидности
сравнения (карточка запрещает сравнивать разные presets).

---

## 7. Что осталось открытым

| Карточка | Открыто | Чем закрывается |
|---|---|---|
| VIS-027 | реальные проценты parity, видео перехода, вывод по alpha | прогон + HANDOFF-3 |
| VIS-029 | draw calls/primives/shadow по вкладу foliage, p95 по повторным прогонам | HANDOFF-1, perf-probe |
| VIS-030 | «нет исчезновения видимой ветки» на краю, размер ячеек против 12 м | P16b edge-left/right видео |
| VIS-076 | читается ли как воздух, а не HUD; тест отключения в кадре | P01/P21 with/without |
| VIS-085 | «лес плотный и неизвестный, не сетка» | P21/P23 + приёмка автора |
