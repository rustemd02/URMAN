# Painterly texture candidates v5 — technical QA

Date: 2026-08-14  
Scope: `damp_earth_v5_albedo.png` and `weathered_wood_boards_v5_albedo.png` only  
Status: **technical PASS; production/art acceptance OPEN**

This is a non-destructive candidate audit. It does not activate either texture,
change `PainterlyMaterialLibrary`, alter the painterly shader, or close the art
lock. The v1 runtime mappings and all earlier candidate files remain untouched.

## Verification

Command:

```sh
./eng/verify-painterly-textures.sh \
  game/assets/textures/painterly/damp_earth_v5_albedo.png \
  game/assets/textures/painterly/weathered_wood_boards_v5_albedo.png
```

Result: **2/2 PASS**. The verifier requires 1 024 × 1 024, non-interlaced
8-bit RGB/RGBA PNG; opposite-edge seam mean ≤ 0.12 and maximum ≤ 0.40;
per-channel clipping ≤ 0.02; mean HSV saturation ≤ 0.68 and fraction above
0.90 saturation ≤ 0.08.

The decoder validates the PNG signature, chunk bounds, CRC for every parsed
chunk, `IHDR`, `IDAT`, `IEND`, zlib decompression and every reconstructed
scanline. Both candidates passed that integrity path. `file` and macOS `sips`
independently report non-interlaced 8-bit RGB PNGs with an embedded
`sRGB IEC61966-2.1` profile.

## Godot import and runtime regression receipt

After the standalone decoder pass, the project was rebuilt and imported through
Godot 4.7.1 .NET. `.tools/dotnet/dotnet build game/Urman.Game.csproj
--no-restore --disable-build-servers --verbosity minimal -nodeReuse:false -m:1`
completed with 0 warnings and 0 errors. The elevated `./eng/verify-godot.sh`
run completed the scene, audio, zone, narrative, UI, persistence, collision,
Chapter 1, full-game and first-person interaction smoke suites with no
`ERROR:`, `SCRIPT ERROR:`, ObjectDB or RID/resource-leak diagnostics. This is
an import/runtime regression receipt, not a decision to activate either v5
material in `PainterlyMaterialLibrary`.

## Results

| Candidate | Bytes | Size / mode / profile | PNG CRC and decode | Seam mean V/H | Seam max V/H | Clip low/high | HSV sat mean/high | Existing owner scale | SHA-256 |
|---|---:|---|---|---:|---:|---:|---:|---|---|
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/damp_earth_v5_albedo.png` | 1,148,952 | 1024×1024, 8-bit RGB, non-interlaced, sRGB IEC61966-2.1 | PASS | 0.0083 / 0.0088 | 0.0588 / 0.0510 | 0.0000 / 0.0000 | 0.2045 / 0.0000 | earth 2.0×5.0; 2048×5120 texels/world | `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108` |
| `/Users/unterlantas/Documents/GitHub/URMAN/game/assets/textures/painterly/weathered_wood_boards_v5_albedo.png` | 1,302,671 | 1024×1024, 8-bit RGB, non-interlaced, sRGB IEC61966-2.1 | PASS | 0.0147 / 0.0205 | 0.0784 / 0.0784 | 0.0000 / 0.0000 | 0.2516 / 0.0000 | wood 3.2×3.2; 3276.8×3276.8 texels/world | `3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70` |

## Technical and style risks

### Damp earth v5

- Opposite edges are numerically quiet and no directional painted rut is baked
  into the albedo, so it is a safer technical input for the authored 9×28 road
  relief than v4.
- Broad cool-gray/taupe patches and repeated painted pebbles can still become a
  recognizable 2.0×5.0 tile rhythm at first-person road distances. Pebbles may
  also duplicate authored relief and puddle dressing even without explicit
  ruts.
- The texture is matte albedo evidence only. Wet roughness/specular response,
  relief-only versus relief-plus-wetness comparison, and 20–30 m repetition
  remain OPEN in day street, Kara-Urman and Zirat captures.

### Weathered wood boards v5

- The muted gray-brown/ochre palette, restrained saturation and broad brush
  breakup fit the accepted Painterly Low-Poly direction at source-image level.
- Long horizontal brush bands and the sparse but recognizable knots may repeat
  or rotate implausibly under triplanar projection. They also do not encode a
  reliable board boundary, so the same shared wood owner may read differently
  on facade, fence, roof and furniture geometry.
- Orientation, end-grain handling, neutral versus warm interior light and
  20–30 m repetition remain OPEN. A future owner split may still be needed;
  this audit does not justify changing the shared runtime wood mapping.

## Remaining production gates

- Keep a clean Godot 4.7.1 import receipt alongside the real-driver A/B frames;
  the current smoke/import run is PASS, while a dedicated visual material
  acceptance pass remains OPEN.

The superseding v3↔v5 A/B receipt is preserved in the non-overwriting
`art/texture_candidate_ab_diagnostic_v5/` directory. Its manifest SHA-256 is
`41cc4702fbca356b7a8e2035251205fc576ebd6f822dec834c159a25f7fbc9e0`; the
capture remains `status=OPEN`, `technical_status=PASS`, 9 sheets and 120/120
contact samples.

The same candidates pass a separate 27-cell near/mid/far × FOV 65°/75°/90°
Metal/Forward+ sweep in
`art/texture_candidate_motion_sweep_v5/`: three 1 920×1 080 sheets, replacement
counts 1512/135/603 for day/house/Kara and manifest SHA
`003c0be712333fa0aaee5b1a596b119768a09cd0e2cd83b8b3376d014fea4053`. This is
technical spatial evidence only; it does not close art acceptance or temporal
comfort.
- Compare earth on authored road relief with wetness disabled/enabled in day
  street, Kara-Urman and Zirat; verify no painted/detail duplication or z-fight.
- Compare wood on facade, fence, furniture and end-grain under neutral daylight
  and warm house lighting; inspect triplanar orientation and shared-owner scale.
- Review near/mid/far and temporal first-person repetition at FOV 65°/75°/90°.
- Complete human art-direction, cultural level-art and release-hardware review.

Until those gates pass, both files are **production candidates only**. This
report is not a runtime activation decision and does not declare final art lock.
