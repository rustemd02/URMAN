# Ledger CHAR — VIS-044, 045, 046, 047, 048, 102, 103 (visual reset 2026-10-07)

Исполнитель: дорожка персонажей (голова/посадка/походка/контакт/дальний NPC/нормализация материала).
База: журнал `docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07.md` (итерации 01–03),
контракт опоры `docs/production/visual_reset_maps_2026-10-07/material_anchor_contract_RU.md`,
стиль `docs/URMAN_VISUAL_RESET_2026-10-07/URMAN_FINAL_STYLE_RECIPE_RU.md` (LiS2-мягкая материальная
стилизация: форма и свет, а не текстурная деталь; одежда — UV/local).

Игра, Blender, экспорт .glb, станция и git в этой сессии **не запускались**. Все карточки,
где результат читается только в кадре, имеют статус `CODE_DONE_VERIFY_PENDING`.

| VIS | Статус | Чем держится |
|---|---|---|
| 044 | CODE_DONE_VERIFY_PENDING | `hero_head_form()` в `tools/blender/generate_character_kit_v2.py` (Mansur) + `SoftSkinResponse()` в `game/scripts/GeneratedCharacterKitDressing.cs`; кадры P05/P05b, R007/R015 |
| 045 | CODE_DONE_VERIFY_PENDING | `tailor_neck_and_shoulders()` + `measure_head_turn_clearance()` (тот же генератор, пилот Mansur); P05b/P19, R007/R042 |
| 046 | CODE_DONE_VERIFY_PENDING | `AlsuStreetWalkPresentation` (каденция + перенос веса + замер скольжения), `RinatFootPlacementModifier.SetWeightShift`, `AnimationCatalog.TrySetMotionScale`; P19 lateral/3-4, C015–C019 |
| 047 | CODE_DONE_VERIFY_PENDING | `CarryCoordinator.SeatGripAgainstBody` + `FirstPersonController.TryGetCarryGrip` + `AccessibilityPresentation.CarryGripSettleSeconds`; P20 carry still/doorway, I035–I038/C006 |
| 048 | CODE_DONE_VERIFY_PENDING | `KitDistantWorkGovernor` (Timer + instance-local state) в `GeneratedCharacterKitDressing.cs`; startup-метрики в `Attach`; P19 near/return-from-far |
| 102 | CODE_DONE_VERIFY_PENDING | `SoftSkinResponse` (весь каст, обе дорожки китов) + `clothAnchor`-меты + UV-правило по `cloth_uv_units`; CharacterArtCapture closeups |
| 103 | CODE_DONE_VERIFY_PENDING | `HERO_HEADS` = Mansur/Gulsina/TimurHazrat с разными авторскими значениями; C001 closeups before-after |

---

## 1. Изменённые файлы

Код (правки аддитивны, внешние имена не менялись):

- `game/scripts/GeneratedCharacterKitDressing.cs` — `HeroConversationPrefixes`, `SoftSkinResponse`,
  `SetClothAnchorMeta`, `ClothFor` (метит якорь), тайминги startup в `Attach`,
  `AttachDistantWorkGovernor` + `DistantWorkState` (VIS-048).
- `game/scripts/AnimationCatalog.cs` — `TrySetMotionScale`, `ResetMotionScale` (VIS-046).
- `game/scripts/RinatFootPlacementModifier.cs` — `SetWeightShift`/`ClearWeightShift`/
  `ShiftWeightOntoPlantedLeg`, порядок «сдвиг таза → reach → IK ног» (VIS-046).
- `game/scripts/AlsuStreetWalkPresentation.cs` — `SyncGaitCadence`, `ApplyWeightTransfer`,
  `EaseWeightTransfer`, `CapturePlantedSoleSamples`, `MeasureSupportSoleSlide`,
  `ReleaseSupportedFeet`, новые поля в `DescribeWalkEligibility` (VIS-046).
- `game/scripts/CarryCoordinator.cs` — `SeatGripAgainstBody`, `ReportGrip`, `GripSeatSteps`,
  сброс якоря при смене предмета (VIS-047).
- `game/scripts/FirstPersonController.Body.cs` — `_bodyCoatMesh`, `CarryGrip`,
  `TryGetCarryGrip` (VIS-047).
- `game/scripts/FirstPersonController.Steps.cs` — только запись факта шага с предметом
  (`carryStepRiseMetres`, `carryStepEyeKeptLevelMetres`, `carryStepReducedMotion`);
  параметры движения и head-bob не тронуты (карточка запрещает без отдельной причины).
- `game/scripts/AccessibilityPresentation.cs` — `CarryGripSettleSeconds`,
  `CarryGripContactTolerance` (VIS-047, облегчённый режим).

Генератор (Blender-скрипт, экспорт выполняет основной агент):

- `tools/blender/generate_character_kit_v2.py` — `HERO_HEADS`, `SHOULDER_FIT_PREFIXES`,
  `ClothMinimumGap`, `ClothPenetrationAlarm`, `hero_head_form()`,
  `tailor_neck_and_shoulders()`, `measure_head_turn_clearance()`; вызовы вплетены в `main()`
  (после `shape_body`/`cheek_fullness`, перед `dress()`) и в `dress()` (после пояса),
  и после `assign_clips()`. `python -m py_compile` проходит.

Не изменены намеренно: `PainterlyMaterialLibrary.cs`, `Act1ConnectedWorld*.cs`,
`Act1DemoRoot.PerformanceCapture.cs`, `eng/*` — см. `HANDOFF_CHAR.md`.
`RinatPresencePresentation.cs` в VIS-046 не тронут: карточка требует **одну** походку,
пилот — Алсу; его шаги уже опираются на тот же модификатор, и менять их без кадра нельзя.

## 2. Точный список потребителей NPC-ткани и их опора

Правило включения UV уже в коде: mesh получает UV-ткань только с glTF-extra `cloth_uv_units`
(`HasMetricClothUv`) плюс два жёстко прописанных носителя. префикс, у которого
`UsesHumanKit == true`, всегда берётся из human-v2 кита, поэтому процедурный путь для
Mansur/Gulsina/Alsu/TimurHazrat/Naila мёртв (это и есть расхождение «15 из 17» в журнале
против фактических 11 живых потребителей — ниже сведено точно).

| # | Потребитель (prefix, kit-путь) | Сегодня | После перегенерации |
|---|---|---|---|
| 1 | CouncilWitness, procedural (в т.ч. тело игрока) | **uv-local** (ForMovingCloth по префиксу) | uv-local + метка UV (двойная защита) |
| 2 | Alsu, human-v2 | **uv-local** (по префиксу) | uv-local по метке kit |
| 3 | CouncilElder, procedural | world-pending-kit | uv-local |
| 4 | ArchiveClerk, procedural | world-pending-kit | uv-local |
| 5 | PactKeeper, procedural | world-pending-kit | uv-local |
| 6 | Mansur, human-v2 | world-pending-kit | uv-local |
| 7 | Gulsina, human-v2 | world-pending-kit | uv-local |
| 8 | Naila, human-v2 | world-pending-kit | uv-local |
| 9 | TimurHazrat, human-v2 | world-pending-kit | uv-local |
| 10 | Rinat, human-v2 | world-pending-kit | uv-local |
| 11 | Resident, human-v2 | world-pending-kit | uv-local |
| 12 | Tamara, human-v2 | world-pending-kit (её пальто/юбка идут через `tamara_housecoat`, UV уже авторский) | uv-local |
| 13 | PhoneGuy, human-v2 | world-pending-kit | uv-local |
| 14 | Alsu/CouncilWitness — addressing-перетекстуры `ClothFor`: robe Тимура хәзрәта (`Act1ConnectedWorld.MosqueImamDress.cs:71,75`) | по метке kit | uv-local |
| 15 | `ClothFor`: продавец Разилә (`Act1ConnectedWorld.PublicBuildings.cs:304,306`) | по метке kit | uv-local |
| 16 | Обувь игрока (`FirstPersonController.Footwear.cs:46-47`, `ForMovingCloth`) | **uv-local** | без изменений |
| 17 | Верх пальто игрока (`AidarUpperCoat`, наследует путь CouncilWitness) | **uv-local** | без изменений |

Итого: живых неверных опоры потребителей было **11** (строки 3–13), стало 0 после
перегенерации обоих китов; 6 строк уже корректны. Числа «15 из 17» в журнале итерации 01
— это подсчёт по строкам вызовов (`GeneratedCharacterKitDressing.cs:360` и `:386`) без
вычитания мёртвых процедурных путей пяти префиксов; таблица выше — воспроизводимая
перечисливая сверка. Каждый mesh теперь сам несёт доказательство:
`clothAnchor = "uv-local" | "world-pending-kit"` (метка ноды, читается из capture-отчёта).

`ApplyHumanMaterials` больше не решает якорь только на cache-miss: классификация поверхности
вынесена из блока кэша, поэтому метка ставится на каждом mesh, даже если материал пришёл из
кэша (иначе второй NPC того же цвета терял доказательство).

## 3. Команда перегенерации кита (выполняет основной агент)

```
blender --background --python tools/blender/generate_character_kit_v2.py -- \
  --root . --ubc "<UniversalBaseCharacters[Standard]>" --ual "<UniversalAnimationLibrary[Standard]>" \
  --only Mansur --export
```

Первый прогон — **только пилот** (`--only Mansur`), потому что `tailor_neck_and_shoulders`
и `measure_head_turn_clearance` печатают числа и падают loudly:
`VIS-045 <name> neckline verts lowered=N penetrations corrected=M max=X mm` и
`VIS-045 Mansur head turn range=Y° min chin above collar=Z mm at frame F`.
Порог тревоги: `ClothPenetrationAlarm = 30 мм`, допуск входа подбородка в воротник `−5 мм`.
Затем полный кит без `--only` (метка `cloth_uv_units` уже ставится `finish_character`
для всех людей, VIS-104) и процедурный кит:

```
blender --background --python tools/blender/generate_character_kit.py -- --root .
```

Порядок обязателен: сначала пилот, потом всё остальное. `.glb` без перегенерации не меняется,
и картинка до перегенерации остаётся прежней по замыслу (правило VIS-104).
Проверка комплекта после импорта: `tools/blender/verify_character_kit.py` +
`dotnet build game/Urman.Game.csproj` (0/0) + capture P05/P05b/P19/P20/C001.

## 4. Что именно делает каждая правка

**VIS-044 / VIS-103 (голова формой и светом).** `HERO_HEADS` задаёт каждому из трёх hero-лиц
свои числа (глаза: посадка `eye_recess` 4–5 мм и диаметр ×0.93–0.95 — это снимает «наклеенные
шарики»; надбровная дуга; скула/нижняя щека; линия челюсти; проекция носа; уши назад) и всё
это непрерывными гауссианами по собственным границам Head-группы, как уже сделанный
`soften_tamara` — ничего не переригуется, скин и лицевые attachments не трогаются.
Герой-полигоны **не** добавлены: карточка требует сначала подсчёт, поэтому pass печатает
`hero head pass triangles body=N head_span=…` — бюджет утверждается по этому числу.
Второй половиной карточки (свет, а не текстура) занимается рантайм: `SoftSkinResponse`
оставляет диффуз мягко-toon без блика и ограничивает силу normal-карты скина потолком
`0.35`, а генератор ставит authored `skin_normal = 0.40` в материал источника.
Поры перестают быть тем, чем лицо отвечает на свет; форма — становится.

**VIS-045 (воротник и плечи).** Пальто кита вырезано по принадлежности костям и раздуто
на один offset по нормалям — отсюда «панцирь» и воротник у челюсти. `tailor_neck_and_shoulders`
для пилота делает ровно три вещи и одно измерение: воротник опускается к основанию шеи
(до 30 мм, только внутри 2.2 радиусов шеи), шапка плеча снимает 12 мм inflated- thickness над
дельтой, и из проймы идут три крупные складки (`sin(3θ)`, 6 мм) — не шум, а место прогиба.
Затем каждый vertex ткани, оказавшийся внутри тела, выносится на зазор `ClothMinimumGap`
(6 мм), а исправленная глубина печатается: допустимые пересечения в крупном плане = 0,
численный max замерен. `measure_head_turn_clearance` проигрывает Idle/Tension/Talk по кадрам
(как `idle_sole_height`: NLA-полосы заглушены, клип оценивается один) и сравнивает подбородок
с кромкой воротника, а диапазон поворота берётся из поверхности лица, не из костной оси.
UV-исправление VIS-031 сохраняется: крой идёт до `metric_cloth_uv` в `finish_character`,
поэтому авторские метрические UV пересчитываются по новой форме.

**VIS-046 (вес одной походки).** Ход хозяйки — `AlsuStreetWalkPresentation`, параллельный
контроллер не появился. (1) Темп клипа теперь привязан к её реальным шагам:
`SyncGaitCadence` читает длину цикла через `AnimationCatalog.TrySetMotionScale`
(кнопка в того же `AnimationPlayer`, без второго миксера) и ставит scale = половина цикла /
шаг (0.28 с обычной, 0.42 с поворотной), в границах 0.70…1.35; на остановке —
`ResetMotionScale`. (2) Перенос веса: на одиночной опоре таз уходит до 18 мм вниз и до
20 мм в сторону опорной стопы; применяется тем же `RinatFootPlacementModifier`, **до**
reach-KK, поэтому постановка стопы не меняется, а `SetWorldPose` стопы остаётся единственным
источником опоры. Величины гасятся `MoveToward` (5 1/с), так что старт и остановка не
меняют высоту тела скачком. (3) Замер вместо впечатления: `MeasureSupportSoleSlide`
считает, насколько реально применяемая (skinned) позиция опорной стопы сдвинулась между
двумя passes модификатора, и хранит худшее значение в `alsuMaxSupportSoleSlideMetres`
бюджет `0.020` м). Те же числа добавлены в `DescribeWalkEligibility`, который
`Act1AlsuWalkProof` уже печатает — новая проверка не требует нового теста.
Видеодоказательство (3 цикла сбоку и 3/4, 30/60 fps) остаётся за станцией.

**VIS-047 (контакт рук и тела).** Предмет висел в 0.6–0.7 м перед грудью: `DesiredHoldPoint`
считается от камеры, а видимое тело (CouncilWitness-пальто) ничего о хвате не знает.
`TryGetCarryGrip` измеряет переднюю кромку **фактически скинутого пальто** вдоль направления
взгляда; `SeatGripAgainstBody` придвигает предмет по этой же линии, пока его собственная
поверхность не коснётся ткани (допуск `10 мм`), и останавливается: дальше — никогда.
Проверяется дистанция до камеры (`GripCameraClearance` 22 мм) и объём через существующий
`ClearVolume`, поэтому дверь/стена не съедаются, а если сесть некуда — остаётся принятая
физикой поза и в мету пишется `carryGripStatus = "refused-blocked"`.
interaction-ID, custody, правила установки и `TryHeldPose` не тронуты; смещение — только
визуальная точка хвата того же узла. Облегчённый режим: `CarryGripSettleSeconds` даёт 0.12 с
на settling при обычном bob-кадре и 0 (хват ставится один раз и держится) при ReducedMotion —
поза не «ползёт» к телу по кадру, и режим сниженного движения её не ломает.
Числа (`carryGripGapMetres`, `carryGripSeatedGapMetres`, `carryGripSeatMetres`) лежат на
предмете.

**VIS-048 (ненужная анимационная работа дальнего NPC).** Прототип один: `background_resident`
(сосед у дровяного штабеля — ни цели, ни состояния, ни маршрута, только живой фон).
Шаг 1 карточки — замер — закрыт кодом: `Attach` пишет `kitLoadMicroseconds`,
`kitClipSelectionMicroseconds`, `kitMaterialMicroseconds`, `kitStartupMicroseconds`
(startup), а governor — `kitWorkGatedSeconds`, `kitWorkStopMicroseconds`,
`kitWorkResumeMicroseconds`, `kitWorkDecisionMicrosecondsMax` (steady-state), отдельно.
Шаг 3: за пределами собственного LOD1-диапазона (48 м + 4 м margin) `AnimationPlayer.Stop()`
не даёт mixer пересчитывать кости того, кто не рисуется; `stop()` не делает reset, поэтому
поза не «сбрасывается» ни при уходе, ни при возврате. Возврат восстанавливает **логическое**
время: фаза цикла `(gatedSeconds % cycle)` возвращается через `Play + Seek + Advance(0)`,
то есть человек приходит в ту позу, в которую его собственный цикл должен был привести,
а не с нулевого кадра. Shared mutable state нет: состояние живёт в одном `DistantWorkState`,
таймер — ребёнок этого экземпляра, `AnimationLibrary` остаётся частной (клипы и так
дублируются на экземпляр в `Attach`). Если вызывающий код сам остановил mixer (как водопровод
у дров), governor видит, что позиция не продвигает, и **разоружается навсегда** — логика и
чужие позы важнее экономии. Игровая логика не затронута: никаких таймеров состояния,
никакого freeze NPC за камерой вместе с механикой.
Шаг 2 (собственный `.glb` на персонажа) сознательно отложен: он имеет смысл только если
станция подтвердит, что costly именно whole-kit загрузка; замеры для этого решения уже
пишутся, а генератор human-v2 уже умеет `--only`.

**VIS-102 (мягкая материальная нормализация каста).** Одна функция `SoftSkinResponse`
обслуживает обе дорожки (процедурный face-путь и human-v2 `skin_textured`/глаза/брови),
поэтому каст не разъезжается на два материала: матовый toon-диффуз, блик выключен,
roughness 1, normal-скан срезан потолком. Рукавицы, реальные пропорции и human-v2 скелет
не тронуты; отдельных пальцев зимой не появилось. «Пластикового asset-пака» больше нет
и на ткани: `clothAnchor`-меты делают режим наблюдаемым, а UV-режим включается только
по авторской метке, так что до перегенерации картинка честно остаётся прежней.

## 5. Открытые риски (что увидит сборка/станция)

1. **API-имена, которые сборка проверит первой** (я не имел права запускать `dotnet build`):
   `AnimationPlayer.SpeedScale`, `AnimationPlayer.PlaybackPosition`, `AnimationPlayer.Seek`,
   `AnimationPlayer.Advance`, `AnimationPlayer.Stop`, `AnimationPlayer.GetCurrentAnimation`,
   `BaseMaterial3D.NormalMapStrength`, `BaseMaterial3D.NormalEnabled`, `Timer.ProcessAlways`.
   Все — штатные Godot 4.x; при расхождении правки локализованы в трёх методах.
2. `tailor_neck_and_shoulders`/`measure_head_turn_clearance` падают loudly. Если пилот
   превысит допуск — это сигнал о неверном `garment(..., offset .028)` или о кройке шеи,
   а не повод отключать проверку: карточка требует измеренный максимум.
3. Governor меняет число детей у экземпляра кита `background_resident` (+1 Timer).
   Проверено по тестам: `Act1FullRouteCoreWorldCapture` считает у `VillageLife` только
   `CpuParticles3D` и `CollisionObject3D/WorldEnvironment`; ни один тест не фиксирует
   `GetChildCount()` кит-экземпляра. Первый же прогон это подтвердит.
4. Мёртвая ветка: альбедо возраста Mansur (`mansur_age_v1_albedo.png`) применяется только
   в процедурном пути, а Mansur живёт в human-v2 → в кадре он не работал никогда.
   В VIS-044/103 решается формой и светом, **не** новой текстурой (карточка прямо
   запрещает компенсировать форму normal/альбедо). Ветка оставлена как есть.
5. `hero_head_form` правит вершины тела, к которому затем `dress()` кроит одежду и
   `hide_covered_skin()` удаляет закрытую кожу. Порядок в `main()` соблюдён (лицо → одежда),
   но посадка воротника пилота после изменения черепа пересчитывается tailor-проходом —
   цифры из пункта 3 раздела 3 это и проверяют.
6. Human-v2 пилот пока один (`SHOULDER_FIT_PREFIXES = {"Mansur"}`): Gulsina/TimurHazrat
   получают по карточке 045 только после человеческой приёмки кадра R007/R042.

## 6. Что должно быть на станции, чтобы карточка стала PASS

Пары before/after на одинаковых camera/FOV/resolution/зоне/профиле (протокол пакета):

- P05 front и P05b three-quarter Mansur (044, 045, 103), R007 и R015/R042 интерьерные ракурсы;
- grayscale-вырезка того же кадра для 103 («читается форма без цвета»);
- P19 walking lateral + three-quarter, 3 полных цикла Алсу, 30 и 60 fps + `C015–C019` (046),
  со значениями `alsuMaxSupportSoleSlideMetres`, `alsuWalkCadenceScale`, `walkWeight*Metres`;
- P20 carry still + doorway, видео прохода в дверь и лестницы (047), с `carryGrip*`-метами
  и отдельным кадром при ReducedMotion = on;
- P19 near / return-from-far для соседа у дров + startup/steady receipt (048):
  `kitStartupMicroseconds`, `kitWorkGatedSeconds`, `kitWorkResumePhaseSeconds`;
- CharacterArtCapture closeups + dialogue views всего каста (102), до и после
  перегенерации обоих китов.

`dotnet build`, импорт и capture — за основным агентом; эта дорожка ничего из этого не запускала.
