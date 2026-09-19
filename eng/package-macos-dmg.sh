#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

fail() {
  echo "macos-dmg: $*" >&2
  exit 1
}

[ "$#" -eq 2 ] || fail "usage: $0 <macos-zip-from-release-export> <output.dmg>"
MAC_ZIP=$1
OUTPUT_DMG=$2

[ -s "$MAC_ZIP" ] || fail "missing or empty macOS package: $MAC_ZIP"
command -v hdiutil >/dev/null 2>&1 || fail "the 'hdiutil' command is required"
command -v codesign >/dev/null 2>&1 || fail "the 'codesign' command is required"
command -v file >/dev/null 2>&1 || fail "the 'file' command is required"
command -v unzip >/dev/null 2>&1 || fail "the 'unzip' command is required"

MAC_ZIP=$(CDPATH= cd -- "$(dirname -- "$MAC_ZIP")" && pwd -P)/$(basename -- "$MAC_ZIP")
OUTPUT_DIR=$(CDPATH= cd -- "$(dirname -- "$OUTPUT_DMG")" && pwd -P) \
  || fail "output directory does not exist: $(dirname -- "$OUTPUT_DMG")"
OUTPUT_DMG="$OUTPUT_DIR/$(basename -- "$OUTPUT_DMG")"
case "$OUTPUT_DMG" in
  *.dmg) ;;
  *) fail "output path must end in .dmg: $OUTPUT_DMG" ;;
esac
[ ! -e "$OUTPUT_DMG" ] || fail "refusing to overwrite an existing image: $OUTPUT_DMG"

VOLUME_NAME="URMAN"
[ ! -e "/Volumes/$VOLUME_NAME" ] || fail "a volume named '$VOLUME_NAME' is already mounted; eject it first"

# The DMG is a delivery artifact, so it is built from the verified export ZIP and
# never from a source tree: run export-desktop-release.sh first.
WORK_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/urman-macos-dmg.XXXXXX")
ATTACHED=0
cleanup() {
  if [ "$ATTACHED" -eq 1 ]; then
    hdiutil detach "/Volumes/$VOLUME_NAME" >/dev/null 2>&1 || true
  fi
  rm -rf "$WORK_ROOT"
}
trap cleanup EXIT HUP INT TERM

APP_ROOT="$WORK_ROOT/package"
mkdir -p "$APP_ROOT"
unzip -q "$MAC_ZIP" -d "$APP_ROOT"
APP="$APP_ROOT/URMAN.app"
APP_BIN="$APP/Contents/MacOS/URMAN"
[ -s "$APP_BIN" ] || fail "missing exported app binary: $APP_BIN"

app_info=$(file "$APP_BIN")
echo "$app_info"
echo "$app_info" | grep -Eq 'Mach-O universal' || fail "macOS app is not a universal Mach-O"

run_app() {
  # Runs the bundle's own binary the way verify-macos-host.sh does; the log file
  # is written outside the app so a read-only mount stays read-only.
  binary_dir=$1
  log_file=$2
  (
    cd "$binary_dir"
    ./URMAN --headless --audio-driver Dummy --quit-after 4 --log-file "$log_file"
  )
}

check_run() {
  log_file=$1
  grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log_file" && fail "Godot reported an error during the startup probe"
  grep -Eq 'ObjectDB instances leaked|resources still in use' "$log_file" && fail "Godot reported a resource leak during the startup probe"
  grep -Fq 'zone-loaded: village_day@arrival' "$log_file" || fail "startup zone marker is missing from the startup probe"
  grep -Fq 'УРМАН Godot vertical slice ready: Painterly Low-Poly / first person' "$log_file" \
    || fail "first-person bootstrap marker is missing from the startup probe"
}

probe_log="$WORK_ROOT/startup-probe.log"
if ! run_app "$(dirname -- "$APP_BIN")" "$probe_log" >"$probe_log.stdout" 2>&1; then
  cat "$probe_log.stdout"
  [ -f "$probe_log" ] && cat "$probe_log"
  fail "exported app did not exit cleanly"
fi
check_run "$probe_log"

# Godot exports the app around a prebuilt template, so the bundle keeps the
# template's Developer ID signature, which no longer matches the payload Godot
# appended. That stale signature makes macOS report a damaged app and cannot be
# repaired without the owner's certificate, so the bundle is re-signed ad-hoc.
# Hardened runtime is deliberately not requested: the .NET runtime JITs and would
# need entitlements that an ad-hoc signature cannot legitimately carry.
sign_macho_files() {
  find "$1" -type f | while IFS= read -r candidate; do
    file "$candidate" | grep -q "Mach-O" || continue
    printf '%s\n' "$candidate"
  done
}
sign_macho_files "$APP" >"$WORK_ROOT/macho-files.txt"
[ -s "$WORK_ROOT/macho-files.txt" ] || fail "no Mach-O binaries found inside the bundle"
macho_count=$(wc -l <"$WORK_ROOT/macho-files.txt" | tr -d '[:space:]')
while IFS= read -r candidate; do
  codesign --remove-signature "$candidate" >/dev/null 2>&1 || true
done <"$WORK_ROOT/macho-files.txt"
while IFS= read -r candidate; do
  codesign --force --sign - "$candidate" >/dev/null 2>&1 \
    || fail "ad-hoc signing failed for $candidate"
done <"$WORK_ROOT/macho-files.txt"
codesign --force --sign - --identifier "game.urman" "$APP" >/dev/null 2>&1 \
  || fail "ad-hoc signing failed for the app bundle"
codesign --verify --deep --strict "$APP" >/dev/null 2>&1 \
  || fail "the ad-hoc signed bundle does not verify"
echo "macos-dmg: ad-hoc signed $macho_count Mach-O binaries and the app bundle"

DMG_STAGE="$WORK_ROOT/dmg"
mkdir -p "$DMG_STAGE"
mv "$APP" "$DMG_STAGE/URMAN.app"
ln -s /Applications "$DMG_STAGE/Applications"

commit=$(git -C "$URMAN_ROOT" rev-parse --short=12 HEAD 2>/dev/null || echo unknown)
cat >"$DMG_STAGE/START_HERE_RU.txt" <<TXT
УРМАН — Акт I: Возвращение
Вертикальный срез, сборка от $(date '+%Y-%m-%d'), коммит $commit
macOS universal (Apple Silicon и Intel)

Как запустить
1. Перетащите URMAN в папку «Программы» (Applications).
2. Первый запуск — не двойным щелчком: нажмите на URMAN правой кнопкой
   и выберите «Открыть», затем в диалоге ещё раз «Открыть».
   Это нужно один раз. Сборка не подписана сертификатом разработчика Apple
   и не нотаризована — это отдельный, ещё не пройденный этап.
   Если macOS сообщает, что приложение повреждено, выполните в «Терминале»:
     xattr -dr com.apple.quarantine /Applications/URMAN.app
   и запустите снова.
3. Дальше игра открывается обычным двойным щелчком.

Управление
WASD — идти · мышь — смотреть
E — начать или осмотреть · J — журнал · Esc — меню

Сообщения об ошибках присылайте вместе с этим файлом: в нём указан коммит сборки.
TXT

hdiutil create -quiet -volname "$VOLUME_NAME" -srcfolder "$DMG_STAGE" \
  -fs HFS+ -format UDZO "$OUTPUT_DMG" || fail "hdiutil could not create the image"

hdiutil attach -quiet -nobrowse -readonly "$OUTPUT_DMG" || fail "the created image could not be mounted"
ATTACHED=1
MOUNTED=/Volumes/$VOLUME_NAME
MOUNTED_APP="$MOUNTED/URMAN.app"
MOUNTED_BIN="$MOUNTED_APP/Contents/MacOS/URMAN"
[ -s "$MOUNTED_BIN" ] || fail "the mounted image does not contain the app binary"
[ -L "$MOUNTED/Applications" ] || fail "the mounted image lost its /Applications link"
[ -s "$MOUNTED/START_HERE_RU.txt" ] || fail "the mounted image lost START_HERE_RU.txt"
codesign --verify --deep --strict "$MOUNTED_APP" >/dev/null 2>&1 \
  || fail "the app inside the mounted image does not verify"

mounted_log="$WORK_ROOT/mounted-probe.log"
if ! run_app "$(dirname -- "$MOUNTED_BIN")" "$mounted_log" >"$mounted_log.stdout" 2>&1; then
  cat "$mounted_log.stdout"
  [ -f "$mounted_log" ] && cat "$mounted_log"
  fail "the app launched from the mounted image did not exit cleanly"
fi
check_run "$mounted_log"

hdiutil detach "$MOUNTED" >/dev/null || fail "the image could not be detached"
ATTACHED=0

image_bytes=$(wc -c <"$OUTPUT_DMG" | tr -d '[:space:]')
image_sha=$(shasum -a 256 "$OUTPUT_DMG" | awk '{print $1}')
printf '%s\n' \
  "macos-dmg: PASS — the image mounts, verifies and launches on this host" \
  "macos-dmg: image=$OUTPUT_DMG bytes=$image_bytes" \
  "macos-dmg: sha256=$image_sha" \
  "macos-dmg: ad-hoc signed only; Apple Developer ID signing and notarization remain OPEN" \
  "macos-dmg: recipients must use right-click Open on first launch (see START_HERE_RU.txt in the image)"
