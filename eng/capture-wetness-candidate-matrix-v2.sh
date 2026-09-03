#!/bin/sh

# Produces only a versioned, test-only matrix receipt. It never calls the game
# bootstrap and never writes the v1 wetness evidence directory.
set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/wetness_candidate_matrix_v2"
SOURCE_SCENES="
$URMAN_ROOT/game/scenes/zones/style_benchmark_day_street.tscn
$URMAN_ROOT/game/scenes/zones/style_benchmark_house_pc.tscn
$URMAN_ROOT/game/scenes/zones/style_benchmark_kara_urman_night.tscn
$URMAN_ROOT/game/scenes/zones/chapter1_zirat_road.tscn
"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-wetness-matrix-v2-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-wetness-matrix-v2-capture.XXXXXX")

cleanup() {
  rm -f "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"
}
trap cleanup EXIT

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  echo "Godot/.NET toolchain is missing under .tools/." >&2
  exit 1
fi

# The candidate harness may only move nodes in memory. Pin the exact authored
# scene inputs so accidental source-origin writes fail the evidence run.
SOURCE_HASHES_BEFORE=$(printf '%s\n' "$SOURCE_SCENES" | sed '/^$/d' | xargs shasum -a 256)

"$DOTNET" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

has_runtime_errors() {
  for log in "$@"; do
    [ -f "$log" ] || continue
    if grep -Eq '^(ERROR:|SCRIPT ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|libc\+\+abi:)' "$log"; then
      return 0
    fi
  done
  return 1
}

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  cat "$IMPORT_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$IMPORT_LOG"
if has_runtime_errors "$IMPORT_LOG" "$IMPORT_LOG.godot"; then
  echo "Godot reported asset import errors." >&2
  cat "$IMPORT_LOG.godot" 2>/dev/null || true
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game \
  res://tests/wetness_candidate_matrix_v2_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$CAPTURE_LOG"
if has_runtime_errors "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  echo "Godot reported wetness-matrix capture errors or leaks." >&2
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi

SOURCE_HASHES_AFTER=$(printf '%s\n' "$SOURCE_SCENES" | sed '/^$/d' | xargs shasum -a 256)
if [ "$SOURCE_HASHES_BEFORE" != "$SOURCE_HASHES_AFTER" ]; then
  echo "Wetness matrix must not change authored benchmark scene inputs." >&2
  exit 1
fi

MANIFEST="$OUTPUT_DIR/wetness_candidate_matrix_v2_manifest.json"
README="$OUTPUT_DIR/README.md"
[ -s "$MANIFEST" ] || { echo "Wetness matrix manifest was not written." >&2; exit 1; }
[ -s "$README" ] || { echo "Wetness matrix README was not written." >&2; exit 1; }

python3 - "$MANIFEST" "$OUTPUT_DIR" <<'PY'
import hashlib
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1]).resolve()
output_dir = pathlib.Path(sys.argv[2]).resolve()
manifest = json.loads(manifest_path.read_text())

def normalized(name):
    return ''.join(character for character in name.lower() if character.isalnum())

def get(obj, key, default=None):
    wanted = normalized(key)
    for actual, value in obj.items():
        if normalized(actual) == wanted:
            return value
    return default

def fail(message):
    raise SystemExit(f"wetness-matrix-v2 gate: {message}")

def close(actual, expected):
    return isinstance(actual, (float, int)) and abs(actual - expected) <= 0.0005

if get(manifest, "kind") != "urman.godot_wetness_candidate_matrix":
    fail("unexpected manifest kind")
if get(manifest, "version") != "v2":
    fail("missing v2 receipt version")
if get(manifest, "status") != "OPEN" or get(manifest, "roughness_acceptance") != "OPEN":
    fail("candidate acceptance must remain OPEN")
if get(manifest, "candidate_roughness_matrix") != [0.4, 0.5, 0.6]:
    fail(f"unexpected roughness matrix: {get(manifest, 'candidate_roughness_matrix')!r}")
if not close(get(manifest, "contact_tolerance_m"), 0.005):
    fail("candidate contact tolerance must be 0.005 m")

source = get(manifest, "source_runtime_material", {})
if get(source, "status") != "PASS" or not close(get(source, "expected_roughness"), 0.9):
    fail("source/runtime roughness must remain 0.90 and PASS")
if get(source, "candidate_clones_are_in_memory_only") is not True or get(source, "source_overrides_restored_before_shutdown") is not True:
    fail("source material isolation/restoration did not prove")
origins = get(manifest, "production_puddle_origins", {})
if get(origins, "status") != "PASS" or get(origins, "changed") is not False:
    fail("raw production puddle origins must now pass without a harness source write")

contact = get(manifest, "contact_gate", {})
if get(contact, "status") != "PASS" or get(contact, "tolerance_applies_to") != "test-only in-memory candidate instances":
    fail("test-only contact/owner gate must PASS before rendering")

totals = get(manifest, "totals", {})
for key, expected in (("clusters", 5), ("expected_clusters", 5), ("patches", 15),
                      ("expected_patches", 15), ("material_backed_patches", 15),
                      ("expected_captures", 9)):
    if get(totals, key) != expected:
        fail(f"{key} must be {expected}, got {get(totals, key)!r}")

isolation = get(manifest, "candidate_material_isolation", {})
if get(isolation, "status") != "PASS" or get(isolation, "exact_patch_clone_count_per_row") != 15:
    fail("matrix clone isolation must be 15 per row")
if get(isolation, "matrix_rows") != 3 or get(isolation, "aggregate_shader_material_clones") != 45:
    fail("matrix must contain exactly three isolated rows / 45 total clones")

rows = get(manifest, "rows", [])
if len(rows) != 3:
    fail("matrix must contain three rows")
expected_scenes = {"day_street", "kara_urman_edge", "zirat_road"}
for row, roughness in zip(rows, (0.4, 0.5, 0.6)):
    if not close(get(row, "candidate_roughness"), roughness):
        fail(f"row roughness does not match {roughness}")
    if get(row, "candidate_shader_material_clones") != 15 or get(row, "isolation") != "PASS" or get(row, "source_still_unchanged") is not True:
        fail(f"row {roughness} failed in-memory isolation")
    row_captures = get(row, "captures", [])
    if len(row_captures) != 3 or {get(capture, "scene") for capture in row_captures} != expected_scenes:
        fail(f"row {roughness} does not cover required scenes")

raw_samples = []
for scene in get(manifest, "scenes", []):
    scene_contact = get(scene, "contact", {})
    raw_samples.extend(get(scene_contact, "raw_production_origin_samples", []))
    for alignment in get(scene_contact, "test_only_alignment_meters", []):
        if not close(get(alignment, "test_only_alignment_meters", 0.0), 0.0):
            fail("raw production contact still requires candidate-local alignment")
if len(raw_samples) != 15 or not all(get(sample, "within_tolerance") is True and get(sample, "collider_matches_relief") is True for sample in raw_samples):
    fail("all fifteen raw production puddle origins must contact their exact relief owner within 5 mm")

captures = get(manifest, "captures", [])
if get(manifest, "rendered") is not True or len(captures) != 9:
    fail("all nine candidate renders must exist after PASS gates")
for capture in captures:
    relative = get(capture, "output")
    if not isinstance(relative, str) or pathlib.PurePosixPath(relative).is_absolute() or ".." in pathlib.PurePosixPath(relative).parts:
        fail(f"unsafe candidate output path: {relative!r}")
    file_path = (manifest_path.parents[4] / relative).resolve()
    if output_dir not in file_path.parents or not file_path.is_file():
        fail(f"candidate output escapes v2 evidence directory: {relative!r}")
    if get(capture, "width") != 1920 or get(capture, "height") != 1080:
        fail(f"candidate capture has wrong dimensions: {relative!r}")
    if file_path.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
        fail(f"candidate is not a PNG: {relative!r}")
    actual_sha = hashlib.sha256(file_path.read_bytes()).hexdigest()
    if get(capture, "sha256") != actual_sha:
        fail(f"SHA-256 mismatch: {relative!r}")

print("wetness-matrix-v2 gate: status=OPEN contact=PASS source=PASS rows=3 clones=45 captures=9")
PY

echo "wetness-matrix-v2 gate: authored benchmark scene SHA-256 inputs unchanged"
shasum -a 256 "$MANIFEST" "$README" "$OUTPUT_DIR"/*.png
