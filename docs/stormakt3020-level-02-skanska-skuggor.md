# Bana 2 - Skånska skuggor

Status: implementation underway. A deterministic 60-second gameplay skeleton is selectable in developer mode. The shared technical spine is maintained in `docs/stormakt3020-level-hook-map.md`; keep that map synchronized as level hooks land.

The generated starless black-forest mining plate is active in both 400x280 and 320x224 paths. Code-drawn crystal-tree scenery now appears only as a missing-WFSA fallback.

Current identity slice: Sören Svartkrut has matched generated neutral/speaking radio portraits and his first transmission at frame 900. Level 2 suppresses the ordinary star layer; HUD bars render above the player and hazards.

Sören's generated three-state corsair crosses the background without collision between frames 540 and 780, foreshadowing the radio contact.

Current audio slice: `skanska-skuggor-loop-v1.wav` is the dedicated runtime score, selected through the level id and isolated from Stora Bält combat music.

Current world slice: complete. Three deterministic copper/green signal beacons enter during the 60-second skeleton. They are destructible physical targets with readable code-drawn fallback art. Dark mist drones reveal their silhouette around a green aimed shot, while Danish red/white cargo barges establish the fogde convoy with a slower aimed volley. Transparent generated crystal-pine, kiln-moon and mining-hoist props cross the foreground edges without obscuring the play lane.

Current dialogue slice: four Swedish pilot lines for Ebba and Sören are generated through synthetic VoxCPM2 casting references and Dots MF line rendering, then passed through the deterministic runtime radio filter. Sören transmissions use their own copper/green frame rather than Danish red/white.

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
4. **Miniboss:** två läsbara faser och avbruten duell. Commit och push.
5. **Glimminge Järn:** presentation, två faser, död och resultat. Commit och push.

## Acceptanskriterier

- Level select visar båda banorna och kan aldrig starta fel ban-id.
- Samma bana, seed och input ger samma fiender, dialog och slutbild.
- Svart/koppar/grönt går att skilja från både svensk blå/guld och dansk röd/vit i 400x280.
- Signalstörning döljer aldrig spelarens hitbox, farliga projektiler eller pauskort.
- Sören lämnar minibossduellen reproducerbart utan att räknas som dödad.
- Varje ny grafik- och ljudgeneration har versionerad källa/prompt och runtimefallback.
