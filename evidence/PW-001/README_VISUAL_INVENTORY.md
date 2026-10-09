# VIS-002 inventory and PW-001 baseline contacts

This inventory preserves the existing 221-image source set without treating it as a current-build visual pass. The full per-image record is in [`visual_inventory.json`](visual_inventory.json); the historical contact page is [`history_contact.jpg`](history_contact.jpg). The separately captured current-build baseline is [`current_baseline_contact.png`](current_baseline_contact.png), with the capture receipt and per-frame metadata beside it.

## Coverage and classification

The inventory combines the 177 primary rows from `docs/production/ai_visual_reset_2026-10-07/_research/pack_manifest.json` and the 44 route rows from `docs/production/ai_visual_reset_2026-10-07/03_SCREENSHOT_INDEX_EXTRA_RU.md`. Stable IDs preserve the index category code and row number: E (world/exterior), M (materials), I (interiors), C (characters), U (UI), N (night/forest), V (vehicle), and R (route moments).

| Type | Count | Classification basis |
|---|---:|---|
| `runtime-historical` | 204 | Earlier runtime/gameplay images, including all 44 route moments. None is asserted to depict the current PW-001 build. |
| `concept` | 3 | M001–M003 are explicitly identified as style references/targets. |
| `diagram` | 1 | E024 is explicitly the village layout scheme. |
| `debug` | 11 | M010–M017 and M021–M023 are material/style swatches, candidate captures, matrices, or diagnostics. |
| `rejected` | 2 | E026–E027 are explicitly marked rejected/paused layout variants. |
| `runtime-current` | 0 | The 221 historical source rows contain no image from the fresh PW-001 capture; current frames are recorded separately below. |
| `unknown` | 0 | Every row has a source image and enough source/index context for one of the classifications above. |

All 221 records retain the original stable ID, source path, original filename/name, displayed inventory filename, source note, type and classification basis. The primary source filenames are preserved as indexed. Route source filenames are recovered using the existing `build_extra_route.py` selection rule (middle image from each sorted unique route prefix); the 44 recovered PNGs match R001–R044 one-to-one. Every record has `excluded_from_current_pass_pack: true`: these files remain historical inventory and cannot be counted as proof for the current PW-001 build, including the rows classified `runtime-historical`.

The existing route package was generated from `images-walk-13`; the primary and route indexes are from the prior visual-reset inventory. These frames are historical evidence, not new reference aesthetics or a substitute for fresh captures.

## Time, snapshot, and file provenance

The inventory records a capture time only when an adjacent receipt/manifest names the exact source file and supplies `captured_at*`, or when the source image contains EXIF `DateTimeOriginal`. Eight rows have such a timestamp: E041 and M012–M017 plus M021. The remaining 213 rows explicitly carry unknown capture time. File modification times and date-like folder names were not used as capture times.

No exact-source receipt identifies a project/build commit or snapshot SHA for these images, so all 221 `known_snapshot` fields remain unknown. Nine adjacent receipts do provide an image-output SHA256; all nine match the corresponding source-file hash. Those are recorded separately as image hashes and are not represented as runtime snapshots. Thirty exact-source receipt/manifests are linked from the inventory rows where available.

All **221 exact source image paths** and all **221 indexed package images** are present in this checkout. No missing source or package-image paths were found.

## Separate contact pages and current baseline

`history_contact.jpg` is the historical inventory page: 2,248 × 6,802 pixels, grouped by the eight source categories, with every ID, evidence type and a thumbnail. Its graphite background, subdued blue-gray frames and sand category headings keep attention on the evidence labels; the sheet contains only existing source images.

`current_baseline_contact.png` is the distinct runtime-baseline page: 1,920 × 450 pixels, made from only `current_station.png`, `mosque_wall_front.png`, and `mosque_wall_approach.png` from protected Windows capture job `441d83b0a1714e7d815e8c96673e6fd8`. Each source frame is 1,886 × 1,061 and has an adjacent JSON camera/profile sidecar. The exact snapshot manifest is in [`receipt.json`](receipt.json), the viewpoint definitions in [`request.json`](request.json), the engine log in [`game.log`](game.log), and the empty engine error log in [`engine-errors.log`](engine-errors.log).

The receipt records checkout HEAD `d1f693e693132a76a8aa95b68e1bab538c2ae9cd`, Godot `4.7.1.stable.mono.official.a13da4feb`, SDK `10.0.302`, renderer `Vulkan 1.3.280 - Forward+ - NVIDIA GeForce GTX 970`, scene `res://scenes/act1_demo.tscn`, profile `village-winter-frost`, 1,920 × 1,080 viewport, 0.9 render scale, and capture time `2026-10-08T19:50:30Z`–`19:56:14Z`. The exact source snapshot/manifest SHA is `64ed22b96b7e4ad87d361edbe6a7931c674f078e5efcb5e4ff5cdef6dc8964e3`; it includes the dirty-path list and SHA-256 for each captured source file. The code reference immediately before the docs-only instruction commit is `c2a43feed9b45f61b02e4ec982e63b9b6f11c893`; the commit-to-commit documentation diff SHA-256 is `c184f6bd526a38b4db3391efdac899672903d73bea9c640be01c08729bf3312b`. Do not attribute the runtime changes to that docs-only commit; the snapshot manifest identifies the actual captured source contents.

The exact runtime/content source delta from base `d1f693e693132a76a8aa95b68e1bab538c2ae9cd` is preserved in [`source-snapshot-441.patch`](source-snapshot-441.patch); its SHA-256 is `240bef60ccce8c1e0e7d775dae3d7103fcf70235d10575cce4474a234262d824`. [`source-snapshot-441-inventory.json`](source-snapshot-441-inventory.json) binds that patch to the receipt and snapshot, lists the 11 changed runtime paths and their exact hashes, and records the recovery method for the two current files changed after capture. The recovered `MosqueInterior.cs` copy and the `Addresses.cs` base copy are kept under `source-snapshot-441/game/scripts/`. The patch was applied in an isolated base-commit tree: all 11 resulting files matched the receipt byte counts and SHA-256 values, and `git apply --check` passed. All 1,819 receipt paths reconcile; the only base-tracked runtime path omitted from the station manifest is a `.blend` authoring source intentionally excluded by `eng/remote_common.py` (the runtime GLB is used instead). The live source files were left untouched.

Root reviewed the current contact page and full frames and accepted them as a reproducible runtime baseline. This is not an art/style pass for the full game, does not prove ordinary player movement or route completion, and does not show the mounted Mosque address plaque clearly. Those limitations remain explicit; the prior failed capture and its images are kept separately under [`history/ae59d933fe394be597135bf7998c94a3/`](history/ae59d933fe394be597135bf7998c94a3/). Together, this inventory and the separate current-baseline page satisfy the inventory/contact-page evidence mapped to PW-001; the broader VIS-002 successors PW-030 and PW-098 remain separate.

## One-shot validation recorded for this evidence

The generation-time and pass-boundary assertions passed:

1. Source counts are exactly 177 primary + 44 route = 221; stable IDs are unique (221/221), including every category code/row and R001–R044.
2. Route source selection reproduces the existing generator's 44 sorted-prefix/middle-frame mapping; every generated route filename matches its index row.
3. Each record has a type in the VIS-002 whitelist and the required `source_path`, `original_filename`, `capture_time`, and `known_snapshot` fields.
4. Exact source paths missing: 0. Indexed inventory-image paths missing: 0. Type counts: 204 runtime-historical, 3 concept, 1 diagram, 11 debug, 2 rejected.
5. All 221 records are excluded from the current PASS pack; no record is `runtime-current`.
6. `history_contact.jpg` is a JPEG at 2,248 × 6,802 pixels and includes all 221 historical inventory records. Root reviewed the sheet packaging and confirmed the type labels remain distinct; individual thumbnails are not style evidence.
7. `current_baseline_contact.png` is a separate PNG at 1,920 × 450 pixels and contains exactly the three current-capture frames named above; its receipt, request, frame sidecars, scene log, source snapshot and capture time were read back. Root reviewed the contact sheet and full frames and accepted the reproducible baseline only.

The checks were one-off assertions during evidence generation; no test suite or new test files were added.
