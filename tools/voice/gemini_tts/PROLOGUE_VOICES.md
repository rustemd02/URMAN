# Synthetic prologue voices (preview)

`generate_prologue_voices.py` voices the Act I prologue with Gemini TTS
(`gemini-3.8-flash-tts`): grandfather Mansur's ride barks in all three Tatar
language levels, the Niva language dialogue and three forest lines (65 clips).
Output: `game/assets/audio/act1/voice_preview/<text-id>[.lvl-*].wav` plus
`manifest.json` (voice, model, text, sha256; every entry `synthetic: true`).

- API key: macOS Keychain, service `urman-gemini-api-key` (or `GEMINI_API_KEY`).
  Never store it in the repository; `api_key*.txt` is git-ignored.
- Resume/extend: rerun the script; existing files are skipped (`--force` redoes).
- Runtime: `PrologueVoice` (game/scripts/PrologueVoice.cs) plays a clip by text id
  on the Voice bus; a line without a file stays silent. Hooked into the ride barks,
  `DialogueUi` and the forest teaser. `eng/run-act1-demo.sh` imports new clips.
- These are placeholders. They do not replace original recordings and do not close
  AUDIO-011..013 (see docs/urman_knowledge_base/audio/act1_voice_recording_brief.md).
- Free tier: `gemini-3.8-flash-tts` allows ~10 requests/day; the script then falls
  through to `gemini-3.8-flash-lite-tts`. Per-minute 429s are waited out and retried.
- Style is passed as a `speech_metadata` annotation, never as text: a "Say like a
  young Tatar boy:" prefix gets read aloud by the model.
- `forest_fx.py` (numpy/scipy only) bakes the eerie forest treatment: distant/echoing/
  distorted Marat calls, a muffled Mansur, plus the `knockout_ring` and `dread_drone`
  foley. It re-processes from raw copies in `.tools/voice-preview/raw-forest/`, so the
  effect can be tuned without new API calls. Rerun it after regenerating forest clips.
