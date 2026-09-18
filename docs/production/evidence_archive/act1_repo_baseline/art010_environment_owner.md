# ART-010 Inspection — Single Day/Night, Rain, Fog Owner
- 2026-09-03, commit 65b42a0; no code edit.
- Contract already smoke-enforced: `CountActiveWorldEnvironments(connectedWorld)
  == 1` with the interior environment active only for the active interior and
  exterior zone environments nulled (`Act1DemoLaunchSmokeTest:60-96,107-109`);
  one exterior atmosphere owner (`AgentBEnvironment`); rain particles owned by
  the exterior layer and off inside; exactly three `KaraAccentLights`.
- Routing lives in `SetActiveLogicalZone` (`Act1ConnectedWorld.cs:356+`) —
  one owner, no second environment node authored anywhere in the demo chain.
- Open: per-zone exposure/fog/rain readability tuning and the human day/
  return/zirat/Kara/interior transition review (weather checklist in
  Z01-007..Z08-007); no second owner introduced.
