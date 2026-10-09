# Corrected full-game flow native retry — 2026-10-09

- Job: `e72c7f4c669e47c6afcd72aa6074b655`
- Snapshot: `d786f7f08d80c2ceb791b9bf253085cd98d666f5ec6a6e7850c63415d50e5770`
- Source commit: `e0095b0d7d197760521c8113278795a57e8377a8`
- Station: `UNTERPC`; native, non-headless smoke of `res://tests/full_game_flow_smoke_test.tscn`.
- Station result: `FAIL`; worker elapsed `158.781` s.
- Build/content/import steps passed. The native game exited 1.
- `engine-errors.log` records missing OldPcUi child nodes `Screen/App_notepad/Content/Text` and `Screen/App_notepad/Content/PreparePhotoCaption`, then a semantic-flow `NullReferenceException`. The errors repeat in the log. No `fullgame-caption-ui-png:` record was emitted, so this attempt has no UI PNG and cannot support visual acceptance.
- Snapshot source hashes verified: `FullGameFlowSmokeTest.cs` `358b369c4903d590d4cb85a9d5561982f88f0d40390e5a594f04c9dec07730d2`; `PhotoWorldState.cs` `d14e9d8eb00f0a48fae460c71481a71213a4c77c5092a203c3e7e1854f9c978f`; compiled full-game content `c1afa18be60a451d7a2a5295d8be72cba7211579c415f51e75ca2f56ae209052`. Exact source copies accompany this attempt.
- Raw `result.zip` SHA-256: `eb7a813b1b96578748d20baf9d425611f8083148ec105a4e028802ba6d7a21a9`; the duplicate remains only in ignored `.codex-captures/remote/e72c7f4c669e47c6afcd72aa6074b655/`.
- This is a preserved failed retry. Semantic/art/physical acceptance remains open.
