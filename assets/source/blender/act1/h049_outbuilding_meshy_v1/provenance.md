# H049 — one generated whole-shed candidate, HOLD

One Meshy Image-to-3D task was submitted from the root-accepted H049 material/shape reference. The task succeeded, but actual opposite-side views reveal a repeated entrance/window layout and excessive depth. This candidate is **HOLD**, not runtime or art acceptance. The conditional remesh was not submitted; no repair, retexture, rig, second generation or UV attempt followed.

## Input and exact paid chain

- Input: `assets/source/blender/act1/h049_outbuilding_reference_v1/imagegen-reference-v1.png`, 2,134,839 bytes; SHA256 `4511d043c56f36f87a2a3cfa9b49baf5bfeaeb03e33124dd947c8a3ad31335d9`. Source/ref accepted in root commit `d32e90a4`.
- Existing source/reference GLB and its separate glass remain unchanged in that reference folder. No keep-UV graft onto the former primitive model was attempted; the new model was generated as a whole shed.
- Root authorized one30-credit generation and a conditional5-credit remesh, ceiling35. No other paid task was permitted.
- Client: existing `eng/meshy_assets.py submit-image <exact-input-png> --spend-credits 30`; Meshy7.1 standard, 4K PBR textures, no Ultra, no automatic remesh, GLB only. Exact sanitized payload and client SHA are in `request-sanitized.json`; data URI/API key are omitted.
- Task: `01a15823-02c4-765e-87a6-df6e30a748d4`, SUCCEEDED. `status-02.json` reports **consumed_credits30** directly. Stage actual30, global actual360→390, unused reserve5. `submit.json` and `submit.raw.log` retain the single actual submission outcome; `status-01.json` retains the initial in-progress charged response.
- Official API/pricing consulted on2026-10-20: https://docs.meshy.ai/en/api/image-to-3d ; https://docs.meshy.ai/en/api/remesh ; https://docs.meshy.ai/en/api/pricing . No Meshy webapp/browser/account operation.
- Initial sandbox balance GET had URLError→gaierror errno8, no HTTP response. Network escalation resolved it; beforebalance3920 (monthly2770/permanent1150). `balance-after.json` is a supporting account reconciliation, not the charge authority. No key, signed model URL or API body is logged.

## Unique returned model and actual inspection

- Raw unique output: `meshy-raw.glb`, 53,269,376 bytes; SHA256 `62285cd95ce86c5ad7c6c80d3ab0cdff7563605f49e8aef12a7890047690731f`. `download-raw.json` is the sanitized actual download receipt.
- Geometry: one mesh/node, **940,754 triangles**. One opaque double-sided PBR material. Embedded basecolor4096², normal4096², packed metallic/roughness2048²; maps are retained only inside the unique returned GLB, not duplicated on disk. Glass appearance is baked into this opaque material; there is no separate generated glass part.
- `inspect_candidate.py` reuses the existing hero studio helper without modifying it. It imports the actual GLB and renders five1440×1080 views. No model re-export, .blend copy, runtime file or normalized candidate binary was created. Four CPU threads, Blender4.5.12LTS; single log `inspect-raw.raw.log`.
- Display normalization was in-memory **uniform** scale to3.10m height and footprint centering. The original asset was not warped to fit the old collision envelope.
- All five images were personally inspected: `raw-front-left.png`, `raw-rear-right.png`, `raw-front.png`, `raw-left.png`, `raw-roof-and-openings.png`.
- View filenames name camera axes, **not semantic source facades**: `raw-front.png` faces a window side, `raw-left.png` faces a door gable. A90° yaw would align the generated gable direction with the original front axis; no corrective transform/export was applied.
- At matched3.10m height, the aligned generated dimensions are approximately **4.073906m wide ×4.794279m deep ×3.10m high**, against the original4.14×2.8757482×3.10m. Width−1.60%, depth+66.72%. This is a substantial proportion mismatch, not a claimed metric-fit pass.
- The two opposite three-quarter renders each show a door on the opposite gable and a window on the opposite side. The original H049 has one front door and one left window. The single-image model repeated both features around the back; do not mistake a plausible-looking individual angle for correct whole-object layout.
- Wood/roof/stone/dim-pane appearances are visually differentiated; exterior walls/plinth/gabled roof look like a complete supported volume in these views. No explorable interior, hidden construction, target budget, gameplay collision, movement/performance or native style acceptance is claimed. The repeated openings and depth already fail this exact candidate before optimization.

## Freeze and verification

`raw-inspection.json` retains geometry/image/material facts, five view hashes, source bounds, actual dimensions, the HOLD review and task charge authority. Python AST parse, JSON parse, downloaded GLB SHA/size and all five rendered-image hashes were checked. No tests, build, Godot or Windows were run. No shared JSON, runtime file, Git operation or source/reference folder was changed. No duplicate map extraction, archive or re-export was created; the unique paid returned GLB, recipe, raw log, receipts and five views are preserved.

Next action is root review of this finite HOLD result and release of the unused5-credit remesh reserve. No further paid operation is authorized by this receipt.

After the frozen review, root preserved the raw GLB losslessly in local `meshy-raw.glb.zip` (44,292,632 bytes; SHA256 `db3e52b8846acc63b53776e5ff21879d5f1d86c469078494c138b292f19dfd9c`). The sole member `meshy-raw.glb` decompresses to 53,269,376 bytes and the original SHA256 `62285cd95ce86c5ad7c6c80d3ab0cdff7563605f49e8aef12a7890047690731f`; full stream comparison passed before removing the uncompressed duplicate. Restore that member before rerunning the inspection recipe. The archive remains local, outside runtime and Git.
