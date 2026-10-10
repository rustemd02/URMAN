#!/bin/sh
# PW-033 / VIS-108: a committed kit GLB must be exactly what its Blender generator
# produces from the committed .blend. Runs the generator in a throwaway copy (the
# generator rewrites its .blend and GLB in place) and compares SHA-256 with the
# committed GLB. Usage: eng/verify-kit-reproducible.sh [blender-binary]
# Covers every act1 kit that has both a generator and a committed .blend.
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BLENDER=${1:-${BLENDER:-blender}}
WORK=$(mktemp -d "${TMPDIR:-/tmp}/urman-kit-repro.XXXXXX")
trap 'rm -rf "$WORK"' EXIT INT TERM
status=0
check() { # generator .blend glb
    mkdir -p "$WORK/$3/$(dirname "$2")" "$WORK/$3/$(dirname "$4")"
    cp "$ROOT/$2" "$WORK/$3/$2"
    "$BLENDER" --background --factory-startup --python "$ROOT/$1" -- --root "$WORK/$3" > "$WORK/$3.log" 2>&1 \
        || { echo "FAIL $1: generator exited non-zero (log: $WORK/$3.log)"; status=1; return; }
    want=$(shasum -a 256 "$ROOT/$4" | cut -d' ' -f1)
    got=$(shasum -a 256 "$WORK/$3/$4" | cut -d' ' -f1)
    if [ "$want" = "$got" ]; then echo "PASS $4 $got"; else echo "FAIL $4 committed=$want generated=$got"; status=1; fi
}
check assets/source/blender/act1/urman_village_exterior_kit.py \
      assets/source/blender/act1/urman_village_exterior_kit.blend village \
      game/assets/models/act1/urman_village_exterior_kit.glb
check assets/source/blender/act1/urman_fap_clinic_kit.py \
      assets/source/blender/act1/urman_fap_clinic_kit.blend fap \
      game/assets/models/act1/urman_fap_clinic_kit.glb
check assets/source/blender/act1/urman_wet_village_road_kit.py \
      assets/source/blender/act1/urman_wet_village_road_kit.blend road \
      game/assets/models/act1/urman_wet_village_road_kit.glb
check tools/blender/export_kara_forest_edge_kit.py \
      assets/source/blender/act1/urman_kara_forest_edge_kit.blend kara \
      game/assets/models/act1/urman_kara_forest_edge_kit.glb
check tools/blender/export_zirat_roadside_kit.py \
      assets/source/blender/act1/urman_zirat_roadside_kit.blend zirat \
      game/assets/models/act1/urman_zirat_roadside_kit.glb
"$BLENDER" --version | head -1
exit $status
