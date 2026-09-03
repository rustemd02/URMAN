# Texture candidate A/B diagnostic

Status: **OPEN — technical capture PASS, production/material acceptance not decided.**

This is a test-only diagnostic. Each cell uses a fresh benchmark instance and its own physics world. It clones presentation materials in memory, verifies the expected 9×28/216-cell relief collider by instance identity, checks every puddle patch within ±0.010 m, and never changes runtime material owners, shaders, collisions, saves or narrative.

Earth sheets use columns: `v3 relief-only`, `v3 relief+wetness`, `v4 relief-only`, `v4 relief+wetness`. Day, Kara-Urman and Zirat are captured both along and across the road. Wood sheets use columns `v3`, `v4` in the day, house/old-PC and Kara-Urman benchmarks.

The result stays OPEN because images require human review for double relief, 20–30 m repetition, triplanar striping, warm/neutral lighting response and wetness acceptance.

## Exact capture hashes

- `godot_day_street_earth_v3_v4_along_ab.png` — `8a86bc90b47611144858b13f73fdb29eb4ccbdc4cfcfb94f42c5901316a7c45b`
- `godot_day_street_earth_v3_v4_across_ab.png` — `6934e28c7ac74f3d009b19d5fb3092c185a0c8f9b2d0dceacca0b78220d1a6e8`
- `godot_kara_urman_edge_earth_v3_v4_along_ab.png` — `9833502b453c247cdfd497829d8aed6ee95ccc2eab75654146515d26835fe49e`
- `godot_kara_urman_edge_earth_v3_v4_across_ab.png` — `859371fa0ba3c3cb244218fab8c57a1420bcf3d5ebe9e7b50c6ff1f062850654`
- `godot_zirat_road_earth_v3_v4_along_ab.png` — `6b14a338ff2a0005c2f08f57e280b2511b9afdda2b0a261b2bdeae4cb5e42d03`
- `godot_zirat_road_earth_v3_v4_across_ab.png` — `e790cbde11ed2ebf461b3bf1dc3e1abb3a5e69d72d46a3e6a4f074f017def103`
- `godot_day_street_wood_v3_v4_ab.png` — `8f74640c248dcc18e46f95d483c2c8a79ed493a17184e278eab06775ec12d5b1`
- `godot_house_old_pc_wood_v3_v4_ab.png` — `3f852fce9dc7f1e3a79cec77c313db4499605e07a322c3b0c66130c792080cf9`
- `godot_kara_urman_edge_wood_v3_v4_ab.png` — `02d3e7387512dee214e69024e077114e66801561cd93e08b82903b14f983a140`

Candidate input hashes and all collider/contact samples are recorded in `texture_candidate_ab_diagnostic_manifest.json`.

Verification: `./eng/capture-texture-ab-diagnostic.sh`
