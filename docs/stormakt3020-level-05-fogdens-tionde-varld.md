# Bana 5 - Fogdens tionde värld

Status: komplett spelbar bana landad 2026-07-16 och kvar som `DEV` för direkt balanseringsprov. Beslagfält, tullpass, myntminor, registerval, Ränteverket, tre tvåvalsskåp och Rigsregnskabets trefasfinal kör på eget state och seed `3505`. Bossen har egen packad konst, dansk syntetröst, radiokö och unik 90 BPM-maskinfuga.

## Dramatiskt löfte

Malmmoderns ring leder Karl CCLV till en konstgjord fickvärld bakom fogdens vanliga farleder. Där hänger beslagtagna svenska, danska och främmande skepp i kedjor kring ett mekaniskt skattearkiv. Ebba hittar räkenskaper som visar att fogdesystemet inte bara tjänar på kriget: det förlänger kriget för att motivera sin egen existens.

Karl tar med sig Silverkroppens krigskassa, bryter sig in i arkivet och börjar montera fogdens beslagtagna vapenteknik på sitt eget skepp.

Speltid: cirka 6–8 minuter. Banan återgår till shmup men ska kännas trängre, mer mekanisk och mer valstyrd än Öresund.

## Visuell identitet

- Svart valvjärn, oxiderad koppar, smutsigt guld och kallt bokföringsgrönt.
- Beslagtagna skepp hänger i flera parallaxlager; bara markerade kedjelås är fysiska mål.
- Mekaniska bokrullar, räknedrev och ändlösa valvdörrar ersätter vanlig rymdarkitektur.
- Miljölandmärket är **Det hängande registret**, en kilometerhög cylinder med skeppskedjor som löper genom dess sidor.
- Projektiler och uppgraderingar behåller Sveriges blå/gula/cyan språk och får aldrig försvinna mot guldet.

## Uppdragsstruktur

### 0:00–1:30 - Beslagsfältet

- Karl flyger mellan kedjade skepp och tullbogserare.
- Spelaren lär sig skjuta sönder blåmarkerade kedjelås utan att träffa det beslagtagna skeppet.
- Ett befriat skepp flyr in i bakgrunden och registreras som framtida allierad.
- Malmmoderns ring öppnar det första beslagsskåpet.

### 1:30–3:00 - Roterande tullkorridorer

- Portpar varnar i minst 60 bildsteg och vrider den säkra korridoren 90 grader.
- Arkivets väggar rör sig; spelarens styrning och hela spelplanen roteras aldrig artificiellt.
- Magnetiska myntminor dras mot Karl, andra minor och aktiv bredsida.

### 3:00–4:30 - Det hängande registret

- Två vägar: den korta revisionsrännan ger poäng, den längre kedjehallen låter spelaren befria fler skepp.
- Sören varnar att fogdens register räknar fred som en ekonomisk förlust.
- Andra beslagsskåpet ger nästa uppgraderingsval.

### 4:30–6:00 - Ränteverket

- Tryckpresskanoner skapar långsamma väggar av sigill medan indrivningsbogserare försöker kedja fast Karl.
- Uppgraderad `Z`-eld och `X`-bredsida får olika användbara lösningar; ingen gren är obligatorisk.
- Ebba läser beviset för att fogdesystemet avsiktligt återstartar gamla konflikter.

### 6:00–8:00 - Rigsregnskabet

- Revisionsmaskinen bygger om sig av indrivna skeppsdelar.
- Seger frigör valvet, sparar valda vapensystem och öppnar vägen mot `Snapphanens ed`.

## Vapenuppgraderingssystem

Systemet introduceras här och följer med genom bana 5–7. Bana 1–4 förblir orörda och balanseras inte om bakåt.

### Grundregler

- Malmmoderns ring fungerar som fysisk behörighetsnyckel till tre fasta beslagsskåp.
- Varje skåp ger ett val mellan två ömsesidigt uteslutande moduler i en bestämd kategori.
- Valen är helt deterministiska; inga slumpdroppar, sällsynthetsnivåer eller procentbonusar.
- Ett säkrat val kan bytas till systermodulen vid ett senare skåp men inte mitt under strid.
- Uppgraderingarna sparas som kampanjstate. Developer-start får ett uttryckligt standardkit och behöver aldrig en äldre sparfil.
- Död återställer aktuell sektor men behåller redan låsta modulval.
- Varje modul ändrar projektilgrafik, skeppsdetalj, ljud och spelbeteende synligt.

### Skåp I - huvudeld `Z`

**Kronborren**

- Tvillingkanonerna får en långsammare central silverbult som går igenom ett pansarmål eller två små mål.
- Stark mot kedjelås, bogserare och bossrustning.
- Bygger mer värme och uppmuntrar avsiktliga salvor.

**Salvdirektören**

- Kortare eldningsintervall och alternerande tre-kronorsmönster.
- Stark mot myntminor och lätta sigilldrönare.
- Varje enskild träff är svagare och överhettning kommer snabbare vid obruten eld.

### Skåp II - bredsida `X`

**Magnetbredsidan**

- En kort blågul tryckvåg vänder magnetiska myntminor och lätta fiendeprojektiler tillbaka mot arkivet.
- Kort räckvidd och mycket hög värmekostnad.
- Kan inte vända bossprojektiler eller tydligt tunga salvor.

**Kedjekarteschen**

- Bredsidan blir en bredare solfjäder som gör extra strukturskada på kedjor, portar och valvpansar.
- Vänder inga projektiler och har längre omladdning.

### Skåp III - skeppssystem

**Silverkylare**

- Värme sjunker snabbare efter en kort paus i elden.
- Överhettat skepp återgår tidigare till normal grafik och eldhastighet.
- Ger ingen extra tålighet.

**Beslagspansar**

- Ett synligt lager mörkt valvjärn ger en extra sköldsegment per liv.
- Segmentet återställs bara mellan sektorer, inte genom passiv läkning.
- Skeppet kyler långsammare och blir därför sämre på konstant eld.

### Gränssnitt

- Ett beslagsskåp stoppar nya hot och öppnar en kompakt tvåkolumnspanel; spelet pausas inte bakom aktiva projektiler.
- Vänster/höger väljer, `Z` installerar, `X` visar den konkreta nackdelen och `Start` bekräftar att skåpet lämnas.
- HUD visar tre små fysiska modulikoner bredvid värmemätaren. Ingen separat inventarieskärm används i shmup-läget.
- Panelen jämför beteenden i klartext: exempelvis `GENOMSLAG 1 / VÄRME +4`, aldrig otydliga kvalitetsord.

## Magnetiska myntminor

- Varje mina äger position, hastighet, laddning och stabilt mål-id.
- Minor söker närmaste giltiga metallmål med deterministisk tie-break.
- Två minor som möts bildar en större men långsammare klump; kedjan har ett hårt tak.
- Magnetbredsidan byter ägare och riktning, inte bara färg.
- Minst 45 bildsteg glöd och ljud föregår explosion.

## Befriade skepp

- Varje kedjelås tillhör exakt ett bakgrundsskepp och kan förstöras utan att skeppet blir ett kollisionsobjekt.
- Befrielse ger ingen omedelbar eldkraft i Bana 5; skeppen registreras till Bana 6–7.
- Svenska, danska och okända skepp får olika radiorespons men samma mekaniska belöning.
- Den snabba vägen förblir giltig och ger högre tidspoäng men färre framtida allierade.

## Boss - Rigsregnskabet

### Fas 1 - Revisionens skal

- Två revisionsarmar drar in beslagtagna skeppsplåtar och bygger lokala rustningszoner.
- Spelaren bryter kedjenoder för att förhindra nästa lager.

### Fas 2 - Ränta på ränta

- Magnetringar cirkulerar myntminor och pressar spelaren mellan roterande tullportar.
- Vald bredsidemodul ger två olika men likvärdiga lösningar.

### Fas 3 - Huvudboken

- Rigsregnskabet öppnar en enorm grön bokföringskärna.
- Kärnan försöker återställa en förstörd rustningsdel tills motsvarande registerrulle skjuts sönder.
- Slutet är en kontrollerad kedjereaktion där valvet spricker och bakgrundsskeppen tänder sina motorer.

## Radio och röster

- Karl konstaterar att arkivet luktar mindre silver och mer rädsla; hans nya fasta rollseed återanvänds.
- Ebba identifierar krigets självbärande bokföring.
- Sören känner igen skepp som fogden tagit från båda sidor.
- Rigsregnskabet får en egen syntetisk kör av flera torra kansliröster, aldrig Rasmus röst med filter.
- Radiokön avslutar varje replik före uppgraderingspanel och bossintroduktion.

## Musik och ljud

- Grundspår: mörk mekanisk fuga av tryckpress, kedjor, myntslag, låg orgel och sorgsen svensk mässing.
- Uppgraderingsskåp tunnar musiken till tickande räknedrev och en ensam silverton.
- Bossvarianten lägger till felräknade taktarter utan att ändra spelarens simuleringshastighet.
- Obligatoriska egna effekter: kedjelås, portrotation, myntmagnetism, myntladdning, myntbrott, skåpöppning, modulinstallation och tre nya vapenfamiljer.

## Tekniska checkpoints

1. **Plan och kampanjrad (landad 2026-07-16):** detta dokument, Bana 5-rad som `DEV`, nivå-id, seed och separat deterministisk tidslinje.
2. **Beslagsfält (första skiva landad 2026-07-16):** kodritat mekaniskt arkiv, två kedjade skepp, fysiska cyanlås och befrielseflykt. Genererade parallaxlager och full skeppsvariation återstår till konstpasset.
3. **Första skåpet (spelbart 2026-07-16):** båda `Z`-modulerna har kompakt wide/legacy-UI, synlig nackdel, skilda projektilmönster, skada, intervall, värme och HUD-namn. Permanent kampanjsparning och egna ljud/assets hör till checkpoint 6 respektive konst/ljudpasset.
4. **Tullkorridorer (första skiva landad 2026-07-16):** fyra alternerande tullpass visar en 60-bilders säker öppning innan de går fysiskt genom spelplanen. Magnetiska myntminor söker Karl, laddar i 45 bilder och kan skjutas sönder eller fångas av bredsidan som cyan returminor. Genererade port/mynt-assets och den senare Magnetbredsidan återstår.
5. **Registerval (spelbart 2026-07-16):** spelaren väljer led genom att flyga vänster till revisionsrännan eller höger till kedjehallen. Revision ger fast 1500-bonus och tre hårdare indrivare; kedjehallen ger tid och tre nya fysiska skeppslås. Resultatkortet visar vald led och verkligt antal befriade skepp. Nya korta Sören/Ebba-rutor avslöjar att fogden tagit båda sidors skepp och bokför fred som förlust; deras nya seedlåsta röster landade i ljudpasset.
6. **Fullt vapensystem (landat 2026-07-16):** Ränteverket har fem långsamma, segmenterade sigillväggar och lätta pressprojektiler. Magnetbredsidan ger två korta cyan pulser, vänder lätta skott inom fångstfältet men kostar 24 värme. Kedjekarteschen ger fem power-8 strukturskott men vänder ingenting och laddar i 30 bilder. Skåp III installerar Silverkylare, som tredubblar kylningen efter 18 bilders eldpause, eller ett synligt Beslagspansar med ett laddat sköldsegment och långsammare kylning. De tre valen skrivs atomiskt till `campaign.json`, laddas med vanlig Start och kringgås med `Slow+Start standard`.
7. **Rigsregnskabet (landat 2026-07-16):** den enorma revisionsmaskinen anländer först efter skåp III. Fas 1 har två separata revisionspansar, fas 2 öppnar räntkärnan och blandar minor med sigillväggar, fas 3 kräver att båda huvudboksrullarna bryts innan kärnan tar skada. Fyra köade danska VoxCPM2-repliker delar rollseed `302129`; dödsrepliken får tala klart innan resultatkortet. Den egna 90 BPM-musiken använder en tystnadsfri 12-takters runtime-loop, och segern sparar kampanjkitet atomiskt.
8. **Grafiköversyn (landad 2026-07-16):** det kodritade rälsprovet ersätts av ett packat, kontinuerligt rullande arkivvalv med huvudboksband, kedjor, pergamenttrummor, cyan registerbelysning och vaxsigill. Kedjelås, myntminor, sigillväggar, tullportar, beslagsskåp och Ränteverkets pressar har separata alpha-assets i samma materialfamilj som Rigsregnskabet. Tullpassens säkra öppning förannonseras av en kunglig varningsportal, och registervalet har en fysisk tvåvägsväxel med egna Revision- och Kedjehall-emblem i både banan och valpanelen. Alla sex uppgraderingar har dessutom egna fysiska skeppsdelar, menyikoner och skott- eller energifält: modulerna monteras på rätt nos-, bredside- och akterfäste utan att ändra skeppets träffyta. Kodgrafiken finns kvar som fallback och alla fysiska träffytor och valgränser är oförändrade.
9. **Ljud och radio (landat 2026-07-16):** tretton lokalt genererade Small-SFX-effekter följer de auktoritativa övergångarna för tullport, myntmina, kedjelås, registerväxel, sigillvägg, installation, fyra vapensystem samt Rigsregnskabets fas- och huvudboksbrott. Sören och Ebbas tre arkivrepliker är nygenererade från deras helt syntetiska rollreferenser med oförändrade dialogseeds `302122` respektive `302121`. Aktiv radio duckar musiken och de tyngsta mekanikljuden är gain-sänkta i mixen.
10. **Publicering:** determinism, wide/legacy, balansering och status `STRID`.

## Acceptanskriterier

- Bana 5 använder egen state, seed, musik, radio och resultatkort utan läckage från Bana 1–4.
- Samma input och modulval ger samma minor, befriade skepp, bossrustning och slutbild.
- Varje uppgradering har en synlig styrka och nackdel; ingen gren är ett matematiskt självklart val.
- En spelare kan slutföra banan med vilket av de åtta möjliga tre-skåpskiten som helst.
- Uppgraderingspanel öppnas aldrig med levande farliga projektiler eller aktiv radioreplik.
- Magnetbredsidan kan aldrig reflektera en icke-markerad tung projektil.
- Kedjade bakgrundsskepp blir aldrig fysiska hinder när de frigörs.
- Valen överlever död och fortsättning men developer-reset är reproducerbar utan sparfil.
- HUD och valpanel är fullt läsbara i både 400x280 och 320x224.
- Resultatet visar befriade skepp, valda moduler och vägen mot `SNAPPHANENS ED`.

## Icke-mål för första skivan

- Ingen generell loot, butik, crafting eller slumpmässig vapengenerering.
- Ingen ombalansering av tidigare banor.
- Ingen fri ombyggnad under strid.
- Inga hundratals aktiva räddade skepp; flottan är presentation och framtida kampanjstate.
- Ingen full boss eller permanent save-migrering innan första skåpets båda val är roliga i direkt provspelning.

## Första skivans verifiering

- Direkt WFEX startar rad 5 i utvecklarläge och går aldrig via tidigare banors radio- eller fiendetabeller.
- Kronborren och Salvdirektören ger olika slutbilder efter samma rörelsespår i både 400x280 och 320x224.
- Uppgraderingspanelen är verifierad vid bild 849; upprepade körningar gav identisk hash `cc7d4c1dc0c7ecdb`.
- Salvdirektörspåret upprepades med identisk hash `01b0f8a942447d1e`; båda legacy-valen har separata stabila slutbilder.
- Remote-säker bekräftelse med `Enter/Start` verifieras direkt efter skåpet i båda upplösningarna; den går före vanlig pauslogik och installerar valt vapen omedelbart.
- Tullpassvarning och en fångad `RETUR`-mina har direkta wide/legacy-bilder. Samma returspår ger samma objektordning och slutbild vid upprepning.
- Båda registerlederna är körda till separata resultatkort i wide och legacy. Samma kedjehallsspår räknar bara skepp vars lås verkligen sköts sönder; ingen presentationsbild tilldelar gratis allierade.
- Ränteverkets sigillvägg, det andra skåpet och båda installerade bredsidorna har separata direkta bilder i wide och legacy. Magnetspåret och karteschspåret får skilda projektilfält, HUD-namn, värme och stabila slutbilder.
- Tredje skåpet, Silverkylarens fenor, Beslagspansarets järnplåtar, sköldladdning och båda värmekurvorna har separata wide/legacy-spår. Ett efterföljande nytt nivå-5-startprov läser tillbaka det sparade kitet.
- Så länge raden heter `DEV` fångar en märkt utvecklarsköld skada efter att ett eventuellt Beslagspansarsegment förbrukats. Det är en provspelningshjälp för låg-FPS-remote och ska tas bort när raden publiceras som `STRID`; ordinarie balansvärden ändras inte.
- Rigsregnskabet är besegrad genom verklig styrning och eld i både 400x280 och 320x224. Efter modulpasset gav upprepade wide-körningar genom hela banan samma slutbildshash `b41d22ee3c0c7400`; legacy gav `880a27f6fbe5c915`. Resultatkortet väntar på dödsradions avslut och visar det sparade kampanjkitet.
- Ljudmotorn laddar 43 separata effekter och 68 radiofiler utan formatfallback. Bana 5:s tretton runtime-effekter är 48 kHz stereo PCM16; rågenerationer, prompts, seeds och de tre nya röstförfrågningarna är bevarade.
