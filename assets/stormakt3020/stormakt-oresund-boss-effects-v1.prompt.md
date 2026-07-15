# Öresund boss effects v1

Mode: built-in image generation, reference-guided `stylized-concept`.

Style reference: `stormakt-oresund-twin-fortress-v1-source.png`.

Prompt:

> Use case: stylized-concept. Asset type: production 2x2 effects sprite sheet for Stormakt 3020, matching Image 1 exactly in gritty pre-rendered 1990s pixel-art texture and top-down orthographic view, black iron, worn brass, restrained Danish red-white enamel, cyan electrical light and compact readable game silhouettes. Create exactly four isolated boss effects in a clean 2x2 grid on a perfectly flat uniform solid #ff00ff chroma-key background, generous gutters, no overlap, no cast shadows, no floor, no text, no UI, no ships or people beyond the specified effect, no plain circles. Top-left: Öresund twin-fortress crown shield field pulse A, an open-centered roughly oval defensive lattice made from thin cyan hexagonal energy plates, five brass crown-point projector nodes around the rim, sparse arcs, designed to overlay a fortress without obscuring its center, readable at about 126x110 pixels. Top-right: the exact same shield field pulse B, same size and orientation, energy shifted through alternating hex cells and brighter projector nodes, open transparent center. Bottom-left: first-stage catastrophic fortress breach seen directly from above, a compact irregular blast of torn black-iron armor plates, red-white enamel shards, white-hot core fissure, orange fire and cyan electrical arcs, no round fireball, readable at about 62x54 pixels. Bottom-right: second-stage collapsing fortress wreck effect, wider split armor sections and bent brass crown machinery folding into a dark center, contained orange embers, smoke and dying cyan arcs, no round fireball, readable at about 76x62 pixels. Keep every subject wholly within its quadrant. The #ff00ff background must remain perfectly uniform without gradients, texture, reflections or lighting variation, and #ff00ff must not appear in any subject.

The source was alpha-cleaned with `remove_chroma_key.py`; the packer maps the quadrants to two shield pulses, a first breach and a later collapse.
