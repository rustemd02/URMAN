# Character texture variants

## mansur_age_v1_albedo.png

- Purpose: an age-color variant for Mansur's head only; the shared male atlas and eye/normal textures remain unchanged.
- Source: `game/assets/generated/urman_character_kit_T_Superhero_Male_Dark.png`, derived from Quaternius Universal Base Characters Standard (CC0). Upstream provenance is recorded in `assets/source/blender/characters/README.md`.
- Tool: built-in ImageGen, 2026-09-12, precise-object-edit. Generated source is 1254 × 1254; normalized to 1024 × 1024 with the existing `sips -z 1024 1024` workflow. UVs use normalized coordinates.
- Generated-source SHA-256: `65e0af3c3237daf5d83ecde8ed5817f2d9dd06d1bdb86c13004098d7cb733b19`.
- Runtime SHA-256: `1a650889ed159b6cc39ca9dd5f9f24b595d61916aa8893e05d9bc53d74668608`.
- Status: native face/UV review passed in `mansur_age_v1_frames` (close view and full room). Other character face maps remain unchanged. Color wrinkles do not replace age geometry; overall character art lock remains open.

Prompt:

> Use case: precise-object-edit. Asset type: 1024x1024 RGB UV texture atlas for a stylized 3D game character, original CC0 Quaternius male albedo. Edit target: attached UV atlas. Change ONLY the facial skin island in the upper-left area (roughly x0..390 y35..365). Make this same face read as a kindly weathered man about 70 years old: subtle forehead creases, eye corner wrinkles, soft under-eye folds, nasolabial folds, slightly less saturated cheeks. Hand-painted stylized game albedo, broad soft shapes compatible with Zelda BOTW/TOTK-inspired rendering, no photoreal pores or deep black lines. CRITICAL: preserve exact 1024x1024 canvas, every UV island boundary, eye socket positions, nose and mouth position, facial proportions, colors outside the face region, and all other atlas content. Do not draw eyeballs (separate material), hair or beard. No repacking, no portrait, no crop, no labels, no lighting effects. Return the entire edited atlas in the exact original layout.

The corresponding female-atlas generation was rejected by the image tool's safety filter; no female variant was produced or included.
