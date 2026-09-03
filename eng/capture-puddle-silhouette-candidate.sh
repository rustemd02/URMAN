#!/bin/sh

# Produces only a versioned, test-only puddle silhouette receipt. It never
# enters the game bootstrap and never writes wetness-matrix evidence.
set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/puddle_silhouette_candidate"
SOURCE_SCENES="
$URMAN_ROOT/game/scenes/zones/style_benchmark_day_street.tscn
$URMAN_ROOT/game/scenes/zones/style_benchmark_kara_urman_night.tscn
$URMAN_ROOT/game/scenes/zones/chapter1_zirat_road.tscn
"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-puddle-silhouette-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-puddle-silhouette-capture.XXXXXX")

cleanup() {
  rm -f "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"
}
trap cleanup EXIT

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  echo "Godot/.NET toolchain is missing under .tools/." >&2
  exit 1
fi

# The candidate may only replace Mesh resources on in-memory instances. Pin the
# exact authored benchmark inputs so source-scene writes fail the receipt.
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
  res://tests/puddle_silhouette_candidate_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$CAPTURE_LOG"
if has_runtime_errors "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  echo "Godot reported puddle-silhouette capture errors or leaks." >&2
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi

SOURCE_HASHES_AFTER=$(printf '%s\n' "$SOURCE_SCENES" | sed '/^$/d' | xargs shasum -a 256)
if [ "$SOURCE_HASHES_BEFORE" != "$SOURCE_HASHES_AFTER" ]; then
  echo "Puddle silhouette capture must not change authored benchmark scene inputs." >&2
  exit 1
fi

MANIFEST="$OUTPUT_DIR/puddle_silhouette_candidate_manifest.json"
README="$OUTPUT_DIR/README.md"
[ -s "$MANIFEST" ] || { echo "Puddle silhouette manifest was not written." >&2; exit 1; }
[ -s "$README" ] || { echo "Puddle silhouette README was not written." >&2; exit 1; }

python3 - "$MANIFEST" "$OUTPUT_DIR" <<'PY'
import hashlib
import json
import pathlib
import struct
import sys

manifest_path = pathlib.Path(sys.argv[1]).resolve()
output_dir = pathlib.Path(sys.argv[2]).resolve()
manifest = json.loads(manifest_path.read_text())

def fail(message):
    raise SystemExit(f"puddle-silhouette gate: {message}")

def close(actual, expected):
    return isinstance(actual, (float, int)) and abs(actual - expected) <= 0.0005

if manifest.get("kind") != "urman.godot_puddle_silhouette_candidate" or manifest.get("version") != "v1":
    fail("unexpected manifest identity")
if manifest.get("status") != "OPEN" or manifest.get("silhouette_acceptance") != "OPEN":
    fail("candidate/art acceptance must remain OPEN")
non_mutation = manifest.get("non_mutation", {})
for key in ("test_only",):
    if non_mutation.get(key) is not True:
        fail(f"{key} must be true")
for key in ("production_scenes_changed", "runtime_or_shader_changed", "material_roughness_changed", "collider_added_or_changed", "saves_or_narrative_changed"):
    if non_mutation.get(key) is not False:
        fail(f"{key} must be false")
if not close(non_mutation.get("source_runtime_roughness"), 0.9):
    fail("source roughness must remain 0.90")

contact = manifest.get("exact_relief_contact", {})
if contact.get("status") != "PASS" or not close(contact.get("tolerance_m"), 0.005):
    fail("exact relief contact must PASS at 5 mm")
if contact.get("raw_production_origins_changed") is not False or contact.get("source_and_candidate_samples_pass") is not True:
    fail("candidate contact receipt must not write production origins")

mesh = manifest.get("candidate_mesh", {})
if mesh.get("source_mesh") != "CylinderMesh" or mesh.get("candidate_mesh") != "ArrayMesh":
    fail("expected CylinderMesh -> ArrayMesh test-only mesh replacement")
if mesh.get("candidate_mesh_replacements") != 15 or mesh.get("expected_replacements") != 15:
    fail("must replace exactly fifteen PuddlePatch meshes")
if mesh.get("materials_preserved") is not True or mesh.get("collision_objects_added") != 0 or mesh.get("source_mesh_references_restored") is not True:
    fail("candidate must preserve material/collision and restore source meshes")

totals = manifest.get("totals", {})
if totals.get("patches") != 15 or totals.get("expected_patches") != 15 or totals.get("captures") != 3 or totals.get("expected_captures") != 3:
    fail("expected 15 patches and three diagnostic captures")

expected_scenes = {"day_street", "kara_urman_edge", "zirat_road"}
scenes = manifest.get("scenes", [])
if len(scenes) != 3 or {scene.get("Name") for scene in scenes} != expected_scenes:
    fail("diagnostic must cover day, Kara-Urman and Zirat")
for scene in scenes:
    if scene.get("PatchCount") != scene.get("CandidateMeshReplacements"):
        fail(f"{scene.get('Name')}: every patch must receive a candidate mesh")
    if scene.get("CollisionObjectCountBefore") != scene.get("CollisionObjectCountAfter"):
        fail(f"{scene.get('Name')}: collision object count changed")
    if not all(scene.get(key) is True for key in ("SourceMaterialPass", "SourceMeshPass", "MaterialReferencesPreserved", "OnlyMeshesReplaced", "SourceMeshesRestored")):
        fail(f"{scene.get('Name')}: source/material/mesh isolation failed")
    for phase in ("SourceContacts", "CandidateContacts"):
        contacts = scene.get(phase, [])
        if len(contacts) != scene.get("PatchCount") or not all(contact.get("WithinTolerance") is True and contact.get("ColliderMatchesRelief") is True for contact in contacts):
            fail(f"{scene.get('Name')}: {phase} must prove exact-owner contact for every patch")

captures = manifest.get("captures", [])
if len(captures) != 3 or {capture.get("Scene") for capture in captures} != expected_scenes:
    fail("missing scene captures")
expected_outputs = set()
for capture in captures:
    relative = capture.get("Output")
    if not isinstance(relative, str) or pathlib.PurePosixPath(relative).is_absolute() or ".." in pathlib.PurePosixPath(relative).parts:
        fail(f"unsafe output: {relative!r}")
    path = (manifest_path.parents[4] / relative).resolve()
    if output_dir not in path.parents or not path.is_file():
        fail(f"capture escapes output directory: {relative!r}")
    expected_outputs.add(path.name)
    payload = path.read_bytes()
    if payload[:8] != b"\x89PNG\r\n\x1a\n":
        fail(f"not a PNG: {relative!r}")
    width, height = struct.unpack(">II", payload[16:24])
    if (width, height) != (1920, 1080) or capture.get("Width") != width or capture.get("Height") != height:
        fail(f"wrong PNG dimensions: {relative!r}")
    if capture.get("Sha256") != hashlib.sha256(payload).hexdigest():
        fail(f"SHA-256 mismatch: {relative!r}")
    tiles = capture.get("Tiles", [])
    if len(tiles) != 4 or [tile.get("Cell") for tile in tiles] != ["source_flat_overview", "candidate_faceted_overview", "source_flat_detail", "candidate_faceted_detail"]:
        fail(f"invalid diagnostic layout: {relative!r}")
    if not all(tile.get("Width") == 960 and tile.get("Height") == 540 and isinstance(tile.get("PixelSha256"), str) and len(tile["PixelSha256"]) == 64 for tile in tiles):
        fail(f"invalid tile evidence: {relative!r}")

actual_outputs = {path.name for path in output_dir.glob("godot_*_puddle_silhouette_diagnostic_1080p.png")}
if actual_outputs != expected_outputs:
    fail(f"unexpected/stale candidate PNGs: {sorted(actual_outputs ^ expected_outputs)!r}")

print("puddle-silhouette gate: status=OPEN contact=PASS source=PASS patches=15 mesh-replacements=15 captures=3")
PY

echo "puddle-silhouette gate: authored benchmark scene SHA-256 inputs unchanged"
shasum -a 256 "$MANIFEST" "$README" "$OUTPUT_DIR"/*.png
