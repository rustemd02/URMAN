# Native wide framing diagnostic — H019 and Babai hero house

This protected native Windows capture has station status PASS for the stock `act1_demo` scene, with four static DevViewCapture control views at FOV 70. It contains a wide H019 silhouette/roof pair and an ACT1-DEPTH.9 Babai hero-house front/oblique pair. The station PASS proves the capture and protected cleanup only; the visual review is pending, and these views do not accept the house art or claim an ordinary player walk-through.

- Job: `9b3f2961f21647f39a2bc8ee772e99a7`
- Snapshot: `67f554453879c49cb1ccd9bf684bc6f976239a083907492f866806f2ad09b1dd`
- Base commit: `c01128ba543d2cf830cc25ffea1d580ca644cfd6` (the snapshot also includes recorded dirty paths; see `source-manifest.json`)
- Runtime: Godot 4.7.1, Windows, Vulkan Forward+, NVIDIA GTX 970; the actual setup is in `receipt.json`.
- Exact request and points: `request.json`; exact frame coordinates, resolved owners, render data and H019 roof census: `frames/*.json`.
- Original station logs/receipt are copied byte-for-byte. The downloaded `result.zip` container is omitted; its SHA-256 is recorded in `manifest.json`.
- Exact sealed source copies for the capture and house source binding are under `source/`; their hashes also appear in the source manifest and `manifest.json`.

`depth9_source_binding.json` links the Babai views to the existing ACT1-DEPTH.9 card and exact runtime owner path. No tracker status is changed here.
