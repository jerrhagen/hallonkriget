# People

Personerna: bärare, hantlangare och arbetare, och hur de går. Se docs/designdokument.md (Bärare och logistik).

- `Person`: roll, ruta, steg på väg, jobb och vad den bär. Går längs en väg från `Pathfinder` med 1,2 rutor per sekund på upptrampad stig, hälften så fort på gräs. Det som blir över av ett steg följer med till nästa, så att farten stämmer.
- Två personer får stå på samma stigruta, den tredje väntar. Efter en sekunds väntan tränger den sig förbi, så att ingen fastnar för alltid.
- Hantlangare (`GameState.People.cs`): tar närmaste bygge som kan byggas vidare på (högst två per bygge), annars närmaste planerade stigruta, som är upptrampad efter fyra sekunder.
- Arbetare: går till närmaste byggnad med deras yrke som saknar arbetare och stannar inne.
- Bärarnas jobb sköts av leveranssystemet i sim/Economy.

- Humör och mat (`GameState.Mood.cs`): humöret sjunker en enhet var 6:e sekund. Vid 30 går personen till närmaste kafferep med mat när hen gjort klart det hen håller på med, och äter det bästa först, en portion av varje sort. Vid 0 ger hen upp, tar med sig det hen bär hem till stugan och vilar där i tre minuter.
- Bygdegården: spelaren ställer yrken i kön (`Train`). Första i kön börjar när kaffet (eller två surrogat) och verktyget finns och det finns en sovplats ledig, och efter 30 sekunder kommer en ny person ut genom dörren.

Inte här än: matens bonusar, kafferasten, kyla, att bärare som möts går runt varandra, drängarnas fart och två varor i taget, att skogshuggaren går ut och fäller träd.
