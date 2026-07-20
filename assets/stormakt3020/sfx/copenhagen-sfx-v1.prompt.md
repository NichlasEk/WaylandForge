# Köpenhamns ring och Codex Argentum - Stable Audio 3 SFX v1

Model: local `stabilityai/stable-audio-3-small-sfx`, run entirely offline from the accepted local model cache.

Runtime target: PCM16 stereo, 48 kHz. Every effect uses eight diffusion steps, CFG `1.0`, a fixed seed and the shared negative prompt `music ambience speech voices long reverb repeated sequence laser beam generic sci-fi blaster`.

The canonical names, prompts, durations and seeds live in `tools/stormakt3020/generate_copenhagen_sfx.py`. Raw 44.1 kHz generations are retained unchanged under `sfx/raw/`; runtime siblings are normalized to -10 LUFS/-1 dBTP before their per-event mix gain, resampled to 48 kHz and end in a 120 ms fade. The script loads Small-SFX once and renders the family sequentially. `--runtime-only` remixes the accepted raw files without loading the model.

The 23-effect family deliberately separates ring locks and clocks, Frederik's chains and ledgers, the Eye's optics, Armada machinery, the landing, Holmen's written silver, physical moose/horse legends, Korrektorius's pens, Christian's claims and the Codex recognition. Moose and horse prompts explicitly request physical hoof, armor, timber and animal textures while the common negative prompt excludes laser and generic blaster language.

Regenerate with:

```sh
/home/nichlas/ai/stable-audio-3/.venv/bin/python \
  tools/stormakt3020/generate_copenhagen_sfx.py
```
