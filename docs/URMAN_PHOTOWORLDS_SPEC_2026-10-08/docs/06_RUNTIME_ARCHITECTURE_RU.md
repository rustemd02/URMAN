# 06. Архитектура и интеграция с текущим кодом

## 1. Что прочитано и что ещё проверить агенту

На срезе `9869808…` адресно прочитаны AGENTS, canon.md, game/project.godot, RuntimeBridge.Notebook.cs, QuestRuntimeCoordinator.cs. Прежний пакет содержит более широкий исторический аудит `69012da…`. Нельзя превращать исторические названия файлов в утверждение, что прочитана каждая их актуальная строка.

Наблюдения текущего кода:
- `RuntimeBridge.Notebook.cs` выводит карту/адреса/людей из существующего kernel state и world props. Исследование карты ограничено village zone IDs. Фотомиры не должны загрязнять эту карту своими локальными координатами.
- `QuestRuntimeCoordinator` использует GameCommand, QuestLifecycleReducer и согласование quest objectives; новый прогресс должен подключаться к этому механизму, не обходить его.
- `project.godot` запускает Act1DemoRoot через act1_demo.tscn. Требуется сознательная интеграция нового маршрута в normal entry.
- Защищённые запуски уже разрешены актуальным AGENTS; старый freeze в прежнем ZIP исторический.

## 2. Карта интеграции

| Система/файл | Действие |
|---|---|
| `game/scripts/RuntimeBridge.cs` и partials | Добавить тонкий адаптер фото-command/state, не ещё один самостоятельный save manager |
| `game/scripts/RuntimeBridge.Notebook.cs` | Сохранить map/addresses/people/personal notes; изолировать world namespace |
| `game/scripts/JournalUi.cs` и partials | Единые данные для семейной книги, inspection, карты и словаря |
| `game/scripts/QuestRuntimeCoordinator.cs` | Подключить quests/objectives и идемпотентные последствия |
| `game/scripts/CompiledCampaignRepository.cs` | Загрузить определения фото/миров через существующий компилируемый content pipeline |
| `game/scripts/Act1DemoRoot*.cs` | Новый пролог/переход/обычное начало, убрать дублирующий старый тизер |
| `game/scripts/Act1ConnectedWorld*.cs` | Семантические anchors, найденные фотографии, последствия и доступные маршруты |
| `game/scripts/FirstPersonController*.cs` | Modal locks, перенос/возврат, взаимодействие без новой отдельной физики игрока |
| `game/scripts/AtmosphereProfiles.cs` | Новые scope-aware профили и восстановление старых |
| `game/scripts/AmbientAudioDirector.cs` | Единственный audio ownership при переходе; snapshots миров |
| `game/scripts/GraphicsQuality.cs` | Профили отражения/foliage без потери gameplay cues |
| `game/tests/` и `eng/` | Изолированные smoke, route captures и сериализованные station runs |

Если файл переименован в текущем checkout, найти действующий consumer и обновить map. Не создавать заглушку со старым именем ради совпадения документа.

## 3. Новые модули — проектные имена

`PhotoWorldCatalog` — неизменяемые определения WorldId/PhotoId/anchors.
`PhotoBookProjection` — вычисляемое представление книги из kernel state.
`PhotoWorldCommandHandler` — валидация acquire/mount/enter/observe/complete/return.
`PhotoWorldCoordinator` — runtime orchestration загрузки, control locks, lifecycle.
`PhotoWorldSceneRoot` — content root с safe nodes, observations, services и профилем.
`PhotoAnchorResolver` — связь с существующей деревней по стабильным смысловым ID.
`PhotoReturnTicket` — сериализуемые данные возврата.
`PhotoWorldTransitionPresentation` — анимация/звук, не владелец прогресса.
`EvidenceProjection` — отображение происхождения знания.
`PhotoWorldDebugHarness` — только development/test.

Предложенные пути: `game/scripts/photoworlds/`, `game/content/photoworlds/`, `game/scenes/photoworlds/`, `game/tests/photoworlds/`. Source/authoring content разместить там, где реально живут некомпилированные материалы текущего pipeline; не редактировать только генерируемый output.

## 4. Источник истины

Gameplay state живёт в существующем kernel. Scene nodes — его проекция. UI не владеет acquired/mounted/completed. Анимация окончания не вызывает скрытое продвижение главы без command. После переоткрытия меню/перезагрузки сцены состояние одинаково.

Если существующий contracts module требует нового protocol/version, добавить его вместе со схемой, компиляцией и тестами. Не складывать обязательный прогресс в глобальные static поля, JSON в UI-кэше или дублирующий AutoLoad.

Идентификаторы `pw1:*` имеют отдельный namespace. Строковые IDs сохраняются между локализациями. Индексы массивов, имена отображения и filesystem enumeration order не являются ключами сохранения.

## 5. Команды

| Команда | Предусловия | Эффект |
|---|---|---|
| `pw1.photo.acquire` | Источник доступен, разрешение действительно, носитель ещё не получен | acquired + source provenance |
| `pw1.photo.mount` | acquired; правильный PageId | mounted |
| `pw1.context.observe` | Действительное наблюдение/диалог в нужном месте | соответствующий context fact |
| `pw1.visit.prepare` | Условия входа, нет другого transition | ReturnTicket + prepared visit |
| `pw1.visit.commit` | Сцена подготовлена, безопасная опора проверена | ActiveWorld/visit ID |
| `pw1.world.observe` | Игрок реально достиг observation trigger | knowledge с происхождением |
| `pw1.world.action` | Валидный target/state/дистанция | изменённый проход/prop |
| `pw1.chapter.complete` | Все смысловые условия главы | completed once; очередь наружных последствий |
| `pw1.visit.return` | Есть активный visit/ticket | очередь безопасного возврата |
| `pw1.outside.verify` | Независимое внешнее подтверждение | corroborated fact, не автоматический unlock всего |
| `pw1.visit.abort` | Подготовка не committed | возврат к исходному snapshot без наград |

Каждая команда: occurrence ID, campaign version, visit ID при необходимости, валидируемый payload. Ошибка даёт typed rejection; не молчаливый false с зависшим интерфейсом.

`chapter.complete` не должен зависеть от чисто визуальной анимации. Повторный command не даёт второй предмет/реплику. Финальная внешняя сцена отмечается отдельным event, а не выводится из последней открытой страницы UI.

## 6. Lifecycle перехода

`VillageActive → Inspecting → Preparing → Loading → DestinationReady → PhotoActive → Returning → VillageReady → VillageActive`.

Переход имеет монотонную revision/generation и cancellation token. Завершение старой async-задачи после New Game или Load отвергается. В `finally` освобождаются все выданные locks. Нельзя иметь два активных ReturnTicket с одинаковой кампанией и разными мирами.

Сценовая загрузка ресурсов может идти фоново [TECH01]. Создание/изменение активного SceneTree выполняется по требованиям Godot thread safety [TECH02]. `load_threaded_get` не вызывать преждевременно на главном потоке так, чтобы имитировать асинхронность блокирующим ожиданием.

Прелоад не означает готовность физики. После instantiate дождаться необходимых physics ticks, проверить коллайдер опоры и camera transform. Только затем возвращать ввод. Незавершённая сцена никогда не показывается как проходимая.

## 7. ReturnTicket

Минимальные поля: `ticket_id`, `campaign_id`, `content_version`, `origin_world_id`, `origin_anchor_id`, `origin_safe_node`, `position`, `rotation_y`, `pitch`, `movement_mode`, `world_clock`, `active_quest_context`, `placed_carryable_ids`, `origin_profile_id`, `origin_audio_snapshot`, `prepared_at_revision`.

Это сериализуемые данные, не ссылки на Node. Не переносить GUID объектов, которых новый WorldRoot не умеет восстановить. Стабильная точка возврата привязана к anchor и fallback safe node; сохранённый transform проверяется коллизионным sweep.

В новой кампании нельзя входить в книгу за рулём. Такой билет поэтому не обязан оживлять движущуюся Ниву. Старые транспортные saves остаются в старом namespace и не мигрируют молча в пешеходный билет.

## 8. Семантические anchors

Обязательные ID:
`PW_HOME_TABLE`, `PW_RIVER_ENTRY`, `PW_FOREST_ENTRY`, `PW_HOME_PHOTO_ENTRY`, `PW_COMMON_ENTRY`, `PW_CELLAR_ENTRY`, `PW_PROLOGUE_ROAD`.

Каждый resolver обязан найти ровно одну точку или выдать диагностическую ошибку сборки контента. Нет fallback «в центре карты» и нет подбора ближайшего совпадения по имени. Таблица actual transforms создаётся агентом в текущем checkout после осмотра маршрута.

Семантический ID решает изменяемую географию, но не освобождает от конкретного размещения: результат задачи — `anchor_bindings.generated.json` с реальными node paths/transform, сцена и скриншот. В этой спецификации не выдумываются координаты непрочитанного текущего layout.

## 9. Наблюдения и пространственные события

ObservationDef: stable ID, world ID, trigger shape, facing/occlusion requirements, текст/звук, prerequisites, produced knowledge, replay behavior. Не выдавать наблюдение через стену или на входе в большой level bounding box.

Ключевой предмет: физический target ≥0,15–0,25 м в зависимости от размера, подсказка при направленном внимании, пользовательские настройки интеракции. E не должен ловиться в одном пикселе. Для крупных объектов допускается forgiving cone/raycast focus hysteresis, но не взаимодействие через закрытую стену.

Удобство нового опыта не должно отменять проверку контакта. Полезные правки InteractionTarget применить общим образом, чтобы старые двери не оставались хуже новой книги.

## 10. Отладка без загрязнения игры

Debug-команды: перейти к готовой фикстуре Wxx, проверить safe node, вывести state diff, переключить профиль, захватить набор F0–F5. Они существуют только в development build/явном harness. Debug progression не сохраняется в авторский игровой профиль.

`data/` этого пакета — проектные спецификации, не готовые game assets. Агент строит адаптер/генератор и показывает diff generated content. Нельзя просто положить JSON рядом с игрой и объявить механизм интегрированным.
