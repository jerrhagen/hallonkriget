using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;
using Xunit.Abstractions;

namespace Hallonkriget.Sim.Tests.Economy;

/// <summary>
/// En hel gård från stugan till knäckebröd: skog, ved, sågbod, sten, brunn, åker med råg, kvarn och
/// bagarstuga, längs en stig. Sex arbetare finns från start, resten läggs till direkt i stället för att utbildas.
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

        // Byggplatserna får material i placeringsordning. Startförrådets 6 sten räcker precis till de
        // fyra första, och stenröjaren måste finnas innan något annat kan byggas. Sedan kafferepet,
        // så att knäckebrödet kommer på bordet så tidigt som möjligt.
        farm.Place("skogshuggarkojan", 3, 11);
        farm.Place("sagboden", 11, 10);
        farm.Place("vedboden", 9, 11);
        farm.Place("stenrojarboden", 34, 11);
        farm.Place("kafferepet", 13, 10);
        farm.Place("boden", 15, 10);
        farm.Place("brunnen", 19, 11);
        // Tre åkrar med råg och två bagarstugor. En person äter ett knäckebröd var tredje minut, och
        // en åker räcker till ungefär fyra knäckebröd i minuten. Gården har 19 personer.
        foreach (int x in new[] { 22, 17, 24 })
        {
            var aker = farm.Place("akern", x, 10);
            farm.Command(CommandType.SelectRecipe, aker.Id, 1); // råg
        }
        farm.Place("kvarnen", 26, 10);
        farm.Place("bagarstugan", 30, 10);
        farm.Place("bagarstugan", 28, 10);

        // Sex arbetare finns från start. Resten läggs till direkt, i stället för bygdegården.
        var door = farm.Home.Entrance;
        foreach (var job in new[] { "bonde", "bonde", "mjolnare", "bagare", "bagare" })
            farm.State.SpawnPerson(0, PersonRole.Worker, door, job);
        for (int i = 0; i < 3; i++) farm.State.SpawnPerson(0, PersonRole.Carrier, door);
        return farm;
    }

    private static int Bread(Farm farm) => farm.State.Players[0].Produced[farm.G("knackebrod")];

    [Fact]
    public void FarmBakesCrispbreadSteadilyAndFeedsItself()
    {
        var farm = Build();
        int built = farm.RunUntil(() => farm.State.Buildings.All(b => b.Stage == BuildingStage.Done), 30 * 600, "allt byggt");
        _out.WriteLine($"Allt byggt efter {built / 600.0:F1} min");

        // Gården har gått hungrig medan den byggdes. Låt den komma i gång i tio minuter, kör sedan
        // 30 minuter och räkna knäckebröd per 5 minuter. Ingen ska behöva ge upp av hunger.
        farm.RunUntil(() => Bread(farm) > 0, 30 * 600, "första knäckebrödet");
        farm.Run(10 * 600);
        var perWindow = new List<int>();
        int last = Bread(farm), gaveUp = farm.State.Players[0].GaveUp;
        for (int w = 0; w < 6; w++)
        {
            farm.Run(5 * 600);
            int now = Bread(farm);
            perWindow.Add(now - last);
            last = now;
        }
        var table = farm.State.Buildings.First(b => b.Def.Id == "kafferepet");
        _out.WriteLine("Knäckebröd per 5 min: " + string.Join(", ", perWindow));
        _out.WriteLine($"På bordet: {table.InputCount(farm.G("knackebrod"))}, medelhumör {farm.State.People.Average(p => p.Mood):F0}");

        Assert.All(perWindow, n => Assert.True(n >= 20, $"för lite knäckebröd: {string.Join(", ", perWindow)}"));
        Assert.True(perWindow.Min() * 2 >= perWindow.Max(), $"ojämn takt: {string.Join(", ", perWindow)}");
        Assert.Equal(gaveUp, farm.State.Players[0].GaveUp);
    }
}
