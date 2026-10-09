#!/usr/bin/env python3
"""Мессенджер агентов УРМАН поверх docs/tasktracker/AGENT_CHAT.md.

Источник истины — обычный Markdown-файл: его читает человек, правит git (merge=union),
а этот скрипт показывает как чат в браузере и позволяет писать из терминала.
Зависимостей нет, только стандартная библиотека Python 3.9+.

  python3 tools/agent_chat/chat.py serve [--port 8765] [--host 127.0.0.1] [--git-sync 60]
  python3 tools/agent_chat/chat.py post --as Claude --where "Mac Кадыра" --to всем "Текст"
  python3 tools/agent_chat/chat.py tail -n 10
"""
from __future__ import annotations

import argparse
import json
import os
import re
import secrets
import subprocess
import sys
import threading
import time
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

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


def sync_once(push: bool = True) -> str:
    """Коммитит только файл чата, подтягивает чужое и отправляет своё. Остальное дерево не трогает."""
    rel = str(CHAT.relative_to(ROOT))
    if (ROOT / ".git" / "MERGE_HEAD").exists():
        return "пропуск: в репозитории незавершённое слияние"
    done = []
    if git("status", "--porcelain", "--", rel).stdout.strip():
        before = load()["messages"]
        git("add", "--", rel)
        names = ", ".join(participants(before[-5:])) or "агенты"
        commit = git("commit", "-m", f"chat: новые сообщения в AGENT_CHAT.md ({names})", "--", rel)
        done.append("commit" if commit.returncode == 0 else f"commit: {commit.stderr.strip()[:120]}")
    pull = git("pull", "--no-rebase", "--no-edit")
    done.append("pull ok" if pull.returncode == 0 else f"pull: {(pull.stderr or pull.stdout).strip()[:160]}")
    _cache["key"] = None
    if push and pull.returncode == 0:
        ahead = git("rev-list", "--count", "@{upstream}..HEAD").stdout.strip()
        if ahead not in ("", "0"):
            sent = git("push")
            done.append("push ok" if sent.returncode == 0 else f"push: {sent.stderr.strip()[:160]}")
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
    p.add_argument("text", help="текст или - чтобы читать из stdin")
    t = sub.add_parser("tail", help="показать последние сообщения")
    t.add_argument("-n", type=int, default=10)
    sub.add_parser("sync", help="один раунд git-синхронизации файла чата")
    args = parser.parse_args()
    if args.cmd == "serve":
        serve(args)
    elif args.cmd == "post":
        text = sys.stdin.read() if args.text == "-" else args.text
        try:
            message = append(args.name, args.where, args.to, text, args.reply)
        except ValueError as error:
            sys.exit(f"не отправлено: {error}")
        print(f"записано #{message['id']} в {CHAT.relative_to(ROOT)} — не забудьте закоммитить и запушить вместе с работой")
    elif args.cmd == "tail":
        for message in load()["messages"][-args.n:]:
            print(f"[{message['stamp']}] {message['name']} ({message['where']}) → {message['to']}")
            print("   " + message["text"].replace("\n", "\n   "))
    elif args.cmd == "sync":
        print(sync_once())


if __name__ == "__main__":
    main()
