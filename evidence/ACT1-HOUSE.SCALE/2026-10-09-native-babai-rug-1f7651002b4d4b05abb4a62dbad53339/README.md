# Babai interior rug protected capture

- Job `1f7651002b4d4b05abb4a62dbad53339`; snapshot `9ed4c749dd9fc316275ef70e72161cad0e64e6794b7735f8d16fecf54c45cac2`; native protected capture **PASS` in `res://scenes/act1_demo.tscn` on UNTERPC, Godot 4.7.1.stable.mono.official.a13da4feb, .NET SDK 10.0.302.
- Requested room-overview and close rug-detail points are preserved verbatim in `request.json`. Sidecars bind both to `house_old_pc`, FOV 70, and the authored winter-frost profile. No environment overrides were used.
- Original PNGs and full DevView rug material readbacks are under `frames/`. The readback includes visible interior rug field/bands and inactive legacy OldPc rug nodes; see each sidecar or the compact extraction in `capture-evidence.json`.
- Receipt reports all build/content/import/game steps exit 0, zero engine errors, and `userdataRestored=true`. Post-run doctor is preserved. Human playtest and art/style acceptance are not claimed; root review is pending.
- `source_snapshot/` preserves the sealed generator, GLB, authoring blend, exact nine registry rows plus full registry, interior factory, DevView readback, Painterly material library, texture, checkpoint manifest and related sources. `selected-source-proof.json` records byte/hash matches; .blend is local authoring evidence and is excluded by the station protocol from runtime upload.

`.gitattributes` disables text normalization. `archive-sha256.json` indexes all files except itself.
