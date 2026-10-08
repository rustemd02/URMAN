# ATMOS — запросы на кадры, карты и ассеты (VIS-022, VIS-028, VIS-040, VIS-042, VIS-043)

Дата: 2026-10-08. Автор запроса: дорожка «свет и цветововой сценарий».
Подробные числа и решения — `docs/production/visual_restyle_2026-10-07/ledger_ATMOS.md`.

Правило: ни один ассет не заказан без доказанного пробела. Текстуры, модели и
UV для этих пяти карточек **не требуются** — весь эффект делается солнцем, небом,
туманом, ответом материала и локальным светом в существующем Environment
(`URMAN_FINAL_STYLE_RECIPE_RU.md` §6). Нужны съёмка, один debug-хук и две
правки в чужих файлах.

## 1. Кадры (владелец CAPTURE / Windows-станция)

| ID | Точка (spec) | Зона / фаза | Para | Что проверяет |
|---|---|---|---|---|
| A-01 | `C1_arrival_street` | `village_day` / `village-winter-frost` | before (срез до правки) → after | VIS-040: дорога, фасад и кромка различимы в 320×180, белый снег не теряет рельеф, нет клиппинга |
| A-02 | `C7_trail_junction` (соседняя улица) | `village_day` / `village-winter-frost` | before → after | VIS-040: на соседнем виде нет проваленной чёрной стены |
| A-03 | `C1_arrival_street` | фаза `village-winter-frost`, кандидат A (ambient 0.62, sun 1.12) | probe | VIS-040 шаг 1: однофакторная проба ambient |
| A-04 | `C1_arrival_street` | фаза `village-winter-frost`, кандидат B (sun 0.82, ambient 0.92) | probe | VIS-040 шаг 1: однофакторная проба sun |
| A-05 | `C8_sky_silhouette` | `village_day` | before → after | VIS-040: небо и кроны, отсутствие вечного заката |
| A-06 | P18 (ночная кромка, подход к лесу) | `kara_urman_night` | before → after | VIS-042: три пространственных плана, путь различим, кадр не чёрный |
| A-07 | P18b тот же путь без сюжетного события | `kara_urman_night` | pair к A-06 | VIS-042 шаг 3: тревога не является следствием ошибки рендера |
| A-08 | `C1_arrival_street` | фаза `village-green-night` | after-only (эталон VIS-114) | N1+N2: зелёная ночь с хроматическим контрастом, антипример N3/N4 |
| A-09 | `C7_trail_junction` | `village-winter-frost`, weather fair → blizzard | пара на одной точке | VIS-043: направление снега и дыма совпадает, положение/геометрия стабильны, видимость дороги |
| A-10 | `C3_fence_close` + `C7_trail_junction` | `village_day` | before → after | VIS-028: стебли имеют основание, группы (не решётка) на 10 м, переход к нетронутому снегу |
| A-11 | зират: `zirat_road` / `zirat-entry` (P15) | `zirat_road` | after | VIS-022: вход и чищеная дорожка читаются, ограда ≈1 м |
| A-12 | зират: `zirat_road` / `zirat-grave-row` (P15b) | `zirat_road` | after | VIS-022: ряд могил, отсутствие запрещённых элементов |

Условия съёмки общие: `res://scenes/act1_demo.tscn`, 1920×1080, пресет `medium`,
FOV 70, `eng/remote-check.py capture --phase <id>`, метаданные кадра по VIS-004;
в receipt обязаны оказаться `atmosphereProfile`, `selectorSource`,
`atmosphereNearPlaneLuminance`.

## 2. Карты / документы (не ассеты)

* `docs/production/visual_reset_maps_2026-10-07/light_owners_RU.md` §5.8 — устаревает:
  после этой правки селектор читает `zones` из данных, профиль без зоны явно
  помечен как phase-only. Нужно перезаписать пункт в итерации 04.
* `docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07_checkpoints.json`
  — добавить точки A-06/A-07 (P18, P18b), A-11/A-12 (зират) и пару fair/blizzard
  для A-09; мне файл не передан на правку.

## 3. Правок в чужих файлах (HANDOFF, ассеты не нужны)

1. `game/scripts/MainMenuUi.cs`: две строки в `DebugZones`
   (`("zirat_road","zirat-entry","Зират · вход с дороги")`,
   `("zirat_road","zirat-grave-row","Зират · ряд могил")`).
   Consumer: отладочный проход и съёмка A-11/A-12. Проверка: пункт меню появляется
   только при `debug-zones.enabled`, прыжок не даёт прогресса.
2. `game/scripts/PainterlyMaterialLibrary.cs` + оконные семьи: тёплый отклик окна
   на состояние через `AtmosphereProfiles.Applied` (сейчас эмиссия
   `ffb45e`×1.6 постоянная). Consumer: зелёная ночь (N2), сумерки зирата.
   Проверка: кадр A-08 против A-01, тёплое семейство читается, холодное не сдвинуто.
3. `game/scripts/Act1ConnectedWorld.ZiratFamily.cs`: чтение
   `ZiratPlotLayout.RelocatedStones` (13 авторских координат с yaw) вместо линейной
   сетки и, только после подтверждения человеком, поворот рядов на ось 105.17°
   (перпендикуляр к параметру проекта `MosqueQiblaBearingDegrees = 195.1725°`).
   Проверка: `ziratCulturalAuditAxisDeviationDegrees` → 0 ± 2°.
4. `game/scripts/Act1DemoRoot*.cs`: debug-хук на `SetOpeningBlizzard(true)` вне
   сцены первой ночи (одна точка, без сюжетного прогресса) — иначе пара A-09
   недостижима на постоянной точке.
5. `game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs`: когда станция
   подтвердит кадры, `SetOpeningBlizzard` читает `weather` из профиля (один источник
   чисел), а не собственную копию.

## 4. Человеческий гейт (карточка требует явно)

* VIS-022: носитель языка / культурный консультант и автор подтверждают
  (а) ориентацию оси могил относительно параметра киблы проекта,
  (б) высоту и форму низких камней, (в) допустимость компилированных надписей
  на двух семейных камнях. До ответа статус — `HUMAN_GATE_OPEN`; автоматом
  ничего не закрывается.
* VIS-040: утверждение нового дневного профиля по двум связанным видам (A-01, A-02).
* VIS-042: степень тревоги кромки и допустимое состояние сюжетного события (A-06, A-07).
