# Öresund crown core v1

Mode: built-in image generation, reference-guided `stylized-concept`.

Style reference: `stormakt-oresund-twin-fortress-v1-source.png`.

Prompt:

> Use case: stylized-concept. Asset type: production 2x2 boss-core sprite sheet for Stormakt 3020, matching Image 1 exactly in gritty pre-rendered 1990s pixel-art texture, top-down orthographic view, black iron, worn brass, cyan electricity and Scandinavian gothic machinery. Create exactly four isolated states of ONE central Öresund Järnkrona crown core on a perfectly flat uniform solid #ff00ff chroma-key background, clean 2x2 grid, generous gutters, no overlap, no cast shadows, no floor, no text, no UI, no ships, no people, no floating plain circles. The object must be a compact mechanical crown-shaped power heart seen from directly above: a five-point brass crown frame wrapped around a faceted cyan crystal lens, black iron locking claws, red-white Danish enamel fragments and cable sockets, visually distinct from a generic round reactor, readable at 34-46 pixels. Top-left: sealed/dormant crown core with locking claws closed and dim cyan light. Top-right: exposed active crown core with claws open, bright faceted cyan crystal and visible crown silhouette. Bottom-left: heavily damaged exposed core, cracked crystal, bent crown points, restrained orange embers and intermittent cyan arcs. Bottom-right: collapsing/broken core, split crown frame and shattered crystal pieces kept tightly within the silhouette, dark center, a few contained embers, still recognizable as the same object. Consistent scale, orientation and lighting across all four states. The #ff00ff background must be perfectly uniform without gradients, texture, reflections or lighting variation, and #ff00ff must not appear in the subjects.

The source was alpha-cleaned with `remove_chroma_key.py`; the packer maps the quadrants to sealed, active, damaged and broken core states.
