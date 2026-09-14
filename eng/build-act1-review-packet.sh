#!/bin/sh
# Copy the reviewer packet for an Act I candidate from the single repository
# sources, then prove every copy is byte-identical to its source.
#
# The packet used to be assembled by hand, and a stale copy (the packet README
# and one frame sheet) survived a repair pass because nothing checked it. This
# script makes the copy step mechanical and fails closed on any drift, so the
# repository stays the single source of truth.
#
# It never writes START_HERE_RU.md: that file is written per candidate because
# it records that candidate's own hashes, sizes and measurements.

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

fail() {
  echo "act1-review-packet: $*" >&2
  exit 1
}

[ "$#" -eq 1 ] || fail "usage: $0 <candidate-directory>"

CANDIDATE=$1
[ -d "$CANDIDATE" ] || fail "candidate directory does not exist: $CANDIDATE"
CANDIDATE=$(CDPATH= cd -- "$CANDIDATE" && pwd -P)
case "$CANDIDATE" in
  "$URMAN_ROOT"|"$URMAN_ROOT"/*) fail "candidate directory must be outside the repository: $CANDIDATE" ;;
esac

PACKET="$CANDIDATE/review_packet"
FRAMES="$PACKET/frames"
mkdir -p "$FRAMES"

# Documentation sources: one repository path per packet file name.
copy_doc() {
  source=$1
  name=$2
  [ -f "$source" ] || fail "missing packet source: $source"
  cp "$source" "$PACKET/$name"
  cmp -s "$source" "$PACKET/$name" || fail "copy is not identical: $name"
  echo "act1-review-packet: $name <- $source"
}

copy_doc docs/production/act1_review_packet_readme_RU.md README_RU.md
copy_doc docs/production/act1_language_review_sheet_2026-09-14.md act1_language_review_sheet_2026-09-14.md
copy_doc docs/production/act1_m10_handoff_package_2026-09-14.md act1_m10_handoff_package_2026-09-14.md
copy_doc docs/urman_knowledge_base/audio/act1_voice_recording_brief.md act1_voice_recording_brief.md

SHEET_ROOT=docs/production/urman_visual_review_pack
[ -d "$SHEET_ROOT" ] || fail "missing visual review pack: $SHEET_ROOT"

sheet_count=0
for sheet in "$SHEET_ROOT"/*; do
  [ -f "$sheet" ] || continue
  name=$(basename "$sheet")
  # The pack README documents the sheets; it is not one of the frame sheets.
  [ "$name" = "README.md" ] && continue
  cp "$sheet" "$FRAMES/$name"
  cmp -s "$sheet" "$FRAMES/$name" || fail "frame copy is not identical: $name"
  sheet_count=$((sheet_count + 1))
done
[ "$sheet_count" -gt 0 ] || fail "no review sheets found in $SHEET_ROOT"

# Fail if the packet directory holds anything the sources do not explain, or is
# missing anything the sources provide, so a removed or renamed sheet cannot
# linger in a delivered packet. The comparison is done with two sorted lists
# rather than a `case` inside a command substitution: the bash shipped as
# /bin/sh in this environment rejects that construct (see eng/README.md).
list_root=$(mktemp -d "${TMPDIR:-/tmp}/urman-review-packet.XXXXXX")
cleanup_packet() { rm -rf "$list_root"; }
trap cleanup_packet EXIT

find "$PACKET" -type f | sed "s|^$PACKET/||" | sort > "$list_root/actual.txt"
{
  printf '%s\n' \
    README_RU.md \
    act1_language_review_sheet_2026-09-14.md \
    act1_m10_handoff_package_2026-09-14.md \
    act1_voice_recording_brief.md
  for sheet in "$SHEET_ROOT"/*; do
    [ -f "$sheet" ] || continue
    # The pack README documents the sheets; it is not a sheet itself.
    [ "$(basename "$sheet")" = "README.md" ] && continue
    printf 'frames/%s\n' "$(basename "$sheet")"
  done
} | sort > "$list_root/expected.txt"
if ! diff -u "$list_root/expected.txt" "$list_root/actual.txt"; then
  fail "packet contents do not match the repository sources (expected vs actual above)"
fi

echo "act1-review-packet: PASS $sheet_count sheets + 4 documents, all byte-identical to repository sources"
echo "act1-review-packet: packet=$PACKET"
echo "act1-review-packet: START_HERE_RU.md is per candidate and was not touched"
