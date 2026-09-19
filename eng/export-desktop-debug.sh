#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
BUILD_ROOT="$URMAN_ROOT/build"
LOCK_DIR="$BUILD_ROOT/.desktop-artifacts.lock"
stage_root=
lock_owned=0

cleanup_export() {
  [ -z "$stage_root" ] || rm -rf "$stage_root"
  if [ "$lock_owned" -eq 1 ]; then
    rm -rf "$LOCK_DIR"
    lock_owned=0
  fi
}

handle_export_signal() {
  signal_status=$1
  trap - EXIT HUP INT TERM
  cleanup_export
  exit "$signal_status"
}

trap cleanup_export EXIT
trap 'handle_export_signal 129' HUP
trap 'handle_export_signal 130' INT
trap 'handle_export_signal 143' TERM

command -v zip >/dev/null 2>&1 || {
  echo "desktop-export: the 'zip' command is required" >&2
  exit 1
}

mkdir -p "$BUILD_ROOT"
if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  echo "desktop-export: artifact lock is already held: $LOCK_DIR" >&2
  exit 1
fi
lock_owned=1
lock_token="export-$$-$(date -u +%Y%m%dT%H%M%SZ)"
printf '%s\n' "$lock_token" >"$LOCK_DIR/owner"

stage_root=$(mktemp -d "$BUILD_ROOT/.desktop-stage.XXXXXX")
mkdir -p "$stage_root/macos" "$stage_root/windows" "$stage_root/logs"

# Stabilize generated campaign packs and import settings before recording the
# sources used by the real exports. Any later input change refuses provenance.
sh "$URMAN_ROOT/eng/compile-game-content.sh"
import_log="$stage_root/logs/preflight-import.stdout.log"
if ! "$GODOT" --headless --path game --import >"$import_log" 2>&1; then
  cat "$import_log"
  exit 1
fi
cat "$import_log"
if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$import_log"; then
  echo "desktop-export: import preflight reported errors" >&2
  exit 1
fi
python3 "$URMAN_ROOT/eng/desktop_build_provenance.py" --root "$URMAN_ROOT" start \
  --mode debug --output "$stage_root/export-start.json"

run_export() {
  preset=$1
  destination=$2
  log_slug=$3
  export_log="$stage_root/logs/$log_slug.stdout.log"
  godot_log="$stage_root/logs/$log_slug.godot.log"

  if ! "$GODOT" --headless --log-file "$godot_log" --path game --export-debug "$preset" "$destination" >"$export_log" 2>&1; then
    cat "$export_log"
    [ -f "$godot_log" ] && cat "$godot_log"
    return 1
  fi

  for log in "$export_log" "$godot_log"; do
    [ -f "$log" ] || continue
    cat "$log"
    if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log"; then
      echo "Godot reported export errors for preset: $preset" >&2
      return 1
    fi
  done
}

run_export macOS "$stage_root/macos/URMAN.zip" macos
run_export "Windows Desktop" "$stage_root/windows/URMAN.exe" windows

# Package only the fresh staging directory. -X strips host-specific extra
# attributes, so a Windows artifact cannot inherit __MACOSX/AppleDouble files
# or stale payloads from a previous build/windows directory.
(
  cd "$stage_root"
  zip -q -r -X "$stage_root/URMAN-windows-x86_64.zip" windows
)

python3 "$URMAN_ROOT/eng/desktop_build_provenance.py" --root "$URMAN_ROOT" complete \
  --start "$stage_root/export-start.json" --mac-zip "$stage_root/macos/URMAN.zip" \
  --windows-zip "$stage_root/URMAN-windows-x86_64.zip" --windows-exe "$stage_root/windows/URMAN.exe" \
  --output "$stage_root/build-provenance.json"

# Verify the isolated outputs before replacing any published build artifact.
URMAN_DESKTOP_BUILD_PROVENANCE="$stage_root/build-provenance.json" \
  URMAN_DESKTOP_LOCK_TOKEN=$lock_token "$URMAN_ROOT/eng/verify-desktop-artifacts.sh" \
  "$stage_root/macos/URMAN.zip" \
  "$stage_root/windows/URMAN.exe" \
  "$stage_root/URMAN-windows-x86_64.zip" \
  "$stage_root/desktop-artifact-receipt.json"

# The old receipt describes the old artifact set. Invalidate it before the
# first publication move so an interrupted/partial publish can never retain a
# stale PASS marker. The final verifier recreates it only after all three final
# paths are present and hash-bound together.
rm -f "$BUILD_ROOT/desktop-artifact-receipt.json"

mkdir -p "$BUILD_ROOT/macos"
mv -f "$stage_root/macos/URMAN.zip" "$BUILD_ROOT/macos/URMAN.zip"
mv -f "$stage_root/URMAN-windows-x86_64.zip" "$BUILD_ROOT/URMAN-windows-x86_64.zip"

# build/windows is a convenience mirror for inspection. Replace it wholesale
# only after the staged archive passes, preventing stale files from surviving.
rm -rf "$BUILD_ROOT/windows"
mv "$stage_root/windows" "$BUILD_ROOT/windows"

# Generate the durable receipt from the final published paths.
URMAN_DESKTOP_BUILD_PROVENANCE="$stage_root/build-provenance.json" \
  URMAN_DESKTOP_LOCK_TOKEN=$lock_token "$URMAN_ROOT/eng/verify-desktop-artifacts.sh"
