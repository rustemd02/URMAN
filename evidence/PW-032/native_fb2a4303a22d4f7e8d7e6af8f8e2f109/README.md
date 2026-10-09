# PW-032 Rinat cloth UV native capture

- Protected native Windows capture job `fb2a4303a22d4f7e8d7e6af8f8e2f109`; snapshot `01513da39a414b7da100d4f47c193356a9fc895407ba392c9a00c6fe8135c4bd`, base commit `739ab6355ed0023f26ee4823e4bb3df8c9e35c37`.
- Result PASS; Godot 4.7.1, .NET 10.0.302, interactive Vulkan/GTX 970, three points, FOV 70 and default `village_day`. All native steps exited 0; userdata was restored, and post-run doctor is ready with no busy job/orphan/recovery.
- Each sidecar reads 14 staging `Npc_rinat` cloth surfaces across 14 LOD meshes. All report `clothAnchor=uv-local`, `metricRestUv=true`, `boundUvPigment=true`, and `Rinat_Idle`. Same-spec front A/B naturally sampled animation positions 0.527684s and 0.627180s; this records clip position only, not a global pixel-difference claim or full motion acceptance.
- Original PNGs and sidecars remain under `frames/`; root review is pending. This evidence does not establish overall style acceptance or close PW-032/PW-007.
- `engine-errors.log` is empty. `game.log` also records nonfatal Painterly default-mode warnings, the outstanding VIS-022 cultural measurement warning (no rotation applied), and a horse-cart hoof-support/reach rejection recovered to checked parking.
- `source_snapshot/` preserves the exact submitted source subset and complete station snapshot manifest. Seventeen relevant uploaded entries match their sealed hashes. The Blender generator, canonical `.blend`, asset registry and local evidence receipts are preserved locally because the stock station candidate allowlist excludes them; the `.blend` was not uploaded to the station.
- Fullgame pack provenance is from the same submitted snapshot: compile receipt PASS, pack SHA `474b509a6a3ea01a679a6400ad60113d67820edcc47a8ab14704699047669c45`. It is packaging/runtime provenance only, not a full PW-007 visual or physical pass.
- `result.zip` is the fetched raw transport container and locally ignored; extracted logs are retained.

`.gitattributes` disables text normalization. `archive-sha256.json` indexes every archive file except itself.
