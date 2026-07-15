# Öresund Stable Audio 3 SFX v1

Model: local `stabilityai/stable-audio-3-small-sfx`
Runtime target: PCM16 stereo, 48 kHz.

## Bridge guard shot

- Seed: `330301`
- Duration: `2` seconds raw, trimmed to `0.85` seconds.
- Prompt: `Single compact retro science-fiction bridge guard cannon shot, sharp brass electromagnetic crack, tiny mechanical relay snap, short dry metallic tail, isolated game sound effect, no music, no ambience, no voices, no repeated shots`
- Negative prompt: `music ambience speech footsteps long reverb multiple shots`
- Raw: `raw/oresund-guard-shot-stable-audio3.wav`
- Runtime: `oresund-guard-shot.wav`

## Railway switch break

- Seed: `330302`
- Duration: `3` seconds raw, trimmed to `1.70` seconds.
- Prompt: `Single heavy orbital railway switch mechanism breaking and locking into place, huge iron lever clank, brass gears grinding, short electrical arc and metal stress, isolated retro science-fiction game sound effect, dry and punchy, no music, no ambience, no voices`
- Negative prompt: `music speech footsteps long ambience repeated impacts explosion`
- Raw: `raw/oresund-switch-break-stable-audio3.wav`
- Runtime: `oresund-switch-break.wav`

Both raw generations used eight diffusion steps and CFG `1.0`. Runtime conversion:

```bash
ffmpeg -i raw/oresund-guard-shot-stable-audio3.wav -t 0.85 \
  -af 'aresample=48000,afade=t=out:st=0.75:d=0.10' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-guard-shot.wav

ffmpeg -i raw/oresund-switch-break-stable-audio3.wav -t 1.70 \
  -af 'aresample=48000,afade=t=out:st=1.55:d=0.15' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-switch-break.wav
```
