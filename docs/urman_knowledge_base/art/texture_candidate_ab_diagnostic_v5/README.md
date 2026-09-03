# Texture candidate A/B diagnostic

Status: **OPEN — technical capture PASS, production/material acceptance not decided.**

This is a test-only diagnostic. Each cell uses a fresh benchmark instance and its own physics world. It clones presentation materials in memory, verifies the expected 9×28/216-cell relief collider by instance identity, checks every puddle patch within ±0.010 m, and never changes runtime material owners, shaders, collisions, saves or narrative.

Earth sheets use columns: `v3 relief-only`, `v3 relief+wetness`, `v5 relief-only`, `v5 relief+wetness`. Day, Kara-Urman and Zirat are captured both along and across the road. Wood sheets use columns `v3`, `v5` in the day, house/old-PC and Kara-Urman benchmarks.

The result stays OPEN because images require human review for double relief, 20–30 m repetition, triplanar striping, warm/neutral lighting response and wetness acceptance.

## Exact capture hashes

- `godot_day_street_earth_v3_v5_along_ab.png` — `8a86bc90b47611144858b13f73fdb29eb4ccbdc4cfcfb94f42c5901316a7c45b`
- `godot_day_street_earth_v3_v5_across_ab.png` — `6934e28c7ac74f3d009b19d5fb3092c185a0c8f9b2d0dceacca0b78220d1a6e8`
- `godot_kara_urman_edge_earth_v3_v5_along_ab.png` — `9833502b453c247cdfd497829d8aed6ee95ccc2eab75654146515d26835fe49e`
- `godot_kara_urman_edge_earth_v3_v5_across_ab.png` — `859371fa0ba3c3cb244218fab8c57a1420bcf3d5ebe9e7b50c6ff1f062850654`
- `godot_zirat_road_earth_v3_v5_along_ab.png` — `6b14a338ff2a0005c2f08f57e280b2511b9afdda2b0a261b2bdeae4cb5e42d03`
- `godot_zirat_road_earth_v3_v5_across_ab.png` — `e790cbde11ed2ebf461b3bf1dc3e1abb3a5e69d72d46a3e6a4f074f017def103`
- `godot_day_street_wood_v3_v5_ab.png` — `484f94d1d5e668f8d442d44a0e3ec8c73085cd361fd751eef693dcb7d0f71e41`
- `godot_house_old_pc_wood_v3_v5_ab.png` — `3f6445551aa4a872d81e7e4a7c9441dbc01ad73c7e3a6ce5d6d48f199eb3e3f6`
- `godot_kara_urman_edge_wood_v3_v5_ab.png` — `5150c70591dc1db41b31c241d2367dd925edf767c2b8f278e011034f25d08a35`

Candidate input hashes and all collider/contact samples are recorded in `texture_candidate_ab_diagnostic_manifest.json`.

Verification: `./eng/capture-texture-ab-diagnostic.sh`
