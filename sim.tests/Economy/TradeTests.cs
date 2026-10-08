using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Economy;

public class TradeTests
{
    /// <summary>En gård med landsvägen längs rad 2.</summary>
    private static Farm WithRoad()
    {
        var farm = new Farm(width: 30, height: 20);
        for (int x = 0; x < 30; x++) farm.State.Map.SetTerrain(new TilePoint(x, 2), Terrain.Road);
        return farm;
    }

    private static int Recipe(Farm farm, Building shop, string buy, string pay) =>
        Array.FindIndex(shop.Def.Recipes, r => r.Out[0].Good == farm.G(buy) && r.In.Any(a => a.Good == farm.G(pay)));

    [Fact]
    public void LanthandelnStandsByTheRoad()
    {
        var farm = WithRoad();
        var def = farm.Data.Building("lanthandeln");
        Assert.True(farm.State.CanPlace(0, def, new TilePoint(10, 3)));   // rad 2 ligger intill
        Assert.False(farm.State.CanPlace(0, def, new TilePoint(10, 10))); // långt från vägen
    }

    [Fact]
    public void PricesMatchTheTable()
    {
        var farm = WithRoad();
        var def = farm.Data.Building("lanthandeln");
        // Kaffe: 3 ägg, 1 smör, 2 ved eller 1 korv. Verktyg: 3 ved.
        var coffee = def.Recipes.Where(r => r.Out[0].Good == farm.G("kaffe"))
            .Select(r => (farm.Data.Goods[r.In[0].Good].Id, r.In[0].Count)).ToList();
        Assert.Equal(new[] { ("agg", 3), ("smor", 1), ("ved", 2), ("korv", 1) }, coffee);
        var tools = Assert.Single(def.Recipes, r => r.Out[0].Good == farm.G("verktyg"));
        Assert.Equal(new GoodAmount(farm.G("ved"), 3), Assert.Single(tools.In));
        Assert.All(def.Recipes, r => Assert.Equal(200, r.Ticks));
    }

    [Fact]
    public void NothingIsTradedUntilThePlayerChooses()
    {
        var farm = WithRoad();
        var shop = farm.PlaceDone("lanthandeln", 10, 3);
        Assert.Equal(0, shop.InputSpace(farm.G("ved")));
        farm.Command(CommandType.SelectRecipe, shop.Id, Recipe(farm, shop, "kaffe", "ved"));
        Assert.Equal(Building.StockLimit, shop.InputSpace(farm.G("ved")));
        Assert.Equal(0, shop.InputSpace(farm.G("agg")));
    }

    [Fact]
    public void BuysCoffeeForFirewood()
    {
        var farm = WithRoad();
        var shop = farm.PlaceDone("lanthandeln", 10, 3);
        farm.State.SpawnPerson(0, PersonRole.Worker, shop.Entrance, "handlare");
        farm.Command(CommandType.SelectRecipe, shop.Id, Recipe(farm, shop, "kaffe", "ved"));
        farm.Put(shop, "ved", 3);
        farm.RunUntil(() => shop.OutputCount(farm.G("kaffe")) == 1, 300, "en kopp kaffe");
        Assert.Equal(1, shop.InputCount(farm.G("ved")));
    }

    [Fact]
    public void SugarAndPetrolAreOnlyForStorgarden()
    {
        var farm = WithRoad();
        var shop = farm.PlaceDone("lanthandeln", 10, 3); // spelare 0, Torpet
        farm.Command(CommandType.SelectRecipe, shop.Id, Recipe(farm, shop, "socker", "agg"));
        Assert.Equal(-1, shop.SelectedRecipe);
        farm.Command(CommandType.SelectRecipe, shop.Id, Recipe(farm, shop, "salt", "agg"));
        Assert.NotEqual(-1, shop.SelectedRecipe);

        int count = farm.State.Buildings.Count;
        farm.Command(CommandType.PlaceBuilding, shop.Def.Index, 20, 3, player: 1);
        var theirs = farm.State.Buildings[count];
        Assert.Equal(1, theirs.Owner);
        farm.Command(CommandType.SelectRecipe, theirs.Id, Recipe(farm, theirs, "socker", "agg"), player: 1);
        Assert.NotEqual(-1, theirs.SelectedRecipe);
    }

    [Fact]
    public void ClosedOnSundays()
    {
        var farm = WithRoad();
        var shop = farm.PlaceDone("lanthandeln", 10, 3);
        shop.HasWorker = true;
        shop.SelectRecipe(Recipe(farm, shop, "verktyg", "ved"));
        farm.Put(shop, "ved", 3);
        Assert.Null(shop.UpdateProduction(0, (_, _) => true, closed: true));
        Assert.Equal(-1, shop.CurrentRecipe);
        shop.UpdateProduction(0, (_, _) => true, closed: false);
        Assert.NotEqual(-1, shop.CurrentRecipe);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5142, 0)]      // måndag är 1/7 timme, 8 min 34 s
    [InlineData(5143, 1)]
    [InlineData(30857, 5)]
    [InlineData(30858, 6)]     // söndag
    [InlineData(35999, 6)]
    [InlineData(36000, 0)]     // en ny vecka efter en timme
    public void TheWeekIsOneHourLong(int tick, int weekday)
    {
        Assert.Equal(weekday, GameClock.Weekday(tick));
        Assert.Equal(weekday == 6, GameClock.IsSunday(tick));
    }
}
