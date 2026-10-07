# A2 — Дайджест художественного направления и ограничений УРМАН

**Дата:** 2026-10-07. **Назначение:** зафиксировать, что заявлено в проекте про картинку, и отделить
действующие требования от исторических. Только факты и цитаты, каждый пункт — с путём к файлу.

**Поправка к путям.** Два файла из задания лежат не в `docs/production/`:
`docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` и
`docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md`. Остальные восемь — по указанным путям.

---

## 1. Целевой стиль — что именно заявлено

### 1.1. Зафиксированный стандарт: Painterly Low-Poly 3D

- `docs/urman_knowledge_base/canon.md` (Hard Canon): «Production presentation — ходибельный
  first-person **Painterly Low-Poly 3D** на Godot/C#.»
- `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md`: «Производственный
  стандарт: **Painterly Low-Poly 3D** — авторская, умеренная геометрия + живописные материалы, свет
  и туман в реальном ходибельном 3D-мире»; это «**единственный визуальный контракт** для следующих
  проходов Акта I», статус — «art-direction contract; **не art lock**».
- `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md`: «Рендер-цель: **Painterly
  Low-Poly 3D, first-person, Godot**».

### 1.2. Формула «дорогой картинки» (действующий прод-план)

- `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §0.1: п.1 «**Единство языка.** Одна «кисть»
  везде: одинаковая плотность мазка, один масштаб деталей, одна степень стилизации»; п.2 «**Ценовая
  иерархия (3 ступени)**»: светлое → среднее → тёмные акценты; п.3 «**Направленный свет.** Тёплый
  ключ + холодная заполняющая… Объекты «сидят» на земле»; п.8 «**UI — часть той же книги.** Все
  экраны выглядят страницами одного архива: бумага, тушь, печать».

### 1.3. Предложение от 2 октября 2026 — стиль объявлен не зафиксированным

- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` (шапка): «**Уточнение автора — 2 октября
  2026:** нынешний вид игры не нравится автору; текущий визуальный стиль **не зафиксирован** и может
  быть существенно изменён»; «Главная идея: **«Обжитое тепло внутри огромной холодной чащи»**»;
  «**Статус: предложение и план будущей реализации**»; §1.2 «Не запускать реализацию по факту
  появления этого файла».
- Там же §3.1: «Сдержанный художественный реализм … **Рекомендуется**»; «Полный фотореализм с
  тяжёлыми сканами и светом … Не выбирать». Рабочее название: «**Тихая зимняя деревня у чужого
  леса**». §1.1: «Реалистичная конструкция вещей при избирательно упрощённой детализации. Стилизуем
  отбор форм и отношения цветов, сохраняя масштаб и узнаваемые материалы»; §3.2 — стилизация
  «должна быть видна в уменьшенном кадре».
- `docs/production/URMAN_VISUAL_REFERENCES_FINDINGS_RU.md` §4: «**Живописный материальный мир —
  рекомендую**».

### 1.4. Что считается неверным результатом

- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §3.3: «Неоновые бирюзовые тени и оранжевые
  окна; вечный золотой закат; густой белый туман во всех местах; тотальная ржавчина и плесень;
  мультяшные контуры; ступенчатое cel shading всего мира; вездесущая акварельная рябь; резкость как
  способ скрыть плохие карты; глубокий чёрный вместо леса; игрушечная деревня без масштаба».
  Анти-паттерны «дешёвой картинки» — `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §0.2 (см. §3.1).

### 1.5. Сезон и место (действующая фиксация)

- `docs/urman_knowledge_base/canon.md`: «деревня называется **КАРА-УРМАН**, действие — **зима
  2026**»; «Вокруг деревни высокий густой страшный лес скрывает внешний край мира».
- `docs/urman_knowledge_base/decision_log.md` (2026-09-10): «Пользователь **отклонил** текущий
  летний/влажный вид деревни как неаутентичный: массовые ели и сосны в деревенских зонах читаются
  как «пальмы»/парк, а не как татарская деревня.»

---

## 2. Эталонные ориентиры

### 2.1. Роли ориентиров

- `docs/production/URMAN_VISUAL_REFERENCES_FINDINGS_RU.md` §4: «**Шишкин — лес и масштаб; Моне —
  снег и цвет света; Брейгель — связность обжитого пространства; Ларссон — организация дома; Олкотт —
  свет на объёме; Firewatch и BOTW — отбор форм для игры.** Фридрих, «Фарго», «Сталкер» и INSIDE
  дают отдельные способы построения глубины, напряжения и внимания»; «Не нужно сводить палитры всех
  произведений в одну».

### 2.2. Игры

- `docs/production/URMAN_VISUAL_REFERENCES_FINDINGS_RU.md` §3: Firewatch (2016, Campo Santo); INSIDE
  (2016, Playdead); The Legend of Zelda: Breath of the Wild (2017, Nintendo); о BOTW — «в интервью
  Эйдзи Аонумы живописный стиль связан с тем, чтобы игрок видел важные элементы большого мира».
- `docs/urman_knowledge_base/decision_log.md` (2026-09-28): «Исследование в духе **BOTW/TOTK** должно
  быть самостоятельно интересным, а не линейным опросом жителей»; (30.09.2026): «Ориентир автора
  **Metro 2033/Last Light** — ощущение, а не копирование ассетов».
- Анти-ориентир: `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md` — «повторяемый ряд
  конусов» и «greybox-граница» означают непройденный layout gate.

### 2.3. Культурно-территориальные референсы (только наблюдение)

- `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` §7: «Ссылки ниже — только
  визуальные/контекстные референсы. Ничего из них не копируется, не вырезается в ассет и не
  скачивается в репозиторий». Список: Кинопоиск и Entermedia «Микулай», Blender Artists «Gloomy
  rain», Unsplash dark forest, Rustik68 «село Кугушево», Tatarica «Кошлауч». §6: ««Микулай» имеет
  **кряшенский** контекст. … Нельзя переносить в УРМАН его религиозные, этнические, костюмные,
  персонажные или сюжетные маркеры».- `docs/urman_knowledge_base/decision_log.md` (2026-09-28): деревянные мечети Асан-Елги и Качимира
  (Комитет РТ), мечеть в Малых Кармалах, сине-белый киоск (CIAN); «Современный киоск — референс формы
  и материала, не доказательство его советской датировки».

---
## 3. Технические ограничения по картинке

### 3.1. Явные запреты

- `docs/production/URMAN_VISUAL_REVIEW_PROMPT_RU.md` (§«ЖЁСТКИЕ ОГРАНИЧЕНИЯ»): «Стиль: painterly
  low-poly; запрещены **фотореализм, DOF, motion blur, вигнетка, хроматические аберрации, «свечение
  всего кадра», LUT-файлы**».
- `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §0.2: «bloom/свечение на всём подряд,
  SSAO-грязь, хроматические аберрации, вигнетка, motion blur, DOF (**запрещены design_style**)»;
  §3 п.6: «Запрещено: фотореализм, LUT-файлы, DOF/motion blur/вигнетка/аберрация, свечение всего
  кадра, автозакрытие art-lock, изменение канона/культурного слоя».
- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §16.4: «Постоянный DOF при ходьбе,
  хроматическая аберрация, тяжёлое плёночное зерно, сильная виньетка, lens dirt, принудительный
  motion blur, агрессивный sharpening»; краткий эффект пролога «нельзя распространить на всю
  ходьбу».
- `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` §3.7: «Постоянные VHS,
  пикселизация, тяжёлый outline, chromatic aberration и aggressive motion blur запрещены».

### 3.2. Условно разрешённое (с оговорками)

- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §16.2: «Допустим **очень сдержанный**
  отклик ярких ламп и некоторых окон ночью. Он не должен превращать весь снег, белые стены и UI в
  светящиеся поверхности»; «Новый WorldEnvironment поверх старого **запрещён**». §16.3: «LUT не
  нужен, если обычных параметров достаточно»; «Не строить разные независимые LUT для каждого дома».
  §16.5: «Не уменьшать FOV ради «кинематографичности»».
- `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §0.1 п.5: «**Каждый эффект обоснован.** Для
  каждого постэффекта действует тест отключения: если выключение не ухудшает кадр — эффект лишний,
  убрать или ослабить. Glow живёт только на эмиссивах и не мылит UI». §1: «Пост-эффекты
  (пер-зонные): tonemap AgX, Glow, SSAO, Adjustments, туман, небо».

### 3.3. Бюджеты: FPS, меши, draw calls

- `docs/production/URMAN_VISUAL_REVIEW_PROMPT_RU.md`: «Производительность: **119–145 FPS** на Apple
  M4 Pro при полу 30; **~65–71 тыс. мешей**. Бюджет на геометрию ограничен».
- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §18.2: «Для режима **60 FPS** длительность
  кадра около **16.7 мс** — ориентир, а не обещание на любой машине. Отчёт включает **p95** и редкие
  пики после прогрева, а не только средний FPS»; §18.1: «школа около **6689 draw calls / 16.7 мс**,
  ДК около **20592 / 31.9 мс** на Apple M1. Это сигнал проверить подачу геометрии и теней, а не
  текущий benchmark».
- `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §5: DoD включает «benchmark **floor 30** с
  запасом; low-профиль не разваливает стиль».
- `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md` (§9): «**OPEN:** mesh-level
  collision, navigation and **target-host performance** for future authored geometry».

### 3.4. Бюджеты: текстуры и UV

- `docs/urman_knowledge_base/art/texture_candidates_qa.md` (§Deterministic image gate): «**1 024 ×
  1 024**, 8-bit RGB/RGBA, non-interlaced PNG, opposite-edge seam, clipping, HSV safety and
  texel-density thresholds».
- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §11.5: «**Не повышать всё до 4K.**
  Разрешение выбирается по экранному размеру и частоте повторения»; «Реальный масштаб повторения
  задаётся в метрах. Не масштабировать текстуру под один ракурс»; «На плоской бумаге и вывеске —
  authored UV; точный текст сохраняется редактируемым». §11.2: «Увеличивать metallic для «красоты»
  неметаллических вещей **нельзя**». §22: «Детальнее — всем 4K» — не лечит UV, форму и повторение.

### 3.5. Пространственно-владельческие запреты

- `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md` (§No-go): «**Never call a material,
  fog, foliage or transition fade a fix for missing geometry, off-ground placement or a 180°
  backside.**»; «Never attach imported `StaticBody3D`, `CollisionShape3D`, navigation or interaction
  descendants from a presentation GLB without a separately accepted owner»; §8: «No claim says “ready
  demo”, “final art lock” or “production-ready kit” while any master-layout gate remains `OPEN`».
  `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §1.1 п.6: «Никакого отдельного «нового
  рендера» рядом со старым»; `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §3 п.2: «Никаких
  новых систем рендера/UI-фреймворков; правки в существующих владельцах».

---

## 4. Культурные требования

### 4.1. Канонические рамки

- `docs/urman_knowledge_base/canon.md` (Hard Canon): «Ислам в деревне сохраняется. Религиозный слой
  **нельзя делать карикатурным или враждебным фольклору**»; «Существа **не должны быть простыми
  монстрами**. Шурале — не один демон, а древний лесной народ / класс существ»; «Пакт — не религия и
  не культ, а старый порядок, обязанность и техника выживания»; «Тимур хәзрәт Нуруллин — молодой
  имам из Казани, моральная опора и safe zone через мечеть».
- Там же (Soft Canon): «Мечеть и дом Тимура — safe zone не как магический щит, а как место
  собранности, света и религиозной защиты»; (Contradictions): нерешённые Айдар/Айрат,
  Гөлсинә/Миннигуль, два Марата; «Кырлай» — прежнее рабочее имя.

### 4.2. Визуальные правила культурного слоя

- `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` §6: «Татарский культурный
  код держится местом, бытом, отношениями, речью, документами и точными предметами. **Орнамент не
  наклеивается на каждую поверхность**»; «**Ислам не изображается как враждебная или «проклятая»
  сила.** … визуальный страх не строится на религиозном объекте»; «Шурале и другие существа — часть
  старого порядка и системы сосуществования, **не мобы**. В Акте I предпочтительны след, силуэт,
  звук, документ или несостыковка; не полный monster reveal»; «Все татарские надписи/слова в
  визуальных ассетах должны сохранять буквы `ә, ө, ү, җ, ң, һ`; финальная орфография, таблички
  зирата, ФАП, мечети и документы требуют языкового и культурного **human review**».
- `docs/production/URMAN_VISUAL_REVIEW_PROMPT_RU.md`: «Культурный слой: татарская деревня —
  уважительно, **быт и предметы вместо орнаментного перегруза**; ислам не карикатурится; любые
  надписи — через проверку носителем».
- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §12.4: «Орнамент принадлежит конкретному
  предмету; его не надо размножать на каждую стену и кнопку»; «Мечеть и зират **не превращаются в
  декорацию «проклятого места»**»; «Существующие утверждённые тексты не перерисовывать ImageGen».
- `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md` (§8): «Татарские labels/words,
  zirat markers, FAP sign and local household forms pass language, cultural and religious **human
  review**»; «No literal `Микулай`/Kryashen borrowing, generic monster language, “cursed mosque”
  framing or full creature reveal appears before the locked cliffhanger».

### 4.3. Авыл, быт, тон

- `docs/urman_knowledge_base/decision_log.md` (2026-09-28, п.9): «обычная, приятная, уютная деревня с
  отдельными страшными эпизодами; **не постоянно зловещая декорация**»; (п.12): «Магазин — сине-белый
  сельский павильон/ларёк, **без салунного фальшфасада и крыльца**»; «Мечеть — по татарским сельским
  прототипам, с полноценным минаретом».
- Там же (30.09.2026): «Визуальный быт **2000–2026** без точных декоративных дат»; «Без предъявления
  названного монстра, боевой смерти, падения как ragdoll, громкого удара/скримера в лицо».
- `docs/urman_knowledge_base/art/layout_rework_textures_2026-10-02.md` (T09, «Текст»): «**Нет
  арабских, псевдоарабских, религиозных или иных надписей**».
---

## 5. Правила работы с ассетами и текстурами

### 5.1. Роль ImageGen и паспорт ассета

- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §11.6: «Генерировать только **недостающую**
  карту для известного меша и material slot после проверки библиотеки»; «**Не генерировать
  одновременно альбедо с нарисованными объёмными тенями** и затем накладывать на геометрию те же тени
  от света»; «Не получать roughness простой инверсией красивой картинки без проверки физического
  смысла»; «Блокировка генерации задерживает только конкретную карту, не свет, UV или геометрию».
- `docs/urman_knowledge_base/decision_log.md` (2026-09-28, «Визуал»): «Только самостоятельно
  производимый 3D-результат с геометрией и текстурами; красивые недостижимые imagegen-концепты **не
  цель**. … Imagegen допустим для пригодных текстур, **не доказательство сцены**»; (п.9): «Не считать
  imagegen-концепт доказательством качества runtime».
- Паспорт: `docs/urman_knowledge_base/art/layout_rework_textures_2026-10-02.md` (T09) — поля Статус,
  Consumer, Файл, Runtime URI, Формат/размер, SHA-256, Назначение, UV, Направление, Контекст, Текст,
  Материал, Масштаб, Происхождение, Проверка изображения, Открыто.
  `docs/urman_knowledge_base/decision_log.md` (30.09.2026): «Все вывески, плакаты, рукописные
  надписи, детские рисунки и экспонаты школы/ДК: **подробные отдельные ImageGen паспорта** с точным
  текстом, UV, потребителем, масштабом, возрастом поверхности и проверкой».

### 5.2. Можно / нельзя

**Можно:** `docs/urman_knowledge_base/art/layout_rework_textures_2026-10-02.md` — «остальные текстуры
дерева, стен и крыши **переиспользуются из библиотеки**, без генерации уникального изображения на
каждый повтор»; «Новая текстура производится только при подтверждённом отсутствии подходящего
consumer-совместимого материала». `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §11.5 —
triplanar для большого статичного природного объекта; §11.3 — «Не менять цвет дома при новом
запуске».

**Нельзя:** `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` §2 — «**Текстура
как замена геометрии**» в колонке «Сейчас нельзя»; «Геометрия сначала задаёт контакт, фаску, толщину,
край и силуэт; материал добавляет живописную неоднородность».
`docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §11.3 — «Если кора, штукатурка, снег и обои
одинаково рябят с десяти метров, весь мир становится визуально дешёвым и шумным».
`docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` §9 — порядок обязателен:
1) рельеф и силуэты → 2) архитектура и границы → 3) флора → 4) материалы; следующий слой не
маскирует провал предыдущего. `docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §0.2 — «шум вместо
текстуры; видимый квадрат тайла на стене/дороге» = немедленный брак.

**Запрет однотонных заглушек:** `AGENTS.md` — «Ни **однотонная заглушка**, ни «95% сгенерировано» не
принимают качество». `docs/urman_knowledge_base/art/texture_candidates_qa.md`: «Day street: road,
houses, fences and repeated tree family **still read as greybox modules**».
`docs/urman_knowledge_base/decision_log.md` (2026-09-10) — кадры `phase02/final1080` помечены как
«**однотонный снег**», и «Новые колея, материал снега, следы и формы деревьев… **не приняты**».
`docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` (Фаза 1, DoD) — «30 м стены не однотонные».

### 5.3. Остановка «чехарды» вариантов

- `docs/urman_knowledge_base/decision_log.md` (2026-09-03, «Stop painterly texture candidate churn;
  production set is the runtime-referenced six»): «Treat the six runtime-referenced families as the
  **current production set**. No new painterly variants are authored; no candidate is promoted to the
  runtime set without a recorded, proven material/scale blocker and a new decision log entry»;
  «Candidate churn stops at v6… does not claim art lock». `docs/urman_knowledge_base/art/texture_candidates_qa.md`:
  «**QA verdict: PASS … art lock remains OPEN.**»

---

## 6. Ранее предложенные и отклонённые решения

### 6.1. Направление и сезон

| Предложение | Итог | Источник |
|---|---|---|
| Летний/влажный вид, «недавно прошедший дождь», сине-зелёные сумерки | **Отклонено автором**; Акт I переведён на зиму | `docs/urman_knowledge_base/decision_log.md` (2026-09-10) |
| Массовые ели и сосны в деревенских зонах | **Отклонено**: читаются как «пальмы»/парк | `docs/urman_knowledge_base/decision_log.md` (2026-09-10) |
| Буквальное повторение флешфорварда; эпизод во время метели | **Отклонено** | `docs/urman_knowledge_base/decision_log.md` (2026-09-28) |
| Отдельное видимое автобусное вступление | **Заменено** Нивой; автобус — предыстория | `docs/urman_knowledge_base/decision_log.md` (2026-09-28) |

### 6.2. Текстуры (v2–v6)

| Кандидат | Итог | Источник |
|---|---|---|
| v2: wood, plaster, earth, pine, stone, fabric | Презентационные кандидаты с владельцами; арт-лок открыт | `docs/urman_knowledge_base/art/texture_candidates_qa.md` |
| v3: те же 6 | Сравнительный проход; «runtime activation … remain rejected» | `docs/urman_knowledge_base/decision_log.md` (2026-08-13) |
| v4 earth/wood | **HOLD/REWORK**: earth дублировал авторский рельеф дороги и лужи, wood давал регулярные швы/сучья | `docs/urman_knowledge_base/decision_log.md` (2026-08-14) |
| v5 earth/wood | Технические кандидаты; «runtime activation and final art lock remain rejected» | `docs/urman_knowledge_base/decision_log.md` (2026-08-14) |
| v6 earth/wood | Image gate PASS, но «production decision remains **OPEN**»; wood — «abstract wash tile without a recognizable knot or board border»; первая попытка wood «rejected as photographic» | `docs/urman_knowledge_base/art/texture_candidates_qa.md` (§v6) |
| Новые painterly-варианты как практика | **Остановлено** | `docs/urman_knowledge_base/decision_log.md` (2026-09-03) |

### 6.3. Прочие отклонённые решения

- `docs/urman_knowledge_base/decision_log.md` (2026-10-04): «**азан отклонён и снят**» — «Автор отверг
  стамбульскую CC0-запись: «азан кринж»».
- Там же: (5752) «Автор отклонил **процедурный прототип участка как нереалистичный**»; (4799)
  добавление второй формы в `AgentB_TerrainCollision` **отклонено**; (30.09.2026) «Повторные два
  кадра р2 **отвергнуты**»; (2026-09-29) «ImageGen quota/API key **блокирует** новую генерацию»;
  (2026-09-28, п.13) «концепт через imagegen **не закрепляет новый лор**».
- `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §22 — отклонены как ошибочные реакции:
  «Серо — увеличить saturation»; «Страшнее — затемнить весь экран»; «Стильно — включить полный набор
  эффектов»; «Детальнее — всем 4K»; «Лес редкий — удвоить деревья»; «High красивый — Low неважен».

### 6.4. Открытые гейты (не отклонено и не принято)

- `docs/urman_knowledge_base/art/texture_candidates_qa.md`: «Remaining production gates are temporal
  motion/head-bob comfort, near/mid/far readability, authored geometry and hero props, fog/light
  calibration, mesh-collision review, M1/Windows performance, accessibility and cultural review,
  authored voice/final mix, and **final art lock**».
---

## 7. Противоречия между документами

**1. Пути источников.** Оба файла лежат в `docs/urman_knowledge_base/art/`, а не в `docs/production/`.

**2. Зафиксирован ли стиль.** `docs/urman_knowledge_base/canon.md` («Painterly Low-Poly 3D»),
`docs/production/URMAN_VISUAL_REVIEW_PROMPT_RU.md` («Стиль: painterly low-poly») и
`docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` («единственный визуальный
контракт») подают стиль как действующий. Но `docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md`
(2 октября 2026) заявляет: «текущий визуальный стиль **не зафиксирован**» и рекомендует
«сдержанный художественный реализм».

**3. Сезон и топоним.** `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md`
написан под **дождь и Кырлай** («дорога имеет вес, уклон и **мокрые края**», «затем **дождь**,
туман»), а §3.3 разрешает «молодые ели» в деревенских зонах. Действующий канон
(`docs/urman_knowledge_base/canon.md`) фиксирует **зиму 2026** и **Кара-Урман**;
`docs/urman_knowledge_base/decision_log.md` (2026-09-10) делает хвойные в деревенских зонах
недопустимыми. Шапка bible снимает только §3.7.

**4. Glow и adjustments.** `docs/production/URMAN_VISUAL_REVIEW_PROMPT_RU.md` описывает текущее
освещение как «AgX-тонмаппинг, **Glow/SSAO/Adjustments**».
`docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §2.2 утверждает обратное: «Внешние **glow и
adjustments сейчас принудительно отключаются** при применении профиля… цветокоррекция может вообще
не дожить до кадра».

**5. Свечение: полный запрет vs ограниченный glow.**
`docs/production/URMAN_PROD_READY_VISUAL_PLAN_RU.md` §3 п.6 запрещает «**свечение всего кадра**», а
`docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` §16.2 допускает «**очень сдержанный** отклик
ярких ламп и некоторых окон ночью». Буквально совместимо, но задаёт разную планку приёмки.

**6. Бюджет производительности.** `docs/production/URMAN_VISUAL_REVIEW_PROMPT_RU.md` даёт «119–145 FPS
на Apple M4 Pro … ~65–71 тыс. мешей», тогда как `docs/urman_knowledge_base/decision_log.md`
(2026-09-10) фиксирует p95 около 100 мс. Значения относятся к разным срезам/методикам
(принудительный скрытый draw против обычного запуска), но в документах не сведены.

**7. Статус приёмки.** Все визуальные документы одновременно называют себя контрактом и отрицают
приёмку: `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md` — «AUTHORITATIVE SPATIAL
PRODUCTION CONTRACT / **PARTIAL IMPLEMENTATION**»; bible — «art-direction contract; **не art lock**»;
`docs/production/URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` — «**предложение и план** будущей реализации».
Ни один источник не подтверждает принятую картинку.
