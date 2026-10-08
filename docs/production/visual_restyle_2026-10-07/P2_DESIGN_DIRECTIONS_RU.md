# 20 сложных дизайн-направлений к цели P2 (08.10.2026)

**Цель автора:** сочная красивая картинка уровня референса P2
(`docs/URMAN_VISUAL_RESET_2026-10-07/references/approved/P2_author_gallery_quality_target.webp`),
а не топорная, как сейчас. Из P2 берётся уровень исполнения и свет, не музейный сеттинг.

Статус везде **CODE_DONE_VERIFY_PENDING**: код собран (`dotnet build` 0/0), кадры со станции ещё не сняты.
Шейдеры проверены только запуском на Mac (Metal): найдено и исправлено три ошибки компиляции чужих и моих шейдеров.

| # | Направление | Что сделано | Файлы |
|---|---|---|---|
| 1 | Цель автора | P2 сохранён в пакете, прописан в рецепте стиля, манифесте референсов, журнале решений | `URMAN_FINAL_STYLE_RECIPE_RU.md`, `REFERENCES_RU.md`, `reference_manifest.json`, `decision_log.md` |
| 2 | Непрямой свет | SSIL на medium/high; SDFGI на high (интерьер: малые каскады без света неба, bounce ≤ 0.45; улица: 4 каскада); плоский ambient снижается под GI; GI в половинном разрешении | `GraphicsQuality.ConfigureEnvironment`, `project.godot` |
| 3 | Мягкие тени солнца | угловой размер солнца 0.9° (medium) / 1.6° (high), на low жёсткие | `GraphicsQuality.ConfigureSun` |
| 4 | Свет в зданиях внешней зоны | при входе в клуб/школу/мечеть/участок/магазин/баню окружение переключается: туман ×0.15, ambient ×0.55, экспозиция +0.1 | `Act1ConnectedWorld.cs`, `Act1ConnectedWorld.InteriorPresentation.cs` |
| 5 | Отражения комнат | по одному статическому ReflectionProbe в главной комнате, клубе, школе, мечети (контракт как у клиники) | `InteriorReflectionProbes.cs` |
| 6 | Скошенные кромки | интерьерные коробки получают фаску 3–15 мм (44 треугольника), кроме тонких, UV-привязанных и стекла | `RuralPropGeometry.InteriorBox/ChamferBox` |
| 7 | Пол из досок | шейдер раскладывает плиту на доски 0.19×2.6 м со швами, смещением стыков и тоном каждой доски | `PainterlyMaterialLibrary.ForPlankedFloor` |
| 8 | Плинтус и карниз | главная комната и флигель: плинтус 0.10 м и двухступенчатый карниз | `StyleBenchmarkInteriorFactory(.Rooms).cs` |
| 9 | Окна изнутри | наличники, накладная голова с карнизиком, фартук под подоконником | `StyleBenchmarkInteriorFactory.BuildWindow` |
| 10 | Портал сцены ДК | базы, капители, ступенчатый архитрав, золочёный багет, нос сцены | `Act1ConnectedWorld.SquareInteriors.Club.cs` |
| 11 | Стены ФАП | двухтонная покраска: масляная панель до 1.5 м, побелка выше, тёмная линия кисти | `PainterlyMaterialLibrary.ForDadoWall`, `StyleBenchmarkZone.cs` |
| 12 | Лица | вместо toon: Burley-диффуз, скин-рассеяние, слабый широкий блик и ободок | `GeneratedCharacterKitDressing.SoftSkinResponse` |
| 13 | Мягкие тени ламп | размер источника у настольных, потолочных и мечетных ламп; тени от ламп зала только на high | `GraphicsQuality.SoftShadowLampGroup` |
| 14 | Потолок и чердак | потолок комнаты, перекрытие флигеля и чердачный пол читаются как настил | `StyleBenchmarkInteriorFactory(.Rooms).cs` |
| 15 | Ткань | блеск ткани на скользящем свету: пальто, обивка, шторы, ковры | `PainterlyMaterialLibrary` (`cloth_sheen`) |
| 16 | Залы ДК и школы | плинтус и двухступенчатый карниз по периметру | `Act1ConnectedWorld.SquareInteriors*.cs` |
| 17 | Свечение окон | на high: мягкое свечение только от ярких источников (порог HDR 1.15) | `GraphicsQuality.ConfigureEnvironment` |
| 18 | Отражения на полу | SSR на high только в закрытых комнатах | `GraphicsQuality.ConfigureEnvironment` |
| 19 | Чёткость под углом | анизотропная фильтрация текстур 8× | `project.godot` |
| 20 | Починка шейдеров и контрактов | `response_uv` не видел `UV`; `blend_mix` в шейдере окна; `hint_white`; пустой пул ветра; мета с дефисом; контракты заборов | `PainterlyMaterialLibrary`, `VillageWindowMaterials`, `AgentBWindStreaks`, `Act1ConnectedWorld`, `YardFences` |

## Что остаётся вне кода

- Кадры «до/после» на станции, чтобы подобрать силу SSIL/SDFGI, фаски и свечения по глазам.
- Стоимость на целевом ПК (GTX 970 на станции): SDFGI/SSR/свечение включены только на пресете high; medium получает SSIL, мягкие тени солнца и всё геометрическое.
- Геометрия, которой нет в коде (резные наличники, лепнина, фигурный паркет-ёлочка как модель), — работа в Blender и текстурные заявки (`VISUAL_RESET_GENERATION_REQUESTS_2026-10-08.md`).
