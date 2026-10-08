using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Buildings;

public class BuildingTests
{
    private static GameData Data => TestData.Game;
    private static int G(string id) => Data.GoodIndex(id);

    private static GameState NewMatch(TilePoint? start = null) => GameState.NewMatch(new MatchSetup(
        Seed: 1, MapWidth: 30, MapHeight: 30,
        Players: new[]
        {
            new PlayerSetup(Faction.Torpet, IsComputer: false, start),
            new PlayerSetup(Faction.Storgarden, IsComputer: false),
        },
        Data: Data));

    private static Building Place(GameState s, string id, int x, int y, byte player = 0)
    {
        int count = s.Buildings.Count;
        s.Tick(new[] { new Command(s.TickCount, player, CommandType.PlaceBuilding, Data.Building(id).Index, x, y) });
        Assert.True(s.Buildings.Count == count + 1, $"{id} kunde inte placeras på ({x},{y})");
        return s.Buildings[^1];
    }

    private static bool TryPlace(GameState s, string id, int x, int y, byte player = 0)
    {
        int count = s.Buildings.Count;
        s.Tick(new[] { new Command(s.TickCount, player, CommandType.PlaceBuilding, Data.Building(id).Index, x, y) });
        return s.Buildings.Count > count;
    }

    /// <summary>Levererar allt material och bygger klart, som bärare och hantlangare kommer att göra.</summary>
    private static void Finish(Building b)
    {
        foreach (var c in b.Def.Cost)
            for (int i = 0; i < c.Count; i++) Assert.True(b.DeliverMaterial(c.Good));
        while (b.Stage == BuildingStage.Construction) b.AddWork();
    }

    private static void Run(GameState s, int ticks)
    {
        for (int i = 0; i < ticks; i++) s.Tick(Array.Empty<Command>());
    }

    [Fact]
    public void MatchStartsWithTheCottageAndItsStores()
    {
        var s = NewMatch(start: new TilePoint(5, 5));
        var stugan = Assert.Single(s.Buildings);
        Assert.Equal("stugan", stugan.Def.Id);
        Assert.Equal(BuildingStage.Done, stugan.Stage);
        // Designdokumentet: 10 brädor, 6 sten, 4 ved, 6 knäckebröd och 2 kaffe.
        Assert.Equal(10, stugan.OutputCount(G("brador")));
        Assert.Equal(6, stugan.OutputCount(G("sten")));
        Assert.Equal(4, stugan.OutputCount(G("ved")));
        Assert.Equal(6, stugan.OutputCount(G("knackebrod")));
        Assert.Equal(2, stugan.OutputCount(G("kaffe")));
        Assert.Equal(new TilePoint(6, 8), stugan.Entrance);
        Assert.Equal(1, s.Map.OccupantAt(new TilePoint(7, 7)));
        Assert.Equal(0, s.Map.OwnerAt(new TilePoint(7, 7)));
    }

    [Fact]
    public void StorageHoldsThirtyGoodsInTotal()
    {
        var s = NewMatch(start: new TilePoint(5, 5));
        var stugan = s.Buildings[0];
        // Startförrådet är 30 varor: 10 brädor, 6 sten, 4 ved, 6 knäckebröd, 2 kaffe och 2 verktyg.
        Assert.Equal(0, stugan.InputSpace(G("ved")));
        Assert.False(stugan.PutInput(G("timmer")));
        Assert.True(stugan.TakeOutput(G("kaffe")));
        Assert.True(stugan.TakeOutput(G("kaffe")));
        Assert.True(stugan.PutInput(G("timmer")));
        Assert.True(stugan.PutInput(G("timmer")));
        Assert.False(stugan.PutInput(G("timmer")));
    }

    [Fact]
    public void PlacingABuildingMakesAConstructionSite()
    {
        var s = NewMatch();
        var b = Place(s, "vedboden", 10, 10);
        Assert.Equal(BuildingStage.Construction, b.Stage);
        Assert.Equal(b.Id + 1, s.Map.OccupantAt(new TilePoint(10, 10)));
        Assert.Equal(new TilePoint(10, 11), b.Entrance);
        Assert.Equal(3, b.MaterialNeeded(G("brador")));
        Assert.Equal(1, b.MaterialNeeded(G("sten")));
        Assert.Equal(0, b.MaterialNeeded(G("ved")));
        Assert.Equal(0, b.InputSpace(G("timmer"))); // en byggplats tar inte emot varor
    }

    [Fact]
    public void PlacementRules()
    {
        var s = NewMatch();
        s.Map.SetTerrain(new TilePoint(20, 20), Terrain.Water);
        s.Map.SetTerrain(new TilePoint(3, 21), Terrain.Water);

        Assert.False(TryPlace(s, "vedboden", 20, 20), "på vatten");
        Assert.False(TryPlace(s, "sagboden", 29, 5), "utanför kartan");
        Assert.False(TryPlace(s, "vedboden", 5, 29), "dörren utanför kartan");
        Assert.False(TryPlace(s, "vedboden", 3, 20), "dörren i vattnet");
        Assert.False(TryPlace(s, "stugan", 2, 2), "stugan byggs inte, den finns från start");

        Place(s, "sagboden", 10, 10);
        Assert.False(TryPlace(s, "vedboden", 11, 11), "ovanpå en annan byggnad");
        Assert.False(TryPlace(s, "vedboden", 11, 12), "på sågbodens dörr");
        Assert.False(TryPlace(s, "vedboden", 11, 9), "så att dörren hamnar i sågboden");
        Assert.True(TryPlace(s, "vedboden", 12, 11), "bredvid dörren går bra");

        Assert.False(TryPlace(s, "vedboden", 10, 10, player: 1), "på någon annans mark");
        Assert.False(TryPlace(s, "vedboden", 1, 1, player: 5), "spelaren finns inte");
    }

    [Fact]
    public void BuildingOnAPathRemovesThePath()
    {
        var s = NewMatch();
        s.Map.SetPath(new TilePoint(10, 10), PathState.Trodden);
        Place(s, "vedboden", 10, 10);
        Assert.Equal(PathState.None, s.Map.PathAt(new TilePoint(10, 10)));
    }

    [Fact]
    public void ConstructionGoesAsFarAsTheMaterialAllows()
    {
        var s = NewMatch();
        var b = Place(s, "vedboden", 10, 10); // 3 brädor + 1 sten, 60 sekunder
        Assert.Equal(600, b.Def.BuildTicks);
        Assert.False(b.AddWork());
        Assert.Equal(0, b.WorkDone);

        b.DeliverMaterial(G("brador"));
        b.DeliverMaterial(G("brador"));
        while (b.AddWork()) { }
        for (int i = 0; i < 1000; i++) b.AddWork();
        Assert.Equal(300, b.WorkDone); // halva materialet, halva bygget
        Assert.False(b.DeliverMaterial(G("ved")));

        b.DeliverMaterial(G("brador"));
        b.DeliverMaterial(G("sten"));
        Assert.False(b.DeliverMaterial(G("sten")));
        int ticks = 0;
        while (!b.AddWork()) ticks++;
        Assert.Equal(299, ticks);
        Assert.Equal(BuildingStage.Done, b.Stage);
    }

    [Fact]
    public void ProductionNeedsWorkerAndInputs()
    {
        var s = NewMatch();
        var vedboden = Place(s, "vedboden", 10, 10); // timmer -> ved ×3 på 20 s
        Finish(vedboden);
        Assert.Equal(5, vedboden.InputSpace(G("timmer")));
        Assert.Equal(0, vedboden.InputSpace(G("ved")));

        vedboden.PutInput(G("timmer"));
        Run(s, 400);
        Assert.Equal(0, vedboden.OutputCount(G("ved"))); // ingen vedhuggare

        vedboden.HasWorker = true;
        Run(s, 200);
        Assert.Equal(0, vedboden.OutputCount(G("ved")));
        Run(s, 1);
        Assert.Equal(3, vedboden.OutputCount(G("ved")));
        Assert.Equal(0, vedboden.InputCount(G("timmer")));
    }

    [Fact]
    public void FullOutputStopsProduction()
    {
        var s = NewMatch();
        var vedboden = Place(s, "vedboden", 10, 10);
        Finish(vedboden);
        vedboden.HasWorker = true;
        for (int i = 0; i < 3; i++) vedboden.PutInput(G("timmer"));
        Run(s, 1000);
        Assert.Equal(3, vedboden.OutputCount(G("ved"))); // 3 + 3 > 5, så nästa omgång väntar
        Assert.Equal(2, vedboden.InputCount(G("timmer")));

        vedboden.TakeOutput(G("ved"));
        Run(s, 1000);
        Assert.Equal(5, vedboden.OutputCount(G("ved")));
        Assert.Equal(1, vedboden.InputCount(G("timmer")));
    }

    [Fact]
    public void GatherersNeedTheirTerrainNearby()
    {
        var s = NewMatch();
        var koja = Place(s, "skogshuggarkojan", 10, 10);
        Finish(koja);
        koja.HasWorker = true;
        Run(s, 400);
        Assert.Equal(0, koja.OutputCount(G("timmer")));

        s.Map.SetTerrain(new TilePoint(10, 19), Terrain.Forest); // 8 rutor från dörren
        Run(s, 301);
        Assert.Equal(1, koja.OutputCount(G("timmer")));
    }

    [Fact]
    public void MillTakesTurnsUnlessARecipeIsChosen()
    {
        var s = NewMatch();
        var kvarn = Place(s, "kvarnen", 10, 10); // vete -> mjöl, råg -> rågmjöl, 20 s
        Finish(kvarn);
        kvarn.HasWorker = true;
        for (int i = 0; i < 2; i++)
        {
            kvarn.PutInput(G("vete"));
            kvarn.PutInput(G("rag"));
        }
        Run(s, 402);
        Assert.Equal(1, kvarn.OutputCount(G("mjol")));
        Assert.Equal(1, kvarn.OutputCount(G("ragmjol")));

        s.Tick(new[] { new Command(s.TickCount, 0, CommandType.SelectRecipe, kvarn.Id, 1) });
        Assert.Equal(0, kvarn.InputSpace(G("vete"))); // vete behövs inte längre
        Run(s, 500);
        Assert.Equal(1, kvarn.OutputCount(G("mjol")));
        Assert.Equal(2, kvarn.OutputCount(G("ragmjol")));
        Assert.Equal(1, kvarn.InputCount(G("vete")));
    }

    [Fact]
    public void OnlyTheOwnerCanChooseRecipe()
    {
        var s = NewMatch();
        var kvarn = Place(s, "kvarnen", 10, 10);
        s.Tick(new[] { new Command(s.TickCount, 1, CommandType.SelectRecipe, kvarn.Id, 1) });
        Assert.Equal(-1, kvarn.SelectedRecipe);
    }

    [Fact]
    public void BakeryMakesCrispbread()
    {
        var s = NewMatch();
        var bageri = Place(s, "bagarstugan", 10, 10);
        Finish(bageri);
        bageri.HasWorker = true;
        foreach (var g in new[] { "ragmjol", "vatten", "ved" }) bageri.PutInput(G(g));
        Run(s, 301);
        Assert.Equal(3, bageri.OutputCount(G("knackebrod")));
    }

    [Fact]
    public void BuildingsAreInTheHash()
    {
        var a = NewMatch();
        var b = NewMatch();
        Assert.Equal(a.Hash(), b.Hash());
        Place(a, "vedboden", 10, 10);
        Run(b, 1);
        Assert.NotEqual(a.Hash(), b.Hash());
        Place(b, "vedboden", 10, 10);
        Run(a, 1);
        Assert.Equal(a.Hash(), b.Hash());
        a.Buildings[0].DeliverMaterial(G("sten"));
        Assert.NotEqual(a.Hash(), b.Hash());
    }
}
