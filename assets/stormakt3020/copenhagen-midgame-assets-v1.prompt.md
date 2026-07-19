# Copenhagen Ring midgame assets v1

Generated with the built-in image generator on 2026-07-19. All three outputs use flat-green sources followed by local soft-matte alpha removal. Gameplay state, collision, moving boss parts, health markers and attack telegraphs remain code-owned.

## Runtime files

- `copenhagen-frederik-eye-v1.png`: Frederik Null and Øresunds Øje phase/wreck sheet.
- `copenhagen-royal-armada-v1.png`: Dannebrog, Absalon, Elefanten and superfregatt sheet.
- `copenhagen-radio-portraits-v1.png`: neutral/speaking portraits for Frederik, Eye, Absalon and Elefanten.
- Each production PNG has an untouched `-source.png` sibling.

Alpha removal for each sheet:

```sh
python ~/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py \
  --input SOURCE.png --out PRODUCTION.png --auto-key border \
  --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
```

## Frederik Null and Øresunds Øje prompt

References: `copenhagen-ring-machinery-v1.png` and `stormakt-danish-boss-enemies-v1.png`.

> Use case: stylized-concept
>
> Asset type: strict 4-column by 2-row transparent-ready boss sprite sheet for Stormakt 3020, Copenhagen Ring midgame
>
> Primary request: Create exactly eight isolated top-down game sprites in a strict 4 by 2 grid. Row 1 cell 1: Frederik Null's intact royal seizure throne-ship, a broad triangular Danish black-iron frigate with oxblood registry armor, brass crown bridge, hanging paper receipt plates and chain winches. Row 1 cell 2: the same ship in phase two with two enormous detachable silver register blades extended symmetrically from its flanks. Row 1 cell 3: the same ship in phase three with three exposed numbered-looking but non-textual repair sockets beneath the hull, cyan-green restoration energy and cracked armor. Row 1 cell 4: the exact same ship catastrophically broken, blades absent, throne split and orange machinery exposed. Row 2 cell 1: Øresunds Øje intact, a wide diamond-shaped top-down black-iron customs surveillance craft built around one huge mechanical cyan iris. Row 2 cell 2: the same eye craft with four detachable orange lens satellites extended around the central iris. Row 2 cell 3: the same eye craft enraged, iris red, prediction machinery open and armor cracked. Row 2 cell 4: the same eye craft blinded and broken, shattered lens, dark pupil and orange internal damage.
>
> Input images: Image 1 is the strict reference for Copenhagen black iron, tarnished brass, cyan machinery, top-down rendering and coherent intact/broken states. Image 2 is the strict reference for Danish oxblood red, white enamel, royal gold, readable boss silhouettes and pre-rendered sprite finish.
>
> Scene/backdrop: one perfectly flat solid #00ff00 chroma-key background; no scene, floor, starfield or shadows.
>
> Style/medium: richly detailed late-1990s pre-rendered arcade/PC boss sprites, painterly pixel-downsample-friendly edges, dark Scandinavian baroque-industrial science fantasy matching both references.
>
> Composition/framing: exact evenly sized 4x2 grid; one centered isolated opaque object per cell; generous clean gutters; no overlap or debris crossing cell boundaries; all ships near-orthographic top-down, nose upward, consistent scale and viewpoint. Intact/damaged variants must remain clearly the same object.
>
> Lighting/mood: hard readable silhouettes, warm brass rim light, cold cyan systems, orange damage glow.
>
> Color palette: blackened iron, gunmetal, tarnished brass, dark oxblood red, restrained white enamel, cold cyan, sparse green repair light. Do not use chroma green #00ff00 in objects.
>
> Constraints: opaque hard-edged sprites; no cast shadow, contact shadow, reflection, smoke, text, letters, numerals, UI, grid lines, borders, watermark, logos, characters or projectiles. Keep blades and satellites wholly inside their own cells.

## Royal armada prompt

References: `copenhagen-ring-machinery-v1.png` and `stormakt-danish-boss-enemies-v1.png`.

> Use case: stylized-concept
>
> Asset type: strict 4-column by 2-row transparent-ready boss sprite sheet for Stormakt 3020, Copenhagen Ring royal armada sequence
>
> Primary request: Create exactly eight isolated top-down game sprites in a strict 4 by 2 grid. Row 1 cell 1: a rotating Dannebrog compilation portal, an open circular black-iron and brass mechanism with four red-and-white royal fleet sockets and a large transparent center. Row 1 cell 2: one compact Dannebrog formation node craft, triangular and striped oxblood red and white, gold crown core. Row 1 cell 3: Absalon intact, a broad defensive shield frigate with black iron armor, white cross-bracing, cyan shield projector and two small side turrets. Row 1 cell 4: Absalon broken and falling, same silhouette with ruptured shield projector and exposed orange machinery. Row 2 cell 1: Elefanten intact, a heavy aggressive wedge frigate with an elephantine armored prow, oxblood registry armor, brass crown ram and two side turrets. Row 2 cell 2: Elefanten broken and falling, same silhouette with shattered ram and orange internal damage. Row 2 cell 3: Kong Christian's intact colossal superfregatt, a very broad triangular royal flagship with blackened iron, Danish red-white cross armor, huge gold crown bridge, three readable armored weapon sections and central sealed heart. Row 2 cell 4: the exact same superfregatt catastrophically broken, cross armor torn open, three ruined sections and exposed orange-white reactor.
>
> Input images: Image 1 is the strict reference for Copenhagen black iron, tarnished brass, circular lock architecture, top-down rendering and matched damaged states. Image 2 is the strict reference for Danish red-white-gold fleet heraldry, broad boss silhouettes and crisp pre-rendered game sprites.
>
> Scene/backdrop: one perfectly flat solid #00ff00 chroma-key background; no scene, floor, starfield or shadows.
>
> Style/medium: richly detailed late-1990s pre-rendered arcade/PC boss sprites, painterly pixel-downsample-friendly edges, dark Scandinavian baroque-industrial science fantasy matching both references.
>
> Composition/framing: exact evenly sized 4x2 grid; one centered isolated opaque object per cell; generous clean gutters; no overlap or debris crossing cell boundaries; all craft near-orthographic top-down, nose upward, consistent family design. Intact/damaged pairs must remain clearly the same object. The superfregatt may fill most of its cell but must not cross boundaries.
>
> Lighting/mood: hard readable silhouettes, warm brass edge light, cold cyan shield systems, orange damage glow.
>
> Color palette: blackened iron, gunmetal, tarnished brass, Danish oxblood red and white enamel, royal gold, restrained cyan. Do not use chroma green #00ff00 in objects.
>
> Constraints: portal center and machinery gaps must remain clear for alpha removal; opaque hard-edged sprites; no cast shadow, contact shadow, reflection, smoke, text, letters, numerals, UI, grid lines, borders, watermark, logos, characters or projectiles.

## Radio portrait prompt

References: `stormakt-radio-portraits-v1.png`, `soren-svartkrut-radio-v1.png` and `copenhagen-ring-machinery-v1.png`.

> Use case: stylized-concept
>
> Asset type: strict 2-column by 4-row radio portrait sprite sheet for Stormakt 3020, Copenhagen Ring
>
> Primary request: Create exactly eight bust portraits arranged in a strict 2-column by 4-row grid. Every row is one role: left cell neutral listening expression, right cell the exact same identity and framing speaking with mouth visibly open. Row 1 Frederik Null: an entirely fictional gaunt Danish royal seizure-administrator, male around 55, severe pale face, close silver hair, narrow moustache, oxblood registry uniform fused with black iron accounting armor, brass zero-shaped collar seal and tiny paper docket clasps; cold, meticulous, threatening. Row 2 Øresunds Øje: not human, a sentient circular black-iron customs lens filling a radio bust frame, one cyan mechanical iris in neutral state and the same iris glowing orange-red with speech shutters open in speaking state. Row 3 Absalon: an entirely fictional Danish defensive admiral, male around 60, square weathered face, short grey beard, white-cross oxblood cuirass, black iron gorget and cyan shield-projector monocle; immovable and disciplined. Row 4 Elefanten: an entirely fictional Danish assault captain, male around 45, massive broad face, shaved head, heavy dark moustache, scarred oxblood armor, brass elephant-prow gorget and black iron shoulder plates; impatient and charging.
>
> Input images: Image 1 is the strict reference for portrait scale, paired neutral/speaking structure, crisp late-1990s pre-rendered pixel-downsample finish and Danish red-white-gold officer design. Image 2 is the strict reference for rugged male facial rendering and identical paired framing. Image 3 is the strict reference for Copenhagen black iron, tarnished brass, cyan optics and machine detail.
>
> Scene/backdrop: one perfectly flat solid #00ff00 chroma-key background in every cell; no room, scenery, frame or shadow.
>
> Style/medium: richly detailed late-1990s pre-rendered PC game radio portraits, painterly pixel-downsample-friendly edges, dark Scandinavian baroque-industrial science fantasy matching the references.
>
> Composition/framing: exact evenly sized 2x4 grid; centered chest-up bust in every cell; identical crop, pose, costume and lighting within each row; generous gutter; no overlap across cells. All human roles face slightly toward screen center. Øje uses the same visual mass and crop as a bust.
>
> Lighting/mood: strong warm brass edge light, cool cyan instrument light, dark authoritative radio-communication mood.
>
> Color palette: blackened iron, oxblood red, white enamel, tarnished brass, restrained cyan; natural distinct skin and hair tones for humans. Do not use chroma green #00ff00 in subjects.
>
> Constraints: all identities entirely fictional and not resembling real people; only expression/mouth aperture changes within a pair; crisp opaque silhouettes; no cast shadow, contact shadow, smoke, text, letters, numerals, UI, borders, badges with readable writing, watermark or logos.
