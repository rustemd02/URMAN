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
# timeout; no package output is ever written inside the repository.
run_godot() {
  timeout_seconds=$1
  log_file=$2
  shift 2
  python3 - "$GODOT" "$timeout_seconds" "$log_file" "$@" <<'PY'
import os
import signal
import subprocess
import sys
from pathlib import Path

godot, timeout_text, log_path, *arguments = sys.argv[1:]
timeout_seconds = int(timeout_text)
with Path(log_path).open("w", encoding="utf-8") as log:
    process = subprocess.Popen(
        [godot, *arguments],
        stdout=log,
        stderr=subprocess.STDOUT,
        start_new_session=True,
    )
    try:
        status = process.wait(timeout=timeout_seconds)
    except subprocess.TimeoutExpired:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait()
        log.write(f"act1-release: Godot timed out after {timeout_seconds}s\n")
        raise SystemExit(124)
raise SystemExit(status)
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

python3 - "$MAC_PCK" "$WINDOWS_PCK" <<'PY'
from __future__ import annotations

import struct
import sys
import unicodedata
from pathlib import Path


def fail(message: str) -> None:
    print(f"act1-release: {message}", file=sys.stderr)
    raise SystemExit(1)


def read_pck(path: Path) -> tuple[bytes, dict[str, tuple[int, int]]]:
    raw = path.read_bytes()
    if len(raw) < 0x70 or raw[:4] != b"GDPC":
        fail(f"{path.name} is not a Godot PCK")
    if struct.unpack_from("<I", raw, 4)[0] != 4:
        fail(f"{path.name} is not a Godot 4 PCK")

    data_base, index_base = struct.unpack_from("<QQ", raw, 0x18)
    if not 0x70 <= data_base < index_base < len(raw):
        fail(f"{path.name} has invalid data/index bounds")

    count = struct.unpack_from("<I", raw, index_base)[0]
    if not 0 < count <= 10000:
        fail(f"{path.name} has an invalid file count: {count}")

    entries: dict[str, tuple[int, int]] = {}
    folded: dict[str, str] = {}
    cursor = index_base + 4
    for entry_number in range(count):
        if cursor + 4 > len(raw):
            fail(f"{path.name} index ends before entry {entry_number}")
        field_length = struct.unpack_from("<I", raw, cursor)[0]
        cursor += 4
        if field_length == 0 or field_length > 4096 or cursor + field_length > len(raw):
            fail(f"{path.name} has an invalid path length at entry {entry_number}")
        field = raw[cursor : cursor + field_length]
        cursor += field_length
        trimmed = field.rstrip(b"\0")
        if not trimmed or b"\0" in trimmed:
            fail(f"{path.name} has an invalid padded path at entry {entry_number}")
        try:
            name = trimmed.decode("utf-8")
        except UnicodeDecodeError as error:
            fail(f"{path.name} has a non-UTF-8 path at entry {entry_number}: {error}")

        if cursor + 8 + 8 + 16 + 4 > len(raw):
            fail(f"{path.name} index ends inside entry {entry_number}")
        data_offset, data_size = struct.unpack_from("<QQ", raw, cursor)
        cursor += 16 + 16 + 4
        # Godot PCK v4 stores payload offsets relative to the file-data base.
        # Offset zero is the first payload, not a missing-resource sentinel.
        data_offset += data_base
        if data_offset > len(raw) or data_size > len(raw) - data_offset:
            fail(f"{path.name} has an out-of-bounds payload: {name}")
        if data_offset < data_base or data_offset + data_size > index_base:
            fail(f"{path.name} has a payload overlapping its index: {name}")

        if "\\" in name or name.startswith("/") or any(part in {"", ".", ".."} for part in name.split("/")):
            fail(f"{path.name} has an unsafe resource path: {name}")
        normalized = unicodedata.normalize("NFKC", name).casefold()
        previous = folded.get(normalized)
        if previous is not None and previous != name:
            fail(f"{path.name} has a case-folded path collision: {previous} / {name}")
        folded[normalized] = name
        if name in entries:
            fail(f"{path.name} contains a duplicate resource path: {name}")
        entries[name] = (data_offset, data_size)

    # Native Windows exports append at most seven zero alignment bytes before
    # their size/magic footer; extracted PCKs retain that alignment.
    trailing = raw[cursor:]
    if len(trailing) > 7 or any(trailing):
        fail(f"{path.name} has non-alignment bytes after its resource index")
    return raw, entries


def require(entries: dict[str, tuple[int, int]], name: str) -> None:
    if name not in entries or entries[name][1] == 0:
        fail(f"required Act I resource is missing or empty: {name}")


def require_prefix(entries: dict[str, tuple[int, int]], prefix: str, suffix: str) -> None:
    matches = sorted(name for name in entries if name.startswith(prefix) and name.endswith(suffix))
    if len(matches) != 1 or entries[matches[0]][1] == 0:
        fail(f"required derived resource is missing or ambiguous: {prefix}*{suffix}")


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
    raw, pck_entries = read_pck(pck_path)
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
