# Windows-стенд: кратко для агента

Полная настройка, подключение Mac/второго разработчика/Cloud, ограничения и восстановление:
[WINDOWS_TEST_STATION_RU.md](WINDOWS_TEST_STATION_RU.md). Сначала прочитай также корневой `AGENTS.md`.

Разрабатывай в текущем checkout. Когда поручение требует игровой проверки или PNG,
отправляй текущие исходники на Windows через `eng/remote-check.py`. Клиент передаёт
незакоммиченные изменения, а станция проверяет хеши и собирает свежую DLL.

Нужны Python 3.12+, доступ к Windows-узлу в Tailscale и личный токен вне Git.
На Mac конфигурация по умолчанию — `~/.config/urman-station/client.json`:

```json
{"url":"https://unterpc.tail9423b1.ts.net","token_file":"~/.config/urman-station/token"}
```

Из корня репозитория:

```sh
python3 eng/remote-check.py doctor
# Выполняй только нужную и разрешённую поручением проверку:
python3 eng/remote-check.py smoke --timeout 300
python3 eng/remote-check.py capture --points 'station:Ground@-14,3,15>Ground@0,2,0' --timeout 300
# Если связь прервалась, используй уже напечатанный ID, не создавай второй запуск:
python3 eng/remote-check.py status --job-id <id>
python3 eng/remote-check.py fetch --job-id <id>
```

Результаты: `.codex-captures/remote/<id>/` (или `--output <папка>`).
Принимай результат по `receipt.json`, журналам и PNG: статус PASS, точный snapshot,
коды выхода 0, отсутствие engine errors, подтверждённое восстановление userdata.
Наличие одной картинки не означает PASS. Одновременно станция выполняет одно задание.

При недоступности станции сообщи причину и оставь проверку not-run; автоматического
локального Godot fallback нет. Явную просьбу пользователя «запусти поиграть» выполняй
локально существующим безопасным launcher. Отдельный запрет игровых запусков сохраняется.

Windows-приёмка выполнена: короткий native smoke и PNG 1920×1080 на GTX 970.
Соединения с Mac, вторым Mac и Cloud пока не проверены. Windows должен быть включён,
пользователь должен войти в разблокированную desktop-сессию. Не включай публичный Funnel.
Не публикуй токены, не меняй закреплённые версии инструментов ради запуска.

Остановка и отключение автозапуска на Windows после завершения задания:

```powershell
& 'C:\Users\ruste\Documents\GitHub\URMAN\eng\manage-windows-worker.ps1' -Action disable
```
