# Codexkriget · Instans 256

## Riktning

Codex Argentums fråga `ÅTERUPPTA KRIGET?` är kampanjens postgame-port. Svaret är spelbart:

- **JA · INSTANS 256** skriver kampanjsave schema 3, låser upp samtliga sju fälttåg som `OMSTRID` och öppnar den åttonde raden `CODEXKRIGET`.
- **NEJ · KRIGET SLUTAR** skriver att Köpenhamn är avslutat, visar ett kort slutkort och återvänder därefter till huvudmenyn utan att öppna Instans 256.

Schema 1 och 2 förblir läsbara. Schema 3 lägger till `CopenhagenCompleted`, `WarResumed` och den utbyggbara bitmasken `CodexWarDefeatedMask`.

## Första spelbara kedjor

Instans 256 har tre riktiga bosskedjor och en återgång till fälttågen:

1. **Blandad kedja · sex bossar:** Silverfogden, Blinde herden, Lemminkäinens skugga, Sagokonungen, Korrektorius och Konung Christians vrede.
2. **Silverkroppens tre:** de tre första bossarna ovan i sina riktiga gruv-/tempelarenor.
3. **Kungliga kedjan:** de tre Köpenhamnsbossarna ovan med riktiga marginal-, legend- och lagmekaniker.
4. **Till fälttågen:** tillbaka till den upplåsta kampanjmenyn.

Silverkroppen-kedjan använder `DungeonState`, befintliga boss-AI:n och riktiga assets. Vanliga fiender, loot och autosave tas bort lokalt i Codexkörningen; spelarens vanliga `autosave.json` skrivs aldrig över. Mellan bossarna återställs Karl, kameran och arenan deterministiskt. Den kungliga kedjan använder `CopenhagenGroundState` med suppressad marksave och går genom de befintliga rumsövergångarna.

## Nästa bossar

Bossregistret ska växa genom samma explicita start-/slutkontrakt, inte genom kopierad AI. När en boss tas in ska dess ursprungliga arena, träffytor, faser och musik bevaras medan kampanjskrivningar undertrycks.

Prioriterad fortsättning:

1. Tuonelas svan och Malmmodern Louhi från Silverkroppen.
2. Frederik Null, Øresunds Øje och Christians superfregatt från Köpenhamn.
3. Röda hundarnas maskfregatt från Snapphanens ed.
4. Tiondefogden, Öresunds järnkrona och Glimmingehus som separata skjutspelskedjor.

På sikt kan en seedad Codexlag väljas före varje kedja. Lagen får ändra en uttrycklig mekanik—exempelvis vikt, flygförbud eller ägande—men aldrig bossarnas grundläggande läsbarhet eller determinism.

## Acceptans

- JA öppnar Instans 256 och alla åtta huvudmenyrader; NEJ öppnar inte postgame.
- Schema 1, 2 och 3 kan läsas utan migrationstvång.
- Codexkriget skriver aldrig över ordinarie Silverkroppen- eller Köpenhamnssave.
- Samma val och input ger identiska framehashar i wide och legacy.
- Varje klar kedja OR:as in i `CodexWarDefeatedMask` utan att tidigare kampanjval tappas.
- Nya bossar läggs till via en explicit roster/start/progress-gren och återanvänder sin riktiga encounter.
