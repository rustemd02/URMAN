#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

# Validate source/derived ownership and hashes before launching Blender. This
# gate is host-independent and must pass even when the pinned Blender binary is
# unavailable or crashes during GPU backend startup.
"$URMAN_ROOT/eng/verify-asset-registry.sh"

BLENDER="$URMAN_ROOT/.tools/blender/Blender.app/Contents/MacOS/Blender"
BLEND="$URMAN_ROOT/assets/source/blender/urman_modular_kit.blend"
CHARACTER_BLEND="$URMAN_ROOT/assets/source/blender/urman_character_kit.blend"

if [ ! -x "$BLENDER" ]; then
  echo "Pinned Blender 4.5 LTS is missing: $BLENDER" >&2
  exit 1
fi

verify_blender_script() {
  blend_file=$1
  script_file=$2
  log_file=$(mktemp "${TMPDIR:-/tmp}/urman-blender-verify.XXXXXX")
  trap 'rm -f "$log_file"' EXIT HUP INT TERM
  if "$BLENDER" --background "$blend_file" --python "$script_file" >"$log_file" 2>&1; then
    blender_status=0
  else
    blender_status=$?
  fi
  cat "$log_file"
  if [ "$blender_status" -ne 0 ]; then
    if [ "$blender_status" -eq 139 ] || grep -Eiq 'SIGSEGV|Segmentation fault|blender\.crash\.txt|GPU_backend_type_selection_detect|metal_is_supported' "$log_file"; then
      echo "asset-verifier: HOST_TOOLCHAIN_BLOCKED (Blender startup/Metal backend): $script_file" >&2
    else
      echo "asset-verifier: Blender verifier failed (exit $blender_status): $script_file" >&2
    fi
    exit 1
  fi
  # Blender 4.5 can print an unhandled Python traceback but still return 0.
  # Treat that output as a failed asset gate instead of accepting stale files.
  if grep -q '^Traceback (most recent call last):' "$log_file"; then
    echo "Blender verifier raised a Python traceback: $script_file" >&2
    exit 1
  fi
  rm -f "$log_file"
  trap - EXIT HUP INT TERM
}

verify_blender_script "$BLEND" tools/blender/verify_modular_environment.py
verify_blender_script "$CHARACTER_BLEND" tools/blender/verify_character_kit.py
python3 -m json.tool assets/asset_registry.json >/dev/null
printf '%s\n' "asset-registry-smoke: valid JSON, source/derived hashes, environment LOD provenance and character-kit provenance"
