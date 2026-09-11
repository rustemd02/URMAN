#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

package_root=
cleanup_package() {
  [ -z "$package_root" ] || rm -rf "$package_root"
}
trap cleanup_package EXIT

PACKAGE_BINARY=${URMAN_ACT1_PACKAGE_BINARY:-}
PACKAGE_ZIP=${URMAN_ACT1_PACKAGE_ZIP:-}
if [ -z "$PACKAGE_BINARY" ] && [ -n "$PACKAGE_ZIP" ]; then
  [ -f "$PACKAGE_ZIP" ] || {
    echo "act1-package-performance: packaged macOS ZIP not found: $PACKAGE_ZIP" >&2
    exit 1
  }
  command -v unzip >/dev/null 2>&1 || {
    echo "act1-package-performance: 'unzip' is required to inspect the published macOS ZIP." >&2
    exit 1
  }
  package_root=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-package.XXXXXX")
  unzip -q "$PACKAGE_ZIP" -d "$package_root"
  PACKAGE_BINARY="$package_root/URMAN.app/Contents/MacOS/URMAN"
fi

if [ -z "$PACKAGE_BINARY" ] && [ -x "$URMAN_ROOT/build/macos/URMAN.app/Contents/MacOS/URMAN" ]; then
  PACKAGE_BINARY="$URMAN_ROOT/build/macos/URMAN.app/Contents/MacOS/URMAN"
fi

if [ -z "$PACKAGE_BINARY" ] && [ -f "$URMAN_ROOT/build/macos/URMAN.zip" ]; then
  command -v unzip >/dev/null 2>&1 || {
    echo "act1-package-performance: 'unzip' is required to inspect the published macOS ZIP." >&2
    exit 1
  }
  package_root=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-package.XXXXXX")
  unzip -q "$URMAN_ROOT/build/macos/URMAN.zip" -d "$package_root"
  PACKAGE_BINARY="$package_root/URMAN.app/Contents/MacOS/URMAN"
fi

if [ ! -x "$PACKAGE_BINARY" ]; then
  echo "act1-package-performance: packaged macOS binary not found or not executable: $PACKAGE_BINARY" >&2
  echo "Run ./eng/export-desktop-debug.sh first, or set URMAN_ACT1_PACKAGE_BINARY/URMAN_ACT1_PACKAGE_ZIP." >&2
  exit 1
fi

safe_mode=0
probe_mode_set=0
probe_vsync_set=0
for argument in "$@"; do
  if [ "$argument" = "--urman-safe-mode" ]; then
    safe_mode=1
  fi
  case "$argument" in
    --urman-perf-probe-mode=*) probe_mode_set=1 ;;
    --urman-perf-no-vsync|--urman-perf-vsync) probe_vsync_set=1 ;;
  esac
done

# The default package measurement is the real arrival gameplay surface. Menu
# measurements remain available explicitly and are labelled diagnostic-only by
# Act1DemoRoot. Disable VSync by default so the result reports render headroom;
# pass --urman-perf-vsync to retain the platform's current pacing.
if [ "$probe_mode_set" -eq 0 ]; then
  set -- "$@" --urman-perf-probe-mode=gameplay
fi
if [ "$probe_vsync_set" -eq 0 ]; then
  set -- "$@" --urman-perf-no-vsync
fi

probe_status=0
set +e

if [ "${URMAN_ACT1_HEADLESS:-0}" = "1" ]; then
  if [ "$safe_mode" -eq 1 ]; then
    "$PACKAGE_BINARY" --headless --rendering-method mobile --urman-perf-probe "$@"
  else
    "$PACKAGE_BINARY" --headless --urman-perf-probe "$@"
  fi
else
  # The default is a real desktop renderer: this probe is intended to answer
  # a user-facing FPS report, not only measure a headless CPU loop.
  if [ "$safe_mode" -eq 1 ]; then
    "$PACKAGE_BINARY" --rendering-method mobile --urman-perf-probe "$@"
  else
    "$PACKAGE_BINARY" --urman-perf-probe "$@"
  fi
fi
probe_status=$?
set -e

if [ "$probe_status" -eq 2 ]; then
  echo "act1-package-performance: no valid windowed gameplay FPS result (headless or menu diagnostic)" >&2
fi
exit "$probe_status"
