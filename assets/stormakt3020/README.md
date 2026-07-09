# Stormakt 3020 Assets

`karl-cclv-swedish-hero-danish-enemies-v3.png` is the active AI-generated visual target for the EXT3 shmup core. The original `karl-cclv-sprite-concept.png` and the first Danish-enemy pass remain as historical references.

Prompt summary:

- Player ship: Karl CCLV, ornate Swedish blue/yellow and brass steampunk spaceship.
- Hero motifs: three crowns, deep royal blue panels, yellow-gold trim, brass fittings, steam vents.
- Enemies: a fictional futuristic Danish royal navy in Dannebrog red/white, dark iron and restrained gold trim.
- Use: 16-bit top-down shmup sprite sheet concept.

Runtime note:

The EXT3 core can load `stormakt3020.wfsa`, a small raw sprite pack generated from the concept image. If the pack is missing, the core falls back to code-drawn sprites.

Rebuild the pack after editing/replacing the concept sheet:

```sh
python tools/stormakt3020/build_assets.py
```

To rebuild from another compatible sheet without changing the active default:

```sh
python tools/stormakt3020/build_assets.py --input path/to/sheet.png
```

`WFSA` is intentionally tiny: magic/version/count, then named ARGB8888 sprites. Runtime does not need PNG decoding.

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
- `oresund-i-brand-v1.wav`: faster normal-combat loop.
- `kronans-sista-salva-v1.wav`: monumental boss loop.

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
