# ТЗ: зимние painterly-текстуры для УРМАН (8 семейств, 14 файлов)

Дата: 2026-09-10. Адресат: исполнитель с ImageGen-инструментом (Codex-задача).
Контекст: Акт I переведён на зиму (см. `docs/urman_knowledge_base/decision_log.md`,
запись 2026-09-10, и `design_style.md` → «Winter — Act I season lock»).
Промоушен летних кандидатов и уже сгенерированных 19 семейств — НЕ трогать.
Эта задача только про снег/лёд/зимние детали.

Куда класть файлы: `game/assets/textures/painterly/`. Правила нормализации,
seamless-проверки, provenance и записи в README — те же, что в предыдущем
ТЗ на 19 семейств (1024×1024, 8-bit RGB PNG, мозаика 2×2 в
`evidence/act1_prod_ready/winter_textures/`, SHA-256 и промпты в README).

## ОБЩИЙ БЛОК (препендировать к каждому промпту)

Generate a game texture: a perfectly seamless, tileable, square PBR albedo map.
Strict rules:
- flat, even, shadowless lighting (like an orthographic material scan);
- edge-to-edge full bleed, edges must wrap around so the tile repeats invisibly;
- stylized hand-painted look, broad visible painterly brushwork,
  Ghibli/Zelda:BotW-like material painting style;
- cold winter palette: warm-white snow, cool blue-grey shadow tones;
- no photorealistic micro-detail, no photographic grain, no normal-map relief,
  no gloss or specular highlights, no sparkle particles;
- no perspective, no vignette, no frame, no borders, no text, no watermark,
  no objects — only the material surface itself.

## Список

1. `snow_fresh_v1_albedo.png` — свежий нетронутый пушистый снег сверху.
   Material: freshly fallen fluffy snow seen from directly above, painterly
   stylization. Soft rounded drifts and gentle undulations, warm-white with
   cool blue-grey hollows between the ridges, subtle wind-ripple lines,
   broad painted brushwork, no footprints, no dirt, muted and calm, not pure
   white and not blown out.

2. `snow_fresh_v2_albedo.png` — тот же снег, крупнее рельеф и больше ветровых
   заструг. Same as snow_fresh_v1 but with larger drifts and stronger wind
   sculpting, a few sparse dry grass blades frozen into the surface near
   hollows (very few, not a pattern).

3. `snow_trampled_v1_albedo.png` — утоптанный/продавленный снег.
   Material: trampled packed snow seen from directly above, painterly
   stylization. Irregular overlapping footstep hollows and compressed patches,
   cooler grey-white than fresh snow, soft pressed edges, faint darker packed
   cores, a little exposed grit; broad painted brushwork, muted, no bright
   highlights.

4. `snow_road_v1_albedo.png` — укатанная снежная дорога.
   Material: packed snow road surface seen from directly above, painterly
   stylization. Long parallel wheel ruts with pressed darker cores, slightly
   polished runnels, scattered snow crumbs at the rut edges, cool grey-white
   with faint brown grit from the dirt road showing through in a few places,
   broad painterly brushwork, muted, matte.

5. `snow_grass_peek_v1_albedo.png` — снег с торчащей сухой травой.
   Material: thin snow over dry winter grass seen from directly above,
   painterly stylization. Mostly snow with sparse dry straw blades and sedge
   tips poking through, small melted hollows around the tufts, warm dry-grass
   ochre against cool white snow, broad painted brushwork, restrained density
   (no full grass field), muted.

6. `snow_roof_v1_albedo.png` — снежная шапка на кровле.
   Material: thick snow blanket lying on a sloping roof seen from above,
   painterly stylization. Smooth snow slab with a soft rounded edge, faint
   melt line and shallow sag between roof battens, tiny snow crumbs below the
   edge, cool white with blue-grey in the low areas, broad painterly
   brushwork, no ice gloss, no blown-out white.

7. `ice_patch_v1_albedo.png` — лёд на луже/реке.
   Material: dark river ice seen from directly above, painterly stylization.
   Deep grey-green-black ice with frosty pale scuffs, thin snow dust trails
   across the surface, faint trapped bubbles and hairline cracks, matte with
   one restrained dull sheen band (painted as value, not gloss), muted, no
   mirror reflections.

8. `ice_patch_v2_albedo.png` — тот же лёд, светлее, с тонким снежным налётом.
   Same as ice_patch_v1 but paler: a thin snow dusting across most of the ice,
   only a few dark open-ice windows, softer frost edges.

9. `rowan_berries_v1_albedo.png` — гроздья рябины для зимней кроны.
   Material: clusters of ripe rowan berries seen against snow, painterly
   stylization. Small round muted orange-red berry clusters with tiny dark
   attachment points, a few berries dusted with snow, deep red-ochre (not
   bright scarlet), broad painted dots, transparent gaps so the texture reads
   as clusters not a field. Flat view, no background objects.

10. `rowan_berries_v2_albedo.png` — та же рябина, зимняя: больше снега.
    Same as rowan_berries_v1 but winter-dusted: half the berries under snow
    caps, fewer visible berries per cluster, colder and more sparse.

11. `birch_bark_winter_v1_albedo.png` — зимняя кора берёзы.
    Material: birch bark in winter seen from the side, painterly stylization.
    Creamy white bark with dark horizontal dashes (lenticels), thin frost and
    snow dust in the bark crevices, cooler and slightly greyer than summer
    bark, soft peeling curls, vertical feature orientation, seamless
    horizontal wrap.

12. `wattle_weave_v1_albedo.png` — плетень (плетёный забор), опционально.
    Material: woven wattle fence panel seen from the side, painterly
    stylization. Horizontal flexible rods of grey-brown willow interwoven
    around vertical stakes, irregular hand-made rhythm, frost and light snow
    on the upper rods, dry winter palette, broad painted brushwork, matte.

13. `snow_slush_v1_albedo.png` — снежная каша/наледь у дороги.
    Material: slushy snow edge beside a road seen from directly above,
    painterly stylization. Dirty wet snow with brown-grey slush streaks,
    pressed ice lumps, exposed grit, melting hollows, cool white mixed with
    muted brown, broad painterly brushwork, matte, no gloss.

14. `frost_window_v1_albedo.png` — морозный узор на стекле (интерьеры/ФАП).
    Material: frost pattern on window glass seen straight on, painterly
    stylization. Delicate white fern-like ice crystals on a dark blue-grey
    glass base, mostly transparent (thin frost, not opaque), cold palette,
    no text, restrained density so it reads as glass, flat view.

## Приоритет подключения

Приоритет 1: `snow_fresh_v1`, `snow_trampled_v1`, `snow_road_v1`,
`snow_roof_v1`, `ice_patch_v1`, `birch_bark_winter_v1`.
Приоритет 2: остальные (пункты 2, 5, 8, 9–14).

## Проверка

1. Мозаика 2×2 на швы, превью-виста всех файлов в
   `evidence/act1_prod_ready/winter_textures/overview.png`.
2. Provenance-таблица в `game/assets/textures/painterly/README.md`
   (раздел «Winter surface families, 2026-09-10»).
3. Материалы подключаются задачей W2 (не в этой): ключи `snow_ground`,
   `snow_trampled`, `snow_road`, `snow_roof`, `ice`, `snow_grass`,
   `bark_birch_winter`, `rowan_berries`, `wattle`, `frost_window`.
