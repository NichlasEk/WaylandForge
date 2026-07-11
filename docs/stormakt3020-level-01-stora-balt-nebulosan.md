# Bana 1 - Återtåget över Stora Bält-nebulosan

Status: checkpoint 1, 2A-2C, bosscheckpoint 3A-3C och den första grafikpolishen är klara. Bana 1 har nu spelbar genomflygning, tre bossfaser, resultatsekvens, genererad bakgrund och fysiska miljöassets; separat balans- och ljudpolish återstår.

## Wide field-checkpoint - 2026-07-11

- Standardupplösningen är 400x280, 25 procent större än prototypens 320x224 i båda riktningarna. Skepp/assets behåller sin pixelstorlek och upplevs därför mindre med mer spelrum runt sig.
- HUD, radio, titlar, bosskort och resultatkort är förankrade mot skärmkanter eller dynamiskt centrerade.
- Fiendeformationer, kedjeankare, bossens sidledspass och fas-3-rusning använder hela det bredare fältet.
- WFSA innehåller separata sömlösa 320- och 400-pixelsbakgrunder. `WAYLANDFORGE_STORMAKT_LEGACY_320=1` återställer prototypfältet för A/B-test.

## Implementerad grafikpolish - 2026-07-11

- Den kodritade himlen/nebulosan har ersatts av en genererad, hög vertikal Stora Bält-plåt som rullar långsamt bakom stjärnlagret. Mittkorridoren är avsiktligt mörk och lågkontrast.
- Brospann, skadat brospann, dansk brokanon och energinod är nu riktiga frilagda assets. Deras deterministiska kollisionsytor, hälsa och skottvarningar är oförändrade.
- Två matchande sönderslitna brovalv, ett svenskt blågult linjekryssarvrak och ett asteroid-/skrotfält ersätter de kodritade parallaxobjekten.
- Alla tidigare kodritade miljölager ligger kvar som fallback om WFSA-paketet eller ett enskilt namn saknas.

## Implementerad checkpoint 1 - 2026-07-10

- En 60-sekunders, ramstyrd flythrough loopar deterministiskt och använder fem deklarerade vågposter i stället för slumpmässig spawn.
- Titelplaketten visar `ÅTERTÅGET ÖVER` / `STORA BÄLT NEBULOSAN` före första fiendevågen.
- Fjärrymd/norrsken, turkos-roströd nebulosa, stjärnor och Bältruiner rör sig i separata hastigheter.
- Ruinlagret innehåller drivande sten, ett avlägset svenskt linjekryssarvrak och en sönderskjuten gravitationsbro märkt `3020`.
- Radiofönstren ligger i lugna fickor i bantidslinjen: Ebba vid 3 sekunder, Rasmus vid 15, Christian vid 23 och boss-Rasmus vid 55. Christians redan genererade engelska röst och neutral/talande kungaporträtt är nu kopplade till hans första förebådande av kronflottan.
- Reset återställer även slumpgenerator och stjärnfält. Två fristående 3 600-rutorskörningar med samma svepande eldinput gav samma slutbild: SHA-256 `71fe1ad83b88632b480bef546ff58d9ce086b384def2cf2e875e04e5787233b5`.

## Implementerad checkpoint 2A - första brohotet

- Tre brogrupper ligger i 60-sekunderstidslinjen och växlar sida för att skapa tydliga korridorer.
- Varje grupp består av ett skadebart brospann, ett marktorn och en separat pulserande energiledning.
- Marktornets första synliga låsstråle varar 48 simuleringsrutor, exakt 0,8 sekunder vid 60 Hz, före en riktad treskottssalva.
- Förstörd energiledning slår av det kopplade tornet; torn och brospann kan också förstöras direkt för olika poäng.
- Marktornens projektiler är en separat grupp med egen rörelse, spelarkollision och 90 rutors träffimmunitet så en treskottssalva inte tar alla liv samtidigt.
- Efter markhotet gav två nya fristående 3 600-rutorskörningar med identisk input samma slutbild: SHA-256 `45e237634c0ec1da712e909506eb6f97ef7248076c73ad55845ec5291310e960`.

## Implementerad checkpoint 2B - fogdeslupar och brokollaps

- Varje aktivt brospann eskorteras av två medeltunga rödvita fogdeslupar som håller position strax bakom markobjektets kollisionsyta.
- Spelarprojektiler träffar därför bron före de skyddade sluparna. När bron försvinner bryter sluparna formation och flyr ut åt motsatta sidor.
- Brospannet har hel, sprucken och kritisk skadebild. Kritisk metall får två tydliga glödande brottlinjer.
- Vid noll hälsa övergår hela brogruppen till en 45-rutors kollaps i stället för att försvinna direkt. Spann, torn och nod faller i olika sektioner med glödande splitter.
- Kollapsen använder befintlig bredsideknall som tillfälligt tungt ljud; ett eget brospricke- och kollapsljud återstår till polishskivan.
- Två fristående 3 600-rutorskörningar efter 2B gav samma slutbild: SHA-256 `58ef331d8281cb0024f626dd033c3b01edb58292ce1fd0954b221e9101dfa8aa`.

## Implementerad checkpoint 2C - bredsidekorridoren

- Den sista brogruppen i flythroughn består av två samtidiga spann från vänster och höger med egna torn, noder och fogdeeskorter.
- Mitten lämnas öppen som säker korridor, medan bredsidan kan träffa båda markgruppernas inre komponenter.
- Brogrupperna har separata länknings-id:n: spelaren kan fälla ena sidan, öppna ett bredare flyktutrymme och lämna andra tornet aktivt.
- Två fristående 3 600-rutorskörningar efter 2C gav samma slutbild: SHA-256 `55d01afcc9d2384d7bd7442a81055423086ec74015caf1ad9b819e8962da01f1`.

## Implementerad checkpoint 3A - Kronens Tiende, fas 1

- Vanliga vågor tystnar före ruta 3 300. Fogde Rasmus anropar, därefter glider `KRONENS TIENDE - FOGDESKEPP` in med låst skrov under radio och namnskylt.
- Bossmodellen är en bred rödvit indrivningsgaljon med korspaneler, kedjor, tredubbel krona, mekaniskt tullsigill och två separata sidokanoner.
- Varje sidokanon har 70 egen hälsa, kan förstöras separat och försvinner då ur kommande salvor.
- Fas 1 skjuter två femkulors solfjädrar med avsiktliga luckor. Ett kedjeankare markerar sin lodräta kolumn i 48 rutor innan det dras upp från skärmens nederkant.
- Skrovets fas-1-segment går från 100 till 65 procent. Vid tröskeln rensas aktiva salvor och ankare och övergångsskylten `BROFOGDENS VREDE` visas; fas 2 är ännu passiv.
- `WAYLANDFORGE_STORMAKT_INVINCIBLE=1` finns endast som opt-in för deterministiska fullbanecaptures. Normal körning påverkas inte.
- Två 4 251-rutors bosskörningar med samma testinput gav samma fas-2-övergångsbild: SHA-256 `398f461fbe5c42911dd1cd41858ff3763104693916d06b006fdc6503a0fca246`.
- Bossinträdet begär en halvsekunds korsfade till bossloopen; stage-reset korsfadar tillbaka till normal stridsmusik.
- Bossmusiken använder nu den tystnadsfria `kronans-sista-salva-loop-v2.wav`: 40 sekunder vid en exakt 84 BPM-taktgräns, med v1 kvar som orörd källa och fallback.
- Kronens Tiendes intakta och skadade fas, fogdesluparnas två lägen och tullsigilldrönaren använder nu ett separat AI-genererat 16-bitars spriteark. Kodgrafiken finns kvar som fallback och hitboxarna är fortsatt frikopplade från bildytan.
- Två 4 251-rutors körningar med det nya arket gav samma fas-2-bild: SHA-256 `a08c9ac3c424c0c7cd7500148d9b58311b7d9d64703654edecdcb98e5b92b1a9`. En ombyggnad av WFSA-paketet gav reproducerbart SHA-256 `c76414d0c565ae93400aafb7042f526d5e7f1285b52ecf4fdd4983cb5fa60165`.

## Implementerad checkpoint 3B - Brofogdens vrede, fas 2

- Det öppnade tullsigillet skjuter tre koncentriska guldringar per 300-rutorscykel. Varje ring utelämnar samma tydliga sektor och sektorn roterar mellan cyklerna.
- Överlevande sidokanoner skjuter tre riktade vita salvor mellan ringarna. Förstörda fas-1-kanoner förblir utslagna och bidrar inte.
- Två små rödvita brotorn dockar utanför bossens sidor med 50 egen hälsa vardera, separata träffytor, skadebild och poäng.
- Kärnan visar kall blågrå sköld när den är stängd och orange sigill när den är sårbar. Med docktornen kvar är skadefönstret 60 rutor; när båda förstörts blir det 160 rutor.
- Fas-2-segmentet går från 65 till 25 procent. Vid tröskeln rensas projektilfältet och `TIONDET BRISTER` visas; fas 3 är ännu passiv.
- Två fristående 4 601-rutorskörningar gav samma aktiva fas-2-bild: SHA-256 `4e4a8f395997fe889f610b3f31314b34fe624058779a0f892140bddc0b1037d8`. En längre 6 201-rutorskörning nådde den avsedda fas-3-skylten.

## Implementerad checkpoint 3C - Tiondet brister, fas 3 och död

- Efter `TIONDET BRISTER` är järnkärnan permanent sårbar och bossen får rött nödljus över det genererade skadade spritearket.
- Två vertikalrusningar föregås av 48-rutors dubbla röda varningslinjer. Under rusningen slits mörka, glödande vrakdelar ut mot skärmkanterna.
- Sigillet skjuter en permanent långsam tvåarmad orange spiral. Fas-3-skadan är balanserad för att en träffsäker dubbelkanon ska hinna uppleva båda rusningarna före noll hälsa.
- Dödsekvensen spränger vänsterkanon, högerkanon, pansar, sigill och kärna i tidsordning. Bossmusiken duckas till cirka en tredjedel under den 2,6 sekunder långa huvudbrisningen.
- Efter 210 dödsrutor tas bossens träffytor bort och resultatkortet visar `BÄLTET ÄR ÖPPET` / `KRONARKIV SÄKRAT`.
- En liten svart/kopparfärgad snapphane med grön signalglöd betraktar vrakplatsen ordlöst. Start återställer hela den deterministiska banan och stridsmusiken.
- Två fristående 6 501-rutorskörningar gav samma aktiva fas-3-bild: SHA-256 `6bbeb1153f48e0312ccaeb815d3fd146e4ec6bccf9429a644b5df2136a5219dc`. En 6 901-rutorskörning nådde det färdiga resultatkortet.

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

1. **Banskelett:** klart - ramstyrd tidslinje, titel, tre parallaxlager och deterministisk 60-sekunders genomflygning.
2. **Brokriget:** marklager, torn, energiledningar, förstörbara brospann och vågor fram till 2:45. Commit och push.
3. **Fogdeskeppet:** klart - modell, presentation, delmål, bossmusik, tre faser och dödssekvens.
4. **Polish:** bakgrundsassets, skadebilder, nya SFX, poängbalans och full fyraminuters capture. Commit och push.

## Acceptanskriterier

- Samma input ger samma fiender, skott och bossmönster mellan körningar.
- `ÅTERTÅGET`, `ÖVER`, `BÄLT`, `POÄNG` och `BÄLTET ÄR ÖPPET` renderas med riktiga ÅÄÖ-glyfer.
- Luftfiender, markhot och bakgrund går att skilja i både 400x280-standardcapture och 320x224-legacycapture utan att pausa.
- Inget torn skjuter utan minst 0.8 sekunders visuell förvarning första gången det introduceras.
- Bossen når alla tre faser, ändrar mönster när sidokanoner förstörts och kan besegras utan träff med ett fast testinput.
- Musikbyten lämnar inga gamla PCM-paket i kön och SFX mixas utan samplevärden över ±0.98.
- Hela banan kan köras headless från start till resultattext och producera en verifierbar slutbild/hash.
