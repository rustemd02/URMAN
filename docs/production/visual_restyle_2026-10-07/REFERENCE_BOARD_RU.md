# Референс-board (VIS-066)

Сгенерировано из [reference_manifest.json](reference_manifest.json); проверка — `python3 eng/verify-visual-reference-manifest.py`.
Референс задаёт только перечисленное свойство. Ни один не production asset и не копируется буквально.
Точные кадры, которых нет в репозитории, не заменяются похожими: открывайте источник по ссылке.
Сами изображения — `URMAN_VISUAL_REFERENCE_BOARD.pdf` внутри ZIP, ссылки в `docs/URMAN_VISUAL_RESET_2026-10-07/DRIVE_LINKS_RU.md`.

## 1. Выбор автора (15 кодов) — это цели

| Код | Статус | Брать (только это свойство) | Не брать | Источник | Образ в репо |
|---|---|---|---|---|---|
| P1 | author_selected | Художественность поверхности, крупные живописные пятна, ощущение нарисованного кадра при физически убедительных зданиях. | Не копировать сеттинг, персонажа, архитектуру или точную палитру. | `references/approved/P1_user_selected_painterly.png` (в ZIP на Drive) | missing_locally_in_repo |
| W1 | author_positive_property | Смелые тёпло-холодные цвета снега и зимнего воздуха. | Не копировать всю стилизацию игры. | https://store.steampowered.com/app/1053710/The_Red_Lantern/ | link_only |
| W2 | author_positive_property | Физически читаемая утоптанная/углублённая дорога, снег имеет толщину и края. | Не переносить весь художественный стиль. | https://store.steampowered.com/app/1180660/Tell_Me_Why/ | link_only |
| V1 | author_positive_property | Смелые цветовые отношения в человеческом пространстве. | Не копировать городскую архитектуру. | https://store.steampowered.com/app/936790/Life_is_Strange_True_Colors/ | link_only |
| V2 | author_positive_property | Смелая палитра и крупные цветовые массы при простой геометрии. | Не брать карикатурность лиц/форм. | https://store.steampowered.com/app/1466640/Road_96/ | link_only |
| V4 | author_positive_property | Абстрактные графические линии ветра как часть атмосферы мира. | Не превращать эффект в UI, outline или постоянный шум. | https://store.steampowered.com/app/1466640/Road_96/ | link_only |
| I1 | author_positive_property | Цветовая температура и приятное сочетание тёплого/холодного. | Не копировать предметный стиль или cozy-мультяшность. | https://store.steampowered.com/app/897730/Among_Trees/ | link_only |
| N1 | author_strong_positive_property | Зелёная/изумрудная нечеловеческая ночь, читаемая без сплошной черноты. | Не делать всю игру вечным Firewatch и не копировать low-poly лес. | https://store.steampowered.com/app/383870/Firewatch/ | link_only |
| N2 | author_strong_positive_property | Сильный контраст двух цветовых семейств ночью. | Не превращать каждый ночной кадр в неон. | https://store.steampowered.com/app/1466640/Road_96/ | link_only |
| H2-1 | author_positive | Холодный зимний лес, большая атмосферная глубина, исчезающие слои. | Не копировать сюжетные эффекты/призраков. | https://store.steampowered.com/app/343710/KHOLAT/ | link_only |
| H2-2 | author_positive | Дозированный неправильный янтарно-оранжевый цвет внутри холодного леса. | Не делать весь лес оранжевым. | https://store.steampowered.com/app/343710/KHOLAT/ | link_only |
| H2-3 | author_positive | Сильный цвет может становиться состоянием пространства в редких эпизодах. | Красный не должен стать постоянным horror shorthand. | https://www.alanwake.com/ | link_only |
| H3-1 | author_partial_positive | Редкие длинные стрёмные ветви/сучья, нарушающие нормальный ритм. | Не переносить гигантские корни, лес-организм, гипертрофированный масштаб всего мира. | https://store.steampowered.com/app/274520/Darkwood/ | link_only |
| H4-2 | author_positive_property | Нечеловеческая, почти монохромная цветовая атмосфера с сохранённой материальностью. | Не копировать киношный сеттинг. | https://a24films.com/films/the-green-knight | link_only |
| H4-3 | author_positive_property | Холодная сине-зелёная нечеловеческая ночь/лес. | Не переносить body-horror мотивы. | https://www.imdb.com/title/tt11881160/ | link_only |

## 2. Положительные позиции автора, не входящие в 15 кодов (общие свойства, не отдельные кадры)

| Код | Статус | Брать (только это свойство) | Не брать | Источник | Образ в репо |
|---|---|---|---|---|---|
| LIS2-PAINT | author_positive_property | Рисовка/материальность: формы не демонстрируют low-poly, поверхности мягкие и цельные. | Не брать скучный нейтральный daylight как обязательную палитру. | https://store.steampowered.com/app/532210/Life_is_Strange_2/ | link_only |
| I-CURRENT | author_positive_current_game | Сохранить умеренный реализм и советско-татарский вайб текущих интерьеров. | Не проводить глобальную мультяшную/наружную стилизацию по комнатам. | baseline_images + screenshot index | link_only |
| B-COLOR | author_positive_aggregate | Смелость цвета важнее буквального общего стиля отдельной игры. | Не объявлять одну игру единым стилем УРМАНА. | — | no_image |

## 3. Антипримеры — никогда не цель

| Код | Статус | Брать (только это свойство) | Не брать | Источник | Образ в репо |
|---|---|---|---|---|---|
| W3 | author_negative | Только как антипример. | Плоский снег без массы и читаемого рельефа. | https://store.steampowered.com/app/532210/Life_is_Strange_2/ | link_only |
| W4 | author_negative | Ничего обязательного. | Не использовать как цель. | reference discussed in chat; exact frame bytes unavailable | link_only |
| F-REJECT | author_negative | Ничего как основной лесной язык. | Красивый cozy-лес вместо пугающего хтонического леса. | — | no_image |
| N3 | author_negative | Антипример. | Скучная ночь как просто затемнённый день. | https://store.steampowered.com/app/532210/Life_is_Strange_2/ | link_only |
| N4 | author_negative | Антипример. | Скучная нейтральная ночь без собственной палитры. | https://store.steampowered.com/app/936790/Life_is_Strange_True_Colors/ | link_only |
| H3 | author_mostly_negative | Только идеи пространственной странности в очень редких эпизодах. | Не делать лес постоянно сюрреалистическим или фантастически деформированным. | — | no_image |
