# Wetness roughness matrix v2

Status: **OPEN — production-candidate evidence only; wetness/roughness acceptance and art lock are not claimed.**

The test instantiates each benchmark alone for contact sampling and records its raw authored puddle origins before any test-only candidate action. All production origins must now pass the strict contact gate without candidate-local alignment. It creates fresh in-memory `ShaderMaterial` clones for each matrix row (`0.40`, `0.50`, `0.60`). Source/runtime material roughness remains `0.90`; source overrides are restored before shutdown. No shader, runtime registry, save, narrative, scene, GLB or PainterlyMaterialLibrary owner is changed.

- Candidate contact + exact relief-owner gate: **PASS** (±0.005 m)
- Raw production puddle origins at the same ≤5 mm threshold: **PASS** (reported only; never changed)
- Source/runtime `roughness_value=0.90`: **PASS**
- Per-row clone isolation: **PASS** (15 fresh clones × 3 rows; in-memory only)
- Candidate PNGs: **9 written after all pre-render gates passed**

Each PNG is only an isolated test candidate. The harness retains candidate-local alignment only as a fail-closed diagnostic; this receipt requires it to remain zero for every production patch. It does not activate production puddle materials and cannot close the wetness, art-lock, temporal-comfort, authored-geometry, cultural or release-hardware gates.

## Verification

Run `./eng/capture-wetness-candidate-matrix-v2.sh`. The wrapper validates manifest shape, source roughness, exact contact owner, all three rows, 9 PNG dimensions and SHA-256 values.
