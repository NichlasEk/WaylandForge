# Copenhagen Codex Argentum v1

Built-in image generation produced `copenhagen-codex-v1-source.png` using
`dungeon-temple-props-v1.png` as the primary style reference and
`copenhagen-holmen-environment-v1.png` as the supporting Copenhagen material reference. The
flat green background was removed with the image-generation skill's chroma-key helper to create
`copenhagen-codex-v1.png`.

## Prompt

```text
Use case: stylized-concept
Asset type: 4 by 2 transparent-ready game sprite sheet for Stormakt 3020 Codex Argentum chamber
Primary request: Create eight separate opaque late-1990s PC action-RPG pixel-art sprites in a strict 4-column by 2-row grid. Row 1: cell 1 a closed ancient silver mechanical book on a low black-iron pedestal; cell 2 the exact same book opening with two pale blank silver pages; cell 3 the same fully open book awakened with restrained cold cyan circuitry and thin gold marginal lines; cell 4 a single unlit human-sized Karl-shaped silver contour/statue, hollow and dormant, viewed top-down/isometric. Row 2: cell 1 a wide black-iron Copenhagen vault wall panel inset with dormant silver channels; cell 2 a circular cold mechanical clock seal with 255 tiny radial registration marks but no readable numerals; cell 3 a thin incomplete silver marginal-writing flare with no letters; cell 4 a circular programmable-silver recognition ring sized to surround a standing hero.
Input images: Image 1 is the primary style reference for dark detailed Diablo-like temple props, metal rendering, scale and isometric viewpoint. Image 2 is the supporting reference for Copenhagen black iron, tarnished brass, programmable silver and cyan energy.
Scene/backdrop: every cell on one perfectly flat solid #00ff00 chroma-key background for removal; no scene, no floor plane, no shadows.
Style/medium: polished crisp hand-painted pixel art, late-1990s PC action RPG, readable after downscaling, Scandinavian baroque-industrial science fantasy.
Composition/framing: exact evenly sized 4x2 grid, one centered isolated object per cell, generous padding, no overlap, no grid lines or borders. Preserve identical viewpoint and silhouette across the three book states.
Lighting/mood: nearly black iron and pale silver, very restrained tarnished gold, cold cyan only on awakened objects; ancient machine rather than magical fantasy.
Constraints: opaque hard-edged game sprites suitable for chroma-key removal; no cast shadow, contact shadow, reflection, smoke, or glow crossing cell boundaries; no green anywhere in an object.
Avoid: text, letters, readable numerals, flags, crowns, characters other than the single hollow contour, watermark, logos, UI panels, photorealism, blur, green object pixels.
```

## Runtime crops

The packer divides the sheet into four columns and two rows and exports:

- `codex_book_closed`, `codex_book_open`, `codex_book_awake`, `codex_instance_dormant`;
- `codex_wall_panel`, `codex_clock_seal`, `codex_margin_flare`, `codex_recognition_ring`.
