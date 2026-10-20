# H049 multi-view generation — raw source candidate, remesh HOLD

The four-view generation resolved the prior single-image repetition: actual raw views show one front door, one left window, blank rear/right walls and close source proportions. Root personally reviewed all five raw views and accepted their constructive suitability as a source candidate. The one authorized remesh then introduced visible defects and is **HOLD**. Neither output is promoted to runtime; snow, scene/material fit and performance acceptance remain open. No repairs or further paid tasks followed.

## Exact input and paid chain

Four frozen geometry inputs are the parent folder's separate front/back/left/right PNGs. Their order, full hashes, sizes and roles are in `request-sanitized.json`; they match the parent `input-receipt.json`. The accepted ImageGen reference remains at `assets/source/blender/act1/h049_outbuilding_reference_v1/imagegen-reference-v1.png`, SHA256 `4511d043c56f36f87a2a3cfa9b49baf5bfeaeb03e33124dd947c8a3ad31335d9`, supplied separately as `texture_image_url`. No input image, source GLB or .blend was copied here; no new ImageGen or UV stage occurred.

Existing CLI `eng/meshy_assets.py submit-multi-image` submitted one Meshy 7.1 standard task, 4K PBR, `image_enhancement:false`, `should_remesh:false`, GLB only. Task **01a15841-9e99-7197-aa52-5ae2e9786101** succeeded; `status-04.json` confirms **consumed_credits:30**. The request/submit receipts omit base64, credentials and signed URLs.

After five raw views confirmed usable construction and 543,840 triangles, the conditional one-time remesh used that same generation task ID, triangle target 8000. Task **01a1584a-2d8b-75cc-8006-2c1e6b055329** succeeded; `remesh-status-02.json` confirms **consumed_credits:5**. No second remesh, retexture, rig, resize or model repair was submitted.

Stage actual **35/35**; H049 family actual **65/65**, including the earlier single-image HOLD's 30. Global author ledger **425/4000**. `balance-before.json` and `balance-after.json` reconcile the account; task `consumed_credits` is the charge authority, not account delta. Network escalation was used for the approved CLI requests; no account/browser/key change.

Official API references consulted: https://docs.meshy.ai/en/api/multi-image-to-3d ; https://docs.meshy.ai/en/api/remesh ; https://docs.meshy.ai/en/api/pricing .

## Unique outputs, real bounds and actual views

- `meshy-raw.glb`: **39,842,984 B**, SHA256 `1cea9044c9d124998d6ffa383d71bfb606947cc3a5263751659a5be4f825fdfa`; one mesh/node, **543,840 triangles**. At uniform height 3.10 m: **4.197423 × 2.946889 × 3.10 m**, versus canonical 4.14 × 2.8757482 × 3.10 m. Width +1.39%, depth +2.47%; not a claim of exact fit.
- `meshy-remesh-8000.glb`: **38,340,704 B**, SHA256 `179b7e3c308207a2b701216b816024ac3bc311657970145dcc9e8181d0a8a2bd`; one mesh/node `output_unwrapped`, **5538 triangles**, not an asserted 8000. At uniform height 3.10 m: **4.195628 × 2.946882 × 3.10 m**. Actual polygon count can differ from the requested target.

`inspect_candidate.py` follows the previous bounded shed inspection recipe and reuses the existing hero studio helper. It imports each actual GLB, uniformly scales in memory to height 3.10 m and centers its footprint for the studio only. No nonuniform stretch, geometry export or normalized sibling model was made. Both models' semantic front agrees with Blender −Y / glTF +Z and left with Blender −X / glTF −X; no extra yaw was needed in these actual views.

The two inspection receipts record original bounds, preview transform, and the **unapplied** source-frame transform formula. Preview puts the bottom at Z0; canonical source has bottom Z0.02 and footprint center Y−0.0071259. This difference is explicit; runtime parcel compensation, edge clamp, grounding and collision were not simulated or changed.

Personally inspected five raw and five remesh actual views: front-left, rear-right, front, left, roof-and-openings. Root also reviewed all ten. Layout survives remesh, but `remesh-rear-right.png` shows a large black triangular cutout/void appearance at the lower rear plinth corner, and `remesh-front.png` / `remesh-roof-and-openings.png` show a vertical dark slit above the door header. These defects are absent from corresponding raw views. The visible failure is confirmed; the exact boundary-hole versus inward/degenerate-face mechanism was not diagnosed or repaired. Moderate board/edge waviness is also visible. No interior, watertightness or native acceptance is asserted.

## All four material bindings, including emission

Both GLBs contain **four actual bindings**: base color, metallic/roughness, normal and emissive; one opaque double-sided material. No map was removed or altered locally. API remesh rebaked/reordered them; bytes are not identical to raw.

- Raw: base/normal 4096²; metallic/roughness/emissive 2048². Emissive texture index 3, factor `[1,1,1]`, RGB extrema 0–12/255; means approximately `[0.001773,0.001775,0.001691]`/255. 6180 of 4,194,304 pixels have nonzero luma; none exceed luma25/255.
- Remesh: all four maps 4096². Emissive texture index 0, factor `[1,1,1]`, RGB extrema 0–9/255; means approximately `[0.003248,0.003248,0.003071]`/255. 41,317 of 16,777,216 pixels have nonzero luma; none exceed luma25/255.

Emission is almost black, **not mathematically zero**. The actual imported material was preserved, and runtime glow/readability remains unaccepted. Per-role image dimensions/byte hashes and pixel measurements are in both inspection receipts. Embedded maps were not extracted to duplicate files. The final GLB remains large because embedded textures dominate; no compression/downscale variant or .blend copy was created.

## Freeze / verification / restoration

Recipe AST, every stage JSON, both downloaded GLB hashes/sizes and all ten PNG hashes/sizes were checked. Both Blender 4.5.12 LTS runs exited 0, four CPU threads. The inaccurate single-image limits text was corrected in the recipe and existing receipts without a rerun or PNG replacement. The fourth emission binding was measured from embedded bytes using Pillow, with no map files saved. Raw logs and sanitized receipts are retained. No tests, Godot, Windows, build, runtime file, shared JSON or Git operation occurred.

Both unique GLBs were closed at freeze (`lsof` found no open handles). Root may losslessly archive them to save disk; after such archival, restore the original member at its original filename before rerunning the inspection recipe. Root records archive hashes/restoration instructions separately. Neither raw nor remesh may be treated as a recreatable duplicate of the other: they are distinct paid outputs.

Finite result is frozen: raw source candidate retained, remesh HOLD; no further paid/repair/decimation/tuning work was undertaken in this stage.

## Root lossless archival after freeze

Both exact files were closed and full original hashes checked. Each ZIP below contains only the original named GLB; full decompressed stream SHA256 and size were verified before removing its unpacked duplicate. Unique paid bytes remain preserved locally outside Git. Restore the sole member to this folder at its original filename before rerunning inspection.

- `meshy-raw.glb.zip`: 34128000 B, SHA256 `509e13a61269688153ce1b26e50cd9726740720aab6d223c290c07f1013cac59`; member `meshy-raw.glb`: 39842984 B, SHA256 `1cea9044c9d124998d6ffa383d71bfb606947cc3a5263751659a5be4f825fdfa`.
- `meshy-remesh-8000.glb.zip`: 38034020 B, SHA256 `e33c87089ea22529e7ab933e44e23bf6a99fa0292a738bf8051b64dc7be2adf4`; member `meshy-remesh-8000.glb`: 38340704 B, SHA256 `179b7e3c308207a2b701216b816024ac3bc311657970145dcc9e8181d0a8a2bd`.

Freed 6,021,668 B in total. No source content, receipts, logs, views, user data or runtime assets were deleted.
