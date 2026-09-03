# OldPc hero-detail candidate — 2026-08-14

Status: **production candidate / art lock OPEN**.

This is a bounded, presentation-only close capture for two authored details in
the project-original modular kit:

- `OldPc_DriveSlot_LOD0/LOD1` — a shallow slot on the tower front;
- `OldPc_LabelPlate_LOD0/LOD1` — a restrained identification plate below the CRT glass.

The source generator keeps both parts under `prop.oldpc.crt`, `collision=none`,
and the existing OldPc layer-2 proxy remains the only gameplay collision owner.
No runtime kernel, SaveGameV3, narrative state, shader, texture default,
interaction binding or web-retirement path changed.

## Source and contract

- Blender source: `assets/source/blender/urman_modular_kit.blend`
  — SHA-256 `4f3aa74be1d34e1cd956806e56fb57e084cbbfc385380042492bac5c66d48b48`;
- Godot derived GLB: `game/assets/generated/urman_modular_kit.glb`
  — SHA-256 `c9f9e8d9a036c3fc8ef20dfc736a164fb393eb8194eff0c0e55782547efa2c36`;
- Blender verifier: `49` deterministic LOD1 meshes;
- Godot contract: `OldPc_ 8/8`, exact four new LOD names, semantic owners
  `plaster`/`shader`, zero imported physics descendants in presentation mode;
- source registry: `assets/asset_registry.json`, preflight `36/36` derived and
  `10/10` explicit local sources.

## Godot close evidence

`eng/capture-oldpc-hero-detail.sh` ran on Apple M4 Pro with Metal 4.0 / Forward+
at 1 920 × 1 080:

| Frame | SHA-256 | Review |
|---|---|---|
| `godot_oldpc_close_front_1080p.png` | `f85a5ee70a9399e633ff017392fcb582adeac64bd38b7809f0d0a5c0d4916931` | both details visible; no visible z-fight |
| `godot_oldpc_close_side_1080p.png` | `606e32a58fdef37cb90066dfc91eb9bae2913613ea7e8d1b7e499b853bcdf819` | tower slot and label remain readable from offset view |

The harness found all four required LOD nodes before each capture and reported
no Godot errors, script errors or RID/ObjectDB leak diagnostics. These stills
are evidence of candidate legibility only; they do not close motion, traversal,
20–30 m repetition, cultural review, M1/Windows performance or final art lock.

## Remaining production gates

1. Recapture the standard full-game Act 3 Soviet frame after this GLB change and
   compare against the prior frame for composition/regression.
2. Review the close details during first-person movement/head-bob at FOV 65/75/90;
   confirm the slot does not collapse into a dark bar at mid distance.
3. Check the shared OldPc material under final warm/dusk lighting and with the
   active painterly candidates; no runtime material activation is implied here.
4. Obtain cultural/level-art sign-off and release-host performance evidence.
