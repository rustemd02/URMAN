# 10 — Предупреждения мира/`game`: полный аудит и классификация — 2026-10-05

**Задача.** Собрать каждое предупреждение, ошибку и подозрительную строку лога, которые мир/игра
выдаёт при старте и в рантайме, а также падающие проверки ассетов/реестра; классифицировать,
разобрать bake-in список, список молчания и дать топ-10 исправлений.

**Метод (read-only).** Игра, Godot, импорт, сборки и замеры не запускались (поручение автора
03.10.2026). Сделано статически:

- прогнан только read-only hash-гейт `sh eng/verify-asset-registry.sh` (чистый python, без движка);
- прочитаны 320+ логов под `docs/production/` (волны 10-04: `village_life`, `village_repair`,
  `police_post`; смоуки `act1_takeover_evidence_2026-09-16/`, `act1_takeover_evidence_2026-09-20/`;
  `visual_rework_2026-09-29`), плюс `performance_audit_2026-10-03.md`,
  `night_shift_summary_2026-10-04.md`;
- выгружены и сгруппированы все 170 сайтов `GD.PushWarning`/`GD.PushError` (78 — в рантайм-коде
  `game/scripts` без тестов), проверено, какие из них реально встречаются в архивных логах;
- статически сверены ссылки кода и контента на `res://`-ассеты, `.import`-файлы, id каталога
  `content/world/catalog.v1.json` и id интерактивов против `urman.chapter1.compiled.v1.json`.

**Оговорка.** Логи последних волн — это guard-смоуки (`Act1Demo`, `Act1HouseholdLifeSmokeTest`,
`Act1PolicePostSmokeTest`, `Act1VillageRepairSmokeTest`), а не человеческий проход обычной игры.
Человеческий сеанс в проекте — `external/not-run`. Поэтому частоты «в обычном сеансе» ниже —
оценка по кодовым путям и по смоук-покрытию; пункты, где нужен реальный запуск, помечены
`needs runtime`.

---

## 1. Сводная классификация

| # | Что | Источник | Когда срабатывает | Последствие | Игрок заметит? | Класс |
|---|-----|----------|-------------------|-------------|----------------|-------|
| 1 | `Vehicle babay-niva placement rejected … recovered to physically checked parking.` | `game/scripts/VehicleFleet.cs:211-212` | каждый старт сеанса/респавн `village_day@arrival` (в `preview.log` Act1Demo — 2 раза за прогон) | Нива не стоит на авторской парковке; восстанавливается поиском ±6/±12 м; контракт `VehicleSmokeTest.cs:59` «ordinary authored start needs no parking recovery» нарушен | Да, если сверять с задуманной стоянкой; сама машина физически валидна | **REAL BUG** |
| 2 | Exit-блок: `resources still in use`, `ObjectDB leaked`, `RIDs leaked`, `Pages in use`, `shader never freed` | движок (не код игры), строки 72–99 `village_life_2026-10-04/native.log` | каждый выход любого прогона, 100 % детерминировано | Только шум в логах/CI; сессию и сейвы не портит; маскирует новые утечки | Нет | **ACCEPTED NOISE** (до work по теардауну кэшей) |
| 3 | `ERROR: timeout waiting for fence` | Metal-драйвер движка (`rendering_device_driver_metal3.cpp:54`) | иногда при выходе native-прогона на Apple M1/M4 (5 записей в 5 логах) | Ничего; кадр к моменту выхода уже снят | Нет | **EXTERNAL** |
| 4 | `act1-overlap-cleanup: hidden=4`, `legacyRelocated=0 authoredOwners=12` | `Act1ConnectedWorld.OverlapCleanup.cs:72,89` | каждый билд мира, детерминировано | Скрыты 4 безадресных сарая; ничего игрового (защита `IsProtectedGameplayNode` бросает исключение) | Нет | Информация; в старых волнах было `hidden=41 / relocated=12` — при цитировании логов указывать волну |
| 5 | `act1-yard-composition: hidden=28` | `Act1ConnectedWorld.YardComposition.cs:95` | каждый билд мира | Скрыты дубли оград/сараев во дворе бабая; игровых узлов не скрывает (иначе throw) | Нет | Информация; в старых волнах `hidden=41` |
| 6 | `act1-open-part-clear: hiddenMeshes=793 …`, `act1-gorge-clear: hidden=606 …`, `act1-gorge-clear: large mesh spans the gorge` | `OpenPart.cs:197`, `Gorge.cs:442` | каждый билд мира | Скрыта открытая presentation-геометрия по миру и лишние меши у ущелья; `BackdropGround` намеренно оставлен и лишь печатается | Нет | Информация (пугающая формулировка) |
| 7 | `act1-street-frontage: rejected axis=39, footprint=2` | `StreetFrontage.cs:428` | каждый билд мира | Ось без фронта просто пропущена; звучит как ошибка, но штатная ветка | Нет | **STALE MESSAGE** (переименовать в info) |
| 8 | `WARNING: Interaction … rejected: ContentConditionRejected …` | `RuntimeBridge.cs:1358` | когда игрок жмёт действие, а условия контента его не пропускают (в смоуках `arrival-enter-house` ×37, `reread-to-edge-sketch` ×6) | Действие честно отклонено; в логе — warning на каждое легитимное «ещё нельзя» | Нет (игрок видит отказ в UI) | Шум; понизить до Print/debug |
| 9 | `Ignoring interaction that is not present in the compiled campaign` | `RuntimeBridge.cs:551` | клик по объекту, чей id не скомпилирован | Игрок жмёт — ничего; ловится поздно | Да (но в текущем контенте не воспроизводится: все id резолвятся) | Defensive / нужен startup-guard |
| 10 | `authored-world: <id> has kind … with no executor`, `unknown catalogue entry`, `model … could not be read` | `AuthoredWorldDirector.cs:206,217,242` | при сборке мира | Пропуск объекта; в текущем контенте не срабатывает (generic-плоты: только prop/npc/scatter; 6 catalog id и 2 scene существуют) | Нет | Defensive (не сработавшие) |
| 11 | `SaveGameV3 load failed / write failed / recover…`, `Quick save is unavailable…` | `RuntimeBridge.cs:170,199,285,331,391,392,409`; `RuntimeBridge.LoadRecovery.cs:49,60` | ошибки IO/слотов, ввод до готовности, негативные тесты смоуков | Загрузка отклоняется/откатывается; сохранность сейвов обеспечена | Да, в сценарии сбоя | Defensive/external |
| 12 | Renderer `Condition "vertex_count == 0"` + `Index p_surface … out of bounds` | старый `WindowBatching.cs` (сейчас `:207-231` явно валидирует `vertex_count` и не вызывает `ImporterMesh.FromMesh`) | встречалось только в `renderer-arrival-03-on.log` (09-17), 246 раз | Тогда — битая сборка батча окон; сейчас страж на месте | Нет | **STALE (исправлено)** |

Полная раскладка по 78 рантайм-сайтам: 12 — `RuntimeBridge.cs`, по 3–5 — `VehicleFleet`,
`Main`, `Act1DemoRoot`, `VillagePaSystem`, `UserSettingsStore`, `SuspensionBridgeDynamics`,
`AuthoredWorldDirector`, `AudioSettingsService`; остальные по 1–2. В архивных логах реально
встречались только семейства 1–9, 11; всё остальное — защитные ветки, которые не срабатывали
ни разу (см. §5).

---

## 2. Bake-in список (разбор)

### 2.1 Нива: рекovery парковки — «ещё корректен после изменения физики транспорта?»

**Факты.**

- Авторская стоянка: `game/content/vehicles/act1_vehicles.v1.json` → `babay-niva`, `spawn=[-1.3,0,17]`,
  `yaw=180`. Загрузка/заземление: `VehicleFleet.cs:87-94` → `VehicleController.GroundAuthoredSpawn`
  (`AgentBAct1HeightField.CollisionGround`).
- Во всех логах 10-04 (28 записей) предупреждение одинаковое:
  `ChassisCollision intersects …/AgentBExteriorWorld/AgentB_TerrainCollision; shape=…/AgentB_TerrainFaces index=0;
  chassisOrigin=(-1.3, -0.49108145, 17); recovered to physically checked parking.`
  Ни одного `no clear authored parking` — recovery **всегда** находит место.
- Срабатывает в реальной сцене `Act1Demo` (`police_post_2026-10-04/preview.log:55,67` — дважды за
  прогон), не только в тестовых сценах. Значит, обычная новая игра тоже начинает с этой ветки.
- Путь recovery (`VehicleController.TryRestoreAuthoredParking`, `:796-831`) сам по себе физически
  честный: перебирает 0/±6/±12 м вдоль оси стоянки, заземляет по фактическим support-точкам
  (отказ, если разброс пола > 0.35 м), требует полный набор опор, прогоняет
  `ValidatePhysicalPlacement` (компаунд-коллизия + дорожный граф) и только затем включает
  `PlacementAvailable`. То есть после «vehicle physics change» (compound/wheel collision,
  изменение `VehicleHorsePose`/`CompoundCollision`, см. `performance_audit_2026-10-03.md`) recovery
  не сломан — он корректно лечит позу, которую текущая коллизия не принимает.
- Но авторская точка невалидна **до** recovery: `chassisOrigin` в логе равен ровно `Definition.Spawn`
  (Y = ground(-0.491)), а пересечение идёт с фактическим коллайдером рельефа, а не с аналитическим
  heightfield. Это расхождение heightfield↔collision mesh под стоянкой (или перекрытие дорожного/
  дворового меша) — гипотеза, требующая runtime-кадра для точной причины.
- Контракт проекта прямо запрещает такой старт: `game/tests/VehicleSmokeTest.cs:59`
  `Require(!parked.HasMeta("rejectedPlacementProbe"), "<id> ordinary authored start needs no parking recovery")`.
  `performance_audit_2026-10-03.md:152-153` уже фиксирует, что `VehicleSmokeTest` из-за этого не
  запускался: «fleet parking assertions … заведомо сталкиваются с текущей отклонённой Niva placement».

**Вывод.** Recovery корректен как страховка, но он маскирует реальную регрессию авторской стоянки.
Правильное лечение — переавторить spawn Нивы (взять валидную позу, которую recovery доказуемо
находит), а не принимать warning. Игрок: машина стоит не там, где задумано (до 12 м в сторону);
при этом не интерпенетрирует и доступна. `needs runtime`: какая именно дистанция принимается и
как выглядит финальная поза (мета `restoredParkingRecovery`, `rejectedPlacementProbe` уже
публикуются в дерево — проверка занимает один отладочный сеанс).

### 2.2 Exit-строки утечек: какие ресурсы и вероятная причина

Из `docs/production/village_life_2026-10-04/native.log:72-99` (идентично в `police_post`,
`village_repair`, headless-прогоне):

- `2 × Pages in use` (`GeometryInstanceSurfaceDataCache`, `GeometryInstanceForwardClustered`);
- `1 shaders of type SceneForwardClusteredShaderRD were never freed`;
- `3 × Leaked instance dependency … did not call instance_notify_deleted`;
- RID: `2 UniformBuffer`, `13 IndexArray`, `13 IndexBuffer`, `6 VertexBuffer`;
- `53 ObjectDB instances leaked`, `48 resources still in use`;
- RID allocations: `1 Mesh`, `2 Material`, `1 Shader`, `1 Instance` (`RendererSceneCull`).
- Headless — те же числа в Dummy-рендерере (`RasterizerSceneDummy*`, `DummyMesh/DummyMaterial/DummyShader`).

Числа стабильны от прогона к прогону (13/13/6/2 везде) — это не растущая утечка, а
**остаток статических кэшей** Godot-ресурсов в C#-статике, которые движок видит при shutdown
раньше, чем выгружается managed-сборка. Кандидаты (все — `static`):
`PainterlyMaterialLibrary.Materials` (`:478`), `RuralPropMaterials.Cache` (`:9`),
`RuralPropGeometry.Cache` (`:9`), `AgentBKitMaterials.Cache` (`experiments/agent_b_act1/AgentBKitMaterials.cs:57`),
`GeneratedCharacterKitDressing.HumanMaterials` (`:364`), `VehicleVisualFactory.NivaModel.ModelMaterials` (`:12`),
`VillageWindowMaterials.Materials` (`:29`), `WinterParticleSurfaces._mask/_fade/_openSnowMeshes/_roomExclusionShader` (`:9,16,18`),
`TamaraFenceQuest` статические материалы (`:269-273`), `Act1ConnectedWorld.SquareInteriors.SquareGlass` (`:16`),
`PolicePostAssets.Scenes` (`:8`). Единственный кэш с очисткой для тестов — `PolicePostAssets.ClearCacheForTests()`;
у остальных нет выхода.

Последствие: только хвост лога/CI («8 baseline renderer/resource leak errors», зафиксировано
`performance_audit_2026-10-03.md:157`), игрок не видит. Риск — новая настоящая утечка утонет в
этом шуме. Дёшево: (а) один debug-прогон с `--verbose` даст ObjectDB-адреса, (б) добавить
`ClearCaches()` на выходе SceneTree/`_ExitTree` мира, (в) минимум — заморозить точные числа в
этом документе как baseline.

### 2.3 Overlap-cleanup / yard-composition hide-counts

- `act1-overlap-cleanup: hidden=4` — это длина статического списка `OverlapSuppressions`
  (`OverlapCleanup.cs:18-24`: 4 безадресных сарая), а не «сколько реально удалось скрыть»:
  отсутствие цели бросает исключение, так что счётчик совпадает с фактом. `legacyRelocated=0
  authoredOwners=12` — все 12 переездов домов уже принадлежат авторским плотам (мета
  `placementOwner`), перемещений нет.
- `act1-yard-composition: hidden=28` — реальное число узлов (4 дублирующих прогона ограды
  + китовые столбы/пролёт + 2 саженца), каждый проходит проверку «нет игровых узлов».
- История: в логах 09–28 встречались `hidden=41` и `relocated=12`. Это **не регрессия**, а смена
  волны/сцены; при цитировании обязательно указывать дату/лог, иначе легко принять за поломку.
- Игрок ничего не видит: скрытие выставляет `Visible=false` + `CollisionLayer/Mask=0` +
  `Disabled` (см. `Act1ConnectedWorld.cs:10083-10116`), игровые узлы защищены `IsProtectedGameplayNode`.

### 2.4 Registry preflight: точный список и последствия

`sh eng/verify-asset-registry.sh` (запущен read-only, exit=1, 13 FAIL-строк):

| assets[i] | id | файлы записи |
|---|---|---|
| 0 | `model.rural.realism.shared.20260929` | `derived` = **проза**: «Runtime cached metre-scale ArrayMesh; furniture and tea props»; source = `game/scripts/RuralPropGeometry.cs; game/scripts/RuralPropModels.cs` |
| 30 | `character.fullgame.lowpoly.v1` | `game/assets/generated/urman_character_kit.glb` + `assets/source/blender/urman_character_kit.blend` |
| 60 | `urman.act1.village_exterior_kit` | `game/assets/models/act1/urman_village_exterior_kit.glb` + `…blend` |
| 63 | `urman.act1.agentb.terrain_road_kit` | `game/assets/models/agent_b_act1/agentb_terrain_road_kit.glb` + `…blend` |
| 65 | `urman.act1.agentb.foliage_kit` | `game/assets/models/agent_b_act1/agentb_foliage_kit.glb` + `…blend` |
| 113 | `vehicle.niva.body` | `game/assets/generated/urman_niva.glb` + `assets/source/blender/urman_niva.blend` |
| 114 | `character.act1.human.v2` | `game/assets/generated/urman_character_kit_v2.glb` + `assets/source/blender/urman_character_kit_v2.blend` |

Точный вывод гейта (13 FAIL + хвост, дословно):

```
asset-registry-preflight: FAIL: assets[0].derived: missing file: Runtime cached metre-scale ArrayMesh; furniture and tea props
asset-registry-preflight: FAIL: assets[30].derivedSha256: expected a12a4ef860d80bb2bf5a1e1d929378b51e64314414e1886d25a732c38fc5877d, actual 4c4136a471fcc21c1c190217c121ecac3f2991912ea9160523802090b4982693
asset-registry-preflight: FAIL: assets[30].sourceSha256: expected 8588e69133fa1189d8962258fb518db0393c237f4ab4f9a580192a89f55eccfa, actual b344cd9865b64af19d504a9c5437c4b0323d7e5815642f3d6d989c9bdb4c948e
asset-registry-preflight: FAIL: assets[60].derivedSha256: expected 9134570f7481abfec3d09dd62f03e55ca175492d9322064ec26a85f73136c48e, actual f3570f9d0ebfc37e3066bc4d39c6b3e90755ba2cbf75c38a57232aedb08c7b9e
asset-registry-preflight: FAIL: assets[60].sourceSha256: expected 44be2ff80169750d53c33d51afe70fb203d1fe02a436b0ea4a2e24a169da2fba, actual f3fa9137183c486692f6e542909ba34278b7dd1826e976e08328e15a406e0521
asset-registry-preflight: FAIL: assets[63].derivedSha256: expected c35b5c7334221443a2ec17ea6e341cd7c1c4c490218b01b7eca90e1c3532e7fc, actual e13c7e234232e3f85342055e6bc9637084bff3835a89a179e779e80d70b12017
asset-registry-preflight: FAIL: assets[63].sourceSha256: expected 3c9fbb633a1948893f9779dabc8f1a963e57ea457164903b4b11dd803a28f567, actual ef1f99560c6523c60351e66ea9b5fc14b52fa04c80e05f8ecb82c50ce89be21a
asset-registry-preflight: FAIL: assets[65].derivedSha256: expected 23a5ff03617ca3de1150d504e2bec3eadd4f627d6e062d61fe3b15d010ddb75f, actual d21df9c1f3020fdf686ab0fb79597bf09e738ae16de527c45944d814ac3bc52e
asset-registry-preflight: FAIL: assets[65].sourceSha256: expected 551a3e83821fc091e77c650f2c7b51767fb445d436deec4fc96ea3ace0971c15, actual 40743fc7a6d6e0934126f64e404ffd6d2d566e640fcfd6ae0fa0dccd113c993c
asset-registry-preflight: FAIL: assets[113].derivedSha256: expected 0385f483a314acee33b54ce34033d3fdad6c6818db2a2d09308c987d29ef01e4, actual 63d262259c061103728a30fc698f66c17e3a32a95333528ca17086988a8a0ecb
asset-registry-preflight: FAIL: assets[113].sourceSha256: expected d519b541671bd57791f5fc92626ed4dee4be7de466ffd01bbac761e923d9bd9c, actual d9e45fb8a1fed36ee29da17f21127a3cf2199ddd7d701aa93e5c80047c4ea3c4
asset-registry-preflight: FAIL: assets[114].derivedSha256: expected b1b4bf8958b86c29fef9695becd787ac133a491772c3049502d90e6dceb4f6e6, actual 91e1d50991def548d9993e63ce77205715512a67b64f2c75077b7ce070bb1e9b
asset-registry-preflight: FAIL: assets[114].sourceSha256: expected eb90578e5da77f8b8fe17fdf2c77da4e1f709fdbb7faf8d50c86cf0e3b20e22e, actual e9a2f161bf9f2ea0c4e7718b5d9e5b82428076b43f1ac1042bef5124f7a393a0
asset-registry-preflight: checked assets=164 derived=164 explicit_sources=78
```

Причина: генераторы/исходники перегенерированы без обновления записей реестра
(в `night_shift_summary_2026-10-04.md:88-89` это уже признано: «перегенерация без обновления
реестра»), а у записи `[0]` `derived` заполнен описанием рантайм-меша, а не файлом. mtime файлов
и реестра совпадают (2026-10-04 10:36), но байты не совпадают — реестр писался не из текущих
файлов; у niva/`[113]` файлы от 2026-09-29, т.е. запись устарела минимум с той волны.

Последствия: `eng/verify-assets.sh` падает на первой же строке (`:11`) — Blender-проверки
provenance даже не запускаются; `eng/rebuild-assets.sh` (`:34`) в конце прогона гарантированно
FAIL; provenance шести ключевых китов (персонажи, экстерьер деревни, terrain/road, foliage, Нива)
и персонажного кита v2 сейчас **не подтверждена**. Игрок не затронут напрямую, но это ломает
rebuild-пайплайн и любые заявления «verify-assets PASS».

### 2.5 `.import`-стражи

- Среди 100 существующих импортируемых ресурсов, на которые код ссылается строковыми литералами,
  пропущенных `.import` — **0**; среди 369 `.import` в `game/` — **0** сирот с отсутствующим
  `source_file`.
- Незакрытая дыра: пути, собираемые интерполяцией. `FootstepAudioController.cs:251`
  (`step_{surface}_{variant:00}.wav`) проверяет `ResourceLoader.Exists` на каждый вариант и тихо
  пропускает отсутствующие; `CompiledCampaignRepository.cs:451-459` строит `res://assets/{file}` из
  манифеста кампании (сам гейт реестра такие файлы не видит). Один debug-счётчик «нет N
  footstep-вариантов для surface X» закрыл бы риск.

### 2.6 `ResourceLoader.Exists`-фолбэки, которые тихо пропускают фичу

| Источник | Что пропускается молча | Сейчас файл есть? | Примечание |
|---|---|---|---|
| `PainterlyMaterialLibrary.cs:471-475,949-960` | зимняя альбедо-текстура (`WinterTextures`): `rowan_berries_v1`, `wattle_weave_v1` | **нет обоих** (задокументировано в `texture_runtime_inventory_2026-09-22.md` F08/F09) | поверхность рисуется базовым painterly; `SurfaceTextures` при пропаже, наоборот, бросает исключение (громко) |
| `AgentBAct1HeightField.cs:137,156-157` | `res://content/world/terrain.v1.json` (strokes) при отсутствии → `[]` | файла нет нигде в репо | Studio пишет `game/content/world/terrain.v1.json` (`StudioWorldSection.cs:60`) — путь расходится |
| `PrologueVoice.cs:31,51,81` | превью-озвучка пролога (клип не найден → тишина, «captions keep working») | превью-TTS есть; финальных записей нет | внешний блокер; молчание — осознанное, но невидимое |
| `AudioCueUi.cs:169-174` | аудио-клип при отсутствии URL → подстановка NonAudioCue/субтитров | — | один debug-список отсутствующих URL был бы полезен |
| `FootstepAudioController.cs:246-256` | отдельные варианты шагов (0..2) | все 4 surface присутствуют | нет счётчика «сколько вариантов загружено» |
| `VillageSoundMoodDirector.cs:45-59` | слой деревни/дред и adhan-запись при отсутствии файла | все 3 файла есть | `AdhanRecordingReady` лишь гейтит фичу |
| `VehicleSnowTracks.cs:102`, `VehicleRadioPlayer.cs:158`, `ClubGramophone.cs:61`, `VillagePaSystem.cs:391`, `UiFoley.cs:152`, `SuspensionBridgeDynamics.cs:544`, `BathhouseSteamAtmosphere.cs:497`, `BathSpirit.cs:526`, `Act1DemoRoot.PrologueRideRadio.cs:16,48` | соответствующая звуковая/визуальная фича | файлы есть | тихие skip-и, каждый по одному |
| `RuralPropMaterials.cs:37-39` | текстура поверхности → цвет без карты (мета `dedicatedTexturePending`) | все заявленные есть | тихо; мета видна только Studio |
| `CivicSurfaceLibrary.cs:53-54` | текстура/атлас → **предупреждает** `PushWarning` | все есть | хороший пример: предупреждение сработает |
| `Act1ConnectedWorld.MosqueCarpets.cs:160-163` | ковёр → warning «awaits asset import» | файл уже есть | предупреждение стало мёртвым, но безопасным |
| `ForestEdgePresence.cs:115-121` | raven/magpie/branch у кромки леса | все 3 файла есть | тихо |
| `AuthoredWorldDirector.cs:224-247` | сцена каталога: при отсутствии — прямой glTF-путь, при неудаче — `PushError` | все 2 сцены есть | громко на отказе |
| `VillageHouseholdDirector.cs:56-58` | — | все мотивы есть | **бросает исключение** при пропаже, т.е. громко (хорошо) |

---

## 3. Список молчания: где предупреждение должно быть, но его нет

1. **Зимние/опциональные текстуры.** `WinterTextures` пропускаются по `ResourceLoader.Exists`
   без следа. Дёшево: один debug-вывод при построении материалов — `N optional painterly textures
   unresolved` + список (сейчас это ровно `rowan_berries_v1_albedo.png`, `wattle_weave_v1_albedo.png`;
   `frost_window` поставлен 03.10).
2. **`terrain.v1.json`.** Runtime читает `res://content/world/terrain.v1.json`, файла нет; Studio
   сохраняет по `game/content/world/terrain.v1.json`. Предложение: одна проверка
   `FileAccess.FileExists` с однократным предупреждением, либо удалить мёртвую константу и запись
   в Studio.
3. **Озвучка пролога.** `PrologueVoice` молча возвращает 0 — по дизайну (субтитры работают), но
   внешнему человеку невозможно из лога понять, что записей нет. Дёшево: debug-счётчик
   `prologue voice clips missing: N/M`.
4. **Аудио-каталог.** `AudioCueUi.Present` при `!ResourceLoader.Exists(url)` уходит в текст. Дёшево:
   one-time debug-предупреждение `audio asset missing: <assetId> <url>`.
5. **Шаги.** Нет счётчика, сколько вариантов реально загрузилось на surface. Дёшево: debug-строка
   при `LoadSurface`, если `variants.Count < 3`.
6. **Интерактивы vs compiled campaign.** Сейчас неверный id ловится только в момент клика
   (`RuntimeBridge.cs:551`). Все текущие id (generic-плоты: `talk.interactionId`, `pickup`,
   `interactionId` каталога) статически резолвятся — но это проверено руками этой аудитории.
   Дёшево: startup/debug-проход по `InteractionTarget`-мета и сверка с
   `CompiledCampaignRepository` до первого кадра.
7. **Динамические `res://`-пути.** Ни один host-гейт не проверяет пути, собранные интерполяцией
   (footsteps, манифест кампании). Дёшево: расширить `eng/verify-asset-registry.sh` или добавить
   маленький `eng/verify-asset-refs.py`: собрать литералы + известные шаблоны и потребовать
   существование файла и `.import`.
8. **Low-friction binder Нивы.** `VehicleFleet.BindLowFrictionSurfaces` при нуле владельцев молча
   возвращает `false` — на льду не будет заноса, никто не узнает. Debug-счётчик найденных
   поверхностей (`snowTrampleBlocked`) закрыл бы.
9. **Отсутствие пустого списка зимних фич** — предупреждение `MosqueCarpets`/`CivicSurface` есть,
   а `RuralPropMaterials` только ставит мету. Единый debug-отчёт «N surfaces without dedicated
   texture» был бы достаточен.

---

## 4. Что требует runtime (нельзя закрыть статикой)

- Какая из поз recovery (±6/±12 м) принимается для Нивы и как выглядит финальная стоянка; после
  правки spawn — что warning исчез и `VehicleSmokeTest` снова проходит первый блок (`:54-64`).
- Атрибуция exit-утечек: один debug-прогон с `--verbose` покажет конкретные ObjectDB/RID адреса;
  полезно также прогнать 60-минутную сессию и убедиться, что числа не растут.
- Реальная частота `Interaction … rejected` в человеческом прохождении (в смоуках это негативные
  проверки; в обычной игре ожидается на «ещё нельзя» дверях).
- Воспроизводимость Metal `timeout waiting for fence` на других хостах (сейчас только M1/M4).

---

## 5. Защитные предупреждения, которые не срабатывали ни разу (проверено по архиву логов)

Все эти сайты существуют и корректны, но в `docs/production/**/*.log` нет ни одного их срабатывания:
зонные отказы `Main.cs:120,128,134,168`; `authored-world` unknown catalogue/model/no-executor
(`AuthoredWorldDirector.cs:206,217,242`); greeting-pool и schedule-refusals
(`AuthoredWorldDirector.Schedules.cs:81,322`, `Greetings.cs:54`); `Ignoring interaction`
(`RuntimeBridge.cs:551`); pre-ready save/load (`RuntimeBridge.cs:170,285,331`); `Invalid saved
vehicle pose` (`VehicleFleet.cs:166`); `Vehicle state commit failed` (`:457`); `Alsu` placement
checks (`AlsuStreetWalkPresentation.cs:253,531`); `carry:` rejections (`CarryCoordinator.cs:365,550`);
`village-pa:` (326,489,504); `suspension-bridge-dynamics` (158,169,183); `mosque-sanctuary` (214);
`mosque-overlap-audit` budget/allowlist (101,562); `Civic handmade surface missing`
(`CivicSurfaceLibrary.cs:54` — все атласы есть); `Mosque prayer mat` (`MosqueCarpets.cs:163` — файл
поставлен); `PainterlyMaterialLibrary` shader-seam (`:366` — замена кода работает); прологовые
ошибки (`PrologueNivaRide.cs:125,414`, `PrologueForest.cs:38,700`, `FirstNight.cs:83`,
`IntroFlyover.cs:60`, `PrologueDriver.cs:43`); ошибки магазина/документов/бани/обуви
(`ShopUi.cs:102`, `DocumentImageReader.cs:136`, `Notebook.cs:18`, `Bathhouse.cs:423`,
`PlayerFootwear.cs:83`). Их надо **сохранить**: они охраняют инварианты, а не создают шум.

---

## 6. Топ-10 исправить (по риску)

1. **Нива: невалидная авторская стоянка.** `act1_vehicles.v1.json` spawn (-1.3, 17) пересекает
   `AgentB_TerrainCollision`; recovery маскирует это каждый сеанс и блокирует `VehicleSmokeTest`
   (`:59`, `performance_audit_2026-10-03.md:152-153`). Переавторить spawn по фактически валидной
   позе (или устранить перекрытие под стоянкой) и требовать `rejectedPlacementProbe` отсутствующим.
2. **Реестр ассетов: 6 устаревших записей + запись `[0]` с прозой в `derived`.** Перегенерировать
   хеши из фактических байтов (или перезапустить генераторы и обновить записи), для `[0]` —
   либо файловый `derived`, либо разрешить прозу и в `derived`, как уже разрешено в `source`.
   Пока падает — `verify-assets`/`rebuild-assets` красные.
3. **`VehicleSmokeTest` снова должен проходить первый блок** — это приёмка для п.1, а не отдельная
   правка.
4. **Exit-утечки: атрибуция и очистка статических кэшей** (список в §2.2). Как минимум —
   `ClearCaches()` и заморозка чисел в этом документе как baseline «8 errors / 48 resources / 53
   ObjectDB / RID 13+13+6+2».
5. **Понизить `Interaction … rejected` до Print/debug** (`RuntimeBridge.cs:1358`): легитимные
   отказы условий сейчас пишутся как warning и маскируют настоящие ошибки контента.
6. **Диагностика опциональных текстур/аудио** (пункты 1, 3, 4, 5 списка молчания): один debug-отчёт
   на сеанс вместо тишины.
7. **Решить судьбу `terrain.v1.json`** (guard или удалить константу/Studio-путь).
8. **Формулировки info-строк**: `act1-street-frontage: rejected …` и `act1-gorge-clear: large mesh
   spans the gorge` переименовать в `act1-*-info:`/печатать «skipped by design», чтобы аудит не
   принимал их за дефекты.
9. **Startup-guard интерактивов** (молчание №6): сверка id до первого кадра, чтобы `RuntimeBridge:551`
   не был единственной линией обороны.
10. **Контент-гейт динамических путей** (молчание №7): маленький `eng/verify-asset-refs.py` или шаг
    в registry-гейте.

## 7. Допустимый шум — задокументировать как ожидаемый

- **Exit-блок движка** (пп. 72–99 `village_life/native.log`): baseline, не растёт, игрока не
  касается. Точные числа — в §2.2; любые отклонения от них считать новым сигналом.
- **`timeout waiting for fence`** (Metal): host-драйвер, 5 записей в 5 native-логах; не дефект игры.
- **`act1-overlap-cleanup: hidden=4` / `legacyRelocated=0 authoredOwners=12`,
  `act1-yard-composition: hidden=28`, `act1-open-part-clear hiddenMeshes=793…`,
  `act1-gorge-clear hidden=606…`**: ожидаемые информационные строки с известными числами; сверять
  волну при цитировании.
- **`act1-school-bank-cut: … no cut`**, `act1-terrain-room-cut …`, `village-households … budgets …`,
  `authored-world: 127 objects …` — диагностика сборки мира, не ошибки.
- **Негативные смоук-тесты** (`SaveGameV3 … failed`, `Interaction … rejected`,
  `vehicle-12.log` fixture-строки) — это намеренные проверки отказа; в обычной игре не ожидаются.

---

*Все ссылки на логи даны на репозиторные пути; хеши реестра воспроизводятся командой
`sh eng/verify-asset-registry.sh` (read-only) в состоянии дерева на 2026-10-05. Игра не
запускалась; пункты «needs runtime» остаются открытыми.*
