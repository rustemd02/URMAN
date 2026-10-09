# PW005 native save-isolation failure

- Job: `b469c6e5fc3c4b06ba4ea83206a86f35`
- Snapshot: `ed2d2bf71b436f0b31fddaccd58c9bf97d303381b9bef2ecb982a243885e438f`
- Result: **FAIL**, native windowed run, Godot 4.7.1, .NET 10.0.302; game exit code 1. Result archive SHA-256: `532faff2d0729a33190717e02d7bcd8faf6a42f5961678e331b6a7435654bfe2`.
- Test caller SHA-256: `d557231e4e7ed75f7a5a1b8149fbeb4d4d588f6b12aace17546141ad525398bd` (39,039 bytes).

The save is written to the isolated `photoworlds-v1/quick.savegame-v3.json`. Native `game.log` reports all three comparisons:

- `initial-vs-decoded`: initial and decoded SHA differ; first differing top-level property is `$.world.props`. Initial contains `act1/opening`; decoded additionally contains `npc/alsu-street-walk` (position `0.85,0,2.05`, checkpoint and pending checkpoint 0, segment travel 0, yaw `-0.2617994`).
- `initial-vs-postsave-live`: same two hashes and same first differing property/value.
- `postsave-live-vs-decoded`: matching hashes; no differing top-level keys.

The smoke fails at `AssertPhotoWorldDiskRoundTripAsync` (`FullGameFlowSmokeTest.cs:508`), where the decoded disk state is required to equal the state captured before saving. This records the observed difference but does not diagnose its cause. `userdata guard` reports original files restored by rename and verified; ACLs were not compared, and the receipt reports no pending recovery. Physical/human play was not run. Do not close PW005 on this failure.

All fetched station artifacts, including `result.zip`, are copied byte-for-byte. `receipt.json` embeds the full source hash manifest; `source_snapshot/` contains that manifest and hash-verified copies of the test caller, save bridge/store, campaign repository, runtime state, compiled campaign, project settings, and guard/protocol files.
