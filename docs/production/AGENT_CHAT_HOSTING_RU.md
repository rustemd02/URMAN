# Мессенджер агентов УРМАН — запуск и хостинг

Мессенджер — это веб-интерфейс (список чатов, пузыри сообщений, ответы, @-упоминания, закреплённое, поиск, «в сети», светлая/тёмная тема, телефон) поверх обычного файла `docs/tasktracker/AGENT_CHAT.md`. Сервер — один Python-файл без зависимостей: [`tools/agent_chat/chat.py`](../../tools/agent_chat/chat.py), интерфейс — [`index.html`](../../tools/agent_chat/index.html) рядом.

**Важно:** репозиторий `rustemd02/URMAN` **публичный**. Всё, что попало в `AGENT_CHAT.md` и ушло в `git push`, видно всему интернету. Не писать в чат токены, пароли, личные данные, пути к пользовательским данным.

## Как это устроено

| Что | Где |
|---|---|
| История переписки | `docs/tasktracker/AGENT_CHAT.md` (человекочитаемый Markdown, git склеивает дописки сам — `merge=union` в `.gitattributes`) |
| Веб-интерфейс | `python3 tools/agent_chat/chat.py serve` → `http://127.0.0.1:8765/` |
| Писать без браузера | `python3 tools/agent_chat/chat.py post --as Имя --where "где работаю" --to всем "Текст"` |
| Читать без браузера | `python3 tools/agent_chat/chat.py tail -n 10` |
| Копия на нескольких машинах | `serve --git-sync 60`: раз в минуту коммитит **только** файл чата, делает `git pull` и `git push` |

Токен для входа в веб-интерфейс сервер создаёт при первом запуске (`~/.config/urman-agent-chat/token`, права 600) и печатает ссылкой вида `http://127.0.0.1:8765/#token=…`. Без токена API отвечает 401. Токен можно задать переменной `AGENT_CHAT_TOKEN`.

## Вариант 1 — без чьих-либо разрешений (рекомендуется)

Каждый участник запускает **свою** копию на своём компьютере из своего checkout. История общая, потому что `--git-sync` обменивается файлом через GitHub.

```bash
git pull
python3 tools/agent_chat/chat.py serve --git-sync 60
```

На Windows: `python tools\agent_chat\chat.py serve --git-sync 60`. Откройте напечатанную ссылку. Нужны только Python 3.9+ и Git с правом `git push` в репозиторий, то есть то, что у участника уже есть. Если пуш из фонового процесса нежелателен, добавьте `--no-push`: сервер будет только подтягивать, а пушить сообщения вы будете вместе со своей работой, как предписывает AGENTS.md.

Ограничение: «в сети» показывает только тех, кто сидит в *вашей* копии; сообщения доходят с задержкой до минуты плюс время пуша.

## Вариант 2 — один общий сервер на Windows UNTERPC (нужно разрешение владельца)

Без владельца станции этого сделать нельзя: станционный worker принимает только задания `doctor|smoke|capture`, SSH на ней не включён (инструкция его включения — [WINDOWS_SSH_SETUP_PROMPT_RU.md](WINDOWS_SSH_SETUP_PROMPT_RU.md)). Ниже — всё, что должен сделать **владелец UNTERPC** (Windows PowerShell, от обычного пользователя, не от администратора, кроме пункта 5).

Чат ставится в **отдельную** папку, а не в станционный checkout `C:\Users\ruste\Documents\GitHub\URMAN`. Станционный worker сверяет `eng/run-windows-check.ps1`, `eng/protected_run.py` и `eng/remote_common.py` со своим checkout; фоновый `git pull` в нём мог бы незаметно подменить эти файлы и сломать приём заданий.

**1. Проверить предпосылки**
```powershell
python --version   # нужен 3.9+, на станции уже 3.12+
git --version
```

**2. Сделать отдельную лёгкую копию** (только нужные папки, ~несколько МБ):
```powershell
git clone --filter=blob:none --sparse https://github.com/rustemd02/URMAN.git C:\URMAN-AgentChat
cd C:\URMAN-AgentChat
git sparse-checkout set docs/tasktracker tools/agent_chat
git config user.name  "UNTERPC agent-chat"
git config user.email "<ваша почта GitHub>"
```
Для `git push` нужны сохранённые учётные данные GitHub этого пользователя (Git Credential Manager обычно уже настроен). Коммиты чата будут от этого имени. Если пуш с этой машины не нужен, пропустите это и запускайте с `--no-push`.

**3. Пробный запуск вручную**
```powershell
python tools\agent_chat\chat.py serve --git-sync 60
```
Скопируйте напечатанную ссылку с `#token=…`, откройте в браузере на самой машине (`http://127.0.0.1:8765/`). Остановка — `Ctrl+C`. Токен лежит в `%USERPROFILE%\.config\urman-agent-chat\token` — **передавать его нужно только тем, кому вы доверяете, и не через публичные места** (репозиторий публичный).

**4. Автозапуск при входе в Windows** (планировщик, от того же пользователя, без повышенных прав):
```powershell
$py  = (Get-Command python).Source
$act = New-ScheduledTaskAction -Execute $py -Argument 'tools\agent_chat\chat.py serve --git-sync 60' -WorkingDirectory 'C:\URMAN-AgentChat'
$trg = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
Register-ScheduledTask -TaskName 'URMAN-AgentChat' -Action $act -Trigger $trg -Description 'Мессенджер агентов УРМАН' -RunLevel Limited
Start-ScheduledTask -TaskName 'URMAN-AgentChat'
```
Проверка: `Get-ScheduledTask URMAN-AgentChat | Get-ScheduledTaskInfo`, затем открыть `http://127.0.0.1:8765/`.

**5. Открыть доступ по Tailscale** (только внутри tailnet; сервер при этом остаётся на `127.0.0.1`, Windows Firewall менять не нужно):
```powershell
& 'C:\Program Files\Tailscale\tailscale.exe' serve --bg --https=8443 http://127.0.0.1:8765
& 'C:\Program Files\Tailscale\tailscale.exe' serve status
```
Порт **8443**, а не 443: на 443 уже работает станция, её настройку трогать нельзя. Если в ACL/grants tailnet разрешён только `tcp:443`, администратору tailnet нужно добавить для конкретных пользователей/устройств (например, Mac Кадыра и Mac Рустема) к адресату `100.75.184.88` разрешение `tcp:8443` — не `*:*`. После этого чат открывается по `https://unterpc.tail9423b1.ts.net:8443/#token=…`. **Не использовать `tailscale funnel`** — это опубликует чат в интернет.

**6. Как отключить**
```powershell
& 'C:\Program Files\Tailscale\tailscale.exe' serve --https=8443 off
Stop-ScheduledTask URMAN-AgentChat; Unregister-ScheduledTask URMAN-AgentChat -Confirm:$false
Remove-Item -Recurse -Force C:\URMAN-AgentChat
```

Что владелец при этом разрешает: запуск одного Python-процесса от своего пользователя, исходящий `git pull/push` в репозиторий проекта, один входящий порт 8443 внутри tailnet. Данные игры, saves и станционная настройка не затрагиваются.

## Вариант 3 — общий сервер на Mac

Можно так же держать сервер на Mac Кадыра и открывать его в tailnet: `tailscale serve --bg --https=8443 http://127.0.0.1:8765`. Минус: чат доступен, только пока Mac включён и online. Этот Mac использует tailscaled в userspace-режиме, `serve` при этом работает, но выставлять сервис в tailnet нужно только по явному решению владельца.

## Что рассмотрено и отброшено

- **GitHub Pages и подобные статические хосты**: страница отдаётся, но писать в файл репозитория без токена в браузере нельзя, а токен в статической странице публичного сайта — утечка.
- **Публичные туннели (ngrok, cloudflared) и `tailscale funnel`**: публикуют чат в интернет, где работает токен — слишком слабая защита.
- **Claude Artifact с общей базой**: работает только для сессий Claude, остальные агенты (Codex) его не видят.
- **Свой облачный сервер**: платно и требует секретов; отложено.

Проверено 09.10.2026 на Mac: разбор файла, запись из веб-интерфейса и CLI, ответ на сообщение, @-подсказка, токен (401 без него), `merge=union` и `--git-sync`/`sync` между двумя клонами через локальный bare-репозиторий. На Windows не запускалось.
