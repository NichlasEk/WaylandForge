# Bana 2 - Skånska skuggor

Status: complete deterministic gameplay pass and first graphics/audio polish pass are public and marked `STRID`. Remaining work is balance and later campaign integration, not a progression blocker. The shared technical spine is maintained in `docs/stormakt3020-level-hook-map.md`; keep that map synchronized as level hooks land.

## Grafik- och ljudpolish - 2026-07-21

Bana 2 har nu en egen svit med 16 effekter. Sex ofta upprepade vapenljud - dimpuls, kopparsalva, konvojpuls, järnpuls, borrpuls och glödpuls - är korta deterministiska syntpulser med fallande tonhöjd. De använder inte modellgenererat brus och kan därför inte staplas till det tidigare motorsågsljudet. Karl CCLV återanvänder samma två rena spelar-piows som på Bana 1 eftersom skeppets kanon är densamma.

Tio fasta Stable Audio 3-klipp ger engångshändelserna egen identitet: signalfyrens brott, Sörens ankomst och reträtt, Glimminges varning, järnkorparnas start, järnväggen, kristallspjutets varning, fasbrottet, brandläget och bossdöden. Alla runtimefiler är 48 kHz stereo PCM16, tonade i slutet och regenererbara med `tools/stormakt3020/generate_skanska_sfx.py` och fasta seeds `3020201`-`3020216`.

Den grafiska passagen ersätter det sista enkla spjutkorset med en packad koppar/grön alfamarkör, gör Sörens två radarekon genomskinliga och lägger trestegs packade glöd- respektive järnbrott över Glimminges brand- och dödsövergångar. Sju nya små RGBA-effekter byggs deterministiskt in i WFSA-paketet av `tools/stormakt3020/build_assets.py`; den långa vertikala spjutlinjen är kvar som säkerhetstelegraf.

Åtta neutrala kontrollrutor behöll sina tidigare pixelhashar i både 400x280 och 320x224. Ett komplett invincible testpass med kontinuerlig eld nådde resultatkortet två gånger med samma wide-hash `c0b40317e42237975af95d1482c81de62c9428cc44f9edf1f621dc56dea47285`; motsvarande legacy-slutbild gav `598896bd2f1964ff1f454b5271916180ae9939b686cd4c7d0b3cbdd0abf6ccf0`. Runtime laddade 92 effekter utan fallback.

The generated starless black-forest mining plate is active in both 400x280 and 320x224 paths. Code-drawn crystal-tree scenery now appears only as a missing-WFSA fallback.

Current identity slice: Sören Svartkrut has matched generated neutral/speaking radio portraits and his first transmission at frame 900. Level 2 suppresses the ordinary star layer; HUD bars render above the player and hazards.

Sören's generated three-state corsair crosses the background without collision between frames 540 and 780, foreshadowing the radio contact.

Current audio slice: `skanska-skuggor-loop-v1.wav` is the dedicated runtime score, selected through the level id and isolated from Stora Bält combat music.

Current world slice: complete. Three deterministic copper/green signal beacons enter during the opening skeleton with generated intact/damaged physical sprites and code fallback. Dark mist drones remain fully hidden when generated assets are present, reveal their real sprite around a green aimed shot and leave no placeholder fragments. Generated intact/damaged Danish red/white cargo barges establish the fogde convoy with a slower aimed volley. Transparent crystal-pine, kiln-moon and mining-hoist props cross the foreground edges without obscuring the play lane.

Current dialogue slice: four Swedish pilot lines for Ebba and Sören are generated through synthetic VoxCPM2 casting references and Dots MF line rendering, then passed through the deterministic runtime radio filter. Sören transmissions use their own copper/green frame rather than Danish red/white.

Current rival slice: complete for the 60-second skeleton. Sören enters at frame 2700 with lateral boost dashes and aimed copper fire, shifts to two non-physical green radar decoys and paired salvos, then disengages at 35 percent health or the deterministic fogde interruption at frame 3420. Interruption never awards a kill and clears the rival volley before his damaged escape.

Current boss slice: Glimminge Järn enters after Sören at frame 3600 under **Kommandør Birgitte Bille**, Ebba Grip's broad-built, iron-voiced Danish counterpart. Her matched neutral/shouting portrait and three locally rendered Danish lines drive intro, drill deployment and defeat. The 720-health fight visibly braces coherent red/white shield wings that block frontal damage and drives iron-wall volleys with moving safe gaps. Animated Glimminge iron-raven escorts launch throughout the fight, bank into aimed fire and retreat in a damaged state. At 420 health the boss unfolds twin drill turrets, adds aimed heavy fire and telegraphed crystal spears. Below 210 health the ship switches to a generated burning state with denser escorts, faster drills, spear cadence and radial ember bursts. Death uses a connected generated castle wreck for 300 frames instead of floating block placeholders, awards the boss score, then shows the level-specific result card with one green snapphane blink.

Boss presentation continuity: content-aware sprite crops preserve both Glimminge side sections and complete Danish shield wings. Shield deployment, phase-two damage/drill deployment, the burning threshold and wreck collapse crossfade in the same locked boss center. Phase-two lateral movement eases from the exact phase-one X position instead of jumping to a new sine curve.

## Dramatiskt löfte

Efter reträtten över Stora Bält söker Karl CCLV reservdelar i ett mörkt asteroidbälte av kristallgranar, gruvmånar och glödande kolmilor. Danska fogdekonvojer plundrar samma område, men en tredje svart/kopparfärgad styrka slår mot båda sidor. Spelaren möter framtidens snapphane **Sören Svartkrut**: först störsändare och kapare, sedan miniboss och till sist en motvillig möjlig allierad.

Speltid: cirka 4 minuter. Bana 2 ska introducera signalstörning, maskerade minor och en snabb rivalduell utan att ännu införa kampanjval eller permanent uppgradering.

## Visuell riktning

1. **Fjärrymd, 0.15x:** svartblå rymd, djuprött norrsken och avlägsna gruvljus.
2. **Skogsasteroider, 0.35x:** höga kristallformationer som läser som mörka granar, med kalla dimstråk mellan dem.
3. **Kolmilor och gruvvrak, 0.75x:** små månar med kopparglöd, brutna hissverk och övergivna malmskepp.
4. **Fysiskt lager, 1.0x:** förstörbara gruvbryggor, signalfyrar, järnportar och marktorn inbyggda i asteroidytor.

Palett: nästan svart järn, oxiderad koppar, dämpat skogsgrönt och signalgrön glöd. Danska konvojer behåller rött/vitt. Karl CCLV förblir blå/guld och får aldrig blandas ihop med snapphanarna.

## Banstruktur

### 0:00-0:35 - Den svarta skogen

- Ebba Grip varnar för okända signaler.
- Lätta dimdrönare lär spelaren att vissa silhuetter syns först när de skjuter.
- Första snapphaneskeppet passerar ordlöst i bakgrunden och lämnar grön störning.

### 0:35-1:25 - Fogdekonvojen

- Rödvita lastpråmar eskorteras av kanonslupar.
- Förstör eskort eller lastlås; beslagtaget gods faller ut som tillfälliga sköldfragment.
- Sören bryter in på videokom och anklagar båda flottorna för att stjäla från bygden.

### 1:25-2:10 - Kolmilefältet

- Marktorn sitter på små roterande gruvmånar.
- Kopparminor är mörka tills en grön tändlinje telegrapherar kedjereaktionen.
- Snapphanedrönare skjuter sönder ett fogdetorn men lämnar spelaren att hantera splitterfältet.

### 2:10-2:45 - Glimminge järns förtrupp

- Danska pansarvakter pressar spelaren genom två kristallkorridorer.
- Signalfyrar kan förstöras för att minska störningen i HUD/radio.
- Sören kräver att Karl CCLV lämnar kronarkivet och går in som miniboss.

### 2:45-3:20 - Miniboss: Sören Svartkrut

- Snabb svart/kopparfärgad kapare med grön signalglöd.
- Fas 1: sidledsdash med tydliga gröna efterbilder och korta kopparsalvor.
- Fas 2: två falska radarsilhuetter; endast den riktiga skapar fysisk mynningsflamma.
- Vid 35 procent avbryts duellen av fogdegaljonen. Sören flyr men skjuter sönder dess första kanon.

### 3:20-4:00 - Boss: Glimminge Järn

- Självgående fogdegaljon byggd av mörkt slottsjärn, rödvita sköldar och gruvmaskineri.
- Fas 1: mursektioner och riktade järnkulor.
- Fas 2: fäller ut två borrtorn och drar kristallspjut genom spelplanen.
- Död: rustningen faller som tunga slottsblock; en grön snapphanesignal blinkar en gång innan resultatkortet.

## Nya assets

- Sörens kapare: neutral, boost/dash och skadad ruta.
- Sören Svartkrut videokom: neutral och talande.
- Snapphanedrönare, falsk radarsilhuett och grön efterbild.
- Skogsasteroidbakgrund, kristallgranar, kolmilor, gruvmånar och signalfyrar.
- Glimminge Järn: hel/skadad, borrtorn och slottsvrak.

## Musik och ljud

- Banmusik: sorgmättad marsch med mörka stråkar, låg mässing och torra kopparslag.
- Sörens motiv lägger till ojämn handtrumma, metallisk nyckelharpa och kort grönsignal-liknande synth.
- Minibossmusik ska kunna korsfada in i Glimminge Järn utan tystnad.
- Nya SFX: störpuls, kapardash, kopparsalva, minlänk, kristallspricka och tungt slottsblock.

## Tekniska system

- Level select med låst/upplåst status.
- Ban-id separerat från reset så en vald bana kan startas deterministiskt.
- Kort deterministisk signalstörning som påverkar presentation men aldrig input eller hitboxar.
- Miniboss som kan avbrytas och lämna banan utan dödssekvens.

## Icke-mål för första byggskivan

- Inga permanenta val eller sparfiler.
- Ingen full vän/fiende-förgrening för Sören.
- Ingen dynamisk TTS i runtime.
- Ingen färdig Glimminge Järn-boss innan banans 60-sekundersskelett är verifierat.

## Byggskivor

1. **Level select och plan:** två synliga banposter, bana 1 startbar, bana 2 märkt under byggnad. Commit och push.
2. **Snapphaneidentitet:** Sörens kapare/porträtt, första radio och ordlös bakgrundspassage. Commit och push.
3. **Banskelett:** egen bakgrund, 60-sekunderstidslinje, konvoj och signalfyrar. **Landed 2026-07-11.**
4. **Miniboss:** två läsbara faser och avbruten duell. **Landed 2026-07-11.**
5. **Glimminge Järn:** presentation, två faser, död och resultat. **Landed 2026-07-11.**

## Acceptanskriterier

- Level select visar båda banorna och kan aldrig starta fel ban-id.
- Samma bana, seed och input ger samma fiender, dialog och slutbild.
- Svart/koppar/grönt går att skilja från både svensk blå/guld och dansk röd/vit i 400x280.
- Signalstörning döljer aldrig spelarens hitbox, farliga projektiler eller pauskort.
- Generated level-2 actors, hazards and projectiles leave no loose pixels or code-placeholder fragments; only safety telegraphs and HUD markers remain primitive-drawn.
- Sören lämnar minibossduellen reproducerbart utan att räknas som dödad.
- Varje ny grafik- och ljudgeneration har versionerad källa/prompt och runtimefallback.
