# Copenhagen Ring opening graphics v1

Generated with the built-in image generator on 2026-07-19. These are concept-to-runtime bitmap assets for the opening flight of Bana 7. Gameplay state, collision, telegraphs, health bars and projectiles remain code-owned.

## Runtime files

- `copenhagen-ring-background-v1.png`: opaque vertical source packed as `copenhagen_background` (320x700) and `copenhagen_background_wide` (400x875), each mirrored vertically by the packer for seamless scrolling.
- `copenhagen-ring-machinery-v1-source.png`: untouched 4x2 flat-green generation.
- `copenhagen-ring-machinery-v1.png`: alpha-cleaned production sheet.
- Packed machinery names: `copenhagen_gate_ring`, `copenhagen_gate_ring_broken`, `copenhagen_gate_node_blue`, `copenhagen_gate_node_red`, `copenhagen_gate_node_green`, `copenhagen_gate_core`, `cph_admiralty_clock`, `cph_admiralty_clock_broken`.

The production machinery sheet was made with:

```sh
python ~/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py \
  --input assets/stormakt3020/copenhagen-ring-machinery-v1-source.png \
  --out assets/stormakt3020/copenhagen-ring-machinery-v1.png \
  --auto-key border --soft-matte --transparent-threshold 12 \
  --opaque-threshold 220 --despill
```

## Background prompt

References: `stormakt-oresund-background-v1.png` for the vertical composition and dark flight lane; `copenhagen-holmen-environment-v1.png` for Copenhagen materials and color language.

> Use case: stylized-concept
>
> Asset type: vertically scrolling gameplay background for Stormakt 3020 level 7, Copenhagen Ring approach
>
> Primary request: Create one very tall top-down space-city background showing Karl flying inward through the colossal orbital seal built around Copenhagen. The playable center lane must remain dark, readable, and mostly unobstructed while enormous broken circular fortification arcs, radial iron ribs, suspended bastions, distant copper-roof Copenhagen spires, dock lights, chains, and old royal machinery frame the left and right edges. The city is a lid holding something ancient beneath it; show subtle buried silver geometry and cold cyan seams under black iron, without revealing the Codex itself.
>
> Input images: Image 1 is the strict reference for vertical composition, top-down perspective, material detail, scrolling density, black iron architecture and dark playable lane. Image 2 is the strict reference for Copenhagen/Holmen black iron, tarnished brass, oxblood brick, orange furnace light and restrained cyan programmable silver.
>
> Scene/backdrop: deep Nordic space with sparse stars and faint blue nebula behind the orbital city; no planet dominating the frame.
>
> Style/medium: richly detailed late-1990s pre-rendered PC/arcade game background, painterly pixel-downsample-friendly edges, dark Scandinavian baroque-industrial science fantasy, matching the references.
>
> Composition/framing: portrait orientation, very tall continuous flyover, near-orthographic top-down view; central 45 percent kept navigable and low-contrast; architecture stays mainly on outer thirds; several distinct ring layers and royal seal fragments create progression from outer approach toward the inner lock. Avoid one giant bridge deck or solid wall across the center.
>
> Lighting/mood: ominous royal machinery, tiny warm amber windows, cold cyan silver seams, deep blue-black void.
>
> Color palette: blackened iron, gunmetal, tarnished brass, dark oxblood red, soot brown, cold cyan, sparse royal gold.
>
> Constraints: background only; no ships, characters, projectiles, UI, text, letters, numbers, flags, logos, watermark, borders, chroma key, transparent areas, hard horizontal seam, or central obstacle blocking player visibility.

## Machinery prompt

References: `stormakt-danish-boss-enemies-v1.png` for Danish boss heraldry and damaged-state continuity; `copenhagen-holmen-environment-v1.png` for Copenhagen materials and sprite finish.

> Use case: stylized-concept
>
> Asset type: strict 4-column by 2-row transparent-ready boss sprite sheet for Stormakt 3020 level 7, Copenhagen Ring opening encounters
>
> Primary request: Create exactly eight separate opaque top-down game sprites in a strict 4 by 2 grid. Row 1 cell 1: a complete colossal Trekroner orbital lock ring, black iron and brass with three evenly spaced royal crown sockets and a large open center. Row 1 cell 2: the exact same ring broken and twisted after defeat, with three ruptured sockets but still one coherent ring sprite. Row 1 cell 3: an isolated compact blue-cyan crown lock node, intact. Row 1 cell 4: the same node silhouette in Danish oxblood red, intact. Row 2 cell 1: the same node silhouette in cold silver-green, intact. Row 2 cell 2: the isolated central Trekroner lock core, a dense black-iron royal seal mechanism with one gold crown and cyan inner aperture. Row 2 cell 3: the Urverksamiralens complete Amiralitet clock fortress, an enormous circular top-down war machine with twelve readable hour sockets around its rim, brass clockwork, black armor, a crown core and open negative space between mechanisms. Row 2 cell 4: the exact same clock fortress catastrophically broken, stopped hands, cracked rim and exposed orange machinery, still coherent and clearly matching cell 3.
>
> Input images: Image 1 is the strict reference for Danish red-white-gold heraldic machinery, boss readability, top-down sprite rendering and damaged-state continuity. Image 2 is the strict reference for Copenhagen black iron, tarnished brass, oxblood materials, orange furnace glow, restrained cyan programmable silver, crisp edges and small-game readability.
>
> Scene/backdrop: every cell on one perfectly flat solid #00ff00 chroma-key background for removal; no scene, floor plane, shadow or starfield.
>
> Style/medium: richly detailed late-1990s pre-rendered arcade/PC boss sprites, painterly pixel-downsample-friendly edges, Scandinavian baroque-industrial science fantasy, matching both references.
>
> Composition/framing: exact evenly sized 4x2 grid; one centered isolated object per cell; generous clean gutter; no overlap or debris crossing cell boundaries; ring and clock shown near-orthographic top-down, not perspective side views. Cells 1, 2, 7 and 8 may fill most of their cell; node/core cells remain centered with padding.
>
> Lighting/mood: strong readable silhouettes, warm brass edge light, orange internal damage glow, cold cyan/silver energy.
>
> Color palette: blackened iron, gunmetal, tarnished brass, Danish oxblood red and white enamel, royal gold, cyan; only node cell 5 may use desaturated silver-green. Do not use chroma green #00ff00 anywhere in an object.
>
> Constraints: ring hole and gaps must remain clear for alpha removal; intact/damaged pairs must share exact silhouette and viewpoint; opaque hard-edged sprites; no cast shadow, contact shadow, reflection, smoke or sparks crossing cells.
>
> Avoid: text, letters, numerals, flags, characters, ships, projectiles, UI, grid lines, borders, watermark, logos, photorealism, soft blurry painting, neon green object pixels.
