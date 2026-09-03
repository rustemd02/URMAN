# Puddle silhouette candidate v1

Status: **OPEN — diagnostic production-candidate evidence only; wetness, geometry acceptance and art lock are not claimed.**

Each 1 920 × 1 080 contact sheet compares the existing flat `CylinderMesh` puddle proxy with a test-only irregular shallow `ArrayMesh`. The source view is on the left, the candidate view is on the right; the upper row is the fixed first-person overview and the lower row is a close diagnostic view. The material reference and source `roughness_value=0.90` are identical on both sides.

- Exact relief-collider contact: **PASS** (±0.005 m, source and candidate)
- Candidate mesh operations: **15/15** in-memory `Mesh` replacements; no colliders added
- Source scenes, shader, PainterlyMaterialLibrary, roughness, saves and narrative state: **unchanged**
- Diagnostic PNGs: **3/3**

The candidate intentionally has no bottom face or vertical cylindrical side wall. It remains a bounded silhouette experiment: visual assessment must still decide whether it reads as damp, avoids a plate/rim silhouette, survives traversal, and stays compatible with the Painterly Low-Poly 3D style bible.

## Verification

Run `./eng/capture-puddle-silhouette-candidate.sh`. The wrapper builds the C# project, runs the real Metal/Forward+ capture, pins benchmark-scene hashes, validates exact owner/contact samples, confirms material/collision isolation and decodes the output PNG dimensions/SHA-256 values.
