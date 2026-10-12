#!/bin/bash
# Run the probe against TREE, NEVER against a stale copy of the assemblies.
#
# A HintPath reference does not reliably re-copy a rebuilt DLL into this probe's bin: a stage was
# once "verified" against a Urman.Studio.Core.dll that was 90 minutes old. So this script rebuilds
# the tree, copies the three DLLs into the probe's output itself, and only then runs the probe.
set -euo pipefail
TREE="${1:?usage: runprobe.sh <tree>}"
DOTNET_ROOT=/Users/unterlantas/Documents/GitHub/URMAN-deepseek/.tools/dotnet
export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"
OUT=/tmp/dsprobe/bin/Debug/net10.0
mkdir -p "$OUT"
for P in Urman.Studio.Core Urman.Core Urman.Content; do
  dotnet build "$TREE/src-dotnet/$P/$P.csproj" -v q --nologo >/dev/null
  cp "$TREE/src-dotnet/$P/bin/Debug/net10.0/$P.dll" "$OUT/"
done
cd /tmp/dsprobe
dotnet run --project /tmp/dsprobe/dsprobe.csproj -p:ProbeRoot="$TREE" 2>&1
