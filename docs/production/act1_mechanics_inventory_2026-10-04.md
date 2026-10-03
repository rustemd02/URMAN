# Инвентаризация интерактивов Акта I — 2026-10-04

Основание: карточка `ACT1-MECH-AUDIT.1` (шаг `.2` — её предметное продолжение) и разделы
**M1–M2** ТЗ [06_ui_audio_mechanics_performance.md](../tasktracker/review_2026-09-28/06_ui_audio_mechanics_performance.md).
Это документальный результат: игровой прогон не выполнялся, приёмка интереса остаётся `not-run`.

**Метод.** Источник — только исходные модули `content/modules/**` (14 файлов); производные
`game/content/*.compiled*.json` не считаются источником. Для каждой строки собраны: ID, модуль,
условия (`knowledgeId`/`textId`/`npcId`/`questId`, то есть что уже должно быть известно),
результат (эффекты `knowledge.set-status`, `journal.record`, `npc.set-state`,
`vocabulary.set-status`, `beat.set-state`), класс и повторяемость. Дополнительно проверено,
на что ссылается каждая цель знания.

## Сводка

- **156** уникальных исходных interaction ID в 14 модулях: `definitions.json` 64 (мир Акта I),
  `urman-fullgame/definitions.json` 23 (Акты II–V — вне объёма Акта I), остальные 69 — тематические
  модули Акта I (`village-life`, `exploration-*`, `investigation-*`, `tamara-fence`,
  `arrival-personal-sources`, `rinat-roadside-presence`).
- **33** несут знание из реестра `urman.chapter1:knowledge/*` (kinds: fact 22, clue 10,
  hypothesis 5, contradiction 1, route 8, pressure 1) → предлагается `keep`.
- **47** несут наблюдение или запись вне реестра → по M2 `fix`: показать, зачем предмет
  трогают, или перестать делать его интерактивным.
- **76** не имеют **никакого** эффекта (ни знания, ни журнала, ни beat/quest) → это и есть
  класс «действие ради кнопки» из M1: каждой строке нужно ясное назначение либо поздний
  payoff, иначе удаление.
- **Ни одна цель знания не «мертва»**: все 69 knowledge-целей, которые ставят интерактивы,
  на что-то ссылаются в исходных модулях (условия, журнал, квесты, сцены) — проверено
  подсчётом ссылок. Сирот, которые можно удалить без последствий, в этом классе нет.
- Класс «без результата» **по построению не влияет на save/квест/EX**: он не меняет ни
  knowledge, ни journal, ни beat/quest/npc. Поэтому удаление такой строки не может сломать
  обязательный факт или действие, и замена по `done_when` для этого класса не требуется.
- Плотные классы (3+ применений) подтверждаются данными: `compare-*` 35 (журнал и
  сопоставления), `discover-*` 29, `observe-*` 7, `talk-*` 5, `revise-*` 4, `excerpt-*` 4.
  Одиночные: `arrival-*`, `zirat-*`, `school-*`, `council-*`, `view-*`, `act2-4-*`.
- Оценка автора «90 %» этим документом **не подтверждается как доля**: измеренная доля
  строк без результата — 76 из 156 (48,7 %), из них 23 относятся к Актам II–V и в объём
  Акта I не входят; для Акта I это 76 из 133 (57 %). Это сигнал проблемы в терминах M1,
  а не приказ удалить половину.

## Именованные случаи M2 — точные ID

| Случай из ТЗ | ID в исходниках | Текущее состояние по данным | Варианты M2 | Решение |
|---|---|---|---|---|
| Z13, вращающаяся «Фаб Юл» | `discover-babai-yard-childhood-spinner` | ставит знание + запись журнала + слово; отдельного физического эффекта у строки нет | 1) убрать interactable; 2) дать понятный физический эффект/ограничение; 3) оставить ранним намёком с поздним payoff | **ждёт автора** (2–3 спорных решения пилота) |
| ФАП/D13: кружка у входа и D13-предметы | отдельного ID кружки в исходных модулях **нет**; ФАП-интерактивы — `discover-fap-*`, из них `fap-document-desk-to-official-record` | часть строк ставит знание из реестра | показывать, зачем предмет трогают, либо снять интерактивность; вход/сопоставление фактов — ТЗ 02 | `fix`; отсутствие ID кружки уточнить у владельца сцены ФАП |
| Колодец/сарай: варежка и щеколда | `observe-well-tie`, `discover-arrival-insulated-well`, `upper-latch`, `discover-connective-street-shed-bypass` | `observe-well-tie` ставит знание и запись журнала (варежка), `upper-latch` — отдельная строка | варежка и щеколда не должны автоматически выдавать очередную необязательную записку; бытовая функция вещи может остаться | `fix` |
| Z18/И22: лавка с держателем, мостик/смахивание снега | `discover-zirat-outer-rest-bench`, `discover-connective-street-return-bench`, `discover-connective-street-repair-bench` | знание + запись журнала | развернуть/убрать лавку; мостик/смахивание связать с настоящим проходом или повторяемым правилом либо снять ложную цель; перемещение — ТЗ 03 | `fix`/`remove` по месту |

Проверка полноты: все перечисленные в `done_when` имена либо найдены как конкретные ID
(варежка, щеколда, лавки, «Фаб Юл», колодец, сарай), либо, как кружка ФАП, **отсутствуют**
в исходных модулях — это отдельный факт для владельца сцены, а не пропуск инвентаризации.

## Таблица A — без игрового результата (76 строк, класс M1)

| ID | модуль | условия (что открывает) | результат | классификация |
|---|---|---|---|---|
| `arrival-answer-mother` | arrival-personal-sources.json | arrival_mother_message_read, arrival_reply_help_babai, arrival_reply_kept_silent, memory_marat_childhood_photo | — | без результата |
| `view-arrival-message` | arrival-personal-sources.json | arrival_mother_message_read, memory_marat_childhood_photo, memory_marat_kazansky_ne_otstavay | — | без результата |
| `view-arrival-photo` | arrival-personal-sources.json | — | — | без результата |
| `act2-alsu` | definitions.json | — | — | без результата |
| `act2-council-to-archive` | definitions.json | — | — | без результата |
| `act2-council-voice` | definitions.json | — | — | без результата |
| `act2-house-to-river` | definitions.json | — | — | без результата |
| `act2-mosque-to-council` | definitions.json | — | — | без результата |
| `act2-river-to-mosque` | definitions.json | — | — | без результата |
| `act2-timur` | definitions.json | — | — | без результата |
| `act3-archive-to-soviet` | definitions.json | — | — | без результата |
| `act3-naila` | definitions.json | — | — | без результата |
| `act3-soviet-document` | definitions.json | — | — | без результата |
| `act3-soviet-to-water` | definitions.json | — | — | без результата |
| `act3-water-to-tukay` | definitions.json | — | — | без результата |
| `act3-water-voice` | definitions.json | — | — | без результата |
| `act4-1552-document` | definitions.json | — | — | без результата |
| `act4-1552-to-pact` | definitions.json | — | — | без результата |
| `act4-keeper` | definitions.json | — | — | без результата |
| `act4-pact-document` | definitions.json | — | — | без результата |
| `act4-pact-to-boundary` | definitions.json | — | — | без результата |
| `act4-tukay-document` | definitions.json | — | — | без результата |
| `act4-tukay-to-1552` | definitions.json | — | — | без результата |
| `act4-tukay-voice` | definitions.json | — | — | без результата |
| `act5-aidar-choice` | definitions.json | — | — | без результата |
| `compare-records-accident` | definitions.json | clue_record_wording_mismatch | — | без результата |
| `compare-records-murder` | definitions.json | clue_record_wording_mismatch | — | без результата |
| `compare-reread-author` | definitions.json | clue_internal_wording_reread | — | без результата |
| `compare-reread-compensation` | definitions.json | clue_internal_wording_reread | — | без результата |
| `edge-sketch-to-zirat-road` | definitions.json | clue_kara_urman_edge_is_rule_boundary, clue_route_check_discussed | — | без результата |
| `fap-document-desk-to-official-record` | definitions.json | — | — | без результата |
| `fap-to-document-desk` | definitions.json | — | — | без результата |
| `first-night-sleep` | definitions.json | alsu_walk_invitation, family_home_pause | — | без результата |
| `forest-approach-to-forest` | definitions.json | — | — | без результата |
| `house-to-route` | definitions.json | — | — | без результата |
| `internal-register-to-rinat` | definitions.json | clue_rinat_accounting_question_heard, clue_village_has_internal_compensation_system, contradiction_marat_official_vs_internal, route_kara_urman_edge_hint | — | без результата |
| `internal-register-to-saved-message` | definitions.json | contradiction_marat_official_vs_internal | — | без результата |
| `official-leave-clinic` | definitions.json | clue_marat_official_death_version | — | без результата |
| `official-to-internal-register` | definitions.json | clue_marat_official_death_version | — | без результата |
| `oldpc-power` | definitions.json | — | — | без результата |
| `reread-to-edge-sketch` | definitions.json | clue_mansur_allowed_pc_access_deliberately, clue_rinat_accounting_question_heard, clue_village_has_internal_compensation_system | — | без результата |
| `route-to-fap` | definitions.json | clue_marat_versions_conflict | — | без результата |
| `route-to-mosque` | definitions.json | clue_folklore_as_survival_rule | — | без результата |
| `saved-message-to-boundary-source` | definitions.json | clue_marat_was_afraid_before_death | — | без результата |
| `talk-alsu` | definitions.json | — | — | без результата |
| `talk-gulsina` | definitions.json | — | — | без результата |
| `talk-mansur` | definitions.json | — | — | без результата |
| `talk-naila` | definitions.json | — | — | без результата |
| `talk-rinat` | definitions.json | — | — | без результата |
| `village-sign` | definitions.json | — | — | без результата |
| `zirat-road-to-forest` | definitions.json | clue_marat_last_route_near_zirat | — | без результата |
| `compare-photo-neighbor` | exploration-observation-checks.json | — | — | без результата |
| `compare-sketch-minaret` | exploration-observation-checks.json | — | — | без результата |
| `view-first-snow-photo` | exploration-sources.json | discovery-house-interior-photo-back | — | без результата |
| `compare-family-call-erased` | investigation-family.json | clue_notice_call_not_request | — | без результата |
| `compare-route-purpose-alive` | investigation-route.json | clue_route_check_intent | — | без результата |
| `compare-route-purpose-summon` | investigation-route.json | clue_route_check_intent | — | без результата |
| `compare-versions-departure` | investigation-route.json | clue_marat_versions_conflict | — | без результата |
| `compare-versions-diagnoses` | investigation-route.json | clue_marat_versions_conflict | — | без результата |
| `compare-record-scope-cause` | investigation-social.json | clue_naila_record_scope, clue_record_wording_mismatch, contradiction_marat_official_vs_internal | — | без результата |
| `compare-record-scope-lie` | investigation-social.json | clue_naila_record_scope, clue_record_wording_mismatch, contradiction_marat_official_vs_internal | — | без результата |
| `compare-warning-agreement` | investigation-social.json | clue_yaramyy_contexts_distinguished | — | без результата |
| `compare-warning-rule` | investigation-social.json | clue_yaramyy_contexts_distinguished | — | без результата |
| `compare-accounting-same-case` | investigation-source-returns.json | clue_accounting_fragment_read, clue_marat_case_boundary_marker, clue_village_has_internal_compensation_system | — | без результата |
| `tamara-fence-hand-in` | tamara-fence.json | — | — | без результата |
| `arrival-stop-timetable` | village-life.json | — | — | без результата |
| `compare-sabirov-authorship` | village-life.json | sabirov-family-linked | — | без результата |
| `council-photo-album` | village-life.json | — | — | без результата |
| `council-sabantuy-poster` | village-life.json | — | — | без результата |
| `council-village-plan` | village-life.json | — | — | без результата |
| `school-class-photo` | village-life.json | — | — | без результата |
| `school-staff-note` | village-life.json | — | — | без результата |
| `school-transport-notice` | village-life.json | — | — | без результата |
| `shop-account-book` | village-life.json | — | — | без результата |
| `village-shop-greeting` | village-life.json | — | — | без результата |
| `zirat-family-links` | village-life.json | — | — | без результата |

## Таблица B — наблюдение/запись вне реестра (47 строк, класс M2 → fix)

| ID | модуль | условия (что открывает) | результат | классификация |
|---|---|---|---|---|
| `act5-boundary-to-epilogue` | definitions.json | act5_protection_lost | знание: act5_truth_price | наблюдение/запись вне реестра |
| `arrival-enter-house` | definitions.json | — | эффекты: beat.set-state | наблюдение/запись вне реестра |
| `boundary-source-to-reread` | definitions.json | clue_folklore_as_survival_rule, clue_voice_answer_is_dangerous_hint | эффекты: beat.set-state | наблюдение/запись вне реестра |
| `compare-records-contradiction` | definitions.json | clue_notice_cause_excerpt, clue_record_wording_mismatch, clue_register_wording_excerpt | знание: clue_record_wording_mismatch; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-route-center` | definitions.json | clue_marat_last_route_near_zirat, hypothesis_route_enters_zirat, hypothesis_tag_identifies_marat | знание: hypothesis_route_enters_zirat; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-route-marat-proof` | definitions.json | clue_marat_last_route_near_zirat, hypothesis_route_enters_zirat, hypothesis_tag_identifies_marat | знание: hypothesis_tag_identifies_marat; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-voice-creature` | definitions.json | hypothesis_heading_identifies_voice, hypothesis_voice_explained_as_echo | знание: hypothesis_heading_identifies_voice; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-voice-echo` | definitions.json | hypothesis_heading_identifies_voice, hypothesis_voice_explained_as_echo | знание: hypothesis_voice_explained_as_echo; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-photo-yard` | exploration-observation-checks.json | clue_photo_place_compared | знание: clue_photo_place_compared; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-sketch-place` | exploration-observation-checks.json | clue_sketch_place_compared | знание: clue_sketch_place_compared; эффекты: journal.record | наблюдение/запись вне реестра |
| `inspect-photo-gate-repair` | exploration-observation-checks.json | clue_photo_gate_repair_checked, clue_photo_place_compared | знание: clue_photo_gate_repair_checked; эффекты: journal.record | наблюдение/запись вне реестра |
| `inspect-sketch-tag-base` | exploration-observation-checks.json | clue_sketch_place_compared, clue_sketch_tag_base_checked | знание: clue_sketch_tag_base_checked; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-bench-plank` | exploration-observation-checks.json | discovery-zirat-outer-rest-bench, observation-bench-plank | знание: observation-bench-plank; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-branch-profile` | exploration-observation-checks.json | discovery-kara-branch-profile, observation-branch-overlap | знание: observation-branch-overlap; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-photo-facade-windows` | exploration-observation-checks.json | clue_first_snow_source_read, clue_photo_facade_windows_observed | знание: clue_photo_facade_windows_observed; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-photo-yard` | exploration-observation-checks.json | clue_first_snow_source_read, clue_photo_yard_observed | знание: clue_photo_yard_observed; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-sketch-landmarks` | exploration-observation-checks.json | clue_kara_urman_edge_is_rule_boundary, clue_sketch_field_landmarks | знание: clue_sketch_field_landmarks; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-well-tie` | exploration-observation-checks.json | discovery-arrival-insulated-well, observation-well-tie | знание: observation-well-tie; эффекты: journal.record | наблюдение/запись вне реестра |
| `return-local-words` | exploration-observation-checks.json | clue_local_words_return, discovery-arrival-insulated-well, discovery-house-interior-language-tin, discovery-main-street-sign-reverse | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `return-yard-loop` | exploration-observation-checks.json | clue_yard_loop_return, discovery-babai-yard-childhood-spinner, discovery-babai-yard-loose-side-gate-board, discovery-babai-yard-sled-repair | знание: clue_yard_loop_return; эффекты: journal.record | наблюдение/запись вне реестра |
| `discover-fap-service-cabinet` | exploration-small-spaces.json | discovery-fap-service-cabinet | знание: discovery-fap-service-cabinet; эффекты: journal.record | наблюдение/запись вне реестра |
| `discover-shed-loft-roofline` | exploration-small-spaces.json | discovery-shed-loft-roofline | знание: discovery-shed-loft-roofline; эффекты: journal.record | наблюдение/запись вне реестра |
| `discover-underdeck-rattle` | exploration-small-spaces.json | discovery-underdeck-rattle | знание: discovery-underdeck-rattle; эффекты: journal.record | наблюдение/запись вне реестра |
| `mark-underdeck-quiet` | exploration-small-spaces.json | clue_underdeck_rattle_quiet, discovery-underdeck-rattle | знание: clue_underdeck_rattle_quiet; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-family-call-scope` | investigation-family.json | clue_notice_call_not_request | знание: clue_notice_call_not_request; эффекты: journal.record | наблюдение/запись вне реестра |
| `revise-creature` | investigation-revisions.json | hypothesis_heading_identifies_voice | знание: hypothesis_heading_identifies_voice | наблюдение/запись вне реестра |
| `revise-echo` | investigation-revisions.json | hypothesis_voice_explained_as_echo | знание: hypothesis_voice_explained_as_echo | наблюдение/запись вне реестра |
| `revise-identity` | investigation-revisions.json | hypothesis_tag_identifies_marat | знание: hypothesis_tag_identifies_marat | наблюдение/запись вне реестра |
| `revise-inside` | investigation-revisions.json | hypothesis_route_enters_zirat | знание: hypothesis_route_enters_zirat | наблюдение/запись вне реестра |
| `compare-route-purpose-landmarks` | investigation-route.json | clue_route_check_intent | знание: clue_route_check_intent; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-warning-contexts` | investigation-social.json | clue_yaramyy_contexts_distinguished | знание: clue_yaramyy_contexts_distinguished; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-message-seen` | investigation-source-checks.json | clue_marat_message_read, clue_message_question_prepared, hypothesis_message_proves_sighting | знание: hypothesis_message_proves_sighting; эффекты: journal.record | наблюдение/запись вне реестра |
| `excerpt-message-voice` | investigation-source-checks.json | clue_alsu_heard_versions, clue_marat_message_read, clue_message_voice_excerpt | знание: clue_message_question_prepared, clue_message_voice_excerpt; эффекты: journal.record | наблюдение/запись вне реестра |
| `excerpt-notice-cause` | investigation-source-checks.json | clue_marat_official_death_version, clue_notice_cause_excerpt | знание: clue_notice_cause_excerpt; эффекты: journal.record | наблюдение/запись вне реестра |
| `excerpt-register-category` | investigation-source-checks.json | clue_marat_case_boundary_marker, clue_register_category_excerpt | знание: clue_register_category_excerpt; эффекты: journal.record, npc.set-state | наблюдение/запись вне реестра |
| `excerpt-register-wording` | investigation-source-checks.json | clue_marat_case_boundary_marker, clue_register_wording_excerpt | знание: clue_register_wording_excerpt; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-accounting-scope` | investigation-source-returns.json | clue_accounting_fragment_read, clue_marat_case_boundary_marker, clue_village_has_internal_compensation_system | знание: clue_village_has_internal_compensation_system; эффекты: journal.record | наблюдение/запись вне реестра |
| `observe-rinat-roadside` | rinat-roadside-presence.json | route_kara_urman_edge_hint | знание: clue_rinat_at_roadside; эффекты: beat.set-state, journal.record | наблюдение/запись вне реестра |
| `tamara-fence-take-board-1` | tamara-fence.json | — | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `tamara-fence-take-board-2` | tamara-fence.json | — | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `tamara-fence-take-board-3` | tamara-fence.json | — | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `tamara-fence-take-board-4` | tamara-fence.json | — | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `tamara-fence-take-board-5` | tamara-fence.json | — | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `tamara-fence-take-board-6` | tamara-fence.json | — | эффекты: npc.set-state | наблюдение/запись вне реестра |
| `bathhouse-condensation-observation` | village-life.json | bathhouse-condensation-observation | знание: bathhouse-condensation-observation; эффекты: journal.record | наблюдение/запись вне реестра |
| `compare-sabirov-family` | village-life.json | sabirov-family-linked | знание: sabirov-family-linked; эффекты: journal.record | наблюдение/запись вне реестра |
| `mosque-visit` | village-life.json | mosque-visit | знание: mosque-visit; эффекты: journal.record | наблюдение/запись вне реестра |

## Таблица C — сюжетное знание из реестра (33 строки, keep)

| ID | модуль | условия (что открывает) | результат | классификация |
|---|---|---|---|---|
| `compare-reread-response` | definitions.json | clue_internal_wording_reread | знание: clue_internal_wording_reread; эффекты: journal.record | сюжетное знание |
| `compare-route-match` | definitions.json | clue_marat_last_route_near_zirat, clue_sketch_field_landmarks, hypothesis_route_enters_zirat, hypothesis_tag_identifies_marat | знание: clue_marat_last_route_near_zirat | сюжетное знание |
| `compare-voice-link` | definitions.json | hypothesis_heading_identifies_voice, hypothesis_voice_explained_as_echo | знание: clue_folklore_as_survival_rule, clue_voice_answer_is_dangerous_hint; эффекты: vocabulary.set-status | сюжетное знание |
| `discover-arrival-bench-race-notches` | definitions.json | discovery-arrival-bench-race-notches | знание: discovery-arrival-bench-race-notches; эффекты: journal.record | сюжетное знание |
| `discover-arrival-insulated-well` | definitions.json | discovery-arrival-insulated-well, observation-well-tie | знание: discovery-arrival-insulated-well; эффекты: journal.record | сюжетное знание |
| `discover-babai-yard-childhood-spinner` | definitions.json | discovery-babai-yard-childhood-spinner | знание: discovery-babai-yard-childhood-spinner; эффекты: journal.record, vocabulary.set-status | сюжетное знание |
| `discover-babai-yard-entry-wall-joint` | definitions.json | discovery-babai-yard-entry-wall-joint | знание: discovery-babai-yard-entry-wall-joint; эффекты: journal.record | сюжетное знание |
| `discover-babai-yard-loose-side-gate-board` | definitions.json | discovery-babai-yard-loose-side-gate-board | знание: discovery-babai-yard-loose-side-gate-board; эффекты: journal.record | сюжетное знание |
| `discover-babai-yard-porch-paint-tin` | definitions.json | discovery-babai-yard-porch-paint-tin | знание: discovery-babai-yard-porch-paint-tin; эффекты: journal.record | сюжетное знание |
| `discover-babai-yard-sled-repair` | definitions.json | discovery-babai-yard-sled-repair | знание: discovery-babai-yard-sled-repair; эффекты: journal.record | сюжетное знание |
| `discover-connective-street-repair-bench` | definitions.json | discovery-connective-street-repair-bench | знание: discovery-connective-street-repair-bench; эффекты: journal.record | сюжетное знание |
| `discover-connective-street-return-bench` | definitions.json | discovery-connective-street-return-bench | знание: discovery-connective-street-return-bench; эффекты: journal.record | сюжетное знание |
| `discover-connective-street-shed-bypass` | definitions.json | discovery-connective-street-shed-bypass | знание: discovery-connective-street-shed-bypass; эффекты: journal.record | сюжетное знание |
| `discover-fap-exterior-care-porch` | definitions.json | discovery-fap-exterior-care-porch | знание: discovery-fap-exterior-care-porch; эффекты: journal.record | сюжетное знание |
| `discover-fap-exterior-service-path` | definitions.json | discovery-fap-exterior-service-path | знание: discovery-fap-exterior-service-path; эффекты: journal.record | сюжетное знание |
| `discover-fap-interior-height-marks` | definitions.json | discovery-fap-interior-height-marks | знание: discovery-fap-interior-height-marks; эффекты: journal.record | сюжетное знание |
| `discover-fap-interior-repaired-desk-object` | definitions.json | discovery-fap-interior-repaired-desk-object | знание: discovery-fap-interior-repaired-desk-object; эффекты: journal.record | сюжетное знание |
| `discover-house-exterior-porch-nook` | definitions.json | discovery-house-exterior-porch-nook | знание: discovery-house-exterior-porch-nook; эффекты: journal.record | сюжетное знание |
| `discover-house-exterior-rear-minaret-view` | definitions.json | discovery-house-exterior-rear-minaret-view | знание: discovery-house-exterior-rear-minaret-view; эффекты: journal.record | сюжетное знание |
| `discover-house-interior-language-tin` | definitions.json | discovery-house-interior-language-tin | знание: discovery-house-interior-language-tin; эффекты: journal.record, vocabulary.set-status | сюжетное знание |
| `discover-house-interior-photo-back` | definitions.json | discovery-house-interior-photo-back | знание: discovery-house-interior-photo-back; эффекты: journal.record | сюжетное знание |
| `discover-kara-branch-profile` | definitions.json | discovery-kara-branch-profile, observation-branch-overlap | знание: discovery-kara-branch-profile; эффекты: journal.record, vocabulary.set-status | сюжетное знание |
| `discover-kara-old-forestry-side-track` | definitions.json | discovery-kara-old-forestry-side-track | знание: discovery-kara-old-forestry-side-track; эффекты: journal.record | сюжетное знание |
| `discover-kara-warm-window-clearing` | definitions.json | discovery-kara-warm-window-clearing | знание: discovery-kara-warm-window-clearing; эффекты: journal.record | сюжетное знание |
| `discover-main-street-fenced-service-lane` | definitions.json | discovery-main-street-fenced-service-lane | знание: discovery-main-street-fenced-service-lane; эффекты: journal.record | сюжетное знание |
| `discover-main-street-side-window` | definitions.json | discovery-main-street-side-window | знание: discovery-main-street-side-window; эффекты: journal.record | сюжетное знание |
| `discover-main-street-sign-reverse` | definitions.json | discovery-main-street-sign-reverse | знание: discovery-main-street-sign-reverse; эффекты: journal.record, vocabulary.set-status | сюжетное знание |
| `discover-zirat-outer-culvert-crossing` | definitions.json | discovery-zirat-outer-culvert-crossing | знание: discovery-zirat-outer-culvert-crossing; эффекты: journal.record | сюжетное знание |
| `discover-zirat-outer-rest-bench` | definitions.json | discovery-zirat-outer-rest-bench, observation-bench-plank | знание: discovery-zirat-outer-rest-bench; эффекты: journal.record | сюжетное знание |
| `forest-rinat-intervention` | definitions.json | clue_rinat_at_roadside | знание: clue_do_not_answer_rule; эффекты: beat.set-state | сюжетное знание |
| `zirat-roadside-clue` | definitions.json | clue_zirat_roadside_marks, route_kara_urman_edge_hint | знание: clue_zirat_roadside_marks; эффекты: journal.record | сюжетное знание |
| `compare-versions-scope` | investigation-route.json | clue_marat_versions_conflict | знание: clue_marat_versions_conflict; эффекты: journal.record | сюжетное знание |
| `compare-record-scope` | investigation-social.json | clue_naila_record_scope, clue_record_wording_mismatch, contradiction_marat_official_vs_internal | знание: contradiction_marat_official_vs_internal; эффекты: journal.record | сюжетное знание |

## Пилот по M1 (первые 5–8 интерактивов маршрута)

Порядок производства из M1: старт/Z13 → ФАП/D13 → бабаев двор/Z18/И22 → баня → остальной путь.
Первые строки маршрута по данным: `discover-arrival-*` (скамья, утеплённый колодец, столб),
`observe-well-tie`, `discover-babai-yard-childhood-spinner` (Z13), затем ФАП-строки и
`upper-latch`. Из них 2–3 спорных решения (Z13; лавка с держателем; варежка/щеколда)
показываются автору — как и требует M1/M2.

## Что решает автор или владелец очереди

1. Z13: убрать интерактив, дать физический эффект или оставить ранним намёком (M2).
2. Класс «без результата»: для каждой строки либо ясное назначение, либо удаление; удаление
   безопасно для save/квеста/EX по данным выше.
3. Кружка ФАП и D13-предметы: есть ли отдельный интерактив вне контентного реестра.
4. Варежка/щеколда: сохранить ли бытовую функцию без автоматической выдачи записки.

## Ограничения доказательства

Инвентаризация статическая: она не измеряет приятность действия и не заменяет человеческий
проход. Доля 76/156 — измеренная доля строк без эффектов, а не подтверждение оценки «90 %».
Производные `game/content/*.compiled*.json` не использовались как источник. Роли, которые
определяются только сценой (позиция, форма, звук, анимация), здесь не оцениваются — для них
нужен обычный прогон с проходом и поворотами.
