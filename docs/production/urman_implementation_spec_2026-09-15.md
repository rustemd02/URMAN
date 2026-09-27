# URMAN — Implementation Specification (передача coding-модели)

Версия документа: 2026-09-15. Базируется на: ТЗ на доработку (передано автором 2026-09-15),
аудите [`urman_ttz_compliance_audit_2026-09-15.md`](urman_ttz_compliance_audit_2026-09-15.md)
и верификации [`urman_ttz_audit_verification_2026-09-15.md`](urman_ttz_audit_verification_2026-09-15.md).

Ревизия кода, на которой всё проверено: `HEAD 1cc47a5` (ветка `main`).
**Если фактический код в репозитории отличается от этого документа — верен код, а не документ.**
Каждое расхождение implementer обязан отметить в отчёте по задаче.

Все пути в документе — относительно корня репозитория `/Users/kadyrahunov/Projects/URMAN`.
Помеченные `NEW FILE` пути **предлагаются этим документом** и в репозитории отсутствуют;
помеченные `EXISTS` — найдены в коде.

---

## 0. Сводка (что есть сейчас)

### 0.1. Движок и сборка

| Параметр | Значение |
| --- | --- |
| Движок | **Godot 4.7.1 (.NET, C#)**; `config/features=PackedStringArray("4.7","C#")` (`game/project.godot`) |
| Целевой фреймворк | `net10.0` (`game/Urman.Game.csproj`, `Directory.Build.props`) |
| Сборка игры | `Urman.Game` (assembly), Godot-проект лежит в подпапке `game/` |
| Главная сцена | `res://scenes/act1_demo.tscn` (`game/project.godot`, `run/main_scene`) |
| Раннеры | `eng/compile-game-content.sh`, `eng/run-act1-demo.sh`, `eng/run-smoke-guarded.sh`, `eng/export-desktop-release.sh` |
| Инструменты | Godot закреплён в `.tools/godot/Godot_mono.app/...` и dotnet в `.tools/dotnet` — **в этой среде отсутствуют**, поэтому реализацию нужно проверять на машине с ними |
| Экспорт | `game/export_presets.cfg` — macOS и Windows, `export_filter="all_resources"` внутри `game/` (то есть legacy `src/**` в поставку не попадает) |

### 0.2. Архитектура

| Слой | Путь | Роль |
| --- | --- | --- |
| Narrative-ядро | `src-dotnet/Urman.Core` | команды/события/состояние, `RuntimeKernel`, квесты, vocabulary, capabilities, детерминированные clock/RNG, persistence (`SaveGameV3`, `AtomicSaveGameStore`) |
| Контент-слой | `src-dotnet/Urman.Content` | компилятор `ContentCompiler`, схемы, резолверы текста/аудио, валидаторы |
| Godot-слой | `game/scripts`, `game/scenes` | composition root, мир, UI, аудио, сохранения |
| Источник контента | `content/**` | JSON/Markdown; компилируется в `game/content/urman.chapter1.compiled.v1.json` |
| Legacy web | `src/**` (TS/three.js) | **выведен из эксплуатации** (parity oracle), в поставку не входит; переиспользовать только как источник дизайна, не как код |
| Контентные тесты | `tests/**` (node) | `npm run content:check`, `npm run test:content`, `npm run test:runtime` — **сейчас красные, см. FIX-003** |
| Godot-смоуки | `game/tests/*.cs` (54 класса) | запускаются через `eng/run-smoke-guarded.sh` |

### 0.3. Основные сцены

- `game/scenes/act1_demo.tscn` → `Act1DemoRoot` (`game/scripts/Act1DemoRoot.cs`): меню, интро-панель,
  гейт модальности, клиффхэнгер, перф-проба.
- `game/scenes/main.tscn` → `Main` + дочерние: `RuntimeBridge`, `ZoneHost`, `Player`
  (`scenes/player/first_person_player.tscn`), `OldPcUi`, `DialogueUi`, `AudioCueUi`,
  `AmbientAudioDirector`, `FootstepAudioController`, `JournalUi`, `SettingsUi`, `DocumentUi`.
- Зоны Акта I (5): `village_day` (`style_benchmark_day_street.tscn`), `house_old_pc`
  (`style_benchmark_house_pc.tscn`), `fap_clinic` (`chapter1_fap_clinic.tscn`),
  `zirat_road` (`chapter1_zirat_road.tscn`), `kara_urman_night`
  (`style_benchmark_kara_urman_night.tscn`). Все собираются `StyleBenchmarkZone` по `ZoneKind`.
- Связанный мир: `Act1ConnectedWorld` (`game/scripts/Act1ConnectedWorld.cs` + partial-файлы) и
  `AgentBAct1ExteriorLayer` (киты GLB, террейн, погода, снег). Размещения и спавны — `Act1WorldLayout.cs`.
- Зоны актов 2–5 (`scenes/zones/fullgame/*.tscn`, `FullGameZone`+`FullGameZoneDressing`) — серые,
  используются только смоуками, из демо недоступны.

### 0.4. Player Controller (существующий)

`game/scripts/FirstPersonController.cs` (`partial class FirstPersonController : CharacterBody3D`),
сцена `game/scenes/player/first_person_player.tscn`:

- нода: `CharacterBody3D` (`collision_mask=3`) → `CollisionShape3D` (CapsuleShape3D r=0.35, h=1.8, y=0.9),
  `Head` (y=1.7) → `Camera3D` (fov 75, near 0.05) → `RayCast3D` (`target_position=(0,0,-2.7)`, `collision_mask=5`),
  `Hud` (`Reticle`, `InteractionPrompt`);
- `WalkSpeed = 3.4` (`:19`), гравитация из `physics/3d/default_gravity`, pitch ±82°,
  head bob (отключаемый), `ClampToAuthoredWorld()` (клампы и восстановление после падения);
- **прыжка нет** (действия `jump` в InputMap нет, `Velocity.Y` только убывает);
- **приседания нет** (в InputMap есть `crouch` = C / L3, и он даже переназначается в настройках —
  но его не читает ни один скрипт);
- **тела/ног/тени нет** (у игрока нет ни одного `MeshInstance3D`).

### 0.5. Система взаимодействий (существующая)

- `game/scripts/InteractionTarget.cs` (`partial class InteractionTarget : StaticBody3D`):
  экспорты `InteractionId`, `Prompt`, `TargetZoneId`, `TargetSpawnPointId`, `DialogueId`,
  `DocumentId`, `JournalEntryId`; внутренние `WorldFoleySample`, `PresentationRepeat`;
  `_Ready` → `RefreshAvailability()`; `Interact()` (async) → `RuntimeBridge.DispatchInteractionAsync`
  → открытие диалога/документа/журнала → `Main.SwitchZone(...)` → world-foley.
- Доступность приходит из контента: `RuntimeBridge.IsInteractionAvailable` (`RuntimeBridge.cs:389–405`)
  проверяет `conditions` интеракции и `entryConditions` целевой сцены.
- Роутинг лучей по зонам: `Act1ConnectedWorld.ApplyInteractionRouting` (`:606–630`) гасит слой
  недоступных целей; сбор целей — `FindDescendants<InteractionTarget>(zone)` (`:372–385`).
- Физические цели создаются `StyleBenchmarkZone.MakeInteractionBox(...)` (`:2056–2095`):
  обычные — `CollisionLayer=1`, `rayOnly` (находки) — `CollisionLayer=4`, не блокируют ход.
- HUD: перекрестие + контекстный промпт `[E] …` / `[A]`, состояние «Осмотрено».

### 0.6. UI-системы (существующие)

`JournalUi` (журнал: «Записи» + «Сопоставить», цели, словарь), `OldPcUi` (экран «АРХИВ КЫРЛАЙ»:
поиск/список/читалка/сохранить), `DocumentUi` (читалка документов в зоне), `DialogueUi`,
`AudioCueUi` (реплики с субтитрами/транскриптами), `SettingsUi` (FOV, чувствительность, head bob,
доступность, графика, переназначение 10 действий, save/load), `PauseMenuUi`, `MainMenuUi`
(Новая игра/Продолжить/Настройки/Об игре/Отладка: локации). Модальность — `player.SetModalOpen(bool)`.

### 0.7. Логика деревни (существующая)

- Мир: улица, возвратная улица, двор бабая, ФАП, зиратская дорога, кромка Кара-Урмана, мечеть
  (фасад/двор), река со сломанным мостом; киты `urman_village_exterior_kit.glb`,
  `urman_wet_village_road_kit.glb`, `urman_fap_clinic_kit.glb`, `urman_zirat_roadside_kit.glb`.
- NPC (6): Алсу, Гөлсинә, Мансур, Наиля, Ринат, Тимур хәзрәт (повороты к игроку, стадирование Рината).
- Контент: `content/modules/urman-chapter1/definitions.json` — 233 записи, 58 интеракций
  (24 `discover-*`, 17 переходов, 9 `compare-*`, 6 диалогов, `oldpc-power`, `village-sign`).
- Находки: 25 статических целей + фабрика `DiscoveryTarget` (ещё 25), часть с анимациями предметов.

### 0.8. Логика леса (существующая)

`KaraForestEdgeKit` (`Act1ConnectedWorld.cs:72–91`) + ночная зона `kara_urman_night`;
контент-сцены `scene/forest-approach` → `scene/forest` (в `onEnter` два `audio.request`:
голос Марата и вмешательство Рината); клиффхэнгер `beat/cliffhanger-hard-cut`.
**Леса как отдельной зоны нет, транспорта в лес нет, сущности Шурале нет.**

### 0.9. Состояние транспорта

**Отсутствует полностью.** Ни `RigidBody3D`, ни `VehicleBody3D`, ни моделей. Единственные следы:
квитанция про Ниву в архиве ПК и заметки о приёме радио (тексты).

### 0.10. Состояние компьютера бабая

- Capability `urman.oldpc` — `content/modules/urman-oldpc/{module.json,capabilities.json,definitions.json,schemas/,documents/*.md}`
  (26 документов, 9 разделов `pcSection`), провайдер `src-dotnet/Urman.Core/Capabilities/OldPc/OldPcCapabilityProvider.cs`
  (команды `search/open/save/section`), инстанс создаётся в `RuntimeBridge.CreateCapabilities` (`:1030–1042`),
  UI — `game/scripts/OldPcUi.cs` + `game/scenes/ui/old_pc_ui.tscn` (один экран, **навигации по
  разделам из UI нет**, хотя `section` поддержан в capability).
- Полноценный desktop (ярлыки/панель/часы/приложения) существует только в legacy TS `src/os/**`
  и в поставку не входит.

### 0.11. Состояние записной книжки

`JournalUi` — модальная панель по `J`: записи (журнал улик), вкладка сопоставления, цели, словарь.
Отметки «✓/🔒» — в архиве ПК. Разделов «Персонажи/Адреса/Наблюдения» нет, свободных заметок нет,
предмета в мире нет.

### 0.12. Состояние ключевых локаций

| Локация | Состояние |
| --- | --- |
| Дом бабая | Есть: интерьер с печью, стол, ПК, 2 NPC, вход/выход интеракциями, находки |
| ФАП | Есть: интерьер (кушетка, ширма, шкаф, лоток, регистратура), фельдшер Наиля, документы, внешний кит |
| Мечеть | Частично: зал, купол, двор, закрытая дверь (`Act1ConnectedWorld.cs:8025–8130`); интерьера и интерактива нет; имам — на улице |
| Зират | Частично: придорожная граница с маркерами и воротами, физически открыт внутрь, но без имён/интерактива |
| Лес | Частично: кромка + ночная зона + аудио-финал |
| Баня | Нет (только участок `BanyaYard` в ките) |
| Магазин | Нет (только пустая контент-сцена `selsmag_counter_interior`) |
| Школа | Нет |
| Сельсовет/ДК | Нет в Акте I |
| Автобусная остановка | Нет (старт — спавн `village_day@arrival` на дороге у въезда) |

---

## 1. Правила реализации (обязательны для всех задач)

1. **Не переписывать существующее без доказанной необходимости.** Расширять:
   `FirstPersonController`, `InteractionTarget`, `JournalUi`, `OldPcUi`, `RuntimeBridge`,
   `StyleBenchmarkZone`/`Act1ConnectedWorld`. Полная замена допустима только с объяснением
   в отчёте: что ломает текущая архитектура, что заменяется, какие зависимости затрагиваются.
2. **Запрещено ломать контракты:**
   - стабильные content-id (`urman.chapter1:…`, `urman.oldpc:…`) — переименование только с миграцией;
   - формат `SaveGameV3` (`src-dotnet/Urman.Core/Persistence/SaveGameV3.cs`, `CurrentSchemaVersion = 3`);
     добавление полей — аддитивно, при необходимости бампа версии **со чтением версии 3**
     (иначе сломаются существующие сохранения; `:114–117` сейчас просто отвергает чужие версии);
   - `RuntimeBridge` остаётся **единственным** владельцем нарративного состояния
     (`knowledge/journal/quests/vocabulary/npc/beats`); новые системы пишут туда через эффекты
     контента или через capability-снимки, а не в собственные файлы;
   - существующие 54 Godot-смоука и JS-тесты должны оставаться зелёными (кроме известной
     красноты FIX-003, которую эта спецификация закрывает).
3. **PLACEHOLDER ACCEPTABLE.** Если нет финального арта/звука/UI-дизайна — делать систему на
   плейсхолдерах (Godot-примитивы, `BoxMesh`/`CylinderMesh`, `Label3D`, пустые `AudioStream`),
   не блокировать задачу. В каждой задаче ниже отмечено, где это допускается.
4. **Data-driven.** Текст, списки, документы, товары, объявления, надгробья, посты — в `content/**`,
   не в C#. Состояние — в нарративном состоянии или capability-снимках.
5. **Новые типы контента** добавляются в **два** места: `scripts/content/compile-content.mjs`
   (`REGISTRY_BY_KIND`, стр. 8–19) + `content/schemas/<kind>.schema.json`, и
   `src-dotnet/Urman.Content/Compilation/ContentCompiler.cs` (`RegistryByKind`, стр. 21–33)
   + `content/schemas/compiled-content-pack.schema.json` (список `registries` и `required`).
6. **Новые capability** копируют существующий образец `urman.oldpc`: манифест модуля +
   `capabilities.json` + 5 под-схем (`*-config/state/command/event/outcome.schema.json`) +
   провайдер `ICapabilityProvider` в `src-dotnet/Urman.Core/Capabilities/<Name>/` + регистрация
   в `RuntimeBridge.CreateCapabilities` + сохранение через `_capabilities.CaptureAll()`.
7. **Новые InputMap-действия** требуют: записи в `game/project.godot [input]`, в
   `InputBindingService.RemappableActions`, подписи в `SettingsUi.ActionLabels` **и** задачи FIX-004
   (иначе сохранённые настройки/сейвы игрока упадут при старте).
8. **После каждой задачи:** `eng/compile-game-content.sh` (если менялся контент),
   `npm run content:check` (после FIX-003 обязателен), релевантные Godot-смоуки из `game/tests/`,
   `graphify update .`; в отчёте — список изменённых файлов и результат прогонов.
9. **Коммиты** — по одной задаче (или логической паре), сообщение в стиле репозитория
   (`feat(act1): …`, `fix(act1): …`), без смешивания несвязанных правок.

---

## 2. Фазы и зависимости

```
PHASE 0  Предпосылки (FIX-001..005)      ← обязательна до остального
   │  (FIX-004 нужен для PLAYER-001/003; FIX-003 — для любой работы с контентом;
   │   FIX-001/002 — дешёвые исправления существующих дефектов)
   ▼
PHASE 1  Player core (PLAYER-001..007)
   ▼
PHASE 2  Interaction framework (INTERACT-001, KNOCK-001/002, INTERACT-002)
   ▼
PHASE 3  Notebook (NOTE-001..003)
   ▼
PHASE 4  Babay computer (PC-001..007)    ← самая большая подсистема; зависит от CORE-001/DATA-001
   ▼
PHASE 5  Village locations (START-001, MOSQUE-*, FAP-001, SHOP-*, SCHOOL-001, COUNCIL-001, ZIRAT-001, BANYA-001)
   ▼
PHASE 6  Vehicles (VEH-001..004)         ← зависит от PHASE 5 (парковки) и RADIO-001 (радио в машине)
   ▼
PHASE 7  Radio (RADIO-001/002)           ← RADIO-001 можно делать параллельно PHASE 4
   ▼
PHASE 8  Forest / cart / Shurale (FOREST-001, SHURALE-001)
```

Сквозные задачи: `CORE-001` (общий просмотрщик), `CORE-002` (lore database для книжки),
`DATA-001` (схемы контента), `SAVE-001` (правила сохранения) — делать по мере входа
соответствующих фаз, не отдельным большим спринтом.

**Почему такой порядок.** Фаза 0 закрывает три реальных дефекта и один «капкан»: добавление
новых InputMap-действий без FIX-004 валит старт у игроков с сохранёнными настройками;
правки контента без FIX-003 невозможно проверять автоматически. Player core идёт раньше
интеракций, потому что присед/прыжок меняют параметры капсулы и высоты камеры, от которых
зависят новые дверные проёмы и посадочные места транспорта. Компьютер — самый объёмный и
наименее зависимый блок, его выгодно делать после книжки (общий просмотрщик и категории).

---

# PHASE 0 — Предпосылки

## FIX-001 — Сделать фотографию Марата достижимой

**Priority:** P0
**Current state:** `ArrivalPhotoTarget` создаётся с **пустым** `interactionId`:
`game/scripts/Act1ConnectedWorld.ExteriorDiscoveries.cs:77–83` —
`village.MakeInteractionBox("ArrivalPhotoTarget", …, "665b49", "", "Посмотреть фотографию", documentId: "urman.chapter1:document/arrival-photo-evidence", rayOnly: true)`.
`InteractionTarget.RefreshAvailability` (`game/scripts/InteractionTarget.cs:164–187`) получает
`IsInteractionAvailable("") == false` (пустой id не найден в `_interactionsById`,
`game/scripts/CompiledCampaignRepository.cs:214–215`), поэтому цепь такая: `CollisionLayer=0`
→ `Interact()` делает ранний выход (`:70–74`) → `Act1ConnectedWorld.ApplyInteractionRouting`
(`:606–630`, условие `IsAvailable()` на `:619`) гасит цель повторно. Документ в паке есть
(`game/content/urman.chapter1.compiled.v1.json`, `registries/documents[0]`), но на него не
ссылается ни одна сущность пака.
**Problem:** заявленная механика M1 («память Марата — действие игрока») в билде недостижима:
карточки в журнале нет, два факта скрыты; тест `game/tests/JournalFlowSmokeTest.cs:159`
проверяет только программное открытие документа.
**Required implementation:**
1. Добавить в `content/modules/urman-chapter1/definitions.json` интеракцию
   `urman.chapter1:interaction/arrival-photo` с `targetDocumentId` = существующим документом
   (`urman.chapter1:document/arrival-photo-evidence`) и без условий (`conditions: []`),
   чтобы её можно было вызвать сразу на въезде. Если схема интеракций требует `labelTextId` —
   использовать существующий текст (`urman.chapter1:text/inspect`) или добавить новый
   `text/arrival-photo-prompt`.
2. В `ExteriorDiscoveries.cs` заменить `""` на id этой интеракции.
3. Если интеракция должна быть «мировой» (не привязана к активной сцене) — добавить
   `worldLocations: ["village_day"]` по образцу `discover-*`; тогда роутинг включит цель в
   `village_day`.
4. Прогнать `eng/compile-game-content.sh`, чтобы пересобрать
   `game/content/urman.chapter1.compiled.v1.json`.
5. Добавить Godot-смоук (или расширить существующий обходной тест
   `game/tests/Act1FirstPersonWalkthroughSmokeTest.cs`): дойти до скамейки на въезде,
   навести луч на фото, нажать `E`, проверить: открылся `DocumentUi` с документом,
   в состоянии подтверждены `knowledge/memory_marat_childhood_photo`,
   `knowledge/memory_marat_kazansky_ne_otstavay`, в журнале появилась запись.
**Files to modify:** `content/modules/urman-chapter1/definitions.json`,
`game/scripts/Act1ConnectedWorld.ExteriorDiscoveries.cs`,
`game/content/urman.chapter1.compiled.v1.json` (пересборка),
`game/tests/Act1FirstPersonWalkthroughSmokeTest.cs` (или новый тест).
**New files/components:** — (можно добавить текст `text/arrival-photo-prompt` в тот же definitions.json).
**Existing code to reuse:** механизм `discover-*`-целей (`Act1ConnectedWorld.Exploration.cs:112–123`,
`DiscoveryTarget`), `DocumentUi`, `RuntimeBridge.OpenDocumentAsync`.
**Existing code to remove/deprecate:** ничего.
**Dependencies:** FIX-003 (чтобы прогон `content:check` был возможен).
**Scene/inspector setup (Godot):** ничего в сценах; цель создаётся кодом.
**Runtime behavior:** подход к скамейке → промпт «Посмотреть фотографию» → `E` → открывается
читалка документа → после закрытия в журнале есть запись, на книжке нет отметки «Осмотрено»
(документ, а не find).
**Edge cases:** документ уже прочитан (повторный осмотр не должен дублировать запись журнала —
`journal.record` идемпотентен по `entryId`); игрок нажал `E` до загрузки моста (цель просто
недоступна — существующее поведение).
**Acceptance criteria:**
- в связанном мире `ArrivalPhotoTarget.IsAvailable() == true` (после `ApplyInteractionRouting` слой не нулевой);
- нажатие `E` на цель открывает `DocumentUi` с `arrival-photo-evidence`;
- после открытия в состоянии `knowledge/memory_marat_childhood_photo` = `confirmed`;
- в журнале ровно одна запись по этому документу при повторных открытиях;
- новый/расширенный смоук проходит на чистом userdata.
**Manual test:** новая игра → интро → дойти до скамейки у въезда (справа от дороги) →
навести на фотографию → `E` → прочитать → `J` → проверить запись «Детская фотография».

---

## FIX-002 — Починить отладочные переходы «Мечеть» и «подход к лесу»

**Priority:** P1
**Current state:** `game/scripts/MainMenuUi.cs:44–54` объявляет 8 переходов; кнопки называются
`DebugZone_{zone}_{spawn}` (`:418`). В связанном мире `Main.SwitchZone` (`game/scripts/Main.cs:98–111`)
требует спавн из `Act1WorldLayout` (`game/scripts/Act1WorldLayout.cs:43–93`). Для двух записей
спавнов нет: `("kara_urman_night","forest-approach")` (`MainMenuUi.cs:52`; в layout у зоны только
`village_path`/`default`) и `("village_day","mosque")` (`:53`; в layout только `arrival`,
`from_house`, `from_fap`, `from_forest`, `default`; «mosque» есть лишь в несвязанной ветке
`Main.cs:247–248`). При нажатии печатается `GD.PushError("… no mapped spawn … transition aborted")`
и переход прерывается, но `Act1DemoRoot.OnMenuStartDebugZoneAsync` (`game/scripts/Act1DemoRoot.cs:859–873`)
уже начал новый сеанс и после вызова всё равно записывает запрошенные zone/spawn в мост.
Покрытие: `game/tests/Act1MainMenuSmokeTest.cs:303` проверяет только `village_path`.
**Problem:** две кнопки отладочного меню обещают переход и не выполняют его; состояние моста
расходится с фактической позицией игрока (важно для отладки и для будущих задач по локациям).
**Required implementation:**
1. Объявить в `Act1WorldLayout` недостающие спавны: для `village_day` — `"mosque"` в точке перед
   воротами мечети (комплекс стоит вокруг якоря `(-46, y, -34)`, `Act1ConnectedWorld.cs:970–979`;
   спавн из `Main.cs:248` — `(-40, 0.05, -34)`, yaw 265°; перенести эти значения как world-space
   точку с учётом `Placements[0].Origin`), для `kara_urman_night` — либо `"forest-approach"` как
   алиас `village_path`, либо заменить в меню на `village_path`.
2. Провести те же спавны в несвязанную ветку `Main.SpawnTransform` (единообразие).
3. Убрать «ложную запись» в `Act1DemoRoot.OnMenuStartDebugZoneAsync`: если `SwitchZone` не сменил
   `_main.ActiveZoneScenePath`, не записывать `bridge.CurrentZoneId/CurrentSpawnPointId` и показать
   статус в меню (`_mainMenu?.ShowStatus(...)`).
4. Расширить `Act1MainMenuSmokeTest`: пройти по **всем** записям `MainMenuUi.DebugZones`
   и проверить, что каждая приводит к `bridge.CurrentZoneId == zone` и
   `bridge.CurrentSpawnPointId == spawn`.
**Files to modify:** `game/scripts/Act1WorldLayout.cs`, `game/scripts/Main.cs`,
`game/scripts/MainMenuUi.cs` (при выборе варианта «заменить алиасом»),
`game/scripts/Act1DemoRoot.cs`, `game/tests/Act1MainMenuSmokeTest.cs`.
**New files/components:** —
**Existing code to reuse:** `Act1WorldLayout.SpawnPoints(...)` (`:191–201`), существующий тест меню.
**Existing code to remove/deprecate:** —
**Dependencies:** —
**Scene/inspector setup (Godot):** —
**Runtime behavior:** каждая кнопка «Отладка: локации» телепортирует игрока в объявленную точку
и не даёт прогресса (существующее правило).
**Edge cases:** флаг `user://debug-zones.enabled` отсутствует → меню обычное (не ломать);
неизвестный спавн → статус об ошибке, а не молчаливый сброс сеанса.
**Acceptance criteria:** (обновлено 2026-09-27: переходов 12, источник точки — владелец
места, поэтому литеральная проверка `Act1WorldLayout.TryGetWorldSpawn` заменена на
`Act1ConnectedWorld.TryGetWorldSpawn` после сборки мира; см. журнал решений)
- для всех записей `DebugZones` выполняется `Act1ConnectedWorld.TryGetWorldSpawn(zone, spawn, out _) == true`;
- после нажатия любой кнопки `bridge.CurrentZoneId/CurrentSpawnPointId` совпадают с записью,
  и позиция игрока совпадает со спавном (проверка через `Main.ActiveZoneScenePath` + координаты);
- смоук меню проходит.
**Manual test:** с файлом `debug-zones.enabled` в userdata вызывать по очереди все пункты и
убедиться (по логу `zone-loaded: …` и позиции), что каждый работает.

---

## FIX-003 — Развязать контентные модули и вернуть зелёный контент-гейт

**Priority:** P0
**Current state:** `npm run content:check` падает; `npm run test:content` — 62/68, `npm run test:runtime` — 99/102.
Причина одна: `urman.chapter1` ссылается на документы `urman.oldpc` в
`journalAction.sourceIds` (`content/modules/urman-chapter1/definitions.json`, указатель
`/169/interactions/*/journalAction/sourceIds/*`; 9 экшенов), а манифест объявляет
`"dependencies": []` (`content/modules/urman-chapter1/module.json:25`). Валидатор:
`scripts/content/compile-content.mjs:496` и `:728`. Обратная зависимость уже есть —
`urman.oldpc` зависит от `urman.chapter1` (`content/modules/urman-oldpc/module.json:51–56`),
поэтому простое объявление зависимости даст цикл (циклы фатальны, тест
«module dependency cycles are fatal»). Тест-контракт требует, чтобы chapter1 **не** упоминал
oldpc: `tests/content/urman-chapter1/chapter1-data.test.mjs:59–64`. В поставляемом паке
ссылки есть (9 из 9 `journalAction` ссылаются на `urman.oldpc:document/*`) — .NET-компилятор
кампании это пропускает, JS-аудит модуля — нет. Ссылки внесены коммитом `401f5bd`.
`content:check` не вызывается ни одним скриптом `eng/`.
**Problem:** контент-гейт красный и не автоматизирован: любая правка данных не проверяется,
расхождение контракта модулей накапливается незаметно (это уже произошло).
**Required implementation (выбрать один вариант и зафиксировать в `decision_log.md`):**
- **Вариант A (предпочтительный, минимальные правки данных):** перенести 9 `journalAction`
  из `scene/investigation-journal` в новый модуль `content/modules/urman-investigation/`
  (`module.json` с `dependencies: [urman.chapter1, urman.oldpc]`, `definitions.json` с
  интеракциями и их `journalAction`), включить модуль в кампанию
  `content/campaigns/urman.chapter1/campaign.json` (`modules` и `narrativeOrder` при необходимости);
  `scene/investigation-journal` оставить как «presentation»-сцену без экшенов; сценарии
  сравнения и `activeScene`-условия при этом не меняются (сравнения идут через
  `RuntimeBridge.CompareJournalSourcesAsync`, который не зависит от сцены).
- **Вариант B:** разрешить chapter1 объявить зависимость от oldpc и развернуть граф так,
  чтобы `urman.oldpc` не зависел от chapter1 (его документы ссылаются на knowledge/character
  chapter1 в `accessConditions`/`knowledgeRefs` — придётся вынести эти ссылки или переопределить
  их в отдельном модуле; дороже и рискованнее).
1. Реализовать выбранный вариант.
2. Добиться: `npm run content:check` — 0 диагностик; `npm run test:content` — 68/68;
   `npm run test:runtime` — 102/102.
3. Прогнать `eng/compile-game-content.sh` и убедиться, что оба пака собрались и
   `game/content/*.compiled.v1.json` изменились предсказуемо (только ожидаемые поля).
**Files to modify:** `content/modules/urman-chapter1/definitions.json`,
`content/campaigns/urman.chapter1/campaign.json`, `content/modules/urman-chapter1/module.json`
(если вариант B), `tests/content/urman-chapter1/chapter1-data.test.mjs` (если контракт меняется
по решению — тогда правка теста обязана быть отражена в `decision_log.md`).
**New files/components (NEW FILE, предлагаемые пути — в репозитории отсутствуют):**
`content/modules/urman-investigation/module.json`, `content/modules/urman-investigation/definitions.json`
(вариант A).
**Existing code to reuse:** компилятор кампании (`ContentCompiler`), манифест oldpc как образец
модуля с зависимостями, `RuntimeBridge.CompareJournalSourcesAsync` (`:417–427`).
**Existing code to remove/deprecate:** `journalAction`-блоки внутри
`scene/investigation-journal` (переносятся, не удаляются по смыслу).
**Dependencies:** — (делать до любой работы с контентом).
**Scene/inspector setup (Godot):** —
**Runtime behavior:** не меняется: сравнение источников по-прежнему доступно после получения
обеих улик; журнал показывает те же 9 выводов.
**Edge cases:** загрузка старых сохранений (сравнения уже применены) — состояние в ядре,
пересборка контента его не ломает; `fingerprint` кампании изменится → старые сейвы станут
несовместимыми по `CampaignFingerprint` (`SaveGameV3` валидирует отпечаток). **Учесть это**:
если сохранения нужны — зафиксировать новый отпечаток в релизной заметке.
**Acceptance criteria:**
- `content:check` зелёный;
- `test:content` и `test:runtime` — 100% pass;
- в паке `game/content/urman.chapter1.compiled.v1.json` те же 9 сравнений доступны
  (проверить смоуком `game/tests/ChapterOneFlowSmokeTest.cs`, который проходит сравнение);
- запись в `decision_log.md` о выбранном варианте.
**Manual test:** в игре дойти до сопоставления «справка ↔ реестр» и убедиться, что вывод
по-прежнему появляется.

---

## FIX-004 — Толерантное применение биндингов и миграция настроек

**Priority:** P0 (блокирует PLAYER-001 и PLAYER-003)
**Current state:** `game/scripts/InputBindingService.cs:34–55` — `Apply()` бросает
`InvalidDataException($"Saved input bindings are missing action {action}.")`, если в сохранённых
биндингах нет любого действия из `RemappableActions`. `FirstPersonController._Ready`
(`game/scripts/FirstPersonController.cs:158–161`) вызывает `ApplySettings(storedSettings)` при
наличии `user://settings.json`; `ApplySettings` → `InputBindingService.Apply(settings.InputBindings)`
без try/catch. Загрузка сейва делает то же (`RuntimeBridge` → `player.ApplySettings`).
**Problem:** как только в `RemappableActions` появится новое действие (`jump`, `sprint`),
у любого игрока с существующим `settings.json` или сейвом v3 старт упадёт
(`ArgumentNullException`/`InvalidDataException` внутри `_Ready` → общий catch в
`Act1DemoRoot.InitializeDemo` → алерт «Не удалось запустить Акт I» и `Quit(1)`).
**Required implementation:**
1. В `InputBindingService.Apply` заменить бросок на «дозаполнение по умолчанию»:
   если действия нет в сохранённых данных — взять его из `_defaults` (вызвав
   `InitializeDefaults()`), пропустив отсутствующие, и один раз залогировать
   `GD.PushWarning("input-bindings: filled missing action <action> from defaults")`.
2. Неизвестные действия в сохранённых данных игнорировать (обратная совместимость).
3. Добавить тест в `game/tests/Act1UserSettingsSmokeTest.cs` (или новый):
   записать `settings.json` без нового действия → загрузить → приложение не падает,
   новое действие получает дефолт; и обратный тест: лишнее действие в файле не ломает загрузку.
4. Проверить, что `UserSettingsStore.CurrentVersion` (сейчас `1`) и `SaveGameV3.CurrentSchemaVersion`
   (сейчас `3`) при этом менять не требуется.
**Files to modify:** `game/scripts/InputBindingService.cs`,
`game/tests/Act1UserSettingsSmokeTest.cs` (или новый тест-файл).
**New files/components:** — (при желании — новый тест `game/tests/Act1InputBindingMigrationSmokeTest.cs`, NEW FILE).
**Existing code to reuse:** `InitializeDefaults()`/`Capture()` (`:22–32`), `Act1BindingConflictSmokeTest`.
**Existing code to remove/deprecate:** строгий бросок в `Apply`.
**Dependencies:** —
**Scene/inspector setup (Godot):** —
**Runtime behavior:** старое `settings.json`/сейв загружается, недостающие биндинги берутся из
проектных дефолтов; пользователь видит свои прежние настройки.
**Edge cases:** файл настроек повреждён (текущее поведение: `TryLoad` вернёт `null` — не менять);
сейв v3 без действия (то же дозаполнение при `ApplySettings` после загрузки).
**Acceptance criteria:**
- с `settings.json`, в котором нет действия из `RemappableActions`, игра стартует и логирует warning;
- биндинг-конфликт-смоук и смоук настроек проходят;
- `InputBindingService.RestoreDefaults()` по-прежнему восстанавливает проектные дефолты.
**Manual test:** удалить из `settings.json` одно действие → запустить демо → игра стартует,
в настройках действие показано с дефолтной клавишей.

---

## FIX-005 — Подсказка взаимодействия на геймпаде

**Priority:** P3
**Current state:** `interact` привязан к `E`, кнопкам `button_index=1` (B) и `0` (A)
(`game/project.godot [input]`); `FirstPersonController.RefreshInteractionHints`
(`:551–586`) берёт **первую** найденную кнопку → в игре показывается `[B]`,
тогда как интро-панель говорит «A — начать / осмотреть» (`Act1DemoRoot.cs:1127–1129`).
**Problem:** расхождение подсказок путает при игре на геймпаде.
**Required implementation:** выбирать кнопку для подсказки по приоритету
`JoyButton.A` → `B` → `X` → `Y` (или явно сортировать по `ButtonIndex`, поставив `A` первым);
то же для клавиатуры — оставить `E`. Обновить текст интро, если решено иначе.
**Files to modify:** `game/scripts/FirstPersonController.cs` (`RefreshInteractionHints`),
при необходимости `game/scripts/Act1DemoRoot.cs`.
**New files/components:** —
**Existing code to reuse:** существующая логика форматирования подсказки.
**Existing code to remove/deprecate:** —
**Dependencies:** —
**Scene/inspector setup (Godot):** порядок событий в `interact` при желании поменять прямо
в `project.godot` (тогда правка кода не нужна) — выбрать одно решение, не оба.
**Runtime behavior:** подсказка `[A]` на геймпаде, `[E]` на клавиатуре.
**Edge cases:** геймпад без кнопки A (нестандартный) — падать на существующий fallback `[Gamepad]`.
**Acceptance criteria:** в игре на геймпаде подсказка взаимодействия = `[A]`; интро и подсказка
согласованы; смоук настроек/подсказок зелёный.
**Manual test:** подключить геймпад, подойти к двери дома, сравнить подсказку с интро.

---

# PHASE 1 — Player core

Общее для фазы: все правки — в `game/scripts/FirstPersonController.cs` (EXISTS, 591 строка) и
`game/scenes/player/first_person_player.tscn` (EXISTS). Новые файлы — только partial-классы и
сцены, перечисленные в задачах. Ни один существующий публичный член контроллера не удаляется:
на него опираются смоуки (`Act1FirstPersonWalkthroughSmokeTest`, `Act1FirstPersonCorridorSmokeTest`,
`CollisionQaSmokeTest`, `Act1SpawnMatrixSmokeTest`, `Act1BindingConflictSmokeTest` и др.).

## PLAYER-001 — Физический прыжок

**Priority:** P0
**Current state:** действия `jump` нет в `game/project.godot [input]`; в
`FirstPersonController._PhysicsProcess` (`:266–295`) вертикаль меняется только гравитацией
(`Velocity.Y - _gravity * delta` при `!IsOnFloor()`), положительного импульса нет.
`IsOnFloor()` уже используется для гравитации и head bob.
**Problem:** ТЗ §2.1 требует прыжок; сейчас его нет ни в input, ни в коде.
**Required implementation:**
1. Добавить InputMap-действие `jump`: `Space` (physical keycode 32) + кнопка геймпада `A`
   (`button_index=0`). **Одновременно убрать кнопку `A` из действия `interact`** (оставить `E` и `B`),
   иначе на геймпаде прыжок и взаимодействие сработают вместе. Обновить подпись интро
   (`Act1DemoRoot.UpdateIntroControls`, `:1119–1129`) на «A — прыжок · B — осмотреть»
   (или, если автор захочет иначе, поменять местами; правка — одна строка).
2. Добавить `jump` в `InputBindingService.RemappableActions` (`game/scripts/InputBindingService.cs:8–20`)
   и в `SettingsUi.ActionLabels` (`game/scripts/SettingsUi.cs:35–46`, подпись «Прыжок»).
   **Без FIX-004 это ломает старт у игроков со старыми настройками** — FIX-004 обязателен раньше.
3. В `FirstPersonController` добавить экспортируемые параметры:
   `JumpVelocity` (`[Export] float`, по умолчанию `4.6`), `CoyoteSeconds` (`0.12`),
   `JumpBufferSeconds` (`0.15`), `AirControlFactor` (`0.6`).
4. Реализовать в `_PhysicsProcess` (между расчётом `direction` и `MoveAndSlide()`):
   - обновлять `_coyoteTimer` (пока `IsOnFloor()` → `CoyoteSeconds`, иначе убывать);
   - `Input.IsActionJustPressed("jump")` → `_jumpBufferTimer = JumpBufferSeconds`;
   - если `_jumpBufferTimer > 0 && _coyoteTimer > 0 && !_modalOpen` → `Velocity.Y = JumpVelocity`,
     обнулить оба таймера, `_coyoteTimer = 0`;
   - гравитацию применять как сейчас (она уже даёт правильную параболу).
5. Не менять `ClampToAuthoredWorld()`, `SetModalOpen`, head bob и `ApplySmokeLook` — тесты на них опираются.
**Files to modify:** `game/project.godot`, `game/scripts/FirstPersonController.cs`,
`game/scripts/InputBindingService.cs`, `game/scripts/SettingsUi.cs`, `game/scripts/Act1DemoRoot.cs`.
**New files/components:** — (при желании тест: `game/tests/Act1JumpSmokeTest.cs`, NEW FILE).
**Existing code to reuse:** текущая ветка гравитации, `IsOnFloor()`, тест-хелперы
`Act1FirstPersonWalkthroughSmokeTest` (движение по waypoint-ам).
**Existing code to remove/deprecate:** кнопка `A` в действии `interact` (переносится на `jump`).
**Dependencies:** FIX-004.
**Scene/inspector setup (Godot):** новых нод не требуется; `JumpVelocity` тюнится в инспекторе
инстанса игрока в `scenes/main.tscn`.
**Runtime behavior:** стоя на земле → `Space`/`A` → подъём ≈1.0 м и мягкое приземление;
в воздухе повторное нажатие ничего не даёт; при приседе прыжок разрешён и прерывает присед
(см. PLAYER-002).
**Edge cases:** прыжок в модальном окне (журнал/ПК) — заблокирован (existing early return);
прыжок у края authored-окна — `ClampToAuthoredWorld` вернёт в мир, падение глубже 2.5 м лечится
`FallRecoveries`; прыжок на лестницу/порог — допустим, капсула не застревает (проверить QA-смоуком).
**Acceptance criteria:**
- нажатие `jump` на земле даёт ровно один прыжок (высота 0.8–1.2 м при дефолте);
- в воздухе повторные нажатия не дают второго прыжка;
- при удержании направления в прыжке игрок сохраняет горизонтальную скорость (изменённая
  `AirControlFactor` управляет только корректировкой);
- `Act1FirstPersonWalkthroughSmokeTest` и `CollisionQaSmokeTest` проходят;
- биндинг `jump` виден и переназначается в настройках.
**Manual test:** демо → New Game → интро → `Space`/`A`: одиночный прыжок у дома бабая,
двойной прыжок невозможен, приземление без проваливания.

---

## PLAYER-002 — Физическое приседание (капсула + камера + просвет)

**Priority:** P0
**Current state:** действие `crouch` существует (`project.godot`, строка 65: `C` / `L3`) и
переназначается в настройках (`InputBindingService.cs:15`, `SettingsUi.cs:40` — «Пригнуться»),
но **ни один скрипт его не читает**. Капсула — `CapsuleShape3D` `height=1.8`, `radius=0.35`
на `y=0.9` (`first_person_player.tscn:5–15`), `Head` на `y=1.7`; высота не меняется.
**Problem:** ТЗ §2.2 требует реального приседания (камера + капсула + проход под низким);
сейчас UI обещает механику, которой нет.
**Required implementation:**
1. В `FirstPersonController` добавить параметры: `StandHeight` (`1.8`), `CrouchHeight` (`1.2`),
   `CrouchHeadHeight` (`1.05`), `CrouchSpeedFactor` (`0.55`), `CrouchTransitionSeconds` (`0.15`),
   `CrouchMode` (`enum { Hold, Toggle }`, default `Hold`), `JumpCancelsCrouch` (`true`).
2. При `_Ready` **скопировать** форму капсулы в уникальный ресурс перед изменением
   (`_capsule = (CapsuleShape3D)_collision.Shape.Duplicate();`, затем `_collision.Shape = _capsule;`) —
   в Godot под-ресурсы сцены могут шариться между инстансами, менять их на месте нельзя.
3. Логика в `_PhysicsProcess` перед расчётом движения:
   - `_crouchWanted = CrouchMode == Hold ? Input.IsActionPressed("crouch") : toggle-state`;
   - если `JumpCancelsCrouch` и сработал прыжок → `_crouchWanted = false`;
   - если `!_crouchWanted` и сейчас присед → проверить просвет `CanStandUp()`:
     `PhysicsShapeQueryParameters3D` с капсулой стоя (r=0.35, h=1.8, центр y=0.9), `CollisionMask = CollisionMask`
     игрока, `Exclude = [GetRid()]`, `GetWorld3D().DirectSpaceState.IntersectShape(...)` → если попадание, остаться в приседе;
   - целевые значения: высота капсулы, `y` центра (`h/2`), `Head.Position.Y` (1.7 ↔ 1.05),
     множитель скорости (`CrouchSpeedFactor`);
   - переход — `Tween` длительностью `CrouchTransitionSeconds`; при `ReducedMotion`
     (`Accessibility.ReducedMotion`) — мгновенно (как в `Main.PlayZoneTransition`, `Main.cs:213–221`).
4. `_headBasePosition` обновлять при смене позы, чтобы head bob считался от новой высоты
   (`UpdateHeadBob`, `:348–362`).
5. Подсказка/настройки: `crouch` уже есть в UI, менять подписи не нужно.
**Files to modify:** `game/scripts/FirstPersonController.cs`,
`game/scenes/player/first_person_player.tscn` (только если решено задавать дефолты в сцене),
`game/scripts/SettingsUi.cs` (опционально — переключатель Hold/Toggle, тогда + сохранение в `GameSettingsSnapshot`).
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/FirstPersonController.Crouch.cs`
(partial-класс, если не хочется раздувать основной файл), `game/tests/Act1CrouchSmokeTest.cs`.
**Existing code to reuse:** `Tween`-паттерн из `Main.PlayZoneTransition`, `Accessibility.ReducedMotion`,
готовая настройка биндинга `crouch`.
**Existing code to remove/deprecate:** —
**Dependencies:** FIX-004 (если `crouch` остаётся в `RemappableActions` — он там уже есть).
**Scene/inspector setup (Godot):** у инстанса `Player` в `scenes/main.tscn` появляются новые
экспортируемые поля (высоты, время перехода); форма капсулы в `first_person_player.tscn`
остаётся исходной (её меняет код).
**Runtime behavior:** удержание `C`/`L3` → камера опускается до 1.05 м, капсула 1.2 м,
скорость падает примерно вдвое; под низким выступом (например, водопропуск у зирата,
`Act1ConnectedWorld.CulvertVerandaDiscoveries.cs`) игрок проходит, не проваливаясь и не застревая;
отпускание под препятствием не встаёт, пока просвет не свободен.
**Edge cases:** присед в прыжке (капсула не увеличивается в воздухе, вставание только после
приземления и проверки просвета); прыжок из приседа (разрешён, прерывает присед);
присед в модальном окне (движение запрещено — поза не меняется); присед на порог/ступени
(капсула не должна просаживаться под пол: нижняя точка капсулы = `h/2` от центра).
**Acceptance criteria:**
- при удержании `crouch` высота `CapsuleShape3D` = 1.2 ± 0.01, `Head.Position.Y` = 1.05 ± 0.02;
- под авторизованным низким проёмом игрок проходит; без приседа упирается;
- при отпускании под проёмом игрок остаётся в приседе; после выхода — встаёт;
- скорость в приседе ≈ `WalkSpeed * CrouchSpeedFactor`;
- при `ReducedMotion` переход мгновенный;
- существующие смоуки зелёные.
**Manual test:** в доме бабая присесть и встать у стены; в зиратском водопропуске пройти
присев; выйти из-под проёма и убедиться, что игрок встал только на открытом месте.

---

## PLAYER-003 — Тюнинг движения и спринт

**Priority:** P1
**Current state:** `WalkSpeed = 3.4` (`FirstPersonController.cs:19`), скорость задаётся мгновенно
(`:285`), ускорения/торможения и бега нет (`sprint`/`run` в InputMap отсутствуют).
**Problem:** ТЗ §2.3 требует нормальной настройки ходьбы и «более быстрого перемещения,
если предусмотрено»; сейчас движение «телепортно-резкое», бега нет вообще.
**Required implementation:**
1. Добавить `Acceleration` (`8.0` м/с²) и `Deceleration` (`12.0` м/с²); горизонтальную скорость
   менять через `Vector2.MoveToward` по направлению, а не присваиванием.
2. Добавить действие `sprint` (`Shift`, physical keycode 4194325 — проверить точное значение
   в Godot 4.7, либо назначить через редактор; геймпад — `RIGHT_SHOULDER`, `button_index=10`)
   и параметр `RunSpeed` (`5.2`). Добавить `sprint` в `InputBindingService.RemappableActions`
   и `SettingsUi.ActionLabels` («Бег»).
3. Спринт запрещён в приседе и в модальном состоянии; при приседе скорость = `WalkSpeed * CrouchSpeedFactor`.
4. **Важно:** не менять дефолтный `WalkSpeed` и не делать спринт обязательным для прохождения —
   обходной смоук `Act1FirstPersonWalkthroughSmokeTest` вымеряет маршрут по waypoint-ам с W;
   если после ускорения тест начнёт «перелетать» точки, уменьшить `Acceleration`, а не менять
   waypoint-логику теста.
**Files to modify:** `game/project.godot`, `game/scripts/FirstPersonController.cs`,
`game/scripts/InputBindingService.cs`, `game/scripts/SettingsUi.cs`.
**New files/components:** —
**Existing code to reuse:** существующая сборка `direction` из `Input.GetVector`, хелперы тестов.
**Existing code to remove/deprecate:** прямое присваивание `Velocity = new Vector3(direction.X * WalkSpeed, …)`.
**Dependencies:** FIX-004, PLAYER-002 (присед ограничивает спринт).
**Scene/inspector setup (Godot):** новые экспортируемые параметры на инстансе `Player`.
**Runtime behavior:** игрок плавно разгоняется до 3.4 м/с, при удержании `Shift` — до 5.2 м/с;
в приседе — ≈1.9 м/с; торможение плавное, но быстрое.
**Edge cases:** спринт в воздухе (горизонтальная скорость сохраняется, `AirControlFactor` ограничивает
управление); спринт у стены (без «скольжения» вдоль стены — обычная физика `MoveAndSlide`);
спринт при открытом журнале (заблокирован модальным состоянием).
**Acceptance criteria:** при удержании `sprint` установившаяся скорость ≈5.2 м/с, без — ≈3.4 м/с;
разгон занимает ≈0.4 с; обходной смоук и QA-коллизий проходят; `sprint` переназначается в настройках.
**Manual test:** пробежать улицу без Shift и с Shift, сравнить время и ощущение; проверить,
что в приседе бег не работает.

---

## PLAYER-004 — Видимое тело и ноги в POV

**Priority:** P0
**Current state:** игрок — капсула без геометрии: в `first_person_player.tscn` нет ни одного
`MeshInstance3D`; ни один скрипт не добавляет меш (проверено по группе `player_controller`).
При взгляде вниз игрок не видит ничего своего.
**Problem:** ТЗ §3 требует видимые ноги/нижнюю часть тела; сейчас это «летающая камера».
**Required implementation:**
1. В `first_person_player.tscn` добавить узел `BodyRig` (`Node3D`, `y = 0`) с дочерними:
   `Legs` (`MeshInstance3D` ×2: плейсхолдер — `CapsuleMesh` r 0.09, h 0.75, позиции `x = ∓0.11`,
   `y = 0.42`, `z = 0`), `Boots` (плейсхолдер `BoxMesh`), опционально `Torso`.
   **PLACEHOLDER ACCEPTABLE**: до финального арта допустимы примитивы с материалами из
   `PainterlyMaterialLibrary.ForColor("<hex>", "fabric")`.
2. Позиционировать меши так, чтобы верх ног был за пределами `near` камеры (0.05) при
   взгляде вниз: ориентир — верхняя точка ног не выше `y ≈ 0.95`, камера на `1.7` (`1.05` в приседе).
3. Скрывать/показывать по углу взгляда: если `_pitch < -25°` и не модально — ноги видимы,
   иначе видимы (вариант «всегда видимы» тоже допустим, но тогда обязательно проверить
   отсутствие пересечения с камерой при приседе).
4. Ноги должны быть привязаны к позе: в приседе `BodyRig` опускается вместе с капсулой
   (использовать те же целевые высоты, что в PLAYER-002), в прыжке — остаются на месте
   относительно тела (тело не «летит» отдельно от игрока).
5. `Legs` не участвуют в коллизиях (никаких `CollisionShape3D` в `BodyRig`).
**Files to modify:** `game/scenes/player/first_person_player.tscn`
(новые ноды `BodyRig/Legs`), `game/scripts/FirstPersonController.cs` (видимость + привязка позы).
**New files/components (NEW FILE, предлагаемые пути):** `game/scenes/player/first_person_body.tscn`
(если тело выносится в отдельную сцену-инстанс), `game/scripts/FirstPersonBodyRig.cs` (управление
видимостью и позой, если логика не помещается в контроллер).
**Existing code to reuse:** `PainterlyMaterialLibrary`, хелперы `AddVisualBox`-стиля из
`Act1ConnectedWorld.Exploration.cs:125–147` (как образец создания мешей кодом).
**Existing code to remove/deprecate:** —
**Dependencies:** PLAYER-002 (высоты приседа), PLAYER-005 (тень использует тот же риг).
**Scene/inspector setup (Godot):** ноды `BodyRig` → `Legs`/`Boots`; материал — `StandardMaterial3D`
или `PainterlyMaterialLibrary`; `cast_shadow = ON` для `Legs` (см. PLAYER-005).
**Runtime behavior:** при взгляде вниз игрок видит две ноги, стоящие на земле; в приседе ноги
ближе к камере и ниже; в прыжке ноги видны до приземления; при ходьбе ноги слегка смещаются
(см. PLAYER-006).
**Edge cases:** взгляд вниз в приседе (ноги не должны перекрывать весь экран — проверить
`CrouchHeadHeight` 1.05 и верх ног 0.95); бег (ноги не «уезжают» вперёд); модальные окна
(ноги не мешают UI — они в 3D-слое, UI поверх); смена FOV 65–90 (ноги не должны «обрезаться»).
**Acceptance criteria:**
- в первом кадре взгляда вниз видны обе ноги (проверяется кадровым тестом: `capture`-смоук
  с `pitch = -60°`, `docs`-эвиденс);
- при приседе ноги опускаются вместе с камерой и не пересекают `near`-плоскость;
- в прыжке ноги остаются видимыми до приземления;
- ни один QA-смоук коллизий не падает (меши не имеют коллайдеров).
**Manual test:** стоя у дома бабая посмотреть вниз, присесть, прыгнуть — ноги ведут себя
предсказуемо, не «мигают» и не проваливаются.

---

## PLAYER-005 — Тень игрока

**Priority:** P1
**Current state:** у игрока нет геометрии, поэтому нет и тени; солнце мира включено с тенями
(`Act1ConnectedWorld.cs:1443–1450`, `sun.ShadowEnabled = true`), тени домов/заборов видны.
**Problem:** ТЗ §3 и §21 требуют нормальной тени персонажа; «бестелесный» игрок выбивается из
освещённой сцены.
**Required implementation:**
1. Использовать возможности Godot 4: у меша-прокси `LegsShadowProxy` (`MeshInstance3D`,
   упрощённая капсула/силуэт) установить
   `CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly` и
   `ExtraCullMargin` (≥ 1.0). Такой меш **не рисуется** камерой, но отбрасывает тень —
   это заменяет «тело в кадре» там, где видимое тело пока не готово.
2. Дополнительно (для ночной зоны `kara_urman_night`, где солнце слабое — `LightEnergy 0.55`,
   `ShadowOpacity 0.38`) добавить мягкий «блоб»-плейсхолдер: `Decal` (`DecalTexture` — радиальный
   альфа-градиент, NEW texture `game/assets/textures/act1/player_shadow_blob_v1.png`,
   **PLACEHOLDER ACCEPTABLE**) либо `MeshInstance3D` с `QuadMesh` и альфа-материалом на `y = 0.02`,
   который следует за игроком (проекция на землю, `RotationDegrees.X = -90`), с прозрачностью,
   зависящей от того, стоит ли игрок на земле (в прыжке — меньше, дальше — слабее).
3. Блоб включается только когда игрок на земле (`IsOnFloor()`), иначе плавно гаснет.
**Files to modify:** `game/scenes/player/first_person_player.tscn`, `game/scripts/FirstPersonController.cs`.
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/PlayerShadowBlob.cs` (следование за игроком и затухание),
`game/assets/textures/act1/player_shadow_blob_v1.png` (плейсхолдер-градиент).
**Existing code to reuse:** `ShadowsOnly` — встроенная возможность Godot; существующее солнце;
`SnowTrampleField` (`game/scripts/SnowTrampleField.cs`) как образец «мира, реагирующего на игрока».
**Existing code to remove/deprecate:** —
**Dependencies:** PLAYER-004 (прокси-меши того же рига).
**Scene/inspector setup (Godot):** `LegsShadowProxy` с `cast_shadow = ShadowsOnly`;
`PlayerShadowBlob` — `MeshInstance3D`/`Decal` в мире или ребёнком `Player`, `y = 0.02`,
`render_priority` выше снега.
**Runtime behavior:** в солнечном свете игрок отбрасывает длинную зимнюю тень; в прыжке
тень остаётся на земле; в ночной зоне видно мягкое пятно под ногами.
**Edge cases:** интерьеры (блоб не должен светиться сквозь пол — включать только на улице,
признак — `Interior` у текущего размещения из `Act1WorldLayout`); наклонный рельеф
(блоб ориентируется по нормали через `RayCast3D` вниз, иначе плавает); несколько источников
света (ShadowsOnly дублирует тень от каждого включённого источника — допустимо).
**Acceptance criteria:** на улице днём у игрока есть тень; при прыжке тень остаётся на земле;
в интерьере блоб выключен; в ночной зоне виден мягкий контакт; кадровый смоук фиксирует
тень (сравнение кадров «до/после» PLAYER-005).
**Manual test:** выйти из дома, посмотреть на снег под собой, прыгнуть; зайти в дом — пятна нет.

---

## PLAYER-006 — Интеграция анимации тела

**Priority:** P2
**Current state:** анимации игрока нет вовсе (только head bob камеры, `:348–362`).
Ноги/тело (PLAYER-004) статичны.
**Problem:** ТЗ §3/§21 требует, чтобы тело «правильно реагировало на ходьбу» и приседание.
**Required implementation:**
1. Ввести в контроллере машину состояний тела: `Idle`, `Walk`, `Run`, `CrouchIdle`, `CrouchWalk`,
   `Jump`, `Land`. Переходы — по `input.LengthSquared()`, `IsOnFloor()`, `_crouchWanted`,
   событию прыжка/приземления.
2. Вариант A (без арта, **PLACEHOLDER ACCEPTABLE**): процедурная анимация в
   `FirstPersonBodyRig`: покачивание ног по синусоиде (`phase += delta * speed * k`),
   амплитуда 0.06–0.10 м, противофаза для левой/правой; в приседе — амплитуда ×0.6;
   в прыжке ноги подтягиваются на 0.05 м вверх.
3. Вариант B (когда появится риг): `AnimationPlayer` + клипы, `AnimationTree` (StateMachine);
   контроллер выставляет только строковый/целочисленный параметр состояния. Клипы —
   `PLACEHOLDER ACCEPTABLE`.
4. Синхронизировать шаги: `FootstepAudioController` уже определяет поверхность по зоне/пространству
   (`game/scripts/FootstepAudioController.cs`); фазу шага брать из анимации (в варианте A —
   из процедурной фазы), чтобы звук и движение совпадали; при `ReducedMotion` амплитуда
   процедурной анимации = 0, звук шагов остаётся.
5. Приземление: одноразовый «просад» тела на 0.03 м за 0.15 с (только если
   `!Accessibility.ReducedMotion`).
**Files to modify:** `game/scripts/FirstPersonController.cs`,
`game/scripts/FirstPersonBodyRig.cs` (NEW, если создан в PLAYER-004),
`game/scenes/player/first_person_player.tscn`.
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/FirstPersonBodyRig.cs`,
`game/animation/player_body_clips.tres` (вариант B, плейсхолдер).
**Existing code to reuse:** `FootstepAudioController` (поверхности и тайминг), `Accessibility.ReducedMotion`.
**Existing code to remove/deprecate:** —
**Dependencies:** PLAYER-002 (присед), PLAYER-004 (тело), PLAYER-001 (прыжок).
**Scene/inspector setup (Godot):** `BodyRig` с экспортируемой ссылкой в контроллере
(`[Export] Node3D BodyRig`), либо `GetNode` по имени (предпочтительно).
**Runtime behavior:** при ходьбе ноги двигаются в такт шагам; в приседе — медленнее и ниже;
в прыжке — подтянуты; при приземлении короткая просадка.
**Edge cases:** смена направления на месте (анимация не «дёргается» — фаза продолжается);
ходьба спиной вперёд (та же анимация); `ReducedMotion` (без движения, звук есть);
одновременный прыжок и присед (состояние `Jump` приоритетнее).
**Acceptance criteria:** фаза шага совпадает со звуком (проверка: кадровый смоук + счётчик
шагов `FootstepAudioController`); при `ReducedMotion` нет движения ног, звук остаётся;
нет дрожания при смене состояний.
**Manual test:** пройти 20 м, присесть и пройти ещё 10 м, прыгнуть — визуально и на слух
движение согласовано.

---

## PLAYER-007 — Интеграция камеры (присед, прыжок, head bob)

**Priority:** P1
**Current state:** `Head` жёстко на `y=1.7`; head bob смещает `Head.Position` от
`_headBasePosition` (`:348–362`); pitch клампится ±82° (`:341–346`); FOV 65–90 (`:224`).
**Problem:** при приседе (PLAYER-002) и прыжке (PLAYER-001) камера должна вести себя предсказуемо,
а head bob не должен конфликтовать с высотой приседа.
**Required implementation:**
1. Ввести единственный источник высоты камеры: `_targetHeadHeight` (1.7 / 1.05) и применять его
   в `_headBasePosition`, а head bob — как смещение **от** текущей базы, а не от жёстких 1.7.
2. При приземлении после прыжка — короткий «кик» камеры вниз на 0.04 м и обратно за 0.18 с
   (выключается при `ReducedMotion`); при прыжке — без кика.
3. Не менять `ApplySmokeLook`, `CapturePortableTransform`, `ApplyPortableTransform` —
   на них опираются смоуки и сохранения (в `ApplyPortableTransform` уже сбрасывается head bob).
4. Проверить, что `SetModalOpen(true)` не оставляет камеру в промежуточной позе:
   при закрытии UI поза восстанавливается к текущей цели (присед сохраняется).
**Files to modify:** `game/scripts/FirstPersonController.cs`.
**New files/components:** —
**Existing code to reuse:** существующий head bob, `ReducedMotion`, `_headBasePosition`.
**Existing code to remove/deprecate:** жёсткая константа 1.7 в `_Ready`/head bob (заменяется на
`_headBasePosition` из параметров).
**Dependencies:** PLAYER-001, PLAYER-002.
**Scene/inspector setup (Godot):** у инстанса `Player` — параметры высот (см. PLAYER-002);
нода `Head` остаётся на `y=1.7` как дефолт сцены.
**Runtime behavior:** вход в присед — плавное опускание камеры; выход — плавный подъём;
прыжок — камера едет вместе с телом, приземление — короткий кик; head bob работает в обеих позах.
**Edge cases:** присед во время head bob (смещение пересчитывается, нет рывка); присед при
открытии/закрытии модального окна; `ReducedMotion` (без кика и без bob).
**Acceptance criteria:** высота камеры в приседе 1.05 ± 0.02; при выходе из приседа возврат
к 1.7 ± 0.02; кик приземления длится < 0.25 с и отключается настройкой reduced motion;
`ApplySmokeLook`-смоуки зелёные.
**Manual test:** присесть/встать у стены, прыгнуть с крыльца, открыть журнал в приседе и закрыть.

---

# PHASE 2 — Interaction framework

## INTERACT-001 — Единая логика дверей (состояния) без переписывания системы

**Priority:** P0
**Current state:** двери — статичные меши без состояний: `StyleBenchmarkZone.cs:1819`
(дверь бенчмарка), `Act1ConnectedWorld.cs:7526, 9505` (двери сараев), `:3531–3542`
(закрытая листва фасада дома: «authored closed leaf visible; entry stays an interaction»),
`:8116` (дверь мечети наглухо). Вход в дом — `InteractionTarget` `HouseDoor`
(`StyleBenchmarkZone.cs:316–328`, промпт «Войти в дом бабая и әби», фоли `door_creak`),
выход — `HouseExit` (`:670–678`). Доступность уже управляется контентом через
`conditions`/`entryConditions` (`RuntimeBridge.IsInteractionAvailable`, `:389–405`).
**Problem:** ТЗ §4.2 требует понятной системы состояний (открыта/закрыта/заперта/стук/без стука/
недоступна); сейчас каждая дверь — частный случай, состояний не видно игроку.
**Required implementation:**
1. Расширить `InteractionTarget` (не заменять!) partial-файлом
   `game/scripts/InteractionTarget.Door.cs` (NEW FILE):
   - `[Export] public DoorMode DoorMode { get; set; }` — `enum { Auto, Transition, Animated }`
     (`Transition` = текущее поведение с `TargetZoneId`; `Animated` = поворот створки на 75–85°
     за 0.4 с без смены зоны; `Auto` = открыть и оставить открытой);
   - `[Export] public NodePath LeafPath` — узел створки для `Animated`;
   - `[Export] public string LockedPrompt = "Заперто"` — текст, когда цель недоступна из-за условий;
   - `[Export] public string UnlockHint = ""` — подсказка, что нужно сделать (можно пусто).
2. Состояние двери **не дублировать в новом хранилище**: «заперта/доступна» определяется
   контентом (`conditions`, `knowledge.status`, `npc.state`, `journal`), как сейчас; новый код
   только отображает состояние и анимирует створку. Это сохраняет совместимость сейвов.
3. Отображение состояния в HUD: `FirstPersonController.UpdateInteraction` (`:377–414`) уже
   показывает «Осмотрено» для завершённых находок; добавить ветку: если цель — дверь и недоступна,
   показывать `LockedPrompt`/`UnlockHint` вместо пустоты (для этого в `InteractionTarget`
   добавить свойство `IsDoor` и `LockedText`, а в контроллер — чтение).
4. Перевести существующие двери на новую модель:
   - дом бабая: `HouseDoor`/`ReturnToHouseRegister`/`HouseExit` → `Transition` (поведение не меняется,
     добавить только `LeafPath`, если решено анимировать створку перед переходом);
   - дверь мечети (`MosqueEntranceDoor`) — сделать отдельный `InteractionTarget` со
     `DoorMode = Animated`, `LockedPrompt = "Дверь закрыта. Ключа нет."`, доступность — по
     `knowledge/clue_*` (например, после разговора с имамом), **интерьер мечети — отдельная задача
     MOSQUE-001**, здесь только створка и состояние;
   - сараи/калитки (`Act1ConnectedWorld.BabaiYardSideGate.cs:122`,
     `BypassDiscoveries.cs:89, 219`, `FapExploration.cs:138`) — уже имеют `WorldFoleySample = "door_creak"`,
     оставить как есть, при желании перевести в `Animated`.
5. Единый звук открытия: `door_creak` (уже в поставке, `game/assets/audio/act1/foley/door_creak.wav`);
   для металлических ворот добавить `gate_metal.wav` — **PLACEHOLDER ACCEPTABLE**
   (можно временно тот же `door_creak`).
**Files to modify:** `game/scripts/InteractionTarget.cs` (точка расширения: `Interact()`, `RefreshAvailability`),
`game/scripts/FirstPersonController.cs` (показ состояния), `game/scripts/StyleBenchmarkZone.cs`
(двери дома), `game/scripts/Act1ConnectedWorld.cs` (дверь мечети, калитки).
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/InteractionTarget.Door.cs`,
`game/scripts/DoorLeafAnimator.cs` (твин створки, если не помещается в partial).
**Existing code to reuse:** условия/эффекты контента, `WorldFoleySample` + `UiFoley.PlayWorld`,
`InteractionTarget.TargetZoneId/TargetSpawnPointId`, `Act1ConnectedWorld.ApplyInteractionRouting`.
**Existing code to remove/deprecate:** ничего (статические двери без интерактива остаются декором).
**Dependencies:** PLAYER-002 (проёмы/капсула), KNOCK-001 (запрет автостука).
**Scene/inspector setup (Godot):** у дверных целей — `DoorMode`, `LeafPath`, `LockedPrompt`;
створки домов/мечети вынести в отдельные `MeshInstance3D`-узлы (сейчас часть створок — меши
внутри фасадных китов; для мечети створка уже отдельный меш `MosqueEntranceDoor`, `Act1ConnectedWorld.cs:~8118`).
**Runtime behavior:** подход к двери ничего не делает; `E` на закрытой двери — открытие
(зона/анимация) и звук; `E` на запертой — HUD показывает «Заперто», звука нет; повторное `E`
на открытой `Animated`-двери — закрытие.
**Edge cases:** дверь открыта (повторный `E` не дублирует переход — существующее правило
«цель недоступна после срабатывания»); игрок в приседе у двери (капсула уже 1.2 м, проём ≥ 1.9 м);
дверь, открытая с обеих сторон (переход в другую зону уже реализован);
игрок стоит в проёме во время анимации створки (створка не должна толкать/застревать игрока —
анимация визуальная, коллизия створки не создаётся).
**Acceptance criteria:**
- ни одна дверь не реагирует на простое приближение (см. KNOCK-001);
- у запертой двери HUD показывает текст, а не пустоту;
- `Animated`-дверь открывается/закрывается визуально, зона не меняется;
- `Transition`-двери (дом, ФАП, зиратская дорога, лес) продолжают работать как сейчас;
- существующие смоуки (`Act1AudioTransitionSmokeTest`, `ChapterOneFlowSmokeTest`) зелёные.
**Manual test:** подойти к двери дома — тишина; нажать `E` — переход внутрь; подойти к двери
мечети — «Дверь закрыта. Ключа нет.»; калитка сарая — открывается на месте со скрипом.

---

## KNOCK-001 — Гарантия отсутствия автоматического стука

**Priority:** P0
**Current state:** автоматического стука **нет**: `Area3D`/`body_entered`/`get_overlapping_bodies`/
`intersect_shape` в `game/scripts` отсутствуют; `UiFoley.PlayWorld` вызывается ровно из одного
места — `InteractionTarget.Interact` (`game/scripts/InteractionTarget.cs:152`); в поставке
4 фоли-сэмпла (`game/assets/audio/act1/foley`: `door_creak`, `keyboard_key`, `paper_open`, `ui_click`),
сэмпла стука не существует; `Main.SwitchZone` проигрывает только визуальный фейд
(`Main.cs:184–236`), звука нет; история git реализации стука не содержит.
**Problem:** требование ТЗ §4.1/§28 («убрать автоматический стук») в этом коде выполнять нечего —
но это нужно **закрепить механизмом**, чтобы механика не появилась при будущих правках
(двери, транспорт, NPC).
**Required implementation:**
1. Инструментировать `UiFoley` (`game/scripts/UiFoley.cs`) тест-видимым счётчиком:
   `public static int PlayedSampleCount { get; }`, `public static IReadOnlyList<string> PlayedSamples { get; }`
   (сбрасывается методом `ResetForTests()`), без влияния на продакшн-поведение.
2. Новый Godot-смоук `game/tests/InteractionProximityGuardSmokeTest.cs` (NEW FILE):
   - загрузить демо, пройти маршрут от спавна `village_day@arrival` до двери дома
     (`AgentBAct1Layout.HouseDoorApproach`) **без нажатия `E`** и постоять там 3 с;
   - за это время `UiFoley.PlayedSamples` не должен содержать ни одного сэмпла
     (допустимы только UI-клики, которых не было) — то есть «подход = тишина»;
   - отдельно: пройти мимо всех `InteractionTarget` в радиусе 1.5 м (`FindDescendants<InteractionTarget>`)
     и убедиться, что ни один не перешёл в доступное/сработавшее состояние (`IsAvailable()` как был);
   - скан сцены: `FindChildren("*", "Area3D", true, false)` в зонах — при появлении любого `Area3D`
     тест падает с сообщением «proximity-триггеры запрещены правилом KNOCK-001; используйте InteractionTarget».
3. Добавить правило в `AGENTS.md`-стиле проекта: новый файл
   `docs/production/interaction_rules.md` (NEW FILE) с формулировкой: «любое звуковое/нарративное
   событие двери, дома или транспорта обязано исходить из `InteractionTarget.Interact()`
   или из эффекта контента; `Area3D`-триггеры и `_Process`-проверки расстояния для этого запрещены».
**Files to modify:** `game/scripts/UiFoley.cs` (инструментация).
**New files/components (NEW FILE, предлагаемые пути):**
`game/tests/InteractionProximityGuardSmokeTest.cs`, `docs/production/interaction_rules.md`.
**Existing code to reuse:** `FindDescendants<InteractionTarget>` (образец — `Act1ConnectedWorld.cs:372–385`),
тест-инфраструктура `game/tests` (`Act1FirstPersonCorridorSmokeTest` как образец ходьбы по маршруту).
**Existing code to remove/deprecate:** ничего (удалять нечего — это подтверждено верификацией).
**Dependencies:** INTERACT-001 (чтобы тест покрывал и новые дверные режимы).
**Scene/inspector setup (Godot):** —
**Runtime behavior:** не меняется; приближение к двери всегда бесшумно.
**Edge cases:** фоновые звуки (шаги, амбиент) не считаются фоли — счётчик считает только `UiFoley`;
снег/шаги идут через `FootstepAudioController` и в счётчик не попадают.
**Acceptance criteria:**
- новый смоук проходит на чистом userdata;
- в `game/scripts` и `game/scenes` нет ни одного `Area3D` в игровых зонах (проверяется тем же смоуком);
- `PlayedSamples` пуст при проходе мимо дверей без `E`.
**Manual test:** пройти вдоль всех домов улицы — ни одного стука; нажать `E` у двери — слышен скрип.

---

## KNOCK-002 — Ручной стук как сознательное действие

**Priority:** P1
**Current state:** стука нет ни в каком виде; у дома есть только вход/выход; у NPC нет реакции
на стук. `WorldFoleySample` + `UiFoley.PlayWorld` — готовый механизм звука по интеракции
(`InteractionTarget.cs:152`), `PresentationRepeat` — готовый механизм локального повтора без
записи в журнал (`:42–43, 78–88`).
**Problem:** ТЗ §4.1 требует, чтобы стук существовал как намеренное действие игрока.
**Required implementation:**
1. Контент (в `content/modules/urman-chapter1/definitions.json`): добавить интеракции
   `interaction/knock-<door>` для 3–5 дверей первого акта (дом бабая — «дом, куда можно постучать»,
   ФАП, дом Рината/Фап-соседа, калитка двора). Каждая:
   `labelTextId: text/knock-prompt` («Постучать»), `conditions: []`, `effects: []`,
   опционально `targetDialogueId` (ответ из-за двери) и/или `effects: [beat.set-state …]`.
2. В коде: новый partial `game/scripts/InteractionTarget.Knock.cs` (NEW FILE) с
   `[Export] public string KnockFoleySample = "knock";`, `[Export] public float KnockCooldownSeconds = 1.2f`,
   `[Export] public bool KnockIsRepeatable = true;`
   Логика: если у цели задан `PresentationRepeat`-путь (существующий механизм), повторный стук
   разрешён, но не чаще кулдауна; звук — `UiFoley.PlayWorld(host, GlobalPosition, KnockFoleySample)`.
3. Звук: добавить `game/assets/audio/act1/foley/knock.wav` — **PLACEHOLDER ACCEPTABLE**:
   временно допускается `ScriptAudioStream`/запись короткого тука; регистрация — там же, где
   остальные фоли (`UiFoley` и `tests`-инвентарь фоли, если он есть в смоуках).
4. Ответ мира (опционально, но желательно для «сознательного действия»): у стука на двери дома —
   диалог `urman.chapter1:dialogue/knock-house-answer` с одной репликой әби («Сейчас, сейчас…»),
   который ничего не меняет в прогрессе; у ФАП — реплика Наили. Контент — в
   `definitions.json`, без новых систем.
5. Цель стука должна быть **отдельным** `InteractionTarget` рядом с дверью (например, смещён
   на 0.15 м в сторону створки), чтобы игрок мог выбрать: стучать или войти. Промпты:
   «Постучать» и «Войти».
**Files to modify:** `content/modules/urman-chapter1/definitions.json`,
`game/scripts/StyleBenchmarkZone.cs` (создание целей стука у дома/ФАП),
`game/scripts/InteractionTarget.cs` (только если нужен кулдаун в базовом классе).
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/InteractionTarget.Knock.cs`,
`game/assets/audio/act1/foley/knock.wav` (плейсхолдер),
`content/modules/urman-chapter1/text/` в `definitions.json` (`text/knock-prompt`, `text/knock-answer-*`).
**Existing code to reuse:** `PresentationRepeatAvailable`/`PresentationRepeat`
(`InteractionTarget.cs:42–43, 78–88`), `WorldFoleySample`, `UiFoley.PlayWorld`,
`DialogueUi`/`RuntimeBridge.OpenDialogueUi`.
**Existing code to remove/deprecate:** —
**Dependencies:** INTERACT-001, KNOCK-001.
**Scene/inspector setup (Godot):** у новых целей — `InteractionId` (контентный), `Prompt`
(«Постучать»), `WorldFoleySample = "knock"`, `KnockCooldownSeconds`; позиция — у створки.
**Runtime behavior:** подойти → промпт «Постучать» → `E` → звук тука и (если задано) реплика;
повторный `E` в пределах кулдауна игнорируется; прогресс не меняется; подход без `E` — тишина.
**Edge cases:** быстрый двойной `E` (кулдаун гасит второй тук); стук при открытом диалоге
(модальное состояние блокирует движение, повторный стук недоступен); стук в дом, где игрок
уже внутри (цель снаружи, недостижима — норм); `ReducedMotion` (звук остаётся, UI-анимация нет).
**Acceptance criteria:**
- приближение к двери без `E` не издаёт звука (см. KNOCK-001);
- `E` на цели «Постучать» даёт ровно один звук тука и не меняет прогресс;
- повторный `E` раньше кулдауна не даёт второго звука;
- ответ мира (если задан) показывается через `DialogueUi` и не пишет в журнал/знания.
**Manual test:** подойти к дому бабая, постучать дважды быстро — один тук; подождать, постучать
снова — звук есть; после стука войти в дом.

---

## INTERACT-002 — Доработка общей системы взаимодействий

**Priority:** P1
**Current state:** `InteractionTarget` уже единый (промпт, доступность по контенту, фоли,
переходы зон, документы, диалоги, находки). Чего нет: удержания, типов целей, отладочной телеметрии,
подсказок «почему недоступно».
**Problem:** новым системам (компьютер, магазин, транспорт, школа) понадобятся удержание
(тяжёлые ворота), различение типов (для туториалов/эффектов) и видимая причина недоступности.
**Required implementation:**
1. `[Export] public float HoldSeconds { get; set; } = 0f;` + прогресс-подсказка в HUD
   (`FirstPersonController.SetInteractionPrompt`) для «удерживай E».
2. `[Export] public InteractableKind Kind { get; set; }` — `enum { Generic, Door, Document,
   Computer, Vehicle, ShopCounter, Npc, Discovery }` (используется UI и телеметрией, не влияет
   на прогресс).
3. `PromptKind`: если цель недоступна из-за условий, но у неё есть `LockedPrompt` — показывать его
   (общий механизм для INTERACT-001 и магазина/школы).
4. Опциональный «фокус по времени»: если в радиусе луча больше одной цели, сейчас работает
   эвристика двух кадров (`FocusSwitchFrames = 2`, `:16`); вынести настройку наружу
   (`[Export] int FocusSwitchFrames`) и добавить угол-приоритет (выбирать цель, наиболее
   близкую к центру луча).
**Files to modify:** `game/scripts/InteractionTarget.cs`, `game/scripts/FirstPersonController.cs`.
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/InteractionTarget.Types.cs`
(если решено держать enum и логику типов отдельно).
**Existing code to reuse:** вся текущая логика доступности/промптов/луча.
**Existing code to remove/deprecate:** —
**Dependencies:** INTERACT-001.
**Scene/inspector setup (Godot):** новые экспортируемые поля у целей; значения по умолчанию
не меняют поведение существующих 28 целей.
**Runtime behavior:** существующие интеракции работают как раньше; новые получают удержание и
тип; при недоступности видна причина (если задана).
**Edge cases:** удержание и отпускание (прогресс сбрасывается), удержание через переход зоны
(цель освобождается — прогресс сбрасывается), несколько целей под лучом (угловой приоритет).
**Acceptance criteria:** все существующие смоуки зелёные; новые поля по умолчанию не меняют
поведение (проверяется прогоном `Act1FirstPersonWalkthroughSmokeTest` без изменений);
удержание работает на тестовой цели (тест `game/tests/InteractionHoldSmokeTest.cs`, NEW FILE).
**Manual test:** удержание на тяжелой калитке (после того как она появится) — прогресс-подсказка,
отпустил — сброс.

---

# PHASE 3 — Notebook (diegetic-записная книжка)

## NOTE-001 — Модель данных книжки и категории

**Priority:** P1
**Current state:** `game/scripts/JournalUi.cs` (EXISTS) + `game/scenes/ui/journal_ui.tscn` — журнал
улик: вкладки «Записи»/«Сопоставить» (`:205–256`), цели (`RuntimeBridge.ActiveObjectives`, `:461–520`),
словарь (`RuntimeBridge.LearnedVocabulary`, `:351–377`), записи (`RuntimeBridge.JournalEntries`, `:441–453`).
Данные приходят из нарративного состояния (`journal`, `knowledge`, `quests`, `vocabulary`).
Доступ к реестру персонажей из Godot **отсутствует** (`CompiledCampaignRepository` отдаёт тексты,
аудио, документы, vocabulary, quests, но не characters).
**Problem:** ТЗ §5 требует разделов/категорий и ощущения «личной книжки», а не одного списка улик.
**Required implementation:**
1. Определить категории как **проекции существующих данных** (не новое хранилище):
   - **Заметки (Notes)** — `knowledge` с `kind == "discovery"` (24 записи `discovery-*` в контенте);
   - **Люди (People)** — реестр `characters` (8 записей) + знания `clue_*`/`memory_*`, у которых
     `sourceIds`/`reveals` ссылаются на персонажа; портрет — `assetRefs` персонажа
     (`content/modules/urman-chapter1/logical/portrait-*.ref`);
   - **Адреса (Addresses)** — NEW: новый тип контента `place` (см. DATA-001) либо, до его появления,
     статический список из `scene`-записей с координатами; у каждой записи — связанные знания/документы;
   - **Задачи (Tasks)** — `quests`/objectives (уже есть проекция `ActiveObjectives`);
   - **Наблюдения (Observations)** — записи журнала, пришедшие из документов/сообщений
     (`journal.record` с `sourceId`, начинающимся на `urman.oldpc:document/`), плюс тексты
     `audio-*`-улик.
2. Добавить в `CompiledCampaignRepository` (EXISTS, `game/scripts/CompiledCampaignRepository.cs`)
   аксессор `public IReadOnlyList<CompiledCharacterContent> Characters` по образцу
   `VocabularyEntries` (`:195`) и `Quests` (`:230`), читая `registries.characters` из пака.
   Если типа `CompiledCharacterContent` нет — NEW FILE `game/scripts/CompiledCharacterContent.cs`
   (или добавить в существующий файл репозитория).
3. Добавить в `RuntimeBridge` (EXISTS) три проекции без изменения состояния:
   `NotebookNotes()`, `NotebookPeople()`, `NotebookObservations()` (по образцу `JournalEntries()`,
   `:441–453`), плюс `NotebookPlaces()` — из нового реестра `places` (или из статического
   списка до DATA-001).
4. Ничего не писать в новое хранилище: все категории читаются из уже персистентного состояния
   (см. SAVE-001). Если для «Адресов» решено хранить открытие игроком — использовать
   существующий эффект `knowledge.set-status` (например, `knowledge/place-<id>-visited`), а не новое поле.
**Files to modify:** `game/scripts/CompiledCampaignRepository.cs`, `game/scripts/RuntimeBridge.cs`,
`content/modules/urman-chapter1/definitions.json` (только если добавляются `text/*` для заголовков).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/CompiledCharacterContent.cs`, `game/scripts/NotebookProjections.cs` (если удобнее
держать проекции отдельно от моста).
**Existing code to reuse:** `JournalEntries()`, `ActiveObjectives()`, `LearnedVocabulary()`,
`ContentRuleEngine` (условия для «прочитано/не прочитано»).
**Existing code to remove/deprecate:** —
**Dependencies:** DATA-001 (для «Адресов»), SAVE-001 (правила).
**Scene/inspector setup (Godot):** —
**Runtime behavior:** данные книжки наполняются по мере игры из уже существующих механик; ничего
не нужно «добавлять вручную».
**Edge cases:** пустые категории (показывать «—», как сейчас в журнале `:174–181`);
персонаж без портрета (плейсхолдер-инициал); знание `hypothesis` (показывать как «предположение»,
как в словаре `JournalUi.cs:149–158`); загрузка сейва (проекции пересчитываются из состояния).
**Acceptance criteria:** каждая из 5 категорий отдаёт непустой список на финальном состоянии
Акта I (кроме «Адресов» до DATA-001); данные совпадают с содержимым соответствующих реестров
(тест сверяет количество); новых файлов состояния не создано.
**Manual test:** пройти до ФАП и открыть книжку: в «Люди» есть Алсу/Наиля/Мансур, в «Заметки» —
найденные предметы, в «Задачи» — текущая цель.

---

## NOTE-002 — UI книжки: разделы, навигация, доступность

**Priority:** P1
**Current state:** `JournalUi` — центральная панель `Screen/Book` с вкладками и читалкой
(`journal_ui.tscn`, `JournalUi.cs:205–256`), открывается по `journal` (`J`/`Y`), блокирует
управление (`SetPlayerModal(true)`), стиль «тёплая бумага», фоли `paper_open`.
**Problem:** ТЗ §5.2 требует понятной навигации между страницами/разделами и типов информации.
**Required implementation:**
1. Расширить `JournalUi` до 5 вкладок: «Записи» (текущая), «Люди», «Адреса», «Задачи»,
   «Наблюдения» + сохранить существующую «Сопоставить» там, где она есть сейчас
   (шестая вкладка или внутри «Записей» — на выбор implementer, но обе должны быть достижимы
   с клавиатуры/геймпада).
2. Навигация: `TabBar` слева/сверху, список записей в выбранной категории, читалка справа
   (переиспользовать существующие `_entries`/`_title`/`_body`/`_source`), горячие клавиши
   `Q`/`E` (или `LB`/`RB`) для переключения вкладок, `Esc` — закрыть.
3. Для «Люди»: слева список имён, справа — портрет (плейсхолдер-инициал, если нет ассета),
   связанные знания и «где встречался» (из `sourceIds`).
4. Доступность: применить существующий `AccessibilityPresentation.ApplyToControl` и
   масштаб текста (`TextScale`); при `ReducedMotion` отключить анимации появления панели.
5. UX: панель остаётся центрированной и масштабируется по вьюпорту (`RefitToViewport`, `:350–375`).
**Files to modify:** `game/scripts/JournalUi.cs`, `game/scenes/ui/journal_ui.tscn`
(при необходимости — новые узлы вкладок).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/JournalUi.Categories.cs` (partial, чтобы не раздувать существующий файл).
**Existing code to reuse:** вся существующая механика журнала, `AccessibilityPresentation`,
`UiFoley` (`paper_open`), `RefitToViewport`.
**Existing code to remove/deprecate:** —
**Dependencies:** NOTE-001.
**Scene/inspector setup (Godot):** узлы вкладок и списков; если новые Label/RichTextLabel —
не забыть про `font_color`-оверрайды для high-contrast, как в существующем коде (`:61–74`).
**Runtime behavior:** `J` открывает книжку в последней открытой категории; стрелки/клики
переключают записи; `Esc`/`J` закрывают; движение заблокировано, пока книжка открыта.
**Edge cases:** открытие во время диалога (сейчас блокируется проверкой `ModalOpen`, `:84–88`);
пустая категория; очень длинный документ (скролл `RichTextLabel`); смена языка текстов
(тексты берутся через `ResolveText`, `:300`).
**Acceptance criteria:** все 5 категорий открываются и листаются с клавиатуры и геймпада;
экран не «ломается» при `TextScale` 1.6; смоук `game/tests/JournalFlowSmokeTest.cs` проходит;
новый смоук `game/tests/NotebookTabsSmokeTest.cs` (NEW FILE) проверяет переключение всех вкладок.
**Manual test:** открыть книжку, пройти по всем разделам, закрыть; проверить в настройках
масштаб текста/contrast.

---

## NOTE-003 — Diegetic-презентация (предмет в мире), опционально

**Priority:** P3
**Current state:** книжка — чистое UI; предмета в мире нет.
**Problem:** ТЗ §5.1/§5.3 хочет ощущение «личной физической книжки»; UI-часть — NOTE-002, предмет — эта задача.
**Required implementation:**
1. На столе в доме бабая (рядом с ПК) разместить меш-плейсхолдер блокнота
   (`AddVisualBox`-паттерн, как в `Act1ConnectedWorld.Exploration.cs:125–147`) + `InteractionTarget`
   `interaction/open-notebook` («Открыть записную книжку»), который вызывает
   `JournalUi.Open(bridge)` (тот же UI, что и по `J`).
2. Фоли открытия — существующие `paper_open`; звук перелистывания при смене вкладки —
   `paper_open` со сдвигом тона (`PitchScale = 1.15`) — **PLACEHOLDER ACCEPTABLE** до появления
   отдельного сэмпла.
3. Визуальный «поворот страницы» — короткий твин (0.2 с) при смене категории, отключается при
   `ReducedMotion`. Не перегружать: никаких 3D-разворотов камеры.
**Files to modify:** `game/scripts/StyleBenchmarkZone.cs` (меш+цель в доме),
`game/scripts/JournalUi.cs` (твин/фоли), `content/modules/urman-chapter1/definitions.json` (интеракция).
**New files/components (NEW FILE, предлагаемые пути):** текстура обложки
`game/assets/textures/act1/notebook_cover_v1.png` (плейсхолдер).
**Existing code to reuse:** `JournalUi.Open`, `UiFoley`, паттерн «предмет + цель» из находок.
**Existing code to remove/deprecate:** —
**Dependencies:** NOTE-002.
**Scene/inspector setup (Godot):** меш + `InteractionTarget` на столе; масштаб — 1:1 к столу
(стол 3.2×0.14×1.35, `StyleBenchmarkZone.cs:395`).
**Runtime behavior:** игрок подходит к столу, `E` — открывается книжка (движение блокируется),
`Esc`/`J` — закрыть.
**Edge cases:** книжка открыта из UI и с предмета одновременно (второй вызов игнорируется —
существующая проверка `ModalOpen`); игрок стоит далеко (луч 2.7 м — вне зоны, промпт не появляется).
**Acceptance criteria:** предмет виден на столе, `E` открывает ту же книжку, состояние сохраняется.
**Manual test:** войти в дом, посмотреть на стол, открыть книжку с предмета и по `J`.

---

# PHASE 4 — Babay computer (полноценная подсистема)

Общее для фазы. Базовая система **уже существует** и её нужно расширять, а не писать заново:

- контент: `content/modules/urman-oldpc/{module.json, capabilities.json, definitions.json, schemas/, documents/*.md}`
  (26 документов; front-matter с блоком `oldPc: {type, pcSection, canonStatus, reliability, searchTerms, suggestedTerms}`);
- провайдер: `src-dotnet/Urman.Core/Capabilities/OldPc/OldPcCapabilityProvider.cs`
  (протокол `urman.oldpc:capability/archive-hub`, команды `search/open/save/section`,
  состояние `{activeDocumentId, activeSection, nextActionSequence, query, savedDocumentIds}`);
- мост: `game/scripts/RuntimeBridge.cs` (`OldPcDocuments`, `IsOldPcDocumentAccessible` `:647–656`,
  `OpenOldPcUi` `:661–667`, `HandleOldPcInputAsync` `:669–736`, инстанс в
  `CreateCapabilities` `:1030–1042`);
- UI: `game/scripts/OldPcUi.cs` + `game/scenes/ui/old_pc_ui.tscn` (один экран: заголовок, поиск,
  список, читалка, «Сохранить в журнал»);
- сохранение: состояние capability попадает в `SaveGameV3.Capabilities` автоматически
  (`_capabilities.CaptureAll()` в `SaveSlotAsync`, `:100–134`);
- дизайн-источник (код не переиспользуется, только решения): legacy `src/os/core/registry.ts:12–31`
  (17 ярлыков), `src/os/core/OS.ts` (desktop/taskbar/clock/windows), `src/os/apps/*`.

**Важное ограничение по совместимости.** `OldPcSession.Restore`
(`OldPcCapabilityProvider.cs:68–103`) валидирует **точный** набор ключей снимка
(`SnapshotKeys`). Если добавляются новые ключи состояния:
- **предпочтительно** оставить `StateSchemaVersion = 1` и сделать `Restore` терпимым
  (недостающие ключи заполнять дефолтами, неизвестные — по-прежнему отвергать);
- альтернатива — поднять версию до 2 и проверить поведение `CapabilityHost.Create` при
  несовпадении версии снимка (при отказе — обязателен апгрейд-путь, иначе старые сейвы умрут).

## PC-001 — Виртуальная файловая система и типизированный контент

**Priority:** P1
**Current state:** документы есть, но структуры «папки/файлы/ярлыки/сайты» нет: UI показывает
плоский список, раздел (`pcSection`) виден только в строке статуса пути
(`OldPcUi.cs:151` — `C:\KYRLAY\<section>\<id>.txt`).
**Problem:** ТЗ §6.2/§8 требует рабочий стол, ярлыки, папки, окна, файлы; для этого нужна
внутриигровая ФС-модель, а не только поиск.
**Required implementation:**
1. Расширить front-matter документов `urman.oldpc` новым блоком (по образцу `oldPc`):
   `fs: { path: "C:/KYRLAY/household_misc", name: "Квитанции 1996-2003.txt", icon: "doc",
   readonly: true, hidden: false }`. Схема `content/schemas/document.schema.json` —
   `additionalProperties: false`, поэтому **добавить свойство `fs` в схему** (и в JS-схему,
   и в .NET-тип резолвера, если он типизирует блок).
   Обязательное правило: **существующие id документов не меняются**, иначе сломаются
   `knowledgeRefs`, `openEffects`, `journal`-записи и сохранения.
2. NEW FILE `content/modules/urman-oldpc/fs.json` — декларация дерева папок и ярлыков
   (папки: `C:/KYRLAY/{documents_marat, household_registry, internal_accounting,
   violations_compensation, saved_messages, tatarwiki, kara_urman, household_misc, damaged_hidden}`,
   ярлыки рабочего стола: «Архивный поиск», «Документы Марата», «Татарвики»,
   «Сохранённые сообщения», «Учёт Кырлая», «Повреждённые файлы», «Заметки Айдара»,
   «Проигрыватель», «Корзина» — список из legacy-реестра, `src/os/core/registry.ts:12–31`).
   Формат — строгий JSON, добавить схему `content/modules/urman-oldpc/schemas/fs.schema.json`
   (NEW FILE) и указать файл в `module.json` → `sourceFiles`.
3. Расширить `OldPcCapabilityProvider`: команды `fs.list` (по пути) и `fs.open` (по пути/ярлыку),
   состояние — новые ключи `fsCurrentPath`, `desktopShortcutId` (см. ограничение выше),
   `section` оставить как есть (используется ядром сравнений).
4. Godot-доступ: `RuntimeBridge` получает `OldPcFsTree()` (проекция `fs.json`) и
   `HandleOldPcInputAsync` — поддержку новых типов ввода (`fs.list`, `fs.open`) с той же
   проверкой доступа, что и `open` (`IsOldPcDocumentAccessible`).
5. Миграция контента: для всех 26 документов проставить `fs.path` по их текущему `pcSection`
   и `fs.name` (человекочитаемое имя файла на русском, расширение `.txt`/`.doc`).
**Files to modify:** `content/modules/urman-oldpc/module.json`, `definitions.json`,
`documents/*.md` (26 файлов — добавить блок `fs`), `content/schemas/document.schema.json`,
`src-dotnet/Urman.Core/Capabilities/OldPc/OldPcCapabilityProvider.cs`,
`game/scripts/RuntimeBridge.cs`, `game/scripts/CompiledCampaignRepository.cs` (проекция `fs`).
**New files/components (NEW FILE, предлагаемые пути):**
`content/modules/urman-oldpc/fs.json`, `content/modules/urman-oldpc/schemas/fs.schema.json`,
`game/scripts/OldPcFileSystem.cs` (проекция дерева для UI).
**Existing code to reuse:** capability-паттерн oldpc, `IsOldPcDocumentAccessible`, `oldPc.pcSection`.
**Existing code to remove/deprecate:** —
**Dependencies:** FIX-003 (гейт), DATA-001 (если добавляется новый тип вместо блока `fs`).
**Scene/inspector setup (Godot):** —
**Runtime behavior:** игрок открывает ПК и видит рабочий стол с ярлыками → двойной клик по
папке открывает список файлов → клик по файлу открывает документ в читалке (существующая логика);
поиск остаётся доступен как отдельное приложение.
**Edge cases:** документ, недоступный по условиям (`accessConditions`), виден как файл с замком
(поведение как «🔒 Закрытые записи», `OldPcUi.cs:216–220`); пустая папка; путь с несуществующим
родителем (валидация при сборке контента — тест `tests/content/urman-oldpc/oldpc-data.test.mjs`
не удалять, расширить); старые сейвы с состоянием без `fsCurrentPath`.
**Acceptance criteria:** все 26 документов доступны через дерево; ни один id не изменился
(тест сверяет список id до/после); `content:check` и `test:content` зелёные;
`SaveGameV3` со старым снимком oldpc загружается без ошибок.
**Manual test:** включить ПК, пройти по всем ярлыкам, открыть по документу из каждой папки,
ввести поиск, сохранить документ в журнал.

---

## PC-002 — Desktop shell (обои, ярлыки, панель задач, часы, «Пуск»)

**Priority:** P1
**Current state:** desktop-оболочки нет; `OldPcUi` показывает сразу экран поиска.
Legacy-реализация (не в поставке): `src/os/core/OS.ts:97–130` (desktop-grid, taskbar, «Пуск», часы),
`applyMonitorStyles` — рамка монитора.
**Problem:** ТЗ §6.1/§6.2 требует оболочку в духе Windows XP/2000: рабочий стол, обои, панель
задач, часы, меню «Пуск», ярлыки, папки, окна, корзина.
**Required implementation:**
1. NEW сцена `game/scenes/ui/old_pc_desktop.tscn` (NEW FILE) + скрипт
   `game/scripts/OldPcDesktop.cs` (NEW FILE, `CanvasLayer`):
   - `Wallpaper` (`TextureRect`, плейсхолдер — цветной градиент; арт-референс
     `ui_old_pc_desktop_base` из `content/modules/urman-oldpc/definitions.json` —
     **PLACEHOLDER ACCEPTABLE**),
   - `DesktopGrid` (`GridContainer`) с ярлыками из `fs.json` (PC-001),
   - `Taskbar` (`PanelContainer`) с кнопкой «Пуск», списком открытых окон и `Clock` (`Label`,
     обновление раз в секунду, время игровое — см. п.3),
   - `StartMenu` (`PopupPanel`): разделы (`pcSection`) и приложения.
2. Виртуальное разрешение оболочки — 640×480 (как в legacy DedOS), масштабируется до экрана
   через `Control.scale`; курсор — системный (мышь уже видима при `SetModalOpen(true)`).
3. Часы: показывать **внутриигровое** время, а не системное (в мире зима/вечер). Источник —
   новый ключ состояния capability `clockMinutes` (например, фиксированное `18:40` при
   включении ПК + тикание) — **PLACEHOLDER ACCEPTABLE** до появления игрового времени в ядре.
4. «Пуск» содержит: Все программы (Проводник, Блокнот, Документ, Браузер, Проигрыватель,
   Корзина), Найти (открывает существующий поиск), Завершение работы (выключение ПК —
   существующий эффект `oldpc-power` / закрытие UI).
5. Включение/выключение: существующий `interaction/oldpc-power` (`StyleBenchmarkZone.cs:401–410`)
   открывает оболочку; выключение — закрывает её (как сейчас `OldPcUi.Close`).
**Files to modify:** `game/scenes/main.tscn` (добавить инстанс новой сцены), `game/scripts/RuntimeBridge.cs`
(`OpenOldPcUi` открывает desktop, а не поиск), `game/scripts/OldPcUi.cs` (поиск становится
одним из приложений — встроить существующий экран как «окно»).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scenes/ui/old_pc_desktop.tscn`, `game/scripts/OldPcDesktop.cs`,
`game/scenes/ui/old_pc_window.tscn` (рамка окна, см. PC-003).
**Existing code to reuse:** `OldPcUi` целиком как приложение «Архивный поиск»;
`UiFoley` (`ui_click`, `keyboard_key`); существующий ввод/модальность.
**Existing code to remove/deprecate:** прямое открытие поискового экрана из
`InteractionTarget.Interact` (`InteractionTarget.cs:102–105` вызывает `bridge.OpenOldPcUi()` —
оставить, но под ним теперь оболочка).
**Dependencies:** PC-001, PC-003 (окна).
**Scene/inspector setup (Godot):** `OldPcDesktop` (`CanvasLayer`, layer выше HUD, ниже меню),
`Wallpaper`, `DesktopGrid`, `Taskbar` (+`Clock`), `StartMenu`, `WindowsContainer` (`Control`).
**Runtime behavior:** `E` на ПК → загрузочный экран (0.6 с, плейсхолдер-текст «Старый компьютер»,
как в legacy `showBootScreen`) → рабочий стол; двойной клик по ярлыку — окно; «Пуск» — меню;
часы идут; `Esc` закрывает верхнее окно, повторный — выключает ПК.
**Edge cases:** открытие ПК во время диалога (модальность уже блокирует); повторное открытие
того же приложения (фокус на существующем окне, а не второе окно — см. PC-003);
сохранение/загрузка при открытом ПК (модальное состояние сбрасывается, оболочка закрывается).
**Acceptance criteria:** рабочий стол виден, ≥ 9 ярлыков из `fs.json`, часы идут, «Пуск»
открывает меню, поиск открывается из «Пуск» и как ярлык, выключение закрывает оболочку;
новый смоук `game/tests/OldPcDesktopSmokeTest.cs` (NEW FILE) проверяет открытие/закрытие.
**Manual test:** включить ПК, открыть «Татарвики» с рабочего стола, вернуться на рабочий стол,
открыть «Пуск», выключить ПК.

---

## PC-003 — Window manager (окна, фокус, z-order, перетаскивание)

**Priority:** P1
**Current state:** окна как такового нет — один полноэкранный Control; legacy реализация
(не в поставке) — `src/os/core/OS.ts:132–221` (открытие/закрытие/фокус/перетаскивание/
minimize/z-index).
**Problem:** ТЗ §6.2 требует окон; несколько приложений должны работать одновременно.
**Required implementation:**
1. NEW: `game/scenes/ui/old_pc_window.tscn` (NEW FILE): рамка окна с заголовком
   (`TitleBar` с текстом, кнопки `_`/`X`), `Content` (`MarginContainer`), `Resizer` (опционально).
2. NEW: `game/scripts/OldPcWindowManager.cs` (NEW FILE):
   `OpenWindow(appId, title, contentFactory, size)`, `CloseWindow(handle)`, `FocusWindow(handle)`,
   `MinimizeWindow(handle)`, `BringToFront`, `CaptureState()/RestoreState()`.
   Правила: одно окно на `appId` (повторный вызов — фокус), z-index монотонно растёт,
   drag — за заголовок, ограничение координат виртуальным экраном 640×480.
3. Состояние окон сохранять в capability: `openWindows: [{appId, x, y, w, h, minimized}]`,
   `focusedAppId` (новые ключи состояния, см. ограничение фазы). При загрузке сейва — восстановить.
4. Ввод: при открытом окне `Esc` закрывает/минимизирует верхнее окно; `Tab` не используется
   для переключения (конфликт с UI-навигацией Godot) — вместо этого `Ctrl+Tab` или клики по панели задач.
5. Модальность: пока открыт хотя бы один ПК-экран, `player.SetModalOpen(true)` (как сейчас);
   закрытие всех окон = закрытие ПК-режима (или оставить оболочку и закрывать явно — выбрать
   одно: рекомендуется «закрытие последнего окна возвращает на рабочий стол, выключение ПК — отдельное действие»).
**Files to modify:** `game/scripts/RuntimeBridge.cs` (прокид состояния окон),
`src-dotnet/Urman.Core/Capabilities/OldPc/OldPcCapabilityProvider.cs` (новые ключи),
`game/scripts/OldPcUi.cs` (встроить существующий экран как контент окна).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scenes/ui/old_pc_window.tscn`, `game/scripts/OldPcWindowManager.cs`,
`game/scripts/OldPcWindow.cs`.
**Existing code to reuse:** legacy-логика DedOS как референс поведения (не копировать код —
он на TypeScript и вне поставки), существующий `SetModalOpen`.
**Existing code to remove/deprecate:** —
**Dependencies:** PC-002.
**Scene/inspector setup (Godot):** `WindowsContainer` внутри `OldPcDesktop`;
`OldPcWindow` — тема оформления в духе Win2000 (плоские серые панели, `StyleBoxFlat`);
PLACEHOLDER ACCEPTABLE.
**Runtime behavior:** двойной клик по ярлыку → окно по центру; перетаскивание мышью;
клик по другому окну — фокус; `X` — закрытие; `_` — минимизация (кнопка в панели задач
возвращает окно); состояние окон переживает сохранение/загрузку.
**Edge cases:** открытие второго экземпляра приложения; перетаскивание за пределы экрана
(клампится); закрытие ПК с открытыми окнами (состояние сохраняется, оболочка закрывается);
разные разрешения окна игры (`window/stretch/mode="canvas_items"`, `project.godot`) — масштаб
считается от вьюпорта.
**Acceptance criteria:** ≥ 3 окна одновременно, фокус/z-order корректны, drag работает,
состояние окон сохраняется и восстанавливается после save/load; смоук PC-003 зелёный.
**Manual test:** открыть «Татарвики», «Блокнот» и «Корзину», потаскать окна, сохранить игру
(`quick_save`), загрузить — окна на местах.

---

## PC-004 — Приложения: Блокнот, Документ, Проводник, Корзина

**Priority:** P1
**Current state:** ничего из этого нет в Godot. Legacy-прототипы: `src/os/apps/notepad.ts` (397 стр.),
`viewer.ts` (пустой), `word.ts` (заглушка), `oldPcHub.ts` (архив) — вне поставки.
**Problem:** ТЗ §6.3 требует аналоги Word и Notepad; §6.2 — папки/файлы/корзину.
**Required implementation:**
1. **Проводник (File explorer)**: окно с деревом/списком из `fs.json` (PC-001); двойной клик по
   папке — вход, по файлу — открытие в окне «Документ»; хлебные крошки пути в заголовке.
2. **Документ (Word-подобное)**: окно, показывающее markdown-тело документа в стилизованной
   «странице» (рамка листа, поля, шрифт Times-подобный). Кнопки: «Сохранить в записную книжку»
   (существующий `JournalUi`/`journal.record`), «Закрыть». Редактирование **не требуется** —
   достаточно режима чтения; если делается редактирование, писать только в состояние capability
   (`drafts`), не в контент. **PLACEHOLDER ACCEPTABLE**: без постраничной вёрстки.
3. **Блокнот (Notepad)**: окно с `TextEdit`; текст хранится в состоянии capability
   (`notes: [{id, title, body, modifiedAt}]`), сохраняется в сейв; кнопка «В записную книжку»
   создаёт запись в категории «Наблюдения» (см. NOTE-001) — через существующий механизм
   `journal.record` с `entryId = noteId` (текст записи — из capability, поэтому потребуется
   небольшое расширение `ResolvedJournalEntry`-проекции: см. NOTE-001 п.3, добавить источник
   «из блокнота ПК»).
   Ввод текста — латиница/кириллица средствами Godot; **ввод татарских букв** не блокирует задачу.
4. **Корзина (Recycle bin)**: окно, где лежат «удалённые» файлы; предзаполнено одной записью
   с текстом-отсылкой (legacy: «Корзина пуста. Кроме одного ярлыка с подписью: „Не удалять старые
   реестры“»). Удаление/восстановление файлов можно не реализовывать — только просмотр.
5. **Проигрыватель (аудио)**: окно с кнопкой воспроизведения аудио-улики из архива
   (`audio-marat-voice`, `audio-rinat-interruption` — существующие ассеты), повторно использует
   `AudioCueUi`-плейер или отдельный `AudioStreamPlayer`; субтитры обязательны (доступность).
   Если объём работ мешает — вынести в P2 и не блокировать фазу.
**Files to modify:** `game/scripts/RuntimeBridge.cs` (прокид команд блокнота),
`src-dotnet/Urman.Core/Capabilities/OldPc/OldPcCapabilityProvider.cs` (команды `note.*`, `bin.list`),
`game/scripts/OldPcUi.cs` (использовать как «Документ»-приложение).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/apps/OldPcExplorerApp.cs`, `game/scripts/apps/OldPcDocumentApp.cs`,
`game/scripts/apps/OldPcNotepadApp.cs`, `game/scripts/apps/OldPcBinApp.cs`,
`game/scripts/apps/OldPcPlayerApp.cs` (каталог `game/scripts/apps/` — новый).
**Existing code to reuse:** `OldPcUi` (читалка), `UiFoley`, `CompiledCampaignRepository.RequireOldPcDocument`,
`JournalUi.RefreshProjection` (обновление книжки после записи).
**Existing code to remove/deprecate:** —
**Dependencies:** PC-001, PC-003, NOTE-001.
**Scene/inspector setup (Godot):** каждое приложение — `Control`-контент для окна;
`TextEdit` с темой «Блокнот» (моноширинный шрифт, PLACEHOLDER ACCEPTABLE).
**Runtime behavior:** все приложения открываются из «Пуска»/ярлыков, работают в окнах,
не теряют введённый текст при переключении окон, сохраняют содержимое блокнота в сейв.
**Edge cases:** блокнот с пустым текстом (не создаёт запись); сохранение в книжку дважды
(идемпотентно по `entryId`); документ с недоступными условиями (замок); очень длинный текст
в блокноте (скролл); выход из ПК без сохранения блокнота (автосейв при закрытии окна).
**Acceptance criteria:** все 5 приложений открываются; текст блокнота переживает save/load;
«Сохранить в записную книжку» создаёт ровно одну запись; корзина содержит предзаполненную запись;
новый смоук `game/tests/OldPcAppsSmokeTest.cs` (NEW FILE) проверяет открытие каждого приложения.
**Manual test:** открыть каждое приложение, написать в блокноте строку, сохранить игру,
загрузить, открыть блокнот — строка на месте.

---

## PC-005 — Браузер, ТатВики и локальная соцсеть

**Priority:** P1
**Current state:** нет. Контент ТатВики существует как 3 документа-статьи
(`content/modules/urman-oldpc/documents/tw_shurale_urman_boundary.md`, `tw_tavysh_boundary_stories.md`,
`tw_zirat_customs.md`, поле `oldPc.type: tatarwiki_article`); арт-шаблон статьи —
логический ассет `ui-tatarwiki-article-template`; соцсети нет вообще.
**Problem:** ТЗ §7 требует браузер, локальные сайты, ТатВики и местную соцсеть с переходами.
**Required implementation:**
1. **Модель данных (без нового движка):** страницы описываются документами `urman.oldpc` с
   новым блоком `site:` (по образцу `oldPc`/`fs`):
   `site: { siteId: "tatarwiki", pageSlug: "shurale-urman-boundary", kind: "article|profile|post|feed",
   links: ["tatarwiki:tavysh-boundary-stories"], authorId: "urman.chapter1:character/mansur",
   postedAt: "2009-02-14", tags: ["шүрәле"] }`.
   Существующие 3 статьи получают `site`-блок и открываются как страницы ТатВики (их `openEffects`
   и `knowledgeRefs` сохраняются — улики не ломаются).
2. **Сайты:** минимум два — `tatarwiki` (энциклопедия: статьи, оглавление по тегам) и
   `kyrlay` (локальная соцсеть: лента, профили, посты, комментарии). Данные — контент:
   NEW FILE `content/modules/urman-oldpc/site.json` (или отдельные документы с `site`-блоками),
   схема `schemas/site.schema.json` (NEW FILE).
3. **Приложение «Браузер»**: окно с адресной строкой (плейсхолдер-адрес вида
   `http://kyrlay.local/...`), кнопками «Назад/Вперёд/Домой», областью страницы
   (`RichTextLabel` с BBCode-ссылками через `meta_clicked`), историей.
   **Не делать настоящий сетевой стек** — только внутренние переходы по `links`.
4. **История**: `history: [{url, title, visitedAt}]` в состоянии capability; отдельное окно/раздел
   «История» в «Пуска» (ТЗ §7.3). Посещённые страницы отмечаются как прочитанные.
5. **Связь с уликами:** посещение страницы ТатВики применяет её `openEffects` (как у документов),
   поэтому улики `evidence-tatarwiki-boundary`/`evidence-tatarwiki-reread` продолжают работать;
   соцсеть даёт атмосферные записи + 2–3 подсказки (в т.ч. «поздравления» и «саламы»,
   которые потом перекликаются с радио, см. RADIO-002).
6. **Локальная соцсеть — контент (можно минимум):** 3–5 профилей (Мансур, Гөлсинә, Алсу, Ринат,
   Фанис), 6–10 постов с датами 2007–2011, по 2–3 комментария; контент пишет контент-автор,
   implementer делает систему и по одному примеру каждого типа (PLACEHOLDER TEXT ACCEPTABLE).
**Files to modify:** `content/modules/urman-oldpc/module.json` (+`site.json` в `sourceFiles`),
`documents/tw_*.md` (добавить `site`-блок), `content/schemas/document.schema.json` (свойство `site`),
`src-dotnet/Urman.Core/Capabilities/OldPc/OldPcCapabilityProvider.cs` (команды `site.open`, `history.list`),
`game/scripts/RuntimeBridge.cs`.
**New files/components (NEW FILE, предлагаемые пути):**
`content/modules/urman-oldpc/site.json`, `content/modules/urman-oldpc/schemas/site.schema.json`,
`game/scripts/apps/OldPcBrowserApp.cs`, `game/scripts/OldPcSiteIndex.cs`.
**Existing code to reuse:** механизм документов (`accessConditions`, `openEffects`, `knowledgeRefs`),
`RichTextLabel.meta_clicked` (Godot), `UiFoley`.
**Existing code to remove/deprecate:** —
**Dependencies:** PC-001, PC-003, PC-004 (общая рамка окна).
**Scene/inspector setup (Godot):** окно браузера: `LineEdit` (адрес), `HBoxContainer` (кнопки),
`RichTextLabel` (`bbcode_enabled = true`, `meta_underlined = true`).
**Runtime behavior:** открыл браузер → домашняя страница `kyrlay` → переходы по ссылкам →
страница ТатВики открывается с эффектами улик → история пополняется.
**Edge cases:** ссылка на несуществующую страницу (страница-заглушка «Страница не найдена»);
страница с `accessConditions` (замок, как у документов); возврат «Назад» с эффектами
(эффекты применяются один раз — идемпотентность `openEffects` сохраняется); длинная лента (скролл).
**Acceptance criteria:** ТатВики открывается как сайт с 3 существующими статьями; переходы по
ссылкам работают; посещение статьи по-прежнему двигает улики (смоук `ChapterOneFlowSmokeTest`
с `evidence-tatarwiki-*` зелёный); история наполняется и сохраняется; соцсеть содержит ≥ 3 профиля.
**Manual test:** открыть браузер, зайти в ТатВики, открыть «Шүрәле и лесная граница», перейти
по ссылке на статью про тавыш, вернуться на домашнюю страницу соцсети, открыть профиль Мансура.

---

## PC-006 — Языковой слой ru/tt для интерфейса ПК

**Priority:** P2
**Current state:** все подписи ПК — русские (`OldPcUi.cs`); татарский присутствует только в
контенте (словарь `vocabulary.tt_*`, слова внутри документов, legacy-ярлык «Чүплек»).
**Problem:** ТЗ §6.4 требует естественной русско-татарской смеси в интерфейсе системы.
**Required implementation:**
1. NEW FILE `game/scripts/LocaleService.cs` (NEW FILE): статический резолвер подписей
   `Label(string key)` → берёт текст из контента (`urman.core`-модуль или `urman.oldpc`),
   выбор языка — из настройки (`GameSettingsSnapshot`) с дефолтом `ru`.
2. Контент: NEW FILE `content/modules/urman-oldpc/texts.ui.json` (или записи `text/*` в
   `definitions.json`) с ключами: `pc.desktop`, `pc.start`, `pc.trash` = «Чүплек»,
   `pc.folder.registry` = «Реестр домов», `pc.folder.accounting` = «Хисап» (учёт),
   `pc.search`, `pc.save`, `pc.history`, `pc.browser.home` и т.д. — каждое значение с
   `{default, translations:{ru, tt}}`.
3. Правило естественности: татарские подписи — у «локальных» сущностей (корзина, папки
   хозяйства/учёта, разделы ТатВики), русские — у системных (ОС-команды), чтобы это читалось как
   реальный домашний компьютер, а не как перевод каждого второго пункта.
4. Никакой смены языка «на лету» не требуется: язык — часть профиля пользователя
   (`UserSettingsStore`), применяется при открытии ПК.
**Files to modify:** `game/scripts/OldPcDesktop.cs`, `game/scripts/apps/*` (использовать `LocaleService`
вместо строковых литералов), `game/scripts/SettingsUi.cs` (если добавляется выбор языка).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/LocaleService.cs`, `content/modules/urman-oldpc/ui-texts.json` (+схема при необходимости).
**Existing code to reuse:** `TextResolver`/`RuntimeBridge.ResolveText` (существующий резолвер текстов
с `translations`), `VocabularyEntryContent` как образец двуязычных данных.
**Existing code to remove/deprecate:** строковые литералы подписей в `OldPcUi.cs` (постепенно).
**Dependencies:** PC-002..PC-005.
**Scene/inspector setup (Godot):** —
**Runtime behavior:** интерфейс ПК показывает выбранный язык; при `ru` — русские подписи с
татарскими названиями локальных сущностей.
**Edge cases:** ключ без перевода (fallback на `default`); отсутствующий ключ (логировать
warning и показывать ключ — не падать); татарская буква в имени файла (шрифт должен её
поддерживать — проверить шрифт UI, при отсутствии глифов использовать существующий шрифт с
расширенной кириллицей).
**Acceptance criteria:** ≥ 10 подписей ПК читаются из контента; при `ru`/`tt` отображаются
разные строки; ни одна подпись не «зашита» в новых приложениях; языковой гейт из
`docs/production/act1_language_review_sheet_2026-09-14.md` проходит для новых строк.
**Manual test:** переключить язык профиля и открыть ПК — подписи сменились, буквы отображаются.

---

## PC-007 — Сохранение контрактов старого ПК (улики, гейтинг, журнал)

**Priority:** P1
**Current state:** открытие документа применяет `openEffects` и `knowledgeRefs`
(`RuntimeBridge.HandleOldPcInputAsync`, `:669–736`), «Сохранить» пишет запись в журнал
(`HandleOldPcSave`, `:1108–1120`), доступность — по `accessConditions`, «✓/🔒» — в UI
(`OldPcUi.cs:185–221`). Сравнения улик ссылаются на документы oldpc (`journalAction.sourceIds`).
**Problem:** новая оболочка не должна сломать ни одну из этих связей: улики, гейтинг, сравнения,
журнал и сохранения.
**Required implementation:**
1. При переносе UI (PC-002..PC-005) **не менять** путь применения эффектов: любые открытия
   документов обязаны идти через `RuntimeBridge.HandleOldPcInputAsync` (`open`), а не напрямую
   в UI. То же для «Сохранить в записную книжку».
2. Сохранить правило «открытая запись помечается ✓, недоступная — 🔒» и перенести его в
   приложение «Проводник/Документ» (в файловом списке — иконка замка).
3. Добавить регрессионный смоук `game/tests/OldPcContractSmokeTest.cs` (NEW FILE):
   - открыть документ с доступом → знание подтверждено, улика в журнале;
   - сохранить документ в книжку → ровно одна запись;
   - попытаться открыть недоступный → ошибка UI, состояние не изменилось;
   - сравнение двух источников (`CompareJournalSourcesAsync`) работает после новых UI-правок;
   - `SaveGameV3` round-trip: открытые/сохранённые документы и история восстанавливаются.
**Files to modify:** только UI-файлы фазы; мост и провайдер — по минимуму.
**New files/components (NEW FILE, предлагаемые пути):** `game/tests/OldPcContractSmokeTest.cs`.
**Existing code to reuse:** существующие мостовые методы и смоук `game/tests/OldPcFlowSmokeTest.cs`.
**Existing code to remove/deprecate:** —
**Dependencies:** PC-001..PC-005.
**Scene/inspector setup (Godot):** —
**Runtime behavior:** не меняется по существу: улики, гейтинг, журнал и сравнения работают
через новую оболочку так же, как через старый экран.
**Edge cases:** старый сейв с открытыми документами (состояние capability совместимо);
документ, сохранённый дважды (идемпотентно); документ, ставший недоступным после загрузки
(статус замка пересчитывается).
**Acceptance criteria:** `OldPcFlowSmokeTest` и `ChapterOneFlowSmokeTest` зелёные;
`OldPcContractSmokeTest` покрывает 5 пунктов выше; ни один id документа не изменился.
**Manual test:** пройти весь цикл расследования через новый ПК (ФАП → справка → реестр →
сообщение → ТатВики → сравнение → рисунок) и убедиться, что маршрут не изменился.

---

# PHASE 5 — Village locations

Общее: новые локации строятся по существующему паттерну — `BuildCore*`-методы
`Act1ConnectedWorld` (`game/scripts/Act1ConnectedWorld.cs`, образцы: `BuildCoreZirat` `:965`,
`BuildCoreFapExterior`, `AddVillageMosque` `:8025–8130`) для мира и `StyleBenchmarkZone`-строители
для интерьеров (`BuildHouseOldPc` `:352`, `BuildFapClinic` `:909`). Коллизии — через
`AddVisualBox(..., collision: true)` и `StaticBody3D`-прокси; интеракции — через
`MakeInteractionBox` + контент. Материалы — `PainterlyMaterialLibrary.ForColor(hex, surface)`.
**PLACEHOLDER ACCEPTABLE** для всей геометрии: допустимы примитивы с правильными коллизиями
и интеракциями; финальный арт приходит позже.

## START-001 — Автобусная остановка как точка старта

**Priority:** P1
**Current state:** спавн `village_day@arrival` = `(0, 0.05, 9)` (`AgentBAct1Layout.cs:80`,
`Act1WorldLayout.cs:51–56`); вокруг — колодец-журавль `ArrivalSweepWell` и плетень
(`Act1ConnectedWorld.cs:5511–5515`). Остановки, автобуса и 3D-сцены приезда нет.
Контент-сцена `scene/arrival_vehicle_dusk` («Дорога к Кырлаю») даёт заголовок и эффекты приезда.
Интро — текстовая панель `Act1DemoRoot.BuildIntro` (`:887–937`).
**Problem:** ТЗ §11 требует, чтобы игра начиналась на остановке на съезде в деревню.
**Required implementation:**
1. Построить остановку на въезде: павильон 3×2.2×2 м (столбы, навес, скамья, стенд с
   расписанием), знак «Кырлай» — `BuildCore`-метод в `Act1ConnectedWorld` (NEW partial
   `game/scripts/Act1ConnectedWorld.BusStop.cs`) + коллизия стенд/павильон.
2. Автобус (плейсхолдер: `BoxMesh`-силуэт 8×2.6×2.4 с окнами-полосками, **PLACEHOLDER ACCEPTABLE**)
   на дороге `z ≈ +18`; в первое посещение он «уезжает»: твин позиции по оси `+Z` на 25 м за 6 с
   с затуханием звука двигателя (сэмпл — NEW `game/assets/audio/act1/ambience/bus_departure.wav`,
   плейсхолдер — низкий `AudioStreamGenerator` или зацикленный существующий амбиент).
3. Спавн игрока перенести **под навес остановки**: новый спавн `arrival_busstop`
   в `Act1WorldLayout` (координаты — на 1.5 м впереди павильона, yaw в сторону деревни),
   `Act1DemoRoot.DefaultPerformanceSample` и `Main.SpawnTransform` обновить; старый `arrival`
   оставить как алиас (для сейвов и тестов).
4. Интро: заменить текст панели на короткий (2 строки) и добавить в
   `Act1DemoRoot.UpdateIntroControls` упоминание прыжка/приседа после их появления
   (PLAYER-001/002), например: «WASD — идти · Space — прыжок · C — пригнуться · E — осмотреть · J — книжка».
5. Расписание на стенде — `DocumentUi`-документ (`urman.chapter1:document/busstop-schedule`,
   NEW markdown) с временами рейсов и **упоминанием соседних деревень** (готовит почву для радио).
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs` (+ новый partial),
`game/scripts/Act1WorldLayout.cs`, `game/scripts/Main.cs`, `game/scripts/Act1DemoRoot.cs`,
`content/modules/urman-chapter1/definitions.json` (документ расписания + интеракция),
`content/modules/urman-chapter1/documents/busstop-schedule.md` (NEW).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/Act1ConnectedWorld.BusStop.cs`, `content/modules/urman-chapter1/documents/busstop-schedule.md`,
звук отправления (плейсхолдер), модель автобуса (плейсхолдер-примитивы).
**Existing code to reuse:** `AddVisualBox`/`AddVisualGate`-паттерны, `MakeInteractionBox`,
`DocumentUi`, твины из `Main.PlayZoneTransition`.
**Existing code to remove/deprecate:** старый спавн как основной (остаётся алиасом).
**Dependencies:** FIX-002 (работа со спавнами), PLAYER-001/002 (тексты интро).
**Scene/inspector setup (Godot):** —
**Runtime behavior:** новая игра → игрок под навесом, автобус уезжает по дороге и исчезает в
тумане; панель интро показывает управление; игрок идёт в деревню.
**Edge cases:** продолжение из сейва (автобус уже уехал — не повторять твин; проверять
`beat/arrival` в состоянии); прыжок в момент интро (движение заблокировано модальностью);
повторный вход в зону (автобус не появляется).
**Acceptance criteria:** новый старт — на остановке; автобус уезжает ровно один раз за сеанс;
спавн-матрица (`Act1SpawnMatrixSmokeTest`) зелёная с новым спавном; стенд открывает документ.
**Manual test:** новая игра → осмотреться на остановке → увидеть отъезжающий автобус →
прочитать расписание → идти в деревню.

---

## MOSQUE-001 — Интерьер мечети

**Priority:** P1
**Current state:** зал/купол/двор/закрытая дверь существуют (`AddVillageMosque`, `:8025–8130`;
дверь — «stays shut in Act I», `:8116`); внутри пусто, интеракций нет.
**Problem:** ТЗ §14/§25 требует подробно проработанную мечеть с интерьером, кабинетом имама,
книгами, объявлениями — с уважением к религиозному пространству.
**Required implementation:**
1. Новая зона `mosque_hall` (интерьер) по образцу `house_old_pc`/`fap_clinic`:
   - `Act1WorldLayout`: новое `ZonePlacement` с `Interior: true` (origin — рядом с комплексом,
     например `(-46, 0, -34)` + локальные координаты зала 11×5.2×8.5 из `AddVillageMosque`);
   - сцена `game/scenes/zones/chapter1_mosque_hall.tscn` (NEW FILE, `StyleBenchmarkZone`
     с новым `BenchmarkKind.MosqueHall`) — либо отдельный `Node3D`-скрипт
     `game/scripts/MosqueInteriorBuilder.cs` (NEW FILE), если проще не трогать enum.
2. Наполнение (плейсхолдеры допустимы): молельный зал (ковры, михраб-ниша, окна), полки с
   книгами (Коран, тафсиры, книги по истории деревни), подставки, вешалка, обувные полки,
   объявления на стене, электрочайник/посуда в подсобке, кабинет имама (стол, книги, тетради,
   старый ноутбук-плейсхолдер, кружка).
3. Интеракции (контент): `inspect-mosque-quran-shelf`, `inspect-mosque-notices`,
   `inspect-mosque-imam-desk`, `inspect-mosque-village-history-book` (даёт знание о деревне),
   `talk-imam` (см. MOSQUE-002). Каждая — `DocumentUi`/диалог/знание, без новых систем.
   Находки — через существующий `DiscoveryTarget` (`Act1ConnectedWorld.Exploration.cs:112–123`).
4. Дверь: связать с INTERACT-001 — изнутри и снаружи; снаружи открывается только после
   условий (см. MOSQUE-002).
5. Уважительность: никаких хоррор-эффектов, скримеров и «проклятых» предметов в мечети;
   максимум — тёплый свет, тишина, ощущение защищённости (см. MOSQUE-003).
**Files to modify:** `game/scripts/Act1WorldLayout.cs` (новая зона), `game/scripts/Main.cs`
(карта зон), `game/scripts/Act1ConnectedWorld.cs` (переходы/ближайшие интеракции),
`content/modules/urman-chapter1/definitions.json` (сцена `mosque_hall`, интеракции, документы),
`content/modules/urman-chapter1/documents/mosque-*.md` (NEW).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scenes/zones/chapter1_mosque_hall.tscn`, `game/scripts/MosqueInteriorBuilder.cs`,
3–4 документа-контента.
**Existing code to reuse:** `BuildFapClinic` как образец интерьера (штукатурка, свет, мебель),
`DiscoveryTarget`, `MakeInteractionBox`, `DocumentUi`, `DialogueUi`.
**Existing code to remove/deprecate:** закрытая дверь-меш без интерактива (становится дверью с состоянием).
**Dependencies:** INTERACT-001 (двери), MOSQUE-003 (safe zone), PHASE 1 (присед/капсула для проёмов).
**Scene/inspector setup (Godot):** новая сцена зоны; `Interior: true` в `Act1WorldLayout`
включает интерьерную атмосферу (`SetActiveLogicalZone`, `:465–480` — свет/окружение).
**Runtime behavior:** игрок входит (или его впускает имам) → тёплый свет, тишина, книги,
объявления; осмотр книжной полки даёт знание о деревне; в кабинете — разговор.
**Edge cases:** вход с грязной обувью (игнорируется — не делать механик), игрок в приседе
(проём ≥ 2 м), ночная зона рядом (атмосфера переключается корректно при возврате),
несколько NPC в зале (не делать — только имам).
**Acceptance criteria:** зона существует, посещаема, интерьер содержит ≥ 6 интеракций;
загрузка в мечети корректна (`Act1SaveLifecycleSmokeTest`-паттерн); ни один элемент мечети не
участвует в «жутких» событиях (проверяется ревью, не скриптом).
**Manual test:** войти в мечеть, обойти зал, осмотреть полку и объявления, зайти в кабинет.

---

## MOSQUE-002 — Молодой имам внутри мечети

**Priority:** P1
**Current state:** NPC Тимур хәзрәт создан кодом на улице — `TimurHazratNpc` в
`Act1ConnectedWorld.cs:332–334` («Поговорить с Тимуром хәзрәтом»), позиция `(-3.8, ·, -19)`
(`NpcStaging.cs:46–49`), диалог `timur_restraint` (choices: grandparents/register/shurale/silence),
`worldLocations: [village_day, zirat_road, kara_urman_night]`, повороты к игроку
(`NpcStaging.cs:174–228`).
**Problem:** ТЗ §14.1 хочет молодого современного имама в мечети, а не на улице.
**Required implementation:**
1. Переместить NPC в зал мечети (позиция у кабинета), оставив диалог и портрет
   (`portrait-timur-hazrat`) — контент диалога не переписывать.
2. Убрать/переписать `worldLocations` в контентной интеракции `route-to-mosque`: сейчас она
   позволяет говорить с имамом на улице как «подход к мечети»; после переноса — интеракция
   должна вести **в** мечеть (открыть дверь), а разговор — внутри (новый
   `interaction/talk-imam-hall` с `targetDialogueId: dialogue/timur_restraint`).
3. Дверь мечети: открывается, если игрок поговорил с кем-то о мечети, либо всегда днём
   (улицу/время можно не моделировать — простейшее правило: открыта с начала акта;
   если хочется сюжетности — по `knowledge/clue_folklore_as_survival_rule = hypothesis`,
   как сейчас у `route-to-mosque`).
4. Реакция на игрока: существующее автоповорачивание (`NpcStaging`) применится само.
5. Реплики о «молодом, современном»: контент-автор добавляет минимум 2 ветки про быт/телефон/
   учёбу; implementer только подключает (правки `definitions.json`, диалог `timur_restraint`
   расширять осторожно — он уже участвует в квестах, проверить `ChapterOneFlowSmokeTest:334`).
**Files to modify:** `game/scripts/Act1ConnectedWorld.NpcStaging.cs` (позиция/хост),
`game/scripts/Act1ConnectedWorld.cs` (цель у двери), `content/modules/urman-chapter1/definitions.json`.
**New files/components:** —
**Existing code to reuse:** `GeneratedCharacterKitDressing.Attach` (портрет/NPC-кит),
`TurnNpcTowardsPlayer`, готовый диалог.
**Existing code to remove/deprecate:** уличная постановка Тимура как основная (оставить как
редкое появление на улице — опционально).
**Dependencies:** MOSQUE-001.
**Scene/inspector setup (Godot):** позиция NPC внутри зоны мечети; при необходимости — якорь
в сцене интерьера.
**Runtime behavior:** игрок подходит к мечети → дверь открывается (см. MOSQUE-001/INTERACT-001) →
внутри стоит имам → разговор работает как раньше, плюс новые ветки.
**Edge cases:** игрок пришёл ночью (открыто/закрыто — решить одно правило и записать в контенте);
повторный разговор (диалог идемпотентен по существующим правилам);
Тимур «на улице» в активной сцене улицы (если оставляем — не дублировать двух Тимуров).
**Acceptance criteria:** имам физически в мечети; `ChapterOneFlowSmokeTest` (интеракция
`route-to-mosque`) зелёный после правки; диалог открывается и завершается без ошибок.
**Manual test:** прийти к мечети, войти, поговорить с имамом, выбрать 2 ветки.

---

## MOSQUE-003 — Safe zone как игровая механика

**Priority:** P1
**Current state:** механики безопасной зоны нет (grep `safe` — только флаги запуска).
Сверхъестественных событий в коде тоже нет: единственные «сверхъестественные» сущности —
аудио-биты (`audio-marat-voice`, `audio-rinat-interruption`) и клиффхэнгер
(`beat/cliffhanger-hard-cut`), которые срабатывают через эффекты контента.
**Problem:** ТЗ §14.2 требует safe zone мечети: игрок внутри должен ощущать безопасность,
сверхъестественное внутри не работает, надписи «SAFE ZONE» быть не должно.
**Required implementation:**
1. NEW FILE `game/scripts/SafeZoneVolume.cs` (`partial class SafeZoneVolume : Area3D`):
   `[Export] public string ZoneTag = "safe"`, `[Export] public string AmbienceOverride = "mosque"`,
   сигналы `body_entered/body_exited` — **внимание**: это единственное разрешённое исключение из
   правила KNOCK-001, потому что зона не издаёт звуков двери, а переключает амбиент; в
   `docs/production/interaction_rules.md` зафиксировать исключение явно.
2. Реестр присутствия: NEW FILE `game/scripts/SupernaturalDirector.cs` (NEW FILE) —
   минимальный синглтон-узел (в `main.tscn`), который держит флаг `SuppressedBySafeZone` и
   метод `bool AllowsPresentation(string eventKind)`; пока игрок внутри safe zone — возвращает
   `false` для событий с `eventKind == "supernatural"`.
3. Единственное место применения: `RuntimeBridge.PresentRuntimeEvents` (`:970–983`) пропускает
   `runtime.audio.requested` события, чей `eventKind == "supernatural"` (поле добавить в контент
   у эффекта `audio.request`; обратная совместимость — отсутствие поля = обычный звук).
   `AudioCueUi` при этом продолжает работать как обычно для остальных реплик.
4. Атмосфера: внутри мечети — тёплый свет (существующий `AddLantern`-паттерн), тишина,
   отдельный амбиент `mosque_room_tone` (NEW `game/assets/audio/act1/ambience/mosque_room_tone.wav`,
   **PLACEHOLDER ACCEPTABLE** — можно временно переиспользовать `house_room_tone.wav`),
   без «жутких» подмешиваний. `AmbientAudioDirector.SetZone` (`game/scripts/AmbientAudioDirector.cs:116–150`)
   уже выбирает бэд по зоне — добавить зону мечети в `game/assets/audio/ambient_manifest.json`.
5. Никаких религиозных механик: safe zone — просто правило пространства (как просит ТЗ).
**Files to modify:** `game/scenes/main.tscn` (узел директора), `game/scripts/RuntimeBridge.cs`
(фильтр событий), `game/scripts/AmbientAudioDirector.cs` (бэд), `game/assets/audio/ambient_manifest.json`,
`content/modules/urman-chapter1/definitions.json` (`eventKind` у сверхъестественных звуков),
`docs/production/interaction_rules.md` (исключение для Area3D).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/SafeZoneVolume.cs`, `game/scripts/SupernaturalDirector.cs`,
`game/assets/audio/act1/ambience/mosque_room_tone.wav` (плейсхолдер),
`game/tests/SafeZoneSmokeTest.cs`.
**Existing code to reuse:** `AmbientAudioDirector`, `AudioCueUi`, тёплый свет
(`StyleBenchmarkZone`-освещение), `Accessibility`-настройки.
**Existing code to remove/deprecate:** —
**Dependencies:** MOSQUE-001.
**Runtime behavior:** вход в мечеть → амбиент меняется на тёплый и тихий; если в этот момент
должен был сработать сверхъестественный звук (например, «голос» у кромки), он подавляется;
выход → обычная атмосфера возвращается.
**Edge cases:** игрок стоит на границе зоны (гистерезис: вход/выход с разницей 0.5 м, чтобы не
дребезжало); загрузка сейва внутри мечети (флаг восстанавливается по позиции, не по сейву);
событие, «зависшее» на входе (пропущенный звук не воспроизводится позже — так задумано).
**Acceptance criteria:** внутри мечети `SupernaturalDirector.AllowsPresentation("supernatural") == false`;
смоук `SafeZoneSmokeTest` проверяет вход/выход и подавление; амбиент переключается; надписи
«SAFE ZONE» нигде нет.
**Manual test:** зайти в мечеть и постоять (тихо, тепло); выйти — вернулся обычный амбиент;
стоя на пороге — не «дребезжит».

---

## FAP-001 — ФАП: добивка наполнения (без RPG-медицины)

**Priority:** P2
**Current state:** ФАП уже играбелен и хорошо оснащён: интерьер
(`StyleBenchmarkZone.cs:909–1610`: кушетка, ширма, шкаф с лекарствами, лоток, приёмный стол,
регистратура, шкаф, плакаты, календарь, ростомерные метки), фельдшер Наиля
(`:1055–1085`), документы Марата (`fap-to-document-desk`, `fap-document-desk-to-official-record`),
внешний кит `urman_fap_clinic_kit.glb`.
**Problem:** ТЗ §15 требует «медицину и информацию о жителях», но без сложных механик; сейчас
информация есть (документы), «медицинской» функции нет вовсе.
**Required implementation:**
1. Добавить 3–4 интеракции-осмотра (контент + `DiscoveryTarget`):
   `inspect-fap-medicine-cabinet` (шкаф: список лекарств, знание о деревне),
   `inspect-fap-patient-journal` (журнал приёма: имена жителей → питает книжку «Люди»),
   `inspect-fap-wall-chart` (плакат).
2. Простая «медицинская» функция (одна, без системы): интеракция
   `interaction/fap-get-bandage` — Наиля перевязывает ссадину Айдара (одноразовый
   сюжетный микро-бит): эффект `beat.set-state fap-bandage completed`, реплика, без статов и
   без изменения движения. **Если даже это считается лишним — реализовать только осмотры.**
3. Никаких health/HP/hunger: запрещено добавлять новые шкалы.
**Files to modify:** `content/modules/urman-chapter1/definitions.json`,
`game/scripts/StyleBenchmarkZone.cs` (цели осмотров в ФАП), `game/scripts/Act1ConnectedWorld.FapExploration.cs`
(если находки удобнее добавить там).
**New files/components:** —
**Existing code to reuse:** `DiscoveryTarget`, `FapExploration`-паттерн, `DialogueUi`.
**Existing code to remove/deprecate:** —
**Dependencies:** NOTE-001 (для «Люди»), MOSQUE-* не нужен.
**Scene/inspector setup (Godot):** позиции целей на существующей мебели ФАП
(координаты брать из `BuildFapClinic`).
**Runtime behavior:** осмотр шкафа/журнала даёт записи в книжку; перевязка — короткая сценка.
**Edge cases:** повторный осмотр (идемпотентность), открытие во время диалога (модальность),
журнал приёма с именами, которых ещё нет в книжке (запись всё равно появляется).
**Acceptance criteria:** ≥ 3 новых интеракции; ни одной новой шкалы/стата; книжка «Люди»
пополняется после осмотра журнала приёма; смоуки ФАП-маршрута зелёные.
**Manual test:** зайти в ФАП, осмотреть шкаф и журнал приёма, поговорить с Наилей.

---

## SHOP-001 — Магазин (сельмаг): локация и прилавок

**Priority:** P1
**Current state:** магазина в мире нет; в контенте есть пустая сцена
`scene/selsmag_counter_interior` («Прилавок сельмага», `definitions.json:4177`, `interactions: []`).
**Problem:** ТЗ §16/§25 требует магазин как полноценную локацию с покупками и социальной ролью.
**Required implementation:**
1. Здание магазина в деревне: одноэтажный сельмаг с крыльцом, вывеской, витриной; место —
   на главной улице (координаты подобрать так, чтобы между домом бабая и ФАП; предложение:
   `x ≈ +14, z ≈ -12`, в стороне от дороги, с площадкой для будущей парковки Нивы).
2. Интерьер (новая зона `selsmag_interior` по образцу ФАП): прилавок, витрины, полки
   (хлеб, консервы, крупы, сладости, бытовая химия), холодильник-ларь, весы, касса-плейсхолдер,
   доска объявлений у входа.
3. Продавщица — NPC (имя предложить: «Фәридә» — **новый персонаж, требует записи в
   `content/modules/urman-chapter1/definitions.json` + `characters.md` KB**); модель — существующий
   `GeneratedCharacterKitDressing` с плейсхолдер-головой.
4. Интеракции: `talk-seller` (диалог с новостями деревни), `inspect-shop-noticeboard`
   (объявления — питает слухи и позже радио-контент), товары — через SHOP-002.
5. Доска объявлений у входа — `DocumentUi`-документ (`shop-noticeboard.md`, NEW): объявления
   жителей, объявление о Сабантуе, «продам дрова», «ищу сено» — визуальная история + подготовка
   соцсети/радио.
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs` (+ partial),
`game/scripts/Act1WorldLayout.cs` (новая зона), `game/scripts/Main.cs`,
`content/modules/urman-chapter1/definitions.json` (сцена, интеракции, NPC-персонаж, документ).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/Act1ConnectedWorld.Selsmag.cs`, `game/scenes/zones/chapter1_selsmag.tscn`,
`content/modules/urman-chapter1/documents/shop-noticeboard.md`.
**Existing code to reuse:** `BuildFapClinic`-образец интерьера, `MakeInteractionBox`, `DocumentUi`,
`GeneratedCharacterKitDressing`.
**Existing code to remove/deprecate:** пустая сцена `selsmag_counter_interior` остаётся как
презентационная (не удалять: на неё могут ссылаться тексты), добавить ей `interactions`.
**Dependencies:** INTERACT-001, NOTE-001, PHASE 1 (капсула/присед — проёмы).
**Scene/inspector setup (Godot):** зона + интерьер; прилавок — `InteractionTarget` с
`Kind = ShopCounter` (INTERACT-002).
**Runtime behavior:** игрок входит в магазин, здоровается с продавщицей, читает объявления,
берёт товары (SHOP-002).
**Edge cases:** магазин закрыт (решено: открыт всегда в Акте I), NPC занят (одна реплика-повтор),
игрок без денег (денег нет вовсе — см. SHOP-002).
**Acceptance criteria:** зона существует и посещаема; ≥ 3 интеракции; NPC отвечает; объявления
читаются; ни одной денежной системы не добавлено.
**Manual test:** зайти в магазин, поговорить, прочитать объявления.

---

## SHOP-002 — Покупки «в долг» и долговая тетрадь

**Priority:** P1
**Current state:** нет ни покупок, ни денег, ни тетради.
**Problem:** ТЗ §16.1/§16.2: покупки есть, финансовой системы нет; продавщица записывает
покупку «на бабая»; запись появляется в тетради.
**Required implementation:**
1. **Ledger как capability** (не новое хранилище): NEW модуль
   `content/modules/urman-selsmag/` (по образцу `urman.oldpc`): `module.json`,
   `capabilities.json` (протокол `urman.selsmag:capability/debt-ledger`, версия 1.0.0),
   `definitions.json` (товары + тексты), `schemas/{ledger-config,ledger-state,ledger-command,
   ledger-event,ledger-outcome}.schema.json` (NEW).
   Провайдер: NEW FILE `src-dotnet/Urman.Core/Capabilities/Selsmag/DebtLedgerCapabilityProvider.cs`
   (команды: `items.list`, `debt.add`, `debt.list`; состояние: `records: [{itemId, quantity,
   notedAt, buyerLabel: "бабай"}]`).
2. Товары (контент): 6–10 позиций (`хлеб`, `молоко`, `свечи`, `провод`, `чай`, `сахар`,
   `батарейки`, `масло`) с полем `allowDebt: true` и подсказкой-репликой.
3. Интеракция: у прилавка — `InteractionTarget` `Kind = ShopCounter`
   (`interaction/shop-counter`), открывает простой UI-список (новое окно в стиле проекта:
   `game/scenes/ui/selsmag_counter.tscn`, NEW FILE + `game/scripts/SelsmagCounterUi.cs`, NEW FILE):
   список товаров, кнопка «Взять», текст «Записала на бабая».
   Никаких цен, баланса и инвентаря: товар просто «получен» (журнал/знание при желании).
4. Тетрадь: физический объект у прилавка (`inspect-debt-notebook`) и/или в доме бабая;
   открытие показывает `RichTextLabel` со сформированным списком записей (из состояния capability,
   сортировка по времени, шапка «Тетрадь. Долги Мансура»); **UI-форма — простая, PLACEHOLDER ACCEPTABLE**.
5. Сохранение: состояние ledger сохраняется автоматически (capability snapshot в `SaveGameV3`).
6. Реплики: продавщица комментирует первую/третью/пятую покупку (контент, 3 текста).
**Files to modify:** `game/scripts/RuntimeBridge.cs` (инстанс+команды ledger, по образцу oldpc),
`game/scenes/main.tscn` (UI-сцена), `content/campaigns/urman.chapter1/campaign.json` (модуль +
`capabilityRequirements`), `content/modules/urman-chapter1/definitions.json` (интеракции/тексты).
**New files/components (NEW FILE, предлагаемые пути):**
`content/modules/urman-selsmag/{module.json,capabilities.json,definitions.json,schemas/*}`,
`src-dotnet/Urman.Core/Capabilities/Selsmag/DebtLedgerCapabilityProvider.cs`,
`game/scripts/SelsmagCounterUi.cs`, `game/scenes/ui/selsmag_counter.tscn`,
`game/tests/SelsmagDebtSmokeTest.cs`.
**Existing code to reuse:** oldpc-capability как образец (инстанс, команды, снимок),
`JournalUi`/`journal.record` для «получен товар», `RuntimeBridge`-паттерн `Handle*InputAsync`.
**Existing code to remove/deprecate:** —
**Dependencies:** SHOP-001, DATA-001 (регистрация нового модуля/схем), SAVE-001.
**Scene/inspector setup (Godot):** UI-окно прилавка (список + кнопки), модальность через
`SetModalOpen(true)`.
**Runtime behavior:** игрок подходит к прилавку → `E` → список товаров → «Взять» → реплика
продавщицы → запись в тетради → тетрадь можно посмотреть у прилавка.
**Edge cases:** повторное взятие того же товара (запись добавляется с количеством, не дублируется
идентично); открытие тетради до первой покупки («Тетрадь пуста»); сохранение/загрузка
(записи восстанавливаются); отмена выбора (закрытие окна).
**Acceptance criteria:** ≥ 6 товаров; каждая покупка даёт ровно одну запись; тетрадь читается
и переживает save/load; денег/инвентаря/цен в коде нет (проверяется ревью и отсутствием полей).
**Manual test:** купить хлеб и свечи, открыть тетрадь (две записи), сохранить и загрузить —
записи на месте.

---

## SCHOOL-001 — Закрытая школа (частично доступная)

**Priority:** P1
**Current state:** здания нет; есть только страница школьной тетради Марата в архиве ПК
(`content/modules/urman-oldpc/documents/doc_school_notebook_marat_page.md`).
**Problem:** ТЗ §17 требует частично доступную школу с кабинетами, стендами, фотографиями,
документами и пасхалками.
**Required implementation:**
1. Здание школы на окраине (предложение: `x ≈ -34, z ≈ -46`), с забитым главным входом и
   доступным боковым входом; внутри 3 помещения: коридор, класс, учительская/кабинет;
   остальные двери — заперты (`INTERACT-001`, `LockedPrompt = "Заперто. Ключей нет."`).
2. Наполнение: парты (плейсхолдеры), доска с мелом, стенды с фотографиями выпускников,
   расписание на стене, шкаф с папками, портреты/грамоты, старые карты, коробки с тетрадями.
3. Интеракции (всё через существующие системы): 4–6 `DiscoveryTarget`-осмотров
   (`inspect-school-photos`, `inspect-school-schedule`, `inspect-school-desk-drawings`,
   `inspect-school-folder-graduates`, опционально `inspect-school-piano`), 2–3 документа
   (`DocumentUi`): список выпускников, «приказ о закрытии школы», детское сочинение.
4. Пасхалки (ТЗ §17.3): на фотографиях выпускников — молодые версии нынешних жителей
   (в текстах документов: «на втором ряду — Ринат…», «в центре — Алсу…»), в тетрадях — рисунок
   леса и Шурале, датированный 2009 годом. **Только текстом** — портретных ассетов не требуется
   (**PLACEHOLDER ACCEPTABLE**).
5. Никаких сверхъестественных механик: пустота и тишина — сам носитель атмосферы
   (амбиент — существующий `house_room_tone` как плейсхолдер, отдельный `school_room_tone` — позже).
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs` (+ partial), `Act1WorldLayout.cs`,
`Main.cs`, `content/modules/urman-chapter1/definitions.json` (+ 3 документа).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/Act1ConnectedWorld.School.cs`, `game/scenes/zones/chapter1_school.tscn`,
`content/modules/urman-chapter1/documents/school-*.md` (3 файла).
**Existing code to reuse:** `BuildFapClinic`-паттерн, `DiscoveryTarget`, `DocumentUi`,
`MakeInteractionBox` с `LockedPrompt`.
**Existing code to remove/deprecate:** —
**Dependencies:** INTERACT-001, NOTE-001, PLAYER-002 (проёмы/капсула).
**Scene/inspector setup (Godot):** зона `school_interior` (Interior: true) + двери с состояниями.
**Runtime behavior:** игрок входит через боковую дверь, осматривает класс и коридор, читает
документы; запертые двери показывают «Заперто».
**Edge cases:** игрок пытается выйти через забитый главный вход (заперт изнутри — сообщение);
прыжок на парту (капсула стоит на парте — допустимо, коллизии парт простые);
фотографии без ассетов (плейсхолдер-рамки + текст).
**Acceptance criteria:** зона существует, ≥ 4 осмотров и ≥ 2 документа; запертые двери не
открываются; ни одной новой системы не добавлено (только контент + существующие механики).
**Manual test:** войти в школу, осмотреть стенд и расписание, прочитать приказ о закрытии,
попробовать запертую дверь.

---

## COUNCIL-001 — Сельсовет / ДК (архив, план деревни, сцена)

**Priority:** P1
**Current state:** в Акте I ничего нет; серые зоны актов 2–5 (`fullgame_act2_council`,
`fullgame_act3_archive`, `fullgame_act3_soviet`) для демо недоступны.
**Problem:** ТЗ §18 требует сельсовет/ДК как источник визуальной истории деревни.
**Required implementation:**
1. Одно здание или комплекс: административный кабинет + зал со сценой + склад/архив
   (предложение: `x ≈ +30, z ≈ -40`, ближе к ФАП и к выезду).
2. Наполнение: столы, шкафы с папками, доска объявлений, сцена с занавесом, стулья в зале,
   стенд «История совхоза», старые афиши Сабантуя, грамоты, кубок, сломанный проектор.
3. Интеракции: `inspect-council-village-plan` (карта/план деревни — даёт знание о структуре
   авыла и подсвечивает зират/мечеть/школу), `inspect-council-archive-folder` (папка с
   документами: 2–3 документа через `DocumentUi`), `inspect-council-sabantuy-board`
   (афиши Сабантуя разных лет), `inspect-council-stage` (сцена, занавес).
4. Данные: 3–4 документа-контента (`council-village-plan.md`, `council-shop-history.md`,
   `council-sabantuy-1998.md`, `council-archive-letter.md`) — плейсхолдер-текст, но с реальными
   именами деревни/жителей (Мансур, Гөлсинә, Фанис).
5. Архив можно связать с ПК бабая (одни и те же люди в разных источниках — ТЗ §26), но
   дублирования документов избегать: разные документы об одних людях.
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs` (+ partial), `Act1WorldLayout.cs`,
`Main.cs`, `content/modules/urman-chapter1/definitions.json` (+4 документа).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/Act1ConnectedWorld.Council.cs`, `game/scenes/zones/chapter1_council.tscn`,
`content/modules/urman-chapter1/documents/council-*.md`.
**Existing code to reuse:** `FullGameZoneDressing.Act2Council/Act3Archive/Act3Soviet`
(`game/scripts/FullGameZoneDressing.cs:95–139`) как готовые схемы расстановки мебели —
перенести идеи, не код; `DiscoveryTarget`, `DocumentUi`.
**Existing code to remove/deprecate:** —
**Dependencies:** INTERACT-001, NOTE-001, CORE-001 (просмотрщик планов/фото).
**Scene/inspector setup (Godot):** зона `council_interior`, зал и кабинет.
**Runtime behavior:** игрок изучает план деревни (книжка «Адреса» пополняется), читает архив,
видит историю Сабантуя; в зале — тишина и пыль.
**Edge cases:** дверь в архив заперта до какого-то знания (по желанию, через `conditions`);
сцена в темноте (свет включается/не включается — одно правило).
**Acceptance criteria:** зона посещаема; ≥ 4 интеракции; ≥ 3 документа; план деревни добавляет
записи в «Адреса»; никаких новых систем сверх существующих.
**Manual test:** зайти в сельсовет, изучить план, прочитать папку, посмотреть афиши.

---

## ZIRAT-001 — Зират: имена, даты, связи

**Priority:** P1
**Current state:** зират — придорожная граница с маркерами и воротами
(`urman_zirat_roadside_kit.glb`: `ZiratBoundaryFence`, `ZiratOpenGate`, `ZiratMarkerGroup_Low/Far`,
`ZiratPathEdge`), физически открыт внутрь (меши `Zirat_*` исключены из коллизий,
`AgentBAct1ExteriorLayer.cs:752–767`), но без имён и интерактива; след у дороги —
`zirat-roadside-clue` (`StyleBenchmarkZone.cs:1660–1685`), обходы — «мостик»/«скамья»
(`Act1ConnectedWorld.CulvertVerandaDiscoveries.cs`, `RoadsideDiscoveries.cs`).
**Problem:** ТЗ §19 требует обычную часть авыла, где через имена/даты/могилы связываются
истории жителей; сейчас это декорация.
**Required implementation:**
1. Данные: NEW тип контента `grave` (см. DATA-001) — записи вида
   `urman.chapter1:grave/<slug>`: `{ name: {default, ru, tt}, birthYear, deathYear, familyId,
   epitaph, linkedCharacters: [...], spot: {x, z, yaw}, photoRef? }`. Минимум 12 надгробий,
   включая: род Мансура/Гөлсинә, родителей Марата, «безымянную 1974 года» (перекличка с
   документом `rec_internal_accounting_blank_1970s.md`).
2. Рендер: NEW FILE `game/scripts/ZiratMarkerField.cs` — строит каменные маркеры из данных
   (BoxMesh/CylinderMesh — **PLACEHOLDER ACCEPTABLE**), у каждого — `InteractionTarget`
   (`Kind = Discovery`) с промптом «Прочитать надпись»; чтение открывает небольшое `DocumentUi`-окно
   или HUD-панель с именем/годами/эпитафией (тексты — из данных grave).
3. Связи: если у надгробия указан `linkedCharacters`, после прочтения в книжку («Люди» и
   «Наблюдения») попадает связка «имя ↔ человек», а при наличии соответствующего знания —
   новая запись («Тот же человек, что на фотографии в архиве»). Реализация — через
   существующие `knowledge.set-status`/`journal.record` в эффектах интеракций (контент).
4. Коллизии: маркеры получают простые коллайдеры (`StaticBody3D` + `BoxShape3D`), чтобы игрок
   не проходил сквозь них; граница зирата остаётся проходимой через ворота.
5. Тон: никаких хоррор-эффектов; амбиент — существующий `zirat_wind.wav`
   (`game/assets/audio/zirat_wind.wav`).
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs` (+ partial), `Act1WorldLayout.cs`
(если добавляется поле/спавн), `content/modules/urman-chapter1/definitions.json` (записи grave,
интеракции), `content/schemas/` (новая схема), `scripts/content/compile-content.mjs`
(регистрация типа), `src-dotnet/Urman.Content/Compilation/ContentCompiler.cs` (`RegistryByKind`),
`content/schemas/compiled-content-pack.schema.json` (реестр `graves`), `game/scripts/CompiledCampaignRepository.cs`
(аксессор `Graves`).
**New files/components (NEW FILE, предлагаемые пути):**
`content/schemas/grave.schema.json`, `game/scripts/ZiratMarkerField.cs`,
`content/modules/urman-chapter1/graves.json` (или записи в `definitions.json`),
`game/tests/ZiratGravesSmokeTest.cs`.
**Existing code to reuse:** существующие ограды/ворота зирата, `zirat_wind.wav`,
`DiscoveryTarget`-паттерн, `JournalUi`/`RuntimeBridge` для записей.
**Existing code to remove/deprecate:** —
**Dependencies:** DATA-001, NOTE-001, CORE-002 (lore database).
**Scene/inspector setup (Godot):** маркеры — процедурные меши на существующей площади зирата
(координаты из данных `spot`), при этом **не** перекрывать существующий маршрут
(`AgentBAct1Layout.WalkChain` идёт западнее — не ломать).
**Runtime behavior:** игрок входит в зират через ворота, читает надписи, узнаёт имена,
связывает их с фото/архивом; записи появляются в книжке.
**Edge cases:** несколько надгробий рядом (луч выбирает ближайшее по центру — INTERACT-002);
надгробье без `linkedCharacters` (просто текст); чтение до получения архивных знаний
(запись появляется позже, при чтении документа — через контент-условия).
**Acceptance criteria:** ≥ 12 надгробий из данных; чтение даёт текст и запись; ≥ 3 связи
«надгробье ↔ персонаж» работают на финальном состоянии; маршрут и смоуки зирата зелёные.
**Manual test:** зайти в зират, прочитать 3 надгробья, открыть книжку — имена и связи на месте.

---

## BANYA-001 — Баня бабая: тревожная атмосфера

**Priority:** P1
**Current state:** бани нет; в ките есть только участок `VillageParcel_VariantC_BanyaYard`
(`Act1ConnectedWorld.cs:61`, `:3747–3748`).
**Problem:** ТЗ §13 требует баню как одну из самых тревожных бытовых локаций (печь, треск,
дерево, пар, темнота, минимум скримеров).
**Required implementation:**
1. Здание бани во дворе бабая (сруб 4×2.4×4 м, предбанник 2.4×2.4 м), доступное по дворовой
   дорожке; новая зона `banya_interior` (Interior: true).
2. Интерьер: печь-каменка (меш + оранжевая подсветка `OmniLight3D`), полки, ковш, кадка с водой,
   дрова, окно с морозным узором, пар.
3. **Пар/жар**: `GPUParticles3D` (или `CpuParticles3D`) с мягкими спрайтами и низкой
   `lifetime`; включается при входе игрока; при `ReducedMotion` — отключается, остаётся только
   свет/звук (доступность важнее эффекта).
4. **Звук**: три слоя — `banya_stove_crackle.wav` (треск), `banya_steam.wav` (шипение пара),
   `banya_room_tone.wav` (низкий гул) — NEW файлы, **PLACEHOLDER ACCEPTABLE**
   (временно: `house_room_tone.wav` + `keyboard_key`-подобный треск не использовать).
   Подключение — через `AmbientAudioDirector` (зональный бэд) + отдельный
   `AudioStreamPlayer` для печи с рандомизированным питчем.
5. **События** (по ТЗ — «subtle»): 2–3 одноразовых события, каждое — эффект контента:
   (а) скрип половицы при первом входе; (б) лёгкое затухание света на 1.5 с;
   (в) один раз — короткий «вздох» пара. Реализуются как `scene.request`/`audio.request`
   в `onEnter` сцены `banya_first_visit` (контент), без нового «хоррор-движка».
6. Никаких скримеров, монстров и «красного экрана» (ТЗ §13/§22). Баня — про ожидание и жар.
7. Интеракции: `inspect-banya-stove`, `inspect-banya-ladle`, `inspect-banya-window`
   (на окне — процарапанный знак, перекликающийся с рисунком Марата).
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs` (+ partial), `Act1WorldLayout.cs`,
`Main.cs`, `content/modules/urman-chapter1/definitions.json` (сцена, интеракции),
`game/assets/audio/ambient_manifest.json` (бэды), `game/scripts/AmbientAudioDirector.cs`.
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/Act1ConnectedWorld.Banya.cs`, `game/scenes/zones/chapter1_banya.tscn`,
3 аудио-файла (плейсхолдеры), `game/tests/BanyaAtmosphereSmokeTest.cs`.
**Existing code to reuse:** интерьерный паттерн ФАП/дома, `AddLantern`-свет, `AmbientAudioDirector`,
`Accessibility.ReducedMotion`, `UiFoley` (для скрипа — существующий `door_creak` с низким питчем,
если нет своего сэмпла).
**Existing code to remove/deprecate:** —
**Dependencies:** INTERACT-001 (двери), PHASE 1 (капсула/присед — низкая баня),
RADIO-001 не требуется.
**Scene/inspector setup (Godot):** зона-интерьер; печь с `OmniLight3D` (`d89b58`, энергия 1.0–1.4);
частицы пара — `GPUParticles3D` с `amount ≈ 60`.
**Runtime behavior:** тёплый тёмный сруб, треск печи, пар у каменки, запотевшее окно;
скрип половицы один раз; свет слегка «дышит»; игрок осматривает предметы; выход — тишина двора.
**Edge cases:** игрок стоит в бане долго (события не повторяются — флаги в `beats`/`knowledge`);
`ReducedMotion` (пар выключен, скрип — только один раз); вход в приседе (низкая дверь);
сохранение/загрузка в бане (события не повторяются).
**Acceptance criteria:** зона существует; 3 звуковых слоя подключены (плейсхолдеры допустимы);
пар работает и уважает reduced motion; ≤ 3 одноразовых события, каждое не повторяется после
загрузки; ни одного скримера (проверяется ревью кода: нет camera shake, нет мгновенных
громких звуков без контекста).
**Manual test:** войти в баню, постоять 30 секунд (треск, пар, одно событие), осмотреть печь,
выйти; войти снова — событий больше нет.

---

# PHASE 6 — Vehicles

Общее: **фреймворка транспорта нет** — ни `VehicleBody3D`, ни `RigidBody3D` в проекте не используются.
Поэтому сначала общий слой (VEH-001), затем три машины на его основе. Управление —
«аркадное, но бытовое»: без GTA-физики (ТЗ §8.1), простые коллизии, ограничение по authored-миру
(`FirstPersonController.ClampToAuthoredWorld` — образец логики границ).

## VEH-001 — Базовый слой транспорта (вход/выход, камера, состояние, сохранение)

**Priority:** P1
**Current state:** отсутствует.
**Problem:** три транспорта не должны быть тремя независимыми системами.
**Required implementation:**
1. NEW FILE `game/scripts/VehicleBase.cs` (`partial class VehicleBase : CharacterBody3D`):
   `[Export] public string VehicleId`, `EnterPoint`/`ExitPoint` (`Marker3D`), `[Export] float MaxSpeed`,
   `Acceleration`, `SteeringSpeed`, `[Export] NodePath DriverSeat`, `[Export] Camera3D VehicleCamera`;
   методы `Enter(FirstPersonController player)`, `Exit()`, `IsOccupied`, `CaptureState()`, `ApplyState()`.
   Движение — упрощённое: `MoveAndSlide()` с рулевым углом, скорость по `Input.GetVector`
   (те же действия `move_*`), торможение, ограничение по склонам (не ездить по крышам).
2. Камера: при входе игрок становится «невидимым пассажиром» — `FirstPersonController`
   отключается (`SetPhysicsProcess(false)` или `Visible=false` + `SetProcessInput(false)`),
   включается `VehicleCamera` (третье лицо с фиксированным смещением, `look_*` вращают обзор
   вокруг машины); при выходе — возврат управления и камеры. Реализовать без ломания
   существующих методов контроллера: использовать уже существующие
   `ApplyZoneSpawn`/`SetModalOpen`/`CapturePortableTransform`.
3. Интеракция: у машины `InteractionTarget` (`Kind = Vehicle`, `Prompt = "Сесть"` /
   «Выйти») → `VED`-мост: `RuntimeBridge` получает `EnterVehicle(vehicleId)` / `ExitVehicle()`
   (только presentation — состояние занятости в capability `urman.vehicles`, см. п.5).
4. Сохранение: NEW capability `urman.vehicles` (протокол `urman.vehicles:capability/parking`,
   модуль `content/modules/urman-vehicles/` по образцу oldpc): состояние
   `{ vehicles: { <id>: { zoneId, x, y, z, yaw, fuelless: true } }, currentVehicleId }
   ` — позиции машин и «в какой машине игрок» (если решено сохранять факт поездки — иначе
   при загрузке игрок выходит из машины автоматически: **так проще и безопаснее, выбрать это**).
5. Радио (PHASE 7) включается из VEH-001 (общий хук `OnEnterVehicle`).
6. Звук: `engine_idle.wav`, `engine_rev.wav`, `door_slam.wav` — NEW, **PLACEHOLDER ACCEPTABLE**
   (допустим один зацикленный плейсхолдер + питч по скорости).
**Files to modify:** `game/scenes/main.tscn` (инстансы машин как сцены-инстансы зон/мира),
`game/scripts/Act1ConnectedWorld.cs` (размещение), `game/scripts/Main.cs` (если нужен пере-
зон при поездке — не нужно, мир один), `game/scripts/RuntimeBridge.cs` (мост),
`content/campaigns/urman.chapter1/campaign.json` (модуль vehicles + capability).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/VehicleBase.cs`, `content/modules/urman-vehicles/{module.json,capabilities.json,definitions.json,schemas/*}`,
`src-dotnet/Urman.Core/Capabilities/Vehicles/VehicleParkingCapabilityProvider.cs`,
`game/tests/VehicleEnterExitSmokeTest.cs`.
**Existing code to reuse:** `InteractionTarget` (вход/выход), `FirstPersonController`
(вход/выход через существующие методы), capability-паттерн oldpc, `AgentBAct1HeightField`
(для посадки колёс на землю — `CollisionGround`), `Act1WorldLayout` (зоны/координаты).
**Existing code to remove/deprecate:** —
**Dependencies:** INTERACT-002 (`Kind = Vehicle`), PHASE 5 (парковки), RADIO-001 (хук).
**Scene/inspector setup (Godot):** `VehicleBase`-инстанс: `CharacterBody3D` + `CollisionShape3D`
(прямоугольная капсула/бокс), `Marker3D` `EnterPoint`/`ExitPoint`, `Camera3D`,
`AudioStreamPlayer3D` (двигатель), `MeshInstance3D` (плейсхолдер).
**Runtime behavior:** подойти к машине → `E` «Сесть» → камера снаружи, управление машиной →
`E` «Выйти» → игрок появляется у двери машины (`ExitPoint`, с проверкой свободного места).
**Edge cases:** выход в стену/воду/на склон (проверить `ExitPoint` запросом формы; если занято —
не выходить и показать «Здесь не выйти»); выход в момент движения (сначала остановка, потом выход);
загрузка сейва «в машине» (игрок выходит автоматически на парковку); падение машины с обрыва
(ограничение скорости + сброс на ближайшую authored-точку, как `FallRecoveries`).
**Acceptance criteria:** вход/выход работают на всех трёх машинах через один базовый слой;
состояние парковки сохраняется; при выходе игрок всегда на земле; ни один смоук не падает.
**Manual test:** сесть в Ниву, проехать 50 м, выйти; сохранить и загрузить — машина стоит там,
где оставлена, игрок рядом.

---

## VEH-002 — Старая Нива (деревня)

**Priority:** P1
**Current state:** отсутствует; в архиве ПК есть квитанция про Ниву
(`doc_household_misc_niva_receipt.md`: «Нива: заменить ремень, проверить масло, не слушать Фаниса…»).
**Problem:** ТЗ §8.1 — основной транспорт по деревне.
**Required implementation:**
1. Модель-плейсхолдер: коробка 4.1×1.5×1.75 с колёсами-цилиндрами, фары (2 `OmniLight3D`
   слабые), салон-плейсхолдер. **PLACEHOLDER ACCEPTABLE**; при появлении арта — подмена меша.
2. Парковка: у двора бабая (за воротами, чтобы не мешать пешему маршруту) — координаты
   подобрать по `BabaiYardOpenGate`/`BabaiYardSideGate`.
3. Настройки: `MaxSpeed 12 м/с` (деревенская езда), ускорение 6, руль 1.6 рад/с; звук двигателя
   по питчу; при столкновении — глухой удар (`door_slam`-подобный плейсхолдер).
4. Взаимодействие с миром: машина едет по существующим дорогам (меши дорог есть:
   `urman_wet_village_road_kit.glb`), не обязана иметь физику подвески; выезд за authored-зону
   запрещён (`Act1WorldLayout`-границы — как у игрока).
5. Радио: при входе включается станция (RADIO-001/002), `R` переключает канал/выключает.
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs`, `Act1WorldLayout.cs` (точка парковки),
`content/modules/urman-vehicles/definitions.json` (машина №1), `content/modules/urman-chapter1/definitions.json`
(интеракция «Сесть в Ниву», тексты).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scenes/vehicles/niva.tscn`, `game/scripts/vehicles/NivaVehicle.cs` (тонкий наследник VEH-001).
**Existing code to reuse:** VEH-001, существующие дороги/заборы, `AddVisualBox`-паттерн.
**Existing code to remove/deprecate:** —
**Dependencies:** VEH-001, START-001 (парковка рядом с деревней), RADIO-001.
**Scene/inspector setup (Godot):** сцена машины с `VehicleBase`-скриптом, `Camera3D` за кабиной,
`AudioStreamPlayer3D` двигателя, `EnterPoint`/`ExitPoint`.
**Runtime behavior:** игрок садится, едет по деревне (быстрее пешком), радио играет,
останавливается у магазина/ФАП/сельсовета, выходит.
**Edge cases:** езда по тропинкам без дорог (разрешена, но с тряской/замедлением — по желанию);
наезд на NPC (не реализовывать травмы: NPC неуязвимы, машина просто упирается);
ночная зона (машина доезжает до зиратской дороги, дальше — пешком/телега, въезд в лес запрещён
авторитетно: барьер по `kara_urman_night`).
**Acceptance criteria:** Нива управляема, скорость ≈12 м/с, радио играет, парковка сохраняется,
въезд в лес блокирован, пеший маршрут не перекрыт (смоук обхода зелёный).
**Manual test:** сесть, проехать от двора до ФАП и вернуться, выйти.

---

## VEH-003 — Мотоцикл (быстрый транспорт)

**Priority:** P2
**Current state:** отсутствует.
**Problem:** ТЗ §8.2 — быстрый транспорт и «просто кататься».
**Required implementation:**
1. Наследник VEH-001 с параметрами `MaxSpeed 18 м/с`, ускорение 9, руль 2.2 рад/с,
   наклон в поворотах (визуальный roll до 8°), без крыши/кабины (камера ближе, обзор шире).
2. Парковка: у магазина или у дома (одна точка), при желании — «найдётся в сарае»
   после `knowledge/...` (по желанию).
3. Звук: `engine_moto.wav` (плейсхолдер), деревенский рёв.
4. Радио: **нет** (ТЗ допускает без радио; если хочется — маленький приёмник в кармане,
   но это усложняет — не делать).
**Files to modify:** те же, что VEH-002 (другая конфигурация).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scenes/vehicles/motorcycle.tscn`, `game/scripts/vehicles/MotorcycleVehicle.cs`.
**Existing code to reuse:** VEH-001.
**Existing code to remove/deprecate:** —
**Dependencies:** VEH-001.
**Scene/inspector setup (Godot):** сцена мотоцикла, `EnterPoint` сверху, камера чуть выше и ближе.
**Runtime behavior:** быстрая езда, наклон в поворотах, ощущение свободы; без радио.
**Edge cases:** падение с мотоцикла не реализуется; столкновение — остановка;
набор скорости на бездорожье — замедление.
**Acceptance criteria:** максимальная скорость ≈18 м/с, наклон работает, скорость сохраняется
(как у VEH-001), смоук VEH-001 проходит и на мотоцикле.
**Manual test:** проехать деревню на мотоцикле туда-обратно.

---

## VEH-004 — Лошадь и телега (поездки в лес)

**Priority:** P1
**Current state:** отсутствует; лес связан с телегой по ТЗ (§8.3, §23), сейчас в лес ведёт
только пеший путь через `zirat-road-to-forest` → `scene/forest-approach` → `scene/forest`.
**Problem:** ТЗ §8.3/§23: телега — транспорт игрока для леса, а не декорация; поездка должна
сильно отличаться по ощущению от машины.
**Required implementation:**
1. Наследник VEH-001 с иной моделью движения: `MaxSpeed 5 м/с` (шаг), ускорение 2, медленный
   разворот 1.0 рад/с; обязательная визуальная обратная связь — покачивание телеги, стук копыт
   (`hooves_*.wav`, плейсхолдер), скрип дерева.
2. Лошадь — не отдельный NPC-интеллект: единый «vehicle»-объект (лошадь + телега + оглобли),
   **без** AI/навигации; это осознанное упрощение (ТЗ §23/§25).
3. Парковка: у двора бабая / у зиратской дороги (вторая точка — «у кромки леса»); поездка
   разрешена по существующим дорогам до `kara_urman_night` и по лесной тропе
   (нужна проверка: тропа в `AgentBAct1Layout.KaraRoadAxis` шириной 3.5 м — телега шириной
   ≤ 1.8 м проходит).
4. Игрок может сойти где угодно (по VEH-001), но телега **остаётся** там, где оставлена;
   пешая часть леса (кромка, клиффхэнгер) — пешком, как сейчас.
5. Сход с телеги в лесу не телепортирует игрока и не ломает клиффхэнгер: финальные сцены
   `scene/forest-approach`/`scene/forest` требуют пешего присутствия — переход к ним остаётся
   интеракцией `zirat-road-to-forest`/`forest-approach-to-forest` (не менять условия).
**Files to modify:** `game/scripts/Act1ConnectedWorld.cs`, `Act1WorldLayout.cs`,
`content/modules/urman-vehicles/definitions.json`, `content/modules/urman-chapter1/definitions.json`.
**New files/components (NEW FILE, предлагаемые пути):**
`game/scenes/vehicles/horse_cart.tscn`, `game/scripts/vehicles/HorseCartVehicle.cs`.
**Existing code to reuse:** VEH-001, `KaraRoadAxis`/`ZiratRoadAxis` (`AgentBAct1Layout.cs:38–55`),
существующие условия финала.
**Existing code to remove/deprecate:** —
**Dependencies:** VEH-001, START-001, MOSQUE-* нет; FOREST-001 (правила поездки в лес).
**Scene/inspector setup (Godot):** модель «лошадь+телега», `EnterPoint` (сиденье),
`ExitPoint` (слева сзади), камера от третьего лица чуть выше телеги.
**Runtime behavior:** игрок садится, едет медленно и «бытово», лошадь идёт рысцой;
у кромки леса оставляет телегу, дальше идёт пешком; радио — **нет** (или тишина/ветер).
**Edge cases:** оставить телегу на дороге (никто не увозит); сесть в телегу из леса (да, работает);
загрузка сейва «телега в лесу» (позиция восстанавливается, игрок рядом);
лошадь «устала» — не реализовывать, чтобы не плодить системы.
**Acceptance criteria:** телега управляема, скорость ≈5 м/с, ощущение отличается от Нивы
(проверяется кадрами/ревью), финальный маршрут пешком не изменён, состояние сохраняется.
**Manual test:** доехать на телеге до кромки Кара-Урмана, сойти, пройти финал пешком.

---

# PHASE 7 — Radio system

## RADIO-001 — Ядро радиосистемы

**Priority:** P1
**Current state:** радио отсутствует; есть декоративный приёмник (`StyleBenchmarkZone.cs:1765–1769`)
и документ «Заметки о приёме радиоприёмника»; шины аудио — `Master/Ambience/Voice/SFX`
(`game/scripts/AudioSettingsService.cs:14–17`).
**Problem:** ТЗ §9 требует аутентичной радиосистемы с каналами, сегментами, расписанием и
локальным контентом; сейчас нет ни плеера, ни данных.
**Required implementation:**
1. **Шина**: добавить `RadioBus = "Radio"` в `AudioSettingsService.RoutedBuses` и `DefaultVolumes`
   (`:19–20, :79–83`), слайдер «Радио» в `SettingsUi` рядом с остальными громкостями,
   тест — расширить `game/tests/Act1AudioSettingsSmokeTest.cs`.
2. **Capability**: NEW модуль `content/modules/urman-radio/`
   (протокол `urman.radio:capability/receiver`, версия 1.0.0) с состоянием
   `{ on: bool, stationId, segmentCursor, lastFinishedAt, volumeHint }` и командами
   `radio.toggle`, `radio.next`, `radio.tune(stationId)`.
   Провайдер: NEW FILE `src-dotnet/Urman.Core/Capabilities/Radio/RadioCapabilityProvider.cs`.
3. **Модель контента** (NEW `content/modules/urman-radio/definitions.json` + схемы
   `schemas/radio-config.schema.json`):
   - `station`: `{ id, name: {ru, tt}, description, segments: [segmentId] }`;
   - `segment`: `{ id, kind: "music"|"speech"|"weather"|"greeting"|"news"|"ads"|"salam",
     audioAssetId, captionsTextId?, transcriptTextId?, durationSeconds, weight, timeOfDay? }`;
   - чередование: плейлист собирается как «музыка → короткий сегмент → музыка → …», где короткие
     сегменты детерминированно выбираются из пула (RNG-поток уже есть в ядре —
     `OwnerRngStreams`, использовать его, а не `Random`).
4. **Плеер**: NEW FILE `game/scripts/RadioReceiver.cs` (`Node`) — `AudioStreamPlayer` на шине
   Radio, очередь сегментов, кроссфейд 0.3 с между сегментами, титры через `AudioCueUi`-подобный
   оверлей или существующий `AudioCueUi` (переиспользовать его субтитры).
5. **Расширение мира**: приёмник в доме бабая можно включить (`inspect-radio` на существующем
   декоративном приёмнике переделать в `InteractionTarget`), и он же — часть Нивы (VEH-002).
6. Сохранение: состояние в capability snapshot (сохраняется автоматически).

NOTE: currently, `StyleBenchmarkZone.MakeRadioAndHerbs` (`:1765-1769`) creates the radio only as decoration in a benchmark zone; the connected world suppresses benchmark presentation, so the receiver should be placed by `Act1ConnectedWorld` (`AddVisualBox`) and accompanied by a new `InteractionTarget` per INTERACT-002.

**Files to modify:** `game/scripts/AudioSettingsService.cs`, `game/scripts/SettingsUi.cs`,
`game/scripts/RuntimeBridge.cs` (мост), `game/scenes/main.tscn` (узел `RadioReceiver`),
`content/campaigns/urman.chapter1/campaign.json` (модуль + capability).
**New files/components (NEW FILE, предлагаемые пути):**
`content/modules/urman-radio/{module.json,capabilities.json,definitions.json,schemas/*}`,
`src-dotnet/Urman.Core/Capabilities/Radio/RadioCapabilityProvider.cs`,
`game/scripts/RadioReceiver.cs`, `game/tests/RadioSystemSmokeTest.cs`.
**Existing code to reuse:** `AudioSettingsService` (шины), `AudioCueUi` (субтитры),
`OwnerRngStreams` (детерминизм), capability-паттерн oldpc, `AmbientAudioDirector` (как образец
управления аудио-состоянием без нарратива).
**Existing code to remove/deprecate:** декоративный `OldRadio`-меш бенчмарка не трогать
(он вне связанного мира); для дома — новый приёмник.
**Dependencies:** DATA-001, SAVE-001; для Нивы — VEH-002.
**Scene/inspector setup (Godot):** `RadioReceiver` (`Node`) с `AudioStreamPlayer`
(`Bus = "Radio"`), подключение к `RuntimeBridge` для команд.
**Runtime behavior:** включение радио → играет станция: музыка, затем короткий сегмент
(новости/погода/объявление/салам), затем снова музыка; `R` — следующая станция/выключение;
субтитры доступны; громкость/мьют влияют только на радио.
**Edge cases:** два источника (дом + Нива) одновременно — играет только один (побеждает источник,
в котором находится игрок); переключение канала во время речи (плавное завершение сегмента);
`ReducedMotion` (без визуального «эквалайзера», звук остаётся); пауза игры (`PauseMenu`) —
радио ставится на паузу; отсутствие аудио-ассета сегмента (сегмент пропускается с warning).
**Acceptance criteria:** ≥ 2 станции; ≥ 5 видов сегментов; детерминированное воспроизведение
(один и тот же сейв → одна и та же последовательность); пауза/возобновление работают;
новый смоук `RadioSystemSmokeTest` зелёный; шина Radio регулируется в настройках.
**Manual test:** включить радио в доме, послушать цикл «музыка → объявление → музыка»,
выйти на улицу (радио не слышно), вернуться; проверить субтитры.

---

## RADIO-002 — Контент станции «Кырлай» и локальная радиостанция

**Priority:** P1
**Current state:** контента нет; есть только документ с заметками о приёме радио
(`doc_household_radio_log.md` — «помехи», «вечером лучше»).
**Problem:** ТЗ §9 требует татарскую музыку, речь, местные объявления, новости, погоду,
поздравления, саламы, упоминания соседних деревень.
**Required implementation:**
1. Контент-структура: `content/modules/urman-radio/definitions.json` — станции:
   - «Кырлай Радиосы» (местная): новости района, объявления, поздравления, саламы, погода,
     татарская музыка (плейсхолдер-аудио);
   - «ТНВ-радио»-подобная (музыкальная, общереспубликанская) — как вторая станция для переключения.
2. Наполнение (минимум, тексты пишет контент-автор, **PLACEHOLDER AUDIO ACCEPTABLE**):
   - 6–8 музыкальных сегментов-плейсхолдеров (зацикленные инструментальные лупы);
   - 4 новостных («выезд фельдшера», «ремонт моста», «приезд комиссии», «отключение света»);
   - 3 погодных («мороз крепчает», «к вечеру метель», «оттепель»);
   - 3 объявления («ищу сено», «продам картошку», «собрание в клубе»);
   - 2 поздравления и 2 салама с упоминанием соседних деревень (предложить названия:
     «Югары Кырлай», «Түбән Бистә» — **новые имена, согласовать с автором/каноном, помечены в
     отчёте**);
   - 2 «тревожных» сегмента с намёками (для последней трети акта — через условия
     `knowledge/clue_*`), например «на кромке опять слышали тавыш».
3. Связь с миром: объявления должны перекликаться с досками объявлений магазина (SHOP-001),
   соцсетью (PC-005) и документами архива (одни и те же люди/события) — ТЗ §26.
4. Языковой слой: тексты станции — двуязычные (`{ru, tt}`), при озвучке — только татарская речь
   с русскими субтитрами (правило проекта: татарский присутствует, но понятен).
5. Тишина как контент: между сегментами допустимы паузы 1–2 с (радио не должно быть бесконечным
   потоком), в ночной зоне станция «теряется» (шум/помехи — отдельный сегмент-плейсхолдер).
**Files to modify:** `content/modules/urman-radio/definitions.json`,
`content/modules/urman-radio/assets/*` (аудио-плейсхолдеры),
`game/assets/audio/ambient_manifest.json` (если музыка идёт как лупы),
`content/modules/urman-chapter1/definitions.json` (тексты-переклички).
**New files/components (NEW FILE, предлагаемые пути):**
аудио-плейсхолдеры в `game/assets/audio/radio/`, тексты в `urman-radio/definitions.json`,
`docs/urman_knowledge_base/audio/radio_style_guide.md` (краткое описание тона станции).
**Existing code to reuse:** RADIO-001, `TextResolver` (двуязычные тексты),
`AudioCueUi`-субтитры.
**Existing code to remove/deprecate:** —
**Dependencies:** RADIO-001, SHOP-001/PC-005 (для перекличек, но не блокирующе).
**Scene/inspector setup (Godot):** —
**Runtime behavior:** радио звучит как районная станция: музыка, голос диктора (татарский),
погода, объявления, саламы; иногда — помехи в плохой зоне.
**Edge cases:** конец плейлиста (цикл продолжается с начала, без «щелчка»); сегмент без аудио
(пропуск); переключение станции посреди речи; сегмент с условием, не выполненным в первом акте
(не воспроизводится).
**Acceptance criteria:** ≥ 2 станции, ≥ 20 сегментов суммарно (плейсхолдеры допустимы);
≥ 8 текстов на татарском с русскими субтитрами; сегменты с условиями не звучат до их выполнения;
языковой лист ревью обновлён для новых строк.
**Manual test:** включить радио, дождаться блока «музыка → погода → объявление → музыка»,
переключить станцию, доехать до кромки леса и услышать помехи.

---

# PHASE 8 — Forest / cart / Shurale

## FOREST-001 — Переход деревня → лес и правила телеги

**Priority:** P1
**Current state:** переходы существуют и работают как контент-интеракции:
`zirat-road-to-forest` (условие `knowledge/clue_marat_last_route_near_zirat = confirmed`)
→ `scene/forest-approach`; `forest-approach-to-forest` → `scene/forest`
(`content/modules/urman-chapter1/definitions.json`); карта дороги — `AgentBAct1Layout.KaraRoadAxis`.
Телеги нет (VEH-004).
**Problem:** ТЗ §23 требует, чтобы телега была органично связана с лесом, а поездка отличалась
от машины; сейчас лес — только пеший маршрут.
**Required implementation:**
1. Определить и записать правилами (в контенте и в документации):
   - пешком в лес — можно как сейчас (существующие интеракции не меняются);
   - на телеге — можно доехать до точки «у кромки» (новый спавн/якорь
     `kara_urman_night@cart_edge`, объявленный в `Act1WorldLayout`, см. FIX-002-паттерн);
   - на Ниве/мотоцикле в лес — нельзя (барьер: интеракция въезда показывает
     «Дальше только пешком или на телеге»; реализуется условием в контенте).
2. Точки: где взять телегу (двор бабая), где оставить (кромка леса), что происходит при сходе
   (телега стоит, лошадь «ждёт» — без AI).
3. Атмосферный переход: при поездке на телеге включается лесной амбиент
   (`kara_urman_edge_ambience.wav`), при пешем входе — как сейчас.
4. Не менять условия финала: `scene/forest` остаётся скриптовой сценой с двумя
   `audio.request` (голос Марата + вмешательство Рината).
**Files to modify:** `game/scripts/Act1WorldLayout.cs` (новый спавн),
`content/modules/urman-chapter1/definitions.json` (условие/тексты барьера),
`game/scripts/Act1ConnectedWorld.cs` (точка «у кромки»).
**New files/components (NEW FILE, предлагаемые пути):**
документ `docs/production/forest_access_rules.md` (правила доступа в лес).
**Existing code to reuse:** VEH-004, существующие сцены леса и условия,
`AmbientAudioDirector.SetZone`.
**Existing code to remove/deprecate:** —
**Dependencies:** VEH-004, FIX-002-подход к спавнам.
**Scene/inspector setup (Godot):** новый анкер/спавн у кромки; точка парковки телеги.
**Runtime behavior:** игрок запрягает/садится, едет медленно, у кромки слезает, дальше пешком;
Нива доезжает до зиратской дороги и останавливается сообщением.
**Edge cases:** приехал на телеге, ушёл пешком, вернулся — телега там же;
загрузка сейва «в лесу без телеги» — игрок на пешем спавне;
попытка въехать в лес на Ниве — сообщение, машина не проходит.
**Acceptance criteria:** правила доступа работают (проверяется смоуком: три вида въезда);
финальные интеракции не изменены (`ChapterOneFlowSmokeTest` зелёный); телега сохраняет позицию.
**Manual test:** пройти три варианта: пешком, на телеге, на Ниве (последний — отказ).

---

## SHURALE-001 — Постепенное присутствие Шурале

**Priority:** P2
**Current state:** сущности/логики Шурале нет: grep `Shurale/Шүрәле` в `game/scripts` — только
документ ТатВики (`StyleBenchmarkZone.cs:705, 713`). Присутствие сейчас — текст статьи,
голос Марата на кромке (`audio-marat-voice`), клиффхэнгер. Агрессивного AI/монстра нет
(и не должно появиться).
**Problem:** ТЗ §24 требует постепенного присутствия (звук, силуэт, движение, странности среды),
полное появление — осознанно и редко.
**Required implementation:**
1. NEW FILE `game/scripts/PresenceDirector.cs` (NEW FILE) — расширение идеи
   `SupernaturalDirector` (MOSQUE-003): уровни присутствия `0..3`, вычисляемые из
   состояния (`knowledge/clue_*`, `beat/*`, зона) и подмешиваемые в презентацию:
   - уровень 0: ничего;
   - уровень 1: редкие звуки (хруст ветки за спиной, дальний «тавыш») — набор сегментов
     `audio-marat-voice`/новые плейсхолдеры, не чаще 1 раза в 3–5 минут;
   - уровень 2: средовые аномалии — короткая тень/силуэт на границе кадра (плейсхолдер:
     `MeshInstance3D` с тёмным материалом, появляется на 0.4 с в 15–25 м от игрока, только
     если игрок не смотрит прямо на него), затухание звука леса;
   - уровень 3: единственное «полное» появление — уже реализованный финал
     (`scene/forest` + клиффхэнгер), не менять.
2. Правила: присутствие **никогда** не атакует, не блокирует маршрут, не открывает UI,
   не издаёт громких скримеров; все события — с `eventKind = "supernatural"`, чтобы
   safe zone их подавляла (MOSQUE-003).
3. Детерминизм: выбор момента — через `OwnerRngStreams`, чтобы повторный проход/загрузка
   не «переигрывали» события иначе.
4. Data-driven: список атмосферных событий — в контенте (`content/modules/urman-chapter1/definitions.json`
   или новый модуль `urman-presence`), с полями `{ zoneId, kind, minLevel, cooldownSeconds,
   audioAssetId?, visualAssetId? }`.
**Files to modify:** `game/scripts/RuntimeBridge.cs` (мост событий, если нужно),
`content/modules/urman-chapter1/definitions.json` (события), `game/scenes/main.tscn`
(узел директора, если не создан в MOSQUE-003).
**New files/components (NEW FILE, предлагаемые пути):**
`game/scripts/PresenceDirector.cs`, `content/modules/urman-presence/definitions.json`
(если решено отдельным модулем), 3–4 аудио-плейсхолдера.
**Existing code to reuse:** `SupernaturalDirector` (MOSQUE-003), `OwnerRngStreams`,
`AmbientAudioDirector`, `AudioCueUi`, ночная зона `kara_urman_night`, финальные сцены.
**Existing code to remove/deprecate:** ничего (агрессивного AI нет — удалять нечего).
**Dependencies:** MOSQUE-003 (реестр подавления), RADIO-001 (не обязательно).
**Scene/inspector setup (Godot):** узел директора в `main.tscn`; плейсхолдер-силуэт — `Node3D`
с тёмным мешем, скрытый по умолчанию.
**Runtime behavior:** в начале игры ничего не происходит; ближе к зиратской дороге и кромке
начинаются редкие звуки; у кромки — короткие силуэты; финал — существующий клиффхэнгер;
внутри мечети — ничего.
**Edge cases:** игрок оборачивается в момент появления силуэта (появление отменяется);
игрок в безопасной зоне (ничего); игрок проходит маршрут повторно (события не повторяются
в том же объёме — кулдауны в состоянии); загрузка сейва в лесу (кулдауны восстанавливаются,
события не «залипают»).
**Acceptance criteria:** уровни 0–2 наблюдаемы и не нарушают прохождение; ни одного скримера,
урона, погони; события детерминированы (смоук: два прогона с одного сейва дают одну
последовательность); safe zone подавляет события.
**Manual test:** пройти дорогу к зирату и к кромке; услышать ≥ 2 события, увидеть силуэт
(если не смотреть прямо), зайти в мечеть — тишина.

---

# Сквозные (cross-cutting) задачи

## CORE-001 — Общий просмотрщик «инспектируемых» объектов (документы, фото, планы, надписи)

**Priority:** P1
**Current state:** `DocumentUi` (EXISTS) показывает markdown-документ с кнопкой «Сохранить в журнал»
(`game/scripts/DocumentUi.cs`); фото/планы/надписи показываются только текстом в документах;
арт-ассеты (`assetRefs`) рантайм **не читает** (проверено: в `game/scripts` нет ссылок на
`assetRefs`; в паку они есть как логические ссылки).
**Problem:** школе, ДК, зирату, магазину и компьютеру нужен единый способ показывать
изображение + подпись + источник, а не «текст с описанием картинки».
**Required implementation:**
1. Расширить `DocumentUi` поддержкой изображений: `CompiledDocumentContent` получает
   `ImageAssetIds` (из `assetRefs`), UI показывает `TextureRect` над текстом (или рядом),
   если у документа есть изображение; порядок: изображение → подпись → текст.
2. Новый резолвер изображений: NEW FILE `game/scripts/DocumentArtResolver.cs`.
   Важно: сегодня все арт-ассеты контента — это **логические ссылки** (`kind: image`,
   `mediaType: application/vnd.urman.logical-asset-ref`, `file: logical/<name>.ref`, а сам `.ref`
   содержит только id), реальных файлов нет. Поэтому: (а) для каждой картинки добавить в модуль
   полноценную запись ассета (`kind: image`, `mediaType: image/png`, `file: images/<name>.png`),
   (б) положить файл в `game/assets/images/` (плейсхолдер-картинка допустима),
   (в) резолвер переводит assetId → `res://assets/images/<file>`, при отсутствии файла — рамка
   «изображение недоступно» (`PLACEHOLDER ACCEPTABLE`).
   Сверяться с `assets/asset_registry.json` (там формат `{id, source, derived, materials, status}`
   для 3D-китов) — не ломать его схему, при необходимости добавить записи для новых файлов.
3. Поддержать в документах поле `imageCaption` (текст подписи, двуязычный) — добавление в
   `content/schemas/document.schema.json` и `compiled-content-pack.schema.json`.
4. Единый вызов: `InteractionTarget` уже умеет `DocumentId` → ничего не менять; для надписей
   на надгробиях (ZIRAT-001) использовать тот же UI с коротким документом.
**Files to modify:** `game/scripts/DocumentUi.cs`, сцена `game/scenes/ui/document_ui.tscn`, `game/scripts/CompiledCampaignRepository.cs`,
`content/schemas/document.schema.json`, `src-dotnet/Urman.Content/Compilation/ContentCompiler.cs`
(проброс `assetRefs` в документ).
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/DocumentArtResolver.cs`,
`game/assets/images/` (каталог с плейсхолдерами).
**Existing code to reuse:** `DocumentUi`, `RichTextLabel`, существующий `ui-document-viewer-template`
(логический арт-референс), `PainterlyMaterialLibrary` (для рамок/фона).
**Existing code to remove/deprecate:** —
**Dependencies:** DATA-001 (схема), NOTE-001 (записи).
**Scene/inspector setup (Godot):** в `document_ui.tscn` добавить `TextureRect` (плейсхолдер-рамка),
`AspectRatioContainer` для сохранения пропорций.
**Runtime behavior:** открытие документа с изображением показывает картинку и подпись;
без изображения — как сейчас.
**Edge cases:** отсутствующий файл изображения (рамка + «изображение недоступно»); очень
широкое изображение (масштаб по ширине панели); несколько изображений (галерея со стрелками,
если понадобится — опционально).
**Acceptance criteria:** документ с `assetRefs` показывает изображение (или плейсхолдер);
тексты без картинок не изменились; `DocumentUi`-смоуки зелёные.
**Manual test:** открыть документ с фото (после появления такого контента), проверить масштаб
и подпись.

---

## CORE-002 — Lore database (люди, могилы, фото, документы) для книжки

**Priority:** P1
**Current state:** реестры `characters`, `documents`, `knowledge`, `grave` (появится в ZIRAT-001)
разрознены; книжка (NOTE-001) собирает их проекциями.
**Problem:** чтобы «один человек встречался на фото, в архиве, на зирате» (ТЗ §26), нужна единая
модель связей, а не пять независимых списков.
**Required implementation:**
1. NEW FILE `game/scripts/LoreIndex.cs` (NEW FILE): строит индекс `entityId → {
   displayName, kind, portraitRef?, mentions: [ {sourceId, sourceTitle, kind} ] }`
   из реестров пака (`characters`, `documents` + их `knowledgeRefs`, `graves` + `linkedCharacters`,
   `knowledge` с `sourceIds`).
2. Индекс — **только представление**, без состояния: строится при загрузке пака и обновляется
   по `RuntimeStateChanged` (знание подтверждено/запись в журнале → появляется упоминание).
3. Книжка и документы используют индекс: в «Люди» — связанные упоминания; в «Наблюдения» —
   обратный список «кто упоминается в этом документе»; на зирате — «тот же человек, что …».
4. Производительность: индекс строится один раз (≈ 8 персонажей, 26+ документов, 12+ могил) —
   никакой оптимизации не требуется.
**Files to modify:** `game/scripts/RuntimeBridge.cs` (доступ к индексу), `game/scripts/JournalUi.cs`
(использование), `game/scripts/CompiledCampaignRepository.cs` (аксессоры реестров).
**New files/components (NEW FILE, предлагаемые пути):** `game/scripts/LoreIndex.cs`,
`game/tests/LoreIndexSmokeTest.cs`.
**Existing code to reuse:** существующие проекции и реестры, `RuntimeStateChanged`.
**Existing code to remove/deprecate:** —
**Dependencies:** NOTE-001, ZIRAT-001 (grave-реестр), DATA-001.
**Scene/inspector setup (Godot):** —
**Runtime behavior:** игрок читает документ и видит «Упоминается: Мансур, Марат»; в книжке
у человека — список мест, где он встречался.
**Edge cases:** персонаж без упоминаний (пустой список); могила без связи (только имя);
дубли упоминаний (дедупликация по `sourceId`).
**Acceptance criteria:** индекс содержит ≥ 8 персонажей и ≥ 10 перекрёстных упоминаний на
финальном состоянии; книжка показывает связи; смоук проверяет 3 известные связи
(Мансур↔реестр дома, Марат↔архив, Гөлсинә↔квитанция).
**Manual test:** открыть документ «Реестр дома: линия Мансура» и книжку — связи видны.

---

## DATA-001 — Расширение схем контента для новых типов

**Priority:** P1
**Current state:** типы контента закрыты: `RegistryByKind` в
`src-dotnet/Urman.Content/Compilation/ContentCompiler.cs:21–33` (.NET) и
`scripts/content/compile-content.mjs:8–19` (JS); схемы — в `content/schemas/*.schema.json`
(`document.schema.json` — `additionalProperties: false`, есть `oldPc` как образец типизированного блока;
`compiled-content-pack.schema.json` перечисляет `registries`).
**Problem:** новым системам нужны данные: `grave`, `place`, `site` (или блоки `site`/`fs` в документах),
`radio` (модуль-капабилити), `selsmag` (ledger).
**Required implementation:**
1. Выбрать минимальный набор новых типов (рекомендуется):
   - **в документах** (без новых kind): `fs` (PC-001), `site` (PC-005), `imageCaption` (CORE-001);
   - **новые kind**: `grave` (ZIRAT-001), `place` (NOTE-001 «Адреса»);
   - **новые модули**: `urman-radio` (RADIO-001), `urman-selsmag` (SHOP-002), `urman-vehicles` (VEH-001).
2. Для каждого нового kind: схема `content/schemas/<kind>.schema.json` + регистрация в
   `RegistryByKind` (.NET) + `REGISTRY_BY_KIND` (JS) + реестр в `compiled-content-pack.schema.json`
   (`properties` и `required`) + аксессор в `CompiledCampaignRepository` + тесты контента.
3. Для каждого нового модуля-capability: 5 схем (`*-config/state/command/event/outcome.schema.json`),
   манифест, `capabilities.json`, провайдер, регистрация в кампании и в `RuntimeBridge.CreateCapabilities`.
4. Обязательный тест: `tests/content/**` — новый тип валидируется, невалидный отвергается с
   точным указателем; `content:check` зелёный (FIX-003).
**Files to modify:** `content/schemas/*` (новые файлы), `scripts/content/compile-content.mjs`,
`src-dotnet/Urman.Content/Compilation/ContentCompiler.cs`,
`content/schemas/compiled-content-pack.schema.json`, `game/scripts/CompiledCampaignRepository.cs`,
`content/campaigns/urman.chapter1/campaign.json`, `content/modules/*/module.json`.
**New files/components (NEW FILE, предлагаемые пути):** перечислены выше.
**Existing code to reuse:** образец `urman.oldpc` (модуль + capability + схемы), `document.schema.json`
с блоком `oldPc`.
**Existing code to remove/deprecate:** —
**Dependencies:** FIX-003.
**Scene/inspector setup (Godot):** —
**Runtime behavior:** данные попадают в пак и читаются рантаймом; сейвы не затрагиваются
(новые типы — контент, не состояние).
**Edge cases:** модуль без новой схемы (компилятор должен падать с понятной ошибкой);
kind без реестра (ошибка `Unsupported content kind`); дубль id между модулями (ошибка `DuplicateId`).
**Acceptance criteria:** каждый новый тип проходит компиляцию и валидацию в **обоих** валидаторах
(.NET и JS); `content:check` и `test:content` зелёные; в паке новые реестры присутствуют.
**Manual test:** собрать контент (`eng/compile-game-content.sh`) и убедиться, что в
`game/content/urman.chapter1.compiled.v1.json` появились новые реестры.

---

## SAVE-001 — Правила сохранения новых систем

**Priority:** P1
**Current state:** `SaveGameV3` (`src-dotnet/Urman.Core/Persistence/SaveGameV3.cs`,
`CurrentSchemaVersion = 3`) хранит: runtime-состояние ядра (knowledge/journal/quests/npc/beats/…),
clock/RNG/scheduler, `Capabilities` (снимки capability-сессий), `CurrentZone/SpawnPoint`,
`PlayerTransform`, `Settings` (включая `InputBindings` и `Accessibility`), `PlayTimeSeconds`.
Загрузка отвергает чужую версию (`:114–117`), валидирует поля (в т.ч. `InputBindings` непусты, `:155–170`).
Настройки пользователя — отдельно: `user://settings.json` (`UserSettingsStore`, версия 1).
**Problem:** новые системы (книжка-блокнот ПК, покупки, радио, транспорт, открытые места)
должны сохраняться без новой save-системы и без поломки старых сейвов.
**Required implementation:**
1. **Правило №1:** новое *состояние* хранить в (а) нарративном состоянии через эффекты контента
   (`knowledge`/`journal`/`beats`/`npc`) либо (б) в capability-снимке существующего/нового
   модуля. Новых файлов и новых save-форматов не создавать.
2. **Правило №2 (изменение состояния capability):** не менять `StateSchemaVersion` без
   необходимости; при добавлении ключей — терпимый `Restore` (заполнение дефолтами), как описано
   в PHASE 4.
3. **Правило №3 (если всё же нужен бамп схемы сейва):** поднять `SaveGameV3.CurrentSchemaVersion`
   до 4, читать версию 3 (дефолты для новых полей) — иначе все существующие сейвы станут
   непригодны. Изменение версии сопровождать тестом на загрузку «v3 → v4».
4. **Правило №4 (настройки):** новые пользовательские предпочтения (язык ПК, громкость радио,
   режим приседа) — через `GameSettingsSnapshot`/`UserSettingsStore` с той же терпимостью
   (см. FIX-004).
5. **Что где сохраняется (таблица — обязана быть в отчёте implementer-а):**

| Система | Где хранится | Что именно |
| --- | --- | --- |
| Книжка (категории) | нарративное состояние (уже есть) | journal/knowledge/quests/vocabulary — новый persistence не нужен |
| Блокнот ПК, история браузера, состояние окон | capability `urman.oldpc` (расширение) | notes, history, openWindows, fsCurrentPath |
| Открытые/прочитанные документы | capability `urman.oldpc` (уже есть) | activeDocumentId, savedDocumentIds, activeSection |
| Магазин/долги | capability `urman.selsmag` (NEW) | records[] |
| Радио | capability `urman.radio` (NEW) | on, stationId, segmentCursor |
| Транспорт | capability `urman.vehicles` (NEW) | позиции машин; «в машине» — не сохранять (игрок выходит) |
| Мечеть/safe zone | не сохраняется | определяется позицией игрока при загрузке |
| Надгробья/зират | не сохраняется | контент + существующие knowledge-флаги |
| Уровень присутствия Шурале | нарративное состояние (beats/knowledge) + кулдауны в capability при необходимости | — |
| Язык ПК, громкость радио | `GameSettingsSnapshot` | locale, radioVolume |

6. Тесты: `SaveGameV3` round-trip для каждой новой capability
   (`game/tests/PersistenceSmokeTest.cs` расширить или NEW `game/tests/NewSystemsSaveSmokeTest.cs`),
   плюс тест «старый сейв v3 + новые настройки не падают».

**Files to modify:** по мере задач; обязательный минимум — `SaveGameV3.cs` (только при бампе),
`game/tests/PersistenceSmokeTest.cs`.
**New files/components (NEW FILE, предлагаемые пути):** `docs/production/save_contract.md`
(таблица выше как правило проекта).
**Existing code to reuse:** `AtomicSaveGameStore`, `SaveGameV3`, `UserSettingsStore`, capability-снимки.
**Existing code to remove/deprecate:** —
**Dependencies:** все задачи, вводящие состояние.
**Scene/inspector setup (Godot):** —
**Runtime behavior:** сохранение/загрузка сохраняют весь новый прогресс; старые сейвы
не ломаются.
**Edge cases:** сейв, сделанный до появления новой системы (загрузка проходит, новые поля — дефолты);
сейв с новыми полями, загруженный старой сборкой (не поддерживается — версия отвергается, это ок);
повреждённый сейв (существующий atomic-store recovery).
**Acceptance criteria:** для каждой новой системы есть round-trip тест;
`SaveGameV3` без бампа версии (при выборе capability-пути); загрузка «сейв v3 + новые ключи»
проходит.
**Manual test:** сохранить игру до и после каждого нового действия (покупка, радио, поездка,
блокнот) и загрузиться — всё на месте.

---

## Data-driven принципы (обязательны для всех новых систем)

1. Любой новый текст — в `content/modules/**` с `{default, translations: {ru, tt}}`; в C# —
   только получение через `ResolveText`/`TextResolver`.
2. Любые списки (товары, сегменты радио, страницы сайтов, надгробья, объявления, документы) —
   JSON в модулях с явными схемами; никаких массивов-литералов в C# длиннее 3 элементов
   (за исключением технических списков вроде компонентов китов).
3. Идентификаторы — `urnam.<module>:<kind>/<slug>` (существующее правило, `KindFromId`),
   стабильные и уникальные; переименование — только с миграцией ссылок.
4. Условия/эффекты — только через существующие опкоды (`knowledge.*`, `npc.state`, `beat.state`,
   `quest.*`, `journal.record`, `document.open`, `audio.request`, `scene.request`,
   `vocabulary.set-status`); новые опкоды добавлять в `ContentRuleEngine` (`src-dotnet/Urman.Core/Narrative/ContentRuleEngine.cs`)
   и в схему `condition-effect.schema.json` **только если без них нельзя**.
5. Контент-автор пишет тексты; implementer отвечает за систему и по одному примеру каждого типа.

## Save/load (сводно)

См. `SAVE-001`. Кратко: новых save-систем не создавать; состояние — либо в нарративном
состоянии, либо в capability-снимках; настройки — в `UserSettingsStore`; версии не бампать
без миграции; на каждую систему — round-trip тест.

---

# ФИНАЛЬНЫЙ IMPLEMENTATION BACKLOG

Порядок — рекомендуемая последовательность выполнения.

| Order | ID | Feature | Priority | Depends on | Existing/New | Complexity | Files affected | Acceptance summary |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | FIX-004 | Терпимые биндинги + миграция настроек | P0 | — | Existing | S | `InputBindingService.cs`, тест | старые settings/saves не ломают старт |
| 2 | FIX-001 | Фото Марата (M1) достижимо | P0 | FIX-003 | Existing | S | `ExteriorDiscoveries.cs`, контент | `E` на фото открывает документ и даёт записи |
| 3 | FIX-003 | Развязка chapter1↔oldpc, зелёный content:check | P0 | — | Existing+New | M | контент, `campaign.json` | 68/68 и 102/102 тестов зелёные |
| 4 | FIX-002 | Отладочные спавны | P1 | — | Existing | S | `Act1WorldLayout.cs`, `MainMenuUi.cs` | все 8 переходов работают |
| 5 | KNOCK-001 | Guard против автостука | P0 | INTERACT-001 | New | S | `UiFoley.cs`, тест, правила | проход без `E` не издаёт звуков |
| 6 | PLAYER-001 | Прыжок | P0 | FIX-004 | Existing | M | `FirstPersonController.cs`, `project.godot` | один прыжок, без двойного |
| 7 | PLAYER-002 | Присед (капсула+камера) | P0 | FIX-004 | Existing | M | `FirstPersonController.cs` | капсула 1.2, проход под низким |
| 8 | PLAYER-004 | Тело/ноги в POV | P0 | PLAYER-002 | New | M | сцена игрока, контроллер | ноги видны вниз, не мешают |
| 9 | PLAYER-005 | Тень игрока | P1 | PLAYER-004 | New | S | сцена игрока, текстура | тень на снегу, блоб в ночи |
| 10 | PLAYER-007 | Камера (присед/прыжок) | P1 | PLAYER-001/002 | Existing | S | `FirstPersonController.cs` | высоты и кик корректны |
| 11 | PLAYER-003 | Скорость и спринт | P1 | FIX-004, PLAYER-002 | Existing | S | контроллер, настройки | 3.4/5.2 м/с, смоуки зелёные |
| 12 | PLAYER-006 | Анимация тела | P2 | PLAYER-004/005 | New | M | контроллер, `FirstPersonBodyRig` | шаг совпадает со звуком |
| 13 | INTERACT-001 | Двери и состояния | P0 | PLAYER-002 | Existing+New | M | `InteractionTarget*.cs`, мир | заперто/открыто/анимация |
| 14 | INTERACT-002 | Типы целей, удержание, приоритет | P1 | INTERACT-001 | Existing | S | `InteractionTarget.cs`, `FirstPersonController.cs` | старые цели без изменений |
| 15 | KNOCK-002 | Ручной стук | P1 | INTERACT-001, KNOCK-001 | New | S | контент, `InteractionTarget.Knock.cs` | стук только по `E`, кулдаун |
| 16 | START-001 | Автобусная остановка и старт | P1 | FIX-002, PLAYER-001/002 | New | M | мир, layout, контент | старт на остановке, автобус уезжает |
| 17 | NOTE-001 | Данные книжки (5 категорий) | P1 | DATA-001 | Existing+New | M | репозиторий, мост | 5 проекций наполняются |
| 18 | NOTE-002 | UI книжки (разделы) | P1 | NOTE-001 | Existing | M | `JournalUi.cs`, сцена | все разделы листаются |
| 19 | NOTE-003 | Предмет-книжка в доме | P3 | NOTE-002 | New | S | дом, контент | `E` на столе открывает книжку |
| 20 | DATA-001 | Схемы новых типов | P1 | FIX-003 | Existing+New | M | схемы, оба компилятора | оба валидатора зелёные |
| 21 | PC-001 | ФС и типизированный контент ПК | P1 | DATA-001 | Existing+New | L | oldpc-модуль, провайдер | 26 документов в дереве |
| 22 | PC-002 | Desktop shell | P1 | PC-001, PC-003 | New | L | новые сцены/скрипты | рабочий стол, часы, «Пуск» |
| 23 | PC-003 | Window manager | P1 | PC-002 | New | L | новые сцены/скрипты | окна, фокус, сохранение окон |
| 24 | PC-004 | Приложения (Блокнот/Документ/Проводник/Корзина) | P1 | PC-001..003, NOTE-001 | New | L | `game/scripts/apps/*` | 5 приложений работают |
| 25 | PC-005 | Браузер, ТатВики, соцсеть | P1 | PC-004, DATA-001 | New | L | контент, новое приложение | статьи и переходы, улики живы |
| 26 | PC-006 | ru/tt в интерфейсе ПК | P2 | PC-002..005 | New | M | `LocaleService`, контент | ≥10 подписей из контента |
| 27 | PC-007 | Контракты oldpc сохранены | P1 | PC-001..005 | Existing | S | тесты | улики/журнал/сравнения живы |
| 28 | MOSQUE-001 | Интерьер мечети | P1 | INTERACT-001 | New | L | мир, зона, контент | ≥6 интеракций, уважительно |
| 29 | MOSQUE-002 | Имам в мечети | P1 | MOSQUE-001 | Existing | S | NPC-стейджинг, контент | диалог работает внутри |
| 30 | MOSQUE-003 | Safe zone | P1 | MOSQUE-001 | New | M | `SafeZoneVolume`, директор | сверхъестественное подавляется |
| 31 | FAP-001 | Добивка ФАП | P2 | NOTE-001 | Existing | S | контент, зона | ≥3 интеракции, без RPG |
| 32 | SHOP-001 | Магазин (локация) | P1 | INTERACT-001, NOTE-001 | New | L | мир, зона, контент | магазин посещаем, NPC, доска |
| 33 | SHOP-002 | Долговая тетрадь | P1 | SHOP-001, DATA-001, SAVE-001 | New | M | capability, UI, контент | покупки пишутся «на бабая» |
| 34 | SCHOOL-001 | Школа (частично) | P1 | INTERACT-001, PLAYER-002 | New | L | мир, зона, контент | ≥4 осмотра, запертые двери |
| 35 | COUNCIL-001 | Сельсовет/ДК | P1 | INTERACT-001, CORE-001 | New | L | мир, зона, контент | план/архив/афиши |
| 36 | ZIRAT-001 | Зират: имена и связи | P1 | DATA-001, NOTE-001, CORE-002 | New | L | данные, рендер, контент | ≥12 надгробий, связи |
| 37 | BANYA-001 | Баня: атмосфера | P1 | INTERACT-001, PLAYER-002 | New | M | мир, звук, частицы | пар/треск, ≤3 события |
| 38 | VEH-001 | База транспорта | P1 | INTERACT-002, SAVE-001 | New | L | `VehicleBase.cs`, capability | вход/выход/парковка |
| 39 | VEH-002 | Нива | P1 | VEH-001, RADIO-001 | New | M | сцена машины | езда по деревне + радио |
| 40 | VEH-003 | Мотоцикл | P2 | VEH-001 | New | S | сцена | 18 м/с, наклон |
| 41 | VEH-004 | Лошадь и телега | P1 | VEH-001, FOREST-001 | New | M | сцена, правила | поездка в лес отличается |
| 42 | RADIO-001 | Ядро радио | P1 | DATA-001, SAVE-001 | New | L | шина, capability, плеер | станции/сегменты/сохранение |
| 43 | RADIO-002 | Контент станции | P1 | RADIO-001 | New | M | контент, аудио-плейсхолдеры | ≥20 сегментов, tt+ru |
| 44 | FOREST-001 | Правила леса и телеги | P1 | VEH-004 | New | S | layout, контент, документ | три способа въезда |
| 45 | SHURALE-001 | Присутствие Шурале | P2 | MOSQUE-003 | New | M | директор, контент | уровни 0–2, без монстра |
| 46 | CORE-001 | Просмотрщик изображений | P1 | DATA-001, NOTE-001 | Existing+New | M | `DocumentUi`, резолвер | фото/планы показываются |
| 47 | CORE-002 | Lore-индекс | P1 | NOTE-001, ZIRAT-001 | New | M | `LoreIndex.cs` | ≥10 перекрёстных упоминаний |
| 48 | SAVE-001 | Контракт сохранения | P1 | все с состоянием | Existing+New | M | тесты, документ | round-trip по каждой системе |

Обозначения сложности: S ≈ 0.5–1 день, M ≈ 1–3 дня, L ≈ 3–7 дней работы одного разработчика
с доступом к Godot-редактору и прогоном смоуков.

---

# SPRINT 1

Задачи, которые рекомендуется сделать первыми: они закрывают реальные дефекты, дают ощутимый
результат для игрока и не требуют новой большой подсистемы. Порядок обязателен:
**FIX-004 → FIX-001 → FIX-003 → (KNOCK-001 + INTERACT-001) → PLAYER-001 → PLAYER-002 →
PLAYER-007 → PLAYER-004 → PLAYER-005 → START-001 (шаг «спавн+интро») → NOTE-001 (основа)**.

### S1-1. FIX-004 — терпимые биндинги (до всего остального)
1. `game/scripts/InputBindingService.cs`: в `Apply()` заменить `throw new InvalidDataException(...)`
   на `if (!byAction.TryGetValue(action, out var binding)) { binding = DefaultFor(action); warn(...); }`,
   где `DefaultFor` берёт значение из `_defaults` (через `InitializeDefaults()`/`Capture()`).
2. Добавить в `game/tests/Act1UserSettingsSmokeTest.cs` кейс «сохранённые биндинги без действия».
3. Прогнать `eng/run-smoke-guarded.sh act1_user_settings_smoke_test` (или соответствующий смоук)
   и записать результат в отчёт.

### S1-2. FIX-001 — фото Марата
1. В `content/modules/urman-chapter1/definitions.json` добавить интеракцию
   `urman.chapter1:interaction/arrival-photo` (targetDocumentId = `urman.chapter1:document/arrival-photo-evidence`).
2. В `game/scripts/Act1ConnectedWorld.ExteriorDiscoveries.cs:77–83` заменить `""` на новый id.
3. Пересобрать контент `eng/compile-game-content.sh`.
4. Расширить `game/tests/Act1FirstPersonWalkthroughSmokeTest.cs`: после старта пройти к скамейке
   (координаты скамейки — `BuildArrivalBenchDiscovery`: `(4.9, ·, 6.1)`), навести луч, `E`,
   проверить `DocumentUi.OpenDocumentId` и `knowledge/memory_marat_childhood_photo == confirmed`.

### S1-3. FIX-003 — зелёный контент-гейт
1. Выбрать вариант A (новый модуль `urman-investigation`) и перенести 9 `journalAction`
   из `scene/investigation-journal`.
2. Прогнать `npm run content:check`, `npm run test:content`, `npm run test:runtime` — добиться 0 fail.
3. Прогнать `eng/compile-game-content.sh` и смоук `ChapterOneFlowSmokeTest`.
4. Записать решение в `docs/urman_knowledge_base/decision_log.md`.

### S1-4. KNOCK-001 — guard
1. `game/scripts/UiFoley.cs`: добавить счётчик и историю воспроизведённых сэмплов + `ResetForTests()`.
2. Создать `game/tests/InteractionProximityGuardSmokeTest.cs` (проход к двери без `E`, скан `Area3D`).
3. Создать `docs/production/interaction_rules.md` с правилом и исключением для safe zone.

### S1-5. INTERACT-001 — двери и состояния
1. Новый partial `game/scripts/InteractionTarget.Door.cs`: `DoorMode`, `LeafPath`, `LockedPrompt`,
   `IsDoor`/`LockedText`.
2. В `FirstPersonController.UpdateInteraction` показывать `LockedText`, когда цель недоступна.
3. Дом: `HouseDoor`/`HouseExit` — `DoorMode = Transition`; мечеть: новый `InteractionTarget` у
   `MosqueEntranceDoor` (`Act1ConnectedWorld.cs:~8118`) с `Animated` + `LockedPrompt`.
4. Смоук: `Act1AudioTransitionSmokeTest` + новый кейс «запертая дверь не издаёт звук».

### S1-6. PLAYER-001 — прыжок
1. `project.godot`: добавить `jump` (Space + gamepad A), убрать `A` из `interact`.
2. `InputBindingService.RemappableActions` += `jump`; `SettingsUi.ActionLabels` += «Прыжок».
3. `FirstPersonController`: `JumpVelocity`, `CoyoteSeconds`, `JumpBufferSeconds`, `AirControlFactor`
   + логика в `_PhysicsProcess` (см. PLAYER-001 п.4).
4. Обновить интро-текст (`Act1DemoRoot.UpdateIntroControls`).
5. Смоук: `game/tests/Act1JumpSmokeTest.cs` — прыжок на земле даёт `GlobalPosition.Y > start + 0.5`
   в апексе; двойной прыжок невозможен (второй `Input.IsActionJustPressed` в воздухе не меняет `Velocity.Y`).

### S1-7. PLAYER-002 — присед
1. `FirstPersonController`: параметры высот/скорости/времени + `CrouchMode`.
2. Дублировать `CapsuleShape3D` в `_Ready` перед изменением (обязательно, см. задачу).
3. Логика `_crouchWanted` + `CanStandUp()` через `IntersectShape`.
4. Смоук: `game/tests/Act1CrouchSmokeTest.cs` — высота капсулы/головы, запрет вставания под проёмом.

### S1-8. PLAYER-007 + PLAYER-004 + PLAYER-005 — камера, тело, тень
1. Ввести `_targetHeadHeight` и применять в head bob; кик приземления (см. PLAYER-007).
2. `first_person_player.tscn`: `BodyRig` с ногами-плейсхолдерами; видимость по `_pitch < -25°`.
3. `LegsShadowProxy` с `cast_shadow = ShadowsOnly`; блоб-текстура + `PlayerShadowBlob`.
4. Кадровый смоук: добавить `capture`-точку «взгляд вниз» (образец — `eng/capture-act1-visual-review.sh`).

### S1-9. START-001 (минимальный шаг: спавн + интро)
1. Перенести спавн на площадку остановки: новый `arrival_busstop` в `Act1WorldLayout`,
   обновить `Act1DemoRoot.DefaultPerformanceSample` и `Main.SpawnTransform`; старый `arrival` — алиас.
2. Павильон и знак — примитивы (`AddVisualBox`), коллизия стенда.
3. Интро-текст: добавить `Space`/`C` в список управления.
4. Смоук: `Act1SpawnMatrixSmokeTest` (расширить) + `Act1DemoLaunchSmokeTest` (позиция старта).
Полный автобус/расписание/тень — по задаче START-001, следующим спринтом.

### S1-10. NOTE-001 — основа книжки (только данные)
1. Аксессор `Characters` в `CompiledCampaignRepository` + тип `CompiledCharacterContent`.
2. Проекции `NotebookNotes/People/Observations` в `RuntimeBridge`.
3. Тест `game/tests/NotebookDataSmokeTest.cs`: на финальном состоянии Акта I каждая проекция непуста.
UI-вкладки — задача NOTE-002 (следующий спринт).

**Definition of Done спринта:** все пункты S1 приняты по своим acceptance criteria; сборка
запускается; `Act1FirstPersonWalkthroughSmokeTest`, `ChapterOneFlowSmokeTest`,
`Act1AudioTransitionSmokeTest`, `Act1SpawnMatrixSmokeTest` и `JournalFlowSmokeTest` зелёные;
в отчёте — список изменённых файлов и результаты прогонов.

---

# INSTRUCTIONS TO IMPLEMENTING MODEL

Ты — implementer. Ниже прямые правила работы; следуй им буквально.

1. **Сначала прочитай** (в этом порядке): `AGENTS.md`, `docs/urman_knowledge_base/README.md`,
   `docs/urman_knowledge_base/technical_architecture.md`, этот документ,
   `docs/production/urman_ttz_compliance_audit_2026-09-15.md`,
   `docs/production/urman_ttz_audit_verification_2026-09-15.md`.
   Далее — по задаче: `game/scripts/FirstPersonController.cs`, `game/scripts/InteractionTarget.cs`,
   `game/scripts/RuntimeBridge.cs`, `game/scripts/Act1ConnectedWorld.cs`,
   `game/scripts/StyleBenchmarkZone.cs`, `content/modules/urman-chapter1/definitions.json`.
2. **Работай по задачам в порядке из раздела «ФИНАЛЬНЫЙ IMPLEMENTATION BACKLOG»** (и начинай
   со SPRINT 1). Не начинай новую задачу, пока предыдущая не принята по её acceptance criteria.
3. **Не меняй несвязанные системы.** Любая правка вне файлов задачи — обоснуй в отчёте.
   Запрещено: переписывать `FirstPersonController` целиком, менять формат контента без DATA-001,
   менять `SaveGameV3` без SAVE-001, добавлять новый save-формат, трогать акты 2–5 и legacy `src/**`.
4. **Сохраняй обратную совместимость:** стабильные content-id, совместимость сейвов (см. SAVE-001),
   совместимость настроек (FIX-004), зелёные существующие смоуки.
5. **После каждой задачи:** собери проект, прогони релевантные смоуки/тесты
   (`eng/compile-game-content.sh`, `npm run content:check`, Godot-смоуки из `game/tests/`),
   выполни `graphify update .`, перечисли изменённые файлы, приведи вывод прогонов.
6. **Используй существующие паттерны:** data-driven условия/эффекты, capability-модули по образцу
   `urman.oldpc`, `MakeInteractionBox`/`DiscoveryTarget`, `AddVisualBox`-геометрию,
   `PainterlyMaterialLibrary` для материалов, `UiFoley` для звука UI.
7. **Не добавляй новых зависимостей** (пакеты NuGet/npm, сторонние плагины Godot) без письменного
   обоснования в отчёте. Всё нужное уже есть в Godot 4.7 и .NET 10.
8. **Арт и звук**: работай с плейсхолдерами (`PLACEHOLDER ACCEPTABLE`); не блокируй задачи из-за
   отсутствия финальных ассетов; плейсхолдеры помечай метаданными/комментариями, чтобы их было
   легко заменить.
9. **Если код отличается от этого документа — верь коду** и отметь расхождение в отчёте
   отдельным пунктом «РАСХОЖДЕНИЕ С ТЗ»: что в документе, что в коде, что ты сделал.
10. **Правила игры, которые нельзя нарушать:** никаких скримеров и монстров; никакой экономики,
   крафта, голода, боевой системы; Шурале — присутствие, а не погоня; мечеть — уважительно и
    безопасно; простое приближение к двери никогда не издаёт звук.
11. **Отчёт по каждой задаче** — краткий: ID, статус, изменённые файлы, что проверено, чем
    проверено (команда/смоук), что осталось, расхождения. Без пересказа документа.
12. Если задача требует решения, которого нет в документе (например, конкретное имя нового NPC
    или название соседнего села) — выбери вариант, пометь его `PROPOSAL` в отчёте и вынеси в
    `docs/urman_knowledge_base/open_questions.md`; не меняй канон молча.
