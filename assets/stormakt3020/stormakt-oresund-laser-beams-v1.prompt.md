# Öresund laser beams v1

Mode: built-in image generation, reference-guided `stylized-concept`.

Style reference: `stormakt-oresund-section-machinery-v1-source.png`.

Prompt:

> Use case: stylized-concept. Asset type: production 3-frame vertical laser-beam sprite sheet for Stormakt 3020, matching the established Öresund laser relay in gritty pre-rendered 1990s pixel-art texture, orthographic game effect, cyan-white electrical energy with restrained Danish red warning light. Create exactly three isolated very tall narrow beam subjects arranged left-to-right on a perfectly flat uniform solid #ff00ff chroma-key background, generous gutters, no overlap, no cast shadows, no floor, no text, no UI, no machinery, no people, no diagonal targeting vectors, no projectiles. Left: warning filament, a thin interrupted dark-red and bright-red vertical energy guide with tiny brass-colored sparks, readable when scaled to roughly 4 pixels wide. Center: active beam pulse A, a narrow white-hot vertical core wrapped in cyan plasma filaments and sparse red electrical arcs, hard clean silhouette, readable at roughly 10 pixels wide. Right: the exact same active beam pulse B, same width and orientation with shifted cyan arcs and alternating white-hot knots for two-frame animation. All three beams must run nearly the full height of their cells, have tapered but contained endpoints, and remain wholly separated. The #ff00ff background must stay perfectly uniform without gradients, texture, reflections or lighting variation, and #ff00ff must not appear in the beams.

The source was alpha-cleaned with `remove_chroma_key.py`; runtime scales the warning filament or alternating active pulse to the authoritative beam endpoints.
