# H002 — исходный пример VariantB

Это виды существующего 3D-шаблона, **не кадры текущей игры**. Генераций ImageGen/Meshy, загрузок и расходов здесь нет.

Текущий объект — `urman.world:act1/open-part/h002-house`, адрес `ADR-H002`, Кыр, 10. Его generic plot использует `urman.catalog:urman_village_exterior_kit/villageparcel-variantb-plasterannex` из `game/content/world/catalog.v1.json`. Это жилое хозяйство VariantB с оштукатуренными стенами и сенями; публичный фасад или другой дом не подставлялся. У объекта нет `hidden`, `states` или `modelPath`: текущий `ApplyStates` выбирает видимое базовое состояние.

Источник — `game/assets/models/act1/urman_village_exterior_kit.glb`, SHA256 `fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8`. Выбран только `VillageParcel_VariantB_PlasterAnnex_Dwelling` внутри `VillageParcel_VariantB_PlasterAnnex`; двор и хозпостройка не входят в эти виды. Права: `Project-original` по записи `urman.act1.village_exterior_kit` в `assets/asset_registry.json`; новая публичная лицензия не объявляется.

`AuthoredWorldDirector.BuildVisual` задаёт всему Visual масштаб 0,758 и смещение `-catalog.origin*scale`, где origin `[39.49,0,1.42]`. `BuildCollision(surfaces)` затем умножает Dwelling/Outbuilding на `0.9/0.758 = 1.187335092348285` и сдвигает выросшие части внутрь границ участка. Поэтому в студийных видах дом показан с эффективным масштабом 0,9. Удалён только исходный preview-offset `[41,0,0]`; собственные transforms и геометрия дочерних узлов сохранены.

Runtime style seed — `StableHash("urman.world:act1/open-part/h002-house") = 2733781018`: вариант 3, цвет наличников `e9e3d4`, остаток по 17 равен 2. Generic-вызов разрешает редкий второй этаж, но **для H002 условие не выполняется**.

Не воспроизведены runtime TatarCarpentry/резьба и перекраска наличников, оконные материалы и свет, дым и звук, точный parcel-edge Push, позиция `[37.75,0,118.747]` с terrain grounding, коллизия, world lighting/fog/частицы. Уход от этих отличий в исходных картинках не означает их отсутствия в игре. UV-атлас, новый материал, LOD и новая геометрия не создавались.

Рецепт — `render_h002_source.py`; сырой вывод — `render.log`; точные входы, node/material inventory, параметры камер и SHA кадров — `source-receipt.json`. Следующий шаг требует отдельного принятия root именно этого примера и current consumer.
