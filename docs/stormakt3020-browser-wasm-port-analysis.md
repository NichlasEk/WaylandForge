# StorMakt 3020: analys inför WebAssembly-port

Datum: 2026-07-16

Status: analys och rekommenderad portplan. Ingen browserport är påbörjad.

## Sammanfattning

StorMakt 3020 är realistiskt att göra fullt spelbart som WebAssembly i en webbläsare utan att skriva om själva spelet. Den nuvarande arkitekturen har redan flera egenskaper som gör porten förhållandevis rak:

- gameplay uppdateras deterministiskt en frame i taget genom `StormaktGame.Step`;
- hela bilden mjukvarurenderas av C# till en 400 x 280 ARGB8888-buffer genom `StormaktGame.Render`;
- input representeras av ett kompakt knappbitfält samt pointerkoordinater och musknappar;
- gameplay och rendering är inte beroende av Wayland, SDL, OpenGL eller andra native-spelbibliotek;
- sprites är redan paketerade i ett fristående runtimeformat, `stormakt3020.wfsa`.

Porten behöver därför främst ersätta dagens plattformsgränser: stdin/stdout-protokollet, filsystemet och PipeWire-ljudet. Uppskattningsvis kan 80-90 procent av gameplay- och renderingskoden återanvändas.

Bedömning: en medelstor port med relativt låg teknisk risk i simulation och rendering, men tydligt arbete kring ljud, assets, saves och uppdelningen av den stora `Program.cs`.

## Nuvarande arkitektur

StorMakt körs i dag som en separat .NET-process. WaylandForge skickar input till processen och tar emot färdiga WFEX-frames över stdin/stdout.

Den körbara kärnan gör i princip följande:

1. Läser miljökonfiguration och väljer 400 x 280 eller äldre 320 x 224.
2. Laddar spritepaket och ljud.
3. Väntar på ett inputkommando från värdprocessen.
4. Kör `game.Step(buttons, pointer)`.
5. Kör `game.Render(frame, frameIndex)`.
6. Skriver en WFEX-header och hela ARGB8888-bilden till stdout.

Detta är redan nästan samma kontrakt som en browserhost behöver. Skillnaden är att browsern kan anropa spelkärnan direkt i WebAssembly och presentera bufferten på ett HTML-canvas, utan en separat process eller WFEX-serialisering.

`StormaktGame` och större delen av dess tillstånd är plattformsoberoende. De viktigaste direkta plattformsberoendena är:

- `Console.OpenStandardInput` och `Console.OpenStandardOutput` i processens huvudloop;
- `Environment`, `File`, `Directory` och `Path` för konfiguration, assets och saves;
- `StormaktMusicLoop`, som använder en bakgrundstråd och Unix-socket mot WaylandForges PipeWire-daemon;
- några menyfrågor som testar om savefiler finns direkt med `File.Exists`.

## Rekommenderad målarkitektur

Browserporten bör byggas bredvid den befintliga desktopkärnan. WaylandForge och WFEX-adaptern ska fortsätta fungera.

```text
Stormakt.Game
|- gameplay och tillstånd
|- mjukvarurenderare
|- spritepack-läsare
|- audio- och savekontrakt
|
|- Stormakt.DesktopHost
|  |- WFEX över stdin/stdout
|  |- saves på filsystemet
|  `- PipeWire/WaylandForge-audio
|
`- Stormakt.Browser
   |- .NET browser-wasm
   |- requestAnimationFrame
   |- Canvas 2D eller WebGL
   |- keyboard, pointer, touch och gamepad
   |- localStorage eller IndexedDB
   `- Web Audio
```

Det rekommenderade .NET-alternativet är ett litet `Microsoft.NET.Sdk.WebAssembly`-projekt med runtime identifier `browser-wasm` och direkt JavaScript-interop genom `JSImport`/`JSExport`. Blazor behövs inte för själva spelvyn.

Den lokala maskinen har .NET 10 SDK, men WebAssembly-workloaden `wasm-tools` är inte installerad vid analystillfället.

## Arbete som krävs

### 1. Bryt ut den återanvändbara spelkärnan

Flytta eller dela följande kod till ett vanligt bibliotek:

- `StormaktGame`;
- `RtsPointer`;
- gameplaymodeller och save-DTO:er;
- `SpritePack`;
- renderingshjälpare och fontdata;
- audio- och storagekontrakt.

Den nuvarande top-level-loopen blir kvar som desktophost och fortsätter tala WFEX med WaylandForge.

Det behöver göras i små steg eftersom `Program.cs` är över 13 000 rader och samtidigt innehåller pågående kampanjarbete. Browserporten bör inte börja med en stor omstrukturering av gameplaykoden. Första säkra steget är att införa tydliga plattformsinterface och flytta processens startkod från simulationen.

### 2. Lägg till en browserhost

Browserhosten ska:

- starta .NET-runtimen och ladda spelkärnan;
- ladda spritepaketet innan första spelbara frame;
- äga en återanvänd framebuffer;
- driva spelet med `requestAnimationFrame`;
- samla input och anropa ett enda steg per logisk frame;
- presentera färdig pixeldata på canvas;
- pausa när fliken tappar fokus eller dokumentet blir dolt.

Första versionen kan använda `ImageData` och `putImageData`. Om profilering visar för hög kopieringskostnad bör pixelbufferten laddas upp till en WebGL-textur med `texSubImage2D`. WebGL lämpar sig också väl för nearest-neighbour-skalning, letterboxing och fullscreen utan att spelrenderaren behöver känna till browserfönstrets storlek.

En frame är 400 x 280 x 4 byte, cirka 448 KB. Vid 60 FPS motsvarar det ungefär 27 MB pixeldata per sekund mellan WASM-minnet och canvaspresentationen. Det är normalt hanterbart på desktop, men ska mätas på svagare datorer och telefoner innan renderingsvägen låses.

### 3. Mappa input

Browserinput kan återanvända det befintliga knappbitfältet:

- piltangenter till rörelse;
- Enter till Start;
- befintliga A/B/C/X/Y/Z-bindningar;
- Q till snabbåtkomst för hälsoelixir;
- musposition och knappar till `RtsPointer`;
- developer save/load endast i utvecklingsbuildar.

Browserhosten måste dessutom:

- räkna om CSS-koordinater till spelets 400 x 280-koordinater;
- hindra piltangenter och Space från att scrolla sidan när spelet har fokus;
- hantera pointer capture vid drag;
- skilja på browsergenvägar och spelinput;
- ha en tydlig klickyta för fokus och ljudaktivering.

Gamepad och touch bör använda samma interna actions i ett senare steg, inte skapa separata gameplayvägar.

### 4. Gör spritepaketet browserladdningsbart

`SpritePack.LoadDefault` söker i dag efter `stormakt3020.wfsa` på det lokala filsystemet. Läsaren bör delas i två lager:

- `SpritePack.Load(Stream)` eller `SpritePack.Load(ReadOnlySpan<byte>)` som är plattformsoberoende;
- desktopkod som öppnar en fil;
- browserkod som hämtar paketet med HTTP/fetch.

Nuvarande spritepaket är cirka 18,8 MiB och cirka 9,2 MiB med gzip-komprimering. Det är acceptabelt för en första browserbuild. Servern måste leverera statisk Brotli- eller gzip-komprimering och korrekta cacheheaders.

På sikt kan paketet delas i ett gemensamt paket och ett paket per kampanjnivå. Det minskar tiden till meny och första spelbara nivå, men behövs inte för proof of concept.

PNG-källor, prompts, rågenereringar och andra utvecklingsassets ska inte publiceras. Endast runtimepaketet ska med i webbbygget.

### 5. Ersätt PipeWire-ljudet med Web Audio

Detta är portens största isolerade del.

`StormaktMusicLoop` gör i dag allt följande i C#:

- laddar PCM16-WAV-filer;
- håller flera hela musikspår i minnet;
- mixar musik, effekter och en aktiv radioröst;
- gör crossfade och ducking;
- producerar float stereo i en bakgrundstråd;
- skickar PCM-paket över Unix-socket till PipeWire-daemonen.

Spelkärnan bör i stället bero på exempelvis `IStormaktAudio`:

```text
PlayEffect(sound)
PlayVoice(voice)
SwitchMusic(track)
DuckMusic(milliseconds)
SetPaused(paused)
```

Desktopimplementationen kan kapsla in nuvarande `StormaktMusicLoop`. Browserimplementationen använder Web Audio:

- `AudioBufferSourceNode` eller mediaelement för musik;
- separata GainNodes för musik, röster och effekter;
- schemalagda gainramper för crossfade och ducking;
- cache av redan avkodade korta effekter;
- nivåvis hämtning av musik och röster.

Nuvarande runtime-WAV-material omfattar ungefär 182 MiB musik, 65 MiB röster och 8 MiB effekter. Det ska inte skickas som rå PCM till browsern. En webbexport bör skapa Opus, alternativt AAC där browserstöd kräver det, och ladda material per kampanjnivå. Det väntas minska nedladdningen kraftigt.

Browserns autoplayregler innebär att ljudmotorn måste startas eller återupptas efter en uttrycklig användarklickning. Menyn bör därför visa något i stil med `KLICKA FÖR ATT STARTA` innan första ljudet.

### 6. Abstrahera saves

Dungeon- och kampanjsaves är redan versionsmärkta JSON-strukturer. Formatet kan behållas.

Inför ett litet storagekontrakt, exempelvis:

```text
Exists(slot)
Load(slot)
Save(slot, json)
Backup(slot, json)
```

Desktop använder nuvarande filer under XDG state. Browserns första version kan använda `localStorage`, eftersom savefilerna är små och nuvarande spelkod förväntar sig synkrona operationer. En robust publik version kan använda IndexedDB och erbjuda export/import av save som JSON.

Save-schema och befintlig validering ska vara gemensamma mellan desktop och browser. Ett save skapat i browsern bör kunna exporteras och laddas av desktopversionen.

### 7. Flytta miljövariabler till hostkonfiguration

Följande typer av konfiguration bör skickas in när spelet skapas i stället för att läsas direkt från `Environment`:

- upplösning;
- developer mode;
- invincibility/testlägen;
- ljud aktiverat eller avstängt;
- startnivå och diagnostikflaggor.

Det gör spelkärnan renare och gör browserbuilden reproducerbar.

### 8. Bevara determinismen

Den seedade simulationen och tidigare WFEX-framehashar är en stor tillgång för porten. Browserarbetet bör använda inspelade inputsekvenser och jämföra framehashar mellan:

- nuvarande desktopkärna;
- den utbrutna delade kärnan;
- browser-WASM.

Den ensamma användningen av `Random.Shared` i dungeon-loot bör bytas till spelinstansens seedade RNG om helt identiska replayresultat ska garanteras.

Minimikrav före varje portmilstolpe:

- solution build för desktop;
- samma framehash för utvalda inputreplays;
- save/load roundtrip;
- meny, shooter, RTS och dungeon testade var för sig;
- inga browserfel eller växande allocationer under längre körning.

## Leveransstorlek och laddning

Repoets hela `assets/stormakt3020` är över 500 MiB, men merparten är källbilder, prompts och rått produktionsmaterial som inte ska publiceras.

En första faktisk webbleverans består ungefär av:

- .NET WebAssembly-runtime och trimmade assemblies;
- komprimerat spritepaket, omkring 9-10 MiB med nuvarande format;
- en liten boot-/menyljuduppsättning;
- webbkomprimerade assets för vald nivå;
- HTML, JavaScript och CSS-shell.

Musik, röster och större nivåresurser bör lazy-loadas. En service worker/PWA-cache kan läggas till efter att den vanliga statiska builden fungerar stabilt.

## Föreslagna milstolpar

### Milstolpe 0: reproducerbar baseline

- Dokumentera en kort native input-replay från meny till Stora Bält.
- Spara framehashar vid fasta frames.
- Lägg till minst en RTS- och dungeonreplay.
- Bekräfta vilka nuvarande ocommittade gameplayändringar som ska landa före portgrenen.

Resultat: en referens som avslöjar regressioner under refaktorn.

### Milstolpe 1: bild i browsern

- Installera `wasm-tools`.
- Skapa browserprojektet.
- Bryt ut minsta möjliga delade spelkärna.
- Ladda `stormakt3020.wfsa` över HTTP.
- Visa menyn på canvas.
- Kör Stora Bält med tangentbord, utan ljud och saves.
- Jämför framehashar med desktop.

Resultat: den första riktiga spelbara WASM-prototypen.

Uppskattning: 1-2 fokuserade arbetsdagar.

### Milstolpe 2: komplett input och saves

- Mus och pointer capture.
- RTS-kontroller.
- Dungeon-inventory och snabbåtkomst.
- localStorage-baserade saves.
- fullscreen, pause och fokusförlust.

Resultat: gameplaymässigt användbar browserbuild utan färdigt ljud.

Uppskattning: ytterligare 2-3 dagar.

### Milstolpe 3: browserljud

- `IStormaktAudio` och bibehållen desktopadapter.
- Web Audio-backend.
- konverteringssteg för Opus/AAC;
- musikbyte, crossfade, effekter, röster och ducking;
- autoplay/startoverlay;
- nivåvis laddning och felhantering.

Resultat: en komplett vertikal slice med samma presentation som desktop.

Uppskattning: ytterligare 2-4 dagar.

### Milstolpe 4: full kampanjparitet och publicering

- testa samtliga implementerade kampanjgrenar;
- Firefox, Chromium och Safari där tillgängligt;
- svagare dator och mobil prestandaprofil;
- gamepad/touch efter behov;
- loadingindikator, cache och offlinebeteende;
- statisk deploy och cacheheaders;
- kontroll av vilka assets och licenser som får distribueras publikt.

Resultat: en robust publik browserversion.

Total uppskattning från start:

- första WASM-probe: 1-2 dagar;
- spelbar vertikal slice: 4-7 dagar;
- desktop-browserparitet: cirka 7-12 fokuserade arbetsdagar;
- publik polish inklusive mobil och PWA: ungefär 2-4 veckor totalt.

Uppskattningen förutsätter att pågående gameplayarbete kan landas i tydliga checkpoints och att porten inte samtidigt används för en större omdesign av spelet.

## Risker

### Stor `Program.cs`

Den största kodrisken är att simulation, rendering, save-I/O och processhost ligger i samma fil. En för stor första refaktor skulle skapa onödiga mergekonflikter med pågående kampanjarbete. Lösningen är små adaptersteg med framehashverifiering efter varje steg.

### Ljudstorlek och minne

Att ladda nuvarande WAV-filer som float-arrayer skulle ge lång starttid och hög minnesanvändning. Webbkomprimering och lazy loading är ett krav, inte en senare optimering.

### Browserns huvudtråd

Första versionen kan köra simulation och presentation på huvudtråden. Om mätning visar frameproblem kan spelkärnan senare flyttas till en Web Worker eller använda WebAssembly-trådar. Det bör inte göras före en enkel single-threaded baseline, eftersom det komplicerar hostingheaders och felsökning.

### Autoplay och fokus

Ljud får inte förutsättas starta automatiskt. Input måste dessutom pausas eller nollställas när canvas tappar fokus, annars kan knappar fastna.

### Publik distribution

All klientkod och alla assets kan laddas ner av användaren. Inga hemligheter får finnas i browserbuilden. Asset- och ljudrättigheter behöver kontrolleras innan en offentlig deploy, även om en lokal teknisk prototyp fungerar.

## Rekommenderad första helgslice

Det bästa första målet är medvetet smalt:

> StorMakt-menyn och Stora Bält ska vara spelbara i Firefox genom WebAssembly, med tangentbord, korrekt canvas-skalning och samma framehashar som desktopversionen. Ljud och saves får vara avstängda i denna första slice.

Ordning:

1. Säkra eller avgränsa pågående gameplayändringar.
2. Skapa deterministisk native baseline.
3. Installera WebAssembly-workloaden.
4. Lägg till browserprojekt och statisk HTML/canvas-shell.
5. Gör `SpritePack` streambaserad.
6. Exponera en minimal `Create`, `Step` och `Render`-yta från C#.
7. Mappa tangentbord och presentera frames.
8. Kör samma replay native och i browsern.
9. Lämna ljud och storage bakom tydliga interface till nästa slice.

Denna ordning ger snabbt ett konkret bevis på att kärnan fungerar i browsern, samtidigt som de två största sidoprojekten - ljudpaketering och save-storage - hålls utanför den första tekniska risken.

