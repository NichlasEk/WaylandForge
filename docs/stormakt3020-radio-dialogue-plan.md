# Stormakt 3020 - videokomradio, dialog och TTS

Status: första runtimeprototypen, tre engelska platshållarröster och genererade produktionsporträtt för Ebba Grip och Fogde Rasmus är implementerade. Kung Christians två porträttrutor är färdigpackade för hans kommande radiokort. Svenska och danska slutröster återstår.

## Målbild

Stormakt ska använda korta videokominslag i stilen från äldre japanska shmups: ett porträtt glider in i ett hörn, namnskylten tänds, en eller två korta textrader skrivs fram och en förgenererad röst hörs genom radiofilter. Spelet fortsätter hela tiden.

Dialogen ska ge fienderna personlighet utan att dölja projektiler eller bli en filmsekvens. En replik bör normalt vara 1.5-4 sekunder och aldrig bära information som spelaren måste höra för att överleva.

## Bildlayout vid 320x224

- Allierade sändare visas uppe till vänster; fiender uppe till höger.
- Ytterram: 138x48 pixlar, placerad direkt under den 16 pixlar höga HUD-raden.
- Porträtt: 38x38 pixlar med två bildrutor för mun/radiobrus, inte full läppsynk.
- Namn/rang: en 7 pixlar hög färgad remsa.
- Text: högst två rader, cirka 15 tecken per rad. Längre repliker delas i två radiokort.
- Svensk radio använder blå ram och gul anropslampa; dansk kunglig/fogderadio använder mörkröd ram och vit lampa; snapphanar använder svart kopparram och grön signalstörning.
- Rutan är 85 procent opak, fälls in på 8 rutor, hålls kvar efter talet i 20 rutor och fälls ut på 8 rutor.
- Radio triggas i avsiktligt lugnare vågfickor. Bossnamn och radio får inte slåss om samma skärmyta.

## Runtimearkitektur

Varje radiokort är data, inte hårdkodad timing:

```text
frame, speaker_id, side, subtitle_lines, voice_asset, portrait_id, priority, skippable
```

- En kö med högst tre väntande kort; bossvarning har högre prioritet än vanlig kommentar.
- Samma simuleringsruta ger samma dialogordning i headless och interaktiv körning.
- Texten visas alltid även om röster är avstängda eller WAV-filen saknas.
- Start tryckt under ett skippbart kort avslutar endast kortet, inte spelet.
- `StormaktMusicLoop` utökas till att mixa en separat voice-kanal. Musik duckas cirka 6 dB under tal, SFX fortsätter men tunga explosioner begränsas under repliken.
- Röster förgenereras till PCM16, mono eller stereo, 48 kHz. Ingen TTS-modell eller GPU krävs när spelet körs.
- Ett lätt radiofilter läggs vid assetbygget: högpass runt 180 Hz, lågpass runt 4.5 kHz, mild kompression och mycket svagt deterministiskt brus.

## Dots TTS - verifierat läge 2026-07-09

Den lokala workern på `127.0.0.1:18765` är varm med `dots.tts-mf`, bfloat16 och 48 kHz-utdata. Lokalkoden accepterar språk-taggar som `SV` och `DA`, och tokenizertecknen täcker ÅÄÖ/ÆØ.

Det är däremot inte samma sak som officiellt språkstöd. Dots tekniska rapport redovisar 24 MiniMax-språk, men svenska och danska ingår inte i den publicerade listan. Dots ska därför betraktas som:

- bevisat användbar för vår befintliga svenska lokalkedja,
- experimentell för dramatiskt svenskt skådespel,
- oprövad för acceptabel dansk prosodi tills ett språkmatchat A/B-prov har lyssnats igenom.

EutherLinks automatiska referensfras behöver också rättas före dansk preset-generering: den väljer engelska för `en` och svensk text för alla andra språk. Danska får annars en svensk referensfras trots `DA`-tagg.

## Engelsk platshållarpipeline

EutherLink exponerar både GrapheneOS Matcha English och den tyngre VoxCPM2-modellen. Den första prototypen använder Matcha eftersom den är varm, snabb och inte behöver flytta de tunga GPU-resurserna. VoxCPM2 är kandidaten vi sannolikt mindes som bättre men mer krävande; den sparas till ett uttrycksfullt engelskt referensprov för Dots.

Tre syntetiska engelska röster ligger under `assets/stormakt3020/radio/`. Råfiler, exakta requestfiler, jobb-id, hash och godkännandestatus finns i `voice-manifest.json`. Inga privata eller kända röstreferenser har använts. `tools/stormakt3020/build_radio_voices.py` samplar om till 48 kHz stereo och lägger på reproducerbart radiofilter.

Runtime visar korten vid fasta simuleringsrutor, spelar separat voice-kanal och duckar musiken cirka 6 dB medan repliken hörs. Start hoppar över ett aktivt kort; saknad röstfil påverkar inte text eller simulering.

Rekommenderad pilot:

1. Välj eller spela in en godkänd 5-10 sekunders neutral svensk referens och dess exakta utskrift.
2. Välj eller spela in motsvarande dansk referens. Använd inte en känd skådespelares eller verklig offentlig persons röst.
3. Rendera samma tre korta tonlägen med Dots SOAR och MF: neutral order, hotfull fogde och pressad stridsreplik.
4. Blindlyssna på uttal, rytm, röststabilitet och radiokänsla.
5. Behåll Dots endast för språk/roller som klarar provet. Byt modell eller använd en inspelad röst för resten.

## Pilotmanus

### Svenska

**RIKSAMIRAL EBBA GRIP**

> Karl CCLV, håll kursen. Stora Bält är inte förlorat än.

**KARL CCLV**

> Kronarkivet är säkrat. Vi lämnar ingen bakom oss.

**SÖREN SVARTKRUT**

> Ni skjuter prydligt, blårockar. Synd att ni siktar åt fel håll.

### Danska

**FOGDE RASMUS GYLDENTOLD**

> Svenske fartøj, læg bi. Kronens tiende forfalder nu.

**KONG CHRISTIANS FREGATTKAPTEN**

> Dannebrog holder linjen. Slip dronerne løs.

**KUNG CHRISTIAN - FINALRADIO**

> I har brudt fogdens segl. Nu møder I kronens flåde.

De danska raderna ska granskas av en dansk talare eller åtminstone genom ett separat uttalsprov innan de låses som slutmanus.

## Porträtt och karaktärsspråk

- Porträtt byggs som separata pixelark efter respektive banplan, med neutral mun och talbild samt en brusmask. Första produktionsarket är klart; runtime växlar munruta var åttonde simuleringsruta och faller tillbaka till det kodritade ansiktet om en asset saknas.
- Ebba Grip: svensk amiral, blå uniform, gul mässing, kallt lugn.
- Rasmus Gyldentold: fogde, rödvit rock, överdimensionerat mekaniskt tullsigill, självsäker men inte komisk.
- Sören Svartkrut: sotad kopparmask, svart rock och grön signalglöd; hans bild bryter ibland upp i två positioner.
- Kung Christian har nu ett fullständigt neutral/talande porträtt i assetpaketet. Det visas först när hans eget radiokort kopplas till Kung Christians Superarmada.

## Checkpoints

1. **Radioprototyp:** klar med tre engelska platshållarkort, riktiga neutral/talande porträtt för Ebba och Rasmus, kodritad fallback och headless capture.
2. **Voice pipeline:** engelsk Matcha-placeholder, radiofilter och manifest är klara. Två godkända SV/DA-referensröster, Dots-pilot och lyssningsbeslut återstår.
3. **Bana 1-dialog:** tre tidsatta radiokort, riktiga porträtt, musikduckning och voice-mix är klara. Två ytterligare kort och full ban-capture återstår.

## Acceptanskriterier

- ÅÄÖ/ÆØ renderas korrekt i namn och undertexter.
- Rutan täcker aldrig spelaren, bossens huvudsakliga träffyta eller mer än en av skärmens fyra mittkvadranter.
- En saknad röstfil påverkar inte simulering eller undertext.
- Voice-kanalen kan spelas samtidigt med musik/SFX utan samplevärden över ±0.98.
- Ingen replik startar under banans första introduktion av ett nytt dödligt skottmönster.
- Varje genererad röst har ett manifest med manus, språk, referensägande, modell, seed och godkännandestatus.
