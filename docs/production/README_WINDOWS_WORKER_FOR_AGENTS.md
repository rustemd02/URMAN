# Windows-стенд: кратко для агента

Полная настройка, подключение Mac/второго разработчика/Cloud, ограничения и восстановление:
[WINDOWS_TEST_STATION_RU.md](WINDOWS_TEST_STATION_RU.md). Сначала прочитай также корневой `AGENTS.md`.

Разрабатывай в текущем checkout. Когда поручение требует игровой проверки или PNG,
отправляй текущие исходники на Windows через `eng/remote-check.py`. Клиент передаёт
незакоммиченные изменения, а станция проверяет хеши и собирает свежую DLL.

### Что уходит по сети

Полный снимок — около 420 МБ, поэтому клиент сначала спрашивает станцию, каких файлов
у неё нет, и отправляет только их (обычная правка кода — сотни килобайт, повторный
запуск на том же дереве — ничего). В выводе это видно строкой `Incremental snapshot:
station already has N of M files, sending K`. Станция хранит по блобу на содержимое
файла и собирает дерево с проверкой каждого SHA256.

Это работает только если станционный работник обновлён: сделай на Windows
`git pull --ff-only origin main` и перезапусти задачу
(`eng\manage-windows-worker.ps1 -Action stop`, затем `-Action start`). На необновлённой
станции клиент не падает, а автоматически откатывается на полную отправку архива —
в выводе будет `Full snapshot: this station has no incremental support`.

Нужны Python 3.12+, доступ к Windows-узлу в Tailscale и личный токен вне Git.
На Mac конфигурация по умолчанию — `~/.config/urman-station/client.json`:

```json
{"url":"https://unterpc.tail9423b1.ts.net","token_file":"~/.config/urman-station/token"}
```

### Если имя станции не разрешается

Симптом: `curl: (6) Could not resolve host: unterpc.tail9423b1.ts.net`, и то же самое
у любого инструмента (не только внутри sandbox). Причина не в правах и не в токене:
на этом Mac служба Tailscale работает без системного туннеля (CLI в режиме userspace,
без root), поэтому MagicDNS не прописан и системного имени `*.ts.net` нет. Ходить к
станции в таком режиме можно только через локальный прокси демона. Добавь его в тот же
`client.json` — клиент сам подставит прокси, никаких переменных окружения не нужно:

```json
{"url":"https://unterpc.tail9423b1.ts.net","token_file":"~/.config/urman-station/token","proxy":"http://127.0.0.1:1055"}
```

Принимается только loopback-адрес (иначе клиент откажет: токен не должен идти через
чужой прокси). Переменные `HTTPS_PROXY`/`URMAN_STATION_PROXY` продолжают работать.
Штатный способ без прокси — обычное приложение Tailscale с системным туннелем.

### Если `tailscale status` говорит «failed to connect to local tailscaled»

Симптом: `dial unix /var/run/tailscaled.socket: connect: no such file or directory`.
Это **не** значит, что сеть или станция недоступны: демон запущен пользовательским
агентом и слушает нестандартный сокет, потому что без root путь
`/var/run/tailscaled.socket` создать нельзя. Список узлов смотри так:

```sh
tailscale --socket="$HOME/.config/urman-station/tailscaled.sock" status
```

Для проверок станции сокет не нужен вовсе: `eng/remote-check.py` ходит через прокси из
`client.json`. Поэтому вывод «доступа к Windows нет» по одной только этой ошибке
неверен — сначала выполни `doctor` и смотри его `ready`.

Из корня репозитория:

```sh
python3.12 eng/remote-check.py doctor
# Выполняй только нужную и разрешённую поручением проверку:
python3.12 eng/remote-check.py smoke --timeout 300
python3.12 eng/remote-check.py capture --points 'station:Ground@-14,3,15>Ground@0,2,0' --timeout 300
# Если связь прервалась, используй уже напечатанный ID, не создавай второй запуск:
python3.12 eng/remote-check.py status --job-id <id>
python3.12 eng/remote-check.py fetch --job-id <id>
```

### Если `CERTIFICATE_VERIFY_FAILED`

Клиент проверяет сертификат станции обычным образом, поэтому нужен Python с рабочим
набором корневых сертификатов. На этом Mac так работает `python3.12` из Homebrew
(использует `/opt/homebrew/etc/openssl@3/cert.pem`). Интерпретатор python.org
(например `/Library/Frameworks/Python.framework/Versions/3.11/bin/python3`) падает с
`unable to get local issuer certificate`: у него нет связки сертификатов, пока не
запущен `/Applications/Python 3.11/Install Certificates.command`. Это не проблема
станции, токена или сети — только интерпретатора.

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
