#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
# Keep the same freshness contract and argument forwarding as ordinary launch.
exec "$URMAN_ROOT/eng/run-act1-demo.sh" --rendering-method mobile --urman-safe-mode "$@"
