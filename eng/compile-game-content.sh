#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

.tools/dotnet/dotnet build tools-dotnet/Urman.ContentCli/Urman.ContentCli.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1
tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli compile \
  --root "$URMAN_ROOT" \
  --campaign urman.chapter1 \
  --out game/content/urman.chapter1.compiled.v1.json
tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli compile \
  --root "$URMAN_ROOT" \
  --campaign urman.fullgame \
  --out game/content/urman.fullgame.compiled.v1.json
