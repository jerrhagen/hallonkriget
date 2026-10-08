# Beslut

Beslut som tagits efter designdokumentet. Nyast överst.

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
