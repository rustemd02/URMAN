# Native footstep lag probe — 2026-10-08

- Job: `2cf68876477348b5a2ed2157ba415b77`
- Snapshot: `4ba26b9d23ff8cacd45db0a5e513758b36f9c8080436e7fd069e5ab7f91df3c3`
- Station result: `FAIL`; build/import completed; userdata restoration is recorded in `stdout.log`.
- Probe result: `data_complete`; `route_complete=True`, `frame_count=241`, `elapsed_ms=12523.103`, `route_steps={"road_band": 37, "offroad": 15}`.
- Runtime gate failure: `ERROR: Can't play finished Tween, use stop() first to reset its state.` from `ExplorationFeedbackUi._Process` line 89 (two engine-error entries). This is separate from the probe output.
- The probe itself states `data route-complete; no diagnosis asserted`; no cause is accepted from this run.
- Exact probe source SHA-256 at submission: `1e7f80ad73596c8c3a8bbbfe66f7bf4e650ee92eed716a85b9d253a206b3ab55`.
- Original station `result.zip` remains in ignored `.codex-captures/remote/2cf68876477348b5a2ed2157ba415b77`; its SHA-256 is included in `manifest.json`.
