# Манифест источников handoff

Срез и состав: **2026-09-03**. В архиве нет полного репозитория. Каждый перечисленный ниже файл включён намеренно; путь в левой колонке — путь внутри ZIP. `A0/A1/B/E/H/G` — уровень авторитета, описанный в легенде.

## Легенда

| Код | Смысл |
|---|---|
| `A0` | Нормативный канон/ограничение: имеет высший приоритет в своей области |
| `A1` | Рабочий canon/decision/scope/release документ; конфликт нужно назвать, а не скрыть |
| `B` | Текущий working-tree source или verification source; показывает реализацию/контракт, не гарантирует acceptance |
| `E` | Текущее наблюдаемое evidence; receipt и кадры нужно читать вместе, с ограничениями integrity/coverage |
| `H` | Исторический benchmark, target, provenance или эксперимент; не переопределяет текущий source |
| `G` | Сгенерированный handoff этого пакета; контракт чтения и ответа для Pro |

Классы содержимого: `source` — канон/документ/код/данные; `evidence` — receipt или текущий capture; `benchmark` — target или историческое сравнение; `experiment` — неканонический отчёт; `generated handoff` — authored package.

## Authored handoff (generated handoff)

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/README.md` | G | generated handoff | Инструкция загрузки, порядок доверия, dirty-tree warning и failed capture-gate |
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/CURRENT_STATE.md` | G | generated handoff | Сводка текущих доказанных возможностей, greybox-состояния, принятых/отклонённых изменений и unknowns |
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/ARCHITECTURE_AND_OWNERS.md` | G | generated handoff | Карта владельцев runtime/content/presentation и неразрешённых duplicate risks |
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/GAPS_AND_RELEASE_GATES.md` | G | generated handoff | Восемь zone gates, системные gaps и границы MVP/public/post-MVP |
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/SOURCE_MANIFEST.md` | G | generated handoff | Полный список вложений, их роль и authority level |
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/PROMPT_FOR_GPT_5_6_PRO.md` | G | generated handoff | Exact paste-ready задача для решительного ответа Pro |
| `docs/handoffs/chatgpt_5_6_pro_mvp_direction_2026-09-03/EXPECTED_RESPONSE_SCHEMA.md` | G | generated handoff | Строгий формат, headings и tracker columns для ответа Pro |

## Канон и навигация по проекту

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `AGENTS.md` | A0 | source | Обязательные правила URMAN, канонический checkout, Luna-only, graphify и запрет молчаливых ретконов |
| `URMAN_Codex_Context.md` | A0 | source | Нормализованный канон и MVP-бриф; разделы 1–17 выше сырого приложения A |
| `docs/urman_knowledge_base/README.md` | A1 | source | Навигация KB и текущая граница connected Act I greybox |
| `docs/urman_knowledge_base/canon.md` | A1 | source | Hard/soft canon: Айдар, Кырлай, Марат, pact, исламская рамка, персонажи и запреты |
| `docs/urman_knowledge_base/decision_log.md` | A1 | source | История решений о connected world, Agent B, FAP owner, capture watchdog и physical gates |
| `docs/urman_knowledge_base/open_questions.md` | A1 | source | Нерешённые противоречия, first-person/language/cultural/platform вопросы; нельзя выдавать их за facts |
| `docs/urman_knowledge_base/weak_points.md` | A1 | source | Честный ledger незакрытых visual/runtime/playtest/release рисков и предел старых evidence |
| `docs/urman_knowledge_base/design_style.md` | A1 | source | Painterly Low-Poly направление, «обычность сначала, неправильность потом», культурные ограничения и art-lock rule |
| `docs/urman_knowledge_base/assets.md` | A1 | source | Asset inventory, provenance и ограничения candidate/generated ассетов |
| `docs/urman_knowledge_base/gameplay.md` | A1 | source | Core detective loop, physical interactions, old PC, vocabulary, sound-first pacing и safe zones |
| `docs/urman_knowledge_base/mvp_scope.md` | A1 | source | Must-have Act I beats, cliffhanger и явный out-of-scope Acts II–V/combat/open world |
| `docs/urman_knowledge_base/playtest_plan.md` | A1 | source | Human playtest gates, completion/comprehension targets и platform/accessibility expectations |
| `docs/urman_knowledge_base/mindmap.md` | A1 | source | Cross-domain dependency/risk map проекта и MVP-critical tags |
| `docs/urman_knowledge_base/act1_demo_handoff.md` | A1/H | source/benchmark | Последнее описание connected Act I controls, route, owners и известного incomplete state; исторические claims датированы внутри |
| `docs/urman_knowledge_base/mvp_completion_handoff.md` | A1/H | source/benchmark | История completion framing и explicit warning, что старые sections не являются текущими instructions |
| `docs/urman_knowledge_base/release_gate_matrix.md` | A1 | source | Матрица technical/art/audio/cultural/accessibility/platform/playtest gates и их OPEN/PASS boundaries |
| `docs/urman_knowledge_base/project_brief.md` | A1 | source | Product intent, audience, tone и вертикальный срез, который Pro должен защищать |
| `docs/urman_knowledge_base/narrative.md` | A1 | source | Act I trail, персонажи и dramaturgy от arrival до `Не отвечай` |
| `docs/urman_knowledge_base/language_learning.md` | A1 | source | Татарский mixed-speech mechanic, 5–7 слов, comprehension и обязательность консультации |
| `docs/urman_knowledge_base/old_pc.md` | A1 | source | Old PC как narrative/archive hub, не OS и не отдельный state owner |
| `docs/urman_knowledge_base/village_lore.md` | A1 | source | Кырлай, социальная память, деревенские правила и локальная фактура |
| `docs/urman_knowledge_base/mythology.md` | A1 | source | Татарский фольклор как система территории/правил, не bestiary; respectful MVP manifestations |
| `docs/urman_knowledge_base/technical_architecture.md` | A1 | source | Godot/C#/.NET architecture, Core/Content boundary, RuntimeBridge sole writer, SaveGameV3 и presentation ownership |
| `docs/urman_knowledge_base/route_navigation_graph.md` | A1/H | source/benchmark | Route intent и provenance; читать как исторический navigation context после перехода к first-person 3D |
| `docs/urman_knowledge_base/route_navigation_product_lock.md` | A1/H | source/benchmark | Старый route product lock; нужен для понимания superseded решений, не для возврата web/runtime |
| `docs/urman_knowledge_base/village_route_art_spec.md` | A1/H | source/benchmark | Пространственные/art требования маршрута и их связь с нынешним визуальным gate; не второй runtime owner |
| `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md` | A1 | source | Авторитетный spatial contract восьми visual zones, reverse/near-mid-far/ground/cultural acceptance |
| `docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md` | A1 | source | Art direction, production order, source-vs-actual boundary и human motion-frame art lock |

## Текущий runtime и исходники владельцев

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `game/project.godot` | B | source | Реальный Godot entrypoint, viewport, inputs, renderer/runtime settings и scene configuration |
| `game/scenes/main.tscn` | B | source | Shared production scene: Main, RuntimeBridge, player, old PC, dialogue, audio, journal, settings, document UI |
| `game/scripts/Act1ConnectedWorld.cs` | B | source | Текущий persistent Act I presentation envelope, eight direct visual zones, kit/suppression и exterior collision metadata |
| `game/scripts/Act1WorldLayout.cs` | B | source | Deterministic five logical zone placements, world origins, spawn points и connectors |
| `game/scripts/Main.cs` | B | source | Connected-world opt-in, default one-zone fallback, zone switches, spawn, RuntimeBridge location и ambience routing |
| `game/scripts/RuntimeBridge.cs` | B | source | Единственный runtime narrative/progression/save/vocabulary owner; критичен для architecture-preserving tracker |
| `game/scripts/FirstPersonController.cs` | B | source | Реальный CharacterBody3D movement/look/FOV/settings и presentation-only spawn application |
| `game/scripts/Act1DemoRoot.cs` | B | source | Demo presentation wrapper, start/final card и fade; не должен стать narrative owner |
| `game/scripts/AmbientAudioDirector.cs` | B | source | Единственный continuous ambience router/crossfade owner; placeholder/final-mix gap виден в контексте |
| `game/scripts/PainterlyMaterialLibrary.cs` | B | source | Shared material/shader candidate и preset behavior; не proof of visual acceptance |
| `game/scripts/QuestRuntimeCoordinator.cs` | B | source | Projection of compiled quest definitions, отделённый от kernel state |
| `game/scripts/InteractionTarget.cs` | B | source | Physical target metadata, availability and dispatch path into RuntimeBridge |

## Experiment/provenance

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `docs/experiments/agent_b_act1_world_report.md` | H | experiment | Исторический Agent B standalone report с прежними frame/waypoint claims; явно non-canonical, retired path и не текущая evidence |

## Act I content pack

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `content/campaigns/urman.chapter1/campaign.json` | B | source | Entrypoint, role bindings, narrative order и invariants, включая reveal-not-before для `Не отвечай` |
| `content/modules/urman-chapter1/module.json` | B | source | Module dependencies, provided scenes, characters, interactions, clues, vocabulary и asset IDs |
| `content/modules/urman-chapter1/definitions.json` | B | source | Фактические dialogue/text/document/knowledge/vocabulary/interaction definitions, а не только замысел docs |
| `game/content/urman.chapter1.compiled.v1.json` | B | source | Runtime-read compiled content pack; подтверждает границу между authored data и engine presentation |

## Verification sources (не запускались для этой сборки)

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `game/tests/Act1FirstPersonWalkthroughSmokeTest.cs` | B | source | Узкий реальный first-person/gate-order smoke contract; показывает, что технический PASS не равен human playtest |
| `game/tests/Act1FullRouteCoreWorldCapture.cs` | B | source | Capture frame specs, eight-zone audit, root viewport и receipt schema; объясняет текущую duplicate-view failure |
| `game/tests/Act1DemoLaunchSmokeTest.cs` | B | source | Structural launch/connected-owner checks и их границы |
| `game/tests/AmbientAudioSmokeTest.cs` | B | source | Manifest/import/routing smoke contract, не final audio mix |
| `game/tests/DialogueFlowSmokeTest.cs` | B | source | Dialogue/vocabulary/Rinat shared-kernel contract |
| `game/tests/OldPcFlowSmokeTest.cs` | B | source | Old PC document/evidence progression contract |
| `game/tests/PersistenceSmokeTest.cs` | B | source | Save/load contract and `SaveGameV3` expectations |
| `game/tests/PlayerSettingsSmokeTest.cs` | B | source | Settings/input/FOV contract and comfort baseline |
| `eng/capture-act1-full-route-core-world.sh` | B | source | Wrapper acceptance rules for frame count/hash/viewport/zone/capture-process; supplied run failed unique-hash gate |
| `eng/verify-godot.sh` | B | source | Broad verification entrypoint and included tests; not run in this handoff by instruction |
| `eng/verify-dotnet.sh` | B | source | .NET verification entrypoint; parent supplied build result, this worker did not run it |

## Asset manifests (без GLB/Blend binaries)

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `game/assets/models/act1/urman_kara_forest_edge_kit_manifest.md` | B/H | source/benchmark | Kara kit component intent, dimensions/provenance и отсутствие runtime acceptance |
| `game/assets/models/act1/urman_fap_clinic_kit_manifest.md` | B/H | source/benchmark | FAP geometry/exterior owner intent и ограничения candidate kit |
| `game/assets/models/act1/urman_village_exterior_kit_manifest.md` | B/H | source/benchmark | Village parcel/landmark component inventory and source boundary |
| `game/assets/models/act1/urman_wet_village_road_kit_manifest.md` | B/H | source/benchmark | Road/shoulder/ditch candidate coverage and axis-safe intent |
| `game/assets/models/act1/urman_zirat_roadside_kit_manifest.md` | B/H | source/benchmark | Zirat boundary/roadside/marker intent and cultural approval gap |

## Visual targets (benchmark, не actual result)

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `docs/urman_knowledge_base/art/style_refs/painterly_low_poly_atmosphere_target.png` | H | benchmark | Atmosphere target for comparison; не screenshot текущей сцены |
| `docs/urman_knowledge_base/art/style_refs/painterly_low_poly_geometry_target.png` | H | benchmark | Geometry/material target; не доказательство art lock |

## Текущее first-person evidence: failed capture gate

Источник на диске: `/private/tmp/urman-gpt56-pro-current-capture-20260903/`. В ZIP каждый файл лежит под `visual_evidence/current_act1_360_failed_gate/`. Все 44 PNG и receipt намеренно перечислены, чтобы Pro мог проверить не только красивые направления, но и повторную сериализацию.

| Путь в архиве | Авторитет | Класс | Почему Pro обязан прочитать |
|---|---:|---|---|
| `visual_evidence/current_act1_360_failed_gate/act1_full_route_core_world_receipt.json` | E | evidence | Receipt: 44 frames, 8 zones, 10/10 waypoints, 242.49586 m, real root viewport, hash list; фиксирует failed uniqueness gate |
| `visual_evidence/current_act1_360_failed_gate/arrival_forward.png` | E | evidence | Arrival forward near/mid/far frame |
| `visual_evidence/current_act1_360_failed_gate/arrival_back.png` | E | evidence | Arrival reverse frame |
| `visual_evidence/current_act1_360_failed_gate/arrival_left.png` | E | evidence | Arrival lateral-left frame |
| `visual_evidence/current_act1_360_failed_gate/arrival_right.png` | E | evidence | Arrival lateral-right frame |
| `visual_evidence/current_act1_360_failed_gate/arrival_depth.png` | E | evidence | Arrival depth frame |
| `visual_evidence/current_act1_360_failed_gate/main_street_forward.png` | E | evidence | Main Street forward frame |
| `visual_evidence/current_act1_360_failed_gate/main_street_back.png` | E | evidence | Main Street reverse frame |
| `visual_evidence/current_act1_360_failed_gate/main_street_left.png` | E | evidence | Main Street lateral-left frame |
| `visual_evidence/current_act1_360_failed_gate/main_street_right.png` | E | evidence | Main Street lateral-right frame |
| `visual_evidence/current_act1_360_failed_gate/main_street_depth.png` | E | evidence | Main Street depth frame |
| `visual_evidence/current_act1_360_failed_gate/babai_yard_forward.png` | E | evidence | Babai–Ebi yard forward frame |
| `visual_evidence/current_act1_360_failed_gate/babai_yard_back.png` | E | evidence | Babai–Ebi yard reverse frame |
| `visual_evidence/current_act1_360_failed_gate/babai_yard_left.png` | E | evidence | Babai–Ebi yard lateral-left frame |
| `visual_evidence/current_act1_360_failed_gate/babai_yard_right.png` | E | evidence | Babai–Ebi yard lateral-right frame |
| `visual_evidence/current_act1_360_failed_gate/babai_yard_depth.png` | E | evidence | Babai–Ebi yard depth frame |
| `visual_evidence/current_act1_360_failed_gate/house_exterior_forward.png` | E | evidence | House exterior approach forward frame |
| `visual_evidence/current_act1_360_failed_gate/house_exterior_back.png` | E | evidence | House exterior approach reverse frame |
| `visual_evidence/current_act1_360_failed_gate/house_exterior_depth.png` | E | evidence | House exterior approach depth frame |
| `visual_evidence/current_act1_360_failed_gate/house_interior_forward.png` | E | evidence | House interior forward checkpoint |
| `visual_evidence/current_act1_360_failed_gate/house_interior_back.png` | E | evidence | House interior reverse checkpoint |
| `visual_evidence/current_act1_360_failed_gate/house_interior_left.png` | E | evidence | House interior lateral-left checkpoint |
| `visual_evidence/current_act1_360_failed_gate/house_interior_right.png` | E | evidence | House interior lateral-right checkpoint |
| `visual_evidence/current_act1_360_failed_gate/connective_street_return_forward.png` | E | evidence | Connective return forward; duplicate group member, so not valid directional proof |
| `visual_evidence/current_act1_360_failed_gate/connective_street_return_back.png` | E | evidence | Connective return reverse; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/connective_street_return_depth.png` | E | evidence | Connective return depth; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_exterior_forward.png` | E | evidence | FAP exterior forward; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_exterior_back.png` | E | evidence | FAP exterior reverse; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_exterior_left.png` | E | evidence | FAP exterior lateral-left; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_exterior_right.png` | E | evidence | FAP exterior lateral-right; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_exterior_depth.png` | E | evidence | FAP exterior depth; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_interior_forward.png` | E | evidence | FAP interior forward; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_interior_back.png` | E | evidence | FAP interior reverse; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_interior_left.png` | E | evidence | FAP interior lateral-left; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/fap_interior_right.png` | E | evidence | FAP interior lateral-right; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/zirat_forward.png` | E | evidence | Zirat forward; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/zirat_back.png` | E | evidence | Zirat reverse; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/zirat_left.png` | E | evidence | Zirat lateral-left; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/zirat_right.png` | E | evidence | Zirat lateral-right; duplicate group member |
| `visual_evidence/current_act1_360_failed_gate/zirat_depth.png` | E | evidence | Zirat depth; unique relative to the four-member duplicate group, but insufficient for zone acceptance |
| `visual_evidence/current_act1_360_failed_gate/kara_approach_forward.png` | E | evidence | Kara approach forward; one of five identical views |
| `visual_evidence/current_act1_360_failed_gate/kara_approach_back.png` | E | evidence | Kara approach reverse; one of five identical views |
| `visual_evidence/current_act1_360_failed_gate/kara_approach_left.png` | E | evidence | Kara approach lateral-left; one of five identical views |
| `visual_evidence/current_act1_360_failed_gate/kara_approach_right.png` | E | evidence | Kara approach lateral-right; one of five identical views |
| `visual_evidence/current_act1_360_failed_gate/kara_approach_depth.png` | E | evidence | Kara approach depth; one of five identical views |

## Reading rule for this manifest

`E` текущего capture имеет приоритет над историческими кадрами только как описание того, что было снято 2026-09-03. Его duplicate groups означают failed coverage, а не failed runtime. Никакой `H` benchmark нельзя использовать для замены отсутствующего current proof. Любое утверждение о готовности должно иметь ссылку на конкретный source/evidence и пройти human/cultural/platform gates, перечисленные в `GAPS_AND_RELEASE_GATES.md`.
