# Open Questions

## Gameplay

- Question: Какой основной формат gameplay для MVP?
  Context: Есть варианты classic Zelda-like, isometric, interface-heavy и hybrid.
  Why it matters: От этого зависят ассеты, камера, перемещение, UI и объём разработки.
  Status: Resolved 2026-05-16 — static-node hybrid investigation.
  Decision: Статичные / слегка анимированные сцены-узлы + in-world route navigation по деревне + ограниченное перемещение + глубокие интерфейсные расследования.
  Suggested next step: Сделать 15-минутный greybox prototype в этом формате.
  Priority: High

- Question: Что игрок делает каждую минуту?
  Context: Core loop описан, но не доказан прототипом.
  Why it matters: Игра может превратиться в чтение документов без напряжения.
  Status: Resolved 2026-05-16 — main repeatable action is detective loop.
  Decision: Игрок находит противоречия, фиксирует clues, применяет знание в диалогах / архиве / документах и возвращается к старым данным с новым смыслом.
  Suggested next step: Сценарий первого playable loop: дом → кладбище → архив → Ринат → cliff clue.
  Priority: High

- Question: Как деревня реагирует на опасные ключи?
  Context: Есть идея notify council / suspicion, но нет правил.
  Why it matters: Без реакции мира расследование будет статичным.
  Status: Resolved 2026-05-16 — scripted pressure flags + light 0–3 pressure level.
  Decision: Опасные ключи могут менять реплики NPC, запускать семейные / council reactions, менять доступность узлов и усиливать ambience, но не превращают MVP в сложную симуляцию.
  Suggested next step: Описать первые pressure flags для Рината, бабая, Алсу и council attention в сценарии первого loop.
  Priority: Medium

- Question: Как игрок перемещается по деревне?
  Context: Полная карта сверху слишком рано раскрывает масштаб Кырлая; свободная изометрия слишком дорогая для принятого ink-wash MVP.
  Why it matters: Формат перемещения определяет локационные ассеты, камеру, UI, темп и ощущение тайны.
  Status: Resolved 2026-05-16 — in-world discrete route navigation.
  Decision: Использовать вариант A из pitch: игрок видит текущий участок дороги, делает фиксированный шаг вперёд, поворачивается на 90 градусов и ориентируется по физическим указателям / landmarks. Неполная схема маршрутов может жить в журнале, но не заменяет перемещение.
  Suggested next step: Сделать greybox route prototype: главная улица → поворот к дому → указатель к зирату → кромка Кара-Урмана, с turn/step transitions.
  Priority: High

## Story

- Question: Имя героя окончательно Айдар или Айрат?
  Context: Ранний текст использует Айрат, нормализованный канон — Айдар.
  Why it matters: Нужны стабильные IDs, UI и сценарий.
  Status: Resolved 2026-05-16 — Айдар is canon.
  Decision: Использовать Айдара в сценарии, UI и data IDs. Айрат оставить только как старый alias / legacy reference.
  Suggested next step: Удалять активные упоминания Айрата из production docs, если они всплывают вне разделов про alias.
  Priority: High

- Question: Что делать с Кара-Урманом?
  Context: Ранний текст использует Кара-Урман, поздний лор — Кырлай.
  Why it matters: Название локации влияет на бренд и лор.
  Status: Resolved 2026-05-16 — Кырлай is village canon; Кара-Урман is forbidden forest tract / old place-name.
  Decision: Не использовать Кара-Урман как название деревни. Использовать как старое локальное название тёмной части леса / запретного урочища у границы Кырлая.
  Suggested next step: Ввести Кара-Урман через предупреждения жителей, старую карту или архивную подпись, а не как отдельный населённый пункт.
  Priority: Medium

- Question: Как зовут бабая?
  Context: Ранний список даёт Мансур, поздний лор имя не закрепляет.
  Why it matters: Персонаж центральный, имя нужно для сценария и ассетов.
  Status: Resolved 2026-05-16 — Мансур is canon.
  Decision: Использовать Мансура как имя бабая; `char_babay` остаётся стабильным data ID. Фамилию пока не фиксировать.
  Suggested next step: Обновлять production text на «Мансур» / «Мансур бабай» там, где нужно display name.
  Priority: High

- Question: Какой финальный cliffhanger MVP?
  Context: Есть формула, но нет конкретной сцены.
  Why it matters: MVP должен закончиться сильным доказательством.
  Status: Resolved 2026-05-16 — edge of Кара-Урман / Марат's voice / Ринат says «Не отвечай».
  Decision: Detective trail выводит Айдара к кромке Кара-Урмана. Он слышит голос Марата из леса; Ринат появляется и останавливает его короткой фразой «Не отвечай», раскрывая, что знает практические правила опасности.
  Suggested next step: Написать beat sheet сцены без монолога Рината: путь к кромке, звуковая пауза, голос Марата, вмешательство Рината, hard cut.
  Priority: High

- Question: Какая личная фраза Марата звучит в финале?
  Context: `chapter1_mvp_campaign.md` требует, чтобы голос у кромки использовал детскую / личную деталь, показанную раньше через фото или сообщение.
  Why it matters: Без этой фразы финал станет generic «голос зовёт из леса». С ней игрок хочет ответить эмоционально, а не потому что игра просит нажать кнопку.
  Status: Partially resolved 2026-05-23 — temporary production default in `chapter1_mvp_campaign.md`.
  Decision: Use «Казанский, не отставай» as the temporary personal marker: it appears in an early photo/message and returns in the final voice with a wrong pause. This can be replaced during dialogue polish, but the final scene must not be left without a personal marker.
  Suggested next step: Написать 3–5 более тонких вариантов фразы Марат / Айдар during dialogue polish and keep or replace the temporary default. Any татарская or русско-татарская form still needs consultant review.
  Priority: High

## MVP

- Question: Включать ли КФУ-интро?
  Context: Оно хорошо задаёт контраст, но может удлинить MVP.
  Why it matters: Сразу влияет на объём сцен и ассетов.
  Status: Resolved 2026-05-16 — no standalone KFU intro in MVP.
  Decision: MVP starts at or near arrival in Кырлай. Казань / КФУ контраст можно оставить только лёгким фоном через сообщения, диалог или журнал, без отдельной playable сцены.
  Suggested next step: Beat sheet MVP should open with arrival / road / house, not campus.
  Priority: Medium

- Question: Включать ли линию вырубки в MVP?
  Context: Ранний синопсис строился вокруг вырубки, поздний MVP — вокруг Марата.
  Why it matters: Вырубка может перегрузить первый срез.
  Suggested next step: Оставить как ложный документальный след, не как главный сюжет MVP.
  Priority: High

## Assets

- Question: Какой минимальный визуальный пакет нужен для MVP?
  Context: Список ассетов широкий, но production capacity неизвестна.
  Why it matters: Ассеты — главный риск scope creep.
  Suggested next step: Утвердить MVP asset floor: 6–8 портретов, 6–8 локаций/экранов, 10–15 документов, UI.
  Priority: High

- Question: Какие сцены можно заменить интерфейсами?
  Context: ПК, документы и мессенджеры могут заменить катсцены.
  Why it matters: Это снижает стоимость MVP.
  Suggested next step: Пометить каждую MVP-сцену как spatial / interface / document / audio.
  Priority: High

## Visual Style

- Question: Top-down, isometric, interface-heavy или hybrid?
  Context: В источнике есть Zelda-like и Octopath-like идеи, но решения нет.
  Why it matters: Определяет производство.
  Status: Resolved 2026-05-16 — gameplay direction accepted as static-node hybrid; art direction accepted as ink-wash storybook.
  Decision: Для общего визуального стиля использовать тушь + приглушённую акварель / ink-wash storybook: обычная деревня, ручная линия, бумажная фактура, постепенная неправильность вместо гиперреализма и прямого monster horror.
  Suggested next step: Сделать первые production style frames: дом бабая и әби, портреты Айдара / Мансура / Алсу, old PC frame, кромка Кара-Урмана.
  Priority: High

- Question: Использовать ли uncanny portraits?
  Context: Идея сильная, но может быть дорогой или тонально грубой.
  Why it matters: Влияет на horror tone.
  Status: Partially resolved 2026-05-16 — subtle wrongness only.
  Decision: Портреты должны быть обычными и человеческими в базовом состоянии; uncanny использовать как редкое pressure-состояние, а не постоянный фильтр.
  Suggested next step: Протестировать 2–3 портрета в одной ink-wash линии: neutral / tension / partial reveal.
  Priority: Low

## Audio

- Question: Какая роль звука в первом мистическом следе?
  Context: MVP может не показывать существ полноценно.
  Why it matters: Звук может заменить дорогие VFX/монстров.
  Status: Direction accepted 2026-05-16 — напряжённые паузы, звук и неполно объяснённые правила важнее постоянных скримеров.
  Suggested next step: Спроектировать audio-first cliffhanger и ambience states для дома, улицы, кладбища, кромки леса.
  Priority: Medium

## Татарский язык

- Question: Какой первый playable татарский mechanic?
  Context: Есть варианты vocabulary unlock, context guess, archive re-read.
  Why it matters: Без этого язык останется темой, а не системой.
  Status: Resolved 2026-05-23 — real татарские words through русско-татарская mixed speech.
  Decision: Использовать среднюю плотность татарских вставок: сначала русская речь с отдельными татарскими словами, дальше татарского больше через повторение и узнавание. Не вводить пословицы, сложную грамматику и большие татарские монологи как обязательную механику MVP.
  Suggested next step: Прототип на 5–7 словах: бытовая сцена, документ / указатель, диалоговый ключ, re-read старой улики и короткий фрагмент татароязычного NPC без блокировки основного прогресса.
  Priority: High

- Question: Кто будет татароязычным NPC?
  Context: Принято, что один NPC может говорить только или почти только по-татарски как маркер прогресса понимания.
  Why it matters: Нужен персонаж, который не ломает основной прогресс и не превращает язык в стену.
  Suggested next step: Выбрать кандидата из MVP cast или ambient NPC: пожилой сосед, старая әби у окна, дед у кладбища, ребёнок-повторитель или другой локальный персонаж.
  Priority: Medium

- Question: Кто проверяет татарский язык?
  Context: Ошибки будут культурно заметны и вредны.
  Why it matters: Нужна точность и уважение.
  Suggested next step: Найти носителя / консультанта до production текста.
  Priority: High

## Technical

- Question: Какой формат данных выбрать?
  Context: Нужны characters, clues, dialogues, documents, vocabulary.
  Why it matters: Контент должен быть валидируемым и переносимым.
  Suggested next step: Начать с JSON + Markdown body, добавить schema validation.
  Priority: High

- Question: Как валидировать clue graph?
  Context: Ручные связи легко сломать.
  Why it matters: Детектив зависит от непротиворечивых связей.
  Suggested next step: Написать validator: orphan clues, missing sources, broken unlocks.
  Priority: Medium

## Modular migration follow-ups

- Question: Какие legacy ambient character rows реально входят в production Chapter 1 campaign?
  Context: MM-10 found `fanis`, `zarifa`, `ildar`, `gulnara`, `karat_guard`, `rashid`, `nail`, `rushania` and `razilya` in the old `CHARACTERS` array, but the accepted Chapter 1 active cast is narrower.
  Why it matters: Presence in a legacy array must not silently add NPCs, portraits, dialogue obligations or canon to the campaign.
  Status: Deferred, non-blocking for MM-20. MM-50 includes only explicitly referenced/accepted records; all others remain out of the campaign until a content decision.
  Suggested next step: During MM-50, report the explicit production character list and leave unreferenced ambient rows unmigrated rather than inventing roles.
  Priority: Medium

- Question: Should Су Анасы and Убыр receive future module IDs?
  Context: Legacy `characters.ts` models `shurale`, `su_anasy` and `ubyr` as ordinary character rows. Chapter 1 only needs Шүрәле as vocabulary/knowledge and must not turn mythological beings into mobs/NPCs.
  Why it matters: Assigning character IDs now would silently choose a future ontology and campaign scope.
  Status: Deferred; not part of MM-50 Chapter 1 migration. No new canon decision.
  Suggested next step: Define a mythology/lore module only when a future accepted campaign needs these entities.
  Priority: Low

- Question: Есть ли дополнительные user preference keys, которые должен сохранить MM-70?
  Context: MM-10 found `urman_username` but no other current preference keys. The exact four gameplay/UI keys are authorized for reset.
  Why it matters: The reset must preserve user choices without retaining gameplay state or deleting unrelated origin data.
  Status: Resolved 2026-07-18. Audit found only `urman_username`; reset deletes exactly four v1 gameplay keys and never performs broad clear. Unknown origin keys and the isolated Content Lab namespace remain untouched.
  Suggested next step: Classify any future preference key explicitly before adding it to reset behaviour; keep the named gateway as the sole storage owner.
  Priority: Low

## Production

- Question: Кто владелец canon updates?
  Context: Knowledge base должна жить, а не устареть после первого спринта.
  Why it matters: Документы быстро станут ложными, если их не обновлять.
  Suggested next step: Правило: любой сюжетный/геймплейный PR обновляет relevant KB files.
  Priority: High

## Cultural / Religious Accuracy

- Question: Как проверять исламскую рамку Тимура?
  Context: Тимур важен для культурного баланса.
  Why it matters: Нельзя сделать религию карикатурной или враждебной фольклору.
  Suggested next step: Консультация с религиозно грамотным татарским консультантом.
  Priority: High

- Question: Как подавать Тукая 1913?
  Context: Линия сильная, но рискованная.
  Why it matters: Прямая версия «его убили существа» может быть грубой.
  Suggested next step: Оставить как спорный архив / локальную интерпретацию, не hard canon.
  Priority: Medium
