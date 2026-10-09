# PW-003 full-game semantic smoke — expected W01 failure

Protected native Windows smoke `f377c44ea4bf447cae3c6ca6a51c422f` used sealed snapshot `b4e60d36de35fd2249c51fffaab09ebbbf6e372f766886a1db60443c2a174438` from `32264f34b933eda2cf015b4ff188e32cb126a31e`. The full 1,825-file source manifest is preserved and matches the receipt; exact test, scene, campaign, runtime and compiler source copies are under `source/`. The test and scene were not modified.

- Stock command: `eng/remote-check.py smoke --scene res://tests/full_game_flow_smoke_test.tscn --timeout 300 --submit-only`; native window (`headless=false`), no environment or phase override.
- Build, content-build, chapter1/fullgame content validation, document-image validation, and import steps exited 0. The unchanged smoke completed prologue/save setup and dispatched the W01 forest-group, photographer, and Shurale interactions; it then failed at the existing W01 completion assertion. The exact exception is in both `game.log` and `engine-errors.log`. W02 completion was not reached.
- The overall job is FAIL because the child later exited 124 at the 300-second game guard while shutting down after the semantic assertion. This is not a clean process exit; no retry was run.
- Receipt reports verified snapshot hashes. `userdataRestored` is null because of the timeout, but the game log records that the guard restored original user files by rename and verification. The post-run doctor is ready with no pending userdata recovery, and `projectRestoredSha256` matches the sealed snapshot. Windows ACLs were not compared.
- This is semantic gate evidence only. Physical anchors, ordinary play, human playtest, and visual/art acceptance are not established.
- Raw result ZIP remains in ignored `.codex-captures/remote/f377c44ea4bf447cae3c6ca6a51c422f/result.zip`; its SHA-256 is recorded.
