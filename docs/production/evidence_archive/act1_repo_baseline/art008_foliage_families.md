# ART-008 Inspection — Foliage Families & Density
- 2026-09-03, commit 65b42a0; no code edit.
- Foliage layer: `AgentB_FoliageKit` + `AgentB_PlantedFoliage` plan with
  smoke-enforced contract: planted child count > 0, planted entry count ==
  planned entry count, node count == child count, `minimumFoliageRoadClearance`
  meta and `foliageRebasePolicy` asserted by `Act1DemoLaunchSmokeTest:46-88`;
  the foliage source library stays hidden after the deterministic extraction
  pass (decision log 2026-08-24), so the template source is never visible.
- Kara/zirat-specific masses come from their kits (birch mass, sedge, understory
  walls) rather than repeated generic pines.
- Open: repetition-heatmap and species/motion acceptance are human
  (Z01-006..Z08-006); route clearance contract unchanged.
