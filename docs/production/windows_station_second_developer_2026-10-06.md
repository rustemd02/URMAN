# Подключение второго разработчика к Windows-станции УРМАН

Краткая памятка: как владелец открывает доступ и что делает второй разработчик (Кадыр,
`kad.akhunov@gmail.com`, свой tailnet `tailbfbb59.ts.net`, узел `macbook-kadyr-2`).
Общая схема и полная настройка — [WINDOWS_TEST_STATION_RU.md](WINDOWS_TEST_STATION_RU.md),
кратко для агента — [README_WINDOWS_WORKER_FOR_AGENTS.md](README_WINDOWS_WORKER_FOR_AGENTS.md).

Выбранный вариант — **А: расшарить только узел станции**. Второй разработчик остаётся
в своём tailnet и не становится участником tailnet станции.

## Часть 1. Владелец (один раз)

1. **Расшарить узел.** `login.tailscale.com/admin/machines` → узел `unterpc` → меню `⋯` →
   **Share** → `kad.akhunov@gmail.com`.
2. **Разрешить доступ.** `Access controls` → в политику добавить grant, других правил не
   трогать:

   ```json
   { "src": ["kad.akhunov@gmail.com"], "dst": ["100.75.184.88"], "ip": ["tcp:443"] }
   ```

   Сохранить и проверить, что правило действует (в консоли есть проверка политики).
3. **Передать личный токен.** На Windows-ПК взять
   `%LOCALAPPDATA%\URMAN-STATION\second-developer.token` и отправить защищённым каналом
   (менеджер паролей). Не через чат и не через Git. Taildrop между разными аккаунтами не
   работает — только внутри одного аккаунта.
   **Важно:** второму разработчику идёт `second-developer.token`, а не `owner.token`.
   Owner-токен даёт полный доступ владельца и не должен уходить второму человеку: станция
   не различает, кто именно прислал запрос, и отозвать такой доступ можно только сменой
   собственного токена.
4. **Держать станцию доступной.** Когда второй разработчик запускает проверку, Windows-ПК
   должен быть включён, пользователь — вошли в систему, рабочий стол **разблокирован**,
   работник станции запущен (`eng\manage-windows-worker.ps1 -Action start`).

## Часть 2. Второй разработчик

1. **Tailscale.** Войти своим аккаунтом `kad.akhunov@gmail.com` в свой tailnet. Не входить
   в tailnet владельца.
2. **Принять доступ.** Если в админке видно приглашение к расшаренному узлу — принять.
   Проверить:

   ```sh
   tailscale status | grep -i unterpc
   ```

   Ожидается строка узла `unterpc` с адресом `100.75.184.88`.
3. **Забрать изменения репозитория:**

   ```sh
   git status --short
   git pull --ff-only origin main
   ```

4. **Python 3.12+.** Проверить `python3.12 --version`; если нет — `brew install python@3.12`.
   Системный `python3` может быть старее и не подойти.
5. **Токен на место:**

   ```sh
   mkdir -p ~/.config/urman-station && chmod 700 ~/.config/urman-station
   # сохранить содержимое second-developer.token в ~/.config/urman-station/token
   chmod 600 ~/.config/urman-station/token
   ```

6. **Конфигурация клиента** (секрета не содержит) — `~/.config/urman-station/client.json`:

   ```json
   {"url":"https://unterpc.tail9423b1.ts.net","token_file":"~/.config/urman-station/token"}
   ```

   Если Tailscale на Mac работает без системного туннеля (CLI в режиме userspace, без
   root), системного имени `*.ts.net` не будет и `curl` упадёт с
   `Could not resolve host`. Тогда добавь в тот же файл локальный прокси демона:

   ```json
   {"url":"https://unterpc.tail9423b1.ts.net","token_file":"~/.config/urman-station/token","proxy":"http://127.0.0.1:1055"}
   ```

   Принимается только loopback-адрес. При обычном приложении Tailscale с системным
   туннелем это поле не нужно.

   Отдельно: если клиент падает с `CERTIFICATE_VERIFY_FAILED`, дело в интерпретаторе —
   ему не хватает корневых сертификатов. Используй Python из Homebrew (`python3.12`,
   у него есть `/opt/homebrew/etc/openssl@3/cert.pem`) или запусти
   `/Applications/Python 3.11/Install Certificates.command` для python.org-сборки.

7. **Проверить связь:**

   ```sh
   curl -sS -m 15 -o /dev/null -w '%{http_code}\n' https://unterpc.tail9423b1.ts.net/health
   ```

   Ожидается `401` — это правильно: туннель есть, авторизации в запросе нет.
   Затем:

   ```sh
   python3.12 eng/remote-check.py doctor
   ```

   Ожидается `"ready": true`.

8. **Запускать проверки:**

   ```sh
   python3.12 eng/remote-check.py smoke --timeout 300
   python3.12 eng/remote-check.py capture \
     --points 'arrival_forward:0,1.7,9>-0.6,1.2,-1.5;street_forward:-2.2,1.7,-8>0,1.1,-41' --timeout 300
   ```

   Результаты — в `.codex-captures/remote/<job-id>/`: `receipt.json`, журналы,
   `engine-errors.log`, `frames/*.png`.

## Если что-то не так

| Симптом | Причина и что делать |
|---|---|
| `curl`: `Could not resolve host` | Узел расшарен, но имя владельца не резолвится. Включить MagicDNS в своём tailnet и перепроверить `tailscale status`. Если имя так и не резолвится — вернуться к варианту Б (приглашение в tailnet владельца) и отдельно сообщить об этом. |
| `curl`: `403` или `404` | Нет grant в политике владельца либо указана не та идентичность. |
| `curl`: таймаут | Узел не расшарен или Windows-ПК выключен / Tailscale не запущен. |
| Всё висит, ничего не отвечает | На Mac включён интернет-прокси, который не маршрутизирует tailnet. Для одного назначения: `NO_PROXY='unterpc.tail9423b1.ts.net'`. |
| `doctor`: `"ready": false`, `desktop.input_desktop: false` | На Windows заблокирован экран. Разблокировать и запустить работника станции. |
| `doctor`: `"userdata_recovery_pending": true` | Остался маркер восстановления после сбоя. Не стирать его: сначала разобрать восстановление по маркеру, потом запускать. |
| `doctor`: `"busy_job": "<id>"` | Второй разработчик или владелец уже запустил проверку. Станция выполняет одно задание за раз. |
| HTTP `409` при отправке | То же: занято, в ответе придёт id активного задания. Дождаться его окончания. |
| Ответ на отправку потерялся | Не создавать второй запуск. Использовать напечатанный `job_id`: `status --job-id <id>`, затем `fetch --job-id <id>`. |

## Правила, которые важно не нарушать

- Один прогон за раз; при обрыве ответа пользоваться существующим `job_id`.
- Результат относится только к тому снимку, который подтверждён в `receipt.json`
  (`commit`, `snapshot`, `dirty paths`).
- `receipt.json` со статусом PASS и пустой `engine-errors.log` — техническая проверка.
  Это не художественная приёмка и не человеческий плейтест.
- Токен не передавать третьим лицам и не хранить в Git; при утечке владелец меняет токен
  в `station-config.json`.
- Игровые запуски допустимы только когда это разрешено текущим поручением автора.
