# Промпт для Codex на Windows UNTERPC

Настрой постоянный административный SSH-доступ с моего Mac к этой Windows через существующий Tailscale, чтобы Codex на Mac мог обслуживать репозиторий и worker без физического доступа. Это прямое разрешение установить и настроить Windows OpenSSH Server, службу sshd, нужные узкие правила Firewall и unattended mode Tailscale. Не открывай SSH в публичный интернет. Выполни настройку, а не ограничивайся планом.

Сначала прочитай AGENTS.md, docs/production/WINDOWS_TEST_STATION_RU.md, docs/production/README_WINDOWS_WORKER_FOR_AGENTS.md и соответствующие eng/manage-windows-worker.ps1, eng/run-windows-check.ps1, eng/windows_station_worker.py, eng/remote_common.py. Проверь реальные пути, пользователя, службы и активные задания. Ожидаемый checkout: C:\Users\ruste\Documents\GitHub\URMAN.

Проверенные на Mac параметры:

- Windows: unterpc.tail9423b1.ts.net, Tailscale IPv4 100.75.184.88.
- Mac: macbook-rustem, Tailscale IPv4 100.106.163.96.
- Публичный ключ Mac:

```text
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIKq1pIj6wDuoQ8zXdvRKMRFX37l/JJuu+eWnR+6Iq40e urman-station-macbook-rustem
```

Приватный ключ остаётся только на Mac: ~/.ssh/urman_station_ed25519. Не проси его, не копируй, не выводи пароли, station token или Tailscale auth keys.

## Настройка доступа

1. Используй Windows OpenSSH Server через Tailscale: сервер Tailscale SSH на Windows не поддерживается. Установи штатный OpenSSH Server, если отсутствует. Сохрани копию существующей конфигурации. Используй существующего владельца checkout, выясни точное имя для SSH и проверь его административные права. Не создавай новый аккаунт и не включай автологин.
2. Разреши только public-key authentication и точный аккаунт; отключи парольную и keyboard-interactive аутентификацию. Для администратора учти штатный Match Group administrators и файл %ProgramData%\ssh\administrators_authorized_keys с ACL только SYSTEM и BUILTIN\Administrators. Не затри существующие ключи. Проверь sshd -t до перезапуска.
3. TCP/22 разреши только от Tailscale IPv4 Mac 100.106.163.96 к Tailscale IPv4 Windows 100.75.184.88 через фактический Tailscale-интерфейс. Проверь активные правила, способные открыть порт шире; штатное широкое правило OpenSSH отключи после создания узкого. Не меняй посторонние правила. Если tailnet ACL/grants блокирует соединение, подготовь точечное правило Mac → UNTERPC:22 с сохранением доступа к станции на 443; не разрешай *:* и не обходи управляемую политику.
4. sshd должен запускаться автоматически, с перезапуском службы при сбое. Включи штатный Tailscale unattended mode и проверь автозапуск его службы. Сообщи срок действия ключа узла; не отключай его истечение без отдельного решения. Не перезагружай компьютер при активном задании или незавершённом восстановлении userdata.
5. Настрой стандартную shell SSH на PowerShell, чтобы удалённые команды обслуживания работали предсказуемо. Проверь фактические права SSH-сеанса: статус Administrator сам по себе не доказательство способности обслуживать службу. Не отключай UAC или другие системные защиты ради этого.

## Почини несовпадение runner

Два задания станции отказали до Godot:
0cd314c656ae4dfe9a185b36084808c6 и 58cb86f19954429b909d16ed0bb00ab2.
Ошибка: station execution infrastructure differs from snapshot: eng/run-windows-check.ps1; update station before retrying.
На Mac проверялись исходный runner 84635bfc и runner origin/main 747cb4d6 — оба не совпали с установленным.

Диагностируй фактический файл, который исполняет worker, его checkout, хеш и локальный diff. Сохрани локальные изменения. Не отключай source-match, protected_run или recovery guard. Не используй reset, clean, stash, force checkout и не заменяй грязные файлы вслепую. Если checkout чистый и main не разошёлся, обнови через git pull --ff-only origin main. Для грязного checkout разберись с конкретным diff и сохрани его отдельно перед согласованием runner; не теряй полезную работу. Обслуживание worker выполняй штатным manage-windows-worker.ps1 только после завершения активного задания и проверки recovery/lock-состояния. Worker должен сохранить предусмотренную интерактивную сессию и права; не переноси его в SYSTEM.

Действующий main содержит изменения интерфейса и res://tests/exploration_ui_capture.tscn. После ремонта используй штатный station doctor и защищённый smoke этой сцены с timeout 300 через loopback по инструкции станции. Сохрани receipt, журнал и реальные PNG; проверь результат, а не только exit code клиента. Не запускай игру прямо из авторингового checkout и не выдавай технический capture за человеческое прохождение.

## Приёмка

На Windows проверь конфигурацию sshd, службы, разрешения ключа, firewall и журнал входа. Выведи fingerprint SSH host key, точный SSH username и команду подключения. Не подменяй проверку с Mac локальным ssh localhost. Не отключай StrictHostKeyChecking.

На этом Mac Tailscale работает в userspace через HTTP proxy 127.0.0.1:1055, поэтому проверочная команда после сверки и сохранения host key:

```sh
ssh -i ~/.ssh/urman_station_ed25519 -o IdentitiesOnly=yes -o BatchMode=yes -o PasswordAuthentication=no -o 'ProxyCommand=nc -X connect -x 127.0.0.1:1055 %h %p' <точный-Windows-user>@unterpc.tail9423b1.ts.net
```

Если у тебя нет доступа к Mac, честно оставь внешнюю приёмку pending и передай команду и fingerprint сюда: Codex на Mac выполнит её. Локальную Windows-сессию оставь открытой до подтверждённого внешнего входа. Не заявляй, что проверен reboot, если его не выполнял.

Обнови станционную документацию: команда подключения, где хранятся ключи (без приватной части), штатное обновление/перезапуск worker, восстановление после сбоя и какие условия требуют внимания. Не обещай абсолютной бесперебойности: нужны питание, сеть и исправный Tailscale. SSH после перезапуска не обеспечивает интерактивный desktop для игровых захватов — это отдельное ограничение.

В конце сообщи сделанные настройки, доказанные проверки, fingerprint, SSH username, состояние worker, receipt/PNG и оставшиеся ограничения. Не закрывай задачу одним описанием без попытки выполнить доступные действия.

Основание: [Microsoft OpenSSH](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse), [ключи и конфигурация](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh-server-configuration), [Tailscale SSH](https://tailscale.com/docs/features/tailscale-ssh), [unattended mode](https://tailscale.com/docs/how-to/run-unattended).
