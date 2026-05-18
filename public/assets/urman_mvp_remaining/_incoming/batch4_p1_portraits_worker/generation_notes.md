# Batch 4 P1 Portraits Worker Notes

Status: DONE draft, 2026-05-18.

Scope:

- Inventory #11: `char_timur_hazrat_portrait_*`
- Inventory #14: `char_naila_portrait_*`
- Inventory #15: `char_razilya_portrait_*`

Read before generation:

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/characters.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/asset_pack_first10/character_identity_sheets.md`

Generation approach:

- Used built-in image generation with flat `#00ff00` chroma-key background.
- Used existing URMAN portrait contact sheets as visual references for line weight, muted watercolor, bust framing and paper texture.
- Preserved raw key sheets under `_source/`.
- Converted contact sheets and cropped cells to alpha with `$CODEX_HOME/skills/.system/imagegen/scripts/remove_chroma_key.py`.
- Cropped variants from fixed grid order and normalized runtime portraits to 1024x1024 transparent PNG.
- Did not edit shared `asset_manifest.json`, shared animation metadata or shared editable-layer metadata.

Variant order:

- Timur hazrat contact sheet: top-left `calm`, top-right `direct`, bottom-left `concerned`, bottom-right `prayerful_restraint`.
- Naila contact sheet: left `professional`, middle `evasive`, right `alarmed`.
- Razilya contact sheet: left `gossip_friendly`, middle `cautious`, right `pressure`.

Tone constraints checked:

- Timur hazrat remains calm, direct and supportive; not sinister.
- Naila remains practical/professional medpunkt staff; not horror nurse or mad doctor.
- Razilya remains grounded selsmag/social gossip; not cartoon comic relief.
- No monster, gore, glowing eyes, Halloween props or full creature reveal.

Production note:

- A first Razilya generation included an explicit counter/table prop. It was rejected for transparent dialogue-portrait use and was not copied into this worker folder. The saved Razilya sheet is the character-only regeneration.

Alpha validation:

- All cropped portrait PNGs are `1024x1024` RGBA.
- All three transparent contact sheets are `1536x1024` RGBA.
- `contact_sheet.jpg` is a non-alpha batch preview, `1536x1024` RGB.
- Transparent corner alpha values are `[0, 0, 0, 0]` for every transparent PNG.
- High green-dominance visible-pixel check found `0` likely chroma-key fringe pixels across transparent PNGs.
