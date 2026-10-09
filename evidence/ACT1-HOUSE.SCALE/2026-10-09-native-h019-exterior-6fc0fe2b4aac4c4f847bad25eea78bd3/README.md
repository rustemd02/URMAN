# H019 protected exterior geometry capture

- Job `6fc0fe2b4aac4c4f847bad25eea78bd3`; snapshot `29659a2c0b03a5fd1240c64afde5bb5d81e06d24e54a46b252f878b8f9c305bb`; station result **PASS** in a native windowed `res://scenes/act1_demo.tscn` capture on UNTERPC, Godot 4.7.1, .NET SDK 10.0.302.
- The two requested exterior points are preserved verbatim in `request.json`. Both sidecars resolve the subject to one visible `MainStreetEastNeighborFacade` in `village_day`, with `village-winter-frost` and explicit FOV 70.
- `frames/` contains the untouched original PNGs and their exact station sidecars. Each sidecar includes the four-target H019 mesh census and three live geometry rays; `capture-evidence.json` is a compact extraction for review.
- `receipt.json`, `result.zip`, fetched logs, request, and post-run doctor are retained byte-for-byte. Receipt reports all build/content/import/game steps exit 0, zero engine errors, and `userdataRestored=true`; post-run doctor reports ready with no busy job, orphan process, or pending recovery.
- `source_snapshot/` preserves selected source files and the complete receipt snapshot manifest. Copies were accepted only when their bytes matched the pre-submit proof or receipt manifest; see `selected-source-proof.json`.
- This is visual diagnostic evidence. Human playtest and art/style acceptance are **not** claimed; root review remains pending.

`.gitattributes` disables text normalization so original log and receipt bytes remain stable. `archive-sha256.json` indexes every archive file other than itself.
