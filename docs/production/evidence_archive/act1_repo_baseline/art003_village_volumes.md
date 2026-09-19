# ART-003 Inspection — Village Exterior Full Volumes
- 2026-09-03, commit 65b42a0; no code edit.
- Authored kit `urman_village_exterior_kit` (106 meshes / 3,512 tris / 17
  materials; authored meshes only, LOD0, no physics nodes per manifest) with 6
  placed components: DwellingFacade_TimberPlaster, OutbuildingShed_Low,
  FenceSegment_RoughPicket, Gate_CrookedTimber, Woodpile_StackedLogs,
  Well_YardLandmark (`Act1ConnectedWorld.cs:19-27`), integrated through typed
  `Act1ExteriorParcelComponentPlacement` rows and protected by the exterior
  framing builders (house/FAP/zirat scopes).
- Procedural/candidate masses remain suppressed via reason-tagged
  suppressions; visible-volume acceptance (no one-sided facade in final views,
  floating-foundation check) is the open human 360 review (CAPTURE-006,
  Z02-003/Z03-003). Local plausibility review = CULTURE-002.
