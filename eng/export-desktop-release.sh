#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

fail() {
  echo "act1-release-export: $*" >&2
  exit 1
}

[ "$#" -eq 1 ] || fail "usage: $0 <empty-output-directory>"
command -v python3 >/dev/null 2>&1 || fail "the 'python3' command is required"
command -v unzip >/dev/null 2>&1 || fail "the 'unzip' command is required"
command -v zip >/dev/null 2>&1 || fail "the 'zip' command is required"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
[ -x "$GODOT" ] || fail "pinned Godot executable not found: $GODOT"

OUTPUT_DIR=$1
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR=$(CDPATH= cd -- "$OUTPUT_DIR" && pwd -P)
case "$OUTPUT_DIR" in
  "$URMAN_ROOT"|"$URMAN_ROOT"/*)
    fail "output directory must be outside the repository: $OUTPUT_DIR"
    ;;
esac
[ "$OUTPUT_DIR" != "/" ] || fail "refusing to publish into the filesystem root"

if [ -n "$(find "$OUTPUT_DIR" -mindepth 1 -maxdepth 1 -print -quit)" ]; then
  fail "output directory must be empty: $OUTPUT_DIR"
fi

MAC_ZIP="$OUTPUT_DIR/macos/URMAN.zip"
WINDOWS_EXE="$OUTPUT_DIR/windows/URMAN.exe"
WINDOWS_ZIP="$OUTPUT_DIR/URMAN-windows-x86_64.zip"
RECEIPT="$OUTPUT_DIR/desktop-artifact-receipt.json"

LOG_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-release-export.XXXXXX")
stage_root=
cleanup_output=0

cleanup() {
  if [ "$cleanup_output" -eq 1 ]; then
    rm -rf "$OUTPUT_DIR/macos" "$OUTPUT_DIR/windows"
    rm -f "$OUTPUT_DIR/URMAN-windows-x86_64.zip" "$OUTPUT_DIR/desktop-artifact-receipt.json"
  fi
  [ -z "$stage_root" ] || rm -rf "$stage_root"
  rm -rf "$LOG_ROOT"
}

handle_signal() {
  status=$1
  trap - EXIT HUP INT TERM
  cleanup
  exit "$status"
}

trap cleanup EXIT
trap 'handle_signal 129' HUP
trap 'handle_signal 130' INT
trap 'handle_signal 143' TERM

# Keep a timed process group around every export. This prevents a failed
# release export from leaving a detached Godot process behind.
run_godot() {
  timeout_seconds=$1
  log_file=$2
  shift 2
  python3 - "$GODOT" "$timeout_seconds" "$log_file" "$@" <<'PY'
import os
import signal
import subprocess
import sys
from pathlib import Path

godot, timeout_text, log_path, *arguments = sys.argv[1:]
timeout_seconds = int(timeout_text)
with Path(log_path).open("w", encoding="utf-8") as log:
    process = subprocess.Popen(
        [godot, *arguments],
        stdout=log,
        stderr=subprocess.STDOUT,
        start_new_session=True,
    )
    try:
        status = process.wait(timeout=timeout_seconds)
    except subprocess.TimeoutExpired:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait()
        log.write(f"act1-release-export: Godot timed out after {timeout_seconds}s\n")
        raise SystemExit(124)
raise SystemExit(status)
PY
}

run_export() {
  preset=$1
  destination=$2
  log_slug=$3
  export_log="$LOG_ROOT/$log_slug.stdout.log"

  if ! run_godot 180 "$export_log" \
    --headless --audio-driver Dummy \
    --path "$URMAN_ROOT/game" --export-release "$preset" "$destination"; then
    cat "$export_log"
    fail "Godot release export failed for preset: $preset"
  fi

  cat "$export_log"
  if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$export_log"; then
    fail "Godot reported an export error for preset: $preset"
  fi
}

stage_root=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-release-stage.XXXXXX")
mkdir -p "$stage_root/macos" "$stage_root/windows"

run_export "Act I Release macOS" "$stage_root/macos/URMAN.zip" macos
run_export "Act I Release Windows" "$stage_root/windows/URMAN.exe" windows

[ -s "$stage_root/macos/URMAN.zip" ] || fail "macOS release export did not produce URMAN.zip"
[ -s "$stage_root/windows/URMAN.exe" ] || fail "Windows release export did not produce URMAN.exe"

# The archive contains exactly the fresh Windows staging tree. -X removes
# host-specific metadata before the existing structural verifier inspects it.
(
  cd "$stage_root"
  zip -q -r -X "$stage_root/URMAN-windows-x86_64.zip" windows
)

# The caller supplied an empty directory, so these moves publish one complete
# native package set without retaining files from a previous run.
cleanup_output=1
mv "$stage_root/macos" "$OUTPUT_DIR/macos"
mv "$stage_root/windows" "$OUTPUT_DIR/windows"
mv "$stage_root/URMAN-windows-x86_64.zip" "$OUTPUT_DIR/URMAN-windows-x86_64.zip"

# Reuse the existing PCK scope verifier against the actual native package
# payloads. macOS carries an external PCK; Windows embeds the same PCK at the
# end of its executable, so extract only the GDPC payload before invoking the
# verifier. Any scope failure rolls the just-published set back via cleanup().
NATIVE_PCK_ROOT="$LOG_ROOT/native-pck"
mkdir -p "$NATIVE_PCK_ROOT/macos" "$NATIVE_PCK_ROOT/windows"
unzip -q "$MAC_ZIP" -d "$NATIVE_PCK_ROOT/macos"
MAC_PCK="$NATIVE_PCK_ROOT/macos/URMAN.app/Contents/Resources/URMAN.pck"
[ -s "$MAC_PCK" ] || fail "macOS app is missing its external URMAN.pck"
WINDOWS_PCK="$NATIVE_PCK_ROOT/windows/URMAN.pck"
python3 - "$WINDOWS_EXE" "$WINDOWS_PCK" <<'PY'
from pathlib import Path
import struct
import sys

source, destination = map(Path, sys.argv[1:])
payload = source.read_bytes()
if len(payload) < 12 or payload[-4:] != b"GDPC":
    raise SystemExit(f"act1-release-export: Windows executable has no embedded PCK footer: {source}")
size = struct.unpack_from("<Q", payload, len(payload) - 12)[0]
offset = len(payload) - 12 - size
if offset < 0 or payload[offset:offset + 4] != b"GDPC":
    raise SystemExit("act1-release-export: invalid embedded PCK bounds")
destination.write_bytes(payload[offset:-12])
PY
[ -s "$WINDOWS_PCK" ] || fail "Windows executable did not yield an embedded URMAN.pck"
"$URMAN_ROOT/eng/verify-act1-release-package.sh" "$MAC_PCK" "$WINDOWS_PCK"

# Rebind the native receipt to the caller's final paths after the PCK scope
# verifier and publication both succeed.
"$URMAN_ROOT/eng/verify-desktop-artifacts.sh" \
  "$MAC_ZIP" "$WINDOWS_EXE" "$WINDOWS_ZIP" "$RECEIPT"

cleanup_output=0
mac_bytes=$(wc -c < "$MAC_ZIP" | tr -d '[:space:]')
windows_bytes=$(wc -c < "$WINDOWS_EXE" | tr -d '[:space:]')
echo "act1-release-export: macOS=$MAC_ZIP bytes=$mac_bytes"
echo "act1-release-export: Windows=$WINDOWS_EXE bytes=$windows_bytes"
echo "act1-release-export: Windows ZIP=$WINDOWS_ZIP"
echo "act1-release-export: receipt=$RECEIPT"
echo "act1-release-export: PASS — unsigned native Act I candidates; host/signing/performance acceptance remains OPEN"
