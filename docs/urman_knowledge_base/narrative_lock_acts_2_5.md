# Narrative Lock — Acts 2–5

Дата: 2026-08-11  
Статус: **Accepted narrative lock**. Это lock для производства контента и сцен, а не заявление о готовности 3D-, audio- или cultural-review слоя.

## Назначение и границы

Этот документ закрывает production-пакет для актов 2–5 перед дорогими ассетами. Он дополняет `narrative.md`, не меняя hard canon из `canon.md` и `decision_log.md`.

- Акт 1 остаётся отдельным lock-файлом `chapter1_mvp_campaign.md` и заканчивается клиффхэнгером «Не отвечай».
- Акты 2–5 используют compiled campaign `urman.fullgame` и его namespaced IDs как источник истины для текущего Godot vertical slice.
- Финал один: Айдар осознанно разрушает несправедливый пакт; Кырлай теряет старую защиту и входит в опасное, но честное будущее.
- Локальные решения меняют доверие, порядок сведений, реплики, доступ к документам и постановку эпилога. Они не создают альтернативных концовок и не меняют факт разрушения пакта.
- Шүрәле, Су Анасы и другие существа не являются combat-мобами. Угроза выражается правилами, звуком, границей, погоней или необходимостью укрыться/уйти.
- Тимур хәзрәт остаётся моральной и культурной опорой. Исламская рамка не используется как антагонистический или декоративный слой.
- Исторические слои 1913/1552 — архивная память, документы и постановочные наложения; lock не утверждает буквальное путешествие во времени и не делает спорные интерпретации о Тукае hard canon.

## Сквозная драматургическая цепь

| Акт | Драматический вопрос | Вход | Выход | Главный сдвиг |
| --- | --- | --- | --- | --- |
| 2 | Почему у деревни две версии одной реальности? | `urman.fullgame:scene/act2-house` | `urman.fullgame:scene/act3-archive` | Айдар доказывает практическое существование пакта и понимает, что Алсу знала больше него. |
| 3 | Что Марат успел узнать и какую цену платит лес? | `urman.fullgame:scene/act3-archive` | `urman.fullgame:scene/act4-tukay` | Марат становится предупреждением, а не героическим мстителем; Су Анасы возвращает стёртое имя как долг памяти. |
| 4 | Откуда взялись наследственные роли и кто имеет право их продолжать? | `urman.fullgame:scene/act4-tukay` | `urman.fullgame:scene/act5-boundary` | Айдар видит, что пакт был одновременно защитой и несправедливой сделкой. |
| 5 | Можно ли прекратить защиту, не превращая правду в новую форму насилия? | `urman.fullgame:scene/act5-boundary` | `urman.fullgame:scene/act5-epilogue` | Айдар выбирает правду и принимает трагическую цену; старая защита прекращается. |

## Акт 2 — «Две книги деревни»

### Цель и эмоциональный поворот

Снять ложную гипотезу «все просто прикрывают человеческое преступление», но не выдавать мифологическое объяснение как готовый ответ. Айдар впервые видит, что бытовые правила деревни имеют практическое последствие, а Алсу является участницей системы, а не тайным существом.

### Beat sheet

1. **Дом после лесной границы** — `scene/act2-house`. Айдар возвращается к бабаю и әби уже с правилом «не отвечать» в голове. `knowledge/act2_village_double_system` получает статус `confirmed`; семейная сцена должна быть тёплой, но с паузой при имени Марата.
2. **Река с двумя именами** — `scene/act2-river`. Игрок проходит короткий ходибельный участок между домом и мечетью, видит две системы обозначений одного места и получает `knowledge/act2_alsu_knows_pact` как `hypothesis`.
3. **Двор мечети** — `scene/act2-mosque` + `dialogue/timur-counsel`. Тимур формулирует границу: вера не оправдывает сокрытие несправедливости, но и страх нельзя превращать в повод для гордого саморазрушения.
4. **Сход и две книги** — `scene/act2-council` + `dialogue/alsu-confession` + `dialogue/council-voice`. Алсу признаётся в знании практического правила; совет называет пакт защитой, не называя тех, кто платит. `knowledge/act2_practical_pact_proof` становится `confirmed`.
5. **Закрытие расследовательского шага** — `quest/act2-double-ledger` завершает `beat/act2-complete`, после чего переход ведёт в `scene/act3-archive`.

### Clue graph

```text
chapter1:knowledge/clue_do_not_answer_rule
  -> fullgame:knowledge/act2_village_double_system
  -> fullgame:knowledge/act2_alsu_knows_pact
  -> fullgame:knowledge/act2_practical_pact_proof
  -> fullgame:quest/act2-double-ledger
  -> fullgame:scene/act3-archive
```

`dialogue/timur-counsel` не раскрывает происхождение пакта и не завершает quest сам по себе. Он меняет только отношение/моральную рамку (`supports_truth_without_pride`).

### Производственная карта

- **Зоны:** `fullgame_act2_house`, `fullgame_act2_river`, `fullgame_act2_mosque`, `fullgame_act2_council`.
- **NPC:** Айдар, Мансур, Гөлсинә, Алсу, Тимур хәзрәт, члены совета как безымянная группа. Физические тела не владеют narrative state.
- **Документы/реквизит:** две книги деревни (публичная и внутренняя), семейная фотография/сообщение с личным маркером Марата, указатель к реке, двор мечети.
- **Языковые ключи:** переиспользуются `tt_urman`, `tt_yaramyy`, `tt_javap`, `tt_tavysh`, `tt_zirat`; новые обязательные ключи акта не добавляются до проверки носителем.
- **World variants:** `village.double-ledger`, `village.alsu-withholds`, `village.council-pressure-1`. Они меняют свет, ambient-паузы, доступные реплики и placement реквизита, но не карту переходов.
- **Threat beat:** `act2-river-two-names`. Короткое напряжение на воде: неверный ответ/шум вызывает звук и закрывает безопасный проход на несколько секунд. Нет боя, урона или системного stealth AI; выход всегда читаем.
- **Production dependencies:** дом с живыми следами быта, две разные книги, речной берег с локальным туманом, двор мечети без карикатурных символов, NPC-группа совета, приглушённые дневные/вечерние ambience states.

## Акт 3 — «Имя, которое стёрли»

### Цель и эмоциональный поворот

Показать Марата как человека, который увидел правду, но неверно понял способ сопротивления. Советский архив и Су Анасы соединяются через память о долге: лес не просит поклонения, он возвращает имя, которое люди вычеркнули.

### Beat sheet

1. **Архив после молчания** — `scene/act3-archive` + `dialogue/naila-record`. Наиля показывает, что в папке Баранова отсутствует страница. `knowledge/act3_baranov_1967` становится `hypothesis`, только если доказательство Акта 2 подтверждено.
2. **Папка Баранова, 1967** — `scene/act3-soviet` + `document/baranov-1967`. Документ связывает хозяйственную проверку, исчезновение страницы и раннюю попытку Марата вынести правило наружу. `knowledge/act3_baranov_1967` и `knowledge/act3_marat_warning` становятся `confirmed`.
3. **Кромка воды** — `scene/act3-suanasy`. Вода меняет звуковую перспективу; новое слово `tt_su_anasy` становится `confirmed`, а `knowledge/act3_su_anasy_debt` фиксирует долг имени.
4. **Голос Су Анасы** — `dialogue/suanasy`. Голос просит назвать стёртое имя. Он не объясняет весь пакт и не требует жертвы; игрок может слушать или отойти, но основная цепь не блокируется.
5. **Закрытие долга** — `quest/act3-water-debt` завершает `beat/act3-complete` и передаёт Айдара к тетради 1913 года.

### Clue graph

```text
fullgame:knowledge/act2_practical_pact_proof
  -> fullgame:knowledge/act3_baranov_1967 [hypothesis]
  -> fullgame:document/baranov-1967
  -> fullgame:knowledge/act3_marat_warning
  -> fullgame:vocabulary/tt_su_anasy
  -> fullgame:knowledge/act3_su_anasy_debt
  -> fullgame:quest/act3-water-debt
  -> fullgame:scene/act4-tukay
```

### Производственная карта

- **Зоны:** `fullgame_act3_archive`, `fullgame_act3_soviet`, `fullgame_act3_water`.
- **NPC/голоса:** Наиля как свидетель архива; Марат только через документ, звук и личные следы; Су Анасы — voice/presence без combat body; Тимур может появляться как безопасный follow-up, но не забирает сцену у Наили.
- **Документы/реквизит:** папка Баранова 1967, отсутствующая страница, металлический шкаф, вода/смытая надпись, предмет Марата без портретного reveal.
- **Языковые ключи:** `tt_boranov`, `tt_su_anasy` плюс повторное чтение `tt_urman`, `tt_tavysh`, `tt_javap`. Тексты не называют Су Анасы «монстром» и не превращают слово в spell.
- **World variants:** `archive.page-missing`, `archive.marat-trace`, `water.name-returned`, `water.debt-audio`. Они управляют доступностью света, мокрыми следами, звуковым слоем и journal-подсказками.
- **Threat beat:** `act3-water-name`. Граница воды отвечает на неправильное приближение/шум; игрок выбирает отступить или дождаться тишины. Никакого убийства существа и никакого fail-state, требующего боя.
- **Production dependencies:** модульный архив, hero prop папки/страницы, старый советский стол и ПК, wet-surface material, направленный звук воды, voice/caption/non-audio cue, отдельная проверка исторической формулировки Баранова.

## Акт 4 — «Наследство не равно согласию»

### Цель и эмоциональный поворот

Перевести расследование из «что случилось с Маратом» в «кто имеет право продолжать роль». Тукай и 1913 дают язык заботы, 1552 — историческую потерю и перемещение границы, а книга пакта впервые делает сделку проверяемым человеческим документом.

### Beat sheet

1. **Открытая тетрадь Тукая** — `scene/act4-tukay` + `document/tukai-1913` + `dialogue/tukay`. `knowledge/act4_tukay_1913` подтверждается; `tt_vasiyat` читается как «завещание/поручение», а не магическая формула.
2. **Год, когда граница сдвинулась** — `scene/act4-1552` + `document/kazan-1552`. `knowledge/act4_kazan_1552` подтверждается, `tt_amanat` сначала остаётся `guessed`, чтобы игроку требовалось сопоставление контекста.
3. **Книга пакта** — `scene/act4-pact` + `document/pact-ledger`. В документе видны роли, компенсации и подписи, но не «истинный голос леса». `knowledge/act4_inherited_roles` и `knowledge/act4_pact_origin` подтверждаются.
4. **Разговор с хранителем** — `dialogue/pact-keeper`. Мансур признаёт, что пакт защищал людей и одновременно закреплял наследственное молчание. `knowledge/act5_pact_unjust` становится доступным.
5. **Закрытие исторического шага** — `quest/act4-inherited-roles` завершает `beat/act4-complete`; переход ведёт к последней границе.

### Clue graph

```text
fullgame:knowledge/act3_su_anasy_debt
  -> fullgame:document/tukai-1913
  -> fullgame:knowledge/act4_tukay_1913
  -> fullgame:document/kazan-1552
  -> fullgame:knowledge/act4_kazan_1552
  -> fullgame:vocabulary/tt_amanat
  -> fullgame:document/pact-ledger
  -> fullgame:knowledge/act4_inherited_roles
  -> fullgame:knowledge/act4_pact_origin
  -> fullgame:quest/act4-inherited-roles
  -> fullgame:knowledge/act5_pact_unjust
```

### Производственная карта

- **Зоны:** `fullgame_act4_tukay`, `fullgame_act4_1552`, `fullgame_act4_pact`.
- **NPC:** Тимур хәзрәт как собеседник о языке ответственности; Мансур как хранитель; Айдар как читающий и сопоставляющий, не как избранный маг. Не добавлять буквальную сцену «Тукай встречает Айдара».
- **Документы/реквизит:** тетрадь 1913, карта/лист 1552, книга пакта с разными слоями чернил, семейные отметки ролей, печать/узел границы.
- **Языковые ключи:** `tt_vasiyat`, `tt_amanat`, повторное чтение `tt_su_anasy` и `tt_boranov`. Все татарские формы, включая религиозно-семейную семантику `аманат`, остаются `pending` до консультации.
- **World variants:** `archive.tukay-open`, `archive.1552-layer`, `pact.ledger-readable`, `pact.roles-inherited`. Визуально это разные слои света/чернил/звука, а не literal time travel.
- **Threat beat:** `act4-pact-ledger`. После чтения книги граница меняет звук и закрывает один путь; игрок должен укрыться за архивным реквизитом или выйти по освещённому маршруту. Это короткая постановочная сцена без боевого AI и без альтернативного финала.
- **Production dependencies:** hero-document readability at first person, readable Tatar tokens and Russian subtitles, authored hand/prop animation, controlled archival overlay, local fog/lighting, culturally reviewed mosque/Tukai/religious framing.

## Акт 5 — «Цена честного будущего»

### Цель и эмоциональный поворот

Дать игроку не победу над «монстром», а осознанное решение прекратить систему, в которой дети наследуют долг без согласия. Трагедия состоит в том, что защита была реальной: после разрушения пакта опасность не исчезает.

### Beat sheet

1. **Последняя граница** — `scene/act5-boundary`. `knowledge/act5_pact_unjust` уже `confirmed`; окружение показывает одновременно след старой защиты и признаки её износа.
2. **Решение Айдара** — `dialogue/aidar-final`. Диалог может иметь разные интонационные варианты по отношениям и найденным clues, но каждая валидная ветка делает одно и то же: завершает `beat/pact-broken` и подтверждает `knowledge/act5_protection_lost`.
3. **Потеря защиты** — world/presentation transition. Старый ambient shield прекращается, звук деревни становится открытым, а локальные NPC-сцены показывают последствия без катастрофического spectacle.
4. **Опасное, но честное утро** — `scene/act5-epilogue`. `knowledge/act5_truth_price` подтверждается, `beat/canonical-tragic-ending` завершается. Эпилог фиксирует цену для деревни и личную цену для Айдара, не предлагая «secret good ending».

### Clue graph

```text
fullgame:knowledge/act4_pact_origin
  -> fullgame:knowledge/act5_pact_unjust
  -> fullgame:dialogue/aidar-final
  -> fullgame:beat/pact-broken
  -> fullgame:knowledge/act5_protection_lost
  -> fullgame:knowledge/act5_truth_price
  -> fullgame:beat/canonical-tragic-ending
```

### Производственная карта

- **Зоны:** `fullgame_act5_boundary`, `fullgame_act5_epilogue`.
- **NPC:** Айдар, Мансур, Алсу и несколько жителей в эпилоге; их presentation state показывает отношение/последствие, но не меняет финальный outcome. Су Анасы/Шүрәле остаются присутствием через звук, воду, тень и границу, а не телом для боя.
- **Документы/реквизит:** книга пакта как последний physical anchor, boundary marker, семейный предмет Марата, незаполненная новая книга/лист будущего.
- **Языковые ключи:** `tt_amanat`, `tt_urman`, `tt_tavysh`, `tt_javap`; обязательная финальная реплика не должна требовать неизвестного татарского слова для прохождения.
- **World variants:** `boundary.before-break`, `boundary.protection-lost`, `village.after-pact`, `epilogue.open-future`. Варианты меняют свет, ambient, доступность некоторых второстепенных реплик и composition эпилога, но не canonical ending ID.
- **Threat beat:** `act5-boundary-disturbance`. Короткая bounded chase/укрытие после решения с ясным выходом и отдельной motion-comfort проверкой. При reduced motion событие сокращается по камере/ускорению, но его narrative outcome сохраняется.
- **Production dependencies:** hero boundary asset, authored transition lighting/fog/audio, final character expressions, safe readable epilogue composition, subtitles/transcript/audio-description cue, full-playthrough save/load checkpoint.

## Матрица локальных решений

| Момент | Что меняется | Что не меняется |
| --- | --- | --- |
| Доверие Алсу или совету | Доступность поясняющей реплики, порядок clues, relationship flags | Пакт существует; переход в Акт 3 остаётся достижимым. |
| Сначала архив или вода | Порядок document/audio reveal и journal emphasis | Долг Су Анасы и предупреждение о Марате остаются обязательными. |
| Как прочитан `аманат` | Интонация Тимура/Мансура, подсветка слова и качество re-read | Происхождение пакта и необходимость финального выбора не исчезают. |
| Финальная интонация Айдара | Эпилогическая постановка, реакция близких, last line | `pact-broken`, `protection-lost` и `canonical-tragic-ending` обязательны. |

## Narrative invariants для Content Lab

1. `urman.fullgame:scene/act5-epilogue` достижим из entrypoint без debug-команды.
2. `urman.fullgame:knowledge/act5_protection_lost` не может стать `confirmed` до `urman.fullgame:dialogue/aidar-final`.
3. `urman.fullgame:knowledge/act5_truth_price` появляется только после `protection_lost` и входа в эпилог.
4. Ни один Act 2–5 interaction не должен отправлять `world.interact` вместо namespaced compiled target.
5. Документы `baranov-1967`, `tukai-1913`, `kazan-1552`, `pact-ledger` открываются через shared kernel и могут создать только idempotent journal projection.
6. Татарские keys имеют `consultantReview.status=pending` до внешнего review; это не считается narrative lock failure, но блокирует cultural/content lock.
7. Mythology entities remain lore/presence contracts; no combat component, loot table or enemy class may be added by this lock.

## Production handoff checklist

- [x] Beat sheet, clue graph, zones, NPCs, documents, language keys, world variants and threat beats зафиксированы для каждого акта.
- [x] Runtime IDs и текущая 46-beat campaign сверены с `content/modules/urman-fullgame/definitions.json` и `content/campaigns/urman.fullgame/campaign.json`.
- [x] Одна каноническая трагическая концовка и reveal ordering зафиксированы.
- [ ] Носитель татарского языка проверяет `tt_amanat`, `tt_su_anasy`, `tt_vasiyat`, `tt_boranov` и повторно используемые Chapter 1 keys.
- [ ] Культурный консультант проверяет Тимура хәзрэта, фольклорные роли, историческую подачу Тукая/1552 и визуальные/аудио threat beats.
- [ ] После style lock production art/animation/audio tasks могут брать этот документ как вход; до этого они остаются blocked соответствующими задачами.

Изменения в этом lock требуют записи в `decision_log.md` и повторной проверки compiled campaign. Внешний review может исправлять формулировки и presentation, но не может молча создавать альтернативную концовку или превращать мифологических существ в combat-мобов.
