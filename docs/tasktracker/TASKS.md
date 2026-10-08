# TASKS.md — вся единая очередь УРМАН

**Порядок исполнения 09.10:** [протокол коротких этапов и приёмки](06_small_model_execution_2026-09-22.md); приоритеты — `campaign_delivery_plan.dispatch_policy`, ближайший предметный результат — `execution.next_unit` карточки. Сначала приёмка переданного результата, параллельно — эталонный дом и готовая ветка кампании; диагностика не вытесняет обе работы. Порядок не отменяет зависимости и внешние гейты.

**Срез 08.10.2026: 306 карточек = 206 сохранённых ID + 100 PW.** Статусы, зависимости, активные владельцы и доказательства — только в [execution_backlog.json](../urman_knowledge_base/execution_backlog.json). Индекс не является второй очередью.

[Текущий контракт и покрытие](07_full_game_integration_2026-10-08.md) · [PhotoWorlds](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/00_START_HERE_RU.md) · [Visual Reset](../URMAN_VISUAL_RESET_2026-10-07/README_RU.md).

Готовую игру принимает REL-003. PW-100 требует весь PW-объём; прежние игровые функции остаются required, кроме явно исторических/отдельных потоков. Пять миров обязательны производству, W05 необязателен игроку. Импорт PW-011 — документальная работа, не выполненная игра.

## PW0 — Контракт и интеграция

| ID | Результат | Исходник / связь |
|---|---|---|
| `PW-001` | Зафиксировать актуальный checkout и базовый проход | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-002` | Внести авторское разрешение на фундаментальную переработку | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-003` | Перестроить сюжетный граф кампании «За краем снимка» | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-004` | Разобрать судьбу существующих EX и бытовых механик | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-005` | Определить версию состояния новой кампании | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-010` | Свести новый стиль с прежним visual reset | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-011` | Импортировать эту очередь в действующий backlog | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |

## PW1 — Книга, состояние, переходы

| ID | Результат | Исходник / связь |
|---|---|---|
| `PW-006` | Расширить единый kernel командами фотокниги | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-007` | Создать строгий каталог фото, миров и разворотов | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-008` | Разделить впечатление и доказанное знание | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-009` | Создать реестр семантических якорей и safe nodes | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-012` | Ввести session generation и отмену переходов | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-013` | Реализовать приобретение и разрешённое копирование фото | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-014` | Реализовать фронт, оборот и читаемую транскрипцию | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-015` | Реализовать вклейку и её сохранение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-016` | Переработать журнал в семейную книгу без потери функций | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-018` | Согласовать ввод, фокус и modal ownership | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-019` | Реализовать условия первого сознательного входа | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-020` | Реализовать prepare и возвратный билет | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-021` | Загрузить фотомир без блокирующей сцены на main loop | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-022` | Проверить spawn и завершить commit | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-023` | Вернуться из любой нормальной точки мира | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-024` | Сохранение и загрузка внутри фотомира | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-025` | Сделать миграции safe nodes и защиту старых слотов | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-026` | Приостановить внешние часы и бытовые процессы на время визита | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-027` | Разрешить спокойные повторные посещения | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-028` | Сделать необязательные запросные подсказки | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-029` | Сохранять предметные действия и пространственные связи | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-030` | Создать изолированные fixtures и capture harness | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |

## PW2 — Стиль и production pipeline

| ID | Результат | Исходник / связь |
|---|---|---|
| `PW-017` | Произвести физическую книгу и хват от первого лица | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-031` | Зафиксировать colorscript и значения профилей | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-032` | Исправить привязку мазка к движущимся поверхностям | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-033` | Настроить воспроизводимый Blender→GLB цикл | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-034` | Произвести общие материалы и растительные формы | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-035` | Реализовать ограниченный отражённый вид W02 | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-036` | Реализовать конечные обратимые пространственные связи | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-037` | Настроить LOD, batching и culling без разрушения стиля | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-038` | Ограничить память и срок жизни миров | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-039` | Встроить звуковые домены и переходы | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-040` | Выпустить все 13 согласованных изображений | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-041` | Набрать текст книги и короткие реплики | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-042` | Сделать физическую печать из ПК | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |

## PW3 — Пять фотомиров

| ID | Результат | Исходник / связь |
|---|---|---|
| `PW-043` | Перенести пролог внутрь W01 | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-044` | Удержать красоту наружной деревни после разделения | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-045` | W01: произвести и собрать всю проходимую геометрию «Лес за спиной» | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-046` | W01: действия за рамкой и состояние ветки | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-047` | W01: произвести, оживить и поставить Шурале | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-048` | W01: закончить звуковую и световую постановку | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-050` | W02: произвести и собрать всю проходимую геометрию «Мостки» | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-051` | W02: связать отражение с настоящим подходом | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-052` | W02: произвести и поставить Су анасы | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-053` | W02: закончить звуковую и световую постановку | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-055` | W03: произвести и собрать всю проходимую геометрию «Дом на просвет» | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-056` | W03: реализовать два вида одного двора | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-057` | W03: бытовые действия и позиция фотографа | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-058` | W03: закончить звуковую и световую постановку | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-060` | W04: произвести и собрать всю проходимую геометрию «Общий кадр» | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-061` | W04: условия входа и четыре содержательных действия | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-062` | W04: собрать непрерывный горизонт и спокойную кульминацию | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-063` | W04: закончить звуковую и световую постановку | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-065` | W05: произвести и собрать всю проходимую геометрию «Тёплое подполье» | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-066` | W05: произвести и оживить Бичуру | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-067` | W05: порядок вещей и необязательное внешнее открытие | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-068` | W05: закончить звуковую и световую постановку | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-070` | Разместить и проверить семь деревенских якорей | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-071` | Поставить находки и копии в реальные места деревни | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-072` | Сделать школьный контекст лесной фотографии | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-073` | Сделать внешнюю проверку старого речного подхода | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-074` | Сделать подтверждение авторства семейного снимка | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-075` | Сделать поиск файла и собственную подпись на ПК | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-076` | Поставить Алсу перед финальным внешним событием | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-077` | Реализовать единственный летний лист снаружи | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-078` | Закончить домашний эпилог и открыть свободное возвращение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-079` | Переподключить бытовые механики к новой кампании | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |

## PW4 — Деревня, последствия, эпилог

| ID | Результат | Исходник / связь |
|---|---|---|
| `PW-049` | W01: принять обычный маршрут, изображения и повторное посещение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-054` | W02: принять обычный маршрут, изображения и повторное посещение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-059` | W03: принять обычный маршрут, изображения и повторное посещение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-064` | W04: принять обычный маршрут, изображения и повторное посещение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-069` | W05: принять обычный маршрут, изображения и повторное посещение | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-080` | Провести языковой проход и нормализовать имена | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-081` | Проверить весь путь с клавиатурой и контроллером | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-082` | Проверить комфорт и доступность фотомиров | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-083` | Проверить границы сезонов, света и эффектов | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-084` | Провести полноценный слуховой проход | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-085` | Прогнать матрицу двадцати сбоев сохранений и переходов | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-086` | Проверить двадцать циклов посещений и сохранённые последствия | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-087` | Пройти оба порядка первых глав и ранние находки | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-088` | Пройти геометрию, обратные стороны и interaction targets | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-089` | Устранить мерцание и скольжение в движении | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-090` | Измерить производительность на целевом устройстве | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-091` | Проверить книгу при 720p, 1080p, 16:10 и крупном тексте | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-092` | Провести культурный review без выдуманного канона | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-093` | Закрыть происхождение и права каждого runtime-ресурса | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-094` | Пройти кампанию обычным пользовательским маршрутом | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |

## PW5 — QA, handover и полный выпуск

| ID | Результат | Исходник / связь |
|---|---|---|
| `PW-095` | Собрать самостоятельный релизный кандидат | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-096` | Прогнать согласованный регрессионный набор деревни | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-097` | Удалить противоречивые активные инструкции и мёртвые входы | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-098` | Собрать доказательный handover реализации | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-099` | Отделить самостоятельную приёмку от авторской | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |
| `PW-100` | Закрыть всю новую концепцию, а не только первый пилот | [PW-карточки](../URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/11_TASKS_RU.md) |

## Сохранённые игровые задачи и их PW-преемники

| ID | Результат | Исходник / связь |
|---|---|---|
| `ACT1-VILLAGE-LAYOUT` | План живой Кара-Урман под 60–70 жителей и якоря новой кампании | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-044, PW-070, PW-079 |
| `ACT1-TEXTURE` | Сгенерировать и внедрить разнообразную библиотеку текстур | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.00` | Дополненный существующий инвентарь текстур для текущих и планируемых объектов | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.01` | Первая партия: 16 опорных текстур | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02` | Экстерьеры, снег, дерево, камень и растения | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03` | Интерьерные ткани, металл, пластик и утварь | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04` | Бумага, документы, вывески и сюжетные изображения | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05` | Адресные декали и изменяемые состояния | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06` | Персонажи, одежда и первое лицо по реальным UV | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07` | Крупные предметы, ПК и используемый транспорт по настоящим UV | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08` | Экранные подложки, загрузочный арт и условные эффекты | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.09` | Сведение прежнего требования: Уникальные носители Актов II–V | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-040, PW-093 |
| `ACT1-TEXTURE.10` | Сведение прежнего требования: Утверждённые поверхности Шурале и Су Анасы | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-047, PW-052 |
| `ACT1-TEXTURE.11` | Подключение карт к семантическому владельцу материалов | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.12` | Проверить текстуры и игровые кадры каждой интегрированной партии | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.13` | Человеческая художественная и культурная приёмка библиотеки | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PLAYER` | Игрок: прыжок, бег, приседание и видимое тело | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR` | Адреса действующей деревни и самостоятельная навигация | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NOTEBOOK` | Сведение прежнего требования: Личная записная книжка и постепенное знание | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-016, PW-091 |
| `ACT1-OLDPC` | Компьютер 2000-х и внутриигровая сеть | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-CHAT` | Живой чат «Ялкын · Сообщения»: треды Алсу/Ринат/бабай/заметки, варианты ответа, триггеры, unread, bounded-снапшот | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-HINTS` | Контекстные подсказки 3 уровней: тред «Заметки Айдара» + строка архива, hints/*.md, gating без спойлеров, hintsSeen | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-SEARCH` | Глобальный поиск: поле в «Пуске» + «Искать везде»; доки+чат+заметки+история; татарские ключи | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-DOCS` | Содержательные документы, поиск и повторное чтение без обязательного словесного наполнителя | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-TETRIS` | Тетрис-пасхалка: стакан 10×20, 7 фигур, уровни/пауза, только tetrisHigh, изоляция от knowledge | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-SHELL` | Оболочка под 9 окон: снапшот/схема/провайдер (chat, tetris, hintsSeen, tetrisHigh), миграция сейвов, доступность | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT` | Нива, мотоцикл, лошадь и телега | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-RADIO` | Татарское местное радио в транспорте | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ARRIVAL` | Сведение прежнего требования: Лесной крючок, поездка с бабаем и семейное прибытие по принятому сценарию | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-043, PW-079 |
| `ACT1-SHOP` | Магазин и атомарная долговая тетрадь | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC` | Баня, мечеть, школа, сельсовет/ДК и семейный зират | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TZ-ACCEPT` | Совокупная приёмка расширенного ТЗ и адресов | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-09` | Dense open-world exploration, secrets and hidden paths | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-08` | External release confirmation | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-07` | Сведение прежнего требования: Пресеты реально меняют нагрузку | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-090 |
| `FINISH-06` | Product UI menus save resume and credits | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-05` | Winter sound and complete cue lifecycle | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-04` | Home FAP cast and contextual village presentation | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-03` | Сведение прежнего требования: Three investigation cycles and staged final route | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-008, PW-073, PW-074, PW-078 |
| `FINISH-02` | Сведение прежнего требования: First 8–12 minute playable investigation slice | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-043, PW-070, PW-072 |
| `FINISH-01` | Native release export and timed package baseline | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `BASE-001` | Зафиксировать baseline и golden fixtures | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `BASE-002` | Закрепить engine, style, story and retirement boundaries | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `CORE-001` | Перенести content compiler и Content Lab на C# | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `CORE-002` | Перенести runtime kernel, capabilities and SaveGameV3 | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-001` | Собрать first-person Chapter 1 vertical slice | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-002` | Подключить full-game authored zones and presentation adapters | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-003` | Сведение прежнего требования: Принять три in-engine style benchmarks | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-031, PW-044, PW-049, PW-054, PW-059, PW-064, PW-069, PW-099 |
| `GODOT-004` | Закрыть SaveGameV3-backed accessibility settings contract | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-005` | Зафиксировать отдельную играбельную демо-точку входа первого акта | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `NARR-002` | Сведение прежнего требования: Провести татарский и культурно-религиозный review authored Acts 2–5 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-080, PW-092 |
| `ASSET-001` | Произвести and verify modular environment kit | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-002` | Произвести project-original character silhouette kit | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-003` | Создать hero faces, clothing and authored character animation | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-004` | Заменить greybox dressing и принять authored mesh collision | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-005` | Создать и проверить Painterly texture production candidates | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-006` | Проверить host-independent provenance реестра ассетов | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-007` | Разделить semantic material owners для imported wood и cloth | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-008` | Сведение прежнего требования: Подключить authored HouseA к каноническому эпилогу | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-044, PW-055, PW-078 |
| `ASSET-009` | Подключить authored WellA, WoodpileA и GateA к village dressing | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `AUDIO-001` | Произвести authored ambience, voice, captions and non-audio cues | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `QA-001` | Провести внешнюю accessibility and first-time usability review | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `REL-001` | Получить M1 and Windows release-class performance evidence | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `REL-002` | Проверить macOS and Windows desktop exports on hosts | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `REL-003` | Принять готовую игру «За краем снимка» по всей единой очереди | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-094, PW-095, PW-096, PW-099, PW-100 |
| `LEN01` | Ритм и содержательность новой кампании: LEN01 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.1` | Ритм и содержательность новой кампании: LEN01.1 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.2` | Ритм и содержательность новой кампании: LEN01.2 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.3` | Ритм и содержательность новой кампании: LEN01.3 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.4` | Ритм и содержательность новой кампании: LEN01.4 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.5` | Ритм и содержательность новой кампании: LEN01.5 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.6` | Ритм и содержательность новой кампании: LEN01.6 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.7` | Ритм и содержательность новой кампании: LEN01.7 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `LEN01.8` | Ритм и содержательность новой кампании: LEN01.8 | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004, PW-094 |
| `ACT1-EX00` | §13 EX00: Общий контракт новых действий | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX01` | §13 EX01: Поднимать, переносить, ставить | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02` | §13 EX02: Несколько решений одной задачи | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX03` | §13 EX03: Многоцелевые инструменты | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX04` | §13 EX04: Ограниченные сочетания | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX05` | Объёмный снег с опорой и следами на реальной поверхности | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX06` | §13 EX06: Переносной свет | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX07` | §13 EX07: Тепло/холод/материал | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX08` | §13 EX08: Поиск и проверка звуком | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX09` | §13 EX09: Человеческая вертикальность | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX10` | §13 EX10: Небольшие самостоятельные места | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX11` | §13 EX11: Микрозагадки на странность | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX12` | §13 EX12: Найти место по фото/рисунку | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX13` | Связать существующие действия в проверяемый цикл исследования | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX14` | §13 EX14: Устойчивый результат | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX-ACCEPT` | §13.18: единый учёт приёмки EX01–EX14 | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR-FENCE` | Таблички на заборе/калитке для домов без места на фасаде (H032/H034/H045/H046) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR-ACCESS` | Физический доступ к адресам и честные границы новой кампании | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-044, PW-070, PW-079 |
| `ACT1-LANG` | Татарский словарь: автосбор слов и начальная калибровка знания | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.1` | Единый реестр татарских слов и статусы в сейве | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.2` | Автосбор услышанных слов в записную книжку | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.3` | Словарь на ПК бабая | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.4` | Определить уровень татарского в живом разговоре с бабаем в Ниве | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.5` | Ручная смена одного из трёх уровней без сброса прогресса | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.6` | Подтверждение гипотез и видимый прогресс | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.7` | Разметка татарских слов в авторском контенте | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR` | Квесты ведутся адресной системой (без маркеров и автопути) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.1` | Аудит квестов на адресность | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.2` | Убрать маркеры и автопуть с обязательного пути | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.3` | Живой краткий адрес в реплике, запись источника и физическая табличка | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.4` | Приёмочный адресный проход квестов | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI` | Цельный простой UI по выбранной автором системе | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.1` | Два-три сопоставимых варианта простой узнаваемой UI-системы | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.2` | Меню и настройки | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.3` | HUD и диалог | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.4` | Сведение прежнего требования: Простая личная книжка и понятный переход от наблюдения к действию | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-016, PW-091 |
| `ACT1-UI.5` | Старый ПК | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.6` | Татарский словарь | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.7` | Совокупная приёмка UI | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT` | Аудит механик: ни одна не висит без дела | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT.1` | Сведение прежнего требования: Полная инвентаризация существующих интерактивов по группам с retain/rework/retire и ролью в эпизоде | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-004 |
| `ACT1-MECH-AUDIT.2` | Сведение прежнего требования: Полная инвентаризация существующих интерактивов по группам с retain/rework/retire и ролью в эпизоде | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-004 |
| `ACT1-MECH-AUDIT.3` | Понятная бытовая/сюжетная роль сохранённого механизма и общий закон результата | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT.4` | Сняты ложные обещания действия/случайные записки с проверенными зависимостями | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH` | Техническое ревью мира: свет, коллизии, мерцание текстур | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.1` | Аудит технических проблем по зонам | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.2` | Устранён корень провала у D14 X29.8 Z2.6 после проверки неоднозначной координаты | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.3` | Исправления общего производителя дверей/стен/крыш/окон/лесных пересечений | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.4` | Освещение интерьеров | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH` | Настоящие объёмные объекты вместо плоских заглушек | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.1` | Инвентаризация плоских заглушек | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.2` | Дом бабая: объёмные объекты интерьера | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.3` | Действующий медпункт с осмысленным разделением функций и приятным бытовым видом | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.4` | Магазин: объёмные объекты интерьера | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.5` | Рабочие комнаты школы/ДК, достаточные проходы и достоверные предметы | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.6` | Мечеть и баня: совокупный результат профильных ТЗ | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.7` | Улица и дворы: объёмные объекты | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9` | Эталон жилого срубного дома по вступительному изображению | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10` | Распространить срубную архитектуру на жилые дома Акта I | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.11` | Художественная приёмка домов относительно вступительного изображения | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.12` | Приблизить эталонный дом к изначальному вступительному изображению | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-VILLAGE-COMPOSITION` | Ведомость retain/move/rework/retire для всех перечисленных построек, пустырей, камней и двора бабая | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.8` | Приёмка глубины: нет плоских имитаций объёма | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR` | Естественные люди и намеренная тревожность отдельных сцен | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.1` | Подходящая современная модель/rig/анимация с подтверждёнными правами и бюджетом | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.2` | Лица и головы до эталона | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.3` | Материалы кожи, глаз, волос, одежды | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.4` | Естественная походка и сдержанный idle NPC | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.5` | Приёмка персонажей по эталону | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CUTSCENE` | Сведение прежнего требования: Постановка принятого вступления и ночной метели | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-043, PW-085 |
| `ACT1-CUTSCENE.1` | Сведение прежнего требования: Расширить действующую постановку только для тизера, пассажирской поездки и короткой метели | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-043 |
| `ACT1-CUTSCENE.2` | Сведение прежнего требования: Лесной крючок, поездка с бабаем и семейное прибытие по принятому сценарию | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-043, PW-079 |
| `ACT1-CUTSCENE.3` | Сведение прежнего требования: Принять вступление и переход ночи обычным просмотром, пропуском и загрузкой | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-043, PW-085, PW-094 |
| `ACT1-NPC` | Живая деревня: NPC с распорядком и занятиями | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.1` | Data-driven распорядок NPC (routine) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2` | Набор занятий и поз с переходами | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.3` | Новые жители деревни из данных | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.4` | Реакции NPC на события и время суток | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.5` | Приёмка живой деревни | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP` | Звук шагов вне основной дороги: лаг | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP.1` | Диагностика лага шагов вне основной дороги | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP.2` | Фикс лага шагов и проверка проходом | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LEGS` | Правдоподобные ноги при взгляде вниз | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LEGS.1` | Ноги Айдара синхронизированы с фактическим движением | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LEGS.2` | Приёмка походки (ходьба/бег/присед/повороты) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TAMARA-FENCE` | Забор Тамары Геннадьевны (необязательный квест) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `REVIEW28-PLAN` | Полный реестр цитат, шесть тематических ТЗ, решения/зависимости и обновлённая очередь | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `REVIEW28-RESEARCH` | Первичные основания BOTW/TOTK и адресные референсы для зависимых пакетов | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NARRATIVE-SHOW` | Редакторская карта: видимые ремарки и лишние записки заменены действием, звуком, предметом либо содержательной репликой | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TUTORIAL` | Контекстное обучение адресам, сравнению и языку по мере нужды | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PROGRESSION-DOORS` | Установленные и исправленные причины отсутствия подсказки/входа на трёх названных дверях | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NOTEBOOK.DESIGN` | Простая личная книжка и понятный переход от наблюдения к действию | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.APPLY` | Решить содержательную задачу через татарское слово в поиске ПК/Татарвики | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.ADAPT` | Три авторских варианта доли и сложности татарского при одной истории | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-VILLAGE-BLOCKOUT` | Проходимая раскладка принятой деревни и пассажирский въезд | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-044, PW-070, PW-079 |
| `ACT1-PLACEMENT-RULES` | Короткие проверяемые правила посадки/габаритов/стыков у существующего владельца данных | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-HOUSE-SCALE` | Согласованные физические габариты дверей, этажей и домов по источнику mesh и instance | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FENCE-KIT` | Небольшое семейство оград и нормальные углы/ворота/опоры | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-RIVER-BRIDGE` | Физически убедительный мост и берег на маршруте новой кампании | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-044, PW-070, PW-079 |
| `ACT1-BOUNDARY` | Видимая естественная граница деревни/лесного эпизода с аварийной страховкой вне обычного пути | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR.MOUNT` | Раздельные правила для уличного указателя и номерной таблички, крепёж и чтение с подхода | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.FACADES` | Различимая сельская школа, ДК, ФАП и сине-белый магазин | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-INTERIOR.PLAN` | Раздельные основные интерьеры и физически согласованные малые дома | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MOSQUE` | Цельная уважительная татарская мечеть с обоснованным минаретом, входом и обжитым залом | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-BATHHOUSE` | Уютная необязательная баня с приватным окном, парилкой и выразительным духом | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.POST` | Почтовое отделение с читаемым местом/назначением и минимальной бытовой ролью | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ART-DIRECTION` | Сведение прежнего требования: Доказать достижимый стиль одним участком в действующем движке | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-010, PW-031, PW-044 |
| `ACT1-ATMOSPHERE` | Уютные помещения и давящая зимняя непогода снаружи | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MATERIAL.MOTION` | Сведение прежнего требования: Исправленный источник скольжения материала относительно движущейся формы | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-032, PW-089 |
| `ACT1-CHAR.CAT` | Узнаваемая кошка с подходящими пропорциями, фактурой и простым живым поведением | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.FOCUS` | Один активный выбор при keyboard focus и hover другого пункта | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.LOADING` | Понятные реальные состояния загрузки без новой обязательной иллюстрации | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-AUDIO.MAP` | Карта существующих звуковых событий и среды | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-AUDIO.CUES` | Ложный success и неправильный звук стука | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CARRY.CONTACT` | Правдоподобный хват, перенос и опора предмета | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT.ROUTE` | Пассажирская экскурсия и полезный транспорт по доступным дорогам | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT.CABIN` | Собрать салон Нивы для переднего пассажирского ракурса | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PERF.PROFILE` | Сведение прежнего требования: Свежий CPU/GPU/frame-time профиль Low/Medium/High с реальной конфигурацией и бюджетом | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-090 |

## Исторические требования с явной заменой

| ID | Результат | Исходник / связь |
|---|---|---|
| `NARR-001` | Заблокировать narrative package Acts 2–5 до дорогих ассетов | [контракт очереди](../urman_knowledge_base/execution_backlog.json); PW-003, PW-004 |

## Отдельный продукт Studio — виден в очереди, не гейт игры

| ID | Результат | Исходник / связь |
|---|---|---|
| `STUDIO` | URMAN Studio — визуальный редактор игры по ТЗ 2026-09-27 (зонтик) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X1` | Эксперимент: перенос участка мира из C# в авторские данные | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X2` | Эксперимент: встроенное моделирование (Godot-меш + Blender 4.5 за интерфейсом) | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X3` | Эксперимент: единая модель квеста для списка и графа с развилками | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X4` | Эксперимент: семантическое объединение ревизий по сущностям и полям | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P0` | P0: кликабельный прототип пяти экранов и технические границы | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P1` | P1: один сквозной рабочий участок — карта и квест вместе | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P2` | P2: вся деревня и каталог | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P3` | P3: полная логика квестов, диалоги, NPC, анимации, сцены | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P4` | P4: остальные игровые редакторы | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P5` | P5: встроенное моделирование, лица, одежда, персонажи | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P6` | P6: совместная работа, восстановление, передача контекста, окончательная приёмка | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |

## Отдельно разрешаемый cutover после выпуска

| ID | Результат | Исходник / связь |
|---|---|---|
| `REL-004` | Выполнить absence-gated web runtime retirement | [контракт очереди](../urman_knowledge_base/execution_backlog.json) |
