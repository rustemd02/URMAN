# Weak Points

## Свежий авторский срез — 28 сентября 2026

[Полный реестр](../tasktracker/review_2026-09-28/coverage.md) заменяет выборочный пересказ претензий. Наиболее опасны:
непонятная сюжетная причинность/сопоставление и входы; разрыв качества меню и живой сцены; масштаб/щели/плавающие
объекты; бесцельные интерактивы; неестественная пластика; низкий FPS на авторском M1. Это сообщения автора,
не свежая репродукция на данном HEAD. Кодовая причина knock→door_creak требует звуковой проверки результата исправления.

Риски процесса: бесконечная полировка одного квеста, массовые ассеты до эталона, ожидание всех внешних решений,
квоты текста вместо интереса и ложный перенос PASS. Против них — ограниченные units/критерии/stop в единой очереди.
[Зависимости производства](../tasktracker/review_2026-09-28/implementation_blockers.md) отделены от продуктовых вариантов.

## Отладочные переходы: двенадцать точек, две мёртвые записи починены — 2026-09-27

`village_day@mosque` и `kara_urman_night@forest-approach` не резолвились в
`Act1WorldLayout`, `Main.SwitchZone` прерывал переход, но сеанс и запись моста
уже случались (N2 аудита 2026-09-15). Теперь координату и разворот даёт владелец
места (`Act1ConnectedWorld.DebugSpawns.cs`): публичные здания — измеренная
площадка у крыльца, мечеть — точка адресного реестра и своя дверь, баня — своя
мета `entryApproach`, подход к лесу — конечная точка дороги; в список добавлены
магазин, школа, сельсовет/ДК и баня бабая, всего двенадцать. Неизвестная точка
отказывает до старта сеанса; после посадки печатается `act1-debug-zone-standing`
с `on-floor` и `speed`. Проверено машинно (`act1_main_menu_smoke_test` PASS,
двенадцать защищённых прогонов, receipt в
`docs/production/mvp_tracker_2026-09-27/debug-zones/`). Открытым остаётся
человеческий осмотр точек — вид и разворот камеры, — и то, что автотест меню
перепрыгивает один пункт из двенадцати ([журнал решений](decision_log.md)).

## Единичная авария native-запуска W05, 22 сентября — причина открыта

`texture-w05-20260922-high-01.log`: exit134 при построении сцены до проверки материала, `Invalid Program: attempted to call a UnmanagedCallersOnly method from managed code`. Системный `Godot-2026-09-22-193656.ips` указывает поток .NET BGC и JIT_ReversePInvokeEnterRare. Точный повтор той же DLL/аргументов и следующий Low прошли exit0; сохранения восстановлены guard во всех трёх попытках. Это не воспроизводимый отказ каждого запуска и не доказательство дефекта W05; источник аварии и риск повторения не закрыты. Следующая диагностика при повторении должна сохранить crash-stack и runtime identity, а не ослаблять проверки/отключать GC наугад. Связанный остаток текущей технической приёмки, без новой очереди; [журнал W05](../tasktracker/05_texture_production_2026-09-22.md).

## Подходы к трём сараям: отказ фонового запуска разобран, 22 сентября

Разрешено на `integrated-build-20260922-fence-settle-02`: причина начального
отказа — модальная блокировка при потере фокуса тестовым окном. На одной DLL
(`2bd467e3af4c99c003de2e55076351c5e09f19e5ddf057336be72156bf2d1ba2`)
без фонового флага у всех трёх адресов `ModalOpen=true`, `onFloor=false`,
`canStand=true`, присед/транспорт/intro/menu выключены; с уже существующим
`--urman-smoke-background-input` — `ModalOpen=false`, `onFloor=true` и PASS432,
failures=[] в `fence-settle-background-20260922-02/address-world-receipt.json`.
Пройдены все опубликованные точки подходов обычным контроллером; стык зирата
`allValid=true`. Этот флаг действует только вместе с `res://tests/*.tscn`;
обычная focus-loss автопауза, коллизии и прежний standing-критерий не менялись.
Guard восстановил userdata. Это техническое прохождение, не человеческая
навигационная или художественная приёмка.

История обнаружения:

На `integrated-build-20260922-fence-uv-02` существующий `AddressWorldSmokeTest`
со scope `standalone-access` записал 50 проверок и завершился exit1: у
`PerimeterWestStreetShed`, `ZiratVillageEdgeEastShed` и
`ArrivalReverseEastDomesticShed` не прошла начальная проверка
`local road fixture settles with the ordinary standing capsule`
(`AddressWorldSmokeTest.StandaloneAccess.cs:104`). В receipt шесть записей
ошибок: по check и exception на каждый адрес. Это не доказанный дефект коллизии
и не объявленная прежняя ошибка: сравнение с исходным состоянием не выполнено.
Маршруты далее не проверены. Отдельный `zirat-shed-fence-junction.json` из того
же запуска: `allValid=true`, две рейки, четыре опёртых торца, точная физика;
его результат не закрывает подходы. Данные восстановлены guard.

Копии файла стыка трёх запусков побайтно совпали; оставлена одна в
`fence-settle-background-20260922-02/zirat-shed-fence-junction.json`,
SHA `36fd75df3cfb89be537698f16196ba130069a34192da188e1569b6d62561e05a`.

Связанный остаток действующих TECH/DEPTH-задач, без новой параллельной очереди:
проверить постановку/стойку обычного контроллера и доступность этих точек, не
ослаблять standing-критерий и не менять коллизии без подтверждения.
Свидетельства: `../production/act1_takeover_evidence_2026-09-16/texture-fence-uv-standalone-20260922-02.log`
и одноимённый каталог без `.log`.

## Boundary-архитектура: 5 новых падений на B90 (2026-09-21, открыто)

`act1_boundary_architecture_smoke_test` на `integrated-build-90`, exit 1,
1018 записанных проверок, лог
`../production/act1_takeover_evidence_2026-09-16/act1_boundary_smoke_2026-09-21.log`:

- `BabaiFirewoodShelterPost`: обычный контроллер не касается стены столба
  (`wall contact=False, separation=1.017m`);
- `MosqueEntranceDoor`: видимая створка без физической опоры на 0.90 м
  (расхождение 0.027 м) и на 1.55 м (0.028 м);
- `fap_window_right`: **проверочная точка исправлена 2026-09-22**. Старый луч
  действительно пересекал видимую рейку перегородки (контакты совпадают с
  треугольниками меша), это не лишняя production-коллизия. Перенесена только
  standing fixture x=3.35 → 4.75, сохранены CanStandAt/settle/clear-ray.
  На `integrated-build-20260922-texture-lang-02` узкий native clinic scope
  прошёл 113 проверок; `fits/standing/clear=true`, failures=[] в
  `../production/act1_takeover_evidence_2026-09-16/texture-clinic-20260922-02/architecture-receipt.json`.
  Подход к этой fixture не выдан за человеческий маршрут; остальные B90-падения
  не перепроверены этим clinic scope;
- Пересаженное дерево в `(-33, 8.2)`: потерян LOD или физический ствол остался.
Связанные задачи: TECH/DEPTH/CHAR-блоки. Не блокирует EX01–EX06 (carry-смоук
зелёный), чинить отдельным проходом с кадрами до/после.

## Лампа просвечивала сквозь стену у метки — исправлено 2026-09-21 (EX06)

`PortableLight.ReachesWithSight` считал заслонённой стеной луч «дошедшим», если
точка попадания была в пределах `GrazeMargin .22` от метки. Когда лампу держат
вплотную к метке (в смоуке ~0.3 м), стена-фиxture в середине отрезка давала
попадание в ~0.11–0.16 м от метки — внутри grace — и чтение открывалось сквозь
стену (`act1_carry_interaction_smoke_test`, `a wall between the lamp and the mark
blocks the reading`). Исправление: grace оставлен только для собственной ближней
грани метки, любое другое тело на луче лампа→метка — тень
(`PortableLight.cs`, гейт передаёт `target` как `markBody`). Проверено дважды
подряд 320/320 на `integrated-build-90`, лог
`../production/act1_takeover_evidence_2026-09-16/act1_carry_smoke_2026-09-21.log`.
Попутно: на этом хосте 1/4 прогонов ронял финальный settle возврата с топором
(`SettleWoodpileFeet`), теперь при падении печатаются stableFrames/canStand/feet.
Подъём второго отрезка из раздела ниже на этой сборке в 3/3 прогонах проходил —
окно регрессии B42→`1c1a90c` остаётся открытым наблюдением, не приговором.

## Маршрут по боковой площадке бани не проходится шагом (2026-09-19)

2026-09-19, carry-smoke на слитой сборке (`integrated-build-85`, лог
`../production/act1_takeover_evidence_2026-09-16/act1_carry_smoke_2026-09-19.log`):
сценарий `ApproachWoodpileFromBathSteps` (новая ветка, пришедшая с origin) доводит игрока
до `entryApproach`, поднимает по лестнице (`BathEntryTread*`, подъём ≤0.17 м на ступень)
и на третьем отрезке — «reach the woodpile view by walking onto the actual upper landing»
`WalkLocal(bypass[1] = bath.ToGlobal(3.70, 0, 1.84))` — срывается: игрок уходит **вниз**
(старт `(-30.02, 1.11, 4.57)` → финал `(-31.70, 0.63, 4.14)`), упирается в
`BathEntryTread1Body` / `BathEntryLandingBypassBody`, и шаг отклоняется строкой
`no walkable tread within step height` (`FirstPersonController.Steps.cs:56`,
лимит `MaximumStepHeight = .22f`). `WalkLocal` проверяет только горизонтальное
приближение (`Vector2(X,Z)`), поэтому предыдущий отрезок «засчитан» на 0.3 м ниже площадки.
Дефект не связан со слиянием 2026-09-19: маршрут стартует из фиксированного
`entryApproach`, а `Act1ConnectedWorld.Bathhouse.cs` и шаговая логика контроллера в слиянии
не менялись. Окно регрессии известно: тот же маршрут проходил на B42 (2026-09-17,
HEAD `04f039e`, запись `progress_2026_09_17_B42` в очереди — «actual carried shovel
ascends existing steps, all 9 feet rays on real landing»), а 2026-09-18 в `1c1a90c`
появились новая шаговая логика (`FirstPersonController.Steps.cs`) и обычный обзор через
`ScreenRelative`. Синтетические повороты при этом сходятся: в логе все 11 записей
`act1-carry-ordinary-turn` имеют `ready=true`, `delivered=1`; срывается именно **второй**
подъём (за топором, `localFixture: false`) — первый подъём и спуск за лопатой проходят.
Нужно: либо удержать игрока на боковой площадке (ширина 1.0 м) и дать честный маршрут
подъёма на неё, либо разобрать расхождение траектории второго подъёма; проверять шагом,
а не телепортом, и не засчитывать фиктивное «прибытие» (сейчас `WalkLocal` сверяет только
`Vector2(X,Z)`, поэтому отрезок «засчитан» на 0.3 м ниже площадки).

## M1-фото недостижимо, отладочные спавны и контент-гейт сломаны (верификация 2026-09-15)

2026-09-15, второй проход по коду (`../production/urman_ttz_audit_verification_2026-09-15.md`):

- `ArrivalPhotoTarget` (`Act1ConnectedWorld.ExteriorDiscoveries.cs:77–83`) создан с
  **пустым** `interactionId`, поэтому `RuntimeBridge.IsInteractionAvailable("")` всегда
  ложен: цель навсегда получает `CollisionLayer=0` (`InteractionTarget.cs:164–187`, маршрутизация
  гасит её повторно в `Act1ConnectedWorld.cs:606–630`). Документ
  `urman.chapter1:document/arrival-photo-evidence` есть в паке, но на него не ссылается
  ни одна сущность — память Марата в игре не получить. Тесты закрывают только
  программное открытие документа (`JournalFlowSmokeTest.cs:159`).
- Две записи меню «Отладка: локации» (`MainMenuUi.cs:52–53`: `Мечеть`,
  `подход к лесу`) ссылаются на спавны, не объявленные в `Act1WorldLayout`
  (`village_day/mosque`, `kara_urman_night/forest-approach`): `Main.SwitchZone` печатает
  ошибку и прерывает переход (`Main.cs:98–111`), а `Act1DemoRoot` после этого всё равно
  записывает запрошенные zone/spawn в мост. Тест проверяет только `village_path`.
- Гейт контента красный: `npm run content:check` падает, `npm run test:content` 6/68 fail,
  `npm run test:runtime` 3/102 fail — одна причина: `urman.chapter1` ссылается на
  документы `urman.oldpc` в `journalAction.sourceIds`, а манифест объявляет
  `"dependencies": []` (обратная зависимость oldpc→chapter1 уже есть, поэтому просто
  объявить нельзя — будет цикл). Ссылки внесены коммитом `401f5bd`; `content:check`
  не входит в `eng/`-проверки, поэтому расхождение накопилось незамеченным.

## Управление и двери расходятся с ТЗ на доработку

2026-09-15, аудит ТЗ (`../production/urman_ttz_compliance_audit_2026-09-15.md`):
прыжка нет вовсе; действие `crouch` связано с C/gamepad B и переназначается в
настройках («Пригнуться» в `SettingsUi.cs:40`), но его не читает ни один скрипт —
UI обещает механику; у игрока нет меша, ног и тени (`first_person_player.tscn`);
двери — статичные меши без состояний (открыта/заперта/стук). Пока это не
исправлено, настройки ввода вводят игрока в заблуждение, а ТЗ §2–§4 остаётся
невыполненным. Отдельно: премиса ТЗ об автоматическом стуке не подтверждается
кодом и историей git — стука нет ни автоматического, ни ручного.

## New painterly surface families remain candidates

2026-09-10: 19 ImageGen albedos cover 12 previously missing surface families
and are wired into the existing material/runtime paths. Technical seams,
imports, traversal, capture and the 30 FPS floor are covered by evidence under
`docs/production/evidence_archive/act1_repo_baseline/textures_new_families/`, but large grass planes can
still expose repetition and the Tatar ornament/carved-wood choices have no
human cultural approval. Keep art lock open until moving first-person review
selects final family variants and a cultural reviewer accepts the restricted
hero-house/gate placement.

## Connected Act I map remains the primary visual gate

2026-09-07, tree geometry (user art feedback): the shared conifer builder
no longer stacks flat single-colour flakes — it now builds a two-layer
crown (open drooping skirt widest in the lower third + darker inner fill
mesh + upturned leader tip), the birch trunk is thicker with base flare
and carries a second smaller leaf spray on each hanging shoot, and the
arrival uncut-verge grass pattern extends along the main street with
gaps at gates and the FAP apron. Benchmark stays green (111-120 FPS avg
vs the 30 floor). Zoom pairs: docs/production/evidence_archive/act1_repo_baseline/trees/.
Broadleaf/understory richness and whole-zone art acceptance remain open.


2026-09-07, kit material grading: probe-verified that the conspicuous
mint-blue mass beside the west main-street fence was the road kit's
`FernShrubBreak` shrubs rendering raw `Shrub_BlueGreen`/`Fern_MossGreen`
GLB albedo outside `RebindWetVillageRoadMaterials`; bound the eight
missing organic/fence entries plus the village kit's well water, cut
wood, bark and dull metal in `RegradeAct1DaylightKitMaterials`. Frames:
mass now reads as a muted bog-green bush, fences dark wet wood, well
water dark (docs/production/evidence_archive/act1_repo_baseline/matfix/). This closes that
specific raw-albedo family for the road+village kits. Same-day follow-up
bound the zirat roadside and FAP kit leftovers (MossGreen, MossyStone,
WeatheredWood/Dark, DistantFence/Foliage, DitchWater, WetSheen,
FapBirch/Shrub/Vent) in the same table: cemetery pale stone/shrub masses
and washed clinic greens are gone from the frames
(docs/production/evidence_archive/act1_repo_baseline/sliceb/). Kara edge materials were already
fully covered by their scoped table; agentb kits receive materials from
their C# builders rather than raw GLB albedo.

2026-09-07, street facade diversity: five perimeter clones of the single
`DwellingFacade_TimberPlaster` component (west/east street-mid,
west/east return-mid, east-street horizon) now place the kit's three
full-volume variant dwellings (VariantA/B/C) at house scale 0.88-0.90.
Frames: mixed families read on both street sides
(docs/production/evidence_archive/act1_repo_baseline/slicec/); walkthrough and capture stay green.
Near-camera clones (arrival group with the hero window, side-closure,
zirat village edge) are intentionally retained. Subagent review found the
fifth conversion (EastStreetMid) had silently failed plus two geometric
defects: the far holding crossed its own/neighbour fences (real VariantC
AABB 8.63x6.32), and the babai service-yard fences cut the drawn house
path. All fixed the same day: rule narrowed to "FarHolding", holding
relaid out with measured clearances, babai boundary rebuilt as an
L-fence north of the path, fifth conversion applied at a shifted anchor
(docs/production/evidence_archive/act1_repo_baseline/sliced/).

2026-09-07, Kara ground life: the flat boulder-disc cluster moved off the
road shoulder into the stand (yaw -35, 0.95 scale), muted stone clusters
and root shrubs added on the slopes outside the route envelope; one
first-candidate stone exposed on the open shoulder was rejected by its
own frame and removed. The duplicate ZiratVillageEdgeWestFacade in front
of the standing full-depth core house is removed (boundary remains on
the fence). Frames: docs/production/evidence_archive/act1_repo_baseline/sliceE/.

Follow-up (same day): continued footprint re-checks with real asymmetric
AABBs found the east side-closure facade volume intersecting the
near-mid holding house (merged two-shell mass at the road side) and the
relocated EastStreetMid dwelling crossing the holding boundary at
x=30.7. The intersecting facade is removed (the standing near-mid house
owns that read; shed/fence/trees stay as its yard) and EastStreetMid is
shifted to (25.3,-19.2) with a 0.5 m boundary clearance
(docs/production/evidence_archive/act1_repo_baseline/slicef/).

2026-09-07, Babai yard east depth: the miniature 0.44/0.48-scale
`BabaiEastDepthPlasterAnnexParcel` between full-sized dwellings is replaced
by the existing full-scale `OutbuildingShed_Low` with firewood and a picket
service boundary (road side open). First shed yaw candidate was rejected in
frames (windowless rear gable faced the depth camera) and corrected; build
0/0, walkthrough PASS 335,27 m, capture 48/48 with inspected yard frames.
This removes that specific miniature-parcel defect only; repeated facades,
bare plots and whole-zone art remain open.

2026-09-05, physical yard verification exposed a ground collision mismatch:
SharedVillageGroundTraversalCollision still spans 86x208 m with top at y=0,
while the visible height field dips below it. Yard detour passes but player's
Y stays ~0.00039 rather than following the visible ground. Resolve this and
connector/benchmark ground overlaps before claiming surface-aligned traversal.
Keep interior floors/stairs and narrative transitions intact; the old launch
test's BoxShape3D assertion must change with the actual ownership contract.
Follow-up verified: shared flat plane/connector collision removed, old exterior
benchmark surfaces disabled, interior floors retained. Launch test and full
GPU physical route pass (271.61 m); checked yard foot error .004/.001 m.
The specific flat-ground override is fixed; broader motion/collision and art
acceptance remain open.

2026-09-05, FAP-branch holding pass: duplicate miniature dwelling instances
removed and one shared shed resized/repositioned with front/rear boundaries.
Physical route passes 230.88 m; 44-view capture passes technically. Main's
review still rejects whole-plot art acceptance: main_street_right exposes
detached gate/fence fragments, flat unused ground and repetitive facades.
Next fix must join plot boundaries and entrances, not add another house layer.
Follow-up: holding side boundaries connected; orphan HouseA2 gate/fence
removed with its obsolete house. The remaining long beams traced to lower
rails outside their suppression owner; shared hierarchy corrected. Flat
parcel ground and weak architectural variation remain open, not solved by
removing these defects.
Close holding views now expose actual ground/junction defects rather than
only distant facades: removed the .4-scale shed blocking the entrance and
made the paths terrain-conformed/visible. Fixed winding exposed floating old
landform ribbons, now suppressed in favor of shared terrain. Integrated
46-view capture has no such strip regression, but angular path joins, missing
porch connection and unproven physical yard access remain open.

ФАП: скорректировано слишком тесное размещение четырёх соседних объёмных
домов. Белый треугольник внизу кадра оказался бликом Road_FapBranch, а не
пропущенным объектом: разделение материалов мокрой земли и дорожного грунта
убрало его в том же игровом ракурсе. Это устраняет конкретный артефакт,
но боковые/задние виды участков и общая художественная приёмка ещё открыты.

2026-09-05: переход в ФАП перенесён с главной улицы на существующую
площадку перед входом. Физический маршрут теперь включает ответвление к
зданию и проходит 171,04 м. В процессе обнаружены невидимые коллизии старых
домов и оград: автоматические имена повторных узлов обходили отключение
benchmark-геометрии. Семантические имена исправлены в двух конструкторах.
Крыльцо и двор всё ещё REWORK. Обратный путь теперь также проходит по
ответвлению ФАПа и дорожке к дому: 230,88 м за полный тестовый маршрут,
включая сохранение и загрузку посреди улицы. Реальные обратные ракурсы
проверены; это подтверждает этот проход, но не художественную приёмку.

На подходе к Кара-Урману устранена доказанная прямоугольная граница:
benchmark Ground и три дорожных сегмента больше не перекрывают общий
рельеф в connected world; коллизия сохранена. Сравнение одного физического
ракурса подтверждает непрерывность видимой дороги. Отдельная проверка
выявила пропущенные BirchBark/BirchLeaves/ShrubGreen в общей painterly-
палитре: назначение существующих материалов убрало белёсые кроны справа,
без изменения геометрии или света. Грубые объекты, плоская обочина и
композиция остаются REWORK, художественный gate не закрыт. Грубый объект
слева оказался папоротником: исправлены метровые ширины листовых сечений
общего генератора и разорванная конструкция вай. Реальный кадр подтверждает
небольшое связное растение; пустая плоская обочина от этого не стала готовой.

Физические кадры подхода к дому обнаружили ограды и стены, сквозь которые
персонаж проходил при зелёном smoke-тесте. Подтверждённые пересечения убраны
размещением домов/ограды и поворотом створки вокруг петли; дублирующий дом
удалён из того же участка. Повторные девять кадров и полный маршрут проходят.
Проверка сегментов по треугольникам — диагностическое дополнение, не замена
ручного осмотра всей карты и не подтверждение художественного качества.

Последний проход 2026-09-05 заменил сами дома: объёмные стены с проёмами,
вертикальные окна, тонкая столярка и закрытые боковые сени вместо внешнего
каркаса. Общая семья HouseA заменена тем же Blender-источником; берёзы и
садовые деревья получили разветвлённые кроны. Новый Godot-захват и физический
маршрут 137,60 м проходят, но свет пока плоский, участки чрезмерно регулярны,
старые мелкие навесы/кусты и конфликты компоновки заметны. Это PARTIAL/REWORK.
Пользователь требует постановку через paintover; встроенный image_gen дважды
вернул HTTP 404. Концептов нет, выбор направления не состоялся; точное
продолжение записано в существующем execution ledger. Не выдавать ранее
сделанную геометрию за реализацию ещё не созданного paintover.

2026-09-05, integrated arrival candidate: видимый DwellingFacade имел глубину
лишь около 2,4 м; источник расширен до ~5,2 м вместе с крышей и задними
деталями, два ближних дома увеличены до 95/98% метрового масштаба.
Ограда MainStreetEastNeighborFence ошибочно пересекала дорогу перед въездом;
ограда и калитка перенесены к восточному участку. Terrain_Main больше не
назначает цвет целым двухметровым клеткам: пигмент интерполируется по вершинам.
Физический полный маршрут 137,60 м и checkpoint restore проходят в отдельном
профиле. Захват показывает более весомые дома и открытый вид вдоль улицы,
но повторение фасадов, пустые участки и грубые наземные элементы сохраняются.
ART-002/003/007, Z01 и Z02 остаются PARTIAL/REWORK, не visual acceptance.

Следующий интегрированный проход удалил накладные диагонали основного
фасада и заменил разрозненные дорожные тайлы непрерывной существующей дорогой
с интерполированным цветом грунта/колеи. Реальный кадр въезда подтверждает
цельную дорогу; у зирата ещё видны перекрывающие полосы его собственного
набора. Повторный физический маршрут 137,60 м завершён. Пустые участки,
повторение домов и старые примитивные объекты не позволяют принять визуал.

2026-09-05, leaf-crown candidate: у общих берёз/лиственных деревьев сплошные
сферы заменены объёмными группами листьев. Повторный игровой захват показывает
просветы и более сложный силуэт, но это не production acceptance. Отдельные
импортированные деревья всё ещё состоят из шаров/конусов; ветви-коробки,
пустая земля и упрощённые дома сохраняют отвергнутый пользователем вид.
P0 «примитивный/N64 visual» остаётся открытым на всём маршруте.

Следующий проход заменил закрытые кроны трёх импортированных Birch-вариантов
в существующем Blender-наборе. Сравнение двора и въезда подтвердило удаление
крупных шарообразных крон. Листовые массы пока слишком тёмные и плоские;
хвойные силуэты, пустота земли и архитектура по-прежнему требуют переработки.

Исправлена потеря обратных граней листьев при Blender validation. Проверен
экспорт обеих сторон и игровой захват; материал использует корректную
матрицу нормалей и ограниченное светопропускание листвы без emission.
Заметного скачка общего качества это не даёт: следующие проходы должны
менять хвойную геометрию, землю и архитектуру, а не снова яркость листьев.

Общий хвойный генератор больше не строит сплошную коническую оболочку:
14 ярусов отдельных ветвей сохраняют просветы и объём. Слишком редкий
первый вариант отклонён; второй проверен на въезде и у Кара-Урмана.
Остались крупные угловатые объекты ближнего плана, голая земля и слабая
архитектура. Общий профессиональный visual gate не закрыт.

Четыре видимые полигональные массы у порога/в глубине Кара-Урмана удалены
из источника размещения и заменены 12 деревьями на тех же боковых участках.
Захват подтвердил исчезновение крупных «глыб» в прямом виде. Боковой вид
по-прежнему проваливается: пустой склон, старые блочные деревья и грубые
объекты земли. Это конкретное улучшение композиции, не готовая лесная зона.

Боковые склоны Кара получили 22 дерева в нескольких планах и привязанный
к рельефу подлесок. Общие кусты теперь используют листовые кроны вместо
сфер. Проверены левый/правый/обратный кадры: боковая глубина появилась,
но пустые промежутки, почти чёрная земля и старые блочные деревья сохраняются.

Проверка материалов исключила выключенные текстуры: medium, 153 активных
текстурированных материала, low-quality=0. Ослаблено подавление фактуры
общим шейдером и уменьшена частота повторения досок. Сравнение дома/въезда
показывает более читаемую поверхность, но это не исправляет примитивную
архитектуру, персонажей и пустую землю. Визуальный P0 остаётся открытым.

Пучки травы заменены с четырёх брусков на изогнутые сужающиеся травинки;
на въезде добавлены разреженные пятна вдоль обочин/задних оград. Ровная
полоса первого варианта отклонена. Финальный кадр подтверждает локальный
контакт растительности с землёй, но большие площади дворов ещё пустуют.

Чёрное блочное дерево справа у Кара оказалось дальним модулем, установленным
вблизи камеры. Убраны два таких размещения; участок остаётся заполнен новыми
деревьями склонов. Обратные светлые трапеции отдельно прослежены до
ZiratBirchShrubMass_MidCanopy_01_LOD0: этот берёзовый модуль присутствует
и в zirat-return-road, и в core presentation. Нужна проверка дублирования
и материалов; надгробия и религиозные элементы не затронуты.

2026-09-05: главный P0 — заметная пустота карты при обычном осмотре.
Непринятые дальние Pine_4/Pine_5 удалены после сравнения игровых кадров:
они почти не меняли композицию. Следующий кандидат формирует реальный
дальний водораздел за въездом с одинаковой высотой визуального рельефа и
коллизии. Это ещё не закрывает P0: дворы, боковые виды и лесная граница
требуют отдельной проверки и доработки.

Общий `AddVisualConifer` теперь строит связную ветвистую крону вместо
четырёх сплющенных сфер. Скрытый захват подтвердил устранение разрывов
силуэта, но близкие деревья всё ещё схематичны; лесные массы, подлесок и
их контакт с рельефом остаются `REWORK`. Это не финальное качество ассета.

Следующий проход добавил задние/межевые ограды соседних участков у въезда
и заполнение его деревянных заборов. Кадры подтвердили более связный двор,
но боковое поле осталось пустым. Заполнение ограничено именованными
оградами Arrival: проба на всех оградах делала зират слишком закрытым.
Проходы и владельцы коллизии не менялись; визуальный P0 остаётся открытым.

Периметр общего рельефа получил боковые водоразделы за x=-40/x=44 и
дальний склон за z=-128. Формулы экспорта и коллизии синхронны;
дальние деревья в этих областях привязаны к высоте общей земли.
В первом захвате устранена пустая линия горизонта, но незаполненные
ближние участки и схематичный лес сохраняют статус `REWORK`.

`AddAuthoredHouse` теперь ставит HouseA на землю по нижней границе видимого
LOD0, устраняя погружение стен дальних домов у въезда. Низкие компоненты
соседних участков в левом виде двора бабая этим не исправлены: следующий
проход должен найти их собственный источник геометрии и позиционирования.

Последующая проверка нашла источник: три соседних mount-а (`WestSideParcel`,
`BabaiYardWestDepthBanyaYardParcel`, `BabaiEastDepthPlasterAnnexParcel`)
оставались на Y=0. Они привязаны к общей земле. Кадры слева и в глубину
двора подтвердили появление стен/окна/крыльца над землёй; конкретное
погружение соседних построек исправлено, общий P0 плотности остаётся открыт.

Кара: три группы деревьев за финальной точкой добавили дальний лесной план.
Прямой вид стал плотнее, правый склон остаётся открытым; текущие силуэты
ещё схематичны. Захват после исправления синхронизации скрытого рендера
прошёл 44/44, что не закрывает визуальный P0.

Актуальное состояние: production `Act1ConnectedWorld` теперь создаёт пять
логических зон в одном persistent root и монтирует `AgentBExteriorWorld` с
пятью authored GLB-kit, terrain/road collision, foliage и rain/fog/light.
Физический walkthrough до клиффхэнгера проходит, но 360° first-person кадры,
near/mid/far review, human wayfinding и отсутствие повторов ещё не приняты.

Историческая формулировка blocker-а: текущий `act1_demo.tscn` технически проходит маршрут, но
`Main.SwitchZone` уничтожает текущий `ZoneHost` и загружает одну benchmark-сцену
за раз. Поэтому три красивых кадра и физический walkthrough не доказывают
цельный мир: при развороте игрок может увидеть пустоту, greybox-границу или
повторяющуюся процедурную корону вместо продолжения Кырлая.

Следующий gate: сохранить общий world/runtime owner, провести ограниченный
full-route root-viewport capture для восьми визуальных зон и проверить руками
направления вперёд/назад/вбок/вверх/вниз, near/mid/far, wayfinding и повторяемость.
Текстуры, package/FPS и benchmark receipt остаются вторичными до этого review.

Точечный 360° pass 2026-08-25 добавил обратные оконно-дверные и timber cues к
authored dwelling facades через существующий presentation-only helper. Это
убирает доказанную пустую заднюю плоскость соседнего `MainStreetWestNeighborFacade`
в Babai-кадре, не меняя collision, interactions или `RuntimeBridge`; свежий
headless receipt подтверждает 36/36 root-viewport кадров и 10/10 waypoint.
Пасс всё ещё не является visual acceptance: road/terrain flatness, near/mid/far
density, Kara night readability, cultural review и human 360° проход остаются
OPEN.

В точечном road-пассе 2026-08-25 скрыты только presentation-меши legacy
`RoadSurface` коннекторов и core outer-bank envelope: видимый мокрый crown,
ruts, puddles и terrain остаются за Agent B, а traversal strips/collision
сохраняются. Headless receipt после каждого варианта подтверждает 36/36 кадров,
10/10 waypoint и отсутствие runtime errors. Это улучшает читаемость мокрой
дороги, но оставшиеся плоские участки, общий near/mid/far density и human visual
review по-прежнему OPEN.

Периферийный authored-parcel pass 2026-08-25 добавил пять дальних фасадных
кластеров из существующего `urman_village_exterior_kit.glb` вокруг въезда,
главной улицы и возвратной дороги. Они остаются presentation-only и не входят
в collision, interactions или `RuntimeBridge`; receipt сохранил 36/36 кадров и
10/10 waypoint. Это закрывает только несколько дальних silhouette gaps:
боковые поля, плотность near/mid/far, повторяемость prefab-семейств и human
360° review всё ещё OPEN. Шесть дальних foliage entries добавлены в тот же
deterministic plan; clearance guard сохраняет дорогу свободной, но authored
canopy и material variation ещё не приняты.

## FAP exterior now has a single presentation owner

Проверенный production-риск был конкретным: `agentb_village_buildings_kit.glb`
содержит собственное семейство `Fap_*`, а connected-world слой отдельно
подключает `urman_fap_clinic_kit.glb` на том же участке. Это давало два
конкурирующих фасада, ограды и входа, даже когда каждый kit по отдельности
выглядел корректно.

Минимальное исправление: `AgentBAct1ExteriorLayer` скрывает `Fap_*` до
генерации архитектурной коллизии и помечает семейство suppression metadata;
`FapClinicAuthoredKitPresentation` остаётся единственным exterior visual
owner; его authored wayfinding board получает один физически привязанный
diegetic label `ФАП`, чтобы suppression не убирал ориентир вместе с дублем.
Интерьер, desk/document targets и `RuntimeBridge` не затронуты.
`Act1DemoLaunchSmokeTest` теперь fail-closed проверяет этот контракт и сам
label. Это закрывает только структурный duplicate-owner риск; forward/back/
side кадры, wayfinding comprehension, локальный медицинский контекст и human
art review всё ещё OPEN.

## Foliage template source is hidden after extraction

Проверенный production-риск был рядом с тем же presentation-layer:
`agentb_foliage_kit.glb` является библиотекой исходных семейств, но до
исправления оставался видимым после создания `AgentB_PlantedFoliage`. Это
оставляло у начала мира плотную доску шаблонов и одновременно показывало
исходные и посаженные варианты.

`AgentBAct1ExteriorLayer` теперь скрывает только source root после сбора
шаблонов, помечает `templateSourceHidden` и сохраняет deterministic copies в
`AgentB_PlantedFoliage`. План дополнен низким contact-scale pass по мокрым
обочинам, дворам, ФАПу, переходу зирата и кромке Кара; он использует
разнообразные authored sedge/fern/grass/moss/branch/stump variants и не входит
в дорожный проход. Smoke проверяет, что source root невидим, planted layer
непуст, обработал все записи плана и не допускает посадку ближе 0,25 м к
road envelope. Это закрывает template-board
contamination и устраняет известный spatial density недобор, но не закрывает
360° повторяемость, authored foliage review, near/mid/far motion readability
или культурную проверку.

## Act I atmosphere now has one routed environment owner

До этой коррекции `AgentBEnvironment` добавлялся в persistent core и оставался
активным вместе с `WorldEnvironment` текущей logical zone. Это создавало
конкурирующие глобальные environment/light owners: внешний Agent B sun мог
складываться с локальным directional light, а интерьер не гарантировал свой
тёплый фон.

`Act1ConnectedWorld` теперь маршрутизирует ровно одного владельца: на
`village_day`, `zirat_road` и `kara_urman_night` активен Agent B exterior
environment/sun, а локальные directional suns выключены; в `house_old_pc` и
`fap_clinic` Agent B environment/sun выключены и остаётся локальная среда
интерьера; дождь Agent B также выключается внутри. Smoke проверяет один
активный `WorldEnvironment` и rain-emitter в обоих режимах.
Для ночной кромки добавлены три слабых холодных `OmniLight3D` cue в том же
`AgentBExteriorWorld`: они подсвечивают порог, корневой кластер и жестовую
ветвь только при `kara_urman_night`, без теней, коллизии, навигации или
интеракций. Dedicated smoke проверяет их runtime-включение через test-owned
camera probe; это только value-separation correction. Эта коррекция закрывает
ownership-конфликт, но не является визуальной приемкой света, ночной
читаемости, дождя, тёплого окна или art lock.

## Standalone Agent B world variant is retired

Production теперь имеет один исполняемый внешний владелец:
`Act1ConnectedWorld/Act1CoreWorldGreybox/AgentBExteriorWorld`, помеченный
`variantStatus=production-canonical`. Старый `AgentBAct1World` и его
windowed/probe harness удалены; исторический capture-report оставлен только
для provenance. Это снижает риск случайно проверять не тот мир и не меняет
маршрут, collision, interaction, `RuntimeBridge` или save ownership. Smoke
проверяет canonical marker, а визуальный 360° gate остаётся OPEN.

## Capture harness liveness is now bounded

Предыдущий full-route capture мог оставаться живым без PNG до ручного убийства:
test-only код напрямую ждал `RenderingServer.FramePostDraw`, а shell-wrapper не
имел watchdog. В `Act1FullRouteCoreWorldCapture` ожидание заменено коротким
settle-window по `ProcessFrame`; `eng/capture-act1-full-route-core-world.sh`
дополнительно ограничивает production capture таймаутом
`URMAN_CAPTURE_TIMEOUT_SECONDS` (по умолчанию 300 секунд). Это только QA
инфраструктура и не является доказательством визуальной приемки; сам capture и
human review остаются обязательным следующим gate.

## MVP-прототипы не связаны в одну игру

Слабое место: старый route navigation уже не соответствует принятому first-person 3D target, а внешний minute-to-minute detective loop всё ещё не доказан. Базовый разрыв общей world presentation закрыт частично: physical full-game documents теперь открываются в Godot и могут записываться в тот же journal, что и old PC, но vocabulary re-read, dialogue topics и полноценный плейтест ещё требуют production-прохода.

Путь решения: сохранить shared knowledge source of truth и довести цепочку `old PC/physical document -> journal -> dialogue reaction -> vocabulary re-read -> pressure -> 3D world/cliffhanger`. Сейчас Godot smoke доказывает первые два звена для четырёх full-game документов; следующим шагом остаются re-read, authored dialogue consequences и external first-time playtest. Старый route prototype использовать только как provenance и reference.

## Визуальный пакет опережает runtime

Слабое место: `public/assets/urman_mvp_remaining/` визуально покрывает большую часть старого 2D-формата, но не является готовым набором для ходибельного 3D. Runtime использует HTML-заглушки, 3D-placeholder мечети, старую рамку ПК и частичные handoff previews.

Путь решения: разделить пакет на reusable references/UI/textures и то, что нужно заменить 3D-моделями. До массовой генерации собрать один in-engine environment kit и один hero prop; ink-wash документы, портреты и old PC UI использовать там, где 2D остаётся частью принятого формата.

## Acts 2–5 имеют presentation pass, но не production lock

Слабое место: `FullGameZoneDressing` теперь даёт каждой зоне реальный модульный визуальный контекст, `GeneratedModularKitDressing` подключает environment `.glb` с проверяемыми LOD-диапазонами и provisional layer-2 proxy colliders, а три style benchmark кадра дополнительно упражняют project-original `HouseA_`/`PineA_` через тот же адаптер. Последний Blender pass добавил HouseA foundation/door/frame/window-trim/porch/step/eave cues и довёл environment kit до 32 детерминированных LOD1 mesh без расширения gameplay-collision owner. `FullGameNpcDressing` размещает project-original character `.glb` с девятью prefix-вариантами, лицевыми landmarks, layered clothing, детерминированными LOD и Blender-authored `Idle`/`Tension` armatures. Godot `AnimationPlayer` импортирует и переключает оба клипа presentation-only; четырёхстемный `AmbientAudioDirector` и `AmbientAudioSmokeTest` добавляют только технический zone-routing/import contract; семь Godot capture-кадров, три style capture и отдельный `CollisionQaSmokeTest` подтверждают wiring, queryable floors всех 12 зон и изоляцию layer-2 proxy colliders. Окружение и персонажи всё ещё production-progress: полный authored module family, финальная expression-постановка, authored ambience/voice/mix, mesh-level collision acceptance, release-hardware LOD/performance evidence и cultural presentation review остаются открыты.

Путь решения: заменять dressing по актам на Blender-ассеты только после beat-sheet/content lock; каждый акт принимать отдельной проходимой сборкой с frame-time, collision, audio-caption и культурной проверкой. Текущий smoke-контракт закрывает только базовую геометрию пола и изоляцию proxy; следующий collision pass должен проверить реальные authored mesh-colliders перед заменой procedural colliders.

Bounded epilogue progress, 2026-08-14: канонический Act 5 epilogue теперь
получает тот же project-original `HouseA_` модуль, что и Act 2, через отдельный
`act5-epilogue-house` presentation variant. Это улучшает проверяемую композицию
седьмым full-game capture и не добавляет второй gameplay-owner. Риск не снят:
один HouseA не заменяет полное семейство деревенских модулей, authored mesh
collision, hero-пропорции или культурную проверку.

## Локальный performance baseline не заменяет release hardware

Слабое место: три 1080p benchmark-сцены и отдельный full-entrypoint probe Act 1
измерены на Apple M4 Pro и держат внутренний 30 FPS low-preset floor, но это
ещё не доказательство 60 FPS на среднем Windows-PC, 30 FPS на Apple M1 или
стабильности экспортированных сборок. Локальный probe полного
`act1_demo.tscn` также не воспроизводит жалобу «около 1 FPS». Исправлена
несогласованность старта: заявленный `medium` теперь реально применяется
до первого кадра (0.90 scale / 2× MSAA); release-hardware reproduction всё ещё
нужен.

Путь решения: повторить тот же benchmark на Apple M1 и реальном Windows-PC, сохранить raw frame-time и проверить low/medium/high пресеты до финального cutover.

Для жалобы на единичный FPS добавлен явный диагностический путь
`eng/run-act1-demo-safe.sh`: Mobile renderer, 0.75 3D scale, без MSAA и без
трёх triplanar texture reads в painterly fragment shader. Он не меняет зоны,
сюжет, сохранения или collision owners. Если safe launch исправляет FPS, нужно
отдельно принять renderer/material budget для целевого GPU; не выдавать этот
локальный rescue path за закрытие M1/Windows performance gate.

## Плейтест полного MVP пока преждевременен

Слабое место: полный путь «приезд → клиффхэнгер» ещё нельзя честно тестировать как цельную главу. Можно тестировать old PC search loop и narrative comprehension, а старую route orientation — только как исторический прототип, не как final external MVP experience.

Путь решения: использовать Godot first-person matrix из `playtest_plan.md`: сначала movement/input/performance smoke, old PC, language and narrative tests. Full first-time MVP playtest возможен только после 3D-house scene, shared journal/dialogue graph и final cliffhanger path.

## Gameplay core loop не доказан

Слабое место: detective loop принят как главное действие игрока, но пока не доказано прототипом, что это интересно каждую минуту.

Путь решения: сделать 15-минутный playable prototype с одним расследовательским циклом: противоречие → clue → NPC / архив → новый ключ → новая реакция NPC → татарский unlock → мистический след.

## Формат игры выбран, но не доказан

Слабое место: ходибельный 3D от первого лица принят как направление, но ещё не доказано, что маленькая команда сможет удержать качество окружения, атмосферу и темп без расползания локаций.

Путь решения: сделать 15-минутный greybox: дом → участок улицы → кладбище или архив/ПК → Ринат → audio-first след. Проверить масштаб, управление, интерактивность, скорость сборки модульного окружения и стоимость одной законченной минуты. Для текущей демо-сборки непрерывность ambience усилена двухплеерным 0.65-секундным crossfade; authored voice, полевой ambience mix и культурное listening review остаются отдельными gates.

## Ассеты не зафиксированы как минимум

Слабое место: список персонажей, локаций, документов и UI быстро разрастается.

Путь решения: утвердить MVP asset floor: минимум портретов, минимум локаций, минимум документов, один old PC UI, один journal UI, один dialogue key UI.

## Визуальный стиль принят, но не доказан production-тестом

Слабое место: Painterly Low-Poly 3D принят, но ещё не доказано, что его производственные бюджеты дают нужное качество в движении. Красивый одиночный concept render может скрыть чрезмерную стоимость ассетов или нестабильный frame-time.

Путь решения: собрать три ходибельных Godot benchmark scenes — улица, дом/ПК и ночная кромка леса. Проверить силуэты, материалы, свет, туман, поворот камеры, collision, производственную повторяемость, frame-time и отсутствие generic horror. Шесть versioned v2 texture candidates (`*_v2_albedo.png`), шесть focused v3 siblings (`*_v3_albedo.png`), focused v4 earth/wood pair (`*_v4_albedo.png`) и focused v5 earth/wood rework (`*_v5_albedo.png`) проходят формат/seam gate; все остаются test-only preview ownership и не включены глобально. Отдельный v3↔v5 Metal/Forward+ A/B даёт 9 листов и 120/120 изолированных contact samples; отдельный v5 motion sweep даёт 27 near/mid/far × FOV 65°/75°/90° ячеек на каждой обязательной сцене. Earth relief-only/relief+wetness и wood facade/house/forest comparison остаются OPEN. Отдельные camera sweeps подтверждают spatial projection; temporal sweep добавляет 216 кадров для FOV 65°/75°/90° с head bob и reduced motion. Это техническое evidence, не внешний comfort review. Независимый аудит держит v4 в HOLD/REWORK; v5 — production OPEN: earth всё ещё требует проверки pebble repetition/wetness и отсутствия двойного relief, wood — проверки ориентации и shared owner/scale; greybox geometry, wet roughness и fog/light calibration всё ещё не приняты.

### Лужи имеют отдельный contact/wetness gate

Слабое место: текущие low-poly puddle silhouettes визуально существуют, а
первый общий physics-world аудит давал ложные совпадения с relief другой сцены.
После изоляции сцен и bounded-коррекции `PuddleFar`/`BoundaryWetPatch` все
15 патчей принадлежат ожидаемому relief и укладываются в ±0.010 м. Снижение
roughness до «мокрого» значения всё ещё может скрыть реальные проблемы
specular/clip/z-fight, поэтому визуальный material gate остаётся отдельным.

Путь решения: использовать отдельный test-only Godot harness, который поочерёдно
инстанцирует benchmark-сцены, требует instance-owner collider, клонирует только
presentation `ShaderMaterial` в памяти, проверяет 5 кластеров/15 патчей и
отсутствие изменения исходного `roughness=0.90`. Contact gate теперь PASS;
wetness roughness/specular остаётся OPEN и не становится runtime owner.

Матрица v2 теперь измеряет raw production origins до любых test-only действий.
Отдельный узкий geometry slice посадил только восемь превышавших 5 мм патчей
на их собственные relief-cells; все 15 origin samples проходят ±0.005 м с
exact owner и candidate-local alignment больше не требуется. Матрица всё ещё
сравнивает `roughness_value` в rows `0.40/0.50/0.60` (45 клонов, 9 кадров).
По кадрам wetness остаётся слабой/плоской; не выбирать roughness и не менять
runtime до отдельного visual review.

## Полная игра шире подробно написанного канона — narrative lock закрыт

Слабое место: релиз на 6–8 часов и трагический финал приняты, но production мог начать диктовать сюжет до подробной фиксации актов 2–5.

Решение: 2026-08-11 принят `narrative_lock_acts_2_5.md` с beat sheet, clue graph, зонами, NPC, документами, language keys, world variants и threat beats для каждого акта. Это снимает narrative ambiguity для производства, но не закрывает внешний татарский/культурный review, art/audio, mesh-collision или release gates. Greybox и final art по-прежнему принимаются раздельно.

## Татарский язык требует точной механики

Слабое место: «изучение татарского» легко станет декоративным словарём или скучным уроком.

Путь решения: первый language prototype должен менять расследование. Игрок выучил слово → старый документ стал понятнее → NPC дал другую реакцию.

## MVP может расползтись

Слабое место: полный лор включает 1552, Тукая, Баранова, Марата, пакт, Алсу, Тимура, несколько существ и концовки.

Путь решения: MVP держать вокруг Марата. 1552, Тукай и Баранов — только короткие намёки или архивные teasers.

## Сюжет большой, но вертикальный срез должен быть узким

Слабое место: хочется раскрыть весь мир сразу.

Путь решения: MVP раскрывает только смену рамки: «деревня лжёт» → «деревня живёт в системе сосуществования». Не раскрывать правила пакта полностью.

## Риск спойлернуть финальное «Не отвечай»

Слабое место: если Гөлсинә, Ринат, Татарвики или journal заранее прямо объяснят правило «не отвечать голосу Марата у кромки», финальный клиффхэнгер станет не откровением, а выполнением инструкции.

Путь решения: до финала использовать только тревожную гипотезу `clue_voice_answer_is_dangerous_hint`: голос / ответ / граница опасно связаны, но правило не подтверждено. Ключ `clue_do_not_answer_rule` становится confirmed только после действия Рината в сцене у кромки Кара-Урмана. Детальный lock: `chapter1_mvp_campaign.md`.

## Риск перегрузить игрока лором

Слабое место: архивы, термины, существа и история могут задушить темп.

Путь решения: каждый документ должен иметь gameplay use. Документ без clue, ключа или эмоционального удара не входит в MVP.

## Старый ПК может съесть MVP

Слабое место: ПК бабая теперь сюжетно важный документальный хаб, и его легко разрастить до самостоятельной тьюринг-полной ОС раньше, чем доказан базовый detective loop.

Путь решения: для MVP делать не «компьютер внутри игры», а плотный old PC / archive loop: поиск, документы, «Татарвики», сохранённое сообщение, gated/corrupted fragment и re-read после vocabulary/search unlock. Прототип 2026-05-18 уже покрывает этот loop на 10 content files. Следующий риск не в самой оболочке, а в интеграции улик ПК с journal/dialogue graph. Полную программируемость держать как post-MVP ambition. Техническая метадата не должна быть главным clue; использовать документальные отметки и противоречия.

## Слишком много персонажей

Слабое место: список NPC широкий: семья, Алсу, Тимур, Ринат, Наиля, Разиля, Гаяз, Ленар, лесник, Самат, Мурат, бабушки, абыйлар, сельсовет.

Путь решения: MVP делит персонажей на active NPC и ambient/document NPC. Не всем нужны портреты и сцены.

## Хоррор, детектив и education могут конфликтовать

Слабое место: если татарский mechanic будет слишком учебным, он сломает хоррор; если хоррор будет слишком прямым, он сломает детектив.

Путь решения: язык должен быть инструментом расследования, а хоррор — следствием понимания, а не отдельным режимом.

## Религиозный слой требует аккуратности

Слабое место: Тимур хәзрәт и исламская рамка легко могут стать карикатурой.

Путь решения: писать Тимура как цельного человека, а не функцию экспозиции; консультироваться; не противопоставлять ислам и татарскую культуру примитивно.

## Непонятно, насколько игра интерфейсная

Слабое место: первое лицо усиливает деревню как место, но баланс всё ещё может съехать: слишком много документов остановит исследование, слишком много пустой ходьбы раздует производство и размоет детективный цикл.

Путь решения: компактные ходибельные зоны + глубокие интерфейсные расследования. Интерфейсы как реальные предметы использовать выборочно: старый ПК, бумага, фото, журнал.

## Риск укачивания и пустого 3D

Слабое место: медленное первое лицо легко испортить сильным head bob, инерцией, узким FOV или длинными пустыми маршрутами.

Путь решения: минимальный head bob, настраиваемый FOV, предсказуемая скорость, короткие насыщенные маршруты и частые смысловые ориентиры. Проверить управление на сером блок-ауте до производства красивых ассетов.

## Конфликт имён Марата

Слабое место: центральный Марат — друг детства, но ранний список содержит Марата-лесника.

Путь решения: переименовать лесника или объединить функцию с другим персонажем до сценарного прототипа.

## Бабай как действующий глава в 80 лет

Слабое место: может быть неправдоподобно, если он формально всё ещё глава администрации в 2026.

Путь решения: Мансур уже зафиксирован как бабай и фактический держатель семейного доступа к ПК. Открытый вопрос теперь не имя бабая, а статус Гаяза: нужен ли он как отдельный официальный глава/ambient NPC или его функцию нужно удалить из MVP.

## Линия Тукая культурно рискованна

Слабое место: прямое утверждение, что деревня / существа повлияли на смерть Тукая, может звучать грубо.

Путь решения: держать как спорный архивный документ, локальную интерпретацию или фан-теорию внутри мира, не как подтверждённый факт.

## Runtime несколько источников истины до cutover — закрыто 2026-07-18

Бывший риск: `GameState`, `SceneManager`, TypeScript arrays, custom old-PC parser, localStorage owners и `window.URMAN` одновременно несли content, progression, navigation и persistence.

Решение: production теперь использует compiled content pack, `RuntimeKernel`, capability providers и named persistence gateway; old owners/validators удалены, а static gate проверяет 88 production source files. Legacy MainMap/Content Lab изолированы как dev-only. Permanent fallback и второй owner не допускаются.

Исторический риск `GameSnapshotV2` не хранить текущую presentation-position закрыт в target: `SaveGameV3` теперь хранит текущую зону, spawn point и portable player transform. Восстановление Godot-зоны и settings проверено интеграционным smoke; повреждённый primary recovery покрыт atomic-store tests.

## Accessibility contract — технически закрыт, пользовательская проверка открыта

Слабое место: доступность легко принять за наличие нескольких флажков, хотя качество зависит от того, действительно ли reduced motion, контраст, размер текста, субтитры и текстовые описания действуют на всех presentation owners и не расходятся с сохранением.

Путь решения: `AccessibilitySettingsSnapshot` живёт внутри unreleased `SaveGameV3`; `AccessibilityPresentation` применяет его через одну Godot target-group к first-person head bob, dialogue/journal/document/old-PC/settings panels и `AudioCueUi`. C# codec и Godot smoke проверяют roundtrip, диапазон text scale и reduced-motion override. Остались внешние проверки укачивания, читаемости на 1080p/M1/Windows и культурной корректности non-audio descriptions.

## Current runtime спойлерит «Не отвечай» раньше accepted финала

Слабое место: текущие `dialogue_data.ts`, `knowledge_keys.ts` and `quests.ts` могут unlock/require `clue_do_not_answer_rule` до действия Рината, хотя accepted Chapter 1 разрешает только hypothesis `clue_voice_answer_is_dangerous_hint` до cliffhanger.

Путь решения: MM-50 не копирует текущие unlock edges. Narrative invariant test должен доказывать отсутствие/неподтверждённость `clue_do_not_answer_rule` до финального outcome и confirmation только после реплики Рината.

## Character IDs и records не замкнуты

Слабое место: legacy array использует `babay`/`abi`, другие data use `char_babay`/`char_abi`/`char_gulsina`, а обязательные Айдар, Марат, Ринат, Наиля and Тимур records отсутствуют. `rushania` нельзя молча принять за Наилю.

Путь решения: применить one-time mapping MM-10 без runtime aliases; MM-50 создаёт пять missing records из accepted authority, а ambient legacy characters подключает только явно через campaign manifest.

## V6 texture candidates remain evidence, not acceptance

Слабое место: v6 earth и wood выглядят спокойнее на swatch, но в текущем
greybox/освещении разница земли с v5 едва заметна; это может означать как
правильную подчинённость фактуры геометрии, так и отсутствие полезного
материального breakup.

Путь решения: держать v6 test-only, проверять earth relief-only против
relief+wetness и wood на фасаде/заборе/мебели/end-grain при нейтральном и
тёплом свете. A/B и 27-cell motion receipts уже технически чистые, но
temporal comfort, 20–30 m repetition, geometry/fog/light, cultural review и
art lock остаются открыты.

## Pinned Blender verifier зависит от host-контекста

Слабое место: в managed/sandboxed запуске pinned Blender 4.5.12 LTS завершается `SIGSEGV (139)` внутри
`gpu::supports_barycentric_whitelist -> MTLBackend::metal_is_supported` ещё до
загрузки `.blend` и verifier script. Это воспроизводится для environment,
character и даже `--factory-startup --python-expr`; проблема не доказывает
дефект исходных мешей или текстур. На elevated real-driver запуске оба
верификатора сейчас проходят, поэтому статус зависит от host и не должен
маскироваться одним зелёным/красным результатом.

Путь решения: всегда сначала запускать `eng/verify-asset-registry.sh`, который
проверяет 36 derived и 10 явных source hashes независимо от Blender; затем
классифицировать sandbox SIGSEGV как `HOST_TOOLCHAIN_BLOCKED` и повторить
environment/character verification на release host или после обновления
pinned Blender с полным regression pass. Доказательства и точные команды:
`art/blender_asset_verifier_host_blocker.md`.

## Lighting/fog calibration — технически проверено, визуально открыто

Слабое место: даже после bounded ambient/fog/key-light calibration в
`StyleBenchmarkZone.cs` day street остаётся серо-оливковым greybox, дом слишком
тёплый/пустой, а Kara-Urman сжимается туманом в синюю массу. Это нельзя
исправить дальнейшим усилением albedo: нужны authored geometry, material
owners и культурная level-art проверка.

Путь решения: считать новые static/27-cell/216-sample кадры production
candidate evidence, но держать GODOT-003 и art lock OPEN; следующий дорогой
срез — authored village/forest/hero-PC geometry и owner-specific material
orientation, а не ещё одна универсальная texture версия.

## Imported GLB collision descendants require an adapter boundary

Слабое место: project-original environment GLB содержит собственные
`StaticBody3D`/`CollisionShape3D` descendants. Если оставить их внутри
presentation instance, они добавляют неожиданные layer-1 physics owners рядом
с authored walkable floor и controlled layer-2 proxy.

Путь решения: `GeneratedModularKitDressing` синхронно удаляет эти imported
descendants до `AddChild`. Обычный `Attach` затем создаёт ровно один
контролируемый layer-2 proxy, а `AttachPresentationOnly` — ноль physics nodes;
`SceneSmokeTest` считает фактическое дерево, а не только metadata. Raw GLB и
его registry не меняются. Это закрывает ownership regression, но не визуальный
арт-гейт: PineA family всё ещё повторяется, а release-host/motion/cultural
review остаются OPEN.

## Temporal evidence must preserve the scene it measures

Слабое место: ранняя версия temporal harness освобождала целые
`CollisionObject3D`. В benchmark-сценах MeshInstance3D часто является его
потомком, поэтому формально «непустые» кадры были разреженной копией сцены и не
могли подтверждать комфорт или читаемость.

Путь решения: текущий render-only clone сохраняет визуальных потомков,
обнуляет collision layer/mask и освобождает только `CollisionShape3D` после
снимка исходного дерева. Receipt на 2026-09-14 фиксирует 2072/1723/1065 visual
meshes, 288/28/654 удалённых shapes и ноль активных physics-query owners (числа
2026-08-14 — 806/109/826 и 286/22/695 — относились к базовой сцене до зимнего
дрессинга). Старые кадры сохранены как superseded evidence; 216 новых образцов —
лишь техническое доказательство. Внешний comfort review и art lock остаются OPEN.

## Lighting/fog calibration is an integrated bounded baseline, not an art lock

Слабое место: даже после интеграции bounded calibration в Chapter 1
`StyleBenchmarkZone` day street остаётся серо-оливковым greybox, дом тёплым и
пустым, а Kara-Urman всё ещё требует authored canopy/ground geometry. Усиление
текстур не решает иерархию света и authored geometry.

Путь решения: значения ambient/fog/key/fill были сначала проверены через
`StyleCalibrationCandidateCapture`, затем перенесены только в
`StyleBenchmarkZone.cs` как презентационный baseline для демо первого акта.
Production material owners, shader, GLB, collision, saves и narrative state не
меняются. Свежий Metal/Forward+ capture технически PASS, но visual/cultural/art
acceptance, near/mid/far repetition, temporal comfort и полноценная authored
geometry остаются OPEN.

## Owner-specific wood/cloth calibration — технически подключено, визуально открыто

Слабое место: один world-scale для imported wood surfaces превращал фасад,
забор, мебель и кору в одинаковую регулярную полосу; character kit запрашивал
`cloth`, которого не было среди texture descriptors и потому терял albedo.

Путь решения: `PainterlyMaterialLibrary` теперь разделяет только semantic
owners (`wood_facade`, `wood_fence`, `wood_furniture`, `wood_bark`) с тем же
source albedo и разными world scales, а `cloth` явно указывает на bounded
`old_fabric_v2` descriptor. Shader, geometry, collision, saves и narrative
state не меняются. Следующие gates — Godot near/mid/far traversal, end-grain
orientation, 20–30 m repetition, clothing readability, cultural review и art
lock; это не финальное утверждение качества текстур.

## Authored yard/threshold modules remain presentation candidates

Слабое место: `WellA_`, `WoodpileA_` и `GateA_` добавляют культурно читаемые
силуэты, но пока являются только декоративными импортированными мешами. У них
нет отдельной gameplay collision authority; GateA также может визуально
перекрыть marker выбора или проявить повтор при дальнем проходе.

Путь решения: держать zone floor/path и существующие layer-2 proxy colliders
единственными физическими владельцами, проверять GateA в PineA-композиции, а
well/woodpile — в HouseA-композиции. Статические Act 2/Act 5 captures и
runtime-backed marker-clearance smoke уже записаны; остаются near/mid/far
screen-space traversal, 20–30 m repetition review, authored family coverage и
release-host performance. Не подключать GateA к HouseA-эпилогу и не лечить
возможные floating/occlusion проблемы новой текстурой.

## Puddle silhouette candidate is retired: the shipped wet read is the authored kit

Слабое место: исходный низкий `CylinderMesh` читается как отдельная толстая
пластина с вертикальным бортом. Новый in-memory фасетный `ArrayMesh` убирает
этот борт и сохраняет exact-owner контакт ≤5 мм, но при общей source
roughness `0.90` почти исчезает в обзорном кадре; это не доказывает ни сырость,
ни читаемость в движении.

Путь решения (закрыто 2026-09-14, измерено): этот выбор больше не нужно делать —
низкополигональная цилиндрическая лужа в поставку не попадает. Проверено:
`PainterlyEnvironmentDetails.AddPuddleCluster` вызывается только из
`StyleBenchmarkZone` (шесть кластеров), каждый помечен `puddleGeometry =
low-poly-overlap-proxy`, а `Act1ConnectedWorld.ApplyLogicalZonePresentationSuppressions`
скрывает все узлы с этой метой в `village_day`, `zirat_road` и `kara_urman_night` —
то есть ровно там, где они есть. Маркер `OPEN-roughness-review`
(`PainterlyEnvironmentDetails.cs:65`) относится к скрытой геометрии.

В поставке wet читает авторский kitset `urman_wet_village_road_kit.glb`: 15
именованных компонентов (`RoadRuts_PuddleNear/Far`, `RoadsideDitch_L/R`,
`RoadCrown_SunkenWet`, `MuddyShoulder_L/R`, ...), смонтированных на Arrival,
MainStreet, ConnectiveStreetReturn и ZiratMemoryField в
`BuildAct1WetVillageRoadKit` и перекрашенных в `RebindWetVillageRoadMaterials`
в явные painterly-отклики: `water` 0.38/0.45/0.98, `wet_ground` 0.56/0.32/0.94,
`earth` 0.78/0.16/0.70 (по `surfaceGrade` в `PainterlyMaterialLibrary`).
Поверх того же коридора лежит `snow_road`/`snow_ground` (0.68/0.90, wet grade 0),
поэтому зимой сырость читается как тёмный блик в колее, влажный излом обочины и
линия канавы — видно на `art/style_frames/godot_day_street_1080p.png` и в кадрах
маршрута.

Что осталось человеческим: убедительна ли эта сырость на высоте игрока и в
движении. Что закрыто: выбор mesh'а лужи и вопрос «активировать ли candidate в
`PainterlyEnvironmentDetails`» — активировать нечего, механизм выведен из
поставки. Contact-sheet'ы кандидатов остаются валидными диагностиками
выведенного прокси.

## GLB family contract does not replace mesh-collision review

Слабое место: даже project-original `.glb` может пройти registry hash и всё
же получить лишний import-helper или визуальный mesh вне опубликованных LOD
пар. Prefix-only выборка уже проявила такой случай: importer оставил у
`HouseA` не-LOD дочерний render helper, который раньше становился видимым.

Путь решения: `GeneratedModularKitContractSmokeTest` instantiates all nine
families presentation-only and fail-closes on exact `_`-terminated prefixes and
pairs `12/12, 6/6, 1/1, 2/2, 5/5, 6/6, 7/7, 4/4, 4/4`, one-to-one LOD names,
visibility ranges, exact semantic `ShaderMaterial` owner sets, imported-physics
removal metadata and any physics descendant. `GeneratedModularKitDressing` now accepts
only explicit `_LOD0`/`_LOD1` render meshes for published visibility, so the
shortened HouseA/OldPc helpers stay hidden. The current superseding modular
source is a 49-mesh contract with `OldPc_ 8/8`; the exact DriveSlot/LabelPlate
LOD pairs are asserted in Blender and Godot. FenceA is explicitly
presentation-only in the asset registry because the GLB has no `FenceA-col`.
This proves import/presentation hygiene, not player-facing mesh collision:
authored collision, Blender release-host verification, traversal, visual review
and art lock remain OPEN.

## OldPc hero details are a candidate, not a finished hero prop

Слабое место: два новых authored детали делают силуэт старого ПК понятнее на
интеракционной дистанции, но общий Soviet frame остаётся дальним, а shared
material и low-poly формы могут потерять слот/табличку при движении или на
средней дистанции.

Путь решения: `OldPcHeroDetailCapture` проверяет четыре exact LOD-имени и даёт
два Metal/Forward+ close кадра без изменения proxy/physics/runtime. Новый
`OldPcHeroDetailMotionSweep` добавляет 18 near/mid × FOV 65/75/90 × head-bob
кадров в одном isolated world; все четыре детали и non-empty readback gate
проходят. Это закрывает только техническую проекцию и устойчивость захвата:
нужны стандартный Soviet recapture, observed traversal/comfort,
repetition review, cultural/level-art review и M1/Windows performance. Не
объявлять art lock и не включать отдельный shader/texture owner только ради
усиления этих деталей.

## Act 1 demo is the current product gate, not the full-game gate

Слабое место: после большого Godot migration backlog легко принять наличие
12 full-game зон и 46-beat campaign за обязательный объём ближайшей сборки.
Это размывает проверку атмосферы, темпа и понятности первого часа.

Путь решения: текущий launch path зафиксирован на
`game/scenes/act1_demo.tscn`. Приёмка демо проверяет пять компактных зон,
16 авторских Chapter 1 beats, общий state old PC/journal/dialogue/vocabulary,
первое лицо и финальный fade-to-black «НЕ ОТВЕЧАЙ». Акты 2–5, полный
6–8-часовой playthrough, release-host gates и web retirement — deferred scope,
а не незаметно урезанная или уже пройденная часть демо. Открытые demo gates:
авторский voice/ambience mix, культурный review, observed first-time playtest,
M1/Windows performance, near/mid/far readability и art lock.

## Act 1 day-street props are presentation candidates, not collision owners

Слабое место: дневная улица получила project-original `WellA_` и `WoodpileA_`
вместо двух самых очевидных процедурных greybox-пропов, но это пока одна
вариация каждого семейства. Дальний road dressing, повторяемость заборов и
деревьев, а также культурная правдоподобность всей улицы ещё не проверены
наблюдаемым прохождением.

Путь решения: оставлять `AttachPresentationOnly` единственным visual owner;
скрытые helper-boxes зоны и layer-1 дороги остаются физическими владельцами.
Сценовые smoke-проверки должны продолжать требовать exact
`WellA_project_original|WoodpileA_project_original` metadata и ноль импортированных
physics descendants. Следующий осмысленный шаг — near/mid/far проход демо и
level-art review, а не активация новой текстуры или добавление collision к
декоративным модулям.

Diegetic wayfinding pass, 2026-08-14: existing signpost now carries a muted
`ФАП` label on the board and is covered by scene/style smoke. This is a useful
near/mid landmark, but the 27-cell sweep is still a technical projection; it
does not prove that a first-time player notices the sign, understands the
route, or finds the FAP without help. Keep observed wayfinding and cultural
review open, and do not replace world landmarks with floating quest markers.

## Act 1 physical evidence corridor is technical proof, not observed playtest

The dedicated corridor smoke now proves the real first-person handoff from the
arrival door through old PC, FAP documents, Rinat dialogue, saved message,
Татарвики reread, edge sketch, zirat and the Kara-Urman cliffhanger. Evidence
targets open the shared `DocumentUi`; the Rinat interaction commits the same
runtime `alerted` state used by the authored route. This removes the previous
silent scene-only evidence gap in the internal Godot path.

The route still has human-facing risks: the Rinat cue is an empty chair/coat/
radio presentation aid rather than a final authored character, the player must
discover the evidence targets without test teleporting, and the current smoke
does not measure pacing, controller comfort, document comprehension or cultural
read. Keep observed first-time playthrough, authored voice, language/cultural
review, release-host performance and art lock OPEN.

The new bounded physical walkthrough now drives the same route through
`CharacterBody3D.MoveAndSlide` for about 93 m and reaches the Kara-Urman
cliffhanger without writing a player transform. It closes the previous
physics/spawn regression blind spot, but it is still deterministic QA: it does
not prove that a first-time player understands the FAP landmark, reads the
documents, tolerates the camera motion or accepts the atmosphere.

## Packaged Act 1 performance is now directly measurable

`Act1DemoRoot` exposes a diagnostic-only `--urman-perf-probe` path because the
exported Godot binary cannot accept a replacement scene path. The new
`./eng/benchmark-act1-demo-package.sh` wrapper measures the published macOS ZIP
itself and supports `--urman-safe-mode`; both fresh package runs are around
120 FPS in the real Metal window on the local M4 Pro (medium Forward+, safe
Forward Mobile). This removes the editor-vs-package evidence gap,
but does not close M1/Windows performance, Windows host execution or the user
reported hardware-specific 1 FPS until that machine is reproduced.

## Interaction availability no longer polls the kernel every frame

Слабое место: старый presentation path вызывал полную сериализацию
`RuntimeKernel.SelectState()` из каждого `InteractionTarget._Process`, хотя
нарративное состояние меняется только после команд. Для слабого CPU это было
лишней работой в render/game loop и могло усиливать аппаратную просадку FPS.

Путь решения: `RuntimeBridge` теперь ставит coalesced main-thread notification
после committed state/zone changes, а `InteractionTarget` подписывается на неё,
обновляя collision layer и видимость только при изменении состояния. Прямой
kernel owner, команды, сохранения и narrative IDs не менялись. Source-tree
реальный probe после изменения: 119.97 FPS Forward+ medium и 119.99 FPS
Forward Mobile low на M4 Pro. Это закрывает только лишний polling-риск; 1 FPS
на пользовательском OS/GPU всё ещё требует target-host замера.

После этого тем же уведомлением переведён финальный detector `Act1DemoRoot`:
он больше не сериализует состояние каждый кадр ради проверки клиффхэнгера, а
проверяет правило только после изменения runtime-state и оставляет в кадре
только короткий таймер затемнения. Это уменьшает ещё один CPU-усилитель, но не
является доказательством производительности на машине пользователя.

## Act 1 OldPc is now an imported presentation candidate

Слабое место: процедурный CRT в доме был читаемым greybox-заменителем, но не
соответствовал уже проверенному project-original `OldPc_` modular kit и не
давал убедительного сюжетного якоря для первого лица.

Путь решения: дом акта 1 теперь подключает `GeneratedOldPcAct1` через
`AttachPresentationOnly`, удаляя дублирующие procedural CRT/keyboard/tower
pieces. `SceneSmokeTest` и `StyleFrameCapture` требуют exact 8/8 LOD,
положительное удаление импортированных physics nodes, ноль physics descendants
у модуля и сохранённый layer-1 `OldPc` interaction target. Это bounded
presentation progress, а не art lock: близкая читаемость, motion/comfort,
наблюдаемое прохождение, культурная проверка и target-host FPS остаются OPEN.

## Kara-Urman night frame is readable but still not art-locked

The previous night calibration compressed the path and tree trunks into a
single blue-black mass in the fixed first-person frame. The demo benchmark now
uses a small cold ambient/fog adjustment (`0.64` ambient, `0.0034` fog density,
`0.075` height density, `1.10` MoonFill) that preserves the restrained night
mood while restoring a value floor for the walkable path. The current Metal
capture and full Godot smoke pass are clean; this is still only a benchmark
presentation change. Human motion/wayfinding review, forest-family geometry,
cultural review and release-host performance remain open, and the values are
not a global full-game lighting owner.

## Act 1 slow-startup guard is bounded, not a platform-performance pass

The reported single-digit-FPS path now has a session-only presentation rescue:
after a short warm-up the entrypoint requires several consistently slow frames
and switches only the existing low material/render-scale profile. It does not
change zones, kernel state, narrative data or SaveGameV3. `--print-fps
--no-auto-performance-fallback` prints live FPS/frame-time/preset/scale/MSAA
for target-host diagnosis. The fresh package remains healthy on the local M4
Pro (`120.04 FPS` medium Forward+, `119.86 FPS` safe Mobile/low), so Windows,
M1-class hardware, editor-vs-package launch and any remaining ~1 FPS report
stay open until reproduced with the diagnostic command.

## Direct Godot launch can be mistaken for an FPS failure

Слабое место: запуск `.tools/godot/Godot_mono.app` напрямую из shell без
`eng/dotnet-env.sh` не является валидным тестом демки. На чистом окружении
Godot воспроизводимо сообщает об отсутствующих `hostfxr/coreclr` зависимостях;
это launch-path error, который нельзя смешивать с измерением FPS.

Путь решения: добавлен канонический `./eng/run-act1-demo.sh`, который подключает
закреплённый .NET runtime и запускает `game/scenes/act1_demo.tscn`; для слабого
GPU остаётся `./eng/run-act1-demo-safe.sh` с Mobile/low. На текущем M4 Pro эти
два wrapper-а дают 120.00 и 120.06 FPS соответственно. Если safe wrapper на
целевой машине всё ещё даёт около 1 FPS, остаются открытыми OS/GPU/driver,
display scaling и editor-versus-package gates; до их фиксации нельзя менять
сюжетный runtime или объявлять performance gate закрытым.

## Act 1 zone transitions now face the next landmark

Слабое место: doorway transitions preserved the player's previous yaw. После
выхода из дома или ФАПа это могло оставить игрока смотрящим назад, а на зирате
лес Кара-Урмана оказывался за спиной без объяснимого физического ориентира.
Старый corridor smoke этого не ловил, потому что перед каждой интеракцией сам
принудительно наводил камеру.

Путь решения: `Main` теперь применяет destination-facing spawn yaw только для
компактных переходов Act 1 (дом, улица, ФАП, зират, кромка леса); возврат из
леса смотрит обратно в деревню. `FirstPersonController.ApplyZoneSpawn` меняет
только presentation transform и сбрасывает наклон/скорость, не меняя kernel,
сохранения, quest state или interaction IDs. Corridor smoke проверяет
объявленный yaw после каждого перехода, а физический walkthrough проходит
93.05 м до клиффхэнгера. Human first-time wayfinding, input drift, cultural
review и art lock остаются открытыми.

## Act 1 journal now exposes the active objective without owning quest state

Слабое место: после закрытия старого ПК игрок видел сохранённую улику, но не
получал в журнале явного следующего шага. В мире уже были diegetic-ориентиры,
однако это оставляло риск, что первый игрок примет отсутствие подсказки за
сломанный маршрут.

Путь решения: `RuntimeBridge.ActiveObjectives()` читает уже вычисленную активную
цель из kernel state, а `JournalUi` показывает её в блоке `ТЕКУЩАЯ ЦЕЛЬ`. Ни
команды, ни сохранение, ни narrative IDs не меняются; журнал остаётся только
presentation projection. Smoke проверяет текст цели после сохранения официальной
справки. Остаются открытыми наблюдаемая понятность маршрута, формулировка для
первого игрока и локализационный review.

После этого сохранение также подтверждается коротким контекстным статусом с
кнопкой журнала (`[J]`/`[Y]`). Это снижает риск, что игрок не заметит новую
проекцию, но не закрывает observed first-time comprehension.

## Act 1 audio-first cliffhanger required ordered presentation

Слабое место: вход в Кара-Урман отправляет два audio events подряд. При
логических audio-ref без физических записей один label мог сразу заменить
текст голоса Марата предупреждением Рината, поэтому технический smoke видел
только последний cue.

Путь решения: `AudioCueUi` теперь держит короткую presentation-очередь и
показывает `Marat → Rinat`; `Act1DemoRoot` ждёт её перед финальной карточкой.
Это закрывает только потерю текста в одном кадре. Authored voice, финальный
ambience/mix, редактура субтитров, timing по реальному голосу и культурная
listening-проверка остаются production-gates.

## Act 1 pine palette improves value separation but is not authored canopy

Слабое место: одинаковый хвойный цвет усиливал повторяемость процедурных
ярусов на кромке Кара-Урмана и сжимал ближний, средний и дальний планы в одну
массу. Это presentation-проблема первого акта, а не повод добавлять шум,
пикселизацию или новый material owner.

Путь решения: `StyleBenchmarkZone` получил детерминированную приглушённую
палитру из пяти холодных хвойных оттенков, выбираемую из мировых координат
ствола. Геометрия, коллизии, shader, texture registry, сохранения, narrative
state и акты II–V не менялись. Реальные Forward+ style frames подтверждают
более читаемое разделение value planes, но forest family всё ещё procedural.

Остаются открытыми authored canopy/ветви, near-mid-far motion sweep,
проверка на M1 и Windows и human art/cultural review. Не усиливать это
текстурным шумом и не объявлять art lock по одному still-кадру.

## Act 1 exterior world is integrated but visual 360 review is not yet captured on the new layer

Слабое место: `AgentBExteriorWorld` теперь даёт production-маршруту
авторский террейн, архитектуру, зират и кромку Кара-Урмана, а физический
walkthrough проходит весь путь (91,76 м, cliffhanger completed). Однако
45-кадровый first-person visual review нового слоя в связке с интерьерными
зонами ещё не снят: существующие кадры — либо изолированный эксперимент
Agent B, либо старый greybox.

Путь решения: следующий capture pass должен снять forward/back/left/right/
depth по восьми зонам уже из `act1_demo.tscn` (реальная камера игрока,
Forward+, без окна на рабочем столе пользователя) и прогнать структурные
QA-гейты no_empty_horizon / no_degenerate_frames на новом слое.

Остаются открытыми культурное ревью меток зирата, ночная экспозиция Кара по
человеческой оценке, M1/Windows performance acceptance и финальная
оптимизация. Наличие зелёных smoke/walkthrough не объявляется art lock или
release readiness.

## Act I road grade and lateral silhouettes are a bounded visual pass (2026-08-25)

Слабое место: в первом connected-world capture дорожные `WetRoad_WornLight`,
`WetRoad_MutedOchre` и грязевые плечи давали слишком контрастные светлые полосы,
из-за чего мокрая дорога читалась как набор модульных плит. Несколько боковых
секторов также теряли дальний силуэт деревни.

Путь решения: `Act1ConnectedWorld` теперь перевязывает только дорожные,
плечевые и дренажные материалы существующего wet-road kit на приглушённый
Painterly earth-профиль; коллизия, route owners и RuntimeBridge не меняются.
В `DistantPerimeterParcels` добавлены четыре малых presentation-only силуэта
из уже закэшированного authored house family и существующие заборы/деревья для
Babai yard, MainStreet, FAP и Zirat lateral views.

Headless Metal capture через `./eng/capture-act1-full-route-core-world.sh`
подтвердил 36/36 PNG, 8 visual zones, forward/back + lateral + near/mid/far,
10/10 traversal waypoints и 242.50 м маршрута; build: 0 warnings / 0 errors,
runtime leak gate: PASS в принятом запуске. Ручной просмотр показывает более
цельную мокрую дорогу и немного закрытые боковые горизонты, но не закрывает
greybox/simple-module, empty-field, authored canopy, lighting и first-impression
gates. Это bounded presentation pass, не art lock и не доказательство готовой
демки.

## Act I ground planes and lateral field edges received a bounded relief pass (2026-08-25)

Слабое место: после material grade часть ближних дорожных плеч, берм и parcel
fields всё ещё читалась как длинные BoxMesh-плиты, а fixed right-turn кадры
MainStreet и Zirat доходили до плоского дальнего поля.

Путь решения: `Act1ConnectedWorld.AddVisualLandformSegment` теперь строит
неглубокую 5×7 low-poly crown surface без вертикальных коробчатых стенок;
`AddVisualParcelPatch` использует неровный presentation-only perimeter вместо
плоского блока. В `DistantPerimeterParcels` добавлены три дальних parcel
anchors из существующего authored village kit для восточного края улицы,
дальнего двора бабая и восточной границы зирата. `AddVillageFacade` получил
сдержанную заднюю оконную обвязку для 360°-проверки. Маршрут, collision,
navigation, interactions, camera, сохранения и RuntimeBridge не менялись.

Новый headless Metal capture подтвердил 36/36 кадров, 8 visual zones,
forward/back + lateral + near/mid/far, 10/10 waypoint и 242.50 м; manual
review подтверждает более естественный relief дороги и несколько закрытых
дальних секторов. MainStreet right field, Babai hero facade, generic
low-poly silhouettes, lighting depth, cultural review и first-impression
quality остаются открытыми. Это bounded visual pass, не art lock и не готовая
демка.

## Act I MainStreet и Zirat получили адресное закрытие боковых полей (2026-08-25)

Слабое место: после relief-пасса правый поворот MainStreet всё ещё смотрел в
пустой дальний участок, а восточная сторона зирата читалась как плоский
горизонт без связи с деревней.

Путь решения: в `DistantPerimeterParcels` добавлены два sparse
presentation-only parcel anchors из существующего `urman_village_exterior_kit`
с фасадом, сараем/забором и разными деревьями; основной MainStreet anchor
подвинут ближе к реальному лучу камеры и получил небольшой mixed-tree edge.
Маршрут, collision, navigation, interactions, camera, сохранения и
RuntimeBridge не менялись.

Headless Metal capture `sideclosures6` подтвердил 36/36 PNG, 8 зон,
forward/back + lateral + near/mid/far, 10/10 waypoint и 242.50 м; manual
review показывает читаемую дальнюю связку фасадов/деревьев в MainStreet и
Zirat. Пустое небо, generic low-poly foliage, простой rear hero facade,
свет, культурная проверка и first-impression gate остаются открытыми. Это
ограниченный composition pass, не art lock и не готовая демка.

## Act I hero house получила локальный rear-window light cue (2026-08-25)

Слабое место: при развороте во двор бабая/әби фасад оставался читаемым как
простая тёмная масса, без тёплого следа недавней жизни.

Путь решения: только placement `BabaiApproachDwellingFacade` получил один
низкоэнергетический `OmniLight3D` внутри presentation-only rear dressing;
дальние копии фасадов остались material-only. Это не новый global light owner:
маршрут, коллизия, navigation, interactions, сохранения и RuntimeBridge не
менялись.

Новый headless Metal capture `hero-warmth` прошёл 36/36 кадров, waypoint и
runtime gates; manual review подтверждает локальный тёплый акцент, но простой
hero silhouette, daylight contrast, authored materials, культурная проверка и
first-impression gate остаются открытыми. Это локальный lighting pass, не art
lock и не готовая демка.

## Зимний baseline 2026-09-10: размещение и бюджет растительности

Новый production capture выявил реальные дома над двором: дальние ряды
шли от отрицательного X к деревне с единой высотой исходной точки. Фаза 1
переносит ряды вдоль внешних слоёв, рассчитывает опору по экземплярам и
заменяет BoxMesh-гряды поверхностью без вертикального торца. Первые
сравнения `URMAN_visual_20260910/phase01` подтверждают устранение домов над
двором. Physical walkthrough: PASS 335,27 м до клиффхэнгера.

Исторический риск фазы 0–1 (закрытие бюджета ниже): baseline имеет 67 648 MeshInstance3D и p95
110,17 мс в скрытом Metal capture на M4 Pro. После первого шага фона p95
114,50 мс. Это не приемлемый финальный бюджет. Требуется исправление
существующей растительности и объединение ветвей; увеличивать плотность
или разрешение маски снега до исправления бюджета нельзя.

Деревья в очищенной полосе, брусковые ветви, прямоугольные навалы,
текстурная колея, материал/деформация снега и конечная UI-типографика
ещё не приняты. Полный план остаётся в работе; технический baseline и
первый исправленный кадр не означают art lock.

### Уточнение зимней дорожной опоры, 2026-09-10

Лучевой захват обнаружил расхождение визуальной дороги и физического terrain
до 14,7 см. Причины: завышенный старый профиль и разная сетка jitter в
Blender/Godot. Коллайдер сохранён. Визуальный Terrain_Main теперь использует
его треугольники; Road_* получает высоту опоры с той же сетки. Проверка
существующего capture-пути измеряет вершины и центры дорожных треугольников
лучами, контролирует зазор ≤5 см и отсутствие пересечения с грунтом.
Дополнительно исправлено противоположное направление yaw в посадке
растительности: смещение от preview board и поворот меша теперь согласованы.
До bounded-приёмки фазы 6 форма старых ветвей, летние кроны и цена отдельных
мешей оставались открытой задачей; эта формулировка относится к историческому
baseline.

### Бюджет зимнего представления, 2026-09-10

Следы на 1024 проверены кадрами и повторным проходом. Окно ограничено 24 м
и 512 отпечатками; дальняя история следов не сохраняется. До оптимизации
растительности CPU-обновление шага достигало 21,34 мс, число draw calls —
около 70 тысяч, p95 — около 98 мс. Результаты после оптимизации приведены ниже.
Скрытый ForceDraw измеряет отдельный capture-режим: его показатели нельзя
выдавать за FPS обычного игрового окна или выбирать по самому быстрому запуску.

### Фаза 6: закрытые проверки и оставшиеся риски, 2026-09-10

Получены 11 фактических кадров: восемь видов мира и три эталонные формы.
Проверены 111 корней на фоновой поверхности и физическом грунте,
36 точек опоры `WinterRoot`. Near/Light/Far используют по 2–3 поверхности;
наземная растительность объединена в MultiMesh-группы размером 12 м.
Ветви не создают тысячи отдельных узлов. Сохранены существующий набор
ассетов, шейдер и игровые контракты. В конфигурации M4 Pro medium 1080p, FOV 75, scale 0,9,
MSAA 2× захват дал среднее 14,057 / p95 15,396 / max 29,874 мс,
10 449 draw calls и 1 853 474 примитивов против baseline: среднее 74,076 / p95 110,173
и 71 081 draw calls. Это время кадра с принудительным рендером скрытого окна.

После прохода `phase06/trample1024` дал среднее 12,952 / p95 14,032 / max 21,09 мс;
21 реальный шаг дал среднее CPU 14,154 и max 19,657 мс. CPU риск остаётся,
как и проверка на других целевых машинах; окно 24 м и маска 1024 неизменны.
Узкий demo smoke и физический проход 335,27 м завершились успешно;
сохранения восстановлены, сборка без ошибок, `git diff --check` чистый.
Материалы проверки:
`/Users/unterlantas/Documents/URMAN_visual_20260910/phase06/contact1080`,
`/Users/unterlantas/Documents/URMAN_visual_20260910/phase06/trample1024/snapshots`
и `/Users/unterlantas/Documents/URMAN_visual_20260910/phase06/footprints_road_after_foliage.mp4`
(104 кадра, 3,466667 с).

Фильтр посадки у крыш проверяет точку опоры по габаритам крыши. Он не
проверяет весь объём наклонного ствола и широкой кроны: такие посадки
требуют отдельного осмотра у края крыши.

Фазы 8–10 приняты по отдельным кадрам, маршруту и замерам. Итоговая локальная
техническая приёмка от 2026-09-11 приведена ниже; она не означает art lock.

## Фаза 10 UI/readability: bounded closure, 2026-09-11

UI-проверка закрыта в заявленном узком объёме. В
`/Users/unterlantas/Documents/URMAN_visual_20260910/phase10/accepted_ui`
есть 34 PNG и 34 JSON: 17 capture на 1280×720 и 17 на 1920×1080 для dialogue,
document, journal, old PC и settings. Receipt покрывает baseline/filled/large
состояния, 19 записей журнала, реальный old-PC document и longest document на
807 символов; в каждом есть панель, Tatar glyphs и keyboard focus.

Исходники подтверждают, что old-PC reader использует фактический
`ReaderArea/Reader` с `document.BodyMarkdown` и `scroll_active`; settings
использует `BodyScroll/Body`, включая вложенный `BindingScroll`, и Theme 20 px.
При масштабе 1.6 меняются font metrics до 48 px, а root layout не
масштабируется. Отдельный `navigation.log` подтверждает безопасное открытие
меню, начальный focus, rollback без apply и сохранение после явного apply.

Остаётся риск интерпретации: это technical/readability evidence, а не
наблюдаемый first-time usability-сеанс, культурное/языковое sign-off или
проверка release-host. Фаза 10 UI закрыта локально. Общая техническая приёмка завершена ниже;
production/art lock требует отдельной художественной оценки.


### Итог зимнего плана, 2026-09-11

Локальная техническая приёмка фаз 0–10 завершена: 56+56 кадров мира,
34 реальных UI-кадра, физический маршрут 335,27 м, видео следов/повторного
прохода, stationary/slow-turn snow capture. Итоговые пути и измерения —
в одноимённой записи `decision_log.md` и `URMAN_visual_20260910/final`.
Критичный baseline-бюджет закрыт: p95 110,173 → 13,592 мс на M4 Pro medium1080.
После следов p95 14,191 мс. Не переносить ForceDraw-результат на обычное окно,
M1 или Windows. Максимальный CPU штампа 19,397 мс на накате и 15,894 мс на
целине; это оставшийся риск кратких задержек, основной расход — обновление
локальной геометрии. Маска остаётся 1024 / окно 24 м / максимум 512 следов.

Устранены подтверждённые финальными кадрами парящие benchmark-лужи и камень
BoundaryStoneNear. Скриншоты проверяют контрольные ракурсы, а не математически
исключают все перекрытия за пределами маршрута. Художественный art lock,
первый пользовательский playtest и культурная проверка прежнего контента
этим техническим проходом не подменяются.

## Интерьеры и пропорции персонажей: калибровка 2026-09-11

В доме и ФАПе общий снежный материал покрывал закрытые полы, стены, мебель
и одежду. `PainterlyMaterialLibrary.ForColor` теперь различает защищённые и
открытые поверхности в своём кеше. Дом, ФАП и интерьерные NPC используют
защищённый вариант. Пол и потолок ФАПа больше не получают фактуру старой
штукатурки; верх исходного пола совмещён с физическим полом, поэтому обувь
Наили не утоплена на 12 см.

В исходном Blender-наборе поднята линия плеч, укорочен корпус и воротник,
удлинены брюки, согласованы кисти и суставы. Имена костей, anchors, LOD и
клипы `Idle`/`Tension` сохранены; положения суставов изменены вместе с
геометрией. У Мансура восстановлен взрослый масштаб и седая борода;
у Гөлсинә читается фартук, у Наили — светлая рабочая одежда. Правый домашний
мебельный ряд возвращён к исходным позициям; коллизия лежанки приведена к
её габаритам. Источники `.blend`, производные GLB и хеши обновлены вместе.

Свежие проверки: сборка C# без предупреждений; существующий Blender verifier
(9 префиксов, 217 пар LOD, клипы, отсутствие коллизий); три 1080p кадра
`/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/interior_calibration_v3`.
Это калибровка двух интерьерных ракурсов, а не финальная приёмка шести лиц,
всех ракурсов и движения. Полное прохождение и художественная оценка остаются
в FINISH-04/07.

2026-09-14, зимние деревья (закрыт частный пункт P0 «палки вместо деревьев»).
Пользователь дважды называл деревья главной визуальной претензией («странные
квадратные деревья», «деревья-палки»). Причина найдена в генераторе, а не в
сцене: радиус ствола задавался как 3,9 % высоты, то есть у 6-метровой берёзы
выходил ствол около 0,5 м в диаметре, первичные ветви до 0,16 м, внешнее
сужение обрывалось с 36 % до 12 % за одно сечение, а тонкие сучья существовали
только в ближнем LOD — на 24–64 м дерево показывало ствол с двумя ветвями, а
дальше пять голых палок. Исправлено в `agent_b_foliage.py`: калибр ствола и
первичных ветвей — доля высоты по породе, сужение непрерывное, три вторичные
ветви и 3–5 сучьев в ближнем LOD, две вторичные и два сучка в среднем, одна
вторичная в дальнем. Набор вырос с 74 878 до 81 070 треугольников, бюджеты
ярусов соблюдены ассертами генератора. Пары «до/после» по трём породам —
`docs/production/urman_visual_review_pack/16_winter_trees_before_after.png`;
kit-contract smoke, коридор и проход 403,43 м зелёные, dev-замер 100,70 FPS.

Что остаётся открытым: хвойные у Кара (повторяющиеся ярусы, грубые стволы)
по-прежнему схематичны, повторяемость форм деревьев на улице не измерялась
отдельно, и общая художественная приёмка Акта I — человеческий гейт.

2026-09-14, хвойные у Кара (пункт ослаблен, не закрыт). Прошлый проход отмечал
«повторяющиеся ярусы и грубые стволы» елей. Замер генератора показал числовую
причину: радиус ветви доходил до 47 % её длины, поэтому двенадцать ярусов
складывались в стопку плоских «тарелок». Ветви утоньшены до полосы 0,13 + 0,21·sin,
провис увеличен с 0,40 до 0,52 длины, в ближний LOD добавлена короткая ветвь-брызга
между ярусами. Ель ближнего LOD 3744 → 5300 треугольников (потолок 6000), средний и
дальний ярусы сохранены, набор 81 070 → 83 626. Кадры до/после —
`docs/production/urman_visual_review_pack/17_spruce_boughs_before_after.png`: крона
воздушнее, сквозь неё видны дальние стволы. Что остаётся: стволы сосен у Кара
по-прежнему из исходного CC0-ассета без переработки, разнообразие пород леса не
измерялось, и общая художественная приёмка леса — человеческий гейт.

2026-09-14, «состав в движении» (открытый дефект конвейера ассетов). Проверка
пункта M6 «все шестеро основных NPC вблизи и в движении» доведена до скелета и
дала неприятный результат: импортированные клипы персонажей **инертны**. Набор
`urman_character_kit` привязывает меши к костям (`parent_type="BONE"`) без скина,
поэтому glTF-экспорт кладёт анимацию в узлы-кости, а Godot импортирует это
дорожками с именем кости (`Alsu_Rig/Skeleton3D:Spine`). Воспроизведение дорожки не
двигает ни `GetBonePoseRotation`, ни `GetBoneGlobalPose`, ни видимый меш;
проверены и штатные форматы `bone_pose/<idx>/rotation`, `bones/<idx>/rotation` —
ни один не работает в этой сборке. Часть каналов в GLB при этом содержит реальное
движение (4 из 24 у клипа: повороты Spine/Head/Arm.L/Arm.R), то есть данные есть,
но не доходят до сцены.

Следствие: прежние доказательства «состав анимирован» (лист 14, «3 из 3 фаз
различаются») опирались на различие кадров, а его давала мировая анимация — снег,
ветки, события VillageLife. Это исправлено в тексте обзора состава.

Что сделано сейчас: `NpcIdleMotion` даёт видимую жизнь тем же API, которым уже
пользовался житель у поленницы (покачивание четырёх костей, амплитуда до ~1,3°,
фаза от мировой позиции, уважается reduced motion); `SceneSmokeTest` теперь
требует, чтобы кости реально смещались во времени, и печатает измерение
(`npc-idle-motion: ... movedBones=2 largestAngle=0,0201`). На кадрах 0/40 в области
персонажа меняется 22 % пикселей против 0–3 % фона и 0 % неба.

**Закрыто в тот же день.** Персонажи переведены на жёсткий скин: каждый меш
получил одну группу вершин на свою кость с весом 1,0 и Armature modifier вместо
привязки к кости; политика записана в самом .blend
(`skin_policy`). После перегенерации экспорт содержит 9 скинов по 8 суставов и 542
скиненых примитива, Godot действительно привязывает меши к скелету
(`skinned=542/542` в консоли smoke), а движение в игре подтверждено кадрами:
в области персонажа между фазами 0/40 различается 16,7 % пикселей против 1,5 %
фона и 0 % неба (лист 21). Кривая проверка `verify_character_kit.py` переведена на
новый контракт и по-прежнему требует ровно одну группу вершин, все вершины с весом
1,0 и правильную кость для кистей — то есть строгость не ослаблена.

Ограничение, которое стоит держать в голове: smoke-сцена собирает зону вне дерева,
поэтому в ней `AnimationPlayer` не тикает; контракт там проверяет сам ассет
(скиненые меши + движущиеся каналы клипа), а фактическое проигрывание
подтверждается кадрами харнесса. Драматургическая разница `Idle`/`Tension` теперь
технически возможна (клипы рабочие), но её выразительность — человеческая оценка.

2026-09-14, неиспользуемые авторские сцены (находка аудита, не дефект игрока).
Проверка «все ли авторские сцены достижимы» дала: в паке Акта I 19 логических сцен,
из них четыре — `scene/mosque` («Двор мечети»), `scene/river_bank_day`,
`scene/selsmag_counter_interior` и `scene/zirat` — **не имеют ни одного входящего
перехода**: ни `targetSceneId` ни в одной интеракции, ни эффекта `scene.request` ни в
сцене, ни в документе. Реальный прогон это подтверждает: маршрут проходит 13
переходов (приезд → дом → маршрут → ФАП → стол с документом → справка → внутренний
реестр → сохранённое сообщение → кромка → перечитка → схема → зиратская дорога →
подход → лес), и ни одна из четырёх сцен в нём не встречается.

Проверено, что это не ломает игрока: ни одна реплика и ни одна цель не зовут туда.
Все семь текстов с упоминанием зирата говорят про **зиратскую дорогу** (она в мире
есть, и цель третьего цикла прямо указывает на неё: «Проверить схему у зиратской
дороги»), а единственный текст про мечеть — это заголовок `scene-mosque-title`,
который игрок просто никогда не увидит. Поэтому «недостижимая локация» здесь не
обман игрока, а неиспользованный задел: мечеть, берег реки и сельмаг — естественные
для авыла места, и если автор хочет их в Акте I, это дизайн-работа (маршруты,
интеракции, наполнение), а не правка одной строки. Отдельно: `scene/investigation-journal`
тоже не имеет входящего перехода, но это не локация, а контейнер для интеракций
сопоставления, которые рантайм вызывает через UI журнала — они покрыты тестами.

Что с этим делать: либо оставить как есть и не обещать этих мест игроку (текущее
состояние — честное), либо подключить их отдельной задачей с приёмкой на
исследование. Молча добавлять переходы не нужно: это меняет дизайн Акта I.

2026-09-14, style-кадры показывали не тот мир (находка, исправлено частично).
Регенерация устаревшего style-sweep вскрыла не проблему картинки, а проблему
доказательства: харнесс `eng/capture-style-motion-sweep.sh` собирал только саму
зону `style_benchmark_*`, а всё, что игрок реально видит, добавляет
`Act1ConnectedWorld` — он прячет `Ground`, `Road`, бенчмарочные `Pine`/`Birch`,
`BoundaryThread`, `DistantWindow`/`DistantWarmWindow` и монтирует общий зимний
heightfield, авторские парцеллы и внешнюю атмосферу. Поэтому старые листы
показывали оливково-зелёную «траву» лета, парящий тёплый прямоугольник окна без
дома и голые палки-ограды — геометрию, которой в игре нет. Для арт-гейта это
опаснее, чем отсутствие кадров: ревьюер оценивал бы то, что не поставляется.

Исправлено: `StyleMotionSweepCapture` теперь собирает то же дерево, что `Main`
(`new Act1ConnectedWorld()` + `SetActiveLogicalZone(zone_id)` + камера в
`origin + авторское смещение`), сохраняя ту же сетку 3×3 и те же fail-closed
проверки импортированного модуля (теперь мета читается у инстанса зоны внутри
connected-мира). 27 клеток пересняты; день — зимняя улица с колеёй и сугробами,
Кара — заснеженная кромка без парящего окна. Тем же способом переснят
`StyleFrameCapture` (три hero-кадра 1920×1080): проверки `HouseA`/`OldPc`/`PineA`,
`WellA_`/`WoodpileA_`/`OldPc_` и layer-1 цели `OldPc` сохранены, только
перекоренены на инстанс зоны внутри connected-мира; проверка ФАП-ориентира
переведена с `VillageSignText` (эту доску connected-владелец скрывает) на
`FapWayfindingLabel` авторского стенда с тем же текстом и метой. Хеши:
день `91c4d004`, дом `488d650b`, Кара `6c21dea2`.

Тем же способом переснят `StyleTemporalComfortCapture`: одно connected-мир на
сцену, один render-only sanitization на мир (23 195 visual meshes, 1 599
удалённых shapes), камера в `origin + авторское смещение`. 216 образцов теперь
измеряют ту же зимнюю улицу, что видит игрок; максимальная средняя
frame-to-frame дельта яркости 0,011 (день, FOV 65°, head-bob), в reduced-motion
ниже во всех сценах, нулевая доля чёрных пикселей сохранена.

Остаётся открытым и зафиксировано в README самих наборов: wetness-кандидат и
puddle-диагностика всё ещё снимают базовую сцену — это честно помечено в их
README как «базовая сцена, не вид из игры», и это осознанно: обе измеряют
контакт raycast'а с конкретным relief-телом и клон одного материала в памяти,
а connected-мир это тело скрывает и заменяет общим heightfield'ом. Приведение
их к connected-миру —
следующая задача того же типа; временный sweep требует более крупной перестройки
(18 сборок мира → одна на сцену).

2026-09-14, `captured_at` врал о дате (тихий дефект, исправлен).
`StyleMotionSweepCapture` и `StyleTemporalComfortCapture` писали в манифест
захардкоженную дату (`2026-08-12` и `2026-08-14`), поэтому любой перезапуск
выглядел как августовский прогон. Теперь оба пишут `captured_at_utc` с реальным
временем, как остальные харнессы репозитория.

2026-09-14, «правки только в тестах не устаревают кандидат» — неверно (исправлено).
Долгое время решение «нужен ли новый экспорт» опиралось на то, что тестовые сцены и
скрипты в PCK не попадают (это правда: `exclude_filter=tests/*`, и поиск по PCK даёт
0 вхождений тестовых сцен). Но поставляется не только PCK: рядом лежит
`data_Urman.Game_*/Urman.Game.dll`, и в неё попадают **все** скомпилированные
C#-классы, включая харнессы из `game/tests/`. Проверка на r43: `strings` по
`Urman.Game.dll` даёт 20 вхождений имён `StyleMotionSweepCapture`/
`StyleTemporalComfortCapture`/`StyleFrameCapture`. Значит правка только тестов
меняет байты архива (но не `URMAN.exe`: движок и ресурсный PCK не менялись —
проверено: exe r44 побайтово равен exe r43, а zip'ы разные).

Что сделано: (1) в receipt добавлен `source.commit` и `source.workTree`, чтобы
привязка пакета к ревизии была проверяемой, а не по памяти
(`eng/verify-desktop-artifacts.sh`); (2) экспортирован r44 из `af98055`, и он
документально отличается от r43 только тестовыми харнессами и receipt'ом;
(3) правило в AGENTS.md переписано: сверять `source.commit`, а не считать пакет
актуальным по памяти. Для игрока r43 и r44 идентичны, fingerprint кампании тот же.

2026-09-14, зонд пакета писал в userdata (тихий дефект, исправлен).
`eng/benchmark-act1-demo-package.sh` запускал реальное приложение без защиты, и
прогон перезаписал `settings.json` (mtime сменился; файлы сохранений не трогались).
Теперь скрипт обёрнут в тот же `protected_run.py`, что smoke-runner и M10-комплект,
предупреждает, если защиты нет, и после защищённых прогонов хеш `settings.json` не
меняется (проверено дважды). Честно про первый прогон: он был без защиты и
`settings.json` тогда перезаписался.

2026-09-14, окружение: этот `sh` не принимает `case` внутри `$( )`.
`/bin/sh` здесь — bash 3.2 (arm64-apple-darwin26), и конструкция
`x=$(case a in a) echo b ;; esac)` падает с `syntax error near unexpected token ';;'`,
хотя на верхнем уровне `case` работает, а `zsh` и `dash` ту же строку принимают.
Проверено: в `eng/*.sh` такого паттерна больше нет (единственный случай был в только
что написанном `eng/build-act1-review-packet.sh` и переписан на сравнение двух
отсортированных списков через `diff`, что заодно ловит и лишние, и недостающие файлы
пакета).

2026-09-14, закрыт пункт 4 матрицы: поиск по архиву не вытаскивает закрытую запись.
Пункт 4 («нет обхода gates через ПК, поиск, разговор, загрузку») стоял ЧАСТИЧНО с
формулировкой «отдельного теста на прямой open/поиск нет». Половина про `open` уже
была покрыта: `old_pc_flow_smoke_test` открывает справку, затем пытается открыть
внутренний реестр напрямую и требует `InvalidOperationException` при неизменной
активной сцене — то есть отказ живёт в ядре, а не только в UI. Не покрытым оставался
поиск, и это не косметика: `OldPcUi.RefreshResults` просто **пропускает**
недоступные документы и рисует одну невыбираемую строку «🔒 Закрытые записи», поэтому
утечка выглядела бы как обычная строка в списке, открыть её UI бы не дал, но заголовок
и текст уже были бы видны.

Добавлен зонд в `act1_ui_readability_smoke_test` (существующий харнесс, без нового
suite): берётся термин, который встречается **только** в тексте закрытой записи
(длина 5–40, одна строка — длинное тело сделало бы receipt нечитаемым; термин
проверяется против текстов всех доступных записей, и если уникального нет, прогон
падает, а не молчит), вводится в поиск, и требуется `rows=0`, `selectable=0`, статус
«Найдено записей: 0» и отсутствие заголовка и первой строки тела закрытой записи в
читателе. Зонд обязан отработать 4 раза (2 разрешения × 2 масштаба текста) — иначе
прогон падает, чтобы проверка не могла тихо исчезнуть. Фактический receipt:
`act1-ui-readability: locked-search probe term='- старая тропа за зиратом;' locked=8
rows=0 selectable=0` при 1280×720 и 1920×1080, масштаб 1,0 и 1,6. Пункт 4 переведён в
ДОКАЗАНО; у него нет «человеческой половины», поэтому он убран из списка
«закрыты механически, но вкус открыт».

Отдельная осторожность, которая стоила времени: `eng/run-smoke-guarded.sh` **не
собирает** C#, поэтому первый прогон нового зонда молча исполнил старую сборку и
напечатал старую строку PASS. Перед smoke-прогоном нужно собирать закреплённым
toolchain (`. eng/dotnet-env.sh && .tools/dotnet/dotnet build game/Urman.Game.csproj
--no-restore --disable-build-servers -nodeReuse:false -m:1`); после сборки зонд
действительно появился в выводе.

2026-09-14, стабильностная линия перепроверена после правки теста.
Все 34 сцены `game/tests/*_smoke_test.tscn` прогнаны подряд под защитой userdata
на HEAD `a3233a7` (свежая сборка закреплённым toolchain): **33 зелёные**, одна
красная — `full_game_flow_smoke_test` с её собственной формулировкой про вход в
Акт II (`Full-game Act 2 entrypoint has no zone-specific dressing, authored
interaction layout, generated house kit, or NPC presentation`), то есть вне
охвата Акта I. Guard отработал 34 раза из 34: каждая сцена напечатала
`protected_run userdata restored byte-for-byte` — это сравнение хешей до и после
запуска, а не обещание. В прогон включён новый зонд утечки закрытой записи через
поиск архива.

2026-09-14, закрыт пункт 23 матрицы: титры и лицензии проверены по содержимому.
Раньше `act1_main_menu_smoke_test` только открывал экран «Об игре и титры», снимал
кадр и проверял, что Escape возвращает фокус. То есть лицензионная поверхность была
достижима, но не проверена: удаление любой атрибуции или поломку кнопки «Лицензии
Godot» тест бы не заметил, а это ровно тот класс требования, который обязан быть
доказан, — атрибуция лицензий CC0/MIT.

Проверено до правки: в поставке лежит `content/credits.ru.txt` (поиск по PCK r44 даёт
этот путь), а его содержание совпадает с происхождением ассетов в реестре —
Quaternius Universal Base Characters (головы) и Stylized Nature MegaKit (сосны и
сухое дерево), Corsica_S (шаги по снегу, извлёк Iwan Gabovitch), Kenney Impact Sounds
(шаги), шесть авторов Freesound (Magnesus, soundofsong, callmethefoo, RIFORKA,
bruno.auzet, lwdickens), Godot, .NET, Blender и ImageGen. Банк шагов в
`game/assets/audio/act1/footsteps/manifest.json` ссылается на те же страницы паков,
поэтому расхождения между реестром и титрами нет.

Добавлено в тест: теперь он находит экран About, требует 16 обязательных семейств
авторов в тексте титров и проверяет, что кнопка «Лицензии Godot» действительно
подменяет текст на лицензию движка — 110 864 знака, содержит `MIT` и `Copyright`, и
кнопка после этого отключается. Строка receipt:
`act1-main-menu: credits name 16 attribution families; Godot license action returned
110864 chars of engine license text`. Пункт 23 переведён в ДОКАЗАНО; host-исполнение
Windows и подпись остаются в пункте 24. Отдельно зафиксировано: инструкции
(`START_HERE_RU.md`, `review_packet/`) лежат рядом с архивом, а не внутри него, — это
намеренно, и внешний запрос передаёт шаги текстом.

2026-09-14, журнал выдавал услышанное слово за понятое (исправлено).
Одиннадцать авторских записей Акта I пишут слово со статусом `guessed` — это момент,
когда Айдар его слышит (сцена прибытия, дом, граница, зиратская дорога, ветки
разговоров). Ядро специально держит лестницу монотонной: запись «услышал» не
откатывает уже подтверждённое слово, и `guessed` живёт до подтверждения документом,
сравнением или разговором. Но `JournalUi` показывал и `guessed`, и `confirmed` одной
и той же строкой «термин — значение», то есть журнал утверждал, что Айдар знает слово,
которое он только услышал. Для пункта 6 матрицы («статус проверки честен») это ровно
то расхождение, которое нельзя оставлять: игрок читает журнал как источник правды.

Проверено, что гейты при этом честные: условие `vocabulary.status` в ядре —
точное равенство (`HasStatus`), а не «не ниже», поэтому «услышанное» слово не
открывает ни реплик, ни документов. То есть дефект был только на поверхности
журнала.

Исправлено минимально: неподтверждённые слова помечаются «(услышано)», и если такие
есть, под списком появляется одна поясняющая строка — «„услышано“ — Айдар слышал
слово, но ещё не проверил его значением». Формат «термин — значение» сохранён, поэтому
существующие проверки (`journal_flow_smoke_test` ищет «урман» и «лес») продолжают
работать. Проверено на текущей сборке: `journal_flow_smoke_test`,
`act1_ui_readability_smoke_test`, `chapter_one_flow_smoke_test` — все exit 0, журнал
по-прежнему вписывается в оба разрешения при масштабе 1,0 и 1,6 (строгие проверки
SaveShot: fit, фокус, глифы, вписывание интерактивных детей).

2026-09-14, визуальный аудит свежих кадров маршрута: одна поверхность вызвала вопрос.
Переснят канонический маршрут (80 кадров, 8 зон, 80 уникальных SHA — PASS) и
просмотрены шесть кадров, которых раньше не видел: `zirat_clue_close`,
`kara_warm_window`, `fap_care_cup`, `rear_minaret_view`, `house_interior_forward`,
`babai_firewood_detail`. Тёплое окно у Кара в этот раз читается правильно — окно
внутри силуэта дома, а не парящая плоскость (это ровно тот дефект, что был найден и
закрыт в бенчмарочной сцене). Двор с поленницей, вид на минарет за домом и интерьер
дома с печью, столом, лампой и ковром читаются как жилые места без артефактов.

Единственная поверхность, которая остановила: в кадре `fap_care_cup` белая плита с
чашкой, термосом, сушёной салфеткой и парой обуви. Разобрано: это не безымянная
материальная заглушка. В GLB ФАПа 25 авторских материалов, и самый светлый —
`FapWindowWarm` 0.64/0.45/0.23, то есть near-white из них получиться не может.
Плита — это **общий снежный heightfield, накрывший авторскую террасу**
`FapEntryPorch_Deck`: FAP-компоненты крепятся как yard props с заглублением 4 см, а
терраса низкая (локальные границы y = 0.30…0.50), поэтому снег оказывается выше её
плоскости. Пропсы тихой заботы стоят на авторской высоте террасы (0.515–0.58) и в
кадре сидят на этой снежной поверхности: получается «на крыльце кто-то оставил чашку
и сухие салфетки», что соответствует замыслу находки `fap-exterior-care-porch`.

Что это значит: это не дефект привязки, а художественный выбор, который стоит увидеть
человеку — снег скрывает авторские материалы террасы, и если арт-гейт хочет видеть
доску/настил, это отдельная правка (приподнять террасу или расчистить снег на её
пятне), а не исправление бага. Записано как вопрос для арт-гейта, а не как
«исправлено»: менять высоту авторского компонента без решения автора не стал.

2026-09-14, пункт 1 матрицы: выход доказан машиной, а не наблюдением.
Пункт 1 стоял ЧАСТИЧНО только из-за оговорки «что выход действительно закрывает
приложение — остаётся человеческим наблюдением»: `act1_main_menu_smoke_test` проверял,
что кнопка «Выход» существует, доступна и имеет подписчика на `pressed`, но нажимать её
не мог, потому что нажатие завершает процесс.

Теперь прогон доходит до меню игровым путём и нажимает кнопку по-настоящему. Сначала
надо было понять, почему первая попытка не сработала: кнопка, взятая в начале теста,
после Continue уже disposed (`ObjectDisposedException: Cannot access a disposed
object`, `Godot.Button`), то есть `EmitSignal` уходил в мёртвый объект и выход
происходил не из-за обработчика. Поэтому проба перестроена: в конце прогона тест
открывает паузу (Escape), возвращается в главное меню через «В главное меню» с
подтверждением (как в `act1_pause_menu_smoke_test`), берёт **свежую** кнопку «Выход»
из вернувшегося меню и нажимает её.

Доказательство держится на том, что обработчик закрывает процесс изнутри: тест печатает
маркер перед нажатием, а строка `did not close the application within 30 frames`
достижима только при неработающем выходе. Receipt текущего прогона: exit 0, маркер
нажатия есть, строки отказа нет, ноль строк `ERROR`, userdata восстановлен побайтово
(`protected_run userdata restored byte-for-byte`). Пункт 1 переведён в ДОКАЗАНО;
человеческий двойной клик по приложению остаётся частью пункта 21.

2026-09-14, пункт 20: cold start измерен на актуальном пакете r45.
Запуск распакованного `URMAN.app` с чтением маркера готовности через псевдо-терминал
(иначе stdout буферизуется и маркер теряется) под защитой userdata: прогретые запуски
дали 4,24 / 4,29 / 4,40 с (среднее 4,31 с), а самый первый запуск после распаковки —
11,23 с. Первая цифра разовая: macOS проверяет неподписанный бандл и заполняет кэши, —
поэтому её нельзя выдавать за постоянную стоимость старта. Для сравнения: прежний
receipt 4,93/4,73/4,61 с относился к r38, то есть текущий пакет стартует не медленнее.

2026-09-14, найден и закрыт гоночный тест в аудио-линии (не дефект игры).
Полный прогон 34 сцен дал **32 зелёные** вместо 33: упал
`act1_audio_transition_smoke_test` с сообщением «World foley pause did not pause the
native stream». Это не списали на «flaky»: одиночный прогон той же сцены три раза
подряд прошёл, значит условие зависело от нагрузки, а не от кода игры.

Разобрано по владельцу: `UiFoley.SetWorldPaused` выставляет
`AudioStreamPlayer3D.StreamPaused`, но в Godot этот флаг не действует, пока поток ещё
не начал играть. `UiFoley.PlayWorld` отдаёт воспроизведение аудио-серверу, который
начинает микс позже, чем узел появляется в группе `world_foley`; тест ждал ровно один
кадр и сразу выставлял паузу. На тихой машине микс успевал начаться, под нагрузкой —
нет, и `StreamPaused = true` уходил в пустоту.

Исправлено в тесте, а не в игре: перед проверкой контракта паузы тест ждёт, пока
`player.Playing` станет истинным, в пределах 400 мс — это заведомо внутри исходника
длиной 1,21 с. Если воспроизведение так и не началось, прогон падает с явной причиной
(«World foley never started playing…»), то есть условие не ослаблено, а сам контракт
паузы не изменён. Проверено: пять одиночных прогонов подряд — exit 0, маркеров отказа
нет; и полный набор 34 сцен, перепрогнанный на той же машине после правки, снова дал
**33 зелёные**, единственная красная — известный `full_game_flow_smoke_test` про вход в
Акт II. До правки тот же полный прогон давал 32 зелёные, то есть это была именно гонка,
а не случайность.

2026-09-14, уборка доказательной папки: 31 ГБ → 1,2 ГБ.
Автор попросил освободить место, назвав `Documents/URMAN_ActI_Finish_20260911` (31,5 ГБ
«повторных рендеров, AVI и ZIP»). Разобрано по составу перед удалением, а не «rm -rf
папки»: 1405 записей, из них основную массу давали двадцать с лишним устаревших
кандидатов (`native_20260912_r1…r23`, `native_20260913_r24…r31`, `native_v3`,
`native_baseline`) по 0,6–1,0 ГБ каждый и пять скриптовых обзоров
(`scripted_native_review_*`) с AVI по ~520 МБ.

Удалено: все устаревшие кандидаты (включая r44 — предыдущий: r45 проверен запуском,
зондом, пакетом рецензента и START_HERE, а любой кандидат воспроизводим командой
`eng/export-desktop-release.sh`), все скриптовые обзоры с AVI (это записи прежних
сборок, которые и раньше нельзя было выдавать за текущее прохождение), а также
историческая черновая выдача: логи прогонов, сотни кадров-папок (`*_frames`,
`*_before/after`, `focused_*`, `motion_*`, `art_round*`), бэкапы и патчи `.cs`,
кандидаты `.blend`, превью, `WORK_STATE.md`.

Оставлено осознанно, три вещи:

1. `native_20260914_r45_candidate` — единственный актуальный кандидат (и распакованный
   `launch/URMAN.app` внутри него, потому что именно его ищет комплект M10);
2. `protected_run.py` — страж userdata, на который ссылаются
   `eng/run-smoke-guarded.sh`, `eng/apply-act1-voice-recordings.sh`,
   `eng/run-m10-playtest-session.sh` и `eng/benchmark-act1-demo-package.sh`;
3. `UniversalBaseCharacters_Standard.zip` (123 МБ) — **единственная локальная копия
   исходного CC0-пака базовых персонажей**: в репозитории лежат только производные
   `.blend` и исходники nature-кита (`assets/source/quaternius/stylized_nature`), так
   что без этого архива пересборку персонажей пришлось бы начинать со скачивания пака.
   Остальные ZIP'ы (по 144 МБ на кандидата) удалены вместе со своими кандидатами.

Проверено после удаления, а не по предположению: `eng/run-smoke-guarded.sh
act1_user_settings_smoke_test` отработал с `protected_run userdata restored
byte-for-byte` (страж на месте и работает); `eng/run-m10-playtest-session.sh
--resolve-only` находит кандидата, бинарник (170 394 672 байта) и стража. Заодно в
комплект M10 добавлен режим `--resolve-only`: сеанс длится час с человеком за
клавиатурой, и лучше проверить разрешение путей заранее, чем узнать о пропавшем
кандидате на нулевой минуте. Исторические записи в KB, которые ссылались на удалённые
папки (`art_round8_frames`, старые `*.log`), остаются историей: их числа и выводы
записаны в самих документах, а сама папка больше не является хранилищем доказательств.

2026-09-14, дебаг-меню локаций в главном меню (инструмент автора, не часть игры).
Автор попросил возможность прыгать в разные локации, чтобы смотреть их и присылать
замечания, в первую очередь Кара-Урман. Сделано: в главном меню появляется пункт
«Отладка: локации», который открывает список авторских зон и переходов (приезд, улица
от дома, дом Мансура и Гөлсинә, ФАП, зиратская дорога, кромка Кара-Урмана ночью и
подход к лесу). Выбор пункта начинает новый сеанс и сразу ставит игрока в выбранную
зону через `Main.SwitchZone`, без прохождения маршрута.

Гейт сделан честным и проверяемым: пункт существует только пока рядом с сохранениями
лежит файл `debug-zones.enabled`. Автору он у меня уже создан, а сборка без этого файла
показывает обычное меню, поэтому в поставку чит не протекает. Это проверено тестом, а не
обещано: `act1_main_menu_smoke_test` теперь требует, чтобы пункт был при файле и исчез
(на пересобранном меню) после его удаления, а сам переход обязан привести в
`kara_urman_night@village_path` при закрытом меню, без интро, без подтверждённых слов и
без завершённых квестов. Строка receipt:
`act1-main-menu: debug zone jump reached kara_urman_night@village_path without granting
confirmed knowledge or a completed quest`.

Отдельно пришлось поправить собственное утверждение: первая версия теста требовала
«словарь пуст», и она падала — вход в зону законно пишет *услышанное* слово как
`guessed` (те самые одиннадцать авторских записей). Поэтому проверяется не пустой
словарь, а отсутствие подтверждённых слов и завершённых квестов: прыжок не должен
давать знания.

2026-09-14, P0 по плану автора: целостность мира (коллизии, дверь, границы, провалы).
Взято в работу первым приоритетом, потому что автор ходил по деревне и проваливался
сквозь текстуры, а заборы и дома не имели коллизий.

**Заборы, дома, надворные постройки.** Причина: авторские компоненты кин-наборов
(`DwellingFacade_`, `OutbuildingShed_`, `FenceSegment_`, `Woodpile_`, `Well_`,
`VillageParcel_`, `FapService*`, `RoadFenceBreak_`, `CulvertStoneCrossing`)
смонтированы presentation-only (`collisionOwner: none`), поэтому сквозь них можно было
пройти. Добавлен слой прокси-коллайдеров: `BuildAuthoredKitBlockers` в
`Act1ConnectedWorld` строит по одному StaticBody3D на компонент, слой 2 / маска 0 (то
же соглашение, что у «provisional layer-2» прокси), боксы выводятся из AABB мешей.
Итог: 57 размещений, 142 фигуры. Что НЕ блокируется и почему: плоские полосы и
перекрытия (фундаментные пояса, сливы, полы, кровельные плоскости) — 635 мешей,
иначе запечатывался бы ходибельный интерьер и появлялся «потолок» из крыши;
внутренние оболочки фасада дома (лево/право/зад) — 618 мешей, потому что интерьер
дома обнесён собственными стенами зоны, а фасадный «футляр» стоит внутри той же
комнаты; тела корпуса ФАПа и фасадов (`*_Body*`) — по той же причине; проёмы
(porch/step/awning/door/gate/lamp/sign/window/canopy) и полоса дороги (±0.15 м)
пропускаются сознательно, чтобы входы и маршрут остались проходимыми. Дверные проёмы
дополнительно «вырезаются» из стеновых боксов: стена делится на 5 полос, полоса с
подходом к двери выбрасывается (13 таких полос).

Проверка: физический обход всего маршрута (реальное движение, не телепорт) —
**422,11 м, PASS**, финальная зона `kara_urman_night`, клиффхэнгер достигнут. Это и есть
доказательство, что прокси блокируют то, что нужно, и не ломают проходимость садов,
сервисных проездов и обходов.

**Дверь дома Мансура и Гөлсинә.** Причина найдена в коде: строка, которая прятала
`DwellingFacade_StreetDoorClosed_LOD0` у играбельного дома («playable house uses the
existing portal/interaction, never a decorative door wall») — из-за этого в фасаде
зиял проём, а за фасадом стоит та же комната интерьера, поэтому автор видел, «что
изнутри происходит». Лист двери возвращён видимым, материал — painterly `wood`
(`5a4433`); дверь presentation-only, вход по-прежнему за интеракцией. Проверено
кадрами: `house_exterior_forward` и `babai_door_detail` — дверь на месте, проёма нет.

**Границы мира и провалы.** Причина: ходибельная коллизия существует только внутри
окна террейна (x −64…66, z −152…104), а клампа перемещения не было — за границей
игрок падал. В `FirstPersonController` добавлен `ClampToAuthoredWorld`, который
работает и до модальной проверки (меню/диалог/финал), и после `MoveAndSlide`: держит
игрока внутри окна с запасом 3 м и возвращает на землю, если он оказался ниже
поверхности больше чем на 2,5 м (счётчики `EdgeClamps`/`FallRecoveries`, строка
`act1-fall-recovery` в логе). Проверка в физическом обходе: четыре края мира, по 300
кадров движения наружу — **1200 срабатываний клампа, 0 кадров за пределами окна,
худшее заглубление 0,02 м**; отдельно игрок ставится на 6 м ЗА окном, и следующий
кадр возвращает его внутрь. Это ровно тот случай, в который попал автор.

**Аудит захвата.** Строгий аудит `Act1FullRouteCoreWorldCapture` запрещал любые
коллизии вне объявленных владельцев трассировки; новый владелец
`authored-kit-blocker` объявлен явно, и аудит проверяет его контракт (слой 2,
маска 0, наличие `authoredKitBlockerCount` у размещения) вместо простого разрешения.
Строка: `act1-kit-blockers: verified_placements=57 verified_shapes=142 layer=2 mask=0
declared_by_placement=true`.

2026-09-14, два собственных дефекта, найденных проверками P0 (исправлены).
Первый: прокси-коллайдеры создавались в начале цикла по размещению, а большинство
размещений оставалось без блокеров — такой `StaticBody3D`, никогда не попавший в дерево,
на выходе считается утёкшим физическим телом. Прибор показал `139 RID allocations of type
'P11GodotBody3D' were leaked at exit` в 21 сцене из 34. Исправлено ленивым созданием:
прокси появляется только при первом реальном блокере. После правки — 0 утечек при тех же
57 размещениях и 142 фигурах.

Второй: `act1_demo_launch_smoke_test` требовал «medium» на старте, но демо правильно
уступает сохранённому профилю, а в локальном userdata лежал `GraphicsPreset = high`
(след моих игровых запусков). Тест падал не из-за сборки, а из-за профиля. Исправлено в
тесте: он удаляет стор настроек перед проверкой, то есть измеряет сборку, а не то, что
случайно лежит в userdata; настройки автора при этом не трогаются (guard возвращает
профиль после прогона).

Третий урок того же рода, что и с аудитом захвата: строгий аудит
`Act1FullRouteCoreWorldCapture` запрещал любые коллизии вне объявленных владельцев. Новый
владелец `authored-kit-blocker` не «разрешён», а объявлен и проверяется: слой 2, маска 0,
наличие `authoredKitBlockerCount` у размещения, печать
`act1-kit-blockers: verified_placements=57 verified_shapes=142 layer=2 mask=0
declared_by_placement=true`.

2026-09-14, B1: лес вокруг деревни стал стеной, а не рощей (первый проход).
Автор: «деревня должна быть полностью окружена лесом… сейчас выглядит как небольшая
негустая роща… хочется стены из деревьев». Причина измерена: периметровая полоса в
`AgentBAct1ExteriorLayer` строилась **двумя рядами** (шаг 3,8 м, отступ 3 м), поэтому
из деревни лес читался как линия, а не как массив.

Сделано: полоса стала **шестью рядами** с уплотнением к глубине (шаг 3,4 / 3,4 / 3,0 /
3,0 / 2,6 / 2,6 м), дальние ряды смещаются в ель (глубокий силуэт против неба), а в
рядах от второго добавлен второй ярус подлеска, чтобы не было просвета на высоте
стволов. Деревья по-прежнему не ставятся в полосу дороги (существующий отсев ≥ 2,6 м).
Проверено кадром `zirat_depth`: сплошная линия елей и берёз по горизонту вместо
редких силуэтов.

Честная оговорка про производительность: headless-замеры
(`act1_demo_performance_smoke_test`) на этой машине гуляют от 6,9 до 23,4 мс в
зависимости от фоновой нагрузки (в момент одного из замеров работали фоновые
перестройки графа), поэтому решать по одному прогону нельзя. Решение о плотности
полосы принимается по упакованному зонду (60 с, реальное окно, средний пресет) при
следующем экспорте; если он просядет, полоса утоньшается.

Заодно проверен и **отклонён** вариант дешёвой стены: упаковка полосы в MultiMesh
(3 инстанс-группы на вариант/ячейку с visibility range) снимает узлы (30 237 → 27 591),
но проигрывает по кадрам, потому что MultiMesh рисует все инстансы ячейки, если ячейка
в кадре, — индивидуального отсева по дереву нет. Замер: 108 → 95 FPS, p95 9,9 → 13,0 мс.
Откатано, причина записана в коде комментарием.

2026-09-14, B2: река и сломанный мост — канонная граница деревня/лес.
Автор подтвердил канон: лес отделён от деревни рекой, через неё — старый сломанный
мост. Первая попытка была неверной и я её исправил по своим же правилам: ледяные
плиты лежали на 10 см НИЖЕ террейна (то есть их не было видно), а поперёк поля
стояли невидимые блокеры — ровно тот приём «невидимая стена», за который автор
ругал игру.

Как сделано теперь: **русло вырезано в самом террейне** (`RiverChannel` в
`AgentBAct1HeightField`, который питает и визуальный меш, и коллизию). Меандр по
x, глубина 3,4 м при ширине 9,2 м — склоны круче 45°, поэтому берега сами
останавливают игрока, и никаких невидимых стен не нужно. Полоса дороги у трубы
обнуляет русло (road gap), поэтому маршрут проходит как раньше. Лёд и открытые
полыньи лежат в русле и видны с берега, снежные валы — на плечах канала, а
сломанный мост (два каменных устоя, обрушенный пролёт, наклонная стойка, верёвка)
стоит поперёк русла у x=15; устои имеют коллизию. Деревья периметровой полосы в
русло не ставятся.

Проверено: физический обход 422,11 м PASS (дорога проходит через разрыв у трубы),
corridor PASS (все три квеста завершены), проба краёв мира PASS. Кадры
`zirat_depth` и `kara_approach_back` показывают тёмную полосу воды со снежными
валами и мост. Визуально «вода» пока сдержанная — если автор скажет, что река не
читается, следующий шаг: шире полыньи, тёмнее лёд, рябь/торосы по берегам.

2026-09-14, исследовательский пакет по Шүрәле: что он даёт и чего в нём нет.
В репозиторий добавлен `docs/research_shurale_package/` — 76 файлов, 19 МБ: тринадцать
тематических документов, отчёт в Markdown/PDF/DOCX, шестнадцать машиночитаемых
реестров плюс XLSX и JSON/JSONL, девять схем (пять интерьерных планов, профили улиц,
структура деревни, переход ландшафта), визуальный атлас и паспорта референсов,
передача разработчику, руководство по арт-направлению и чек-лист аутентичности.

Слабое место, которое надо произносить вслух: это **не канон и не проверка**. Пакет сам
перечисляет свои пробелы — современные частные интерьеры и улицы натурно не обмерены,
пять интерьерных планов и размеры улиц/двора помечены как предложения для игры,
прослушанных полевых фонограмм нет вовсе, а 58 записей источников включают каталоги и
недоступные книги. Права: четыре малые фотокопии под CC BY-SA 4.0 с авторами
(Engelberthumperdink, TimoteoCirkla и др.), остальные референсы — ссылки, неизвестные
права помечены как неизвестные; чужих книг, диссертаций и фонограмм в пакете нет.

Как им пользоваться: дома/наличники/дворы/интерьеры из `04`, `05`, `06`, `10` — вход в
арт-направление (в том числе к замечаниям автора про «не по-татарски»), `07` — в лист
языковой проверки, `08` — в подачу Шүрәле (12 мотивов, фольклор, а не монстр), `12` —
готовый список вопросов для культурного ревьюера. Статусы пакета («документировано /
наблюдается / оценено / предложено для игры / не установлено») при пересказе не
повышаются; правило зафиксировано в `README.md` базы знаний и в `decision_log.md`.
Ничего из пакета в контент Акта I без отдельного решения не переносится: правки
контента меняют fingerprint кампании, а пакет не заменяет ни консультанта, ни плейтест.

2026-09-14, новая авторская оценка деревни: прежние технические PASS не закрывают
замечания к её достоверности, коллизиям, посадке объектов, границам мира, человеческим
пропорциям и интересу исследования. Подготовлен
[подробный план](../research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md),
без изменений игры. Сообщённые дефекты отделены от воспроизведённых и рисков из кода.
В частности, шесть рядов пояса в `AgentBAct1ExteriorLayer` не доказывают ни закрытие
всего доступного периметра, ни непроходимость: растительность этого слоя остаётся
presentation-only. Автор теперь явно требует очень высокий, густой и страшный лес
вокруг всей деревни без сквозных видов; открытый ландшафт исследования сознательно
не принят. При реализации нужны раздельные доказательства объёмного лесного вида,
физической границы и человеческого интереса. Культурные ограничения пакета остаются.

Повторная проверка плана: покрытие тем отчёта не обеспечивало самодостаточность
карточек для небольшой модели. В [поэлементном дополнении](../research_shurale_package/URMAN_ACT1_SOURCE_COVERAGE_RU.md)
учтены все 330 записей; §11 основного плана требует заполненного паспорта объекта
перед реализацией. Новая полная геометрия домов и лесного кольца пока не спроектирована
и не проверена игровыми кадрами. Этот риск нельзя закрывать количеством страниц,
ссылок или перечисленных предметов.

## Act I village rework — what the forest ring and collision batch did not close

2026-09-14, corrected after the independent review wave: the forest ring and the
parcel-collision batch pass the evidence they actually ran (full-route capture
gate PASS 80 frames, physical walkthrough PASS 424.88 m, spawn matrix PASS 13/13,
corridor PASS). That is not the plan's full L3 evidence, and the items below stay
open. Four defects the review found in this batch were fixed in the same pass
(mosque courtyard planted with firs, arrival closure through the reverse-edge
houses, gaps in the boundary thicket, the north band stopping short of the +Z
seam):

- The zirat roadside kit still shows floating shoulder/beam slabs near the
  culvert, visible in `zirat_clue_close` in the pre-rework baseline capture as
  well, so it is pre-existing and not caused by the ring. Owner is the
  river/culvert card (plan L4/T2), not the forest.
- `zone_flow_smoke_test` exits 0 but prints "2 resources still in use at exit"
  (the count varies between 2 and 4 on the same scene). The production capture
  gate does treat that message as fatal, and it passes on the same build, so the
  warning looks like a test-scene shutdown artifact rather than a world leak;
  unproven either way.
- The tall spruce far LOD is still a spiky silhouette on the skyline at 60 m+.
  It closes the view, which is the requirement, but the crown reads thinner than
  the near tier. Art acceptance is open.
- The §12.7 perimeter sweep WAS run on 2026-09-14, as a harness sweep: a 10 m
  grid along the envelope, three outward headings plus up, 324 frames, day on
  all four edges and night on the Kara edge (capture gate 404 frames PASS). It
  found and closed a real eye-level gap on the west edge. What it is still not:
  a human free-camera walk, not the plan's 5 m starting grid, and no
  diagnostic-fog pass — so it proves enclosure at those positions, not the
  absence of every gap.
- The 24 finds and seven physical branches were NOT re-walked after 475 new
  blocker shapes went live. Only the one scripted loop that regressed was re-run
  and rerouted, so a branch that approached a now-solid wall from a side the
  walkthrough does not cover would not have been caught.
- No forest-sector passport per plan §11.3 was written before the ring was
  planted; the sector parameters live in code constants and layer metadata.
- The six NPC proportions are only partly addressed: neck and shoulder breadth
  are fixed at the generator, but arm length, leg line, per-character profiles
  and the movement clips are untouched, and no measurement sheet exists. The
  conversation close-ups in the capture set are the only visual evidence.
- `BlockerShape` divides the placement scale out of a world-space AABB, which is
  exact only for unrotated placements; a facade yawed near ±90° can carry a box
  that protrudes past its visible wall. Pre-existing, still open.
- Card H2 is three steps in (stone plinth and door step; rafter tails, eaves
  troughs and a downpipe; two real window openings in the interior's rear wall),
  all verified on one SHA with the walkthrough intact. Still open inside H2: the
  grey-material shape pass before a final material sign-off and the seni trim
  variance; the interior being one 12 x 10 m room behind a 6.2 x 6.0 m facade is
  an I1 replanning question rather than an exterior one. H2's exterior drainage
  is complete: main eaves trough and downpipe, plus a seni trough, spout and
  splash stone.
- Cards still open after this session: H1/H4-H5 (the yard furniture near walking
  lines, snow trample and the three-path scheme), I1 (author decision pending), the
  rest of N2-N3 (human reference, per-character profiles, clip-by-clip review),
  E4 (human playtest), C2 remainder (native-speaker wording/register), V1 remainder
  (moving-camera is done; the per-sector RenderingServer breakdown and the
  author's light acceptance remain), V2 (seni trough drip needs an authored
  recording; the trough is deliberately silent), Q1-Q2 (final pass), and the
  EX00-EX14 implementation (EX00/EX11-EX13 designed only).- The repeated-sapling silhouette on the north edge of the ring is reduced but
  still recognisable at distance (arrival_12_out-right); that is a variety task,
  not an enclosure one.

### 2026-09-15 — длительность и расширенный объём исследования

- **LEN01, причина не установлена:** автор сообщает о прохождении Акта I за 5–6 минут при сценарном ориентире около 50. Нужны проверка сборки/режима, матрица сценарий→действие→вывод, проверка условий финала и раздельный хронометраж технического маршрута и первого человеческого опыта. Существующие PASS по отдельным функциям не доказывают полноту или длительность главы.
- **EX01–EX14, новый порученный объём:** [§13 плана деревни](../research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md) задаёт зависимости, атомарные работы и приёмку. Риски — потеря/дублирование переносимых вещей, рассогласование сохранения с физикой, обход сюжетных условий и новые сквозные виды на границу сверху. Наличие спецификации не закрывает эти пункты; полная реализация всех 14 и человеческая оценка интереса ещё требуют доказательств.

## Историческая арифметическая оценка длительности — не замер

2026-09-15: reproduced from the repository rather than a stopwatch — walk speed
3.4 m/s, mandatory route 424.88 m (2 min 05 s of walking), the whole chapter's
Russian text is 1,234 words, the core investigation's three comparisons are 22
words, the one mandatory document is 68 words. These counts describe that source
revision only; they do not reproduce a timed first playthrough. The old inference
that a chapter requires 6,000–9,000 mandatory words is withdrawn: no word quota
can establish meaningful duration. Investigative actions, actual source reading,
causality, presentation and final prerequisites must be checked separately. See
`docs/production/act1_len01_playtime_audit_2026-09-15.md`; LEN01.1-LEN01.5 are in
the backlog. No human timing has happened yet, and nothing here substitutes for
it.

### Действующие риски после восстановления 2026-09-15

Минимум основного первого опыта — **60 минут**, без обязательных секретов.
LEN01.1–.5 не считаются законченными на основании старого аудита: человеческий
хронометраж не выполнен. Реализуются вопросы с последствиями, подтверждение
исходной улики при её открытии и применение перевода к перечитанному источнику.
Интеграционная проверка текущего working tree ещё впереди.

EX00/01/03/05 частичны: текущий перенос теряет коллизию после постановки,
не позволяет повторно взять placed-вещь, выбирает цель без линии видимости и
не сбрасывает отсутствующие в загруженном snapshot отклонения. У скрытого топора
после загрузки возможен невидимый held/placed-экземпляр. Эти причины подтверждены
чтением общего владельца; исправление и физические сценарии в работе.

T1: реальный подход контроллером воспроизвёл проход через ствол липы (4–9 мм до
центра, ноль контактов). До исправления лучи не обнаружили bark-коллизию у 404
живых деревьев. Исправление находится у владельца ExteriorLayer, проверка после
правки выполняется отдельно. Исторические снимки кольца не доказывали физику.

I1: расхождение объёма дома 12×10 внутри / 6,2×6 снаружи — техническая задача
согласованной геометрии, разрешённая нынешним поручением; ожидание согласования
каждого размера снято. Канонные сюжетные изменения по-прежнему отдельный вопрос.

### verify-godot после восстановления 2026-09-23

Полный `eng/verify-godot.sh` не прогонялся целиком с 2026-09-18, и 17 из 33
smoke-сцен были красными. Большая часть — устаревшие после переделки мира
проверки; найдены и исправлены и настоящие дефекты игры: сырой
`{address:ADR-BABAI}` без связного мира, невидимая «висящая» скамья над
телефоном приезда, спавн `from_house` за пределами земли компактной улицы,
собранная GC обёртка комплекта персонажей («Handle is not initialized»),
несуществующий слот сохранения как ошибка движка, реестр Марата без абзацев
полей после партии документов 1 (выписка у Наили была невозможна). Сторож
userdata на macOS мог не вернуть сохранения (EPERM) — исправлен, у smoke есть
таймаут. Открыто:

- **W-SOFTLOCK-EDGE [решено 2026-09-24]**: «ложбина» у кромки (x −6…−14,
  z −88…−94) — канонное русло реки между деревней и лесом. Оба берега круче 45°,
  соскользнувший на лёд не мог выбраться. Рельеф не менялся (на нём стоят
  запечённые ассеты). Выход — дощатые ступени к проруби на **деревенском**
  берегу в пяти местах (x −44, −28, −10, 28, 44; `Act1ConnectedWorld.RiverSteps.cs`):
  лестница подбирается под профиль берега и выходит за кромку на ровное место.
  Попутно новый тест нашёл старую дыру в границе: у x≈−41 лесной берег низкий и
  на него можно было выбраться, то есть перейти реку мимо трубы. Закрыто видимым
  буреломом (два ствола и лапник) по лесной кромке вне дороги; снежный вал
  пробовали и отказались — на его плоскую верхушку заходили со склона.
  Проверки: `act1_river_exit_smoke_test` (в `verify-godot`), запасной путь
  edge-probe в walkthrough (ступени → дорога → труба → старт) — оба PASS.
- **W-HEADLESS-TYPE-SCALE [решено 2026-09-24]**: журнал и карта мерили
  масштаб экрана через окно, а в headless окно-заглушка крошечное: подсказка и
  подпись источника получали шрифт 420 px и `act1_ui_readability` падал.
  На headless масштаб окна не учитывается.
- **W-NPC-PRESENTATION [решено 2026-09-24]**: `act1_npc_presentation` падал ещё
  до этой сессии и заслонял остальные проверки: мишень Алсу поворачивалась вместе
  с ней при приветственном повороте; тест не исключал собственную капсулу
  персонажа и не видел пол мечети (слой 2); люди нового кита стояли на 4–7 см в
  земле; тест знал только имена частей первого кита. Всё исправлено, тест PASS
  и включён в `verify-godot`.
- **W-CAST-FIRST-KIT [решено 2026-09-24]**: Алсу и Ринат оставались «куклами»
  первого кита, потому что их шаги держались на одной «деревянной» кости ноги,
  которую код растягивал по длине. Теперь обе походки на новом скелете: стопа
  ставится в точку опоры, колено сгибается аналитическим IK, таз опускается,
  когда шаг уходит ниже прямой ноги; стоя на склоне, Алсу прижимает стопы к
  снегу. Попутно найдено: в экземпляре кита лежат скелеты всех людей (брать
  надо скелет видимых ботинков), кость стопы нового скелета смотрит вдоль
  стопы, а не вверх (наклон по склону — от вертикали мира), клипы не были
  зациклены. У Рината удерживаемые позы: фонарь в левой руке, «стоп» правой в
  Tension. Тесты, где он участвует (финал, чекпоинты, прерывания, коридор,
  глава), зелёные. На первом ките — только тело игрока и житель у поленницы.
- **W-BOUNDARY-ARCH-2026-09-24 [решено 2026-09-24]**: падение
  `act1_boundary_architecture` не было связано с чужой правкой кита деревни
  (те же 4 ошибки и со старым `.glb`). Причины: тест выбирал подход к столбу
  навеса для дров по свободной точке, но не по свободному пути (упирался в
  навершие столба забора в метре от цели); дверь мечети стала настоящей
  створкой на петле (`MosqueEntranceLeafBody`), а тест ждал старый блокер;
  перенесённая берёза у (−33; 8,2) оказалась под баней, поставленной позже,
  и правило «под крышей растений нет» согласованно скрыло дерево и его ствол.
  Тест учтён, PASS, включён в `verify-godot`.
- **W-PC-TARGETS [историческое; исправление найдено в коде 2026-09-27]**:
  прежние большие наложенные цели перед ПК заменены `FitOldPcTargetsToModel`
  (`StyleBenchmarkZone.cs`): питание привязано к корпусу, документные действия —
  к экрану. Недоступные этапы выключают collision layer; выбор луча предпочитает
  активное действие повторному. `FirstPersonInteractionSmokeTest` и второй заход
  `Act1FirstPersonWalkthroughSmokeTest` содержат физическое наведение/E.
  Этот старый отчёт больше не основание повторно менять production. Новый прогон
  при одновременном активном документном этапе не выполнялся; это ограничение
  доказательства, а не подтверждённый дефект.
- **W-FULLGAME-SCOPE [OPEN]**: `full_game_flow` честно падает: компактный
  загрузчик Главы 1 не содержит полевой сцены эскиза. Решение — перевести
  full_game на связный мир Акта I или сузить проверку; без решения не менять.
- Проверки UI выписки и изображений на реальном окне требуют фокуса окна ОС;
  в `verify-godot` (чистый `--headless`) они external/not-run.

### 2026-09-26 — локальный снег и проход мастерской

Новая проверка Тамары выявила видимый Grade_Street_West на 18–23 см выше
traversal terrain в двух точках у поленницы. Квестовые доски посажены на видимый
откос; расхождение ног/снега в целом не исправлено и не принято. Также остаётся
тесный подход к мастерской между козлами, настилом и лестницей бани. Владение
обоими пунктами — ACT1-VILLAGE-COMPOSITION единой очереди; доказательства
`docs/production/tamara_mvp_2026-09-26/walking-route/roadside/`.

### Адреса: граница численного PASS — 2026-09-26

- presentationOnly/visualOnly сейчас исключает из обзора и непрозрачные
  импортированные стены. Простое удаление исключения неприемлемо: подбор
  разворачивает BABAI внутрь дома. Нужен доказанный контракт внешней стороны.
- Фиксированные камеры4м H013/H041 находятся в соседней геометрии: сначала
  проверить поддержанный подход игрока, не объявлять адрес недоступным по кадру.
- Палитра оград использует process-randomized string.GetHashCode, поэтому
  цвета меняются между запусками; отдельный небольшой пакет стабильности.
- Текущие доказательства и отклонённый кадр:
  `docs/production/tamara_mvp_2026-09-26/frontage-sightlines/`.

Уточнение текущего среза 2026-09-26: H013/H041 реально пройдены контроллером
от дороги, E/блокнот/save/load прошли; проблема кадра4м не подтверждает
непроходимость. Человеческий поиск остаётся открытым. Процессный выбор цвета
оград заменён на существующий стабильный HashText; компиляция прошла,
отдельного сравнения двух запусков для палитры не было. Доказательства:
`docs/production/tamara_mvp_2026-09-26/crowded-addresses/` и `frontage-palette/`.

Срез 2026-09-27: крупный native кадр выявил парящую причёску Тамары. Исправлена исходная привязка Head, просвет в проверенных Idle/Talk закрыт. Брови мягче; лицо всё ещё слишком молодое, короткий волнистый силуэт и сходство открыты. Технический пакет `docs/production/tamara_mvp_2026-09-26/brow-hair-fit/` не закрывает человеческую художественную приёмку.

27 сентября, hair-waves: профиль выявил затылок, проступавший сквозь волосы; подгонка существующей сетки закрыла дефект на просмотренных native кадрах. Волны не равны портретной приёмке. Лицо всё ещё слишком молодое; это следующий предмет доработки. Более плотная сетка волос (16029 вершин LOD0) не измерена на целевом железе.

Срез face-proportions 27 сентября: взрослый овал уменьшил детское впечатление, но лицо всё ещё моложе референса. Сильные геометрические складки дали неестественное выражение и не перенесены в игру. Ворот/посадка глаз и волос/кадрирование выхода и реакции проверены на текущем native; художественная приёмка остаётся открытой.

Срез Тамары 2026-09-27: мягкий объём под нижними веками (до4мм) проверен
в native Idle/Talk/профиле и отклонён: в игровом свете возраст почти не
меняется. Генератор/blend/GLB восстановлены точно, protected reimport прошёл.
Не повторять этот малый сдвиг как новое улучшение. Возраст и сходство
нужно решать на уровне более крупных форм лица и масштаба кадра катсцены.
Отклонённый вариант: `docs/production/tamara_mvp_2026-09-26/rejected-eye-age/`.

Срез Тамары 2026-09-27, нижняя часть лица: широкая деформация щёк затрагивает
уголки рта, а увеличение передней части редкой сетки шеи даёт угловатый выступ
в профиль. Оба preview-кандидата отклонены до экспорта; рабочие ассеты не
менялись. Не повторять эту деформацию без исправления формы/сетки. Сравнение:
`docs/production/tamara_mvp_2026-09-26/rejected-lower-face/`. Художественное
направление уточняется у автора; это не блокирует остальные задачи MVP.
