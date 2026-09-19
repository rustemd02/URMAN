# MAP-012 Verification Receipt — Canonical Eye Height / FOV / Reduced Motion

- Date: 2026-09-03
- Commit: `65b42a0` (branch `main`)
- Method: canonical-owner inspection + existing smoke/sweep evidence; no code
  edit.

## Contract coverage already proven

- **FOV clamp**: `FirstPersonController` applies
  `_camera.Fov = Clamp(settings.FieldOfView, 65, 90)`
  (`FirstPersonController.cs:139`); default 75. `PlayerSettingsSmokeTest`
  covers persistence and restoration of FOV (e.g. 82), head bob and
  `ReducedMotion` round-trips (exit 0 in every verify-godot run).
- **Reduced motion**: `AccessibilityPresentation` + player accessibility
  snapshot remove nonessential motion (head-bob gate
  `HeadBobEnabled => _headBob && !ReducedMotion`); temporal sweep evidence
  (`./eng/capture-style-temporal-sweep.sh`, 216 samples, FOV 65/75/90 ×
  bob/reduced-motion modes) proves the reduced-motion vertical component can
  be removed (documented in `playtest_plan.md`).
- **FOV composition cells**: the style motion sweep covers 27 near/mid/far ×
  FOV 65/75/90 cells per mandatory scene (`playtest_plan.md` evidence list).
- **Capture camera discipline**: the capture harness moves only the test-owned
  camera; debug-camera composition is a tracker-forbidden acceptance path and
  no acceptance evidence relies on it.

## Open (human, stays with this task)

- Route comprehension at 65/75/90 FOV in motion (near-occluders, sign scale,
  door reach, horizon) requires the observed full-route walk at three FOV
  settings plus contact sheets — NEW VERIFICATION REQUIRED; CAPTURE-004 owns
  the pitched supplemental frames and CAPTURE-006 the human review.
