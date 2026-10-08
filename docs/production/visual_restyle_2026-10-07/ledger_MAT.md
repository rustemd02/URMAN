# ledger_MAT — материалы (VIS-034/035/038/080/081/092/093/094/095)

Волна: visual reset 2026-10-07, исполнительский пакет «материалы». Дата записи: 2026-10-08.
Срез: рабочее дерево станции `C:\Users\ruste\Documents\GitHub\URMAN` (после итераций 01–03
журнала `docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07.md`; `SetSnowMood`,
`ForColor`-режимы, `ground_contact_height` сохранены и развиты).

Что здесь запрещено считать доказанным: игра не запускалась, сборка не выполнялась,
Godot-импорт не выполнялся, `git` не вызывался. Поэтому статус `DONE` стоит только там,
где критерий — статика, проверенная скриптом/чтением. Всё, что видно глазами, — 
`CODE_DONE_VERIFY_PENDING`. GLSL проверен чтением и структурным парсером (движок
компилирует шейдер только на прогоне).

Единый проверяемый гейт этой волны (локально, без движка):

```
bash eng/verify-painterly-textures.sh --contract          # статус на момент записи: PASS
bash eng/verify-painterly-textures.sh <файл.png>          # seam/saturation/clipping
```

---

## Визуальный контракт, которому подчинены все строки

- Сохранён **собственный** painterly-шейдер; никаких новых `StandardMaterial3D`-проектов
  наружу, никакого full-PBR «для галочки».
- Normal/roughness/mask добавляются **по семействам** (`FamilyResponses`, строки 742–831
  `PainterlyMaterialLibrary.cs`), а не глобально: у семейства без строки в таблице
  все новые uniform-ы остаются идентичностями → картинка не меняется.
- Период повторяемости стал **явным числом** в каждом семействе (`detail_scale`,
  `snow_micro_scale`, `SurfaceTextures` scale, `surfaceTileMetres`), и гейт проверяет
  нижнюю границу.
- Материал не подменяет форму: микро-амплитуда снега зафиксирована гейтом в ±0.002 м,
  снежное одеяло остаётся тонким слоем, а «масса» — ответственность VIS-077…079.
- Запреты соблюдены: вечного golden-закат-фильтра нет, global overlay нет, новых
  v7/v8-версий albedo не заведено (см. «Аудит существующих кандидатов» ниже).

---

## VIS-092 — painterly material response вместо asset-pack PBR

**Сделано.**
1. В шейдер введены три независимых opt-in слота + процедурный слой (строки 109–160):
   `proc_relief`/`proc_relief_freq`/`proc_roughness` (ALU, без самплера),
   `detail_normal_map`+`detail_normal_scale`, `detail_roughness_map`+`detail_roughness_delta`,
   `wear_mask_map`+`wear_roughness_delta`+`wear_metallic`+`wear_tint`.
2. Блок применения — строки 383–470, после `finish_grain`, всегда под `!low_quality`
   (та же дисциплина, что у `finish_grain` и снежного микрополя): relief → roughness →
   wear → bottom wear → end grain → civic grime → hoarfrost.
3. Проекция микро-слоя берёт **ту же доминантной плоскостью** выбранную грань, что и
   albedo (`response_uv`, `response_slope`, строки 213–233), поэтому рельеф не может
   разойтись с нарисованным слоем. Единый world-triplanar-blend для нормалей не
   вводился: это утроило бы стоимость самплера ради суб-сантиметровых деталей
   (записано как осознанное ограничение).
4. Затухание ниже размера пикселя: `response_detail_fade` (строки 244–247, порог
   `smoothstep(0.05, 0.14)` по производной UV) — тот же класс защиты от мерцания, что
   уже есть у снежного микрополя и `cut_wood_end`. Он же служит бюджетом: процедурный
   relief и roughness-модуляция **не вычисляются**, когда fade равен нулю
   (`detail_budget`, шейдер 401–410/422–426). Причина: 5 вызовов value-noise —
   примерно 20 `sin` на фрагмент, а дальние фасады составляют большинство пикселей C1/C6. Профиль
   `low_quality` по-прежнему не заходит в блок целиком (как `finish_grain` и снежное
   микрополе); cost-замер — VIS-063/VIS-118, не этот пакет.
5. Авторские значения по семействам (`FamilyResponses`, 742–831). Примеры:
   `wood_facade` relief 0.38 @ (2.6, 22) cycles/m, roughness-модуляция 0.10;
   `plaster`/`wall_institution` relief 0.26 @ (8, 8), roughness 0.14;
   `metal` relief 0.20 @ (3.5, 3.5); `zinc_sheet` 0.24 @ (1.6, 1.6); `iron` 0.30 @ (4, 4);
   `enamel` 0.05 @ (9, 9); `stone` 0.45 @ (6, 6); `cloth` 0.22 @ (16, 16);
   `bark_pine` 0.72 @ (4.5, 13); `roof` 0.28 @ (2.4, 2.4).
6. Пять «немых» семейств из карты дефектов (§3 `material_consumers_RU.md`) получили
   ответ на свет в `surfaceGrade` (строки 1683–1696): `metal` 0.58/0.32,
   `zinc_sheet` 0.52/0.34, `glass` 0.14/0.52, `leather` 0.64/0.12, `paper` 0.94/0.05,
   `painted` 0.55/0.26, `ceramic` 0.36/0.30, `rubber` 0.88/0.10 (было: все семеро
   падали в `_ =>` = 0.90/0.20, то есть железо читалось штукатуркой).
7. `metal`, `zinc_sheet`, `paper`, `leather` перенесены из `FlatByDesign` в
   `TunedWithoutMap` (строки 784–805): режим выбран явно, как требует VIS-007 шаг 3.

**Как проверяется.** `--contract`: «36 строк ответа, 0 мёртвых», «metallic arms»,
«50 путей на диске / 15 зарегистрированных / 0 необъяснённых». Визуальная часть
(критерий карточки: wood/plaster/metal/snow различимы по response при близких цветах) —
material sphere + реальный receiver, P01/P02/P09/P11, кадр обязательны.

**Статус:** `DONE` для статической части контракта (гейт PASS),
`CODE_DONE_VERIFY_PENDING` для критерия «различимы на кадре».

---

## VIS-093 — единое семейство старого дерева с историей использования

**Сделано.**
1. Разделение ролей внутри семейства сохранено и усилено, новых дублей не заведено:
   facade `wood_facade` (urman_w01), board `wood_fence`/`wood` (weathered_wood_boards),
   end `wood_cut` (процедурные годовые кольца), painted `wood_painted_blue|green|trim`,
   metric-UV `wood_log_uv`/`wood_fence_uv` не тронуты (их UV — контракт, см. реестр W01/W02).
2. Износ по физической причине, а не «рябь»:
   - **низ доски/панели** — `wear_bottom_gain` (шейдер 432–449), измеряется от
     **собственного основания** экземпляра через уже существующий instance uniform
     `ground_base_y` (VIS-033): `wood_fence` 0.60 @ 0.34 м, `wood_facade` 0.45 @ 0.30 м,
     `wood` 0.35 @ 0.28 м, `wood_prop` 0.25 @ 0.18 м, `stone_foundation` 0.30 @ 0.26 м.
     Никакого global Y: на склоне пояс остаётся у ножки, а не на середине стойки.
   - **торец** — `wear_end_grain` 0.22 для `wood_cut` (шейдер 443–448): roughness по
     кольцам +0.22, albedo −0.35·кольцо, т.е. срез сохнет и темнее по той же причине,
     по которой он впитывает воду.
   - **кромка/ручка/сквозной контакт** — маска `wood_wear_v1_mask.png`
     (`WearRoughness` 0.18–0.20, `WearTint` `6b5a48`, blend 0.50–0.55). Файла ещё нет:
     слот привязан условно, заказ — MAT-W03.
3. Направление волокон по конструкции сохранено (`upright_texture`,
   `local_floor_texture`, `local_fence_rail`, 1425–1431) и не расширялось вслепую.
4. `cell_jitter`/`macro_stain` остались анти-плиточными инструментами; для семейства
   досок период поднят явно: `wood` scale 0.65 → **0.50** (тайл 1.54 → 2.0 м),
   `wood_fence` 0.80 → **0.55** (1.25 → 1.82 м), `wood_furniture` 0.95 (1.05 м),
   `wood_prop` 0.90 (1.11 м), `log_wall` 0.90 (1.11 м). `wood_facade`/`wood_log_uv`
   сознательно оставлены на 1.0 м: в urman_w01 доски нарисованы ~17 см, удвоение тайла
   дало бы 34 см доски — форма солгала бы. Анти-плиточность там держат `cell_jitter`
   0.35 и macro breakup 3 м.
5. Свет не запекается: ни один albedo-файл не менялся, изменения только в roughness/mask.

**Как проверяется.** `--contract` (правило «≥1 м на тайл» для 11 перетилённых семейств,
сейчас PASS). Критерии «no visible tile at 5–10 m» и «направление по конструкции» —
P01/P17/P18 close + дальний кадр; `eng/verify-painterly-textures.sh` по файлам не
применялся (новых PNG не заводилось).

**Статус:** `CODE_DONE_VERIFY_PENDING`; подпункт «явный период» — `DONE` (гейт).

---

## VIS-094 — штукатурка общественных зданий: художественно, но материально

**Сделано.**
1. Macro-неоднородность стала параметром, а не случайностью: `macro_scale`
   (по умолчанию `(0.33, 0.11)` = прежние ~3 м) и `macro_gain` (по умолчанию 1.0) —
   шейдер 149–150, применение 348–354. Обратная совместимость: ни одно здание не
   изменилось, пока не вызвано новый API.
2. `ForCivicSurface(html, role, sheltered)` (1240–1264) + таблица `CivicFinishes`
   (844–842…): role → (макроскоп, soiling):
   - `clinic` (ФАП): macro `(0.42, 0.14)` ≈ 2.4 м, gain 1.15, grime **0.075** @ 0.55 м,
     тон `6d7272` — работающее чистое учреждение;
   - `club` (ДК): `(0.20, 0.07)` ≈ 5 м, gain 1.45, grime 0.155 @ 1.05 м, `6b6153`;
   - `school`: `(0.29, 0.10)` ≈ 3.4 м, gain 1.30, grime 0.115 @ 0.80 м, `666459`;
   - `admin` (сельсовет/контора): `(0.24, 0.09)`, gain 1.20, grime 0.095 @ 0.70 м;
   - `sacred` (мечеть): `(0.26, 0.09)`, gain 1.10, grime 0.035 @ 0.45 м — хоррорного
     старения нет, только широкий спокойный тон.
   Все длины волн в требуемом диапазоне 2–8 м; микrorельеф 0.26 @ (8,8) ≈ 12 см, т.е.
   **не** доминирует на 10 м (шейдерный fade гасит его раньше пикселя).
3. «Локальные ремонты» — отдельная маска `civic_repair_v1_mask.png`
   (`WearTint` `d9d6cb`, blend 0.35, `WearRoughness` −0.06) для `wall_institution`;
   заказ MAT-C03. Никаких одинаковых decal-splats: событие живёт в маске 1 тайл ≈ 2.2 м.
4. Base grime по причине, а не «плесень на всём»: шейдер 451–462, `pow(rise, 1.6)` от
   **собственного основания стены** (`ground_base_y`), ragged-шум 0.55+0.65·noise и жёсткий
   шейдерный потолок **0.45**; для `sheltered: true` grime всегда 0 (1257) — интерьер и
   навес не пачкаются.
5. Readability signage не затронут: вывески идут через `CivicSurfaceLibrary.Face`
   (отдельные материалы), ни один вызов не менялся.
6. Тайл штукатурки поднят: `plaster` (1.5, 1.15) → **(0.70, 0.55)** (0.67×0.87 м →
   1.43×1.82 м), `wall_institution` 1.00 → **0.55** (1 м → 1.82 м). GATE-таблица
   `CATALOG_EXPECTED` синхронизирована (urman_b03 0.55×0.55).

**Как проверяется.** `--contract` PASS. «ФАП/ДК/школа различимы материалом и функцией» —
P09/P24 фиксированные виды, нужны кадры; пока роль не назначена вызовом, здание
выглядит как раньше (аддитивность).

**Статус:** `CODE_DONE_VERIFY_PENDING` (нужен HANDOFF-перевызов на роли).

---

## VIS-095 — нормализация металла: кровли, трубы, эмаль

**Сделано (painterly-половина).**
1. Metallic-политика (1435–1447): `metal` = **0** (это и крашеный лист, и «не знаю, что
   за металл» — честное направление, как требует «нет неметаллической краски с
   metallic>0»), `zinc_sheet` = **0.90** (новая явная оцинковка), `iron` = 0.65 →
   **0.78** (кованое/литьевое железо физически металл), `enamel` = 0. Транспортные
   арки соседней карточки (VIS-105) не тронуты.
2. Возраст и мороз в roughness: `frost_roughness` по семействам (шейдер 463–470,
   работает только на вверх смотрящих гранях `smoothstep(0.45,0.85, N·up)` и **забирает**
   specular вполовину от прироста) — metal 0.10, zinc_sheet 0.14, iron 0.08,
   roof 0.10, roof_metal 0.14, wood_facade 0.04. Интенсивность теперь модулируется
   состоянием света: `SetSnowMood(..., frostScale = 1f)` (981–1017) — необязательный
   последний параметр, существующий вызов `Act1ConnectedWorld.cs:1846` компилируется
   и ведёт себя как раньше.
3. Разведение четырёх состояний: painted steel (`metal`), bare/wrought (`iron`),
   galvanized sheet (`zinc_sheet`), редкое ржавое повреждение (маска
   `metal_wear_v1_mask.png`: roughness +0.26…0.30, metallic → 0.05…0.10, тон `6b4a35`),
   эмаль со сколами (`enamel` + `enamel_chip_v1_mask.png`).
4. `RuralPropMaterials.cs` переписан в декларативную таблицу `Finishes` (30–64) с
   явным `surfaceTileMetres` и **политикой покрытия**: steel/enamel/laminate/plastic/
   ceramic/concrete = metallic 0; metal/zinc/brass = metallic 1 c
   `MetallicSpecular` 0.55–0.62 и roughness 0.30–0.52 (не chrome). У всех красок
   `MetallicSpecular` снижен с прежних 0.50 до 0.24–0.35 (раньше краска имела
   металлический профиль блика — ровно тот art-trick, который карточка запрещает).
   Нормальные и ORM-слоты подвешены условно (95–122): `NormalTexture`/`NormalScale`,
   `MetallicRoughnessTexture` c G=roughness, B=metallic; файлов пока нет → картинка
   прежняя. Мета `metallicPolicy`/`responseNormalPending`/`responseOrmPending` для
   receipts.
5. Новые kind-ы `enamel` и `zinc` добавлены; ни один существующий kind не удалён и не
   переименован (`wood/plywood/steel/laminate/cloth/upholstery/velvet/curtain/plastic/
   rubber/ceramic/earthenware/metal/brass/concrete` сохранены).

**Как проверяется.** `--contract` проверяет metallic-политику по обоим владельцам
(«painterly metallic arms: iron=0.78, vehicle_bare_metal=0.85, vehicle_trim_metal=1.0,
zinc_sheet=0.90», 17 finishes RuralProp, 0 нарушений) — это и есть критерий
«нет неметаллической краски с metallic>0» в статической форме. «Highlight shape» —
кадры: кровля P01/P08/P09, труба/ведро P21, чайник/миска P17.

**Статус:** `DONE` (статический metallic-критерий), `CODE_DONE_VERIFY_PENDING` (вид).

---

## VIS-034 — снег после исправления формы

**Сделано.**
1. Числовая защита карточки зафиксирована гейтом: амплитуда микро-высоты осталась
   `(r-0.5) * 0.004 * snow_relief_scale * up`, т.е. **±0.002 м**, и `--contract` падает,
   если строка изменится. Масштаб сугроба по-прежнему делает геометрия (VIS-077…079).
2. Период микрополя объявлен явно (`snow_micro_scale`, шейдер 515/521,.vertex 13):
   `snow_ground` 0.80 (1.25 м), `snow_road` 0.55 (1.82 м), `snow_trampled` 0.70 (1.43 м),
   `snow_grass` 0.90, `snow_roof` 1.10 (0.91 м), `ice` 0.40 (2.5 м). Разные состояния
   перестали шуметь одинаково на 10 м.
3. Различение рыхлое/утоптанное усилено без шумового фильтра: уплотнённый след теперь
   **холоднее**, а не просто темнее — `ALBEDO *= mix(1, vec3(0.905, 0.930, 0.985), packed)`
   (шейдер 561–565) вместо прежнего равномерного 0.91; roughness следа 0.79 в диапазоне
   `snow_trampled` (0.72–0.88) сохранён. След остаётся снегом, а не «вырезанной дырой».
4. Базовый светлый тон не тронут: `snow_color` по-прежнему `(0.93, 0.95, 0.97)` и
   модулируется только `SetSnowMood` из atmospheric profiles.

**Как проверяется.** `--contract` (амплитуда) + требуемые P03 snow close / P06 along
road / R022/R023, статичный и движущийся кадр, пробы 0.5/3/10 м.

**Статус:** `DONE` (числовой пол), `CODE_DONE_VERIFY_PENDING` (приёмка вида).

---

## VIS-081 — snow material под новую форму, а не наоборот

**Сделано.**
1. Искра стала/view-dependent (`sparkle_grazing_only`, шейдер 525–532):
   `grain *= smoothstep(0.12, 0.70, 1 − N·V)`. Флэт-прибавка specular по всей площади —
   это и было «glitter при рассеянном свете»; теперь блеск живёт у скользящих углов.
   Флаг включают только семейства с ненулевым `snow_sparkle` и сами снежные family,
   остальные материалы не изменились.
2. Roughness снега калибруется состоянием, а не константой: `SetSnowMood` теперь
   модулирует и `frost_roughness` (981–1017) с мемоизацией базового значения в
   `_snowBase` (Field: 894, сброс в `ClearCacheForHeadlessTests` 1051–1057). Заодно
   исправлена реальная потеря: `sparkleScale` клампился к 1.0, из-за чего авторское
   значение профиля «морозное утро» (`sparkle: 1.2` в `game/content/world/atmosphere.v1.json:66`)
   молча урезалось; теперь диапазон 0…2 и 1.2 действительно применяется.
3. **Новая family map не заказана «на всякий случай»:** существующие albedo
   (`urman_s01/s02`, `snow_trampled_v1`, `snow_roof_v1`, `snow_grass_peek_v1`,
   `ice_patch_v1`, пара `snow_micro_*`) признаны достаточными; единственная заявка —
   правка tone `snow_trampled_v2_albedo.png` (MAT-S01, строка S03 реестра). Это ровно
   запрет «не плодить v7/v8 без доказанного блокера»: блокера нет, новый слой — только
   roughness/mask-класс, а не ещё одна albedo.
4. Seam-гейты карточки (mean ≤ 0.12, max ≤ 0.40) принадлежат
   `eng/verify-painterly-textures.sh`; проверено на одном файле-кандидате
   (urman_b03: 0.0203/0.0189 mean, 0.1529/0.0902 max, PASS). Полный прогон по каталогу
   на станции не выполнялся (dekoding ~50 файлов в pure Python — минуты CPU; это шаг
   приёмки, не этой правки).

**Как проверяется.** `--contract` PASS; P07 distance sweep + close/mid/far: «остаётся
snow, нет tile square на 5–15 м».

**Статус:** `CODE_DONE_VERIFY_PENDING`; `DONE` для части «ни одной новой v7/v8 albedo».

---

## VIS-080 — снег на крыши и горизонтальные детали (материальная половина)

Итерация 03 журнал не брала эту карточку из-за риска: материальный снег на `iron`/
`log_wall`/`wood_painted_trim` попадает в интерьеры. Риск реален и найден снова:
`FacilitySolid` → `AddVisualBox` (`Act1ConnectedWorld.MosqueInterior.cs:661`,
`Act1ConnectedWorld.cs:10435`) **никогда не передаёт `sheltered`**, поэтому
`wood_painted_trim` (потолки/пилоны/обвязка мечети: `MosqueLayoutV4.cs:25,26,29,53,114`,
`MosqueInterior.cs:85–88,174`) получал кровельное одеяло 0.45 внутри зала.

**Сделано без чужих файлов.**
1. Снежное одеяло разделено на «форму» и «тонкий слой» как требует карточка:
   - **shape** остаётся геометрией (`snow_roof`-меши, VIS-077…079 — не этот пакет);
   - **thin layer** получает правильную величину: `wood_painted_trim` 0.45 → **0.28**
     и band `(0.66, 0.90)` (1593, 1609–1618), `plaster`/`wall_institution` 0.42 → **0.34**
     с band `(0.74, 0.92)` — стена снежится по парапету/отливу, а не по всей плоскости.
2. Band по семействам (`snow_settle_band`, шейдер 481–495): roof/roof_metal
   `(0.62, 0.86)`, stone `(0.58, 0.84)`, wood_facade/wood `(0.52, 0.80)`,
   log_wall `(0.58, 0.86)`, trim `(0.66, 0.90)`, остальные остаются на прежних
   `(0.30, 0.72)` → вертикальные и защищённые поверхности больше не снежатся как
   горизонтальные (прямой запрет карточки).
3. Снег следует **форме детали**: `snow_follows_local_normal` (шейдер 486–490) включает
   измерение вверх-грани в local-кадре, и оно включено только там, где local-кадр
   действительно сохраняет вертикаль (`zyx`-перестановка): `hay_bundle`, `hay_fibers`
   (1620–1622, плюс `ForLocalWoodPiece` наследует семейство `wood_prop` — не включено,
   переносимому предмету одеяло 0.14 уже назначено). `xzy`-перестановка у `wood_fence_rail`
   **намеренно не включена**: она переносит длину рейки в Y, и local-Y там — не «верх».
4. Instance-путь вместо дубляжа: `instance uniform float snow_shelter` (144) и
   `SetSnowShelter(GeometryInstance3D, float)` (1177) + групповой помощник
   `CivicSurfaceLibrary.SetSnowShelter(Node3D, float)` (`CivicSurfaceLibrary.cs:73`).
   Значение по умолчанию 0 во **both** places (шейдер и материал, 1409) — поэтому до
   первого вызова картинка не меняется ни у одного объекта, и правка безопасна при
   любой семантике нерасставленного instance-uniform.
5. Интерьерные «дыры» не закрыты молча: они переданы владельцу с точным кодом (ниже).

**Как проверяется.** Статически: `--contract` не находит ни одного семейства с
необъявленным band; значения перечислены здесь. Кадрово: P01 (hero roof), P08/P09
(ДК/мечеть/минарет), P21 close + wide; отдельный тест — «под навесом снега нет».

**Статус:** `CODE_DONE_VERIFY_PENDING`.

---

## VIS-038 — уменьшить дублирование материалов одного семейства

**Сделано.**
1. Прежнее слияние `fabric`→`cloth` (1364) сохранено и документировано как единственный
   байтово-одинаковый дубль: сводный аудит 7 карт / 17 семейств (§5.1 карты дефектов)
   перечисляет остальные пары как различающиеся **флагами проекции**, а не цветом, —
   объединять их запрещено той же карточкой («не мержить по одному albedo, игнорируя
   shader mode»), поэтому они не сливались.
2. Различие tint вынесено в разрешённый instance-путь: `instance uniform vec3
   instance_pigment_mul` + `SetInstanceTint(instance, surface, targetHtml)` (1190–1205)
   и `ForSharedTint(surface, sheltered)` (1227–1231). Отношение считается **в linear
   light** (`ToLinear`, 1219–1220), потому что `base_color` — `source_color` uniform;
   multiplier вне диапазона (0.025…4) возвращает `false` → владелец остаётся на точном
   `ForColor`, а не получает приближение. Это ровно требование «0 новых уникальных материалов
   для визуально одинакового класса» для пилотной ограды: два тинта рейлок
   (`Act1ConnectedWorld.cs:9692–9693`: `979a92` и `888c84`, один и тот же `wood_fence_uv`)
   могут жить в одном материале.
3. `ForCivicSurface` кладёт различие зданий в **3 параметра на роль**, а не в N цветовых
   состояний: ключ кэша `civic:{surface}:{hex}:{exposed|sheltered}:{role}` (1247) —
   ролей 5, а не по материалу на здание.
4. Receipt вместо догадок: `CachedMaterialCount` (1268) и `DescribeCachedMaterials()`
   (1272–1297) — surface, вариант шейдера (rigid/two-sided/cutout/deforming), флаги
   новых слотов, metallic/roughness/snow_coverage/base_color. Это то, чем доказывается
   «число ресурсов не растёт» и «правильный snow/deform режим сохранён».

**Как проверяется.** Статически — контрактный гейт + перечисление ключей. Кадры:
P02 fence + P11 close, A/B с фиксированными динамическими масками (E034/R029) и
material-count receipt.

**Статус:** `CODE_DONE_VERIFY_PENDING`; `DONE` для части «механизм instance-tint и
receipt существуют и не плодят состояния» (проверено чтением/гейтом).

---

## VIS-035 — свести масштаб двух соседних материалов в доме

**Сделано (пара обои ↔ стол в hero-комнате, I021/R014).**

| Параметр | Было | Стало | Почему |
|---|---|---|---|
| `wallpaper` texture_scale | (1.10, −1.10) → тайл 0.91 м | **(0.55, −0.55)** → 1.82 м | орнамент перестает быть сеткой на 2 и 5 м; направление V сохранено (стебли внизу) |
| `wallpaper` variation | 0.08 (в группе с log_wall/wall_institution) | **0.07** своей веткой (1508–1511) | −12.5 % macro-конраста конфликтующей фактуры, в разрешённом карточкой диапазоне 10–20 %; global saturation не тронута |
| `wallpaper` texture_strength | 0.92 (в группе) | **0.90** (1536–1537) | ещё немного в пользу крупной формы |
| `wood_furniture_interior` scale | 1/0.75 → 0.75 м | **1/1.2 → 1.2 м** (633) | стол и обои теперь живут в одной метрике; доска не превращается в «микрорисунок» |
| `fabric_upholstery`/`cloth_clinic`/`cloth_towel` variation | 0.08 | **0.07** | та же причина для тканей; `cloth`/`fabric` (одежда NPC) оставлены 0.08 — их alias общий с персонажами, карточка требует ограничить патч указанным потребителем |

Гейт `--contract` фиксирует «≥1 м на тайл» для 11 перетилённых семейств; таблица
`CATALOG_EXPECTED` в том же файле обновлена (wallpaper 0.55/−0.55, urman_b03 0.55,
urman_w08 1/1.2), чтобы source-gate не врал после правки.

**Как проверяется.** P17 home table и P05 seated face, пробы 0.7 / 2 / 5 м; решение по
паре остаётся за человеком (карточка прямо это требует).

**Статус:** `CODE_DONE_VERIFY_PENDING`.

---

## Аудит существующих кандидатов (почему ничего нового albedo не заведено)

Прямой запрет VIS-005/VIS-081. Проверено списком §5.4 карты дефектов:

| Семейство | Кандидат на диске | Решение |
|---|---|---|
| `roof` (B09) | `roof_slate_v1_albedo.png` | **не подключать**: реестр 22.09 описывает просмотр («целые волнистые листы/стыки/мох/тени») — это не material-only. Заказан normal `roof_sheet_v1_normal.png` (MAT-R01) |
| `roof_metal` | `roof_metal_v2_albedo.png` | не подключать вслепую: карточка B09 запрещает шифер на metal; сначала MAT-M01/M05 |
| `bark_birch`, `leaf_birch` | `bark_birch_v1/v2`, `leaf_birch_v1/v2` | вне этого пакета (VIS-082/083/084 — силаэт и ветвление); response уже дан числами, albedo не менялась |
| `grass` | `grass_verge_v1/v2` | вне пакета (VIS-028/029) |
| `wood_carved` | `wood_carved_gate_v1_albedo.png` | response дан числами (0.34 @ (6,20)); подключение карты — решение VIS-090 по конкретному резному элементу |
| `ornament_trim` | `ornament_trim_v1_albedo.png` | получателей нет (§5.3) — заказывать/подключать нельзя |
| `cloth_table`, `terrain`, `grass_tuft`, `wattle` | есть/нет файлы | получателей нет — строки не тронуты, `wattle`/`rowan_berries` зарегистрированы как MAT-X02/MAT-X01 без генерации |
| `snow_*` | `snow_fresh_v1/v2`, `snow_road_v1` | не подключать: старые поколения, принятые карты `urman_s01/s02` остаются |

Ни один PNG не удалён, ни одна `.import` не тронута, импорт не запускался.

---

## HANDOFF (файл → метод → точный код)

### H-1. Снег под навесами и в интерьерах — VIS-080
Владелец: `Act1ConnectedWorld*` / интерьерные билдеры.
Точка правки 1: `game/scripts/Act1ConnectedWorld.MosqueLayoutV4.cs`, сразу после
`AddVisualBox(room, "MosqueCeiling", …)` (строки 25–29) и после `FacilitySolid(...)`
обвязки (53, 114):
```csharp
foreach (var mesh in room.FindChildren("*", nameof(GeometryInstance3D), true, false)
                     .OfType<GeometryInstance3D>())
    PainterlyMaterialLibrary.SetSnowShelter(mesh, 1f);
```
Точка правки 2 (дешевле и точнее): в `Act1ConnectedWorld.AddVisualBox`
(`game/scripts/Act1ConnectedWorld.cs:10435`) добавить параметр `bool sheltered = false`
и перед возвратом:
```csharp
if (sheltered) PainterlyMaterialLibrary.SetSnowShelter(node, 1f);
```
затем в `Act1ConnectedWorld.MosqueInterior.FacilitySolid` (строка 661–664) пробросить
`sheltered: true`. То же для веранд/навесов: `Act1ConnectedWorld.CulvertVerandaDiscoveries.cs:140,173`
(уже `sheltered: true` в материале — достаточно ничего не менять) и
`AgentBAct1ExteriorLayer.cs:357,465,519` (`supportOwner` = covered → `SetSnowShelter(..., 1f)`).

### H-2.Exposed metal получает честное семейство — VIS-095
`game/scripts/Act1ConnectedWorld.Bathhouse.cs:99` (кровля бани — оцинковка),
`game/scripts/CarryableProp.Geometry.cs:13` (ведро),
`game/scripts/Act1ConnectedWorld.YardMechanisms.cs:406`,
`game/scripts/Act1ConnectedWorld.ShopSign.cs:72,79` (щит — **крашеный**, оставить `metal`),
`game/scripts/Act1ConnectedWorld.QuietCareDiscoveries.cs:217` (таз — `enamel`), `:223`
(эмалированная кружка — `enamel`), `game/scripts/Act1ConnectedWorld.MosqueInterior.cs:522,742`
(металлические детали зала — `metal` либо `zinc_sheet` по факту).
Замена только там, где металл физически голый:
```csharp
PainterlyMaterialLibrary.ForColor("636a66", "zinc_sheet")     // было "metal"
PainterlyMaterialLibrary.ForColor("d1cfc2", "enamel")         // было "metal"
```
`RuralPropMaterials`: `Surface("steel", …)` остаётся краской; для оцинковки
`RuralPropMaterials.Surface("zinc", tint)`, для эмали `Surface("enamel", tint)`.
Внутренние панели `Act1ConnectedWorld.PolicePost.Materials.cs:49` уже переводят
painterly `metal` → `Rural_steel` (краска) — это корректно, менять не нужно.

### H-3. Роли общественных фасадов — VIS-094
`game/scripts/Act1ConnectedWorld.cs:2096–2109` (FapPaintedSage/DustyBlue →
`ForCivicSurface(color, "clinic")`), `Act1ConnectedWorld.cs:9078` (`FapFacadeWall` →
`"clinic"`), билдеры школы/ДК/сельсовета в `Act1ConnectedWorld.PublicBuildings.cs`
(138, 241–250, 300–306, 385–392, 566–609) и `CivicSurfaceLibrary`-потребители ДК →
`"school"` / `"club"` / `"admin"`; мечеть (`Act1ConnectedWorld.cs:8136–8255`) → `"sacred"`.
Форма вызова:
```csharp
["FapPaintedSage"] = CivicSurfaceLibrary.PublicFacade("clinic", "7b8b80"),
["DistantWall"]    = PainterlyMaterialLibrary.ForCivicSurface("6f716a", "club"),
```
И для точности soiling-пояса по основанию стены:
`PainterlyMaterialLibrary.SetGroundContact(wallMesh, supportWorldY)` уже существующим
вызовом (VIS-033) — grime измеряется от него же.

### H-4. Пилот ограды без дублей материалов — VIS-038
`game/scripts/Act1ConnectedWorld.cs:9692–9727` (`AddVisualFenceRun`):
```csharp
var railShared = PainterlyMaterialLibrary.ForSharedTint("wood_fence_uv");
var rail = new MeshInstance3D { Name = $"{name}{suffix}", Mesh = surface.Commit(),
    MaterialOverride = railShared };
if (!PainterlyMaterialLibrary.SetInstanceTint(rail, "wood_fence_uv", color))
    rail.MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "wood_fence_uv");
```
(семейная база `wood_fence_uv` = `979a92`, зарегистрирована в `FamilyTintBase`,
`PainterlyMaterialLibrary.cs:866–875`; `888c84` в linear-light — это ×1.26/×1.24/×1.26,
внутри допуска). Проверка эквивалентности — A/B кадр, как требует карточка.

### H-5. Receipt числа материалов — VIS-038
`game/scripts/Act1DemoRoot.RenderDiagnostics.cs` (рядом со `UsesSharedShadowMaterial`,
строка 256) добавить в JSON:
```csharp
["painterlyMaterialCount"] = PainterlyMaterialLibrary.CachedMaterialCount,
["painterlyMaterials"] = PainterlyMaterialLibrary.DescribeCachedMaterials(),
```

### H-6. Frost по состоянию света — VIS-095/081
`game/scripts/Act1ConnectedWorld.cs:1846–1847`:
```csharp
PainterlyMaterialLibrary.SetSnowMood(
    profile.SnowColor, profile.SnowCoverage, profile.SnowSparkle, profile.SnowTintStrength,
    profile.SnowFrost);      // новый необязательный параметр профиля
```
`game/scripts/AtmosphereProfiles.cs` (запись 20: `Color SnowColor, float SnowCoverage,
float SnowSparkle, float SnowTintStrength, string Identity`) → добавить `float SnowFrost`
и чтение `"frost"` в блоке `snow` (99), дефолт `1f` при отсутствии ключа;
`game/content/world/atmosphere.v1.json` → `"snow": { …, "frost": <0…2> }` по состояниям
(предложение: морозное утро 1.3, пасмурный 1.0, golden hour 0.6, сумерки 1.1,
зелёная ночь 1.2, лесная ночь 1.4). До этой правки поведение идентично нынешнему
(frostScale = 1).

---

## Открытое, что нельзя закрыть из репозитория

1. Ни один визуальный критерий этой волны не принят: нужны пары before/after на C1…C8
   (и P01/P02/P03/P05/P09/P11/P17/P18/P21) с одинаковыми camera/FOV/resolution/профилем.
2. GLSL компилируется только движком: новые `instance uniform` (snow_shelter,
   instance_pigment_mul) и три блока выборки проверяются первым же станционным прогоном
   (том же классе риска, что `ground_base_y` в итерации 02).
3. 15 response-карт нет на диске; пока их нет, families живут на процедурном отклике.
   Заказ — `docs/urman_knowledge_base/art/asset_requests/MAT.md`.
4. `C:\`-специфика гейта: на станции `python3` — Store-заглушка, а stdout в cp1251 ронял
   отчёт на символе «×». Оба исправлены в `eng/verify-painterly-textures.sh` (проверка
   интерпретатора + UTF-8 with replace). Полный прогон pixel-режима по всему каталогу
   на станции не выполнялся в этой волне (дорог в pure Python), одиночный файл — PASS.
5. Полная таблица metallic/roughness/band/period — в этом файле; перенос в
   `act1_acceptance_matrix`/`execution_backlog` не делался (статусы карточек ведутся
   владельцем очереди).
