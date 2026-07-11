# Bana 4 - Silverkroppen i skogen

Status: approved concept and implementation plan. This is a deliberate compact RTS intermission between `Öresunds järnkrona` and the renumbered `Fogdens tionde värld`.

## Dramatiskt löfte

Efter slaget vid Öresunds järnkrona går Karl CCLV lågt över en skogsmåne för att undvika fogdens spaning. Ebba Grip upptäcker en enorm sammanhängande silverkropp under kristallskogen. Kronarkivet kan inte nå Köpenhamn utan bränsle, ammunition och löner; spelaren måste landa flaggskeppet, upprätta ett ångdrivet fältläger och säkra en krigskassa innan danska mätpatruller hinner göra anspråk på fyndigheten.

Ebbas öppningsradio:

> Instrumenten visar en massiv kropp av silver under skogen. Landa, Karl. Vi säkrar krigskassan innan dansken hinner före.

Speltid: cirka 7–9 minuter. Tempot ska kännas som ett koncentrerat `Command & Conquer`-uppdrag, inte som en menysekvens mellan två shmup-banor.

## Perspektiv och karta

- Fast top-down-karta byggd för både 400x280 och 320x224.
- En skärmstor spelplan med lätt kamerapanorering inom en karta på ungefär 1.6 skärmar.
- Karl CCLV landar i sydväst och blir kommandocentral, reparationspunkt och nederlagsvillkor.
- Silverkroppen löper diagonalt genom kartans mitt som ljusa ådror under mörk skogsmark.
- Danska styrkor kommer från en gammal fogdeväg och ett tullfort i nordost.
- Kristallgranar, kolmilor och gruvvrak återanvänder bana 2:s visuella språk men visas ovanifrån.

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

Silver bryts automatiskt när en kraftsatt silverkross står inom räckvidd för huvudådern. Spelaren ska fatta placerings-, produktions- och försvarsbeslut, inte manuellt köra skördare i den första versionen.

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

- Ebbas silverreplik spelas över en kort automatisk landningssekvens.
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

## RTS-styrning

- Riktningsknappar flyttar markör eller panorerar kameran vid kartkant.
- `Z / Fire`: välj enhet eller byggnad, bekräfta order och placera bygge.
- `X / AltFire`: avmarkera, avbryt placering eller beordra markerad stridsgrupp att hålla position.
- `Start`: paus med separat bygg-/kontrollhjälp.
- Axel-/extra knappar växlar byggkategori när de finns; tangentbordsfallback får egna tydliga tangenter.
- Klickfri gamepad-first-design: inga små ikoner eller krav på musprecision.

Första implementationen använder gruppval per typ inom en liten radie, inte dragrektangel eller individuellt formationsmikro.

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

1. **Plan och kampanjplats:** renumbering, nivå-id och level-select-kort. Commit och push.
2. **RTS-skelett:** egen top-down-state, markör, kamera, grid, landad Karl och deterministisk tom karta. Commit och push.
3. **Kraft och ekonomi:** ångkraftverk, silverkross, skattkammare, byggplacering och HUD. Commit och push.
4. **Svensk produktion:** barack, fotkaroliner, djurhall och älgkaroliner. Commit och push.
5. **Danska vågor:** fem fiendetyper, stabil målsökning och försvarstorn. Commit och push.
6. **Toldhuset:** markboss, markbredsida, seger/resultat och balanspass. Commit och push.
7. **Presentation:** genererade assets, ånga, landningsanimation, musik, lokal radio och båda upplösningarna. Commit och push.

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
- RTS-HUD, radio, paus och byggplacering fungerar utan överlapp i både 400x280 och 320x224.
- Karl CCLV kan inte förstöras av en fiende som skjuter från utanför synlig karta.
- Resultatkortet visar säkrad krigskassa, överlevande trupper och öppnar vägen till `Fogdens tionde värld`.
