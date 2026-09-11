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
- `AudioCueUi` serializes every voice request, including physical recordings
  when subtitles and audio descriptions are disabled. Physical streams use
  `AudioStream.GetLength()`; logical text uses a readable fallback of at least
  two seconds and approximately 18 characters per second, while a silent
  logical request drains immediately. The pause shell freezes both the player
  and presentation clock; new game, successful load and return to menu clear
  the queue and presentation history.
- Physical voice temporarily ducks the existing `Ambience` bus through
  `AmbientAudioDirector`. The user's persisted ambience volume is re-applied
  when the cue queue drains; no second ambience owner or settings write is
  introduced.
- Priority ladder: voice → spot events → bed. Silence is an authored choice,
  never a bug cover or a fog mask.
- Footsteps (AUDIO-004) and UI foley (AUDIO-010) are presentation-only: they
  never write state.

## Footsteps (AUDIO-004, winter runtime)

`FootstepAudioController` owns the four active Act I surface families:
`snow_packed`, `snow_soft`, `wood` and `interior_floor`, with three
source-derived variants per family. The outdoor choice uses the existing
`AgentBAct1HeightField.RoadInfo`: `Distance < HalfWidth` resolves to packed
snow and the remaining outdoor ground resolves to soft snow. The house maps to
`wood`; the FAP maps to `interior_floor`.

Cadence is based on the player's actual XZ displacement at `0.55 m`, matching
the `SnowTrampleField` stride. Zone-spawn/load revisions, direct teleports,
modal frames and airborne frames reset the accumulator. Reduced motion keeps
footstep audio enabled while continuing to govern camera and other motion
presentation. The legacy wet-road, mud and grass WAVs remain in the repository
as inactive historical assets.

The four families are CC0 source-derived candidates. Their source paths,
archive hashes, per-file hashes and conversion record live in
`game/assets/audio/act1/footsteps/manifest.json`; the source codecs and license
texts are retained under `assets/source/audio/act1/footsteps/`. Human listening,
mix and cultural review remain open.

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
- The cliffhanger hard cut remains responsible for stopping the bed after the
  presentation queue drains. This lifecycle slice provides queue drain and
  transient voice duck; final hard-cut staging remains an authored finale
  concern. The ending is silent by design, not by omission.

## Anti-patterns (reopen triggers)

- Combat/stinger logic, sudden loud scares, cemetery horror decoration.
- Fog or silence used to hide unfinished visuals.
- A second ambience owner or scene-local loops.
- TTS or placeholder voice presented as final (AUDIO-012 gate).
