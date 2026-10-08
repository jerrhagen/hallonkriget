using System.Diagnostics;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

// Kör en match utan grafik: en byggordning från data/ai på dess karta, och skriver produktion per
// minut. Till sist kontrollfrågan för fas 1: bakar gården knäckebröd i jämn takt de sista 30 minuterna?
//
//   dotnet run --project sim.cli -- [byggordning] [minuter] [frö]
//   dotnet run --project sim.cli -- brodgarden 90
//   dotnet run --project sim.cli -- brodgarden 10 --spela-in=sim.tests/Replays/brodgarden.hkr
//   dotnet run --project sim.cli -- dator 1000 normal     (datorspelare mot datorspelare, se Tournament.cs)

if (args.FirstOrDefault() == "dator")
{
    Tournament.Run(args[1..], FindDataDir());
    return;
}

var positional = args.Where(a => !a.StartsWith("--")).ToArray();
string orderId = positional.Length > 0 ? positional[0] : "brodgarden";
int minutes = positional.Length > 1 ? int.Parse(positional[1]) : 90;
ulong seed = positional.Length > 2 ? ulong.Parse(positional[2]) : 1958_07_14UL;
string? recordTo = args.FirstOrDefault(a => a.StartsWith("--spela-in="))?["--spela-in=".Length..];

string dataDir = FindDataDir();
var data = GameData.FromFiles(name => File.ReadAllText(Path.Combine(dataDir, name)));
var order = BuildOrder.Parse(File.ReadAllText(Path.Combine(dataDir, "ai", orderId + ".json")), data);
var map = MapDef.Parse(File.ReadAllText(Path.Combine(dataDir, "maps", order.MapId + ".json")));
var setup = MatchSetup.OnMap(seed, map, data, (Faction.Torpet, false));
var state = GameState.NewMatch(setup);
var replay = new Hallonkriget.Sim.Replay.Replay(seed, map, setup.Players, data.Fingerprint);
var player = new BuildOrderPlayer(order, 0);

int bread = data.GoodIndex("knackebrod");
string[] watch = { "timmer", "brador", "ved", "sten", "vatten", "rag", "ragmjol", "knackebrod", "kaffe", "verktyg" };
Console.WriteLine($"Byggordning {order.Id} på {map.Id}, {minutes} minuter, frö {seed}");
Console.WriteLine("Produktion under minuten, och läget när minuten är slut.");
Console.WriteLine($"{"min",3} {string.Join(" ", watch.Select(w => Short(w)))}  folk sov hum gav bord klart väntar");

var sw = Stopwatch.StartNew();
var perMinute = new List<int>();
var gaveUpAt = new List<int>();
var produced = new int[data.Goods.Count];
for (int m = 1; m <= minutes; m++)
{
    Array.Copy(state.Players[0].Produced, produced, produced.Length);
    for (int t = 0; t < 60 * GameState.TicksPerSecond; t++)
    {
        var commands = player.Next(state);
        replay.Record(state.TickCount, commands);
        state.Tick(commands);
        replay.AfterTick(state);
    }

    var now = state.Players[0].Produced;
    perMinute.Add(now[bread] - produced[bread]);
    gaveUpAt.Add(state.Players[0].GaveUp);
    var people = state.People.Where(p => p.Owner == 0).ToList();
    var table = state.Buildings.FirstOrDefault(b => b.Def.Table > 0 && b.Stage == BuildingStage.Done);
    Console.WriteLine($"{m,3} {string.Join(" ", watch.Select(w => $"{now[data.GoodIndex(w)] - produced[data.GoodIndex(w)],6}"))}" +
        $"  {people.Count,4} {state.Beds(0),3} {(people.Count == 0 ? 0 : people.Average(p => p.Mood)),3:F0} {state.Players[0].GaveUp,3}" +
        $" {(table is null ? "-" : table.InputTotal.ToString()),4} {state.Buildings.Count(b => b.Stage == BuildingStage.Done),3}/{state.Buildings.Count,-2}" +
        $" {people.Count(p => p.WaitTicks > 0),5}");
}
sw.Stop();

foreach (var problem in player.Problems) Console.WriteLine($"Problem i byggordningen: {problem}");
if (!player.Finished) Console.WriteLine("Byggordningen hann inte bli klar.");

// Kontrollfrågan: de sista 30 minuterna i sex fönster om 5. Jämn takt betyder att inget fönster
// har mindre än hälften av det bästa, och ingen ger upp av hunger under tiden.
Console.WriteLine();
if (perMinute.Count < 40 || perMinute.Take(perMinute.Count - 30).All(n => n == 0))
{
    Console.WriteLine("Kontrollfrågan: för kort körning, eller inget knäckebröd före de sista 30 minuterna.");
}
else
{
    int from = perMinute.Count - 30;
    var windows = Enumerable.Range(0, 6).Select(w => perMinute.Skip(from + 5 * w).Take(5).Sum()).ToList();
    int gaveUp = gaveUpAt.Count > from ? gaveUpAt[^1] - gaveUpAt[from - 1] : 0;
    bool steady = windows.Min() > 0 && windows.Min() * 2 >= windows.Max() && gaveUp == 0;
    Console.WriteLine($"Kontrollfrågan, minut {from + 1}–{perMinute.Count}: knäckebröd per 5 minuter {string.Join(", ", windows)}, " +
        $"{gaveUp} gav upp av hunger.");
    Console.WriteLine(steady ? "Jämn takt: ja." : "Jämn takt: nej.");
}
if (args.Contains("--byggnader"))
{
    Console.WriteLine();
    Console.WriteLine("Byggnaderna när körningen är slut:");
    foreach (var b in state.Buildings)
    {
        string Goods(Func<int, int> count) => string.Join(" ", Enumerable.Range(0, data.Goods.Count)
            .Where(g => count(g) > 0).Select(g => $"{data.Goods[g].Id}:{count(g)}"));
        string queue = b.TrainingQueue.Count > 0 ? $" kö [{string.Join(", ", b.TrainingQueue.Select(q => q.Id))}]" : "";
        Console.WriteLine($"  {b.Id,2} {b.Def.Id,-17} {b.Stage,-12} {(b.HasWorker ? "arbetare" : (b.WorkerId >= 0 ? "ute" : "-")),-8}" +
            $" in {{{Goods(b.InputCount)}}} ut {{{Goods(b.OutputCount)}}}{queue}");
    }
}
if (recordTo is not null)
{
    File.WriteAllBytes(recordTo, replay.ToBytes());
    Console.WriteLine($"Reprisen sparad: {recordTo}");
}
Console.WriteLine($"{minutes} minuter på {sw.ElapsedMilliseconds} ms, slutlig kontrollsumma {state.Hash():x16}");

static string Short(string id) => (id.Length > 6 ? id[..6] : id).PadLeft(6);

static string FindDataDir()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "data", "goods.json"))) return Path.Combine(dir.FullName, "data");
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "data", "goods.json"))) return Path.Combine(dir.FullName, "data");
    throw new DirectoryNotFoundException("Hittar inte data/");
}
