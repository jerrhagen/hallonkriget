# Military

Logen, rekryterna, grupperna och striden. Designdokumentet: Enheter och Strid. Enheterna och stridens regler står i `data/units.json`.

- `GameState.Military.cs`: logen (`Equip`), rekryter som väntar i logen, grupper och order (`MoveGroup`, `AttackGroup`, `AttackBuilding`, `SetFormation`, `TurnGroup`, `SplitGroup`, `MergeGroups`, `HaltGroup`), och hur en soldat rör sig: till sin plats i formationen, mot fiender inom synhåll, hem efter skott eller till kafferepet när hen är hungrig.
- `GameState.Combat.cs`: slag var 2:a sekund, skada = anfall − försvar (minst 1, dubbelt i flank och rygg), avståndsvapen som träffar slumpvis inom en ruta, salthagelbössan, skräms, medaljer, och vad som händer när en soldat ger upp.
- `Group.cs`: en grupp med formation (kolumner), riktning (0–7) och främsta ruta.

Soldaterna är personer (`Person.Role == Soldier`) med en enhet. Djuren (höns, gäss) räknas inte i befolkningen, och inte gubben heller.
