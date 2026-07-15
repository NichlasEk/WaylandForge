# Öresund train coupling v1

Mode: built-in image generation, reference-guided `stylized-concept`.

Style reference: `stormakt-oresund-armored-train-v1-source.png`.

Prompt:

> Use case: stylized-concept. Asset type: production 2-frame sprite sheet for Stormakt 3020, matching the established Öresund armored train exactly in gritty pre-rendered 1990s pixel-art texture, top-down orthographic view, black iron, worn brass, restrained Danish red-white enamel and tiny cyan electrical details. Create exactly two isolated states of ONE physical armored-train master coupling / royal brake valve in a clean horizontal 2-cell sheet on a perfectly flat uniform solid #ff00ff chroma-key background, generous gutters, no overlap, no cast shadows, no floor, no text, no UI, no people, no railway signal paddle, no floating plain circle, no extra debris. Left: intact compact coupling assembly seen directly from above, a chunky crown-shaped brass locking jaw on a short black-iron drawbar, small red-white enamel collar and one restrained cyan status lens, readable at roughly 16x14 gameplay pixels. Right: the exact same coupling broken open, split locking jaw, bent drawbar, dark status lens and two tiny contained sparks, same scale and orientation, no loose fragments outside the silhouette. Keep both objects wholly inside their cells. The #ff00ff background must remain perfectly uniform without gradients, texture, reflections or lighting variation, and #ff00ff must not appear in either subject.

The source was alpha-cleaned with `remove_chroma_key.py`; the packer maps the two cells to intact and broken master-coupling states.
