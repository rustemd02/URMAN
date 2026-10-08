# Native footstep contact/input probe04 — 2026-10-09

- Job `bb23b8302bce473e9f95696a58247ac3`, snapshot `293220b83178739b4f9ed2392ae99fa432b892243abae8f092b563721a743665`; station result `PASS`, non-headless native smoke `res://tests/act1_footstep_lag_probe.tscn`. Worker elapsed 331.641 s.
- Build, content, import and game steps exited 0; receipt confirms userdata restored and snapshot hashes verified. Engine error log is empty.
- The probe reports `data_complete` and `route_complete=True` over 258 frames / 13280.715 ms, with road/off-road counts `{"offroad": 15, "road_band": 37}`. Exact structured profile, including segments, step events, gaps, surface changes, timing notes and hitches, is preserved in `probe-profile.json`; a compact parsed index is `probe-summary.json`.
- The probe's concluding record says `data route-complete; no diagnosis asserted`. This is measurement evidence for legs-motion to analyze; no diagnosis, visual acceptance or task closure is asserted here.
- Exact probe source `04eb16406f09f72f695f7be2072d41cdae221a9cace90b32beea44ddc51aa6b1` (38,223 bytes) and DevViewCapture source `d3a2e6252196c56033b0fc87823ee2088596519ca8eac2fcc2267c1ea9f34996` accompany this run.
- Redundant `result.zip` SHA-256 `acf818a0ab34e4222a05587a20fc0cea5263dcaf665b33295970796b21f6ed8d` remains in ignored `.codex-captures/remote/bb23b8302bce473e9f95696a58247ac3/`, and is not duplicated here.
