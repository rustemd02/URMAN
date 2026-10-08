# H019 roof native capture — 2026-10-09

- Job `b4c77e61a0394fc38926c8da3ed21105`, snapshot `76a7aa1051096130604ac6bf74555b9aa021477272aad0fe53df79d88c24a181`; station status `PASS`.
- Native, non-headless capture of the two previously approved H019 approach/front views on res://scenes/act1_demo.tscn; worker elapsed 357.359 s. Userdata was restored, and the station receipt confirms snapshot hashes.
- The receipt confirms source commit `e0095b0d7d197760521c8113278795a57e8377a8` and full-game compiled-content SHA `c1afa18be60a451d7a2a5295d8be72cba7211579c415f51e75ca2f56ae209052`. Exact `Act1ConnectedWorld.cs` bytes accompany this archive with SHA `b35a5e71a8aabe129bff7fe9d833303ff2496f68faf6f82b08e3ed44fcd5509a`.
- Both frame sidecars census a single visible H019 root and one visible, shadow-casting instance of each mapped roof mesh. The reported world AABB upper Y is 4.740632 for `DwellingFacade_Roof_LOD0` and 4.910777 for `DwellingFacade_RoofSnow_LOD0`. This runtime census does not by itself establish the visual result.
- Engine error log is empty. PNGs and original sidecars are preserved. Root reviewed both original PNGs and rejected the roof silhouette as unchanged; the source-culling fix is not accepted as effective. This is separate from station protocol PASS. Details are in `root_review.json`.
- Redundant `result.zip` SHA-256 `eeef7f4ef9d67a10b874a342b1b7ea761901c2ac9bf943d52da5ded8c149c35a` remains in ignored `.codex-captures/remote/b4c77e61a0394fc38926c8da3ed21105/` and is not duplicated here.
