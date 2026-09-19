# MAP-008 Verification Receipt — Zirat → Kara-Urman Escalation Corridor

- Date: 2026-09-03
- Commit: `0549e7c` (branch `main`)
- Method: canonical-owner inspection + existing capture evidence; no code edit.

## Authored escalation (geometric, not fog-only)

- **Two authored road envelopes**: `ZiratRoadEnvelope` (z −53.5 → −89.5,
  width 4.2 m, earth `5d624f`) then `KaraApproachRoadEnvelope` (z −103 →
  −122.5, width 3.5 m, darker `526052`) — the route measurably narrows and
  darkens across the corridor (`Act1ConnectedWorld.cs:5065,5202`).
- **Asymmetric banks and roots**: `KaraAsymmetricWestBank` (2.2 m band, drop
  0.34) vs `KaraAsymmetricEastBank` (1.5 m, 0.22); three root clusters
  (`KaraRootBankWest/East`, `KaraThresholdRootWest`) plus the Kara kit's
  `FallenLogCluster`/`MossyBoulderCluster`/`CrookedStump` break the ground
  line; lateral forest shelves and distant edge windows (`KaraLateralForestShelf*`,
  `KaraDistantEdgeWindow*`) give mid/far mass on both sides.
- **Retreating village mass**: `ZiratDistantVillageMass` and the zirat
  village-edge parcel group sit behind the player on this leg; the
  `kara-edge-approach` connector (19.5 m, width 3.5 m, MAP-003) is fully
  bidirectional — the walkthrough walks to the cliffhanger and the back
  capture frame proves the retreat view renders (frame 41).
- **Bounded value separation, not a black wall**: the Kara night accent and
  value-separation cues are smoke-protected (3 `KaraAccentLights`,
  decision log 2026-08-24 "Add bounded Kara night value-separation cues");
  fog-as-sole-escalation is a reopen condition and audio escalation is owned
  by AUDIO-007 (not simulated here).

## Former blocker removed with evidence

Tracker: "все пять current capture views одинаковы". Diag-run2 kara_approach
frames have five distinct SHAs — `59fa8375…`, `d5b90658…`, `6ad3b186…`,
`3dd4dbe8…`, `173c8bbc…`, all `same_as_prev=false`
(`evidence/act1_repo_baseline/base005-capture-diag-run2-PASS-44of44.txt`).

## Open (human, stays with this task)

- Bidirectional manual traversal video + forward/back/side/up/down capture
  review (NEW VERIFICATION REQUIRED); escalation-readability judgement is
  human. No forced movement, audio cut or creature silhouette was introduced.
