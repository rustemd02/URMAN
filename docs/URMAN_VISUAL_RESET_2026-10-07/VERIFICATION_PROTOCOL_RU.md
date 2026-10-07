# Verification protocol

- Сравнивать один camera/FOV/resolution/time/weather pair. DevView fallback на world coords при missing Building запрещён для доказательства.
- Visual state metadata должно сообщать фактически применённый atmosphere profile, включая preview override.
- Geometry before material: grayscale/form capture before beauty where applicable.
- Snow: check access intersections, elevation, footprint, road/trail continuity.
- Materials: run `eng/verify-painterly-textures.sh`; проверять близко/средне/далеко.
- Motion: не принимать global pixel difference за character animation; нужны pose/body-relative checks.
- Final route: 44 checkpoints accounted, black/missing subject cannot PASS.
- Performance: target из свежего handoff — base M1 High 50–60 FPS without degrading High. Report raw frame times p50/p95/p99; no arithmetic sum of overlapping monitors.
- Human review: author art acceptance + language/cultural reviewer where required.
