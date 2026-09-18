#!/bin/sh

set -eu

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"
BUILD_ROOT="$URMAN_ROOT/build"
LOCK_DIR="$BUILD_ROOT/.desktop-artifacts.lock"
extract_root=
receipt_tmp=
lock_owned=0

cleanup_verifier() {
  [ -z "$receipt_tmp" ] || rm -f "$receipt_tmp"
  [ -z "$extract_root" ] || rm -rf "$extract_root"
  if [ "$lock_owned" -eq 1 ]; then
    rm -rf "$LOCK_DIR"
    lock_owned=0
  fi
}

handle_verifier_signal() {
  signal_status=$1
  trap - EXIT HUP INT TERM
  cleanup_verifier
  exit "$signal_status"
}

trap cleanup_verifier EXIT
trap 'handle_verifier_signal 129' HUP
trap 'handle_verifier_signal 130' INT
trap 'handle_verifier_signal 143' TERM

fail() {
  echo "desktop-artifacts: $*" >&2
  exit 1
}

check_receipt=0
case $# in
  0)
    MAC_ZIP="$URMAN_ROOT/build/macos/URMAN.zip"
    WINDOWS_EXE="$URMAN_ROOT/build/windows/URMAN.exe"
    WINDOWS_ZIP="$URMAN_ROOT/build/URMAN-windows-x86_64.zip"
    RECEIPT="$URMAN_ROOT/build/desktop-artifact-receipt.json"
    ;;
  1)
    [ "$1" = "--check-receipt" ] || fail "usage: $0 [--check-receipt] or [mac_zip windows_exe windows_zip receipt]"
    check_receipt=1
    MAC_ZIP="$URMAN_ROOT/build/macos/URMAN.zip"
    WINDOWS_EXE="$URMAN_ROOT/build/windows/URMAN.exe"
    WINDOWS_ZIP="$URMAN_ROOT/build/URMAN-windows-x86_64.zip"
    RECEIPT="$URMAN_ROOT/build/desktop-artifact-receipt.json"
    ;;
  4)
    MAC_ZIP=$1
    WINDOWS_EXE=$2
    WINDOWS_ZIP=$3
    RECEIPT=$4
    ;;
  *)
    fail "usage: $0 [mac_zip windows_exe windows_zip receipt]"
    ;;
esac

command -v python3 >/dev/null 2>&1 || fail "the 'python3' command is required"

canonical_path() {
  python3 -c 'from pathlib import Path; import sys; print(Path(sys.argv[1]).resolve(strict=False))' "$1"
}

MAC_ZIP=$(canonical_path "$MAC_ZIP")
WINDOWS_EXE=$(canonical_path "$WINDOWS_EXE")
WINDOWS_ZIP=$(canonical_path "$WINDOWS_ZIP")
RECEIPT=$(canonical_path "$RECEIPT")

for artifact_path in "$MAC_ZIP" "$WINDOWS_EXE" "$WINDOWS_ZIP"; do
  [ "$RECEIPT" != "$artifact_path" ] || fail "receipt path aliases an artifact path: $RECEIPT"
done

require_file() {
  path=$1
  [ -s "$path" ] || fail "missing or empty: $path"
}

if [ "$check_receipt" -eq 1 ]; then
  require_file "$MAC_ZIP"
  require_file "$WINDOWS_EXE"
  require_file "$WINDOWS_ZIP"
  require_file "$RECEIPT"
  python3 - "$MAC_ZIP" "$WINDOWS_ZIP" "$WINDOWS_EXE" "$RECEIPT" <<'PY'
from collections import Counter
from pathlib import Path
import hashlib
import json
import stat
import sys
import unicodedata
import zipfile


def fail(message: str) -> None:
    print(f"desktop-artifacts: read-only receipt check failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def digest_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_manifest(entries: dict[str, tuple[int, str]]) -> tuple[int, str]:
    payload = "".join(
        f"{name}\t{size}\t{digest}\n"
        for name, (size, digest) in sorted(entries.items())
    ).encode("utf-8")
    return len(entries), hashlib.sha256(payload).hexdigest()


def validate_zip_name(name: str, allow_root_directory: bool = False) -> str:
    normalized = unicodedata.normalize("NFKC", name)
    if normalized.endswith("/"):
        normalized = normalized[:-1]
    if allow_root_directory and normalized == "windows":
        return ""
    if not normalized or "\\" in normalized or normalized.startswith("/"):
        fail(f"unsafe Windows ZIP entry: {name!r}")
    parts = normalized.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        fail(f"unsafe normalized Windows ZIP entry: {name!r}")
    if not normalized.startswith("windows/"):
        fail(f"Windows ZIP entry is outside the published windows/ tree: {name!r}")
    return normalized[len("windows/"):]


def archive_manifest(path: Path) -> tuple[int, str]:
    with zipfile.ZipFile(path) as archive:
        if archive.testzip() is not None:
            fail(f"ZIP CRC/integrity check failed: {path}")
        infos = archive.infolist()
        names = [info.filename for info in infos]
        duplicates = sorted(name for name, count in Counter(names).items() if count > 1)
        if duplicates:
            fail(f"duplicate ZIP entries: {duplicates[:5]}")
        entries: dict[str, tuple[int, str]] = {}
        for info in infos:
            if info.is_dir():
                validate_zip_name(info.filename, allow_root_directory=True)
                continue
            name = validate_zip_name(info.filename)
            if stat.S_ISLNK(info.external_attr >> 16):
                fail(f"symbolic-link ZIP entry is not allowed: {info.filename!r}")
            digest = hashlib.sha256()
            with archive.open(info) as source:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    digest.update(chunk)
            entries[name] = (info.file_size, digest.hexdigest())
        return canonical_manifest(entries)


def tree_manifest(root: Path) -> tuple[int, str]:
    if not root.is_dir():
        fail(f"published Windows mirror is missing: {root}")
    entries: dict[str, tuple[int, str]] = {}
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            fail(f"published Windows mirror contains a symlink: {path}")
        if path.is_file():
            relative = path.relative_to(root).as_posix()
            entries[relative] = (path.stat().st_size, digest_file(path))
    return canonical_manifest(entries)


mac_zip, windows_zip, windows_exe, receipt_path = map(Path, sys.argv[1:])
mac_zip = mac_zip.resolve()
windows_zip = windows_zip.resolve()
windows_exe = windows_exe.resolve()
receipt_path = receipt_path.resolve()
try:
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
except (OSError, json.JSONDecodeError) as error:
    fail(f"invalid receipt: {error}")

if receipt.get("schema") != "urman.desktop_artifact_receipt.v1":
    fail("unsupported receipt schema")
if receipt.get("status") != "PASS" or receipt.get("scope") != "structural-package-verification":
    fail("receipt status/scope is not the structural PASS contract")
if receipt.get("windowsHostExecution") != "OPEN":
    fail("receipt must keep Windows host execution OPEN")
checks = receipt.get("checks")
if not isinstance(checks, dict) or not all(checks.values()):
    fail("receipt contains a non-passing check")
if checks.get("windowsArchiveMatchesStagingTree") is not True:
    fail("receipt predates full Windows payload-manifest binding")

expected = [mac_zip, windows_zip, windows_exe]
artifacts = receipt.get("artifacts")
if not isinstance(artifacts, list) or len(artifacts) != len(expected):
    fail("receipt must contain exactly three artifacts")
by_path = {Path(item.get("path", "")).resolve(): item for item in artifacts}
for path in expected:
    item = by_path.get(path)
    if item is None:
        fail(f"receipt is missing artifact path: {path}")
    actual_digest = digest_file(path)
    if item.get("sha256") != actual_digest or item.get("sizeBytes") != path.stat().st_size:
        fail(f"receipt digest/size mismatch: {path}")
    if item.get("kind") == "zip":
        with zipfile.ZipFile(path) as archive:
            if item.get("entryCount") != len(archive.infolist()):
                fail(f"receipt entryCount mismatch: {path}")

archive_count, archive_digest = archive_manifest(windows_zip)
mirror_count, mirror_digest = tree_manifest(windows_zip.parent / "windows")
payload = receipt.get("windowsPayload")
if not isinstance(payload, dict):
    fail("receipt is missing windowsPayload manifest")
if payload.get("fileCount") != archive_count or payload.get("manifestSha256") != archive_digest:
    fail("receipt does not match the current Windows ZIP payload manifest")
if mirror_count != archive_count or mirror_digest != archive_digest:
    fail(
        "published build/windows mirror differs from the Windows ZIP payload "
        f"(mirror={mirror_count}/{mirror_digest}, archive={archive_count}/{archive_digest})"
    )

print(
    "desktop-artifacts: READ-ONLY receipt check PASS "
    f"artifacts=3 windows_payload={archive_count} files manifest={archive_digest}"
)
PY
  exit 0
fi

for command_name in cmp file lipo strings unzip; do
  command -v "$command_name" >/dev/null 2>&1 || fail "the '$command_name' command is required"
done

mkdir -p "$BUILD_ROOT"
supplied_lock_token=${URMAN_DESKTOP_LOCK_TOKEN:-}
if [ -n "$supplied_lock_token" ]; then
  [ -d "$LOCK_DIR" ] || fail "supplied artifact lock token has no active lock: $LOCK_DIR"
  [ -f "$LOCK_DIR/owner" ] || fail "active artifact lock has no owner token: $LOCK_DIR"
  IFS= read -r active_lock_token <"$LOCK_DIR/owner" || fail "cannot read artifact lock owner: $LOCK_DIR/owner"
  [ "$active_lock_token" = "$supplied_lock_token" ] || fail "supplied artifact lock token does not own: $LOCK_DIR"
else
  if ! mkdir "$LOCK_DIR" 2>/dev/null; then
    fail "artifact lock is already held: $LOCK_DIR"
  fi
  lock_owned=1
  verifier_lock_token="verifier-$$-$(date -u +%Y%m%dT%H%M%SZ)"
  printf '%s\n' "$verifier_lock_token" >"$LOCK_DIR/owner"
fi

# A PASS receipt is valid only for one exact verified artifact set. Remove any
# previous receipt after acquiring the lock but before the first check; failures
# intentionally leave no PASS marker behind.
rm -f "$RECEIPT"

require_file "$MAC_ZIP"
require_file "$WINDOWS_EXE"
require_file "$WINDOWS_ZIP"

# CRC/integrity checks happen before extraction. Python then rejects archive
# names which unzip could otherwise normalize outside the intended root, plus
# duplicates and macOS metadata that must never be shipped in the Windows ZIP.
unzip -tq "$MAC_ZIP" >/dev/null || fail "macOS ZIP failed integrity check"
unzip -tq "$WINDOWS_ZIP" >/dev/null || fail "Windows ZIP failed integrity check"

python3 - "$MAC_ZIP" "$WINDOWS_ZIP" <<'PY' || exit 1
from collections import Counter
import stat
import sys
import unicodedata
import zipfile


def fail(message: str) -> None:
    print(f"desktop-artifacts: {message}", file=sys.stderr)
    raise SystemExit(1)


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


verify_archive(sys.argv[1], reject_macos_metadata=False)
verify_archive(sys.argv[2], reject_macos_metadata=True)
PY

windows_info=$(file "$WINDOWS_EXE")
echo "$windows_info"
echo "$windows_info" | grep -Eq 'PE32\+ executable \(GUI\).*x86-64' || fail "Windows executable is not PE32+ GUI x86-64"

extract_root=$(mktemp -d "${TMPDIR:-/tmp}/urman-desktop-artifacts.XXXXXX")

mkdir -p "$extract_root/macos" "$extract_root/windows"
unzip -q "$MAC_ZIP" -d "$extract_root/macos"
unzip -q "$WINDOWS_ZIP" -d "$extract_root/windows"

mac_resources="$extract_root/macos/URMAN.app/Contents/Resources"
mac_app="$extract_root/macos/URMAN.app/Contents/MacOS/URMAN"
mac_pck="$mac_resources/URMAN.pck"
mac_arm64_dir="$mac_resources/data_Urman.Game_macos_arm64"
mac_x86_64_dir="$mac_resources/data_Urman.Game_macos_x86_64"
windows_archive_root="$extract_root/windows/windows"
windows_archive_exe="$windows_archive_root/URMAN.exe"
windows_dotnet_dir="$windows_archive_root/data_Urman.Game_windows_x86_64"

for path in \
  "$mac_app" \
  "$mac_pck" \
  "$mac_arm64_dir/Urman.Game.dll" \
  "$mac_x86_64_dir/Urman.Game.dll" \
  "$windows_archive_exe" \
  "$windows_dotnet_dir/Urman.Game.dll"
do
  require_file "$path"
done

mac_info=$(file "$mac_app")
echo "$mac_info"
mac_arches=$(lipo -archs "$mac_app" | tr ' ' '\n' | sed '/^$/d' | LC_ALL=C sort | tr '\n' ' ' | sed 's/ $//')
[ "$mac_arches" = "arm64 x86_64" ] || fail "macOS executable architecture set must be exactly arm64+x86_64; got: $mac_arches"

windows_archive_info=$(file "$windows_archive_exe")
echo "$windows_archive_info"
echo "$windows_archive_info" | grep -Eq 'PE32\+ executable \(GUI\).*x86-64' || fail "Windows archive payload is not PE32+ GUI x86-64"
cmp -s "$WINDOWS_EXE" "$windows_archive_exe" || fail "Windows archive executable differs from the verified staging executable"

# Validate each self-contained .NET payload against its own deps/runtimeconfig
# files. deps assets are flattened by Godot's export, so closure is checked by
# the published basename while the RID and framework contract remain exact.
python3 - \
  "$mac_arm64_dir" .NETCoreApp,Version=v10.0/osx-arm64 macos \
  "$mac_x86_64_dir" .NETCoreApp,Version=v10.0/osx-x64 macos \
  "$windows_dotnet_dir" .NETCoreApp,Version=v10.0/win-x64 windows <<'PY' || exit 1
from pathlib import Path, PurePosixPath
import json
import sys


def fail(message: str) -> None:
    print(f"desktop-artifacts: {message}", file=sys.stderr)
    raise SystemExit(1)


def only_file(root: Path, suffix: str) -> Path:
    matches = sorted(root.glob(f"*{suffix}"))
    if len(matches) != 1:
        fail(f"expected exactly one *{suffix} in {root}, found {len(matches)}")
    return matches[0]


arguments = sys.argv[1:]
if len(arguments) % 3:
    fail("internal verifier argument error")

for offset in range(0, len(arguments), 3):
    root = Path(arguments[offset])
    expected_target = arguments[offset + 1]
    platform = arguments[offset + 2]
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
PY

verify_audio_markers() {
  packaged_payload=$1
  payload_label=$2
  strings_output=$3
  strings -a "$packaged_payload" >"$strings_output"
  for marker in \
    '"kind": "urman.ambient_audio_manifest"' \
    'res://assets/audio/village_day_ambience.wav' \
    'res://assets/audio/house_room_tone.wav' \
    'res://assets/audio/kara_urman_edge_ambience.wav' \
    'res://assets/audio/water_edge_ambience.wav'
  do
    grep -Fq "$marker" "$strings_output" || fail "$payload_label is missing marker: $marker"
  done
}

verify_audio_markers "$mac_pck" "macOS PCK" "$extract_root/macos-pck.strings"
verify_audio_markers "$windows_archive_exe" "Windows embedded PCK" "$extract_root/windows-exe.strings"

mkdir -p "$(dirname -- "$RECEIPT")"
receipt_tmp=$(mktemp "$(dirname -- "$RECEIPT")/.desktop-artifact-receipt.XXXXXX")
# Record the checkout inspected by this verifier separately from build inputs.
# Only a completed exporter run binds its before/after source snapshots to the
# actual artifacts. A standalone structural check cannot establish freshness.
source_commit=$(git -C "$URMAN_ROOT" rev-parse HEAD 2>/dev/null || echo unknown)
# Record the exact modified paths, not just clean/dirty: a dirty tree means the
# package cannot be reproduced from the commit alone, and whoever reads the
# receipt needs to know which files differed to judge whether they can even
# reach the build (a docs edit cannot; a game/assets edit can).
source_dirty_paths=$(git -C "$URMAN_ROOT" status --porcelain 2>/dev/null | awk '{print $2}' | paste -sd, - || true)
if [ -z "$source_dirty_paths" ]; then
  source_tree=clean
else
  source_tree=dirty
  echo "desktop-artifacts: WARNING source tree is dirty; receipt records workTree=dirty and the modified paths" >&2
  echo "desktop-artifacts: WARNING dirty paths: $source_dirty_paths" >&2
fi
python3 - "$MAC_ZIP" "$WINDOWS_ZIP" "$WINDOWS_EXE" "$RECEIPT" "$windows_archive_root" \
  "$source_commit" "$source_tree" "$source_dirty_paths" "$URMAN_ROOT" "${URMAN_DESKTOP_BUILD_PROVENANCE:-}" >"$receipt_tmp" <<'PY'
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import sys
import zipfile

sys.path.insert(0, str(Path(sys.argv[9]) / "eng"))
from act1_candidate_identity import source_manifest, validate_build_provenance


def artifact(path_string: str, platform: str, kind: str) -> dict:
    path = Path(path_string).resolve()
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    result = {
        "platform": platform,
        "kind": kind,
        "path": str(path),
        "sizeBytes": path.stat().st_size,
        "sha256": digest.hexdigest(),
    }
    if kind == "zip":
        with zipfile.ZipFile(path) as archive:
            result["entryCount"] = len(archive.infolist())
    return result


def payload_manifest(root_string: str) -> dict:
    root = Path(root_string).resolve()
    entries = {}
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            raise SystemExit(f"desktop-artifacts: staged Windows payload contains a symlink: {path}")
        if path.is_file():
            digest = hashlib.sha256()
            with path.open("rb") as source:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    digest.update(chunk)
            entries[path.relative_to(root).as_posix()] = (path.stat().st_size, digest.hexdigest())
    canonical = "".join(
        f"{name}\t{size}\t{digest}\n"
        for name, (size, digest) in sorted(entries.items())
    ).encode("utf-8")
    return {
        "fileCount": len(entries),
        "manifestSha256": hashlib.sha256(canonical).hexdigest(),
    }


receipt = {
    "schema": "urman.desktop_artifact_receipt.v1",
    "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "status": "PASS",
    "scope": "structural-package-verification",
    "source": {
        "commit": sys.argv[6],
        "workTree": sys.argv[7],
        "dirtyPaths": [path for path in sys.argv[8].split(",") if path],
        "verificationSnapshot": source_manifest(Path(sys.argv[9])),
        "note": "checkout at structural verification; this snapshot is not evidence of the inputs used to build the package",
    },
    "windowsHostExecution": "OPEN",
    "artifacts": [
        artifact(sys.argv[1], "macOS", "zip"),
        artifact(sys.argv[2], "Windows", "zip"),
        artifact(sys.argv[3], "Windows", "executable"),
    ],
    "architectures": {
        "macOS": ["arm64", "x86_64"],
        "Windows": ["x86_64"],
    },
    "dotnetRuntimeTargets": [
        ".NETCoreApp,Version=v10.0/osx-arm64",
        ".NETCoreApp,Version=v10.0/osx-x64",
        ".NETCoreApp,Version=v10.0/win-x64",
    ],
    "checks": {
        "zipIntegrity": True,
        "safeUniqueEntries": True,
        "windowsArchiveMetadataClean": True,
        "windowsArchiveMatchesStagingExecutable": True,
        "exactMacArchitectures": True,
        "dotnetDependencyClosure": True,
        "ambientContentMarkers": True,
        "windowsArchiveMatchesStagingTree": True,
    },
    "windowsPayload": payload_manifest(sys.argv[5]),
    "notProven": [
        "Windows host execution",
        "release signing or notarization",
        "release-hardware performance",
        "full playthrough acceptance",
    ],
}
if sys.argv[10]:
    provenance = json.loads(Path(sys.argv[10]).read_text(encoding="utf-8"))
    validate_build_provenance(provenance, receipt["artifacts"])
    receipt["buildProvenance"] = provenance
else:
    receipt["buildProvenance"] = {"status": "unverified", "reason": "standalone structural verification has no completed exporter run"}
    receipt["notProven"].append("source-to-build binding")
json.dump(receipt, sys.stdout, ensure_ascii=False, indent=2)
sys.stdout.write("\n")
PY
# Re-read the temporary receipt and independently bind every recorded digest to
# the verified input paths before it is published. A truncated, stale or
# mismatched receipt never becomes the durable PASS marker.
python3 - "$receipt_tmp" "$MAC_ZIP" "$WINDOWS_ZIP" "$WINDOWS_EXE" "$windows_archive_root" <<'PY' || exit 1
from pathlib import Path
import hashlib
import json
import sys


def fail(message: str) -> None:
    print(f"desktop-artifacts: {message}", file=sys.stderr)
    raise SystemExit(1)


receipt_path = Path(sys.argv[1])
try:
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
except (OSError, json.JSONDecodeError) as error:
    fail(f"invalid artifact receipt {receipt_path}: {error}")

if receipt.get("schema") != "urman.desktop_artifact_receipt.v1" or receipt.get("status") != "PASS":
    fail(f"artifact receipt has an unsupported schema or status: {receipt_path}")
if receipt.get("windowsHostExecution") != "OPEN":
    fail("structural receipt must keep Windows host execution OPEN")
source = receipt.get("source")
if source is not None:
    if not isinstance(source, dict) or not str(source.get("commit", "")).strip():
        fail(f"artifact receipt has a malformed source block: {receipt_path}")
    if source.get("workTree") not in {"clean", "dirty"}:
        fail(f"artifact receipt source.workTree must be clean or dirty: {receipt_path}")
    if source.get("workTree") == "dirty":
        print(
            f"desktop-artifacts: NOTE {receipt_path} was built from a dirty tree; "
            f"it cannot be reproduced from commit {source.get('commit')} alone. "
            f"Modified paths: {', '.join(source.get('dirtyPaths', [])) or 'not recorded'}",
            file=sys.stderr,
        )
else:
    print(
        f"desktop-artifacts: NOTE {receipt_path} predates the source-provenance block",
        file=sys.stderr,
    )
if not all(receipt.get("checks", {}).values()):
    fail(f"artifact receipt contains a non-passing structural check: {receipt_path}")

expected_paths = [Path(value).resolve() for value in sys.argv[2:5]]
artifacts = receipt.get("artifacts", [])
if len(artifacts) != len(expected_paths):
    fail(f"artifact receipt must contain exactly {len(expected_paths)} artifacts")

by_path = {Path(item.get("path", "")).resolve(): item for item in artifacts}
for path in expected_paths:
    item = by_path.get(path)
    if item is None:
        fail(f"artifact receipt is missing verified path: {path}")
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    if item.get("sha256") != digest.hexdigest() or item.get("sizeBytes") != path.stat().st_size:
        fail(f"artifact receipt digest/size mismatch: {path}")

payload_root = Path(sys.argv[5]).resolve()
entries = {}
for path in sorted(payload_root.rglob("*")):
    if path.is_symlink():
        fail(f"staged Windows payload contains a symlink: {path}")
    if path.is_file():
        digest = hashlib.sha256()
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        entries[path.relative_to(payload_root).as_posix()] = (path.stat().st_size, digest.hexdigest())
canonical = "".join(
    f"{name}\t{size}\t{digest}\n"
    for name, (size, digest) in sorted(entries.items())
).encode("utf-8")
payload = receipt.get("windowsPayload")
if not isinstance(payload, dict) or payload.get("fileCount") != len(entries) or payload.get("manifestSha256") != hashlib.sha256(canonical).hexdigest():
    fail("artifact receipt Windows payload manifest does not match staged archive contents")

print(f"desktop-artifacts: receipt verification PASS {receipt_path}")
PY
mv -f "$receipt_tmp" "$RECEIPT"
receipt_tmp=

printf '%s\n' \
  "desktop-artifacts: PASS" \
  "desktop-artifacts: macOS ZIP has exact arm64+x86_64 app and complete .NET payloads" \
  "desktop-artifacts: Windows ZIP is safe/clean and matches the staged PE32+ GUI x86-64 executable" \
  "desktop-artifacts: macOS PCK and Windows embedded PCK contain ambient manifest and four routed WAV stems" \
  "desktop-artifacts: receipt: $RECEIPT" \
  "desktop-artifacts: Windows host execution remains OPEN"
