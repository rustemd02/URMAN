#!/bin/sh

set -eu

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <output-directory-outside-repository>" >&2
  exit 2
fi

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
if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  echo "Pinned Godot/.NET toolchain is missing under .tools/." >&2
  exit 1
fi

TEMP_BASE=$(printenv TMPDIR || true)
if [ -z "$TEMP_BASE" ]; then
  TEMP_BASE=/tmp
fi
LOG_DIR=$(mktemp -d "$TEMP_BASE/urman-act1-full-route-core.XXXXXX")
trap 'rm -rf "$LOG_DIR"' EXIT HUP INT TERM
CAPTURE_TIMEOUT_SECONDS=${URMAN_CAPTURE_TIMEOUT_SECONDS:-300}

if [ "$("$DOTNET" --version)" != "10.0.302" ]; then
  echo "The capture harness requires pinned .NET SDK 10.0.302." >&2
  exit 1
fi

. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

if [ ! -f "$URMAN_ROOT/game/obj/project.assets.json" ]; then
  "$DOTNET" restore Urman.slnx --disable-build-servers --verbosity minimal -m:1
fi

"$DOTNET" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

has_runtime_errors() {
  for log in "$@"; do
    [ -f "$log" ] || continue
    if grep -Eq '^(ERROR:|SCRIPT ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|ERROR: [0-9]+ RID allocations|libc[+][+]abi:)' "$log"; then
      return 0
    fi
  done
  return 1
}

# This harness is silent, but the real 3D capture still opens a rendering context;
# run it only with explicit user approval.
if ! "$GODOT" --headless --audio-driver Dummy --log-file "$LOG_DIR/import.godot.log" --path game --import >"$LOG_DIR/import.log" 2>&1; then
  cat "$LOG_DIR/import.log"
  cat "$LOG_DIR/import.godot.log" 2>/dev/null || true
  exit 1
fi
cat "$LOG_DIR/import.log"
if has_runtime_errors "$LOG_DIR/import.log" "$LOG_DIR/import.godot.log"; then
  cat "$LOG_DIR/import.godot.log" 2>/dev/null || true
  echo "Godot asset import reported errors or leaks." >&2
  exit 1
fi

# Keep the real Metal renderer while suppressing the desktop window/focus grab.
# `--headless` alone has no RenderingDevice on this macOS Godot build; the
# explicit macOS display driver provides an off-screen GPU context without
# presenting the demo to the user.
if ! perl -e 'alarm shift; exec @ARGV' "$CAPTURE_TIMEOUT_SECONDS" "$GODOT" \
  --headless \
  --display-driver macos \
  --audio-driver Dummy \
  --path "$URMAN_ROOT/game" \
  --resolution 1280x720 \
  --log-file "$LOG_DIR/capture.godot.log" \
  "--urman-act1-core-output=$OUTPUT_DIR" \
  res://tests/act1_full_route_core_world_capture.tscn >"$LOG_DIR/capture.log" 2>&1; then
  cat "$LOG_DIR/capture.log"
  cat "$LOG_DIR/capture.godot.log" 2>/dev/null || true
  echo "Godot full-route core-world capture failed." >&2
  exit 1
fi
cat "$LOG_DIR/capture.log"
if has_runtime_errors "$LOG_DIR/capture.log" "$LOG_DIR/capture.godot.log"; then
  cat "$LOG_DIR/capture.godot.log" 2>/dev/null || true
  echo "Godot full-route core-world capture reported errors or leaks." >&2
  exit 1
fi

python3 - "$OUTPUT_DIR" <<'PY'
import hashlib
import json
import pathlib
import struct
import sys

output = pathlib.Path(sys.argv[1]).resolve()
receipt_path = output / "act1_full_route_core_world_receipt.json"
visual_zones = {
    "arrival",
    "main_street",
    "babai_yard",
    "house_exterior",
    "connective_street_return",
    "fap_exterior",
    "house_interior",
    "fap_interior",
    "zirat",
    "kara_approach",
}
required_lateral_zones = {"arrival", "main_street", "babai_yard", "fap_exterior", "house_interior", "fap_interior", "zirat", "kara_approach"}
interior_zones = {"house_interior", "fap_interior"}

def fail(message: str) -> None:
    raise SystemExit(f"act1 full-route core-world capture gate: {message}")

if not receipt_path.is_file():
    fail("JSON receipt is missing")

try:
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
except json.JSONDecodeError as error:
    fail(f"invalid JSON receipt: {error}")

if receipt.get("kind") != "urman.godot_act1_full_route_core_world_capture":
    fail("unexpected receipt kind")
if receipt.get("schema_version") != 1:
    fail("unexpected receipt schema")
if receipt.get("production_scene") != "res://scenes/main.tscn":
    fail("receipt does not identify the production main scene")
if receipt.get("root_viewport") is not True or receipt.get("subviewport_used") is not False:
    fail("receipt does not prove root-viewport capture without SubViewport")
if receipt.get("capture_process_count") != 1:
    fail("receipt does not record the single production-root capture process")
if receipt.get("viewport") != {"width": 1280, "height": 720}:
    fail("receipt viewport is not 1280x720")

core = receipt.get("core_layer") or {}
if core.get("name") != "Act1CoreWorldGreybox" or core.get("presentation_only") is not True:
    fail("receipt does not prove the presentation-only Act1CoreWorldGreybox layer")
if core.get("visual_zone_count") != 8:
    fail("receipt does not record eight visual zones")
if core.get("visual_mesh_count", 0) <= 0:
    fail("receipt reports no core-world visual meshes")
if core.get("forbidden_gameplay_node_count") != 0:
    fail("presentation layer contains gameplay nodes")

frames = receipt.get("frames")
if not isinstance(frames, list) or len(frames) != 48:
    fail(f"expected 48 frame records, found {len(frames) if isinstance(frames, list) else 'non-list'}")
if receipt.get("frame_count") != 48:
    fail("receipt frame_count is not 48")

pngs = sorted(output.glob("*.png"))
if len(pngs) != 48:
    fail(f"expected exactly 48 PNGs, found {len(pngs)}")
if len({path.stem for path in pngs}) != 48:
    fail("PNG frame ids are not unique")

def png_size(path: pathlib.Path) -> tuple[int, int]:
    raw = path.read_bytes()
    if raw[:8] != bytes.fromhex("89504e470d0a1a0a") or len(raw) < 24:
        fail(f"invalid or truncated PNG: {path.name}")
    return struct.unpack(">II", raw[16:24])

actual_sha = {}
for path in pngs:
    if png_size(path) != (1280, 720):
        fail(f"{path.name} is not 1280x720")
    actual_sha[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()

allowed = {path.name for path in pngs} | {receipt_path.name}
unexpected = [path.name for path in output.iterdir() if path.name not in allowed]
if unexpected:
    fail(f"unexpected capture artifacts: {unexpected!r}")

seen_ids = set()
seen_shas = set()
seen_zones = set()
directions_by_zone = {}
for frame in frames:
    frame_id = frame.get("frame_id")
    visual_zone = frame.get("visual_zone")
    if not isinstance(frame_id, str) or frame_id in seen_ids:
        fail(f"unexpected or duplicate frame id: {frame_id!r}")
    seen_ids.add(frame_id)
    if visual_zone not in visual_zones:
        fail(f"unexpected visual zone for {frame_id!r}: {visual_zone!r}")
    seen_zones.add(visual_zone)
    directions_by_zone.setdefault(visual_zone, set()).add(frame.get("direction"))
    output_file = frame.get("output_file")
    if output_file != f"{frame_id}.png":
        fail(f"unsafe or mismatched output file for {frame_id!r}")
    if frame.get("output_path") != str((output / output_file).resolve()):
        fail(f"receipt output path is not exact for {frame_id!r}")
    if frame.get("width") != 1280 or frame.get("height") != 720:
        fail(f"receipt dimensions are wrong for {frame_id!r}")
    if frame.get("sha256") != actual_sha.get(output_file):
        fail(f"SHA-256 mismatch for {frame_id!r}")
    seen_shas.add(frame["sha256"])
    if frame.get("active_zone_id") != frame.get("logical_zone"):
        fail(f"active logical zone mismatch for {frame_id!r}")
    camera = frame.get("camera") or {}
    if set((camera.get("global_position") or {})) != {"x", "y", "z"}:
        fail(f"camera position is not represented as scalar x/y/z for {frame_id!r}")

if seen_zones != visual_zones:
    fail(f"visual zone coverage is incomplete: {seen_zones!r}")
if len(seen_shas) != 48:
    fail("frame SHA-256 values are not unique")
for zone in visual_zones:
    required = {"forward", "back"}
    if zone in required_lateral_zones:
        required |= {"left", "right"}
    if not required.issubset(directions_by_zone.get(zone, set())):
        fail(f"directional coverage is incomplete for {zone}: {directions_by_zone.get(zone, set())!r}")
    required_evidence_kind = "interior-360" if zone in interior_zones else "near-mid-far"
    if not any(frame.get("visual_zone") == zone and frame.get("evidence_kind") == required_evidence_kind for frame in frames):
        fail(f"{required_evidence_kind} evidence is missing for {zone}")

traversal = receipt.get("traversal") or {}
if traversal.get("start_waypoint") != "arrival" or traversal.get("endpoint_waypoint") != "kara_cliffhanger_endpoint":
    fail("arrival-to-Kara traversal endpoints are missing")
if traversal.get("waypoint_count", 0) < 10 or traversal.get("total_horizontal_meters", 0) <= 0:
    fail("full arrival-to-Kara waypoint traversal is incomplete")
waypoints = traversal.get("waypoints")
if not isinstance(waypoints, list) or not waypoints or not all(row.get("reached") is True for row in waypoints):
    fail("waypoint traversal receipt contains an unreached waypoint")

print("act1 full-route core-world capture gate: PASS 48/48 root-viewport PNGs / 10 visual zones / forward-back + lateral + near-mid-far + interior-360 / waypoint receipt")
print(f"receipt: {receipt_path}")
print(f"capture directory: {output}")
print(f"unique frame SHA-256 values: {len(seen_shas)}")
print(f"waypoints: {traversal['waypoint_count']} / {traversal['total_horizontal_meters']:.2f} horizontal meters")
PY

echo "act1 full-route core-world capture: real production first-person frames and receipt completed successfully."
