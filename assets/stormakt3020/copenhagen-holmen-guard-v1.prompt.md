# Copenhagen Holmen guard v1

Built-in image generation produced `copenhagen-holmen-guard-v1-source.png`. The source used
`dungeon-karl-v1.png` as the primary scale and camera reference and `dungeon-danish-enemies-v1.png`
as a costume/palette reference. The flat green background was removed with the Codex image-generation
skill's chroma-key helper to create `copenhagen-holmen-guard-v1.png`.

## Prompt

```text
Use case: stylized-concept
Asset type: 4 by 2 character sprite sheet for Stormakt 3020 Holmen dock guard
Primary request: Create eight frames of the exact same Danish Holmen arsenal guard in a strict 4-column by 2-row grid. Top row: upright alert idle, upright walking step A, upright walking step B, upright sword telegraph with weapon raised but torso still tall. Bottom row: controlled horizontal sword attack, brief upright recoil without crouching or collapsing, beginning to fall, fully fallen body. The guard wears an oxblood-red Danish naval coat, dark iron helmet with small brass lamp, leather crossbelts, black trousers and boots, and carries a short naval saber.
Input images: Image 1 is the primary reference for exact character scale, tall readable proportions, camera angle, pixel density and late-1990s action-RPG sprite presentation. Image 2 is the palette and costume reference only; do not copy its squat or heavily crouched poses.
Scene/backdrop: perfectly flat solid #00ff00 chroma-key background, one uniform color, no floor and no shadows.
Style/medium: polished late-1990s PC action-RPG pixel art, crisp hand-painted pixels, Diablo-like top-down three-quarter viewpoint, same visual family and apparent height as Karl in Image 1.
Composition/framing: exact evenly sized 4x2 grid, one centered full-body frame per cell, generous padding, boots aligned to the same baseline in all standing frames, no overlap and no grid lines.
Lighting/mood: readable iron, red wool and brass under cold arsenal light.
Color palette: oxblood red, blackened iron, brown leather, tarnished brass, pale steel. Never use green in the character.
Constraints: preserve one character identity, costume, body size and camera viewpoint across all frames; idle, walk, telegraph, attack and recoil must retain an upright tall silhouette; only the final two death frames may lower the body; opaque hard edges for chroma removal; weapon remains fully inside its cell.
Avoid: hunched idle, kneeling, crawling, squat proportions, oversized head, foreshortened tiny body, blur, soft painting, text, flags, watermark, logos, cast shadow, green pixels on the guard.
```

## Runtime crops

The packer exports `holmen_guard_idle`, two walk frames, `holmen_guard_telegraph`,
`holmen_guard_attack`, `holmen_guard_hit`, `holmen_guard_fall` and `holmen_guard_dead`.
