# Stormakt 3020: Lemminkäinens tempel

## Beslut

Silverkroppen fortsätter efter RTS-segern och muterar till ett isometriskt/top-down hack-and-slash-avsnitt i fyra underjordiska akter. Det är fortfarande kampanjrad 4 och samma uppdrag; spelaren skickas inte tillbaka till nivåmenyn mellan gruvorna.

Arbetstitel: **Lemminkäinens tempel**.

Slutboss: **Louhi, Silverbergets häxa**, i tre faser. Den blinde herden och Svanen i Tuonela är de två sista tröskelväktarna före henne.

## Visuell kompass

Den tillhandahållna boss-moodboarden sätter en stark riktning utan att vara en färdig spritebeställning:

- kolsvart malm och våt, nästan organisk gruvmark;
- kallt silverblått ljus, runelektricitet och svart vatten;
- små svenska gulddetaljer som håller Karl visuellt förankrad;
- monumentala, lättlästa bosssilhuetter snarare än effektdimma;
- 16-bitarsinspirerad tydlighet med modern detaljnivå och konsekvent top-down-perspektiv.

Bosskedjans fem visuella pelare blir **Lemminkäinens skugga**, **Silverkroppen**, **Den blinde herden**, **Svanen i Tuonela** och **Louhi**. Danska befäl och sprängmästare används som jordnära minibossar mellan dem.

## Övergång från RTS

Efter den befintliga tempelavslöjningen stannar Karl ovanför landningsplattan. Resultatkortet ersätts på sikt av en speltypsövergång:

> **EBBA GRIP:** Karl. Det är bäst att du går ner i gruvan själv. Arbetarna vägrar. De säger att berget har börjat ropa deras namn.

Karl sänks ner vid sprickan. Skeppsljudet försvinner ovanför honom, musikens marsch bryts ner till slagverk, kedjor och låg kör. Spelaren får kontroll först när Karls stövlar når gruvgolvet.

Övergången behöver vara skippbar efter första visningen, men aldrig skippa nödvändig tillståndsinitiering.

## Ton och spelkänsla

- Diablo II-lik läsbarhet och tyngd, men Stormakt 3020:s svenska militärscience-fiction och mörka humor.
- Ett fullvärdigt lootspel med många fynd, tydlig jämförelse och rustningsdelar som syns på Karl. Inventoryskärmen pausar striden så sortering aldrig blir ett realtidstvång.
- Gruvan börjar historisk och materiell: trä, järn, talg, lera och danska störpatruller.
- Varje akt tar ett steg bort från verkligheten. I akt 4 är det uppenbart att templet är äldre än både Sverige och Danmark.
- Strid ska kännas brutal men begriplig: tydliga upptakter, träffreaktioner, kort hit-stop, blodfri metall/aska/silvereffekt och starka ljudsignaturer.

## Kärnloop

1. Utforska ett kompakt rum eller en korridor.
2. Läs fiendens upptakt och välj position.
3. Använd lätt attack, tung attack och undanmanöver/parad.
4. Säkra silver, runfragment eller ett sällsynt utrustningsfynd.
5. Öppna nästa port, hiss eller schakt.
6. Nå en fast checkpoint före miniboss eller aktbyte.

Normala rum ska ta 30–120 sekunder och varvas med utforskning, kistor, hemliga gångar och förrådsplatser. Varje akt siktar på 25–45 minuter första gången. Fyra akter och Louhi ger ungefär 2–4 timmar för en rak första genomspelning, mer för den som kartlägger allt och jagar utrustningsset.

## Kontroller

Samma externa protokoll behålls; `DungeonState` tolkar det annorlunda än RTS.

| Handling | Tangent/handkontroll | Mus |
|---|---|---|
| Förflyttning | riktningar | vänsterklick på mark |
| Lätt attack/kombokedja | Z / primär | vänsterklick på fiende |
| Tung attack/utrustningsförmåga | X / sekundär | högerklick |
| Undanmanöver eller parad | Slow | extra musknapp eller Slow |
| Interagera/plocka upp | Z nära objekt | vänsterklick på objekt |
| Inventory | pausmeny/inventoryknapp | klick på ryggsäcksikon |

Vänsterklick får aldrig teleportera Karl. Det sätter ett gångmål och går över i attack först inom vapnets räckvidd. Hållna knappar ska kunna kedja handlingar utan att skapa flera attacker per bildruta.

## Karl till fots

Karl lämnar skeppet i en tung blå-gul karolinsk gruvrustning med pannlampa, kort mantel och ett vapen som kan byta silhuett.

Grundförmågor:

- **Karolinsk huggserie:** tre slag; det tredje bryter lätt gard.
- **Kungligt hammarslag:** långsamt områdesslag som skadar rustning och spräcker runsten.
- **Stålsteg:** kort undanmanöver med begränsade odödlighetsrutor.
- **Blå gard:** tajmad parad ger en kort motattackslucka, men kan inte hållas för alltid.

Startvapen: en kort officersvärja och en mekanisk gruvhammare. Spelaren väljer inte en permanent klass; utrustningen formar i stället spelstilen och kan bytas vid behov.

## Resurser, inventory och loot

Grundresurser:

- **Liv:** återställs delvis vid checkpoint och av sällsynt fältbrännvin.
- **Kraft:** byggs av träffar/parader och betalar tung attack.
- **Runfragment:** valuta för smide, omrullning av en affix och vissa tempellås.
- **Malmskrot:** fås när oönskad utrustning bryts ned och används för enkla förbättringar.

### Utrustningsplatser

- huvud;
- bröst;
- handskar;
- stövlar;
- bälte;
- amulett;
- ring vänster/höger;
- huvudhand;
- andra hand;
- relik.

Tvåhandsvapen blockerar andra hand. Vapen, hjälm, bröst och större reliker ändrar Karls renderade lager/silhuett. Småsmycken behöver inte synas på modellen men ska synas tydligt i HUD och inventory.

### Rutnätsryggsäck

Ryggsäcken är ett klassiskt rutnät där föremål upptar 1×1, 1×2, 2×2 eller 2×3 rutor. Grundstorlek är 10×6 och kan byggas ut en gång genom en gruvfogdeuppgift. Musen drar och släpper; handkontroll flyttar en tydlig ankarmarkör och kan rotera tillåtna föremål.

En separat förrådskista nås vid hissar och fasta säkra läger. Den delas mellan de fyra gruvakterna inom samma genomspelning och rymmer samlingsset som spelaren inte vill bära.

Snabbplock lägger föremålet på första giltiga plats. Om ryggsäcken är full stannar föremålet på marken med en tydlig etikett; spelet får aldrig radera loot tyst.

### Vapenfamiljer

- **Värja:** snabb, bra parad och smal räckvidd.
- **Sabel:** breda hugg och stark kombokedja.
- **Yxa:** hög skada och rustningsbrott men lång återhämtning.
- **Stridshammare:** stagger, runbrott och områdesslag.
- **Spjut:** räckvidd och kontroll men svagt när Karl omringas.
- **Krutvapen:** pistol eller kort karbin i andra hand, ammunition som taktisk resurs och lång omladdning.

Varje familj har egen idle, gångsilhuett, lätt kedja, tung attack och träffljud. Ett nytt vapen är inte bara nya siffror.

### Sällsynthet och affixer

- **Järn:** normal basutrustning.
- **Karolinsk:** ett starkt fördefinierat bonusdrag.
- **Silverbunden:** två affixer och motstånd mot silvermagi.
- **Runristad:** sällsynt, tre affixer och möjlighet till setkoppling.
- **Pohjola/Unik:** handgjort namn, egen regel, fast konst och kort fyndtext.

Slumpaffixer väljs ur små familjespecifika pooler: skada, attackhastighet, gardbrott, kraft vid parad, liv vid avrättning, motstånd, fyndchans och färdighetsmodifierare. Namngeneratorn får inte stapla meningslösa prefix och suffix. Alla procentsatser visas i jämförelsepanelen.

### Set och unika fynd

Varje akt har ett litet samlingsset och 2–4 unika föremål. Exempel:

- **Bergslagens blårock:** rustningsset som stärker gard och hammare.
- **Fogdens sista räkenskap:** dansk klinga, sigillring och krutbälte med riskfylld fyndbonus.
- **Tuonelas färd:** mörkt set som gör undanmanöver längre men sänker läkningen.
- **Pohjolas kronspillror:** slutspelsset som växlar kraft mellan silver och åska.

Bossar släpper minst ett garanterat bosspecifikt föremål första gången. Hemliga rum och elitfiender använder den seedade loottabellen. Samma savestate ska alltid ge samma redan bestämda drop; laddning får inte slå om loot.

### Smide och nedbrytning

Vid hissläger kan spelaren:

- bryta ned föremål till malmskrot;
- höja ett favoritföremål ett begränsat antal nivåer;
- byta en affix mot runfragment;
- låsa en affix innan byte till högre kostnad;
- sätta en funnen runa i ett tomt uttag.

Smidet ska ge kontroll utan att ersätta fyndglädjen. Unika föremåls huvudregel kan aldrig rullas bort.

Silvret från RTS förblir säkrad krigskassa och blir inte vanlig dungeon-guldvaluta. Gruvans ekonomi använder runfragment och malmskrot så RTS-segern inte trivialiserar hela lootkurvan.

## Akt 1 – Den övergivna gruvan

**Bild:** trovärdig gruva, timmerstöd, räls, lyktor, vinschar, fuktig sten och färska danska fotspår.

**Berättelse:** arbetarna har flytt. Den saknade gruvfogdens markeringar leder nedåt, men danska störtrupper har följt efter för att återta silvret.

**Fiender:**

- Kronans störknekt: sabel, enkel tredelad attack.
- Tullpikenerare: håller korridorer och kräver sidsteg/gardbrott.
- Krutläggare: placerar en tydlig stubinmina och försöker backa.
- Förvirrad gruvarbetare: räddas genom att bryta silverångans kontroll, inte dödas.

**Miniboss:** **Överfogdens sprängmästare**, med sköld, krutladdningar och rasande takbjälkar.

**Aktboss:** **Lemminkäinens skugga**, en silverblå krigarande som först härmar Karls senaste kombination och därefter visar en äldre, snabbare version av samma vapenkonst. När skuggan besegras böjer den knä i stället för att dö; Karl har bevisat att han får gå djupare.

**Nytt system som lärs ut:** grundstrid, interaktion, räddning och första checkpointen.

## Akt 2 – Djupgruvan

**Bild:** större bergrum, gamla hissar, silverångor, självdragande gruvvagnar och blåvitt damm som rinner uppåt.

**Fiender:**

- Silverblind arbetare: hör Karl och anfaller mot ljudets senaste position.
- Malmväktare: långsam kropp av järn och kristall; ryggrunan är svag punkt.
- Ånggast: glider mellan sprickor och materialiseras före slag.
- Besatt gruvvagn: rusar längs läsbara rälslinjer och kan styras in i fiender.

**Miniboss:** **Tuonelas Silvervakt**, en uråldrig rustning med lykta i bröstet och ett spjut som förmörkar delar av rummet.

**Aktboss:** **Silverkroppen**, gruvans malmväktare som växer ur huvudådern. Den skiftar mellan tung stenrustning och blottade ledande silvernerver. Spelaren använder gruvvagnar för att spräcka pansaret och slåss sedan nära innan kroppen hinner stelna igen.

**Nytt system:** miljöfara, rustningsbrott och fiender som inte följer mänsklig rörelselogik.

## Akt 3 – Den förbannade gruvan

**Bild:** timret har vuxit ihop med svart sten. Runor reagerar på Karl, verktyg sitter fast i väggarna och fjädrar ligger där inga fåglar kan leva.

**Fiender:**

- Runbunden dödgrävare: reser sig igen om dess runsten inte krossas.
- Tuonelahund: jagar i par och försöker driva Karl mot svart vatten.
- Svanens skugga: flygande silhuett som stjäl kraft och lämnar vit spegelbild.
- Malmmoderlarv: föds ur silverådern och stärker närliggande väktare.

**Aktboss:** **Den blinde herden**, en stilla bågskytt/spjutkastare omgiven av mörka stenfår. Han lyssnar efter steg, släcker rummet, markerar ljudvågor i golvet och kan besegras genom att lura hans skott mot tre runpelare.

**Nytt system:** mörker, ljudtelegraph, återuppståndelse och den första tydliga kontakten med Louhi.

## Akt 4 – Lemminkäinens tempel

**Bild:** ett omöjligt heligt rum under gruvan. Svart vatten utan reflektion, en lång stenbank, silveraltare, svanmotiv, frusna vågor och en artificiell stjärnhimmel i taket.

Gruvans raka geometri upphör. Rummen binds ihop av korta handbyggda tempelgårdar och tre sigill som öppnar altaret. Den saknade gruvfogden hittas vid första sigillet, omtöcknad men levande; hans räddning fullbordar RTS-berättelsens löfte.

**Tempelväktare:** uppgraderade kombinationer av akt 2–3, men inga nya småfiender efter den sista checkpointen. Vägen till Louhi ska bygga förväntan, inte tömma spelaren.

**Tröskelboss:** **Svanen i Tuonela**, en svart metallisk svan i ett spegellöst vattenrum. Vingarna skapar sektorer av svart vatten, halsens hugg följer en tydlig båge och runringar visar var nästa dödsvåg når. Svanen ska kännas sorglig och helig snarare än ond; när den faller öppnas vägen till Louhi utan vanlig segerfanfar.

## Slutboss – Louhi, Silverbergets häxa

### Fas 1: Louhi av Pohjola

Louhi står vid silveraltaret i tung svart dräkt, hornkrona och metalliska fjädrar. Hon använder stav, runfält och korta teleportsteg genom svart vatten.

- Tre tydliga stavkombinationer.
- Silverrunor tänds i den ordning de exploderar.
- En förbannelse gör Karls nästa giriga attack långsammare; parad eller väntan bryter den.

### Fas 2: Pohjolas järnfågel

Louhi river loss altarets kraft och blir en väldig järnörn/ravnlik krigsvarelse.

- Sveper över rummet med synlig skugga före träff.
- Slår sönder delar av arenan och öppnar svart vatten.
- Tappar metallfjädrar som Karl kan slå tillbaka mot vingarnas leder.

### Fas 3: Templets Malmmoder

Den skadade Louhi binds samman med silveraltaret. Överkroppen är häxa, resten ett växande nät av silverådror under golvet.

- Fyra altarhjärtan matar hennes sköld.
- Räddade gruvarbetare påverkar inte striden direkt, men gruvfogdens radiosignal avslöjar vilket hjärta som är äkta.
- Slutfönstret kräver hammarslag mot altaret följt av en kort huggserie mot Louhi.
- Vid seger rinner silvret tillbaka ned i berget i stället för att explodera som vanlig bossloot.

## Berättelse och radiosändningar

Ebba kan nå Karl nära schakt och metallkonstruktioner men tappar kontakt i djupa stenrum. Det ger naturliga fönster för dialog utan ständig radioprat.

Viktiga repliktillfällen:

1. Ebba skickar ner Karl ensam.
2. Karl hittar första överlevande arbetaren.
3. Gruvfogdens nödsignal hörs för första gången i akt 2.
4. Den blinde herden nämner Louhi utan att förklara henne.
5. Gruvfogden hittas i templet.
6. Ebba förstår att silverfyndigheten var ett lås, inte en skatt.

Röstcasting görs som egen poleringscheckpoint. Nuvarande Ebba-röst får fungera som tonreferens, men nya huvudrepliker ska granskas före slutmix.

## Teknisk arkitektur

### Separat simulering

Inför `DungeonState` bredvid `RtsState`, inte inuti den. Kampanjrad 4 får en överordnad fas:

```text
SilverkroppenPhase
├── Rts
├── Evacuation
├── Descent
└── Dungeon
```

När `Dungeon` är aktivt anropas endast `StepDungeon` och `DrawDungeon`. RTS-enheter, byggmus, vågor och ekonomi fryses och får inte återanvändas som dungeon-entiteter.

### Värld

- Handbyggda rum från små tilelager: golv, vägg/kollision, dekor, spawn och trigger.
- Fast seed per akt för deterministiska effekter och drops.
- Rumsportar låser under strid och öppnar när den lokala encounter-listan är tom.
- Kamera följer Karl mjukt men begränsas av rummets gränser.
- Alla kroppar använder cirkelkollision mot tilegrid och varandra; inga fiender genom väggar eller pelare.

### Strid

- Attackdata hålls i tabeller: start, aktiva rutor, återhämtning, räckvidd, båge, skada, stagger och kraftkostnad.
- En attack kan träffa samma mål högst en gång per sving.
- Fiende-AI har tillstånden `Idle`, `Approach`, `Telegraph`, `Attack`, `Recover`, `Stagger`, `Dead`.
- Hit-stop påverkar dungeon-simuleringen men aldrig audio eller extern protokolläsning.
- Döda fiender tas bort efter sin animation; deras loot ägs av rummet.

### Rendering

- Top-down 3/4-perspektiv med Y-sortering för Karl, fiender, props och effekter.
- Väggar delas i bak- och framkant så Karl kan gå bakom dem utan att försvinna.
- Skuggor är fasta sprites; inga mjuka generiska cirklar.
- Varje akt får ett golvset, ett väggset, 6–8 props och ett effektatlas innan mängden fiender ökas.

### Item- och inventorymodell

Statiska `ItemDefinition` beskriver basföremål, footprint, utrustningsplats, vapentabell, tillåtna affixpooler och sprite-lager. Varje drop skapar en `ItemInstance` med stabilt 64-bitars-id, bas-id, kvalitetsgrad, item level, affixvärden, uttag och eventuell set/unik identitet.

Ryggsäck och förråd lagrar item-id samt rutnätsposition och rotation; utrustningsplatser lagrar bara item-id. Ett item får exakt en ägare: mark, ryggsäck, förråd eller utrustningsplats. Flytt valideras innan det gamla läget frigörs så ett avbrutet drag aldrig kan förlora föremålet.

Loot bestäms när encounter-instansen skapas eller fienden först spawnar, inte när spelaren öppnar kistan eller dödar fienden. Därmed kan savestate-laddning aldrig användas för att rulla om samma drop. Inventoryvyn använder en separat pausad UI-state och muterar inga stridslistor direkt.

### Savestates och persistens

Systemet är en hybrid mellan pålitliga kampanjcheckpointar och riktiga manuella savestates.

#### Platser

- **Autosave:** en roterande plats som skrivs vid nedstigning, hiss, aktbyte, räddad nyckelperson och precis före varje bossport.
- **Manuell 1–3:** spelaren väljer plats i pausmenyn. Varje plats visar akt, rum, speltid, liv, relikikoner och en liten skärmbild.
- **Snabbspara/snabbladda:** kan senare mappas på tangentbord, men får inte vara enda sättet att nå funktionen.

#### Exakt snapshot

Ett manuellt läge innehåller:

- `SilverkroppenPhase`, akt, rum och kamerans logiska position;
- Karls position, riktning, liv, kraft, pågående action och återstående actionrutor;
- alla fienders typ, AI-läge, position, liv, cooldown, stagger och aktuella mål;
- projektiler, farozoner, loot, förstörbara objekt, portar och rumstriggers;
- samtliga item-instanser, exakt ryggsäcks-/förrådslayout, utrustningsplatser, markloot, reliker, runfragment, malmskrot och räddade arbetare;
- deterministiskt RNG-tillstånd och nästa stabila entitets-id;
- aktiv musikdel, atmosfär och relevanta berättelseflaggor.

Manuella savestates får tas i vanliga rum och under bossar. Sparkommandot köas till slutet av den aktuella simuleringsrutan så snapshoten aldrig hamnar mitt i en listmutation. Laddning återställer exakt speltillstånd men startar ljud på en ren takt/toning i stället för mitt i en WAV-sample.

#### Robust filformat

- Sökväg: XDG state-katalog, exempelvis `~/.local/state/waylandforge/stormakt3020/saves/`.
- Versionsmärkt payload med magi, schema, payloadlängd och checksumma.
- Atomisk skrivning till temporär fil, flush och rename; föregående fungerande fil behålls som `.bak`.
- Okända nyare scheman vägras med begripligt fel. Äldre scheman migreras explicit, aldrig genom att gissa fält.
- Sparfiler innehåller inga absoluta asset- eller installationssökvägar.

Autosave återställer ett rent rum eller läget före bossporten och är alltid den säkra återhämtningsvägen. Ett manuellt savestate kan återuppta mitt i en bossfas, men får aldrig skriva över autosaven. Vid korrupt manuell fil erbjuder spelet `.bak` och därefter autosave.

Död erbjuder `ÅTERUPPTA AUTOSAVE`, `LADDA LÄGE` och `TILL FÄLTTÅGET`. Det förhindrar att en dålig manuell sparning låser spelaren.

## Byggordning

### Checkpoint 1 – Mutationen

- Ersätt RTS-resultatets återvändsgränd med `Descent`.
- Ny Ebba-replik, nedstigning, Karl-till-fots idle/run och första gruvrum.
- Separat `DungeonState`, kamera, tilekollision och båda kontrollschemana.
- Save-schema, atomisk autosave och tre tomma manuella platser i pausmenyn.
- Ingen fiende ännu.

**Godkänt när:** spelaren kan vinna RTS-delen, gå ner utan omstart och röra Karl med tangentbord eller mus i ett kollisionssäkert rum; avslut och omstart återupptar det första gruvrummet från autosave.

### Checkpoint 2 – Inventory- och lootgrunden

- Fullskärmsinventory, 10×6-ryggsäck, utrustningsplatser och jämförelsepanel.
- Mus-drag, rotation, snabbflytt och handkontrollnavigering.
- `ItemDefinition`, stabila item-id:n, fyra grundvapen, rustningsdelar och affixrullning.
- Förrådskista, markloot, plocka upp/släppa och save/load av exakt layout.
- Tillfällig Karl-docka duger här; slutliga utrustningslager kommer med respektive assetpass.

**Godkänt när:** hundratals deterministiskt genererade flyttoperationer inte kan duplicera eller förlora ett item, och samma save återställer bytexakt layout och affixvärden.

### Checkpoint 3 – Ett komplett stridsrum

- Lätt kombokedja, tung attack, stålsteg, gard, hitboxar och hit-stop.
- Störknekt, pikenerare och tydliga träff/dödsanimationer.
- Rumsport, segertrigger, liv/kraft-HUD och stridsljud.
- Exakt manuell save/load av ett aktivt encounter inklusive RNG och fiende-AI.

**Godkänt när:** ett 60-sekunders encounter kan spelas om deterministiskt och ingen träff registreras dubbelt.

### Checkpoint 4 – Gruva 1 komplett

- 10–14 handbyggda rum, sidogångar, två hemliga rum, krutläggare, räddningsbar arbetare, sprängmästarens miniboss och Lemminkäinens skugga.
- Första setet, unika drops, hissläger, smide, checkpoint, död/återstart samt full musik- och miljöljudloop.
- Grafisk slutputs för akt 1.

**Godkänt när:** hela Gruva 1 kan spelas från RTS-seger till hiss utan utvecklargenväg.

### Checkpoint 5 – Djupgruvan

- 12–16 rum, silverånga, vagnar, nya väktare, Tuonelas Silvervakt och Silverkroppen.
- Aktbyte och andra uppgraderingsvalet.

### Checkpoint 6 – Den förbannade gruvan

- 12–16 rum, runåteruppståndelse, mörker/ljudsystem och Den blinde herden.
- Louhis första fulla framträdande.

### Checkpoint 7 – Templet och Louhi

- 8–12 tempelrum, svart vatten, tre sigill och gruvfogdens räddning.
- Svanen i Tuonela, Louhis tre faser, slutsekvens och kampanjövergång.

**Pågående 2026-07-13:** den första tempelsträckan är spelbar. Lemminkäinens skugga låser en fysisk
runbarriär; efter segern öppnas porten till en separat sigillgård med tre zonbundna väktare. Det första
sigillet renar förbannelse, lämnar ett runristat fragment, autosparar och etablerar att två sigill återstår.
Hälsotinkturer plockas nu upp som riktiga föremål, fyller ett fyrplatsers snabbälte före ryggsäcken och
dricks med `Q` i stället för att förbrukas automatiskt på marken. Nästa tempelslice börjar bakom den
andra förseglade porten; Svanen och Louhi ingår fortfarande inte i den landade delen.

Den första sigillgården har fått en egen tydligare tempelidentitet: två svarta väktarstatyer, ett lågt
offeraltare och både slutna och öppnade svansarkofager ersätter återanvänd gruvrekvisita. Föremålen är
fysiska hinder men står längs rummets kanter så att strid och vägen till sigillet förblir fria.

Inventoryföremål kan slängas med `D` eller den klickbara `SLÄNG`-knappen. Föremålet placeras på en
giltig golvpunkt framför Karl, får marken som enda ägare och ligger kvar i samma position genom autosave.
Utrustningsrutorna går nu också att klicka för att granska det burna föremålet. Ett klick på utrustningsbar
loot i ryggsäcken visar dess verkliga byte mot nuvarande plats med gröna plus, röda minus och grå nollor
för skada, rustning, kraft och förbannelsevärn. Ett andra klick på samma föremål utrustar det; byteskoden
frigör först kandidatens inventoryruta så att likstora föremål alltid kan byta plats även i en full ryggsäck.

### Checkpoint 8 – Balans, lootkurva och casting

- Röstcasting och slutmix för Ebba, herden, fogden och Louhi.
- Svårighetskurva, droprates, affixpooler, setbonusar, smideskostnad, checkpointkostnad, musergonomi och båda upplösningarna.
- Full direkt-WFEX-genomspelning och determinismkontroll.

## Första implementationen

Nästa kodslice ska endast vara **Checkpoint 1 – Mutationen**. Vi ska inte skapa fyra halvfärdiga gruvor samtidigt. När rörelse, kamera, kollision, nedstigning, autosave och Karl-spriten känns rätt bygger vi inventory-/lootgrunden. Först därefter bygger vi ett komplett stridsrum och använder kombinationen som kvalitetsribba för resten.
