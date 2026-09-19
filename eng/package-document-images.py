#!/usr/bin/env python3
"""Materialize declared raster images from their source modules into the game.

The compiled manifest remains the only asset list. Logical art references are
not textures. Every destination is validated before copying, and conflicting
module paths fail rather than silently replacing another module's picture.
"""

from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path, PurePosixPath


def package_images(root: Path) -> None:
    module_roots: dict[str, Path] = {}
    for manifest in (root / "content/modules").glob("*/module.json"):
        module_id = json.loads(manifest.read_text(encoding="utf-8"))["moduleId"]
        if module_id in module_roots:
            raise ValueError(f"Duplicate module: {module_id}")
        module_roots[module_id] = manifest.parent

    copies: dict[Path, tuple[Path, str]] = {}
    for pack_path in sorted((root / "game/content").glob("*.compiled.v1.json")):
        pack = json.loads(pack_path.read_text(encoding="utf-8"))
        for asset in pack["registries"]["assets"]:
            if asset["kind"] != "image" or not asset["mediaType"].startswith("image/"):
                continue
            module = module_roots[asset["id"].split(":", 1)[0]]
            for record in [asset, *asset.get("variants", [])]:
                relative = PurePosixPath(record["file"])
                if relative.is_absolute() or ".." in relative.parts or ":" in str(relative):
                    raise ValueError(f"Invalid module image path: {relative}")
                source = (module / str(relative)).resolve()
                if not source.is_relative_to(module.resolve()) or not source.is_file():
                    raise ValueError(f"Missing module image: {asset['id']}: {source}")
                destination = root / "game/assets" / str(relative)
                digest = hashlib.sha256(source.read_bytes()).hexdigest()
                expected = record.get("sha256")
                if expected and expected != digest:
                    raise ValueError(f"Image digest differs from manifest: {asset['id']}")
                if destination in copies and copies[destination][1] != digest:
                    raise ValueError(f"Different module images use the same game path: {relative}")
                copies[destination] = (source, digest)

    for destination, (source, digest) in sorted(copies.items()):
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists() or hashlib.sha256(destination.read_bytes()).hexdigest() != digest:
            shutil.copyfile(source, destination)
        print(f"document-image: {destination.relative_to(root)} sha256={digest}")
    print(f"document-images: {len(copies)} declared raster files packaged")


if __name__ == "__main__":
    package_images(Path(__file__).resolve().parent.parent)
