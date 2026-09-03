#!/bin/sh

set -eu

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <output-directory-outside-repository>" >&2
  exit 2
fi

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
OUTPUT_INPUT=$1
mkdir -p "$OUTPUT_INPUT"
OUTPUT_DIR=$(CDPATH= cd -- "$OUTPUT_INPUT" && pwd)

case "$OUTPUT_DIR" in
  "$URMAN_ROOT"|"$URMAN_ROOT"/*)
    echo "Capture output must be outside the repository so evidence cannot enter git: $OUTPUT_DIR" >&2
    exit 1
    ;;
esac

if find "$OUTPUT_DIR" -mindepth 1 -maxdepth 1 -print -quit | grep -q .; then
  echo "Capture output directory must be empty: $OUTPUT_DIR" >&2
  exit 1
fi

TOOLCHAIN_LINK_CREATED=0
ASSET_LINK_CREATED=0
GODOT_IMPORTED_LINK_CREATED=0
LOG_DIR=""
EXPECTED_FRAME_COUNT=6
PRIMARY_ROOT=""
TEMP_BASE=$(printenv TMPDIR || true)
if [ -z "$TEMP_BASE" ]; then
  TEMP_BASE=/tmp
fi
LINK_RECORD=""

cleanup() {
  status=$?
  links_left=0
  set +e
  trap - EXIT HUP INT TERM

  if [ -n "$LOG_DIR" ] && [ -e "$LOG_DIR" ]; then
    rm -rf "$LOG_DIR"
  fi

  if [ -n "$LINK_RECORD" ] && [ -f "$LINK_RECORD" ]; then
    while IFS= read -r path; do
      if [ -L "$path" ]; then
        rm "$path"
      fi
      if [ -e "$path" -o -L "$path" ]; then
        links_left=1
      fi
    done < "$LINK_RECORD"
  fi

  if [ "$GODOT_IMPORTED_LINK_CREATED" -eq 1 ] && [ -L "$URMAN_ROOT/game/.godot/imported" ]; then
    rm "$URMAN_ROOT/game/.godot/imported"
  fi

  if [ "$ASSET_LINK_CREATED" -eq 1 ] && [ -L "$URMAN_ROOT/game/assets" ]; then
    rm "$URMAN_ROOT/game/assets"
  fi

  if [ "$TOOLCHAIN_LINK_CREATED" -eq 1 ] && [ -L "$URMAN_ROOT/.tools" ]; then
    rm "$URMAN_ROOT/.tools"
  fi

  if [ -n "$LOG_DIR" ] && [ -e "$LOG_DIR" ]; then
    echo "Capture harness cleanup failed for temporary log directory: $LOG_DIR" >&2
    status=1
  fi
  if [ "$ASSET_LINK_CREATED" -eq 1 ] && [ -e "$URMAN_ROOT/game/assets" -o -L "$URMAN_ROOT/game/assets" ]; then
    echo "Capture harness cleanup failed for temporary asset link." >&2
    status=1
  fi
  if [ "$TOOLCHAIN_LINK_CREATED" -eq 1 ] && [ -e "$URMAN_ROOT/.tools" -o -L "$URMAN_ROOT/.tools" ]; then
    echo "Capture harness cleanup failed for temporary toolchain link." >&2
    status=1
  fi
  if [ "$GODOT_IMPORTED_LINK_CREATED" -eq 1 ] && [ -e "$URMAN_ROOT/game/.godot/imported" -o -L "$URMAN_ROOT/game/.godot/imported" ]; then
    echo "Capture harness cleanup failed for temporary Godot imported-asset link." >&2
    status=1
  fi
  if [ "$links_left" -eq 1 ]; then
    echo "Capture harness cleanup failed for temporary asset links." >&2
    status=1
  fi

  if [ -n "$LINK_RECORD" ]; then
    rm -f "$LINK_RECORD"
  fi

  exit "$status"
}
trap cleanup EXIT HUP INT TERM
LINK_RECORD=$(mktemp "$TEMP_BASE/urman-act1-visual-review-links.XXXXXX")

find_primary_root() {
  candidate=$(printenv URMAN_PRIMARY_TOOLCHAIN_ROOT || true)
  if [ -n "$candidate" ]; then
    case "$candidate" in
      */.tools) candidate=$(dirname "$candidate") ;;
    esac
    if [ -x "$candidate/.tools/dotnet/dotnet" ] \
      && [ -x "$candidate/.tools/godot/Godot_mono.app/Contents/MacOS/Godot" ]; then
      PRIMARY_ROOT=$candidate
      return 0
    fi
    echo "URMAN_PRIMARY_TOOLCHAIN_ROOT does not contain the pinned toolchain: $candidate" >&2
    return 1
  fi

  while IFS= read -r candidate; do
    if [ -x "$candidate/.tools/dotnet/dotnet" ] \
      && [ -x "$candidate/.tools/godot/Godot_mono.app/Contents/MacOS/Godot" ]; then
      PRIMARY_ROOT=$candidate
      return 0
    fi
  done <<EOF
$(git -C "$URMAN_ROOT" worktree list --porcelain | awk '/^worktree / { print substr($0, 10) }')
EOF

  return 1
}

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  if ! find_primary_root; then
    echo "Pinned Godot/.NET toolchain is missing from this worktree and no primary worktree was found." >&2
    exit 1
  fi

  if [ ! -e "$URMAN_ROOT/.tools" ] && [ ! -L "$URMAN_ROOT/.tools" ]; then
    ln -s "$PRIMARY_ROOT/.tools" "$URMAN_ROOT/.tools"
    TOOLCHAIN_LINK_CREATED=1
  else
    echo "Cannot attach the primary toolchain because .tools already exists but is incomplete." >&2
    exit 1
  fi

  GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
  DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
fi

if [ ! -d "$URMAN_ROOT/game/assets" ]; then
  if [ -z "$PRIMARY_ROOT" ] && ! find_primary_root; then
    echo "game/assets is missing and no primary asset tree was found." >&2
    exit 1
  fi
  if [ ! -d "$PRIMARY_ROOT/game/assets" ]; then
    echo "Primary game/assets directory is missing: $PRIMARY_ROOT/game/assets" >&2
    exit 1
  fi
  ln -s "$PRIMARY_ROOT/game/assets" "$URMAN_ROOT/game/assets"
  ASSET_LINK_CREATED=1
fi

if [ -z "$PRIMARY_ROOT" ]; then
  if ! find_primary_root; then
    echo "The current asset tree has no pinned primary worktree available for import links." >&2
    exit 1
  fi
fi

if [ ! -d "$URMAN_ROOT/game/.godot" ]; then
  mkdir -p "$URMAN_ROOT/game/.godot"
fi
if [ ! -e "$URMAN_ROOT/game/.godot/imported" ] \
  && [ ! -L "$URMAN_ROOT/game/.godot/imported" ] \
  && [ -d "$PRIMARY_ROOT/game/.godot/imported" ]; then
  ln -s "$PRIMARY_ROOT/game/.godot/imported" "$URMAN_ROOT/game/.godot/imported"
  GODOT_IMPORTED_LINK_CREATED=1
fi

while IFS= read -r primary_asset; do
  relative=$(printf '%s\n' "$primary_asset" | sed "s#^$PRIMARY_ROOT/game/assets/##")
  current_asset="$URMAN_ROOT/game/assets/$relative"
  if [ ! -e "$current_asset" ] && [ ! -L "$current_asset" ]; then
    mkdir -p "$(dirname "$current_asset")"
    ln -s "$primary_asset" "$current_asset"
    printf '%s\n' "$current_asset" >> "$LINK_RECORD"
  fi
done <<EOF
$(find "$PRIMARY_ROOT/game/assets" -type f -print)
EOF

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  echo "Pinned Godot/.NET toolchain is not executable after setup." >&2
  exit 1
fi

. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

if [ "$("$DOTNET" --version)" != "10.0.302" ]; then
  echo "The capture harness requires pinned .NET SDK 10.0.302." >&2
  exit 1
fi

LOG_DIR=$(mktemp -d "$TEMP_BASE/urman-act1-visual-review.XXXXXX")

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

GODOT_RUNS=0
for FRAME_ID in \
  kara_forest_forward \
  kara_forest_back \
  kara_forest_left \
  kara_forest_right \
  zirat_forward \
  zirat_back
do
  GODOT_RUNS=$((GODOT_RUNS + 1))
  FRAME_LOG="$LOG_DIR/$FRAME_ID.log"
  FRAME_GODOT_LOG="$FRAME_LOG.godot"

  if ! "$GODOT" \
    --path "$URMAN_ROOT/game" \
    --resolution 1280x720 \
    --log-file "$FRAME_GODOT_LOG" \
    "--urman-act1-frame=$FRAME_ID" \
    "--urman-act1-output=$OUTPUT_DIR" \
    res://tests/act1_visual_review_capture.tscn >"$FRAME_LOG" 2>&1; then
    cat "$FRAME_LOG"
    cat "$FRAME_GODOT_LOG" 2>/dev/null || true
    echo "Godot failed for frame $FRAME_ID." >&2
    exit 1
  fi

  cat "$FRAME_LOG"
  if has_runtime_errors "$FRAME_LOG" "$FRAME_GODOT_LOG"; then
    cat "$FRAME_GODOT_LOG" 2>/dev/null || true
    echo "Godot reported an error or leak for frame $FRAME_ID." >&2
    exit 1
  fi
done

if [ "$GODOT_RUNS" -ne "$EXPECTED_FRAME_COUNT" ]; then
  echo "Internal capture-process count is $GODOT_RUNS, expected 6." >&2
  exit 1
fi

python3 - "$OUTPUT_DIR" <<'PY'
import hashlib
import json
import pathlib
import struct
import sys

output = pathlib.Path(sys.argv[1]).resolve()
receipt_path = output / "act1_visual_review_receipt.json"
expected = {
    "kara_forest_forward": "kara_urman_night",
    "kara_forest_back": "kara_urman_night",
    "kara_forest_left": "kara_urman_night",
    "kara_forest_right": "kara_urman_night",
    "zirat_forward": "zirat_road",
    "zirat_back": "zirat_road",
}

def fail(message: str) -> None:
    raise SystemExit(f"act1 visual review gate: {message}")

if not receipt_path.is_file():
    fail("JSON receipt is missing")

pngs = sorted(path for path in output.glob("*.png"))
if {path.stem for path in pngs} != set(expected):
    fail(f"expected exactly six named PNGs, found {[path.name for path in pngs]!r}")
if len(pngs) != 6:
    fail(f"expected six PNGs, found {len(pngs)}")

allowed = set(expected) | {"act1_visual_review_receipt"}
unexpected = [path.name for path in output.iterdir() if path.stem not in allowed]
if unexpected:
    fail(f"unexpected capture artifacts: {unexpected!r}")

def png_size(path: pathlib.Path) -> tuple[int, int]:
    raw = path.read_bytes()
    if raw[:8] != bytes.fromhex("89504e470d0a1a0a"):
        fail(f"invalid PNG signature: {path.name}")
    if len(raw) < 24:
        fail(f"truncated PNG: {path.name}")
    return struct.unpack(">II", raw[16:24])

actual_sha = {}
for path in pngs:
    if png_size(path) != (1280, 720):
        fail(f"{path.name} is not 1280x720")
    actual_sha[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
if len(set(actual_sha.values())) != 6:
    fail("PNG SHA-256 values are not unique")

try:
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
except json.JSONDecodeError as error:
    fail(f"invalid JSON receipt: {error}")

if receipt.get("kind") != "urman.godot_act1_visual_review_capture":
    fail("unexpected receipt kind")
if receipt.get("production_scene") != "res://scenes/main.tscn":
    fail("receipt does not identify the current production main scene")
if receipt.get("root_viewport") is not True or receipt.get("subviewport_used") is not False:
    fail("receipt does not prove root-viewport capture without SubViewport")
if receipt.get("capture_process_count") != 6:
    fail("receipt does not record six capture processes")
if receipt.get("viewport") != {"width": 1280, "height": 720}:
    fail("receipt viewport is not 1280x720")

frames = receipt.get("frames")
if not isinstance(frames, list) or len(frames) != 6:
    fail("receipt does not contain six frame records")
if receipt.get("frame_count") != 6:
    fail("receipt frame_count is not six")

seen_positions = set()
seen_targets = set()
seen_zones = set()
seen_shas = set()
seen_ids = set()
for frame in frames:
    frame_id = frame.get("frame_id")
    if frame_id not in expected or frame_id in seen_ids:
        fail(f"unexpected or duplicate frame id: {frame_id!r}")
    seen_ids.add(frame_id)
    if frame.get("zone") != expected[frame_id] or frame.get("active_zone_id") != expected[frame_id]:
        fail(f"active zone mismatch for {frame_id!r}")
    seen_zones.add(frame["active_zone_id"])
    if frame.get("output") != f"{frame_id}.png":
        fail(f"unsafe or mismatched output name for {frame_id!r}")
    if frame.get("width") != 1280 or frame.get("height") != 720:
        fail(f"receipt dimensions are wrong for {frame_id!r}")
    if frame.get("sha256") != actual_sha[frame["output"]]:
        fail(f"SHA-256 mismatch for {frame_id!r}")
    seen_shas.add(frame["sha256"])
    camera = frame.get("camera") or {}
    position = camera.get("global_position") or {}
    target = camera.get("target") or {}
    if set(position) != {"x", "y", "z"} or set(target) != {"x", "y", "z"}:
        fail(f"camera position/target is not represented by scalar x/y/z fields for {frame_id!r}")
    if any(isinstance(value, bool) or not isinstance(value, (int, float)) for value in (*position.values(), *target.values())):
        fail(f"camera position/target contains a non-scalar value for {frame_id!r}")
    seen_positions.add(tuple(position[key] for key in ("x", "y", "z")))
    seen_targets.add(tuple(target[key] for key in ("x", "y", "z")))

if seen_ids != set(expected) or len(seen_positions) != 6 or len(seen_targets) != 6 or len(seen_shas) != 6:
    fail("six frame records are not distinct by id, camera position, target, and SHA-256")
if seen_zones != {"kara_urman_night", "zirat_road"}:
    fail(f"unexpected active zone ids: {seen_zones!r}")

print("act1 visual review gate: PASS 6/6 root-viewport PNGs / 1280x720 / unique SHA-256 / two active zones")
print(f"receipt: {receipt_path}")
for frame_id in sorted(expected):
    print(f"{frame_id}: {expected[frame_id]} {actual_sha[frame_id + '.png']}")
PY

echo "act1 visual review capture: six separate Godot processes completed successfully."
