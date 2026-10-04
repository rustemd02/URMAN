#!/bin/sh
# Run Godot smoke scenes with the userdata guard around each one.
#
# Headless smokes are not read-only: the checkpoint, save-lifecycle and
# walkthrough tests write real SaveGameV3 files into the user's Godot userdata,
# which is how a local checkpoint got overwritten twice during Act I work. The
# guard keeps the originals aside, runs a clean test session and restores the
# originals byte-for-byte. The guard lives in the repository with this runner.

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
# Pinned Godot .NET binary: macOS app bundle by default, plain binary elsewhere;
# URMAN_GODOT overrides both (e.g. a system-wide install on Linux CI).
case "$(uname -s)" in
  Darwin)
    GODOT_DEFAULT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
    DISPLAY_ARGS="--display-driver macos"
    ;;
  *)
    GODOT_DEFAULT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
    [ -x "$GODOT_DEFAULT" ] || GODOT_DEFAULT="$URMAN_ROOT/.tools/godot/Godot"
    DISPLAY_ARGS=""
    ;;
esac
GODOT=${URMAN_GODOT:-$GODOT_DEFAULT}
GUARD=${URMAN_GUARD:-$URMAN_ROOT/eng/protected_run.py}
# A smoke that throws inside async void never quits; fail it instead of hanging the run.
SMOKE_TIMEOUT=${URMAN_SMOKE_TIMEOUT:-900}
# Physical door animations use actual window focus. Their native checks need
# a real window; the default remains the existing headless test invocation.
HEADLESS_ARGS="--headless"
if [ "${URMAN_SMOKE_NATIVE:-0}" = "1" ]; then
  HEADLESS_ARGS=""
else
  # An explicit macOS display driver overrides --headless in Godot. Keep
  # native window checks opt-in and let --headless select its own driver.
  DISPLAY_ARGS=""
fi
BACKGROUND_INPUT_ARGS=""
if [ "${URMAN_SMOKE_BACKGROUND_INPUT:-0}" = "1" ]; then
  BACKGROUND_INPUT_ARGS="--urman-smoke-background-input"
fi
shared_session=false
if [ "${1:-}" = "--shared-session" ]; then
  shared_session=true
  shift
fi

if [ "$#" -lt 1 ]; then
  echo "usage: $0 [--shared-session] <smoke-scene> [smoke-scene ...]" >&2
  echo "       a scene may be a bare name (act1_audio_settings_smoke_test) or a res:// path" >&2
  exit 2
fi

[ -x "$GODOT" ] || {
  echo "run-smoke-guarded: missing pinned Godot .NET binary: $GODOT" >&2
  exit 1
}

cd "$URMAN_ROOT"
. "$URMAN_ROOT/eng/dotnet-env.sh"

[ -f "$GUARD" ] || {
  echo "run-smoke-guarded: no userdata guard at $GUARD" >&2
  exit 1
}

status=0
if [ "$shared_session" = true ]; then
  # Cold-load checks need distinct processes to share only the guarded saves.
  python3 "$GUARD" --clean --timeout "$SMOKE_TIMEOUT" /bin/sh -eu -c '
    godot=$1
    display_args=$2
    headless_args=$3
    background_args=$4
    shift 4
    for scene do
      case "$scene" in
        res://*) path=$scene ;;
        *) path="res://tests/$scene.tscn" ;;
      esac
      echo "run-smoke-guarded shared session: $path"
      "$godot" $headless_args $display_args --path game "$path" $background_args
    done
  ' shared-smoke "$GODOT" "$DISPLAY_ARGS" "$HEADLESS_ARGS" "$BACKGROUND_INPUT_ARGS" "$@"
  exit $?
fi
for scene in "$@"; do
  case "$scene" in
    res://*) path=$scene ;;
    *) path="res://tests/$scene.tscn" ;;
  esac
  echo "run-smoke-guarded: $path"
  python3 "$GUARD" --clean --timeout "$SMOKE_TIMEOUT" "$GODOT" $HEADLESS_ARGS $DISPLAY_ARGS --path game "$path" $BACKGROUND_INPUT_ARGS || status=$?
done

exit "$status"
