# Native footstep lag probe retry — 2026-10-08

- Job: `e7c0cdd0340c4fe195a2a7d0e2231fc6`
- Snapshot: `0014a78480b0e0a6e1ba06361e344b3e0292510a3c82836af16f13f2781bb3fb`
- Base commit: `795ca1f9fa4b13c15b9727cd6bd03da3e8bc7e18`; station result: `PASS`.
- Probe output: `data_complete`; `route_complete=True`, `frame_count=252`, `elapsed_ms=12973.588`, `route_steps={"road_band": 37, "offroad": 13}`.
- Display: `Windows`; adapter: `NVIDIA GeForce GTX 970`; preset: `medium`. No headless flag was used.
- `engine-errors.log` is empty; receipt records `userdataRestored=true`. The earlier failed run remains preserved in its separate job directory.
- Probe output says `data route-complete; no diagnosis asserted`; this run does not establish a causal diagnosis or listening acceptance.
- The runtime source snapshot includes dirty production paths; see `receipt.json` for the exact manifest and source provenance.
