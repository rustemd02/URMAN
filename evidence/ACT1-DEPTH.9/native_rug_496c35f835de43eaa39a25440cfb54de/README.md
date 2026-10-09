# ACT1-DEPTH.9 rug UV protected capture

Protected native Windows capture for two existing `house_old_pc` DevView points. This archive records runtime material readback and source provenance; it is not visual-style acceptance or task closure.

- Job `496c35f835de43eaa39a25440cfb54de`: PASS, exit code 0. Snapshot `c9c2c9e87be19e64095760e8da5c579a9c7c629e1c9796b6d5bb6c523ce6883e` from base `954d8ee35bc4d53e159f264fb857f2ab4582af0c`; 1,825 station-source files, hash-verified. Native Godot 4.7.1.stable.mono.official.a13da4feb, .NET SDK 10.0.302, actual elapsed 335.56s, timeout 300s, FOV70, no phase override. `userdataRestored=true`; game/build/import step results are in `receipt.json`.
- Points: `babai_room_overview` and `babai_rug_detail`; both sidecars show requested and active zone `house_old_pc`, profile `village-winter-frost`, FOV70.
- Each sidecar reports six visible Babai interior rug surfaces (field/bands, LOD0 and LOD1): `authored_uv_texture=true`, `bound_uv_pigment=true`, texture scale `(1.0, 1.0)`, using `carpet_palas_v1_albedo.png`. This is material readback, not proof of final appearance or whole-room acceptance.
- Both original PNGs are 1886×1061 by PNG IHDR. Each sidecar reports a 1920×1080 viewport; both values are preserved as captured.
- `engine-errors.log` and `stderr.log` are empty. Root must review the original frames. No player walk, collision/route, whole-room art, or human acceptance is claimed.
- `source-proof.json` maps every selected station file to the sealed receipt and preserves exact local-only copies of the generator, Blender source, asset registry/selected nine rows, export receipt and band-selection evidence. Those authoring/provenance files were excluded from the station snapshot; their presence here does not imply they ran on Windows. The raw `result.zip` is retained under ignored `.codex-captures/remote/496c35f835de43eaa39a25440cfb54de/result.zip`; its SHA-256 is `c6a313a16695b6928b93364a122ee77904667700e2cb35878694feb68287cb85`.
