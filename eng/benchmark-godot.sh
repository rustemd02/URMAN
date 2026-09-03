#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
.tools/dotnet/dotnet build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-performance-import.XXXXXX")
BENCHMARK_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-performance-benchmark.XXXXXX")

if ! "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
cat "$IMPORT_LOG"
if grep -q '^ERROR:' "$IMPORT_LOG"; then
  echo "Godot reported asset import errors." >&2
  exit 1
fi

if ! "$GODOT" --log-file "$BENCHMARK_LOG.godot" --path game res://tests/performance_benchmark.tscn >"$BENCHMARK_LOG" 2>&1; then
  cat "$BENCHMARK_LOG"
  exit 1
fi
cat "$BENCHMARK_LOG"
if grep -q '^ERROR:' "$BENCHMARK_LOG"; then
  echo "Godot reported performance benchmark errors." >&2
  exit 1
fi

cat docs/urman_knowledge_base/performance/godot_m4pro_baseline.tsv
