# Release-gate matrix — Godot migration

## Действующая приёмка всей игры — 08.10.2026

Текущий объём — [готовая кампания и оба пакета](../tasktracker/07_full_game_integration_2026-10-08.md#5-финальная-приёмка-готовой-игры). Статусы только в [execution_backlog.json](execution_backlog.json). Все PASS/OPEN и артефакты ниже — исторический срез миграции, не свежий выпуск. REL-003 заменена полной приёмкой «За краем снимка» и зависит от всей required-очереди.

| Гейт | Судьба / текущий контракт | Карточки |
|---|---|---|
| Полное прохождение | Новый обычный New Game→эпилог, Q-ROUTE-A/B/C/D; пять миров и 13 фото. Старый 6–8-часовой трагический маршрут заменён | PW-087/094/100, REL-003 |
| Арт/движение/культура/язык/звук/доступность | Сохранены и расширены на всю деревню и пять миров; человеческое принятие отдельно, исторические PASS не переносятся | PW-080–094/099, QA-001, FINISH-08, REL-003 |
| M1/Windows производительность | Required; реальное железо, полная новая сборка, не данные другой машины/пустой сцены | PW-090, REL-001 |
| macOS/Windows самостоятельный экспорт и host запуск | Required; ввод/глифы/saves/GLB/settings и hashes новой кампании | PW-095, REL-002 |
| Подпись/нотаризация и публичная distribution | Не заявлены выполненными; отдельный distribution contract/поручение, не условие самостоятельного кандидата для автора и не разрешение публиковать | Выпускное уточнение до distribution |
| Web retirement | Сохранён как separate_authorization после REL-003; пользовательские browser saves и история защищены | REL-004 |

Дальше — **исторические** таблицы 2026-08-15 и последующих локальных проверок. Их исключение release/M1/Windows из демо не действует для нового полного результата.

Дата обновления: 2026-08-15. Документ фиксирует только проверяемые границы
текущей миграции. Он не объявляет release readiness, art lock или удаление
веб-рантайма.

## Текущая цель приёмки — connected greybox Акта I

| Demo gate | Required evidence | Status | Boundary |
|---|---|---|---|
| Dedicated entrypoint | `game/scenes/act1_demo.tscn`, `Act1DemoLaunchSmokeTest`, `project.godot` points to the demo scene; intro exposes keyboard/gamepad controls, stays modal until mapped A/E confirmation, and uses demo-only zone transition fade | PASS (technical) | Проверяет только запуск первого лица и input discovery; не является desktop release acceptance |
| Playable Act 1 route | `ChapterOneFlowSmokeTest` plus `res://tests/act1_first_person_corridor_smoke_test.tscn` in `./eng/verify-godot.sh`: physical first-person ray/input route through arrival → old PC → ФАП evidence → Rinat dialogue → saved message / Татарвики / edge sketch → zirat → Kara-Urman, with shared document UI and 16 authored beats | PASS (technical) | Не заменяет first-time usability, observed pacing, full controller parity или cultural review |
| Unified Act I territory | One persistent world containing arrival, road, yard/house, village street, FAP, return road, zirat and Kara-Urman approach; 360°/near-mid/far first-person evidence | OPEN — главный blocker | Current `Main.SwitchZone` unloads one benchmark scene at a time; three style frames are not map evidence |
| First Tatar vocabulary projection | `RuntimeBridge.LearnedVocabulary()` → existing journal `ТАТАРСКИЕ СЛОВА`; `JournalFlowSmokeTest` checks `урман` and its meaning | PASS (technical) | Read-only presentation only; native-speaker wording review and first-time comprehension remain OPEN |
| Demo ending | Shared final rule/audio cue plus modal `НЕ ОТВЕЧАЙ` / `Конец демо` after `cliffhanger-hard-cut` | PASS (technical) | Presentation candidate; authored voice, mix and human review остаются OPEN |
| Zone ambience continuity | `AmbientAudioDirector` uses two presentation-only `AudioStreamPlayer`s and a 0.65 s crossfade on real Metal/Forward+; headless smoke verifies both players and all five Act 1 zone mappings | PASS (technical) | Project-original stems and crossfade are a routing/presentation pass, not authored field recording, final mix or cultural listening acceptance |
| Atmospheric/art acceptance | Connected-world greybox first, then authored geometry and real first-person 360°/near-mid/far review; existing benchmark captures are references only | OPEN | Painterly Low-Poly art lock, authored geometry, repetition and cultural review не закрыты |

Акты 2–5, полный 6–8-часовой playthrough, release-class M1/Windows gates и
web retirement явно вынесены за пределы текущей демо-приёмки. Их rows ниже
сохраняются как долгосрочные release gates и не должны трактоваться как
необходимые для запуска этой демо-сборки.

## Закрытые технические основания

| Gate | Evidence | Status | Boundary |
|---|---|---|---|
| C# parity | `./eng/verify-dotnet.sh`; Core 38/38, Content 12/12, 4 modules / 4 campaigns | PASS | Сюжетное состояние меняет только kernel; audio report остаётся OPEN |
| Godot runtime | `./eng/verify-godot.sh` (fresh build/import plus scene, narrative, persistence, collision, first-person ray/input, dynamic Act 1 intro and ambience crossfade smoke; authored old-PC target and fail-closed unknown-ID check) | PASS | Smoke, а не визуальная или 6–8-часовая acceptance; full multi-controller critical-path parity remains open |
| Modular GLB presentation contract | `GeneratedModularKitContractSmokeTest` through `AttachPresentationOnly`; 9 exact `_`-terminated family pairs, one-to-one LOD names, exact semantic owners, positive imported-physics removal counts and zero physics descendants | PASS (bounded) | Published LOD/material/import hygiene only; `FenceA` is presentation-only because the GLB has no `FenceA-col`; Blender host gate, authored mesh collision, traversal and art lock remain open |
| Painterly candidates | `./eng/verify-painterly-textures.sh`; 18/18 discovered v2/v3/v4/v5/v6 candidates, 1024×1024 RGB, seam/clip/saturation pass; v5↔v6 A/B 9 sheets / 120 contacts and v6 27-cell sweep | PASS (technical) | Candidates, не global activation и не art lock; v4 HOLD/REWORK, v5/v6 production OPEN pending relief/wetness, repetition, orientation and human review |
| Material ownership | `PainterlyMaterialLibrary` + `FullGameFlowSmokeTest` + `TextureCandidateFrameCapture`; bounded `wood_facade`/`wood_fence`/`wood_furniture`/`wood_bark` owners and explicit `cloth` albedo owner; candidate scene coverage remains wood 232, plaster 15, earth 4, foliage 956, stone 2, fabric 3 | PASS (bounded integration) | Source shader/albedo contract unchanged; end-grain/orientation, clothing readability, repetition and visual art acceptance remain open |
| Desktop package structure | `./eng/export-desktop-debug.sh`; `./eng/verify-desktop-artifacts.sh`; read-only `./eng/verify-desktop-artifacts.sh --check-receipt`; `build/desktop-artifact-receipt.json` | PASS (structural) | Fresh staging, clean Windows ZIP, exact arches, .NET closure and full `build/windows`↔ZIP payload manifest; unsigned debug packages не заменяют host/performance/signing gates |
| macOS current-host smoke | `./eng/verify-macos-host.sh`; fresh universal ZIP | PASS | Elevated current Mac, Dummy audio; managed sandbox can be environment-blocked by CA-store setup; не M1 evidence |
| Authored road-relief/puddle candidate | `RoadReliefQaSmokeTest`, extended `CollisionQaSmokeTest`, strict test-only wetness matrix v2 (`0.40/0.50/0.60`, 45 in-memory clones, 9 Metal/Forward+ PNGs), and `puddle_silhouette_candidate` (15 mesh-only replacements, 3 Metal/Forward+ diagnostics) | PASS (bounded candidate) | All 15 raw samples pass exact-owner ≤5 mm with no candidate-local alignment. The mesh-selection half closed on 2026-09-14 by measurement rather than by choice: `AddPuddleCluster` is called from `StyleBenchmarkZone` only and the connected world hides every `puddleGeometry` cluster in village_day/zirat_road/kara_urman_night, so the cylinder proxy and its faceted candidate are both out of the shipped Act I; the wet read ships as the authored `urman_wet_village_road_kit` (rut puddles, ditch water, damp shoulders) graded to water 0.38/0.45/0.98, wet_ground 0.56/0.32/0.94 and earth 0.78/0.16/0.70. Wet readability of the shipped surfaces at player height and in motion, traversal and cultural level-art review remain OPEN |
| Authored village modules and OldPc hero detail | `verify_modular_environment.py`, `GeneratedModularKitContractSmokeTest`, `FullGameFlowSmokeTest`, OldPc close capture and `eng/capture-oldpc-hero-detail-motion.sh`; WellA/WoodpileA/GateA plus OldPc DriveSlot/LabelPlate and 49-mesh LOD1 contract; runtime GateA marker clearance 2.687m | PASS (bounded candidate) | Decorative presentation only; the 18-tile OldPc near/mid/FOV/head-bob receipt strengthens readability evidence but is not observed traversal/comfort or art acceptance; repetition, authored family coverage, cultural review and art lock remain open |
| Act 1 day-street authored props | `StyleFrameCapture` + `SceneSmokeTest` require `WellA_project_original|WoodpileA_project_original`, presentation-only instances and zero imported physics descendants; fresh Metal/Forward+ style frame recorded | PASS (bounded candidate) | Improves the demo's well/woodpile silhouettes only; invisible helper boxes remain collision owners, road/forest greybox repetition, near/mid/far traversal, cultural review and art lock remain OPEN |
| Act 1 diegetic wayfinding landmark | Existing day-street signboard carries non-billboarded `Label3D` `ФАП`; scene/style smoke require exact `fap` metadata; 27-cell Metal/Forward+ sweep remains clean | PASS (bounded candidate) | In-world readability aid only; observed first-time orientation, cultural review and art lock remain OPEN; no floating quest marker or route-state owner added |
| Act 1 house OldPc presentation | `SceneSmokeTest` and `StyleFrameCapture` require `OldPc_project_original`, exact 8/8 LOD pairs, positive imported-collision sanitation, zero module physics descendants and the existing layer-1 `OldPc` interaction target; fresh Metal/Forward+ house frame recorded | PASS (bounded candidate) | Replaces procedural CRT/keyboard/tower visuals only; gameplay interaction, documents, table and lamp remain; close readability, observed route, cultural review and art lock remain OPEN |
| Asset registry provenance | `./eng/verify-asset-registry.sh`; 36/36 derived files and 10/10 explicit local sources hash-match; negative fixtures fail closed | PASS | Host-independent registry evidence; does not replace Blender/GLB verification |
| Blender asset verification | `./eng/verify-assets.sh`; elevated real-driver run passes both verifiers, managed sandbox reproduces typed `HOST_TOOLCHAIN_BLOCKED` during Blender Metal startup | PASS (elevated) / OPEN (managed sandbox) | No fallback is used; retain host/toolchain receipt and revalidate on release host |

## Открытые обязательные gates

| Gate | Required evidence | Current status | Do not claim |
|---|---|---|---|
| Temporal motion / head-bob comfort | `./eng/capture-style-temporal-sweep.sh` provides 216 real Metal/Forward+ frame samples, FOV 65/75/90 and head-bob/reduced-motion rows; the render-only clone preserves visual descendants and removes only collision shapes. `style_calibration_candidate` records the six-frame A/B that informed the integrated Chapter 1 baseline. The OldPc candidate adds 18 near/mid/FOV/head-bob detail tiles. | PARTIAL | Technical receipts are fresh; observed traversal, external comfort, visual calibration and art review remain OPEN |
| Production art lock | Three accepted Godot scenes, authored geometry/hero PC/forest family, fog/light and cultural presentation review | OPEN | Текущие кадры остаются production-progress |
| Authored audio | Licensed/project-original voice and final ambience mix, captions/transcripts/non-audio cues | OPEN | Four deterministic WAV stems — только routing foundation |
| Cultural / language review | Татарский-speaking and religious/cultural consultant sign-off for keys, Timur, folklore, historical framing | OPEN | Narrative lock не равен cultural lock |
| External accessibility | Real-user motion/readability/input/caption/audio-description review on release-shaped build | OPEN | Technical SaveGameV3/settings smoke не равен external review |
| M1 / Windows performance | Raw 1080p frame-time on Apple M1-class and medium Windows PC, low/medium/high | OPEN | M4 Pro benchmark не закрывает release hardware |
| Windows host execution | Launch fresh Windows package; save/input/glyph/GLB checks | OPEN | PE32+ package structure не равна host smoke |
| Full playthrough | Observed 6–8-hour path from arrival to canonical tragic epilogue | OPEN | 46-beat deterministic simulation не равна player acceptance |
| Web retirement | `./eng/check-web-retirement.sh --assert-absent` after all gates | BLOCKED | Legacy source/localStorage не удалять сейчас |

## Current artifacts

- macOS ZIP: `/Users/unterlantas/Documents/GitHub/URMAN/build/macos/URMAN.zip`
  SHA-256 `e38176747d9eecaf3fc25207d34a138197200e18dfc90c984279f9cfd58b7751`
  (161,721,643 bytes; export 2026-08-15; includes the Act 1 demo entrypoint,
  authored old-PC interaction, two-player ambience crossfade, logical-voice
  caption fallback, persistent keyboard/gamepad opening card and the physical
  gamepad-A/keyboard-E smoke path plus the session-only slow-startup low-profile
  rescue and `--print-fps` diagnostic branch).
- Windows ZIP: `/Users/unterlantas/Documents/GitHub/URMAN/build/URMAN-windows-x86_64.zip`
  SHA-256 `b93fcc10ca090dca5ba3e780aa403a3e3e533226c2ed4a84d138379a3d8ea56c`
  (96,888,584 bytes; export 2026-08-15; Windows ZIP metadata audit: 0
  `__MACOSX`, 0 AppleDouble, 0 `.DS_Store`).
- Windows executable: `/Users/unterlantas/Documents/GitHub/URMAN/build/windows/URMAN.exe`
  SHA-256 `ca2ccde016413ea88a77146676716643657bd0f343f39ecff8805e063582440e`
  (127,508,992 bytes; export 2026-08-15).
- Current receipt: `./eng/verify-desktop-artifacts.sh --check-receipt` passes ZIP/package checks,
  universal macOS arm64/x86_64 Mach-O, Windows PE32+ x86-64, embedded PCK/.NET
  payloads, three .NET dependency closures, four routed ambient stems and an
  atomic SHA/size-bound `build/desktop-artifact-receipt.json` (SHA-256
  `567e45835cbdbcf7c1ffbeafbf0933d65ce82a19fe367014c3d4e309a30c47be`). The hardened
  export uses fresh staging, lock serialization and clean Windows ZIP metadata.
  `./eng/verify-macos-host.sh` passes on the elevated current Mac with Forward+
  and Dummy audio; Windows host execution, M1/Windows performance and
  signing/notarization remain open.
- Texture previews, prompts, per-file hashes and scene coverage: [texture candidate QA](art/texture_candidates_qa.md), [generation manifest](art/texture_candidates_generation.md), [v5 technical QA](art/texture_candidates_v5_technical.md), [v6 technical QA](art/texture_candidates_v6_technical.md), [v3↔v5 A/B receipt](art/texture_candidate_ab_diagnostic_v5/README.md), [v5↔v6 A/B receipt](art/texture_candidate_ab_diagnostic_v6/README.md), [v5 motion sweep](art/texture_candidate_motion_sweep_v5/README.md), [v6 motion sweep](art/texture_candidate_motion_sweep_v6/README.md), [full-game captures](art/fullgame_frames/README.md).
- Authored module candidates and source/derived hashes: [module receipt](art/authored_module_candidates_2026-08-14.md), [asset registry](../../assets/asset_registry.json), [full-game captures](art/fullgame_frames/README.md), [style frames](art/style_frames/README.md).
- Current Blender verifier host blocker and reproducible crash evidence: [blender asset verifier host blocker](art/blender_asset_verifier_host_blocker.md).

## Next owner order

1. Production art/level owner reviews the isolated wetness matrix v2 together
   with the mesh-only puddle-silhouette v1 diagnostic, then decides a separate
   production geometry slice; no texture-only workaround for silhouette,
   folds, collision or canopy density.
2. Audio/narrative owner records or licenses authored voice and completes the
   final mix plus captions/non-audio cues.
3. Татарский-speaking and religious/cultural consultant reviews language,
   mosque/Timur framing, folklore roles and historical documents.
4. QA runs external accessibility, M1/Windows performance, Windows host smoke
   and the observed full playthrough.
5. Only after all rows above are PASS may release owner run the absence gate and
   make a separate decision on web-runtime retirement. Browser localStorage is
   never deleted by this matrix.
