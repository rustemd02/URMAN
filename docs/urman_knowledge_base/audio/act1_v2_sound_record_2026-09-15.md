# V2 — запись: звук на изменённой геометрии

Дата: 2026-09-15. Срез: `main` после `4255370`. Владельцы звука не менялись: `UiFoley.PlayWorld`
(позиционные сэмплы по миру), `FootstepAudioController` (шаги по поверхности), `AmbientAudioDirector`
(фоновые слои).

## Что проверено на изменённой геометрии

**Ворота и калитки (7 ветвей).** Все семь физических ветвей §6.2 несут
`WorldFoleySample = "door_creak"` на своём InteractionTarget (`FapExploration.cs:138`,
`BabaiYardSideGate.cs:122`, `BypassDiscoveries.cs:89/219`, `KaraOptionalDiscoveries.cs:257`, двери дома
в `StyleBenchmarkZone.cs:328/340/709`). `UiFoley.PlayWorld` берёт позицию из самой цели — ворота в
переработке не двигались, поэтому скрип остаётся привязан к своей геометрии. Проверено тестом
`act1-world-foley: PASS source position + SFX routing + unit/max distance + pause/resume + StopWorld` и
тем, что walkthrough прожимает каждую из этих ветвей (журнал `journal.changed` на находки).

**Скрип снега на новых линиях.** `FootstepAudioController` выбирает семпл по поверхности, а не по
зоне: в библиотеке четыре зимних семейства (`step_snow_packed_*`, `step_snow_soft_*`, `step_mud_*`,
`step_wet_road_*`) плюс `step_interior_floor_*`, `step_wood_*`, `step_grass_*`. Новые ходовые линии
(восточное хозяйство, перенесённая петля двора, подходы к дворам с комплектами) лежат на том же снегу —
шаги подхватываются автоматически, привязка «линия → звук» не требуется.

**Манифест шагов честно помечен** `listening-review-open`: технический прогон есть, человеческое
прослушивание микса — нет.

## Чего в V2 нет сознательно

- **Жёлоб и слив сеней** молчат: в библиотеке нет ни одного водного/капельного сэмпла (всего четыре
  фоли: `door_creak`, `keyboard_key`, `paper_open`, `ui_click`). По V2.5 AV-карточки пакета — бриф,
  а не готовые фонограммы: синтезировать «капель» из подручных сэмплов значило бы подменить запись.
  Нужна авторская фонограмма — внешняя работа.
- **Дым печи**, скрип снега «с разным весом» (свежий/утоптанный различаются сэмплами, но привязка
  свежести к месту — авторское решение) — не делались.
- Живые голоса Марата и Рината — отдельная ранее открытая работа, TTS-заменой не закрывается.

## Проверка

`act1_first_person_walkthrough_smoke_test` PASS 424,88 м; `act1-audio-transitions: PASS 5 zones ->
manifest beds + world foley spatial lifecycle`; `ambient-audio-smoke` PASS (manifest + import +
zone switching + looping).
