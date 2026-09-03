#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

.tools/dotnet/dotnet build Urman.slnx \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1
tests-dotnet/Urman.Core.Tests/bin/Debug/net10.0/Urman.Core.Tests
tests-dotnet/Urman.Content.Tests/bin/Debug/net10.0/Urman.Content.Tests
tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli validate --root "$URMAN_ROOT"
tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli report --root "$URMAN_ROOT"
tools-dotnet/Urman.ContentCli/bin/Debug/net10.0/Urman.ContentCli simulate --root "$URMAN_ROOT" --campaign urman.chapter1
