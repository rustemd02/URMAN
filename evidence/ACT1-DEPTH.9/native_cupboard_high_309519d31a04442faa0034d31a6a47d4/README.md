# ACT1-DEPTH.9 cupboard High-request diagnostic capture

Protected native Windows capture 309519d31a04442faa0034d31a6a47d4 used sealed snapshot 87c7d2aec0b98099d60d636000406ae4a2c5d6a65a0efb39aae95610fb32a0e4 from base 8962bf8ecc6214f6476df64946b08bf666937449. The full 1825-file manifest is preserved with SHA-256 817153ccc37dd24c557b9aae8d206e1c9d9e97fe233787c0ac29b20a21a904bb. Preseal provenance includes 33 selected runtime files (the prior 32 plus Act1ConnectedWorld.RoadsideGrounding.cs) and 10 fresh local-only authoring/evidence copies; local-only copies were excluded from the station payload.

- The two exact existing house_old_pc checkpoint views were requested at High, FOV70, with a 300-second game timeout and no environment or phase override. The station runner/build/import/game result is PASS, all reported build/content/import/game exit codes are 0, and user data was restored.
- The capture did not retain High quality. Startup performance rescue switched graphics to Low (avg=991.2ms, median=109.4ms, reason=slow-startup). Both preserved sidecars say requested High/effective Low, graphicsSessionOverride=performance-rescue, graphicsComparable=false, scale 0.70. These are not valid High comparison frames.
- Original PNGs are 1886×1061 while sidecars report a 1920×1080 viewport. engine-errors.log and stderr.log are empty; all original logs and sidecars are retained.
- Post-run doctor is ready, with no active job, orphan game process, or pending userdata recovery. Engine: 4.7.1.stable.mono.official.a13da4feb; SDK: 10.0.302; renderer: Vulkan Forward+ / NVIDIA GeForce GTX 970.
- actual-cupboard-material-census.json exposes the six-entry readback from the original detail sidecar. The original sidecar remains authoritative.
- Root visual review is pending. This static capture does not establish whole-interior style, interaction, or an ordinary player route. The raw result ZIP remains in the ignored remote artifact cache; its SHA-256 is recorded.
