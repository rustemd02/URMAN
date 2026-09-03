#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-temporal-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-temporal-capture.XXXXXX")

"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  cat "$IMPORT_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$IMPORT_LOG"
cat "$IMPORT_LOG.godot" 2>/dev/null || true
if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$IMPORT_LOG" "$IMPORT_LOG.godot" 2>/dev/null; then
  echo "Godot reported asset import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/style_temporal_comfort_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$CAPTURE_LOG"
cat "$CAPTURE_LOG.godot" 2>/dev/null || true
if grep -Eq '^(SCRIPT ERROR:|ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|ERROR: [0-9]+ RID allocations)' "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  echo "Godot reported style temporal sweep errors or leaks." >&2
  exit 1
fi

for sheet in \
  docs/urman_knowledge_base/art/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_temporal_sweep/godot_house_old_pc_temporal_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_temporal_sweep/godot_kara_urman_edge_temporal_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_temporal_sweep/style_temporal_sweep_manifest.json
do
  test -s "$sheet"
done

python3 - <<'PY'
import json
from pathlib import Path

path = Path("docs/urman_knowledge_base/art/style_temporal_sweep/style_temporal_sweep_manifest.json")
data = json.loads(path.read_text())
assert data["status"] == "technical_temporal_evidence_only"
assert data["external_review_required"] is True
assert len(data["scenes"]) == 3
for scene in data["scenes"]:
    sanitization = scene["render_only_sanitization"]
    assert sanitization["visual_mesh_count"] > 0
    assert sanitization["collision_shape_count_before"] > 0
    assert sanitization["collision_shapes_removed"] == sanitization["collision_shape_count_before"]
    assert sanitization["collision_shape_count_after"] == 0
    assert sanitization["active_physics_query_owner_count"] == 0
    assert len(scene["modes"]) == 2
    for mode in scene["modes"]:
        assert len(mode["metrics"]) == 3
        for metric in mode["metrics"]:
            assert metric["render_valid"] is True
            assert metric["frame_count"] == 12
            assert metric["black_pixel_fraction"] < 0.995
PY

shasum -a 256 \
  docs/urman_knowledge_base/art/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_temporal_sweep/godot_house_old_pc_temporal_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_temporal_sweep/godot_kara_urman_edge_temporal_sweep_1080p.png \
  docs/urman_knowledge_base/art/style_temporal_sweep/style_temporal_sweep_manifest.json
