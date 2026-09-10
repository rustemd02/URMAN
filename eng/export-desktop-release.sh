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

MAC_PCK="$OUTPUT_DIR/URMAN-Act1-macOS.pck"
WINDOWS_PCK="$OUTPUT_DIR/URMAN-Act1-Windows.pck"
if [ -e "$MAC_PCK" ] || [ -L "$MAC_PCK" ] || [ -e "$WINDOWS_PCK" ] || [ -L "$WINDOWS_PCK" ]; then
  fail "output directory already contains an Act I release artifact"
fi

LOG_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-release-export.XXXXXX")
keep_output=0

cleanup() {
  if [ "$keep_output" -ne 1 ]; then
    rm -f "$MAC_PCK" "$WINDOWS_PCK"
  fi
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

# Keep a timed process group around each export. This prevents a failed export
# from leaving Godot attached to the developer session.
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

export_pack() {
  preset=$1
  output=$2
  log_file=$3
  if ! run_godot 120 "$log_file" \
    --headless --audio-driver Dummy --path "$URMAN_ROOT/game" \
    --export-pack "$preset" "$output"; then
    cat "$log_file"
    fail "Godot export failed for preset: $preset"
  fi
  if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log_file"; then
    cat "$log_file"
    fail "Godot reported an export error for preset: $preset"
  fi
  [ -s "$output" ] || fail "export produced an empty PCK for preset: $preset"
  echo "act1-release-export: $(basename "$output") produced"
}

export_pack "Act I Release macOS" "$MAC_PCK" "$LOG_ROOT/macos-export.log"
export_pack "Act I Release Windows" "$WINDOWS_PCK" "$LOG_ROOT/windows-export.log"

# The verifier owns the package allowlist and the headless/Dummy bootstrap.
"$URMAN_ROOT/eng/verify-act1-release-package.sh" "$MAC_PCK" "$WINDOWS_PCK"

mac_bytes=$(wc -c < "$MAC_PCK" | tr -d '[:space:]')
windows_bytes=$(wc -c < "$WINDOWS_PCK" | tr -d '[:space:]')
echo "act1-release-export: macOS=$MAC_PCK bytes=$mac_bytes"
echo "act1-release-export: Windows=$WINDOWS_PCK bytes=$windows_bytes"
echo "act1-release-export: PASS — unsigned Act I PCK candidates only; RC/release readiness remains OPEN"
keep_output=1
