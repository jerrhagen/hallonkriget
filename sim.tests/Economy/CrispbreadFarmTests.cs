using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;
using Xunit.Abstractions;

namespace Hallonkriget.Sim.Tests.Economy;

/// <summary>
/// En hel gård från stugan till knäckebröd: skog, ved, sågbod, sten, brunn, åker med råg, kvarn och
/// bagarstuga, längs en stig. Arbetarna läggs till direkt eftersom bygdegården inte finns än.
/// Föregångare till fas 1:s kontrollfråga, som ska köras från sim.cli.
/// </summary>
public class CrispbreadFarmTests
{
    private readonly ITestOutputHelper _out;

    public CrispbreadFarmTests(ITestOutputHelper output) => _out = output;

    public static Farm Build()
    {
        var farm = new Farm(width: 44, height: 24, start: new TilePoint(5, 5));
        var map = farm.State.Map;
        for (int y = 14; y <= 19; y++)
        for (int x = 1; x <= 9; x++) map.SetTerrain(new TilePoint(x, y), Terrain.Forest);
        for (int y = 14; y <= 16; y++)
        for (int x = 33; x <= 37; x++) map.SetTerrain(new TilePoint(x, y), Terrain.Stony);

        farm.PlanPath(new TilePoint(2, 12), new TilePoint(38, 12));
        farm.PlanPath(new TilePoint(6, 9), new TilePoint(6, 11));

        farm.Place("skogshuggarkojan", 3, 11);
        farm.Place("sagboden", 11, 10);
        farm.Place("vedboden", 9, 11);
        farm.Place("stenrojarboden", 34, 11);
        farm.Place("boden", 15, 10);
        farm.Place("brunnen", 19, 11);
        var aker = farm.Place("akern", 22, 10);
        farm.Command(CommandType.SelectRecipe, aker.Id, 1); // råg
        farm.Place("kvarnen", 26, 10);
        farm.Place("bagarstugan", 30, 10);

        var door = farm.Home.Entrance;
        foreach (var job in new[] { "skogshuggare", "vedhuggare", "sagare", "stenrojare", "vattenbarare", "bonde", "mjolnare", "bagare" })
            farm.State.SpawnPerson(0, PersonRole.Worker, door, job);
        for (int i = 0; i < 3; i++) farm.State.SpawnPerson(0, PersonRole.Carrier, door);
        return farm;
    }

    private static int Bread(Farm farm) => farm.State.Players[0].Produced[farm.G("knackebrod")];

    /// <summary>
    /// Äter upp knäckebrödet, som kafferepet kommer att göra. Förråden blir fulla av annat med tiden
    /// (vatten, sten, brädor), så brödet hämtas också direkt ur bagarstugan.
    /// </summary>
    private static void Eat(Farm farm)
    {
        foreach (var b in farm.State.Buildings)
            while (b.OutputCount(farm.G("knackebrod")) - b.Outgoing[farm.G("knackebrod")] > 0) b.TakeOutput(farm.G("knackebrod"));
    }

    [Fact]
    public void FarmBakesCrispbreadSteadily()
    {
        var farm = Build();
        int built = farm.RunUntil(() => farm.State.Buildings.All(b => b.Stage == BuildingStage.Done), 20 * 600, "allt byggt");
        _out.WriteLine($"Allt byggt efter {built / 600.0:F1} min");

        // Vänta tills det första brödet kommer, kör sedan 30 minuter och räkna knäckebröd per 5 minuter.
        int firstBread = farm.RunUntil(() => Bread(farm) > 0, 30 * 600, "första knäckebrödet");
        _out.WriteLine($"Första knäckebrödet {firstBread / 600.0:F1} min efter att allt var byggt");
        Eat(farm);
        var perWindow = new List<int>();
        int last = Bread(farm);
        for (int w = 0; w < 6; w++)
        {
            for (int m = 0; m < 5; m++)
            {
                farm.Run(600);
                Eat(farm);
            }
            int now = Bread(farm);
            perWindow.Add(now - last);
            last = now;
        }
        _out.WriteLine("Knäckebröd per 5 min: " + string.Join(", ", perWindow));
        _out.WriteLine("Bärare som väntar just nu: " + farm.State.People.Count(p => p.WaitTicks > 0));

        Assert.All(perWindow, n => Assert.True(n >= 9, $"för lite knäckebröd: {string.Join(", ", perWindow)}"));
        Assert.True(perWindow.Min() * 2 >= perWindow.Max(), $"ojämn takt: {string.Join(", ", perWindow)}");
    }
}
