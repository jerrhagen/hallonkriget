using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Ai;

public class BuildOrderTests
{
    private static BuildOrder Order(string steps) =>
        BuildOrder.Parse($$"""{ "id": "test", "steps": [ {{steps}} ] }""", TestData.Game);

    private static void Play(Farm farm, BuildOrderPlayer player, int ticks)
    {
        for (int i = 0; i < ticks; i++) farm.State.Tick(player.Next(farm.State));
    }

    [Fact]
    public void PlacesBuildingsAndRemembersTheirNames()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var player = new BuildOrderPlayer(Order("""
            { "place": "vedboden", "at": [12, 6], "name": "ved" },
            { "place": "akern", "at": [16, 6], "name": "aker" },
            { "recipe": "aker", "index": 1 },
            { "path": [[10, 10], [14, 12]] }
            """), 0);
        // Receptet väntar ett tick, tills åkern som placerades i samma tick finns.
        Play(farm, player, 1);
        Assert.False(player.Finished);
        Play(farm, player, 1);
        Assert.True(player.Finished);
        Assert.Equal("vedboden", farm.State.Buildings[player.BuildingId("ved")].Def.Id);
        var aker = farm.State.Buildings[player.BuildingId("aker")];
        Assert.Equal(1, aker.SelectedRecipe);
        Assert.Equal(PathState.Planned, farm.State.Map.PathAt(new TilePoint(14, 10)));
        Assert.Equal(PathState.Planned, farm.State.Map.PathAt(new TilePoint(14, 12)));
        Assert.Empty(player.Problems);
    }

    [Fact]
    public void StepsWaitForTheMinuteAndForABuildingToBeDone()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var player = new BuildOrderPlayer(Order("""
            { "place": "boden", "at": [12, 6], "name": "bod" },
            { "place": "vedboden", "at": [16, 6], "when_done": "bod" },
            { "place": "brunnen", "at": [18, 6], "minute": 30 }
            """), 0);
        Play(farm, player, 1);
        Assert.Equal(2, farm.State.Buildings.Count);
        var bod = farm.State.Buildings[1];
        int waited = 0;
        while (bod.Stage != BuildingStage.Done)
        {
            Play(farm, player, 1);
            waited++;
            Assert.Equal(2, farm.State.Buildings.Count);
        }
        Play(farm, player, 1);
        Assert.Equal(3, farm.State.Buildings.Count);
        Assert.False(player.Finished); // brunnen väntar på minut 30
        Assert.True(farm.State.TickCount < 30 * 600);
    }

    [Fact]
    public void WaitsUntilEnoughHasBeenMade()
    {
        var farm = new Farm(width: 20, height: 12);
        var player = new BuildOrderPlayer(Order("""
            { "place": "brunnen", "at": [3, 3], "name": "brunn" },
            { "place": "vedboden", "at": [8, 3], "when_produced": { "vatten": 2 } }
            """), 0);
        Play(farm, player, 1);
        var well = farm.State.Buildings[0];
        well.CompleteAtOnce(Array.Empty<GoodAmount>());
        farm.State.SpawnPerson(0, Hallonkriget.Sim.People.PersonRole.Worker, well.Entrance, "vattenbarare");
        int ticks = 0;
        while (farm.State.Buildings.Count == 1 && ticks++ < 1000) Play(farm, player, 1);
        Assert.Equal(2, farm.State.Players[0].Produced[farm.G("vatten")]);
        Assert.Equal(2, farm.State.Buildings.Count);
    }

    [Fact]
    public void TrainsAndTradesByName()
    {
        var farm = new Farm(width: 30, height: 20);
        for (int x = 0; x < 30; x++) farm.State.Map.SetTerrain(new TilePoint(x, 2), Terrain.Road);
        var player = new BuildOrderPlayer(Order("""
            { "place": "bygdegarden", "at": [3, 8], "name": "bygd" },
            { "place": "lanthandeln", "at": [10, 3], "name": "handel" },
            { "trade": "handel", "buy": "kaffe", "pay": "agg" },
            { "unblock": "bygd", "good": "kaffe" },
            { "train": "bygd", "profession": "barare", "count": 2, "when_done": "bygd" }
            """), 0);
        Play(farm, player, 2);
        var shop = farm.State.Buildings[1];
        Assert.Equal(farm.G("kaffe"), shop.Def.Recipes[shop.SelectedRecipe].Out[0].Good);
        Assert.Equal(new GoodAmount(farm.G("agg"), 3), shop.Def.Recipes[shop.SelectedRecipe].In[0]);
        farm.State.Buildings[0].CompleteAtOnce(Array.Empty<GoodAmount>());
        Play(farm, player, 1);
        Assert.Equal(2, farm.State.Buildings[0].TrainingQueue.Count);
        Assert.True(player.Finished);
    }

    [Fact]
    public void ABuildingThatDoesNotFitIsReported()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var player = new BuildOrderPlayer(Order("""
            { "place": "boden", "at": [5, 5], "name": "bod" },
            { "train": "bod", "profession": "barare" }
            """), 0);
        Play(farm, player, 1);
        Assert.Equal(2, player.Problems.Count);
        Assert.Single(farm.State.Buildings);
    }

    [Theory]
    [InlineData("""{ "place": "slott", "at": [1, 1] }""", "slott")]
    [InlineData("""{ "recipe": "aker", "index": 1 }""", "aker")]
    [InlineData("""{ "flyga": true }""", "okänt steg")]
    [InlineData("""{ "place": "boden", "at": [1, 1], "name": "a" }, { "place": "boden", "at": [4, 1], "name": "a" }""", "finns redan")]
    public void MistakesInTheFileAreExplained(string steps, string expected)
    {
        var e = Assert.Throws<GameDataException>(() => Order(steps));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void TheRealBuildOrdersLoad()
    {
        var order = TestData.Order("brodgarden");
        Assert.Equal("brodgarden", order.MapId);
        Assert.NotEmpty(order.Steps);
    }
}
