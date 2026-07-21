# Bana 6 - Snapphanens ed

Status: implementeringsplan fastlagd 2026-07-16; checkpoint 1–6 landade 2026-07-17. Bana 6 är kampanjrad 6, nivå-id `5`, seed `3606` och börjar som `DEV`. Vrakhavet, Sörens hedersduell, båda spelbara vägvalen och hela De Røde Hunde-striden kan startas direkt från den rad som redan förväljs efter Rigsregnskabets resultatkort och använder det sparade vapenkitet från Bana 5. Banan publiceras som `STRID` först när full radio, unik musik, slutligt konstpass och verklig genomspelning är färdiga.

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
- Kungliga jägare går in från båda flankerna under hela räddningen. Två av tre jägare låser sin eld på Sörens verkliga position medan resten fortsätter pressa Karl; en vänd doftmina har fortfarande högsta prioritet och kan stjäla bådas målsökning.
- Sören håller motsatt flank vid det aktiva bojlåset, gör återkommande dimsteg och skjuter målsökande dubbla kopparsalvor mot den närmaste levande jägaren. HUD-raden `SÖREN TÄCKER` gör rollfördelningen explicit.
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
- Jaktmaskens tre luktventiler är separata mål. Så länge någon ventil lever laddar masken sin varnade huvudstråle samtidigt som flankerna skjuter korsande trefingersalvor.
- När sista ventilen brister börjar **Sista bettet** i stället för ett lugnt skadefönster: ledaren sveper aggressivt, riktade femskottsfläktar växlar med sexdelade projektilgardiner vars enda lucka flyttar sig mellan salvorna, och en sidofregatt fyller på med snabb treudd.
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

1. Tre sömlösa vrakhavslager: den genererade, vertikalt speglade Svarta vrakhavet-plåten; åtta långsamma halvtransparenta parallaxvrak; samt tydliga stateägda spelplansvrak ovanpå.
2. Riktig/falsk fyr i släckt, signal, avslöjad och förstörd form.
3. Doftmina, vänd mina, fysisk jaktmarkör och kompakt dödseffekt.
4. Sörens kapare i inflygning, duell, krokattack, edsflygning och skadad men levande form. Tidigare Sören-konst är stilreferens, inte färdig Bana 6-animation.
5. Räddningsboj hel/bruten och två snapphaneskeppsvarianter.
6. Sporet, Biddet och Koblet i hela, skadade och döende former; gemensamma kedjelänkar och jaktmask i stängd/öppen form.
7. Nya projektiler, ljuskäglor, kedjebrott, skrovexplosioner och resultatkortets gröna edssigill.

Alla generationsprompts, källbilder och alpha-rensade runtimebilder sparas bredvid varandra. Kodritade former är endast fallback och får inte synas när assetpaketet är komplett.

## Musik, ljud och radio

### Musik

- Banmusik: **Snapphanens jakt**, 120 BPM i D-moll. Mörk nyckelharpa, kammarstråkar och torr cembalo bär ett tretons jaktmotiv över analog sequencerbas, järnkick, breakbeat-skuggor, svensk fälttrumma och kedjeslagverk. Samma tystnadsfria jaktloop fortsätter genom Sörens duell och återvänder efter eden utan att starta om simuleringen.
- Bossvariant: **De Røde Hundes drev**, också 120 BPM i D-moll för en ren halsekunds-crossfade. Jaktmotivet delas i tre sammanflätade stämmor för orgelpedal, låg stråkfuga och skärande analog arpeggiator över dansk virvel, jaktbleck och kedjeklampar.
- Segervariant: **Snapphanens ed**, 120 BPM i F-dur med kvarvarande D-mollskugga. Nyckelharpa, naturhorn och mjukare technopuls låter Sörens kopparmotiv och Karls karolinska bleck svara varandra utan att bli en glad slutjingel.
- De bevarade 48/48/24-sekunders ACE-Step-mastrarna har fasta seeds `30201801`–`30201803`. Runtime använder exakta tystnadsfria 22/22/8-taktersedit på 44/44/16 sekunder och behåller segerloopen över resultatkortet tills Start; menymusiken börjar först i fälttågsvalet.

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

1. **Plan och kampanjrad (landad 2026-07-17):** detta dokument, rad 6 som `DEV`, nivå-id `5`, seed `3606`, separat `SnapphaneWorldState`, en deterministisk minut av det första tredelade vrakhavet, egen render/tidslinje, grön dubbelblinkande signalfyr, titel och resultatkort. Bana 1:s fiender och radio är hårt bortkopplade. Resultat-Start går till menymusik och markerar Bana 7; radlayouten rymmer nu alla sju fälttåg i wide och legacy. Commit och push.
2. **Kampanjkit och save-schema (landad 2026-07-17):** `StormaktLoadout` äger huvudvapen, bredsida, skeppssystem, pansarladdning och vapenvila gemensamt för Bana 5–6. Schema 1 laddas bakåtkompatibelt; schema 2 skriver även `SnapphaneRoute`, `SnapphaneAllies` och `SorenOathComplete` atomiskt. Alla åtta kit ger skilda beväpnade WFEX-bilder i vrakhavsarenan. `WAYLANDFORGE_STORMAKT_LOADOUT_TEST=0..7` och `Slow+Start` undertrycker kampanjskrivning; ett verkligt Bana 5-val skriver schema 2. Commit och push.
3. **Vrakhav och falska fyrar (landad 2026-07-17):** tre stateägda vrakdjup, skjutbara fysiska vrak med seedad impuls, tre grupper av äkta/falska fyrar, fyra fyrstates, doftminor, vänd Magnetbredsida-state och egna kungliga jägare/projektiler. Målsökningen väljer en vänd mina framför Karl; detonationen kan skada fyr, vrak eller annan jägare och räknas som `VILSELD`. Tolv bildgenererade alpha-assets med prompt/källbild/runtimebild är packade med kodfallback. Två wide-spår matchar i sex kontrollbilder; legacy och resultat→Bana 7 är verifierade separat. Commit och push.
4. **Sörens hedersduell (landad 2026-07-17):** `SorenDuelState` äger heder, verklig position, två stridsfaser, kopparsalvor, icke-kolliderande dimsteg och skjutbara fysiska kedjekrokar. Endast den verklige Sören bär grön lanternreflex. Noll heder går utan död eller explosion till en mjuk jämsidesflygning, en köad ed mellan Sören och Ebba och först därefter resultatkortet. Ett nytt bildgenererat åttadelat alpha-ark är packat med fallback; nya textrutor använder inga äldre ljudklipp och får sina seedlåsta WAV-filer i checkpoint 8. Två wide-spår matchar i åtta kontrollbilder och legacy täcker duellstart, krokfas, ed, resultat och meny. Commit och push.
5. **Eden och vägvalet (landad 2026-07-17, Kaparleden utbyggd 2026-07-18):** efter den köade eden öppnas en icke-modal vrakkorsning där Karls verkliga position låser Kaparleden eller Krutrännan. Kaparleden äger tre individuella skepp/bojar och räknar bara fullbordade 150-bilders räddningskanaler; under kanalen får huvudvapnet dubbel omladdning och bredsidan spärras. Ruttens kungliga jägare delar målsökningen mellan Karl och den verklige Sören, medan Sören manövrerar kring aktivt bojlås och besvarar elden med egna dubbla kopparsalvor. Doftminor fortsätter bryta all annan mållåsning när de vänds. `WAYLANDFORGE_STORMAKT_SNAPPHANE_RESCUE_TEST=1` startar en skrivskyddad tre-bojsfixtur; slutliga wide/legacy-spår fullbordar 3/3 räddningar vid `1183fc121da72fd9` respektive `1929accd60bc6cf2`. Krutrännan äger fyra fysiska vrakpressar med synligt kollisionsgap, tre bonusfyrar, doftminor och två 74-hälsas elitjägare. Rutt, verkligt allieradantal, ed och inkommande flottvärde skrivs atomiskt vid val, varje räddning och ledslut. Ett nytt bildgenererat alpha-ark ger åtta ruttassets med kodfallback. Commit och push.
6. **De Røde Hunde (landad 2026-07-17, utbyggd 2026-07-18):** efter ruttens verkliga slut flyger befriade kaparskepp ur sina gamla ruttpositioner innan Sporet, Biddet och Koblet anfaller som tre självständigt rörliga ankare med en delad 1050-hälsopool. Drevet har ett fysiskt prickfält som låser Karls position och kanoner med egen hälsa; Kopplet för dem mjukt till V-formation med två skjutbara 110-hälsolänkar där Kedjekartesch och Kronborr gör dubbel skada. Alla tre faser skjuter nu tätare. Sista jakten kräver tre synliga luktventiler medan maskens varnade huvudstråle, femdelade projektilsalva och flankernas trefingersalvor överlappar. När ventilerna förstörts börjar Sista bettet: rörlig ledare, riktade femskottsfläktar och sexprojektilgardiner med en deterministiskt flyttande säker lucka, plus alternerande sidotreudd. Sören prioriterar kanon, kedja och därefter kärnan men kan aldrig avlossa slutskottet; 0–3 räddade allierade angriper bara kanoner. De fynska amiralstrillingarna Agnete, Bodil och Dagmar Rød äger varsin dansk radiopersonlighet och porträtt samt ett gemensamt vrålporträtt; fasernas minimitider håller replikerna synkroniserade med drev, koppel, mask och megavapen. `WAYLANDFORGE_STORMAKT_RED_HOUNDS_FINAL_TEST=1` öppnar en skrivskyddad Sista bettet-fixtur; upprepade wide-hashar vid 100/430/760/1180 är `2e6e7a023ce42621`, `7c2aca334c738504`, `3370e9da77886cba`, `36944898e29fe50f`, med legacy `714ce5a36c831024`, `f65656786a5ead8b`, `f42e473e90c07ad7`, `95436497ec64e36d`. Utvecklarläget blockerar endast spelarskada på nivå-id `5`; publik körning påverkas inte. Commit och push.
7. **Konstpass (vrakhav och objekt landade 2026-07-18):** en ny hög Svarta vrakhavet-plåt ger det mörka centrala flygstråket avlägsna förstörda flottor längs sidorna. Ett separat alpha-ark ger åtta stora icke-fysiska halvskrov, kedjemaster, kanonvrak, motorer, kölar, ankare, pannor och bredsidor i långsam parallax. Samma ark ger nu nedskalade målade silhuetter åt statevärldens bakre två vraklager; de gamla kodritade trianglarna, korsen och strecken syns endast om assetpaketet saknas. De främsta stateägda vraken ligger kvar som fullt opaka kollisionsobjekt. Doftminor har röd hotring eller pulserande cyan lockring och varje aktiv räddningsboj har gul/grön fältring. `build_assets.py` packar egna wide/legacy-bakgrunder och alla parallaxobjekt med kodfallback. Upprepade normal-, räddnings- och bossspår matchar i båda upplösningarna. Commit och push.
8. **Ljud och röster (radio- och musikpass landade 2026-07-18, SFX sanerat 2026-07-21):** Agnete, Bodil och Dagmar har tre seedlåsta syntetiska referenser och sju färdiga DOTs SOAR-bossrepliker. Ebbas och Sörens befintliga syntetiska roller ger ytterligare tio Bana 6-repliker för duell, ed, vägval, koppel, jaktmask och seger. Samtliga 17 radiokort har egna råmasters, requestposter, manifest-hashar, deterministiskt radiofilter och runtimekoppling; inga Bana 6-kort återstår som text-only. Tre seedlåsta ACE-Step-mastrar ger dessutom egen klassisk technojakt, De Røde Hunde-bossmusik och en separat segerloop, med exakta tystnadsfria taktedits och fasägda crossfades. SFX-inventeringen fann noll nivåegna filer och tio lånade filer från generisk strid och Tionde världen. Bana 6 har nu sexton deterministiska, brusfria procedurljud för signaler, falska fyrar, doftminor, jägare, vrak, Sörens kopparsalvor och krokar, räddning, De Røde Hundes salvor, megavapen, kedjor, delbrott, dödssekvens och skrovträff. Det permanenta Bana 5-kitet behåller sina sju redan sanerade vapenfiler eftersom det är samma fysiska moduler. Totalt täcker 23 rena runtimefiler alla kit och nivåevents; inga generiska `TwinCannon`, `Broadside`, `Deploy`, `EnemyExplosion` eller `HullHit` återstår under aktiv Bana 6-simulering. Direktstart utan kampanjsave utrustar nu Magnetbredsidan så doftminornas signaturmekanik är tillgänglig; ärvda kampanjval och alla åtta testkit förblir oförändrade, och vanlig eld samt Kedjekartesch kan fortfarande förstöra minorna. Ljudmotorn laddar 120 effekter och 129 radiofiler utan fallback. Ett 12 500-bilders offentligt menystartsspår når Sista bettet efter vrakhav, duell och vägval (`d826b5bef652525ba8ef532ba6e022f59713d7651810f40502b115b1af39df8e`), och den officiella slutbossfixturen når `DE RØDE HUNDE BRUTNA` efter 3 600 bilder (`a2eb6e391e274a470249c608d6803aff973d8fde3893ca92231350d4566040a3`). Commit och push.
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
