# Authored village module candidates — 2026-08-14

Status: **production candidate / art lock OPEN**.

This bounded presentation slice adds three Blender-authored Painterly Low-Poly
modules to the shared modular kit. It does not change narrative state, runtime
commands, SaveGameV3, shader code, or gameplay collision authority.

| Module | Imported prefixes | LOD0 parts | Intended placement | Collision owner |
|---|---|---:|---|---|
| WellA | `WellA_Rim`, `WellA_Water`, `WellA_PostLeft`, `WellA_PostRight`, `WellA_Header`, `WellA_Roof`, `WellA_Bucket` | 7 | Act 2 house yard | none; zone floor remains authoritative |
| WoodpileA | `WoodpileA_Log_00..03` | 4 | Act 2 house yard and Act 5 epilogue dressing | none; decorative only |
| GateA | `GateA_PostLeft`, `GateA_PostRight`, `GateA_Crossbar`, `GateA_Ribbon` | 4 | Act 5 Kara-Urman threshold | none; replaces procedural visual posts only |

The rebuilt source/derived pair is:

- `assets/source/blender/urman_modular_kit.blend`
  — SHA-256 `4f3aa74be1d34e1cd956806e56fb57e084cbbfc385380042492bac5c66d48b48`;
- `game/assets/generated/urman_modular_kit.glb`
  — SHA-256 `c9f9e8d9a036c3fc8ef20dfc736a164fb393eb8194eff0c0e55782547efa2c36`.

The Blender verifier reports 49 deterministic LOD1 meshes. Registry records
`env.well.a`, `env.woodpile.a` and `env.gate.a` point to this same source and
derived pair and are marked Project-original. The new parts use semantic
material owners (`stone`, `wood_prop`, `wood_bark`, `wood_fence`, `cloth`),
without activating any v2–v6 texture candidate as the runtime default.

The same rebuild adds two restrained `OldPc_` hero details —
`OldPc_DriveSlot` and `OldPc_LabelPlate` — as presentation-only `prop.oldpc.crt`
parts. The Godot contract is now `OldPc_ 8/8` and the exact pair names are
asserted by both the Blender verifier and `GeneratedModularKitContractSmokeTest`.
Close first-person evidence is recorded separately in
`art/oldpc_hero_detail_candidate/`; it confirms legibility in two stills but is
not an art-lock decision.

Act 2 attaches `HouseA_`, `FenceA_`, `WellA_` and `WoodpileA_` under the existing
house anchor. Act 5 boundary attaches `PineA_` and `GateA_`; the old procedural
threshold posts/board/mark are not duplicated. GateA is intentionally not
attached to the Act 5 epilogue HouseA anchor because its authored coordinates
belong to the PineA composition.

## Acceptance gates

The slice is not an art lock. Required evidence is:

1. registry preflight and rebuilt Blender verifier pass;
2. Godot full smoke sees all new prefixes, exact LOD pairs and semantic owners;
3. Act 2, Act 5 boundary and epilogue captures show no floating/duplicate
   dressing; runtime-backed `FullGameFlowSmokeTest` checks both Act 5 marker
   targets and records GateA horizontal clearance `2.687 m` with the gate
   behind both markers by at least `1.0 m`. Screen-space near/far readability
   remains a separate visual gate;
4. floor/path and existing proxy collision tests remain unchanged and pass;
5. near/mid/far traversal, 20–30 m repetition, cultural review and M1/Windows
   performance remain open production gates.
