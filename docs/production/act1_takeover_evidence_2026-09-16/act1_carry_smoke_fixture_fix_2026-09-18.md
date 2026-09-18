# EX01–EX05 carry smoke: machine-dependent fixture fixed (2026-09-18)

Build: HEAD `04f039e` + the working-tree change below, `Urman.Game.dll`
sha256 `5adc8f4d57f8d5bbf2fa82ed420f07f3e383baf6af161dc73ca40afe741f23ba`
(pinned .NET 10.0.302, Godot 4.7.1.stable.mono, macOS arm64).

## What was broken

`act1_carry_interaction_smoke_test` failed on this host at its third check,
"aimed pickup commits one held item". The interaction ray hit
`YardSupport_0_Post` (`AgentB_ArchitectureCollision`, 0.36–0.48 m from the
camera) instead of the log.

Cause: the fixture placed every stance at a fixed 1.25 m behind the target.
For `carry-log` that point lies inside the yard canopy post's collision added by
`Act1WorldLayout`/`Act1ConnectedWorld.AuthoredKitCollision.BuildYardSupportContacts`.
The capsule was therefore pushed out by the solver, and the resolved camera
position differs per machine and per run, so the aim ray clipped the post on one
host and passed beside it on another.

Two further harness defects surfaced while fixing that one:

1. A press held for a fixed two frames can fall entirely between two physics
   steps on a fast windowed run; the game reads `interact` in its physics step
   and never saw the press. This made windowed runs flaky at a different check
   on every attempt.
2. The stance was measured before the capsule finished settling on its ground,
   so a placement point measured just before the press could differ from the
   committed transform.

## What changed (test harness only, no game code)

`game/tests/Act1CarryInteractionSmokeTest.cs`:

- `StandFacing` walks a ring of eight directions at four natural reach
  distances (0.95–2.0 m, all inside the coordinator's 2.7 m reach), requires
  the real capsule to fit (`CanStandAt`), waits for a grounded, still player,
  and accepts only a stance whose first aim-ray hit is the expected target node
  when the fixture names one. A target no stance reaches fails the run.
- The disposable layer-2 occlusion fixture is seated on the stance-to-target
  line instead of a fixed world offset, so it blocks the aim from whichever
  side the ring picked.
- `Press` holds the action until the coordinator has picked it up (or 20
  physics frames pass), then releases.

## Verification

All three runs on the build above; userdata guard active, originals restored
byte-for-byte.

- headless via `eng/run-smoke-guarded.sh act1_carry_interaction_smoke_test`:
  `act1-carry: PASS 29 meaningful local checks`.
- windowed (real rendering device, 1280x720) via `eng/protected_run.py`: three
  consecutive PASS runs, 29 checks.
- windowed with `URMAN_CARRY_CAPTURE=1`: `PASS 35 meaningful local checks`,
  six frames in `carry_frames/`; terminal output in
  `act1_carry_smoke_2026-09-18.log`.

Covered by the passing run: take, blocked aim, single held item, rejected
second ownership, modal gating, checked placement, re-take, shovel held
physically, aimed snow clearing (only the aimed opening), the physical service
gap barrier losing its collision, the uncovered axe becoming takeable, save
with cleared yard + held object, load of the pristine snapshot resetting
deviations, load of the cleared snapshot restoring them, no story transition
from optional work, New Game clearing local consequences, and pickup after
re-registration.

## Limits

Mechanical verification only. No human walkthrough, no art/audio listening
claim, and no claim about the 60-minute experience. The windowed captures in
`carry_frames/` are the same fixtures the test asserts, not a playthrough.
