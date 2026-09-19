# BASE-003 Inspection Receipt — Single Presentation Owner / Fallback Boundary

- Date: 2026-09-03
- Commit inspected: `66684b701ad5580f3bd53fae65145621a05104cb` (branch `main`)
- Method: owner/caller source inspection only; no code edit (inspection-first per tracker).
- Verification: `./eng/verify-godot.sh` on this commit (see `evidence/act1_repo_baseline/base003-verify-godot-PASS.txt`).

## Launch chain (single canonical presentation root)

1. `game/project.godot` → main scene `res://scenes/act1_demo.tscn`.
2. `act1_demo.tscn` root carries only `game/scripts/Act1DemoRoot.cs`.
3. `Act1DemoRoot._Ready()` (`game/scripts/Act1DemoRoot.cs:98-106`) instantiates
   `res://scenes/main.tscn` once and sets:
   - `InitialZoneId = "village_day"`, `InitialSpawnPointId = "arrival"`;
   - `EnableZoneTransitionFade = true`;
   - `EnableAct1ConnectedWorld = true` (`Act1DemoRoot.cs:103`).
4. `Main._Ready()` (`game/scripts/Main.cs:66-87`) creates exactly one
   `Act1ConnectedWorld` child under `ZoneHost` when the flag is set.

## Exterior traversal owner (one)

- `Main.SwitchZone` connected-mode branch (`Main.cs:97-136`) is the only
  production path for Act I zones: it guards `Act1WorldLayout.ContainsZone`
  (rejects non-Act-I zones), resolves spawns via
  `Act1WorldLayout.TryGetWorldSpawn`, calls `_connectedWorld.SetActiveLogicalZone`,
  updates player spawn, `RuntimeBridge.SetWorldLocation` and
  `AmbientAudioDirector.SetZone`.
- Fallback per-scene loader (`Main.cs:138-180`) is reachable only when
  `EnableAct1ConnectedWorld` is false (ordinary tests / full-game entry); it is
  the documented fallback boundary, not a competing Act I path.

## Owner inventory verified

| Owner | Location | Evidence |
|---|---|---|
| Persistent connected world | one `Act1ConnectedWorld` per `Main` (`Main.cs:75-82`) | source inspection |
| Logical placements | 5 zones in `Act1WorldLayout._placements` (`village_day`, `house_old_pc`, `fap_clinic`, `zirat_road`, `kara_urman_night`) | `Act1WorldLayout.cs:41-91` |
| Connectors | 7 in `Act1WorldLayout._connectors` (`Act1WorldLayout.cs:92-137`) | source inspection |
| FAP exterior | single labeled `FapClinicAuthoredKitPresentation`; `AgentB_VillageBuildingsKit` carries `suppressedPresentationFamilies="Fap_*"` with `suppressedFapMeshCount > 0`, `HasVisibleFapMesh` must be false, `HasFapCollision(AgentB_Architecture)` must be false | `Act1DemoLaunchSmokeTest.cs:41-102` |
| Environment | `CountActiveWorldEnvironments(connectedWorld) == 1`; `AgentBEnvironment` active, `village-main-road/WorldEnvironment` and per-scene environments must be null | `Act1DemoLaunchSmokeTest.cs:60-96,107-109` |
| Canonical exterior layer | `AgentBExteriorWorld` meta `variantStatus="production-canonical"`, `retiredVariants="AgentBAct1World"` | `Act1DemoLaunchSmokeTest.cs:67-69` |
| Narrative state | single `RuntimeBridge` node in `main.tscn` (`main.tscn:17`); no scene-local narrative store found in demo chain | source inspection |
| Continuous ambience | single `AmbientAudioDirector` node in `main.tscn` (`main.tscn:31`); routed via group `ambient_audio` | source inspection |

## Verdict

`KEEP` / `VERIFY` — single Act I presentation root, single exterior traversal
owner, single FAP owner, single environment owner, single narrative/ambience
owner. No second world root, no second FAP exterior, no scene-local narrative
store. No code edit required. Reopen conditions per tracker: appearance of a
second world root / FAP exterior / ambience owner / scene-local narrative store.
