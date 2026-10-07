#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
REGISTRY="$URMAN_ROOT/assets/asset_registry.json"

if [ "$#" -gt 1 ]; then
  echo "Usage: eng/verify-asset-registry.sh [registry.json]" >&2
  exit 64
fi
if [ "$#" -eq 1 ]; then
  REGISTRY=$1
fi

exec python3 - "$URMAN_ROOT" "$REGISTRY" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path


root = Path(sys.argv[1]).resolve()
registry_arg = Path(sys.argv[2]).expanduser()
if not registry_arg.is_absolute():
    registry_arg = (Path.cwd() / registry_arg).resolve()
else:
    registry_arg = registry_arg.resolve()

errors = []
sha256_re = re.compile(r"^[0-9a-fA-F]{64}$")


def fail(message):
    errors.append(message)


def safe_repo_path(value, label):
    if not isinstance(value, str) or not value.strip():
        fail(f"{label}: expected a non-empty relative path")
        return None
    candidate = Path(value)
    if candidate.is_absolute() or ".." in candidate.parts:
        fail(f"{label}: path escapes repository: {value!r}")
        return None
    return root / candidate


def digest(path):
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def check_hash(value, path, label):
    if not isinstance(value, str) or not sha256_re.fullmatch(value):
        fail(f"{label}: missing or malformed SHA-256")
        return False
    if path is None or not path.is_file():
        return False
    actual = digest(path)
    if actual.lower() != value.lower():
        fail(f"{label}: expected {value.lower()}, actual {actual}")
        return False
    return True


def source_is_explicit_path(value):
    if not isinstance(value, str):
        return False
    # ImageGen/audio provenance is intentionally descriptive text. Only exact
    # repository paths are dereferenced here; Blender source paths are always
    # explicit and therefore receive the strongest source+hash check.
    if value.startswith(("assets/", "game/", "content/", "tools/", "docs/")):
        return " " not in value and "\t" not in value and "\n" not in value
    return value.endswith((".blend", ".glb", ".png", ".wav", ".ttf", ".otf"))


EXTERNAL_LICENSES = {"CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0", "OFL-1.1", "MIT", "Apache-2.0"}


def check_external_asset(asset, prefix):
    """License/provenance chain file -> source -> licence -> consumer for external assets."""
    license_id = asset.get("licenseSpdx")
    if license_id not in EXTERNAL_LICENSES:
        fail(f"{prefix}.licenseSpdx: external asset needs one of {sorted(EXTERNAL_LICENSES)}, got {license_id!r}")
    for field in ("sourceUrl", "sourceLicenseUrl"):
        value = asset.get(field)
        if not isinstance(value, str) or not value.startswith("https://"):
            fail(f"{prefix}.{field}: external asset needs an https URL")
    for field in ("sourceAuthor", "modification"):
        value = asset.get(field)
        if not isinstance(value, str) or not value.strip():
            fail(f"{prefix}.{field}: missing or empty (write 'none' if unmodified)")
    consumers = asset.get("consumerPaths")
    if not isinstance(consumers, list) or not consumers:
        fail(f"{prefix}.consumerPaths: external asset needs at least one runtime consumer path")
    else:
        for consumer in consumers:
            path = safe_repo_path(consumer, f"{prefix}.consumerPaths")
            if path is not None and not path.is_file():
                fail(f"{prefix}.consumerPaths: missing file: {consumer}")
    if not (isinstance(asset.get("sourceSha256"), str) and sha256_re.fullmatch(asset["sourceSha256"])):
        fail(f"{prefix}.sourceSha256: external asset needs the hash of the original download")
    receiver = asset.get("receiverCapture")
    if asset.get("integrated") is True:
        path = safe_repo_path(receiver, f"{prefix}.receiverCapture")
        if path is not None and not path.is_file():
            fail(f"{prefix}.receiverCapture: integrated external asset needs an existing runtime capture file")
    elif receiver not in (None, "PENDING"):
        fail(f"{prefix}.receiverCapture: use 'PENDING' until integrated is true")


if not registry_arg.is_file():
    fail(f"registry: missing file: {registry_arg}")
    data = None
else:
    try:
        data = json.loads(registry_arg.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"registry: invalid JSON: {exc}")
        data = None

asset_count = 0
derived_count = 0
source_count = 0
external_count = 0
if isinstance(data, dict):
    if data.get("schemaVersion") != 1:
        fail(f"registry: schemaVersion must be 1, got {data.get('schemaVersion')!r}")
    assets = data.get("assets")
    if not isinstance(assets, list):
        fail("registry: assets must be an array")
        assets = []
    seen_ids = set()
    for index, asset in enumerate(assets):
        prefix = f"assets[{index}]"
        if not isinstance(asset, dict):
            fail(f"{prefix}: expected an object")
            continue
        asset_count += 1
        asset_id = asset.get("id")
        if not isinstance(asset_id, str) or not asset_id.strip():
            fail(f"{prefix}.id: missing or empty")
        elif asset_id in seen_ids:
            fail(f"{prefix}.id: duplicate {asset_id!r}")
        else:
            seen_ids.add(asset_id)
        for field in ("generator", "license"):
            if not isinstance(asset.get(field), str) or not asset[field].strip():
                fail(f"{prefix}.{field}: missing or empty")

        derived = safe_repo_path(asset.get("derived"), f"{prefix}.derived")
        if derived is not None:
            derived_count += 1
            if not derived.is_file():
                fail(f"{prefix}.derived: missing file: {asset.get('derived')}")
            else:
                check_hash(asset.get("derivedSha256"), derived, f"{prefix}.derivedSha256")

        if asset.get("externalAsset") is True:
            # VIS-107: forward-only provenance gate for downloaded/third-party assets.
            # Legacy entries are not retroactively required to carry these fields.
            check_external_asset(asset, prefix)
            external_count += 1

        source_value = asset.get("source")
        if source_is_explicit_path(source_value):
            source = safe_repo_path(source_value, f"{prefix}.source")
            if source is not None:
                source_count += 1
                if not source.is_file():
                    fail(f"{prefix}.source: missing file: {source_value}")
                else:
                    check_hash(asset.get("sourceSha256"), source, f"{prefix}.sourceSha256")
        elif isinstance(source_value, str) and source_value.endswith(".blend"):
            # Keep a distinct diagnostic if a future registry uses a decorated
            # Blender path rather than silently treating it as prose.
            fail(f"{prefix}.source: Blender source must be an exact repository path")

if errors:
    for message in errors:
        print(f"asset-registry-preflight: FAIL: {message}", file=sys.stderr)
    print(
        f"asset-registry-preflight: checked assets={asset_count} derived={derived_count} explicit_sources={source_count} external={external_count}",
        file=sys.stderr,
    )
    raise SystemExit(1)

print(
    f"asset-registry-preflight: PASS assets={asset_count} derived={derived_count} explicit_sources={source_count} external={external_count}"
)
PY
