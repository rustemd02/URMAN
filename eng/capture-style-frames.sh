#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
.tools/dotnet/dotnet build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-capture.XXXXXX")

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
cat "$IMPORT_LOG"
if grep -q '^ERROR:' "$IMPORT_LOG"; then
  echo "Godot reported asset import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/style_frame_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  exit 1
fi
cat "$CAPTURE_LOG"
if grep -q '^ERROR:' "$CAPTURE_LOG"; then
  echo "Godot reported style capture errors." >&2
  exit 1
fi

shasum -a 256 docs/urman_knowledge_base/art/style_frames/godot_*_1080p.png
