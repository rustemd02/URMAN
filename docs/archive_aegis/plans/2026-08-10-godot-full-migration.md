# План полного переноса «УРМАНА» на Godot

Date: 2026-08-10  
Status: Accepted for execution  
ArchitectureReviewRequired: yes

## Intent lock

Перенести текущую portable-архитектуру и весь планируемый контент «УРМАНА» в полноценную first-person 3D-игру на Godot 4.7.1 .NET и C#. Целевая игра длится 6–8 часов, использует стиль Painterly Low-Poly 3D, не содержит боя и завершается одной трагической концовкой: Айдар осознанно разрушает несправедливый пакт, после чего Кырлай теряет его защиту.

## Неподвижные границы

- macOS и Windows — обязательные desktop-платформы; browser и mobile не входят в релиз.
- Portable JSON/Markdown content остаётся источником сюжетной истины.
- Чистое C#-ядро не зависит от Godot; только runtime kernel меняет сюжетное состояние.
- Godot владеет представлением, first-person movement, world scenes, input, audio и UI, но не progression truth.
- Старый TypeScript/Vite/Three.js runtime остаётся read-only parity oracle до финального cutover. Runtime bridge и постоянный fallback запрещены.
- SaveGameV3 не импортирует GameSnapshotV2 и не читает browser localStorage. Существующие browser-сохранения не удаляются.
- Компактные загружаемые зоны заменяют route screens; открытый мир, бой и постоянный systemic stealth не входят в scope.

## Зафиксированные продуктовые решения

- Стиль: Painterly Low-Poly 3D — производственная геометрия варианта 2 и живописные материалы, свет и туман варианта 3.
- Камера: первое лицо, высота около 1,7 м, FOV 75° с настройкой 65–90°, минимальный head bob.
- Ввод: клавиатура/мышь и геймпад, переназначаемые actions и динамические подсказки.
- Релизный язык: русский; татарский остаётся проверяемой игровой механикой, localization contracts не зашиваются в UI.
- Угроза: без боя; редкие постановочные эпизоды погони, укрытия или запрета ответа.
- Производительность: 1080p/60 FPS на среднем desktop; low preset — не ниже 30 FPS на Apple M1 и сопоставимой интегрированной графике.

## Целевая архитектура

- `Urman.Core`: commands, events, state, kernel, quests, clues, vocabulary, capabilities, deterministic clock/RNG.
- `Urman.Content`: portable models, JSON Schema, Markdown, compiler, reference closure и narrative invariants.
- `Urman.Godot`: composition root, world director, first-person controller, interactions, UI, audio и save coordinator.
- `Urman.ContentCli`: `validate`, `compile`, `inspect`, `simulate`, `report`.
- `SaveGameV3`: campaign lock/fingerprint, runtime snapshot, capability snapshots, world checkpoint, player transform, settings и playtime; atomic write under `user://`.
- Логические world contracts: `WorldLocationId`, `SpawnPointId`, `InteractionId`, `WorldStateVariantId`; Godot scene paths не попадают в generic content/runtime owners.

## Исполнение

1. Зафиксировать веб-baseline, golden fixtures и migration work record.
2. Принять style/engine/full-story decisions в knowledge base и заблокировать акты 2–5 до дорогого art production.
3. Создать .NET solution и переписать Content Lab/compiler на C# с parity текущей Chapter 1.
4. Перенести kernel, registries, resolvers, quests, capabilities, old PC и SaveGameV3.
5. Собрать Godot vertical slice Chapter 1: дом, двор, улица и кромка Кара-Урмана; принять три in-engine style frames.
6. Производить акты 2–5 только после их beat sheet/clue graph/location/NPC/language/threat lock.
7. Проверить полное прохождение и desktop exports, затем удалить старый web-runtime и production Node/Vite/Three.js пути.

## Сюжетная дуга полной игры

- Акт 1: возвращение, Марат и финальное «Не отвечай».
- Акт 2: двойная система деревни, Алсу, Тимур и практическое доказательство пакта.
- Акт 3: правда о Марате, Баранов 1967 года, Су Анасы и советский слой.
- Акт 4: Тукай, 1913 и 1552 годы, происхождение пакта и наследственные роли.
- Акт 5: осознанное разрушение пакта, потеря защиты и трагический эпилог.

Локальные решения меняют отношения, доступные сведения и постановку сцен, но не создают альтернативных концовок.

## Verification boundary

- Golden parity для существующей Chapter 1: diagnostics, IDs, campaign fingerprint, commands/events, snapshots и reveal timing.
- Unit/scenario/save-load тесты чистого C#-ядра; Godot headless scene/interaction/resource checks.
- Полный проход от приезда до эпилога; old PC, journal, dialogue keys и vocabulary re-read работают через одно runtime state.
- Keyboard/mouse и gamepad parity; macOS/Windows export smoke; performance budgets на трёх style scenes.
- Финальный absence gate запрещает production TypeScript, Vite, Three.js, browser persistence и route fallback.

## TDD Route

- Mode: off
- Decision: skipped
- Strict authority: not applicable
- Test posture: golden baseline до переноса, focused regression после каждого пакета, полный acceptance gate перед retirement

