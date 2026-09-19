# ART-007 Inspection — Terrain Envelope Grade & Contact
- 2026-09-03, commit 65b42a0; no code edit.
- Traversal terrain owner: `AgentBExteriorWorld` with exactly two declared
  collision owners (`AgentB_TerrainCollision:act1-exterior-terrain`,
  `AgentB_ArchitectureCollision:act1-exterior-architecture`); the capture
  harness asserts this metadata verbatim and scans for forbidden gameplay
  nodes outside the owners (`Act1FullRouteCoreWorldCapture.cs:264-293`).
- Visible road crown/grade is owned by the AgentB terrain road kit; the legacy
  connector RoadSurface presentation is deliberately suppressed
  ("AgentB_TerrainRoadKit owns visible crown", `Act1ConnectedWorld.cs:676`) so
  no second flat slab draws over the authored relief. Envelope landform
  segments (e.g. KaraAsymmetric banks, AddCoreRouteEnvelope bands) carry
  per-segment width/height relief.
- Open: manual low-angle route review and seam judgement (human); collision
  ownership unchanged — rollback condition per tracker.
