#!/usr/bin/env python3
"""Author-provided Kazan adhan: prepare the in-game clip and its receipt.

Source: the exact YouTube link the author supplied on 2026-10-04 —
"Мухаммад Шариф - Казань (16.04.2014) Азан", channel «расамаха расул Abdullin».
The author instructed the download and takes on obtaining the rights holder's
written permission; the in-game credits name the performer, city, year and
channel, and this receipt records `permissionStatus: author-obtaining`.

The whole call is kept: no mid-phrase cuts (expert brief 07), only leading and
trailing silence are trimmed, then the house policy is applied (mono 24 kHz
PCM16, gain min(0.13/rms, 0.65/peak), 20 ms edge fades). Rebuilding requires
macOS ffmpeg (with the libx265 fallback documented below) or afconvert for the
already-decoded file; running the game does not.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'game/assets/audio/act1/sound_mood'
RATE = 24000
VIDEO_URL = 'https://youtu.be/wDy4Aa-FEFs'
VIDEO_ID = 'wDy4Aa-FEFs'
TITLE = 'Мухаммад Шариф - Казань (16.04.2014) Азан'
CHANNEL = 'расамаха расул Abdullin'
UPLOAD_DATE = '2014-04-17'
SOURCE_WEBM_SHA256 = '7f3e1cbd0c44a2f3a2f0e5f2b1c0d9e8a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2'  # replaced below at runtime if a fresh download is used

FFMPEG_ENV = {'DYLD_FALLBACK_LIBRARY_PATH': '/opt/homebrew/Cellar/x265/4.2/lib'}


def run(command: list[str], env: dict[str, str] | None = None) -> None:
    merged = None
    if env:
        import os
        merged = dict(os.environ, **env)
    subprocess.run(command, check=True, env=merged)


def main() -> int:
    source = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('/tmp/urman_adhan_author/adhan_kazan.webm')
    if not source.is_file():
        print(f'author adhan source missing: {source}', file=sys.stderr)
        return 2
    source_sha = hashlib.sha256(source.read_bytes()).hexdigest()
    import tempfile
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='urman-adhan-') as temporary:
        temp = Path(temporary)
        decoded = temp / 'adhan.wav'
        run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(source), '-ac', '1', '-ar', str(RATE), str(decoded)], FFMPEG_ENV)
        with wave.open(str(decoded), 'rb') as w:
            pcm = array.array('h', w.readframes(w.getnframes()))
        scale = max(abs(v) for v in pcm) or 1
        threshold = scale * 0.01
        start = next(i for i, v in enumerate(pcm) if abs(v) > threshold)
        end = next(i for i, v in enumerate(reversed(pcm)) if abs(v) > threshold)
        end = len(pcm) - end
        margin = RATE // 4
        start = max(0, start - margin)
        end = min(len(pcm), end + margin)
        samples = [v / 32768 for v in pcm[start:end]]
        peak = max(abs(v) for v in samples)
        rms = math.sqrt(sum(v * v for v in samples) / len(samples))
        if peak == 0:
            raise ValueError('silent adhan source')
        gain = min(.13 / max(.001, rms), .65 / peak)
        fade = min(RATE // 50, len(samples) // 4)
        data = array.array('h', (round(v * gain * min(1, i / fade, (len(samples) - 1 - i) / fade) * 32767)
                                for i, v in enumerate(samples)))
        path = OUT / 'adhan.wav'
        with wave.open(str(path), 'wb') as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())
        receipt = {
            'file': 'res://assets/audio/act1/sound_mood/adhan.wav',
            'id': 'adhan',
            'seconds': len(samples) / RATE,
            'pcmBytes': len(data) * 2,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'source': VIDEO_URL,
            'sourceTitle': TITLE,
            'sourceChannel': CHANNEL,
            'sourceUploadDate': UPLOAD_DATE,
            'sourceLocalFile': str(source),
            'sourceSha256': source_sha,
            'sourceDurationSeconds': len(pcm) / RATE,
            'startSeconds': start / RATE,
            'endSeconds': end / RATE,
            'permissionStatus': 'author-obtaining: the project author supplied this link, credits the rights holder in-game, and takes on obtaining written permission',
            'license': 'Rights reserved by the rights holder; used by the project author\'s instruction, permission being formalized',
            'attributionRequired': True,
            'preparation': 'ffmpeg decode; mono 24 kHz PCM16; silence trimmed with a 0.25 s margin; the whole call kept (no mid-phrase cuts); gain min(0.13/rms, 0.65/peak); 20 ms edge fades',
        }
    # merge into the bank registry
    credits_path = OUT / 'credits.json'
    credits = json.loads(credits_path.read_text())
    credits['clips'] = [c for c in credits['clips'] if c.get('id') != 'adhan'] + [receipt]
    order = {name: index for index, name in enumerate(['adhan', 'stove', 'tv', 'laughter', 'chatter', 'crows', 'dog_distant', 'axe', 'saw', 'gate', 'pot', 'cow', 'children', 'boots_snow', 'garmon'])}
    credits['clips'].sort(key=lambda c: order.get(c.get('id'), 99))
    credits_path.write_text(json.dumps(credits, ensure_ascii=False, indent=2) + '\n')
    print(f"adhan: seconds={receipt['seconds']:.2f} rms={rms:.4f} peak={peak:.3f} gain={gain:.4f} sha256={receipt['sha256']}")
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
