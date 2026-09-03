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

run_blender_generator tools/blender/generate_modular_environment.py
run_blender_generator tools/blender/generate_character_kit.py
./eng/verify-assets.sh

printf '%s\n' "asset-rebuild: environment and character Blender/GLB sources regenerated and verified"
