# Текстуры переделки раскладки — 2 октября 2026

Паспорт нового ассета по поручению автора об уютной татарской деревне и полной переделке мечети.
Это продолжение существующего инвентаря (T09), не новая очередь работ.
Встроенный ImageGen вызван отдельным исполнителем; остальные текстуры дерева, стен и крыши
переиспользуются из библиотеки, без генерации уникального изображения на каждый повтор.

## T09 / Mosque prayer carpet v2

| Поле | Значение |
|---|---|
| Статус | Изображение получено, сохранено, визуально просмотрено; интеграция/нативная приёмка у владельца мечети |
| Consumer | `Act1ConnectedWorld.MosqueInterior.cs`, верхняя плоскость `MosquePrayerCarpet`; только молитвенный зал |
| Файл | `game/assets/textures/civic/mosque_prayer_carpet_v2_albedo.png` |
| Runtime URI | `res://assets/textures/civic/mosque_prayer_carpet_v2_albedo.png` |
| Формат / размер | PNG RGB, 1254 × 1254 px, 3 478 399 bytes; непрозрачный |
| SHA-256 | `1e1b5a2e269f348c791d8dfada84d7fa6e6bc10dc20384bd2536af591637e20c` |
| Назначение | Albedo/base color шерстяного тканого ковра; поверхность без теней и перспективы |
| UV | Повтор обеих осей; согласованный consumer scale 1,8 m U × 2,2 m V; две соседние молитвенные ячейки по U, один ряд по V |
| Направление | Вершины арок смотрят вверх изображения, texture +V. В runtime совместить с направлением киблы; вращать UV верхней плоскости, не растягивать весь зал одной картинкой |
| Контекст | Тёплая, ухоженная сельская татарская мечеть зимой 2026. Спокойное бирюзовое поле, бордовые полосы, слоновая кость, приглушённые золотые нити |
| Орнамент / референс | Тюльпаны, листья, цветочно-геометрическая кайма, повтор молитвенных мест. Автор просил красивый ковёр с узорами; конкретная историческая мечеть/музейный образец не копируются |
| Текст | Нет арабских, псевдоарабских, религиозных или иных надписей |
| Материал | Только albedo; metallic 0, высокая шероховатость ткани. Normal/roughness карты в этом этапе не генерировались; не выдавать albedo за полный PBR-комплект |
| Масштаб | Физический repeat согласован с владельцем мечети: 1,8 × 2,2 m, одна молитвенная ячейка 0,9 × 2,2 m. В первоначальном prompt запрошено 1,8 × 1,8 m; решение consumer имеет приоритет |
| Происхождение | Встроенный `image_gen.imagegen`, brand-new generation, без входных изображений; CLI/API fallback не использован |
| Исходный output | `/Users/kadyrahunov/.codex/generated_images/01a0fb8d-532a-7c42-bcf2-cde7dff62480/exec-555b5fa1-eaaf-4dce-8914-750bbba32b57.png` (копия сохранена в репозитории) |
| Проверка изображения | `view_image`: две арочные ячейки одного направления, тканые детали, без перспективы/текста/объектов, спокойное поле и читаемый орнамент. `sips`: реальный размер 1254², RGB PNG. Запрошенный 2048² tool не выдал; искусственный апскейл не делался |
| Открыто | У repeat требуется нативный close/motion осмотр шва и масштаба нитей; несовпадение крайних пикселей не проверено в движке. Согласование орнамента культурным консультантом и художественная приёмка автора остаются открыты |
| Не запускалось | Build/import/smoke/захват: сериализует главный исполнитель, отдельный ImageGen-исполнитель их не запускает |

### Полный использованный prompt

```text
Use case: stylized-concept
Asset type: production game PBR base-color/albedo seamless square floor textile texture, a prayer-row carpet for a welcoming rural Tatar mosque in winter 2026, not a scene illustration.
Primary request: Create a beautiful woven mosque carpet tile seen perfectly from directly overhead, flat orthographic square. Dense real textile weave with refined Tatar tulip, curling leaf and restrained floral geometric ornament. Deep calm teal field, muted burgundy panels, warm ivory and understated antique gold threads.
Composition: The tile contains exactly two side-by-side repeated individual prayer spaces in ONE row. Each prayer space is a long vertical mihrab-like pointed floral arch facing straight UP toward the top of the image (+V direction); bases toward image bottom. Thin understated horizontal straight row separator at bottom and top tile boundaries. All individual arches same direction and size. Continuous repeating pattern matching left-right and top-bottom edges, no outer whole-carpet frame, no fringe. Each repeated prayer-space roughly 0.90m wide x 1.80m long at runtime; whole tile physically 1.8m wide x 1.8m long.
Materials: photoreal woven wool detail, subtle variation in yarn only; well-maintained cherished rural textile, high clarity without noisy high-contrast flecks. Tasteful ornamental richness, field remains quiet and readable.
Lighting: neutral uniform diffuse albedo only, absolutely no baked directional highlights, shadows, lighting gradients, AO, wrinkles or folds.
Constraints: 2048x2048 square if available. Seamless repeat on both axes. Fill entire square edge-to-edge with textile, no padding or margins, no perspective, no environment, no people or objects, no Arabic or pseudo-Arabic writing, no religious inscriptions or any text, no logos, no watermark.
```

## Границы этапа

Новые фактуры дерева не произведены: доступная библиотека содержит существующие поверхности
досок/крашеного дерева для сарая, туалета, ворот и оград. Новая текстура производится
только при подтверждённом отсутствии подходящего consumer-совместимого материала.
Общие `texture_runtime_inventory_2026-09-22.md` и prompt book отдельный исполнитель
не меняет; главный исполнитель дописывает ссылку на этот паспорт вместе с runtime binding.

