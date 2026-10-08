# УРМАН — сводный запрос на генерацию текстур и ассетов (visual reset, 2026-10-08)

**Зачем этот файл.** Переделка визуального языка Акта I (VIS-001…VIS-118) требует фактур и мешиков, которых нет в репозитории. Исполнитель их не генерирует: ниже то, что нужно произвести GPT-Image или Blender. Имена уже имеют потребителя в коде, поэтому после генерации файл кладётся по указанному пути и включается существующей привязкой.

**Как сдавать.** Для каждого блока: положить файл(ы) в указанный `destination`, соблюсти `format`/`size`/`colorspace`, не менять имя. Проверка — в блоке `verify`. Если кадр после подстановки не стал лучше, блок возвращается в работу, а не принимается «потому что файл существует».

**Собрано из фрагментов лан:** MAT, TREES, CHAR, YARD, ATMOS

**Правила, которые ограничивают список (зафиксированы пакетом):**
- сначала переиспользование: часть карт уже лежит на диске и не подключена — новые albedo для той же функции не заказываются, пока кандидат не сверен и не отвергнут с причиной;
- ни одна фактура не является однотонной заглушкой;
- нет чехарды v7/v8 вариантов: запрос на замену принятой карты помечен `edit` и объяснён;
- нет запечённого света и бликов в albedo, нет «плесени на всё», нет chrome-look;
- видимое low-poly не является целью; красный не является базовым цветом леса;
- у каждого ассета есть consumer, provenance и лицензия (гейт `eng/verify-asset-registry.sh`).

## Что именно сгенерировать (сводка имён)

Полные спецификации — в разделах лан ниже (consumer / контекст / формат / UV / размер / назначение / референс / проверка). Здесь только перечень имён, чтобы ничего не потерялось.

### Нужны новые файлы

| Файл | Лана | Где описан |
|---|---|---|
| `civic_plaster_v1_normal.png` | MAT | ### MAT-C01 — `civic_plaster_v1_normal.png` |
| `civic_plaster_v1_roughness.png` | MAT | ### MAT-C02 — `civic_plaster_v1_roughness.png` |
| `civic_repair_v1_mask.png` | MAT | ### MAT-C03 — `civic_repair_v1_mask.png` |
| `enamel_chip_v1_mask.png` | MAT | ### MAT-M06 — `enamel_chip_v1_mask.png` |
| `fur_pile_v1_normal.png` | CHAR | ### CHAR-C02 — `fur_pile_v1_normal.png` (та же папка) |
| `garment_felt_v1_normal.png` | CHAR | ### CHAR-C01 — `garment_knit_v1_normal.png`, `garment_felt_v1_normal.png` (`game/assets/textures/response/`) |
| `garment_knit_v1_normal.png` | CHAR | ### CHAR-C01 — `garment_knit_v1_normal.png`, `garment_felt_v1_normal.png` (`game/assets/textures/response/`) |
| `garment_knit_v1_roughness.png` | CHAR | ### CHAR-C03 — `garment_knit_v1_roughness.png` (условная, только если C01 не закрыл вопрос) |
| `metal_galvanized_v1_normal.png` | MAT | ### MAT-M02 — `metal_galvanized_v1_normal.png` |
| `metal_galvanized_v1_roughness.png` | MAT | ### MAT-M07 — `metal_galvanized_v1_roughness.png` (`game/assets/textures/response/`) |
| `metal_painted_v1_normal.png` | MAT | ### MAT-M01 — `metal_painted_v1_normal.png` |
| `metal_wear_v1_mask.png` | MAT | ### MAT-M05 — `metal_wear_v1_mask.png` |
| `metal_wrought_v1_normal.png` | MAT | ### MAT-M04 — `metal_wrought_v1_normal.png` |
| `realism_20260929/metal_galvanized_v1_normal.png` | MAT | и `RuralPropMaterials` `metal`/`zinc` (`realism_20260929/metal_galvanized_v1_normal.png`, |
| `roof_sheet_v1_normal.png` | MAT | ### MAT-R01 — `roof_sheet_v1_normal.png` |
| `rowan_berries_v1_albedo.png` | MAT | ### MAT-X01 — `rowan_berries_v1_albedo.png` (`res://assets/textures/painterly/`) |
| `snow_trampled_v2_albedo.png` | MAT | ### MAT-S01 — `snow_trampled_v2_albedo.png` (`edit` строки S03 реестра 22.09) |
| `wattle_weave_v1_albedo.png` | MAT | ### MAT-X02 — `wattle_weave_v1_albedo.png` (`res://assets/textures/painterly/`) |
| `wood_grain_v1_normal.png` | MAT | ### MAT-W01 — `wood_grain_v1_normal.png` (folder `game/assets/textures/response/`) |
| `wood_grain_v1_roughness.png` | MAT | ### MAT-W02 — `wood_grain_v1_roughness.png` |
| `wood_wear_v1_mask.png` | MAT | ### MAT-W03 — `wood_wear_v1_mask.png` |

Всего к генерации: **21**.

### Уже лежат в репозитории — их не генерировать, а подключать/сверять

Эти имена упомянуты в заявках, но файл уже существует в дереве: заказывать его заново нельзя (правило переиспользования из VIS-005 и реестра текстур).

| Файл | Лана | Где упомянут |
|---|---|---|
| `bark_birch_v1_albedo.png` | TREES | §5.4 на диске уже лежат и **не подключены**: `bark_birch_v1_albedo.png`, |
| `bark_birch_v2_albedo.png` | TREES | `bark_birch_v2_albedo.png`, `leaf_birch_v1_albedo.png`, |
| `bark_pine_v1_albedo.png` | TREES | `pine_foliage_v3_albedo.png`, `bark_pine_v1_albedo.png` (имя файла занято |
| `leaf_birch_v1_albedo.png` | TREES | `bark_birch_v2_albedo.png`, `leaf_birch_v1_albedo.png`, |
| `leaf_birch_v2_albedo.png` | TREES | `leaf_birch_v2_albedo.png`, `pine_foliage_albedo.png`, |
| `mansur_age_v1_albedo.png` | CHAR | | Подключение `mansur_age_v1_albedo.png` к human-v2 Mansur | файл подключён только в мёртвой процедурной ветке (Mansur живёт в human-v2, см. `ledger_C |
| `old_fabric_v3_albedo.png` | CHAR | (альбедо остаётся `old_fabric_v3_albedo.png`). |
| `pine_foliage_albedo.png` | TREES | `leaf_birch_v2_albedo.png`, `pine_foliage_albedo.png`, |
| `pine_foliage_v2_albedo.png` | TREES | `pine_foliage_v2_albedo.png` = `foliage`, `bark_pine_v1_albedo.png` = |
| `pine_foliage_v3_albedo.png` | TREES | `pine_foliage_v3_albedo.png`, `bark_pine_v1_albedo.png` (имя файла занято |
| `roof_slate_v1_albedo.png` | MAT | `roof_slate_v1_albedo.png`: реестр 22.09 (B09) прямо отвергает этот файл как |
| `weathered_wood_boards_v2_albedo.png` | YARD | | «Новая доска для каждого из 5 семейств заборов» | `wood_fence` (`weathered_wood_boards_v2_albedo.png`, relief `.45`, freq `3.2×30`) и `wood_painted_ |

Всего уже на диске: **12**.

## Разделы по ланам

| Лана | Разделов во фрагменте | Фрагмент |
|---|---|---|
| MAT | 24 | `docs/urman_knowledge_base/art/asset_requests/MAT.md` |
| TREES | 21 | `docs/urman_knowledge_base/art/asset_requests/TREES.md` |
| CHAR | 6 | `docs/urman_knowledge_base/art/asset_requests/CHAR.md` |
| YARD | 6 | `docs/urman_knowledge_base/art/asset_requests/YARD.md` |
| ATMOS | 4 | `docs/urman_knowledge_base/art/asset_requests/ATMOS.md` |


---

# ЛАНА: MAT

# Заявки на material-response текстуры — MAT (visual reset 2026-10-07)

Владелец: карточки VIS-092/093/094/095/081/034 (материальный отклик). Формулировка по
схете ТЗ05 / `AGENTS.md`: **consumer / контекст / формат / UV / размер / назначение /
референс / проверка**.

Правила этой волны, которые ограничивают список:

1. **Сначала переиспользование.** `docs/production/visual_reset_maps_2026-10-07/material_consumers_RU.md`
   §5.4 перечисляет PNG, которые уже лежат в репозитории и не подключены. Для новых семейств
   `bark_birch`, `leaf_birch`, `grass`, `roof`, `roof_metal`, `ornament_trim`, `wood_carved`
   новые albedo **не заказываются**: карточка VIS-005 это прямо запрещает до сверки. Ниже
   заказаны только normal/roughness/mask-слои и два отсутствующих файла (F08/F09).
2. **Не заказывать то, чего код не читает.** Каждая строка имеет слот в коде
   (`PainterlyMaterialLibrary.FamilyResponses`, `RuralPropMaterials.Finishes`), который
   файл уже привязывает условно: пока PNG нет, семейство остаётся на процедурном отклике
   и сегодняшней картинке. Проверка: `eng/verify-painterly-textures.sh --contract` падает,
   если код объявляет путь, которого нет ни на диске, ни в этом списке.
3. **Один проход, без v7/v8-чехарды.** Ни одна строка не просит замену существующей
   принятой albedo-карты; запросы на замену помечены `edit` и объяснены цитатой реестра.
4. **Запреты стиля.** Нет запечённого света/бликов в albedo, нет плесени «на всё», нет
   total-rust, нет chrome-look, нет одинакового шума на 10 м (период задаётся явно в коде).

Общее для всех normal-карт: `hint_normal`-кодирование OpenGL (X+ = право, Y+ = вверх),
8-bit RGB, без alpha, mean-RGB около `(128,128,255)`, амплитуда слабая (это painterly,
не фотоскан). Общее для всех mask-карт: один канал R в `0..255`, где `0` = нет события,
`255` = полное; доли выше `128` держим редкими (единицы процентов площади).
Общее для roughness-карт: один канал R, mean ≈ 0.5, без микронного «песка»: карта
задаёт распределение возраста, а не шум.

---

## MAT-W · Старое дерево с историей использования (VIS-093)

### MAT-W01 — `wood_grain_v1_normal.png` (folder `game/assets/textures/response/`)
- **Consumer:** `PainterlyMaterialLibrary.FamilyResponses["wood_facade"]`, `["wood_fence"]`
  (слот `NormalMap`, `NormalScale` 0.35 / 0.30; `TilesPerMetre` 0.5 → один тайл 2 м).
- **Контекст:** зимние деревенские фасады и ограды: доска, бревно, горбыль. Герой-дом,
  заборы, сараи, обвязки. Кадр P01/P02/P11, близкий и средний план.
- **Формат:** PNG RGB 8-bit, OpenGL normal.
- **UV / период:** seamless TILE, мировая трипланарная проекция (не авторский UV);
  1 тайл = 2 м; волокно ориентировано вдоль текстуры V, как в `urman_w01`/`weathered_wood_boards`.
- **Размер:** 1024×1024 (не 4K; гейт запрещает blanket 4K).
- **Назначение:** углубить продольное волокно и кромку стыка досок; не менять рисунок,
  не добавлять сучки-«паттерн» и не запекать свет.
- **Референс:** P1 (живописная поверхность без контура), LIS2-PAINT (мягкая материальность);
  существующая albedo `urman_w01_v01_basecolor.png` как носитель тона.
- **Проверка:** `--contract` (путь+эффект), затем material-sphere A/B normal on/off и
  кадр P02 fence close на 0.5/3/10 м: направление волокон читается, плиточного шва нет.

### MAT-W02 — `wood_grain_v1_roughness.png`
- **Consumer:** `FamilyResponses["wood_facade"]`, слот `RoughnessMap`, `RoughnessDelta` 0.14.
- **Контекст:** та же доска/бревно: early-lacquer и open grain должны стареть разными
  бликами, а не разными цветами.
- **Формат:** PNG R-канал (G/B равны R), 8-bit.
- **UV / период:** seamless TILE, мировая проекция, 1 тайл = 2 м.
- **Размер:** 1024×1024.
- **Назначение:** распределение ages: открытое волокно суше (выше roughness), смолистые
  и торцовые полосы плотнее. Диапазон в материале остаётся 0.82…0.89, карта добавляет ±0.07.
- **Референс:** P1; действующая `roughness_value` семейства (0.82 для `wood_facade`).
- **Проверка:** A/B на сфере и на P01: блик перестаёт быть равномерным, silhouette не
  меняется; на 10 м нет регулярного рисунка.

### MAT-W03 — `wood_wear_v1_mask.png`
- **Consumer:** `FamilyResponses["wood_facade"]`, `["wood_fence"]`, `["roof_metal"]`? — нет:
  только wood-семейства (слот `WearMap`, `WearRoughness` 0.18–0.20, `WearTint` `6b5a48`,
  `WearTintBlend` 0.50–0.55).
- **Контекст:** VIS-093 требует износ **по физической причине**: торцы досок, нижний край
  у земли, кромка у ручек и замок, место контакта с снегом и водой.
- **Формат:** PNG R-канал mask, 8-bit.
- **UV / период:** seamless TILE 1 тайл = 2 м; событие занимает единицы процентов площади,
  распределение мягкое, без правильных фигур.
- **Размер:** 1024×1024.
- **Назначение:** где mask = 1 — surface светлее/суше по тону `6b5a48`, roughness +0.18,
  metallic остаётся 0 (дерево не металл).
- **Референс:** P1, LIS2-PAINT; запрет: «не одинаковая рябь», «не запекать блики».
- **Проверка:** gate `--contract`; кадр P11/P02: износсосредоточен на торцах и понизу, а не по
  всей доске; на 5 м плиточность не читается.

---

## MAT-C · Штукатурка общественных зданий (VIS-094)

### MAT-C01 — `civic_plaster_v1_normal.png`
- **Consumer:** `FamilyResponses["plaster"]`, `["wall_institution"]` (`NormalScale` 0.28,
  `TilesPerMetre` 0.45 → тайл ≈ 2.2 м).
- **Контекст:** фасады ФАП, ДК, школы, сельсовета, мечети и их дворовых построек —
  ровные площади, которые сейчас читаются как greybox/asset-pack.
- **Формат:** PNG RGB normal, OpenGL, 8-bit.
- **UV / период:** seamless TILE, мировая проекция, 1 тайл ≈ 2.2 м; мазок мастерка
  широкий, без песочной крошки.
- **Размер:** 1024×1024.
- **Назначение:** мелкая пластика штукатурного слоя (наброс, след полутёрки) при
  **сохранении** крупного macro-рисунка, который делает шейдер (2–8 м).
- **Референс:** LIS2-PAINT; антипример: «одинаковые decal splats на всех стенах».
- **Проверка:** P09/P24 фиксированные виды школы/ДК: стена перестаёт быть плоской
  на 5–8 м, вывески (материал `CivicSurfaceLibrary.Face`) не затронуты.

### MAT-C02 — `civic_plaster_v1_roughness.png`
- **Consumer:** те же семейства, `RoughnessDelta` 0.16.
- **Контекст:** известок/мел vs масляная краска ведут себя по-разному на мокром снегу;
  сейчас обе стены получают одинаковый matte.
- **Формат:** PNG R-канал, 8-bit.
- **UV / период:** seamless TILE 1 тайл ≈ 2.2 м.
- **Размер:** 1024×1024.
- **Назначение:** локальные «ремонтные» пятна плотнее, открытая побелка суше; без
  гри́м-градиента (низ стены — ответственность шейдера `base_grime`, не карты).
- **Референс:** LIS2-PAINT; действующая `roughness_value` 0.94/0.96.
- **Проверка:** A/B на сфере и P09; specular-пятна не читаются как грязь.

### MAT-C03 — `civic_repair_v1_mask.png`
- **Consumer:** `FamilyResponses["wall_institution"]`, `WearRoughness` −0.06,
  `WearTint` `d9d6cb`, `WearTintBlend` 0.35.
- **Контекст:** «локальные ремонты по причине»: заплатка после раскопки коммуникаций,
  новый кусок у входа, подкраска после вывески.
- **Формат:** PNG R-канал mask, 8-bit.
- **UV / период:** seamless TILE 1 тайл ≈ 2.2 м; 2–4 события на тайл, мягкие границы,
  разный размер, никаких прямоугольников и следов кисти-«штампа».
- **Размер:** 1024×1024.
- **Назначение:** свежая заплатка чуть светлее и плотнее (roughness −0.06), всё остальное
  без изменений. Доля площади mask > 128 — не более ~6 %.
- **Референс:** LIS2-PAINT; запрет «плесень/ржавчина на всём».
- **Проверка:** gate `--contract`; P09/P24: читаются 1–2 ремонта на здание, не паттерн.

---

## MAT-M · Металл: кровли, трубы, эмаль (VIS-095)

### MAT-M01 — `metal_painted_v1_normal.png`
- **Consumer:** `FamilyResponses["metal"]` (`NormalScale` 0.16, `TilesPerMetre` 0.7 →
  тайл ≈ 1.4 м).
- **Контекст:** крашеные стальные листы: водосток, обделки, щиты, двери, сараи, ФАП
  кровля-«железо».
- **Формат:** PNG RGB normal.
- **UV / период:** seamless TILE, мировая проекция, тайл ≈ 1.4 м.
- **Размер:** 1024×1024.
- **Назначение:** «апельсиновая корка» краски и слабая линия фальца; **не** царапины
  металла (для этого MAT-M05).
- **Референс:** LIS2-PAINT; запрет chrome-look.
- **Проверка:** P01/P08/P09 кровли и close трубы: metallic остаётся 0, блик — широкой
  мягкой полосой.

### MAT-M02 — `metal_galvanized_v1_normal.png`
- **Consumer:** `FamilyResponses["zinc_sheet"]` (`NormalScale` 0.30, `TilesPerMetre` 0.55)
  и `RuralPropMaterials` `metal`/`zinc` (`realism_20260929/metal_galvanized_v1_normal.png`,
  `NormalScale` 0.30).
- **Контекст:** оцинковка: крыша бани, вёдра, печные трубы, короба.
- **Формат:** PNG RGB normal.
- **UV / период:** seamless TILE, тайл ≈ 1.8 м (патина крупная, не «шум»).
- **Размер:** 1024×1024.
- **Назначение:** спангл (кристаллы цинка) как очень мягкая рельефность + шов листа;
  prohibition: без «молоткового» читаемого ритма и без щелей в тайле.
- **Референс:** LIS2-PAINT; паспорт M01 реестра 22.09 («Оцинковка 0,5 м на выбранную
  крышу/ведро, не все металлы»).
- **Проверка:** P01 баня/ведро close+mid, highlight читается металлом, а не хромом.

### MAT-M03 — `metal_galvanized_v1_orm.png`
- **Consumer:** `RuralPropMaterials` `metal`/`zinc` (`MetallicRoughnessTexture`,
  roughness = G, metallic = B).
- **Контекст:** возраст оцинковки: матовая патина, единичные рыжие streaks у стыка.
- **Формат:** PNG RGB ORM (R = unused/AO, G = roughness, B = metallic), 8-bit.
- **UV / период:** seamless TILE, тайл ≈ 1.8 м.
- **Размер:** 1024×1024.
- **Назначение:** G: 0.40…0.70; B: базовое 255 (металл), зоны патины 210–255, **редкие**
  рыжие streaks B ≤ 40 (окисел — диэлектрик). Доля пикселей с B ≤ 40 не выше ~3 %.
- **Референс:** LIS2-PAINT; карточка: «rusty damage as rare mask», «не тотальная ржавчина».
- **Проверка:** gate `--contract` + P01/P21 close: ржавчина только в швах и понизу.

### MAT-M04 — `metal_wrought_v1_normal.png`
- **Consumer:** `FamilyResponses["iron"]` (`NormalScale` 0.26, `TilesPerMetre` 0.7).
- **Контекст:** кованое/литьевое железо: крюки, засовы, решётки, печи, инструмент.
- **Формат:** PNG RGB normal.
- **UV / период:** seamless TILE, тайл ≈ 1.4 м.
- **Размер:** 1024×1024.
- **Назначение:** молотая фактура и окалина, мягкая, без микронного «песка».
- **Референс:** LIS2-PAINT.
- **Проверка:** close P18/P17 (инструмент, печка): metallic 0.78 держит подсветку,
  фактура не «шумит».

### MAT-M05 — `metal_wear_v1_mask.png`
- **Consumer:** `FamilyResponses["metal"]` (`WearRoughness` 0.30, `WearMetallic` 0.05,
  `WearTint` `6b4a35`, blend 0.80), `["zinc_sheet"]` (0.26 / 0.10 / `6b4a35` 0.85),
  `["iron"]`, `["roof_metal"]`.
- **Контекст:** повреждённое покрытие: скол краски до металла, рыжий streak под фальцем,
  след контакта у опоры.
- **Формат:** PNG R-канал mask, 8-bit.
- **UV / период:** seamless TILE, тайл ≈ 1.4 м; 3–5 малых событий, мягкие края.
- **Размер:** 1024×1024.
- **Назначение:** где mask = 1 — roughness +0.26…0.30, metallic уходит к 0.05…0.10
  (окисел диэлектрик), тон `6b4a35`. Доля mask > 128 ≤ ~5 %.
- **Референс:** LIS2-PAINT; запрет «тотальная ржавчина».
- **Проверка:** P01/P08/P09 кровли и трубы; на 10 м событие не превращается в узор.

### MAT-M06 — `enamel_chip_v1_mask.png`
- **Consumer:** `FamilyResponses["enamel"]` (`WearRoughness` 0.22, `WearMetallic` 0,
  `WearTint` `4c4a44`, blend 0.90, `TilesPerMetre` 1.4 → тайл ≈ 0.71 м).
- **Контекст:** эмалированная посуда и бакены: сколы до тёмного металла по кромке и у ручки.
- **Формат:** PNG R-канал mask, 8-bit.
- **UV / период:** seamless TILE, тайл 0.71 м (предметная величина), 1–3 малых скола.
- **Размер:** 512×512 (предмет вблизи, 4K не нужен).
- **Назначение:** скол — темнее, шероховатее, **не** металл по metallic (эмаль держит
  metallic 0; подслоём занимается тон, а не metallic trick).
- **Референс:** LIS2-PAINT; паспорт M03 реестра.
- **Проверка:** P17 чайник/миска close: 1–2 скола по кромке, остальное чистое.

### MAT-M07 — `metal_galvanized_v1_roughness.png` (`game/assets/textures/response/`)
- **Consumer:** `PainterlyMaterialLibrary.FamilyResponses["zinc_sheet"]`, слот
  `RoughnessMap`, `RoughnessDelta` 0.18, `TilesPerMetre` 0.55 → тайл ≈ 1.8 м.
- **Контекст:** painterly-версия оцинковки (крыша бани, короба, уличные детали) там,
  где предмет идёт через `ForColor`, а не через `RuralPropMaterials`.
- **Формат:** PNG R-канал (G/B равны R), 8-bit.
- **UV / период:** seamless TILE, мировая проекция, тайл ≈ 1.8 м.
- **Размер:** 1024×1024.
- **Назначение:** патина и следы сгиба листа задают возраст через roughness
  (0.42…0.78 вокруг авторского 0.52), а не через metallic и не через specular-трюк.
- **Референс:** LIS2-PAINT; карточка VIS-095 «Roughness задаёт возраст и мороз».
- **Проверка:** gate `--contract` (связка пути и эффекта), затем P01/P21 close+mid:
  лист различается по блику, chrome-look отсутствует.

---

## MAT-R · Кровельный лист

### MAT-R01 — `roof_sheet_v1_normal.png`
- **Consumer:** `FamilyResponses["roof"]` (`NormalScale` 0.30) и `["roof_metal"]` (0.26),
  `TilesPerMetre` 0.5 → тайл 2 м.
- **Контекст:** шифер/профлист/фальцевая кровля: кровельные family — единственная
  большая плоскость, которую игрок видит сверху (C8, P01/P08).
- **Формат:** PNG RGB normal.
- **UV / период:** seamless TILE, тайл 2 м; волна/гофра строго периодична **внутри**
  тайла так, чтобы стык был нечитаем.
- **Размер:** 1024×1024.
- **Назначение:** геометрическая волна листа и зернистость асбеста/стали; **без** мха, без
  запечённых теней в стыках. Причина отдельного запроса, а не подключения
  `roof_slate_v1_albedo.png`: реестр 22.09 (B09) прямо отвергает этот файл как
  material-only («целые волнистые листы/стыки/мох/тени»).
- **Референс:** P1; VIS-091 (карниз/фальц как constructive language).
- **Проверка:** P01/P08 silhouette и close; snow cap (шейдерный band 0.62–0.86) ложится
  по гребням, а не полосами по мировой Y.

---

## MAT-S · Снег (VIS-034/081)

Новых albedo-карт **не требуется**: `urman_s01_v01`, `urman_s02_v02`, `snow_trampled_v1`,
`snow_roof_v1`, `snow_grass_peek_v1`, `ice_patch_v1` и пара `snow_micro_response` /
`snow_micro_normal` уже подключены. Период микрополя теперь объявлен явно (0.4–1.1 тайла
на метр по состояниям), амплитуда осталась ±0.002 м. Единственная заявка — правка tone,
не новый слой:

### MAT-S01 — `snow_trampled_v2_albedo.png` (`edit` строки S03 реестра 22.09)
- **Consumer:** `PainterlyMaterialLibrary.WinterTextures["snow_trampled"]` (замена файла
  в той же записи, масштаб 1.6 → пересмотреть при приёмке).
- **Контекст:** протоптанные дорожки и колея: различить рыхлое и утоптанное **без**
  шумного фильтра (VIS-034).
- **Формат:** PNG RGB, без alpha.
- **UV / период:** seamless TILE, мировая проекция, тайл ≈ 0.63 м при scale 1.6.
- **Размер:** 1024×1024.
- **Назначение:** тихие уплотнённые пятна и лёгкая синеватая плотность; запрет: целые
  отпечатки ног, серый «вырез», тёмная дыра.
- **Референс:** W2 (протоптанная дорога читается физически), W3 антипример плоского снега.
- **Проверка:** seam-гейт (mean ≤ 0.12, max ≤ 0.40, saturation gates) + P03 close /
  P06 along road / P07 distance sweep: след различим на 0.5/3/10 м и остаётся снегом.

---

## MAT-X · Два отсутствующих файла, на которые код уже ссылается

Не новые идеи, а дыры, зафиксированные `material_consumers_RU.md` §3 и реестром 22.09
(F08/F09): ссылки есть, файлов нет, семейство рисуется плоско и молча.

### MAT-X01 — `rowan_berries_v1_albedo.png` (`res://assets/textures/painterly/`)
- **Consumer:** `WinterTextures["rowan_berries"]`; получатели `AgentBKitMaterials.cs:50`,
  `AgentBAct1ExteriorLayer.cs:1719` (рябина в деревне).
- **Контекст:** мелкие зимние ягоды на кроне; сейчас — плоский пигмент.
- **Формат:** PNG RGB; alpha не нужна (кроны делают `ForCutout` со своей alpha-картой).
- **UV / период:** тайл 1 м (scale 1.0), мировая проекция.
- **Размер:** 512×512.
- **Назначение:** приглушённый пигмент кистей; без бликов, без снега, без «грозди-паттерна».
- **Референс:** реестр F09.
- **Проверка:** seam-гейт + P05/P26: ягоды читаются, не мерцают в движении.

### MAT-X02 — `wattle_weave_v1_albedo.png` (`res://assets/textures/painterly/`)
- **Consumer:** `WinterTextures["wattle"]` (`AddVisualWattleFence`, прутья).
- **Контекст:** плетень. Получателей у семейства по карте §5.3 нет, поэтому заказ
  **условный**: файл нужен только вместе с решением по форме плетня.
- **Формат:** PNG RGB.
- **UV / период:** тайл ≈ 1.1 м (scale 0.9).
- **Размер:** 512×512.
- **Назначение:** тонкое продольное волокно прута; нарисованная решётка запрещена.
- **Референс:** реестр F08.
- **Проверка:** seam-гейт; P02/P11 только после того, как плетень появится в сцене.

---

## Как заказ закрывается

Генерация — GPT-Image автора, интеграция — в существующие UV/материалы без новых
material-состояний. Порядок полезности (по доказанному эффекту): **MAT-M03 → MAT-C01 →
MAT-W01 → MAT-M05 → MAT-C03 → MAT-W03 → MAT-R01 → MAT-M01 → MAT-M02 → MAT-M04 →
MAT-C02 → MAT-W02 → MAT-M06 → MAT-S01 → MAT-X01 → MAT-X02**.

После поставки файла достаточно положить его по указанному пути: conditional binding
в `ApplyFamilyResponse` / `RuralPropMaterials.Surface` подхватит его без правки кода.
Проверка связности: `eng/verify-painterly-textures.sh --contract` (статическая) и
`eng/verify-painterly-textures.sh <файл>` (seam/saturation/clipping) — затем парный
кадр до/после, потому что ни один из гейтов не доказывает художественную готовность.


---

# ЛАНА: TREES

# TREES — заявки ассетов: кора, хвоя, ветви, меши деревьев

**Дата:** 2026-10-08. **Дорожка:** пакет визуального reset, деревья/лес.
**Карточки-владельцы:** VIS-025, VIS-026, VIS-075, VIS-082, VIS-083, VIS-084,
VIS-085. **Паспорта:** AS-05 (зимняя лиственная семья), AS-06 (редкие
тревожные ветви), AS-07 (избирательное хвойное семейство).
**Режим:** это вход для последующей генерации автором, а не разрешение
генерировать что-либо автоматически.

## 0. Правила, из которых составлены заявки

1. **Сначала reuse audit.** По `docs/production/visual_reset_maps_2026-10-07/material_consumers_RU.md`
   §5.4 на диске уже лежат и **не подключены**: `bark_birch_v1_albedo.png`,
   `bark_birch_v2_albedo.png`, `leaf_birch_v1_albedo.png`,
   `leaf_birch_v2_albedo.png`, `pine_foliage_albedo.png`,
   `pine_foliage_v3_albedo.png`, `bark_pine_v1_albedo.png` (имя файла занято
   семейством `wood_bark`). Новый PNG для той же функции заказывать нельзя,
   пока кандидат не сверен с этим списком и не отвергнут с указанием причины.
2. **Занятые идентификаторы:** `urman_f01_v03_basecolor.png` = `bark_birch_winter`,
   `urman_f02_v02_basecolor.png` = `bark_pine`,
   `pine_foliage_v2_albedo.png` = `foliage`, `bark_pine_v1_albedo.png` =
   `wood_bark`. Версии v7/v8 без доказанного blocker не вводятся (стиль-рецепт §8).
3. **Ветка не «одна на всех»:** у каждого видимого семейства должна быть
   подходящая фактура, но библиотека переиспользуется вариантами и масками,
   а не сотнями уникальных карт (политика L10/L28).
4. **Цветовой контракт леса:** база blue/green/dirty teal, постоянный красный
   запрещён, cozy-лес отклонён, лес-организм запрещён (H2, H3-1, H4-2, H4-3).
5. **Каждый новый файл** получает source/provenance → license → consumer →
   размер/метрический тайл → UV → слоты материала → LOD/shadow/collision →
   hash → runtime-кадр проверки → художественная приёмка автора
   (`ASSET_PASSPORTS_RU.md`, «Обязательный gate каждого нового файла»).

## 1. Что важно знать про UV до заказа

Мечи зимних деревьев (`agent_b_foliage.py`, `mesh_from_pydata`/
`_append_polyline_tube`) **UV-слоя не имеют**: `assets/source/blender/agent_b_act1/agent_b_common.py:178-183`.
Поэтому кора сегодня читается через world-space трипланар
(`PainterlyMaterialLibrary.cs:167-175`, режим `upright_texture` для
`bark_birch_winter`/`bark_pine`, `:858-860`), а метрический тайл задаётся
`texture_scale`: у `bark_pine` это 1.0×1.0 м, у `bark_birch_winter`
0.75×1.5 м (`:419`, `:477`). Все TILE-карты ниже проектируются под эту
проекцию, V-ось = вертикаль ствола. Заявка TRE-M05 — единственный вариант
перевести hero-ствола на настоящие UV.

Каталог `res://assets/textures/response/` **отсутствует в репозитории**, хотя
`SurfaceResponses` уже ссылается на `wood_grain_v1_normal.png` и
`civic_plaster_v1_normal.png` (`:747-778`). Кора сейчас имеет только
процедурный relief (`wood_bark` Relief .70 / `bark_pine` .72,
`bark_birch_winter` .55) и не имеет normal/roughness карт.

---

## 2. Текстуры коры

### TRE-B01 — Кора липы (старая, глубокая, холодная)

| Поле | Значение |
|---|---|
| Consumer | `AgentBKitMaterials.Map["AB_bark_linden"]` (`game/scripts/experiments/agent_b_act1/AgentBKitMaterials.cs`), runtime-слот `wood_bark` → новая поверхность `bark_linden` требует правки `PainterlyMaterialLibrary.SurfaceTextures` + `SurfaceResponses` (HANDOFF, карточка VIS-082) |
| Контекст | дворы и улица деревни, `village`/`zirat` регистр; кадры C1_arrival_street, C2_hero_house, P01/P06; hero-дистанция 2–6 м |
| Формат | TILE, бесшовная RGB base-color, без baked lighting; опционально пара normal/roughness (TRE-B07) |
| UV | world-space трипланар, `upright_texture` (V = мировая вертикаль); метрический тайл **0.8 м × 1.6 м** |
| Размер | 1024×1024, 8-bit sRGB, ~1–2 МБ, без alpha |
| Назначение | читаемая порода: узкие продольные рёбра и глубокие широкие борозды старой липы, мелкие поперечные трещины; крупные живописные пятна (P1), средний рисунок различим на 3 м, не «шум на 10 м» |
| Референс | H2-1 (лесная база), P1 (живописная материальность); антипример: нейтральный asset-pack realism |
| Проверка | кадр C1/P01 в 3 м от ствола: липа отличается от клёна/вязы по рисунку, а не только по тону; тест на 10 м: отсутствие мерцания; A/B с `wood_bark` |
| Палитра | холодный серо-бурый `55534a` как среднее; допуск синеватой тени; **красного нет** |
| Reuse audit | кандидатов с функцией «липа» в реестре нет; `log_wall_v1`/`weathered_wood_boards_v3` — пиломатериал, не кора → отвергнуты |
| Статус | `REQUESTED`, не генерировалось |

### TRE-B02 — Кора ивы (узкая, волокнистая, тёмная)

| Поле | Значение |
|---|---|
| Consumer | `AB_bark_willow` → `wood_bark`;placement: `AgentBFoliagePlan` (ива у мокрой канавы `(-5.6,-48)` и на зиратском плече `(-3.6,-84)`) |
| Контекст | сырые обочины, низина, канал; C5_forest_edge, P03 edge |
| Формат | TILE RGB |
| UV | трипланар `upright_texture`, метрический тайл **0.55 м × 1.8 м** (частые вертикальные рёбра) |
| Размер | 1024×1024 sRGB |
| Назначение | Узкие длинные свитые волокна, рваные вертикальные ленты, тёмные влажные участки у основания; различима от липы на силуэте |
| Референс | H2-1, P1 |
| Проверка | кадр P03/P06: ива не читается копией липы; смена направления взгляда не «плывёт» (нет домино-эффекта) |
| Палитра | `484a46`, холодный; никаких рыжих тонов |
| Reuse audit | нет аналога в `material_consumers_RU.md` §5.4 |
| Статус | `REQUESTED` |

### TRE-B03 — Кора рябины (гладкая, с чечевичками)

| Поле | Значение |
|---|---|
| Consumer | `AB_bark_rowan` → `wood_bark`; размещается только в деревне/зирате (`WinterRowan_1` у дома, `WinterRowan_2` во дворе), не в лесном регистре |
| Контекст | огороды, калитки, двор ФАП; C2/C3/C4 |
| Формат | TILE RGB |
| UV | трипланар, тайл **0.45 м × 0.9 м** (мелкий ствол) |
| Размер | 512×512 sRGB (близкий масштаб, но небольшой ствол) |
| Назначение | гладкая серебристо-серая кора с редкими тёмными чечевичками и слабым горизонтальным рисунком; отличимая «молодая» фактура рядом со старой липой |
| Референс | P1, I-CURRENT (обжитость двора) |
| Проверка | C3_fence_close рядом с забором: рябина не выглядит окрашенной доской |
| Палитра | `6b6257` |
| Reuse audit | нет аналога; `bark_birch_v1/v2` светлее и другой рисунок → не заменяет |
| Статус | `REQUESTED` |

### TRE-B04 — Кора старого дерева (AS-06, hero-старообрядец)

| Поле | Значение |
|---|---|
| Consumer | `AB_bark_old` (новый слот `winter_old_branch_variant`), runtime — HANDOFF-3 (`RegionalFoliageMaterial`, вариант содержит `OldBranch`) |
| Контекст | редкие длинные тревожные ветви, лесной регистр; P21/P23, C5_forest_edge |
| Формат | TILE RGB + нормаль (TRE-B07) |
| UV | трипланар `upright_texture`, тайл **1.2 м × 2.4 м** (толстый ствол, чтобы рисунок не мелькал) |
| Размер | 1024×1024 sRGB; близкие hero-ствола допускают 2048 только после отказа 1024 на кадре 2 м |
| Назначение | Блокистая кора с глубокой бороздой и широкими «полками», локальные гнилые пятна и следы сломанных сучьев; должна читаться материальной в первых слоях леса, тогда как дальность съедается туманом |
| Референс | H3-1 (только ветви/сучья), H4-2 (почти монохромная материальность), H2-1 |
| Проверка | P21/P23 before-after + turntable в Blender; тест отключения: без карты дерево снова выглядит пластиковым |
| Палитра | `3d3a36`; тёплые аномалии редкие и только локальные; **красный baseline запрещён** |
| Reuse audit | `bark_pine_v1` — хвойная, отвергнута; `weathered_wood_boards_v5/v6` — пиломатериал |
| Статус | `REQUESTED` |

### TRE-B05 — Берёзовая зимняя кора (проверка reuse, не новый файл)

| Поле | Значение |
|---|---|
| Consumer | `bark_birch_winter` → `urman_f01_v03_basecolor.png` (подключено, `:477`) |
| Контекст | улица, двор, кромка; C1/C5/P01 |
| Формат/UV | TILE RGB, тайл 0.75 × 1.5 м |
| Размер | существующий файл |
| Назначение | **Проверить, а не генерировать:** достаточно ли v03 для трёх разных берёзовых силуэтов (VIS-083) и hero-дистанции 2 м |
| Референс | P1, H2-1 |
| Проверка | кадр C1 в 2 м: берёза читается как живая, с корой, а не как белая труба |
| Статус | `REUSE_PENDING_AUDIT` — новый PNG запрещён до провала этой проверки |

### TRE-B06 — Хвойная кора ели/сосны (проверка reuse)

| Поле | Значение |
|---|---|
| Consumer | `AB_bark`/`AB_bark_dark`/`AB_bark_pine` → `bark_pine` (`urman_f02_v02_basecolor.png`); `wood_bark` (`bark_pine_v1_albedo.png`) для прототипа `PineA_Trunk` |
| Контекст | западный стан и пояс леса, hero-ель VIS-025; P16/P16b |
| Формат/UV/размер | существующие файлы; тайл 1.0 × 1.0 м |
| Назначение | проверка, что одна карта не делает все хвойные одинаковыми на 3 м |
| Референс | H2-1 |
| Проверка | P16 forest close: ствол ближайшей ели отличается от ствола второй |
| Статус | `REUSE_PENDING_AUDIT`; новый файл — только с доказанным blocker |

### TRE-B07 — Пары normal/roughness для коры (одна на семью, маски вместо копий)

| Поле | Значение |
|---|---|
| Consumer | `PainterlyMaterialLibrary.SurfaceResponses` (`:747-793`) — поля `NormalMap`/`RoughnessMap` уже поддержаны шейдером (`detail_normal_map` `:120`, `detail_roughness_map` `:123`, binding `:1906-1915`); сейчас ни одна кора их не привязывает |
| Контекст | все стволы в 0–8 м: деревня, зират, кромка, лес |
| Формат | **не** sRGB: `*_normal.png` (OpenGL, linear, 8-bit), `*_roughness.png` (linear, один канал в R) |
| UV | тот же трипланар и тот же метрический тайл, что у парной base-color (обязательно, иначе рельеф разъедется) |
| Размер | 1024×1024; normal — без сжатых артефактов, без baked lighting |
| Назначение | одна универсальная «bark_relief_v1_normal.png» + «bark_relief_v1_roughness.png», переиспользованная липой/ивой/старым деревом через `relief_freq` и `detail_scale` (семейства различаются albedo и масштабом, а не сотней карт); хвоя — вторая пара с более рваной частотой |
| Референс | P1 (поверхность читается пятнами, но физически убедительно), LIS2-PAINT |
| Проверка | A/B на кадре C1/P16 с `detail_normal_scale` = 0 против значения карты; relief не должен сглаживать силуэт на 15 м; папка `res://assets/textures/response/` создаётся вместе с файлами |
| Статус | `REQUESTED` (новое содержимое отсутствующей папки) |

## 3. Хвоя, листва, ягоды

### TRE-N01 — Масса хвои для hero-ели (карточки с alpha)

| Поле | Значение |
|---|---|
| Consumer | `AB_needles` внутри `urman_winter_pine.glb` (шейдерный cutout: `AgentBAct1ExteriorLayer.cs:99-104`, `PainterlyMaterialLibrary.ForCutout`, единственный потребитель cutout — §6 карты `material_consumers_RU.md`) |
| Контекст | P16/P16b, C5_forest_edge; западный стан 13–21 м |
| Формат | RGBA, alpha-clip порог 0.2 (уже в шейдере), без premultiply |
| UV | атлас существующих needle-карт источника `Leaf_Pine_C.png` сохраняется; новая карта — тот же макет |
| Размер | 1024×1024; 24-bit + alpha |
| Назначение | replacement-фактура для пересаженных масс VIS-025: отдельные лапы с неровным краем, dirty teal, снежная верхняя сторона только лёгким тонированием; запрет на «одинаковый плоский веер» |
| Референс | H2-1, H4-2, H4-3 |
| Проверка | P16: с 3 м лапа не сливается в пластину; оценка overdraw до/после (карточка прямо запрещает добавлять прозрачные карты без оценки overdraw) |
| Reuse audit | на диске `pine_foliage_v2_albedo.png` (подключён как `foliage`), `pine_foliage_albedo.png`, `pine_foliage_v3_albedo.png` (не подключены) → сначала сверить v3 |
| Статус | `REUSE_PENDING_AUDIT`, затем `REQUESTED` |

### TRE-N02 — Сухая зимняя листва/ветки (ива, черёмуха, кустарник)

| Поле | Значение |
|---|---|
| Consumer | `AB_foliage_birch`/`leaf_birch` (летние), в Act I лиственные голые → карта нужна для **серединных** побегов и засыпанных кустов; владельцы `AgentBKitMaterials`, `RegionalFoliageMaterial` kind `foliage` |
| Контекст | обочины, канавы, пояс подлеска; P03/P06 |
| Формат | RGBA cutout, small-scale spray sheet |
| UV | спрайт-лист с явной alpha; масштаб задаётсяconsumer-мешем, не трипланаром |
| Размер | 512×512 |
| Назначение | редкие сухие кисти/серёжки на нижнем ярусе; не «зелёная масса» |
| Референс | H2-1 |
| Проверка | M018/R022 переход дороги к нетронутому снегу: группы не образуют мерцающую сетку на 10 м |
| Reuse audit | `leaf_birch_v1_albedo.png`, `leaf_birch_v2_albedo.png` лежат неподключёнными → использовать их, а не новую генерацию |
| Статус | `REUSE_PENDING_AUDIT` |

### TRE-F01 — Ягоды рябины (отсутствующий файл, реальная зависимость)

| Поле | Значение |
|---|---|
| Consumer | `PainterlyMaterialLibrary.WinterTextures["rowan_berries"]` (`:478`) ссылается на `rowan_berries_v1_albedo.png`, **которого нет**; runtime kind `berries` → `RegionalFoliageMaterial` `:1722`; слот `AB_foliage_rowan` |
| Контекст | дворы и зиратское плечо (`WinterRowan_1/2/3`); рябину в deep-Kara регистр не сажать |
| Формат | TILE/UV-sprite RGB по геометрии кистей (мелкие ягоды), без alpha |
| UV | кисти генерируются `_append_berry` (малые октаэдры) → метрический тайл **0.06 м**, либо authored UV при TRE-M05 |
| Размер | 512×512 sRGB |
| Назначение | приглушённая рябина: тёпло-ржавый акцент **малой площадью** как разрешённая редкая аномалия (H2-2), а не horror-красный |
| Референс | H2-2 (дозированный янтарно-оранжевый внутри холодного), антипример — «красный horror shorthand» |
| Проверка | C1/C3: кисть читается как одна-две на дерево; в лесном регистре ягод быть не должно; тон ниже насыщенности `784239` не поднимать без отдельного решения автора |
| Статус | `REQUESTED` (блокатор только этой заявки, не независимой работы) |

## 4. Меши и геометрия

### TRE-M01 — Семейство `WinterOldBranch_1..5` (AS-06)

| Поле | Значение |
|---|---|
| Consumer | `agentb_foliage_kit.glb` → `AgentBAct1ExteriorLayer.FoliageMesh/Geometry` через `VariantKey` (**HANDOFF-1 обязателен**), план `AgentBFoliagePlan.HeroEntries` (4 корней), флаг `HeroFamilyGeometryAvailable` |
| Контекст | лесной регистр: `(-18.5,-112) (19,-121) (-52,-30) (-47,12)`; ни один экземпляр в уютном ядре |
| Формат | GLB в существующем наборе, near/light/far на один habit-сид |
| UV | без UV (трипланар), base z = 0, высота 11.2–16.8 м, pivot `_Root` |
| Размер бюджета | near 1100–6000, light 360–1500, far 70–400 тр-ков (own corridor в `_assert_winter_variant`), collision — только stem по существующему правилу |
| Назначение | длинные тревожные сучья H3-1 с физически правдоподобным основанием; без корней-рук, лиц, гигантского масштаба |
| Референс | H3-1, H2-1, H4-2 |
| Проверка | Blender turntable + P21/P23 before-after; ≤10 % ближних лесных hero-посадок; ритм нарушен одним силуэтом, лес не стал «фэнтезийным организмом» |
| Команда | `AGENTB_OUT=. blender --background --python assets/source/blender/agent_b_act1/agent_b_foliage.py` (не выполнялась) |
| Статус | `GEOMETRY_AUTHORED_IN_SCRIPT`, экспорт не выполнен |

### TRE-M02 — Третьи силуэты лиственных семей (AS-05)

| Поле | Значение |
|---|---|
| Consumer | `WinterBirch_3`, `WinterLinden_3`, `WinterRowan_3`, `WinterWillow_3` + light/far; плановые замены `AgentBFoliagePlan.HeroSubstitutions` |
| Контекст | улица/двор/кромка: деревня должна читаться авылом, а не парком двух копий |
| Формат | GLB, тот же 12 m grid и `_Root` контракт |
| UV | без UV; метрический масштаб задаётся планом (высота в таблице спецификации) |
| Размер бюджета | существующий corridor near 2000–6000, light 600–1500, far 100–400 |
| Назначение | 2–3 различимых силуэта на породу, ветвление 1/2/редко 3 порядок, без радиального шаблона, развилка co-dominant, корневое вздутие |
| Референс | H2-1, H3-1 (умеренно), P1 |
| Проверка | tree lineup 320 px: деревья читаются как разные организмы; distance sweep P01/P21; LOD-переход без исчезновения кроны (VIS-027) |
| Статус | `GEOMETRY_AUTHORED_IN_SCRIPT` |

### TRE-M03 — Hero-ель `urman_winter_pine.glb` v2 (AS-07)

| Поле | Значение |
|---|---|
| Consumer | `AgentBAct1ExteriorLayer.FoliageMesh` (`:79-116`), план `WinterPine` в западном стану `(-62..-40)`; внешние имена `WinterPine_LOD0/1/2`, слоты `AB_bark`/`AB_needles` сохранены |
| Контекст | P16/P16b, R025 |
| Формат | GLB + Blender source, base z = 0, height = 1 |
| UV | сохраняется UV источника `Leaf_Pine_C` (462 тр-ка, сигнатура UV не меняется) |
| Размер бюджета | bark-only decimation 0.5/0.2 как в v1; census пишется в `urman_winter_pine_v2_manifest.json` |
| Назначение | 3–5 нерегулярных масс хвои с просветами вместо повторяющихся плоских ярусов |
| Референс | H2-1 |
| Проверка | R025 + P16 close: с 2–3 м одинаковых дисков не видно; с 15 м ель не шум; цена (triangles/draw calls/overdraw) зафиксирована в `ledger_TREES.md` |
| Команда | `blender --background --python assets/source/blender/urman_winter_pine_v2.py -- --root . --seed 20261008` |
| Провенанс | CC0 Quaternius `Pine_3.gltf`, SHA-гейты сохранены; `eng/verify-asset-registry.sh` (VIS-107) для любых новых файлов |
| Статус | `GEOMETRY_AUTHORED_IN_SCRIPT`, экспорт не выполнен |

### TRE-M04 — Прототип `PineA` в `urman_modular_kit.glb` (HANDOFF-4)

| Поле | Значение |
|---|---|
| Consumer | `GeneratedModularKitDressing.cs:251-252` (`PineA_Trunk`/`PineA_Crown`), `Act1ConnectedWorld.AddAuthoredPine` → `KaraEdgePineA_West/East/Far` (`:9288-9290`), `StyleBenchmarkZone.AttachPresentationPine` |
| Контекст | **это и есть близкая «ель» R025** на подходе к лесу |
| Формат | GLB-набор модулярного кита, имена `PineA_Trunk_LOD0`/`PineA_Crown_LOD0` и budget 1200 тр-ков сохраняются |
| UV | box/triplanar как сейчас |
| Назначение | 4 равных конуса → 3–5 нерегулярных масс (неровный radius/depth/z, смещение центра, зазоры, разный yaw, один сломанный верхний ярус) |
| Референс | H2-1 |
| Проверка | P16 close и R025; после правки — альтернатива: routed на производные `WinterSpruce_*`/`WinterOldBranch_*` вместо прототипа (HANDOFF-5) |
| Статус | `HANDOFF` (владелец файла вне дорожки) |

### TRE-M05 — UV-проход для hero-стволов (опционально, разблокирует нормальные карты)

| Поле | Значение |
|---|---|
| Consumer | `agent_b_foliage.py` (`_winter_habit_records`, `mesh_from_pydata`), runtime — режим `authored_uv_texture` (`PainterlyMaterialLibrary.cs:864`, `response_uv` `:213-219`) |
| Контекст | 6–12 ближайших стволов (двор, кромка, P21/P23) |
| Формат | UV-слой `TEXCOORD_0` на LOD0, без overlap, корневое вздутие и ствол — единый развёрнутый цилиндр |
| UV | метрическая развёртка 0.8 м по окружности, 1.6 м по высоте (совпадает с TRE-B01/B04) |
| Размер | LOD1/LOD2 остаются на трипланаре; запрет удваивать UV-набор |
| Назначение | перевести ближайшую кору с процедурного relief на настоящую normal/roughness пару без «одинакового шума на 10 м» |
| Референс | P1, LIS2-PAINT |
| Проверка | кадр C1/P16 2 м: relief следует волокну, а не мировым осям; LOD-переход не меняет рисунок ствола |
| Статус | `PROPOSED` — выполняется только после принятия TRE-B01…B07 |

## 5. Порядок, который разблокирует съёмку

1. HANDOFF-1 → экспорт кита (TRE-M01, TRE-M02) → `HeroFamilyGeometryAvailable = true`.
2. TRE-M03 (pine v2) и HANDOFF-4 (PineA) — любой порядок, оба нужны для R025/P16.
3. TRE-B01/B02/B03/B04, TRE-B07, TRE-F01 → подключаются после reuse-аудита TRE-B05/B06/N01/N02.
4. Станционные пары C5_forest_edge (VIS-026), P16/P16b (VIS-025), P21/P23 (VIS-075),
   tree/foliage lineup (VIS-082/083), top-down census (VIS-084) — результат в
   `docs/production/visual_restyle_2026-10-07/ledger_TREES.md`.
5. Художественная приёмка автора отдельным решением; особенно hero-силуэт ели
   (VIS-025 требует явного утверждения) и доля тревожных ветвей (VIS-075).


---

# ЛАНА: CHAR

# Заявки на текстуры персонажей — CHAR (visual reset 2026-10-07)

Владелец: VIS-044, VIS-045, VIS-102, VIS-103 (лицо, воротник/плечи, мягкий материальный
стиль каста). Схема паспорта: **consumer / контекст / формат / UV / размер / назначение /
референс / проверка** (`AGENTS.md` ТЗ05, политика L10/L28).

## Главный вывод волны

**Новых текстур кожи не заказано, и заказывать нельзя.** Прямое требование карточек:
выразительность лица дают **форма и свет**, а не текстурная деталь. VIS-044 дословно
запрещает «компенсировать форму дорогой normal map», VIS-103 — «no beauty-scan detail»,
Style Recipe §8 запрещает skin-scan uncanny. Реализация: `hero_head_form()` правит череп,
брови, скулы, челюсть, нос, уши и посадку глаз в источнике
(`tools/blender/generate_character_kit_v2.py`), а `SoftSkinResponse()` в
`game/scripts/GeneratedCharacterKitDressing.cs` убирает из ответа света микроний скана
(потолок силы normal-карты скина `0.35`, authored `skin_normal = 0.40` в источнике,
roughness 1, блик выключен). Действующие CC0-albedo (`T_Superhero_*_Light`,
`old_lightskinned_female_diffuse`) остаются носителями тона: замены им нет.

Также не заказаны: альбедо-варианты пальто на каждого жителя (политика — переиспользование
библиотеки и вариантов, а не сотни уникальных повторов), «2K кожа для hero», поры,
любой bake света в albedo, и любая карта, которую код не читает (правило §2 из `MAT.md`).

Единственный доказанный пробел материальности каста — **разный отклик тканей**. Весь human-v2
гардероб (сукно, вязаное, войлок, мех, кожа) приходит в одно painterly-семейство `cloth`,
потому что `ApplyHumanMaterials` теряет авторское имя семейства из имени материала
`<hex>__<surface>` и всегда просит `"cloth"`. Валенки, вязаная шапка, овчинный воротник и
пальто имеют поэтому буквально один relief (`{ Relief = .22, ReliefFreq = 16×16 }`) и один
шум. Слоты в `PainterlyMaterialLibrary.FamilyResponses` для этого **уже есть** (`NormalMap`,
`NormalScale`, `RoughnessMap`, `RoughnessDelta`, `TilesPerMetre` — как у `wood_facade`);
не хватает объявленных семейств, и это H-3 в
`docs/production/visual_restyle_2026-10-07/HANDOFF_CHAR.md`. Ниже заказаны только
response-слои (normal/roughness), без новой альбедо.

---

## CHAR-C · Отклик тканей костюма (VIS-102; вступает после H-3)

### CHAR-C01 — `garment_knit_v1_normal.png`, `garment_felt_v1_normal.png` (`game/assets/textures/response/`)
- **Consumer:** `PainterlyMaterialLibrary.FamilyResponses["cloth_knit"]` и `["cloth_felt"]`,
  слот `NormalMap` (`NormalScale` .30 / .22, `TilesPerMetre` 2.0 / 1.4). Носители:
  `Naila_*`/`Alsu_*`/`PhoneGuy_*` (knit), `Resident_*_Boot*`/`Mansur_*_Boot*` (felt).
  Режим выборки — UV-режим A (`ForMovingCloth` → `ForDeformingSurface`, H-2).
- **Контекст:** зимний каст в 1–2 м (P05b, R007) и в общем плане улицы (C1). Вязаное должно
  ловить низкое солнце петлями, войлок — матовой массой, а не тем же шумом, что сукно.
- **Формат:** PNG RGB 8-bit, OpenGL-кодирование (X+ вправо, Y+ вверх), без alpha,
  mean-RGB ≈ `(128,128,255)`, амплитуда слабая: это painterly, не фотоскан.
- **UV / период:** seamless TILE по **метрическим авторским UV** одежды
  (`cloth_uv_units`: 1 UV-единица = 1 м покоя), период задаёт `TilesPerMetre` явно —
  1 тайл петли ≈ 0.5 м, 1 тайл войлока ≈ 0.71 м. Не мировая проекция: кость двигает ткань,
  и узор обязан держаться за неё (`material_anchor_contract_RU.md` §3.2, §5.1–5.2).
- **Размер:** 1024×1024 (blanket 4K запрещён гейтом).
- **Назначение:** распределение петель/войлочной массы как материальный отклик. Без
  запечённого света, без грязи-пятен, без кожеподобного микрошума, без смены тона
  (альбедо остаётся `old_fabric_v3_albedo.png`).
- **Референс:** LIS2-PAINT (мягкая материальность, low-poly не выставлен наружу),
  P1 (живописная поверхность без контура).
- **Проверка:** `eng/verify-painterly-textures.sh --contract` (файл отсутствует — семейство
  остаётся на процедурном relief, чёрного слота не появляется), A/B на материальной сфере,
  затем пара кадров P05b и C1 на одном camera/FOV/resolution/профиле: Naila (вязаное),
  Resident (войлочные валенки), Mansur (овчина) различимы как разные материалы **в
  grayscale-вырезке**. Без human art acceptance карточка не закрывается.

### CHAR-C02 — `fur_pile_v1_normal.png` (та же папка)
- **Consumer:** `FamilyResponses["fur_animal"]` — `Mansur_Hat_LOD0`, `Rinat_Hat_LOD0`,
  `Resident_Hat_LOD0` (ushanka), `TimurHazrat_Hat_LOD0` (karakul), будущие `*_Collar_LOD0`.
- **Контекст:** hero-крупный план: мех сейчас имеет ровно тот же отклик, что пальто, поэтому
  ushanka читается как пластиковая шапка.
- **Формат:** PNG RGB 8-bit, OpenGL normal, амплитуда слабая.
- **UV / период:** seamless TILE по метрическим UV (`cloth_uv_units`),
  `TilesPerMetre = 4.0` → 1 тайл 0.25 м; направление pile задаётся анизотропной парой
  частот (`ReliefFreq = (4, 34)`), как волокно у `wood_facade`, а не изотропным шумом.
- **Размер:** 1024×1024.
- **Назначение:** полоса света по краю ворса на кромке шапки и воротника; цвет не менять,
  «красивость» в альбедо не добавлять.
- **Референс:** LIS2-PAINT, P1.
- **Проверка:** P05b (плечо/воротник Mansur) before/after + grayscale-вырезка + тот же
  объект с 10 м (период не должен читаться как частый шум); гейт `--contract`.

### CHAR-C03 — `garment_knit_v1_roughness.png` (условная, только если C01 не закрыл вопрос)
- **Consumer:** `FamilyResponses["cloth_knit"]`, слот `RoughnessMap` (`RoughnessDelta` .06).
- **Контекст:** подтаявший снег на рукавах после вьюги должен уходить бликом, а не цветом:
  сегодня `wet_grade` у ткани .01, то есть ткань практически не мокреет.
- **Формат:** PNG R-канал (G=B=R), 8-bit, mean ≈ 0.55.
- **UV / период:** как C01, `TilesPerMetre = 2.0`, метрические авторские UV.
- **Размер:** 1024×1024.
- **Назначение:** распределение возраста: петли снаружи суше, изнанка и подгибы плотнее.
- **Референс:** P1; действующая `roughness_value` семейства `cloth` = 0.98.
- **Проверка:** A/B на материальной сфере + сумеречный кадр (VIS-071) с мокрыми рукавами.
- **Статус:** **не подключать до human-приёмки C01** — иначе это чехарда версий без
  доказанного blocker (Style Recipe §8).

---

## Чего в этой дорожке заказано не будет

| Позиция | Почему нет |
|---|---|
| Новая или заменённая альбедо кожи лица, поры, scan-карты | VIS-044 «не компенсировать форму дорогой normal map», VIS-103 «no beauty-scan detail», Style Recipe §8 (skin-scan uncanny запрещён) |
| Отдельное лицо на каждого из 60–70 жителей | политика ассетов: переиспользование библиотеки и вариантов, не сотни уникальных повторов |
| Подключение `mansur_age_v1_albedo.png` к human-v2 Mansur | файл подключён только в мёртвой процедурной ветке (Mansur живёт в human-v2, см. `ledger_CHAR.md` §5.4); ответ карточки — форма и свет, а не вторая альбедо. Файл не удаляется и не копируется в другую ветку без отдельного решения |
| Тканые узоры/орнамент на каждую одежду | сначала семейный отклик (CHAR-C01), затем — только по отдельному решению автора про татарский визуальный код, не по инициативе исполнителя |
| Normal/альбедо для hair и beard | CC0-источник уже несёт `T_Hair_*_Normal` и alpha-пряди; второй заказ создал бы «чехарду v7/v8» без blocker |
| 4K-любые карты персонажей | гейт пакета запрещает blanket 4K; hero-бюджет решения — полигоны и свет (VIS-044), а не разрешение |


---

# ЛАНА: YARD

# YARD — заявки на текстуры и меши (VIS-014, VIS-024, VIS-051, VIS-088, VIS-089)

Дата: 2026-10-08. Дорожка: визуальный reset `docs/URMAN_VISUAL_RESET_2026-10-07/`.
Числа и реализации — `docs/production/visual_restyle_2026-10-07/ledger_YARD.md`.
Схема паспорта: **consumer / контекст / формат / UV / размер / назначение / референс / проверка**
(`AGENTS.md` ТЗ05, политика L10/L28). Реестр существующих потребителей —
`docs/urman_knowledge_base/art/texture_runtime_inventory_2026-09-22.md`.

## Главный вывод волны

Из четырёх возможных новых поверхностей заказана **одна** (плетень) и **одна** подтверждена как
чужая (ковка). Всё остальное — переиспользование действующей библиотеки с авторским tint'ом и
масками, потому чтоStyle Recipe §8 и политика L10/L28 запрещают и однотонную заглушку, и сотни
уникальных повторов. Меши заборов в рантайме строятся процедурно (`Act1ConnectedWorld.YardFences.cs`),
поэтому новый GLB нужен **для приёмки и орто-съёмки**, а не как зависимость игры: игра не ждёт
этот файл и не падает без него.

---

## YARD-01 — `wattle_weave_v1_albedo.png` (ПЛЕТЕНЬ) — статус: файл отсутствует, поверхность уже объявлена

| Поле | Значение |
|---|---|
| ID / код реестра | `F08` в `texture_runtime_inventory_2026-09-22.md` (строка 84: «`wattle_weave_v1_albedo.png` в PML объявлен, но отсутствует») |
| Consumer | `PainterlyMaterialLibrary.cs:704` (`["wattle"]` → `res://assets/textures/painterly/wattle_weave_v1_albedo.png`); `Act1ConnectedWorld.YardFences.cs:95-98` (уличное семейство `wattle`, сейчас на `wood_fence`); `Act1ConnectedWorld.cs:7746-7769` `AddVisualWattleFence` (колья и прутья сейчас `wood_fence`) |
| Контекст | зимний полевой забор-плетень (тат. *чүбә*): плотные вертикальные колье 75×30 мм через 160 мм, прутья 32×50 мм на высотах 300/480/920/1100 мм, продетые то перед, то за кольем |
| Формат | RGBA, sRGB albedo, тайл без видимого шва; без нарисованной решётки и без прозрачности (переплетение даёт **форма**, не изображение) |
| UV | метровые, как у `wood_fence` (`TimberHomeStyle.AppendMetricBox` пишет UV в метрах; у прута 2.25 м должен читаться один виток волокна на ~0.5 м, а не одна карта на весь пролёт) |
| Размер | 1024×1024 (максимум `size_limit` в существующих `.import`), mipmaps on, 8-bit |
| Назначение | тонкое продольное волокно гибкого прута (ива/ольха), серебристо-серая патина, локальные потемнения в точках переплетения; колье — торцевое сечение с корой |
| Референс | LIS2-PAINT (мягкая материальность, не демонстрировать low-poly); натурный референц — плетень в зимнем свете, прутья светлее кольев за счёт иной ориентации волокна |
| Проверка | (1) source-gate: отсутствие шва и средний тон без клиппинга; (2) кадр C3_fence_close на одном прогоне плетня: различимы колье и прутья, прут не выглядит «нарисованной решёткой»; (3) тот же пролёт на 10 м — волокно не шумит uniformly (Style Recipe §5) |
| Переключение в коде | одна строка: `Surface = "wood_fence"` → `"wattle"` в `StreetDesigns` (`YardFences.cs:98`); сделано после появления файла и приёмки кадра, не раньше |

## YARD-02 — ковка: петли, защёлка, ручка калитки и ворот (поверхность `metal`) — статус: заявка **не** создана, передана владельцу VIS-095

| Поле | Значение |
|---|---|
| Consumer | `Act1ConnectedWorld.YardFences.cs:594-608` (петли/штырь/ручка ворот), `:697-712` (2 петли-полосы, штырь, latch plate, ручка калитки), `BabaiYardSideGate.cs`, `YardMechanisms.cs` (`IcedLatch`, `UpperShutterLatch`) |
| Контекст | холодное кованое железо деревенской калитки: полоса 300×60×18 мм на стойке, штырь 55×190×55 мм, ручка 50×50×180 мм на 1.05 м |
| Что сейчас | поверхность `metal` в `PainterlyMaterialLibrary` имеет рельеф (`.20`, freq 3.5), **но не имеет albedo-карты** (`:624-660` её не содержит): деталь читается как procedural-металл с авторским tint `4a4d4a` |
| Решение | заказ **не** дублируется: нормализация металла (кровли, трубы, транспорт, эмаль, кузнечные детали) — предмет карточки **VIS-095** другой дорожки; ей передаётся, что потребителями кроме транспорта становятся 4 группы дверной/калиточной фурнитуры и что нужна матовая, не хромовая, реакция (roughness ≈ .65, без зеркального блика на снегу) |
| Проверка (общая с VIS-095) | P17 approach: петля не светится как нержавейка; тень от полосы читается на доске створки |

## YARD-03 — что сознательно НЕ заказано

| Поверхность / меш | Почему не заказано |
|---|---|
| «Новая доска для каждого из 5 семейств заборов» | `wood_fence` (`weathered_wood_boards_v2_albedo.png`, relief `.45`, freq `3.2×30`) и `wood_painted_trim` (`urman_w05_v02`) уже покрывают крашеные и немалёные семейства; различие семейств в этой волне — **конструкция** (сечение, пролёт, число рельсов, плетение, наклон), а не картинка. Политика L10/L28: переиспользовать библиотеку, варианты и маски |
| Отдельная карта «свежей доски» для ремонтного пятна | ремонт читается цветом (`b9ab8e`) и накладным рельсом; отдельный albedo создал бы 6-ю версию W02 без доказанного blocker'а (Style Recipe §8 «чехарда v7/v8 variants») |
| Камень порога калитки | переиспользуется `stone_foundation` (`urman_b07_v02_basecolor.png`); порог — 300×50 мм пластина на грунте, отдельной карты не требует |
| Настил сарая (VIS-014) | доска настила уже `wood_fence`; дефект был в **посадке опор**, а не в текстуре, и лечится геометрией (`RuralPropGeometry.SeatSupportFoot`) |
| Меши «козлы», «скамья», «штабель» для калитки/двора | все три существуют: `TrestleLeg*` (4 ноги) в `YardMechanisms.cs:200-202`, `VariantA_Yard_Bench*` в `urman_village_exterior_kit.py:2014-2016`, `Woodpile_StackedLogs` в том же файле `:1569+`; новые меши = запрещённый «больше объектов» |

## YARD-04 — меш: `urman_village_fence_kit.glb` + манифест (для приёмки, не для рантайма)

| Поле | Значение |
|---|---|
| Consumer | художественная приёмка линейки заборов (VIS-088 «Fence lineup + P01/P17»), Blender-орто эталона VIS-112; в игру не импортируется: рантайм строит забор процедурно |
| Контекст | 5 семейств по одному пролёту + 4 входа (3 калитки, одни ворота) в одном файле, рядом для сравнения конструкции |
| Формат | GLB (Y-up), имена `<Family>_<Member>_LOD0`, custom properties `component_root`, `geometry_pass`, `member_size_m`, `post_lean_deg`, `clear_passage_m`; рядом `urman_village_fence_kit.manifest.json` с contract и sha256 |
| UV | метровые, per-face (в генераторе: `uv = (point.x, point.y)` и аналоги), scale 1 м |
| Размер | 5 сегментов 2.10–2.60 м, высота 1.10–1.30 м; калитка 1.24 м между стойками (проём 1.10 м), ворота 3.44 м (проём 3.27 м); доски 20–40 мм |
| Назначение | один файл = один проверяемый ряд, чтобы «соседние участки различаются конструкцией и биографией» можно было показать без запуска игры |
| Референс | LIS2-PAINT; W2 (калитка как физический вход с порогом и дорожкой) |
| Проверка | `python assets/source/blender/act1/urman_village_fence_kit.py -- --check` → `contract OK: 5 families, 4 entrances` (пройдено 08.10.2026); геометрия требует Blender-прогона, который в этом запуске **не выполнялся** → статус `GENERATED_PENDING_RUN`; `validate()` сам проверяет 20–40 мм, ногу стойки в z=0 и запрет непрерывного элемента в зоне 0.70 м |
| Запись в реестре | после прогона: `assets/asset_registry.json` (`externalAsset: false`, provenance = project-original, consumer = `game/scripts/Act1ConnectedWorld.YardFences.cs`) — **HANDOFF**, файл реестра вне права этой дорожки |

## YARD-05 — не ассет, но блокирует приёмку

* Снеговая дорожка, доходящая до проёма (VIS-089 шаг 3): у каждого калиточного/вратного узла уже
  лежит `meta pathTarget` в мировых координатах. Владелец дорожек (`SnowRelief`/`WinterRoads`/
  `MainStreetSnowBanks`) обязан заканчивать протоптанную полосу у этой точки, а не у линии забора.
  Никаких новых текстур не требует.
* Кадровые гейты: `C3_fence_close`, `P01`, `P02 gate`, `P06 street`, `P08 workgroup`, `P17 approach`
  (пары before/after на одинаковых camera/FOV/resolution/зоне) — снимает Windows-станция.


---

# ЛАНА: ATMOS

# ATMOS — запросы на кадры, карты и ассеты (VIS-022, VIS-028, VIS-040, VIS-042, VIS-043)

Дата: 2026-10-08. Автор запроса: дорожка «свет и цветововой сценарий».
Подробные числа и решения — `docs/production/visual_restyle_2026-10-07/ledger_ATMOS.md`.

Правило: ни один ассет не заказан без доказанного пробела. Текстуры, модели и
UV для этих пяти карточек **не требуются** — весь эффект делается солнцем, небом,
туманом, ответом материала и локальным светом в существующем Environment
(`URMAN_FINAL_STYLE_RECIPE_RU.md` §6). Нужны съёмка, один debug-хук и две
правки в чужих файлах.

## 1. Кадры (владелец CAPTURE / Windows-станция)

| ID | Точка (spec) | Зона / фаза | Para | Что проверяет |
|---|---|---|---|---|
| A-01 | `C1_arrival_street` | `village_day` / `village-winter-frost` | before (срез до правки) → after | VIS-040: дорога, фасад и кромка различимы в 320×180, белый снег не теряет рельеф, нет клиппинга |
| A-02 | `C7_trail_junction` (соседняя улица) | `village_day` / `village-winter-frost` | before → after | VIS-040: на соседнем виде нет проваленной чёрной стены |
| A-03 | `C1_arrival_street` | фаза `village-winter-frost`, кандидат A (ambient 0.62, sun 1.12) | probe | VIS-040 шаг 1: однофакторная проба ambient |
| A-04 | `C1_arrival_street` | фаза `village-winter-frost`, кандидат B (sun 0.82, ambient 0.92) | probe | VIS-040 шаг 1: однофакторная проба sun |
| A-05 | `C8_sky_silhouette` | `village_day` | before → after | VIS-040: небо и кроны, отсутствие вечного заката |
| A-06 | P18 (ночная кромка, подход к лесу) | `kara_urman_night` | before → after | VIS-042: три пространственных плана, путь различим, кадр не чёрный |
| A-07 | P18b тот же путь без сюжетного события | `kara_urman_night` | pair к A-06 | VIS-042 шаг 3: тревога не является следствием ошибки рендера |
| A-08 | `C1_arrival_street` | фаза `village-green-night` | after-only (эталон VIS-114) | N1+N2: зелёная ночь с хроматическим контрастом, антипример N3/N4 |
| A-09 | `C7_trail_junction` | `village-winter-frost`, weather fair → blizzard | пара на одной точке | VIS-043: направление снега и дыма совпадает, положение/геометрия стабильны, видимость дороги |
| A-10 | `C3_fence_close` + `C7_trail_junction` | `village_day` | before → after | VIS-028: стебли имеют основание, группы (не решётка) на 10 м, переход к нетронутому снегу |
| A-11 | зират: `zirat_road` / `zirat-entry` (P15) | `zirat_road` | after | VIS-022: вход и чищеная дорожка читаются, ограда ≈1 м |
| A-12 | зират: `zirat_road` / `zirat-grave-row` (P15b) | `zirat_road` | after | VIS-022: ряд могил, отсутствие запрещённых элементов |

Условия съёмки общие: `res://scenes/act1_demo.tscn`, 1920×1080, пресет `medium`,
FOV 70, `eng/remote-check.py capture --phase <id>`, метаданные кадра по VIS-004;
в receipt обязаны оказаться `atmosphereProfile`, `selectorSource`,
`atmosphereNearPlaneLuminance`.

## 2. Карты / документы (не ассеты)

* `docs/production/visual_reset_maps_2026-10-07/light_owners_RU.md` §5.8 — устаревает:
  после этой правки селектор читает `zones` из данных, профиль без зоны явно
  помечен как phase-only. Нужно перезаписать пункт в итерации 04.
* `docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07_checkpoints.json`
  — добавить точки A-06/A-07 (P18, P18b), A-11/A-12 (зират) и пару fair/blizzard
  для A-09; мне файл не передан на правку.

## 3. Правок в чужих файлах (HANDOFF, ассеты не нужны)

1. `game/scripts/MainMenuUi.cs`: две строки в `DebugZones`
   (`("zirat_road","zirat-entry","Зират · вход с дороги")`,
   `("zirat_road","zirat-grave-row","Зират · ряд могил")`).
   Consumer: отладочный проход и съёмка A-11/A-12. Проверка: пункт меню появляется
   только при `debug-zones.enabled`, прыжок не даёт прогресса.
2. `game/scripts/PainterlyMaterialLibrary.cs` + оконные семьи: тёплый отклик окна
   на состояние через `AtmosphereProfiles.Applied` (сейчас эмиссия
   `ffb45e`×1.6 постоянная). Consumer: зелёная ночь (N2), сумерки зирата.
   Проверка: кадр A-08 против A-01, тёплое семейство читается, холодное не сдвинуто.
3. `game/scripts/Act1ConnectedWorld.ZiratFamily.cs`: чтение
   `ZiratPlotLayout.RelocatedStones` (13 авторских координат с yaw) вместо линейной
   сетки и, только после подтверждения человеком, поворот рядов на ось 105.17°
   (перпендикуляр к параметру проекта `MosqueQiblaBearingDegrees = 195.1725°`).
   Проверка: `ziratCulturalAuditAxisDeviationDegrees` → 0 ± 2°.
4. `game/scripts/Act1DemoRoot*.cs`: debug-хук на `SetOpeningBlizzard(true)` вне
   сцены первой ночи (одна точка, без сюжетного прогресса) — иначе пара A-09
   недостижима на постоянной точке.
5. `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs`: когда станция
   подтвердит кадры, `SetOpeningBlizzard` читает `weather` из профиля (один источник
   чисел), а не собственную копию.

## 4. Человеческий гейт (карточка требует явно)

* VIS-022: носитель языка / культурный консультант и автор подтверждают
  (а) ориентацию оси могил относительно параметра киблы проекта,
  (б) высоту и форму низких камней, (в) допустимость компилированных надписей
  на двух семейных камнях. До ответа статус — `HUMAN_GATE_OPEN`; автоматом
  ничего не закрывается.
* VIS-040: утверждение нового дневного профиля по двум связанным видам (A-01, A-02).
* VIS-042: степень тревоги кромки и допустимое состояние сюжетного события (A-06, A-07).
