#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
TEXTURE_DIR="$URMAN_ROOT/game/assets/textures/painterly"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/texture_candidate_frames/v3"
VERIFY_REPORT=$(mktemp "${TMPDIR:-/tmp}/urman-texture-v3-verify.XXXXXX.md")
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-texture-v3-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-texture-v3-capture.XXXXXX")

cleanup() {
  rm -f "$VERIFY_REPORT" "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"
}
trap cleanup EXIT

set +e
"$URMAN_ROOT/eng/verify-painterly-textures.sh" --report "$VERIFY_REPORT" \
  "$TEXTURE_DIR/weathered_wood_boards_v3_albedo.png" \
  "$TEXTURE_DIR/aged_plaster_v3_albedo.png" \
  "$TEXTURE_DIR/damp_earth_v3_albedo.png" \
  "$TEXTURE_DIR/pine_foliage_v3_albedo.png" \
  "$TEXTURE_DIR/mossy_stone_v3_albedo.png" \
  "$TEXTURE_DIR/old_fabric_v3_albedo.png"
VERIFY_STATUS=$?
set -e
cat "$VERIFY_REPORT"
if [ "$VERIFY_STATUS" -ne 0 ]; then
  echo "Painterly v3 image gate failed." >&2
  exit 2
fi

"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  cat "$IMPORT_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$IMPORT_LOG"
if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$IMPORT_LOG" "$IMPORT_LOG.godot" 2>/dev/null; then
  echo "Godot reported v3 import errors." >&2
  cat "$IMPORT_LOG.godot" 2>/dev/null || true
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/texture_candidate_v3_frame_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$CAPTURE_LOG"
if grep -Eq '^(ERROR:|SCRIPT ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|libc\+\+abi:)' "$CAPTURE_LOG" "$CAPTURE_LOG.godot" 2>/dev/null; then
  echo "Godot reported v3 capture errors or leaks." >&2
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi

shasum -a 256 "$OUTPUT_DIR"/*.png
