# Replay

Reprisfiler, som också är sparfiler. En `Replay` har matchens uppsättning (frö, kartan, spelarna), alla kommandon och en kontrollsumma var 100:e tick. Att ladda ett sparat spel är att spela upp kommandona till sista ticket med `Play`, som kastar `ReplayMismatchException` med första avvikande tick om något inte stämmer.

Spela in: `Record(tick, kommandon)` före `GameState.Tick`, `AfterTick(state)` efter. Skriv med `Write`/`ToBytes`, läs med `Read`/`FromBytes`.

`RulesVersion` höjs med flit när simuleringen ändras så att gamla repriser spelas annorlunda. Reprisregressionen i `sim.tests` kräver att sparade repriser i `sim.tests/Replays/` med samma version och samma speldata stämmer. Spela in en ny med `dotnet run --project sim.cli -- brodgarden 10 --spela-in=sim.tests/Replays/brodgarden.hkr`.
