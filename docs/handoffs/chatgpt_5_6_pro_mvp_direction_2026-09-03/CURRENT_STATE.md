# Текущее состояние УРМАНА

Срез: **2026-09-03**. Это аудит текущего working tree и supplied evidence, а не описание одного `HEAD` и не декларация готовности.

## Короткий вердикт

УРМАН сейчас — first-person Godot/C# вертикальный срез Act I с data-driven content, физически проходимым connected-world greybox и работающим narrative kernel. Он технически способен довести игрока от въезда до Kara-Urman и собрать доказательства маршрута, но ещё не является playable-and-beautiful MVP: визуальная среда остаётся неоднородной benchmark/greybox-сборкой, capture 360° провален из-за сериализации повторных видов, а human/cultural/audio/accessibility/platform release review не закрыты.

Главная ошибка, которую нельзя повторять: считать зелёные smoke/receipt и большое число mesh доказательством художественного качества. Текущая визуальная приёмка должна начинаться с исправления capture contract и затем пройти human first-person review всех eight visual zones.

## Что зафиксировано как текущее

| Область | Наблюдаемый факт | Интерпретация |
|---|---|---|
| Канон | Айдар возвращается в татарскую деревню Кырлай, разбирается с тайной Марата и старым порядком сосуществования людей и леса | Это камерный этно-хоррор/детектив, не generic monster game; источники — `URMAN_Codex_Context.md` и KB |
| Runtime | `game/scenes/main.tscn` содержит `Main`, `RuntimeBridge`, player, UI и ambience; `Act1DemoRoot` включает connected-world режим для Act I | Базовая сцена собрана; состояние и презентация должны оставаться разделены |
| Логическая карта | `Act1WorldLayout.cs` задаёт пять logical zones и семь connector placement; `Act1ConnectedWorld.cs` держит их в одном persistent world | Маршрутные поверхности и презентационные объекты существуют, но это ещё не art lock |
| Контент | `campaign.json`, module, definitions и compiled pack задают entrypoint, narrative order, physical gates, vocabulary и invariant о `Не отвечай` | Data-driven каркас есть; прохождение целиком и качество подачи не доказаны этим аудитом |
| Build | Родитель независимо проверил `dotnet build Urman.slnx --no-restore`: 0 warnings, 0 errors | Это доказательство сборки на момент проверки, не proof of release |
| First-person smoke | Родитель независимо запустил настоящий headless Dummy-audio `Act1FirstPersonWalkthroughSmokeTest`: PASS, distance **135.49 m**, final zone `kara_urman_night`, cliffhanger completed | Проходимость и gate order технически наблюдались; это не human playtest и не visual wow |
| Capture receipt | `visual_evidence/current_act1_360_failed_gate/act1_full_route_core_world_receipt.json`: 44 frames, 8 visual zones, 10/10 waypoints, **242.49586 m**, root viewport=true, subviewport=false, capture_process_count=1, visual_mesh_count=11532, forbidden gameplay nodes=0 | Структурный envelope и маршрут зафиксированы |
| Capture integrity | Receipt hashes совпадают с 44 файлами, но только **25 уникальных SHA-256** | Integrity файлов PASS, coverage/360 gate FAIL; это текущий P0, а не успешная визуальная приёмка |

## Текущий capture-gate: что именно сломано

Свежий capture создан 2026-09-03 из точного каталога `/private/tmp/urman-gpt56-pro-current-capture-20260903/`; в архиве он сохранён под `visual_evidence/current_act1_360_failed_gate/`. Wrapper завершился с кодом 1, потому что направления, которые должны были дать разные views, сериализовали одинаковое изображение. Нельзя исправлять этот факт заменой на старый benchmark или удалением повторов.

Из supplied inspection известны группы повторов:

- 16 одинаковых файлов: `connective_street_return_{forward,back,depth}`, все пять `fap_exterior`, все четыре `fap_interior` и четыре направления `zirat` (depth `zirat_depth` имеет другой хэш);
- 5 одинаковых файлов: все направления `kara_approach`;
- остальные кадры дают 25 уникальных значений среди 44 файлов.

Следствие: для полного eight-zone acceptance нельзя считать прошедшими 360°/lateral review ни `ConnectiveStreetReturn`, ни FAP exterior/interior, ни `ZiratMemoryField`, ни `KaraForestEdge`. Даже уникальный `zirat_depth` не спасает неполный набор. Уникальность хэша сама по себе тоже не заменяет визуальный человеческий осмотр.

## Что демонстрационно работает

По коду и supplied verification видно следующее:

1. Runtime загружает compiled content pack; `RuntimeBridge` владеет narrative/progression, old PC capabilities, save snapshot и world-location state.
2. Physical interaction targets dispatch через `RuntimeBridge`, а UI получает результат; `InteractionTarget` не становится вторым narrative owner.
3. Connected Act I размещает `village_day`, `house_old_pc`, `fap_clinic`, `zirat_road`, `kara_urman_night` в едином world-space envelope, оставляя route collision в явно объявленном exterior layer.
4. Пройден технический first-person маршрут от arrival до Kara cliffhanger; smoke проверяет реальные движения, ray/input и порядок gates, а не только вызов методов.
5. Old PC, dialogue, persistence, settings и audio имеют отдельные smoke-contract источники в bundle. Их наличие не означает, что первые игроки прошли их без подсказок или что тексты/микс приняты.
6. Capture запускает production main scene, использует реальную player Camera3D и root viewport и не мутирует narrative/save; поэтому он полезен для presentation evidence, но не доказывает gameplay completion.

## Что является benchmark/greybox

`Act1CoreWorldGreybox` и большая часть процедурной композиции — presentation layer для проверки непрерывности, масштаба, маршрута и near/mid/far намерения. Target PNG в `docs/urman_knowledge_base/art/style_refs/` — визуальная цель, не screenshot текущего результата. Присутствующие candidate kit GLB в working tree — авторские исходники/кандидаты; в bundle включены только их manifests.

Честная визуальная оценка:

- зират всё ещё читается плоским и процедурным, с резким горизонтом и крупными повторяющимися деревьями;
- Kara остаётся разреженной и примитивной по боковым направлениям;
- целостность авторской деревни, иерархия near/mid/far, обратные views, контакт с рельефом и финальный production look не достигнуты;
- текущий failed capture дополнительно лишает Pro достоверной поздней 360°-проверки.

Это формирует visual blocker для требования «красивый MVP», даже когда техническая traversal receipt зелёная.

## Недавно принято и оставлено в текущем дереве

Родитель сообщил о следующих bounded improvements, которые следует считать текущим working-tree состоянием до отдельной проверки кадров:

- пять минимальных Kara transforms уменьшают доминирующую близкую crown-массу;
- в зирате существующие authored distant-village masses сдвинуты west/east в midground;
- уменьшены точные cross-zone crown owners `ReturnWestConifer` и `FapOppositeFieldNeighborConifer`.

Это локальные улучшения композиции, не разрешение failed capture и не общий art lock.

## Отклонённые или no-op эксперименты

Следующие попытки не являются текущим продуктом и не должны возвращаться в план как готовые решения:

- broad Kara composition была reverted;
- две попытки с zirat asset/horizon были reverted;
- placement zirat forest-wing был reverted.

Подробности и старые числа Agent B находятся в `docs/experiments/agent_b_act1_world_report.md`; документ включён как non-canonical experiment evidence, а не как source of truth.

## Что ещё не подтверждено

- Исправление причины duplicate serialized views и новый валидный 360°/near-mid-far capture всех eight zones.
- Human first-time playthrough: понятность маршрута без marker-зависимости, доступность old PC, language clue loop и эмоциональная работа клиффхэнгера.
- Финальные art, lighting, fog, materials, foliage, authored props, rain/wetness, horizon и cross-zone coherence.
- Production audio: voice performance, mix, quiet-space dynamics, localization/caption quality и комфортные переходы.
- Татарская, культурная и религиозная консультация; корректность употребления языка и роли Тимура хәзрәтә.
- Accessibility и motion comfort: FOV, sensitivity, subtitles/readability, flashing/contrast, nausea-safe motion.
- M1/Windows и другие target-platform performance budgets; локальная сборка не равна cross-platform release verification.
- Реальное save/restart/recovery поведение в пользовательском сеансе; source/tests описывают контракт, но этот handoff их не запускал.
- Onboarding, settings discovery, packaging, distribution и human/public demo gates.

Есть orchestration-status `blocked` у активной goal записи. Это статус процесса координации, а не доказательство того, что репозиторий или runtime не запускаются.

## Граница текущего MVP

В этом handoff рассматривается только Act I: приезд, дом бабая и әби, социальное напряжение, Alsu, противоречивые следы Марата, old PC/архив, первый татарский слой, первый сильный мистический след и cliffhanger `Не отвечай`. Acts II–V, полный pact reveal, combat, open world и полная упаковка не должны маскировать незакрытые Act I gates.
