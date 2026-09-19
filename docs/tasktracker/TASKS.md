# TASKS.md — единый тасктрекер Акта I

> Источники требований — `docs/tasktracker/`: `00_rework_brief.md` (§1–§23),
> `01_production_tracker.md` (очередь волн, гейты), `02_village_rework_plan.md` (§13: EX00–EX14, LEN01),
> `03_oldpc_full_system.md` (ПК v2: живой чат, хинты, глобальный поиск, 30+ документов, тетрис-пасхалка),
> `address_mechanics_v3/` (механика адресов). Живая очередь — `docs/urman_knowledge_base/execution_backlog.json`.
> Статус `blocked` = внешний гейт (человек/голоса/культура/Windows/подпись), не бросать молча.

Всего задач: **78**. In progress: **50**, blocked: **11**, completed/done: **16**, review: **1**.

## Задачи по разделам ТЗ

### §1. Игрок и управление

Файл: `00_rework_brief.md` §1.
- **ACT1-PLAYER** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Игрок: прыжок, бег, приседание и видимое тело
  Результат: §1: grounded-прыжок, быстрое перемещение, реальная капсула приседа, ноги/низ тела при взгляде вниз, коллизии и равные переназначаемые устройства ввода.
  Зависимости: ACT1-EX00. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §2. Взаимодействия и стук в двери

Файл: `00_rework_brief.md` §2.
- **ACT1-EX00** [in_progress] (milestone M5 — External QA and release acceptance, deferred): §13 EX00: Общий контракт новых действий
  Результат: Управление, перенос/размещение, состояние и сохранение согласованы для первого набора
  Зависимости: —. Владелец: root.
  Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.

### §3. Адресная система деревни

Файл: `00_rework_brief.md` §3.
- **ACT1-ADDR** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Адреса действующей деревни и самостоятельная навигация
  Результат: §3 и полный пакет v3: импорт текущего мира, устойчивые building/parcel/address/access IDs, двуязычный каталог, существующие номера/суффиксы/угловой вход, фиктивный кадастр с уникальностью, единый граф, migration/rebuild/reorder/rename, Усал15/52≤40м, достижимые входы и реальные читаемые таблички. NPC→книжка→поиск→взаимодействие без GPS/автопути.
  Зависимости: ACT1-EX00. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **ACT1-ADDR-FENCE** [in_progress] (milestone M2 — Godot first-person Act 1 demo, in_progress): Таблички на заборе/калитке для домов без места на фасаде (H032/H034/H045/H046)
  Зависимости: ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Табличка 1.18м висит рядом с калиткой/на заборе для H032/H034/H045/H046 без изменения геометрии.
- **ACT1-ADDR-ACCESS** [in_progress] (milestone M2 — Godot first-person Act 1 demo, in_progress): Проходы к 11 домам: точки доступа внутри коллизии (H001/H005/H008/H014/H021/H024/H033/H037/H044/H046/H049)
  Зависимости: ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Полный аудит address-world PASS: 0 ACCESS_NOT_VERIFIED.

### §4. Записная книжка

Файл: `00_rework_brief.md` §4.
- **ACT1-NOTEBOOK** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Личная записная книжка и постепенное знание
  Результат: §4: люди, услышанные/прочитанные адреса, задачи, места, источники/наблюдения и личные заметки; удобное перечитывание, совместимый save/load, отсутствие преждевременных знаний и видимых целей на карте.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §5. Компьютер бабая

Файл: `00_rework_brief.md` §5.
- **ACT1-OLDPC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Компьютер 2000-х и внутриигровая сеть
  Результат: §§5–6: XP-подобные desktop/start/taskbar/clock/windows/focus/z-order/drag, папки/корзина/редакторы/browser/viewer; TatWiki и местная соцсеть с профилями, фото, постами, комментариями и внутренними ссылками. Авторские данные/старый OldPc, bounded compatibility и accessConditions остаются единственным владельцем знаний.
  Зависимости: CORE-002, FINISH-06. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §6. Интернет бабая

Файл: `00_rework_brief.md` §6.
- **ACT1-OLDPC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Компьютер 2000-х и внутриигровая сеть
  Результат: §§5–6: XP-подобные desktop/start/taskbar/clock/windows/focus/z-order/drag, папки/корзина/редакторы/browser/viewer; TatWiki и местная соцсеть с профилями, фото, постами, комментариями и внутренними ссылками. Авторские данные/старый OldPc, bounded compatibility и accessConditions остаются единственным владельцем знаний.
  Зависимости: CORE-002, FINISH-06. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §5–§6 v2. ПК бабая — полная система (чат, подсказки, поиск, документы, тетрис)

Файл: `03_oldpc_full_system.md` (решения опроса 2026-09-18: гибрид desktop+поиск, живой чат+хинты, 30+ доков, тетрис — чистая пасхалка).
Замки `docs/urman_knowledge_base/old_pc.md` действуют: accessConditions — единственный владелец знаний, пакт доказывается но не объясняется, Марат только read-only архив, техно-метаданные не улика.
- **ACT1-OLDPC-CHAT** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Живой чат «Ялкын · Сообщения»: треды Алсу/Ринат/бабай/заметки Айдара, варианты ответа, скриптованные входящие по триггерам, unread-счётчик, bounded-снапшот
  Результат: `03_oldpc_full_system.md` §3: тред Марата только read-only архив («ответить нельзя»); Ринат после `rinat.alerted=true`; первая сцена доступа к ПК — первое сообщение бабая; свободный текст не двигает сюжет; чат не выдаёт закрытые документы целиком.
  Зависимости: ACT1-OLDPC. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **ACT1-OLDPC-HINTS** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Контекстные подсказки 3 уровней: системный тред «Заметки Айдара» + строка в архиве, data-driven `hints/*.md`, gating без спойлеров, `hintsSeen` в снапшоте
  Результат: `03_oldpc_full_system.md` §4: хинт ведёт к месту улики, а не цитирует закрытый документ; уровень 3 только после ≥2 Tier-A документов; хинты не пишут knowledge/журнал сами.
  Зависимости: ACT1-OLDPC, ACT1-OLDPC-CHAT. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **ACT1-OLDPC-SEARCH** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Глобальный поиск: поле в «Пуске» + «Искать везде» в архиве; ищет по доступным документам, истории чатов, заметкам, истории браузера; татарские слова — кнопки-ключи
  Результат: `03_oldpc_full_system.md` §2: закрытые записи не участвуют в выдаче («🔒 Закрытые записи», ноль — «переформулируйте» + 3 термина); клик идёт через обычный `IsOldPcDocumentAccessible`; поиск не даёт знаний.
  Зависимости: ACT1-OLDPC. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **ACT1-OLDPC-DOCS** [in_progress] (milestone M5 — External QA and release acceptance, deferred): 30+ глубоких документов по §6: Tier A (12, ≥350 слов) / Tier B (10, ≥150 слов) / Tier C (10+), цепочки gating, татарский слой, перекрёстные ссылки
  Результат: `03_oldpc_full_system.md` §6: каждый Tier-A — searchTerms ≥5 (≥1 татарское), suggestedTerms ≥2, reveals/requires/unlocks залинкованы, reliability+canonStatus обязательны, ссылки на ≥2 связанных; один пароль максимум (татарское слово); быт — ложные следы, не конкуренты.
  Зависимости: ACT1-OLDPC. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **ACT1-OLDPC-TETRIS** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Тетрис-пасхалка: стакан 10×20, 7 фигур, уровни/скорость/пауза, только персистентное поле `tetrisHigh`, изоляция от knowledge-шины
  Результат: `03_oldpc_full_system.md` §7: счёт не открывает документы/реплики/хинты/журнал; середина партии не сохраняется; пауза при сворачивании; отдельный UI-класс.
  Зависимости: ACT1-OLDPC. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **ACT1-OLDPC-SHELL** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Оболочка под 9 окон: снапшот/схема/провайдер (`Applications += chat, tetris`, окон max 9, `chat`/`hintsSeen`/`tetrisHigh`), миграция старых сейвов, доступность новых окон
  Результат: `03_oldpc_full_system.md` §9: C# + схема + провайдер меняются только вместе; неизвестные id окон отбрасываются, новые поля — дефолты, load не крашится; новые контролы вписаны в `ApplyDesktopAccessibility`.
  Зависимости: ACT1-OLDPC. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §7. Старая Нива

Файл: `00_rework_brief.md` §7.
- **ACT1-TRANSPORT** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Нива, мотоцикл, лошадь и телега
  Результат: §§7–9: посадка/безопасный выход, запуск/движение/тормоза/свет/камеры, устойчивый баланс, лошадь/телега/реакции/связь, реальные интерьеры/формы/звуки, парковка и сохранения без обхода сюжетных границ. Контакты всего видимого объёма, удержанный ввод и altered-obstacle restore.
  Зависимости: ACT1-EX00, ACT1-PLAYER, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §8. Мотоцикл

Файл: `00_rework_brief.md` §8.
- **ACT1-TRANSPORT** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Нива, мотоцикл, лошадь и телега
  Результат: §§7–9: посадка/безопасный выход, запуск/движение/тормоза/свет/камеры, устойчивый баланс, лошадь/телега/реакции/связь, реальные интерьеры/формы/звуки, парковка и сохранения без обхода сюжетных границ. Контакты всего видимого объёма, удержанный ввод и altered-obstacle restore.
  Зависимости: ACT1-EX00, ACT1-PLAYER, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §9. Лошадь и телега

Файл: `00_rework_brief.md` §9.
- **ACT1-TRANSPORT** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Нива, мотоцикл, лошадь и телега
  Результат: §§7–9: посадка/безопасный выход, запуск/движение/тормоза/свет/камеры, устойчивый баланс, лошадь/телега/реакции/связь, реальные интерьеры/формы/звуки, парковка и сохранения без обхода сюжетных границ. Контакты всего видимого объёма, удержанный ввод и altered-obstacle restore.
  Зависимости: ACT1-EX00, ACT1-PLAYER, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §10. Татарское радио

Файл: `00_rework_brief.md` §10.
- **ACT1-RADIO** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Татарское местное радио в транспорте
  Результат: §10: музыка и татарская/русская речь, погода, новости, объявления, поздравления/саламы/услуги/соседние авылы и уместные подсказки; управление/переходы/позиция/save. Подтверждённые права на тексты, музыку, исполнение и мастер.
  Зависимости: ACT1-TRANSPORT, FINISH-05. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §11. Начало игры и автобусная остановка

Файл: `00_rework_brief.md` §11.
- **ACT1-ARRIVAL** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Приезд на остановку Кара-Урмана
  Результат: §11: обычная новая игра на остановке у съезда; имя, расписание, объявления, лавка, дорога и звуковое ощущение приезда. Необязательный уход автобуса не создаёт тяжёлую cutscene-систему.
  Зависимости: ACT1-ADDR, FINISH-02. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §12. Дом бабая

Файл: `00_rework_brief.md` §12.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §13. Баня бабая

Файл: `00_rework_brief.md` §13.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §14. Мечеть

Файл: `00_rework_brief.md` §14.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §15. ФАП / медпункт

Файл: `00_rework_brief.md` §15.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §16. Магазин

Файл: `00_rework_brief.md` §16.
- **ACT1-SHOP** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Магазин и атомарная долговая тетрадь
  Результат: §16: Разиля, товары/холодильник/прилавок/объявления/разговор; нужные товары на бабая, одна атомарная покупка+custody+запись, без денежной экономики/повторной выдачи послеload.
  Зависимости: ACT1-EX00, ACT1-ADDR, ACT1-PUBLIC. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §17. Закрытая школа

Файл: `00_rework_brief.md` §17.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §18. Сельсовет / ДК

Файл: `00_rework_brief.md` §18.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §19. Зират

Файл: `00_rework_brief.md` §19.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

### §20. Обычная деревня

Файл: `00_rework_brief.md` §20.
- **ACT1-PUBLIC** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Баня, мечеть, школа, сельсовет/ДК и семейный зират
  Результат: §§13–15,17–19: доступные цельные бытовые помещения, безопасная мечеть и обычный имам, печь/тепло/пар/конденсат/звук бани, школа закрыта из-за малого числа детей, кабинет/архив/зал/сцена/склад ДК, уважительные согласованные семейные записи. ФАП развивается у существующего владельца. Не открывать ActII/III.
  Зависимости: ACT1-EX00, ACT1-ADDR. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.
- **FINISH-09** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Dense open-world exploration, secrets and hidden paths
  Результат: Beautiful, rewarding locations to explore in the spirit of BOTW/TOTK; minimum production batch 24 varied optional discoveries, 3 walkable hidden loops, 1 meaningful removable/breakable access obstacle. Counts alone do not prove interest.
  Зависимости: —. Владелец: main.
  Готово когда: All existing Act I locations reward thorough exploration, without requiring every secret for the main story.

### §21. Лес

Файл: `00_rework_brief.md` §21.
- **FINISH-09** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Dense open-world exploration, secrets and hidden paths
  Результат: Beautiful, rewarding locations to explore in the spirit of BOTW/TOTK; minimum production batch 24 varied optional discoveries, 3 walkable hidden loops, 1 meaningful removable/breakable access obstacle. Counts alone do not prove interest.
  Зависимости: —. Владелец: main.
  Готово когда: All existing Act I locations reward thorough exploration, without requiring every secret for the main story.

### §22. Шурале

Файл: `00_rework_brief.md` §22.
- **FINISH-03** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Three investigation cycles and staged final route
  Результат: Three distinguishable player-driven cycles, source return, incomplete inference feedback, Timur and prepared Rinat.
  Зависимости: —. Владелец: None.
  Готово когда: Three distinguishable player-driven cycles, source return, incomplete inference feedback, Timur and prepared Rinat.

### §23. Общая структура имплементации

Файл: `00_rework_brief.md` §23.
- **ACT1-TZ-ACCEPT** [in_progress] (milestone M5 — External QA and release acceptance, deferred): Совокупная приёмка расширенного ТЗ и адресов
  Результат: Все23раздела расширенногоТЗ+полныйaddressv3+исходныйActI+EX00–14. Реализация→точнаясборка→доказательство→границы. СвежийReleaseactualDLL/PCK/binary/provenance,12s+60sforegroundperf включаятранспорт/новыекомнаты; первыйосновнойчеловеческийчас после измененияадресов/скорости. Ничего не исключается как 'следующаяигра'.
  Зависимости: ACT1-PLAYER, ACT1-ADDR, ACT1-NOTEBOOK, ACT1-OLDPC, ACT1-TRANSPORT, ACT1-RADIO, ACT1-ARRIVAL, ACT1-SHOP, ACT1-PUBLIC, ACT1-EX-ACCEPT, LEN01. Владелец: root/expanded-act1.
  Готово когда: Требование реализовано в основной игре и подтверждено соответствующей проверкой на точной текущей сборке.

## Задачи вне §1–§23 брифа (очередь, EX/LEN, финиш)

### FINISH-08 [blocked]

External release confirmation (milestone M5 — External QA and release acceptance, deferred).
Результат: Rights-cleared physical voice, cultural review, new-player observation and real host checks; never invent approval.
Зависимости: —. Владелец: None.
Готово когда: Rights-cleared physical voice, cultural review, new-player observation and real host checks; never invent approval.
Evidence: 2026-09-14 M10: native r34 candidate launched and its menu observed live, userdata restored; interactive pass blocked by the environment (transparent system window owns all pixels, no AX, no keystroke injection) and recorded without invented answers; human checklist form added to playtest_

### FINISH-07 [in_progress]

Final performance traversal and standalone candidate (milestone M5 — External QA and release acceptance, deferred).
Результат: Full ordinary input pass; targeted existing smoke; native package launched; 60s measured heavy views meet handover section24 or remain explicitly open.
Зависимости: —. Владелец: None.
Готово когда: Full ordinary input pass; targeted existing smoke; native package launched; 60s measured heavy views meet handover section24 or remain explicitly open.
Evidence: 2026-09-11 current source window probe, village_day@arrival, M4 Pro 1080p medium0.9/MSAA2: 12s warmup +60.01s, avg9.372ms,p9516.686,p9917.188,max22.976,106.70FPS,0 long frames; source_perf_stalls/run.log. Current standalone package, other heavy views and ordinary full traversal remain open

### FINISH-06 [in_progress]

Product UI menus save resume and credits (milestone M5 — External QA and release acceptance, deferred).
Результат: Normal and large text/UI input states, safe freshest continue, truthful credits and end-to-menu lifecycle.
Зависимости: —. Владелец: main.
Готово когда: Normal and large text/UI input states, safe freshest continue, truthful credits and end-to-menu lifecycle.
Evidence: 2026-09-11: newest validated Continue + profile preserved on load + credits and licenses + ending-to-menu/restart. menu_lifecycle_build_v9, menu_user_settings_smoke, menu_ui, menu_cold_continue_v2 and menu_ending_flow_v3 PASS; 1280x720 normal/1.6 menu/credits frames inspected. Export credi

### FINISH-05 [in_progress]

Winter sound and complete cue lifecycle (milestone M5 — External QA and release acceptance, deferred).
Результат: Winter surfaces and ambience; reduced motion retains footsteps; caption-independent queue; real licensed final voices or explicit external recording gap.
Зависимости: —. Владелец: None.
Готово когда: Winter surfaces and ambience; reduced motion retains footsteps; caption-independent queue; real licensed final voices or explicit external recording gap.
Evidence: /Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/footsteps_regenerate.log

### FINISH-04 [in_progress]

Home FAP cast and contextual village presentation (milestone M5 — External QA and release acceptance, deferred).
Результат: Mandatory scenes/people reviewed in real first-person motion and 1080p/720p frames; winter exterior strengths retained.
Зависимости: —. Владелец: None.
Готово когда: Mandatory scenes/people reviewed in real first-person motion and 1080p/720p frames; winter exterior strengths retained.
Evidence: 2026-09-12 d1b9749: fitted Mansur cap/age atlas, household CRT/table/chair scale, round FAP stool, Gulsina folded-towel rest pose; actual native close/general frames reviewed, Blender verifiers/import/build/registry pass. Overall character/art acceptance remains open.

### FINISH-02 [in_progress]

First 8–12 minute playable investigation slice (milestone M5 — External QA and release acceptance, deferred).
Результат: Early meaningful choices and first evidence/application/reaction loop with polished home, character, audio and save.
Зависимости: —. Владелец: None.
Готово когда: Early meaningful choices and first evidence/application/reaction loop with polished home, character, audio and save.
Evidence: /Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/early_dialogue_smoke.log

### FINISH-01 [done]

Native release export and timed package baseline (milestone M5 — External QA and release acceptance, deferred).
Результат: Native macOS/Windows files plus honest 12s warmup/60s window-renderer baseline; ordinary UI baseline recorded.
Зависимости: —. Владелец: /root.
Готово когда: Native macOS/Windows files plus honest 12s warmup/60s window-renderer baseline; ordinary UI baseline recorded.
Evidence: /Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/native_export_v3.log

### BASE-001 [completed]

Зафиксировать baseline и golden fixtures (milestone M0 — Baseline and migration contract, completed).
Результат: TaskStartSnapshot, parity fixtures, toolchain versions and migration evidence.
Зависимости: —. Владелец: None.
Готово когда: Текущая Chapter 1 имеет воспроизводимые compiled content, commands, events, fingerprint и snapshots.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/10-intent.md

### BASE-002 [completed]

Закрепить engine, style, story and retirement boundaries (milestone M0 — Baseline and migration contract, completed).
Результат: Accepted Godot/C# ownership, Painterly Low-Poly direction, five-act arc and cutover boundary.
Зависимости: BASE-001. Владелец: None.
Готово когда: Нет конкурирующего engine/style decision в активных документах.
Evidence: docs/urman_knowledge_base/decision_log.md

### CORE-001 [completed]

Перенести content compiler и Content Lab на C# (milestone M1 — C# content and runtime parity, completed).
Результат: C# validate/compile/inspect/simulate/report with Chapter 1 and full-game parity.
Зависимости: BASE-002. Владелец: None.
Готово когда: Chapter 1 diagnostics, namespaced IDs, fingerprint and narrative invariants совпадают с golden fixtures.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/90-evidence.md

### CORE-002 [completed]

Перенести runtime kernel, capabilities and SaveGameV3 (milestone M1 — C# content and runtime parity, completed).
Результат: Godot-independent kernel with deterministic state, capabilities, atomic SaveGameV3 and settings contract.
Зависимости: CORE-001. Владелец: None.
Готово когда: Core tests cover runtime, scheduler, capability lifecycle, save/load and invalid payload recovery.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/20-checkpoint.md

### GODOT-001 [completed]

Собрать first-person Chapter 1 vertical slice (milestone M2 — Godot first-person Act 1 demo, in_progress).
Результат: Walkable arrival-to-cliffhanger path with interactions, old PC, dialogue, journal, documents and persistence.
Зависимости: CORE-002. Владелец: None.
Готово когда: 16 authored Chapter 1 beats проходят через пять compact zones в одном runtime state.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/90-evidence.md

### GODOT-002 [completed]

Подключить full-game authored zones and presentation adapters (milestone M2 — Godot first-person Act 1 demo, in_progress).
Результат: 12 authored PackedScene wrappers and full-game traversal to the canonical tragic epilogue.
Зависимости: GODOT-001. Владелец: None.
Готово когда: 46 authored beats проходят через 12 compact zones.
Evidence: docs/urman_knowledge_base/art/fullgame_frames/README.md

### GODOT-003 [in_progress]

Принять три in-engine style benchmarks (milestone M2 — Godot first-person Act 1 demo, in_progress).
Результат: Godot captures and spatial near/mid/far/FOV evidence for day street, house/old PC and Kara-Urman night edge, retained as production candidates pending visual acceptance.
Зависимости: GODOT-001, ASSET-001. Владелец: godot/art.
Готово когда: Силуэты, painterly materials, muted palette, local lights, fog and detail density проходят visual review.
Evidence: docs/urman_knowledge_base/art/style_frames/README.md

### GODOT-004 [completed]

Закрыть SaveGameV3-backed accessibility settings contract (milestone M2 — Godot first-person Act 1 demo, in_progress).
Результат: Reduced motion, high contrast, text scale, subtitles and audio-description settings shared by runtime and Godot presentation.
Зависимости: GODOT-001, CORE-002. Владелец: None.
Готово когда: Accessibility snapshot round-trips through SaveGameV3 and rejects text scale outside 0.8–1.6.
Evidence: docs/urman_knowledge_base/decision_log.md

### GODOT-005 [completed]

Зафиксировать отдельную играбельную демо-точку входа первого акта (milestone M2 — Godot first-person Act 1 demo, in_progress).
Результат: Default Godot launch for an atmospheric first-person Act 1 demo with an intro card, five-zone investigation route and a fade-to-black at the authored «Не отвечай» cliffhanger.
Зависимости: GODOT-001, GODOT-004. Владелец: None.
Готово когда: game/project.godot launches res://scenes/act1_demo.tscn instead of the generic composition root.
Evidence: game/scripts/Act1DemoRoot.cs

### NARR-001 [completed]

Заблокировать narrative package Acts 2–5 до дорогих ассетов (milestone M3 — Narrative lock for Acts 2–5, deferred).
Результат: Accepted beat sheet, clue graph, zones, NPCs, documents, татарские keys, world variants and threat beats for each act.
Зависимости: CORE-001, GODOT-001. Владелец: None.
Готово когда: Каждый акт имеет explicit narrative lock и список production dependencies.
Evidence: docs/urman_knowledge_base/narrative.md

### NARR-002 [blocked]

Провести татарский и культурно-религиозный review authored Acts 2–5 (milestone M3 — Narrative lock for Acts 2–5, deferred).
Результат: Носитель татарского и культурный консультант проверили слова, религиозный контекст, фольклорные роли и тексты документов.
Зависимости: NARR-001. Владелец: None.
Готово когда: Нет blocker/serious findings по татарским ключам, Тимуру хәзрәту, фольклорным существам и историческим интерпретациям.
Evidence: docs/urman_knowledge_base/playtest_plan.md

### ASSET-001 [completed]

Произвести and verify modular environment kit (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Rebuildable `.blend`/`.glb` kit with deterministic LOD1 variants, painterly materials and documented provenance.
Зависимости: BASE-002. Владелец: None.
Готово когда: Environment families have 49 deterministic LOD1 meshes, including the bounded HouseA facade, WellA/WoodpileA/GateA anchors and explicit Godot visibility ranges.
Evidence: docs/urman_knowledge_base/assets.md

### ASSET-002 [completed]

Произвести project-original character silhouette kit (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Nine canonical character prefixes with anchors, face landmarks, layered clothing and deterministic LOD0/LOD1 meshes.
Зависимости: ASSET-001. Владелец: None.
Готово когда: 143 matching character LOD pairs, face landmarks and nine ground anchors pass asset verification.
Evidence: docs/urman_knowledge_base/decision_log.md

### ASSET-003 [blocked]

Создать hero faces, clothing and authored character animation (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Production-grade visible models for important NPCs with faces, clothing materials, animation-safe rigs and licenses/provenance.
Зависимости: GODOT-003, NARR-001, ASSET-002. Владелец: None.
Готово когда: Ключевые NPC больше не выглядят как silhouette-only proxies.
Evidence: docs/urman_knowledge_base/assets.md

### ASSET-004 [in_progress]

Заменить greybox dressing и принять authored mesh collision (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Production-grade authored zones for Acts 2–5 with reviewed mesh colliders, LODs, lighting and interaction affordances.
Зависимости: GODOT-003, NARR-001, ASSET-003. Владелец: act1-village-rework.
Готово когда: Все 12 зон имеют достаточную lived-in detail density и не читаются как procedural greybox.
Evidence: docs/urman_knowledge_base/art/fullgame_frames/README.md

### ASSET-005 [completed]

Создать и проверить Painterly texture production candidates (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Six non-destructive 1 024 × 1 024 RGB v2 painterly albedo candidates, six focused v3 siblings, focused v4 and v5 earth/wood comparisons, and a focused v6 earth/wood rework, with prompts, provenance, deterministic image metrics and Godot candidate captures.
Зависимости: ASSET-001. Владелец: None.
Готово когда: Weathered wood, aged plaster, damp earth, pine foliage, mossy stone and old fabric exist under exact *_v2_albedo.png names without overwriting v1.
Evidence: docs/urman_knowledge_base/art/texture_candidates_generation.md

### ASSET-006 [completed]

Проверить host-independent provenance реестра ассетов (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Fail-closed stdlib preflight for IDs, licenses, safe paths and source/derived SHA-256, plus typed Blender host-toolchain status.
Зависимости: ASSET-001, ASSET-002. Владелец: None.
Готово когда: Positive registry preflight verifies every derived file and every explicit local source hash without launching Blender.
Evidence: assets/asset_registry.json

### ASSET-007 [completed]

Разделить semantic material owners для imported wood и cloth (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Bounded owner-specific world scales for imported facade/fence/furniture/bark meshes and an explicit textured character-cloth descriptor.
Зависимости: ASSET-005, ASSET-006. Владелец: None.
Готово когда: Imported HouseA/FenceA/TableA/PineA meshes report the intended semantic material owner without shader or geometry changes.
Evidence: docs/urman_knowledge_base/decision_log.md

### ASSET-008 [completed]

Подключить authored HouseA к каноническому эпилогу (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Presentation-only project-original HouseA module in the canonical Act 5 epilogue with explicit variant, LOD/material-owner assertions and a fresh full-game frame.
Зависимости: ASSET-002, ASSET-007. Владелец: None.
Готово когда: fullgame_act5_epilogue attaches the act5-epilogue-house variant and keeps gameplay/narrative state in the existing zone wrapper.
Evidence: docs/urman_knowledge_base/art/fullgame_frames/README.md

### ASSET-009 [review]

Подключить authored WellA, WoodpileA и GateA к village dressing (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Blender-authored low-poly well, woodpile, threshold gate and bounded OldPc hero details with deterministic LOD1, registry provenance and presentation-only Act 2/Act 3/Act 5 dressing.
Зависимости: ASSET-001, ASSET-006, ASSET-008. Владелец: None.
Готово когда: The modular Blender source and GLB contain WellA, WoodpileA, GateA and OldPc DriveSlot/LabelPlate prefixes and the verifier reports the 49-mesh LOD1 contract.
Evidence: assets/asset_registry.json

### AUDIO-001 [blocked]

Произвести authored ambience, voice, captions and non-audio cues (milestone M4 — Production assets, audio and world presentation, deferred).
Результат: Licensed/project-original ambience and authored voice moments with captions, transcripts and equivalent non-audio cues.
Зависимости: NARR-001, GODOT-004. Владелец: None.
Готово когда: Critical Chapter 1 and Acts 2–5 audio requests resolve to real resources or intentionally authored non-audio cues.
Evidence: docs/urman_knowledge_base/playtest_plan.md

### QA-001 [blocked]

Провести внешнюю accessibility and first-time usability review (milestone M5 — External QA and release acceptance, deferred).
Результат: Observed playtest covering motion comfort, readability, subtitles/audio descriptions, keyboard/gamepad parity and comprehension.
Зависимости: GODOT-003, AUDIO-001, ASSET-004. Владелец: None.
Готово когда: Нет blocker/serious issues по motion comfort, text scale, contrast, captions and input prompts.
Evidence: docs/urman_knowledge_base/playtest_plan.md

### REL-001 [blocked]

Получить M1 and Windows release-class performance evidence (milestone M5 — External QA and release acceptance, deferred).
Результат: Raw frame-time evidence at 1080p for M1-class Mac and medium Windows PC across low/medium/high presets.
Зависимости: ASSET-004. Владелец: None.
Готово когда: Low preset is at least 30 FPS on Apple M1-class hardware.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/90-evidence.md

### REL-002 [blocked]

Проверить macOS and Windows desktop exports on hosts (milestone M5 — External QA and release acceptance, deferred).
Результат: Executable macOS and Windows builds launched on their target hosts with save/input/font/GLB checks.
Зависимости: ASSET-004, AUDIO-001. Владелец: None.
Готово когда: macOS and Windows exports launch without host-specific errors.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/90-evidence.md

### REL-003 [blocked]

Пройти полный 6–8-hour acceptance path (milestone M5 — External QA and release acceptance, deferred).
Результат: Full observed path: arrival → Marat investigation → pact → historical layers → pact destruction → tragic epilogue.
Зависимости: NARR-002, QA-001, REL-001, REL-002. Владелец: None.
Готово когда: Каноническая трагическая концовка достигается без debug commands и альтернативных финалов.
Evidence: (архив aegis удалён при чистке 2026-09-18; см. git-историю) work/2026-08-10-godot-full-migration/20-checkpoint.md

### REL-004 [blocked]

Выполнить absence-gated web runtime retirement (milestone M6 — Final web retirement and cutover, deferred).
Результат: Production no longer launches TypeScript/Vite/Three.js/browser runtime; old web version remains only in Git history/archive tag.
Зависимости: REL-003. Владелец: None.
Готово когда: Static absence gate finds no runnable production web entrypoint, Node/Vite/Three.js command or browser persistence fallback.
Evidence: docs/urman_knowledge_base/decision_log.md

### LEN01 [in_progress]

LEN01: устранить причины короткого акта и обеспечить минимум 60 минут первого основного опыта (milestone M5 — External QA and release acceptance, deferred).
Результат: Воспроизведение, карта сценария и причин, задачи исправления и честный хронометраж обязательного прохода.
Зависимости: —. Владелец: root/len01_content + root.
Готово когда: Причины короткого прохождения воспроизведены и измерены на репозитории.
Evidence: docs/production/act1_len01_playtime_audit_2026-09-15.md

### LEN01.1 [in_progress]

Карта переживаемых эпизодов обязательного пути (milestone M5 — External QA and release acceptance, deferred).
Результат: Сценарная опора → сцена/диалог/документ → доступное действие → изменение понимания → условие продолжения; отдельно основной и необязательный опыт.
Зависимости: —. Владелец: root/len01_content + root.
Готово когда: Каждый обязательный эпизод реализован в доступном пути; написанное, автоматически засчитанное и переживаемое игроком различены.
Evidence: docs/production/act1_len01_playtime_audit_2026-09-15.md

### LEN01.2 [in_progress]

Расширить ядро расследования из трёх однострочных сравнений (milestone M5 — External QA and release acceptance, deferred).
Результат: Связная цепочка расследовательских действий: выбрать вопрос, получить источник, сопоставить конкретные сведения, проверить ошибочную версию, перечитать и применить понимание в месте. Размер текста не является критерием.
Зависимости: LEN01.1. Владелец: root/len01_content + root.
Готово когда: Выводы основаны на действиях и прочитанных источниках, не выдаются приветствием или входом в сцену.
Evidence: docs/production/act1_len01_playtime_audit_2026-09-15.md

### LEN01.3 [in_progress]

Проверить полное чтение обязательных документов и повторное чтение с новыми знаниями (milestone M5 — External QA and release acceptance, deferred).
Результат: Обязательные документы доступны полностью, исходная улика выдаётся при открытии источника; применение нового понимания при повторном чтении требует собственного действия. Не вводить обязательный таймер или scroll-gate.
Зависимости: —. Владелец: root/len01_content + root.
Готово когда: Проверка подтверждает доступность полного текста и дополнительного сопоставления после перечитывания; понимание человеком проверяется отдельно.
Evidence: docs/production/act1_len01_playtime_audit_2026-09-15.md

### LEN01.4 [in_progress]

Отделить основной опыт от полезного добровольного исследования (milestone M5 — External QA and release acceptance, deferred).
Результат: У основных выводов есть путь без секретов; добровольные находки дают человеческую реакцию, понимание или удобный маршрут и не включаются в обязательный минимум времени.
Зависимости: LEN01.1. Владелец: None.
Готово когда: Основной путь проходим без скрытого K-счётчика и без требования найти все необязательные места; альтернативные источники имеют понятную пользу.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13.2

### LEN01.5 [blocked]

Честный хронометраж обычного прохождения (milestone M5 — External QA and release acceptance, deferred).
Результат: Замер обычного прохождения человеком и сравнение с целью.
Зависимости: LEN01.1. Владелец: None.
Готово когда: Есть реальный незнакомый игрок, идентификатор сборки, журнал действий и времени: минимум 60 активных минут первого основного опыта, отдельно быстрый знакомый маршрут, исследование, загрузки, паузы и потери из-за ошибок.

### LEN01.6 [in_progress]

§13.19 LEN01.6: подтверждённые исправления короткого прохождения (milestone M5 — External QA and release acceptance, deferred).
Результат: Каждый подтверждённый дефект привязан к существующему owner и исправлен; до правки — точный пример, после — тот же сценарий и затронутый сосед.
Зависимости: —. Владелец: root/len01_content + root.
Готово когда: Возвращён потерянный эпизод согласованного сценария или починено неправильное автоматическое условие, доступный разговор, загрузка контента либо обход финала.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13.19 LEN01.6

### LEN01.7 [in_progress]

§13.19 LEN01.7: целевой ритм основного опыта (milestone M5 — External QA and release acceptance, deferred).
Результат: Для строк LEN01.2: потерянное содержательное действие, принятое изменение, реально наблюдаемое время либо not-run; минимум 60 минут обычного первого основного опыта без секретов и искусственного добора.
Зависимости: LEN01.6. Владелец: root/len01_content + root.
Готово когда: В каждой части понятны смысл, действие игрока и изменение понимания.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13.19 LEN01.7

### LEN01.8 [blocked]

§13.19 LEN01.8: приёмка LEN01 и обязательный отчёт (milestone M5 — External QA and release acceptance, deferred).
Результат: Отчёт: проверяемая сборка/режим, воспроизведены ли 5–6 минут, заполненная матрица опор, подтверждённые причины с файлами/ID, выполненные исправления и различающиеся хронометражи.
Зависимости: LEN01.6, LEN01.7. Владелец: act1-village-rework.
Готово когда: Показано, что ни одна обязательная часть не исчезла, знания не выдаются ошибочно и финал имеет нужные предпосылки.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13.19 LEN01.8

### ACT1-EX01 [in_progress]

§13 EX01: Поднимать, переносить, ставить (milestone M5 — External QA and release acceptance, deferred).
Результат: Три класса вещей с физически работающим повторным применением
Зависимости: ACT1-EX00. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX02 [in_progress]

§13 EX02: Несколько решений одной задачи (milestone M5 — External QA and release acceptance, deferred).
Результат: Три локальных эпизода, каждый имеет два естественных разных решения и воспринимаемый результат; отказ, возвращение и ошибка не ломают сюжет.
Зависимости: ACT1-EX00. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX03 [in_progress]

§13 EX03: Многоцелевые инструменты (milestone M5 — External QA and release acceptance, deferred).
Результат: Лопата и шест: по два осмысленных применения каждого при наведении на конкретную физическую цель; циклическое переключение удалённых эффектов у стойки не засчитывается.
Зависимости: ACT1-EX00, ACT1-EX01. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX04 [in_progress]

§13 EX04: Ограниченные сочетания (milestone M5 — External QA and release acceptance, deferred).
Результат: Две понятные комбинации, создающие полезное действие
Зависимости: ACT1-EX01, ACT1-EX03. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX05 [in_progress]

§13 EX05: Читаемый и изменяемый снег (milestone M5 — External QA and release acceptance, deferred).
Результат: Цепочка следов, расчистка, устойчивое открытие
Зависимости: ACT1-EX03. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13, docs/production/act1_takeover_evidence_2026-09-16/carry_frames/
2026-09-19 (срез слияния `21d041e`): carry smoke проходит new game, переносы, узкий проём, EX06-фонарь и save/load, но останавливается на втором подъёме по боковой площадке бани (за топором); первый подъём за лопатой, расчистка и спуск проходят. Маршрут проходил на B42 (2026-09-17) до новой шаговой/обзорной логики `1c1a90c`; разбор — `weak_points.md`, лог — `docs/production/act1_takeover_evidence_2026-09-16/act1_carry_smoke_2026-09-19.log`. PASS за этот прогон не заявлен.

### ACT1-EX06 [in_progress]

§13 EX06: Переносной свет (milestone M5 — External QA and release acceptance, deferred).
Результат: Фонарь в руке и на опоре, помогает осмотреть два места
Зависимости: ACT1-EX00, ACT1-EX01. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX07 [in_progress]

§13 EX07: Тепло/холод/материал (milestone M5 — External QA and release acceptance, deferred).
Результат: Две локальные бытовые реакции с причиной и результатом
Зависимости: ACT1-EX00, ACT1-EX03. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX08 [in_progress]

§13 EX08: Поиск и проверка звуком (milestone M5 — External QA and release acceptance, deferred).
Результат: Три различимых звуковых эпизода: локальный источник, направленность/материал, действие и результат; видимый эквивалент сохраняет возможность играть без слуха.
Зависимости: ACT1-EX00. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX09 [in_progress]

§13 EX09: Человеческая вертикальность (milestone M5 — External QA and release acceptance, deferred).
Результат: Подъём по лестнице и низкий проход с безопасным возвратом
Зависимости: ACT1-EX01. Владелец: root/geometry_architecture + root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX10 [in_progress]

§13 EX10: Небольшие самостоятельные места (milestone M5 — External QA and release acceptance, deferred).
Результат: Три небольших функциональных пространства с различными действиями, полезными открытиями и безопасным понятным возвратом.
Зависимости: ACT1-EX06, ACT1-EX09. Владелец: root/geometry_architecture + root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX11 [in_progress]

§13 EX11: Микрозагадки на странность (milestone M5 — External QA and release acceptance, deferred).
Результат: Три различимых наблюдения и проверки догадки
Зависимости: —. Владелец: root + root/len01_content.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX12 [in_progress]

§13 EX12: Найти место по фото/рисунку (milestone M5 — External QA and release acceptance, deferred).
Результат: Два сопоставления изображения с местом и отличием
Зависимости: —. Владелец: root + root/len01_content.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX13 [in_progress]

§13 EX13: Цепочки любопытства (milestone M5 — External QA and release acceptance, deferred).
Результат: Две цепочки пространственных открытий с возвратом
Зависимости: —. Владелец: root + root/len01_content.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX14 [in_progress]

§13 EX14: Устойчивый результат (milestone M5 — External QA and release acceptance, deferred).
Результат: Проход, звук/свет и реакция жителя сохраняют последствия действий
Зависимости: ACT1-EX00. Владелец: root/exploration_runtime.
Готово когда: Минимальный законченный результат §13.2 достигнут и проверен обычным управлением.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13

### ACT1-EX-ACCEPT [in_progress]

§13.18: единый учёт приёмки EX01–EX14 (milestone M5 — External QA and release acceptance, deferred).
Результат: 14 строк §13.18, связанные с существующими ACT1-EX01…EX14: объект и маршрут, требуемое постоянство, реальные object/interaction ID, runtime owner, путь реализации, ввод, SHA, evidence и фактический статус.
Зависимости: ACT1-EX00. Владелец: root.
Готово когда: В каждой строке заполнены реальные object/interaction ID, runtime owner и путь реализации.
Evidence: docs/research_shurale_package/URMAN_ACT1_VILLAGE_REWORK_PLAN_RU.md §13.18
