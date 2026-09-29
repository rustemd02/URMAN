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

## Не доделано (следующий шаг)
1. **Импортировать `scare_sting.wav`** (новый файл): `source eng/dotnet-env.sh; Godot --headless --path game --import`
   (процесс зависает после импорта — убить, когда появится `.import`).
2. Финал леса переписан (последняя правка, НЕ прогнан): поворот — никого — тишина — рывок вниз в снег + стинг +
   белая вспышка → чёрный → пробуждение. Автор: **монстра перед лицом не показывать** (сделано), нужен
   клишированный полускример. Проверить на глаз и смоком.
3. Реплика `prologue-ride-bark-nightmare` (все три уровня) переписать по автору: бабай спрашивает «что тебе
   приснилось, дрожишь, вскочил», и объясняет, что **от остановки ехать далеко — вот и уснул**.
4. Деревня мала для поездки — `docs/production/act1_village_expansion_plan_2026-09-29.md`.
5. Смоки `act1_first_person_walkthrough` (основной режим) и коридор устарели после A02 (телефон перенесён домой
   и т.п.) — не игровые дефекты.
Звуки процедурные, не прослушаны; татарские формулировки не проверены носителем.
