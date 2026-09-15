# LEN01 — аудит причин короткого прохождения Акта I

Дата: 2026-09-15. Срез: `main` после `6243fcb`, дерево содержало только незакоммиченные правки автора
в `SHURALE_RESEARCH_PACKAGE/`. Аудит выполнен отдельным read-only исследователем по §13.19 и проверен
основным исполнителем на загрузочных утверждениях.

**Граница проверки.** В этой среде нельзя играть (координатный ввод перехватывает системное окно,
AX-дерево Godot не отвечает). Все длительности ниже **выведены из фактов репозитория** (расстояние ÷
скорость ходьбы, авторский объём текста, удерживаемая длительность реплик), а не наблюдены. Живое
первое прохождение остаётся `external/not-run`.

## LEN01.1 — что именно было пройдено за 5–6 минут

Сообщение автора **воспроизводится как содержание**, а не как дефект сборки или запуска.

| Блок | Реальные события | Выведенное время |
|---|---|---|
| Старт | «Новая игра» → `RuntimeBridge.StartNewGameAsync` (`game/scripts/RuntimeBridge.cs:187`) → entrypoint `scene/arrival_vehicle_dusk` → `InitializeEntrypointAsync` применяет `onEnter`. Вступление пропускается Enter. | ~10–20 с |
| Ходьба | 424,88 м при `WalkSpeed = 3.4f` (`game/scripts/FirstPersonController.cs:19`) | ≈ 2:05 |
| 5 разговоров | `gulsina_yaramyy/home-warning`, `alsu_route_context/name-road`, `mansur_pc_request/ask-for-help`, `naila_medical_record/official-wording`+`wording-detail`, `rinat_internal_register/dangerous-category`+`javap-interrupt` = **137 слов** | ~50 с |
| 3 сопоставления в журнале | 3 нажатия; подписи кнопок сами называют вывод | ~12 с |
| 5 документов ПК | открытие `doc_marat_official_death_notice` (36 слов), `rec_marat_case_register_conflict` (30), `msg_marat_saved_last_normal` (63), `tw_shurale_urman_boundary` (81), `doc_kara_urman_edge_sketch` (56) = 266 слов. Открытие применяет `knowledgeRefs` и `openEffects` (`RuntimeBridge.cs:1079-1106`); чтение не требуется | ~1 мин даже с чтением |
| Финал | `scene/forest` `onEnter` ставит `audio-marat-voice` и `audio-rinat-interruption`; текстовая реплика удерживается `max(2,0 с, длина/18)` (`AudioCueUi.cs`) ≈ 2,06 + 2,0 с; затем `EvaluateEndingState` → `ShowEnding` | ~4,1 с |

Обязательный читаемый текст на несущем пути — **~530 слов** (137 диалога + 266 тела документов +
42 фото приезда + подписи). При спокойных 180 сл/мин это ≈ 3 мин плюс ≈ 2 мин ходьбы = **≈ 5 минут**
плюс переходы меню. Заявленные 5–6 минут согласуются с авторским объёмом контента; **это не признак
отладочного старта и не признак старого пакета**.

## LEN01.2 — матрица «сценарий → контент → действие → результат»

`auto` = выдано `onEnter`/открытием документа, а не осмотром; `mand` = обязательно для финала; `opt` = необязательно.

| # | Сценарная опора | Реальные ID | Статус | Доказательство | Время |
|---|---|---|---|---|---|
| 1 | Приезд и память Марата | `scene/arrival_vehicle_dusk` onEnter; `document/arrival-photo-evidence`; `interaction/arrival-enter-house`; `discover-arrival-bench-race-notches`, `discover-arrival-insulated-well` | приезд **auto**; фото **opt**; две находки **opt** и не требуются | `definitions.json` arrival `onEnter`; `documents/arrival-photo-evidence.md`; `Act1ConnectedWorld.ExteriorDiscoveries.cs:80` | ~10–20 с |
| 2 | Мансур и Гөлсинә | `house` onEnter; `talk-mansur` → `mansur_pc_request/ask-for-help`; `talk-gulsina` → `gulsina_yaramyy/home-warning`; `house-to-route` | **mand** оба (выход из дома требует `clue_marat_official_death_version` + `warning_heard`); дом `onEnter` дополнительно подтверждает `clue_family_avoids_marat` **auto** | effects узлов; условия `house-to-route` | ~40 с |
| 3 | Алсу и деревенские версии | `talk-alsu` → `alsu_route_context/name-road`; `route-to-fap` | **mand**; `name-road` подтверждает `clue_marat_versions_conflict` — единственное, что открывает `route-to-fap` | effects узла; условия `route-to-fap` | ~15 с |
| 4 | Старый ПК и официальная версия | `interaction/oldpc-power`; `doc_marat_official_death_notice`, `rec_marat_case_register_conflict`; `fap-document-desk-to-official-record`, `official-to-internal-register` | **mand**, но **auto**: открытие подтверждает `clue_marat_official_death_version` / `clue_marat_case_boundary_marker` | `RuntimeBridge.cs:1079-1106`; `onEnter` сцен `evidence-*` | ~30 с |
| 5 | ФАП, Наиля, Ринат, внутренний реестр | `talk-naila` → `naila_medical_record` (`wording-detail`/`transfer-detail`); `fap-to-document-desk`; `rinat_internal_register/dangerous-category` | ФАП/Наиля **mand** (`accessConditions` реестра требуют `naila.record_access_granted`); Ринат **mand** (`dangerous-category` ставит `rinat.alerted` + `beat/rinat-alerted-route-causality`) | effects узлов; `rec_marat_case_register_conflict.md` | ~50 с |
| 6 | Татарвики, перечитывание, Тимур | `msg_marat_saved_last_normal`, `tw_shurale_urman_boundary`, `doc_kara_urman_edge_sketch`; `route-to-mosque` → `timur_restraint` | цепочка документов **mand**; Тимур **opt** и ничего не гейтит | `accessConditions`/`openEffects` | ~40 с |
| 7 | Вечерняя дорога и кромка | `edge-sketch-to-zirat-road`; `scene/zirat-road` onEnter; `zirat-roadside-clue`; `compare-route-match`; `zirat-road-to-forest`; `forest-approach-to-forest` | **mand**; два **auto** при входе (`route_kara_urman_edge_hint`, `beat/rinat-visible-before-edge`) | `onEnter`; условия переходов | ~25 с |
| 8 | «Не отвечай» | `scene/forest` onEnter `audio.request` ×2; `forest-rinat-intervention`; `EvaluateEndingState`; `ShowEnding` | **mand**; правило подтверждается не действием игрока, а стартом реплики: `OnAudioCueStarted` → `CommitRinatIntervention` (`RuntimeBridge.cs:1229-1240`) | `onEnter` + interaction | ~6–8 с |

Три цикла расследования существуют и обязательны, но каждый — одно нажатие кнопки, подпись которой
уже содержит вывод. Проверено непосредственно в `definitions.json`:

- `text/compare-records-contradiction` = «У одного дела две разные формулировки.»
- `text/compare-voice-link` = «Голос, ответ и граница связаны. Нужно перечитать источник.»
- `text/compare-route-match` = «Канава и две засечки совпадают. Проверю тропу снаружи.»

Ошибочные гипотезы (`compare-records-accident`/`-murder`, `compare-voice-echo`/`-creature`,
`compare-route-center`/`-marat-proof`) имеют `effects: []`: они ничего не стоят, нигде не записываются
и повторяются без ограничения. Название квеста не доказывает расследования, и здесь не доказывает.

## LEN01.5 — гипотезы сокращения

| Гипотеза | Итог | Файл + ID | Влияние на потерянный опыт |
|---|---|---|---|
| Короткий/старый пакет, сохранение или debug-start | **опровергнута на этом пути** | `game/content/urman.chapter1.compiled.v1.json` против `content/modules/urman-chapter1/definitions.json` | авторский и скомпилированный контент структурно совпадают (19/19 сцен); debug-start не даёт битов и не доводит до финала. Причина — объём контента, не пакет |
| Части сценария остались только в документации | **подтверждена частично** | `content/campaigns/urman.chapter1/campaign.json` `narrativeOrder` | манифест ссылался на несуществующий `beat/rinat_do_not_answer`; обязательных *сцен* не потеряно. **Исправлено — см. ниже** |
| Знания и выводы выдаются автоматически | **подтверждена** | `evidence-internal-register` `onEnter`; `RuntimeBridge.cs:1079-1106`; `HandleOldPcOpen`; `knowledgeRefs` документов | вход в сцену или открытие документа подтверждает улику без осмотра; `DocumentUi`/`OldPcUi` не имеют состояния прочтения. Содержательный шаг (выбрать и открыть нужный документ) существует, а чтение не требуется. §13.19 прямо запрещает *запрещать* быстрое закрытие текста, поэтому это не дефект цепочки, а следствие объёма |
| До финала есть обход | **не найден (опровергнута)** | `entryConditions` `scene/forest` и `scene/forest-approach`; условия `zirat-road-to-forest`; `IsInteractionAvailable` (`RuntimeBridge.cs:403-404`) | обе сцены требуют `route_kara_urman_edge_hint` + `rinat.alerted` + `beat/rinat-visible-before-edge`. Физическая близость к Кара-Урману не входит: единственные входы — взаимодействия, принадлежащие сценам (`Act1ConnectedWorld.KaraOptionalDiscoveries.cs:474`, `StyleBenchmarkZone.cs:1704`) |
| Основной путь — серия коротких кликов | **подтверждена** | `JournalUi.cs` сравнения; подписи `text/compare-*`; ошибочные варианты с `effects: []` | игрок почти нигде не выбирает источник, вопрос или вывод: три выбора, каждый подписан собственным выводом |
| Расчёт включал необязательные документы и секреты | **опровергнута как причина** | 24 `discover-*` + 7 физических обходов, все необязательны | обязательное ядро (~530 слов + 424,88 м) само по себе ≈ 5 минут |
| Обещанная подача/реплики пропускаются | **подтверждена как отсутствующий ассет** | `content/modules/urman-chapter1/logical/audio-marat-voice.ref`, `audio-rinat-interruption.ref`; `AudioCueUi.PresentNextCue` | под `game/assets/audio/` нет голосовых файлов (только эмбиент, шаги, фоли). Финал подаётся двумя текстовыми репликами ~4,1 с. Внешняя работа, не причина короткого времени |
| Быстрый опытный проход сравнили с первым | **не проверена** (только человек) | — | контекста сеанса в репозитории нет; записано как неизвестное, не как оправдание |

## LEN01.3 — обязательные предпосылки финала

Каждая строка — действие, которым предпосылка становится истинной. Полный список из 21 шага сведён к
несущим:

1. `scene/arrival_vehicle_dusk` — авто при «Новой игре».
2. `scene/house` — `interaction/arrival-enter-house`.
3. `npc/mansur.pc_access_granted` — узел `mansur_pc_request/ask-for-help`.
4. `npc/gulsina.warning_heard` — узел `gulsina_yaramyy/home-warning`.
5. `clue_marat_official_death_version` — открытие `doc_marat_official_death_notice`.
6. `scene/crossroad_signs_inspect` — `interaction/house-to-route` (требует 5 и 4).
7. `clue_marat_versions_conflict` — узел `alsu_route_context/name-road`.
8. `scene/fap_waiting_room_day` — `interaction/route-to-fap` (требует 7).
9. `npc/naila.record_access_granted` — узел `naila_medical_record/wording-detail` или `transfer-detail`.
10. `clue_marat_case_boundary_marker` — открытие `rec_marat_case_register_conflict`.
11. `contradiction_marat_official_vs_internal` — сравнение в журнале `compare-records-contradiction`.
12. `npc/rinat.alerted` + `beat/rinat-alerted-route-causality` — узел `rinat_internal_register/dangerous-category`.
13. `clue_marat_was_afraid_before_death` — открытие `msg_marat_saved_last_normal`.
14. `vocabulary/tt_javap` — сравнение `compare-voice-link`.
15. `beat/language-reread` — задача `quest_language_reread`, объектив `apply-words`.
16. `clue_kara_urman_edge_is_rule_boundary` — открытие `doc_kara_urman_edge_sketch`.
17. `clue_marat_last_route_near_zirat` — `compare-route-match` после `zirat-roadside-clue`.
18. `beat/rinat-visible-before-edge` + `route_kara_urman_edge_hint` — `scene/zirat-road` `onEnter`.
19. `scene/forest` — `forest-approach-to-forest` (условия = 17 + 12 + 18).
20. `clue_do_not_answer_rule` + `beat/cliffhanger-hard-cut` — `interaction/forest-rinat-intervention`,
    применяется автоматически при старте реплики `audio-rinat-interruption`.
21. Итоговый экран — `EvaluateEndingState` при активной `scene/forest` и завершённом клиффхэнгер-бите.

Изоляция новой игры чиста: `CreateNewSession` пересобирает ядро, способности, часы, RNG и планировщик,
и `ChapterOneFlowSmokeTest.cs:449-484` утверждает, что после второго прохождения не остаётся ни знаний
о находках, ни записей журнала, ни подачи, ни открытых калиток. Данных тестового прохода новая игра
не получает.

## Подтверждённые дефекты и что с ними сделано

1. **Объём обязательного текста ~10× ниже цели 45–60 минут.** 1 247 слов во всей главе против ~530 слов
   на несущем пути. Механикой 50 минут из этого корпуса не получить.
   **Статус: авторское решение.** Измеренная сторона передана в
   `act1_len01_1_beat_map_draft_2026-09-15.md`; текст пишет автор, исполнитель его не выдумывает.
2. **Манифест ссылался на несуществующий бит.** `content/campaigns/urman.chapter1/campaign.json`
   держал в `narrativeOrder` бит `urman.chapter1:beat/rinat_do_not_answer` (и тот же идентификатор в
   инварианте `reveal-not-before`), которого нет ни в одном модуле; активный бит — `cliffhanger-hard-cut`.
   **Исправлено:** манифест приведён к фактическому контенту, добавлена пропущенная `scene/forest-approach`,
   порядок инварианта сохранён; тот же дефект поправлен в `urman.fullgame`. Контент перекомпилирован,
   `chapter_one_flow_smoke_test` проходит.
3. **Выводы трёх сопоставлений напечатаны на кнопках.** Подписи `text/compare-records-contradiction`,
   `compare-voice-link`, `compare-route-match` формулируют сам вывод, а ошибочные варианты не имеют
   эффектов, не записываются и повторяются без ограничения.
   **Статус: подготовлено для автора.** Это редактура уже написанного текста и дизайна выбора, а не
   новый сюжетный факт; конкретный вариант изложен в отчёте и в `execution_backlog.json` (LEN01.6).
   Исполнитель не переписывает авторские реплики молча.
4. **Финал разрешается стартом реплики, а не действием игрока.** `OnAudioCueStarted` →
   `CommitRinatIntervention` (`RuntimeBridge.cs:1229-1240`) отправляет `forest-rinat-intervention`.
   **Проверено и оставлено как осознанный дизайн:** у этого взаимодействия нет физической цели в мире
   (`forest-rinat-intervention` не создаётся ни одним builder'ом), поэтому старт реплики — единственный
   достижимый путь; сцена объявлена `sceneType: "scripted"`, и её предусловия строгие, обхода нет.
   **Что остаётся риском:** у игрока нет собственного действия в последнем бите. Это вопрос постановки
   клиффхэнгера для автора, а не сломанное условие; менять его молча нельзя, потому что контракт
   закреплён `chapter_one_flow_smoke_test`.
5. **`zirat-roadside-clue` обязателен через случайность порядка выдачи.** `onEnter` сцены `zirat-road`
   выдаёт `route_kara_urman_edge_hint` и `beat/rinat-visible-before-edge` при входе, поэтому переход
   к лесу уже удовлетворяет своим условиям до чтения придорожной засечки.
   **Статус: подозрение, не подтверждено как обход** — сравнение `compare-route-match` всё равно
   обязательно, потому что только оно даёт `clue_marat_last_route_near_zirat`.
6. **Живые голосовые рефы не имеют файлов.** `logical/audio-marat-voice.ref` и
   `audio-rinat-interruption.ref` — обязательная внешняя запись (`eng/apply-act1-voice-recordings.sh`).

## Что остаётся человеку (external/not-run)

- Реальная длительность первого прохождения и то, что понял новый игрок. В этой среде не воспроизводится.
- Различение быстрого опытного прохода и первого; скриптовый маршрут и проход разработчика, знающего
  ответы, как замена не используются.
- Запись голосов и связанный с ней пересчёт финала.
- Решение автора по объёму обязательного текста и по дизайну трёх сопоставлений.
