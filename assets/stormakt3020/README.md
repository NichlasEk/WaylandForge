# Stormakt 3020 Assets

`karl-cclv-dark-frigate-v1.png` is the active dark Swedish player-frigate sheet, with matched normal and overheated states. Its painted flame area is cropped during asset packing; runtime draws two pulsing thrust jets only while a movement direction is held. `karl-cclv-swedish-hero-danish-enemies-v3.png` remains the standard-enemy/projectile source and preserves the superseded bright player concept. `stormakt-danish-boss-enemies-v1.png` is the transparent production sheet for Kronens Tiende, fogde sloops and the tax-seal drone. `stormakt-radio-portraits-v1.png` supplies paired neutral/speaking portraits for Ebba Grip, Fogde Rasmus and Kung Christian. `stormakt-stora-balt-environment-v1.png` contains physical bridge and parallax props, while `stormakt-stora-balt-background-v1.png` is the tall scrolling nebula plate. Each `-source.png` sibling preserves the original flat green generation, and the exact image prompts are versioned beside them.

`stormakt3020-logo-v1.png` is the transparent dark-blue, iron and gold three-crowns title emblem used by the level-select screen. Its chroma-key source and exact prompt are preserved alongside it.

`soren-svartkrut-radio-v1.png` supplies matched neutral and speaking portraits for the Skånska skuggor rival transmission. Its magenta-key source and generation brief are preserved beside it.

`stormakt-bridge-cannons-projectiles-v1.png` adds three bridge-collapse pieces, intact/destroyed Danish bridge cannon states, a detachable boss broadside cannon and red/white/gold enemy projectile families. Runtime uses the wreck pieces during the existing 45-frame collapse and mirrors the broadside module for the boss's right side.

Prompt summary:

- Player ship: Karl CCLV, ornate Swedish blue/yellow and brass steampunk spaceship.
- Hero motifs: three crowns, deep royal blue panels, yellow-gold trim, brass fittings, steam vents.
- Enemies: a fictional futuristic Danish royal navy in Dannebrog red/white, dark iron and restrained gold trim.
- Use: 16-bit top-down shmup sprite sheet concept.

Runtime note:

The EXT3 core can load `stormakt3020.wfsa`, a small raw sprite pack generated from both production sheets. If the pack or a named sprite is missing, the core falls back to code-drawn sprites.

Rebuild the pack after editing/replacing the concept sheet:

```sh
python tools/stormakt3020/build_assets.py
```

The builder currently packs 42 named sprites, including normal/hot player states, environment parts, combat-detail assets and separate 320- and 400-pixel-wide scrolling backgrounds. Smaller derived cannon/wreck sizes polish the boss attachments without duplicating source art. Each background is followed by its vertical mirror, making both wrap boundaries exact instead of exposing a hard seam. Active radio cards alternate their neutral and speaking frames every eight simulation frames. All former code-drawn backgrounds, faces, player ship, bridge pieces, cannons and projectiles remain missing-asset fallbacks. The builder trims alpha and downsamples with high-quality filtering; gameplay keeps separate deterministic hitboxes.

To rebuild the transparent generated sheet from its chroma-key source:

```sh
python ~/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py \
  --input assets/stormakt3020/stormakt-danish-boss-enemies-v1-source.png \
  --out assets/stormakt3020/stormakt-danish-boss-enemies-v1.png \
  --auto-key border --soft-matte --transparent-threshold 12 \
  --opaque-threshold 220 --despill
```

The radio portrait sheet uses the same command with `stormakt-radio-portraits-v1-source.png` as input and `stormakt-radio-portraits-v1.png` as output.

The Stora Bält environment sheet uses the same command with `stormakt-stora-balt-environment-v1-source.png` as input and `stormakt-stora-balt-environment-v1.png` as output.

To rebuild from another compatible sheet without changing the active default:

```sh
python tools/stormakt3020/build_assets.py --input path/to/sheet.png
```

`WFSA` is intentionally tiny: magic/version/count, then named ARGB8888 sprites. Runtime does not need PNG decoding.

## Resolution

Stormakt defaults to a 400x280 logical framebuffer. Assets retain their native gameplay pixel size, providing 25 percent more field in both directions and making ships smaller relative to the world. WaylandForge scales the WFEX frame to its viewport. Set `WAYLANDFORGE_STORMAKT_LEGACY_320=1` to A/B test the original 320x224 field; the pack contains a correctly sized background for both modes.

## Music concept

`stormakt-over-oresund-v1.wav` is a 60-second instrumental ACE-Step concept for the main combat theme: a somber, grandiose D-minor march at 96 BPM with low strings, brass, field drums, timpani, organ and restrained harpsichord. The exact local generation request is preserved in `music-request.json`.

Audition it locally with:

```sh
pw-play assets/stormakt3020/stormakt-over-oresund-v1.wav
```

The EXT3 core starts the track automatically and streams bounded 48 kHz stereo F32LE chunks over WaylandForge's existing `WFAU` audio socket. It keeps roughly half a second buffered, handles partially accepted packets without replaying samples, retries harmlessly while the audio daemon is unavailable, crossfades the final half-second into the opening, and clears queued PCM when the core starts or stops.

Set `WAYLANDFORGE_STORMAKT_MUSIC=0` to disable music, `WAYLANDFORGE_STORMAKT_MUSIC_PATH` to audition another compatible 48 kHz stereo PCM16 WAV, or `WAYLANDFORGE_AUDIO_SOCKET` to use another audio-daemon socket.

Additional scored roles live under `music/`:

- `marsch-mot-kopenhamn-v1.wav`: somber menu and launch procession.
- `music/tre-kronors-jarnmarsch-v1.wav`: a faster, brass-led grand campaign menu march.
- `music/tre-kronors-jarnmarsch-loop-v1.wav`: the active 16-bar, 88 BPM menu loop derived from the new march.
- `oresund-i-brand-v1.wav`: faster normal-combat loop.
- `kronans-sista-salva-v1.wav`: monumental boss loop.

`kronans-sista-salva-v1.wav` is the preserved 60-second generation; its final 12.09 seconds are effectively silent. `kronans-sista-salva-loop-v2.wav` is the active non-destructive edit: 40.000 seconds, exactly 14 four-beat bars at 84 BPM, cut before the generated fade. Rebuild it with `python tools/stormakt3020/build_music_loops.py`.

The loop edit is preloaded with the combat score. Kronens Tiende requests a 0.5-second in-stream crossfade at arrival, and restarting the stage requests the same crossfade back to the combat loop. Track-transition and loop positions advance only by audio frames accepted by the daemon. If loop-v2 is absent, runtime falls back to the preserved v1 generation.

`music/generation-manifest.json` records accepted task IDs, seeds, models, prompt files, and a rejected menu attempt. The complete local workflow is documented in `docs/stormakt3020-audio-toolchain.md`.

## Sound effects

Five deterministic 48 kHz stereo effects live under `sfx/`: twin cannon, broadside, enemy explosion, hull hit, and deploy chime. Rebuild them with:

```sh
python tools/stormakt3020/build_sfx.py
```

The external core triggers them from actual gameplay events and mixes up to 32 voices into the music stream with headroom before sending 2048-frame `WFAU` packets.

## Radio voices

The first videocom prototype uses three synthetic English placeholder voices under `radio/voices/`. They were rendered by EutherLink's GrapheneOS Matcha English backend without reference-voice cloning. Requests, raw output, job IDs, hashes and approval state are preserved under `radio/`; none of these voices are final casting.

Rebuild the 48 kHz stereo radio-filtered runtime files from the raw WAV files with:

```sh
python tools/stormakt3020/build_radio_voices.py
```

The game keeps voice playback separate from ordinary effects and ducks music by about 6 dB while a radio line is active. Missing voice assets never suppress the deterministic subtitle card.
