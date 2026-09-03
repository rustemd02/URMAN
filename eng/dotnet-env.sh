#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
export DOTNET_ROOT="$URMAN_ROOT/.tools/dotnet"
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"
export DOTNET_CLI_HOME="$URMAN_ROOT/.tools/dotnet-home"
export NUGET_PACKAGES="$URMAN_ROOT/.tools/nuget"
export PATH="$DOTNET_ROOT:/usr/bin:/bin:/usr/sbin:/sbin"
export MSBUILDUSESERVER=0
