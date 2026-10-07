# VIS-006 — Единственный владелец света, environment и графического пресета

Дата среза: 2026-10-07, 17:00. Только статическое чтение рабочего дерева. Движок не
запускался, readback-кадр не снимался (шаг 2 карточки выполнен как «что нужно замерить»,
а не как «замерено»).

**HEAD `612f9bc4`, 12 грязных путей.** Параллельный исполнитель правит именно этот слой:
за время разбора `game/content/world/atmosphere.v1.json` перешёл с 3 профилей на 6 и
изменил числа, а в `PainterlyMaterialLibrary` добавлены `SetSnowMood` (VIS-068/070) и в
`TuneConnectedAct1Atmosphere` — env-var `URMAN_ATMOSPHERE_PHASE` (VIS-067). Значения в §3 —
из среза, а не из `docs/production/ai_visual_reset_2026-10-07/_research/B_render_pipeline_map_RU.md`
(его §5.2 на момент разбора уже **расходится** с файлом: там ambient 0.85 / sun 0.48 /
fog 0.03, в файле — 0.92 / 1.12 / 0.016).

---

## 1. Итог: кто действительно владеет

| Вопрос | Ответ | Доказательство |
|---|---|---|
| Сколько `WorldEnvironment` в `.tscn` | **0** | `grep -rn "WorldEnvironment" game/scenes/` — пусто; `game/scenes/zones/*.tscn` — только скрипт-заглушки (`style_benchmark_day_street.tscn:5-8`) |
| Единственный наружный `WorldEnvironment` | нода `Act1CoreWorldGreybox/AgentBExteriorWorld/AgentBEnvironment` | создаётся `AgentBAct1ExteriorLayer.BuildEnvironment()` `:2538-2589`, имя `:2586`, meta `atmosphereOwner=AgentBExteriorWorld` `:2588`; монтируется `Act1ConnectedWorld.cs:1671-1677`, вызов `:1212` |
| Единственный наружный sun | `AgentBSun` (`DirectionalLight3D`) | `AgentBAct1ExteriorLayer.cs:2540-2548`, ссылка `_sun` |
| Кто пишет итоговые значения наружного света | **`Act1ConnectedWorld.TuneConnectedAct1Atmosphere`** (`game/scripts/Act1ConnectedWorld.cs:1751-1848`) | вызов `:664` (при смене зоны) и `:1748` (Studio refresh); профиль из `AtmosphereProfiles.Get` `:1791` |
| Кто владеет графическим пресетом | **`FirstPersonController.ApplyGraphicsPreset()`** (`game/scripts/FirstPersonController.cs:529-533`) | единственный production-вызов `GraphicsQuality.Apply` (`GraphicsQuality.cs:30`); тестовые вызовы — `game/tests/CharacterPoseProbe.cs:183`, `game/tests/Act1HouseholdLifeSmokeTest.cs:91` |
| Данные света | `game/content/world/atmosphere.v1.json` (6 профилей в срезе), загрузчик `game/scripts/AtmosphereProfiles.cs`; отсутствующий профиль — исключение, не дефолт | `AtmosphereProfiles.cs:33`, `:47-53` |
| Readback | `game/scripts/AtmosphereDump.cs` — только при `URMAN_ATMOSPHERE_DUMP=<file>`; вызов `Act1ConnectedWorld.cs:1846` | `AtmosphereDump.cs:11-40` |
| Второй WorldEnvironment/sun в обычном запуске | **нет**: `Main` запрещает вход во внесценионные зоны при смонтированном connected-мире | `Main.cs:124-130` (`"Connected Act I world cannot enter non-Act-I zone"`), `SetActiveLogicalZone` `:138` |

---

## 2. Цепочка владения (порядок записи = приоритет)

`Act1ConnectedWorld.SetActiveLogicalZone(zoneId)` — `game/scripts/Act1ConnectedWorld.cs:572`
(первый вызов из `_Ready` `:514`, далее из `Main.cs:138`):

| Шаг | Строка | Что делает |
|---|---|---|
| 1 | `:589` | `useExteriorAtmosphere = !activePlacement.Interior` |
| 2 | `:591-602` | для всех зон: `binding.Node.Environment = isActive ? binding.Resource : null` — гасит наружные WorldEnvironment чужих зон. Комментарий `:597-599`: у `WorldEnvironment` нет флага enabled, поэтому обнуление ресурса — единственный шов |
| 3 | `:604-626` | `light.Visible = isActive && (!useExteriorAtmosphere || light is not DirectionalLight3D)` — **второй directional sun никогда не включается** снаружи |
| 4 | `:628-652` | только для `house_old_pc`: копирует наружные `Sky`/fog в **ресурс интерьера** (`:636-651`) |
| 5 | `:654-656` | `exteriorLayer.SetExteriorPresentationEnabled(useExteriorAtmosphere, isKaraNight, windowSnowView)` |
| 6 | `:662-665` | `coreWorld.Visible`, `SetAuthoredKitCollisionEnabled`, `TuneConnectedAct1Atmosphere(...)` |
| 7 | `:667-681` | мета-стейты `activeZoneId`, `activeWorldEnvironmentCount`, `activeExteriorAtmosphere`, `activeAtmosphereOwner`, `activeDirectionalLightCount`, `activeLightCount` |

Регистрация: `:384-390` `_environmentsByZone` (пустой ресурс — исключение),
`:391-395` `_lightsByZone` с фильтром по meta `connectedWorldHidden` (`:10284`),
тип связи — record `EnvironmentBinding` `:213-215`.

---

## 3. Что и в каком порядке пишется в один и тот же наружный `Environment`/`AgentBSun`

| # | Писатель | Файл:строка | Что пишет | Итог |
|---|---|---|---|---|
| A | `AgentBAct1ExteriorLayer.BuildEnvironment` | `AgentBAct1ExteriorLayer.cs:2538-2589` | factory: `AmbientLightSource=Sky`, `AmbientLightEnergy=0.80`, `TonemapMode=Aces`, `TonemapExposure=0.98`, `FogDensity=0.0034`, `FogAerial=0.54`, цвета `ProceduralSkyMaterial` `:2550-2563`, `AgentBSun` `c7d3d1` / 1.04 / shadowOpacity 0.30 / rot (−48, 32) |Base |
| B | `AgentBAct1ExteriorLayer.ApplyAtmosphere` (через `SetExteriorPresentationEnabled` `:258-293`, вызов `:270`) | `:2835-2893` | ambient source/color/energy (`:2843-2846`, `:2853-2858`), fog color/density/height/heightDensity/aerial/skyAffect/sunScatter, `TonemapExposure` (`:2852`, `:2866`), 4 цвета неба (`:2869-2874`), sun color/energy/shadowOpacity/rotation/`ShadowEnabled` (`:2877-2885`), энергии 3 кара-акцентов (`:2888-2892`) | **перекрывается C целиком** |
| C | `Act1ConnectedWorld.TuneConnectedAct1Atmosphere` | `Act1ConnectedWorld.cs:1751-1848` | селектор профиля (`:1776-1790`), ambient + fog (`:1792-1802`), `TonemapMode = Agx` (`:1803`), `TonemapExposure` (`:1804`), `GlowEnabled=false` (`:1808`), `SsaoEnabled=true` + intensity/radius (`:1809-1811`), `AdjustmentEnabled=false` + 1/1/1 (`:1812-1815`), `GraphicsQuality.ConfigureEnvironment(environment, authoredSsao: true)` (`:1816`), `ProceduralSkyMaterial` 4 цвета + `SunAngleMax`/`SunCurve`/`SkyCoverModulate` (`:1818-1826`), `AgentBSun` color/energy/shadowOpacity/`ShadowEnabled`/rotation (`:1832-1836`) + `ConfigureSun` (`:1837`) | **ВЛАДЕЛЕЦ** |
| D | `PainterlyMaterialLibrary.SetSnowMood` (вызов из C, `Act1ConnectedWorld.cs:1842`) | `PainterlyMaterialLibrary.cs:572-604` | по **всему** кэшу материалов: `snow_color`, `snow_coverage = базовое × coverageScale`, `snow_sparkle = базовое × sparkleScale` (`:595-598`) | владелец снежного отклика материала, см. §5.5 |
| E | `GraphicsQuality.Apply` | `GraphicsQuality.cs:30-60` | `viewport.Scaling3D*`, `Msaa3D`, `ScreenSpaceAA`, `MeshLodThreshold`, `PositionalShadowAtlasSize`, `DirectionalShadowAtlasSetSize`, soft-shadow фильтр; `:57` `ConfigureEnvironment(viewport.World3D?.Environment)`; `:58-59` `ConfigureSun` для **всех** `DirectionalLight3D` в `Root` | владелец растра/тени/SSAO-on-off |

**Числа среза, чтобы видеть масштаб перекрытия B→C** (`atmosphere.v1.json`,
`AgentBAct1ExteriorLayer.cs:2843-2885`):

| Параметр | B (день) | C `village-winter-frost` | B (ночь) | C `kara-winter-night-edge` |
|---|---|---|---|---|
| sun energy | 1.04 (`:2879`) | **1.12** | 0.86 (`:2879`) | **0.16** |
| sun rotation | (−48, 32) | **(−14, 26)** | (−52, −28) | (−52, −28) |
| sun shadowOpacity | 0.30 | **0.40** | 0.20 | **0.55** |
| sun color | `c7d3d1` | **`fff1d8`** | `7f96a0` | **`6fa0a0`** |
| ambient source / energy | Sky / 0.80 | **Color / 0.92** | Color / 0.94 | **Color / 0.22** |
| fog density | 0.0034 | **0.016** | 0.0048 | **0.045** |
| exposure | 0.98 | **1.06** (TonemapMode при этом всегда Agx, `:1803`) | 1.06 | **0.85** |

Ночью расхождение по солнцу — **5.4×** (0.86 → 0.16). То есть блок B работает,
но его результат никогда не попадает в кадр; при смене порядка вызовов в
`SetActiveLogicalZone` (`:654` ↔ `:664`) кадр изменится целиком. Это и есть
скрытое переопределение, которое требовалось зафиксировать.

---

## 4. Кто создаёт собственные `WorldEnvironment` (все случаи)

| Владелец | Файл:строка | Когда | Арбитраж |
|---|---|---|---|
| `StyleBenchmarkZone.BuildEnvironment` | `game/scripts/StyleBenchmarkZone.cs:97-166`; `AddChild(new WorldEnvironment { Name = "WorldEnvironment" })` `:166`; `GraphicsQuality.ConfigureEnvironment(environment, authoredSsao: interior)` `:165` | `_Ready` `:74`, для **каждого** экземпляра | 5 логических зон = 5 нод; все `StyleBenchmarkZone` (`game/scenes/zones/style_benchmark_day_street.tscn:3`, `style_benchmark_house_pc.tscn`, `chapter1_fap_clinic.tscn:3` `ZoneKind=3`, `chapter1_zirat_road.tscn:3` `ZoneKind=4`, `style_benchmark_kara_urman_night.tscn`). Гасятся `Act1ConnectedWorld.cs:600` |
| `StyleBenchmarkZone` sun | `:170-185` (`MainDirectionalLight`, `ShadowEnabled=true`) | если `!interior` | гасится `Act1ConnectedWorld.cs:614-616` всегда, когда активен наружный atmosphere |
| `FullGameZone.BuildEnvironment` | `game/scripts/FullGameZone.cs:41-75`; `AddChild(new WorldEnvironment …)` `:75`; `KeyLight` `:77-83`; `LocalStoryLight` (OmniLight3D) `:132-137`; `TonemapMode = Filmic` `:73` | `_Ready` `:36-39` | **не арбитрируется `Act1ConnectedWorld`** — держится `Main.cs:124-130` (connected-мир не пускает в fullgame-зону). При `_connectedWorld == null` (`Main.cs:66,77`) это единственный WorldEnvironment сцены |
| `AgentBAct1ExteriorLayer.BuildEnvironment` | `AgentBAct1ExteriorLayer.cs:2586` | `Build()` из `Act1ConnectedWorld.cs:1674` | `:265` `_environment.Environment = enabled ? _environmentResource : null` |

`.tscn`-файлов с `WorldEnvironment` нет ни одного (§1).

---

## 5. Конфликты владельцев, которые должен закрыть VIS-006

### 5.1 Два автора наружного света в одном фрейме (§3 строки B и C)
`ApplyAtmosphere` (`AgentBAct1ExteriorLayer.cs:2835-2893`) и `TuneConnectedAct1Atmosphere`
(`Act1ConnectedWorld.cs:1770-1848`) пишут **одни и те же** свойства одного `Environment`
и одного `AgentBSun` в одном вызове смены зоны, разойдясь по всем ключевым значениям.
Победитель определяется порядком строк `:654` → `:664`. В обычном запуске профиль
побеждает, но блок B остаётся живым кодом, который нужно либо удалить, либо сделать
единственным владельцем.

### 5.2 Readback `activeWorldEnvironmentCount` врёт ровно в наружном режиме
`Act1ConnectedWorld.cs:674-676`:

```
SetMeta("activeWorldEnvironmentCount", useExteriorAtmosphere ? 0 : _environmentsByZone[zoneId].Count);
```

Когда `useExteriorAtmosphere == true`, активен `AgentBEnvironment` (`:265`), то есть
**1**, а мета пишет **0**. Критерий приёмки VIS-006 («в отчёте указан один активный
Environment») по этой мете проверить нельзя — она должна считаться как
`useExteriorAtmosphere ? 1 : count` либо подтверждаться отдельным readback.

### 5.3 Ресурс интерьера мутирует чужой владелец, без восстановления
`Act1ConnectedWorld.cs:632-652` пишет `BackgroundMode`, `Sky`, `FogEnabled`,
`FogMode=Depth`, `FogLightColor/Energy`, `FogDensity=.93`, `FogDepthBegin=12`,
`FogDepthEnd`, `FogHeightDensity=0`, `FogAerialPerspective=0`, `FogSunScatter=0`,
`FogSkyAffect` в **тот же** `Environment`, который авторизовал
`StyleBenchmarkZone.cs:125-164`. Ссылка на ресурс хранится в `_environmentsByZone`
(`Act1ConnectedWorld.cs:384-390`), поэтому:
- исходные авторские значения fog теряются безвозвратно (restore-пути нет —
  `grep "FogDepthBegin"` даёт только `:644`);
- повторный вход в `house_old_pc` переносит **текущие** наружные fog снова
  (`:645` `FogDepthEnd = clamp(2.4 / outdoor.FogDensity, 24, 160)`), то есть кадр
  интерьера зависит от истории переходов только через `outdoor.FogDensity`, который
  к тому моменту уже профильный.

### 5.4 `GraphicsQuality.Apply` — глобальный писатель всех sun
`GraphicsQuality.cs:58-59` обходит **всё дерево `Root`** и для каждого
`DirectionalLight3D` вызывает `ConfigureSun` (`:138-144`), включая `MainDirectionalLight`
пятим зон и `KeyLight` из `FullGameZone`. При этом `ShadowEnabled` пишут другие владельцы
(`AgentBAct1ExteriorLayer.cs:2884`, `Act1ConnectedWorld.cs:1835`), а `ConfigureSun`
**пропускает** свет без теней (`:140`). Порядок: смена пресета перестраивает splits/distance
у всех sun, включая выключенные; смена зоны перестраивает sun только активной наружной
цепочки. Владелец `DirectionalShadowMaxDistance` — один (`GraphicsQuality.cs:142`:
45 / 80 / 120 м), но он применяется в двух разных моментях времени.

### 5.5 Владелец света пишет параметры материалов
`Act1ConnectedWorld.cs:1842` → `PainterlyMaterialLibrary.SetSnowMood`
(`PainterlyMaterialLibrary.cs:572-604`) проходит по **всему** кэшу painterly-материалов
и перезаписывает `snow_color`, `snow_coverage`, `snow_sparkle` (умножением на авторское
значение, зафиксированное при первом проходе, `:591-594`). Таким образом значение
`snow_coverage` семейства больше не определяется только `ForColor` (`:905-921`);
второй владелец — профиль атмосферы. Прямое противоречие запрету
`docs/urman_knowledge_base/design_style.md:356` («не добавлять новый lighting owner»)
в части «новый владелец материала от светового слоя»; зафиксировать явно.

### 5.6 Пресет: второй писатель без согласия пользователя
Единственный владелец значения — `FirstPersonController._graphicsPreset`
(`FirstPersonController.cs:51`), но его меняют четыре источника:
1. `GraphicsQuality.DefaultPreset()` по адаптеру (`:51`, `GraphicsQuality.cs:22-28`);
2. флаг `--urman-safe-mode` (`FirstPersonController.cs:208-210`);
3. сохранённые настройки (`:291` из `ApplySettings`, запись через `SettingsUi.cs:222`);
4. **автоматический стартап-rescue** `ActivateLowPerformanceFallback()` (`:165-174`),
   вызываемый из `Act1DemoRoot.cs:1175` при `avg > 120 мс && median > 80 мс`
   по 4 сэмплам (`:1163-1172`).

Последствия `low`: scale 0.7 + FSR, MSAA выкл., FXAA вкл., LOD 4.0, атласы 2048/1024,
hard-тени, SSAO выкл. (`GraphicsQuality.cs:33-53`, `:129-136`) и однополярарный
альбедо-ридинг в шейдере (`PainterlyMaterialLibrary.cs:617-624`, `:170-193`).
Отдельно: rescue не изолирован от persistency — `CaptureSettings()` (`:270-279`)
берёт **текущий** `_graphicsPreset`, а `ApplySettings` пишет его в `UserSettingsStore`,
то есть временно снятый rescue может осесть в предпочтениях пользователя.
`Act1DemoRoot.cs:1186-1187` печатает `act1-demo-performance-rescue:` — это единственный
признак в логе; в readback-мету зоны он не попадает.

### 5.7 Probe/Studio override — вне обычного запуска, но публичны
- **`URMAN_ATMOSPHERE_PHASE`** (`Act1ConnectedWorld.cs:1777`) — override выбора профиля для
  capture-станции VIS-067; при неизвестном id свет **не применяется вообще** (`:1780-1788`,
  `return`). Это второй способ получить кадр, отличный от обычного запуска: он не меняет
  пресет, но меняет весь профиль света. Как и probe-флаги, несовместим с shipping-оценкой
  и должен записываться в receipt.
- Probe-флаги читаются только при аргументе `--urman-perf-probe*`
  (`GraphicsQuality.cs:68,82-83`) и умеют менять scale, splits и distance
  (`:101-105`, `:119-126`, `:56`). Сводка — `ProbeOverrideSummary()` (`:108-117`),
  печатается `Act1DemoRoot.cs:740`. В мету зоны не попадает.
- `Act1ConnectedWorld.StudioPreviewProfile` — **public static setter** (`:1740`),
  переопределяет выбор профиля (`:1790`). Пишут только
  `game/studio/StudioAtmospherePanel.cs:63` и `game/studio/StudioSelfCheck.cs:390,394`;
  обычного запуска это не касается (проверено поиском вызовов).
- `AtmosphereProfiles.Reload(json)` и `AgentBAct1HeightField.ReloadStrokes(json)` —
  runtime-подмена авторских данных; тоже studio-path.

### 5.8 Три профиля без потребителя
В срезе `atmosphere.v1.json` 6 профилей, а селектор `:1790` различает только
`kara-winter-night-edge`, `zirat-winter-muted`, `village-winter-frost`.
`overcast-day`, `golden-hour`, `village-green-night` достижимы только через
`StudioPreviewProfile`. Не дефект, но и не «единственный владелец» — список
нужно либо сократить, либо завести честный селектор состояния.

### 5.9 Post-эффекты: подтверждено отсутствие второго владельца
`GlowEnabled = false` (`Act1ConnectedWorld.cs:1808`) и
`AdjustmentEnabled = false` (`:1812`) пишутся только там; повторного включения нет
ни в одном файле (сверено с `B_render_pipeline_map_RU.md §9.1`). Volumetric fog, SSIL,
SDFGI/VoxelGI/LightmapGI, DOF, motion blur, screen-space reflections — **не найдено**.
`environment.MotionBlur` в `FirstPersonController` — настройка UI (`:274`
`MotionBlur: _motionBlur`); отдельного write в `Environment` не найдено.

---

## 6. Согласуется ли с константами карточки VIS-006

| Требование карточки | Факт среза | Итог |
|---|---|---|
| High: scale 1.0, MSAA4, shadow distance 120 м, 4 splits, atlas 4096 | `GraphicsQuality.cs:36` → `(1f, Msaa4X, 1f, 4096, 4096)`; `:142` `high => 120f`; `:141` не `low` → `Parallel4Splits` | **совпадает** |
| AgX, Glow=false, Adjust=false в прочитанном пути | `Act1ConnectedWorld.cs:1803`, `:1808`, `:1812` | **совпадает** (наружный Акт I). Интерьерные `StyleBenchmarkZone.cs:162` = Filmic (кроме дома — Agx); `FullGameZone.cs:73` = Filmic |
| Один активный Environment и один соответствующий sun | наружный — да (`:265`, `:273-276`, `:614-616`); **readback-мета `:676` это не доказывает** (§5.2) | требует readback |
| Отличие от High не скрыто | `low`/`medium` полностью описаны в `GraphicsQuality.cs:33-53`; rescue логируется `Act1DemoRoot.cs:1177-1183`, но не в мету зоны (§5.6) | частично |
| Обычный запуск не использует probe override | `GraphicsQuality.cs:82-83` требует `--urman-perf-probe*`; `StudioPreviewProfile` пишется только из `game/studio/*` | **подтверждено статически** |
| Шаг 2: readback итоговых параметров после готовности зоны | есть только через env-var `URMAN_ATMOSPHERE_DUMP` (`AtmosphereDump.cs:13`) и только для наружного профиля; interior/zirат/FullGame — не покрываются | **не выполнено, требуется** |

---

## 7. Минимальный список правок, который вытекает из карты (решения не приняты)

1. Убрать `ApplyAtmosphere` (`AgentBAct1ExteriorLayer.cs:2835-2893`) или сузить его до
   параметров, которые `TuneConnectedAct1Atmosphere` не пишет, и доказать отсутствие
   пересечения свойство-в-свойство.
2. Считать `activeWorldEnvironmentCount` как `useExteriorAtmosphere ? 1 : count`
   (`Act1ConnectedWorld.cs:674-676`) либо добавить readback активного
   `viewport.World3D.Environment`.
3. Сохранить исходный авторский fog ресурса `house_old_pc` перед мутацией
   (`:632-652`) и восстановить его при выходе.
4. Разделить владельца «свет → параметры материала»: `SetSnowMood`
   (`PainterlyMaterialLibrary.cs:572`) должен вызываться из material-слоя, а не из
   `TuneConnectedAct1Atmosphere`, иначе правка света меняет снежный отклик всех семейств.
5. Вызывать `GraphicsQuality.ConfigureEnvironment`/`ConfigureSun` для `FullGameZone`
   (`FullGameZone.cs:41-83`) — сейчас этот путь их не проходит ни при какой смене пресета.
6. Зафиксировать, что авто-rescue (`FirstPersonController.cs:165-174`,
   `Act1DemoRoot.cs:1175`) не должен попадать в `CaptureSettings()`
   (`FirstPersonController.cs:270-279`).
