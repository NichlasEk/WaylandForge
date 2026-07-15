# Bana 3 - Öresunds järnkrona

Status: checkpoints 1-2 landed 2026-07-15. The active `DEV` row starts a deterministic 60-second level-id 2 skeleton containing the first three-way systemic bridge section.

## Dramatiskt löfte

Karl CCLV lämnar den svarta skogen och når Öresund: inte ett tomt sund, utan en väldig orbital ringbro som har vuxit runt jorden i lager av tullvalv, järnväg och gamla kustfästningar. Färden går tätt över bron. Spelaren ska känna att skeppet flyger genom en fungerande krigsmaskin snarare än över en stilla bakgrund.

Bron själv är banans motståndare. Klaffar stänger flygvägen, kronans pansartåg korsar skärmen och fyrbatterier låser korridorer. Sören Svartkrut återkommer som osäker tredje kraft. Han hjälper inte Karl av vänskap, men fogdens fullständiga kontroll över sundet vore värre för alla.

Speltid: cirka 4 minuter. Banan ska vara tätare och mer rytmisk än Skånska skuggor, men farliga brodelar måste alltid telegraferas innan de går in i spelarens körfält.

Det avgörande designkravet är **läsbar emergens**. Bron är ett sammankopplat maskineri, inte en rad kulisser som alltid spelar samma film. Spelaren får påverka nästa lokala situation genom vad som förstörs, lämnas vid liv eller används som skydd. Följderna är deterministiska och synliga i världen; banan har inga hemliga moralpoäng.

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
- Sörens gröna signal syns i bakgrunden. Han skjuter bort det farligaste fortfarande aktiva brosystemet: ett laddat fyrbatteri, en kommandovagn eller ett låst klaffverk. Valet avgörs av synlig lokal hotnivå, inte av en dold moralflagga.

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

## Systemisk emergens

### Tre gemensamma språk

Alla emergenta möten byggs av högst tre tillstånd som spelaren kan läsa direkt i bild:

1. **Ström:** blåvit kabelglöd visar vilka laserfyrar, klaffmotorer och kronnoder som får kraft. En förstörd nod släcker eller överbelastar sina synligt anslutna konsumenter.
2. **Spårläge:** gula växellampor visar tågets kommande körväg. Ett kontrollhus kan växla nästa vagn, men aldrig teleportera ett tåg som redan är på skärmen.
3. **Täckning:** klaffar, vagnar och sköldar stoppar både spelarens och fiendens direkta eld. Aktiv laser skär däremot igenom vanlig täckning efter sin långa varning.

Det finns ingen allmän fysiksandlåda. Dessa tre språk är den fullständiga kontraktsytan och återanvänds genom hela banan och bossen.

### Tillåtna korsreaktioner

| Orsak | Synlig följd | Spelarens möjlighet |
|---|---|---|
| En kanonvagns koppling skjuts sönder | Vagnen fortsätter fritt på aktuellt spår tills nästa stopp | Låt den krascha i ett kontrollhus eller förstör den säkert för poäng |
| En laser träffar en vagn eller tullslup | Målet tar kraftig skada och strålen bryts kort av explosionen | Locka fienden över den markerade strållinjen |
| En klaff stängs mellan Karl och ett batteri | Klaffen absorberar vanlig eld från båda håll | Använd den som temporärt skydd eller öppna den via kontrollhuset |
| En överlastad kraftnod förstörs | Pulsen färdas längs synligt glödande kabel till nästa anslutna system | Släck en fyr säkert eller överladda den så den skjuter genom egna led |
| Ett kontrollhus förstörs före nästa växelsignal | Växeln låses i den visade reservriktningen | Välj enklare flygkorridor eller skicka tåget mot ett annat brosystem |
| Sören anländer | Han slår ut högsta kvarvarande hotklass, med grön sikteslinje före skottet | Spelarens tidigare handlingar avgör vad som finns kvar att rädda dem från |

Fiendeskott får inte slumpmässigt förstöra viktiga kontrollmål. Endast namngivna tunga salvor, aktiva lasrar, fria vagnar och spelarvapen deltar i korsreaktionerna. Det gör följderna reproducerbara och avsiktliga.

### Lokal minnesregel

Varje brosektion minns högst två spelarorsakade förändringar tills den lämnar skärmen, exempelvis `växel låst vänster` och `laser överlastad`. Tillstånd får påverka nästa möte inom samma sektion men kedjas inte genom hela banan. På så vis känns bron reaktiv utan att fyra minuters tidiga val gör bossen obegriplig.

### Poäng utan rätt svar

- Direkt förstörelse ger säker grundpoäng.
- Miljödöd ger högre **LIST**-bonus eftersom spelaren tog risken att arrangera korsreaktionen.
- Att bevara ett kontrollhus kan öppna en svårare, poängrik tågrutt.
- Att förstöra det kan skapa en lugnare korridor men färre mål.
- Ingen väg ger permanent bättre skepp, berättelsestraff eller dold “god” lösning.

### Emergenta vinjetter

1. **Första växeln:** spelaren ser laserlinjen, en kanonvagn och ett kontrollhus samtidigt. Alla tre går att lösa var för sig; skicklig placering kan få lasern att spränga vagnen, vars vrak låser växeln.
2. **Klaffskyddet:** tullslupens bredsida blir farlig, men den kommande klaffen kan fånga salvan. Öppnar spelaren klaffen för tidigt förloras skyddet men mittkorridoren öppnas.
3. **Sörens val:** tre hot byggs upp men endast de spelaren lämnat vid liv räknas. Sören markerar och förstör ett av dem; resten måste fortfarande hanteras.
4. **Tvillingfästet:** ström som bryts på Helsingör kan ge Helsingborg mer kraft men också tvinga fram en synlig kabelbro som Karl kan skjuta sönder. Bossen reagerar på spelarens fokus utan att byta till slumpmässiga attacker.

### Emergensbudget

- Högst två korsreaktiva system är farliga samtidigt i 400x280; högst ett i 320x224 under introduktionen.
- Minst 45 bildsteg telegraph före ny kollisionsgeometri och minst 60 före en korsreaktion som kan förstöra ett annat system.
- En kedjereaktion får ha högst tre led: orsak, mellanobjekt, slutverkan.
- Kameran får aldrig lämna ett orsakande objekt innan dess synliga följd är avgjord.
- HUD visar endast korta ord vid systemutfall: `VÄXEL LÅST`, `FYR ÖVERLASTAD`, `LIST x2`. Inga tutorialrutor mitt i strid.
- Varje system introduceras ensamt innan det kombineras med ett annat.

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
- En `BridgeSectionState` äger ström, spår, täckning och högst två lokala förändringar. Render, kollision och följdlogik läser samma state.
- Korsreaktioner skickas som små deterministiska händelser (`LaserHitCarriage`, `CarriageHitControl`, `NodeOverload`) i stabil objektordning. De får inte uppstå genom godtycklig spriteöverlappning.
- Sörens mål väljs deterministiskt från kvarvarande hotklass, därefter lägsta stabila objekt-id. Resultatet måste vara identiskt vid samma input.
- En debugrad i utvecklarläge visar sektion, ström, växel, täckning och senaste korsreaktion för reproducerbar felsökning.

## Icke-mål för första byggskivan

- Ingen färdig bossgrafik eller full bosskamp.
- Ingen permanent Bana 2-moralflagga.
- Ingen fysiksimulering av en hel cirkulär ringbro.
- Inga otelegrapherade klaffkollisioner eller tåg som kan köra över spelaren som bakgrundsdekor.
- Ingen ny generell dataarkitektur innan nivå-id 2 har visat vilka nya fält bron faktiskt kräver.
- Ingen emergens byggd på slump, obegränsad rigid-body-fysik eller fiendeskott som råkar träffa osynliga triggers.
- Ingen global dominoeffekt där ett val i första minuten i hemlighet avgör bossmönstret.

## Byggskivor

1. **Hook och 60-sekundersskelett (landed 2026-07-14):** DEV-start, egen seed, titel, kall brofallback, Öresunds musikkanal med `oresund-i-brand-v1.wav` som prototyp, broväktarvåg och resultatkort.
2. **Systemisk provsektion (landed 2026-07-15):** kodritad laserfyr, kontrollhus, växel och en kanonvagn med tre verifierade slutbilder från tre inspelade inputspår.
3. **Ringbrovärlden:** genererad bakgrund, Kronspannet, rälsparallax och två säkra klaffsekvenser som använder samma täckningsstate som provsektionen.
4. **Pansartåget:** lokpassage, flera kanonvagnar, spårbyte, kopplingsdöd, miljökrasch och LIST-poäng.
5. **Sörens ingripande:** radiokö, bakgrundspassage och deterministiskt val av högsta kvarvarande hot.
6. **Helsingör/Helsingborg:** två ankare, delad hälsa, korskopplad ström, tre faser, död och övergång mot Silverkroppen.

## Landad checkpoint 1

Nivå-id `2` har egen seed `3303`, tom egen radiotabell, egen fiendevåg, egen kodritad ringbro/räls/Kronspann, egen musikdispatch och eget resultatkort. DEV-rad 3 startar nivån direkt medan normalläget behåller den låst. Start från resultatet återgår till samma kampanjrad.

Två upprepade direkta WFEX-spår matchade vid bild 60, 1200, 3590 och 3650:

- 400x280: `69d10c592b7f492f`, `72e3272c46290227`, `0b31b57cdd259e3e`, `e3ad228ac8f8b656`.
- 320x224: `36488dc5e898588c`, `ebb10f9c19160184`, `1468186cfe2cc108`, `2b94df7215946c78`.

## Landad checkpoint 2

`BridgeSectionState` äger den första lokala sektionens ström, växelläge, kontrollhus, kanonvagn och separat gul koppling. Tre explicita händelsevägar är spelbara:

1. Mittläge och direkt eld förstör vagnen för 400 poäng.
2. Två riktade salvor bryter kopplingen; vagnen glider in i den varnade lasern och ger totalt 900 poäng samt `LIST x2`.
3. Vänsterläge och eld förstör kontrollhuset; `VÄXEL LÅST` leder vagnen säkert ur körfältet för 250 poäng.

Varje spår upprepades tio gånger med identisk slutbild:

- 400x280: direkt `18ae56ccc9b86b8d`, laser `18743e8de190b03d`, omledning `38e9d320ae09f385`.
- 320x224: direkt `6c2c54be1cf969ab`, laser `b2d30d3dd120c6fa`, omledning `88240bddedef2fc9`.

Utvecklarläget visar sektionens ström, växel och senaste explicita event. Normalläget visar endast kabelglöd, växellampa och korta HUD-kvitton.

## Nästa implementeringscheckpoint

Bygg skiva 3, Ringbrovärlden, ovanpå det bevisade statet:

1. Generera en läsbar Öresundsbakgrund och separata transparenta Kronspann/rälsdelar utan fysisk kollisionsgeometri.
2. Lägg till två klaffar vars render och kollision läser samma täckningsstate.
3. Introducera klaff 1 ensam, därefter klaff 2 tillsammans med laserfyrens långa varning.
4. Låt vanlig eld stoppas av stängd klaff från båda håll medan aktiv laser skär igenom efter minst 60 bildsteg varning.
5. Behåll provsektionens tre inputspår som regressionstester och lägg två nya spår för klaffskydd respekt tidig öppning.

**Definition of done:** generated miljö och fysisk klaff använder samma state utan att ändra de tre landade utfallen eller skapa mer än två samtidiga faror.

## Acceptanskriterier

- Öresunds järnkrona kan startas som nivå-id `2` i utvecklarläge utan att återanvända Bana 1 eller Bana 2:s tidslinje.
- Samma seed och input ger samma tågspår, klafföppningar, fyrsalvor och radio.
- Varje fysisk klaff visar minst 45 bildsteg varning före körfältsintrång.
- Bakgrundståg, bågar och hav kan aldrig skada spelaren.
- Lasertelegraph, aktiva strålar, spelare och projektiler förblir läsbara i både 400x280 och 320x224.
- Bossens två halvor delar exakt en livsmätare men har oberoende attack- och renderankare.
- Radio överlappar inte bossintroduktion eller annan aktiv radioreplik.
- Alla nya genererade assets behåller källa, prompt och kodfallback och packas utan lösa grannfragment.
- Provsektionen har minst tre reproducerbara lösningar: direkt förstörelse, miljödöd och säker omledning.
- Samma korsreaktion ger samma händelseordning, poäng och slutstate i tio upprepade körningar.
- Ett inspelat inputspår kan aldrig få mer än tre kedjeled eller mer än två samtidiga farliga system.
- Sörens målsökning ändras när spelaren lämnar olika synligt hot aktiva, men är identisk för samma slutstate.
