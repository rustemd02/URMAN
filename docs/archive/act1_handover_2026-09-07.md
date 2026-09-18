# Act I Handover — 2026-09-07 (для следующей модели-исполнителя)

Исторический срез: после него выполнен зимний визуальный проход.
Текущее продуктовое поручение — [хэндовер законченного Акта I от 11 сентября](../production/URMAN_ACT_I_FINISHED_PRODUCT_HANDOVER_RU.md).
Не принимать прежние captures, ограничения объёма и состояние ассетов за свежую приёмку.

Назначение: полная передача состояния после запуска, закрывшего шесть
визуальных slice'ов, три verification-семейства и финальный осмотр всех
кадров. Читай вместе с `AGENTS.md`,
`URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (раздел 0) и
`docs/production/act1_opencode_execution_state.md` (карточки slice'ов
вверху файла — первичный источник фактов этого запуска).

## 1. Состояние

- Ветка `main`, HEAD `99e041359f8f27db8f1e27d0c5c0b27e12201858`. Рабочее
  дерево содержит большой незакоммиченный диф прошлых сессий ПОВЕРХ HEAD —
  это пользовательская работа. Не откатывай, не коммить его целиком, не
  смешивай со своими правками. Коммиты/пуш в этом запуске не делались;
  без явного разрешения пользователя — не делай.
- Act I физически проходим: walkthrough PASS 335,27 м до клиффхэнгера
  «НЕ ОТВЕЧАЙ» (реальный CharacterBody, без телепортов), checkpoint
  save/resume работают. Команда:
  `. eng/dotnet-env.sh && .tools/godot/Godot_mono.app/Contents/MacOS/Godot --headless --audio-driver Dummy --path game res://tests/act1_first_person_walkthrough_smoke_test.tscn`
- Сборка: `. eng/dotnet-env.sh && .tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity quiet -nodeReuse:false -m:1` → 0/0.
- Полный capture: `./eng/capture-act1-full-route-core-world.sh <пустой
  каталог вне репо>` → PASS 48/48 уникальных кадров, 8 зон. Строка
  «expected 44 frame records, found 48» — информационная (кадры
  добавлялись к историческим 44), exit 0.
- Диск перед тяжёлыми запусками: `df -h .`. В этой сессии было 26 ГиБ →
  внешний провал до 472 МиБ → пользователь освободил (68-73 ГиБ). При
  <2 ГиБ не запускай capture/импорты (прецедент стопа 2026-09-05).

## 2. Что сделано в этом запуске (все IN_GAME_VERIFIED, evidence в репо)

1. **Babai yard**: миниатюрный parcel 0.44/0.48 заменён на хоздвор —
   `BabaiEastDepthServiceShed` (OutbuildingShed_Low 0.85, yaw -80) +
   woodpile + L-ограда (East x=-2.5 z 12.0..15.8; North z=15.8 x
   -6.2..-2.5), mount `BabaiEastDepthParcel` (-5.5,13.5). Западный/задний
   прогоны удалены умышленно: их края держат kit-fence и соседский забор
   z=16. Дорожка к дому (коннектор, полуширина 2.4 м) свободна.
2. **Материалы**: 34 записи в два словаря грейдинга
   (`RebindWetVillageRoadMaterials` — road-кит органика/fence/water;
   `RegradeAct1DaylightKitMaterials` — village kit Well_DarkWater/CutEnd/
   Bark_Metal + zirat-кит MossGreen/MossyStone/WeatheredWood(Dark)/
   DistantFence/DistantFoliage/DitchWater/WetSheen + FAP-кит
   FapBirch*/FapShrub*/FapVentDark). Причина дефекта доказана probe'ом:
   сырой GLB-albedo (Shrub_BlueGreen = минтовая масса у ограды).
3. **Фасады**: пять клонов DwellingFacade переведены на variant-dwellings
   (WestStreetMid→C, EastStreetMid→A с якорем (25.3,-19.2), WestReturnMid→B,
   EastReturnMid→C, EastStreetHorizon→A), scale 0.88-0.90. Правило в
   `AddAuthoredHouse` сужено до `Contains("FarHolding")` → VariantC.
4. **EastStreetFarHolding**: дом (35.4,1.0,-16.9) yaw 2° scale 0.85
   (веранда на восток), сарай полевой (43.8,-20.5), дрова у проёма
   (34.9,-14.4), ограды north (проём 37.0..38.6) и east x=41.6.
5. **Kara**: `KaraMossyBoulderCluster` сдвинут с обочины (local (9.5,-8.5),
   yaw -35, 0.95), камни/кусты на склонах вне 3.5-м конверта; западный
   камень-кандидат снят после собственного кадра (блин на открытом месте).
6. **Пересечения**: удалён `EastStreetSideClosureFacade` (пересекался с
   near-mid домом; parcel сохранён как двор с сараем/оградой) и
   `ZiratVillageEdgeWestFacade` (дубль ядра-дома
   ZiratVillageMemoryHouseWest).
7. **Verification**: CAPTURE-004 supplemental PASS 10/10
   (`eng/capture-act1-supplemental-directions.sh`, receipt
   `evidence/act1_repo_baseline/cap004-supplemental-receipt.json`);
   PERF-002 PASS ~120 FPS avg на M4 Pro, floor 30
   (`eng/benchmark-godot.sh`, TSV
   `docs/urman_knowledge_base/performance/godot_m4pro_baseline.tsv`);
   RELEASE-002/004 автоматическая часть PASS — act1-macos.pck +
   act1-windows.pck по 156 записей, headless bootstrap обеих целей
   (`eng/verify-act1-release-package.sh`; RC readiness остаётся OPEN).
8. Осмотр всех 48 кадров (включая интерьеры) — новых дефектов с
   владельцами в коде нет; записано в ledger.

Evidence-пары до/после: `evidence/act1_repo_baseline/{matfix,sliceb,slicec,sliced,sliceE,slicef}/`.
Финальные capture-наборы (эфемерные): `/private/tmp/urman-sliceF.Foesg1`
(48 кадров), `/private/tmp/urman-cap004.M5zToJ` (10 кадров).

## 3. Изменённые файлы этого запуска (мои маркеры для поиска)

- `game/scripts/Act1ConnectedWorld.cs`: блоки `BabaiEastDepthService*`,
  `BabaiEastDepthServiceBoundary*`, записи материалов (ищи
  `Shrub_BlueGreen`, `URMAN_Well_DarkWater`, `FapVentDark`),
  `Contains("FarHolding")`, блок `EastStreetFarHolding*`, пять
  variant-facade placements, комментарий Review-corrected у Kara-кластера,
  отсутствие `ZiratVillageEdgeWestFacade` и `EastStreetSideClosureFacade`.
- `game/tests/Act1FullRouteCoreWorldCapture.cs`: временный probe/hide-test
  добавлялись и полностью удалены — net-изменений от меня нет (файл остался
  M от чужого дифа).
- Документы: `docs/production/act1_opencode_execution_state.md` (карточки),
  `docs/urman_knowledge_base/weak_points.md`, `assets.md`,
  `docs/urman_knowledge_base/performance/godot_m4pro_baseline.tsv`,
  `URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md` (Babai-row +
  датированная verification-запись), evidence-каталоги. Этот файл.

## 4. Технические факты и грабли (проверено на этом запуске)

- `AddAuthoredHouse` форсирует scale ≥0.90 (`Mathf.Max`) — «миниатюрных»
  авторских домов НЕТ; вызовы с 0.32-0.76 декларативны.
- AABB вариант-домов асимметричны (веранда на local +X):
  VariantA 8.83×6.62 (X -3.72..+5.12), VariantB 8.33×7.22 (X -3.47..+4.87,
  Z -5.51..+1.71), VariantC 8.63×6.32 (X -3.62..+5.01). Считай world-extent
  по углам с yaw, а не «±половина ширины» — на этом ошиблись дважды
  (holding, EastStreetMid).
- Материал-грейдинг: словари матчят `Mesh.SurfaceGetMaterial().ResourceName`;
  отсутствие записи = сырой albedo (белый/минтовый). Имена семантические
  сохраняются при Duplicate() — клоны наследуют биндинг.
- Hero-окно (`ApplyHeroWarmWindow`) и `AddBabaiRearFacadeDressing`
  привязаны к точному имени `DwellingFacade_TimberPlaster` — при переводе
  placement на variant-dwelling тыльный декор пропадает (это ок, варианты
  полные). `ArrivalForwardWestFacade` обязан остаться фасадом.
- Suppression-списки (core blockout/faceted mass) — точные пути; новые имена
  узлов под них не попадают. Zonal name-паттерны действуют только внутри
  инстансов логических зон.
- Python-правки файла: ВСЕГДА assert на совпадение старого текста. Одна
  без-assert замена дала silent no-op (slice C), который вскрылся только
  ревью.
- Capture-харнесс: кадры добавляются в `Frames` в
  `Act1FullRouteCoreWorldCapture.cs`; уникальность SHA гейтится; камера
  должна совпадать с рендером (aim deviation ≤0.5°).
- Тест-гигиена: `verify-godot.sh` сам чистит settings.json/audio-settings.json
  перед прогоном; после benchmark убивай MSBuild-воркер (nodeReuse).
- /tmp хост чистит сам — важные кадры сразу копируй в
  `evidence/act1_repo_baseline/<slice>/`.

## 5. Что осталось (очередь для продолжения)

Автоматизируемых именованных дефектов с владельцем в коде на момент
передачи НЕТ (проверено осмотром 48 кадров + ревью субагента). Дальше по
убыванию ценности:

1. **Human-гейты (BLOCKED_EXTERNAL, главный приоритет)**: 360° art review
   зон (Z01-Z008 readiness), культурная/религиозная экспертиза зирата
   (CULTURE-003), татарская языковая редактура, observed playtest
   (PLAYTEST-001..006), AUDIO-011..014 (authored voice/mix), M1/Windows
   performance на целевом железе (PERF-003..005), release sign-off
   (RELEASE-006..010). Evidence-пакеты подготовлены.
2. **Код, если human-фидбек даст конкретику**: near-клоны фасадов
   (Arrival-группа держит hero-окно — трогать только с сохранением
   `ApplyHeroWarmWindow`; side-closure уже разряжен); whole-zone light/
   exposure (Z0x-007) — только по замечаниям art review; ART-012 NPC
   силуэты/одежда (нужен cultural ok).
3. **Не делать**: новые пропсы без дефекта-владельца (prop-spam запрещён
   трекером), brightness-пассы вместо геометрии, вторых источников
   состояния/маршрута/сохранений, коммиты чужого дифа, push/PR.

## 6. Правила работы (сокращённо, полные — в tracker разделе 0)

Один writer, canonical checkout, ветка main. Каждый slice: выбрать дефект с
владельцем → правка минимальная в canonical owner → build → walkthrough →
capture (только после causal fix, в свежий пустой каталог) → осмотр кадров
собственными глазами → evidence-пара до/после → карточка в ledger →
graphify update → git diff --check. Статус IN_GAME_VERIFIED только с
просмотренными кадрами; art gate не закрывать автоматикой. «До»-состояние
воспроизводимо из кода — при отклонении кандидата откатывай правку, а не
снимай «удачный» кадр.
