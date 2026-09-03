# Act I Package Include/Exclude Manifest (BASE-004)

- Date: 2026-09-03
- Commit: `e50c64a` (branch `main`)
- Status: decision manifest for the Act I public MVP package. This document
  fixes what belongs in an Act I release package and what must stay out while
  being preserved in the repository as provenance. It is the acceptance
  reference for RELEASE-001/RELEASE-003; the actual PCK/resource comparison is
  NEW VERIFICATION REQUIRED until the release preset exists (debug export
  currently uses `export_filter="all_resources"` with empty include/exclude
  filters in both macOS and Windows presets — `game/export_presets.cfg:9-12,
  50-53`).

## Include in Act I release package (runtime dependencies of the launch path)

Authority chain: `game/project.godot` main scene → `game/scenes/act1_demo.tscn`
→ `Act1DemoRoot.cs` → `game/scenes/main.tscn` → `Act1ConnectedWorld` +
`Act1WorldLayout` + zone scenes.

- Launch/host scenes and scripts: `game/scenes/act1_demo.tscn`,
  `game/scenes/main.tscn`, `game/scenes/player/first_person_player.tscn`,
  `game/scripts/**` used by that chain (`Act1DemoRoot`, `Main`,
  `Act1ConnectedWorld`, `Act1WorldLayout`, `StyleBenchmarkZone`,
  `RuntimeBridge`, `QuestRuntimeCoordinator`, `FirstPersonController`,
  `InteractionTarget`, `PainterlyMaterialLibrary`, `AmbientAudioDirector`,
  `AudioCueUi`, `OldPcUi`, `DialogueUi`, `JournalUi`, `DocumentUi`,
  `SettingsUi`, `AccessibilityPresentation`, `InputBindingService` and their
  direct helpers), UI scenes under `game/scenes/ui/`.
- Logical zone scenes: `game/scenes/zones/style_benchmark_day_street.tscn`,
  `style_benchmark_house_pc.tscn`, `chapter1_fap_clinic.tscn`,
  `chapter1_zirat_road.tscn`, `style_benchmark_kara_urman_night.tscn`.
- Authored Act I kits: `game/assets/models/act1/*.glb` (+ `.import`).
- Shared kit GLBs actually loaded by the connected world (e.g.
  `assets/generated/urman_act1_village_landmark_kit.glb`,
  `assets/generated/urman_modular_kit.glb`,
  `game/assets/generated/urman_character_kit.glb`) — final list to be
  diffed against the compiled pack/loader references at RELEASE-003.
- Production textures referenced by `PainterlyMaterialLibrary.cs`
  (`weathered_wood_boards_albedo.png`, `damp_earth_albedo.png`,
  `aged_plaster_albedo.png`, `pine_foliage_albedo.png`,
  `mossy_stone_v2_albedo.png`, `old_fabric_v2_albedo.png`).
- Audio: `game/assets/audio/ambient_manifest.json` + Act I stems
  (`village_day_ambience.wav`, `house_room_tone.wav`,
  `kara_urman_edge_ambience.wav`); `water_edge_ambience.wav` only if the
  manifest still routes a fullgame zone (remove with the routing entry).
- Content: `game/content/urman.chapter1.compiled.v1.json` (generated from
  `content/campaigns/urman.chapter1/` + `content/modules/urman-chapter1/` +
  `content/modules/urman-oldpc/`; the authored sources stay in the repo and
  are not needed inside the package unless the runtime loads them directly —
  verify at RELEASE-003).

## Exclude from Act I release package (preserve in repo, never delete)

- Full-game campaign content: `content/campaigns/urman.fullgame/`.
- Compiled fullgame pack: `game/content/urman.fullgame.compiled.v1.json`
  (the Act I launch reads only `urman.chapter1.compiled.v1.json`).
- Dev/legacy campaigns: `content/campaigns/dev-legacy-mainmap/`,
  `content/campaigns/dev-oldpc/`.
- Full-game scenes: `game/scenes/full_game.tscn`,
  `game/scenes/zones/fullgame/` (all `fullgame_*` zone scenes),
  `game/scenes/zones/fullgame_zone.tscn`.
- Any compiled fullgame packs (e.g. fullgame compiled content outputs) — the
  Act I launch must not depend on them and the release package must not ship
  them.
- Candidate texture provenance: painterly `*_v3..v6` candidate PNGs not
  referenced by `PainterlyMaterialLibrary.cs` (candidate-provenance entries in
  `assets/asset_registry.json`).
- Retired candidates: `assets/retired_candidates/` (if present at packaging
  time).
- Test-only scenes/scripts under `game/tests/` (except any runtime-loaded
  dependency — none known; verify at RELEASE-003).
- Web/runtime-parity sources and retired extracted variants.

## Enforcement boundary

- No repository deletion is authorized by this manifest. Exclusion is
  packaging-scope only (release preset allowlist / include-exclude filters).
- The debug export presets must not be relabeled as release; RELEASE-001 owns
  the separate release configuration, RELEASE-003 owns the clean-build
  allowlist file, and the first executable check is the RELEASE-003 PCK vs
  allowlist comparison on an exact RC.
- Reopen this manifest if the launch path gains a new runtime dependency or if
  a packaged asset cannot show registry provenance.
