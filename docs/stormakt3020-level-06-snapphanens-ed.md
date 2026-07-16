# Bana 6 - Snapphanens ed

Status: implementeringsplan fastlagd 2026-07-16. Bana 6 blir kampanjrad 6, nivå-id `5`, seed `3606` och börjar som `DEV`. Första spelbara checkpointen ska kunna startas direkt från den rad som redan förväljs efter Rigsregnskabets resultatkort. Banan publiceras som `STRID` först när båda vägvalen, Sörens duell, De Røde Hunde, radio, unik musik och wide/legacy-verifiering är färdiga.

## Dramatiskt löfte

Karl lämnar fogdens mekaniska arkiv med Malmmoderns ring, tre beslagtagna vapensystem och de skepp han lyckades frigöra. Sören Svartkrut sänder en grön signal från ett drivande vrakhav men vägrar leda Karl mot Köpenhamn innan han vet om karolinen är en bundsförvant eller ännu en fogde med blå rock.

Banan börjar som en jakt genom falska fyrar och sönderskjutna flottor. Mitt i stormens öga kräver Sören en kort hedersduell. När ingen av dem viker svär de en praktisk, misstänksam ed och måste omedelbart slåss tillsammans mot den kungliga jägarflottan **De Røde Hunde**.

Speltid: cirka 7–9 minuter. Speltypen är åter vertikal shmup, men vrak, signaler och fienders inbördes träffar ska skapa mer systemisk improvisation än i tidigare rymdbanor.

## Låsta designbeslut

- Resultatet från Bana 5 följer med. Karl behåller valt huvudvapen, bredsida och skeppssystem.
- Alla åtta möjliga kombinationer av de tre tvåvalen måste kunna klara banan. Inget vägval får kräva Magnetbredsidan, Kronborren eller någon annan specifik modul.
- Sörens duell är på heder och kan inte döda någon av huvudpersonerna. Den slutar när hans hedersmätare bryts, inte med en falsk död eller ett manusstyrt förräderi.
- Hjälp rebellflottan kostar tempo och tillfällig eldkraft nu men sparar verkliga allierade till Bana 7. Genombrottsleden ger högre poäng och kortare tid men inga gratis slutallierade.
- Falska fyrar och jägarspår får aldrig vara tunna debugvektorer. Varningar ska vara fysiska ljuskäglor, partiklar, bojar, kablar eller målade assets.
- Segerkortet står kvar i banans musik tills spelaren trycker Start. Därefter öppnas fälttågsmenyn med `KÖPENHAMNS RING` förvald och menymusik aktiv; Bana 7 startar aldrig automatiskt.

## Bana i tid

### 0:00–1:10 - Den gröna signalen

- Karl flyger in i ett tredelat vrakhav: långsamma bakgrundsskrov, mellanlager av trasiga master och ett litet antal tydligt telegraferade fysiska vrakdelar.
- Ebbas instrument visar tre identiska signaler. Sörens riktiga fyr svarar med ett ojämnt grönt dubbelblink; de danska kopiorna svarar jämnt rött/vitt.
- Skjuter spelaren en falsk fyr tidigt kollapsar dess kamouflage och avslöjar en liten jägargrupp. Väntar spelaren på dubbelblinket kan gruppen passeras eller lockas in i vrak.
- Befriade skepp från Bana 5 syns i bakgrunden, upp till ett läsbart presentationsmaximum. De påverkar inte spelarens träffyta.

### 1:10–2:30 - Falska fyrars led

- Tre fasta fyrgrupper ger samma seedade ordning men olika lokala konsekvenser beroende på skott och position.
- Kungliga doftminor fäster inte direkt på Karl. De lämnar ett kort fysiskt glödspår som De Røde Hundes projektiler följer.
- Magnetbredsidan kan vända lätta doftminor. Kedjekarteschen kan slå sönder deras järnhölje. Vanlig eld kan alltid förstöra dem långsammare.
- En vänd doftmina kan få en jägarsalva att träffa en falsk fyr, ett vrak eller en annan dansk jägare. Det är banans första emergenta systemsamband.

### 2:30–3:45 - Sörens hedersduell

- Vrakstormen öppnar en rund, mörk duellplats av kedjade gruvskrov. Sören flyger in utan bakgrundsdubblett.
- Bossrad: `SÖREN SVARTKRUT  HEDER`.
- Fas 1 läser spelarens sidbyten och svarar med kopparsalvor och korta dimsteg. Hans verkliga position har alltid en grön lanternreflex; efterbilder saknar den.
- Fas 2 låser bredsidorna växelvis med fysiska kedjekrokar. Spelaren kan skjuta kroken eller byta sida innan den sluts.
- Spelarskada fungerar normalt. Sören avbryter sitt sista skott om det skulle ta Karls sista liv under duellens avslutningsögonblick; detta är presentation, inte generell odödlighet.
- Vid noll heder bryter Sören striden, flyger jämsides och avlägger eden i radio. Inga vrakexplosioner används på honom.

### 3:45–5:25 - Eden och vägvalet

Valet görs genom verklig position i en bred vrakkorsning, inte i en modal meny.

#### Vänster: Kaparleden

- Tre snapphaneskepp sitter fast i kungliga jaktbojar.
- Karl måste ligga nära ett skepp medan dess bojlås bryts. Under räddningskanalen går huvudvapnet på halv kadens och bredsidan spärras; rörelse och kylning fungerar normalt.
- Varje verkligt räddat skepp sparas som `SnapphaneAllies`. Överlevande skepp gör en kort fysisk motattack i slutbossen och följer med till Bana 7.
- Befriade svenska skepp från Bana 5 kan automatiskt täcka den första räddningskanalen, men ger aldrig gratis räddade snapphanar.

#### Höger: Krutrännan

- En smalare led med rörliga vrakpressar, doftminor och två elitjägare.
- Karl behåller full eldkraft och bygger en multiplikator genom att bryta fyrar utan att lämna leden.
- Genomförd ränna ger fast poängbonus och ett kort kylfönster före bossen, men `SnapphaneAllies` ökar inte.

Sören kommenterar det faktiska valet, inte knapptryckningen. Båda lederna sammanfogas före bossen och får samma totala bosshälsa.

### 5:25–8:00 - De Røde Hunde

Bossen är en jägarflotta med tre verkliga skepp och en delad övergripande struktur, inte ett ensamt stort skrov med tre målade huvuden.

Bossrad: `DE RØDE HUNDE`.

#### Fas 1 - Drevet

- Tre hundfregatter kommer från varsin jaktlinje: **Sporet**, **Biddet** och **Koblet**.
- Varje fregatt har egen position, kanoner och skadestate men hälsan presenteras i en gemensam rad.
- Fysiska strålkastarkäglor söker i turordning. Ett markerat vrak eller en vänd doftmina kan stjäla låsningen.
- Förstörda sidokanoner minskar kommande korseld; skrov kan inte tas bort helt före fasbytet.

#### Fas 2 - Kopplet

- De tre skeppen länkar sig med två tjocka mekaniska jaktkedjor och sveper spelplanen som ett brutet V.
- Kedjorna blockerar vanliga skott men tar strukturskada. Kronborren piercar en länk, Kedjekarteschen skadar ett bredare område och standardskotten kan alltid bryta länkar över tid.
- Sören attackerar den friskaste länken. Räddade snapphanar angriper en kanon, aldrig bosskärnan.
- När en länk går av behåller skeppen sin aktuella position och glider isär i en mjuk stateövergång; ingen teleport eller ryckig fasreset.

#### Fas 3 - Sista jakten

- Det återstående ledarskeppet fäller ut en rödvit jaktmask och de två skadade fregatterna blir rörliga flankhot.
- Jaktmaskens tre luktventiler är separata mål. Varje bruten ventil minskar mängden målsökande eld och öppnar en större sektor mot kärnan.
- Under sista 20 procenten flyger Karl och Sören på varsin sida. Spelaren behåller full kontroll och avlossar själv slutskotten.
- Dödssekvensen bryter först jaktmasken, sedan kedjorna och sist skroven. Den gröna eden-signalen överlever explosionen och leder vidare mot Köpenhamn.

## Kampanjstate och kodstruktur

### Ny nivåstate

Inför `SnapphaneWorldState` som ensam ägare till Bana 6:s tidslinje:

- `Age`, `Route`, `RouteLocked` och `ScoreMultiplier`.
- listor för fyrar, doftminor, fysiska vrak, räddningsbojar och aktiva allierade;
- `SorenDuelState` med heder, fas, verklig position, efterbilder och krokstate;
- `RedHoundsBossState` med tre skeppsdelar, gemensam fas, kedjor, jaktlås och dödsålder;
- aktivt kampanjkit, ingående `FreedShips` och utgående `SnapphaneAllies`.

Level id `5` får seed `3606`, egen resetgren, rendergren, tidslinje, radiotabell, musikdispatch, resultatkort och WFEX-spår. Generiska Bana 1-vågor får aldrig falla igenom när en Bana 6-lista är tom.

### Gemensamt vapenkitt

Nuvarande vapenkod läser moduler direkt från `TitheWorldState`. Innan Bana 6-strid införs en liten aktiv `StormaktLoadout` eller motsvarande hjälpfunktion som kan exponera:

- `PrimaryModule`;
- `BroadsideModule`;
- `ShipModule` och aktuell pansarladdning.

Bana 5 och 6 använder samma avfyrnings-, värme-, HUD- och skeppsöverlägg. Bana 1–4 fortsätter med standardvapnen och ändras inte.

### Save-schema 2

Migrera `StormaktCampaignSave` från schema 1 till schema 2 med bakåtkompatibla standardvärden:

- befintliga tre moduler och `FreedShips`;
- `SnapphaneRoute`;
- `SnapphaneAllies`;
- `SorenOathComplete`.

En gammal schema-1-fil ska laddas som samma vapenkitt och noll nya snapphanefält. Bana 6 skriver atomiskt efter vägvalet och efter bossen. Test-/standardstart får inte skriva över spelarens riktiga kampanjfil.

## Emergent systemsamband

- Vända doftminor kan lura dansk målsökning till fyrar, vrak och andra jägare.
- Vrak som träffas av tung eld får en seedad rörelseriktning och kan öppna eller stänga en tillfällig skottlinje utan att skapa oförutsägbara omöjliga passager.
- Sörens målval hämtas från synligt state: aktiv jaktkedja, friskaste kanon eller närmaste räddningsboj. Han får aldrig förstöra en dold framtida bossfas.
- Befriade skepp och räddade snapphanar presenteras som begränsade verkliga stödattacker. Kampanjvärdet kan vara större än antalet aktiva kollisionsobjekt.
- Silverkylaren, Beslagspansaret och båda bredsidorna förändrar lösningar men inte vilka möten som är möjliga.

## Grafikplan

All ny rasterkonst använder egna alpha-assets och samma late-1990s pre-rendered arcade/PC-språk som Bana 3–5.

1. Tre sömlösa vrakhavslager: avlägsna flottor, kedjade mellanskrov och tydliga spelplansvrak.
2. Riktig/falsk fyr i släckt, signal, avslöjad och förstörd form.
3. Doftmina, vänd mina, fysisk jaktmarkör och kompakt dödseffekt.
4. Sörens kapare i inflygning, duell, krokattack, edsflygning och skadad men levande form. Tidigare Sören-konst är stilreferens, inte färdig Bana 6-animation.
5. Räddningsboj hel/bruten och två snapphaneskeppsvarianter.
6. Sporet, Biddet och Koblet i hela, skadade och döende former; gemensamma kedjelänkar och jaktmask i stängd/öppen form.
7. Nya projektiler, ljuskäglor, kedjebrott, skrovexplosioner och resultatkortets gröna edssigill.

Alla generationsprompts, källbilder och alpha-rensade runtimebilder sparas bredvid varandra. Kodritade former är endast fallback och får inte synas när assetpaketet är komplett.

## Musik, ljud och radio

### Musik

- Banmusik: **Snapphanens ed**, cirka 96 BPM i moll. Mörk nyckelharpa, torr kopparslagverk, låg svensk fälttrumma, drivande basstråkar och korta gröna signaltoner.
- Duellen skalar bort orkestern till puls, stråkar och två svarande kopparmotiv utan att byta simuleringshastighet.
- Bossvariant: **De Røde Hundes drev**, snabbare jaktpuls, dansk virvel, järnkedjor och Sörens motiv som kommer in efter edens fasbyte.
- Resultatkortet behåller bossens lättnadsloop tills Start; menymusiken börjar först i fälttågsvalet.

### Minsta nya SFX-familj

- riktig och falsk fyrsignal;
- doftmina fäster, vänds och brister;
- Sörens dimsteg och kedjekrok;
- räddningsboj bryts och skepp frigörs;
- hundfregattens jaktlås;
- jaktkedja spänns och bryts;
- jaktmask öppnas;
- bossens tredelade dödssekvens.

Effekterna genereras lokalt med fasta seeds, korta torra svansar och runtime-gain anpassad efter verklig repetitionsfrekvens.

### Radio

Sören behåller sin syntetiska rollreferens och seed `302122`; Ebba behåller `302121`. Nya texter och WAV-filer skapas för Bana 6 och äldre repliker återanvänds inte. En ny dansk jägarroll får egen helt syntetisk referens och fast rollseed.

Obligatoriska radiotillfällen:

1. Ebba identifierar tre omöjligt lika Sören-signaler.
2. Sören kallar Karl till hedersduellen.
3. Duellen bryts och eden avläggs.
4. Sören reagerar separat på Kaparleden och Krutrännan.
5. De Røde Hunde kungör jakten.
6. Sören kommenterar den första brutna jaktkedjan.
7. Ebba sätter kurs mot Köpenhamns ring efter segern.

All radio går genom befintlig kö; fasbyten får inte tala över en aktiv replik.

## Implementeringscheckpoints

1. **Plan och kampanjrad:** detta dokument, rad 6 som `DEV`, nivå-id `5`, seed `3606`, tom separat state/render/tidslinje, titel och resultatkort. Resultat-Start markerar Bana 7. Commit och push.
2. **Kampanjkit och save-schema:** bryt ut aktiv loadout, ladda schema 1/2, bevisa alla åtta kit i en kort testarena och skriv inga saves under testläge. Commit och push.
3. **Vrakhav och falska fyrar:** tre bakgrundslager, fysiska vrak, fyra fyrstates, doftminor och minst ett emergent fiende-mot-miljöutfall. Wide/legacy-hashar. Commit och push.
4. **Sörens hedersduell:** egen state, två faser, efterbildsläsning, krokattack, radio och mjuk övergång till jämsidesflygning. Ingen dödsexplosion. Commit och push.
5. **Eden och vägvalet:** spatial korsning, tre räddningsbojar, tillfällig kanaliseringsnackdel, Krutrännan, verklig allieradräkning och atomisk schema-2-save. Commit och push.
6. **De Røde Hunde:** tre självständiga bossankare, delad hälsa, jaktlås, två brytbara kedjor, jaktmask, Sören/allierad målprioritet och ryckfria fasövergångar. Commit och push.
7. **Konstpass:** alla fyrar, vrak, Sören-states, snapphanar, hundfregatter, kedjor, mask, projektiler och dödseffekter packas med kodfallback. Commit och push.
8. **Ljud och röster:** unik bana/duell/bossmusik, fasta SFX-seeds, nya Sören/Ebba/jägarrepliker, radiokö och mixkontroll. Commit och push.
9. **Publicering:** verklig spelstyrning genom båda lederna och alla kitfamiljer, determinism i 400x280 och 320x224, normal död/omstart, resultatpaus, Bana 7 förvald och status `STRID`. Commit och push.

## Acceptanskriterier

- Bana 6 kan startas från den rad som Rigsregnskabets resultat förväljer och läcker aldrig Bana 1–5:s fiende- eller radiotabeller.
- Resultatkortet väntar på Start; Start går till menymusik med Bana 7 markerad och startar den inte.
- Spelarens sparade kit syns på skeppet, fungerar mekaniskt och överlever död/omstart.
- Alla åtta kit kan bryta varje obligatoriskt mål, om än på olika sätt och tider.
- Båda vägvalen ger tydligt olika mittsektion, resultattext och sparat finalstöd.
- Sören kan aldrig dö, stjäla spelarens slutskott eller förstöra ett framtida bossmål.
- De tre hundfregatterna har verkliga, läsbara positioner och träffytor; kedjor och jaktmask följer auktoritativt state.
- Inga tunna debuglinjer, platta cirklar eller generiska färgrutor syns när assetpaketet är laddat.
- Aktiv radio duckar musik och repetitiva vapen/SFX utan klippning eller staplad ljudvägg.
- Samma input, kit och väg ger samma objektordning, saveutfall och slutbild i wide och legacy.

## Icke-mål

- Ingen butik, crafting eller ny permanent vapenmeny.
- Ingen fri ombyggnad av Bana 5-kitet under strid.
- Inga hundratals aktiva allierade; flottstorlek presenteras med begränsade ankare och bakgrundslager.
- Ingen personlig slutduell mot den danska kungen; den hör till Köpenhamns sista ring och Superarmadan.
- Ingen ombalansering av Bana 1–5 utöver rena regressioner som upptäcks av progressionstesterna.
