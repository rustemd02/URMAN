#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

BLENDER="$URMAN_ROOT/.tools/blender/Blender.app/Contents/MacOS/Blender"
if [ ! -x "$BLENDER" ]; then
  echo "Pinned Blender 4.5 LTS is missing: $BLENDER" >&2
  exit 1
fi

run_blender_generator() {
  script_file=$1
  log_file=$(mktemp "${TMPDIR:-/tmp}/urman-blender-generate.XXXXXX")
  trap 'rm -f "$log_file"' EXIT HUP INT TERM
  if ! "$BLENDER" --background --python "$script_file" -- --root "$URMAN_ROOT" >"$log_file" 2>&1; then
    cat "$log_file"
    exit 1
  fi
  cat "$log_file"
  # Blender 4.5 may return 0 after an unhandled Python exception.
  if grep -q '^Traceback (most recent call last):' "$log_file"; then
    echo "Blender generator raised a Python traceback: $script_file" >&2
    exit 1
  fi
  rm -f "$log_file"
  trap - EXIT HUP INT TERM
}

# Reject unseeded generators before mutating any authored Blender/GLB output.
python3 eng/verify-blender-determinism.py
run_blender_generator tools/blender/generate_modular_environment.py
run_blender_generator tools/blender/generate_character_kit.py
./eng/verify-assets.sh
# VIS-108: rewrite the diffable GLB structure receipts; `git diff assets/source/blender/receipts`
# then shows exactly which objects, slots or triangle counts a rebuild changed.
python3 tools/blender/glb_structure_receipt.py game/assets/generated/urman_modular_kit.glb \
  game/assets/generated/urman_character_kit.glb

printf '%s\n' "asset-rebuild: environment and character Blender/GLB sources regenerated and verified"
