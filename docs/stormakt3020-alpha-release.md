# Stormakt 3020 alpha

Detta är en tidig Linux x64-testversion för Wayland. Den innehåller egen
.NET-runtime, Stormakt-kärnan, spelassets och WaylandForges lilla ljuddaemon.
Musik, radio och effekter distribueras som Ogg Opus; de fria Opus-, opusfile-
och libogg-biblioteken samt deras licenstexter följer med i paketet.

## Start

1. Packa upp arkivet.
2. Öppna en terminal i den uppackade katalogen.
3. Kör `./start-stormakt-3020.sh`.

Launchern öppnar Stormakt direkt i WaylandForge och döljer de andra externa
kärnornas utvecklarknappar. Inget `EXT`-val behövs.

Piltangenter väljer och styr. Enter startar. Z skjuter och X använder
bredsidan. Menyn har `DEVLÄGE` och `DEVSKÖLD`; båda är av vid normal start.
Devläge låser upp testvägar och diagnostik. Devsköld hindrar spelarskada.

## Systemkrav

- x86-64 Linux med en aktiv Wayland-session;
- `libwayland-client`, `libwayland-cursor` och PipeWire 0.3;
- fungerande tangentbordslayout i Wayland.

Om ljuddaemonen inte kan starta går spelet fortfarande att köra, men utan
musik, röster och ljudeffekter. `SHA256SUMS` innehåller kontrollsummor för alla
filer i paketet.
