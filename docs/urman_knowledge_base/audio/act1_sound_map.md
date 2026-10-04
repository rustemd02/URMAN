# Act I Sound Map (AUDIO-002)

Status: **authored sound-design map; technical routing baseline 2026-09-04; final
mix, human listening and cultural review remain open (AUDIO-014, CULTURE-004)**

This map fixes the intended sonic read of every mandatory Act I route segment:
beds, spot events, silence windows, transitions and voice priorities. It is the
acceptance reference for AUDIO-003…AUDIO-010 authored layers and for the final
mix review. No combat music logic, no horror stingers, no cemetery
sensationalism — tension is built from ordinary sound going quiet or wrong.

## Ownership rules

- One continuous ambience owner: `AmbientAudioDirector` + `ambient_manifest.json`
  (0.65 s crossfade between zone beds). No scene-local loops, no second director.
- Voice plays through `AudioCueUi` with synchronous captions; logical cue IDs
  today: `urman.chapter1:asset/audio-marat-voice`,
  `urman.chapter1:asset/audio-rinat-interruption` (final recordings:
  AUDIO-011…AUDIO-013).
- `AudioCueUi` serializes every voice request, including physical recordings
  when subtitles and audio descriptions are disabled. Physical streams use
  `AudioStream.GetLength()`; logical text uses a readable fallback of at least
  two seconds and approximately 18 characters per second, while a silent
  logical request drains immediately. The pause shell freezes both the player
  and presentation clock; new game, successful load and return to menu clear
  the queue and presentation history.
- Physical voice temporarily ducks the existing `Ambience` bus through
  `AmbientAudioDirector`. The user's persisted ambience volume is re-applied
  when the cue queue drains; no second ambience owner or settings write is
  introduced.
- Priority ladder: voice → spot events → bed. Silence is an authored choice,
  never a bug cover or a fog mask.
- Footsteps (AUDIO-004) and UI foley (AUDIO-010) are presentation-only: they
  never write state. The source-backed `door_creak.wav` candidate from
  soundofsong (Freesound 647646, CC0) is routed by the existing
  `InteractionTarget` owner through `UiFoley.PlayWorld` at the active house
  portal or gate proxy. Human listening and final mix review remain open.

## Footsteps (AUDIO-004, winter runtime)

`FootstepAudioController` owns the four active Act I surface families:
`snow_packed`, `snow_soft`, `wood` and `interior_floor`, with three
source-derived variants per family. The outdoor choice uses the existing
`AgentBAct1HeightField.RoadInfo`: `Distance < HalfWidth` resolves to packed
snow and the remaining outdoor ground resolves to soft snow. The house maps to
`wood`; the FAP maps to `interior_floor`.

Cadence is based on the player's actual XZ displacement at `0.55 m`, matching
the `SnowTrampleField` stride. Zone-spawn/load revisions, direct teleports,
modal frames and airborne frames reset the accumulator. Reduced motion keeps
footstep audio enabled while continuing to govern camera and other motion
presentation. The legacy wet-road, mud and grass WAVs remain in the repository
as inactive historical assets.

The four families are CC0 source-derived candidates. Their source paths,
archive hashes, per-file hashes and conversion record live in
`game/assets/audio/act1/footsteps/manifest.json`; the source codecs and license
texts are retained under `assets/source/audio/act1/footsteps/`. Human listening,
mix and cultural review remain open.

## Zone/segment map

| Segment | Current recorded bed | Spot events / remaining work | Intended read |
|---|---|---|---|
| Arrival (`village_day@arrival`) | 69 s winter wind, lwdickens 261226 | footsteps on packed snow; no synthetic rain or drips | quiet inhabited winter village |
| MainStreet | 119 s winter wind from the same residential recording | physical gate creaks; sparse household activity still needs listening review | people behind fences |
| BabaiEbiYard / porch | quieter 69 s winter wind fragment | own footsteps and gate; no summer poultry or roof rain | sheltered domestic threshold |
| House interior (`house_old_pc`) | 96 s cabin room tone, callmethefoo 744447 | UI keyboard/paper remain procedural; near-PC and domestic foley need mix review | warm small interior without synthetic continuous whine |
| FAP interior (`fap_clinic`) | 121 s indoor room tone, RIFORKA 801025 | paper and local equipment need listening review | quiet institutional space; no horror drone |
| ConnectiveStreetReturn | later 69 s winter wind fragment | fewer foreground events | village recedes towards the outskirts |
| Zirat / approach road | 26.43 s recorded wind, Magnesus 606960 | own snow steps; no grave sound effects | restrained remembrance and distance |
| KaraForestEdge (`kara_urman_night`) | 119 s January pine wind, bruno.auzet 670307 | natural tree movement in source; no added stinger | forest remains a place, not a monster cue |
| Final beat (`scene/forest` onEnter) | existing ambience duck and hard cut | physical Marat/Rinat voices remain missing | familiar call, intervention, silence |

All are prepared CC0 public HQ previews, not locally recorded Kyrlay field
masters. Source pages, hashes, licensed preview URLs and PCM parameters live
in `ambient_manifest.json` and `asset_registry.json`. Only the inactive
future-act water bed stays procedural. House and Kara shared future-zone
bindings remain unchanged; this pass does not claim acceptance of later acts.

Regeneration of the seven newly replaced beds uses the existing
`tools/audio/generate_act1_ambience_layers.py SOURCE_DIRECTORY`. The directory
must contain the original preview URL basenames with matching SHA-256 values.
The script verifies all inputs before writing, reuses the existing PCM header
normalizer, slices at manifest offsets, joins a one-second equal-power loop,
and applies declared RMS levels with a 0.70 peak ceiling. The runtime retains
its existing -12 dB bed level and transient -6 dB voice duck. This replaces
repeating eight-second synthesis with 69–121 second recordings; it does not
prove the audible seam or artistic balance. Listening on speakers/headphones,
final mix and cultural review remain open.

## Transitions and silence windows

- Zone switches crossfade beds (0.65 s); interior↔exterior adds a soft
  occlusion dip — no pops, no double beds (AUDIO-009 owns the routing rules).
- Authored silence windows: the zirat pause (before the roadside clue) and the
  Kara threshold (before Rinat's line). Both are dramatic choices with visual
  support, never dead audio.
- The cliffhanger hard cut remains responsible for stopping the bed after the
  presentation queue drains. This lifecycle slice provides queue drain and
  transient voice duck; final hard-cut staging remains an authored finale
  concern. The ending is silent by design, not by omission.

## Anti-patterns (reopen triggers)

- Combat/stinger logic, sudden loud scares, cemetery horror decoration.
- Fog or silence used to hide unfinished visuals.
- A second ambience owner or scene-local loops.
- TTS or placeholder voice presented as final (AUDIO-012 gate).

### Ending silence — 2026-09-11
The existing AmbientAudioDirector stops both continuous bed players when the
closing card is shown after the authored cue queue drains. This is a hard
presentation stop, with no write to player volume settings. New Game / load
re-enters the normal SetZone path and restarts the appropriate bed. The
existing chapter flow check covers final silence and fresh-session restart.
Physical Marat/Rinat recordings and final winter-bed listening/mix are still
open; this lifecycle fix is not voice or audio-quality acceptance.

## Recorded door and gate foley — 2026-09-11
The soundofsong 647646 CC0 preview conversion replaces the procedural
door sample. Successful InteractionTarget actions own playback; zone
polling no longer triggers it during load. House entry uses the active
interior HouseExit, exit uses the exterior HouseDoor, and four opening
gates use their physical interaction proxy. Zone-changing sounds live
on Main so replacing an isolated zone cannot destroy the one-shot.
Pause, load/reset and menu paths pause or clear world players; a sound
created after an awaited action also respects the already-open pause menu.
Native Metal Act1AudioTransitionSmokeTest passed actual HouseDoor.Interact,
source position, SFX attenuation, pause/resume, natural finish, explicit
clear and silent save restoration. This is runtime evidence, not human
listening or final mix acceptance.

Взаимодействие после ожидания записи checkpoint или открытия документа проверяет прежний kernel, готовность загрузки, существование target и реальное состояние главного меню. Это не позволяет старому действию переключить зону или заново включить звук после load/restart/menu. Отдельного счётчика жизненного цикла у аудио нет. Нативный `lifecycle_guard_native_audio.log` подтвердил вход через настоящий `HouseDoor.Interact`, позицию звука у `HouseExit`, отклонение presentation в меню/для прежнего kernel после load, паузу и очистку one-shot, а также переходы пяти зон. Это техническая проверка с Dummy audio; художественное прослушивание остаётся открытым.

## Original voice recording handoff — 2026-09-12

The [recording brief](act1_voice_recording_brief.md) fixes the existing spoken lines, performance variants, delivery format, rights information, caption IDs and planned physical paths for the two missing recordings. No actor recording, rights grant, registry hash or listening acceptance is claimed.


## 2026-09-12 — финальные слова в обычных субтитрах

`audio-marat-caption` теперь показывает «Знакомый голос: „Казанский… не отставай“», `audio-rinat-caption` — «Ринат: „Не отвечай“». Раньше они описывали появление голоса и вмешательство, не передавая самих слов. Английский fallback синхронизирован; подробные `NonAudioCue` transcripts и последовательность очереди сохранены. Это исправление канала субтитров и не замена двух отсутствующих актёрских записей. Минимально обновлено точное ожидание существующего ChapterOneFlow; content compile, game build и этот smoke прошли, пользовательские данные восстановлены.

### 2026-09-12 — крупное аудиоописание

Предположение о фиксированной высоте панели не подтвердилось в native 720p:
при TextScale 1,6 длинный transcript Марата занимает две строки, Label имеет
minimum Y=101 logical px, PanelContainer автоматически вырастает с 84 до 129 px
и полностью содержит текст. Кадр `audio_caption_large_probe_r11` просмотрен;
кастомный пересчёт высоты не добавлялся. Это проверка раскладки одной подписи,
не полной доступности, озвучки или прослушивания. Временный capture восстановлен.

## Карта событий по отзыву 28.09.2026 (ACT1-AUDIO.MAP)

Срез по коду рабочего дерева 29.09.2026 (над HEAD `3391370`). Это карта «событие → источник →
способ воспроизведения», а не утверждение, что звук исправлен или прослушан. Прослушивание — открыто.

| Отзыв | Событие в игре | Владелец / вызов | Сэмпл, способ | Состояние |
|---|---|---|---|---|
| R062 стук | «Постучать» у адресной двери | `Act1ConnectedWorld.Addresses.cs` `PresentationRepeat` | `wood_tap`, `PlayWorld` у двери | Ложный `door_creak` заменён ранее; настоящей записи стука нет — **отсутствует** |
| R060 «случайный звук успеха» | Включение лампы на столе ФАПа | `Act1ConnectedWorld.Exploration.cs` | был `ui_click` в UI-плеер у слушателя; теперь `ui_click` через `PlayWorld` у лампы | **Исправлено 29.09** (позиционный щелчок выключателя); на слух не проверено |
| R060 | Баннер побочного задания | `SideQuestBannerUi.Present` → `paper_open` | UI | Вызывается только квестом Тамары (вне объёма); в Акте I без Тамары не звучит |
| R060 | Переключение станции радио в Ниве | `VehicleController.cs` | `ui_click`, `PlayWorld` у машины | Механический щелчок, объяснимое событие |
| R060 | Открытие книжки/документа, диалог | `JournalUi`/`DocumentUi`/`DialogueUi` | `paper_open`/`ui_click`, UI | Интерфейсные звуки на действие игрока, не награда |
| — | Двери и калитки (дом, обходы, ФАП) | `InteractionTarget.WorldFoleySample` | `door_creak`, `PlayWorld` у проёма | Соответствует событию |
| — | Магазин: покупка | `Act1ConnectedWorld.ShopUses.cs` | `metal_rattle` (батарейки) / `paper_open` | Соответствует предмету; качество на слух открыто |
| R131 баня | Растопка печи | `Act1ConnectedWorld.Bathhouse.cs` | `wood_tap` у топки | Огня/треска нет — **отсутствует**; звук духа бани — **отсутствует** (вариант ТЗ04) |
| R033 шаги | Шаг по поверхности | `FootstepAudioController` по meta `footstepSurface` | 7 наборов: snow_packed/soft, wood, interior_floor, grass, mud, wet_road | Различие поверхностей есть; громкость/характер дороги vs снега — прослушивание открыто |
| R061 лес | Кромка/ночь, звук леса | `AmbientAudioDirector` + `ambient_manifest.json` | `kara_urman_edge_ambience.wav`, CC0 preview | Пространственных слоёв (скрип, ветка) нет — **отсутствует** |
| — | Пролог-тизер в лесу | `Act1DemoRoot.PrologueForest.cs` | `wood_tap`, `metal_rattle`, `hollow_board` позади игрока | Позиционно; художественная сила не проверена |
| — | Первая ночь, метель/обрушение | `Act1DemoRoot.FirstNight.cs` | `zirat_wind.wav` + `hollow_board` в фокусе | Нет звука обрушения пролёта — **отсутствует** |

Неизвестные: авторский «звук успеха» не воспроизведён по записи; если он не лампа, найти по следующему
плейтесту с таймкодом. Отсутствующие записи (стук, огонь, обрушение, лесные спот-слои, дух бани) — заявки
на подбор CC0/запись; до них используются ближайшие честные сэмплы, не выдаваемые за финальные.

### Пролог, глубокий лес (29.09.2026, отзыв автора: «совы, звери, шевеление в кустах, кто-то пробегал»)

Генератор: `tools/audio/generate_prologue_forest_sounds.py` → `game/assets/audio/act1/foley/forest/*.wav`
(32 кГц, моно, процедурные заглушки из шума, тональных глиссандо и лесной реверберации; не полевые записи).
Воспроизведение — `UiFoley.PlayWorld` с собственной дальностью (перегрузка volume/maxDistance/unitSize).

| Событие | Когда | Сэмпл | Где |
|---|---|---|---|
| Совы | случайно каждые 4,5–11 с (30%) | `owl_eagle`, `owl_tawny` | 30–70 м, в кронах 12–22 м |
| Шорох в кусте | случайно (25%) | `brush_rustle` + куст дёргается, осыпается снег | ближайший куст 4–16 м |
| Треск ветки | случайно | `branch_crack` | 12–35 м |
| Щелчки в темноте | случайно после 95 м | `strange_clicks` | 10–20 м |
| Порыв ветра | случайно | `wind_gust` | над игроком |
| Волк | 20 м пути, один раз | `wolf_far` | ~95 м |
| Пробег через просеку | 88 м пути | `runner_past`, движется слева направо в 9 м впереди, кусты и снежная пыль | просека |
| Крик лисы | 128 м пути | `fox_scream` | ~38 м |
| Низкий стон | 184 м пути | `low_moan` | ~22 м |
| Нападение и падение | финал леса | `branch_crack` → `rush_close` → `body_fall` → `heartbeat` → `gasp` | у игрока |

Каждое крупное событие дублируется субтитром в квадратных скобках. Прослушивание, микс и замена на
CC0-записи открыты.

## Постоянная непогода · поручение автора 2026-10-03

Четыре действующих дневных bed ID используют уже подготовленный CC0 January wind in pine trees
(`kara_urman_edge_ambience.wav`, Freesound bruno.auzet/670307); паспорта manifest соответствуют этому файлу.
Уличные beds имеют gain −3 dB; дом и ФАП сохраняют −12 dB и свои тихие записи.
Разница gain 9 dB дополняется разницей записей; это не обещание конкретной воспринимаемой громкости.
Существующий переход укрытия и crossfade 0,65 с сохраняются, речь использует прежний voice duck.
Первая ночь усиливает метель; обрушение и утренняя новость не меняются.
Техническая проверка loop/bed/gain не заменяет прослушивание микса автором.

## Живой слой деревни, шкала настроения и отладочная панель · поручение автора 2026-10-04

Автор: звуки деревни перебиваются бураном; убрать неуместные зимой птичьи голоса; добавить треск
печки, телевизор, добрый лай, тарелки, добрый смех и разговоры; дать шкалу от жуткой деревни
(гул, странные птицы) до доброй (музыка, ТВ, тарелки, собаки); скачать азан и добавить его
культурно корректно; открыть отладочную панель звука по читкоду. Уточнение того же запуска:
трим громкости не нужен — достаточно простого вкл/выкл бурана.

Владелец — `VillageSoundMoodDirector` (группа `village_sound_mood`), два непрерывных слоя на
Ambience bus: `village_life_layer.wav` (ветер в основе плюс негромкий гомон, приглушённый ТВ,
редкая домашняя скрипка и дальний лай) и `village_dread_layer.wav` (тот же ветер, низкий гул и
редкие совиные крики без стингеров). Дневное авторское настроение 0,9, ночное 0,25; панель может
переопределить значение вручную.

Постоянная непогода 03.10 сохранена (тот же bed, тот же файл), но по отчёту аудита 04.10 его
внешний gain снижен с −3 до −10 дБ, чтобы деревня не тонула; слои получили уровни −3/−10 днём и
−12/−4 ночью; панель даёт простой переключатель вкл/выкл всего bed через
`AmbientAudioDirector.SetBedEnabled`. Слои глушатся в помещении, диалоге и модальном режиме
панели (исправлено по аудиту 04.10).

Бытовой каталог вырос с 23 до 35 зимних событий: печка, телевизор за стеной, добрый смех,
негромкий разговор, колка дров, пила, калитка, готовящаяся еда, корова в закрытом сарае, детвора
в снегу, шаги по снегу и гармонь (по культурному брифингу 06/08). Куры остаются только дневным
событием закрытого сарая; летних птиц в деревне нет; совы принадлежат прологу и слою жути. Азан: стамбульская запись CC0 отклонена автором (ресторанный фон, не татарская традиция) и
удалена 04.10.2026; хук у якоря минарета (`Act1ConnectedWorld.TryPlayAdhan`) и кнопки панели
сохранены и ждут лицензированную татарскую запись (CC0/CC BY/PD или письменное разрешение);
синтетической подмены, выдуманного расписания и нелицензированного аудио нет.

Панель отладки — `DebugSoundPanel`, читкод `zvuk` (физические клавиши Z-V-U-K). Существует только
при `user://debug-zones.enabled`, удерживает игрока модальным режимом (без паузы дерева — иначе
плееры узлов замирают и азан/слои молчат), отпускает курсор, прослушивает one-shot записи,
показывает состояние слоёв и не входит в релиз.
