# Beslut

Beslut som tagits efter designdokumentet. Nyast överst.

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
