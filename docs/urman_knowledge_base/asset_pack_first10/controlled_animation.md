# Controlled Animation Spec

Status: Added after first asset batch, 2026-05-16.

## Principle

УРМАНу нужна не «живая картинка ради живости», а контролируемая анимированность. Static-node screens remain painted backgrounds; motion is added through small overlay layers, masks and engine-side parameters.

The asset itself should reserve clean places for animation:

- a window where warm light can pulse;
- sky space where small birds can cross far away;
- a candle / bulb / CRT glow layer;
- foreground branches or grass for tiny sway;
- document highlight / scan flicker;
- pressure shadow overlay that can fade in without revealing a creature.

Do not bake normal ambience into video files unless a scene specifically requires it. Prefer static PNG base + transparent overlay sprites / masks / shader parameters.

## Motion Rules

| Motion Type | Allowed Use | Amplitude |
|---|---|---|
| Light pulse | windows, candle, CRT, weak bulb | very low; slow 3-8 s cycle |
| Environmental drift | birds, dust motes, curtain, grass, branches | small; mostly background/foreground |
| Investigation feedback | clue highlight, document focus, UI cursor | readable, not flashy |
| Pressure overlay | shadow, edge darkening, wrong-light fade | rare; tied to pressure flags |
| Character idle | blink, breath, tiny posture shift | optional; never cartoon-like |

## Forbidden Motion

- Looping jump-scare movement.
- Constant spooky pulsing on every screen.
- Glowing eyes or hidden creature animation.
- Fast horror glitch that harms readability.
- UI wobble that makes documents harder to read.
- Over-animated village life that makes Кырлай feel cozy or whimsical instead of quiet.

## Production Requirement For Future Image Prompts

Every location / route / UI prompt should include:

```text
Leave clean animation-safe regions for later overlay layers: light pulse, small environmental motion and pressure overlay. Do not bake the animated effect into the base image. Keep important text blank and editable.
```

For scenes with lamps, candles, windows, CRTs, water, sky, curtains, grass or branches, the prompt should name which parts are intended as animation slots.

## Current Batch Slots

The first batch was generated before this rule was stated, but it still has usable overlay regions. Coordinates and suggested behavior are captured in `animation_layers.json`.

