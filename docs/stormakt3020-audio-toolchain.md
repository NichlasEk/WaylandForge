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
