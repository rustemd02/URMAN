#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
GUARD=${URMAN_GUARD:-$URMAN_ROOT/eng/protected_run.py}
# A smoke that throws inside async void never quits; fail it instead of hanging the run.
SMOKE_TIMEOUT=${URMAN_SMOKE_TIMEOUT:-900}
[ -f "$GUARD" ] || { echo "userdata guard not found: $GUARD" >&2; exit 1; }
GODOT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-godot-smoke.XXXXXX")
IMPORT_LOG=$(mktemp "${TMPDIR:-/tmp}/urman-godot-import.XXXXXX")
TEST_OUTPUT=$(mktemp "${TMPDIR:-/tmp}/urman-godot-test-output.XXXXXX")
"$URMAN_ROOT/.tools/dotnet/dotnet" build game/Urman.Game.csproj \
  --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1

has_runtime_errors() {
  for log in "$@"; do
    [ -f "$log" ] || continue
    if grep -Eq '^(ERROR:|SCRIPT ERROR:|WARNING: [0-9]+ ObjectDB instances were leaked|ERROR: [0-9]+ resources still in use|libc\+\+abi:)' "$log"; then
      return 0
    fi
  done
  return 1
}

# Every child receives separate clean userdata. Tests can write checkpoints and
# settings without deleting or overwriting the player's files or each other's.

# A fresh checkout has no Godot importer cache. Import all source assets before
# loading test scenes so GLB/scene failures cannot be hidden by a warm desktop.
if ! python3 "$GUARD" --clean "$GODOT" --headless --log-file "$IMPORT_LOG.godot" --path game --import >"$IMPORT_LOG" 2>&1; then
  cat "$IMPORT_LOG"
  exit 1
fi
cat "$IMPORT_LOG"
if has_runtime_errors "$IMPORT_LOG" "$IMPORT_LOG.godot"; then
  echo "Godot reported asset import errors." >&2
  cat "$IMPORT_LOG.godot" 2>/dev/null || true
  exit 1
fi

for TEST_SCENE in \
  res://tests/scene_smoke_test.tscn \
  res://tests/ambient_audio_smoke_test.tscn \
  res://tests/zone_flow_smoke_test.tscn \
  res://tests/narrative_transition_smoke_test.tscn \
  res://tests/dialogue_flow_smoke_test.tscn \
  res://tests/old_pc_flow_smoke_test.tscn \
  res://tests/journal_flow_smoke_test.tscn \
  res://tests/player_settings_smoke_test.tscn \
  res://tests/persistence_smoke_test.tscn \
  res://tests/first_person_interaction_smoke_test.tscn \
  res://tests/act1_first_person_corridor_smoke_test.tscn \
  res://tests/act1_first_person_walkthrough_smoke_test.tscn \
  res://tests/act1_spawn_matrix_smoke_test.tscn \
  res://tests/act1_save_lifecycle_smoke_test.tscn \
  res://tests/act1_main_menu_smoke_test.tscn \
  res://tests/act1_pause_menu_smoke_test.tscn \
  res://tests/act1_final_state_smoke_test.tscn \
  res://tests/act1_interruption_smoke_test.tscn \
  res://tests/act1_user_settings_smoke_test.tscn \
  res://tests/act1_settings_navigation_smoke_test.tscn \
  res://tests/act1_audio_settings_smoke_test.tscn \
  res://tests/act1_footstep_smoke_test.tscn \
  res://tests/act1_audio_transition_smoke_test.tscn \
  res://tests/act1_checkpoint_smoke_test.tscn \
  res://tests/act1_ui_readability_smoke_test.tscn \
  res://tests/act1_binding_conflict_smoke_test.tscn \
  res://tests/act1_reduced_motion_smoke_test.tscn \
  res://tests/road_relief_qa_smoke_test.tscn \
  res://tests/collision_qa_smoke_test.tscn \
  res://tests/generated_modular_kit_contract_smoke_test.tscn \
  res://tests/act1_demo_launch_smoke_test.tscn \
  res://tests/chapter_one_flow_smoke_test.tscn \
  res://tests/full_game_flow_smoke_test.tscn
do
  : > "$GODOT_LOG"
  : > "$TEST_OUTPUT"
  if ! python3 "$GUARD" --clean --timeout "$SMOKE_TIMEOUT" "$GODOT" --headless --log-file "$GODOT_LOG" --path game "$TEST_SCENE" >"$TEST_OUTPUT" 2>&1; then
    cat "$TEST_OUTPUT"
    cat "$GODOT_LOG"
    exit 1
  fi
  cat "$TEST_OUTPUT"
  if has_runtime_errors "$TEST_OUTPUT" "$GODOT_LOG"; then
    cat "$GODOT_LOG"
    echo "Godot reported runtime errors in $TEST_SCENE." >&2
    exit 1
  fi
done
