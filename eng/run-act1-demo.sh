#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"

[ -x "$GODOT" ] || {
  echo "act1-demo: missing Godot .NET binary: $GODOT" >&2
  exit 1
}

cd "$URMAN_ROOT"
. "$URMAN_ROOT/eng/dotnet-env.sh"

# Regenerate derived content and let MSBuild perform its incremental freshness
# check. A failed refresh must never fall through to yesterday's runtime DLL.
echo "act1-demo: refreshing campaign packs and checking the C# build"
if ! sh "$URMAN_ROOT/eng/compile-game-content.sh"; then
  echo "act1-demo: content refresh failed; the game was not started" >&2
  exit 1
fi
if ! "$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1; then
  echo "act1-demo: C# build failed; the game was not started" >&2
  exit 1
fi
# Newly generated preview voice clips must be imported before the game can load them.
echo "act1-demo: importing new assets"
"$GODOT" --headless --path game --import >/dev/null 2>&1 ||
  echo "act1-demo: asset import reported a problem; new voice clips may stay silent" >&2
exec "$GODOT" --path game "$@"
