#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$URMAN_ROOT/.tools/dotnet/dotnet"
OUTPUT_DIR="${URMAN_TEXTURE_AB_OUTPUT_DIR:-$URMAN_ROOT/docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v3}"
VERSIONS="${URMAN_TEXTURE_AB_VERSIONS:-v3,v4}"
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-texture-ab-import.XXXXXX")
CAPTURE_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-texture-ab-capture.XXXXXX")

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
manifest = output / "texture_candidate_ab_diagnostic_manifest.json"
readme = output / "README.md"
if not manifest.exists():
    with manifest.open("x", encoding="utf-8") as stream:
        json.dump({
            "schema_version": 1,
            "kind": "urman.texture_candidate_ab_diagnostic",
            "captured_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "status": "OPEN",
            "technical_status": "ERROR",
            "rendered": False,
            "reason": reason,
            "acceptance": "OPEN",
            "non_mutation": {"test_only": True, "runtime_owners_changed": False},
        }, stream, indent=2)
        stream.write("\n")
if not readme.exists():
    safe_reason = reason.replace("`", "'")
    with readme.open("x", encoding="utf-8") as stream:
        stream.write("# Texture candidate A/B diagnostic\n\n")
        stream.write("Status: **OPEN — fail-closed, no accepted capture.**\n\n")
        stream.write(f"Exact reason: `{safe_reason}`\n\n")
        stream.write("No runtime/shader/collider/save/narrative owner was changed.\n")
PY
}

if [ -d "$OUTPUT_DIR" ] && [ -n "$(find "$OUTPUT_DIR" -mindepth 1 -maxdepth 1 -print -quit)" ]; then
  echo "Refusing to overwrite existing diagnostic artifacts in $OUTPUT_DIR" >&2
  exit 1
fi

if [ ! -x "$GODOT" ] || [ ! -x "$DOTNET" ]; then
  fail_closed "Godot/.NET toolchain is missing under .tools/."
  exit 1
fi

if ! "$DOTNET" build game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1; then
  fail_closed "dotnet build failed before Godot capture."
  exit 1
fi

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
  fail_closed "Godot import failed."
  exit 1
fi
if has_runtime_errors "$IMPORT_LOG" "$IMPORT_LOG.godot"; then
  cat "$IMPORT_LOG"
  fail_closed "Godot import reported errors or leaks."
  exit 1
fi

if ! URMAN_TEXTURE_AB_OUTPUT_DIR="$OUTPUT_DIR" URMAN_TEXTURE_AB_VERSIONS="$VERSIONS" \
  "$GODOT" --log-file "$CAPTURE_LOG.godot" --path game res://tests/texture_candidate_ab_diagnostic.tscn >"$CAPTURE_LOG" 2>&1; then
  cat "$CAPTURE_LOG"
  fail_closed "Godot A/B harness exited non-zero; inspect capture log output."
  exit 1
fi
cat "$CAPTURE_LOG"
if has_runtime_errors "$CAPTURE_LOG" "$CAPTURE_LOG.godot"; then
  fail_closed "Godot A/B capture reported errors or leaks."
  exit 1
fi

MANIFEST="$OUTPUT_DIR/texture_candidate_ab_diagnostic_manifest.json"
README="$OUTPUT_DIR/README.md"
[ -s "$MANIFEST" ] || { fail_closed "A/B manifest was not written."; exit 1; }
[ -s "$README" ] || { fail_closed "A/B README was not written."; exit 1; }

python3 - "$MANIFEST" "$OUTPUT_DIR" <<'PY'
import hashlib
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
output = pathlib.Path(sys.argv[2])
manifest = json.loads(manifest_path.read_text())
if manifest.get("kind") != "urman.texture_candidate_ab_diagnostic":
    raise SystemExit("texture A/B gate: unexpected manifest kind")
if manifest.get("status") != "OPEN":
    raise SystemExit("texture A/B gate: acceptance status must remain OPEN")
if manifest.get("technical_status") != "PASS":
    raise SystemExit(f"texture A/B gate: technical status is {manifest.get('technical_status')!r}")
captures = manifest.get("captures", [])
if len(captures) != 9:
    raise SystemExit(f"texture A/B gate: expected 9 sheets, got {len(captures)}")
for capture in captures:
    path = output / capture["Output"]
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != capture["Sha256"]:
        raise SystemExit(f"texture A/B gate: hash mismatch for {path}")
print(
    "texture A/B gate: status=OPEN technical=PASS "
    f"captures={len(captures)} contacts={manifest['passing_contact_samples']}/{manifest['expected_contact_samples']}"
)
PY

shasum -a 256 "$MANIFEST" "$README" "$OUTPUT_DIR"/*.png
