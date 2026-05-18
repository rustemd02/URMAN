# MVP Playtest Plan

Status: audit plan, 2026-05-18.

Purpose: define how to test УРМАН from prototype pieces to a real MVP. Do not run external "full MVP" playtests until the shared journal/dialogue key graph, house scene and cliffhanger path exist. Until then, test route navigation, old PC investigation and narrative comprehension separately.

## Current Testability

Can test now:

- route orientation in `?scene=village`;
- old PC search/document loop in `?scene=computer`;
- phone intro comprehension from the main menu;
- rough route-to-house path;
- asset/readability smoke checks.

Cannot honestly test yet as full MVP:

- complete arrival-to-cliffhanger chapter;
- dialogue key system;
- shared investigation journal;
- татарский re-read loop across systems;
- save/load of investigation state;
- final audio-first cliffhanger.

## Playtest Matrix

| Track | Goal | Testers | Build scope | Main risk | Pass condition |
|---|---|---:|---|---|---|
| Internal smoke | Check that the spine does not break technically | 1 dev/QA | `intro`, `village`, `house`, `computer`, `forest` | Blockers, missing assets, impossible transitions | 0 blockers, validators pass |
| Route orientation | Test discrete route navigation | 6 players | `?scene=village` | 90-degree turns confuse players | 80% find house, FAP/selsmag, mosque, zirat/forest |
| Old PC investigation | Test search/document loop | 6 players | `?scene=computer` or house-to-PC | PC becomes reading without action | 70% find official doc and contradictory register |
| Narrative comprehension | Test Айдар, Марат, false versions | 5 players | intro + route + old PC | Марат feels like lore, not emotional hook | 80% explain the Marat contradiction |
| Language mechanic | Test татарский as gameplay | 5 players + language reviewer | intro + old PC + journal/vocab | language feels decorative | 60% use a татарский word as search/key |
| Cliffhanger | Test genre shift | 5 players | final route and forest scene | reads as generic monster lure | 80% say "old system/rules", not only "monster" |
| Cultural review | Check татарский, islam, folklore | 1-2 consultants | all text and scenes | cultural/religious caricature | 0 blocker/serious issues |
| First-time full slice | Test complete MVP | 5-8 players | menu to cliffhanger | player does not know what to do | 70% finish with <= 2 neutral hints |

## Instrumentation Needed Before Serious Tests

Implement before external or first-time full-slice tests:

- anonymous `session_id`, build hash, timestamp, viewport, browser;
- `scene_enter`, `scene_exit`, duration per scene;
- `route_node_enter`, `route_action`, `route_state_transition`;
- `journal_open`, `external_handoff_open`, `stuck_hint_used`;
- `oldpc_open`, `search_query`, `item_open`, `locked_item_seen`, `clue_saved`, `term_clicked`, `reset_session`;
- `clue_acquired`, `contradiction_seen`, `vocabulary_unlocked`, `dialogue_key_used`, `pressure_level_changed`;
- observer hotkeys or panel: `confused`, `bored`, `stuck`, `aha`, `cultural_question`, `bug`;
- export one JSON per session and optional CSV summary;
- QA reset controls: clear route state, clear old PC state, jump to route node, jump to old PC section.

## Automated Checks

Current commands:

```bash
npm run build
node scripts/validate-old-pc-content.mjs
git status --short
```

Recommended package scripts to add:

```bash
npm run validate:old-pc
npm run validate:route-graph
npm run validate:clue-graph
npm run validate:content
npm run test:smoke
```

Direct QA URLs after `npm run dev`:

```text
http://localhost:5173/
http://localhost:5173/?scene=village
http://localhost:5173/?scene=house
http://localhost:5173/?scene=computer
http://localhost:5173/?scene=forest
http://localhost:5173/?scene=zirat
```

## Script A: Internal Smoke

Duration: 20-30 minutes.

Setup:

1. Start a fresh browser profile or clear `localStorage`.
2. Run `npm run dev`.
3. Keep devtools console visible.

Steps:

1. Open `/`.
2. Click `Начать игру`; enter default or custom name.
3. Confirm chapter card appears and then phone intro starts.
4. Click 2-3 татарские chat messages and confirm translations are readable.
5. Buy ticket and confirm transition to `village`.
6. In route scene, move from arrival toward main street.
7. Find the house route; enter house handoff.
8. In house, click `Сесть за компьютер`.
9. In old PC, open `Архивный поиск`.
10. Search `Марат`.
11. Open `Справка о смерти Марата Н.`.
12. Save clue.
13. Search `реестр`; open `Строка реестра: дело Марата`.
14. Search or click `урман`; open relevant Татарвики / Кара-Урман material.
15. Use the desktop icon `Отойти от компьютера` or route return path.
16. Route toward zirat/forest approach.
17. Trigger pressure / Rinat interruption if the route supports it.
18. Open forest scene.

Record:

- console errors;
- broken images;
- missing audio;
- actions that appear clickable but do nothing;
- route dead ends;
- unreadable text;
- moments where direct URL jumps were required;
- localStorage state that fails to reset.

Fail immediately if:

- black screen;
- unhandled exception;
- route graph fails to load;
- old PC cannot open;
- validator fails;
- cliffhanger handoff is impossible after it is claimed implemented.

## Script B: Route Orientation

Duration: 15-20 minutes.

Start at:

```text
http://localhost:5173/?scene=village
```

Task for tester:

1. Find the house of Мансур.
2. Return to the main route.
3. Find the crossroad.
4. Find FAP/selsmag.
5. Find the mosque sign.
6. Find the route toward zirat.
7. Reach forest approach or Кара-Урман edge.
8. Open journal sketch at least once.

Observer records:

- wrong turns;
- time to first house;
- whether signs or journal were used;
- whether turns feel spatially logical;
- whether route labels feel like diegetic signs or quest markers;
- places where player gets stuck longer than 3 minutes.

Pass:

- 80% of testers find the required locations;
- no tester gets stuck twice for more than 3 minutes;
- journal helps orientation but does not feel like GPS.

## Script C: Old PC Investigation

Duration: 20-30 minutes.

Start at:

```text
http://localhost:5173/?scene=computer
```

Task for tester:

1. Find the official document about Марат.
2. Save it as a clue.
3. Find one document that contradicts it.
4. Unlock or identify one gated/corrupted file.
5. Use one suggested татарский term as a search term.
6. Explain what changed in your understanding of Марат.

Observer records:

- first search query;
- dead searches;
- first useful clue;
- whether locked file requirements are understandable;
- whether suggested terms feel helpful or too hand-holdy;
- documents skipped because too long or too dry;
- whether player understands "official lie" without the UI spelling it out.

Pass:

- 70% find official doc and contradictory register;
- median time to first useful clue is <= 7 minutes;
- at least 60% use a term such as `урман`, `Шүрәле`, `граница`, `не отвечай`.

## Script D: Narrative Comprehension

Duration: 45-60 minutes.

Rules:

- Do not explain canon.
- Ask player to think aloud.
- Observer may only ask: "what are you trying to check?" and "what do you think this means?"
- Use neutral hints only after 3 minutes stuck.

Run:

1. Start from `/`.
2. Play through intro, route, house and old PC.
3. If route to forest is implemented, continue to cliffhanger.
4. Ask post-test questions.

Post-test questions:

- Who is Айдар?
- Why did he come back?
- Who is Марат?
- What are the versions of what happened to Марат?
- Which source feels official but suspicious?
- Which source contradicts it?
- What does `урман` mean in the game after the old PC?
- Why might "do not answer" matter?
- What does Ринат know that Айдар does not?
- What do you think the village is hiding?

Pass:

- 80% explain Айдар's return and Марат's emotional role.
- 75% identify at least two connected clues.
- 80% understand that the village hides a system, not only one crime.

Fail signals:

- "I guess there is a monster in the forest" is the whole takeaway.
- Марат is remembered only as a name in documents.
- Татарский is remembered only as flavor.
- Player thinks Кара-Урман is the village.

## Script E: Language Mechanic

Duration: 20 minutes after old PC or dedicated build.

Task:

1. Identify all татарские words the player noticed.
2. Ask which word helped them act.
3. Ask them to use a word as a search term or dialogue key.
4. Show a re-read target after a word is confirmed.
5. Ask what changed in meaning.

Target words:

- `урман`
- `тавыш`
- `җавап`
- `ярамый`
- `зират`
- `шүрәле`

Pass:

- 60% use one word as a tool.
- 80% say the word changed search, interpretation or dialogue.
- Language reviewer marks no blocker errors.

Fail:

- Player says "language is cool but not needed".
- Words unlock only dictionary entries, not action.
- The player feels mocked for not knowing татарский.

## Script F: Cliffhanger

Duration: 10-15 minutes.

Prerequisite:

- route final path and forest/cliffhanger scene implemented.
- audio fallback implemented if authored voice is missing.

Steps:

1. Start from a state where final route clue is unlocked.
2. Route to forest approach.
3. Trigger Kara-Urman edge.
4. Let the voice call Айдар.
5. Pause long enough that player feels the temptation to answer.
6. Rинат interrupts with «Не отвечай».
7. Hard cut.

Post-test:

- What changed about the genre?
- Why was answering dangerous?
- Did Rинат feel like he knew a practical rule?
- Did you expect a monster reveal?
- Did the cut happen too early, too late or at the right moment?

Pass:

- 80% understand "rules/system" without exposition.
- 80% say Rинат's line is practical, not random.
- No one needs a full creature reveal to understand the turn.

Fail:

- Scene reads as jump scare.
- Scene reads as random ghost voice.
- Rинат reads as exposition delivery.

## Cultural And Language Review

Review before any external demo.

Reviewers:

- татарский language consultant;
- culturally knowledgeable татарский reviewer;
- religious/cultural reviewer for Тимур хәзрәт and mosque scenes if possible.

Review scope:

- intro татарский messages;
- route signs: `мәчет`, `зират`, `ФАП`, `сельмаг`, `Кара-Урман`;
- old PC Татарвики;
- all vocabulary cards;
- all Тимур/ mosque text;
- Шүрәле, Су Анасы, Бичура framing;
- audio lines, especially Марат voice and Ринат's «Не отвечай».

Severity:

- Blocker: must fix before any public build.
- Serious: fix before external demo.
- Minor: fix before polished MVP.
- Style: note for later polish.

Blocker examples:

- wrong татарский meaning that changes gameplay;
- mosque/islam framed as hostile or decorative superstition;
- folklore reduced to enemy mobs;
- generic "tribal horror" aesthetics;
- historical Тукай line stated as crude hard fact.

## Success Metrics

| Area | Success | Failure threshold |
|---|---|---|
| Technical | 0 smoke blockers, validators pass | Any blocker or repeated missing asset |
| Completion | 70% finish full slice with <= 2 hints | <50% finish |
| Detective loop | 75% identify 2 connected clues | Players read docs but infer nothing |
| Old PC | 70% find official + contradictory files | Search feels random |
| Татарский | 60% use a word as tool | Language described as cosmetic |
| Route | 80% find required locations | Journal/signs do not improve orientation |
| Narrative | 80% explain Айдар/Марат | Марат is not emotional hook |
| Cliffhanger | 80% understand old system/rules | Reads as generic monster reveal |
| Culture | 0 blocker/serious review issues | Any unresolved serious issue |

## MVP Release Gate

Before saying "MVP is ready for external playtest":

- `npm run build` passes.
- old PC validator passes.
- clue graph validator exists and passes.
- route graph validator exists and passes.
- smoke Script A passes with 0 blockers.
- Route Script B meets pass threshold.
- Old PC Script C meets pass threshold.
- Narrative Script D has at least one internal pass.
- Cultural review has no unresolved blocker.

