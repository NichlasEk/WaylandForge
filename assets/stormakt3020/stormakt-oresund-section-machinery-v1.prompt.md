# Öresund section machinery v1

Mode: built-in image generation, reference-guided `stylized-concept`.

Style reference: `stormakt-oresund-guard-control-v1-source.png`.

Prompt:

> Use case: stylized-concept. Asset type: production 2x2 sprite sheet for Stormakt 3020, matching Image 1 exactly in gritty pre-rendered 1990s pixel-art texture, top-down orthographic view, black iron, worn Danish red-white armor, warm brass, cyan electrical light and readable small-game silhouettes. Create exactly four isolated machinery subjects in a clean 2x2 grid on a perfectly flat uniform solid #ff00ff chroma-key background, with generous gutters, no overlap, no cast shadows, no floor, no text, no UI, no people, no extra debris. Top-left: compact Öresund railway service carriage viewed directly from above, horizontal orientation, riveted gunmetal body, restrained Danish red-white armor strip, two small black rail wheels on each side, brass master coupling on its right edge, readable at about 34x24 pixels. Top-right: the exact same service carriage damaged and disabled, cracked armor, bent wheels, broken coupling, tiny contained orange embers, still structurally recognizable. Bottom-left: intact Öresund laser relay / double-drawbridge beam emitter seen from above, compact gothic-brass circular machine mounted on a short iron base, cyan glass energy lens, cable sockets, not a floating circle or UI icon, readable at about 28x34 pixels. Bottom-right: the exact same emitter fully charged and firing, brighter white-cyan inner lens with a restrained red warning ring and small contained electrical arcs, same silhouette and scale. Keep every subject wholly inside its quadrant. The #ff00ff background must remain perfectly uniform without gradients, texture, reflections or lighting variation, and #ff00ff must not appear in the subjects.

The source was alpha-cleaned with `remove_chroma_key.py`; the packer maps quadrants to the intact/damaged carriage and idle/charged relay.
