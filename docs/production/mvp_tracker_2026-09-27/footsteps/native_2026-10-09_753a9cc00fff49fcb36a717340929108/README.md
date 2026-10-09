# Native footstep lag probe — 2026-10-09

- Job: `753a9cc00fff49fcb36a717340929108`
- Snapshot: `217d717334db6b2411c23f4d7460c754d23287da667e2a039e937400baa51c0a`
- Source commit: `e0095b0d7d197760521c8113278795a57e8377a8`
- Station result: `PASS`; native, non-headless smoke on `UNTERPC`.
- Scene: `res://tests/act1_footstep_lag_probe.tscn`.
- Source SHA-256: `ab7913a41f6bbee67f6108c639f3e5a108c70d9b8c3f442db9226796cd91e3dd` (29,993 bytes; worker snapshot manifest agrees).
- Probe reports `data_complete` and `route_complete=True`; 260 frames over 13277.61 ms, with route steps `{"offroad": 16, "road_band": 33}`. The probe explicitly says `data route-complete; no diagnosis asserted`; this is measurement data, not an accepted cause or task closure.
- Godot engine error log is empty. The game log includes a vehicle placement warning with recovery to checked parking; it remains visible in the preserved log. User-data guard restoration is recorded in `game.log` and the station receipt.
- Worker elapsed: 356.985 s.
- Raw `result.zip` SHA-256: `9860bd0726bc90601ea0dfdcbbf68857168da73f7889b476353bfc818224c235`; the duplicate container stays ignored under `.gitignore` and remains in ignored `.codex-captures/remote/753a9cc00fff49fcb36a717340929108/`.
