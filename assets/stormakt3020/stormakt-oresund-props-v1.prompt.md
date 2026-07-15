# stormakt-oresund-props-v1

Generation mode: built-in image generation followed by the imagegen skill's chroma-key removal helper.

Prompt:

> Create a production sprite sheet for the same retro 1990s dark Scandinavian science-fantasy shoot-em-up. Canvas is a flat pure magenta chroma-key background (#ff00ff), with exactly four large isolated objects arranged in a clean 2x2 grid, generous empty magenta gutters, no overlap, no shadows touching other cells, no text, no UI, no ships or characters. Strict top-down / slightly elevated orthographic game view, consistent cold gunmetal palette with oxidized steel, muted cyan runes, tiny amber lamps, gritty painterly pre-rendered pixel-art texture. Top-left: a complete gothic Öresund Kronspann bridge arch spanning horizontally, with cable braces and a central crown-like keystone. Top-right: a long straight orbital railway machinery segment with riveted sleepers, signal conduits and side cables, horizontal orientation. Bottom-left: a heavy rectangular drawbridge flap panel seen from above, designed to slide horizontally from a wall, armored ribs and a bright cyan control node at its inner tip. Bottom-right: a complementary heavy drawbridge flap panel mirrored in construction, also seen from above with armored ribs and cyan control node. Each object fully contained in its quadrant and clearly separated. High detail but readable when reduced to 30-120 pixels.

The untouched chroma generation is preserved as `stormakt-oresund-props-v1-source.png`. It was converted with key `#ff00ff`, tolerance `34`, soft matte and spill cleanup. The production PNG contains real alpha; the packer crops four fixed quadrants and trims each alpha subject independently.
