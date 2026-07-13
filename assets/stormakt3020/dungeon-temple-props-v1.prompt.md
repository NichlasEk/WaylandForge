# Dungeon temple props v1

Generated with Codex's built-in image generation in reference-guided mode, using
`dungeon-temple-act1-v2-source.png` as the palette and material reference.

## Final prompt

Create a clean 2x2 sprite sheet of four distinct environment props for the same dark Nordic occult
temple shown in the reference image. Orthographic three-quarter top-down game sprites, crisp detailed
pixel-art / painted 16-bit aesthetic, readable at small gameplay size. Use black basalt, dark forged
iron, cold desaturated blue rune light, and very sparse aged brass accents. Keep each prop centered in
its own equal quadrant with generous empty margins. Flat solid #ff00ff magenta chroma-key background,
with thin white horizontal and vertical separator lines exactly through the middle; no shadows extending
into neighboring cells.

Top left: one tall black-iron Nordic guardian statue, severe helmeted ancient warrior, sword held downward,
heavy basalt plinth, restrained blue rune details.

Top right: one low sacrificial altar/table, dark carved basalt slab on a sturdy base, shallow central bowl
and old ritual grooves, a few brass fittings and faint blue runes; no gore, no body.

Bottom left: one closed stone sarcophagus with a carved Tuonela swan motif on the lid, heavy black basalt
and iron bands, subtle cold blue inlay.

Bottom right: the same style sarcophagus opened and cracked, displaced lid leaning beside it, dark empty
interior with a faint silver-blue supernatural glow, swan carving still visible.

No characters, no text, no labels, no interface, no watermark, no extra loose props, no perspective room,
no scenery beyond the four isolated assets.

## Production workflow

The generated chroma-key image is retained as `dungeon-temple-props-v1-source.png`. The runtime image
`dungeon-temple-props-v1.png` was produced with the imagegen skill's `remove_chroma_key.py` helper using
border auto-keying, soft matte, despill, transparent threshold 12, and opaque threshold 220.
