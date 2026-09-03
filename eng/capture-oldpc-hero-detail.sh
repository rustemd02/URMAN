#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-oldpc-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-oldpc-capture.XXXXXX")
trap 'rm -f "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"' EXIT HUP INT TERM

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
if grep -q '^ERROR:' "$IMPORT_LOG"; then
  cat "$IMPORT_LOG"
  echo "Godot reported OldPc asset import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/oldpc_hero_detail_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  exit 1
fi
cat "$CAPTURE_LOG"
if grep -q '^ERROR:' "$CAPTURE_LOG" || grep -Eiq 'SCRIPT ERROR|ObjectDB instances leaked|RID allocations leaked' "$CAPTURE_LOG"; then
  echo "Godot reported OldPc capture errors or leaks." >&2
  exit 1
fi

find "$URMAN_ROOT/docs/urman_knowledge_base/art/oldpc_hero_detail_candidate" \
  -maxdepth 1 -type f -name 'godot_*_1080p.png' -print0 \
  | xargs -0 shasum -a 256
