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

| Segment | Current recorded bed | Spot events / remaining work | Intended read |
|---|---|---|---|
| Arrival (`village_day@arrival`) | 69 s winter wind, lwdickens 261226 | footsteps on packed snow; no synthetic rain or drips | quiet inhabited winter village |
| MainStreet | 119 s winter wind from the same residential recording | physical gate creaks; sparse household activity still needs listening review | people behind fences |
| BabaiEbiYard / porch | quieter 69 s winter wind fragment | own footsteps and gate; no summer poultry or roof rain | sheltered domestic threshold |
| House interior (`house_old_pc`) | 96 s cabin room tone, callmethefoo 744447 | UI keyboard/paper remain procedural; near-PC and domestic foley need mix review | warm small interior without synthetic continuous whine |
| FAP interior (`fap_clinic`) | 121 s indoor room tone, RIFORKA 801025 | paper and local equipment need listening review | quiet institutional space; no horror drone |
| ConnectiveStreetReturn | later 69 s winter wind fragment | fewer foreground events | village recedes towards the outskirts |
| Zirat / approach road | 26.43 s recorded wind, Magnesus 606960 | own snow steps; no grave sound effects | restrained remembrance and distance |
| KaraForestEdge (`kara_urman_night`) | 119 s January pine wind, bruno.auzet 670307 | natural tree movement in source; no added stinger | forest remains a place, not a monster cue |
| Final beat (`scene/forest` onEnter) | existing ambience duck and hard cut | physical Marat/Rinat voices remain missing | familiar call, intervention, silence |

All are prepared CC0 public HQ previews, not locally recorded Kyrlay field
masters. Source pages, hashes, licensed preview URLs and PCM parameters live
in `ambient_manifest.json` and `asset_registry.json`. Only the inactive
future-act water bed stays procedural. House and Kara shared future-zone
bindings remain unchanged; this pass does not claim acceptance of later acts.

Regeneration of the seven newly replaced beds uses the existing
`tools/audio/generate_act1_ambience_layers.py SOURCE_DIRECTORY`. The directory
must contain the original preview URL basenames with matching SHA-256 values.
The script verifies all inputs before writing, reuses the existing PCM header
normalizer, slices at manifest offsets, joins a one-second equal-power loop,
and applies declared RMS levels with a 0.70 peak ceiling. The runtime retains
its existing -12 dB bed level and transient -6 dB voice duck. This replaces
repeating eight-second synthesis with 69–121 second recordings; it does not
prove the audible seam or artistic balance. Listening on speakers/headphones,
final mix and cultural review remain open.

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

### Ending silence — 2026-09-11
The existing AmbientAudioDirector stops both continuous bed players when the
closing card is shown after the authored cue queue drains. This is a hard
presentation stop, with no write to player volume settings. New Game / load
re-enters the normal SetZone path and restarts the appropriate bed. The
existing chapter flow check covers final silence and fresh-session restart.
Physical Marat/Rinat recordings and final winter-bed listening/mix are still
open; this lifecycle fix is not voice or audio-quality acceptance.
