# Asset Inventory 50

## Status

Draft production inventory, 2026-05-16.

Задача документа — ограничить ассетный аппетит УРМАНА до 50 самых важных production-юнитов. Это не список всех будущих PNG / WAV / JSON-файлов. Один пункт ниже может включать несколько файлов и состояний, если они принадлежат одному ассетному юниту: например, «портрет Айдара» включает neutral / thinking / pressure, а «дом бабая» включает day / evening / pressure.

Основа отбора:

- MVP держится вокруг Марата, дома, старого ПК, первых диалоговых ключей, татарского языка и кромки Кара-Урмана.
- Принятый формат: static-node hybrid investigation.
- Принятый стиль: ink-wash storybook / тушь + приглушённая акварель.
- Full creature reveal, большая карта, боёвка, отдельная КФУ-сцена и полная 3D-постановка не входят в этот список.

## Priority Legend

- **P0** — без этого вертикальный срез не работает.
- **P1** — нужно для убедительного MVP, можно временно заменить placeholder в greybox.
- **P2** — усиливает атмосферу или расширяет сцену, но не должно блокировать первый playable loop.

## Top 50 Assets

| # | Asset | Type | Priority | Required variations | Used by / reason |
|---:|---|---|---|---|---|
| 1 | Айдар — портретный набор | Character portrait | P0 | neutral, thinking, worried, fear/pressure, small avatar | Главная идентификация игрока, диалоги, журнал, телефонные сцены |
| 2 | Айдар — малый игровой спрайт | Character sprite | P0 | idle, walk 2–4 кадра или cutout-shift, evening/forest tint | Статичные узлы, route navigation overlays, ограниченное перемещение |
| 3 | Мансур бабай — портретный набор | Character portrait | P0 | warm, evasive, stern, almost-confession, pressure | Дом, старый ПК, семейный конфликт, первые полуправды |
| 4 | Мансур бабай — позы / cutout | Character sprite | P1 | standing yard, seated at table, near PC | Дом, двор, разрешение доступа к ПК |
| 5 | Гөлсинә / Әби — портретный набор | Character portrait | P0 | warm, worried, quiet-warning, hidden-knowledge | Дом, язык, бытовая память, оберег |
| 6 | Гөлсинә / Әби — кухонная поза | Character sprite | P1 | cooking/tea, seated, night talk tint | Кухня, safe zone, обучение бытовым словам |
| 7 | Алсу — портретный набор | Character portrait | P0 | guarded, helpful, ironic, worried, withheld-truth | Проводник, первая прогулка, water false lead, диалоги |
| 8 | Алсу — малый спрайт / cutout | Character sprite | P1 | street, water edge, evening | Узлы прогулки, сопровождение, недоговорённость |
| 9 | Марат — портрет / силуэт подростка | Character evidence | P0 | old-photo version, diary silhouette, forest-memory silhouette | Эмоциональный центр MVP; Марат присутствует через следы |
| 10 | Детское фото Айдара и Марата | Character evidence / photo | P0 | clean memory, damaged/metadata version, journal thumbnail | Человеческий крючок расследования |
| 11 | Тимур хәзрәт — портретный набор | Character portrait | P1 | calm, direct, concerned, prayerful/restraint | Мечеть, safe zone, религиозная рамка без карикатуры |
| 12 | Ринат — портретный набор | Character portrait | P0 | official-deflect, irritated, fear, practical-warning | Главный тест dialogue key system, дело Марата |
| 13 | Ринат у кромки — силуэт / cutout | Character scene asset | P0 | distant silhouette, close interruption, hard-cut pose | Клиффхэнгер «Не отвечай» |
| 14 | Наиля — портретный набор | Character portrait | P1 | professional, evasive, alarmed | Медпункт, медсправка Марата, медицинские противоречия |
| 15 | Разиля — портретный набор | Character portrait | P1 | gossip-friendly, cautious, pressure | Сельмаг, социальная карта, слухи |
| 16 | Ambient villagers group | Character crowd | P1 | бабушки silhouettes, абыйлар silhouettes, street watchers, chat avatars | Деревня как социальное давление без дорогих отдельных NPC |
| 17 | Приезд / дорога к Кырлаю | Static location node | P1 | dusk, bus/car arrival, first village glimpse | Открытие без отдельной КФУ-сцены; контраст Казани через ощущение дороги |
| 18 | Дом бабая и әби — экстерьер | Static location node | P0 | day, evening, pressure/night, lit window | Первый safe zone, семейный центр, визуальный якорь игры |
| 19 | Кухня / первый ужин | Static location node | P0 | warm normal, tense silence, night talk | Быт, әби, первое обучение словам, семейное молчание |
| 20 | Комната / стол Мансура со старым ПК | Static location node | P0 | PC off, PC on, search pressure, late night | Переход от дома к главному документальному хабу |
| 21 | Двор / сарай / старая Нива | Static location node + prop | P1 | daytime yard, evening yard, Niva close-up, tool/shed detail | Бабай, семейная практичность, маршрутность деревни |
| 22 | Главная улица Кырлая | Static location node | P0 | day, empty evening, watchers/pressure | Деревня как место, первые взгляды, social pressure |
| 23 | Village route navigation kit | Navigation / location screens | P0 | 6–10 route segments, 90-degree facing views, forward/turn affordances, diegetic signs, journal sketch, evening restrictions, unsafe routes, pressure variants | Перемещение по Кырлаю без top-down карты и без полноценной 3D/изометрии |
| 24 | Сельмаг | Static location node | P1 | exterior, counter interior, gossip pressure | Разиля, слухи, бытовые clues |
| 25 | Мечеть — экстерьер | Static location node | P1 | day, evening, safe light | Тимур хәзрәт, светлая safe zone, культурная аккуратность |
| 26 | Мечеть — чай / кабинет Тимура | Static location node | P1 | calm, serious talk, night safe-zone | Нравственная рамка и выдох между давящими сценами |
| 27 | Медпункт / ФАП | Static location node | P0 | waiting room, cabinet/document desk, pressure | Наиля, медсправка, contradiction loop |
| 28 | Кладбище / зират — общий вид | Static location node | P0 | daytime, late evening, wind/pressure | Первый сильный clue, дата/могила, татарское слово `зират` |
| 29 | Могила Марата — close-up / interaction | Static close-up / minigame | P0 | mossy, cleaned, inscription readable, journal crop | Могильная дата, re-read, tactile investigation |
| 30 | Река / берег | Static location node | P1 | day, dusk, water-still, false-Su-Anasy hint | Water clues, Алсу false lead, `су` vocabulary |
| 31 | Кромка Кара-Урмана | Static location node | P0 | approach, silence, voice moment, Rinat interruption | Финальный cliffhanger MVP; сущность не показывать полностью |
| 32 | Администрация / архивный угол | Static location node | P1 | office, archive cabinet, denied-access state | Бюрократический слой, сельсовет, ложные документы |
| 33 | Старый CRT / monitor hardware | UI / prop | P0 | off, boot, working, power-off, close crop | Старый ПК как предмет, не абстрактное меню |
| 34 | Old PC desktop shell | UI | P0 | desktop, folder view, active window, error/corrupt file | Оболочка document hub, Win98-like без мема |
| 35 | Archive search app | UI | P0 | empty search, results, metadata clue, gated result | Her Story loop, поиск по словам / датам / именам |
| 36 | «Татарвики» page template | UI / document template | P0 | article, highlighted term, survival-note reveal, re-read state | Фольклор сначала как сайт, потом как инструкция |
| 37 | «Ялкын» / ICQ messenger | UI | P1 | contacts, group chat, DM Алсу, saved Marat log, unread danger message | Чаты жителей, язык, бытовой юмор и тревога |
| 38 | Телефон Айдара / intro chat UI | UI | P1 | mother chat, ticket/purchase, Казань context, translation popup | Лёгкий городской фон без отдельной КФУ-сцены |
| 39 | Journal / clue graph | UI | P0 | facts, contradictions, timeline, character page, dangerous clue mark | Игроку нужен внешний мозг для детектива |
| 40 | Dialogue key picker | UI | P0 | simple key list, filtered keys, dangerous key warning, used key state | Основная механика «знание как ключ» |
| 41 | Vocabulary / татарский карточки | UI | P0 | unknown, guessed, confirmed, re-read unlocked, word-as-key | Татарский не декор, а механика расследования |
| 42 | Document viewer template | UI / document system | P0 | scanned page, clean text layer, metadata panel, highlighted term, re-read diff | Все документы должны быть читаемыми и playable |
| 43 | Clue type icon set | UI icon set | P0 | name, date, place, document, word, photo, contradiction, danger | Журнал, dialogue picker, document viewer |
| 44 | Inventory / evidence prop icon set | UI icon set | P1 | phone, flashlight, notepad, house key, әби candy, obereg, brush | Текущий inventory + tactile clue handling |
| 45 | Медсправка / медзапись Марата | Document | P0 | scan, metadata, redacted/ambiguous phrase, readable text layer | Главный медпункт clue; версия смерти не сходится |
| 46 | Могильная запись / cemetery registry | Document / photo | P0 | photo, registry line, date crop, journal evidence card | Дата смерти / факт смерти / contradiction |
| 47 | Дневник Марата | Document | P0 | early note, angry note, forest route note, damaged page | Голос Марата без флэшбеков и катсцен |
| 48 | Сохранённые сообщения Марата | Document / chat log | P0 | last normal message, warning message, missing attachment, timestamp clue | Эмоциональный и технический bridge к old PC |
| 49 | Внутренний учёт / pact folder | Document pack | P0 | household list, incident log, compensation note, `ПАКТ_1999` placeholder replacement | Первое доказательство системы, но без полного раскрытия |
| 50 | Audio / VFX state pack | Audio + overlay pack | P0 | home ambience, street silence, old PC hum, mosque calm, cemetery wind, water stillness, forest presence, Marat voice, Rinat «Не отвечай» | Напряжение строится звуком и состояниями, не показом монстра |

## Folded Under Existing Rows

Эти ассеты важны, но не получают отдельную строку в лимите 50:

- Старая Нива включена в #21.
- Обрег / вещь от әби включены в #44.
- Щётка для зират-сцены включена в #44.
- Фоновые бабушки, абыйлар и чат-аватары включены в #16.
- Карта / маршрут к Кара-Урману входит в #23 и #49, если это документ.
- Шурале как полный creature asset не входит в MVP; его присутствие выражается через #31 и #50.
- Бичура, Албасты, Убыр, Су бабасы, Аждаһа и Дию не получают production-ассеты в MVP; только текстовые / wiki references через #36.
- Баранов 1967 и Тукай 1913 не получают отдельных ассетов в этом списке, кроме возможного teaser внутри #36 или #49.

## Existing Prototype Assets Audit

Текущие файлы в `public/assets/` полезны как прототипы, но не являются утверждённым финальным стилем:

- `background_house.jpg`, `tatar_house_sprite.jpg`, `photo_alsu.jpg`, `photo_les.jpg`, `photo_defekt.jpg` — заменить или перерисовать в ink-wash style.
- `monitor-frame.png` — можно использовать как reference для #33, но финальный CRT должен совпадать с `design_style.md`.
- `al-aqsa_mosque.glb`, `mosque/source/Cami Mosque.glb`, `bridge.glb` — 3D-заглушки; для MVP-дока не считать финальным art direction.
- `grass_tex.png`, `light_grass_tex.png`, `water_tex.png`, `wood_tex.png`, `trees.png`, `zirat_sprite.png`, `yangir.png` — прототипные textures / sprites, подлежат замене или стилизации.
- `audio/wind.mp3`, `audio/footsteps.mp3`, `audio/forest_howl.mp3`, `audio/cattle.mp3`, `audio/door_creak.mp3` — можно использовать как временный audio bed для #50, но нужен авторский audio pass.

## Village Route Navigation Scope

Village route navigation kit — отдельный P0-ассет (#23), а не декор. Он заменяет большую открытую карту и держит static-node hybrid.

Accepted direction, 2026-05-16: **variant A from the village map pitch** — игрок стоит внутри деревни, видит текущий участок дороги и выбирает шаг вперёд / поворот на 90 градусов / осмотр.

Detailed draw list: `village_route_art_spec.md`.

Формат:

- in-world route screens in ink-wash style, not a full top-down map;
- текущий сегмент дороги: forward direction, left/right turns where available, inspectable signs / doors / landmarks;
- физические указатели вместо чистых quest markers: `мәчет`, `зират`, `ФАП`, `сельмаг`, дом Мансура, мост, Кара-Урман;
- неполная схема в журнале Айдара как support layer: дорисованные маршруты, clue pins, старые названия, unsafe marks;
- pressure variants: route feels watched, signs are harder to read, ambience changes, some paths are socially or narratively blocked.

Плавность:

- turn left / right: 250–450 ms transition between facing views, with foreground parallax and ink-smear edge darkening;
- step forward: 500–800 ms walking transition, footsteps, slight bob and foreground occlusion;
- no fake continuous 3D rotation of a flat painting;
- important route reveals can use custom painted transition frames, but normal navigation must use a reusable transition kit.

Минимальные узлы MVP:

- дом бабая и әби;
- двор / сарай / старая Нива;
- главная улица;
- сельмаг;
- мечеть;
- медпункт / ФАП;
- кладбище / зират;
- река / берег;
- администрация / архивный угол;
- точка Алсу;
- кромка Кара-Урмана.

Минимальные route segment screens MVP:

- главная улица Кырлая: first arrival / social pressure;
- поворот к дому Мансура: safe-zone direction, old Нива landmark;
- перекрёсток у сельмага / ФАП;
- указатель к мечети;
- дорога к зирату;
- берег / мост route, if water line remains in the slice;
- последний readable segment before Кара-Урман;
- кромка Кара-Урмана.

Варианты route kit:

- day: обычные доступные маршруты;
- evening: часть маршрутов социально или сюжетно закрыта;
- pressure: деревня реагирует, появляются отметки наблюдения / запрета;
- archive overlay: старая подпись, маршрут Марата или Кара-Урман как старое place-name.

Текущий процедурный / изометрический `MainMap` в коде можно считать greybox-навигацией, но не финальным визуальным направлением. Финальное направление — in-world route segments + incomplete journal sketch.

## Next Step

Разложить эти 50 production-юнитов в asset registry с полями из `technical_architecture.md`: `asset id`, `type`, `priority`, `used by`, `MVP status`, `placeholder status`, `source / license`.
