# Painterly texture candidates v6 — technical QA

Date: 2026-08-14  
Status: **image/format PASS; Godot production-art OPEN**

This report covers the selected v6 earth and wood rework. The files are
non-destructive siblings; no v1 mapping, shader, save, collision, narrative or
runtime owner was changed.

## Image gate

Command:

```sh
./eng/verify-painterly-textures.sh \
  game/assets/textures/painterly/damp_earth_v6_albedo.png \
  game/assets/textures/painterly/weathered_wood_boards_v6_albedo.png
```

Result: **2/2 PASS**. The complete discovery gate is now **18/18 PASS** across
the v2–v6 candidate families. Both v6 files are 1 024 × 1 024, 8-bit RGB,
non-interlaced PNGs with embedded sRGB IEC61966-2.1. CRC/decode, clipping,
opposite-edge seam and saturation checks pass.

| Candidate | Seam mean V/H | Seam max V/H | Clip low/high | HSV sat mean/high | Existing scale | SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v6_albedo.png` | 0.0133 / 0.0124 | 0.1020 / 0.0667 | 0 / 0 | 0.3208 / 0 | earth 2.0×5.0 | `9ef03566bf89c800dc6317028ce1d9dfb6304ed8de70ea35710fa1b22c544e54` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v6_albedo.png` | 0.0063 / 0.0070 | 0.0353 / 0.0667 | 0 / 0 | 0.2951 / 0 | wood 3.2×3.2 | `9b1703f44f00a226ccfe79cace0a8231148125d81c3c91db6e9951ab3dfcd55e` |

## Previews

![Damp earth v6 albedo](/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v6_albedo.png)

![Weathered wood v6 albedo](/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v6_albedo.png)

Godot motion previews are retained in
`art/texture_candidate_motion_sweep_v6/`; the isolated A/B previews are in
`art/texture_candidate_ab_diagnostic_v6/`.

## Visual decision

- **Damp earth v6 — candidate:** broad cool-gray/taupe/olive masses remove the
  v5 painted pebbles and directional rut risk. It still needs in-engine checks
  for tile rhythm, wetness separation and whether the low-frequency contrast is
  strong enough at near distance.
- **Weathered wood v6 — candidate:** abstract wash fields avoid the v5 knot and
  continuous board-band problem. It may be too quiet on small props and still
  needs facade/fence/furniture/end-grain owner review.
- The first wood v6 ImageGen attempt was rejected before normalization because
  it remained photographic and had a visible knot; its source and hash are
  recorded in `texture_candidates_generation.md` only for provenance.

## Remaining gates

1. The isolated v5↔v6 Godot A/B capture is technically clean: 9 sheets,
   120/120 relief/contact samples, manifest
   `db978e19b72451d9548bafa323448fdf2d9d9ece64a4e2360f2f5e8cba751043`, under
   `art/texture_candidate_ab_diagnostic_v6/`. The earth relief-only delta is
   still subtle and remains an art-review question.
2. The dedicated v6 motion receipt is technically clean: three 1 920×1 080
   sheets, 27 cells per scene, under
   `art/texture_candidate_motion_sweep_v6/`; manifest SHA
   `e7d52a52fd8022c7b99b842c7d0a376b87886b88ff945e761f3748a8bab4cd5b`.
3. Run wood on facade, fence, furniture and end-grain under neutral daylight
   and warm house light; inspect triplanar orientation and shared-owner scale,
   then run the temporal first-person pass after the v6 substitution.
4. Keep the greybox geometry, fog/light, cultural presentation, Windows/M1
   performance and art-lock gates separate from this pixel-level PASS.

Until those checks pass, v6 is **production candidate only** and must not be
activated in `PainterlyMaterialLibrary` or treated as final art lock.
