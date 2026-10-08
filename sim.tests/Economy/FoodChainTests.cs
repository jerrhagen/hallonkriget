using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;
using Xunit.Abstractions;

namespace Hallonkriget.Sim.Tests.Economy;

/// <summary>
/// Fas 2: hela matkedjan spelbar. En gård på kartan hemmanet med alla matens byggnader färdiga och
/// bemannade, och bärare som sköter resten. All mat ska komma fram till kafferepet: knäckebröd,
/// pannkakor, korv, abborre, sylt, svagdricka och kaffe från lanthandeln.
/// </summary>
public class FoodChainTests
{
    private readonly ITestOutputHelper _out;

    public FoodChainTests(ITestOutputHelper output) => _out = output;

    private static readonly string[] Foods = { "knackebrod", "pannkakor", "korv", "abborre", "sylt", "svagdricka", "kaffe" };

    public static (Farm Farm, Building Table) Build()
    {
        var farm = Farm.OnMap("hemmanet");
        // Stigen längs byggnaderna, från stugans dörr, och ner till lanthandeln vid landsvägen.
        farm.LayPath(new TilePoint(3, 5), new TilePoint(3, 12));
        farm.LayPath(new TilePoint(3, 12), new TilePoint(57, 12));
        farm.LayPath(new TilePoint(43, 13), new TilePoint(43, 46));

        var table = farm.PlaceDone("kafferepet", 5, 10);
        farm.PlaceDone("boden", 8, 10);
        string[] row =
        {
            "bagarstugan:11,10", "koket:14,10", "kvarnen:17,10", "bryggstugan:20,10", "rokeriet:23,11",
            "slakteriet:25,10", "syltkoket:28,11", "brunnen:30,11", "vedboden:32,11", "ladugarden:34,9",
            "honshuset:38,10", "grisstian:41,10", "brunnen:46,11",
            "skogshuggarkojan:4,14", "angen:14,14", "bikuporna:17,16", "barplockarstugan:24,14",
            "fiskeboden:29,16", "lanthandeln:44,45",
        };
        foreach (var spec in row)
        {
            var parts = spec.Split(':', ',');
            var b = farm.PlaceDone(parts[0], int.Parse(parts[1]), int.Parse(parts[2]));
            farm.State.SpawnPerson(0, PersonRole.Worker, farm.Home.Entrance, b.Def.Worker!);
        }
        // Två åkrar vete (kvarnen och hönshuset), två råg (kvarnen och bryggstugan) och en potatis.
        foreach (var (x, y, crop) in new[] { (47, 10, 0), (52, 10, 0), (47, 13, 1), (52, 13, 1), (57, 10, 2) })
        {
            var aker = farm.PlaceDone("akern", x, y);
            farm.Command(CommandType.SelectRecipe, aker.Id, crop); // vete, råg, potatis
            farm.State.SpawnPerson(0, PersonRole.Worker, farm.Home.Entrance, "bonde");
        }

        var picker = farm.State.Buildings.Single(b => b.Def.Id == "barplockarstugan");
        farm.Command(CommandType.SelectRecipe, picker.Id, 0); // hallon, inte kårt

        var shop = farm.State.Buildings.Single(b => b.Def.Id == "lanthandeln");
        int coffeeForWood = Array.FindIndex(shop.Def.Recipes,
            r => r.Out[0].Good == farm.G("kaffe") && r.In.Length == 1 && r.In[0].Good == farm.G("ved"));
        farm.Command(CommandType.SelectRecipe, shop.Id, coffeeForWood);
        farm.Command(CommandType.BlockGood, table.Id, farm.G("kaffe"), 0); // kaffet får ätas
        for (int i = 0; i < 20; i++) farm.State.SpawnPerson(0, PersonRole.Carrier, farm.Home.Entrance);
        return (farm, table);
    }

    [Fact]
    public void EveryFoodReachesTheKafferep()
    {
        var (farm, table) = Build();
        var reached = new int[farm.Data.Goods.Count];
        for (int minute = 1; minute <= 40; minute++)
        {
            for (int t = 0; t < 600; t++)
            {
                farm.Run(1);
                if (t % 10 != 0) continue;
                foreach (var f in Foods)
                    if (table.InputCount(farm.G(f)) > 0 || farm.State.Players[0].Eaten[farm.G(f)] > 0) reached[farm.G(f)]++;
            }
            if (Foods.All(f => reached[farm.G(f)] > 0)) break;
        }
        _out.WriteLine(string.Join(", ", Foods.Select(f => $"{f}: ätit {farm.State.Players[0].Eaten[farm.G(f)]}, gjort {farm.State.Players[0].Produced[farm.G(f)]}")));
        var missing = Foods.Where(f => reached[farm.G(f)] == 0).ToList();
        Assert.True(missing.Count == 0, "kom aldrig till kafferepet: " + string.Join(", ", missing));
    }
}
