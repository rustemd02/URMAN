#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/fullgame_boundary_runtime_capture"
MANIFEST="$OUTPUT_DIR/fullgame_boundary_runtime_manifest.json"

"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-boundary-runtime-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-boundary-runtime-capture.XXXXXX")
trap 'rm -f "$IMPORT_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG" "$CAPTURE_LOG.godot"' EXIT

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
cat "$IMPORT_LOG"
if grep -q '^ERROR:' "$IMPORT_LOG"; then
  echo "Godot reported asset import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/full_game_boundary_runtime_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  exit 1
fi
cat "$CAPTURE_LOG"
if grep -q '^ERROR:' "$CAPTURE_LOG"; then
  echo "Godot reported runtime-backed boundary capture errors." >&2
  exit 1
fi
if grep -Eiq 'leak|RID allocations|ObjectDB instances leaked|SCRIPT ERROR' "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  echo "Godot reported a runtime leak or script error." >&2
  exit 1
fi

python3 - "$MANIFEST" "$OUTPUT_DIR" <<'PY'
import hashlib
import json
import pathlib
import struct
import sys

manifest_path = pathlib.Path(sys.argv[1]).resolve()
output_dir = pathlib.Path(sys.argv[2]).resolve()
data = json.loads(manifest_path.read_text())
if data.get("kind") != "urman.godot_fullgame_boundary_runtime_capture":
    raise SystemExit("unexpected boundary runtime manifest kind")
if data.get("status") != "OPEN" or data.get("technical_status") != "PASS":
    raise SystemExit("boundary runtime capture is not technical PASS")
runtime = data.get("runtime_backed", {})
if runtime.get("runtime_bridge_present") is not True or runtime.get("compiled_scene_resolved") != "urman.fullgame:scene/act5-boundary":
    raise SystemExit("runtime-backed compiled scene contract failed")
if runtime.get("godot_zone_scene_path") != "res://scenes/zones/fullgame/act5_boundary.tscn":
    raise SystemExit("runtime-backed capture did not load the Act 5 boundary zone scene")
gate = data.get("gate", {})
if gate.get("visible_lod_meshes") != 8 or gate.get("behind_both_markers") is not True:
    raise SystemExit("GateA marker contract failed")
if float(gate.get("minimum_horizontal_clearance_m", 0)) < 1.0:
    raise SystemExit("GateA clearance is below 1m")
required = {
    "urman.fullgame:interaction/act5-aidar-choice",
    "urman.fullgame:interaction/act5-boundary-to-epilogue",
}
if set(gate.get("found_interaction_ids", [])) != required:
    raise SystemExit("runtime interaction targets do not match required Act 5 markers")
captures = data.get("captures", [])
if len(captures) != 2:
    raise SystemExit("expected standard and markers captures")
for capture in captures:
    rel = capture["Output"]
    if rel.startswith("/") or ".." in pathlib.PurePosixPath(rel).parts:
        raise SystemExit("unsafe capture path")
    path = (output_dir.parent.parent.parent.parent / rel).resolve()
    if output_dir not in path.parents:
        raise SystemExit("capture escaped output directory")
    raw = path.read_bytes()
    if raw[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"invalid PNG signature: {path}")
    width, height = struct.unpack(">II", raw[16:24])
    if (width, height) != (1920, 1080):
        raise SystemExit(f"unexpected PNG dimensions for {path}: {width}x{height}")
    if hashlib.sha256(raw).hexdigest() != capture["Sha256"]:
        raise SystemExit(f"SHA-256 mismatch for {path}")
marker_capture = next((capture for capture in captures if capture.get("Name") == "markers"), None)
if marker_capture is None:
    raise SystemExit("missing markers camera capture")
screen_space = marker_capture.get("ScreenSpace", {})
if screen_space.get("MarkersInsideViewport") is not True or screen_space.get("GateDoesNotOverlapMarkers") is not True:
    raise SystemExit("markers camera screen-space visibility contract failed")
markers = screen_space.get("Markers", [])
if len(markers) != 2 or any(marker.get("InsideViewport") is not True or marker.get("OverlapsGate") is not False for marker in markers):
    raise SystemExit("runtime marker projections are not visible and clear of GateA")
print("fullgame-boundary-runtime: PASS 2/2 captures / runtime bridge / 2 markers / GateA clearance")
PY

shasum -a 256 "$OUTPUT_DIR"/godot_act5_boundary_runtime_*_1080p.png "$MANIFEST" "$OUTPUT_DIR/README.md"
