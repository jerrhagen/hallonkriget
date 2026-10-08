# Beslut

Beslut som tagits efter designdokumentet. Nyast överst.

## 2026-10-08: Alla byggnader och hela matkedjan (fas 2)

- Alla 45 byggnader står i `buildings.json` och alla yrken i `professions.json`. Logen, vedtraven, hundkojan och hundgården, uppfinnarverkstans katapult och maskinhallens traktor gör inget förrän striden kommer i fas 3; rekryten och kaffedrängen kommer då också.
- Åkerns sex odlingsrutor ligger 3×2 till höger om huset. Man kan gå över dem men inte bygga eller lägga stig där. Brödgårdens byggordning flyttades så att åkrarna får plats; kontrollfrågan för fas 1 håller fortfarande.
- Det som tar slut, eftersom designdokumentet inte säger hur mycket: en ruta stenig mark ger 6 sten, en ruta skog 4 träd, en skrothög 12 skrot. Sedan blir rutan glänta. Skogshuggaren planterar en gran per två fällda, först i glesnad skog, annars på en glänta intill skogen.
- Byggnader med recept man sällan vill turas om mellan börjar med ett valt: hönshuset ägg, grisstian grisar, köket pannkakor, smedjan järn, snickarboden verktyg. Spelaren kan byta.
- Mjölkpallen byter 2 mjölk mot 1 kaffe en gång per speldag, när dagen börjar, inte på söndagar.
- Matens bonusar verkar nu: pannkakor ger 10 procent fart och svagdricka 10 procent arbetstakt i tre minuter. Syltens anfallsbonus räknas men används först i fas 3.
- Ny karta `hemmanet` (64×48) för att bygga en hel gård: skog, äng, hallonsnår, plommonträd, sjön, stenig mark, skrothögar, myr och landsväg. Ett test visar att all mat kommer fram till kafferepet där.
- En gård med 55 personer, en kvarn och fem åkrar föder inte sig själv: kvarnen och åkrarna är flaskhalsen. Det är balans, och prövas i speltestet.

## 2026-10-08: sim.cli och kontrollfrågan för fas 1

- `dotnet run --project sim.cli` spelar upp en byggordning från `data/ai/` på dess karta i `data/maps/` och skriver produktion per minut. Kontrollfrågan räknas på de sista 30 minuterna: knäckebröd i sex fönster om 5 minuter, inget fönster under hälften av det bästa, och ingen som ger upp av hunger.
- Byggordningen `brodgarden` utbildar sitt folk i bygdegården och köper verktyg och kaffe för ved i lanthandeln, omväxlande. Efter en timme bakar gården 27–39 knäckebröd per 5 minuter, och ingen leverans tar mer än en minut.
- Den som ger upp tar varan med sig hem och lägger den i stugan, i stället för att den försvinner. Varor som ligger kvar på marken kommer med pysslingarna som tappar dem.
- En byggnad som ingen arbetare har tagit begär inga varor. Annars samlades veden i bagarstugorna innan det fanns en bagare, och lanthandeln fick ingen ved att köpa verktyg för.
- Ett steg i en byggordning väntar på sin minut, på att en byggnad är färdig eller på att något är tillverkat, och håller kvar stegen efter sig. Det är så datorspelarens byggordningar kommer att fungera i fas 3.
- Matchens början är hård: ingen mat kommer på bordet förrän kafferepet står, och det kan inte byggas före sågboden med startförrådets brädor. Alla ger upp en gång runt minut 10. Det följer av designdokumentets siffror och får prövas när spelet går att spela.

## 2026-10-08: Mat, humör, bygdegården och lanthandeln

- **Starten (Aron valde den 8 oktober).** Designdokumentets start går inte ihop: varje arbetare kostar en kopp kaffe och ett verktyg i bygdegården, startförrådet har 2 kaffe och inga verktyg, och verktyg kommer bara från snickarboden eller lanthandeln, som båda behöver en utbildad arbetare. Aron valde att matchen börjar med sex arbetare utöver bärarna och hantlangarna (skogshuggare, sågare, vedhuggare, stenröjare, bonde, vattenbärare) och 2 verktyg i startförrådet. Bygdegården byggs vid minut 5–12 som i tidslinjen. Allt står i `start_people` och `start_stock` i `buildings.json`.
- På kafferepet äter man det bästa som finns först, en portion av varje sort, tills humöret är fullt (som värdshuset i KaM). Sylt räknas bara ihop med pannkakor. Med bara knäckebröd går en person från 30 till 60 och äter igen efter tre minuter, så en person äter ett knäckebröd var tredje minut.
- Kaffet är spärrat på kafferepet från början, så att det inte äts upp innan bygdegården fått sitt. Spelaren kan häva spärren (`BlockGood`), som kommer att finnas för alla varor.
- Arbetare äter mellan två omgångar, aldrig mitt i en, och går tillbaka till samma arbetsplats. Bärare äter när de lämnat det de bär. Finns ingen mat arbetar man vidare tills humöret tar slut.
- Den som ger upp vilar tre minuter i stugan och kommer tillbaka med fullt humör och samma yrke.
- Bonusarna (pannkakor, sylt, svagdricka) står i `food.json` men verkar inte än. De kommer när köket och bryggstugan finns.
- Bygdegården begär bara det kön behöver: kaffe eller surrogat för dem som väntar och ett verktyg i taget. Kön rymmer sex. Ingen utbildning börjar om alla sovplatser är tagna; en byggnad ger en sovplats om inget annat står (stugan 4, mangårdsbyggnaden 6, boden 10).
- Lanthandeln gör ingenting förrän spelaren valt vad som ska köpas och med vad. Varje byte tar 20 sekunder. Den ska ligga intill landsvägen (diagonalt räknas).
- Spelets klocka: en vecka per timme, matchen börjar en måndag. Söndagen är sista sjundedelen av varje timme.
- Rosteriet är med i datan, eftersom bygdegården tar surrogat. Mjölkpallen kommer med korna.
- Brödgården i testet behöver tre åkrar och två bagarstugor för att föda sina 19 personer. Byggordningen spelar roll: startförrådets sten räcker till fyra hus, och stenröjarboden måste vara ett av dem.

## 2026-10-08: Personer och leveranser i fas 1

- 1,2 rutor per sekund är farten på upptrampad stig. På gräs går det hälften så fort, i skog en fjärdedel. Då hinner en bärare ungefär 3 leveranser per minut på en tät gård, som designdokumentet räknar med.
- Alla går lika fort tills vidare. Drängarnas 0,8 rutor per sekund och två varor i taget kommer med humöret och lägren.
- Två på samma stigruta, den tredje väntar. Efter en sekund tränger den sig förbi, så att möten ger köer men aldrig låser gården.
- Byggplatser får material i den ordning de placerades. Utan det sprids brädorna ut på alla byggen och inget blir klart.
- Färdiga byggnader delar på knappa varor: begäran tas i omgångar. Annars tar sågboden allt timmer och vedboden får inget.
- Ett förråd tar emot överskott av en vara bara upp till en fjärdedel av sin plats (boden 50, stugan 7). Annars fyller vatten och sten upp boden så att inget annat får plats. Det som en byggnad begär går alltid fram.
- Tester får skapa arbetare direkt tills bygdegården finns.

## 2026-10-08: Byggnaderna i fas 1

- Byggtid när materialet finns: 60, 90 och 120 sekunder för 1×1, 2×2 och 3×3 ("en liten byggnad tar ungefär en minut"). Kan sättas per byggnad med `build_seconds`.
- Hantlangarna bygger medan materialet kommer: halva materialet räcker till halva bygget, som i KaM.
- Dörren är rutan mitt under byggnaden. Ingen byggnad får stå på en annans dörr.
- Utlagret rymmer 5 av varje vara, men ett recept som ger fler (knäckebröd ×3, hårdkokta ägg ×10) får starta när utlagret är tomt nog för en omgång.
- Data för fas 1: alla 54 varor, och de byggnader som behövs för knäckebröd (stugan, mangårdsbyggnaden, boden, skogshuggarkojan, sågboden, vedboden, stenröjarboden, brunnen, åkern, kvarnen, bagarstugan). Stenröjarboden och brunnen är med fast planen bara räknar upp åtta, eftersom knäckebröd behöver vatten och byggena behöver mer sten än startförrådet har.
- Speldatan har en kontrollsumma som ingår i `Hash()`.

## 2026-10-08: Publikt repo och CI

- Repot är publikt sedan den 8 oktober, så Actions-minuterna kostar inget. Varje pull request kör Linux (tester och spelbygge) och Windows (determinismen jämförs mellan plattformarna). En ny push avbryter en körning som inte är klar, och ändringar som bara rör grafik eller dokument kör inga tester.

## 2026-10-08: Stilprovet

- Kontrollfrågan för fas 0 är besvarad ja: Aron tycker att stilprovet "ser fint ut". Stilen gäller, fas 1 börjar.

- Renderaren är `gl_compatibility`, så att spelet går på äldre datorer och utan grafikkort i CI.
- Grafiken renderas i 1× (128 px per ruta) och 2×. Spelet använder 2× nerskalad till hälften, med mipmaps, så att den är skarp både inzoomad och utzoomad.
- Klippdockor byggs som platta `Node2D`-delar med pivotpunkter från `*_rig.json`, inte med `Skeleton2D`. Animationerna nycklas i kod i 12 bilder per sekund.
- Pysslingarna i stilprovet går bara i vyn, inte genom simuleringen. Kopplingen sim–vy kommer i fas 1.
- Stigen i stilprovet är en enda dekal. De riktiga stigarna (16 rutvarianter) kommer i fas 2.

## 2026-10-08: Fas 0 startar

- Repot är `jerrhagen/hallonkriget`, privat. Grundstrukturen läggs direkt på `main`, resten kommer som pull requests.
- Godot-versionen låses till 4.4.1 .NET. Simuleringskärnan är .NET 8.
- Dokumenten i `docs/` är exporterade från designdokumentet den 8 oktober 2026. Originalet (med diagrammen) är Claude-dokumentet "Hallonkriget – designdokument och implementationsplan".
