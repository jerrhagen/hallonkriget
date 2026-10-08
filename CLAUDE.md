# CLAUDE.md – Hallonkriget

Ett RTS i Knights and Merchants anda, på en liten småländsk bondgård 1958. Torpet (Algot och katten Sixten) mot Storgården (Holger och hunden Major). Hobbyprojekt, gratis, för Aron och några kompisar.

Den här filen är projektets grundlag. Uppdatera den när ett beslut tas.

## Var saker står

- `docs/designdokument.md`, `docs/produktionskedjor.md`, `docs/implementationsplan.md`: hela specen, exporterad från designdokumentet. Läs det avsnitt som gäller innan du bygger något. Ändra inte det specen fastslår utan att fråga Aron.
- `docs/beslut.md`: beslut som tagits efter designdokumentet.

## Mappar

```
data/        speldata i JSON (varor, byggnader, enheter, mat, kartor, kampanj)
sim/         simuleringskärnan, rent C#, inga Godot-beroenden
sim.tests/   xUnit: determinism, beroenden, enhetstester per modul
sim.cli/     kör matcher utan grafik
game/        Godot 4.4-projektet (C#), läser GameState och ritar
art/         SVG-källor, palette.json, svg-biblioteket och render.py
docs/        designdokumentet och beslut
```

Varje mapp under `sim/` är en modul med ett tydligt gränssnitt och egna tester. En arbetssession tar en modul i taget.

## Kommandon

```
dotnet test                         # alla tester, ska alltid vara gröna
dotnet run --project sim.cli        # kör simuleringen utan grafik
python3 art/render.py               # SVG -> PNG (kräver: pip install resvg-py)
```

Godot-projektet öppnas från `game/project.godot` med Godot 4.4 .NET.

## Determinismregler för sim/

Kärnan är `GameState.Tick(kommandon)`. Samma tillstånd och samma kommandon ger exakt samma nya tillstånd, på alla datorer. Reglerna kontrolleras av `sim.tests`:

1. Inga flyttal (`float`, `double`, `decimal`) i `sim/`. Positioner är heltal i 1/256 ruta (`Fixed`), hastigheter och procent är heltal.
2. En enda slumpgenerator, `Rng` (xorshift64), som ingår i tillståndet och seedas vid matchstart. `System.Random` är förbjuden.
3. Ingen iteration över `Dictionary` eller `HashSet` där ordningen påverkar resultatet. Använd listor och arrayer med fasta id-nummer.
4. Ingen klocka, ingen `DateTime`, `Stopwatch`, `Environment.TickCount`, inga trådar eller `Task`. Tiden är tickräknaren.
5. Ingen referens till Godot.
6. `Hash()` täcker hela tillståndet. Lägger du till ett fält i tillståndet ska det in i `Hash()`.

Tick: 10 per sekund. Ordning inom ett tick: tillämpa kommandon, flytta personer, produktion, leveranser, humör, strid, datorspelare, öka tickräknaren.

## Konventioner

- Namn i koden på engelska (`GameState`, `Building`). Kommentarer, docs och commit-meddelanden på svenska.
- Speldata-id är svenska utan å, ä, ö: `mjol`, `agg`, `hardkokta_agg`, `koket`.
- Allt som står i tabellerna i Produktionskedjor ligger i JSON under `data/`, inte i kod. Generell kod, inte en klass per byggnad.
- Tester före funktion. En session börjar med testet för det som ska byggas.
- Små commits, en per avslutad uppgift i faslistan.
- Grafik: SVG som skrivs (eller genereras) av kod under `art/`, renderas till PNG med `art/render.py`. Ingen handritad grafik i binärformat utan SVG-källa.
