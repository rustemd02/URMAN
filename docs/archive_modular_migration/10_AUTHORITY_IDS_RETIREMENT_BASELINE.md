---
task_id: MM-10
title: Authority, ID и retirement baseline
status: complete
kind: audit-and-decision-baseline
depends_on: [MM-03]
blocks: [MM-20]
parallel_group: null
owner_scope:
  - docs/modular_migration/10_AUTHORITY_IDS_RETIREMENT_BASELINE.md
  - docs/urman_knowledge_base/decision_log.md
  - docs/urman_knowledge_base/open_questions.md
  - docs/urman_knowledge_base/weak_points.md
forbidden_scope:
  - src/**
  - content/**
  - public/**
deliverables:
  - Карта источников истины и canonical ID
  - Retirement matrix для всех текущих владельцев
  - Зафиксированная save v1 policy и reveal timing
verification:
  - npm run validate:content
  - npm run build
  - git diff --check
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-10 — Authority, ID и retirement baseline

## Цель

До создания схем определить, какие данные и runtime-пути считаются текущими владельцами, какие ID становятся canonical и какие старые пути должны исчезнуть. Пакет не меняет код.

## Решения, которые нужно перенести без переоткрытия

- Приоритет канона задан `AGENTS.md`; текущая Chapter 1 — заменяемый production campaign, но не вечная структура игры.
- До финала доступен только `clue_voice_answer_is_dangerous_hint` со статусом hypothesis. `clue_do_not_answer_rule` становится confirmed после действия Рината в финале.
- Pre-MVP gameplay saves не мигрируют. При cutover удаляются `urman.mvp.save.v1`, `urman.oldPcHub.state.v1`, `game.notepad.files.v1` и `game.notepad.activeFile.v1`; `urman_username` и отдельно классифицированные user preferences сохраняются.
- Legacy aliases для ID не создаются. Старые ID преобразуются один раз при переносе данных.

## Аудит

Составить в handoff таблицы со столбцами `surface`, `current owner`, `consumers`, `target owner`, `classification`, `retirement packet`, `evidence`.

Обязательные поверхности:

- characters, dialogues, knowledge, vocabulary, quests, scenes, route graph, handoffs, asset manifests;
- `GameState`, `SceneManager`, `RouteNavigationScene`, `HouseScene`, `ForestScene`, `MosqueScene`, `IntroScene`, `ComputerScene`, `ZiratMiniGame`;
- old PC content/parser/UI, DedOS registry/apps и notepad;
- `SaveSystem`, direct `localStorage`, `window.URMAN` и smoke tooling;
- пустые `QuestSystem`, `InventorySystem`, `DialogueUI`;
- `MainMap` greybox и его registrations.

Для каждой поверхности выбрать только одно:

- `migrate -> delete old owner`;
- `quarantine dev-only -> production import forbidden`;
- `delete as dead/unreachable`.

Постоянный adapter или fallback не считается retirement.

## Mandatory retirement matrix

Значение `current owner` ниже описывает фактический owner на момент MM-10, а не желаемую архитектуру. `Consumers` перечисляет production caller paths, которые обязаны исчезнуть или переключиться. Для внутренних code paths действует `delete-first`; старые `public/**` graph/manifest files после отключения runtime-import остаются только rebuildable provenance и не считаются вторым production owner.

| Surface | Current owner | Consumers / caller paths | Target owner | Classification | Retirement packet | Evidence |
|---|---|---|---|---|---|---|
| Characters | `src/data/characters.ts` (`CHARACTERS`) | `src/MainMap/logic/generators/HomesteadGenerator.ts`; old PC refs use a separate `char_*` vocabulary | `urman.chapter1` `CharacterDefinition` records in `CompiledContentPack` | migrate -> delete old owner | MM-50 migrates; MM-53 removes MainMap dependency; MM-54 deletes `src/data/**` | `characters.ts:12-153`; `HomesteadGenerator.ts:3,34` |
| Dialogues | `src/data/dialogue_data.ts` (`DIALOGUE_LINES`) | `src/systems/DialogueSystem.ts`; route/NPC presentation through `Game.dialogue` | `urman.chapter1` `DialogueDefinition` + dialogue/reaction provider | migrate -> delete old owner | MM-50 data; MM-51 provider; MM-54 old array/caller retirement | `dialogue_data.ts:3-64`; `DialogueSystem.ts:1,9,24` |
| Knowledge / evidence | `src/data/knowledge_keys.ts` plus mutation rules in `GameState` | `GameState`, `InvestigationSystem`, `NotebookUI`, old PC bridge | `urman.chapter1` knowledge/evidence definitions + kernel evidence/provenance store | migrate -> delete old owner | MM-50 data; MM-31/MM-40 state owner; MM-51 consumers; MM-54 deletes old data/state owner | `knowledge_keys.ts:3-326`; `GameState.ts:90-131`; `InvestigationSystem.ts:1-3`; `NotebookUI.ts:1-2` |
| Vocabulary | `src/data/vocabulary_data.ts` plus `GameState`/`VocabularySystem` | `GameState`, `InvestigationSystem`, `NotebookUI`, notepad bridge | `urman.chapter1` `VocabularyDefinition` + kernel vocabulary state | migrate -> delete old owner | MM-50 data; MM-51 UI; MM-70 removes direct notepad/storage bridge | `vocabulary_data.ts:3-77`; `GameState.ts:141-149,222-225`; `VocabularySystem.ts:71-89` |
| Quests | `src/data/quests.ts`; `src/systems/QuestSystem.ts` is empty | No active `QUESTS` import; progression is currently scattered through scene/state flags | `urman.chapter1` `QuestDefinition` + `src/runtime/quests/**` | migrate -> delete old owner | MM-40 runtime; MM-50 data; MM-54 deletes empty/stale owners | `quests.ts:3-22`; zero-byte `QuestSystem.ts`; repository import scan finds no `QUESTS` consumer |
| Scene definitions / order | `SceneManager` switch + hardcoded scene classes + `Game.start` allowlist | `Game`, every scene transition, route/OS globals | `SceneRegistry` + production `CampaignManifest` entrypoint/handoffs | migrate -> delete old owner | MM-51 scene providers; MM-54 composition cutover and old switch/allowlist retirement | `SceneManager.ts:26-82`; `Game.ts:50-59` |
| `chapter1` launch token / `ChapterScene` | `MainMenuScene` sends `chapter1`; `SceneManager` maps it to `ChapterScene`, whose splash then sends `intro` | menu click/Enter and `Game.start` allowlist | host launch request for campaign ID `urman.chapter1`, entering that manifest's declared arrival/road scene | migrate -> delete old owner | MM-51 prepares only the generic menu/presentation provider and does not switch a production entrypoint or edit `ChapterScene`; MM-54 atomically switches host campaign launch, removes the `chapter1` allowlist/switch token and callers, and deletes `ChapterScene.ts`; there is no scene-ID migration or runtime alias | `MainMenuScene.ts:154,159`; `Game.ts:52`; `SceneManager.ts:37-39`; `ChapterScene.ts:3-55`; splash title prematurely exposes the pact |
| Route graph | `public/assets/urman_route_map/route_graph.json` | `RouteNavigationScene` fetches `/assets/urman_route_map/route_graph.json` | `urman.chapter1` route/scene definitions in pack | quarantine dev-only -> production import forbidden | MM-50 migrates definitions; MM-51 removes runtime fetch; MM-54 proves production import closure | `RouteNavigationScene.ts:111-115,358`; 32 current graph nodes |
| External handoffs | route graph `externalHandoff` data plus `RouteNavigationScene` modal/global transitions | `RouteNavigationScene` calls `SceneManager` and inline `window.URMAN` | Chapter content effects + generic scene/navigation provider | migrate -> delete old owner | MM-50 data; MM-51 provider/global removal; MM-54 old manager retirement | `RouteNavigationScene.ts:399,560,882`; `SceneManager.ts:33-67` |
| Asset manifests | `public/assets/urman_route_map/asset_manifest.json`, `urman_mvp_first10/asset_manifest.json`, `urman_mvp_remaining/asset_manifest.json` | `RouteNavigationScene` directly fetches route manifest; production assets otherwise use direct paths | portable `AssetDefinition` records + `AssetResolver`; old manifests remain generation provenance only | quarantine dev-only -> production import forbidden | MM-41 resolver; MM-50 logical refs/definitions; MM-51 removes direct fetch/path ownership; MM-55 closure gate | `RouteNavigationScene.ts:111-115,358,624`; three current manifest files |
| `GameState` | `src/game/GameState.ts` mutable public fields/methods | `Game`, scenes, systems and UI import/read/write it directly | `RuntimeKernel` immutable store/selectors | migrate -> delete old owner | MM-31 kernel; MM-51 consumers; MM-54 deletes old owner | `Game.ts:13,26`; `GameState.ts:20-233`; import scan across scenes/systems/UI |
| `SceneManager` | `src/game/SceneManager.ts` | `Game`, route scene, OS and all current scene switches | `SceneRegistry` + one navigation/scene host | migrate -> delete old owner | MM-31 registry; MM-51 providers; MM-54 cutover/delete old switch | `Game.ts:1,14,27`; `SceneManager.ts:14-82`; `OS.ts:119`; `RouteNavigationScene.ts:399,560` |
| `RouteNavigationScene` | `src/scenes/RouteNavigationScene.ts` owns parsing, rendering, navigation, conditions, narrative IDs and handoffs | `SceneManager` maps production `village` to it | generic route renderer/controller/graph/handoff adapters over pack + kernel | migrate -> delete old owner | MM-51 decomposes/retires hardcoded owner; MM-54 switches caller | `SceneManager.ts:12,52-54`; `RouteNavigationScene.ts:111-115,196-199,358,597-603,882` |
| `HouseScene` | `src/scenes/HouseScene.ts` | `SceneManager`; direct `Game` state/audio/navigation | generic static/scripted scene provider + `urman.chapter1` definition | migrate -> delete old owner | MM-51 | `SceneManager.ts:4,49-51`; class import/caller scan |
| `ForestScene` | `src/scenes/ForestScene.ts` | `SceneManager`; direct `Game` state/audio/navigation | generic scripted/audio-first scene provider + content definition | migrate -> delete old owner | MM-51 | `SceneManager.ts:3,46-48`; class import/caller scan |
| `MosqueScene` | `src/scenes/MosqueScene.ts` | `SceneManager`; direct `Game` state/audio/navigation | generic static/scripted scene provider + content definition | migrate -> delete old owner | MM-51 | `SceneManager.ts:10,58-60`; class import/caller scan |
| `IntroScene` | `src/scenes/IntroScene.ts` | `SceneManager`; `Game.start` routes through `intro` | generic chapter presentation provider + campaign entrypoint | migrate -> delete old owner | MM-51; MM-54 removes old registration | `SceneManager.ts:5,40-42`; `Game.ts:52-58` |
| `ComputerScene` | `src/scenes/ComputerScene.ts` constructs `DedOS` and owns CRT/audio shell | `SceneManager` `computer` case | old PC capability adapter | migrate -> delete old owner | MM-52 is sole file owner; MM-54 removes old registration | `ComputerScene.ts:1-87`; `SceneManager.ts:2,43-45` |
| `ZiratMiniGame` | `src/scenes/ZiratMiniGame.ts` | `SceneManager` `zirat` case | none for this legacy code; accepted zirat content is a new `urman.chapter1` typed scene/route definition rendered by a generic provider | migrate -> delete old owner | MM-50/MM-51 independently provide the typed replacement without porting minigame code or behavior; MM-54 atomically deletes the legacy caller, registration and file | `SceneManager.ts:11,61-63`; current registration is reachable, but the minigame is not an authority for accepted Chapter 1 zirat content |
| Old PC authored content | Markdown/frontmatter under `content/old_pc/**` | `oldPcContent.ts` Vite glob/parser; old PC UI | `urman.oldpc` module records under portable schema | migrate -> delete old owner | MM-52 | `oldPcContent.ts:69,153-161`; 10 current authored items |
| Old PC parser/index | `src/os/data/oldPcContent.ts` custom frontmatter/Markdown parser and indexes | `oldPcHub.ts`, `browser.ts`, OS registry types | semantic compiler + `ContentRegistry` | migrate -> delete old owner | MM-52 after parity test | `oldPcContent.ts:69,87-161`; imports in `oldPcHub.ts`, `browser.ts`, `registry.ts` |
| Legacy web narrative corpus | `src/os/data/web_content.json` (unstructured HTML/JSON for Татарвики, poems and forum) | none: repository scan found no import, Vite reference or runtime lookup | no target record: a noncanonical unreachable corpus must not become a portable module implicitly | delete as unreachable internal owner | MM-52 deletes exact file; MM-55 enforces its absence | 2026-07-18 `rg` over `src`, `scripts`, `tests` and docs found no `web_content` reference; no user or persistent state is stored in this file |
| Old PC UI/progression | `src/os/apps/oldPcHub.ts` local state, search/unlock and `window.URMAN` bridge | browser adapter, DedOS registry/OS | old PC runtime module/provider using `RuntimeContext` and registry records | migrate -> delete old owner | MM-52 runtime; MM-70 removes legacy storage/global carrier | `oldPcHub.ts:1-25,68-81,165,179,389-417` |
| DedOS registry/apps | `src/os/core/registry.ts`, `src/os/core/OS.ts`, `src/os/apps/**` | `ComputerScene`; OS switch launches apps and direct scene transition | descriptor-driven old PC app registry and providers | migrate -> delete old owner | MM-52 | `ComputerScene.ts:2,71`; `registry.ts:15-54`; `OS.ts:185-203` |
| DedOS notepad | `src/os/apps/notepad.ts` | OS app switch; `VocabularySystem` writes same storage namespace | old PC capability snapshot/session state; shared journal remains kernel projection | migrate -> delete old owner | MM-52 UI migration; MM-70 storage retirement | `notepad.ts:1-61`; `VocabularySystem.ts:71-89` |
| `SaveSystem` / save v1 | `src/systems/SaveSystem.ts` (`urman.mvp.save.v1`) | `Game` startup, `NotebookUI`, smoke; also mirrors old PC UI state | `GameSnapshotV2` codec + one storage gateway | migrate -> delete old owner | MM-42 new codec; MM-54 production cutover; MM-70 deletes v1 owner/keys | `Game.ts:10,21,31-33`; `SaveSystem.ts:4-100`; `NotebookUI.ts:72` |
| Direct `localStorage` | `SaveSystem`, `VocabularySystem`, old PC hub, notepad, `MainMenuScene`; smoke clears all storage | gameplay save, old PC, notes/vocabulary, username | storage gateway; namespaced capability snapshot; preserved username/preferences classification | migrate -> delete old owner | MM-70; `urman_username` value preserved through gateway/reset | repository scan: `SaveSystem.ts:38-89`, `VocabularySystem.ts:73-89`, `oldPcHub.ts:70-81,415`, `notepad.ts:31-61`, `MainMenuScene.ts:7,150`, smoke:51 |
| `window.URMAN` | `Game` publishes global; route/OS/old PC/chat/notebook/smoke consume it | runtime transitions, knowledge/save bridge and test inspection | constructor/registry injection + `RuntimeContext`; test-only public harness in MM-60 | migrate -> delete old owner | MM-51/MM-52 internal callers; MM-60 smoke harness; MM-70 global removal | `Game.ts:44`; repository global scan lists `OS.ts`, `oldPcHub.ts`, `ChatProvider.ts`, `RouteNavigationScene.ts`, `NotebookUI.ts`, smoke |
| Smoke tooling | `scripts/playtest-mvp-smoke.mjs` imports host-specific Playwright/Chrome paths and production global | manual MVP smoke | `node:test` E2E + owned zero-dependency Chrome CDP driver + test-only harness | migrate -> delete old owner | MM-60 | smoke:1-23,127-155; MM-60 contract |
| Empty `QuestSystem` | zero-byte `src/systems/QuestSystem.ts` | no imports | `src/runtime/quests/**` | delete as dead/unreachable | MM-54 | zero-byte file and no caller scan |
| Empty `InventorySystem` | zero-byte `src/systems/InventorySystem.ts` | no imports | kernel item/custody store | delete as dead/unreachable | MM-54 | zero-byte file and no caller scan |
| Empty `DialogueUI` | zero-byte `src/ui/DialogueUI.ts` | no imports | MM-51 dialogue/reaction UI provider | delete as dead/unreachable | MM-54 | zero-byte file and no caller scan |
| MainMap greybox | `src/MainMap/**` procedural/isometric runtime and authored layout | `SceneManager` imports/registers `villageGreybox`; generator imports `CHARACTERS` | `urman.legacy.mainmap` dev-only runtime module | quarantine dev-only -> production import forbidden | MM-53 isolation; MM-54 removes production registration; MM-55 checks import graph | `SceneManager.ts:8,55-56,69`; `MainMap/config.ts:40-88`; `HomesteadGenerator.ts:3,34` |

Matrix closure: every mandatory surface has one target owner and a retirement packet. No permanent compatibility adapter is authorized. The owner-approved reset of exactly four pre-MVP browser keys is authorized deferred execution for MM-70; no broader storage deletion is allowed, and MM-10 performs no deletion.

## Authority table

| Concern | Authority during migration | Final production authority | Derived / non-authoritative surfaces | Enforcement |
|---|---|---|---|---|
| Canon and product behavior | `AGENTS.md`, normalized canon/KB and accepted Chapter 1 lock | Same authority docs; campaign content must conform | compiled pack, runtime state, UI copy caches | MM-50 invariant tests; MM-55 closure; MM-80 canon review |
| Portable schemas | frozen MM-01 contract, then `content/schemas/**` | JSON Schema from MM-20 | generated TypeScript types | MM-20 schema validation; MM-30 type drift check |
| Authored campaign composition | current arrays/route JSON only until migrated | production `CampaignManifest` + explicit module manifests/files | `CompiledContentPack` and Vite virtual catalog | MM-30 compiler; MM-54 cutover; MM-55 closure |
| Runtime content lookup | current arrays, Vite glob and scene switch | `ContentRegistry`, `SceneRegistry`, condition/effect/capability registries built from one pack | renderer projections and indexes | MM-31 architecture tests; MM-54 cutover |
| Gameplay state mutation | current mutable `GameState` | `RuntimeKernel` transaction/store | UI view state and immutable selectors | MM-31 transaction tests; MM-54 removes old owner |
| Quest/mechanic state | current scene flags and scattered UI state | quest instances + capability instances owned through kernel | capability presentation state | MM-40 lifecycle/cleanup tests |
| Persistence | current `SaveSystem` plus three local UI storage owners | `GameSnapshotV2` codec + one storage gateway | encoded JSON blob and test snapshots | MM-42 roundtrip/load tests; MM-70 storage gate |
| Assets/text/audio | public manifests and direct `/assets/` paths | logical `AssetRef`/`TextRef` records + resolvers | public binary files and generation provenance manifests | MM-41 resolver tests; MM-55 import/closure scan |
| Old PC content | current Markdown plus `oldPcContent.ts` parser/index | `urman.oldpc` authored module compiled by common compiler | search index and UI projections | MM-52 parity/preflight tests |
| Legacy MainMap | `src/MainMap/**` and `villageGreybox` registration | no production authority; dev-only `urman.legacy.mainmap` descriptor | generated greybox world | MM-53 isolation; MM-54/MM-55 production import ban |

`CompiledContentPack`, generated types, search indexes, journal projections and browser URLs are derived. Они не могут исправлять или перекрывать authored source и не становятся fallback-authority.

## Canonical ID map

Зафиксировать однозначное сопоставление текущих ID с `moduleId:kind/localId`. Минимальные namespaces:

- `urman.core` — общие доменные сущности, не сюжет Chapter 1;
- `urman.chapter1` — текущая кампания;
- `urman.oldpc` — документы и UI-конфигурация старого ПК;
- `urman.legacy.mainmap` — dev-only greybox до удаления.

Отдельно устранить `babay` против `char_babay` и перечислить отсутствующие character records, включая Рината. Нельзя менять смысл или имена персонажей без записи в `decision_log.md`.

### Правила one-time migration

- Final ID всегда имеет вид `moduleId:kind/localId`; runtime legacy aliases отсутствуют.
- One-time converter принимает только перечисленные ниже legacy forms во время MM-50/MM-52, пишет final ID и больше не сохраняет legacy token.
- Если ID уже содержит `char_`, `clue_`, `quest_`, `doc_` и подобный старый type-prefix, prefix остаётся частью `localId` только там, где таблица прямо не задаёт semantic rename. Это предотвращает массовые догадки.
- Cross-module reference получает final ID владельца entity; old PC не копирует character/knowledge records, а объявляет dependency на `urman.chapter1`.
- `urman.core` не содержит названных персонажей, Марата, route Chapter 1 или lore-specific assets. Это только действительно переиспользуемые определения/records.

### Character ID map

| Current form(s) | One-time normalized token | Final ID | Disposition |
|---|---|---|---|
| `babay`, `char_babay` | `char_babay` | `urman.chapter1:character/mansur` | Мансур бабай; одна identity |
| `abi`, `char_abi`, `char_gulsina` | `char_gulsina` | `urman.chapter1:character/gulsina` | Гөлсинә әби; одна identity |
| `alsu`, `char_alsu` | `char_alsu` | `urman.chapter1:character/alsu` | migrate |
| `fanis`, `char_fanis` | `char_fanis` | `urman.chapter1:character/fanis` | ambient/support record; campaign inclusion explicit |
| `zarifa` | `char_zarifa` | `urman.chapter1:character/zarifa` | ambient; campaign inclusion explicit |
| `ildar` | `char_ildar` | `urman.chapter1:character/ildar` | ambient; campaign inclusion explicit |
| `gulnara` | `char_gulnara` | `urman.chapter1:character/gulnara` | ambient; campaign inclusion explicit |
| `karat_guard` | `char_karat_guard` | `urman.chapter1:character/karat_guard` | ambient; do not infer a new canon name |
| `rashid` | `char_rashid` | `urman.chapter1:character/rashid` | ambient; campaign inclusion explicit |
| `nail` | `char_nail` | `urman.chapter1:character/nail` | ambient; not Наиля |
| `rushania` | `char_rushania` | `urman.chapter1:character/rushania` | legacy FAP record; not silently merged with Наиля |
| `razilya` | `char_razilya` | `urman.chapter1:character/razilya` | migrate if referenced |
| missing `char_aidar` | `char_aidar` | `urman.chapter1:character/aidar` | MM-50 must create from accepted canon |
| missing `char_marat` | `char_marat` | `urman.chapter1:character/marat` | MM-50 must create; childhood friend identity only |
| missing `char_rinat` | `char_rinat` | `urman.chapter1:character/rinat` | MM-50 must create before dialogue migration |
| missing `char_naila` | `char_naila` | `urman.chapter1:character/naila` | MM-50 must create; do not alias `rushania` |
| missing `char_timur_hazrat` | `char_timur_hazrat` | `urman.chapter1:character/timur_hazrat` | MM-50 must create with accepted moral/cultural role |
| `shurale`, `su_anasy`, `ubyr` character rows | none | none in current character registry | do not migrate as ordinary Chapter 1 NPCs; Shurale survives through vocabulary/knowledge; other future entities need their own accepted module/decision |

Required record closure for current Chapter 1 is therefore: Айдар, Мансур, Гөлсинә, Алсу, Марат, Ринат, Наиля and Тимур хәзрәт. Ambient records are opt-in; their presence in the legacy array does not add them to the campaign.

### Family and entity mapping rules

| Legacy family / exact example | Final rule / example |
|---|---|
| Dialogue `rinat_no_key` | `urman.chapter1:dialogue/rinat_no_key` |
| Quest `quest_marat_first_contradiction` | `urman.chapter1:quest/quest_marat_first_contradiction` |
| Knowledge/clue/route/pressure key `clue_marat_official_death_version` | `urman.chapter1:knowledge/clue_marat_official_death_version` |
| Vocabulary `tt_urman` | `urman.chapter1:vocabulary/tt_urman` |
| Location `loc_kara_urman_edge` | `urman.chapter1:location/loc_kara_urman_edge` |
| Route node `arrival_vehicle_dusk` | `urman.chapter1:route-node/arrival_vehicle_dusk` |
| Scene IDs `intro`, `house`, `forest`, `mosque`, `zirat`, `village` | `urman.chapter1:scene/<legacy-id>`; `mainMenu` remains host shell, not campaign content |
| Current `chapter1` scene token and `ChapterScene` | no final scene ID; delete both. The host selects campaign `urman.chapter1` and navigates directly to its declared arrival/road entry scene; no `chapter1` runtime alias and no migration of the premature «Пакт с Шурале» splash |
| Scene `computer` | `urman.oldpc:scene/computer` |
| `villageGreybox` | `urman.legacy.mainmap:scene/village_greybox` and dev-only registration |
| Old PC item `doc_marat_official_death_notice` | `urman.oldpc:document/doc_marat_official_death_notice` |
| DedOS app `archive_search` | `urman.oldpc:app/archive_search` |
| Chapter asset `route_kara_urman_edge_voice_moment` | `urman.chapter1:asset/route_kara_urman_edge_voice_moment` |
| Old PC asset `ui_old_pc_desktop_base` | `urman.oldpc:asset/ui_old_pc_desktop_base` |
| Item `notepad` | `urman.chapter1:item/notepad` |
| Beat/flag references | `urman.chapter1:beat/<legacy-id>` or typed state field; raw untyped flag is not carried when a typed definition exists |
| Stale knowledge ref `dialogue_rinat_internal_register` | one-time rewrite to the existing dialogue `urman.chapter1:dialogue/rinat_internal_register`; source definition is `dialogue_data.ts:22`; no alias record |
| Stale knowledge ref `dialogue_gulsina_home_words` | one-time rewrite to the existing dialogue `urman.chapter1:dialogue/gulsina_yaramyy`; source definition is `dialogue_data.ts:53`; no alias record |

Asset ownership is decided by the consuming module, not physical directory. A binary may remain under `public/assets/**`, but its logical `AssetDefinition` has exactly one module ID.

### External route-target ID closure

`externalLocationNodes` entries are route-exit targets outside the JSON route-node set. MM-50 rewrites each current token directly to its final `SceneRef`; no parallel `handoff` entity or implicit runtime alias is created. MM-51 resolves that scene through the registry. Day/evening or interaction detail travels as typed transition input and does not create a second scene owner:

| Current `externalLocationNodes` token | Final scene ID | Target disposition |
|---|---|---|
| `fap_pressure_document_desk` | `urman.chapter1:scene/fap_pressure_document_desk` | FAP investigation scene; accepted document-desk state |
| `fap_waiting_room_day` | `urman.chapter1:scene/fap_waiting_room_day` | FAP waiting-room scene |
| `mansur_house_exterior_day` | `urman.chapter1:scene/house` | house scene with `day` route-transition context |
| `mansur_house_exterior_evening` | `urman.chapter1:scene/house` | same house owner with `evening` route-transition context |
| `mosque_exterior_day` | `urman.chapter1:scene/mosque` | mosque scene with `day` route-transition context |
| `mosque_exterior_evening_safe` | `urman.chapter1:scene/mosque` | same mosque owner with `evening_safe` route-transition context |
| `mvp_end_cliffhanger` | `urman.chapter1:scene/forest` | final interruption/hard-cut scene; the effect remains content-authored |
| `river_bank_day` | `urman.chapter1:scene/river_bank_day` | river-bank scene |

The current `EXTERNAL_HANDOFFS` table also contains three referenced inline targets not declared by `externalLocationNodes`. They have explicit one-time dispositions and no runtime aliases:

| Current inline target | Exact final ID | Disposition and packet owner |
|---|---|---|
| `selsmag_counter_interior` | `urman.chapter1:scene/selsmag_counter_interior` | migrate as an optional Chapter 1 static/interaction scene; MM-50 owns the typed definition and MM-51 owns the generic provider |
| `zirat_general_late_evening` | `urman.chapter1:scene/zirat` | migrate as the accepted typed Chapter 1 zirat scene with `late_evening` route-transition context; MM-50 owns scene/route data and MM-51 owns the generic provider; no dependency on `ZiratMiniGame` |
| `multiple` | `urman.chapter1:scene/crossroad_signs_inspect` | semantic rename to the Chapter 1 crossroad-signs inspect scene; MM-50 owns the definition and rewrites references once, MM-51 owns the generic provider; no `multiple` alias |

### Asset-family ownership closure

The three legacy manifests are provenance inventories, never module boundaries. MM-50 emits each logical asset once according to its consuming definition, using these ordered rules:

1. An asset referenced by an `urman.oldpc` document, app, desktop or old-PC-only renderer is `urman.oldpc:asset/<legacy-id>`. This includes `ui_old_pc_*`, `ui_archive_search_*`, `ui_tatarwiki_*`, `ui_document_viewer_template` when used by the old PC, and document backgrounds owned by old-PC `DocumentDefinition` records.
2. An asset referenced by the accepted Chapter 1 campaign — route/location screens, transitions, journal, dialogue, vocabulary, phone/messenger, character art and in-world evidence — is `urman.chapter1:asset/<legacy-id>`.
3. An asset used only by the dev MainMap is `urman.legacy.mainmap:asset/<legacy-id>` and remains dev-only until MM-53 retirement.
4. `urman.core` ownership requires an explicit content-neutral shared definition consumed by at least two production modules. A generic-looking filename is insufficient. If rules 1–3 do not determine one owner, compilation fails with an ownership decision request; it must not duplicate the definition or infer `urman.core`.

Applied to the current physical packs:

| Provenance manifest | Logical ownership rule |
|---|---|
| `urman_route_map/asset_manifest.json` | All current route backgrounds, route transitions and journal-route support assets are Chapter 1 assets. Copied `first10`/`remaining` provenance does not create a second definition. |
| `urman_mvp_first10/asset_manifest.json` | `char_aidar_*`, `route_main_street_entry_day` and `route_kara_urman_forest_edge_pressure` -> `urman.chapter1`; `ui_old_pc_desktop_base` and old-PC-owned `doc_marat_medical_record_bg` -> `urman.oldpc`. |
| `urman_mvp_remaining/asset_manifest.json` | Apply the ordered consumer rules per record: campaign character/route/location/journal/dialogue/vocabulary/phone/evidence families -> `urman.chapter1`; old-PC desktop/archive/Tatarwiki/document-renderer families and their owned document backgrounds -> `urman.oldpc`; MainMap-only assets -> `urman.legacy.mainmap`. Directory membership and `source_pack` metadata never override that owner. |

When two modules display the same binary, the authoring-domain owner above keeps the sole `AssetDefinition`; the other module declares a dependency and references that final ID. Physical PNG reuse is allowed, duplicate logical ownership is not.

## Production narrative invariants

These are compile/preflight test inputs, not prose suggestions:

1. The production entrypoint starts at arrival/road near Кырлай; no playable KFU/Kazan intro.
2. Кырлай is the village; Кара-Урман is the forbidden forest tract/boundary, never the village name.
3. Chapter 1 remains centered on Марат and the detective chain; logging/corruption and broader pact history do not replace it.
4. No complete creature reveal, combat mob, bestiary framing or full pact exposition appears in Chapter 1.
5. Айдар is not a chosen mage; named characters retain accepted identities and roles.
6. Тимур хәзрәт remains a moral/cultural support, not an exposition device or anti-folklore caricature; Алсу remains human and is not the mystical solution.
7. Before the final scene only `clue_voice_answer_is_dangerous_hint` may exist as hypothesis. `clue_do_not_answer_rule` is absent/unconfirmed until Ринат acts and says «Не отвечай» at the cliffhanger.
8. The final route requires authored sources, including `doc_kara_urman_edge_sketch`, plus `rinat_alerted`; journal inference alone cannot unlock it.
9. Ринат's movement/vehicle/light causality beat occurs before the edge scene; he does not teleport into the finale.
10. The finale has no real answer/do-not-answer branch: Айдар is interrupted before answering, followed by hard cut without explanation.
11. Татарские vocabulary records keep unknown/guessed/confirmed state, re-read/application targets and consultant-review metadata; language changes investigation and does not become a fictional cipher.
12. Stress fixtures remain synthetic `Proposal` and are excluded from production campaign/assets/backlog.

Current-source drift captured for retirement rather than normalized as canon: `clue_voice_answer_is_dangerous_hint` has no current shared record; `dialogue_data.ts` lines 29 and 59 unlock `clue_do_not_answer_rule` before the finale; `knowledge_keys.ts` lines 149-160 and 229-242 also encode the rule too early; `quests.ts` line 19 requires it before the cliffhanger. MM-50 must create the hypothesis record and encode the invariant above, not copy these paths.

## Save v1 reset policy

Owner-approved deferred action for MM-70:

- delete exactly once: `urman.mvp.save.v1`, `urman.oldPcHub.state.v1`, `game.notepad.files.v1`, `game.notepad.activeFile.v1`;
- preserve `urman_username`;
- preserve only user preferences explicitly classified by the MM-70 audit; MM-10 found no additional current preference keys;
- do not call `localStorage.clear()` and do not delete unknown, lab, browser or unrelated origin keys;
- write a versioned reset marker only after all four exact removals succeed, and show the one-time reset notice only for this migration;
- new production and Content Lab storage namespaces remain separate;
- there is no save-v1 reader, migration adapter, guessed default or silent best-effort fallback after cutover.

`scripts/playtest-mvp-smoke.mjs` currently calls `localStorage.clear()` for test setup. That behavior is test-only and must not be reused as production reset logic.

## Handoff

Handoff обязан содержать:

1. baseline-команды и результаты;
2. canonical ID map;
3. retirement matrix со всеми caller paths;
4. список production narrative invariants;
5. подтверждение save reset policy;
6. все обнаруженные канонические противоречия либо запись «новых противоречий нет».

`MM-20` не стартует, пока у каждой обязательной поверхности нет target owner и retirement packet.

## Completed handoff

Status: complete
Date: 2026-07-17
Owner: MM-10 authority/retirement baseline implementer

### Baseline evidence

- `npm run validate:content`: pass, exit 0 — 10 old PC files, 20 shared keys, 32 route nodes, 41 assets and 11 handoffs validated.
- `npm run build`: pass, exit 0 — TypeScript and Vite production build completed, 72 modules transformed.
- `git diff --check`: pass, exit 0.
- Current owner/caller evidence: mandatory retirement matrix above, based on bounded source/import/storage/global scans.

### Deliverables

- Canonical ID map: complete above; no runtime aliases. One-time family normalization explicitly includes `babay → char_babay → urman.chapter1:character/mansur`.
- Missing character records: Айдар, Марат, Ринат, Наиля, Тимур хәзрәт; MM-50 owns creation from accepted authority. `rushania` is not an alias for Наиля.
- Retirement matrix: every mandatory surface has current owner, consumers, target, one classification, retirement packet and evidence.
- Production narrative invariants: 12 listed above, including exact reveal timing and route causality.
- Save policy: exact four-key reset authorized for deferred MM-70 execution; username/preferences preserved; no broad clear.
- Canon contradictions: no new canon decision was made. Existing runtime reveal-timing drift and incomplete/mixed character IDs are implementation drift to replace, recorded here and in KB risk/decision notes.

### Anti-entropy receipt

- Deletion class: internal code retirement; contract-carrying parser/persistence retirement; exact owner-approved pre-MVP browser-state reset.
- New canonical owners: authored modules/compiler/registries/kernel/`GameSnapshotV2`/resolvers as tabled above.
- Preserved behavior: Chapter 1 critical path, old PC investigation, route navigation, journal/dialogue/vocabulary/pressure chain, username/preferences.
- Retired behavior: raw parsers, mutable global state, hardcoded scene/ID switches, duplicate storage/progression owners, production MainMap import and host-specific smoke bypass.
- Compatibility: none retained; pre-MVP gameplay save v1 intentionally resets.
- Lingering-reference checks belong to the named retirement packet and final MM-55/MM-70 gates.

### Release decision

`MM-20` is released by MM-10: all three required verification commands pass. No source/content/public file was changed by MM-10.
