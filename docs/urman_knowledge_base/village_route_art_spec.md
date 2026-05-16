# Village Route Art Spec

## Status

Accepted art / navigation spec, 2026-05-16.

Этот документ фиксирует, **что конкретно рисовать** для ходибельной карты мира УРМАНА. Это не top-down карта и не полноценная изометрическая деревня. Принятое направление: **variant A — in-world discrete route navigation** в стиле `ink-wash storybook / тушь + приглушённая акварель`.

Игрок видит один маршрутный сегмент Кырлая за раз: участок дороги, поворот, забор, дом, указатель, лесную границу. Он делает фиксированный шаг вперёд, поворачивается на 90 градусов, осматривает объекты и постепенно собирает неполную схему деревни в журнале.

## Production Rule

Рисовать нужно не «красивую карту деревни», а **набор игровых экранов маршрута**.

Каждый route screen должен отвечать на четыре вопроса:

- куда можно идти вперёд;
- есть ли поворот налево / направо;
- какой landmark помогает не потеряться;
- почему в этом месте слегка не по себе.

Если экран не помогает перемещаться, расследовать, чувствовать pressure или узнавать Кырлай, он не входит в MVP.

## Frame Format

Базовый формат для route screen:

- 16:9 gameplay frame;
- walking-height / eye-level view, как будто Айдар стоит на дороге;
- ink line + muted watercolor wash + paper grain;
- минимум один clear route affordance: дорога вперёд, поворот, калитка, тропа, указатель;
- без полного обзора деревни сверху;
- без fake 3D perspective stretch.

Layer package на каждый экран:

- `background`: небо, дальние дома, лес, минарет, линия улицы;
- `midground`: дорога, заборы, дома, столбы, основные landmarks;
- `foreground`: ветки, край забора, трава, грязь, предметы у края кадра;
- `occlusion`: слой для шагового transition, если нужен;
- `interactables`: указатели, дверь, окно, NPC silhouette, след, объявление;
- `light_overlay`: вечер, окно, фонарь, mosque-safe light;
- `pressure_overlay`: тени, неподвижность, странный силуэт из веток, затемнение краёв.

## MVP Draw List

Минимальный набор для первого playable route prototype: **8 route screen units**.

| ID | Экран | Что рисовать | Навигация | Состояния |
|---|---|---|---|---|
| `route_main_street_entry` | Главная улица, вход в Кырлай | грязная дорога, деревянные заборы, 2–3 дома, столб, дальний минарет | вперёд, поворот к дому Мансура | day, evening, pressure |
| `route_mansur_house_turn` | Поворот к дому Мансура | старая Нива как landmark, калитка, тёплое окно, двор | вперёд к дому, назад на улицу | day, evening |
| `route_village_crossroad` | Малый перекрёсток | доска объявлений, развилка, указатели `ФАП`, `сельмаг`, `мәчет` | налево / направо / вперёд | day, pressure |
| `route_selsmag_fap_lane` | Проулок сельмаг / ФАП | низкое здание ФАП, сельмаг, закрытая дверь, окно с наблюдением | вперёд, поворот назад | day, evening, pressure |
| `route_mosque_sign` | Поворот к мечети | спокойный свет, силуэт мечети, чистая дорожка, менее давящая композиция | вперёд к мечети, назад | day, evening |
| `route_zirat_road` | Дорога к зирату | узкая дорога, сухая трава, знак `зират`, деревья плотнее обычного | вперёд, назад | day, evening, pressure |
| `route_forest_approach` | Последний обычный участок перед Кара-Урманом | дорога темнеет, лес занимает больше кадра, старый предупреждающий знак | вперёд к кромке, назад | evening, pressure |
| `route_kara_urman_edge` | Кромка Кара-Урмана | граница леса, ветки почти складываются в фигуру, нет полного существа | осмотр, назад, scripted lock | pressure, cliffhanger |

## Stretch Draw List

Эти экраны нужны, если MVP сохраняет речную / Алсу / water false lead линию:

| ID | Экран | Что рисовать | Навигация | Состояния |
|---|---|---|---|---|
| `route_bridge_river_turn` | Поворот к мосту / реке | мост, влажная дорога, камыш, холодный свет воды | вперёд к берегу, назад | day, dusk, pressure |
| `route_alsu_meeting_spot` | Точка Алсу | край дороги, скамейка / забор, вид на воду или поле | разговор, назад | day, evening |
| `route_admin_archive_corner` | Администрация / архивный угол | сельсовет, дверь, стенд, бумажные объявления | осмотр, назад | day |

## Facing Views

Не каждый сегмент требует четыре стороны. Для MVP рисовать только playable directions.

Правило:

- если игрок может повернуть налево, нужен отдельный facing view после поворота;
- если игрок может повернуть направо, нужен отдельный facing view после поворота;
- если назад — это просто reverse transition к прошлому сегменту, отдельный красивый кадр можно не рисовать в greybox;
- для важных мест, где игрок должен почувствовать пространство, нужен `back_view` или `side_view`.

Minimum facing set:

| Route | Required views |
|---|---|
| `route_main_street_entry` | forward, right_to_mansur |
| `route_mansur_house_turn` | forward_to_house, back_to_street |
| `route_village_crossroad` | forward, left_to_fap_selsmag, right_to_mosque |
| `route_selsmag_fap_lane` | forward, back |
| `route_mosque_sign` | forward, back |
| `route_zirat_road` | forward, back |
| `route_forest_approach` | forward, back |
| `route_kara_urman_edge` | forward / inspect-only |

## Diegetic Signs

Указатели должны быть частью мира, а не floating UI.

Рисовать:

- деревянный указатель `мәчет`;
- деревянный / металлический указатель `зират`;
- табличку `ФАП`;
- вывеску `сельмаг`;
- старую доску объявлений;
- предупреждающую табличку у леса: `Кара-Урман` или старая выцветшая подпись;
- рукописные бумажки / объявления, которые можно приблизить.

Не рисовать:

- большие glowing arrows;
- миникарту с полным маршрутом;
- RPG quest markers над объектами;
- одинаковые идеальные дорожные знаки;
- декоративный татарский орнамент на каждом указателе.

Татарский текст на знаках требует проверки носителем до production lock. До проверки можно использовать placeholder IDs в слоях, но не финальные фразы.

## Interaction Affordances

Допустимые подсказки:

- слабая тушевая стрелка на краю кадра при hover / focus;
- чуть более уверенная линия у интерактивного указателя;
- локальный свет / контраст на двери или табличке;
- cursor change / small UI hint вне финального кадра;
- короткий звук шага, дерева, бумаги или далёкого голоса.

Недопустимые подсказки:

- яркие кнопки поверх мира;
- постоянная сетка направлений;
- компас как GPS;
- автоматический quest path.

## Transition Assets

Нужен reusable transition kit, а не уникальная анимация на каждый шаг.

Рисовать / подготовить:

| Asset | Purpose | Notes |
|---|---|---|
| `transition_turn_90_ink_smear` | поворот налево / направо | 250–450 ms, затемнение краёв, лёгкий smear |
| `transition_step_forward_occlusion` | шаг вперёд | 500–800 ms, foreground passes across frame |
| `transition_forest_pressure_edge` | важный переход к Кара-Урману | использовать редко, более тёмный paper wash |
| `journal_route_sketch_update` | появление новой линии в журнале | simple ink line draw-on |

Технический принцип: transition скрывает смену заранее нарисованных кадров, но не притворяется настоящей 3D-камерой.

## Journal Sketch

Журнал — support layer, не основной экран перемещения.

Рисовать:

- бумажную неполную схему маршрутов;
- открытые route nodes как точки / короткие линии;
- зачёркнутые или опасные маршруты;
- clue pins;
- старые place names, включая Кара-Урман как урочище / лесную границу;
- rough handwriting Айдара.

Не рисовать:

- полную карту Кырлая с самого начала;
- точное GPS-положение;
- красивый fantasy map poster;
- карту, которая раскрывает все будущие места.

## Pressure Variants

Не надо перерисовывать каждый экран полностью для pressure. Достаточно заранее заложить overlay и 1–2 изменяемые детали.

Examples:

- окно, которое раньше было тёмным, теперь слабо светится;
- силуэт жителя появился слишком далеко, чтобы поговорить;
- указатель стал хуже читаем;
- дорога кажется уже за счёт foreground branches;
- ambience почти исчез;
- лес стоит слишком неподвижно.

Levels:

- `normal`: бытовая деревня, читаемый маршрут;
- `evening`: меньше света, больше тишины, часть маршрутов закрыта социально;
- `pressure`: деревня наблюдает, но без прямого монстра;
- `cliffhanger`: только для `route_kara_urman_edge`.

## First Art Batch

Если рисовать прямо сейчас, порядок такой:

1. `route_main_street_entry` — проверить базовый язык ходибельной деревни.
2. `route_village_crossroad` — проверить указатели и 90-degree choices.
3. `transition_turn_90_ink_smear` — проверить псевдоплавный поворот.
4. `transition_step_forward_occlusion` — проверить шаг без fake 3D.
5. `route_forest_approach` — проверить «обычность сначала, неправильность потом».
6. `journal_route_sketch_update` — проверить, что схема помогает, но не раскрывает всё.

Если эти шесть элементов работают, можно производить остальные route screens.

## Naming

Suggested asset IDs:

```text
route_<place>_<function>_<state>
route_main_street_entry_day
route_main_street_entry_pressure
route_village_crossroad_day
route_zirat_road_evening
route_kara_urman_edge_cliffhanger
transition_turn_90_ink_smear
transition_step_forward_occlusion
journal_route_sketch_base
journal_route_sketch_pressure
```

## Acceptance Checklist

Route screen готов, если:

- игрок понимает минимум одно доступное направление;
- масштаб деревни не раскрыт целиком;
- есть физический landmark или указатель;
- экран выглядит как ink-wash storybook, а не 3D render / pixel art / horror poster;
- интерактивные элементы читаются без ярких quest markers;
- в кадре есть лёгкая тревога, но нет прямого monster reveal;
- экран можно соединить с соседним сегментом через reusable transition;
- татарский текст либо проверен, либо помечен как placeholder.

## Explicit Non-goals

- Не рисовать полную карту деревни сверху как основной overworld.
- Не делать свободное перемещение по каждому метру.
- Не делать fake 3D rotation из одной растянутой картинки.
- Не делать все улицы одинаково страшными.
- Не превращать Кара-Урман в обычный dungeon entrance.
- Не показывать Шурале или другое существо полностью в route navigation MVP.
