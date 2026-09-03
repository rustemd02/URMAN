#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

MAC_ZIP="$URMAN_ROOT/build/macos/URMAN.zip"
fail() {
  echo "macos-host: $*" >&2
  exit 1
}

[ -s "$MAC_ZIP" ] || fail "missing or empty: $MAC_ZIP"
command -v file >/dev/null 2>&1 || fail "the 'file' command is required"
command -v unzip >/dev/null 2>&1 || fail "the 'unzip' command is required"

probe_root=$(mktemp -d "${TMPDIR:-/tmp}/urman-macos-host.XXXXXX")
trap 'rm -rf "$probe_root"' EXIT HUP INT TERM

unzip -q "$MAC_ZIP" -d "$probe_root"
app="$probe_root/URMAN.app/Contents/MacOS/URMAN"
[ -s "$app" ] || fail "missing exported app binary: $app"

app_info=$(file "$app")
echo "$app_info"
echo "$app_info" | grep -Eq 'Mach-O universal' || fail "macOS app is not a universal Mach-O"

run_log="$probe_root/host-run.log"
godot_log="$probe_root/host-run.godot.log"
app_dir=$(dirname "$app")
if ! (
  cd "$app_dir"
  ./URMAN --headless --audio-driver Dummy --quit-after 4 --log-file "$godot_log"
) >"$run_log" 2>&1; then
  cat "$run_log"
  [ -f "$godot_log" ] && cat "$godot_log"
  fail "exported app did not exit cleanly"
fi

for log in "$run_log" "$godot_log"; do
  [ -f "$log" ] || continue
  cat "$log"
  grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log" && fail "Godot reported an error in $(basename "$log")"
  grep -Eq 'ObjectDB instances leaked|resources still in use' "$log" && fail "Godot reported a resource leak in $(basename "$log")"
done

grep -Fq 'zone-loaded: village_day@arrival' "$run_log" || fail "startup zone marker is missing"
grep -Fq 'УРМАН Godot vertical slice ready: Painterly Low-Poly / first person' "$run_log" || fail "first-person bootstrap marker is missing"

printf '%s\n' \
  "macos-host: PASS" \
  "macos-host: exported universal app launched with embedded PCK and .NET payload" \
  "macos-host: startup zone and first-person bootstrap markers observed" \
  "macos-host: Forward+ headless run used Dummy audio driver; Windows host remains unverified"
