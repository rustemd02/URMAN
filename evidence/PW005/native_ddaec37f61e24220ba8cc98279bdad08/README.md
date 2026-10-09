# PW005 native failure archive

- Job: `ddaec37f61e24220ba8cc98279bdad08`
- Snapshot: `ea293c870baf0b0ee28c7bc32c5a3ee08aa4455c1e12578e5c6ccff773730496`
- Result: **FAIL** (`gameExitCode=1`); this is not a passing persistence check.
- Native startup reached `zone-loaded: village_day@arrival` and wrote the isolated `photoworlds-v1/quick.savegame-v3.json`. The smoke then rejected the saved snapshot at `FullGameFlowSmokeTest.cs:493` / `Require` line 591: `The disk SaveGameV3 did not retain the actual PhotoWorlds v2 campaign snapshot.`
- `game.log` records `userdata guard: original files restored by rename and verified (Windows ACLs not compared)`. `receipt.json` reports no pending userdata recovery.
- This attempt contains no accepted visual evidence or ordinary-player playthrough. Physical/human play remains `NOT_RUN`.
- Exact fetched station artifacts, including `result.zip`, are copied unchanged. `receipt.json` embeds the complete 1,820-file source snapshot hash manifest; `snapshot-manifest.json` is a readable extracted copy. Seven assertion-path source files are copied under `source/` and hash/size verified against that manifest.

The unresolved save round-trip failure is assigned for causal analysis; do not close PW005 on this archive.
