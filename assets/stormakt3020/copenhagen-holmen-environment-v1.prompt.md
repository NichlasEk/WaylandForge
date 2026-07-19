# Copenhagen Holmen environment v1

Built-in image generation produced `copenhagen-holmen-environment-v1-source.png`. The source used
`dungeon-temple-props-v1.png` as the primary style reference and `dungeon-danish-enemies-v1.png`
as a supporting Danish palette reference. The flat green background was removed with the Codex
image-generation skill's chroma-key helper to create `copenhagen-holmen-environment-v1.png`.

## Prompt

```text
Use case: stylized-concept
Asset type: 4 by 2 game sprite sheet for Stormakt 3020, Holmen arsenal environment props
Primary request: Create eight separate opaque pixel-art environment sprites in a strict 4-column by 2-row grid: row 1 cell 1 an intact massive Danish naval landing anchor hanging from five heavy chain links; row 1 cell 2 the same anchor broken into two readable pieces with snapped chain; row 1 cell 3 a short separate heavy iron-and-brass chain segment; row 1 cell 4 a burning Copenhagen dock forge or arsenal tower with brick, iron, cannon fittings and orange furnace light. Row 2 cell 1 a sealed mechanical silver heart door, symmetrical and dormant; row 2 cell 2 the exact same silver heart unfolded into a glowing cyan portal; row 2 cell 3 a compact Holmen arsenal furnace with Danish baroque ironwork; row 2 cell 4 a pile of naval debris, broken cannon, timber, rope and crates.
Input images: Image 1 is the primary visual-style reference for dark detailed Diablo-like pixel-art props, metal rendering, scale and top-down/isometric viewpoint. Image 2 is a supporting palette reference for the Danish red, worn iron and brass materials.
Scene/backdrop: every cell on one perfectly flat solid #00ff00 chroma-key background for removal; no scene and no floor plane.
Style/medium: polished late-1990s PC action-RPG pixel art, crisp hand-painted pixels, readable at small runtime size, consistent with the references, Scandinavian baroque-industrial science fantasy.
Composition/framing: exact evenly sized 4x2 grid, one centered isolated object per cell, generous padding, no overlap across cells, no grid lines or borders.
Lighting/mood: strong readable forms, warm orange fire and restrained cyan silver energy, otherwise dark iron and weathered timber.
Color palette: blackened iron, tarnished brass, Danish oxblood red, soot brown, pale programmable silver, cyan highlights. Never use green on any object.
Constraints: the sealed and open heart must share the same silhouette and viewpoint; the intact and broken anchor must clearly be the same object; opaque hard-edged game sprites suitable for local chroma removal; no cast shadow, no contact shadow, no reflection, no smoke crossing cell boundaries.
Avoid: text, letters, numerals, flags, characters, watermark, logos, UI, photorealism, soft blurry painting, green pixels in objects.
```

## Runtime crops

The packer divides the sheet into four columns and two rows and exports:

- `holmen_anchor_intact`, `holmen_anchor_broken`, `holmen_chain`, `holmen_dock_forge`;
- `holmen_heart_sealed`, `holmen_heart_open`, `holmen_arsenal_furnace`, `holmen_naval_debris`.
