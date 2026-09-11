# Current continuation — finished Act I, 2026-09-11

- Authority: `docs/production/URMAN_ACT_I_FINISHED_PRODUCT_HANDOVER_RU.md`, full user attachment at `/Users/unterlantas/.codex/attachments/6991f080-1dd4-4095-8371-6210eb550dab/pasted-text.txt`; latest direct request additionally authorizes regular commits/pushes and native GPT-5.6 Luna max. No new branch/worktree. Historical restrictions below are superseded only within this Act I task.
- TaskStartSnapshot: main at `1f42f95`, clean worktree, ahead origin/main by 1; 54 GiB available. Active create_goal has no token budget.
- Single queue: `docs/urman_knowledge_base/execution_backlog.json`, FINISH-01…08. One writer; build/import/generation/capture/graph updates serialized.
- Baseline read: AGENTS, KB README/current decisions/questions/weak points, complete finished-product handover; relevant narrative/audio/art authorities being traced by main and three native Luna max read-only agents (investigation_audit, audio_delivery_audit, visual_audit). Tool exposes priority service tier, no separate Fast toggle.
- Evidence root: `/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911`. Preserve `/Users/unterlantas/Documents/URMAN_visual_20260910` unchanged.
- Source baseline launch reached arrival on real Metal/M4 Pro. CUA getApp by bundle and path timed out twice; no ordinary-input playthrough claimed. Own PID48375 terminated, protective runner restored saves/preferences byte-for-byte. Preexisting headless PID39503 belongs to another session and remains untouched.
- Userdata protection: evidence-root `protected_run.py` backs up existing files in savegames plus settings.json/audio-settings.json; finally restores hashes and removes only files newly created by child. Wrap each game/smoke invocation. Do not overlap runs or use broad verify-godot.
- FINISH-01: native_v3 export PASS (233 PCK entries each; .NET/native archives), runtime material exclusions and imported winter resource lookup fixed. Actual macOS window probe at1080 medium/.9/MSAA2/FOV75 completed12s+60s: avg13.601ms,p9544.206ms,p9947.464ms,long14.914%, FAIL. No world errors. This is a verified failing baseline, not performance acceptance. CUA gets app; screenshot call waited until probe exit, no final frame obtained. Next: indoor/character calibration and investigation; FINISH-07 owns native frame pacing. Evidence native_export_v3.log + native_v3/window_probe.log/receipt under evidence root.
- FINISH-04 calibration: indoor snow/cache fixed; adult proportions, collar, hair/role clothing adjusted in existing character generator; rig bone names/clips preserved, joint positions updated. Source FAP floor top moved from+0.12 to0, daybed source placement restored and collider aligned. Source/GLBs/registry updated together. C# build and existing character verifier pass; latest source frames interior_calibration_v3 under evidence root. Not full art lock.
- Native measurement follow-up: no screenshot p958.54ms but UI activation outlier1186ms; untouched run p958.355ms,p999.122ms,avg7.122ms,max529.721ms, long0.024%. Still FAIL strict max100ms; do not blame all stalls on CUA. Existing native_v3 package predates indoor changes. Next export must be rebuilt.
- Native ordinary UI reached menu, New Game, intro and arrival through CUA. Intro still says «Дождь» and needs winter copy correction. Subsequent keyboard checks were inconclusive (CUA stale/no-window errors). CUA uses full app path due duplicate bundle IDs; original native_baseline extracted app removed, ZIP kept. All own native/source processes terminated; wrapper restored userdata. No full ordinary-input walk claimed.
- Asset registry: changed character/FAP source and derived hashes PASS. Whole existing preflight found stale hashes for unchanged HEAD assets village_exterior_kit, agentb.terrain_road_kit, agentb.foliage_kit (evidence interior_asset_registry.log). Do not claim global registry green or silently rehash these unrelated source pairs; investigate provenance at final asset closure.
- FINISH-02 early choices: Gulsina offers tea/question and Mansur help/question on the first conversation; intro snow copy corrected. Content compile + affected C# build PASS. Main protected dialogue_flow and first_person_corridor smoke PASS; saves/preferences restored byte-for-byte. This proves branch/route contracts, not an ordinary 8–12-minute playthrough. FINISH-03 manual comparisons remain next.
- FINISH-05 footsteps: 12 CC0 source-backed WAVs + sources/licenses/manifest integrated, native randomizer/polyphony retains sample tails, actual XZ cadence .55m follows packed/soft/wood/FAP surfaces and ReducedMotion keeps sound. Generator reproduces every output SHA; affected build/import + existing footsteps smoke v2 PASS; v1 failed solely because the amended test used nonexistent fap@entry, corrected to waiting_room. Userdata restored. Listening/mix, ambience/voice lifecycle and physical final voices remain open.
- FINISH-03 manual investigation implemented: three source pairs and nine hypotheses in the existing journal; only actual document opens record sources; wrong choices remain retryable; correct results use existing content.apply and checkpoint owner. Old PC is usable on returning home. Premature archive rule/pact explanations removed; roadside tag has two visible notches and supports a geographic inference, not proof of Marat’s presence. One-shot external implementation scripts have been APPLIED ONCE; do not rerun.
- Manual investigation evidence: content compile v2 and affected C# build v8 PASS; protected journal v2, ChapterOne, first-person corridor and checkpoint smoke PASS. Existing UI capture v2_retry PASS at720/1080 and scales1/1.6; main inspected large720 comparison. First v2 capture failed only because the external output directory was absent; retry created it. Actual1080 zirat_clue_close frame shows the two notches. All runs restored userdata byte-for-byte. No ordinary full playthrough claimed.
- Independent Luna review: three journal UI findings fixed (visible focus on reopen, retained selected entry, state-change refresh); existing JournalFlow smoke v2 covers focus/selection and passes. Conditional vocabulary downgrade has no reachable reverse transition in the current route; no broad status-engine change.
- Next writer: main. Audio cue lifecycle and final rule timing remain separate pending changes; forest entry currently reveals the final rule before Rinat’s staged interruption.
- Open external facts: no physical Marat/Rinat final voice established; rights/cultural/human/Windows/signing approvals must not be fabricated. Finish independent local work before closure assessment.
- Drift: original product scope retained; legacy acts II–V/web retirement below are historical and out of scope. No completion claim.

---

# Todo Checkpoint Draft — старт Godot migration

Date: 2026-08-10

## Current todo

- [x] Зафиксировать TaskStartSnapshot и прочитать baseline owners.
- [x] Запустить доступный web golden gate.
- [x] Закрепить принятые engine/style/story/runtime решения в knowledge base.
- [x] Установить и зафиксировать Godot 4.7.1 .NET, .NET 10 и Blender 4.5 LTS.
- [x] Создать .NET solution и C# content parity slice.
- [x] Перенести runtime и SaveGameV3. Kernel, deterministic clock/RNG/scheduler, atomic SaveGameV3, custody/evidence provenance, resolver catalog, asset/text/audio resolvers, capability host, quest-owned session orchestration, общий content condition/effect engine, полный data-driven quest lifecycle и handlers для dialogues/vocabulary/journal готовы; протокол старого ПК выровнен с compiled content.
- [ ] Собрать Godot Chapter 1 vertical slice и art benchmarks. First-person greybox, пять связанных зон, data-driven UI старого ПК, журнала и диалогов, SaveGameV3-backed accessibility settings, deterministic four-stem ambience routing и полный authored arrival-to-cliffhanger flow готовы; v2 texture regression и отдельный v3 candidate pass (wood/plaster/earth/pine) с тремя in-engine сценами, swatch capture и 27-cell near/mid/far × FOV spatial sweep записаны, но temporal motion comfort, authored voice/mix и финальный art lock ещё не готовы.
- [ ] Заблокировать и произвести акты 2–5. Авторский C# content layer, compiled pack, narrative invariants, 12 walkable zones, zone-specific `FullGameZoneDressing` pass, project-original character-kit face/clothing detail pass и Godot `Idle`/`Tension` playback готовы; production-grade зоны, facial expression polish, постановка, звук и культурный review ещё не готовы.
- [ ] Выполнить desktop/full-playthrough gate и web retirement.

## Active slice — Act 1 demo (full-game foundation deferred)

The current delivery boundary is only the playable first act: five compact
zones, 16 authored beats, first-person investigation, old PC/archive,
journal/dialogue/vocabulary state and the `НЕ ОТВЕЧАЙ` cliffhanger. The
full-game adapter evidence described below is retained as deferred foundation;
it is not a requirement to expand this demo into Acts 2–5.

 Godot Chapter 1 vertical slice plus full-game content/runtime adapter. C# content/runtime parity, SaveGameV3, связный first-person bootstrap, shared-state journal projection, physical full-game document reader, Blender modular-kits, все 16 authored Chapter 1 beats и 46 authored full-game beats через 12 компактных Acts 2–5 зон готовы. Каждая full-game зона теперь загружается через отдельный authored `PackedScene` wrapper; zone-specific dressing pass, selective environment `.glb` dressing, project-original nine-prefix character kit with explicit Godot LOD ranges, imported `Idle`/`Tension` playback и семь representative full-game captures воспроизводимы. Новый `CollisionQaSmokeTest` проверяет queryable layer-1 floors, authored 9×28 path relief cells и изоляцию layer-2 kit proxies во всех 12 зонах; отдельный `RoadReliefQaSmokeTest` проверяет семь relief paths и три day-road puddle clusters. Четыре deterministic WAV stems, `ambient_manifest.json`, `AmbientAudioDirector` и `AmbientAudioSmokeTest` закрывают импорт/zone-routing, а headless cleanup остаётся leak-free; final voice/mix не заявлены. Локальный M4 Pro benchmark после road-relief/puddle pass подтверждает 30 FPS low-preset floor на трёх style scenes. Технический accessibility-контракт SaveGameV3/Godot UI теперь подключён и smoke-tested. Шесть v2 Painterly candidates и шесть focused v3 candidates имеют отдельные image gates; v6 earth/wood pair добавлен как test-only comparison, а motion/temporal sweeps остаются техническим evidence без art lock. Authored road-relief/puddle recapture обновил static/spatial/temporal evidence; wet specular/roughness, greybox family and visual acceptance остаются открыты. Narrative package актов 2–5 зафиксирован в `narrative_lock_acts_2_5.md`; машиначитаемая очередь `docs/urman_knowledge_base/execution_backlog.json` содержит 7 milestones и 24 задачи, текущий focus — `GODOT-003`, `ASSET-006` закрывает host-independent asset provenance, а `NARR-001` завершён и `NARR-002` ждёт внешнего cultural review. Следующий рубеж — production wetness/material and full authored art pass, temporal traversal review, M1/Windows performance evidence и authored voice/mix, внешняя accessibility-проверка и cultural gates. Production web boot не менялся и остаётся oracle.

Road-relief QA count note: the focused smoke currently covers seven relief
paths (day street, three Kara-Urman segments, Zirat road and two full-game
wrappers) and three day-road puddle clusters; the full-game collision suite
additionally checks the same path contract while traversing all 12 zones.

## Production-candidate texture checkpoint (2026-08-14)

The focused v4 earth/wood pass is complete as non-destructive evidence. New
`damp_earth_v4_albedo.png` and `weathered_wood_boards_v4_albedo.png` files are
1 024 × 1 024 RGB PNGs with ImageGen source paths, prompts and SHA-256 recorded
in the art manifests and asset registry. The deterministic painterly gate is
14/14 for all discovered v2/v3/v4 candidates; dedicated Godot still and 27-cell
near/mid/far × FOV 65°/75°/90° Metal/Forward+ captures are clean and reproducible.

Independent visual review keeps both v4 replacements at **HOLD/REWORK**:
earth's painted rut bands/stones risk duplicating authored road relief and
wetness geometry, while wood's dark seams/knots risk regular triplanar stripes
and expose the shared wood-owner problem. Runtime v1 mappings, shader, saves,
collisions and narrative state are unchanged; v3 remains the safer comparison
baseline. The next art gate is relief-only versus relief+wetness A/B for earth,
wood owner/scale separation, and a 20–30 m traversal/no-repeat review. No art
lock is declared. Final graphify after the receipt/documentation sync reports
6 447 nodes, 8 807 edges and 503 communities; `graph.html` remains skipped over
the 5 000-node visualization limit.

The v5 focused rework is now recorded without superseding that HOLD decision.
`damp_earth_v5_albedo.png` (SHA `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108`)
and `weathered_wood_boards_v5_albedo.png` (SHA
`3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70`) are
1 024 × 1 024 RGB/sRGB candidates; the focused technical verifier is 16/16
overall and the isolated v3↔v5 Metal/Forward+ A/B receipt is 9 sheets with
120/120 relief/contact samples. The A/B technical gate is clean, but earth's
relief-only cells do not yet show a reliable rendered v3→v5 difference, so both
files remain production OPEN and runtime v1 ownership is unchanged. Required
follow-up is a material-owner/scale-safe earth capture plus wood facade/fence/
furniture/end-grain review, then 20–30 m temporal/readability review. No art
lock is declared.

The test-only manifest wording was corrected to derive its acceptance keys from
the selected versions and rerun without overwriting the prior receipt:
`art/texture_candidate_ab_diagnostic_v5/`, manifest SHA
`41cc4702fbca356b7a8e2035251205fc576ebd6f822dec834c159a25f7fbc9e0`, still
9 sheets and 120/120 contact samples.

After the v5 motion-sweep harness and documentation sync, `graphify update .`
rebuilt the current AST graph at 6 634 nodes, 9 014 edges and 519 communities. `graph.html`
remains intentionally skipped because the graph exceeds the 5 000-node visual
limit; `graph.json` and `GRAPH_REPORT.md` are current.

The v5 pair also has a dedicated non-overwriting 27-cell near/mid/far × FOV
65°/75°/90° Metal/Forward+ receipt under
`art/texture_candidate_motion_sweep_v5/`. Day/house/Kara sheets are SHA
`b9c7587602da28d8a62563c99c4e36deeef434725de870745fb52052ae69c2ae`,
`f891abeac5ded356014390bdd14ec452735a45b2348dd5e1f23c1c84bef73a5a` and
`ea56366bcc7ff2bb3013e2ecb9bf66b4d3c72f7eb89802429edc1543dff92f5f`;
the manifest SHA is `003c0be712333fa0aaee5b1a596b119768a09cd0e2cd83b8b3376d014fea4053`.
This is technical spatial evidence only; earth relief-vs-wetness, wood owner
orientation, temporal comfort and art-lock gates remain open.

The post-sweep regression is also clean: `.tools/dotnet/dotnet build
game/Urman.Game.csproj --no-restore --disable-build-servers --verbosity minimal
-nodeReuse:false -m:1` reports 0 warnings/errors, and the elevated
`./eng/verify-godot.sh` completes the import, scene, audio, zone, narrative,
UI, persistence, collision, Chapter 1, full-game and first-person smoke set
without `ERROR:`, `SCRIPT ERROR:`, ObjectDB or RID/resource-leak diagnostics.

## Evidence refs

- `npm run content:check` — PASS, 4 modules / 4 campaigns.
- `npm run test:unit` — PASS, content 68/68 и runtime 102/102.
- `npm run test:architecture` — PASS, 2/2.
- `npm run build` — PASS, 56 modules.
- `npm run test:e2e` — sandbox listen EPERM; approved loopback rerun PASS, 10/10.
- TaskStartSnapshot записан в `10-intent.md`.
- `design_style.md`, `technical_architecture.md`, `decision_log.md`, `narrative.md`, `canon.md` и связанные KB owners синхронизированы с accepted Godot/full-game direction.
- Два ImageGen concept target перенесены под `art/style_refs/` с SHA-256 и explicit non-screenshot status.
- `git diff --check` — PASS после documentation lock.
- Project-local toolchain: .NET SDK 10.0.302, Godot 4.7.1 Mono, Blender 4.5.12 LTS; URLs и SHA-256 записаны в `eng/toolchain.json`.
- `dotnet build Urman.slnx --no-restore ...` — PASS, 6 projects, 0 warnings/errors.
- C# Chapter 1 pack `JsonNode.DeepEquals` frozen JS golden; campaign fingerprint `59ef1e...3097163fd` и все module fingerprints совпадают.
- C# tests — PASS: Core 38/38, Content 12/12. World-primitives suite покрывает scheduler lease/retry/restore/ack, custody conflict rollback и evidence provenance chain; SaveGameV3 suite — portable keyboard/gamepad button/axis input-binding и accessibility roundtrip, range/axis-shape rejection и duplicate rejection; resolver suite — module order, deterministic variants, locale fallback, vocabulary segments, accessibility closure и audio/non-audio equivalence; narrative suite также проверяет `document.open`/`journal.record` в общей presentation/journal state; capability orchestration suite — create/start, save restore, claim-gated teardown и provider-failure rollback; audio production report suite отличает logical voice refs от physical ambience files и сохраняет `OPEN` authoring boundary.
- Execution backlog validation — PASS: `execution_backlog.json` parses as JSON; 7 milestone references, 22 task IDs and all dependency/milestone links are unique and resolvable. The queue keeps `GODOT-003` in progress, `ASSET-005` completed for versioned texture candidates, `ASSET-006` completed for host-independent provenance, `NARR-001` completed, `NARR-002` blocked on external cultural review and `REL-004` blocked until release acceptance.
- `Urman.ContentCli validate` — PASS: 4 modules / 4 campaigns; Chapter 1 `simulate` — 16 deterministic steps/events; full-game `inspect` — 46 authored beats; full-game `simulate` — 46 deterministic steps/events, state hash `2af5bf7f21c387c9514d3dd1d6cc2854849fe7ee61a9d26521605f2c76b8f6d9`.
- Godot headless smoke — PASS: player, пять Chapter 1 zones, old-PC/dialogue UI, main, переходы зон, authored narrative transitions, old-PC clue chain, автоматическое завершение authored contradiction quest, Gulsina/Rinat dialogue effects и `user://` SaveGameV3 roundtrip с восстановлением зоны. Full `./eng/verify-godot.sh` now imports before tests, captures both console and dedicated Godot logs, fails on `ERROR:`/`SCRIPT ERROR:`/ObjectDB/resource-leak diagnostics and passes the scene, audio, zone, narrative, UI, persistence, collision, Chapter 1 and full-game smoke set.
- Journal smoke — PASS: открытие old-PC документа меняет knowledge, но не журнал; явное `save` создаёт одну идемпотентную `runtime.journal` запись, а `JournalUi` разрешает её русский заголовок и текст из compiled content без собственного progression state.
- Full-game document smoke — PASS: four authored 3D targets (Baranov 1967, Tukay 1913, Kazan 1552, pact ledger) open compiled Markdown through `DocumentUi`, apply `openEffects` and `document.open` in the kernel, then record one idempotent `runtime.journal` entry through the typed journal command.
- Player settings smoke — PASS: Godot adapter round-trips FOV, sensitivity, motion-blur/head-bob flags, graphics preset, last input device, portable keyboard/gamepad button/axis bindings и accessibility snapshot; reduced motion overrides head bob, а keyboard/gamepad events автоматически меняют interaction hint между `[E]` и `[A]`. `SettingsUi` загружается как modal owner, применяет FOV/sensitivity/head bob, render scale/MSAA и accessibility controls и принимает key/button/axis remap input.
- Fresh integrated `user://` persistence smoke после добавления `InputBindings` — PASS: Godot записал и восстановил SaveGameV3 из `user://`, включая player settings/remapping path; повреждённый primary recovery остаётся покрыт C# atomic-store tests. Persistence smoke освобождает `Main` и painterly material cache до выхода; verbose run чистый по ObjectDB/RID diagnostics.
- Chapter 1 end-to-end smoke — PASS: 16 authored narrative points проходят через `village_day -> house_old_pc -> fap_clinic -> house_old_pc -> zirat_road -> kara_urman_night`; финальные `clue_do_not_answer_rule=confirmed` и `cliffhanger-hard-cut=completed` проверены в runtime state.
- Full-game Godot flow smoke — PASS: compiled Acts 2–5 entrypoint, 46 authored beats, 12 walkable full-game zones, four physical document-to-journal paths and final `act5_pact_unjust`, `act5_protection_lost`, `act5_truth_price` plus `canonical-tragic-ending` are verified in one shared runtime state.
- Full-game scene/dressing smoke/capture — PASS: 12 authored `PackedScene` wrappers under `game/scenes/zones/fullgame/` now map one-to-one to logical Acts 2–5 zones; `FullGameFlowSmokeTest` requires each traversed transition to load its authored wrapper, its `ProductionDressing` node and the generated character-kit presentation contract, including imported `Idle`/`Tension` playback. `./eng/verify-godot.sh` now performs a fresh `--import` before the headless suite and rejects runtime `ERROR:`/`SCRIPT ERROR:` lines, while `./eng/capture-fullgame-frames.sh` produced six fresh 1 920 × 1 080 frames after the animation playback pass, with hashes recorded in `art/fullgame_frames/README.md`. `CollisionQaSmokeTest` additionally passes layer-1 floor queries and layer-2 proxy isolation in all 12 zones. These remain production-progress evidence, not art acceptance.
- Local performance benchmark — PASS on Apple M4 Pro: `./eng/benchmark-godot.sh` after the bounded stone/fabric owner pass measured 1920×1080 Forward+ at `118.65 FPS` for day street, `144.92 FPS` for house/old-PC and `144.83 FPS` for Kara-Urman edge (p95 frame time `7.559–12.497 ms`). This proves only the local 30 FPS floor; M1/Windows release evidence remains open.
- Fresh desktop debug export — PASS after the tapered foliage-tier source pass: macOS ZIP `04af0c2856897700a62902c4641d6031d30c2cb66e666facb10b3cee529e45d4`, Windows ZIP `f7d7081b772335c2e43bf3b439752d20459d54c5e5b0285b28c60d452c2c3fd3`, Windows executable `4a22ac9959c6814b8128a80c6a7dbb79d51b5afa1f74faecf7e8327e1caa805a`; the export wrapper now invokes `verify-desktop-artifacts.sh`, which checks the extracted Mach-O/PE32+ payloads, .NET DLLs, PCK and ambient manifest/stems. Both console and dedicated Godot export logs contain no `ERROR:`/`SCRIPT ERROR:`. Real Windows-host smoke and performance evidence remain required.
- Desktop artifact verifier slice — PASS: `sh -n eng/verify-desktop-artifacts.sh eng/export-desktop-debug.sh`, `./eng/verify-desktop-artifacts.sh` and `git diff --check`; the verifier extracts both archives, checks the universal Mach-O and PE32+ payloads, both macOS .NET payloads, the Windows `Urman.Game.dll`, the macOS PCK, the Windows embedded PCK and the four routed ambience markers in both payloads. This is structural packaging evidence, not host execution or release signing evidence.
- macOS host smoke — PASS: `./eng/verify-macos-host.sh` extracts macOS ZIP `04af0c2856897700a62902c4641d6031d30c2cb66e666facb10b3cee529e45d4`, verifies the universal arm64/x86_64 Mach-O, launches the embedded PCK and .NET payload on the current macOS host with the default Forward+ renderer and Dummy audio driver, observes `zone-loaded: village_day@arrival` plus the first-person bootstrap marker, and exits cleanly without Godot/runtime/leak diagnostics. Windows host smoke and release signing remain open.
- Superseding desktop export (2026-08-12) — PASS after the bounded stone/fabric owner pass: macOS ZIP `07a414a10ff5a134d53d30eaefd4313f465cd213cdddb7ec9c91ad6c59b657f5`, Windows ZIP `a1dc0747fd93ca8af7f4309d0471e1dc65361eedb196da77253a0b738a5175b5`, Windows executable `090d5f8c840655ed1263783bebfe0f2f3214e43aea34aa1fc2f33a474cc2a75c`. `./eng/export-desktop-debug.sh` and `./eng/verify-desktop-artifacts.sh` pass; extracted packages contain universal arm64/x86_64 Mach-O, PE32+, self-contained .NET payloads, embedded PCK, six candidate textures, ambient manifest and four routed WAV stems. Windows-host execution, signing/notarization and release-hardware performance remain open.
- Superseding macOS host smoke (2026-08-12) — PASS on the fresh macOS ZIP `07a414a10ff5a134d53d30eaefd4313f465cd213cdddb7ec9c91ad6c59b657f5`: `./eng/verify-macos-host.sh` launches the embedded PCK/.NET payload, observes `zone-loaded: village_day@arrival` and the first-person bootstrap marker, and exits without Godot/runtime/leak diagnostics. The run uses the current macOS host and Dummy audio driver; Windows host and M1/Windows performance remain open.
- Current desktop export (2026-08-12) — PASS after the bounded OldPc tower/panel/power-button detail pass: macOS ZIP `43f2bc1142da195347ddea687f71bbf111b8642af7a0f62982bc7c6d026bda64`, Windows ZIP `8a9fb972ef92c47b10bd133c20a1a702d4c392bffd0d589142909eccb2caf3c2`, Windows executable `efa290d42a62d700b403bbcef525660e554d880a5c46c0ddc8e44a859e9bc46f`. `./eng/export-desktop-debug.sh` and `./eng/verify-desktop-artifacts.sh` pass; extracted packages contain universal arm64/x86_64 Mach-O, PE32+, self-contained .NET payloads, embedded PCK, six candidate textures, ambient manifest and four routed WAV stems. Windows-host execution, signing/notarization and release-hardware performance remain open.
- Current macOS host smoke (2026-08-12) — PASS on the current macOS ZIP `43f2bc1142da195347ddea687f71bbf111b8642af7a0f62982bc7c6d026bda64`: `./eng/verify-macos-host.sh` launches the embedded PCK/.NET payload, observes `zone-loaded: village_day@arrival` and the first-person bootstrap marker, and exits without Godot/runtime/leak diagnostics. The run uses the current macOS host and Dummy audio driver; Windows host and M1/Windows performance remain open.
- Superseding HouseA facade receipt (2026-08-12) — PASS as bounded production evidence, art lock OPEN: source/derived environment SHA `b6ac99408e352427bc27aad05feddce8c635b095139442b65dd8b271f45e5148` / `9abb68bea5cfd19ca48438447abc9e37a0eb67ec61be881055de4484f1544918`; Blender verifier confirms 32 environment LOD1 meshes and Godot captures/sweeps were regenerated after import. Static, spatial and temporal hashes are authoritative in the three art README files. No new gameplay collider, narrative state or save behavior was added.
- Superseding desktop/performance receipt (2026-08-12) — PASS: local M4 Pro benchmark remains above 30 FPS (`125.59/144.95/144.99 FPS` for day/house/Kara); fresh package hashes are macOS `7c6d475e95e2bb9474238cc5ac143c3ac052a82763e8471dfa2d87cc3a606d9f`, Windows ZIP `151264f76bbff77834e99face5cec4b58d15083948806b45d07ce0d3d780df0c`, Windows executable `be10ab9e915773e1627f109b68a8935c9750e8692a2ffbb7a17750e34c2b09c3`; structural verifier and current macOS host smoke pass. Windows host and release-hardware performance remain open.
- Persistence boundary — `SaveGameV3` сохраняет scheduler jobs, но не временные leases; schema V3 обновлена на месте, потому что она ещё не выпускалась. V2/browser compatibility path не добавлен, browser localStorage не затронут.
- Resolver boundary — dialogue UI получает русский текст через `Urman.Content.Resolvers.TextResolver`; journal projection разрешает документы и knowledge keys через compiled registries. Audio resolver формирует captions, transcript и non-audio cue; Godot `AudioCueUi` воспроизводит существующий ресурс либо показывает равнозначную русскую подсказку. Четыре deterministic ambience stems теперь импортируются и маршрутизируются `AmbientAudioDirector`, но authored voice/audio assets и финальный mix ещё не произведены.
- Quest capability boundary — `QuestCapabilitySessionOrchestrator` сверяет committed runtime state с `CapabilityHost`, восстанавливает quest sessions из save, откатывает частичное создание и запрещает teardown до release runtime claims. `RuntimeBridge` вызывает reconciliation после lifecycle/quest refresh и при load.
- Blender generators — PASS; environment source/GLB and project-original character source `assets/source/blender/urman_character_kit.blend` (`1439bbba4d972b062b8295a77a2d19f58840c847e9c1f929a44f735f27663dd3`) plus derived `game/assets/generated/urman_character_kit.glb` (`cb5aca34ece44b8adfa8a8240302dc35d3f0627ed74f2f5044bc017d1975ffd5`) are reproducible. `./eng/verify-assets.sh` confirms 32 environment LOD1 variants including the bounded HouseA facade pass and nine character prefixes with 143 matching LOD0/LOD1 meshes, face landmarks, layered clothing and nine `Idle`/`Tension` clips; Godot headless smoke confirms environment LOD0 `0–24m`, environment LOD1 `18–72m`, character LOD0 `0–18m`, character LOD1 `14–48m`, self-fade and both clip playback/switching with no character collision meshes. Facial expression polish, authored audio and mesh collision review remain separate gates.
- Godot style capture pipeline — PASS with fresh Metal/Forward+ capture after the bounded stone/fabric owner pass. Fresh frame hashes: day `2792727dd2b6751a09460af436d589ac8aaf52a3fd9be78050c629ed7b088b62d`, house `7027b66e27d834aa681f834817f4af173c4e90f43cb0593711e5b7f122b0d97d`, forest `fe952ceb6a3ee42d309ffc6b35fe2ff8f8593a20ca5c2e7d60b1a2073bf56c4c`. Technical capture is reproducible and the imported modules exercise the same Godot LOD/collision adapter as the full-game zones, but visual review still rejects final art lock: the forest crown family remains provisional, hero PC/domestic specificity, complete environment family and cultural presentation are not accepted.
- Godot style camera-sweep evidence — PASS as spatial evidence, art lock OPEN: `./eng/capture-style-motion-sweep.sh` builds cleanly and renders 27 real Metal/Forward+ 640×360 cells (near/mid/far × FOV 65°/75°/90°) into three 1 920×1 080 RGBA contact sheets. Fresh hashes: day `c58e535c7bfdeacd67ec1bc115d7c0d69e32a0444f94a471e4e738a81fe4f228`, house `753645b72bebfe8564c5b98c29da4282e5e55c4bb8e032a7277ce793f39a3120`, Kara `5bad3328efc95c19088a98aa842cd498893e1dd5aa55e416c2722467d03fc264`, manifest `3540e56242c6234d85433bf04e8cd1dfdc5ad00af435e2264b8718f0135dba57`. The harness normalizes SubViewport readback to RGBA8 and fails closed on missing imported modules, blank frames or Godot/leak diagnostics. Spatial evidence confirms old-PC focal read and foreground/midground forest separation; temporal motion/head-bob comfort, global v2 activation, greybox geometry, fog/light calibration, cultural review and release-hardware performance remain open.
- Painterly texture candidates — PASS for image/material-owner/capture, art lock OPEN: six exact `_v2_albedo.png` files, prompts, source paths, SHA-256, image-gate metrics and provenance are recorded in `art/texture_candidates_generation.md`, `art/texture_candidates_technical.md`, `art/texture_candidates_qa.md` and `assets/asset_registry.json`. Four v1 mappings plus explicit v2-only stone/fabric owners render in the three mandatory scenes with coverage wood 232, plaster 15, earth 4, foliage 956, stone 2 and fabric 3. This pass does not replace runtime v1 textures and does not declare art lock.
- Focused v3 texture candidates — PASS image gate + test-only capture, art lock OPEN: all six materials (weathered wood, aged plaster, damp earth, pine foliage, mossy stone and old fabric) are recorded with exact ImageGen provenance, final hashes and v3 scene/swatches under `art/texture_candidate_frames/v3/`; the six-material motion sweep is under `art/texture_candidate_motion_sweep/`; runtime v1/v2 ownership remains unchanged. v2 regression capture also passes after harness extension. Remaining gates are traversal tiling, geometry/lighting/fog, cultural review and final art lock.
- Fresh full `./eng/verify-godot.sh` after the texture-stage cleanup repair — PASS twice consecutively: build/import plus scene, audio, zone, narrative, dialogue, old-PC, journal, settings, persistence, collision, Chapter 1 and full-game smoke all completed without ObjectDB/RID leak diagnostics. The test-only painterly cache cleanup now releases managed ShaderMaterial/ImageTexture wrappers before native shutdown; runtime and save ownership are unchanged.
- Ambient audio technical foundation — PASS: `tools/audio/generate_ambient_audio.py` deterministically generates four project-original 24 kHz WAV stems; `game/assets/audio/ambient_manifest.json` resolves every currently routed zone; `AmbientAudioSmokeTest` verifies resource import and switching; desktop playback loops through `AudioStreamPlayer`, while headless mode validates streams without native playback so `./eng/verify-godot.sh` exits without leak diagnostics. This does not close authored voice/ambience mix, captions or cultural listening review.
- Audio production readiness report — PASS: `Urman.ContentCli report --root .` compiles all four campaigns, enumerates eight campaign-level occurrences of the two non-decorative logical voice assets, confirms caption closure `8/8`, transcript closure `8/8`, physical authored voice files `0`, logical refs `8`, missing physical audio files `0`, and ambient manifest files `4/4`. The command intentionally reports authoring status `OPEN`; it is a production boundary check, not authored voice, final mix or cultural listening acceptance.

## Blocked on

Metal recapture теперь доступен и выполнен, art acceptance не пройден. Технический accessibility contract, SaveGameV3 roundtrip, Godot Idle/Tension playback и текущий macOS host smoke закрыты; остаются реальный Windows-host smoke, M1/Windows performance, внешняя accessibility-проверка, authored audio production, cultural review и production-grade art/level pass для Acts 2–5.

## DriftCheckDraft

- Intent: aligned.
- Scope: aligned.
- Compatibility: web remains unchanged oracle; no bridge or V2 reader added.
- New fallback/owner: no fallback; bounded v2-only stone/fabric presentation owners were accepted in `PainterlyMaterialLibrary`; shared compilation disabled project-wide из-за воспроизводимого зависания Roslyn local server на этом host.
- Retirement track: explicit and deferred until acceptance.
- Latest slice: non-destructive Painterly v2 regression plus focused six-material v3 candidates, bounded stone/fabric presentation owners, authored 9×28 road-relief surface/collision cells and bounded puddle clusters in style, Chapter 1 and FullGameZone paths, refreshed baseline and six-material v3 27-cell spatial sweeps, 216-sample temporal/head-bob sweep with render-only collision cleanup, bounded OldPc tower/panel/button geometry, refreshed captures, deterministic test-only Godot cache cleanup, audio readiness reporting, desktop artifact verification, macOS host smoke and the release-gate matrix stay inside the release-evidence boundary; they add no fallback, save compatibility path or web-retirement behavior. `RoadReliefQaSmokeTest`, full `./eng/verify-godot.sh`, collision QA and M4 Pro benchmark pass; latest frame hashes are recorded in the art README files. The v3 sweep is recorded under `art/texture_candidate_motion_sweep/` with clean real Metal/Forward+ logs and wood, plaster, earth, pine, stone and fabric substitutions. Final graphify rebuild after the v3 source/QA pass: 6 330 nodes / 8 667 edges / 492 communities; graph.html is omitted because the node count exceeds the visualization threshold. Temporal evidence is technical only; observed comfort, wetness/specular calibration, geometry/lighting and art lock remain explicit gates.
- Wetness candidate checkpoint (2026-08-14): `WetnessCandidateCapture` and `eng/capture-wetness-candidate.sh` remain isolated test-only evidence. After the first fail-closed receipt exposed day-far clearance and cross-scene collider aliasing, the harness now instantiates each benchmark scene one at a time and requires the expected relief-body instance. The latest real Metal/Forward+ run verifies 5/5 clusters, 15/15 patches, source puddle roughness `0.90`, exact 15 in-memory candidate clones at `0.50`, and three 1 920×1 080 candidate frames. `PuddleFar` and `BoundaryWetPatch` origins were bounded downward only in the benchmark presentation (`0.102→0.092`, `0.105→0.078`); runtime material, shader, collision, save and narrative owners are unchanged. Contact is PASS within ±0.010 m; wetness roughness/specular acceptance and final art remain OPEN. Baseline 27-cell and 216-sample sweeps were recaptured after the same geometry and render-only cleanup. Manifest/README and capture hashes are stored in `art/wetness_candidate/`. Graphify current report is 6 398 nodes / 8 754 edges / 502 communities; graph.html remains omitted over the visualization threshold.
- V6 texture candidate checkpoint (2026-08-14): `damp_earth_v6_albedo.png` SHA `9ef03566bf89c800dc6317028ce1d9dfb6304ed8de70ea35710fa1b22c544e54` and `weathered_wood_boards_v6_albedo.png` SHA `9b1703f44f00a226ccfe79cace0a8231148125d81c3c91db6e9951ab3dfcd55e` are 1 024² RGB/sRGB candidates. The complete v2–v6 image gate is 18/18 PASS. Isolated v5↔v6 A/B is technical PASS with 9 sheets and 120/120 relief/contact samples; the dedicated v6 motion sweep is technical PASS with three 1 920×1 080 sheets and 27 cells per mandatory scene. Earth relief-only separation remains subtle and wood owner/orientation review remains open; runtime v1 mappings and art lock remain unchanged.
- Latest graphify after the ASSET-006 preflight, lighting calibration and documentation sync: 6 730 nodes, 9 102 edges and 530 communities; graph.html remains intentionally skipped over the 5 000-node visualization limit, with graph.json and GRAPH_REPORT.md current.
- Semantic material-owner split (2026-08-14): imported environment meshes now
  use explicit facade/fence/furniture/bark owners with separate scales, and
  generated character clothing resolves the `cloth` albedo descriptor. Full
  Godot smoke, material-owner assertions, 18/18 texture gate, static/27-cell/
  216-sample Metal captures and M4 benchmark pass; visual art lock remains
  OPEN for greybox geometry, end-grain/orientation, clothing readability,
  cultural review and release hardware.
- Latest graphify after the canonical epilogue HouseA code/documentation sync:
  6 757 nodes, 9 129 edges and 522 communities; `graph.html` remains skipped
  over the 5 000-node visualization limit, while `graph.json` and
  `GRAPH_REPORT.md` are current.

- Canonical epilogue HouseA slice (2026-08-14): `fullgame_act5_epilogue` now
  receives the project-original `HouseA_` presentation module under the
  `act5-epilogue-house` variant. Full-game smoke asserts the expected anchor,
  LOD ranges and `wood_facade` owner; fresh capture evidence contains seven
  1 920 × 1 080 frames, with the new epilogue SHA recorded in
  `art/fullgame_frames/README.md`. This remains presentation-only: proxy
  collision is provisional, and authored family, mesh collision, cultural
  review and art lock remain open.

- Fresh M4 Pro benchmark (2026-08-14): day/house/Kara Forward+ scenes measure
  `120.11/119.84/119.90 FPS` with p95 frame times
  `9.650/9.327/9.465 ms`; the local 30 FPS floor remains PASS, while M1,
  Windows and release-hardware evidence remain open.
- Current asset-verifier host blocker (2026-08-14): a fresh `./eng/verify-assets.sh` rerun is OPEN, not PASS. Pinned Blender 4.5.12 exits `SIGSEGV (139)` during Metal backend detection before opening either `.blend` or Python verifier; `--factory-startup`, explicit `--gpu-backend metal`, environment and character inputs, and a no-file startup expression all reproduce the crash. Backtrace and logs are recorded in `art/blender_asset_verifier_host_blocker.md`. No texture, runtime, shader, save or narrative owner was changed; rerun after a toolchain fix or on another host.
- Decision: continue.

## ResumeStateHint

При возобновлении читать `10-intent.md`, этот checkpoint, active goal и
текущий `git status`. Для текущей демо-цели следующий workstream — наблюдаемое
первое прохождение Акта 1, voice/ambience/caption pass, татарский и культурный
review, gamepad/input parity и near/mid/far readability. Authored Acts 2–5,
M1/Windows host evidence и web retirement остаются deferred north-star work,
а не следующими обязательными шагами этой цели.

## First-person interaction smoke checkpoint (2026-08-14)

The Godot smoke set now includes `res://tests/first_person_interaction_smoke_test.tscn`.
It verifies the real production hierarchy rather than direct runtime dispatch:
the camera `InteractionRay` resolves the physical HouseDoor, mapped keyboard `E`
commits the arrival-to-house transition, the ray resolves the old-PC target in
the new zone, and `E` opens the existing `OldPcUi` modal. The test closes the
modal through its existing UI owner and releases the instantiated main scene;
it does not add runtime APIs, narrative state, save compatibility, or a new
input binding. A direct elevated run and the full `./eng/verify-godot.sh` suite
pass with zero build warnings and no Godot/ObjectDB/RID diagnostics. This is
internal wiring evidence only: mouse/gamepad parity, first-time orientation,
comfort and observed playtest remain open.

## Authored village-module checkpoint — 2026-08-14

The bounded ASSET-009 slice rebuilt the project-original modular Blender kit
with `WellA_` (7 LOD0 parts), `WoodpileA_` (4) and `GateA_` (4). The verifier
reports the updated deterministic 47-mesh LOD1 contract. Source/derived SHA-256
are `9f0371c773e106ec0bcd554045ae4b0b0cbc1f49f2aa4aaf8ce61c71c77798f4` and
`a7647bc1156a3770433d7b8a2e8ba25df380cf94376a12737a54f12d058cf37a`; the
host-independent registry preflight reports 36/36 derived and 10 explicit
local-source records. Elevated `./eng/verify-assets.sh` passes both Blender
verifiers and the registry smoke.

Godot `.NET` build and elevated `./eng/verify-godot.sh` pass with zero warnings,
errors or leak diagnostics. `FullGameFlowSmokeTest` now checks exact WellA,
WoodpileA and GateA LOD0/LOD1 family counts plus presentation-only metadata;
the full path still reaches the canonical tragic epilogue. A fresh full-game
capture contains seven 1 920×1 080 frames (Act 2 house SHA
`32c4d9e2bd1e7178f3e962f8b8433a288c92bbcb059aa7037e8c0d9ad641153a`; Act 5
boundary SHA `5b315b75b9ab55ec94d6e712d34a700bf81bb36edde455a4517b703d31df0e92`;
epilogue SHA `ca38cd52b279dafc02e16202d95ac2bee1ccafc80ee06be2aab17ecb2b858938`).
The three fixed-camera style frames were recaptured; the full frame table is
in `art/fullgame_frames/README.md` and `art/style_frames/README.md`.

Local M4 Pro Forward+ benchmark after the added meshes: day/house/Kara
`120.19/119.80/119.91 FPS`, p95 `9.784/9.373/9.427 ms`; the 30 FPS floor is
technical PASS only. GateA is decorative under the PineA anchor and does not
own collision; WellA/WoodpileA are decorative under HouseA. Near/mid/far
traversal, 20–30 m repetition, marker readability, authored family coverage,
cultural review and M1/Windows evidence remain open. Art lock is not declared.

The runtime-backed boundary assertion now finds both authored Act 5 interaction
markers and records GateA horizontal clearance `2.687 m`; all GateA meshes stay
at least `1.0 m` behind both marker positions. This is a spatial ownership
guard, not a screen-space or observed traversal acceptance.

Graphify was refreshed after the source/docs changes: 6 776 nodes, 9 158
edges, 528 communities; `graph.html` remains omitted above the 5 000-node
visualization threshold.

Current desktop packaging receipt (2026-08-14): the rebuilt macOS universal ZIP
has SHA-256 `131888d82a9b1be3362540506d6d3b5f1f131336b55a33569504dd108b739b6d`,
the Windows ZIP `4462f271d4abd1452f3e511961ea2e799d4aa356c9f3fdba5235f37473e4a0f1`,
and the Windows PE32+ executable
`d4985248230ef8f19b38ea0567a279242cd02bc579da0069d5bad436dbaa4750`.
`./eng/verify-desktop-artifacts.sh` passes structural package checks, three
.NET dependency closures, safe archive namespace and atomic receipt binding; the
Windows ZIP has zero `__MACOSX`/AppleDouble/`.DS_Store` entries. The elevated
current-macOS `./eng/verify-macos-host.sh` also passes startup and first-person
bootstrap with Forward+ and Dummy audio. Windows-host execution, Apple
M1/medium-Windows performance, signing/notarization and release acceptance
remain open; this is not web cutover.

After the release-evidence update, `graphify update .` rebuilt the graph with
6 776 nodes, 9 158 edges and 529 communities; `graph.html` remains omitted
above the 5 000-node visualization threshold.

The hardened export/receipt update was followed by another graph refresh:
6 782 nodes, 9 166 edges and 527 communities; `graph.html` remains omitted
above the 5 000-node visualization threshold.

## GLB presentation-contract checkpoint — 2026-08-14

The bounded modular-kit contract slice is now independently revalidated after
review hardening. `GeneratedModularKitContractSmokeTest` uses exact
`HouseA_`/`FenceA_`/`RoadDirt_`/`PineA_`/`TableA_`/`OldPc_`/`WellA_`/
`WoodpileA_`/`GateA_` prefixes and checks 9/9 expected LOD pairs, one-to-one
LOD0↔LOD1 names, exact semantic-owner sets, positive imported-collision
removal counts, hidden `HouseA`/`OldPc` helpers (Godot-normalized from
Blender `-col` names) and zero presentation physics descendants. The adapter
still restricts published visibility to explicit LOD meshes.

The registry now records `env.fence.a` as presentation-only because the
project-original GLB contains no `FenceA-col`; zone floor/path and the host
proxy remain the only walkability owners. No shader, texture, save, narrative,
runtime-kernel or web-retirement path changed.

Fresh `.tools/dotnet` build and elevated `./eng/verify-godot.sh` pass with zero
warnings/errors/leak diagnostics; the new Godot smoke reports 9/9 families.
`./eng/verify-asset-registry.sh`, JSON validation, `git diff --check` and
`graphify update .` pass. Graphify now reports 6 978 nodes, 9 469 edges and
541 communities; `graph.html` remains intentionally skipped above its 5 000
node visualization limit.

Remaining gates are unchanged: Blender host revalidation, authored mesh
collision, near/mid/far traversal and art acceptance, cultural review, audio,
M1/Windows performance, Windows host execution, observed 6–8 hour playthrough
and final web-retirement absence gate.

## Style evidence repair checkpoint — 2026-08-14

The temporal render-only harness was corrected after review found that freeing
whole `CollisionObject3D` nodes removed visible descendants from the benchmark
clone. The current sanitizer preserves those visual meshes, zeros collision
layers/masks and removes only disposable `CollisionShape3D` nodes. Its fresh
Metal/Forward+ receipt records 216 valid samples, visual mesh counts
806/109/826, removed shape counts 286/22/695 and zero active physics-query
owners. The prior sparse receipt remains under
`art/style_temporal_sweep/superseded_pre_visual_sanitization/` and is explicitly
superseded.

The isolated `StyleCalibrationCandidateCapture` also produced six 1 920×1 080
baseline/candidate frames by cloning `WorldEnvironment` and overriding only
presentation lights/fog in disposable OwnWorld3D viewports. Both receipts are
technical candidates, not a runtime style switch or art-lock decision.

After the adapter reliability repair, imported `CollisionShape3D.Shape`
resources are detached before synchronous `Free()`, preventing the two native
Shape3D RID leaks seen on Metal/Forward+. Fresh build and full Godot verifier
pass without warnings, ObjectDB or RID/resource-leak diagnostics. The GLB,
registry, gameplay collision owners, save/narrative state and web boundary are
unchanged.

The final graphify refresh after this evidence/doc synchronization reports
7,077 nodes, 9,601 edges and 538 communities; `graph.html` remains omitted
above the 5,000-node visualization threshold.

## OldPc hero-detail candidate checkpoint — 2026-08-14

The project-original modular kit was rebuilt without regenerating the character
kit. `OldPc_DriveSlot_LOD0/LOD1` and `OldPc_LabelPlate_LOD0/LOD1` are now
presentation-only `prop.oldpc.crt` parts with `collision=none`; the existing
Act 3 layer-2 table/OldPc proxy is unchanged. The deterministic modular
contract is now 49 LOD1 meshes and `OldPc_ 8/8`. Blender and Godot both assert
the exact four new names, their LOD pairing, `lod_source` and collision policy.

Source/derived SHA-256 values are
`4f3aa74be1d34e1cd956806e56fb57e084cbbfc385380042492bac5c66d48b48` and
`c9f9e8d9a036c3fc8ef20dfc736a164fb393eb8194eff0c0e55782547efa2c36`;
registry preflight is 36/36 derived and 10/10 explicit sources. Fresh
`./eng/verify-assets.sh`, C# build, full Godot smoke (including 46-beat
canonical flow), seven full-game frames and three fixed style frames pass.
The two close OldPc frames are recorded in
`art/oldpc_hero_detail_candidate/` and show both details at interaction
distance without visible z-fight or leak diagnostics.

This remains a production candidate, not art lock: mid-distance readability,
FOV/head-bob traversal, repetition, cultural review, M1/Windows performance,
standard full-game visual review and release acceptance remain open.

The post-slice `graphify update .` receipt reports 7,114 nodes, 9,639 edges and
543 communities; `graph.html` remains intentionally omitted above the 5,000-node
visualization threshold.

## OldPc motion/readback checkpoint — 2026-08-14

The OldPc close candidate now has a test-only first-person matrix at near and
mid distance, FOV 65°/75°/90° and `bob_up`/`neutral`/`bob_down` rows. The
initial per-tile SubViewport prototype was rejected after real Metal evidence
showed cleared later tiles. The harness now reuses one isolated OwnWorld3D
world, copies normalized RGBA readbacks before teardown, removes only
CollisionShape3D nodes and leaves shared Shape3D resources to Godot
reference-counting.

Fresh `./eng/capture-oldpc-hero-detail-motion.sh` passes build, import and
Metal 4.0 / Forward+ capture with 2 sheets, 18/18 non-empty tiles, 4/4 exact
hero details in every tile, 1 920 × 1 080 PNGs and luma spans `0.545–0.601`.
Near SHA is `6e8f4bb3729b1b40edde9468bd5bd61476408eb7ade3726fff789312352077b9`;
mid SHA is `47fe7545dbcf2c3ca0227b760f1d211e1e1e52b1a984658fc7053f2d4489f38f`;
manifest SHA is `a7052d3ad7bfe721ea14f69ff8784d69cb39b60423e1ce60beeaadb4915f9073`.

This is stronger technical readability evidence, not art lock, external motion
comfort, observed traversal, cultural review, M1/Windows release evidence or
full-game acceptance. Production runtime, shader/material ownership, collision,
SaveGameV3 and narrative state remain unchanged.

The graphify refresh after this motion/docs slice reports 7,157 nodes, 9,692
edges and 548 communities; `graph.html` remains intentionally omitted above
the 5,000-node visualization threshold.

## Act 1 demo scope checkpoint — 2026-08-14

The current delivery target is now explicitly narrowed to the playable
first-person Act 1 demo. `game/project.godot` launches
`res://scenes/act1_demo.tscn`, whose `Act1DemoRoot` adds only presentation
framing around the existing `Main`/`RuntimeBridge` composition. The dedicated
launch smoke passes, and the Chapter 1 flow smoke reaches all 16 authored beats
through `village_day`, `house_old_pc`, `fap_clinic`, `zirat_road` and
`kara_urman_night` before confirming the `НЕ ОТВЕЧАЙ` / `Конец демо` overlay.

This is a product-scope decision, not a narrative rewrite: Acts 2–5, the
6–8-hour full playthrough, release-host gates and web retirement are deferred
long-term work. Existing full-game scenes, source assets and browser saves are
untouched and remain outside the demo launch path.

Fresh evidence: build 0 warnings/0 errors; `./eng/verify-godot.sh` passes
scene/import, input, persistence, accessibility, Chapter 1 demo launch and
16-beat flow, plus the existing full-game regression suites. `graphify update .`
reports 7,258 nodes, 9,816 edges and 550 communities; `graph.html` remains
omitted above the 5,000-node visualization threshold.

Fresh desktop package receipt after switching the default launch to the demo and
integrating the Chapter 1 lighting baseline: macOS ZIP SHA-256
`4bac33bd57c796c2a27b1223e91c20e50f26fd97466e6c105c5a5dde5da936f4`
(161,580,917 bytes), Windows ZIP SHA-256
`0e9c448e46e4eca0531146ce26d7adf0457b8257e2384c7ea9d7851b014669ee`
(96,818,155 bytes), and Windows PE32+ SHA-256
`838b5d6f3feba46c0b531334b29f432b79bd4a95406c6da74ac9049c3ca54fd9`
(127,503,520 bytes). Structural package verification and current macOS host
smoke pass; Windows host, M1/Windows performance and signing remain OPEN.

## Chapter 1 lighting/fog baseline checkpoint — 2026-08-14

The isolated lighting/fog A/B candidate is now integrated only into the
Chapter 1 `StyleBenchmarkZone` presentation path. Day uses cooler neutral
ambient/fog and a slightly lower directional key; the old-PC house uses a
lower, less saturated warm lamp/fill balance; Kara-Urman uses a restrained
moon/window balance. No geometry, shader, material owner, collision,
SaveGameV3 or narrative state changed, and the full-game zone lighting path is
outside this slice.

Fresh real Metal/Forward+ capture hashes are day
`d6887d013c08340ee6197b6126c4d55b2c10ab0b0e9637f2be2fccdd9700a3d8`, house
`00396d56324728735d479835b28d9309f902a523ae5c991e9751f7314e4ca2b8` and Kara
`3b9fdab9a7fc85f41c834ee6245e904005d22f52622576ec65c46ac711494674`.
`./eng/verify-godot.sh` and `./eng/benchmark-godot.sh` pass with zero build
warnings; visual geometry, repetition, temporal comfort, cultural review,
release hardware and final art lock remain OPEN.

## Active-goal amendment — 2026-08-14

The user has explicitly replaced the active full-game delivery objective with
the Act 1 atmospheric demo. The checkpoint is complete only when the demo
launch, all 16 Chapter 1 beats, shared-state persistence/input smoke and three
mandatory Godot style frames are verified. Acts 2–5 production, full-game
release acceptance and web retirement are deferred and must not be pulled into
this checkpoint by the orchestration queue.

## Act 1 atmosphere and accessibility checkpoint — 2026-08-14

The bounded presentation pass now keeps two ambient players and crossfades
between the five Act 1 zone beds over 0.65 seconds. A logical voice without a
physical recording still exposes its authored caption when subtitles are on and
audio descriptions are off. These changes remain presentation/accessibility
owners; they do not add a second runtime state owner, alter SaveGameV3,
narrative progression or Acts 2–5.

Fresh evidence after the pass: .NET build 0 warnings/errors; Core 38/38 and
Content 12/12; Node content closure 9/9; elevated `./eng/verify-godot.sh`
passes scene/import, input, persistence, accessibility, ambience switching,
old-PC, the dedicated Act 1 launch and the 16-beat Chapter 1 flow; elevated
`./eng/verify-macos-host.sh` launches the universal package at the Act 1
arrival. `graphify update .` reports 7,387 nodes, 9,997 edges and 565
communities; HTML remains omitted above the 5,000-node limit.

The previous package receipt was structurally bound and read-only verified;
the newer receipt below supersedes it after the opening-control change. The
current package receipt is structurally bound and read-only verified:
macOS ZIP `b95be15636c4cd9a385c758bc84661781d24581e15a834f253413cdb1bb6da78`
(161,615,212 bytes), Windows ZIP
`fbad7111e3046a4573fb74cbe524258596b76abcf3ecf3f280ce9b584013e280`
(96,835,469 bytes), Windows executable
`6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
(127,504,832 bytes), receipt
`e0702a7ca808b59c0e97aa7242f68cb0e29e538e781d2a7a961498b1d8c40948`.

Drift check: the slice remains inside the Act 1 scope fence and preserves the
portable kernel/content boundary. The demo is technically playable, but the
stop state is `needs-verification` for observed first-time playtest, authored
voice/final mix, cultural-language review, motion/accessibility review, art
lock, M1/Windows performance and Windows host execution. Full playthrough,
Acts 2–5 production and web retirement remain explicit non-goals here.

## Act 1 input-discovery checkpoint — 2026-08-14

The opening overlay now follows the last input device detected by the
first-person controller. Keyboard/mouse players see WASD, mouse, E and J;
joypad input switches the card to left/right stick, A and Y. The mapped
`interact`/`ui_accept` action or left click dismisses the card and releases the
player's modal lock; no input actions, save fields or runtime narrative owners
were added.

Fresh targeted real-Godot evidence passes the initial keyboard wording, joypad
device switch, `[A]` interaction hint and mapped dismissal. The complete
`./eng/verify-godot.sh` regression suite passes afterward. This is internal
input-discovery evidence only; observed first-time playthrough, full
controller-path parity, motion comfort and external accessibility remain
`needs-verification`.

## Act 1 physical input and package checkpoint — 2026-08-14

`FirstPersonInteractionSmokeTest` now drives the authored HouseDoor with a
real joypad `A` event, verifies the gamepad interaction hint and then opens the
old-PC target with a keyboard `E` event. This remains a deterministic internal
smoke, not an observed first-time usability or controller-parity sign-off.

Fresh evidence after this test extension: .NET build 0 warnings/errors;
`./eng/verify-dotnet.sh` passes Core 38/38 and Content 12/12; Node content
closure 9/9; the targeted Act 1 launch and first-person interaction smokes,
followed by the complete elevated `./eng/verify-godot.sh`, pass. `graphify
 update .` reports 7,396 nodes, 10,011 edges and 564 communities; HTML remains
omitted above the 5,000-node visualization threshold.

Fresh desktop package receipt after the physical input smoke: macOS ZIP
`a3935e08d54aee0668668fbb9a64ed3f2cb69b93011d7036f16ae9032c72cfa5`
(161,616,257 bytes), Windows ZIP
`e018a52673eb473784a9f2532b8eeb3c782a177959be4dbf54623266874bcad8`
(96,835,653 bytes), Windows executable
`6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
(127,504,832 bytes), receipt
`1c6cddfd23c29bf5cd455ba2e30357d4a72c7a62aa518c783645e969342e8907`.
`./eng/verify-desktop-artifacts.sh --check-receipt` and the elevated
`./eng/verify-macos-host.sh` pass; Windows host execution, M1/Windows
performance, signing and observed demo playthrough remain open.

## Act 1 persistent opening-card checkpoint — 2026-08-14

The opening control card no longer auto-dismisses after a timer. Its backing
shade softens for readability, but the player remains modal until an explicit
`E`/`A`/Enter/left-click confirmation. This keeps the first-time control
discovery inside the playable demo rather than making it dependent on timing.
No input actions, save fields, narrative state or Acts 2–5 launch paths changed.

Fresh evidence after the presentation fix: .NET build 0 warnings/errors;
elevated `./eng/verify-godot.sh` passes scene/import, persistence, accessibility,
old-PC, first-person gamepad-A/keyboard-E interaction, the dedicated Act 1
launch and the 16-beat Chapter 1 flow. The dedicated launch smoke explicitly
waits past the former timer window and confirms the intro is still visible and
movement remains locked. `graphify update .` reports 7,397 nodes, 10,012 edges
and 566 communities; HTML remains omitted above the 5,000-node threshold.

Fresh desktop package receipt after this fix: macOS ZIP
`afd577b221e8a4712bdc6d68c8cc2a225f528522db49b715af3f1d98ab8760cf`
(161,616,859 bytes), Windows ZIP
`784fda87aa1a3106e39101593fcf7c3b94b1c1d22a3f0220b5fa6c2052be7531`
(96,835,879 bytes), Windows executable
`6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
(127,504,832 bytes), receipt
`e7003e81904b2c893b4f173c1ec56c0a6cb4687468eeb0a37ab348a56017aec4`.
`./eng/verify-desktop-artifacts.sh --check-receipt` and elevated
`./eng/verify-macos-host.sh` pass; Windows host, M1/Windows performance,
authored audio, cultural review and observed first-time playthrough remain
open.

## Act 1 authored day-street prop checkpoint — 2026-08-14

The current demo-focused visual slice replaces the two most obvious day-street
greybox hero props with project-original `WellA_` and `WoodpileA_` modules.
`StyleBenchmarkZone` attaches them through the presentation-only adapter at
`(-4.9, 0, 4.6)` and `(5.0, 0, 2.3)`; hidden helper boxes remain the only local
collision owners. `SceneSmokeTest` and `StyleFrameCapture` require exact
`WellA_project_original|WoodpileA_project_original` metadata, positive imported
physics-removal counts and zero actual collision descendants.

Fresh evidence: build and elevated `./eng/verify-godot.sh` pass with 0 warnings,
including the 16-beat Act 1 route, first-person A/E input path, persistence,
collision and modular-kit contracts. Metal/Forward+ style hashes are day
`c06331e1daa7657a0faae6874172604d69ec5822d38b87324605d251cf34512e`, house
`a3eb1fab7c6d3f2c89ebdf03b7570d3a363cb2a9c8edfa95adb907e5d7b220c2`, Kara
`862904f0a079e9f2c7611f80d6237938189975d2a42b710f8865f97cca2726c1`.
The fresh package is structurally verified and launches on the current Mac:
macOS ZIP `b60eee45ad882570d386506df02871f97869c52800296cc6e1cf4cbe0534730b`
(161,619,891 bytes), Windows ZIP
`48a6489ffac2361fecd504dc8d02ef8cd7dcbb8d0a2ba9df47bd9bce906a90a1`
(96,837,240 bytes), Windows executable
`6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
(127,504,832 bytes), receipt
`30d4f60d7f84177e5f75eac16a1fa07739d8a904fc5ed81cf2573d83e5ca7071`.
`graphify update .` reports 7,418 nodes, 10,039 edges and 557 communities;
HTML remains intentionally omitted above the 5,000-node limit.

This remains a bounded presentation candidate for the Act 1 demo, not an art
lock: road/forest greybox repetition, near/mid/far traversal, authored audio,
cultural review, external accessibility, M1/Windows performance and observed
first-time playthrough remain open. Acts 2–5 production and web retirement stay
outside the current goal.

## Act 1 diegetic wayfinding checkpoint — 2026-08-14

The existing day-street signpost now has a muted `Label3D` reading `ФАП`,
physically attached to the board with depth testing and no billboard behavior.
This gives the first-person route one readable in-world landmark without adding
a quest-marker HUD, route-state field, interaction ID or save owner.
`SceneSmokeTest` and `StyleFrameCapture` require the exact `fap` landmark
metadata. Build, full `./eng/verify-godot.sh`, static style capture and the
27-cell near/mid/far × FOV sweep pass on Metal/Forward+; the day frame is
`dd6f9e1d43dea0c7de8434659633eee410bc937e7a78e43a9196b3a8772f089d` and the
sweep contact sheets are recorded in `art/style_motion_sweep/`.

This improves route readability but does not close observed first-time
wayfinding, cultural review, temporal comfort, art lock or release-host gates.

## Act 1 demo package receipt after wayfinding — 2026-08-14

The package was rebuilt after the physical `ФАП` landmark landed. Structural
verification and the elevated current-macOS host smoke both pass. The receipt
is bound to the fresh artifacts: macOS ZIP
`d518c32f02aa8a64a5445005aa088f428d8ed2b3732b9b49c5c63ac55d701e3e`
(161,621,232 bytes), Windows ZIP
`462d053b3ceeddb09a1dbae7d93524516e830ef0b3176b08bc37012693596a44`
(96,837,788 bytes), Windows executable
`6799e5f94b3c1136d79ccbce9954dc2d52a60dd0a8bee8d0a84f42f13969826e`
(127,504,832 bytes), and receipt
`89134708e766f7fc89f9187d83ec55d65c34c5100bf9099cd142847cbb8b8a05`.
`./eng/verify-desktop-artifacts.sh --check-receipt` reports 203 Windows
payload files; `./eng/verify-macos-host.sh` observes `village_day@arrival` and
the first-person bootstrap. This is package/host evidence only: Windows host,
M1/Windows performance, authored audio, cultural review and observed first-time
wayfinding remain open. Current graph receipt: 7,421 nodes, 10,042 edges, 556
communities; HTML is omitted over the graphify size threshold.

## Act 1 physical corridor checkpoint — 2026-08-15

The demo now proves one internal first-person detective loop rather than only
logical dispatch. `Act1FirstPersonCorridorSmokeTest` instantiates the dedicated
demo entrypoint, uses the production ray and mapped `E` input for every handoff,
opens the official notice, saved message, Татарвики source and edge sketch in
the shared `DocumentUi`, commits Rinat's `alerted` state through the real
dialogue UI, and reaches the Kara-Urman `НЕ ОТВЕЧАЙ` state. The test is part of
`./eng/verify-godot.sh`, which passes with 0 build warnings/errors.

The physical evidence targets carry explicit document IDs; the house has a
presentation-only empty-chair/coat/radio cue for Rinat's off-screen warning.
This is a bounded demo slice, not a final character or staging decision.
Observed first-time pacing/wayfinding, gamepad parity with human players,
authored voice/mix, cultural review, M1/Windows performance and art lock remain
open. Acts 2–5 are still deferred and untouched by this slice.

## Act 1 demo package after physical corridor — 2026-08-15

The fresh export includes the first-person corridor test and physical document
bindings. Structural desktop verification and elevated current-macOS host
smoke pass: macOS ZIP `82c6b682af0ec633151922a86fd97025becc08662af2bf2dcad5750924d7e64f`
(161,648,302 bytes), Windows ZIP
`7ead35eb92e646af7c3c2c82ae71adfe75fe82a57b1af27b834eaf99db61f5be`
(96,850,304 bytes), Windows executable
`d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`
(127,506,120 bytes), receipt
`df8342eb8c482f2f6b4714a2777b86b7b0ad4b0a838a33f6e3fc1ef2e507e812`.
Windows host, release hardware, authored audio, cultural review and observed
first-time playthrough remain open; this is not art lock or full-game release
evidence.

## Post-RID-cleanup Act 1 package refresh — 2026-08-15

The presentation adapter no longer manually disposes shared imported
`Shape3D` subresources. The isolated modular-kit contract smoke and full
Godot suite pass without the previous two-RID leak. Current structural package
receipt: macOS ZIP
`96d63b2130e46b8921120ffc6194d33987811c3baf7243c7fa56de35128542d5`
(161,648,202 bytes), Windows ZIP
`340c0ab989173da2ea1b33fdc10dd0ab0e793ac79993dab9556c2357e10891de`
(96,850,207 bytes), Windows EXE
`d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`
(127,506,120 bytes), receipt
`0d468d974ea6aef5674e86cc445edc9fe0490b6c00d0aa8e96d84012fbbf2bd4`.
Windows-host execution and release-hardware performance remain open.

## Physical journal handoff package refresh — 2026-08-15

The corridor now presses the real `DocumentUi` `В журнал` button for the FAP
death notice and asserts the shared journal entry before continuing. Fresh
desktop package receipt: macOS ZIP
`07ca63e17bd825d31635b249e3d066c6d6e710115315af628410a84c2ae56582`
(161,649,874 bytes), Windows ZIP
`1335c505a9e56a34ed75daff1de64ad99ce0b45ed2525c10586bf109b2fdea3c`
(96,850,912 bytes), Windows EXE
`d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`,
receipt `a56f2effe013bede9c3d0039cf3f9a7fb36b2108ca1a863d9513a894348e64a1`.
Human-facing playtest, authored sound, cultural review and release-hardware
checks remain open.

## Act 1 first-objective cue package refresh — 2026-08-15

The opening card now states the presentation-only first goal — reach the house
and check the old computer — and shows `Esc/Start — меню` alongside the detected
keyboard/gamepad controls. No RuntimeBridge, quest state, save or narrative
owner changed. Fresh Godot smoke, desktop structural receipt and elevated
current-macOS host smoke pass. Current package hashes: macOS ZIP
`7a7187719b32516aaefa581b179e1182542ce85ceb99e9233cede29943a22eb5`
(161,649,937 bytes), Windows ZIP
`ad92d31b4142095134ca677bdb3008362998e4f3e87cab503b003055ec08dae2`
(96,850,962 bytes), Windows EXE
`d7dd84df20bf75b4bf834307a42689069da961290046512a599eaa1554163375`
(127,506,120 bytes), receipt
`6f5c8c0e422c4ad2d6ef7d7ee31b0f206b417108a8b8783361820d4c03128542`.
Observed first-time playtest, authored sound, cultural review, release hardware
and art lock remain open. Graphify receipt: 7,479 nodes, 10,122 edges, 561
communities; HTML omitted above the visualization threshold.

## Act 1 startup graphics/performance repair — 2026-08-15

`FirstPersonController._Ready` now applies the declared default `medium`
graphics profile before the first frame. The actual viewport starts at 0.90 3D
scale with 2× MSAA, and `Act1DemoLaunchSmokeTest` fails if this contract drifts.
The full Godot suite passes. A real full-entrypoint probe on the local Apple M4
Pro reports about 120 FPS in Forward+, Forward Mobile and Compatibility; target
M1/Windows hardware still requires evidence.

Fresh packages after the repair: macOS ZIP
`5938dd4f82ff02968228d834e6fe3a04570fb9ceb8cf14970fa3087605c9fe9d`
(161,657,803 bytes), Windows ZIP
`b70a8323b2ca4e26f524ea3b944ceab5714318af94a672cfd16d69000129c578`
(96,855,264 bytes), Windows EXE
`a2f00debbc0e347e47f32c3ba709dcd4e8f509de9ded3fba87758f0833e89ee2`
(127,507,360 bytes), receipt
`cb2abfb5c73e974d966c83a30c2bd898a79c0150a6cf99e358c7b329c939f3dd`.
Current graphify receipt: 7,488 nodes, 10,134 edges, 563 communities; graph
HTML remains omitted above the visualization threshold.

## Safe graphics profile checkpoint — 2026-08-15

The Act 1 demo keeps its normal Medium painterly path but now exposes an
explicit safe launch for a weak/software GPU. `--urman-safe-mode` selects the
existing low scale/MSAA profile and a material branch without triplanar
albedo reads; `eng/run-act1-demo-safe.sh` also selects Godot Mobile. No
narrative, save, interaction, collision or Acts 2–5 launch owner changed.

Verification: .NET build 0 warnings/errors; ordinary launch smoke and full
`./eng/verify-godot.sh` pass; real entrypoint probes report Medium 120.00 FPS
and safe Mobile/low 120.29 FPS on the local M4 Pro. Fresh package receipt is
bound to macOS ZIP `db4fa8eab12200dc14597662d941120a6c719ebecf9e8326b12be9e58c3f752d`,
Windows ZIP `73b317b7d1679d9fa72e52fc43ba36811487cd154db9a560bbe606ba08142521`,
Windows EXE `a2f00debbc0e347e47f32c3ba709dcd4e8f509de9ded3fba87758f0833e89ee2`
and receipt `3eaa889b93937132782eed44125b2da3c24640bf1c13e01c7da54d9fa50693a0`.
Target hardware reproduction, Windows host execution and art/playtest gates
remain open.

## Packaged Act 1 performance probe checkpoint — 2026-08-15

`Act1DemoRoot` now owns a diagnostic-only `--urman-perf-probe` branch so the
published macOS main scene can report frame time without a scene-path override.
`./eng/benchmark-act1-demo-package.sh` unpacks the current ZIP to a temporary
directory, runs the real binary and removes the extraction; its optional
`--urman-safe-mode` argument selects Mobile/low materials. Medium and safe
real-window Metal package probes both report about 120 FPS on the local Apple
M4 Pro (medium Forward+, safe Forward Mobile) and exit 0;
`URMAN_ACT1_HEADLESS=1` is only a CPU/startup check.
This remains host-specific evidence, not M1/Windows acceptance or art lock.

## Act 1 physical walkthrough and package refresh — 2026-08-15

The physical walkthrough smoke now drives about 93.05 m through the real
first-person controller and reaches the Kara-Urman cliffhanger without
teleporting the player or dispatching narrative commands. Full Godot verify
passes with this scene included; this only closes a traversability regression
blind spot, not human playtest acceptance.

The latest rebuilt package receipt is bound to macOS ZIP
`b9df0e1ef5fae43d3760d6e28cece250f44614e19c9cded7648c70781062ac4f`, Windows
ZIP `db4780218958dee3ab807f47b29ca7790680865bb0ae58c2ecd31a0eb99eb744`,
Windows EXE `b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
and receipt `f3f238e2dc55c002762553d9384db83268a0cda610226814880fd8ed88b95419`.
The real packaged Metal probe reports 118.00 FPS in both normal Medium
Forward+ and safe Mobile/low on the local M4 Pro; target-host reproduction
remains open.

## Fresh package after interaction-availability CPU repair — 2026-08-15

`InteractionTarget` now receives coalesced main-thread invalidation from
`RuntimeBridge` instead of serializing kernel state in every frame, and the
Act 1 ending detector no longer serializes state from its frame loop either.
The fresh desktop export and `./eng/verify-desktop-artifacts.sh --check-receipt`
pass; macOS ZIP is
`388e896d387b8164327c75e3bce6444902a69c966bbdc1026c6f157fe0061800`
(161,692,897 bytes), Windows ZIP is
`73209ddc5d1e6f1d5e75b6b6340fa2c623c31aec4390404a071658659dd296f4`
(96,875,494 bytes), Windows EXE remains
`b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
(127,508,656 bytes), receipt SHA is
`b4c000c79c16728e7217e3fcd9e8b7002922bbd6c1719a46bbd7c07edcb42cf4`.
Target-host performance is still not proven.

## Act 1 house OldPc presentation slice — 2026-08-15

The playable demo now uses the project-original `OldPc_` GLB module as the
single visible house-PC focal prop. Duplicate procedural CRT/keyboard/tower
pieces were removed from the benchmark, while the existing layer-1 `OldPc`
interaction remains intact. The presentation adapter removes imported collision
descendants before scene insertion; SceneSmoke and StyleFrameCapture require
8/8 LOD meshes, positive sanitation counts and zero module physics.

Fresh verification: .NET build, elevated `./eng/verify-godot.sh`, real
Metal/Forward+ style capture and `./eng/verify-desktop-artifacts.sh
--check-receipt` pass. Current house frame SHA is
`1cc76c7f469a04f4cb064016ba29c7b5a8f6ca61bdcddc41c9ddb1be17f842ae`.

The fresh package is macOS ZIP
`27a7f3c49d41d05449acdfb58e3a0b8be36f252d07c0029e81ca0d7876c58544`
(161,693,895 bytes), Windows ZIP
`5c51fc40db7f3ae8c9a66a9f2f1470c1b19a49ad14b69ce3ce26d63cb682e4d4`
(96,876,105 bytes), Windows EXE
`b444ba41351e5d3cf14893a99d0c3c127e9b09a9b619aaab0a3b267b6bb4a43c`
(127,508,656 bytes). Real package probes on the local M4 Pro report 119.85
FPS Medium/Forward+ and 119.97 FPS safe Mobile/low (a fresh re-probe measured
119.51 FPS in Medium/Forward+). This is production
progress evidence, not art lock or target-host acceptance; Acts 2–5 remain
deferred.

## Kara-Urman night readability calibration — 2026-08-15

Review of the fixed first-person frame found that the previous night values
collapsed the route into a blue-black mass. A bounded presentation-only
calibration now uses cold ambient `7b9096 @ 0.64`, fog `52666d @ 0.0034` with
height density `0.075`, directional energy `0.82` and local `MoonFill`
`8198a0 @ 1.10`. No gameplay light, material, shader, collision, narrative or
save owner changed. Fresh build, full Godot smoke and real Metal/Forward+
capture pass; current Kara frame SHA is
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

The fresh macOS ZIP was launched as a normal window with
`--print-fps --no-auto-performance-fallback`. Godot printed `Project FPS: 120`
for Forward+ and `Project FPS: 119` for Mobile/low. A one-second macOS sample
showed the expected VSync wait in `CAMetalLayer.nextDrawable`, not a CPU-bound
one-FPS loop. A managed/sandboxed launch aborted before the first frame while
opening `user://logs`/Metal state and is excluded from performance evidence.
The original report therefore remains an external reproduction gate: OS/GPU,
editor-versus-ZIP path and complete target-host `--print-fps` output are still
required.

## Continuation checkpoint — 2026-08-24

Slice Card:

- Goal: remove a concrete Act I reverse-arrival ground seam and make the
  full-route visual capture fail closed instead of running indefinitely.
- Parent plan/spec: `docs/aegis/plans/2026-08-15-act1-connected-greybox.md`.
- Files: `Act1ConnectedWorld.cs`, Agent B heightfield/layout constants,
  `Act1DemoLaunchSmokeTest.cs`, full-route capture harness/wrapper, Act I
  layout/weak-point/decision records.
- Boundary: presentation/traversal envelope and test-only QA; no
  `RuntimeBridge`, interaction IDs, save schema, scene ownership or narrative
  changes.
- Verification: project build; dedicated Act I launch smoke; full
  `./eng/verify-godot.sh`; `./eng/verify-dotnet.sh`; graphify update; targeted
  diff-check and no running Godot capture process.
- Stop: visual 360° capture and human review remain `needs-verification`; do
  not claim art lock or ready-demo status.

Outcome: shared ground/heightfield now cover `z=-152…56` (reverse-arrival
framing included), launch smoke asserts the `AgentBExteriorWorld` layer and
`208 m` collider, and both verification suites pass. The first full-route
capture's liveness issue is bounded with a `ProcessFrame` settle window plus a
300-second shell watchdog; no desktop capture was rerun in this slice.

## Continuation checkpoint — 2026-08-24 — FAP owner seam

Slice Card:

- Goal: remove the concrete duplicate FAP exterior that was present in both
  the Agent B village-buildings kit and the dedicated authored FAP kit.
- Files: `AgentBAct1ExteriorLayer.cs`, `Act1DemoLaunchSmokeTest.cs`, Act I
  master-layout/core-greybox/weak-point/decision records.
- Boundary: presentation filtering and fail-closed QA only; keep the local FAP
  interior, interaction targets, route collision, saves and `RuntimeBridge`
  unchanged.
- Verification: .NET build; dedicated launch smoke; full `verify-godot.sh`;
  graphify update; tracked and untracked diff-check.
- Stop: no desktop/windowed capture; first-person FAP forward/back/side review,
  medical/local-context review and art lock remain open.

Outcome: `AgentBAct1ExteriorLayer` suppresses and tags every `Fap_*` mesh before
architecture collision is generated. `FapClinicAuthoredKitPresentation` is
now the sole exterior FAP visual owner; its authored wayfinding board carries
one diegetic `ФАП` label. Launch smoke asserts positive suppression metadata/
count, zero visible Agent B FAP meshes and the label text.

## Continuation checkpoint — 2026-08-24 — Foliage source-board seam

Slice Card:

- Goal: prevent the Agent B foliage source library from remaining visible after
  deterministic planted copies are generated.
- Files: `AgentBAct1ExteriorLayer.cs`, `Act1DemoLaunchSmokeTest.cs`, Act I
  layout/greybox/weak-point/decision/handoff records.
- Boundary: presentation extraction and fail-closed QA only; preserve terrain,
  route collision, interactions, saves and `RuntimeBridge` ownership.
- Verification: .NET build; dedicated launch smoke; full `verify-godot.sh`;
  graphify update; tracked and untracked diff-check; no windowed capture.
- Stop: foliage repetition, near/mid/far composition, cultural review and art
  lock remain open.

Outcome: `AgentB_FoliageKit` is hidden after template collection and records
source-policy metadata; `AgentB_PlantedFoliage` remains the only visible layer.
Launch smoke asserts hidden source, non-empty planted copies and consistent
entry/node metadata.

## Continuation checkpoint — 2026-08-24 — Atmosphere owner routing

Slice Card:

- Goal: remove the concrete double-WorldEnvironment/directional-sun seam
  introduced when the Agent B exterior layer was mounted under the persistent
  connected-world root.
- Files: `AgentBAct1ExteriorLayer.cs`, `Act1ConnectedWorld.cs`,
  `Act1DemoLaunchSmokeTest.cs`, Act I layout/greybox/weak-point/decision/
  handoff records.
- Boundary: presentation-owner routing only; preserve rain, terrain, route
  collision, interactions, saves and `RuntimeBridge` ownership.
- Verification: .NET build; dedicated launch smoke; full `verify-godot.sh`;
  graphify update; tracked and untracked diff-check; no windowed capture.
- Stop: lighting quality, night readability, warm-window balance, cultural
  review and art lock remain open.

Outcome: exterior logical zones use `AgentBEnvironment`/`AgentBSun` and rain;
house/FAP use their local environment with Agent B atmosphere/sun/rain disabled.
Smoke asserts one active WorldEnvironment in both modes.
