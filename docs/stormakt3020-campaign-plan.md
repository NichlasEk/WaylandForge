# Stormakt 3020 - kampanjplan

## Grundidé

Stormakt 3020 är en sorgmättad, pampig vertikal shmup om ett skadat svenskt hjälteskepp som försöker återföra flottans kronarkiv genom ett sönderfallet framtida Norden. Danmark är den synliga rivalen, men kampanjen ska handla lika mycket om fogdevälde, tullmaskiner och gamla konflikter som om nationalflaggor. Humorn får finnas i namn och överdriven heraldik; världen och musiken spelas på allvar.

Varje bana får en egen `.md` innan kod, grafik eller ljud kopplas in. Planen ska låsa:

- dramatisk funktion och geografisk/futuristisk plats,
- tre bakgrundslager och minst ett miljölandmärke,
- introducerade fiender och marktorn,
- vågstruktur, mellansektion och bossfaser,
- musikroll och obligatoriska ljudeffekter,
- nya tekniska system samt uttryckliga icke-mål,
- verifierbara acceptanskriterier.

## Kampanjbåge

### Bana 1 - Återtåget över Stora Bält-nebulosan

Ett skadat Karl CCLV lämnar ett förlorat slag och måste korsa en söndersprängd gravitationsbro. Danska tullbojar, brotorn och fogdens indrivningsskepp lär spelaren läsa markhot och förstörbar infrastruktur. Boss: fogdeskeppet **Kronens Tiende**.

Plan: `docs/stormakt3020-level-01-stora-balt-nebulosan.md`.

### Bana 2 - Skånska skuggor

Flottan söker reservdelar bland mörka skogsasteroider och övergivna gruvmånar. Här introduceras framtidens snapphane **Sören Svartkrut**, en snabb maskerad kapare som angriper både svenska och danska konvojer. Han fungerar först som miniboss och flyr när spelaren bryter fogdens blockad.

Bakgrundsidé: svarta granformationer av kristall, röda norrskensband och brinnande kolmilor på små månar. Boss: den självgående fogdegaljonen **Glimminge Järn**.

### Bana 3 - Öresunds järnkrona

En tät mark- och bromiljö där spelaren flyger längs en enorm orbital ringbro. Marktorn, rörliga broklaffar, tågburna kanoner och laserfyrar skapar banans rytm. Sören kan skjuta bort ett annars dödligt batteri om spelaren skonade hans följare i bana 2.

Boss: en tvådelad brofästning, **Helsingör/Helsingborg**, som anfaller från varsin skärmkant och delar livsmätare.

### Bana 4 - Fogdens tionde värld

Infiltration av ett mekaniskt skattearkiv där beslagtagna skepp hänger i kedjor. Smalare korridorer, roterande tullportar och magnetiska myntminor ger en mer teknisk bana. Här avslöjas att fogdesystemet fortsätter kriget för att motivera sin egen existens.

Boss: revisionsmaskinen **Rigsregnskabet**, en modulär valvkoloss som bygger om sin rustning av indrivna skeppsdelar.

### Bana 5 - Snapphanens ed

Sören leder spelaren genom en storm av vrak och falska fyrar mot fogdens kommandokedja. Banan har valbara riskkorridorer: hjälp rebellflottan och få mindre eldkraft nu men stöd i finalen, eller rusa mot målet för högre poäng.

Boss/miniboss: först en hedersduell mot Sören, därefter gemensam strid mot jägarflottan **De Røde Hunde**.

### Bana 6 - Köpenhamns sista ring

Final över en barock rymdstad med spiror, dockor, urverk och en kronformad försvarsring. Tidigare val avgör vilka allierade skepp som syns i bakgrunden och vilka batterier som redan är utslagna.

Överfogden **Frederik Null** är banans näst sista konfrontation, först i ett pampigt tron-/slagskepp och sedan som den avskalade tullkärnan **Øresunds Øje**. När tullkärnan brister öppnas Köpenhamns kronring och spelaren tror för ett ögonblick att slaget är vunnet.

Då anländer **Kung Christians Superarmada** ur en gyllene hyperrymdsport. Det är kampanjens riktiga final: en kunglig rödvit superfregatt omgiven av flera lager specialiserade drönare, mindre fregattskärmar och en hel bakgrundsflotta som får himlen att kännas större än spelplanen.

#### Finalsekvens - Superarmadan

1. **Dannebrogsvingarna:** röda/vita jaktdrönare bygger roterande korsformationer. Skjut ett hörn för att öppna en flyktväg genom formationen. Räddade svenska skepp och Sörens snapphanar håller undan bakgrundsflankerna.
2. **Fregattskärmen:** två mindre skärmfregatter gör växelvisa bredsidor från kanterna medan reparationsdrönare försöker återställa deras kanontorn. De är delmål, inte fulla bossar.
3. **Kung Christians superfregatt:** flaggskeppet **KONG CHRISTIAN - SUPERFREGATT** fyller övre halvan av skärmen. Vapnen sitter i separata sektioner: drönarbuk, babords bredsida, styrbords bredsida och kronreaktor.
4. **Den kungliga salvan:** under 40 procent fäller fregatten ut en enorm vit korsram, låser skärmens fyra kvadranter i turordning och blandar långsamma kanonkulor med snabba drönarrusningar. Förstör korsramens fyra noder för att blotta kronreaktorn.
5. **Sista hållet:** när reaktorn öppnas går Karl CCLV och Sören in från varsin sida. Spelaren avlossar den avgörande bredsidan; fregatten bryts i flera stora, långsamma explosioner medan Superarmadan tappar formation och flyr tillbaka genom porten.

Kung Christian behöver inte visas som människa i första kampanjen. Hans närvaro bärs av vapensköld, ordertext och den orimligt stora fregatten; det sparar en personlig duell till en eventuell fortsättning.

Finalmusik: ett eget spår, arbetsnamn **Kung Christians Superarmada**, som börjar med en kort kunglig fanfar, går över i snabb sorgmättad bossmarsch och lägger till snapphanarnas kopparslag när allierade anländer. `kronans-sista-salva-v1.wav` kan användas som prototyp men ska inte vara slutlig finalmusik.

Segern slår sönder fogdemaskinen och tvingar Superarmadan till reträtt, men utplånar varken Danmark eller den danska kronan. Slutbilden visar Karl CCLV och snapphanarnas kapare mot den slocknade kronringen.

## Återkommande systemspråk

- Rött/vitt och mörkt järn markerar fogdens ordinarie styrkor.
- Svart, koppar och dämpat grönt markerar snapphanarnas tredje fraktion.
- Blå/gul heraldik reserveras för spelaren och räddade svenska enheter.
- Marktorn telegrapherar med en ljuskägla före skott; broar visar sprickor i två steg före kollaps.
- Bakgrunder ska röra sig i minst tre hastigheter men aldrig försämra projektilernas läsbarhet.
- Varje boss får ett eget musikspår eller en tydlig bossvariant, en namnskylt, minst två faser och en unik dödssekvens.
- Superarmadans drönare ska uppträda som läsbara formationer med svaga bakgrundsdubbletter; känslan av hundratals skepp får aldrig bli hundratals aktiva kollisionsobjekt.

## Byggordning

1. Bygg bana 1 som vertikalt snitt: tidslinje, bakgrund, markhot, boss och musikbyte.
2. Bryt ut dataformat för banor och vågor först när bana 1 bevisat vilka fält som faktiskt behövs.
3. Skriv nästa banas `.md`, granska den mot kampanjbågen och bygg därefter en bana i taget.
4. Commit och push efter plan, spelbar mittsektion och färdig boss för varje bana.
