# Snapphanens ed - De Røde Hunde v1

Built-in image generation in style-reference mode, 2026-07-17. `snapphane-route-v1-source.png` was used only as the royal material, camera and faction-tech reference. Runtime alpha was produced from the flat magenta source with the Codex imagegen chroma-key helper using border auto-key, soft matte and despill.

```text
Use case: style-transfer
Asset type: boss sprite-sheet source for Stormakt 3020, a late-1990s pre-rendered vertical shoot-em-up
Input image: use only the royal red-white elite hunter in the bottom row as material, camera and faction reference; create a new boss fleet sheet
Primary request: Create exactly eight separate subjects in a strict 4-column by 2-row grid for the royal hunter fleet De Røde Hunde.
Row 1: (1) Sporet, a long narrow red-white scent-tracker frigate with brass search lanterns and a hound-nose prow; (2) Biddet, a broad armored red-white attack frigate with two visible side cannons and toothlike ram plates; (3) Koblet, the larger command frigate with royal brass crownwork and a closed sinister mechanical hound mask over its prow; (4) one thick heavy mechanical hunting-chain segment with interlocking black iron and red enamel links, compact isolated object.
Row 2: (1) badly damaged but still flying Sporet/Biddet flank-frigate form with torn plating and dark smoke contained close to hull; (2) damaged Koblet command frigate with scorched crownwork, still intact and flying; (3) detached closed red-white mechanical hound mask plate, armored and ominous; (4) same hound mask fully open as a radial hunting iris, revealing exactly three separate glowing amber-red scent vents around a dark central throat.
Scene/backdrop: perfectly flat uniform solid #ff00ff chroma-key background, no floor, no shadow and no gradient.
Style/medium: richly detailed pre-rendered 3D arcade-game sprite art, painterly pixel-downsample-friendly edges, top-down three-quarter vertical-shooter viewpoint matching the input.
Composition/framing: equal grid cells, centered subjects with generous magenta padding and no overlap; all ships point downward toward the player while remaining readable as vertical-shooter enemies.
Color palette: aged royal red and ivory paint, black iron, dirty brass, restrained amber lamps; no green faction lights.
Constraints: exactly eight subjects; the three intact ships have clearly different silhouettes; thick physical chain, not a line; open mask has exactly three distinct physical vents; no labels, text, UI, grid lines or watermark; never use magenta in subjects.
Avoid: modern clean sci-fi, generic geometric spaceships, flat vector shapes, giant flames, active full explosion, subjects touching cell edges.
```

The generator placed the closed and open mask variants together in the last lower cell. The asset builder crops those two opaque subjects separately; no regenerated or hand-painted content was substituted.
