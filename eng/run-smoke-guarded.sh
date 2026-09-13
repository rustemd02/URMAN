#!/bin/sh
# Run Godot smoke scenes with the userdata guard around each one.
#
# Headless smokes are not read-only: the checkpoint, save-lifecycle and
# walkthrough tests write real SaveGameV3 files into the user's Godot userdata,
# which is how a local checkpoint got overwritten twice during Act I work. The
# guard copies saves and settings aside, runs one child, then restores them
# byte-for-byte. Set URMAN_GUARD to use a different guard; the default is the
# evidence-side guard the M10 session kit already uses.

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
GUARD=${URMAN_GUARD:-/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/protected_run.py}

if [ "$#" -lt 1 ]; then
  echo "usage: $0 <smoke-scene> [smoke-scene ...]" >&2
  echo "       a scene may be a bare name (act1_audio_settings_smoke_test) or a res:// path" >&2
  exit 2
fi

[ -x "$GODOT" ] || {
  echo "run-smoke-guarded: missing pinned Godot .NET binary: $GODOT" >&2
  exit 1
}

cd "$URMAN_ROOT"
. "$URMAN_ROOT/eng/dotnet-env.sh"

guard_available=0
if [ -f "$GUARD" ]; then
  guard_available=1
else
  echo "run-smoke-guarded: no userdata guard at $GUARD" >&2
  echo "run-smoke-guarded: running directly; this may overwrite local saves and settings" >&2
fi

status=0
for scene in "$@"; do
  case "$scene" in
    res://*) path=$scene ;;
    *) path="res://tests/$scene.tscn" ;;
  esac
  echo "run-smoke-guarded: $path"
  if [ "$guard_available" -eq 1 ]; then
    python3 "$GUARD" "$GODOT" --headless --display-driver macos --path game "$path" || status=$?
  else
    "$GODOT" --headless --display-driver macos --path game "$path" || status=$?
  fi
done

if [ "$guard_available" -eq 1 ]; then
  echo "run-smoke-guarded: saves and settings restored from the guard backup"
fi
exit "$status"
