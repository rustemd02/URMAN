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
MARKUP = Path(__file__).with_name("speech_markup.json")
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

def load_markup():
    """Per-line direction (speaker, tone, pace, pauses, priority) kept in speech_markup.json."""
    if not MARKUP.exists():
        return {"speakers": {}, "lines": {}}
    return json.loads(MARKUP.read_text(encoding="utf-8"))


MARKUP_DATA = load_markup()
for _name, _spec in MARKUP_DATA["speakers"].items():
    VOICES.setdefault(_name, {"voice": _spec["voice"], "style": _spec["base_style"]})

PACE = {"slow": "slow, unhurried pace", "medium": "", "fast": "fast, urgent pace"}

# Lines that are not in the content pack (captions in the forest teaser).
EXTRA_LINES = [
    ("forest-wake", "mansur", "Әй! Әй, улым, уян! Уян дим!", "urgent, alarmed, shaking someone awake"),
    ("forest-name-fade", "keeper", "Айдар… Айдар… Айдар…", "unearthly, calling his name over and over, fading into echo as if the listener is losing consciousness"),
    ("forest-babai-name", "mansur", "Айдар… Айдар! Улым, уян!", "worried, close, calling his grandson by name to wake him"),
    ("forest-name-call", "keeper", "Айдар…", "distant, quiet, from between the trees"),
    ("forest-come-here", "keeper", "Айдар. Кил монда…", "close behind the listener, calm, unsettling"),
    ("forest-call-far", "keeper", "Эй! Эй! Айдар! Кил монда!", "calling from very far away between the trees, shouting, hollow"),
    ("forest-call-hey", "keeper", "Эй… эй… эй…", "faint, far away, drawn out, like an echo in a forest"),
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
    """Spoken text only: the Russian gloss on the next line "(...)" is a subtitle aid and is never voiced."""
    text = re.split(r"\n\s*\(", text.strip(), maxsplit=1)[0]
    text = text.replace("«", "").replace("»", "").replace("\"", "").strip()
    return re.sub(r"\s+", " ", text).strip()


def build_lines():
    """(id, speaker, text, tone, pause_before_ms, pause_after_ms, priority) for every line to voice."""
    defs = {item["id"]: item for item in json.loads(DEFINITIONS.read_text(encoding="utf-8"))}
    marked = MARKUP_DATA["lines"]

    def direction(lid, speaker, fallback_tone):
        m = marked.get(lid, {})
        tone = ", ".join(part for part in (m.get("tone") or fallback_tone, PACE.get(m.get("pace", "medium"), "")) if part)
        return (m.get("speaker", speaker), tone, m.get("pause_before_ms", 0), m.get("pause_after_ms", 0),
                m.get("priority", "P1"), m.get("tts_text"))

    lines = []
    for record_id, item in defs.items():
        short = record_id.split("/", 1)[1]
        if "value" not in item:
            continue  # dialogues, graphs and other non-text records
        text = clean(item["value"]["default"])
        if short.startswith("prologue-ride-bark-"):
            base = short.split(".lvl-")[0]
            speaker, tone, before, after, priority, alt = direction(short, "mansur", TONE.get(base, ""))
        elif record_id in NIVA_NODES:
            speaker, tone, before, after, priority, alt = direction(short, "mansur", "playful, teasing about the language")
        elif short in marked and marked[short]["speaker"] == "aidar":
            speaker, tone, before, after, priority, alt = direction(short, "aidar", "")
        else:
            continue
        lines.append((short, speaker, alt or text, tone, before, after, priority))
    for lid, speaker, text, tone in EXTRA_LINES:
        speaker, tone2, before, after, priority, alt = direction(lid, speaker, tone)
        lines.append((lid, speaker, alt or text, tone2, before, after, priority))
    return lines


def pad_silence(wav_bytes, before_ms, after_ms):
    """Bake the marked pauses into the clip so the runtime needs no timing logic."""
    if not before_ms and not after_ms:
        return wav_bytes
    import io
    with wave.open(io.BytesIO(wav_bytes), "rb") as src:
        params, frames = src.getparams(), src.readframes(src.getnframes())
    frame_bytes = params.sampwidth * params.nchannels
    def silence(ms):
        return b"\x00" * (int(params.framerate * ms / 1000) * frame_bytes)
    out = io.BytesIO()
    with wave.open(out, "wb") as dst:
        dst.setparams(params)
        dst.writeframes(silence(before_ms) + frames + silence(after_ms))
    return out.getvalue()


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
    parser.add_argument("--priority", choices=["P0", "P1", "P2"],
                        help="только реплики этого приоритета и важнее (бесплатный лимит ~10 запросов/сутки: сначала --priority P0)")
    args = parser.parse_args()

    lines = build_lines()
    if args.only:
        wanted = set(args.only)
        lines = [line for line in lines if line[0] in wanted]
    if args.priority:
        order = {"P0": 0, "P1": 1, "P2": 2}
        lines = [line for line in lines if order.get(line[6], 1) <= order[args.priority]]
    if args.list:
        for lid, speaker, text, _, _, _, priority in lines:
            print(f"{lid:52} {speaker:7} {priority} {text[:64]}")
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
    for index, (lid, speaker, text, tone, before_ms, after_ms, _priority) in enumerate(lines, start=1):
        target = args.out / f"{lid}.wav"
        # A clip is current only when the text it was voiced from is still the text in the pack.
        known = manifest.get(lid, {})
        stale = target.exists() and (known.get("text") not in (None, text) or known.get("speaker") not in (None, speaker))
        if target.exists() and not args.force and not stale:
            print(f"[{index}/{len(lines)}] есть: {lid}")
            continue
        if stale:
            print(f"[{index}/{len(lines)}] текст изменился, пересинтез: {lid}")
        while True:
            model = MODELS[model_index]
            try:
                target.write_bytes(pad_silence(with_retry(lambda: synthesize(client, model, speaker, text, tone)), before_ms, after_ms))
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
