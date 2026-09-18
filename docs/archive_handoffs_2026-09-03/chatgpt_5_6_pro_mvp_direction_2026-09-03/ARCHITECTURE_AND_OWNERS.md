# Архитектура и владельцы

Документ описывает observed working-tree architecture на 2026-09-03. Он не утверждает, что все найденные ветви должны остаться. Риски дублирования перечислены явно; этот handoff не удаляет и не переименовывает владельцев.

## Цепочка источника и исполнения

```text
URMAN_Codex_Context.md + AGENTS.md
        ↓ канон/ограничения
docs/urman_knowledge_base/*
        ↓ рабочие решения, scope, gates
content/campaigns + content/modules + compiled pack
        ↓ data-driven narrative/content
RuntimeBridge (единственный state/progression writer)
        ├─ QuestRuntimeCoordinator (читает quest definitions)
        ├─ InteractionTarget (dispatch physical interaction)
        ├─ Old PC / Dialogue / Journal / Document UI (read/present)
        └─ Main.SetWorldLocation → Act1WorldLayout → Act1ConnectedWorld
                                           ↓ presentation only
                             meshes, terrain envelope, lighting, audio route
```

`RuntimeBridge` не должен уступать narrative state процедурной сцене, UI или capture harness. `Act1WorldLayout` не должен становиться квестовым/сейвовым реестром. `Act1ConnectedWorld` не должен создавать собственные dialogue, interaction или save semantics.

## Карта ownership

| Слой | Текущий владелец | Контракт / граница |
|---|---|---|
| Канон и продукт | `URMAN_Codex_Context.md`, `AGENTS.md`, `docs/urman_knowledge_base/` | При конфликте сначала нормализованный Codex Context, затем решения/open questions; неизвестное маркировать, не додумывать |
| Narrative/progression | `game/scripts/RuntimeBridge.cs` + Core runtime/content | Единственный writer состояния, knowledge, vocabulary, quests, old-PC capability и SaveGameV3; UI только читает/показывает |
| Quest projection | `game/scripts/QuestRuntimeCoordinator.cs` | Читает compiled quest definitions и обновляет projection; не заменяет kernel |
| World placement | `game/scripts/Act1WorldLayout.cs` | Пять logical zones, world origins, spawns и семь connector placement; только deterministic presentation/traversal mapping |
| Connected presentation | `game/scripts/Act1ConnectedWorld.cs` | Один persistent `Act1ConnectedWorld`; создаёт logical scenes и `Act1CoreWorldGreybox`, связывает visual envelope, lighting и presentation suppression |
| Scene bootstrap / zone route | `game/scripts/Main.cs`, `game/scenes/main.tscn`, `game/scripts/Act1DemoRoot.cs` | `Main` выбирает connected-world opt-in и spawn; `main.tscn` держит shared runtime/player/UI; `Act1DemoRoot` — presentation wrapper, не narrative owner |
| Player / input | `game/scripts/FirstPersonController.cs` | Реальное first-person движение/look/FOV/settings и применение presentation spawn; не меняет сюжет |
| Physical interactions | `game/scripts/InteractionTarget.cs` | Authored ID/zone/spawn/dialogue/document, availability event-driven; dispatch в `RuntimeBridge`, без второго state cache |
| Continuous ambience | `game/scripts/AmbientAudioDirector.cs` | Один routed ambience owner с bounded crossfade; текущие stems/manifest wiring ещё не финальный mix/voice package |
| Materials | `game/scripts/PainterlyMaterialLibrary.cs` | Shared candidate painterly surface/material cache; не art approval и не замена authored geometry |
| Act I content source | `content/campaigns/urman.chapter1/campaign.json`, `content/modules/urman-chapter1/{module,definitions}.json`, `game/content/urman.chapter1.compiled.v1.json` | Campaign/module/compiled pack задают beats, gates, texts, documents, vocabulary и invariants; compiled pack не отменяет канонические docs |
| Tests / runtime evidence | `game/tests/*.cs`, `eng/*.sh` | Проверяют узкие contracts и capture envelope; test-only capture не dispatch narrative и не пишет save |
| Asset candidates | пять `game/assets/models/act1/*_manifest.md` | Manifests — provenance/intent/coverage, не бинарный runtime и не accepted final art; GLB/Blend намеренно не включены |

## Logical zones и визуальный envelope

`Act1WorldLayout` задаёт следующие logical placements:

| Logical zone | Placement/origin | Визуальная роль в `Act1CoreWorldGreybox` |
|---|---|---|
| `village_day` | `village-main-road`, `(0,0,0)` | Arrival, MainStreet, BabaiEbiYard, HouseExteriorApproach, ConnectiveStreetReturn, FapExterior |
| `house_old_pc` | `babay-abi-house`, `(-28,0,0)`, interior | House interior / old PC safe-zone |
| `fap_clinic` | `fap-clinic-yard`, `(28,0,-30)`, interior | FAP interior / waiting room |
| `zirat_road` | `zirat-return-road`, `(0,0,-70)` | ZiratMemoryField / return road |
| `kara_urman_night` | `kara-urman-edge`, `(0,0,-115)` | KaraForestEdge / cliffhanger approach |

В capture contract восемь direct visual zones: `Arrival`, `MainStreet`, `BabaiEbiYard`, `HouseExteriorApproach`, `ConnectiveStreetReturn`, `FapExterior`, `ZiratMemoryField`, `KaraForestEdge`. `house_interior` и `fap_interior` — отдельные first-person capture checkpoints внутри logical interiors, но не дополнительные direct zones в receipt count. Это различие нужно сохранять при планировании acceptance.

## Важные риски дублирования и исторические ветви

1. `Act1ConnectedWorld` содержит текущую persistent presentation envelope, procedural composition methods и `Act1CoreWorldGreybox` с восьмью direct visual zones. В нём же есть suppression metadata для legacy/generated pieces. Наличие suppression не означает, что все старые композиционные методы безопасно удалить без проверки.
2. `AgentBExteriorWorld` встроен как runnable exterior traversal layer и в текущей runtime-иерархии помечен production-canonical для этого слоя. `docs/experiments/agent_b_act1_world_report.md` описывает старую standalone ветвь и старые результаты; это **non-canonical historical experiment**, а не второй runtime owner. Pro должен проверить фактическую ownership boundary перед удалением источников.
3. `Main.cs` всё ещё содержит обычный one-zone loader для режима без `EnableAct1ConnectedWorld`, одновременно с connected-world opt-in `Act1DemoRoot`. Это потенциальный divergence/fallback risk: smoke для обычной сцены и Act I demo могут смотреть на разные presentation paths.
4. В `Act1ConnectedWorld` остаются legacy/candidate процедурные группировки, authored kits и suppression для overlapping trees/house/FAP/zirat pieces. Нельзя добавлять ещё один слой, пока не выбран один canonical map и не доказано, какие узлы удаляются или остаются rollback-only.
5. Тест `Act1FullRouteCoreWorldCapture` проверяет presentation-only envelope и root viewport; он сознательно не выполняет interactions/narrative/save. Его receipt нельзя использовать как gameplay completion receipt.
6. Исторические route/navigation docs отражают ранее обсуждавшийся маршрутный/вебовый контекст и помечены в KB как superseded. Они полезны только для provenance; текущий first-person Godot owner важнее.
7. Candidate manifests и target images описывают намерение и сравнение, но не доказывают наличие, качество или runtime collision бинарных ассетов.

## Что Pro должен решить явно

- оставить ли `Act1ConnectedWorld` единственным Act I presentation root и какую часть `AgentBExteriorWorld` считать его единственным traversal owner;
- какие procedural/candidate subtrees удалить, какие оставить как rollback-only, и каким acceptance это подтверждается;
- надо ли унифицировать demo и default loader до одного пути до art lock, не ломая существующие full-game placeholders;
- как чинить capture camera/serialization, не маскируя проблему duplicate views;
- где проходит минимальная граница «красивого MVP»: только canonical connected map + core route, без параллельного Acts II–V world.

До этого решения не создавать новый world root, новый narrative store, второй audio owner или альтернативный capture harness.
