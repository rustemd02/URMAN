#!/usr/bin/env python3
"""Retain one exact Commons audio original and its primary rights snapshot.

Downloads only into a new source directory. It does not approve rights, alter the
station, generate audio, or launch Godot. curl uses the host's verified TLS store.
"""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.parse import urlencode, urlparse


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("title", help="exact Commons File: title")
    parser.add_argument("sha1", help="expected original-file SHA-1 from the reviewed primary page")
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    if not args.title.startswith("File:") or len(args.sha1) != 40:
        parser.error("an exact File: title and reviewed 40-character SHA-1 are required")
    destination = args.destination.resolve()
    destination.mkdir(parents=True, exist_ok=False)
    parameters = {"action": "query", "titles": args.title, "prop": "revisions|imageinfo",
                  "rvprop": "ids|timestamp|content", "rvslots": "main",
                  "iiprop": "url|extmetadata|sha1|size|timestamp", "format": "json"}
    source_url = "https://commons.wikimedia.org/w/api.php?" + urlencode(parameters)
    request = ["curl", "--fail", "--silent", "--show-error", "--max-time", "60", "--proto", "=https"]
    metadata_bytes = subprocess.run(request + [source_url], check=True, stdout=subprocess.PIPE).stdout
    metadata = json.loads(metadata_bytes)
    pages = list(metadata["query"]["pages"].values())
    if len(pages) != 1 or pages[0].get("title") != args.title or "imageinfo" not in pages[0]:
        raise ValueError("Commons did not return the exact reviewed audio file")
    page = pages[0]
    info = page["imageinfo"][0]
    if info["sha1"] != args.sha1:
        raise ValueError("Commons original changed; review the new file before downloading")
    metadata_path = destination / "commons-primary-source.json"
    metadata_path.write_bytes(metadata_bytes)
    original_url = info["url"]
    parsed = urlparse(original_url)
    if parsed.scheme != "https" or parsed.hostname != "upload.wikimedia.org":
        raise ValueError("audio source is not the Commons original-file host")
    suffix = Path(args.title).suffix.lower()
    if suffix not in (".flac", ".wav", ".ogg", ".oga", ".webm"):
        raise ValueError("unexpected audio source container")
    original = destination / ("original" + suffix)
    partial = destination / ("original" + suffix + ".download")
    subprocess.run(request + ["--max-filesize", "167772160", "--output", str(partial), original_url], check=True)
    payload = partial.read_bytes()
    if len(payload) != info["size"] or hashlib.sha1(payload).hexdigest() != args.sha1:
        raise ValueError("downloaded original differs from the Commons size or hash")
    partial.rename(original)
    licence = info.get("extmetadata", {}).get("LicenseUrl", {}).get("value", "")
    receipt = {"schemaVersion": 1, "sourceTitle": args.title, "sourcePage": info["descriptionurl"],
               "sourceApi": source_url, "sourceRevision": page["revisions"][0],
               "sourceFile": original.name, "sourceSha1": args.sha1,
               "sourceSha256": hashlib.sha256(payload).hexdigest(), "sourceBytes": len(payload),
               "primaryEvidence": metadata_path.name,
               "primaryEvidenceSha256": hashlib.sha256(metadata_bytes).hexdigest(),
               "declaredLicenseURL": licence, "durationSeconds": info.get("duration"),
               "acceptance": "original bytes and primary declaration retained; no listening or station acceptance"}
    (destination / "source-receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({key: value for key, value in receipt.items() if key != "sourceRevision"}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
