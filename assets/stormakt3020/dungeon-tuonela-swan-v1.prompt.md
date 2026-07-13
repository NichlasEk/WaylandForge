# Svanen i Tuonela v1

Generated with Codex's built-in image generation in reference-guided mode. The temple prop sheet and
the active temple act sheet were supplied as material, palette, sprite-style and sheet-layout references.

## Final prompt

Use case: stylized-concept

Asset type: 2x2 top-down action-RPG boss sprite sheet for Stormakt 3020.

Create Svanen i Tuonela, one sacred and sorrowful black metallic swan threshold boss, shown as four
coherent states of the same creature. Use a perfectly flat solid `#ff00ff` magenta chroma-key background
with thin pure-white separator lines exactly through the center. No floor, water plane, cast shadows,
reflections, scenery, text, labels or watermark.

Match the references' crisp detailed painted 16-/32-bit-era top-down action-RPG art. The swan is made
from layered blackened-iron feathers and dark-silver joints, with a long articulated neck, narrow mournful
face, faint cold-blue runes in the feather seams and very sparse aged-brass fittings. It is sacred and
tragic rather than demonic. No gore.

- Top left: dormant folded-wing state, neck bowed like an ancient funerary sculpture.
- Top right: awakened idle state, neck raised in an S curve, wings slightly open, runes lit.
- Bottom left: broad wing-sweep telegraph, both wings extended into a wide readable attack arc.
- Bottom right: neck-strike state, long neck lunging forward while the wings brace backward.

Use an orthographic three-quarter top-down view. Center every state with generous padding, consistent
scale and cold subterranean rim lighting. Keep the subject opaque with a crisp silhouette; no magenta in
the creature, detached parts, extra swans, humanoid rider or crown.

## Production workflow

The built-in result is retained as `dungeon-tuonela-swan-v1-source.png`. The runtime alpha sheet
`dungeon-tuonela-swan-v1.png` was produced with the imagegen skill's `remove_chroma_key.py` helper using
border auto-keying, soft matte, despill, transparent threshold 12 and opaque threshold 220.
