#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/oldpc_hero_detail_motion_sweep"
MANIFEST="$OUTPUT_DIR/oldpc_hero_detail_motion_manifest.json"
"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-oldpc-motion-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-oldpc-motion-capture.XXXXXX")
trap 'rm -f "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"' EXIT HUP INT TERM

mkdir -p "$OUTPUT_DIR"
rm -f "$OUTPUT_DIR"/godot_oldpc_*_motion_1080p.png "$MANIFEST"

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
if grep -q '^ERROR:' "$IMPORT_LOG"; then
  cat "$IMPORT_LOG"
  echo "Godot reported OldPc motion import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/oldpc_hero_detail_motion_sweep.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  exit 1
fi
cat "$CAPTURE_LOG"
if grep -q '^ERROR:' "$CAPTURE_LOG" "$CAPTURE_LOG.godot" 2>/dev/null \
  || grep -Eiq 'SCRIPT ERROR|ObjectDB instances leaked|RID allocations leaked|resources still in use' "$CAPTURE_LOG" "$CAPTURE_LOG.godot" 2>/dev/null; then
  echo "Godot reported OldPc motion capture errors or leaks." >&2
  exit 1
fi

if ! grep -Eq 'Metal|Vulkan|OpenGL' "$CAPTURE_LOG" \
  || ! grep -Eq 'Forward\+' "$CAPTURE_LOG"; then
  echo "OldPc motion capture did not report a real 3D renderer." >&2
  exit 1
fi

[ -s "$MANIFEST" ] || { echo "OldPc motion manifest is missing." >&2; exit 1; }

python3 - "$MANIFEST" "$URMAN_ROOT" <<'PY'
import json
import pathlib
import struct
import sys

manifest_path = pathlib.Path(sys.argv[1])
root = pathlib.Path(sys.argv[2])
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

def fail(message):
    raise SystemExit(f"oldpc motion gate: {message}")

if manifest.get("kind") != "urman.godot_oldpc_hero_detail_motion_sweep":
    fail("unexpected manifest kind")
if manifest.get("status") != "OPEN" or manifest.get("technical_status") != "PASS":
    fail("candidate must remain OPEN while technical status is PASS")
if manifest.get("tile") != {"width": 640, "height": 360}:
    fail("tile dimensions must be 640x360")
if manifest.get("sheet") != {"columns": 3, "rows": 3, "width": 1920, "height": 1080}:
    fail("contact-sheet dimensions must be 1920x1080")
if manifest.get("fovs") != [65, 75, 90]:
    fail("FOV matrix must be 65/75/90")
if manifest.get("rows") != ["bob_up", "neutral", "bob_down"]:
    fail("head-bob rows are incomplete")
required = {
    "OldPc_DriveSlot_LOD0",
    "OldPc_DriveSlot_LOD1",
    "OldPc_LabelPlate_LOD0",
    "OldPc_LabelPlate_LOD1",
}
if set(manifest.get("required_hero_details", [])) != required:
    fail("required OldPc hero-detail contract is incomplete")

sheets = manifest.get("sheets", [])
if {sheet.get("name") for sheet in sheets} != {"near", "mid"}:
    fail("near and mid sheets are both required")

def png_size(path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
        fail(f"not a PNG with IHDR: {path}")
    return struct.unpack(">II", data[16:24])

for sheet in sheets:
    frames = sheet.get("frames", [])
    if len(frames) != 9:
        fail(f"{sheet.get('name')} must contain nine frames")
    path = root / sheet.get("output", "")
    if not path.is_file():
        fail(f"missing sheet: {path}")
    if png_size(path) != (1920, 1080):
        fail(f"wrong sheet dimensions: {path}")
    if len(frames) != len({(f.get("row"), f.get("column")) for f in frames}):
        fail(f"duplicate frame coordinate in {sheet.get('name')}")
    for frame in frames:
        if frame.get("details_found") != 4:
            fail(f"hero details missing in {sheet.get('name')} frame")
        if frame.get("luma_span", 0) < 0.08:
            fail(f"blank/cleared frame in {sheet.get('name')} frame")

print("oldpc-motion-manifest: PASS 2 sheets / 18 frames / 4 details / 1920x1080")
PY

find "$OUTPUT_DIR" \
  -maxdepth 1 -type f -name 'godot_oldpc_*_motion_1080p.png' -print0 \
  | xargs -0 shasum -a 256
