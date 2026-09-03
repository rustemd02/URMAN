#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-motion-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-motion-capture.XXXXXX")

"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
cat "$IMPORT_LOG"
if grep -q '^ERROR:' "$IMPORT_LOG"; then
  echo "Godot reported asset import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/style_motion_sweep_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$CAPTURE_LOG"
if grep -Eq '^(SCRIPT ERROR:|ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|ERROR: [0-9]+ RID allocations)' "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  echo "Godot reported style motion sweep errors or leaks." >&2
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi

for sheet in \
  docs/urman_knowledge_base/art/style_motion_sweep/godot_day_street_motion_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_motion_sweep/godot_house_old_pc_motion_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_motion_sweep/godot_kara_urman_edge_motion_sweep_1080p.png
do
  test -s "$sheet"
done

shasum -a 256 \
  docs/urman_knowledge_base/art/style_motion_sweep/godot_day_street_motion_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_motion_sweep/godot_house_old_pc_motion_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_motion_sweep/godot_kara_urman_edge_motion_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_motion_sweep/style_motion_sweep_manifest.json
