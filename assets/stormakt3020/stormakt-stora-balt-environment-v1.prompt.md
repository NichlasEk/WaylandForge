# Stormakt 3020 Stora Bält environment v1

Generated with the built-in image generation model on 2026-07-11. The existing Swedish/Danish spacecraft production sheets were supplied as style references only.

Two outputs were requested:

1. A strict 2x4 chroma-key production sheet in crisp late-arcade 16-bit pixel art: intact and critically damaged baroque gravity-bridge spans; a Danish red-white bridge turret; a copper/cyan energy node; matching broken left/right bridge arches; a distant wrecked Swedish blue-gold line cruiser; and a grouped Belt asteroid/debris field. Every object was isolated on a uniform `#00ff00` backdrop with no labels, UI, borders, cast shadows, characters or watermark.
2. A tall vertically scrolling background plate: midnight-blue space, muted turquoise-gray nebula, subdued rust-red battle scars, faint aurora, tiny stars and distant orbital ruins. The middle 60 percent was kept calm and low-contrast for projectile readability, with bright player/projectile colors explicitly excluded.

The environment source is `stormakt-stora-balt-environment-v1-source.png`. Its alpha production sibling was made with the imagegen skill's `remove_chroma_key.py` helper using border auto-key, soft matte, despill, transparent threshold 12 and opaque threshold 220. The opaque background plate is `stormakt-stora-balt-background-v1.png`.
