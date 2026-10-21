#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
TEMP_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-release.XXXXXX")

cleanup() {
  rm -rf "$TEMP_ROOT"
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

fail() {
  echo "act1-release: $*" >&2
  exit 1
}

[ -x "$GODOT" ] || fail "pinned Godot executable not found: $GODOT"
command -v python3 >/dev/null 2>&1 || fail "the 'python3' command is required"

# Python owns the timeout so a hung export cannot leave a detached Godot
# process behind on the developer machine. The process group is terminated on
# timeout or cancellation; package output stays in the existing temporary tree.
run_godot() {
  timeout_seconds=$1
  log_file=$2
  shift 2
  python3 - "$GODOT" "$timeout_seconds" "$log_file" "$@" <<'PY'
import os
import signal
import subprocess
import sys
import time
from pathlib import Path

godot, timeout_text, log_path, *arguments = sys.argv[1:]
timeout_seconds = int(timeout_text)
process = None
interrupted = 0

def signal_group(signum):
    if process is not None:
        try:
            os.killpg(process.pid, signum)
        except ProcessLookupError:
            pass

def interrupt(signum, _frame):
    global interrupted
    if not interrupted:
        interrupted = signum
    signal_group(signal.SIGTERM)

def group_exists():
    try:
        os.killpg(process.pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        # A macOS group can briefly refuse the zero-signal probe while exiting.
        # This is not proof of termination: retain the bounded wait, then fail
        # cleanup unless a later probe confirms that the whole group is gone.
        return True

def await_group_exit(seconds):
    deadline = time.monotonic() + seconds
    while True:
        # Reap the direct child, but also wait for its remaining group members.
        process.poll()
        if not group_exists():
            process.wait()
            return True
        if time.monotonic() >= deadline:
            return False
        time.sleep(.025)

# The outer userdata guard signals the shell/helper group. Godot has its own
# group, so this helper must forward cancellation and finish cleanup before the
# shell can remove staging files or the guard can restore the player's data.
previous = {sig: signal.signal(sig, interrupt)
            for sig in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)}
status = 1
cleanup_failed = False
with Path(log_path).open("w", encoding="utf-8") as log:
    try:
        if not interrupted:
            process = subprocess.Popen(
                [godot, *arguments], stdout=log, stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            deadline = time.monotonic() + timeout_seconds
            while process.poll() is None and not interrupted:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    status = 124
                    log.write(f"act1-release: Godot timed out after {timeout_seconds}s\n")
                    break
                try:
                    status = process.wait(timeout=min(.1, remaining))
                except subprocess.TimeoutExpired:
                    pass
            if process.returncode is not None and status != 124:
                status = process.returncode
    finally:
        if process is not None:
            # Stay below the outer guard's five-second TERM deadline. Even a
            # successfully exited child can have descendants left in its group.
            signal_group(signal.SIGTERM)
            stopped = await_group_exit(2)
            if not stopped:
                signal_group(signal.SIGKILL)
                stopped = await_group_exit(1)
            cleanup_failed = not stopped
            log.write(f"act1-release: child_group={process.pid} stopped={str(stopped).lower()}\n")
        for sig, handler in previous.items():
            signal.signal(sig, handler)
if cleanup_failed:
    raise SystemExit(125)
raise SystemExit(128 + interrupted if interrupted else (status if status >= 0 else 128 - status))
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
}

if [ "$#" -eq 0 ]; then
  MAC_PCK="$TEMP_ROOT/act1-macos.pck"
  WINDOWS_PCK="$TEMP_ROOT/act1-windows.pck"
  export_pack "Act I Release macOS" "$MAC_PCK" "$TEMP_ROOT/macos-export.log"
  export_pack "Act I Release Windows" "$WINDOWS_PCK" "$TEMP_ROOT/windows-export.log"
elif [ "$#" -eq 2 ]; then
  MAC_PCK=$1
  WINDOWS_PCK=$2
  [ -s "$MAC_PCK" ] || fail "macOS PCK is missing or empty: $MAC_PCK"
  [ -s "$WINDOWS_PCK" ] || fail "Windows PCK is missing or empty: $WINDOWS_PCK"
else
  fail "usage: $0 [<macOS-pck> <Windows-pck>]"
fi

python3 "$URMAN_ROOT/eng/desktop_release_checks.py" scope "$MAC_PCK" "$WINDOWS_PCK"

run_headless() {
  label=$1
  pck=$2
  log_file=$3
  if ! run_godot 30 "$log_file" \
    --headless --display-driver headless --audio-driver Dummy \
    --path "$URMAN_ROOT/game" --main-pack "$pck" --quit-after 2; then
    cat "$log_file"
    fail "headless package bootstrap failed: $label"
  fi
  if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log_file"; then
    cat "$log_file"
    fail "headless package bootstrap reported an error: $label"
  fi
  grep -Fq "fullgame-scene-loaded: urman.fullgame:scene/pw-prologue physical=W01" "$log_file" \
    || fail "headless package bootstrap missed the PhotoWorlds prologue launch marker: $label"
  echo "act1-release: $label headless/Dummy bootstrap PASS"
}

run_headless macOS "$MAC_PCK" "$TEMP_ROOT/macos-launch.log"
run_headless Windows "$WINDOWS_PCK" "$TEMP_ROOT/windows-launch.log"

echo "act1-release: PASS — reproducible PhotoWorlds launch PCK scope gate only; RC/release readiness remains OPEN"
