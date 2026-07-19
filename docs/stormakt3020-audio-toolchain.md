# Stormakt 3020 local audio toolchain

Stormakt audio is produced entirely on the local RTX 4090. The proven path is:

1. A versioned JSON brief in `assets/stormakt3020/music/prompts/`.
2. The warm local ACE-Step 1.5 API at `127.0.0.1:8001`.
3. A 48 kHz stereo PCM16 source WAV stored with the game assets.
4. `ffprobe` format validation and `ffmpeg` peak/mean-level validation.
5. Runtime conversion to F32LE, a half-second tail/head crossfade, and bounded `WFAU` packets to `waylandforge-audiod`.

No API key or cloud service is involved.

## Start the local generator

The model and caches live under `/home/nichlas/ai/eutherstudio`. Reuse the warm service when it is already running:

```sh
cd /home/nichlas/ai/eutherstudio/worker
./start-ace-api.sh
```

The default endpoint is `http://127.0.0.1:8001`. Do not start a duplicate model process when that port is already occupied; the DiT and language model together use a substantial part of GPU memory.

## Submit a prompt

```sh
curl -sS \
  --json @assets/stormakt3020/music/prompts/oresund-i-brand.json \
  http://127.0.0.1:8001/release_task
```

The response contains a task ID. Poll it with:

```sh
curl -sS \
  --json '{"task_id_list":"[\"TASK_ID\"]"}' \
  http://127.0.0.1:8001/query_result
```

On success, copy the local WAV named by `/v1/audio?path=...` into `assets/stormakt3020/music/`. Preserve the request JSON and record the returned seed and models in `generation-manifest.json`.

ACE-Step's `thinking=true` path lets the 5 Hz language model rewrite the musical brief before diffusion. This often adds useful structure, but the rewrite must be inspected: the first menu attempt turned "somber grandiose procession" into a triumphant achievement jingle and was rejected. Use `thinking=false` and `use_format=false` for a role whose mood and loop structure must stay close to the authored prompt.

## Acceptance checks

```sh
ffprobe -v error \
  -show_entries format=duration,size:stream=codec_name,sample_rate,channels \
  -of default=noprint_wrappers=1 \
  assets/stormakt3020/music/TRACK.wav

ffmpeg -hide_banner -i assets/stormakt3020/music/TRACK.wav \
  -af volumedetect -f null -
```

Required source format is PCM16, 48 kHz, stereo. Reject clipped, silent, vocal, comedic, obviously modern-pop, or structurally empty generations. Runtime looping does not require destructive edits to the source: `StormaktMusicLoop` blends the final 0.5 seconds into the opening and resumes after the overlapped head.

## Sound effects

Short effects are generated deterministically rather than through the long-form music model:

```sh
python tools/stormakt3020/build_sfx.py
```

The script uses fixed seeds, explicit oscillators, filtered noise and envelopes to rebuild five PCM16 stereo assets under `assets/stormakt3020/sfx/`: twin cannon, broadside, enemy explosion, hull hit and deploy chime. Each source peaks at about -1.3 dB.

`StormaktMusicLoop` is also the core's mixer. Music is attenuated for headroom, effect triggers are queued thread-safely from gameplay, at most 32 voices overlap, the result is clamped below full scale, and 2048-frame packets keep effect latency near 0.1 seconds. Mixing before `WFAU` is essential: separate packets would play effects after queued music instead of on top of it.

Combat and boss scores are preloaded. A gameplay track request starts a 24,000-frame crossfade inside the same stream; both the outgoing loop position and transition position advance from the daemon's accepted-frame count, including partially accepted packets. Stage reset requests the combat score so boss music cannot leak into a new run.

The original boss generation contains 12.09 seconds of near-silence from 47.91 seconds to its 60-second end. `tools/stormakt3020/build_music_loops.py` derives the active 40.000-second boss loop at an exact 84 BPM bar boundary. Runtime still performs its 0.5-second tail/head overlap, then resumes at the overlapped head, so neither silence nor duplicated head samples enter the loop.

Snapphanens ed uses the same non-destructive rule for a three-track 120 BPM family. The preserved 48-second chase and Red Hounds boss masters fade after their first 44 seconds, so the builder emits exact 22-bar runtime loops. The 24-second victory master begins its generated release after 16 seconds, so runtime uses its first eight bars. `StormaktMusicTrack.Snapphane`, `RedHoundsBoss` and `SnapphaneVictory` preload these edits and crossfade only at level reset, boss ownership and boss death respectively.

Köpenhamns Ring extends that rule to ten generated roles plus the deterministic Codex clock. Source masters remain untouched; `build_music_loops.py` cuts whole-bar loops before each generated release. The suite progresses through Ring, Frederik, Eye, Armada, Superfrigate, Landing, Holmen, Legends, Wrath and Victory. `CurrentCopenhagenMusicTrack()` also derives the correct role from a developer fixture or restored ground room, preventing a generic track from flashing in before the first transition. The first Eye seed is preserved as rejected because it opened with one second of silence; seed `30201913` is the accepted immediate-entry replacement.
