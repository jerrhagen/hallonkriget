# Map

Rutnät, terräng, ägare per ruta, stigar och sökvägar. Se docs/designdokument.md (Kartan, Stigar) och docs/implementationsplan.md (Sökvägar).

- `GameMap`: ett lager per egenskap (terräng, stig, ägare, vad som står där), en plats per ruta i radordning. `Version` ökar när något ändras som påverkar gåendet, så att sparade vägar vet när de är gamla. Ingår i `GameState.Hash()`.
- `TerrainRules`: vad som går att bygga på, var man kan gå och hur mycket ett steg kostar. Stig 4, landsväg 3, gräs 8, stenig mark 12, skog och myr 16, vatten stängt. Myr är stängd för fordon (`MoveClass.Vehicle`).
- `PathState`: en stig planeras av spelaren och trampas upp av en hantlangare. Bara upptrampad stig är snabb.
- `Pathfinder`: A* i åtta riktningar, inga diagonaler över stängda hörn. Byggnader stänger sin ruta men kan vara mål. Samma fråga ger alltid samma väg. Inte en del av tillståndet.

Inte här än: att två bärare får mötas på en stigruta men inte tre (People), cachning av vägar mellan byggnader (Economy), kartor från `data/maps/`.

`MapDef` läser en karta från data/maps: storlek, startplatser och terräng som rektanglar. `MatchSetup.OnMap` startar en match på den.
