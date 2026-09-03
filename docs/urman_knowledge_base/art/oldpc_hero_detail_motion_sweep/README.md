# OldPc hero-detail motion sweep — 2026-08-14

Status: **technical PASS / production and art acceptance OPEN**.

This is a test-only first-person readability matrix for the project-original
OldPc details. It does not activate a new material, shader, interaction,
collision, save, narrative or web-runtime owner.

## Matrix and renderer

- Godot 4.7.1 .NET, Metal 4.0 / Forward+, Apple M4 Pro;
- two 1 920 × 1 080 contact sheets, each containing a 3 × 3 grid;
- columns: FOV 65° / 75° / 90°;
- rows: `bob_up` / `neutral` / `bob_down`;
- distances: `near` camera z `-2.05`, `mid` camera z `0.10`, both aimed at
  the OldPc interaction anchor;
- one isolated `OwnWorld3D` SubViewport is reused for the complete matrix;
  the readback is copied before teardown, and each tile must have a luma span
  of at least `0.08` so a cleared/blank render cannot pass.

The harness finds all four required nodes in the instantiated zone for every
matrix run:

`OldPc_DriveSlot_LOD0`, `OldPc_DriveSlot_LOD1`,
`OldPc_LabelPlate_LOD0`, `OldPc_LabelPlate_LOD1`.

The disposable clone zeros physics layers/masks and removes only
`CollisionShape3D` nodes. It does not manually dispose shared `Shape3D`
resources; Godot reference counting owns those RIDs. Production scene files
and the existing Act 3 layer-2 proxy remain unchanged.

## Captures

| Sheet | Absolute path | SHA-256 | Review |
|---|---|---|---|
| Near | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/oldpc_hero_detail_motion_sweep/godot_oldpc_near_motion_1080p.png` | `6e8f4bb3729b1b40edde9468bd5bd61476408eb7ade3726fff789312352077b9` | All nine tiles retain the CRT, drive slot and label plate at close first-person distance. |
| Mid | `/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/oldpc_hero_detail_motion_sweep/godot_oldpc_mid_motion_1080p.png` | `47fe7545dbcf2c3ca0227b760f1d211e1e1e52b1a984658fc7053f2d4489f38f` | Details remain visible but correctly read as restrained secondary marks rather than UI. |

Manifest: `oldpc_hero_detail_motion_manifest.json`, SHA-256
`a7052d3ad7bfe721ea14f69ff8784d69cb39b60423e1ce60beeaadb4915f9073`.
The manifest records all 18 camera poses and luma spans (`0.545–0.601`),
with `details_found=4` for every tile.

The wrapper `eng/capture-oldpc-hero-detail-motion.sh` validates the renderer
log, PNG signatures/dimensions, matrix shape, unique tile coordinates, hero
detail count and non-empty readback. It removes stale candidate sheets before
each run and fails closed on Godot errors, leaks or an incomplete manifest.

## Remaining gates

- motion evidence is still a contact-sheet diagnostic, not external comfort or
  observed traversal on keyboard/gamepad;
- the standard Act 3 Soviet frame and close interaction capture must remain
  compositionally consistent after future asset changes;
- mid/far repetition, final warm/dusk lighting, shared material response,
  cultural/level-art review and M1/Windows release-host performance remain
  open;
- this receipt is not an art lock and does not select a runtime texture or
  shader change.
