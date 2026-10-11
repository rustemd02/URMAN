#!/usr/bin/env python3
"""Мессенджер агентов УРМАН поверх docs/tasktracker/AGENT_CHAT.md.

Источник истины — обычный Markdown-файл: его читает человек, правит git (merge=union),
а этот скрипт показывает как чат в браузере и позволяет писать из терминала.
Зависимостей нет, только стандартная библиотека Python 3.9+.

  python3 tools/agent_chat/chat.py serve [--port 8765] [--host 127.0.0.1] [--git-sync 60]
  python3 tools/agent_chat/chat.py post --as Claude --where "Mac Кадыра" --to всем "Текст"
  python3 tools/agent_chat/chat.py tail -n 10
  python3 tools/agent_chat/chat.py inbox --as Claude          # что адресовано мне и я ещё не видел
  python3 tools/agent_chat/chat.py notify "Вопрос автору"
  python3 tools/agent_chat/chat.py post --as Claude --where "Mac" --to Codex --task "Проверь X"
  python3 tools/agent_chat/chat.py export-static ПАПКА        # публичная версия для GitHub Pages
"""
from __future__ import annotations

import argparse
import json
import os
import re
import secrets
import socket
import subprocess
import sys
import threading
import time
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse
from urllib.error import HTTPError, URLError
from urllib.request import HTTPRedirectHandler, Request, build_opener

ROOT = Path(__file__).resolve().parents[2]
CHAT = ROOT / "docs" / "tasktracker" / "AGENT_CHAT.md"
UI = Path(__file__).with_name("index.html")
MSK = timezone(timedelta(hours=3))  # у Москвы нет перехода на летнее время
MARKER = "## Переписка"
PINNED = "## 📌 Закреплено"
HEADER = re.compile(
    r"^\*\*(?P<name>[^·*\n]+?) · (?P<where>[^·*\n]+?) · (?P<stamp>\d{2}\.\d{2} \d{2}:\d{2})\*\*"
    r"(?: → (?P<to>.+?))?\s*$")
REPLY = re.compile(r"^↩ ответ на «(?P<quote>.*?)»\s*(?P<rest>.*)$")
MAX_TEXT = 8000
WRITE_LOCK = threading.Lock()


def clean_field(value: str, limit: int = 60) -> str:
    value = re.sub(r"[·*\r\n]+", " ", value or "").strip()
    return value[:limit]


def parse(text: str) -> dict:
    """Разбирает файл: закреплённое и список сообщений в порядке файла."""
    head, _, body = text.partition(MARKER)
    pinned: list[str] = []
    in_pinned = False
    for line in head.splitlines():
        if line.startswith("## "):
            in_pinned = line.strip() == PINNED
        elif in_pinned and line.startswith("- "):
            pinned.append(line[2:].strip())
    messages: list[dict] = []
    current: dict | None = None
    for line in body.splitlines():
        match = HEADER.match(line)
        if match:
            current = {"id": len(messages), "name": match["name"].strip(), "where": match["where"].strip(),
                       "stamp": match["stamp"], "to": (match["to"] or "всем").strip(), "lines": []}
            messages.append(current)
        elif current is not None:
            current["lines"].append(line)
    for message in messages:
        lines = message.pop("lines")
        while lines and not lines[-1].strip():
            lines.pop()
        while lines and not lines[0].strip():
            lines.pop(0)
        message["reply"] = None
        if lines:
            reply = REPLY.match(lines[0])
            if reply:
                message["reply"] = reply["quote"]
                lines = ([reply["rest"]] if reply["rest"].strip() else []) + lines[1:]
        message["text"] = "\n".join(line[1:] if line.startswith("​") else line for line in lines).strip()
    return {"pinned": pinned, "messages": messages}


_cache: dict = {"key": None, "value": None}


def load() -> dict:
    try:
        stat = CHAT.stat()
    except FileNotFoundError:
        return {"pinned": [], "messages": []}
    key = (stat.st_mtime_ns, stat.st_size)
    if _cache["key"] != key:
        _cache["value"] = parse(CHAT.read_text(encoding="utf-8"))
        _cache["key"] = key
    return _cache["value"]


def append(name: str, where: str, to: str, text: str, reply: str | None = None) -> dict:
    name, where, to = clean_field(name), clean_field(where), clean_field(to, 120) or "всем"
    text = (text or "").replace("\r\n", "\n").strip()
    if not name or not where:
        raise ValueError("нужны имя и «где работает»")
    if not text:
        raise ValueError("пустое сообщение")
    if len(text) > MAX_TEXT:
        raise ValueError(f"сообщение длиннее {MAX_TEXT} символов")
    safe = []
    for line in text.split("\n"):  # строка-«шапка» внутри текста ломала бы разбор
        safe.append("​" + line if HEADER.match(line) or line.startswith(MARKER) or line.strip() == "---" else line)
    stamp = datetime.now(MSK).strftime("%d.%m %H:%M")
    block = f"\n**{name} · {where} · {stamp}** → {to}\n"
    if reply:
        block += f"↩ ответ на «{clean_field(reply, 80)}»\n"
    block += "\n".join(safe) + "\n"
    with WRITE_LOCK:
        CHAT.parent.mkdir(parents=True, exist_ok=True)
        existing = CHAT.read_bytes() if CHAT.exists() else b""
        prefix = "" if existing.endswith(b"\n") or not existing else "\n"
        with open(CHAT, "a", encoding="utf-8", newline="\n") as handle:
            handle.write(prefix + block)  # одним write: параллельные дописки не перемешиваются
            handle.flush()
            os.fsync(handle.fileno())
        _cache["key"] = None
    return load()["messages"][-1]


# ---------- веб-сервер ----------

PRESENCE: dict[str, float] = {}
TOKEN = ""


def participants(messages: list[dict]) -> list[str]:
    seen: dict[str, None] = {}
    for message in messages:
        seen.setdefault(message["name"], None)
    return list(seen)


class Handler(BaseHTTPRequestHandler):
    server_version = "UrmanAgentChat/1"

    def log_message(self, fmt, *args):  # тише: только ошибки
        if args and str(args[1]).startswith(("4", "5")) and str(args[1]) != "401":
            sys.stderr.write("%s %s\n" % (self.address_string(), fmt % args))

    def _send(self, status: int, body: bytes, ctype: str):
        self.send_response(status)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.end_headers()
        self.wfile.write(body)

    def _json(self, status: int, payload):
        self._send(status, json.dumps(payload, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8")

    def _authorized(self, query: dict) -> bool:
        given = self.headers.get("X-Chat-Token") or (query.get("token") or [""])[0]
        return bool(TOKEN) and secrets.compare_digest(given.encode(), TOKEN.encode())

    def do_GET(self):
        url = urlparse(self.path)
        query = parse_qs(url.query)
        if url.path in ("/", "/index.html"):
            return self._send(200, UI.read_bytes(), "text/html; charset=utf-8")
        if url.path == "/api/state":
            if not self._authorized(query):
                return self._json(401, {"error": "нужен токен"})
            since = int((query.get("since") or ["-1"])[0])
            data = load()
            now = time.time()
            return self._json(200, {
                "total": len(data["messages"]), "since": since,
                "messages": [m for m in data["messages"] if m["id"] > since],
                "pinned": data["pinned"], "participants": participants(data["messages"]),
                "online": [n for n, t in PRESENCE.items() if now - t < 35], "now": datetime.now(MSK).strftime("%d.%m %H:%M")})
        self._json(404, {"error": "нет такой страницы"})

    def do_POST(self):
        url = urlparse(self.path)
        if not self._authorized(parse_qs(url.query)):
            return self._json(401, {"error": "нужен токен"})
        length = int(self.headers.get("Content-Length") or 0)
        if length > 40_000:
            return self._json(413, {"error": "слишком большое сообщение"})
        try:
            payload = json.loads(self.rfile.read(length) or b"{}")
        except json.JSONDecodeError:
            return self._json(400, {"error": "не JSON"})
        if url.path == "/api/presence":
            name = clean_field(str(payload.get("name", "")))
            if name:
                PRESENCE[name] = time.time()
            return self._json(200, {"ok": True})
        if url.path == "/api/send":
            try:
                message = append(str(payload.get("name", "")), str(payload.get("where", "веб")), str(payload.get("to", "всем")),
                                 str(payload.get("text", "")), payload.get("reply") or None)
            except ValueError as error:
                return self._json(400, {"error": str(error)})
            PRESENCE[message["name"]] = time.time()
            return self._json(200, message)
        self._json(404, {"error": "нет такой страницы"})


def git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, text=True, timeout=180)


def sync_blocker(rel: str) -> str | None:
    """Return a reason to defer sync before it can mutate the shared checkout."""
    git_dir_result = git("rev-parse", "--git-dir")
    if git_dir_result.returncode != 0:
        return f"ошибка git rev-parse: {(git_dir_result.stderr or git_dir_result.stdout).strip()[:160]}"
    git_dir = Path(git_dir_result.stdout.strip())
    if not git_dir.is_absolute():
        git_dir = ROOT / git_dir
    operation_paths = (
        ("index.lock", "заблокированный Git index"),
        ("MERGE_HEAD", "незавершённое слияние"),
        ("rebase-merge", "незавершённый rebase"),
        ("rebase-apply", "незавершённый rebase"),
        ("REBASE_HEAD", "незавершённый rebase"),
        ("CHERRY_PICK_HEAD", "незавершённый cherry-pick"),
        ("sequencer", "незавершённая последовательная Git-операция"),
    )
    for marker, description in operation_paths:
        if (git_dir / marker).exists():
            return f"пропуск: {description}"

    chat_exclusion = f":(exclude){rel}"
    for args in (
        ("diff", "--quiet", "--", ".", chat_exclusion),
        ("diff", "--cached", "--quiet", "--", ".", chat_exclusion),
    ):
        result = git(*args)
        if result.returncode == 1:
            return "пропуск: есть tracked-правки вне AGENT_CHAT.md"
        if result.returncode != 0:
            return f"ошибка git diff: {(result.stderr or result.stdout).strip()[:160]}"
    return None


def sync_once(push: bool = True) -> str:
    """Синхронизирует чат только когда shared checkout свободен от чужих tracked-правок."""
    rel = str(CHAT.relative_to(ROOT))
    blocker = sync_blocker(rel)
    if blocker:
        return blocker

    status = git("status", "--porcelain", "--untracked-files=no", "--", rel)
    if status.returncode != 0:
        return f"ошибка git status: {(status.stderr or status.stdout).strip()[:160]}"
    done = []
    if status.stdout.strip():
        before = load()["messages"]
        blocker = sync_blocker(rel)
        if blocker:
            return blocker
        added = git("add", "--", rel)
        if added.returncode != 0:
            return f"ошибка git add: {(added.stderr or added.stdout).strip()[:160]}"
        blocker = sync_blocker(rel)
        if blocker:
            return blocker
        names = ", ".join(participants(before[-5:])) or "агенты"
        commit = git("commit", "-m", f"chat: новые сообщения в AGENT_CHAT.md ({names})", "--", rel)
        if commit.returncode != 0:
            return f"ошибка git commit: {(commit.stderr or commit.stdout).strip()[:160]}"
        done.append("commit")

    blocker = sync_blocker(rel)
    if blocker:
        return "; ".join(done + [blocker])
    pull = git("-c", "pull.autostash=false", "-c", "merge.autostash=false", "-c", "rebase.autostash=false",
               "pull", "--no-rebase", "--no-edit")
    if pull.returncode != 0:
        return "; ".join(done + [f"ошибка git pull: {(pull.stderr or pull.stdout).strip()[:160]}"])
    done.append("pull ok")
    _cache["key"] = None
    if push:
        blocker = sync_blocker(rel)
        if blocker:
            return "; ".join(done + [blocker])
        ahead_result = git("rev-list", "--count", "@{upstream}..HEAD")
        if ahead_result.returncode != 0:
            return "; ".join(done + [f"ошибка git rev-list: {(ahead_result.stderr or ahead_result.stdout).strip()[:160]}"])
        ahead = ahead_result.stdout.strip()
        if not re.fullmatch(r"\d+", ahead):
            return "; ".join(done + ["ошибка git rev-list: некорректное число исходящих коммитов"])
        if ahead != "0":
            sent = git("push")
            if sent.returncode != 0:
                return "; ".join(done + [f"ошибка git push: {(sent.stderr or sent.stdout).strip()[:160]}"])
            done.append("push ok")
    return "; ".join(done)


def sync_loop(seconds: int, push: bool):
    while True:
        time.sleep(seconds)
        try:
            print(time.strftime("%H:%M:%S"), "git-sync:", sync_once(push), flush=True)
        except Exception as error:  # сеть и git не должны ронять чат
            print("git-sync error:", error, flush=True)


def serve(args):
    global TOKEN
    token_file = Path(args.token_file).expanduser()
    if os.environ.get("AGENT_CHAT_TOKEN"):
        TOKEN = os.environ["AGENT_CHAT_TOKEN"]
    else:
        if not token_file.exists():
            token_file.parent.mkdir(parents=True, exist_ok=True)
            token_file.write_text(secrets.token_urlsafe(18), encoding="utf-8")
            try:
                token_file.chmod(0o600)
            except OSError:
                pass
        TOKEN = token_file.read_text(encoding="utf-8").strip()
    if args.git_sync:
        threading.Thread(target=sync_loop, args=(args.git_sync, not args.no_push), daemon=True).start()
    httpd = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"Чат агентов: http://{'127.0.0.1' if args.host in ('0.0.0.0', '') else args.host}:{args.port}/#token={TOKEN}")
    print(f"Файл переписки: {CHAT}")
    print("Токен хранится в", token_file, "(или AGENT_CHAT_TOKEN). Ctrl+C — остановить.", flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass


def addressed_to(message: dict, name: str) -> bool:
    low = name.lower()
    targets = [t.strip().lower().lstrip("@") for t in re.split(r"[,/]| и ", message["to"])]
    return low in targets or "всем" in targets or f"@{low}" in message["text"].lower()


def inbox(name: str, peek: bool) -> int:
    """Непрочитанное, адресованное имени (лично, @упоминанием или «всем»). Позиция хранится по имени."""
    state = Path("~/.config/urman-agent-chat").expanduser() / f"inbox-{re.sub(r'[^0-9A-Za-zА-Яа-я_-]', '_', name)}.txt"
    last = int(state.read_text().strip() or -1) if state.exists() else -1
    messages = load()["messages"]
    fresh = [m for m in messages if m["id"] > last and m["name"].lower() != name.lower() and addressed_to(m, name)]
    for message in fresh:
        mark = "📋 " if message["text"].lstrip().startswith("📋") else ""
        print(f"{mark}[{message['stamp']}] {message['name']} ({message['where']}) → {message['to']}")
        print("   " + message["text"].replace("\n", "\n   "))
    if not fresh:
        print("Новых сообщений для вас нет.")
    if messages and not peek:
        state.parent.mkdir(parents=True, exist_ok=True)
        state.write_text(str(messages[-1]["id"]), encoding="utf-8")
    return len(fresh)


TELEGRAM_CONFIG = Path("~/.config/urman-agent-chat/telegram.json").expanduser()
TELEGRAM_API = "https://api.telegram.org"
TELEGRAM_TIMEOUT = 10
TELEGRAM_RESPONSE_LIMIT = 65_536
TELEGRAM_TOKEN_RE = re.compile(r"^[0-9]+:[A-Za-z0-9_-]+$")


class TelegramNotifyError(Exception):
    """Sanitized error safe to show in the CLI; never includes request details."""


def telegram_credentials() -> tuple[str, str | int]:
    env_token = os.environ.get("URMAN_TELEGRAM_BOT_TOKEN", "").strip()
    env_chat_id = os.environ.get("URMAN_TELEGRAM_CHAT_ID", "").strip()
    config: dict = {}
    if not env_token or not env_chat_id:
        try:
            config_stat = TELEGRAM_CONFIG.stat()
            if config_stat.st_size > 16_384:
                raise TelegramNotifyError("Файл telegram.json слишком большой.")
            if os.name == "posix" and config_stat.st_mode & 0o077:
                raise TelegramNotifyError("Права telegram.json должны быть закрыты для группы и остальных (chmod 600).")
            with TELEGRAM_CONFIG.open("rb") as handle:
                config_data = handle.read(16_385)
            if len(config_data) > 16_384:
                raise TelegramNotifyError("Файл telegram.json слишком большой.")
            config = json.loads(config_data.decode("utf-8"))
        except FileNotFoundError:
            config = {}
        except TelegramNotifyError:
            raise
        except json.JSONDecodeError:
            raise TelegramNotifyError("Файл telegram.json содержит некорректный JSON.") from None
        except RecursionError:
            raise TelegramNotifyError("Файл telegram.json содержит некорректный JSON.") from None
        except (OSError, UnicodeError):
            raise TelegramNotifyError("Не удалось прочитать telegram.json.") from None
        if not isinstance(config, dict):
            raise TelegramNotifyError("telegram.json должен содержать объект с bot_token и chat_id.")

    token = env_token or config.get("bot_token")
    chat_id = env_chat_id or config.get("chat_id")
    if not isinstance(token, str) or not TELEGRAM_TOKEN_RE.fullmatch(token.strip()):
        raise TelegramNotifyError("Telegram не настроен: задайте bot_token и chat_id в telegram.json или обе переменные URMAN_TELEGRAM_*.")
    token = token.strip()
    if isinstance(chat_id, bool) or not isinstance(chat_id, (str, int)):
        raise TelegramNotifyError("Telegram не настроен: chat_id должен быть числом или непустой строкой.")
    if isinstance(chat_id, str):
        chat_id = chat_id.strip()
        if not chat_id or len(chat_id) > 128 or any(ord(char) < 32 or ord(char) == 127 for char in chat_id):
            raise TelegramNotifyError("Telegram не настроен: chat_id должен быть числом или непустой строкой.")
    return token, chat_id


class NoTelegramRedirects(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def send_telegram(text: str) -> None:
    if not text.strip():
        raise TelegramNotifyError("Текст уведомления пуст.")
    if len(text) > 4096:
        raise TelegramNotifyError("Текст уведомления длиннее 4096 символов.")
    token, chat_id = telegram_credentials()
    url = f"{TELEGRAM_API}/bot{token}/sendMessage"
    payload = {"chat_id": chat_id, "text": text, "link_preview_options": {"is_disabled": True}}
    try:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    except (UnicodeEncodeError, TypeError, ValueError):
        raise TelegramNotifyError("Текст уведомления содержит неподдерживаемые символы.") from None
    request = Request(url, data=body,
                      headers={"Content-Type": "application/json"}, method="POST")
    opener = build_opener(NoTelegramRedirects)
    try:
        with opener.open(request, timeout=TELEGRAM_TIMEOUT) as response:
            status = response.getcode()
            response_data = response.read(TELEGRAM_RESPONSE_LIMIT + 1)
    except HTTPError as error:
        status = error.code
        error.close()
        response_data = b""
    except (URLError, TimeoutError, socket.timeout, OSError):
        raise TelegramNotifyError("Не удалось подключиться к Telegram.") from None
    except Exception:
        raise TelegramNotifyError("Не удалось подключиться к Telegram.") from None
    if 300 <= status < 400:
        raise TelegramNotifyError("Telegram вернул перенаправление; запрос остановлен.")
    if status != 200:
        raise TelegramNotifyError(f"Telegram ответил HTTP {status}.")
    if len(response_data) > TELEGRAM_RESPONSE_LIMIT:
        raise TelegramNotifyError("Ответ Telegram превысил допустимый размер.")
    try:
        result = json.loads(response_data)
    except (json.JSONDecodeError, UnicodeDecodeError, RecursionError):
        raise TelegramNotifyError("Telegram вернул некорректный ответ.") from None
    if not isinstance(result, dict) or result.get("ok") is not True:
        raise TelegramNotifyError("Telegram отклонил отправку.")


RAW_SOURCE = "https://raw.githubusercontent.com/rustemd02/URMAN/main/docs/tasktracker/AGENT_CHAT.md"


def export_static(out: Path, source: str):
    """Страница только для чтения: сама тянет AGENT_CHAT.md с GitHub, сервер не нужен."""
    html = UI.read_text(encoding="utf-8")
    inject = f"<script>window.CHAT_STATIC_SOURCE = {json.dumps(source)};</script>\n<script>\n\"use strict\";"
    marker = '<script>\n"use strict";'
    if marker not in html:
        sys.exit("в index.html не найден основной <script>")
    out.mkdir(parents=True, exist_ok=True)
    (out / "index.html").write_text(html.replace(marker, inject, 1), encoding="utf-8")
    (out / ".nojekyll").write_text("", encoding="utf-8")
    print(f"готово: {out}/index.html (источник: {source})")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("serve", help="запустить веб-интерфейс")
    s.add_argument("--host", default="127.0.0.1")
    s.add_argument("--port", type=int, default=8765)
    s.add_argument("--token-file", default="~/.config/urman-agent-chat/token")
    s.add_argument("--git-sync", type=int, default=0, metavar="СЕК", help="раз в N секунд коммитить файл чата, делать pull и push")
    s.add_argument("--no-push", action="store_true", help="синхронизация только в одну сторону: pull")
    p = sub.add_parser("post", help="написать сообщение без сервера")
    p.add_argument("--as", dest="name", default=os.environ.get("AGENT_CHAT_NAME"), required=not os.environ.get("AGENT_CHAT_NAME"))
    p.add_argument("--where", default=os.environ.get("AGENT_CHAT_WHERE"), required=not os.environ.get("AGENT_CHAT_WHERE"))
    p.add_argument("--to", default="всем")
    p.add_argument("--reply", help="цитата сообщения, на которое отвечаете")
    p.add_argument("--task", action="store_true", help="оформить как передачу задачи (📋): попадёт во вкладку «Задачи»")
    p.add_argument("text", help="текст или - чтобы читать из stdin")
    t = sub.add_parser("tail", help="показать последние сообщения")
    t.add_argument("-n", type=int, default=10)
    i = sub.add_parser("inbox", help="непрочитанное, адресованное вам")
    i.add_argument("--as", dest="name", default=os.environ.get("AGENT_CHAT_NAME"), required=not os.environ.get("AGENT_CHAT_NAME"))
    i.add_argument("--peek", action="store_true", help="не отмечать прочитанным")
    n = sub.add_parser("notify", help="отправить явно указанный текст в настроенный Telegram-чат")
    n.add_argument("text", nargs="+", help="вопрос или сообщение для Telegram")
    e = sub.add_parser("export-static", help="собрать публичную read-only страницу")
    e.add_argument("out", help="папка для index.html")
    e.add_argument("--source", default=RAW_SOURCE)
    sub.add_parser("sync", help="один раунд git-синхронизации файла чата")
    args = parser.parse_args()
    if args.cmd == "serve":
        serve(args)
    elif args.cmd == "post":
        text = sys.stdin.read() if args.text == "-" else args.text
        if args.task and not text.lstrip().startswith("📋"):
            text = "📋 Задача" + (f" для @{args.to}" if args.to != "всем" else "") + ": " + text.lstrip()
        try:
            message = append(args.name, args.where, args.to, text, args.reply)
        except ValueError as error:
            sys.exit(f"не отправлено: {error}")
        print(f"записано #{message['id']} в {CHAT.relative_to(ROOT)} — не забудьте закоммитить и запушить вместе с работой")
    elif args.cmd == "tail":
        for message in load()["messages"][-args.n:]:
            print(f"[{message['stamp']}] {message['name']} ({message['where']}) → {message['to']}")
            print("   " + message["text"].replace("\n", "\n   "))
    elif args.cmd == "inbox":
        inbox(args.name, args.peek)
    elif args.cmd == "notify":
        try:
            send_telegram(" ".join(args.text))
        except TelegramNotifyError as error:
            sys.exit(f"notify: {error}")
        print("Уведомление отправлено в Telegram.")
    elif args.cmd == "export-static":
        export_static(Path(args.out), args.source)
    elif args.cmd == "sync":
        print(sync_once())


if __name__ == "__main__":
    main()
