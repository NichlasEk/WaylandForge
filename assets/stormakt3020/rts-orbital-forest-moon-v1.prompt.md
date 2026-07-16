# Silverkroppen orbital forest moon v1

Mode: built-in image generation, reference-guided `stylized-concept`.

References: the in-game orbital insertion screenshot, `rts-forest-floor-v1.png` and `rts-silver-vein-v1.png`.

Prompt:

> Use case: stylized-concept. Create one production orbital asteroid/moon horizon overlay for Stormakt 3020. Image 1 defines the exact composition: a huge convex moon surface rising from the bottom beneath Karl's ship. Images 2 and 3 define the surface materials and silver-vein language. Output a wide landscape asset with the upper approximately 38 percent as perfectly uniform solid #ff00ff chroma key and the lower region completely filled by the curved surface of a dark forest asteroid. The horizon must be one smooth broad convex arc, highest in the center and descending toward both lower side edges, matching Image 1 so a spacecraft can remain centered above it. Surface: extremely dark charcoal rock, sparse deep-green moss and miniature black conifer forests seen from orbit, shallow impact basins, broken ridgelines, a few cold mist pockets, and several thin branching silver seams with restrained white-cyan glow derived from Image 3. Add a very thin cyan-green atmospheric/mineral rim only along the horizon and a faint cool spill immediately below it. Gritty pre-rendered 1990s PC game pixel-art texture, readable after scaling to 400x220 and 320x180, subtle detail rather than noisy photorealism. Lighting comes from upper left, with slightly brighter crater rims there and deep shadows toward lower right. No spacecraft, no stars, no UI, no text, no buildings, no large trees in foreground, no circular planet outline, no separate objects floating above the horizon. The surface must extend fully to the bottom and both side edges with no magenta holes. The #ff00ff sky area must remain perfectly flat and uniform without gradients, stars, texture, reflection, or lighting variation, and #ff00ff must not appear in the moon surface.

The runtime PNG is alpha-cleaned from the versioned chroma-key source and packed as `rts_orbit_forest_moon`; the previous code-drawn dome remains the missing-asset fallback.
