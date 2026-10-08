# People

Personerna: bärare, hantlangare och arbetare, och hur de går. Se docs/designdokument.md (Bärare och logistik).

- `Person`: roll, ruta, steg på väg, jobb och vad den bär. Går längs en väg från `Pathfinder` med 1,2 rutor per sekund på upptrampad stig, hälften så fort på gräs. Det som blir över av ett steg följer med till nästa, så att farten stämmer.
- Två personer får stå på samma stigruta, den tredje väntar. Efter en sekunds väntan tränger den sig förbi, så att ingen fastnar för alltid.
- Hantlangare (`GameState.People.cs`): tar närmaste bygge som kan byggas vidare på (högst två per bygge), annars närmaste planerade stigruta, som är upptrampad efter fyra sekunder.
- Arbetare: går till närmaste byggnad med deras yrke som saknar arbetare och stannar inne.
- Bärarnas jobb sköts av leveranssystemet i sim/Economy.

Inte här än: humör och mat, bygdegården, att bärare som möts går runt varandra, drängarnas fart och två varor i taget, att skogshuggaren går ut och fäller träd.
