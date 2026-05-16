# URMAN Asset Pack Style Bible

Status: Locked for first production test batch, 2026-05-16.

## Core Rule

Ordinary first, wrongness later. Every asset must first read as a real quiet Tatar village scene, person, document or object. Horror enters through small mismatches: stillness, withheld gaze, incorrect light, too-empty roads, branch shapes that almost mean something.

## Line Style

- Hand-drawn black ink line, slightly uneven and alive.
- Readable silhouettes, never sterile vector outlines.
- Faces, hands, interactables and document edges get the firmest line.
- Background detail is selective: 2-3 grounded details beat decorative overload.
- No hyperrealism, glossy 3D polish, superhero anatomy, pixel art or comic-book action posing.

## Palette

- Paper: warm grey, faded cream, light ochre.
- Wood: grey-brown, old varnish, dark plank, soot-softened corners.
- Village nature: bog green, dry grass, muted pine, wet road clay.
- Home warmth: weak amber light, never bright orange.
- Evening and forest: cold blue-green, black-green, desaturated teal.
- Old PC: faded grey plastic, muted turquoise UI, dusty CRT glow.
- Accent color must be restrained and character-specific, not decorative folklore pasted everywhere.

## Paper Texture

- Visible watercolor wash, paper grain and slight pigment blooms across all final raster assets.
- Texture should unify portraits, route screens, documents and UI.
- Documents may look scanned or copied, but important text stays editable outside the bitmap.

## Lighting Rules

- Natural domestic light first: window light, weak bulb, CRT glow, cloudy sky.
- Pressure lighting is a small rule break, not a stage spotlight.
- Shadows may be slightly too long or misaligned at creepiness 2-3.
- Avoid theatrical horror contrast unless the scene is the Kara-Urman boundary.

## Controlled Animation Rules

- Base assets remain static PNGs unless a scene explicitly needs video.
- Every location / route / UI asset should reserve clean animation-safe regions for overlay layers: window light, candle flame, CRT glow, birds, dust, grass, branches, water, document highlights or pressure shadows.
- Animation should be slow, sparse and low-amplitude. Motion is a mood layer, not spectacle.
- Use transparent overlays, masks, shader passes or engine transforms over baked effects.
- Important text must remain editable and readable above any animation layer.
- Pressure animation requires a narrative or gameplay flag; it must not run constantly.
- Full creature motion, glowing eyes, jump-scare movement and heavy glitch are forbidden in MVP.

## Creepiness Scale

| Level | Use | Visual Rule |
|---|---|---|
| 0 | Home, neutral portraits, ordinary route screens | calm village realism, soft paper texture |
| 1 | Early unease | wrong pause, empty space, slightly odd window light |
| 2 | Investigation pressure | deeper shadows, watched windows, social withholding |
| 3 | Kara-Urman boundary, dangerous clue | forest almost forms a figure, no full creature reveal |

MVP assets must stay in levels 0-3.

## Forbidden

- Obvious monsters, glowing eyes, skulls, blood, gore, demons or jump-scare framing.
- Halloween props, neon, cyberpunk UI, pure noir, generic Slavic horror village.
- Full creature reveal in MVP.
- Turning татарский cultural detail into costume decoration.
- Making Тимур хәзрәт, the mosque or Islamic framing visually sinister.
- Redesigning the same character between variants.

## Prompt Anchor

Use this anchor in every image prompt:

Ink-wash storybook game asset for URMAN, hand-drawn black ink line, muted watercolor wash, visible paper grain, grounded contemporary Tatar village details, ordinary first with subtle wrongness, no hyperrealism, no glossy 3D render, no pixel art, no neon, no gore, no obvious monster, no full creature reveal.

Add for locations, route screens, documents and UI:

Leave clean animation-safe regions for later overlay layers: light pulse, small environmental motion and pressure overlay. Do not bake the animated effect into the base image. Keep important text blank and editable.
