# ImageGen — школа, ДК и общественный центр

Дата: 30 сентября 2026. Исполнение: встроенный ImageGen. Изображения сохраняются в `game/assets/textures/realism_20260930/` точными исходными PNG; code/native текстурами не подменены. SHA/размеры/версии — `imagegen_manifest.json`.

## Правила интеграции

- Атласы: равные ячейки, нулевой индекс, слева направо и сверху вниз. Использовать реальную пропорцию панели, небольшой UV inset; не сжимать квадратные таблички в длинные тонкие полосы.
- `signs_exterior`: 2×2, ячейка 3:1, свежие фасадные вывески. `signs_civic_a`: 4×4, состаренные внутренние панели.
- `signs_civic_b_v2`: 4×4, исправлены мини-карты; ячейка 10 с ошибочным именем исключена, вместо неё уникальная бирка.
- `notices_v2`: 2×4; ячейки 0 и 6 исключены из-за Tatar глифов, заменены `postal_closure` и `costume_tag`.
- `craft_details`: фактически 1774×887, 2×4; каждая ячейка 4:1, хотя запрошен квадрат. Подстраивать UV/размер геометрии, не растягивать слепо.
- Паркет: метрический повтор 1,4×1,4 м, целевая ламель около 0,35×0,07 м, истинная ёлочка 90°, не chevron. Стыки повторения/масштаб проверить в движке. Лак даёт материал, не запечённый блик.
- Вымывание доски, вышивка, живопись, бумажные заломы — исходный пигмент. Не превращать цвет в выдуманные normal/height карты. Форма, медали и ткань остаются настоящей геометрией интегратора.
- Карта — декоративная учебная схема природы, не буквальная карта Татарстана/Кара-Урмана и не маршрут игрока. Портрет Тукая — новая рисованная репродукция; культурная/художественная приёмка открыта.
- Документы-квесты используют только опубликованные отрывки. 2005 допустим только архивной афише, потому что это сюжетный источник; остальные новые декоративные даты запрещены.

## Паспорта и точные использованные промпты

### herringbone_oak_v1_basecolor.png

- Статус: candidate-for-runtime-review.
- Размер: 1254×1254 px. SHA256: `b721dd2f46852197f4e4f8e2fd130187a171bf2d23b6961a722d22b3b20ac1cd`.
- UV: `{"type":"metric-repeat","metres":[1.4,1.4],"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source viewed: true perpendicular herringbone, restrained diffuse oak. Seam continuity and exact plank physical dimensions need native repeat review; no authored normal/roughness maps.

```text
Use case: photorealistic-natural. Asset type: production game PBR BASE COLOR texture only for a school and village house of culture oak parquet floor. Generate a square seamless orthographic overhead texture showing TRUE traditional 90-degree herringbone parquet: separate rectangular oak lamellas, each ~350 mm long and 70 mm wide, interlocking alternating perpendicular sticks, NOT chevron, NO V-shaped cut ends. The texture square represents 1.4 by 1.4 metres; include approximately four lamella lengths across. Entire square filled edge to edge with repeating floor pattern and exact continuous tile boundaries. Warm restrained honey-brown and amber oak, individually differing natural straight grain along each plank, close tight seams, narrow dark hairline joints, no huge cavities. Modest well-maintained satin lacquer, subtle differences of old wear and a few newly repaired planks; grain fine enough to be credible from eye height. NO baked reflection, NO specular glare, NO directional light, NO cast shadows, NO depth illusion, NO vignette, NO border, NO labels or dates, NO symbols, no furniture. Uniform neutral diffuse basecolor illumination, maximum real material clarity. Do not render a room. Seamless surface swatch fills canvas.
```

### signs_civic_a_v1_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 1254×1254 px. SHA256: `b7cfc1d45284d35a7922b7a5910cce3de17cb8d26e2f992794aadd2219c7b7b7`.
- UV: `{"columns":4,"rows":4,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: All 16 panels present source-viewed. Stronger chipped edges than desired; interior aged subset only. External first4 superseded by fresh exterior atlas. Essential text broadly legible; cultural/native final review remains.

```text
Use case: product-mockup. Asset type: unique game texture atlas for authentic rural Tatar village school/club signage. Square canvas with EXACT 4 columns and 4 rows, SIXTEEN EQUAL SQUARE CELLS. Cell boundaries at 25%,50%,75% exactly, no gutter. All panels orthographic flat front-on, each fills its cell, safe 5% inward margin for letters. Do not draw perspective, external frames, shadows, lighting or cell numbers. Carefully crafted painted enamel, varnished plywood or lightly worn ivory card backgrounds; restrained blue/green/burgundy lettering, real small scuffs, well-maintained rather than ruins, mix fresh and gently aged. Very legible Cyrillic INCLUDING Tatar Unicode letters Ә Ө Ү Җ Ң Һ; copy exact. Content row-major, each quoted string is the ONLY lettering in that cell, newline denoted /: row1: 'КАРА-УРМАН УРТА МӘКТӘБЕ / КАРА-УРМАНСКАЯ СРЕДНЯЯ ШКОЛА', 'МӘДӘНИЯТ ЙОРТЫ / ДОМ КУЛЬТУРЫ', '«КАРА УРМАН» СОВХОЗЫ / ИДАРӘСЕ · КОНТОРА', 'ПОЧТА'. Row2: 'МАКТАУ ТАКТАСЫ / ДОСКА ПОЧЁТА', 'Часть рамок снята', 'М.  А.', 'УРМАН КАМИЛЛӘРЕ / КАРА-УРМАН МӘКТӘБЕ'. Row3: 'ГАРДЕРОБ · ЧИК', 'КАССА', 'ЗАЛ · 96 УРЫН', 'КОСТЮМЕРНАЯ'. Row4: 'ИНСТРУМЕНТЫ', 'Мәктәп почмагы / Школьный уголок', 'УРМАН — балалар рәсемнәре / Лес — рисунки детей', 'Габдулла Тукай'. Use museumlettering tastefully, longer phrases split into specified lines and fit, no lost words. A supplied word must NOT be corrected, translated or reinvented. No invented dates, logos, watermarks, additional text. Flat diffuse material pigment for texture, no baked specular reflection.
```

### signs_civic_b_v1_atlas.png

- Статус: superseded.
- Размер: 1254×1254 px. SHA256: `590b8e3b8f2047699911bdd700c7baab06523a39a9fa216bf8e557ede6fb8b90`.
- UV: `{"columns":4,"rows":4,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Invented mini maps in5/7 and incorrectЖinsteadҗin10. Use v2 plus unique costume tag.

```text
Use case: product-mockup. Asset type: Cyrillic/Tatar handmade signage BASECOLOR atlas for rural school and village club. Square canvas EXACTLY4 columns ×4 rows, SIXTEEN EQUAL SQUARE CELLS, boundaries at25%,50%,75%, no gutter, NO cell index labels. Every cell flat orthographic front-on, filled edge to edge with a lightly aged ivory paper, smooth cream enamel or warm pale plywood plaque; safe5% margin inside. Handwritten pencil/ink or neatly hand-painted dark red/blue/green letters. Real modest handled edges, few tiny pinholes, newer and older mix, never ruined. No perspective, no cast shadow, no baked glare. Copy following text VERBATIM; / means linebreak. ROW1: 'УЧИТЕЛЬСКАЯ-2 / не открывать'; '5 «А» / течёт крыша'; '6 «Б» / на ремонте'; 'КЛАДОВАЯ'. ROW2: 'Проход закрыт'; 'КАРТА · КАРА-УРМАН'; 'ТАТАРСТАН'; 'КАРА-УРМАН / старый план'. ROW3: 'Снимок забрали / для архива'; 'Сцену собрал / Габдулла Сабиров'; 'Наҗия апа — не трогать, / ещё дошью'; 'Автобус — борылышта / Автобус — у поворота'. ROW4: 'САБАНТУЙ'; '1–4 класс'; '5–9 класс'; 'ЧӘЙ ВАКЫТЫ'. Must render letters җ and Ә correctly, distinctive existing semantics, absolutely no invented dates/numbers except listed classroom labels. All letters completely inside cells and legible. No watermarks, logos, extra embellishment lettering, duplicate or missing panels.
```

### signs_civic_b_v2_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 1254×1254 px. SHA256: `a0961694cd952e2086cc85b83b49fc5475288a5fb3aa0cefea009e6bc6c1c19e`.
- UV: `{"columns":4,"rows":4,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Corrective edit removes invented miniature maps5/7. Cell10 still incorrectЖinsteadҗ, explicitly excluded and replaced unique costume tag. Other panels preserved broadly.

```text
Use case: text-localization. Edit target Image1 is a4×4 equalcell signage atlas. Keep SAMEsize EXACT4×4grid andALLothercells, text, layout, colours andsurfacepigmentunchanged. ChangeONLYthreecells: (1) row3column3 index10: firstword MUST be 'Наҗия', letter-by-letter Н а җ и я. Tatar Cyrillicsmall җ (UnicodeU+0497) is ж with additional DESCENDINGTAIL BELOWBASELINE atbottomright; doNOTwriteplain ж. Exact entirecelltext 'Наҗия апа — не трогать, / ещё дошью'. Maintain originalcreamcardandgreenhandlettering. (2) row2column2 index5: remove miniaturemapdrawing completely leaving plaincreambackgroundwithONLY exactheader 'КАРТА · КАРА-УРМАН' centred. (3) row2column4 index7: remove miniatureroad/buildinglandscape leaving plainagedcreambackgroundwithONLYtwoexactlines 'КАРА-УРМАН' and 'старый план'. DoNOTaddnewdrawings. Nootherchanges. Critical preserve16cells andnon-targetedexistinglettering exactly. No newnumbers, datesorwatermarks. Thisisproductionrepair to ensureuniqueexactTatarname andnotinventvillagegeography.
```

### notices_v1_atlas.png

- Статус: superseded.
- Размер: 1254×1254 px. SHA256: `e1089900da8189f39a9458ff35d73568119327a5c68a44fc32041351fb03c4c2`.
- UV: `{"columns":2,"rows":4,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed; cells0Tatarә and6җ unreliable; replaced isolated assets. Freshpaper/staff/concert/canteen remaining cells legible.

```text
Use case: product-mockup. Asset type: handmade notices/posters production game BASECOLOR atlas. Square canvas EXACT2 columns and4 rows,8 EQUAL cells, every cell fills entire area with flat front-on paper, exact boundaries x50%,y25/50/75%, no gutters, each cell landscape2:1. NO perspective, NO backdrop/table, NO cast shadows. Very legible handwritten/brush Cyrillic/Tatar incl Ә ә җ, believable cared-for school/club rural notices, varied younger vs lightly aged cream/ivory paper, tiny pinholes/creases only pigment not heavy baked folds. Copy each exact text only in its cell; / denotes linebreak. Row1left postalclosure: 'Почта ябык. / Хатлар — кибеттә, Разиләдә. / Почта закрыта. / Письма — в магазине, у Разили.' Row1right canteenmenu: 'ЧӘЙ ВАКЫТЫ / Суп · перемяч · чай'. Row2left staffnotice: 'УЧИТЕЛЬСКАЯ / Не забывайте закрывать окна'. Row2right concertbill: 'КИЧӘ · ВЕЧЕР / Субботний концерт / вход свободный'. Row3left folkfestivalposter: 'САБАНТУЙ', with restrained handmade tulip ornament. Row3right stagecraft plaque: 'Сцену собрал / Габдулла Сабиров', simple modest handwritten plywood. Row4left sewn fabric tag: 'Наҗия апа — не трогать, / ещё дошью', handwritten dark blue ink on cream linen, sewing threads lightly visible across edges. Row4right museumarchive notice: 'Снимок забрали / для архива'. Materials physically plausible, handwritten text genuinely varied but fully readable, tastefully designed, no fake official emblems. NO dates, watermark, extra text or brands.
```

### notices_v2_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 1254×1254 px. SHA256: `98b1e16468d5a710a4fa980efcdf85b246138850aada6f903e58ab14b89dca38`.
- UV: `{"columns":2,"rows":4,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Corrective request failed exactTatar glyphs in0/6; DO NOT select0/6. Other six cells legible, freshpaper mix.

```text
Use case: text-localization. Edit targetImage1 is a2column×4row equalcell handwrittennoticeatlas. Keepall8cells, completegrid, papercolours, pictorialdecoration andALLotherwordsunchanged. ChangeONLYtheTatarlettererrors inrow1column1 postalnotice androw4column1 sewnname. Postalrow1left EXACT 'Почта ябык. / Хатлар — кибеттә, Разиләдә. / Почта закрыта. / Письма — в магазине, у Разили.' Correctwordsletter-by-letter: к и б е т т ә; Р а з и л ә д ә. LowercaseTatar ә is MIRRORED LOWERCASE e openingtoleft, not э, not а or я, UnicodeU+04D9. Sewnrow4left EXACT 'Наҗия апа — не трогать, / ещё дошью'. Spell Н а җ и я. SmallTatar җ is ж plusDESCENDINGTAILbelowbaseline atbottomright (U+0497), MUSTnotplainж. Add a clearshortdownwardtail toexistingжform whilekeepinghandwrittenstyle. DoNOTchangeothercellsoraddtext. Exacttextmoreimportantthanhandwritingflourishes. Preserveasset2×4cellcontract. No datesorwatermark.
```

### children_drawings_v1_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 1254×1254 px. SHA256: `1411ce1b695ced72cfbf3f4a42c084ffb55e3f5c39a4cb4c200f91419899c265`.
- UV: `{"columns":2,"rows":2,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed fourdistinctpigmentdrawings, panel3small ambiguousfarforestmarkmatchesexistingtext; drawinghandmoreproficientthan7–10brief, aged/freshpaper mixed.

```text
Use case: illustration-story. Asset type: production game atlas of FOUR distinct actual child-made crayon/watercolour drawings pinned in a small Tatar village primary classroom. Squarecanvas EXACT2columns×2rows fourEQUALcells, bounds x50/y50, no gutters. Every cell is an entire flat landscape paper sheet, edge-to-edge, front-on orthographic scan, no frames, pins, wall, lighting, castshadow or perspective. Drawings genuine child art ages7–10, different hands and media, delicate realpaper fibres, slight imperfect edges/tiny crumplemarks as basecolor, some fresher white, some gently creamy aged. Row1left: winter wooden villagehomes with colouredwindowtrim under low orange sun and foreground snowy fence, childcrayon. Row1right: dense tall spruces withblue snow and tinyyellow birds, loose watercolour. Row2left: tranquil forest/river at dusk and snowy footbridge, childcolouredpencil. Row2right: a child's darkgreen forestedge drawing, thin tall ordinary human-shaped darkmark very SMALL and ambiguous at far forestedge, barely noticeable amongtree trunks, no face/eyes/claws or named creature; narratively matches existing inspectiontext without adding clue. NO explicit monster, no violence, no scarygraphicjump, no polishedprofessional adultillustration. ALLfourdistinctcompositions, no words, signatures, names, numbers, dates, watermark or labels. Muted believable schoolpigment, calm everyday atmosphere. These are paperdecorations, not screenshots or roomrenders.
```

### chalkboard_lessons_v1_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 1490×1056 px. SHA256: `cd5824b1b04c5b2a67f3352c7b90f5269f100bbb700cd68c631b4a7701df94e5`.
- UV: `{"columns":1,"rows":2,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed sixuppercase/lowercaseTatarpairs includingtails andtwo-linealternate lesson. Exacttypesourceappearscorrect; nativelegibility/culturalreview open.

```text
Use case: scientific-educational. Asset type: actual classroom chalkboard writing BASECOLOR game atlas. Wide rectangular canvas aspect1.41:1. EXACT two equal horizontal panels stacked vertically, each panel2.82:1 aspect, boundary exactlyy50%, no gap. Each panel covers actual3.1×1.1metre chalkboard face, edge-to-edge only dark desaturated bottle-green smooth slate enamel, NO wooden frame, NO tray, NO room. Flat front-on orthographic pigment texture uniformly diffuse, no glare/shadows. Soft old wipe streaks and fine residual chalkdust barely visible. PRIMARY REQUIREMENT exact TATAR letters written with ordinary white chalk, clean teacher's handwriting, correct Unicode forms; no extra words. TOP PANEL two lines EXACT 'Ә ә   Ө ө   Ү ү' and 'Җ җ   Ң ң   Һ һ'. Ә is Cyrillic schwa looking like mirrored E/open oval with horizontal centre; Ө is O with centre bar; Ү is Y with straightlowerstem; Җ is Ж with descending bottomright tail; Ң is Н with bottomright tail; Һ is lowercase h form for uppercase and lowercase. Do not substitute Э or Ж or Н or Latin. Each upper/lowercase pair clearly separated. BOTTOM PANEL only two lines EXACT 'Татар теле' and 'Алга таба!'. White chalk small broken granular marks, believable chalk handpressure, not digital type. Writing centred with10% safe margin. No decorative symbols, no dates, no watermarks. Keep grid and text exact. No polishedgloss.
```

### museum_towel_v1_basecolor.png

- Статус: candidate-for-runtime-review.
- Размер: 809×1942 px. SHA256: `8415c0939208b66f3b0ba01c4dbe532d5cdeb739d2098fd20af5f2c35ae3944e`.
- UV: `{"type":"unique","metres":[0.5,1.2],"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed wovenlinen/redgreenmodesttulipembroideredbands, caredforagedsurface. Fibrephysicalscale/clothform owned by integrator.

```text
Use case: product-mockup. Asset type: flat textile BASECOLOR texture for an actual museum Tatar embroidered towel hung in a village school. Vertical image aspect5:12, exactfulltowel surface, orthographic front-on unlit fabric scan. Rectangularcream flaxlinen towel, fine visible wovenweft, subtle honest fibreimperfections, a few gently yellowed spots but carefully keptclean. Traditional modest Tatar floral tulip embroidery in restrainedmadderred anddarkgreen, compact stylisedpairedtulips andleafstems in narrowembroidered bands nearBOTTOM andTOP, wellmade handstitches visible. Centre largeplainlinenarea, no words, dates, brand, sacredsymbol or ornatefakeheraldry. Bottomtinyshortlinenfringes, withinimageedges, NObackground, NOgarmentmockup, NOroom, NOframe, NOperspective, NOcastshadow or bakedfoldshadow. Uniformflatdiffuselighting; raised stitchesonlypigment, geometrywillprovidedbyruntime. EntireimageuniquetowelUV0–1, no repetition, no bigstitchpolygons. Caredforoldhandmadeheirloom inregularschoolmuseum, culturallyrestrained.
```

### tukay_portrait_v1_basecolor.png

- Статус: candidate-for-runtime-review.
- Размер: 1100×1430 px. SHA256: `e4ebfb70761f3b64e3434953803c24636d16e39417f8af3047ebc12022247fbe`.
- UV: `{"type":"unique","metres":[0.5,0.65],"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed warmgouachehistoricalportraitreproduction. No dates/signature/text. Resemblance not independently museum-approved; artistic/cultural review open.

```text
Use case: historical-scene. Asset type: unique school classroom painted portrait reproduced as flat game texture, not a living character design. Vertical canvas aspect10:13. Actual recognisable Tatar poet GABДULLA TUKAY (Габдулла Тукай): slender young adult man, narrow long face, fine dark moustache, dark expressiveeyes, short darkhair, familiar formal darkjacket andsimplewhitehighcollar, modest threequarterhead portrait, sober intelligent expression. Realistic handpainted oil/gouache schoolportrait reproduction on mildlyyellowedcream paper, smallfinepaperfibres, caredfornotruined, mutedumberwarmgrey tones. Head andupperbust centred, no outerframe, no room, no wall, no perspective, no dramaticlighting or glossyplasticportrait, uniformdiffuse reproduction. No text/caption/name/date/signature/watermark atall. Keeprealhistoricalpersonappearance, no invented ethniccostume, headdress or fakeorders. The physical woodframe andcaptionwillprovided separatelybygame. Thisisprintableflatartworksurface notcinematicphotography.
```

### classroom_map_v1_basecolor.png

- Статус: candidate-for-runtime-review.
- Размер: 1520×1035 px. SHA256: `b91cec9dfaa4ecd8f5d0afa8f9a4f340917fa1b1f3bf641f30177d7c3e4e6689`.
- UV: `{"type":"unique","metres":[1.4,0.95],"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed genericnaturalgeography no roads/buildings/names. NOTactualTatarstan orKarUrmanmap, notliteralnavigationtopology.

```text
Use case: scientific-educational. Assettype: handpainted school geography EDUCATIONAL DECORATIVE MAP flatBASECOLOR texture, landscapeaspect1.47:1, for actualframedclassroomwall. Flatorthographicscanofcreampaper oldschoolmap, slightlyfadedbutwellkept, softfoldcreasesasgentlepaperpigment,no3Dlighting. Showstylisedschematicnaturalgeography: bluewindingriverwithtributaries, mutedgreenforestpatches, paleochrefields, lightcontourlines, smallmeadows, decorativecompassmark, simplehanddrawnlegendiconswithNOlettering. Thisisdecorativeeducationaldiagram NOTanyrealvillage/countrymap andNOTliteralplayernavigationtopology. DoNOTshowroads, buildings, namedlocations, latitudelongitude orplotcoordinates. No words, labels, dates, signatures, logosorwatermarks. Handpaintedcarefulschoolprojectquality, restrainedwatercolourandink, mixedslightlyoldpaperandfreshrepaircorners; nochildishrandomglyphs. Wholeimageedge-to-edgemapface,noframe,nowall,noperspective,nobakedglareorcastshadow.
```

### signs_exterior_v1_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 2172×724 px. SHA256: `7655292742eab6b2afb00ef302862e1a63ef8582dbc2ebba62225640335eae98`.
- UV: `{"columns":2,"rows":2,"cellAspect":3,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed fresh exterior4signs exact2lineTatar/Russiancontentsandpost. Physical plaque3:1ratio avoids crushingglyphs; finalnativecultural review open.

```text
Use case: product-mockup. Asset type: four actual exterior public-building signboards game BASECOLOR atlas. Wide landscape canvas ASPECT3:1 exactly. EXACT2 columns and2 rows, fourequal cells, eachcellrectangle3:1aspect. Boundariesx50% y50%; no gutter. Front-onorthographicscan ONLYflatpainted signfaces, eachfillscell edge-to-edge, no perspective, no frames, no shadows/glare. Thisratioiscritical solettersdon'tdistortonphysicalwideplaques. Row1left exactTWO centredlines 'КАРА-УРМАН УРТА МӘКТӘБЕ' and 'КАРА-УРМАНСКАЯ СРЕДНЯЯ ШКОЛА'. Row1right exactTWO lines 'МӘДӘНИЯТ ЙОРТЫ' and 'ДОМ КУЛЬТУРЫ'. Row2left exactTWO lines '«КАРА УРМАН» СОВХОЗЫ' and 'ИДАРӘСЕ · КОНТОРА'. Row2right singleword 'ПОЧТА'. UseproperTatarӘ andordinarylegiblepaintedsans-serifblockletters, nosubstitutionЭ. Schoolnavyblueenamelandcreamletters, DKmutedcreamandburgundyletters, officecreamdarkgreen, Postbluecream. Qualitywellcared-forlocalcraft, only1–2tinyedgescuffs perplaque, schoolrecentlyrepainted, NOTheavyfakesevereweathering. Uniformdiffusepigmentbasecolor. Eachlettercompletelyinsideitscell with8%edgesafearea. Noadditionaldateorwords, nowatermark, nologo. Fullcanvasisatlasofplaquesnotbuildingrender.
```

### craft_details_v1_atlas.png

- Статус: candidate-for-runtime-review.
- Размер: 1774×887 px. SHA256: `3685b61688112431892f71111da3a6e16ef917065fd0fafa4f3c88b06976aa88`.
- UV: `{"columns":2,"rows":4,"cellAspect":4,"type":"unique-atlas","rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed8detailcells. ACTUALcanvas2:1 ratherthanrequested square; eachcell4:1, normalizedgrid unchanged. Heightmarkscene hasblur/wallstrip; cropUV ifneeded. Medal/display/pennantimages containbakedshapedobjects, not normal data; can't substitute physicalgeometry.

```text
Use case: product-mockup. Assettype: handmade rural Tatar school/club decorative surface game BASECOLOR atlas. SQUAREcanvas EXACT2columns×4rows eightEQUALcells. Eachcell2:1wideaspect, boundsx50%,y25/50/75%; no gutters/celllabels. Flatorthographicfronton scans of craft surfaces, no perspective/wall/table orcastshadow, uniformdiffusepigment. Row1left: restrainedlongrepeat ofhandpaintedmadderred tulips withdarkgreenleafscrolls onivory plaster, caredforlocalstagefrieze NOtext. Row1right: amateur theatrehandpaintedbackdrop landscape bluegreyrollinghillsandgreenforestunderpalegoldenskies, visiblebrushwork, NOtext. Row2left: darkgreenlinenboundclassjournalcover, thinornamentalborder andEXACTlabel 'КЛАССНЫЙ ЖУРНАЛ', a tiny bit rubbedcorners. Row2right: four small circularbrassSabantuyprizemedals andplainribbons onburgundyfelt, tasteful embossedflowerornaments, NOnumbers/emblems/text. Row3left: cleanagedivory unfoldedpaperenvelopeface withgentlefolds andshort blueballpoint underlines NOwords, noaddress/postagestamp/date. Row3right: emptyoldhonourdisplay darkgreenfelt withsixpale rectangularghostmarks where smallportraitframesremoved, NOpeople andNOtext. Row4left: actualwooddoorjamb surface with6small pencilheightticks andONLYexactinitials 'М.' and 'А.' atdifferentheights, NOotherwriting. Row4right: modestred/greencream sewnmuseumawardpennantsflattextiles, embroideredtulipshapes, NOtext/words/dates. Distinctyoungandgentlyagedmaterialsexceptneverruined. No fakeofficialsymbol, watermarks, outerframes, extralabels. Colours restrainedandauthenticordinaryruralcraft.
```

### costume_tag_v1_basecolor.png

- Статус: candidate-for-runtime-review.
- Размер: 1774×887 px. SHA256: `3cb649a3012831c36eb0e6ac962b67e9194619dc425d78737931f5b24e11bd87`.
- UV: `{"type":"unique","metres":[0.16,0.08],"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Third isolated request succeeds visibleҗdescendingtail inНаҗия. Exact phrase source-viewed; replacesincorrecttag atlas cells.

```text
Use case: product-mockup. Assettype: ONEunique handsewn costume name tag game BASECOLOR texture, landscapeaspect2:1, noatlas. Thetagiscreamlinenflatfronton andfillswholecanvas, nooutsidebackground, no perspective, no castshadow. Tinyhandstitchesalongedges, handwriting inblueballpointinkwithmaximumclarity. Exact2linewriting: 'Наҗия апа — не трогать,' and 'ещё дошью'. TheimportantnameisTATAR: spell EXACT Н-а-җ-и-я. Thirdletter is җ UnicodeU+0497, a SMALLCyrilliczhe Ж-shapedletter WITH A SHORTDOWNWARDTAILfromitsBOTTOMRIGHT extendingbelowbaseline. It isNOTordinaryж! DeliberatelymakeitsdescendingtailCLEARLYVISIBLE belowtheletterlikeletterщtail, retainingЖcentre. Smallletterҗintheword'Наҗия'mustvisiblyhaveDESCENDER. AllotherlettersstandardhandwrittenCyrillic. UseexactmeaningandnosubstitutionoftheTatarname. Noornaments, hearts, noextrawords, no datesorsignature/watermark. Cleanrecentlysewncarefullymadeclothlabel, notfancyofficialplaque. Flatunlitpigmentonly.
```

### postal_closure_v1_basecolor.png

- Статус: candidate-for-runtime-review.
- Размер: 1448×1086 px. SHA256: `86f3a6f569fb1a89ed8425da4c282ce1cce55b7ca53c4518d9e74906093994ac`.
- UV: `{"type":"unique","metres":[0.72,0.54],"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed freshnotecorrect uppercaseӘ inКИБЕТТӘ/РАЗИЛӘДӘ. Tatar portion intentionallyuppercase forglyphclarity; meaningunchanged. Replaceswrongnotices0.

```text
Use case: product-mockup. Assettype: ONEflat unique paper postalclosure notice texture. Landscapeaspect4:3; wholecanvasunlit ivorypaperface, no frame/wall/perspective, no castshadows. Recentlyhandletteredontidypaperwithtinysimpleblueinkborder. Critical correctTatarletter Ә mirroredE, U+04D8, mustnotЭorА. TopTatarportion neatuppercase handpaintedletters EXACTthreelines 'ПОЧТА ЯБЫК.' / 'ХАТЛАР — КИБЕТТӘ,' / 'РАЗИЛӘДӘ.' Spellingisolatedwordparts КИБЕТТӘ (К И Б Е Т Т Ә) and РАЗИЛӘДӘ (Р А З И Л Ә Д Ә); everyӘ must be mirroredcapital E-lookingglyph, opensleftwithhorizontalbar. BottomRussianportionthreeexactlines 'Почта закрыта.' / 'Письма — в магазине,' / 'у Разили.' SeparateTatarandRussianwithtinyredunderline, noblemodernordinaryruralnotice. Noextrawords, nosignature/date/logo/watermark. Everyletterclearandreadable, caredfornewpaper. Exactnamepointsreader'sexistingquestlocation no inventedfacts. Basecolorpigment, nobakedglare, nospecular.
```

### quest_papers_v1_atlas.png

- Статус: superseded-pending-corrective-name.
- Размер: 1254×1254 px. SHA256: `cafe889349c73d90ec5152c05bf125dee579cbdbc03647d9275fd56af294b995`.
- UV: `{"type":"unique-atlas","columns":2,"rows":2,"rowMajor":true,"indexOrigin":0}`.
- Проверка/ограничение: Source-viewed three published excerpts and 2005front correctbroadly; staffsignaturegeneratedФарида insteadФәридә, don'tselectcell1 untiltargetededit.

```text
Use case: product-mockup. Assettype: actualquestdocuments paper BASECOLOR atlas forgameUrman. Squarecanvas EXACT2columns×2rows fourequalcells eachsquare; no gutter; bounds x50 y50. All areflatfrontonunlitpapersurface scans, no tables/walls, no perspective, castshadowsorframes. Wellmadehumanlettering, three genuine documents basedONLYonpublishedgamecanon excerpts, nofictionalclues. Copy EXACT text specified below, not loremipsum, nopseudoletters; alllegibleinside5%edgesafearea. TOPLEFT: freshcreampaperannouncement withneatprintstylebody andtinyhandwrittenloweredge note. Title 'Для родителей'. Body 'Занятия в нашей школе прекращены: детей осталось мало. Оставшихся школьников возят учиться в соседнее село.' Lowerhandwrittennote 'Кто узнаёт детей на старых фото — напишите полностью, не просто «наш» или «из соседнего дома»'. TOPRIGHT: lightlyhandledruledschoolpaper withdarkblueballpointscript. Title 'Тем, кто разбирает фотографии'. Body 'Школьные тетради положила в отдельную папку. Мансур абый забрал цифровые копии на домашний компьютер. Искать удобнее по словам «школа» или «тетрадь»; потом проверьте подпись самой страницы.' Signature 'Фәридә Габдулловна Сабирова'. Tatarә iscyrillicschwa notэandnotа. BOTTOMLEFT: genuinelyoldbutpreservedwarmcreamfestivalposter simple handpaintedtulipborder. Title 'САБАНТУЙ'. Secondline 'Кара-Урман · лето 2005 года'. Body 'Днём — игры, песни и состязания. После выступлений не торопитесь расходиться: столы и скамейки возвращаем туда, откуда принесли.' Theyear2005 is intentionallyallowedONLYthisarchivedpaper andmustnotchanged; nootherdates. BOTTOMRIGHT: completelyBLANK oldcreamarchivalpaperwithfinefibres, faintoldfoldcreaseandtinyedgewear; NOwords/marks/mapgraphornewclues. Theseareeachdiffusepaperpigmentsnot3Drenders. No watermarks, syntheticcodetext, frontmatter, IDs, fakeaddress oradditionalwords.
```

## Отдельный корректирующий запрос для quest_papers_v2_atlas.png

Первоначальный статус: v1 имя ошибочно. Итоговый v2 получен и просмотрен; обе Ә в ФӘРИДӘ имеют нужный зеркальный глиф, родительская языковая/нативная приёмка остаётся открытой. Остальные три ячейки и опубликованные абзацы сохранены.

```text
Usecase: text-localization. EdittargetImage1 is2×2paperatlas. Preservecompleteimage EXACTLYincluding4equalcells, ALLbodytext, handwriting, agedpaper, festivaldate2005 andblankcell. CorrectONLY signatureinTOPRIGHTcell loweredge. Existingincorrect'Фарида' must become EXACTTatarname. Forclarity write entireONLYsignatureline in neatuppercasehandletters: 'ФӘРИДӘ ГАБДУЛЛОВНА САБИРОВА'. The firstword EXACT7letters Ф Ә Р И Д Ә (actually6characters), spell Ф-Ә-Р-И-Д-Ә, twoӘletters. Ә is CyrillicCAPITALSCHWAU+04D8, resembles MIRRORED CAPITAL E withcurvedoutlineopeningLEFTandhorizontalcrossbar. It is NOT А, Э, Е or Я. NameФӘРИДӘ mandatorycorrect bothsecondandlastletter Ә. Keeptheblueballpoint style andsamebaselineposition; no changestootherlinesorothercells. Noadditionalwords, nowatermarks. Thisistargetedrepairtotext ofpublishedgameclue, notanewcharactername.
```

## Итог — quest_papers_v2_atlas.png

- Статус: candidate-for-runtime-review; исходник просмотрен, точные пиксели сохранены.
- Размер: 1254×1254 px; SHA256: `a7d5d8961dbbc23f6cf28f2d26fa30b70f57e5765a33eb48baac549f61230453`.
- Атлас 2×2: 0 — объявление родителям; 1 — записка о тетрадях/компьютере, исправленная подпись ФӘРИДӘ ГАБДУЛЛОВНА САБИРОВА; 2 — историческая афиша лето 2005; 3 — настоящий пустой старый бумажный лист.
- V1 с подписью «Фарида» оставлен как история/отвергнутый источник, в runtime выбирать v2.
- Ячейка 7 `craft_details` — **три тканевых вымпела, не пустая бумага**. Для пустой бумаги использовать quest v2 ячейку 3. Ячейка 4 craft — конверт со сгибом/линиями, также не пустой лист.
- Ячейка 2 `craft_details` — текст титульной наклейки 4:1. Накладывать на настоящую крышку журнала узкой наклейкой; не растягивать весь 4:1 рисунок на крышку .32×.23м.
- Все 17 исходных PNG и промпты сохранены. Были исправления текстовых ошибок, поэтому общий «всё абсолютно точно» PASS не применяется. Нативный просмотр, чтение и культурная приёмка остаются у основного исполнителя.
