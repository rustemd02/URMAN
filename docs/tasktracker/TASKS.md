# TASKS.md — навигация единой очереди УРМАН

20 блоков · 308 стабильных карточек · 448 исполнимых единиц · 122 агрегатов · 60 внешних гейтов · 47 условных единиц.

Источник состояний, назначений, результатов и evidence — только [execution_backlog.json](../urman_knowledge_base/execution_backlog.json). Этот индекс генерирует `python3 tools/tasktracker.py render`; он не хранит статусы. `python3 tools/tasktracker.py show ID` показывает конечный контракт и следующий шаг; `ready` — доступные входы.

Действующий порядок 11.10: [короткое исполнение](06_small_model_execution_2026-09-22.md) и [полный продукт](07_full_game_integration_2026-10-08.md). Живое поручение и разрешённый объём указаны в `execution_schema.current_authority` и `current_scope.execution_this_request` единого JSON.

Процедурные S-шаги не являются самостоятельными задачами. Агрегаты не назначаются разработчикам. Инженерное завершение и внешняя приёмка считаются отдельно. Условные единицы входят в обязательный остаток только при `condition.state=true`.

Приоритет MVP: Сначала законченные, сразу заметные игроку этапы: обычный запуск и Continue; цельный пролог и первая прогулка; фото → физическая книга → вход → содержательное действие → возврат → сохранённое продолжение; убедительная сцена деревни; самостоятельный релизный кандидат. Мелкие веточки, кусты, фактуры и локальная полировка идут после этих результатов, кроме доказанного блокера проходимости, читаемости, стабильности или производительности выбранного этапа.

`ready` сортирует доступные задачи по `mvp_delivery_order`; этот приоритет не снимает зависимости и не меняет статусы.

| Блок | Карточек | Исполнимых единиц | Результат |
|---|---:|---:|---|
| [B01](#b01) | 15 | 22 | Физическая семейная книга, снимки, обороты, вклейка и чтение. |
| [B02](#b02) | 30 | 66 | Живописные материалы, выразительный цвет и свет, убедительные снег и растительность. |
| [B03](#b03) | 42 | 56 | Эталонный дом, затем жилые/общественные здания, дворы, ограды, мост и интерьеры. |
| [B04](#b04) | 24 | 24 | Надёжный вход в снимок, действие внутри, сохранение и возвращение. |
| [B05](#b05) | 6 | 8 | Законченный лесной мир, действия и встреча с Шурале. |
| [B06](#b06) | 6 | 8 | Речной мир, отражение, настоящий подход и Су анасы. |
| [B07](#b07) | 5 | 7 | Два вида одного двора и содержательные бытовые действия. |
| [B08](#b08) | 5 | 7 | Мир общего кадра с действиями и спокойной кульминацией. |
| [B09](#b09) | 5 | 7 | Необязательный игроку мир с Бичурой и порядком вещей. |
| [B10](#b10) | 28 | 30 | Естественные жители, занятия, реакции и осмысленный татарский. |
| [B11](#b11) | 10 | 13 | Рабочий компьютер с поиском, чатами, документами и печатью снимков. |
| [B12](#b12) | 16 | 20 | Удобное движение, тело, хват, меню и понятный ввод. |
| [B13](#b13) | 3 | 5 | Убедительный салон Нивы, полезная поездка и управление транспортом. |
| [B14](#b14) | 10 | 10 | Шаги, радио, окружение, пространственные события и звуковые переходы. |
| [B15](#b15) | 52 | 33 | Настоящие обязательные действия, внешние подтверждения и законченный сюжет. |
| [B16](#b16) | 20 | 14 | Самостоятельно запускаемая принятая игра. |
| [B18](#b18) | 16 | 37 | Осмысленные EX-действия: перенос, вода, снег, свет, тепло, звук и поиск. |
| [B17](#b17) | 1 | 0 | Сохранённые задачи вне текущей кампании; только по отдельному поручению. |
| [B19](#b19) | 2 | 3 | Самостоятельно понятный ранний эпизод из New Game с действием, возвратом и Continue; полный опыт доводится профильными карточками до эпилога. |
| [B20](#b20) | 12 | 78 | Автор редактирует настоящий мир и контент вручную и через Codex/Claude, видит результат в игре и отменяет всю правку. |

<a id="b01"></a>
## B01 — Книга, фотографии и чтение

Книга/Journal UI и собственные ассеты; переходы и миграции передавать B04, общие материалы B02, печать согласовать с B11.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-TEXTURE.04` | aggregate | Бумага, документы, вывески и сюжетные изображения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E01` | engineering | Подключить P01 — Современный бланк ФАПа; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E02` | engineering | Подключить P02 — Старый машинописный лист; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E03` | conditional | Подключить P03 — Лист школьной тетради; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E04` | engineering | Подключить P04 — Долговая тетрадь магазина; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E05` | engineering | Подключить P05 — Картон архивной папки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E06` | conditional | Подключить P06 — Оборот семейной фотографии; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E07` | conditional | Подключить P07 — Пустой конверт: поверхность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E08` | engineering | Подключить P08 — Подложка объявлений и календаря; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E09` | conditional | Подключить P09 — Повреждённая бумага; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E10` | conditional | Подключить P10 — Основа товарной этикетки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E11` | engineering | Подключить P11 — Семейный снимок по референсу; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E12` | engineering | Подключить P12 — Фото или рисунок для EX12; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E13` | conditional | Подключить P13 — Аватар / портрет в ПК; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/E14` | engineering | Подключить P14 — Архивная фотография / школьная запись / Сабантуй; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.04/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Бумага, документы, вывески и сюжетные изображения»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NOTEBOOK` | aggregate | Сведение прежнего требования: Личная записная книжка и постепенное знание; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.4` | aggregate | Сведение прежнего требования: Простая личная книжка и понятный переход от наблюдения к действию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NOTEBOOK.DESIGN` | engineering | Простая личная книжка и понятный переход от наблюдения к действию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-007` | engineering | Создать строгий каталог фото, миров и разворотов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-013` | engineering | Реализовать приобретение и разрешённое копирование фото; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-014` | engineering | Реализовать фронт, оборот и читаемую транскрипцию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-015` | engineering | Реализовать вклейку и её сохранение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-016` | engineering | Переработать журнал в семейную книгу без потери функций; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-017` | engineering | Произвести физическую книгу и хват от первого лица; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-018` | engineering | Согласовать ввод, фокус и modal ownership; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-040` | aggregate | Выпустить все 13 согласованных изображений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-040/E01` | engineering | Поставить камеры и три узнаваемые опоры каждой портальной фотографии до её финальной отрисовки.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-040/E02` | engineering | Произвести 13 фронтов, 13 оборотов, миниатюры и транскрипции.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-040/E03` | engineering | Разделить игровой вымышленный документ, разрешённую копию и проходной снимок.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-041` | engineering | Набрать текст книги и короткие реплики; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-042` | engineering | Сделать физическую печать из ПК; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-091` | integration_check | Проверить книгу при 720p, 1080p, 16:10 и крупном тексте; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b02"></a>
## B02 — URMAN Rework — визуальный стиль

Общие shader/material/environment и производство фактур; конструкция домов B03, отдельные сцены миров B05–B09. Не заменять форму новой текстурой.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-TEXTURE` | aggregate | Сгенерировать и внедрить разнообразную библиотеку текстур; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Сгенерировать и внедрить разнообразную библиотеку текстур»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Сгенерировать и внедрить разнообразную библиотеку текстур»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.00` | engineering | Дополненный существующий инвентарь текстур для текущих и планируемых объектов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.00/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Дополненный существующий инвентарь текстур для текущих и планируемых объектов»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.01` | engineering | Первая партия: 16 опорных текстур; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02` | aggregate | Экстерьеры, снег, дерево, камень и растения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E01` | engineering | Подключить S03 — Утоптанный снег у дверей; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E02` | engineering | Подключить S04 — Снег на крышах и верхних гранях; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E03` | engineering | Подключить S05 — Смерзшийся грязный снег; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E04` | conditional | Подключить S06 — Открытая промёрзшая земля; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E05` | conditional | Подключить S07 — Лесная подстилка под снегом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E06` | engineering | Подключить S08 — Поверхность небольшого льда; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E07` | engineering | Подключить S09 — Снег с редкими растительными включениями; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E08` | conditional | Подключить S10 — Грунт в сухих сенях и под навесом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E09` | conditional | Подключить S11 — Асфальт под зимним покрытием; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E10` | conditional | Подключить S12 — Щебёночная подсыпка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E11` | engineering | Подключить W02 — Серебристое дерево забора; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E12` | engineering | Подключить W05 — Светлая краска наличников; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E13` | conditional | Подключить W06 — Сдержанная охристая краска; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E14` | engineering | Подключить W07 — Тёмное дерево сарая; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E15` | engineering | Подключить W10 — Светлое дерево бани; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E16` | engineering | Подключить W11 — Расколотые дрова и рукояти; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E17` | engineering | Подключить W12 — Торец древесины; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E18` | engineering | Подключить B02 — Чистая штукатурка мечети; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E19` | conditional | Подключить B05 — Линолеум магазина и школы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E20` | conditional | Подключить B06 — Красный кирпич как поверхность отдельных кирпичей; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E21` | engineering | Подключить B07 — Минеральный фундамент; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E22` | conditional | Подключить B08 — Бетон учреждений и остановки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E23` | engineering | Подключить B09 — Серое кровельное покрытие; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E24` | conditional | Подключить B10 — Тихая каменная поверхность зирата; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E25` | engineering | Подключить F03 — Еловая кора; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E26` | engineering | Подключить F04 — Хвоя на существующих объёмах кроны; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E27` | engineering | Подключить F05 — Небольшая хвойная веточка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E28` | conditional | Подключить F06 — Сухая зимняя осока; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E29` | engineering | Подключить F07 — Сено; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E30` | engineering | Подключить F08 — Прутья плетня и сухих ветвей; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/E31` | engineering | Подключить F09 — Пигмент ягод рябины; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.02/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Экстерьеры, снег, дерево, камень и растения»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03` | aggregate | Интерьерные ткани, металл, пластик и утварь; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E01` | engineering | Подключить M01 — Сдержанно окисленный оцинкованный металл; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E02` | engineering | Подключить M02 — Тёмное рабочее железо; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E03` | engineering | Подключить M03 — Белая бытовая эмаль; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E04` | engineering | Подключить M04 — Окрашенный металл радиатора и шкафа; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E05` | engineering | Подключить M06 — Тёмный пластик и бакелит; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E06` | engineering | Подключить M07 — Резина; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E07` | conditional | Подключить M08 — Выцветшая краска транспорта; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E08` | engineering | Подключить M09 — Потёртый винил сидений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E09` | engineering | Подключить M10 — Неглазурованная керамика; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E10` | engineering | Подключить M11 — Глазурованная бытовая керамика; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E11` | conditional | Подключить M12 — Джут, шнур и верёвочное волокно; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E12` | engineering | Подключить M13 — Мебельный ламинат; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E13` | engineering | Подключить M14 — Нержавеющая сталь; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E14` | engineering | Подключить M15 — Практичная кожа и кожзам; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E15` | conditional | Подключить M16 — Чистая полимерная поверхность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E16` | conditional | Подключить T02 — Клеёнка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E17` | engineering | Подключить T03 — Лёгкая тюлевая занавеска; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E18` | engineering | Подключить T04 — Плотная домашняя штора; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E19` | engineering | Подключить T05 — Тканая дорожка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E20` | conditional | Подключить T06 — Уникальный домашний ковёр; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E21` | engineering | Подключить T07 — Полотенце; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E22` | engineering | Подключить T09 — Ковровое покрытие мечети; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E23` | engineering | Подключить T11 — Тяжёлая ткань ДК; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/E24` | engineering | Подключить T12 — Обои жилой комнаты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.03/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Интерьерные ткани, металл, пластик и утварь»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05` | aggregate | Адресные декали и изменяемые состояния; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E01` | engineering | Подключить D01 — Небольшая потёртость краски; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E02` | conditional | Подключить D02 — Локальная ржавчина; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E03` | engineering | Подключить D03 — Копоть у топки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E04` | conditional | Подключить D04 — Сухая пыль; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E05` | conditional | Подключить D05 — Вода и высохший налёт; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E06` | engineering | Подключить D06 — След клея и сорванной бумаги; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E07` | engineering | Подключить D07 — Морозный узор окна; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E08` | conditional | Подключить D08 — Широкое морозное покрытие; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E09` | conditional | Подключить D09 — Мягкий зимний след; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.05/E10` | conditional | Подключить D10 — След локального ремонта; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06` | aggregate | Персонажи, одежда и первое лицо по реальным UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E01` | engineering | Подключить C01 — Мансур: кожа на существующем UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E02` | conditional | Подключить C02 — Гөлсинә: кожа на существующем UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E03` | conditional | Подключить C03 — Алсу: кожа на существующем UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E04` | conditional | Подключить C04 — Наиля: кожа на существующем UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E05` | conditional | Подключить C05 — Ринат: кожа на существующем UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E06` | conditional | Подключить C06 — Тимур хәзрәт: кожа на существующем UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E07` | engineering | Подключить C07 — Глаза: согласованная правка атласа; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E08` | engineering | Подключить C08 — Волосы и брови: текущий атлас; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E09` | engineering | Подключить C09 — Матовая зимняя курточная ткань; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E10` | conditional | Подключить C10 — Шерстяной трикотаж; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E11` | engineering | Подключить C11 — Плотная брючная ткань; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.06/E12` | conditional | Подключить C12 — Айдар: руки и видимое тело; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07` | aggregate | Крупные предметы, ПК и используемый транспорт по настоящим UV; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/E01` | conditional | Подключить O01 — Старый домашний ПК: корпус; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/E02` | conditional | Подключить O02 — Нива: индивидуальные следы эксплуатации; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/E03` | conditional | Подключить O03 — Мотоцикл: окрашенные детали; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/E04` | conditional | Подключить O04 — Лошадь: шерсть по текущей развёртке; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/E05` | conditional | Подключить O05 — Рабочий фонарь / инструмент: адресный износ; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/E06` | conditional | Подключить O06 — Рабочая тетрадь / книга: обложка Акта I; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.07/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Крупные предметы, ПК и используемый транспорт по настоящим UV»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08` | aggregate | Экранные подложки, загрузочный арт и условные эффекты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08/E01` | conditional | Подключить U01 — Тихая бумажная подложка журнала; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08/E02` | conditional | Подключить U02 — Сухой графический мазок; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08/E03` | engineering | Подключить U03 — Обои домашнего компьютера; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08/E04` | conditional | Подключить V01 — Дыхание / пар; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08/E05` | engineering | Подключить V02 — Лёгкий печной дым; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.08/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Экранные подложки, загрузочный арт и условные эффекты»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.09` | aggregate | Сведение прежнего требования: Уникальные носители Актов II–V; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.10` | aggregate | Сведение прежнего требования: Утверждённые поверхности Шурале и Су Анасы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.11` | engineering | Подключение карт к семантическому владельцу материалов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.11/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Подключение карт к семантическому владельцу материалов»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.12` | integration_check | Проверить текстуры и игровые кадры каждой интегрированной партии; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TEXTURE.13` | external_acceptance | Человеческая художественная и культурная приёмка библиотеки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-07` | aggregate | Сведение прежнего требования: Пресеты реально меняют нагрузку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-003` | aggregate | Сведение прежнего требования: Принять три in-engine style benchmarks; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-005` | engineering | Создать и проверить Painterly texture production candidates; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-007` | engineering | Разделить semantic material owners для imported wood и cloth; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ART-DIRECTION` | aggregate | Сведение прежнего требования: Доказать достижимый стиль одним участком в действующем движке; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ATMOSPHERE` | aggregate | Уютные помещения и давящая зимняя непогода снаружи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ATMOSPHERE/E01` | engineering | Согласовать зимний внешний свет/видимость/частицы с действующим профилем деревни; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ATMOSPHERE/E02` | engineering | Согласовать уютный свет интерьеров и переход наружу без утечки shared-параметров; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MATERIAL.MOTION` | aggregate | Сведение прежнего требования: Исправленный источник скольжения материала относительно движущейся формы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PERF.PROFILE` | aggregate | Сведение прежнего требования: Свежий CPU/GPU/frame-time профиль Low/Medium/High с реальной конфигурацией и бюджетом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-010` | engineering | Свести новый стиль с прежним visual reset; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-010/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Свести новый стиль с прежним visual reset»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-031` | engineering | Зафиксировать colorscript и значения профилей; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-031/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Зафиксировать colorscript и значения профилей»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-032` | engineering | Исправить привязку мазка к движущимся поверхностям; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-033` | engineering | Настроить воспроизводимый Blender→GLB цикл; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-034` | engineering | Произвести общие материалы и растительные формы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-037` | engineering | Настроить LOD, batching и culling без разрушения стиля; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-083` | integration_check | Проверить границы сезонов, света и эффектов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-089` | engineering | Устранить мерцание и скольжение в движении; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b03"></a>
## B03 — Деревня, дома и окружение

Меши, UV, коллизии и размещение наружной деревни; общие материалы B02, жители B10, событийный граф B15.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-VILLAGE-LAYOUT` | aggregate | План живой Кара-Урман под 60–70 жителей и якоря новой кампании; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR` | aggregate | Адреса действующей деревни и самостоятельная навигация; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR/E04` | engineering | Согласовать27 building bindings/street aliases с SettlementRegistry/SettlementRoadGraph; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-SHOP` | engineering | Магазин и атомарная долговая тетрадь; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC` | aggregate | Баня, мечеть, школа, сельсовет/ДК и семейный зират; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-04` | aggregate | Home FAP cast and contextual village presentation; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-04/E04` | engineering | Сопоставить actual дом/ФАП/cast в ordinary first-person target видах720p/1080p; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-001` | engineering | Произвести and verify modular environment kit; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-004` | engineering | Заменить greybox dressing и принять authored mesh collision; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-009` | engineering | Подключить authored WellA, WoodpileA и GateA к village dressing; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR-FENCE` | engineering | Таблички на заборе/калитке для домов без места на фасаде (H032/H034/H045/H046); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR-ACCESS` | engineering | Физический доступ к адресам и честные границы новой кампании; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH` | aggregate | Техническое ревью мира: свет, коллизии, мерцание текстур; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.1` | research | Аудит технических проблем по зонам; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.2` | engineering | Устранён корень провала у D14 X29.8 Z2.6 после проверки неоднозначной координаты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.3` | aggregate | Исправления общего производителя дверей/стен/крыш/окон/лесных пересечений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.3/E01` | research | Установить exact mesh/generator/consumer проёма K13 и отсутствие створки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.3/E02` | engineering | Устранить пустоту/z-fighting выбранного shared door/wall/roof/window producer по exact mesh IDs; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TECH.4` | engineering | Освещение интерьеров; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH` | aggregate | Настоящие объёмные объекты вместо плоских заглушек; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.1` | research | Инвентаризация плоских заглушек; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.2` | engineering | Дом бабая: объёмные объекты интерьера; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.3` | engineering | Действующий медпункт с осмысленным разделением функций и приятным бытовым видом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.4` | engineering | Магазин: объёмные объекты интерьера; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.5` | engineering | Рабочие комнаты школы/ДК, достаточные проходы и достоверные предметы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.6` | aggregate | Мечеть и баня: совокупный результат профильных ТЗ; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.6/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Мечеть и баня: совокупный результат профильных ТЗ»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.7` | engineering | Улица и дворы: объёмные объекты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9` | aggregate | Эталон жилого срубного дома по вступительному изображению; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9/E01` | engineering | Подключить геометрию и UV жилого сруба бабая по выбранному reference ID; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9/E02` | engineering | Подключить крыльцо бабая: стойки, ступени, настил и свободный вход по reference ID; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9/E03` | engineering | Подключить интерьер бабая: диван, обои, занавески, печь, шкаф, ковёр и D14 без перекрытия прохода; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9/PORCH-UV` | engineering | Подключить направленные UV настила крыльца и настоящий торец досок; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9/STOVE` | engineering | Подключить компактную геометрию печи в комнате бабая; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.9/CORNER-MATERIAL` | engineering | Согласовать longitudinal/end-grain response угловых брёвен без смены silhouette; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10` | aggregate | Распространить срубную архитектуру на жилые дома Акта I; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10/E01` | engineering | Перенести принятую срубную/оконную конструкцию на семейство DwellingFacade_TimberPlaster; только instances: urman.world:act1/kit/village-day-main-street-east-facade; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10/E02` | engineering | Перенести принятую срубную/оконную конструкцию на семейство VillageParcel_VariantA_TimberGable_Dwelling; только instances: urman.world:act1/kit/village-day-east-street-horizon-facade; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10/E03` | engineering | Перенести принятую срубную/оконную конструкцию на семейство VillageParcel_VariantB_PlasterAnnex_Dwelling; только instances: urman.world:act1/kit/village-house-returneasthousea8silhouette, urman.world:act1/kit/zirat-road-west-return-mid-facade; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10/E04` | engineering | Перенести принятую срубную/оконную конструкцию на семейство VillageParcel_VariantC_BanyaYard_Dwelling; только instances: urman.world:act1/kit/zirat-road-east-return-mid-facade; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.10/E05` | research | Установить конкретный source consumer камней I12, опоры/силуэт и ID, затем передать изменение ACT1-DEPTH.7; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.11` | external_acceptance | Художественная приёмка домов относительно вступительного изображения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.12` | aggregate | Приблизить эталонный дом к изначальному вступительному изображению; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.12/E01` | engineering | Согласовать силуэт, цоколь, вынос кровли и глубину проёмов эталонного дома с выбранным reference ID; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.12/E02` | engineering | Согласовать цвет дерева/наличников, свет окон и снег эталонного дома с существующими материалами; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.12/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Приблизить эталонный дом к изначальному вступительному изображению»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-VILLAGE-COMPOSITION` | research | Ведомость retain/move/rework/retire для всех перечисленных построек, пустырей, камней и двора бабая; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-DEPTH.8` | integration_check | Приёмка глубины: нет плоских имитаций объёма; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-VILLAGE-BLOCKOUT` | engineering | Проходимая раскладка принятой деревни и пассажирский въезд; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PLACEMENT-RULES` | engineering | Короткие проверяемые правила посадки/габаритов/стыков у существующего владельца данных; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-HOUSE-SCALE` | aggregate | Согласованные физические габариты дверей, этажей и домов по источнику mesh и instance; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-HOUSE-SCALE/H019-CAUSE` | research | Установить причину срезанной крыши H019/BLD-H019/ADR-H019; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-HOUSE-SCALE/H019-FIX` | engineering | Восстановить крышу и фронтон H019 у установленного shared producer; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-HOUSE-SCALE/SCALE-CHECK` | integration_check | Проверить габариты K15/H019 и капсулу на установленных типах домов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FENCE-KIT` | engineering | Небольшое семейство оград и нормальные углы/ворота/опоры; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-RIVER-BRIDGE` | engineering | Физически убедительный мост и берег на маршруте новой кампании; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-BOUNDARY` | engineering | Видимая естественная граница деревни/лесного эпизода с аварийной страховкой вне обычного пути; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR.MOUNT` | aggregate | Раздельные правила для уличного указателя и номерной таблички, крепёж и чтение с подхода; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR.MOUNT/MOSQUE-PLATE` | engineering | Подключить читаемую exterior табличку мечети на высоте1.8000001м; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-ADDR.MOUNT/REMAINING` | integration_check | Проверить остальные facade-mount/access случаи табличек; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.FACADES` | aggregate | Различимая сельская школа, ДК, ФАП и сине-белый магазин; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.FACADES/E01` | engineering | Подключить различимую сельскую школу с входом и фасадом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.FACADES/E02` | engineering | Подключить различимый ДК с входом и фасадом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.FACADES/E03` | engineering | Подключить различимый ФАП с входом и фасадом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.FACADES/E04` | engineering | Подключить сине-белый магазин с входом и фасадом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-INTERIOR.PLAN` | aggregate | Раздельные основные интерьеры и физически согласованные малые дома; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-INTERIOR.PLAN/E01` | engineering | Согласовать наружный проём и вход в комнату бабая; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-INTERIOR.PLAN/E02` | engineering | Согласовать наружный проём и вход в ФАП; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-INTERIOR.PLAN/E03` | engineering | Согласовать наружный проём и вход в магазин; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-INTERIOR.PLAN/E04` | engineering | Согласовать наружные проёмы и входы школы/ДК; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MOSQUE` | engineering | Цельная уважительная татарская мечеть с обоснованным минаретом, входом и обжитым залом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MOSQUE/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Цельная уважительная татарская мечеть с обоснованным минаретом, входом и обжитым залом»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-BATHHOUSE` | engineering | Уютная необязательная баня с приватным окном, парилкой и выразительным духом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PUBLIC.POST` | engineering | Почтовое отделение с читаемым местом/назначением и минимальной бытовой ролью; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-044` | engineering | Удержать красоту наружной деревни после разделения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b04"></a>
## B04 — Переходы, сохранения и общий runtime

Один владелец общих kernel/save/transition/anchor контрактов; книга и миры потребляют их, не создают параллельные bool/сейвы.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `BASE-001` | engineering | Зафиксировать baseline и golden fixtures; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `CORE-001` | engineering | Перенести content compiler и Content Lab на C#; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `CORE-002` | engineering | Перенести runtime kernel, capabilities and SaveGameV3; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-005` | engineering | Определить версию состояния новой кампании; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-006` | engineering | Расширить единый kernel командами фотокниги; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-008` | engineering | Разделить впечатление и доказанное знание; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-009` | engineering | Создать реестр семантических якорей и safe nodes; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-012` | engineering | Ввести session generation и отмену переходов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-019` | engineering | Реализовать условия первого сознательного входа; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-020` | engineering | Реализовать prepare и возвратный билет; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-021` | engineering | Загрузить фотомир без блокирующей сцены на main loop; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-022` | engineering | Проверить spawn и завершить commit; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-023` | engineering | Вернуться из любой нормальной точки мира; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-024` | engineering | Сохранение и загрузка внутри фотомира; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-025` | engineering | Сделать миграции safe nodes и защиту старых слотов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-026` | engineering | Приостановить внешние часы и бытовые процессы на время визита; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-027` | engineering | Разрешить спокойные повторные посещения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-028` | engineering | Сделать необязательные запросные подсказки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-029` | engineering | Сохранять предметные действия и пространственные связи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-030` | engineering | Создать изолированные fixtures и capture harness; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-036` | engineering | Реализовать конечные обратимые пространственные связи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-038` | engineering | Ограничить память и срок жизни миров; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-085` | integration_check | Прогнать матрицу двадцати сбоев сохранений и переходов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-086` | integration_check | Проверить двадцать циклов посещений и сохранённые последствия; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b05"></a>
## B05 — W01 — Лес за спиной

Только собственная сцена/контент W01; общий переход B04, стиль B02, событийная интеграция B15.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `PW-043` | engineering | Перенести пролог внутрь W01; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-045` | aggregate | W01: произвести и собрать всю проходимую геометрию «Лес за спиной»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-045/E01` | engineering | Собрать узлы F00, F01, F02, F03, F10, F11, F12, F13, F14, F15, F16 и все связи из level-паспорта.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-045/E02` | engineering | Произвести перечисленные геометрические семейства с реальным размером, UV, опорами и дальними планами.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-045/E03` | engineering | Авторить три linger-точки F11, F14, F15 с полноценным обратным обзором.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-046` | engineering | W01: действия за рамкой и состояние ветки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-047` | engineering | W01: произвести, оживить и поставить Шурале; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-048` | engineering | W01: закончить звуковую и световую постановку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-049` | integration_check | W01: принять обычный маршрут, изображения и повторное посещение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-049/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «W01: принять обычный маршрут, изображения и повторное посещение»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b06"></a>
## B06 — W02 — Мостки

Сцена W02 и специфическое отражение; наружный мост остаётся B03, общий переход B04.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `PW-035` | engineering | Реализовать ограниченный отражённый вид W02; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-050` | aggregate | W02: произвести и собрать всю проходимую геометрию «Мостки»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-050/E01` | engineering | Собрать узлы R00, R01, R02, R03, R04, R05, R06, R07, R08 и все связи из level-паспорта.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-050/E02` | engineering | Произвести перечисленные геометрические семейства с реальным размером, UV, опорами и дальними планами.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-050/E03` | engineering | Авторить три linger-точки R02, R06, R07 с полноценным обратным обзором.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-051` | engineering | W02: связать отражение с настоящим подходом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-052` | engineering | W02: произвести и поставить Су анасы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-053` | engineering | W02: закончить звуковую и световую постановку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-054` | integration_check | W02: принять обычный маршрут, изображения и повторное посещение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-054/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «W02: принять обычный маршрут, изображения и повторное посещение»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b07"></a>
## B07 — W03 — Дом на просвет

Сцена W03, собственные пространственные связи и контент; общая реализация обратимых связей B04.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `PW-055` | aggregate | W03: произвести и собрать всю проходимую геометрию «Дом на просвет»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-055/E01` | engineering | Собрать узлы H00, H01, H02, H03, H04, H05, H06, H07, H08 и все связи из level-паспорта.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-055/E02` | engineering | Произвести перечисленные геометрические семейства с реальным размером, UV, опорами и дальними планами.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-055/E03` | engineering | Авторить три linger-точки H04, H05, H07 с полноценным обратным обзором.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-056` | engineering | W03: реализовать два вида одного двора; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-057` | engineering | W03: бытовые действия и позиция фотографа; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-058` | engineering | W03: закончить звуковую и световую постановку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-059` | integration_check | W03: принять обычный маршрут, изображения и повторное посещение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-059/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «W03: принять обычный маршрут, изображения и повторное посещение»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b08"></a>
## B08 — W04 — Общий кадр

Сцена W04 и локальные действия; условия общего сюжета B15.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `PW-060` | aggregate | W04: произвести и собрать всю проходимую геометрию «Общий кадр»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-060/E01` | engineering | Собрать узлы C00, C01, C02, C03, C04, C05, C06, C07, C08 и все связи из level-паспорта.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-060/E02` | engineering | Произвести перечисленные геометрические семейства с реальным размером, UV, опорами и дальними планами.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-060/E03` | engineering | Авторить три linger-точки C01, C05, C06 с полноценным обратным обзором.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-061` | engineering | W04: условия входа и четыре содержательных действия; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-062` | engineering | W04: собрать непрерывный горизонт и спокойную кульминацию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-063` | engineering | W04: закончить звуковую и световую постановку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-064` | integration_check | W04: принять обычный маршрут, изображения и повторное посещение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-064/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «W04: принять обычный маршрут, изображения и повторное посещение»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b09"></a>
## B09 — W05 — Тёплое подполье

Сцена W05 и локальное поведение; необязательность игроку не отменяет производство.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `PW-065` | aggregate | W05: произвести и собрать всю проходимую геометрию «Тёплое подполье»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-065/E01` | engineering | Собрать узлы B00, B01, B02, B03, B04, B05, B06, B07 и все связи из level-паспорта.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-065/E02` | engineering | Произвести перечисленные геометрические семейства с реальным размером, UV, опорами и дальними планами.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-065/E03` | engineering | Авторить три linger-точки B01, B05, B06 с полноценным обратным обзором.; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-066` | engineering | W05: произвести и оживить Бичуру; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-067` | engineering | W05: порядок вещей и необязательное внешнее открытие; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-068` | engineering | W05: закончить звуковую и световую постановку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-069` | integration_check | W05: принять обычный маршрут, изображения и повторное посещение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-069/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «W05: принять обычный маршрут, изображения и повторное посещение»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b10"></a>
## B10 — Люди, язык и культурная достоверность

Люди/rig/анимации/языковой контент; существа конкретного мира в B05–B09, звуковая инфраструктура B14.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `NARR-002` | aggregate | Сведение прежнего требования: Провести татарский и культурно-религиозный review authored Acts 2–5; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-002` | engineering | Произвести project-original character silhouette kit; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-003` | aggregate | Создать hero faces, clothing and authored character animation; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-003/E01` | engineering | Подключить лицо/одежду главных людей к лицензированной базе без потери rig; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-003/E02` | engineering | Подключить authored idle/walk к реальному NPC consumer с LOD и бюджетом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG` | aggregate | Татарский словарь: автосбор слов и начальная калибровка знания; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.1` | engineering | Единый реестр татарских слов и статусы в сейве; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.2` | engineering | Автосбор услышанных слов в записную книжку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.4` | engineering | Определить уровень татарского в живом разговоре с бабаем в Ниве; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.5` | engineering | Ручная смена одного из трёх уровней без сброса прогресса; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.6` | engineering | Подтверждение гипотез и видимый прогресс; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.7` | engineering | Разметка татарских слов в авторском контенте; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.6` | engineering | Татарский словарь; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR` | aggregate | Естественные люди и намеренная тревожность отдельных сцен; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Естественные люди и намеренная тревожность отдельных сцен»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.1` | research | Подходящая современная модель/rig/анимация с подтверждёнными правами и бюджетом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.1/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Подходящая современная модель/rig/анимация с подтверждёнными правами и бюджетом»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.2` | aggregate | Лица и головы до эталона; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.2/E01` | engineering | Подключить формы лиц/голов NPC Mansur/Gulsina/Alsu/Rinat/Naila/TimurHazrat к лицензированной базе; Aidar body отдельно B12, Marat не живой NPC; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.2/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Лица и головы до эталона»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.3` | aggregate | Материалы кожи, глаз, волос, одежды; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.3/E01` | engineering | Подключить skin/eye/hair slots NPC Mansur/Gulsina/Alsu/Rinat/Naila/TimurHazrat к semantic material owner; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.3/E02` | engineering | Подключить winter cloth/headwear Mansur/Gulsina/Alsu/Rinat/Naila/TimurHazrat к UV/rig и исправить clipping характерных поз; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.3/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Материалы кожи, глаз, волос, одежды»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.4` | engineering | Естественная походка и сдержанный idle NPC; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.5` | external_acceptance | Приёмка персонажей по эталону; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC` | aggregate | Живая деревня: NPC с распорядком и занятиями; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.1` | engineering | Data-driven распорядок NPC (routine); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2` | aggregate | Набор занятий и поз с переходами; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2/E01` | research | Зафиксировать6–8 exact routine activity IDs с existing animation/pose consumer; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2/STAND-WALK` | engineering | Связать idle-plain→walk-plain→idle-plain на named resident route; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2/TALK` | engineering | Связать walk-plain→talk-plain→возврат к расписанию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2/SIT` | engineering | Связать sit-down→sit→stand-up с named seat и collider; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2/WORK` | engineering | Связать work-kneel/torch с named prop и actor consumer; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.2/OWNERSHIP` | engineering | Согласовать schedule Claim/Release, onBlocked/safePoint/follow без второго motion owner; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.3` | aggregate | Новые жители деревни из данных; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.3/E01` | research | Сопоставить actual resident dataset с60–70 жителями и явными отсутствующими IDs; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.3/E02` | engineering | Подключить roster residents к data-driven NPC/двору/распорядку с бюджетом scene; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.4` | engineering | Реакции NPC на события и время суток; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NPC.5` | integration_check | Приёмка живой деревни; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.APPLY` | engineering | Решить содержательную задачу через татарское слово в поиске ПК/Татарвики; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.ADAPT` | engineering | Три авторских варианта доли и сложности татарского при одной истории; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CHAR.CAT` | engineering | Узнаваемая кошка с подходящими пропорциями, фактурой и простым живым поведением; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-080` | engineering | Провести языковой проход и нормализовать имена; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-092` | external_acceptance | Провести культурный review без выдуманного канона; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b11"></a>
## B11 — Старый ПК, поиск и печать

OldPc UI и его контент; семейная книга B01, единое знание/сохранения B04.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-OLDPC` | aggregate | Компьютер 2000-х и внутриигровая сеть; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC/E08` | engineering | Подключить browser navigation/back/history к accessConditions и desktop snapshot; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC/E09` | engineering | Подключить document viewer с повторным чтением и запретом чужого knowledge; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC/E10` | engineering | Подключить notes/editor save draft и reopen без потери пользовательского текста; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC/E11` | engineering | Подключить social page links/доступные записи к snapshot без spoiler выдачи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-CHAT` | engineering | Живой чат «Ялкын · Сообщения»: треды Алсу/Ринат/бабай/заметки, варианты ответа, триггеры, unread, bounded-снапшот; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-HINTS` | engineering | Контекстные подсказки 3 уровней: тред «Заметки Айдара» + строка архива, hints/*.md, gating без спойлеров, hintsSeen; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-SEARCH` | engineering | Глобальный поиск: поле в «Пуске» + «Искать везде»; доки+чат+заметки+история; татарские ключи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-DOCS` | engineering | Содержательные документы, поиск и повторное чтение без обязательного словесного наполнителя; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-TETRIS` | engineering | Тетрис-пасхалка: стакан 10×20, 7 фигур, уровни/пауза, только tetrisHigh, изоляция от knowledge; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-OLDPC-SHELL` | engineering | Оболочка под 9 окон: снапшот/схема/провайдер (chat, tetris, hintsSeen, tetrisHigh), миграция сейвов, доступность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LANG.3` | engineering | Словарь на ПК бабая; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.5` | engineering | Старый ПК; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-075` | engineering | Сделать поиск файла и собственную подпись на ПК; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b12"></a>
## B12 — Игрок, интерфейс и взаимодействие

Общее движение/UI/focus; содержимое книги B01, ПК B11, транспорт B13.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-PLAYER` | aggregate | Игрок: прыжок, бег, приседание и видимое тело; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PLAYER/E01` | engineering | Согласовать прыжок с grounded/капсулой на земле и у препятствия; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PLAYER/E02` | engineering | Согласовать бег с переназначенным вводом и выходом в обычный шаг; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PLAYER/E03` | engineering | Согласовать приседание с реальной высотой капсулы и отказом встать под препятствием; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06` | aggregate | Product UI menus save resume and credits; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06/E01` | engineering | Подключить NewGame выбранной кампании с отказом при invalid content; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06/E02` | engineering | Подключить Continue самого свежего совместимого save с unknown-version refusal; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06/E03` | engineering | Подключить settings и возврат с сохранением accessibility; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06/E04` | engineering | Подключить credits из actual attribution sources; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06/E05` | engineering | Подключить эпилог→меню→возврат без второго session; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-06/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Product UI menus save resume and credits»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-004` | engineering | Закрыть SaveGameV3-backed accessibility settings contract; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI` | aggregate | Цельный простой UI по выбранной автором системе; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Цельный простой UI по выбранной автором системе»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.1` | research | Два-три сопоставимых варианта простой узнаваемой UI-системы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.2` | engineering | Меню и настройки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.3` | engineering | HUD и диалог; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.7` | integration_check | Совокупная приёмка UI; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LEGS` | aggregate | Правдоподобные ноги при взгляде вниз; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LEGS.1` | integration_check | Ноги Айдара синхронизированы с фактическим движением; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-LEGS.2` | integration_check | Приёмка походки (ходьба/бег/присед/повороты); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.FOCUS` | engineering | Один активный выбор при keyboard focus и hover другого пункта; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-UI.LOADING` | engineering | Понятные реальные состояния загрузки без новой обязательной иллюстрации; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CARRY.CONTACT` | engineering | Правдоподобный хват, перенос и опора предмета; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-081` | integration_check | Проверить весь путь с клавиатурой и контроллером; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-082` | integration_check | Проверить комфорт и доступность фотомиров; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-082/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Проверить комфорт и доступность фотомиров»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b13"></a>
## B13 — Транспорт

Модели/контроллеры транспорта; дороги B03, радио B14, сюжет поездки B15.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-TRANSPORT` | aggregate | Нива, мотоцикл, лошадь и телега; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT/E01` | engineering | Согласовать вход/выход и управление Нивой с SaveGameV3; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT/E02` | engineering | Согласовать вход/выход и управление мотоциклом с SaveGameV3; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT/E03` | engineering | Согласовать коня/телегу с опорами, вводом и сохранённой позой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT.ROUTE` | engineering | Пассажирская экскурсия и полезный транспорт по доступным дорогам; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TRANSPORT.CABIN` | engineering | Собрать салон Нивы для переднего пассажирского ракурса; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b14"></a>
## B14 — Звук и атмосфера

Общая маршрутизация/микс/ресурсы; постановка сцены с её владельцем.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-RADIO` | engineering | Татарское местное радио в транспорте; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-05` | aggregate | Winter sound and complete cue lifecycle; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-05/E06` | engineering | Проверить caption-independent cue queue и сохранение footsteps при reduced motion; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-05/G-LISTENING` | external_acceptance | Подтвердить listening для «Winter sound and complete cue lifecycle»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `AUDIO-001` | aggregate | Произвести authored ambience, voice, captions and non-audio cues; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `AUDIO-001/E01` | engineering | Подключить ambience из имеющихся лицензированных записей к реальным зонам; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `AUDIO-001/E02` | engineering | Подключить VO к обязательным репликам с caption и текстовой альтернативой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `AUDIO-001/E03` | engineering | Подключить non-audio cues к обязательным слуховым действиям; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `AUDIO-001/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Произвести authored ambience, voice, captions and non-audio cues»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP` | aggregate | Звук шагов вне основной дороги: лаг; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP.1` | research | Диагностика лага шагов вне основной дороги; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP.2` | engineering | Фикс лага шагов и проверка проходом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-FOOTSTEP.2/G-LISTENING` | external_acceptance | Подтвердить listening для «Фикс лага шагов и проверка проходом»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-AUDIO.MAP` | research | Карта существующих звуковых событий и среды; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-AUDIO.MAP/G-LISTENING` | external_acceptance | Подтвердить listening для «Карта существующих звуковых событий и среды»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-AUDIO.CUES` | engineering | Ложный success и неправильный звук стука; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-AUDIO.CUES/G-LISTENING` | external_acceptance | Подтвердить listening для «Ложный success и неправильный звук стука»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-039` | engineering | Встроить звуковые домены и переходы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-084` | external_acceptance | Провести полноценный слуховой проход; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b15"></a>
## B15 — Кампания, действия и эпилог

Событийный граф, совместимые EX, якоря находок и эпилог; не подменять производство сцен таблицей.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-ARRIVAL` | aggregate | Сведение прежнего требования: Лесной крючок, поездка с бабаем и семейное прибытие по принятому сценарию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-09` | aggregate | Dense open-world exploration, secrets and hidden paths; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-09/E05` | research | Сопоставить24finds/3loops/1barrier с actual IDs и successor PW079/PW088; неизвестные IDs назвать явно; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-03` | aggregate | Сведение прежнего требования: Three investigation cycles and staged final route; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-02` | aggregate | Сведение прежнего требования: First 8–12 minute playable investigation slice; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `BASE-002` | engineering | Закрепить engine, style, story and retirement boundaries; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-001` | engineering | Собрать first-person Chapter 1 vertical slice; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-002` | engineering | Подключить full-game authored zones and presentation adapters; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `GODOT-005` | engineering | Зафиксировать отдельную играбельную демо-точку входа первого акта; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `NARR-001` | engineering | Заблокировать narrative package Acts 2–5 до дорогих ассетов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-008` | aggregate | Сведение прежнего требования: Подключить authored HouseA к каноническому эпилогу; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01` | aggregate | Ритм и содержательность новой кампании: LEN01; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.1` | aggregate | Ритм и содержательность новой кампании: LEN01.1; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.2` | aggregate | Ритм и содержательность новой кампании: LEN01.2; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.3` | aggregate | Ритм и содержательность новой кампании: LEN01.3; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.4` | aggregate | Ритм и содержательность новой кампании: LEN01.4; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.5` | aggregate | Ритм и содержательность новой кампании: LEN01.5; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.6` | aggregate | Ритм и содержательность новой кампании: LEN01.6; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.7` | aggregate | Ритм и содержательность новой кампании: LEN01.7; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `LEN01.8` | aggregate | Ритм и содержательность новой кампании: LEN01.8; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR` | aggregate | Квесты ведутся адресной системой (без маркеров и автопути); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.1` | research | Аудит квестов на адресность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.2` | engineering | Убрать маркеры и автопуть с обязательного пути; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.3` | engineering | Живой краткий адрес в реплике, запись источника и физическая табличка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-QUEST-ADDR.4` | integration_check | Приёмочный адресный проход квестов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT` | aggregate | Аудит механик: ни одна не висит без дела; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT.1` | aggregate | Сведение прежнего требования: Полная инвентаризация существующих интерактивов по группам с retain/rework/retire и ролью в эпизоде; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT.2` | aggregate | Сведение прежнего требования: Полная инвентаризация существующих интерактивов по группам с retain/rework/retire и ролью в эпизоде; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT.3` | engineering | Понятная бытовая/сюжетная роль сохранённого механизма и общий закон результата; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-MECH-AUDIT.4` | engineering | Сняты ложные обещания действия/случайные записки с проверенными зависимостями; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CUTSCENE` | aggregate | Сведение прежнего требования: Постановка принятого вступления и ночной метели; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CUTSCENE.1` | aggregate | Сведение прежнего требования: Расширить действующую постановку только для тизера, пассажирской поездки и короткой метели; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CUTSCENE.2` | aggregate | Сведение прежнего требования: Лесной крючок, поездка с бабаем и семейное прибытие по принятому сценарию; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-CUTSCENE.3` | aggregate | Сведение прежнего требования: Принять вступление и переход ночи обычным просмотром, пропуском и загрузкой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `REVIEW28-PLAN` | engineering | Полный реестр цитат, шесть тематических ТЗ, решения/зависимости и обновлённая очередь; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `REVIEW28-RESEARCH` | research | Первичные основания BOTW/TOTK и адресные референсы для зависимых пакетов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-NARRATIVE-SHOW` | engineering | Редакторская карта: видимые ремарки и лишние записки заменены действием, звуком, предметом либо содержательной репликой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TUTORIAL` | engineering | Контекстное обучение адресам, сравнению и языку по мере нужды; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-TUTORIAL/G-REAL_PARTICIPANT` | external_acceptance | Подтвердить real_participant для «Контекстное обучение адресам, сравнению и языку по мере нужды»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-PROGRESSION-DOORS` | engineering | Установленные и исправленные причины отсутствия подсказки/входа на трёх названных дверях; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-001` | engineering | Зафиксировать актуальный checkout и базовый проход; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-002` | engineering | Внести авторское разрешение на фундаментальную переработку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-003` | aggregate | Перестроить сюжетный граф кампании «За краем снимка»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-003/GRAPH-SOURCE` | engineering | Скомпилировать canonical photoworlds-v1 graph:13photos/5worlds/7anchors/2first orders; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-003/NEGATIVE-W01` | engineering | Отказать завершению W01 по старым forest semantic actions; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-003/GRAPH-CHECK` | integration_check | Сопоставить полный authored сюжетный граф с physical fact producers без выдачи debug route за игрока; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-004` | engineering | Разобрать судьбу существующих EX и бытовых механик; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-011` | engineering | Импортировать эту очередь в действующий backlog; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-070` | engineering | Разместить и проверить семь деревенских якорей; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-071` | engineering | Поставить находки и копии в реальные места деревни; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-072` | engineering | Сделать школьный контекст лесной фотографии; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-073` | engineering | Сделать внешнюю проверку старого речного подхода; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-074` | engineering | Сделать подтверждение авторства семейного снимка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-076` | engineering | Поставить Алсу перед финальным внешним событием; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-077` | engineering | Реализовать единственный летний лист снаружи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-078` | engineering | Закончить домашний эпилог и открыть свободное возвращение; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-079` | engineering | Переподключить бытовые механики к новой кампании; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b16"></a>
## B16 — Интеграция и итоговая приёмка

Только конкретный текущий интеграционный blocker либо готовая приёмка; не занимать исполнителей бесконечным аудитом.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-TZ-ACCEPT` | integration_check | Совокупная приёмка расширенного ТЗ и адресов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-08` | external_acceptance | External release confirmation; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `FINISH-01` | engineering | Native release export and timed package baseline; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ASSET-006` | engineering | Проверить host-independent provenance реестра ассетов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `QA-001` | external_acceptance | Провести внешнюю accessibility and first-time usability review; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `REL-001` | integration_check | Получить M1 and Windows release-class performance evidence; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `REL-002` | integration_check | Проверить macOS and Windows desktop exports on hosts; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `REL-003` | external_acceptance | Принять готовую игру «За краем снимка» по всей единой очереди; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `REL-004` | conditional | Выполнить absence-gated web runtime retirement; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-087` | integration_check | Пройти оба порядка первых глав и ранние находки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-088` | integration_check | Пройти геометрию, обратные стороны и interaction targets; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-090` | integration_check | Измерить производительность на целевом устройстве; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-093` | engineering | Закрыть происхождение и права каждого runtime-ресурса; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-093/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Закрыть происхождение и права каждого runtime-ресурса»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-094` | integration_check | Пройти кампанию обычным пользовательским маршрутом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-095` | engineering | Собрать самостоятельный релизный кандидат; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-095/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Собрать самостоятельный релизный кандидат»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-096` | integration_check | Прогнать согласованный регрессионный набор деревни; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-097` | engineering | Удалить противоречивые активные инструкции и мёртвые входы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-098` | engineering | Собрать доказательный handover реализации; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-099` | external_acceptance | Отделить самостоятельную приёмку от авторской; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `PW-100` | aggregate | Закрыть всю новую концепцию, а не только первый пилот; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b18"></a>
## B18 — Бытовые механики и исследование

Один конкретный механизм с настоящей ролью; событийная связь B15, сохранённый результат B04, геометрия B03.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-EX00` | aggregate | §13 EX00: Общий контракт новых действий; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX00/E01` | engineering | Согласовать interaction availability/presentation gate с kernel refusal; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX00/E02` | engineering | Согласовать custody/world.props и сохранение новых действий без второго state owner; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX01` | aggregate | §13 EX01: Поднимать, переносить, ставить; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX01/E01` | engineering | Перенести/поставить три выбранных класса вещей с опорой, отказом и save/load; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02` | aggregate | §13 EX02: Несколько решений одной задачи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02/E01` | engineering | Пройти side-panel двумя альтернативами с единственным результатом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02/E02` | engineering | Пройти upper-latch двумя альтернативами с единственным результатом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02/E03` | engineering | Выбрать и подключить третий конкретный эпизод с двумя естественными решениями; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02/SIDE-PANEL-HIST` | engineering | Проверить альтернативы side-panel в историческом дворе; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX02/UPPER-LATCH-HIST` | engineering | Проверить альтернативы upper-latch в историческом дворе; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX03` | aggregate | §13 EX03: Многоцелевые инструменты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX03/E01` | engineering | Связать шест с side-panel и high-shelf target, сохранять оба результата; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX03/E02` | engineering | Связать лопату с двумя конкретными полезными target действиями; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX03/POLE-HIST` | engineering | Связать шест с side-panel и high-shelf в историческом дворе; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX04` | aggregate | §13 EX04: Ограниченные сочетания; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX04/E01` | engineering | Связать шест+крючок с атомарной сборкой/разборкой и custody; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX04/E02` | engineering | Связать доску+две опоры с размещением и отказом удаления под лампой/игроком; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX04/POLE-HOOK-HIST` | engineering | Сохранить атомарную сборку шест+крючок в историческом дворе; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX04/BOARD-SUPPORT-HIST` | engineering | Сохранить доску на двух опорах и защитить занятую опору; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX05` | aggregate | Объёмный снег с опорой и следами на реальной поверхности; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX05/E01` | engineering | Согласовать следы на грунте с фактической поверхностью; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX05/E02` | engineering | Согласовать объёмный сугроб/расчистку прохода и сохранённое открытие; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX06` | aggregate | §13 EX06: Переносной свет; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX06/E01` | engineering | Связать lantern hand/ground и occlusion с porch-paint-tin/entry-wall-joint; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX06/E02` | engineering | Пройти две метки, отказ за стеной, повтор и save/load/NewGame в текущей кампании; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX06/LANTERN-HIST` | engineering | Пройти lantern hand/ground/occlusion и две точные метки; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX07` | aggregate | §13 EX07: Тепло/холод/материал; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX07/E01` | engineering | Согласовать warm-water→window-thaw/cabinet-thaw и расход/сброс локального тепла; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX07/THAW-HIST` | engineering | Связать warm-water с window-thaw/cabinet-thaw и расходом тепла; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX08` | aggregate | §13 EX08: Поиск и проверка звуком; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX08/E01` | engineering | Связать source cause ставни с caption/ремонтом/quiet state; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX08/E02` | engineering | Связать source cause полой доски с caption/ремонтом/quiet state; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX08/E03` | engineering | Выбрать и подключить третий звуковой эпизод с причинным действием и persistent результатом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX09` | aggregate | §13 EX09: Человеческая вертикальность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX09/E01` | engineering | Пройти лестницу вверх/вниз с защитой занятой опоры; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX09/E02` | engineering | Пройти отдельный низкий проход туда/обратно с фактической капсулой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX10` | aggregate | §13 EX10: Небольшие самостоятельные места; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX10/E01` | engineering | Пройти три разных small spaces: вход→локальное действие→польза→выход→repeat/save; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX11` | aggregate | §13 EX11: Микрозагадки на странность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX11/E01` | engineering | Пройти три конкретных observe-* странности: неверная/верная проверка→обоснованное открытие; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX12` | aggregate | §13 EX12: Найти место по фото/рисунку; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX12/E01` | engineering | Пройти два ImageDiscoveries сопоставления с ориентирами, неверным/верным отличием и save/load; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX13` | aggregate | Связать существующие действия в проверяемый цикл исследования; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX13/E01` | engineering | Пройти первую yard-service-gap цепочку и сохранить фактическое движение/результат; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX13/E02` | engineering | Подключить и пройти вторую отдельную цепочку исследования с достижимой пользой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX13/YARD-CHAIN-HIST` | engineering | Пройти служебную петлю двора47.51м обычным вводом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX14` | aggregate | §13 EX14: Устойчивый результат; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX14/E01` | engineering | Восстановить clearing/light/world.props и осведомлённую реакцию NPC; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX14/E02` | engineering | Восстановить изменённую звуковую причину после возврата/save/load/NewGame; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX14/CONSEQUENCES-HIST` | engineering | Сохранить clearing/lantern knowledge и ask-porch-paint; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX-ACCEPT` | engineering | §13.18: единый учёт приёмки EX01–EX14; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `ACT1-EX-ACCEPT/G-LISTENING` | external_acceptance | Подтвердить listening для «§13.18: единый учёт приёмки EX01–EX14»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b17"></a>
## B17 — Ограниченный объём с отдельным разрешением

Навигационная группа, не назначение на текущую игру. Действующие exclusion/separate_authorization сохраняются.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `ACT1-TAMARA-FENCE` | conditional | Забор Тамары Геннадьевны (необязательный квест); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b19"></a>
## B19 — Цельность игрового опыта — аудит 10.10.2026

Ранние техническая и человеческая приёмки E; F01–F14/J01–J12/V01–V18/§10 сведены в requirements_integration_2026_10_10. Production и полная C остаются B01–B16/B18; никаких дублей систем или статусов.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `UXA-001` | aggregate | Принять ранний цельный эпизод новой кампании обычным управлением; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `UXA-001/READY` | integration_check | Свести exact candidate E: NewGame/PhotoId/AnchorId/W01 route; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `UXA-001/ROUTE` | integration_check | Пройти E обычным вводом: фото→книга→вход→действие→возврат→следующий шаг; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `UXA-001/CONTINUE` | integration_check | Сохраниться внутри E, полностью закрыть игру и Continue к безопасному выходу; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `UXA-002` | external_acceptance | Проверить понимание и интерес раннего эпизода новым игроком; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |

<a id="b20"></a>
## B20 — URMAN Studio — ИИ и визуальный редактор игры

Отдельный продукт macOS; текущий запрос — только импорт ТЗ. 45 единиц в существующей STUDIO, не новые дублирующие карточки. Не условие REL-003/PW-100; остальные разрешения не расширяются.

| ID | Тип | Конечный результат / ссылка |
|---|---|---|
| `STUDIO` | aggregate | URMAN Studio — визуальный редактор игры по ТЗ 2026-09-27 (зонтик); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-01` | engineering | Актуальный контракт встроен в существующую очередь без потери назначений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-02` | aggregate | Автор открывает поручение из выбранного объекта и возвращается к прежнему вводу/камере; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-02/MODAL` | engineering | Подключить frozen AI-панель и маршрутизацию modal/shortcut без захвата текстового Undo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-02/FINISH` | integration_check | Автор открывает поручение из выбранного объекта и возвращается к прежнему вводу/камере; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-03` | aggregate | Видимый дом/окно однозначно разрешаются в исходную сущность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-03/IDENTITY` | engineering | Связать semantic window IDs и source locator с выбранным authored домом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-03/FINISH` | integration_check | Видимый дом/окно однозначно разрешаются в исходную сущность; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-04` | aggregate | Для выбранного дома зафиксирована полная цепочка окна и границы изменения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-04/SOURCE` | engineering | Зафиксировать цепочку окон дома recipe→генератор→GLB→экстерьер/интерьер; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-04/FINISH` | integration_check | Для выбранного дома зафиксирована полная цепочка окна и границы изменения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-05` | aggregate | Поручение включает новый/грязный объект и точный scope без лишних данных; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-05/EXPORT` | engineering | Экспортировать bounded контекст с full Git revision и понятным frontier; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-05/FINISH` | engineering | Поручение включает новый/грязный объект и точный scope без лишних данных; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-06` | aggregate | У каждого автора виден реальный безопасный статус своего провайдера; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-06/CODEX-PREFLIGHT` | engineering | Показать фактический installed/login/status Codex отдельно от Claude; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-06/FINISH` | engineering | У каждого автора виден реальный безопасный статус своего провайдера; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-07` | aggregate | Codex получает Request и возвращает структурированный Proposal прямо в Studio; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-07/CODEX-TURN` | engineering | Получить строгий Proposal из owned Codex-запроса без tools/записи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-07/FINISH` | engineering | Codex получает Request и возвращает структурированный Proposal прямо в Studio; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-08` | engineering | Claude выполняет тот же цикл без другого формата данных или обязательного API key; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-09` | aggregate | Размер окна изменяется в настоящем проёме, экстерьере и интерьере; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-09/CONSUMERS` | engineering | Применять индивидуальную ширину окна в exterior и interior consumer; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-09/FINISH` | integration_check | Размер окна изменяется в настоящем проёме, экстерьере и интерьере; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-09/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Размер окна изменяется в настоящем проёме, экстерьере и интерьере»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-10` | aggregate | Одна проверенная группа правок применяется/отменяется без порчи чужих изменений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-10/FILE-TXN` | engineering | Применять file-transaction с CAS, журналом, rollback и конфликтом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-10/FINISH` | engineering | Одна проверенная группа правок применяется/отменяется без порчи чужих изменений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-11` | aggregate | Модель и данные видны согласованно; Undo возвращает оба; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-11/EIGHT-WINDOWS` | engineering | Доставить Codex-пилот всех восьми окон с игровыми кадрами и byte-exact Undo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-11/FRONT0` | engineering | Доставить Codex-пилот Front0 с неизменными соседями и byte-exact Undo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-11/FINISH` | integration_check | Модель и данные видны согласованно; Undo возвращает оба; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-12` | aggregate | Первый сквозной результат: дом → фраза → агент → Studio/игра → Undo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-12/CODEX-UI` | integration_check | Пройти native Studio: выбрать Front0 → Codex → preview → Apply → игра → Undo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-12/CLAUDE-UI` | integration_check | Пройти тот же оконный цикл в Studio через Claude; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-12/CODEX-RESTART` | integration_check | Перезапустить Studio после Codex Apply и восстановить durable Undo/Redo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-12/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Первый сквозной результат: дом → фраза → агент → Studio/игра → Undo»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-13` | aggregate | Автор выбирает действующую кампанию и мир, а не зашитый старый demo; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-13/CATALOG` | engineering | Подключить readonly каталог кампаний/сцен и authoring state без второго registry; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-13/SELECTOR` | engineering | Подключить selector кампании/мира и write boundary активного контекста; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-13/TWO-WORLDS` | integration_check | Проверить два independently authorable runtime мира и изолированное редактирование; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-14` | engineering | Массовое поручение имеет обозримый фиксированный охват и исключения; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-15` | engineering | Выбранный участок ограды выравнивается без сдвига калитки и соседних секций; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-16` | engineering | Дорожку можно сгладить на выбранном отрезке с сохранением присоединений; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-17` | engineering | Область рельефа/растительности редактируется воспроизводимо и не перекрывает проходы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-18` | engineering | Автор меняет атмосферу и существующий звуковой источник с понятной областью действия; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-18/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Автор меняет атмосферу и существующий звуковой источник с понятной областью действия»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-18/G-LISTENING` | external_acceptance | Подтвердить listening для «Автор меняет атмосферу и существующий звуковой источник с понятной областью действия»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-18/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Автор меняет атмосферу и существующий звуковой источник с понятной областью действия»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-19` | engineering | ИИ меняет шаг/развилку квеста через ту же модель, что ручной список и граф; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-20` | engineering | Реплика, ответ и условие диалога меняются без потери локализации и связей; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-20/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Реплика, ответ и условие диалога меняются без потери локализации и связей»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-21` | engineering | Квест, диалог и триггер связываются из выбранной области мира; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-22` | engineering | Маршрут, занятие и анимация NPC редактируются и сохраняются без потери сюжетной логики; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-23` | engineering | Раздел предметов становится настоящим редактором, а не заглушкой; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-24` | engineering | Книга, улики, язык и сообщения ПК редактируются в актуальном контентном контуре; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-25` | engineering | Параметры существующих магазинов, транспорта и головоломок доступны без ручного JSON; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-26` | engineering | Настройки интерфейса игры имеют ручной/AI preview и не меняют тему Studio; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-26/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Настройки интерфейса игры имеют ручной/AI preview и не меняют тему Studio»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-27` | engineering | Форму простого предмета можно изменить внутри Studio и увидеть в игре; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-27/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Форму простого предмета можно изменить внутри Studio и увидеть в игре»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-28` | engineering | Персонаж из подготовленной базы получает изменяемое лицо/одежду и сохраняет рабочий rig; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-28/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Персонаж из подготовленной базы получает изменяемое лицо/одежду и сохраняет рабочий rig»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-29` | aggregate | Чужие правки и сбой посреди записи не уничтожают ни одну сторону; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-29/DRAFTS` | engineering | Согласовать guarded SaveAll/drafts/reorder recovery с session history; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-29/FINISH` | engineering | Чужие правки и сбой посреди записи не уничтожают ни одну сторону; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-30` | aggregate | Контекст и результат переносятся между компьютерами и импортируются тем же безопасным путём; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-30/MANUAL` | engineering | Импортировать clipboard/file Proposal через тот же strict parser/staging; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-30/LEGACY-PREVIEW` | engineering | Читать legacy context ZIP только как ограниченный readonly preview; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-30/FINISH` | engineering | Контекст и результат переносятся между компьютерами и импортируются тем же безопасным путём; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-31` | engineering | Новичок выполняет основные действия через интерфейс и подсказки без знания файлов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-31/G-REAL_PARTICIPANT` | external_acceptance | Подтвердить real_participant для «Новичок выполняет основные действия через интерфейс и подсказки без знания файлов»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-32` | aggregate | Режим разработки отделён от обычного редактора; секреты, saves и источники защищены; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-32/JOB-METADATA` | engineering | Записывать только валидированные metadata и свежие temp stages; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-32/FINISH` | engineering | Режим разработки отделён от обычного редактора; секреты, saves и источники защищены; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-33` | aggregate | Полный продукт имеет подтверждённую техническую приёмку и отдельно перечисленные внешние гейты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-34` | engineering | Катсцена выбирается из активной кампании, а список и шкала редактируют её реальные действия; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-35` | conditional | При подтверждённой необходимости Кадыр использует Windows Studio с тем же контрактом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-36` | engineering | Материал или модельный ресурс можно изменить локально, видя все общие использования; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-36/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Материал или модельный ресурс можно изменить локально, видя все общие использования»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-36/G-LANGUAGE_CULTURE` | external_acceptance | Подтвердить language_culture для «Материал или модельный ресурс можно изменить локально, видя все общие использования»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-36/G-LICENSE_DECISION` | external_acceptance | Подтвердить license_decision для «Материал или модельный ресурс можно изменить локально, видя все общие использования»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-37` | aggregate | Старые сложные сценарии квестов не теряются за первым простым AI-примером; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-37/RULE-CATALOG` | engineering | Показывать readonly покрытие существующего каталога quest rules; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-37/FINISH` | engineering | Старые сложные сценарии квестов не теряются за первым простым AI-примером; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-38` | engineering | Авторы получают, сравнивают, объединяют и отправляют ревизии через UI без ручного Git; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-39` | aggregate | Автор создаёт именованную контрольную версию и восстанавливает её без поиска файлов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-39/CHECKPOINT-SOURCE` | engineering | Подключить именованные source checkpoints с preview и защищённым Restore; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-39/RAM-HISTORY` | engineering | Показывать текущий Undo/Redo stack и запускать существующие callbacks; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-39/FINISH` | integration_check | Автор создаёт именованную контрольную версию и восстанавливает её без поиска файлов; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-40` | engineering | Пробный запуск объясняет несрабатывание и позволяет пройти следующий логический шаг; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-41` | engineering | Лицо редактируется скульптурными кистями в Studio, а не только выбором готового варианта; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-41/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «Лицо редактируется скульптурными кистями в Studio, а не только выбором готового варианта»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-42` | engineering | Автор создаёт элемент одежды из основы или поверхности и проверяет посадку в движении; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-43` | engineering | Новый персонаж из заготовки/геометрии получает совместимый скелет, веса и сокеты; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-44` | engineering | Генерируемый участок можно сделать авторским без двух владельцев transform; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO/AI-45` | engineering | Дом перемещается и редактируется вместе с интерьером, дверьми и входными точками; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X1` | engineering | Эксперимент: перенос участка мира из C# в авторские данные; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X2` | engineering | Эксперимент: встроенное моделирование (Godot-меш + Blender 4.5 за интерфейсом); [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X3` | engineering | Эксперимент: единая модель квеста для списка и графа с развилками; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.X4` | engineering | Эксперимент: семантическое объединение ревизий по сущностям и полям; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P0` | aggregate | P0: кликабельный прототип пяти экранов и технические границы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P0/E01` | engineering | Подключить пять экранов прототипа: мир/триггер+квест/список+граф/монтаж/конфликт с empty/error/draft; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P1` | aggregate | P1: один сквозной рабочий участок — карта и квест вместе; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P1/E01` | engineering | Подключить оболочку Studio и атомарные черновики с внешним конфликтом; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P1/E02` | engineering | Подключить ручное перемещение и каталог к перенесённым runtime данным; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P1/E03` | engineering | Подключить ручной список/граф квеста и триггер карты с двумя исходами; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P1/E04` | engineering | Подключить «Играть отсюда» к изолированной ревизии контента; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P2` | aggregate | P2: вся деревня и каталог; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P2/E01` | integration_check | Пройти WORLD editor A02/A03/A08/A09: placement, house/interior, terrain/road/catalog; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P3` | aggregate | P3: полная логика квестов, диалоги, NPC, анимации, сцены; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P3/E01` | integration_check | Пройти QUEST/SCENE editor A04/A05/A10–A15: branches, dialogue, NPC, timeline, legacy data; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P4` | aggregate | P4: остальные игровые редакторы; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P4/E01` | integration_check | Пройти десять existing editor sections §14 с real game consumer по A11/A22; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P5` | aggregate | P5: встроенное моделирование, лица, одежда, персонажи; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P5/E01` | integration_check | Пройти пять MODEL07 операций в Studio и импорт каталога/игры по A21; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P6` | aggregate | P6: совместная работа, восстановление, передача контекста, окончательная приёмка; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P6/E01` | integration_check | Пройти COLLAB/SAVE A17–A20/A24 на exact candidate, human A01/A15/A23/A25 отдельно; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
| `STUDIO.P6/G-AUTHOR_ART` | external_acceptance | Подтвердить author_art для «P6: совместная работа, восстановление, передача контекста, окончательная приёмка»; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |
