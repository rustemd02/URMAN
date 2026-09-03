# MAP-001 Verification Receipt — Canonical Connected Route Baseline

- Date: 2026-09-03
- Commit verified: `5539c9b` (branch `main`; code identical to `66684b7` for route files)
- Method: existing verification only; no code edit. Status `DONE — VERIFY` per tracker.
- Verification: `./eng/verify-godot.sh` exit 0 on this commit —
  `evidence/act1_repo_baseline/base003-verify-godot-PASS.txt` (same run).

## Physical walkthrough (real CharacterBody movement)

From the verify log:

```
act1-first-person-walkthrough: PASS distance=135,50m final-zone=kara_urman_night cliffhanger=completed
act1-first-person-corridor: arrival -> old PC -> FAP document -> Rinat dialogue -> evidence documents -> zirat -> Kara-Urman cliffhanger
```

The walkthrough test explicitly avoids teleport/state injection
(`game/tests/Act1FirstPersonWalkthroughSmokeTest.cs:7`).

## Canonical baseline recorded (do not drift without a proven route blocker)

5 logical placements (`game/scripts/Act1WorldLayout.cs:41-91`):

| Zone | Placement | Origin | Interior |
|---|---|---|---|
| `village_day` | `village-main-road` | `(0,0,0)` | no |
| `house_old_pc` | `babay-abi-house` | `(-28,0,0)` | yes |
| `fap_clinic` | `fap-clinic-yard` | `(28,0,-30)` | yes |
| `zirat_road` | `zirat-memory-road` | `(0,0,-70)` | no |
| `kara_urman_night` | `kara-forest-edge` | `(0,0,-115)` | no |

7 connectors (`game/scripts/Act1WorldLayout.cs:92-137`):
`arrival-to-house-yard`, `residential-road-extension`, `village-to-fap-branch`,
`house-to-zirat-return`, `zirat-to-kara-urman`, `fap-yard-loop`,
`kara-edge-approach`.

8 direct visual zones built as direct children of the core greybox
(`game/scripts/Act1ConnectedWorld.cs:805-812`) and asserted by the capture
harness (`game/tests/Act1FullRouteCoreWorldCapture.cs:248-262`):
`Arrival`, `MainStreet`, `BabaiEbiYard`, `HouseExteriorApproach`,
`ConnectiveStreetReturn`, `FapExterior`, `ZiratMemoryField`, `KaraForestEdge`.

## Verdict

Canonical route preserved: single world root, five logical zones, seven
connectors, eight direct visual zones, physical walkthrough reaches
`kara_urman_night` with completed cliffhanger. Any coordinate/spawn change from
here requires a concrete visual/traversal blocker and updated
smoke/capture expectations (tracker reopen condition).
