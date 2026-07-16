# Fogdens tionde värld - Stable Audio 3 SFX v1

Model: local `stabilityai/stable-audio-3-small-sfx`

Runtime target: PCM16 stereo, 48 kHz. Every effect uses eight diffusion steps, CFG `1.0`, a fixed seed and the shared negative prompt `music ambience speech voices footsteps long reverb repeated sequence`.

The canonical names, prompts, durations and seeds live in `tools/stormakt3020/generate_tithe_sfx.py`. Raw 44.1 kHz generations are retained under `sfx/raw/`; the runtime siblings are resampled to 48 kHz and end in a 150 ms fade. The script deliberately loads Small-SFX once and renders the family sequentially so local GPU memory is bounded.

The family covers chain-lock release, customs gate engagement, coin-mine charge and break, register switch, seal-wall press, module installation, all four Tithe World weapon families, the Rigsregnskabet phase rupture and individual armored-ledger breaks.
