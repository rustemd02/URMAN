# Act I package manifest — scope preparation

- Date: 2026-09-04
- Observed source commit before this scope-prep: `99e041359f8f27db8f1e27d0c5c0b27e12201858`
- Status: **SCOPE-PREP PASS — not release-ready and not an RC**
- Owner: `game/export_presets.cfg` + this manifest
- Reproducible scope gate: `./eng/verify-act1-release-package.sh` (PCK-only; not an RC gate)

This is the repository-grounded boundary for a public Act I package. It does
not delete or retire Acts II–V, development campaigns, test sources, asset
provenance or long-term content. It only defines what the Act I release
presets may export. A final release still requires the release, provenance,
platform, performance, human and exact-RC gates in the production tracker.

## 1. Launch dependency boundary

The first-person Act I launch is:

```text
game/project.godot
  -> scenes/act1_demo.tscn
  -> scripts/Act1DemoRoot.cs
  -> scenes/main.tscn
  -> scripts/Main.cs + RuntimeBridge.cs + player/UI scenes
  -> Act1ConnectedWorld.Build()
  -> five Act I logical zone scenes, seven connectors and Act I presentation kits
  -> RuntimeBridge -> content/urman.chapter1.compiled.v1.json
  -> AmbientAudioDirector -> assets/audio/ambient_manifest.json -> routed stems
```

The connected-world builder instantiates these five logical zone scenes before
the first playable frame:

| Logical zone | Scene resource |
|---|---|
| `village_day` | `scenes/zones/style_benchmark_day_street.tscn` |
| `house_old_pc` | `scenes/zones/style_benchmark_house_pc.tscn` |
| `fap_clinic` | `scenes/zones/chapter1_fap_clinic.tscn` |
| `zirat_road` | `scenes/zones/chapter1_zirat_road.tscn` |
| `kara_urman_night` | `scenes/zones/style_benchmark_kara_urman_night.tscn` |

`Act1ConnectedWorld` dynamically loads the five zone Act I GLBs
`urman_village_exterior_kit`, `urman_wet_village_road_kit`,
`urman_fap_clinic_kit`, `urman_zirat_roadside_kit` and
`urman_kara_forest_edge_kit`, plus the generated village landmark kit. Its
canonical `AgentBAct1ExteriorLayer` presentation owner also loads five
runtime Agent B kits under `assets/models/agent_b_act1/` and resolves the
production `urman_winter_pine.glb` / `urman_winter_dead_tree.glb` foliage
scenes dynamically when it builds the authored Kara trees.
`GeneratedModularKitDressing` and `GeneratedCharacterKitDressing` dynamically
load the modular and character GLBs. These paths therefore cannot safely be
discovered by a selected-scenes/selected-resources export alone.

`AmbientAudioDirector`, `FootstepAudioController`, `UiFoley` and
`AudioCueUi` also resolve resources at runtime from manifest or constructed
paths. The release presets consequently use Godot's `all_resources` mode with
an explicit exclusion contract, rather than pretending that a static scene
allowlist proves all dynamic dependencies.

`Main.cs` retains full-game zone IDs for the long-term/full-game entrypoint,
but connected Act I mode rejects non-Act-I zones. Those full-game scenes and
their compiled content are not launch dependencies of `act1_demo.tscn` and are
excluded by the Act I release presets. The compiled assembly may still contain
future code symbols; this scope-prep does not rewrite the product architecture
or claim that a package scan has closed RELEASE-002.

## 2. Act I package include set

The following paths are the intended runtime package boundary, relative to the
Godot project directory `game/`.

### Launch, scene and script resources

- `scenes/act1_demo.tscn`
- `scenes/main.tscn`
- `scenes/player/first_person_player.tscn`
- `scenes/ui/audio_cue_ui.tscn`
- `scenes/ui/dialogue_ui.tscn`
- `scenes/ui/document_ui.tscn`
- `scenes/ui/journal_ui.tscn`
- `scenes/ui/old_pc_ui.tscn`
- `scenes/ui/settings_ui.tscn`
- `scripts/Act1DemoRoot.cs`
- `scripts/Act1ConnectedWorld.cs`
- `scripts/Act1WorldLayout.cs`
- `scripts/AccessibilityPresentation.cs`
- `scripts/AmbientAudioDirector.cs`
- `scripts/AudioCueUi.cs`
- `scripts/AudioSettingsService.cs`
- `scripts/CompiledCampaignRepository.cs`
- `scripts/DialogueUi.cs`
- `scripts/DocumentUi.cs`
- `scripts/FirstPersonController.cs`
- `scripts/FootstepAudioController.cs`
- `scripts/GeneratedCharacterKitDressing.cs`
- `scripts/GeneratedModularKitDressing.cs`
- `scripts/InputBindingService.cs`
- `scripts/InteractionTarget.cs`
- `scripts/JournalUi.cs`
- `scripts/Main.cs`
- `scripts/MainMenuUi.cs`
- `scripts/OldPcUi.cs`
- `scripts/PainterlyEnvironmentDetails.cs`
- `scripts/PainterlyMaterialLibrary.cs`
- `scripts/PauseMenuUi.cs`
- `scripts/QuestRuntimeCoordinator.cs`
- `scripts/RuntimeBridge.cs`
- `scripts/SettingsUi.cs`
- `scripts/StyleBenchmarkZone.cs`
- `scripts/UiFoley.cs`
- `scripts/UserSettingsStore.cs`

The code-only `MainMenuUi`, `PauseMenuUi` and their direct helpers are listed
because the Act I root instantiates them from C#; `scenes/ui/main_menu_ui.tscn`
is not loaded by the launch path and is excluded from the release presets.

### Act I world and presentation assets

- `assets/models/act1/urman_village_exterior_kit.glb`
- `assets/models/act1/urman_wet_village_road_kit.glb`
- `assets/models/act1/urman_fap_clinic_kit.glb`
- `assets/models/act1/urman_zirat_roadside_kit.glb`
- `assets/models/act1/urman_kara_forest_edge_kit.glb`
- `assets/models/act1/urman_winter_pine.glb`
- `assets/models/act1/urman_winter_dead_tree.glb`
- `assets/models/act1/urman_winter_pine_Leaf_Pine_C.png` (Godot-extracted Pine alpha mask)
- `assets/generated/urman_act1_village_landmark_kit.glb`
- `assets/generated/urman_modular_kit.glb`
- `assets/generated/urman_character_kit.glb`
- `assets/models/agent_b_act1/agentb_terrain_road_kit.glb`
- `assets/models/agent_b_act1/agentb_village_buildings_kit.glb`
- `assets/models/agent_b_act1/agentb_foliage_kit.glb`
- `assets/models/agent_b_act1/agentb_zirat_kit.glb`
- `assets/models/agent_b_act1/agentb_kara_edge_kit.glb`

The five Agent B GLBs are current runtime dependencies, not retired
experiments: `AgentBAct1ExteriorLayer` resolves each exact path from
`res://assets/models/agent_b_act1/` before extracting presentation geometry.
Their `.glb.import` files and `.godot/imported/*.scn` derivatives are required
package dependencies. No other file in that directory is a release
dependency; the verifier fails closed on an unlisted sibling or derivative.

Godot may package imported `.scn`/texture derivatives rather than the source
GLB/PNG bytes. The source paths above are the runtime resource identities; the
matching imported derivatives are dependencies of those resources and are
covered by the same export run. For the two winter GLBs, the matching
`.glb.import` files and `.godot/imported/*.scn` derivatives are required; the
Pine material additionally requires
`urman_winter_pine_Leaf_Pine_C.png.import` and its single imported `.ctex`.
The Quaternius GLTF/BIN/PNG files and project BLEND files under
`assets/source/` remain CC0 provenance and authoring inputs; they are not
runtime requirements and are not part of the required PCK mapping.

### Materials, weather and audio

- Production texture identities referenced by `PainterlyMaterialLibrary.cs`:
  `assets/textures/painterly/{weathered_wood_boards_albedo,damp_earth_albedo,aged_plaster_albedo,pine_foliage_albedo,mossy_stone_v2_albedo,old_fabric_v2_albedo}.png`
- `assets/audio/ambient_manifest.json`
- All nine manifest-routed stems: `village_day_ambience.wav`,
  `house_room_tone.wav`, `kara_urman_edge_ambience.wav`,
  `water_edge_ambience.wav`, `fap_institutional.wav`, `zirat_wind.wav`,
  `village_arrival.wav`, `village_yard.wav`, `village_return.wav`
- `assets/audio/act1/foley/*.wav` (four UI/interaction samples)
- `assets/audio/act1/footsteps/step_{wet_road,mud,grass,wood,interior_floor}_{00..02}.wav`
  (fifteen surface variants)

### Compiled Act I content

- `content/urman.chapter1.compiled.v1.json`

The source campaigns/modules under the repository root are preserved as
provenance and build inputs. They are not Godot-project resources and do not
belong in the public PCK unless a later runtime change makes them loadable
dependencies.

## 3. Explicit package exclusions

The Act I release presets in `game/export_presets.cfg` use the following
project-relative exclusion patterns. These are packaging exclusions only;
the source files remain in the repository.

| Exclusion | Reason |
|---|---|
| `tests/*` | Test scenes/scripts/capture harnesses are not runtime dependencies. |
| `scripts/experiments/*` | Agent-B experiments are not the canonical launch owner. |
| `scripts/FullGame*.cs` | Full-game presentation code is outside Act I launch. |
| `scenes/full_game.tscn` | Full-game entrypoint is not `project.godot`'s main scene. |
| `scenes/zones/fullgame/*` | Acts II–V zone scenes are not reachable in connected Act I mode. |
| `scenes/zones/fullgame_zone.tscn` | Full-game base zone is not an Act I dependency. |
| `scenes/ui/main_menu_ui.tscn` | The public Act I menu is built by `MainMenuUi.cs`; this scene is unused. |
| `content/urman.fullgame.compiled.v1.json` | The Act I bridge loads only the chapter-one compiled pack. |
| `assets/generated/urman_forest_edge_kit.glb` | Legacy/generated candidate not loaded by the canonical Act I world. |
| Unlisted `assets/models/agent_b_act1/*` siblings or `.godot/imported/agentb_*` derivatives | The five exact Agent B GLBs above are current runtime dependencies; future/unconsumed experiment files remain rejected by the verifier. |
| painterly `*_v2`…`*_v6` candidate PNGs | Candidate provenance not referenced by the production material library. |

Repository-root `content/campaigns/{dev-legacy-mainmap,dev-oldpc,urman.fullgame}/`,
`content/modules/urman-fullgame/` and `assets/retired_candidates/` are outside
the `game/` project and are therefore not traversed by these Godot presets.
They remain preserved source/provenance and are still listed here to prevent a
future project-root move from silently widening the package.

## 4. Export configuration decision

The existing `macOS` and `Windows Desktop` presets remain debug presets and
retain their historical `export_filter="all_resources"` plus console-wrapper
settings. They were not relabeled as release presets.

Two separate presets were added:

- `Act I Release macOS` — universal architecture, console wrapper disabled,
  output `../build/macos/URMAN-Act1-Release.zip`.
- `Act I Release Windows` — x86_64 embedded PCK, console wrapper disabled,
  output `../build/windows/URMAN-Act1-Release.exe`.

Both release presets use the identical exclusion contract above. The filters
are deliberately exclusion-based because the Act I runtime uses dynamic
`ResourceLoader.Load` paths for kits, audio and compiled content. A future
allowlist may replace this contract only after a resource-level verifier proves
that every dynamic dependency is explicitly included.

## 5. Evidence for the filter contract

The reproducible package-scope command is now:

```bash
./eng/verify-act1-release-package.sh
```

With no arguments it exports both named Act I presets into an exact temporary directory, parses
the Godot 4.7.1 PCK index, checks the required launch/world/audio/material
paths, requires the five exact Agent B runtime GLBs, the two winter foliage
GLBs, their imported derivatives, and the Pine alpha derivative, rejects
full-game/test/unlisted-agent/candidate/secrets/developer-cache/SDK paths,
and boots each PCK headlessly with Dummy audio. No exported binary or
receipt is written to the repository. Godot injects exactly two engine runtime
indices, `.godot/uid_cache.bin` and `.godot/global_script_class_cache.cfg`;
the verifier allows those two paths but rejects other developer-cache trees.

The minimal release-export entrypoint writes disposable PCK candidates to a
caller-owned empty directory outside the repository, then invokes the same
verifier against those exact files:

```bash
./eng/export-desktop-release.sh /private/tmp/urman-act1-release-run
```

It produces `URMAN-Act1-macOS.pck` and `URMAN-Act1-Windows.pck` using the
separate Act I release presets, with a pinned headless Godot process and Dummy
audio. The entrypoint leaves those two files for inspection and removes its
own temporary logs; the caller must remove the exact disposable output
directory after review. These are unsigned PCK payload candidates, not native
macOS/Windows release packages.

The earlier direct export evidence below remains historical scope evidence;
the wrapper is the current acceptance command and must be rerun after any
code/content/art/audio/config change:

```text
Godot --headless --audio-driver Dummy --path game \
  --export-pack "Act I Release macOS" <temp>/act1-macos.pck
Godot --headless --audio-driver Dummy --path game \
  --export-pack "Act I Release Windows" <temp>/act1-windows.pck
```

The wrapper must be rerun after any code/content/art/audio/config change. A
previous run was blocked outside this package ownership by a transient compile
error in `game/scripts/StyleBenchmarkZone.cs`; that failure is not package
evidence. The reconciliation run below records the actual current result.

Latest reconciliation run (2026-09-04):

```text
act1-release: act1-macos.pck PCK scope PASS entries=156
act1-release: act1-windows.pck PCK scope PASS entries=156
act1-release: macOS headless/Dummy bootstrap PASS
act1-release: Windows headless/Dummy bootstrap PASS
act1-release: PASS — reproducible Act I PCK scope gate only; RC/release readiness remains OPEN
```

The run required the five current `AgentBAct1ExteriorLayer` kit source
identities (`.glb.import`) and their `.godot/imported/*.scn` derivatives. The
raw repository `.glb` files remain source inputs; Godot does not emit a second
raw-GLB payload in these PCKs. The no-argument verifier removes its temporary
PCK/output directory by exit trap; the release-export entrypoint keeps only
the two caller-requested PCKs until the caller removes that exact directory.

This proves the preset filter syntax and initial dynamic world bootstrap. It
does not prove Windows/M1 host execution, a final package receipt, rights,
clean-machine behavior, full playthrough, performance, cultural approval or
release readiness. The temporary PCKs are disposable evidence and must be
recreated and hash-bound by RELEASE-003/RELEASE-009 for any real RC.

## 6. Scope-gate owner and remaining gates

The package owner now provides the minimal release export wrapper and
resource-level PCK-vs-manifest check. It must not reuse a stale debug receipt
or call this scope-prep a release build. This gate is a prerequisite for the
exact RC tasks, not an RC itself.

Remaining gates include:

- exact RC manifest/allowlist and final package-to-registry provenance;
- package-to-registry provenance, credits and rights audit;
- safe user-data/settings policy and support-visible build metadata;
- real Windows and Apple Silicon/M1 clean-machine play/performance;
- final authored audio/voice and human listening review;
- Tatar, cultural, religious and local-context sign-off;
- accessibility, motion comfort and first-time-player playtest;
- exact RC manifest, human go/no-go and final release notes.


### 2026-09-12 — winter foliage dependency gate

The existing scope gate now requires the two dynamically loaded winter GLBs and the imported Pine alpha texture. Both PCKs extracted from native R5 (`b22a62f`) passed with 281 entries and headless/Dummy bootstrap. This adds explicit dependency coverage; it does not establish Windows host execution or signing.

### 2026-09-12 menu illustration dependency

The main menu binds `assets/textures/ui/act1_menu_winter_v1.png`; package verification requires its `.png.import` plus exactly one nonempty imported `.ctex`. Original ImageGen provenance and exact prompt are in `game/assets/textures/ui/README.md`. This dependency enters the next package after R5.

### R5 → R6 save boundary

R6 removes the premature arrival grant of `tt_yul`. Its chapter campaign fingerprint is `774cf31e2d254b94e9319dfbf75fe4f97db980d52feac33d23334dceef08bbf1` (R5: `589643db90f5b18a8a8f8d986511dca9405c5f8b962f25f14fcb7a4619f9067c`). Existing R5 saves are not migrated. The menu declines incompatible Continue, retains the files, and confirms New Game; later saves may overwrite the corresponding slots. Preserve the R5 package with its saves to continue an R5 playthrough.
