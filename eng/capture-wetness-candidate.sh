#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/wetness_candidate"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-wetness-candidate-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-wetness-candidate-capture.XXXXXX")

cleanup() {
  rm -f "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"
}
trap cleanup EXIT

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  echo "Godot/.NET toolchain is missing under .tools/." >&2
  exit 1
fi

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
  res://tests/wetness_candidate_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi
cat "$CAPTURE_LOG"
if has_runtime_errors "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  echo "Godot reported wetness-candidate capture errors or leaks." >&2
  cat "$CAPTURE_LOG.godot" 2>/dev/null || true
  exit 1
fi

MANIFEST="$OUTPUT_DIR/wetness_candidate_manifest.json"
README="$OUTPUT_DIR/README.md"
[ -s "$MANIFEST" ] || { echo "Wetness candidate manifest was not written." >&2; exit 1; }
[ -s "$README" ] || { echo "Wetness candidate README was not written." >&2; exit 1; }

python3 - "$MANIFEST" <<'PY'
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
manifest = json.loads(manifest_path.read_text())

# The C# receipt intentionally uses its default PascalCase JSON contract;
# accept either casing so the wrapper validates the artifact rather than
# imposing a second serialization owner.
def get(obj, key, default=None):
    if key in obj:
        return obj[key]
    pascal = key[0].upper() + key[1:]
    return obj.get(pascal, default)

def fail(message: str) -> None:
    raise SystemExit(f"wetness-candidate gate: {message}")

if get(manifest, "kind") != "urman.godot_wetness_candidate_capture":
    fail("unexpected manifest kind")
if get(manifest, "status") != "OPEN":
    fail(f"status must remain OPEN, got {get(manifest, 'status')!r}")
if get(manifest, "roughness_acceptance") != "OPEN":
    fail("roughness acceptance must remain OPEN")

source = get(manifest, "source_runtime_material", {})
if get(source, "status") != "PASS" or get(source, "expected_roughness") != 0.9:
    fail("source/runtime material did not verify at roughness 0.90")

totals = get(manifest, "totals", {})
for key, expected in (("clusters", 5), ("expected_clusters", 5),
                      ("patches", 15), ("expected_patches", 15),
                      ("material_backed_patches", 15)):
    if get(totals, key) != expected:
        fail(f"{key} must be {expected}, got {get(totals, key)!r}")

scene_names = [get(scene, "name") for scene in get(manifest, "scenes", [])]
if scene_names != ["day_street", "house_old_pc", "kara_urman_edge", "zirat_road"]:
    fail(f"unexpected benchmark scene order: {scene_names!r}")

contact = get(manifest, "contact_gate", {})
contact_status = get(contact, "status")
captures = get(manifest, "captures", [])
candidate_isolation = get(manifest, "candidate_material_isolation", {})
if get(candidate_isolation, "status") != "PASS":
    fail("candidate material isolation must verify exactly fifteen in-memory clones")
if contact_status == "OPEN":
    if get(manifest, "rendered") is not False:
        fail("OPEN contact gate must suppress rendering")
    if captures:
        fail("OPEN contact gate must have no captures")
    if get(totals, "candidate_shader_material_clones") != 15:
        fail("OPEN contact gate must still verify exactly fifteen in-memory candidate clones")
elif contact_status == "PASS":
    if get(manifest, "rendered") is not True or len(captures) != 3:
        fail("contact PASS requires day/Kara/Zirat candidate captures")
    if get(totals, "candidate_shader_material_clones") != 15:
        fail("contact PASS requires exactly fifteen in-memory ShaderMaterial clones")
    expected = {"day_street", "kara_urman_edge", "zirat_road"}
    if {get(capture, "scene") for capture in captures} != expected:
        fail("contact PASS captures must cover day, Kara and Zirat")
else:
    fail(f"unexpected contact gate status {contact_status!r}")

print(
    "wetness-candidate gate: "
    f"status={get(manifest, 'status')} contact={contact_status} "
    f"clusters={get(totals, 'clusters')}/5 patches={get(totals, 'patches')}/15 "
    f"rendered={'yes' if get(manifest, 'rendered') else 'no'}"
)
PY

shasum -a 256 "$MANIFEST" "$README"
