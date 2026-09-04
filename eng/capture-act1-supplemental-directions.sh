#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
OUTPUT_INPUT=$1
mkdir -p "$OUTPUT_INPUT"
OUTPUT_DIR=$(CDPATH= cd -- "$OUTPUT_INPUT" && pwd -P)

case "$OUTPUT_DIR" in
  "$URMAN_ROOT"|"$URMAN_ROOT"/*)
    echo "Capture output must be outside the repository: $OUTPUT_DIR" >&2
    exit 1
    ;;
esac

if find "$OUTPUT_DIR" -mindepth 1 -maxdepth 1 -print -quit | grep -q .; then
  echo "Capture output directory must be empty: $OUTPUT_DIR" >&2
  exit 1
fi

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

"$DOTNET" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-cap004-import.XXXXXX")
if ! "$GODOT" --headless --audio-driver Dummy --log-file "$IMPORT_LOG" --path game --import >"$IMPORT_LOG.out" 2>&1; then
  cat "$IMPORT_LOG.out"
  exit 1
fi
rm -f "$IMPORT_LOG" "$IMPORT_LOG.out"

if ! "$GODOT" --headless --display-driver macos --audio-driver Dummy \
  --path "$URMAN_ROOT/game" --resolution 1280x720 \
  "--urman-act1-supplemental-output=$OUTPUT_DIR" \
  res://tests/act1_supplemental_directional_capture.tscn >/dev/null 2>&1; then
  echo "Godot supplemental capture failed." >&2
  exit 1
fi

python3 - "$OUTPUT_DIR" <<'PY'
import hashlib, json, pathlib, sys
output = pathlib.Path(sys.argv[1]).resolve()
receipt = json.loads((output / "act1_supplemental_directional_receipt.json").read_text())
frames = receipt["frames"]
assert len(frames) == 10, f"expected 10 supplemental frames, found {len(frames)}"
shas = {f["sha256"] for f in frames}
assert len(shas) == 10, "supplemental frame SHA-256 values are not unique"
assert receipt["root_viewport"] is True and receipt["subviewport_used"] is False
print(f"act1 supplemental directions: PASS {len(frames)}/10 unique root-viewport frames")
PY
