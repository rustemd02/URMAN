# Wetness candidate capture

Status: **RETIRED as a shipped-look question — diagnostic evidence only; the shipped wet read comes from the authored wet village road kit (see below). Roughness acceptance of the shipped surfaces is still human.**

This harness instantiates the three mandatory style benchmarks plus `chapter1_zirat_road.tscn` one at a time, so relief raycasts cannot hit a collider from another benchmark. It duplicates only the fifteen existing puddle `ShaderMaterial` overrides in memory and sets the duplicate `roughness_value` to `0.50`; source/runtime material roughness remains `0.90`.

- Contact gate: **PASS** (tolerance ±0.010 m)
- Puddle clusters: `5/5`
- Puddle patches: `15/15`
- Candidate ShaderMaterial clones: `15`
- Candidate material isolation: **PASS** (in-memory only)
- Candidate renders: **written after contact PASS**

The day-street, Kara-Urman-edge and Zirat-road 1080p images are test-only roughness candidates. They do not activate runtime materials or close the wetness/art-lock gate.

Scene basis and retirement (2026-09-14) — measured, not assumed. This harness
instantiates the raw `style_benchmark_*` scenes, and the surfaces it clones
(`puddleGeometry` clusters, roughness raised from 0.90 to 0.50 in memory) are
hidden at runtime: `Act1ConnectedWorld.ApplyLogicalZonePresentationSuppressions`
hides every child with meta `puddleGeometry` in `village_day`, `zirat_road` and
`kara_urman_night`, and `PainterlyEnvironmentDetails.AddPuddleCluster` is called
from `StyleBenchmarkZone` only. The shipped wet read is the authored wet village
road kit instead, graded in `RebindWetVillageRoadMaterials` to `water`
(0.38 / 0.45 / 0.98), `wet_ground` (0.56 / 0.32 / 0.94) and `earth`
(0.78 / 0.16 / 0.70); the full table and the evidence frame are in
`../puddle_silhouette_candidate/README.md` and
`../style_frames/godot_day_street_1080p.png`.

The roughness sweep below therefore answers a question about a retired proxy. Its
numbers stay valid as diagnostics (they are honest measurements of those
surfaces), but the shipped-look decision it was made for is closed by the kit,
not by this matrix. What stays human is whether the shipped wet read — dark glint
in the ruts, the damp shoulder break and the ditch line under the snow blanket —
is convincing at player height and in motion.

## Contact receipt

See `wetness_candidate_manifest.json` for every patch's sampled relief height, puddle bottom, gap and collider check.

Verification command: `./eng/capture-wetness-candidate.sh`
