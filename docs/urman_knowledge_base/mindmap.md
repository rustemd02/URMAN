# Mindmap

```mermaid
mindmap
  root((УРМАН))
    Core [NARRATIVE]
      Жанр
        Камерный этно-хоррор
        Мистический детектив
        Interface investigation
      Тон
        Деревенская достоверность
        Медленная мистика
        Молчание и несостыковки
        Напряжённые паузы и звук
        Неполно объяснённые правила
      Темы
        Память
        Корни
        Татарский язык [LANG]
        Долг
        Цена правды
      Уникальность
        Татарский культурный код
        Язык как ключ к памяти [LANG]
        Сосуществование вместо охоты на монстров
    MVP [MVP]
      Завязка [MVP][NARRATIVE]
        Айдар возвращается в Кырлай
        Без отдельного КФУ-интро
        Дед болеет
        Марат не отпускает
      Персонажи [MVP][ASSET]
        Айдар
        Бабай
        Әби
        Алсу
        Тимур хәзрәт
        Ринат
        Наиля
        Разиля
      Первая загадка [MVP][NARRATIVE]
        Версии смерти Марата не сходятся
        Могила
        Медпункт
        Архив
      Клиффхэнгер [MVP][NARRATIVE]
        Не просто деревенская тайна
        Реальная система сосуществования
        Кромка Кара-Урмана
        Голос Марата
        Ринат: «Не отвечай»
      Completion Handoff [MVP][TECH]
        mvp_completion_handoff.md
        P0 shared evidence chain
        playtest_plan.md
      Chapter 1 Campaign Lock [MVP][NARRATIVE]
        chapter1_mvp_campaign.md
        Road arrival to home
        Alsu route guide
        Old PC official death doc
        FAP and Rinat contradiction
        Tatarwiki re-read
        Timur moral safe zone
        Evening route past zirat
        Kara-Urman edge cliffhanger
        Hint before finale not rule
        Rinat confirms Не отвечай
      Игровой цикл [MVP][OPEN][RISK]
        Найти ключ
        Проверить у NPC
        Найти документ
        Вернуться с новым смыслом
    Narrative [NARRATIVE]
      Айдар
        Свой по крови
        Чужой по правилам
        Не избранный маг
      Марат
        Друг детства
        Трагический двойник
        Винтовка и лес
      Бабай
        Мансур
        Администратор тайны
        Сломанная преемственность
      Әби
        Гөлсинә
        Тихий архив
        Обучает Алсу
      Алсу
        Проводник
        Ложное подозрение Су Анасы
        Будущее Кырлая
      Тимур хәзрәт
        Safe zone мечети
        Исламская рамка
        Моральная опора
      Ринат
        Участковый
        Закрывает дело Марата
        Знает практические правила опасности
      Совет деревни
        Старшие семьи
        Дозированный доступ
        Контроль чужаков
    Gameplay [MVP][OPEN][RISK]
      Static-node hybrid [MVP][TECH]
        Статичные сцены-узлы
        Route navigation [MVP][ASSET]
          Вид изнутри улицы
          Повороты на 90 градусов
          Фиксированный шаг вперёд
          Физические указатели
          Неполная схема в журнале
          Route graph pack generated [MVP][TECH]
          Runtime route scene implemented [MVP][TECH]
          32 route nodes
          41 route-map PNG assets
          Controlled animation metadata
          Editable sign and journal text regions
        Ограниченное перемещение
        Interface investigation
      Исследование [MVP]
        Дом
        Улица
        Медпункт
        Кладбище
        Кромка леса
      Диалоги через ключи [MVP][TECH]
        Факт
        Дата
        Имя
        Документ
        Татарское слово [LANG]
        Rinat first prototype [OPEN][MVP]
      Архивы [MVP][TECH]
        Поиск
        Документы
        Противоречия
      Татарский язык [MVP][LANG][RISK]
        Контекст
        Словарь
        Частичный перевод
      Мессенджеры [TECH]
        Ялкын
        Чаты жителей
      ПК бабая [MVP][TECH][ASSET]
        Главный документальный хаб [NARRATIVE]
        Runtime prototype implemented 2026-05-18 [TECH]
        Сюжетно-критичный доступ
        Доступен всегда из дома
        Безымянная Win98-like оболочка
        Mouse-driven desktop and windows
        Data-driven content loader
        Татарвики
        Архив
        Документы Марата
        Сохранённые сообщения
        Реестр домов и семей
        Нарушения и компенсации
        Кара-Урман
        Повреждённые файлы
        Gated-документы
        Local clue saving
        Journal integration gap [OPEN][TECH]
        Shared evidence bridge needed [MVP][TECH][RISK]
        Техническая метадата не clue
        Старые письма и бытовые папки ограниченно
        Внутренний учёт Кырлая
      Лес [NARRATIVE][ASSET]
        Запретные границы
        Шурале
        Звуки
        Правила понятны не полностью
      Safe zones [TECH]
        Дом
        Мечеть
    Language Learning [LANG][MVP][RISK]
      Татарские слова
        Урман
        Су
        Юл
        Өй
        Әби
        Бабай
        Ярамый
        Җавап
        Тавыш
        Шүрәле
      Контекст
        Русская речь с татарскими вставками
        Средняя плотность [MVP]
        Бытовые фразы
        Запреты
        Документы
      Диалоги
        Непонятые реплики
        Слова как dialogue keys
        Один татароязычный NPC [OPEN]
      Документы
        Частичные переводы
        Re-read старых улик
        Поиск по татарским словам
      Прогрессия [MVP][LANG]
        Stage 1 отдельные слова
        Stage 2 слова-запреты
        Stage 3 короткие формулы
        Stage 4 татароязычный NPC
      Механики перевода [TECH][OPEN]
        Vocabulary unlock
        Context guess
        Re-read old evidence
    Assets [ASSET][RISK]
      Static-node scenes [MVP][ASSET]
      Selective object interfaces [MVP][TECH]
      Ink-wash storybook style [MVP][ASSET]
        Тушевая линия
        Приглушённая акварель
        Бумажная фактура
        Обычность сначала
        Неправильность потом
      Asset Inventory 50 [MVP][ASSET]
      First generated asset batch [MVP][ASSET]
        Айдар portrait set
        Main street route screen
        Old PC frame
        Medical record template
        Kara-Urman forest edge pressure screen
      Remaining asset pack [MVP][ASSET][RISK]
        Visual coverage generated
        179 PNG assets
        Manifest-driven coverage for 50 rows
        Editable text metadata
        Controlled animation metadata
        Audio row #50 still separate [RISK]
      Controlled animation slots [MVP][ASSET][TECH]
        Static PNG base
        Overlay masks
        Window light pulse
        CRT glow
        Birds and dust
        Branch/grass drift
        Pressure overlays
      Village route kit [MVP][ASSET]
        Route segments
        Facing views
        Turn and step transitions
        Diegetic signs
        Generated route-map integration pack
        Runtime scene consumes graph and metadata
      Art reference Искатель [ASSET]
      Персонажи [MVP]
      Локации [MVP]
      UI [MVP]
      Звук [MVP]
      Музыка
      Документы [MVP]
      Фото [MVP]
      Иконки
    Lore [NARRATIVE]
      Кырлай
        62 жителя
        21 домохозяйство
        Основание после 1552
      Кара-Урман
        Запретное урочище
        Старое place-name
        Не название деревни
      Пакт
        Молчание
        Границы
        Компенсация
      Шурале
        Лесной народ
        Не один монстр
      Су Анасы
        Вода
        Гребень
        Баранов 1967
      Бичура
        Домашний уклад
      Тукай
        1913 [RISK]
        Спорная архивная линия
      1967
        Баранов
        Плёнка
        Утопление
      История Марата
        Дневник
        Письма наружу
        Ложные версии смерти
    Technical [TECH]
      Modular migration complete [MVP][TECH]
        Portable JSON Markdown content
        CampaignManifest composition
        RuntimeKernel single state writer
        Immutable claim query before capability teardown
        Capability providers for unique mechanics
        Campaign fingerprint locked per run
        V2 persistence gateway in production boot [TECH]
          Exact v1 reset with one-time notice
          Incompatible V2 requires explicit reset
          Username, preferences and Content Lab stay separate
        Content Lab dev-only
        Authoring guide for LLMs [TECH]
          Data-only quests, dialogues, roles and assets
          Content check and isolated Lab runs
        Legacy owners retired after migration
        Unowned DedOS chat retired; archive remains sole narrative PC owner
        Atomic scene→dialogue handoff commits start effects once [TECH][NARRATIVE]
        Architecture ready to build MVP, not MVP release [MVP][RISK]
        Task packet docs/modular_migration
      Data model
        Characters
        Locations
        Clues
        Documents
        Vocabulary
      Shared Evidence Chain [MVP][TECH][RISK]
        Old PC clue
        Knowledge key
        Journal card
        Dialogue reaction
        Vocabulary re-read
        Pressure state
        Cliffhanger route
      Quest system [MVP]
      Dialogue system [MVP]
      Knowledge keys [MVP]
      Inventory
      Save system [MVP]
      Localization [LANG]
      Clue graph [MVP]
      Old PC authoring [MVP][TECH]
        Markdown плюс frontmatter
        content/old_pc
        10 runtime files
        Reliability status
        Canon status
        Search index
        Validator
    Risks [RISK]
      Сюжет
        Слишком большой лор
        Слабый первый крючок
      Геймплей [OPEN]
        Minute-to-minute не доказан
        Прототипы не связаны в один loop
        Old PC clues локальны
        Journal dialogue vocabulary pressure save/load раздельны
      Ассеты [ASSET]
        Нет точного минимума
        Visual pack ahead of runtime
        House Mosque Forest still placeholders
      Scope creep [MVP]
        Полная игра вместо среза
        Тьюринг-полный ПК до проверки MVP loop
        ПК как отдельная ОС вместо document hub
        Универсальный движок вместо минимальных capability contracts
      Cultural accuracy
        Татарский фольклор
        Ислам
      Татарский язык [LANG]
        Риск учебника
        Риск декорации
```

## Core

Core фиксирует идентичность игры: УРМАН — не «хоррор в деревне», а мистический детектив о языке, памяти, корнях и системе молчания. Любая новая механика должна усиливать эту формулу.

## MVP

MVP — вертикальный срез. Он обязан показать прибытие Айдара, тревожную деревню, первые следы Марата, ключи в диалогах, старый ПК как главный документальный хаб, татарский как инструмент понимания и клиффхэнгер о старом порядке.

## Narrative

Narrative держит эмоциональный двигатель. Марат — человеческий крючок, бабай и әби — семейный долг, Алсу — проводник в будущее Кырлая, Тимур хәзрәт — моральная и религиозная рамка.

## Gameplay

Gameplay пока остаётся рискованной зоной. Есть сильная идея ключей, старого ПК, архивов и повторного прочтения данных, но нужно прототипом доказать, что игроку интересно каждую минуту, а не только читать документы.

## Language Learning

Татарский язык должен открывать смысл, а не быть украшением. Игрок учит слова через контекст, возвращается к старым репликам и документам и видит больше, чем видел раньше.

## Assets

Ассеты — один из главных производственных рисков. MVP должен по максимуму использовать интерфейсы, документы, портреты, звук и ограниченные локации вместо дорогой полной постановки. Общий визуальный стиль принят: ink-wash storybook / тушь + приглушённая акварель, с обычной деревней на первом плане и постепенной неправильностью вместо гиперреализма.

## Lore

Лор строится вокруг Кырлая, пакта и существ как старой системы сосуществования. Существа не должны становиться простыми врагами.

## Technical

Техническая архитектура должна быть data-driven и engine-neutral: квесты, диалоги, clues, документы, vocabulary, состояние деревни и clue graph.

## Risks

Риски надо не сглаживать, а превращать в задачи: gameplay prototype, asset minimum, татарский mechanic prototype, cultural review и вертикальный сценарий MVP.
