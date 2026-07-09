# Bana 1 - Återtåget över Stora Bält-nebulosan

Status: planerad, inte implementerad.

## Banans löfte

Karl CCLV retirerar genom Stora Bält-nebulosan med kronarkivet ombord. Den gamla gravitationsbron är sönderskjuten men fortfarande bemannad av danska tullautomater. Spelaren ska känna att hen flyr genom ruinerna av ett större slag, återfår initiativet och till sist vänder sig mot fogdeskeppet som jagat flottan.

Speltid: cirka 4 minuter inklusive boss. Bana 1 ska fungera som kampanjens vertikala snitt och introducera rörelse, dubbelkanon, bredsida, markhot, förstörbara brodelar och bossfaser utan separat handledningsruta.

## Visuell riktning

### Parallaxlager

1. **Fjärrymd, 0.15x:** kall mörkblå rymd, långsamma stjärnor och ett blekt norrskensband.
2. **Nebulosa, 0.35x:** sorgligt turkosgrå gas med roströda sår efter slaget; låg kontrast bakom projektilfältet.
3. **Bältruiner, 0.75x:** drivande sten, skeppsvrak, trasiga rälssektioner och flaggbojar som passerar utanför spelarens kärnyta.
4. **Bro/mark, 1.0x:** stora segment av en barock gravitationsbro, kanontorn och energiledningar som läses som markobjekt.

Palett: marinblå, sotgrå, kall turkos och koppar. Danska hot använder rött/vitt endast i lokala paneler så projektiler fortfarande syns. Bakgrunden får inte använda spelarprojektilernas klara cyan eller gula kärnfärg i hög intensitet.

### Landmärken

- En bruten brobåge med årtalet `3020` och ett slocknat kronur.
- Ett vrak av en svensk linjekryssare i bakgrunden; inga kollisionsytor.
- Tre brospann som går att skjuta sönder för poäng och för att stänga av tillhörande torn.
- En enorm tullfyr som tänds strax före bossens ankomst.

## Banstruktur

### 0:00-0:30 - Efter slaget

- Musik: `marsch-mot-kopenhamn-v1.wav` i låg nivå eller en framtida särskild reträttvariant.
- Deploy-signal och bantitel: `ÅTERTÅGET ÖVER STORA BÄLT-NEBULOSAN`.
- Svensk radio, vänster: Ebba Grip - `KARL CCLV` / `HÅLL KURSEN.`
- Två långsamma danska spanardrönarpar lär spelaren dubbelkanonen.
- Ett tomt, tydligt brosegment visar vad som räknas som mark.

### 0:30-1:20 - Tullbojarna

- Musik växlar till `stormakt-over-oresund-v1.wav` med kort korsfade.
- Röda/vita tullbojar kommer i alternerande V-formationer.
- Dansk radio, höger före första tornet: Rasmus Gyldentold - `SVENSKE FARTØJ` / `LÆG BI.`
- Första marktornet låser med en rödvit ljuskägla i 0.8 sekunder och skjuter sedan tre långsamma kulor.
- Tornets energiledning blinkar på bron. Förstör ledningen och tornet slocknar; förstör tornet direkt för högre risk och poäng.

### 1:20-2:05 - Den fallande bron

- Tre brospann fyller halva spelbredden och skapar växlande säkra korridorer.
- Varje spann har två skadebilder: sprucket och kritiskt. Efter förstörelse faller det bakåt ur planet, inte över spelaren.
- Små fogdeslupar använder brodelarna som skydd och bryter formation när deras spann faller.
- Bredsidan introduceras naturligt genom två samtidiga torn på motsatta sidor.

### 2:05-2:45 - Fogdens uppmarsch

- Bakgrunden öppnar sig; fler vrak men färre marksegment.
- En elitvåg med två vaktskepp och ett tungt tullsigill anfaller.
- Tullfyren sveper över skärmen. Svepet är visuellt, men markerar bossens kommande inflygningslinje.
- Sista tio sekunderna tunnas fienderna ut och musiken får andas före bossbytet.
- Fogderadio, höger: `KRONENS TIENDE` / `TAGER ALT.`

### 2:45-4:00 - Boss: Kronens Tiende

Fogdeskeppet är en bred rödvit indrivningsgaljon med ett enormt vitt kors, guldkant, kedjor och ett mekaniskt sigill i fören. Namnskylt: `KRONENS TIENDE - FOGDESKEPP`.

Musik: `kronans-sista-salva-v1.wav`, startad vid bossnamnet med 0.5 sekunders korsfade.

Bossradion visas färdigt före första skottmönstret och följer reglerna i `docs/stormakt3020-radio-dialogue-plan.md`.

#### Fas 1 - Indrivningen, 100-65 procent

- Skeppet går i långsamma sidledspass.
- Två tullkanoner skjuter solfjädrar med tydliga luckor.
- Kedjeankare markeras på marken och drar sig därefter uppåt genom spelplanen.
- Kanonerna kan förstöras separat och förändrar fas 2.

#### Fas 2 - Brofogdens vrede, 65-25 procent

- Sigillet öppnas och avfyrar tre koncentriska ringar; en sektor saknas alltid.
- Överlevande sidokanoner skjuter riktade salvor mellan ringarna.
- Två små brotorn dockar till bossen. Förstör dem för att öppna en längre skadeperiod.

#### Fas 3 - Tiondet brister, 25-0 procent

- Rött nödljus, bortslitna vita paneler och synlig mörk järnkärna.
- Bossen rusar vertikalt två gånger med tydlig förvarning och lämnar brinnande vrakdelar som faller ut mot kanterna.
- Sigillet blir permanent öppet och sårbart, men skjuter en tät långsam spiral.

#### Död

- Kanoner, torn och sigill exploderar i ordning innan huvudskrovet brister.
- Musiken duckas under den sista explosionen.
- Resultattext: `BÄLTET ÄR ÖPPET`.
- En svart/kopparfärgad liten kapare betraktar vraket i bakgrunden: första ordlösa antydan om framtidens snapphane.

## Nya fiender och miljöobjekt

- `Tullboj`: lätt flygande fiende, V-formation.
- `Fogdeslup`: medeltung, söker skydd nära brosegment.
- `Tullsigill`: tung elitdrönare med ringprojektiler.
- `Brotorn`: markbundet, telegrapherat riktat skott.
- `Energiledning`: förstörbar marknod kopplad till ett torn.
- `Brospann`: förstörbart miljöobjekt med tre visuella tillstånd.
- `Kronens Tiende`: boss med separata delmål och tre faser.

## Tekniska system som bana 1 får införa

- En deterministisk bantidslinje driven av simuleringsrutor, inte väggtid.
- Dataposter för fiendevågor, markobjekt, musikbyten och titeltexter.
- Minst tre parallaxlager plus ett marklager.
- Separata projektilgrupper för spelare, luftfiender och marktorn.
- Hälsa och skadebilder för markobjekt.
- Bossdelar med egen hälsa och fasberoende skottmönster.
- Ljudhändelser för tornskott, brospricka, brokollaps, bossvarning och bossdöd.

## Icke-mål för första byggpasset

- Ingen förgrenad rutt.
- Ingen permanent uppgraderingsmeny.
- Ingen full dialogscen eller dynamisk/runtimegenererad TTS; bana 1 använder endast korta förgenererade radiokort när radioprototypen är godkänd.
- Ingen full kampanjlagring.
- Ingen adaptiv musik utöver tydliga spårbyten och korsfade.

## Byggskivor och checkpoints

1. **Banskelett:** ramstyrd tidslinje, titel, tre parallaxlager och deterministisk 60-sekunders genomflygning. Commit och push.
2. **Brokriget:** marklager, torn, energiledningar, förstörbara brospann och vågor fram till 2:45. Commit och push.
3. **Fogdeskeppet:** bossmodell, tre faser, delmål, bossmusik och dödssekvens. Commit och push.
4. **Polish:** bakgrundsassets, skadebilder, nya SFX, poängbalans och full fyraminuters capture. Commit och push.

## Acceptanskriterier

- Samma input ger samma fiender, skott och bossmönster mellan körningar.
- `ÅTERTÅGET`, `ÖVER`, `BÄLT`, `POÄNG` och `BÄLTET ÄR ÖPPET` renderas med riktiga ÅÄÖ-glyfer.
- Luftfiender, markhot och bakgrund går att skilja i en 320x224-capture utan att pausa.
- Inget torn skjuter utan minst 0.8 sekunders visuell förvarning första gången det introduceras.
- Bossen når alla tre faser, ändrar mönster när sidokanoner förstörts och kan besegras utan träff med ett fast testinput.
- Musikbyten lämnar inga gamla PCM-paket i kön och SFX mixas utan samplevärden över ±0.98.
- Hela banan kan köras headless från start till resultattext och producera en verifierbar slutbild/hash.
