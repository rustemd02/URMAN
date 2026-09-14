# Godot temporal style sweep

Дата capture: 2026-09-14 (rerun of the visual-preserving render-only collision sanitization on current base-scene code). Engine: Godot 4.7.1 .NET, Forward+, Metal 4.0,
Apple M4 Pro. Command: `./eng/capture-style-temporal-sweep.sh`.

Статус: **technical temporal evidence only; external comfort review and art
lock remain OPEN**.

Harness renders each mandatory style scene at FOV 65°/75°/90° for 12 frames in
two presentation modes: `head_bob_on` uses the current first-person amplitudes
(vertical peak около 0,0208 m, horizontal peak 0,012 m), while `reduced_motion`
disables vertical bob and keeps the same small lateral camera sweep. Each tile
is 480×270; each contact sheet is 1 440×540 with the top row as head-bob mode
and the bottom row as reduced-motion mode. The manifest records sampled-frame
luminance deltas, visible-range values, black-pixel fraction and camera peaks.

## Contact sheets

| Scene | Preview | SHA-256 |
|---|---|---|
| Day street | [temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png) | `5986c5457410b4bdd92e091e04aacbd6d16a248f13cf381c5b3463ba0169a932` |
| House / old PC | [temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_house_old_pc_temporal_sweep_1080p.png) | `66e20edaeb4cfe5dbb6d2c33f0e7f1dc0449ddeaea0c3cf5647aa8c064f1d5b4` |
| Kara-Urman edge | [temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_kara_urman_edge_temporal_sweep_1080p.png) | `d669cb7646727871f02e0d24fb31b83ddae829bd592d4df58559e8558270cc7c` |

![Day street temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png)

Manifest: [style_temporal_sweep_manifest.json](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/style_temporal_sweep_manifest.json), SHA-256 `8d026dea73ab79d350ccfba451421405209a7f39a47792a9fd68290d4bdea5ed`.

The current receipt is the post-visual-sanitization run. For a separate,
test-only ambient/fog/key-light A/B, see
[`style_calibration_candidate/`](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_calibration_candidate/);
neither receipt activates a runtime presentation switch or closes the art lock.

Scene basis (2026-09-14): this harness still instantiates the raw
`style_benchmark_*` scene, so its tiles show the benchmark ground, trees and
stand-in props that `Act1ConnectedWorld` hides at runtime. Read it as a temporal
comfort measurement of the base scene, not as the shipped look. The 27-cell
[`../style_motion_sweep/`](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_motion_sweep/)
sheets now assemble the connected world and are the current spatial evidence for
the shipped look. The manifest key is now `captured_at_utc` with the real capture
time; it used to be a hardcoded `2026-08-14` literal that relabelled every rerun.

## Deterministic results

- 3 scenes × 3 FOV values × 2 modes × 12 frames = 216 real Metal/Forward+
  frame samples.
- Every render was non-empty, every tile had zero sampled black pixels, and
  the helper log contained no `ERROR:`, `SCRIPT ERROR:` or ObjectDB/RID leak.
- Before every sample the disposable clone fail-closes unless it preserves
  authored visual meshes and disables all physics-query owners. The 2026-09-14
  receipts preserve 2072/1723/1065 visual meshes for day/house/Kara while
  removing 288/28/654 `CollisionShape3D` nodes; all resulting collision
  layer/masks are zero. The earlier 806/109/826 and 286/22/695 counts in this
  file belonged to the 2026-08-14 base scene and moved with the winter dressing.
- Reduced-motion rows report `camera_vertical_peak = 0`; the harness therefore
  verifies that the presentation flag can remove the vertical component without
  touching gameplay state.
- Mean frame-to-frame luminance deltas stayed below 0,009 across all scene/FOV
  combinations (highest sample 0,0084, house/old PC at FOV 65° in head-bob
  mode). These numbers describe this fixed camera sweep, not a medical comfort
  threshold.

## What this closes

- Reproducible temporal render evidence exists for all three mandatory scenes.
- The current head-bob amplitude is explicitly measured rather than inferred
  from a still frame.
- Reduced-motion presentation is visibly exercised at the same FOVs.

## What remains open

The render-only clone preserves `CollisionObject3D` visual descendants, zeros
their collision layer/mask and removes only `CollisionShape3D` nodes before
each sample. It awaits one SceneTree frame after freeing the viewport; this
keeps evidence cleanup separate from production collision ownership and leaves
the capture process free of native RID leak diagnostics. The day row includes
the authored road-relief mesh. The house row includes the
bounded imported OldPc tower/panel/button detail pass, but not a final hero
asset.

This is **not** an external motion-sickness or usability pass. Testers still
need to traverse the actual game at different speeds, mouse/gamepad look rates,
FOV settings and display refresh rates, with reduced motion on and off. It also
does not accept the art lock: the day street remains a greybox village family,
the house has only a bounded imported OldPc detail pass rather than a final hero
asset, and the Kara-Urman crown/fog family
still needs authored geometry and cultural review. M1/Windows performance,
authored audio, cultural review and full playthrough remain independent gates.

## Superseded pre-visual-sanitization temporal evidence — 2026-08-14

The prior temporal harness freed entire `CollisionObject3D` nodes. Because
many authored `MeshInstance3D` nodes are descendants of `StaticBody3D`, its
contact sheets captured a different, depleted scene even though their basic
non-empty checks passed. Those PNGs and manifest are retained unchanged under
`superseded_pre_visual_sanitization/` for audit, but are not valid GODOT-003
temporal evidence. The current top-level receipt above supersedes them.

## Historical lighting/fog calibration receipt — 2026-08-14

After the bounded `StyleBenchmarkZone` calibration, the 216-sample temporal
run was repeated without changing the camera contract or gameplay state.

| Scene | Current contact sheet | SHA-256 |
|---|---|---|
| Day street | `godot_day_street_temporal_sweep_1080p.png` | `a3605f65e79b13572cd37bab7080eca39f5ad763143592c14bd51c5d3d6c9153` |
| House / old PC | `godot_house_old_pc_temporal_sweep_1080p.png` | `a6bd040c63adba14914e02fcc4cdd94bf55d74ebb00230b92e15a5097539152d` |
| Kara-Urman edge | `godot_kara_urman_edge_temporal_sweep_1080p.png` | `0160b77e20104f2bf9d266cbe0a62ff891727575f4c3c6cacf536579dd1f6af2` |

Manifest SHA-256: `b1e99d145ab2f493a264eb4343c89acdd476a063a44ea9d9690203ba94d4af3e`.
The 216 samples remain technical evidence only; external comfort review and
the visual art lock are still OPEN.

## Semantic material-owner split rerun — 2026-08-14

The 216-sample temporal harness was repeated after the imported wood/cloth
owner calibration. All scenes, FOV values and reduced-motion rows rendered
without blank cells or Godot/RID/ObjectDB leaks:

| Scene | Current contact sheet SHA-256 |
|---|---|
| Day street | `b8109a4ad6a9b7d9db7755f643ffe47e0934f7bfb09dbda4e394ba48fe7ec8b7` |
| House / old PC | `a6bd040c63adba14914e02fcc4cdd94bf55d74ebb00230b92e15a5097539152d` |
| Kara-Urman edge | `55a4f4af5df4035377a621ee128b1aada5258fae1daafee1da58b80a854c3f5d` |

Manifest SHA-256: `7a93d3274e1d31e0733201251354214b485ae306e507126037435e414a392d3e`.
The receipt remains technical evidence; external comfort review and the art
lock are still OPEN.

## PineA collision-isolation rerun — 2026-08-14

The 216-sample Metal/Forward+ temporal sweep was repeated after the adapter
removed imported collision descendants from the modular-kit instances. The
render-only harness still produced 216 valid samples (three scenes × three
FOVs × two motion modes × twelve frames), with zero black tiles and no
Godot/RID/ObjectDB leaks.

| Scene | Current contact sheet SHA-256 |
|---|---|
| Day street | `b8109a4ad6a9b7d9db7755f643ffe47e0934f7bfb09dbda4e394ba48fe7ec8b7` |
| House / old PC | `a6bd040c63adba14914e02fcc4cdd94bf55d74ebb00230b92e15a5097539152d` |
| Kara-Urman edge | `19f4b896c5ed7cc353a998c0b3dddda5657cfef26dc7cad62b8596eba0344161` |

Manifest SHA-256: `a111c42a31a01fde0b82ec95cac879d2089e686db91856cb82997c85697748df1`.
This is technical evidence only; external motion-sickness review, release-host
testing and the art lock remain OPEN.
