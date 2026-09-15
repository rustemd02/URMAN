# ASTRA 6 — IMPLEMENTATION HANDOFF

Ты работаешь с игрой URMAN.

## Главная команда

**НЕ ГЕНЕРИРУЙ НОВУЮ КАРТУ, ПОКА НЕ ПРОВЕРИЛ, ЕСТЬ ЛИ В ПРОЕКТЕ УЖЕ ГОТОВАЯ.**

Если карта есть:
1. найди scene/world/level;
2. извлеки road splines/meshes, buildings, entrances, forest/water;
3. адаптируй их в schema этого архива;
4. запусти аудит;
5. сохрани существующий layout максимально;
6. подготовь repair proposals;
7. автоматически применяй только чисто data-level исправления;
8. topology/geometry меняй лишь когда это необходимо для связности и минимально.

Если карты нет:
используй `docs/PROCEDURAL_FALLBACK.md`.

## Что нужно реализовать

### World registry
Единый runtime/database registry для:
- roads
- buildings
- parcels
- addresses
- game cadastral IDs
- entrances/access
- POI

### Stable IDs
Квесты/сейвы никогда не ссылаются на отображаемый адрес.

### Address engine
- street origin;
- chainage;
- odd/even sides;
- preserve existing;
- suffix infill;
- corner access rule;
- address history.

### Parcel engine
Если parcels уже существуют — импортировать.
Если нет — создать упрощённые игровые parcels с обязательным frontage/access.

### Game cadastral IDs
Формат:
`URM-Q##-P####`

Это fictional technical ID.
Не выдавать его за ЕГРН.

### Navigation
Address -> entrance/gate -> graph node -> route.

### 2D map
Строить из того же registry.

### Easter egg
`Усал ур. / ул. Злая`
- forest edge;
- не главная транзитная улица;
- №15 и №52 рядом;
- при наличии подходящего existing spur использовать его;
- если нет — proposal на маленькое ответвление.

## Визуал табличек

В ЭТОМ АРХИВЕ НЕТ ART DIRECTION.

Ты должен:
1. сначала изучить текущую игру;
2. определить уровень реализма материалов/геометрии;
3. найти реальные референсы Татарстана нужного периода;
4. самостоятельно сгенерировать/собрать финальный asset;
5. соблюдать только data contract:
   - две строки street label;
   - house number;
   - bilingual fields из registry;
   - mount point;
   - wear parameters optional.

Не наследуй внешний вид старых прототипов, если они есть в истории проекта.

## Acceptance tests

- existing map preserved unless repair justified;
- 100% addressable residential buildings have road/walk access;
- no duplicate current display addresses;
- stable IDs independent of address strings;
- 2D map and navigation use same graph;
- every parcel has stable ID;
- Usal 15 and 52 exist and are spatial neighbors;
- re-running data build is deterministic;
- committed IDs do not change when geometry is slightly edited;
- save references survive street rename.
