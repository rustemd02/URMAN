#!/bin/sh

set -eu

# M8 integration tool: validate the two recorded finale lines, place them where
# the runtime resolves them, point the two content assets at the files and run
# the audio checks. It never generates audio. Supplied WAVs need a provenance
# sidecar identifying recordings or licensed synthesis; validation is not an
# artistic, listening, or legal acceptance of the supplied declaration.
#
# usage: apply-act1-voice-recordings.sh <input-dir> [--take N] [--dry-run]
#   The folder may hold the final files (marat_call.wav, rinat_warning.wav) or the
#   raw takes the recording brief asks for (marat_call_take01.wav .. take03.wav,
#   rinat_warning_take01.wav .. take03.wav). Raw takes are never guessed at: the
#   caller must say which take was chosen with --take N.
#   room_tone.wav is accepted and reported, but it is not a game asset.
#   provenance.json identifies both selected files, exact spoken text, credits,
#   source, and a local copy/hash of the source permission or license evidence.
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
INPUT_DIR=$(CDPATH= cd -- "$INPUT_DIR" && pwd -P)
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

CONTENT_FILE="$URMAN_ROOT/content/modules/urman-chapter1/definitions.json"
PACK_FILES="$URMAN_ROOT/game/content/urman.chapter1.compiled.v1.json $URMAN_ROOT/game/content/urman.fullgame.compiled.v1.json"

# Validate before creating a rollback trap or changing any repository file.
# In particular, an invalid dry run must never delete an existing recording.
python3 - "$INPUT_DIR" "$MARAT_SRC" "$RINAT_SRC" <<'PY'
import hashlib, json, pathlib, re, sys

root = pathlib.Path(sys.argv[1]).resolve()
manifest = root / 'provenance.json'
if not manifest.is_file():
    raise SystemExit('missing provenance.json: supply source, license evidence, exact texts, selected WAV hashes and credits')
try:
    data = json.loads(manifest.read_text())
except (OSError, ValueError) as error:
    raise SystemExit(f'cannot read provenance.json: {error}')

def text(value, label):
    if not isinstance(value, str) or not value.strip():
        raise SystemExit(f'provenance: {label} must be nonempty text')
    return value

def sha(value, label):
    if not isinstance(value, str) or re.fullmatch('[0-9a-f]{64}', value) is None:
        raise SystemExit(f'provenance: {label} must be a lowercase SHA-256')
    return value

def local_file(value, label):
    relative = pathlib.Path(text(value, label))
    path = (root / relative).resolve()
    if relative.is_absolute() or not path.is_relative_to(root) or not path.is_file():
        raise SystemExit(f'provenance: {label} must name an existing file inside the input directory')
    return path

if not isinstance(data, dict) or data.get('schemaVersion') != 1:
    raise SystemExit('provenance: expected schemaVersion 1')
origin = data.get('origin')
if origin not in ('human-recording', 'licensed-synthetic-speech'):
    raise SystemExit('provenance: origin must distinguish human-recording from licensed-synthetic-speech')
text(data.get('source'), 'source')
license_info = data.get('license')
if not isinstance(license_info, dict):
    raise SystemExit('provenance: license must identify the retained evidence')
license_id = text(license_info.get('id'), 'license.id')
evidence = local_file(license_info.get('evidencePath'), 'license.evidencePath')
if evidence.stat().st_size == 0 or hashlib.sha256(evidence.read_bytes()).hexdigest() != sha(license_info.get('sha256'), 'license.sha256'):
    raise SystemExit('provenance: license evidence is empty or does not match its SHA-256')

models = {}
if origin == 'licensed-synthetic-speech':
    entries = data.get('models')
    if not isinstance(entries, list) or not entries:
        raise SystemExit('provenance: synthetic speech requires models with source hashes')
    for entry in entries:
        if not isinstance(entry, dict):
            raise SystemExit('provenance: each model must be an object')
        model_id = text(entry.get('id'), 'model.id')
        if model_id in models:
            raise SystemExit('provenance: duplicate model id')
        sha(entry.get('sha256'), 'model.sha256')
        sha(entry.get('configSha256'), 'model.configSha256')
        models[model_id] = entry
    engine = data.get('engine')
    if not isinstance(engine, dict):
        raise SystemExit('provenance: synthetic speech requires engine name/version')
    text(engine.get('name'), 'engine.name')
    text(engine.get('version'), 'engine.version')
    urls = data.get('sourceURLs')
    if not isinstance(urls, list) or not urls or any(not isinstance(url, str) or not url.startswith('https://') for url in urls):
        raise SystemExit('provenance: synthetic speech requires original HTTPS sourceURLs')
    processing = data.get('processing')
    if not isinstance(processing, list) or not processing:
        raise SystemExit('provenance: record synthesis and resampling/cleanup in processing')
    for step in processing:
        text(step, 'processing step')

expected = {
    'urman.chapter1:asset/audio-marat-voice': (pathlib.Path(sys.argv[2]).resolve(), 'Казанский… не отставай'),
    'urman.chapter1:asset/audio-rinat-interruption': (pathlib.Path(sys.argv[3]).resolve(), 'Не отвечай'),
}
recordings = data.get('recordings')
if not isinstance(recordings, list) or len(recordings) != 2:
    raise SystemExit('provenance: exactly two selected recordings are required')
seen = set()
for recording in recordings:
    if not isinstance(recording, dict):
        raise SystemExit('provenance: each recording must be an object')
    asset_id = recording.get('assetId')
    if asset_id not in expected or asset_id in seen:
        raise SystemExit('provenance: unknown or duplicate finale assetId')
    seen.add(asset_id)
    selected, spoken = expected[asset_id]
    path = local_file(recording.get('file'), 'recording.file')
    if path != selected or recording.get('spokenText') != spoken:
        raise SystemExit(f'provenance: selected file or exact authored spokenText does not match {asset_id}')
    if hashlib.sha256(path.read_bytes()).hexdigest() != sha(recording.get('sha256'), 'recording.sha256'):
        raise SystemExit(f'provenance: WAV SHA-256 mismatch for {asset_id}')
    text(recording.get('credit'), 'recording.credit')
    if origin == 'licensed-synthetic-speech' and recording.get('modelId') not in models:
        raise SystemExit('provenance: each synthetic recording must reference a declared modelId')

print(f'provenance file/hash checks passed: origin={origin}, license declaration={license_id}; source terms and listening require separate review')
PY

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
    if audio_format != 1:
        raise SystemExit(f"{path}: expected integer PCM WAV, got format tag {audio_format}")
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

# A successful installation must retain the evidence that was just checked.
# The immutable delivery key also prevents a later take from overwriting an
# earlier source, permission record or credit in the accepted audio structure.
DELIVERY_HASH=$(python3 - "$INPUT_DIR/provenance.json" <<'PY'
import hashlib, pathlib, sys
print(hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest())
PY
)
SOURCE_DELIVERY_DIR="$URMAN_ROOT/assets/source/audio/act1/voice/$DELIVERY_HASH"

if [ "$DRY_RUN" -eq 1 ]; then
  echo "dry run: would copy into $DEST_DIR and point the two assets at"
  echo "  audio/act1/voice/marat_call.wav and audio/act1/voice/rinat_warning.wav"
  echo "source recordings, provenance and license evidence: $SOURCE_DELIVERY_DIR"
  echo "then run eng/compile-game-content.sh and the audio smokes"
  exit 0
fi

# The mutation starts only after successful input checks. Preserve prior WAVs
# as well as content, so a failed replacement restores the previous recording.
ROLLBACK_DIR=$(mktemp -d "${TMPDIR:-/tmp}/urman-voice-rollback.XXXXXX")
cp "$CONTENT_FILE" "$ROLLBACK_DIR/definitions.json"
for pack in $PACK_FILES; do cp "$pack" "$ROLLBACK_DIR/$(basename "$pack")"; done
for voice in marat_call.wav rinat_warning.wav; do
  if [ -f "$DEST_DIR/$voice" ]; then cp "$DEST_DIR/$voice" "$ROLLBACK_DIR/$voice"; fi
done
APPLIED=0
SOURCE_DELIVERY_CREATED=0
rollback() {
  [ "$APPLIED" -eq 1 ] && return 0
  cp "$ROLLBACK_DIR/definitions.json" "$CONTENT_FILE"
  for pack in $PACK_FILES; do cp "$ROLLBACK_DIR/$(basename "$pack")" "$pack"; done
  for voice in marat_call.wav rinat_warning.wav; do
    if [ -f "$ROLLBACK_DIR/$voice" ]; then cp "$ROLLBACK_DIR/$voice" "$DEST_DIR/$voice"
    else rm -f "$DEST_DIR/$voice"; fi
  done
  rmdir "$DEST_DIR" 2>/dev/null || true
  if [ "$SOURCE_DELIVERY_CREATED" -eq 1 ]; then rm -rf "$SOURCE_DELIVERY_DIR"; fi
  echo "voice integration rolled back: content, both compiled packs and prior runtime WAVs restored" >&2
}
finish() {
  status=$?
  if [ "$APPLIED" -eq 0 ]; then rollback; fi
  rm -rf "$ROLLBACK_DIR"
  exit "$status"
}
trap finish EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

mkdir -p "$DEST_DIR"
cp "$MARAT_SRC" "$DEST_DIR/marat_call.wav"
cp "$RINAT_SRC" "$DEST_DIR/rinat_warning.wav"

if [ ! -e "$SOURCE_DELIVERY_DIR" ]; then SOURCE_DELIVERY_CREATED=1; fi
python3 - "$INPUT_DIR" "$SOURCE_DELIVERY_DIR" <<'PY'
import hashlib, json, pathlib, shutil, sys

source = pathlib.Path(sys.argv[1]).resolve()
destination = pathlib.Path(sys.argv[2]).resolve()
manifest = source / 'provenance.json'
data = json.loads(manifest.read_text())
files = ['provenance.json', data['license']['evidencePath']]
files.extend(recording['file'] for recording in data['recordings'])
if (source / 'room_tone.wav').is_file():
    files.append('room_tone.wav')

existed = destination.exists()
for relative in dict.fromkeys(files):
    original = (source / relative).resolve()
    target = (destination / relative).resolve()
    if not original.is_relative_to(source) or not target.is_relative_to(destination):
        raise SystemExit('source delivery path escapes its package')
    if existed:
        if not target.is_file() or hashlib.sha256(target.read_bytes()).digest() != hashlib.sha256(original.read_bytes()).digest():
            raise SystemExit('existing immutable voice source delivery differs: ' + str(target))
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(original, target)
print('voice source delivery retained and byte-checked: ' + str(destination))
PY

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
    entry['mediaType'] = 'audio/wav'
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
GUARD=${URMAN_GUARD:-$URMAN_ROOT/eng/protected_run.py}
[ -f "$GUARD" ] || { echo "userdata guard not found: $GUARD" >&2; exit 1; }
for scene in act1_audio_settings_smoke_test act1_audio_transition_smoke_test act1_footstep_smoke_test ambient_audio_smoke_test; do
  python3 "$GUARD" --clean "$GODOT" --headless --audio-driver Dummy --path game "res://tests/$scene.tscn" >"/tmp/voice_$scene.log" 2>&1 || {
    echo "audio check failed: $scene (see /tmp/voice_$scene.log)" >&2; exit 1; }
  echo "audio check passed: $scene"
done

APPLIED=1
echo "voice recordings integrated; human listening still required"
