# Native HOUSE / H019 capture

- Job: `515ce530e1e642b9bbe404ca1577ade7`
- Snapshot: `5ecf819639a6fc03306f2f51b8ff2247dad5ca445a242490899fb99e14a1cfc3`
- Station receipt: **PASS**; mode `capture`, native windowed, Godot 4.7.1, .NET 10.0.302. Timeout was 300 seconds; the combined build/import/native job took 378.9 seconds. Terminal result archive SHA-256: `a839b361faa2310b8c3bfb245bca74614eba0594839b27f1c5b2a3453d9a2e5b`.
- Five authored points produced five original 1886×1061 PNGs and matching sidecars. The active zone is `village_day`; all frames report atmosphere profile `village-winter-frost`.
- H019 diagnostic pair uses identical camera position/target, FOV 70, zone/profile, renderer preset, viewport and atmosphere values. Capture timestamps are 1791500973 and 1791500984 Unix seconds (11 seconds apart), as expected for sequential frames. The sidecars report one visible ADR-H019 root and one visible instance for each roof mesh. Modes are `graded-runtime` and `imported-source-roof-only`. The latter is a deliberate in-memory roof-surface override comparison, not final-art evidence.
- `engine-errors.log` is empty. The original `game.log` and other station logs are preserved byte-for-byte; `game.log` includes warnings on undeclared Painterly surface modes, the VIS-022 author/cultural-consultant confirmation, and forest horse-cart placement. The userdata guard reports that original files were restored by rename and verified (ACLs were not compared).
- Source manifest and pre-submit source copies are under `source_snapshot/`. The remote snapshot included the DevView source, connected-world source, world JSON, GLB, and current flow smoke caller. The Blender generator and asset registry are also hash-preserved locally but are outside the station packager’s allowed-root list, so they were **not** station inputs.
- `babai_porch_detail` is a geometry candidate for ACT1-DEPTH.9; its visual acceptance remains pending. Root visual review is pending for all five frames. This capture does not accept the house art or prove ordinary player movement.

See `request.json`, `receipt.json`, `_capture-summary.json`, individual frame sidecars, and `source_snapshot/snapshot-manifest.json` for exact provenance.
