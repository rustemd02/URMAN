# Вывески четырёх зданий заречья — паспорт, 01.10.2026

Статус: **reuse W05 / текущие потребители установлены по коду; новый runtime ещё не принят**.
Новая генерация не требуется: просмотренная W05 уже даёт спокойную окрашенную
слегка потёртую древесину без букв и без силуэта целой доски. Четыре почти одинаковых
PNG не создаются. Это решение согласовано с владельцем интеграции. Подмена
runtime-вывесок иллюстрациями зданий не нужна.

## Источник и покрытие библиотеки

- Реестр: [W05](../../urman_knowledge_base/art/texture_runtime_inventory_2026-09-22.md).
- Источник: [urman_w05_v02_basecolor.png](../../../game/assets/textures/painterly/urman_w05_v02_basecolor.png),
  `res://assets/textures/painterly/urman_w05_v02_basecolor.png`.
- Формат: opaque RGB PNG, **1254 × 1254**, 1 542 552 байта; alpha отсутствует.
- SHA-256: `c2170588472b77aab0f560818d34349cc4397d1793f0e5d06a7cbd73ee2ac317`.
- Происхождение: существующий built-in ImageGen результат W05 от 22.09.2026;
  промпт, первая отклонённая версия и отбор — [painterly README, раздел W05](../../../game/assets/textures/painterly/README.md).
  В нынешнем этапе imagegen и CLI/API fallback не вызывались.
- Референс: собственная уже подключённая библиотека игры, живая деревня зимой 2026,
  светлая ухоженная краска с редкими тихими серыми потёртостями. Сторонней фотографии нет.
- Альтернативы каталога M03/M04 относятся к эмали и окрашенному металлу; PNG для
  них пока нет. Текущий носитель задан деревянным (`wood_painted_trim`), поэтому
  менять его на металл ради дополнительной генерации не требуется.
- [Книга промптов](../../urman_knowledge_base/art/urman_imagegen_texture_prompt_book_2026-09-22.md)
  прямо допускает M03/M04/W05 как поверхность таблички и требует хранить точные
  надписи в контенте.

## Четыре потребителя

Источник положения/названия: [act1_far_bank.world.v1.json](../../../game/content/world/act1_far_bank.world.v1.json).
Владелец геометрии: `AddFarBankPublicBuilding` в
[Act1ConnectedWorld.FarBank.cs](../../../game/scripts/Act1ConnectedWorld.FarBank.cs).
Координаты ниже — X/Z; высоту пола runtime вычисляет по четырём углам участка.

| ID данных | Узел-потребитель | Точный Label3D из данных | X/Z, yaw | Текущий цвет носителя |
|---|---|---|---|---|
| `far-police` | `FarBankPublic_police/NameBoard` | `ОПОРНЫЙ ПУНКТ ПОЛИЦИИ` | 84 / −14; −90° | `e8ecee` |
| `far-bakery` | `FarBankPublic_bakery/NameBoard` | `ПЕКАРНЯ` | 88 / −45; 180° | `efe6d4` |
| `far-dairy` | `FarBankPublic_dairy/NameBoard` | `МОЛОКОЗАВОД` | 98 / −74; 0° | `eef0ec` |
| `far-forestry` | `FarBankPublic_forestry/NameBoard` | `ЛЕСНИЧЕСТВО` | 94 / 115; 0° | `d9c9ad` |

Каждый носитель сейчас `BoxMesh`, размер **3,2 × 0,62 × 0,06 м**.
Ширина определяется `min(width − 0.6, 3.2)` и для всех четырёх зданий равна 3,2 м.
Локальная позиция `(0, 2.85, depth/2 + 0.07)`; разворот наследуется от здания.
Табличка presentation-only, своей коллизии не имеет. Снег/освещение остаются
физическими/шейдерными слоями; букв, рамок, теней, крепежа и наледи в PNG нет.

## Проекция и физический материал

Текущий материал уже назначается через
`PainterlyMaterialLibrary.ForColor(trim, "wood_painted_trim")`.
Это **метрическая world-space triplanar проекция**, `upright_texture=true`,
`texture_scale=(2,2)`, повтор примерно **0,5 × 0,5 м**. На вертикалях используются
X/Y либо Z/Y мировых координат; поворот здания учитывается весами нормали.
Это не развёрнутый уникальный UV-атлас вывески: ветка `authored_uv_texture`
для `wood_painted_trim` выключена. PNG используется как настоящая фактура
материала на меше. Если впоследствии нужен износ по краю конкретной доски,
потребуется отдельный локальный UV/маска, а не рисунок целой вывески в tile.

Наследуется `wood_facade` finish: metallic 0, базовые roughness 0,82,
specular 0,17, wet-grade 0,42; шейдер далее корректирует отклик влажностью.
Текстурная доля 0,95 до дальнейшего шейдерного grading.
Импорт: mipmaps включены, cap **1024**, `compress/mode=0`.
На Low шейдер не читает альбедо PNG, сохраняя цвет и геометрию — это существующий
контракт профиля, не утверждение, что Low показывает фактуру W05.

## Точный текст и читаемость

`NameBoardText` — отдельный `Label3D`, `Text=spec.Label.ToUpperInvariant()`.
Текст не генерируется. Текущие параметры: `FontSize=64`, `PixelSize=0.0045`,
`Width=640` (2,88 м), `AutowrapMode=WordSmart`, `OutlineSize=0`,
`DoubleSided=false`, цвет `(0.16,0.17,0.2)`.
Локальная позиция текста `(0,2.85,depth/2+0.105)`; от лицевой плоскости доски
отступ около 5 мм. Это параметры кода, а не доказательство отсутствия мерцания.

Особенно проверить длинную полицейскую надпись: перенос, высоту строк,
вписывание в 0,62 м и видимость с подъезда. Другие три подписи должны помещаться
в одну строку. Визуальный гейт также проверяет фронт текста при всех yaw,
контраст оттенков при снежном дневном свете, затенении и Low/High,
чтение с пешеходной дороги и из Нивы, повтор W05 на длинной доске и движение камеры.
Если выявится дефект, сначала исправлять размер/позицию текста либо локальный
цвет носителя. Новая ImageGen-карта оправдана только доказанным дефектом самой
фактуры, который нельзя устранить её масштабом/проекцией.

## Проверки и пределы доказательства

- 01.10.2026: исходный PNG визуально просмотрен — поверхность светлая, непрерывная,
  без букв, объекта/рамки, крупного повреждения и запечённого освещения.
- 01.10.2026: размеры, alpha, объём, SHA проверены локально; совпадают с библиотечным
  паспортом. Карта не изменена.
- Исторический source-gate W05: PASS1/1, mean seams 0,0120/0,0182,
  max 0,1176/0,0784, без clipping. Это прежний срез источника, не новый прогон.
- Назначение четырём `NameBoard` подтверждено по текущему коду. Комментарий о
  «painted placeholder until ImageGen» устарел: фактура уже ImageGen, отдельно
  требуется проверка runtime-вывески и её геометрии.
- Новый build/import/runtime/capture не запускался этим исполнителем;
  сериализован у владельца интеграции. Приёмка всей семьи вывесок остаётся открытой
  до кадров и просмотра. Культурная/языковая корректность новых переводов не
  заявляется: используются только уже существующие русские labels.

## Промпт для происхождения выбранной W05

Первый промпт и полный provenance находятся в painterly README; финальное
редактирование уже выбранного источника, **не новая заявка на генерацию**:

```text
Use case: precise-object-edit. Edit the provided W05 painted wood base-color texture for URMAN. Preserve its warm ivory palette, opaque square canvas, neutral even lighting, and softly painted finish. Change only the material continuity: remove ALL board boundaries, seams, full-width horizontal lines, gaps, edge wear bands, knots and visible exposed wood grain. Make this a single uninterrupted painted surface with almost intact maintained ivory paint. Retain only sparse, very subtle short multidirectional brush strokes and faint soft grey rubbed pigment, no dominant grain direction. Four-edge seamless tile for a 0.5 by 0.5 metre painted trim surface. Broad calm painterly pigment, not photographic or noisy. No boards, panels, cracks, dark flecks, carving, objects, text, perspective, shadows, highlights, border or collage. Output only the flat RGB base-color texture.
```
