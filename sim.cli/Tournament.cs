using System.Diagnostics;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

/// <summary>
/// Datorspelare mot datorspelare på en karta för två, implementationsplanens balanstest:
/// vinstandel per läger (40–60 procent) och matchlängd (45–75 minuter).
///
///   dotnet run --project sim.cli -- dator [matcher] [svårighet] [--karta=grannarna] [--minuter=100] [--visa]
/// </summary>
public static class Tournament
{
    public sealed record Result(int Seed, Faction? Winner, int Minutes, int[] Army, int[] Lost, int[] Buildings, int[] Taken, int[] Equipped, int[] People, int[] Eaten);

    public static void Run(string[] args, string dataDir)
    {
        var positional = args.Where(a => !a.StartsWith("--")).ToArray();
        int matches = positional.Length > 0 ? int.Parse(positional[0]) : 1000;
        var difficulty = positional.Length > 1 ? ParseDifficulty(positional[1]) : Difficulty.Normal;
        string mapId = Option(args, "karta") ?? "grannarna";
        int maxMinutes = int.Parse(Option(args, "minuter") ?? "100");
        bool show = args.Contains("--visa");
        if (Option(args, "dumpa") is { } dump) DumpAt = int.Parse(dump);

        var data = GameData.FromFiles(name => File.ReadAllText(Path.Combine(dataDir, name)));
        var map = MapDef.Parse(File.ReadAllText(Path.Combine(dataDir, "maps", mapId + ".json")));
        Console.WriteLine($"{matches} matcher dator mot dator på {map.Id}, {difficulty}, högst {maxMinutes} minuter");

        var sw = Stopwatch.StartNew();
        var results = new Result[matches];
        int done = 0;
        Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            results[i] = Play(data, map, i, difficulty, maxMinutes, show && matches == 1);
            int n = Interlocked.Increment(ref done);
            if (!show && n % 50 == 0) Console.Error.WriteLine($"  {n} klara");
        });
        sw.Stop();

        int torpet = results.Count(r => r.Winner == Faction.Torpet), storgarden = results.Count(r => r.Winner == Faction.Storgarden);
        int draws = matches - torpet - storgarden;
        var decided = results.Where(r => r.Winner is not null).ToList();
        Console.WriteLine();
        Console.WriteLine($"Torpet vann {torpet} ({Percent(torpet, matches)}), Storgården {storgarden} ({Percent(storgarden, matches)}), oavgjort efter {maxMinutes} minuter {draws}.");
        if (decided.Count > 0)
            Console.WriteLine($"Matchlängd när någon vann: snitt {decided.Average(r => r.Minutes):F0} minuter, " +
                $"kortast {decided.Min(r => r.Minutes)}, längst {decided.Max(r => r.Minutes)}, " +
                $"inom 45–75: {Percent(decided.Count(r => r.Minutes is >= 45 and <= 75), decided.Count)}.");
        Console.WriteLine($"Byggnader i snitt: {results.Average(r => r.Buildings[0]):F0} och {results.Average(r => r.Buildings[1]):F0}. " +
            $"Soldater som gav upp: {results.Average(r => r.Lost[0] + r.Lost[1]):F0} per match.");
        Console.WriteLine($"Folk i slutet: {results.Average(r => r.People[0]):F0} och {results.Average(r => r.People[1]):F0}. " +
            $"Måltider: {results.Average(r => r.Eaten[0]):F0} och {results.Average(r => r.Eaten[1]):F0}. Armé i slutet: {results.Average(r => r.Army[0]):F1} och {results.Average(r => r.Army[1]):F1}.");
        foreach (var u in data.Units.Where(u => results.Any(r => r.Equipped[u.Index] > 0)))
            Console.WriteLine($"  {u.Name}: {results.Average(r => r.Equipped[u.Index]):F1} per match");
        Console.WriteLine($"{matches} matcher på {sw.Elapsed.TotalSeconds:F0} s");
    }

    /// <summary>En match. Lägren byter sida varannan match, så att kartan inte avgör.</summary>
    public static Result Play(GameData data, MapDef map, int index, Difficulty difficulty, int maxMinutes, bool show = false)
    {
        bool swap = index % 2 == 1;
        var west = swap ? Faction.Storgarden : Faction.Torpet;
        var east = swap ? Faction.Torpet : Faction.Storgarden;
        var setup = MatchSetup.OnMap((ulong)(1958_07_14 + index), map, data,
            new PlayerSetup(west, true, null, difficulty), new PlayerSetup(east, true, null, difficulty));
        var state = GameState.NewMatch(setup);
        var none = Array.Empty<Command>();
        int minute = 0;
        while (state.Winner < 0 && minute < maxMinutes)
        {
            for (int t = 0; t < 60 * GameState.TicksPerSecond && state.Winner < 0; t++) state.Tick(none);
            minute++;
            if (show && minute % 5 == 0) Show(state, minute);
            if (show && minute == DumpAt) Dump(state);
        }
        var winner = state.Winner >= 0 ? state.Players[state.Winner].Faction : (Faction?)null;
        if (show)
            foreach (var p in state.Players)
                Console.WriteLine($"  {p.Faction} gjorde " + string.Join(" ", data.Goods.Where(g => p.Produced[g.Index] > 0).Select(g => $"{g.Id}:{p.Produced[g.Index]}")));
        if (show) Console.WriteLine($"Vinnare: {(winner?.ToString() ?? "ingen")} efter {minute} minuter");
        var equipped = new int[data.Units.Count];
        foreach (var p in state.Players)
            for (int u = 0; u < equipped.Length; u++) equipped[u] += p.Equipped[u];
        return new Result(index, winner, minute,
            state.Players.Select(p => state.ArmySize(p.Id)).ToArray(),
            state.Players.Select(p => p.SoldiersLost).ToArray(),
            state.Players.Select(p => state.Buildings.Count(b => b.Owner == p.Id && b.Stage == BuildingStage.Done)).ToArray(),
            state.Players.Select(p => p.BuildingsTaken).ToArray(),
            equipped,
            state.Players.Select(p => state.People.Count(q => q.Owner == p.Id)).ToArray(),
            state.Players.Select(p => p.Eaten.Sum()).ToArray());
    }

    private static void Show(GameState state, int minute)
    {
        foreach (var p in state.Players)
        {
            var people = state.People.Where(q => q.Owner == p.Id).ToList();
            int table = state.Buildings.Where(b => b.Owner == p.Id && b.Def.Table > 0).Sum(b => b.InputTotal);
            var ai = state.Computer(p.Id);
            Console.WriteLine($"{minute,3} {p.Faction,-10} hus {state.Buildings.Count(b => b.Owner == p.Id && b.Stage == BuildingStage.Done),2}/{state.Buildings.Count(b => b.Owner == p.Id),-2}" +
                $" steg {ai?.NextStep,2} folk {people.Count,3} sov {state.Beds(p.Id),3} arb {people.Count(q => q.Role == PersonRole.Worker),2}" +
                $" bär {people.Count(q => q.Role == PersonRole.Carrier),2} rek {people.Count(q => q.Role == PersonRole.Recruit),2}" +
                $" armé {state.ArmySize(p.Id),3} bord {table,3} gav {p.GaveUp,3} förlorade {p.SoldiersLost,3} tog {p.BuildingsTaken,2}" +
                $" åt {string.Join(",", state.Data.Foods.Where(f => p.Eaten[f.Good] > 0).Select(f => $"{state.Data.Goods[f.Good].Id}:{p.Eaten[f.Good]}"))}" +
                $" kaffe {state.Stored(p.Id, state.Data.GoodIndex("kaffe")),2} anfall {(ai?.Attacking == true ? "ja" : "nej")}");
        }
    }

    public static int DumpAt = -1;

    private static void Dump(GameState state)
    {
        foreach (var b in state.Buildings)
        {
            var inputs = string.Join(" ", state.Data.Goods.Where(g => b.InputCount(g.Index) > 0).Select(g => $"{g.Id}:{b.InputCount(g.Index)}"));
            var outputs = string.Join(" ", state.Data.Goods.Where(g => b.OutputCount(g.Index) > 0).Select(g => $"{g.Id}:{b.OutputCount(g.Index)}"));
            Console.WriteLine($"  p{b.Owner} {b.Def.Id,-18} {b.Stage,-12} ({b.Origin.X},{b.Origin.Y}) arb {b.WorkerId,4} kö {string.Join(",", b.TrainingQueue.Select(q => q.Id))} in[{inputs}] ut[{outputs}]");
        }
        foreach (var p in state.People.Where(q => q.Owner == 0))
            Console.WriteLine($"  person {p.Id} {p.Role} {p.Profession} {p.Job} ({p.Tile.X},{p.Tile.Y}) humör {p.Mood}");
    }

    private static Difficulty ParseDifficulty(string s) => s switch
    {
        "latt" or "easy" => Difficulty.Easy,
        "svar" or "hard" => Difficulty.Hard,
        _ => Difficulty.Normal,
    };

    private static string? Option(string[] args, string name) =>
        args.FirstOrDefault(a => a.StartsWith($"--{name}="))?[(name.Length + 3)..];

    private static string Percent(int n, int of) => of == 0 ? "-" : $"{100 * n / of} %";
}
