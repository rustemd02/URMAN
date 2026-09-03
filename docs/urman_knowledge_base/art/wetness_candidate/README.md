# Wetness candidate capture

Status: **OPEN — candidate evidence only; roughness acceptance is not claimed.**

This harness instantiates the three mandatory style benchmarks plus `chapter1_zirat_road.tscn` one at a time, so relief raycasts cannot hit a collider from another benchmark. It duplicates only the fifteen existing puddle `ShaderMaterial` overrides in memory and sets the duplicate `roughness_value` to `0.50`; source/runtime material roughness remains `0.90`.

- Contact gate: **PASS** (tolerance ±0.010 m)
- Puddle clusters: `5/5`
- Puddle patches: `15/15`
- Candidate ShaderMaterial clones: `15`
- Candidate material isolation: **PASS** (in-memory only)
- Candidate renders: **written after contact PASS**

The day-street, Kara-Urman-edge and Zirat-road 1080p images are test-only roughness candidates. They do not activate runtime materials or close the wetness/art-lock gate.

## Contact receipt

See `wetness_candidate_manifest.json` for every patch's sampled relief height, puddle bottom, gap and collider check.

Verification command: `./eng/capture-wetness-candidate.sh`
