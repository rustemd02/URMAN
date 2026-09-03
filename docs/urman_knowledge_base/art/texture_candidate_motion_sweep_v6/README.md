# Painterly texture candidate v6 — Godot motion sweep

Status: **technical PASS / production-art OPEN**. This is a non-destructive,
test-only presentation capture. The v6 files are not active in
`PainterlyMaterialLibrary`; runtime state, shader, saves, collisions and
narrative were not changed.

## Receipt

```sh
./eng/capture-texture-v6-motion-sweep.sh
```

Godot 4.7.1 .NET ran on a real Apple M4 Pro Metal/Forward+ driver. Each of the
three mandatory scenes contains the complete 27-cell grid: near/mid/far camera
distances × FOV 65°/75°/90°. Sheets are 1 920 × 1 080 (640 × 360 per cell).

| Scene | Replacement count | Sheet SHA-256 |
| --- | ---: | --- |
| Day street | 1 512 | `acf47e860275f3f9a2904d709c4a7c2bdb2965eca5063293c55d4658a25d0e0f` |
| House / old PC | 135 | `3ffca14fed6a9f479dd39be7be7528024978c837be5e59718d1988485bcab25c` |
| Kara-Urman edge | 603 | `790f591239c89934538bc1ed3d351a86b776f2242d8281df46ed313ff52a6478` |

Manifest SHA-256: `e7d52a52fd8022c7b99b842c7d0a376b87886b88ff945e761f3748a8bab4cd5b`.

Import and capture logs contain no Godot, ObjectDB, RID or resource-leak
diagnostics. This is spatial evidence only; it is not a temporal comfort,
Windows/M1 performance or final visual acceptance pass.

## A/B receipt

The isolated v5↔v6 diagnostic was captured without overwriting prior evidence:

```sh
URMAN_TEXTURE_AB_VERSIONS=v5,v6 \
URMAN_TEXTURE_AB_OUTPUT_DIR=docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v6 \
./eng/capture-texture-ab-diagnostic.sh
```

It returns `status=OPEN`, `technical_status=PASS`, 9 sheets and 120/120
relief/contact samples. Manifest SHA-256:
`db978e19b72451d9548bafa323448fdf2d9d9ece64a4e2360f2f5e8cba751043`.
The A/B confirms safe material isolation but not an art decision: the earth
relief-only delta remains subtle, while the wood difference is visible mainly
on the house/furniture crop and still needs owner/orientation review.

## Open gates

- Earth must be judged against authored relief with wetness disabled/enabled in
  day street, Kara-Urman and Zirat; no painted repetition or double relief may
  remain.
- Wood needs neutral and warm crops on facade, fence, furniture and end-grain;
  mesh board seams, triplanar orientation and shared-owner scale remain open.
- 20–30 m repetition, temporal first-person readability, greybox geometry,
  fog/light calibration, cultural review and release-hardware performance are
  separate gates.

Until these checks pass, v6 remains a production candidate only and no art lock
or runtime activation follows from this receipt.
