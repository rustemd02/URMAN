# Runtime State Diagnostics (STATE-006)

Status: **current diagnostics surface documented 2026-09-04; release-safe —
no personal data, no state mutation, no debug controls**

This document describes the diagnostics the game already emits, where they
go, and how to read them in a support session. Everything here is read-only
observation of the shared runtime state owned by `RuntimeBridge`; nothing in
this surface can mutate narrative, quest or save state.

## What is logged and where

- Godot log (per-run file via `--log-file`, or stdout in console runs):
  - `zone-loaded: <zone>@<spawn> connected-world=<id>` — every zone switch
    (both connected-world and fallback paths), printed by
    `game/scripts/Main.cs`.
  - `scene.entered: <interactionId>` / `[N] scene.entered: ...` — narrative
    scene transitions, printed by the bridge's event presentation.
  - `runtime.audio.requested: <assetId>` — logical audio cue requests
    (captions play regardless; AUDIO-013 queue).
  - `checkpoint: auto-saved at <scene>` — SAVE-004 rolling checkpoint
    writes (slot `checkpoint`).
  - `SaveGameV3 written to <path>` / `restored...` / `load failed: ...` —
    save/load outcomes including backup recovery.
  - `input-device: gamepad via <event>` — device-switch trace
    (AUDIO-004-era diagnostic, low volume).
  - `journal.changed` / `knowledge.changed` kernel events surface through
    the same presentation pipeline.
- `user://logs/godot.log` — the engine's own rolling log (same content).
- `user://savegames/` — `SaveGameV3` slots: `quick`, `checkpoint` (rolling,
  SAVE-004) plus user-facing slots; atomic primary + backup per slot.

## What is intentionally NOT logged

- Document contents, journal entry text, dialogue lines — only IDs.
- Personal data: none exists in the runtime.
- Any debug teleport/state-edit controls: the release build exposes none
  (verified by the TEST-007 source guard and the release package scan in
  RELEASE-002 scope).

## Support-session recipe

1. Reproduce with the shipped build; quit normally.
2. Attach `user://logs/godot.log` and the newest
   `user://savegames/<slot>.savegame-v3.json` (+ `.backup.json`).
3. The log's `zone-loaded` / `scene.entered` lines reconstruct the route;
   the save's `runtime.eventSequence` pins the narrative progress.

## Boundaries

- The format above is the diagnostics contract for Act I; new log lines must
  keep the `subject: fact` shape and must not include document/journal text.
- Release packaging (RELEASE-002) re-verifies that no debug/UI mutation
  controls ship; this document does not change that gate.
