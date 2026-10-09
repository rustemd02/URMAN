# Native geometry diagnostic capture

- Job `7f64b86109eb4a1ab848469fc105e9d2`, snapshot `85dc8e29e0fcd359ba56ba4fe7f3b97f5a5a0bd523e589dd20a60adc6d216bdc`; native capture PASS on UNTERPC, `res://scenes/act1_demo.tscn`, Godot 4.7.1.stable.mono.official.a13da4feb, .NET 10.0.302. Result archive SHA-256: `4c45e619eecdc91050c6bfd1dd9e7d3fcb70f828603350fd75e3645451cb975d`.
- Three original PNGs and their JSON sidecars are under `frames/`: `babai_porch_side_clear`, `h019_geometry_approach`, and `h019_geometry_shoulder`. FOV 70 was explicit. No zone/scope override was sent; all captured points remained in the default `village_day` zone.
- Each of 15 `source_snapshot/` copies matches the sealed station manifest. The pre-submit source proof is retained separately as `source_snapshot/local-source-proof.json`.
- The Blender generator, its `.blend`, and `assets/asset_registry.json` were present in the local pre-submit checkout but absent from the worker's captured manifest. They are preserved under `local_only_source_context_not_transmitted/` and are not claimed as native-run inputs. The rendered GLB and authored world/scene inputs were captured and hash-verified.
- Guard: receipt reports `userdataRestored=True`; post-run doctor reports ready, no active job/orphan process, and no pending recovery. Root original-image review is recorded in `root_visual_review.json`: porch footprint stage accepted, H019 rejected; whole-house art remains open.

Fetched logs and `result.zip` are retained byte-for-byte. `.gitattributes` disables text normalization. `archive-sha256.json` lists all local files except itself.
