# Stormakt 3020: lokalt LTX-2-videoflöde

Det här dokumentet beskriver den lokala, fungerande vägen från en Stormakt-bild
till en kort LTX-2-video med genererat stereoljud. Flödet verifierades den
22 juli 2026 på ett GeForce RTX 4090 med 24 GB VRAM.

## Verifierat resultat

Smoke-testet genererade följande fil:

```text
/home/nichlas/ai/comfy-profiles/ltx-2/output/video/Stormakt_Intro_Smoke_00001_.mp4
```

Resultatet är 4,04 sekunder långt och innehåller 97 bildrutor i 24 fps. Videon
är H.264 i 384 x 224 och har ett stereospår i AAC, 24 kHz. Ljudnivån mättes till
-12,1 dB i medel och -2,3 dB som högsta topp, alltså hörbart utan klippning.

Smoke-testet visar att hela kedjan fungerar. Upplösningen är avsiktligt modest;
nästa kvalitetssteg bör provas separat eftersom renderingen redan använder nästan
hela 24 GB VRAM.

## Lokala delar

| Del | Sökväg |
| --- | --- |
| ComfyUI | `/home/nichlas/ai/ComfyUI` |
| Isolerad profil | `/home/nichlas/ai/comfy-profiles/ltx-2` |
| Launcher | `/home/nichlas/ai/start-ltx-2-optimized.sh` |
| Fungerande API-graf | `/home/nichlas/ai/comfy-profiles/ltx-2/user/default/workflows/Stormakt_Intro_Smoke_API_working.json` |
| Indatabild | `/home/nichlas/ai/comfy-profiles/ltx-2/input/stormakt-3020-logo.png` |
| Videoutdata | `/home/nichlas/ai/comfy-profiles/ltx-2/output/video` |
| LTX-2 FP8-checkpoint | `/home/nichlas/ai/ComfyUI/models/checkpoints/LTX-2/ltx-2-19b-distilled-fp8.safetensors` |
| Gemma 3 FP4 | `/home/nichlas/ai/ComfyUI/models/text_encoders/LTX-2/gemma_3_12B_it_fp4_mixed.safetensors` |

Profilen har dessutom symlänkar till VAE, audio-VAE samt temporal och spatial
upscaler. Checkpointnamnet `LTX-2/ltx-2-19b-distilled.safetensors`, som används av
textloadern, pekar vidare på den lokala FP8-checkpointen.

## Start efter omboot

Starta ComfyUI i en terminal:

```bash
cd /home/nichlas/ai
./start-ltx-2-optimized.sh
```

Servern ska då finnas på:

```text
http://127.0.0.1:8198
```

Kontrollera utan webbläsare:

```bash
curl -sS http://127.0.0.1:8198/system_stats | jq '{system: .system.comfyui_version, devices: .devices}'
```

Launchern använder den isolerade profilen, `--lowvram`, 2 GB reserverat VRAM,
avstängd pinned memory och profilens egen SQLite-databas. Endast de custom nodes
som workflowfamiljen behöver tillåts.

## Köa det bevisat fungerande smoke-testet

API-grafen innehåller både den exekverbara nodgrafen under `.output` och den
ursprungliga GUI-grafen under `.workflow`. Skapa en ComfyUI-request och skicka
den så här:

```bash
jq '{
  prompt: .output,
  extra_data: {extra_pnginfo: {workflow: .workflow}},
  client_id: "stormakt-intro"
}' \
  /home/nichlas/ai/comfy-profiles/ltx-2/user/default/workflows/Stormakt_Intro_Smoke_API_working.json \
  > /tmp/stormakt-intro-request.json

curl -sS \
  -X POST \
  -H 'Content-Type: application/json' \
  --data-binary @/tmp/stormakt-intro-request.json \
  http://127.0.0.1:8198/prompt
```

Svaret innehåller ett `prompt_id`. Följ kön med:

```bash
curl -sS http://127.0.0.1:8198/queue | jq .
```

När kön är tom kan resultatet hittas under profilens `output/video`-katalog.
Historiken för ett visst jobb kan kontrolleras med:

```bash
curl -sS "http://127.0.0.1:8198/history/PROMPT_ID" | jq .
```

## Gör en ny Stormakt-video

1. Kopiera den fungerande API-grafen till ett nytt namn.
2. Lägg en PNG- eller JPG-bild i profilens `input`-katalog.
3. Ändra indatabild, prompt, storlek, längd och utdata-prefix i kopian.
4. Köa kopian med samma API-recept som ovan.
5. Gör först ett kort smoke-test innan upplösning eller längd höjs.

De viktigaste nod-ID:na i den fungerande grafen är:

| Nod | Betydelse | Fält |
| --- | --- | --- |
| `5175` | Positiv prompt | `.output["5175"].inputs.value` |
| `5180` | Indatabild | `.output["5180"].inputs.image` |
| `5185` | Arbetsstorlek | `.output["5185"].inputs.width` och `.height` |
| `5186` | Grundlängd | `.output["5186"].inputs.value` |
| `5184` | Bildfrekvens | `.output["5184"].inputs.value` |
| `4958` | Filprefix | `.output["4958"].inputs.filename_prefix` |

Exempel som skapar en variant utan att skriva över originalgrafen:

```bash
jq '
  .output["5175"].inputs.value = "NY ENGELSK VIDEOPROMPT" |
  .output["5180"].inputs.image = "ny-stormakt-bild.png" |
  .output["4958"].inputs.filename_prefix = "video/Stormakt_Ny_Intro"
' \
  /home/nichlas/ai/comfy-profiles/ltx-2/user/default/workflows/Stormakt_Intro_Smoke_API_working.json \
  > /tmp/stormakt-ny-intro-api.json
```

Utgå från en tydlig engelsk prompt. Beskriv kronologiskt vad som ska röra sig,
kamerarörelse, vad som måste bevaras och vilket ljud som ska höras. För logotyper
är formuleringar som `preserve the supplied crest`, `no additional words or
letters` och `final frame holds steady` användbara.

## Parametrarna i smoke-testet

```text
Indata:       stormakt-3020-logo.png
Arbetsbredd:  768
Arbetshöjd:   448
Grundrutor:   49
Bildfrekvens: 24 fps
Filprefix:    video/Stormakt_Intro_Smoke
```

Den temporala uppskalningen gav 97 slutliga rutor och cirka fyra sekunders film.
Den aktuella grafen gav 384 x 224 som faktisk videoupplösning trots arbetsmåttet
768 x 448. Höj därför inte bara siffrorna blint; kontrollera faktisk MP4 med
`ffprobe` efter varje variant.

## Viktig loaderlösning

Den fungerande grafen använder ComfyUI:s inbyggda nod:

```text
LTXAVTextEncoderLoader
```

med:

```text
text_encoder = LTX-2/gemma_3_12B_it_fp4_mixed.safetensors
ckpt_name    = LTX-2/ltx-2-19b-distilled.safetensors
device       = default
```

Använd inte den äldre `LTXVGemmaCLIPModelLoader` med den enkla Comfy-Org-FP4-
filen. Den loadern förväntar sig hela Googles femdelade Transformers-katalog,
inklusive `tokenizer.model`, `preprocessor_config.json` och `model-*.safetensors`.
Det skulle både kräva fler filer och ta betydligt mer diskutrymme.

En vanlig `CLIPLoader` med typen `ltxv` kan läsa FP4-filen men saknar LTXAV:s
audio/video-connectors. I den här grafen gav det inkompatibla tensordimensioner i
samplern. `LTXAVTextEncoderLoader` är därför en nödvändig del av det fungerande
flödet, inte bara en optimering.

De två gamla noderna `LTXVGemmaEnhancePrompt` togs bort ur den exekverbara grafen.
De var markerade som utgångsnoder och kördes annars trots att deras resultat inte
användes av videon. Prompten går nu direkt från nod `5175` till `CLIPTextEncode`.

## Kontrollera en färdig video

```bash
ffprobe -v error \
  -show_entries format=filename,duration,size,bit_rate:stream=index,codec_name,codec_type,width,height,r_frame_rate,sample_rate,channels \
  -of json \
  /home/nichlas/ai/comfy-profiles/ltx-2/output/video/FILNAMN.mp4
```

Kontrollera att ljudet finns och inte klipper:

```bash
ffmpeg -hide_banner \
  -i /home/nichlas/ai/comfy-profiles/ltx-2/output/video/FILNAMN.mp4 \
  -map 0:a:0 -af volumedetect -f null - 2>&1 |
  rg 'mean_volume|max_volume'
```

## Frigör VRAM efter rendering

Servern kan lämnas igång medan modellerna lossas ur grafikminnet:

```bash
curl -sS \
  -X POST \
  -H 'Content-Type: application/json' \
  -d '{"unload_models":true,"free_memory":true}' \
  http://127.0.0.1:8198/free
```

Efter smoke-testet sjönk användningen från ungefär 22,4 GB till ungefär 3 GB.
Det är särskilt bra före speltest, fjärrskrivbord eller annan GPU-användning.

## Nästa rekommenderade pass

1. Behåll 49 grundrutor och prova en ny prompt/indatabild först.
2. Bedöm bildidentitet, rörelse och ljud separat.
3. Prova därefter högre faktisk upplösning i ett eget jobb och övervaka VRAM.
4. För en längre spelintro: generera flera korta, kontrollerbara scener och klipp
   ihop dem, i stället för att försöka generera hela sekvensen i ett enda jobb.
5. Lägg slutlig musik, dialog och exakt mix i Stormakts vanliga ljudverktyg; se
   även `docs/stormakt3020-audio-toolchain.md`.

