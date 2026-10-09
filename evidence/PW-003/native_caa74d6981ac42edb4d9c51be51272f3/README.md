# PW-003 full-game semantic smoke — clean W01 failure exit

Protected native Windows smoke `caa74d6981ac42edb4d9c51be51272f3` used sealed snapshot `a88bb7b23b7cfadb1f3cc12b2232ef26e29c307dedd97ff79c97240e6a8eed0e` from `620394f0572742d1a08a3d220a8d74f5d890c2ba`. The full 1,825-file manifest matches the receipt. Six cleanup-flow line edits are archived in the exact `FullGameFlowSmokeTest.cs` snapshot; route assertions and canonical content pack remain unchanged. The prior timeout attempt remains preserved separately at `evidence/PW-003/native_f377c44ea4bf447cae3c6ca6a51c422f`.

- Stock command: `eng/remote-check.py smoke --scene res://tests/full_game_flow_smoke_test.tscn --timeout 300 --submit-only`; native window, no environment or phase override.
- Build, content-build, both campaign content validations, document-image validation, and import all exited 0. The unchanged flow reached forest-group, photographer, and Shurale observations, then failed at the existing W01 completion assertion. W02 was not reached.
- The cleanup adjustment allowed the child to exit 1 at 129.8s rather than timing out at 300s. This is still a semantic FAIL, not a canonical route PASS, and there was no retry.
- Receipt's `userdataRestored` is null despite the clean game exit. The game log explicitly reports that the guard restored original user files by rename and verification; the post-run doctor is ready with no recovery pending, and the restored project hash matches the sealed snapshot. Windows ACLs were not compared.
- Physical anchors, ordinary play, human playtest, and art acceptance remain unproven.
- Raw result ZIP remains in ignored `.codex-captures/remote/caa74d6981ac42edb4d9c51be51272f3/result.zip`; its SHA-256 is recorded.
