# MVP Completion Handoff

Status: product execution handoff. Architecture baseline updated 2026-07-18.

Purpose: this is the working list of what is still missing before УРМАН can be called a full MVP vertical slice. It is written for a future LLM or developer who may have zero context. Follow the current campaign and playtest gate; do not revive legacy runtime owners described in the historical audit below.

## Architecture baseline — 2026-07-18

The modular migration is complete and independently reviewed. Chapter 1 runs as compiled portable content through `RuntimeKernel`; scenes/dialogues/quests/assets are campaign data, old PC is a capability module, and production persistence uses `GameSnapshotV2` through one named gateway. This removes the former product blocker of shared state ownership, but does not prove MVP quality, final audio/assets or player comprehension.

The sections that name `GameState`, `SceneManager`, `RouteNavigationScene`, `SaveSystem`, `src/data/**`, direct old-PC imports or legacy validators are a historical 2026-05-18 audit only. Do not execute their file lists. For new work use `docs/modular_migration/80_FINAL_VERIFICATION_KB_HANDOFF.md`, `chapter1_mvp_campaign.md`, `playtest_plan.md` and the current compiled content modules.

## Current product execution — 2026-07-18

The architecture foundation is ready for MVP construction, but the game is not yet an MVP release. The active work is to author, compile and playtest the Chapter 1 campaign through the portable content contracts — not to restore any legacy runtime owner. Start with the current campaign data and acceptance gates in `chapter1_mvp_campaign.md`, `playtest_plan.md`, `backlog.md` and `docs/modular_migration/80_FINAL_VERIFICATION_KB_HANDOFF.md`.

## Historical audit archive — 2026-05-18 (reference only; do not execute)

The remainder of this file is preserved evidence from the pre-migration audit. Its file lists, commands, system names, completeness assessments and suggested order predate the modular architecture baseline. They must not be used as implementation instructions; current ownership is defined by the modular-migration package.

### Executive Verdict (historical)

УРМАН already has a strong canon, a clear MVP identity, a validated old PC content prototype, a generated route-navigation asset pack and a working route runtime. The project is not blocked by lack of ideas.

The real blocker is **cross-system integration**:

```text
old PC clue -> shared knowledge key -> journal card -> dialogue reaction -> vocabulary re-read -> pressure change -> route/cliffhanger
```

Right now the old PC can prove a local document loop, and the route scene can prove a movement loop. The MVP promise requires those loops to become one detective loop. Until a clue from the old PC can change Rinat's dialogue, appear in the investigation journal, unlock/reframe a татарский word, raise village pressure and survive reload, the game is still a collection of strong prototypes.

### Evidence Snapshot (historical)

Verified locally during this audit:

```bash
node scripts/validate-old-pc-content.mjs
# old pc content ok: 10 files

npm run build
# tsc && vite build passed
```

Important current facts:

- `src/scenes/RouteNavigationScene.ts` is the strongest runtime slice: it consumes `public/assets/urman_route_map/route_graph.json`, `asset_manifest.json`, `animation_layers.json` and `editable_layers.json`.
- `content/old_pc/` has 10 valid Markdown/frontmatter files and supports a real search/unlock loop inside the old PC.
- `src/systems/DialogueSystem.ts`, `InventorySystem.ts`, `QuestSystem.ts`, `SaveSystem.ts`, `src/ui/DialogueUI.ts` and `src/types/game.types.ts` are empty.
- `src/ui/NotebookUI.ts` is a vocabulary notebook, not an investigation journal.
- `src/scenes/HouseScene.ts` and `src/scenes/ForestScene.ts` are stubs.
- `src/scenes/MosqueScene.ts` is a 3D Al-Aqsa placeholder and conflicts with the accepted ink-wash static-node direction for a татарская деревня.
- `public/assets/urman_mvp_remaining/` contains broad visual coverage, but most of it is not runtime-integrated.
- `public/assets/audio/cattle.mp3` and `public/assets/audio/forest_howl.mp3` are not valid authored MVP audio assets; do not rely on them as final.

### Non-Negotiable MVP Acceptance (historical product target)

The MVP is done only when a first-time player can complete this arc:

1. Айдар arrives in or near Кырлай after a light Kazan/phone context.
2. The player understands that Айдар came under a family pretext, but Марат is the unresolved emotional hook.
3. The house of Мансур and Гөлсинә gives warmth plus unease.
4. The village reacts to Айдар with silence, looks and social containment.
5. The player finds at least two conflicting versions of Марат's fate.
6. The old PC opens or reframes at least one critical clue about Марат.
7. A татарский word becomes useful as a search term, dialogue key or re-read trigger.
8. The player applies a knowledge key to at least one NPC and receives a changed reaction.
9. The journal records facts, contradictions, vocabulary and a minimal Марат timeline.
10. Village pressure changes at least once because Айдар knows something dangerous.
11. The final route to Кара-Урман is caused by gathered clues, not by a random quest marker.
12. At the edge of Кара-Урман, Айдар hears Марат's voice; before he answers, Ринат says: «Не отвечай».
13. The cut happens before exposition and before a full creature reveal.

If any of these fail, call the build a prototype, not an MVP.

### Current Playable Spine (historical)

This is what currently exists:

```text
Main menu
  -> Chapter card
  -> Phone intro
  -> Village route navigation
  -> House stub
  -> Old PC prototype
  -> Route graph can approach forest states
  -> Forest placeholder
```

The strongest pieces are route navigation and old PC. The weakest pieces are shared evidence state, dialogue, journal, scene content, final cliffhanger, audio and save/load.

### End-to-End MVP Beat Sheet (historical)

This is the canonical target path. Build toward this sequence before adding optional branches.

| # | Beat | Player action | Current status | Missing P0 work | Acceptance |
|---:|---|---|---|---|---|
| 0 | Menu and light Kazan context | Start game, read phone chat, buy ticket | Playable in `MainMenuScene`, `ChapterScene`, `IntroScene` | Fix labels that imply Кара-Урман is the village; keep KFU as background only | Player knows Айдар is going to help family in/near Кырлай |
| 1 | Arrival at Кырлай | Enter route navigation from arrival vehicle | Route node exists | Confirm arrival copy says Кырлай; no full map; no GPS feel | Player understands forward/turn/inspect |
| 2 | First route to house | Find Мансур's house from main route | Route handoff exists | Add clear diegetic signs and return state; no dead-end | Player finds house within 3 minutes |
| 3 | House warmth with unease | Meet Мансур and Гөлсинә, receive PC access motive | `HouseScene` is stub | Replace with image-backed house/kitchen/PC room scene, family dialogue and first objective | PC access feels permitted, not theft |
| 4 | First mention of Марат | Family or local NPC cuts off topic | Not implemented as dialogue | Add `dlg_babay_marat`, `dlg_gulsina_home_words`, first flags | Player asks why adults avoid Марат |
| 5 | Village pressure / Алсу | Route or street encounter introduces social silence and Алсу | Mostly not implemented | Add Алсу first-walk beat or modal scene; mark her as guide with withheld truth | Player sees Алсу as human guide, not mystical girl trope |
| 6 | First hard clue | Cemetery/FAP/old PC reveals official version | Old PC doc exists; cemetery/FAP not integrated | Add route-to-zirat or FAP scene; create shared clue when found | Player can name one official version of death |
| 7 | Contradiction | Internal register, grave date or med phrasing conflicts with official version | Old PC register exists locally | Bridge old PC reveals to shared knowledge keys and journal | Player can explain what contradicts what |
| 8 | Татарский unlock | `урман`, `тавыш`, `җавап`, `ярамый` or `шүрәле` gains gameplay use | Vocabulary is local/prototype | Add `vocabulary_data`, states, re-read target and search/dialogue use | Player uses one татарский word as tool |
| 9 | NPC key reaction | Apply clue to Ринат or Наиля | Dialogue system empty | Implement minimal `DialogueSystem`/`DialogueUI` and Rinat topic first | NPC lies differently or reveals fear |
| 10 | Journal consolidation | Journal shows clue graph/timeline/vocab | Notebook only shows vocabulary | Replace/extend NotebookUI as investigation journal | Player can review facts and contradictions |
| 11 | Safe-zone framing | Тимур gives moral/religious frame | Mosque is 3D placeholder | Replace with ink-wash mosque/tea office scene and respectful short dialogue | Player understands not to romanticize or mock the hidden order |
| 12 | Final route cause | Clues unlock route toward Кара-Урман | Route assets exist | Connect `route_kara_urman_edge_hint`/clues to route state; no random unlock | Player understands why Айдар goes there |
| 13 | Cliffhanger | Voice of Марат, Ринат interrupts, hard cut | Forest placeholder; scripted route exit not executed | Implement scripted route/final scene, audio cues, end screen | Player says "old system exists", not "monster showed up" |

### P0 Implementation Sequence (historical; superseded)

Do these in order. Later steps depend on earlier shared state.

#### P0.1 Canon And Runtime Drift Cleanup

Goal: stop the runtime from teaching players the wrong canon.

Files:

- `src/data/characters.ts`
- `src/data/chat_data.ts`
- `src/scenes/MainMenuScene.ts`
- `src/scenes/IntroScene.ts`
- `src/ui/HUD.ts`
- `src/game/GameState.ts`
- `docs/urman_knowledge_base/open_questions.md`

Tasks:

- Replace active runtime usage where Кара-Урман means the village with Кырлай. Keep Кара-Урман only as the forbidden forest tract / old place-name.
- Remove or quarantine `Алсу 1926` as active MVP truth. If used later, mark it as in-world false lead or post-MVP puzzle, not current canon.
- Rewrite runtime Шүрәле descriptions away from "one monster that tickles people to death" and "generic evil".
- Remove creature/monster emojis from production data where they push generic horror tone.
- Change `GameState.flags.debug_bypass` default from always-on to QA-only or remove it from MVP progression checks.
- If a detail is intentionally kept as a false in-world claim, add `canonStatus` / note in the relevant data file or `open_questions.md`.

Acceptance:

- A player cannot infer that Кара-Урман is the village name.
- No MVP runtime text frames Шүрәле as a simple monster enemy.
- Debug bypass is not required for normal progression.

Verification:

```bash
rg -n "Кара-Урман|Kara|Алсу|Шүрәле|debug_bypass" src docs/urman_knowledge_base content
npm run build
```

#### P0.2 Shared Knowledge Source Of Truth

Goal: create the common data layer that all systems use.

Create or modify:

- `src/types/game.types.ts`
- `src/data/knowledge_keys.ts`
- `src/data/vocabulary_data.ts`
- `src/data/dialogue_data.ts`
- `src/data/quests.ts`
- `src/game/GameState.ts`
- `src/systems/InvestigationSystem.ts` or `src/systems/InventorySystem.ts`

Minimum types:

```ts
export type KnowledgeKeyType =
  | 'name'
  | 'date'
  | 'place'
  | 'document'
  | 'word'
  | 'photo'
  | 'contradiction'
  | 'testimony'
  | 'route';

export interface KnowledgeKey {
  id: string;
  type: KnowledgeKeyType;
  title: string;
  summary: string;
  sourceIds: string[];
  tags: string[];
  dangerLevel: 0 | 1 | 2 | 3;
  relatedCharacters: string[];
  relatedLocations: string[];
  reveals: string[];
  contradicts: string[];
  unlocks: string[];
  usableInDialogue: boolean;
  mvp: boolean;
}

export type VocabularyState = 'unknown' | 'guessed' | 'confirmed';

export interface VocabularyEntry {
  id: string;
  tatar: string;
  russian: string;
  context: string;
  stateDefault: VocabularyState;
  searchTerms: string[];
  reReadTargets: string[];
  dialogueKeyId?: string;
  needsConsultantReview: boolean;
}
```

Minimum MVP knowledge keys:

- `clue_marat_official_death_version`
- `clue_marat_case_boundary_marker`
- `clue_marat_was_afraid_before_death`
- `clue_folklore_as_survival_rule`
- `clue_kara_urman_edge_is_rule_boundary`
- `clue_village_has_internal_compensation_system`
- `clue_mansur_allowed_pc_access_deliberately`
- `clue_do_not_answer_rule`
- `contradiction_marat_official_vs_internal`
- `tt_urman`
- `tt_tavysh`
- `tt_javap`
- `tt_yaramyy`
- `route_kara_urman_edge_hint`

Extend `GameState` with:

- `knownKeys: string[]`
- `savedEvidenceIds: string[]`
- `vocabularyStates: Record<string, VocabularyState>`
- `npcStates: Record<string, { reactionLevel?: number; flags: string[] }>`
- `pressureLevel: 0 | 1 | 2 | 3`
- `pressureFlags: string[]`
- `completedBeats: string[]`

Acceptance:

- A key ID can be used by old PC, journal, dialogue, quest and save/load without duplicate local meaning.
- Old PC frontmatter `reveals`, `contradicts`, `unlocks`, `vocabulary` can be cross-checked against these data files.

Verification:

```bash
npm run build
```

#### P0.3 Bridge Old PC To Shared State

Goal: old PC discoveries must affect the game, not only the old PC window.

Files:

- `src/os/apps/oldPcHub.ts`
- `src/os/data/oldPcContent.ts`
- `src/game/Game.ts`
- `src/game/GameState.ts`
- `src/systems/InvestigationSystem.ts` or equivalent

Tasks:

- Keep local old PC UI state for window layout/query/active section.
- When the player opens an unlocked item, add its `reveals`, `unlocks`, `vocabulary` and item id to shared `GameState`.
- When the player clicks "Сохранить улику", add a shared evidence record, not only `saved_${id}` in old PC state.
- Emit events: `knowledge_updated`, `clue_acquired`, `vocabulary_unlocked`, `pressure_level_changed` if a dangerous key applies.
- Preserve existing `scripts/validate-old-pc-content.mjs`; do not rewrite the authoring model unless blocked.

Acceptance:

- Open `doc_marat_official_death_notice` -> journal sees `clue_marat_official_death_version`.
- Open `rec_marat_case_register_conflict` -> journal sees contradiction and Rinat dialogue can react.
- Open `tw_shurale_urman_boundary` -> vocabulary system knows `tt_urman` and `term_do_not_answer`.

Verification:

```bash
node scripts/validate-old-pc-content.mjs
npm run build
```

Manual:

1. Open `?scene=computer`.
2. Search `Марат`.
3. Open official doc, save clue.
4. Open journal.
5. Confirm the clue appears outside old PC.

#### P0.4 Investigation Journal

Goal: replace the current vocabulary-only notebook with the player's external detective brain.

Files:

- `src/ui/NotebookUI.ts`
- `src/ui/HUD.ts` if needed for open button
- `src/data/knowledge_keys.ts`
- `src/data/vocabulary_data.ts`
- `src/game/GameState.ts`

Required journal tabs:

- `Улики`: saved clues with source, danger and tags.
- `Противоречия`: pairs such as official version vs internal register.
- `Марат`: minimal timeline from childhood memory to official death to internal boundary marker.
- `Словарь`: татарские words with state `unknown/guessed/confirmed`.
- `Маршрут`: keep route sketch support, but do not make it full overworld.

Minimum cards:

- Official death version.
- Internal register boundary marker.
- Saved Marat message.
- `урман` word card.
- Do-not-answer rule.
- Kara-Urman edge hint.

Acceptance:

- Player can answer "what do I know and what can I test next?" from the journal.
- Journal does not expose hidden future truth before the player earns it.
- It supports татарские glyphs: `ә`, `ө`, `ү`, `җ`, `ң`, `һ`.

#### P0.5 Dialogue Key Prototype

Goal: prove "knowledge as key" through one real NPC before expanding.

Files:

- `src/systems/DialogueSystem.ts`
- `src/ui/DialogueUI.ts`
- `src/data/dialogue_data.ts`
- `src/data/knowledge_keys.ts`
- `src/game/GameState.ts`
- `src/scenes/RouteNavigationScene.ts` or a new NPC scene/handoff

Start with Ринат. Add these reactions:

| Level | Requires | Rinat response role | Effects |
|---:|---|---|---|
| 0 | none | deflects: "уехал / не твоё дело" | none |
| 1 | `clue_marat_official_death_version` | shifts to official death wording | unlock `topic_marat_case_formal` |
| 2 | `clue_marat_case_boundary_marker` or contradiction | fear/containment: "ты не понимаешь" | `rinat_alerted`, pressure +1 |
| 3 | `clue_do_not_answer_rule` or Kara-Urman edge sketch | practical warning | unlock route/finale hint |

After Ринат, add minimal Наиля and Мансур:

- Наиля confirms that medical wording is too convenient but refuses full truth.
- Мансур reacts to PC discoveries with pain, not villainy.

Acceptance:

- Player can apply a clue and see a visibly different NPC response.
- Dangerous key raises pressure or changes availability.
- NPCs do not dump exposition.

#### P0.6 First Vocabulary/Re-read Loop

Goal: татарский becomes gameplay, not flavor.

Files:

- `src/data/vocabulary_data.ts`
- `src/systems/VocabularySystem.ts`
- `src/os/apps/oldPcHub.ts`
- `src/ui/NotebookUI.ts`
- `content/old_pc/tatarwiki/tw_shurale_urman_boundary.md`
- `content/old_pc/documents/doc_kara_urman_edge_sketch.md`

Minimum words:

| ID | Word | Initial meaning | MVP gameplay use |
|---|---|---|---|
| `tt_urman` | урман | лес | search term, re-read internal records as boundary/system |
| `tt_tavysh` | тавыш | voice/sound | links Marat message to forest voice |
| `tt_javap` | җавап | answer/response | supports "do not answer" rule |
| `tt_yaramyy` | ярамый | нельзя | village prohibition as rule, not superstition |
| `tt_zirat` | зират | cemetery | route/cemetery clue |
| `tt_shurale` | шүрәле | folklore name | reframes wiki as survival instruction |

Every MVP word must have at least one of:

- old PC search use;
- dialogue key use;
- re-read target;
- route/sign interpretation;
- journal contradiction link.

Acceptance:

- Tester does not say "language is pretty but unnecessary".
- At least one old document changes meaning after a word is confirmed.

#### P0.7 Scene Integration: House, Zirat/FAP, Mosque, Forest

Goal: replace stubs and tech demos with MVP scenes that use existing assets.

#### House

Files:

- `src/scenes/HouseScene.ts`
- existing assets in `public/assets/urman_mvp_remaining/`

Tasks:

- Use `loc_mansur_house_exterior_*`, `loc_kitchen_first_dinner_*`, `loc_mansur_pc_room_*` images where available.
- Add Mансур and Гөлсинә short dialogue beats.
- Motivate PC access: Мансур asks for help with old computer or papers.
- Add exit back to village.

Acceptance: house feels like safe zone with unease, not a menu button.

#### Zirat / Cemetery

Files:

- `src/scenes/ZiratMiniGame.ts` or a new static scene
- route graph handoff
- document/journal data

Tasks:

- Connect route to cemetery scene.
- Produce `clue_marat_grave_date` or mark if cemetery clue is postponed.
- Avoid making the grave scene a gimmick mini-game unless it serves the clue.

Acceptance: cemetery yields a clear contradiction or timeline fact.

#### FAP / Наиля

Files:

- new `FapScene.ts` or route external modal plus dialogue
- `src/data/dialogue_data.ts`
- `src/data/knowledge_keys.ts`

Tasks:

- Use existing FAP location images.
- Add medical phrasing clue.
- Add Наиля response to official document and contradiction.

Acceptance: medical layer supports the Marat mystery, not a separate clinic branch.

#### Mosque / Тимур

Files:

- `src/scenes/MosqueScene.ts`
- existing mosque assets

Tasks:

- Remove Al-Aqsa GLB from MVP flow.
- Use ink-wash mosque exterior/tea office art.
- Write a short respectful Тимур scene: he does not mock folklore, does not endorse blind pact obedience, and gives Айдар moral restraint.

Acceptance: no cultural mismatch from placeholder 3D model; mosque reads as safe zone.

#### Forest / Cliffhanger

Files:

- `src/scenes/ForestScene.ts`
- `src/scenes/RouteNavigationScene.ts`
- `public/assets/urman_route_map/route_graph.json`
- audio assets

Tasks:

- Implement the final scene with `route_kara_urman_edge_rinat_interruption.png` or equivalent.
- Add scripted route action support for the final handoff if needed.
- Add audio beats: village ambience thins, silence, Marat voice, pause, Rinat step/line.
- Hard cut before explanation.

Acceptance: final player takeaway is "there is an old system with rules", not "a monster is in the woods".

#### P0.8 Route Graph Finalization

Goal: ensure every route exit used by the MVP is deliberate and playable.

Files:

- `src/scenes/RouteNavigationScene.ts`
- `public/assets/urman_route_map/route_graph.json`
- route manifest files

Tasks:

- Support `exits.scripted` or remove it and convert to a supported action/state transition.
- Normalize external handoffs: house, FAP, mosque, river, zirat, forest should either open a real scene or be clearly unavailable.
- Add validation for graph targets, background assets, external handoff targets and route actions.
- Keep journal sketch incomplete; do not turn it into a full map.

Acceptance:

- No route action silently dead-ends.
- Player can reach house, FAP/selsmag, mosque, zirat/forest and final handoff.

#### P0.9 Save/Load

Goal: a player can leave and resume without losing the detective graph.

Files:

- `src/systems/SaveSystem.ts`
- `src/game/GameState.ts`
- `src/os/apps/oldPcHub.ts`

Save:

- current scene/route node;
- discovered route nodes and journal route updates;
- known knowledge keys;
- saved evidence ids;
- vocabulary states;
- NPC states;
- village pressure;
- old PC unlocked/read/saved state;
- completed beats/quests.

Acceptance:

- Save after finding official doc and internal register.
- Reload.
- Journal, Rinat dialogue availability, vocabulary and route state still match.

#### P0.10 Validators And QA Tools

Goal: prevent narrative graph rot.

Create:

- `scripts/validate-clue-graph.mjs`
- `scripts/validate-route-graph.mjs`
- package scripts:
  - `validate:old-pc`
  - `validate:clue-graph`
  - `validate:route-graph`
  - `validate:content`

Validator checks:

- every old PC `reveals`, `contradicts`, `unlocks`, `vocabulary` id exists in shared data or allowed generated key list;
- no MVP clue is orphaned with no source and no use;
- every dialogue `requires` key exists;
- every MVP vocabulary word has gameplay use;
- every route graph background asset exists in manifest and file system;
- every external handoff has a registered scene or explicit unavailable marker;
- no forbidden active MVP canon labels: Кара-Урман as village, Айрат as protagonist, Миннигуль as current әби without alias note.

Acceptance:

```bash
npm run build
npm run validate:content
```

Both pass before any MVP handoff.

### P1 Work After P0 Loop Works (historical)

Do these after one full detective loop works.

- Rewrite old PC prototype text into production draft with stronger human voice and less blunt exposition.
- Add Алсу first-walk scene as a real support beat, not just lore.
- Add Наиля/FAP scene if the cemetery and old PC are not enough for the contradiction loop.
- Add Разиля/sельмаг if the village needs more social pressure and gossip.
- Add a minimal "first day evening lock" where pressure changes route availability.
- Add authored audio for all MVP ambience states.
- Add playtest instrumentation and observer hotkeys.
- Add first-time player playtest build.

### P2 / Post-MVP Restraints (historical)

Do not spend MVP time here unless P0/P1 are already green.

- Full Bаранов 1967 quest.
- Тукай 1913 line beyond a careful teaser.
- Full romance with Алсу.
- Full мессенджер «Ялкын» as a major system.
- Full creature reveal or creature animation.
- Combat, stealth, survival mechanics.
- Turing-complete old PC / free hacking.
- Full top-down map.
- Complex village simulation.

### Content Files To Create (historical)

The current code uses TypeScript modules plus Markdown. Use that style first; do not start by inventing a separate engine pipeline.

Recommended content/data files:

```text
src/data/knowledge_keys.ts
src/data/vocabulary_data.ts
src/data/dialogue_data.ts
src/data/quests.ts
src/data/locations.ts
src/data/mvp_beats.ts
content/scenes/mvp_day1/arrival.md
content/scenes/mvp_day1/house_first_evening.md
content/scenes/mvp_day1/first_marat_mention.md
content/scenes/mvp_day1/alsu_first_walk.md
content/scenes/mvp_day1/rinat_first_confrontation.md
content/scenes/mvp_day1/timur_safe_zone.md
content/scenes/mvp_day1/kara_urman_cliffhanger.md
```

If creating new Markdown scene files, include frontmatter:

```yaml
---
id: scene_house_first_evening
mvpBeat: 3
locationId: loc_mansur_house
characters: [char_aidar, char_babay, char_gulsina]
requires: []
unlocks: [topic_marat, old_pc_access]
clues: []
vocabulary: [tt_abi, tt_babay]
pressureDelta: 0
status: draft
---
```

### MVP Clue Graph Minimum (historical)

This is the minimum graph another LLM should implement before expanding.

```text
doc_marat_official_death_notice
  reveals -> clue_marat_official_death_version
  unlocks -> query_marat_registry

rec_marat_case_register_conflict
  requires -> clue_marat_official_death_version
  reveals -> clue_marat_case_boundary_marker
  contradicts -> clue_marat_official_death_version
  vocabulary -> tt_urman

msg_marat_saved_last_normal
  requires -> query_marat_registry
  reveals -> clue_marat_was_afraid_before_death
  unlocks -> tw_shurale_urman_boundary

tw_shurale_urman_boundary
  reveals -> clue_folklore_as_survival_rule
  unlocks -> term_do_not_answer
  vocabulary -> tt_urman, tt_tavysh, tt_javap

doc_kara_urman_edge_sketch
  requires -> clue_folklore_as_survival_rule
  reveals -> clue_kara_urman_edge_is_rule_boundary
  unlocks -> route_kara_urman_edge_hint

msg_mansur_unsent_note
  requires -> clue_marat_was_afraid_before_death
  reveals -> clue_mansur_allowed_pc_access_deliberately
  unlocks -> doc_kara_urman_edge_sketch

rec_internal_accounting_damaged
  requires -> clue_marat_case_boundary_marker, tt_urman
  reveals -> clue_village_has_internal_compensation_system
  unlocks -> pressure_council_attention_1

Rinat dialogue
  requires -> clue_marat_official_death_version, clue_marat_case_boundary_marker
  reveals -> clue_rinat_knows_practical_rule
  unlocks -> route_kara_urman_edge_hint
```

Add more clues only after every edge above has one source and one use.

### Audio Requirements (historical)

Audio is not optional for the accepted MVP tone.

Required authored audio IDs:

- `home_evening_warm`
- `street_silence_pressure`
- `old_pc_hum`
- `mosque_calm_room`
- `zirat_wind_low`
- `water_stillness`
- `forest_presence_low`
- `marat_voice_near`
- `rinat_ne_otvechai`
- `route_footsteps_dirt`
- `paper_journal`

Rules:

- Do not use generic wolf howl as Кара-Урман identity.
- Voice of Марат must feel personal and restrained.
- Rinat's line must be short and practical, not trailer-acting.
- Audio must support silence and pauses; constant horror bed is wrong for УРМАН.

### Runtime Asset Integration Requirements (historical)

The visual asset pack is ahead of runtime. Integrate existing assets before generating more.

P0 replacements:

- `HouseScene.ts`: use house/kitchen/PC-room images, not plain text only.
- `MosqueScene.ts`: remove Al-Aqsa GLB from MVP path.
- `ForestScene.ts`: use Kara-Urman/Rinat route art, not placeholder text.
- `ComputerScene.ts`/OS CSS: keep readable old PC, but reduce mismatch with accepted ink-wash/CRT assets where feasible.
- Document viewer: render readable text over document bases; do not bake final text into PNG.

Acceptance:

- The first 10 minutes of play are visually coherent with `design_style.md`.
- Runtime no longer looks like a mix of Vite prototype, 3D tech demo and generated route art.

### Definition Of Done For MVP (historical)

MVP is complete when all are true:

- `npm run build` passes.
- `node scripts/validate-old-pc-content.mjs` passes.
- Shared clue graph validator passes.
- Route graph validator passes.
- A fresh browser can play from menu to cliffhanger without direct URL jumps.
- At least one old PC clue appears in the journal.
- At least one old PC clue changes NPC dialogue.
- At least one татарский word unlock changes search, re-read or dialogue.
- At least one dangerous key changes village pressure or route/ambience state.
- Save/load preserves route, clues, vocabulary, NPC state and old PC progress.
- Internal smoke playtest has zero blockers.
- First-time comprehension test reaches the pass thresholds in `playtest_plan.md`.
- Татарский/cultural/religious review has no unresolved blocker or serious issue.

### Suggested Work Order For Next LLM (historical)

1. Read `AGENTS.md`, this file, `playtest_plan.md`, `mvp_scope.md`, `old_pc.md`, `route_navigation_graph.md`, `technical_architecture.md`.
2. Run `node scripts/validate-old-pc-content.mjs` and `npm run build`.
3. Fix P0.1 runtime canon drift.
4. Implement shared data/state P0.2.
5. Bridge old PC to shared state P0.3.
6. Build investigation journal P0.4.
7. Implement Rinat dialogue prototype P0.5.
8. Implement first vocabulary/re-read loop P0.6.
9. Replace house/forest/ mosque stubs P0.7.
10. Close route scripted/final handoff P0.8.
11. Add save/load P0.9.
12. Add validators P0.10.
13. Run internal smoke playtest from `playtest_plan.md`.
14. Only then polish content, audio and optional branches.
