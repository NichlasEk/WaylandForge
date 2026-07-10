# Stormakt 3020 radio portraits v1

Generated with the built-in image generation model on 2026-07-10.

Style references:

- `karl-cclv-swedish-hero-danish-enemies-v3.png`
- `stormakt-danish-boss-enemies-v1.png`

Prompt:

> Create one clean production sprite sheet for a retro-futuristic 16-bit vertical shmup, matching the ornate hand-pixeled detail, dramatic lighting, brass machinery, and saturated royal palette of the supplied spacecraft references. Exactly three rows and two equal columns, with one consistent adult character per row. Left column is a stern neutral radio portrait; right column is the same character speaking with a clearly changed mouth and slightly more animated expression. Head-and-shoulders/front three-quarter busts, centered, completely separated, no overlap, no borders, no labels, no text, no logos, no UI. Flat uniform chroma-key green background `#00ff00` only.
>
> Row 1: Riksamiral Ebba Grip, an exceptionally attractive adult Swedish future Carolean general-admiral. Confident and commanding rather than pin-up posed; elegant face, long pale-blonde hair, blue eyes, tasteful full bust, structured deep royal-blue military coat and breastplate with yellow-gold piping, brass gorget, three-crowns insignia, small blue-and-gold officer cap, subtle cybernetic/radio details. Noble, calm, battle-ready.
>
> Row 2: Fogde Rasmus Gyldentold, a formidable adult Danish royal tax-fleet commander. Broad weathered face, large well-groomed red beard and curled moustache, red-and-white Dannebrog officer coat, dark iron armor, restrained gold trim, mechanical toll-seal/radio hardware. Proud, severe, slightly theatrical, never a caricature.
>
> Row 3: Kung Christian, an older adult Danish space king and armada commander. Strong graying beard and moustache, stern lined face, compact futuristic crown integrated with a red-white-gold command helm, red armor, white ermine collar, brass royal machinery. Majestic, dangerous, somber.
>
> Preserve each identity, costume, pose, crop, and lighting exactly between the neutral and speaking frames. Use crisp deliberate pixel clusters, readable silhouettes, high facial detail, and a limited arcade palette. No painterly blur, no anti-aliased background contamination, no extra people, no weapons crossing cell boundaries.

The generated chroma-key original is `stormakt-radio-portraits-v1-source.png`. The runtime production sheet is `stormakt-radio-portraits-v1.png`, made with the imagegen skill's `remove_chroma_key.py` helper using border auto-key, soft matte, despill, transparent threshold 12, and opaque threshold 220.
