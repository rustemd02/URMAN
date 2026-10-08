# УРМАН — съёмка авторских состояний цвета (VIS-069…074)

**Дата:** 2026-10-08 (продолжение итерации 02 пакета `URMAN_VISUAL_RESET_2026-10-07`).
**Данные:** `game/content/world/visual_checkpoints.v1.json` (он же `res://content/world/visual_checkpoints.v1.json`).
**Код:** `game/scripts/Act1DemoRoot.DevViewCapture.cs` (`LoadVisualCheckpoints`, `BindVisualCheckpoint`),
`game/scripts/Act1ConnectedWorld.cs` (`ApplyCapturePhase`, рядом с `TuneConnectedAct1Atmosphere`).
**Профили:** `game/content/world/atmosphere.v1.json` — шесть авторских состояний.

## Зачем нужен манифест

Транспорт станции умеет передать в задании только строку точек:
`eng/remote-check.py capture --points …` → `URMAN_VIEW_POINTS` → harness съёмки.
Переменная `URMAN_ATMOSPHERE_PHASE` в этот путь не попадает, а один job снимает одно
дерево с одним профилем. Из-за этого состояния, которые не принадлежат ни одной зоне
(`overcast-day`, `golden-hour`, `village-green-night`), было нельзя сфотографировать,
и карточки VIS-069…074 оставались в статусе BLOCKED (съёмка фаз).

Решение не меняет транспорт: репозиторный манифест сам несёт профиль кадра.

## Как это работает

1. Harness читает манифест, только когда задан `URMAN_VIEW_POINTS`. Файла нет — поведение
   ровно как раньше: каждый кадр под профилем своей зоны.
2. Для каждого запрошенного имени кадра ищется строка с этим `id`.
   Есть `profile` → кадр снимается под ним; строки нет или `profile` отсутствует → кадр
   остаётся на профиле зоны (так снимаются канонические C1…C8 — базовая пара before/after).
3. Перед первым PNG проверяются все строки манифеста: `id` — безопасное имя кадра,
   `spec` — часть `камера>цель` без префикса, `profile` — существующий id из
   `atmosphere.v1.json`, дубликаты `id` запрещены.
4. `spec` строки обязан совпадать с координатами, которые просит задание. Иначе кадр
   фотографировал бы другой предмет под меткой состояния — это отказ, а не «примерно тот же вид».
5. Профиль применяется на каждом кадре до прогрева камеры: `ApplyCapturePhase(profile)`
   ставит его в существующий механизм `StudioPreviewProfile`, повторно вызывает обычный
   `TuneConnectedAct1Atmosphere` для активного связного мира и сверяет результат с meta
   `unifiedAtmosphereProfile` — тем же значением, которое уходит в сайдкар. При переходе на
   кадр без строки оверрайд снимается, свет возвращается к профилю зоны.
6. Приоритет: профиль манифеста кадра → `URMAN_ATMOSPHERE_PHASE` → профиль зоны.
   Обычный игровой путь манифест не читает.

## Отказ, а не догадка

Неизвестный `profile` в манифесте останавливает весь job до записи первого PNG:

```
View capture refused: checkpoint 'C1_overcast' names atmosphere profile 'overcast-dayy',
which is not authored in res://content/world/atmosphere.v1.json (known profiles: ...).
Point 'C1_overcast:...' wrote no frame.
```

`GD.PushError` + `GetTree().Quit(1)`; станция падает на проверке `frames/*.png`, receipt
не появляется. То же — если профиль не дошёл до мира (`ApplyCapturePhase` вернул `false`).
Принцип тот же, что у VIS-003: неверный адрес камеры уже был отказом, теперь отказом стало и
неверное состояние света.

## Доказательство в кадре

Сайдкар `<name>.json` (VIS-004) теперь несёт и запрос, и факт:

- `requestedProfile` — что просит манифест (`null`, если строки нет);
- `atmosphereProfile` — что реально применено (meta мира);
- `checkpointSubject` — предмет чекпоинта;
- плюс прежние `studioPreviewProfile`, tonemap, цвет/плотность тумана, `snowActive`, draw calls.
- `_capture-summary.json` добавляет `checkpointManifest` и `checkpointRows`.

Кадр считается свидетельством состояния только при `requestedProfile == atmosphereProfile`.

## Пакеты съёмки (2–3 вызова)

Станция принимает не больше **8 записей** и **1000 символов** в `--points`
(`eng/windows_station_worker.py:218`), поэтому шесть состояний покрываются тремя вызовами.
Команды ниже — ровно `batches` из манифеста; при изменении любой `spec` менять и строку,
иначе harness откажет.

### A — улица в шести состояниях (6 записей, 280 символов)

Одна и та же камера C1, шесть профилей: закрывает сравнение «одно место, шесть authored states».

```bash
python3.12 eng/remote-check.py capture --points 'C1_frost_morning:Ground@0,1.7,12>Ground@0,1.6,-30;C1_overcast:Ground@0,1.7,12>Ground@0,1.6,-30;C1_golden_hour:Ground@0,1.7,12>Ground@0,1.6,-30;C1_green_night:Ground@0,1.7,12>Ground@0,1.6,-30;C1_forest_night:Ground@0,1.7,12>Ground@0,1.6,-30;C1_dusk:Ground@0,1.7,12>Ground@0,1.6,-30'
```

### B — лес и окна в состояниях (4 записи, 187 символов)

Критерии VIS-071/072 читаются на окнах hero-дома, VIS-073/074 — на глубоком лесном плане.

```bash
python3.12 eng/remote-check.py capture --points 'C2_green_night:Ground@-14,1.7,4>Ground@-30,1.8,-6;C5_overcast:Ground@0,1.7,-40>Ground@0,2.0,-80;C5_forest_night:Ground@0,1.7,-40>Ground@0,2.0,-80;C5_dusk:Ground@0,1.7,-40>Ground@0,2.0,-80'
```

### C — канонический набор C1…C8 на профиле зоны (8 записей, 404 символа)

Anchor для пар before/after: те же камеры, свет обычного игрового пути.

```bash
python3.12 eng/remote-check.py capture --points 'C1_arrival_street:Ground@0,1.7,12>Ground@0,1.6,-30;C2_hero_house:Ground@-14,1.7,4>Ground@-30,1.8,-6;C3_fence_close:Ground@-8,1.4,-6>Ground@-14,1.0,-10;C4_fap_branch:Ground@10,1.7,-10>Ground@30,1.8,-28;C5_forest_edge:Ground@0,1.7,-40>Ground@0,2.0,-80;C6_dk_square:Ground@6,1.7,-55>Ground@-10,2.0,-70;C7_trail_junction:Ground@2,1.5,-20>Ground@18,1.4,-34;C8_sky_silhouette:Ground@0,1.7,-8>Ground@-40,6.0,-40'
```

После отправки: `python3.12 eng/remote-check.py status --job-id <id>` и
`fetch --job-id <id>`; кадры и receipt привязаны к snapshot-хешу дерева.

## Статус этих команд: QUEUED, не выполнены

- Станция `UNTERPC` сообщает о **недостатке свободного места на диске**; job в таком
  состоянии не проходит, поэтому захват не запускался и `not-run` сохранён честно.
- По действующему поручению автора (AGENTS.md, 08.10.2026 13:05) станция — только экран для
  ручного запуска игры: **Codex сам не вызывает `remote-check.py`** и не запускает движок.
  Эти строки подготовлены для автора/оператора стенда и остаются в очереди.
- Проверка этого этапа — статическая: узкая компиляция C# (`game/Urman.Game.csproj`) и
  разбор манифеста (совпадение `batches` со строками `points`, ≤8 записей, ≤1000 символов,
  все `profile` существуют в `atmosphere.v1.json`).

## Что закроют эти кадры

| Карточка | Состояние | Кадры |
|---|---|---|
| VIS-069 | `golden-hour` | `C1_golden_hour` |
| VIS-070 | `overcast-day` | `C1_overcast`, `C5_overcast` |
| VIS-071 | `village-green-night` | `C1_green_night`, `C2_green_night` |
| VIS-072 | контраст двух семейств ночью | `C2_green_night` (тёплые окна против зелёной массы), `C1_dusk` |
| VIS-073 | плотный лесной туман | `C5_forest_night`, `C5_dusk` |
| VIS-074 | нечеловеческий лесной цвет | `C5_forest_night`, `C1_forest_night` |

Кадр закрывает карточку только в паре с тем же camera/FOV/resolution/зоной и при
подтверждённом профиле в сайдкаре; художественная приёмка цвета остаётся за автором.
