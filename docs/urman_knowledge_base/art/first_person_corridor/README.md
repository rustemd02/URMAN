# Act 1 first-person corridor receipt

Date: 2026-08-15  
Status: **PASS (technical corridor evidence) / OPEN (human-facing demo acceptance)**

This receipt records the bounded playable-demo slice, not a full-game claim.
The default entrypoint remains `game/scenes/act1_demo.tscn`; Acts 2–5, the
6–8-hour playthrough, web retirement and art lock remain outside this goal.

## Route covered

The test instantiates the actual demo entrypoint and uses the production
first-person camera ray plus mapped `E` input. It does not dispatch commands
directly and does not call `Main.SwitchZone` to advance the route.

1. Arrival → physical house door → house.
2. Physical old PC → modal archive UI → close.
3. House exit → day street → physical route to the ФАП.
4. Waiting room → document desk → official death notice document UI → physical
   `В журнал` save into the shared journal.
5. Physical return target → house → Rinat dialogue UI and `alerted` state.
6. Saved message, Татарвики boundary source, reread and Kara-Urman edge sketch
   all open through the physical evidence target and `DocumentUi`.
7. Zirat road → Kara-Urman edge → authored `cliffhanger-hard-cut` and the
   confirmed `clue_do_not_answer_rule`.

Each Act 1 compact-zone transition now applies a destination-facing spawn yaw:
house/FAP/zirat/forest arrivals face the next route landmark along `-Z`, while
the optional forest-to-village return faces back along `+Z`. This is a
presentation-only camera placement; it does not add a GPS marker, interaction,
quest state or save field. The corridor smoke asserts the declared yaw after
every Act 1 transition so a doorway-facing look cannot silently point the
player back toward the zone they just left.

The evidence targets now carry explicit document owners. The corridor smoke
also clicks the real `DocumentUi` save button for the official notice and
asserts the resulting shared journal entry; this is stronger than the separate
logical `JournalFlowSmokeTest` alone.

| Physical target | Document opened |
|---|---|
| FAP desk | `urman.oldpc:document/doc_marat_official_death_notice` |
| Saved message | `urman.oldpc:document/msg_marat_saved_last_normal` |
| Boundary source / reread | `urman.oldpc:document/tw_shurale_urman_boundary` |
| Edge sketch | `urman.oldpc:document/doc_kara_urman_edge_sketch` |

The house includes a presentation-only empty-chair/coat/radio cue beside the
Rinat target. It makes the off-screen practical warning findable without
inventing an unregistered Rinat character mesh; the authored Rinat model,
voice and final staging remain production gates.

## Verification

- `./eng/verify-godot.sh` — PASS outside the managed sandbox; build has 0
  warnings/errors and the new corridor test is included in the suite.
- Targeted `res://tests/act1_first_person_corridor_smoke_test.tscn` — PASS on
  Godot 4.7.1 .NET / Metal Forward+.
- Targeted `res://tests/act1_first_person_walkthrough_smoke_test.tscn` — PASS;
  93.05 m of production `MoveAndSlide` movement reaches Kara-Urman and the
  spawn-facing contract remains intact.
- Existing scene, narrative, old-PC, journal, persistence, collision,
  Chapter 1 and full-game smoke tests — PASS in the same run.
- The expected warning from `narrative_transition_smoke_test` remains an
  intentional unknown-ID rejection and is not a corridor failure.

## Remaining gates

This evidence proves the production interaction spine and shared-state
handoffs, not player acceptance. Still open: observed first-time pacing and
wayfinding, keyboard/mouse + gamepad coverage with human players, authored
voice/mix/captions, cultural and language review, M1/Windows performance,
near/mid/far traversal readability, final Rinat presentation and Painterly
Low-Poly art lock. No Acts 2–5 runtime or save contract was changed.

## Desktop receipt boundary

The fresh debug packages are structurally verified for this demo, but this is
not a release claim: macOS host smoke passes on the current Mac while Windows
host execution and M1/Windows performance remain open.

- macOS ZIP: `3bf0d162fee3cac3c4ca2d2c5084571b080e8c6b68fa8058d8cdbb9c42c5d038`
  (161,714,445 bytes)
- Windows ZIP: `11a7c0002114594cba5e0a49c36e6b22e7b44fbc1130c5d814b979cdc01d3a8c`
  (96,886,035 bytes)
- Windows EXE: `4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`
  (127,508,816 bytes)
