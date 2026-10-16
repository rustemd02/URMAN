# Guarded W02/home capture — stdout (2026-10-16)

Сохранён существующий stdout/terminal защищённого захвата W02/home, без повторного
прогона (запрос @Кодекса Кадыра, 16.10 22:56: renderer + restore-подтверждение
рядом с текущими карточками).

## Прогон

- Команда: `sh eng/capture-fullgame-frames.sh` — импорт ассетов, затем
  `python3 eng/protected_run.py --clean --timeout 1200 <godot> --log-file ... --path game res://tests/full_game_dressing_capture.tscn`.
- Это последний защищённый прогон захвата перед коммитом `415df477` (16.10, ~22:04).
  Он произвёл все 22 кадра: 7 зонных (акты 2–5) + 12 W02 + 3 дома.
- Зонные PNG после прогона возвращены к HEAD и в коммит не вошли; закоммичены
  12 W02 + 3 home-кадра именно этого прогона.

## Renderer и restore (строки из логов)

- `Godot Engine v4.7.1.stable.mono.official.a13da4feb`
- `Metal 4.0 - Forward+ - Using Device #0: Apple - Apple M4 Pro (Apple9)`
  (локальный Mac-прогон; это не Windows native Vulkan-подтверждение)
- Хвост stdout: `userdata guard: original files and permissions restored and verified`
- `ERROR:`-строк нет; в хвосте известное предупреждение `7 RIDs of type "Texture" were leaked`
  (шум движка, не провал гейта скрипта).

## Файлы

| Файл | Что это |
|---|---|
| `capture.stdout.log` | полный stdout/stderr защищённого процесса захвата |
| `capture.godot.log` | лог движка того же процесса (`--log-file`) |

## Кадры этого прогона (SHA-256, сверено 2026-10-17)

| Кадр | SHA-256 |
|---|---|
| `godot_w02_entry_1080p.png` | `028e77e32c295402e89d3a584d3a8818afd93ec28e73228af13e8ffb1053cd17` |
| `godot_w02_vista_1080p.png` | `7e6fc66cf90f497351b795cd59e5d4d4cce80e0d3e7b47dc5faf5121c8a27ce5` |
| `godot_w02_bay_1080p.png` | `252944d919fb3c2910b374a03cc55259d03648fa56b11a0f130f94090849ef20` |
| `godot_w02_bay_outside_1080p.png` | `7c0e95bf5797e59b2e95e53a5ef2297746d849c1694e915122882ea357ef401d` |
| `godot_w02_bay_high_1080p.png` | `bbec6e50ead2b37f302902b24111ad76aa143b5431088cb9dc1a626d6e901200` |
| `godot_w02_reflection_1080p.png` | `efe838ad8b6d8caa07060ae3a6f7ce60579e6c3c6649c1a7b7f4b1a50c1e5aea` |
| `godot_w02_reed_turn_1080p.png` | `9f2e80c92081d85b418edf4bded0b2c1363f74a3970e8007c6a7da37dae123c1` |
| `godot_w02_reed_open_1080p.png` | `2336054a3a79578df1ea23cb5fd86be881fb0394d1ae2f310e1c6d870bd5ede4` |
| `godot_w02_old_approach_1080p.png` | `5b0ca7a5ce0934cdae24bffc037d725e6e063e327bcb06a105d42c8bc36ba802` |
| `godot_w02_far_pier_1080p.png` | `49aa342e8e261725ea44ed2ac257e3add8fae53a384ced8f49f31dffe556a5d9` |
| `godot_w02_answer_1080p.png` | `c98ba10047bfe3ea7890fa389cc7cc697c99d92f17f1b69d697b60935a37cc8e` |
| `godot_w02_summer_bank_1080p.png` | `bbbbc8b6aeeed6282e810104eb43c7ce676a9903b3ef98a9dcfaca8a4f6eac32` |
| `godot_home_water_table_1080p.png` | `1c0a8aa01cfb6f01ea0d7c99b9c1517b4d3e19dc5c5b856fd3ce68698278fe80` |
| `godot_home_water_book_1080p.png` | `68fabace66bf1826b45d95cfef7f8f4da1f8629b1ca3f9a616b9f3bd29b9609c` |
| `godot_home_water_envelope_1080p.png` | `fd583dc8be28d7955a86cf17a738f27b85250791da1f33834ad28c7a67493119` |

Хеши кадров `godot_w02_entry/bay/reflection/old_approach/answer` и
`godot_home_water_book/envelope` совпадают один в один с записью root-ревью
16.10 в карточках PW-050/PW-035/PW-071.

## Пределы (честно)

- Это постановочный захват: изолированный SubViewport/фиксированные камеры,
  `ApplyCaptureFacts` для reed_open/answer — не обычная ходьба игрока, не главный
  viewport и не станционный Windows-receipt. Обычный маршрут и повторное
  посещение остаются PW-054.
- Логи Mac-прогона (Metal); native Windows/станционный receipt этим не закрыт.
