#!/usr/bin/env python3
"""Shared desktop archive, runtime closure and Act I package scope checks."""

from __future__ import annotations

from collections import Counter
from datetime import datetime, timezone
import argparse
import configparser
import hashlib
import json
import stat
import struct
import unicodedata
import zipfile
import sys
from pathlib import Path, PurePosixPath

from godot_pck import read_pck


def fail(message: str) -> None:
    raise ValueError(message)




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
                              source: str, suffix: str, expected_type: str | None = None) -> None:
    if not isinstance(source, str) or not source:
        fail(f"invalid registered resource path: {source!r}")
    path = PurePosixPath(source)
    if path.is_absolute() or ".." in path.parts or ":" in source or "\\" in source:
        fail(f"invalid registered resource path: {source}")
    if expected_type is not None:
        require(entries, source + ".import")
        if entries[source + ".import"][1] > 64 * 1024:
            fail(f"import remap metadata exceeds its size limit: {source}")
    import_payload = payload(raw, entries, source + ".import")
    try:
        remap = import_payload.decode("utf-8")
    except UnicodeDecodeError:
        fail(f"import remap is not UTF-8: {source}")
    if expected_type is not None:
        if remap.endswith("\0"):
            remap = remap[:-1]
        if "\0" in remap:
            fail(f"imported scene remap contains an interior NUL: {source}")
        config = configparser.ConfigParser(interpolation=None)
        config.optionxform = str
        try:
            config.read_string(remap)
        except configparser.Error as error:
            fail(f"invalid imported scene remap: {source}: {error}")
        if (config.defaults() or not config.has_section("remap")
                or config.get("remap", "importer", fallback=None) != '"scene"'
                or config.get("remap", "type", fallback=None) != f'"{expected_type}"'):
            fail(f"import remap must identify a scene of type {expected_type}: {source}")
        remap = "\n".join(f"{key}={value}" for key, value in config.items("remap", raw=True))
    targets = re.findall(r'^path(?:\.[A-Za-z0-9_]+)?="res://([^"]+)"$', remap, re.MULTILINE)
    if not targets:
        fail(f"required imported resource has no export remap: {source}")
    for target in targets:
        if not target.startswith(f".godot/imported/{path.name}-") or not target.endswith(suffix):
            fail(f"unexpected imported resource remap: {source} -> {target}")
        require(entries, target)


# Keep the scope gate aligned with the shared runtime material owner.
import re
material_source = Path("game/scripts/PainterlyMaterialLibrary.cs").read_text(encoding="utf-8")
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
    "photoworlds_game",
    "full_game",
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
            or lower.startswith("studio/")
            or lower.startswith("scripts/experiments/")
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

    fullgame_name = "content/urman.fullgame.compiled.v1.json"
    require(entries, fullgame_name)
    fullgame_offset, fullgame_size = entries[fullgame_name]
    fullgame_payload = raw[fullgame_offset : fullgame_offset + fullgame_size]
    if (b'"schemaVersion": 1' not in fullgame_payload
            or b'"id": "urman.fullgame"' not in fullgame_payload
            or b'"entrypoint": "urman.fullgame:scene/pw-prologue"' not in fullgame_payload):
        fail(f"{label} content does not contain the current PhotoWorlds campaign entrypoint")

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

    # Runtime resolves the authored house through Godot's imported PackedScene;
    # the source GLB itself is intentionally absent from normal native exports.
    recipe_name = "content/studio/hero_house.recipe.json"
    require(entries, recipe_name)
    recipe_source = Path("game", recipe_name)
    recipe_info = recipe_source.lstat()
    if (not stat.S_ISREG(recipe_info.st_mode) or stat.S_ISLNK(recipe_info.st_mode)
            or getattr(recipe_info, "st_file_attributes", 0) & 0x0400
            or not 0 < recipe_info.st_size <= 1024 * 1024
            or entries[recipe_name][1] > 1024 * 1024):
        fail(f"{label} house recipe must be bounded regular metadata")
    recipe_payload = payload(raw, entries, recipe_name)
    if recipe_payload != recipe_source.read_bytes():
        fail(f"{label} contains a stale authored house recipe")
    recipe = json.loads(recipe_payload)
    if not isinstance(recipe, dict) or recipe.get("schema") != "house-recipe/v1":
        fail(f"{label} contains an unsupported authored house recipe")
    model_path = recipe.get("modelPath")
    if (not isinstance(model_path, str) or len(model_path) > 1024
            or not model_path.startswith("res://assets/models/studio/") or not model_path.endswith(".glb")):
        fail(f"{label} house recipe has an invalid modelPath")
    model_source = model_path.removeprefix("res://")
    if model_source != PurePosixPath(model_source).as_posix():
        fail(f"{label} house recipe has a noncanonical modelPath")
    require_imported_resource(raw, entries, model_source, ".scn", expected_type="PackedScene")

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



def verify_archive(path: str, reject_macos_metadata: bool) -> None:
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist()
        names = [info.filename for info in infos]
        duplicates = sorted(name for name, count in Counter(names).items() if count > 1)
        if duplicates:
            fail(f"duplicate ZIP entries in {path}: {duplicates[:5]}")

        windows_normalized_names = {}
        windows_reserved_stems = {
            "aux",
            "con",
            "nul",
            "prn",
            *(f"com{number}" for number in range(1, 10)),
            *(f"lpt{number}" for number in range(1, 10)),
        }

        for info in infos:
            name = info.filename
            normalized_security_name = unicodedata.normalize("NFKC", name)
            if normalized_security_name.endswith("/"):
                if not info.is_dir():
                    fail(f"ZIP entry has an invalid directory marker in {path}: {name!r}")
                normalized_security_name = normalized_security_name[:-1]
            if (
                not normalized_security_name
                or "\\" in normalized_security_name
                or normalized_security_name.startswith("/")
            ):
                fail(f"unsafe ZIP entry in {path}: {name!r}")
            parts = normalized_security_name.split("/")
            if any(part in {"", ".", ".."} for part in parts):
                fail(f"unsafe normalized ZIP entry in {path}: {name!r}")
            if stat.S_ISLNK(info.external_attr >> 16):
                fail(f"symbolic-link ZIP entry is not allowed in {path}: {name!r}")

            if reject_macos_metadata:
                normalized_parts = []
                for part in parts:
                    normalized_part = part
                    if normalized_part != normalized_part.rstrip(" ."):
                        fail(f"Windows ZIP entry has a trailing dot/space alias: {name!r}")
                    if any(character in normalized_part for character in '<>:"|?*'):
                        fail(f"Windows ZIP entry has an invalid Windows path character: {name!r}")
                    if any(ord(character) < 32 for character in normalized_part):
                        fail(f"Windows ZIP entry has a control character: {name!r}")
                    folded_part = normalized_part.casefold()
                    reserved_stem = folded_part.split(".", 1)[0]
                    if reserved_stem in windows_reserved_stems:
                        fail(f"Windows ZIP entry uses a reserved device alias: {name!r}")
                    normalized_parts.append(folded_part)

                if (
                    normalized_parts[0] == "__macosx"
                    or any(part.startswith("._") for part in normalized_parts)
                    or any(part == ".ds_store" for part in normalized_parts)
                ):
                    fail(f"macOS metadata is not allowed in Windows ZIP: {name!r}")

                normalized_name = "/".join(normalized_parts)
                previous_name = windows_normalized_names.get(normalized_name)
                if previous_name is not None and previous_name != name:
                    fail(
                        "case-folded Windows ZIP path collision: "
                        f"{previous_name!r} and {name!r}"
                    )
                windows_normalized_names[normalized_name] = name



def only_file(root: Path, suffix: str) -> Path:
    matches = sorted(root.glob(f"*{suffix}"))
    if len(matches) != 1:
        fail(f"expected exactly one *{suffix} in {root}, found {len(matches)}")
    return matches[0]


def verify_dotnet(root, expected_target, platform):
    root = Path(root)
    deps_path = only_file(root, ".deps.json")
    runtimeconfig_path = only_file(root, ".runtimeconfig.json")
    deps = json.loads(deps_path.read_text(encoding="utf-8"))
    runtimeconfig = json.loads(runtimeconfig_path.read_text(encoding="utf-8"))

    actual_target = deps.get("runtimeTarget", {}).get("name")
    if actual_target != expected_target:
        fail(f"{root.name} runtime target is {actual_target!r}, expected {expected_target!r}")
    target_entries = deps.get("targets", {}).get(actual_target)
    if not isinstance(target_entries, dict) or not target_entries:
        fail(f"{deps_path} has no dependency target entries for {actual_target}")

    runtime_options = runtimeconfig.get("runtimeOptions", {})
    if runtime_options.get("tfm") != "net10.0":
        fail(f"{runtimeconfig_path} must target net10.0")
    frameworks = runtime_options.get("includedFrameworks", [])
    if not any(
        item.get("name") == "Microsoft.NETCore.App"
        and str(item.get("version", "")).startswith("10.0.")
        for item in frameworks
    ):
        fail(f"{runtimeconfig_path} has no included Microsoft.NETCore.App 10.0 runtime")

    referenced = set()
    for package in target_entries.values():
        for section in ("runtime", "native"):
            assets = package.get(section, {})
            if not isinstance(assets, dict):
                fail(f"invalid {section} dependency section in {deps_path}")
            for relative_path in assets:
                basename = PurePosixPath(relative_path).name
                referenced.add(basename)
                candidate = root / basename
                if not candidate.is_file() or candidate.stat().st_size == 0:
                    fail(f"missing dependency asset for {root.name}: {relative_path} -> {candidate}")

    common_required = {
        "GodotSharp.dll",
        "System.Private.CoreLib.dll",
        "Urman.Content.dll",
        "Urman.Core.dll",
        "Urman.Game.dll",
    }
    native_required = (
        {"hostfxr.dll", "hostpolicy.dll", "coreclr.dll", "clrjit.dll"}
        if platform == "windows"
        else {"libhostfxr.dylib", "libhostpolicy.dylib", "libcoreclr.dylib", "libclrjit.dylib"}
    )
    for filename in sorted(common_required | native_required):
        candidate = root / filename
        if not candidate.is_file() or candidate.stat().st_size == 0:
            fail(f"required self-contained runtime file is missing: {candidate}")

    if not common_required.issubset(referenced):
        missing = sorted(common_required - referenced)
        fail(f"{deps_path} does not reference required managed assets: {missing}")

    print(f"desktop-artifacts: .NET closure PASS {root.name} ({actual_target}, {len(referenced)} deps assets)")


def windows_tree(root):
    from act1_candidate_identity import sha256_file
    root_info = root.lstat()
    if (not stat.S_ISDIR(root_info.st_mode) or stat.S_ISLNK(root_info.st_mode)
            or getattr(root_info, "st_file_attributes", 0) & 0x0400):
        fail("Windows payload root must be a real directory")
    result = {}
    for path in sorted(root.rglob("*")):
        info = path.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x0400:
            fail(f"Windows payload contains a symlink or reparse point: {path}")
        if not stat.S_ISDIR(info.st_mode):
            if not stat.S_ISREG(info.st_mode):
                fail(f"Windows payload contains a non-regular file: {path}")
            result[path.relative_to(root).as_posix()] = (info.st_size, sha256_file(path))
    if not result:
        fail("Windows payload is empty")
    return result


def windows_payload_manifest(entries):
    canonical = "".join(f"{name}\t{size}\t{digest}\n"
                        for name, (size, digest) in sorted(entries.items())).encode("utf-8")
    return {"fileCount": len(entries), "manifestSha256": hashlib.sha256(canonical).hexdigest()}


def verify_windows_package(output):
    from remote_common import MAX_RESULT_BYTES, MAX_RESULT_FILES
    output = Path(output).resolve()
    windows = output / "windows"
    entries = windows_tree(windows)
    exe = windows / "URMAN.exe"
    with exe.open("rb") as source:
        dos = source.read(64)
        if len(dos) != 64 or dos[:2] != b"MZ":
            fail("Windows executable has no DOS header")
        pe_offset = struct.unpack_from("<I", dos, 60)[0]
        if pe_offset < 64 or pe_offset + 94 > exe.stat().st_size:
            fail("Windows executable has invalid PE bounds")
        source.seek(pe_offset)
        pe = source.read(94)
        if (pe[:4] != b"PE\0\0" or struct.unpack_from("<H", pe, 4)[0] != 0x8664
                or struct.unpack_from("<H", pe, 20)[0] < 70
                or struct.unpack_from("<H", pe, 24)[0] != 0x20b
                or struct.unpack_from("<H", pe, 24 + 68)[0] != 2):
            fail("Windows executable is not PE32+ GUI x86-64")
    archive_path = output / "URMAN-windows-x86_64.zip"
    verify_archive(str(archive_path), reject_macos_metadata=True)
    with zipfile.ZipFile(archive_path) as archive:
        infos = archive.infolist()
        if (len(infos) > MAX_RESULT_FILES
                or sum(info.file_size for info in infos) > MAX_RESULT_BYTES):
            fail("Windows ZIP expanded payload exceeds the station result bounds")
        if archive.testzip() is not None:
            fail("Windows ZIP failed CRC/integrity check")
        archived = {}
        for info in infos:
            if info.is_dir():
                if not info.filename.startswith("windows/"):
                    fail("Windows ZIP contains a directory outside windows/")
                continue
            if not info.filename.startswith("windows/"):
                fail("Windows ZIP contains a file outside windows/")
            digest = hashlib.sha256()
            with archive.open(info) as source:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    digest.update(chunk)
            archived[info.filename[len("windows/"):]] = (info.file_size, digest.hexdigest())
        if entries != archived:
            fail("Windows ZIP differs from the complete exported staging tree")
    verify_dotnet(windows / "data_Urman.Game_windows_x86_64",
                  ".NETCoreApp,Version=v10.0/win-x64", "windows")
    return windows_payload_manifest(entries)


def prepare_windows(output, pck):
    output = Path(output).resolve()
    # The runner supplies a fresh job-owned output directory and never publishes
    # a PASS receipt before bootstrap and the complete source/artifact checks.
    entries = windows_tree(output / "windows")
    with zipfile.ZipFile(output / "URMAN-windows-x86_64.zip", "x", zipfile.ZIP_DEFLATED) as archive:
        for name in entries:
            archive.write(output / "windows" / name, "windows/" + name)
    verify_windows_package(output)
    exe = output / "windows/URMAN.exe"
    with exe.open("rb") as source:
        length = exe.stat().st_size
        if length < 12:
            fail("Windows executable is too short for an embedded PCK")
        source.seek(-12, 2)
        size, magic = struct.unpack("<Q4s", source.read(12))
        offset = length - 12 - size
        if magic != b"GDPC" or offset < 0 or not size:
            fail("Windows executable has no valid embedded PCK footer")
        source.seek(offset)
        if source.read(4) != b"GDPC":
            fail("Windows executable has invalid embedded PCK bounds")
        source.seek(offset)
        with Path(pck).open("xb") as destination:
            remaining = size
            while remaining:
                chunk = source.read(min(1024 * 1024, remaining))
                if not chunk:
                    fail("Windows executable ended inside its embedded PCK")
                destination.write(chunk)
                remaining -= len(chunk)
    print("desktop-artifacts: Windows package prepared; bootstrap remains OPEN")


def complete_windows(root, output, start_path, pck, bootstrap_log):
    from act1_candidate_identity import artifact_paths, sha256_file
    from desktop_build_provenance import complete_export
    from remote_common import MAX_RESULT_BYTES, MAX_RESULT_FILES, validate_candidate_guard_project
    output = Path(output).resolve()
    start = json.loads(Path(start_path).read_text(encoding="utf-8"))
    if start.get("producer", {}).get("mode") != "release-windows":
        fail("Windows package requires the release-windows export profile")
    log = Path(bootstrap_log).read_text(encoding="utf-8-sig")
    if ("fullgame-scene-loaded: urman.fullgame:scene/pw-prologue physical=W01" not in log
            or "urman-photoworld-bootstrap-ready: scene=urman.fullgame:scene/pw-prologue supported=true" not in log
            or re.search(r"(^|\s)(ERROR:|SCRIPT ERROR:)|\bFAIL\b|Unhandled exception", log, re.MULTILINE)
            or not re.search(r"userdata guard: original files.*restored.*verified", log)):
        fail("Windows PCK bootstrap did not confirm clean PhotoWorlds launch and userdata restoration")
    payload = verify_windows_package(output)
    raw, entries = read_pck(Path(pck))
    exe = output / "windows/URMAN.exe"
    with exe.open("rb") as source:
        source.seek(-12, 2)
        size, magic = struct.unpack("<Q4s", source.read(12))
        offset = exe.stat().st_size - 12 - size
        if magic != b"GDPC" or offset < 0 or size != len(raw):
            fail("Windows embedded PCK changed after extraction")
        source.seek(offset)
        digest = hashlib.sha256()
        remaining = size
        while remaining:
            chunk = source.read(min(1024 * 1024, remaining))
            if not chunk:
                fail("Windows embedded PCK ended unexpectedly")
            digest.update(chunk)
            remaining -= len(chunk)
        if digest.digest() != hashlib.sha256(raw).digest():
            fail("Windows embedded PCK differs from the verified bootstrap package")
    assert_scope(raw, entries, "Windows embedded")
    for marker in (b'"kind": "urman.ambient_audio_manifest"',
                   b"res://assets/audio/village_day_ambience.wav",
                   b"res://assets/audio/house_room_tone.wav",
                   b"res://assets/audio/kara_urman_edge_ambience.wav",
                   b"res://assets/audio/water_edge_ambience.wav"):
        if marker not in raw:
            fail(f"Windows embedded PCK is missing ambient marker: {marker!r}")
    artifacts = []
    for (platform, kind), relative in artifact_paths("release-windows").items():
        path = output / relative
        item = {"platform": platform, "kind": kind, "path": str(path),
                "sizeBytes": path.stat().st_size, "sha256": sha256_file(path)}
        if kind == "zip":
            with zipfile.ZipFile(path) as archive:
                item["entryCount"] = len(archive.infolist())
        artifacts.append(item)
    provenance = complete_export(root, start, artifacts)
    validate_candidate_guard_project({"buildProvenance": provenance}, Path(root) / "game/project.godot")
    receipt = {
        "schema": "urman.desktop_artifact_receipt.v1", "status": "PASS",
        "scope": "structural-package-verification",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "windowsHostExecution": "OPEN", "artifacts": artifacts,
        "architectures": {"Windows": ["x86_64"]},
        "dotnetRuntimeTargets": [".NETCoreApp,Version=v10.0/win-x64"],
        "checks": {"zipIntegrity": True, "safeUniqueEntries": True,
                   "windowsArchiveMetadataClean": True, "exactWindowsArchitecture": True,
                   "windowsArchiveMatchesStagingExecutable": True,
                   "windowsArchiveMatchesStagingTree": True, "dotnetDependencyClosure": True,
                   "ambientContentMarkers": True, "embeddedPckScope": True,
                   "photoWorldsPackageBootstrap": True},
        "windowsPayload": payload, "buildProvenance": provenance,
        "bootstrap": {"scope": "guarded-headless-main-pack", "logSha256": sha256_file(Path(bootstrap_log))},
        "windowsStagingRetention": "standalone-executable; complete payload retained in Windows ZIP",
        "notProven": ["Windows EXE host execution", "release signing", "release-hardware performance",
                      "full playthrough acceptance", "macOS package"],
    }
    # ZIP/tree equality and all package checks have succeeded. Retain the
    # declared standalone EXE; shipping the full staging tree twice can exceed
    # the outer transport's size/file-count bounds.
    for path in sorted((output / "windows").rglob("*"), key=lambda path: len(path.parts), reverse=True):
        if path != exe:
            if path.is_dir():
                path.rmdir()
            else:
                path.unlink()
    receipt_bytes = (json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    # The runner removes this exact extracted PCK in finally. Reserve eight
    # closing receipt/log files and 8 MiB for their remaining output.
    retained = [path for path in output.rglob("*") if path.is_file() and path != Path(pck).resolve()]
    if (len(retained) + 1 + 8 > MAX_RESULT_FILES
            or sum(path.stat().st_size for path in retained) + len(receipt_bytes) + 8 * 1024**2 > MAX_RESULT_BYTES):
        fail("retained Windows release result exceeds station transport bounds including closing logs")
    with (output / "desktop-artifact-receipt.json").open("xb") as destination:
        destination.write(receipt_bytes)
    print("desktop-artifacts: release-windows structural PASS; Windows EXE host execution remains OPEN")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="action", required=True)
    scope = subcommands.add_parser("scope")
    scope.add_argument("pcks", nargs="+", type=Path)
    archives = subcommands.add_parser("archives")
    archives.add_argument("mac_zip")
    archives.add_argument("windows_zip")
    dotnet = subcommands.add_parser("dotnet")
    dotnet.add_argument("triples", nargs="+")
    prepare = subcommands.add_parser("windows-prepare")
    prepare.add_argument("output", type=Path)
    prepare.add_argument("pck", type=Path)
    complete = subcommands.add_parser("windows-complete")
    complete.add_argument("root", type=Path)
    complete.add_argument("output", type=Path)
    complete.add_argument("start", type=Path)
    complete.add_argument("pck", type=Path)
    complete.add_argument("bootstrap_log", type=Path)
    args = parser.parse_args()
    try:
        if args.action == "scope":
            for path in args.pcks:
                raw, entries = read_pck(path)
                assert_scope(raw, entries, path.name)
        elif args.action == "archives":
            verify_archive(args.mac_zip, reject_macos_metadata=False)
            verify_archive(args.windows_zip, reject_macos_metadata=True)
        elif args.action == "dotnet":
            if len(args.triples) % 3:
                fail("internal verifier argument error")
            for offset in range(0, len(args.triples), 3):
                verify_dotnet(*args.triples[offset:offset + 3])
        elif args.action == "windows-prepare":
            prepare_windows(args.output, args.pck)
        else:
            complete_windows(args.root, args.output, args.start, args.pck, args.bootstrap_log)
        return 0
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print(f"desktop-artifacts: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
