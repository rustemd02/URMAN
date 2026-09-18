# Act I OpenCode Execution Ledger

## Current work card — итерация 2 по фидбеку: плотность деревни, границы-лесополоса, UI-полировка

Фидбек пользователя: «деревня пустовата; непонятно как сделаны границы — невидимая
стена? некрасиво; мало растительности; UI уже похож на архив, но можно стильнее».
Сделано: (A) плотность — детерминированный (seed 20260910) densify-план в
`AgentBAct1ExteriorLayer.BuildDensifiedPlan`: по 2–4 сателлита вокруг каждой из 74
авторских точек, обочинная полоса вдоль всех дорог (сетка 2.2 м, отбор по конверту
дороги 0.45–2.7 м, шаг между растениями 1.7 м), огородные грядки у дома бабая
(1.35 м сетка), двор ФАПа, палисадники жилой полосы (|x|>7), трёхрядный
периметровый пояс (шаг 2.5 м) и rim-лес на поднимающихся ободьях
(x≈±(44..62), z≈-128..-150) — горизонт замыкается лесом. Границы: пояс+rim дают
видимый лесной край вместо пустого поля. Генерируемые точки фильтруются по
конверту дороги молча (авторские по-прежнему fail-closed); счётчики меты
синхронизированы (plannedFoliageEntryCount = плотный план) — падение
Act1DemoLaunchSmokeTest поймано и устранено. Меши мира: 14 372 → 26 247 (1.8x),
benchmark держит 120 FPS (floor 30). (B) UI — журнал: иерархия (заголовок 25 px
охры), тёплые чернильные цвета тела/источника, нумерация записей «01 · Название»,
состояния списка (selected/hover/line_separation); унифицирован охряной акцент
панелей в MainMenu/Pause/Settings; UI-матрица перегенерирована (10 PNG в
evidence/act1_prod_ready/ui_matrix/, opt-in URMAN_UI_SHOT_DIR).
Верификация: build 0/0; walkthrough PASS 335,27 м; demo-launch PASS;
capture 48/48; verify-godot exit 0; benchmark 120 FPS; git diff --check чистый.
Пары: evidence/act1_prod_ready/iteration2_density/ (итерация 1 → 2) и
обновлённые final_before_after/ (baseline → финал).
Статус: IN_GAME_VERIFIED.

## Previous work card — prod-ready plan: Фаза 6 исполнена, аудит доведён, финальная заморозка повторена

Дополнение к карточке ниже после верификатора. Фаза 6 (Blender-обвесы):
`author_rural_dwelling` в `urman_village_exterior_kit.py` дополнен тёмным
коньком-брусом (RidgeBeam) и кирпичной трубой с колпаком на 3 из 4 dwellings
(hero + VariantB/C; VariantA — без, по принципу «на части домов»); наличники/
свес 36 см/толщина крыши/уголки уже были в ките. Регенерация тем же
генератором (blend+GLB), registry-хэши обновлены, verify-asset-registry
PASS 55/55, manifest обновлён (715 мешей / 20 670 tris / 16 материалов).
Одна итерация: первый конёк был сориентирован поперёк ската («антенны» над
фронтоном) — переориентирован вдоль. Фаза 8 (полный аудит): все 48 кадров +
интерьеры просмотрены; дополнительно закрыты D5 (бледная головка колодца
двора — адресный тон через scoped-tint) и D6 (бледная лента ворот —
тёплая ткань fabric_pattern с чит-текстурой). D4 (конёк) закрыт.
Финальная заморозка повторена на финальном состоянии: build 0/0;
walkthrough PASS 335,27 м; capture 48/48; benchmark 119–120 FPS (floor 30);
verify-asset-registry PASS; git diff --check чистый. Финальные пары
обновлены: evidence/act1_prod_ready/final_before_after/. UI-матрица
Фазы 9: evidence/act1_prod_ready/ui_matrix.md (5×2 PASS smoke + код-аудит
остальных; пиксельные скрины меню — human gate).
Честная оценка: уровень «дорогой стилизованный инди» достигнут и
подтверждён кадрами; формулировку «SoTA-паритет» оставляю человеческому
art review (по контракту плана art-lock не закрывается автоматикой).

Статус: IN_GAME_VERIFIED.
Примечание к заморозке: GateA_Ribbon сначала перевовдился на
fabric_pattern — контракт кита (GeneratedModularKitContractSmokeTest)
разрешает для GateA_ только wood_fence/cloth; оставлен owner=cloth с
приглушённым тоном 9a8d78. Финальная заморозка на истинном финальном
состоянии: verify-godot exit 0, walkthrough PASS, capture 48/48,
benchmark 119.8–120 FPS, git diff --check чистый; пары перегенерированы
из финального capture.

Статус: IN_GAME_VERIFIED.

## Previous work card — prod-ready visual plan: фазы 0–10 исполнены (автоматизируемая часть)

Исполнение `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` (см. также
параллельную задачу Codex по 19 новым текстурам). Фаза 0: базлайны — capture
48 кадров + benchmark (115–120 FPS) в evidence/act1_prod_ready/baseline.
Фаза 1A: промоушен кандидатов — wood_facade/fence v4/v2, furniture v3,
earth v3, terrain v5, plaster v3, foliage v2, stone v3, fabric v3 (выбор по
вистам; человеческий art-gate остаётся открытым). Фаза 2: шейдер —
текстура-доминанта (strength 0.92–0.95), вертикальная посадка
ground_darken 0.10–0.25, поклеточный hue-jitter (6 м, de-clone), макро-пятна
value_noise ~3 м; тихий stroke-wash. Фаза 3: свет — тёплый низкий ключ
(ffd9a8, 1.42, −41°, shadow 0.42), холодный слабый fill (9fb0b8, 0.70),
крыши на ступень темнее стен, окна: DimGlass → тёмное стекло, WarmInset →
авторизованный emissive (ffb45e, 1.6). Фаза 1B: подключены семейства Codex
(grass/roof/roof_metal/bark_birch/bark_pine/leaf_birch/log_wall/wallpaper/
wall_institution/carpet/fabric_pattern/ornament_trim/wood_carved;
leaf_birch_v1 локально перекрашен — 76k небесных пикселей → глухая зелень,
записано в provenance); трава/берёзы/кроны/крыши перенаправлены (совместно
с параллельной обвязкой Codex; одна коллизия дублей switch-arms/словаря
устранена). Фаза 4: адресная палитра — per-parcel tints стен (A/B/C/hero) +
один приглушённый сине-зелёный акцент бруса VariantB. Фаза 5: мокрые
плоскости темнее. Фаза 7: ветер — вершинный sway (foliage/leaf_birch/
grass_tuft) с глобальным wind_enabled по reduced-motion (SetWindMotion;
rm-smoke PASS). Фаза 8 аудит: D1 масштаб земли (1.3x2.6→0.55x1.1 — тропы
читаются), D2 десатурация оранжевого дерева (4 bases), D3 DistantRoof
plaster→roof — закрыты перекадрировкой. Фаза 9: аудит UI-темы — единый
язык подтверждён (идентичные Panel/Button стили, 5 состояний), readability
PASS 2x5; глубокий рескин — человеческий gate. Фаза 6: силуэты — частично
покрыты (ridge/eave/corner-post в core-билдере, тёмные фактурные кровли);
полный Blender-обвес свесов/труб зафиксирован как remaining. Фаза 10:
verify-godot полный агрегатор exit 0; benchmark 119–120 FPS (floor 30,
запас 4x); capture 48/48; verify-asset-registry PASS; git diff --check
чистый. Финальные пары «весь путь»: evidence/act1_prod_ready/
final_before_after/ (+ контактный лист 00_ALL). Оценка против бара: стиль
прочитан (тёпло-холодный ключ, ценовая структура, фактурные поверхности) —
уровень «дорогой стилизованный инди»; full SoTA-паритет требует Фазы 6
(Blender обвесы), ground-scatter и человеческого art review — честно
зафиксировано как remaining, автозакрытия art-lock нет.
Статус: IN_GAME_VERIFIED (автоматизируемая часть плана).

## Previous work card — content1: Трек 2 приоритет 1 (документы/диалоги/словарь/хронометраж)

Участок: data-driven контент, без новых систем. (1) Старый ПК: 11→27
документов — 16 новых бытовых/архивных md (хозяйственная книга, квитанции
электрики с приписками о просадке «в сторону леса», заметки о приёме
радио, письмо из Казани, страница тетради Марата с production-default
фразой «Казанский, не отставай», линии реестра дома Гөлсинә/Фаниса/приезд
внука, «допуск по бумагам», вымаранная строка 1970-х, строка 1987 «граница
/ ответил», сводка категорий по десятилетиям, статьи «Татарвики»:
тавыш-рассказы (по канону кампании 4.9 — без императива «не откликаться»)
и обычаи зирата); существующие 2 бытовых расширены. (2) Диалоги: 7 шт.,
17 узлов / 10 choices — ветки «без ключа / слабый / сильный» у Гөлсинә,
Мансура, Алсу, Наили (подключён orphan-узел separate-record + «Бу ярамый…»
как канонное подтверждение tt_yaramyy), Рината (подключён orphan «формально
закрыто» + ветка «Не здесь. И не этим словом» при confirmed җавап,
кампания 4.8), Тимура. Стартовые узлы всех диалогов остаются бесвыборными
при первом прохождении (контракт Continue-закрытия в walkthrough и
DialogueFlowSmokeTest сохранён); choices гейтятся знаниями, которых в этот
момент ещё нет. (3) Словарь: 6→12 слов (`юл`, `өй`, `бабай`, `әби`,
`барма`, `хәзрәт`; экспозиция guessed в onEnter arrival/house/zirat-road и
в диалоге Тимура; consultantReview: pending). Одно исправление по падению
DialogueFlowSmokeTest: ветка «почему ярамый» перегейтана с guessed на
confirmed (тест входит в узел до открытия UI — guessed уже стоит).
(4) Хронометраж (честно, из compiled): 13 254 знака документов + 5 617
знаков диалогов при 1400 зн/мин = 7,6 + 4,0 мин чтения; + PC-overhead
27×0,3 = 8,1 мин; + диалоговый темп 2,6 мин; + маршрут 335 м = 6–8 мин;
+ интеракции улик/журнал = 4–6 мин. Итого 34–38 мин критического пути,
40–48 мин с возвратами по веткам ключей. Цель ≥35–40 достигнута на
full-path; критический путь на нижней границе.
Статус: IN_GAME_VERIFIED.
Результат: compile зелёный (validate/simulate в составе), build 0/0;
walkthrough PASS 335,27 м (три прогона: после документов, после диалогов,
финальный); DialogueFlowSmokeTest PASS (после перегейтовки); verify-godot
полный агрегатор exit 0. Счётчики compiled: documents 27, vocabulary 12,
texts 63, dialogues 7 (узлы 17/choices 10).
Остающееся (Приоритет 2 — требует согласования с пользователем по промпту):
мечеть+Тимур как 3D-зона (самая дорогая), сельмаг-интерьер, решение по
river_bank (вне MVP с фиксацией в decision_log); Приоритет 3: давление
деревни (реакции 2–3 NPC на опасные ключи + ambience-эскалация); мессенджер
«Ялкын» (Should Have). Татарские фразы — «требует носителя», автозакрытия
нет.

## Previous work card — sky1: painterly-облака + солнечный диск (Трек 1 Шаг 2)

Участок: тот же единственный владелец — ProceduralSkyMaterial в
`AgentBAct1ExteriorLayer.BuildEnvironment` + zone-настройка в
`TuneConnectedAct1Atmosphere` (`Act1ConnectedWorld.cs`). Что меняю:
(1) SkyCover — процедурная 512x256 текстура мягких живописных полос
облаков, генерируется на месте через FastNoiseLite (SimplexSmooth fBm,
2 слоя: низкочастотная маска полос + детальный слой; сэмпл по цилиндру —
шов на горизонте отсутствует; SkyCoverModulate alpha 0.52 день);
(2) солнечный диск/ореол для day-зон: SunAngleMax 3.2, SunCurve 0.10,
в kara night — 0 (голое холодное небо); (3) night/zirat модуляция обложки
(0.22/0.42 alpha) — ночью и на зирате облака тише. Одна итерация тюнинга:
первая генерация читалась вертикальными «колоннами» (угловая частота
высока) — частоты снижены, полосы сплющены в горизонтальные мягкие
washes. Никаких volumetric clouds, новых систем и владельцев.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м (cliffhanger completed);
capture gate PASS 48/48 (/private/tmp/urman-sky2.Cq4Zh); benchmark PASS —
day_street 7.534 мс / 132.7 FPS, house_old_pc 6.999 / 142.9,
kara_urman_edge 6.900 / 144.9, floor 30 (TSV обновлён). Кадры inspected:
arrival_forward / main_street_depth — небо с мягкими горизонтальными
полосами вместо пустого градиента, «полтора экрана пустоты» ушли;
kara_approach_forward — слабые холодные полосы, диск отсутствует;
zirat_depth — пасмурная глубина, надгробия с контактными тенями.
Регрессий нет. Evidence: evidence/act1_repo_baseline/sky1/ (4 пары).
Остающееся: Шаг 3 (ветер) — опционален по бюджету; art review человеком.

## Previous work card — postfx1: Glow+SSAO+Adjustments+AgX в единственном владельце окружения

Участок: `TuneConnectedAct1Atmosphere` в `game/scripts/Act1ConnectedWorld.cs`
(единственный внешний владелец `AgentBEnvironment`, контракт ART-010 сохранён;
интерьерные zone-environment не тронуты). Что меняю (один slice): Glow
(enabled, intensity 0.40 kara / 0.55 день, Softlight, hdr_threshold 1.0,
bloom 0.10), SSAO (enabled, intensity 1.2–1.5, radius 1.2–1.5),
Adjustments (saturation 1.12–1.16, contrast 1.05–1.06), TonemapMode
Linear→AgX (painterly rolloff), туман: FogLightColor сдвинут к цвету неба
зоны (village `5f7477`→`6d8588`, zirat `6b7775`→`6f7f7d`, kara `536b70`→
`506a72`), FogAerialPerspective 0.54/0.48/0.56 → 0.60/0.55/0.62 — дальний
план тает в небо, ближний план контрастнее.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м (cliffhanger completed);
capture gate PASS 48/48 (/private/tmp/urman-postfx.AfMuqG; gate-обёртка
`eng/capture-act1-full-route-core-world.sh` синхронизирована 44→48 под
текущий тест с 4 holding-кадрами из production-диффа); benchmark PASS —
day_street avg 7.163 мс / 139.6 FPS, house_old_pc 6.900 / 144.9,
kara_urman_edge 6.900 / 144.9, floor 30 (TSV обновлён прогоном);
verify-godot полный агрегатор exit 0, включая act1-ui-readability PASS
(2 разрешения x 5 критичных UI — bloom текст не мылит). Кадры inspected
глазами (6 пар до/после): arrival_forward — теплее/насыщеннее, дальние
дома растворяются мягче; main_street_left — дальний VariantC читается
объёмом в воздушной перспективе, оконные проёмы с контактными тенями;
kara_approach_forward — ночь глубже, дорога читается лучше; zirat_forward,
babai_yard_depth, main_street_depth — регрессий нет; интерьер дома
(свой environment) не изменён. Evidence:
evidence/act1_repo_baseline/postfx1/ (6 пар кадров).
Остающееся: Трек 1 Шаг 2 (небо: облака/солнечный диск), Шаг 3 (ветер,
опционально), затем Трек 2 (контент-глубина). Финальная оценка «дорого
выглядит» — человеческий art review.

## Previous work card — User feedback + handoff — 2026-09-07, вечер

Две претензии пользователя после просмотра сравнения до/после:
(1) «выглядит сыро, возможно не хватает постэффектов/шейдеров; пусто,
хотя не пусто» (сравнение с BOTW/TOTK); (2) сомнение в полноте Акта I —
«должен проходиться около 40 минут». Диагноз предыдущего запуска:
(1) мир наполнен, но рендер не разделяет планы — нет Glow/SSAO/
Adjustments, туман сливается с небом по value, небо — пустой градиент,
мир статичен (владелец: AgentBEnvironment/AgentBSun, ~строки 995-1045);
(2) претензия обоснована числами: 11 документов по 239-497 символов,
7 диалогов почти по 1 узлу, 6 слов словаря, сцены-заглушки без
интеракций (mosque/selsmag/river_bank/forest/zirat-0), мечети нет в
3D-мире, «Ялкын» не реализован — фактическая длительность 12-18 минут
против 40-60 в chapter1_mvp_campaign. Полный handover-промпт для
следующей модели: docs/production/URMAN_VISUAL_AND_ACT1_COMPLETION_PROMPT_RU.md
(трек 1: пост-процессинг по шагам с проверками; трек 2: контент-план
по приоритетам с хронометражем).

# Act I OpenCode Execution Ledger

## Current work card — Blender-участок: фасад вариант-домов на средней дистанции

Участок / ассет: уличные фасады семейства author_rural_dwelling
(urman_village_exterior_kit, все variant-дома + hero-фасад).
Дефект и путь к изображению: на 28-30 м VariantB-дом
(MainStreetEastNearMidHouse, probe-подтверждён: Roof/Window_Mullion на
кадре main_street_depth) читается решёткой тёмных стоек/перекладин, а не
оштукатуренным объёмом; то же у VariantC на main_street_left (кропы
/tmp/s1b.png, /tmp/s2b.png из /private/tmp/urman-trees.G0XlH5).
Цель из арт-документа: концепт 03_main_street_forward — дома читаются
сплошными объёмами, окна — спокойные тёмные проёмы с тонкими рамами,
плоские перемычки, без декоративных «корон».
Исходник/экспортёр/потребитель: assets/source/blender/act1/
urman_village_exterior_kit.py (author_rural_dwelling) → .blend+GLB тем же
генератором → placements Act1ConnectedWorld (VariantA/B/C + hero).
Проверено Blender-рендером: сени закрыты и корректны вблизи
(/tmp/urman-blender-check/variantC_seni_*.png) — дефект дистанционного
читается, не отсутствующих стен.
Что именно меняю (≤3): (1) убрать 5-точечную Headboard-корону над каждым
окном; (2) убрать Transom-бар; (3) всё остальное (jambs/rails/mullion/
sill/glass) сохранить. Внешних ссылок на Headboard/Transom нет (grep).
Что должно быть видно: на 28 м фасад читается сплошной штукатурной
стеной с тихими окнами, без решётчатого read; вблизи окна остаются
обрамлёнными.
Камеры и путь к кадрам «до»: main_street_depth/right, main_street_left
(/private/tmp/urman-trees.G0XlH5 + зум-кропы выше).
Команда сохранения / экспорта / проверки: Blender --python <генератор>;
verify-asset-registry; build; walkthrough; capture.
Последний устойчивый файл: urman_village_exterior_kit.py (текущий).
Статус: IN_GAME_VERIFIED.
Результат: генератор отредактирован (2 удаления в wall(): Transom-бар +
Headboard-корона; jambs/rails/mullion/sill/glass сохранены), .blend и GLB
регенерированы тем же генератором (709 LOD0-мешей / 20,558 tris / 16
материалов; Headboard/Transom в GLB — 0), registry-хэши и manifest-числа
обновлены, verify-asset-registry PASS 55/55. Build 0/0; walkthrough PASS
335,27 м; capture PASS 48/48 (/private/tmp/urman-windowtrim.G4sGjY).
Кадры inspected: main_street_depth — восточный дом на 28 м читается
сплошным штукатурным объёмом с тихими проёмами (zoom-пара: решётка →
объём); main_street_left — дальний VariantC-дом стал сплошным тёмным
объёмом; babai_yard_forward — hero-фасад вблизи только выиграл (окна
спокойнее, тёплое окно и портал на месте). Регрессий не найдено.
Diagnostic: pixel-probe доказал владельца (VariantB Roof/Window_Mullion
на 27.9-30.6 м) до правки; Blender-рендер изолированного дома подтвердил
закрытость сеней (дефект был в дистанционном read, не в геометрии стен);
временный probe удалён из capture-теста. Evidence:
evidence/act1_repo_baseline/windowtrim/ (3 пары кадров + zoom-пара +
Blender-референс).
Остающееся: фасады — вопрос человеческого art review (готовность к
приёмке), не автозакрытие.

## Previous work card — 2026-09-07, slice G: tree geometry + street verge (user art feedback)

Триггер: пользовательский фидбек «графика всё ещё плохая, ассеты не на
месте» — человеческий сигнал, открывший следующую art-итерацию.
Дефект (по зум-кропам финальных кадров): кониферы читались схематичной
колонной из плоских одноцветных чешуек (14×7×9 flat-треугольников,
один материал); берёзы — тонкая палка (r=0.018·h) с редким зонтиком;
обочины главной улицы голые (трава была только на въезде).
Что именно меняю (общие билдеры = ~73 конифера / 97 берёз / 62
широколиственных одним изменением): (1) AddVisualConifer — двухслойная
крона: открытый подол из провисающих fans (профиль шире в нижней трети,
12 ярусов × 6 ветвей × 5 sprays) + тёмная внутренняя заливка (второй
меш/материал DarkerFoliageHex 0.76) + 5-fan лидер-вершина; (2) берёза —
ствол 0.024·h с базовым расширением 1.3×, crownScale 0.077→0.105,
второй меньший spray на свисающем конце побега (полнота нижней кроны);
(3) MainStreet — паттерн некошеной обочины ArrivalUncutVerge расширен
на главную улицу (2 полосы x=±4.3, z 5.5..-17.9, с проходами у калиток
и ФАП-аппарели), та же палитра.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-trees.G0XlH5); benchmark PASS (111-120 FPS avg,
floor 30; p95 kara 13.2 мс — запас 2.3×). Зум-сравнения inspected:
arrival-конифер (слоистый подол с тёмной глубиной вместо плоской
колонны), kara-кониферы (полные силуэты-«ёлки»), main_street_left
(обочина с травой, кроны полнее), main_street_forward (регрессий нет).
Evidence: evidence/act1_repo_baseline/trees/ (4 пары кадров + 2 пары
зум-кропов).

## Previous work card — 2026-09-07, verification batch: CAPTURE-004 / PERF-002 / RELEASE-002+004

Автоматизируемые verification-семейства трекера, закрытые после визуальных
slice'ов: (G) CAPTURE-004 — supplemental directional capture PASS 10/10
уникальных root-viewport кадров (up/down/pitched по зонам; receipt
evidence/act1_repo_baseline/cap004-supplemental-receipt.json, набор
/tmp/urman-cap004.M5zToJ); (H) PERF-002 — benchmark PASS, ~120 FPS avg по
day_street/house_old_pc/kara_urman_edge на Apple M4 Pro при floor 30
(docs/urman_knowledge_base/performance/godot_m4pro_baseline.tsv обновлён;
M1/Windows-хосты остаются открытыми); (I) RELEASE-002/004 автоматическая
часть — act1-macos.pck и act1-windows.pck по 156 записей, headless/Dummy
bootstrap обоих целей PASS (RC/release readiness остаются OPEN, human-
гейты не закрыты). Все три команды exit 0; трекер дополнен датированной
записью. Эти результаты не закрывают human/art/cultural гейты.

## Handover — 2026-09-07

Полный handover-отчёт для следующей модели-исполнителя:
`../archive/act1_handover_2026-09-07.md` (состояние, маркеры правок,
грабли, очередь). Первичный источник фактов запуска — карточки ниже.

## Full-frame inspection — 2026-09-07, final candidate sliceF

Систематический осмотр финального capture (/private/tmp/urman-sliceF.Foesg1,
48 кадров, 8 зон) сверх уже проверенных slice-кадров: house_interior (4),
fap_interior (4), arrival_back/depth, house_exterior_back,
connective_street_return_forward, zirat_holding_entry. Новых дефектов с
владельцами в коде не найдено: интерьеры обитаемы (печь/стол/шкаф/старый ПК
в доме; регистратура/ширма/стенд в ФАП), обратные и дальние виды связаны,
периметры без провалов. Вне автоматической очереди остались: схематичность
NPC-моделей (ART-012, human cultural review), специфичный сбоку каркас сеней
VariantC (стилистический вопрос art review), финальный свет/экспозиция
(Z0x-007, human). Очередь именованных автоматизируемых дефектов исчерпана.

## Current work card — 2026-09-07, slice F: merged-mass fix + boundary clearance

Участок / ассет: восточная сторона главной улицы (near-mid holding и
EastStreetMid parcel).
Дефект и путь к кадрам «до»: (1) объём EastStreetSideClosureFacade
физически пересекался с полноразмерным MainStreetEastNearMidHouse — на
кадрах читалось как одна слипшаяся масса из двух корпусов;
(2) повторный расчёт асимметричного AABB VariantA (8.83x6.62, локальный
+X до 5.12) показал: EastStreetMid при якоре (26.6,-19.2) yaw -82
достаёт x=31.5 и пересекает границу holding x=30.7 (мой прежний расчёт
был по симметричному габариту). «До»: slicef/*_BEFORE.png.
Что именно меняю: (1) фасад side-closure удалён — replacement уже стоит
рядом (near-mid дом), parcel сохраняет сарай/ограду/деревья как двор;
(2) EastStreetMid якорь сдвинут на (25.3,-19.2) — восточный край
x=30.2, зазор до границы 0.5 м, до near-mid дома 2.8 м, до far holding
2.0 м, до FapBranch 2.8 м.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-sliceF.Foesg1). Кадры inspected: main_street_right
(один корпус вместо слипшейся массы, EastStreetMid отдельным домом),
main_street_depth (улица вглубь чистая). Пары: slicef/.

## Previous work card — 2026-09-07, slice E: Kara ground life + duplicate edge facade

Участок / ассет: кромка Кара-Урмана (склоны и обочины) и деревенско-зиратская
граница.
Дефект и путь к кадрам «до»: плоские бледные валуны-«блины» кластера
KaraMossyBoulderCluster на обочинах дороги (kara_approach_right/forward,
evidence/act1_repo_baseline/sliceE/*_BEFORE.png); ZiratVillageEdgeWestFacade
дублировал стоящий полноразмерный ZiratVillageMemoryHouseWest.
Цель: P0-таблица «KaraForestEdge … foliage must not mask missing geometry»,
лесопол «ground life» из ledger.
Исходник/потребитель: компоненты kara-кита через существующий
AttachAct1ExteriorKitComponent; камни/кусты — нативные AddVisualStoneCluster/
AddVisualShrub (материалы уже в painterly-таблицах).
Что именно меняю: (1) кластер валунов сдвинут с обочины в глубину между
стволами (local (9.5,-8.5), yaw -35, scale 0.95); (2) два скрытых камня и
два куста у корней на склонах вне 3.5-м конверта дороги (западный камень
первого кандидата сам попадал на открытую обочину и был убран после
кадров); (3) удалён дубль-фасад ZiratVillageEdgeWestFacade (замена —
стоящий рядом полноразмерный дом; граница осталась на ограде).
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-sliceE2.l78Ub9). Кадры inspected: kara_approach_right
(блины с обочины ушли), kara_approach_forward (обочина чистая после
удаления западного камня-кандидата), kara_approach_depth (глубина леса
без нареканий). Один внутренний rejection: первый кандидат с западным
камнем на открытой обочине отклонён по собственному кадру.

## Previous work card — 2026-09-07, slice D: subagent review fixes

Участок / ассет: EastStreetFarHolding, ограды двора бабая, EastStreetMidFacade,
правило VariantC в AddAuthoredHouse.
Дефект: независимое read-only ревью (субагент, вердикт REQUEST CHANGES)
доказало: (1) дом holding (VariantC scale 1.0, реальный AABB 8.63x6.32,
веранда на local +X) пересекал собственную восточную ограду и соседний
забор z=-15; (2) сарай/дрова клиппили угол веранды; (3) ограды двора бабая
(x=-2.5/-8.5, z 7.6..13.6) резали видимую дорожку к дому
(BabaiYardArrivalRoadEnvelope, полувысота 2.4 м); (4) правило "Far"
переключало ~10 дальних домов на VariantC вместо одного; (5) правка
EastStreetMidFacade из slice C молча не применилась (python-replace без
assert) — в коде оставалось 4 конверсии из заявленных 5, пары кадров
main_street_right BEFORE/AFTER совпадали пиксельно.
Что именно меняю: (1) правило сужено до "FarHolding"; (2) дом переставлен
(35.4,-16.9) yaw 2 scale 0.85 (веранда на восток, зазоры >=0.5 м до обеих
оград и до соседнего забора), сарай вынесен как полевая постройка на
(43.8,-20.5), дрова к северному проёму (34.9,-14.4), east fence x=41.6,
проём сдвинут к 37.0..38.6; (3) двор бабая сдвинут на север (mount
(-5.5,13.5), сарай 0.85), западный/задний прогоны удалены (их края уже
держат kit fence и соседский z=16), остались L-ограда East z 12.0..15.8 +
North x -6.2..-2.5 на z=15.8, обе >=0.3 м от дорожки; (4) EastStreetMidFacade
переключён на VariantA 0.88 с якорем (26.6,-19.2) (зазор до границы
x=30.7 — 0.9 м).
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-sliceD.CBPbqQ). Кадры inspected: babai_yard_forward
(ограда с проёмом, дорожка открыта, шед севернее), main_street_right
(EastStreetMid теперь VariantA, holding без наложений), zirat_back (силуэты
улицы чистые). Пары до/после: evidence/act1_repo_baseline/sliced/.
Честные оговорки: в момент capture slice C конверсий было четыре (заявление
«пять» и часть inspected-описаний той карточки не соответствовали коду —
исправлено настоящей карточкой); в sliceb у fap_exterior_back и
main_street_depth нет BEFORE-пар (промежуточный capture удалён до
копирования); постоянной BEFORE/AFTER-пары babai-yard slice 1 нет (evidence
остался в /tmp, удалён хостом).

## Previous work card — 2026-09-07, slice C: street facade diversity

Участок / ассет: пять клонов `DwellingFacade_TimberPlaster` на периметре
улицы (WestStreetMid, EastStreetMid, WestReturnMid, EastReturnMid,
EastStreetHorizon).
Дефект и путь к актуальному изображению: «repeated facades» из P0-таблицы
трекера — один и тот же фасад-компонент по обеим сторонам улицы («до»:
evidence/act1_repo_baseline/slicec/*_BEFORE.png).
Цель: design_style — «ряд одинаковых фасадов» запрещён; три авторские семьи
домов в наборе (VariantA timber gable, VariantB plaster annex, VariantC
banya yard) должны читаться.
Исходник, экспортёр, потребитель: те же GLB-компоненты кита
urman_village_exterior_kit через существующий AddAct1AuthoredExteriorParcel
(вложенный путь `VillageParcel_X/X_Dwelling`); размещение/потребители без
изменений.
Что именно меняю: (1) пять placements переключены с facade-компонента на
полноразмерные variant-dwellings (масштаб 0.88-0.90, соответствующий
AddAuthoredHouse-дому); (2) якоря/yaw сохранены. Presentation-only.
Что должно быть видно после изменения: обе стороны улицы читаются разными
домами (A/B/C ротация), без парящих/миниатюрных/пересечений.
Камеры и путь к кадрам «до»: slicec/*_BEFORE.png.
Команда сохранения / экспорта / проверки: build; walkthrough; capture.
Последний устойчивый файл: game/scripts/Act1ConnectedWorld.cs.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-sliceC.OV3OiS). Кадры inspected: main_street_left
(WestStreetMid теперь VariantC), main_street_right (EastStreetMid —
VariantA, far holding C на краю), main_street_back и
connective_street_return_back (смешанные семьи по обеим сторонам), без
новых пересечений/парящих частей. Conflicts проверены по координатам до
правки (заборы 30.7/-3, far holding 33.5, FapBranch z-26 — зазоры >=0.5 м).
Оставшиеся клоны DwellingFacade (Arrival-группа с hero-окном,
SideClosure у ограды, ZiratVillageEdge у ядра-дома) намеренно сохранены:
near-камера/спец-обработка либо конфликт якорей; следующий slice при
необходимости — с переносом якорей.
ПОПРАВКА ПО РЕВЬЮ: фактически в этом slice применились четыре конверсии —
правка EastStreetMidFacade не совпала (replace без assert) и не попала в
capture; заявление «пять» и её кадровое описание неверны для этого slice.
Конверсия выполнена с якорем (26.6,-19.2) в slice D ниже; кадры
main_street_right BEFORE/AFTER здесь идентичны — это и есть следствие.

## Previous work card — 2026-09-07, slice A+B: remaining kit materials + east far holding

Участок / ассет: (A) zirat roadside и FAP kit-материалы в общих видах;
(B) восточное поле главной улицы (x 33.5..40, z -13.5..-22).
Дефект и путь к актуальному изображению: (A) бледные каменные группы,
сырые кусты и белёсые ограды в zirat_forward, выцветшие кусты/берёзы ФАП
(«до»: evidence/act1_repo_baseline/sliceb/*_BEFORE.png); (B) ограда-сирома
в пустом поле на main_street_right.
Цель из действующего арт-документа: design_style палитра (болотная зелень,
мокрое дерево, приглушённый камень); P0-таблица трекера «both parcel sides».
Исходник, экспортёр, потребитель: zirat kit (MossGreen, MossyStone,
WeatheredWood, WeatheredWoodDark, DistantFence, DistantFoliage, DitchWater,
WetSheen) и FAP kit (FapBirchBarkMark, FapBirchPale, FapShrubGreen,
FapShrubLight, FapVentDark) не входили в `RegradeAct1DaylightKitMaterials`;
holding собирается из существующих семей (AddAuthoredHouse VariantC через
новое правило «Far», OutbuildingShed_Low, Woodpile_StackedLogs,
AddVisualFenceRun/Shrub/Tree).
Что именно меняю: (1) +13 записей материалов в общий словарь; (2) новый
EastStreetFarHolding (дом VariantC + сарай + дрова + 3 прогона ограды с
входным проёмом + куст + берёза), существующая EastStreetFarFence стала
южной границей; (3) AddAuthoredHouse получил третье семейство VariantC для
имён с «Far». Presentation-only.
Что должно быть видно после изменения: zirat/FAP в общей палитре; восточное
поле читается worked plot, а не пустотой с сиротой-оградой.
Камеры и путь к кадрам «до»: sliceb/*_BEFORE.png + /tmp/urman-matfix.VPf2dU.
Команда сохранения / экспорта / проверки: build; walkthrough; capture.
Последний устойчивый файл: game/scripts/Act1ConnectedWorld.cs.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-sliceB.s8yqUU). Кадры inspected: zirat_forward (камни
и кусты в палитре, белые массы ушли), fap_exterior_left/back (кусты и
берёзы ФАП тёмные, здание читается), main_street_right/depth (поле получило
полноразмерный VariantC-holding, ограды связаны). Замечание: AddAuthoredHouse
форсирует масштаб >=0.90 — все авторские дома уже полноразмерные; «миниатюр»
в этой семье нет. Оставшиеся raw-материалы: только agentb-наборы, чьи части
получают материалы из C#-строителей (не сырые); при появлении сырого вида в
кадрах — отдельный slice.

## Previous work card — 2026-09-07, main street mint mass

Участок / ассет: главная улица (западная обочина у MainStreetWestNearFence),
компоненты `FernShrubBreak_Left/Right` кита `urman_wet_village_road_kit`.
Дефект и путь к актуальному изображению: крупная мятно-бирюзовая полиэдральная
масса на линии ограды в main_street_left (кроп: /tmp/msl3_big.png из
/private/tmp/urman-babai-yard-slice3.sYpkkv); также белёсые ограды и ярко-синяя
вода колодца в hide-тесте /tmp/urman-hidetest.PHs0MI/main_street_left.png.
Цель из действующего арт-документа: design_style палитра — «болотная и хвойная
зелень», «обычность сначала»; никаких кислотных/неоновых масс.
Исходник, экспортёр, потребитель: kit GLB materials `Shrub_BlueGreen`,
`Fern_MossGreen`, `Fern_LeafLight`, `Grass_SedgeMuted`, `Grass_SedgeDryTips`,
`Moss_WetOlive`, `Fence_DampWood`, `Fence_CutWood` не входят в словарь
`RebindWetVillageRoadMaterials` (12 записей) и рендерятся сырым albedo;
потребитель — все маунты road-кита на Arrival/MainStreet/ReturnStreet.
Отдельно `URMAN_Well_DarkWater/Wood_CutEnd/Bark_Muted/Metal_Dulled`
(village kit) не входят в `RegradeAct1DaylightKitMaterials`.
Что именно меняю: (1) добавить 8 недостающих organic/fence записей в
`RebindWetVillageRoadMaterials`; (2) добавить 4 записи village kit в
`RegradeAct1DaylightKitMaterials`; (3) убрать временные probe/hide-test из
capture-теста. Только материалы, без геометрии/коллизии/нарратива.
Что должно быть видно после изменения: масса у ограды читается приглушённым
болотно-зелёным кустом, ограды — мокрым деревом, вода колодца тёмная.
Камеры и путь к кадрам «до»: main_street_left/right, babai_yard_left,
arrival_forward из /private/tmp/urman-babai-yard-slice3.sYpkkv.
Команда сохранения / экспорта / проверки: dotnet build; walkthrough smoke;
`./eng/capture-act1-full-route-core-world.sh <fresh dir>`.
Последний устойчивый файл: game/scripts/Act1ConnectedWorld.cs.
Статус: IN_GAME_VERIFIED.
Результат: build 0/0; walkthrough PASS 335,27 м; capture PASS 48/48
(/private/tmp/urman-matfix.VPf2dU). Кадры «после» inspected:
main_street_left (минтовая масса читается приглушённым болотным кустом,
ограды — мокрым деревом), main_street_right (полевые ограды потемнели),
babai_yard_left (белые блобы у куста исчезли), arrival_forward (чисто).
Сравнение «до»: /private/tmp/urman-babai-yard-slice3.sYpkkv (полный
«до»-набор) и evidence/act1_repo_baseline/matfix/ (четыре парных кадра).
Владелец дефекта доказан временно добавленным CPU triangle-probe
(Shrub_BlueGreen albedo 0.38/0.49/0.45 = наблюдаемый минт) и hide-тестом
ArrivalAuthoredParcels (масса осталась — не фасад); диагностикум удалён.
zirat/kara/agentb kit-материалы (MossGreen, FoliageBlueGreen, AB_foliage_* и
пр.) — та же семья, отдельный следующий slice со своим сравнением кадров.

## Current continuation — 2026-09-07, independent main agent

Babai yard east-depth slice (the exact resume point of the 2026-09-05
resource stop): replaced the miniature `BabaiEastDepthPlasterAnnexParcel`
(0.44/0.48-scale complete VariantB parcel at (-5.5,0,10.5)) with the
existing full-scale `OutbuildingShed_Low` (.95 scale), one
`Woodpile_StackedLogs` and a three-run picket service boundary
(BabaiEastDepthServiceBoundaryWest/East/Rear, x -8.5..-2.5 / z 7.6..13.6,
visual-only `AddVisualFenceRun`, rear run closed, road side open).
Shed door face turns toward the yard/path cameras (-80 deg yaw after the
first 100 deg candidate was rejected in frames: rear gable/windowless wall
faced babai_yard_depth). Mount stays presentation-only; HousePathAxis, the
hero threshold, collision/navigation/interaction owners unchanged; no asset,
source or texture edit (`Act1ConnectedWorld.cs` placement block only).

Verification: build 0 warnings/0 errors; launch smoke exit 0; physical
walkthrough PASS 335.27 m twice (before and after the yaw correction),
including street save/resume and completed cliffhanger; full capture PASS
48/48 unique frames, eight zones, exit 0. Root inspected babai_yard_depth,
babai_yard_forward, babai_yard_left, babai_yard_right: the miniature house
is gone from the yard depth view, the service shed reads at outbuilding
scale inside its boundary, no floating/oversized fragments. Ephemeral
capture: /private/tmp/urman-babai-yard-slice3.sYpkkv (slice1/slice2 dirs
were removed by host /tmp cleanup before inspection; slice3 retained the
final series). This closes the specific miniature-parcel defect named in
the tracker's P0 example table; whole-zone and whole-map art remain
REWORK, no art gate closed.

Disk: session started at 26 GiB free; fell to 1.9 GiB during the slice
and to 472 MiB at slice close while only ~44 MiB of own capture output
exists under /tmp — the consumption is external to this run. This is the
second confirmed low-disk impasse (same condition as the 2026-09-05
stop); heavy iteration must not resume
until several GiB of stable headroom are restored; recheck `df -h .`
before any capture/import/walkthrough. graphify update and
`git diff --check` pass. Next ready scope after headroom recovery: the
remaining village spatial weaknesses named in this ledger (repeated
facades, bare east plots, crude stone/ground forms) and the Kara
forest-floor/material work; do not restart brightness passes.

## Previous continuation — 2026-09-05, independent main agent

Resource stop — 2026-09-05 (first confirmed low-disk impasse): previous turn
was verified production progress. This turn re-inspected current main-street
and Babai-yard frames in rjpFSt. BabaiEastDepthPlasterAnnexParcel is still a
0.44/0.48-scale complete parcel at (-5.5,0,10.5), reading as a miniature house
between full-sized dwellings. Next bounded change: replace that camera prop
with the existing full-scale OutbuildingShed_Low and a coherent service-yard
boundary/access, checking actual door orientation and neighboring footprints.
Do not enlarge the entire mini-parcel in place or block HousePathAxis.
No production edits made to this yard yet; code location around line 3640.
Source shed footprint ~3.7x2.5 m, door local Blender (0.42,-1.22,1.15).

Disk fell from 709 MiB to 398 MiB even after removing 273 redundant own PNGs
(213.28 MiB) from seven ledger-confirmed old captures: DLFSAv, RLFTTW, jjd9TM,
pLZILb, OlsD8a, 7kSbz7, 13kyyr. Retained arrival/yard/FAP/zirat/Kara forward
comparisons in each and the latest full rjpFSt series. No sessions, source,
SDKs, user files or unknown captures touched. sysctl reports 10,291 MiB swap
used; this alone does not prove the cause of the ongoing disk decrease.
No Godot/Blender processes are running. Do not start builds/imports/captures
until disk capacity is restored. Need several GiB of stable free headroom;
recheck df before proceeding. Goal remains unachieved; no art gate closed.
Second consecutive goal-turn recheck: 398 MiB available, no Godot/Blender
process and no verification override. Capacity has not recovered; no new
implementation/build/capture started. Further deletion would sacrifice retained
evidence or touch dependencies/user-owned data without resolving stable headroom.
Third consecutive goal-turn recheck: 397 MiB available. Same resource impasse;
goal marked blocked, not complete. Resume after several GiB of stable free
space are restored, starting with the yard replacement described above.

Current Kara stand composition: previous turn was progress (shared terrain,
duplicate benchmark suppression, physical route 335.27 m). Rejected the rows
of detached bent roots visible in k452VQ. Removed their replacements and four
bank logs; original triangular plates remain suppressed. Replaced four near
kit shell-crown groups with eight staggered native open-canopy trees on the
slopes, roots taper from each trunk into the same terrain. Existing branch
builder gains optional radii; other callers retain defaults. Added ten small
conifer regrowth anchors in sheltered pockets/beyond the final playable
threshold, plus three open-leaf shrubs. No ground/collision/narrative changes.
Intermediate 8ibfrE PASS 48/48; main inspected depth and identified excessive
open space below the canopy. Regrowth npMzVT PASS 48/48, main inspected actual
forward/depth: no detached root row, clear route/threshold, more layered stands;
forest still needs material/ground-life work and less crude boulders. Separate
material step now blends meadow pigment into muted wet needle/litter tones
from z -96 to -114 with a low-frequency boundary. Same mesh/height/collision;
no texture or lighting change. JT9yA7 PASS 48/48, main inspected depth/left and
found the old green road verge no longer matched the forest soil. Applied one
shared forest-weight function to terrain and road-edge pigment; re-exported
and reimported. Current terrain GLB cfca1654a9b866a0909f469b8d2408a8fe5929c7b03ed8104c293db0428a50a9;
registry 55/55, build 0/0. Integrated /private/tmp/urman-kara-integrated-stand.rjpFSt
PASS 48/48, exit 0. Main inspected depth/right/back: road-edge green mismatch
removed, view back to the same village retained. Forward/left composition was
also inspected in the preceding geometry/material stages. No new physical-walk claim:
last 335.27 m result applies to the unchanged route/collision before this
presentation-only pass. Art gate stays REWORK. Cleanup removed 129 older own
PNGs (121.12 MiB), retaining five Kara comparisons per stage. Free space was
1.6 GiB during the pass; no SDK/cache/user-file cleanup or deletion attempted.
JT9yA7 intermediate also trimmed to five Kara comparisons. Final full series
retained. All processes exited and verification override removed. Next scope:
resolve crude boulder/ground-contact forms and review remaining village spatial
weaknesses; do not keep adding more evenly repeated conifers as an art solution.

Current Kara ground integration: matching Blender/C# terrain now has four low
asymmetric forest shoulders, with zero added height inside x +/-3 m and before
z -90. Regenerated terrain Blend/GLB; registry 55/55 passes. Suppressed imported
bank ground pedestals, retaining the common visual/collision heightfield.
Initial core-only triangle probes missed a second live owner. Whole-world
probes proved the remaining foreground mounds belonged to the logical
kara-urman-edge/KaraForestEdgeAuthoredKit benchmark copy (SplayedShoulder and
RidgeContact), not the modified core. Suppressed that duplicate presentation
using the existing logical-zone policy; narrative targets remain mounted.
Capture 2joNqh passed all 48 frames; main inspected forward/depth and rejected
remaining triangular root plates. Replacing those at their existing footprints
with low bent native wood segments seated to Ground, suppressing RootWall
ground plates too. Integrated /private/tmp/urman-kara-grounded-roots.k452VQ
PASS 48/48, eight zones, exit 0; main inspected depth/left/right/back and the
prior-stage forward comparison. Ground wedges/triangular root plates removed,
but exposed wood still repeats and the forest floor/stands remain too sparse.
Do not equate this bounded cleanup with the requested production art standard.
Physical walkthrough on the changed heightfield (isolated profile, true
headless/no audio) PASS 335.27 m, exit 0, FAP/save/resume, holding detours and
completed Kara cliffhanger. Main also inspected the final forward frame.
Diagnostic harness filter/rays removed; build 0 warnings/errors, registry
55/55, graph update and diff check pass. Cleanup removed 198 redundant own
PNGs (182.48 MiB) from five explicitly identified earlier capture folders,
retaining their zone comparisons and latest full 48-view set. Art REWORK.
All processes exited; verification override removed. Next concrete production
work: restructure sparse Kara forest-floor/stand composition and replace the
remaining repeated root/log rows and crude boulders, not another brightness
pass. No human/cultural/language/platform gate closed by this test.

Current shrub geometry slice: previous turn was progress (full holding and
335.27 m physical route). One-frame diagnostic (not full capture acceptance)
at zirat_forward pixel (320,410), viewport-scaled ray/triangle intersection,
identified AgentB_PlantedFoliage source AB_foliage_shrub. Existing C# shrubs
already use open leaf sprays; did not rewrite the wrong generator. Blender
shrub_variant still used three closed ring-profile lobes, ignoring most of
radius. Reused current birch leaf-shoot geometry at shrub scale, added thin
woody stems, retained Shrub_1..3/Lobe names and placement plan. Removed unused
closed-canopy profile table/builders. Native forceReadableName preserves part
names for regional bark/leaf grading on repeated planted copies.
Initial Blender run caught Vector.length called as a method; corrected property.
First exported/captured candidate Kk0nvd PASS 48/48 technically, but main REJECTED
floating branches/displaced shrub composition: new branch object rotation was
being applied after preview-grid translation. Now bake each branch before that
translation via existing bake_transforms. Added preview-cell world-coordinate
assertion to this same generator to catch recurrence. Corrected export succeeded;
First assertion observed stale Blender matrix_local (before depsgraph refresh),
not escaped exported geometry. Added view_layer.update before grid placement;
same bounds now pass. Re-export byte-stable, source Blend unchanged; correct
GLB eaf3ed25d4e01fa026fb09a42a9abddffe2684dddfcf18b51a84051e3ea0fdea.
Diagnostic capture edits removed. Build 0/0; import and registry 55/55 pass.
Final /private/tmp/urman-shrub-integrated.IjNaSJ PASS 48/48, exit 0. Main
confirmed the branches/shrubs returned to their anchors, but found hovering
ground contact. BuildPlantedFoliage calculates groundY/target.Y yet copy.Y used
only sourcePosition.Y. Corrected to target.Y + sourcePosition.Y * verticalScale:
the same terrain root/scale now applies to all parts of each planted variant.
Build 0/0; /private/tmp/urman-foliage-seated.0Um1hh PASS 48/48, eight zones,
exit 0. Main inspected actual zirat forward, arrival, FAP left and Kara forward:
the green closed shrub solid is replaced by low open foliage; airborne branch
regression gone and plant roots follow terrain. Remaining crude stone/ground
forms, repeated houses and empty plots still fail art acceptance. Graph/diff
checks pass; override removed after exit. Latest 48-frame set retained; Kk0nvd,
IjNaSJ and previous T06eLW retain five comparisons each. No new physical-route
claim; planting plan and collision unchanged. Next major task remains authored
ground/plot composition, not more shader parameter or repeated capture loops.
Cleanup removed 129 redundant own PNGs, freeing 119.63 MiB. No Godot process
left running; diagnostics removed from the existing capture harness.

Current western holding slice: previous turn was progress (material evidence
and terrain-seated cemetery path). Replaced ZiratWestLateralFence crossing the
dwelling footprint with a complete five-run boundary, x -38..-19 / z -73..-54,
3.5 m open entrance at z -65..-61.5. Reused terrain-following fence builder with
explicit picket infill, existing full-scale shed and woodpile kit components;
one terrain-following Curve3D access from the road. No new asset/source texture,
collision or narrative owner. Two close capture poses added to existing harness
(48 frames, same eight zones). Build first caught missing height-field namespace
in these poses; fixed import, build 0/0. Capture QcCOPF PASS 48/48; main inspected
entry/return: continuous boundary works, but path ended at window facade.
Source dwelling has side seni at local Blender (3.75,1.95); rerouted access
around north corner to its existing door, not a new doorway. Existing physical
walkthrough now detours via gate and seni approach and returns to the story road.
Build 0/0; physical GPU run /private/tmp/urman-zirat-holding-walk.Pso0ug PASS
335.27 m, exit 0, including gate/seni detour, street save/resume and cliffhanger.
Main inspected the actual door approach (-28.36,-67.44) and return through
gate; door reached without teleporting, no blocker. Graph/diff check passed.
Final corrected-path presentation capture /private/tmp/urman-zirat-holding-final.T06eLW
PASS 48/48, eight zones, exit 0. Main inspected final entry view: path turns
toward the side seni instead of terminating at the window facade. Override
removed after process exit. Retained final 48-frame set, six physical movement
frames and three earlier plot comparisons; deleted 91 redundant own PNGs,
freed 80.48 MiB. No tests running in background.
Art still REWORK; new plot is not whole-world acceptance and presentation
fences do not imply collision coverage. Eastern plots and repetitive house
silhouettes remain unfinished; do not turn this into more micro-material passes.

Current zirat material slice: previous turn was progress (scale cleanup and
five-direction evidence). Temporary mesh-triangle ray probes in the existing
capture identified the white back-view stripe as WetVillageRoadReturnCulvert/
CulvertStoneCrossing_DampBed_LOD0, material Culvert_StoneShadow; depth stripe
is ZiratAuthoredPathEdge/ZiratPathEdge_PathRibbon_00_LOD0, material PathDirt.
Both remained imported StandardMaterial3D outside the current painterly tables.
Probe initially used output pixels without viewport scaling; corrected using
GetVisibleRect().Size before accepting hits. No production edit based on the
incorrect initial rays. Added PathDirt and both Culvert stone materials to
their existing grading owners. Capture Ix8Zox PASS 46/46; main inspected
back/depth and confirmed the white stripes replaced by earth/stone response.
Extended same kit grade to LeafLitter/DitchGrass/RoadGrass/ZiratGrass (source
roles checked; markers/inscriptions untouched). No geometry, light, collision
or narrative changes. Temporary diagnostic removed. Build 0/0; final capture
/private/tmp/urman-zirat-groundgrade.33bkLV PASS 46/46, exit 0. Main inspected
zirat forward/depth plus arrival/Kara: path/leaf-litter/grass no longer white;
the separate pale stone grouping still needs art work. Then conformed the
existing imported ZiratPathEdge ribbon vertices to the shared height field,
preserving its outline, width, authored relief and material; regenerated
normals, no source asset or collision mutation. Initial edit matched another
components[8] call; build caught undefined pathPlacement. Corrected exact
ZiratAuthoredPathEdge call; final build 0/0. Final geometry capture
/private/tmp/urman-zirat-seated.4Ni0S9 PASS 46/46, exit 0; main inspected
depth/right. Flat anchor replaced by terrain-following path, but scene remains
artistically primitive: lumpy ground dressing, repeated facades and empty plots.
Graph update/diff check pass. Diagnostic and isolated override removed.
Four intermediate sets retain five zirat views each; removed 164 redundant
PNGs, freed 151.85 MiB. Final seated set retains all 46. No physical-route
rerun or new traversal claim for this presentation-only slice. Next substantial
work must improve cemetery-edge ground geometry and inhabited plot composition,
not continue brightness tweaks or treat these material fixes as an art gate.

Current zirat scale slice: previous turn was progress (terrain collider ownership).
Removed five presentation-only camera-prop assemblies: forward-west .34 house
and fence, forward-east .34 shed and fence, east-horizon .28 house/fence,
east-field .34 house/fence and west-field .30 house/fence. Full-depth
ZiratWestLateralHouse/ZiratEastLateralHouse and existing cemetery boundaries
remain; no source assets, markers/inscriptions, gameplay or collision edited.
Build 0/0; capture /private/tmp/urman-zirat-scale.vlkuel PASS 46/46. Main
inspected all five zirat views: miniature houses gone, but two kit-mounted
ZiratDistantVillageMass instances still expose flat wall/roof blocks and
faceted canopy lumps near the camera. Source exporter confirms these are
backdrop house/canopy families, not markers. Suppressed both exact
ZiratAuthoredDistantVillageTransition[East] subtrees through existing protected
presentation check; full-depth lateral houses retained. Build 0/0; final
capture /private/tmp/urman-zirat-world.UExIO0 PASS 46/46, eight zones, exit 0.
Main inspected forward/back/left/right/depth: backdrop blocks removed and
full-depth houses retained. Art remains REWORK: exposed white slab/path
geometry, repeated houses, faceted shrubs and unstructured empty plots are
still conspicuous. No new physical-route claim: presentation-only removal,
prior terrain-aligned 271.61 m unchanged. Graph update passed. Override removed
after process exit. Keep final 46-frame set; intermediate scale capture retains
only five zirat comparison views. Next inspect ownership/source of the white
roadside slabs before changing them; preserve grave markers and cultural content.
Cleanup removed 41 intermediate PNGs (37.96 MiB); final comparisons retained.
Initial source check: exported ZiratPathEdge uses PathDirt, marker mounds use
DampEarth/DampEarthDark; GLB materials carry nonwhite base colors. Therefore
do not assume white geometry is intentional stone or blindly recolor markers.
Next use an exact runtime mesh/material diagnostic to distinguish these kit
surfaces from other overlapping roadside presentation before a targeted fix.

Current collision slice: previous turn was progress (continuous path + verified
yard detour, exposing y=0 mismatch). Removed SharedVillageGroundTraversalCollision
and flat connector RoadTraversalCollision bodies, plus their unused builders.
Disabled connected benchmark Ground/Road surfaces for village/zirat and
Ground/PathNear/Middle/Far for Kara with existing subtree suppression; isolated
benchmark source scenes and interior floors remain unchanged. AgentB_TerrainCollision
is the active exterior ground owner. Connector collision count now zero;
Act1DemoLaunchSmokeTest exact assertion updated from obsolete BoxShape3D to
active ConcavePolygonShape3D and absence of legacy plane. First physical route
PASS 271.61 m, exit 0. Added height alignment check at (23,-12) to that existing
walkthrough so future y=0 fallback cannot pass unnoticed. Final build 0/0.
Launch test PASS exit 0. GPU physical run active in
/private/tmp/urman-terrain-walk.aNRYjT: approach frames now show varying
player Y (-.268 at arrival; -.567 on FAP branch), not y=0. FAP entry succeeds.
Final GPU physical PASS 271.61 m, exit 0, including house/FAP floors,
yard detour, street save/resume and cliffhanger. Yard alignment assertion:
outward player=-.224/ground=-.220; return player=-.227/ground=-.228 (metres).
Main inspected actual FAP approach and yard return frames. No ERROR/FAIL/
walk-obstacle. Graph update/diff check pass; override removed after exit.
This fixes the proven flat-ground override, not all collision/comfort/art gates.
Next production work returns to visible world quality: path-to-road seams,
miniature adjacent facades and zirat's toy-sized buildings still read as
prototype; do not restart cosmetic lighting or broad verification loops.

Current access-path slice: previous turn was progress (corrected winding,
removed entrance prop, close capture evidence). Replaced the two intersecting
path strips with one Curve3D centerline through (19,-22.5), (24,-17), (23,-12),
(21,-8.5), baked at .5 m cross-section spacing by the existing ground-surface
builder. Shared height field and generated normals retained; no new terrain
or narrative owner. Existing physical walkthrough now detours into the holding
and back during the FAP return, then continues save/resume and full story.
Build 0/0, diff check pass. GPU physical run PASS 271.45 m, exit 0, no
ERROR/FAIL/walk-obstacle, including yard detour, street save/resume and final
cliffhanger. Main inspected movement frames at (24,-17)/(21,-8.5) in
/private/tmp/urman-yard-walk.zokRB6. Static close curve capture PASS 46/46,
eight zones, exit 0 in /private/tmp/urman-yard-curve.KmR44T; main inspected
entry/return and confirmed the angular overlap replaced by a continuous bend.
Graph update/diff check passed; override removed after both processes exited.
Retain five physical frames and four previous A1mXy2 comparison frames;
current KmR44T set retains all 46. Yard remains artistically REWORK.
Important newly inspected root cause: actual player Y remains ~0.00039 across
this lowered yard. BuildConnectorPresentation still creates an 86x208 flat
SharedVillageGroundTraversalCollision at y=0, plus flat RoadTraversal strips,
alongside AgentB_TerrainCollision. Visible ground/paths are lower. Do not claim
surface-aligned walking based on this PASS. Next production task: reconcile
active ground collision ownership with shared terrain, including remaining
benchmark Ground/Road bodies in zirat/Kara. Act1DemoLaunchSmokeTest currently
asserts the obsolete ground BoxShape3D size/location: update its exact contract
with the intended production change, not by retaining an active flat fallback.
Inspect all ConnectorTraversalCollisionCount consumers first. Preserve interior
floor and stair collision, named transitions, RuntimeBridge and user saves.

Current ground slice: previous turn was progress (fence owner correction).
AddVisualLandformSegment's constant baseY is intentional for existing berms,
but wrong for the two new holding access paths. Added explicit terrain-conform
option to the existing surface builder, enabled only for EastStreetPlotEntryPath
and EastStreetPlotHousePath. Vertices sample the shared world height field,
convert back into mesh-local space, and regenerate normals. Other banks and
source terrain/collider/runtime ownership unchanged. Existing capture harness
now adds east_holding_entry/return at sampled ground + player height: 46 frames,
same eight zones. These close views address the previous evidence gap; they
remain waypoint presentation checks, not walking. Build 0/0 and diff check pass.
First 46-frame capture /private/tmp/urman-yard-ground.IgcWz1 passes technically,
but close views reveal EastStreetMidShed (scale .4) at (23.3,-15.8) inside the
holding entrance. A temporary named-node diagnostic confirmed its path;
removed that duplicate instance, retained PerimeterEastStreetShed and the
adjacent facade. Diagnostic removed from harness. Paths still invisible:
AddVisualLandformSurface used upward geometric cross-product winding, whereas
Godot top front faces require clockwise winding. Reversed its two triangle
orders, keeping upward normals. This shared fix also exposes existing berm
surfaces. Capture /private/tmp/urman-yard-surface.ooMyhT PASS 46/46 but main
REJECTED its art regression: exposed constant-height ribbons cut across
arrival/zirat and beside the holding as floating slabs. Shared terrain remains
the relief owner; old unconformed overlays now Visible=false, while explicitly
conformed paths remain. Their winding stays corrected, not hidden by backwards
faces. Build 0/0; integrated capture PASS 46/46, eight zones, exit 0,
/private/tmp/urman-yard-integrated.A1mXy2. Main inspected entry, arrival forward,
zirat left and Kara forward: exposed floating-strip regression gone; remaining
paths visible, mini shed removed. No ERROR/FAIL; graph update/diff check pass.
Override removed after exit. Earlier diagnostic/winding captures retain only
four comparisons each; latest integrated set retains all 46.
Removed 126 redundant PNGs from those three own runs, freeing 115.90 MiB.
No new physical walk claim: collision/narrative unchanged. Overall art rejected
as still primitive: irregular path joins, empty grounds and zirat mini props.
Current path junction is still angular and ends short of the dwelling porch.
Next production change should integrate a continuous access path into the
existing terrain pigment/road source and physically test entry into the yard;
do not mistake close camera placement for a traversable yard or completed art.

Current boundary slice: previous turn was progress (consolidated holding and
verified physical route). Added terrain-following side fences to the holding
around MainStreetEastNearMidHouse, joining front/rear edges; west edge x=15.5
clears the retained shed footprint. Front-west picket center shifted to x=18.2
so it meets that edge; entrance toward (24,-17) remains open. Removed the
diagonal MainStreetEastNearMidBank crossing the house/yard. Existing fence
builder, no source asset/runtime changes. First capture passes, but its main
street right view exposed the actual orphan gate source: Gate_HouseA2_ at
(10.8,4.1) survived suppression of HouseA2_. Extended the existing exact
suppression prefix list to Fence_HouseA2_/Gate_HouseA2_ only; other parcel
families remain unchanged. This also excludes their decorative collision
from BuildArchitectureCollision. Build 0/0. Reusing the intermediate output
directory correctly failed the harness's overwrite guard, exit 1; no frames
overwritten. Capture /private/tmp/urman-orphan-gate.txwgmE PASS 44/44 and
main inspected main_street_right: orphan gate removed, one low beam remains.
Physical headless route PASS 230.88 m, exit 0, including street save/resume.
Root cause of remaining beam: AddVisualFenceRun added LowerRail as a sibling
after exact legacy suppression lists had been written for Rail and posts.
Now LowerRail and arrival slats belong under the Rail mesh (identity transform),
so existing replacement ownership hides the complete fence infill. No new
suppression list or special per-location patch. Build 0/0. Final visual-only
capture PASS 44/44, 8 zones, exit 0 at /private/tmp/urman-fence-owner.b3FHI5;
main inspected main_street_right and fap_exterior_left: orphan low beams
are gone in both same-position views. No ERROR/FAIL. Previous physical
pass precedes this presentation hierarchy fix, not a newer full-play claim.
Keep only main_street_right/fap_exterior_left/arrival_forward comparison PNGs
from djxJC5/arg6Rl/txwgmE; their original receipts are no longer full retained
image sets. Removed 123 redundant PNGs, 112.95 MiB, retaining three comparisons
per intermediate directory and all 44 final frames. Override removed after
exit, graph update/diff check pass. Art remains REWORK: clearing orphan props
does not make these flat plots lived-in. Next production step: terrain-conform
the actual holding access paths (AddVisualLandformSegment currently overwrites
endpoint Y with constant baseY), then shape purposeful yard ground using the
existing terrain source/height-field contract. Avoid another scatter pass or
claiming every newly drawn path is visible without close first-person evidence.

Current slice: consolidated the holding north of the FAP branch around
MainStreetEastNearMidHouse. Removed PerimeterEastStreetFacade,
EastStreetNearParcel and the duplicate MainStreetEastNearMidShed; moved
PerimeterEastStreetShed to (18.4,-11), uniform .95 scale. Front fence runs
at (20,-17)/(28,-17) leave an entrance; rear boundary moved behind the house
to z=-3. Existing landform helper supplies two short entry path segments.
No asset, collision or narrative owner changes. First GPU walk FAILED:
both front fences requested the same destructively detached source node
within one imported parcel. Split the two placements into independent
existing helper calls; build passes 0/0. Corrected GPU walk PASS 230.88 m,
exit 0, no ERROR/FAIL/walk-obstacle; includes street save/resume and final
cliffhanger. Main inspected corrected branch and reverse movement frames in
/private/tmp/urman-branch-holding.aUwO85. First failed run was overwritten,
not accepted. Graph update and diff check pass. Dedicated capture PASS 44/44,
8 zones, exit 0 in /private/tmp/urman-holding-review.djxJC5; main inspected
fap_exterior_back and main_street_right. Keep this complete presentation set
and five actual-walk comparison frames. The view still shows detached fence
runs/gate fragments beyond the street house and flat yard ground: REWORK,
not a completed artistic holding. Next concrete step: resolve the remaining
EastParcel gate/fence and plot-boundary ownership in main_street_right into
connected yard edges, preserving entrances; do not add another facade layer.
Repetitive facades and detached curb strips also remain open. Test override
removed after both processes exited; no hidden running game is promised.

Latest slice: previous turn was progress (duplicate parcel removal). Rebuilt
OutbuildingShed_Low through the existing gable-shed builder with an explicit
name prefix, clearing obsolete stacked-box children; removed the obsolete
joinery builder. Same component root/placement API, no new kit or texture.
Existing material grade now binds URMAN_Wood_Weathered/WetShadow, including
the shared shed's walls. First GPU captures exposed inward roof-top normals
in variant_roof; reversed shell winding and added the two top-normal assertions.
Canonical shed and the existing variant sheds now share the correction.
Blender validation: 781 meshes, 21,643 triangles, 16 materials, no degenerates;
registry hashes updated; export/import, build 0/0 and registry 55/55 pass.
Main inspected FAP-left, main-street-right and Babai-yard-back. Same-view
roof comparison: urman-gable-sheds.FyRHLg (inward normals) versus
urman-shed-roofs.gMQsrp (corrected). Both 44-frame captures exited 0.
Removed the half-scale FapReverseEastDomesticShed camera prop because the
clinic already has a service shed; raised MainStreetEastNeighborShed to .95
uniform scale instead of compressed .68/.66/.76. Build passes. Final GPU
physical route with these placement changes PASS 230.88 m, exit 0, including
street save/resume; no ERROR/FAIL/walk-obstacle. Main inspected actual
fap-branch-17--21,5 and fap-return-10--17 in urman-shed-walk.kHJebx. Corrected
roof material visible in the previous 44-view source capture gMQsrp, but that
capture precedes the last two placement changes; do not label it the final
whole-world candidate. graphify update/diff check pass; override removed.
Broader shed placement scale, yard ground composition and whole-world art
remain open. Next concrete location: PerimeterEastStreetShed at (20,-20),
still an undersized prop beside FAP approach. Audit its whole plot and nearby
dwelling before resizing/moving; do not blindly clamp every shed's scale,
because expanded footprints caused the previous overlapping-house defect.

Latest slice: previous turn was progress (FAP spacing + diagnosed road glare).
Fresh 44-frame review exposed remaining overlapping roof/facade planes in
fap_exterior_back. EastParcel/MainStreetEastNeighborFacade at (9.8,-10.5)
coexisted with camera-specific MainStreetEastLateralParcel at (11,-12) and
ForwardEastParcel at (8.4,-11.5). Removed those two duplicate visual parcel
assemblies (house/shed/fence), retaining the main dwelling and its yard.
Removed EastStreetFarHouse at (39,-18), which overlapped the retained
FapRightFieldHouse plot. Source assets, interactions and colliders untouched.
Suppressed FapAuthoredBirchShrubMass preview polyhedron crowns: existing
FapClinicNearBirch remains the grove's detailed tree. No replacement texture.
Build 0 warnings/errors, graphify update and diff check pass. Final hidden
Godot presentation capture PASS 44/44, 8 zones, exit 0, no ERROR/FAIL.
Main inspected fap_exterior_back/main_street_right before/after parcel removal
and fap_exterior_left after grove suppression. Final full capture retained:
/private/tmp/urman-fap-grove.DLFSAv; baseline selected comparisons under
/private/tmp/urman-world-review.HCnM6D and urman-parcel-owner.xhpjlI.
No new physical-route claim: prior 230.88 m movement evidence is unchanged,
this pass removes presentation-only instances. Temporary override removed.
Art still REWORK: the shared OutbuildingShed_Low looks like a flat-roofed
stack of boxes and is repeated beside the road. Next production step should
rebuild that existing source component (author_shed_volume/author_shed_joinery
in urman_village_exterior_kit.py), preserving its placement contract and
closed volume, then review several real placements. Do not add more little
camera-specific facade clusters to hide remaining empty parcel ground.

Latest continuation: previous turn was progress (physical return + porch).
Readback corrected the proposed FAP diagnosis: AddDistantHouse already routes
to full-depth dwellings with minimum .9 scale, not old box models. Their old
miniature placement spacing remained. Moved four FAP neighbors to separated
plot anchors and adjusted two boundary runs; no new meshes or route changes.
Approach/entry comparisons in urman-fap-parcels.qw9bZ7 inspected. Parcel
composition is still REWORK; no claim of full side/back acceptance.

The pale foreground triangle was traced with a temporary screen-pixel mesh
ray probe: Road_FapBranch, 2.694742 m from the camera, material AB_road_pigment;
its actual GLB vertex RGB range .06838–.15487 excludes white vertex paint.
Wet-road roughness/specular shared a nearly water-film grade. Split the
existing wet_road semantic grade from wet_ground: roughness .82, specular .12,
wet grade .72. Separate puddles unchanged. Exact same-position GPU frame in
/private/tmp/urman-road-glare.7rPPaC confirms the white triangle disappears.
Camera, lighting and road geometry unchanged in this comparison. Probe was
removed after diagnosis. GPU physical route PASS 230.88 m including return
save/load, exit 0, no ERROR/FAIL/walk-obstacle. Final compile after removal
of diagnostic-only code passes 0 warnings/errors. graphify update and diff
check pass; temporary override removed. Cleaned 54.6 MiB of redundant own
captures, retained comparisons. This is not visual approval of Act I.
Next: continue FAP parcel side/back composition and inspect broad street/zirat
road response with this grade. Do not repeat the disproved missing-mesh theory
for the pale triangle or claim all house/parcel overlaps have been audited.

Implemented: FAP return uses an effect-free official-leave-clinic interaction
to switch presentation to the village at its FAP apron; the existing
official-to-internal-register target moves to the house exterior. Narrative
scene/evidence remain unchanged during walking; no new scene/state owner.
Existing physical test now walks the reverse FAP branch and house path.
Content check/compilation and game build pass (0 warnings/errors). True-headless
and GPU physical route pass 230.88 m to the cliffhanger. GPU run additionally
saves/loads walk-fap-return on the street, asserts unchanged position, scene,
zone and house interaction, then walks HousePathAxis. No ERROR/FAIL or
walk-obstacle in final log. Main inspected fap-return-10--17 and
house-return--19-0 in /private/tmp/urman-fap-return.gfgWMU. Their art is still
REWORK. Temporary profile override removed. No image generation.

FAP construction/material follow-up: rebuilt the existing three porch steps
as solid grounded risers and put the threshold on the deck at the door.
Same mesh/triangle count; extended existing connected material bindings for
stone, door, boards, metal and glass. Light/camera unchanged. Blender export,
import, build (0/0), registry (55/55), graphify update and diff check pass.
Final GPU physical route still passes 230.88 m with street save/resume, no
ERROR/FAIL/walk-obstacle. Actual same-player approach/entry comparisons in
/private/tmp/urman-fap-entry.FtbqaV vs urman-fap-walk.xfC8t4 inspected by main.
White placeholder-looking porch/board/door corrected, but FAP parcel is
still visually REWORK. This is not a completed world-art milestone.
No Godot process left running; temporary override removed. Content pack
fingerprints changed with the new interaction; old-build save compatibility
was not established and existing user save files were not touched.

Next concrete production step: rebuild the FAP neighboring parcel composition
in BuildCoreFapExterior/BuildAct1FapClinicKit. Replace the nearby DistantHouse
box facades with existing full-depth authored dwellings at distinct, correctly
scaled plot positions; remove proven overlapping presentation buildings,
connect plot boundaries to clinic/service yard, preserve FapBranchAxis and
both walking directions. Inspect side/back and the actual approach, not a
new fixed showcase angle. Also identify the remaining pale foreground polygon
in entry capture before claiming this approach clean. Do not repeat lighting
or isolated prop tweaks as a substitute for this geometry/composition work.

User revision: continue without image_gen for now. Paintover/API availability
is no longer a blocking dependency; do not retry image generation or use paid
fallback. Goal API confirms active. Next integrated gameplay slice: move the
FAP exterior transition from the main-road shortcut onto its actual authored
approach, and physically walk that branch before entering the clinic.

FAP approach implementation: connected-world RoadToFap now uses the existing
FapBranchAxis terminal apron (28,-26.2), not the shortcut at (3.8,-8.2).
Same interaction ID, runtime gate and interior target remain authoritative;
isolated benchmark unchanged. Existing physical test asserts the anchor and
walks the branch axis before entering. This exposed invisible old benchmark
fences and houses: duplicate default node names became @StaticBody3D@...,
evading existing Fence/House/ Foundation numeric-family suppression. Fixed
the two constructors with forceReadableName, preserving standalone collisions
while connected suppression now reaches the intended repeated families.
Added exact last lateral collider reporting to the existing walk test; no
new test framework. Initial failures and the diagnostic compile typo were
repaired; final build 0/0. True-headless route PASS 171.03 m, GPU physical
route PASS 171.04 m to cliffhanger; no ERROR/FAIL/walk-obstacle in final log.
Main inspected fap-branch-17--21,5 and actual entry-apron PNGs under
/private/tmp/urman-fap-walk.xfC8t4. Road-to-clinic travel now exists, but the
porch steps/materials, adjacent small buildings and parcel composition remain
REWORK. This does not prove all other production scene transitions are now
continuous walks. Next investigate FAP-to-house return travel and retain its
existing narrative owner; do not describe the old transition placement as a
physical walk. graphify update and git diff --check passed; override removed.

### Active objective — user revision, 2026-09-05

Завершить реализацию MVP всего Акта I «УРМАНА» по актуальному production
tracker: от запуска новой игры и приезда Айдара до клиффхэнгера «НЕ ОТВЕЧАЙ».
Добиться цельного, увлекательного, устойчиво работающего и визуально
впечатляющего first-person опыта. Устранить вид пустого прототипа,
примитивной геометрии и случайного набора ассетов. Выполнить все задачи,
которые можно объективно завершить имеющимися средствами; проверить
интегрированный результат. Для задач, требующих внешнего специалиста или
решения пользователя, подготовить конкретный материал для приёмки и честно
оставить соответствующий gate открытым. Не выдавать автоматические проверки
за художественную или человеческую приёмку.

Дополнение пользователя о способе производства (2026-09-05): достичь
художественного качества через осознанный выбор производственного подхода.
Не ограничиваться программной генерацией примитивов. Использовать Blender,
визуальное моделирование и сборку сцен, подходящие лицензированные ассеты,
процедурные инструменты и другие доступные способы там, где они дают
проверяемое улучшение результата. Независимый арт-директор оценивает реальные
кадры и даёт один предметный brief на законченный участок; повторный review
проверяет его реализацию. Старые bounds/counts не ограничивают художественную
переделку; сохраняются игровые контракты, не плохие силуэты.

Без ограничения времени. Самостоятельная реализация в основном checkout,
без Sol Advisor и обязательного Luna-only. Не передавать управление после
мелкого исправления: продолжать законченные производственные этапы до
кандидата для приёмки, настоящего блокера или технического/ресурсного предела.
Первый `create_goal` отклонил замену из-за существующей активной цели;
после пользовательского обновления `get_goal` подтвердил новую формулировку
и статус `active`. Дополнение о производственном подходе сохранено здесь:
доступный API не умеет редактировать objective активной цели; `create_goal`
не заменяет незавершённую цель, `update_goal` меняет только статус. Не отмечать
цель завершённой ради её пересоздания.

Immediate outcome: integrated arrival/street/yard art candidate with
inhabited parcels, architectural depth and continuous ground; inspect 360°
and physical traversal before propagating its standard across Act I.

Current implementation receipt (same continuation):
- Visible village dwelling source expanded from ~2.4 to ~5.2 m depth, with
  corresponding rear roof/gable/plinth/window placement, four side windows
  and continuous foundation. Near arrival pair now uses .95/.98 scale;
  shared dwelling mounts sample the existing terrain height owner.
- MainStreetEastNeighborFence/Gate moved from the actual central road to
  the east parcel edge; no collision or narrative owner changed.
- Terrain_Main uses interpolated GroundPigment vertex colors instead of
  material-per-quad islands. Initial glTF export put pigment in COLOR_1 and
  rendered white ground; explicit material vertex-color binding corrected
  COLOR_0. White-ground candidate rejected, not acceptance evidence.
- Agent B building windows now have four open frame members rather than a
  solid box covering glazing. Side windows reuse the same geometry function;
  rear-facing box bounds are sorted. These are not the near arrival houses.
- Independent delegated wet-road source fix reverses only the retained grass,
  fern and shrub height channel, with one-time source markers and winding
  correction. First upright sedge remained blocky; replaced with 96 curved,
  tapered two-sided leaves within the existing 24 child meshes. Main inspected
  source and the integrated arrival frames; vegetation quality still open.
- Shared visual fence rails/posts now follow the existing terrain owner.
  Independent review caught inward rail winding; main reproduced the normal
  direction in Godot and reversed indices before the final capture.
- Physical CharacterBody/input/raycast walkthrough: PASS, 137.60 m, all
  transitions to kara_urman_night, cliffhanger completed. Checkpoint smoke:
  PASS rolling checkpoints, exact restore, terminal once. Both ran under
  verified isolated user directory URMAN-verification-20260905; temporary
  override/probe and generated test profile removed. Tests precede the last
  presentation-only side-window/foundation/foliage changes.
- Completed capture /private/tmp/urman-arrival-stage.RLFTTW: build 0/0,
  44 unique frames across 10 visual stops. Main inspected arrival left and
  babai yard left: rails fixed, but repeated miniature-looking parcel variants,
  empty ground and disconnected shoulders still reject artistic acceptance.
  Removed generated facade appliques and added front/rear attic boarding.
  Shoulder-only projection did not fix the fragmented road; rejected and
  removed that implementation. Continuous existing Road_Main now owns the
  village/zirat road with Road_FapBranch/HousePath/KaraPath for branches.
  Legacy wet-road crown/rut/shoulder/ditch components remain in the import
  contract but are presentation-suppressed; their vegetation remains visible.
  Road profile is shallow and uses interpolated RoadPigment rather than
  hard material-per-strip bands. No physics/narrative change.
  Latest /private/tmp/urman-road-pigment.lCOLY5: build 0/0, 44 frames PASS.
  Main viewed arrival_forward: continuous earth route replaces polygon
  islands and black stripes. Viewed zirat_forward: duplicate core zirat kit
  road strips still visible, so later-zone ground acceptance remains open.
  Retained before: /private/tmp/urman-zirat-owner.jjd9TM and
  /private/tmp/urman-arrival-stage.RLFTTW. Intermediate batches removed.
- After the road/architecture integration, physical walkthrough repeated:
  PASS 137.60 m, main-menu new game through final cliffhanger; checkpoints
  written to isolated URMAN-verification-20260905 profile. No user saves
  touched. Temporary override removed after process exit.
- Removed another six superseded own capture batches (~197 MiB), in addition
  to the 93 MiB noted below. User source/sessions/toolchains untouched.
  Later removed six more intermediates (207,928 KiB, ~203 MiB); retain only
  meaningful stage comparisons, not each cosmetic/rejected iteration.
- ART-002/003/007, Z01/Z02 remain PARTIAL/REWORK. Repeated facades, open plots,
  crude ground intersection/puddle contours and pale zirat trees remain
  visible; do not claim the first art checkpoint accepted or close the goal.

Current continuation evidence (supersedes intermediate claims below):
- Pixel-ray ownership proved the remaining strips came from logical
  zirat-return-road/ZiratRoadsideAuthoredKit, not only the core kit. The old
  suppression sat in an unreachable second `else if (zirat_road)`. Merged
  the branches, hid logical Ground/Road visually without disabling collision,
  and suppressed eight duplicate core ribbon meshes. Culvert restored to
  its roadside offset. /private/tmp/urman-zirat-unified.pLZILb and
  /private/tmp/urman-gardens-birches.OlsD8a confirm continuous zirat road.
  Temporary ray probe removed; its viewport coordinates required 1920x1080
  logical-to-1280x720 capture conversion. Earlier ownership claim was incomplete.
- ZiratBirchShrubMass now has tapered branched trunks and volumetric leaf
  crowns. First flat-envelope candidate rejected; revised 23-mesh component
  passed independent spec and code-quality review, root source/hash/preview
  inspection and integration capture. 145 outside meshes, all nodes/materials
  preserved. Visual quality still unaccepted; not a cultural review.
- Two cultivated garden plots were added inside existing arrival holdings,
  outside road/house corridors. Build/capture pass, but close inspection at
  /private/tmp/urman-garden-inspection.HUpsq6/arrival_left.png hit a house wall.
  Therefore garden placement/visibility acceptance is NOT demonstrated.
  Temporary capture positions restored. No ray instrumentation remains.
- ZiratRouteTraceTag changed from cross-like horizontal board to narrow
  vertical tag, preserving the clue position, interaction and narrative.
  This last presentation change awaits fresh affected build/frame check.

Latest implementation and exact continuation — art-director brief implemented
partially, NOT accepted:
- Independent art director rejected current images: repeated miniature houses,
  unassigned plots, pole-like crowns, flat ground use, disconnected light.
- Replaced four Blender dwelling roots with pierced 20 cm wall reveals,
  vertical windows, narrow joinery, thick roofs and enclosed side seni.
  Main inspected source/front/rear renders. Found and corrected variant
  house/shed/yard overlap; groups now have independently checked non-overlap.
  Main hero portal hides only DwellingFacade_StreetDoorClosed_LOD0; neighbors
  remain closed. Removed obsolete C# facade-overlay windows/beams.
- AddAuthoredHouse now extracts the same source's A/B dwelling only, rather
  than the old HouseA modular shell; normal human scale replaces miniature
  copies. Full-parcel placement still needs spatial review: larger footprints
  can overlap separate adjacent runtime instances even when source groups do not.
- Shared birch/orchard tree now has separate authored primary bough habits,
  tapered branching and hanging leaf groups, seated on actual terrain.
  Day ambient and imported plinth/roof grading unified; image remains too
  flat/bright and orderly, so no artistic acceptance claimed.
- /private/tmp/urman-authored-direction.7kSbz7: build 0 warnings/errors,
  44/44 capture PASS. Main viewed arrival forward/back and yard forward.
  This is presentation evidence, not an accepted finished world.
- Physical walkthrough with these houses/trees: PASS 137.60 m to cliffhanger,
  isolated URMAN-verification-20260905 profile. Override and generated profile
  removed. No original user saves touched.
- Rejected own path experiment: changed Road_HousePath to old
  Act1WorldLayout connector at z=9.5, then actual walkthrough source proved
  AgentBAct1Layout.HousePathAxis (z=-8) is authoritative. Restored original
  HousePathAxis and re-exported terrain. Prior screenshot batch predates
  this restoration; do not describe the mistaken path as an improvement.
  Curved arrival road continuation remains, without changing height/collision.
- Removed ~102 MiB more own obsolete diagnostic PNGs; kept their logs and
  meaningful stage comparisons. Architecture previews use ~10 MiB.

Latest user requests two or more image_gen paintovers of the same current
frame, choose a feasible one, then implement its geometry/material/light
separately in real 3D. imagegen skill read. Input inspected:
/private/tmp/urman-authored-direction.7kSbz7/arrival_forward.png.
Two requested variants: working street vs garden street, invariant camera,
footprints, human scale, central 5.6 m clearance, left yard approach and
Tatar cultural context. Built-in image_gen failed HTTP 404 Not Found;
one bounded retry also failed immediately. NO generated concept exists,
NO paintover selection/implementation may be claimed. No external fallback
API, purchases or uploads beyond the requested built-in generation used.

Next concrete step: restore image_gen service or obtain explicit permission
for skill CLI/API fallback (requires OPENAI_API_KEY), create the bounded
paintovers, inspect/choose one and implement it. Then same-camera real Godot
comparison, physical route and one art-director re-review of that result.
Do not re-audit, invent generated evidence, or run repeated image retries.
Full Act I goal remains active; visual and external human gates remain OPEN.

2026-09-05 continuation while image_gen unavailable — real route defects:
- Previous goal turn made progress (architecture/source/capture evidence),
  not a verified wait. image_gen is a blocked subtask, not the whole goal.
- Existing physical walkthrough now optionally saves nine arrival/house
  frames from actual CharacterBody positions with URMAN_WALK_CAPTURE_DIR.
  No camera teleport, no new suite; normal smoke needs no GPU. Optional
  architecture probe uses local AABB broadphase then actual triangle tests
  at four heights along each leg's start/end segment. This is a diagnostic,
  not a complete swept-volume collision or visual-acceptance claim.
- Before /private/tmp/urman-physical-arrival.Zc9Y2e proved a picket, gate
  post and GateBabai_Leaf crossed the approach despite physical smoke PASS.
  Moved MainStreetForwardWestFence/Gate south of HousePathAxis; gate source
  now rotates all leaf parts around the hinge and opens Babai inward 90°.
  Source assertion checks open leaf clearance. Other gate leaves use their
  existing angle with corrected hinge/brace transformation.
- Triangle probing additionally found ArrivalForwardWestFacade and
  MainStreetForwardWestFacade walls crossing the route. Moved the first
  north 3 m, the second to the south holding at full inhabited scale; moved
  its shed clear and removed the overlapping MainStreet WestParcel copy.
  No collision, interaction or narrative owner changed.
- After /private/tmp/urman-physical-clearance.kIWfqT: nine actual-walk frames,
  zero reported architecture-segment crossings; full physical route PASS
  137.60 m through final cliffhanger. Main viewed axis-2/3/4 frames and checked
  diff. Build 0 warnings/errors; registry 55/55 PASS. Two batches total ~15 MiB.
  Art quality remains REWORK: separate decorative gate alignment, crude props,
  repetitive architecture and weak ground/light composition are still visible.
  This fixes route integrity, not the pending paintover/art direction request.
- Extended the same optional capture to every WalkTo leg. First run failed
  because interaction IDs contained slash/colon filename characters; fixed
  filename encoding in this existing capture owner, then repeated successfully.
  /private/tmp/urman-physical-full.op2sPw contains 24 actual-position PNGs
  (~19 MiB); build 0/0, physical walkthrough PASS 137.60 m to cliffhanger,
  no architecture-segment diagnostics reported. This does NOT prove continuous
  physical travel across zone transitions, which still use production entry
  placement, nor a swept player volume or complete 360-degree acceptance.
  Main inspected route-to-fap and zirat-road-to-forest: facade/ground junctions,
  pale foliage, crude props and a hard forest-ground boundary remain REWORK.
- Resource stop: free disk dropped from ~3 GiB to 1.5 GiB during this run;
  final check after Godot exit showed only 418 MiB free. New capture accounts
  for only ~19 MiB; cause of the larger decrease is unknown. Stop heavy writes.
  Deleted ~69 MiB of own obsolete PNGs from garden-inspection.HUpsq6 and
  road-pigment.lCOLY5, preserving logs and current comparisons. No user assets
  or toolchains touched. Godot exited; temporary game/override.cfg removed.
  Disposable URMAN-verification-20260905 profile remains under 1 MiB.
  Next: recover safe disk headroom and resolve image_gen availability/API
  permission, then execute the requested paintover-to-3D pass. No concept
  was generated or selected, and no artistic completion is claimed.

2026-09-05 next continuation: disk recovered to 3 GiB, later 5.5 GiB;
the prior storage blocker did not persist and the goal stayed active.
Fixed a separately proven Kara ground seam in the existing connected-world
presentation owner: the 38 m benchmark Ground box and PathNear/Middle/Far
were still visible over Terrain_Main/Road_KaraPath. Hide only their visuals
when mounted in the connected world; retain physics and isolated benchmark.
No material, lighting, camera, interaction or narrative changes.
Build 0/0, physical walkthrough PASS 137.60 m, git diff --check clean.
Same physical endpoint compared in /private/tmp/urman-physical-full.op2sPw
and /private/tmp/urman-kara-ground.dxlORx, image
approach-urman.chapter1_interaction_zirat-road-to-forest.png: straight black
ground boundary is gone and the existing forest road is now continuous.
This is direct local geometry evidence, not full terrain/art acceptance.
Pale foliage and crude near props remain. Day-village ownership follow-up
found SetActiveLogicalZone already hides the entire village-main-road
benchmark root; no extra village suppression is justified or added. Requested
paintover selection still awaits image_gen service or explicit API approval.
Temporary override removed after the completed process. graphify update passed;
only the two relevant zirat after-frames retained (1.6 MiB), duplicate captures
from this run deleted. Final free disk recovered to 12 GiB; disk is no longer
a current blocker.

Next material-only continuation: verified the pale right-hand birch group
belongs to the imported zirat transition. GLB uses distinct, correctly
assigned BirchBark/BirchLeaves/ShrubGreen materials (no emissive factor), but
the connected daylight grade omitted these names. Added them to the existing
per-surface grade dictionary using the same palette as Kara/benchmark grade;
no new shader, geometry, lighting, camera or narrative changes. Same-position
Godot comparison against urman-kara-ground.dxlORx visibly removes the pale
PBR canopy mismatch. After: /private/tmp/urman-birch-grade.FIpB1I/
approach-urman.chapter1_interaction_zirat-road-to-forest.png.
Build 0/0, physical walkthrough PASS 137.60 m, no ERROR/FAIL/walk-obstacle
in the full isolated runtime log, graphify update and git diff --check pass.
Art remains REWORK: flat verge, crude foreground fallen-branch shape and weak
spatial composition. These fixes are not the still-unavailable paintover pass.
Foreground ownership investigation corrected the prior label: the object
was Fern_2 at (-3.4,-96), not a fallen branch. Shared _blade computed width
but used literal half-widths .50/.37/.19 m in the first three sections, making
centimetre leaves one metre wide across fern/sedge/grass families. Multiplied
the sections by width and added an export assertion for all cross-sections.
Width-only same-camera capture exposed detached elevated fern blades, so
replaced the three Fern variants with connected arched rachises and paired
pinnae sharing a grounded base; no planting positions or collision changes.
The final geometry (128 meshes, 21,132 triangles) replaces the same GLB and
Blender source; registry SHA updated. Blender source assertions/export and
Godot import passed, registry 55/55 PASS, graphify update and diff check pass.
Physical route PASS 137.60 m; main inspected the actual forest-approach frame
at /private/tmp/urman-leaf-width.jcSeTb/approach-urman.chapter1_interaction_zirat-road-to-forest.png.
The metre-wide silhouette is replaced by a small connected fern. No material
or lighting changes in this geometry pass. Prior wrong fallen-branch diagnosis
was not implemented. Keep only two relevant after-frames, discard redundant
ones from this run. Art remains REWORK; isolated foliage correction does not
solve the flat empty verge. Next needs a composed terrain/understory section,
not another brightness or single-object tweak; requested paintover remains
pending image_gen availability or API approval. No game process or temporary
override remains; latest disk ~17 GiB free.

Paintover checkpoint revalidation after geometry fixes: inspected current
urman-leaf-width.jcSeTb forest-approach frame and requested one built-in
image_gen edit (wet drainage/forest transition; exact camera/road clearance,
no cultural-marker changes). Service again returned immediate HTTP 404.
No output exists; second variant/selection/3D realization are NOT complete.
No paid API fallback is authorized. Stop the succession of isolated art
touch-ups: the next spatial production pass requires the requested concept
comparison, not another cosmetic substitute. Independent UIUX-001/005
readback found the described menu/pause implementation and existing targeted
smokes already present; their PARTIAL status includes device/human lifecycle
acceptance, not justification to add duplicate UI code or rerun green tests.
Await image_gen recovery or explicit CLI/API permission (configured key,
separate billing); alternatively the user can provide external paintovers.
Whole goal remains unfinished; no art lock or release readiness claimed.

Rejected experiment: shared landform crown/normal changes did not visibly
remove the zirat road strips; reverted exactly, no visual credit claimed.
Deleted three obsolete own capture batches (93 MiB); current comparisons
remain available. Next inspect visible arrival geometry owners before edits.

Latest zirat ownership correction: connected-world suppression now hides
only the legacy `ZiratRoadsideAuthoredKit`, retaining the core kit and the
zone's separate interaction/traversal owners. Affected build: 0 warnings,
0 errors; hidden capture: 44/44 unique frames (presentation audit, not a
physical walkthrough). Root inspected Kara reverse and zirat forward in
`/private/tmp/urman-zirat-owner.jjd9TM`: pale trapezoid crowns, exposed road
strips, empty ground and schematic buildings remain plainly visible.
Removing a duplicate presentation does not solve those source-asset defects.
The user's N64/primitive assessment remains valid; visual acceptance is OPEN.

The user superseded Luna-only and Sol Advisor routing. Implementation and
verification now run in the main task, in the canonical checkout.
The previous OpenCode run and numbered waves below are historical evidence.

Wave34 shelterbelts were visually rejected and removed, including their
generator definitions, placements and exported geometry. The foliage kit is
back to 128 meshes / 5,922 triangles. The next terrain candidate raises the
existing reverse-arrival watershed beyond z=52 to a visible broad slope;
Python source and C# collision use the same expression. Asset registry and
affected game build pass. Hidden capture on 2026-09-05 produced 44 unique
frames; root review confirms a rear land horizon and removal of the stray
near-camera branch. This is a geography improvement only: left-side empty
land, broad road surfaces and primitive crowns remain P0 visual failures.
No visual-production or release task is closed by this pass.

The subsequent conifer pass replaces detached sphere crowns in the shared
`AddVisualConifer` owner with a continuous irregular branch envelope.
Affected build passes with zero warnings/errors; a new hidden full-route
capture passes 44/44 unique frames. Root inspected Arrival reverse and Kara
approach: disc gaps are removed, but conifers remain schematic up close and
the forest horizon remains empty. ART/world statuses remain REWORK.

Arrival parcel pass: added six rear/shared boundary runs around the existing
west/east holdings and weathered slats on Arrival fences. The capture
passed 44 unique frames; Arrival right/reverse show a clearer domestic
boundary. Zirat review exposed excessive enclosure from global slats, so
the final code limits slats to Arrival; other fences retain open rails.
No collision or interaction owners changed. Side-field emptiness remains
open. The capture precedes that final scope restriction; it is not evidence
of a full visual pass on the final file.

Perimeter/contact pass: matching Blender/C# watershed grades now close the
west/east and far Kara terrain horizons outside the playable yards/route.
Distant procedural tree anchors follow that ground. Existing HouseA
presentation instances are seated using visible LOD0 lower bounds rather
than the imported reference node origin. The final hidden capture passed
44/44 unique frames and the build passed 0 warnings/errors. Root reviewed
Arrival reverse, Babai left and Kara approach: Arrival far-house walls now
stand above ground; peripheral terrain is visible. Babai's low roof-like
neighbor components remain unresolved and must be traced to their separate
presentation owner. Near-field emptiness and schematic trees remain P0.
Temporary current review pair: urman-astra-grounded.cw2oH9 and
urman-astra-housecontact.n1gQgZ under /private/tmp; these are ephemeral.

Babai neighbor contact fix: `WestSideParcel`,
`BabaiYardWestDepthBanyaYardParcel` and `BabaiEastDepthPlasterAnnexParcel`
now use shared-terrain height instead of zero. These are presentation-only
mounts; their internal relative placements and gameplay owners are intact.
Final affected build is 0 warnings/errors and capture is 44/44 unique.
Root reviewed `babai_yard_left` and `babai_yard_depth`: the previously
roof-only central neighbor now shows walls/window/porch above the terrain.
This resolves that concrete placement defect, not the broader P0 density gate.
Latest ephemeral capture: /private/tmp/urman-astra-yardcontact.VK7Rcn.

Kara watershed pass: 24 trees in three offset depth groups occupy the slope
behind z=-130, beyond the playable endpoint. Root reviewed final depth,
right lateral and zirat return: the forward forest is fuller, but the right
slope remains open and tree silhouettes remain schematic; visual P0 is open.
Two pre-fix capture attempts failed unique hashes at different frame spans.
`WaitForRenderedFrameAsync` had only awaited process frames; it now calls
`RenderingServer.ForceDraw(false)` on the main thread before root readback.
The subsequent exact-candidate capture passed 44/44 unique frames, with an
affected build of 0 warnings/errors. Latest ephemeral evidence:
/private/tmp/urman-astra-render-sync.rOabbr. Failed captures are not passes.

Leaf-crown candidate: shared birch/broadleaf crown meshes now use folded,
double-sided leaf sprays instead of three closed spheres. Positions and
collision/state owners are unchanged. First sparse candidate was rejected;
second candidate restores crown volume while retaining gaps. Affected build
passes 0 warnings/errors; hidden capture passes 44 unique frames at
/private/tmp/urman-leaf-dense.Yl7IQv. Root inspected Babai left, Arrival
reverse and Kara reverse. This is NOT asset acceptance: exposed box branches,
the separate imported sphere/cone trees, bare ground and crude architecture
remain visible. Saved graphics profile is medium; the wood texture source is
detailed, so simply generating more textures is not justified. Next work
must address the remaining visible geometry owners and material readability.
Removed two obsolete failed forest captures (about 54 MiB); no source deleted.

Imported birch pass: replaced the three Birch variants' closed canopy
geometry in the existing Blender foliage generator; rebuilt its Blend/GLB
and updated registry hashes. Export has 128 meshes / 13,014 triangles.
Asset registry passes 55/55; hidden capture passes 44 unique frames at
/private/tmp/urman-import-leaves.lqin1H. Root comparison of Arrival forward
and Babai left confirms the large imported bulb crowns are gone. Leaf
clusters remain too dark/flat and conifers remain schematic, so this is a
candidate improvement only, not a visual gate closure. No new kit, runtime
state owner, collision or tree placement was introduced.

Leaf export correction: Blender validation removed reversed faces sharing
the same vertex indices. Independent face vertices now preserve both sides;
GLB inspection confirms 1,728 triangles per crown (48 shoots × 9 leaves × 4),
20,790 for the complete kit. Registry passes 55/55. Shared material now uses
MODEL_NORMAL_MATRIX for nonuniform scale and bounded foliage BACKLIGHT,
following Godot 4.7 spatial shader built-ins (not emissive fill):
https://docs.godotengine.org/en/4.7/tutorials/shaders/shader_reference/spatial_shader.html
Final hidden capture /private/tmp/urman-leaf-light.Fn6Vt7 passes 44 unique
frames, build 0 warnings/errors. Root inspected Babai left and Kara depth:
leaf undersides exist and night foliage does not glow, but lighting gain is
modest; primitive conifers, bare terrain and angular props still dominate.
Do not spend further passes tuning leaf brightness while those remain.

Shared conifer geometry pass: removed the continuous triangle crown shell
from AddVisualConifer, replacing it with 14 staggered branch tiers and a
tapered cylinder trunk. No placement/collision changes. First sparse
candidate was rejected because it made the forest boundary transparent;
the second restores crown volume with folded branch fans. Final hidden
capture /private/tmp/urman-conifer-volume.13kyyr passes 44 unique frames;
build 0 warnings/errors, diff check clean. Root reviewed Arrival reverse
and Kara depth: common conifers no longer have solid zigzag-cone silhouettes.
This does not close P0: large angular near-field props, bare ground, old
imported foliage families and architecture still fail the target quality.

Kara side-mass replacement: tracing confirmed the four visible
KaraThresholdDarkMass*/KaraFarDarkForestMass* nodes were extra core polygon
solids, not the already-suppressed kit previews. Removed those four calls
and populated the same side parcels with 12 terrain-grounded trees using
existing vegetation owners. Initial compile caught a double/float cast;
corrected build passes 0 warnings/errors. Hidden capture passes 44 unique
frames at /private/tmp/urman-kara-stands.HNYVn2. Root reviewed depth/right:
the large forward polygon walls are gone; tree layering replaces them.
The right-facing void, old block-like side trees and crude ground props
remain open P0 issues. Removed obsolete sparse-conifer capture (~27 MiB).

Lateral forest pass: 22 explicitly placed trees now occupy the east/west
slopes in multiple depth groups, with ground-height-bound understory outside
the route. Shared shrub crowns reuse existing leaf sprays instead of spheres.
Build passes 0 warnings/errors; hidden capture passes 44 unique frames at
/private/tmp/urman-forest-sides.pKIj5o. Root reviewed Kara left/right/back:
side slopes now have forest depth, but gaps, black ground and old block-like
trees remain visible. P0 is still open; this is not full 360-degree acceptance.

Material readability pass: capture now reports actual visible shader state;
medium profile has 161 unique visible shader materials, 153 textured, zero
low-quality materials. Missing textures were not the cause. Reduced the
shared base-color floor so painted source value differences survive grading;
reduced wood repetition to avoid tiny board patterns. No new texture assets.
Final capture /private/tmp/urman-material-scale.1XR14n passes 44 unique frames
and build 0 warnings/errors. Root compared Arrival and house interior with
the unchanged material baseline: wood/plaster detail is more visible, board
scale is broader. This is surface readability only, not improved building
geometry or professional world acceptance. Geometry/empty land remain P0.

Arrival ground-contact pass: shared grass clumps now use curved tapered
blades instead of four boxes. Growth patches follow shoulders/rear fences,
grounded to terrain outside the route. Rejected the first uninterrupted
linear-strip layout; final placement includes wider scatter and open gaps.
Final capture /private/tmp/urman-verge-patches.gD2n1k passes 44 unique frames;
build 0 warnings/errors, diff clean. Root reviewed Arrival reverse/right:
ground contact improves locally but large yard fields remain bare. This
pass does not close density or art acceptance and adds no gameplay owner.

Kara pixel-owner trace: screen-to-ray coordinates require the logical
1920x1080 viewport scale, not raw 1280x720 capture pixels. Corrected temporary
probe identified the black box-tree as KaraClosureEastDistantMass's imported
DistantForestMass_Tall meshes. Removed its near-camera placement and the
matching west distant-module placement; existing slope tree stands now own
those views. No asset deleted. Temporary pixel diagnostic was removed.
Final capture /private/tmp/urman-forest-placement.4RQjbz passes 44 unique frames,
build 0 warnings/errors; root right-view review confirms the black block-tree
is gone. Pale reverse-view trees were traced separately to
ZiratBirchShrubMass_MidCanopy_01_LOD0, present in both zirat-return-road and
core presentation mounts. Their geometry/material duplication remains an
explicit next investigation; no cemetery marker was changed.

Run: 2026-09-03, single external OpenCode implementer, canonical checkout
` /Users/unterlantas/Documents/GitHub/URMAN`, branch `main`.

- Base commit: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`)
- Current commit (ledger init): `fc58c408326c0fff420641cdd82e712bb51b4f6c`
- Branch: `main`
- Tracker: `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (root, untracked input)
- Current task/slice: Wave 9/10 final visual closeout on 2026-09-05 — `PARTIAL` /
  `REWORK`, with technical evidence green but the human gate open. Asset
  source/registry `55/55 PASS`, build `0/0` warnings/errors, physical
  first-person walkthrough `PASS` over `135.49 m` through the final Kara
  cliffhanger, and hidden capture `/private/tmp/urman-wave10-capture.5sjDbu`
  with `44/44` unique PNGs, 10 visual zones and a `242.50 m` waypoint audit
  passed their technical gates. The capture path is ephemeral and will be
  deleted, not a durable deliverable.

- Technical progress: the master-layout/terrain pass produced a real
  improvement — Arrival/MainStreet now have a readable narrow road, closer
  parcels and near/mid/far depth, and this basis is retained. FAP sightlines
  improved, while the Naila/document focus remains only partial; Wave 5
  removed oversized source markers from Zirat, but runtime still reads sparse
  and primitive with exposed terrain/legacy masses. Kara remains dark/sparse.
  The C# cleanup task returned `PARTIAL`/no change rather than speculative
  suppression. `RuntimeBridge` was untouched and no new narrative-state owner
  was introduced.

- Root visual verdict: the human root review rejects the Professional gate.
  Keep the improved Arrival/MainStreet basis, but stacked/jagged road strips,
  a gate/fence crossing the arrival composition, empty reverse/lateral
  horizons and close crop/scale crowding remain. FAP is atmospheric but
  underexposed/empty; Zirat remains culturally open; Kara has near-camera
  canopy/trunk occlusion and weak side depth. Affected production-ready
  zone/art IDs remain `OPEN` or `PARTIAL`; no `DONE` follows from automated
  evidence. This is not demo-ready, art-locked, production-ready or
  release-ready.

- Confirmed P0 visual defect: the Act I map reads conspicuously empty because
  normal first-person 360° stops expose broad, uncomposed flat unused ground
  and empty horizons, while buildings/props read as isolated islands instead
  of continuous authored yards, street frontage, neighboring parcels, terrain
  transitions and forest edge. Any exterior stop with that read fails the
  professional/visual gate even when route/tests are green. Each stop must
  have coherent near/mid/far spatial occupancy and visual continuity
  forward/back/left/right. Density means authored terrain relief, parcel
  boundaries, architecture, vegetation masses and landmarks—not random prop
  spam, procedural repetition or foliage masking missing geometry. Traversal
  clarity, deliberate breathing space and landmarks remain required; not every
  square meter is to be cluttered. Current failing/open P0 examples are
  `BabaiEbiYard`, `Arrival`/`MainStreet`, `ZiratMemoryField` and the
  `zirat → Kara` approach/`KaraForestEdge`; existing rows stay `OPEN`/`PARTIAL`
  and the demo is not ready.

- The Professional visual quality gate is product-wide for the Act I vertical
  slice, not map-only: 3D world/interiors, NPC presentation, UI readability,
  audio, staging/transitions and overall cohesion must read as deliberate,
  professional and impressive in motion. Professional means coherent,
  shippable vertical-slice quality, not photorealism or AAA scope; placeholder,
  benchmark-scene, generic asset-pack or cheap-indie read is not accepted.
  Automation, FPS, hashes and capture uniqueness cannot close this gate;
  human in-motion review of the exact candidate is required. Current status
  and all task rows remain unchanged.

- Root-cause decision: stop another broad kit-count/detail pass. The next exact
  priority is targeted visual-owner diagnosis for overlapping road/fence and
  legacy Zirat/Kara nodes; then source/material/lighting polish and one hidden
  capture with human review. Do not use speculative C# suppression as a
  substitute. Demo/art/release gates remain `OPEN`, and cultural, audio,
  platform and human gates remain open. No task IDs or statuses changed.

- Full `verify-godot` aggregate note: in the isolated temporary HOME, the only
  setup failure was the editor-profiler `user://... ObjectDB Snapshots`
  directory setup. Individual main-menu, pause, scene-smoke, corridor and
  full physical walkthrough checks passed headless/Dummy. The profiler issue
  is an environment/harness caveat, not a fabricated product pass/fail.

- Cultural, language, human, audio, platform and release gates remain open;
  see the Queue reassessment and per-slice entries below for the remaining
  human-gated work.

## Completed

- UIUX-009 (P1 REWORK, automation portion): UI readability matrix smoke
  2026-09-04. New `Act1UiReadabilitySmokeTest` in the aggregator: at
  1280x720 and 1920x1080 — settings/journal/old-PC/document/dialogue each
  open, their panels stay fully inside the viewport bounds, and each closes
  cleanly (Close buttons or ui_cancel via the owner's own handler). Text
  readability itself remains the human review gate (PLAYTEST-004 scope).
  Evidence: `evidence/act1_repo_baseline/uiux009-verify-{godot,dotnet}-PASS.txt`.

## Completed

- STATE-006 (P1 REWORK, doc slice): release-safe diagnostics surface
  documented in `docs/production/runtime_state_diagnostics.md` — what is
  logged (zone-loaded/scene.entered/audio requests/checkpoint writes/save
  outcomes), where (Godot log, user://logs, user://savegames), the support
  recipe, and the boundaries (no document text, no personal data, no debug
  controls). verify-godot and verify-dotnet exit 0 on the same HEAD.

- SAVE-002 (P1 REWORK, matrix remainder): beat-matrix save/restore
  roundtrips added to Act1CheckpointSmokeTest — crossroad (post house exit),
  evidence-official-death and the reread beat: each saves the live state,
  drifts to kara, restores and proves byte-identical state plus correct
  scene; the final leg proves the pre-reveal ordering survives the restore.
  verify-godot (15 PASS smokes) and verify-dotnet exit 0.

## Completed

- SAVE-004 (P1 CREATE): checkpoint smoke green 2026-09-04 (commit with this
  ledger). The rebuilt smoke walks the authored chain, asserts rolling
  checkpoints at internal-register and reread, restores the reread checkpoint
  exactly (pre-reveal state, no cliffhanger), re-collects the zirat clue and
  reaches the single terminal beat. Added to the aggregator; verify-godot
  (15 PASS smokes) and verify-dotnet exit 0.
  - `RuntimeBridge`: rolling `checkpoint` slot auto-saved after the four
    stable beats (evidence-official-death, evidence-internal-register,
    evidence-tatarwiki-reread, zirat-road) — writer only RuntimeBridge, no
    autosave inside dialogue/document/transition states (the checkpoint
    fires only when an interaction commit lands on a checkpoint scene);
    verified by the smoke's auto-save receipts during the walk.
  - `Act1DemoRoot`/`MainMenuUi`: Continue deliberately stays quick-only —
    checkpoint-slot fallback reverted until the restore defect is fixed
    (softlock risk).
  - `Act1CheckpointSmokeTest` kept in the repo but OUT of the aggregator
    (reproducibly demonstrates the defect; hangs past its Fail).
  - verify-godot exit 0 (14 PASS smokes), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/save004-verify-{godot,dotnet}-PASS.txt`.

## Completed (historical)

- UIUX-010 (P1 REWORK, zone-fade slice): reduced-motion zone transitions
  2026-09-04.
  - `FirstPersonController.ReducedMotion` exposed for presentation owners;
    `Main.PlayZoneTransition` gates the animated fade on it — reduced motion
    cuts instantly (overlay cleared on the same switch, no tween), normal
    motion keeps the animated fade.
  - New `Act1ReducedMotionSmokeTest` in the aggregator: reduced-motion
    instant cut, normal-motion fade present and clearing.
  - verify-godot exit 0 (aggregator incl. new smoke — 12 PASS lines),
    verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux010-verify-{godot,dotnet}-PASS.txt`.
  - Remaining UIUX-010 scope (full-route comfort at 65/75/90 FOV) stays with
    the human motion-comfort review (PLAYTEST-004).

- AUDIO-004 (P1 CREATE): footstep system 2026-09-04.
  - `tools/audio/generate_act1_footsteps.py`: 15 deterministic
    project-original procedural step samples (5 surface families x3 variants:
    wet_road, mud, grass, wood, interior_floor), same provenance pattern as
    the ambience stems.
  - New `game/scripts/FootstepAudioController.cs` wired into `main.tscn`:
    cadence from real movement (velocity over distance), surface follows the
    active bridge zone (interiors -> floor, kara/zirat -> grass, village ->
    wet road), plays through the SFX bus, pitch/variance per step, holds no
    gameplay state; reduced motion silences footsteps.
  - New `Act1FootstepSmokeTest` in the aggregator: 5x3 surfaces loaded, SFX
    routing, movement-driven cadence via real input, reduced-motion silence,
    zone surface mapping.
  - verify-godot exit 0 (aggregator incl. new smoke — 13 PASS lines),
    verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/audio004-verify-{godot,dotnet}-PASS.txt`.
  - Human remainder: listening review (AUDIO-014, CULTURE-004); authored
    foley remains a gate (no placeholder-as-final claims).

- AUDIO-006 + AUDIO-007 (P1 CREATE): dedicated FAP and zirat beds 2026-09-04.
  - New `tools/audio/generate_act1_ambience_layers.py`: deterministic
    project-original procedural beds — `fap_institutional.wav` (cool
    fluorescent hum, corridor taps, paper — AUDIO-006) and `zirat_wind.wav`
    (open-field wind, grass detail, far tractor line — AUDIO-007), loop-seam
    crossfaded, 8 s.
  - `ambient_manifest.json`: fap_clinic now routes to the FAP institutional
    bed (house room no longer reused) and zirat_road to the zirat wind bed
    (village bed no longer bleeds into the zirat) — single ambience owner
    kept, director unchanged (generic per-zone stems).
  - Registry: 2 new audio entries with generator provenance + hashes;
    verify-assets exit 0 (43 assets), verify-godot exit 0 (ambient smoke
    green with 6 stems).
  - Evidence: `evidence/act1_repo_baseline/audio006007-verify-{godot,assets}-PASS.txt`.
  - Human remainder: listening/cultural review (AUDIO-014, CULTURE-004).

- AUDIO-003 (P1 REWORK): village sub-zone beds 2026-09-04.
  - `AmbientAudioDirector.SetZone` gains an optional sub-key: manifest stems
    keyed `zoneId@subKey` (e.g. `village_day@from_house`) select a distinct
    bed for a spawn area, with the plain zone bed as fallback.
  - `Main.SwitchZone` passes the spawn point id as the sub-key; three new
    project-original village beds generated (`village_arrival`, `village_yard`,
    `village_return`), manifest + registry updated (9 stems / 46 assets).
  - `Act1AudioTransitionSmokeTest` extended: arrival sub-bed, plain fallback
    via the default spawn, plus the five zone beds. `AmbientAudioSmokeTest`
    updated for the arrival sub-bed + fallback case.
  - verify-assets, verify-godot (14 PASS smokes), verify-dotnet — all exit 0.
  - Evidence: `evidence/act1_repo_baseline/audio003-verify-{godot,assets,dotnet}-PASS.txt`.
  - Human remainder: listening/cultural review (AUDIO-014, CULTURE-004).

- AUDIO-010 (P1 CREATE, old-PC slice): interaction foley 2026-09-04.
  - New `tools/audio/generate_act1_foley.py`: 3 project-original procedural
    samples (ui_click, keyboard_key, paper_open), registered in the asset
    registry (49 assets).
  - `OldPcUi`: SFX-bus foley player + hooks — search/header-key press ->
    keyboard_key, document save -> paper_open, close -> ui_click; streams
    nulled in `_ExitTree` (no renderer leaks); headless guard matches the
    ambient-director pattern.
  - Found and fixed a suite-hermeticity defect the foley exposed: a stale
    persisted `settings.json` (InputDevice=gamepad, ReducedMotion=true) from
    an earlier run leaked into smokes asserting keyboard wording.
    `verify-godot.sh` now clears the two regenerable preference files before
    the loop (story savegames untouched). Journal smoke diagnostic improved
    (status text in the failure message).
  - verify-assets (49 assets), verify-godot (14 PASS smokes), verify-dotnet —
    all exit 0, zero leak warnings.
  - Evidence: `evidence/act1_repo_baseline/audio010-verify-{godot,dotnet}-PASS.txt`.
  - Remaining AUDIO-010 scope: foley for journal/document/dialogue UIs uses
    the same pattern (open work).

- AUDIO-010 remainder (P1 CREATE): journal/document/dialogue foley
  2026-09-04.
  - New shared `game/scripts/UiFoley.cs` (SFX-bus player attach + cached
    sample playback + headless guard); `OldPcUi` refactored onto it.
  - Wired: JournalUi open -> paper; DocumentUi open -> paper, close -> click;
    DialogueUi open/continue/close -> soft clicks/keys. Production handlers
    preserved (an initial patch accidentally replaced Save/Close/Search
    handlers — caught by the suite and fixed).
  - verify-godot exit 0 (14 PASS smokes, zero leak warnings),
    verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/audio010b-verify-{godot,dotnet}-PASS.txt`.
  - Human remainder: listening review (AUDIO-014, CULTURE-004).

- AUDIO-010 door slice (P1 CREATE): house-entry door creak 2026-09-04.
  - `generate_act1_foley.py` extended with a deterministic project-original
    `door_creak.wav` (slow irregular creak with grit); registered in the
    asset registry (50 assets).
  - `Act1DemoRoot`: SFX-bus one-shot via `UiFoley` on village -> house zone
    transitions (presentation-only, silent in headless runs).
  - verify-assets (50 assets), verify-godot (16 PASS smokes), verify-dotnet
    — all exit 0 on this slice; listening review remains human
    (AUDIO-014, CULTURE-004).

- AUDIO-010 door slice (P1 CREATE): house-entry door creak 2026-09-04.
  - `generate_act1_foley.py` extended with a deterministic project-original
    `door_creak.wav` (slow irregular creak with grit); registered in the
    asset registry (50 assets).
  - `Act1DemoRoot`: SFX-bus one-shot via `UiFoley` on village -> house zone
    transitions (presentation-only, silent in headless runs).
  - verify-assets (50 assets), verify-godot (16 PASS smokes), verify-dotnet
    — all exit 0 on this slice; listening review remains human
    (AUDIO-014, CULTURE-004).

- AUDIO-009 (P1 CREATE): zone-transition bed routing smoke 2026-09-04.
  - New `Act1AudioTransitionSmokeTest` in the aggregator: switching through
    all five Act I zones settles on exactly the manifest-mapped bed (one
    stream holder, exact manifest path read from the same manifest the
    director uses), no double loops after the crossfade.
  - Found and fixed a test-contract drift: the content audio-production
    reporter expected 4 ambient stems; the FAP institutional and zirat wind
    beds legitimately make 6 (assertion updated to the canonical outcome).
  - Footstep controller gained ExitTree stream hygiene (fixes a real
    exit-leak found by the suite run).
  - verify-godot exit 0 (14 PASS smokes), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/audio009-verify-{godot,dotnet}-PASS.txt`.

- TEST-008 (P0 CREATE): covered by delivered smokes 2026-09-04.
  - Coverage receipt `evidence/act1_repo_baseline/test008_ui_smoke_coverage.md`
    maps every required area (main menu, settings persistence, audio volumes,
    modal stack, accessibility options) to the delivered aggregator smokes —
    main menu, user settings, settings navigation, audio settings, pause
    stack, binding conflicts, final state, interruption, reduced motion —
    all driving production buttons/paths. verify-godot exit 0 (12 PASS
    lines), verify-dotnet exit 0. Human readability/first-time gates stay
    open (PLAYTEST-004, UIUX-009).

## Completed

- UIUX-008 (P1 REWORK): binding conflict detection + restore defaults
  2026-09-04.
  - `InputBindingService.InitializeDefaults()` snapshots pristine bindings at
    player ready (before any rebind/stored-settings apply); `RestoreDefaults()`
    re-applies them; `FindKeyboardConflicts(key, excludingAction)` reports
    other remappable actions bound to the same physical key.
  - `SettingsUi`: a conflicting key now requires a second identical press
    (explicit choice, conflicting action keeps its binding, warning names the
    action); Esc/BeginRemap resets the pending conflict; new
    «Сбросить управление» button restores defaults; `BeginRemap`/`IsAwaitingRemap`
    exposed so tests drive the same production remap entry.
  - New `Act1BindingConflictSmokeTest` in the aggregator: two-step conflicting
    rebind (journal onto interact's key), conflicting action preserved,
    restore-defaults returns pristine bindings with no empty actions.
  - verify-godot exit 0 (aggregator incl. new smoke — 11 PASS lines),
    verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux008-verify-{godot,dotnet}-PASS.txt`.

## Completed

- UIUX-011 (P1 CREATE): audio bus volume service 2026-09-04.
  - New `game/scripts/AudioSettingsService.cs`: creates the Act I bus set
    (Master + routed Ambience/Voice/SFX) idempotently, applies per-bus
    volumes live (mute at ~0), persists a versioned
    `user://audio-settings.json` (atomic write, safe defaults).
  - Routing: `AmbientAudioDirector` players -> Ambience bus; `AudioCueUi`
    voice player -> Voice bus. Captions are visual, so a muted voice bus
    still presents the authored line (mute-safe captions).
  - `SettingsUi`: four volume rows (Общая/Окружение/Голос/Эффекты) applying
    live and persisting.
  - New `Act1AudioSettingsSmokeTest` in the aggregator: buses exist, player
    routing, voice-mute keeps captions, live volume application, versioned
    persistence (file parse), settings rows apply to the bus.
  - verify-godot exit 0 (aggregator incl. new smoke — 10 PASS lines),
    verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux011-verify-{godot,dotnet}-PASS.txt`.

## P2 disposition (explicit, per Definition of Exhausted Automatable Work)

| P2 ID | Disposition | Reason |
|---|---|---|
| BASE-008 | done | tracked .DS_Store removed (commit `3776b27`) |
| ART-012 | done (inspect slice) + open remainder | presence inspected; silhouette/variation and cultural review are human (CULTURE-002) |
| Z01-005…Z08-005 (8) | deferred | landmark/prop wayfinding acceptance requires the first-person landmark test + art/cultural checklist — human evidence cycle after zone art iteration |
| HOUSE-007, FAP-007 | deferred | comfort/perf polish explicitly after final art lock; measured bottlenecks first (STOP-DOING #7) |
| NARR-016 | done | matrix authored (`a6e5ced`); readability review stays human |
| AUDIO-010 | deferred | interaction foley needs authored project-original sounds + rights; wiring without them would ship placeholders |
| UIUX-003/004/009 | deferred | prompt/affordance/readability acceptance needs visual + human matrix evidence (recorded in Queue reassessment) |

## Completed (continued)

- CAPTURE-003 (P0, automated portion): fresh 44/44 capture on current HEAD
  2026-09-04 — the post-fix hidden run
  `/private/tmp/urman-wave4-postfix-capture.1pHWyQ` produced 44/44 unique
  root-viewport PNGs, 10 visual zones and a 242.50 m waypoint audit. The temp
  path is ephemeral evidence, not a durable deliverable. Human 360°/art review
  remains open; root review marks Arrival/MainStreet FAIL and FAP PARTIAL.

## Blocked

- RESOLVED 2026-09-04 (was SAVE-004 restore-defect, P0-class): the rebuilt
  checkpoint smoke proves the rolling checkpoint saves at
  evidence-internal-register and evidence-tatarwiki-reread, restores exactly
  to the saved beat, and the session continues to the single terminal beat.
  The earlier "rinat unavailable" reading was the smoke re-dispatching an
  interaction whose content design correctly gates it on `not rinat.alerted`
  — not a production defect. SaveCheckpointAsync/LoadSlotAsync verified
  green; no player-facing softlock.

- WAVE 4 visual art gate: `PARTIAL` / technical-only. Arrival/MainStreet
  fail on wide/flat road, empty lateral/back horizons and obvious repetition.
  FAP obstruction is fixed and the route is readable, but its authored-greybox
  still has excessive empty floor, weak composition, flat light and little
  environmental story. Affected zone/art IDs remain `OPEN` or `PARTIAL`, never
  `DONE` from automated uniqueness alone.

- Rejected Wave 5 code-only attempt: `44/44` capture and the physical
  first-person walkthrough passed technical checks, but root visual review
  failed Arrival/MainStreet and FAP. Ineffective additions were pruned/restored;
  Zirat remains flat and Kara received cleanup only. Authored 3D source-asset/
  world-layout rebuild remains required; no affected row is `DONE`.

- Cultural, language, human, audio, platform and release gates remain
  `OPEN`/`BLOCKED_EXTERNAL`; no demo-ready, art-lock, production-ready or
  release-ready status is asserted.

## Queue reassessment (2026-09-04, honest disposition of remaining families)

- Automatable-ready (code/doc, dependencies closed): UIUX-011 (audio bus
  volume service + settings controls + smoke), UIUX-008 (binding conflict
  detection + restore defaults), UIUX-003/004 (prompt/affordance polish —
  needs at least one visual judgment, marginal), UIUX-002/009/010/012
  (acceptance is first-time/human readability or external matrix evidence —
  preparation only).
- Human-gated (BLOCKED_EXTERNAL, evidence packages prepared): CAPTURE-003..007
  (CAPTURE-003 technical 44/44 is green on the ephemeral post-fix run; zone art,
  supplemental evidence and signed review remain open),
  PLAYTEST-001..006, CULTURE-001..005, AUDIO-011..014 (real recordings/mix),
  WIN/M1 host gates, PERF-002..005 (after CAPTURE-006), RELEASE-*.
- Authored-iteration (next automatable priority; one serialized Luna writer,
  not closable by reports): source-first 3D/world-layout rebuild using existing
  `ART-003…ART-012`, `Z01-001…Z08-008`, `HOUSE-001…HOUSE-008` and
  `FAP-001…FAP-008` rows. Freeze/verify authored sources first, integrate
  placements/shared presentation owners second, then take one hidden capture
  and perform the human root visual review. No parallel ownership overlap and
  no additional C# presentation, UI, performance, packaging or release work
  takes priority before this gate.
- Narrative text polish (NARR-002..015, 017): editing is possible but
  acceptance requires native/cultural/first-time evidence — editing without
  that evidence would fake the gate; deferred to the human cycle.

## Completed

## Completed

- NARR-016 (P2 CREATE): environmental storytelling matrix 2026-09-04.
  - New `docs/urman_knowledge_base/art/act1_environmental_storytelling_matrix.md`:
    per space (8 zones, 2 interiors, mosque, road, final beat) — intended
    inference, kit-component prop owners, clue-vs-atmosphere split aligned
    with `definitions.json` beat/interaction IDs, false-positive controls
    (blank boards, no invented signage/epitaphs, no second screen, no
    creature silhouettes). Rule added: new props add a matrix row in the same
    change; unresolved rows block zone readiness.
  - Human remainder: inference readability review (CAPTURE-006, PLAYTEST-003).
  - Docs-only slice; suites unchanged-green.

## Completed

- AUDIO-002 (P1 CREATE): Act I sound map 2026-09-04.
  - New `docs/urman_knowledge_base/audio/act1_sound_map.md`: beds (current
    stems -> authored targets per AUDIO-003..008), spot events, authored
    silence windows (zirat pause, Kara threshold), transition/crossfade and
    voice-priority rules, anti-pattern list (no combat/stingers, no silence-
    as-cover). Cue IDs aligned with `definitions.json`
    (`audio-marat-voice`, `audio-rinat-interruption`); routing facts from
    `ambient_manifest.json` + `AmbientAudioDirector`.
  - Human remainder: narrative/audio listening review (AUDIO-014, CULTURE-004).
  - Docs-only slice; suites unchanged-green (last runs exit 0).

## Completed

- UIUX-006 (P1 REWORK): settings navigation candidate 2026-09-03.
  - `SettingsUi.Open` now lands keyboard/gamepad entry focus on the first
    control (`_fov.GrabFocus()`), making the whole panel reachable without a
    mouse; apply/cancel rollback semantics verified structurally (explicit
    Apply commits; closing without Apply discards edits and reopen shows the
    live values).
  - New `Act1SettingsNavigationSmokeTest` in the aggregator: menu-safe open
    (menu beneath, player gated, entry focus), rollback without apply (live
    FOV untouched, store untouched), explicit apply commits live + persists
    to the user store.
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux006-verify-{godot,dotnet}-PASS.txt`.

## Completed

- UIUX-007 (P1 REWORK): versioned user settings store 2026-09-03.
  - New `game/scripts/UserSettingsStore.cs`: `user://settings.json`, versioned
    payload holding ONLY the `GameSettingsSnapshot` (display/input/
    accessibility — no narrative fields per the SAVE-006 boundary); atomic
    write (temp+move), safe null on missing/corrupt file.
  - `FirstPersonController`: cold launch restores stored preferences
    (`_Ready`); every applied settings snapshot persists to the store (latest
    applied mirror). Story slots keep their own settings snapshot — restoring
    a slot applies the slot's values and the store follows as latest-applied.
  - New `Act1UserSettingsSmokeTest` in the aggregator: cold persistence,
    store/slot separation (store=70 while slot=82, restore applies 82), safe
    store reset (narrative slot unaffected).
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux007-verify-{godot,dotnet}-PASS.txt`.

## Completed

- TEST-007 (P0): static source-contract guard 2026-09-03.
  - New `tests-dotnet/Urman.Core.Tests/SourceContractTests.cs` (2 facts,
    engine-independent, runs in the dotnet suite): only `RuntimeBridge.cs`
    may construct/restore the `RuntimeKernel` (`new RuntimeKernel(` /
    `RuntimeKernel.Restore(`) and only `RuntimeBridge.cs` may own the save
    store (`new AtomicSaveGameStore(` / `user://savegames`) across all
    `game/scripts/**`. Self-check asserts the patterns ignore ordinary
    mentions. Any new caller now fails the suite — the standing regression
    guard behind the STATE-001 audit ledger.
  - Suite 43+12 green, verify-dotnet exit 0 (Godot side unchanged).
  - Evidence: `evidence/act1_repo_baseline/test007-verify-dotnet-PASS.txt`.

## Completed

- GAME-009 (P0): interruption/softlock fixtures 2026-09-03.
  - New `game/tests/Act1InterruptionSmokeTest(.cs/.tscn)` in the aggregator:
    (1) dialogue-cancel atomicity — Escape on an open Gulsina modal applies
    no state change; (2) quick save taken under the open modal + load
    afterwards reproduces the cancelled-dialogue state exactly (no
    half-applied effects); (3) repeated NPC re-talk is legal and idempotent
    (identical state, exit stays unlocked exactly once); (4) baseline load
    reverts the whole interrupted chain to a consistent replayable state.
  - Complementary one-shot semantics asserted in
    `Act1FinalStateSmokeTest`: the zirat roadside clue consumes once
    (second dispatch rejected).
  - Finding recorded (not a defect): NPC re-talk (Gulsina) is intentionally
    repeatable with idempotent effects; true one-shots are clue/evidence
    targets (zirat clue).
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/game009-verify-{godot,dotnet}-PASS.txt`.

## Completed

- STATE-005 (P0): focused final-state fixtures 2026-09-03.
  - New `game/tests/Act1FinalStateSmokeTest(.cs/.tscn)` in the aggregator;
    drives the real authored chain through RuntimeBridge to the zirat road,
    then proves the STATE-005 matrix:
    (1) forest approach locked before the zirat clue; out-of-order dispatch
    rejected with no state change and no final knowledge;
    (2) zirat clue confirms WITHOUT granting `clue_do_not_answer_rule`;
    (3) pre-forest snapshot: reveal-hidden, cliffhanger not completed;
    (4) forest entry completes the single terminal beat;
    (5) loading the pre-forest save restores the pre-reveal state exactly
    (reveal gone, beat not completed, scene zirat-road);
    (6) ending completes again after restore — once; a post-terminal repeat
    dispatch is scene-locked, rejected, and leaves the terminal state
    byte-identical.
  - Reveal-not-before ordering survives the save/load round trip (MAP-009
    contract + STATE-005 acceptance).
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/state005-verify-{godot,dotnet}-PASS.txt`.

## Completed

- UIUX-005 (P1 REWORK): pause shell + modal stack 2026-09-03.
  - New `game/scripts/PauseMenuUi.cs` (+ tscn in aggregator):
    `PauseMenuUi` owns the pause action end-to-end — opens only when the demo
    root's `CanOpenPause` allows (no menu/intro/ending), gates the player,
    stacks the settings panel on top and re-asserts the modal gate after the
    panel closes (Close() releases input — the exact trap the tracker flags),
    resumes cleanly. In-shell actions: Продолжить, Сохранить/Загрузить
    (bridge lifecycle, status feedback), Настройки, and two-step confirmations
    for Заново (`StartNewGameAsync` -> resume at arrival), В главное меню
    (returns to MainMenuUi, gameplay stays gated), Выход.
  - `SettingsUi`: pause branch removed (PauseMenuUi owns pause routing);
    ui_cancel close retained.
  - New `Act1PauseMenuSmokeTest` in the aggregator: pause gate, save/load
    resume, settings stack + pause-key return, resume, confirmed restart to
    arrival, menu return with gated input.
  - verify-godot exit 0 (aggregator incl. new smoke), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux005-verify-{godot,dotnet}-PASS.txt`.

## Completed

- UIUX-001 (P1 CREATE): public main menu 2026-09-03.
  - `game/scripts/MainMenuUi.cs` + `game/scenes/ui/main_menu_ui.tscn`: pure
    presentation overlay (CanvasLayer, group `main_menu`) — title АКT I,
    Новая игра / Продолжить (hidden without a quick slot) / Настройки / Выход;
    no runtime, session or save owner inside the menu; choices reported as
    events; accessibility target (text scale + high contrast).
  - `Act1DemoRoot`: boot now gates the demo behind the menu
    (`BuildMainMenu` → player modal; New Game → `StartNewGameAsync`; Continue
    → `LoadSlotAsync("quick")` after `HasLoadableSlot`; Settings → existing
    SettingsUi; Quit). Intro card builds only after a choice
    (`ShowIntroAfterMenu`).
  - Demo-consuming smokes (launch, walkthrough, corridor, chapter-one) now
    pass the menu through the production New Game button via shared helper
    `Act1MainMenuTestSupport.StartThroughMainMenuAsync` (button press, never
    state injection).
  - New `Act1MainMenuSmokeTest` in the aggregator: menu gate, hidden Continue
    on fresh profile, Settings open/close from menu, Continue restores the
    session after a quick save (and restores any pre-existing quick slot).
  - verify-godot exit 0 (aggregator incl. new smoke; walkthrough PASS
    135,50 m), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/uiux001-verify-{godot,dotnet}-PASS.txt`.
  - Notes: `SettingsUi.Close()` made public (programmatic close from the
    menu flow; behavior unchanged). Expected-error corridors stay out of
    Godot smokes per the aggregator's `^ERROR:` gate.

## Completed

- TEST-006 (P0): save/recovery corruption cases 2026-09-03, engine-side
  (`tests-dotnet/Urman.Core.Tests/DeterminismAndSaveTests.cs`, 3 new facts;
  suite 41+12 green, exit 0):
  - `AtomicStore_FailsSafelyWhenBothPrimaryAndBackupAreUnreadable` — both
    artifacts invalid AND never-written slot → InvalidDataException (fresh
    Continue disabled; no silent new session).
  - `AtomicStore_RecoversBackupWhenPrimaryIsTruncated` — interrupted write
    (byte-truncated primary) recovers from the one-generation backup.
    Semantics discovered and documented: the store keeps exactly ONE backup
    generation = previous primary (backup holds beat 1 after two saves).
  - `StoreRoundTrip_RestoresKernelStateAndSequenceForBothBeats` — full
    store+codec+kernel round trip: beat 2 (clue+rule, seq 2) restores state,
    event sequence and occurrence dedupe (replayed occurrence rejected, no
    duplicate commit); backup holds beat 1 (clue only, rule unknown, seq 1) —
    reveal-not-before ordering survives the round trip.
  - Kernel semantics recorded: `EventSequence` counts committed EVENTS (not
    commands). Existing coverage already included occurrence dedupe,
    snapshot-restore, claim conflicts and rollbacks (RuntimeKernelTests).
  - Godot side of New Game/Continue wiring: covered by SAVE-003's
    `Act1SaveLifecycleSmokeTest` (aggregator). Corruption matrices stay in
    xunit by design (Godot aggregator fails on any `^ERROR:` line).

## Completed

- SAVE-003 (P0): New Game/Continue lifecycle 2026-09-03 (commit `b341042`).
  - `RuntimeBridge.StartNewGameAsync` (reset via existing CreateNewSession,
    entrypoint re-applied, canonical arrival spawn, live user settings
    preserved, slots never deleted) + `HasLoadableSlot` query.
  - New `Act1SaveLifecycleSmokeTest` in the aggregator: reset, settings
    preservation, slot preservation, missing-slot gate. verify-godot exit 0
    (aggregator incl. two new tests), verify-dotnet exit 0.
  - Evidence: `evidence/act1_repo_baseline/save003-verify-{godot,dotnet}-PASS.txt`.
  - Authoring note: Godot aggregator fails on ANY `^ERROR:` log line, so
    expected-error paths (corrupt load) must live in tests-dotnet, not in
    Godot smokes.

- STATE-001 (P0): repo-wide sole-writer caller audit 2026-09-03
  (commit `2365d0c`). Zero unauthorized write paths: only user:// writer is
  the bridge-owned AtomicSaveGameStore; kernel never leaks outside
  RuntimeBridge; QuestRuntimeCoordinator dispatches only kernel-mediated,
  content-gated, deterministically idempotent reconciliation commands invoked
  by the bridge. Evidence:
  `evidence/act1_repo_baseline/state001_caller_audit.md`.

- GAME-010 (P0): no-combat sweep clean 2026-09-03 (commit `8590fcc`): zero
  combat-verb matches across game/scripts, project.godot input map, chapter 1
  content and src-dotnet (fullgame excluded). Evidence:
  `evidence/act1_repo_baseline/game010_no_combat_audit.md`.

## Completed

- STATE-001 (P0): repo-wide sole-writer caller audit 2026-09-03
  (commit `2365d0c`). Zero unauthorized write paths: only user:// writer is
  the bridge-owned AtomicSaveGameStore; kernel never leaks outside
  RuntimeBridge; QuestRuntimeCoordinator dispatches only kernel-mediated,
  content-gated, deterministically idempotent reconciliation commands invoked
  by the bridge. Evidence:
  `evidence/act1_repo_baseline/state001_caller_audit.md`.

- GAME-010 (P0): no-combat sweep clean 2026-09-03 (commit `8590fcc`): zero
  combat-verb matches across game/scripts, project.godot input map, chapter 1
  content and src-dotnet (fullgame excluded). Evidence:
  `evidence/act1_repo_baseline/game010_no_combat_audit.md`.

- Queue-status note (2026-09-03): WAVE 4-5 zone tasks (Z01-Z08) and the
  substantive WAVE 6-7 slices (interior art staging, narrative text polish)
  have their automatable evidence already recorded (composition anchors,
  distinct-frame capture runs, single-owner smokes in MAP-/ART- receipts);
  their remaining slice requires in-engine authored art iteration and human
  motion/360/cultural review (CAPTURE-003/004/006, CULTURE reviews). They
  stay honestly REWORK/OPEN - no PASS is claimed from receipts.

## Completed

- WAVE 3 (ART-003/004/007/008/009/010/011/012): inspection + churn-stop
  slices 2026-09-03, one commit each:
  `7b34059` ART-003 village volumes, `86b45ad` ART-004 FAP full volume,
  `9ebbf49` ART-007 terrain owner, `77c8b30` ART-008 foliage contract,
  `533907a` ART-009 texture churn stop (decision log entry: production set =
  six runtime-referenced families; candidates provenance/out-of-package),
  `ed68c8c` ART-010 single environment owner, `55c6f1f` ART-011 suppression
  inventory, `19a74e4` ART-012 NPC presence. verify-assets exit 0 after the
  decision-log change. All human review components remain open
  (CAPTURE-006, CULTURE-002/003).

## Completed

- MAP-010 (P0): implemented table-driven spawn matrix smoke 2026-09-03
  (commit `5dbdc1e`).
  - New `game/tests/Act1SpawnMatrixSmokeTest(.cs/.tscn)`: iterates ALL 12
    declared spawns in `Act1WorldLayout.Placements` through the production
    `Main.SwitchZone` connected path; asserts exact world transform
    (placement origin + local spawn — layout positions are placement-local),
    declared yaw, `RuntimeBridge.CurrentZoneId` sync, floor within 2 m below
    the spawn (void check) and no capsule clipping at chest height (sphere
    query). Player physics frozen for deterministic assertions.
  - Added to `eng/verify-godot.sh` aggregator; full suite exit 0 with
    `act1-spawn-matrix: PASS 12/12` (`evidence/act1_repo_baseline/map010-verify-godot-PASS.txt`).
  - First authoring iteration had a real build failure (`IsEmpty` on
    `Array<Dictionary>`, `GetWorld3D` on Node) and a coordinate-convention
    finding (placement-local vs world spawn) — both fixed in-slice; no
    production code changed.

- MAP-009 (P0): final trigger contract verified 2026-09-03 (commit `998abc8`).
  - Receipt `evidence/act1_repo_baseline/map009_final_trigger_contract.md`:
    approach gating (zirat clue condition), three `scene/forest` entry
    conditions, terminal effects fire once in `onEnter` (no scene
    interactions -> non-retriggerable), state-assignment idempotency,
    reveal-not-before invariant simulated 16/16, presentation card read-only
    and once-guarded.

- MAP-008: zirat->Kara corridor inspection 2026-09-03 (commit `0748308`).
  - Authored escalation documented: envelope widths 4.2->3.5 m, darkening
    surface colors, asymmetric banks, root clusters, lateral forest shelves,
    bounded Kara value separation; kara_approach capture frames 5/5 distinct.
    Human bidirectional video remains.

- MAP-007: zirat transition inspection 2026-09-03 (commit `0549e7c`).
  - Boundary fence out of road window ("open presentation cue, never a
    blocker"), non-inscribed markers unchanged, village-edge return sightline
    authored, zirat capture frames 5/5 distinct. Religious review =
    CULTURE-003, BLOCKED_EXTERNAL.

- MAP-005 + MAP-006: inspections 2026-09-03 (commit `b4b92fb` — both receipts
  share one commit; deviation from one-task-per-commit noted, strict one-task
  commits resumed from MAP-007).
  - MAP-005: branch read chain (RoadToFap trigger at connector start, two
    diegetic ФАП landmarks, authored BranchWet road surface).
  - MAP-006: duplicate-view blocker removed with evidence — connective return
    frames have three camera transforms and three PNG SHAs in diag-run2;
    continuity anchors documented.

## Completed

- MAP-006: verification slice done 2026-09-03 (no code edit).
  - Former blocker removed with evidence: the three connective_street_return
    capture views are distinct on the current build — diag-run2 frames 23–25
    have three camera transforms and three PNG SHAs (`de1028a3…`,
    `729a5222…`, `177b0ef8…`), `same_as_prev=false`; 44/44 gate passed twice.
  - Continuity anchors documented (FapYardGate/FapYardLoop egress,
    house-to-zirat-return connector, return boundary fences,
    ApproachWorn transition segment, single persistent world).
  - Flat-corridor/empty-horizon judgement stays human.
  - Evidence: `evidence/act1_repo_baseline/map006_return_street_continuity.md`.

- MAP-005: inspection slice done 2026-09-03 (no code edit).
  - Receipt `evidence/act1_repo_baseline/map005_fap_branch_sightline.md`:
    branch read chain — `RoadToFap` trigger at connector start with diegetic
    prompt, two diegetic «ФАП» landmarks (smoke-protected board label +
    street landmark), authored `RoadCrown_BranchWet` road surface (ART-002),
    single FAP destination volume. Human route clips remain with the task.

- MAP-004: inspection slice done 2026-09-03 (no code edit).
  - Receipt `evidence/act1_repo_baseline/map004_arrival_house_chain.md`:
    arrival→yard-gate→door chain documented from `AgentBAct1Layout`
    (`HousePathAxis`, `WalkChain` with the open yard gate gap z 1.75–3.45),
    `BabaiYardOpenGate` composition anchor (`Act1ConnectedWorld.cs:4765`) and
    the walkthrough's ±0.01 m HouseDoor-at-anchor assertion with unchanged
    ray standoff. Physical walk evidence = walkthrough PASS 135,49 m.
  - Human first-time wayfinding observation remains NEW VERIFICATION REQUIRED
    with the task; no marker/teleport fallback introduced.

- MAP-003: inspection slice done 2026-09-03 (no code edit).
  - Receipt `evidence/act1_repo_baseline/map003_connector_inspection.md`:
    per-connector table (lengths 10.4–64.7 m, widths 2.6–5.6 m) computed from
    `Act1WorldLayout.Connectors`; all seven endpoints lie over the shared
    traversal ground (x ∈ [−43,43], z ∈ [−152,+56]) and each has its own
    collision-bearing traversal strip; visual crown owned by AgentB terrain
    (RoadSurface presentation suppressed by design).
  - Mechanical coverage proven by construction + launch-smoke ground-envelope
    assertions + physical walkthrough (135,49 m). Human edge-walk video of all
    seven connectors remains NEW VERIFICATION REQUIRED with this task.
  - Baseline `verify-godot` exit 0 on `e50c64a` (parent commit changed docs
    only).

- BASE-004: REWORK slice done 2026-09-03 (include/exclude decision manifest).
  - Authored `docs/production/act1_package_include_exclude.md`: grounded in the
    current debug presets (`export_filter="all_resources"`, empty filters,
    `game/export_presets.cfg:9-12,50-53`); fixes the Act I include list
    (launch chain scenes/scripts, 5 logical zone scenes, Act I kits,
    production textures, ambient manifest + Act I stems, chapter1 compiled
    pack) and the exclusion list (fullgame campaign + compiled fullgame pack,
    dev campaigns, fullgame scenes, candidate-texture provenance, retired
    candidates, test-only scenes) with an explicit no-deletion enforcement
    boundary.
  - PCK/resource-level comparison remains NEW VERIFICATION REQUIRED behind
    RELEASE-001/RELEASE-003 (no release preset exists yet); no packaging code
    changed in this slice.

- MAP-002: REWORK slice done 2026-09-03 (metric separation).
  - `act1-first-person-walkthrough` PASS line now carries
    `mode=physical-characterbody-walk`; capture PASS line carries
    `mode=presentation-waypoint-audit`; `playtest_plan.md` gained the MAP-002
    rule that 135,49 m (physical walk) and 242,50 m (waypoint audit) are
    different measurements and the audit never counts as gameplay completion.
  - `./eng/verify-godot.sh` exit 0; walkthrough PASS with new label
    (`evidence/act1_repo_baseline/map002-verify-godot-PASS.txt`).

- BASE-006: REWORK slice done 2026-09-03 (queue authority repointed).
  - `execution_backlog.json`: `authority` now names the repo-grounded tracker
    as the active queue, lists the old full tracker as superseded provenance
    (fictional commands never to be executed/cited), and adds the wave/P0> P1>
    P2 queue rule; `orchestrator_policy.queue_rule` mirrors section 0 of the
    tracker; `updated` = 2026-09-03. JSON parse verified.
  - `backlog.md` header: same repoint note; single active queue, no second
    task owner.
  - IDs are imported per executed wave only (BASE/MAP/ART wave-1+2 rows so
    far), not bulk-imported.

- BASE-008: DELETE NOW done 2026-09-03.
  - Removed both tracked host-noise files from the index (kept on disk;
    `.gitignore:16` `.DS_Store` rule keeps future files untracked):
    `.DS_Store`, `public/.DS_Store` (`git ls-files '**/.DS_Store'` now empty).
  - No other cleanup performed (no bulk hygiene, no source-asset deletion).

- ART-006 (automatable portion): documented reproducible export 2026-09-03.
  - Audit: kit already carries the authored threshold/boundary family (13
    roots: asymmetric banks, mixed tree clusters, crooked pine, birch edge,
    root walls, fallen logs, boulders, stump, two distant closure masses with
    a central road gap; 156 meshes / 3,144 tris / 16 materials). Runtime
    composition places each root once with distinct roles — no repeated-clone
    crown wall at composition level.
  - Added `tools/blender/export_kara_forest_edge_kit.py` (same pattern as
    ART-005): validates contract and exports with fixed settings.
  - Proof: byte-stable re-export — GLB SHA-256 `28f58eaa…0d48a` identical
    before/after; `./eng/verify-assets.sh` exit 0.
  - No geometry changes authored: silhouette additions risk the
    creature-as-prop hard stop and require human 360 review (Z08-004/Z08-006,
    CAPTURE-006).
  - BLOCKED_EXTERNAL remainder: human 360/lateral-density art review.

- ART-005 (automatable portion): documented reproducible export 2026-09-03.
  - Audit: the kit already carries the tracker's required restrained geometry
    (boundary fence with deliberate gate gap, swung-open low timber gate,
    non-inscribed plain marker groups, path edge, birch/shrub framing, distant
    village mass; 11 roots / 234 meshes / 7,160 tris / 24 materials; no
    images/symbols/collision).
  - Added `tools/blender/export_zirat_roadside_kit.py`: validates the
    component contract (roots/parents/counts, no images, no collision-like
    names, no camera/light/physics) and exports the GLB with fixed settings.
  - Proof: re-export of the unchanged source is byte-stable — GLB SHA-256
    `f2f87e87…963a3d` identical before/after; `./eng/verify-assets.sh` exit 0.
  - No geometry changes authored: marker/boundary additions require
    cultural/religious review first (tracker hard stop on invented
    inscriptions/symbols).
  - BLOCKED_EXTERNAL remainder: 360° in-engine review + specialist
    religious/local sign-off (CULTURE-003 scope). Evidence package:
    manifest + reproducible export receipt + verify-assets log.

- ART-002: REWORK slice done 2026-09-03 (authored variant road modules + integration).
  - Gap: MainStreet/ReturnStreet/ReturnTransition all placed literal clones of
    the single `RoadCrown_SunkenWet` strip → visible tile rhythm; FAP branch had
    no authored road segment.
  - Generator (`urman_wet_village_road_kit.py`): added two authored variant
    modules — `RoadCrown_BranchWet` (narrower 2.5 m muddier branch strip,
    off-centre crown, 2 puddles, worn patch) and `RoadCrown_ApproachWorn`
    (worn 3.0 m approach strip, 3 clods, 3 patches, 1 restrained puddle) —
    sharing the crown height contract (z 0.018–0.092, relief ≤ 0.18), existing
    19-material palette, axis bake and validation. Rebuilt .blend + GLB
    deterministically (192 meshes / 3,042 tris).
  - Integration (`Act1ConnectedWorld.cs`): component contract +2; return
    transition now uses the distinct `RoadCrown_ApproachWorn` segment instead
    of a third `SunkenWet` clone; new `WetVillageRoadFapBranchCrown`
    (`RoadCrown_BranchWet`) placed at the `village-to-fap-branch` connector
    midpoint. Presentation-only; collision/interaction owners untouched.
  - Manifest + registry updated (hashes/counts/components/status).
  - Verify: `./eng/verify-assets.sh` exit 0 (41/41 hashes),
    `./eng/verify-godot.sh` exit 0 (physical walkthrough PASS 135,49 m to
    completed cliffhanger). In-engine motion review remains part of the
    zone-level human review (Z01-002–Z08-002).
  - Evidence: `evidence/act1_repo_baseline/art002-verify-assets-PASS.txt`,
    `evidence/act1_repo_baseline/art002-verify-godot-PASS.txt`.

- BASE-007: REWORK → verified + registry completed 2026-09-03.
  - Baseline `./eng/verify-assets.sh` exit 0 before edits
    (`base007-verify-assets-baseline-PASS.txt`).
  - Gap found: 4 of 5 Act I kits missing from `assets/asset_registry.json`
    (wet road, village exterior, zirat roadside, kara forest edge; only FAP kit
    was registered). Added entries with real source/derived SHA-256, GLB-derived
    mesh/triangle/material counts cross-checked against manifests (177/2,718;
    106/3,512; 234/7,160; 156/3,144), honest generators (zirat/kara: manual
    .blend export, no generator script) and `art-lock`-open statuses.
  - Added `releaseDisposition` to all 41 assets: runtime-referenced assets =
    `include-in-act1-release` (per `PainterlyMaterialLibrary.cs` texture usage
    and `ambient_manifest.json` zone routing); v3–v6 texture candidates =
    candidate-provenance/exclude; `audio.ambient.water-edge` =
    deferred-fullgame-scope/exclude. No unknown licenses (all
    Project-original/Project-generated).
  - Post-edit `./eng/verify-assets.sh` exit 0, `assets=41 derived=41
    explicit_sources=15` (`base007-verify-assets-with-kits-PASS.txt`).

- ART-001: KEEP 2026-09-03 (docs-only; three existing art docs fixed as the
  Act I acceptance authority, no new style bible).
  - Decision recorded in `docs/urman_knowledge_base/decision_log.md`
    (2026-09-03 entry): `design_style.md`,
    `art/act1_master_layout_2026-08-17.md`,
    `art/act1_visual_reference_bible_2026-08-17.md` are the acceptance
    reference for every Act I art/zone task; target PNGs are references, not
    runtime screenshots.
  - Manual cross-check vs current runtime: docs declare Painterly Low-Poly
    full-volume walkable 3D; runtime implements it via
    `PainterlyMaterialLibrary` presets + authored Act I kits (consistent, no
    contradiction found).

- BASE-005 + CAPTURE-002: PASS 2026-09-03 (capture harness diagnostics + readback
  pose assertion; two consecutive clean 44/44 runs).
  - Diagnosis: supplied 44 files / 25 unique failure did NOT reproduce on
    current HEAD. Harness code unchanged since `fc58c40` (single-commit history);
    failure was environmental/dirty-tree in the supplied handoff run, not a
    current-code settle/readback/camera defect. Diagnostic run shows perfectly
    deterministic pacing: `drawn_frame` advances exactly 18 per spec (37 → 811),
    zero `same_as_prev` frames, all aim deviations ≤ 0.028°.
  - Causal guard added (also closes CAPTURE-002 scope): readback-time assertion
    that actual camera forward matches the requested target within 0.5° (fails
    immediately if the pose was not the rendered pose), plus per-frame log of
    camera transform/forward, active logical zone, drawn frame index, sha256
    prefix and same-as-previous flag. 44/44 uniqueness gate unchanged and
    unweakened; no delays increased.
  - Runs: run1 `/tmp/urman-base005-diag.6NkUiP` exit 0, 44 PNG / 44 unique;
    run2 `/tmp/urman-base005-diag2.Qt8gyk` exit 0, 44 PNG / 44 unique (raw PNGs
    ephemeral in /tmp; retained evidence = logs + receipt below). The
    pre-existing duplicate groups named in the tracker (connective_street_return
    ×3, fap_exterior ×5, zirat ×4, kara_approach ×5) all produced distinct
    transforms and distinct PNG SHAs in both runs.
  - Evidence: `evidence/act1_repo_baseline/base005-capture-diag-run1-PASS-44of44.txt`,
    `evidence/act1_repo_baseline/base005-capture-diag-run2-PASS-44of44.txt`,
    `evidence/act1_repo_baseline/base005-capture-receipt.json`.
  - Note: CAPTURE-003 remains open until the post-art-candidate rerun with a
    retained evidence package; CAPTURE-001 keeps the failed 44/25 receipt as
    historical FAIL evidence.

- MAP-001 verify: DONE — VERIFY 2026-09-03 (no code edit, canonical baseline recorded).
  - `./eng/verify-godot.sh` exit 0 on `5539c9b`; walkthrough smoke PASS
    (`distance=135,50m final-zone=kara_urman_night cliffhanger=completed`).
  - Canonical baseline fixed in receipt: 5 placements, 7 connectors, 8 direct
    visual zones (`Act1ConnectedWorld.cs:805-812`; capture harness asserts the
    same list at `Act1FullRouteCoreWorldCapture.cs:248-262`).
  - Evidence: `evidence/act1_repo_baseline/map001_route_baseline.md`.

- BASE-003 inspection-first: KEEP/VERIFY 2026-09-03 (no code edit).
  - Contract proven by source inspection on `66684b7`: `act1_demo.tscn` →
    `Act1DemoRoot` → one `main.tscn` instantiation with
    `EnableAct1ConnectedWorld=true`; one `Act1ConnectedWorld` under `ZoneHost`;
    connected-mode `SwitchZone` guards non-Act-I zones; fallback per-scene
    loader only reachable with flag false (tests/fullgame). Owners: 5 placements
    / 7 connectors in `Act1WorldLayout`; single labeled FAP owner with
    suppressed `Fap_*` families; `CountActiveWorldEnvironments == 1`; single
    `RuntimeBridge` + single `AmbientAudioDirector` in `main.tscn`; no
    scene-local narrative store. Assertions enforced by
    `Act1DemoLaunchSmokeTest.cs:41-109`.
  - Evidence: `evidence/act1_repo_baseline/base003_owner_inspection.md`,
    `evidence/act1_repo_baseline/base003-verify-godot-PASS.txt` (exit 0).

- BASE-001 + BASE-002 preflight: PASS 2026-09-03 (commit `66684b7`).
  - Root cause of initial `verify-dotnet` FAIL: stale frozen golden fixture
    `tests-dotnet/fixtures/content/urman.chapter1.compiled.v1.json` (fp `d89a…`)
    vs legitimate HEAD authored content (Mansur PC gate, Naila dialogues, house
    exit gates, zirat clue; fp `38fdae…`). Runtime pack
    `game/content/urman.chapter1.compiled.v1.json` already matched fresh compile.
  - Fix (test maintenance only, no content change): refreshed fixture from
    `ContentCli compile`, updated hardcoded fingerprint in
    `ContentCompilerParityTests.cs:26`.
  - `validate` exit 0 (4 modules, 4 campaigns); `simulate urman.chapter1` 16/16.
  - Evidence: `evidence/act1_repo_baseline/verify-dotnet-baseline.txt` (initial
    FAIL), `evidence/act1_repo_baseline/verify-dotnet-PASS.txt`,
    `evidence/act1_repo_baseline/verify-godot-PASS.txt`.

## Blocked / BLOCKED_EXTERNAL

- WAVE 9/10 visual production gate remains `PARTIAL` / `REWORK`: the
  master-layout/terrain basis is retained after a real Arrival/MainStreet
  improvement, but the Professional gate is still `OPEN` for overlapping
  road/fence ownership, empty reverse/lateral horizons and close
  crop/scale crowding; FAP, Zirat and Kara remain partial/open as described
  above. Affected zone/art IDs are `OPEN` or `PARTIAL`, not `DONE`.
- Cultural, language, human, audio, platform and release gates remain open.

## Reopened

- (none)

## Commands / evidence (exit codes)

- Supplied Wave 9/10 root report (not rerun in this documentation-only slice):
  asset registry `55/55 PASS`; `.NET` build `0 warnings/0 errors`; Core
  `43/43`; Content `12/12`; diagnostics `0`; physical first-person
  walkthrough `PASS`, `135.49 m`, through the final Kara cliffhanger; hidden
  capture `/private/tmp/urman-wave10-capture.5sjDbu` = `44/44` unique
  PNGs, 10 visual zones, `242.50 m` waypoint audit.
- Full `verify-godot` aggregate in isolated temporary HOME: only the
  editor-profiler `user://... ObjectDB Snapshots` directory setup failed;
  individual main-menu, pause, scene smoke, corridor and full physical
  walkthrough checks passed headless/Dummy. This is an environment/harness
  caveat, not a product pass/fail.
- Documentation verification: `git diff --check --` for the two owned files
  only (see final result below). No build, Godot, capture, test, graphify or
  external command was run in this slice.

## Dirty state / чужие изменения (preserved, not mine)

- `M AGENTS.md` — pre-existing narrow exception clause for this exact user-authorized OpenCode Act I run (12 insertions). Preserved, excluded from task-owned commits.
- `M .DS_Store` (binary 8196 → 10244) — host noise, owned by future BASE-008. Untouched for now.
- `?? URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` — tracker input file, untracked. Left untracked.

## Next ready IDs

- Next wave / automatable priority: use the existing `ART-003…ART-012`,
  `Z01-001…Z08-008`, `HOUSE-001…HOUSE-008` and `FAP-001…FAP-008` rows for a
  targeted visual-owner diagnosis of overlapping road/fence and legacy
  Zirat/Kara nodes. Keep the improved master-layout/terrain basis, preserve
  current `OPEN`/`PARTIAL` statuses and do not add duplicate IDs.
- After that diagnosis, apply only the required source/material/lighting
  polish, freeze the candidate, take one hidden capture and perform human root
  review. This is not another broad kit-count pass; do not substitute
  speculative C# presentation suppression, UI, performance, packaging or
  release work before this sequence is accepted.
- Only after that evidence cycle: CAPTURE-004 supplemental directional
  evidence (up/down + pitched views), then human cultural/language/audio/
  accessibility, Windows/M1 and release gates. Technical 44/44 and walkthrough
  PASS do not close the visual gate.

## External gates

- ART-005 remainder: specialist religious/local sign-off + 360 in-engine
  review — BLOCKED_EXTERNAL.
- ART-006 remainder: human 360/lateral-density art review — BLOCKED_EXTERNAL.
- MAP-003 remainder: manual seven-connector edge-walk video — BLOCKED_EXTERNAL.
- MAP-004 remainder: observed first-time wayfinding — BLOCKED_EXTERNAL.

## Resume point

- If interrupted now: resume with the targeted visual-owner diagnosis for the
  existing road/fence and legacy Zirat/Kara nodes, then source/material/
  lighting polish, one hidden capture and human root review. Keep the improved
  master-layout/terrain basis, preserve all current task IDs/statuses, and keep
  cultural, language, religious, human, audio, platform and release gates open.

## 2026-09-10 — painterly texture families / technical candidate pass

- Status: `TECHNICAL PASS / ART LOCK OPEN`.
- Generated with built-in ImageGen: 19 albedo PNGs covering 12 missing surface
  families. All delivered files are 1 024 × 1 024 RGB; exact prompts, SHA-256
  and candidate status are recorded in
  `game/assets/textures/painterly/README.md`.
- Runtime selection: `grass_verge_v1`, `roof_slate_v1`, `roof_metal_v2`,
  `bark_birch_v1`, `bark_pine_v1`, `leaf_birch_v1`, `log_wall_v1`,
  `wallpaper_old_v1`, `wall_institution_v1`, `carpet_palas_v1`,
  `fabric_chit_v1`, `ornament_trim_v1` and `wood_carved_gate_v1`; the remaining
  siblings stay documented candidates. Ornament and carved wood are restricted
  to the hero house/window and one authored gate family.
- Evidence: `evidence/act1_repo_baseline/textures_new_families/` contains 19
  2×2 seam mosaics, `overview.png`, `tile_review.png`, `seam_metrics.tsv` and
  six runtime before/after sheets plus `runtime_review_before_after.png`.
- Fresh technical results: build `0 warnings / 0 errors`; physical walkthrough
  `PASS`, `335.27 m`, final zone `kara_urman_night`; capture `48/48` unique PNGs;
  benchmark `119.90–120.18 FPS` against the 30 FPS floor; full
  `./eng/verify-godot.sh` `PASS` (exit 0) after the intentional modular-kit
  material-contract update.
- Open gates: moving human review for grass repetition/final family choice and
  cultural approval of the Tatar ornament/carved-wood placement. No art lock,
  canon change, commit, push or release claim.
