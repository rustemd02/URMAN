# Assets

Ассеты — одна из главных проблемных зон проекта. MVP нужно держать компактным и активно заменять дорогую постановку интерфейсами, документами, фото, звуком и портретами.

Top 50 production inventory: см. `asset_inventory_50.md`.

## Production Direction

Accepted MVP direction: static-node hybrid with in-world discrete route navigation and sound-first tension.
Accepted art direction: ink-wash storybook / тушь + приглушённая акварель. См. `design_style.md`.

- Локации лучше делать как статичные или слегка анимированные экраны-узлы, связанные маршрутными сегментами деревни: вид изнутри улицы, поворот на 90 градусов, шаг к следующему сегменту.
- Полноценные перемещение, изометрия, боевые состояния и сложные анимации не входят в MVP floor.
- Top-down карта деревни не является основным экраном перемещения. Она может существовать только как неполная схема в журнале Айдара.
- Интерфейсы как реальные предметы использовать выборочно: старый ПК, бумажный документ, фото, тетрадь/журнал. Старый ПК — главный документальный хаб MVP; остальные diegetic UI не должны становиться обязательным правилом для всего проекта, иначе production cost вырастет.
- Напряжение строить через звук, паузы, реакцию NPC, частично понятные правила и изменение состояния сцены.
- Визуальный принцип: обычность сначала, неправильность потом. Базовые ассеты не должны быть гиперреалистичными или сразу «страшными».
- Art direction reference: «Искатель в доме с привидениями» можно использовать только как вторичный reference для ручной, бумажной тревожности. Базовое решение уже принято в `design_style.md`, а не должно переоткрываться без причины.

## Visual Style Rules

- Линия: неровная тушевая, ручная, читаемая.
- Цвет: приглушённая бумага, болотные зелёные, старое дерево, холодный вечер, слабый янтарный домашний свет.
- Текстура: бумажное зерно, акварельные пятна, умеренная потертость.
- Крипота: через свет, тишину, композицию, тени и почти-заметные силуэты, не через gore / скримерные монстры.
- Спрайты: маленькие 2D cutout / ink silhouettes для карты и узлов; портреты несут эмоции и детализацию.

## Characters

- Asset: Айдар
  Purpose: главный герой, реакции, портрет / модель.
  MVP priority: High
  Needed for: интро, диалоги, журнал, эмоциональная идентификация.
  Notes: нужен не «герой хоррора», а обычный городской парень.

- Asset: Бабай / Мансур
  Purpose: дед, держатель пакта, главный семейный конфликт.
  MVP priority: High
  Needed for: дом, старый ПК, первые полуправды.
  Notes: каноническое имя — Мансур; фамилия пока не фиксируется.

- Asset: Әби / Гөлсинә
  Purpose: тепло дома, бытовой татарский, тихий архив.
  MVP priority: High
  Needed for: дом, язык, обереги, домашние документы.
  Notes: поздний лор сильнее раннего имени Миннигуль.

- Asset: Алсу
  Purpose: проводник, опора, недоговорённость, будущая линия.
  MVP priority: High
  Needed for: первая прогулка, water clues, диалоги.
  Notes: не делать «мистической девушкой».

- Asset: Тимур хәзрәт
  Purpose: safe zone, религиозная рамка.
  MVP priority: High
  Needed for: мечеть, моральная сцена, баланс фольклора и ислама.
  Notes: требует уважительной подачи.

- Asset: Ринат
  Purpose: участковый, dialogue key test.
  MVP priority: High
  Needed for: дело Марата, отказ, реакция на ключи.
  Notes: хороший NPC для первого prototype.

- Asset: Наиля
  Purpose: медпункт и медсправки.
  MVP priority: Medium
  Needed for: противоречия смерти Марата.
  Notes: может быть текстовым NPC на раннем прототипе.

- Asset: Разиля
  Purpose: сельмаг, слухи.
  MVP priority: Medium
  Needed for: социальная карта и бытовые clues.
  Notes: может работать через чат / портрет.

- Asset: Марат
  Purpose: эмоциональный центр через следы.
  MVP priority: High
  Needed for: фото, дневник, сообщения, могила.
  Notes: не обязательно полноценная модель в MVP.

## Locations

- Asset: Дом бабая и әби
  Purpose: первая safe zone, семейный центр.
  MVP priority: High
  Needed for: приезд, ужин, ПК, язык.
  Notes: можно начать как 2–3 экрана / комнаты.

- Asset: Двор / сарай / старая Нива
  Purpose: земной быт и связь с бабаем.
  MVP priority: Medium
  Needed for: разговоры, предметы, воспоминания.
  Notes: Нива важна как живая деталь.

- Asset: Главная улица
  Purpose: деревня как социальная сцена.
  MVP priority: High
  Needed for: первые взгляды, route navigation, социальное давление.
  Notes: accepted direction — walking-height route segment with 90-degree turn affordances and diegetic signs.

- Asset: Village route navigation kit
  Purpose: перемещение по Кырлаю без full top-down map.
  MVP priority: High
  Needed for: дом, улица, кладбище, медпункт, мечеть, кромка Кара-Урмана.
  Notes: 6–10 route screens, physical signs / landmarks, incomplete journal sketch, reusable turn and step transitions.

- Asset: Сельмаг
  Purpose: gossip hub.
  MVP priority: Medium
  Needed for: Разиля, слухи.
  Notes: интерьер может быть простым.

- Asset: Мечеть
  Purpose: safe zone Тимура.
  MVP priority: High
  Needed for: религиозная рамка.
  Notes: нужна культурная аккуратность.

- Asset: Медпункт
  Purpose: документы Марата.
  MVP priority: High
  Needed for: медсправка, Наиля.
  Notes: можно реализовать как UI-доступ к журналу.

- Asset: Кладбище
  Purpose: могила и дата Марата.
  MVP priority: High
  Needed for: первый сильный clue.
  Notes: атмосфера без прямого хоррора.

- Asset: Кромка леса
  Purpose: граница пакта.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: не нужна большая лесная карта.

- Asset: Река / берег
  Purpose: Су Анасы, Баранов, Алсу false lead.
  MVP priority: Medium
  Needed for: water clues.
  Notes: можно отложить, если cliffhanger у леса.

## UI

- Asset: Journal UI
  Purpose: факты, противоречия, персонажи, timeline.
  MVP priority: High
  Needed for: core loop.
  Notes: критично, иначе расследование развалится.

- Asset: Dialogue key picker
  Purpose: применять clues в диалогах.
  MVP priority: High
  Needed for: dialogue key system.
  Notes: лучше простой список с фильтрами.

- Asset: Old PC UI
  Purpose: главный документальный хаб расследования: архив, «Татарвики», документы, сохранённые сообщения, внутренний учёт.
  MVP priority: High
  Needed for: interface investigation, Марат mystery, first pact/system reveal.
  Notes: самый выгодный ассет MVP. Должен ощущаться абсурдно проработанным, но в MVP не требует полноценной ОС или свободного программирования.

- Asset: Vocabulary UI
  Purpose: татарские слова и unlocks.
  MVP priority: High
  Needed for: language mechanic.
  Notes: должен быть связан с уликами.

## Documents

- Asset: Медсправка Марата
  Purpose: противоречие причины смерти.
  MVP priority: High
  Needed for: Марат mystery.
  Notes: формулировки должны быть правдоподобными.

- Asset: Могильная запись / фото могилы
  Purpose: дата и факт смерти.
  MVP priority: High
  Needed for: первый clue.
  Notes: можно сделать как фото/экран.

- Asset: Дневник / сообщения Марата
  Purpose: эмоциональный голос отсутствующего персонажа.
  MVP priority: High
  Needed for: empathy.
  Notes: дозировать, не раскрывать всё сразу.

- Asset: Статья «Татарвики» о Шурале
  Purpose: фольклор как инструкция.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: сначала должна казаться наивной.

- Asset: Баранов 1967 excerpt
  Purpose: архивное доказательство повторяемости.
  MVP priority: Medium
  Needed for: Су Анасы / water line.
  Notes: можно использовать коротким отрывком.

## Audio

- Asset: Домашний ambience
  Purpose: тепло и безопасность.
  MVP priority: Medium
  Needed for: дом.
  Notes: контраст с улицей.

- Asset: Деревенская улица
  Purpose: быт, тишина, наблюдение.
  MVP priority: High
  Needed for: exploration.
  Notes: звук может заменить часть визуальной сложности.

- Asset: Лесная кромка
  Purpose: тревога и мистический след.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: кромка Кара-Урмана; шорохи, шаги, дальние звуки, голос Марата, напряжённые паузы вместо скримеров.

- Asset: Голос Марата
  Purpose: финальный эмоциональный и мистический удар MVP.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: должен звучать лично и узнаваемо, но не раскрывать природу источника.

- Asset: Ринат у кромки
  Purpose: короткое вмешательство «Не отвечай», раскрывающее его практическое знание опасности.
  MVP priority: High
  Needed for: cliffhanger.
  Notes: силуэт / портрет / короткая постановка без объясняющего монолога.

- Asset: Мечеть
  Purpose: safe zone.
  MVP priority: Medium
  Needed for: Тимур.
  Notes: не делать декоративно или клишированно.

## Music

- Asset: Main motif
  Purpose: память, корни, тревога.
  MVP priority: Medium
  Needed for: intro / menu.
  Notes: лучше камерно, без эпического хоррора.

- Asset: Investigation texture
  Purpose: документы и ПК.
  MVP priority: Low
  Needed for: UI.
  Notes: может быть заменено ambience.

## VFX

- Asset: UI scan / old monitor effects
  Purpose: старый ПК.
  MVP priority: Medium
  Needed for: atmosphere.
  Notes: не мешать читаемости.

- Asset: Forest presence
  Purpose: первый мистический след.
  MVP priority: Medium
  Needed for: cliffhanger.
  Notes: силуэт/туман/звук важнее монстра; в принятом cliffhanger главное — голос Марата и реакция Рината.

## Props

- Asset: Старая Нива
  Purpose: бабай, поездки в Казань, семья.
  MVP priority: Medium
  Needed for: двор, воспоминания.
  Notes: сильная земная деталь.

- Asset: Обрег / вещь от әби
  Purpose: забота и тревога.
  MVP priority: Medium
  Needed for: language/home scene.
  Notes: не делать магическим артефактом без решения.

- Asset: Винтовка Марата
  Purpose: финал линии Марата.
  MVP priority: Low
  Needed for: full game / late reveal.
  Notes: для MVP лучше как упоминание.

## Icons

- Asset: clue types
  Purpose: имя, дата, место, документ, слово, фото.
  MVP priority: High
  Needed for: journal and dialogue keys.
  Notes: простые читаемые icons.

## Fonts / Typography

- Asset: UI font with Cyrillic and татарские glyphs
  Purpose: русский + татарский текст.
  MVP priority: High
  Needed for: all UI.
  Notes: проверить ә, ө, ү, җ, ң, һ.

- Asset: archival font style
  Purpose: старые документы.
  MVP priority: Medium
  Needed for: archive.
  Notes: читаемость важнее стилизации.

## Language Learning Assets

- Asset: vocabulary cards
  Purpose: слова, контекст, подтверждения.
  MVP priority: High
  Needed for: татарский mechanic.
  Notes: связать с clues.

- Asset: partially translated text state
  Purpose: re-read mechanic.
  MVP priority: High
  Needed for: documents/dialogues.
  Notes: нужна техническая поддержка.

## Archival Materials

- Asset: 1913 protocol excerpt
  Purpose: Тукай / old order.
  MVP priority: Low
  Needed for: full game.
  Notes: культурно рискованно, осторожно.

- Asset: 1967 Baranov folder
  Purpose: Су Анасы / external researcher.
  MVP priority: Medium
  Needed for: archive quest.
  Notes: может стать MVP teaser.
