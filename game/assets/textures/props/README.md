# Date-free Chayan magazine prop

This asset is a fictional, undated issue of the Tatar satire and humor magazine
«Чаян», built as a standalone Act I prop candidate. The cover has no month,
year, issue number, or ISSN. The printed atlas is a single 4096 × 4096 PNG:

- the exterior wrap is in the upper-left: back cover, spine, front cover;
- two inside-cover panels and sixteen page faces are laid out in the remaining
  UV islands;
- the final small cream swatch is used for the paper edges.

The accompanying GLB is 155 × 215 mm when closed. It contains eight
double-sided, independently hinged page leaves. Animation clips are
**OpenCover**, **CloseCover**, **TurnPage_01**–**TurnPage_08**,
**ReturnPage_01**–**ReturnPage_08**, and **OpenAndFlipThrough**.

The full front page is a single ImageGen image: red «ЧАЯН» masthead, scorpion
mascot, blue journal band, Tatar slogan, and the winter cartoon are all part of
the generated image. The atlas builder places that page into the front-cover UV
island without typesetting or compositing over it. It was generated with the
original URMAN winter cartoon as a visual reference; no raster from a published
cover was copied. The masthead is an original generated treatment informed by
the magazine's recognizable identity.

The reference pass used the [TATMEDIA product page for a Tatar-language
issue](https://shop.tatmedia.ru/ru/shop/iumoristiceskii-zurnal-na-tatarskom-iazyke-caian-3-elektronnaia-versiia24_1725366068),
[Tatarica's magazine entry](https://tatarica.org/ru/razdely/sredstva-massovoj-informacii/periodicheskie-izdaniya/chayan),
and the [Tatar-Inform centenary article about the masthead's history](https://www.tatar-inform.ru/news/respublika-scitaet-nas-nacionalnym-dostoyaniem-100-letnii-yubilei-prazdnuet-cayan-5894185).
The cover slogan and recognizable section names were checked against the
published cover/product copy; the new inside-page filler is decorative and
has not had a native-speaker review.

## ImageGen sources

The original cartoon is `assets/source/illustrations/chayan_winter_cover_art_v1.png`.
The built-in OpenAI ImageGen tool produced it on 2026-09-28 with no input image.
Its prompt:

> Create one portrait editorial cartoon illustration asset for a printed Tatar-language satire and humor magazine. Aspect ratio 3:4, flat artwork edge to edge, intended to be cropped into the lower two-thirds of a magazine front cover. Scene: a winter village courtyard in Tatarstan, rendered as a witty humane newspaper cartoon. A bundled middle-aged village man has carefully shoveled a narrow path to his wooden gate, but the path now leads to a huge snowdrift that has buried the gate; a tiny dignified hen stands on the snowbank holding the man's lost mitten like a traffic inspector, while the man looks at it with resigned amusement. Ordinary contemporary quilted coat, wool hat, felt boots, simple timber house and fence, one birch, packed blue-white snow. Strong readable silhouettes, expressive faces, crisp hand-inked contours with lively colored-pencil and gouache fills, editorial caricature, artful print grain, charming and clever rather than mean. Vivid but restrained magazine-print palette: raspberry red, cobalt blue, warm cream, mint, muted lavender snow shadows. No text, no lettering, no numbers, no logo, no speech bubbles, no border, no page mockup, no visible date, no calendar, no labels, no watermark, no photorealism, no folklore creature, no costume stereotype, no religious objects, no open landscape or visible forest edge.

The complete cover is `assets/source/illustrations/chayan_full_cover_v1.png`
(1086 × 1448). ImageGen created it on 2026-09-28 using the original cartoon as
a visual reference. The generation brief:

> Use the attached generated winter cartoon as the main illustration reference. Create a complete, flat, print-ready front cover for a fictional undated issue of the Tatar satire magazine «Чаян», portrait 3:4, full bleed and facing straight toward the viewer, not a magazine mockup. Preserve the funny winter village scene: a bundled man shovels a path toward a gate buried under a huge snowdrift, and a dignified hen holds his lost mitten. Make a vivid, polished editorial caricature with crisp ink contours and lively painted color. Across the upper lavender field, put the exact Tatar slogan «Көлү – сихәт, көлеп яшәү мәслихәт!» in dark blue, then a huge custom red masthead «ЧАЯН» with a white outline and a friendly red scorpion mascot at its right. Directly below, place a cobalt blue band with the exact white text «САТИРА ҺӘМ ЮМОР ЖУРНАЛЫ». Keep all required text spelled exactly and legible; include no other text. Do not include a date, month, year, issue number, ISSN, price, barcode, watermark, border, or perspective mockup.

## Build

Run python3 tools/asset_generation/build_chayan_magazine.py from the repository
root. The script uses the two checked-in ImageGen images and project PT Sans
fonts to rebuild the atlas, animated GLB, and perspective preview. The generated
front cover is kept intact and only resized to the UV panel dimensions. Blender
is not required; the GLB is a glTF 2.0 model that can be opened in Blender and
imported by Godot.

game/assets/models/props/chayan_magazine_preview_v1.png is a perspective
mockup composed from the front-cover UV island. It is a preview image, not an
engine screenshot. The model is not yet placed in an Act I scene or assigned a
narrative interaction.
