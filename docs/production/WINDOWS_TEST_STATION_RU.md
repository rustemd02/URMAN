# УРМАН: разработка где удобно, игровые проверки на Windows

Схема: текущий checkout Mac/Linux/Codex Cloud → `eng/remote-check.py` → приватный
Tailscale HTTPS → Windows worker → существующий `run-windows-check.ps1` →
`protected_run.py` → Godot → receipt, логи и игровые PNG. Перенос чата не нужен.
Разработка остаётся в текущем checkout. Запрос «запусти поиграть» выполняется
локально на том компьютере, где пользователь его дал. Обычный статический анализ
и узкая C#-компиляция могут выполняться локально. Не запускать движок после каждой
правки по привычке; отдельный запрет игровых проверок сохраняет силу.

## Настроенная станция

- Checkout: `C:\Users\ruste\Documents\GitHub\URMAN` (приоритетное поручение автора).
- HTTPS: `https://unterpc.tail9423b1.ts.net`, порт 443, только tailnet.
- Worker слушает только `127.0.0.1:8765`; публичный Funnel-маршрут не используется.
- Данные: `%LOCALAPPDATA%\URMAN-STATION`, вне Git. Конфигурация и три отдельные
  авторизации: `station-config.json`, `owner.token`, `second-developer.token`,
  `cloud.token`. Значения не включать в diff, логи, инструкции или результаты.
- Закреплённые версии: `global.json`, `eng/toolchain.json`, `game/Urman.Game.csproj`.
  На Windows устанавливаются Windows-бинарники, на Mac — Mac-бинарники.
- Blender не нужен для выполнения готовых GLB. Станционный `game/override.cfg`
  отключает Blender-import; его SHA256 записывается отдельно в receipt.

Установка зависимостей: Python 3.12+ и Git for Windows; затем в checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/setup-windows-station.ps1
```

В текущем PowerShell нужен PATH с установленным Python 3.12+, а не старым 3.10.
Runner также принимает явный `-Python`. Сборка, обновление content packs и импорт
сериализуются общим станционным lock. Windows выполняет те же две компиляции
campaign через существующий ContentCli и `package-document-images.py`, что Mac.
Ошибочная компиляция не допускает запуска старой DLL.

На этой Windows установлен интернет-прокси `127.0.0.1:10809`, который не достигает
tailnet. Для **локальной диагностики этой станции** в текущем PowerShell задавать
`$env:NO_PROXY='unterpc.tail9423b1.ts.net'` и
`$env:URMAN_STATION_CONFIG="$env:LOCALAPPDATA\URMAN-STATION\client.json"`.
Это проверенное исключение одного назначения, без изменения глобальных настроек.
В Cloud это исключение не переносить: там требуется штатный HTTPS_PROXY.

## Особенность текущего Windows-checkout

В main есть путь `.commandcode/taste/--prefers-communication-in-russian.-confidence:-0.95/taste.md`,
который нельзя создать в Windows из-за двоеточия. Только в этом checkout включён
Git sparse checkout, исключающий `.commandcode`; runtime-файлы присутствуют.
Исключение не передаётся на Mac и не меняет репозиторий. Git 2.37 потребовал
однократный `-c core.protectNTFS=false` при начальном checkout исключённого пути;
постоянная защита Git/Windows не отключена. Последующий обычный `git pull --ff-only`
проверен. Не отключать sparse checkout на Windows до исправления пути владельцем.
Это shallow checkout main; для сборки и source snapshot история не нужна.

## Desktop-сессия и автозапуск

Исполнитель работает под вошедшим пользователем в интерактивной Session 1, через
Task Scheduler: **URMAN Windows Test Worker**, logon trigger, Interactive,
Limited. Пароль Windows не сохраняется, автологин не включается. После перезагрузки
войти в Windows; для native/capture оставить доступный дисплей и разблокированную
сессию. Служба Tailscale обеспечивает сеть, но GUI worker не является службой SYSTEM.
`doctor` проверяет desktop, инструменты, recovery marker и занятую станцию;
health сам по себе не доказывает успешный GPU-прогон.

```powershell
# Зарегистрировать/запустить (конфигурация уже должна существовать вне Git):
powershell -NoProfile -ExecutionPolicy Bypass -File eng/manage-windows-worker.ps1 -Action install
# Запустить существующее задание:
powershell -NoProfile -ExecutionPolicy Bypass -File eng/manage-windows-worker.ps1 -Action start
# Остановить после завершения активного задания:
powershell -NoProfile -ExecutionPolicy Bypass -File eng/manage-windows-worker.ps1 -Action stop
# Остановить и отключить автозапуск:
powershell -NoProfile -ExecutionPolicy Bypass -File eng/manage-windows-worker.ps1 -Action disable
# Отключить частный HTTPS-прокси:
& 'C:\Program Files\Tailscale\tailscale.exe' serve --https=443 off
```

Не завершать worker/guard принудительно во время задания. При аварии marker
`%APPDATA%\Godot\app_userdata\.URMAN.protected-run.lock` сохраняет пути recovery.
Следующая проверка блокируется. Не стирать marker и recovery-каталог ради запуска:
сначала проверить отсутствие Godot, восстановить оригинал по marker и подтвердить
его файлы. Guard проверяет содержимое и восстановление переименованием; Windows
ACL отдельно не сравнивает. Незавершённые задания после перезапуска получают FAIL.

При первом импорте чистого снимка Godot пытается загрузить custom font ещё до
его импорта. Runner временно снимает назначение `gui/theme/custom_font` **только
в изолированной копии project.godot на время импорта**, затем восстанавливает
исходные байты и сверяет SHA256 с manifest до native-запуска. Во время этого
bootstrap импорт также выполняется последовательно (`editor/import/use_multiple_threads=false`):
наблюдалось падение Windows Godot при импорте TTF (0xc0000005), соответствующее
[известным font import races](https://github.com/godotengine/godot/issues/111039).
Это локальный обход для импорта, не изменение runtime-настроек/версии движка. Сам шрифт
импортируется и используется игрой. Этот отдельный bootstrap отражён в receipt.
Editor import не читает override.cfg; authoring .blend отсутствуют в runtime
снимке, поэтому Blender-import не нужен. Основной checkout не меняется.

## Точный снимок и протокол

Клиент берёт текущее содержимое tracked и новых неигнорируемых runtime-файлов в
`game`, `content`, `src-dotnet`, `tools-dotnet`, `eng`, а также корневые конфигурации.
Удалённые файлы отсутствуют. Это включает незакоммиченные C# и GLB; push не нужен.
Authoring `.blend`, исторические docs/evidence, legacy browser public/assets,
`.git`, `.tools`, `.godot`, bin/obj, node_modules, graphify, caches, старые кадры,
`.env` и ключи не передаются. Runtime Godot не зависит от legacy web/public или
authoring source/audio. При добавлении новой внешней runtime-зависимости расширить
явный список корней клиента, а не выдавать неполный снимок за проверенный.

Manifest содержит repository identity, реальный base commit, dirty paths, перечень
и SHA256 файлов. Snapshot ID — SHA256 canonical manifest. Клиент повторно сверяет
файлы и Git-state после упаковки и отказывает при изменениях. Windows проверяет
ZIP-paths, размеры, состав и каждый хеш до запуска; source распаковывается в
`runs/<job-id>/source`, без фиктивного `.git` и без изменения основного checkout.

### Инкрементальная передача (2026-10-07)

Полный снимок — около 420 МБ сжатыми, и раньше он уходил на каждую проверку, даже
когда менялись только файлы кода. Теперь клиент сначала отправляет на
`POST /snapshots/plan` один манифест (около 180 КБ) и получает список отсутствующих
у станции файлов, после чего заливает только их через `POST /blobs/<sha256>`.
Станция хранит по одному блобу на каждое содержимое файла в `data/blobs/<xx>/<sha256>`
и собирает из них точное дерево источника, проверяя размер и SHA256 каждого файла;
манифесты лежат в `data/manifests`, хранятся последние 50. При первом таком запросе
станция один раз наполняет хранилище из самого свежего уже загруженного снимка
(`data/snapshots/<sid>.zip`), поэтому история не пересылается заново.

Правка только кода сводится к сотням килобайт вместо 420 МБ; повторный запуск на том
же дереве не отправляет ничего. Полный ZIP-путь `POST /snapshots` сохранён без
изменений, `remote_common.py`, `run-windows-check.ps1` и `protected_run.py` не
затронуты, поэтому старый клиент продолжает работать. Новый клиент при отсутствии
`/snapshots/plan` на станции (404) сам откатывается на полную отправку архива.
Станционный работник нужно обновить (`git pull --ff-only` и перезапуск задачи), иначе
проверки просто пойдут по старой полной схеме.

Исполняемые станционные runner/guard/remote_common должны совпадать со снимком
по исходному тексту (допускаются только различия Git LF/CRLF). Если разработчик
меняет эти файлы, сначала обновить станцию; старый runner не выдаёт PASS за новый.
Receipt содержит byte SHA256 обеих сторон и SHA256 кода запущенного worker.

Все HTTP-операции требуют Bearer token. Принимаются только структурированные
smoke/capture задания; произвольного shell нет. Код допускается только от доверенных
авторизованных разработчиков, не из посторонних PR/fork. Один pipeline одновременно.
Повтор того же `job_id` с теми же параметрами возвращает существующее задание;
конкурентный запрос получает HTTP 409 и ID активного задания. Изменённый запрос
с прежним ID отклоняется. Не создавать новый ID для повтора потерянного ответа.

API: GET `/health`, POST `/snapshots` (ZIP с manifest), POST `/jobs` (JSON),
GET `/jobs/<id>`, GET `/jobs/<id>/result`. Timeout игры 1–300 секунд; build/content
имеют отдельные deadlines. Потеря клиентского подключения не отменяет задание.
Дедлайн ожидания клиента ограничен; позже доступны status/fetch.

Результат: `receipt.json`, `stdout.log`, `stderr.log`, отдельные build/import/game
логи, `engine-errors.log`, `frames/*.png` для capture. Receipt связывает станцию,
снимок/base commit/diff, версии, SHA256 DLL, сцену, native/headless, время, exit,
ошибки и подтверждение guard. Для стандартного smoke проверяется completion marker player-settings-smoke;
exit 0 с engine errors не принимается.
Это техническая проверка, не художественная приёмка и не человеческий плейтест.

Штатный DevViewCapture проверяет запись PNG, освобождает Image и завершает
сцену через существующий GodotSmokeCleanup. C# backtrace выявил ошибку в
VehicleImmersionDetails: sibling presentation nodes создавались из _Ready,
пока visual parent был занят. Инициализация перенесена на уже существующий первый
physics tick. Это устраняет неуспешные add_child и оставшиеся без владельца
render resources. Сохранения, story, collision и управление не меняются.

Штатный DevViewCapture проверяет запись PNG, освобождает Image и завершает
сцену через существующий GodotSmokeCleanup. C# backtrace выявил ошибку в
VehicleImmersionDetails: sibling presentation nodes создавались из _Ready,
пока visual parent был занят. Инициализация перенесена на уже существующий первый
physics tick. Это устраняет неуспешные add_child и оставшиеся без владельца
render resources. Сохранения, story, collision и управление не меняются.

Capture-корутина возвращает scene references до финализации C# resource wrappers;
после GC и нескольких render frames выполняется quit. Это только завершение
DevViewCapture, не изменение normal play.

Штатный DevViewCapture проверяет запись PNG, освобождает Image и завершает
сцену через существующий GodotSmokeCleanup. C# backtrace выявил ошибку в
VehicleImmersionDetails: sibling presentation nodes создавались из _Ready,
пока visual parent был занят. Инициализация перенесена на уже существующий первый
physics tick. Это устраняет неуспешные add_child и оставшиеся без владельца
render resources. Сохранения, story, collision и управление не меняются.

## Оба Mac: одинаковый клиент, личные авторизации

1. Установить [Tailscale для macOS](https://tailscale.com/download/mac), подключиться.
   Владелец использует свой аккаунт. Второй разработчик использует свой аккаунт:
   владелец через Machines → Windows node → Share даёт ему только этот узел.
   В access rules разрешить нужным идентичностям доступ к Windows TCP 443;
   не выдавать другому разработчику личный аккаунт владельца.
   Для владельца tailnet: в Access controls → Grants добавить разрешение с
   destination `100.75.184.88` и IP permission `tcp:443`; sources — конкретный
   пользователь владельца, принятый участник/идентичность второго разработчика
   и выделенный Cloud tag. Не копировать глобальное `*:*`. Для node sharing
   использовать только Windows node и проверить effective rules после принятия.
   Не удалять существующие правила других сервисов автоматически. Пока Mac и
   Cloud не подключены, их фактические идентичности и доступ не проверены.
2. Получить опубликованные изменения в своём checkout: `git status --short`,
   затем `git pull --ff-only origin main`. При локальных изменениях/расхождении истории
   сначала безопасно объединить работу; не reset/clean/stash чужую работу.
   Краткая памятка агента: [README_WINDOWS_WORKER_FOR_AGENTS.md](README_WINDOWS_WORKER_FOR_AGENTS.md).
   Если используется переданный отдельно инфраструктурный patch, сначала
   `git status --short`, затем `git apply --check /path/windows-station.patch`,
   затем `git apply /path/windows-station.patch`. Если check не прошёл, сохранить
   свой diff и объединить изменения вручную; не reset/stash чужую работу.
   Изменение Windows-копии AGENTS.md не действует на Mac/Cloud до передачи.
3. Владелец передаёт через менеджер паролей/другой защищённый канал `owner.token`
   на свой Mac и `second-developer.token` второму разработчику. Не через Git или чат.
   Сохранить каждый личный токен в `~/.config/urman-station/token` с `chmod 600`.
   Рядом создать `client.json` (без самого секрета):

```json
{"url":"https://unterpc.tail9423b1.ts.net","token_file":"~/.config/urman-station/token"}
```

Из checkout с Python 3.12+:

```sh
python3 eng/remote-check.py doctor
# Только если игровая проверка нужна и разрешена текущим поручением:
python3 eng/remote-check.py smoke --scene res://tests/player_settings_smoke_test.tscn --timeout 300
python3 eng/remote-check.py capture --points 'station:Ground@-14,3,15>Ground@0,2,0' --timeout 300
python3 eng/remote-check.py status --job-id <полученный-id>
python3 eng/remote-check.py fetch --job-id <полученный-id>
```

Логи и PNG скачиваются в `.codex-captures/remote/<job-id>` либо `--output <папка>`.
Агент читает receipt и логи файловыми инструментами и открывает PNG своим image viewer.
При сетевой ошибке запуск остаётся not-run: локального Godot fallback нет.
Локальная игра сохраняет прежние команды `eng/run-act1-demo.sh` / safe launcher
и существующий контракт сохранений; их чтение здесь не является Mac-прогоном.

## Codex Cloud

Настройка по актуальным [Cloud environments](https://learn.chatgpt.com/docs/environments/cloud-environments):
Settings → Codex Cloud → Environments → нужное environment → Edit.
В Advanced → VPN → Add выбрать Tailscale, внести auth key с **Reusable + Ephemeral**;
ключ создать в tailnet под выделенной Cloud-идентичностью/tag с доступом только к
станции TCP 443. Auth key не записывать в Git. Save и Publish/Republish.

В Environment variables задать `URMAN_STATION_URL=https://unterpc.tail9423b1.ts.net`.
В Network secrets → Manage: Key `URMAN_STATION_TOKEN`, Value — содержимое отдельного
`cloud.token`, Allowed domains — только `unterpc.tail9423b1.ts.net` (HTTPS 443).
В network policy разрешить этот hostname; не менять глобальные permissions на
unrestricted. Клиент использует HTTPS_PROXY и стандартную проверку сертификата.

В новом задании после Republish выполнить `python3 eng/remote-check.py doctor`.
Из того же текущего checkout отправить нужный smoke/capture: упаковка включает
незакоммиченные изменения. Старое Cloud-задание не подтверждает новое environment.
Network secret передаётся через proxy placeholder и доступен для HTTPS в task phase;
его не надо сохранять в файл или заменять setup-only secret.

[Legacy Cloud](https://learn.chatgpt.com/docs/environments/cloud-environment) удаляет
secrets до agent phase. Если интерфейс аккаунта не содержит VPN/Network secrets,
не считать такой environment настроенным и не сохранять setup-secret в repo/cache
для обхода ограничения. Конкретная альтернатива: скачать изменённые исходники из
Cloud в текущий Mac-checkout, применить с проверкой diff и отправить их тем же
клиентом с Mac. Приватный worker сохраняется; публикация наружу не требуется.

Cloud HTTP-доступ возвращает задания/логи/встроенные игровые кадры. Он не даёт
Cloud Computer Use доступ к рабочему столу Windows. Доступность VPN в конкретном
аккаунте и Mac→Windows/Cloud→Windows проверяются только реальными doctor/job.

Стандартный smoke — короткая существующая `player_settings_smoke_test.tscn`
(настройки контроллера, клавиатура/gamepad, capture/rebind/restore), без полного
набора меню/сохранений. На этой станции расширенная main-menu сцена не успела
завершиться за 300 секунд и осталась FAIL, её нельзя считать проверенной.
Первоначальная остановка выявила кратковременный Windows directory-handle race:
existing guard теперь повторяет переименование максимум 5 секунд, только при
Windows PermissionError. Mac-ветка и контракт сохранений не меняются. При
исчерпании ожидания guard сохраняет recovery и worker блокирует новые задания.
Состояние той остановки восстановлено: оригинальный userdata отсутствовал;
тестовый каталог сохранён отдельно, игровой профиль не создан восстановлением.

## Приёмка этой установки

Фактические результаты находятся в переданном отчёте и receipt конкретных заданий.
В приёмке допускаются одна короткая native-проверка и один необходимый capture.
Windows-client подтверждает протокол и Windows-путь; он не подтверждает соединение
с двух Mac или Cloud. Секретов в patch/результатах нет. Commit/push не выполняются
без отдельного поручения автора.

Сетевые команды сверены с [Tailscale Serve](https://tailscale.com/docs/reference/tailscale-cli/serve).

Тестовая очистка освобождает также detached library scene AnimationCatalog (ual1_standard.glb): диагностика orphan nodes обнаружила её сохранение после capture. Освобождение выполняет существующий GodotSmokeCleanup после закрытия сцены; обычная игра сохраняет кэш.
