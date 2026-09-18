#!/usr/bin/env python3
"""Record the actual M10 candidate and compare it with repository build inputs."""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import uuid
import zipfile

from godot_pck import read_pck

PACKS = ("urman.chapter1", "urman.fullgame")
ASSEMBLIES = ("Urman.Game.dll", "Urman.Core.dll", "Urman.Content.dll")
SOURCE_SCHEMA = "urman.game_source_manifest.v1"
PROVENANCE_SCHEMA = "urman.desktop_build_provenance.v1"
ARTIFACT_PATHS = {("macOS", "zip"): "macos/URMAN.zip",
                  ("Windows", "zip"): "URMAN-windows-x86_64.zip",
                  ("Windows", "executable"): "windows/URMAN.exe"}
EXPORT_PRESETS = {"debug": ["macOS", "Windows Desktop"],
                  "release": ["Act I Release macOS", "Act I Release Windows"]}


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def source_manifest(root):
    """Hash runtime sources/assets, declared campaign inputs and .NET build inputs."""
    root = Path(root).resolve()
    inputs = set()
    for folder in ("game", "src-dotnet", "tools-dotnet/Urman.ContentCli"):
        for directory, children, names in os.walk(root / folder):
            children[:] = sorted(name for name in children
                                  if name not in {".godot", ".git", "bin", "obj", "__pycache__"})
            for name in names:
                path = Path(directory) / name
                if name == ".DS_Store" or name.endswith((".pyc", ".blend1")):
                    continue
                if folder == "game" or path.suffix in {".cs", ".csproj", ".props", ".targets", ".resx", ".json"}:
                    inputs.add(path)
    inputs.update((root / "content").rglob("*.json"))
    inputs.update((root / "content").rglob("*.ref"))
    for module in (root / "content/modules").glob("*/module.json"):
        for relative in json.loads(module.read_text(encoding="utf-8")).get("sourceFiles", []):
            inputs.add(module.parent / relative)
    for name in ("Directory.Build.props", "Directory.Build.targets", "global.json",
                 "NuGet.Config", "NuGet.config", "toolchain.json", "Urman.slnx"):
        if (root / name).is_file():
            inputs.add(root / name)
    files = {}
    for path in sorted(inputs):
        resolved = path.resolve()
        if not resolved.is_relative_to(root):
            raise ValueError(f"build input resolves outside the checkout: {path}")
        before = path.stat()
        digest = sha256_file(path)
        after = path.stat()
        if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise ValueError(f"build input changed during the snapshot: {path}")
        files[path.relative_to(root).as_posix()] = digest
    canonical = json.dumps(files, sort_keys=True, separators=(",", ":")).encode()
    return {"schema": SOURCE_SCHEMA, "manifestSha256": hashlib.sha256(canonical).hexdigest(),
            "fileCount": len(files), "files": files,
            "scope": "game sources/assets/import settings; declared content; .NET sources/config"}


def manifest_summary(manifest):
    return {key: value for key, value in manifest.items() if key != "files"}


def validate_source_manifest(manifest):
    if not isinstance(manifest, dict) or manifest.get("schema") != SOURCE_SCHEMA:
        raise ValueError("source input manifest has an unsupported schema")
    files = manifest.get("files")
    if not isinstance(files, dict) or not files or manifest.get("fileCount") != len(files):
        raise ValueError("source input manifest has no complete file map")
    for name, digest in files.items():
        if (not isinstance(name, str) or not name or "\\" in name
                or PurePosixPath(name).is_absolute() or any(part in {"", ".", ".."} for part in name.split("/"))
                or not isinstance(digest, str) or not re.fullmatch(r"[0-9a-f]{64}", digest)):
            raise ValueError("source input manifest contains an unsafe path or malformed hash")
    canonical = json.dumps(files, sort_keys=True, separators=(",", ":")).encode()
    if hashlib.sha256(canonical).hexdigest() != manifest.get("manifestSha256"):
        raise ValueError("source input manifest digest is invalid")
    return manifest


def artifact_bindings(artifacts):
    if not isinstance(artifacts, list):
        raise ValueError("build provenance has no artifact list")
    bindings = {}
    for item in artifacts:
        if not isinstance(item, dict):
            raise ValueError("build provenance contains a malformed artifact")
        key = (item.get("platform"), item.get("kind"))
        digest, size = item.get("sha256"), item.get("sizeBytes")
        if (key not in ARTIFACT_PATHS or key in bindings or type(size) is not int or size <= 0
                or not isinstance(digest, str) or not re.fullmatch(r"[0-9a-f]{64}", digest)):
            raise ValueError("build provenance has duplicate, incomplete or malformed artifact bindings")
        bindings[key] = (size, digest)
    if set(bindings) != set(ARTIFACT_PATHS):
        raise ValueError("build provenance must bind all three desktop artifacts")
    return bindings


def validate_build_provenance(provenance, artifacts):
    if (not isinstance(provenance, dict) or provenance.get("schema") != PROVENANCE_SCHEMA
            or provenance.get("status") != "completed"):
        raise ValueError("build provenance is not a completed exporter run")
    producer = provenance.get("producer") or {}
    mode = producer.get("mode")
    if (mode not in EXPORT_PRESETS or producer.get("script") != f"eng/export-desktop-{mode}.sh"
            or producer.get("presets") != EXPORT_PRESETS[mode]):
        raise ValueError("build provenance has an unsupported exporter")
    uuid.UUID(provenance["runId"])
    before = validate_source_manifest(provenance.get("sourceBefore"))
    after = validate_source_manifest(provenance.get("sourceAfter"))
    if before["files"] != after["files"] or before["manifestSha256"] != after["manifestSha256"]:
        raise ValueError("build inputs changed between the export snapshots")
    if artifact_bindings(provenance.get("artifacts")) != artifact_bindings(artifacts):
        raise ValueError("build provenance does not bind the verified artifact set")
    return before


def pack_identity(payload):
    if payload is None:
        return {"status": "not-shipped"}
    pack = json.loads(payload)
    return {"status": "present", "sha256": hashlib.sha256(payload).hexdigest(),
            "campaignId": pack["campaign"]["id"], "campaignFingerprint": pack["campaignFingerprint"]}


def inspect_candidate(root, candidate, historical=False):
    root, candidate = root.resolve(), candidate.resolve()
    contents = candidate / "launch/URMAN.app/Contents"
    pck_path = contents / "Resources/URMAN.pck"
    raw, entries = read_pck(pck_path)
    packs = {}
    for campaign in PACKS:
        entry = entries.get(f"content/{campaign}.compiled.v1.json")
        packs[campaign] = pack_identity(raw[entry[0]:entry[0] + entry[1]] if entry else None)
    if packs["urman.chapter1"].get("campaignId") != "urman.chapter1":
        raise ValueError("the selected application has no Chapter I campaign pack")
    candidate_files = {"URMAN.app/Contents/MacOS/URMAN": sha256_file(contents / "MacOS/URMAN"),
                       "URMAN.app/Contents/Resources/URMAN.pck": hashlib.sha256(raw).hexdigest()}
    dlls = {}
    for architecture in ("arm64", "x86_64"):
        for assembly in ASSEMBLIES:
            path = contents / f"Resources/data_Urman.Game_macos_{architecture}" / assembly
            digest = sha256_file(path)
            dlls[f"{architecture}/{assembly}"] = digest
            candidate_files[path.relative_to(candidate / "launch").as_posix()] = digest

    receipt_path = candidate / "desktop-artifact-receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8")) if receipt_path.is_file() else {}
    source = receipt.get("source") or {}
    provenance = receipt.get("buildProvenance") or {}
    recorded = {}
    current = source_manifest(root)
    current_packs = {}
    for campaign in PACKS:
        path = root / f"game/content/{campaign}.compiled.v1.json"
        current_packs[campaign] = pack_identity(path.read_bytes()) if path.is_file() else {"status": "missing"}
    changed, unknown, differences = [], [], []
    same_source = None
    if provenance.get("status") == "completed":
        recorded = validate_build_provenance(provenance, receipt.get("artifacts"))
        declared = recorded["files"]
        changed = sorted(name for name in set(declared) | set(current["files"])
                         if declared.get(name) != current["files"].get(name))
        same_source = not changed
        if changed:
            differences.append("recorded source inputs differ from the checkout")
    else:
        unknown.append("receipt has no completed exporter provenance; a verification-time source snapshot is insufficient")

    artifact_set_binding = None
    if recorded:
        expected_artifacts = artifact_bindings(receipt.get("artifacts"))
        if all((candidate / name).is_file() for name in ARTIFACT_PATHS.values()):
            artifact_set_binding = all(
                ((candidate / name).stat().st_size, sha256_file(candidate / name)) == expected_artifacts[key]
                for key, name in ARTIFACT_PATHS.items())
            if not artifact_set_binding:
                differences.append("the retained desktop artifacts differ from the completed export run")
        else:
            unknown.append("the complete exporter artifact set is unavailable for identity verification")

    binding = None
    mac_zip = candidate / "macos/URMAN.zip"
    artifacts = receipt.get("artifacts", [])
    mac_record = next((item for item in artifacts if item.get("platform") == "macOS"
                       and item.get("kind") == "zip"), None)
    if mac_record and mac_zip.is_file():
        binding = (receipt.get("schema") == "urman.desktop_artifact_receipt.v1"
                   and receipt.get("status") == "PASS"
                   and mac_record.get("sha256") == sha256_file(mac_zip)
                   and mac_record.get("sizeBytes") == mac_zip.stat().st_size)
        if binding:
            with zipfile.ZipFile(mac_zip) as archive:
                for name, digest in candidate_files.items():
                    if archive.namelist().count(name) != 1:
                        binding = False
                        break
                    with archive.open(name) as member:
                        check = hashlib.sha256()
                        for chunk in iter(lambda: member.read(1024 * 1024), b""):
                            check.update(chunk)
                    if check.hexdigest() != digest:
                        binding = False
                        break
        if not binding:
            differences.append("the unpacked binary/PCK/DLLs or macOS archive do not match the receipt")
    else:
        unknown.append("the receipt and macOS archive cannot bind the unpacked application")

    pack_comparison = {}
    for campaign in PACKS:
        actual, local = packs[campaign], current_packs[campaign]
        if actual["status"] == "not-shipped":
            pack_comparison[campaign] = "not-shipped-in-Act-I"
        elif local["status"] == "missing":
            pack_comparison[campaign] = "checkout-pack-missing"
            unknown.append(f"checkout has no {campaign} pack")
        else:
            equal = actual["sha256"] == local["sha256"]
            pack_comparison[campaign] = "equal" if equal else "different"
            if not equal:
                differences.append(f"{campaign} compiled pack differs from the checkout")

    debug_path = root / "game/.godot/mono/temp/bin/Debug/Urman.Game.dll"
    debug_sha = sha256_file(debug_path) if debug_path.is_file() else None
    exports = {}
    for configuration in ("ExportDebug", "ExportRelease"):
        for rid in ("osx-arm64", "osx-x64"):
            path = root / f"game/.godot/mono/temp/bin/{configuration}/{rid}/Urman.Game.dll"
            if path.is_file():
                exports[f"{configuration}/{rid}"] = sha256_file(path)
    commit = subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"],
                            capture_output=True, text=True, check=False).stdout.strip() or "unknown"
    status = "different" if differences else "unknown" if unknown else "matches-recorded-inputs"
    return {
        "schema": "urman.m10_candidate_identity.v1",
        "mode": "historical-or-unverified-candidate" if historical else "checkout-comparison",
        "candidate": {"path": str(candidate), "binarySha256": candidate_files["URMAN.app/Contents/MacOS/URMAN"],
                      "pckSha256": candidate_files["URMAN.app/Contents/Resources/URMAN.pck"],
                      "packs": packs, "dllSha256": dlls,
                      "receiptPath": str(receipt_path),
                      "receiptSha256": sha256_file(receipt_path) if receipt_path.is_file() else None,
                      "buildProvenance": {"status": provenance.get("status", "unverified"),
                                          "runId": provenance.get("runId"), "producer": provenance.get("producer")},
                      "source": {"commit": source.get("commit"), "workTree": source.get("workTree"),
                                 "inputManifest": manifest_summary(recorded)}},
        "checkout": {"path": str(root), "commit": commit, "packs": current_packs,
                     "debugDllSha256": debug_sha, "exportDllSha256": exports,
                     "inputManifest": manifest_summary(current)},
        "comparison": {"status": status, "sourceInputsMatch": same_source,
                       "artifactSetBoundToExportRun": artifact_set_binding,
                       "unpackedAppBoundToReceipt": binding, "packs": pack_comparison,
                       "changedSourcePaths": changed, "differences": differences, "unknown": unknown},
        "limits": ["Debug and ExportRelease DLLs are different build artifacts; byte difference alone is not staleness.",
                   "Exporter snapshots bind stable inputs to successful exports; coordinated source freeze is still required.",
                   "Before/after snapshots are not compiler attestation and cannot detect a transient edit reverted during export.",
                   "Identity and timing logs do not establish a first human playthrough or 60 active minutes."]
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--resolve-only", action="store_true")
    parser.add_argument("--historical", action="store_true")
    args = parser.parse_args()
    try:
        identity = inspect_candidate(args.root, args.candidate, args.historical)
        rendered = json.dumps(identity, ensure_ascii=False, indent=2) + "\n"
        if args.output:
            with args.output.open("x", encoding="utf-8") as output:
                output.write(rendered)
        print(rendered, end="")
        if identity["comparison"]["status"] != "matches-recorded-inputs" and not args.historical:
            print("act1-m10-session: candidate differs or cannot be tied to the checkout; "
                  "use --historical for an intentional measurement of this candidate.", file=sys.stderr)
            return 0 if args.resolve_only else 3
        return 0
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print(f"act1-m10-session: candidate inspection failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
