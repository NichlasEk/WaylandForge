# Fogdens tionde värld - Stable Audio 3 SFX v1

Model: local `stabilityai/stable-audio-3-small-sfx`

Runtime target: PCM16 stereo, 48 kHz. Every effect uses eight diffusion steps, CFG `1.0`, a fixed seed and the shared negative prompt `music ambience speech voices footsteps long reverb repeated sequence`.

The canonical names, prompts, durations and seeds live in `tools/stormakt3020/generate_tithe_sfx.py`. Raw 44.1 kHz generations are retained under `sfx/raw/`; the runtime siblings are resampled to 48 kHz and end in a 150 ms fade. The script deliberately loads Small-SFX once and renders the family sequentially so local GPU memory is bounded.

The family covers chain-lock release, customs gate engagement, coin-mine charge and break, register switch, seal-wall press, module installation, all four Tithe World weapon families, the Rigsregnskabet phase rupture and individual armored-ledger breaks.

## Weapon mix revision v2

Salvdirektören and Magnetbredsidan were regenerated independently after playtesting found that their original long, full-scale transients accumulated too strongly during repeated fire. The canonical runtime filenames are unchanged; the new untouched masters are `raw/tithe-volley-director-v2-stable-audio3.wav` and `raw/tithe-magnet-broadside-v2-stable-audio3.wav`.

- Salvdirektören: seed `3020514`, duration `0.55` seconds, prompt `Single subdued three-gun starship volley, three tiny brass clockwork breeches click almost together, compact soft electromagnetic pops, restrained mechanical recoil, very short dry tail, quiet isolated retro science fiction game sound effect, no cannon boom no sharp crack`, runtime gain `0.06`.
- Magnetbredsidan: seed `3020515`, duration `0.85` seconds, prompt `Single compact magnetic starship broadside, paired cyan coils make one muted low mechanical thump and a short soft field flutter, restrained brass relay click, narrow dry tail, quiet isolated retro science fiction game sound effect, no laser blast no resonant sweep`, runtime gain `0.08`.

Regenerate only these replacements with:

```sh
/home/nichlas/ai/stable-audio-3/.venv/bin/python tools/stormakt3020/generate_tithe_sfx.py \
  --only tithe-volley-director --only tithe-magnet-broadside
```
