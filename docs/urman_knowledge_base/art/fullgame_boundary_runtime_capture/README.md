# Act 5 runtime-backed boundary capture

Status: **OPEN / technical PASS**. This is a production-candidate receipt, not an art lock.

The harness instantiates `res://scenes/full_game.tscn` with the real `RuntimeBridge`, loads the authored Act 5 boundary, and verifies that `FullGameZone` creates both compiled interaction targets:

- `urman.fullgame:interaction/act5-aidar-choice`
- `urman.fullgame:interaction/act5-boundary-to-epilogue`

GateA visible LOD meshes: **8** (four LOD0 + four LOD1). Minimum horizontal clearance to either runtime marker: **2.687 m**. Behind both markers by >=1 m: **True**.
Godot zone scene: `res://scenes/zones/fullgame/act5_boundary.tscn`. Kernel entry scene before story dispatch: `urman.fullgame:scene/act2-house` (the harness does not dispatch a story transition). No story commands, saves, materials, shaders or packed scenes were mutated.

Captures:

- `standard` — `docs/urman_knowledge_base/art/fullgame_boundary_runtime_capture/godot_act5_boundary_runtime_standard_1080p.png`, 1920x1080, SHA-256 `eadb15d9f593685244d1e7ad59db74f5b7ac5d96458ac3bad6071433887a8ed7`, markers inside=True, gate overlap=True
- `markers` — `docs/urman_knowledge_base/art/fullgame_boundary_runtime_capture/godot_act5_boundary_runtime_markers_1080p.png`, 1920x1080, SHA-256 `ccfbdb4eb043c978ef3ab636d6cd06ffbb6fdca2445f6c44643da9c083982128`, markers inside=True, gate overlap=False

The dedicated `markers` camera keeps both runtime interaction markers inside the viewport with no GateA screen-space overlap. The `standard` camera is a distant composition check and may overlap the marker projection. Remaining gates: screen-space visibility during observed traversal, GateA family repetition, cultural/level-art review, release-host performance, accessibility, audio and full playthrough.
