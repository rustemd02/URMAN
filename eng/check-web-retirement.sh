#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

MODE=${1:---report}
case "$MODE" in
  --report|--assert-absent)
    ;;
  *)
    echo "usage: $0 [--report|--assert-absent]" >&2
    exit 2
    ;;
esac

findings=0

finding() {
  findings=$((findings + 1))
  printf 'web-retirement: %s\n' "$1"
}

for path in \
  "package.json" \
  "package-lock.json" \
  "pnpm-lock.yaml" \
  "yarn.lock" \
  "vite.config.ts" \
  "vite.config.js" \
  "index.html" \
  "dist" \
  "src"
do
  if [ -e "$path" ]; then
    finding "legacy production path exists: $path"
  fi
done

scan_source_file() {
  file=$1
  if grep -Eq "(localStorage|sessionStorage|indexedDB|navigator\\.storage)" "$file"; then
    finding "browser persistence reference: $file"
  fi
  if grep -Eq "(from[[:space:]]+[\\\"'](three|vite)|require\\([\\\"'](three|vite)|WebGLRenderer|vite[[:space:]]+(build|preview))" "$file"; then
    finding "web runtime/toolchain reference: $file"
  fi
}

if [ -d src ]; then
  source_files=$(find src -type f \( -name '*.ts' -o -name '*.tsx' -o -name '*.js' -o -name '*.mjs' -o -name '*.html' \) -print)
  for file in $source_files; do
    scan_source_file "$file"
  done
fi

if [ -f package.json ]; then
  if grep -Eq '"(dev|build|preview|test:[^"]*)"[[:space:]]*:[[:space:]]*"[^"]*(vite|npm|node)' package.json; then
    finding "Node/Vite command remains in package.json"
  fi
  if grep -Eq '"(three|@types/three|vite)"[[:space:]]*:' package.json; then
    finding "Three.js/Vite dependency remains in package.json"
  fi
fi

if [ "$MODE" = "--report" ]; then
  if [ "$findings" -eq 0 ]; then
    echo "web-retirement: READY (no legacy production findings)"
  else
    echo "web-retirement: NOT_READY ($findings findings; report mode does not fail)"
  fi
  exit 0
fi

if [ "$findings" -ne 0 ]; then
  echo "web-retirement: ASSERT_ABSENT failed ($findings findings)." >&2
  echo "Complete desktop/cultural/accessibility/full-playthrough acceptance before deleting the legacy owner." >&2
  exit 1
fi

echo "web-retirement: ASSERT_ABSENT passed"
