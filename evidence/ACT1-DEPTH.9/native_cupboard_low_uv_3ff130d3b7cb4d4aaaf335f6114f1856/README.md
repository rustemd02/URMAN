# ACT1-DEPTH.9 cupboard Low UV-candidate capture

Protected native Windows capture 3ff130d3b7cb4d4aaaf335f6114f1856 used sealed snapshot fcf629e5726943405df72aeee41ab8e3f1417e65de40330dc893179fed37d5c5 from base 8962bf8ecc6214f6476df64946b08bf666937449. The full 1825-file manifest is preserved with SHA-256 a006e4a06ff894e4685d91b321d2f5d08db3d51059c34b0fa62706884f3d0359. Preseal provenance has 33 selected runtime files and 10 fresh local-only authoring/evidence copies; the latter were excluded from the station payload.

- The two exact existing house_old_pc points were captured at actual Low, FOV70, timeout300, with no phase or environment override. Runner/build/import/game exit codes are 0; the station receipt verifies the snapshot and reports user data restored.
- Both original sidecars report requested/effective Low, no graphics session override, graphicsComparable=true, and 0.70 render scale. PNG IHDR is 1886×1061 while the sidecars report a 1920×1080 viewport.
- The detail sidecar census has six Body/Door/Pull entries for LOD0 and LOD1. CupboardDoor LOD0 and LOD1 read back authoredUvTexture=true, boundUvPigment=true, lowQuality=true. Body and Pull UV flags remain false. All raw sidecars, original images, and logs are preserved; actual-cupboard-material-census.json is a convenience extraction.
- visibleInTree and overlapping visibility ranges do not establish that both LODs were drawn in one frame. The previous High-request run was performance-rescued to Low, so it does not form a matched High pair.
- The post-run doctor is ready, with no active job, orphan process, or pending userdata recovery. Engine: 4.7.1.stable.mono.official.a13da4feb; SDK: 10.0.302; renderer: Vulkan Forward+ / NVIDIA GeForce GTX 970. Root visual review is pending. This static capture does not prove an ordinary route or whole-interior acceptance.
- The raw result ZIP remains in the ignored remote artifact cache; its SHA-256 is recorded in result-zip.sha256.
