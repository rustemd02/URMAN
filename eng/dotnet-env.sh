#!/bin/sh

set -eu

# Sourced by the eng scripts (where $0 is the calling script) and by people in
# an interactive shell (where $0 is the shell itself, so the path next to it
# means nothing). Try the $0-relative root first, then walk up from the current
# directory; the pinned toolchain is the marker either way.
resolve_urman_root() {
  candidate=$(CDPATH= cd -- "$(dirname -- "$0")/.." 2>/dev/null && pwd) || candidate=
  if [ -n "$candidate" ] && [ -d "$candidate/.tools/dotnet" ]; then
    printf '%s\n' "$candidate"
    return 0
  fi
  candidate=$PWD
  while [ "$candidate" != "/" ]; do
    if [ -d "$candidate/.tools/dotnet" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
    candidate=$(dirname -- "$candidate")
  done
  # No pinned toolchain anywhere above: keep the historical answer so a clear
  # "missing binary" error still points at the expected location.
  printf '%s\n' "$(CDPATH= cd -- "$(dirname -- "$0")/.." 2>/dev/null && pwd || printf '%s' "$PWD")"
}

URMAN_ROOT=$(resolve_urman_root)
export DOTNET_ROOT="$URMAN_ROOT/.tools/dotnet"
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"
export DOTNET_CLI_HOME="$URMAN_ROOT/.tools/dotnet-home"
export NUGET_PACKAGES="$URMAN_ROOT/.tools/nuget"
export PATH="$DOTNET_ROOT:/usr/bin:/bin:/usr/sbin:/sbin"
export MSBUILDUSESERVER=0
