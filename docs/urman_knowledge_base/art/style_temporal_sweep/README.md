# Godot temporal style sweep

Дата capture: 2026-08-14 (visual-preserving render-only collision sanitization). Engine: Godot 4.7.1 .NET, Forward+, Metal 4.0,
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
| Day street | [temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png) | `46bf62b77985323f8a11f6ffd45ce808b2c7dc1e5c785c3866b006cb50423236` |
| House / old PC | [temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_house_old_pc_temporal_sweep_1080p.png) | `785ad834ac354aa098c9212e593def8c0c32219e972d5950be4a1906d6fd2c6d` |
| Kara-Urman edge | [temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_kara_urman_edge_temporal_sweep_1080p.png) | `0cd91d711e4997f0ec2ce3226db97534e5d1a19629bec57b8d5689cf11b8fbf1` |

![Day street temporal sweep](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/godot_day_street_temporal_sweep_1080p.png)

Manifest: [style_temporal_sweep_manifest.json](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_temporal_sweep/style_temporal_sweep_manifest.json), SHA-256 `57ac2b4ae0b81883c30de7b54e41e24b6c9b8be620d7cef3c629e662fb17d66d`.

The current receipt is the post-visual-sanitization run. For a separate,
test-only ambient/fog/key-light A/B, see
[`style_calibration_candidate/`](/Users/unterlantas/Documents/GitHub/URMAN/docs/urman_knowledge_base/art/style_calibration_candidate/);
neither receipt activates a runtime presentation switch or closes the art lock.

## Deterministic results

- 3 scenes × 3 FOV values × 2 modes × 12 frames = 216 real Metal/Forward+
  frame samples.
- Every render was non-empty, every tile had zero sampled black pixels, and
  the helper log contained no `ERROR:`, `SCRIPT ERROR:` or ObjectDB/RID leak.
- Before every sample the disposable clone fail-closes unless it preserves
  authored visual meshes and disables all physics-query owners. The current
  receipts preserve 806/109/826 visual meshes for day/house/Kara while removing
  286/22/695 `CollisionShape3D` nodes; all resulting collision layer/masks are
  zero.
- Reduced-motion rows report `camera_vertical_peak = 0`; the harness therefore
  verifies that the presentation flag can remove the vertical component without
  touching gameplay state.
- Mean frame-to-frame luminance deltas stayed below 0,013 across all scene/FOV
  combinations. These numbers describe this fixed camera sweep, not a medical
  comfort threshold.

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
