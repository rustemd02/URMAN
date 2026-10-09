# PW005 native save-isolation attempt

- Job: `e0b91bad118b4ee1b56db1ab0e96a8c9`
- Snapshot: `76f9a489b1b063276931ad83db62b23a73998f9d64a1864c1355d7eeb720a9d3`
- Result: **FAIL**, native windowed smoke on UNTERPC; Godot 4.7.1.stable.mono.official.a13da4feb, .NET 10.0.302; game exit code 1. Result archive SHA-256: `983ea63f192c06783a45916bc855d956bb2395e48359b33c62e5f72e6f2c264e`.
- Test caller SHA-256: `089f27ba329912b11011f0537c5d4bfd31349063254d5163ebd94cfc5d1c785e` (38892 bytes).

The build, content build, both authored campaign compile steps, document-image step, and import succeeded. The native test failed at `AssertPhotoWorldDiskRoundTripAsync` (`FullGameFlowSmokeTest.cs:525`) with: `The disk snapshot did not preserve the real stationary Alsu pose, checkpoint, pending checkpoint, and travel state.` No initial/decoded/post-flush first-difference values were emitted, so the failure is not yet diagnosed.

`receipt.json` reports `userdata_recovery_pending: false`; `game.log` says original files were restored by rename and verified (Windows ACLs not compared). It also reports `projectRestoredSha256` matching the source `game/project.godot` proof. Physical/human play was not run. Do not close PW005 on this attempt.

Fetched files are copied byte-for-byte. `source_snapshot/` contains the sealed source manifest/proof and verified source copies. `archive-sha256.json` records local hashes for all carried files; `.gitattributes` disables text normalization.
