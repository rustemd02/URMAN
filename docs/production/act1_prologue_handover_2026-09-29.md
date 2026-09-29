# Handover: пролог Акта I — 29.09.2026

Рабочее дерево (не всё закоммичено ранее), сборка C# 0 ошибок. Смок `prologue-watch` PASS до последней правки
(`URMAN_DISCOVERY_ROUTE_ONLY=prologue-watch sh eng/run-smoke-guarded.sh act1_first_person_walkthrough_smoke_test`).

## Сделано
- Флешфорвард в отдельной локации `game/scripts/PrologueDeepForest.cs` (просека 230 м, деревья MultiMesh, следы, варежка,
  ель, избушка, силуэт, огоньки, поляна, кусты, снег); логика — `Act1DemoRoot.PrologueForest.cs` (события по пути,
  звуковой планировщик, пробег через просеку). Guard мира: `FirstPersonController.DetachedWorldGuard`.
- Поездка: подъезд `PrologueApproachRoad.cs` + деревня, `Act1DemoRoot.PrologueNivaRide.cs`; реплики бабая
  `prologue-ride-bark-*` (+`.lvl-some/.lvl-fluent`) в `content/modules/urman-chapter1/definitions.json`.
- Калибровка по-татарски + «давай так/проще/больше»; адаптивный текст `CompiledCampaignRepository.ResolveText(id, level)`.
- Звуки: `tools/audio/generate_prologue_forest_sounds.py` → `game/assets/audio/act1/foley/forest/`.
- Поиск ПК (нормализация татарских букв, ступенчатая помощь) — смок PASS; тексты без ремарок; карта звуков.

## Состояние после сессий 29.09 (обновлено)
Закрыто позже другими сессиями: импорт `scare_sting`, переписанная реплика пробуждения (`089780b`), расширение
деревни срезы 1–2 и поездка по новой улице (`a24e6a5`, `3fa901a`, `a583862`), TTS-превью и лесной звук (`fe7d84d`).
Добавлено здесь: идея 2 разбора — «радио ловит имя» (`Act1DemoRoot.PrologueRideRadio.cs`, звук
`tools/audio/generate_prologue_radio_evp.py` → `foley/forest/radio_evp.wav`).

## Открыто
1. Ответы автора: чья варежка (идея 6), появление фигуры при взгляде из Нивы (идея 3).
2. Слух-проверка всех процедурных звуков и TTS; татарский — носителем.
3. Смоки `act1_first_person_walkthrough` (основной режим) и коридор устарели после A02.
4. Здания площади — объёмы-заготовки (ТЗ04).
