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

## Laser relay charge and discharge

- Seed: `30203101`
- Duration: `2.8` seconds.
- Prompt: `Heavy Scandinavian gothic science fiction laser relay charging through brass coils, rising cyan electrical whine, sharp energy discharge at the end, dry isolated game sound effect, no music, no ambience`
- Raw: `raw/oresund-laser-relay-stable-audio3.wav`
- Runtime: `oresund-laser-relay.wav`

## Armored flap motor

- Seed: `30203102`
- Duration: `2.6` seconds.
- Prompt: `Massive armored iron drawbridge flaps closing on hydraulic pistons, deep mechanical groan, brass gears and heavy locking clank, dry isolated game sound effect, no music, no ambience`
- Raw: `raw/oresund-flap-motor-stable-audio3.wav`
- Runtime: `oresund-flap-motor.wav`

## Armored train crash

- Seed: `30203103`
- Duration: `2.8` seconds.
- Prompt: `Armored railway cannon train hits a heavy iron buffer stop, enormous metal crash, wheels screeching, rivets and wreckage settling, dry isolated game sound effect, no music, no ambience`
- Raw: `raw/oresund-train-crash-stable-audio3.wav`
- Runtime: `oresund-train-crash.wav`

## Crown core break

- Seed: `30203104`
- Duration: `2.8` seconds.
- Prompt: `Ancient mechanical brass crown power core cracking apart, faceted crystal shatters, deep iron rupture with cyan electrical arcs fading out, dry isolated boss game sound effect, no music, no ambience`
- Raw: `raw/oresund-crown-core-break-stable-audio3.wav`
- Runtime: `oresund-crown-core-break.wav`

## Crown core unlock

- Seed: `30203105`
- Duration: `2.6` seconds.
- Prompt: `Ancient Scandinavian brass crown reactor unlocking, five heavy iron claws retract in sequence, crystal power heart awakens with a low cyan electrical resonance, dry isolated boss game sound effect, no laser blast, no music, no ambience`
- Raw: `raw/oresund-crown-core-open-stable-audio3.wav`
- Runtime: `oresund-crown-core-open.wav`

## Armored train rumble

- Seed: `30203106`
- Duration: `3.2` seconds.
- Prompt: `Massive armored steam railway train rolling fast over iron bridge tracks, deep rhythmic wheel clatter, heavy chassis rumble, brass machinery vibration, isolated loop-like game sound effect, no crash, no horn, no music, no ambience`
- Raw: `raw/oresund-train-rumble-stable-audio3.wav`
- Runtime: `oresund-train-rumble.wav`

## Twin-fortress lock

- Seed: `30203107`
- Duration: `2.8` seconds.
- Prompt: `Two colossal orbital iron fortresses mechanically locking together, deep synchronized docking impact, huge gears, chains tightening and brass cross-lock bolts engaging, dry isolated boss game sound effect, no explosion, no laser, no music, no ambience`
- Raw: `raw/oresund-fortress-lock-stable-audio3.wav`
- Runtime: `oresund-fortress-lock.wav`

Both raw generations used eight diffusion steps and CFG `1.0`. Runtime conversion:

```bash
ffmpeg -i raw/oresund-guard-shot-stable-audio3.wav -t 0.85 \
  -af 'aresample=48000,afade=t=out:st=0.75:d=0.10' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-guard-shot.wav

ffmpeg -i raw/oresund-switch-break-stable-audio3.wav -t 1.70 \
  -af 'aresample=48000,afade=t=out:st=1.55:d=0.15' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-switch-break.wav

ffmpeg -i raw/oresund-laser-relay-stable-audio3.wav -t 2.80 \
  -af 'aresample=48000,afade=t=out:st=2.65:d=0.15' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-laser-relay.wav

ffmpeg -i raw/oresund-flap-motor-stable-audio3.wav -t 2.60 \
  -af 'aresample=48000,afade=t=out:st=2.45:d=0.15' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-flap-motor.wav

ffmpeg -i raw/oresund-train-crash-stable-audio3.wav -t 2.80 \
  -af 'aresample=48000,afade=t=out:st=2.60:d=0.20' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-train-crash.wav

ffmpeg -i raw/oresund-crown-core-break-stable-audio3.wav -t 2.80 \
  -af 'aresample=48000,afade=t=out:st=2.60:d=0.20' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-crown-core-break.wav

ffmpeg -i raw/oresund-crown-core-open-stable-audio3.wav -t 2.60 \
  -af 'aresample=48000,afade=t=out:st=2.45:d=0.15' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-crown-core-open.wav

ffmpeg -i raw/oresund-train-rumble-stable-audio3.wav -t 3.20 \
  -af 'aresample=48000,afade=t=out:st=3.00:d=0.20' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-train-rumble.wav

ffmpeg -i raw/oresund-fortress-lock-stable-audio3.wav -t 2.80 \
  -af 'aresample=48000,afade=t=out:st=2.60:d=0.20' \
  -ar 48000 -ac 2 -c:a pcm_s16le oresund-fortress-lock.wav
```
