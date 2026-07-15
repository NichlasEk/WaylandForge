# Stormakt 3020 - lokal röst- och ljudeffektsgenerering

Det här är den reproducerbara arbetsvägen för nya Stormakt-röster och ljudeffekter. All generering sker lokalt. Röstidentitet och SFX-frön versionshanteras i repot.

## Röster via EutherLink

Roller, repliker, språk och fasta skådespelarseeder finns i `assets/stormakt3020/radio/skanska-cast.json`. En roll har exakt en `dialogue_seed`; alla repliker för samma skådespelare använder den seed:en så rösten inte byter identitet mellan omgenereringar.

1. Starta och kontrollera EutherLink:

   ```bash
   /home/nichlas/EutherLink/start.sh
   curl http://127.0.0.1:8765/health
   ```

2. Rendera valda repliker med den syntetiska rollreferensen:

   ```bash
   python tools/stormakt3020/render_radio_cast.py lines \
     --line soren-oresund-nerve \
     --line ebba-oresund-crosscurrent \
     --line rasmus-oresund-gate
   ```

3. Bygg runtimefiler med deterministiskt radiofilter:

   ```bash
   python tools/stormakt3020/build_radio_voices.py
   ```

Rå WAV och en request-JSON utan base64-referensen hamnar i `radio/raw/`. Runtimefilerna hamnar i `radio/voices/` som 48 kHz stereo PCM16. `skanska-generation-manifest.json` sparar jobb-id, hash, backend och referenshash.

## Ljudeffekter via Stable Audio 3

Installationen finns i `/home/nichlas/ai/stable-audio-3`. Small-SFX är förstavalet för korta vapen-, maskin- och kollisionsljud. Medium är bättre lämpad för längre ljudlandskap och musikaliska förlopp.

Modellerna ligger i en repo-lokal Hugging Face-cache. Sätt därför cachevariablerna uttryckligen vid automatiserad/offline körning:

```bash
source /home/nichlas/ai/stable-audio-3/local-env.sh
export HF_HOME=/home/nichlas/ai/stable-audio-3/huggingface
export HF_HUB_CACHE="$HF_HOME/hub"
export TRANSFORMERS_CACHE="$HF_HOME/transformers"
export HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1
```

Exempel med fast seed:

```bash
/home/nichlas/ai/stable-audio-3/.venv/bin/stable-audio \
  --model small-sfx --device cuda \
  --prompt "Single dry mechanical science-fiction impact, no music, no voices" \
  --negative-prompt "music speech ambience long reverb" \
  --duration 2 --steps 8 --cfg-scale 1.0 --seed 330301 \
  --output assets/stormakt3020/sfx/raw/example.wav
```

Stable Audio skriver för närvarande 44,1 kHz stereo PCM16. Stormakt kräver 48 kHz stereo PCM16, så råfilen ska trimmas, tonas ut och konverteras med `ffmpeg`. Spara prompt, negativ prompt, seed, råfil och konverteringskommando i en `.prompt.md` bredvid effekten.

## GPU-samordning

VoxCPM2, Stable Audio, ACE-Step och ComfyUI delar samma RTX 4090. Använd deras kontrollerade frigöringsvägar; döda inte processer på måfå.

```bash
# Frigör endast laddade ComfyUI-modeller, servern stannar kvar.
curl -X POST http://192.168.32.88:8188/free \
  -H 'Content-Type: application/json' \
  -d '{"unload_models":true,"free_memory":true}'

# Pausa ACE-Step när en tung ljudrendering behöver hela kortet.
systemctl --user stop eutherstudio-ace-api.service

# Återställ alltid efter renderingen.
systemctl --user start eutherstudio-ace-api.service
```

Kontrollera före och efter med `nvidia-smi`, EutherLinks `/health` och ACE-Steps `http://127.0.0.1:8001/health`. VoxCPM2 kan frigöras när dess kö är tom med `POST /v1/resources/voxcpm2/unload`.
