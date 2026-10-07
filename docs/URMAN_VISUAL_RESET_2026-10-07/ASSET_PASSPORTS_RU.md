# Паспорта ассетов visual reset

Новая генерация/скачивание не является обязательным для каждой строки: сначала reuse audit; затем produce/download only if blocker proven.

| ID | Семейство | Зачем | Формат | Путь/владелец | Потребители |
|---|---|---|---|---|---|
| AS-01 | Village hero-house construction kit | Нормализованный mid-poly деревянный дом: фундамент, толщина стен/кровли, оконные ниши, карнизы, bevel. | GLB + Blender source | `assets/source/blender/act1/ + game/assets/models/act1/` | VIS-086, VIS-087, VIS-091 |
| AS-02 | Window surround / trim kit | Наличники, подоконники, откосы, тёплые окна без плоской наклейки. | GLB modules | `assets/source/blender/act1/` | VIS-087, VIS-096 |
| AS-03 | Fence board generator kit | Набор досок и процедурный генератор вариативных деревенских заборов. | Blend/Python + GLB | `assets/source/blender/act1/` | VIS-088 |
| AS-04 | Gate and wicket kit | Правдоподобные калитки/ворота с петлями, толщиной и посадкой. | GLB modules | `assets/source/blender/act1/` | VIS-089 |
| AS-05 | Winter deciduous tree family | Берёза/липа/рябина/ива: силуэт, кора, ветвление, снег. | GLB + LOD | `assets/source/blender/` | VIS-082, VIS-083 |
| AS-06 | Rare disturbing branch variants | Редкие старые деревья с длинными тревожными сучьями по H3-1, без леса-организма. | GLB + LOD | `assets/source/blender/act1/` | VIS-075, VIS-083 |
| AS-07 | Selective conifer winter family | Хвойные только там, где уместны; не «пальмы» вдоль улицы. | GLB + LOD | `assets/source/blender/` | VIS-084 |
| AS-08 | Snow bank / drift shape kit | Геометрические формы валов, надувов, кромок и roof caps; текстура не заменяет форму. | runtime geometry helpers / GLB where needed | `game/scripts/Act1ConnectedWorld*.cs` | VIS-077, VIS-079, VIS-080 |
| AS-09 | Painterly snow material family | Albedo/normal/roughness/detail/macro masks для снега, без baked lighting. | PNG 1024–2048 + shader params | `game/assets/textures/painterly/` | VIS-081 |
| AS-10 | Painterly aged wood family | Фасад/доска/торец/крашеное дерево; локальный масштаб и физический roughness. | PNG 1024–2048 + trim where useful | `game/assets/textures/painterly/` | VIS-093 |
| AS-11 | Plaster / civic wall family | Штукатурка без asset-pack стерильности, крупные пятна и локальный износ. | PNG 1024–2048 | `game/assets/textures/` | VIS-094 |
| AS-12 | Aged painted metal family | Эмалированная сталь, кровля, трубы/желоба; корректный metallic=0/1 по материалу. | PNG 1024–2048 | `game/assets/textures/` | VIS-095 |
| AS-13 | Frosted village window family | Мороз, стекло, room color, night spill; без фейковой прозрачности. | PNG + shader params | `game/assets/textures/painterly/` | VIS-096 |
| AS-14 | Wind-line VFX family | Редкие графические линии потока воздуха по V4, world-space atmospheric motif. | mesh/particle/shader effect | `game/scripts/ + assets VFX` | VIS-076 |
| AS-15 | Forest fog state assets | Профиль тумана/sky/lighting для непроглядной лесной глубины; без второго WorldEnvironment. | JSON/profile + existing Environment owner | `game/content/world/atmosphere.v1.json` | VIS-073, VIS-074 |
| AS-16 | Character cloth/head material normalization | UV-local cloth, painterly faces, воротники/плечи, единый материал каста. | material/GLB corrections | `game/scripts/GeneratedCharacterKitDressing.cs + Blender sources` | VIS-102, VIS-103, VIS-104 |
| AS-17 | Vehicle/prop normalization kit | Нива, ВАЗ, мотоцикл и скачанные props: unified materials, LOD, UV, scale, snow contact. | GLB/material conversion | `game/assets/models + generated` | VIS-105, VIS-106, VIS-107 |
| AS-18 | Reference and capture gate assets | Контакт-листы, fixed cameras, histogram/tiling/grounding checks; не production art. | scripts + docs | `eng/ + game/tests/` | VIS-066, VIS-117, VIS-118 |

## Обязательный gate каждого нового файла

source/provenance → license → consumer → scale/dimensions → UV → material slots → LOD/shadow/collision owner → generated/downloaded origin → hash → runtime receiver frame → human art acceptance.