#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
OUTPUT_DIR="$URMAN_ROOT/docs/urman_knowledge_base/art/style_calibration_candidate"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-calibration-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-style-calibration-capture.XXXXXX")

cleanup() {
  rm -f "$IMPORT_LOG" "$CAPTURE_LOG" "$IMPORT_LOG.godot" "$CAPTURE_LOG.godot"
}
trap cleanup EXIT

fail_closed() {
  reason=$1
  mkdir -p "$OUTPUT_DIR"
  python3 - "$OUTPUT_DIR" "$reason" <<'PY'
import datetime
import json
import pathlib
import sys

output = pathlib.Path(sys.argv[1])
reason = sys.argv[2]
for path in output.glob("godot_*_1080p.png"):
    path.unlink()
(output / "style_calibration_candidate_manifest.json").write_text(json.dumps({
    "schema_version": 1,
    "kind": "urman.godot_style_calibration_candidate",
    "captured_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "status": "OPEN",
    "technical_status": "ERROR",
    "acceptance": "OPEN",
    "rendered": False,
    "fail_closed": True,
    "reason": reason,
    "calibration_scope": {
        "test_only": True,
        "allowed_node_types": ["WorldEnvironment", "DirectionalLight3D", "OmniLight3D"],
        "runtime_scene_changed": False,
        "packed_scene_changed": False,
        "materials_changed": False,
        "shader_changed": False,
        "glb_or_registry_changed": False,
        "collision_changed": False,
        "save_or_narrative_changed": False,
    },
}, indent=2) + "\n", encoding="utf-8")
(output / "README.md").write_text(
    "# Style calibration candidate\n\n"
    "Status: **OPEN — fail-closed; no accepted capture was produced.**\n\n"
    f"Reason: `{reason.replace('`', chr(39))}`\n\n"
    "No production scene, material, shader, GLB/registry, collision, save or narrative owner was changed.\n",
    encoding="utf-8",
)
PY
}

has_runtime_errors() {
  for log in "$@"; do
    [ -f "$log" ] || continue
    if grep -Eq '^(ERROR:|SCRIPT ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|ERROR: [0-9]+ RID allocations|libc\+\+abi:)' "$log"; then
      return 0
    fi
  done
  return 1
}

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  fail_closed "Godot/.NET toolchain is missing under .tools/."
  exit 1
fi

if ! "$DOTNET" build game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1; then
  fail_closed "dotnet build failed before style-calibration capture."
  exit 1
fi

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  fail_closed "Godot import failed."
  exit 1
fi
if has_runtime_errors "$IMPORT_LOG" "$IMPORT_LOG.godot"; then
  cat "$IMPORT_LOG"
  fail_closed "Godot import reported errors or leaks."
  exit 1
fi

if ! "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game \
  res://tests/style_calibration_candidate_capture.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  fail_closed "Godot style-calibration harness exited non-zero; inspect the capture log."
  exit 1
fi
cat "$CAPTURE_LOG"
if has_runtime_errors "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  fail_closed "Godot style-calibration capture reported errors or leaks."
  exit 1
fi

MANIFEST="$OUTPUT_DIR/style_calibration_candidate_manifest.json"
README="$OUTPUT_DIR/README.md"
[ -s "$MANIFEST" ] || { fail_closed "Style-calibration manifest was not written."; exit 1; }
[ -s "$README" ] || { fail_closed "Style-calibration README was not written."; exit 1; }

python3 - "$MANIFEST" "$OUTPUT_DIR" "$URMAN_ROOT" <<'PY'
import hashlib
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
output = pathlib.Path(sys.argv[2])
root = pathlib.Path(sys.argv[3])
manifest = json.loads(manifest_path.read_text())

def fail(message):
    raise SystemExit(f"style-calibration gate: {message}")

if manifest.get("kind") != "urman.godot_style_calibration_candidate":
    fail("unexpected manifest kind")
if manifest.get("status") != "OPEN" or manifest.get("acceptance") != "OPEN":
    fail("candidate/art acceptance must remain OPEN")
if manifest.get("technical_status") != "PASS" or manifest.get("rendered") is not True:
    fail("real-driver candidate capture did not pass technically")
if manifest.get("resolution") != {"width": 1920, "height": 1080}:
    fail("capture resolution must be exactly 1920x1080")

scope = manifest.get("calibration_scope", {})
if scope.get("test_only") is not True or scope.get("candidate_environment_resource_cloned") is not True:
    fail("candidate must be test-only and clone its Environment resource")
if scope.get("overridden_node_types") != ["WorldEnvironment", "DirectionalLight3D", "OmniLight3D"]:
    fail("candidate may override only the three presentation node types")
for key in ("runtime_scene_changed", "packed_scene_changed", "materials_changed", "shader_changed", "glb_or_registry_changed", "collision_changed", "save_or_narrative_changed"):
    if scope.get(key) is not False:
        fail(f"non-mutation boundary failed for {key}")

captures = manifest.get("captures", [])
if len(captures) != 6:
    fail(f"expected six baseline/candidate captures, got {len(captures)}")
expected = {(scene, variant) for scene in ("day_street", "house_old_pc", "kara_urman_edge") for variant in ("baseline", "candidate")}
if {(capture.get("scene"), capture.get("variant")) for capture in captures} != expected:
    fail("missing or unexpected scene/variant pair")
for capture in captures:
    if capture.get("width") != 1920 or capture.get("height") != 1080:
        fail(f"wrong dimensions for {capture.get('scene')}:{capture.get('variant')}")
    path = root / capture.get("output", "")
    if not path.is_file():
        fail(f"capture is missing: {path}")
    if hashlib.sha256(path.read_bytes()).hexdigest() != capture.get("sha256"):
        fail(f"SHA-256 mismatch: {path}")
    isolation = capture.get("isolation", {})
    if isolation.get("isolated_subviewport") is not True:
        fail("SubViewport isolation was not confirmed")
    if capture.get("variant") == "candidate":
        if isolation.get("environment_clone_created") is not True:
            fail("candidate did not clone its Environment")
        if isolation.get("environment_resource_id_before") == isolation.get("environment_resource_id_after"):
            fail("candidate Environment resource identity was not isolated")
    if not isinstance(isolation.get("physics_nodes_before"), int) or not isinstance(isolation.get("physics_nodes_after"), int):
        fail("render-only physics ownership counts are missing")
    if isolation.get("physics_nodes_after") > isolation.get("physics_nodes_before"):
        fail("candidate calibration added physics nodes")
    if isolation.get("visual_mesh_count", 0) <= 0:
        fail("render-only clone lost all visual meshes")
    if isolation.get("collision_shape_count_before", 0) <= 0:
        fail("render-only clone did not observe imported collision shapes")
    if isolation.get("collision_shapes_removed") != isolation.get("collision_shape_count_before"):
        fail("render-only clone did not remove every disposable collision shape")
    if isolation.get("collision_shape_count_after") != 0:
        fail("render-only clone retained collision shapes")
    if isolation.get("active_physics_query_owner_count") != 0:
        fail("render-only clone retained an active physics-query owner")

print("style-calibration gate: status=OPEN technical=PASS captures=6 at 1920x1080")
PY

shasum -a 256 "$MANIFEST" "$README" "$OUTPUT_DIR"/*.png
