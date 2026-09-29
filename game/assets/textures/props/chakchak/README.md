# Kara-Urman chak-chak carton

This is a fictional everyday grocery package for Kara-Urman, not a real bakery
brand. The Tatar face reads «КАРА-УРМАН ИКМӘКХАНӘСЕ», «ЧӘК-ЧӘК», and «ТАТАР
ТӘМЕ». The front artwork has a genuine alpha cutout instead of a printed picture
of the pastry. The current GLB places a full-frame block of tightly packed,
overly wet, honey-glazed chak-chak behind the opening and lightly tinted PET
film. It fills the full window instead of forming a lone triangular mound. This
is an intentionally gross visual experiment; the earlier appetizing cutout is
kept alongside it for comparison.

The box is 120 × 150 × 72 mm. Its 3072 × 2048 RGBA atlas supplies the printed
front, back, side, top, and bottom panels. The prop is a static standalone GLB;
it has not been placed in a game scene or imported in Godot. The generated Tatar
copy and the procedural back-panel text still need native-speaker review.

## Cultural reference

The choice of chak-chak follows the [Tatarica encyclopedia entry](https://tatarica.org/ru/razdely/narody/tatary/tatarskaya-kuhnya/chak-chak),
which describes it as a Tatar celebratory sweet made from fried pieces of dough
bound with honey. The spelling «Чәк-чәк» also appears in the
[Atninsky district's intangible-heritage catalogue](https://atnya.tatarstan.ru/chk-chak-chk-chk.htm).
The plant border takes its tulip motif from documented Tatar ornament; see
[Tatarica's ornament entry](https://tatarica.org/ru/razdely/kultura/iskusstvo/izobrazitelnoe-iskusstvo-i-dpi/ornament).
The bakery name is invented for this prop.

## ImageGen source and prompts

The checked-in front-label source is
`assets/source/illustrations/karaurman_chakchak_front_window_v1.png` (1122 ×
1402, RGBA), generated and edited with built-in OpenAI ImageGen on 2026-09-28.
After the project canon was updated to Kara-Urman, the bakery line was reset in
the source with the project's PT Sans Bold font; the ImageGen ornament and
transparent window were retained.

Initial image prompt:

> Create one flat, print-ready front label artwork for a small folded paperboard carton of chak-chak from a fictional village bakery in Tatarstan. Portrait 4:5, straight-on two-dimensional artwork filling the canvas edge to edge; this is the printed face only, absolutely no package mockup, no perspective. Design a charming contemporary local bakery label on warm uncoated cream paper. Use a confident dark cobalt blue and deep cranberry red palette with small leaf-green accents. Frame it with restrained, hand-drawn Tatar floral tulip scroll ornament, neat and authentic-looking, no Arabic calligraphy and no religious symbols. At the top, set the exact Tatar name «КЫРЛАЙ ИКМӘКХАНӘСЕ» in a clear small line. Across the upper-middle, set the exact large product name «ЧӘК-ЧӘК» in beautiful bold custom Cyrillic display lettering. Below that put only the exact Tatar phrase «ТАТАР ТӘМЕ». In the central lower area make a delightful editorial food illustration of a small mound of golden fried dough pieces bound with glossy honey, shown on a plain white-blue ceramic saucer over a modest checked tea towel; a tiny honey dipper beside it, warm afternoon light, delicious but not photorealistic. Make it feel like a real everyday regional grocery product in winter 2026, handmade by a village bakery, not a tourist souvenir. Keep every required word spelled exactly in Tatar, clean and legible, and do not add any other words. No dates, weights, numbers, barcode, nutrition panel, price, seal, watermark, border mockup, package edges, brand imitation, mosque, crescent, costume, flag, or generic folk-dance imagery.

ImageGen edit prompt for the display window:

> Edit this exact flat packaging-front artwork. Keep the cream paper, Tatar floral tulip border, all placement and style, and preserve the three exact inscriptions as legibly as possible: «КЫРЛАЙ ИКМӘКХАНӘСЕ» at top, «ЧӘК-ЧӘК» large, «ТАТАР ТӘМЕ» below. Remove the entire illustrated food scene in the lower half: no drawn chak-chak, no plate, no towel, no honey dipper. In its place make one large centered rounded-rectangle clear display window cut into the printed cardboard, with a fine cream die-cut rim and only subtle pale icy-blue transparent-film edge glints. The area inside the window must be visually empty/transparent and show the same neutral transparency as the outer transparent background; do not depict, sketch, photograph, or print any food in the window. This is a flat print graphic for a real carton whose actual 3D contents will be placed behind the cutout later. Maintain portrait 4:5, straight-on, no package mockup, no perspective, no new copy, no extra words, no numbers, no date, no barcode, no watermark.

The exact historical prompt text above used the then-current place name «Кырлай».
After the project canon changed to «Кара-Урман», only that small label line was
replaced in the checked-in PNG using the project PT Sans Bold font; the AI
ornament, product title, and window pixels were retained.

The first contents image is
`assets/source/illustrations/karaurman_chakchak_contents_v1.png` (1254 × 1254,
RGBA), an appetizing cutout retained for comparison. The current experiment is
`assets/source/illustrations/karaurman_chakchak_contents_v02_experiment.png`
(1254 × 1254, RGBA): a close, edge-to-edge wall of compressed pieces, thick
sticky honey, wet gloss, and dark caramel patches. The current image is embedded
on one textured plane behind the PET window; neither food image is part of the
printed-cardboard atlas.

ImageGen prompt for the first contents image:

> Create an isolated transparent-background game asset: a small appetizing heap of real Tatar chak-chak, shaped from dozens of irregular little fried dough bites and short crinkled ribbons stuck together with amber honey. Rich baked golden brown, crisp blistered surfaces, naturally varied pieces and honey glints, painterly food illustration with softly realistic 3D shading, warm tactile detail. Center the rounded heap, nearly front view, fill most of a square canvas, clean alpha cutout. Looks delicious and handmade. No uniform rods or capsules, no low-poly shapes, no bowl, plate, table, backdrop, text, package, label, logo, sesame, nuts, berries, powdered sugar, watermark.

ImageGen edit prompt for the full-window gross experiment:

> Use this exact food image as starting reference. Change only the chakchak arrangement and surface appearance; no box. Make an experimental, intentionally gross version of the food, and completely change its silhouette and packing arrangement. Subject: a dense, rectangular block / wall of Tatar chak-chak packed tightly into a carton, viewed straight-on through a display window. The fried dough pieces fill the entire frame edge-to-edge, as if the whole box is solidly stuffed with chak-chak. Show hundreds of naturally varied tiny fried dough bits and short crinkled strands, tightly compressed and stuck into one continuous rectangular slab by thick amber honey. The top, sides, and bottom of the food block run right up to the image edges. No single mound, no triangle, no pile silhouette, no empty space, no plate. Style: close-up hyper-detailed food photograph, deliberately overdone and off-putting for a funny game-prop experiment; still unmistakably chak-chak and recognizable as fried dough. Make it overly wet, greasy and sticky, with too much dark amber syrup pooled between every piece, long tacky honey strings, soggy compressed dough, harsh gloss, uneven dark caramel patches, and some stale dull spots. Square image filled entirely by the rectangular cross-section, nearly front-on. No package, label, background, empty margin, smooth blob, identical capsules, plate, text, logo, props, insects, body parts, gore, or watermark.

## Rebuild

From the repository root run:

```sh
python3 tools/asset_generation/build_karaurman_chakchak_box.py
```

The script uses Pillow and NumPy to build the RGBA atlas, alpha-textured
contents plane, GLB, and perspective preview. Blender is not required. The
preview is a local illustrative render and is not an in-engine screenshot.
