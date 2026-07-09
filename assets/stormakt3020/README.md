# Stormakt 3020 Assets

`karl-cclv-sprite-concept.png` is the first AI-generated visual target for the EXT3 shmup core.

Prompt summary:

- Player ship: Karl CCLV, ornate brass-and-blue steampunk spaceship.
- Motifs: Swedish blue/yellow, three crowns, brass fittings, steam vents.
- Enemies: drones loosely inspired by Karoliner uniforms and banners.
- Use: 16-bit top-down shmup sprite sheet concept.

Runtime note:

The EXT3 core can load `stormakt3020.wfsa`, a small raw sprite pack generated from the concept image. If the pack is missing, the core falls back to code-drawn sprites.

Rebuild the pack after editing/replacing the concept sheet:

```sh
python tools/stormakt3020/build_assets.py
```

`WFSA` is intentionally tiny: magic/version/count, then named ARGB8888 sprites. Runtime does not need PNG decoding.
