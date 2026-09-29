#!/usr/bin/env python3
"""Generate a two-speaker radio episode from episode.txt using Gemini TTS."""

import base64
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SCRIPT_FILE = ROOT / "episode.txt"
KEY_FILE = ROOT / "api_key.txt"
OUTPUT_FILE = ROOT / "radio.wav"
MODEL = "gemini-3.8-flash-tts"

# Stable character direction and prebuilt voices for every turn.
SPEAKERS = {
    "ВЕДУЩИЙ": {
        "id": "Host",
        "voice": "Puck",
        "style": (
            "A warm, curious male radio host speaking Russian and Tatar. "
            "Clear diction, conversational and lively, moderate-fast pace, "
            "with gentle humor."
        ),
    },
    "РАИСА АПА": {
        "id": "Raisa",
        "voice": "Kore",
        "style": (
            "An older Tatar woman speaking Russian and Tatar. "
            "Dry, sharp, teasing and a little impatient, with confident "
            "natural pauses and warmth underneath."
        ),
    },
}

HEADER_RE = re.compile(r"^\s*([^:\n]{1,40})\s*:\s*(.*)$")


def normalize_label(label):
    return re.sub(r"\s+", " ", label.strip()).upper()


def looks_like_label(label):
    letters = [character for character in label if character.isalpha()]
    return bool(letters) and label.strip().upper() == label.strip()


def parse_episode(text):
    turns = []
    current = None

    for line_number, raw_line in enumerate(text.splitlines(), start=1):
        line = raw_line.strip()
        if not line:
            continue

        match = HEADER_RE.match(raw_line)
        if match:
            label = normalize_label(match.group(1))
            if label in SPEAKERS:
                current = {"speaker": label, "text": match.group(2).strip()}
                turns.append(current)
                continue
            if looks_like_label(match.group(1)):
                raise ValueError(
                    f"Строка {line_number}: неизвестный спикер «{match.group(1).strip()}». "
                    "Используй ВЕДУЩИЙ: или РАИСА АПА:."
                )

        if current is None:
            raise ValueError(
                f"Строка {line_number}: сначала укажи спикера — ВЕДУЩИЙ: или РАИСА АПА:."
            )
        current["text"] = (current["text"] + "\n" + line).strip()

    turns = [turn for turn in turns if turn["text"].strip()]
    if not turns:
        raise ValueError("В episode.txt нет реплик. Добавь хотя бы одну строку со спикером и текстом.")
    return turns


def read_api_key():
    if not KEY_FILE.exists():
        raise FileNotFoundError("Не найден api_key.txt рядом со скриптом.")
    key = KEY_FILE.read_text(encoding="utf-8-sig").strip()
    if not key:
        raise ValueError("api_key.txt пуст. Вставь Gemini API key одной строкой.")
    if "\n" in key or "\r" in key:
        raise ValueError("В api_key.txt должен быть только один ключ в одной строке.")
    if key.upper() in {"YOUR_API_KEY", "PASTE_GEMINI_API_KEY_HERE", "ВСТАВЬ_КЛЮЧ"}:
        raise ValueError("Замени текст-подсказку в api_key.txt на свой Gemini API key.")
    return key


def main():
    try:
        api_key = read_api_key()
        if not SCRIPT_FILE.exists():
            raise FileNotFoundError("Не найден episode.txt рядом со скриптом.")
        source = SCRIPT_FILE.read_text(encoding="utf-8-sig")
        turns = parse_episode(source)

        try:
            from google import genai
        except ImportError as exc:
            raise RuntimeError(
                "Не установлена библиотека google-genai. Запусти RUN_WINDOWS.bat, "
                "чтобы она установилась автоматически."
            ) from exc

        content = []
        for turn in turns:
            character = SPEAKERS[turn["speaker"]]
            content.append({
                "type": "text",
                "text": turn["text"],
                "annotations": [{
                    "type": "speech_metadata",
                    "speaker": character["id"],
                    "style": character["style"],
                }],
            })

        client = genai.Client(api_key=api_key)
        print(f"Отправляю {len(turns)} реплик в Gemini TTS…")
        interaction = client.interactions.create(
            model=MODEL,
            input=[{"type": "user_input", "content": content}],
            response_format={"type": "audio"},
            generation_config={
                "speech_config": {
                    "mode": "conversational",
                    "speakers": [
                        {"speaker": SPEAKERS["ВЕДУЩИЙ"]["id"], "voice": SPEAKERS["ВЕДУЩИЙ"]["voice"]},
                        {"speaker": SPEAKERS["РАИСА АПА"]["id"], "voice": SPEAKERS["РАИСА АПА"]["voice"]},
                    ],
                }
            },
        )

        audio = getattr(interaction, "output_audio", None)
        encoded = getattr(audio, "data", None) if audio is not None else None
        if not encoded:
            raise RuntimeError("Gemini не вернул аудио. Попробуй ещё раз или проверь ключ и сценарий.")
        wav_bytes = base64.b64decode(encoded)
        if not wav_bytes.startswith(b"RIFF"):
            raise RuntimeError("Ответ Gemini не похож на WAV-файл; radio.wav не записан.")
        OUTPUT_FILE.write_bytes(wav_bytes)
        print(f"Готово: {OUTPUT_FILE.name}")

    except KeyboardInterrupt:
        print("\nОперация остановлена.")
        return 130
    except Exception as exc:
        print(f"Ошибка: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
