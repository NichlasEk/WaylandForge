# Bana 3 - Öresunds järnkrona

Status: pre-production plan locked 2026-07-14. This is the active `DEV` campaign row after Skånska skuggor became a public `STRID`.

## Dramatiskt löfte

Karl CCLV lämnar den svarta skogen och når Öresund: inte ett tomt sund, utan en väldig orbital ringbro som har vuxit runt jorden i lager av tullvalv, järnväg och gamla kustfästningar. Färden går tätt över bron. Spelaren ska känna att skeppet flyger genom en fungerande krigsmaskin snarare än över en stilla bakgrund.

Bron själv är banans motståndare. Klaffar stänger flygvägen, kronans pansartåg korsar skärmen och fyrbatterier låser korridorer. Sören Svartkrut återkommer som osäker tredje kraft. Han hjälper inte Karl av vänskap, men fogdens fullständiga kontroll över sundet vore värre för alla.

Speltid: cirka 4 minuter. Banan ska vara tätare och mer rytmisk än Skånska skuggor, men farliga brodelar måste alltid telegraferas innan de går in i spelarens körfält.

## Visuell riktning

1. **Nedre sundet, 0.12x:** mörkt jordhav, spridda kustljus och moln under ringbron.
2. **Ringkronan, 0.3x:** enorma järnbågar med blekt blått kronljus, försvinnande mot horisonten.
3. **Järnväg och tullverk, 0.65x:** parallella spår, stationsvalv, kablar, ångrör och röda danska signaler.
4. **Fysiskt brolager, 1.0x:** klaffar, laserfyrar, pansartåg och kanonplattformar som delar spelarens körfält.

Palett: kallt svartjärn, smutsig svenskblå patina, danskt signalrött och varmt mässingsljus. Farozoner använder vitt/rött; Sörens ingripanden behåller koppar och dämpat grönt.

Miljölandmärke: **Kronspannet**, en kronformad portal där tre järnbågar går samman. Den passerar bakom spelaren strax före bossen och delar sig sedan i de två fästena Helsingör och Helsingborg.

## Banstruktur

### 0:00-0:35 - Inflygning över sundet

- Ebba identifierar bron och varnar för att dess trafik fortfarande följer en trehundra år gammal tulltidtabell.
- Små brovakter kommer från rälsfickor i sidorna.
- En ensam laserfyr visar banans språk: smal vit söklinje, röd låsning, därefter fysisk stråle.

### 0:35-1:20 - Kronans järnväg

- Ett pansartåg löper parallellt med Karl CCLV och visar kanonvagnar en i taget.
- Loket är bakgrundsobjekt; vagnarnas torn och projektiler är de enda fysiska målen.
- Två varningssignaler föregår varje spårbyte så tåget aldrig hoppar oförklarligt mellan sidorna.

### 1:20-2:00 - Broklaffarna

- Klaffpar roterar in från vänster och höger och lämnar en tydlig, flyttande öppning.
- Sprickor, ånga och gul mekanisk markering visas före rörelsen.
- Ett förstörbart kontrollhus kan hålla en klaff öppen och skapa en säkrare men poängfattigare väg.

### 2:00-2:40 - Fyrarnas korseld

- Fyrlinser bygger växelvisa diagonala låsningar över bron.
- Danska tullslupar försöker driva spelaren in i strålarna med långsamma bredsidor.
- Sörens gröna signal syns i bakgrunden. Han skjuter bort ett fullt laddat batteri och öppnar mittkorridoren; ingripandet är en berättelsehändelse i första versionen, inte ett dolt moralval.

### 2:40-3:10 - Kronspannet

- Bron smalnar av genom den enorma kronportalen.
- Två tågburna kommandokanoner anfaller växelvis från varsin skärmkant.
- När båda förstörs bryts rälsen och avslöjar fästningarnas gemensamma energiledning.

### 3:10-4:00 - Boss: Helsingör / Helsingborg

- Två skilda brofästen anfaller från vänster och höger men delar en livsmätare: **ÖRESUNDS JÄRNKRONA**.
- **Fas 1 - Tvillingtullen:** Helsingör skjuter röda salvor medan Helsingborg flyttar pansarsköldar. Roller byts efter varje större attack.
- **Fas 2 - Sundet sluts:** fästena kör in mot mitten och skapar en föränderlig port. Spelaren skadar exponerade kronnoder under reträtten.
- **Fas 3 - Bruten krona:** den gemensamma ledningen slits av. Båda sidor skjuter osynkroniserat, bron spricker och Sörens kopparsalva hindrar ett sista dödligt korslås.
- Död: fästena kollapsar bort från mitten, inte över spelaren. Kronspannets översta båge slocknar och Karl CCLV fortsätter mot Silverkroppen.

## Fiender och mål

- **Broväktare:** smal rödvit jaktmaskin som lämnar rälsfickor i par.
- **Tullslup:** långsam sidofarkost med bredsida som styr spelaren snarare än jagar direkt.
- **Kanonvagn:** separat tågvapen med rekyl, skadad ruta och exploderande koppling.
- **Laserfyr:** fysisk linsplattform; söklinje och stråle är separata, deterministiska tillstånd.
- **Kontrollhus:** förstörbart markmål som ändrar nästa klaffsekvens, aldrig den klaff spelaren redan läser.

## Musik och ljud

- Banmusik: drivande järnvägsmarsch i 6/8 med stråkar, låg mässing, rälslik slagverkspuls och ett avlägset sorgset horn över sundet.
- Kronspannet lägger till orgel och klockslag utan att byta tempo.
- Bossvarianten delar grundpuls men svarar vänster/höger i stereobilden; viktig telegraph-SFX måste fortfarande vara tydliga i mono.
- Nya SFX: rälsväxel, klaffvarning, tung broled, lasersvep, tågrekyl, kronnod och avlägset brobrott.

## Radio och roller

- **Ebba Grip:** taktisk orientering och torr kommentar om tulltidtabellen.
- **Öresundsfogden:** dansk stationsröst, lugn och administrativ även när bron rasar.
- **Sören Svartkrut:** en kort svensk signal vid batteriingripandet; ingen vänskapsförklaring.
- All radio använder samma deterministiska röstseed per roll som resten av spelet och går genom den befintliga radiokön.

## Tekniska system

- Level id `2` får egen reset-seed, tidslinje, vågtabell, radio, musikval, bakgrund och resultatkort.
- Broklaffar är tidsstyrda hazards med separata telegraph-, rörelse- och vilolägen. Deras visuella geometri och kollisionsgeometri kommer från samma state.
- Pansartåget har en deterministisk spårkurva; endast kanonvagnarna skapar aktiva kollisionsobjekt.
- Helsingör/Helsingborg ägs av ett gemensamt boss-state med två renderankare och en delad hälsopool. Ingen av halvorna får använda den andra som lös sprite-offset.
- Sörens ingripande är en explicit tidslinjehändelse så det kan förgrenas senare utan att första skivan behöver kampanjval.

## Icke-mål för första byggskivan

- Ingen färdig bossgrafik eller full bosskamp.
- Ingen permanent Bana 2-moralflagga.
- Ingen fysiksimulering av en hel cirkulär ringbro.
- Inga otelegrapherade klaffkollisioner eller tåg som kan köra över spelaren som bakgrundsdekor.
- Ingen ny generell dataarkitektur innan nivå-id 2 har visat vilka nya fält bron faktiskt kräver.

## Byggskivor

1. **Hook och 60-sekundersskelett:** DEV-start, egen seed, titel, kall brofallback, musikplats, broväktarvåg och resultatkort.
2. **Ringbrovärlden:** genererad bakgrund, Kronspannet, rälsparallax, laserfyr och två säkra klaffsekvenser.
3. **Pansartåget:** lokpassage, kanonvagnar, spårbyte, skada och kopplingsdöd.
4. **Sörens ingripande:** radiokö, bakgrundspassage och förstört fyrbatteri.
5. **Helsingör/Helsingborg:** två ankare, delad hälsa, tre faser, död och övergång mot Silverkroppen.

## Acceptanskriterier

- Öresunds järnkrona kan startas som nivå-id `2` i utvecklarläge utan att återanvända Bana 1 eller Bana 2:s tidslinje.
- Samma seed och input ger samma tågspår, klafföppningar, fyrsalvor och radio.
- Varje fysisk klaff visar minst 45 bildsteg varning före körfältsintrång.
- Bakgrundståg, bågar och hav kan aldrig skada spelaren.
- Lasertelegraph, aktiva strålar, spelare och projektiler förblir läsbara i både 400x280 och 320x224.
- Bossens två halvor delar exakt en livsmätare men har oberoende attack- och renderankare.
- Radio överlappar inte bossintroduktion eller annan aktiv radioreplik.
- Alla nya genererade assets behåller källa, prompt och kodfallback och packas utan lösa grannfragment.
