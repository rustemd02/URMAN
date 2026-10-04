# Assets

2026-10-04, живой звук деревни: лицензированный азан и банк `sound_mood`.
Азан — полевая запись «Adhan in Istanbul» (Viceskeeni2, Wikimedia Commons, CC0 1.0),
подготовлена у якоря минарета через `Act1ConnectedWorld.TryPlayAdhan`; расписание намазов
остаётся открытым хуком. One-shot банк: печка (Freesound D.jones 525253), приглушённый ТВ
(milcahrawr 846688, без защищённого вещания), смех/гомон (craigsmith 482798), комнатный гомон
(Breviceps 457043), дальний лай (Sadiquecat 737196) и зимняя серая ворона (Walking.With.Microphones
556219) — все CC0, публичные HQ previews.
Восемь записей второй партии (топор, пила, калитка, кипящая еда, корова, детвора, шаги по снегу,
гармонь-аккордеон) — Freesound CC0, проверены на фоновые шумы; гармонь документирована как
аккордеон, не татарская тальянка. Непрерывные слои `village_life_layer.wav` (112 с) и `village_dread_layer.wav` (120 с) сведены
`tools/audio/prepare_village_beds.py` из существующих записей проекта. Хеши, смещения, лицензии:
`game/assets/audio/act1/sound_mood/credits.json`; границы и проверки —
[отчёт](../production/sound_mood_2026-10-04.md). Человеческое прослушивание открыто.

2026-09-28, standalone «Чәк-чәк» package candidate: a fictional Kara-Urman bakery
carton with ImageGen Tatar floral front label and a real alpha-cut display
window. The current visual experiment places a full-window block of compressed,
overly wet honey-glazed chak-chak behind transparent film; an earlier appetizing
cutout is retained for comparison. Neither image is printed on the box.
120 × 150 × 72 mm, 3072 × 2048 RGBA atlas. Not yet placed or imported in-game;
language review and runtime/art acceptance remain open. [Source, prompts,
references, and builder](../../game/assets/textures/props/chakchak/README.md).

2026-09-28, standalone «Кара-Урманское подворье» milk-bottle candidate: a 1 L
clear-glass bottle with milk fill, red cap, and a 360° ImageGen label whose
transparent tulip window reveals the bottle contents. Blender source and GLB are
saved separately; the prop has no runtime scene placement. Engine import and
native-speaker language review remain open. [Texture passport, full prompt,
references, and builder](../../game/assets/textures/props/milk_bottle/README.md).

2026-09-28, date-free «Чаян» prop candidate: ImageGen created the complete front
page (masthead, scorpion mascot, Tatar copy, and winter cartoon) as one image;
the generator places it unchanged into the front UV panel of a 4096² atlas. A
custom glTF 2.0 builder creates a 155 × 215 mm soft-cover magazine with eight
hinged, double-sided page leaves and 19 animation clips. The cover has no
date, month, year, issue number, or ISSN. Atlas/model sources and references are
documented in
[the texture README](../../game/assets/textures/props/README.md).
Standalone asset only: not yet placed in a scene, imported in-engine, or given
a gameplay interaction; native-speaker review of new filler text remains open.

2026-09-22, B07: `urman_b07_v02_basecolor.png`, source PASS1/1; новый спокойный минеральный материал stone_foundation на основаниях ФАП и ступенях общественных зданий. b07-03 build/import PASS348, drift/errors=[]; High/Low действующего фундамента из стоячего сервисного подхода просмотрены, guard restored. Глобальный лесной камень и смешанный FapWetStone сохранены. Это 20 новых подключённых карт плюс T12 reuse; close/motion, другие потребители и art lock открыты. [Промпты, границы и свидетельства](../tasktracker/05_texture_production_2026-09-22.md).

W05, итог текущего среза: w05-04 build/import PASS346; street-02 снят из штатного from_house, CanStandAt прошёл, crouching=false, кадр просмотрен. Промежуточный вид 3,2 м под навесом отвергнут. Production-материал после w05-02 не менялся; ранние кадры остаются свидетельствами своих сборок. Движение, batch pilot runtime и art lock не закрыты.

2026-09-22, W05: встроенным ImageGen получена светлая краска наличников `urman_w05_v02_basecolor.png` (1254 RGB, source PASS1/1, mipmaps/cap1024). Первый вариант со стыками досок отвергнут. Существующий DressPaintedWindowSurrounds назначает новую роль wood_painted_trim только четырём деталям окна; opt-in batch согласован, другие деревянные поверхности не заменены. На w05-02 близкие High/Low-кадры просмотрены из приседа; это 19 новых подключённых карт плюс T12 reuse, не полная художественная приёмка. Первый native-запуск завершился аварией .NET BGC до готовности сцены, точный повтор прошёл; причина этой аварии не закрыта. [Промпты, свидетельства и остаток](../tasktracker/05_texture_production_2026-09-22.md).

Связанная проверка W02, 22 сентября: fence-settle-02 PASS344; standalone-access с существующим `--urman-smoke-background-input` PASS432, все три подхода и стык сарая зирата проверены. Без флага эта же DLL попадала в автопаузу; геометрия/коллизии не исправлялись. Три достигнутых вида дверей просмотрены, новую художественную приёмку это не закрывает. Точные исходные High/Low-виды W02 относятся к fence-uv-02 (production-код материалов после неё не менялся); [журнал](../tasktracker/05_texture_production_2026-09-22.md).

2026-09-22, продолжение W02: составные рейки AddVisualFenceRun получили метровые UV вдоль каждого уклона; стык с сараем зирата сохраняет UV при обрезке и разворачивает соединительный участок. Прежние границы и коллизии сохранены. Fence-uv-02 build/import PASS344, три кадра: East High/Low стоя, North High из приседа. Волокно реек горизонтальное, штакетников вертикальное; Low сохраняет цвет/форму без bitmap. Новых PNG-источников нет. Импортированные mixed-fence/плетень и человеческая приёмка открыты; [журнал](../tasktracker/05_texture_production_2026-09-22.md).

2026-09-22, W02: создана и частично подключена серебристая древесина `urman_w02_v02_basecolor.png` — стойки/штакетники AddVisualFenceRun с локальным Y и шесть реек бокового прохода с локальным Z. Один метр/повтор, mipmaps, cap1024, отдельные серые оттенки Low/High; остальные mixed-fence не переназначены. Source PASS1/1, Fence-04 build/import PASS344, три свежих кадра с явно отмеченным приседом у реек. [Промпты/ограничения/свидетельства](../tasktracker/05_texture_production_2026-09-22.md). Это 18 новых подключённых карт плюс T12 reuse, не полная приёмка W02.

2026-09-22, полная инвентаризация: [132 карточки ↔ реальные получатели](art/texture_runtime_inventory_2026-09-22.md), 31 reuse / 37 new / 5 edit / 59 conditional. ACT1-TEXTURE.00 закрыта как исследование; генерация, интеграция и художественная приёмка остальных групп открыты. Зафиксированы неподключённые крыши, отсутствующие rowan/wattle/frost PNG, общие атласы лиц, черновые носители следующих актов и необходимость отдельных UV/масок. Новых изображений/сборок этот срез не создавал.

2026-09-22, шторы: T04 v01 — отдельная матовая бирюзовая ткань cloth_curtain на 14 плотных шторах семи окон Factory; 0,5 м/повтор и метровые UV. T12 — обоснованное переиспользование существующих цветочных обоев, исправлены направление V и проекция на боковые стены. Тюль, одежда, скатерть, мебель и коллизии не менялись. Source-gate 2/2; curtains-01 build/import PASS342. Просмотрены три native-кадра: Rear High (crouching=true, близкий вид снизу), Right High/Low (standing). В High видны спокойное плетение и направленные вверх цветы, в Low сохраняются цвет/форма без bitmap. Это не приёмка движения/всех окон или человеческий art lock. [Промпт, SHA и точные границы](../tasktracker/05_texture_production_2026-09-22.md).

2026-09-22, домашняя отделка: **16/16 опорных карт подключены**, полная приёмка не закрыта. B01 v01 — только стена Factory/LeftWallPier0 у печи; T01 v02 — отдельная TeaTablecloth по реальной столешнице, с передним свесом и метровыми UV. Source-gate 2/2; home-finish-01/house PASS105, 22,46 м; home-finish-03 и четыре Low/High-кадра проверены отдельно. Исправлена отладочная постановка камеры (стойка/ввод/крен), обычное управление не менялось. High показывает фактуру, Low сохраняет форму/цвет; движение, другие ракурсы и остальные материалы по профилям открыты. [Промпты, SHA и свидетельства](../tasktracker/05_texture_production_2026-09-22.md).

2026-09-22, снег и кора: **14/16** выбранных карт первой партии. S01 v01/S02 v02/F01 v03/F02 v02 подключены по отдельным ролям; берёзовые отметины горизонтальны, зимняя сосна больше не получает материал лиственного ствола. Общий wood_bark и мебель не заменены. Source-gate 4/4, build snow-bark-02 PASS336, два guarded native-кадра просмотрены. Снег остаётся малоконтрастным из-за шейдера; сосна вблизи, повёрнутые брёвна, High/Low/движение и художественная приёмка не подтверждены. [Журнал и точные ограничения](../tasktracker/05_texture_production_2026-09-22.md).

2026-09-22, следующий материаловый срез: **10/16** выбранных карт первой партии. Новые W01/W03/W04 v02/W09 с исходниками, точными промптами и SHA находятся в painterly README/asset_registry. Четыре source-gate PASS; W09 подключена к реальному `StyleBenchmarkInteriorFactory/Floor`, а не только старому модулю досок. House-03 на wood-01 PASS105 и просмотрен; последняя сборка wood-04 и два свежих фасадных кадра отдельно отмечены в [журнале](../tasktracker/05_texture_production_2026-09-22.md). Форма отдельных досок текущего пола, High/Low/движение и художественная приёмка открыты; новые PNG не закрывают их автоматически.

2026-09-22, проверенный следующий срез: выбраны 6 карт первой партии — B03/B04/M05/T08/T10 v02/W08, с точными версиями, исходниками, SHA и сохраняемыми import-настройками. Кадры дома и ФАПа на `texture-lang-02` просмотрены; узкие boundary scopes PASS105/PASS113, человеческая художественная оценка и остальные карты открыты. [Полные ограничения и журнал проверок](../tasktracker/05_texture_production_2026-09-22.md). Поздний функциональный build04 не выдан за новую визуальную приёмку.

2026-09-22, исполнение нового поручения: генерация внесена в каноническую очередь как ACT1-TEXTURE/.00–.13. Первые B04/T10 v02/W08 сгенерированы встроенным ImageGen и адресно подключены в ФАП; source/image-gate, текущая сборка и ограничения native capture зафиксированы в [срезе исполнения](../tasktracker/05_texture_production_2026-09-22.md). Общая clinic-проверка остаётся FAIL из-за ранее открытой перекрытой точки правого окна, не из-за ошибочного импорта. Художественная приёмка и остальные карточки открыты. Ниже — историческое состояние на момент подготовки книги промптов, до начала генерации.

2026-09-22: подготовлена [книга промптов ImageGen для текстур Акта I и Актов II–V](art/urman_imagegen_texture_prompt_book_2026-09-22.md): 132 карточки, палитры и назначения по локациям, первая партия из 16 карт, ограничения UV/сюжетных изображений и памятка подключения к текущему painterly-материалу. Это предложение библиотеки по актуальному тасктрекеру, не новый art lock или очередь. Изображения не генерировались, игровые материалы не менялись; неизвестные поздние ассеты не выданы за утверждённые. В документе используется актуальное имя деревни Кара-Урман; исторические записи ниже сохранены.

2026-09-11, прямое уточнение пользователя: красивый арт-стиль уровня ориентиров
Zelda BOTW/TOTK — обязательная цель текущей реализации. Критерии: выразительные
силуэты, цельные цветовые массы, живописные материалы, свет, глубина и композиция
обычных игровых ракурсов. Зимний Кырлай и татарский культурный код сохраняются.
Техническая корректность импорта/полигонов не заменяет художественную приёмку;
добавление постэффектов само по себе этот критерий не закрывает.

2026-09-10: generated 19 built-in ImageGen painterly albedo candidates across
12 missing surface families (grass, roofs, bark, birch leaves, log walls,
wallpaper, institutional wall, ornament, carpet, household fabric and carved
wood). The delivered files are 1 024 × 1 024 RGB PNGs with exact prompts and
SHA-256 provenance in `game/assets/textures/painterly/README.md`; seam metrics,
2×2 tiles, overview and runtime before/after receipts are in
`evidence/act1_repo_baseline/textures_new_families/`. Selected candidates are
bound through the existing painterly material owner and current Act I meshes.
This is a technical/runtime candidate pass, not art lock: grass repetition,
family selection and Tatar ornament placement still require human art and
cultural review.

2026-09-07: создан [пакет из 16 связанных 2K-концептов окружения](../production/urman_concept_image_pack/README.md) для работы Blender-агента: въезд, улицы, двор и дом, старый ПК, ФАП, зират и Кара-Урман с прямыми и обратными видами. Серия следует Painterly Low-Poly, каноническому тону и культурным ограничениям; принципы BOTW/TOTK используются только как вторичный ориентир для силуэтов, цветовых масс, атмосферной глубины и wayfinding. Концепты не являются точным layout, runtime evidence или art lock.

2026-09-07: добавлен [промпт для последовательной работы небольшой модели в Blender](../production/URMAN_BLENDER_SMALL_MODEL_PROMPT_RU.md), адаптированный из Skyline Restaurant and Cocktail Bar. Он направляет работу по одному участку существующего Акта I, использует текущий execution ledger и три принятых арт-документа. Это инструкция для будущих запусков, не новый арт-контракт и не свидетельство готовности ассетов. В рамках подготовки промпта модели и игровой код не изменялись.

2026-09-07, village kit window-trim pass: every window in the shared
`author_rural_dwelling` family (hero facade + Variants A/B/C) lost its
transom bar and tapered headboard crown — probe-proven timber-lattice read
at street distance; jambs/rails/mullion/sill remain. Blend+GLB regenerated
by the same generator (709 LOD0 meshes / 20,558 tris), registry hashes and
manifest updated, walkthrough/capture green, frames in
`evidence/act1_repo_baseline/windowtrim/`.

2026-09-07, kit material grading: the wet village road kit's organic verge
family (FernShrubBreak shrubs/ferns, sedge masses, moss, kit fences) and the
village exterior kit's well water, cut wood, bark and dull metal now bind to
the existing painterly tables (`RebindWetVillageRoadMaterials` and
`RegradeAct1DaylightKitMaterials`). No GLB, source, geometry or texture
change; probe receipt and before/after frames in the execution ledger and
`evidence/act1_repo_baseline/matfix/`. Same-day follow-up bound the
zirat roadside and FAP kit leftovers in the same tables (frames in
`evidence/act1_repo_baseline/sliceb/`). Kara edge is fully covered by its
scoped table; agentb kit parts are materialled by their C# builders, not
raw GLB albedo. Same day: five street-perimeter facade clones now place
the kit's three full-volume variant dwellings (A/B/C rotation at house
scale), reducing the repeated-facade read; a subagent review caught one
silent no-op conversion and two fence/house overlaps, all fixed and
re-verified (pairs in `evidence/act1_repo_baseline/slicec/` and
`sliced/`).

2026-09-05, Kara stand composition supersedes the detached grounded-root row:
roots now emerge from eight staggered near trees, not from arbitrary road-edge
positions. Four old shell-crown kit groups and four repetitive bank logs are
suppressed. Reused native open-canopy tree/branch/shrub builders for sheltered
regrowth and the view beyond the final threshold. Ground/collision and
narrative ownership are unchanged. No new kit, texture or generated concept;
GroundPigment now transitions continuously from village meadow to muted wet
needle/litter soil at the forest threshold; same geometry, no new textures or
lighting edits. Terrain source/derived hashes refreshed in registry. Crude
boulders, richer ground-life composition and human art acceptance remain open.

2026-09-05, Kara ground: shared terrain/heightfield now owns asymmetric low
forest shoulders; central x +/-3 m route remains unchanged. Suppressed separate
bank/root-wall soil pedestals and the duplicate logical-zone benchmark kit.
The core still owns the woodland presentation and existing narrative targets
are untouched. Wide triangular roots are replaced in their existing footprints
by low bent wood segments following Ground. This is geometry integration, not
a new lighting grade, new lore, or forest art acceptance. Terrain source/derived
hashes updated in the existing registry; detailed checks in execution ledger.

2026-09-05, shared planted shrub geometry: triangle-probed the conspicuous
zirat green solid to AB_foliage_shrub, not the already-open C# shrub builder.
Replaced imported Shrub_1..3 closed lobes with existing leaf-shoot geometry
and woody stems at the actual variant radius; source names and placement plan
retained. Removed unused closed-canopy profile builders. Foliage kit now
137 meshes / 28,656 triangles / 13 materials; registry hashes updated.
New stem rotations bake before preview-grid placement; generator asserts
world-space shrub bounds after refreshing Blender transforms. Re-export is
byte-stable. Planted copies retain semantic part names so the existing bark/
leaf material selection survives duplication. No narrative or collision change;
this is a geometry/material integration candidate, not foliage art lock.
Runtime grounding correction: planted copies now apply the already-computed
terrain height and vertical scale to each source part's Y translation; prior
placement calculated target.Y but ignored it. This seats whole plant families
on their shared root rather than leaving them at y=0 or separating scaled crowns.

2026-09-05, last western holding beside zirat: the existing full-depth dwelling
now has a complete timber-picket boundary with a 3.5 m opening, full-scale
storage shed/firewood and a curved terrain-seated access to its side seni.
Removed the old short fence through the house footprint. Reused current kit
components and surface/fence builders; no new texture, gameplay or religious
content. New close entry/return views in the existing capture harness expose
the plot beyond a single roadside camera. Whole-zone art remains REWORK.

2026-09-05, zirat material integration: runtime triangle probes identified
PathDirt and Culvert_StoneShadow as the conspicuous white roadside strips.
Added these and Culvert_WeatheredStone to their existing painterly material
tables; also bound LeafLitter and the kit's ditch/road/zirat grasses to their
existing earth/foliage palettes. No new textures, geometry or lighting;
memorial markers and cultural content retained. This corrects material
integration only, not the remaining flat placement or whole-zone art quality.
Follow-up geometry: the imported ZiratPathEdge ribbon now conforms each vertex
to the shared terrain height field, keeping its existing footprint and relief;
normals regenerated. Source kit and collision unchanged. Full-zone composition
and remaining stone/shrub primitives still require production work.

2026-09-05, zirat architectural scale: removed the five forward/side/horizon
miniature dwelling/shed-and-fence assemblies (.28–.34 scale) placed as camera
backdrops. Existing full-depth lateral houses and cemetery boundaries remain.
No GLB/Blend deleted or changed; no marker, inscription or religious detail
added. This is instance/composition cleanup, not cemetery art acceptance.
Also suppressed both mounted ZiratDistantVillageMass backdrop assemblies:
their small house boxes and faceted canopy masses sit near the player in
the connected map. Existing full-depth lateral houses own the village read;
source kit, grave markers, path and cultural content are unchanged.

2026-09-05, continuous holding access: replaced the two sharp-ended path
meshes with one native Curve3D ribbon, through the entrance toward the house.
Half-metre longitudinal sampling follows the same height field as the terrain;
existing surface builder/material, no new kit or texture. Physical walkthrough
now includes entry/return through this holding rather than relying on two
presentation poses. Whole-yard art acceptance remains open.

2026-09-05, yard ground: existing landform surface builder now optionally
conforms paths to the shared terrain height field (enabled for the two
EastStreetPlot access paths only). Corrected shared top-face winding;
terrain-conformed normals regenerated. No new texture or terrain-state owner.
Close first-person capture identified .4-scale EastStreetMidShed at (23.3,-15.8)
blocking the holding entrance; removed that redundant instance, keeping the
full-size shed inside the plot and the adjacent facade. Close entry/return
poses added to the existing capture harness, now 46 frames / eight zones.
Correct winding exposed obsolete constant-height landform strips as floating
slabs; unconformed overlays are now not rendered, with shared terrain retaining
relief ownership. Ground-conformed access paths remain visible. Angular joins
and unfinished porch connection are still production rework, not art lock.

2026-09-05, holding boundary continuation: east/west fence returns now join
the front picket runs to the rear of MainStreetEastNearMidHouse's plot.
West return x=15.5 clears the shed; the FAP-side entrance remains open.
Removed the diagonal decorative bank through the dwelling/yard. Reuses the
existing terrain-following rail/post builder; no source asset changes.
The orphan Fence_HouseA2_/Gate_HouseA2_ preview family beside EastParcel
is now suppressed together with its already-hidden house, including its
derived decorative collision. Other old parcel fence families are untouched.
Shared AddVisualFenceRun now places LowerRail and arrival slats under Rail:
existing exact replacement suppressions hide the whole infill instead of
leaving isolated low beams. Posts retain their existing named contracts.

2026-09-05, FAP-branch holding: retained the full-size
MainStreetEastNearMidHouse instead of two extra miniature facade parcels;
one .95-scale shared gable shed now stands inside its yard at (18.4,-11).
Front fence runs with an entrance and a rear boundary replace overlapping
scattered props. Reuses existing source components and landform paths;
no new kit, texture, collision or narrative owner. Whole-plot visual gate open.

2026-09-05, shared shed rebuild: OutbuildingShed_Low now reuses the existing
closed gable-shed construction (wood walls, two closed gables, pitched thick
roof, door, side window) instead of stacked flat-roof boxes and add-on lean-to.
Root/anchors preserved; old children replaced, no parallel kit. Roof shell
winding fixed at the shared generator with top-facing normal assertions.
Village kit now 781 meshes / 21,643 triangles / 16 materials. Registry hashes
match regenerated Blend/GLB. Existing weathered/shadow wood material names
are bound to the connected painterly palette. Half-scale decorative FAP shed
removed; its real service shed retained. Main-street neighbor shed scale .95.
Art/placement review remains open; larger geometry must not be blindly scaled
into crowded parcels. Sources and RuntimeBridge ownership remain unchanged.

2026-09-05, instance ownership correction: retained MainStreetEastNeighborFacade
and removed overlapping EastLateral/ForwardEast parcel instances; removed
EastStreetFarHouse where the FAP neighbor already occupies the plot. Source
GLB/Blend files retained. The FAP kit's preview polyhedron grove is suppressed
in the connected scene, retaining the existing detailed FapClinicNearBirch.
Same-view Godot side/back review confirms the duplicate geometry removal.
OutbuildingShed_Low silhouette and parcel ground composition remain REWORK.

2026-09-05, connected FAP follow-up: four existing full-depth neighboring
dwellings were spaced into separate plot positions instead of the former
miniature-backdrop spacing; two fence runs follow the revised plot edges.
No new asset family. A screen-ray diagnosis identified the bright foreground
triangle as Road_FapBranch, not an unfilled model. Existing wet_road material
now uses a rough wet-earth response distinct from water-like wet_ground;
puddle materials, lighting, camera and road geometry are unchanged. Same-view
Godot capture confirms the glare removal; parcel art acceptance remains open.

2026-09-05, FAP entry: the existing clinic kit's three masonry steps now
have grounded solid risers, overlapping treads and a wider lower landing;
the door threshold rests on the deck rather than its outer stair edge.
Same 405 meshes / 12,110 triangles / 25 materials, no new assets or textures.
Connected-world material binding now includes the kit's wet stone, door,
dark timber, wayfinding/notice panels, metal and glass; lighting unchanged.
Registry hashes updated. This is a construction/material correction, not
acceptance of the FAP parcel composition or the whole Act I art direction.

Ассеты — одна из главных проблемных зон проекта. MVP нужно держать компактным и активно заменять дорогую постановку интерфейсами, документами, фото, звуком и портретами.

Top 50 production inventory: см. `asset_inventory_50.md`.

## Production Direction

Accepted presentation direction, 2026-08-10: walkable 3D with a first-person camera and sound-first tension.
Accepted art treatment: **Painterly Low-Poly 3D** — production-safe stylized geometry plus painterly materials, lighting and fog. In-engine tests validate this direction; they no longer choose an alternative. См. `design_style.md`.

- Локации собирать как компактные ходибельные 3D-зоны из модульных environment kits.
- Свободное перемещение и обзор от первого лица входят в MVP floor; бесшовный open world, бои и сложные анимации не входят.
- Top-down карта деревни не является основным экраном перемещения. Она может существовать только как неполная схема в журнале Айдара.
- Интерфейсы как реальные предметы использовать выборочно: старый ПК, бумажный документ, фото, тетрадь/журнал. Старый ПК — главный документальный хаб MVP; остальные diegetic UI не должны становиться обязательным правилом для всего проекта, иначе production cost вырастет.
- Напряжение строить через звук, паузы, реакцию NPC, частично понятные правила и изменение состояния сцены.
- Визуальный принцип: обычность сначала, неправильность потом. Базовые ассеты не должны быть гиперреалистичными или сразу «страшными».
- Старые route screens и ink-wash location frames — референсы композиции, палитры, фактуры и ориентиров, но не финальные world assets и не runtime fallback.
- Art direction reference: «Искатель в доме с привидениями» можно использовать только как вторичный reference для ручной, бумажной тревожности.

## 3D Asset Pipeline

2026-09-05: в `agent_b_foliage.py` исправлена размерность общего `_blade`:
ширина сечений травы/осоки/папоротника больше не задаётся метровыми
константами вместо рассчитанных сантиметров. Проверка ширины встроена
в экспорт. Семья Fern пересобрана как изогнутые вайи с парными листочками,
сходящимися к общему основанию на земле, вместо разрозненных подвешенных
лент. План посадки и collision owners не изменены. Экспорт: 128 meshes,
21 132 triangles; актуальные SHA находятся в `assets/asset_registry.json`.
Это исправление геометрии, не художественная приёмка всей растительности.

- Blender 4.5 LTS and versioned Python generators are the source for modular environment, foliage, furniture and prop families.
- `.blend`, generator inputs and texture sources are authored assets; `.glb` is a rebuildable Godot import artifact.
- ImageGen supplies concept targets, ornament/texture sources and moodboards. It does not prove in-engine quality or replace collision/LOD/material QA.
- Character meshes, faces, clothes and textures are original URMAN assets. Only documented CC0 skeleton/animation bases may be reused.
- Every asset record stores source, license, scale, polygon budget, texture budget, materials, collision, LOD and verification status.
- Three mandatory benchmark scenes gate mass production: daytime street, house/old PC, night Kara-Urman edge.
- UI/audio presentation assets must expose captions and non-audio descriptions through the shared `AccessibilitySettingsSnapshot`; readable text and description wording still require external and cultural review.

Current migration evidence: Blender generated `assets/source/blender/urman_modular_kit.blend` and rebuildable `game/assets/generated/urman_modular_kit.glb`; four ImageGen-derived painterly albedos remain the v1 environment mappings, while `stone` and `fabric` are explicit v2-only presentation surfaces on non-interactive benchmark anchors. Focused v3 siblings for all six materials, a v4 earth/wood pair and a v5 relief-aware earth/wood rework now pass deterministic image gates and test-only Metal/Forward+ evidence; none activate runtime owners. Independent review keeps v4 at HOLD/REWORK and v5 at production OPEN: v5 removes painted road ruts/strong stone relief and reduces wood striping risk, but earth pebble repetition, wetness response, wood orientation/shared-owner scale, near/mid/far traversal and 20–30m repetition remain open. A separate Blender `assets/source/blender/urman_character_kit.blend` and `game/assets/generated/urman_character_kit.glb` now provide nine project-original character prefixes with 313 deterministic LOD0/LOD1 meshes, layered face/hand landmarks, layered clothing, ground anchors and one animation-safe armature per prefix with `Idle`/`Tension` clips. Godot `GeneratedCharacterKitDressing` imports each matching `AnimationPlayer`, starts `Idle`, and exposes presentation-only switching to `Tension`; `FullGameNpcDressing` selects `Tension` in pact/boundary threat presentation and `Idle` elsewhere, while `FullGameFlowSmokeTest` covers both clips. The Acts 2–5 `FullGameZone` has one authored `PackedScene` wrapper per logical zone plus a separate `FullGameZoneDressing` pass with zone-specific river/dock, archive, Soviet file room, pact ledger and boundary compositions. `GeneratedModularKitDressing` loads the environment `.glb` selectively for the Act 2 house, Act 3 old-PC/table and Act 5 forest boundary, while `GeneratedCharacterKitDressing` selects the named character GLB prefixes and applies explicit Godot LOD ranges; imported `-col` render meshes stay out of physics. `PainterlyEnvironmentDetails.AddRoadRelief` now gives style, Chapter 1 Zirat and shared Acts 2–5 paths a deterministic 9×28 low-poly crown/rut surface with matching collision cells; `RoadReliefQaSmokeTest` and `CollisionQaSmokeTest` cover the candidate geometry. `CollisionQaSmokeTest` verifies queryable layer-1 floors in all 12 zones and isolates kit proxies on layer 2. Seven representative captures are stored in `art/fullgame_frames/`. The four original v1 mappings remain active; v2/v3/v4/v5 candidates remain deliberately scoped presentation-only candidates, not a global texture switch. This is still modular production evidence, not final art lock: wetness/puddle dressing, geometry/canopy/hero-prop polish, facial expression, authored voice/ambience, mesh-collision review, release-hardware performance and cultural presentation lock remain open.

Audio foundation evidence, 2026-08-11 / Act 1 continuity pass, 2026-08-14: four deterministic project-original WAV stems (`village_day`, `house_old_pc`, `kara_urman_night`, `water_edge`) are generated by `tools/audio/generate_ambient_audio.py` and registered in `game/assets/audio/ambient_manifest.json`. `AmbientAudioDirector` is a presentation-only Godot owner that maps the active logical zone to one manifest stem, uses two `AudioStreamPlayer`s for a 0.65-second crossfade during zone changes, loops the active stem in a normal desktop run and deliberately validates imported streams without starting playback in headless smoke mode so the process remains leak-free. `AmbientAudioSmokeTest` covers manifest closure, resource import, two-player ownership and zone switching; a real Metal/Forward+ smoke also observes the crossfade metadata. These stems and the crossfade are a technical demo ambience pass, not final field recordings, authored voice, mix/master or cultural approval; Marat's voice, Rinat's «Не отвечай», authored ambience mix and accessibility listening review remain open.

Texture sweep addendum, 2026-08-14: the v5 damp-earth and weathered-wood candidates have a separate non-overwriting Godot receipt in `art/texture_candidate_motion_sweep_v5/`. It covers 27 near/mid/far × FOV 65°/75°/90° cells for each mandatory scene at 1 920 × 1 080. This closes only the technical spatial-capture gate; it does not activate runtime owners or close earth relief/wetness, wood orientation/shared-owner, temporal, geometry, lighting or art-lock review.

V6 addendum, 2026-08-14: quieter damp-earth and abstract weathered-wood
siblings are registered as production candidates only. They pass the image gate,
the isolated v5↔v6 A/B receipt and a dedicated 27-cell Godot sweep, while the
runtime v1 mappings remain active. The earth relief-only delta is subtle and
wood still needs facade/fence/furniture/end-grain owner review; no art lock is
declared.

Asset provenance addendum, 2026-08-14: `eng/verify-asset-registry.sh` now checks
all 36 registry entries without Blender, including 36 derived-file hashes and
10 explicit local source hashes. The character source hash in
`assets/asset_registry.json` was corrected to the actual project file; negative
missing-file, duplicate-ID and hash-mismatch fixtures fail closed. The full
Blender gate remains host-dependent: elevated real-driver verification passes,
while the managed sandbox reports typed `HOST_TOOLCHAIN_BLOCKED` on the pinned
Metal startup crash. Neither result activates unreviewed art or changes runtime
owners.

Audio production readiness evidence, 2026-08-11: `Urman.ContentCli report --root .` compiles all four campaigns and enumerates eight campaign-level occurrences of the two non-decorative Chapter 1 voice assets. All 8/8 caption and transcript references are closed; all 8 occurrences remain intentional `logical-ref` placeholders, so authored voice count is 0 and the report stays `OPEN`. The same report confirms 4/4 ambient manifest files are physically present. This is a traceable production boundary, not a release claim: voice recording, final ambience mix, caption copy review and cultural listening review remain blocked.

The bounded 2026-08-11 benchmark pass adds non-interactive CRT/document/house details, shelf/calendar/radio/herb dressing, laundry/crates/hay/signage, additional shrubs, tapered low-poly pine tiers with deterministic side boughs, fallen logs, boundary charms and restrained fireflies to the three Godot style scenes. The latest environment rebuild keeps the deterministic modular-kit contract and adds authored edge breaks, a bounded `HouseA` facade pass (foundation, door/frame, window trims, porch/step and eave), layered `PineA` crown geometry, softened table/CRT forms and a bounded `OldPc` tower/panel/power-button detail pass; the capture additionally exercises the imported project-original `HouseA_` and `PineA_` modules in the day-street and Kara-Urman scenes through the production adapter. The 2026-08-12 recapture also exercises bounded v2-only stone/fabric owners on two stone anchors and three rug meshes. Current environment source/derived hashes are recorded in `assets/asset_registry.json`; the latest static frames are `d64b9e…`, `433a97…`, `6fe1c7…`, spatial sweep hashes are in `art/style_motion_sweep/README.md`, temporal hashes in `art/style_temporal_sweep/README.md`, and full-game hashes in `art/fullgame_frames/README.md`. All remain explicitly rejected as final art lock: the street/house read is denser, while the forest canopy is still a greybox family and the hero PC, cultural specificity, complete environment family and sound/presentation review remain open.

Act 5 epilogue presentation addendum, 2026-08-14: the canonical
`fullgame_act5_epilogue` wrapper now places the project-original `HouseA_`
module through the `act5-epilogue-house` variant and asserts its `wood_facade`
owner, LOD ranges and presentation-only proxy contract in the full-game smoke.
The fresh 1 920 × 1 080 frame is recorded in `art/fullgame_frames/README.md`.
This closes only the bounded wiring/capture slice; authored epilogue modules,
mesh-level collision, hero-prop detail, cultural review and production art lock
remain open.

Authored road-relief/puddle slice, 2026-08-13: `PainterlyEnvironmentDetails.AddRoadRelief`
replaces the flat road/path slabs in the three mandatory style benchmarks, the
Chapter 1 Zirat road and every compact Acts 2–5 `FullGameZone`. The helper emits
a deterministic 9×28 low-poly surface grid with shallow crown/rut variation and
216 matching collision cells per path; the Painterly shader and narrative/runtime
owners remain unchanged. `RoadReliefQaSmokeTest` and the extended
`CollisionQaSmokeTest` pass. This is production-candidate geometry evidence,
not final art lock: wet specular/roughness response, mesh dressing, village
module variety, temporal traversal review and cultural presentation remain open.

Wetness candidate QA, 2026-08-14: a separate test-only Godot harness instantiates
each benchmark scene in isolation, requires the ray collider to match the
scene's expected relief body, then clones the existing puddle `ShaderMaterial`
in memory and sweeps the existing `roughness_value` uniform. The source puddle
owner remains at baseline `roughness=0.90`; no shader, runtime registry,
collision, save or narrative path is changed. After bounded benchmark-origin
corrections (`PuddleFar` −10 mm, `BoundaryWetPatch` −22 mm from the previous
candidate origin), all five clusters/15 patches pass ±0.010 m contact and three
candidate frames are recorded. Wetness roughness/specular acceptance remains
OPEN.

Wetness matrix v2 addendum, 2026-08-14: a separately owned geometry slice seats
only eight measured `PuddlePatch` origins against their exact authored
road/path-relief cell (no cluster-wide translation and no collision-owner
change). The isolated Metal/Forward+ harness now records all 15 raw production
origins as exact-owner contact within ±0.005 m, with zero candidate-local
alignment needed, then compares roughness `0.40/0.50/0.60` with 45 in-memory
clones and nine 1 920 × 1 080 PNGs. Source roughness stays `0.90`; shader,
textures, GLB, runtime, saves and narrative remain unchanged. The frames still
read as flat/weak wetness, so no roughness value is activated and the
wetness/art-lock gate remains OPEN.

Lighting/fog calibration addendum, 2026-08-14: `StyleBenchmarkZone.cs` received
only a bounded ambient/fog and key/fill-light calibration. The fresh static,
27-cell spatial and 216-sample temporal captures are clean Metal/Forward+
technical evidence, but day street remains greybox, the house remains warm and
sparse, and Kara-Urman still needs authored canopy/ground geometry. This is a
presentation candidate, not a global texture switch or art lock.

## Visual Style Rules

- Геометрия: упрощённая, но не примитивная; сильные силуэты, аккуратные пропорции, больше деталей только у hero props.
- Материалы: ручная тушевая линия и акварельная неоднородность переводятся в albedo, декали и маски, а не рисуются как плоская сцена.
- Цвет: приглушённая бумага, болотные зелёные, старое дерево, холодный вечер, слабый янтарный домашний свет.
- Текстура: бумажное зерно, акварельные пятна, умеренная потертость.
- Крипота: через свет, тишину, композицию, тени и почти-заметные силуэты, не через gore / скримерные монстры.
- Персонажи: стилизованные 3D-модели для присутствующих NPC; 2D-портреты могут нести дополнительную эмоцию в диалогах.

## Characters

- Asset: Айдар
  Purpose: главный герой, реакции, портрет / модель.
  MVP priority: High
  Needed for: интро, диалоги, журнал, эмоциональная идентификация.
  Notes: нужен не «герой хоррора», а обычный городской парень.

- Asset: Бабай / Мансур
  Purpose: дед, держатель пакта, главный семейный конфликт.
  MVP priority: High
  Needed for: дом, старый ПК, первые полуправды.
  Notes: каноническое имя — Мансур; фамилия пока не фиксируется.

- Asset: Әби / Гөлсинә
  Purpose: тепло дома, бытовой татарский, тихий архив.
  MVP priority: High
  Needed for: дом, язык, обереги, домашние документы.
  Notes: поздний лор сильнее раннего имени Миннигуль.

- Asset: Алсу
  Purpose: проводник, опора, недоговорённость, будущая линия.
  MVP priority: High
  Needed for: первая прогулка, water clues, диалоги.
  Notes: не делать «мистической девушкой».

- Asset: Тимур хәзрәт
  Purpose: safe zone, религиозная рамка.
  MVP priority: High
  Needed for: мечеть, моральная сцена, баланс фольклора и ислама.
  Notes: требует уважительной подачи.

- Asset: Ринат
  Purpose: участковый, dialogue key test.
  MVP priority: High
  Needed for: дело Марата, отказ, реакция на ключи.
  Notes: хороший NPC для первого prototype.

- Asset: Наиля
  Purpose: медпункт и медсправки.
  MVP priority: Medium
  Needed for: противоречия смерти Марата.
  Notes: может быть текстовым NPC на раннем прототипе.

- Asset: Разиля
  Purpose: сельмаг, слухи.
  MVP priority: Medium
  Needed for: социальная карта и бытовые clues.
  Notes: может работать через чат / портрет.

- Asset: Марат
  Purpose: эмоциональный центр через следы.
  MVP priority: High
  Needed for: фото, дневник, сообщения, могила.
  Notes: не обязательно полноценная модель в MVP.

## Locations

- Asset: Дом бабая и әби
  Purpose: первая safe zone, семейный центр.
  MVP priority: High
  Needed for: приезд, ужин, ПК, язык.
  Notes: можно начать как 2–3 экрана / комнаты.

- Asset: Двор / сарай / старая Нива
  Purpose: земной быт и связь с бабаем.
  MVP priority: Medium
  Needed for: разговоры, предметы, воспоминания.
  Notes: Нива важна как живая деталь.

- Asset: Главная улица
  Purpose: деревня как социальная сцена.
  MVP priority: High
  Needed for: первые взгляды, свободное перемещение, социальное давление.
  Notes: compact walkable first-person 3D zone with diegetic signs and landmarks.

- Asset: Modular village environment kit
  Purpose: сборка ходибельных участков Кырлая без производства каждого дома с нуля.
  MVP priority: High
  Needed for: дом, улица, кладбище, медпункт, мечеть, кромка Кара-Урмана.
  Notes: стены, крыши, окна, двери, заборы, калитки, дорога, столбы, знаки, растительность, collision and LOD-ready variants.

- Asset: Сельмаг
  Purpose: gossip hub.
  MVP priority: Medium
  Needed for: Разиля, слухи.
  Notes: интерьер может быть простым.

- Asset: Мечеть
  Purpose: safe zone Тимура.
  MVP priority: High
  Needed for: религиозная рамка.
  Notes: нужна культурная аккуратность.

- Asset: Медпункт
  Purpose: документы Марата.
  MVP priority: High
  Needed for: медсправка, Наиля.
  Notes: можно реализовать как UI-доступ к журналу.

- Asset: Кладбище
  Purpose: могила и дата Марата.
  MVP priority: High
  Needed for: первый сильный clue.
  Notes: атмосфера без прямого хоррора.

- Asset: Кромка леса
  Purpose: граница пакта.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: не нужна большая лесная карта.

- Asset: Река / берег
  Purpose: Су Анасы, Баранов, Алсу false lead.
  MVP priority: Medium
  Needed for: water clues.
  Notes: можно отложить, если cliffhanger у леса.

## UI

- Asset: Journal UI
  Purpose: факты, противоречия, персонажи, timeline.
  MVP priority: High
  Needed for: core loop.
  Notes: критично, иначе расследование развалится.

- Asset: Dialogue key picker
  Purpose: применять clues в диалогах.
  MVP priority: High
  Needed for: dialogue key system.
  Notes: лучше простой список с фильтрами.

- Asset: Old PC UI
  Purpose: главный документальный хаб расследования: архив, «Татарвики», документы, сохранённые сообщения, внутренний учёт.
  MVP priority: High
  Needed for: interface investigation, Марат mystery, first pact/system reveal.
  Notes: самый выгодный ассет MVP. Должен ощущаться абсурдно проработанным, но в MVP не требует полноценной ОС или свободного программирования.

- Asset: Vocabulary UI
  Purpose: татарские слова и unlocks.
  MVP priority: High
  Needed for: language mechanic.
  Notes: должен быть связан с уликами.

## Documents

- Asset: Медсправка Марата
  Purpose: противоречие причины смерти.
  MVP priority: High
  Needed for: Марат mystery.
  Notes: формулировки должны быть правдоподобными.

- Asset: Могильная запись / фото могилы
  Purpose: дата и факт смерти.
  MVP priority: High
  Needed for: первый clue.
  Notes: можно сделать как фото/экран.

- Asset: Дневник / сообщения Марата
  Purpose: эмоциональный голос отсутствующего персонажа.
  MVP priority: High
  Needed for: empathy.
  Notes: дозировать, не раскрывать всё сразу.

- Asset: Статья «Татарвики» о Шурале
  Purpose: фольклор как инструкция.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: сначала должна казаться наивной.

- Asset: Баранов 1967 excerpt
  Purpose: архивное доказательство повторяемости.
  MVP priority: Medium
  Needed for: Су Анасы / water line.
  Notes: можно использовать коротким отрывком.

## Audio

- Asset: Домашний ambience
  Purpose: тепло и безопасность.
  MVP priority: Medium
  Needed for: дом.
  Notes: контраст с улицей.

- Asset: Деревенская улица
  Purpose: быт, тишина, наблюдение.
  MVP priority: High
  Needed for: exploration.
  Notes: звук может заменить часть визуальной сложности.

- Asset: Лесная кромка
  Purpose: тревога и мистический след.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: кромка Кара-Урмана; шорохи, шаги, дальние звуки, голос Марата, напряжённые паузы вместо скримеров.

- Asset: Голос Марата
  Purpose: финальный эмоциональный и мистический удар MVP.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: должен звучать лично и узнаваемо, но не раскрывать природу источника.

- Asset: Ринат у кромки
  Purpose: короткое вмешательство «Не отвечай», раскрывающее его практическое знание опасности.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: силуэт / портрет / короткая постановка без объясняющего монолога.

- Asset: Мечеть
  Purpose: safe zone.
  MVP priority: Medium
  Needed for: Тимур.
  Notes: не делать декоративно или клишированно.

## Music

- Asset: Main motif
  Purpose: память, корни, тревога.
  MVP priority: Medium
  Needed for: intro / menu.
  Notes: лучше камерно, без эпического хоррора.

- Asset: Investigation texture
  Purpose: документы и ПК.
  MVP priority: Low
  Needed for: UI.
  Notes: может быть заменено ambience.

## VFX

- Asset: UI scan / old monitor effects
  Purpose: старый ПК.
  MVP priority: Medium
  Needed for: atmosphere.
  Notes: не мешать читаемости.

- Asset: Forest presence
  Purpose: первый мистический след.
  MVP priority: Medium
  Needed for: cliffhanger.
  Notes: силуэт/туман/звук важнее монстра; в принятом cliffhanger главное — голос Марата и реакция Рината.

## Props

- Asset: Старая Нива
  Purpose: бабай, поездки в Казань, семья.
  MVP priority: Medium
  Needed for: двор, воспоминания.
  Notes: сильная земная деталь.

- Asset: Обрег / вещь от әби
  Purpose: забота и тревога.
  MVP priority: Medium
  Needed for: language/home scene.
  Notes: не делать магическим артефактом без решения.

- Asset: Винтовка Марата
  Purpose: финал линии Марата.
  MVP priority: Low
  Needed for: full game / late reveal.
  Notes: для MVP лучше как упоминание.

## Icons

- Asset: clue types
  Purpose: имя, дата, место, документ, слово, фото.
  MVP priority: High
  Needed for: journal and dialogue keys.
  Notes: простые читаемые icons.

## Fonts / Typography

- Asset: UI font with Cyrillic and татарские glyphs
  Purpose: русский + татарский текст.
  MVP priority: High
  Needed for: all UI.
  Notes: проверить ә, ө, ү, җ, ң, һ.

- Asset: archival font style
  Purpose: старые документы.
  MVP priority: Medium
  Needed for: archive.
  Notes: читаемость важнее стилизации.

## Language Learning Assets

- Asset: vocabulary cards
  Purpose: слова, контекст, подтверждения.
  MVP priority: High
  Needed for: татарский mechanic.
  Notes: связать с clues.

- Asset: partially translated text state
  Purpose: re-read mechanic.
  MVP priority: High
  Needed for: documents/dialogues.
  Notes: нужна техническая поддержка.

## Archival Materials

- Asset: 1913 protocol excerpt
  Purpose: Тукай / old order.
  MVP priority: Low
  Needed for: full game.
  Notes: культурно рискованно, осторожно.

- Asset: 1967 Baranov folder
  Purpose: Су Анасы / external researcher.
  MVP priority: Medium
  Needed for: archive quest.
  Notes: может стать MVP teaser.

## Material owner calibration — 2026-08-14

`PainterlyMaterialLibrary` сохраняет общий v1 `wood` owner для процедурного
декора, но импортированный Blender-модуль больше не обязан использовать один
масштаб для всех поверхностей. Семантические presentation-owners
`wood_facade`, `wood_fence`, `wood_furniture` и `wood_bark` используют тот же
weathered-wood albedo с отдельными world scales (1.8, 2.2, 2.7 и 1.35), чтобы
фасады, заборы, мебель и стволы не образовывали одинаковую полосу. Это
калибровка материала, а не изменение shader, геометрии, коллизий или narrative
state.

Generated character kit сохраняет metadata-имя `cloth`, но теперь получает
явный `old_fabric_v2` descriptor вместо texture-less fallback. Это bounded
presentation owner для одежды; folds, silhouette, лицензия, cultural review и
финальный art lock остаются открытыми.

## Authored village modules — 2026-08-14

The rebuilt Blender kit adds `WellA_` (7 LOD0 parts), `WoodpileA_` (4),
`GateA_` (4) and two `OldPc_` hero details (`DriveSlot`, `LabelPlate`), with a
deterministic 49-mesh LOD1 contract. Registry records
`env.well.a`, `env.woodpile.a` and `env.gate.a` share the project-original
source/derived hashes and remain decorative. Act 2 uses the well and woodpile
beside the HouseA anchor; the Act 5 boundary uses GateA as the visual threshold
after removing duplicate procedural posts/board/mark. No new gameplay collider
or runtime-state owner is introduced, and GateA is deliberately not attached
to the HouseA epilogue anchor. Fresh Godot captures and runtime marker-clearance
smoke now exist. The runtime-backed receipt
`art/fullgame_boundary_runtime_capture/fullgame_boundary_runtime_manifest.json`
instantiates `full_game.tscn` with `RuntimeBridge`, resolves both Act 5
interaction IDs, records GateA clearance `2.687 m`, and validates a dedicated
marker camera with both interaction anchors inside the viewport and no GateA
screen-space overlap. The standard and marker frames are
`godot_act5_boundary_runtime_standard_1080p.png` (SHA-256
`eadb15d9f593685244d1e7ad59db74f5b7ac5d96458ac3bad6071433887a8ed7`) and
`godot_act5_boundary_runtime_markers_1080p.png` (SHA-256
`ccfbdb4eb043c978ef3ab636d6cd06ffbb6fdca2445f6c44643da9c083982128`). This
closes the missing runtime-owner and marker-camera evidence for the static
composition only;
near/mid/far screen-space traversal, repetition, cultural review and art lock
remain open.

## Puddle silhouette diagnostic v1 — 2026-08-14

`art/puddle_silhouette_candidate/` contains a non-overwriting Godot
comparison of the fifteen existing `PuddlePatch` meshes in the day street,
Kara-Urman edge and Zirat road. In memory only, the source `CylinderMesh` is
replaced with an open, shallow, irregular faceted `ArrayMesh`: it has no bottom
face or vertical cylindrical rim. The source `ShaderMaterial` stays referenced
at `roughness_value=0.90`; no texture, shader, `PainterlyMaterialLibrary`,
collider, production scene, save or narrative owner changes.

Three 1 920 × 1 080 contact sheets place the flat source on the left and the
candidate on the right, with a fixed-camera overview plus a close diagnostic
row. Exact relief ownership and all source/candidate contacts pass ≤5 mm;
collision-node counts are unchanged and all source mesh references are restored
before shutdown. This is deliberately a silhouette test, not a wetness
acceptance: the faceted surface loses the obvious plate/rim, but at source
roughness it is too quiet to prove wet readability at distance. Keep both
geometry and wetness/art acceptance OPEN pending traversal and level-art review.

## GLB presentation-contract smoke — 2026-08-14

`GeneratedModularKitContractSmokeTest` is a host-independent Godot receipt for
the single project-original environment artifact
`game/assets/generated/urman_modular_kit.glb` (SHA-256
`c9f9e8d9a036c3fc8ef20dfc736a164fb393eb8194eff0c0e55782547efa2c36`). It
loads the GLB through `GeneratedModularKitDressing.AttachPresentationOnly` and
checks the exact LOD0/LOD1 pairs `HouseA_ 12/12`, `FenceA_ 6/6`, `RoadDirt_ 1/1`,
`PineA_ 2/2`, `TableA_ 5/5`, `OldPc_ 8/8`, `WellA_ 7/7`, `WoodpileA_ 4/4` and
`GateA_ 4/4`. Each selected family must expose only published LOD meshes with
the 0–24 m / 18–72 m self-fade policy, exact semantic-owner sets, one-to-one
LOD names and zero physics descendants. The adapter proves that imported
collision bodies/shapes were removed and hides the only two non-LOD render
helpers (`HouseA`/`OldPc` in Godot, corresponding to Blender's `HouseA-col`/
`OldPc-col`) rather than letting a prefix match make them visible.

This is a bounded technical PASS, not mesh-collision acceptance or art lock.
The Blender source/GLB provenance remains independently verified; Blender host
revalidation, authored mesh collision, traversal/readability, cultural review
and release-hardware gates remain OPEN.

## OldPc hero-detail candidate — 2026-08-14

The project-original kit now includes `OldPc_DriveSlot_LOD0/1` and
`OldPc_LabelPlate_LOD0/1`. They are small low-poly presentation details under
the existing `prop.oldpc.crt` asset id; they add no collision, interaction or
narrative owner. `eng/capture-oldpc-hero-detail.sh` produced two 1 920 × 1 080
Metal/Forward+ close frames at the Act 3 Soviet interaction distance, with all
four exact names present and no Godot/RID/ObjectDB leak diagnostics. The close
frames are useful production evidence. A follow-up
`eng/capture-oldpc-hero-detail-motion.sh` receipt now covers near and mid
first-person distances, FOV 65°/75°/90° and three small head-bob rows in one
isolated Metal/Forward+ world. All 18 tiles find the four exact details and
pass the non-empty readback gate; the near/mid contact sheets and manifest live
in `art/oldpc_hero_detail_motion_sweep/`. This strengthens motion/readability
evidence only: observed traversal, standard Soviet regression review,
cultural review, release-host performance and art lock remain OPEN.

## Temporal/style calibration evidence — 2026-08-14

The current 216-sample Metal/Forward+ receipt preserves visual descendants
while stripping only render-only collision shapes; its predecessor is retained
as superseded audit material. `StyleCalibrationCandidateCapture` adds six
isolated baseline/candidate frames for ambient/fog/key-light comparison. Both
are production-candidate evidence only: no global light/material switch is
active, and geometry, comfort, cultural and art-lock review remain open.

## Act 1 demo presentation boundary — 2026-08-14

The current playable target is the dedicated `game/scenes/act1_demo.tscn`
entrypoint, not the full Acts 2–5 asset set. It reuses the five compact Chapter
1 zones and existing Painterly Low-Poly candidate materials, with a runtime
intro card and a final `НЕ ОТВЕЧАЙ` / `Конец демо` overlay. This makes the route
playable without pretending that the three benchmark captures are final art.
Remaining demo asset gates are authored voice/ambience, near/mid/far visual
readability, cultural review, observed first-time playtest, release-host
performance and art lock. Full-game dressing and web retirement remain
deferred; no old assets or browser saves are removed.

The bounded day-street presentation pass adds project-original `WellA_` and
`WoodpileA_` modules plus a muted physical `ФАП` `Label3D` on the existing
signboard. The modules remain presentation-only with hidden helper-box
collision ownership; the label is a diegetic wayfinding candidate rather than
a floating quest marker. Static and 27-cell Metal/Forward+ receipts are
recorded in `art/style_frames/README.md` and `art/style_motion_sweep/`; observed
route comprehension and cultural review remain open.

## Act 1 first-person corridor evidence — 2026-08-15

The demo now has a fail-closed, production-path first-person corridor smoke at
`game/tests/Act1FirstPersonCorridorSmokeTest.cs`. It drives the actual
`act1_demo.tscn` entrypoint through the camera ray and mapped `E` input, checks
the five compact zones, opens the official notice / saved message / Татарвики /
edge-sketch documents through `DocumentUi`, commits the Rinat `alerted` state
and reaches the authored Kara-Urman cliffhanger. The scene and wrapper are
listed in `art/first_person_corridor/README.md`.

The physical evidence targets now own explicit compiled document IDs. The house
adds a presentation-only empty chair, coat and radio beside the Rinat target so
the practical off-screen warning has a readable world cue without inventing an
unregistered Rinat mesh. These are demo presentation aids, not final character
assets; authored Rinat staging, voice, cultural review, near/mid/far traversal
and art lock remain open. No texture, shader, save, narrative kernel or Acts
2–5 launch owner changed.

## Act 1 house OldPc presentation candidate — 2026-08-15

The first-act house now consumes the project-original `OldPc_` family through
`GeneratedModularKitDressing.AttachPresentationOnly`. The procedural CRT bezel,
keyboard keys, tower and hero scanline boxes were removed from the benchmark
zone; the authored table, documents, mouse, lamp and existing interaction target
remain. The module is anchored at the existing interaction location and keeps
the adapter's exact 8/8 LOD contract, material routing and imported-collision
sanitation. It contributes no `CollisionObject3D` or `CollisionShape3D`; the
layer-1 `InteractionTarget` remains the only gameplay ray owner.

`SceneSmokeTest` and `StyleFrameCapture` now fail closed on the scene metadata,
8/8 counts, positive imported-physics removal and zero module physics. The
fresh house frame is recorded in `art/style_frames/README.md` and is a
production-progress candidate only. Close interaction readability, observed
first-person pacing, cultural review and the Painterly Low-Poly art lock remain
open.

## Act 1 Kara-Urman forest-edge kit — 2026-08-17

Status: **PARTIAL / authored-geometry production candidate; art lock OPEN.**

The new project-original pair is:

- `assets/source/blender/urman_forest_edge_kit.blend` — 1,365,721 bytes;
  SHA-256 `75de1f8ab43f3d29736bf44dcba05da6d0bcf51649a043f6aac8d4369aa3f3d3`;
- `game/assets/generated/urman_forest_edge_kit.glb` — 544,480 bytes;
  SHA-256 `7cc13472f0024861d5b91ae6710b3c0ac18e7fbefb87ae9dc73110604a9791ba`.

The GLB publishes 16 independent families with exact LOD0/LOD1 nodes: left
and right `ForestBank`, left and right `ForestOccluder` clusters,
`PineMass_A`, `PineMass_B`, `BirchEdgeMass`, `DistantForestMass_A/B`, left
and right `UnderstoryRootWall`, `CrookedStump`, `BranchSilhouette_Hook`,
`FallenLogCluster`, `MossyBoulderCluster` and
`SideGate_WayfindingLandmark`. This is 32 MeshInstance3D nodes plus the
`ForestEdgeKit_Root` node. Exact names and per-family enablement/limits are
documented in `art/act1_forest_edge_kit_2026-08-17.md`.

The kit uses authored low-poly geometry to break the straight repeated-PineA
read and close the empty horizon. It reuses the existing semantic material
direction (`earth`, `stone`, `wood_bark`, `foliage` and plain wood) without
creating or downloading raster textures. All meshes carry `collision=none`;
there are no `-col`, `StaticBody3D` or `CollisionShape3D` descendants. A later
composition task must keep path/floor collision and route state in their
existing owners.

Evidence: pinned Blender 4.5.12 export and Blender re-import succeeded; pinned
Godot 4.7.1 .NET `--import` and a direct PackedScene load probe succeeded with
`32` mesh nodes and `0` collision nodes. The neutral preview is
`/private/tmp/urman_forest_edge_kit_preview.png` (1600×900, 1,709,781 bytes).
The preview grade is **3.4/5, partial**: side silhouettes and horizon variety
are materially improved, but banks still need first-person ground integration,
small details need a close read and the side gate is not yet a strong overview
landmark. This does not claim demo/art acceptance, traversal comprehension,
target-hardware performance, cultural review or a final forest family.

The asset registry remains unchanged by ownership constraint; add its source/
derived hashes in the registry before production runtime wiring.

## Act 1 complete-village landmark kit runtime composition — 2026-08-17

Status: **PARTIAL / authored-geometry composition candidate; art lock OPEN.**

The accepted project-original village landmark pair is now present at:

- `assets/source/blender/urman_act1_village_landmark_kit.blend` — SHA-256
  `5df27a1bec9e71bd4968c3ba702ae6f5cabbaccb7b1632393acbda7529dd644e`;
- `game/assets/generated/urman_act1_village_landmark_kit.glb` — SHA-256
  `bd5016db2b0e9d9187b6c200dd1564c2333396b0d9b70971b89cb4539f074daf`;
- the production record is `art/act1_village_landmark_kit_2026-08-17.md`.

`Act1ConnectedWorld` loads the GLB as a presentation-only root named
`URMAN_Act1_VillageLandmarkKit`, validates the exact authored groups
`Arrival`, `VillageStreet`, `BabaiYard`, `HouseExterior`, `ConnectiveStreet`,
`FapExterior`, `ReturnStreet`, `ZiratBoundary` and `KaraApproach`, and
re-anchors those groups to the existing arrival, village, house, FAP, zirat
and Kara route zones. The source is a linear authored strip; the runtime
placement is therefore deliberately group-based rather than a blind root
transform.

The GLB contributes no collision, navigation or interaction nodes. Existing
route floor/path collision, zone interaction targets and `RuntimeBridge`
state ownership remain authoritative. Only the old visual-only
`ZiratGreyboxEnclosure` duplicate is suppressed; gameplay floor and route
continuation are retained. The existing forest-edge kit is unchanged.

Pinned Godot import, connected-route smoke/walkthrough and a fresh six-frame
root-viewport capture pass are technical evidence for this composition, not
demo or art acceptance. Babai remains near-wall heavy; Kara side dressing is
busy; the reverse Zirat view still leans on the existing village framing; and
full near/mid/far traversal, cultural review, target-hardware performance,
wayfinding and final art lock remain open.

## Зимний реестр и реальные шаги — 2026-09-11

Реестр синхронизирован с source/generator/GLB зимнего изменения `1f42f95`.
Исходники и экспорт уже были изменены вместе; ошибочными оставались прежние
SHA, материалы и счётчики реестра. Текущие байты совпадают с тем коммитом.
Village exterior: 725 mesh nodes / 21 845 triangles / 15 materials; terrain:
61 / 72 296 / 11; foliage: 218 / 74 454 / 15. Счётчики получены из GLB,
повторная генерация и визуальная приёмка не заявляются.

Добавлены 12 активных CC0 шагов из собственного footstep manifest: packed/soft
snow, wood, interior floor. Каждый имеет SHA исходника и результата, лицензию,
генератор и ссылку на provenance. Release validator требует именно эти четыре
семьи; исторические wet-road/mud/grass не являются активной библиотекой Акта I.
Проверка реестра — целостность 67 записей; финальное прослушивание и art lock
остаются отдельными открытыми требованиями.

## Точка поворота персонажей — 2026-09-11

У общего character kit каждый префикс имеет свой сдвиг на экспортном стенде.
Прежний adapter вычитал его только из позиции instance; последующий yaw
вращал модель вокруг начала стенда и уводил от interaction target. Теперь
adapter вычитает offset из root children, оставляет локальный anchor в нуле
и ставит instance в требуемую точку. GLB/source не изменены, анимационные
каналы обращаются к костям, ни один scene root не анимируется.

Actual Metal capture `character_anchor_fix` подтверждает дом, ФАП, улицу и
зират после исправления; Наиля теперь стоит у своего target. Отдельный
`menu_winter_background` показывает зимнюю улицу за читаемой тёмной панелью
вместо почти чёрного фона. Художественная доработка персонажей и света остаётся.

Bounded character-art calibration, 2026-09-11: all nine project-original
prefixes were regenerated from the shared Blender source with longer visible
leg runs, tapered trousers and compact beveled boots; the six Act I-facing
roles retain distinct coat/accent palettes, hats, hair profiles and the Naila
side bun/locks. Faces now use separate eye whites, recessed dark irises,
tapered brows, upper lids, modeled nose/lower lip/chin and volumetric beard
pieces; hands remain HeadHand-routed skin meshes. Existing prefix names,
LOD0/LOD1 pairing, ground anchors, Idle/Tension clips and no-collision policy
remain intact. The source SHA is `e5a59400f53741010cec3823f739580bcce134c9f011670da679ad1ceafb9b17`;
the derived GLB SHA is `f953999fad14b4a61605aca2e086a8a70bcd29743b1e8345db73ef31f0b29d77`.
Fresh Metal route frames are in the external `character_art_luna` evidence
directory. This is a bounded calibration pass, not final BOTW/TOTK, cultural,
lighting or art-lock acceptance; street NPC visibility and the remaining
character close-up review stay open.

### 2026-09-11 — семейная фотография и домашние предметы исследования

Новая project-generated ImageGen-текстура `game/assets/textures/act1/photo_first_snow_v1.png` используется на лицевой стороне поворачиваемой фотографии. Полный prompt/mode/provenance — соседний README; SHA-256 — asset registry. Оборот — реальная плоскость с подписью «Первый снег». Коробка для шитья, катушки, карточка и починенная лампа собраны в существующем world owner из runtime-геометрии. Это игровые предметы с изменяемым состоянием, не декоративные interaction proxy cubes. Общий арт-стиль и качество кадров пока не приняты.

При проверке реальных кадров скорректированы высота лампы по authored столешнице (верх 1,00 м), отметки по передней плоскости косяка, ширина подписи фотографии. Коробка стоит на существующем сундуке у входа, а не поверх бумаг у ПК. Коллизия этого сундука перенесена вслед за ранее сдвинутой моделью: прежний proxy оставлял невидимый блок у задней стены.


### 2026-09-11 — character kit round5b

Пересобраны source Blender и runtime GLB из общего генератора: 9 персонажей,
313 LOD0 / 313 LOD1, 20100 / 10010 экспортированных треугольников, 40 материалов.
Обновлены обе SHA-256 в registry. Сохранены имена meshes, rigs, Idle/Tension,
владение коллизиями у gameplay targets. Пропорции зимней одежды и размеры
деталей лица различаются; Алсу использует округлую макушку. Native captures
`character_round5_frames` и `character_round5b_frames` просмотрены;
выразительные лица, культурная проверка и финальный художественный уровень
остаются OPEN. Указанные числа — экспортированные GLB triangles, не исходные
полигоны Blender.


### 2026-09-11 — записанный ветер у зирата

`zirat_wind.wav` заменён подготовленным фрагментом Magnesus — Wind in forest,
Freesound 606960, CC0 1.0; общедоступный HQ preview, не исходный master.
Source URL, лицензия, исходный/итоговый SHA и преобразование закреплены
в manifest/registry, автор указан в титрах. Итог: PCM16 RIFF, mono 24 кГц,
26.430292 с, peak −3.0983 dBFS, RMS −25.4514 dBFS, overlap .25 с.
Скачок на границе loop ниже p95 соседних изменений исходного сигнала;
это числовая проверка, не прослушивание. Godot import и существующая
проверка переходов ambience прошли: один слой после crossfade.
Процедурный генератор больше не перезаписывает эту запись. Художественный
микс, слуховая приёмка loop и реальные финальные голоса остаются OPEN.

### 2026-09-11 — округлые волосы и объём камней

В существующем character kit округлены восемь оставшихся волосяных макушек,
слегка увеличены существующие детали глаз и рта. Имена, девять ригов,
Idle/Tension и 313 пар LOD сохранены. Экспорт содержит 20 772 треугольника
LOD0 и 10 346 LOD1; максимум одного персонажа в одном LOD — 2388 из 3000.
Blender verifier прошёл; реальные Metal кадры Алсу, Тимура и Рината просмотрены
в `exploration24_art_frames` внешнего каталога. Силуэты и лица по-прежнему
требуют художественной доводки; это не art lock.

В Kara kit четыре прежних камня получили объёмные кольца и грани торцов.
Отдельный идемпотентный проход после Wave17 сохраняет предыдущую очистку,
имена и размещение. Проверенный экспорт: 202 meshes, 5040 triangles из 5200,
16 материалов. Native кадры просмотрены; слишком вертикальные силуэты части
камней и форма ближних ветвей остаются художественными дефектами.

### 2026-09-11 — индивидуальные лица и зимние силуэты

В существующих FACE_PROFILES / WARDROBE_PROFILES изменены пропорции глаз,
бровей, носа, плеч и подола. Шесть колец головы дают более выраженную челюсть
и щёки; имена, риги и точки привязки сохранены. Blender generation/verification
и импорт прошли: 9 персонажей, 313 пар LOD, 20 772 / 10 346 треугольников,
максимум 2 388 на персонажа в одном LOD. Native Metal кадры сохранены в
`URMAN_ActI_Finish_20260911/art_round8_frames`. Это промежуточная доводка:
лица и общий мир ещё не достигли требуемого художественного качества.

### 2026-09-11 — минарет и контакт лесного берега

Девять прежних элементов единственного дальнего минарета получили
восьмигранный сужающийся силуэт, галерею и сдержанную зелёную кровлю.
Точка, высота и имена сохранены; культурная проверка по-прежнему открыта.
Вид за домом смещён на 2,6 м к западу: верхняя часть ориентира читается
над ветвями, хотя густота и форма деревьев ещё требуют доводки.

Точный native triangle probe показал, что крупные вертикальные формы
у лесной колеи принадлежат AgentB KaraRoot_/KaraStone_, а не четырём
MossyBoulder из отдельного kit. Исправлен существующий проход размещения:
низкие округлые камни заглублены в снег, материал — камень; их физические
trimesh теперь создаются после изменения геометрии. FallenLog сохранён.
Просмотрены actual Metal кадры `landmark_round4_frames`; narrow Kara walk
прошёл 157,32 м без препятствий, rear walk — 78,02 м. Это локальное
улучшение формы и контакта, не приёмка художественного качества всего мира.

### 2026-09-11 — записанные фоны дома, ФАПа, деревни и леса

Семь оставшихся активных фонов Акта I заменены CC0-записями из Freesound:
callmethefoo 744447 (дом), RIFORKA 801025 (ФАП), bruno.auzet 670307
(зимний сосновый лес), lwdickens 261226 (зимний ветер для улицы, приезда,
двора и возвращения). Использованы общедоступные HQ preview, не исходные
master-файлы и не полевые записи самого Кырлая. Ветер зирата Magnesus
606960 сохранён. Права, SHA, конвертация и авторы отражены в manifest,
registry и титрах. Длительность новых PCM16 mono 24 кГц петель — 69–121 с
вместо 8 с; RMS 0,008–0,035, максимальный измеренный peak 0,594.

Существующий generator теперь принимает проверенные по SHA исходники,
использует нормализацию WAV из footstep pipeline и не синтезирует замену
пропущенной записи. Второй старый generator оставлен только для неактивной
в Акте I воды. Нового runtime audio owner нет. Нативный импорт и проверка
пяти переходов прошли (`recorded_ambience_import.log`,
`recorded_ambience_transition.log`); прослушивание и финальный микс
не подменяются проверкой PCM или наличием файла.

### 2026-09-11 — размер зимних лип

У существующего WinterLinden_1 уменьшен spread с 1,25 до 0,86 одинаково
для near/light/far. Причина — чрезмерная ширина первичных ветвей: некоторые
липы занимали около 10 м в ширину и перекрывали соседние силуэты. Сохраняются
seeded-форма, топология, точки посадки и действующий LOD. В Blender заново
созданы source/GLB; проверены прежние 218 mesh nodes, 74 454 треугольника
и 15 материалов, SHA обновлены. Импорт и четыре Metal кадра
`linden_narrow_frames` проверены. Тёмный материал и однообразие части
древесных силуэтов остаются видимым художественным долгом.

### Уточнение зимнего представления — 2026-09-11
Убраны четыре летних светлячка из старого лесного benchmark: их светящиеся
полигоны были видны в действующем зимнем Кара-Урмане. Новые мистические
эффекты взамен не добавлены. Для обычной лиственной коры сохранена
существующая текстура bark_pine_v1; базовый оттенок 9b9487 компенсирует
двойное затемнение тёмной текстурой и прежним тёмным tint. Берёза, ель
и освещение не изменены. Runtime-проверка активного материала подтвердила
этот оттенок и текстуру; просмотрены дневные и ночные native кадры.
Ночной лес всё ещё требует работы с силуэтами и композицией.

Персонажи: исходник и GLB после формы переносицы и соединения прядей
содержат 19782 LOD0 + 9842 LOD1 = 29624 треугольника, 313 пар мешей,
18 клипов для 9 скелетов; максимум одного LOD0-персонажа — 2278
треугольников при бюджете 3000. Hash в asset_registry обновлены.

### 2026-09-11 — источник голов Quaternius, CC0

`assets/source/blender/characters/urman_cc0_head_templates.blend` — локальный source для голов и волос; лицензия автора, происхождение ZIP и команда подготовки сохранены рядом. `tools/blender/prepare_character_heads.py` воспроизводит производные из бесплатного Standard-пакета: объединяет швы, отделяет голову от тела, ограничивает число треугольников, уменьшает текстуры и удаляет исходный rig. `generate_character_kit.py` использует эти шаблоны со своими одеждой и анимациями.

Это смешанное происхождение: одежда/rig/анимации — Project-original; геометрия и текстуры лица/волос — CC0 Quaternius с изменениями. Runtime GLB включает шесть общих изображений: два albedo 1024², два normal 512², albedo и normal глаз 256². Godot извлекает их в соседние PNG при импорте; они являются производными того же GLB. Проверка Blender сохранила девять префиксов и пары LOD, а реальные игровые кадры подтвердили, что текстуры глаз не потерялись. Окончательная художественная и культурная оценка открыта.

Проверка CC0 round 3: Blender verifier — 9 prefixes, 190 пар LOD, Idle/Tension, отсутствие коллизий и корректная привязка кистей/больших пальцев к рукам. GLB: 30 752 треугольника суммарно, максимум 2367 для одного LOD0, 46 материалов, 6 карт. Сборка C# и импорт Godot прошли; пять кадров 1920×1080 (Алсу, Ринат, Тимур, дом, ФАП) захвачены и просмотрены. Седина и один задний пучок заменили прежние формы, но возраст лица, одежда и художественная готовность ещё открыты.

### 2026-09-12 — плечи, текстиль и контакт с полом

У character kit верх корпуса теперь образует наклон плечей; отдельная плоская ShoulderWrap удалена, рукава имеют четыре кольца с утопленной верхней крышкой. Кисти и воротники уменьшены. Итог: 181 пара LOD, 31 112 треугольников суммарно, максимум 2391 на персонажа LOD0; девять скелетов/Idle/Tension и привязка кистей проверены. Пять native кадров 1920×1080 просмотрены; общая художественная приёмка остаётся открытой.

Ковёр HouseInterior_RugField находился под досками (верх .0375 м против .055 м пола). В Blender-источнике центр поднят до .070 м; ковёр теперь лежит над полом с зазором .0025 м снизу. Материал carpet использует уже подготовленный ImageGen carpet_palas_v1_albedo.png с шагом проекции .25/.40. SHA и исходный промпт сохранены; орнамент остаётся предметом культурной проверки. Источник/GLB пересобраны, существующий verifier пройден, ковёр виден на native кадре дома.

В существующем Environment дома и ФАПа включено мягкое SSAO (.55/.30) для контактов мебели; уличный Environment не менялся. Это локальная поправка объёма, не финальная световая приёмка.

### 2026-09-12 — материалы стен дома и ФАПа

Три подготовленные ImageGen-карты подключены к существующим семантикам PainterlyMaterialLibrary: wallpaper (1.1/1.1 по обеим осям проекции), log_wall (.9/.9) и wall_institution (1.3/1.0). Обои назначаются боковым стенам дома, брёвна — передней и задней; краска ФАПа — только стенам и панелям. Пол и потолок ФАПа исключены из этого текстурного назначения. Источники, промпты и SHA-256 сохранены в painterly README и asset_registry.json.

Сборка C# и восемь native кадров 1920×1080 пройдены; все ракурсы дома и ФАПа просмотрены. Логи и кадры: /Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/interior_wall_material1_*. Это проверка назначения материалов, не завершение художественной приёмки: формы мебели и общий свет ещё требуют доработки. После предыдущего коммита также прошёл существующий act1_demo_launch_smoke_test: обычное меню → новая игра → вступление → глава I; пользовательские данные восстановлены.

### 2026-09-12 — силуэты домашней лежанки и стойки ФАПа

В исходнике modular kit спинка лежанки понижена с 1.30 до .86 м и слегка сужена сверху существующим tapered-box helper. Столешница стойки ФАПа уменьшена с .16 до .06 м и опущена до .99 м, сохраняя контакт с корпусом. Планировка и коллизии не менялись. Оба BLEND/GLB пересобраны, SHA-256 десяти записей registry обновлены. Modular verifier: 133 LOD1; FAP validate: 405 meshes, 12 110 triangles, 28 interior LOD1. Восемь native кадров 1920×1080 просмотрены (`interior_prop_v2_frames`); спинка и столешница читаются легче. Общая художественная приёмка и свет остаются открытыми.

В тележке ФАПа устранён зазор .27 м между четырьмя ножками и колёсами: нижний край ножек теперь .24 м, верхний сохранён 1.21 м. Свежий GLB и native `window_discovery_v6_frames/fap_interior_right.png` подтверждают соединение. Актуальный registry preflight: PASS, 72 assets; manifest ФАПа синхронизирован с текущими bytes без переноса старого утверждения о детерминизме на новую генерацию.

У бокового окна улицы текстурированная светлая занавеска видна узкой полосой за приоткрытой ставней. Ширина ставни = ширина исходного стекла + .04 м; исходный угол −42°, открытый −160°. Кружка, варежки и рисунок на стекле раскрываются только после действия; это существующая находка, новая сущность награды не добавляется. Ткань — уже включённая old_fabric_v3, с мягкой эмиссией .35. Два native кадра v6 просмотрены: подсказка видна до действия, бытовые детали после. Существующая ChapterOneFlow-проверка теперь явно проверяет household/fog после загрузки и их сброс, а не постоянный контейнер; прошла на v4 той же логики состояния.

### 2026-09-12 — дальность силуэта у тёплого окна Кара-Урмана

По временной проекции mesh AABB найден точный владелец чрезмерной тёмной развилки: KaraMixedMassWestNear, WinterLinden_1. Дерево отодвинуто с мировой точки (-9.5, -108.5) до (-23.45, -102.45), высота уменьшена с 8.1 до 6.1 м. Оно остаётся справа от окна в глубине леса. На native кадре тёплого окна развилка больше не занимает передний план; дополнительно просмотрены forward/left/depth подхода. Игровые коллизии и сюжетные триггеры не менялись; временный диагностический код удалён до сборки. Это локальная композиционная правка, не art lock.

### 2026-09-12 — распределение света в интерьерах

Существующий RoomFill перенесён ближе к центру дома: (0, 2.5, .5), радиус 8.5 м, энергия .56. В ФАПе энергия общего освещения снижена до .48, холодного света у окна — до .44. Новые источники не добавлялись. C# build прошёл без ошибок и предупреждений; шесть native кадров `interior_light2_frames` просмотрены: небольшой подъём освещённости дома и более заметный локальный свет ФАПа. Это ограниченная коррекция; общая художественная приёмка остаётся открытой.

Для authored-хвойных Кара-Урмана существующий VegetationHash выбирает WinterSpruce_1 или WinterSpruce_2; оба варианта и их light/far уровни присутствуют в текущем GLB. Четыре native кадра `spruce_variants_frames` просмотрены после успешной сборки: пустых деревьев и перекрытия основного пути не видно. Повторяемость уменьшена, но раздельные ярусы кроны остаются художественным недостатком.

### 2026-09-12 — более цельная ближняя крона елей

В исходном winter_spruce_variant линейное сужение заменено плавным .27 × (1 − level/12)^.65, корень хвойной ветви утолщён с .10L до .19L, провисание уменьшено с .52t до .40t. Снежная шапка следует тому же радиусу. Сохранены узкая вершина, набор объектов и бюджет: 218 meshes / 74 454 triangles / 15 материалов всего kit. Генератор со встроенными проверками и Godot import прошли; три из четырёх native ракурсов `spruce_crown_v2_frames` просмотрены. Ближняя крона заметно плотнее, но редкие ярусы light/far ещё требуют художественной коррекции. Registry синхронизирован с текущими BLEND/GLB; общий art lock открыт.

### 2026-09-12 — открытый тканевый абажур дома

Закрытый короб LampShade заменён открытым восьмигранным усечённым конусом (.18/.28 м, высота .28 м) с существующей old_fabric_v3. Его собственная тень отключена, слабая эмиссия .25 передаёт просвечивание ткани; тени остальных предметов и WarmTableLamp сохранены. Стойка укорочена до .64 м, свет опущен до 1.70 м. Удалено только отображение дублирующей WallShelf через существующий список замещённых визуалов; StillLifeShelf и предметы на ней сохранены.

C# build: 0 ошибок/предупреждений. Четыре native ракурса дома `house_lamp_v1_frames` захвачены и просмотрены: абажур читается как лампа и не перекрывает окно большим ящиком. Это коррекция формы и локального света; полную блокировку прежнего omni-light экспериментально не измеряли. Коллизии, диалоги и сохранения не менялись; общий art lock открыт.

### 2026-09-12 — масштаб и опоры рабочего места дома

После исправления parent-local anchor старый ПК стал виден на своём столе. Для домашнего масштаба OldPc_ экземпляр уменьшен до .45, центр CRT — (0,1.15,-3.68): ширина корпуса .63 м, низ около .891 м при столешнице .89 м. Корпусу возвращён светлый выцветший пластик; область взаимодействия, мышь и локальный свет согласованы с прибором. Прежний отдельный CrtInnerScreen/scanline/vent декор удалён: детали принадлежат уже видимому GLB. Кабель сохранён и перенесён к системному блоку.

Стул имеет сиденье .52×.50 м на высоте .47 м, четыре ножки и спинку до 1.09 м. У стола также четыре ножки. Документы и чайная чашка уменьшены и помещены в пределы столешницы; открытый верх чашки позволяет видеть чай. Это правка существующего рабочего места, без новых интерактивных сущностей или сюжетных условий.

Modular BLEND/GLB пересобраны; существующий verifier обновлён только для четырёх добавленных задних ножек: 137 LOD1 всего / 88 HouseInterior. C# build/import/registry (73 assets) прошли. Три native ракурса `house_workstation_v3_frames` просмотрены. Существующий полный CharacterBody walkthrough завершился на 403.08 м с игровым лучом взаимодействия, ПК и клиффхэнгером (`workstation_full_walk_v3.log`); пользовательские данные восстановлены. Это проходимость, не наблюдение за новым игроком и не финальная художественная приёмка.

Узкий FirstPersonInteractionSmokeTest согласован с новым масштабом: подход к столу на 0.55 м ближе и взгляд вниз на 14° через существующий ApplySmokeLook. Невидимая область ПК не увеличивалась ради старого горизонтального луча. Проверка gamepad A → дом → разговор → keyboard E → архив прошла (`workstation_interaction_v3_smoke.log`), сборка — 0 ошибок/предупреждений.

### 2026-09-12 — подавление заменённых деревьев до привязки мешей

BindAuthoredWinterTrees теперь пропускает roots с connectedWorldHidden: FapClinicNearBirch и четыре KaraMixedMass не получают поздние BranchSkeletonLOD после замены постановочными группами. Пустой Node3D прежде сохранял видимость, поэтому старые деревья возвращались поверх нового слоя. Общий HidePresentationNode не менялся; коллизии и игровые потомки не затронуты. Сборка — 0 ошибок/предупреждений; четыре native кадра `foliage_suppression_frames` захвачены и просмотрены. Лишняя развилка возле тёплого окна исчезла. Повторяемость елей и художественная приёмка остаются открытыми.

### 2026-09-12 — табурет ФАПа, осока и воздушная перспектива леса

Сиденье медицинского табурета стало округлым диаметром .50 м, верх — .60 м от authored пола; ножка соединяет сиденье с основанием. Имена и количество объектов сохранены. Генератор/self-validate: 405 мешей, 12 214 треугольников, 28 интерьерных LOD-пар; native передний и левый ракурсы `prop_detail_v2_frames` просмотрены. Осока использует прежние позиции, но горизонтальный масштаб уменьшен до .62, вертикальный до .78 от прежнего; native ракурс Тимура проверен.

В действующем connected-профиле Кара-Урмана плотность тумана .009, aerial perspective .78; освещение не изменено. Три native кадра `household_fog_v2_frames` просмотрены: дальние стволы мягче по контрасту, путь и тёплое окно различимы. Повторяемость крон и полная художественная приёмка леса ещё открыты.

### 2026-09-12 — сухое дерево у западной кромки

Грубая развилка `KaraClosureMidBroadleafWest` заменена производной Quaternius DeadTree_3 (CC0). Исходные glTF/bin/карты, лицензия и SHA архива сохранены в `assets/source/quaternius/stylized_nature`; генератор `tools/blender/prepare_winter_dead_tree.py` создаёт локальные Blender и GLB без сети. Высота источника нормализована до 1 м, основание на нуле; runtime ставит дерево прежней высоты 8 м. Три LOD: 5802 / 2901 / 1160 треугольников, единый материал коры, без runtime-карт и коллизий. Сохраняются существующие road/roof/ground guards. Три native кадра `winter_dead_tree_native_frames` просмотрены: заменена именно высокая чёрная развилка, окно и путь видны. Общая художественная приёмка леса открыта.


### 2026-09-12 — сохранение силуэта дальних елей

Light/far варианты сохраняют все 12 высотных ярусов существующей кроны. Упрощаются сечения ветвей: три продольные станции и пять/три стороны; у двух верхних дальних ярусов остаётся одна ветвь. Ближний вариант неизменен. Бюджеты соблюдены: near 4004, light 1136, far 388 треугольников; весь kit — 218 мешей / 74 878 треугольников / 15 материалов. Native LOD-диапазоны, размещение и коллизии не менялись.

Blender со встроенными проверками и Godot import прошли. Три native кадра `spruce_full_levels_frames` сопоставлены с `winter_pine_alpha_frames`: большие пустоты между дальними ярусами уменьшились, дорожка и тёплое окно остаются видны. Сравнение изолирует геометрию елей; отдельный Pine alpha A/B в обоих наборах одинаков и ещё не принят. Полная художественная приёмка и разнообразие леса остаются открытыми.


### 2026-09-12 — сосны с сохранённым силуэтом хвои

Три существующих authored места Кара-Урмана используют Quaternius Pine_3 (CC0): ближнее западное, среднее восточное и дальнее западное. Исходники, три карты и лицензия сохранены; `prepare_winter_pine.py` проверяет SHA и исходную геометрию. Runtime GLB нормализован до 1 м; LOD 4964 / 2713 / 1375 треугольников сохраняют все 462 треугольника хвои и UV, упрощая только кору.

Маска хвои 2048×2048 использует исходный UV и Alpha Scissor .2. Цвет, снег, ветер и графический профиль берутся из существующего PainterlyMaterialLibrary через отдельный cached cutout-вариант того же shader; непрозрачные материалы не изменены. Для двусторонних карточек снег учитывает сторону поверхности. Устаревшее позднее скрытие NearConiferWest удалено; road/roof/ground guards сохранены.

Генератор со встроенными проверками, Godot import и C# build прошли. Четыре native ракурса `winter_pine_production_frames` просмотрены: сосны видимы, летняя зелень заменена зимней палитрой, центральный проход и тёплое окно читаются. Registry включает GLB и извлечённую Godot маску. Это локальное улучшение разнообразия силуэтов; общий художественный уровень леса ещё не принят.


### 2026-09-12 — корни принадлежат существующим деревьям

Дорожный фильтр `AddVisualTree` пропускал четыре из восьми ближних деревьев Кара-Урмана, а их корни всё равно создавались и выглядели как чёрные клинья на снегу. Построение корней теперь проверяет наличие дерева. Native луч из пикселя (1510, 800) фронтального кадра до исправления попадал в `KaraSlopeRoot4_1Base`, после — только в Terrain_Main. Оставшиеся деревья 1, 3, 5 и 7 видимы. Три кадра `kara_root_ownership_frames` просмотрены; диагностика удалена из runtime capture. Общая художественная приёмка леса открыта.


### 2026-09-12 — окно дома без остатков старых штор

`CurtainLeft` и `CurtainRight` включены в существующий список примитивов, заменённых авторским интерьером. Две светлые прямоугольные полосы над новым окном исчезли; коллизии не менялись. C# build и два native кадра `house_curtain_cleanup_frames` прошли проверку; оба ракурса просмотрены.

### 2026-09-12 — зимняя иллюстрация меню

Главное меню использует оригинальную ImageGen-иллюстрацию Кырлая: резные деревянные дома, тёплые окна, зимняя тропа и лес. Точный prompt, происхождение и SHA сохранены в `game/assets/textures/ui/README.md`; изображение 1672×941 включено в registry и обязательные зависимости следующего native package. Native меню и титры проверены в 720p/1080p при обычном и увеличенном тексте; прокрутка сохраняет доступ к пунктам. Это иллюстрация меню, а не игровой кадр: художественная приёмка самих локаций остаётся открытой.

Та же PNG теперь назначена штатной заставкой загрузки в `project.godot`, с тёмным фоном из палитры меню. Используются настройки движка для сохранения пропорций и линейной фильтрации; искусственное минимальное время показа остаётся нулевым. Проверка текущим Godot 4.7.1 подтверждает путь, декодирование 1672×941 и настройки. Это оформление старта, не отзывчивый индикатор загрузки и не исправление задержек кадров. Появление заставки при обычном запуске требует проверки после разблокировки Mac; evidence находится вне checkout в `URMAN_ActI_Finish_20260911/boot_branding_r18`.

Значок приложения — отдельная оригинальная ImageGen-композиция: тёплое окно между заснеженными хвойными деревьями. `urman_app_icon_v1.png`, 1254×1254, подключён через общий `application/config/icon`; desktop-пресеты наследуют его штатным способом. Точный prompt и происхождение находятся рядом с PNG в README, SHA — в registry. Это образ игры, не новое каноническое место; портретный UI-аватар не используется вместо эмблемы. Исходное изображение просмотрено root/Luna. Из R19 извлечены настоящие ICNS PNG 32–1024 px и Windows RT_ICON PNG 16–256 px; root просмотрел уменьшенные версии. Все шесть Windows-ресурсов отличаются от стандартной иконки R18. При 16–32 px детали становятся светлым окном и силуэтами леса; проверка этих файлов не заменяет показ в Finder или запуск Windows. PNG сохранён с исходным alpha-каналом. Evidence: внешний `URMAN_ActI_Finish_20260911/app_icon_r19`.

### 2026-09-12 — остаточная ветка старой сцены

Чёрный оторванный конус в кадре Кара-Урмана оказался второй веткой `StyleBenchmarkZone.MakeBranch`, вне `Act1CoreWorldGreybox`. При одинаковых именах Godot переименовывал её в `@MeshInstance3D@…`, поэтому существующее скрытие `ForestBranchSilhouette` и числовых суффиксов её пропускало. `AddChild` теперь сохраняет читаемый числовой суффикс. Native луч по всей сцене до исправления попадал в старую ветку в 5.14 м; после — только в дальние деревья. Три `kara_branch_name_frames` просмотрены, чёрный фрагмент исчез. Stand21 и общие шейдеры леса не менялись; первоначальная гипотеза об их дефекте отвергнута. Временная диагностика удалена.

### 2026-09-12 — домашний платок Гөлсинә

Существующий character kit получил один Hat LOD pair: прилегающую корону, ткань затылка, узел и короткие концы в одном меше, привязанном к Head. Материал — существующая ткань без снега, светлый льняной оттенок. Сохраняются девять скелетов и Idle/Tension; источник и GLB пересобраны, registry hashes обновлены. Source verifier подтвердил 190 пар LOD, отдельная диагностика — пять замкнутых компонентов с положительным signed volume. Native боковой ракурс и поворот при авторской реплике просмотрены root и Luna: оторванной геометрии и заметного clipping нет. Спереди деталь всё ещё частично читается как низкая шапка; возраст лица и общая художественная приёмка персонажа остаются открытыми.


### 2026-09-12 — просвет в дальнем лесном плане

В существующем ряду Кара-Урмана `KaraWatershedStand11` смещён с X=-1 на X=-8.5 при прежнем Z=-145: он присоединяется к западной группе, открывая узкий просвет на оси подхода. Имя, высота, LOD и материал сохранены; это дальняя визуальная посадка за пределами маршрута, без изменения terrain или взаимодействий. Native `kara_approach_depth`, `kara_warm_window` и `kara_profile_front` из `URMAN_ActI_Finish_20260911/kara_composition_reveal_frames` просмотрены. Центральный просвет читается, окно и профиль сохранены; повторяемость всей лесной массы и общий арт остаются открытыми.


### 2026-09-12 — поза Алсу в существующем character kit

Пересобраны рукава, манжеты и кисти Алсу для спокойной позы со сложенными перед курткой руками. Источник и GLB обновлены вместе с SHA в registry; 9 rig, 190 LOD-пар и текущие клипы сохранены. Native общий и разговорный кадры просмотрены основным агентом и Luna; видимого clipping нет. Гөлсинә сохраняет прежнюю голову: оба исследованных возрастных кандидата отклонены и остались во внешних материалах.

### 2026-09-12 — разреженные кроны у конца тропы

Четыре существующих `KaraShelteredRegrowth6–9` используют уже импортированный
`WinterPine` вместо сплошных ярусных елей. Позиции, высоты 3,1–5,2 м, grounding,
road-envelope guard и три LOD сохраняются. Новые ассеты/коллизии не добавлены.
Центральный владелец Regrowth6 установлен лучом из фактической финальной камеры
в пикселе (1060,350), а не одним наличием узла в сцене.

Native A/B `kara_spruce_owner_probe_v3` → `kara_regrowth6_pine_trial` →
`kara_regrowth_cluster_pine_trial` во внешнем каталоге
`/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911`: четыре открытые кроны
разбивают одинаковый ряд и дают просветы между стволами. Root и Luna просмотрели
сравнение; root дополнительно проверил depth/profile из
`kara_regrowth_cluster_views` (4 native-ракурса, C# build PASS). Нижние стволы
сосен остаются редкими; общий арт и повторяемость крупных елей ещё не приняты.

### 2026-09-12 — силуэт дворовых салазок

Существующий `BabaiYardSled` теперь имеет загнутые деревянные носы полозьев,
короткие опоры, четыре рейки сиденья и открытую реечную спинку. Снег лежит
отдельными полосами; синяя ремонтная перекладина опирается на верх сиденья
(0,4475 м в локальных координатах). Новые ассеты, материалы и коллизии не добавлены:
использован прежний C#-владелец `AddVisualSled` и общий `AddVisualBox`.

Первый вариант с длинными носами отклонён: слишком близко к соседней доске.
V2 сохраняет прежнюю глубину около 1,2 м, anchor и grounding через mesh bounds.
Native 1080p before/after во внешнем `sled_shape_v2` просмотрены root/Luna;
реальный `InteractionTarget.Interact` сохраняет запись и открытый вид находки.
C# build: 0 ошибок и предупреждений; временный capture восстановлен, userdata byte-for-byte.
Это локальное улучшение узнаваемости санок; общая арт-приёмка остаётся открытой.

### 2026-09-12 — форма брюк Мансура и Рината

В существующем генераторе только `Mansur` и `CouncilWitness` получили пять
колец брюк: объём бедра, сужение колена и икру. Сохранены верхняя и нижняя
точки, обувь, bone attachment и прежние имена. Обе LOD пересобраны; набор
остаётся из 9 rigs и 190 пар мешей, 32 314 треугольников (+94).

Native 1080p `trouser_shape_before` → `trouser_shape_v1` и отдельные боковые
кадры `trouser_shape_v1_side` во внешнем каталоге завершения просмотрены root
и Luna: форма читается лучше, видимых щелей у обуви и резких перегибов нет.
Существующий `verify_character_kit.py` PASS; сравнение исходного/нового Blender
подтвердило изменение только 8 trouser-мешей с LOD, остальные 372 меша,
их геометрия, basis-transform и bone attachment совпадают. Импорт и C# build
PASS; временный capture восстановлен, userdata сохранены byte-for-byte.
Длинная треугольная форма шеи остаётся отдельным недостатком; общая приёмка
персонажей этим локальным изменением не закрыта.

### 2026-09-12 — задняя часть мужской шеи

Владелец исправления — `prepare_character_heads.py`: нижний задний участок
мужского CC0-шаблона плавно сужается по X/Y к существующему воротнику.
Убрано широкое треугольное расширение за затылком; высота, передняя часть лица,
подбородок, волосы, воротник и скелет не смещались. Полный размер и общая
длина стилизованной головы остаются предметом художественной приёмки.

Сравнение `trouser_shape_v1_side` → `neck_shape_v2` (native 1080p, root/Luna)
показывает уменьшение выступа без видимого шва. Root также просмотрел четыре
кадра `neck_shape_v2_front`: Ринат, Мансур, Тимур и житель сзади сбоку.
Существующая проверка персонажей, импорт, C# build PASS; временный capture
восстановлен, userdata byte-for-byte. Blender-сравнение: изменились только
12 мужских head-мешей с LOD; остальные 368 совпадают. Для всех шести мужских
LOD0 отдельно подтверждены неизменные Z-координаты и вершины вне нижней
задней области. Число треугольников прежнее — 32 314.

### 2026-09-12 — неровные уровни зимней ели

`winter_spruce_variant` использует прежние 12 уровней, но ветви имеют более
разную длину (нижняя граница множителя 0,62 вместо 0,88) и индивидуальное
смещение высоты до ±2,2% дерева. Один seed каждой ветви сохраняется во всех
LOD. Снег следует тем же опорным точкам; материалы, корни, размещение и число
треугольников не менялись. 218 мешей / 74 878 треугольников; Blender-сравнение
подтвердило изменение только 12 spruce needle/snow-мешей, остальные 206 совпали.

Внешний `spruce_profile_owner_r13` установил пикселем (1450,650) владельца
ближней кроны — `KaraShelteredRegrowth3/BranchSkeletonLOD0`. Native 1080p A/B
`kara_profile_world_view_r12` → `spruce_shape_v1` просмотрен root/Luna: появились
просветы, сплошной конус стал менее регулярным; отрыва снежных шапок не видно.
Root также просмотрел 3 кадра с дороги `spruce_shape_v1_route`. Встроенные
проверки генератора PASS; импорт/build PASS, native road foliage проверил
1 489 962 нижние вершины без пересечения дороги. Временный capture восстановлен,
userdata byte-for-byte. Это улучшение формы ели, не общая художественная приёмка;
повторяемость крупных деревьев в глубине леса ещё заметна.


### 2026-09-12 — пропорции головы без вертикального растяжения

В `generate_character_kit.py` импортированные Head/FaceEyes/FaceBrows/Hair/FaceBeard
используют одинаковый `head_scale` по X/Y/Z. Прежний XY-only масштаб сужал
лица, сохраняя высоту: особенно заметно у Алсу (.84) и Рината (.85). Теперь
уменьшение сохраняет пропорции исходника. `head_z`, шапки, воротники, anchors,
риги, позы и коллизии не меняются; male posterior taper источника сохранён.

Native A/B: `head_proportion_before` — два свежих женских baseline; мужской
baseline — `neck_shape_v2_front`; `head_proportion_v1` — шесть тех же ракурсов.
Root просмотрел все шесть, Luna — пять основных сравнений: лица короче,
видимых разрывов у волос/воротников нет. Подозрение на зазор у шапки Тимура
не подтвердилось: база шапки перекрывает макушку на 17,2 мм и волосы на
20,9 мм. Шапку не двигали. Это локальная коррекция, не общая приёмка персонажей.

Blender сравнение: 80 мешей изменены, 300 прочих совпадают, XY LOD0 совпадают
с допуском 1e-7 для float. 9 rigs, 190 LOD-пар; 21 551 LOD0 + 10 767 LOD1 =
32 318 треугольников. Четыре добавочных треугольника LOD1 — результат прежнего
Decimate на изменённой форме. Существующий character-asset-smoke, импорт и
C# build прошли; временный capture восстановлен, userdata byte-for-byte.
Source BLEND/GLB и registry обновлены. Авторский контент и fingerprints R13
не менялись; новый пакет с этой геометрией ещё нужен.

### 2026-09-12 — два различных силуэта в глубине Кара-Урмана

`KaraWatershedStand10` (-6, -139; 11,8 м) и `KaraWatershedStand20`
(-5, -150; 13 м) используют существующий WinterPine вместо WinterSpruce.
Меняется только прежний `winterVariant` override; координаты, высоты, поворот,
LOD, grounding и ограничения вдоль дороги сохраняются. Это два дальних
силуэта в повторяющемся центральном ряду, а не новая посадка деревьев.

`kara_background_pines_v1`: пять native ракурсов сравнены root/Luna с
`spruce_shape_v1_route` и `spruce_shape_v1`. На подходе, особенно в глубине,
пористые кроны дают различимый разрыв одинакового ряда; два ракурса профиля
почти не меняются. Проход и тёплое окно остаются открытыми. Log подтверждает
1 489 962 проверенные низкие вершины без вторжения стволов/ветвей в дорогу;
75 490 collision samples, gap 0,0016–0,0487 м. Это waypoint-проверка
представления, не новое прохождение. Build PASS, userdata byte-for-byte.
Общая художественная приёмка леса и интерес исследования остаются открыты.


### 2026-09-12 — посадка брюк Алсу под узким пальто

В исходном native-кадре верхние торцы брюк выступали по бокам пальто:
ноги читались как отдельные стойки. Только у Алсу два верхних кольца брюк
смещены внутрь (множители leg_offset .48/.78); ниже использован существующий
пятикольцевой профиль колена и икры. Ступни, голеностоп, кости, пальто, поза
и остальные персонажи сохранены. Новых материалов или частей тела нет.

Свежие native front/side A/B: внешний `alsu_hip_before` → `alsu_hip_v1`.
Root и Luna просмотрели оба ракурса: торцы скрыты под пальто, открытого стыка не видно,
ноги имеют непрерывный силуэт. Это локальная правка, общая приёмка персонажей
остаётся открытой. Blender comparison: изменены только четыре Alsu Trouser
LOD-меша, 376 других совпадают. 21 579 LOD0 + 10 779 LOD1 = 32 358 треугольников;
9 rigs / 190 LOD-пар. Character-asset-smoke, import, registry и C# build PASS.
Временный capture восстановлен, userdata побайтно восстановлены.


### 2026-09-12 — деревья обрамляют задний вид на минарет

Ближайшая existing WinterBirch_2 в (-33,82144; -12,23175) повёрнута на +90°
в AgentBAct1ExteriorLayer до проверки низких ветвей и дороги. Все LOD
поворачиваются вместе; положение и масштаб прежние. Existing
ArrivalClosureForwardFarWestBroadleaf в (-35; -15) имеет высоту 5,2 м вместо
7,4 м. Геометрия источников, минарет и коллизии маршрута не менялись.
Native `minaret_view_before` → `minaret_view_v1` показывает свободную верхнюю
часть ориентира; arrival_forward также просмотрен. Подробности — в gameplay.md.


### 2026-09-12 — кора и срезы поленницы

Runtime dump `firewood_before` подтвердил: у MainStreetPhysicalFirewood
материал URMAN_Bark_Muted использовал `bark_pine` без texture descriptor,
а оба торца каждого бревна были назначены тёмному материалу досок
URMAN_Wood_Dark в исходном village kit. Это общая поленница, также
используемая во дворе бабая и других существующих размещениях.

Texture lookup `bark_pine` теперь переиспользует имеющийся descriptor
`wood_bark` / bark_pine_v1_albedo.png, сохраняя зимние параметры pine,
включая снег. URMAN_Bark_Muted получает коричневый тон 715943. Шесть
исходных брёвен имеют отдельный материал торцов URMAN_Wood_CutEnd и UV
поперечного сечения. В существующем Painterly shader только `wood_cut`
получает мягкие неровные годовые кольца; при размере меньше пикселя
контраст колец затухает через fwidth. Новых текстур, геометрии или
постобработки нет. Ступени, NPC, коллизии и позиции поленниц прежние.

`firewood_v4` содержит текущие native1080 close/arrival кадры; root и Luna просмотрели
оба. Кольца читаются вблизи и не дают заметной сетки с дальней точки.
`firewood_v3_context/kara_approach_forward` дополнительно проверяет общий
texture alias на лесном ракурсе; дальнейшая V4 меняет только затухание
колец. V1 отвергнут из-за потери снега и рисунка досок на торцах, V2 —
плоские срезы, V3 — заметный мелкий рисунок вдали. Это проверка статичных
ракурсов, не приёмка temporal stability в движении или всей художественной цели.

Blender source comparison: геометрия и transforms всех 725 мешей совпадают,
21 845 треугольников; только шесть material slots брёвен изменены, на них
добавлены UV торцов. Материалов 16 вместо 15. Встроенная проверка генератора
прошла, включая существующий woodpile contact/размеры и назначение срезов.
Import, C# build, native capture и registry preflight PASS. Временные
capture-вставки восстановлены с повторной сборкой; userdata побайтно
восстановлены. Первый внешний comparison helper имел ошибку изменяемого
списка names при Blender library load; исправленный helper и V3 сравнение
прошли, исходный неуспешный лог сохранён.


### 2026-09-12 — пропорции исследуемого указателя на первой улице

`BuildMainStreetSignDiscovery` использует прежние runtime-примитивы: доска
1,15×0,46 м уменьшена по высоте до 0,28 м, стойка 0,16×1,95×0,16 м —
до 0,11×1,80×0,11 м. Её существующий физический box уменьшен синхронно.
Центр доски 1,68 м, ширина, anchor, область взаимодействия, шрифт и логика
поворота прежние. Полоса старой краски на обороте сдвинута внутрь новой
доски (local y0,17→0,10). Стрелки обеих сторон теперь окрашены как соседние
надписи; прежний тёмный цвет общего DiscoveryLabel сливался с деревом.

Luna выбрала этот предмет по текущему кадру и проверила owner; root и Luna
сравнили native720 `arrival_sign_before/frames` → `arrival_sign_v1/frames`:
`arrival_forward` и `sign_reverse_used`. Указатель меньше закрывает дом,
стрелки различимы; текст оборота и полоса помещаются. Второй кадр использует
существующее действие открытия и демонстрирует повернутую доску. C# build0/0,
existing capture2/97 PASS, userdata восстановлены побайтно. Это локальная
приёмка предмета, не обычное прохождение и не общий художественный lock.
Source ассеты, материалы библиотеки, количества мешей/треугольников и
fingerprint не менялись; отдельный ассет или тестовый набор не добавлены.


### 2026-09-12 — скамья в ФАПе на уровне человека

У `FapInteriorBench_Back` высота панели уменьшена с 0,92 до 0,50 м,
центр опущен с 1,16 до 0,90 м. Верхняя планка перенесена с z=1,68 на 1,17 м
и совмещена со спинкой по глубине (y=−2,63): прежний зазор 0,15 м устранён.
Верх всей спинки теперь 1,23 вместо 1,74 м. Сиденье, ножки, стойка, Наиля,
коллизии и interaction targets сохранены. Изменены только три source-меша
(спинка LOD0, планка LOD0/LOD1); остальные 402 геометрии/трансформации совпали.

Нативные четыре ракурса комнаты до/после просмотрены root и Luna:
боковой и обратный вид свободнее, планка соединена со спинкой. Отдельный
захват исходного входного ракурса диалога подтвердил уменьшение именно
правой тёмной массы; более близкий контрольный forward всё ещё показывает
другую крупную деталь стойки. Общая композиция и художественная приёмка
ФАПа остаются открытыми. Evidence: внешние `fap_bench_before`, `fap_bench_v1`.

Blender validate, import, registry PASS; временный UI capture восстановлен,
C# build 0/0, userdata побайтно восстановлены. Реестр синхронизирован с
фактическим источником: 405 мешей, 12 214 треугольников; интерьер 197 LOD0
и 28 LOD1, 5 532 + 488 треугольников. Сюжетный fingerprint не менялся.


### 2026-09-12 — ткань медицинской ширмы

Три панели `FapInteriorScreen_Panel*` в активном материальном владельце
ФАПа получают существующую семантику `cloth` с приглушённым синим оттенком
`5f7a83`. До этого общая привязка `FapPaintedSage/DustyBlue` назначала им
штукатурку. Используется готовая `old_fabric_v3_albedo.png`; геометрия,
деревянные рамы, соседние предметы и стены не изменены.

Два native 1080p ракурса (forward/left) просмотрены root: у панелей читается
тканая фактура, отличная от стен. C# build 0 ошибок/предупреждений, существующий
capture прошёл; userdata восстановлены побайтно. Evidence:
`URMAN_ActI_Finish_20260911/fap_screen_cloth_v1`. Это локальное исправление
материала, не общая приёмка интерьера или производительности.


### 2026-09-13 — свет разговорного плана Наили

Существующий `FapNailaPractical` перемещён из (3,70; 2,05; 0) в
(2,65; 2,15; 1,05): источник теперь ближе и перед лицом. Энергия .76 → .90,
радиус 3,0 → 2,8 м. Цвет, тени, число источников и общий свет комнаты прежние.
На близком кадре лучше читаются лицо и воротник; правая деревянная поверхность
также стала светлее. Подсветка документов и холодный фон сохраняются.

Root просмотрел три одинаковых native 720p ракурса до/после: новый временный
разговорный ракурс с дистанции около 1,5 м и существующие forward/left.
Позиция/направление камеры подтверждены capture receipt. Временный код захвата
восстановлен побайтно, C# build 0 ошибок/0 предупреждений; userdata сохранены
и восстановлены. Evidence: внешний `fap_conversation_light_v1`.
Это локальная работа со световым акцентом. Общая художественная приёмка,
внешний вид мебели/персонажей и производительность остаются открытыми.


### 2026-09-13 — контакт служебной полки со стойкой ФАПа

Native AABB и проекция уточнили владельца коричневой перекладины справа
в разговорном кадре: это `FapInteriorReceptionCounter_ServiceShelf`, а не
столешница стойки или верхняя рейка перегородки. Полка была без опор:
нижняя грань Y=1,43 м, столешница заканчивается на Y=1,02 м.

В Blender-источнике центр полки Z=1,035 м, толщина 0,03 м, bevel 0,008 м.
Теперь она лежит на столешнице как низкая служебная полочка. Положение
самой стойки, её правдоподобная шестисантиметровая столешница и проходы
сохраняются. Изменены только два меша ServiceShelf LOD0/LOD1; остальные
403 меша прежние. Число треугольников прежнее — 12 214.

BLEND/GLB пересобраны, SHA реестра обновлены; validator и registry PASS.
После Godot import реальный gap между обоими LOD и стойкой равен 0.
Root просмотрел три native 720p ракурса: близкий, forward, left; полка
больше не висит в воздухе на уровне разговорного плана. Временный capture
восстановлен побайтно, C# build 0/0, userdata восстановлены побайтно.
Evidence: внешний `fap_service_ledge_v1`; исходный световой A/B —
`fap_conversation_light_v1/after`. Общая художественная приёмка открыта.

### 2026-09-14 — персонажи переведены на скин, клипы Idle/Tension заработали

Проверка пункта M6 «NPC вблизи и в движении» довела вопрос до скелета и вскрыла
дефект конвейера: меши персонажей были привязаны к костям (`parent_type="BONE"`)
без скина, поэтому glTF не содержал `skins`, а Godot импортировал анимацию
дорожками с именем кости (`Alsu_Rig/Skeleton3D:Spine`), которые не двигают ни позу
кости, ни глобальную позу, ни видимый меш. Данные в клипах при этом были (4 канала
из 24 с реальным движением), то есть анимация существовала и не доходила до сцены.

Правка в `tools/blender/generate_character_kit.py`: вместо привязки к кости каждый
меш получает одну группу вершин с именем своей кости и весом 1,0 на все вершины,
объектом родителем становится риг, и добавляется Armature modifier
(`URMAN_CHARACTER_SKIN`); политика записана в .blend как `skin_policy`. Логика
выбора кости (включая независимое запястье CouncilWitness) не менялась.

Проверено: `eng/verify-assets.sh` PASS — 9 префиксов, 271 LOD0/271 LOD1,
542 жёстких скин-привязки, клипы Idle/Tension, отсутствие коллизийных мешей;
реестр обновлён (источник и производный GLB). `verify_character_kit.py` переведён
на контракт скина: ровно одна группа вершин, вес 1,0 у всех вершин, правильная
кость у кистей, объект-родитель — риг; строгость не ослаблена. `SceneSmokeTest`
требует, чтобы все меши персонажа были скинены и чтобы клип Idle нёс движущиеся
каналы (`skinned=542/542 movingChannels=4`). Движение в игре подтверждено кадрами
харнесса: между фазами 0/40 в области персонажа различается 16,7 % пикселей против
1,5 % фона и 0 % неба — лист
`docs/production/urman_visual_review_pack/21_npc_clip_motion_after_skin.png`.

Ограничение: smoke-сцена собирает зону вне дерева, поэтому `AnimationPlayer` в ней
не тикает, и поведенческая проверка проигрывания там невозможна — контракт в smoke
проверяет ассет, а проигрывание подтверждается кадрами. Общая художественная
приёмка анимации (насколько выразительно движется человек) остаётся человеческой.

### 2026-09-14 — шесть NPC визуально проверены после перехода на скин

Скин меняет структуру: меши перестали быть детьми костей и стали скиненными
мешами скелета, а клип Idle теперь двигает Spine/Head/Arm.L/Arm.R. Поэтому
помимо метаданных и счётчиков нужна была визуальная проверка: если бы привязка
была неверной, части тела разъехались бы, как только клип начал играть.

Сняты все шесть близких кадров состава на текущей сборке
(`alsu_close`, `gulsina_close`, `naila_close`, `mansur_close`, `rinat_close`,
`timur_close`, 1280×720, реальный Metal). Все шесть целы: голова, руки, одежда и
обувь на месте, ничего не смещено и не оторвано. Лист —
`docs/production/urman_visual_review_pack/24_cast_after_skin.png`.

Отдельно повторена уже известная ловушка: в контакт-листе кадр Гөлсинә выглядит
мелким и тёмным, но в 1:1 она занимает центр от головы до обуви, видно платок,
лицо, кисти и лист бумаги в руках. Это подтверждает прежнюю запись: судить кадры
состава нужно в полном разрешении, а не по уменьшенному листу.

## 2026-09-29 — библиотека анимаций Quaternius UAL для каталога движений Studio

- `game/assets/animations/ual1_standard.glb` — неизменённая копия Quaternius
  Universal Animation Library [Standard] (CC0 1.0), 43 клипа на том же
  65-костном скелете, что и `urman_character_kit_v2`; запись
  `animation.library.ual1-standard` в `assets/asset_registry.json`.
- Каталог движений — `game/content/animations/catalog.v1.json` (31 движение,
  8 категорий); исполнение — `game/scripts/AnimationCatalog.cs` (перенос дорожек
  на скелет персонажа, корневое смещение отключено). «Указать рукой», «указать
  вверх» и «нести перед собой» — варианты из ближайших клипов библиотеки,
  «повернуться» — процедурно; помечены в каталоге.
- Не проверено человеком: художественное качество движений на каждом персонаже,
  пересечения одежды в движении (в позе «сидеть» у Resident полы кафтана
  проходят сквозь ноги).


### 04.10.2026 — библиотека бытовых звуков

13 небольших клипов mono PCM16 24 kHz, всего 2 933 280 PCM байт на диске;
плюс существующие фоли. Runtime загружает до 8/2 МБ; не весь банк.
CC0 public-preview источники/хеши и derived CC-BY-SA credit музыки:
`game/assets/audio/act1/village_life/credits.json`. Часы/веник/машинка —
project-original mechanical Foley; прослушивание external/not-run.
Окна используют текущую ImageGen-карту инея с alpha без новых текстур.
Подробности: [бытовой слой](../production/village_life_2026-10-04.md).

### 2026-10-04 — Деревянная мечеть и чистые дворы

Новые процедурные реальные mesh/contact consumers: узкие деревянные секторные ступени, центральная стойка, винтовые перила, восьмигранная дощатая башня, компактный остеклённый фонарь азанчи, металлический шатёр, полумесяц, микрофон/стойка/кабель. Материалы используют существующую библиотеку фактур; новые растровые повторы не создаются. Фотографии пяти деревень — исследовательские ссылки, не импортированные игровые текстуры. `docs/production/village_repair_2026-10-04/references.md` содержит сравнение.

В занятых зимних дворах убрана повторная россыпь веток/мха/осоки исходных parcel kits и остатки старой ограды, когда актуальная граница уже принадлежит `YardFences`. Рабочие дрова, сараи, лавки, механизмы/предметы/подсказки сохраняют назначение; старые мусороподобные повторы не заменяются новыми уникальными ассетами.

Финальный native-кадр04.10: горизонтальные стыки досок зала и вертикальные стыки минарета — один mesh `MosqueTimberBoardJoints`, существующий материал древесины, без контакта и новых текстур. Снимки/проверки: `docs/production/village_repair_2026-10-04/report.md`. Очистка дополнена низкими кустовыми фрагментами и камнями старой ограды; реальные фундаменты/дверные ступени сохраняются.


## 2026-10-04 — сельский опорный пункт

Consumer far-police: доступная метровая оболочка, пол/потолок и реальные проёмы; приёмная со стеклом/столом/телефоном/бумагами/кружкой, коридор и пустой изолятор с открытой решёткой, лавкой, сложенным одеялом, тазом, мышью и картошкой. Consumer PoliceDutyOfficer: существующий CC0 human-v2 Resident и UAL сидение, navy cloth uniform, фуражка и погоны на костях. Consumer DistrictZhiguli: проектная процедурная модель четырёхдверного классического седана; две точные надписи «УЧАСТКОВЫЙ», спущенное колесо, снег, потёртые пороги, без Fleet/RigidBody/управления. Материалы переиспользуют имеющиеся карты cloth/metal/wood_furniture/plaster/paper/snow; прозрачное стекло физического стекла — собственный PBR. Новых растров и уникальных текстур на предмет нет. Формат: runtime ArrayMesh/примитивы+существующий glb; реальные метры, существующая UV/проекция материала. Художественная оценка — по нативным кадрам; никакая историческая фотография не заявлена импортированной моделью.


### Уточнение автора 04.10.2026 — реалистичные готовые ассеты

Автор отклонил процедурный прототип участка как нереалистичный. Для Жигулей, мебели и подходящего реквизита сначала искать и скачивать авторские свободные 3D-модели с разрешением коммерческого использования и распространения в игре. Сохранять источник, автора, лицензию, хеш и изменения в паспорте; проверять реальную форму, масштаб, материалы и ресурсный бюджет. Наличие бесплатного скачивания само по себе не подтверждает права. Заменить упрощённые модели и проверить результат в движке; прежние кадры участка остаются кадрами отклонённого прототипа, художественная готовность не подтверждена. Неподвижность автомобиля, надпись «УЧАСТКОВЫЙ», свободные проходы и бытовые реплики сохранить.
