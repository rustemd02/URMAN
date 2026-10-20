# H002 — исходный пример VariantB

Это виды существующего 3D-шаблона, **не кадры текущей игры**. Генераций ImageGen/Meshy, загрузок и расходов здесь нет.

Текущий объект — `urman.world:act1/open-part/h002-house`, адрес `ADR-H002`, Кыр, 10. Его generic plot использует `urman.catalog:urman_village_exterior_kit/villageparcel-variantb-plasterannex` из `game/content/world/catalog.v1.json`. Это жилое хозяйство VariantB с оштукатуренными стенами и сенями; публичный фасад или другой дом не подставлялся. У объекта нет `hidden`, `states` или `modelPath`: текущий `ApplyStates` выбирает видимое базовое состояние.

Источник — `game/assets/models/act1/urman_village_exterior_kit.glb`, SHA256 `fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8`. Выбран только `VillageParcel_VariantB_PlasterAnnex_Dwelling` внутри `VillageParcel_VariantB_PlasterAnnex`; двор и хозпостройка не входят в эти виды. Права: `Project-original` по записи `urman.act1.village_exterior_kit` в `assets/asset_registry.json`; новая публичная лицензия не объявляется.

`AuthoredWorldDirector.BuildVisual` задаёт всему Visual масштаб 0,758 и смещение `-catalog.origin*scale`, где origin `[39.49,0,1.42]`. `BuildCollision(surfaces)` затем умножает Dwelling/Outbuilding на `0.9/0.758 = 1.187335092348285` и сдвигает выросшие части внутрь границ участка. Поэтому в студийных видах дом показан с эффективным масштабом 0,9. Удалён только исходный preview-offset `[41,0,0]`; собственные transforms и геометрия дочерних узлов сохранены.

Runtime style seed — `StableHash("urman.world:act1/open-part/h002-house") = 2733781018`: вариант 3, цвет наличников `e9e3d4`, остаток по 17 равен 2. Generic-вызов разрешает редкий второй этаж, но **для H002 условие не выполняется**.

Не воспроизведены runtime TatarCarpentry/резьба и перекраска наличников, оконные материалы и свет, дым и звук, точный parcel-edge Push, позиция `[37.75,0,118.747]` с terrain grounding, коллизия, world lighting/fog/частицы. Уход от этих отличий в исходных картинках не означает их отсутствия в игре. UV-атлас, новый материал, LOD и новая геометрия не создавались.

Рецепт — `render_h002_source.py`; сырой вывод — `render.log`; точные входы, node/material inventory, параметры камер и SHA кадров — `source-receipt.json`. Следующий шаг требует отдельного принятия root именно этого примера и current consumer.

## Один ImageGen-референс — 20.10.2026

Root отдельно принял шесть source views и текущую идентичность H002 перед этим шагом. Выполнен ровно один вызов встроенного `image_gen.imagegen`; CLI/API-ключ, Meshy, загрузки в Meshy, покупки и новые 3D/UV/runtime-изменения не использовались.

Входы были просмотрены через `view_image` до генерации. Роли и SHA256:

- Архитектура/пропорции/проёмы: `source-template-front.png`, SHA256 `08a580cb8578ae461927c9e7c5cc504e8c57340944609898775e81ee7218dbe2`.
- Архитектура правой стороны и сеней того же дома: `source-template-right.png`, SHA256 `a19e3ed1595583a923b2737808c8fc8a4c38d1849a51ac97e1efb142ac3cfc6c`.
- Только материальность/цвет/стилизация: `evidence/ACT1-DEPTH.12/native_uv_v3_fdc5f5ef62fc4554ad107b9d9bef31ef/frames/babai_shape_front_high.png`, SHA256 `146035efec8364cf654d773259d17a3dd538acb4cba3549e1a85204a50239adb`. Сруб, персонаж, окружение и погода этого кадра не задают архитектуру H002.

Сохранённый выбранный результат: `imagegen-reference-v1.png`, 1448×1086, 2 043 430 байт, SHA256 `a53bdb1407061568b786d76cef2017fc9971b49feff51a2ade7820e3c3b7d341`.
Исходный output инструмента: `/Users/unterlantas/.codex/generated_images/01a15661-354d-76f1-8270-5d14a959da37/exec-01760896-5171-4efd-a050-2669badd6c28.png`.
Идентификатор выданного инструментом артефакта: `exec-01760896-5171-4efd-a050-2669badd6c28`; отдельного task ID API инструмент не вернул. Оригинал в generated_images сохранён; в репозитории — точная копия.

Результат лично просмотрен: три передних окна, правая пристройка с входом, боковые окна, штукатурка и деревянный фронтон читаются; снег, случайные брёвна, новые этажи и чёрные прямоугольные торцы наличников не появились. Это концепт с собственной интерпретацией стыка крыши сеней и числа ступеней, а не точная проекция source geometry. Он требует отдельной оценки root по стилю и архитектуре перед любым Meshy-входом. Художественная/3D/native-приёмка этим не заявляется. Исходный `source-receipt.json` сохранён без изменений (SHA256 `3b934b716eae73dbd8d4b2d9e600aa955a4d64595b86889ed837173f911540de`).

Снег и направленные погодные/световые эффекты исключены намеренно: это отдельные слои runtime, которые нельзя запекать в будущую базовую материальность/атлас и которыми нельзя закрывать проёмы.

Точный prompt встроенного вызова:

```text
Use case: stylized-concept.
Asset type: one architecture and material reference image for the URMAN game, future 3D asset input; not a finished model, texture atlas or gameplay screenshot.
Input roles: Image 1 (source-template-front) and Image 2 (source-template-right) are authoritative for the SAME existing H002 VariantB house's architecture, proportions, roof and openings. Image 3 (accepted URMAN gameplay frame) is ONLY a material/color/stylization reference: match its restrained painterly 3D game material family and grounded village carpentry, NOT its log-house geometry, scene, people or weather.
Primary request: render that existing H002 house as a complete freestanding building, front-right three-quarter view, centered on a plain neutral warm-grey background and small neutral ground plane. All edges and base visible with comfortable margin; front and right side must be readable together. No landscape, other houses, props, people or labels.
Architectural invariants from Images 1 and 2: modest single-storey rural Tatar dwelling, compact plaster main volume, three tall evenly spaced front windows beneath a vertical-board wooden gable; same main roof pitch, broad eaves, low plinth and believable roof-to-wall bearing. Preserve the smaller enclosed seni annex on the RIGHT with its own low sloping roof, front-facing wooden entry door and two shallow practical steps; preserve the main right-side window and smaller annex side window from Image 2. Do not invent another entrance, storey, balcony, tower, chimney forest or extra windows. Main roof gabled, supported, roof covering and trim physically plausible. Keep recognizable footprint and main-to-annex proportions.
Materials/style: restrained chromatic painterly 3D game asset, believable substantial construction and broad readable planes matching Image 3's game house. Quiet aged warm grey-ochre lime plaster walls with modest surface variation; matte weathered brown timber gable and door, lightly worn pale muted cream painted wooden window casings inspired by the accepted game-frame trim; subdued dark grey roof covering. Correct simple joinery, clean real mitres/ends, deep readable window reveals, continuous support; eliminate the source template's artificial black rectangular endcap patches. The window glass is dark muted desaturated blue-grey, not glowing. No hyperreal photographic microdetail, glossy plastic or clay toy appearance; no fake excessive damage, smeared mud or ornament overload. This remains a PLASTER VariantB house, never convert it into the log VariantA/hero.
Lighting and weather: soft neutral daylight with a gentle contact shadow, no dramatic grading. Show the roof, steps and ground WITHOUT snow or frost. Snow, icicles, weather streak overlays and strong scene-light shadows must not become a material/atlas design: in the game weather is a separate runtime layer, and the future material reference must expose the underlying building surfaces and every opening.
Constraints: one coherent whole-house image, no collage, no cutaway, no text/watermark, no decorations unsupported by the source architecture, no fantasy. Preserve the ordinary lived-in rural identity and the accepted game's material family.
```


Root осмотрел результат 20.10: принимает его только как вход материальности H002. Стык крыши сеней и ступени отличаются от исходной 3D-конструкции, поэтому source-template и её проёмы/опоры остаются геометрическим контрактом; эти изменения концепта не переносить в модель/коллизию. Следующий Meshy-вызов ещё не начат; родительская игровая и художественная приёмка открыта.
