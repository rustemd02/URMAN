# Painterly texture provenance

Status: project-bound material sources generated with the built-in ImageGen tool on 2026-08-10, then resized to 1 024 × 1 024 px for the environment texture budget. These images are albedo inputs, not screenshots or style-acceptance evidence. The `_v2_albedo.png` siblings below are non-destructive production candidates generated on 2026-08-11; the focused `_v3_albedo.png` siblings are an additional 2026-08-13 comparison pass; the two `_v4_albedo.png` siblings are a focused 2026-08-14 earth/wood comparison; the two `_v5_albedo.png` siblings are a 2026-08-14 relief-aware rework. The four original surface mappings remain v1; the new stone/fabric surfaces have explicit v2-only presentation owners in the benchmark scenes, pending art-lock review.

| File | Intended surface | SHA-256 |
|---|---|---|
| `weathered_wood_boards_albedo.png` | wood, fences, roofs and furniture | `178ede16b37d566cc41d2e78c4d95822d97f7ae9de9e99633b7968b2bba08fc8` |
| `damp_earth_albedo.png` | dirt roads and forest paths | `4f53dc3db51b6653d6df601dda671937e654a884c9e8eab1d563b67b837004fe` |
| `aged_plaster_albedo.png` | village house and FAP plaster | `9f12199dac9c9b05a0d77836d15ecb2aeb5d46a4ea4bd38e4ca4907dfc62ea94` |
| `pine_foliage_albedo.png` | faceted conifer crowns | `1bf9122c044a54765de61990d1fafdd39bbee38f014b9a936c0db6dc0e491f39` |

Shared prompt constraints: seamless square albedo, flat orthographic material swatch, broad painterly brushwork, muted natural palette, no directional lighting, no text, no watermark, no horror symbols and no photorealistic microdetail. Individual prompts differ by the intended surface description and are retained in the migration evidence for this production pass.

License/provenance: project-generated through OpenAI ImageGen for «УРМАН»; no third-party source image was used.

The focused `_v6_albedo.png` earth/wood siblings are also project-generated,
non-destructive production candidates; their exact prompts, provenance and
Godot receipts are recorded below and in the linked knowledge-base reports.

## Production candidates v2 (not globally active at runtime)

The six candidates were generated one-per-material with the built-in ImageGen
tool and normalized from the 1 254 px source to 1 024 × 1 024 RGB PNG. Exact
prompts, source paths, selection rationale and hashes are recorded in
[`texture_candidates_generation.md`](../../../../docs/urman_knowledge_base/art/texture_candidates_generation.md).
Deterministic image metrics and the Godot Forward+/Metal scene captures are in
[`texture_candidates_technical.md`](../../../../docs/urman_knowledge_base/art/texture_candidates_technical.md)
and [`texture_candidates_qa.md`](../../../../docs/urman_knowledge_base/art/texture_candidates_qa.md).

| File | Intended surface | SHA-256 | Current owner | Status |
|---|---|---|---|---|
| `weathered_wood_boards_v2_albedo.png` | wood, fences, roofs and furniture | `03499969fcb1cb172a3d6b4b11bb710c65daa159e4c9cb24de11f4a1c3c1c9bf` | `PainterlyMaterialLibrary/wood` (preview-only; v1 remains active) | selected production candidate; art-lock-pending |
| `aged_plaster_v2_albedo.png` | village house and FAP plaster | `a88975b10aa0d7070f868408366309c2b4100fd5eb1defd9fb08fe3281083416` | `PainterlyMaterialLibrary/plaster` (preview-only; v1 remains active) | selected production candidate; art-lock-pending |
| `damp_earth_v2_albedo.png` | dirt roads and forest paths | `02f356c591d52fdde31992f6064fbb3b10ec6928e856bfbb0315a687c24aeb56` | `PainterlyMaterialLibrary/earth` (preview-only; v1 remains active) | selected production candidate; art-lock-pending |
| `pine_foliage_v2_albedo.png` | faceted conifer crowns | `3838784bdcf4d3fd31f6bf6bff93ef5906452cebe3df1d6b86075b42888e8ff6` | `PainterlyMaterialLibrary/foliage` (preview-only; v1 remains active) | selected production candidate; art-lock-pending |
| `mossy_stone_v2_albedo.png` | boundary walls, old foundations and path stones | `38b0e69f32a4e3e859921229cab0ea7bf12426dac9b8e150683b7607b5277b15` | `PainterlyMaterialLibrary/stone`, 2.4×2.4; v2-only presentation owner | selected candidate; mapped, art-lock-pending |
| `old_fabric_v2_albedo.png` | household cloth, curtains and rugs | `f1c646785cddb876510f81be2400c6c2da01764b1fceb5d2bb8e92f0186d9132` | `PainterlyMaterialLibrary/fabric`, 3.0×3.0; v2-only presentation owner | selected candidate; mapped, art-lock-pending |

The candidates are deliberately kept beside, not over, the working v1 files.
The test-only `TextureCandidateFrameCapture` still clones presentation
materials for comparison; the source benchmark scenes additionally use the
new `stone`/`fabric` owners on non-interactive well/forest-marker/rug anchors.
No candidate is globally enabled across gameplay scenes, no candidate closes
the art lock, and no gameplay state, collision owner or save contract is
changed.

## Production candidates v3 (focused, not active at runtime)

The v3 siblings below are a focused ImageGen pass for the highest-risk materials
identified by the art review. They use the existing preview scales in a separate
test-only Godot harness; the v1 runtime textures and v2 candidate owners remain
unchanged.

| File | Intended surface | SHA-256 | Status |
|---|---|---|---|
| `weathered_wood_boards_v3_albedo.png` | wood, fences, roofs and furniture | `65372413ff490da9dce831fd5f4022d6f804265b42dd9895ad4b13a6fa196093` | image gate + v3 scene capture PASS; art-lock-pending |
| `aged_plaster_v3_albedo.png` | village house and FAP plaster | `bb5f6c13d1947ef5616e9dac49ff37b41e6ff323901cc0b44b783212ac21454e` | image gate + v3 scene capture PASS; art-lock-pending |
| `damp_earth_v3_albedo.png` | dirt roads and forest paths | `cd031be6ff3fbb2750a05abe6b667d23bba2ad3705b6f5a14755d21a33b77b20` | image gate + v3 scene capture PASS; art-lock-pending |
| `pine_foliage_v3_albedo.png` | faceted conifer crowns | `05073ea5b36b6ad0bdd25a19c4ac38360bf6681e09584449447fccabece36830` | image gate + Kara motion capture PASS; art-lock-pending |
| `mossy_stone_v3_albedo.png` | boundary walls, old foundations and path stones | `4444dc8585aada9dfd85601b8b32c96b0af3aa385d1d96e8ae88d24dfed66024` | image gate + test-only anchor motion capture PASS; art-lock-pending |
| `old_fabric_v3_albedo.png` | household cloth, curtains and rugs | `c19f22b7b5f2e9b4f8f5460e407c964d0ca9395022b4f2ef93d38722a0e3da82` | image gate + test-only anchor motion capture PASS; art-lock-pending |

Exact prompts, ImageGen source paths, source/final hashes and selection notes
are in [`texture_candidates_generation.md`](../../../../docs/urman_knowledge_base/art/texture_candidates_generation.md).
The three mandatory-scene captures and the material swatch are in
[`art/texture_candidate_frames/v3/`](../../../../docs/urman_knowledge_base/art/texture_candidate_frames/v3/).
The six-material near/mid/far/FOV sweep is in
[`art/texture_candidate_motion_sweep/`](../../../../docs/urman_knowledge_base/art/texture_candidate_motion_sweep/).
Numeric seam safety is not a substitute for near/mid/far traversal review,
20–30 m repetition review, geometry relief, fog/light calibration or cultural
approval.

## Production candidates v4 (focused, not active at runtime)

V4 narrows the remaining texture question to the two surfaces with the largest
first-person impact: damp earth on the road/path and weathered wood on village
boards. The candidates are test-only substitutions; v1 runtime mappings and all
v2/v3 files remain unchanged.

| File | Intended surface | SHA-256 | Status |
|---|---|---|---|
| `weathered_wood_boards_v4_albedo.png` | wood, fences, roofs and furniture | `5203ad6e37e5a3328b8aef2dc8c1aa00a5c3e83896be7286663c8e640c7af25a` | image + Godot still/motion PASS; **HOLD/REWORK**; not active |
| `damp_earth_v4_albedo.png` | dirt roads and forest paths | `f1ed53e8b40f94fc45a2e8e43816e5c77e1717da0be09a60dcc68045da1bad31` | image + Godot still/motion PASS; **HOLD/REWORK**; not active |

The exact ImageGen source hashes, prompts and selection notes are in
`docs/urman_knowledge_base/art/texture_candidates_generation.md`. Still
captures are under `art/texture_candidate_frames/v4/`; the 27-cell
near/mid/far/FOV sweep is under `art/texture_candidate_motion_sweep_v4/`.
Independent review found earth/road-relief duplication and wood triplanar
striping/shared-owner risk. V4 is not globally enabled and does not close the
art lock; v3 remains the safer test-only comparison baseline.

## Production candidates v5 (focused rework, not active at runtime)

V5 removes the most dangerous v4 cues: painted continuous road ruts/volume-like
stones from earth and long dark seams/concentric knots from wood. Both files are
still test-only and keep the existing v1 owners active.

| File | Intended surface | SHA-256 | Status |
|---|---|---|---|
| `damp_earth_v5_albedo.png` | dirt roads and forest paths | `6e15102bdbd755e5bbfb737f20946cecd0b1f1fab30b1e2b85a20a1cfb900108` | image gate PASS; Godot v3↔v5 A/B technical PASS; production OPEN |
| `weathered_wood_boards_v5_albedo.png` | wood, fences, roofs and furniture | `3a9c69862b2ad140bf8a9a26b54fcbcdc144aedfa43703aa1d4c11d964b9cf70` | image gate PASS; Godot v3↔v5 A/B technical PASS; production OPEN |

Prompts, source paths and exact normalization are recorded in
`docs/urman_knowledge_base/art/texture_candidates_generation.md`. The clean
9-sheet capture is under
`docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v4/`.
Neither candidate is a runtime replacement or art-lock decision.

## Production candidates v6 (focused rework, not active at runtime)

V6 keeps the two high-impact surfaces deliberately quiet: earth is broad and
non-directional so authored road relief owns ruts, while wood is an abstract
wash so mesh geometry owns boards, seams and end-grain.

| File | Intended surface | SHA-256 | Status |
|---|---|---|---|
| `damp_earth_v6_albedo.png` | dirt roads and forest paths | `9ef03566bf89c800dc6317028ce1d9dfb6304ed8de70ea35710fa1b22c544e54` | image gate PASS; v5↔v6 A/B and motion technical PASS; production OPEN |
| `weathered_wood_boards_v6_albedo.png` | wood, fences, roofs and furniture | `9b1703f44f00a226ccfe79cace0a8231148125d81c3c91db6e9951ab3dfcd55e` | image gate PASS; v5↔v6 A/B and motion technical PASS; production OPEN |

The v6 prompts, ImageGen source paths and source/final hashes are recorded in
`docs/urman_knowledge_base/art/texture_candidates_generation.md`. The isolated
9-sheet A/B receipt is under `art/texture_candidate_ab_diagnostic_v6/`; the
27-cell per-scene motion receipt is under
`art/texture_candidate_motion_sweep_v6/`. Earth relief-only separation is
subtle and wood still needs owner/orientation review. No v6 file is globally
enabled and no art lock is declared.

## New surface families, 2026-09-10

Generated one file per prompt with the built-in OpenAI ImageGen tool on 2026-09-10, with no source images. Each result was normalized to 1 024 × 1 024, 8-bit RGB PNG. Edge pairs above a 10/255 mean absolute-difference threshold received a local 64 px mirrored cosine blend; the two final leaf PNGs also received a local sky-gap cleanup after ImageGen. All 2×2 receipts, metrics and the 19-file overview are under `evidence/act1_repo_baseline/textures_new_families/`. Runtime selections below are candidates only; final family choice and ornament appropriateness remain human art/cultural gates.

| File | Intended surface | SHA-256 | Status |
|---|---|---|---|
| `grass_verge_v1_albedo.png` | overgrown village verges and meadow ground | `fc59feef19ceec6d94810ce0036ae293680aad9d9e27d6c01b2e78e607231b8e` | candidate, art-lock-pending |
| `grass_verge_v2_albedo.png` | trodden yard grass | `6b247106eddbeb4541700c382bf5ab56f9ad409ca6b0ed9e4ecf5e18564d6ab1` | candidate, art-lock-pending |
| `roof_slate_v1_albedo.png` | weathered slate roofs | `ea354f70dc10e1c209010eda2afc022810f51661d2aa64bab27ded2b845c1708` | candidate, art-lock-pending |
| `roof_metal_v2_albedo.png` | FAP and village metal roofs | `b0b70b09c0bdbf7e1567de7c2185f8e21d8489bcf7932c21a7087107a81f48ae` | candidate, art-lock-pending |
| `roof_shingle_v3_albedo.png` | weathered shingle roofs | `6726fc18ad72f12ccb9866f4cb83a72cd505ebd9c61367de6479bef46d4563d4` | candidate, art-lock-pending |
| `bark_birch_v1_albedo.png` | young/middle-aged birch trunks | `9697a055e638625cd68b9028342abea97f46a058ffb1136b3cae99e62e8a0ee9` | candidate, art-lock-pending |
| `bark_birch_v2_albedo.png` | older birch trunks | `2e96da7bf3bee4d749b7bf17681898715a5e333587f0d681f15180c8c22df581` | candidate, art-lock-pending |
| `bark_pine_v1_albedo.png` | pine and spruce trunks | `5cc0577732ba886cbcfb727c5d30bca822b996cf1875d6d88cfad47c38f951e4` | candidate, art-lock-pending |
| `leaf_birch_v1_albedo.png` | summer birch crowns | `6b469475e3280ed08a584a8bcca06ec23255607cadf69321621b275e6485c2cb` | candidate, art-lock-pending |
| `leaf_birch_v2_albedo.png` | early-autumn birch crowns | `77d16e04ef2d04394f7b2b5d972711628459db8b7f95faa7827934cad37c9332` | candidate, art-lock-pending |
| `log_wall_v1_albedo.png` | warm old-house log walls | `7ddca7416c3b95549b60eab0b50f81d5dcb1c2f724b49bab7de1e1b60cf47e17` | runtime wall semantic; native-reviewed 2026-09-12, art lock pending |
| `log_wall_v2_albedo.png` | cooler smoke-aged log walls | `0e51db435030ae30a28e4e26fc106d60e5324926f7a1735adff53314b8d71cc7` | candidate, art-lock-pending |
| `wallpaper_old_v1_albedo.png` | faded floral house wallpaper | `f80e97c330e2a7e8237a8f7fed860c233da3a2f6678a8ad882acb89a93806a3c` | runtime wall semantic; native-reviewed 2026-09-12, art lock pending |
| `wallpaper_old_v2_albedo.png` | striped house wallpaper | `451123ec6bf67a3ce5af46cc4cbf34f8136f161dad29af636a19163fafd3e970` | candidate, art-lock-pending |
| `wall_institution_v1_albedo.png` | FAP institutional walls | `4139f15bc78e4713a85398e697a3418c9e0b910999fa6b800f3318ae8a95f626` | runtime wall semantic; native-reviewed 2026-09-12, art lock pending |
| `ornament_trim_v1_albedo.png` | restrained Tatar border frieze | `c13f6b28a35cae781799729fd0ac6d8c5e72295d036fcac26e7e3a6a9caa2a8a` | candidate, art-lock-pending |
| `carpet_palas_v1_albedo.png` | traditional flat-woven Tatar palas | `d9d48b174b6c8e02bf618cae40ae7248b9562bbb42d7ce7ef471c06b0a55721a` | runtime carpet semantic; native-reviewed 2026-09-12, cultural/art lock pending |
| `fabric_chit_v1_albedo.png` | old Tatar chintz household fabric | `b70fde9721eb785afb00acd912af0ca903ebfe8f2b9f42d184807791a038c91a` | candidate, art-lock-pending |
| `wood_carved_gate_v1_albedo.png` | carved gate and hero-house window trim | `3620ee923093c1677aac99cf8d4f09753917e326b1e7d4d7e20421444c480cae` | candidate, art-lock-pending |

### Exact ImageGen prompts

#### `grass_verge_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: overgrown wet village meadow grass seen from directly above. Soft painterly
tufts of grass in mixed yellow-green and dull olive tones, subtle patches of clover,
thin dry straw blades, small bare-earth gaps, gentle large-scale variation between
lighter and darker patches so a big field does not look uniform. Painterly brush
strokes visible in tuft clumps. Tatar village roadside, early autumn, muted, slightly damp.
```

#### `grass_verge_v2_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: short trodden yard grass seen from directly above, painterly style. Denser,
darker green with worn paths of bare soil where people walk, scattered small plantain
leaves and tiny yellow flowers, compact moist ground showing through. Calm, even,
hand-painted texture with visible brush clusters. Muted palette, no bright saturated green.
```

#### `roof_slate_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: weathered grey asbestos-slate roof sheets seen from directly above, painterly
stylization. Visible sheet divisions into large rectangles, soft moss and lichen patches
in muted green-grey, subtle darker damp streaks running down the slope direction,
a few chipped corners. Calm grey palette with slight warm variation between sheets.
```

#### `roof_metal_v2_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: old faded blue-grey painted corrugated metal roof sheet seen from directly
above, painterly stylization. Gentle corrugation stripes, chalky faded paint with
matte rust streaks near fasteners and edges, soft lichen dots. Muted, not industrial,
not shiny, village house feel.
```

#### `roof_shingle_v3_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: weathered wooden shingle roof seen from directly above, painterly
stylization. Rows of hand-split grey-brown shingles with varied tone per shingle,
slight warping, moss in the gaps, muted wet sheen painted as value, not gloss.
```

#### `bark_birch_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: birch tree bark seen from the side, painterly stylization. Creamy white
parchment-like bark with characteristic dark horizontal dashes and small dark
cracks, subtle grey-black rough patches near the base, soft peeling curls.
Vertical orientation of features, seamless horizontal wrap.
```

#### `bark_birch_v2_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Same as bark_birch_v1 but older tree: more grey-black patches and deeper cracks,
less clean white, still unmistakably birch. Painterly, muted.
```

#### `bark_pine_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: dark pine trunk bark seen from the side, painterly stylization. Deep
grey-brown vertical fissures forming irregular plates, muted moss dust in recesses.
Coarse but hand-painted, not photographic. Seamless horizontal wrap.
```

#### `leaf_birch_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: summer birch crown foliage seen from directly above, painterly
stylization. Clusters of small rounded bright green leaves with visible
hand-painted leaf shapes, lighter yellow-green highlights on clump tops painted
as value, airy gaps showing sky, slight wind-scattered looseness. Muted natural
green, not saturated, no individual photoreal leaves.
```

#### `leaf_birch_v2_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Same as leaf_birch_v1 but early autumn: mixed green and warm yellow-green clumps,
 a few ochre leaves, sparser gaps, painterly and muted, overcast feel.

### Приоритет 2 — интерьеры
```

#### `log_wall_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: interior wall of an old Russian village log house (сруб), painterly
 stylization. Horizontal rounded logs with visible wood grain, warm grey-honey tone,
 subtle dark gaps between logs, occasional checks, aged but clean and homely.
 Flat scan-like view of the wall surface.
```

#### `log_wall_v2_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Same as log_wall_v1 but cooler and more aged: grey-brown tone, more checks,
 faint smoke darkening above where a stove would be. Painterly, muted.
```

#### `wallpaper_old_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: faded old wallpaper of a village house room, painterly stylization.
 Small restrained floral pattern in dusty rose and faded beige on cream, repeated
 evenly, gently sun-faded patches, subtle yellowing near the top edge. Soviet-era
 modest, not decorative overload, no shine.
```

#### `wallpaper_old_v2_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: faded old wallpaper with vertical stripes and tiny flower sprigs,
 sage-green and cream, painterly stylization. Slightly peeling tone variation,
 muted, village room feel, no shine, no photorealism.
```

#### `wall_institution_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: worn institutional painted wall of a small rural medical clinic (ФАП),
 painterly stylization. Faded pale mint-green oil paint with subtle uneven wear,
 faint darker scuffs at the lower half, calm and clean but aged. Flat, shadowless.

### Приоритет 3 — татарская этника (дозированно; использовать точечно, НЕ массово)
```

#### `ornament_trim_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: a horizontal decorative border frieze in traditional Tatar floral
 ornament style, painterly stylization. Muted ochre, terracotta and sage-green
 curving plant motifs on aged cream background, hand-painted with slightly uneven
 brushwork, weathered as if painted on wood decades ago. Two border rows filling
 the square tile, restrained and calm, NOT bright, NOT wedding-grade rich,
 seamless horizontal wrap.
```

#### `carpet_palas_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: traditional Tatar woven palas rug (flat-woven village carpet), painterly
 stylization. Horizontal stripes in muted madder red, ochre, cream and dark brown
 with simple geometric steps and small diamond motifs woven into the bands,
 visible weave texture painted broadly, slightly faded and worn. Flat view,
 hand-painted, not ornate pile carpet, restrained palette.
```

#### `fabric_chit_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: old Tatar chintz cotton fabric with a small repeating floral pattern
 (читә/кытай style), painterly stylization. Tiny muted red and ochre flowers with
 fine dark outline dots on faded cream, gently worn and sun-faded patches.
 Curtain/tablecloth weight, matte, calm, evenly repeating, no shine.
```

#### `wood_carved_gate_v1_albedo.png`

```text
Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- muted natural palette, cohesive with an overcast Russian village setting;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

Material: painted carved wood of old Tatar village gates and window surrounds
 (резные наличники), painterly stylization. Flat-carved floral grooves painted in
 muted faded blue-green and cream over aged wood grain, softly weathered paint
 with small chips showing bare wood. Hand-painted, respectful, not glossy,
 not folkloric overload, seamless in both directions.
```
