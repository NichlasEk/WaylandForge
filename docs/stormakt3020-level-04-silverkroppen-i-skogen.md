# Bana 4 - Silverkroppen i skogen

Status: campaign row 4 is publicly reachable as `STRID` and opens its separate RTS/dungeon submenu. The deterministic RTS mission, Møntgrevens Toldhus, evacuation, four dungeon depths, Lemminkäinens tempel, Louhis collapse and the completed elk/ship epilogue form one continuous campaign mission. The end sequence waits for player confirmation, then returns to menu music with Fogdens tionde värld selected. Economy, production, combat, packed animation families, local effects and event-driven Swedish/Danish radio remain isolated under level id `3`; broader balance polish is still allowed without blocking sequential campaign play.

## Dramatiskt löfte

Efter slaget vid Öresunds järnkrona går Karl CCLV in över en skogsmåne för att undvika fogdens spaning. I vakuum hävdar Karl att han känner lukten av silver. Ebba Grip invänder att det är omöjligt, kontrollerar motvilligt instrumenten och hittar en enorm sammanhängande silverkropp under kristallskogen. Kronarkivet kan inte nå Köpenhamn utan bränsle, ammunition och löner; spelaren måste landa flaggskeppet, upprätta ett ångdrivet fältläger och säkra en krigskassa innan danska mätpatruller hinner göra anspråk på fyndigheten.

Öppningsutbytet:

> Karl: Jag känner silver nedanför oss.
>
> Ebba: Det finns ingen lukt i vakuum, Karl. Men instrumenten visar faktiskt en massiv silveråder under skogen. Jag sätter ner er.

Speltid: cirka 7–9 minuter. Tempot ska kännas som ett koncentrerat `Command & Conquer`-uppdrag, inte som en menysekvens mellan två shmup-banor.

## Perspektiv och karta

- Fast top-down-karta byggd för både 400x280 och 320x224.
- En skärmstor spelplan med lätt kamerapanorering inom en karta på ungefär 1.6 skärmar.
- Karl CCLV landar i sydväst och blir kommandocentral, reparationspunkt och nederlagsvillkor.
- Silverkroppen löper diagonalt genom kartans mitt som ljusa ådror under mörk skogsmark.
- Danska styrkor kommer från en gammal fogdeväg och ett tullfort i nordost.
- Kristallgranar, kolmilor och gruvvrak återanvänder bana 2:s visuella språk men visas ovanifrån.
- Den spelbara kartan använder sexton riktiga propfamiljer: fyra trädtyper, silverhällar, sten/stubbe/buskage och en separat dansk front med spärrar, lyktor, mätstativ, skattekistor, gruvvagn och hjulspår. Alla placeras deterministiskt och Y-sorteras visuellt utan kollisionspåverkan.
- Skogsmarken är en sömlös barr/mossa/rot-platta utan prototypcirklar. Silverådern använder separata glödande sprick-, gren- och malmknutssprites, och Karl landar på en utfälld blågul/mässingsplattform.
- Stora träd, stenar, silverhällar, spärrar, kistor och gruvvagnar blockerar truppsteg och byggplacering. Simuleringen provar stabila vänster/höger-sidosteg runt terrängen; småbuskar, stubbar och markspår förblir passerbara.

## Kärnloop

```text
landa Karl CCLV
  -> placera ångkraftverk
  -> kraftsätt silverkross och skattkammare
  -> bygg barack
  -> håll första danska räden
  -> bygg djurhall
  -> träna älgkaroliner
  -> res försvarstorn
  -> bryt fogdens belägring och slå ut tullfortet
```

Silver bryts automatiskt när en kraftsatt silverkross står inom räckvidd för huvudådern, men blir inte disponibelt eller bärgat förrän en synlig gruvmas har burit en silverlåda från krossen till Karl. Tre deterministiskt fasförskjutna arbetare går i skytteltrafik; varje lossad låda ger 40 disponibelt silver och 40 på den separata bärgningsmätaren. `1200` bärgat silver uppfyller uppdragets ekonomiska vinstvillkor. Spelaren ska fatta placerings-, produktions- och försvarsbeslut, inte manuellt detaljstyra gruvarbetarna.

## Byggnader

### Karl CCLV - landad kommandocentral

- Startbyggnad och nederlagsvillkor.
- Levererar ett litet startlager silver och byggnadsradie.
- Reparerar närliggande svenska enheter långsamt om kraft finns.
- Skeppets kanoner är låsta mot markmål tills sista anfallsvågen.

### Ångkraftverk

- Första obligatoriska placeringen.
- Stor kopparpanna, två svänghjul och vita ångstötar.
- Ger 100 kraft. Skada sänker effekt innan byggnaden förstörs.
- Överbelastning ger långsammare produktion, aldrig slumpmässiga avstängningar.

### Silverkross och skattkammare

- Kostar kraft men ingen ytterligare silver efter tutorialplaceringen.
- Omvandlar huvudådern till en jämn deterministisk silverpuls.
- Har synliga transportvagnar mellan kross och Karl, men de är presentation och inga eskortobjekt.

### Karolinerbarack

- Tränar fotkaroliner i små fyrmannagrupper.
- Billiga, disciplinerade och bäst när de står still i salvläge.
- Krävs innan djurhallen kan byggas.

### Djurhall

- Ett pansrat stall med höga portar, ångvärme och blågula seldon.
- Tränar **älgkaroliner**: karoliner på bepansrade älgar.
- Älgkaroliner är dyra, snabba och kan rusa igenom lätt infanteri, men är sårbara för pikar och kanontorn.

### Försvarstorn

- Kort räckvidd, tydlig blågul eldsektor och lång omladdning.
- Kräver kraft; tappar rotation men inte fysisk hitbox vid strömbrist.
- Kan uppgraderas en gång till dubbel karolinsk muskötorgel i sista byggskivan.

## Svenska enheter

- **Fotkaroliner:** fyrmannagrupp, salveld, håll-position-bonus.
- **Älgkaroliner:** snabb chockkavalleri med rusningsattack och tung svängradie.
- **Fältmekaniker:** en ensam tutorialenhet som reparerar första kraftverket; inte fritt producerbar i första versionen.
- **Karl CCLV:s markbredsida:** engångsförmåga i finalvågen, upplåst av full skattkammare.

## Danska galna trupper

De ska vara absurda i koncept men spelas med samma allvar som resten av världen.

- **Toldstormere:** rödvita indrivare som springer rakt mot kraftnätet med krokbössor och beslagssigill.
- **Regnskabspikenerer:** täta pikblock som stoppar älgrusningar men faller för disciplinerad karolinsk salveld.
- **Møntmastiffer:** mekaniska röda jakthundar som ignorerar byggnader och jagar svenska grupper.
- **Krudtgrise:** låga bepansrade vildsvin med kruttunnor; tydligt glödande stubin, exploderar mot torn eller om de skjuts för sent.
- **Fogdens orgelvagn:** långsam kanonvagn som tvingar spelaren ur en ren tornmur.

## Uppdragsstruktur

### 0:00–1:20 - Landstigningen

- Omloppsbilden låser styrningen medan Karl påstår sig känna silver och Ebba svarar med en ny seedlåst replik.
- Karl CCLV går ner över skogen och fäller ut landningsplattan.
- Karl rider själv från skeppet på en blågul älg, följer ådern och stannar för att få vittring innan RTS-styrningen öppnas.
- Karl CCLV fäller ut landställ och byggnadsradie.
- Spelaren placerar ångkraftverk och silverkross på två tydligt markerade platser.
- Inga fiender innan båda byggnaderna är giltigt placerade.

### 1:20–2:40 - Första indrivningen

- Bygg barack och träna första fotkarolinerna.
- Toldstormere kommer längs fogdevägen i två läsbara grupper.
- Ebba förklarar kraftunderskott och håll-position utan att stoppa tiden.

### 2:40–4:30 - Djurhallen

- Djurhall låses upp när första räden är besegrad.
- Första älgkarolinen demonstrerar rusning mot en ensam dansk mätpost.
- Regnskabspikenerer och møntmastiffer lär spelaren att blanda enheter.

### 4:30–6:30 - Belägringen

- Försvarstorn blir tillgängliga.
- Krudtgrise angriper tornlinjen medan orgelvagnen skjuter över vanligt infanteri.
- Ett andra kraftverk är valfritt men ger säkrare torn och snabbare produktion.

### 6:30–9:00 - Fogdens anspråk

- Danska tullfortet fäller ut en mobil indrivningsmaskin: **Møntgrevens Toldhus**.
- Toldhuset fungerar som markboss och producerar blandade eskorter tills dess två sigillmaster förstörs.
- Full skattkammare låser Karl CCLV:s enda markbredsida; den river fortporten men dödar inte bossen automatiskt.
- Seger kräver förstörda sigillmaster och kärna. Överlevande svenska trupper samlas vid silverådern inför resultatkortet.
- Segerkortet kräver både minst `1200` bärgat silver och ett förstört Toldhus; bossen kan alltså inte rusas utan säkrad krigskassa.

## RTS-styrning

- Riktningsknappar flyttar markör eller panorerar kameran vid kartkant.
- `Z / Fire`: välj enhet eller byggnad, bekräfta order och placera bygge.
- `X / AltFire`: avmarkera, avbryt placering eller beordra markerad stridsgrupp att hålla position.
- `Start`: paus med separat bygg-/kontrollhjälp.
- Höger musknapp: klicka en grupp eller dra en markeringsruta runt valfri blandning av svenska trupper. Högerklick på tom mark ger ett befintligt urval en gemensam formationsorder.
- Vänster musknapp: primärhandling vid pekaren; placera aktuell byggnad eller aktivera produktion/objekt precis som `Z / Fire`.
- Axel-/extra knappar växlar byggkategori när de finns; tangentbordsfallback får egna tydliga tangenter.
- Klickfri gamepad-first-design: inga små ikoner eller krav på musprecision.

Gamepad använder gruppval per typ inom en liten radie. Musläget skickas genom Stormakt-slottens utökade, bakåtkompatibla stdio-paket och ger fri dragrektangel; andra externa kärnors äldre fembytespaket ändras inte.

## Determinism och simulation

- Egen fast seed och fast tick precis som shmup-banorna.
- Byggnader snäpper till ett grovt rutnät; samma inputsekvens ger samma placering.
- Silverinkomst, produktion, fiendevågor och attackmål är helt deterministiska.
- Fiender väljer mål via stabil prioritet: akut hot, kraft, produktion, Karl CCLV.
- Pathfinding använder liten grid med stabil grannordning; inga fysikknuffar får avgöra resultat.
- Presentationståg, ånga och skogsrörelse påverkar aldrig navigation eller träffytor.

## Musik och radio

- Eget fältspår: långsammare karolinsk marsch med ångslag, mörk nyckelharpa och gradvis mässing.
- Byggfasen är återhållen; danska vågor lägger till pukor och fogdehorn utan att byta låt abrupt.
- Obligatoriska radiokort:
  - Ebba upptäcker silverkroppen och ger landningsorder.
  - Ebba godkänner första ångkraften.
  - Dansk mätfogde hävdar kronans äganderätt till allt silver, även det som ännu ligger i marken.
  - En älgkarolin rapporterar att djurhallen är stridsklar.
  - Ebba beordrar markbredsidan i finalen.

## Tekniska byggskivor

1. **Plan och kampanjplats:** renumbering, nivå-id och level-select-kort. **Landed 2026-07-11.**
2. **RTS-skelett:** egen top-down-state, markör, kamera, grid, landad Karl och deterministisk tom karta. **Landed 2026-07-11.**
3. **Kraft och ekonomi:** ångkraftverk, silverkross, skattkammare, byggplacering och HUD. **Landed 2026-07-11.**
4. **Svensk produktion:** barack, fotkaroliner, djurhall och älgkaroliner. **Landed 2026-07-11.**
5. **Danska vågor:** fem fiendetyper, stabil målsökning och försvarstorn. **Landed 2026-07-11.**
6. **Toldhuset:** kartan sträcks till drygt två skärmbredder; den mobila markbossen, två sigillmaster, öppnad port, kärnhälsa, dansk förstärkningsproduktion och det kombinerade segervillkoret är spelbara. Karl CCLV:s särskilda markbredsida och slutligt resultatkort återstår. **Major boss slice landed 2026-07-12.**
7. **Presentation:** genererade byggnads- och truppassets, arbets-/anfallsanimationer, nio lokala stridsljud, sex svenska/danska händelseradior samt egen 84 BPM ångdriven karolinsk RTS-fältmarsch. 400x280 och 320x224 headless-verifieras. **Landed 2026-07-11.**
8. **Berättande insertion:** egen omloppsbild med en texturerad skogsmåne, kratrar, miniatyrbarrskog och synliga silverådror, Karls porträtt och syntetiska rollröst med seeds `302028`/`302128`, Ebbas vakuuminvändning med rollseed `302121`, landning och automatisk älgritt till den synliga silverådern före första byggordern. **Landed 2026-07-16.**

## Icke-mål för första spelbara versionen

- Ingen fri skirmish, multiplayer eller karteditor.
- Ingen generell fraktions-AI som bygger en hel dansk bas.
- Ingen dimma över hela kartan; kristallskogen skymmer lokalt men får inte dölja angreppstelegrafer.
- Ingen individuell soldatmikro, veterangrader eller permanent teknikträd.
- Ingen mus krävs.
- Inga kampanjresurser sparas ännu; krigskassan är dramatisk och resultaträknad tills kampanjsparning byggs senare.

## Acceptanskriterier

- Bana 4 kan startas och avslutas utan att shmup-state läcker in eller ändrar bana 1–3.
- Spelaren kan förstå och genomföra kraftverk → silverkross → barack → djurhall utan extern instruktion.
- Älgkaroliner läser omedelbart som blågult chockkavalleri även i 320x224.
- Varje dansk enhet har unik silhuett och motmedel; inga vågor löses optimalt av en enda enhetstyp.
- Strömbrist stoppar produktion/torn läsbart men låser aldrig uppdraget permanent.
- Varningsljud och marktelegrafer ger minst 45 frames före krudtgrisexplosion och tung orgelvagnssalva.
- Samma seed och input ger samma silvermängd, produktion, målval, döda enheter och resultatbild.
- HUD skiljer disponibelt `S` från faktiskt bärgat `B`; endast lossade gruvmaslaster ökar båda värdena.
- RTS-HUD, radio, paus och byggplacering fungerar utan överlapp i både 400x280 och 320x224.
- Karl CCLV kan inte förstöras av en fiende som skjuter från utanför synlig karta.
- Resultatkortet visar säkrad krigskassa, överlevande trupper och öppnar vägen till `Fogdens tionde värld`.
