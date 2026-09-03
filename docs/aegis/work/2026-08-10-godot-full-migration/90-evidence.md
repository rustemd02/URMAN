# Evidence Bundle Draft — Godot migration

Date: 2026-08-10
Status: partial

## Baseline evidence

| Requirement | Evidence | Result |
|---|---|---|
| Portable content baseline | `npm run content:check` | PASS: 4 modules, 4 campaigns |
| Content/runtime unit baseline | `npm run test:unit` | PASS: 68 content, 102 runtime |
| Owner boundary baseline | `npm run test:architecture` | PASS: 2 tests |
| Browser production build | `npm run build` | PASS: 56 modules |
| Current product behavior | `npm run test:e2e` with approved loopback | PASS: 10 tests |

## Interpretation

Этот bundle начался как parity-oracle baseline, но теперь содержит частичное доказательство C# compiler/runtime, Godot vertical slice, full-game authored flow, ассетов и структурных desktop exports. Он всё ещё не является release acceptance: art lock, authored voice/mix, cultural/accessibility review, реальные M1/Windows host runs, полный 6–8-часовой playthrough и web cutover остаются открыты.

## Documentation lock evidence

- Accepted owner records: `decision_log.md`, `technical_architecture.md`, `narrative.md`, `canon.md`.
- Accepted style record and budgets: `design_style.md`.
- Concept targets copied under `docs/urman_knowledge_base/art/style_refs/` with hashes and non-screenshot warning.
- Cross-owner synchronization: gameplay, assets, backlog, roadmap, playtest, mindmap, open questions and weak points.
- `git diff --check` — PASS.

## C# content parity evidence

- Frozen JS golden: `tests-dotnet/fixtures/content/urman.chapter1.compiled.v1.json`, 118534 bytes, file SHA-256 `94bd83a861f2e86b1c26476427d5f7ce01eb81ca4abdb15f016dd2cdf48419b3`.
- C# compiler output is structurally equal to the golden pack.
- Campaign fingerprint matches exactly: `59ef1e6134daa23f6177de88e57d72b0a18b3d84434a36f4a2c827a3097163fd`.
- Module fingerprints match exactly for `urman.chapter1`, `urman.core` and `urman.oldpc`.
- `JsonSchema.Net` validates the compiled wire pack, including every registry definition.
- Content CLI commands available: `validate`, `compile`, `inspect`, `simulate`, `report`.

## Runtime and persistence evidence

- Runtime kernel tests cover atomic state/event commits, occurrence idempotency/conflict, exclusive resource claims, rollback and snapshot restore.
- Deterministic logical clock and owner-scoped xorshift RNG roundtrip through snapshots.
- SaveGameV3 test proves atomic primary/backup rotation and recovery after corrupting the newest primary file.
- Deterministic scheduler tests prove stable due-tick/job-id ordering, owner-scoped cancellation, lease/retry/ack semantics and snapshot restore without persisting transient leases.
- SaveGameV3 codec roundtrips a non-empty scheduler snapshot and restores its jobs as claimable work.
- Custody tests prove that conflicting item claims reject the full transaction before effects, without partial ownership changes.
- Evidence provenance tests prove deterministic ancestry chains and reject duplicate, self-referential and cyclic links.
- Capability host tests cover exact protocol/version resolution, lifecycle, state capture/restore and incompatible snapshot rejection.
- Shared narrative-state tests cover scene, knowledge, vocabulary, journal, dialogue and quest commands through one runtime kernel.
- Content rule tests cover nested conditions, authored knowledge/vocabulary/pressure/route/scene/audio/document-open/journal-record effects, sequential staging and atomic rejection.
- Quest lifecycle parity tests cover parallel and optional objectives, `all`/`threshold` composition, start/completion conditions, stage/quest checkpoints, retry limits, cancellation, stable occurrence IDs, capability requests and owner cleanup within one kernel transaction.
- Resolver parity tests cover campaign module order, duplicate logical IDs, deterministic asset variants, host URL adaptation, locale fallback, semantic vocabulary segments, stable missing-reference origins and accessibility closure against the real Chapter 1 pack.
- Audio resolver test proves that a meaningful audio asset exposes captions, transcript and a non-audio cue with the same outcome key; decorative audio remains nullable by contract.
- Godot dialogue text is resolved through `Urman.Content.Resolvers.TextResolver`; the previous local Russian/default selection path was removed from `CompiledCampaignRepository`.
- Quest capability orchestration tests prove state-driven create/start, snapshot restore before start, rollback of sessions created during a failed reconciliation and refusal to tear down a session while its runtime resource claim remains active.
- `RuntimeBridge` reconciles quest-owned sessions after committed lifecycle/refresh commands and restores them from `SaveGameV3`; the old-PC global session remains composition-owned.
- C# tests: Core 38/38; Content 12/12, including the audio production readiness report against all four campaigns and the physical ambient manifest.
- Execution backlog validation — PASS: `docs/urman_knowledge_base/execution_backlog.json` parses as JSON; 7 milestones, 22 unique task IDs and all dependency/milestone references resolve. The queue records `GODOT-003` as the current in-progress focus, `NARR-001` as completed with the Acts 2–5 narrative lock, `ASSET-005` as completed for the non-destructive texture-candidate pass, `ASSET-006` as completed for host-independent asset provenance, `NARR-002` as blocked on external cultural review and `REL-004` as blocked until release acceptance.
- Full-game campaign pack is compiled from `urman.core`, `urman.chapter1`, `urman.oldpc` and `urman.fullgame`; `inspect` reports 46 authored beats and `simulate` reproduces 46 deterministic events with state hash `2af5bf7f21c387c9514d3dd1d6cc2854849fe7ee61a9d26521605f2c76b8f6d9`.

## Godot and asset evidence

- `dotnet build Urman.slnx --no-restore ...` — PASS: 6 projects, 0 warnings/errors.
- Godot headless main boot — PASS with first-person controller and runtime bridge.
- Scene smoke — PASS for player, day street, house/old-PC, Kara-Urman night and main.
- Scene smoke also loads the physical FAP and zirat-road scenes.
- Zone-flow smoke — PASS for compact-scene replacement while the player/runtime persist.
- Chapter 1 end-to-end smoke — PASS for all 16 authored narrative points across `village_day -> house_old_pc -> fap_clinic -> house_old_pc -> zirat_road -> kara_urman_night`; interaction availability follows active source scenes, and the final runtime state contains `clue_do_not_answer_rule=confirmed` plus `cliffhanger-hard-cut=completed`.
- Old-PC smoke — PASS for the real compiled-content chain `official notice -> internal register -> authored contradiction quest completed -> saved message unlocked`; opening a document updates shared knowledge, quest state and beat state through kernel commands.
- Journal smoke — PASS for `old PC open -> explicit save -> one data-driven journal entry`; opening alone does not add an entry, repeated saves do not duplicate it, and `JournalUi` resolves the Russian title/body from compiled content while owning no progression state.
- Player settings smoke — PASS: `GameSettingsSnapshot` values, portable keyboard/gamepad button/axis bindings and `AccessibilitySettingsSnapshot` survive the adapters, remapped interaction and movement controls can be captured and restored, reduced motion overrides head bob, keyboard/gamepad events switch the hint between `[E]` and `[A]`, and the low preset applies 0.75 3D scale with MSAA disabled. Scene smoke loads the modal `SettingsUi` with reduced-motion, contrast, text-scale, subtitle and audio-description controls.
- Fresh integrated Godot persistence — PASS after the `InputBindings` contract: `SaveGameV3` is written to and restored from `user://`, with player settings/remapping path intact; atomic corrupt-primary recovery remains covered by C# store tests. The smoke now frees its real `Main` scene and painterly material cache before quitting, so the verbose run ends without ObjectDB/RID leak diagnostics.
- Narrative transition smoke — PASS for compiled `entryConditions/onEnter/onExit`, active-scene interaction ownership and `arrival -> house -> crossroad`.
- Dialogue smoke — PASS for Gulsina vocabulary effects and Rinat's condition-gated authored choice; NPC state and choice history commit in the same kernel transaction.
- Chapter 1 smoke resolves the final logical audio request through `AudioResolver`; because the authored voice file is still absent, `AudioCueUi` presents the equivalent Russian cue «Ринат говорит: „Не отвечай“» with the same outcome key.
- Persistence smoke — PASS for atomic `user://` write/load, player transform and loaded 3D-zone restoration.
- Full-game flow smoke — PASS: `full_game.tscn` loads the compiled `urman.fullgame` entrypoint (Acts 2–5), traverses 46 authored beats through 12 compact walkable zones, and verifies `act5_pact_unjust`, `act5_protection_lost`, `act5_truth_price` and `canonical-tragic-ending` in one kernel state. The C# `.tscn` export seam is serialized with PascalCase property names so scene-specific campaign/zone config is applied deterministically.
- Full-game document flow — PASS: four `targetDocumentId` interactions are materialized beside the Soviet folder, Tukay notebook, 1552 map and pact ledger. Each opens compiled Markdown through `RuntimeBridge.OpenDocumentAsync`, updates `presentation.openedDocumentIds`, and records an idempotent shared-journal entry; `DocumentUi` is loaded in both main and full-game composition scenes.
- Full-game presentation pass — PASS: `Main` resolves 12 authored zone `PackedScene` wrappers, `FullGameZoneDressing` adds distinct modular compositions keyed by the logical zone ID, `GeneratedModularKitDressing` loads selective project-original environment `.glb` families for the Act 2 house, Act 3 old-PC/table and Act 5 forest boundary, `GeneratedCharacterKitDressing` selects nine project-original character prefixes with face landmarks/layered clothing, LOD0 `0–18m` / LOD1 `14–48m` and a Godot `AnimationPlayer` containing the matching `Idle`/`Tension` clips, while `FullGameNpcDressing` selects `Tension` for the pact/boundary threat presentation and `Idle` elsewhere. `FullGameInteractionLayout` places every authored interaction beside a zone prop anchor, and `FullGameFlowSmokeTest` requires each traversed wrapper to contain `ProductionDressing`, `NpcPresentation` and `authored-zone-anchor` metadata while preserving physical compiled interactions. The same smoke asserts environment LOD0 `0–24m`, environment LOD1 `18–72m`, character self-fade, zone-aware animation playback, layer-2 provisional proxy colliders, no character collision objects and the layer-1 interaction-ray boundary. `CollisionQaSmokeTest` passes queryable layer-1 floors and layer-2 proxy isolation in all 12 zones. `./eng/capture-fullgame-frames.sh` stores six 1920×1080 representative renders under `art/fullgame_frames/`; hashes and the production-progress/non-art-lock verdict are recorded in that directory's README.
- Ambient audio technical foundation — PASS: `tools/audio/generate_ambient_audio.py` generates four deterministic project-original 24 kHz WAV stems; `game/assets/audio/ambient_manifest.json` gives each routed zone exactly one stem and `assets/asset_registry.json` records source/license/budget metadata. Current stem hashes are village `07f6ad…1791a7`, house `2b661f…5dd3`, Kara-Urman `24245a…e88a`, water `5fd8af…c3d1`; a second generator run reproduced them byte-for-byte. `AmbientAudioDirector` owns only presentation, switches the `AudioStreamPlayer` on desktop and validates imported streams without starting native playback in headless mode. `AmbientAudioSmokeTest` covers manifest closure, WAV import, metadata and switching; the full `./eng/verify-godot.sh` run exits without ObjectDB/resource-leak diagnostics. This is technical ambience/runtime evidence only; Marat/Rinat voice, final mix, caption copy and cultural listening review remain open.
- Audio production readiness report — PASS: the built `Urman.ContentCli report --root .` compiles all four campaigns and enumerates eight campaign-level occurrences of the two non-decorative voice assets. It confirms caption closure `8/8`, transcript closure `8/8`, `0` authored voice files, `8` intentional `logical-ref` placeholders, `0` unexpected missing physical audio files and `4/4` physical ambient manifest files; authoring status remains `OPEN` by design. `AudioProductionReporterTests` passes as part of the 12-test Content suite. This report makes the production boundary auditable and does not claim final voice, mix or cultural approval.
- Godot debug export — PASS with an explicit `game/Urman.Game.sln`: the export log contains successful .NET publish stages and no `ERROR:`/`SCRIPT ERROR:` in either Godot's captured console log or its dedicated `--log-file`. The export script rejects Godot's misleading zero exit code when either log contains an export error.
- Fresh macOS debug export — PASS after the tapered foliage-tier source pass: universal arm64+x86_64 debug ZIP SHA-256 `04af0c2856897700a62902c4641d6031d30c2cb66e666facb10b3cee529e45d4`; the archive contains `URMAN.pck`, both architecture-specific `Urman.Game.dll` payloads and the four ambient WAV stems/manifest. The integrated `verify-desktop-artifacts.sh` checks the extracted Mach-O/PCK payload and routed audio markers. Execution on a real macOS release machine remains a separate gate.
- Fresh Windows debug export — PASS structurally after the tapered foliage-tier source pass: ZIP SHA-256 `f7d7081b772335c2e43bf3b439752d20459d54c5e5b0285b28c60d452c2c3fd3`, PE32+ GUI x86-64 `URMAN.exe` SHA-256 `4a22ac9959c6814b8128a80c6a7dbb79d51b5afa1f74faecf7e8327e1caa805a` with adjacent self-contained .NET runtime, `Urman.Game.dll` and ambient assets. The integrated `verify-desktop-artifacts.sh` checks the extracted PE32+ payload and package layout; export logs contain no `ERROR:`/`SCRIPT ERROR:`. Execution on a real Windows host remains pending.
- Desktop artifact verifier slice — PASS: `sh -n eng/verify-desktop-artifacts.sh eng/export-desktop-debug.sh`, `./eng/verify-desktop-artifacts.sh` and `git diff --check`; the verifier extracts both archives, checks the universal Mach-O and PE32+ payloads, both macOS .NET payloads, the Windows `Urman.Game.dll`, the macOS PCK, the Windows embedded PCK and the four routed ambience markers in both payloads. This is structural packaging evidence, not host execution or release signing evidence.
- macOS host smoke — PASS: `./eng/verify-macos-host.sh` extracts the macOS debug ZIP SHA-256 `04af0c2856897700a62902c4641d6031d30c2cb66e666facb10b3cee529e45d4`, verifies the universal arm64/x86_64 Mach-O, launches the embedded PCK/.NET payload on the current macOS host with the default Forward+ renderer and Dummy audio driver, observes `zone-loaded: village_day@arrival` and the first-person bootstrap marker, and exits cleanly without `ERROR:`, `SCRIPT ERROR:` or leak diagnostics. This closes only the current macOS host smoke; Windows execution, M1/Windows performance, signing/notarization and full acceptance remain open.
- Superseding desktop export (2026-08-12) — PASS: `./eng/export-desktop-debug.sh` rebuilt both debug packages after the bounded stone/fabric owner pass. macOS universal ZIP SHA-256 `07a414a10ff5a134d53d30eaefd4313f465cd213cdddb7ec9c91ad6c59b657f5`; Windows ZIP SHA-256 `a1dc0747fd93ca8af7f4309d0471e1dc65361eedb196da77253a0b738a5175b5`; Windows PE32+ executable SHA-256 `090d5f8c840655ed1263783bebfe0f2f3214e43aea34aa1fc2f33a474cc2a75c`. `./eng/verify-desktop-artifacts.sh` passes, confirming universal arm64/x86_64 Mach-O, PE32+, self-contained .NET payloads, embedded PCK, six candidate textures, ambient manifest and four routed WAV stems. This is structural packaging evidence; Windows host execution, signing/notarization and release-hardware performance remain open.
- Superseding macOS host smoke (2026-08-12) — PASS: `./eng/verify-macos-host.sh` consumed the fresh ZIP `07a414a10ff5a134d53d30eaefd4313f465cd213cdddb7ec9c91ad6c59b657f5`, verified the universal binary, launched the embedded PCK/.NET payload with Forward+ and Dummy audio, observed `zone-loaded: village_day@arrival` plus the first-person bootstrap marker, and exited without `ERROR:`, `SCRIPT ERROR:` or leak diagnostics. This closes only the current macOS host smoke; Windows execution, M1/Windows performance, signing/notarization and full acceptance remain open.
- Current desktop export (2026-08-12) — PASS: `./eng/export-desktop-debug.sh` rebuilt both debug packages after the bounded OldPc tower/panel/power-button detail pass. macOS universal ZIP SHA-256 `43f2bc1142da195347ddea687f71bbf111b8642af7a0f62982bc7c6d026bda64`; Windows ZIP SHA-256 `8a9fb972ef92c47b10bd133c20a1a702d4c392bffd0d589142909eccb2caf3c2`; Windows PE32+ executable SHA-256 `efa290d42a62d700b403bbcef525660e554d880a5c46c0ddc8e44a859e9bc46f`. `./eng/verify-desktop-artifacts.sh` passes, confirming universal arm64/x86_64 Mach-O, PE32+, self-contained .NET payloads, embedded PCK, six candidate textures, ambient manifest and four routed WAV stems. This is structural packaging evidence; Windows host execution, signing/notarization and release-hardware performance remain open.
- Current macOS host smoke (2026-08-12) — PASS: `./eng/verify-macos-host.sh` consumed the current ZIP `43f2bc1142da195347ddea687f71bbf111b8642af7a0f62982bc7c6d026bda64`, verified the universal binary, launched the embedded PCK/.NET payload with Forward+ and Dummy audio, observed `zone-loaded: village_day@arrival` plus the first-person bootstrap marker, and exited without `ERROR:`, `SCRIPT ERROR:` or leak diagnostics. This closes only the current macOS host smoke; Windows execution, M1/Windows performance, signing/notarization and full acceptance remain open.
- Superseding HouseA facade asset/capture receipt (2026-08-12) — PASS as production evidence, art lock OPEN: Blender source `b6ac99408e352427bc27aad05feddce8c635b095139442b65dd8b271f45e5148`, derived GLB `9abb68bea5cfd19ca48438447abc9e37a0eb67ec61be881055de4484f1544918`, 32 environment LOD1 meshes and 143 character pairs pass the escalated Blender verifier. Godot captures were regenerated after import: static day/house/Kara `d64b9e17ecd29b900a53fdea3cbc65fb5fbee0b7d19d5eaecdcc477e6be6e0df` / `433a976d0d51832c42afcfc2acdf42e1f5314a826066829e03e82e3dbae6ff93` / `6fe1c7b9f383f75cd06e5063dd313ff7c59d0d63da2226bc4988d43704b39512`; spatial sweep `025bd7b27c8b02601d82374211e3f0c3a468c408a4b938578e8da46087177440` / `2ed24bc38d3168a448cae6faca05c473fbcf6221b1e89835176b515b3ce5f411` / `72e312e5cb90c1606d26fa01b663250bb6d74093e3bace225850224a6b9b228f`; temporal sweep `c2c0a18849ceeb29407cd4dd6df0b284dfc39a7cad374d144aae82b5810ed380` / `50307aeb82c52d1f1dd7a913e0199839631495a216ffec12450b860a47b82049` / `b5d0141497236c1cf6038ccffe65aa113d6cc522b1af387254d3ebcfe1713c0f`; full-game six-frame hashes are recorded in `art/fullgame_frames/README.md`. No new gameplay collider was introduced; the existing provisional layer-2 owner remains. Visual art, mesh collision, cultural and release-hardware gates remain open.
- Superseding asset/performance/package receipt (2026-08-12) — PASS: local M4 Pro benchmark reports day `125.59 FPS / 7.962 ms avg / 13.840 ms p95`, house `144.95 / 6.899 / 7.546`, Kara `144.99 / 6.897 / 7.599`, all above the internal 30 FPS floor. `./eng/export-desktop-debug.sh`, `./eng/verify-desktop-artifacts.sh` and `./eng/verify-macos-host.sh` pass; macOS ZIP SHA `7c6d475e95e2bb9474238cc5ac143c3ac052a82763e8471dfa2d87cc3a606d9f`, Windows ZIP SHA `151264f76bbff77834e99face5cec4b58d15083948806b45d07ce0d3d780df0c`, Windows executable SHA `be10ab9e915773e1627f109b68a8935c9750e8692a2ffbb7a17750e34c2b09c3`. Windows host, M1 performance, signing and final acceptance remain open.
- Blender 4.5.12 generators produced the environment `assets/source/blender/urman_modular_kit.blend` and `game/assets/generated/urman_modular_kit.glb`, plus project-original character source `assets/source/blender/urman_character_kit.blend` and `game/assets/generated/urman_character_kit.glb`; Godot imported both GLBs without errors.
- Generated environment asset hashes after the authored edge-break/layered-crown pass: blend `8750d0153b92d1f858ef5b4aeb9e923933a514d275444724d13081546f41642c`; GLB `f051cc0f1715141106c6c5eb08114f8a82508b15b8b54fba4417d0abc4d8c3cf`. Generated character asset hashes: blend `1439bbba4d972b062b8295a77a2d19f58840c847e9c1f929a44f735f27663dd3`; GLB `cb5aca34ece44b8adfa8a8240302dc35d3f0627ed74f2f5044bc017d1975ffd5`.
- Blender LOD/animation smoke — PASS: `./eng/verify-assets.sh` reopens the environment `.blend` (32 `_LOD1` meshes, including the bounded HouseA facade pass) and character `.blend` (143 matching LOD0/LOD1 meshes with nine anchors, face landmarks, layered clothing and nine `Idle`/`Tension` action pairs), validates deterministic policies and parses `assets/asset_registry.json`. Godot full-game and collision QA smoke prove scene visibility ranges, imported `AnimationPlayer` playback/switching, queryable floors, isolated provisional layer-2 proxies and that imported render meshes do not become active physics; final authored facial expression, mesh-collision quality and release-hardware performance remain open.
- `./eng/capture-style-frames.sh` — PASS with fresh Godot editor import and Metal/Forward+ capture after the imported `HouseA_`/`PineA_` module, authored edge-break/layered-crown pass and tapered foliage-tier pass; three 1 920 × 1 080 images are stored under `docs/urman_knowledge_base/art/style_frames/`. `StyleFrameCapture` now fails closed unless the daytime scene reports `HouseA_project_original` and the Kara-Urman scene reports `PineA_project_original`. Fresh hashes: day street `34ee8df886b79c4fed9b61f4710e8554a1b3aa6e139e382af2d6794f9b89c927`, house/PC `6f22dfb6313eade035795303058d9113f3818fd8cc0c91d4e0fefd5825af2f46`, forest edge `cabc1722744217ee9b0ef8372fa32ee5f8159905fb067a14cbc5874bb187ca55`. The imported modules share the full-game Godot LOD/collision adapter and do not alter narrative or walkability ownership; visual review still rejects final art lock because the forest crown family, hero PC/domestic specificity, complete authored environment family and cultural presentation are not accepted.
- Godot style camera-sweep evidence — PASS as spatial evidence, art lock OPEN: `./eng/capture-style-motion-sweep.sh` builds cleanly and renders 27 real Metal/Forward+ 640×360 cells (near/mid/far × FOV 65°/75°/90°) into three 1 920×1 080 RGBA contact sheets. Fresh hashes: day `094347ccfb6bb51598327e77978d3bf98b7014b9558319f6994f285f37fa08b2`, house `11966645066a4e2c9a360ea767a1ddc658be8692c8c962d83439ed6e8f9b5364`, Kara `c7bb59d6622b140b993bf8234c9b1c012ac4641f33e5059502a58ab5a37ba981`, manifest `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`. The harness normalizes SubViewport readback to RGBA8, stores repo-relative output paths and fails closed on missing imported modules, blank frames or Godot/leak diagnostics. Spatial evidence confirms old-PC focal read and foreground/midground forest separation; temporal motion/head-bob comfort, v2 runtime activation, greybox geometry, fog/light calibration, cultural review and release-hardware performance remain open.
- Painterly texture production-candidate pass — PASS/OPEN, non-destructive. Six new 1 024 × 1 024 RGB PNGs (`weathered_wood_boards_v2`, `aged_plaster_v2`, `damp_earth_v2`, `pine_foliage_v2`, `mossy_stone_v2`, `old_fabric_v2`) are recorded with SHA-256, ImageGen prompts/source paths and project-generated provenance in `docs/urman_knowledge_base/art/texture_candidates_generation.md`, `assets/asset_registry.json` and the texture README. The deterministic image gate passes all six; four candidates reuse existing PainterlyMaterialLibrary mappings and appear in-memory in the three mandatory Godot scenes (wood 232 meshes, plaster 15, earth 4, pine 956). Mossy stone and old fabric remain explicitly OPEN and swatch-only because no runtime material/scale owner exists. Metal/Forward+ candidate captures are reproducible: day `f79bfcdacedf4dce5e820ca3cec38848a8ab2583057f005554302a6f10530238`, house `a1f172ecb15936beb7453ece888ab54efd63ce1f189dfea1e1be39329b0486b`, Kara `682bd25de131f9514a574963a53f415f19ec621a53842a258ddb21bfe3d2c51c`, swatches `9a81ec755af5f6c1d4e29da0a3eb377726faa4113dc515bb818eea6e3b1bf4e9`. Candidate frames are evidence only; v1 runtime textures remain active and no art lock is declared.
- Four project-bound ImageGen albedo textures are resized to 1 024 px, registered with project-generated provenance and consumed by the Godot painterly material shader. The visual review rejected this pass as final art lock; capture reproducibility is proven, art acceptance is not.
- Superseding texture-owner update (2026-08-12): the six-candidate image/material-owner/capture gate is now PASS, with explicit v2-only `stone` and `fabric` presentation owners and Metal/Forward+ coverage wood 232, plaster 15, earth 4, foliage 956, stone 2 and fabric 3. Fresh candidate capture hashes are day `3dcded7baa81c89bfc90c46cf3f7d23ceb8572b7cee8f9d2f8814f2a5c750e03`, house `f51b281ba494b356472f6582ce0f9ef6c111e0c20c49747d7c5ea15395f6d9a9`, Kara `95c3606bd0f999276ead5482f3444fc9c5dbe92ee4d96ad5b2ac675504c39edb`, swatches `c033da9f64ba36ed3654ccbc44fde41fa3ce1610d1bd37624c08c3e11fbaf978`. The preceding owner-open sentence is historical evidence from the pre-owner run; art lock remains open.
- Focused v3 texture candidate pass (2026-08-13) — PASS/OPEN, non-destructive. `weathered_wood_boards_v3_albedo.png` (SHA `65372413ff490da9dce831fd5f4022d6f804265b42dd9895ad4b13a6fa196093`), `aged_plaster_v3_albedo.png` (SHA `bb5f6c13d1947ef5616e9dac49ff37b41e6ff323901cc0b44b783212ac21454e`) and `damp_earth_v3_albedo.png` (SHA `cd031be6ff3fbb2750a05abe6b667d23bba2ad3705b6f5a14755d21a33b77b20`) have exact prompts/source paths, final 1 024 × 1 024 RGB files and image-gate metrics in `art/texture_candidates_generation.md` and `art/texture_candidates_qa.md`. `./eng/capture-texture-v3-frames.sh` rendered all three in the day street, old-PC house and Kara-Urman edge scenes on Metal/Forward+ at 1 920 × 1 080, with fresh frame hashes in `art/texture_candidate_frames/v3/README.md`; coverage is wood 246, plaster 15 and earth 4 presentation meshes. The helper uses temporary material clones only; runtime v1/v2 ownership, shader, narrative state, saves and collisions are unchanged. Near/mid/far motion, 20–30 m repetition, authored geometry, fog/light calibration, cultural review and final art lock remain open.
- Focused v3 spatial motion sweep — PASS as clean spatial evidence, art lock OPEN: `./eng/capture-texture-v3-motion-sweep.sh` rendered 27 real Metal/Forward+ cells (near/mid/far × FOV 65°/75°/90°) into three 1 920 × 1 080 contact sheets. After the road-relief + puddle candidate pass, current four-candidate hashes are day `7e74b816c210eeeb91fcc1f438ddae604814529c5bffb9059ce618fd8ed13ffd`, house `ff9ae14a4277ad21c321774900a1b1423dc09f61295ed5c04f95168f703548dd`, Kara `bf4be9e46314a118fcf8c7d7ca92566d034448b322212552b1c5766ce9b7ab3b`, manifest `b94d55d48f15fde9b44a3997f1203e57b8e94a23d4c10b80d04f7573273044a9`. Candidate substitutions are test-only; no runtime, save or narrative state changed. Spatial evidence leaves temporal comfort, 20–30 m no-repeat review, production wet specular/material dressing, fog/light calibration, cultural review and final art lock open.
- Pine foliage v3 candidate — image gate PASS / Godot capture OPEN: `pine_foliage_v3_albedo.png` has exact ImageGen source/prompt, final SHA `05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830`, and deterministic seam/color metrics in the generation, technical and QA manifests. It is intentionally not yet substituted into runtime or the Kara-Urman capture; no shader, save, collision or narrative state changed.
- Pine foliage v3 motion receipt — PASS/OPEN, non-destructive: the focused v3 sweep now substitutes the four existing wood/plaster/earth/pine candidates in memory and renders 27 real Metal/Forward+ cells. Current hashes are day `7e74b816c210eeeb91fcc1f438ddae604814529c5bffb9059ce618fd8ed13ffd`, house `ff9ae14a4277ad21c321774900a1b1423dc09f61295ed5c04f95168f703548dd`, Kara `bf4be9e46314a118fcf8c7d7ca92566d034448b322212552b1c5766ce9b7ab3b`, manifest `b94d55d48f15fde9b44a3997f1203e57b8e94a23d4c10b80d04f7573273044a9`. Pine adds modest near/mid value breakup and puddle geometry adds ground silhouettes; fog, conifer silhouette/branch geometry, wet specular/roughness and final art acceptance remain open.
- Superseding style recapture (2026-08-12): after the bounded stone/fabric owner pass, fresh mandatory style frames hash to day `2792727dd2b6751a09460af436d589ac8aaf52a3fd9be78050c629ed7b088b62d`, house `7027b66e27d834aa681f834817f4af173c4e90f43cb0593711e5b7f122b0d97d`, forest `fe952ceb6a3ee42d309ffc6b35fe2ff8f8593a20ca5c2e7d60b1a2073bf56c4c`; the older style-frame hashes above are retained as historical pre-owner evidence. The art lock remains rejected for greybox geometry, forest crown family, hero PC, fog/light and cultural review.
- Superseding style sweep (2026-08-12): the 27-cell near/mid/far × FOV 65°/75°/90° Metal/Forward+ contact sheets now hash to day `c58e535c7bfdeacd67ec1bc115d7c0d69e32a0444f94a471e4e738a81fe4f228`, house `753645b72bebfe8564c5b98c29da4282e5e55c4bb8e032a7277ce793f39a3120`, Kara `5bad3328efc95c19088a98aa842cd498893e1dd5aa55e416c2722467d03fc264`; the manifest hash remains `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`. This is spatial evidence only; global v2 activation, temporal comfort, geometry, lighting, culture and art lock remain open.
- Baseline style sweep regression (2026-08-13) — PASS: after the road-relief + puddle candidate pass, `./eng/capture-style-motion-sweep.sh` rendered all 27 baseline Metal/Forward+ cells with no Godot/leak diagnostics. Current contact-sheet hashes are day `a1a5d1cfc3be447d96c0ee90497e35fc5023b40f3dae2c0ce7625b7fff09d276`, house `2ed24bc38d3168a448cae6faca05c473fbcf6221b1e89835176b515b3ce5f411`, Kara `9eed6ee096e50baba6c2a55640d27a8a5815d70c73696be2ea8e7ae1e8426c9a`; manifest remains `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`. The rerun is a regression receipt; it does not close the art gates.
- The current source pass adds a procedural sky, world-space painterly material sampling, tapered seven-sided pine crowns with deterministic side boughs, undergrowth, lived-in street props, CRT glow, boundary dressing, stronger house/PC composition, two imported project-original environment modules and an authored edge-break/layered-crown rebuild. The latest Blender/GLB contract has 24 deterministic LOD1 meshes after a bounded OldPc tower/panel/power-button detail pass. It compiles, loads headlessly and has fresh rendered evidence, but visual review still rejects the frames as final art lock: house is not hero-asset quality, and forest detail is still a provisional broad-silhouette family. No final art acceptance claim is made.
- Local performance baseline — PASS on Apple M4 Pro: `./eng/benchmark-godot.sh` after the bounded stone/fabric owner pass records `day_street=118.65 FPS` (p95 12.497 ms), `house_old_pc=144.92 FPS` (p95 7.559 ms), `kara_urman_edge=144.83 FPS` (p95 7.580 ms), all above the internal 30 FPS low-preset floor. This is not M1/Windows evidence and does not close the release performance gate.
- Fresh `graphify update .` — PASS after the OldPc geometry and temporal cleanup slice: 6 257 nodes, 8 577 edges, 495 communities. `graph.html` is intentionally omitted by Graphify because the graph exceeds its 5 000-node HTML threshold.
- Release-gate matrix — PASS as a documentation boundary: `docs/urman_knowledge_base/release_gate_matrix.md` records fresh technical PASS evidence and separates it from open temporal/art, authored audio, cultural, accessibility, M1/Windows, full-playthrough and web-retirement gates. It does not grant release or art-lock authority.
- Temporal style sweep — PASS as technical evidence: `./eng/capture-style-temporal-sweep.sh` rendered 216 real Metal/Forward+ samples across three mandatory scenes, FOV 65°/75°/90° and `head_bob_on`/`reduced_motion`. Superseding hashes are day `a3605f65e79b13572cd37bab7080eca39f5ad763143592c14bd51c5d3d6c9153`, house `dc90fffeeeff27608278662c06658145d065b0662becf29c08ab138a8b2447ff`, Kara `178e49784efebe1d569409ddfa841ac5c6327c1dc0a5ea6f3d6ee80bfb5aa7e9`, manifest `7d59945b92c20c72f9e864147fc509a7aa4ce0e8d032eaf059fcd5f299795ee1`. The run has no Godot/RID leak diagnostics; external motion-comfort and art-lock acceptance remain open.
- Superseding temporal capture (2026-08-13): after reproducing an 842-Shape3D-RID leak in the first render-only run, `StyleTemporalComfortCapture` now strips `CollisionObject3D` nodes from disposable scene clones and awaits viewport release. The road-relief + puddle regression passes with no `ERROR`, `SCRIPT ERROR` or RID leak diagnostics; hashes are day `2ce042f53eaabfe8934d64ec9ef98b461643699098e1ddcb371d4338e3c28ba9`, house `50307aeb82c52d1f1dd7a913e0199839631495a216ffec12450b860a47b82049`, Kara `e72ec5caf383300510abb6079ff401806f09bcde7bc03ff69bc3bf45209c011a`, manifest `41bdd8c9624ee1e153b74b234b288e8004a3418e4abac41d6327fa66d85d16d6`. This remains technical evidence; external motion comfort and art lock stay open.
- Superseding full-game recapture (2026-08-12): after the bounded OldPc detail pass, six Metal/Forward+ production-progress frames hash to act2 house `abb6a78462f7ab692f138231eea1164e378c79fce36e372f4ea302a48f3af40a`, river `136ab5f48708ce455e3ca35f6dd73e6052c55aa75d41c28cc72751716ebeecad`, archive `a23bd2d9039e74a1c86f149b20657abc34d3886fa8577023a525cdf082ec4947`, Soviet `aea4578716cf242ed4bfd5be4b0e3b8dd1e9fa220b42d6bd4d2fa5ab5f87edd7`, pact `eaef16790d67fa6f59b421a0db7986a460c5418f453c3ba47bd2b66c1762c55f`, boundary `2ac2bbd26e3d1d2a0dd0bdfbe46aaf19f040d3bb09d86cf7f9d9a36fcb0b16fd`. The detail is production-progress only; collision, cultural and art-lock gates remain open.
- Current verification note — targeted `./eng/verify-dotnet.sh`, escalated `./eng/verify-assets.sh`, `./eng/capture-texture-candidate-frames.sh`, `./eng/capture-style-motion-sweep.sh`, JSON parsing and shell syntax checks pass. The previous intermittent `zone_flow_smoke_test.tscn` cleanup leak under the full `--log-file` harness is repaired at the test-only owner: `PainterlyMaterialLibrary.ClearCacheForHeadlessTests` now clears managed ShaderMaterial/ImageTexture wrappers before native shutdown. Two consecutive full `./eng/verify-godot.sh` runs now pass all scene, audio, zone, narrative, persistence, collision, Chapter 1 and full-game smoke tests with no ObjectDB/RID diagnostics. Runtime, narrative state and saves are unchanged.
- Web retirement preflight — `./eng/check-web-retirement.sh --report` is implemented and non-destructive. It currently reports the expected legacy `src/`, Vite/Three.js/Node commands and browser-persistence references; strict `--assert-absent` remains intentionally unpassed until desktop host acceptance, cultural/accessibility review and the complete 6–8-hour playthrough. No browser data is deleted.
- Authored road-relief + puddle production-candidate slice (2026-08-13) — PASS/OPEN: `PainterlyEnvironmentDetails.AddRoadRelief` replaces the flat road/path slabs in the three mandatory style benchmarks, Chapter 1 Zirat road and shared Acts 2–5 `FullGameZone` paths with deterministic 9×28 low-poly relief surfaces and 216 matching collision cells per path. `AddPuddleCluster` adds bounded low-poly wet silhouettes to day/Kara/Zirat presentation, with an explicit `OPEN-roughness-review` gate; shader and runtime material ownership remain unchanged. `RoadReliefQaSmokeTest` passes seven relief paths and three day-road puddle clusters; extended `CollisionQaSmokeTest` passes the path contract across all 12 full-game zones; `./eng/capture-style-frames.sh`, `./eng/capture-style-motion-sweep.sh`, `./eng/capture-texture-v3-motion-sweep.sh` and `./eng/capture-style-temporal-sweep.sh` pass on real Metal/Forward+ with refreshed hashes in their README files. Local M4 Pro benchmark remains above 30 FPS (`119.24/116.11/137.55 FPS`, day/house/Kara). The slice is a production candidate, not art lock: wet specular/roughness, authored village-module variety, final road dressing, cultural review, M1/Windows performance and full traversal acceptance remain open. No narrative state, save behavior or web-retirement owner changed.
- Final graphify receipt after the road-relief QA slice (2026-08-13) — PASS: AST topology rebuilt to 6 322 nodes, 8 652 edges and 495 communities; `graph.html` remains intentionally omitted because the graph exceeds the 5 000-node visualization threshold. `graphify-out/graph.json` and `GRAPH_REPORT.md` are current. Documentation/image changes remain recorded in their scoped art/evidence READMEs.
- Final graphify receipt after the puddle candidate slice (2026-08-13) — PASS: AST topology rebuilt to 6 324 nodes, 8 656 edges and 491 communities; `graph.html` remains intentionally omitted because the graph exceeds the 5 000-node visualization threshold. `graphify-out/graph.json` and `GRAPH_REPORT.md` are current. Documentation/image changes remain recorded in their scoped art/evidence READMEs.
- Superseding six-material v3 texture receipt (2026-08-14) — PASS/OPEN, non-destructive: the same six 1 024×1 024 RGB candidates and 12/12 image gate remain unchanged. After the bounded puddle-origin correction and render-only collision-owner cleanup, the real Metal/Forward+ 27-cell near/mid/far × FOV 65/75/90 sweep passes with sheets `4f2d73913fe3fa2c60195fb50423bd68a0ae8718c6efe3a6d25d58bd78c866ff`, `e7faa6af1b4a5aa87bdecad82892eed57323f99fb6db94a57bf01a73df9fed4c`, `ed53e8e8b2103830dd0cdb93c2b11d94c59ddba0a55b57ab8930e7392d904637` and manifest `d9aebe7bb1d04e169ccbc934da223fbf832bd6197324978f717b8118cfed8ee9`. It records replacement totals wood 6 057, plaster 288 and Kara foliage/stone/fabric 4 707 across the 27 cells. The render-only harness strips collision owners while retaining presentation meshes and exits without Godot/RID diagnostics. Candidates remain test-only; 20–30 m no-repeat, temporal comfort, geometry/material-variety, wetness/roughness, lighting/fog, cultural review, release hardware and final art lock remain open.
- Current post-capture verification receipt (2026-08-13) — PASS: `./eng/capture-style-motion-sweep.sh` reproduced the baseline 27-cell Metal/Forward+ sweep with hashes day `a1a5d1cfc3be447d96c0ee90497e35fc5023b40f3dae2c0ce7625b7fff09d276`, house `2ed24bc38d3168a448cae6faca05c473fbcf6221b1e89835176b515b3ce5f411`, Kara `9eed6ee096e50baba6c2a55640d27a8a5815d70c73696be2ea8e7ae1e8426c9a`; road-relief presentation meshes remain visible and no ObjectDB/RID/resource-leak diagnostics were emitted. `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1`, `./eng/verify-painterly-textures.sh` (12/12), `./eng/verify-godot.sh`, `git diff --check` and relevant `sh -n` checks pass. `graphify update .` rebuilt 6 330 nodes, 8 667 edges and 492 communities; HTML remains intentionally omitted over the 5 000-node visualization threshold. This receipt validates the test-only v3 harness changes; it does not close runtime material activation, art, cultural or release gates.
- Wetness candidate receipt (2026-08-14, superseding the fail-closed run above) — PASS contact / OPEN acceptance: `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1`, `./eng/capture-wetness-candidate.sh`, `./eng/verify-painterly-textures.sh` (12/12), `./eng/verify-godot.sh`, `./eng/capture-style-frames.sh`, `./eng/capture-style-motion-sweep.sh`, `./eng/capture-style-temporal-sweep.sh`, `./eng/capture-texture-v3-motion-sweep.sh`, `git diff --check`, `sh -n` checks and `graphify update .` are required fresh checks. The dedicated Metal/Forward+ harness now instantiates scenes one at a time and requires exact relief-body instance ownership. It verifies 5/5 clusters and 15/15 patches within ±0.010 m, source runtime roughness `0.90`, exactly 15 in-memory candidate clones at `0.50`, and writes three candidate frames. Current wetness receipt hashes: manifest `c0a27568a2af31ca70c8c35141b1f71ff59f0161e2723310a27581010d89a0f9`; day `e3c8686f0155cdc71d5bc81ef22c935ab9fa55c5ec51e36d47ee787ef6c6157a`; Kara `1de94c9146a7249e881a4d7bae4e9794559b77c110f85d4ff3dbb1787af3368f`; Zirat `4800db71418c8c648b2c4eb37f01e48c2b9e87cd6bff0b888c0d7187e5213350`. Style recapture hashes are day `f77e80abb1d86843fe1f57724be534d65731a0abf0e1120003146f5c48301038`, house `433a976d0d51832c42afcfc2acdf42e1f5314a826066829e03e82e3dbae6ff93`, Kara `bdbf0343ddfda352ba145a766a7ab1646e3fb6a87202e2a5be5f8cf024782495`; 27-cell sweep hashes are day `022647535c839bb568378b5b3015d8d01af63d7bc8576435680056399cb1ca0d`, house `2ed24bc38d3168a448cae6faca05c473fbcf6221b1e89835176b515b3ce5f411`, Kara `e1aa92cd5003eb9e548350c41500bcea656bde3560b047b370060c474fccdce5`, manifest `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`. Temporal hashes are day `a3605f65e79b13572cd37bab7080eca39f5ad763143592c14bd51c5d3d6c9153`, house `dc90fffeeeff27608278662c06658145d065b0662becf29c08ab138a8b2447ff`, Kara `178e49784efebe1d569409ddfa841ac5c6327c1dc0a5ea6f3d6ee80bfb5aa7e9`, manifest `7d59945b92c20c72f9e864147fc509a7aa4ce0e8d032eaf059fcd5f299795ee1`. V3 texture motion hashes are day `4f2d73913fe3fa2c60195fb50423bd68a0ae8718c6efe3a6d25d58bd78c866ff`, house `e7faa6af1b4a5aa87bdecad82892eed57323f99fb6db94a57bf01a73df9fed4c`, Kara `ed53e8e8b2103830dd0cdb93c2b11d94c59ddba0a55b57ab8930e7392d904637`, manifest `d9aebe7bb1d04e169ccbc934da223fbf832bd6197324978f717b8118cfed8ee9`. The bounded benchmark-origin corrections are `PuddleFar 0.102→0.092` and `BoundaryWetPatch 0.105→0.078`; shader, runtime material, collision, SaveGameV3 and narrative owners remain unchanged. This is candidate evidence only; roughness/specular, cultural review and art lock remain open. Graphify current report is 6 398 nodes / 8 754 edges / 502 communities; HTML remains intentionally omitted.

## Focused v4 texture evidence (2026-08-14)

- Two new non-destructive ImageGen candidates are recorded with exact prompts,
  source/final hashes and normalization details in
  `docs/urman_knowledge_base/art/texture_candidates_generation.md` and
  `assets/asset_registry.json`: `weathered_wood_boards_v4_albedo.png`
  (`5203ad6e37e5a3328b8aef2dc8c1aa00a5c3e83896be7286663c8e640c7af25a`) and
  `damp_earth_v4_albedo.png`
  (`f1ed53e8b40f94fc45a2e8e43816e5c77e1717da0be09a60dcc68045da1bad31`). Both
  are 1 024 × 1 024 RGB PNGs; existing v1/v2/v3 files were not overwritten.
- `./eng/verify-painterly-textures.sh` discovers 14/14 v2/v3/v4 candidates
  and passes dimensions, PNG integrity, seam, clipping and saturation gates.
  The v4 per-file metrics are in
  `docs/urman_knowledge_base/art/texture_candidates_qa.md`.
- Real Godot 4.7.1 .NET Forward+/Metal still captures cover day street, house
  with old PC, Kara-Urman edge and a two-material swatch. The 27-cell spatial
  sweep covers near/mid/far × FOV 65°/75°/90°; previews and SHA-256 receipts are
  in `art/texture_candidate_frames/v4/README.md` and
  `art/texture_candidate_motion_sweep_v4/README.md`. Current motion manifest
  SHA-256 is `27c7a6713ea02f38b8ac074957c59d1f40f4909818d08649b010cf990ea41b43`.
  No Godot, script, ObjectDB, resource-leak or RID diagnostics were emitted.
- Independent visual audit supersedes the initial candidate ranking: both v4
  replacements are **HOLD/REWORK**. Earth has stronger standalone broad value
  grouping, but the in-engine delta is small (day v3→v4 RGB MAD approximately
  `0.47/0.43/0.27`) and its dark rut/stone marks can double authored relief and
  puddle geometry. Wood's calmer palette is offset by dark continuous seams,
  knots and regular triplanar striping across a shared owner. Runtime v1 remains
  active; no art lock is claimed.
- Test-only harness changes are limited to versioned candidate discovery,
  in-memory material substitution and capture scenes/scripts. No runtime
  material registry, shader, gameplay, saves, collisions or narrative state was
  changed by this texture pass.
- Final graphify update after the first-person smoke/docs pass — PASS: 6 447
  nodes, 8 807 edges, 503 communities. `graph.html` remains intentionally omitted by
  the 5 000-node visualization limit; `graph.json` and `GRAPH_REPORT.md` are
  current.

- First-person interaction wiring receipt (2026-08-14) — PASS as internal
  smoke: `FirstPersonInteractionSmokeTest` runs the production camera ray and
  keyboard action path against the physical HouseDoor and old-PC targets. It
  records `arrival -> house_old_pc` through `E`, opens the existing modal
  `OldPcUi`, then releases the scene cleanly. `.tools/dotnet/dotnet build
  game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity
  minimal -nodeReuse:false -m:1` and the elevated `./eng/verify-godot.sh` pass
  with zero build warnings and no ObjectDB/RID/leak diagnostics. This closes
  only local ray/input wiring; gamepad parity, first-time usability, motion
  comfort and external playtest remain open.

- Focused v5 earth/wood rework receipt (2026-08-14) — technical PASS / production
  OPEN, non-destructive. `damp_earth_v5_albedo.png` SHA
  `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108` and
  `weathered_wood_boards_v5_albedo.png` SHA
  `3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70` are
  1 024 × 1 024 8-bit RGB/sRGB PNGs. `./eng/verify-painterly-textures.sh`
  passes 16/16 candidates; v5 seam mean V/H is earth `0.0083/0.0088` and
  wood `0.0147/0.0205`, with clipping `0/0`. The isolated real Metal/Forward+
  v3↔v5 A/B harness writes 9 non-black sheets and verifies 120/120 relief/contact
  samples; manifest SHA is `3ced8de9eeee32ac99bef4e7f7c1633e3bb7083d21503a572260ac9051d69b63`.
  The harness is technical isolation evidence only: earth relief-only cells do
  not yet show a reliable rendered v3→v5 delta, and wood still needs multi-owner
  facade/fence/furniture/end-grain review. Runtime v1 materials, shader, saves,
  collisions and narrative state remain unchanged; no art lock is declared.

- Final graphify receipt after the v5 texture/documentation sync — superseded by
  the final harness receipt below; the current AST topology is 6 607 nodes,
  8 990 edges and 519 communities. `graph.html`
  remains intentionally omitted over the 5 000-node visualization threshold;
  `graphify-out/graph.json` and `GRAPH_REPORT.md` are current.

- Superseding v3↔v5 A/B receipt after dynamic-version manifest correction —
  PASS/OPEN: `URMAN_TEXTURE_AB_VERSIONS=v3,v5 ./eng/capture-texture-ab-diagnostic.sh`
  writes 9 Metal/Forward+ sheets and verifies 120/120 isolated relief/contact
  samples in `art/texture_candidate_ab_diagnostic_v5/`. Manifest SHA is
  `41cc4702fbca356b7a8e2035251205fc576ebd6f822dec834c159a25f7fbc9e0`; the
  test-only harness, runtime materials, shader, saves and narrative state are
  unchanged. Earth visual separation and wood production acceptance remain OPEN.

- Final Godot regression after the dynamic-version A/B harness patch — PASS:
  `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore
  --disable-build-servers --verbosity minimal -nodeReuse:false -m:1` reports
  0 warnings and 0 errors, and elevated `./eng/verify-godot.sh` completes the
  import, scene, audio, zone, narrative, UI, persistence, collision, Chapter 1,
  full-game and first-person interaction smoke suites with no `ERROR:`,
  `SCRIPT ERROR:`, ObjectDB or RID/resource-leak diagnostics.

- Final graphify receipt after the dynamic-version harness sync — PASS: AST
  topology rebuilt to 6 607 nodes, 8 990 edges and 519 communities. `graph.html`
  remains intentionally omitted over the 5 000-node visualization threshold;
  `graphify-out/graph.json` and `GRAPH_REPORT.md` are current.

- Latest v5 motion-sweep/documentation graphify receipt — PASS: AST topology is
  now 6 634 nodes, 9 014 edges and 519 communities; `graph.html` remains
  intentionally omitted over the 5 000-node visualization threshold. The
  dedicated v5 receipt is under
  `art/texture_candidate_motion_sweep_v5/` with three 1 920 × 1 080 sheets,
  27 near/mid/far × FOV 65°/75°/90° cells per scene and manifest SHA
  `003c0be712333fa0aaee5b1a596b119768a09cd0e2cd83b8b3376d014fea4053`.
  This is technical spatial evidence only; no runtime texture activation or
  art lock follows from it.

- Post-sweep Godot regression — PASS: the .NET build reports 0 warnings/errors,
  and elevated `./eng/verify-godot.sh` completes import, scene, audio, zone,
  narrative, UI, persistence, collision, Chapter 1, full-game and first-person
  interaction smoke suites with no `ERROR:`, `SCRIPT ERROR:`, ObjectDB or
  RID/resource-leak diagnostics. The v5/v6 motion and A/B harnesses remain
  test-only.

- Focused v6 earth/wood rework — technical PASS / production OPEN: the new
  1 024² RGB/sRGB files pass the complete 18/18 v2–v6 image gate. The isolated
  v5↔v6 A/B receipt under `art/texture_candidate_ab_diagnostic_v6/` has 9
  sheets and 120/120 relief/contact samples (manifest SHA
  `db978e19b72451d9548bafa323448fdf2d9d9ece64a4e2360f2f5e8cba751043`). The
  dedicated v6 motion receipt under `art/texture_candidate_motion_sweep_v6/`
  has three 1 920 × 1 080 sheets with 27 near/mid/far × FOV cells per scene;
  manifest SHA `e7d52a52fd8022c7b99b842c7d0a376b87886b88ff945e761f3748a8bab4cd5b`.
  Earth relief-only separation is still subtle and wood needs owner/orientation
  review, so v6 is not activated and no art lock is declared.

- Latest graphify after the v6 test-only harness/verifier changes — PASS: AST
  topology is 6 721 nodes, 9 094 edges and 519 communities. `graph.html` is
  intentionally omitted over the 5 000-node visualization threshold;
  `graphify-out/graph.json` and `GRAPH_REPORT.md` are current.

- Latest graphify after ASSET-006, StyleBenchmarkZone calibration and the
  documentation sync — PASS: AST topology rebuilt to 6 730 nodes, 9 102 edges
  and 530 communities. `graph.html` remains intentionally omitted over the
  5 000-node visualization threshold; `graphify-out/graph.json` and
  `GRAPH_REPORT.md` are current.

- Current Blender asset-verifier rerun — OPEN / host-toolchain blocked: pinned
  Blender `4.5.12 LTS` reproduces `SIGSEGV (139)` in
  `gpu::supports_barycentric_whitelist -> MTLBackend::metal_is_supported`
  before `.blend` or verifier startup. The same crash occurs with
  `--factory-startup`, explicit `--gpu-backend metal`, environment and
  character sources, and an empty startup Python expression. Logs/backtrace
  and the no-mutation boundary are recorded in
  `art/blender_asset_verifier_host_blocker.md`; historical generated-source
  receipts remain bounded evidence, not a fresh PASS.

- Asset provenance preflight and host classification (2026-08-14) — PASS for
  the independent registry boundary: `./eng/verify-asset-registry.sh` checks
  33/33 derived files and 7/7 explicit local sources, including the corrected
  `character.fullgame.lowpoly.v1` source hash
  `7041c19a35745e7357c6c303e495001af1aabac902a3c33f52be0d9ea9e5fc77`.
  Negative missing-file, duplicate-ID and hash-mismatch fixtures fail closed.
  `./eng/verify-assets.sh` now runs this check before Blender and classifies a
  managed-sandbox SIGSEGV as `HOST_TOOLCHAIN_BLOCKED` (non-zero, no fallback).
  An elevated real-driver run of the same pinned Blender passes both existing
  verifiers; release-host revalidation remains required.

- Style lighting/fog calibration (2026-08-14) — PASS as bounded presentation
  evidence, art lock OPEN. The isolated candidate values are now integrated in
  Chapter 1 `StyleBenchmarkZone.cs`: cooler outdoor ambient/fog, lower house
  lamp/fill energies and a restrained warm-window/moon balance. No geometry,
  shader, material owner, collision, narrative or save path changed. Fresh
  Metal/Forward+ frame hashes are day
  `d6887d013c08340ee6197b6126c4d55b2c10ab0b0e9637f2be2fccdd9700a3d8`, house
  `00396d56324728735d479835b28d9309f902a523ae5c991e9751f7314e4ca2b8` and
  Kara `3b9fdab9a7fc85f41c834ee6245e904005d22f52622576ec65c46ac711494674`.
  The 27-cell spatial sweep hashes are day
  `022647535c839bb568378b5b3015d8d01af63d7bc8576435680056399cb1ca0d`, house
  `39d810d103d372c09404333212bb04668bd7ea6da191f036f07f13593a665767` and
  Kara `1276650c0bf6f2bdf9d266cbe0a62ff891727575f4c3c6cacf536579dd1f6af2`,
  manifest `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`.
  The 216-sample temporal sweep remains clean; visual review still rejects
  final art lock because day/house/Kara geometry and material readability are
  not production accepted.

- Semantic material-owner split (2026-08-14) — PASS as bounded integration,
  art lock OPEN: `PainterlyMaterialLibrary` now exposes separate imported
  owners `wood_facade` (1.8), `wood_fence` (2.2), `wood_furniture` (2.7),
  `wood_bark` (1.35) and an explicit `cloth` alias to the old-fabric descriptor
  (3.0). `GeneratedModularKitDressing` assigns those owners by mesh family;
  `FullGameFlowSmokeTest` asserts imported owner presence and textured clothing.
  `.tools/dotnet/dotnet build game/Urman.Game.csproj`, full
  `./eng/verify-godot.sh`, `./eng/capture-style-frames.sh`,
  `./eng/capture-style-motion-sweep.sh`,
  `./eng/capture-style-temporal-sweep.sh`,
  `./eng/benchmark-godot.sh`, `./eng/verify-painterly-textures.sh` (18/18),
  `git diff --check` and `graphify update .` pass. Fresh frame hashes are day
  `6728de8d1399f687c1140e171c401cf9bf7165f06c2392c096da37ca49e9d560`, house
  `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47`, Kara
  `f0a5209ef5d052f524eb4bc835ec7161ff18d9f56c375a3aa1fa0e51ae5adc8e`;
  spatial hashes are day `6b76871d171ac322c59fd5f6c90cd098c6cbad136ffd67e319e46fed2f528d71`,
  house `db75b808ac74d9ca9ca268398253c36d177e06c1fef1d83131a11e1f2c82feb5`,
  Kara `9fde939a23e1b81d9838813bb2b9a4727894817b777ebd2ac1abbd42823f13fb`;
  temporal hashes are day `b8109a4ad6a9b7d9db7755f643ffe47e0934f7bfb09dbda4e394ba48fe7ec8b7`,
  house `a6bd040c63adba14914e02fcc4cdd94bf55d74ebb00230b92e15a5097539152d`,
  Kara `55a4f4af5df4035377a621ee128b1aada5258fae1daafee1da58b80a854c3f5d`.
  M4 Pro remains above the 30 FPS floor (`120.14/119.87/119.76 FPS`); M1,
  Windows, end-grain/orientation, clothing readability, cultural review and
  art lock remain open.

- Latest graphify after the semantic owner/test/documentation sync — PASS:
  6 756 nodes, 9 128 edges and 527 communities; `graph.html` remains omitted
  over the 5 000-node visualization limit, while `graph.json` and
  `GRAPH_REPORT.md` are current.

- Latest graphify after the canonical epilogue HouseA code/documentation sync —
  PASS: 6 757 nodes, 9 129 edges and 522 communities; `graph.html` remains
  omitted over the 5 000-node visualization limit, while `graph.json` and
  `GRAPH_REPORT.md` are current.

- Canonical Act 5 epilogue HouseA slice (2026-08-14) — PASS as bounded
  presentation wiring, art lock OPEN: `FullGameZoneDressing` now attaches the
  project-original `HouseA_` module to `fullgame_act5_epilogue` under the
  `act5-epilogue-house` variant; `GeneratedModularKitDressing` gives it the
  same provisional layer-2 house proxy contract, and `FullGameFlowSmokeTest`
  asserts `HouseA_Walls_LOD0` plus the `wood_facade` owner. Fresh `.tools/dotnet`
  build and `./eng/verify-godot.sh` pass with zero warnings/diagnostics.
  `./eng/capture-fullgame-frames.sh` now records seven 1 920 × 1 080 frames;
  the new epilogue frame SHA is
  `9ea07a1b1b50b71f7d92d97c8541698ff5b6255003a1bed32a48793c2367e348`.
  No narrative, save, shader, collision authority or web-runtime behavior
  changed; authored family, mesh collision and cultural review remain open.

- Fresh local performance receipt (2026-08-14) — PASS for the internal floor on
  Apple M4 Pro: Forward+ 1 920 × 1 080 measured day `120.11 FPS` / p95
  `9.650 ms`, house `119.84 FPS` / `9.327 ms`, Kara `119.90 FPS` /
  `9.465 ms`; all three remain above the 30 FPS floor. This does not close
  Apple M1-class, Windows or release-shaped performance evidence.

- Authored village-module slice (2026-08-14) — PASS as bounded presentation
  integration, art lock OPEN: `generate_modular_environment.py` rebuilt the
  project-original kit with `WellA_` (7), `WoodpileA_` (4) and `GateA_` (4)
  LOD0 families; `verify_modular_environment.py` and elevated
  `./eng/verify-assets.sh` report the deterministic 47-mesh LOD1 contract.
  Registry preflight passes 36/36 derived and 10 explicit local-source hashes.
  Rebuilt source/derived hashes are
  `9f0371c773e106ec0bcd554045ae4b0b0cbc1f49f2aa4aaf8ce61c71c77798f4` and
  `a7647bc1156a3770433d7b8a2e8ba25df380cf94376a12737a54f12d058cf37a`.
  `GeneratedModularKitDressing` assigns stone/prop-wood/bark/fence/cloth
  semantic owners and marks selected meshes presentation-only; no shader,
  SaveGameV3, narrative, web or gameplay collision authority changed.

- Authored module Godot receipt (2026-08-14) — PASS: fresh .NET build and
  elevated `./eng/verify-godot.sh` complete import, all existing smoke suites,
  full 46-beat flow and canonical epilogue with zero warnings, `ERROR:`,
  `SCRIPT ERROR:`, ObjectDB or RID/resource-leak diagnostics. The smoke now
  asserts exact WellA/WoodpileA/GateA LOD0/LOD1 counts and presentation-only
  metadata in Act 2 house, Act 5 boundary and epilogue variants. Fresh
  full-game frame hashes are Act 2 house
  `32c4d9e2bd1e7178f3e962f8b8433a288c92bbcb059aa7037e8c0d9ad641153a`, Act 5
  boundary `5b315b75b9ab55ec94d6e712d34a700bf81bb36edde455a4517b703d31df0e92`
  and epilogue
  `ca38cd52b279dafc02e16202d95ac2bee1ccafc80ee06be2aab17ecb2b858938`; all
  seven hashes are recorded in `art/fullgame_frames/README.md`. The fixed
  style-frame recapture is day `e0ebb92243eee19e6e8f5a065b5c5dcda4f83ab083cb09c8fff932de16384d41`,
  house `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47`,
  Kara `f0a5209ef5d052f524eb4bc835ec7161ff18d9f56c375a3aa1fa0e51ae5adc8e`.

- Authored module performance receipt (2026-08-14) — PASS for local internal
  floor: M4 Pro Metal/Forward+ day/house/Kara averages are
  `120.19/119.80/119.91 FPS`, p95 `9.784/9.373/9.427 ms`. M1/Windows and
  release-host evidence remain open. The current visual frames still read as
  greybox/progress art; near/mid/far traversal, repetition, marker readability,
authored family coverage, cultural review, mesh collision and art lock remain
explicitly open.

The runtime-backed boundary assertion also finds both Act 5 interaction marker
targets and records GateA horizontal clearance `2.687 m`, with every GateA mesh
at least `1.0 m` behind both marker positions. This closes the missing-owner
check for the static composition only; screen-space near/far traversal and
observed readability remain open.

- Graphify receipt after ASSET-009 source/docs sync — PASS: 6 776 nodes,
  9 158 edges and 528 communities. `graph.html` is intentionally omitted over
  the 5 000-node visualization threshold; `graph.json` and `GRAPH_REPORT.md`
  are current.

- Current desktop package receipt (2026-08-14) — PASS for structural packaging:
  `./eng/export-desktop-debug.sh` rebuilt the Godot 4.7.1 .NET debug packages
  from the 47-mesh authored environment state. The macOS universal ZIP SHA-256
  is `131888d82a9b1be3362540506d6d3b5f1f131336b55a33569504dd108b739b6d`, the
  Windows ZIP is `4462f271d4abd1452f3e511961ea2e799d4aa356c9f3fdba5235f37473e4a0f1`,
  and the Windows PE32+ executable is
  `d4985248230ef8f19b38ea0567a279242cd02bc579da0069d5bad436dbaa4750`.
  `./eng/verify-desktop-artifacts.sh` passes ZIP/package checks, universal
  arm64/x86_64 Mach-O, PE32+ x86-64, three self-contained .NET dependency
  closures, embedded PCK and the four routed ambient stems. The hardened export
  uses fresh staging, lock serialization, safe Windows ZIP namespace checks and
  an atomic SHA/size-bound `build/desktop-artifact-receipt.json`; the current
  Windows ZIP contains zero `__MACOSX`, AppleDouble or `.DS_Store` entries. The
  package is unsigned debug output, not a Windows-host or release-signing pass.

- Current macOS host receipt (2026-08-14) — PASS on the elevated current Mac:
  `./eng/verify-macos-host.sh` launches the fresh universal ZIP with Forward+
  and Dummy audio, observes `zone-loaded: village_day@arrival` and the
  first-person bootstrap marker, and exits without `ERROR:`, `SCRIPT ERROR:` or
  leak diagnostics. A managed-sandbox run can be environment-blocked by the host
  CA store; this does not substitute for a real Windows host or Apple M1 run.

- Graphify refresh after desktop evidence — PASS: `graphify update .` rebuilt
  `graph.json`/`GRAPH_REPORT.md` with 6 776 nodes, 9 158 edges and 529
  communities; `graph.html` remains omitted above the 5 000-node threshold.

- Graphify refresh after hardened export tooling — PASS: `graphify update .`
  rebuilt `graph.json`/`GRAPH_REPORT.md` with 6 782 nodes, 9 166 edges and
  527 communities; `graph.html` remains omitted above the 5 000-node threshold.

## Missing evidence before completion

- Three accepted in-engine style benchmarks.
- Production-grade Acts 2–5 zones/assets, authored facial presentation, voice/ambience and cultural review (the authored data/adapter, project-original character kit, Blender clips, Godot playback and minimum collision smoke are complete, but this is not yet a 6–8 hour art-complete build).
- Windows-host execution smoke, M1/Windows performance evidence and external accessibility pass.
- Authored voice/ambience production and cultural review.
- Final web-owner absence gate after cutover.
- Windows-host execution, Apple M1/medium-Windows performance and
  signing/notarization remain open despite the hardened structural package
  receipt.

- GLB presentation-contract receipt (2026-08-14) — PASS (bounded):
  `GeneratedModularKitContractSmokeTest` is wired into `./eng/verify-godot.sh`
  and passes all nine project-original environment families. It asserts exact
  `_`-terminated family prefixes, expected counts (`12/12, 6/6, 1/1, 2/2,
  5/5, 6/6, 7/7, 4/4, 4/4`), explicit LOD0/LOD1 name pairing, exact semantic
  owner sets, positive imported collision-object/shape removal metadata,
  hidden normalized `HouseA`/`OldPc` render helpers and zero remaining
  `CollisionObject3D`/`CollisionShape3D` descendants in presentation-only
  instances. C# build and real Godot 4.7.1 Metal/Forward+ full verifier pass
  with zero warnings/errors/leak diagnostics. Registry preflight passes 36/36
  derived and 10/10 explicit source hashes; GLB SHA remains
  `a7647bc1156a3770433d7b8a2e8ba25df380cf94376a12737a54f12d058cf37a` and
  Blender source SHA remains
  `9f0371c773e106ec0bcd554045ae4b0b0cbc1f49f2aa4aaf8ce61c71c77798f4`.
  `env.fence.a` is documented as presentation-only because no `FenceA-col`
  exists; floor/path and host proxies remain authoritative. This does not
  close authored mesh collision, Blender release-host, traversal, art-lock,
  cultural, release-hardware or web-retirement gates.

- Style evidence repair receipt (2026-08-14) — PASS for technical evidence only:
  `./eng/capture-style-temporal-sweep.sh` records 216 real Metal/Forward+
  samples at FOV 65/75/90 with head-bob/reduced-motion rows. The disposable
  clone preserves `CollisionObject3D` visual descendants, removes only
  `CollisionShape3D` nodes and reports visual meshes 806/109/826, removed shapes
  286/22/695 and zero active physics-query owners. Current manifest SHA is
  `57ac2b4ae0b81883c30de7b54e41e24b6c9b8be620d7cef3c629e662fb17d66d`; old
  sparse evidence is retained and marked superseded.

- Isolated style-calibration receipt (2026-08-14) — PASS for technical
  candidate evidence only: `./eng/capture-style-calibration-candidate.sh`
  renders six 1 920×1 080 baseline/candidate PNGs in OwnWorld3D viewports,
  cloning the environment and overriding only named presentation lights/fog.
  Manifest SHA is
  `a98bbda675b5fa424d45b08d4972db44df212e5b6f66d3810aa6d01bd2ffc85a`;
  production scenes/materials/shader/GLB/registry/collision/save/narrative are
  unchanged. Visual, cultural and art-lock review remain open.

- Adapter RID cleanup receipt (2026-08-14) — PASS: imported GLB
  `CollisionShape3D.Shape` resources are detached before synchronous node
  disposal. Fresh `.NET` build and elevated `./eng/verify-godot.sh` complete
  with no RID/ObjectDB/resource-leak diagnostics; controlled layer-2 proxy and
  zone floor/path remain the only gameplay owners.

- Final graphify receipt (2026-08-14) — PASS: after the temporal/calibration
  evidence and adapter cleanup sync, `graphify update .` rebuilt the graph with
  7,077 nodes, 9,601 edges and 538 communities. `graph.html` is intentionally
  omitted above the 5,000-node visualization limit.

- Superseding OldPc hero-detail receipt (2026-08-14) — PASS for bounded
  production candidate: pinned Blender rebuilt only
  `assets/source/blender/urman_modular_kit.blend` and
  `game/assets/generated/urman_modular_kit.glb`. The exact
  `OldPc_DriveSlot_LOD0/1` and `OldPc_LabelPlate_LOD0/1` pairs are present,
  `prop.oldpc.crt`/`collision=none` is asserted, and the deterministic kit is
  now 49 LOD1 meshes. Source SHA is
  `4f3aa74be1d34e1cd956806e56fb57e084cbbfc385380042492bac5c66d48b48` and
  derived GLB SHA is
  `c9f9e8d9a036c3fc8ef20dfc736a164fb393eb8194eff0c0e55782547efa2c36`.
  Registry preflight is 36/36 derived and 10/10 explicit sources; full
  `./eng/verify-assets.sh`, C# build and elevated `./eng/verify-godot.sh` pass
  with zero warnings/errors/leak diagnostics.

- OldPc local performance receipt (2026-08-14) — PASS for the internal M4 Pro
  floor: updated Forward+ 1 920 × 1 080 benchmark reports 120.19/120.14/120.00
  FPS for day/house/Kara and a 30-FPS floor PASS. This is not Apple M1 or
  medium-Windows release evidence.

- OldPc close-capture receipt (2026-08-14) — PASS for technical legibility
  evidence only: `./eng/capture-oldpc-hero-detail.sh` ran on Apple M4 Pro
  Metal 4.0 / Forward+ and produced two 1 920×1 080 frames. Front SHA is
  `f85a5ee70a9399e633ff017392fcb582adeac64bd38b7809f0d0a5c0d4916931`, side
  SHA is `606e32a58fdef37cb90066dfc91eb9bae2913613ea7e8d1b7e499b853bcdf819`.
  Both frames find all four exact new names and show no visible z-fight; the
  evidence is not movement, mid-distance, cultural or art-lock acceptance.

- Superseding full-game/style recapture (2026-08-14) — PASS: seven full-game
  frame hashes and three fixed style-frame hashes are current in
  `art/fullgame_frames/README.md` and `art/style_frames/README.md` after the
  49-mesh GLB rebuild. Act 3 Soviet frame SHA is
  `57ad5be29707070b14448cb45134a22d85438fc39d0ef4437c38c7c27b211598`; style
  day/house/Kara are `242673f2cfa56926b43eea698ee11b3b1eb9a10673e0be0ab321d10ee9f2ce39`,
  `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47` and
  `1e0da8a762b9528e879c3167f3eb470bd1520a3cc4c9e900058ecedda967086f`.
  These are production-progress captures; art lock and release-host gates
  remain open.

- Graphify refresh after the OldPc candidate — PASS: `graphify update .` rebuilt
  `graph.json`/`GRAPH_REPORT.md` with 7,113 nodes, 9,638 edges and 534
  communities. `graph.html` remains omitted above the 5,000-node visualization
  threshold.

- Final graphify refresh after the OldPc performance/docs sync — PASS:
  `graphify update .` reports 7,114 nodes, 9,639 edges and 543 communities;
  `graph.html` remains omitted above the 5,000-node visualization threshold.

- OldPc motion/readback receipt (2026-08-14) — PASS for bounded technical
  evidence / OPEN for production acceptance: the first per-tile SubViewport
  prototype was rejected after real Metal output showed cleared later tiles.
  The corrected `./eng/capture-oldpc-hero-detail-motion.sh` reuses one isolated
  OwnWorld3D world for near and mid distances, normalizes/copies readbacks
  before teardown, removes only CollisionShape3D nodes and validates renderer,
  PNG IHDR/dimensions, unique 3×3 coordinates, four exact hero details and a
  non-empty luma span on every tile. It passes on Metal 4.0 / Forward+ / Apple
  M4 Pro with 18/18 frames and two 1 920 × 1 080 sheets. Near SHA is
  `6e8f4bb3729b1b40edde9468bd5bd61476408eb7ade3726fff789312352077b9`; mid
  SHA is `47fe7545dbcf2c3ca0227b760f1d211e1e1e52b1a984658fc7053f2d4489f38f`;
  manifest SHA is
  `a7052d3ad7bfe721ea14f69ff8784d69cb39b60423e1ce60beeaadb4915f9073`.
  Production scenes, material/shader owners, collision, saves and narrative
  state are unchanged. This does not close observed traversal, external
  comfort, repetition, cultural/level-art review, M1/Windows performance or
  final art lock.

- Graphify refresh after the OldPc motion/readback slice — PASS: `graphify
  update .` rebuilt `graph.json`/`GRAPH_REPORT.md` with 7,157 nodes, 9,692
  edges and 548 communities. `graph.html` remains intentionally omitted above
  the 5,000-node visualization threshold.

- Act 1 demo scope and launch receipt (2026-08-14) — PASS for technical
  playability: `project.godot` now defaults to `res://scenes/act1_demo.tscn`.
  `Act1DemoLaunchSmokeTest` confirms first-person arrival, and
  `ChapterOneFlowSmokeTest` confirms 16 authored beats across five compact
  zones, the shared final rule/audio cue and the explicit `НЕ ОТВЕЧАЙ` /
  `Конец демо` fade-to-black. `./eng/verify-godot.sh` passes with 0 build
  warnings/errors. Human voice/ambience, cultural review, observed playtest,
  release-host performance and art lock remain OPEN; Acts 2–5 and web
  retirement are deferred rather than removed.

- Graphify refresh after Act 1 demo scope sync — PASS: 7,258 nodes, 9,816
  edges and 550 communities; `graph.html` remains intentionally omitted above
  the 5,000-node visualization threshold.

- Fresh desktop package after Act 1 demo switch (2026-08-14) — PASS for
  structural packaging and current macOS host smoke: macOS ZIP SHA-256
  `4c24d0ee52b7cefde27b187f874c5a79056f602488af589e7f8f57f466313063`, Windows
  ZIP `8a46cf15e3277eb685e16d16af8440a1e765bb65f5da2060af32413ab88e8c80`,
  Windows EXE `838b5d6f3feba46c0b531334b29f432b79bd4a95406c6da74ac9049c3ca54fd9`.
  The exported PCK contains `act1_demo.tscn`; package receipt binds all three
  artifacts. Windows host execution, M1/Windows performance and signing remain
  OPEN.

- Fresh desktop package after Chapter 1 lighting baseline integration
  (2026-08-14) — PASS: `./eng/export-desktop-debug.sh`,
  `./eng/verify-desktop-artifacts.sh` and `./eng/verify-macos-host.sh` pass.
  Current macOS ZIP SHA-256 is
  `4bac33bd57c796c2a27b1223e91c20e50f26fd97466e6c105c5a5dde5da936f4`
  (161,580,917 bytes), Windows ZIP is
  `0e9c448e46e4eca0531146ce26d7adf0457b8257e2384c7ea9d7851b014669ee`
  (96,818,155 bytes), and Windows EXE remains
  `838b5d6f3feba46c0b531334b29f432b79bd4a95406c6da74ac9049c3ca54fd9`.
  The PCK contains the integrated Chapter 1 `StyleBenchmarkZone` values;
  Windows host execution, M1/Windows performance and signing remain OPEN.

- Fresh desktop package after the 49-mesh asset-verifier correction
  (2026-08-14) — PASS: `./eng/export-desktop-debug.sh`, independent
  `./eng/verify-desktop-artifacts.sh` and `./eng/verify-macos-host.sh` all pass.
  Current macOS ZIP SHA-256 is
  `e1170c0039d0fee44b00f5135a7da3a8371ac92b33a17249c0acee27aea437cd`
  (161,589,435 bytes), Windows ZIP is
  `8cf60b25301df15f7803e71ed6daa052f67160c0a890ea8945a3b17360b2bd0a`
  (96,822,065 bytes), and Windows EXE is
  `838b5d6f3feba46c0b531334b29f432b79bd4a95406c6da74ac9049c3ca54fd9`
  (127,503,520 bytes). Receipt SHA-256 is
  `e5de3ac21cdd09f26dea1d999b7f952be989e1060da08487e5f83faa196fe6e0`.
  Structural checks bind all three artifacts, the full Windows payload tree,
  exact macOS arm64/x86_64 binaries and all three .NET 10 runtime closures;
  Windows host execution, M1/Windows performance, signing and full
  playthrough remain OPEN.

- Desktop receipt read-only audit repair (2026-08-14) — PASS: the published
  Windows ZIP's explicit `windows/` root directory is now accepted by
  `./eng/verify-desktop-artifacts.sh --check-receipt` while every regular file
  remains bound to the 203-file archive/tree manifest. The check is read-only:
  the published receipt SHA-256 `e5de3ac21cdd09f26dea1d999b7f952be989e1060da08487e5f83faa196fe6e0`
  and mtime remain unchanged. A separate four-argument full verifier run also
  passes into a temporary receipt; package bytes and runtime are unchanged.

- Active Act 1 demo verification rerun (2026-08-14) — PASS for the narrowed
  goal: `./.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore
  --disable-build-servers --verbosity minimal -nodeReuse:false -m:1` reports
  0 warnings/errors; `./eng/verify-dotnet.sh` reports Core 38/38, Content
  12/12, 4 modules / 4 campaigns and Chapter 1 state SHA
  `f12ae3b6561e01e98b858628a79c3d4f8339565f113702f3e5b21a346db8d386`;
  elevated `./eng/verify-godot.sh` passes the dedicated launch, 16-beat
  Chapter 1 flow, SaveGameV3/input/persistence smoke and existing regressions.
  Fresh Metal 4.0 / Forward+ style frames are day
  `d6887d013c08340ee6197b6126c4d55b2c10ab0b0e9637f2be2fccdd9700a3d8`, house
  `00396d56324728735d479835b28d9309f902a523ae5c991e9751f7314e4ca2b8` and
  Kara-Urman `3b9fdab9a7fc85f41c834ee6245e904005d22f52622576ec65c46ac711494674`.
  These are technical production-candidate receipts: observed first-time
  playtest, authored voice/mix, cultural review, release hardware and art lock
  remain OPEN. Acts 2–5 and web retirement are deferred by the active goal.

- ASSET-009 runtime-backed boundary evidence repair (2026-08-14) — PASS for
  technical evidence / OPEN for art acceptance. `./eng/capture-fullgame-boundary-runtime.sh`
  builds the real `full_game.tscn`, resolves the Act 5 boundary scene and both
  compiled interaction IDs, and produces two 1 920×1 080 Metal/Forward+ frames.
  GateA has 8 visible LOD meshes, 2.687 m minimum world clearance and remains
  behind both markers. The new `markers` camera projects both interaction
  anchors inside the viewport with no GateA screen-space overlap; the standard
  distant composition is retained separately. Manifest SHA is
  `b469467b77121046861a3d14bbcf1e3c5f103654ada60ddbe21427b6b53234e9` and the
  capture wrapper validates PNG dimensions, hashes, scene path and projection
  fields. This closes the missing runtime-owner/marker-camera receipt, not
  observed traversal, repetition, cultural review, release hardware or art lock.

- Act 1 demo presentation polish (2026-08-14) — PASS for the bounded
  presentation contract. `Act1DemoRoot` enables a demo-only dark transition
  pulse in `Main` between compact-zone loads, while the generic `Main` scene
  keeps the feature disabled by default. The intro card now exposes the
  first-time controls (`WASD`, mouse, `E`, `J`) without changing the shared
  runtime state. `Act1DemoLaunchSmokeTest` asserts the transition layer;
  `.NET` build and elevated `./eng/verify-godot.sh` pass with zero warnings or
  runtime diagnostics. This is not an art lock, authored audio pass or human
  playtest; those gates and all Acts 2–5 production remain deferred.

- Act 1 physical old-PC interaction and fail-closed receipt (2026-08-14) — PASS
  for the bounded playable-demo contract. The Chapter 1 house scene now owns
  `urman.chapter1:interaction/oldpc-power` in the compiled content, so the
  first-person ray → keyboard `E` → OldPc UI path no longer relies on a generic
  `world.interact` fallback. Unknown interaction IDs are unavailable and are
  rejected without mutating runtime state; the Kara boundary marker is
  presentation-only until an authored interaction exists. The Chapter 1
  compiled pack and golden fixture were regenerated (campaign fingerprint
  `d89a055f4e7ecaf61ba584cf64cb772a21a169552bf59589d3244f74378643ed`).
  `./eng/verify-dotnet.sh` passes Core 38/38 and Content 12/12; elevated
  `./eng/verify-godot.sh` passes first-person interaction, dedicated Act 1
  launch, SaveGameV3/input/persistence and the 16-beat Chapter 1 flow. Fresh
  style-frame hashes are day
  `d6887d013c08340ee6197b6126c4d55b2c10ab0b0e9637f2be2fccdd9700a3d8`, house
  `00396d56324728735d479835b28d9309f902a523ae5c991e9751f7314e4ca2b8` and
  Kara-Urman
  `5a4fd0afca85e736ec92d8919c64d526a1caff19ef302fad5dc0f9436e008ba1`.
  These remain technical production-candidate receipts: observed playtest,
  authored audio/mix, cultural review, release hardware and art lock are OPEN;
  Acts 2–5 and web retirement remain deferred by the active goal.

- Fresh Act 1 demo desktop package after the authored old-PC interaction repair
  (2026-08-14) — PASS for structural packaging and current macOS host smoke.
  `./eng/export-desktop-debug.sh`
  and `./eng/verify-desktop-artifacts.sh` rebuilt the package from fresh staging
  and bind the current default `act1_demo.tscn` plus Chapter 1 compiled content.
  macOS ZIP SHA-256 is
  `1abbdd8e4a0f607109743935157ee43fb2ba141e47b7a051391331f1bc89f490`
  (161,605,058 bytes), Windows ZIP is
  `19ea6f692dcbeb5a596b7e907060b18dadc076d088a77680b97e97949934344a`
  (96,830,042 bytes), and Windows EXE is
  `35ac390f6548ceee8f68d13ff4c76cba85278bc5598566f1b52d79c9cd7e97e2`
  (127,504,784 bytes). Receipt SHA-256 is
  `e83d42c80a949f39f798a935746c15e02a1a1be5a573dcb40e81057f287344c1`.
  Exact macOS arches, Windows ZIP safety and .NET closures pass;
  `./eng/verify-macos-host.sh` observes the universal app booting the default
  Act 1 arrival. Windows host execution, M1/Windows performance, signing and
  observed demo playtest remain OPEN.

- Portable campaign-closure repair after narrowing the active goal (2026-08-14)
  — PASS for content tooling, with Acts 2–5 still deferred. The JS compiler
  now validates and materializes the deferred full-game campaign's explicit
  `transitionOverrides` as deterministic scene interactions, keeps them in
  the campaign fingerprint, and does not reintroduce a generic runtime
  interaction fallback. The Chapter 1 and whole-pack content suites pass
  9/9; the current Act 1 launch remains `res://scenes/act1_demo.tscn` and no
  full-game route is added to its release scope. `graphify update .` rebuilt
  7,377 nodes and 9,982 edges across 561 communities; the HTML visualization was intentionally
  skipped at the repository's 5,000-node limit.

- Act 1 ambience continuity pass (2026-08-14) — PASS for bounded presentation
  evidence, not authored-audio acceptance. `AmbientAudioDirector` now keeps
  two in-memory `AudioStreamPlayer`s and crossfades the project-original zone
  beds over 0.65 seconds when the demo moves between its five compact zones;
  the old stream is explicitly stopped and released after the tween. Headless
  mode remains non-playing and deterministic. `AmbientAudioSmokeTest` passes
  the two-player/active-index contract and manifest closure in
  `./eng/verify-godot.sh`; a real Metal 4.0 / Forward+ smoke also passes the
  village → house → Kara-Urman switching path with crossfade metadata. Final
  field sound, voice performance, mix/master, cultural listening and observed
  playtest remain OPEN. Final graphify refresh after this slice reports 7,386
  nodes, 9,996 edges and 564 communities; the HTML view is omitted above the
  5,000-node visualization limit.

- Act 1 logical-voice caption fallback (2026-08-14) — PASS for bounded
  accessibility presentation. When a Chapter 1 voice asset remains a logical
  reference with no physical recording, `AudioCueUi` now presents its authored
  caption if subtitles are enabled and audio descriptions are disabled; the
  default audio-description transcript path is unchanged. `ChapterOneFlowSmokeTest`
  exercises both the normal Rinat cliffhanger cue and the no-recording caption
  fallback. This does not close authored voice, mix/master, cultural listening
  or external accessibility review.

- Fresh Act 1 desktop package after ambience continuity pass (2026-08-14) —
  PASS for structural packaging and current macOS host smoke. Fresh staging
  export includes `AmbientAudioDirector`'s two-player crossfade and the
  updated manifest. macOS ZIP SHA-256 is
  `103a2c75d1dbbb93c9f742310d44dc553560a3274cfaffb8b9f8cc4974862c84`
  (161,610,946 bytes), Windows ZIP is
  `2df1daadef13a41ba832cc062171950b92c1e2e767036bc0690862353c2c7abf`
  (96,833,246 bytes), and Windows EXE is
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes); receipt SHA-256 is
  `19c19a70354a8d47ec81dc28e3eaf11779fa9bf87d24ef594fadb82ef236fc84`.
  Read-only receipt verification passes with 203
  Windows payload files; the current universal macOS app launches the Act 1
  arrival. Windows host execution, M1/Windows performance, signing and
  observed playtest remain OPEN.

- Fresh Act 1 desktop package after logical-voice caption fallback (2026-08-14)
  — PASS for structural packaging and current macOS host smoke. The fresh
  staging export includes the two-player zone crossfade and the fallback that
  keeps authored captions visible when a logical voice has no recording and
  audio descriptions are disabled. `./eng/verify-desktop-artifacts.sh
  --check-receipt` passes with 203 Windows payload files, universal arm64/x86_64
  macOS payload and three .NET runtime closures. Current artifact hashes are:
  macOS ZIP `dbce7a624259c6708bde5498fd1cff6877e0d9e9897116fe3b3f12ef72369b5d`
  (161,611,346 bytes), Windows ZIP
  `a401db1119c20d9b6497f68526c3851775a138f06308f3b67ac12dd24aeb5a6f`
  (96,833,423 bytes), Windows executable
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes), receipt
  `88d4a45b94b43b9180b544554f1d825442194965134d1220164340aa7f2b234d`.
  `./eng/verify-macos-host.sh` observes the fresh universal app launching the
  Act 1 arrival. Windows host execution, M1/Windows performance, signing and
  observed demo playtest remain OPEN.

- Act 1 opening control discovery (2026-08-14) — PASS for bounded internal
  input evidence. `Act1DemoRoot` now reflects the player's detected device in
  the opening card: keyboard/mouse shows `WASD`, mouse, `E` and `J`, while a
  joypad event switches the card to left/right stick, `A` and `Y`. The mapped
  `interact`/`ui_accept` action or left mouse click dismisses the card before
  releasing the player's modal lock. `Act1DemoLaunchSmokeTest` verifies the
  initial wording, device switch and gamepad dismissal; the physical
  `FirstPersonInteractionSmokeTest` verifies the resulting `[A]` interaction
  hint on HouseDoor. The elevated targeted smoke and fresh full
  `./eng/verify-godot.sh` pass.
  This closes an internal discoverability gap without adding input actions or a
  second state owner. `FirstPersonInteractionSmokeTest` additionally traverses
  the physical HouseDoor with gamepad `A` and opens the old-PC target with
  keyboard `E`, including the device prompt transition. Full controller-path
  parity, observed first-time use, motion comfort and external accessibility
  review remain OPEN.

- Fresh Act 1 desktop package after opening control discovery (2026-08-14) —
  PASS for structural packaging. `./eng/export-desktop-debug.sh` rebuilt fresh
  macOS and Windows artifacts containing the dynamic keyboard/gamepad intro;
  `./eng/verify-desktop-artifacts.sh --check-receipt` passes with 203 Windows
  payload files and the receipt bound to the published files. Current hashes:
  macOS ZIP `b95be15636c4cd9a385c758bc84661781d24581e15a834f253413cdb1bb6da78`
  (161,615,212 bytes), Windows ZIP
  `fbad7111e3046a4573fb74cbe524258596b76abcf3ecf3f280ce9b584013e280`
  (96,835,469 bytes), Windows executable
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes), receipt
  `e0702a7ca808b59c0e97aa7242f68cb0e29e538e781d2a7a961498b1d8c40948`.
  Windows host execution, release-hardware performance, signing and observed
  first-time playtest remain OPEN.

- Fresh Act 1 package after physical controller-path smoke (2026-08-14) —
  PASS for structural packaging and current macOS host smoke. The targeted
  Godot smoke drives HouseDoor with a real joypad `A`, confirms the gamepad
  hint, then opens the old-PC target with keyboard `E`; the full elevated
  `./eng/verify-godot.sh` regression suite passes afterward. Fresh export
  hashes are macOS ZIP
  `a3935e08d54aee0668668fbb9a64ed3f2cb69b93011d7036f16ae9032c72cfa5`
  (161,616,257 bytes), Windows ZIP
  `e018a52673eb473784a9f2532b8eeb3c782a177959be4dbf54623266874bcad8`
  (96,835,653 bytes), Windows executable
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes), receipt
  `1c6cddfd23c29bf5cd455ba2e30357d4a72c7a62aa518c783645e969342e8907`.
  `./eng/verify-desktop-artifacts.sh --check-receipt` and elevated
  `./eng/verify-macos-host.sh` pass. This is internal input evidence, not
  observed first-time playthrough, Windows-host, release-hardware, signing,
  cultural, authored-voice or art-lock acceptance.

- Act 1 persistent opening-card pass (2026-08-14) — PASS for bounded first-time
  control discovery. `Act1DemoRoot` now keeps the keyboard/gamepad card modal
  until explicit `E`/`A`/Enter/left-click confirmation; only the backing shade
  softens after the opening beat. `Act1DemoLaunchSmokeTest` waits beyond the
  former timer window and verifies the card remains visible and movement stays
  locked, then switches to gamepad wording and dismisses it through the mapped
  action. The full elevated `./eng/verify-godot.sh` suite passes, including the
  physical HouseDoor gamepad-A / old-PC keyboard-E smoke. This improves
  discoverability but is not observed first-time usability or external
  accessibility acceptance.

- Fresh Act 1 package after persistent opening-card pass (2026-08-14) — PASS
  for structural packaging and current macOS host smoke. `./eng/export-desktop-debug.sh`
  rebuilt the universal macOS and clean Windows packages; read-only receipt
  verification reports 203 Windows payload files. Current hashes are macOS ZIP
  `afd577b221e8a4712bdc6d68c8cc2a225f528522db49b715af3f1d98ab8760cf`
  (161,616,859 bytes), Windows ZIP
  `784fda87aa1a3106e39101593fcf7c3b94b1c1d22a3f0220b5fa6c2052be7531`
  (96,835,879 bytes), Windows executable
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes), receipt
  `e7003e81904b2c893b4f173c1ec56c0a6cb4687468eeb0a37ab348a56017aec4`.
  `./eng/verify-desktop-artifacts.sh --check-receipt` and elevated
  `./eng/verify-macos-host.sh` pass. Windows host, M1/Windows performance,
  authored audio, cultural review and observed first-time playthrough remain
  open.

- Act 1 day-street authored prop integration (2026-08-14) — PASS as bounded
  presentation evidence, art lock OPEN. `StyleBenchmarkZone` now places the
  project-original `WellA_` and `WoodpileA_` families in the fixed day-street
  benchmark through `AttachPresentationOnly`; hidden helper boxes remain the
  local collision owners. `SceneSmokeTest` and `StyleFrameCapture` verify exact
  presentation metadata, positive imported-physics removal and zero actual
  collision descendants for both modules. Fresh Metal/Forward+ frame hashes are
  day `c06331e1daa7657a0faae6874172604d69ec5822d38b87324605d251cf34512e`,
  house `a3eb1fab7c6d3f2c89ebdf03b7570d3a363cb2a9c8edfa95adb907e5d7b220c2`,
  Kara `862904f0a079e9f2c7611f80d6237938189975d2a42b710f8865f97cca2726c1`.
  The day frame gains a grounded authored well and woodpile silhouette, but
  road/forest greybox repetition, near/mid/far traversal, cultural review and
  final art acceptance remain open. No texture candidate, shader, save,
  narrative state or Acts 2–5 launch path changed.

- Fresh Act 1 demo package after authored prop integration (2026-08-14) — PASS
  for structural packaging and current macOS host smoke. `./eng/export-desktop-debug.sh`
  rebuilt the universal macOS and clean Windows packages;
  `./eng/verify-desktop-artifacts.sh --check-receipt` reports 203 Windows
  payload files and a SHA/size-bound receipt. Current hashes are macOS ZIP
  `b60eee45ad882570d386506df02871f97869c52800296cc6e1cf4cbe0534730b`
  (161,619,891 bytes), Windows ZIP
  `48a6489ffac2361fecd504dc8d02ef8cd7dcbb8d0a2ba9df47bd9bce906a90a1`
  (96,837,240 bytes), Windows executable
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes), receipt
  `30d4f60d7f84177e5f75eac16a1fa07739d8a904fc5ed81cf2573d83e5ca7071`.
  `./eng/verify-macos-host.sh` observes `village_day@arrival` and the
  first-person bootstrap. Windows host execution, M1/Windows performance,
  authored audio, cultural review and observed first-time playthrough remain
  OPEN. `graphify update .` reports 7,418 nodes, 10,039 edges and 557
  communities; HTML is omitted over the 5,000-node threshold.

- Diegetic Act 1 wayfinding landmark (2026-08-14) — PASS as a bounded
  readability improvement, art lock OPEN. The existing day-street wooden
  signpost now carries a depth-tested, double-sided, non-billboarded `Label3D`
  with `ФАП`; the scene and capture smokes require its `fap` landmark metadata.
  This is world geometry/presentation only: no HUD quest marker, route-state
  field, interaction ID or save owner was added. Fresh Metal/Forward+ static
  hashes are day `dd6f9e1d43dea0c7de8434659633eee410bc937e7a78e43a9196b3a8772f089d`,
  house `a3eb1fab7c6d3f2c89ebdf03b7570d3a363cb2a9c8edfa95adb907e5d7b220c2`,
  Kara `862904f0a079e9f2c7611f80d6237938189975d2a42b710f8865f97cca2726c1`.
  The 27-cell near/mid/far × FOV sweep also completes on Metal/Forward+ with
  contact-sheet hashes day `70e3dd7d424cba91767259297d870ab75cf2d314497757071471941f3eb64037`,
  house `c8c0a3f2139a22009cab0282a06c2a54aa6200099930493090931af3995684d0`,
  Kara `dcef799a24cfd0803b47aed57b4d29a87c348dada04ac6bf605b6fc411d62121`.
  The sweep remains technical evidence, not observed first-time wayfinding or
  cultural acceptance.

- Fresh Act 1 demo package after diegetic wayfinding landmark (2026-08-14) —
  PASS for structural packaging and current macOS host smoke. The export was
  rebuilt after the physical `ФАП` sign and its fail-closed scene/capture
  contracts landed. Read-only receipt verification reports 203 Windows payload
  files. Current hashes are macOS ZIP
  `d518c32f02aa8a64a5445005aa088f428d8ed2b3732b9b49c5c63ac55d701e3e`
  (161,621,232 bytes), Windows ZIP
  `462d053b3ceeddb09a1dbae7d93524516e830ef0b3176b08bc37012693596a44`
  (96,837,788 bytes), Windows executable
  `6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
  (127,504,832 bytes), receipt
  `89134708e766f7fc89f9187d83ec55d65c34c5100bf9099cd142847cbb8b8a05`.
  `./eng/verify-desktop-artifacts.sh --check-receipt` and elevated
  `./eng/verify-macos-host.sh` pass; the host receipt observes
  `village_day@arrival` and the first-person bootstrap. Windows host,
  M1/Windows performance, authored audio, cultural review and observed
  first-time wayfinding remain OPEN. The final `graphify update .` for this
  slice reports 7,421 nodes, 10,042 edges and 556 communities; HTML remains
  omitted above the 5,000-node threshold.

- Act 1 physical first-person corridor (2026-08-15) — PASS for internal
  playable-route evidence, human-facing gates OPEN. New
  `res://tests/act1_first_person_corridor_smoke_test.tscn` starts the actual
  `act1_demo.tscn` entrypoint and uses the production camera ray plus mapped
  `E` input for arrival → house → old PC → street → ФАП → official notice →
  house/Rinat → saved message → Татарвики boundary/reread → edge sketch →
  zirat → Kara-Urman. Physical evidence targets open the shared `DocumentUi`,
  and the Rinat dialogue commits the shared `alerted` state before the route
  continues. `./eng/verify-godot.sh` passes with the corridor included;
  build warnings/errors are 0. The house adds only a presentation-only empty
  chair/coat/radio cue beside the Rinat target because a registered Rinat mesh
  is not yet available. Receipt and remaining gates are recorded in
  `docs/urman_knowledge_base/art/first_person_corridor/README.md`; this does
  not claim observed first-time pacing, authored voice, cultural approval,
  release-host performance or art lock.

- Act 1 physical journal handoff strengthening (2026-08-15) — PASS: the
  corridor now presses the real `DocumentUi` `В журнал` button after the FAP
  death notice and asserts the resulting shared journal entry before leaving
  the document. This closes the physical document → shared journal handoff;
  observed usability, authored presentation and cultural review remain open.

- Fresh Act 1 demo package after the physical corridor pass (2026-08-15) —
  PASS for structural packaging and current macOS host smoke. The exporter
  rebuilt the universal macOS and clean Windows packages with the corridor
  scene/test and evidence-document bindings. Read-only receipt verification
  reports 203 Windows payload files; `./eng/verify-macos-host.sh` observes the
  default village arrival and first-person bootstrap. Current hashes are macOS
  ZIP `82c6b682af0ec633151922a86fd97025becc08662af2bf2dcad5750924d7e64f`
  (161,648,302 bytes), Windows ZIP
  `7ead35eb92e646af7c3c2c82ae71adfe75fe82a57b1af27b834eaf99db61f5be`
  (96,850,304 bytes), Windows executable
  `d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`
  (127,506,120 bytes), receipt
  `df8342eb8c482f2f6b4714a2777b86b7b0ad4b0a838a33f6e3fc1ef2e507e812`.
  Windows host, M1/Windows performance, authored audio, cultural review and
  observed first-time playthrough remain OPEN. `graphify update .` after this
  slice reports 7,446 nodes, 10,087 edges and 566 communities; HTML remains
  omitted above the visualization threshold.

- Post-RID-cleanup package refresh (2026-08-15): the adapter now detaches
  shared imported `Shape3D` resources without manually disposing the
  PackedScene subresource. The isolated contract smoke and the full
  `./eng/verify-godot.sh` suite pass with no leaked Shape3D RID. The fresh
  exporter and read-only receipt check report the current macOS ZIP
  `96d63b2130e46b8921120ffc6194d33987811c3baf7243c7fa56de35128542d5`
  (161,648,202 bytes), Windows ZIP
  `340c0ab989173da2ea1b33fdc10dd0ab0e793ac79993dab9556c2357e10891de`
  (96,850,207 bytes), Windows executable
  `d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`
  (127,506,120 bytes), and receipt
  `0d468d974ea6aef5674e86cc445edc9fe0490b6c00d0aa8e96d84012fbbf2bd4`.
  Windows-host execution, M1/Windows performance, authored audio, cultural
  review and observed first-time playthrough remain OPEN.

- Physical journal handoff package refresh (2026-08-15): the Act 1 corridor
  now verifies the actual `DocumentUi` save button and shared journal entry;
  desktop artifacts were republished after that test-only assertion. Current
  macOS ZIP is `07ca63e17bd825d31635b249e3d066c6d6e710115315af628410a84c2ae56582`
  (161,649,874 bytes), Windows ZIP is
  `1335c505a9e56a34ed75daff1de64ad99ce0b45ed2525c10586bf109b2fdea3c`
  (96,850,912 bytes), Windows executable remains
  `d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`,
  and the bound receipt is
  `a56f2effe013bede9c3d0039cf3f9a7fb36b2108ca1a863d9513a894348e64a1`.
  Structural checks pass; Windows host, release hardware, authored audio,
  cultural review and observed first-time playthrough remain OPEN.

- Act 1 first-objective cue package refresh (2026-08-15): the demo opening card
  now states the presentation-only first goal — reach the house and check the
  old computer — and includes `Esc/Start — меню` in the device-specific control
  line. No runtime state, quest owner or narrative content changed. Fresh
  `./eng/verify-godot.sh`, desktop export, read-only receipt check and elevated
  `./eng/verify-macos-host.sh` pass. Current artifacts are macOS ZIP
  `7a7187719b32516aaefa581b179e1182542ce85ceb99e9233cede29943a22eb5`
  (161,649,937 bytes), Windows ZIP
  `ad92d31b4142095134ca677bdb3008362998e4f3e87cab503b003055ec08dae2`
  (96,850,962 bytes), Windows executable
  `d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`
  (127,506,120 bytes), receipt
  `6f5c8c0e422c4ad2d6ef7d7ee31b0f206b417108a8b8783361820d4c03128542`.
  Human first-time playtest, authored audio, cultural review, release hardware
  and art lock remain OPEN. `graphify update .` reports 7,479 nodes, 10,122
  edges and 561 communities; graph HTML remains omitted above the viz limit.

- Act 1 startup graphics/performance repair (2026-08-15): `FirstPersonController`
  now applies its declared default `medium` profile during `_Ready`, before the
  first rendered frame, instead of leaving the viewport at the project default
  scale 1.00 with MSAA disabled. `Act1DemoLaunchSmokeTest` asserts 0.90 3D
  scale and 2× MSAA. The real full-entrypoint probe reports `nodes=1982` and
  approximately 120 FPS on the local Apple M4 Pro in Forward+, Forward Mobile
  and Compatibility; this is diagnostic evidence, not M1/Windows acceptance.
  Fresh `.NET` build and elevated `./eng/verify-godot.sh` pass. Republished
  artifacts are macOS ZIP `5938dd4f82ff02968228d834e6fe3a04570fb9ceb8cf14970fa3087605c9fe9d`
  (161,657,803 bytes), Windows ZIP
  `b70a8323b2ca4e26f524ea3b944ceab5714318af94a672cfd16d69000129c578`
  (96,855,264 bytes), Windows EXE
  `a2f00debbc0e347e47f32c3ba709dcd4e8f509de9ded3fba87758f0833e89ee2`
  (127,507,360 bytes), receipt
  `cb2abfb5c73e974d966c83a30c2bd898a79c0150a6cf99e358c7b329c939f3dd`.
  Windows host, release-hardware performance, authored audio, cultural review,
  observed first-time playthrough and art lock remain OPEN.

## Current Act 1 safe-graphics repair — 2026-08-15

The reported single-digit-FPS path now has an explicit, reversible diagnostic
launch. The normal demo remains `medium` with the full painterly shader; the
new `eng/run-act1-demo-safe.sh` starts Mobile + `--urman-safe-mode`, which
skips the three triplanar albedo reads while preserving the same zones,
interactions, saves and narrative state. Local real-driver probes record
`medium` at 120.00 FPS and safe `low` at 120.29 FPS on the M4 Pro; this is not
M1/Windows acceptance and the target-host reproduction remains open.

Fresh packages after the shader repair pass structural and current-macOS host
checks. Current artifacts are macOS ZIP
`db4fa8eab12200dc14597662d941120a6c719ebecf9e8326b12be9e58c3f752d`
(161,659,198 bytes), Windows ZIP
`73b317b7d1679d9fa72e52fc43ba36811487cd154db9a560bbe606ba08142521`
(96,855,953 bytes), Windows executable
`a2f00debbc0e347e47f32c3ba709dcd4e8f509de9ded3fba87758f0833e89ee2`
(127,507,360 bytes), receipt
`3eaa889b93937132782eed44125b2da3c24640bf1c13e01c7da54d9fa50693a0`.
`./eng/verify-desktop-artifacts.sh --check-receipt`,
`./eng/verify-macos-host.sh`, full `./eng/verify-godot.sh`, shell syntax and
graphify all pass. Windows host, M1/Windows performance, authored audio,
cultural review, observed first-time playthrough and art lock remain OPEN.
  `graphify update .` reports 7,488 nodes, 10,134 edges and 563 communities;
  graph HTML remains omitted above the visualization limit.

## Packaged Act 1 performance probe — 2026-08-15

The published macOS ZIP now includes the diagnostic-only `--urman-perf-probe`
branch in the Act 1 main scene. `./eng/benchmark-act1-demo-package.sh` extracts
the ZIP into a temporary directory and measures the real packaged binary;
passing `--urman-safe-mode` measures the Mobile/low painterly branch. Both
fresh-package real-window Metal runs report about 120 FPS on the local Apple
M4 Pro (medium Forward+, safe Forward Mobile) and exit 0. The optional
`URMAN_ACT1_HEADLESS=1` mode is only a CPU/startup check.
This closes only package-vs-editor evidence on the current Mac; M1/Windows,
observed first-time playthrough, authored audio, cultural review and art lock
remain OPEN.

Current package hashes: macOS ZIP
`f291f2e02b755bd19d64b06375ec91ed926b69d905fc9b216db07258345fc561`
(161,662,462 bytes), Windows ZIP
`404f396bc066beb68459b86c01b18b6fabf1454765ecadf11d6f18375e4c462f`
(96,858,967 bytes), Windows EXE
`a2f00debbc0e347e47f32c3ba709dcd4e8f509de9ded3fba87758f0833e89ee2`
(127,507,360 bytes).

## Superseding Act 1 walkthrough/package refresh — 2026-08-15

The new `Act1FirstPersonWalkthroughSmokeTest` is included in the full
`./eng/verify-godot.sh` run and reaches the Kara-Urman cliffhanger after
93.05 m of production `MoveAndSlide` movement, with no player-position writes
or direct narrative dispatch. It is a traversability regression gate only;
human wayfinding, comfort and art acceptance remain open.

The current export was rebuilt after this test was added. Structural receipt
check passes with macOS ZIP `b9df0e1ef5fae43d3760d6e28cece250f44614e19c9cded7648c70781062ac4f`
(161,688,624 bytes), Windows ZIP
`db4780218958dee3ab807f47b29ca7790680865bb0ae58c2ecd31a0eb99eb744`
(96,872,645 bytes), Windows EXE
`b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
(127,508,656 bytes), and receipt
`f3f238e2dc55c002762553d9384db83268a0cda610226814880fd8ed88b95419`.
Real packaged Metal probes on the local M4 Pro report 118.00 FPS for both
medium Forward+ and safe Forward Mobile/low. This remains host-specific
diagnostic evidence; M1/Windows, observed first-time playtest, authored audio,
cultural review and art lock remain open.

## Fresh package after interaction-availability CPU repair — 2026-08-15

The exported packages were rebuilt after removing both known frame-loop state
pollers: `InteractionTarget` now uses coalesced main-thread invalidation, and
the Act 1 ending detector evaluates only after the same notification. Structural
package verification and the read-only receipt check pass. Current artifacts are
macOS ZIP
`388e896d387b8164327c75e3bce6444902a69c966bbdc1026c6f157fe0061800`
(161,692,897 bytes), Windows ZIP
`73209ddc5d1e6f1d5e75b6b6340fa2c623c31aec4390404a071658659dd296f4`
(96,875,494 bytes), Windows EXE `b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
(127,508,656 bytes), and receipt
`b4c000c79c16728e7217e3fcd9e8b7002922bbd6c1719a46bbd7c07edcb42cf4`.
Fresh package real-window probes report 119.80 FPS Forward+ medium and 119.85
FPS Forward Mobile low on the local M4 Pro; source-tree probes before the
ending-detector change were 119.97 and 119.99 FPS respectively. Windows host
execution, M1/Windows performance and
reproduction of the user's 1 FPS remain open.

## Act 1 house OldPc presentation slice — 2026-08-15

The playable demo now uses the project-original `OldPc_` GLB module as the
single visible house-PC focal prop. Duplicate procedural CRT/keyboard/tower
pieces were removed from the benchmark, while the existing layer-1 `OldPc`
interaction remains intact. The presentation adapter removes imported collision
descendants before scene insertion; SceneSmoke and StyleFrameCapture require
8/8 LOD meshes, positive sanitation counts and zero module physics.

Fresh verification: .NET build, elevated `./eng/verify-godot.sh`, real
Metal/Forward+ style capture and `./eng/verify-desktop-artifacts.sh
--check-receipt` pass. The current style-frame hashes are day
`0439c2434f973bd650872a44f06f48971a3d56a687e4a8e151431ed8477d5e0b`, house
`1cc76c7f469a04f4cb064016ba29c7b5a8f6ca61bdcddc41c9ddb1be17f842ae`, and Kara
`862904f0a079e9f2c7611f80d6237938189975d2a42b710f8865f97cca2726c1`.

The fresh package is macOS ZIP
`27a7f3c49d41d05449acdfb58e3a0b8be36f252d07c0029e81ca0d7876c58544`
(161,693,895 bytes), Windows ZIP
`5c51fc40db7f3ae8c9a66a9f2f1470c1b19a49ad14b69ce3ce26d63cb682e4d4`
(96,876,105 bytes), Windows EXE
`b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
(127,508,656 bytes). Real package probes on the local M4 Pro report 119.85
FPS Medium/Forward+ and 119.97 FPS safe Mobile/low (a fresh re-probe measured
119.51 FPS in Medium/Forward+). This remains a bounded
production-progress slice: art lock, observed playtest, target-host FPS and
Acts 2–5 remain open.

## Kara-Urman night readability calibration — 2026-08-15

Review of the fixed first-person frame found that the previous night values
collapsed the route into a blue-black mass. A bounded presentation-only
calibration now uses cold ambient `7b9096 @ 0.64`, fog `52666d @ 0.0034` with
height density `0.075`, directional energy `0.82` and local `MoonFill`
`8198a0 @ 1.10`. No gameplay light, material, shader, collision, narrative or
save owner changed.

Fresh `.NET` build, elevated `./eng/verify-godot.sh` and real Metal/Forward+
capture pass. Current frame hashes are day
`0891fdbc18f4119028b6de5c9147f0444e1ba04b74afc334cd95d8c000dc377f`, house
`bcb0a1d92c694161d84ef50051de578a54b6ab4c200435662d4160fea83428cb`, and Kara
`c8d73ce00425fc791f12438d7c52195f544322f03f027bcb633e545c058fe9c3`. Visual
art lock, motion/wayfinding, cultural review and target-host performance remain
OPEN.

## Slow-startup FPS guard and fresh package — 2026-08-15

The Act 1 demo now has a bounded session-only rescue for a confirmed slow
startup: after a short warm-up it requires several consistently heavy frames,
then applies the existing low material/render-scale profile. It does not touch
zone routing, kernel/narrative state, SaveGameV3, collision or renderer
architecture. `--print-fps --no-auto-performance-fallback` exposes live target
host diagnostics without the rescue.

Fresh C# build, Godot smoke and desktop receipt verification pass. The current
package is macOS ZIP
`8152badb2462f7447fc8dbcdbfcc1d5eea7fafe8c59e90bb5c96efca73dd5535`
(161,696,758 bytes), Windows ZIP
`06c28014aaabf31a893326efe2407b0f66458a552de87cea412a6e68bc08b26b`
(96,877,380 bytes), Windows EXE
`b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
(127,508,656 bytes), receipt
`ee752d0539ede892752260798a4f574399963b9e50a52ac5579cbabcb3aec61a`.
Real package probes on the local M4 Pro report `119.94 FPS` medium
Forward+ and `120.05 FPS` safe Mobile/low. Target-host Windows/M1
performance and observed first-time playtest remain OPEN.

## Direct-window 1 FPS diagnostic — 2026-08-15

The fresh macOS ZIP was launched as a normal desktop window with
`--print-fps --no-auto-performance-fallback`. Godot printed `Project FPS: 120`
for Forward+ and `Project FPS: 119` for Mobile/low. A one-second macOS sample
showed the expected VSync wait in `CAMetalLayer.nextDrawable`, not a CPU-bound
one-FPS loop. A managed/sandboxed launch aborted before the first frame while
opening `user://logs`/Metal state and is excluded from performance evidence.
The original report therefore remains an external reproduction gate: OS/GPU,
editor-versus-ZIP path and complete target-host `--print-fps` output are still
required.

## Continuation evidence — 2026-08-24

- `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore
  --disable-build-servers --verbosity minimal -nodeReuse:false -m:1` — PASS,
  0 warnings / 0 errors.
- `./eng/verify-dotnet.sh` — PASS, Core 38/38, Content 12/12, 4 modules / 4
  campaigns, diagnostics 0, audio caption/transcript closure 8/8.
- `./eng/verify-godot.sh` — PASS, including connected-world launch,
  91.76 m first-person walkthrough, Chapter 1 flow, collision/road QA and
  full-game regressions; no runtime/leak diagnostics.
- Dedicated `act1_demo_launch_smoke_test.tscn` with the pinned absolute .NET
  environment — PASS. It now asserts `AgentBExteriorWorld` and the shared
  reverse-arrival ground box (`208 m`, center `z=-48`).
- `graphify update .` — PASS; current graph 8,252 nodes / 11,413 edges / 606
  communities (HTML visualization intentionally skipped above the 5,000-node
  limit).
- Full-route capture liveness correction — source/shell checks PASS; the
  previous run is not reused as visual acceptance. No desktop capture was
  rerun because the user requested no repeated focus-stealing demo launches.

Open evidence: fresh 36-frame root-viewport capture, manual 360°/near-mid/far
review, cultural/language review, motion comfort, target-host performance and
release gates.

## Continuation evidence — 2026-08-24 — FAP owner seam

- `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore
  --disable-build-servers --verbosity minimal -nodeReuse:false -m:1` — PASS,
  0 warnings / 0 errors after the FAP filter.
- Dedicated `act1_demo_launch_smoke_test.tscn` — PASS; verifies the authored
  FAP presentation owner, diegetic `ФАП` label, `Fap_*` suppression
  metadata/count and no visible Agent B FAP meshes, alongside the
  reverse-arrival ground envelope.
- `./eng/verify-godot.sh` — PASS; connected-world launch, physical Act I
  corridor and existing full-game regressions remain green.
- `graphify update .` — PASS; 8,258 nodes / 11,421 edges / 608 communities.
- `git diff --check` plus targeted untracked-file whitespace checks — PASS.

This is a narrow presentation-ownership correction, not a visual acceptance
claim. FAP forward/back/side frames, wayfinding, cultural/local-context review,
human playtest, target-host performance and release gates remain open.

## Continuation evidence — 2026-08-24 — Foliage source-board seam

- `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore
  --disable-build-servers --verbosity minimal -nodeReuse:false -m:1` — PASS,
  0 warnings / 0 errors after the foliage source filter.
- Dedicated `act1_demo_launch_smoke_test.tscn` — PASS; verifies hidden
  `AgentB_FoliageKit` source root, `templateSourceHidden` metadata, non-empty
  `AgentB_PlantedFoliage`, and consistent planted entry/node counts.
- `./eng/verify-godot.sh` — PASS; connected-world launch, physical Act I
  corridor and existing full-game regressions remain green.
- `graphify update .` — PASS; 8,265 nodes / 11,429 edges / 612 communities.
- `git diff --check` plus targeted untracked-file whitespace checks — PASS;
  no windowed capture was run.

This closes only source-template contamination. Foliage repetition, ground
contact, near/mid/far readability, cultural review, human playtest, target-host
performance and release gates remain open.

## Continuation evidence — 2026-08-24 — Atmosphere owner routing

- `.tools/dotnet/dotnet build game/Urman.Game.csproj --no-restore
  --disable-build-servers --verbosity minimal -nodeReuse:false -m:1` — PASS,
  0 warnings / 0 errors after atmosphere routing.
- Dedicated `act1_demo_launch_smoke_test.tscn` — PASS; verifies one active
  exterior `WorldEnvironment` (`AgentBEnvironment`) and rain emitter, local
  village environment disabled, then one active house environment with Agent B
  environment/rain disabled.
- `./eng/verify-godot.sh` — PASS through the Act I corridor and existing scene,
  narrative, persistence, collision and full-game regressions; the expected
  wrong-owner/missing-interaction warning remains intentional.
- `./eng/verify-dotnet.sh` — PASS: Core 38/38, Content 12/12, 4 modules/4
  campaigns, diagnostics 0, audio closure 8/8.
- `graphify update .` and targeted diff-check — PASS; no windowed capture was
  run and no Godot process remains active.

This closes only the global presentation-owner conflict. Lighting quality,
night readability, warm-window balance, rain/fog composition, cultural review,
human playtest, target-host performance and release gates remain open.
