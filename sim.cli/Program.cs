using System.Diagnostics;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Commands;

// Kör simuleringen utan grafik. I fas 0: skapar några vandrare och skriver kontrollsumman
// var 1000:e tick. I fas 1 läser den en byggordning och skriver produktion per minut.
//
//   dotnet run --project sim.cli -- [tick] [frö]

int ticks = args.Length > 0 ? int.Parse(args[0]) : 36_000;
ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 1958_07_14UL;

var state = GameState.NewMatch(new MatchSetup(seed, 96, 96, new[]
{
    new PlayerSetup(Faction.Torpet, IsComputer: false),
    new PlayerSetup(Faction.Storgarden, IsComputer: true),
}));

var sw = Stopwatch.StartNew();
var none = Array.Empty<Command>();
for (int t = 0; t < ticks; t++)
{
    if (t < 40)
    {
        var spawn = new[] { new Command(t, (byte)(t % 2), CommandType.SpawnWalker, 10 + t, 10 + t) };
        state.Tick(spawn);
    }
    else
    {
        state.Tick(none);
    }

    if (state.TickCount % 1000 == 0)
        Console.WriteLine($"tick {state.TickCount,7}  ({state.TickCount / GameState.TicksPerSecond / 60,3} min)  hash {state.Hash():x16}");
}
sw.Stop();

Console.WriteLine($"{ticks} tick på {sw.ElapsedMilliseconds} ms, {state.Walkers.Count} vandrare, slutlig hash {state.Hash():x16}");
