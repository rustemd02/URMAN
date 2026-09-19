#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
. "$URMAN_ROOT/eng/dotnet-env.sh"
cd "$URMAN_ROOT"

GODOT="$URMAN_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
TEMP_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/urman-act1-release.XXXXXX")

cleanup() {
  rm -rf "$TEMP_ROOT"
}

handle_signal() {
  status=$1
  trap - EXIT HUP INT TERM
  cleanup
  exit "$status"
}

trap cleanup EXIT
trap 'handle_signal 129' HUP
trap 'handle_signal 130' INT
trap 'handle_signal 143' TERM

fail() {
  echo "act1-release: $*" >&2
  exit 1
}

[ -x "$GODOT" ] || fail "pinned Godot executable not found: $GODOT"
command -v python3 >/dev/null 2>&1 || fail "the 'python3' command is required"

# Python owns the timeout so a hung export cannot leave a detached Godot
# process behind on the developer machine. The process group is terminated on
# timeout or cancellation; package output stays in the existing temporary tree.
run_godot() {
  timeout_seconds=$1
  log_file=$2
  shift 2
  python3 - "$GODOT" "$timeout_seconds" "$log_file" "$@" <<'PY'
import os
import signal
import subprocess
import sys
import time
from pathlib import Path

godot, timeout_text, log_path, *arguments = sys.argv[1:]
timeout_seconds = int(timeout_text)
process = None
interrupted = 0

def signal_group(signum):
    if process is not None:
        try:
            os.killpg(process.pid, signum)
        except ProcessLookupError:
            pass

def interrupt(signum, _frame):
    global interrupted
    if not interrupted:
        interrupted = signum
    signal_group(signal.SIGTERM)

def group_exists():
    try:
        os.killpg(process.pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        # A macOS group can briefly refuse the zero-signal probe while exiting.
        # This is not proof of termination: retain the bounded wait, then fail
        # cleanup unless a later probe confirms that the whole group is gone.
        return True

def await_group_exit(seconds):
    deadline = time.monotonic() + seconds
    while True:
        # Reap the direct child, but also wait for its remaining group members.
        process.poll()
        if not group_exists():
            process.wait()
            return True
        if time.monotonic() >= deadline:
            return False
        time.sleep(.025)

# The outer userdata guard signals the shell/helper group. Godot has its own
# group, so this helper must forward cancellation and finish cleanup before the
# shell can remove staging files or the guard can restore the player's data.
previous = {sig: signal.signal(sig, interrupt)
            for sig in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)}
status = 1
cleanup_failed = False
with Path(log_path).open("w", encoding="utf-8") as log:
    try:
        if not interrupted:
            process = subprocess.Popen(
                [godot, *arguments], stdout=log, stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            deadline = time.monotonic() + timeout_seconds
            while process.poll() is None and not interrupted:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    status = 124
                    log.write(f"act1-release: Godot timed out after {timeout_seconds}s\n")
                    break
                try:
                    status = process.wait(timeout=min(.1, remaining))
                except subprocess.TimeoutExpired:
                    pass
            if process.returncode is not None and status != 124:
                status = process.returncode
    finally:
        if process is not None:
            # Stay below the outer guard's five-second TERM deadline. Even a
            # successfully exited child can have descendants left in its group.
            signal_group(signal.SIGTERM)
            stopped = await_group_exit(2)
            if not stopped:
                signal_group(signal.SIGKILL)
                stopped = await_group_exit(1)
            cleanup_failed = not stopped
            log.write(f"act1-release: child_group={process.pid} stopped={str(stopped).lower()}\n")
        for sig, handler in previous.items():
            signal.signal(sig, handler)
if cleanup_failed:
    raise SystemExit(125)
raise SystemExit(128 + interrupted if interrupted else (status if status >= 0 else 128 - status))
PY
}

export_pack() {
  preset=$1
  output=$2
  log_file=$3
  if ! run_godot 120 "$log_file" \
    --headless --audio-driver Dummy --path "$URMAN_ROOT/game" \
    --export-pack "$preset" "$output"; then
    cat "$log_file"
    fail "Godot export failed for preset: $preset"
  fi
  if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log_file"; then
    cat "$log_file"
    fail "Godot reported an export error for preset: $preset"
  fi
  [ -s "$output" ] || fail "export produced an empty PCK for preset: $preset"
}

if [ "$#" -eq 0 ]; then
  MAC_PCK="$TEMP_ROOT/act1-macos.pck"
  WINDOWS_PCK="$TEMP_ROOT/act1-windows.pck"
  export_pack "Act I Release macOS" "$MAC_PCK" "$TEMP_ROOT/macos-export.log"
  export_pack "Act I Release Windows" "$WINDOWS_PCK" "$TEMP_ROOT/windows-export.log"
elif [ "$#" -eq 2 ]; then
  MAC_PCK=$1
  WINDOWS_PCK=$2
  [ -s "$MAC_PCK" ] || fail "macOS PCK is missing or empty: $MAC_PCK"
  [ -s "$WINDOWS_PCK" ] || fail "Windows PCK is missing or empty: $WINDOWS_PCK"
else
  fail "usage: $0 [<macOS-pck> <Windows-pck>]"
fi

python3 - "$URMAN_ROOT/eng" "$MAC_PCK" "$WINDOWS_PCK" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path, PurePosixPath

sys.path.insert(0, sys.argv.pop(1))
from godot_pck import read_pck


def fail(message: str) -> None:
    print(f"act1-release: {message}", file=sys.stderr)
    raise SystemExit(1)




def require(entries: dict[str, tuple[int, int]], name: str) -> None:
    if name not in entries or entries[name][1] == 0:
        fail(f"required Act I resource is missing or empty: {name}")


def require_prefix(entries: dict[str, tuple[int, int]], prefix: str, suffix: str) -> None:
    matches = sorted(name for name in entries if name.startswith(prefix) and name.endswith(suffix))
    if len(matches) != 1 or entries[matches[0]][1] == 0:
        fail(f"required derived resource is missing or ambiguous: {prefix}*{suffix}")


def payload(raw: bytes, entries: dict[str, tuple[int, int]], name: str) -> bytes:
    require(entries, name)
    offset, size = entries[name]
    return raw[offset : offset + size]


def require_imported_resource(raw: bytes, entries: dict[str, tuple[int, int]],
                              source: str, suffix: str) -> None:
    if not isinstance(source, str) or not source:
        fail(f"invalid registered resource path: {source!r}")
    path = PurePosixPath(source)
    if path.is_absolute() or ".." in path.parts or ":" in source or "\\" in source:
        fail(f"invalid registered resource path: {source}")
    try:
        remap = payload(raw, entries, source + ".import").decode("utf-8")
    except UnicodeDecodeError:
        fail(f"import remap is not UTF-8: {source}")
    targets = re.findall(r'^path(?:\.[A-Za-z0-9_]+)?="res://([^"]+)"$', remap, re.MULTILINE)
    if not targets:
        fail(f"required imported resource has no export remap: {source}")
    for target in targets:
        if not target.startswith(f".godot/imported/{path.name}-") or not target.endswith(suffix):
            fail(f"unexpected imported resource remap: {source} -> {target}")
        require(entries, target)


# Keep the scope gate aligned with the shared runtime material owner.
import re
material_source = Path("game/scripts/PainterlyMaterialLibrary.cs").read_text()
texture_pattern = r"res://assets/textures/painterly/([a-z0-9_]+)\.png"
required_source, optional_source = material_source.split("private static readonly Dictionary<string, (string Path, Vector2 Scale)> WinterTextures", 1)
production_textures = set(re.findall(texture_pattern, required_source))
# Winter entries intentionally fall back to their painted base until authored.
production_textures.update(name for name in re.findall(texture_pattern, optional_source)
                           if Path(f"game/assets/textures/painterly/{name}.png").is_file())
production_textures.update(("snow_micro_response", "snow_micro_normal"))

candidate_textures = (
    "aged_plaster_v2_albedo.png",
    "aged_plaster_v3_albedo.png",
    "damp_earth_v2_albedo.png",
    "damp_earth_v3_albedo.png",
    "damp_earth_v4_albedo.png",
    "damp_earth_v5_albedo.png",
    "damp_earth_v6_albedo.png",
    "mossy_stone_v3_albedo.png",
    "old_fabric_v3_albedo.png",
    "pine_foliage_v2_albedo.png",
    "pine_foliage_v3_albedo.png",
    "weathered_wood_boards_v2_albedo.png",
    "weathered_wood_boards_v3_albedo.png",
    "weathered_wood_boards_v4_albedo.png",
    "weathered_wood_boards_v5_albedo.png",
    "weathered_wood_boards_v6_albedo.png",
)

candidate_textures = tuple(name for name in candidate_textures if Path(name).stem not in production_textures)

ambient_stems = (
    "village_day_ambience",
    "house_room_tone",
    "kara_urman_edge_ambience",
    "water_edge_ambience",
    "fap_institutional",
    "zirat_wind",
    "village_arrival",
    "village_yard",
    "village_return",
)

foley_stems = ("door_creak", "keyboard_key", "paper_open", "ui_click")
footstep_stems = tuple(
    f"step_{surface}_{variant:02d}"
    for surface in ("snow_packed", "snow_soft", "wood", "interior_floor")
    for variant in range(3)
)

required_scene_names = (
    "act1_demo",
    "main",
    "player/first_person_player",
    "ui/audio_cue_ui",
    "ui/dialogue_ui",
    "ui/document_ui",
    "ui/journal_ui",
    "ui/old_pc_ui",
    "ui/settings_ui",
    "zones/style_benchmark_day_street",
    "zones/style_benchmark_house_pc",
    "zones/chapter1_fap_clinic",
    "zones/chapter1_zirat_road",
    "zones/style_benchmark_kara_urman_night",
)

act1_kits = (
    ("assets/models/act1", "urman_village_exterior_kit"),
    ("assets/models/act1", "urman_wet_village_road_kit"),
    ("assets/models/act1", "urman_fap_clinic_kit"),
    ("assets/models/act1", "urman_zirat_roadside_kit"),
    ("assets/models/act1", "urman_kara_forest_edge_kit"),
    ("assets/generated", "urman_act1_village_landmark_kit"),
    ("assets/generated", "urman_modular_kit"),
    ("assets/generated", "urman_character_kit"),
    # AgentBAct1ExteriorLayer resolves these two production foliage scenes
    # dynamically from FoliageMesh; their imported scenes are release deps.
    ("assets/models/act1", "urman_winter_pine"),
    ("assets/models/act1", "urman_winter_dead_tree"),
)

# AgentBAct1ExteriorLayer is the current canonical exterior presentation
# owner, despite living under the historical agent_b_act1 directory. Keep the
# allowlist exact so a future unconsumed experiment cannot enter an all-
# resources export merely because the blanket directory exclusion was removed.
agent_b_kits = (
    ("assets/models/agent_b_act1", "agentb_terrain_road_kit"),
    ("assets/models/agent_b_act1", "agentb_village_buildings_kit"),
    ("assets/models/agent_b_act1", "agentb_foliage_kit"),
    ("assets/models/agent_b_act1", "agentb_zirat_kit"),
    ("assets/models/agent_b_act1", "agentb_kara_edge_kit"),
)
agent_b_source_paths = {
    f"{root}/{kit}.glb"
    for root, kit in agent_b_kits
} | {
    f"{root}/{kit}.glb.import"
    for root, kit in agent_b_kits
}
agent_b_import_prefixes = tuple(
    f".godot/imported/{kit}.glb-" for _, kit in agent_b_kits
)

allowed_engine_indices = {
    ".godot/global_script_class_cache.cfg",
    ".godot/uid_cache.bin",
}


def assert_scope(raw: bytes, entries: dict[str, tuple[int, int]], label: str) -> None:
    for name in entries:
        lower = name.casefold()
        parts = set(lower.split("/"))
        if (
            lower.startswith("tests/")
            or lower.startswith("scripts/experiments/")
            or "full_game" in lower
            or "fullgame" in lower
            or ("agent_b" in lower and name not in agent_b_source_paths)
            or "retired_candidates" in lower
            or lower.startswith(".tools/")
            or ".git/" in lower
            or any(part in {"sdk", "nuget", "downloads", "secrets", "credentials"} for part in parts)
            or lower.startswith(".env")
            or lower.endswith((".pem", ".p12", ".pfx", ".key"))
            or lower.rsplit("/", 1)[-1].startswith(("id_rsa", "secret", "credential"))
        ):
            fail(f"{label} contains forbidden release path: {name}")
        if lower.startswith(".godot/") and name not in allowed_engine_indices:
            cache_markers = ("/editor/", "/mono/", "/shader_cache/", "/cache/", "/caches/")
            if any(marker in lower for marker in cache_markers) or lower.rsplit("/", 1)[-1].endswith("cache.bin"):
                fail(f"{label} contains a developer cache: {name}")
        if any(candidate in lower for candidate in candidate_textures):
            fail(f"{label} contains a retired/candidate texture: {name}")
        if lower.startswith("assets/models/agent_b_act1/") and name not in agent_b_source_paths:
            fail(f"{label} contains an unconsumed Agent B asset: {name}")
        if lower.startswith(".godot/imported/agentb_") and not any(
            name.startswith(prefix) and name.endswith(".scn")
            for prefix in agent_b_import_prefixes
        ):
            fail(f"{label} contains an unconsumed Agent B import: {name}")

    for scene in required_scene_names:
        require(entries, f"scenes/{scene}.tscn.remap")
        require_prefix(entries, ".godot/exported/", f"-{scene.rsplit('/', 1)[-1]}.scn")

    require(entries, "project.binary")
    require(entries, "content/credits.ru.txt")
    content_name = "content/urman.chapter1.compiled.v1.json"
    require(entries, content_name)
    content_offset, content_size = entries[content_name]
    content_payload = raw[content_offset : content_offset + content_size]
    if b'"schemaVersion": 1' not in content_payload or b'"id": "urman.chapter1"' not in content_payload:
        fail(f"{label} content does not contain the chapter-one campaign marker")

    # These owners load JSON dynamically; all_resources alone is not evidence
    # that the native package retained their complete registered dependencies.
    vehicle_data = {}
    for name in ("content/vehicles/act1_vehicles.v1.json", "content/vehicles/avyl_radio.v1.json"):
        packed = payload(raw, entries, name)
        if packed != Path("game", name).read_bytes():
            fail(f"{label} contains stale vehicle/radio data: {name}")
        vehicle_data[name] = json.loads(packed)
    programme = vehicle_data["content/vehicles/avyl_radio.v1.json"]["segments"]
    radio_paths = set()
    for segment in programme:
        resource = segment.get("streamPath")
        if (not isinstance(resource, str) or not resource.startswith("res://assets/audio/act1/radio/")
                or not resource.endswith(".wav")):
            fail(f"radio segment has no delivered WAV resource: {resource!r}")
        radio_paths.add(resource)
    for resource in sorted(radio_paths):
        require_imported_resource(raw, entries, resource.removeprefix("res://"), ".sample")

    compiled = json.loads(content_payload)
    packed_assets = {asset["id"]: asset for asset in compiled["registries"]["assets"]}
    expected_assets = {
        asset["id"]: asset for asset in
        json.loads(Path("game", content_name).read_bytes())["registries"]["assets"]
    }
    public_photo_ids = ("urman.chapter1:asset/school-class-photo",
                        "urman.chapter1:asset/council-photo-album")
    for asset_id in public_photo_ids:
        asset = packed_assets.get(asset_id)
        expected = expected_assets.get(asset_id)
        if asset is None or expected is None or any(
            asset.get(key) != expected.get(key) for key in ("kind", "file", "mediaType", "sha256", "variants")
        ):
            fail(f"{label} public photograph is missing or stale in the registry: {asset_id}")
        if asset["kind"] != "image" or asset["mediaType"] != "image/png":
            fail(f"public photograph must remain a registered PNG image: {asset_id}")
        for image in (asset, *asset.get("variants", [])):
            require_imported_resource(raw, entries, "assets/" + image["file"], ".ctex")
    print(f"act1-release: {label} registered dependencies PASS vehicle-json=2 "
          f"radio-rows={len(programme)} unique-wav={len(radio_paths)} public-photos={len(public_photo_ids)}; "
          "presence only, not listening/art acceptance")

    for root, kit in act1_kits:
        require(entries, f"{root}/{kit}.glb.import")
        require_prefix(entries, f".godot/imported/{kit}.glb-", ".scn")

    for root, kit in agent_b_kits:
        # Godot exports the imported scene and the source .glb.import identity;
        # the raw GLB bytes are not a separate PCK payload, matching the
        # established Act I kit contract above.
        require(entries, f"{root}/{kit}.glb.import")
        require_prefix(entries, f".godot/imported/{kit}.glb-", ".scn")

    require(entries, "assets/audio/ambient_manifest.json")
    for stem in ambient_stems:
        require(entries, f"assets/audio/{stem}.wav.import")
        require_prefix(entries, f".godot/imported/{stem}.wav-", ".sample")
    for stem in foley_stems:
        require(entries, f"assets/audio/act1/foley/{stem}.wav.import")
        require_prefix(entries, f".godot/imported/{stem}.wav-", ".sample")
    for stem in footstep_stems:
        require(entries, f"assets/audio/act1/footsteps/{stem}.wav.import")
        require_prefix(entries, f".godot/imported/{stem}.wav-", ".sample")

    for texture in production_textures:
        require(entries, f"assets/textures/painterly/{texture}.png.import")
        require_prefix(entries, f".godot/imported/{texture}.png-", ".ctex")

    require(entries, "assets/textures/ui/act1_menu_winter_v1.png.import")
    require_prefix(entries, ".godot/imported/act1_menu_winter_v1.png-", ".ctex")
    # Native startup artwork loads PNG bytes before texture imports are ready.
    for image_name in ("act1_menu_winter_v1", "urman_app_icon_v1"):
        image_path = f"assets/textures/ui/{image_name}.png"
        require(entries, image_path)
        image_offset, image_size = entries[image_path]
        if image_size < 8 or raw[image_offset : image_offset + 8] != b"\x89PNG\r\n\x1a\n":
            fail(f"{label} native startup image is not a PNG payload: {image_path}")

    # Pine's embedded Leaf_Pine_C mask is extracted by Godot and consumed by
    # AgentBAct1ExteriorLayer when it replaces the imported needle material.
    require(entries, "assets/models/act1/urman_winter_pine_Leaf_Pine_C.png.import")
    require_prefix(
        entries,
        ".godot/imported/urman_winter_pine_Leaf_Pine_C.png-",
        ".ctex",
    )

    print(f"act1-release: {label} PCK scope PASS entries={len(entries)}")


for pck_path in map(Path, sys.argv[1:]):
    try:
        raw, pck_entries = read_pck(pck_path)
    except (OSError, ValueError) as error:
        fail(str(error))
    assert_scope(raw, pck_entries, pck_path.name)
PY

run_headless() {
  label=$1
  pck=$2
  log_file=$3
  if ! run_godot 30 "$log_file" \
    --headless --display-driver headless --audio-driver Dummy \
    --path "$URMAN_ROOT/game" --main-pack "$pck" --quit-after 2; then
    cat "$log_file"
    fail "headless package bootstrap failed: $label"
  fi
  if grep -Eq '^(ERROR:|SCRIPT ERROR:)' "$log_file"; then
    cat "$log_file"
    fail "headless package bootstrap reported an error: $label"
  fi
  grep -Fq "zone-loaded: village_day@arrival" "$log_file" \
    || fail "headless package bootstrap missed Act I launch marker: $label"
  echo "act1-release: $label headless/Dummy bootstrap PASS"
}

run_headless macOS "$MAC_PCK" "$TEMP_ROOT/macos-launch.log"
run_headless Windows "$WINDOWS_PCK" "$TEMP_ROOT/windows-launch.log"

echo "act1-release: PASS — reproducible Act I PCK scope gate only; RC/release readiness remains OPEN"
