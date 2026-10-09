# Babai house shape and Rinat skin protected capture

- Job `6dc5fb1948d14cfe829d0c8af0799a98`; snapshot `eeb53479d0b81d445552cd40d4f43353406de01587f20203656233e868ad0539`; native protected capture **PASS` in `res://scenes/act1_demo.tscn` on UNTERPC, Godot 4.7.1.stable.mono.official.a13da4feb, .NET SDK 10.0.302.
- Five requested points, FOV 70, default `village_day`, are preserved in `request.json`. All sidecars report the default zone/profile; no environment overrides were used.
- Original PNGs, all sidecars and the full station logs are retained. The two `rinat_material_` sidecars include the protected Rinat material readback. It captures the requested staging actor and every scene mesh named `Rinat_Body_LOD0/LOD1`; inspect node paths when distinguishing the staging NPC from authored-world residents.
- Receipt reports all build/content/import/game steps exit 0, no engine errors and `userdataRestored=true`. Post-run doctor is preserved. This is diagnostic evidence only; human playtest and style acceptance remain pending root review.
- `source_snapshot/` preserves the exact sealed exterior GLB, Blender source and generator, registry with the exterior row, skin/material and DevView code, plus zone/manifest context and the full receipt snapshot manifest. `selected-source-proof.json` links each copy to the pre-submit proof and station manifest.

`.gitattributes` disables text normalization. `archive-sha256.json` indexes every archive file except itself.
