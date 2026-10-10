#!/bin/sh
# Open URMAN Studio on this checkout.
#
# Studio shows the real game world and starts test runs of the real game, so it
# always runs inside the userdata guard: for the whole session the player's
# Godot saves and settings are kept aside and restored byte-for-byte on exit,
# and Studio's world preview and every "Play from here" write to a disposable
# folder. A missing guard stops Studio instead of touching real saves.

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
GUARD=${URMAN_GUARD:-$URMAN_ROOT/eng/protected_run.py}

[ -x "$GODOT" ] || { echo "run-studio: missing pinned Godot .NET binary: $GODOT" >&2; exit 1; }
[ -f "$GUARD" ] || { echo "run-studio: no userdata guard at $GUARD" >&2; exit 1; }

cd "$URMAN_ROOT"
. "$URMAN_ROOT/eng/dotnet-env.sh"
.tools/dotnet/dotnet build game/Urman.Game.csproj --disable-build-servers -v quiet -nologo -nodeReuse:false >/dev/null

# Refresh imported resources before the embedded world consumes newly pulled assets.
python3 "$GUARD" --clean --timeout 300 "$GODOT" --headless --path game --editor --import >/dev/null

exec python3 "$GUARD" --clean "$GODOT" --path game res://studio/studio_main.tscn -- "--urman-studio-root=$URMAN_ROOT" "$@"
