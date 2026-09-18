# Synthetic radio preview delivery

This is a separate, reversible delivery of the ten already authored spoken
segments in `game/content/vehicles/avyl_radio.v1.json`. It uses the existing
`VehicleRadioPlayer`, segment IDs, native streams, timeline and saved offsets.
It does not accept original actor recordings or synthetic music. The two finale
voices remain subject to their original-recording brief and installer.

The current ten WAV files live in `.tools/radio-preview/avyl-preview-01/`.
Their manifest SHA-256 is
`ecad706f6240158020fe22dad11519cc44a459a5956fbd79d611e921ee280ef7`.
They are PCM16 mono 48000 Hz, total 139.467 seconds. Actual byte/header/sample,
duration, peak, RMS and edge-silence validation has run. Listening, native Tatar
pronunciation, mixing and final production acceptance have **not** run.

## Model and provenance

The reviewed model is Silero `v5_cis_base_nostress.jit`, with reference source
commit `d9355348e2781dc8fa25a135d1602c530afae24c`. The pinned README explicitly
places **CIS Base** under MIT; this exception must not be generalized to the
other model families. Preserve the actual `LICENSE_CIS`, README, notebook,
model index, weights, source URLs and digests together.

- Weights: 91,695,221 bytes;
  SHA-256 `d7d361caf78b8480bcd65a0c367af665a2bf6f06c8507306e3781dc7c6ce781b`.
- Primary model URL:
  <https://models.silero.ai/models/tts/ru/v5_cis_base_nostress.jit>.
- Licence:
  <https://github.com/snakers4/silero-models/blob/d9355348e2781dc8fa25a135d1602c530afae24c/LICENSE_CIS>.
- Voices: `tat_albina` ID 50 and `ru_albina` ID 21, as listed in the pinned
  reference notebook. These are synthetic model identities, not a claim that an
  actor recorded URMAN's script.

The generator is `.tools/radio-preview/prepare_avyl_preview.py`; its exact hash
is in the delivery manifest. It rejects a mismatch between the current authored
transcripts and the synthesis input. Russian stress annotations and the `FM`
pronunciation expansion are retained separately from the exact written script.
The complete output manifest records voice IDs, model/software version, seed,
each pronunciation input, fragment length, pauses, trimming, normalization and
each WAV hash. No arbitrary source code from the notebook is executed: only
literal symbol and speaker tables are read with `ast.literal_eval`.

## Validation and installation

Dry-run is the default and writes no game files:

```sh
python3 eng/apply-act1-radio-previews.py \
  .tools/radio-preview/avyl-preview-01 --project .
```

During the integration owner's exclusive asset window:

```sh
python3 eng/apply-act1-radio-previews.py \
  .tools/radio-preview/avyl-preview-01 --project . --install-preview
```

The installer verifies the actual model/evidence hashes, exact authored texts,
speaker IDs and pronunciation text, real PCM samples, duration and declared
processing. It refuses to replace any `rightsStatus: approved` recording with a
preview. It also validates the bytes and provenance of already supplied recorded
music. It reuses the existing install lock, atomic writes and rollback mechanism;
the original human-recording validator is not changed or called with a false
origin. It never starts Godot/import/build or edits the registry.

Installed spoken rows retain their stable IDs and carry these distinct fields:

```json
{
  "rightsStatus": "licensed-synthetic-preview",
  "origin": "synthetic-preview",
  "acceptance": "preview-pending-listening-and-language-review",
  "modelLicense": "MIT"
}
```

All four fields plus nonempty provenance are necessary for preview playback.
The player counts recorded segments, synthetic previews and absent audio
separately. The preview is never counted as an approved recording. In-game
station naming and ordinary controls remain the existing Avyl FM experience.

Runtime WAVs use `game/assets/audio/act1/radio/previews/<segment-id>.wav`.
Credits are stored next to them. Complete originals, model weights, licence,
reference sources, generator, manifest and generated WAVs are retained under
`assets/source/audio/act1/radio/synthetic-preview-<manifest-sha>/`, outside the
Godot runtime/PCK. The persistent source archive is created only by installation;
the `.tools` preparation alone does not constitute persistent source delivery.

After installation the integration owner registers the actual current source
and derived hashes, imports and builds serially, and runs the native radio checks
for decoding, seek, transitions, pause, ducking and save/load. The native report
must keep synthetic preview and recorded delivery counts distinct. Audition and
native language review remain separate gates. A later human recording uses the
existing recording installer and replaces the corresponding station row while
leaving immutable preview provenance available.

The original `music-two` ID is retained as **Из архива · повтор**, an explicitly
declared repeat of `music-one`. Two unique compositions were not an author
minimum; the second placeholder must not introduce a silent period or a false
delivery quota. The repeat uses the identical stream path, bytes, duration,
language and provenance. Its separate `licensed-recording-repeat` status,
`recorded-music-repeat` origin and `repeatsSegmentId` preserve the distinction
between one supplied recording and two programme appearances. The player rejects
a repeat whose source is absent, cyclic, unapproved, or differs in those fields.
The programme has one recorded musical item, one repeat and ten synthetic speech
previews: 543.605667 seconds of actual audio, with no pending silent slot.

Any future independent song still needs recording, performance, composition,
arrangement and lyrics provenance. The archival vocal and other found recordings
with unresolved underlying rights remain candidates, without production credit
or acceptance. This explicit repeat does not widen `music-one`'s licence claim.
