#!/bin/sh

set -eu

# M8 integration tool: validate the two recorded finale lines, place them where
# the runtime resolves them, point the two content assets at the files and run
# the audio checks. It never invents audio: every input is a real WAV supplied by
# the recordist, and the script refuses anything outside the brief's spec.
#
# usage: apply-act1-voice-recordings.sh <input-dir> [--take N] [--dry-run]
#   The folder may hold the final files (marat_call.wav, rinat_warning.wav) or the
#   raw takes the recording brief asks for (marat_call_take01.wav .. take03.wav,
#   rinat_warning_take01.wav .. take03.wav). Raw takes are never guessed at: the
#   caller must say which take was chosen with --take N.
#   room_tone.wav is accepted and reported, but it is not a game asset.
#   --dry-run validates and prints the planned change without touching the repo

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

INPUT_DIR=
DRY_RUN=0
TAKE=
while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1 ;;
    --take) shift; [ "$#" -gt 0 ] || { echo "--take needs a number" >&2; exit 2; }; TAKE=$1 ;;
    --take=*) TAKE=${1#--take=} ;;
    -*) echo "unknown option: $1" >&2; exit 2 ;;
    *) [ -z "$INPUT_DIR" ] || { echo "only one input directory is accepted" >&2; exit 2; }; INPUT_DIR=$1 ;;
  esac
  shift
done
[ -n "$INPUT_DIR" ] || { echo "usage: $0 <input-dir> [--take N] [--dry-run]" >&2; exit 2; }
case "$TAKE" in
  "") ;;
  *[!0-9]*) echo "--take must be a number, got: $TAKE" >&2; exit 2 ;;
  *) [ "$TAKE" -ge 1 ] && [ "$TAKE" -le 9 ] || { echo "--take must be between 1 and 9" >&2; exit 2; } ;;
esac
if [ ! -d "$INPUT_DIR" ]; then
  echo "input directory not found: $INPUT_DIR" >&2
  exit 1
fi
case "$INPUT_DIR" in "$URMAN_ROOT"|"$URMAN_ROOT"/*) echo "input must live outside the repository" >&2; exit 1 ;; esac

# Resolve one line: the final file wins; otherwise an explicitly chosen take.
resolve_recording() {
  base=$1
  if [ -f "$INPUT_DIR/$base.wav" ]; then
    printf '%s\n' "$INPUT_DIR/$base.wav"
    return 0
  fi
  padded="$INPUT_DIR/${base}_take0$TAKE.wav"
  plain="$INPUT_DIR/${base}_take$TAKE.wav"
  takes=$(ls "$INPUT_DIR/${base}_take"*.wav 2>/dev/null || true)
  if [ -z "$takes" ]; then
    echo "missing recording: expected $INPUT_DIR/$base.wav" >&2
    echo "  or raw takes named ${base}_take01.wav .. ${base}_take03.wav with --take N" >&2
    return 1
  fi
  if [ -z "$TAKE" ]; then
    echo "raw takes found for $base, but no take was chosen:" >&2
    for take in $takes; do echo "  $take" >&2; done
    echo "  pick one explicitly, for example: $0 $INPUT_DIR --take 2" >&2
    return 1
  fi
  if [ -f "$padded" ]; then printf '%s\n' "$padded"; return 0; fi
  if [ -f "$plain" ]; then printf '%s\n' "$plain"; return 0; fi
  echo "take $TAKE not found for $base; present:" >&2
  for take in $takes; do echo "  $take" >&2; done
  return 1
}

MARAT_SRC=$(resolve_recording marat_call) || exit 1
RINAT_SRC=$(resolve_recording rinat_warning) || exit 1

if [ -f "$INPUT_DIR/room_tone.wav" ]; then
  echo "room tone received: $INPUT_DIR/room_tone.wav (kept as the recordist's cleanup material; not a game asset)"
fi

DEST_DIR="$URMAN_ROOT/game/assets/audio/act1/voice"

# Validate the spec from the recording brief: mono, 44.1/48 kHz, 16 or 24 bit,
# peak at or below -3 dBFS, no long silent head or tail.
python3 - "$MARAT_SRC" "$RINAT_SRC" <<'PY'
import struct, sys, pathlib

def read_wav(path):
    data = pathlib.Path(path).read_bytes()
    if data[:4] != b'RIFF' or data[8:12] != b'WAVE':
        raise SystemExit(f"{path}: not a RIFF/WAVE file")
    pos = 12
    fmt = None
    samples = None
    while pos + 8 <= len(data):
        cid = data[pos:pos+4]
        size = struct.unpack('<I', data[pos+4:pos+8])[0]
        chunk = data[pos+8:pos+8+size]
        if cid == b'fmt ':
            fmt = struct.unpack('<HHIIHH', chunk[:16])
        elif cid == b'data':
            samples = chunk
        pos += 8 + size + (size % 2)
    if fmt is None or samples is None:
        raise SystemExit(f"{path}: missing fmt or data chunk")
    audio_format, channels, rate, _bps, block_align, bits = fmt
    if audio_format not in (1, 3):
        raise SystemExit(f"{path}: unsupported WAV format tag {audio_format}")
    if channels != 1:
        raise SystemExit(f"{path}: expected mono, got {channels} channel(s)")
    if rate not in (44100, 48000):
        raise SystemExit(f"{path}: expected 44.1 or 48 kHz, got {rate}")
    if bits not in (16, 24):
        raise SystemExit(f"{path}: expected 16 or 24 bit, got {bits}")
    return path, rate, bits, block_align, samples

def peak_dbfs(bits, block_align, samples):
    step = block_align
    peak = 0
    for i in range(0, len(samples) - step + 1, step):
        if bits == 16:
            value = struct.unpack('<h', samples[i:i+2])[0]
            peak = max(peak, abs(value) / 32768.0)
        else:
            raw = samples[i:i+3] + (b'\xff' if samples[i+2] & 0x80 else b'\x00')
            value = struct.unpack('<i', raw)[0]
            peak = max(peak, abs(value) / 8388608.0)
    if peak <= 0:
        return None
    import math
    return 20 * math.log10(peak)

def silence_edges(bits, block_align, samples, rate):
    # count leading/trailing blocks below -60 dBFS
    step = block_align
    def quiet(chunk):
        if bits == 16:
            value = abs(struct.unpack('<h', chunk[:2])[0]) / 32768.0
        else:
            raw = chunk[:3] + (b'\xff' if chunk[2] & 0x80 else b'\x00')
            value = abs(struct.unpack('<i', raw)[0]) / 8388608.0
        return value < 0.001
    head = 0
    while (head + 1) * step <= len(samples) and quiet(samples[head*step:(head+1)*step]):
        head += 1
    tail = 0
    total = len(samples) // step
    while tail < total - head and quiet(samples[(total - 1 - tail)*step:(total - tail)*step]):
        tail += 1
    return head / rate, tail / rate

for path in sys.argv[1:]:
    p, rate, bits, block_align, samples = read_wav(path)
    seconds = len(samples) / (rate * block_align)
    peak = peak_dbfs(bits, block_align, samples)
    if peak is None:
        raise SystemExit(f"{p}: file is silent; a recording is required")
    if peak > -3.0:
        raise SystemExit(f"{p}: peak {peak:.2f} dBFS is above the -3 dBFS ceiling")
    if seconds < 0.4 or seconds > 8.0:
        raise SystemExit(f"{p}: duration {seconds:.2f}s is outside the 0.4-8s range for these two lines")
    head, tail = silence_edges(bits, block_align, samples, rate)
    if head > 0.5 or tail > 0.5:
        raise SystemExit(f"{p}: leading/trailing silence is {head:.2f}s / {tail:.2f}s, trim to at most 0.5s")
    print(f"  {pathlib.Path(path).name}: mono {rate} Hz {bits}-bit {seconds:.2f}s peak {peak:.2f} dBFS head {head:.2f}s tail {tail:.2f}s OK")
PY

echo "recordings validated"

if [ "$DRY_RUN" -eq 1 ]; then
  echo "dry run: would copy into $DEST_DIR and point the two assets at"
  echo "  audio/act1/voice/marat_call.wav and audio/act1/voice/rinat_warning.wav"
  echo "then run eng/compile-game-content.sh and the audio smokes"
  exit 0
fi

mkdir -p "$DEST_DIR"
cp "$MARAT_SRC" "$DEST_DIR/marat_call.wav"
cp "$RINAT_SRC" "$DEST_DIR/rinat_warning.wav"

python3 - <<'PY'
import hashlib, json, pathlib
p = pathlib.Path('content/modules/urman-chapter1/definitions.json')
data = json.loads(p.read_text())
want = {
    'urman.chapter1:asset/audio-marat-voice': 'audio/act1/voice/marat_call.wav',
    'urman.chapter1:asset/audio-rinat-interruption': 'audio/act1/voice/rinat_warning.wav',
}
patched = 0
for entry in data:
    if not isinstance(entry, dict) or entry.get('id') not in want:
        continue
    new_file = want[entry['id']]
    disk = pathlib.Path('game/assets') / new_file
    entry['file'] = new_file
    entry['sha256'] = hashlib.sha256(disk.read_bytes()).hexdigest()
    patched += 1
if patched != 2:
    raise SystemExit(f"expected to patch 2 voice assets, patched {patched}")
p.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n")
print("content assets repointed with sha256")
PY

. "$URMAN_ROOT/eng/dotnet-env.sh"
./eng/compile-game-content.sh

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
GUARD=/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/protected_run.py
for scene in act1_audio_settings_smoke_test act1_audio_transition_smoke_test act1_footstep_smoke_test ambient_audio_smoke_test; do
  if [ -f "$GUARD" ]; then
    python3 "$GUARD" "$GODOT" --headless --audio-driver Dummy --path game "res://tests/$scene.tscn" >"/tmp/voice_$scene.log" 2>&1 || {
      echo "audio check failed: $scene (see /tmp/voice_$scene.log)" >&2; exit 1; }
  else
    "$GODOT" --headless --audio-driver Dummy --path game "res://tests/$scene.tscn" >"/tmp/voice_$scene.log" 2>&1 || {
      echo "audio check failed: $scene (see /tmp/voice_$scene.log)" >&2; exit 1; }
  fi
  echo "audio check passed: $scene"
done

echo "voice recordings integrated; human listening still required"
