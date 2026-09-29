#!/usr/bin/env python3
"""Synthesize placeholder voices for the Act I prologue with Gemini TTS.

Lines come from content/modules/urman-chapter1/definitions.json (ride barks in
three language levels, the Niva language dialogue) plus a few forest lines that
are hard-coded in Act1DemoRoot.PrologueForest.cs. One API call per line.

The API key is read from the macOS Keychain (service `urman-gemini-api-key`) or
the GEMINI_API_KEY environment variable. It is never written to the repository.

Output is a synthetic PREVIEW: every manifest entry is marked synthetic. Per the
project rules this does not replace the original voice recordings.

usage:
  generate_prologue_voices.py --list
  generate_prologue_voices.py --only prologue-ride-bark-wake [--out DIR]
  generate_prologue_voices.py [--out DIR] [--force]
"""

import argparse
import base64
import hashlib
import json
import os
import re
import subprocess
import sys
import time
import wave
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
DEFINITIONS = REPO / "content/modules/urman-chapter1/definitions.json"
DEFAULT_OUT = REPO / ".tools/voice-preview/prologue-01"
# Free tier allows ~10 requests/day per model; when one is spent, fall through to the next.
MODELS = ["gemini-3.8-flash-tts", "gemini-3.8-flash-lite-tts"]
KEYCHAIN_SERVICE = "urman-gemini-api-key"

# Prebuilt Gemini voices and stable character direction.
VOICES = {
    "mansur": {
        "voice": "Algenib",
        "style": "elderly Tatar grandfather in his late seventies, gruff, warm, low voice, natural pace, rural accent",
    },
    "marat": {
        "voice": "Puck",
        "style": "young Tatar man of about twenty, familiar and teasing, natural pace",
    },
}

# Lines that are not in the content pack (captions in the forest teaser).
EXTRA_LINES = [
    ("forest-wake", "mansur", "Әй! Әй, улым, уян! Уян дим!", "urgent, alarmed, shaking someone awake"),
    ("forest-name-fade", "marat", "Айдар… Айдар… Айдар…", "unearthly, calling his name over and over, fading into echo as if the listener is losing consciousness"),
    ("forest-babai-name", "mansur", "Айдар… Айдар! Улым, уян!", "worried, close, calling his grandson by name to wake him"),
    ("forest-name-call", "marat", "Айдар…", "distant, quiet, from between the trees"),
    ("forest-come-here", "marat", "Айдар. Кил монда…", "close behind the listener, calm, unsettling"),
    ("forest-call-far", "marat", "Эй! Эй! Айдар! Кил монда!", "calling from very far away between the trees, shouting, hollow"),
    ("forest-call-hey", "marat", "Эй… эй… эй…", "faint, far away, drawn out, like an echo in a forest"),
]

# Tone hints for a few ride barks; everything else uses the default direction.
TONE = {
    "prologue-ride-bark-nightmare": "worried, gently shaking his grandson out of a bad dream",
    "prologue-ride-bark-wake": "gruff humor, teasing about the late bus",
    "prologue-ride-bark-marat": "careful, heavy, speaks about Marat with difficulty",
}

NIVA_NODES = {
    "urman.chapter1:text/prologue-niva-question": "prologue-niva-question",
    "urman.chapter1:text/prologue-niva-none-reply": "prologue-niva-none-reply",
    "urman.chapter1:text/prologue-niva-some-reply": "prologue-niva-some-reply",
    "urman.chapter1:text/prologue-niva-fluent-reply": "prologue-niva-fluent-reply",
    "urman.chapter1:text/prologue-niva-skip-reply": "prologue-niva-skip-reply",
}


def clean(text):
    text = text.strip()
    text = re.sub(r"^[«\"]+|[»\"]+$", "", text)
    return re.sub(r"\s+", " ", text).strip()


def build_lines():
    defs = {item["id"]: item for item in json.loads(DEFINITIONS.read_text(encoding="utf-8"))}
    lines = []
    for record_id, item in defs.items():
        short = record_id.split("/", 1)[1]
        if short.startswith("prologue-ride-bark-"):
            base = short.split(".lvl-")[0]
            lines.append((short, "mansur", clean(item["value"]["default"]), TONE.get(base, "")))
        elif record_id in NIVA_NODES:
            lines.append((short, "mansur", clean(item["value"]["default"]), "playful, teasing about the language"))
    lines.extend(EXTRA_LINES)
    return lines


def read_api_key():
    key = os.environ.get("GEMINI_API_KEY", "").strip()
    if key:
        return key
    result = subprocess.run(
        ["security", "find-generic-password", "-s", KEYCHAIN_SERVICE, "-w"],
        capture_output=True, text=True,
    )
    if result.returncode != 0 or not result.stdout.strip():
        raise RuntimeError(
            f"Нет ключа: положи его в Keychain (service {KEYCHAIN_SERVICE}) или в GEMINI_API_KEY."
        )
    return result.stdout.strip()


def synthesize(client, model, speaker, text, tone):
    """One line, one speaker. The style goes in a speech_metadata annotation so the
    model does not read it aloud (a "Say like ..." prefix in the text gets spoken).
    The conversational mode wants two speakers, so a silent second one is declared."""
    character = VOICES[speaker]
    style = character["style"] + (f", {tone}" if tone else "")
    name = speaker.capitalize()
    other = "Marat" if speaker == "mansur" else "Mansur"
    interaction = client.interactions.create(
        model=model,
        input=[{
            "type": "user_input",
            "content": [{
                "type": "text",
                "text": text,
                "annotations": [{"type": "speech_metadata", "speaker": name, "style": style}],
            }],
        }],
        response_format={"type": "audio"},
        generation_config={
            "speech_config": {
                "mode": "conversational",
                "speakers": [
                    {"speaker": name, "voice": character["voice"]},
                    {"speaker": other, "voice": VOICES["marat" if speaker == "mansur" else "mansur"]["voice"]},
                ],
            }
        },
    )
    audio = getattr(interaction, "output_audio", None)
    encoded = getattr(audio, "data", None) if audio is not None else None
    if not encoded:
        raise RuntimeError("Gemini не вернул аудио")
    data = base64.b64decode(encoded)
    if not data.startswith(b"RIFF"):
        raise RuntimeError("Ответ не WAV")
    return data


def with_retry(call, attempts=6):
    """Free-tier TTS has a per-minute quota; wait it out instead of failing."""
    for attempt in range(attempts):
        try:
            return call()
        except Exception as exc:
            if "429" not in str(exc) or "per day" in str(exc) or attempt == attempts - 1:
                raise
            wait = 20 * (attempt + 1)
            print(f"  лимит запросов, жду {wait} с…", file=sys.stderr)
            time.sleep(wait)


def wav_seconds(path):
    with wave.open(str(path), "rb") as handle:
        return handle.getnframes() / handle.getframerate()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--list", action="store_true", help="показать реплики и выйти")
    parser.add_argument("--only", action="append", help="только эти id (можно несколько раз)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--force", action="store_true", help="перезаписать готовые файлы")
    args = parser.parse_args()

    lines = build_lines()
    if args.only:
        wanted = set(args.only)
        lines = [line for line in lines if line[0] in wanted]
    if args.list:
        for lid, speaker, text, _ in lines:
            print(f"{lid:52} {speaker:7} {text[:70]}")
        print(f"всего: {len(lines)}")
        return 0

    from google import genai
    global types
    from google.genai import types

    client = genai.Client(api_key=read_api_key())
    args.out.mkdir(parents=True, exist_ok=True)
    manifest_path = args.out / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}

    failures = 0
    model_index = 0
    for index, (lid, speaker, text, tone) in enumerate(lines, start=1):
        target = args.out / f"{lid}.wav"
        if target.exists() and not args.force:
            print(f"[{index}/{len(lines)}] есть: {lid}")
            continue
        while True:
            model = MODELS[model_index]
            try:
                target.write_bytes(with_retry(lambda: synthesize(client, model, speaker, text, tone)))
                break
            except Exception as exc:
                if ("per day" in str(exc) or "RESOURCE_EXHAUSTED" in str(exc)) and model_index + 1 < len(MODELS):
                    model_index += 1
                    print(f"  суточный лимит {model}, перехожу на {MODELS[model_index]}", file=sys.stderr)
                    continue
                failures += 1
                target = None
                print(f"[{index}/{len(lines)}] ОШИБКА {lid}: {type(exc).__name__}: {str(exc)[:200]}", file=sys.stderr)
                break
        if target is None:
            print("Лимиты исчерпаны на всех моделях; запусти скрипт позже — готовые файлы пропускаются.", file=sys.stderr)
            break
        manifest[lid] = {
            "speaker": speaker,
            "voice": VOICES[speaker]["voice"],
            "model": model,
            "text": text,
            "synthetic": True,
            "seconds": round(wav_seconds(target), 2),
            "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
        }
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"[{index}/{len(lines)}] {lid}  {manifest[lid]['seconds']} с")
    print(f"готово, ошибок: {failures}, папка: {args.out}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
