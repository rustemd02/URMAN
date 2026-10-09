# ACT1-DEPTH.9 corner-log protected capture

Three unbound native Windows DevView points captured from the existing `act1_demo` scene. Station status is PASS. Root accepted only the bounded corner material result from all three originals; whole-house style remains open.

- Job `4a1fc995a694435c80b2417eb3a985f6`, snapshot `feb3365e043d066521af496f4acffe1d823519c69fde32eef3cd1da707b4f581` from base `e05a1c69ade3dba4e86d92f400994e85656e7d85`; 1825 station candidate files, source hashes verified. Exact pre-ACK source copies and full manifest are in `source/`.
- Native Godot 4.7.1.stable.mono.official.a13da4feb, .NET 10.0.302, renderer Vulkan 1.3.280 - Forward+ - Using Device #0: NVIDIA - NVIDIA GeForce GTX 970; timeout 300s, total elapsed 351.25583789999996s; build, content, image, import and game steps all exited 0. `userdataRestored=true`; post-run doctor was ready. `engine-errors.log` and `stderr.log` are empty.
- Points are `babai_corner_left`, `babai_corner_right`, and `babai_shape_front`; FOV70. Sidecars show actual zone `village_day`, profile `village-winter-frost`, actual graphics preset `medium`, and no requested preset. These unbound points exercise only the default quality path; no high/low A/B conclusion is supported.
- Left/right sidecars contain protected `cornerLogMaterials` readback for eight surface rows each. Original sidecars and a summary are preserved under `frames/` and `frame-summary.json`. The front view is image-only.
- All original PNGs are 1886×1061 by IHDR, while sidecar viewport is 1920×1080. Original bytes are unchanged.
- Blender, registry, and local receipts are separated under `source/local-authoring-excluded/`, were not sent to the station, and are not Windows execution evidence.
- The raw result ZIP remains in ignored `.codex-captures/remote/4a1fc995a694435c80b2417eb3a985f6/result.zip`; SHA-256 is in `result-zip.sha256`.

## Root review — 2026-10-09 03:50 UTC

Root reviewed the three original PNGs. Both corner close-ups consistently distinguish the long-grain side faces from ring-cut end caps, with no cap material extending over a long face; the existing front silhouette is preserved. This is a bounded acceptance only. The windows, trim, furniture and character style remain coarse; whole-house style, exact-reference comparison, ordinary corner movement, interior AS-01/02/13, D14, budget/performance and author/human gates remain open.

The three sidecars record the actual `medium` preset, FOV 70 and `village-winter-frost`. `graphicsRequestedPreset` and `graphicsComparable` are null, so this job exercised the protected default capture path only; the matched High/Low feature in the source snapshot is not verified by this capture. `userdataRestored=true`. Original image hashes and sealed-source linkage are recorded in `root-review.json`.
