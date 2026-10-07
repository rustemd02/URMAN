# УРМАН — цвет-script Акта I (VIS-067)

**Дата:** 2026-10-07
**Контракт:** `docs/URMAN_VISUAL_RESET_2026-10-07/URMAN_FINAL_STYLE_RECIPE_RU.md` §6 (минимум шесть authored states) и §8 (запреты).
**Данные:** `game/content/world/atmosphere.v1.json`.
**Читатель/применение:** `game/scripts/AtmosphereProfiles.cs` (схема), `game/scripts/Act1ConnectedWorld.cs` → `TuneConnectedAct1Atmosphere` (выбор профиля и запись в существующий `Environment`), `game/scripts/PainterlyMaterialLibrary.cs` (`SetSnowMood` — отклик снега), `game/scripts/AtmosphereDump.cs` (диагностика).

## Что изменилось в модели данных

Схема профиля расширена двумя **необязательными** блоками (обратная совместимость: блок отсутствует → прежний нейтральный вид):

- `"snow": { color, coverage, sparkle, tintStrength }` — отклик снега/земли по light-state (W1/W3). `color` — авторский оттенок снега; `coverage` и `sparkle` — **множители** к семейственно-заданным значениям шейдера (не переопределение, чтобы не плоский снег W3); `tintStrength` 0..1 — степень смешения авторского цвета с нейтральным альбедо шейдера `(0.93,0.95,0.97)`. `tintStrength=0` ⇒ кадр не меняется.
- `"identity"` — короткое имя авторского состояния; пишется в дамп и в meta `unifiedAtmosphereIdentity`.

Цвет делается **только** солнцем (`DirectionalLight3D`), небом (`ProceduralSkyMaterial`), туманом (`Environment.Fog*`), ambient (`Environment.AmbientLight*`) и откликом материала снега. Tonemap остаётся **AgX**; Glow/Adjustments выключены; объёмного тумана нет. Никакого нового `WorldEnvironment`/lighting owner.

## Выбор профиля и override съёмки

Зона резолвится ровно в один профиль — обычный игровой путь не меняется:

- `village_day` / `house_old_pc` / `fap_clinic` → `village-winter-frost` (морозное утро);
- `zirat_road` → `zirat-winter-muted` (сумерки);
- `kara_urman_night` → `kara-winter-night-edge` (нечеловеческая лесная ночь).

Три состояния без зоны (`overcast-day`, `golden-hour`, `village-green-night`) в обычном рантайме не встречаются — они берутся только на станции съёмки через переменную окружения `URMAN_ATMOSPHERE_PHASE`. Приоритет: `StudioPreviewProfile` (Studio) → `URMAN_ATMOSPHERE_PHASE` → дефолт зоны. Если `URMAN_ATMOSPHERE_PHASE` задана и профиль существует — он форсится для связного мира Акта I. Если id неизвестен — `GD.PushError` со списком допустимых id и **отказ** применять что-либо (атмосфера остаётся как есть), без тихого фолбэка (принцип VIS-003). Damp-файл (`URMAN_ATMOSPHERE_DUMP=<file>`) пишет фактически применённый профиль в `appliedProfile` плюс `identity` и все поля `snow*`.

## Валидация схемы

JSON-схемы для atmosphere-profile в `content/schemas/` нет; `Urman.ContentCli validate` сканирует только `<root>/content` (модули/кампании), а не `game/content/`. Единственный валидатор атмосферы — строгий парсер `AtmosphereProfiles.FromParams`: все прежние поля (`ambient/fog/ssao/sky/sun/exposure`) обязательны, новые блоки опциональны с нейтральными дефолтами. `StudioAtmospherePanel` читает только конкретные слайдеры и безопасно игнорирует новые ключи. `StudioSelfCheck` обновлён: ночной туман больше не сверяется с устаревшей константой `0.009`, а выводится из загруженного профиля `kara-winter-night-edge` (проверка строже и не дрейфует).

## Контакт-лист состояний (все authored values)

| Профиль (id) | Состояние / задача | Sun color · energy · rotation(X,Y) · shadowOpacity | Sky top / horizon / groundH / groundBottom · sunAngleMax · cover | Fog color · density · heightDensity · aerial · sunScatter | Ambient color · energy | Exposure · SSAO | Snow color · coverage · sparkle · tintStrength |
|---|---|---|---|---|---|---|---|
| `village-winter-frost` | Морозное утро · **VIS-068** | `fff1d8` · 1.12 · (−14,26) · 0.40 | `2f5f9e`/`d7e6f2`/`b9cadd`/`2d4b6e` · 1.4 · `.16,.22,.32,.55` | `b6c6da` · 0.016 · 0.006 · 0.70 · 0.03 | `9fb4d6` · 0.92 | 1.06 · 0.78/0.4 | `c2ceff` · 1.0 · 1.2 · 0.50 |
| `overcast-day` | Пасмурный день · **VIS-070** (capture-only) | `cbd8e8` · 0.72 · (−34,16) · 0.12 | `48596f`/`a4b4c6`/`7b8ca0`/`3a4a5e` · 0 · `.5,.6,.7,.85` | `8796a8` · 0.028 · 0.006 · 0.60 · 0.02 | `7f93b3` · 1.05 | 1.00 · 0.90/0.4 | `cfd9e8` · 1.0 · 0.45 · 0.32 |
| `golden-hour` | Золотой зимний час · **VIS-069** (capture-only) | `ffb066` · 1.60 · (−12,40) · 0.50 | `2f4c86`/`ff9a4a`/`e08a52`/`3a3f6a` · 4.0 · `.2,.28,.4,.6` | `d99a63` · 0.020 · 0.004 · 0.90 · 0.08 | `6f83b4` · 0.80 | 1.02 · 0.80/0.4 | `ffcaa0` · 1.0 · 0.9 · 0.60 |
| `zirat-winter-muted` | Сумерки (зиратская дорога) · **VIS-067** | `b48ab0` · 0.55 · (−22,−35) · 0.40 | `1c2447`/`8a5a7a`/`5a4566`/`141a30` · 0 · `.24,.3,.4,.7` | `4a5478` · 0.026 · 0.006 · 0.60 · 0.02 | `5c6fa2` · 0.50 | 0.98 · 0.82/0.4 | `a9b3e0` · 1.0 · 0.5 · 0.45 |
| `village-green-night` | Зелёная ночь деревни · **VIS-071/072** (capture-only) | `5fd6a0` · 0.30 · (−48,20) · 0.50 | `0a2a24`/`1f7d5a`/`165a42`/`04140f` · 0 · `.2,.26,.34,.6` | `1f6b4f` · 0.020 · 0.008 · 0.60 · 0.01 | `2f7d5c` · 0.42 | 0.90 · 0.80/0.4 | `b8f0d6` · 1.0 · 0.7 · 0.55 |
| `kara-winter-night-edge` | Нечеловеческая лесная ночь · **VIS-073/074** | `6fa0a0` · 0.16 · (−52,−28) · 0.55 | `0b1f24`/`1f4540`/`173633`/`08161a` · 0 · `.1,.14,.2,.4` | `274b48` · 0.045 · 0.018 · 0.50 · 0.0 | `3f6f6a` · 0.22 | 0.85 · 0.88/0.4 | `8fb0ad` · 0.8 · 0.3 · 0.55 |

## Как рождается цвет (по состояниям)

- **Морозное утро:** высокая ключ-энергия тёплого почти-белого солнца + холодное церулеановое небо (`2f5f9e`) и светлая дымка; снежная синева идёт от `snow.color=c2ceff` и повышенной искры (sparkle 1.2). Дом/дорога/небо в одной value-иерархии, highlights не клиппятся.
- **Пасмурный день:** мягкое low-contrast солнце (shadowOpacity 0.12), плотная облачность (cover до 0.85), цветные холодные тени держатся ambient `7f93b3` и value-разделением снег/небо/стены, а не контрастом в посте.
- **Золотой час (одно authored state, не дефолт):** тёплый key `ffb066` + видимый диск (sunAngleMax 4.0) и `sunScatter=0.08` против холодных ambient/fog-теней (`6f83b4`); персиковый снег `ffcaa0` — окрашено намеренно, но материалы различимы.
- **Сумерки:** индиго-небо с Mauve-последним светом на горизонте (`8a5a7a`), сиреневый свет/снег; дорога читается, но не хорроризирована (запрет по мечети/зирату).
- **Зелёная ночь деревни (N1/N2):** изумрудные key/небо/fog/ambient (`5fd6a0`/`1f7d5a`/`1f6b4f`/`2f7d5c`) с мятным снегом; янтарно-кремовые окна `VillageWindowMaterials`/`VillageHouseholdDirector` остаются редким тёплым контрапунктом — сильный хроматический контраст двух семейств без neon-flood. Экспозиция не занижена вместо работы с hue.
- **Нечеловеческая лесная ночь (H2/H4-3):** холодная база blue/green/dirty teal (`6fa0a0`/`1f4540`/`274b48`), `density 0.045` + `heightDensity 0.018` съедают глубину за 2–3 слоями стволов (ближний план остаётся читаемым, ~5–10 м не молоко), sparkle 0.3 — лес тусклый и чужой. Красного нет вообще.

## Соблюдённые запреты STYLE RECIPE §8

- Нет global green/orange/red overlay, LUT, full-screen tint, adjustments, glow — цвет только из sun/sky/fog/ambient/отклика снега в существующем Environment.
- Золотой закат не вечный: это одно capture-only состояние, дефолт игры — пасмурно-морозный.
- Красного forest-baseline нет; красный — только будущий отдельный beat вне этого объёма.
- Туман не маскирует геометрию во всей игре: плотный лесной fog применяется только к лесному профилю, деревня/мечеть/интерьеры его не наследуют.
- Снег не плоский: `coverage`/`sparkle` — множители семейственных значений, `tintStrength` не даёт чистого `#FFFFFF` и не перебеливает.
- Новый WorldEnvironment/render framework не добавлен; интерьерный restyle не затронут; Tonemap AgX и SSAO сохранены (SSAO лишь поднят intensity в пределах 0.78–0.9, radius 0.4 без изменений).

## Съёмка на станции (Windows, через `eng/remote-check.py`)

Съёмка требует запуска движка — по текущему маршруту только на станции, не локально. Для каждого capture-only состояния задать профиль до запуска и включить дамп:

```bash
URMAN_ATMOSPHERE_PHASE=overcast-day        URMAN_ATMOSPHERE_DUMP=out/atm_overcast.json  # smoke-сцены P06/P09/P11
URMAN_ATMOSPHERE_PHASE=golden-hour         URMAN_ATMOSPHERE_DUMP=out/atm_golden.json    # P01/P17/P19 A/B
URMAN_ATMOSPHERE_PHASE=village-green-night URMAN_ATMOSPHERE_DUMP=out/atm_green.json     # ночные фикс-view P01/P06/P17
URMAN_ATMOSPHERE_PHASE=village-winter-frost  # морозное утро (он же дефолт деревни)
URMAN_ATMOSPHERE_PHASE=zirat-winter-muted    # сумерки (дефолт zirat_road)
URMAN_ATMOSPHERE_PHASE=kara-winter-night-edge# лесная ночь (дефолт kara_urman_night), VIS-073/074
```

`appliedProfile`/`identity`/`snow*` в dump-файле подтверждают, какой профиль реально применён к кадру. Неизвестный id не роняет игру и не подменяется — в логе `GD.PushError` с перечнем допустимых значений, атмосфера не меняется.
