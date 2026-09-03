#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"

[ -x "$GODOT" ] || {
  echo "act1-demo-safe: missing Godot .NET binary: $GODOT" >&2
  exit 1
}

cd "$URMAN_ROOT"
. "$URMAN_ROOT/eng/dotnet-env.sh"
exec "$GODOT" --path game --rendering-method mobile --urman-safe-mode "$@"
