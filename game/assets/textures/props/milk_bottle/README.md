# Кара-Урманское подворье — стеклянная бутылка молока

Самостоятельный кандидат-проп, созданный 2026-09-28 по прямому запросу автора.
Это 1-литровая многоразовая стеклянная бутылка с молочной заливкой, красной
крышкой и 360° бумажной этикеткой. Вымышленное название — «Кара-Урманское
подворье»; промышленный комбинат и реальные товарные знаки не подразумеваются.
В готовой лицевой стороне название хозяйства тёмное, а слово «МОЛОКО» — крупное
и насыщенно-красное.

## Паспорт текстуры

- **Потребитель:** `PaperWrapLabel` в `../../../models/props/milk_bottle/karaurman_milk_bottle_v1.glb`;
  сцена игры пока не назначена.
- **Контекст:** отдельный бытовой продуктовый проп для деревенского мира Акта I;
  размещение и игровой смысл не утверждены.
- **Формат и размер:** PNG RGBA, 2172 × 724 px. Исходник —
  `../../../../../assets/source/illustrations/karaurman_milk_bottle_label_v1.png`.
- **UV:** непрерывная развёртка кольцевой этикетки; U 0–1 обходит всю окружность,
  V 0–1 идёт от нижнего к верхнему краю. Центр лицевой стороны расположен около
  U=0.5. Альфа сохраняет вырубленное тюльпанное окно и прозрачность вокруг бумаги.
- **Назначение:** марка, продуктовая надпись и видимый через стекло продукт;
  глянцевое стекло, белая заливка и красная крышка являются отдельной геометрией
  и материалами модели.
- **Визуальный ориентир:** красная типографика и тюльпан как окно к продукту
  переосмыслены по мотивам ВАМИН; слово, знак и готовая упаковка ВАМИН не
  копировались. Описание редизайна: [Бизнес Online](https://www.business-gazeta.ru/article/516717).
- **Проверка:** исходная этикетка и рендер Blender просмотрены; прозрачное окно
  видно на бутылке. GLB и `.blend` созданы Blender 4.5.12. Импорт в Godot,
  размещение в сцене и проверка татарского носителем не выполнялись.

## Файлы

- Исходное изображение: `assets/source/illustrations/karaurman_milk_bottle_label_v1.png`
- Текстура-потребитель: `karaurman_milk_bottle_label_v1.png`
- Редактируемая сцена Blender и экспорт GLB: `../../../models/props/milk_bottle/`
- Сценарий сборки: `../../../../../tools/asset_generation/build_karaurman_milk_bottle.py`
- Превью: `../../../models/props/milk_bottle/karaurman_milk_bottle_preview_v1.png`

## Промпт ImageGen

```text
Use case: product-mockup
Asset type: one UV-ready 360-degree wraparound paper label for a real glass milk bottle game prop
Primary request: Create an original label for the fictional small village producer “Кара-Урманское подворье”. Take only broad inspiration from the clean red-led Tatar dairy packaging style associated with VAMIN: bold red name, light field, simple tulip motif that works like a window to the product. Do not copy VAMIN’s logo, lettering, exact tulip mark, or any existing product packaging.
Scene/backdrop: no scene; a single flat printed label with transparent pixels outside the paper rectangle.
Subject: A horizontal wraparound label for a reusable 1-liter clear glass milk bottle; this is the print artwork only, not the bottle.
Style/medium: restrained, modern local-farm packaging, simple confident Russian typography, clean and practical, compatible with a painterly low-poly 3D game prop.
Composition/framing: wide horizontal rectangle, straight-on orthographic, one continuous 360-degree wrap label. Front composition centered: brand name above a large product name and an original tulip-shaped clear transparent cutout/window below; the transparent tulip window must be a true alpha hole through the opaque paper label. One side region has a small tulip emblem. The rear region has neatly arranged, readable short copy and a blank date line. Keep all text safely inside the label. Print artwork fills the entire rectangular label edge-to-edge; no perspective or mockup.
Lighting/mood: flat evenly lit print design, no shadows, reflections, folds, bottle highlights or condensation.
Color palette: warm ivory/off-white paper label, strong muted vermilion red as the sole dominant ink, near-black text, one very small leaf-green accent. Keep the design dramatically simpler than the previous cream/red/blue folk carton.
Materials/textures: matte uncoated paper with only very subtle even fiber grain; crisp ink edges and a clean alpha cutout.
Text (verbatim): Front: “КАРА-УРМАНСКОЕ ПОДВОРЬЕ”, “МОЛОКО”, “СӨТ”, “3,2%”, “1 л”. Back: “Молоко питьевое пастеризованное”, “Хранить в холоде”. A single blank date line. Do not add other legible words.
Constraints: no real company name or mark; this must read as a small local household farm, not an industrial combine. Leave the tulip window transparent so glass and white milk are visible through it. One unified label image, no collage or separate pieces.
Avoid: carton, box, bottle mockup, photography, folk border overload, navy blue, dense text, invented address/contact/ingredients, barcode, QR, certification marks, watermark, misspelled Cyrillic, fake letters.
```

## Источник изображения и сборка

Исходник: OpenAI ImageGen built-in, 2026-09-28; генерировался без входных
изображений. Полоска этикетки целиком сохранена и применена как RGBA-текстура.
`tools/asset_generation/build_karaurman_milk_bottle.py` создаёт форму вращения
бутылки, внутреннюю молочную заливку, крышку, кольцевую UV-геометрию этикетки,
редактируемый `.blend`, GLB и превью. Файлы остаются отдельным кандидатом и не
подключены к игровой сцене.
