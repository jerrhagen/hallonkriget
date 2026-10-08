# Implementationsplan

Godot 4 med C#, en fristående deterministisk simuleringskärna och låstegsmodell för multiplayer. Planen är skriven för att köras med Claude Code, en modul i taget.

## Arkitektur

*[Diagram i originaldokumentet: arkitektur · kommandon in, tillstånd ut]*

Gränssnittet ändrar aldrig spelet direkt. Ett klick blir ett kommando, kommandot går till nätverket (även i enspelarläge, där nätverket bara är en kö), och simuleringskärnan kör kommandona i samma ordning på alla datorer. Vyn läser bara. Datorspelaren bor inne i kärnan och ger sina kommandon samma väg. Det är en enda regel, och den gör multiplayer, repriser, sparfiler och automatiska tester till samma sak.

## Teknikval

| Del | Val | Varför |
| --- | --- | --- |
| Spelmotor | Godot 4.4 med C# (.NET 8) | Gratis, öppen källkod, bra 2D, exporterar till Windows och Mac med ett klick. C# ger en riktig typkontroll som Claude Code drar nytta av |
| Simuleringskärna | Ett eget C#-bibliotek utan Godot-beroenden | Kan testas och köras utan grafik, tusen matcher på några minuter. Determinism är lättare att garantera när motorn inte är inblandad |
| Tester | xUnit | Standard i .NET, körs med ett kommando |
| Speldata | JSON-filer i en datamapp | Varor, byggnader, enheter, byggordningar och kampanjuppdrag ändras utan att kompilera |
| Nätverk | Godots ENetMultiplayerPeer, med egen låstegslogik ovanpå | ENet sköter anslutning och pålitliga paket. Låstegslogiken är vår egen, cirka 500 rader |
| Grafik | SVG-källor renderade till PNG med resvg, klippdockor i Godots Skeleton2D | Vektorgrafik är det jag kan rita; motorn animerar |
| Kartor | Godots TileMapLayer plus en egen kartfil i JSON | Kartan är speldata, inte en scen |
| Versionshantering | Git och GitHub, privat repo | Claude Code arbetar direkt i repot |
| Bygg och CI | GitHub Actions kör testerna vid varje push | Determinismtestet måste köras på både Windows och Linux för att fastna tidigt |

Två saker väljs bort med flit. **GDScript** är bekvämt men saknar typsäkerhet och gör simuleringen långsam. **Godots inbyggda MultiplayerSynchronizer** passar inte, eftersom den synkar tillstånd i stället för kommandon.

## Kodens struktur

Ett repo, fyra projekt. Simuleringen får aldrig referera till Godot, och det kontrolleras av ett test.

```
hallonkriget/
  CLAUDE.md               regler för Claude Code, se avsnittet längre ner
  data/                   speldata i JSON
    goods.json            54 varor
    buildings.json        45 byggnader med recept och cykeltider
    units.json            12 enheter med utrustning och värden
    food.json             matnivåer och humörvärden
    trade.json            lanthandelns priser
    ai/                   byggordningar per läger och svårighetsgrad
    maps/                 kartor (terräng, startpositioner, resurser)
    campaign/             tio uppdrag: karta, mål, berättelse
  sim/                    simuleringskärnan, rent C#, inga Godot-beroenden
    Map/                  rutnät, terräng, sökvägar (A*), stigar
    Economy/              varor, lager, begäran och erbjudande, leveranser
    Buildings/            byggnadstyper, byggande, produktion
    People/               personer, yrken, humör, mat, utbildning
    Military/             grupper, formationer, strid, skräms, belägring
    Ai/                   datorspelaren
    Commands/             alla kommandon spelaren kan ge, och deras serialisering
    Determinism/          fast punkt, slump, kontrollsumma
    GameState.cs          hela tillståndet, Tick(), Hash()
  sim.tests/              xUnit: enhetstester, determinismtest, ekonomitest
  sim.cli/                kör matcher utan grafik, skriver statistik
  game/                   Godot-projektet
    Scenes/               karta, byggnader, personer, gränssnitt, menyer
    View/                 läser GameState och ritar
    Net/                  lobby, låstegsloop, ENet
    Audio/
  art/                    SVG-källor och renderingsskript
    buildings/ people/ animals/ goods/ terrain/ ui/
    palette.json
    render.py             SVG till PNG i två storlekar
  docs/                   det här dokumentet exporterat, plus beslut
```

Regeln för Claude Code är att varje mapp under `sim/` är en modul med ett tydligt gränssnitt och egna tester, så att en arbetssession kan hantera en modul i taget utan att läsa allt.

## Simuleringskärnan

Kärnan är en funktion: `GameState.Tick(kommandon)`. Samma tillstånd och samma kommandon ger alltid exakt samma nya tillstånd, på alla datorer. Allt annat i spelet bygger på det.

**Regler för determinism**, som står i CLAUDE.md och kontrolleras av tester:

1. Inga flyttal (`float`, `double`) i `sim/`. Positioner är heltal i 1/256 ruta, hastigheter och procent är heltal. Ett test letar efter förbjudna typer i källkoden.
2. En enda slumpgenerator (xorshift64) som ingår i tillståndet och seedas vid matchstart. `System.Random` är förbjuden.
3. Ingen iteration över `Dictionary` eller `HashSet` där ordningen påverkar resultatet. Listor och arrayer med fasta id-nummer.
4. Ingen klocka, ingen `DateTime`, inga trådar. Tiden är tickräknaren.
5. Ingen referens till Godot. Ett test kontrollerar projektets beroenden.
6. `Hash()` räknar en kontrollsumma över hela tillståndet på under en millisekund och körs var 10:e tick i multiplayer.

**Tick.** 10 per sekund. Varje tick gör, i fast ordning: tillämpa kommandon, flytta personer ett steg längs sina vägar, uppdatera produktion, para ihop leveranser, uppdatera humör, avgör strider, kör datorspelare, öka tickräknaren.

**De viktigaste datastrukturerna:**

| Struktur | Innehåll |
| --- | --- |
| `Map` | Bredd, höjd, terräng per ruta, stig per ruta, ägare per ruta, vad som står på rutan |
| `Building` | Typ, position, ägare, byggfas, arbetare, inlager, utlager, recept som pågår, prioritet |
| `Person` | Yrke, position, mål, väg, humör, vad hen bär, nuvarande uppgift, utrustning |
| `Delivery` | Vara, från, till, bärare, status. Skapas av leveranssystemet |
| `Group` | Lista av soldater, formation, riktning, ledare, order, låst i strid eller inte |
| `Command` | Spelare, tick, typ, parametrar. Serialiseras till några byte. Lagras i repris |
| `Player` | Läger, människa eller dator, varuspärrar, statistik |

**Sökvägar.** A\* på rutnätet med kostnad per terräng och stig. Vägar cachas per par av byggnader och ogiltigförklaras när något byggs. Personer som möts på en stig löser det lokalt: den som kom sist väntar ett tick, sedan försöker den gå runt. Det är KaM:s lösning och den ger propparna.

**Leveranser.** En lista av begäran (byggnad, vara, prioritet) och en lista av erbjudanden (byggnad, vara). Var 5:e tick paras de ihop: för varje begäran i prioritetsordning, hitta närmaste erbjudande (avstånd längs stigar, uppskattat) och närmaste lediga bärare. Det är O(n²) men n är hundratals, inte miljoner, och det körs två gånger per sekund.

**Storlek.** Kärnan blir uppskattningsvis 8 000–12 000 rader C#. KaM Remake är betydligt större, men har femton år av funktioner.

## Datadriven design

Allt som står i tabellerna i fliken Produktionskedjor ligger i JSON-filer, och koden läser dem vid start. En ny vara eller en ändrad cykeltid är en textrad, inte en kodrad. Det är också så Claude Code arbetar bäst: en tydlig datamodell och generell kod, i stället för en klass per byggnad.

En byggnad i `buildings.json` ser ut ungefär så här:

```json
{
  "id": "koket",
  "name": "Köket",
  "size": [2, 2],
  "faction": "both",
  "worker": "kokerska",
  "cost": { "brador": 6, "sten": 3 },
  "recipes": [
    { "in": { "mjol": 1, "agg": 1, "mjolk": 1, "ved": 1 }, "out": { "pannkakor": 2 }, "ticks": 300 },
    { "in": { "agg": 2, "vatten": 1, "ved": 1 }, "out": { "hardkokta_agg": 10 }, "ticks": 300 }
  ],
  "sleeps": 1
}
```

Samma princip för enheter (utrustning, värden, förmågor som `skrams`, `trampar_gardsgard`), för mat (humör och bonus), för datorspelarens byggordningar och för kampanjens uppdrag (karta, mål, berättartext, vilka byggnader som är upplåsta).

Filerna valideras vid start mot ett schema, och ett test kontrollerar att varje vara som används någonstans också produceras någonstans, och att ingen kedja är cirkulär utan källa. Det fångar den sortens fel som den andra AI:n gjorde med kaffet.

## Multiplayer: låstegsmodellen

Låsteg (lockstep) betyder att alla spelare kör samma simulering och bara skickar sina kommandon till varandra. Det är så KaM, KaM Remake, Age of Empires och Starcraft fungerar. Det kräver determinism, som kärnan garanterar, och det ger nästan ingen nätverkstrafik.

**Så går en omgång:**

1. Tiden delas i *steg* om 2 tick (200 ms).
2. Ett kommando spelaren ger i steg N schemaläggs för steg N+2. Det ger 400 ms för kommandot att nå alla, vilket räcker för hembredband i Sverige. Spelaren märker det inte, eftersom spelet är långsamt.
3. Varje spelare skickar sina kommandon för steg N+2 till alla andra, även när listan är tom.
4. När alla spelares kommandon för steg N finns körs steget. Saknas någon väntar alla, och vyn visar "Väntar på Kalle".
5. Var 10:e tick skickar alla sin kontrollsumma. Skiljer de sig har spelet gått isär: matchen pausar, alla sparar sin reprisfil, och spelet skriver vilket tick det hände. Det är allt man behöver för att hitta felet i efterhand.

**Topologi.** En spelare är värd. De andra ansluter till värden, som vidarebefordrar allas kommandon. Värden kör samma simulering som alla andra och har ingen auktoritet över spelet, bara över anslutningarna. Fyra spelare ger alltså tre anslutningar.

**Anslutning utan Steam.** Värden öppnar en port (standard 7958) i sin router, eller så använder alla ett virtuellt nätverk (ZeroTier eller Tailscale, båda gratis för en handfull personer) och ansluter till värdens adress där. Lobbyn är en enkel skärm: värdens adress, spelarnamn, val av läger, val av karta, en startknapp.

**Vad som händer när någon tappar kontakten.** Spelet pausar i 30 sekunder och väntar. Kommer spelaren tillbaka skickar värden de steg som saknas, och eftersom allt är kommandon kan spelaren räkna ikapp på några sekunder. Kommer spelaren inte tillbaka tas gården över av datorspelaren.

**Trafik.** Ett kommando är 8–16 byte. En spelare ger kanske 30 kommandon i minuten. Hela matchen är några hundra kilobyte, vilket också är reprisfilens storlek.

## Grafikpipeline

All grafik börjar som SVG-filer som jag skriver, renderas till PNG av ett skript och läses in av Godot. Ingen bild ritas för hand i ett ritprogram, så allt går att ändra och rendera om på en minut.

1. **Stilguide i kod.** `palette.json` med spelets färger, och ett litet SVG-bibliotek med de knep som ger bilderbokskänslan: skeva kurvor (varje rak linje får små slumpmässiga avvikelser), konturer med varierande tjocklek, akvarellfyllning (flera halvgenomskinliga lager med mjuka kanter) och en papperstextur.
2. **Byggnader** ritas i 128 px per ruta och renderas i 1× och 2×. Varje byggnad har tre byggfaser och en arbetsanimation som är 2–4 bildrutor (rök ur skorstenen, kvarnvingar, en hammare).
3. **Personer och djur** ritas i delar: huvud, kropp, två armar, två ben, föremål. Delarna sätts ihop i Godot med `Skeleton2D` och animeras med `AnimationPlayer`: gå, bära, arbeta, slå, sitta sur, gå hem. Ett yrke är en hatt och ett föremål ovanpå samma kropp. Fyra riktningar ritas, spegling ger sex.
4. **Mark** är 10 terrängtyper i 4 varianter var, plus att Godots TileMap ritar övergångarna. Småsaker strös ut av kärnan (deterministiskt, så alla ser samma burk på samma ställe).
5. **Pappret** är en fullskärmsshader: en papperstextur i multiply-läge och ett svagt pigmentbrus. Det är 30 rader shaderkod och gör mer för stilen än något annat.
6. **Animation i 12 bilder per sekund.** Godot kör 60, men animationerna stegas i tolftedelar så att rörelsen ser handgjord ut. Bärarnas positioner interpoleras mellan simuleringens 10 tick per sekund.

**Ordning.** Fas 0 ritar ett hönshus, en pyssling och äng för att pröva stilen. Fas 2 använder platshållare (färgade lådor med namn) för allt annat. Riktig grafik fylls på byggnad för byggnad under fas 3–5, de som syns mest först.

## Faserna

Sex faser, var och en med en kontrollfråga som måste besvaras ja innan nästa börjar. Uppgifterna är skrivna så att var och en är en arbetssession med Claude Code.

### Fas 0: Stilprov och skelett

- [ ] Skapa repot med strukturen ovan, `CLAUDE.md`, och GitHub Actions som kör testerna
- [ ] `sim` som tomt bibliotek med `GameState`, `Tick()`, `Hash()`, fast punkt och slumpgenerator
- [ ] Determinismtest: kör 10 000 tick med slumpade kommandon två gånger, kontrollsumman ska vara lika
- [ ] Beroendetest: `sim` får inte referera Godot, inga flyttal i `sim/`
- [ ] Stilguide: `palette.json`, SVG-biblioteket för skeva linjer och akvarell, `render.py`
- [ ] Stilprov: ett hönshus, en pyssling med ägg, en bit äng, pappersshadern, visat i Godot

**Kontrollfråga:** ser det ut som en bilderbok?

### Fas 1: Ekonomin utan grafik

- [ ] Karta med terräng, ägare och stigar. A\* med stigkostnad
- [ ] Byggnader från `buildings.json`: placering, byggande, in- och utlager, recept
- [ ] Personer: bärare, hantlangare, arbetare. Leveranssystemet med begäran och erbjudande
- [ ] Mat och humör, kafferepet, bygdegården, lanthandeln
- [ ] `sim.cli`: kör en match från en byggordning och skriver ut produktion per minut
- [ ] De åtta första byggnaderna i data: stuga, bod, skogshuggare, sågbod, vedbod, åker, kvarn, bagarstuga

**Kontrollfråga:** kan en gård i `sim.cli` producera knäckebröd i jämn takt i 30 minuter utan att bärarna fastnar?

### Fas 2: Spelbar gård

- [ ] Godot-projektet: kamera, TileMap från kartan, platshållargrafik för allt
- [ ] Vyn: ritar byggnader och personer från `GameState`, interpolerat
- [ ] Gränssnittet: bygga, lägga stigar, välja byggnad, se lager, utbilda i bygdegården
- [ ] Alla 45 byggnader och 54 varor i data, hela matkedjan spelbar
- [ ] Sixten i hörnet med varningar
- [ ] Spara och ladda genom reprisfilen

**Kontrollfråga:** är det roligt att bygga gården i 30 minuter utan strid?

### Fas 3: Strid

- [ ] Logen, rekryter, utrustning från `units.json`
- [ ] Grupper, formationer, order, sammanstötning, skräms, avståndsvapen
- [ ] Gärdsgård, staket, vedtraven, hundkojan, råttfällor, belägring, att ta byggnader
- [ ] Kafferasten och kaffedrängen
- [ ] Segervillkor och slutscenen
- [ ] Datorspelaren: byggordning plus de tre reflexerna
- [ ] `sim.cli` kör dator mot dator tusen gånger och skriver ut vinstandel per läger

**Kontrollfråga:** går det att vinna mot datorn på normal, och förlora på svår?

### Fas 4: Multiplayer

- [ ] Låstegsloopen i `game/Net`: steg, fördröjning, väntan på kommandon
- [ ] ENet: värd och klienter, lobby, val av läger och karta
- [ ] Kontrollsummor var 10:e tick, pausa vid avvikelse, spara reprisfiler
- [ ] Återanslutning och övertagande av datorspelaren
- [ ] Verktyg som läser två reprisfiler och hittar första avvikande tick
- [ ] Testkväll med brorsan över ZeroTier

**Kontrollfråga:** håller en match på en timme mellan Windows och Mac utan avvikelse?

### Fas 5: Innehåll och finish

- [ ] Riktig grafik för alla byggnader, personer, djur, varor och gränssnitt
- [ ] Kampanjens tio uppdrag med kartor, mål, berättartexter och kartbordet
- [ ] 4–6 skirmishkartor
- [ ] Ljud och musik
- [ ] Bättre datorspelare: formationer, reservtrupp, svårighetsgrader
- [ ] Exportera till Windows och Mac, skriv en sida med installations- och anslutningsinstruktioner

**Kontrollfråga:** vill kompisarna spela igen?

## Tester

Testerna är det som gör att projektet går att driva med Claude Code utan att tappa greppet. De körs vid varje push.

| Test | Vad det kontrollerar | När |
| --- | --- | --- |
| Determinism | Två körningar av samma kommandon ger samma kontrollsumma, på Windows och Linux | Fas 0 och framåt, varje push |
| Beroenden | `sim` refererar inte Godot, inga flyttal, ingen `System.Random`, ingen `DateTime` | Fas 0 |
| Datavalidering | Alla JSON-filer följer schemat, varje vara har en källa, inga cirkulära kedjor | Fas 1 |
| Enhetstester per modul | Sökvägar, leveranser, recept, humör, strid, var för sig | Löpande |
| Ekonomitest | En gård från en byggordning producerar inom förväntat intervall per minut och fastnar inte | Fas 1 |
| Balanstest | Tusen matcher dator mot dator: vinstandel per läger 40–60 procent, matchlängd 45–75 minuter | Fas 3 |
| Reprisregression | Sparade reprisfiler från tidigare versioner ger fortfarande samma kontrollsumma, eller så har versionsnumret höjts med flit | Fas 2 |
| Avvikelsesökning | Verktyget som jämför två reprisfiler hittar ett planterat fel | Fas 4 |

Speltest med människor är en egen sak och sker vid varje kontrollfråga. Reprisfiler från speltesten sparas, eftersom de är det bästa underlaget för balansering.

## Att arbeta med Claude Code på det här projektet

Det som avgör om projektet går i mål är inte hur bra koden blir per session, utan att varje session börjar med rätt kontext och slutar med gröna tester. Därför:

- **`CLAUDE.md` är projektets grundlag.** Den innehåller determinismreglerna, mappstrukturen, kommandot för att köra tester, och regeln att speldata ligger i JSON. Den uppdateras när ett beslut tas.
- **En modul per session.** "Bygg leveranssystemet i `sim/Economy` enligt designdokumentet, med tester" är en bra session. "Bygg ekonomin" är det inte.
- **Designdokumentet ligger i repot** under `docs/`, exporterat som markdown, så att Claude Code kan läsa det avsnitt som gäller.
- **Tester före funktion.** Varje session börjar med att skriva testet för det som ska byggas. Det gör att sessionen vet när den är klar.
- **Determinismtestet körs alltid.** Det är den regel som är lättast att bryta av misstag och dyrast att laga sent.
- **Små commits med tydliga meddelanden**, en per avslutad uppgift i faslistan.
- **När något känns fel i spelet, skriv en reprisfil** och låt nästa session börja från den. Det är bättre än en beskrivning.

Jag kan själv driva sessionerna härifrån mot ett GitHub-repo, när du skapat det och kopplat det till projektet. Du behöver då bara spela, tycka och svara på kontrollfrågorna.

## Risker och motåtgärder

| Risk | Vad som händer | Motåtgärd |
| --- | --- | --- |
| Spelet går isär i multiplayer | Två spelare ser olika saker, matchen är förstörd | Determinism från fas 0, test på två operativsystem, kontrollsummor var 10:e tick, verktyg som hittar första avvikande tick |
| Grafiken tar för lång tid | Spelet är färdigt men fult i ett år | Platshållare från fas 2, riktig grafik i ordning efter synlighet, klippdockor i stället för bildrutor |
| Stilen håller inte | Vektorgrafik ser ut som vektorgrafik | Fas 0 är ett stilprov med en kontrollfråga. Faller det kan stilen bytas innan något ritats |
| Ekonomin är för svår | Ingen kommer till strid | Kampanjens fyra första uppdrag är utan strid, Sixtens varningar, balanstest i `sim.cli` |
| Bärarna fastnar | Gården stannar utan att spelaren förstår varför | Ekonomitestet i fas 1, synlig markering av proppar i vyn, Sixten säger till |
| Projektet tappar fart | Hobbyprojekt dör i tystnad | Små uppgifter, synliga framsteg, en spelbar version efter varje fas, testkvällar som mål |
| Prestanda | 400 gubbar per spelare går långsamt | Heltal, cachade sökvägar, leveranser var 5:e tick. KaM gjorde det 1998 på en Pentium |
| Godot-versionen ändras | API-brott mitt i projektet | Lås versionen i repot. Kärnan är oberoende av Godot och påverkas inte |

## Första veckan

Det här är vad som behöver hända för att fas 0 ska kunna börja, och vem som gör det.

- [ ] Aron: skapa ett privat repo `hallonkriget` på GitHub och koppla det till det här projektet
- [ ] Aron: installera Godot 4.4 (.NET-versionen) och .NET 8 SDK på din dator, så att du kan öppna och köra det jag bygger
- [ ] Claude: repots struktur, `CLAUDE.md`, `sim` med `GameState`, determinismtestet, GitHub Actions
- [ ] Claude: stilguiden och stilprovet (hönshus, pyssling, äng, pappersshader) som en körbar Godot-scen
- [ ] Aron: öppna scenen, titta, och svara på fas 0:s kontrollfråga

När kontrollfrågan är besvarad börjar fas 1, en modul i taget, i den ordning fasens lista anger.
