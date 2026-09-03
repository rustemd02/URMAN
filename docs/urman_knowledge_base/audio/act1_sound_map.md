# Act I Sound Map (AUDIO-002)

Status: **authored sound-design map; technical routing baseline 2026-09-04; final
mix, human listening and cultural review remain open (AUDIO-014, CULTURE-004)**

This map fixes the intended sonic read of every mandatory Act I route segment:
beds, spot events, silence windows, transitions and voice priorities. It is the
acceptance reference for AUDIO-003…AUDIO-010 authored layers and for the final
mix review. No combat music logic, no horror stingers, no cemetery
sensationalism — tension is built from ordinary sound going quiet or wrong.

## Ownership rules

- One continuous ambience owner: `AmbientAudioDirector` + `ambient_manifest.json`
  (0.65 s crossfade between zone beds). No scene-local loops, no second director.
- Voice plays through `AudioCueUi` with synchronous captions; logical cue IDs
  today: `urman.chapter1:asset/audio-marat-voice`,
  `urman.chapter1:asset/audio-rinat-interruption` (final recordings:
  AUDIO-011…AUDIO-013).
- Priority ladder: voice → spot events → bed. Silence is an authored choice,
  never a bug cover or a fog mask.
- Footsteps (AUDIO-004) and UI foley (AUDIO-010) are presentation-only: they
  never write state.

## Zone/segment map

| Segment | Bed (current → target) | Spot events | Intended read |
|---|---|---|---|
| Arrival (`village_day@arrival`) | `village_day_ambience` (rain/wind bed) | distant rooster/dog, pole hum, first car rattle fading | ordinary wet village evening; life continues without the player |
| MainStreet | same bed, denser pole hum + household layer | door knock, TV murmur behind walls, bicycle | inhabited street, people behind fences |
| BabaiEbiYard | same bed, yard layer (hen, firewood) | clock through window, kettle | home warmth against wet street |
| HouseExteriorApproach | bed thins at the porch step | rain on porch roof | threshold moment before the door |
| House interior (`house_old_pc`) | `house_room_tone` | clock, fridge hum, CRT whir near the old PC (AUTHOR-UP: AUDIO-005) | dry warm interior; rain only as muffled bleed |
| FAP interior (`fap_clinic`) | `house_room_tone` (AUTHOR-UP: dedicated bed, AUDIO-006) | corridor tap, paper, distant phone | institutional cool; locally plausible, no horror clinic |
| ConnectiveStreetReturn | village bed returns, slightly emptier | fewer household events, more wind | village going quiet towards the outskirts |
| ZiratMemoryField | village bed thins to wind (AUTHOR-UP: dedicated layers, AUDIO-007) | single wind gusts, fabric, distant tractor | restrained pause; respect, no ornament |
| Zirat → Kara road | wind bed deepens, village events gone | branches, own footsteps become the loudest voice | escalating separation from the village |
| KaraForestEdge (`kara_urman_night`) | `kara_urman_edge_ambience` + layered trees (AUDIO-008) | creaks, wrong-distance rustle, one distant bird | the old order's territory: present, not aggressive |
| Final beat (`scene/forest` onEnter) | bed ducks deliberately | Marat's familiar phrase (wrong pause), Rinat's interruption | authored silence window for «Не отвечай»; hard cut ends all audio |

## Transitions and silence windows

- Zone switches crossfade beds (0.65 s); interior↔exterior adds a soft
  occlusion dip — no pops, no double beds (AUDIO-009 owns the routing rules).
- Authored silence windows: the zirat pause (before the roadside clue) and the
  Kara threshold (before Rinat's line). Both are dramatic choices with visual
  support, never dead audio.
- The cliffhanger hard cut stops the bed with the presentation queue drained
  by `AudioCueUi` — the ending is silent by design, not by omission.

## Anti-patterns (reopen triggers)

- Combat/stinger logic, sudden loud scares, cemetery horror decoration.
- Fog or silence used to hide unfinished visuals.
- A second ambience owner or scene-local loops.
- TTS or placeholder voice presented as final (AUDIO-012 gate).
