# Style calibration candidate

Status: **OPEN — bounded technical A/B evidence only; this is not an art-lock decision.**

Each 1 920 × 1 080 pair renders one isolated `SubViewport` scene. The candidate duplicates the scene's `WorldEnvironment` resource and overrides only its presentation `WorldEnvironment`, `DirectionalLight3D` and `OmniLight3D` values. The disposable clone preserves visual descendants, zeros physics layers/masks and removes only collision shapes; production collision is untouched. Production scenes, material owners, shaders, GLB/registry, saves and narrative are untouched.

| Scene | Baseline SHA-256 | Candidate SHA-256 |
|---|---|---|
| day_street | `242673f2cfa56926b43eea698ee11b3b1eb9a10673e0be0ab321d10ee9f2ce39` | `d6887d013c08340ee6197b6126c4d55b2c10ab0b0e9637f2be2fccdd9700a3d8` |
| house_old_pc | `d17f13f0386b84560b614fcadfe2ecf1cab6e76aecbfc6065652b2d91541fd47` | `00396d56324728735d479835b28d9309f902a523ae5c991e9751f7314e4ca2b8` |
| kara_urman_edge | `1e0da8a762b9528e879c3167f3eb470bd1520a3cc4c9e900058ecedda967086f` | `3b9fdab9a7fc85f41c834ee6245e904005d22f52622576ec65c46ac711494674` |

Run `./eng/capture-style-calibration-candidate.sh` on a real Metal/Forward+ host. The wrapper validates the manifest, all six 1080p PNGs and SHA-256 values. Visual, cultural, motion, release-hardware and final art-lock review remain OPEN.
