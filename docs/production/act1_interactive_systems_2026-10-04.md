# Игрок, транспорт, радио, магазин — 2026-10-04 (ACT1-PLAYER, ACT1-TRANSPORT, ACT1-RADIO, ACT1-SHOP)

Проверено по коду и данным; игра не запускалась. Для всех четырёх карточек `done_when`
требует подтверждения **на точной текущей сборке** — этого здесь нет и не подменяется.

## ACT1-PLAYER: прыжок, бег, приседание, видимое тело

| Требование | Что подтверждено в коде |
|---|---|
| Прыжок | `JumpHeight = .42` и `TryJumpFromGround` с `Input.IsActionJustPressed("jump")` и проверкой опоры; подъём считается как `sqrt(2·g·h)`, есть счётчик `GroundedJumps` |
| Бег | `IsSprinting` + `SprintMultiplier`; в позе скорость = `WalkSpeed·.58` при приседе, `WalkSpeed·SprintMultiplier` при беге |
| Приседание | `CrouchedHeight = 1.08`, `CanCrouchAt(feet)` через `CanFitAt`, высота головы берётся из `crouched ? CrouchedHeight : _standingHeight` |
| Видимое тело | первое лицо цепляет `GeneratedCharacterKitDressing.Attach(this, "aidar-first-person", …)` с именем `AidarLowerBody` и подгонкой подошв по опоре |

Все четыре пункта реализованы; «подтверждено проверкой на точной сборке» — отдельный прогон.

## ACT1-TRANSPORT: Нива, мотоцикл, лошадь с телегой

`game/content/vehicles/act1_vehicles.v1.json` содержит **три** определения:

| id | тип | сиденье | габарит корпуса |
|---|---|---|---|
| `babay-niva` | Niva | (−0.40, 0.65, 0.12) | 1.82 × 1.80 × 4.20 |
| `village-motorcycle` | Motorcycle | (0, 1.015, 0.40) | 1.00 × 1.54 × 2.24 |
| `forest-horse-cart` | HorseCart | (0, 1.10, 0.42) | 1.80 × 2.45 × 5.78 |

То есть «лошадь и телега» — это один состав `HorseCart` (лошадь везёт телегу), отдельных
определений «верхом на лошади» в данных нет. Транспорт различается и по режиму дорожного
графа (`SettlementTravelMode.Car/Motorcycle/HorseCart`), а посадка водителя в Ниве измерена
отдельно: камера `DriverLook` = сиденье + 0.75 = 1.40 при потолке салона 1.64
(`act1_niva_cabin_2026-10-04.md`). Модель Нивы — авторский Blender-ассет с проверенным
происхождением (`roadVehicleGeometryRevision = 4`, `assetOrigin = …urman_niva.glb`).

## ACT1-RADIO: татарское местное радио

- Проигрыватель `VehicleRadioPlayer.cs` держит **диал из двух станций**:
  `avyl_radio.v1.json` («Авыл FM · 101,4», 101.4 МГц) и `kirlay_archive_radio.v1.json`
  (архивная станция; «Кырлай» — историческое имя деревни по канону).
- В `avyl_radio.v1.json` **12 сегментов**, `fictionalStation: true` (вымышленная станция,
  реальные не имитируются) и две честные записи о состоянии:
  `audioAcceptance` = «1 recorded segment(s), 1 explicitly declared archive repeat,
  10 synthetic…» и `languageReview` = «native Tatar review pending».
- Это ровно тот случай, когда принятие не выдаётся за сделанное: одна записанная часть и
  десять синтетических помечены в самом файле, а разбор носителем татарского остаётся
  внешним гейтом.

## ACT1-SHOP: магазин и атомарная долговая тетрадь

- Каталог и интерфейс: `ShopCatalog.cs`, `ShopUi.cs`, `RuntimeBridge.Shop.cs`,
  контент `game/content/urman.shop.v1.json`.
- Атомарность подтверждена комментарием и устройством кода: «Both item ownership and the
  debt-book row commit in one kernel plan» (`RuntimeBridge.Shop.cs:97`) — то есть получение
  предмета и запись долга фиксируются одним планом, а не двумя независимыми шагами. Это и
  есть требование карточки; поведенческая проверка на текущей сборке — прогон.

## Итог

| Карточка | Статически подтверждено | Осталось |
|---|---|---|
| PLAYER | прыжок 0.42, спринт, присед 1.08, видимое тело AidarLowerBody | проверка на точной сборке |
| TRANSPORT | три определения, режимы дорожного графа, измеренный салон Нивы | поведение на текущей сборке |
| RADIO | диал из 2 станций, 12 сегментов, честные audioAcceptance и languageReview | запись остальных сегментов и разбор носителем |
| SHOP | каталог/UI/мост + один план коммита для предмета и долга | прогон |
